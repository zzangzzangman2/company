using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Simulation.Company;
using FamilyCompany.Simulation.Contracts;
using FamilyCompany.Simulation.Core;
using FamilyCompany.Simulation.Technology;

namespace FamilyCompany.Simulation.ContractGrowth
{
    public static class ContractOfferBoardRules
    {
        public const int NormalOfferCount = 6;
        private static readonly int[] OnboardingTemplateIndices = { 0, 2, 18 };
        private static readonly string[] OnboardingClientIds =
        {
            "local_sinchon_photo_studio",
            "local_jongno_typing_academy",
            "local_mapo_video_rental"
        };

        public static ContractOfferBoardSnapshot Generate(
            int worldSeed,
            long elapsedMinute,
            BusinessIndustry industry,
            bool anyContractAccepted,
            ContractPerformanceSummary summary,
            ContractCompanyProfile profile,
            ContractClientTierCatalog clients)
        {
            if (elapsedMinute < 0) throw new ArgumentOutOfRangeException(nameof(elapsedMinute));
            if (summary == null) throw new ArgumentNullException(nameof(summary));
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (clients == null) throw new ArgumentNullException(nameof(clients));
            var dayIndex = (long)(GameTime.CampaignStart.AddMinutes(elapsedMinute).Date - GameTime.CampaignStart.Date).TotalDays;
            var progress = ContractProgressionRules.EvaluateAll(summary, profile, industry);
            var offers = anyContractAccepted
                ? GenerateNormal(worldSeed, dayIndex, industry, progress, clients)
                : GenerateOnboarding(worldSeed, clients);
            return new ContractOfferBoardSnapshot(dayIndex, industry, !anyContractAccepted, offers, progress);
        }

        private static ContractOfferDefinition[] GenerateOnboarding(
            int worldSeed,
            ContractClientTierCatalog clients)
        {
            var offers = new ContractOfferDefinition[OnboardingTemplateIndices.Length];
            for (var slot = 0; slot < offers.Length; slot++)
            {
                var template = LegacyContractTemplateCatalog.Get(OnboardingTemplateIndices[slot]);
                var client = clients.Get(OnboardingClientIds[slot]);
                offers[slot] = ContractRewardBalanceRules.Build(
                    worldSeed,
                    0,
                    slot,
                    template,
                    client,
                    true);
            }
            return offers;
        }

        private static ContractOfferDefinition[] GenerateNormal(
            int worldSeed,
            long dayIndex,
            BusinessIndustry industry,
            IReadOnlyList<ContractTierProgress> progress,
            ContractClientTierCatalog clients)
        {
            var unlocked = progress.Where(item => item.Unlocked).Select(item => item.Tier).OrderBy(item => item).ToArray();
            var highest = unlocked.Max();
            var templates = LegacyContractTemplateCatalog.ForIndustry(industry);
            var result = new List<ContractOfferDefinition>(NormalOfferCount);
            for (var slot = 0; slot < NormalOfferCount; slot++)
            {
                var tier = slot == 0
                    ? ContractClientTier.T0LocalBusiness
                    : PickTier(worldSeed, dayIndex, industry, slot, highest, unlocked);
                var availableClients = clients.ForTierAndIndustry(tier, industry);
                while (availableClients.Count == 0 && tier > ContractClientTier.T0LocalBusiness)
                {
                    tier--;
                    availableClients = clients.ForTierAndIndustry(tier, industry);
                }
                if (availableClients.Count == 0)
                    throw new InvalidOperationException($"No contract client covers {industry} at or below {tier}.");
                var clientIndex = StableIndex(worldSeed, dayIndex, industry, slot, "client", availableClients.Count);
                var templateIndex = StableIndex(worldSeed, dayIndex, industry, slot, "template", templates.Count);
                result.Add(ContractRewardBalanceRules.Build(
                    worldSeed,
                    dayIndex,
                    slot,
                    templates[templateIndex],
                    availableClients[clientIndex],
                    false));
            }
            return result.ToArray();
        }

        private static ContractClientTier PickTier(
            int worldSeed,
            long dayIndex,
            BusinessIndustry industry,
            int slot,
            ContractClientTier highest,
            IReadOnlyCollection<ContractClientTier> unlocked)
        {
            var roll = StableIndex(worldSeed, dayIndex, industry, slot, "tier", 100);
            ContractClientTier candidate;
            if (highest == ContractClientTier.T0LocalBusiness) candidate = highest;
            else if (roll < 45) candidate = highest;
            else if (roll < 75) candidate = highest - 1;
            else if (roll < 92) candidate = ContractClientTier.T0LocalBusiness;
            else candidate = highest >= ContractClientTier.T2GrowthCompany ? highest - 2 : ContractClientTier.T0LocalBusiness;
            while (!unlocked.Contains(candidate) && candidate > ContractClientTier.T0LocalBusiness) candidate--;
            return candidate;
        }

        internal static int StableIndex(
            int worldSeed,
            long dayIndex,
            BusinessIndustry industry,
            int slot,
            string purpose,
            int count)
        {
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
            unchecked
            {
                uint hash = 2166136261;
                var text = $"{worldSeed}|{dayIndex}|{(int)industry}|{slot}|{purpose}";
                foreach (var character in text)
                {
                    hash ^= character;
                    hash *= 16777619;
                }
                return (int)(hash % (uint)count);
            }
        }
    }

    public static class ContractRewardBalanceRules
    {
        private static readonly long[] MinimumRewardWon = { 280_000, 450_000, 700_000, 1_000_000, 1_300_000 };
        private static readonly long[] MaximumRewardWon = { 1_200_000, 1_500_000, 1_800_000, 2_200_000, 2_500_000 };

        public static ContractOfferDefinition Build(
            int worldSeed,
            long dayIndex,
            int slot,
            LegacyContractTemplateDefinition template,
            ContractClientDefinition client,
            bool onboarding)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));
            if (client == null) throw new ArgumentNullException(nameof(client));
            var tier = (int)client.Tier;
            var baseline = template.Baseline;
            var variation = ContractOfferBoardRules.StableIndex(
                worldSeed, dayIndex, baseline.Industry, slot, $"balance:{client.ClientId}:{template.TemplateId}", 7) - 3;
            var hours = Math.Min(80, Math.Max(8, baseline.EstimatedPersonHours + tier * 6 + variation * 2));
            var deadlineDays = Math.Max(3, baseline.DeadlineDays + tier - Math.Max(0, variation) / 2);
            var workers = Math.Min(4, Math.Max(1, baseline.RequiredWorkers + (tier >= 3 ? 1 : 0)));
            var qualityStandard = Math.Min(92, 52 + tier * 8 + Math.Max(0, variation));
            var development = Math.Min(88, Math.Max(baseline.RequiredDevelopment, 18 + tier * 12));
            var requiredCapability = Math.Min(88, Math.Max(baseline.RequiredDevelopment, 18 + tier * 12));
            var reward = CalculateReward(baseline, client.Tier, hours, qualityStandard, deadlineDays);
            if (onboarding)
            {
                hours = baseline.EstimatedPersonHours;
                deadlineDays = baseline.DeadlineDays;
                workers = baseline.RequiredWorkers;
                development = baseline.RequiredDevelopment;
                requiredCapability = baseline.RequiredDevelopment;
                reward = baseline.RewardWon;
                qualityStandard = slot == 0 ? 52 : slot == 1 ? 58 : 62;
            }
            var upfront = onboarding ? baseline.UpfrontCostWon : Math.Min(reward / 8, baseline.UpfrontCostWon + tier * 40_000L);
            var penalty = onboarding ? baseline.PenaltyWon : tier < 2 ? baseline.PenaltyWon : Math.Min(reward / 3, baseline.PenaltyWon + tier * 90_000L);
            var reputationRequired = onboarding ? 0 : Math.Min(100, tier == 0 ? 0 : ContractProgressionRules.TierRequirements[tier].Reputation);
            var offerId = onboarding
                ? $"contract-onboarding-v1:{worldSeed}:{slot:D2}:{client.ClientId}:{template.TemplateId}"
                : $"contract-offer-v1:{worldSeed}:{dayIndex:D8}:{(int)baseline.Industry}:{slot:D2}:{client.ClientId}:{template.TemplateId}";
            var offer = new SubcontractOffer(
                offerId,
                client.ClientId,
                client.DisplayNameKo,
                baseline.ServiceType,
                baseline.Title,
                workers,
                hours,
                deadlineDays,
                upfront,
                reward,
                reputationRequired,
                penalty,
                development,
                0,
                tier >= 3 ? baseline.RequiredTechnologyId : string.Empty,
                baseline.Industry,
                requiredCapability);
            var risk = onboarding
                ? slot == 0 ? ContractRiskLevel.Low : ContractRiskLevel.Moderate
                : ResolveRisk(hours, deadlineDays, qualityStandard, tier);
            return new ContractOfferDefinition(
                offer,
                template,
                client,
                (ContractDifficulty)Math.Min((int)ContractDifficulty.Enterprise, Math.Max((int)template.BaselineDifficulty, tier)),
                template.Specialty,
                qualityStandard,
                2,
                2,
                risk,
                BuildPrerequisiteLabels(client.Tier, qualityStandard, requiredCapability),
                onboarding,
                // The client's experience bar travels with the job, not with the tier, so the same
                // work always asks for the same proven technology wherever it shows up.
                ContractTechnologyRequirementCatalog.ForTemplateIndex(template.LegacyGlobalIndex));
        }

        private static long CalculateReward(
            SubcontractOffer baseline,
            ContractClientTier tier,
            int hours,
            int qualityStandard,
            int deadlineDays)
        {
            var tierIndex = (int)tier;
            var hourlyRate = 24_000L + tierIndex * 7_000L;
            var labor = hours * hourlyRate;
            var skillRisk = baseline.RequiredCapability * 4_400L;
            var qualityRisk = qualityStandard * 1_500L;
            var deadlineRisk = Math.Max(0, hours * 4 - deadlineDays * 10) * 2_000L;
            var legacyAnchor = baseline.RewardWon / 4;
            var raw = labor + skillRisk + qualityRisk + deadlineRisk + legacyAnchor + tierIndex * 120_000L;
            var rounded = ((raw + 5_000L) / 10_000L) * 10_000L;
            return Math.Max(MinimumRewardWon[tierIndex], Math.Min(MaximumRewardWon[tierIndex], rounded));
        }

        private static ContractRiskLevel ResolveRisk(int hours, int deadlineDays, int quality, int tier)
        {
            var pressure = hours * 3 / Math.Max(1, deadlineDays) + quality / 5 + tier * 5;
            if (pressure >= 48) return ContractRiskLevel.Critical;
            if (pressure >= 36) return ContractRiskLevel.High;
            if (pressure >= 25) return ContractRiskLevel.Moderate;
            return ContractRiskLevel.Low;
        }

        private static IEnumerable<string> BuildPrerequisiteLabels(
            ContractClientTier tier,
            int quality,
            int requiredCapability)
        {
            yield return $"고객 단계 {TierLabel(tier)}";
            yield return $"품질 기준 {quality}";
            yield return $"업무 적합도 {requiredCapability}";
        }

        public static string TierLabel(ContractClientTier tier)
        {
            switch (tier)
            {
                case ContractClientTier.T0LocalBusiness: return "T0 동네 사업자";
                case ContractClientTier.T1RegionalSmallBusiness: return "T1 지역 소기업";
                case ContractClientTier.T2GrowthCompany: return "T2 성장 기업";
                case ContractClientTier.T3PrimeVendor: return "T3 전문 발주사";
                case ContractClientTier.T4NationalEnterprise: return "T4 전국 대기업";
                default: return tier.ToString();
            }
        }
    }
}
