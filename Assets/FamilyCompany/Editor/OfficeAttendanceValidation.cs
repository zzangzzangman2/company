using System;
using System.Linq;
using FamilyCompany.Simulation.Core;
using FamilyCompany.Simulation.Family;
using FamilyCompany.Presentation.Unity;
using FamilyCompany.Presentation.Unity.OfficeRuntime;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public static class OfficeAttendanceValidation
    {
        [MenuItem("Family Company/Validate Office Attendance")]
        public static void Run()
        {
            try
            {
                Require(GameTime.CampaignStart ==
                        new DateTime(2000, 1, 3, 8, 50, 0, DateTimeKind.Unspecified),
                    "New campaign must begin Monday at 08:50.");
                DateTime day = GameTime.CampaignStart.Date;
                Require(OfficeAttendanceRules.Resolve(day.AddHours(8).AddMinutes(59)) ==
                        OfficeAttendancePhase.BeforeWork, "08:59 must be before work.");
                Require(!OfficeAttendanceRules.HasArrived(day.AddHours(9), 1),
                    "Second actor must retain the stagger at 09:00.");
                Require(OfficeAttendanceRules.HasArrived(day.AddHours(9).AddMinutes(3), 3),
                    "Fourth actor must arrive by 09:03.");
                Require(!OfficeAutonomyCoordinator.IsAttendanceDoorSfxEligibleAt(
                        day.AddHours(8).AddMinutes(59)),
                    "Attendance door audio must remain gated before the shift.");
                Require(OfficeAutonomyCoordinator.IsAttendanceDoorSfxEligibleAt(day.AddHours(9)),
                    "The canonical 09:00 attendance start must allow the door-open cue.");
                Require(OfficeAutonomyCoordinator.IsAttendanceDoorSfxEligibleAt(
                        day.AddHours(9).AddMinutes(50)),
                    "A normal 08:50 to 09:50 +1 hour clock jump must still allow the first-entry cue.");
                Require(!OfficeAutonomyCoordinator.IsAttendanceDoorSfxEligibleAt(day.AddHours(18)),
                    "Attendance door audio must remain gated after the shift.");
                Require(!OfficeAutonomyCoordinator.IsAttendanceDoorSfxEligibleAt(
                        day.AddDays(5).AddHours(10)),
                    "Attendance door audio must remain gated on weekends.");
                Require(OfficeAutonomyCoordinator.ShouldArmAttendanceDoorSfxOnStateBind(
                        day.AddHours(8).AddMinutes(59)),
                    "A before-work state must arm its upcoming attendance cue.");
                Require(!OfficeAutonomyCoordinator.ShouldArmAttendanceDoorSfxOnStateBind(
                        day.AddHours(9).AddMinutes(50)),
                    "Loading a mid-shift state must not arm an attendance cue.");
                Require(!OfficeAutonomyCoordinator.ShouldArmAttendanceDoorSfxOnStateBind(
                        day.AddDays(5).AddHours(8).AddMinutes(50)),
                    "A weekend state must not arm an attendance cue.");
                DateTime sundayAtTen = day.AddDays(6).AddHours(10);
                DateTime mondayAtTen = sundayAtTen.AddDays(1);
                Require(OfficeAutonomyCoordinator.ShouldArmAttendanceDoorSfxAfterObservedTransition(
                        sundayAtTen,
                        mondayAtTen),
                    "A same-state Sunday-to-Monday +1 day jump must arm Monday's first entry cue.");
                Require(!OfficeAutonomyCoordinator.ShouldArmAttendanceDoorSfxAfterObservedTransition(
                        day.AddHours(10),
                        day.AddDays(1).AddHours(10)),
                    "A working-to-working date jump must not invent a shift-start cue.");
                Require(OfficeRuntimeWorkstationService.StarterEntranceCell.Equals(
                        new OfficeGridCoordinate(8, 1)),
                    "Starter attendance must use the one canonical open passage.");
                FamilyCompany.Simulation.OfficeLayout.OfficeGrid office =
                    OfficeGridLayouts.CreateStarterOfficeV1();
                Require(office.IsWalkable(new OfficeGridCoordinate(8, 1)),
                    "The interior entrance cell must stay walkable.");
                Require(office.FloorAt(new OfficeGridCoordinate(8, 0)) != OfficeFloorTileKind.Void &&
                        office.FloorAt(new OfficeGridCoordinate(0, 6)) != OfficeFloorTileKind.Void &&
                        office.FloorAt(new OfficeGridCoordinate(12, 6)) != OfficeFloorTileKind.Void &&
                        !office.IsWalkable(new OfficeGridCoordinate(8, 0)),
                    "The complete perimeter floor must render under the walls but remain non-walkable.");
                int fullWallCount = office.Furniture.Count(item =>
                    item.KindId == OfficeGridLayouts.EntranceWallKind);
                int cutawayWallCount = office.Furniture.Count(item =>
                    item.KindId == OfficeGridLayouts.PerimeterCutawayWallKind);
                PlacedOfficeFurniture openPassage = office.Furniture.Single(item =>
                    item.FurnitureId == "entrance_door");
                Require(fullWallCount == 24 && cutawayWallCount == 23 &&
                        openPassage.KindId == OfficeGridLayouts.EntranceDoorKind &&
                        openPassage.Origin.Equals(new OfficeGridCoordinate(8, 0)) &&
                        openPassage.Width == 1 && openPassage.Height == 1 &&
                        !openPassage.BlocksMovement,
                    $"Starter office must have four tile-aligned perimeter edges and one open passage; " +
                    $"full={fullWallCount} cutaway={cutawayWallCount} total={office.Furniture.Count}.");
                for (var axis = 0; axis < 12; axis++)
                {
                    Require(office.Furniture.Any(item =>
                            item.Origin.Equals(new OfficeGridCoordinate(axis, 0)) &&
                            item.Facing == OfficeFurnitureFacing.SouthEast),
                        $"Front wall is missing its tile bay at ({axis},0).");
                    Require(office.Furniture.Any(item =>
                            item.Origin.Equals(new OfficeGridCoordinate(axis, 12)) &&
                            item.Facing == OfficeFurnitureFacing.SouthEast),
                        $"Back wall is missing its tile bay at ({axis},12).");
                    var sideY = axis;
                    Require(office.Furniture.Any(item =>
                            item.Origin.Equals(new OfficeGridCoordinate(0, sideY)) &&
                            item.Facing == OfficeFurnitureFacing.SouthWest),
                        $"Left wall is missing its tile bay at (0,{sideY}).");
                    Require(office.Furniture.Any(item =>
                            item.Origin.Equals(new OfficeGridCoordinate(12, sideY)) &&
                            item.Facing == OfficeFurnitureFacing.SouthWest),
                        $"Right wall is missing its tile bay at (12,{sideY}).");
                }
                Require(OfficeAttendanceRules.Resolve(day.AddHours(18)) ==
                        OfficeAttendancePhase.AfterWork, "18:00 must begin departure.");
                Require(OfficeAttendanceRules.Resolve(day.AddDays(5).AddHours(10)) ==
                        OfficeAttendancePhase.AfterWork, "Weekend office must stay closed.");
                foreach (FamilyRole role in Enum.GetValues(typeof(FamilyRole)))
                    Require(FamilyScheduleRules.Resolve(role, day.AddHours(10)).CanPerformCompanyWork,
                        role + " must be available during the common office shift.");
                var state = FamilyCompany.Simulation.Prototype.PrototypeStateFactory.Create(20260813);
                AutonomousOfficeSimulation.EnsureIntents(state.WorldSeed, state.Family, 0);
                Require(state.Family.Members.All(member =>
                        member.Autonomy.CurrentAction == AutonomousOfficeAction.OffDuty &&
                        member.Autonomy.TargetLocation == OfficeSemanticLocation.Exit &&
                        member.Autonomy.ActionEndsMinute == 10),
                    "08:50 off-duty intent must expire exactly at the 09:00 opening.");
                AutonomousOfficeSimulation.EnsureIntents(state.WorldSeed, state.Family, 10);
                Require(state.Family.Members.All(member =>
                        member.Autonomy.TargetLocation != OfficeSemanticLocation.Exit),
                    "09:00 office intents must not send newly arrived family back to the exit.");
                Debug.Log(
                    "OFFICE_ATTENDANCE_VALIDATION: PASS | start=08:50 entry=09:00..09:03 " +
                    "family=4 perimeterBays=48 openPassage=(8,0) oneTile=true nonBlocking=true " +
                    "doorSfxWindow=09:00..17:59 clockJump=09:50 dayJump=Sunday10:00..Monday10:00 " +
                    "midShiftLoadArmed=false exit=18:00");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("OFFICE_ATTENDANCE_VALIDATION: FAIL");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        public static void RunBatch()
        {
            try
            {
                Run();
                EditorApplication.Exit(0);
            }
            catch
            {
                EditorApplication.Exit(1);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
