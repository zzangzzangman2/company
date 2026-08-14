using System;
using System.Collections.Generic;
using System.Linq;

namespace FamilyCompany.Simulation.Workforce
{
    public static class WorkforcePotentialGradeRules
    {
        public static WorkforcePotentialGrade Resolve(int potential)
        {
            if (potential < 0 || potential > 100) throw new ArgumentOutOfRangeException(nameof(potential));
            if (potential >= 90) return WorkforcePotentialGrade.S;
            if (potential >= 80) return WorkforcePotentialGrade.A;
            if (potential >= 65) return WorkforcePotentialGrade.B;
            if (potential >= 50) return WorkforcePotentialGrade.C;
            if (potential >= 35) return WorkforcePotentialGrade.D;
            return WorkforcePotentialGrade.F;
        }

        public static string DisplayLetter(WorkforcePotentialGrade grade) => grade.ToString();
    }

    public sealed class WorkSkillWeights
    {
        private readonly int[] _weights;

        public WorkSkillWeights(
            int engineering,
            int planning,
            int creative,
            int business,
            int operations,
            int collaboration)
        {
            _weights = new[] { engineering, planning, creative, business, operations, collaboration };
            if (_weights.Any(value => value < 0)) throw new ArgumentOutOfRangeException(nameof(engineering));
            if (_weights.Sum() != WorkforcePerformanceRules.BasisPointDenominator)
                throw new InvalidOperationException("Work skill weights must total 10,000 basis points.");
        }

        public int Get(WorkSkillId skillId)
        {
            if (!Enum.IsDefined(typeof(WorkSkillId), skillId)) throw new ArgumentOutOfRangeException(nameof(skillId));
            return _weights[(int)skillId];
        }
    }

    public sealed class WorkTaskProfile
    {
        public WorkTaskProfile(
            string taskId,
            WorkSkillWeights progressWeights,
            WorkSkillWeights qualityWeights,
            WorkSkillWeights learningWeights)
        {
            if (string.IsNullOrWhiteSpace(taskId)) throw new ArgumentException("Task ID is required.", nameof(taskId));
            TaskId = taskId;
            ProgressWeights = progressWeights ?? throw new ArgumentNullException(nameof(progressWeights));
            QualityWeights = qualityWeights ?? throw new ArgumentNullException(nameof(qualityWeights));
            LearningWeights = learningWeights ?? throw new ArgumentNullException(nameof(learningWeights));
        }

        public string TaskId { get; }
        public WorkSkillWeights ProgressWeights { get; }
        public WorkSkillWeights QualityWeights { get; }
        public WorkSkillWeights LearningWeights { get; }
    }

    public static class WorkforcePerformanceRules
    {
        public const int BasisPointDenominator = 10_000;
        public const int MinimumWorkRateBasisPoints = 7_000;
        public const int NeutralWorkRateBasisPoints = 10_000;
        public const int MaximumWorkRateBasisPoints = 13_000;

        public static int CalculateScore(WorkSkillSet skills, WorkSkillWeights weights)
        {
            if (skills == null) throw new ArgumentNullException(nameof(skills));
            if (weights == null) throw new ArgumentNullException(nameof(weights));
            long weighted = 0;
            foreach (WorkSkillId skillId in Enum.GetValues(typeof(WorkSkillId)))
                weighted += (long)skills.Get(skillId) * weights.Get(skillId);
            return checked((int)((weighted + BasisPointDenominator / 2) / BasisPointDenominator));
        }

        public static int CalculateWorkRateBasisPoints(WorkforceCapabilityState capability, WorkTaskProfile task)
        {
            if (capability == null) throw new ArgumentNullException(nameof(capability));
            if (task == null) throw new ArgumentNullException(nameof(task));
            return MinimumWorkRateBasisPoints + CalculateScore(capability.Skills, task.ProgressWeights) * 60;
        }

        public static int CalculateGameMinutesPerPersonHour(WorkforceCapabilityState capability, WorkTaskProfile task)
        {
            var rateBasisPoints = CalculateWorkRateBasisPoints(capability, task);
            const long neutralPersonHourMinutes = 60L;
            return checked((int)((neutralPersonHourMinutes * BasisPointDenominator + rateBasisPoints - 1L) /
                                 rateBasisPoints));
        }

        public static int CalculateQualityScore(WorkforceCapabilityState capability, WorkTaskProfile task)
        {
            if (capability == null) throw new ArgumentNullException(nameof(capability));
            if (task == null) throw new ArgumentNullException(nameof(task));
            return CalculateScore(capability.Skills, task.QualityWeights);
        }

        public static int CalculateWeightedTeamScore(
            IEnumerable<KeyValuePair<WorkforceCapabilityState, int>> contributions,
            WorkTaskProfile task,
            bool quality)
        {
            if (contributions == null) throw new ArgumentNullException(nameof(contributions));
            if (task == null) throw new ArgumentNullException(nameof(task));
            long weighted = 0;
            long total = 0;
            foreach (var contribution in contributions.OrderBy(item => item.Key.MemberId, StringComparer.Ordinal))
            {
                if (contribution.Key == null || contribution.Value <= 0) continue;
                var score = quality
                    ? CalculateQualityScore(contribution.Key, task)
                    : CalculateScore(contribution.Key.Skills, task.ProgressWeights);
                weighted = checked(weighted + (long)score * contribution.Value);
                total = checked(total + contribution.Value);
            }
            return total == 0 ? 0 : checked((int)((weighted + total / 2) / total));
        }
    }

    public static class WorkforceStressRules
    {
        public const int NeutralStressGainBasisPoints = 10_000;
        public const int MinimumStressGainBasisPoints = 8_000;
        public const int MaximumStressGainBasisPoints = 12_000;

        public static int ClampStressGainBasisPoints(int value) =>
            Math.Max(MinimumStressGainBasisPoints, Math.Min(MaximumStressGainBasisPoints, value));

        public static int ApplyAuthoritativeStressGain(int baseGain, int stressGainBasisPoints)
        {
            if (baseGain < 0) throw new ArgumentOutOfRangeException(nameof(baseGain));
            var multiplier = ClampStressGainBasisPoints(stressGainBasisPoints);
            return checked((int)(((long)baseGain * multiplier + 5_000) / 10_000));
        }

        public static int ResistancePercent(int stressGainBasisPoints) =>
            Math.Max(0, Math.Min(100, (12_000 - ClampStressGainBasisPoints(stressGainBasisPoints)) / 40));
    }
}
