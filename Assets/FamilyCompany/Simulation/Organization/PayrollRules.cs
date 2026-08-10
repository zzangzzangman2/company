using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Numerics;
using FamilyCompany.Simulation.Core;

namespace FamilyCompany.Simulation.Organization
{
    public enum PayrollWorkerKind
    {
        FamilyMember = 0,
        ExternalEmployee = 1
    }

    public enum PayrollPaymentStatus
    {
        NoObligation = 0,
        Paid = 1,
        Partial = 2,
        Unpaid = 3
    }

    public enum PersonnelRiskGrade
    {
        None = 0,
        Low = 1,
        Guarded = 2,
        High = 3,
        Critical = 4
    }

    public sealed class PayrollWorkerInput
    {
        public const long NoArrearsSinceMinute = -1;

        public PayrollWorkerInput(
            string workerId,
            PayrollWorkerKind workerKind,
            EmployeeGrade grade,
            long monthlySalaryDueWon,
            long priorArrearsWon,
            long priorArrearsSinceMinute,
            int morale,
            int loyalty,
            int stress,
            int crunchOvertimeMinutes,
            int consecutiveCrunchDays)
        {
            if (string.IsNullOrWhiteSpace(workerId)) throw new ArgumentException("A worker ID is required.", nameof(workerId));
            if (!Enum.IsDefined(typeof(PayrollWorkerKind), workerKind)) throw new ArgumentOutOfRangeException(nameof(workerKind));
            if (!Enum.IsDefined(typeof(EmployeeGrade), grade)) throw new ArgumentOutOfRangeException(nameof(grade));
            if (monthlySalaryDueWon < 0) throw new ArgumentOutOfRangeException(nameof(monthlySalaryDueWon));
            if (priorArrearsWon < 0) throw new ArgumentOutOfRangeException(nameof(priorArrearsWon));
            if (priorArrearsWon == 0 && priorArrearsSinceMinute != NoArrearsSinceMinute)
            {
                throw new ArgumentException("A worker without arrears must use NoArrearsSinceMinute.", nameof(priorArrearsSinceMinute));
            }

            if (priorArrearsWon > 0 && priorArrearsSinceMinute < 0)
            {
                throw new ArgumentException("Existing arrears require a non-negative start minute.", nameof(priorArrearsSinceMinute));
            }

            ValidatePercent(morale, nameof(morale));
            ValidatePercent(loyalty, nameof(loyalty));
            ValidatePercent(stress, nameof(stress));
            if (crunchOvertimeMinutes < 0) throw new ArgumentOutOfRangeException(nameof(crunchOvertimeMinutes));
            if (consecutiveCrunchDays < 0) throw new ArgumentOutOfRangeException(nameof(consecutiveCrunchDays));
            if (crunchOvertimeMinutes == 0 && consecutiveCrunchDays != 0)
            {
                throw new ArgumentException("A crunch streak requires overtime minutes.", nameof(consecutiveCrunchDays));
            }

            WorkerId = workerId.Trim();
            WorkerKind = workerKind;
            Grade = grade;
            MonthlySalaryDueWon = monthlySalaryDueWon;
            PriorArrearsWon = priorArrearsWon;
            PriorArrearsSinceMinute = priorArrearsSinceMinute;
            Morale = morale;
            Loyalty = loyalty;
            Stress = stress;
            CrunchOvertimeMinutes = crunchOvertimeMinutes;
            ConsecutiveCrunchDays = consecutiveCrunchDays;
        }

        public string WorkerId { get; }
        public PayrollWorkerKind WorkerKind { get; }
        public EmployeeGrade Grade { get; }
        public long MonthlySalaryDueWon { get; }
        public long PriorArrearsWon { get; }
        public long PriorArrearsSinceMinute { get; }
        public int Morale { get; }
        public int Loyalty { get; }
        public int Stress { get; }
        public int CrunchOvertimeMinutes { get; }
        public int ConsecutiveCrunchDays { get; }

        private static void ValidatePercent(int value, string parameterName)
        {
            if (value < 0 || value > 100) throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    public sealed class PayrollWorkerOutcome
    {
        internal PayrollWorkerOutcome(
            PayrollWorkerInput input,
            PayrollPaymentStatus paymentStatus,
            long totalDueWon,
            long paidPriorArrearsWon,
            long paidCurrentSalaryWon,
            long remainingPriorArrearsWon,
            long currentSalaryArrearsWon,
            long arrearsSinceMinute,
            int arrearsDays,
            int unpaidBasisPoints,
            int crunchIntensityPoints,
            int morale,
            int loyalty,
            int stress,
            int departureRiskScore,
            PersonnelRiskGrade departureRiskGrade,
            int scoutRiskScore,
            PersonnelRiskGrade scoutRiskGrade)
        {
            WorkerId = input.WorkerId;
            WorkerKind = input.WorkerKind;
            Grade = input.Grade;
            PaymentStatus = paymentStatus;
            MonthlySalaryDueWon = input.MonthlySalaryDueWon;
            PriorArrearsDueWon = input.PriorArrearsWon;
            TotalDueWon = totalDueWon;
            PaidPriorArrearsWon = paidPriorArrearsWon;
            PaidCurrentSalaryWon = paidCurrentSalaryWon;
            TotalPaidWon = checked(paidPriorArrearsWon + paidCurrentSalaryWon);
            RemainingPriorArrearsWon = remainingPriorArrearsWon;
            CurrentSalaryArrearsWon = currentSalaryArrearsWon;
            TotalArrearsWon = checked(remainingPriorArrearsWon + currentSalaryArrearsWon);
            ArrearsSinceMinute = arrearsSinceMinute;
            ArrearsDays = arrearsDays;
            UnpaidBasisPoints = unpaidBasisPoints;
            CrunchOvertimeMinutes = input.CrunchOvertimeMinutes;
            ConsecutiveCrunchDays = input.ConsecutiveCrunchDays;
            CrunchIntensityPoints = crunchIntensityPoints;
            MoraleBefore = input.Morale;
            LoyaltyBefore = input.Loyalty;
            StressBefore = input.Stress;
            Morale = morale;
            Loyalty = loyalty;
            Stress = stress;
            MoraleDelta = morale - input.Morale;
            LoyaltyDelta = loyalty - input.Loyalty;
            StressDelta = stress - input.Stress;
            DepartureRiskScore = departureRiskScore;
            DepartureRiskGrade = departureRiskGrade;
            ScoutRiskScore = scoutRiskScore;
            ScoutRiskGrade = scoutRiskGrade;
        }

        public string WorkerId { get; }
        public PayrollWorkerKind WorkerKind { get; }
        public EmployeeGrade Grade { get; }
        public PayrollPaymentStatus PaymentStatus { get; }
        public long MonthlySalaryDueWon { get; }
        public long PriorArrearsDueWon { get; }
        public long TotalDueWon { get; }
        public long PaidPriorArrearsWon { get; }
        public long PaidCurrentSalaryWon { get; }
        public long TotalPaidWon { get; }
        public long RemainingPriorArrearsWon { get; }
        public long CurrentSalaryArrearsWon { get; }
        public long TotalArrearsWon { get; }
        public long ArrearsSinceMinute { get; }
        public int ArrearsDays { get; }
        public int UnpaidBasisPoints { get; }
        public int CrunchOvertimeMinutes { get; }
        public int ConsecutiveCrunchDays { get; }
        public int CrunchIntensityPoints { get; }
        public int MoraleBefore { get; }
        public int LoyaltyBefore { get; }
        public int StressBefore { get; }
        public int MoraleDelta { get; }
        public int LoyaltyDelta { get; }
        public int StressDelta { get; }
        public int Morale { get; }
        public int Loyalty { get; }
        public int Stress { get; }
        public int DepartureRiskScore { get; }
        public PersonnelRiskGrade DepartureRiskGrade { get; }
        public int ScoutRiskScore { get; }
        public PersonnelRiskGrade ScoutRiskGrade { get; }
    }

    public sealed class PayrollCycleResult
    {
        private readonly ReadOnlyCollection<PayrollWorkerOutcome> _workers;

        internal PayrollCycleResult(
            string cycleKey,
            long payrollCycleIndex,
            long paydayMinute,
            long evaluationMinute,
            long availableCashWon,
            long totalDueWon,
            long totalPaidWon,
            long totalArrearsWon,
            PayrollWorkerOutcome[] workers)
        {
            CycleKey = cycleKey;
            PayrollCycleIndex = payrollCycleIndex;
            PaydayMinute = paydayMinute;
            EvaluationMinute = evaluationMinute;
            AvailableCashWon = availableCashWon;
            TotalDueWon = totalDueWon;
            TotalPaidWon = totalPaidWon;
            TotalArrearsWon = totalArrearsWon;
            UnusedCashWon = checked(availableCashWon - totalPaidWon);
            _workers = Array.AsReadOnly(workers ?? Array.Empty<PayrollWorkerOutcome>());
        }

        public string CycleKey { get; }
        public long PayrollCycleIndex { get; }
        public long PaydayMinute { get; }
        public long EvaluationMinute { get; }
        public long AvailableCashWon { get; }
        public long TotalDueWon { get; }
        public long TotalPaidWon { get; }
        public long TotalArrearsWon { get; }
        public long UnusedCashWon { get; }
        public IReadOnlyList<PayrollWorkerOutcome> Workers => _workers;

        public PayrollWorkerOutcome GetWorker(string workerId)
        {
            if (string.IsNullOrWhiteSpace(workerId)) throw new ArgumentException("A worker ID is required.", nameof(workerId));
            var normalizedWorkerId = workerId.Trim();
            for (var index = 0; index < _workers.Count; index++)
            {
                if (string.Equals(_workers[index].WorkerId, normalizedWorkerId, StringComparison.Ordinal)) return _workers[index];
            }

            throw new KeyNotFoundException("Unknown payroll worker: " + normalizedWorkerId);
        }
    }

    public static class PersonnelRiskRules
    {
        public const int MaximumRiskScore = 100;

        public static PersonnelRiskGrade GradeForScore(int score)
        {
            if (score < 0 || score > MaximumRiskScore) throw new ArgumentOutOfRangeException(nameof(score));
            if (score < 15) return PersonnelRiskGrade.None;
            if (score < 30) return PersonnelRiskGrade.Low;
            if (score < 50) return PersonnelRiskGrade.Guarded;
            if (score < 70) return PersonnelRiskGrade.High;
            return PersonnelRiskGrade.Critical;
        }
    }

    public static class PayrollRules
    {
        public const int BasisPointDenominator = 10_000;
        public const long MinutesPerDay = 1_440;
        public const int CrunchBlockMinutes = 120;
        public const int MaximumCrunchIntensityPoints = 12;

        public static PayrollCycleResult SettleMonthlyPayroll(
            int worldSeed,
            long payrollCycleIndex,
            long paydayMinute,
            long evaluationMinute,
            long availableCashWon,
            IReadOnlyList<PayrollWorkerInput> workers)
        {
            if (payrollCycleIndex < 0) throw new ArgumentOutOfRangeException(nameof(payrollCycleIndex));
            if (paydayMinute < 0) throw new ArgumentOutOfRangeException(nameof(paydayMinute));
            if (evaluationMinute < paydayMinute) throw new ArgumentOutOfRangeException(nameof(evaluationMinute));
            if (availableCashWon < 0) throw new ArgumentOutOfRangeException(nameof(availableCashWon));
            if (workers == null) throw new ArgumentNullException(nameof(workers));

            var cycleKey = BuildCycleKey(worldSeed, payrollCycleIndex, paydayMinute);
            var allocations = BuildAllocations(workers, paydayMinute);
            var totalDueWon = SumDue(allocations);
            var payrollBudgetWon = Math.Min(availableCashWon, totalDueWon);

            var externalWorkers = SelectKind(allocations, PayrollWorkerKind.ExternalEmployee);
            var familyWorkers = SelectKind(allocations, PayrollWorkerKind.FamilyMember);
            var externalPaidWon = AllocateGroup(externalWorkers, payrollBudgetWon, cycleKey + ":external");
            var remainingBudgetWon = checked(payrollBudgetWon - externalPaidWon);
            AllocateGroup(familyWorkers, remainingBudgetWon, cycleKey + ":family");

            allocations.Sort((left, right) => string.CompareOrdinal(left.Input.WorkerId, right.Input.WorkerId));
            var outcomes = new PayrollWorkerOutcome[allocations.Count];
            long totalPaidWon = 0;
            long totalArrearsWon = 0;
            for (var index = 0; index < allocations.Count; index++)
            {
                var outcome = BuildOutcome(allocations[index], cycleKey, paydayMinute, evaluationMinute);
                outcomes[index] = outcome;
                totalPaidWon = checked(totalPaidWon + outcome.TotalPaidWon);
                totalArrearsWon = checked(totalArrearsWon + outcome.TotalArrearsWon);
            }

            if (checked(totalPaidWon + totalArrearsWon) != totalDueWon)
            {
                throw new InvalidOperationException("Payroll conservation failed.");
            }

            return new PayrollCycleResult(
                cycleKey,
                payrollCycleIndex,
                paydayMinute,
                evaluationMinute,
                availableCashWon,
                totalDueWon,
                totalPaidWon,
                totalArrearsWon,
                outcomes);
        }

        private static List<WorkerAllocation> BuildAllocations(
            IReadOnlyList<PayrollWorkerInput> workers,
            long paydayMinute)
        {
            var seenWorkerIds = new HashSet<string>(StringComparer.Ordinal);
            var allocations = new List<WorkerAllocation>(workers.Count);
            for (var index = 0; index < workers.Count; index++)
            {
                var input = workers[index] ?? throw new ArgumentException("Payroll workers cannot contain null.", nameof(workers));
                if (!seenWorkerIds.Add(input.WorkerId)) throw new ArgumentException("Duplicate payroll worker ID: " + input.WorkerId, nameof(workers));
                if (input.PriorArrearsWon > 0 && input.PriorArrearsSinceMinute > paydayMinute)
                {
                    throw new ArgumentException("Prior arrears cannot start after the current payday.", nameof(workers));
                }

                allocations.Add(new WorkerAllocation(input));
            }

            return allocations;
        }

        private static long SumDue(List<WorkerAllocation> allocations)
        {
            long total = 0;
            for (var index = 0; index < allocations.Count; index++)
            {
                total = checked(total + allocations[index].TotalDueWon);
            }

            return total;
        }

        private static List<WorkerAllocation> SelectKind(
            List<WorkerAllocation> allocations,
            PayrollWorkerKind kind)
        {
            var selected = new List<WorkerAllocation>();
            for (var index = 0; index < allocations.Count; index++)
            {
                if (allocations[index].Input.WorkerKind == kind) selected.Add(allocations[index]);
            }

            return selected;
        }

        private static long AllocateGroup(
            List<WorkerAllocation> allocations,
            long availableWon,
            string allocationKey)
        {
            if (availableWon <= 0 || allocations.Count == 0) return 0;
            var groupDueWon = SumDue(allocations);
            if (groupDueWon == 0) return 0;
            if (availableWon >= groupDueWon)
            {
                for (var index = 0; index < allocations.Count; index++)
                {
                    allocations[index].PaidWon = allocations[index].TotalDueWon;
                }

                return groupDueWon;
            }

            long allocatedWon = 0;
            for (var index = 0; index < allocations.Count; index++)
            {
                var share = new BigInteger(availableWon) * allocations[index].TotalDueWon / groupDueWon;
                allocations[index].PaidWon = ToInt64(share);
                allocatedWon = checked(allocatedWon + allocations[index].PaidWon);
            }

            var remainderWon = checked(availableWon - allocatedWon);
            var remainderCandidates = new List<WorkerAllocation>();
            for (var index = 0; index < allocations.Count; index++)
            {
                if (allocations[index].PaidWon < allocations[index].TotalDueWon)
                {
                    remainderCandidates.Add(allocations[index]);
                }
            }

            if (remainderWon > remainderCandidates.Count)
            {
                throw new InvalidOperationException("Payroll remainder exceeded the eligible worker count.");
            }

            remainderCandidates.Sort((left, right) => CompareRemainderOrder(left, right, allocationKey));
            for (long offset = 0; offset < remainderWon; offset++)
            {
                var allocation = remainderCandidates[(int)offset];
                allocation.PaidWon = checked(allocation.PaidWon + 1);
            }

            return availableWon;
        }

        private static int CompareRemainderOrder(
            WorkerAllocation left,
            WorkerAllocation right,
            string allocationKey)
        {
            var leftRoll = StableRandom.StableRandomWord31(BuildWorkerKey(allocationKey, left.Input.WorkerId));
            var rightRoll = StableRandom.StableRandomWord31(BuildWorkerKey(allocationKey, right.Input.WorkerId));
            var rollComparison = leftRoll.CompareTo(rightRoll);
            return rollComparison != 0
                ? rollComparison
                : string.CompareOrdinal(left.Input.WorkerId, right.Input.WorkerId);
        }

        private static PayrollWorkerOutcome BuildOutcome(
            WorkerAllocation allocation,
            string cycleKey,
            long paydayMinute,
            long evaluationMinute)
        {
            var input = allocation.Input;
            var paidPriorArrearsWon = Math.Min(allocation.PaidWon, input.PriorArrearsWon);
            var paidCurrentSalaryWon = checked(allocation.PaidWon - paidPriorArrearsWon);
            var remainingPriorArrearsWon = checked(input.PriorArrearsWon - paidPriorArrearsWon);
            var currentSalaryArrearsWon = checked(input.MonthlySalaryDueWon - paidCurrentSalaryWon);
            var totalArrearsWon = checked(remainingPriorArrearsWon + currentSalaryArrearsWon);
            var arrearsSinceMinute = totalArrearsWon == 0
                ? PayrollWorkerInput.NoArrearsSinceMinute
                : remainingPriorArrearsWon > 0
                    ? input.PriorArrearsSinceMinute
                    : paydayMinute;
            var arrearsDays = totalArrearsWon == 0
                ? 0
                : ToBoundedInt((evaluationMinute - arrearsSinceMinute) / MinutesPerDay);
            var unpaidBasisPoints = CalculateUnpaidBasisPoints(totalArrearsWon, allocation.TotalDueWon);
            var paymentStatus = PaymentStatus(allocation.TotalDueWon, allocation.PaidWon);
            var crunchIntensityPoints = CrunchIntensity(input.CrunchOvertimeMinutes, input.ConsecutiveCrunchDays);

            CalculateStateDeltas(
                input.WorkerKind,
                allocation.TotalDueWon,
                totalArrearsWon,
                unpaidBasisPoints,
                arrearsDays,
                crunchIntensityPoints,
                out var moraleDelta,
                out var loyaltyDelta,
                out var stressDelta);
            var morale = ClampPercent(input.Morale + moraleDelta);
            var loyalty = ClampPercent(input.Loyalty + loyaltyDelta);
            var stress = ClampPercent(input.Stress + stressDelta);

            var riskNoiseKey = BuildWorkerKey(cycleKey + ":risk", input.WorkerId);
            var departureRiskScore = DepartureRiskScore(
                input.WorkerKind,
                morale,
                loyalty,
                stress,
                unpaidBasisPoints,
                arrearsDays,
                crunchIntensityPoints,
                StableRandom.StableRandomInt(riskNoiseKey + ":departure", input.WorkerKind == PayrollWorkerKind.FamilyMember ? 4 : 6));
            var scoutRiskScore = input.WorkerKind == PayrollWorkerKind.FamilyMember
                ? 0
                : ScoutRiskScore(
                    input.Grade,
                    morale,
                    loyalty,
                    stress,
                    unpaidBasisPoints,
                    arrearsDays,
                    StableRandom.StableRandomInt(riskNoiseKey + ":scout", 8));

            return new PayrollWorkerOutcome(
                input,
                paymentStatus,
                allocation.TotalDueWon,
                paidPriorArrearsWon,
                paidCurrentSalaryWon,
                remainingPriorArrearsWon,
                currentSalaryArrearsWon,
                arrearsSinceMinute,
                arrearsDays,
                unpaidBasisPoints,
                crunchIntensityPoints,
                morale,
                loyalty,
                stress,
                departureRiskScore,
                PersonnelRiskRules.GradeForScore(departureRiskScore),
                scoutRiskScore,
                PersonnelRiskRules.GradeForScore(scoutRiskScore));
        }

        private static PayrollPaymentStatus PaymentStatus(long totalDueWon, long totalPaidWon)
        {
            if (totalDueWon == 0) return PayrollPaymentStatus.NoObligation;
            if (totalPaidWon == 0) return PayrollPaymentStatus.Unpaid;
            return totalPaidWon == totalDueWon ? PayrollPaymentStatus.Paid : PayrollPaymentStatus.Partial;
        }

        private static int CrunchIntensity(int overtimeMinutes, int consecutiveCrunchDays)
        {
            if (overtimeMinutes == 0) return 0;
            var overtimeBlocks = ((long)overtimeMinutes + CrunchBlockMinutes - 1) / CrunchBlockMinutes;
            var streakPoints = Math.Min(6, Math.Max(0, consecutiveCrunchDays - 1));
            return (int)Math.Min(MaximumCrunchIntensityPoints, overtimeBlocks + streakPoints);
        }

        private static void CalculateStateDeltas(
            PayrollWorkerKind workerKind,
            long totalDueWon,
            long totalArrearsWon,
            int unpaidBasisPoints,
            int arrearsDays,
            int crunchIntensityPoints,
            out int moraleDelta,
            out int loyaltyDelta,
            out int stressDelta)
        {
            moraleDelta = 0;
            loyaltyDelta = 0;
            stressDelta = 0;
            if (totalDueWon > 0 && totalArrearsWon == 0)
            {
                moraleDelta += 1;
                loyaltyDelta += 1;
                stressDelta -= 1;
            }
            else if (totalArrearsWon > 0)
            {
                var shortagePoints = Math.Min(10, CeilingDividePositive(unpaidBasisPoints, 1_000));
                var delayPoints = Math.Min(8, CeilingDividePositive(arrearsDays, 7));
                if (workerKind == PayrollWorkerKind.ExternalEmployee)
                {
                    moraleDelta -= shortagePoints + delayPoints;
                    loyaltyDelta -= CeilingDividePositive(shortagePoints, 2) + delayPoints;
                    stressDelta += shortagePoints + delayPoints * 2;
                }
                else
                {
                    moraleDelta -= CeilingDividePositive(shortagePoints, 2) + CeilingDividePositive(delayPoints, 2);
                    loyaltyDelta -= CeilingDividePositive(shortagePoints, 3) + CeilingDividePositive(delayPoints, 2);
                    stressDelta += CeilingDividePositive(shortagePoints, 2) + delayPoints;
                }
            }

            if (crunchIntensityPoints <= 0) return;
            moraleDelta -= CeilingDividePositive(crunchIntensityPoints, 2);
            if (workerKind == PayrollWorkerKind.ExternalEmployee)
            {
                loyaltyDelta -= CeilingDividePositive(crunchIntensityPoints, 3);
                stressDelta += crunchIntensityPoints;
            }
            else
            {
                loyaltyDelta -= CeilingDividePositive(crunchIntensityPoints, 2);
                stressDelta += CeilingDividePositive(crunchIntensityPoints * 3, 2);
            }
        }

        private static int DepartureRiskScore(
            PayrollWorkerKind workerKind,
            int morale,
            int loyalty,
            int stress,
            int unpaidBasisPoints,
            int arrearsDays,
            int crunchIntensityPoints,
            int deterministicNoise)
        {
            int score;
            if (workerKind == PayrollWorkerKind.ExternalEmployee)
            {
                score = checked(
                    (100 - loyalty) * 30 / 100 +
                    (100 - morale) * 20 / 100 +
                    stress * 20 / 100 +
                    Math.Min(20, arrearsDays / 3) +
                    unpaidBasisPoints * 10 / BasisPointDenominator +
                    Math.Min(10, crunchIntensityPoints) +
                    deterministicNoise);
            }
            else
            {
                score = checked(
                    (100 - loyalty) * 35 / 100 +
                    (100 - morale) * 10 / 100 +
                    stress * 30 / 100 +
                    Math.Min(10, arrearsDays / 6) +
                    Math.Min(15, crunchIntensityPoints) +
                    deterministicNoise);
            }

            return ClampRisk(score);
        }

        private static int ScoutRiskScore(
            EmployeeGrade grade,
            int morale,
            int loyalty,
            int stress,
            int unpaidBasisPoints,
            int arrearsDays,
            int deterministicNoise)
        {
            var gradePoints = ScoutGradePoints(grade);
            var score = checked(
                gradePoints +
                (100 - loyalty) * 30 / 100 +
                (100 - morale) * 10 / 100 +
                stress * 10 / 100 +
                Math.Min(10, arrearsDays / 6) +
                unpaidBasisPoints * 10 / BasisPointDenominator +
                deterministicNoise);
            return ClampRisk(score);
        }

        private static int ScoutGradePoints(EmployeeGrade grade)
        {
            switch (grade)
            {
                case EmployeeGrade.S: return 30;
                case EmployeeGrade.A: return 22;
                case EmployeeGrade.B: return 14;
                case EmployeeGrade.C: return 8;
                case EmployeeGrade.D: return 4;
                case EmployeeGrade.F: return 0;
                default: throw new ArgumentOutOfRangeException(nameof(grade));
            }
        }

        private static int CalculateUnpaidBasisPoints(long unpaidWon, long totalDueWon)
        {
            if (unpaidWon == 0 || totalDueWon == 0) return 0;
            var numerator = new BigInteger(unpaidWon) * BasisPointDenominator;
            var result = (numerator + totalDueWon - 1) / totalDueWon;
            return (int)BigInteger.Min(BasisPointDenominator, result);
        }

        private static int CeilingDividePositive(int value, int divisor)
        {
            if (value <= 0) return 0;
            return checked((value + divisor - 1) / divisor);
        }

        private static int ClampPercent(int value)
        {
            return Math.Max(0, Math.Min(100, value));
        }

        private static int ClampRisk(int value)
        {
            return Math.Max(0, Math.Min(PersonnelRiskRules.MaximumRiskScore, value));
        }

        private static int ToBoundedInt(long value)
        {
            return value >= int.MaxValue ? int.MaxValue : (int)value;
        }

        private static long ToInt64(BigInteger value)
        {
            if (value < 0 || value > long.MaxValue) throw new OverflowException("Payroll result does not fit in integer won.");
            return (long)value;
        }

        private static string BuildCycleKey(int worldSeed, long payrollCycleIndex, long paydayMinute)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "payroll-v1:{0}:{1}:{2}",
                worldSeed,
                payrollCycleIndex,
                paydayMinute);
        }

        private static string BuildWorkerKey(string prefix, string workerId)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}:{1}:{2}",
                prefix,
                workerId.Length,
                workerId);
        }

        private sealed class WorkerAllocation
        {
            public WorkerAllocation(PayrollWorkerInput input)
            {
                Input = input;
                TotalDueWon = checked(input.MonthlySalaryDueWon + input.PriorArrearsWon);
            }

            public PayrollWorkerInput Input { get; }
            public long TotalDueWon { get; }
            public long PaidWon { get; set; }
        }
    }
}
