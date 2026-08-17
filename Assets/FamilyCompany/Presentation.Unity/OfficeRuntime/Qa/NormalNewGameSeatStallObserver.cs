using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using FamilyCompany.Simulation.Navigation;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime.Qa
{
    /// <summary>
    /// Observer-only player gate for the public new-game flow. It does not take QA control of an
    /// actor, inject a route, jump the clock, alter docking, or force a seating transition.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NormalNewGameSeatStallObserver : MonoBehaviour
    {
        public const string CommandLineFlag = "-familyCompanyNormalSeatStallObserver";
        public const string EmptyOfficeCommandLineFlag = "-familyCompanyNormalEmptyOfficeObserver";
        public const string ArtifactDirectoryArgument = "-familyCompanyNormalSeatStallArtifacts";
        public const string SpeedArgument = "-familyCompanyNormalSeatStallSpeed";
        public const string NoCaptureCommandLineFlag = "-familyCompanyNormalSeatStallNoCapture";
        public const string BurstCaptureCommandLineFlag =
            "-familyCompanyNormalSeatStallBurstCapture";
        public const string DelaySpeedUntilAttendanceCommandLineFlag =
            "-familyCompanyNormalSeatStallDelaySpeedUntilAttendance";

        private static readonly string[] MemberIds =
            { "player", "older_sister", "father", "mother" };
        private static readonly HashSet<long> CaptureMinutes =
            new HashSet<long>
            {
                5L, 10L, 14L,
                15L, 16L, 17L, 18L, 19L, 20L, 21L, 22L, 23L, 24L, 25L, 26L,
                27L, 28L, 29L, 30L, 31L, 32L,
                33L, 34L, 35L, 36L, 37L, 38L, 39L, 40L, 41L, 42L, 43L, 44L,
                60L
            };
        private static NormalNewGameSeatStallObserver _instance;

        private readonly StringBuilder _trace = new StringBuilder(32768);
        private readonly StringBuilder _events = new StringBuilder(8192);
        private readonly Dictionary<string, ActorObservation> _observations =
            new Dictionary<string, ActorObservation>(StringComparer.Ordinal);
        private readonly HashSet<long> _capturedMinutes = new HashSet<long>();
        private string _artifactDirectory = string.Empty;
        private string _firstFailure = string.Empty;
        private float _requestedSpeed = 1f;
        private bool _captureEnabled;
        private bool _burstCaptureEnabled;
        private float _nextBurstCaptureRealtime;
        private int _burstCaptureCount;
        private bool _delaySpeedUntilAttendance;
        private bool _requestedSpeedApplied;
        private bool _emptyOfficeMode;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => _instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInstall()
        {
            if (_instance != null ||
                (!HasCommandLineFlag(CommandLineFlag) &&
                 !HasCommandLineFlag(EmptyOfficeCommandLineFlag))) return;
            var host = new GameObject("~NormalNewGameSeatStallObserver");
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<NormalNewGameSeatStallObserver>();
        }

        private void Start()
        {
            _artifactDirectory = ResolveArtifactDirectory();
            _requestedSpeed = ResolveSpeed();
            _emptyOfficeMode = HasCommandLineFlag(EmptyOfficeCommandLineFlag);
            _captureEnabled = !HasCommandLineFlag(NoCaptureCommandLineFlag);
            _burstCaptureEnabled = HasCommandLineFlag(BurstCaptureCommandLineFlag);
            _delaySpeedUntilAttendance = HasCommandLineFlag(DelaySpeedUntilAttendanceCommandLineFlag);
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
                    Finish(90, "unhandled=" + exception.GetType().Name + ":" + exception.Message, null, null);
                    yield break;
                }
                yield return yielded;
            }
        }

        private IEnumerator Run()
        {
            Directory.CreateDirectory(_artifactDirectory);
            Debug.Log(
                (_emptyOfficeMode
                    ? "FAMILY_COMPANY_NORMAL_EMPTY_OFFICE_OBSERVER: START | observerOnly=true"
                    : "FAMILY_COMPANY_NORMAL_SEAT_STALL_OBSERVER: START | observerOnly=true") +
                " | actorQaControl=false | routeInjection=false | clockJump=false | dockingForce=false" +
                " | requestedSpeed=" + _requestedSpeed.ToString("0", CultureInfo.InvariantCulture) +
                " | artifacts=" + _artifactDirectory);
            yield return null;

            PrototypeBootstrap bootstrap = Object.FindFirstObjectByType<PrototypeBootstrap>();
            if (bootstrap == null)
            {
                Finish(91, "PrototypeBootstrap missing", null, null);
                yield break;
            }

            ScenePreviewJump.ShowStarterOffice();
            bootstrap.StartNewGameNow(1, false);
            ScenePreviewJump.ShowStarterOffice();

            float readyDeadline = Time.realtimeSinceStartup + 30f;
            StarterOfficeRuntimeBootstrap runtime = null;
            while (Time.realtimeSinceStartup < readyDeadline)
            {
                runtime = Object.FindFirstObjectByType<StarterOfficeRuntimeBootstrap>();
                if (runtime != null && runtime.IsReady && runtime.World != null &&
                    runtime.Actors.Count == MemberIds.Length && bootstrap.State != null) break;
                yield return null;
            }
            if (runtime == null || !runtime.IsReady || runtime.World == null ||
                runtime.Actors.Count != MemberIds.Length || bootstrap.State == null)
            {
                Finish(92, "normal runtime did not become ready with four actors", bootstrap, runtime);
                yield break;
            }

            Dictionary<string, OfficeRuntimeAgent> actors = runtime.Actors
                .Where(actor => actor != null)
                .ToDictionary(actor => actor.AgentId, actor => actor, StringComparer.Ordinal);
            if (MemberIds.Any(memberId => !actors.ContainsKey(memberId)))
            {
                Finish(92, "canonical actor set incomplete", bootstrap, runtime);
                yield break;
            }

            if (_emptyOfficeMode)
            {
                int editableFurniture = runtime.World.Grid.Furniture.Count(item =>
                    OfficeFurnitureCatalog.Find(item.KindId)?.IsPlayerEditable == true);
                if (runtime.World.Grid.SeatSlots.Count != 0 || editableFurniture != 0 ||
                    bootstrap.State.OfficeFurnitureInventory.Instances.Count != 0)
                {
                    Finish(
                        94,
                        "normal new game was not empty:seats=" + runtime.World.Grid.SeatSlots.Count +
                        ";editable=" + editableFurniture +
                        ";inventory=" + bootstrap.State.OfficeFurnitureInventory.Instances.Count,
                        bootstrap,
                        runtime);
                    yield break;
                }
            }

            foreach (string memberId in MemberIds)
                _observations.Add(memberId, new ActorObservation(actors[memberId], bootstrap.State.Time.ElapsedMinutes));

            bootstrap.SetWorldTimeScaleNow(_delaySpeedUntilAttendance ? 1f : _requestedSpeed);
            _requestedSpeedApplied = !_delaySpeedUntilAttendance;
            _trace.AppendLine(
                "elapsedMinute,clock,frame,timeScale,worldScale,worldDelta,worldUnscaledDelta,motionDelta," +
                "actor,tick,phase,activity,position,destination,pendingDestination,autonomyIntent," +
                "autonomyDestination,destinationCell,emptyWander,pathCount,pathIndex," +
                "currentDirection,requestedDirection,motionDirection,locomotionPhase,gaitPhase,walkFrame," +
                "actualDisplacement,semanticDisplacement,facingErrorDegrees,visualRootOffset," +
                "seatId,claimOccupied,claimReleased," +
                "attendanceArrivals,workFrames,seatingClip,seatingFrame,sprite,interactionPhase," +
                "occupancyCell,occupancyPresent,occupancyReservations,occupancyEpoch,occupancyRevision," +
                "dockingPlan,lastReleaseReason,lastReleaseTick,stuckSeconds,reservationBlocker,movementBlocker");

            long lastMinute = long.MinValue;
            float wallDeadline = Time.realtimeSinceStartup + 180f;
            while (bootstrap.State.Time.ElapsedMinutes < 60L && Time.realtimeSinceStartup < wallDeadline)
            {
                yield return new WaitForEndOfFrame();
                long minute = bootstrap.State.Time.ElapsedMinutes;
                bool minuteChanged = minute != lastMinute;
                lastMinute = minute;

                string[] duplicateSeats = actors.Values
                    .Where(actor => actor.ActiveSeatId.Length > 0)
                    .GroupBy(actor => actor.ActiveSeatId, StringComparer.Ordinal)
                    .Where(group => group.Count() > 1)
                    .Select(group => group.Key)
                    .ToArray();
                if (duplicateSeats.Length > 0)
                    RecordFirstFailure(
                        minute,
                        actors.Values.First(actor => string.Equals(
                            actor.ActiveSeatId,
                            duplicateSeats[0],
                            StringComparison.Ordinal)),
                        "duplicate-seat-claim:" + duplicateSeats[0]);

                foreach (string memberId in MemberIds)
                {
                    OfficeRuntimeAgent actor = actors[memberId];
                    ActorObservation observation = _observations[memberId];
                    string signature = Signature(actor);
                    bool transition = !string.Equals(signature, observation.LastSignature, StringComparison.Ordinal);
                    if (minute >= 5L && (minuteChanged || transition)) AppendSnapshot(bootstrap, runtime, actor);
                    ObserveActor(minute, runtime, actor, observation);
                    observation.LastSignature = signature;
                }
                if (_emptyOfficeMode) ObserveEmptyOfficeCrowd(minute, runtime, actors);

                if (!_requestedSpeedApplied &&
                    MemberIds.All(memberId => _observations[memberId].CompletedFirstWorkLoop))
                {
                    bootstrap.SetWorldTimeScaleNow(_requestedSpeed);
                    _requestedSpeedApplied = true;
                    Debug.Log(
                        "FAMILY_COMPANY_NORMAL_SEAT_SPEED_APPLIED | clock=" +
                        bootstrap.State.Time.Now.ToString("HH:mm", CultureInfo.InvariantCulture) +
                        " | speed=" + _requestedSpeed.ToString("0", CultureInfo.InvariantCulture) +
                        " | afterAttendanceWorkLoops=4");
                }

                if (_captureEnabled && minute >= 5L && CaptureMinutes.Contains(minute) &&
                    _capturedMinutes.Add(minute))
                {
                    string clockToken = bootstrap.State.Time.Now.ToString("HHmm", CultureInfo.InvariantCulture);
                    string capturePath = ArtifactPath("normal-new-game-" + clockToken + ".png");
                    if (!TryCaptureOverview(capturePath, out string captureFailure))
                        RecordFirstFailure(minute, actors["player"], "capture-failed:" + captureFailure);
                }
                if (_captureEnabled && _burstCaptureEnabled && minute >= 9L && minute <= 15L &&
                    Time.realtimeSinceStartup >= _nextBurstCaptureRealtime)
                {
                    _nextBurstCaptureRealtime = Time.realtimeSinceStartup + 0.1f;
                    string clockToken = bootstrap.State.Time.Now.ToString("HHmm", CultureInfo.InvariantCulture);
                    string capturePath = ArtifactPath(
                        "normal-walk-burst-" + clockToken + "-" +
                        _burstCaptureCount.ToString("D4", CultureInfo.InvariantCulture) + ".png");
                    if (!TryCaptureOverview(capturePath, out string captureFailure))
                        RecordFirstFailure(minute, actors["father"], "burst-capture-failed:" + captureFailure);
                    else
                        _burstCaptureCount++;
                }
            }

            if (bootstrap.State.Time.ElapsedMinutes < 60L)
                RecordFirstFailure(
                    bootstrap.State.Time.ElapsedMinutes,
                    actors["player"],
                    "clock-timeout-before-09:50");

            foreach (string memberId in MemberIds)
            {
                OfficeRuntimeAgent actor = actors[memberId];
                if (_emptyOfficeMode)
                {
                    ActorObservation observation = _observations[memberId];
                    if (!_observations[memberId].EnteredEmptyOffice)
                        RecordFirstFailure(
                            bootstrap.State.Time.ElapsedMinutes,
                            actor,
                            "attendance-never-entered-empty-office");
                    else if (actor.IsPresentationAway || actor.Phase == OfficeRuntimeAgentPhase.Outside)
                        RecordFirstFailure(
                            bootstrap.State.Time.ElapsedMinutes,
                            actor,
                            "attendance-left-empty-office-before-09:50");
                    if (observation.WanderSelections < 2)
                        RecordFirstFailure(
                            bootstrap.State.Time.ElapsedMinutes,
                            actor,
                            "insufficient-empty-office-wander-selections:" + observation.WanderSelections);
                    if (observation.ValidWalkLoops < 2)
                        RecordFirstFailure(
                            bootstrap.State.Time.ElapsedMinutes,
                            actor,
                            "insufficient-valid-walk-loops:" + observation.ValidWalkLoops);
                    if (observation.CurrentLookSelections != 0 ||
                        observation.DestinationlessIdleSelections != 0 ||
                        observation.SameCellDestinationSelections != 0 ||
                        observation.DuplicatePivotEpisodes != 0 ||
                        observation.TranslationBeforePivotFrames != 0 ||
                        observation.DirectionDisplacementMismatchFrames != 0 ||
                        observation.NonCardinalSegmentFrames != 0 ||
                        observation.CollisionProjectedFrames != 0 ||
                        observation.ActorOverlapFrames != 0 ||
                        observation.DuplicateDestinationFrames != 0 ||
                        observation.TileCenterDeviationCount != 0 ||
                        observation.NonzeroVisualRootOffsetFrames != 0)
                    {
                        RecordFirstFailure(
                            bootstrap.State.Time.ElapsedMinutes,
                            actor,
                            "empty-office-motion-invariant:" + ObservationMetrics(observation));
                    }
                    if (observation.LongestStationaryMinutes >= 20L)
                        RecordFirstFailure(
                            bootstrap.State.Time.ElapsedMinutes,
                            actor,
                            "stationary-20-game-minutes:" + observation.LongestStationaryMinutes);
                }
                else if (actor.AttendanceSeatArrivalCount < 1)
                    RecordFirstFailure(bootstrap.State.Time.ElapsedMinutes, actor, "attendance-never-atomically-seated");
                else if (actor.ObservedWorkFrameCount < 6)
                    RecordFirstFailure(
                        bootstrap.State.Time.ElapsedMinutes,
                        actor,
                        "incomplete-work-loop:frames=" + actor.ObservedWorkFrameCount);
            }
            if (!_requestedSpeedApplied)
                RecordFirstFailure(
                    bootstrap.State.Time.ElapsedMinutes,
                    actors["player"],
                    "requested-speed-was-not-applied");
            if (_emptyOfficeMode)
            {
                int totalWalkLoops = _observations.Values.Sum(item => item.ValidWalkLoops);
                int totalStationaryTransitions =
                    _observations.Values.Sum(item => item.StationaryDirectionTransitions);
                if (totalWalkLoops <= totalStationaryTransitions)
                    RecordFirstFailure(
                        bootstrap.State.Time.ElapsedMinutes,
                        actors["player"],
                        "walk-loops-not-above-stationary-transitions:walk=" + totalWalkLoops +
                        ";stationary=" + totalStationaryTransitions);
                OfficeAutonomyCoordinator coordinator =
                    Object.FindFirstObjectByType<OfficeAutonomyCoordinator>();
                if (coordinator == null || coordinator.EmptyOfficeWanderSelectionCount < 8)
                    RecordFirstFailure(
                        bootstrap.State.Time.ElapsedMinutes,
                        actors["player"],
                        "coordinator-wander-selections=" +
                        (coordinator == null ? -1 : coordinator.EmptyOfficeWanderSelectionCount));
            }

            int exitCode = _firstFailure.Length == 0 ? 0 : 21;
            Finish(
                exitCode,
                exitCode == 0
                    ? (_emptyOfficeMode
                        ? "normal-new-game-empty-office-arrival-stall-zero"
                        : "normal-new-game-seat-stall-zero")
                    : _firstFailure,
                bootstrap,
                runtime);
        }

        private void ObserveActor(
            long minute,
            StarterOfficeRuntimeBootstrap runtime,
            OfficeRuntimeAgent actor,
            ActorObservation observation)
        {
            if (_emptyOfficeMode && minute >= 10L && !observation.EnteredEmptyOffice &&
                !actor.IsPresentationAway && actor.Phase != OfficeRuntimeAgentPhase.Outside)
            {
                observation.EnteredEmptyOffice = true;
                observation.LastMovingMinute = minute;
                AppendEvent(minute, actor, "EMPTY_OFFICE_ATTENDANCE_ENTERED");
            }

            if (actor.AttendanceSeatArrivalCount > observation.ArrivalCount)
            {
                observation.ArrivalCount = actor.AttendanceSeatArrivalCount;
                observation.AwaitingFirstWorkLoop = true;
                observation.SeatArrivalTick = actor.R5eRuntimeTick;
                AppendEvent(minute, actor, "ATOMIC_ATTENDANCE_SEAT");
            }

            if (observation.AwaitingFirstWorkLoop)
            {
                if (actor.ObservedWorkFrameCount >= 6)
                {
                    observation.AwaitingFirstWorkLoop = false;
                    observation.CompletedFirstWorkLoop = true;
                    AppendEvent(minute, actor, "FIRST_WORK_LOOP_COMPLETE");
                }
                else if (actor.Phase != OfficeRuntimeAgentPhase.Working)
                {
                    string reason = actor.DiagnosticLastSeatReleaseRequestReason.Length == 0
                        ? "unknown-seat-release"
                        : actor.DiagnosticLastSeatReleaseRequestReason;
                    RecordFirstFailure(
                        minute,
                        actor,
                        "left-atomic-seat-before-work-loop:frames=" + actor.ObservedWorkFrameCount +
                        ";reason=" + reason +
                        ";releaseTick=" + actor.DiagnosticLastSeatReleaseRequestTick);
                    observation.AwaitingFirstWorkLoop = false;
                }
            }

            string progress = ProgressSignature(actor);
            bool moved = Vector2.Distance(actor.Position, observation.LastPosition) > 0.005f;
            if (moved || !string.Equals(progress, observation.LastProgress, StringComparison.Ordinal))
            {
                observation.LastProgressMinute = minute;
                observation.LastProgress = progress;
                observation.LastPosition = actor.Position;
            }

            bool idleIntentHasDestination =
                !actor.IsPlayerControlled &&
                actor.Phase == OfficeRuntimeAgentPhase.Idle &&
                actor.DiagnosticAutonomyIntentId.Length > 0 &&
                (actor.DiagnosticAutonomyDestinationId.Length > 0 ||
                 actor.DiagnosticDestinationId.Length > 0 ||
                 actor.DiagnosticPendingDestinationId.Length > 0 ||
                 actor.SemanticPathLength > 0);
            bool transitionShouldProgress = actor.Phase == OfficeRuntimeAgentPhase.Navigating ||
                                            actor.IsEnteringSeat ||
                                            actor.Phase == OfficeRuntimeAgentPhase.SittingDown ||
                                            actor.Phase == OfficeRuntimeAgentPhase.FinishingWork ||
                                            actor.Phase == OfficeRuntimeAgentPhase.StandingUp ||
                                            actor.Phase == OfficeRuntimeAgentPhase.LeavingSeat ||
                                            idleIntentHasDestination;
            if (transitionShouldProgress && minute - observation.LastProgressMinute >= 20L)
            {
                RecordFirstFailure(
                    minute,
                    actor,
                    "no-transition-progress-20-game-minutes:phase=" + actor.Phase +
                    ";destination=" + actor.DiagnosticDestinationId +
                    ";pending=" + actor.DiagnosticPendingDestinationId +
                    ";path=" + actor.DiagnosticPathIndex + "/" + actor.SemanticPathLength +
                    ";releaseReason=" + actor.DiagnosticLastSeatReleaseRequestReason);
                observation.LastProgressMinute = minute;
            }

            DirectionalLocomotionFrameTrace locomotion = actor.CaptureLocomotionFrameTrace();
            bool moving = locomotion.IsMoving &&
                          locomotion.ActualDisplacement.sqrMagnitude > 0.0000001f;
            int gaitCycle = Mathf.FloorToInt(actor.GaitDistance / Mathf.Max(0.000001f, actor.StrideLength));
            if (moving)
            {
                observation.LastMovingMinute = minute;
                observation.MovingFrames++;
                if (gaitCycle > observation.LastGaitCycle)
                    observation.ValidWalkLoops += gaitCycle - observation.LastGaitCycle;
                observation.MovedSinceLastPivot = true;
                if (observation.LastMotionDirection >= 0 &&
                    observation.LastMotionDirection != locomotion.MotionDirection &&
                    !observation.SawStationarySinceLastMotion)
                    observation.TranslationBeforePivotFrames++;
                observation.LastMotionDirection = locomotion.MotionDirection;
                observation.SawStationarySinceLastMotion = false;

                if (locomotion.DisplayDirection != locomotion.MotionDirection ||
                    actor.CurrentDirection != locomotion.MotionDirection)
                    observation.DirectionDisplacementMismatchFrames++;

                if (locomotion.ActualDisplacement.sqrMagnitude > 0.00000001f &&
                    !IsCardinalTileDisplacement(runtime, locomotion.ActualDisplacement))
                    observation.NonCardinalSegmentFrames++;
                observation.MaximumFrameDisplacement = Mathf.Max(
                    observation.MaximumFrameDisplacement,
                    locomotion.ActualDisplacement.magnitude);
            }
            else
            {
                if (observation.EnteredEmptyOffice)
                    observation.LongestStationaryMinutes = Math.Max(
                        observation.LongestStationaryMinutes,
                        minute - observation.LastMovingMinute);
                observation.SawStationarySinceLastMotion = true;
                if (actor.CurrentDirection != observation.LastDisplayDirection)
                    observation.StationaryDirectionTransitions++;
            }
            observation.LastGaitCycle = gaitCycle;
            observation.LastDisplayDirection = actor.CurrentDirection;

            bool pivoting = !moving && locomotion.Phase == OfficeLocomotionPhase.Pivot;
            if (pivoting && !observation.WasPivoting)
            {
                OfficeGridCoordinate pivotCell = actor.CurrentCell;
                if (observation.HasPivotEpisode && !observation.MovedSinceLastPivot &&
                    pivotCell.Equals(observation.LastPivotCell))
                    observation.DuplicatePivotEpisodes++;
                observation.HasPivotEpisode = true;
                observation.PivotEpisodes++;
                observation.LastPivotCell = pivotCell;
                observation.MovedSinceLastPivot = false;
            }
            observation.WasPivoting = pivoting;

            if (actor.WasCollisionProjected) observation.CollisionProjectedFrames++;
            if (actor.LocomotionVisualFootPlantOffsetWorld.sqrMagnitude > 0.00000001f)
                observation.NonzeroVisualRootOffsetFrames++;

            string autonomySelection = actor.DiagnosticAutonomyIntentId + "|" +
                                       actor.DiagnosticAutonomyDestinationId + "|" +
                                       actor.DiagnosticDestinationId;
            if (!string.Equals(
                    autonomySelection,
                    observation.LastAutonomySelection,
                    StringComparison.Ordinal))
            {
                if (autonomySelection.IndexOf("current-look", StringComparison.Ordinal) >= 0)
                    observation.CurrentLookSelections++;
                bool destinationlessIdle = actor.Phase == OfficeRuntimeAgentPhase.Idle &&
                                           actor.DiagnosticAutonomyIntentId.Length > 0 &&
                                           actor.DiagnosticAutonomyDestinationId.Length == 0 &&
                                           actor.DiagnosticDestinationId.Length == 0 &&
                                           actor.DiagnosticPendingDestinationId.Length == 0 &&
                                           actor.SemanticPathLength == 0;
                if (destinationlessIdle) observation.DestinationlessIdleSelections++;
                if (actor.DiagnosticDestinationId.StartsWith(
                        "empty-office-wander:",
                        StringComparison.Ordinal))
                {
                    observation.WanderSelections++;
                    if (actor.DiagnosticDestinationCell.HasValue &&
                        actor.DiagnosticDestinationCell.Value.Equals(actor.CurrentCell))
                        observation.SameCellDestinationSelections++;
                    float currentError = DistanceToNearestCellCenter(runtime, actor.Position);
                    float previousError = DistanceToNearestCellCenter(
                        runtime,
                        observation.PreviousFramePosition);
                    float departureError = Mathf.Min(currentError, previousError);
                    observation.MaximumTileCenterError = Mathf.Max(
                        observation.MaximumTileCenterError,
                        departureError);
                    if (departureError > 0.0001f) observation.TileCenterDeviationCount++;
                }
                observation.LastAutonomySelection = autonomySelection;
            }

            if (observation.LastRuntimePhase == OfficeRuntimeAgentPhase.Navigating &&
                actor.Phase == OfficeRuntimeAgentPhase.Idle)
            {
                float arrivalError = DistanceToNearestCellCenter(runtime, actor.Position);
                observation.MaximumTileCenterError = Mathf.Max(
                    observation.MaximumTileCenterError,
                    arrivalError);
                if (arrivalError > 0.0001f) observation.TileCenterDeviationCount++;
            }
            observation.LastRuntimePhase = actor.Phase;
            observation.PreviousFramePosition = actor.Position;
        }

        private void ObserveEmptyOfficeCrowd(
            long minute,
            StarterOfficeRuntimeBootstrap runtime,
            IReadOnlyDictionary<string, OfficeRuntimeAgent> actors)
        {
            if (minute < 10L) return;
            OfficeRuntimeAgent[] present = actors.Values
                .Where(actor => !actor.IsPresentationAway &&
                                actor.Phase != OfficeRuntimeAgentPhase.Outside)
                .ToArray();
            for (int left = 0; left < present.Length; left++)
            for (int right = left + 1; right < present.Length; right++)
            {
                if (Vector2.Distance(present[left].Position, present[right].Position) + 0.0001f >=
                    present[left].AgentRadius + present[right].AgentRadius) continue;
                _observations[present[left].AgentId].ActorOverlapFrames++;
                _observations[present[right].AgentId].ActorOverlapFrames++;
            }

            var duplicateTargets = present
                .Where(actor => actor.DiagnosticEmptyOfficeWanderActive &&
                                actor.DiagnosticDestinationCell.HasValue)
                .GroupBy(actor => actor.DiagnosticDestinationCell.Value)
                .Where(group => group.Count() > 1)
                .ToArray();
            foreach (var duplicate in duplicateTargets)
                foreach (OfficeRuntimeAgent actor in duplicate)
                    _observations[actor.AgentId].DuplicateDestinationFrames++;
        }

        private static float DistanceToNearestCellCenter(
            StarterOfficeRuntimeBootstrap runtime,
            Vector2 position)
        {
            OfficeGridCoordinate cell = runtime.World.Presenter.NearestCell(position);
            Vector3 center = runtime.World.Presenter.CellCenterWorld(cell);
            return Vector2.Distance(position, new Vector2(center.x, center.y));
        }

        private static bool IsCardinalTileDisplacement(
            StarterOfficeRuntimeBootstrap runtime,
            Vector2 displacement)
        {
            Vector3 origin3 = runtime.World.Presenter.CellCenterWorld(new OfficeGridCoordinate(1, 1));
            Vector3 x3 = runtime.World.Presenter.CellCenterWorld(new OfficeGridCoordinate(2, 1));
            Vector3 y3 = runtime.World.Presenter.CellCenterWorld(new OfficeGridCoordinate(1, 2));
            Vector2 direction = displacement.normalized;
            Vector2 x = new Vector2(x3.x - origin3.x, x3.y - origin3.y).normalized;
            Vector2 y = new Vector2(y3.x - origin3.x, y3.y - origin3.y).normalized;
            float alignment = Mathf.Max(
                Mathf.Abs(Vector2.Dot(direction, x)),
                Mathf.Abs(Vector2.Dot(direction, y)));
            return alignment >= 0.999f;
        }

        private void AppendSnapshot(
            PrototypeBootstrap bootstrap,
            StarterOfficeRuntimeBootstrap runtime,
            OfficeRuntimeAgent actor)
        {
            OfficeRuntimeOccupancy.CanonicalActorSnapshot occupancy =
                runtime.World.Occupancy.CaptureCanonicalActorSnapshot(actor.AgentId);
            _trace.Append(bootstrap.State.Time.ElapsedMinutes).Append(',')
                .Append(bootstrap.State.Time.Now.ToString("HH:mm", CultureInfo.InvariantCulture)).Append(',')
                .Append(Time.frameCount).Append(',')
                .Append(Format(Time.timeScale)).Append(',')
                .Append(Format(bootstrap.WorldTimeScale)).Append(',')
                .Append(Format(runtime.World.LastFrameDeltaTime)).Append(',')
                .Append(Format(runtime.World.LastUnscaledFrameDeltaTime)).Append(',')
                .Append(Format(runtime.World.LastMotionDeltaTime)).Append(',')
                .Append(Csv(actor.AgentId)).Append(',')
                .Append(actor.R5eRuntimeTick).Append(',')
                .Append(actor.Phase).Append(',')
                .Append(actor.CurrentActivity).Append(',')
                .Append(Csv(actor.Position.ToString("F3"))).Append(',')
                .Append(Csv(actor.DiagnosticDestinationId)).Append(',')
                .Append(Csv(actor.DiagnosticPendingDestinationId)).Append(',')
                .Append(Csv(actor.DiagnosticAutonomyIntentId)).Append(',')
                .Append(Csv(actor.DiagnosticAutonomyDestinationId)).Append(',')
                .Append(Csv(actor.DiagnosticDestinationCell.HasValue
                    ? actor.DiagnosticDestinationCell.Value.ToString()
                    : string.Empty)).Append(',')
                .Append(actor.DiagnosticEmptyOfficeWanderActive).Append(',')
                .Append(actor.SemanticPathLength).Append(',')
                .Append(actor.DiagnosticPathIndex).Append(',')
                .Append(actor.CurrentDirection).Append(',')
                .Append(actor.RequestedDirection).Append(',')
                .Append(actor.MotionDirection).Append(',')
                .Append(actor.LocomotionPhase).Append(',')
                .Append(Format(actor.GaitPhase01)).Append(',')
                .Append(actor.CurrentWalkFrame).Append(',')
                .Append(Csv(actor.LastActualDisplacement.ToString("F6"))).Append(',')
                .Append(Csv(actor.SemanticFrameDisplacement.ToString("F6"))).Append(',')
                .Append(Format(actor.FacingAngularErrorDegrees)).Append(',')
                .Append(Csv(actor.LocomotionVisualFootPlantOffsetWorld.ToString("F6"))).Append(',')
                .Append(Csv(actor.ActiveSeatId)).Append(',')
                .Append(actor.DiagnosticSeatClaimOccupied).Append(',')
                .Append(actor.DiagnosticSeatClaimReleased).Append(',')
                .Append(actor.AttendanceSeatArrivalCount).Append(',')
                .Append(actor.ObservedWorkFrameCount).Append(',')
                .Append(actor.CurrentSeatingClip.HasValue ? actor.CurrentSeatingClip.Value.ToString() : "none").Append(',')
                .Append(actor.CurrentSeatingFrame).Append(',')
                .Append(Csv(actor.CurrentSpriteName)).Append(',')
                .Append(actor.InteractionPhase).Append(',')
                .Append(Csv(occupancy.CurrentCell.ToString())).Append(',')
                .Append(occupancy.IsPresent).Append(',')
                .Append(occupancy.ReservationCount).Append(',')
                .Append(occupancy.Epoch).Append(',')
                .Append(occupancy.Revision).Append(',')
                .Append(Csv(actor.DiagnosticDockingPlan)).Append(',')
                .Append(Csv(actor.DiagnosticLastSeatReleaseRequestReason)).Append(',')
                .Append(actor.DiagnosticLastSeatReleaseRequestTick).Append(',')
                .Append(Format(actor.StuckSeconds)).Append(',')
                .Append(Csv(actor.LastReservationBlocker)).Append(',')
                .Append(Csv(actor.LastMovementBlocker))
                .AppendLine();
        }

        private void AppendEvent(long minute, OfficeRuntimeAgent actor, string kind)
        {
            string row = string.Format(
                CultureInfo.InvariantCulture,
                "clock={0} elapsed={1} frame={2} actor={3} tick={4} kind={5} phase={6} seat={7} " +
                "workFrames={8} destination={9} pending={10} releaseReason={11} releaseTick={12}",
                TimeLabel(minute), minute, Time.frameCount, actor.AgentId, actor.R5eRuntimeTick,
                kind, actor.Phase, actor.ActiveSeatId, actor.ObservedWorkFrameCount,
                actor.DiagnosticDestinationId, actor.DiagnosticPendingDestinationId,
                actor.DiagnosticLastSeatReleaseRequestReason, actor.DiagnosticLastSeatReleaseRequestTick);
            _events.AppendLine(row);
            Debug.Log("FAMILY_COMPANY_NORMAL_SEAT_EVENT | " + row);
        }

        private void RecordFirstFailure(long minute, OfficeRuntimeAgent actor, string reason)
        {
            if (_firstFailure.Length > 0) return;
            _firstFailure = string.Format(
                CultureInfo.InvariantCulture,
                "clock={0};elapsed={1};actor={2};tick={3};phase={4};reason={5}",
                TimeLabel(minute), minute, actor == null ? "none" : actor.AgentId,
                actor == null ? 0UL : actor.R5eRuntimeTick,
                actor == null ? "none" : actor.Phase.ToString(), reason);
            if (actor != null) AppendEvent(minute, actor, "FIRST_FAILURE");
            Debug.LogError("FAMILY_COMPANY_NORMAL_SEAT_STALL_FIRST_FAILURE | " + _firstFailure);
        }

        private void Finish(
            int exitCode,
            string reason,
            PrototypeBootstrap bootstrap,
            StarterOfficeRuntimeBootstrap runtime)
        {
            try
            {
                string artifactPrefix = _emptyOfficeMode
                    ? "normal-new-game-empty"
                    : "normal-new-game-seat";
                File.WriteAllText(ArtifactPath(artifactPrefix + "-trace.csv"), _trace.ToString());
                File.WriteAllText(ArtifactPath(artifactPrefix + "-events.log"), _events.ToString());
                var result = new StringBuilder();
                result.AppendLine(_emptyOfficeMode
                    ? (exitCode == 0
                        ? "FAMILY_COMPANY_NORMAL_NEW_GAME_EMPTY_OFFICE: PASS"
                        : "FAMILY_COMPANY_NORMAL_NEW_GAME_EMPTY_OFFICE: FAIL")
                    : (exitCode == 0
                        ? "FAMILY_COMPANY_NORMAL_NEW_GAME_SEAT_STALL: PASS"
                        : "FAMILY_COMPANY_NORMAL_NEW_GAME_SEAT_STALL: FAIL"));
                result.AppendLine("mode=" + (_emptyOfficeMode ? "empty-office" : "furnished-seat"));
                result.AppendLine("observerOnly=true");
                result.AppendLine("actorQaControl=false");
                result.AppendLine("routeInjection=false");
                result.AppendLine("clockJump=false");
                result.AppendLine("dockingForce=false");
                result.AppendLine("captureEnabled=" + _captureEnabled);
                result.AppendLine("burstCaptureEnabled=" + _burstCaptureEnabled);
                result.AppendLine("burstCaptureFrames=" + _burstCaptureCount);
                result.AppendLine("delaySpeedUntilAttendance=" + _delaySpeedUntilAttendance);
                result.AppendLine("requestedSpeedApplied=" + _requestedSpeedApplied);
                result.AppendLine("requestedSpeed=" + _requestedSpeed.ToString("0", CultureInfo.InvariantCulture));
                result.AppendLine("timeScale=" + Format(Time.timeScale));
                result.AppendLine("worldScale=" + (bootstrap == null ? "missing" : Format(bootstrap.WorldTimeScale)));
                result.AppendLine("elapsedMinute=" + (bootstrap?.State == null ? -1L : bootstrap.State.Time.ElapsedMinutes));
                result.AppendLine("clock=" + (bootstrap?.State == null
                    ? "missing"
                    : bootstrap.State.Time.Now.ToString("HH:mm", CultureInfo.InvariantCulture)));
                result.AppendLine("runtimeReady=" + (runtime != null && runtime.IsReady));
                result.AppendLine("reason=" + reason);
                foreach (string memberId in MemberIds)
                {
                    OfficeRuntimeAgent actor = runtime?.Actors.FirstOrDefault(item =>
                        item != null && string.Equals(item.AgentId, memberId, StringComparison.Ordinal));
                    result.AppendLine(actor == null
                        ? "actor=" + memberId + " missing"
                        : string.Format(
                            CultureInfo.InvariantCulture,
                            "actor={0} arrivals={1} phase={2} activity={3} seat={4} workFrames={5} " +
                            "tick={6} releaseReason={7} releaseTick={8} destination={9} pending={10} path={11}/{12} " +
                            "metrics={13}",
                            actor.AgentId, actor.AttendanceSeatArrivalCount, actor.Phase,
                            actor.CurrentActivity, actor.ActiveSeatId, actor.ObservedWorkFrameCount,
                            actor.R5eRuntimeTick, actor.DiagnosticLastSeatReleaseRequestReason,
                            actor.DiagnosticLastSeatReleaseRequestTick, actor.DiagnosticDestinationId,
                            actor.DiagnosticPendingDestinationId, actor.DiagnosticPathIndex,
                            actor.SemanticPathLength,
                            _observations.TryGetValue(memberId, out ActorObservation observation)
                                ? ObservationMetrics(observation)
                                : "missing"));
                }
                if (_emptyOfficeMode)
                {
                    OfficeAutonomyCoordinator coordinator =
                        Object.FindFirstObjectByType<OfficeAutonomyCoordinator>();
                    result.AppendLine(
                        "coordinatorSelections=" +
                        (coordinator == null ? -1 : coordinator.EmptyOfficeWanderSelectionCount));
                    result.AppendLine(
                        "coordinatorCandidateFailures=" +
                        (coordinator == null ? -1 : coordinator.EmptyOfficeWanderCandidateFailureCount));
                    result.AppendLine(
                        "walkLoops=" + _observations.Values.Sum(item => item.ValidWalkLoops));
                    result.AppendLine(
                        "stationaryDirectionTransitions=" +
                        _observations.Values.Sum(item => item.StationaryDirectionTransitions));
                }
                File.WriteAllText(ArtifactPath(artifactPrefix + "-result.txt"), result.ToString());
            }
            catch (Exception exception)
            {
                Debug.LogError("FAMILY_COMPANY_NORMAL_SEAT_STALL_ARTIFACT_WRITE_FAILED | " + exception.Message);
                if (exitCode == 0) exitCode = 93;
            }

            string resultMarker = _emptyOfficeMode
                ? "FAMILY_COMPANY_NORMAL_NEW_GAME_EMPTY_OFFICE"
                : "FAMILY_COMPANY_NORMAL_NEW_GAME_SEAT_STALL";
            if (exitCode == 0)
                Debug.Log(resultMarker + ": PASS | " + reason);
            else
                Debug.LogError(resultMarker + ": FAIL | code=" + exitCode + " | " + reason);
            Application.Quit(exitCode);
        }

        private string ArtifactPath(string fileName)
        {
            Directory.CreateDirectory(_artifactDirectory);
            return Path.Combine(_artifactDirectory, fileName);
        }

        private static bool TryCaptureOverview(string path, out string failure)
        {
            failure = string.Empty;
            Camera source = Camera.main;
            if (source == null)
            {
                failure = "Camera.main missing";
                return false;
            }
            const int width = 1280;
            const int height = 720;
            RenderTexture previous = RenderTexture.active;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            GameObject captureHost = null;
            try
            {
                captureHost = new GameObject("NormalSeatStallObserverCapture") { hideFlags = HideFlags.HideAndDontSave };
                Camera camera = captureHost.AddComponent<Camera>();
                camera.CopyFrom(source);
                camera.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                camera.aspect = width / (float)height;
                camera.enabled = false;
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                pixels.Apply(false, false);
                File.WriteAllBytes(path, pixels.EncodeToPNG());
                return File.Exists(path) && new FileInfo(path).Length > 1024L;
            }
            catch (Exception exception)
            {
                failure = exception.GetType().Name + ":" + exception.Message;
                return false;
            }
            finally
            {
                RenderTexture.active = previous;
                if (captureHost != null) Object.Destroy(captureHost);
                Object.Destroy(target);
                Object.Destroy(pixels);
            }
        }

        private static string Signature(OfficeRuntimeAgent actor) =>
            actor.Phase + "|" + actor.CurrentActivity + "|" + actor.DiagnosticDestinationId + "|" +
            actor.DiagnosticPendingDestinationId + "|" + actor.DiagnosticAutonomyIntentId + "|" +
            actor.SemanticPathLength + "|" + actor.DiagnosticPathIndex + "|" + actor.ActiveSeatId + "|" +
            actor.DiagnosticDestinationCell + "|" + actor.DiagnosticEmptyOfficeWanderActive + "|" +
            actor.CurrentDirection + "|" + actor.MotionDirection + "|" + actor.LocomotionPhase + "|" +
            actor.CurrentWalkFrame + "|" +
            actor.DiagnosticSeatClaimOccupied + "|" + actor.AttendanceSeatArrivalCount + "|" +
            actor.ObservedWorkFrameCount + "|" + actor.CurrentSeatingClip + "|" + actor.CurrentSeatingFrame + "|" +
            actor.InteractionPhase + "|" + actor.DiagnosticLastSeatReleaseRequestReason + "|" +
            actor.DiagnosticLastSeatReleaseRequestTick;

        private static string ProgressSignature(OfficeRuntimeAgent actor) =>
            actor.Phase + "|" + actor.DiagnosticDestinationId + "|" + actor.DiagnosticPendingDestinationId + "|" +
            actor.SemanticPathLength + "|" + actor.DiagnosticPathIndex + "|" + actor.ActiveSeatId + "|" +
            actor.AttendanceSeatArrivalCount + "|" + actor.ObservedWorkFrameCount + "|" + actor.CurrentSeatingFrame + "|" +
            actor.InteractionPhase;

        private static string Csv(string value)
        {
            string safe = value ?? string.Empty;
            return "\"" + safe.Replace("\"", "\"\"") + "\"";
        }

        private static string Format(float value) => value.ToString("F6", CultureInfo.InvariantCulture);

        private static string ObservationMetrics(ActorObservation observation) =>
            "wander=" + observation.WanderSelections +
            ";loops=" + observation.ValidWalkLoops +
            ";movingFrames=" + observation.MovingFrames +
            ";stationaryTurns=" + observation.StationaryDirectionTransitions +
            ";pivotEpisodes=" + observation.PivotEpisodes +
            ";currentLook=" + observation.CurrentLookSelections +
            ";destinationless=" + observation.DestinationlessIdleSelections +
            ";sameCell=" + observation.SameCellDestinationSelections +
            ";duplicatePivot=" + observation.DuplicatePivotEpisodes +
            ";prePivotTranslation=" + observation.TranslationBeforePivotFrames +
            ";directionMismatch=" + observation.DirectionDisplacementMismatchFrames +
            ";nonCardinal=" + observation.NonCardinalSegmentFrames +
            ";collision=" + observation.CollisionProjectedFrames +
            ";overlap=" + observation.ActorOverlapFrames +
            ";duplicateTarget=" + observation.DuplicateDestinationFrames +
            ";tileCenterDeviation=" + observation.TileCenterDeviationCount +
            ";maxTileCenterError=" + Format(observation.MaximumTileCenterError) +
            ";visualRootOffset=" + observation.NonzeroVisualRootOffsetFrames +
            ";longestStationaryMinutes=" + observation.LongestStationaryMinutes +
            ";maxFrameStep=" + Format(observation.MaximumFrameDisplacement);

        private static string TimeLabel(long elapsedMinute)
        {
            long total = 8L * 60L + 50L + elapsedMinute;
            long hour = ((total / 60L) % 24L + 24L) % 24L;
            long minute = ((total % 60L) + 60L) % 60L;
            return hour.ToString("00", CultureInfo.InvariantCulture) + ":" +
                   minute.ToString("00", CultureInfo.InvariantCulture);
        }

        private static bool HasCommandLineFlag(string flag) => Array.Exists(
            Environment.GetCommandLineArgs(),
            argument => string.Equals(argument, flag, StringComparison.OrdinalIgnoreCase));

        private static string ResolveArtifactDirectory()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index < arguments.Length - 1; index++)
                if (string.Equals(arguments[index], ArtifactDirectoryArgument, StringComparison.OrdinalIgnoreCase))
                    return Path.GetFullPath(arguments[index + 1]);
            return Path.Combine(Application.persistentDataPath, "NormalNewGameSeatStallObserver");
        }

        private static float ResolveSpeed()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index < arguments.Length - 1; index++)
            {
                if (!string.Equals(arguments[index], SpeedArgument, StringComparison.OrdinalIgnoreCase)) continue;
                if (float.TryParse(arguments[index + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out float speed) &&
                    (speed == 1f || speed == 2f || speed == 4f)) return speed;
                throw new InvalidOperationException("Observer speed must be 1, 2, or 4.");
            }
            return 1f;
        }

        private sealed class ActorObservation
        {
            public ActorObservation(OfficeRuntimeAgent actor, long minute)
            {
                ArrivalCount = actor.AttendanceSeatArrivalCount;
                LastPosition = actor.Position;
                LastProgressMinute = minute;
                LastProgress = ProgressSignature(actor);
                LastSignature = string.Empty;
                PreviousFramePosition = actor.Position;
                LastRuntimePhase = actor.Phase;
                LastDisplayDirection = actor.CurrentDirection;
                LastMotionDirection = -1;
                LastGaitCycle = Mathf.FloorToInt(
                    actor.GaitDistance / Mathf.Max(0.000001f, actor.StrideLength));
                SawStationarySinceLastMotion = true;
                MovedSinceLastPivot = true;
                LastAutonomySelection = string.Empty;
                LastMovingMinute = minute;
            }

            public int ArrivalCount { get; set; }
            public bool AwaitingFirstWorkLoop { get; set; }
            public bool CompletedFirstWorkLoop { get; set; }
            public bool EnteredEmptyOffice { get; set; }
            public ulong SeatArrivalTick { get; set; }
            public Vector2 LastPosition { get; set; }
            public long LastProgressMinute { get; set; }
            public string LastProgress { get; set; }
            public string LastSignature { get; set; }
            public Vector2 PreviousFramePosition { get; set; }
            public OfficeRuntimeAgentPhase LastRuntimePhase { get; set; }
            public int LastDisplayDirection { get; set; }
            public int LastMotionDirection { get; set; }
            public int LastGaitCycle { get; set; }
            public bool SawStationarySinceLastMotion { get; set; }
            public bool WasPivoting { get; set; }
            public bool HasPivotEpisode { get; set; }
            public bool MovedSinceLastPivot { get; set; }
            public OfficeGridCoordinate LastPivotCell { get; set; }
            public string LastAutonomySelection { get; set; }
            public int WanderSelections { get; set; }
            public int ValidWalkLoops { get; set; }
            public int MovingFrames { get; set; }
            public int StationaryDirectionTransitions { get; set; }
            public int PivotEpisodes { get; set; }
            public int CurrentLookSelections { get; set; }
            public int DestinationlessIdleSelections { get; set; }
            public int SameCellDestinationSelections { get; set; }
            public int DuplicatePivotEpisodes { get; set; }
            public int TranslationBeforePivotFrames { get; set; }
            public int DirectionDisplacementMismatchFrames { get; set; }
            public int NonCardinalSegmentFrames { get; set; }
            public int CollisionProjectedFrames { get; set; }
            public int ActorOverlapFrames { get; set; }
            public int DuplicateDestinationFrames { get; set; }
            public int TileCenterDeviationCount { get; set; }
            public int NonzeroVisualRootOffsetFrames { get; set; }
            public float MaximumTileCenterError { get; set; }
            public float MaximumFrameDisplacement { get; set; }
            public long LastMovingMinute { get; set; }
            public long LongestStationaryMinutes { get; set; }
        }
    }
}
