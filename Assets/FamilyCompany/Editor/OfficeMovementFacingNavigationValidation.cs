using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FamilyCompany.Presentation.Unity;
using FamilyCompany.Presentation.Unity.OfficeGridView;
using FamilyCompany.Presentation.Unity.OfficeRuntime;
using FamilyCompany.Presentation.Unity.OfficeWorkActions;
using FamilyCompany.Simulation.Navigation;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;
using SemanticOfficeGrid = FamilyCompany.Simulation.OfficeLayout.OfficeGrid;

namespace FamilyCompany.Editor
{
    /// <summary>
    /// Runtime-bound regression for the reported failure: a laterally translating root must never
    /// consume a front-facing body sprite. This intentionally observes the SpriteRenderer after
    /// DirectionalSpriteAnimator.Tick, in the same presentation frame as the actual displacement.
    /// </summary>
    public static class OfficeMovementFacingNavigationValidation
    {
        private const float FrameDeltaTime = 1f / 60f;
        private static readonly string[] DirectionTokens =
        {
            "south", "southwest", "west", "northwest",
            "north", "northeast", "east", "southeast"
        };
        private static readonly Vector2[] DirectionVectors =
        {
            Vector2.down,
            new Vector2(1f, -1f).normalized,
            Vector2.right,
            new Vector2(1f, 1f).normalized,
            Vector2.up,
            new Vector2(-1f, 1f).normalized,
            Vector2.left,
            new Vector2(-1f, -1f).normalized
        };

        [MenuItem("Family Company/Validate Movement Facing Navigation")]
        public static void Run()
        {
            OfficeNavigationRegressionReport navigation = OfficeNavigationRegressionSuite.Run();
            OfficeSharedLocomotionStrictReport strict = OfficeSharedLocomotionStrictValidation.Run();
            ValidateSameFrameLateralSpriteConsumption();
            ValidateEightDirectionsAndStoppedFacing();
            ValidateProjectedSpriteFacingBasis();
            ValidateTileCenterRouting();
            ValidateReferenceCadencePlantedFootGait();
            ValidateVisibleMotionFrameCap();
            ValidateHighRefreshFirstAccelerationStep();
            ValidateActorScopedDebtTransitionsAndRoundRobin();
            ValidateFourActorAttendanceIngressReservation();
            ValidateFollowingAttendanceIngress();
            ValidateIngressToGridReservationHandoff();
            ValidateDoorLeaderTrafficPriority();
            ValidateCanonicalFurniturePathDetours();
            Debug.Log(
                "OFFICE_MOVEMENT_FACING_NAVIGATION_VALIDATION: PASS | " +
                $"seeds={navigation.Seeds} paths={navigation.Paths} " +
                $"facingChecks={navigation.FacingPresentationChecks} " +
                $"gaitChecks={navigation.GaitPresentationChecks} | {strict}");
        }

        public static void RunBatch()
        {
            try
            {
                Run();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void ValidateSameFrameLateralSpriteConsumption()
        {
            using (var harness = new AnimatorHarness())
            {
                ValidateLateralRun(harness, Vector2.left, ExpectedFacing(Vector2.left), "screen-left");
                ValidateLateralRun(harness, Vector2.right, ExpectedFacing(Vector2.right), "screen-right");
            }
            ValidateProductionLateralSpriteAssets();
        }

        // Sprite rows describe visible screen headings. Keep the expectation behind the shared
        // adapter, then independently reject any body leaning away from its travelled side.
        private static int ExpectedFacing(Vector2 worldDisplacement) =>
            DirectionalSpriteAnimator.ResolveTileDirection(
                OfficeGridTilemapPresenter.DefaultWorldVectorToVisualFacingAxes(worldDisplacement));

        private static int HorizontalLean(int direction) => direction switch
        {
            1 or 2 or 3 => -1,
            5 or 6 or 7 => 1,
            _ => 0
        };

        private static void RequireNoMoonwalk(
            DirectionalLocomotionFrameTrace trace,
            Vector2 displacementDirection,
            string label,
            int frame)
        {
            int travelSign = Math.Sign(displacementDirection.x);
            if (travelSign == 0) return;
            int lean = HorizontalLean(trace.DisplayDirection);
            Require(lean * travelSign >= 0,
                $"{label} frame {frame} rendered a body leaning away from the travelled side: {trace}.");
        }

        private static void ValidateProductionLateralSpriteAssets()
        {
            OfficeRuntimeCharacterArtCatalog catalog =
                OfficeRuntimeCharacterArtCatalog.LoadDefault();
            Require(catalog != null, "Default production character art catalog is missing.");
            string[] members = { "player", "older_sister", "father", "mother" };
            foreach (string memberId in members)
            {
                Require(catalog.TryCopyWalkFrames(memberId, out Sprite[] frames),
                    memberId + " production walk frames are missing.");
                using (var harness = new ProductionAnimatorHarness(memberId, frames))
                {
                    ValidateProductionLateralRun(
                        harness, Vector2.left, ExpectedFacing(Vector2.left), "screen-left");
                    ValidateProductionLateralRun(
                        harness, Vector2.right, ExpectedFacing(Vector2.right), "screen-right");
                }
            }
        }

        private static void ValidateProductionLateralRun(
            ProductionAnimatorHarness harness,
            Vector2 displacementDirection,
            int expectedDirection,
            string label)
        {
            harness.Animator.RestoreStandingFacing(0);
            for (var frame = 0; frame < 18; frame++)
            {
                DirectionalLocomotionFrameTrace trace = harness.Step(displacementDirection);
                Require(trace.MotionDirection == expectedDirection &&
                        trace.DisplayDirection == expectedDirection,
                    $"{harness.MemberId}/{label}/{frame} production direction mismatch: {trace}.");
                Require(OfficeWorkActionFrameSet.TryResolveNamedDirection(
                            harness.Renderer.sprite,
                            out int spriteDirection) &&
                        spriteDirection == expectedDirection &&
                        spriteDirection != 0 && spriteDirection != 4,
                    $"{harness.MemberId}/{label}/{frame} production Sprite mismatch: {trace}.");
                Require(!trace.FlipX,
                    $"{harness.MemberId}/{label}/{frame} production Sprite was mirrored: {trace}.");
                RequireNoMoonwalk(trace, displacementDirection, harness.MemberId + "/" + label, frame);
                if (frame == 0 || frame == 17)
                    Debug.Log(
                        $"PRODUCTION_LATERAL_FRAME_TRACE | member={harness.MemberId} " +
                        $"side={label} frame={frame} | {trace}");
            }
        }

        private static void ValidateLateralRun(
            AnimatorHarness harness,
            Vector2 displacementDirection,
            int expectedDirection,
            string label)
        {
            // Recreate the reported stale state before each lateral run: the visible body starts
            // South/front-facing, then receives a pure horizontal root displacement.
            harness.Animator.RestoreStandingFacing(0);
            for (var frame = 0; frame < 36; frame++)
            {
                DirectionalLocomotionFrameTrace trace = harness.Step(displacementDirection, true);
                if (frame == 0 || frame == 12 || frame == 35)
                    Debug.Log($"LATERAL_FRAME_TRACE | side={label} frame={frame} | {trace}");
                Require(Mathf.Sign(trace.ActualDisplacement.x) == Mathf.Sign(displacementDirection.x),
                    $"{label} frame {frame} did not retain lateral actual displacement: {trace}.");
                Require(Mathf.Abs(trace.ActualDisplacement.y) <= 0.000001f,
                    $"{label} frame {frame} acquired vertical displacement: {trace}.");
                Require(trace.ActualSpeed > OfficeSharedLocomotionRules.WalkSpeedThreshold,
                    $"{label} frame {frame} did not report moving speed: {trace}.");
                Require(trace.IsMoving, $"{label} frame {frame} consumed a planted pose: {trace}.");
                Require(trace.MotionDirection == expectedDirection,
                    $"{label} frame {frame} resolved motion direction {trace.MotionDirection}: {trace}.");
                Require(trace.DisplayDirection == expectedDirection,
                    $"{label} frame {frame} displayed stale direction {trace.DisplayDirection}: {trace}.");
                Require(trace.Clip.StartsWith("Walk", StringComparison.Ordinal) ||
                        trace.Clip.StartsWith("Transition/", StringComparison.Ordinal),
                    $"{label} frame {frame} selected a non-locomotion clip: {trace}.");
                Require(OfficeWorkActionFrameSet.TryResolveNamedDirection(
                            harness.Renderer.sprite,
                            out int spriteDirection) &&
                        spriteDirection == expectedDirection,
                    $"{label} frame {frame} consumed wrong sprite metadata: {trace}.");
                Require(spriteDirection != 0 && spriteDirection != 4,
                    $"{label} frame {frame} selected a front/back sprite during lateral motion: {trace}.");
                Require(!trace.FlipX,
                    $"{label} frame {frame} mirrored an independently-authored 8-way sprite: {trace}.");
                RequireNoMoonwalk(trace, displacementDirection, label, frame);
            }
        }

        private static void ValidateEightDirectionsAndStoppedFacing()
        {
            using (var harness = new AnimatorHarness())
            {
                var consumed = new HashSet<int>();
                for (var direction = 0; direction < DirectionVectors.Length; direction++)
                {
                    Vector2 world = DirectionVectors[direction];
                    int expected = ExpectedFacing(world);
                    harness.Animator.RestoreStandingFacing(expected);
                    DirectionalLocomotionFrameTrace trace = harness.Step(world, true);
                    Require(trace.MotionDirection == expected && trace.DisplayDirection == expected,
                        $"8-way runtime frame mismatch for {DirectionTokens[direction]}: {trace}.");
                    Require(OfficeWorkActionFrameSet.TryResolveNamedDirection(
                                harness.Renderer.sprite,
                                out int spriteDirection) &&
                            spriteDirection == expected,
                        $"8-way sprite consumer mismatch for {DirectionTokens[direction]}: {trace}.");
                    RequireNoMoonwalk(trace, world, DirectionTokens[direction], direction);
                    consumed.Add(expected);
                }

                // Eight distinct world headings must still reach eight distinct authored rows. This
                // holds under any adapter convention and fails immediately if the projection ever
                // collapses two headings onto one body.
                Require(consumed.Count == DirectionVectors.Length,
                    $"8-way headings collapsed onto {consumed.Count} authored rows.");

                int heldFacing = ExpectedFacing(Vector2.right);
                harness.Animator.RestoreStandingFacing(heldFacing);
                harness.Step(Vector2.right, true);
                DirectionalLocomotionFrameTrace stopped = default;
                for (var frame = 0; frame < 20; frame++)
                    stopped = harness.Step(Vector2.zero, false);
                Require(stopped.Phase == OfficeLocomotionPhase.Idle && !stopped.IsMoving,
                    "Stopped runtime actor did not settle to Idle: " + stopped);
                Require(stopped.DisplayDirection == heldFacing,
                    "Stopped runtime actor did not retain its screen-right facing: " + stopped);
                Require(OfficeWorkActionFrameSet.TryResolveNamedDirection(
                            harness.Renderer.sprite,
                            out int stoppedSpriteDirection) &&
                        stoppedSpriteDirection == heldFacing,
                    "Stopped sprite consumer did not retain screen-right art: " + stopped);
            }
        }

        private static void ValidateFollowingAttendanceIngress()
        {
            using var harness = new OccupancyHarness(OfficeGridLayouts.CreateNewGameEmptyOfficeV1());
            foreach (string id in new[] { "leader", "follower" })
            {
                harness.Occupancy.RegisterActor(id, harness.Position(new OfficeGridCoordinate(4, 4)), 0.445f, 0.22f);
                harness.Occupancy.SetActorPresent(id, false);
            }
            Vector2 entrance = harness.Position(new OfficeGridCoordinate(8, 1));
            Vector2 inward = harness.Position(new OfficeGridCoordinate(8, 2)) - entrance;
            Vector2 exterior = entrance - inward * 2.5f;
            Require(harness.Occupancy.TryClaimAttendanceIngress("leader", exterior, entrance, 0.22f), "Leader claim failed.");
            harness.Occupancy.SetActorPresent("leader", true);
            Require(!harness.Occupancy.TryClaimAttendanceIngress("follower", exterior, entrance, 0.22f), "Spawn overlap accepted.");
            Require(harness.Occupancy.TryClaimQueuedAttendanceIngress(
                    "follower", exterior, entrance, 0.22f, out Vector2 queuedExterior),
                "Due entrant could not join the safe outside queue.");
            Require(Vector2.Dot(queuedExterior - exterior, inward) < 0f &&
                    Vector2.Distance(queuedExterior, exterior) >= 0.95f,
                "Queued entrant did not preserve the registered body clearance.");
            harness.Occupancy.SetActorPresent("follower", true);
            Require(!harness.Occupancy.CanMoveAttendanceIngress(
                    "follower", queuedExterior, exterior, 0.22f),
                "Queued entrant crossed the leader body.");
            harness.Occupancy.SetActorPresent("follower", false);
            harness.Occupancy.ReleaseAttendanceIngress("follower");
            Vector2 ahead = exterior + inward.normalized * 1.05f;
            Require(harness.Occupancy.CanMoveAttendanceIngress("leader", exterior, ahead, 0.22f), "Leader cannot advance.");
            harness.Occupancy.UpdateActor("leader", ahead, Vector2.zero, 0f);
            Require(harness.Occupancy.TryClaimAttendanceIngress("follower", exterior, entrance, 0.22f), "Safe following entrant blocked by whole-corridor ownership.");
            harness.Occupancy.SetActorPresent("follower", true);
            Require(harness.Occupancy.AttendanceIngressClaimCount == 2, "Following claims were not independent.");
            Require(harness.Occupancy.CanMoveAttendanceIngress("follower", exterior, exterior + inward.normalized * 0.05f, 0.22f), "Safe follow step rejected.");
            Require(!harness.Occupancy.CanMoveAttendanceIngress("follower", exterior, ahead, 0.22f), "Follower crossed leader body.");
            Require(!harness.Occupancy.CanMoveAttendanceIngress("follower", exterior, exterior + Vector2.right, 0.22f), "Follower escaped its claimed corridor.");
            harness.Occupancy.ReleaseAttendanceIngress("leader");
            Require(harness.Occupancy.AttendanceIngressClaimCount == 1, "Leader exit revoked follower claim.");
            harness.Occupancy.SetActorPresent("leader", false);
            Require(harness.Occupancy.CanMoveAttendanceIngress("follower", exterior, entrance, 0.22f), "Follower lost valid corridor after leader left.");
            harness.Occupancy.ReleaseAttendanceIngress("follower");
            Require(harness.Occupancy.AttendanceIngressClaimCount == 0, "Ingress claim leaked.");
            Debug.Log("ATTENDANCE_FOLLOWING_INGRESS: PASS independent claims, safe spawn, swept peer collision, own corridor, release cleanup");
        }

        private static void ValidateDoorLeaderTrafficPriority()
        {
            // Recorded next-day geometry: the indoor leader turns left while the next
            // entrant approaches from outside. Alphabetical IDs must not make the leader
            // retreat into the next person's desk aisle to give way to its own follower.
            foreach (string leaderId in new[] { "player", "a-leader" })
            {
                var leader = new OfficeTrafficAgentState(leaderId, new OfficeNavPoint(0f, 0f),
                    new OfficeNavPoint(-0.8944272f, -0.4472136f), 0.445f, 0f);
                var follower = new OfficeTrafficAgentState("older_sister", new OfficeNavPoint(0.85f, -0.425f),
                    new OfficeNavPoint(-0.8944272f, 0.4472136f), 0.445f, 0f);
                OfficeTrafficDecision front = OfficeNavigationTrafficRules.Resolve(leader, new[] { follower });
                OfficeTrafficDecision back = OfficeNavigationTrafficRules.Resolve(follower, new[] { leader });
                Require(front.ForwardScale > 0f && !front.IsYielding,
                    "The indoor door leader incorrectly yields to its approaching follower.");
                Require(back.ForwardScale == 0f && back.IsYielding,
                    "The door follower must yield while the leading body turns away.");
            }
            Debug.Log("DOOR_LEADER_TRAFFIC_PRIORITY: PASS leader exits first, follower waits; IDs do not reverse queue");
        }

        private static void ValidateIngressToGridReservationHandoff()
        {
            using var harness = new OccupancyHarness(OfficeGridLayouts.CreateNewGameEmptyOfficeV1());
            var cell = new OfficeGridCoordinate(8, 1);
            Vector2 entrance = harness.Position(cell);
            Vector2 inward = harness.Position(new OfficeGridCoordinate(8, 2)) - entrance;
            Vector2 exterior = entrance - inward * 2.5f;
            harness.Occupancy.RegisterActor("leader", entrance, 0.445f, 0.22f);
            harness.Occupancy.RegisterActor("follower", harness.Position(new OfficeGridCoordinate(10, 2)), 0.445f, 0.22f);
            harness.Occupancy.SetActorPresent("follower", false);
            Require(harness.Occupancy.TryClaimAttendanceIngress("follower", exterior, entrance, 0.22f), "Follower ingress setup failed.");
            harness.Occupancy.SetActorPresent("follower", true);
            Vector2 leaderPosition = entrance + inward * 0.43f;
            Vector2 followerPosition = entrance - inward * 0.49f;
            harness.Occupancy.UpdateActor("leader", leaderPosition, inward.normalized, 0f);
            Require(harness.Occupancy.CanMoveAttendanceIngress("follower", exterior, followerPosition, 0.22f), "Nonoverlapping follower step rejected.");
            harness.Occupancy.UpdateActor("follower", followerPosition, inward.normalized, 0f);
            Require(harness.Occupancy.CurrentCell("leader").Equals(cell) && harness.Occupancy.CurrentCell("follower").Equals(cell), "Same nearest-cell counterexample missing.");
            Require(harness.Occupancy.TryReservePath("leader", cell, new[] { new OfficeGridCoordinate(8, 2) }), "Ingress follower falsely blocks the indoor leader's rounded cell.");
            Require(harness.Occupancy.CanMove("leader", leaderPosition, harness.Position(new OfficeGridCoordinate(8, 2)), 0.22f, string.Empty), "Indoor leader cannot leave doorway.");
            Require(!harness.Occupancy.CanMove("leader", leaderPosition, followerPosition, 0.22f, string.Empty), "Handoff bypassed actual body collision.");
            Debug.Log("ATTENDANCE_GRID_HANDOFF: PASS same-cell followers do not deadlock indoor reservations; body collision retained");
        }

        private static void ValidateFourActorAttendanceIngressReservation()
        {
            for (var layout = 0; layout < 3; layout++)
            using (var harness = new OccupancyHarness(CreateAttendanceFurnitureGrid(layout)))
            {
                string[] actors = { "arrival-0", "arrival-1", "arrival-2", "arrival-3" };
                var entranceCell = new OfficeGridCoordinate(8, 1);
                OfficeGridCoordinate[] goals =
                {
                    new OfficeGridCoordinate(2, 10),
                    new OfficeGridCoordinate(5, 10),
                    new OfficeGridCoordinate(8, 10),
                    new OfficeGridCoordinate(11, 10)
                };
                for (var index = 0; index < actors.Length; index++)
                {
                    harness.Register(actors[index], new OfficeGridCoordinate(index + 2, 11));
                    harness.Occupancy.SetActorPresent(actors[index], false);
                }

                Vector2 entrance = harness.Position(entranceCell);
                Vector2 inward = harness.Position(new OfficeGridCoordinate(8, 2)) - entrance;
                Vector2 exterior = entrance - inward * 2.5f;
                Vector2 exteriorFloorCell = harness.Position(new OfficeGridCoordinate(8, 0));
                Require(Vector2.Dot(
                            exterior - exteriorFloorCell,
                            inward.normalized) < -0.5f * inward.magnitude,
                    "Attendance exterior point was not beyond the outermost office floor cell.");
                Require(harness.Occupancy.TryClaimAttendanceIngress(
                        actors[0], exterior, entrance, OfficeRuntimeAgent.DefaultRadius),
                    "First simultaneous attendee could not claim the single ingress.");
                for (var index = 1; index < actors.Length; index++)
                    Require(!harness.Occupancy.TryClaimAttendanceIngress(
                            actors[index], exterior, entrance, OfficeRuntimeAgent.DefaultRadius),
                        "Multiple simultaneous attendees claimed the same ingress.");

                for (var index = 0; index < actors.Length; index++)
                {
                    string actor = actors[index];
                    if (index > 0)
                        Require(harness.Occupancy.TryClaimAttendanceIngress(
                                actor, exterior, entrance, OfficeRuntimeAgent.DefaultRadius),
                            $"Serialized attendee {index} could not claim the cleared ingress.");
                    harness.Occupancy.SetActorPresent(actor, true);
                    Require(harness.Occupancy.CanMoveAttendanceIngress(
                            actor, exterior, entrance, OfficeRuntimeAgent.DefaultRadius),
                        $"Serialized attendee {index} could not traverse its owned ingress.");
                    harness.Occupancy.UpdateActor(
                        actor,
                        entrance,
                        Vector2.zero,
                        0f);
                    harness.Occupancy.ReleaseAttendanceIngress(actor);
                    Require(harness.Occupancy.AttendanceIngressOwner.Length == 0,
                        $"Serialized attendee {index} left stale ingress ownership.");

                    var paths = new OfficeRuntimePathService(
                        harness.Grid,
                        harness.Occupancy,
                        harness.Presenter);
                    IReadOnlyList<OfficeGridCoordinate> path = paths.FindPath(
                        actor,
                        entranceCell,
                        goals[index],
                        string.Empty,
                        true,
                        OfficeRuntimeAgent.DefaultRadius);
                    Require(path.Count > 1 && path.Count <= harness.Grid.CellCount,
                        $"Layout {layout} attendee {index} required an unbounded/failed replan.");
                    Vector2 previousActual = Vector2.zero;
                    for (var pathIndex = 1; pathIndex < path.Count; pathIndex++)
                    {
                        Vector2 start = harness.Position(path[pathIndex - 1]);
                        Vector2 end = harness.Position(path[pathIndex]);
                        Vector2 intended = end - start;
                        Vector2 actual = OfficeRuntimeCollisionMotion.Resolve(
                            harness.Occupancy,
                            actor,
                            start,
                            intended,
                            intended.normalized * OfficeRuntimeAgent.DefaultMoveSpeed,
                            previousActual,
                            OfficeRuntimeAgent.DefaultRadius,
                            string.Empty,
                            out _);
                        Require(Vector2.Distance(actual, intended) <= 0.00001f,
                            $"Layout {layout} attendee {index} path crossed furniture/another NPC " +
                            $"at segment {pathIndex - 1}->{pathIndex}.");
                        previousActual = actual;
                        harness.Occupancy.UpdateActor(actor, end, intended.normalized, 0f);
                    }
                    harness.Occupancy.ClearReservations(actor);
                }
                Require(harness.Occupancy.AgentPenetrationCount == 0,
                    $"Layout {layout} four attendees recorded a dynamic penetration.");
                Require(harness.Occupancy.StaticViolationCount == 0 &&
                        harness.Occupancy.InteractionViolationCount == 0,
                    $"Layout {layout} attendees recorded furniture/static penetration.");
                Debug.Log(
                    $"ATTENDANCE_LAYOUT_TRACE | layout={layout} entrants=4 " +
                    $"canonicalObstacles={harness.Occupancy.CanonicalGeometryObstacleCount} " +
                    "pathQueries=4 retries=0 penetrations=0");
            }
        }

        private static void ValidateVisibleMotionFrameCap()
        {
            foreach (var worldScale in new[] { 1f, 2f, 4f })
            using (var harness = new OccupancyHarness(CreateAttendanceFurnitureGrid(2)))
            {
                string actorId = "visible-budget-" + worldScale.ToString("F0");
                var startCell = new OfficeGridCoordinate(1, 1);
                // Eight center-to-center cells are enough to exercise hitches, corners and debt
                // draining while keeping this per-scale regression in the fast iteration loop.
                var goalCell = new OfficeGridCoordinate(5, 5);
                harness.Register(actorId, startCell);
                var paths = new OfficeRuntimePathService(
                    harness.Grid,
                    harness.Occupancy,
                    harness.Presenter);
                IReadOnlyList<OfficeGridCoordinate> path = paths.FindPath(
                    actorId,
                    startCell,
                    goalCell,
                    string.Empty,
                    false,
                    OfficeRuntimeAgent.DefaultRadius);
                Require(path.Count > 2, $"{worldScale:F0}x debt route was not found.");

                float routeLength = 0f;
                for (var index = 1; index < path.Count; index++)
                    routeLength += Vector2.Distance(
                        harness.Position(path[index - 1]),
                        harness.Position(path[index]));

                Vector2 position = harness.Position(path[0]);
                Vector2 previousActual = Vector2.zero;
                int pathIndex = 1;
                float debt = 0f;
                float totalIncoming = 0f;
                float totalConsumed = 0f;
                float totalMoved = 0f;
                float maximumRenderMove = 0f;
                int multiStepRenderCount = 0;
                int drainedAtFrame = -1;
                float kinematicSeconds = routeLength / OfficeRuntimeAgent.DefaultMoveSpeed;
                int maximumFrames = Mathf.CeilToInt(
                    (kinematicSeconds / worldScale + 4f) * 60f);
                for (var frame = 0; frame < maximumFrames; frame++)
                {
                    float unscaledFrameDelta = frame == 4
                        ? 0.200f
                        : frame == 11
                            ? 0.500f
                            : 1f / 60f;
                    // The office clock and actor routes must consume the same gameplay scale.
                    // The per-render cap/debt mechanism still prevents hitch teleports.
                    float scaledSimulationDelta = unscaledFrameDelta * worldScale;
                    Require(scaledSimulationDelta >= unscaledFrameDelta,
                        "World-scale test input was invalid.");
                    OfficeVisibleMotionBudget budget =
                        OfficeRuntimeWorld.ConsumeVisibleMotionBudget(
                            debt,
                            scaledSimulationDelta);
                    debt = budget.RemainingDebtSeconds;
                    totalIncoming += scaledSimulationDelta;
                    totalConsumed += budget.ConsumedSeconds;
                    float renderMoved = 0f;
                    int stepCount = OfficeNavigationMotionIntegrator.CalculateStepCount(
                        budget.ConsumedSeconds);
                    if (stepCount > 1) multiStepRenderCount++;
                    for (var step = 0; step < stepCount; step++)
                    {
                        float stepDelta = OfficeNavigationMotionIntegrator.ResolveStepDelta(
                            budget.ConsumedSeconds,
                            step,
                            stepCount);
                        float remainingBudget = OfficeRuntimeAgent.DefaultMoveSpeed * stepDelta;
                        int movementIterations = 0;
                        while (remainingBudget > 0.0000001f && pathIndex < path.Count)
                        {
                            movementIterations++;
                            Require(movementIterations <= path.Count * 4,
                                $"{worldScale:F0}x frame {frame} movement loop made no finite " +
                                $"progress: pathIndex={pathIndex}/{path.Count} " +
                                $"remaining={remainingBudget:R} position={position}.");
                            Vector2 target = harness.Position(path[pathIndex]);
                            Vector2 delta = target - position;
                            if (delta.magnitude <= 0.000001f)
                            {
                                position = target;
                                pathIndex++;
                                continue;
                            }
                            Vector2 intended = delta.normalized *
                                               Mathf.Min(delta.magnitude, remainingBudget);
                            Vector2 actual = OfficeRuntimeCollisionMotion.Resolve(
                                harness.Occupancy,
                                actorId,
                                position,
                                intended,
                                intended.normalized * OfficeRuntimeAgent.DefaultMoveSpeed,
                                previousActual,
                                OfficeRuntimeAgent.DefaultRadius,
                                string.Empty,
                                out bool collisionProjected);
                            Require(!collisionProjected &&
                                    Vector2.Distance(actual, intended) <= 0.00001f,
                                $"{worldScale:F0}x debt substep crossed canonical geometry.");
                            position += actual;
                            previousActual = actual;
                            renderMoved += actual.magnitude;
                            totalMoved += actual.magnitude;
                            remainingBudget -= actual.magnitude;
                            harness.Occupancy.UpdateActor(
                                actorId,
                                position,
                                intended.normalized,
                                0f);
                            if (Vector2.Distance(position, target) <= 0.00001f)
                            {
                                position = target;
                                pathIndex++;
                            }
                        }
                    }
                    maximumRenderMove = Mathf.Max(maximumRenderMove, renderMoved);
                    Require(renderMoved <= 0.099001f,
                        $"{worldScale:F0}x rendered root step {renderMoved:F6} exceeded 0.099.");
                    if (pathIndex >= path.Count && debt <= 0.0000001f)
                    {
                        drainedAtFrame = frame;
                        break;
                    }
                }

                Require(drainedAtFrame >= 0 && drainedAtFrame < maximumFrames,
                    $"{worldScale:F0}x motion debt did not drain or route did not arrive " +
                    $"within speed-derived budget: route={routeLength:F6} " +
                    $"speed={OfficeRuntimeAgent.DefaultMoveSpeed:F3} " +
                    $"kinematic={kinematicSeconds:F3}s frames={maximumFrames}.");
                Require(multiStepRenderCount >= 2,
                    $"{worldScale:F0}x hitch did not exercise multiple fixed updates/render.");
                Require(debt <= 0.0000001f,
                    $"{worldScale:F0}x retained a final motion backlog of {debt:F8}s.");
                Require(Mathf.Abs(totalIncoming - totalConsumed) <= 0.00001f,
                    $"{worldScale:F0}x discarded motion time: " +
                    $"incoming={totalIncoming:F6} consumed={totalConsumed:F6}.");
                Require(Mathf.Abs(totalMoved - routeLength) <= 0.0001f &&
                        Vector2.Distance(position, harness.Position(goalCell)) <= 0.00001f,
                    $"{worldScale:F0}x failed exact endpoint/distance preservation: " +
                    $"moved={totalMoved:F6} route={routeLength:F6} position={position}.");
                Require(harness.Occupancy.CurrentCell(actorId).Equals(goalCell) &&
                        harness.Occupancy.StaticViolationCount == 0 &&
                        harness.Occupancy.InteractionViolationCount == 0 &&
                        harness.Occupancy.AgentPenetrationCount == 0,
                    $"{worldScale:F0}x visual root and canonical occupancy diverged.");
                Debug.Log(
                    $"VISIBLE_MOTION_DEBT_TRACE | scale={worldScale:F0}x " +
                    $"hitches=0.200,0.500 maxRenderMove={maximumRenderMove:F6} " +
                    $"distanceError={Mathf.Abs(totalMoved - routeLength):F8} " +
                    $"drainedFrame={drainedAtFrame} multiStepRenders={multiStepRenderCount} " +
                    "finalDebt=0 penetrations=0");
            }
        }

        private static void ValidateHighRefreshFirstAccelerationStep()
        {
            using (var harness = new OccupancyHarness(CreateAttendanceFurnitureGrid(2)))
            {
                const string actorId = "high-refresh-first-step";
                var startCell = new OfficeGridCoordinate(1, 1);
                harness.Register(actorId, startCell);
                Vector2 start = harness.Position(startCell);
                const float deltaTime = 1f / 240f;
                OfficeMotionIntegrationResult motion = OfficeNavigationMotionIntegrator.IntegrateVelocity(
                    new OfficeNavPoint(0f, 0f),
                    new OfficeNavPoint(OfficeRuntimeAgent.DefaultMoveSpeed, 0f),
                    OfficeNavigationMotionIntegrator.DefaultAcceleration,
                    deltaTime);
                var intended = new Vector2(motion.Displacement.X, motion.Displacement.Z);
                Require(intended.sqrMagnitude > 0f && intended.sqrMagnitude < 0.0000001f,
                    "The high-refresh fixture no longer exercises the former zero-displacement threshold.");
                Vector2 actual = OfficeRuntimeCollisionMotion.Resolve(
                    harness.Occupancy,
                    actorId,
                    start,
                    intended,
                    Vector2.right * OfficeRuntimeAgent.DefaultMoveSpeed,
                    Vector2.zero,
                    OfficeRuntimeAgent.DefaultRadius,
                    string.Empty,
                    out bool collisionProjected);
                Require(!collisionProjected && Vector2.Distance(actual, intended) <= 0.0000001f,
                    "A valid 240 Hz first acceleration step was discarded as zero movement.");
            }
        }

        private static void ValidateProjectedSpriteFacingBasis()
        {
            // Literal 2:1 isometric steps. These are written out here instead of read back from the
            // adapter under test so this fixture cannot pass by sharing the product's own mistake.
            Vector2 basisX = new Vector2(1f, 0.5f);
            Vector2 basisY = new Vector2(-1f, 0.5f);
            using (var harness = new AnimatorHarness())
            {
                ValidateProjectedDirection(harness, basisX, 5, "grid+X/screen-up-right/northeast-art");
                ValidateProjectedDirection(harness, -basisX, 1, "grid-X/screen-down-left/southwest-art");
                ValidateProjectedDirection(harness, basisY, 3, "grid+Y/screen-up-left/northwest-art");
                ValidateProjectedDirection(harness, -basisY, 7, "grid-Y/screen-down-right/southeast-art");
                ValidateProjectedDirection(harness, basisX + basisY, 4, "grid+X+Y/screen-up/north-art");
                ValidateProjectedDirection(harness, basisX - basisY, 6, "grid+X-Y/screen-right/east-art");
            }
            Require(Vector2.Distance(
                        OfficeGridTilemapPresenter.DefaultWorldVectorToVisualFacingAxes(basisX).normalized,
                        basisX.normalized) <= 0.000001f &&
                    Vector2.Distance(
                        OfficeGridTilemapPresenter.DefaultWorldVectorToVisualFacingAxes(basisY).normalized,
                        basisY.normalized) <= 0.000001f,
                "Projected walk-art adapter did not preserve the visible screen heading.");
            for (var direction = 0; direction < DirectionalSpriteAnimator.DirectionCount; direction++)
            {
                Vector2 facing = OctantFacingVector(direction);
                Vector2 roundTrip = OfficeGridTilemapPresenter.DefaultWorldVectorToVisualFacingAxes(
                    OfficeGridTilemapPresenter.VisualFacingAxesToWorldVector(facing));
                Require(Vector2.Distance(roundTrip, facing) <= 0.00001f,
                    $"Facing axes round trip lost direction {direction}: {facing} -> {roundTrip}.");
            }
        }

        private static Vector2 OctantFacingVector(int direction) => direction switch
        {
            0 => new Vector2(0f, -1f),
            1 => new Vector2(-1f, -1f),
            2 => new Vector2(-1f, 0f),
            3 => new Vector2(-1f, 1f),
            4 => new Vector2(0f, 1f),
            5 => new Vector2(1f, 1f),
            6 => new Vector2(1f, 0f),
            7 => new Vector2(1f, -1f),
            _ => Vector2.zero
        };

        private static void ValidateProjectedDirection(
            AnimatorHarness harness,
            Vector2 worldDirection,
            int expectedDirection,
            string label)
        {
            harness.Animator.RestoreStandingFacing(expectedDirection);
            DirectionalLocomotionFrameTrace trace = harness.Step(worldDirection, true);
            Require(trace.MotionDirection == expectedDirection &&
                    trace.DisplayDirection == expectedDirection,
                $"{label} did not follow the projected screen segment: {trace}.");
            Require(OfficeWorkActionFrameSet.TryResolveNamedDirection(
                        harness.Renderer.sprite,
                        out int spriteDirection) &&
                    spriteDirection == expectedDirection,
                $"{label} consumed the wrong visible body sprite: {trace}.");
        }

        private static void ValidateTileCenterRouting()
        {
            var pathfinder = new DeterministicOfficePathfinder(
                new OfficeNavBounds(0f, 0f, 5f, 5f),
                1f,
                Array.Empty<OfficeNavObstacle>(),
                0.1f,
                0f);
            Require(pathfinder.TryFindPath(
                    new OfficeNavPoint(0.5f, 0.5f),
                    new OfficeNavPoint(4.5f, 4.5f),
                    out OfficeNavPath path),
                "Tile-center route fixture did not produce a path.");
            Require(path.Waypoints.Count == 9,
                $"Tile-center route skipped cells: points={path.Waypoints.Count}.");
            for (var index = 0; index < path.Waypoints.Count; index++)
            {
                OfficeNavPoint point = path.Waypoints[index];
                Require(Math.Abs(point.X - (Math.Floor(point.X) + 0.5f)) <= 0.000001f &&
                        Math.Abs(point.Z - (Math.Floor(point.Z) + 0.5f)) <= 0.000001f,
                    $"Route point {index} left its tile center: {point}.");
                if (index == 0) continue;
                OfficeNavPoint previous = path.Waypoints[index - 1];
                float dx = Math.Abs(point.X - previous.X);
                float dz = Math.Abs(point.Z - previous.Z);
                Require((Math.Abs(dx - 1f) <= 0.000001f && dz <= 0.000001f) ||
                        (Math.Abs(dz - 1f) <= 0.000001f && dx <= 0.000001f),
                    $"Route segment {index - 1}->{index} did not move center-to-center: " +
                    $"({previous.X:R},{previous.Z:R})->({point.X:R},{point.Z:R}).");
            }
        }

        private static void ValidateReferenceCadencePlantedFootGait()
        {
            float stride = OfficeLocomotionGaitRules.DefaultStrideLength;
            float expectedStride = OfficeRuntimeAgent.DefaultMoveSpeed *
                                   OfficeLocomotionGaitRules.ReferenceWalkCycleSeconds;
            Require(Mathf.Abs(expectedStride - stride) <= 0.000001f,
                $"Walk cycle does not match reference speed/cycle: expected={expectedStride:R} stride={stride:R}.");

            float stepsPerSecond = OfficeRuntimeAgent.DefaultMoveSpeed / stride * 2f;
            Require(stepsPerSecond >= 1.9f && stepsPerSecond <= 2.1f,
                $"Normal walking cadence is outside the one-tile reference range: {stepsPerSecond:F3} steps/s.");
            Require(OfficeLocomotionGaitRules.DistanceFrame(0f, stride, 6) == 0 &&
                    OfficeLocomotionGaitRules.DistanceFrame(stride * 0.5f, stride, 6) == 3 &&
                    OfficeLocomotionGaitRules.DistanceFrame(stride, stride, 6) == 0,
                "Left/right foot contacts do not exchange at half-stride and close at the cycle boundary.");

            Require(Mathf.Abs(OfficeLocomotionGaitRules.PlantedFootPresentationOffset(0f, stride)) <=
                    0.000001f &&
                    Mathf.Abs(OfficeLocomotionGaitRules.PlantedFootPresentationOffset(
                        stride * 0.5f,
                        stride)) <= 0.000001f &&
                    Mathf.Abs(OfficeLocomotionGaitRules.PlantedFootPresentationOffset(stride, stride)) <=
                    0.000001f,
                "Visual root offset is discontinuous at a planted-foot or tile-center contact.");

            float sampleDistance = stride * 0.01f;
            float plantAdvance = sampleDistance +
                                 OfficeLocomotionGaitRules.PlantedFootPresentationOffset(
                                     sampleDistance,
                                     stride);
            float swingStart = stride * 0.25f;
            float swingAdvance = sampleDistance +
                                 OfficeLocomotionGaitRules.PlantedFootPresentationOffset(
                                     swingStart + sampleDistance,
                                     stride) -
                                 OfficeLocomotionGaitRules.PlantedFootPresentationOffset(
                                     swingStart,
                                     stride);
            Require(plantAdvance < sampleDistance * 0.10f &&
                    swingAdvance > sampleDistance * 1.35f,
                $"Foot planting did not slow contact and recover during swing: " +
                $"plant={plantAdvance:R} swing={swingAdvance:R} logical={sampleDistance:R}.");
        }

        private static void ValidateActorScopedDebtTransitionsAndRoundRobin()
        {
            float idleDebt = 0f;
            for (var frame = 0; frame < 10 * 60 * 60; frame++)
            {
                OfficeVisibleMotionBudget idle =
                    OfficeRuntimeWorld.ConsumeActorVisibleMotionBudget(
                        false,
                        idleDebt,
                        1f / 60f);
                idleDebt = idle.RemainingDebtSeconds;
            }
            Require(idleDebt <= 0.0000001f,
                "Ten minutes of idle/work accumulated visible motion debt.");

            OfficeVisibleMotionBudget firstRouteFrame =
                OfficeRuntimeWorld.ConsumeActorVisibleMotionBudget(
                    true,
                    idleDebt,
                    1f / 60f);
            Require(firstRouteFrame.RemainingDebtSeconds <= 0.0000001f &&
                    OfficeRuntimeAgent.DefaultMoveSpeed * firstRouteFrame.ConsumedSeconds <=
                    0.099001f,
                "A route starting after long idle inherited debt or exceeded its first-frame cap.");

            var mixedDebt = new float[4];
            bool[] oneMoving = { true, false, false, false };
            for (var actor = 0; actor < mixedDebt.Length; actor++)
            {
                OfficeVisibleMotionBudget budget =
                    OfficeRuntimeWorld.ConsumeActorVisibleMotionBudget(
                        oneMoving[actor],
                        mixedDebt[actor],
                        0.500f);
                mixedDebt[actor] = budget.RemainingDebtSeconds;
            }
            Require(mixedDebt[0] > 0.41f &&
                    mixedDebt.Skip(1).All(value => value <= 0.0000001f),
                "One moving actor transferred hitch debt to an idle/work sibling.");

            bool[] mixedIntent = { true, false, true, false };
            for (var actor = 0; actor < mixedDebt.Length; actor++)
            {
                OfficeVisibleMotionBudget budget =
                    OfficeRuntimeWorld.ConsumeActorVisibleMotionBudget(
                        mixedIntent[actor],
                        mixedDebt[actor],
                        0.200f);
                mixedDebt[actor] = budget.RemainingDebtSeconds;
            }
            Require(mixedDebt[0] > 0f && mixedDebt[2] > 0f &&
                    mixedDebt[1] <= 0.0000001f && mixedDebt[3] <= 0.0000001f,
                "Mixed active/idle actor debt ownership was not isolated.");

            float transitionDebt = mixedDebt[0];
            string[] inactiveTransitions =
            {
                "route-cancelled",
                "seat-claim-failed",
                "blocked-no-intent",
                "route-complete"
            };
            foreach (string transition in inactiveTransitions)
            {
                transitionDebt = OfficeRuntimeWorld.ConsumeActorVisibleMotionBudget(
                        false,
                        transitionDebt,
                        1f / 60f)
                    .RemainingDebtSeconds;
                Require(transitionDebt <= 0.0000001f,
                    transition + " did not clear actor debt immediately.");
                // A replan/departure starts a new route with zero old debt. A hitch may create
                // new debt, but its first rendered displacement remains bounded.
                OfficeVisibleMotionBudget restarted =
                    OfficeRuntimeWorld.ConsumeActorVisibleMotionBudget(
                        true,
                        transitionDebt,
                        0.500f);
                Require(restarted.ConsumedSeconds <=
                        OfficeRuntimeWorld.MaximumVisibleMotionDeltaSeconds + 0.0000001f &&
                        OfficeRuntimeAgent.DefaultMoveSpeed * restarted.ConsumedSeconds <=
                        0.099001f,
                    transition + " restart exceeded its first rendered movement budget.");
                transitionDebt = restarted.RemainingDebtSeconds;
            }

            int drainFrames = 0;
            while (transitionDebt > 0.0000001f && drainFrames < 120)
            {
                OfficeVisibleMotionBudget drained =
                    OfficeRuntimeWorld.ConsumeActorVisibleMotionBudget(
                        true,
                        transitionDebt,
                        1f / 60f);
                transitionDebt = drained.RemainingDebtSeconds;
                drainFrames++;
            }
            Require(transitionDebt <= 0.0000001f && drainFrames < 120,
                "Active route backlog did not drain in finite render time.");

            ValidateRoundRobinRegistrationOrderDeterminism();
            Debug.Log(
                "ACTOR_SCOPED_MOTION_DEBT_TRACE | idleMinutes=10 actors=4 " +
                "oneActive=pass mixedActiveIdle=pass cancelReplan=pass " +
                "seatFailure=pass departure=pass firstFrame<=0.099 backlog=0 " +
                $"drainFrames={drainFrames} roundRobinDeterministic=pass");
        }

        private static void ValidateRoundRobinRegistrationOrderDeterminism()
        {
            string[] canonical = { "father", "mother", "older_sister", "player" };
            string[][] registrations =
            {
                new[] { "player", "older_sister", "father", "mother" },
                new[] { "mother", "father", "player", "older_sister" },
                new[] { "older_sister", "player", "mother", "father" }
            };
            string expectedSchedule = null;
            foreach (string[] registration in registrations)
            {
                var registry = new OfficeRuntimeActorRegistry();
                var objects = new List<GameObject>();
                try
                {
                    FieldInfo idField = typeof(OfficeRuntimeAgent).GetField(
                        "_agentId",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    Require(idField != null, "OfficeRuntimeAgent ID field was unavailable.");
                    foreach (string id in registration)
                    {
                        var item = new GameObject("round-robin-" + id);
                        objects.Add(item);
                        OfficeRuntimeAgent agent = item.AddComponent<OfficeRuntimeAgent>();
                        idField.SetValue(agent, id);
                        registry.Register(agent);
                    }
                    string[] ordered = registry.Actors.Select(item => item.AgentId).ToArray();
                    Require(ordered.SequenceEqual(canonical),
                        "Runtime actor registry order depends on registration order.");
                    int[] stepCounts = { 3, 1, 2, 3 };
                    var schedule = new List<string>();
                    for (var step = 0; step < stepCounts.Max(); step++)
                    for (var actor = 0; actor < ordered.Length; actor++)
                    {
                        if (step < stepCounts[actor])
                            schedule.Add(ordered[actor] + ":" + step);
                    }
                    string serialized = string.Join(",", schedule);
                    if (expectedSchedule == null) expectedSchedule = serialized;
                    else Require(serialized == expectedSchedule,
                        "Round-robin substep schedule changed with registration order.");
                    for (var actor = 0; actor < ordered.Length; actor++)
                    {
                        int actorIndex = actor;
                        int first = schedule.FindIndex(value =>
                            value == ordered[actorIndex] + ":0");
                        int second = schedule.FindIndex(value =>
                            value == ordered[actorIndex] + ":1");
                        if (stepCounts[actor] > 1)
                            Require(second - first >= ordered.Length - 1,
                                ordered[actor] + " consumed all substeps before its peers.");
                    }
                }
                finally
                {
                    foreach (GameObject item in objects) Object.DestroyImmediate(item);
                }
            }
        }

        private static void ValidateCanonicalFurniturePathDetours()
        {
            var random = new System.Random(240815);
            List<OfficeFurnitureDefinition> definitions = OfficeFurnitureCatalog.All
                .Where(item => item.IsPlayerEditable && item.BlocksNavigation)
                .OrderBy(item => item.DefinitionId, StringComparer.Ordinal)
                .ToList();
            Require(definitions.Count > 0, "No blocking player-editable furniture definitions exist.");
            foreach (OfficeFurnitureDefinition definition in definitions)
            foreach (OfficeFurnitureFacing facing in Enum.GetValues(typeof(OfficeFurnitureFacing)))
            {
                OfficeGridCoordinate footprint = definition.FootprintFor(facing);
                int originX = random.Next(5, 8);
                int originY = random.Next(5, 8);
                var origin = new OfficeGridCoordinate(originX, originY);
                SemanticOfficeGrid grid = CreateFurnitureGrid(definition, facing, origin, footprint);
                using (var harness = new OccupancyHarness(grid))
                {
                    Require(harness.Occupancy.CanonicalGeometryObstacleCount == 1,
                        $"{definition.DefinitionId}/{facing} did not enter canonical runtime occupancy.");
                    Require(harness.Occupancy.LegacyCollisionFallbackCount == 0 &&
                            harness.Occupancy.FullCellFallbackCount == 0,
                        $"{definition.DefinitionId}/{facing} used a legacy/full-cell collision fallback.");
                    var start = new OfficeGridCoordinate(1, originY);
                    var goal = new OfficeGridCoordinate(11, originY);
                    string actor = "furniture-path-" + definition.DefinitionId;
                    harness.Register(actor, start);
                    var paths = new OfficeRuntimePathService(grid, harness.Occupancy, harness.Presenter);
                    IReadOnlyList<OfficeGridCoordinate> path = paths.FindPath(
                        actor,
                        start,
                        goal,
                        string.Empty,
                        false,
                        OfficeRuntimeAgent.DefaultRadius);
                    Require(path.Count > 2,
                        $"{definition.DefinitionId}/{facing} left no radius-safe furniture detour.");
                    for (var index = 1; index < path.Count; index++)
                    {
                        Require(harness.Occupancy.CanTraverseStatic(
                                harness.Position(path[index - 1]),
                                harness.Position(path[index]),
                                OfficeRuntimeAgent.DefaultRadius,
                                string.Empty),
                            $"{definition.DefinitionId}/{facing} path segment {index - 1}->{index} crossed geometry.");
                    }
                }
            }
            Debug.Log(
                $"FURNITURE_PATH_TRACE | definitions={definitions.Count} " +
                $"facings={definitions.Count * 4} pathRetries=0 penetrations=0");
        }

        private static SemanticOfficeGrid CreateFurnitureGrid(
            OfficeFurnitureDefinition definition,
            OfficeFurnitureFacing facing,
            OfficeGridCoordinate origin,
            OfficeGridCoordinate footprint)
        {
            const int width = 13;
            const int height = 13;
            var floor = Enumerable.Repeat(OfficeFloorTileKind.WarmWoodA, width * height).ToArray();
            var walkable = Enumerable.Repeat(true, width * height).ToArray();
            for (var y = origin.Y; y < origin.Y + footprint.Y; y++)
            for (var x = origin.X; x < origin.X + footprint.X; x++)
                walkable[y * width + x] = false;
            var furniture = new[]
            {
                new PlacedOfficeFurniture(
                    "random-" + definition.DefinitionId,
                    definition.DefinitionId,
                    origin,
                    footprint.X,
                    footprint.Y,
                    facing,
                    true)
            };
            return new SemanticOfficeGrid(width, height, floor, walkable, furniture);
        }

        private static SemanticOfficeGrid CreateAttendanceFurnitureGrid(int layout)
        {
            const int width = 13;
            const int height = 13;
            var floor = Enumerable.Repeat(OfficeFloorTileKind.WarmWoodA, width * height).ToArray();
            var walkable = Enumerable.Repeat(true, width * height).ToArray();
            var furniture = new List<PlacedOfficeFurniture>();
            switch (layout)
            {
                case 0:
                    AddBlockingFurniture(furniture, walkable, width, "plant-a", OfficeGridLayouts.PottedPlantKind, 5, 5, OfficeFurnitureFacing.SouthEast);
                    AddBlockingFurniture(furniture, walkable, width, "plant-b", OfficeGridLayouts.PottedPlantKind, 7, 6, OfficeFurnitureFacing.NorthWest);
                    AddBlockingFurniture(furniture, walkable, width, "filing-a", OfficeGridLayouts.FilingCabinetKind, 9, 5, OfficeFurnitureFacing.NorthEast);
                    break;
                case 1:
                    AddBlockingFurniture(furniture, walkable, width, "partition-a", OfficeGridLayouts.PartitionKind, 4, 4, OfficeFurnitureFacing.SouthEast);
                    AddBlockingFurniture(furniture, walkable, width, "partition-b", OfficeGridLayouts.PartitionKind, 9, 5, OfficeFurnitureFacing.NorthWest);
                    AddBlockingFurniture(furniture, walkable, width, "coffee-a", OfficeGridLayouts.CoffeeTableKind, 6, 7, OfficeFurnitureFacing.SouthEast);
                    break;
                case 2:
                    AddBlockingFurniture(furniture, walkable, width, "coffee-b", OfficeGridLayouts.CoffeeTableKind, 3, 5, OfficeFurnitureFacing.NorthEast);
                    AddBlockingFurniture(furniture, walkable, width, "sofa-a", OfficeGridLayouts.SofaKind, 8, 6, OfficeFurnitureFacing.SouthWest);
                    AddBlockingFurniture(furniture, walkable, width, "plant-c", OfficeGridLayouts.PottedPlantKind, 6, 4, OfficeFurnitureFacing.SouthWest);
                    AddBlockingFurniture(furniture, walkable, width, "plant-d", OfficeGridLayouts.PottedPlantKind, 6, 8, OfficeFurnitureFacing.NorthEast);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(layout));
            }
            return new SemanticOfficeGrid(width, height, floor, walkable, furniture);
        }

        private static void AddBlockingFurniture(
            ICollection<PlacedOfficeFurniture> furniture,
            bool[] walkable,
            int gridWidth,
            string instanceId,
            string definitionId,
            int originX,
            int originY,
            OfficeFurnitureFacing facing)
        {
            OfficeFurnitureDefinition definition = OfficeFurnitureCatalog.Require(definitionId);
            OfficeGridCoordinate footprint = definition.FootprintFor(facing);
            var origin = new OfficeGridCoordinate(originX, originY);
            for (var y = originY; y < originY + footprint.Y; y++)
            for (var x = originX; x < originX + footprint.X; x++)
                walkable[y * gridWidth + x] = false;
            furniture.Add(new PlacedOfficeFurniture(
                instanceId,
                definitionId,
                origin,
                footprint.X,
                footprint.Y,
                facing,
                true));
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private sealed class AnimatorHarness : IDisposable
        {
            private readonly GameObject _root;
            private readonly Texture2D _texture;
            private readonly List<Sprite> _sprites = new List<Sprite>();

            public AnimatorHarness()
            {
                _root = new GameObject("MovementFacingAnimatorQa");
                Renderer = _root.AddComponent<SpriteRenderer>();
                Renderer.flipX = true;
                Animator = _root.AddComponent<DirectionalSpriteAnimator>();
                _texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                _texture.SetPixels(Enumerable.Repeat(Color.white, 4).ToArray());
                _texture.Apply(false, false);

                var walk = new Sprite[DirectionalSpriteAnimator.RequiredFrameCount];
                for (var frame = 0; frame < DirectionalSpriteAnimator.WalkFrameCount; frame++)
                for (var direction = 0; direction < DirectionalSpriteAnimator.DirectionCount; direction++)
                    walk[frame * DirectionalSpriteAnimator.DirectionCount + direction] =
                        CreateSprite($"qa_walk_f{frame}_{DirectionTokens[direction]}");
                var idle = new Sprite[DirectionalSpriteAnimator.DirectionCount];
                for (var direction = 0; direction < idle.Length; direction++)
                    idle[direction] = CreateSprite("qa_idle_" + DirectionTokens[direction]);
                var transitions = new Sprite[
                    DirectionalSpriteAnimator.RequiredLocomotionTransitionFrameCount];
                for (var clip = 0; clip < DirectionalSpriteAnimator.LocomotionTransitionClipCount; clip++)
                for (var direction = 0; direction < DirectionalSpriteAnimator.DirectionCount; direction++)
                for (var pose = 0; pose < DirectionalSpriteAnimator.LocomotionTransitionPoseCount; pose++)
                {
                    int index = clip * DirectionalSpriteAnimator.LocomotionTransitionFramesPerClip +
                                direction * DirectionalSpriteAnimator.LocomotionTransitionPoseCount + pose;
                    transitions[index] = CreateSprite(
                        $"qa_transition_c{clip}_p{pose}_{DirectionTokens[direction]}");
                }
                Animator.Configure(Renderer, walk);
                Animator.ConfigureLocomotion(idle, OfficeLocomotionGaitRules.DefaultStrideLength);
                Animator.ConfigureLocomotionTransitions(transitions);
                Require(!Renderer.flipX, "Animator Configure did not clear stale SpriteRenderer.flipX.");
            }

            public DirectionalSpriteAnimator Animator { get; }
            public SpriteRenderer Renderer { get; }

            public DirectionalLocomotionFrameTrace Step(Vector2 direction, bool translate)
            {
                Vector2 semanticVelocity = translate ? direction.normalized * 1.5f : Vector2.zero;
                Vector2 actual = translate ? direction.normalized * 0.025f : Vector2.zero;
                // Inject the stale legacy state immediately before the consumer runs. ApplyFrame
                // must clear it in this same frame, not merely once during Configure.
                Renderer.flipX = true;
                Animator.BeginTilePresentationFrame();
                Animator.AccumulateTileMotion(semanticVelocity, actual, FrameDeltaTime, false);
                Animator.Tick(FrameDeltaTime);
                DirectionalLocomotionFrameTrace trace = Animator.CaptureLocomotionFrameTrace();
                Animator.EndTilePresentationFrame();
                return trace;
            }

            private Sprite CreateSprite(string name)
            {
                Sprite sprite = Sprite.Create(
                    _texture,
                    new Rect(0f, 0f, 2f, 2f),
                    new Vector2(0.5f, 0.5f),
                    32f);
                sprite.name = name;
                _sprites.Add(sprite);
                return sprite;
            }

            public void Dispose()
            {
                if (_root != null) Object.DestroyImmediate(_root);
                foreach (Sprite sprite in _sprites)
                    if (sprite != null) Object.DestroyImmediate(sprite);
                if (_texture != null) Object.DestroyImmediate(_texture);
            }
        }

        private sealed class OccupancyHarness : IDisposable
        {
            private readonly GameObject _root;
            private readonly Tile[] _tiles;

            public OccupancyHarness(SemanticOfficeGrid grid)
            {
                Grid = grid ?? throw new ArgumentNullException(nameof(grid));
                _root = new GameObject("MovementFacingOccupancyQa");
                _tiles = new[]
                {
                    ScriptableObject.CreateInstance<Tile>(),
                    ScriptableObject.CreateInstance<Tile>(),
                    ScriptableObject.CreateInstance<Tile>()
                };
                Presenter = _root.AddComponent<OfficeGridTilemapPresenter>();
                Presenter.Configure(Grid, _tiles);
                Occupancy = new OfficeRuntimeOccupancy();
                Occupancy.Rebuild(Grid, Presenter);
            }

            public OfficeGridTilemapPresenter Presenter { get; }
            public OfficeRuntimeOccupancy Occupancy { get; }
            public SemanticOfficeGrid Grid { get; }

            public Vector2 Position(OfficeGridCoordinate cell)
            {
                Vector3 position = Presenter.CellCenterWorld(cell);
                return new Vector2(position.x, position.y);
            }

            public void Register(string actorId, OfficeGridCoordinate cell)
            {
                Occupancy.RegisterActor(
                    actorId,
                    Position(cell),
                    OfficeRuntimeAgent.DefaultRadius);
            }

            public void Dispose()
            {
                if (_root != null) Object.DestroyImmediate(_root);
                foreach (Tile tile in _tiles)
                    if (tile != null) Object.DestroyImmediate(tile);
            }
        }

        private sealed class ProductionAnimatorHarness : IDisposable
        {
            private readonly GameObject _root;

            public ProductionAnimatorHarness(string memberId, Sprite[] walkFrames)
            {
                MemberId = memberId;
                _root = new GameObject("MovementFacingProductionAnimatorQa_" + memberId);
                Renderer = _root.AddComponent<SpriteRenderer>();
                Animator = _root.AddComponent<DirectionalSpriteAnimator>();
                Animator.Configure(Renderer, walkFrames);
            }

            public string MemberId { get; }
            public SpriteRenderer Renderer { get; }
            public DirectionalSpriteAnimator Animator { get; }

            public DirectionalLocomotionFrameTrace Step(Vector2 direction)
            {
                Renderer.flipX = true;
                Animator.BeginTilePresentationFrame();
                Animator.AccumulateTileMotion(
                    direction.normalized * 1.5f,
                    direction.normalized * 0.025f,
                    FrameDeltaTime,
                    false);
                Animator.Tick(FrameDeltaTime);
                DirectionalLocomotionFrameTrace trace =
                    Animator.CaptureLocomotionFrameTrace();
                Animator.EndTilePresentationFrame();
                return trace;
            }

            public void Dispose()
            {
                if (_root != null) Object.DestroyImmediate(_root);
            }
        }
    }
}
