using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using FamilyCompany.Presentation.Unity.OfficeGridView;
using FamilyCompany.Presentation.Unity.OfficeGridView.Authoring;
using FamilyCompany.Presentation.Unity.OfficeSeating;
using FamilyCompany.Simulation.OfficeLayout;
using FamilyCompany.Simulation.OfficeWorkActions;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime.Qa
{
    /// <summary>
    /// Focused Windows-player proof for the four canonical workstation transitions.
    ///
    /// This runner deliberately has its own command-line flag so the seating contract can be
    /// exercised without coupling it to ScenePreviewJump's broad office, attendance or doorway
    /// scenarios. It samples after each rendered frame, when the runtime depth sorter has applied
    /// the final orders, and fails closed if the chair foreground layer is absent.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OfficeSeatingTransitionPlayerQa : MonoBehaviour
    {
        public const string CommandLineFlag = "-familyCompanySeatingTransitionQa";
        public const string ArtifactDirectoryArgument = "-familyCompanySeatingTransitionQaArtifacts";

        private static readonly string[] MemberIds =
            { "player", "older_sister", "father", "mother" };

        private static readonly string[] DirectionTokens =
        {
            "south", "southwest", "west", "northwest",
            "north", "northeast", "east", "southeast"
        };

        private const float UpperBodyRegionAbovePelvisPx = 32f;
        private const float EntryLowerBodyRegionAbovePelvisPx = 12f;
        private const float HandProtectionRadiusPx = 7f;
        private const int ColorDifferenceThreshold = 6;
        private const float MaximumOpaqueCoreActorResidualRatio = 0.05f;
        private const float MinimumUpperBodyRetention = 0.75f;
        private const float MinimumHandRetention = 0.75f;
        private const float MaximumSeatResidualPx = 0.9f;
        private const float MaximumHandKeyboardResidualPx = 3.5f;
        private const float MaximumEgressStepPx = 0.9f;

        private static OfficeSeatingTransitionPlayerQa _instance;

        private readonly Dictionary<string, ActorTrace> _traces =
            new Dictionary<string, ActorTrace>(StringComparer.Ordinal);
        private readonly List<FrameEvidenceRecord> _frameEvidenceRecords =
            new List<FrameEvidenceRecord>();
        private readonly HashSet<string> _frameEvidenceKeys =
            new HashSet<string>(StringComparer.Ordinal);

        private StarterOfficeRuntimeBootstrap _runtime;
        private OfficeCharacterSeatPoseCatalog _poseCatalog;
        private string _artifactDirectory = string.Empty;
        private string _failure = string.Empty;
        private int _failureCode;
        private bool _sitOverviewCaptured;
        private bool _standOverviewCaptured;
        private float _previousTimeScale = 1f;
        private float _previousCaptureDeltaTime;
        private bool _timingOverrideActive;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInstall()
        {
            if (_instance != null || !HasCommandLineFlag(CommandLineFlag)) return;
            var host = new GameObject("~OfficeSeatingTransitionPlayerQa");
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<OfficeSeatingTransitionPlayerQa>();
        }

        private void Start()
        {
            _artifactDirectory = ResolveArtifactDirectory();
            StartCoroutine(RunGuarded());
        }

        private IEnumerator RunGuarded()
        {
            IEnumerator run = Run();
            while (true)
            {
                object yielded;
                try
                {
                    if (!run.MoveNext()) yield break;
                    yielded = run.Current;
                }
                catch (Exception exception)
                {
                    FinishFailure(
                        90,
                        "Unhandled " + exception.GetType().Name + ": " + exception.Message);
                    yield break;
                }
                yield return yielded;
            }
        }

        private IEnumerator Run()
        {
            _previousTimeScale = Time.timeScale;
            _previousCaptureDeltaTime = Time.captureDeltaTime;
            Time.timeScale = 1f;
            // Camera.Render/ReadPixels/PNG encoding is intentionally synchronous. A fixed capture
            // delta prevents that wall-clock cost from advancing the next presentation tick far
            // enough to skip one of the required 4/6/4 rendered sprites.
            Time.captureDeltaTime = 1f / 60f;
            _timingOverrideActive = true;
            Debug.Log(
                "FAMILY_COMPANY_SEATING_TRANSITION_QA: START | flag=" + CommandLineFlag +
                " | artifacts=" + _artifactDirectory);

            yield return null;
            PrototypeBootstrap bootstrap = Object.FindFirstObjectByType<PrototypeBootstrap>();
            if (bootstrap == null)
            {
                FinishFailure(91, "PrototypeBootstrap is missing.");
                yield break;
            }

            // Match the public player flow rather than fabricating a QA-only scene. The office
            // loader is idempotent and the dedicated runner waits for its staged runtime rebuild.
            ScenePreviewJump.ShowStarterOffice();
            bootstrap.StartNewGameNow(1, false);
            ScenePreviewJump.ShowStarterOffice();

            float readyDeadline = Time.realtimeSinceStartup + 25f;
            while (Time.realtimeSinceStartup < readyDeadline)
            {
                _runtime = Object.FindFirstObjectByType<StarterOfficeRuntimeBootstrap>();
                if (_runtime != null && _runtime.IsReady && _runtime.World != null &&
                    _runtime.Actors.Count == MemberIds.Length) break;
                yield return null;
            }

            if (_runtime == null || !_runtime.IsReady || _runtime.World == null ||
                _runtime.Actors.Count != MemberIds.Length)
            {
                FinishFailure(92, "Starter Office runtime did not become ready with four actors.");
                yield break;
            }

            OfficeTileMigrationPreviewBootstrap assetSource =
                Object.FindFirstObjectByType<OfficeTileMigrationPreviewBootstrap>();
            _poseCatalog = assetSource == null ? null : assetSource.CharacterSeatPoseCatalog;
            if (_poseCatalog == null)
            {
                FinishFailure(92, "The runtime character seat-pose catalog is missing.");
                yield break;
            }
            if (!ValidateEgressCandidateMatrix(out string egressMatrixFailure))
            {
                FinishFailure(92, "Seat egress candidate matrix failed: " + egressMatrixFailure);
                yield break;
            }

            Dictionary<string, OfficeRuntimeAgent> actors = _runtime.Actors
                .Where(actor => actor != null)
                .ToDictionary(actor => actor.AgentId, actor => actor, StringComparer.Ordinal);
            if (MemberIds.Any(memberId => !actors.ContainsKey(memberId)))
            {
                FinishFailure(92, "Canonical family actor set is incomplete.");
                yield break;
            }

            foreach (string memberId in MemberIds)
            {
                OfficeRuntimeAgent actor = actors[memberId];
                actor.BeginQaControl();
                if (!actor.QaBeginSeatedWork("seating-transition-player-qa"))
                {
                    FinishFailure(93, "Could not resolve assigned workstation route for " + memberId + ".");
                    yield break;
                }
                _traces.Add(memberId, new ActorTrace(memberId));
            }

            // PNG encoding is wall-clock heavy but does not advance the fixed presentation delta.
            // Use both a generous real-time ceiling and a deterministic sampled-frame watchdog.
            float workDeadline = Time.realtimeSinceStartup + 300f;
            const int maximumWorkPresentationFrames = 10800;
            int workPresentationFrames = 0;
            while (Time.realtimeSinceStartup < workDeadline &&
                   workPresentationFrames < maximumWorkPresentationFrames &&
                   MemberIds.Any(memberId => !ReadyForWorkEvidence(actors[memberId], _traces[memberId])))
            {
                yield return new WaitForEndOfFrame();
                workPresentationFrames++;
                if (!SampleAll(actors))
                {
                    FinishFailure(_failureCode, _failure);
                    yield break;
                }
            }

            if (MemberIds.Any(memberId => !ReadyForWorkEvidence(actors[memberId], _traces[memberId])))
            {
                FinishFailure(
                    94,
                    "SitDown/work evidence timed out: " + BuildActorSummary(actors));
                yield break;
            }

            if (!CaptureOverview("seating-transition-work-overview-1920x1080.png", out string captureFailure))
            {
                FinishFailure(95, "Work overview capture failed: " + captureFailure);
                yield break;
            }

            foreach (string memberId in MemberIds)
            {
                if (actors[memberId].QaRequestStand()) continue;
                FinishFailure(96, "Could not begin StandUp for " + memberId + ".");
                yield break;
            }

            float leaveDeadline = Time.realtimeSinceStartup + 180f;
            const int maximumLeavePresentationFrames = 5400;
            int leavePresentationFrames = 0;
            while (Time.realtimeSinceStartup < leaveDeadline &&
                   leavePresentationFrames < maximumLeavePresentationFrames &&
                   MemberIds.Any(memberId => !CompletedSeatExit(actors[memberId], _traces[memberId])))
            {
                yield return new WaitForEndOfFrame();
                leavePresentationFrames++;
                if (!SampleAll(actors))
                {
                    FinishFailure(_failureCode, _failure);
                    yield break;
                }
            }

            if (MemberIds.Any(memberId => !CompletedSeatExit(actors[memberId], _traces[memberId])))
            {
                FinishFailure(96, "StandUp/LeavingSeat evidence timed out: " + BuildActorSummary(actors));
                yield break;
            }

            yield return new WaitForEndOfFrame();
            if (!CaptureOverview(
                    "seating-transition-egress-after-overview-1920x1080.png",
                    out string egressOverviewFailure))
            {
                FinishFailure(95, "Safe-egress overview capture failed: " + egressOverviewFailure);
                yield break;
            }
            foreach (string memberId in MemberIds)
            {
                if (CaptureSafeEgressEvidence(
                        actors[memberId],
                        _traces[memberId],
                        out string egressCaptureFailure)) continue;
                FinishFailure(95, memberId + " safe-egress evidence failed: " + egressCaptureFailure);
                yield break;
            }

            foreach (string memberId in MemberIds)
            {
                if (!ValidateFinalActor(actors[memberId], _traces[memberId], out string finalFailure))
                {
                    FinishFailure(97, memberId + ": " + finalFailure);
                    yield break;
                }
            }

            const int expectedPrimaryCaptureCount = 4 * (4 + 6 + 4);
            if (_frameEvidenceRecords.Count != expectedPrimaryCaptureCount ||
                _frameEvidenceKeys.Count != expectedPrimaryCaptureCount)
            {
                FinishFailure(
                    97,
                    $"Primary closeup coverage is {_frameEvidenceRecords.Count}/" +
                    $"{_frameEvidenceKeys.Count}; expected {expectedPrimaryCaptureCount} unique frames.");
                yield break;
            }

            string result = BuildResult(actors, true, string.Empty);
            WriteResult(result);
            WriteFrameEvidenceManifest();
            Debug.Log(
                "FAMILY_COMPANY_SEATING_TRANSITION_QA: PASS | " +
                "family=4 sit=4/4 workHook=6/6 stand=4/4 directionMismatch=0 " +
                "maxOctantDelta=0 facingLocked=SitDown..LeavingSeat depth=perFrame " +
                "pelvisStep<=2px seatResidual<=0.9px handKeyboard<=3.5px " +
                "primaryCloseups=56/56 continuous=4/6/4 penetration=0 " +
                "invalidUpperForegroundOverlap=0 typingHandForegroundOverlap=0 " +
                "chairForeground=canonical-continuous upperBody=pose-split " +
                "egress=reserved-before-stand/safe-anchor/overlap0/maxStep<=0.9px " +
                "transitionHand=diagnostic captures=1920x1080+1024x1024");
            foreach (OfficeRuntimeAgent actor in actors.Values) actor.EndQaControl();
            RestoreTimingOverride();
            yield return null;
            Application.Quit(0);
        }

        private bool SampleAll(IReadOnlyDictionary<string, OfficeRuntimeAgent> actors)
        {
            foreach (string memberId in MemberIds)
            {
                if (SampleActor(actors[memberId], _traces[memberId])) continue;
                return false;
            }
            return true;
        }

        private static bool ValidateEgressCandidateMatrix(out string failure)
        {
            failure = string.Empty;
            var seatCell = new OfficeGridCoordinate(20, 20);
            var rotations = new[]
            {
                new Vector2Int(0, -1), new Vector2Int(-1, 0),
                new Vector2Int(0, 1), new Vector2Int(1, 0)
            };
            OfficeFurnitureFacing[] facings =
            {
                OfficeFurnitureFacing.NorthWest, OfficeFurnitureFacing.NorthEast,
                OfficeFurnitureFacing.SouthEast, OfficeFurnitureFacing.SouthWest
            };
            for (var memberIndex = 0; memberIndex < MemberIds.Length; memberIndex++)
            for (var rotationIndex = 0; rotationIndex < rotations.Length; rotationIndex++)
            {
                Vector2Int front = rotations[rotationIndex];
                var seat = new OfficeSeatSlot(
                    "player-qa-egress-" + memberIndex + "-" + rotationIndex,
                    "player-qa-chair",
                    "player-qa-desk",
                    seatCell,
                    new OfficeGridCoordinate(seatCell.X + front.x, seatCell.Y + front.y),
                    facings[rotationIndex]);
                for (var scenario = 0; scenario < 4; scenario++)
                {
                    bool selected = OfficeSeatEgressRules.TrySelectCandidate(
                        seat,
                        candidate => candidate.Kind switch
                        {
                            OfficeSeatEgressKind.Front => scenario < 1,
                            OfficeSeatEgressKind.Left => scenario < 2,
                            OfficeSeatEgressKind.Right => scenario < 3,
                            _ => false
                        },
                        out OfficeSeatEgressCandidate candidate);
                    OfficeSeatEgressKind expected = scenario switch
                    {
                        0 => OfficeSeatEgressKind.Front,
                        1 => OfficeSeatEgressKind.Left,
                        2 => OfficeSeatEgressKind.Right,
                        _ => OfficeSeatEgressKind.None
                    };
                    if (selected != (scenario < 3) ||
                        (selected ? candidate.Kind : OfficeSeatEgressKind.None) != expected)
                    {
                        failure = $"member={MemberIds[memberIndex]} rotation={rotationIndex * 90} " +
                                  $"scenario={scenario} selected={selected}:{candidate.Kind} " +
                                  "expected=" + expected;
                        return false;
                    }
                }
            }
            return true;
        }

        private bool SampleActor(OfficeRuntimeAgent actor, ActorTrace trace)
        {
            if (actor == null) return Fail(93, trace.MemberId + " actor was destroyed.");

            OfficeRuntimeAgentPhase phase = actor.Phase;
            if (phase == OfficeRuntimeAgentPhase.MovingToSit &&
                TryResolveClaimedSeatDirection(actor, out int movingSeatDirection) &&
                actor.ExpectedSeatDirection == movingSeatDirection &&
                actor.CurrentDirection == movingSeatDirection &&
                actor.IsSeatEntryPresentationPlanted)
                trace.SawAlignedMovingToSit = true;

            bool engaged = phase == OfficeRuntimeAgentPhase.SittingDown ||
                           phase == OfficeRuntimeAgentPhase.Working ||
                           phase == OfficeRuntimeAgentPhase.FinishingWork ||
                           phase == OfficeRuntimeAgentPhase.StandingUp ||
                           phase == OfficeRuntimeAgentPhase.LeavingSeat;
            if (trace.SawLeavingSeat && !actor.HasCompletedSeatEgress &&
                phase != OfficeRuntimeAgentPhase.LeavingSeat)
                return Fail(
                    93,
                    trace.MemberId + " entered " + phase +
                    " before the reserved safe egress anchor was completed.");
            if (!engaged) return true;

            if (!TryResolveClaimedSeatDirection(actor, out int expectedDirection))
                return Fail(93, trace.MemberId + " has no claimed seat in phase " + phase + ".");
            if (expectedDirection < 0 || expectedDirection >= DirectionTokens.Length)
                return Fail(93, trace.MemberId + " has no expected seat direction in phase " + phase + ".");
            if (actor.ExpectedSeatDirection != expectedDirection)
                return Fail(
                    93,
                    $"{trace.MemberId} runtime seat direction differs from the claimed seat: " +
                    $"runtime={actor.ExpectedSeatDirection} seat={expectedDirection}");
            if (trace.ExpectedDirection < 0) trace.ExpectedDirection = expectedDirection;
            if (trace.ExpectedDirection != expectedDirection)
                return Fail(93, trace.MemberId + " changed expected seat direction while engaged.");
            if (trace.SeatId.Length == 0) trace.SeatId = actor.ActiveSeatId;
            if (!string.Equals(trace.SeatId, actor.ActiveSeatId, StringComparison.Ordinal))
                return Fail(93, trace.MemberId + " changed seat claim during the transition.");

            trace.DirectionSampleCount++;
            trace.Phases.Add(phase);
            if (phase == OfficeRuntimeAgentPhase.FinishingWork) trace.SawFinishingWork = true;
            if ((phase == OfficeRuntimeAgentPhase.StandingUp ||
                 phase == OfficeRuntimeAgentPhase.LeavingSeat) &&
                !actor.HasSeatEgressReservation)
                return Fail(93, trace.MemberId + " lost its pre-StandUp egress reservation.");
            if (phase == OfficeRuntimeAgentPhase.StandingUp)
                trace.SawReservedBeforeStandUp = true;
            if (phase == OfficeRuntimeAgentPhase.LeavingSeat)
            {
                trace.SawLeavingSeat = true;
                trace.LeavingSeatSampleCount++;
            }

            int parsedSpriteDirection = ParseSpriteDirection(actor.CurrentSpriteName);
            if (!actor.IsOfficeSeatingFacingLocked ||
                actor.LockedOfficeSeatingDirection != expectedDirection ||
                actor.CurrentDirection != expectedDirection ||
                actor.CurrentSpriteDirection != expectedDirection ||
                parsedSpriteDirection != expectedDirection)
            {
                return Fail(
                    93,
                    $"{trace.MemberId} facing lock mismatch in {phase}: expected={expectedDirection} " +
                    $"locked={actor.IsOfficeSeatingFacingLocked}:{actor.LockedOfficeSeatingDirection} " +
                    $"current={actor.CurrentDirection} spriteDirection={actor.CurrentSpriteDirection}/" +
                    $"{parsedSpriteDirection} sprite={actor.CurrentSpriteName}");
            }

            OfficeSeatingDepthSnapshot depth = actor.LastSeatingDepthSample;
            trace.DepthSampleCount++;
            OfficeSeatingAnimationClip? clip = actor.CurrentSeatingClip;
            int frame = actor.CurrentSeatingFrame;
            bool plantedSitEntry = phase == OfficeRuntimeAgentPhase.SittingDown &&
                                   clip == OfficeSeatingAnimationClip.SitDown &&
                                   frame == 0;
            bool mustEngageForeground = !plantedSitEntry;
            if (!depth.IsValid || depth.Phase != phase || depth.Clip != actor.CurrentSeatingClip ||
                depth.Frame != actor.CurrentSeatingFrame ||
                (mustEngageForeground && !depth.OcclusionEngaged) ||
                (plantedSitEntry && depth.OcclusionEngaged) || !depth.HasChairFront ||
                !depth.HasDeskFront || !depth.IsValidStack)
            {
                return Fail(
                    93,
                    $"{trace.MemberId} invalid per-frame seating depth in {phase}: " +
                    $"valid={depth.IsValid} sample={depth.Phase}/{depth.Clip}/{depth.Frame} " +
                    $"current={phase}/{actor.CurrentSeatingClip}/{actor.CurrentSeatingFrame} " +
                    $"engaged={depth.OcclusionEngaged} " +
                    $"front={depth.HasChairFront}/{depth.HasDeskFront} stack={depth.IsValidStack} " +
                    $"orders=desk{depth.DeskBaseOrder}<chair{depth.ChairBaseOrder}<" +
                    $"actor{depth.ActorOrder}<deskFront{depth.DeskFrontOrder}<chairFront{depth.ChairFrontOrder}");
            }
            if ((mustEngageForeground && !actor.IsSeatedUpperBodyProtectionVisible) ||
                (plantedSitEntry && actor.IsSeatedUpperBodyProtectionVisible))
            {
                return Fail(
                    93,
                    $"{trace.MemberId} upper-body protection mismatch in {phase}: " +
                    $"required={mustEngageForeground} visible=" +
                    actor.IsSeatedUpperBodyProtectionVisible);
            }

            if (clip == OfficeSeatingAnimationClip.SitDown && frame >= 0 && frame < 4)
            {
                int bit = 1 << frame;
                trace.SitDownFrameMask |= bit;
                trace.DepthSitDownFrameMask |= bit;
                if ((trace.SitEvidenceFrameMask & bit) == 0)
                {
                    if (frame != trace.NextExpectedSitEvidenceFrame)
                        return Fail(
                            95,
                            $"{trace.MemberId} SitDown capture sequence skipped/reordered: " +
                            $"expected={trace.NextExpectedSitEvidenceFrame} actual={frame}");
                    if (!CaptureSeatingFrameEvidence(
                            actor,
                            trace,
                            FrameEvidenceKind.SitDown,
                            OfficeSeatingAnimationClip.SitDown,
                            frame,
                            depth,
                            out string captureFailure))
                        return Fail(95, trace.MemberId + " SitDown evidence failed: " + captureFailure);
                    trace.SitEvidenceFrameMask |= bit;
                    trace.NextExpectedSitEvidenceFrame++;
                }
                if (frame == 1) trace.SitCloseupCaptured = true;
                if (trace.MemberId == "older_sister" && frame == 1 && !_sitOverviewCaptured)
                {
                    if (!CaptureOverview(
                            "seating-transition-sitdown-mid-overview-1920x1080.png",
                            out string captureFailure))
                        return Fail(95, "SitDown overview capture failed: " + captureFailure);
                    _sitOverviewCaptured = true;
                }
            }
            else if (clip == OfficeSeatingAnimationClip.StandUp && frame >= 0 && frame < 4)
            {
                int bit = 1 << frame;
                trace.StandUpFrameMask |= bit;
                trace.DepthStandUpFrameMask |= bit;
                if ((trace.StandEvidenceFrameMask & bit) == 0)
                {
                    if (frame != trace.NextExpectedStandEvidenceFrame)
                        return Fail(
                            95,
                            $"{trace.MemberId} StandUp capture sequence skipped/reordered: " +
                            $"expected={trace.NextExpectedStandEvidenceFrame} actual={frame}");
                    if (!CaptureSeatingFrameEvidence(
                            actor,
                            trace,
                            FrameEvidenceKind.StandUp,
                            OfficeSeatingAnimationClip.StandUp,
                            frame,
                            depth,
                            out string captureFailure))
                        return Fail(95, trace.MemberId + " StandUp evidence failed: " + captureFailure);
                    trace.StandEvidenceFrameMask |= bit;
                    trace.NextExpectedStandEvidenceFrame++;
                }
                if (frame == 2) trace.StandCloseupCaptured = true;
                if (trace.MemberId == "older_sister" && frame == 2 && !_standOverviewCaptured)
                {
                    if (!CaptureOverview(
                            "seating-transition-standup-mid-overview-1920x1080.png",
                            out string captureFailure))
                        return Fail(95, "StandUp overview capture failed: " + captureFailure);
                    _standOverviewCaptured = true;
                }
            }

            if ((phase == OfficeRuntimeAgentPhase.Working ||
                  phase == OfficeRuntimeAgentPhase.FinishingWork) &&
                actor.IsOfficeWorkAnimationHookActive &&
                !string.IsNullOrWhiteSpace(actor.CurrentSpriteName))
            {
                trace.SawWorkHookActive = true;
                if (actor.CurrentOfficeWorkMicroAction == OfficeWorkMicroAction.Typing)
                {
                    if (!TryParseTypingFrameIndex(actor.CurrentSpriteName, out int typingFrame) ||
                        typingFrame < 0 || typingFrame >= 6)
                        return Fail(
                            95,
                            $"{trace.MemberId} has an unparseable typing sprite: {actor.CurrentSpriteName}");
                    int bit = 1 << typingFrame;
                    string previousSprite = trace.TypingSpriteNames[typingFrame];
                    if (!string.IsNullOrEmpty(previousSprite) &&
                        !string.Equals(previousSprite, actor.CurrentSpriteName, StringComparison.Ordinal))
                        return Fail(
                            95,
                            $"{trace.MemberId} typing frame {typingFrame} mixed sprites " +
                            $"'{previousSprite}' and '{actor.CurrentSpriteName}'.");
                    trace.TypingSpriteNames[typingFrame] = actor.CurrentSpriteName;
                    trace.WorkHookSprites.Add(actor.CurrentSpriteName);
                    trace.DepthWorkHookSprites.Add(actor.CurrentSpriteName);
                    if ((trace.TypingEvidenceFrameMask & bit) == 0)
                    {
                        if (typingFrame != trace.NextExpectedTypingEvidenceFrame)
                            return Fail(
                                95,
                                $"{trace.MemberId} Typing capture sequence skipped/reordered: " +
                                $"expected={trace.NextExpectedTypingEvidenceFrame} actual={typingFrame}");
                        if (!CaptureSeatingFrameEvidence(
                                actor,
                                trace,
                                FrameEvidenceKind.Typing,
                                OfficeSeatingAnimationClip.Work,
                                typingFrame,
                                depth,
                                out string captureFailure))
                            return Fail(95, trace.MemberId + " Typing evidence failed: " + captureFailure);
                        trace.TypingEvidenceFrameMask |= bit;
                        trace.NextExpectedTypingEvidenceFrame++;
                        trace.WorkCloseupCaptured = true;
                    }
                }
            }

            string sampleKey = phase + ":" + (clip?.ToString() ?? "none") + ":" + frame + ":" +
                               actor.CurrentSpriteName;
            if (trace.LoggedSamples.Add(sampleKey))
            {
                Debug.Log(
                    $"SEATING_TRANSITION_FRAME_QA_SAMPLE | member={trace.MemberId} phase={phase} " +
                    $"clip={(clip?.ToString() ?? "none")} frame={frame} expectedDirection={expectedDirection} " +
                    $"direction={actor.CurrentDirection} spriteDirection={actor.CurrentSpriteDirection} " +
                    $"locked={actor.IsOfficeSeatingFacingLocked}:{actor.LockedOfficeSeatingDirection} " +
                    $"sprite={actor.CurrentSpriteName} orders={depth.DeskBaseOrder}/" +
                    $"{depth.ChairBaseOrder}/{depth.ActorOrder}/{depth.DeskFrontOrder}/" +
                    $"{depth.ChairFrontOrder}");
            }
            return true;
        }

        private bool TryResolveClaimedSeatDirection(
            OfficeRuntimeAgent actor,
            out int direction)
        {
            direction = -1;
            if (actor == null || string.IsNullOrWhiteSpace(actor.ActiveSeatId) ||
                _runtime == null || _runtime.World == null) return false;
            OfficeSeatSlot seat = _runtime.World.Workstations.RequiredSeat(actor.ActiveSeatId);
            direction = seat.Facing switch
            {
                OfficeFurnitureFacing.SouthEast => 7,
                OfficeFurnitureFacing.SouthWest => 1,
                OfficeFurnitureFacing.NorthWest => 3,
                OfficeFurnitureFacing.NorthEast => 5,
                _ => -1
            };
            return direction >= 0;
        }

        private static bool ReadyForWorkEvidence(OfficeRuntimeAgent actor, ActorTrace trace)
        {
            return actor != null && actor.Phase == OfficeRuntimeAgentPhase.Working &&
                   trace.SitDownFrameMask == 0x0f && trace.DepthSitDownFrameMask == 0x0f &&
                   trace.SitEvidenceFrameMask == 0x0f &&
                   actor.IsOfficeWorkAnimationHookActive &&
                   trace.TypingEvidenceFrameMask == 0x3f &&
                   trace.WorkHookSprites.Count == 6 && trace.DepthWorkHookSprites.Count == 6;
        }

        private static bool CompletedSeatExit(OfficeRuntimeAgent actor, ActorTrace trace)
        {
            return actor != null && trace.SawLeavingSeat && !actor.IsOccupyingSeat &&
                   actor.HasCompletedSeatEgress &&
                   actor.LastCompletedSeatEgressClearanceValid &&
                   actor.Phase == OfficeRuntimeAgentPhase.Idle &&
                   trace.StandUpFrameMask == 0x0f && trace.DepthStandUpFrameMask == 0x0f &&
                   trace.StandEvidenceFrameMask == 0x0f;
        }

        private bool ValidateFinalActor(
            OfficeRuntimeAgent actor,
            ActorTrace trace,
            out string failure)
        {
            var failures = new List<string>();
            if (!actor.WasSeatFacingAlignedBeforeSitDown)
                failures.Add("seat-facing rotation was not confirmed before SitDown");
            if (trace.SitDownFrameMask != 0x0f || trace.SitEvidenceFrameMask != 0x0f ||
                actor.ObservedSitDownFrameCount != 4)
                failures.Add($"SitDown={CountBits(trace.SitDownFrameMask)}/{actor.ObservedSitDownFrameCount}");
            if (trace.SawWorkHookActive)
            {
                if (trace.WorkHookSprites.Count != 6 || trace.DepthWorkHookSprites.Count != 6 ||
                    trace.TypingEvidenceFrameMask != 0x3f)
                    failures.Add(
                        $"typingHook={trace.WorkHookSprites.Count}/" +
                        $"{trace.DepthWorkHookSprites.Count}/" +
                        $"{CountBits(trace.TypingEvidenceFrameMask)}");
            }
            else
            {
                failures.Add("Typing work hook was not sampled");
            }
            if (trace.StandUpFrameMask != 0x0f || trace.StandEvidenceFrameMask != 0x0f ||
                actor.ObservedStandUpFrameCount != 4)
                failures.Add($"StandUp={CountBits(trace.StandUpFrameMask)}/{actor.ObservedStandUpFrameCount}");
            if (!trace.SawFinishingWork) failures.Add("FinishingWork was not sampled");
            if (!trace.SawLeavingSeat || trace.LeavingSeatSampleCount == 0)
                failures.Add("LeavingSeat was not sampled");
            if (!trace.SawReservedBeforeStandUp)
                failures.Add("egress anchor was not observed reserved before StandUp");
            if (!actor.HasCompletedSeatEgress || !actor.LastCompletedSeatEgressClearanceValid)
                failures.Add("safe egress was not completed with clearance");
            if (actor.LastCompletedSeatEgressKind == OfficeSeatEgressKind.None)
                failures.Add("completed egress kind is missing");
            if (actor.MaximumSeatEgressRootStepPx > MaximumEgressStepPx)
                failures.Add($"egressStep={actor.MaximumSeatEgressRootStepPx:F3}px");
            if (actor.SeatEgressCollisionViolationCount != 0 ||
                actor.SeatEgressUnsafePhaseTransitionCount != 0)
                failures.Add(
                    $"egressViolations={actor.SeatEgressCollisionViolationCount}/" +
                    actor.SeatEgressUnsafePhaseTransitionCount);
            if (!trace.SafeEgressCloseupCaptured || trace.SafeEgressEmbeddedOverlapPixels != 0)
                failures.Add(
                    $"safeEgressCapture={trace.SafeEgressCloseupCaptured} " +
                    $"embeddedOverlap={trace.SafeEgressEmbeddedOverlapPixels}px");
            if (trace.DirectionSampleCount == 0) failures.Add("no engaged direction samples");
            if (actor.SeatingFacingViolationCount != 0)
                failures.Add("facingViolations=" + actor.SeatingFacingViolationCount);
            if (actor.SeatingSpriteDirectionMismatchCount != 0)
                failures.Add("spriteDirectionMismatches=" + actor.SeatingSpriteDirectionMismatchCount);
            if (actor.MaximumSeatingSpriteDirectionOctantDelta != 0)
                failures.Add("maxOctantDelta=" + actor.MaximumSeatingSpriteDirectionOctantDelta);
            if (actor.SeatingDepthViolationCount != 0)
                failures.Add("depthViolations=" + actor.SeatingDepthViolationCount);
            if (actor.MaxTransitionPelvisStepPx > 2f)
                failures.Add($"pelvisStep={actor.MaxTransitionPelvisStepPx:F3}px");
            if (actor.TransitionMonotonicViolationCount != 0)
                failures.Add("transitionReversals=" + actor.TransitionMonotonicViolationCount);
            if (actor.MaxAnimatedAnchorErrorPx > MaximumSeatResidualPx)
                failures.Add($"anchorError={actor.MaxAnimatedAnchorErrorPx:F3}px");
            if (actor.MaxTypingSeatContactErrorPx > MaximumSeatResidualPx)
                failures.Add($"typingSeat={actor.MaxTypingSeatContactErrorPx:F3}px");
            if (actor.MaxTypingHandWorkErrorPx > MaximumHandKeyboardResidualPx)
                failures.Add($"handKeyboard={actor.MaxTypingHandWorkErrorPx:F3}px");
            if (actor.MaxChairPresentationStepPx > MaximumSeatResidualPx)
                failures.Add($"chairPresentationStep={actor.MaxChairPresentationStepPx:F3}px");
            if (actor.VisualRotationErrorDegrees > 0.01f)
                failures.Add($"rotation={actor.VisualRotationErrorDegrees:F4}deg");
            if (actor.VisualScaleDeviation > 0.001f)
                failures.Add($"scaleDeviation={actor.VisualScaleDeviation:P3}");
            if (actor.IsOfficeSeatingFacingLocked)
                failures.Add("facing lock was not released after LeavingSeat");
            if (trace.DepthSitDownFrameMask != 0x0f || trace.DepthStandUpFrameMask != 0x0f)
                failures.Add("per-frame transition depth coverage is incomplete");
            if (!trace.SitCloseupCaptured || !trace.WorkCloseupCaptured || !trace.StandCloseupCaptured)
                failures.Add("required 1024x1024 closeup is missing");
            if (trace.EvidenceRecordCount != 14)
                failures.Add("primaryEvidence=" + trace.EvidenceRecordCount + "/14");
            if (trace.NextExpectedSitEvidenceFrame != 4 ||
                trace.NextExpectedTypingEvidenceFrame != 6 ||
                trace.NextExpectedStandEvidenceFrame != 4)
                failures.Add(
                    $"continuousCapture={trace.NextExpectedSitEvidenceFrame}/" +
                    $"{trace.NextExpectedTypingEvidenceFrame}/" +
                    $"{trace.NextExpectedStandEvidenceFrame}");
            foreach (FrameEvidenceRecord record in _frameEvidenceRecords.Where(
                         item => string.Equals(item.MemberId, trace.MemberId, StringComparison.Ordinal)))
            {
                OcclusionEvidence evidence = record.Evidence;
                string frameLabel = record.Kind + "[" + record.EvidenceFrame + "]";
                if (evidence.LowerBodyActorPixels <= 0)
                    failures.Add(frameLabel + " lower-body region has no actor pixels");
                if (evidence.LowerBodyOverlapCandidatePixels > 0 &&
                    evidence.LowerBodyOccludedPixels != evidence.LowerBodyOverlapCandidatePixels)
                    failures.Add(
                        $"{frameLabel} lowerOccluded={evidence.LowerBodyOccludedPixels}/" +
                        evidence.LowerBodyOverlapCandidatePixels);
                if (record.Kind == FrameEvidenceKind.Typing &&
                    evidence.LowerBodyOverlapCandidatePixels == 0)
                    failures.Add(frameLabel + " has no lower-body/chair foreground overlap");
                if (evidence.ForegroundPenetrationPixels != 0)
                    failures.Add(frameLabel + " penetration=" + evidence.ForegroundPenetrationPixels);
                if (evidence.UpperBodyActorPixels <= 0 ||
                    evidence.UpperBodyRetention < MinimumUpperBodyRetention ||
                    evidence.UpperBodyInvalidForegroundOverlapPixels != 0)
                    failures.Add(
                        $"{frameLabel} upper={evidence.UpperBodyVisiblePixels}/" +
                        $"{evidence.UpperBodyActorPixels} retention={evidence.UpperBodyRetention:F3} " +
                        $"invalidOverlap={evidence.UpperBodyInvalidForegroundOverlapPixels}");
                // Only Work profiles define a hand-to-keyboard contract. Sit/Stand serialize the
                // same field for pose continuity, but it can point at a transparent pixel or a
                // naturally chair-hidden near edge and is therefore diagnostic rather than a hand
                // segmentation ground truth.
                if (record.Kind == FrameEvidenceKind.Typing &&
                    (evidence.HandActorPixels <= 0 || evidence.HandVisiblePixels <= 0 ||
                     evidence.HandRetention < MinimumHandRetention ||
                     evidence.HandInvalidForegroundOverlapPixels != 0))
                    failures.Add(
                        $"{frameLabel} hand={evidence.HandVisiblePixels}/{evidence.HandActorPixels} " +
                        $"retention={evidence.HandRetention:F3} " +
                        $"invalidOverlap={evidence.HandInvalidForegroundOverlapPixels}");
                if (record.Kind == FrameEvidenceKind.Typing &&
                    (evidence.PelvisSeatErrorPx > MaximumSeatResidualPx ||
                     evidence.HandWorkErrorPx > MaximumHandKeyboardResidualPx))
                    failures.Add(
                        $"{frameLabel} sockets={evidence.PelvisSeatErrorPx:F3}/" +
                        $"{evidence.HandWorkErrorPx:F3}px");
            }
            if (trace.SitLowerBodyOccludedPixels <= 0 ||
                trace.TypingLowerBodyOccludedPixels <= 0 ||
                trace.StandLowerBodyOccludedPixels <= 0)
                failures.Add(
                    $"phaseLowerOcclusion={trace.SitLowerBodyOccludedPixels}/" +
                    $"{trace.TypingLowerBodyOccludedPixels}/" +
                    $"{trace.StandLowerBodyOccludedPixels}");

            failure = string.Join("; ", failures);
            return failures.Count == 0;
        }

        private bool CaptureOverview(string fileName, out string failure)
        {
            string path = ArtifactPath(fileName);
            bool captured = TryCaptureFrame(
                path,
                1920,
                1080,
                null,
                null,
                out CapturedFrame ignored,
                out failure);
            if (captured)
                Debug.Log("SEATING_TRANSITION_OVERVIEW_CAPTURE | resolution=1920x1080 path=" + path);
            return captured;
        }

        private bool CaptureSafeEgressEvidence(
            OfficeRuntimeAgent actor,
            ActorTrace trace,
            out string failure)
        {
            failure = string.Empty;
            if (actor == null || actor.PresentationRenderer == null ||
                actor.Phase != OfficeRuntimeAgentPhase.Idle || actor.IsOccupyingSeat ||
                !actor.HasCompletedSeatEgress || !actor.LastCompletedSeatEgressClearanceValid)
            {
                failure = "actor is not stationary at a completed safe egress anchor";
                return false;
            }
            if (trace == null || trace.SeatId.Length == 0)
            {
                failure = "captured seat id is missing";
                return false;
            }

            OfficeSeatSlot seat = _runtime.World.Workstations.RequiredSeat(trace.SeatId);
            if (!_runtime.World.FurniturePresenter.TryGetSemanticRoot(
                    seat.ChairFurnitureId,
                    out Transform chairRoot) || chairRoot == null)
            {
                failure = "chair semantic root is missing";
                return false;
            }
            SpriteRenderer[] chairRenderers = chairRoot
                .GetComponentsInChildren<SpriteRenderer>(true)
                .Where(renderer => renderer != null)
                .ToArray();
            if (chairRenderers.Length == 0)
            {
                failure = "chair renderers are missing";
                return false;
            }

            SpriteRenderer actorRenderer = actor.PresentationRenderer;
            SpriteRenderer upperBodyRenderer = actor.SeatedUpperBodyProtectionRenderer;
            bool actorEnabled = actorRenderer.enabled;
            bool upperBodyEnabled = upperBodyRenderer != null && upperBodyRenderer.enabled;
            bool[] chairEnabled = chairRenderers.Select(renderer => renderer.enabled).ToArray();
            Bounds actorBounds = actorRenderer.bounds;
            Bounds framing = actorBounds;
            foreach (SpriteRenderer renderer in chairRenderers)
                if (renderer.enabled && renderer.gameObject.activeInHierarchy)
                    framing.Encapsulate(renderer.bounds);
            Vector3 chairAnchor = _runtime.World.Workstations.ChairSeatAnchorWorld(seat);
            Vector3[] evidencePoints = { actor.transform.position, chairAnchor };
            string path = ArtifactPath(
                "seating-transition-egress-after-" + trace.MemberId + "-1024x1024.png");
            try
            {
                if (!TryCaptureFrame(
                        path, 1024, 1024, framing, actorBounds, evidencePoints,
                        out CapturedFrame normal, out failure)) return false;

                for (var index = 0; index < chairRenderers.Length; index++)
                    chairRenderers[index].enabled = false;
                if (!TryCaptureFrame(
                        string.Empty, 1024, 1024, framing, actorBounds, evidencePoints,
                        out CapturedFrame actorOnly, out failure)) return false;

                for (var index = 0; index < chairRenderers.Length; index++)
                    chairRenderers[index].enabled = chairEnabled[index];
                actorRenderer.enabled = false;
                if (upperBodyRenderer != null) upperBodyRenderer.enabled = false;
                if (!TryCaptureFrame(
                        string.Empty, 1024, 1024, framing, actorBounds, evidencePoints,
                        out CapturedFrame chairOnly, out failure)) return false;

                for (var index = 0; index < chairRenderers.Length; index++)
                    chairRenderers[index].enabled = false;
                if (!TryCaptureFrame(
                        string.Empty, 1024, 1024, framing, actorBounds, evidencePoints,
                        out CapturedFrame background, out failure)) return false;

                if (!TryMeasureSafeEgressOverlap(
                        normal,
                        actorOnly,
                        chairOnly,
                        background,
                        out int actorPixels,
                        out int chairPixels,
                        out int embeddedOverlapPixels,
                        out failure)) return false;
                trace.SafeEgressCloseupCaptured = true;
                trace.SafeEgressActorPixels = actorPixels;
                trace.SafeEgressChairPixels = chairPixels;
                trace.SafeEgressEmbeddedOverlapPixels = embeddedOverlapPixels;
                if (embeddedOverlapPixels != 0)
                {
                    failure = "stationary lower-body/chair-center overlap=" +
                              embeddedOverlapPixels + "px";
                    return false;
                }
                Debug.Log(
                    "SEATING_TRANSITION_SAFE_EGRESS_EVIDENCE | member=" + trace.MemberId +
                    " kind=" + actor.LastCompletedSeatEgressKind +
                    " cell=" + actor.LastCompletedSeatEgressCell +
                    " maxStepPx=" + actor.MaximumSeatEgressRootStepPx.ToString("F3") +
                    " actorPixels=" + actorPixels + " chairPixels=" + chairPixels +
                    " embeddedOverlapPixels=0 path=" + path);
                return true;
            }
            finally
            {
                actorRenderer.enabled = actorEnabled;
                if (upperBodyRenderer != null) upperBodyRenderer.enabled = upperBodyEnabled;
                for (var index = 0; index < chairRenderers.Length; index++)
                    chairRenderers[index].enabled = chairEnabled[index];
            }
        }

        private static bool TryMeasureSafeEgressOverlap(
            CapturedFrame normal,
            CapturedFrame actorOnly,
            CapturedFrame chairOnly,
            CapturedFrame background,
            out int actorPixels,
            out int chairPixels,
            out int embeddedOverlapPixels,
            out string failure)
        {
            actorPixels = 0;
            chairPixels = 0;
            embeddedOverlapPixels = 0;
            failure = string.Empty;
            if (!HaveMatchingPixels(normal, actorOnly, chairOnly, background) ||
                normal.EvidencePixels.Length < 2)
            {
                failure = "safe-egress four-way frames or projected anchors are inconsistent";
                return false;
            }

            RectInt actorRect = normal.FocusRect;
            int lowerBodyTop = actorRect.yMin + Mathf.CeilToInt(actorRect.height * 0.58f);
            Vector2 chairCenter = normal.EvidencePixels[1];
            float chairCoreRadius = Mathf.Max(8f, actorRect.width * 0.22f);
            float chairCoreRadiusSquared = chairCoreRadius * chairCoreRadius;
            for (var y = 0; y < normal.Height; y++)
            for (var x = 0; x < normal.Width; x++)
            {
                int index = y * normal.Width + x;
                bool actorContributes = PixelsDiffer(actorOnly.Pixels[index], background.Pixels[index]);
                bool chairContributes = PixelsDiffer(chairOnly.Pixels[index], background.Pixels[index]);
                if (actorContributes) actorPixels++;
                if (chairContributes) chairPixels++;
                if (!actorContributes || !chairContributes || !actorRect.Contains(new Vector2Int(x, y)) ||
                    y > lowerBodyTop) continue;
                float dx = x - chairCenter.x;
                float dy = y - chairCenter.y;
                if (dx * dx + dy * dy <= chairCoreRadiusSquared) embeddedOverlapPixels++;
            }
            if (actorPixels <= 0 || chairPixels <= 0)
            {
                failure = $"non-vacuous silhouettes missing actor={actorPixels} chair={chairPixels}";
                return false;
            }
            return true;
        }

        private bool CaptureSeatingFrameEvidence(
            OfficeRuntimeAgent actor,
            ActorTrace trace,
            FrameEvidenceKind kind,
            OfficeSeatingAnimationClip clip,
            int evidenceFrame,
            OfficeSeatingDepthSnapshot depth,
            out string failure)
        {
            failure = string.Empty;
            string evidenceKey = trace.MemberId + ":" + kind + ":" + evidenceFrame;
            if (_frameEvidenceKeys.Contains(evidenceKey))
            {
                failure = "duplicate primary evidence key " + evidenceKey;
                return false;
            }
            OfficeSeatSlot seat = _runtime.World.Workstations.RequiredSeat(actor.ActiveSeatId);
            if (!_runtime.World.FurniturePresenter.FrontOverlayRenderers.TryGetValue(
                    seat.ChairFurnitureId,
                    out SpriteRenderer overlay) || overlay == null || !overlay.enabled)
            {
                failure = "required chair foreground overlay is missing or disabled";
                return false;
            }
            SpriteRenderer actorRenderer = actor.PresentationRenderer;
            if (actorRenderer == null || actorRenderer.sprite == null || !actorRenderer.enabled)
            {
                failure = "actor presentation renderer is missing or disabled";
                return false;
            }

            int direction = trace.ExpectedDirection >= 0
                ? trace.ExpectedDirection
                : actor.ExpectedSeatDirection;
            int poseFrame = clip == OfficeSeatingAnimationClip.Work ? 0 : evidenceFrame;
            OfficeCharacterSeatPoseProfile pose;
            try
            {
                // Typing micro-actions are registered to the planted Work[0] body pose. Transition
                // frames use their own approved pose. Neither route needs member-specific offsets.
                pose = _poseCatalog.ResolveApproved(
                    actor.AgentId,
                    direction,
                    clip,
                    poseFrame);
            }
            catch (Exception exception)
            {
                failure = $"could not resolve the approved {clip}[{poseFrame}] pose: " +
                          exception.Message;
                return false;
            }

            Bounds framing = WorkstationBounds(actor);
            Bounds actorBounds = actorRenderer.bounds;
            Vector2 pelvisAnchorPx = pose.PelvisAnchorPx;
            Vector2 handAnchorPx = pose.HandAnchorPx;
            int upperBodyCutoffPx = OfficeSeatedUpperBodyProtectionRules.ResolveCutoffSourceY(
                actorRenderer.sprite,
                pelvisAnchorPx);
            float lowerBodyTopY = actor.IsSeatForegroundOcclusionEngaged
                ? Mathf.Max(1f, upperBodyCutoffPx - 1f)
                : pelvisAnchorPx.y + EntryLowerBodyRegionAbovePelvisPx;
            float protectedUpperY = Mathf.Max(
                pelvisAnchorPx.y + UpperBodyRegionAbovePelvisPx,
                handAnchorPx.y);
            Vector3[] evidenceWorldPoints =
            {
                OfficeGridAlignmentMetrics.SpriteAnchorWorld(actorRenderer, pelvisAnchorPx),
                _runtime.World.Workstations.ChairSeatAnchorWorld(seat),
                OfficeGridAlignmentMetrics.SpriteAnchorWorld(actorRenderer, handAnchorPx),
                _runtime.World.Workstations.DeskWorkSocketWorld(seat),
                OfficeGridAlignmentMetrics.SpriteAnchorWorld(
                    actorRenderer,
                    new Vector2(pelvisAnchorPx.x, lowerBodyTopY)),
                OfficeGridAlignmentMetrics.SpriteAnchorWorld(
                    actorRenderer,
                    new Vector2(pelvisAnchorPx.x, protectedUpperY)),
                OfficeGridAlignmentMetrics.SpriteAnchorWorld(
                    actorRenderer,
                    handAnchorPx + Vector2.right * HandProtectionRadiusPx),
                OfficeGridAlignmentMetrics.SpriteAnchorWorld(
                    actorRenderer,
                    handAnchorPx + Vector2.up * HandProtectionRadiusPx)
            };
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                failure = "Camera.main is missing while resolving 1920x1080 socket errors";
                return false;
            }
            float pelvisSeatErrorPx = OfficeGridAlignmentMetrics.ScreenDistance(
                mainCamera,
                evidenceWorldPoints[0],
                evidenceWorldPoints[1]);
            float handWorkErrorPx = OfficeGridAlignmentMetrics.ScreenDistance(
                mainCamera,
                evidenceWorldPoints[2],
                evidenceWorldPoints[3]);
            string stem = trace.MemberId.Replace('_', '-');
            string phaseToken = kind switch
            {
                FrameEvidenceKind.SitDown => "sitdown",
                FrameEvidenceKind.Typing => "typing-work-hook",
                FrameEvidenceKind.StandUp => "standup",
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
            string midToken = kind == FrameEvidenceKind.SitDown && evidenceFrame == 1 ||
                              kind == FrameEvidenceKind.StandUp && evidenceFrame == 2
                ? "-mid"
                : string.Empty;
            string spriteToken = SanitizeFileToken(actor.CurrentSpriteName);
            string fileName =
                $"{stem}-{phaseToken}-frame-{evidenceFrame:D2}{midToken}-{spriteToken}-" +
                "closeup-1024x1024.png";
            string onPath = ArtifactPath(fileName);
            if (!TryCaptureFrame(
                    onPath,
                    1024,
                    1024,
                    framing,
                    actorBounds,
                    evidenceWorldPoints,
                    out CapturedFrame overlayOn,
                    out failure)) return false;

            bool previousEnabled = overlay.enabled;
            bool previousActorEnabled = actorRenderer.enabled;
            SpriteRenderer upperBodyRenderer = actor.SeatedUpperBodyProtectionRenderer;
            bool previousUpperBodyEnabled =
                upperBodyRenderer != null && upperBodyRenderer.enabled;
            CapturedFrame overlayOff = default;
            CapturedFrame actorHiddenOverlayOff = default;
            CapturedFrame actorHiddenOverlayOn = default;
            try
            {
                overlay.enabled = false;
                if (!TryCaptureFrame(
                        string.Empty,
                        1024,
                        1024,
                        framing,
                        actorBounds,
                        out overlayOff,
                        out failure)) return false;

                actorRenderer.enabled = false;
                if (upperBodyRenderer != null) upperBodyRenderer.enabled = false;
                if (!TryCaptureFrame(
                        string.Empty,
                        1024,
                        1024,
                        framing,
                        actorBounds,
                        out actorHiddenOverlayOff,
                        out failure)) return false;

                overlay.enabled = true;
                if (!TryCaptureFrame(
                        string.Empty,
                        1024,
                        1024,
                        framing,
                        actorBounds,
                        out actorHiddenOverlayOn,
                        out failure)) return false;
            }
            finally
            {
                overlay.enabled = previousEnabled;
                actorRenderer.enabled = previousActorEnabled;
                if (upperBodyRenderer != null)
                    upperBodyRenderer.enabled = previousUpperBodyEnabled;
            }

            if (!TryMeasureOcclusionEvidence(
                    overlayOn,
                    overlayOff,
                    actorHiddenOverlayOn,
                    actorHiddenOverlayOff,
                    pelvisSeatErrorPx,
                    handWorkErrorPx,
                    out OcclusionEvidence evidence,
                    out failure)) return false;

            _frameEvidenceKeys.Add(evidenceKey);
            var record = new FrameEvidenceRecord(
                evidenceKey,
                trace.MemberId,
                kind,
                evidenceFrame,
                actor.CurrentSpriteName,
                actor.Phase,
                clip,
                actor.CurrentSeatingFrame,
                onPath,
                depth,
                evidence);
            _frameEvidenceRecords.Add(record);
            trace.RecordEvidence(kind, evidence);
            Debug.Log(
                $"SEATING_TRANSITION_OCCLUSION_EVIDENCE | member={trace.MemberId} " +
                $"kind={kind} evidenceFrame={evidenceFrame} sprite={actor.CurrentSpriteName} " +
                $"actorRegionChangedPixels={evidence.OverlayChangedPixels} " +
                $"lowerCandidates={evidence.LowerBodyOverlapCandidatePixels} " +
                $"lowerOccluded={evidence.LowerBodyOccludedPixels} " +
                $"penetration={evidence.ForegroundPenetrationPixels} " +
                $"filteredEdgeResidual={evidence.FilteredEdgeResidualPixels} " +
                $"upperInvalidOverlap={evidence.UpperBodyInvalidForegroundOverlapPixels} " +
                $"upperVisible={evidence.UpperBodyVisiblePixels}/{evidence.UpperBodyActorPixels} " +
                $"upperRetention={evidence.UpperBodyRetention:F3} " +
                $"handVisible={evidence.HandVisiblePixels}/{evidence.HandActorPixels} " +
                $"handInvalidOverlap={evidence.HandInvalidForegroundOverlapPixels} " +
                $"handRetention={evidence.HandRetention:F3} " +
                $"pelvisSeatPx={evidence.PelvisSeatErrorPx:F3} " +
                $"handWorkPx={evidence.HandWorkErrorPx:F3} noOverlapExpected=" +
                $"{evidence.NoLowerBodyOverlapExpected} primary={onPath}");
            return true;
        }

        private static string SanitizeFileToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "unnamed-sprite";
            char[] invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(value.Length);
            foreach (char current in value.Trim())
            {
                builder.Append(invalid.Contains(current) || char.IsWhiteSpace(current)
                    ? '-'
                    : char.ToLowerInvariant(current));
            }
            return builder.ToString().Trim('-');
        }

        private Bounds WorkstationBounds(OfficeRuntimeAgent actor)
        {
            if (actor == null || actor.PresentationRenderer == null)
                throw new InvalidOperationException("Actor presentation renderer is missing.");
            OfficeSeatSlot seat = _runtime.World.Workstations.RequiredSeat(actor.ActiveSeatId);
            Bounds bounds = actor.PresentationRenderer.bounds;
            EncapsulateFurniture(seat.ChairFurnitureId, ref bounds);
            if (seat.HasWorkstationBinding) EncapsulateFurniture(seat.WorkSurfaceFurnitureId, ref bounds);
            return bounds;
        }

        private void EncapsulateFurniture(string furnitureId, ref Bounds bounds)
        {
            if (!_runtime.World.FurniturePresenter.TryGetSemanticRoot(furnitureId, out Transform root)) return;
            foreach (SpriteRenderer renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy)
                    bounds.Encapsulate(renderer.bounds);
            }
        }

        private static bool TryCaptureFrame(
            string path,
            int width,
            int height,
            Bounds? framingBounds,
            Bounds? focusBounds,
            out CapturedFrame frame,
            out string failure)
        {
            return TryCaptureFrame(
                path,
                width,
                height,
                framingBounds,
                focusBounds,
                null,
                out frame,
                out failure);
        }

        private static bool TryCaptureFrame(
            string path,
            int width,
            int height,
            Bounds? framingBounds,
            Bounds? focusBounds,
            IReadOnlyList<Vector3> evidenceWorldPoints,
            out CapturedFrame frame,
            out string failure)
        {
            frame = default;
            failure = string.Empty;
            Camera source = Camera.main;
            if (source == null)
            {
                failure = "Camera.main is missing";
                return false;
            }

            RenderTexture previousActive = RenderTexture.active;
            var target = new RenderTexture(
                width,
                height,
                24,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            GameObject captureObject = null;
            try
            {
                captureObject = new GameObject("SeatingTransitionQaCaptureCamera")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                Camera camera = captureObject.AddComponent<Camera>();
                camera.CopyFrom(source);
                camera.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                camera.aspect = width / (float)height;
                if (framingBounds.HasValue)
                {
                    Bounds bounds = framingBounds.Value;
                    CenterCameraOnWorldPoint(camera, bounds.center);
                    camera.orthographicSize = Mathf.Max(
                        1.1f,
                        Mathf.Max(bounds.extents.y * 1.18f, bounds.extents.x * 1.18f / camera.aspect));
                }
                camera.enabled = false;
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                pixels.Apply(false, false);

                if (!string.IsNullOrWhiteSpace(path))
                {
                    string directory = Path.GetDirectoryName(path);
                    if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
                    File.WriteAllBytes(path, pixels.EncodeToPNG());
                    if (!File.Exists(path) || new FileInfo(path).Length <= 1024L)
                    {
                        failure = "capture file is missing or too small";
                        return false;
                    }
                }

                Color32[] colors = pixels.GetPixels32();
                if (IsVisuallyBlank(colors))
                {
                    failure = "capture is visually blank";
                    return false;
                }
                RectInt focusRect = focusBounds.HasValue
                    ? WorldBoundsToPixelRect(camera, focusBounds.Value, width, height)
                    : new RectInt(0, 0, width, height);
                Vector2[] evidencePixels = ProjectWorldPoints(
                    camera,
                    evidenceWorldPoints,
                    width,
                    height);
                frame = new CapturedFrame(width, height, colors, focusRect, evidencePixels);
                return true;
            }
            catch (Exception exception)
            {
                failure = exception.GetType().Name + ": " + exception.Message;
                return false;
            }
            finally
            {
                RenderTexture.active = previousActive;
                target.Release();
                if (captureObject != null) Object.Destroy(captureObject);
                Object.Destroy(target);
                Object.Destroy(pixels);
            }
        }

        private static Vector2[] ProjectWorldPoints(
            Camera camera,
            IReadOnlyList<Vector3> worldPoints,
            int width,
            int height)
        {
            if (worldPoints == null || worldPoints.Count == 0) return Array.Empty<Vector2>();
            var result = new Vector2[worldPoints.Count];
            for (var index = 0; index < worldPoints.Count; index++)
            {
                Vector3 viewport = camera.WorldToViewportPoint(worldPoints[index]);
                result[index] = new Vector2(viewport.x * width, viewport.y * height);
            }
            return result;
        }

        private static void CenterCameraOnWorldPoint(Camera camera, Vector3 target)
        {
            float depth = Vector3.Dot(target - camera.transform.position, camera.transform.forward);
            if (depth <= camera.nearClipPlane) depth = Mathf.Max(1f, camera.farClipPlane * 0.01f);
            Vector3 currentCenter = camera.transform.position + camera.transform.forward * depth;
            camera.transform.position += target - currentCenter;
        }

        private static RectInt WorldBoundsToPixelRect(
            Camera camera,
            Bounds bounds,
            int width,
            int height)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            float minX = 1f, minY = 1f, maxX = 0f, maxY = 0f;
            for (var mask = 0; mask < 8; mask++)
            {
                var corner = new Vector3(
                    (mask & 1) == 0 ? min.x : max.x,
                    (mask & 2) == 0 ? min.y : max.y,
                    (mask & 4) == 0 ? min.z : max.z);
                Vector3 viewport = camera.WorldToViewportPoint(corner);
                minX = Mathf.Min(minX, viewport.x);
                minY = Mathf.Min(minY, viewport.y);
                maxX = Mathf.Max(maxX, viewport.x);
                maxY = Mathf.Max(maxY, viewport.y);
            }
            int xMin = Mathf.Clamp(Mathf.FloorToInt(minX * width), 0, width - 1);
            int yMin = Mathf.Clamp(Mathf.FloorToInt(minY * height), 0, height - 1);
            int xMax = Mathf.Clamp(Mathf.CeilToInt(maxX * width), xMin + 1, width);
            int yMax = Mathf.Clamp(Mathf.CeilToInt(maxY * height), yMin + 1, height);
            return new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        private static int CountChangedPixels(
            CapturedFrame first,
            CapturedFrame second,
            RectInt region)
        {
            if (first.Width != second.Width || first.Height != second.Height ||
                first.Pixels == null || second.Pixels == null ||
                first.Pixels.Length != second.Pixels.Length) return 0;
            int changed = 0;
            int xMax = Mathf.Min(first.Width, region.xMax);
            int yMax = Mathf.Min(first.Height, region.yMax);
            for (int y = Mathf.Max(0, region.yMin); y < yMax; y++)
            for (int x = Mathf.Max(0, region.xMin); x < xMax; x++)
            {
                int index = y * first.Width + x;
                Color32 a = first.Pixels[index];
                Color32 b = second.Pixels[index];
                if (PixelsDiffer(a, b))
                    changed++;
            }
            return changed;
        }

        private static bool TryMeasureOcclusionEvidence(
            CapturedFrame overlayOn,
            CapturedFrame overlayOff,
            CapturedFrame actorHiddenOverlayOn,
            CapturedFrame actorHiddenOverlayOff,
            float pelvisSeatErrorPx,
            float handWorkErrorPx,
            out OcclusionEvidence evidence,
            out string failure)
        {
            evidence = default;
            failure = string.Empty;
            if (!HaveMatchingPixels(
                    overlayOn,
                    overlayOff,
                    actorHiddenOverlayOn,
                    actorHiddenOverlayOff))
            {
                failure = "occlusion evidence captures have different pixel dimensions";
                return false;
            }

            // pelvis, chair-seat, hand, desk-work, lower-boundary, upper-boundary,
            // hand-radius-x and hand-radius-y are projected by the same closeup camera.
            if (overlayOn.EvidencePixels == null || overlayOn.EvidencePixels.Length != 8)
            {
                failure = "occlusion evidence anchor projection is incomplete";
                return false;
            }

            Vector2 pelvis = overlayOn.EvidencePixels[0];
            Vector2 chairSeat = overlayOn.EvidencePixels[1];
            Vector2 hand = overlayOn.EvidencePixels[2];
            Vector2 deskWork = overlayOn.EvidencePixels[3];
            Vector2 lowerBoundary = overlayOn.EvidencePixels[4];
            Vector2 upperBoundary = overlayOn.EvidencePixels[5];
            Vector2 handRadiusX = overlayOn.EvidencePixels[6];
            Vector2 handRadiusY = overlayOn.EvidencePixels[7];
            if (!IsFinite(pelvis) || !IsFinite(chairSeat) || !IsFinite(hand) ||
                !IsFinite(deskWork) || !IsFinite(lowerBoundary) || !IsFinite(upperBoundary) ||
                !IsFinite(handRadiusX) || !IsFinite(handRadiusY))
            {
                failure = "occlusion evidence contains a non-finite projected anchor";
                return false;
            }

            Vector2 spriteUp = upperBoundary - pelvis;
            if (spriteUp.sqrMagnitude <= 0.0001f)
            {
                failure = "occlusion evidence cannot resolve the rendered sprite-up axis";
                return false;
            }
            spriteUp.Normalize();
            float lowerBoundaryDistance = Vector2.Dot(lowerBoundary - pelvis, spriteUp);
            float upperBoundaryDistance = Vector2.Dot(upperBoundary - pelvis, spriteUp);
            float handRadius = Mathf.Max(
                Vector2.Distance(hand, handRadiusX),
                Vector2.Distance(hand, handRadiusY));
            if (upperBoundaryDistance - lowerBoundaryDistance < 0.5f ||
                handRadius < 0.5f)
            {
                failure = "occlusion evidence regions collapsed after projection";
                return false;
            }

            RectInt region = overlayOn.FocusRect;
            int xMin = Mathf.Max(0, region.xMin);
            int yMin = Mathf.Max(0, region.yMin);
            int xMax = Mathf.Min(overlayOn.Width, region.xMax);
            int yMax = Mathf.Min(overlayOn.Height, region.yMax);
            int lowerBodyActorPixels = 0;
            int lowerBodyOverlapCandidatePixels = 0;
            int lowerBodyOccludedPixels = 0;
            int foregroundOverlapCandidatePixels = 0;
            int foregroundPenetrationPixels = 0;
            int filteredEdgeResidualPixels = 0;
            int upperBodyActorPixels = 0;
            int upperBodyVisiblePixels = 0;
            int upperBodyInvalidForegroundOverlapPixels = 0;
            int handActorPixels = 0;
            int handVisiblePixels = 0;
            int handInvalidForegroundOverlapPixels = 0;
            float handRadiusSquared = handRadius * handRadius;
            for (int y = yMin; y < yMax; y++)
            for (int x = xMin; x < xMax; x++)
            {
                int index = y * overlayOn.Width + x;
                int actorDeltaWithoutForeground = PixelDifference(
                    overlayOff.Pixels[index],
                    actorHiddenOverlayOff.Pixels[index]);
                int actorDeltaWithForeground = PixelDifference(
                    overlayOn.Pixels[index],
                    actorHiddenOverlayOn.Pixels[index]);
                bool actorWithoutForeground =
                    actorDeltaWithoutForeground >= ColorDifferenceThreshold;
                bool actorWithForeground =
                    actorDeltaWithForeground >= ColorDifferenceThreshold;
                // The chair foreground is an exact RGBA subset of the chair base. With the actor
                // hidden, toggling that duplicate layer is intentionally pixel-identical (C == D),
                // so C-D cannot reveal its mask. A-B is the runtime-visible foreground effect on
                // the actor; C and D remain the matching backgrounds needed to isolate actor
                // contribution before and after that effect.
                bool foregroundAffectsActor = PixelsDiffer(
                    overlayOn.Pixels[index],
                    overlayOff.Pixels[index]);
                bool filteredForegroundCore = foregroundAffectsActor && IsFilteredForegroundCore(
                    x,
                    y,
                    overlayOn,
                    overlayOff);
                bool foregroundOverlap = actorWithoutForeground && foregroundAffectsActor;
                bool coreOverlap = actorWithoutForeground && filteredForegroundCore;
                float actorResidualRatio = actorDeltaWithoutForeground <= 0
                    ? 0f
                    : actorDeltaWithForeground / (float)actorDeltaWithoutForeground;
                bool foregroundPenetration = coreOverlap &&
                                             actorResidualRatio >
                                             MaximumOpaqueCoreActorResidualRatio;
                bool actorActuallyOccluded = coreOverlap && !foregroundPenetration;
                if (coreOverlap)
                    foregroundOverlapCandidatePixels++;
                if (foregroundPenetration) foregroundPenetrationPixels++;
                if (foregroundOverlap && !filteredForegroundCore) filteredEdgeResidualPixels++;
                var pixel = new Vector2(x + 0.5f, y + 0.5f);
                float fromPelvisAlongSpriteUp = Vector2.Dot(pixel - pelvis, spriteUp);
                if (fromPelvisAlongSpriteUp <= lowerBoundaryDistance)
                {
                    if (actorWithoutForeground) lowerBodyActorPixels++;
                    if (coreOverlap)
                        lowerBodyOverlapCandidatePixels++;
                    if (actorActuallyOccluded) lowerBodyOccludedPixels++;
                }
                if (fromPelvisAlongSpriteUp >= upperBoundaryDistance)
                {
                    if (actorWithoutForeground) upperBodyActorPixels++;
                    if (actorWithForeground) upperBodyVisiblePixels++;
                    if (foregroundOverlap) upperBodyInvalidForegroundOverlapPixels++;
                }
                if ((pixel - hand).sqrMagnitude <= handRadiusSquared)
                {
                    if (actorWithoutForeground) handActorPixels++;
                    if (actorWithForeground) handVisiblePixels++;
                    if (foregroundOverlap) handInvalidForegroundOverlapPixels++;
                }
            }

            int overlayChangedPixels = CountChangedPixels(
                overlayOn,
                overlayOff,
                overlayOn.FocusRect);
            float upperBodyRetention = upperBodyActorPixels <= 0
                ? 0f
                : upperBodyVisiblePixels / (float)upperBodyActorPixels;
            float handRetention = handActorPixels <= 0
                ? 0f
                : handVisiblePixels / (float)handActorPixels;
            evidence = new OcclusionEvidence(
                overlayChangedPixels,
                lowerBodyActorPixels,
                lowerBodyOverlapCandidatePixels,
                lowerBodyOccludedPixels,
                foregroundOverlapCandidatePixels,
                foregroundPenetrationPixels,
                filteredEdgeResidualPixels,
                upperBodyActorPixels,
                upperBodyVisiblePixels,
                upperBodyInvalidForegroundOverlapPixels,
                upperBodyRetention,
                handActorPixels,
                handVisiblePixels,
                handInvalidForegroundOverlapPixels,
                handRetention,
                pelvisSeatErrorPx,
                handWorkErrorPx);

            // Policy failures are evaluated after all 56 primary frames have been captured. This
            // keeps a failed run fail-closed while still producing a complete per-frame diagnostic
            // manifest instead of stopping at the first bad pixel.
            return true;
        }

        private static bool HaveMatchingPixels(params CapturedFrame[] frames)
        {
            if (frames == null || frames.Length == 0) return false;
            CapturedFrame first = frames[0];
            if (first.Pixels == null || first.Pixels.Length != first.Width * first.Height) return false;
            for (var index = 1; index < frames.Length; index++)
            {
                CapturedFrame current = frames[index];
                if (current.Width != first.Width || current.Height != first.Height ||
                    current.Pixels == null || current.Pixels.Length != first.Pixels.Length)
                    return false;
            }
            return true;
        }

        private static bool PixelsDiffer(Color32 first, Color32 second)
        {
            return PixelDifference(first, second) >= ColorDifferenceThreshold;
        }

        private static int PixelDifference(Color32 first, Color32 second)
        {
            return Math.Abs(first.r - second.r) +
                   Math.Abs(first.g - second.g) +
                   Math.Abs(first.b - second.b);
        }

        private static bool IsFilteredForegroundCore(
            int x,
            int y,
            CapturedFrame overlayOn,
            CapturedFrame overlayOff)
        {
            // One-pixel erosion removes the outer bilinear-filtered silhouette. Opacity is not
            // assumed from source metadata: the separate actor residual measurement below proves
            // whether each remaining runtime core candidate overwrites at least 95%.
            for (int offsetY = -1; offsetY <= 1; offsetY++)
            for (int offsetX = -1; offsetX <= 1; offsetX++)
            {
                int sampleX = x + offsetX;
                int sampleY = y + offsetY;
                if (sampleX < 0 || sampleY < 0 ||
                    sampleX >= overlayOn.Width ||
                    sampleY >= overlayOn.Height) return false;
                int sampleIndex = sampleY * overlayOn.Width + sampleX;
                if (!PixelsDiffer(
                        overlayOn.Pixels[sampleIndex],
                        overlayOff.Pixels[sampleIndex])) return false;
            }
            return true;
        }

        private static bool IsFinite(Vector2 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y);
        }

        private static bool IsVisuallyBlank(IReadOnlyList<Color32> pixels)
        {
            if (pixels == null || pixels.Count == 0) return true;
            byte minR = byte.MaxValue, minG = byte.MaxValue, minB = byte.MaxValue;
            byte maxR = byte.MinValue, maxG = byte.MinValue, maxB = byte.MinValue;
            int step = Mathf.Max(1, pixels.Count / 65536);
            for (var index = 0; index < pixels.Count; index += step)
            {
                Color32 color = pixels[index];
                minR = Math.Min(minR, color.r);
                minG = Math.Min(minG, color.g);
                minB = Math.Min(minB, color.b);
                maxR = Math.Max(maxR, color.r);
                maxG = Math.Max(maxG, color.g);
                maxB = Math.Max(maxB, color.b);
            }
            return maxR - minR < 8 && maxG - minG < 8 && maxB - minB < 8;
        }

        private static int ParseSpriteDirection(string spriteName)
        {
            if (string.IsNullOrWhiteSpace(spriteName)) return -1;
            string padded = "_" + spriteName.ToLowerInvariant().Trim('_') + "_";
            // Match diagonal tokens before their cardinal suffixes (northwest before west).
            int[] order = { 1, 3, 5, 7, 0, 2, 4, 6 };
            foreach (int direction in order)
            {
                if (padded.Contains("_" + DirectionTokens[direction] + "_")) return direction;
            }
            return -1;
        }

        private static bool TryParseTypingFrameIndex(string spriteName, out int frameIndex)
        {
            frameIndex = -1;
            if (string.IsNullOrWhiteSpace(spriteName)) return false;
            const string marker = "_typing_";
            int start = spriteName.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (start < 0) return false;
            start += marker.Length;
            int end = spriteName.IndexOf('_', start);
            if (end <= start) return false;
            return int.TryParse(
                spriteName.Substring(start, end - start),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out frameIndex);
        }

        private static bool HasCommandLineFlag(string flag)
        {
            return Array.Exists(
                Environment.GetCommandLineArgs(),
                argument => string.Equals(argument, flag, StringComparison.OrdinalIgnoreCase));
        }

        private static string ResolveArtifactDirectory()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index < arguments.Length - 1; index++)
            {
                if (!string.Equals(
                        arguments[index],
                        ArtifactDirectoryArgument,
                        StringComparison.OrdinalIgnoreCase)) continue;
                return Path.GetFullPath(arguments[index + 1]);
            }
            for (var index = 0; index < arguments.Length - 1; index++)
            {
                if (!string.Equals(arguments[index], "-logFile", StringComparison.OrdinalIgnoreCase)) continue;
                string logDirectory = Path.GetDirectoryName(Path.GetFullPath(arguments[index + 1]));
                if (!string.IsNullOrWhiteSpace(logDirectory))
                    return Path.Combine(logDirectory, "SeatingTransitionQa");
            }
            return Path.Combine(Application.persistentDataPath, "SeatingTransitionQa");
        }

        private string ArtifactPath(string fileName)
        {
            Directory.CreateDirectory(_artifactDirectory);
            return Path.Combine(_artifactDirectory, fileName);
        }

        private bool Fail(int exitCode, string message)
        {
            if (_failure.Length == 0)
            {
                _failureCode = exitCode;
                _failure = message ?? "unknown failure";
            }
            return false;
        }

        private void FinishFailure(int exitCode, string message)
        {
            int resolvedCode = exitCode == 0 ? 90 : exitCode;
            string resolvedMessage = string.IsNullOrWhiteSpace(message) ? "unknown failure" : message;
            string result = BuildResult(null, false, resolvedMessage);
            WriteResult(result);
            WriteFrameEvidenceManifest();
            Debug.LogError(
                "FAMILY_COMPANY_SEATING_TRANSITION_QA: FAIL | code=" + resolvedCode +
                " | " + resolvedMessage);
            RestoreTimingOverride();
            Application.Quit(resolvedCode);
        }

        private void OnDestroy()
        {
            RestoreTimingOverride();
        }

        private void RestoreTimingOverride()
        {
            if (!_timingOverrideActive) return;
            Time.captureDeltaTime = _previousCaptureDeltaTime;
            Time.timeScale = _previousTimeScale;
            _timingOverrideActive = false;
        }

        private void WriteResult(string contents)
        {
            try
            {
                File.WriteAllText(ArtifactPath("seating-transition-qa-result.txt"), contents);
            }
            catch (Exception exception)
            {
                Debug.LogError("SEATING_TRANSITION_QA_RESULT_WRITE_FAILED | " + exception.Message);
            }
        }

        private void WriteFrameEvidenceManifest()
        {
            try
            {
                var builder = new StringBuilder();
                builder.AppendLine("FAMILY_COMPANY_SEATING_FRAME_CAPTURE_MANIFEST");
                builder.AppendLine("primaryExpected=56");
                builder.AppendLine("primaryActual=" + _frameEvidenceRecords.Count);
                builder.AppendLine("primaryResolution=1024x1024");
                builder.AppendLine("captureDeltaTime=0.016667 (fixed 60Hz presentation delta)");
                builder.AppendLine(
                    "colorDifference=abs(R1-R2)+abs(G1-G2)+abs(B1-B2)>=6");
                builder.AppendLine(
                    "lowerRegion=all actor-bound pixels at or below pose pelvis + 12 source sprite px");
                builder.AppendLine(
                    "upperProtectedRegion=all actor-bound pixels above max(pelvis+32 source px, hand-anchor height)");
                builder.AppendLine(
                    "handProtectedRegion=7 source-px radius around the approved Work pose hand anchor; strict for Typing 6/6, diagnostic for SitDown/StandUp");
                builder.AppendLine(
                    "filteredCoreCandidate=runtime-visible chair-foreground effect on actor (A-B) eroded by a 3x3 neighborhood to exclude bilinear-filtered edges");
                builder.AppendLine(
                    "overlapCandidate=actor contributes with foreground off AND filteredCoreCandidate is present");
                builder.AppendLine(
                    "occluded=filtered-core overlap whose measured actor residual ratio is <=0.05 (runtime opacity proof)");
                builder.AppendLine(
                    "penetration=filtered-core overlap whose measured actor residual ratio is >0.05; required 0");
                builder.AppendLine(
                    "residualTolerance=the <=5% core residual allowance is solely for D3D11 bilinear sampling and sRGB readback quantization");
                builder.AppendLine(
                    "filteredEdgeResidual=actor/foreground overlap outside the eroded core; reported as bilinear AA evidence, not penetration");
                builder.AppendLine(
                    "transitionZeroOverlap=allowed only when lowerCandidates=0; noOverlapReason is pose/mask geometry");
                builder.AppendLine(
                    "phaseAggregate=each member SitDown/Typing/StandUp lowerOccluded sum must be >0");
                builder.AppendLine(
                    "invalidOcclusion=upper foreground overlap must be 0 in all 56 frames; hand overlap must be 0 in Typing 6/6");
                builder.AppendLine(
                    "typingSockets=1920x1080 main-camera pelvis-to-chair<=1.05px and hand-to-desk-work<=4.05px");
                builder.AppendLine(
                    "depthOrder=deskBase/chairBase/actor/deskFront/chairFront");
                foreach (FrameEvidenceRecord record in _frameEvidenceRecords
                             .OrderBy(record => Array.IndexOf(MemberIds, record.MemberId))
                             .ThenBy(record => (int)record.Kind)
                             .ThenBy(record => record.EvidenceFrame))
                    builder.AppendLine(record.ManifestLine());
                foreach (string memberId in MemberIds)
                {
                    if (!_traces.TryGetValue(memberId, out ActorTrace trace)) continue;
                    builder.Append("coverage member=").Append(memberId)
                        .Append(" sitMask=0x").Append(trace.SitEvidenceFrameMask.ToString("X2"))
                        .Append(" typingMask=0x").Append(trace.TypingEvidenceFrameMask.ToString("X2"))
                        .Append(" standMask=0x").Append(trace.StandEvidenceFrameMask.ToString("X2"))
                        .Append(" continuous=").Append(trace.NextExpectedSitEvidenceFrame)
                        .Append('/').Append(trace.NextExpectedTypingEvidenceFrame)
                        .Append('/').Append(trace.NextExpectedStandEvidenceFrame)
                        .Append(" unique=").Append(trace.EvidenceRecordCount).Append("/14")
                        .AppendLine();
                }
                File.WriteAllText(
                    ArtifactPath("seating-transition-frame-capture-manifest.txt"),
                    builder.ToString());
            }
            catch (Exception exception)
            {
                Debug.LogError("SEATING_TRANSITION_QA_MANIFEST_WRITE_FAILED | " + exception.Message);
            }
        }

        private string BuildResult(
            IReadOnlyDictionary<string, OfficeRuntimeAgent> actors,
            bool success,
            string failure)
        {
            var builder = new StringBuilder();
            builder.AppendLine("FAMILY_COMPANY_SEATING_TRANSITION_QA: " + (success ? "PASS" : "FAIL"));
            if (!success) builder.AppendLine("failure=" + failure);
            builder.AppendLine("artifacts=" + _artifactDirectory);
            builder.AppendLine("overviewResolution=1920x1080");
            builder.AppendLine("closeupResolution=1024x1024");
            builder.AppendLine("primaryCloseups=" + _frameEvidenceRecords.Count + "/56");
            builder.AppendLine("primaryUniqueKeys=" + _frameEvidenceKeys.Count + "/56");
            builder.AppendLine("safeEgressCloseups=" +
                               _traces.Values.Count(trace => trace.SafeEgressCloseupCaptured) + "/4");
            builder.AppendLine("egressMatrix=families4*rotations4*scenarios4=64");
            builder.AppendLine("captureManifest=seating-transition-frame-capture-manifest.txt");
            builder.AppendLine(
                "chairForeground=authored-curved-seat-rim-continuous; " +
                "upperBodyProtection=pose-pelvis-split");
            builder.AppendLine(
                "penetrationContract=filtered-core actor residual >5%=FAIL; <=5% allowed only for D3D11 bilinear/sRGB readback");
            foreach (string memberId in MemberIds)
            {
                if (!_traces.TryGetValue(memberId, out ActorTrace trace)) continue;
                OfficeRuntimeAgent actor = null;
                if (actors != null) actors.TryGetValue(memberId, out actor);
                builder.Append(memberId)
                    .Append(" direction=").Append(trace.ExpectedDirection)
                    .Append(" sit=").Append(CountBits(trace.SitDownFrameMask)).Append("/4")
                    .Append(" workHook=").Append(trace.WorkHookSprites.Count).Append("/6")
                    .Append(" stand=").Append(CountBits(trace.StandUpFrameMask)).Append("/4")
                    .Append(" evidenceMasks=0x").Append(trace.SitEvidenceFrameMask.ToString("X2"))
                    .Append("/0x").Append(trace.TypingEvidenceFrameMask.ToString("X2"))
                    .Append("/0x").Append(trace.StandEvidenceFrameMask.ToString("X2"))
                    .Append(" continuous=").Append(trace.NextExpectedSitEvidenceFrame)
                    .Append('/').Append(trace.NextExpectedTypingEvidenceFrame)
                    .Append('/').Append(trace.NextExpectedStandEvidenceFrame)
                    .Append(" evidence=").Append(trace.EvidenceRecordCount).Append("/14")
                    .Append(" directionSamples=").Append(trace.DirectionSampleCount)
                    .Append(" leavingSamples=").Append(trace.LeavingSeatSampleCount)
                    .Append(" depthSamples=").Append(trace.DepthSampleCount)
                    .Append(" overlayChangedPixels=").Append(trace.OverlayChangedPixels)
                    .Append(" lowerCandidates=").Append(trace.LowerBodyOverlapCandidatePixels)
                    .Append(" lowerOccluded=").Append(trace.LowerBodyOccludedPixels)
                    .Append(" phaseLowerOccluded=").Append(trace.SitLowerBodyOccludedPixels)
                    .Append('/').Append(trace.TypingLowerBodyOccludedPixels)
                    .Append('/').Append(trace.StandLowerBodyOccludedPixels)
                    .Append(" penetration=").Append(trace.ForegroundPenetrationPixels)
                    .Append(" filteredEdgeResidual=").Append(trace.FilteredEdgeResidualPixels)
                    .Append(" invalidUpperForegroundOverlap=")
                    .Append(trace.UpperBodyInvalidForegroundOverlapPixels)
                    .Append(" invalidHandForegroundOverlap=")
                    .Append(trace.HandInvalidForegroundOverlapPixels)
                    .Append(" minUpperRetention=")
                    .Append(trace.MinimumUpperBodyRetention.ToString("F3", CultureInfo.InvariantCulture))
                    .Append(" minHandRetention=")
                    .Append(trace.MinimumHandRetention.ToString("F3", CultureInfo.InvariantCulture))
                    .Append(" noOverlapExpectedFrames=").Append(trace.NoLowerBodyOverlapFrameCount);
                if (actor != null)
                {
                    builder.Append(" facingViolations=").Append(actor.SeatingFacingViolationCount)
                        .Append(" spriteDirectionMismatches=").Append(actor.SeatingSpriteDirectionMismatchCount)
                        .Append(" maxOctantDelta=").Append(actor.MaximumSeatingSpriteDirectionOctantDelta)
                        .Append(" depthViolations=").Append(actor.SeatingDepthViolationCount)
                        .Append(" pelvisStepPx=").Append(actor.MaxTransitionPelvisStepPx.ToString("F3"))
                        .Append(" anchorErrorPx=").Append(actor.MaxAnimatedAnchorErrorPx.ToString("F3"))
                        .Append(" typingSeatPx=").Append(actor.MaxTypingSeatContactErrorPx.ToString("F3"))
                        .Append(" handKeyboardPx=").Append(actor.MaxTypingHandWorkErrorPx.ToString("F3"))
                        .Append(" chairStepPx=").Append(actor.MaxChairPresentationStepPx.ToString("F3"))
                        .Append(" egressKind=").Append(actor.LastCompletedSeatEgressKind)
                        .Append(" egressCell=").Append(actor.LastCompletedSeatEgressCell)
                        .Append(" egressStepPx=").Append(actor.MaximumSeatEgressRootStepPx.ToString("F3"))
                        .Append(" egressAttempts=").Append(actor.SeatEgressReservationAttemptCount)
                        .Append(" egressBlocked=").Append(actor.SeatEgressBlockedAttemptCount)
                        .Append(" egressViolations=").Append(actor.SeatEgressCollisionViolationCount)
                        .Append('/').Append(actor.SeatEgressUnsafePhaseTransitionCount)
                        .Append(" stationaryActorPixels=").Append(trace.SafeEgressActorPixels)
                        .Append(" stationaryChairPixels=").Append(trace.SafeEgressChairPixels)
                        .Append(" stationaryEmbeddedOverlap=")
                        .Append(trace.SafeEgressEmbeddedOverlapPixels);
                }
                builder.AppendLine();
            }
            return builder.ToString();
        }

        private static string BuildActorSummary(
            IReadOnlyDictionary<string, OfficeRuntimeAgent> actors)
        {
            return string.Join(
                ", ",
                MemberIds.Select(memberId =>
                {
                    OfficeRuntimeAgent actor = actors[memberId];
                    return $"{memberId}=phase:{actor.Phase}/clip:{actor.CurrentSeatingClip}/" +
                           $"frame:{actor.CurrentSeatingFrame}/sprite:{actor.CurrentSpriteName}/" +
                           $"direction:{actor.CurrentDirection}/seat:{actor.ActiveSeatId}/" +
                           $"egress:{actor.ActiveSeatEgressKind}/{actor.HasSeatEgressReservation}/" +
                           $"{actor.HasReachedSeatEgressSafeAnchor}/{actor.LastSeatEgressBlocker}";
                }));
        }

        private static int CountBits(int mask)
        {
            var count = 0;
            while (mask != 0)
            {
                count += mask & 1;
                mask >>= 1;
            }
            return count;
        }

        private enum FrameEvidenceKind
        {
            SitDown,
            Typing,
            StandUp
        }

        private readonly struct CapturedFrame
        {
            public CapturedFrame(
                int width,
                int height,
                Color32[] pixels,
                RectInt focusRect,
                Vector2[] evidencePixels)
            {
                Width = width;
                Height = height;
                Pixels = pixels;
                FocusRect = focusRect;
                EvidencePixels = evidencePixels ?? Array.Empty<Vector2>();
            }

            public int Width { get; }
            public int Height { get; }
            public Color32[] Pixels { get; }
            public RectInt FocusRect { get; }
            public Vector2[] EvidencePixels { get; }
        }

        private readonly struct OcclusionEvidence
        {
            public OcclusionEvidence(
                int overlayChangedPixels,
                int lowerBodyActorPixels,
                int lowerBodyOverlapCandidatePixels,
                int lowerBodyOccludedPixels,
                int foregroundOverlapCandidatePixels,
                int foregroundPenetrationPixels,
                int filteredEdgeResidualPixels,
                int upperBodyActorPixels,
                int upperBodyVisiblePixels,
                int upperBodyInvalidForegroundOverlapPixels,
                float upperBodyRetention,
                int handActorPixels,
                int handVisiblePixels,
                int handInvalidForegroundOverlapPixels,
                float handRetention,
                float pelvisSeatErrorPx,
                float handWorkErrorPx)
            {
                OverlayChangedPixels = overlayChangedPixels;
                LowerBodyActorPixels = lowerBodyActorPixels;
                LowerBodyOverlapCandidatePixels = lowerBodyOverlapCandidatePixels;
                LowerBodyOccludedPixels = lowerBodyOccludedPixels;
                ForegroundOverlapCandidatePixels = foregroundOverlapCandidatePixels;
                ForegroundPenetrationPixels = foregroundPenetrationPixels;
                FilteredEdgeResidualPixels = filteredEdgeResidualPixels;
                UpperBodyActorPixels = upperBodyActorPixels;
                UpperBodyVisiblePixels = upperBodyVisiblePixels;
                UpperBodyInvalidForegroundOverlapPixels =
                    upperBodyInvalidForegroundOverlapPixels;
                UpperBodyRetention = upperBodyRetention;
                HandActorPixels = handActorPixels;
                HandVisiblePixels = handVisiblePixels;
                HandInvalidForegroundOverlapPixels = handInvalidForegroundOverlapPixels;
                HandRetention = handRetention;
                PelvisSeatErrorPx = pelvisSeatErrorPx;
                HandWorkErrorPx = handWorkErrorPx;
            }

            public int OverlayChangedPixels { get; }
            public int LowerBodyActorPixels { get; }
            public int LowerBodyOverlapCandidatePixels { get; }
            public int LowerBodyOccludedPixels { get; }
            public int ForegroundOverlapCandidatePixels { get; }
            public int ForegroundPenetrationPixels { get; }
            public int FilteredEdgeResidualPixels { get; }
            public int UpperBodyActorPixels { get; }
            public int UpperBodyVisiblePixels { get; }
            public int UpperBodyInvalidForegroundOverlapPixels { get; }
            public float UpperBodyRetention { get; }
            public int HandActorPixels { get; }
            public int HandVisiblePixels { get; }
            public int HandInvalidForegroundOverlapPixels { get; }
            public float HandRetention { get; }
            public float PelvisSeatErrorPx { get; }
            public float HandWorkErrorPx { get; }
            public bool NoLowerBodyOverlapExpected => LowerBodyOverlapCandidatePixels == 0;
        }

        private readonly struct FrameEvidenceRecord
        {
            public FrameEvidenceRecord(
                string key,
                string memberId,
                FrameEvidenceKind kind,
                int evidenceFrame,
                string spriteName,
                OfficeRuntimeAgentPhase phase,
                OfficeSeatingAnimationClip clip,
                int runtimeFrame,
                string primaryPath,
                OfficeSeatingDepthSnapshot depth,
                OcclusionEvidence evidence)
            {
                Key = key;
                MemberId = memberId;
                Kind = kind;
                EvidenceFrame = evidenceFrame;
                SpriteName = spriteName;
                Phase = phase;
                Clip = clip;
                RuntimeFrame = runtimeFrame;
                PrimaryPath = primaryPath;
                Depth = depth;
                Evidence = evidence;
            }

            public string Key { get; }
            public string MemberId { get; }
            public FrameEvidenceKind Kind { get; }
            public int EvidenceFrame { get; }
            public string SpriteName { get; }
            public OfficeRuntimeAgentPhase Phase { get; }
            public OfficeSeatingAnimationClip Clip { get; }
            public int RuntimeFrame { get; }
            public string PrimaryPath { get; }
            public OfficeSeatingDepthSnapshot Depth { get; }
            public OcclusionEvidence Evidence { get; }

            public string ManifestLine()
            {
                string noOverlapReason = Evidence.NoLowerBodyOverlapExpected
                    ? "4-way projected pose plus runtime A-B foreground effect yielded zero eroded lower-body intersection"
                    : "none";
                return Key +
                       " phase=" + Phase +
                       " clip=" + Clip +
                       " runtimeFrame=" + RuntimeFrame +
                       " sprite=" + SpriteName +
                       " primary=" + Path.GetFileName(PrimaryPath) +
                       " lowerActor=" + Evidence.LowerBodyActorPixels +
                       " lowerCandidates=" + Evidence.LowerBodyOverlapCandidatePixels +
                       " lowerOccluded=" + Evidence.LowerBodyOccludedPixels +
                       " foregroundCandidates=" + Evidence.ForegroundOverlapCandidatePixels +
                       " penetration=" + Evidence.ForegroundPenetrationPixels +
                       " upperActor=" + Evidence.UpperBodyActorPixels +
                       " upperVisible=" + Evidence.UpperBodyVisiblePixels +
                       " filteredEdgeResidual=" + Evidence.FilteredEdgeResidualPixels +
                       " upperInvalidForegroundOverlap=" +
                       Evidence.UpperBodyInvalidForegroundOverlapPixels +
                       " upperRetention=" + F3(Evidence.UpperBodyRetention) +
                       " handActor=" + Evidence.HandActorPixels +
                       " handVisible=" + Evidence.HandVisiblePixels +
                       " handInvalidForegroundOverlap=" +
                       Evidence.HandInvalidForegroundOverlapPixels +
                       " handRetention=" + F3(Evidence.HandRetention) +
                       " pelvisSeatPx=" + F3(Evidence.PelvisSeatErrorPx) +
                       " handWorkPx=" + F3(Evidence.HandWorkErrorPx) +
                       " depth=" + Depth.DeskBaseOrder + "/" + Depth.ChairBaseOrder + "/" +
                       Depth.ActorOrder + "/" + Depth.DeskFrontOrder + "/" + Depth.ChairFrontOrder +
                       " noOverlapExpected=" + Evidence.NoLowerBodyOverlapExpected +
                       " noOverlapReason=\"" + noOverlapReason + "\"";
            }

            private static string F3(float value) =>
                value.ToString("F3", CultureInfo.InvariantCulture);
        }

        private sealed class ActorTrace
        {
            public ActorTrace(string memberId)
            {
                MemberId = memberId;
            }

            public string MemberId { get; }
            public string SeatId { get; set; } = string.Empty;
            public int ExpectedDirection { get; set; } = -1;
            public int SitDownFrameMask { get; set; }
            public int StandUpFrameMask { get; set; }
            public int DepthSitDownFrameMask { get; set; }
            public int DepthStandUpFrameMask { get; set; }
            public int SitEvidenceFrameMask { get; set; }
            public int TypingEvidenceFrameMask { get; set; }
            public int StandEvidenceFrameMask { get; set; }
            public int NextExpectedSitEvidenceFrame { get; set; }
            public int NextExpectedTypingEvidenceFrame { get; set; }
            public int NextExpectedStandEvidenceFrame { get; set; }
            public int DirectionSampleCount { get; set; }
            public int DepthSampleCount { get; set; }
            public int LeavingSeatSampleCount { get; set; }
            public int EvidenceRecordCount { get; private set; }
            public int OverlayChangedPixels { get; private set; }
            public int LowerBodyActorPixels { get; private set; }
            public int LowerBodyOverlapCandidatePixels { get; private set; }
            public int LowerBodyOccludedPixels { get; private set; }
            public int ForegroundOverlapCandidatePixels { get; private set; }
            public int ForegroundPenetrationPixels { get; private set; }
            public int UpperBodyActorPixels { get; private set; }
            public int UpperBodyVisiblePixels { get; private set; }
            public int FilteredEdgeResidualPixels { get; private set; }
            public int UpperBodyInvalidForegroundOverlapPixels { get; private set; }
            public int HandActorPixels { get; private set; }
            public int HandVisiblePixels { get; private set; }
            public int HandInvalidForegroundOverlapPixels { get; private set; }
            public float MinimumUpperBodyRetention { get; private set; } = 1f;
            public float MinimumHandRetention { get; private set; } = 1f;
            public float MaximumPelvisSeatErrorPx { get; private set; }
            public float MaximumHandWorkErrorPx { get; private set; }
            public int NoLowerBodyOverlapFrameCount { get; private set; }
            public int SitLowerBodyOccludedPixels { get; private set; }
            public int TypingLowerBodyOccludedPixels { get; private set; }
            public int StandLowerBodyOccludedPixels { get; private set; }
            public bool SawAlignedMovingToSit { get; set; }
            public bool SawWorkHookActive { get; set; }
            public bool SawFinishingWork { get; set; }
            public bool SawLeavingSeat { get; set; }
            public bool SawReservedBeforeStandUp { get; set; }
            public bool SafeEgressCloseupCaptured { get; set; }
            public int SafeEgressActorPixels { get; set; }
            public int SafeEgressChairPixels { get; set; }
            public int SafeEgressEmbeddedOverlapPixels { get; set; }
            public bool SitCloseupCaptured { get; set; }
            public bool WorkCloseupCaptured { get; set; }
            public bool StandCloseupCaptured { get; set; }
            public string[] TypingSpriteNames { get; } = new string[6];
            public HashSet<OfficeRuntimeAgentPhase> Phases { get; } =
                new HashSet<OfficeRuntimeAgentPhase>();
            public HashSet<string> WorkHookSprites { get; } =
                new HashSet<string>(StringComparer.Ordinal);
            public HashSet<string> DepthWorkHookSprites { get; } =
                new HashSet<string>(StringComparer.Ordinal);
            public HashSet<string> LoggedSamples { get; } =
                new HashSet<string>(StringComparer.Ordinal);

            public void RecordEvidence(FrameEvidenceKind kind, OcclusionEvidence evidence)
            {
                EvidenceRecordCount++;
                OverlayChangedPixels += evidence.OverlayChangedPixels;
                LowerBodyActorPixels += evidence.LowerBodyActorPixels;
                LowerBodyOverlapCandidatePixels += evidence.LowerBodyOverlapCandidatePixels;
                LowerBodyOccludedPixels += evidence.LowerBodyOccludedPixels;
                ForegroundOverlapCandidatePixels += evidence.ForegroundOverlapCandidatePixels;
                ForegroundPenetrationPixels += evidence.ForegroundPenetrationPixels;
                FilteredEdgeResidualPixels += evidence.FilteredEdgeResidualPixels;
                UpperBodyActorPixels += evidence.UpperBodyActorPixels;
                UpperBodyVisiblePixels += evidence.UpperBodyVisiblePixels;
                UpperBodyInvalidForegroundOverlapPixels +=
                    evidence.UpperBodyInvalidForegroundOverlapPixels;
                HandActorPixels += evidence.HandActorPixels;
                HandVisiblePixels += evidence.HandVisiblePixels;
                HandInvalidForegroundOverlapPixels +=
                    evidence.HandInvalidForegroundOverlapPixels;
                MinimumUpperBodyRetention = Mathf.Min(
                    MinimumUpperBodyRetention,
                    evidence.UpperBodyRetention);
                MinimumHandRetention = Mathf.Min(MinimumHandRetention, evidence.HandRetention);
                MaximumPelvisSeatErrorPx = Mathf.Max(
                    MaximumPelvisSeatErrorPx,
                    evidence.PelvisSeatErrorPx);
                MaximumHandWorkErrorPx = Mathf.Max(
                    MaximumHandWorkErrorPx,
                    evidence.HandWorkErrorPx);
                if (evidence.NoLowerBodyOverlapExpected) NoLowerBodyOverlapFrameCount++;
                switch (kind)
                {
                    case FrameEvidenceKind.SitDown:
                        SitLowerBodyOccludedPixels += evidence.LowerBodyOccludedPixels;
                        break;
                    case FrameEvidenceKind.Typing:
                        TypingLowerBodyOccludedPixels += evidence.LowerBodyOccludedPixels;
                        break;
                    case FrameEvidenceKind.StandUp:
                        StandLowerBodyOccludedPixels += evidence.LowerBodyOccludedPixels;
                        break;
                }
            }
        }
    }
}
