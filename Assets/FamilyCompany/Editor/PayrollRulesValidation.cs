using System;
using System.Collections.Generic;
using FamilyCompany.Simulation.Organization;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public static class PayrollRulesValidation
    {
        [MenuItem("Family Company/Validate Payroll Rules")]
        public static void Run()
        {
            try
            {
                ValidateFullPaymentAndTotals();
                ValidatePartialPaymentPriority();
                ValidateOrderIndependentRemainder();
                ValidateArrearsAgeAndOldestFirst();
                ValidateCrunchAndWorkerKindDifferences();
                ValidateRiskProgression();
                ValidateSameInputDeterminism();
                ValidateBoundaries();
                Debug.Log("FAMILY_COMPANY_PAYROLL_RULES_VALIDATION: PASS");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("FAMILY_COMPANY_PAYROLL_RULES_VALIDATION: FAIL");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        private static void ValidateFullPaymentAndTotals()
        {
            var result = PayrollRules.SettleMonthlyPayroll(
                2_000,
                0,
                43_200,
                43_200,
                200_000,
                new[]
                {
                    Worker("external", PayrollWorkerKind.ExternalEmployee, 100_000),
                    Worker("family", PayrollWorkerKind.FamilyMember, 50_000)
                });

            AssertEqual(150_000L, result.TotalDueWon, "full payroll total due");
            AssertEqual(150_000L, result.TotalPaidWon, "full payroll total paid");
            AssertEqual(0L, result.TotalArrearsWon, "full payroll total arrears");
            AssertEqual(50_000L, result.UnusedCashWon, "full payroll unused cash");
            AssertEqual(PayrollPaymentStatus.Paid, result.GetWorker("external").PaymentStatus, "external fully paid");
            AssertEqual(PayrollPaymentStatus.Paid, result.GetWorker("family").PaymentStatus, "family fully paid");
            AssertConserved(result, "full payroll conservation");
        }

        private static void ValidatePartialPaymentPriority()
        {
            var result = PayrollRules.SettleMonthlyPayroll(
                2_000,
                1,
                86_400,
                86_400,
                200_000,
                new[]
                {
                    Worker("external-small", PayrollWorkerKind.ExternalEmployee, 100_000),
                    Worker("family", PayrollWorkerKind.FamilyMember, 100_000),
                    Worker("external-large", PayrollWorkerKind.ExternalEmployee, 300_000)
                });

            AssertEqual(500_000L, result.TotalDueWon, "partial payroll total due");
            AssertEqual(200_000L, result.TotalPaidWon, "partial payroll budget consumed");
            AssertEqual(300_000L, result.TotalArrearsWon, "partial payroll arrears");
            AssertEqual(50_000L, result.GetWorker("external-small").TotalPaidWon, "external proportional small share");
            AssertEqual(150_000L, result.GetWorker("external-large").TotalPaidWon, "external proportional large share");
            AssertEqual(0L, result.GetWorker("family").TotalPaidWon, "family absorbs cash shortfall after external payroll");
            AssertEqual(PayrollPaymentStatus.Partial, result.GetWorker("external-small").PaymentStatus, "external partial status");
            AssertEqual(PayrollPaymentStatus.Unpaid, result.GetWorker("family").PaymentStatus, "family unpaid status");
            AssertConserved(result, "partial payroll conservation");
        }

        private static void ValidateOrderIndependentRemainder()
        {
            var firstOrder = new[]
            {
                Worker("worker-c", PayrollWorkerKind.ExternalEmployee, 1),
                Worker("worker-a", PayrollWorkerKind.ExternalEmployee, 1),
                Worker("worker-b", PayrollWorkerKind.ExternalEmployee, 1)
            };
            var secondOrder = new[] { firstOrder[1], firstOrder[2], firstOrder[0] };
            var first = PayrollRules.SettleMonthlyPayroll(77, 4, 10_000, 10_000, 2, firstOrder);
            var second = PayrollRules.SettleMonthlyPayroll(77, 4, 10_000, 10_000, 2, secondOrder);

            AssertCycleEqual(first, second, "input order independence");
            AssertEqual(2L, first.TotalPaidWon, "remainder distributes exact won");
            AssertEqual(1L, first.TotalArrearsWon, "remainder leaves exact arrears");
            AssertConserved(first, "remainder conservation");

            var zeroDueMixed = PayrollRules.SettleMonthlyPayroll(
                77,
                5,
                20_000,
                20_000,
                1,
                new[]
                {
                    Worker("zero-due", PayrollWorkerKind.ExternalEmployee, 0),
                    Worker("one-a", PayrollWorkerKind.ExternalEmployee, 1),
                    Worker("one-b", PayrollWorkerKind.ExternalEmployee, 1)
                });
            AssertEqual(PayrollPaymentStatus.NoObligation, zeroDueMixed.GetWorker("zero-due").PaymentStatus, "zero due excluded from remainder");
            AssertEqual(1L, zeroDueMixed.TotalPaidWon, "mixed zero-due exact payment");
            AssertConserved(zeroDueMixed, "mixed zero-due conservation");
        }

        private static void ValidateArrearsAgeAndOldestFirst()
        {
            const long firstArrearsMinute = 0;
            const long paydayMinute = 43_200;
            const long evaluationMinute = paydayMinute + 14_400;
            var input = new PayrollWorkerInput(
                "arrears-worker",
                PayrollWorkerKind.ExternalEmployee,
                EmployeeGrade.C,
                100_000,
                50_000,
                firstArrearsMinute,
                70,
                70,
                20,
                0,
                0);

            var oldDebtCleared = PayrollRules.SettleMonthlyPayroll(
                1, 1, paydayMinute, evaluationMinute, 60_000, new[] { input }).GetWorker("arrears-worker");
            AssertEqual(50_000L, oldDebtCleared.PaidPriorArrearsWon, "old arrears paid first");
            AssertEqual(10_000L, oldDebtCleared.PaidCurrentSalaryWon, "remaining payment reaches current salary");
            AssertEqual(0L, oldDebtCleared.RemainingPriorArrearsWon, "old arrears cleared");
            AssertEqual(90_000L, oldDebtCleared.CurrentSalaryArrearsWon, "current salary remainder");
            AssertEqual(paydayMinute, oldDebtCleared.ArrearsSinceMinute, "arrears age resets after old debt cleared");
            AssertEqual(10, oldDebtCleared.ArrearsDays, "current salary arrears days");

            var oldDebtRemaining = PayrollRules.SettleMonthlyPayroll(
                1, 1, paydayMinute, evaluationMinute, 25_000, new[] { input }).GetWorker("arrears-worker");
            AssertEqual(25_000L, oldDebtRemaining.RemainingPriorArrearsWon, "old arrears remains");
            AssertEqual(firstArrearsMinute, oldDebtRemaining.ArrearsSinceMinute, "oldest arrears minute retained");
            AssertEqual(40, oldDebtRemaining.ArrearsDays, "old arrears accumulated days");
        }

        private static void ValidateCrunchAndWorkerKindDifferences()
        {
            var result = PayrollRules.SettleMonthlyPayroll(
                99,
                2,
                90_000,
                90_000,
                0,
                new[]
                {
                    Worker("external-crunch", PayrollWorkerKind.ExternalEmployee, 0, 600, 3),
                    Worker("family-crunch", PayrollWorkerKind.FamilyMember, 0, 600, 3)
                });
            var external = result.GetWorker("external-crunch");
            var family = result.GetWorker("family-crunch");

            AssertEqual(7, external.CrunchIntensityPoints, "crunch blocks and streak points");
            AssertEqual(external.CrunchIntensityPoints, family.CrunchIntensityPoints, "same crunch intensity");
            AssertTrue(external.MoraleDelta < 0, "external crunch reduces morale");
            AssertTrue(external.LoyaltyDelta < 0, "external crunch reduces loyalty");
            AssertTrue(external.StressDelta > 0, "external crunch raises stress");
            AssertTrue(family.StressDelta > external.StressDelta, "family crunch has stronger stress consequence");
            AssertEqual(0, family.ScoutRiskScore, "family has no scout risk score");
            AssertEqual(PersonnelRiskGrade.None, family.ScoutRiskGrade, "family has no scout risk grade");
        }

        private static void ValidateRiskProgression()
        {
            var healthy = PayrollRules.SettleMonthlyPayroll(
                123,
                3,
                100_000,
                100_000,
                100_000,
                new[]
                {
                    new PayrollWorkerInput(
                        "risk-worker", PayrollWorkerKind.ExternalEmployee, EmployeeGrade.S,
                        100_000, 0, PayrollWorkerInput.NoArrearsSinceMinute,
                        90, 90, 5, 0, 0)
                }).GetWorker("risk-worker");
            var distressed = PayrollRules.SettleMonthlyPayroll(
                123,
                3,
                100_000,
                100_000 + 60 * PayrollRules.MinutesPerDay,
                0,
                new[]
                {
                    new PayrollWorkerInput(
                        "risk-worker", PayrollWorkerKind.ExternalEmployee, EmployeeGrade.S,
                        100_000, 100_000, 0,
                        25, 20, 80, 1_440, 7)
                }).GetWorker("risk-worker");

            AssertTrue(distressed.DepartureRiskScore > healthy.DepartureRiskScore, "distress raises departure score");
            AssertTrue(distressed.ScoutRiskScore > healthy.ScoutRiskScore, "distress raises scout score");
            AssertEqual(PersonnelRiskGrade.Critical, distressed.DepartureRiskGrade, "severe distress departure risk");
            AssertTrue((int)distressed.ScoutRiskGrade >= (int)PersonnelRiskGrade.High, "severe distress scout risk");

            AssertEqual(PersonnelRiskGrade.None, PersonnelRiskRules.GradeForScore(14), "risk none upper boundary");
            AssertEqual(PersonnelRiskGrade.Low, PersonnelRiskRules.GradeForScore(15), "risk low lower boundary");
            AssertEqual(PersonnelRiskGrade.Guarded, PersonnelRiskRules.GradeForScore(30), "risk guarded lower boundary");
            AssertEqual(PersonnelRiskGrade.High, PersonnelRiskRules.GradeForScore(50), "risk high lower boundary");
            AssertEqual(PersonnelRiskGrade.Critical, PersonnelRiskRules.GradeForScore(70), "risk critical lower boundary");
        }

        private static void ValidateSameInputDeterminism()
        {
            var workers = new[]
            {
                Worker("deterministic-external", PayrollWorkerKind.ExternalEmployee, 123_457, 180, 2),
                Worker("deterministic-family", PayrollWorkerKind.FamilyMember, 75_003, 60, 1)
            };
            var first = PayrollRules.SettleMonthlyPayroll(456, 8, 500_000, 502_880, 111_111, workers);
            var second = PayrollRules.SettleMonthlyPayroll(456, 8, 500_000, 502_880, 111_111, workers);
            AssertCycleEqual(first, second, "same input deterministic result");
        }

        private static void ValidateBoundaries()
        {
            var noWorkers = PayrollRules.SettleMonthlyPayroll(
                1, 0, 0, 0, 10, Array.Empty<PayrollWorkerInput>());
            AssertEqual(0L, noWorkers.TotalDueWon, "empty payroll due");
            AssertEqual(10L, noWorkers.UnusedCashWon, "empty payroll cash preserved");

            AssertThrows<ArgumentException>(
                () => new PayrollWorkerInput(
                    "worker", PayrollWorkerKind.ExternalEmployee, EmployeeGrade.C,
                    1, 0, 0, 50, 50, 50, 0, 0),
                "no arrears sentinel enforced");
            AssertThrows<ArgumentOutOfRangeException>(
                () => new PayrollWorkerInput(
                    "worker", PayrollWorkerKind.ExternalEmployee, EmployeeGrade.C,
                    -1, 0, PayrollWorkerInput.NoArrearsSinceMinute, 50, 50, 50, 0, 0),
                "negative salary rejected");
            AssertThrows<ArgumentException>(
                () => new PayrollWorkerInput(
                    "worker", PayrollWorkerKind.ExternalEmployee, EmployeeGrade.C,
                    1, 0, PayrollWorkerInput.NoArrearsSinceMinute, 50, 50, 50, 0, 1),
                "crunch streak without overtime rejected");
            AssertThrows<ArgumentException>(
                () => PayrollRules.SettleMonthlyPayroll(
                    1, 0, 100, 100, 1,
                    new[]
                    {
                        Worker("duplicate", PayrollWorkerKind.ExternalEmployee, 1),
                        Worker("duplicate", PayrollWorkerKind.FamilyMember, 1)
                    }),
                "duplicate worker rejected");
            AssertThrows<ArgumentOutOfRangeException>(
                () => PayrollRules.SettleMonthlyPayroll(
                    1, 0, 100, 99, 1, new[] { Worker("worker", PayrollWorkerKind.ExternalEmployee, 1) }),
                "evaluation before payday rejected");
            AssertThrows<ArgumentException>(
                () => PayrollRules.SettleMonthlyPayroll(
                    1, 0, 100, 100, 1,
                    new[]
                    {
                        new PayrollWorkerInput(
                            "future-arrears", PayrollWorkerKind.ExternalEmployee, EmployeeGrade.C,
                            0, 1, 101, 50, 50, 50, 0, 0)
                    }),
                "future prior arrears rejected");
            AssertThrows<ArgumentOutOfRangeException>(
                () => PersonnelRiskRules.GradeForScore(101),
                "risk score upper bound rejected");
        }

        private static PayrollWorkerInput Worker(
            string workerId,
            PayrollWorkerKind kind,
            long salaryDueWon,
            int crunchOvertimeMinutes = 0,
            int consecutiveCrunchDays = 0)
        {
            return new PayrollWorkerInput(
                workerId,
                kind,
                EmployeeGrade.B,
                salaryDueWon,
                0,
                PayrollWorkerInput.NoArrearsSinceMinute,
                70,
                70,
                20,
                crunchOvertimeMinutes,
                consecutiveCrunchDays);
        }

        private static void AssertConserved(PayrollCycleResult result, string label)
        {
            AssertEqual(result.TotalDueWon, result.TotalPaidWon + result.TotalArrearsWon, label + " due split");
            AssertEqual(result.AvailableCashWon, result.TotalPaidWon + result.UnusedCashWon, label + " cash split");
            long workerPaid = 0;
            long workerArrears = 0;
            for (var index = 0; index < result.Workers.Count; index++)
            {
                var worker = result.Workers[index];
                AssertEqual(worker.TotalDueWon, worker.TotalPaidWon + worker.TotalArrearsWon, label + " worker " + worker.WorkerId);
                workerPaid = checked(workerPaid + worker.TotalPaidWon);
                workerArrears = checked(workerArrears + worker.TotalArrearsWon);
            }

            AssertEqual(result.TotalPaidWon, workerPaid, label + " worker paid sum");
            AssertEqual(result.TotalArrearsWon, workerArrears, label + " worker arrears sum");
        }

        private static void AssertCycleEqual(PayrollCycleResult expected, PayrollCycleResult actual, string label)
        {
            AssertEqual(expected.CycleKey, actual.CycleKey, label + " key");
            AssertEqual(expected.TotalDueWon, actual.TotalDueWon, label + " total due");
            AssertEqual(expected.TotalPaidWon, actual.TotalPaidWon, label + " total paid");
            AssertEqual(expected.TotalArrearsWon, actual.TotalArrearsWon, label + " total arrears");
            AssertEqual(expected.UnusedCashWon, actual.UnusedCashWon, label + " unused cash");
            AssertEqual(expected.Workers.Count, actual.Workers.Count, label + " worker count");
            for (var index = 0; index < expected.Workers.Count; index++)
            {
                var left = expected.Workers[index];
                var right = actual.Workers[index];
                AssertEqual(left.WorkerId, right.WorkerId, label + " worker ID " + index);
                AssertEqual(left.TotalPaidWon, right.TotalPaidWon, label + " paid " + left.WorkerId);
                AssertEqual(left.TotalArrearsWon, right.TotalArrearsWon, label + " arrears " + left.WorkerId);
                AssertEqual(left.ArrearsDays, right.ArrearsDays, label + " arrears days " + left.WorkerId);
                AssertEqual(left.Morale, right.Morale, label + " morale " + left.WorkerId);
                AssertEqual(left.Loyalty, right.Loyalty, label + " loyalty " + left.WorkerId);
                AssertEqual(left.Stress, right.Stress, label + " stress " + left.WorkerId);
                AssertEqual(left.DepartureRiskScore, right.DepartureRiskScore, label + " departure " + left.WorkerId);
                AssertEqual(left.ScoutRiskScore, right.ScoutRiskScore, label + " scout " + left.WorkerId);
            }
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
