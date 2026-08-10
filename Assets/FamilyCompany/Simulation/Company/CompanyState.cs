using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Simulation.Finance;

namespace FamilyCompany.Simulation.Company
{
    public sealed class CompanyState
    {
        private readonly List<LedgerTransaction> _ledger;

        public CompanyState(string companyName, long cashWon = 0, int reputation = 0, IEnumerable<LedgerTransaction> ledger = null)
        {
            CompanyName = string.IsNullOrWhiteSpace(companyName) ? "가족회사" : companyName;
            CashWon = cashWon;
            Reputation = Math.Max(0, Math.Min(100, reputation));
            _ledger = ledger == null ? new List<LedgerTransaction>() : new List<LedgerTransaction>(ledger);
            if (_ledger.Select(item => item.TransactionId).Distinct(StringComparer.Ordinal).Count() != _ledger.Count)
            {
                throw new InvalidOperationException("Ledger transaction IDs must be unique.");
            }
        }

        public string CompanyName { get; }
        public long CashWon { get; private set; }
        public int Reputation { get; private set; }
        public IReadOnlyList<LedgerTransaction> Ledger => _ledger;

        public void ContributeCapital(string transactionId, long elapsedMinute, long amountWon)
        {
            RequirePositive(amountWon);
            Post(new LedgerTransaction(transactionId, elapsedMinute, "창업 자본금", new[]
            {
                new LedgerLine(AccountCode.Cash, amountWon, 0),
                new LedgerLine(AccountCode.OwnerCapital, 0, amountWon)
            }));
        }

        public void RecordSale(string transactionId, long elapsedMinute, long amountWon)
        {
            RequirePositive(amountWon);
            Post(new LedgerTransaction(transactionId, elapsedMinute, "매출", new[]
            {
                new LedgerLine(AccountCode.Cash, amountWon, 0),
                new LedgerLine(AccountCode.SalesRevenue, 0, amountWon)
            }));
        }

        public void PayOperatingExpense(string transactionId, long elapsedMinute, long amountWon, string memo)
        {
            RequirePositive(amountWon);
            if (amountWon > CashWon)
            {
                throw new InvalidOperationException("현금이 부족합니다.");
            }

            Post(new LedgerTransaction(transactionId, elapsedMinute, memo, new[]
            {
                new LedgerLine(AccountCode.OperatingExpense, amountWon, 0),
                new LedgerLine(AccountCode.Cash, 0, amountWon)
            }));
        }

        public void RecordContractPenalty(string transactionId, long elapsedMinute, long amountWon, string memo)
        {
            RequirePositive(amountWon);
            Post(new LedgerTransaction(transactionId, elapsedMinute, memo, new[]
            {
                new LedgerLine(AccountCode.OperatingExpense, amountWon, 0),
                new LedgerLine(AccountCode.Cash, 0, amountWon)
            }));
        }

        public void ChangeReputation(int delta)
        {
            Reputation = Math.Max(0, Math.Min(100, Reputation + delta));
        }

        private void Post(LedgerTransaction transaction)
        {
            if (_ledger.Any(item => item.TransactionId == transaction.TransactionId))
            {
                throw new InvalidOperationException($"Duplicate transaction ID: {transaction.TransactionId}");
            }

            var cashDelta = transaction.Lines
                .Where(line => line.AccountCode == AccountCode.Cash)
                .Sum(line => line.DebitWon - line.CreditWon);
            CashWon = checked(CashWon + cashDelta);
            _ledger.Add(transaction);
        }

        private static void RequirePositive(long value)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
        }
    }
}
