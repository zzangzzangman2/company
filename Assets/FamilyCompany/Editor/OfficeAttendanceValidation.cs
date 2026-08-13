using System;
using System.Linq;
using FamilyCompany.Simulation.Core;
using FamilyCompany.Simulation.Family;
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
                Require(!OfficeAttendanceRules.HasArrived(day.AddHours(9).AddMinutes(10), 11),
                    "Twelfth actor must retain the shared stagger at 09:10.");
                Require(OfficeAttendanceRules.HasArrived(day.AddHours(9).AddMinutes(11), 11),
                    "All twelve family and employee actors must be inside by 09:11.");
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
                Debug.Log("OFFICE_ATTENDANCE_VALIDATION: PASS | start=08:50 entry=09:00..09:11 actors=12 exit=18:00");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("OFFICE_ATTENDANCE_VALIDATION: FAIL");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
