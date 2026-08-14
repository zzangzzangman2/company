using System;
using FamilyCompany.Simulation.Workforce;

namespace FamilyCompany.Simulation.ContractGrowth
{
    public static class ContractWorkTaskProfiles
    {
        public static WorkTaskProfile Resolve(ContractSpecialty specialty)
        {
            switch (specialty)
            {
                case ContractSpecialty.WebContent:
                    return Profile("contract.web-content", W(5500, 1500, 2000, 0, 0, 1000), W(4000, 1800, 3000, 0, 0, 1200));
                case ContractSpecialty.DataQualityAssurance:
                    return Profile("contract.data-qa", W(1500, 3500, 0, 0, 4000, 1000), W(1000, 4000, 0, 0, 3500, 1500));
                case ContractSpecialty.OfficeNetwork:
                    return Profile("contract.office-network", W(4200, 1500, 0, 0, 3500, 800), W(3300, 2200, 0, 0, 3300, 1200));
                case ContractSpecialty.BusinessSoftware:
                    return Profile("contract.business-software", W(5000, 3000, 0, 500, 500, 1000), W(3800, 3500, 0, 500, 700, 1500));
                case ContractSpecialty.Localization:
                    return Profile("contract.localization", W(0, 2800, 3500, 500, 1400, 1800), W(0, 2700, 3300, 500, 1000, 2500));
                case ContractSpecialty.MobileContent:
                    return Profile("contract.mobile-content", W(4500, 1200, 3000, 300, 0, 1000), W(3500, 1600, 3500, 300, 0, 1100));
                case ContractSpecialty.HardwareOperations:
                    return Profile("contract.hardware-operations", W(2500, 1600, 0, 0, 5000, 900), W(2000, 2200, 0, 0, 4400, 1400));
                case ContractSpecialty.RetailOperations:
                    return Profile("contract.retail-operations", W(0, 1000, 500, 5000, 2200, 1300), W(0, 1500, 800, 4000, 1800, 1900));
                default: throw new ArgumentOutOfRangeException(nameof(specialty));
            }
        }

        private static WorkTaskProfile Profile(string id, WorkSkillWeights progress, WorkSkillWeights quality) =>
            new WorkTaskProfile(id, progress, quality, progress);

        private static WorkSkillWeights W(int engineering, int planning, int creative, int business, int operations, int collaboration) =>
            new WorkSkillWeights(engineering, planning, creative, business, operations, collaboration);
    }
}
