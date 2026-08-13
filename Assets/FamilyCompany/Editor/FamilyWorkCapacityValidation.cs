using System;
using FamilyCompany.Simulation.Core;
using FamilyCompany.Simulation.Family;
using FamilyCompany.Simulation.Prototype;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public static class FamilyWorkCapacityValidation
    {
        [MenuItem("Family Company/Validate Family Work Capacity")]
        public static void Run()
        {
            try
            {
                ValidateWeekdayScheduleBlocks();
                ValidateRoleCommitmentWindows();
                ValidateEarliestCompletion();
                ValidatePulseAlignment();
                Debug.Log("FAMILY_WORK_CAPACITY_VALIDATION: PASS");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("FAMILY_WORK_CAPACITY_VALIDATION: FAIL");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        private static void ValidateWeekdayScheduleBlocks()
        {
            var family = PrototypeStateFactory.Create(20260810).Family;
            var monday0850 = 0L;
            var monday23 = MinuteAt(0, 23, 0);
            var plan = FamilyWorkCapacityPlanner.Calculate(family, monday0850, monday23);

            AssertEqual(18L, plan.CompanyAvailableBlockCount, "Monday company-open blocks");
            AssertEqual(72L, plan.TotalAvailableMemberBlockCount, "Monday total member blocks");
            AssertEqual(36m, plan.TotalAvailablePersonHours, "Monday total person-hours");
            AssertEqual(4, plan.PeakConcurrentMemberCount, "Monday peak concurrent members");
            AssertEqual(18L, plan.GetMember("player").AvailableBlockCount, "player Monday blocks");
            AssertEqual(18L, plan.GetMember("older_sister").AvailableBlockCount, "sister Monday blocks");
            AssertEqual(18L, plan.GetMember("father").AvailableBlockCount, "father Monday blocks");
            AssertEqual(18L, plan.GetMember("mother").AvailableBlockCount, "mother Monday blocks");
        }

        private static void ValidateRoleCommitmentWindows()
        {
            var family = PrototypeStateFactory.Create(20260810).Family;

            AssertMemberBlocks(family, "player", 0, 9, 0, 18, 0, 18, "player office shift");
            AssertMemberBlocks(family, "player", 0, 18, 0, 19, 0, 0, "player after shift");

            AssertMemberBlocks(family, "older_sister", 0, 9, 0, 14, 0, 10, "sister office shift");
            AssertMemberBlocks(family, "older_sister", 0, 14, 0, 15, 0, 2, "sister office shift continuation");

            AssertMemberBlocks(family, "father", 0, 10, 0, 17, 0, 14, "father office shift");
            AssertMemberBlocks(family, "father", 0, 17, 0, 18, 0, 2, "father office shift continuation");

            AssertMemberBlocks(family, "mother", 0, 18, 0, 20, 0, 0, "mother household duty");
            AssertMemberBlocks(family, "mother", 0, 20, 0, 21, 0, 0, "mother after shift");

            AssertMemberBlocks(family, "older_sister", 5, 10, 0, 15, 0, 0, "sister Saturday part-time job");
            AssertMemberBlocks(family, "mother", 5, 8, 0, 11, 0, 0, "mother weekend household duty");
        }

        private static void ValidateEarliestCompletion()
        {
            var family = PrototypeStateFactory.Create(20260810).Family;
            var monday0850 = 0L;
            var monday23 = MinuteAt(0, 23, 0);

            var tenHours = FamilyWorkCapacityPlanner.EstimateEarliestCompletion(
                family,
                monday0850,
                monday23,
                10);
            AssertEqual(true, tenHours.CanComplete, "company ten-hour feasibility");
            AssertEqual(MinuteAt(0, 11, 30), tenHours.EarliestCompletionMinute, "company ten-hour completion");

            var fullCapacity = FamilyWorkCapacityPlanner.EstimateEarliestCompletion(
                family,
                monday0850,
                monday23,
                36);
            AssertEqual(true, fullCapacity.CanComplete, "company full-day feasibility");
            AssertEqual(MinuteAt(0, 18, 0), fullCapacity.EarliestCompletionMinute, "company full-day completion");

            var overflow = FamilyWorkCapacityPlanner.EstimateEarliestCompletion(
                family,
                monday0850,
                monday23,
                37);
            AssertEqual(false, overflow.CanComplete, "company overflow infeasible");
            AssertEqual(72L, overflow.AccumulatedMemberBlockCount, "company overflow accumulated blocks");

            var player = family.Get("player");
            var playerTwoHours = FamilyWorkCapacityPlanner.EstimateEarliestCompletion(
                player,
                monday0850,
                MinuteAt(0, 19, 0),
                2);
            AssertEqual(true, playerTwoHours.CanComplete, "player split-window feasibility");
            AssertEqual(MinuteAt(0, 11, 0), playerTwoHours.EarliestCompletionMinute, "player shift completion");
        }

        private static void ValidatePulseAlignment()
        {
            var player = PrototypeStateFactory.Create(20260810).Family.Get("player");
            AssertEqual(
                AutonomousOfficeSimulation.PulseMinutes,
                FamilyWorkCapacityPlanner.WorkBlockMinutes,
                "planner uses autonomy pulse");

            var partialOnly = FamilyWorkCapacityPlanner.CalculateMember(player, 5, 35);
            AssertEqual(0L, partialOnly.AvailableBlockCount, "partial pulse is not capacity");

            var oneCompleteBlock = FamilyWorkCapacityPlanner.CalculateMember(player, 5, 60);
            AssertEqual(1L, oneCompleteBlock.AvailableBlockCount, "aligned complete pulse capacity");
            AssertEqual(0.5m, oneCompleteBlock.AvailablePersonHours, "one pulse person-hours");
        }

        private static void AssertMemberBlocks(
            FamilyState family,
            string memberId,
            int dayOffset,
            int startHour,
            int startMinute,
            int endHour,
            int endMinute,
            long expectedBlocks,
            string label)
        {
            var result = FamilyWorkCapacityPlanner.CalculateMember(
                family.Get(memberId),
                MinuteAt(dayOffset, startHour, startMinute),
                MinuteAt(dayOffset, endHour, endMinute));
            AssertEqual(expectedBlocks, result.AvailableBlockCount, label);
        }

        private static long MinuteAt(int dayOffset, int hour, int minute)
        {
            var value = GameTime.CampaignStart.Date
                .AddDays(dayOffset)
                .AddHours(hour)
                .AddMinutes(minute);
            return checked((long)(value - GameTime.CampaignStart).TotalMinutes);
        }

        private static void AssertEqual<T>(T expected, T actual, string label)
        {
            if (!Equals(expected, actual))
            {
                throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
            }
        }
    }
}
