using System;
using System.Collections.Generic;
using FamilyCompany.Simulation.Core;
using FamilyCompany.Simulation.Leisure;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public static class LeisureExecutionValidation
    {
        [MenuItem("Family Company/Validate Leisure Execution")]
        public static void Run()
        {
            try
            {
                ValidateFreeSoloExecution();
                ValidatePaidFamilyExecution();
                ValidateAvailabilityRejections();
                ValidateParticipantAndTimeRejections();
                ValidateFundingBoundary();
                ValidateOrderIndependenceAndDeterminism();
                Debug.Log("FAMILY_COMPANY_LEISURE_EXECUTION_VALIDATION: PASS");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("FAMILY_COMPANY_LEISURE_EXECUTION_VALIDATION: FAIL");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        private static void ValidateFreeSoloExecution()
        {
            var startMinute = MinuteAt(new DateTime(2000, 1, 3, 19, 0, 0));
            var result = LeisureExecutionRules.Evaluate(
                "neighborhood_evening_walk",
                startMinute,
                new[] { Participant("player", 98, 6) },
                75,
                0,
                0);

            AssertEqual(true, result.Succeeded, "free walk succeeds");
            AssertEqual(LeisureExecutionRejectionReason.None, result.RejectionReason, "free walk rejection reason");
            AssertEqual(startMinute + 60, result.EndMinute, "free walk end minute");
            AssertEqual(60, result.DurationMinutes, "free walk duration");
            AssertEqual(0L, result.TotalCostWon, "free walk cost");
            AssertEqual(LeisureFundingSource.None, result.FundingSource, "free walk funding source");
            AssertEqual(75, result.FamilyBondAfter, "solo activity has no shared bond effect");
            AssertEqual(0, result.AppliedFamilyBondDelta, "solo applied bond delta");

            var player = result.GetParticipant("player");
            AssertEqual(100, player.EnergyAfter, "energy clamps at 100");
            AssertEqual(2, player.AppliedEnergyDelta, "actual clamped energy delta");
            AssertEqual(0, player.StressAfter, "stress clamps at zero");
            AssertEqual(-6, player.AppliedStressDelta, "actual clamped stress delta");
            AssertConserved(result, "free walk conservation");
        }

        private static void ValidatePaidFamilyExecution()
        {
            var startMinute = MinuteAt(new DateTime(2000, 1, 7, 19, 0, 0));
            var result = LeisureExecutionRules.Evaluate(
                "family_restaurant_dinner",
                startMinute,
                new[]
                {
                    Participant("father", 95, 10),
                    Participant("player", 40, 80)
                },
                98,
                30_000,
                10_000);

            AssertEqual(true, result.Succeeded, "family dinner succeeds");
            AssertEqual(32_000L, result.RequiredCostWon, "family dinner required cost");
            AssertEqual(10_000L, result.HouseholdCostWon, "household cash used first");
            AssertEqual(22_000L, result.CompanyCostWon, "company covers remainder");
            AssertEqual(LeisureFundingSource.HouseholdAndCompany, result.FundingSource, "split funding source");
            AssertEqual(0L, result.HouseholdCashAfterWon, "household cash after dinner");
            AssertEqual(8_000L, result.CompanyCashAfterWon, "company cash after dinner");
            AssertEqual(100, result.FamilyBondAfter, "family bond clamps at 100");
            AssertEqual(2, result.AppliedFamilyBondDelta, "actual family bond delta");
            AssertEqual(100, result.GetParticipant("father").EnergyAfter, "father energy clamps");
            AssertEqual(0, result.GetParticipant("father").StressAfter, "father stress clamps");
            AssertEqual(54, result.GetParticipant("player").EnergyAfter, "player energy effect");
            AssertEqual(62, result.GetParticipant("player").StressAfter, "player stress effect");
            AssertConserved(result, "family dinner conservation");
        }

        private static void ValidateAvailabilityRejections()
        {
            var tuesday = MinuteAt(new DateTime(2000, 1, 4, 19, 0, 0));
            AssertRejected(
                LeisureExecutionRules.Evaluate(
                    "pc_bang_team_match", tuesday,
                    new[] { Participant("player", 50, 50) }, 50, 100_000, 100_000),
                LeisureExecutionRejectionReason.DayUnavailable,
                "weekday PC room rejected");

            var friday2000 = MinuteAt(new DateTime(2000, 1, 7, 19, 0, 0));
            AssertRejected(
                LeisureExecutionRules.Evaluate(
                    "family_singing_room", friday2000,
                    new[] { Participant("player", 50, 50), Participant("sister", 50, 50) },
                    50, 100_000, 100_000),
                LeisureExecutionRejectionReason.ActivityNotYetAvailable,
                "future singing room rejected");

            AssertRejected(
                LeisureExecutionRules.Evaluate(
                    "family_restaurant_dinner", friday2000,
                    new[] { Participant("player", 50, 50) },
                    50, 100_000, 100_000),
                LeisureExecutionRejectionReason.ParticipantCountUnavailable,
                "family dinner solo rejected");

            var friday2027 = MinuteAt(new DateTime(2027, 1, 8, 19, 0, 0));
            AssertRejected(
                LeisureExecutionRules.Evaluate(
                    "neighborhood_evening_walk", friday2027,
                    new[] { Participant("player", 50, 50) },
                    50, 100_000, 100_000),
                LeisureExecutionRejectionReason.ActivityExpired,
                "post-campaign activity rejected");
        }

        private static void ValidateParticipantAndTimeRejections()
        {
            var friday = MinuteAt(new DateTime(2000, 1, 7, 19, 0, 0));
            AssertRejected(
                LeisureExecutionRules.Evaluate(
                    "family_restaurant_dinner", friday,
                    new[] { Participant("player", 50, 50), Participant(" player ", 60, 40) },
                    50, 100_000, 100_000),
                LeisureExecutionRejectionReason.DuplicateParticipant,
                "trimmed duplicate participant rejected");
            AssertRejected(
                LeisureExecutionRules.Evaluate(
                    "neighborhood_evening_walk", friday,
                    new[] { Participant("player", 101, 50) },
                    50, 100_000, 100_000),
                LeisureExecutionRejectionReason.InvalidParticipantState,
                "invalid participant state rejected");
            AssertRejected(
                LeisureExecutionRules.Evaluate(
                    "neighborhood_evening_walk", -1,
                    new[] { Participant("player", 50, 50) },
                    50, 100_000, 100_000),
                LeisureExecutionRejectionReason.InvalidStartMinute,
                "negative start minute rejected");
            AssertRejected(
                LeisureExecutionRules.Evaluate(
                    "neighborhood_evening_walk", long.MaxValue,
                    new[] { Participant("player", 50, 50) },
                    50, 100_000, 100_000),
                LeisureExecutionRejectionReason.InvalidStartMinute,
                "overflowing start minute rejected");
            AssertRejected(
                LeisureExecutionRules.Evaluate(
                    "missing_activity", friday,
                    new[] { Participant("player", 50, 50) },
                    50, 100_000, 100_000),
                LeisureExecutionRejectionReason.UnknownActivity,
                "unknown activity rejected");
        }

        private static void ValidateFundingBoundary()
        {
            var friday = MinuteAt(new DateTime(2000, 1, 7, 19, 0, 0));
            var insufficient = LeisureExecutionRules.Evaluate(
                "family_restaurant_dinner",
                friday,
                new[] { Participant("player", 50, 50), Participant("father", 50, 50) },
                50,
                21_999,
                10_000);
            AssertRejected(insufficient, LeisureExecutionRejectionReason.InsufficientFunds, "one-won shortage rejected");
            AssertEqual(21_999L, insufficient.CompanyCashAfterWon, "failed company cash unchanged");
            AssertEqual(10_000L, insufficient.HouseholdCashAfterWon, "failed household cash unchanged");
            AssertEqual(0, insufficient.ParticipantEffects.Count, "failed execution has no effects");

            var exact = LeisureExecutionRules.Evaluate(
                "family_restaurant_dinner",
                friday,
                new[] { Participant("player", 50, 50), Participant("father", 50, 50) },
                50,
                22_000,
                10_000);
            AssertEqual(true, exact.Succeeded, "exact combined cash succeeds");
            AssertEqual(0L, exact.CompanyCashAfterWon, "exact company cash consumed");
            AssertEqual(0L, exact.HouseholdCashAfterWon, "exact household cash consumed");
            AssertConserved(exact, "exact funding conservation");

            AssertRejected(
                LeisureExecutionRules.Evaluate(
                    "neighborhood_evening_walk", friday,
                    new[] { Participant("player", 50, 50) },
                    50, -1, 0),
                LeisureExecutionRejectionReason.InvalidFunds,
                "negative funds rejected");
        }

        private static void ValidateOrderIndependenceAndDeterminism()
        {
            var saturday = MinuteAt(new DateTime(2002, 1, 5, 20, 0, 0));
            var canonical = new[]
            {
                Participant("father", 60, 45),
                Participant("mother", 70, 35),
                Participant("player", 45, 75),
                Participant("sister", 55, 65)
            };
            var reordered = new[] { canonical[3], canonical[1], canonical[0], canonical[2] };
            var first = LeisureExecutionRules.Evaluate(
                "adsl_coop_game_night", saturday, canonical, 72, 100_000, 100_000);
            var second = LeisureExecutionRules.Evaluate(
                "adsl_coop_game_night", saturday, reordered, 72, 100_000, 100_000);
            var repeated = LeisureExecutionRules.Evaluate(
                "adsl_coop_game_night", saturday, canonical, 72, 100_000, 100_000);

            AssertResultsEqual(first, second, "participant order independence");
            AssertResultsEqual(first, repeated, "same input determinism");
            AssertEqual("father", first.ParticipantEffects[0].FamilyMemberId, "effects sorted by family ID 0");
            AssertEqual("mother", first.ParticipantEffects[1].FamilyMemberId, "effects sorted by family ID 1");
            AssertEqual("player", first.ParticipantEffects[2].FamilyMemberId, "effects sorted by family ID 2");
            AssertEqual("sister", first.ParticipantEffects[3].FamilyMemberId, "effects sorted by family ID 3");
            AssertConserved(first, "deterministic result conservation");
        }

        private static LeisureParticipantInput Participant(string id, int energy, int stress)
        {
            return new LeisureParticipantInput(id, energy, stress);
        }

        private static long MinuteAt(DateTime at)
        {
            if (at < GameTime.CampaignStart) throw new ArgumentOutOfRangeException(nameof(at));
            return (at.Ticks - GameTime.CampaignStart.Ticks) / TimeSpan.TicksPerMinute;
        }

        private static void AssertRejected(
            LeisureExecutionResult result,
            LeisureExecutionRejectionReason expectedReason,
            string label)
        {
            AssertEqual(false, result.Succeeded, label + " success flag");
            AssertEqual(expectedReason, result.RejectionReason, label + " reason");
            AssertEqual(0L, result.TotalCostWon, label + " no applied cost");
            AssertEqual(result.HouseholdCashBeforeWon, result.HouseholdCashAfterWon, label + " household unchanged");
            AssertEqual(result.CompanyCashBeforeWon, result.CompanyCashAfterWon, label + " company unchanged");
            AssertEqual(result.FamilyBondBefore, result.FamilyBondAfter, label + " bond unchanged");
            AssertEqual(0, result.ParticipantEffects.Count, label + " no participant effects");
        }

        private static void AssertConserved(LeisureExecutionResult result, string label)
        {
            AssertEqual(result.RequiredCostWon, result.TotalCostWon, label + " required cost applied");
            AssertEqual(result.TotalCostWon, result.HouseholdCostWon + result.CompanyCostWon, label + " cost split");
            AssertEqual(
                result.HouseholdCashBeforeWon,
                result.HouseholdCashAfterWon + result.HouseholdCostWon,
                label + " household cash");
            AssertEqual(
                result.CompanyCashBeforeWon,
                result.CompanyCashAfterWon + result.CompanyCostWon,
                label + " company cash");
        }

        private static void AssertResultsEqual(
            LeisureExecutionResult expected,
            LeisureExecutionResult actual,
            string label)
        {
            AssertEqual(expected.Succeeded, actual.Succeeded, label + " succeeded");
            AssertEqual(expected.RejectionReason, actual.RejectionReason, label + " reason");
            AssertEqual(expected.ActivityId, actual.ActivityId, label + " activity");
            AssertEqual(expected.StartMinute, actual.StartMinute, label + " start");
            AssertEqual(expected.EndMinute, actual.EndMinute, label + " end");
            AssertEqual(expected.TotalCostWon, actual.TotalCostWon, label + " total cost");
            AssertEqual(expected.HouseholdCostWon, actual.HouseholdCostWon, label + " household cost");
            AssertEqual(expected.CompanyCostWon, actual.CompanyCostWon, label + " company cost");
            AssertEqual(expected.FamilyBondAfter, actual.FamilyBondAfter, label + " bond");
            AssertEqual(expected.ParticipantEffects.Count, actual.ParticipantEffects.Count, label + " effect count");
            for (var index = 0; index < expected.ParticipantEffects.Count; index++)
            {
                var left = expected.ParticipantEffects[index];
                var right = actual.ParticipantEffects[index];
                AssertEqual(left.FamilyMemberId, right.FamilyMemberId, label + " member " + index);
                AssertEqual(left.EnergyAfter, right.EnergyAfter, label + " energy " + index);
                AssertEqual(left.StressAfter, right.StressAfter, label + " stress " + index);
            }
        }

        private static void AssertEqual<T>(T expected, T actual, string label)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
            }
        }
    }
}
