using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Simulation.Company;
using FamilyCompany.Simulation.Contracts;

namespace FamilyCompany.Simulation.ContractGrowth
{
    /// <summary>
    /// Adds stable metadata to the original 21 bootstrap templates without copying or replacing them.
    /// </summary>
    public static class LegacyContractTemplateCatalog
    {
        public const string TemplateIdPrefix = "legacy-contract-template-v1:";
        private static readonly LegacyContractTemplateDefinition[] Definitions = BuildDefinitions();

        public static IReadOnlyList<LegacyContractTemplateDefinition> All => Definitions;

        public static LegacyContractTemplateDefinition Get(int legacyGlobalIndex)
        {
            if (legacyGlobalIndex < 0 || legacyGlobalIndex >= Definitions.Length)
                throw new ArgumentOutOfRangeException(nameof(legacyGlobalIndex));
            return Definitions[legacyGlobalIndex];
        }

        public static IReadOnlyList<LegacyContractTemplateDefinition> ForIndustry(BusinessIndustry industry)
        {
            return Definitions.Where(item => item.Baseline.Industry == industry).ToArray();
        }

        public static bool TryResolve(SubcontractOffer offer, out LegacyContractTemplateDefinition definition)
        {
            definition = null;
            if (offer == null) return false;
            definition = Definitions.FirstOrDefault(item =>
                item.Baseline.Industry == offer.Industry &&
                item.Baseline.ServiceType == offer.ServiceType &&
                string.Equals(item.Baseline.Title, offer.Title, StringComparison.Ordinal));
            return definition != null;
        }

        private static LegacyContractTemplateDefinition[] BuildDefinitions()
        {
            var count = BootstrapContractCatalog.TotalOfferTemplateCount;
            var result = new LegacyContractTemplateDefinition[count];
            for (var index = 0; index < count; index++)
            {
                var baseline = BootstrapContractCatalog.CreateOffer(
                    0,
                    "metadata_probe",
                    "메타데이터 기준",
                    index);
                result[index] = new LegacyContractTemplateDefinition(
                    $"{TemplateIdPrefix}{index:D2}",
                    index,
                    baseline,
                    ResolveSpecialty(baseline),
                    ResolveDifficulty(baseline));
            }
            return result;
        }

        public static ContractSpecialty ResolveSpecialty(SubcontractOffer offer)
        {
            if (offer == null) throw new ArgumentNullException(nameof(offer));
            switch (offer.Industry)
            {
                case BusinessIndustry.FeaturePhoneAndMobile:
                    return offer.ServiceType == ContractServiceType.LocalizationAndWebQualityAssurance
                        ? ContractSpecialty.Localization
                        : ContractSpecialty.MobileContent;
                case BusinessIndustry.HardwareAndPc:
                    return offer.ServiceType == ContractServiceType.OfficePcAndNetworkSupport
                        ? ContractSpecialty.OfficeNetwork
                        : ContractSpecialty.HardwareOperations;
                case BusinessIndustry.FashionRetailAndOffline:
                    return offer.ServiceType == ContractServiceType.SmallBusinessTool
                        ? ContractSpecialty.BusinessSoftware
                        : ContractSpecialty.RetailOperations;
                default:
                    switch (offer.ServiceType)
                    {
                        case ContractServiceType.DataEntryAndQualityAssurance:
                            return ContractSpecialty.DataQualityAssurance;
                        case ContractServiceType.OfficePcAndNetworkSupport:
                            return ContractSpecialty.OfficeNetwork;
                        case ContractServiceType.SmallBusinessTool:
                            return ContractSpecialty.BusinessSoftware;
                        case ContractServiceType.LocalizationAndWebQualityAssurance:
                            return ContractSpecialty.Localization;
                        default:
                            return ContractSpecialty.WebContent;
                    }
            }
        }

        private static ContractDifficulty ResolveDifficulty(SubcontractOffer offer)
        {
            var score = offer.EstimatedPersonHours + offer.RequiredDevelopment + offer.RequiredSpeed;
            if (!string.IsNullOrEmpty(offer.RequiredTechnologyId) || score >= 150) return ContractDifficulty.Enterprise;
            if (score >= 120) return ContractDifficulty.Professional;
            if (score >= 90) return ContractDifficulty.Skilled;
            if (score >= 65) return ContractDifficulty.Routine;
            return ContractDifficulty.Starter;
        }
    }
}
