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
        private readonly Dictionary<string, FurnitureTransformBaseline> _furnitureBaselines =
            new Dictionary<string, FurnitureTransformBaseline>(StringComparer.Ordinal);

        private StarterOfficeRuntimeBootstrap _runtime;
        private OfficeCharacterSeatPoseCatalog _poseCatalog;
        private string _artifactDirectory = string.Empty;
        private string _failure = string.Empty;
        private int _failureCode;
        private bool _atomicDockBeforeOverviewCaptured;
        private float _maximumFurnitureWorldPositionErrorPx;
        private float _maximumFurnitureWorldRotationErrorDegrees;
        private float _maximumFurnitureWorldScaleError;
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
            // enough to hide an atomic before/after frame or skip a Work-hook sprite.
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
            if (!CaptureFurnitureTransformBaselines(out string furnitureBaselineFailure))
            {
                FinishFailure(92, "Furniture baseline failed: " + furnitureBaselineFailure);
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

            foreach (string memberId in MemberIds) actors[memberId].BeginQaControl();
            if (!ValidateExclusiveSeatReservation(out string reservationFailure))
            {
                FinishFailure(92, "Exclusive seat reservation failed: " + reservationFailure);
                yield break;
            }

            foreach (string memberId in MemberIds)
            {
                OfficeRuntimeAgent actor = actors[memberId];
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
                    "Classic atomic dock/Work evidence timed out: " + BuildActorSummary(actors));
                yield break;
            }

            if (!CaptureOverview("seating-transition-work-overview-1920x1080.png", out string captureFailure))
            {
                FinishFailure(95, "Work overview capture failed: " + captureFailure);
                yield break;
            }

            foreach (string memberId in MemberIds)
            {
                if (actors[memberId].QaRequestStandWithOutwardRoute()) continue;
                FinishFailure(96, "Could not begin classic atomic exit for " + memberId + ".");
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
                FinishFailure(96, "Classic atomic exit/FirstWalk evidence timed out: " +
                                  BuildActorSummary(actors));
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

            const int expectedPrimaryCaptureCount = 4 * (1 + 6);
            int actualPrimaryCaptureCount = _frameEvidenceRecords.Count(record =>
                record.Kind != FrameEvidenceKind.Typing);
            int actualPrimaryKeyCount = _frameEvidenceRecords
                .Where(record => record.Kind != FrameEvidenceKind.Typing)
                .Select(record => record.Key)
                .Distinct(StringComparer.Ordinal)
                .Count();
            if (actualPrimaryCaptureCount != expectedPrimaryCaptureCount ||
                actualPrimaryKeyCount != expectedPrimaryCaptureCount)
            {
                FinishFailure(
                    97,
                    $"Primary closeup coverage is {actualPrimaryCaptureCount}/" +
                    $"{actualPrimaryKeyCount}; expected {expectedPrimaryCaptureCount} unique frames.");
                yield break;
            }

            bool observedTyping = _traces.Values.Any(trace => trace.SawTypingMicroAction);
            string typingDiagnosticFailure = BuildTypingDiagnosticFailure();
            bool typingDiagnosticPass = !observedTyping || typingDiagnosticFailure.Length == 0;
            bool chairValidationPass = actualPrimaryCaptureCount == expectedPrimaryCaptureCount &&
                                       MemberIds.All(memberId =>
                                           _traces[memberId].WorkEvidenceFrameMask == 0x3f);
            string result = BuildResult(actors, chairValidationPass, string.Empty);
            foreach (OfficeRuntimeAgent actor in actors.Values) actor.EndQaControl();
            if (!TryCreateRelocatedWorkstationLayout(
                    _runtime.World.Grid,
                    out OfficeGrid relocated,
                    out OfficeGridCoordinate oldDeskCell,
                    out OfficeGridCoordinate oldSeatCell,
                    out OfficeGridCoordinate newDeskCell,
                    out OfficeGridCoordinate newSeatCell))
            {
                FinishFailure(98, "Could not create the arbitrary workstation layout fixture.");
                yield break;
            }
            string relocatedHash = relocated.ComputeLayoutHash();
            _runtime.ApplyLayoutForQa(relocated);
            float rebuildDeadline = Time.realtimeSinceStartup + 25f;
            while (Time.realtimeSinceStartup < rebuildDeadline && !_runtime.IsReady)
                yield return null;
            if (!_runtime.IsReady || _runtime.World == null ||
                !string.Equals(_runtime.LayoutHash, relocatedHash, StringComparison.Ordinal))
            {
                FinishFailure(98, "Arbitrary workstation layout did not rebuild to the requested hash.");
                yield break;
            }
            yield return new WaitForEndOfFrame();
            if (!ValidateRelocatedWorkstationLayout(
                    oldDeskCell,
                    oldSeatCell,
                    newDeskCell,
                    newSeatCell,
                    out string relocatedFailure))
            {
                FinishFailure(98, "Arbitrary workstation layout failed: " + relocatedFailure);
                yield break;
            }
            result += Environment.NewLine +
                      "chairSeatStability=PASS chairInvariance=PASS seatExclusivity=PASS " +
                      "bodySeatStability=PASS" + Environment.NewLine +
                      "arbitraryLayoutRefresh=PASS oldDesk=" + oldDeskCell +
                      " oldSeat=" + oldSeatCell + " newDesk=" + newDeskCell +
                      " newSeat=" + newSeatCell + " hash=" + relocatedHash;
            WriteResult(result);
            WriteFrameEvidenceManifest();
            Debug.Log(
                "FAMILY_COMPANY_CHAIR_SEAT_STABILITY_QA: PASS | " +
                "family=4 classicAtomicDock=4/4 workHook=6/6 reservedAtomicExit=4/4 " +
                "transitionClips=0 directionMismatch=0 maxOctantDelta=0 " +
                "seatResidual<=0.9px logicalRoot<=0.001px " +
                "primaryCloseups=28/28 atomicSeat+work penetration=0 " +
                "invalidUpperForegroundOverlap=0 typingHandForegroundOverlap=0 " +
                "chairTransform=semantic+visual+parent immutable chairForeground=seat-rim-only " +
                "egress=reserved-before-publish/safe-anchor/overlap0/turn/laterFirstWalk " +
                "arbitraryLayoutRefresh=PASS " +
                "captures=1920x1080+1024x1024");
            if (!observedTyping)
            {
                Debug.Log(
                    "FAMILY_COMPANY_TYPING_EVIDENCE_DIAGNOSTIC: NOT_OBSERVED | " +
                    "optional=true chairResultUnaffected=true");
            }
            else if (typingDiagnosticPass)
            {
                Debug.Log(
                    "FAMILY_COMPANY_TYPING_EVIDENCE_DIAGNOSTIC: PASS | " +
                    "chairResultUnaffected=true");
            }
            else
            {
                Debug.LogWarning(
                    "FAMILY_COMPANY_TYPING_EVIDENCE_DIAGNOSTIC: FAIL | " +
                    typingDiagnosticFailure + " | chairResultUnaffected=true");
            }
            RestoreTimingOverride();
            yield return null;
            Application.Quit(chairValidationPass ? 0 : 97);
        }

        private bool SampleAll(IReadOnlyDictionary<string, OfficeRuntimeAgent> actors)
        {
            if (!ValidateFurnitureTransformBaselines(out string furnitureFailure))
                return Fail(93, furnitureFailure);
            string[] claimedSeats = actors.Values
                .Where(actor => actor != null && actor.ActiveSeatId.Length > 0)
                .Select(actor => actor.ActiveSeatId)
                .ToArray();
            if (claimedSeats.Distinct(StringComparer.Ordinal).Count() != claimedSeats.Length)
                return Fail(93, "Two runtime actors hold the same seat: " +
                                string.Join(",", claimedSeats));
            foreach (string memberId in MemberIds)
            {
                if (SampleActor(actors[memberId], _traces[memberId])) continue;
                return false;
            }
            return true;
        }

        private bool CaptureFurnitureTransformBaselines(out string failure)
        {
            failure = string.Empty;
            _furnitureBaselines.Clear();
            OfficeGridFurniturePresenter presenter = _runtime?.World?.FurniturePresenter;
            if (presenter == null)
            {
                failure = "furniture presenter is missing";
                return false;
            }
            foreach (PlacedOfficeFurniture furniture in _runtime.World.Grid.Furniture)
            {
                if (!presenter.TryGetSemanticRoot(furniture.FurnitureId, out Transform semantic) ||
                    semantic == null ||
                    !presenter.TryGetVisualRoot(furniture.FurnitureId, out Transform visual) ||
                    visual == null)
                {
                    failure = "missing Transform for " + furniture.FurnitureId;
                    return false;
                }
                _furnitureBaselines.Add(
                    furniture.FurnitureId,
                    new FurnitureTransformBaseline(furniture.KindId, semantic, visual));
            }
            return ValidateFurnitureTransformBaselines(out failure);
        }

        private static bool TryCreateRelocatedWorkstationLayout(
            OfficeGrid source,
            out OfficeGrid relocated,
            out OfficeGridCoordinate oldDeskCell,
            out OfficeGridCoordinate oldSeatCell,
            out OfficeGridCoordinate newDeskCell,
            out OfficeGridCoordinate newSeatCell)
        {
            relocated = null;
            oldDeskCell = default;
            oldSeatCell = default;
            newDeskCell = default;
            newSeatCell = default;
            if (source == null) return false;
            PlacedOfficeFurniture oldDesk = source.Furniture.FirstOrDefault(item =>
                string.Equals(item.FurnitureId, "desk_player", StringComparison.Ordinal));
            OfficeSeatSlot oldSeat = source.SeatSlots.FirstOrDefault(item =>
                string.Equals(item.SeatId, "seat_player", StringComparison.Ordinal));
            if (oldDesk == null || oldSeat == null) return false;
            oldDeskCell = oldDesk.Origin;
            oldSeatCell = oldSeat.Cell;

            // Exercise the same atomic layout transaction used by arbitrary player furniture
            // placement. The starter layout contract guarantees this two-cell move and the edit
            // rule moves the desk, chair, seat, approach and operator anchor as one unit.
            OfficeLayoutEditResult move =
                OfficeLayoutEditRules.MoveWorkstation(source, oldSeat.SeatId, 2, 0);
            if (!move.Success || move.Grid == null) return false;
            relocated = move.Grid;
            PlacedOfficeFurniture newDesk = relocated.Furniture.FirstOrDefault(item =>
                string.Equals(item.FurnitureId, oldDesk.FurnitureId, StringComparison.Ordinal));
            OfficeSeatSlot movedSeat = relocated.SeatSlots.FirstOrDefault(item =>
                string.Equals(item.SeatId, oldSeat.SeatId, StringComparison.Ordinal));
            if (newDesk == null || movedSeat == null) return false;
            newDeskCell = newDesk.Origin;
            newSeatCell = movedSeat.Cell;
            return !newDeskCell.Equals(oldDeskCell) && !newSeatCell.Equals(oldSeatCell);
        }

        private bool ValidateRelocatedWorkstationLayout(
            OfficeGridCoordinate oldDeskCell,
            OfficeGridCoordinate oldSeatCell,
            OfficeGridCoordinate newDeskCell,
            OfficeGridCoordinate newSeatCell,
            out string failure)
        {
            failure = string.Empty;
            if (_runtime.Actors.Count != MemberIds.Length ||
                _runtime.World.Grid.SeatSlots.Count != MemberIds.Length)
            {
                failure = "canonical actor/seat count changed";
                return false;
            }
            OfficeSeatSlot seat = _runtime.World.Workstations.RequiredSeat("seat_player");
            if (!seat.Cell.Equals(newSeatCell) ||
                !seat.ApproachCell.Equals(new OfficeGridCoordinate(newSeatCell.X, newSeatCell.Y - 1)))
            {
                failure = "seat registry retained old cells";
                return false;
            }
            OfficeSeatInteractionAnchors anchors =
                _runtime.World.Workstations.ResolveInteractionAnchors(seat);
            if (anchors.Egress.Count != OfficeSeatEgressRules.CandidateCount ||
                Vector3.Distance(
                    anchors.ApproachWorld,
                    _runtime.World.Presenter.CellCenterWorld(seat.ApproachCell)) > 0.000001f ||
                Vector3.Distance(
                    anchors.AlignmentWorld,
                    _runtime.World.Presenter.SubcellAnchorWorld(seat.OperatorAnchor)) > 0.000001f ||
                Vector3.Distance(
                    anchors.PelvisWorld,
                    _runtime.World.Workstations.ChairSeatAnchorWorld(seat)) > 0.000001f ||
                !anchors.HasHandWorld)
            {
                failure = "explicit seat anchors were not rebuilt";
                return false;
            }
            if (_runtime.World.Occupancy.IsCellPassable(
                    newDeskCell, string.Empty, string.Empty, false) ||
                _runtime.World.Occupancy.IsCellPassable(
                    newSeatCell, string.Empty, string.Empty, false) ||
                !_runtime.World.Occupancy.IsCellPassable(
                    oldDeskCell, string.Empty, string.Empty, false) ||
                !_runtime.World.Occupancy.IsCellPassable(
                    oldSeatCell, string.Empty, string.Empty, false))
            {
                failure = "static/interaction occupancy retained the previous placement";
                return false;
            }
            return CaptureFurnitureTransformBaselines(out failure);
        }

        private bool ValidateFurnitureTransformBaselines(out string failure)
        {
            failure = string.Empty;
            OfficeGridFurniturePresenter presenter = _runtime?.World?.FurniturePresenter;
            if (presenter == null)
            {
                failure = "furniture presenter was destroyed";
                return false;
            }
            if (presenter.TransformInvariantViolationCount != 0)
            {
                failure = "furniture invariant guard observed mutations=" +
                          presenter.TransformInvariantViolationCount;
                return false;
            }
            if (!presenter.ValidateTransformInvariants(out string presenterFailure))
            {
                failure = presenterFailure;
                return false;
            }
            foreach (KeyValuePair<string, FurnitureTransformBaseline> pair in _furnitureBaselines)
            {
                _maximumFurnitureWorldPositionErrorPx = Mathf.Max(
                    _maximumFurnitureWorldPositionErrorPx,
                    pair.Value.WorldPositionErrorPx(Camera.main));
                _maximumFurnitureWorldRotationErrorDegrees = Mathf.Max(
                    _maximumFurnitureWorldRotationErrorDegrees,
                    pair.Value.WorldRotationErrorDegrees());
                _maximumFurnitureWorldScaleError = Mathf.Max(
                    _maximumFurnitureWorldScaleError,
                    pair.Value.WorldScaleError());
                if (pair.Value.MatchesExactly()) continue;
                failure = "Furniture Transform changed during simultaneous seating: " +
                          pair.Key + "/" + pair.Value.KindId;
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

        private bool ValidateExclusiveSeatReservation(out string failure)
        {
            failure = string.Empty;
            OfficeRuntimeWorkstationService workstations = _runtime?.World?.Workstations;
            if (workstations == null)
            {
                failure = "workstation service is missing";
                return false;
            }

            OfficeSeatRuntimeClaim firstClaim = null;
            OfficeSeatRuntimeClaim secondClaim = null;
            try
            {
                if (!workstations.TryReserveSeat(
                        "player",
                        "seat_player",
                        "exclusive-seat-qa-a",
                        out OfficeSeatSlot firstSeat,
                        out firstClaim) || firstSeat == null || firstClaim == null)
                {
                    failure = "first claimant could not reserve seat_player";
                    return false;
                }
                if (workstations.TryReserveSeat(
                        "older_sister",
                        "seat_player",
                        "exclusive-seat-qa-b",
                        out _,
                        out secondClaim))
                {
                    failure = "second claimant reserved seat_player while first claim was active";
                    return false;
                }
                return true;
            }
            finally
            {
                secondClaim?.TryRelease(out _);
                firstClaim?.TryRelease(out _);
            }
        }

        private bool SampleActor(OfficeRuntimeAgent actor, ActorTrace trace)
        {
            if (actor == null) return Fail(93, trace.MemberId + " actor was destroyed.");

            OfficeRuntimeAgentPhase phase = actor.Phase;
            if (phase == OfficeRuntimeAgentPhase.ApproachingSeat)
                trace.SawApproachingSeat = true;
            if (phase == OfficeRuntimeAgentPhase.AligningSeat)
                trace.SawAligningSeat = true;
            if (phase == OfficeRuntimeAgentPhase.RotatingToSeat)
                trace.SawRotatingToSeat = true;
            if (phase == OfficeRuntimeAgentPhase.RotatingToSeat &&
                TryResolveClaimedSeatDirection(actor, out int movingSeatDirection) &&
                actor.ExpectedSeatDirection == movingSeatDirection &&
                actor.CurrentDirection == movingSeatDirection &&
                actor.IsSeatEntryPresentationPlanted)
            {
                if (actor.CurrentSeatingClip.HasValue ||
                    actor.R5eCurrentVelocityMagnitude > 0.0001f ||
                    actor.R5eLastActualDisplacementMagnitude > 0.0001f ||
                    actor.VisibleFrameMovementWorld > 0.0001f)
                    return Fail(93, trace.MemberId +
                        " was not planted/motion0 before classic atomic docking.");
                trace.SawAlignedBeforeSitDown = true;
                if (trace.PreDockRuntimeTick == 0)
                {
                    trace.PreDockRuntimeTick = actor.R5eRuntimeTick;
                    trace.PreDockWorld = actor.Position;
                    trace.PreDockSpriteName = actor.CurrentSpriteName;
                    if (trace.MemberId == "older_sister" && !_atomicDockBeforeOverviewCaptured)
                    {
                        if (!CaptureOverview(
                                "seating-transition-atomic-dock-before-overview-1920x1080.png",
                                out string beforeCaptureFailure))
                            return Fail(95, "Atomic dock before-overview failed: " +
                                            beforeCaptureFailure);
                        _atomicDockBeforeOverviewCaptured = true;
                    }
                }
            }

            OfficeSeatingAnimationClip? clip = actor.CurrentSeatingClip;
            if (phase == OfficeRuntimeAgentPhase.SittingDown ||
                phase == OfficeRuntimeAgentPhase.StandingUp ||
                clip == OfficeSeatingAnimationClip.SitDown ||
                clip == OfficeSeatingAnimationClip.StandUp ||
                actor.ObservedSitDownFrameCount != 0 ||
                actor.ObservedStandUpFrameCount != 0 ||
                IsForbiddenClassicTransitionSprite(actor.CurrentSpriteName))
                return Fail(93, trace.MemberId +
                    " rendered a forbidden SitDown/StandUp state on the classic atomic path: " +
                    $"phase={phase} clip={clip} sprite={actor.CurrentSpriteName} " +
                    $"frames={actor.ObservedSitDownFrameCount}/{actor.ObservedStandUpFrameCount}");

            if (phase == OfficeRuntimeAgentPhase.LeavingSeat)
            {
                if (!actor.R5eLastAtomicExitReservationBacked ||
                    actor.R5eLastAtomicExitTick <= trace.EntryAtomicTick ||
                    !actor.HasCompletedSeatEgress ||
                    !actor.LastCompletedSeatEgressClearanceValid ||
                    actor.IsOccupyingSeat || actor.IsOfficeSeatingFacingLocked ||
                    actor.R5eCurrentVelocityMagnitude > 0.0001f ||
                    actor.R5eLastActualDisplacementMagnitude > 0.0001f ||
                    actor.VisibleFrameMovementWorld > 0.0001f ||
                    Vector2.Distance(actor.Position, actor.LastCompletedSeatEgressWorld) > 0.0001f)
                    return Fail(93, trace.MemberId +
                        " classic atomic exit was partial, unreserved, or moving before turn completion.");
                trace.SawLeavingSeat = true;
                trace.LeavingSeatSampleCount++;
                trace.Phases.Add(phase);
                return true;
            }

            bool engaged = phase == OfficeRuntimeAgentPhase.Working ||
                           phase == OfficeRuntimeAgentPhase.FinishingWork;
            if (trace.SawLeavingSeat && !actor.HasCompletedSeatEgress &&
                phase != OfficeRuntimeAgentPhase.LeavingSeat)
                return Fail(
                    93,
                    trace.MemberId + " entered " + phase +
                    " before the reserved safe egress anchor was completed.");
            if (!ObserveClassicFirstWalk(actor, trace)) return false;
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

            OfficeSeatSlot fixedSeat =
                _runtime.World.Workstations.RequiredSeat(actor.ActiveSeatId);
            OfficeSeatInteractionAnchors anchors =
                _runtime.World.Workstations.ResolveInteractionAnchors(fixedSeat);
            Camera camera = Camera.main;
            if (camera == null)
                return Fail(93, trace.MemberId + " has no camera for logical-root validation.");
            trace.MaximumLogicalRootErrorPx = Mathf.Max(
                trace.MaximumLogicalRootErrorPx,
                OfficeGridAlignmentMetrics.ScreenDistance(
                    camera,
                    actor.transform.position,
                    anchors.AlignmentWorld));
            if (!_runtime.World.Presenter.NearestCell(actor.transform.position)
                    .Equals(fixedSeat.Cell))
                trace.SeatCellMismatchCount++;

            trace.DirectionSampleCount++;
            trace.Phases.Add(phase);
            if (phase == OfficeRuntimeAgentPhase.FinishingWork) trace.SawFinishingWork = true;

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
            int frame = actor.CurrentSeatingFrame;
            if (!depth.IsValid || depth.Phase != phase || depth.Clip != actor.CurrentSeatingClip ||
                depth.Frame != actor.CurrentSeatingFrame ||
                !depth.OcclusionEngaged || !depth.HasChairFront ||
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
            if (!actor.IsSeatedUpperBodyProtectionVisible)
            {
                return Fail(
                    93,
                    $"{trace.MemberId} upper-body protection mismatch in {phase}: " +
                    "required=True visible=" +
                    actor.IsSeatedUpperBodyProtectionVisible);
            }

            if (!trace.AtomicSeatEvidenceCaptured && phase == OfficeRuntimeAgentPhase.Working)
            {
                if (!trace.SawAlignedBeforeSitDown || trace.PreDockRuntimeTick == 0 ||
                    actor.R5eAtomicPlacementTick <= trace.PreDockRuntimeTick ||
                    clip != OfficeSeatingAnimationClip.Work ||
                    actor.SeatContactErrorPx > MaximumSeatResidualPx ||
                    actor.R5eCurrentVelocityMagnitude > 0.0001f ||
                    actor.R5eLastActualDisplacementMagnitude > 0.0001f ||
                    actor.VisibleFrameMovementWorld > 0.0001f)
                    return Fail(93, trace.MemberId +
                        " did not enter the exact seated Work key pose in one motion0 atomic dock.");
                if (!CaptureSeatingFrameEvidence(
                        actor,
                        trace,
                        FrameEvidenceKind.AtomicSeat,
                        OfficeSeatingAnimationClip.Work,
                        0,
                        depth,
                        out string captureFailure))
                    return Fail(95, trace.MemberId + " atomic seat evidence failed: " + captureFailure);
                trace.AtomicSeatEvidenceCaptured = true;
                trace.SitCloseupCaptured = true;
                trace.EntryAtomicTick = actor.R5eAtomicPlacementTick;
            }
            else if (phase == OfficeRuntimeAgentPhase.Working)
            {
                if (clip != OfficeSeatingAnimationClip.Work ||
                    actor.SeatContactErrorPx > MaximumSeatResidualPx ||
                    actor.R5eAtomicPlacementTick != trace.EntryAtomicTick ||
                    actor.R5eLastActualDisplacementMagnitude > 0.0001f)
                    return Fail(93, trace.MemberId +
                        " corrected or popped after the classic atomic seated frame.");
                trace.AtomicSeatFollowupSampled = true;
            }

            if ((phase == OfficeRuntimeAgentPhase.Working ||
                  phase == OfficeRuntimeAgentPhase.FinishingWork) &&
                actor.IsOfficeWorkAnimationHookActive &&
                !string.IsNullOrWhiteSpace(actor.CurrentSpriteName))
            {
                trace.SawWorkHookActive = true;
                bool hasWorkMarker = actor.CurrentSpriteName.IndexOf(
                    "_sit_work_",
                    StringComparison.OrdinalIgnoreCase) >= 0;
                if (TryParseWorkFrameIndex(actor.CurrentSpriteName, out int workFrame))
                {
                    if (workFrame < 0 || workFrame >= 6 || workFrame != frame)
                        return Fail(
                            95,
                            $"{trace.MemberId} Work sprite/runtime frame mismatch: " +
                            $"sprite={actor.CurrentSpriteName} parsed={workFrame} runtime={frame}");
                    int workBit = 1 << workFrame;
                    string previousWorkSprite = trace.WorkSpriteNames[workFrame];
                    if (!string.IsNullOrEmpty(previousWorkSprite) &&
                        !string.Equals(
                            previousWorkSprite,
                            actor.CurrentSpriteName,
                            StringComparison.Ordinal))
                        return Fail(
                            95,
                            $"{trace.MemberId} Work frame {workFrame} mixed sprites " +
                            $"'{previousWorkSprite}' and '{actor.CurrentSpriteName}'.");
                    trace.WorkSpriteNames[workFrame] = actor.CurrentSpriteName;
                    trace.WorkHookSprites.Add(actor.CurrentSpriteName);
                    trace.DepthWorkHookSprites.Add(actor.CurrentSpriteName);
                    if ((trace.WorkEvidenceFrameMask & workBit) == 0)
                    {
                        if (workFrame != trace.NextExpectedWorkEvidenceFrame)
                            return Fail(
                                95,
                                $"{trace.MemberId} Work capture sequence skipped/reordered: " +
                                $"expected={trace.NextExpectedWorkEvidenceFrame} actual={workFrame}");
                        if (!CaptureSeatingFrameEvidence(
                                actor,
                                trace,
                                FrameEvidenceKind.Work,
                                OfficeSeatingAnimationClip.Work,
                                workFrame,
                                depth,
                                out string captureFailure))
                            return Fail(95, trace.MemberId + " Work evidence failed: " + captureFailure);
                        trace.WorkEvidenceFrameMask |= workBit;
                        trace.NextExpectedWorkEvidenceFrame++;
                        trace.WorkCloseupCaptured = true;
                    }
                }
                else if (hasWorkMarker)
                {
                    return Fail(
                        95,
                        $"{trace.MemberId} has an unparseable Work sprite: {actor.CurrentSpriteName}");
                }

                if (actor.CurrentOfficeWorkMicroAction == OfficeWorkMicroAction.Typing)
                {
                    trace.SawTypingMicroAction = true;
                    if (!TryParseTypingFrameIndex(actor.CurrentSpriteName, out int typingFrame) ||
                        typingFrame < 0 || typingFrame >= 6)
                    {
                        trace.RecordTypingDiagnostic(
                            $"unparseable sprite '{actor.CurrentSpriteName}'");
                    }
                    else
                    {
                        int bit = 1 << typingFrame;
                        string previousSprite = trace.TypingSpriteNames[typingFrame];
                        if (!string.IsNullOrEmpty(previousSprite) &&
                            !string.Equals(
                                previousSprite,
                                actor.CurrentSpriteName,
                                StringComparison.Ordinal))
                        {
                            trace.RecordTypingDiagnostic(
                                $"frame {typingFrame} mixed sprites '{previousSprite}' and " +
                                $"'{actor.CurrentSpriteName}'");
                        }
                        else if ((trace.TypingEvidenceFrameMask & bit) == 0)
                        {
                            trace.TypingSpriteNames[typingFrame] = actor.CurrentSpriteName;
                            if (typingFrame != trace.NextExpectedTypingEvidenceFrame)
                            {
                                trace.RecordTypingDiagnostic(
                                    $"capture sequence expected={trace.NextExpectedTypingEvidenceFrame} " +
                                    $"actual={typingFrame}");
                            }
                            else
                            {
                                trace.TypingEvidenceFrameMask |= bit;
                                trace.NextExpectedTypingEvidenceFrame++;
                            }
                        }
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

        private static bool IsForbiddenClassicTransitionSprite(string spriteName)
        {
            if (string.IsNullOrWhiteSpace(spriteName)) return false;
            return spriteName.IndexOf("sit_down", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   spriteName.IndexOf("sitdown", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   spriteName.IndexOf("stand_up", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   spriteName.IndexOf("standup", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool ObserveClassicFirstWalk(OfficeRuntimeAgent actor, ActorTrace trace)
        {
            if (!trace.SawLeavingSeat || trace.FirstWalkObserved ||
                !actor.HasCompletedSeatEgress) return true;
            Vector2 displacement = actor.Position - actor.LastCompletedSeatEgressWorld;
            if (displacement.magnitude <= OfficeRuntimeTraceCoordinator.StationaryEpsilon)
                return true;
            int direction = DirectionalSpriteAnimator.ResolveTileDirection(
                displacement,
                actor.R5eLastAtomicExitDirection);
            if (actor.Phase != OfficeRuntimeAgentPhase.Navigating ||
                actor.R5eRuntimeTick <= actor.R5eTurnCompleteTick ||
                direction != actor.R5eLastAtomicExitDirection)
                return Fail(
                    93,
                    $"{trace.MemberId} first walk was early or misdirected: " +
                    $"phase={actor.Phase} ticks={actor.R5eTurnCompleteTick}/" +
                    $"{actor.R5eRuntimeTick} direction=" +
                    $"{actor.R5eLastAtomicExitDirection}/{direction}");
            trace.FirstWalkObserved = true;
            trace.FirstWalkTick = actor.R5eRuntimeTick;
            trace.FirstWalkDirection = direction;
            return true;
        }

        private static bool ReadyForWorkEvidence(OfficeRuntimeAgent actor, ActorTrace trace)
        {
            return actor != null && actor.Phase == OfficeRuntimeAgentPhase.Working &&
                   trace.AtomicSeatEvidenceCaptured && trace.AtomicSeatFollowupSampled &&
                   actor.CurrentSeatingClip == OfficeSeatingAnimationClip.Work &&
                   actor.ObservedSitDownFrameCount == 0 &&
                   actor.ObservedStandUpFrameCount == 0 &&
                   actor.IsOfficeWorkAnimationHookActive &&
                   actor.ObservedWorkFrameCount == 6 &&
                   trace.WorkEvidenceFrameMask == 0x3f &&
                   trace.WorkHookSprites.Count == 6 && trace.DepthWorkHookSprites.Count == 6;
        }

        private static bool CompletedSeatExit(OfficeRuntimeAgent actor, ActorTrace trace)
        {
            return actor != null && trace.SawLeavingSeat && !actor.IsOccupyingSeat &&
                   actor.HasCompletedSeatEgress &&
                   actor.LastCompletedSeatEgressClearanceValid &&
                   actor.Phase == OfficeRuntimeAgentPhase.Idle &&
                   actor.R5eLastAtomicExitReservationBacked &&
                   actor.R5eLastAtomicExitTick > trace.EntryAtomicTick &&
                   actor.R5eTurnCompleteTick > actor.R5eLastAtomicExitTick &&
                   trace.FirstWalkObserved &&
                   trace.FirstWalkTick > actor.R5eTurnCompleteTick &&
                   trace.FirstWalkDirection == actor.R5eLastAtomicExitDirection &&
                   actor.ObservedSitDownFrameCount == 0 &&
                   actor.ObservedStandUpFrameCount == 0;
        }

        private bool ValidateFinalActor(
            OfficeRuntimeAgent actor,
            ActorTrace trace,
            out string failure)
        {
            var failures = new List<string>();
            if (!trace.SawApproachingSeat || !trace.SawAligningSeat ||
                !trace.SawRotatingToSeat || !trace.SawAlignedBeforeSitDown)
                failures.Add(
                    $"seatEntry={trace.SawApproachingSeat}/" +
                    $"{trace.SawAligningSeat}/{trace.SawRotatingToSeat}/" +
                    trace.SawAlignedBeforeSitDown);
            if (!actor.WasSeatFacingAlignedBeforeSitDown)
                failures.Add("seat-facing rotation was not confirmed before atomic dock");
            if (!trace.AtomicSeatEvidenceCaptured || !trace.AtomicSeatFollowupSampled ||
                trace.EntryAtomicTick <= trace.PreDockRuntimeTick ||
                actor.ObservedSitDownFrameCount != 0 ||
                actor.ObservedStandUpFrameCount != 0 ||
                trace.Phases.Contains(OfficeRuntimeAgentPhase.SittingDown) ||
                trace.Phases.Contains(OfficeRuntimeAgentPhase.StandingUp))
                failures.Add(
                    $"classicDock={trace.AtomicSeatEvidenceCaptured}/" +
                    $"{trace.AtomicSeatFollowupSampled} ticks={trace.PreDockRuntimeTick}/" +
                    $"{trace.EntryAtomicTick} clips={actor.ObservedSitDownFrameCount}/" +
                    actor.ObservedStandUpFrameCount);
            if (trace.SawWorkHookActive)
            {
                if (trace.WorkHookSprites.Count != 6 || trace.DepthWorkHookSprites.Count != 6 ||
                    trace.WorkEvidenceFrameMask != 0x3f || actor.ObservedWorkFrameCount != 6)
                    failures.Add(
                        $"workHook={trace.WorkHookSprites.Count}/" +
                        $"{trace.DepthWorkHookSprites.Count}/" +
                        $"{CountBits(trace.WorkEvidenceFrameMask)}/" +
                        actor.ObservedWorkFrameCount);
            }
            else
            {
                failures.Add("Work hook was not sampled");
            }
            if (!trace.SawFinishingWork) failures.Add("FinishingWork was not sampled");
            if (!trace.SawLeavingSeat || trace.LeavingSeatSampleCount == 0)
                failures.Add("LeavingSeat was not sampled");
            if (!actor.R5eLastAtomicExitReservationBacked ||
                actor.R5eLastAtomicExitTick <= trace.EntryAtomicTick)
                failures.Add(
                    $"reservationBeforeAtomicExit={actor.R5eLastAtomicExitReservationBacked} " +
                    $"ticks={trace.EntryAtomicTick}/{actor.R5eLastAtomicExitTick}");
            if (actor.R5eTurnCompleteTick <= actor.R5eLastAtomicExitTick ||
                !trace.FirstWalkObserved ||
                trace.FirstWalkTick <= actor.R5eTurnCompleteTick ||
                trace.FirstWalkDirection != actor.R5eLastAtomicExitDirection)
                failures.Add(
                    $"exitTurnFirstWalk={actor.R5eLastAtomicExitTick}/" +
                    $"{actor.R5eTurnCompleteTick}/{trace.FirstWalkTick} " +
                    $"observed={trace.FirstWalkObserved} direction=" +
                    $"{actor.R5eLastAtomicExitDirection}/{trace.FirstWalkDirection}");
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
            if (actor.MaxTransitionPelvisStepPx > 0.001f)
                failures.Add($"pelvisStep={actor.MaxTransitionPelvisStepPx:F3}px");
            if (actor.TransitionMonotonicViolationCount != 0)
                failures.Add("transitionReversals=" + actor.TransitionMonotonicViolationCount);
            if (actor.MaxAnimatedAnchorErrorPx > MaximumSeatResidualPx)
                failures.Add($"anchorError={actor.MaxAnimatedAnchorErrorPx:F3}px");
            if (actor.MaxTypingSeatContactErrorPx > MaximumSeatResidualPx)
                failures.Add($"typingSeat={actor.MaxTypingSeatContactErrorPx:F3}px");
            if (trace.MaximumLogicalRootErrorPx > 0.001f)
                failures.Add($"logicalRoot={trace.MaximumLogicalRootErrorPx:F6}px");
            if (trace.SeatCellMismatchCount != 0)
                failures.Add("seatCellMismatches=" + trace.SeatCellMismatchCount);
            if (Mathf.Abs(actor.AgentRadius - OfficeRuntimeAgent.DefaultRadius) > 0.000001f)
                failures.Add($"collisionRadius={actor.AgentRadius:F6}");
            if (actor.MaxChairPresentationStepPx > 0.001f)
                failures.Add($"chairPresentationStep={actor.MaxChairPresentationStepPx:F3}px");
            if (actor.VisualRotationErrorDegrees > 0.01f)
                failures.Add($"rotation={actor.VisualRotationErrorDegrees:F4}deg");
            if (actor.VisualScaleDeviation > 0.001f)
                failures.Add($"scaleDeviation={actor.VisualScaleDeviation:P3}");
            if (actor.IsOfficeSeatingFacingLocked)
                failures.Add("facing lock was not released after LeavingSeat");
            if (!trace.SitCloseupCaptured || !trace.WorkCloseupCaptured || !trace.StandCloseupCaptured)
                failures.Add("required 1024x1024 closeup is missing");
            int chairEvidenceCount = _frameEvidenceRecords.Count(record =>
                string.Equals(record.MemberId, trace.MemberId, StringComparison.Ordinal) &&
                record.Kind != FrameEvidenceKind.Typing);
            if (chairEvidenceCount != 7)
                failures.Add("primaryEvidence=" + chairEvidenceCount + "/7");
            if (trace.NextExpectedWorkEvidenceFrame != 6)
                failures.Add(
                    $"continuousWorkCapture={trace.NextExpectedWorkEvidenceFrame}/6");
            foreach (FrameEvidenceRecord record in _frameEvidenceRecords.Where(
                         item => string.Equals(item.MemberId, trace.MemberId, StringComparison.Ordinal) &&
                                 item.Kind != FrameEvidenceKind.Typing))
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
                if (record.Kind == FrameEvidenceKind.Work &&
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
            }
            if (trace.SitLowerBodyOccludedPixels <= 0 ||
                trace.WorkLowerBodyOccludedPixels <= 0)
                failures.Add(
                    $"classicSeatWorkLowerOcclusion={trace.SitLowerBodyOccludedPixels}/" +
                    trace.WorkLowerBodyOccludedPixels);

            failure = string.Join("; ", failures);
            return failures.Count == 0;
        }

        private string BuildTypingDiagnosticFailure()
        {
            var failures = new List<string>();
            foreach (string memberId in MemberIds)
            {
                if (!_traces.TryGetValue(memberId, out ActorTrace trace) ||
                    !trace.SawTypingMicroAction) continue;
                if (trace.TypingDiagnostic.Length != 0)
                    failures.Add(memberId + ":" + trace.TypingDiagnostic);
                else if (trace.TypingEvidenceFrameMask != 0x3f)
                    failures.Add($"{memberId}:incomplete mask=0x{trace.TypingEvidenceFrameMask:X2}");
            }
            return string.Join("; ", failures);
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
                trace.StandCloseupCaptured = true;
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
                    out SpriteRenderer authoredOverlay) || authoredOverlay == null)
            {
                failure = "required authored chair foreground is missing";
                return false;
            }
            _runtime.World.FurniturePresenter.OccupiedChairLowerBodyRenderers.TryGetValue(
                seat.ChairFurnitureId,
                out SpriteRenderer lowerBodyOverlay);
            if (depth.OcclusionEngaged &&
                (lowerBodyOverlay == null || !lowerBodyOverlay.enabled || authoredOverlay.enabled))
            {
                failure = "occupied chair must use only the lower-body rim foreground";
                return false;
            }
            if (!depth.OcclusionEngaged && !authoredOverlay.enabled)
            {
                failure = "released chair authored foreground is disabled";
                return false;
            }
            SpriteRenderer overlay = depth.OcclusionEngaged
                ? lowerBodyOverlay
                : authoredOverlay;
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
                FrameEvidenceKind.AtomicSeat => "atomic-seated-key-pose",
                FrameEvidenceKind.Work => "canonical-seated-work",
                FrameEvidenceKind.Typing => "typing-work-hook",
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
            string spriteToken = SanitizeFileToken(actor.CurrentSpriteName);
            string fileName =
                $"{stem}-{phaseToken}-frame-{evidenceFrame:D2}-{spriteToken}-" +
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
            bool previousLowerBodyEnabled =
                lowerBodyOverlay != null && lowerBodyOverlay.enabled;
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
                if (lowerBodyOverlay != null) lowerBodyOverlay.enabled = false;
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
                if (lowerBodyOverlay != null) lowerBodyOverlay.enabled = previousLowerBodyEnabled;
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
                if (lowerBodyOverlay != null)
                    lowerBodyOverlay.enabled = previousLowerBodyEnabled;
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
            if (end < 0) end = spriteName.Length;
            if (end <= start) return false;
            return int.TryParse(
                spriteName.Substring(start, end - start),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out frameIndex);
        }

        private static bool TryParseWorkFrameIndex(string spriteName, out int frameIndex)
        {
            frameIndex = -1;
            if (string.IsNullOrWhiteSpace(spriteName)) return false;
            const string marker = "_sit_work_";
            int start = spriteName.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (start < 0) return false;
            start += marker.Length;
            int end = spriteName.IndexOf('_', start);
            if (end < 0) end = spriteName.Length;
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
                int primaryActual = _frameEvidenceRecords.Count(record =>
                    record.Kind != FrameEvidenceKind.Typing);
                int optionalTypingActual = _frameEvidenceRecords.Count(record =>
                    record.Kind == FrameEvidenceKind.Typing);
                builder.AppendLine("primaryExpected=28 chair frames (AtomicSeat1 + Work6 per actor)");
                builder.AppendLine("primaryActual=" + primaryActual);
                builder.AppendLine("typingEvidence=diagnostic-only optionalActual=" + optionalTypingActual);
                builder.AppendLine("primaryResolution=1024x1024");
                builder.AppendLine("captureDeltaTime=0.016667 (fixed 60Hz presentation delta)");
                builder.AppendLine(
                    "colorDifference=abs(R1-R2)+abs(G1-G2)+abs(B1-B2)>=6");
                builder.AppendLine(
                    "lowerRegion=all actor-bound pixels at or below pose pelvis + 12 source sprite px");
                builder.AppendLine(
                    "upperProtectedRegion=all actor-bound pixels above max(pelvis+32 source px, hand-anchor height)");
                builder.AppendLine(
                    "handProtectedRegion=7 source-px radius around the approved Work pose hand anchor; strict for Typing 6/6, diagnostic for AtomicSeat");
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
                    "phaseAggregate=each member AtomicSeat/Work lowerOccluded sum must be >0");
                builder.AppendLine(
                    "invalidOcclusion=upper foreground overlap must be 0 in all 28 chair frames; Typing is diagnostic-only");
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
                    int chairEvidenceCount = _frameEvidenceRecords.Count(record =>
                        record.Kind != FrameEvidenceKind.Typing &&
                        string.Equals(record.MemberId, memberId, StringComparison.Ordinal));
                    string typingStatus = !trace.SawTypingMicroAction
                        ? "NOT_OBSERVED"
                        : trace.TypingDiagnostic.Length == 0 &&
                          trace.TypingEvidenceFrameMask == 0x3f
                            ? "PASS"
                            : "DIAGNOSTIC_FAIL";
                    builder.Append("coverage member=").Append(memberId)
                        .Append(" atomicSeat=").Append(trace.AtomicSeatEvidenceCaptured)
                        .Append(" workMask=0x").Append(trace.WorkEvidenceFrameMask.ToString("X2"))
                        .Append(" chairEvidence=").Append(chairEvidenceCount).Append("/7")
                        .Append(" typingStatus=").Append(typingStatus)
                        .Append(" typingMask=0x").Append(trace.TypingEvidenceFrameMask.ToString("X2"))
                        .Append(" continuousWork=").Append(trace.NextExpectedWorkEvidenceFrame)
                        .Append(" typingDiagnostic=").Append(
                            trace.TypingDiagnostic.Length == 0 ? "none" : trace.TypingDiagnostic)
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
            int primaryCaptureCount = _frameEvidenceRecords.Count(record =>
                record.Kind != FrameEvidenceKind.Typing);
            int primaryUniqueKeyCount = _frameEvidenceRecords
                .Where(record => record.Kind != FrameEvidenceKind.Typing)
                .Select(record => record.Key)
                .Distinct(StringComparer.Ordinal)
                .Count();
            bool observedTyping = _traces.Values.Any(trace => trace.SawTypingMicroAction);
            string typingDiagnosticFailure = BuildTypingDiagnosticFailure();
            builder.AppendLine("primaryCloseups=" + primaryCaptureCount + "/28");
            builder.AppendLine("primaryUniqueKeys=" + primaryUniqueKeyCount + "/28");
            builder.AppendLine("typingEvidence=diagnostic-only status=" +
                               (!observedTyping ? "NOT_OBSERVED" :
                                typingDiagnosticFailure.Length == 0 ? "PASS" : "DIAGNOSTIC_FAIL"));
            if (typingDiagnosticFailure.Length != 0)
                builder.AppendLine("typingDiagnostic=" + typingDiagnosticFailure);
            builder.AppendLine("safeEgressCloseups=" +
                               _traces.Values.Count(trace => trace.SafeEgressCloseupCaptured) + "/4");
            builder.AppendLine("egressMatrix=families4*rotations4*scenarios4=64");
            builder.AppendLine("seatReservationExclusive=PASS simultaneousActors=4");
            builder.AppendLine("captureManifest=seating-transition-frame-capture-manifest.txt");
            builder.AppendLine(
                "chairForeground=seat-rim-only; upperBodyProtection=pose-pelvis-split");
            builder.AppendLine(
                "furnitureTransformExact=true worldPositionPx=" +
                _maximumFurnitureWorldPositionErrorPx.ToString("F6", CultureInfo.InvariantCulture) +
                " worldRotationDegrees=" +
                _maximumFurnitureWorldRotationErrorDegrees.ToString("F6", CultureInfo.InvariantCulture) +
                " worldScale=" +
                _maximumFurnitureWorldScaleError.ToString("F9", CultureInfo.InvariantCulture));
            builder.AppendLine(
                "penetrationContract=filtered-core actor residual >5%=FAIL; <=5% allowed only for D3D11 bilinear/sRGB readback");
            foreach (string memberId in MemberIds)
            {
                if (!_traces.TryGetValue(memberId, out ActorTrace trace)) continue;
                OfficeRuntimeAgent actor = null;
                if (actors != null) actors.TryGetValue(memberId, out actor);
                builder.Append(memberId)
                    .Append(" direction=").Append(trace.ExpectedDirection)
                    .Append(" atomicSeat=").Append(trace.AtomicSeatEvidenceCaptured)
                    .Append('/').Append(trace.AtomicSeatFollowupSampled)
                    .Append(" workHook=").Append(trace.WorkHookSprites.Count).Append("/6")
                    .Append(" forbiddenClips=").Append(actor == null ? -1 :
                        actor.ObservedSitDownFrameCount + actor.ObservedStandUpFrameCount)
                    .Append(" workMask=0x").Append(trace.WorkEvidenceFrameMask.ToString("X2"))
                    .Append(" typingObserved=").Append(trace.SawTypingMicroAction)
                    .Append(" typingMask=0x").Append(trace.TypingEvidenceFrameMask.ToString("X2"))
                    .Append(" continuousWork=").Append(trace.NextExpectedWorkEvidenceFrame)
                    .Append(" chairEvidence=").Append(_frameEvidenceRecords.Count(record =>
                        record.Kind != FrameEvidenceKind.Typing &&
                        string.Equals(record.MemberId, memberId, StringComparison.Ordinal))).Append("/7")
                    .Append(" typingEvidence=").Append(_frameEvidenceRecords.Count(record =>
                        record.Kind == FrameEvidenceKind.Typing &&
                        string.Equals(record.MemberId, memberId, StringComparison.Ordinal)))
                    .Append(" typingDiagnostic=").Append(
                        trace.TypingDiagnostic.Length == 0 ? "none" : trace.TypingDiagnostic)
                    .Append(" lifecycleTicks=").Append(trace.PreDockRuntimeTick)
                    .Append('/').Append(trace.EntryAtomicTick)
                    .Append('/').Append(actor == null ? 0UL : actor.R5eLastAtomicExitTick)
                    .Append('/').Append(actor == null ? 0UL : actor.R5eTurnCompleteTick)
                    .Append('/').Append(trace.FirstWalkTick)
                    .Append(" lifecycleDirections=").Append(actor == null ? -1 :
                        actor.R5eLastAtomicExitDirection)
                    .Append('/').Append(trace.FirstWalkDirection)
                    .Append(" directionSamples=").Append(trace.DirectionSampleCount)
                    .Append(" leavingSamples=").Append(trace.LeavingSeatSampleCount)
                    .Append(" depthSamples=").Append(trace.DepthSampleCount)
                    .Append(" overlayChangedPixels=").Append(trace.OverlayChangedPixels)
                    .Append(" lowerCandidates=").Append(trace.LowerBodyOverlapCandidatePixels)
                    .Append(" lowerOccluded=").Append(trace.LowerBodyOccludedPixels)
                    .Append(" phaseLowerOccluded=").Append(trace.SitLowerBodyOccludedPixels)
                    .Append('/').Append(trace.WorkLowerBodyOccludedPixels)
                    .Append('/').Append(trace.TypingLowerBodyOccludedPixels)
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
                    .Append(" handResidualRangePx=")
                    .Append((trace.MaximumHandWorkErrorPx - trace.MinimumHandWorkErrorPx)
                        .ToString("F3", CultureInfo.InvariantCulture))
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
                        .Append(" logicalRootPx=")
                        .Append(trace.MaximumLogicalRootErrorPx.ToString("F6"))
                        .Append(" seatCellMismatches=").Append(trace.SeatCellMismatchCount)
                        .Append(" collisionRadius=").Append(actor.AgentRadius.ToString("F3"))
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
            AtomicSeat,
            Work,
            Typing
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
            public int TypingEvidenceFrameMask { get; set; }
            public int NextExpectedTypingEvidenceFrame { get; set; }
            public int WorkEvidenceFrameMask { get; set; }
            public int NextExpectedWorkEvidenceFrame { get; set; }
            public ulong PreDockRuntimeTick { get; set; }
            public Vector2 PreDockWorld { get; set; }
            public string PreDockSpriteName { get; set; } = string.Empty;
            public ulong EntryAtomicTick { get; set; }
            public bool AtomicSeatEvidenceCaptured { get; set; }
            public bool AtomicSeatFollowupSampled { get; set; }
            public bool FirstWalkObserved { get; set; }
            public ulong FirstWalkTick { get; set; }
            public int FirstWalkDirection { get; set; } = -1;
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
            public float MinimumHandWorkErrorPx { get; private set; } = float.PositiveInfinity;
            public float MaximumLogicalRootErrorPx { get; set; }
            public int SeatCellMismatchCount { get; set; }
            public int NoLowerBodyOverlapFrameCount { get; private set; }
            public int SitLowerBodyOccludedPixels { get; private set; }
            public int WorkLowerBodyOccludedPixels { get; private set; }
            public int TypingLowerBodyOccludedPixels { get; private set; }
            public bool SawApproachingSeat { get; set; }
            public bool SawAligningSeat { get; set; }
            public bool SawRotatingToSeat { get; set; }
            public bool SawAlignedBeforeSitDown { get; set; }
            public bool SawWorkHookActive { get; set; }
            public bool SawTypingMicroAction { get; set; }
            public bool SawFinishingWork { get; set; }
            public bool SawLeavingSeat { get; set; }
            public bool SafeEgressCloseupCaptured { get; set; }
            public int SafeEgressActorPixels { get; set; }
            public int SafeEgressChairPixels { get; set; }
            public int SafeEgressEmbeddedOverlapPixels { get; set; }
            public bool SitCloseupCaptured { get; set; }
            public bool WorkCloseupCaptured { get; set; }
            public bool StandCloseupCaptured { get; set; }
            public string[] TypingSpriteNames { get; } = new string[6];
            public string[] WorkSpriteNames { get; } = new string[6];
            public HashSet<OfficeRuntimeAgentPhase> Phases { get; } =
                new HashSet<OfficeRuntimeAgentPhase>();
            public HashSet<string> WorkHookSprites { get; } =
                new HashSet<string>(StringComparer.Ordinal);
            public HashSet<string> DepthWorkHookSprites { get; } =
                new HashSet<string>(StringComparer.Ordinal);
            public string TypingDiagnostic { get; private set; } = string.Empty;
            public HashSet<string> LoggedSamples { get; } =
                new HashSet<string>(StringComparer.Ordinal);

            public void RecordTypingDiagnostic(string failure)
            {
                if (TypingDiagnostic.Length == 0 && !string.IsNullOrWhiteSpace(failure))
                    TypingDiagnostic = failure;
            }

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
                MinimumHandWorkErrorPx = Mathf.Min(
                    MinimumHandWorkErrorPx,
                    evidence.HandWorkErrorPx);
                if (evidence.NoLowerBodyOverlapExpected) NoLowerBodyOverlapFrameCount++;
                switch (kind)
                {
                    case FrameEvidenceKind.AtomicSeat:
                        SitLowerBodyOccludedPixels += evidence.LowerBodyOccludedPixels;
                        break;
                    case FrameEvidenceKind.Work:
                        WorkLowerBodyOccludedPixels += evidence.LowerBodyOccludedPixels;
                        break;
                    case FrameEvidenceKind.Typing:
                        TypingLowerBodyOccludedPixels += evidence.LowerBodyOccludedPixels;
                        break;
                }
            }
        }

        private sealed class FurnitureTransformBaseline
        {
            private readonly Transform _semantic;
            private readonly Transform _semanticParent;
            private readonly Vector3 _semanticLocalPosition;
            private readonly Quaternion _semanticLocalRotation;
            private readonly Vector3 _semanticLocalScale;
            private readonly Vector3 _semanticWorldPosition;
            private readonly Quaternion _semanticWorldRotation;
            private readonly Vector3 _semanticWorldScale;
            private readonly Transform _visual;
            private readonly Transform _visualParent;
            private readonly Vector3 _visualLocalPosition;
            private readonly Quaternion _visualLocalRotation;
            private readonly Vector3 _visualLocalScale;
            private readonly Vector3 _visualWorldPosition;
            private readonly Quaternion _visualWorldRotation;
            private readonly Vector3 _visualWorldScale;

            public FurnitureTransformBaseline(
                string kindId,
                Transform semantic,
                Transform visual)
            {
                KindId = kindId ?? string.Empty;
                _semantic = semantic;
                _semanticParent = semantic.parent;
                _semanticLocalPosition = semantic.localPosition;
                _semanticLocalRotation = semantic.localRotation;
                _semanticLocalScale = semantic.localScale;
                _semanticWorldPosition = semantic.position;
                _semanticWorldRotation = semantic.rotation;
                _semanticWorldScale = semantic.lossyScale;
                _visual = visual;
                _visualParent = visual.parent;
                _visualLocalPosition = visual.localPosition;
                _visualLocalRotation = visual.localRotation;
                _visualLocalScale = visual.localScale;
                _visualWorldPosition = visual.position;
                _visualWorldRotation = visual.rotation;
                _visualWorldScale = visual.lossyScale;
            }

            public string KindId { get; }

            public float WorldPositionErrorPx(Camera camera)
            {
                if (_semantic == null || _visual == null) return float.PositiveInfinity;
                if (camera == null)
                    return Mathf.Max(
                        Vector3.Distance(_semantic.position, _semanticWorldPosition),
                        Vector3.Distance(_visual.position, _visualWorldPosition)) *
                           OfficeGridTilemapPresenter.PixelsPerUnit;
                return Mathf.Max(
                    OfficeGridAlignmentMetrics.ScreenDistance(
                        camera, _semantic.position, _semanticWorldPosition),
                    OfficeGridAlignmentMetrics.ScreenDistance(
                        camera, _visual.position, _visualWorldPosition));
            }

            public float WorldRotationErrorDegrees()
            {
                if (_semantic == null || _visual == null) return float.PositiveInfinity;
                return Mathf.Max(
                    Quaternion.Angle(_semantic.rotation, _semanticWorldRotation),
                    Quaternion.Angle(_visual.rotation, _visualWorldRotation));
            }

            public float WorldScaleError()
            {
                if (_semantic == null || _visual == null) return float.PositiveInfinity;
                return Mathf.Max(
                    Vector3.Distance(_semantic.lossyScale, _semanticWorldScale),
                    Vector3.Distance(_visual.lossyScale, _visualWorldScale));
            }

            public bool MatchesExactly()
            {
                return _semantic != null && _visual != null &&
                       _semantic.parent == _semanticParent &&
                       _visual.parent == _visualParent &&
                       _semantic.localPosition.Equals(_semanticLocalPosition) &&
                       _semantic.localRotation.Equals(_semanticLocalRotation) &&
                       _semantic.localScale.Equals(_semanticLocalScale) &&
                       _semantic.position.Equals(_semanticWorldPosition) &&
                       _semantic.rotation.Equals(_semanticWorldRotation) &&
                       _semantic.lossyScale.Equals(_semanticWorldScale) &&
                       _visual.localPosition.Equals(_visualLocalPosition) &&
                       _visual.localRotation.Equals(_visualLocalRotation) &&
                       _visual.localScale.Equals(_visualLocalScale) &&
                       _visual.position.Equals(_visualWorldPosition) &&
                       _visual.rotation.Equals(_visualWorldRotation) &&
                       _visual.lossyScale.Equals(_visualWorldScale);
            }
        }
    }
}
