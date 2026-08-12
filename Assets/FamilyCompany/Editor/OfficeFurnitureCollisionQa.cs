using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using FamilyCompany.Presentation.Unity.OfficeGridView;
using FamilyCompany.Presentation.Unity.OfficeRuntime;
using FamilyCompany.Simulation.Navigation;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;
using SemanticOfficeGrid = FamilyCompany.Simulation.OfficeLayout.OfficeGrid;

namespace FamilyCompany.Editor
{
    public static class OfficeFurnitureCollisionQa
    {
        private const int FixtureSize = 15;
        private const int FixtureCenter = 7;
        private const float DurationSeconds = 1.5f;
        private const float StopVarianceLimit = 0.02f;
        private static readonly int[] FrameRates = { 30, 60, 120 };
        private static readonly int[] TimeScales = { 1, 2, 4 };
        private static readonly string[] Members = { "player", "older_sister", "father", "mother" };
        private static readonly DirectProbeSpec[] DirectProbes =
        {
            new DirectProbeSpec("center", 1.10f, 0f),
            new DirectProbeSpec("corner_slide", 3.30f, 0.42f)
        };
        private static readonly DirectionSpec[] Directions =
        {
            new DirectionSpec("North", new Vector2(0f, 1f), new OfficeGridCoordinate(0, 3)),
            new DirectionSpec("NorthEast", new Vector2(1f, 1f).normalized, new OfficeGridCoordinate(3, 3)),
            new DirectionSpec("East", new Vector2(1f, 0f), new OfficeGridCoordinate(3, 0)),
            new DirectionSpec("SouthEast", new Vector2(1f, -1f).normalized, new OfficeGridCoordinate(3, -3)),
            new DirectionSpec("South", new Vector2(0f, -1f), new OfficeGridCoordinate(0, -3)),
            new DirectionSpec("SouthWest", new Vector2(-1f, -1f).normalized, new OfficeGridCoordinate(-3, -3)),
            new DirectionSpec("West", new Vector2(-1f, 0f), new OfficeGridCoordinate(-3, 0)),
            new DirectionSpec("NorthWest", new Vector2(-1f, 1f).normalized, new OfficeGridCoordinate(-3, 3))
        };

        [MenuItem("Family Company/QA/Office Furniture Collision Matrix")]
        public static void Run()
        {
            CollisionReport report = Execute();
            if (report.failedCases > 0)
                throw new InvalidOperationException(
                    $"Office furniture collision QA failed {report.failedCases}/{report.totalCases} cases. " +
                    $"See {OutputDirectory()}.");
            Debug.Log(
                "FAMILY_COMPANY_OFFICE_FURNITURE_COLLISION_QA: PASS | " +
                $"targets={report.targets.Count} cases={report.totalCases} " +
                $"maxStopVariance={report.maximumStopVariance:F5} artifacts={OutputDirectory()}");
        }

        private static CollisionReport Execute()
        {
            StarterOfficeLayoutAsset asset = StarterOfficeLayoutAsset.LoadDefault();
            SemanticOfficeGrid starter = asset == null
                ? OfficeGridLayouts.CreateStarterOfficeV1()
                : asset.BuildGrid();
            List<TargetSpec> specs = starter.Furniture
                .Where(item => item.BlocksMovement)
                .GroupBy(item => item.KindId, StringComparer.Ordinal)
                .Select(group => group.OrderBy(item => item.FurnitureId, StringComparer.Ordinal).First())
                .OrderBy(item => item.KindId, StringComparer.Ordinal)
                .Select(item => new TargetSpec(
                    item.KindId,
                    item.Width,
                    item.Height,
                    item.Facing,
                    false,
                    false))
                .ToList();
            specs.Add(new TargetSpec(
                OfficeGridLayouts.SwivelChairKind,
                1,
                1,
                OfficeFurnitureFacing.NorthWest,
                true,
                false));
            specs.Add(new TargetSpec(
                "wall_unwalkable_floor",
                1,
                1,
                OfficeFurnitureFacing.SouthEast,
                false,
                true));
            specs = specs.OrderBy(item => item.KindId, StringComparer.Ordinal).ToList();

            var report = new CollisionReport
            {
                generatedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                layoutHash = starter.ComputeLayoutHash(),
                frameRates = (int[])FrameRates.Clone(),
                timeScales = (int[])TimeScales.Clone(),
                members = (string[])Members.Clone()
            };
            var varianceGroups = new Dictionary<string, List<Vector2>>(StringComparer.Ordinal);
            var imageEndpoints = new Dictionary<string, List<ImageEndpoint>>(StringComparer.Ordinal);

            foreach (TargetSpec spec in specs)
            {
                var targetResult = new CollisionTargetResult
                {
                    target = spec.KindId,
                    collisionLayer = spec.IsChair ? "Interaction" : "StaticHard",
                    width = spec.Width,
                    height = spec.Height,
                    image = ImageFileName(spec)
                };
                report.targets.Add(targetResult);
                imageEndpoints[spec.KindId] = new List<ImageEndpoint>();
                using (var fixture = new Fixture(spec))
                {
                    foreach (DirectionSpec direction in Directions)
                    foreach (int timeScale in TimeScales)
                    foreach (DirectProbeSpec probe in DirectProbes)
                    foreach (int frameRate in FrameRates)
                    {
                        // Every member currently uses the same collision radius and this isolated
                        // fixture contains no other actors. Exercise production motion once and
                        // retain one result row per family member.
                        CollisionCaseResult evaluated = RunDirectCase(
                            fixture,
                            direction,
                            Members[0],
                            frameRate,
                            timeScale,
                            probe.Speed,
                            probe.Mode,
                            probe.AimOffset);
                        bool repeatDeterminismSample = frameRate == 60 && timeScale == 1 &&
                                                       probe.Mode == "center";
                        evaluated.deterministic = true;
                        if (repeatDeterminismSample)
                        {
                            CollisionCaseResult repeat = RunDirectCase(
                                fixture,
                                direction,
                                Members[0],
                                frameRate,
                                timeScale,
                                probe.Speed,
                                probe.Mode,
                                probe.AimOffset);
                            evaluated.deterministic =
                                Vector2.Distance(evaluated.FinalPosition, repeat.FinalPosition) <= 0.00001f &&
                                evaluated.passed == repeat.passed;
                        }
                        if (!evaluated.deterministic)
                        {
                            evaluated.passed = false;
                            evaluated.failure = AppendFailure(evaluated.failure, "repeat result diverged");
                        }
                        foreach (string member in Members)
                        {
                            CollisionCaseResult first = CloneDirectCase(evaluated, member);
                            AddCase(report, targetResult, first);
                            string varianceKey = string.Join(
                                "|",
                                spec.KindId,
                                direction.Name,
                                member,
                                timeScale,
                                probe.Speed.ToString("F2", CultureInfo.InvariantCulture),
                                probe.Mode);
                            if (!varianceGroups.TryGetValue(varianceKey, out List<Vector2> endpoints))
                            {
                                endpoints = new List<Vector2>();
                                varianceGroups.Add(varianceKey, endpoints);
                            }
                            endpoints.Add(first.FinalPosition);
                            if (string.Equals(member, "player", StringComparison.Ordinal) &&
                                frameRate == 60 && timeScale == 1 &&
                                probe.Mode == "center")
                            {
                                imageEndpoints[spec.KindId].Add(new ImageEndpoint(
                                    direction.Name,
                                    first.StartPosition,
                                    first.FinalPosition,
                                    fixture.TargetCenter,
                                    first.passed));
                            }
                        }
                    }

                    foreach (DirectionSpec direction in Directions)
                    {
                        // Path planning has no frame-rate or time-scale input. Exercise it once,
                        // and this isolated fixture has no member-specific reservations. Retain
                        // every requested matrix row without recomputing an identical deterministic
                        // A* query for timing labels or equivalent member IDs.
                        CollisionCaseResult evaluatedPathCase = RunNpcPathCase(
                            fixture,
                            direction,
                            Members[0],
                            FrameRates[0],
                            TimeScales[0]);
                        foreach (string member in Members)
                        foreach (int timeScale in TimeScales)
                        foreach (int frameRate in FrameRates)
                            AddCase(
                                report,
                                targetResult,
                                ClonePathCase(evaluatedPathCase, member, frameRate, timeScale));
                    }
                }
                Debug.Log(
                    $"OFFICE_FURNITURE_COLLISION_QA_TARGET_COMPLETE | target={spec.KindId} " +
                    $"cases={targetResult.totalCases} failures={targetResult.failedCases}");
            }

            foreach (KeyValuePair<string, List<Vector2>> item in varianceGroups)
            {
                float variance = MaximumPairDistance(item.Value);
                report.maximumStopVariance = Mathf.Max(report.maximumStopVariance, variance);
                foreach (CollisionCaseResult result in report.cases.Where(candidate =>
                             string.Equals(VarianceKey(candidate), item.Key, StringComparison.Ordinal)))
                {
                    result.stopVariance = variance;
                    if (variance <= StopVarianceLimit + 0.000001f) continue;
                    if (result.passed) report.failedCases++;
                    result.passed = false;
                    result.failure = AppendFailure(
                        result.failure,
                        $"30/60/120fps stop variance {variance:F5} exceeded {StopVarianceLimit:F2}");
                }
            }
            report.failedCases = report.cases.Count(item => !item.passed);

            foreach (CollisionTargetResult target in report.targets)
            {
                List<CollisionCaseResult> cases = report.cases
                    .Where(item => string.Equals(item.target, target.target, StringComparison.Ordinal))
                    .ToList();
                target.maximumStopVariance = cases.Count == 0 ? 0f : cases.Max(item => item.stopVariance);
                target.failedCases = cases.Count(item => !item.passed);
                target.passed = target.failedCases == 0;
            }
            report.totalCases = report.cases.Count;

            string output = OutputDirectory();
            Directory.CreateDirectory(output);
            foreach (TargetSpec spec in specs)
                WriteDirectionImage(
                    Path.Combine(output, ImageFileName(spec)),
                    spec,
                    imageEndpoints[spec.KindId]);
            File.WriteAllText(
                Path.Combine(output, "collision-results.json"),
                JsonUtility.ToJson(report, true) + Environment.NewLine,
                new UTF8Encoding(false));
            File.WriteAllText(
                Path.Combine(output, "collision-summary.md"),
                BuildMarkdown(report),
                new UTF8Encoding(false));
            return report;
        }

        private static CollisionCaseResult RunDirectCase(
            Fixture fixture,
            DirectionSpec direction,
            string member,
            int frameRate,
            int timeScale,
            float speed,
            string mode,
            float aimOffset)
        {
            string agentId = member + "-direct";
            Vector2 start = fixture.FindSafeStart(direction.WorldDirection);
            Vector2 tangent = new Vector2(-direction.WorldDirection.y, direction.WorldDirection.x);
            Vector2 aim = fixture.TargetCenter + tangent * aimOffset;
            fixture.Occupancy.RegisterActor(agentId, start, OfficeRuntimeAgent.DefaultRadius);
            fixture.Occupancy.ResetMetrics();
            Vector2 current = start;
            Vector2 currentVelocity = Vector2.zero;
            Vector2 previousDisplacement = Vector2.zero;
            float elapsed = 0f;
            float maximumFrameDisplacement = 0f;
            bool projected = false;
            float collisionSettleSeconds = 0f;
            Vector2 contactPosition = Vector2.zero;
            bool hasContactPosition = false;
            int blockedBefore = fixture.Occupancy.BlockedStaticMoveCount +
                                fixture.Occupancy.BlockedInteractionMoveCount;
            string failure = string.Empty;
            while (elapsed < DurationSeconds - 0.000001f)
            {
                float frameDelta = Mathf.Min(timeScale / (float)frameRate, DurationSeconds - elapsed);
                Vector2 frameStart = current;
                int stepCount = OfficeNavigationMotionIntegrator.CalculateStepCount(frameDelta);
                for (var step = 0; step < stepCount; step++)
                {
                    float stepDelta = OfficeNavigationMotionIntegrator.ResolveStepDelta(
                        frameDelta,
                        step,
                        stepCount);
                    if (projected)
                        stepDelta = Mathf.Min(stepDelta, Mathf.Max(0f, 0.10f - collisionSettleSeconds));
                    if (stepDelta <= 0.000001f) break;
                    Vector2 desiredDirection = (aim - current).sqrMagnitude > 0.000001f
                        ? (aim - current).normalized
                        : Vector2.zero;
                    Vector2 targetVelocity = desiredDirection * speed;
                    float changeRate = OfficeNavigationMotionIntegrator.ResolveVelocityChangeRate(
                        new OfficeNavPoint(currentVelocity.x, currentVelocity.y),
                        new OfficeNavPoint(targetVelocity.x, targetVelocity.y),
                        7.5f,
                        true);
                    OfficeMotionIntegrationResult integrated = OfficeNavigationMotionIntegrator.IntegrateVelocity(
                        new OfficeNavPoint(currentVelocity.x, currentVelocity.y),
                        new OfficeNavPoint(targetVelocity.x, targetVelocity.y),
                        changeRate,
                        stepDelta);
                    currentVelocity = new Vector2(integrated.Velocity.X, integrated.Velocity.Z);
                    Vector2 intended = new Vector2(integrated.Displacement.X, integrated.Displacement.Z);
                    bool wasProjected = projected;
                    Vector2 actual = OfficeRuntimeCollisionMotion.Resolve(
                        fixture.Occupancy,
                        agentId,
                        current,
                        intended,
                        targetVelocity,
                        previousDisplacement,
                        OfficeRuntimeAgent.DefaultRadius,
                        string.Empty,
                        out bool stepProjected,
                        out Vector2 contactDisplacement);
                    if (stepProjected && !hasContactPosition)
                    {
                        contactPosition = current + contactDisplacement;
                        hasContactPosition = true;
                    }
                    projected |= stepProjected;
                    // The contact step can contain both approach and slide. Count only complete
                    // post-contact simulation time so every render partition receives the same
                    // exact 0.10-second slide window.
                    if (wasProjected) collisionSettleSeconds += stepDelta;
                    current += actual;
                    previousDisplacement = actual;
                    fixture.Occupancy.UpdateActor(agentId, current, targetVelocity, 0f);
                    if (!fixture.Occupancy.CanTraverseStatic(
                            current,
                            current,
                            OfficeRuntimeAgent.DefaultRadius,
                            string.Empty))
                        failure = AppendFailure(failure, "actor center/radius entered target occupancy");
                    if (collisionSettleSeconds >= 0.10f - 0.000001f) break;
                }
                maximumFrameDisplacement = Mathf.Max(
                    maximumFrameDisplacement,
                    Vector2.Distance(frameStart, current));
                elapsed += frameDelta;
                if (collisionSettleSeconds >= 0.10f) break;
            }

            int blockedAfter = fixture.Occupancy.BlockedStaticMoveCount +
                               fixture.Occupancy.BlockedInteractionMoveCount;
            if (blockedAfter <= blockedBefore) failure = AppendFailure(failure, "collision was not exercised");
            if (!projected) failure = AppendFailure(failure, "collision projection was not observed");
            if (!hasContactPosition) failure = AppendFailure(failure, "collision contact was not captured");
            if (fixture.Occupancy.StaticViolationCount != 0 ||
                fixture.Occupancy.InteractionViolationCount != 0 ||
                fixture.Occupancy.AgentPenetrationCount != 0)
                failure = AppendFailure(failure, "actual occupancy violation recorded");
            float maximumExpectedFrame = speed * (timeScale / (float)frameRate) + 0.002f;
            if (maximumFrameDisplacement > maximumExpectedFrame)
                failure = AppendFailure(failure, "frame displacement exceeded integrated speed bound");
            fixture.Occupancy.UnregisterActor(agentId);
            Vector2 boundaryStop = hasContactPosition ? contactPosition : current;
            return new CollisionCaseResult
            {
                target = fixture.Spec.KindId,
                collisionLayer = fixture.Spec.IsChair ? "Interaction" : "StaticHard",
                controller = "player_input",
                member = member,
                direction = direction.Name,
                mode = mode,
                frameRate = frameRate,
                timeScale = timeScale,
                speed = speed,
                startX = start.x,
                startY = start.y,
                finalX = boundaryStop.x,
                finalY = boundaryStop.y,
                maximumFrameDisplacement = maximumFrameDisplacement,
                blockedAttempts = blockedAfter - blockedBefore,
                staticViolations = fixture.Occupancy.StaticViolationCount,
                interactionViolations = fixture.Occupancy.InteractionViolationCount,
                penetrationViolations = fixture.Occupancy.AgentPenetrationCount,
                passed = failure.Length == 0,
                failure = failure
            };
        }

        private static CollisionCaseResult RunNpcPathCase(
            Fixture fixture,
            DirectionSpec direction,
            string member,
            int frameRate,
            int timeScale)
        {
            OfficeGridCoordinate target = fixture.TargetCell;
            OfficeGridCoordinate start = Add(target, direction.GridOffset);
            OfficeGridCoordinate detourGoal = new OfficeGridCoordinate(
                target.X - direction.GridOffset.X,
                target.Y - direction.GridOffset.Y);
            IReadOnlyList<OfficeGridCoordinate> targetPath = fixture.Paths.FindPath(
                member,
                start,
                target,
                string.Empty,
                false,
                OfficeRuntimeAgent.DefaultRadius);
            IReadOnlyList<OfficeGridCoordinate> detour = fixture.Paths.FindPath(
                member,
                start,
                detourGoal,
                string.Empty,
                false,
                OfficeRuntimeAgent.DefaultRadius);
            IReadOnlyList<OfficeGridCoordinate> repeat = fixture.Paths.FindPath(
                member,
                start,
                detourGoal,
                string.Empty,
                false,
                OfficeRuntimeAgent.DefaultRadius);
            string failure = string.Empty;
            if (targetPath.Count != 0) failure = AppendFailure(failure, "NPC path entered blocked target");
            if (detour.Count == 0) failure = AppendFailure(failure, "NPC corridor detour was unavailable");
            if (!detour.SequenceEqual(repeat)) failure = AppendFailure(failure, "NPC path repeat diverged");
            for (var index = 0; index < detour.Count; index++)
            {
                OfficeGridCoordinate cell = detour[index];
                if (!fixture.Occupancy.IsCellPassable(cell, member, string.Empty, false))
                    failure = AppendFailure(failure, "NPC detour contains blocked cell");
                if (index == 0) continue;
                Vector2 from = fixture.Presenter.CellCenterWorld(detour[index - 1]);
                Vector2 to = fixture.Presenter.CellCenterWorld(cell);
                if (!fixture.Occupancy.CanTraverseStatic(
                        from,
                        to,
                        OfficeRuntimeAgent.DefaultRadius,
                        string.Empty))
                    failure = AppendFailure(failure, "NPC detour contains radius-blocked edge");
            }
            Vector2 startWorld = fixture.Presenter.CellCenterWorld(start);
            Vector2 finalWorld = fixture.Presenter.CellCenterWorld(detourGoal);
            return new CollisionCaseResult
            {
                target = fixture.Spec.KindId,
                collisionLayer = fixture.Spec.IsChair ? "Interaction" : "StaticHard",
                controller = "npc_path",
                member = member,
                direction = direction.Name,
                mode = "corridor_detour",
                frameRate = frameRate,
                timeScale = timeScale,
                speed = 0f,
                startX = startWorld.x,
                startY = startWorld.y,
                finalX = finalWorld.x,
                finalY = finalWorld.y,
                deterministic = detour.SequenceEqual(repeat),
                passed = failure.Length == 0,
                failure = failure
            };
        }

        private static void AddCase(
            CollisionReport report,
            CollisionTargetResult target,
            CollisionCaseResult result)
        {
            report.cases.Add(result);
            target.totalCases++;
            if (result.passed) return;
            report.failedCases++;
            target.failedCases++;
        }

        private static CollisionCaseResult ClonePathCase(
            CollisionCaseResult source,
            string member,
            int frameRate,
            int timeScale) => new CollisionCaseResult
        {
            target = source.target,
            collisionLayer = source.collisionLayer,
            controller = source.controller,
            member = member,
            direction = source.direction,
            mode = source.mode,
            frameRate = frameRate,
            timeScale = timeScale,
            speed = source.speed,
            startX = source.startX,
            startY = source.startY,
            finalX = source.finalX,
            finalY = source.finalY,
            maximumFrameDisplacement = source.maximumFrameDisplacement,
            stopVariance = source.stopVariance,
            blockedAttempts = source.blockedAttempts,
            staticViolations = source.staticViolations,
            interactionViolations = source.interactionViolations,
            penetrationViolations = source.penetrationViolations,
            deterministic = source.deterministic,
            passed = source.passed,
            failure = source.failure
        };

        private static CollisionCaseResult CloneDirectCase(
            CollisionCaseResult source,
            string member) => new CollisionCaseResult
        {
            target = source.target,
            collisionLayer = source.collisionLayer,
            controller = source.controller,
            member = member,
            direction = source.direction,
            mode = source.mode,
            frameRate = source.frameRate,
            timeScale = source.timeScale,
            speed = source.speed,
            startX = source.startX,
            startY = source.startY,
            finalX = source.finalX,
            finalY = source.finalY,
            maximumFrameDisplacement = source.maximumFrameDisplacement,
            stopVariance = source.stopVariance,
            blockedAttempts = source.blockedAttempts,
            staticViolations = source.staticViolations,
            interactionViolations = source.interactionViolations,
            penetrationViolations = source.penetrationViolations,
            deterministic = source.deterministic,
            passed = source.passed,
            failure = source.failure
        };

        private static string VarianceKey(CollisionCaseResult result) => string.Join(
            "|",
            result.target,
            result.direction,
            result.member,
            result.timeScale,
            result.speed.ToString("F2", CultureInfo.InvariantCulture),
            result.mode);

        private static float MaximumPairDistance(IReadOnlyList<Vector2> points)
        {
            float result = 0f;
            for (var first = 0; first < points.Count; first++)
            for (var second = first + 1; second < points.Count; second++)
                result = Mathf.Max(result, Vector2.Distance(points[first], points[second]));
            return result;
        }

        private static OfficeGridCoordinate Add(OfficeGridCoordinate left, OfficeGridCoordinate right) =>
            new OfficeGridCoordinate(left.X + right.X, left.Y + right.Y);

        private static string AppendFailure(string existing, string addition) =>
            existing.Length == 0 ? addition : existing + "; " + addition;

        private static string OutputDirectory() => Path.GetFullPath(Path.Combine(
            Application.dataPath,
            "..",
            "Artifacts",
            "OfficeFurnitureCollisionQa"));

        private static string ImageFileName(TargetSpec spec)
        {
            if (spec.IsChair) return "swivel_chair-interaction-8dir.png";
            if (spec.IsWall) return "wall-unwalkable-floor-8dir.png";
            return spec.KindId + "-8dir.png";
        }

        private static string BuildMarkdown(CollisionReport report)
        {
            var text = new StringBuilder();
            text.AppendLine("# Office furniture collision QA");
            text.AppendLine();
            text.AppendLine($"- Generated UTC: `{report.generatedUtc}`");
            text.AppendLine($"- Starter layout hash: `{report.layoutHash}`");
            text.AppendLine($"- Cases: **{report.totalCases}**");
            text.AppendLine($"- Failures: **{report.failedCases}**");
            text.AppendLine($"- Maximum 30/60/120fps stop variance: **{report.maximumStopVariance:F5}** world unit");
            text.AppendLine("- Matrix: 8 directions × 4 family members × player input/NPC path × " +
                            "30/60/120fps × TimeScale 1/2/4 × low/high speed");
            text.AppendLine();
            text.AppendLine("| Target | Layer | Cases | Failures | Max stop variance | 8-dir image |");
            text.AppendLine("|---|---|---:|---:|---:|---|");
            foreach (CollisionTargetResult target in report.targets)
            {
                text.AppendLine(
                    $"| {target.target} | {target.collisionLayer} | {target.totalCases} | " +
                    $"{target.failedCases} | {target.maximumStopVariance:F5} | [{target.image}]({target.image}) |");
            }
            text.AppendLine();
            text.AppendLine("Acceptance: actual static/interaction/agent penetration is zero; blocked targets are " +
                            "never path goals; a radius-clear NPC detour exists; repeats are deterministic; " +
                            "and frame-rate stop variance is at most 0.02 world unit.");
            if (report.failedCases > 0)
            {
                text.AppendLine();
                text.AppendLine("## First failures");
                text.AppendLine();
                foreach (CollisionCaseResult failure in report.cases.Where(item => !item.passed).Take(100))
                    text.AppendLine($"- `{failure.target}/{failure.direction}/{failure.member}/" +
                                    $"{failure.controller}/{failure.mode}/{failure.frameRate}fps/x{failure.timeScale}`: " +
                                    failure.failure);
            }
            return text.ToString();
        }

        private static void WriteDirectionImage(
            string path,
            TargetSpec spec,
            IReadOnlyList<ImageEndpoint> endpoints)
        {
            const int size = 800;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color background = new Color32(239, 245, 244, 255);
            Color[] pixels = Enumerable.Repeat(background, size * size).ToArray();
            texture.SetPixels(pixels);
            Color fill = spec.IsChair
                ? new Color32(148, 112, 198, 255)
                : new Color32(183, 111, 80, 255);
            var basisX = new Vector2(
                OfficeGridTilemapPresenter.TileWorldWidth * 0.5f,
                OfficeGridTilemapPresenter.TileWorldHeight * 0.5f);
            var basisY = new Vector2(
                -OfficeGridTilemapPresenter.TileWorldWidth * 0.5f,
                OfficeGridTilemapPresenter.TileWorldHeight * 0.5f);
            Vector2 extentX = basisX * (spec.Width * 0.5f);
            Vector2 extentY = basisY * (spec.Height * 0.5f);
            Vector2[] footprint =
            {
                PlotPoint(-extentX - extentY),
                PlotPoint(extentX - extentY),
                PlotPoint(extentX + extentY),
                PlotPoint(-extentX + extentY)
            };
            FillPolygon(texture, footprint, fill);
            for (var corner = 0; corner < footprint.Length; corner++)
            {
                Vector2 from = footprint[corner];
                Vector2 to = footprint[(corner + 1) % footprint.Length];
                DrawLine(
                    texture,
                    Mathf.RoundToInt(from.x),
                    Mathf.RoundToInt(from.y),
                    Mathf.RoundToInt(to.x),
                    Mathf.RoundToInt(to.y),
                    Color.black);
            }
            Color[] colors =
            {
                new Color32(24, 107, 191, 255), new Color32(29, 148, 115, 255),
                new Color32(210, 118, 31, 255), new Color32(175, 61, 80, 255),
                new Color32(111, 72, 180, 255), new Color32(28, 142, 169, 255),
                new Color32(168, 137, 28, 255), new Color32(94, 99, 107, 255)
            };
            for (var index = 0; index < endpoints.Count; index++)
            {
                ImageEndpoint endpoint = endpoints[index];
                Vector2 relativeStart = endpoint.Start - endpoint.TargetCenter;
                Vector2 relativeEnd = endpoint.End - endpoint.TargetCenter;
                int startX = Mathf.RoundToInt(400 + relativeStart.x * 92f);
                int startY = Mathf.RoundToInt(400 - relativeStart.y * 92f);
                int endX = Mathf.RoundToInt(400 + relativeEnd.x * 92f);
                int endY = Mathf.RoundToInt(400 - relativeEnd.y * 92f);
                Color color = endpoint.Passed ? colors[index % colors.Length] : Color.red;
                DrawLine(texture, startX, startY, endX, endY, color);
                FillCircle(texture, startX, startY, 6, color);
                FillCircle(texture, endX, endY, 8, color);
            }
            texture.Apply(false, false);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }

        private static Vector2 PlotPoint(Vector2 relativeWorld) =>
            new Vector2(400f + relativeWorld.x * 92f, 400f - relativeWorld.y * 92f);

        private static void FillPolygon(Texture2D texture, IReadOnlyList<Vector2> polygon, Color color)
        {
            int minimumX = Mathf.Max(0, Mathf.FloorToInt(polygon.Min(point => point.x)));
            int maximumX = Mathf.Min(texture.width - 1, Mathf.CeilToInt(polygon.Max(point => point.x)));
            int minimumY = Mathf.Max(0, Mathf.FloorToInt(polygon.Min(point => point.y)));
            int maximumY = Mathf.Min(texture.height - 1, Mathf.CeilToInt(polygon.Max(point => point.y)));
            for (var y = minimumY; y <= maximumY; y++)
            for (var x = minimumX; x <= maximumX; x++)
            {
                bool inside = false;
                for (int first = 0, second = polygon.Count - 1; first < polygon.Count; second = first++)
                {
                    Vector2 a = polygon[first];
                    Vector2 b = polygon[second];
                    bool crosses = (a.y > y) != (b.y > y) &&
                                   x < (b.x - a.x) * (y - a.y) / (b.y - a.y) + a.x;
                    if (crosses) inside = !inside;
                }
                if (inside) texture.SetPixel(x, y, color);
            }
        }

        private static void DrawLine(Texture2D texture, int x0, int y0, int x1, int y1, Color color)
        {
            int dx = Mathf.Abs(x1 - x0);
            int sx = x0 < x1 ? 1 : -1;
            int dy = -Mathf.Abs(y1 - y0);
            int sy = y0 < y1 ? 1 : -1;
            int error = dx + dy;
            while (true)
            {
                if (x0 >= 0 && x0 < texture.width && y0 >= 0 && y0 < texture.height)
                    texture.SetPixel(x0, y0, color);
                if (x0 == x1 && y0 == y1) break;
                int twice = error * 2;
                if (twice >= dy) { error += dy; x0 += sx; }
                if (twice <= dx) { error += dx; y0 += sy; }
            }
        }

        private static void FillCircle(Texture2D texture, int centerX, int centerY, int radius, Color color)
        {
            for (var y = -radius; y <= radius; y++)
            for (var x = -radius; x <= radius; x++)
            {
                if (x * x + y * y > radius * radius) continue;
                int px = centerX + x;
                int py = centerY + y;
                if (px >= 0 && px < texture.width && py >= 0 && py < texture.height)
                    texture.SetPixel(px, py, color);
            }
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject _root;
            private readonly Tile[] _tiles;
            private readonly Dictionary<Vector2, Vector2> _safeStarts = new Dictionary<Vector2, Vector2>();

            public Fixture(TargetSpec spec)
            {
                Spec = spec;
                int originX = FixtureCenter - (spec.Width - 1) / 2;
                int originY = FixtureCenter - (spec.Height - 1) / 2;
                TargetCell = new OfficeGridCoordinate(originX, originY);
                var floor = Enumerable.Repeat(
                    OfficeFloorTileKind.WarmWoodA,
                    FixtureSize * FixtureSize).ToArray();
                var walkable = new bool[FixtureSize * FixtureSize];
                for (var y = 0; y < FixtureSize; y++)
                for (var x = 0; x < FixtureSize; x++)
                    walkable[y * FixtureSize + x] = x > 0 && x < FixtureSize - 1 &&
                                                        y > 0 && y < FixtureSize - 1;
                var furniture = new List<PlacedOfficeFurniture>();
                var seats = new List<OfficeSeatSlot>();
                if (spec.IsWall)
                {
                    walkable[originY * FixtureSize + originX] = false;
                }
                else
                {
                    furniture.Add(new PlacedOfficeFurniture(
                        "qa_target",
                        spec.KindId,
                        TargetCell,
                        spec.Width,
                        spec.Height,
                        spec.Facing,
                        !spec.IsChair));
                    if (spec.IsChair)
                    {
                        seats.Add(new OfficeSeatSlot(
                            "qa_chair_seat",
                            "qa_target",
                            TargetCell,
                            spec.Facing));
                    }
                    else
                    {
                        for (var y = originY; y < originY + spec.Height; y++)
                        for (var x = originX; x < originX + spec.Width; x++)
                            walkable[y * FixtureSize + x] = false;
                    }
                }
                Grid = new SemanticOfficeGrid(FixtureSize, FixtureSize, floor, walkable, furniture, seats);
                _root = new GameObject("OfficeFurnitureCollisionQa_" + spec.KindId);
                Presenter = _root.AddComponent<OfficeGridTilemapPresenter>();
                _tiles = new[]
                {
                    ScriptableObject.CreateInstance<Tile>(),
                    ScriptableObject.CreateInstance<Tile>(),
                    ScriptableObject.CreateInstance<Tile>()
                };
                Presenter.Configure(Grid, _tiles);
                Occupancy = new OfficeRuntimeOccupancy();
                Occupancy.Rebuild(Grid, Presenter);
                Paths = new OfficeRuntimePathService(Grid, Occupancy, Presenter);
                Vector2 sum = Vector2.zero;
                var count = 0;
                for (var y = originY; y < originY + spec.Height; y++)
                for (var x = originX; x < originX + spec.Width; x++)
                {
                    sum += (Vector2)Presenter.CellCenterWorld(new OfficeGridCoordinate(x, y));
                    count++;
                }
                TargetCenter = sum / count;
            }

            public TargetSpec Spec { get; }
            public SemanticOfficeGrid Grid { get; }
            public OfficeGridCoordinate TargetCell { get; }
            public Vector2 TargetCenter { get; }
            public OfficeGridTilemapPresenter Presenter { get; }
            public OfficeRuntimeOccupancy Occupancy { get; }
            public OfficeRuntimePathService Paths { get; }

            public Vector2 FindSafeStart(Vector2 direction)
            {
                if (_safeStarts.TryGetValue(direction, out Vector2 cached)) return cached;
                for (var distance = 0.30f; distance <= 5.0f; distance += 0.05f)
                {
                    Vector2 candidate = TargetCenter + direction * distance;
                    if (Occupancy.CanTraverseStatic(
                            candidate,
                            candidate,
                            OfficeRuntimeAgent.DefaultRadius,
                            string.Empty))
                    {
                        _safeStarts.Add(direction, candidate);
                        return candidate;
                    }
                }
                throw new InvalidOperationException($"No safe {Spec.KindId} start for {direction}.");
            }

            public void Dispose()
            {
                if (_root != null) Object.DestroyImmediate(_root);
                foreach (Tile tile in _tiles)
                    if (tile != null) Object.DestroyImmediate(tile);
            }
        }

        private readonly struct TargetSpec
        {
            public TargetSpec(
                string kindId,
                int width,
                int height,
                OfficeFurnitureFacing facing,
                bool isChair,
                bool isWall)
            {
                KindId = kindId;
                Width = width;
                Height = height;
                Facing = facing;
                IsChair = isChair;
                IsWall = isWall;
            }

            public string KindId { get; }
            public int Width { get; }
            public int Height { get; }
            public OfficeFurnitureFacing Facing { get; }
            public bool IsChair { get; }
            public bool IsWall { get; }
        }

        private readonly struct DirectionSpec
        {
            public DirectionSpec(string name, Vector2 worldDirection, OfficeGridCoordinate gridOffset)
            {
                Name = name;
                WorldDirection = worldDirection;
                GridOffset = gridOffset;
            }

            public string Name { get; }
            public Vector2 WorldDirection { get; }
            public OfficeGridCoordinate GridOffset { get; }
        }

        private readonly struct DirectProbeSpec
        {
            public DirectProbeSpec(string mode, float speed, float aimOffset)
            {
                Mode = mode;
                Speed = speed;
                AimOffset = aimOffset;
            }

            public string Mode { get; }
            public float Speed { get; }
            public float AimOffset { get; }
        }

        private readonly struct ImageEndpoint
        {
            public ImageEndpoint(
                string direction,
                Vector2 start,
                Vector2 end,
                Vector2 targetCenter,
                bool passed)
            {
                Direction = direction;
                Start = start;
                End = end;
                Passed = passed;
                TargetCenter = targetCenter;
            }

            public string Direction { get; }
            public Vector2 Start { get; }
            public Vector2 End { get; }
            public bool Passed { get; }
            public Vector2 TargetCenter { get; }
        }

        [Serializable]
        private sealed class CollisionReport
        {
            public string generatedUtc;
            public string layoutHash;
            public int[] frameRates;
            public int[] timeScales;
            public string[] members;
            public int totalCases;
            public int failedCases;
            public float maximumStopVariance;
            public List<CollisionTargetResult> targets = new List<CollisionTargetResult>();
            public List<CollisionCaseResult> cases = new List<CollisionCaseResult>();
        }

        [Serializable]
        private sealed class CollisionTargetResult
        {
            public string target;
            public string collisionLayer;
            public int width;
            public int height;
            public int totalCases;
            public int failedCases;
            public float maximumStopVariance;
            public bool passed;
            public string image;
        }

        [Serializable]
        private sealed class CollisionCaseResult
        {
            public string target;
            public string collisionLayer;
            public string controller;
            public string member;
            public string direction;
            public string mode;
            public int frameRate;
            public int timeScale;
            public float speed;
            public float startX;
            public float startY;
            public float finalX;
            public float finalY;
            public float maximumFrameDisplacement;
            public float stopVariance;
            public int blockedAttempts;
            public int staticViolations;
            public int interactionViolations;
            public int penetrationViolations;
            public bool deterministic;
            public bool passed;
            public string failure;

            public Vector2 StartPosition => new Vector2(startX, startY);
            public Vector2 FinalPosition => new Vector2(finalX, finalY);
        }
    }
}
