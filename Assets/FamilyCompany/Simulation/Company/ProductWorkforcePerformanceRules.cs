using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Simulation.Family;
using FamilyCompany.Simulation.Workforce;

namespace FamilyCompany.Simulation.Company
{
    public static class ProductWorkforcePerformanceRules
    {
        private static readonly KeyValuePair<WorkTaskProfile, int>[] Stages =
        {
            Stage("product.requirements", 2000, W(1000, 5000, 0, 2000, 500, 1500)),
            Stage("product.engineering", 3500, W(6500, 1800, 500, 0, 500, 700)),
            Stage("product.design", 2000, W(1000, 1800, 6000, 200, 0, 1000)),
            Stage("product.launch-sales", 1500, W(0, 1200, 1000, 5700, 500, 1600)),
            Stage("product.operations", 1000, W(1000, 1500, 0, 500, 5600, 1400))
        };

        public static int CalculateCompatibilityQuality(FamilyState family)
        {
            if (family == null) throw new ArgumentNullException(nameof(family));
            long total = 0;
            foreach (var stage in Stages)
            {
                var stageScore = family.Members
                    .Select(member => WorkforcePerformanceRules.CalculateQualityScore(member.Capability, stage.Key))
                    .OrderByDescending(score => score)
                    .Take(Math.Min(2, family.Members.Count))
                    .DefaultIfEmpty(0)
                    .Average();
                total += (long)Math.Round(stageScore) * stage.Value;
            }
            return checked((int)((total + 5_000) / 10_000));
        }

        private static KeyValuePair<WorkTaskProfile, int> Stage(string id, int stageWeight, WorkSkillWeights weights) =>
            new KeyValuePair<WorkTaskProfile, int>(new WorkTaskProfile(id, weights, weights, weights), stageWeight);

        private static WorkSkillWeights W(int engineering, int planning, int creative, int business, int operations, int collaboration) =>
            new WorkSkillWeights(engineering, planning, creative, business, operations, collaboration);
    }
}
