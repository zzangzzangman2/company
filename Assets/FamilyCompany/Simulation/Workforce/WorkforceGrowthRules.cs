using System;

namespace FamilyCompany.Simulation.Workforce
{
    public sealed class WorkforceGrowthResult
    {
        public WorkforceGrowthResult(long authoritativeMinutes, int levelsGained)
        {
            AuthoritativeMinutes = authoritativeMinutes;
            LevelsGained = levelsGained;
        }

        public long AuthoritativeMinutes { get; }
        public int LevelsGained { get; }
    }

    public static class WorkforceGrowthRules
    {
        public const long FixedPointDenominator = 100_000_000L;

        public static long ExperienceRequiredForNextLevel(int currentSkill)
        {
            if (currentSkill < 0 || currentSkill > 100) throw new ArgumentOutOfRangeException(nameof(currentSkill));
            return currentSkill >= 100 ? 0 : 600L + currentSkill * 30L;
        }

        public static WorkforceGrowthResult ApplyAuthoritativeContributionMinutes(
            WorkforceCapabilityState capability,
            WorkTaskProfile task,
            long completedContributionMinutes)
        {
            if (capability == null) throw new ArgumentNullException(nameof(capability));
            if (task == null) throw new ArgumentNullException(nameof(task));
            if (completedContributionMinutes < 0) throw new ArgumentOutOfRangeException(nameof(completedContributionMinutes));
            if (completedContributionMinutes == 0) return new WorkforceGrowthResult(0, 0);

            var learningRateBasisPoints = 8_000 + capability.Potential * 40;
            var totalLevels = 0;
            foreach (WorkSkillId skillId in Enum.GetValues(typeof(WorkSkillId)))
            {
                var numerator = checked(
                    checked(completedContributionMinutes * task.LearningWeights.Get(skillId)) *
                    learningRateBasisPoints + capability.FixedPointRemainder(skillId));
                var earnedExperience = numerator / FixedPointDenominator;
                var remainder = numerator % FixedPointDenominator;
                var experience = checked(capability.Experience(skillId) + earnedExperience);
                var value = capability.Skills.Get(skillId);
                while (value < 100)
                {
                    var required = ExperienceRequiredForNextLevel(value);
                    if (experience < required) break;
                    experience -= required;
                    value++;
                    totalLevels++;
                }
                if (value >= 100) experience = 0;
                capability.ReplaceProgress(skillId, value, experience, remainder);
            }
            return new WorkforceGrowthResult(completedContributionMinutes, totalLevels);
        }
    }
}
