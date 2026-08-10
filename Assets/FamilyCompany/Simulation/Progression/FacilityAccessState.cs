using System;
using System.Collections.Generic;
using System.Linq;

namespace FamilyCompany.Simulation.Progression
{
    public enum CompanyFacility
    {
        Bank = 0,
        ResearchAndDevelopment = 1,
        Hiring = 2,
        CorporateStockAccount = 3,
        Acquisitions = 4
    }

    public enum FacilityMilestone
    {
        FatherAccompaniedBankVisit = 0,
        FirstContractCompleted = 1,
        BusinessRegistrationCompleted = 2,
        AdultHiringApprovalGranted = 3,
        FirstOwnedBusinessFounded = 4
    }

    public enum FacilityAccessRequirement
    {
        FatherAccompaniedBankVisit = 0,
        FirstContractCompleted = 1,
        BusinessRegistrationCompleted = 2,
        AdultHiringApprovalGranted = 3,
        FirstOwnedBusinessFounded = 4,
        AcquisitionReputation = 5,
        AcquisitionCapital = 6
    }

    public sealed class FacilityAccessDecision
    {
        public FacilityAccessDecision(
            CompanyFacility facility,
            IEnumerable<FacilityAccessRequirement> missingRequirements)
        {
            if (!Enum.IsDefined(typeof(CompanyFacility), facility))
                throw new ArgumentOutOfRangeException(nameof(facility));
            Facility = facility;
            MissingRequirements = missingRequirements == null
                ? Array.Empty<FacilityAccessRequirement>()
                : missingRequirements.Distinct().OrderBy(item => (int)item).ToArray();
        }

        public CompanyFacility Facility { get; }
        public IReadOnlyList<FacilityAccessRequirement> MissingRequirements { get; }
        public bool IsUnlocked => MissingRequirements.Count == 0;
    }

    public sealed class FacilityAccessState
    {
        public const int AcquisitionRequiredReputation = 30;
        public const long AcquisitionRequiredCapitalWon = 20_000_000;

        public FacilityAccessState(
            bool facilityStoryGatesEnabled = true,
            bool fatherAccompaniedBankVisit = false,
            bool firstContractCompleted = false,
            bool businessRegistrationCompleted = false,
            bool adultHiringApprovalGranted = false,
            bool firstOwnedBusinessFounded = false,
            int companyReputation = 0,
            long eligibleCapitalWon = 0)
        {
            if (companyReputation < 0 || companyReputation > 100)
                throw new ArgumentOutOfRangeException(nameof(companyReputation));
            if (eligibleCapitalWon < 0) throw new ArgumentOutOfRangeException(nameof(eligibleCapitalWon));

            FacilityStoryGatesEnabled = facilityStoryGatesEnabled;
            FatherAccompaniedBankVisit = fatherAccompaniedBankVisit;
            FirstContractCompleted = firstContractCompleted;
            BusinessRegistrationCompleted = businessRegistrationCompleted;
            AdultHiringApprovalGranted = adultHiringApprovalGranted;
            FirstOwnedBusinessFounded = firstOwnedBusinessFounded;
            CompanyReputation = companyReputation;
            EligibleCapitalWon = eligibleCapitalWon;
        }

        public bool FacilityStoryGatesEnabled { get; private set; }
        public bool FatherAccompaniedBankVisit { get; private set; }
        public bool FirstContractCompleted { get; private set; }
        public bool BusinessRegistrationCompleted { get; private set; }
        public bool AdultHiringApprovalGranted { get; private set; }
        public bool FirstOwnedBusinessFounded { get; private set; }
        public int CompanyReputation { get; private set; }
        public long EligibleCapitalWon { get; private set; }

        public void SetFacilityStoryGatesEnabled(bool enabled)
        {
            FacilityStoryGatesEnabled = enabled;
        }

        public bool RecordMilestone(FacilityMilestone milestone)
        {
            if (!Enum.IsDefined(typeof(FacilityMilestone), milestone))
                throw new ArgumentOutOfRangeException(nameof(milestone));

            switch (milestone)
            {
                case FacilityMilestone.FatherAccompaniedBankVisit:
                    if (FatherAccompaniedBankVisit) return false;
                    FatherAccompaniedBankVisit = true;
                    return true;
                case FacilityMilestone.FirstContractCompleted:
                    if (FirstContractCompleted) return false;
                    FirstContractCompleted = true;
                    return true;
                case FacilityMilestone.BusinessRegistrationCompleted:
                    if (BusinessRegistrationCompleted) return false;
                    BusinessRegistrationCompleted = true;
                    return true;
                case FacilityMilestone.AdultHiringApprovalGranted:
                    if (AdultHiringApprovalGranted) return false;
                    AdultHiringApprovalGranted = true;
                    return true;
                case FacilityMilestone.FirstOwnedBusinessFounded:
                    if (FirstOwnedBusinessFounded) return false;
                    FirstOwnedBusinessFounded = true;
                    return true;
                default:
                    throw new ArgumentOutOfRangeException(nameof(milestone));
            }
        }

        public void UpdateAcquisitionQualification(int companyReputation, long eligibleCapitalWon)
        {
            if (companyReputation < 0 || companyReputation > 100)
                throw new ArgumentOutOfRangeException(nameof(companyReputation));
            if (eligibleCapitalWon < 0) throw new ArgumentOutOfRangeException(nameof(eligibleCapitalWon));
            CompanyReputation = companyReputation;
            EligibleCapitalWon = eligibleCapitalWon;
        }

        public bool IsUnlocked(CompanyFacility facility)
        {
            return Evaluate(facility).IsUnlocked;
        }

        public FacilityAccessDecision Evaluate(CompanyFacility facility)
        {
            if (!Enum.IsDefined(typeof(CompanyFacility), facility))
                throw new ArgumentOutOfRangeException(nameof(facility));
            if (!FacilityStoryGatesEnabled)
                return new FacilityAccessDecision(facility, Array.Empty<FacilityAccessRequirement>());

            var missing = new List<FacilityAccessRequirement>();
            switch (facility)
            {
                case CompanyFacility.Bank:
                    AddIfMissing(
                        missing,
                        FatherAccompaniedBankVisit,
                        FacilityAccessRequirement.FatherAccompaniedBankVisit);
                    break;
                case CompanyFacility.ResearchAndDevelopment:
                    AddIfMissing(
                        missing,
                        FirstContractCompleted,
                        FacilityAccessRequirement.FirstContractCompleted);
                    break;
                case CompanyFacility.Hiring:
                    AddIfMissing(
                        missing,
                        BusinessRegistrationCompleted,
                        FacilityAccessRequirement.BusinessRegistrationCompleted);
                    AddIfMissing(
                        missing,
                        AdultHiringApprovalGranted,
                        FacilityAccessRequirement.AdultHiringApprovalGranted);
                    break;
                case CompanyFacility.CorporateStockAccount:
                    AddIfMissing(
                        missing,
                        FirstOwnedBusinessFounded,
                        FacilityAccessRequirement.FirstOwnedBusinessFounded);
                    break;
                case CompanyFacility.Acquisitions:
                    AddIfMissing(
                        missing,
                        CompanyReputation >= AcquisitionRequiredReputation,
                        FacilityAccessRequirement.AcquisitionReputation);
                    AddIfMissing(
                        missing,
                        EligibleCapitalWon >= AcquisitionRequiredCapitalWon,
                        FacilityAccessRequirement.AcquisitionCapital);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(facility));
            }

            return new FacilityAccessDecision(facility, missing);
        }

        public IReadOnlyList<CompanyFacility> UnlockedFacilities()
        {
            return Enum.GetValues(typeof(CompanyFacility))
                .Cast<CompanyFacility>()
                .Where(IsUnlocked)
                .ToArray();
        }

        private static void AddIfMissing(
            ICollection<FacilityAccessRequirement> missing,
            bool condition,
            FacilityAccessRequirement requirement)
        {
            if (!condition) missing.Add(requirement);
        }
    }
}
