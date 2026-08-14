using System;
using System.IO;
using FamilyCompany.Presentation.Unity.OfficeRuntime;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor.OfficeGridQa
{
    /// <summary>
    /// Deterministic validation of the seat-local egress lifecycle. Pathfinding and clearance stay
    /// owned by OfficeRuntimeOccupancy; this validation covers only candidate order and consumption
    /// of those common queries before the stand transition can begin.
    /// </summary>
    public static class OfficeSeatEgressValidation
    {
        private const string AgentSourcePath =
            "FamilyCompany/Presentation.Unity/OfficeRuntime/OfficeRuntimeAgent.cs";
        private const string OccupancySourcePath =
            "FamilyCompany/Presentation.Unity/OfficeRuntime/OfficeRuntimeOccupancy.cs";
        private static readonly string[] FamilyMembers =
        {
            "player", "older_sister", "father", "mother"
        };

        [MenuItem("Family Company/Validate Office Seat Egress")]
        public static void Validate()
        {
            int matrixCases = ValidateCandidateMatrix();
            ValidateRuntimeLifecycleSource();
            Debug.Log(
                "OFFICE_SEAT_EGRESS_VALIDATION: PASS families=4 rotations=4 scenarios=4 " +
                $"matrixCases={matrixCases} preference=front>left>right rearCandidates=0 " +
                "reserveBeforeStand=true releaseAfterSafeAnchor=true maxStepPx=0.9");
        }

        public static void RunBatch()
        {
            try
            {
                Validate();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static int ValidateCandidateMatrix()
        {
            var seatCell = new OfficeGridCoordinate(10, 10);
            var rotations = new[]
            {
                new RotationCase(OfficeFurnitureFacing.NorthWest, 0, -1),
                new RotationCase(OfficeFurnitureFacing.NorthEast, -1, 0),
                new RotationCase(OfficeFurnitureFacing.SouthEast, 0, 1),
                new RotationCase(OfficeFurnitureFacing.SouthWest, 1, 0)
            };
            var scenarios = new[]
            {
                new ScenarioCase(false, false, false, true, OfficeSeatEgressKind.Front),
                new ScenarioCase(true, false, false, true, OfficeSeatEgressKind.Left),
                new ScenarioCase(true, true, false, true, OfficeSeatEgressKind.Right),
                new ScenarioCase(true, true, true, false, OfficeSeatEgressKind.None)
            };

            var caseCount = 0;
            foreach (string member in FamilyMembers)
            foreach (RotationCase rotation in rotations)
            {
                var approach = new OfficeGridCoordinate(
                    seatCell.X + rotation.FrontX,
                    seatCell.Y + rotation.FrontY);
                var seat = new OfficeSeatSlot(
                    "qa-seat-" + member + "-" + rotation.Facing,
                    "qa-chair",
                    "qa-desk",
                    seatCell,
                    approach,
                    rotation.Facing);
                var candidates = OfficeSeatEgressRules.ResolveCandidates(seat);
                Require(candidates.Count == OfficeSeatEgressRules.CandidateCount,
                    "Every seat rotation must expose exactly three non-rear candidates.");
                Require(candidates[0].Kind == OfficeSeatEgressKind.Front,
                    "Front must be the first egress candidate.");
                Require(candidates[1].Kind == OfficeSeatEgressKind.Left,
                    "Left must be the second egress candidate.");
                Require(candidates[2].Kind == OfficeSeatEgressKind.Right,
                    "Right must be the third egress candidate.");
                var rear = new OfficeGridCoordinate(
                    seatCell.X - rotation.FrontX,
                    seatCell.Y - rotation.FrontY);
                for (var candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
                {
                    Require(!candidates[candidateIndex].TargetCell.Equals(rear),
                        "A rear/desk-through candidate was generated.");
                }

                foreach (ScenarioCase scenario in scenarios)
                {
                    bool selected = OfficeSeatEgressRules.TrySelectCandidate(
                        seat,
                        candidate => candidate.Kind switch
                        {
                            OfficeSeatEgressKind.Front => !scenario.BlockFront,
                            OfficeSeatEgressKind.Left => !scenario.BlockLeft,
                            OfficeSeatEgressKind.Right => !scenario.BlockRight,
                            _ => false
                        },
                        out OfficeSeatEgressCandidate candidate);
                    Require(selected == scenario.ShouldSelect,
                        $"Unexpected selection result for {member}/{rotation.Facing}.");
                    Require(
                        (selected ? candidate.Kind : OfficeSeatEgressKind.None) == scenario.Expected,
                        $"Unexpected egress preference for {member}/{rotation.Facing}.");
                    caseCount++;
                }
            }
            return caseCount;
        }

        private static void ValidateRuntimeLifecycleSource()
        {
            string agent = Compact(ReadAssetSource(AgentSourcePath));
            string occupancy = Compact(ReadAssetSource(OccupancySourcePath));
            int finishing = agent.IndexOf(
                "caseOfficeRuntimeAgentPhase.FinishingWork:",
                StringComparison.Ordinal);
            int reserve = agent.IndexOf("TryPrepareSeatEgressReservation()", finishing,
                StringComparison.Ordinal);
            int beginStand = agent.IndexOf("_animator.BeginStandUp()", finishing,
                StringComparison.Ordinal);
            Require(finishing >= 0 && reserve > finishing && beginStand > reserve,
                "The safe anchor and its segment must be reserved before StandUp begins.");
            RequireContains(
                agent,
                "if(!TryPrepareSeatEgressReservation()){_seatEgressWaiting=true;return;}",
                "An all-blocked egress must remain in seated FinishingWork and retry.");

            RequireContains(agent, "_world.Workstations.ResolveEgressCandidates(_seat)",
                "Runtime does not consume the rotation-aware workstation geometry candidates.");
            RequireContains(agent, "_world.Occupancy.IsCellPassable(",
                "Runtime does not check destination walkability/dynamic occupancy.");
            RequireContains(agent, "_world.Occupancy.CanTraverseStatic(",
                "Runtime does not check continuous static clearance.");
            RequireContains(agent, "_world.Occupancy.HasPresentationClearance(",
                "Runtime does not check common actor body clearance.");
            RequireContains(agent, "_world.Occupancy.TryReservePath(",
                "Runtime does not atomically reserve the chosen egress cell.");
            RequireContains(agent, "_world.Occupancy.CanMove(",
                "Runtime does not consume the common dynamic segment query.");
            RequireContains(agent, "_world.Occupancy.IsActorPresent(_agentId)",
                "Runtime does not require the actor to remain present in occupancy.");
            RequireContains(agent, "ReferenceEquals(registered,this)",
                "Runtime does not require the actor to remain registered in the world.");
            RequireContains(agent, "MaximumSeatEgressStepPx=0.899f",
                "The egress presentation step cap is not strictly below 0.9 pixels.");
            RequireContains(agent, "_seatEgressReachedSafeAnchor=true;",
                "Runtime never records validated safe-anchor arrival.");
            RequireContains(agent,
                "Phase!=OfficeRuntimeAgentPhase.LeavingSeat||!_seatEgressReachedSafeAnchor",
                "Chair alignment can return before safe-anchor arrival.");
            RequireContains(agent, "_hasCompletedSeatEgress=true;",
                "Runtime does not record egress completion before release.");
            RequireContains(occupancy,
                "publicboolHasReservation(stringagentId,OfficeGridCoordinatecell)",
                "Runtime occupancy cannot verify that a pre-stand reservation remains held.");

            int leaving = agent.IndexOf("caseOfficeRuntimeAgentPhase.LeavingSeat:",
                StringComparison.Ordinal);
            int dismountGuard = agent.IndexOf(
                "if(!_seatEgressReachedSafeAnchor){TickSeatEgressDismount(deltaTime);return;}",
                leaving,
                StringComparison.Ordinal);
            int release = agent.IndexOf("ReleaseSeatImmediately();", dismountGuard,
                StringComparison.Ordinal);
            int idle = agent.IndexOf("Phase=OfficeRuntimeAgentPhase.Idle;", release,
                StringComparison.Ordinal);
            int tickDismountMethod = agent.IndexOf(
                "privatevoidTickSeatEgressDismount(floatdeltaTime)",
                leaving,
                StringComparison.Ordinal);
            int validateCall = agent.IndexOf(
                "if(!TryValidateSeatEgressCompletion(outstringblocker))",
                tickDismountMethod,
                StringComparison.Ordinal);
            int reached = agent.IndexOf("_seatEgressReachedSafeAnchor=true;", validateCall,
                StringComparison.Ordinal);
            Require(leaving >= 0 && dismountGuard > leaving && release > dismountGuard &&
                    idle > release && tickDismountMethod > leaving &&
                    validateCall > tickDismountMethod &&
                    reached > validateCall,
                "LeavingSeat may expose Idle or release the claim before validated dismount.");
        }

        private static string ReadAssetSource(string path)
        {
            string absolute = Path.Combine(Application.dataPath, path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(absolute)) throw new FileNotFoundException("Missing source file.", absolute);
            return File.ReadAllText(absolute);
        }

        private static string Compact(string source)
        {
            return source.Replace(" ", string.Empty)
                .Replace("\t", string.Empty)
                .Replace("\r", string.Empty)
                .Replace("\n", string.Empty);
        }

        private static void RequireContains(string source, string token, string message)
        {
            Require(source.IndexOf(token, StringComparison.Ordinal) >= 0, message);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private readonly struct RotationCase
        {
            public RotationCase(OfficeFurnitureFacing facing, int frontX, int frontY)
            {
                Facing = facing;
                FrontX = frontX;
                FrontY = frontY;
            }

            public OfficeFurnitureFacing Facing { get; }
            public int FrontX { get; }
            public int FrontY { get; }
        }

        private readonly struct ScenarioCase
        {
            public ScenarioCase(
                bool blockFront,
                bool blockLeft,
                bool blockRight,
                bool shouldSelect,
                OfficeSeatEgressKind expected)
            {
                BlockFront = blockFront;
                BlockLeft = blockLeft;
                BlockRight = blockRight;
                ShouldSelect = shouldSelect;
                Expected = expected;
            }

            public bool BlockFront { get; }
            public bool BlockLeft { get; }
            public bool BlockRight { get; }
            public bool ShouldSelect { get; }
            public OfficeSeatEgressKind Expected { get; }
        }
    }
}
