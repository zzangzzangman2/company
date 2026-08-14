using System;
using System.Collections.Generic;
using System.Linq;
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
            new Vector2(-1f, -1f).normalized,
            Vector2.left,
            new Vector2(-1f, 1f).normalized,
            Vector2.up,
            new Vector2(1f, 1f).normalized,
            Vector2.right,
            new Vector2(1f, -1f).normalized
        };

        [MenuItem("Family Company/Validate Movement Facing Navigation")]
        public static void Run()
        {
            OfficeNavigationRegressionReport navigation = OfficeNavigationRegressionSuite.Run();
            OfficeSharedLocomotionStrictReport strict = OfficeSharedLocomotionStrictValidation.Run();
            ValidateSameFrameLateralSpriteConsumption();
            ValidateEightDirectionsAndStoppedFacing();
            ValidateVisibleMotionFrameCap();
            ValidateFourActorAttendanceIngressReservation();
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
                ValidateLateralRun(harness, Vector2.left, 2, "west");
                ValidateLateralRun(harness, Vector2.right, 6, "east");
            }
            ValidateProductionLateralSpriteAssets();
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
                    ValidateProductionLateralRun(harness, Vector2.left, 2, "west");
                    ValidateProductionLateralRun(harness, Vector2.right, 6, "east");
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
            }
        }

        private static void ValidateEightDirectionsAndStoppedFacing()
        {
            using (var harness = new AnimatorHarness())
            {
                for (var direction = 0; direction < DirectionVectors.Length; direction++)
                {
                    harness.Animator.RestoreStandingFacing(direction);
                    DirectionalLocomotionFrameTrace trace = harness.Step(DirectionVectors[direction], true);
                    Require(trace.MotionDirection == direction && trace.DisplayDirection == direction,
                        $"8-way runtime frame mismatch for {DirectionTokens[direction]}: {trace}.");
                    Require(OfficeWorkActionFrameSet.TryResolveNamedDirection(
                                harness.Renderer.sprite,
                                out int spriteDirection) &&
                            spriteDirection == direction,
                        $"8-way sprite consumer mismatch for {DirectionTokens[direction]}: {trace}.");
                }

                harness.Animator.RestoreStandingFacing(6);
                harness.Step(Vector2.right, true);
                DirectionalLocomotionFrameTrace stopped = default;
                for (var frame = 0; frame < 20; frame++)
                    stopped = harness.Step(Vector2.zero, false);
                Require(stopped.Phase == OfficeLocomotionPhase.Idle && !stopped.IsMoving,
                    "Stopped runtime actor did not settle to Idle: " + stopped);
                Require(stopped.DisplayDirection == 6,
                    "Stopped runtime actor did not retain its last natural East facing: " + stopped);
                Require(OfficeWorkActionFrameSet.TryResolveNamedDirection(
                            harness.Renderer.sprite,
                            out int stoppedSpriteDirection) &&
                        stoppedSpriteDirection == 6,
                    "Stopped sprite consumer did not retain East: " + stopped);
            }
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
            foreach (var worldScale in new[] { 1f, 4f })
            using (var harness = new OccupancyHarness(CreateAttendanceFurnitureGrid(2)))
            {
                string actorId = "visible-budget-" + worldScale.ToString("F0");
                var startCell = new OfficeGridCoordinate(1, 1);
                var goalCell = new OfficeGridCoordinate(11, 11);
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
                for (var frame = 0; frame < 900; frame++)
                {
                    float unscaledFrameDelta = frame == 4
                        ? 0.200f
                        : frame == 11
                            ? 0.500f
                            : 1f / 60f;
                    // A 4x world clock produces a larger scaled simulation delta, but visible
                    // navigation owns an unscaled debt so normal 60 Hz frames cannot accumulate it.
                    float scaledSimulationDelta = unscaledFrameDelta * worldScale;
                    Require(scaledSimulationDelta >= unscaledFrameDelta,
                        "World-scale test input was invalid.");
                    OfficeVisibleMotionBudget budget =
                        OfficeRuntimeWorld.ConsumeVisibleMotionBudget(
                            debt,
                            unscaledFrameDelta);
                    debt = budget.RemainingDebtSeconds;
                    totalIncoming += unscaledFrameDelta;
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
                        while (remainingBudget > 0.0000001f && pathIndex < path.Count)
                        {
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

                Require(drainedAtFrame >= 0 && drainedAtFrame < 900,
                    $"{worldScale:F0}x motion debt did not drain or route did not arrive.");
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
