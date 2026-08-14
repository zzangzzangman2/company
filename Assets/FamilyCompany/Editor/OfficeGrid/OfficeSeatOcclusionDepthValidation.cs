using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FamilyCompany.Presentation.Unity.OfficeGridView;
using FamilyCompany.Presentation.Unity.OfficeRuntime;
using FamilyCompany.Presentation.Unity.OfficeSeating;
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
        private const string FurniturePresenterSourcePath =
            "FamilyCompany/Presentation.Unity/OfficeGrid/OfficeGridFurniturePresenter.cs";
        private const int BasePriority = 0;
        private const int FrontPriority = 2;
        private const int ChairFrontPriority = 3;
        private const int SeatDeskBasePlane = 0;
        private const int SeatChairBasePlane = 1;
        private const int SeatActorEngagedPlane = 2;
        private const int SeatDeskFrontEngagedPlane = 3;
        private const int SeatChairFrontEngagedPlane = 4;
        private const int SeatDeskFrontReleasedPlane = 2;
        private const int SeatChairFrontReleasedPlane = 3;
        private const int SeatActorReleasedPlane = 4;

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
            ValidatePlantedSitEntryGate();
            ValidateLeavingSeatBoundaryInEightDirections();
            ValidateReservationAndOcclusionAreIndependent();
            ValidateSyntheticSeatBoundDepth();
            ValidateRuntimeDepthSorterBindings();
            Debug.Log(
                "OFFICE_SEAT_OCCLUSION_DEPTH_VALIDATION: PASS phases=9 exitDirections=8 " +
                "safeAnchorRelease=atomic depthStacks=engaged+sitEntryReleased hybridQ=256 " +
                "sitEntryGate=frame0-released chairForeground=lower-rim-only " +
                $"lowerOccluder=" +
                $"{OfficeSeatedUpperBodyProtectionRules.ExpectedChairLowerOpaquePixelCount} " +
                "upperBodyPlane=pose-split " +
                "semanticFrontFootprints=preserved sourceBindings=present");
        }

        private static void ValidatePlantedSitEntryGate()
        {
            var operatorWorld = new Vector2(7.25f, -3.5f);
            var approachWorld = new Vector2(10.75f, -1.25f);
            Require(
                !OfficeSeatOcclusionRules.Evaluate(
                    OfficeRuntimeAgentPhase.SittingDown,
                    operatorWorld,
                    operatorWorld,
                    approachWorld,
                    OfficeSeatingAnimationClip.SitDown,
                    0).ForegroundEngaged,
                "Planted SitDown[0] must keep the chair foreground released.");
            Require(
                OfficeSeatOcclusionRules.Evaluate(
                    OfficeRuntimeAgentPhase.SittingDown,
                    operatorWorld,
                    operatorWorld,
                    approachWorld,
                    OfficeSeatingAnimationClip.SitDown,
                    1).ForegroundEngaged,
                "SitDown[1] must engage the occupied chair foreground.");
            Require(
                OfficeSeatOcclusionRules.Evaluate(
                    OfficeRuntimeAgentPhase.Working,
                    operatorWorld,
                    operatorWorld,
                    approachWorld,
                    OfficeSeatingAnimationClip.Work,
                    0).ForegroundEngaged,
                "Work[0] must engage the occupied chair foreground.");
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

            Vector2 releasedActor = Vector2.LerpUnclamped(operatorWorld, approachWorld, 0.8f);
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
            for (var index = 0; index < ExitVectors.Length; index++)
            {
                Vector2 exit = ExitVectors[index];
                var operatorWorld = new Vector2(11.375f + index * 0.41f, -8.625f + index * 0.27f);
                Vector2 approachWorld = operatorWorld + exit;
                Vector2 perpendicular = new Vector2(-exit.y, exit.x).normalized *
                                        (0.07f + index * 0.013f);

                AssertLeavingState(
                    operatorWorld + exit * 0.001f + perpendicular,
                    operatorWorld,
                    approachWorld,
                    true,
                    0.001f,
                    index,
                    "at-start");
                AssertLeavingState(
                    operatorWorld + exit * 0.999f + perpendicular,
                    operatorWorld,
                    approachWorld,
                    true,
                    0.999f,
                    index,
                    "at-safe-anchor");

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
            Require(noExit.ForegroundEngaged,
                "A claimed LeavingSeat phase must retain foreground occlusion even for a degenerate exit.");
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
            Require(state.ForegroundEngaged,
                "Foreground occlusion must remain engaged until the LeavingSeat reservation ends.");

            string agent = Compact(ReadAssetSource(AgentSourcePath));
            string occupancy = Section(agent, "publicboolIsOccupyingSeat=>", ";publicboolIsBusy");
            RequireContains(
                occupancy,
                "Phase==OfficeRuntimeAgentPhase.LeavingSeat",
                "LeavingSeat must remain part of seat occupancy/reservation ownership.");

            int leavingCase = agent.IndexOf(
                "caseOfficeRuntimeAgentPhase.LeavingSeat:",
                StringComparison.Ordinal);
            int validateSafeAnchor = agent.IndexOf(
                "TryValidateSeatEgressCompletion(outstringblocker)",
                leavingCase,
                StringComparison.Ordinal);
            int reachedSafeAnchor = agent.IndexOf(
                "_seatEgressReachedSafeAnchor=true;",
                validateSafeAnchor,
                StringComparison.Ordinal);
            int releaseClaim = agent.IndexOf(
                "ReleaseSeatImmediately();",
                reachedSafeAnchor,
                StringComparison.Ordinal);
            Require(
                leavingCase >= 0 && validateSafeAnchor > leavingCase &&
                reachedSafeAnchor > validateSafeAnchor && releaseClaim > reachedSafeAnchor,
                "The seat claim must release only after LeavingSeat validates a safe anchor.");
            RequireNotContains(
                agent,
                "RestoreChairPresentation",
                "Seat occlusion lifecycle must never restore or move chair presentation.");
        }

        private static void ValidateSyntheticSeatBoundDepth()
        {
            const int seatX = 4;
            const int seatY = 7;
            var operatorGrid = new Vector2(seatX + 0.5f, seatY + 0.5f);
            var approachGrid = new Vector2(seatX, seatY - 1f);
            Vector2 earlyDismount = Vector2.LerpUnclamped(operatorGrid, approachGrid, 0.001f);
            Vector2 safeAnchor = Vector2.LerpUnclamped(operatorGrid, approachGrid, 0.999f);
            foreach (OfficeRuntimeAgentPhase phase in ForegroundPhases)
                ValidateSyntheticStack(
                    phase,
                    true,
                    (int)phase,
                    operatorGrid,
                    $"{phase} operator");
            ValidateSyntheticStack(
                OfficeRuntimeAgentPhase.LeavingSeat,
                true,
                0,
                earlyDismount,
                "LeavingSeat canonical p=.001");
            ValidateSyntheticStack(
                OfficeRuntimeAgentPhase.LeavingSeat,
                true,
                1,
                safeAnchor,
                "LeavingSeat canonical p=.999");
            ValidateSyntheticStack(
                OfficeRuntimeAgentPhase.SittingDown,
                false,
                0,
                approachGrid,
                "SitDown planted frame0 released");

            for (var index = 0; index < ExitVectors.Length; index++)
            {
                Vector2 directionalApproach = operatorGrid + ExitVectors[index];
                ValidateSyntheticStack(
                    OfficeRuntimeAgentPhase.LeavingSeat,
                    true,
                    index * 2,
                    Vector2.LerpUnclamped(operatorGrid, directionalApproach, 0.001f),
                    $"LeavingSeat direction {index} p=.001");
                ValidateSyntheticStack(
                    OfficeRuntimeAgentPhase.LeavingSeat,
                    true,
                    index * 2 + 1,
                    Vector2.LerpUnclamped(operatorGrid, directionalApproach, 0.999f),
                    $"LeavingSeat direction {index} p=.999");
            }
        }

        private static void ValidateSyntheticStack(
            OfficeRuntimeAgentPhase phase,
            bool foregroundEngaged,
            int frame,
            Vector2 actorGrid,
            string label)
        {
            const int seatX = 4;
            const int seatY = 7;
            const string seatStackId = "synthetic-seat";
            OfficeDepthItem chairFootprint = OfficeDepthItem.Cell(
                "chair-base",
                seatX,
                seatY,
                BasePriority);
            var deskFootprint = new OfficeDepthItem(
                "desk-base",
                seatX,
                seatY + 1,
                seatX + 1,
                seatY + 1,
                BasePriority);
            OfficeHybridDepthItem chairBase = OfficeHybridDepthItem.Furniture(
                chairFootprint,
                OfficeHybridDepthRole.ChairBase,
                "swivel-chair:base",
                "chair",
                seatStackId,
                SeatChairBasePlane);
            OfficeHybridDepthItem deskBase = OfficeHybridDepthItem.Furniture(
                deskFootprint,
                OfficeHybridDepthRole.FurnitureBase,
                "desk:base",
                "desk",
                seatStackId,
                SeatDeskBasePlane);
            OfficeHybridDepthItem actor = OfficeHybridDepthItem.ActorAtGridPosition(
                "actor",
                actorGrid.x,
                actorGrid.y,
                "office-runtime-actor",
                "family-member",
                seatStackId,
                foregroundEngaged ? SeatActorEngagedPlane : SeatActorReleasedPlane);
            OfficeHybridDepthItem chairFront = OfficeHybridDepthItem.Furniture(
                OfficeDepthItem.Cell(
                    "chair-front",
                    seatX,
                    seatY,
                    foregroundEngaged ? ChairFrontPriority : FrontPriority),
                foregroundEngaged
                    ? OfficeHybridDepthRole.ChairFront
                    : OfficeHybridDepthRole.FurnitureFront,
                "swivel-chair:front",
                "chair",
                seatStackId,
                foregroundEngaged
                    ? SeatChairFrontEngagedPlane
                    : SeatChairFrontReleasedPlane);
            OfficeHybridDepthItem deskFront = OfficeHybridDepthItem.Furniture(
                new OfficeDepthItem(
                    "desk-front",
                    deskFootprint.MinX,
                    deskFootprint.MinY,
                    deskFootprint.MaxX,
                    deskFootprint.MaxY,
                    FrontPriority),
                foregroundEngaged
                    ? OfficeHybridDepthRole.DeskFront
                    : OfficeHybridDepthRole.FurnitureFront,
                "desk:front",
                "desk",
                seatStackId,
                foregroundEngaged
                    ? SeatDeskFrontEngagedPlane
                    : SeatDeskFrontReleasedPlane);

            Require(
                SameFootprint(chairFront.FurnitureFootprint, chairBase.FurnitureFootprint),
                label + " rebound the chair foreground away from its semantic footprint.");
            Require(
                SameFootprint(deskFront.FurnitureFootprint, deskBase.FurnitureFootprint),
                label + " rebound the desk foreground away from its semantic footprint.");

            IReadOnlyDictionary<string, int> orders =
                OfficeHybridContinuousDepth.ResolveSortingOrders(
                    new[] { chairBase, deskBase, actor, chairFront, deskFront });
            RequireLess(orders, chairBase.Id, actor.Id, $"{phase} chair base/actor");
            RequireLess(orders, deskBase.Id, actor.Id, $"{phase} desk base/actor");
            RequireLess(orders, deskBase.Id, chairBase.Id, $"{phase} desk/chair base");
            if (foregroundEngaged)
            {
                RequireLess(orders, actor.Id, chairFront.Id, $"{phase} actor/chair front");
                RequireLess(orders, actor.Id, deskFront.Id, $"{phase} actor/desk front");
                RequireLess(orders, deskFront.Id, chairFront.Id, $"{phase} desk/chair front");
            }
            else
            {
                RequireLess(orders, chairBase.Id, deskFront.Id, $"{phase} chair base/desk front");
                RequireLess(orders, deskFront.Id, chairFront.Id, $"{phase} desk/chair released front");
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
            Require(snapshot.IsValidStack, label + " synthetic depth snapshot rejected a valid stack.");

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
            string presenter = Compact(ReadAssetSource(FurniturePresenterSourcePath));
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
                "foregroundEngaged=occupant.IsSeatForegroundOcclusionEngaged;",
                "Foreground depth is not controlled separately from seat occupancy.");
            RequireContains(
                sorter,
                "ApplyOccupiedChairForeground(furniture.FurnitureId,isChair&&foregroundEngaged);",
                "Engaged chairs do not apply their occupied foreground stack.");
            RequireContains(
                presenter,
                "frontRenderer.sprite=definition.FrontOverlaySprite;",
                "Chairs do not retain the authored foreground Sprite for released/empty states.");
            RequireContains(
                sorter,
                "UpperActorPrefix+actor.AgentId",
                "Depth sorter does not emit a seated upper-body protection layer.");
            RequireContains(
                sorter,
                "SeatActorUpperBodyEngagedPlane",
                "Seated upper-body protection has no explicit semantic plane.");
            RequireNotContains(
                presenter,
                "visual.FrontRenderer.sprite=occupiedForeground.Sprite;",
                "The canonical chair foreground is still replaced by a cropped Sprite.");
            RequireContains(
                presenter,
                "OccupiedLowerBodyRenderer",
                "Furniture presenter has no dedicated lower-body seat-rim occluder.");
            RequireContains(
                presenter,
                "visual.FrontRenderer.enabled=false;",
                "Occupied chairs do not suppress the rectangular authored foreground seam.");
            RequireNotContains(
                sorter,
                "minX=maxX=seat.Cell.X;",
                "Foreground depth must retain its semantic furniture x footprint.");
            RequireNotContains(
                sorter,
                "minY=maxY=seat.Cell.Y;",
                "Foreground depth must retain its semantic furniture y footprint.");
            RequireContains(
                sorter,
                "ResolveActorGridContact(actor.Position,",
                "Actor feet are not inverse-projected from their continuous runtime position.");
            RequireContains(
                sorter,
                "pointXQ=OfficeHybridContinuousDepth.Quantize(gridX);",
                "Actor x contact is not quantized by the pure hybrid-depth rule.");
            RequireContains(
                sorter,
                "pointYQ=OfficeHybridContinuousDepth.Quantize(gridY);",
                "Actor y contact is not quantized by the pure hybrid-depth rule.");
            RequireNotContains(
                sorter,
                "_presenter.NearestCell(actor.transform.position)",
                "Actor runtime depth still snaps to a nearest cell.");
            RequireContains(
                sorter,
                "?actor.IsSeatForegroundOcclusionEngaged?SeatActorEngagedPlane:SeatActorReleasedPlane",
                "Live seat actors do not switch between engaged and released seat planes.");
            RequireContains(
                sorter,
                "?SeatChairFrontEngagedPlane:SeatDeskFrontEngagedPlane",
                "Engaged chair/desk foreground planes are not explicit.");
            RequireContains(
                sorter,
                "?SeatChairFrontReleasedPlane:SeatDeskFrontReleasedPlane",
                "Released chair/desk foreground planes are not explicit.");
            RequireContains(
                sorter,
                "OfficeHybridDepthRole.FurnitureFront",
                "Normal/released foregrounds do not use the generic foreground role.");
            RequireContains(
                sorter,
                "RecordSeatingDepthSamples(actors,orders);",
                "Depth samples are not recorded after each Apply sort.");
            RequireContains(
                sorter,
                "actor.RecordSeatingDepthSample(newOfficeSeatingDepthSnapshot(",
                "Per-actor seating depth evidence is not recorded.");
            int resolveOrders = sorter.IndexOf(
                "OfficeHybridContinuousDepth.ResolveSortingOrders(_items,_sortWorkspace);",
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

        private static bool SameFootprint(OfficeDepthItem first, OfficeDepthItem second)
        {
            return first.MinX == second.MinX && first.MinY == second.MinY &&
                   first.MaxX == second.MaxX && first.MaxY == second.MaxY;
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

        private static void RequireNotContains(string source, string token, string message)
        {
            Require(!source.Contains(token), message);
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
