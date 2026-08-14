using System;
using System.Linq;
using FamilyCompany.Save;
using FamilyCompany.Simulation.Family;
using FamilyCompany.Simulation.Game;
using FamilyCompany.Simulation.Prototype;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public static class AutonomyLongRunValidation
    {
        private const int ValidationSeed = 20000103;
        private const int MinutesPerHour = 60;
        private const int MinutesPerDay = 1440;
        private const int LongRunDays = 7;

        [MenuItem("Family Company/Validate Autonomy Long Run")]
        public static void Run()
        {
            try
            {
                ValidateDailyJumpMatchesHourlySteps();
                var longRunState = ValidateSevenDayBoundsAndRecovery();
                ValidateForcedBurnoutRecovery();
                ValidateCurrentSaveRoundTrip(longRunState);
                Debug.Log("FAMILY_COMPANY_AUTONOMY_LONG_RUN_VALIDATION: PASS");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("FAMILY_COMPANY_AUTONOMY_LONG_RUN_VALIDATION: FAIL");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        private static void ValidateDailyJumpMatchesHourlySteps()
        {
            var dailyJump = PrototypeStateFactory.Create(ValidationSeed);
            var hourlySteps = PrototypeStateFactory.Create(ValidationSeed);
            new SimulationRunner(dailyJump).AdvanceMinutes(MinutesPerDay);

            var hourlyRunner = new SimulationRunner(hourlySteps);
            for (var hour = 0; hour < 24; hour++)
            {
                hourlyRunner.AdvanceMinutes(MinutesPerHour);
            }

            AssertStateEqual(dailyJump, hourlySteps, "same-seed daily jump versus hourly steps");
        }

        private static GameState ValidateSevenDayBoundsAndRecovery()
        {
            var state = PrototypeStateFactory.Create(ValidationSeed);
            var runner = new SimulationRunner(state);
            for (var day = 0; day < LongRunDays; day++)
            {
                runner.AdvanceMinutes(MinutesPerDay);
            }

            foreach (var member in state.Family.Members)
            {
                AssertInRange(member.Energy, 0, 100, $"{member.MemberId} seven-day energy");
                AssertInRange(member.Stress, 0, 100, $"{member.MemberId} seven-day stress");
                AssertEqual(
                    state.Time.ElapsedMinutes,
                    member.Autonomy.LastProcessedMinute,
                    $"{member.MemberId} autonomy processed minute");
            }

            var completedBreaks = state.Family.Members.Sum(member => member.Autonomy.CompletedBreaks);
            if (completedBreaks <= 0)
            {
                throw new InvalidOperationException("Seven-day autonomy run did not produce any recovery break.");
            }

            return state;
        }

        private static void ValidateForcedBurnoutRecovery()
        {
            var state = PrototypeStateFactory.Create(ValidationSeed + 1);
            var member = state.Family.Get("older_sister");
            member.ChangeEnergy(5 - member.Energy);
            member.ChangeStress(95 - member.Stress);
            var energyBefore = member.Energy;
            var stressBefore = member.Stress;

            new SimulationRunner(state).AdvanceMinutes(AutonomousOfficeSimulation.PulseMinutes);

            AssertEqual(AutonomousOfficeAction.BurnoutRecovery, member.Autonomy.CurrentAction, "forced burnout action");
            AssertEqual(1, member.Autonomy.BurnoutCount, "forced burnout count");
            if (member.Energy <= energyBefore)
            {
                throw new InvalidOperationException("Forced burnout recovery did not restore energy.");
            }

            if (member.Stress >= stressBefore)
            {
                throw new InvalidOperationException("Forced burnout recovery did not reduce stress.");
            }

            if (member.Autonomy.CompletedBreaks <= 0)
            {
                throw new InvalidOperationException("Forced burnout recovery was not counted as a recovery break.");
            }
        }

        private static void ValidateCurrentSaveRoundTrip(GameState source)
        {
            var sourceDto = GameSaveMapper.ToDto(source);
            AssertEqual(8, sourceDto.schemaVersion, "save schema v8");
            var restored = GameSaveMapper.FromDto(sourceDto);
            AssertStateEqual(source, restored, "save v8 round trip");
        }

        private static void AssertStateEqual(GameState expected, GameState actual, string label)
        {
            var expectedJson = JsonUtility.ToJson(GameSaveMapper.ToDto(expected));
            var actualJson = JsonUtility.ToJson(GameSaveMapper.ToDto(actual));
            if (!string.Equals(expectedJson, actualJson, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"{label}: serialized states differ.");
            }
        }

        private static void AssertInRange(int value, int minimum, int maximum, string label)
        {
            if (value < minimum || value > maximum)
            {
                throw new InvalidOperationException($"{label}: expected {minimum}..{maximum}, got {value}.");
            }
        }

        private static void AssertEqual<T>(T expected, T actual, string label)
        {
            if (!Equals(expected, actual))
            {
                throw new InvalidOperationException($"{label}: expected {expected}, got {actual}.");
            }
        }
    }
}
