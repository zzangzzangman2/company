using System;
using System.Numerics;

namespace FamilyCompany.Simulation.Banking
{
    public sealed class BankRateEnvironment
    {
        public BankRateEnvironment(
            int checkingAnnualRateBasisPoints,
            int twelveMonthDepositAnnualRateBasisPoints,
            int unsecuredLoanBaseAnnualRateBasisPoints,
            string label)
        {
            if (checkingAnnualRateBasisPoints < 0) throw new ArgumentOutOfRangeException(nameof(checkingAnnualRateBasisPoints));
            if (twelveMonthDepositAnnualRateBasisPoints < 0) throw new ArgumentOutOfRangeException(nameof(twelveMonthDepositAnnualRateBasisPoints));
            if (unsecuredLoanBaseAnnualRateBasisPoints < 0) throw new ArgumentOutOfRangeException(nameof(unsecuredLoanBaseAnnualRateBasisPoints));

            CheckingAnnualRateBasisPoints = checkingAnnualRateBasisPoints;
            TwelveMonthDepositAnnualRateBasisPoints = twelveMonthDepositAnnualRateBasisPoints;
            UnsecuredLoanBaseAnnualRateBasisPoints = unsecuredLoanBaseAnnualRateBasisPoints;
            Label = label ?? string.Empty;
        }

        public int CheckingAnnualRateBasisPoints { get; }
        public int TwelveMonthDepositAnnualRateBasisPoints { get; }
        public int UnsecuredLoanBaseAnnualRateBasisPoints { get; }
        public string Label { get; }
    }

    public static class BankRateRules
    {
        public const int BasisPointDenominator = 10_000;
        public const int InterestWithholdingTaxBasisPoints = 1_540;
        public const int MaximumDsrBasisPoints = 4_000;
        public const int MinimumCorporateCreditScore = 300;
        public const int MaximumCorporateCreditScore = 900;
        public const int InitialCorporateCreditScore = 650;
        public const int CashManagementBonusBasisPoints = 15;

        private static readonly BankRateEnvironment HighRate2000To2002 =
            new BankRateEnvironment(120, 550, 850, "2000~2002 고금리기");
        private static readonly BankRateEnvironment Stable2003To2007 =
            new BankRateEnvironment(120, 420, 720, "2003~2007 안정기");
        private static readonly BankRateEnvironment CreditCrunch2008 =
            new BankRateEnvironment(150, 520, 900, "2008 신용경색기");
        private static readonly BankRateEnvironment Recovery2009To2011 =
            new BankRateEnvironment(80, 350, 650, "2009~2011 회복기");
        private static readonly BankRateEnvironment LowRate2012To2015 =
            new BankRateEnvironment(40, 220, 520, "2012~2015 저금리기");
        private static readonly BankRateEnvironment Normalization2016To2019 =
            new BankRateEnvironment(50, 280, 500, "2016~2019 정상화기");
        private static readonly BankRateEnvironment UltraLow2020To2021 =
            new BankRateEnvironment(10, 120, 420, "2020~2021 초저금리기");
        private static readonly BankRateEnvironment Tightening2022To2023 =
            new BankRateEnvironment(100, 400, 680, "2022~2023 긴축기");
        private static readonly BankRateEnvironment Adjustment2024AndAfter =
            new BankRateEnvironment(80, 320, 580, "2024~2026 조정기");

        public static BankRateEnvironment EnvironmentAt(DateTime date)
        {
            if (date.Year <= 2002) return HighRate2000To2002;
            if (date.Year <= 2007) return Stable2003To2007;
            if (date.Year == 2008) return CreditCrunch2008;
            if (date.Year <= 2011) return Recovery2009To2011;
            if (date.Year <= 2015) return LowRate2012To2015;
            if (date.Year <= 2019) return Normalization2016To2019;
            if (date.Year <= 2021) return UltraLow2020To2021;
            if (date.Year <= 2023) return Tightening2022To2023;
            return Adjustment2024AndAfter;
        }

        public static bool IsSupportedDepositTerm(int termMonths)
        {
            return termMonths == 6 || termMonths == 12 || termMonths == 24;
        }

        public static bool IsSupportedLoanTerm(int termMonths)
        {
            return termMonths == 12 || termMonths == 24 || termMonths == 36;
        }

        public static int CheckingAnnualRateBasisPointsAt(DateTime date, bool cashManagementSkill = false)
        {
            return checked(EnvironmentAt(date).CheckingAnnualRateBasisPoints +
                           (cashManagementSkill ? CashManagementBonusBasisPoints : 0));
        }

        public static int TermDepositAnnualRateBasisPointsAt(
            DateTime date,
            int termMonths,
            bool cashManagementSkill = false)
        {
            if (!IsSupportedDepositTerm(termMonths)) return 0;
            var termAdjustment = termMonths == 6 ? -25 : termMonths == 24 ? 50 : 0;
            var result = checked(EnvironmentAt(date).TwelveMonthDepositAnnualRateBasisPoints +
                                 termAdjustment +
                                 (cashManagementSkill ? CashManagementBonusBasisPoints : 0));
            return Math.Max(0, result);
        }
    }

    public static class BankInterestRules
    {
        public static long GrossSimpleInterestWon(
            long principalWon,
            int annualRateBasisPoints,
            int interestDays)
        {
            ValidateNonNegative(principalWon, nameof(principalWon));
            if (annualRateBasisPoints < 0) throw new ArgumentOutOfRangeException(nameof(annualRateBasisPoints));
            if (interestDays < 0) throw new ArgumentOutOfRangeException(nameof(interestDays));
            if (principalWon == 0 || annualRateBasisPoints == 0 || interestDays == 0) return 0;

            var numerator = new BigInteger(principalWon) * annualRateBasisPoints * interestDays;
            var denominator = new BigInteger(BankRateRules.BasisPointDenominator) * 365;
            return ToInt64(RoundHalfUpNonNegative(numerator, denominator));
        }

        public static long InterestWithholdingTaxWon(long grossInterestWon)
        {
            ValidateNonNegative(grossInterestWon, nameof(grossInterestWon));
            if (grossInterestWon == 0) return 0;
            var numerator = new BigInteger(grossInterestWon) * BankRateRules.InterestWithholdingTaxBasisPoints;
            return ToInt64(RoundHalfUpNonNegative(numerator, BankRateRules.BasisPointDenominator));
        }

        public static long NetInterestWon(long grossInterestWon)
        {
            var tax = InterestWithholdingTaxWon(grossInterestWon);
            return checked(grossInterestWon - tax);
        }

        public static long NetSimpleInterestWon(
            long principalWon,
            int annualRateBasisPoints,
            int interestDays)
        {
            return NetInterestWon(GrossSimpleInterestWon(principalWon, annualRateBasisPoints, interestDays));
        }

        public static long MonthlyAmortizingPaymentWon(
            long principalWon,
            int annualRateBasisPoints,
            int remainingMonths)
        {
            ValidateNonNegative(principalWon, nameof(principalWon));
            if (annualRateBasisPoints < 0) throw new ArgumentOutOfRangeException(nameof(annualRateBasisPoints));
            if (remainingMonths <= 0) throw new ArgumentOutOfRangeException(nameof(remainingMonths));
            if (principalWon == 0) return 0;
            if (annualRateBasisPoints == 0)
            {
                return ToInt64(CeilingDivide(principalWon, remainingMonths));
            }

            const int monthlyRateDenominator = BankRateRules.BasisPointDenominator * 12;
            var growthNumerator = new BigInteger(monthlyRateDenominator + annualRateBasisPoints);
            var growthDenominator = new BigInteger(monthlyRateDenominator);
            var numeratorPower = BigInteger.Pow(growthNumerator, remainingMonths);
            var denominatorPower = BigInteger.Pow(growthDenominator, remainingMonths);
            var numerator = new BigInteger(principalWon) * annualRateBasisPoints * numeratorPower;
            var denominator = growthDenominator * (numeratorPower - denominatorPower);
            return ToInt64(CeilingDivide(numerator, denominator));
        }

        public static long PrincipalForMonthlyPaymentWon(
            long monthlyPaymentWon,
            int annualRateBasisPoints,
            int termMonths)
        {
            ValidateNonNegative(monthlyPaymentWon, nameof(monthlyPaymentWon));
            if (annualRateBasisPoints < 0) throw new ArgumentOutOfRangeException(nameof(annualRateBasisPoints));
            if (termMonths <= 0) throw new ArgumentOutOfRangeException(nameof(termMonths));
            if (monthlyPaymentWon == 0) return 0;
            if (annualRateBasisPoints == 0) return checked(monthlyPaymentWon * termMonths);

            const int monthlyRateDenominator = BankRateRules.BasisPointDenominator * 12;
            var growthNumerator = new BigInteger(monthlyRateDenominator + annualRateBasisPoints);
            var growthDenominator = new BigInteger(monthlyRateDenominator);
            var numeratorPower = BigInteger.Pow(growthNumerator, termMonths);
            var denominatorPower = BigInteger.Pow(growthDenominator, termMonths);
            var numerator = new BigInteger(monthlyPaymentWon) * growthDenominator *
                            (numeratorPower - denominatorPower);
            var denominator = new BigInteger(annualRateBasisPoints) * numeratorPower;
            return ToInt64(numerator / denominator);
        }

        internal static long FloorRateAmountWon(long amountWon, int rateBasisPoints)
        {
            ValidateNonNegative(amountWon, nameof(amountWon));
            if (rateBasisPoints < 0) throw new ArgumentOutOfRangeException(nameof(rateBasisPoints));
            return ToInt64(new BigInteger(amountWon) * rateBasisPoints / BankRateRules.BasisPointDenominator);
        }

        internal static long CeilingSimpleInterestWon(
            long principalWon,
            int annualRateBasisPoints,
            int interestDays)
        {
            ValidateNonNegative(principalWon, nameof(principalWon));
            if (annualRateBasisPoints < 0) throw new ArgumentOutOfRangeException(nameof(annualRateBasisPoints));
            if (interestDays < 0) throw new ArgumentOutOfRangeException(nameof(interestDays));
            if (principalWon == 0 || annualRateBasisPoints == 0 || interestDays == 0) return 0;
            var numerator = new BigInteger(principalWon) * annualRateBasisPoints * interestDays;
            var denominator = new BigInteger(BankRateRules.BasisPointDenominator) * 365;
            return ToInt64(CeilingDivide(numerator, denominator));
        }

        private static BigInteger RoundHalfUpNonNegative(BigInteger numerator, BigInteger denominator)
        {
            if (numerator.Sign < 0) throw new ArgumentOutOfRangeException(nameof(numerator));
            if (denominator.Sign <= 0) throw new ArgumentOutOfRangeException(nameof(denominator));
            return (numerator + denominator / 2) / denominator;
        }

        private static BigInteger CeilingDivide(BigInteger numerator, BigInteger denominator)
        {
            if (numerator.Sign < 0) throw new ArgumentOutOfRangeException(nameof(numerator));
            if (denominator.Sign <= 0) throw new ArgumentOutOfRangeException(nameof(denominator));
            return numerator.IsZero ? BigInteger.Zero : (numerator + denominator - 1) / denominator;
        }

        private static long ToInt64(BigInteger value)
        {
            if (value < 0 || value > long.MaxValue) throw new OverflowException("Banking result does not fit in integer won.");
            return (long)value;
        }

        private static void ValidateNonNegative(long value, string parameterName)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    public enum CorporateCreditGrade
    {
        Restricted = 0,
        Watch = 1,
        Normal = 2,
        Prime = 3,
        PrimePlus = 4,
        Top = 5
    }

    public sealed class CorporateCreditTier
    {
        public CorporateCreditTier(
            CorporateCreditGrade grade,
            string label,
            int incomeMultiple,
            int loanRateSpreadBasisPoints,
            bool eligible)
        {
            Grade = grade;
            Label = label ?? string.Empty;
            IncomeMultiple = incomeMultiple;
            LoanRateSpreadBasisPoints = loanRateSpreadBasisPoints;
            Eligible = eligible;
        }

        public CorporateCreditGrade Grade { get; }
        public string Label { get; }
        public int IncomeMultiple { get; }
        public int LoanRateSpreadBasisPoints { get; }
        public bool Eligible { get; }
    }

    public static class CorporateCreditRules
    {
        private static readonly CorporateCreditTier Top =
            new CorporateCreditTier(CorporateCreditGrade.Top, "최우량", 12, 80, true);
        private static readonly CorporateCreditTier PrimePlus =
            new CorporateCreditTier(CorporateCreditGrade.PrimePlus, "우량+", 9, 150, true);
        private static readonly CorporateCreditTier Prime =
            new CorporateCreditTier(CorporateCreditGrade.Prime, "우량", 7, 250, true);
        private static readonly CorporateCreditTier Normal =
            new CorporateCreditTier(CorporateCreditGrade.Normal, "보통", 5, 400, true);
        private static readonly CorporateCreditTier Watch =
            new CorporateCreditTier(CorporateCreditGrade.Watch, "주의", 3, 650, true);
        private static readonly CorporateCreditTier Restricted =
            new CorporateCreditTier(CorporateCreditGrade.Restricted, "대출 제한", 0, 0, false);

        public static int ClampScore(int score)
        {
            return Math.Max(BankRateRules.MinimumCorporateCreditScore,
                Math.Min(BankRateRules.MaximumCorporateCreditScore, score));
        }

        public static int ApplyScoreDelta(int score, int delta)
        {
            var adjusted = (long)ClampScore(score) + delta;
            if (adjusted <= BankRateRules.MinimumCorporateCreditScore) return BankRateRules.MinimumCorporateCreditScore;
            if (adjusted >= BankRateRules.MaximumCorporateCreditScore) return BankRateRules.MaximumCorporateCreditScore;
            return (int)adjusted;
        }

        public static CorporateCreditTier TierForScore(int score)
        {
            var clamped = ClampScore(score);
            if (clamped >= 800) return Top;
            if (clamped >= 750) return PrimePlus;
            if (clamped >= 700) return Prime;
            if (clamped >= 650) return Normal;
            if (clamped >= 600) return Watch;
            return Restricted;
        }
    }

    public enum CorporateLoanRejectionReason
    {
        None = 0,
        InvalidTerm = 1,
        DelinquentDebt = 2,
        CreditRestricted = 3,
        NoQualifyingIncome = 4,
        NoCapacity = 5
    }

    public sealed class CorporateLoanAssessment
    {
        public CorporateLoanAssessment(
            bool eligible,
            CorporateLoanRejectionReason rejectionReason,
            long maximumPrincipalWon,
            int annualInterestRateBasisPoints,
            int termMonths,
            long maximumMonthlyPaymentWon,
            long qualifyingMonthlyIncomeWon,
            CorporateCreditTier creditTier)
        {
            Eligible = eligible;
            RejectionReason = rejectionReason;
            MaximumPrincipalWon = maximumPrincipalWon;
            AnnualInterestRateBasisPoints = annualInterestRateBasisPoints;
            TermMonths = termMonths;
            MaximumMonthlyPaymentWon = maximumMonthlyPaymentWon;
            QualifyingMonthlyIncomeWon = qualifyingMonthlyIncomeWon;
            CreditTier = creditTier ?? throw new ArgumentNullException(nameof(creditTier));
        }

        public bool Eligible { get; }
        public CorporateLoanRejectionReason RejectionReason { get; }
        public long MaximumPrincipalWon { get; }
        public int AnnualInterestRateBasisPoints { get; }
        public int TermMonths { get; }
        public long MaximumMonthlyPaymentWon { get; }
        public long QualifyingMonthlyIncomeWon { get; }
        public CorporateCreditTier CreditTier { get; }

        public long MonthlyPaymentFor(long principalWon)
        {
            return BankInterestRules.MonthlyAmortizingPaymentWon(
                principalWon,
                AnnualInterestRateBasisPoints,
                TermMonths);
        }
    }

    public static class CorporateLoanRules
    {
        public static CorporateLoanAssessment Assess(
            DateTime date,
            int creditScore,
            long qualifyingMonthlyIncomeWon,
            long existingUnsecuredBalanceWon,
            long existingMonthlyDebtServiceWon,
            int termMonths,
            bool hasDelinquency)
        {
            if (qualifyingMonthlyIncomeWon < 0) throw new ArgumentOutOfRangeException(nameof(qualifyingMonthlyIncomeWon));
            if (existingUnsecuredBalanceWon < 0) throw new ArgumentOutOfRangeException(nameof(existingUnsecuredBalanceWon));
            if (existingMonthlyDebtServiceWon < 0) throw new ArgumentOutOfRangeException(nameof(existingMonthlyDebtServiceWon));

            var tier = CorporateCreditRules.TierForScore(creditScore);
            var annualRate = checked(BankRateRules.EnvironmentAt(date).UnsecuredLoanBaseAnnualRateBasisPoints +
                                     tier.LoanRateSpreadBasisPoints);

            CorporateLoanAssessment Denied(CorporateLoanRejectionReason reason)
            {
                return new CorporateLoanAssessment(
                    false,
                    reason,
                    0,
                    annualRate,
                    termMonths,
                    0,
                    qualifyingMonthlyIncomeWon,
                    tier);
            }

            if (!BankRateRules.IsSupportedLoanTerm(termMonths)) return Denied(CorporateLoanRejectionReason.InvalidTerm);
            if (hasDelinquency) return Denied(CorporateLoanRejectionReason.DelinquentDebt);
            if (!tier.Eligible) return Denied(CorporateLoanRejectionReason.CreditRestricted);
            if (qualifyingMonthlyIncomeWon == 0) return Denied(CorporateLoanRejectionReason.NoQualifyingIncome);

            var grossCreditLimit = checked(qualifyingMonthlyIncomeWon * tier.IncomeMultiple);
            var availableByCredit = Math.Max(0, checked(grossCreditLimit - existingUnsecuredBalanceWon));
            var maximumTotalMonthlyDebt = BankInterestRules.FloorRateAmountWon(
                qualifyingMonthlyIncomeWon,
                BankRateRules.MaximumDsrBasisPoints);
            var availableMonthlyPayment = Math.Max(
                0,
                checked(maximumTotalMonthlyDebt - existingMonthlyDebtServiceWon));
            var availableByDsr = BankInterestRules.PrincipalForMonthlyPaymentWon(
                availableMonthlyPayment,
                annualRate,
                termMonths);
            var maximumPrincipal = Math.Min(availableByCredit, availableByDsr);
            if (maximumPrincipal <= 0) return Denied(CorporateLoanRejectionReason.NoCapacity);

            return new CorporateLoanAssessment(
                true,
                CorporateLoanRejectionReason.None,
                maximumPrincipal,
                annualRate,
                termMonths,
                availableMonthlyPayment,
                qualifyingMonthlyIncomeWon,
                tier);
        }
    }

    public enum PromissoryNoteDiscountRejectionReason
    {
        None = 0,
        InvalidMaturity = 1,
        DelinquentDebt = 2,
        CreditRestricted = 3,
        InvalidIssuerRiskSpread = 4,
        NoNetProceeds = 5
    }

    public sealed class PromissoryNoteDiscountAssessment
    {
        public PromissoryNoteDiscountAssessment(
            bool eligible,
            PromissoryNoteDiscountRejectionReason rejectionReason,
            long faceValueWon,
            int remainingDays,
            int annualDiscountRateBasisPoints,
            long discountCostWon,
            long netProceedsWon,
            CorporateCreditTier creditTier)
        {
            Eligible = eligible;
            RejectionReason = rejectionReason;
            FaceValueWon = faceValueWon;
            RemainingDays = remainingDays;
            AnnualDiscountRateBasisPoints = annualDiscountRateBasisPoints;
            DiscountCostWon = discountCostWon;
            NetProceedsWon = netProceedsWon;
            CreditTier = creditTier ?? throw new ArgumentNullException(nameof(creditTier));
        }

        public bool Eligible { get; }
        public PromissoryNoteDiscountRejectionReason RejectionReason { get; }
        public long FaceValueWon { get; }
        public int RemainingDays { get; }
        public int AnnualDiscountRateBasisPoints { get; }
        public long DiscountCostWon { get; }
        public long NetProceedsWon { get; }
        public CorporateCreditTier CreditTier { get; }
    }

    public static class PromissoryNoteDiscountRules
    {
        public static PromissoryNoteDiscountAssessment Assess(
            DateTime discountDate,
            DateTime maturityDate,
            long faceValueWon,
            int corporateCreditScore,
            int issuerRiskSpreadBasisPoints = 0,
            bool hasDelinquency = false)
        {
            if (faceValueWon <= 0) throw new ArgumentOutOfRangeException(nameof(faceValueWon));
            var tier = CorporateCreditRules.TierForScore(corporateCreditScore);
            var remainingDayValue = (maturityDate.Date - discountDate.Date).Days;
            var remainingDays = remainingDayValue <= 0 || remainingDayValue > int.MaxValue
                ? 0
                : (int)remainingDayValue;
            var safeIssuerSpread = Math.Max(0, issuerRiskSpreadBasisPoints);
            var annualRate = checked(BankRateRules.EnvironmentAt(discountDate).UnsecuredLoanBaseAnnualRateBasisPoints +
                                     tier.LoanRateSpreadBasisPoints +
                                     safeIssuerSpread);

            PromissoryNoteDiscountAssessment Denied(PromissoryNoteDiscountRejectionReason reason)
            {
                return new PromissoryNoteDiscountAssessment(
                    false,
                    reason,
                    faceValueWon,
                    remainingDays,
                    annualRate,
                    0,
                    0,
                    tier);
            }

            if (maturityDate.Date <= discountDate.Date) return Denied(PromissoryNoteDiscountRejectionReason.InvalidMaturity);
            if (issuerRiskSpreadBasisPoints < 0) return Denied(PromissoryNoteDiscountRejectionReason.InvalidIssuerRiskSpread);
            if (hasDelinquency) return Denied(PromissoryNoteDiscountRejectionReason.DelinquentDebt);
            if (!tier.Eligible) return Denied(PromissoryNoteDiscountRejectionReason.CreditRestricted);

            var discountCost = BankInterestRules.CeilingSimpleInterestWon(
                faceValueWon,
                annualRate,
                remainingDays);
            if (discountCost >= faceValueWon) return Denied(PromissoryNoteDiscountRejectionReason.NoNetProceeds);
            return new PromissoryNoteDiscountAssessment(
                true,
                PromissoryNoteDiscountRejectionReason.None,
                faceValueWon,
                remainingDays,
                annualRate,
                discountCost,
                checked(faceValueWon - discountCost),
                tier);
        }
    }
}
