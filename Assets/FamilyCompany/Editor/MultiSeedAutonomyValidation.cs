using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Save;
using FamilyCompany.Simulation.Family;
using FamilyCompany.Simulation.Game;
using FamilyCompany.Simulation.Prototype;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public static class MultiSeedAutonomyValidation
    {
        private const int SeedCount = 16;
        private const int BaseSeed = 20000103;
        private const int SeedStep = 7919;
        private const int ValidationDays = 30;
        private const int MinutesPerHour = 60;
        private const int MinutesPerDay = 1440;
        private const int ValidationMinutes = ValidationDays * MinutesPerDay;

        [MenuItem("Family Company/Validate Multi-Seed Autonomy")]
        public static void Run()
        {
            try
            {
                var utilizationSignatures = new HashSet<string>(StringComparer.Ordinal);
                var totalWorkBlocks = 0L;
                var totalBreaks = 0L;
                for (var index = 0; index < SeedCount; index++)
                {
                    var seed = checked(BaseSeed + index * SeedStep);
                    var singleJump = PrototypeStateFactory.Create(seed);
                    var hourlySteps = PrototypeStateFactory.Create(seed);

                    new SimulationRunner(singleJump).AdvanceMinutes(ValidationMinutes);
                    AdvanceHourly(hourlySteps, ValidationMinutes);
                    AssertStateEqual(singleJump, hourlySteps, $"seed {seed} 30-day step determinism");

                    ValidateMemberBounds(hourlySteps, seed);
                    var seedWorkBlocks = hourlySteps.Family.Members.Sum(member => (long)member.Autonomy.CompletedWorkBlocks);
                    var seedBreaks = hourlySteps.Family.Members.Sum(member => (long)member.Autonomy.CompletedBreaks);
                    if (seedWorkBlocks <= 0)
                        throw new InvalidOperationException($"seed {seed}: no autonomous work occurred in 30 days.");
                    if (seedBreaks <= 0)
                        throw new InvalidOperationException($"seed {seed}: no autonomous recovery break occurred in 30 days.");

                    totalWorkBlocks = checked(totalWorkBlocks + seedWorkBlocks);
                    totalBreaks = checked(totalBreaks + seedBreaks);
                    var capacity = ValidateCapacity(hourlySteps, seed);
                    utilizationSignatures.Add(BuildUtilizationSignature(hourlySteps, capacity));
                    ValidateCurrentSaveRoundTrip(hourlySteps, seed);
                }

                var minimumDistinctSignatures = Math.Max(2, SeedCount / 4);
                if (utilizationSignatures.Count < minimumDistinctSignatures)
                {
                    throw new InvalidOperationException(
                        $"Autonomous person-hour utilization collapsed to {utilizationSignatures.Count} paths across {SeedCount} seeds; " +
                        $"expected at least {minimumDistinctSignatures} distinct distributions.");
                }

                Debug.Log(
                    $"FAMILY_COMPANY_MULTI_SEED_AUTONOMY_VALIDATION: PASS · seeds={SeedCount} · days={ValidationDays} · " +
                    $"workBlocks={totalWorkBlocks} · breaks={totalBreaks} · utilizationPaths={utilizationSignatures.Count}");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("FAMILY_COMPANY_MULTI_SEED_AUTONOMY_VALIDATION: FAIL");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        private static void AdvanceHourly(GameState state, int totalMinutes)
        {
            var runner = new SimulationRunner(state);
            var remaining = totalMinutes;
            while (remaining > 0)
            {
                var step = Math.Min(MinutesPerHour, remaining);
                runner.AdvanceMinutes(step);
                remaining -= step;
            }
        }

        private static void ValidateMemberBounds(GameState state, int seed)
        {
            foreach (var member in state.Family.Members)
            {
                AssertInRange(member.Energy, 0, 100, $"seed {seed} {member.MemberId} energy");
                AssertInRange(member.Stress, 0, 100, $"seed {seed} {member.MemberId} stress");
                AssertEqual(
                    state.Time.ElapsedMinutes,
                    member.Autonomy.LastProcessedMinute,
                    $"seed {seed} {member.MemberId} processed minute");
                if (member.Autonomy.CompletedWorkBlocks <= 0)
                    throw new InvalidOperationException($"seed {seed}: {member.MemberId} never performed autonomous work.");
            }
        }

        private static FamilyWorkCapacityPlan ValidateCapacity(GameState state, int seed)
        {
            var capacity = FamilyWorkCapacityPlanner.Calculate(state.Family, 0, ValidationMinutes);
            if (capacity.TotalAvailablePersonHours <= 0)
                throw new InvalidOperationException($"seed {seed}: family has no available person-hours.");
            if (capacity.PeakConcurrentMemberCount <= 0)
                throw new InvalidOperationException($"seed {seed}: family never has concurrent company availability.");

            foreach (var member in capacity.Members)
            {
                if (member.AvailableBlockCount <= 0)
                    throw new InvalidOperationException($"seed {seed}: {member.MemberId} has no scheduled company capacity.");
                var completed = state.Family.Get(member.MemberId).Autonomy.CompletedWorkBlocks;
                if (completed > member.AvailableBlockCount)
                {
                    throw new InvalidOperationException(
                        $"seed {seed}: {member.MemberId} completed {completed} work blocks beyond " +
                        $"scheduled capacity {member.AvailableBlockCount}.");
                }
            }

            return capacity;
        }

        private static string BuildUtilizationSignature(GameState state, FamilyWorkCapacityPlan capacity)
        {
            return string.Join(
                "|",
                capacity.Members
                    .OrderBy(member => member.MemberId, StringComparer.Ordinal)
                    .Select(member =>
                    {
                        var completed = state.Family.Get(member.MemberId).Autonomy.CompletedWorkBlocks;
                        var utilizationPermille = checked((long)completed * 1000L / member.AvailableBlockCount);
                        return $"{member.MemberId}:{utilizationPermille}";
                    }));
        }

        private static void ValidateCurrentSaveRoundTrip(GameState source, int seed)
        {
            var dto = GameSaveMapper.ToDto(source);
            AssertEqual(10, dto.schemaVersion, $"seed {seed} save schema");
            var restored = GameSaveMapper.FromDto(dto);
            AssertStateEqual(source, restored, $"seed {seed} save v8 round trip");
        }

        private static void AssertStateEqual(GameState expected, GameState actual, string label)
        {
            var expectedJson = JsonUtility.ToJson(GameSaveMapper.ToDto(expected));
            var actualJson = JsonUtility.ToJson(GameSaveMapper.ToDto(actual));
            if (!string.Equals(expectedJson, actualJson, StringComparison.Ordinal))
                throw new InvalidOperationException($"{label}: serialized v5 states differ.");
        }

        private static void AssertInRange(int value, int minimum, int maximum, string label)
        {
            if (value < minimum || value > maximum)
                throw new InvalidOperationException($"{label}: expected {minimum}..{maximum}, got {value}.");
        }

        private static void AssertEqual<T>(T expected, T actual, string label)
        {
            if (!Equals(expected, actual))
                throw new InvalidOperationException($"{label}: expected {expected}, got {actual}.");
        }
    }
}
