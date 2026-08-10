using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FamilyCompany.Simulation.Core;

namespace FamilyCompany.Simulation.Leisure
{
    public sealed class LeisureEffectPreview
    {
        public LeisureEffectPreview(
            LeisureActivityDefinition activity,
            int participantCount,
            int energyAfter,
            int stressAfter,
            int familyBondAfter,
            int appliedFamilyBondDelta)
        {
            Activity = activity ?? throw new ArgumentNullException(nameof(activity));
            ParticipantCount = participantCount;
            EnergyAfter = energyAfter;
            StressAfter = stressAfter;
            FamilyBondAfter = familyBondAfter;
            AppliedFamilyBondDelta = appliedFamilyBondDelta;
        }

        public LeisureActivityDefinition Activity { get; }
        public int ParticipantCount { get; }
        public long CostWon => Activity.CostWon;
        public int DurationMinutes => Activity.DurationMinutes;
        public int EnergyAfter { get; }
        public int StressAfter { get; }
        public int FamilyBondAfter { get; }
        public int AppliedFamilyBondDelta { get; }
    }

    public static class LeisureRecommendationRules
    {
        public const int DefaultRecommendationCount = 3;

        public static LeisureActivityDefinition Recommend(
            string simulationSeed,
            DateTime at,
            IEnumerable<string> participantIds)
        {
            return RankRecommendations(simulationSeed, at, participantIds, 1).FirstOrDefault();
        }

        public static IReadOnlyList<LeisureActivityDefinition> RankRecommendations(
            string simulationSeed,
            DateTime at,
            IEnumerable<string> participantIds,
            int maximumResults = DefaultRecommendationCount)
        {
            if (string.IsNullOrWhiteSpace(simulationSeed))
                throw new ArgumentException("Simulation seed is required.", nameof(simulationSeed));
            if (maximumResults < 1) throw new ArgumentOutOfRangeException(nameof(maximumResults));

            var participants = NormalizeParticipants(participantIds);
            var available = LeisureActivityCatalog.AvailableOn(at, participants.Count);
            var minuteKey = string.Format(
                CultureInfo.InvariantCulture,
                "{0:D4}{1:D2}{2:D2}-{3:D2}{4:D2}",
                at.Year,
                at.Month,
                at.Day,
                at.Hour,
                at.Minute);
            var participantKey = string.Join(
                "|",
                participants.Select(id => string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}:{1}",
                    id.Length,
                    id)));

            return available
                .Select(activity => new
                {
                    Activity = activity,
                    Score = StableRandom.StableRandomWord31(
                        $"leisure-recommendation-v1:{simulationSeed.Trim()}:{minuteKey}:{participantKey}:{activity.Id}")
                })
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.Activity.Id, StringComparer.Ordinal)
                .Take(Math.Min(maximumResults, available.Count))
                .Select(item => item.Activity)
                .ToArray();
        }

        public static LeisureEffectPreview PreviewEffects(
            LeisureActivityDefinition activity,
            int participantCount,
            int currentEnergy,
            int currentStress,
            int currentFamilyBond)
        {
            if (activity == null) throw new ArgumentNullException(nameof(activity));
            if (participantCount < activity.MinimumParticipants || participantCount > activity.MaximumParticipants)
                throw new ArgumentOutOfRangeException(nameof(participantCount));
            ValidatePercent(currentEnergy, nameof(currentEnergy));
            ValidatePercent(currentStress, nameof(currentStress));
            ValidatePercent(currentFamilyBond, nameof(currentFamilyBond));

            var sharedBondDelta = participantCount >= 2 ? activity.SharedFamilyBondDelta : 0;
            return new LeisureEffectPreview(
                activity,
                participantCount,
                Clamp100(currentEnergy + activity.EnergyDelta),
                Clamp100(currentStress + activity.StressDelta),
                Clamp100(currentFamilyBond + sharedBondDelta),
                sharedBondDelta);
        }

        private static IReadOnlyList<string> NormalizeParticipants(IEnumerable<string> participantIds)
        {
            if (participantIds == null) throw new ArgumentNullException(nameof(participantIds));
            var normalized = participantIds
                .Select(item => item == null ? string.Empty : item.Trim())
                .Where(item => item.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();
            if (normalized.Length < 1 || normalized.Length > 4)
                throw new ArgumentOutOfRangeException(
                    nameof(participantIds),
                    "One to four distinct participant IDs are required.");
            return normalized;
        }

        private static void ValidatePercent(int value, string parameterName)
        {
            if (value < 0 || value > 100) throw new ArgumentOutOfRangeException(parameterName);
        }

        private static int Clamp100(int value)
        {
            return Math.Max(0, Math.Min(100, value));
        }
    }
}
