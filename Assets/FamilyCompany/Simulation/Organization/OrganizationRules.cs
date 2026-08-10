using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using FamilyCompany.Simulation.Company;
using FamilyCompany.Simulation.Core;

namespace FamilyCompany.Simulation.Organization
{
    public enum EmployeeGrade
    {
        F = 0,
        D = 1,
        C = 2,
        B = 3,
        A = 4,
        S = 5
    }

    public sealed class EmployeeGradeDefinition
    {
        public EmployeeGradeDefinition(
            EmployeeGrade grade,
            string label,
            long baseMonthlySalaryWon,
            long baseSigningBonusWon,
            int minimumPotential)
        {
            if (!Enum.IsDefined(typeof(EmployeeGrade), grade)) throw new ArgumentOutOfRangeException(nameof(grade));
            if (string.IsNullOrWhiteSpace(label)) throw new ArgumentException("A grade label is required.", nameof(label));
            if (baseMonthlySalaryWon <= 0) throw new ArgumentOutOfRangeException(nameof(baseMonthlySalaryWon));
            if (baseSigningBonusWon < 0) throw new ArgumentOutOfRangeException(nameof(baseSigningBonusWon));
            if (minimumPotential < 0 || minimumPotential > 100) throw new ArgumentOutOfRangeException(nameof(minimumPotential));

            Grade = grade;
            Label = label;
            BaseMonthlySalaryWon = baseMonthlySalaryWon;
            BaseSigningBonusWon = baseSigningBonusWon;
            MinimumPotential = minimumPotential;
        }

        public EmployeeGrade Grade { get; }
        public string Label { get; }
        public long BaseMonthlySalaryWon { get; }
        public long BaseSigningBonusWon { get; }
        public int MinimumPotential { get; }
    }

    public static class EmployeeGradeRules
    {
        public const int GradeRollUpperBound = 1_000;

        private static readonly ReadOnlyCollection<EmployeeGradeDefinition> Definitions =
            Array.AsReadOnly(new[]
            {
                new EmployeeGradeDefinition(EmployeeGrade.S, "S", 2_200_000, 4_400_000, 92),
                new EmployeeGradeDefinition(EmployeeGrade.A, "A", 1_700_000, 2_550_000, 80),
                new EmployeeGradeDefinition(EmployeeGrade.B, "B", 1_300_000, 1_300_000, 68),
                new EmployeeGradeDefinition(EmployeeGrade.C, "C", 1_000_000, 750_000, 55),
                new EmployeeGradeDefinition(EmployeeGrade.D, "D", 800_000, 400_000, 45),
                new EmployeeGradeDefinition(EmployeeGrade.F, "F", 650_000, 250_000, 35)
            });

        public static IReadOnlyList<EmployeeGradeDefinition> All => Definitions;

        public static EmployeeGradeDefinition Get(EmployeeGrade grade)
        {
            if (!Enum.IsDefined(typeof(EmployeeGrade), grade)) throw new ArgumentOutOfRangeException(nameof(grade));
            for (var index = 0; index < Definitions.Count; index++)
            {
                if (Definitions[index].Grade == grade) return Definitions[index];
            }

            throw new InvalidOperationException("The employee grade catalog is incomplete.");
        }

        public static EmployeeGrade FromRoll(int roll)
        {
            if (roll < 0 || roll >= GradeRollUpperBound) throw new ArgumentOutOfRangeException(nameof(roll));
            if (roll < 60) return EmployeeGrade.S;
            if (roll < 210) return EmployeeGrade.A;
            if (roll < 440) return EmployeeGrade.B;
            if (roll < 740) return EmployeeGrade.C;
            if (roll < 920) return EmployeeGrade.D;
            return EmployeeGrade.F;
        }

        public static EmployeeGrade FromPotential(int potential)
        {
            if (potential < 0 || potential > 100) throw new ArgumentOutOfRangeException(nameof(potential));
            if (potential >= 92) return EmployeeGrade.S;
            if (potential >= 80) return EmployeeGrade.A;
            if (potential >= 68) return EmployeeGrade.B;
            if (potential >= 55) return EmployeeGrade.C;
            if (potential >= 45) return EmployeeGrade.D;
            return EmployeeGrade.F;
        }
    }

    public static class IndustryRecruitmentRoles
    {
        public static IReadOnlyList<string> GetForIndustry(BusinessIndustry industry)
        {
            return BusinessIndustryCatalog.Get(industry).RecruitableRoles;
        }

        public static bool IsRecruitable(BusinessIndustry industry, string roleId)
        {
            if (!Enum.IsDefined(typeof(BusinessIndustry), industry) || string.IsNullOrWhiteSpace(roleId)) return false;
            var normalizedRoleId = roleId.Trim();
            var roles = BusinessIndustryCatalog.Get(industry).RecruitableRoles;
            for (var index = 0; index < roles.Count; index++)
            {
                if (string.Equals(roles[index], normalizedRoleId, StringComparison.Ordinal)) return true;
            }

            return false;
        }
    }

    public sealed class RecruitmentOffer
    {
        internal RecruitmentOffer(
            string offerKey,
            string candidateId,
            BusinessIndustry industry,
            string roleId,
            long offerMinute,
            int offerSequence,
            EmployeeGrade grade,
            EmployeeGrade potentialGrade,
            int morale,
            int loyalty,
            int potential,
            long monthlySalaryWon,
            long standardSigningBonusWon,
            long talentNetworkDiscountWon,
            bool talentNetworkApplied)
        {
            OfferKey = offerKey;
            CandidateId = candidateId;
            Industry = industry;
            RoleId = roleId;
            OfferMinute = offerMinute;
            OfferSequence = offerSequence;
            Grade = grade;
            PotentialGrade = potentialGrade;
            Morale = morale;
            Loyalty = loyalty;
            Potential = potential;
            MonthlySalaryWon = monthlySalaryWon;
            StandardSigningBonusWon = standardSigningBonusWon;
            TalentNetworkDiscountWon = talentNetworkDiscountWon;
            SigningBonusWon = checked(standardSigningBonusWon - talentNetworkDiscountWon);
            TalentNetworkApplied = talentNetworkApplied;
        }

        public string OfferKey { get; }
        public string CandidateId { get; }
        public BusinessIndustry Industry { get; }
        public string RoleId { get; }
        public long OfferMinute { get; }
        public int OfferSequence { get; }
        public EmployeeGrade Grade { get; }
        public EmployeeGrade PotentialGrade { get; }
        public int Morale { get; }
        public int Loyalty { get; }
        public int Potential { get; }
        public long MonthlySalaryWon { get; }
        public long StandardSigningBonusWon { get; }
        public long TalentNetworkDiscountWon { get; }
        public long SigningBonusWon { get; }
        public bool TalentNetworkApplied { get; }
    }

    public static class RecruitmentOfferRules
    {
        public const string TalentNetworkSkillId = "talent_network";
        public const int BasisPointDenominator = 10_000;
        public const int TalentNetworkDiscountBasisPoints = 1_000;
        public const long SalaryRoundingUnitWon = 10_000;
        public const long SigningBonusRoundingUnitWon = 10_000;

        public static RecruitmentOffer Create(
            int worldSeed,
            string candidateId,
            BusinessIndustry industry,
            string roleId,
            long offerMinute,
            int offerSequence,
            bool hasTalentNetwork)
        {
            var normalizedCandidateId = NormalizeRequired(candidateId, nameof(candidateId));
            var normalizedRoleId = NormalizeRequired(roleId, nameof(roleId));
            if (!Enum.IsDefined(typeof(BusinessIndustry), industry)) throw new ArgumentOutOfRangeException(nameof(industry));
            if (!IndustryRecruitmentRoles.IsRecruitable(industry, normalizedRoleId))
            {
                throw new ArgumentException(
                    $"Role '{normalizedRoleId}' is not recruitable for industry '{industry}'.",
                    nameof(roleId));
            }

            if (offerMinute < 0) throw new ArgumentOutOfRangeException(nameof(offerMinute));
            if (offerSequence < 0) throw new ArgumentOutOfRangeException(nameof(offerSequence));

            var offerKey = BuildOfferKey(
                worldSeed,
                normalizedCandidateId,
                industry,
                normalizedRoleId,
                offerMinute,
                offerSequence);
            var grade = EmployeeGradeRules.FromRoll(
                StableRandom.StableRandomInt(offerKey + ":grade", EmployeeGradeRules.GradeRollUpperBound));
            var gradeDefinition = EmployeeGradeRules.Get(grade);
            var morale = 45 + StableRandom.StableRandomInt(offerKey + ":morale", 41);
            var loyalty = 35 + StableRandom.StableRandomInt(offerKey + ":loyalty", 51);
            var potential = gradeDefinition.MinimumPotential + StableRandom.StableRandomInt(
                offerKey + ":potential",
                101 - gradeDefinition.MinimumPotential);
            var potentialGrade = MaxGrade(grade, EmployeeGradeRules.FromPotential(potential));

            var salaryDemandBasisPoints = checked(9_000 + morale * 5 + potential * 7 - loyalty * 3);
            var signingDemandBasisPoints = checked(8_500 + morale * 4 + potential * 8 - loyalty * 2);
            var monthlySalaryWon = ApplyBasisPointsRounded(
                gradeDefinition.BaseMonthlySalaryWon,
                salaryDemandBasisPoints,
                SalaryRoundingUnitWon);
            var standardSigningBonusWon = ApplyBasisPointsRounded(
                gradeDefinition.BaseSigningBonusWon,
                signingDemandBasisPoints,
                SigningBonusRoundingUnitWon);
            var talentNetworkDiscountWon = hasTalentNetwork
                ? ApplyBasisPointsFloor(standardSigningBonusWon, TalentNetworkDiscountBasisPoints)
                : 0;

            return new RecruitmentOffer(
                offerKey,
                normalizedCandidateId,
                industry,
                normalizedRoleId,
                offerMinute,
                offerSequence,
                grade,
                potentialGrade,
                morale,
                loyalty,
                potential,
                monthlySalaryWon,
                standardSigningBonusWon,
                talentNetworkDiscountWon,
                hasTalentNetwork);
        }

        private static string BuildOfferKey(
            int worldSeed,
            string candidateId,
            BusinessIndustry industry,
            string roleId,
            long offerMinute,
            int offerSequence)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "organization-offer-v1:{0}:{1}:{2}:{3}:{4}:{5}:{6}:{7}",
                worldSeed,
                candidateId.Length,
                candidateId,
                (int)industry,
                roleId.Length,
                roleId,
                offerMinute,
                offerSequence);
        }

        private static string NormalizeRequired(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A non-empty value is required.", parameterName);
            return value.Trim();
        }

        private static EmployeeGrade MaxGrade(EmployeeGrade left, EmployeeGrade right)
        {
            return (int)left >= (int)right ? left : right;
        }

        private static long ApplyBasisPointsRounded(long amountWon, int rateBasisPoints, long roundingUnitWon)
        {
            if (amountWon < 0) throw new ArgumentOutOfRangeException(nameof(amountWon));
            if (rateBasisPoints < 0) throw new ArgumentOutOfRangeException(nameof(rateBasisPoints));
            if (roundingUnitWon <= 0) throw new ArgumentOutOfRangeException(nameof(roundingUnitWon));
            if (amountWon == 0 || rateBasisPoints == 0) return 0;

            var denominator = checked((long)BasisPointDenominator * roundingUnitWon);
            var numerator = checked(amountWon * rateBasisPoints);
            var roundedUnits = checked((numerator + denominator / 2) / denominator);
            return checked(roundedUnits * roundingUnitWon);
        }

        private static long ApplyBasisPointsFloor(long amountWon, int rateBasisPoints)
        {
            if (amountWon < 0) throw new ArgumentOutOfRangeException(nameof(amountWon));
            if (rateBasisPoints < 0) throw new ArgumentOutOfRangeException(nameof(rateBasisPoints));
            return checked(amountWon * rateBasisPoints / BasisPointDenominator);
        }
    }
}
