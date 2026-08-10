using System;
using System.Collections.Generic;
using FamilyCompany.Simulation.Company;
using FamilyCompany.Simulation.Organization;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public static class OrganizationRulesValidation
    {
        [MenuItem("Family Company/Validate Organization Rules")]
        public static void Run()
        {
            try
            {
                ValidateGradeCatalog();
                ValidateIndustryRoles();
                ValidateDeterministicOffer();
                ValidateTalentNetworkDiscount();
                ValidateCandidateIdBoundary();
                ValidateInputRejections();
                Debug.Log("FAMILY_COMPANY_ORGANIZATION_RULES_VALIDATION: PASS");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("FAMILY_COMPANY_ORGANIZATION_RULES_VALIDATION: FAIL");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        private static void ValidateGradeCatalog()
        {
            var expectedOrder = new[]
            {
                EmployeeGrade.S,
                EmployeeGrade.A,
                EmployeeGrade.B,
                EmployeeGrade.C,
                EmployeeGrade.D,
                EmployeeGrade.F
            };
            AssertEqual(expectedOrder.Length, EmployeeGradeRules.All.Count, "grade definition count");
            for (var index = 0; index < expectedOrder.Length; index++)
            {
                var definition = EmployeeGradeRules.All[index];
                AssertEqual(expectedOrder[index], definition.Grade, "grade catalog order " + index);
                AssertEqual(expectedOrder[index].ToString(), definition.Label, "grade catalog label " + index);
                AssertTrue(definition.BaseMonthlySalaryWon > 0, "grade salary is positive " + definition.Label);
                AssertTrue(definition.BaseSigningBonusWon >= 0, "grade signing bonus is non-negative " + definition.Label);
            }

            AssertEqual(EmployeeGrade.S, EmployeeGradeRules.FromRoll(0), "S lower roll boundary");
            AssertEqual(EmployeeGrade.S, EmployeeGradeRules.FromRoll(59), "S upper roll boundary");
            AssertEqual(EmployeeGrade.A, EmployeeGradeRules.FromRoll(60), "A lower roll boundary");
            AssertEqual(EmployeeGrade.B, EmployeeGradeRules.FromRoll(210), "B lower roll boundary");
            AssertEqual(EmployeeGrade.C, EmployeeGradeRules.FromRoll(440), "C lower roll boundary");
            AssertEqual(EmployeeGrade.D, EmployeeGradeRules.FromRoll(740), "D lower roll boundary");
            AssertEqual(EmployeeGrade.F, EmployeeGradeRules.FromRoll(920), "F lower roll boundary");
            AssertEqual(EmployeeGrade.F, EmployeeGradeRules.FromRoll(999), "F upper roll boundary");
            AssertEqual(EmployeeGrade.F, EmployeeGradeRules.FromPotential(44), "F potential boundary");
            AssertEqual(EmployeeGrade.D, EmployeeGradeRules.FromPotential(45), "D potential boundary");
            AssertEqual(EmployeeGrade.S, EmployeeGradeRules.FromPotential(100), "S potential boundary");
        }

        private static void ValidateIndustryRoles()
        {
            AssertRoles(
                BusinessIndustry.WebAndSoftware,
                new[] { "프로그래머", "웹 디자이너", "도트 아티스트", "서비스 기획자" });
            AssertRoles(
                BusinessIndustry.FeaturePhoneAndMobile,
                new[] { "모바일 프로그래머", "MIDI 작곡가", "도트 디자이너", "단말 QA" });
            AssertRoles(
                BusinessIndustry.HardwareAndPc,
                new[] { "전자 엔지니어", "생산직", "PC 정비사", "품질관리자" });
            AssertRoles(
                BusinessIndustry.FashionRetailAndOffline,
                new[] { "패션 디자이너", "생산관리자", "인쇄 디자이너", "마케터" });

            AssertTrue(
                !IndustryRecruitmentRoles.IsRecruitable(BusinessIndustry.WebAndSoftware, "모바일 프로그래머"),
                "cross-industry role is rejected");
        }

        private static void ValidateDeterministicOffer()
        {
            var first = CreateFixture(false);
            var second = CreateFixture(false);
            AssertOffersEqual(first, second, "same input is deterministic");
            AssertEqual("prepared_employee_01", first.CandidateId, "candidate ID remains opaque");
            AssertEqual(BusinessIndustry.WebAndSoftware, first.Industry, "offer industry");
            AssertEqual("프로그래머", first.RoleId, "offer role");
            AssertEqual(120L, first.OfferMinute, "offer minute");
            AssertEqual(0, first.OfferSequence, "offer sequence");
            AssertTrue(first.Morale >= 45 && first.Morale <= 85, "morale bounds");
            AssertTrue(first.Loyalty >= 35 && first.Loyalty <= 85, "loyalty bounds");
            AssertTrue(first.Potential >= 35 && first.Potential <= 100, "potential bounds");
            AssertTrue((int)first.PotentialGrade >= (int)first.Grade, "potential grade never regresses");
            AssertTrue(first.MonthlySalaryWon > 0, "monthly salary is positive integer won");
            AssertTrue(first.StandardSigningBonusWon >= 0, "signing bonus is non-negative integer won");
            AssertEqual(0L, first.MonthlySalaryWon % RecruitmentOfferRules.SalaryRoundingUnitWon, "salary rounding unit");
            AssertEqual(0L, first.StandardSigningBonusWon % RecruitmentOfferRules.SigningBonusRoundingUnitWon, "signing rounding unit");

            var nextSequence = RecruitmentOfferRules.Create(
                20_000_103,
                "prepared_employee_01",
                BusinessIndustry.WebAndSoftware,
                "프로그래머",
                120,
                1,
                false);
            AssertTrue(!string.Equals(first.OfferKey, nextSequence.OfferKey, StringComparison.Ordinal), "offer sequence changes key");
        }

        private static void ValidateTalentNetworkDiscount()
        {
            var standard = CreateFixture(false);
            var network = CreateFixture(true);
            AssertEqual("talent_network", RecruitmentOfferRules.TalentNetworkSkillId, "talent network skill ID");
            AssertEqual(1_000, RecruitmentOfferRules.TalentNetworkDiscountBasisPoints, "talent network ten percent");
            AssertEqual(standard.OfferKey, network.OfferKey, "skill does not reroll offer key");
            AssertEqual(standard.Grade, network.Grade, "skill does not reroll grade");
            AssertEqual(standard.PotentialGrade, network.PotentialGrade, "skill does not reroll potential grade");
            AssertEqual(standard.Morale, network.Morale, "skill does not reroll morale");
            AssertEqual(standard.Loyalty, network.Loyalty, "skill does not reroll loyalty");
            AssertEqual(standard.Potential, network.Potential, "skill does not reroll potential");
            AssertEqual(standard.MonthlySalaryWon, network.MonthlySalaryWon, "skill does not alter salary");
            AssertEqual(standard.StandardSigningBonusWon, network.StandardSigningBonusWon, "standard signing bonus retained");

            var expectedDiscount = standard.StandardSigningBonusWon / 10;
            AssertEqual(expectedDiscount, network.TalentNetworkDiscountWon, "ten-percent signing discount");
            AssertEqual(
                standard.StandardSigningBonusWon - expectedDiscount,
                network.SigningBonusWon,
                "discounted signing bonus");
            AssertEqual(false, standard.TalentNetworkApplied, "standard offer skill flag");
            AssertEqual(true, network.TalentNetworkApplied, "network offer skill flag");
        }

        private static void ValidateCandidateIdBoundary()
        {
            var candidateIds = new[]
            {
                "prepared_employee_01",
                "prepared_employee_02",
                "prepared_employee_03",
                "prepared_employee_04",
                "prepared_employee_05",
                "prepared_employee_06",
                "prepared_employee_07",
                "prepared_employee_08"
            };
            var offersById = new Dictionary<string, RecruitmentOffer>(StringComparer.Ordinal);
            for (var index = 0; index < candidateIds.Length; index++)
            {
                var candidateId = candidateIds[index];
                var offer = RecruitmentOfferRules.Create(
                    20_000_103,
                    candidateId,
                    BusinessIndustry.FashionRetailAndOffline,
                    "마케터",
                    1_440,
                    0,
                    false);
                offersById.Add(candidateId, offer);
                AssertEqual(candidateId, offer.CandidateId, "caller-owned candidate ID " + index);
            }

            AssertEqual(8, offersById.Count, "eight external candidate IDs remain distinct");
        }

        private static void ValidateInputRejections()
        {
            AssertThrows<ArgumentException>(
                () => RecruitmentOfferRules.Create(
                    1, "", BusinessIndustry.WebAndSoftware, "프로그래머", 0, 0, false),
                "blank candidate ID rejected");
            AssertThrows<ArgumentException>(
                () => RecruitmentOfferRules.Create(
                    1, "candidate", BusinessIndustry.WebAndSoftware, "생산직", 0, 0, false),
                "industry-role mismatch rejected");
            AssertThrows<ArgumentOutOfRangeException>(
                () => RecruitmentOfferRules.Create(
                    1, "candidate", (BusinessIndustry)999, "프로그래머", 0, 0, false),
                "unknown industry rejected");
            AssertThrows<ArgumentOutOfRangeException>(
                () => RecruitmentOfferRules.Create(
                    1, "candidate", BusinessIndustry.WebAndSoftware, "프로그래머", -1, 0, false),
                "negative offer minute rejected");
            AssertThrows<ArgumentOutOfRangeException>(
                () => RecruitmentOfferRules.Create(
                    1, "candidate", BusinessIndustry.WebAndSoftware, "프로그래머", 0, -1, false),
                "negative offer sequence rejected");
            AssertThrows<ArgumentOutOfRangeException>(
                () => EmployeeGradeRules.FromRoll(1_000),
                "grade roll upper bound rejected");
        }

        private static RecruitmentOffer CreateFixture(bool hasTalentNetwork)
        {
            return RecruitmentOfferRules.Create(
                20_000_103,
                "prepared_employee_01",
                BusinessIndustry.WebAndSoftware,
                "프로그래머",
                120,
                0,
                hasTalentNetwork);
        }

        private static void AssertRoles(BusinessIndustry industry, string[] expected)
        {
            var actual = IndustryRecruitmentRoles.GetForIndustry(industry);
            AssertEqual(expected.Length, actual.Count, industry + " role count");
            for (var index = 0; index < expected.Length; index++)
            {
                AssertEqual(expected[index], actual[index], industry + " role " + index);
                AssertTrue(IndustryRecruitmentRoles.IsRecruitable(industry, expected[index]), industry + " role accepted " + index);
            }
        }

        private static void AssertOffersEqual(RecruitmentOffer expected, RecruitmentOffer actual, string label)
        {
            AssertEqual(expected.OfferKey, actual.OfferKey, label + " key");
            AssertEqual(expected.CandidateId, actual.CandidateId, label + " candidate");
            AssertEqual(expected.Industry, actual.Industry, label + " industry");
            AssertEqual(expected.RoleId, actual.RoleId, label + " role");
            AssertEqual(expected.OfferMinute, actual.OfferMinute, label + " minute");
            AssertEqual(expected.OfferSequence, actual.OfferSequence, label + " sequence");
            AssertEqual(expected.Grade, actual.Grade, label + " grade");
            AssertEqual(expected.PotentialGrade, actual.PotentialGrade, label + " potential grade");
            AssertEqual(expected.Morale, actual.Morale, label + " morale");
            AssertEqual(expected.Loyalty, actual.Loyalty, label + " loyalty");
            AssertEqual(expected.Potential, actual.Potential, label + " potential");
            AssertEqual(expected.MonthlySalaryWon, actual.MonthlySalaryWon, label + " salary");
            AssertEqual(expected.StandardSigningBonusWon, actual.StandardSigningBonusWon, label + " standard signing");
            AssertEqual(expected.TalentNetworkDiscountWon, actual.TalentNetworkDiscountWon, label + " discount");
            AssertEqual(expected.SigningBonusWon, actual.SigningBonusWon, label + " signing");
            AssertEqual(expected.TalentNetworkApplied, actual.TalentNetworkApplied, label + " talent flag");
        }

        private static void AssertThrows<TException>(Action action, string label)
            where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException)
            {
                return;
            }

            throw new InvalidOperationException(label + ": expected " + typeof(TException).Name);
        }

        private static void AssertTrue(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException(label);
        }

        private static void AssertEqual<T>(T expected, T actual, string label)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
            }
        }
    }
}
