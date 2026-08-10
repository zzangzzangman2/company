using System;
using FamilyCompany.Simulation.Banking;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public static class BankingRulesValidation
    {
        [MenuItem("Family Company/Validate Banking Rules")]
        public static void Run()
        {
            try
            {
                ValidateRateEnvironments();
                ValidateDepositAndTaxRules();
                ValidateCorporateCreditRules();
                ValidateDsrLoanAssessment();
                ValidatePromissoryNoteDiscount();
                ValidateIntegerBoundaries();
                Debug.Log("FAMILY_COMPANY_BANKING_RULES_VALIDATION: PASS");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("FAMILY_COMPANY_BANKING_RULES_VALIDATION: FAIL");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        private static void ValidateRateEnvironments()
        {
            AssertEnvironment(2000, 120, 550, 850, "2000 rate environment");
            AssertEnvironment(2003, 120, 420, 720, "2003 rate environment");
            AssertEnvironment(2008, 150, 520, 900, "2008 rate environment");
            AssertEnvironment(2009, 80, 350, 650, "2009 rate environment");
            AssertEnvironment(2012, 40, 220, 520, "2012 rate environment");
            AssertEnvironment(2016, 50, 280, 500, "2016 rate environment");
            AssertEnvironment(2020, 10, 120, 420, "2020 rate environment");
            AssertEnvironment(2022, 100, 400, 680, "2022 rate environment");
            AssertEnvironment(2024, 80, 320, 580, "2024 rate environment");

            var start = new DateTime(2000, 1, 3);
            AssertEqual(120, BankRateRules.CheckingAnnualRateBasisPointsAt(start), "checking base rate");
            AssertEqual(135, BankRateRules.CheckingAnnualRateBasisPointsAt(start, true), "checking skill rate");
            AssertEqual(525, BankRateRules.TermDepositAnnualRateBasisPointsAt(start, 6), "six-month deposit rate");
            AssertEqual(550, BankRateRules.TermDepositAnnualRateBasisPointsAt(start, 12), "twelve-month deposit rate");
            AssertEqual(600, BankRateRules.TermDepositAnnualRateBasisPointsAt(start, 24), "twenty-four-month deposit rate");
            AssertEqual(0, BankRateRules.TermDepositAnnualRateBasisPointsAt(start, 18), "unsupported deposit term");
        }

        private static void ValidateDepositAndTaxRules()
        {
            AssertEqual(1_540, BankRateRules.InterestWithholdingTaxBasisPoints, "interest tax basis points");
            AssertEqual(55_000L, BankInterestRules.GrossSimpleInterestWon(1_000_000, 550, 365), "annual gross interest");
            AssertEqual(8_470L, BankInterestRules.InterestWithholdingTaxWon(55_000), "interest withholding tax");
            AssertEqual(46_530L, BankInterestRules.NetInterestWon(55_000), "annual net interest");
            AssertEqual(46_530L, BankInterestRules.NetSimpleInterestWon(1_000_000, 550, 365), "combined net interest");
            AssertEqual(154L, BankInterestRules.InterestWithholdingTaxWon(1_000), "15.4 percent exact fixture");
        }

        private static void ValidateCorporateCreditRules()
        {
            AssertEqual(650, BankRateRules.InitialCorporateCreditScore, "initial corporate credit score");
            AssertEqual(300, CorporateCreditRules.ClampScore(-1), "credit minimum clamp");
            AssertEqual(900, CorporateCreditRules.ClampScore(1_000), "credit maximum clamp");
            AssertEqual(900, CorporateCreditRules.ApplyScoreDelta(899, 50), "credit positive delta clamp");
            AssertEqual(300, CorporateCreditRules.ApplyScoreDelta(301, -50), "credit negative delta clamp");
            AssertTier(599, CorporateCreditGrade.Restricted, 0, 0, false, "credit restricted tier");
            AssertTier(600, CorporateCreditGrade.Watch, 3, 650, true, "credit watch tier");
            AssertTier(650, CorporateCreditGrade.Normal, 5, 400, true, "credit normal tier");
            AssertTier(700, CorporateCreditGrade.Prime, 7, 250, true, "credit prime tier");
            AssertTier(750, CorporateCreditGrade.PrimePlus, 9, 150, true, "credit prime-plus tier");
            AssertTier(800, CorporateCreditGrade.Top, 12, 80, true, "credit top tier");
        }

        private static void ValidateDsrLoanAssessment()
        {
            AssertEqual(4_000, BankRateRules.MaximumDsrBasisPoints, "DSR 40 percent basis points");
            var offer = CorporateLoanRules.Assess(
                new DateTime(2000, 1, 3),
                650,
                1_000_000,
                0,
                0,
                12,
                false);
            AssertEqual(true, offer.Eligible, "initial-score loan eligible");
            AssertEqual(1_250, offer.AnnualInterestRateBasisPoints, "initial-score loan rate");
            AssertEqual(400_000L, offer.MaximumMonthlyPaymentWon, "DSR monthly payment cap");
            AssertEqual(4_490_201L, offer.MaximumPrincipalWon, "DSR maximum principal");
            AssertEqual(400_000L, offer.MonthlyPaymentFor(offer.MaximumPrincipalWon), "maximum principal payment");

            var dsrFull = CorporateLoanRules.Assess(
                new DateTime(2000, 1, 3), 650, 1_000_000, 0, 400_000, 12, false);
            AssertEqual(CorporateLoanRejectionReason.NoCapacity, dsrFull.RejectionReason, "DSR exhausted rejection");

            var restricted = CorporateLoanRules.Assess(
                new DateTime(2000, 1, 3), 599, 1_000_000, 0, 0, 12, false);
            AssertEqual(CorporateLoanRejectionReason.CreditRestricted, restricted.RejectionReason, "credit rejection");

            var delinquent = CorporateLoanRules.Assess(
                new DateTime(2000, 1, 3), 800, 1_000_000, 0, 0, 12, true);
            AssertEqual(CorporateLoanRejectionReason.DelinquentDebt, delinquent.RejectionReason, "delinquency rejection");

            var invalidTerm = CorporateLoanRules.Assess(
                new DateTime(2000, 1, 3), 800, 1_000_000, 0, 0, 18, false);
            AssertEqual(CorporateLoanRejectionReason.InvalidTerm, invalidTerm.RejectionReason, "loan term rejection");
        }

        private static void ValidatePromissoryNoteDiscount()
        {
            var discountDate = new DateTime(2000, 1, 3);
            var sixtyDayNote = PromissoryNoteDiscountRules.Assess(
                discountDate,
                discountDate.AddDays(60),
                1_000_000,
                650);
            AssertEqual(true, sixtyDayNote.Eligible, "sixty-day note eligible");
            AssertEqual(60, sixtyDayNote.RemainingDays, "sixty-day note remaining days");
            AssertEqual(1_250, sixtyDayNote.AnnualDiscountRateBasisPoints, "sixty-day note rate");
            AssertEqual(20_548L, sixtyDayNote.DiscountCostWon, "sixty-day note discount cost");
            AssertEqual(979_452L, sixtyDayNote.NetProceedsWon, "sixty-day note proceeds");

            var issuerRisk = PromissoryNoteDiscountRules.Assess(
                discountDate,
                discountDate.AddDays(60),
                1_000_000,
                800,
                200);
            AssertEqual(1_130, issuerRisk.AnnualDiscountRateBasisPoints, "issuer-risk note rate");

            var restricted = PromissoryNoteDiscountRules.Assess(
                discountDate,
                discountDate.AddDays(60),
                1_000_000,
                599);
            AssertEqual(PromissoryNoteDiscountRejectionReason.CreditRestricted, restricted.RejectionReason, "note credit rejection");

            var matured = PromissoryNoteDiscountRules.Assess(
                discountDate,
                discountDate,
                1_000_000,
                650);
            AssertEqual(PromissoryNoteDiscountRejectionReason.InvalidMaturity, matured.RejectionReason, "matured note rejection");

            var negativeSpread = PromissoryNoteDiscountRules.Assess(
                discountDate,
                discountDate.AddDays(60),
                1_000_000,
                650,
                -1);
            AssertEqual(PromissoryNoteDiscountRejectionReason.InvalidIssuerRiskSpread, negativeSpread.RejectionReason, "negative issuer spread rejection");
        }

        private static void ValidateIntegerBoundaries()
        {
            AssertEqual(1L, BankInterestRules.GrossSimpleInterestWon(1, 10_000, 365), "one-won annual interest");
            AssertEqual(85L, BankInterestRules.NetInterestWon(100), "tax rounds to integer won");
            AssertEqual(83_334L, BankInterestRules.MonthlyAmortizingPaymentWon(1_000_000, 0, 12), "zero-rate payment ceiling");
            AssertEqual(1_008_000L, BankInterestRules.PrincipalForMonthlyPaymentWon(84_000, 0, 12), "zero-rate principal");
            AssertThrows<ArgumentOutOfRangeException>(
                () => BankInterestRules.GrossSimpleInterestWon(-1, 550, 365),
                "negative principal rejected");
        }

        private static void AssertEnvironment(
            int year,
            int checkingRate,
            int depositRate,
            int loanRate,
            string label)
        {
            var environment = BankRateRules.EnvironmentAt(new DateTime(year, 1, 1));
            AssertEqual(checkingRate, environment.CheckingAnnualRateBasisPoints, label + " checking");
            AssertEqual(depositRate, environment.TwelveMonthDepositAnnualRateBasisPoints, label + " deposit");
            AssertEqual(loanRate, environment.UnsecuredLoanBaseAnnualRateBasisPoints, label + " loan");
        }

        private static void AssertTier(
            int score,
            CorporateCreditGrade grade,
            int incomeMultiple,
            int spreadBasisPoints,
            bool eligible,
            string label)
        {
            var tier = CorporateCreditRules.TierForScore(score);
            AssertEqual(grade, tier.Grade, label + " grade");
            AssertEqual(incomeMultiple, tier.IncomeMultiple, label + " income multiple");
            AssertEqual(spreadBasisPoints, tier.LoanRateSpreadBasisPoints, label + " spread");
            AssertEqual(eligible, tier.Eligible, label + " eligibility");
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

        private static void AssertEqual<T>(T expected, T actual, string label)
        {
            if (!Equals(expected, actual))
            {
                throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
            }
        }
    }
}
