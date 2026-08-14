using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FamilyCompany.Presentation.Unity.OfficeRuntime;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor.OfficeGridQa
{
    /// <summary>
    /// Pure validation for the seat-bound redraw planes used by the runtime depth sorter. This
    /// deliberately does not build a scene: transition phase/progress and the resulting footprint
    /// order remain deterministic inputs, so every boundary can be exercised in one batch frame.
    /// </summary>
    public static class OfficeSeatOcclusionDepthValidation
    {
        private const string AgentSourcePath =
            "FamilyCompany/Presentation.Unity/OfficeRuntime/OfficeRuntimeAgent.cs";
        private const string DepthSorterSourcePath =
            "FamilyCompany/Presentation.Unity/OfficeRuntime/OfficeRuntimeDepthSorter.cs";
        private const int BasePriority = 0;
        private const int OccupantPriority = 1;
        private const int FrontPriority = 2;
        private const int ChairFrontPriority = 3;

        private static readonly OfficeRuntimeAgentPhase[] ForegroundPhases =
        {
            OfficeRuntimeAgentPhase.SittingDown,
            OfficeRuntimeAgentPhase.Working,
            OfficeRuntimeAgentPhase.FinishingWork,
            OfficeRuntimeAgentPhase.StandingUp
        };

        private static readonly Vector2[] ExitVectors =
        {
            new Vector2(3.75f, 0f),
            new Vector2(2.5f, 4f),
            new Vector2(0f, 5.25f),
            new Vector2(-3f, 2f),
            new Vector2(-6f, 0f),
            new Vector2(-1.75f, -3.5f),
            new Vector2(0f, -4.75f),
            new Vector2(2.25f, -1.25f)
        };

        [MenuItem("Family Company/Validate Office Seat Occlusion Depth")]
        public static void Validate()
        {
            ValidateEveryRuntimePhase();
            ValidateLeavingSeatBoundaryInEightDirections();
            ValidateReservationAndOcclusionAreIndependent();
            ValidateSyntheticSeatBoundDepth();
            ValidateRuntimeDepthSorterBindings();
            Debug.Log(
                "OFFICE_SEAT_OCCLUSION_DEPTH_VALIDATION: PASS phases=9 exitDirections=8 " +
                "releaseProgress=0.35 depthStacks=engaged+disengaged sourceBindings=present");
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

        private static void ValidateEveryRuntimePhase()
        {
            var operatorWorld = new Vector2(7.25f, -3.5f);
            var approachWorld = new Vector2(10.75f, -1.25f);
            Array phases = Enum.GetValues(typeof(OfficeRuntimeAgentPhase));
            foreach (OfficeRuntimeAgentPhase phase in phases)
            {
                OfficeSeatOcclusionState state = OfficeSeatOcclusionRules.Evaluate(
                    phase,
                    operatorWorld,
                    operatorWorld,
                    approachWorld);
                bool expected = IsForegroundPhase(phase) ||
                                phase == OfficeRuntimeAgentPhase.LeavingSeat;
                Require(
                    state.ForegroundEngaged == expected,
                    $"Unexpected foreground state at operator socket for phase {phase}.");
                RequireApproximately(state.ExitProgress01, 0f, $"{phase} operator progress");
            }

            Vector2 releasedActor = Vector2.LerpUnclamped(
                operatorWorld,
                approachWorld,
                OfficeSeatOcclusionRules.LeavingSeatReleaseProgress01 + 0.1f);
            foreach (OfficeRuntimeAgentPhase phase in ForegroundPhases)
            {
                Require(
                    OfficeSeatOcclusionRules.Evaluate(
                        phase,
                        releasedActor,
                        operatorWorld,
                        approachWorld).ForegroundEngaged,
                    $"{phase} must not use LeavingSeat progress to release the foreground.");
            }
        }

        private static void ValidateLeavingSeatBoundaryInEightDirections()
        {
            float release = OfficeSeatOcclusionRules.LeavingSeatReleaseProgress01;
            RequireApproximately(release, 0.35f, "LeavingSeat release threshold");
            for (var index = 0; index < ExitVectors.Length; index++)
            {
                Vector2 exit = ExitVectors[index];
                var operatorWorld = new Vector2(11.375f + index * 0.41f, -8.625f + index * 0.27f);
                Vector2 approachWorld = operatorWorld + exit;
                Vector2 perpendicular = new Vector2(-exit.y, exit.x).normalized *
                                        (0.07f + index * 0.013f);

                AssertLeavingState(
                    operatorWorld + exit * (release - 0.001f) + perpendicular,
                    operatorWorld,
                    approachWorld,
                    true,
                    release - 0.001f,
                    index,
                    "before");
                AssertLeavingState(
                    operatorWorld + exit * (release + 0.001f) + perpendicular,
                    operatorWorld,
                    approachWorld,
                    false,
                    release + 0.001f,
                    index,
                    "after");

                float beforeStart = OfficeSeatOcclusionRules.ResolveExitProgress01(
                    operatorWorld - exit * 0.5f,
                    operatorWorld,
                    approachWorld);
                float pastEnd = OfficeSeatOcclusionRules.ResolveExitProgress01(
                    operatorWorld + exit * 1.5f,
                    operatorWorld,
                    approachWorld);
                RequireApproximately(beforeStart, 0f, $"direction {index} negative clamp");
                RequireApproximately(pastEnd, 1f, $"direction {index} positive clamp");
            }

            var degenerate = new Vector2(-4.25f, 12.5f);
            OfficeSeatOcclusionState noExit = OfficeSeatOcclusionRules.Evaluate(
                OfficeRuntimeAgentPhase.LeavingSeat,
                degenerate,
                degenerate,
                degenerate);
            Require(!noExit.ForegroundEngaged, "A zero-length exit must not retain foreground occlusion.");
            RequireApproximately(noExit.ExitProgress01, 1f, "zero-length exit progress");
        }

        private static void AssertLeavingState(
            Vector2 actorWorld,
            Vector2 operatorWorld,
            Vector2 approachWorld,
            bool expectedEngaged,
            float expectedProgress,
            int directionIndex,
            string boundary)
        {
            OfficeSeatOcclusionState state = OfficeSeatOcclusionRules.Evaluate(
                OfficeRuntimeAgentPhase.LeavingSeat,
                actorWorld,
                operatorWorld,
                approachWorld);
            Require(
                state.ForegroundEngaged == expectedEngaged,
                $"LeavingSeat direction {directionIndex} was incorrect {boundary} the release plane.");
            RequireApproximately(
                state.ExitProgress01,
                expectedProgress,
                $"LeavingSeat direction {directionIndex} {boundary} progress");
        }

        private static void ValidateReservationAndOcclusionAreIndependent()
        {
            var operatorWorld = new Vector2(-2.5f, 6.75f);
            var approachWorld = new Vector2(1.5f, 8.75f);
            Vector2 actorWorld = Vector2.LerpUnclamped(operatorWorld, approachWorld, 0.8f);
            OfficeSeatOcclusionState state = OfficeSeatOcclusionRules.Evaluate(
                OfficeRuntimeAgentPhase.LeavingSeat,
                actorWorld,
                operatorWorld,
                approachWorld);
            Require(!state.ForegroundEngaged,
                "Foreground occlusion must be released before the LeavingSeat reservation ends.");

            string agent = Compact(ReadAssetSource(AgentSourcePath));
            string occupancy = Section(agent, "publicboolIsOccupyingSeat=>", ";publicboolIsBusy");
            RequireContains(
                occupancy,
                "Phase==OfficeRuntimeAgentPhase.LeavingSeat",
                "LeavingSeat must remain part of seat occupancy/reservation ownership.");

            int leavingCase = agent.IndexOf(
                "caseOfficeRuntimeAgentPhase.LeavingSeat:",
                StringComparison.Ordinal);
            int snapToApproach = agent.IndexOf(
                "transform.position=newVector3(target.x,target.y,transform.position.z);",
                leavingCase,
                StringComparison.Ordinal);
            int releaseClaim = agent.IndexOf(
                "ReleaseSeatImmediately();",
                snapToApproach,
                StringComparison.Ordinal);
            Require(
                leavingCase >= 0 && snapToApproach > leavingCase && releaseClaim > snapToApproach,
                "The seat claim must release only after LeavingSeat reaches the approach point.");
        }

        private static void ValidateSyntheticSeatBoundDepth()
        {
            foreach (OfficeRuntimeAgentPhase phase in ForegroundPhases)
                ValidateSyntheticStack(phase, true, (int)phase);
            ValidateSyntheticStack(OfficeRuntimeAgentPhase.LeavingSeat, true, 0);
            ValidateSyntheticStack(OfficeRuntimeAgentPhase.LeavingSeat, false, 1);
        }

        private static void ValidateSyntheticStack(
            OfficeRuntimeAgentPhase phase,
            bool foregroundEngaged,
            int frame)
        {
            const int seatX = 4;
            const int seatY = 7;
            var chairBase = OfficeDepthItem.Cell("chair-base", seatX, seatY, BasePriority);
            var deskBase = new OfficeDepthItem(
                "desk-base",
                seatX,
                seatY + 1,
                seatX + 1,
                seatY + 1,
                BasePriority);
            var actor = OfficeDepthItem.Cell("actor", seatX, seatY, OccupantPriority);
            OfficeDepthItem chairFront = OfficeDepthItem.Cell(
                "chair-front",
                seatX,
                seatY,
                foregroundEngaged ? ChairFrontPriority : BasePriority);
            OfficeDepthItem deskFront = foregroundEngaged
                ? OfficeDepthItem.Cell("desk-front", seatX, seatY, FrontPriority)
                : new OfficeDepthItem(
                    "desk-front",
                    deskBase.MinX,
                    deskBase.MinY,
                    deskBase.MaxX,
                    deskBase.MaxY,
                    BasePriority);

            if (foregroundEngaged)
            {
                Require(IsCell(chairFront, seatX, seatY), "Engaged chair front is not seat-bound.");
                Require(IsCell(deskFront, seatX, seatY), "Engaged desk front is not seat-bound.");
            }

            IReadOnlyDictionary<string, int> orders = OfficeIsometricDepth.ResolveSortingOrders(
                new[] { chairBase, deskBase, actor, chairFront, deskFront });
            RequireLess(orders, chairBase.Id, actor.Id, $"{phase} chair base/actor");
            RequireLess(orders, deskBase.Id, actor.Id, $"{phase} desk base/actor");
            if (foregroundEngaged)
            {
                RequireLess(orders, actor.Id, chairFront.Id, $"{phase} actor/chair front");
                RequireLess(orders, actor.Id, deskFront.Id, $"{phase} actor/desk front");
                RequireLess(orders, deskFront.Id, chairFront.Id, $"{phase} desk/chair front");
            }
            else
            {
                RequireLess(orders, chairFront.Id, actor.Id, $"{phase} released chair front/actor");
                RequireLess(orders, deskFront.Id, actor.Id, $"{phase} released desk front/actor");
            }

            var snapshot = new OfficeSeatingDepthSnapshot(
                phase,
                null,
                frame,
                foregroundEngaged,
                orders[actor.Id],
                orders[chairBase.Id],
                true,
                orders[chairFront.Id],
                true,
                orders[deskBase.Id],
                true,
                orders[deskFront.Id]);
            Require(snapshot.IsValidStack, $"{phase} synthetic depth snapshot rejected a valid stack.");

            var missingEngagedFront = new OfficeSeatingDepthSnapshot(
                phase,
                null,
                frame,
                true,
                101,
                100,
                false,
                0,
                true,
                99,
                true,
                102);
            Require(!missingEngagedFront.IsValidStack,
                "An engaged stack without a chair foreground was accepted.");
        }

        private static void ValidateRuntimeDepthSorterBindings()
        {
            string sorter = Compact(ReadAssetSource(DepthSorterSourcePath));
            RequireContains(
                sorter,
                "_seatsByFurnitureId[seat.ChairFurnitureId]=seat;",
                "Depth sorter does not bind a chair front to its seat.");
            RequireContains(
                sorter,
                "_seatsByFurnitureId[seat.WorkSurfaceFurnitureId]=seat;",
                "Depth sorter does not bind a workstation front to its seat.");
            RequireContains(
                sorter,
                "_activeSeatOccupants[actor.ActiveSeatId]=actor;",
                "Depth sorter does not resolve the active occupant by seat id.");
            RequireContains(
                sorter,
                "if(occupant.IsSeatForegroundOcclusionEngaged)",
                "Foreground depth is not controlled separately from seat occupancy.");
            RequireContains(
                sorter,
                "minX=maxX=seat.Cell.X;",
                "Active foreground x depth is not bound to the seat cell.");
            RequireContains(
                sorter,
                "minY=maxY=seat.Cell.Y;",
                "Active foreground y depth is not bound to the seat cell.");
            RequireContains(
                sorter,
                "RecordSeatingDepthSamples(actors,orders);",
                "Depth samples are not recorded after each Apply sort.");
            RequireContains(
                sorter,
                "actor.RecordSeatingDepthSample(newOfficeSeatingDepthSnapshot(",
                "Per-actor seating depth evidence is not recorded.");
            RequireContains(
                sorter,
                "if(actor.IsSeatForegroundOcclusionEngaged&&actor.ActiveSeatId.Length>0)",
                "Released occupants remain incorrectly pinned to the seat depth cell.");

            int resolveOrders = sorter.IndexOf(
                "OfficeIsometricDepth.ResolveSortingOrders(_items);",
                StringComparison.Ordinal);
            int recordSamples = sorter.IndexOf(
                "RecordSeatingDepthSamples(actors,orders);",
                StringComparison.Ordinal);
            Require(
                resolveOrders >= 0 && recordSamples > resolveOrders,
                "Seating depth samples must record the completed frame sort.");
        }

        private static bool IsForegroundPhase(OfficeRuntimeAgentPhase phase)
        {
            for (var index = 0; index < ForegroundPhases.Length; index++)
                if (ForegroundPhases[index] == phase) return true;
            return false;
        }

        private static bool IsCell(OfficeDepthItem item, int x, int y)
        {
            return item.MinX == x && item.MaxX == x && item.MinY == y && item.MaxY == y;
        }

        private static void RequireLess(
            IReadOnlyDictionary<string, int> orders,
            string behind,
            string ahead,
            string label)
        {
            Require(
                orders[behind] < orders[ahead],
                $"Depth relationship failed for {label}: {behind}={orders[behind]}, " +
                $"{ahead}={orders[ahead]}.");
        }

        private static string ReadAssetSource(string relativeToAssets)
        {
            string path = Path.Combine(
                Application.dataPath,
                relativeToAssets.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) throw new FileNotFoundException("Source file is missing.", path);
            return File.ReadAllText(path);
        }

        private static string Compact(string source)
        {
            var result = new StringBuilder(source.Length);
            for (var index = 0; index < source.Length; index++)
            {
                char value = source[index];
                if (!char.IsWhiteSpace(value)) result.Append(value);
            }
            return result.ToString();
        }

        private static string Section(string source, string startMarker, string endMarker)
        {
            int start = source.IndexOf(startMarker, StringComparison.Ordinal);
            int end = start < 0
                ? -1
                : source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
            if (start < 0 || end <= start)
                throw new InvalidOperationException(
                    $"Unable to resolve source section '{startMarker}' -> '{endMarker}'.");
            return source.Substring(start, end - start);
        }

        private static void RequireContains(string source, string token, string message)
        {
            Require(source.Contains(token), message);
        }

        private static void RequireApproximately(float actual, float expected, string label)
        {
            Require(
                Mathf.Abs(actual - expected) <= 0.0001f,
                $"{label} was {actual:0.######}, expected {expected:0.######}.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
