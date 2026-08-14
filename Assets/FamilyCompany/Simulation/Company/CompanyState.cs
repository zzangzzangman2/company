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

        public bool HasTransaction(string transactionId) =>
            !string.IsNullOrWhiteSpace(transactionId) &&
            _ledger.Any(item => string.Equals(item.TransactionId, transactionId, StringComparison.Ordinal));

        /// <summary>Posts a capitalized office-furniture purchase. Call only after layout validation.</summary>
        public void PurchaseOfficeFurniture(
            string transactionId,
            long elapsedMinute,
            long amountWon,
            string memo)
        {
            RequirePositive(amountWon);
            if (amountWon > CashWon) throw new InvalidOperationException("회사 자금이 부족합니다.");
            Post(new LedgerTransaction(transactionId, elapsedMinute, memo, new[]
            {
                new LedgerLine(AccountCode.OfficeFurnitureAssets, amountWon, 0),
                new LedgerLine(AccountCode.Cash, 0, amountWon)
            }));
        }

        /// <summary>
        /// Disposes of a furniture asset at its configured resale value while preserving the
        /// original purchase basis in the ledger. The difference is an explicit disposal loss.
        /// </summary>
        public void SellOfficeFurniture(
            string transactionId,
            long elapsedMinute,
            long purchaseBasisWon,
            long refundWon,
            string memo,
            bool capitalizedPurchase = true)
        {
            RequirePositive(purchaseBasisWon);
            if (refundWon <= 0 || refundWon > purchaseBasisWon)
                throw new ArgumentOutOfRangeException(nameof(refundWon));
            checked { _ = CashWon + refundWon; }
            if (!capitalizedPurchase)
            {
                Post(new LedgerTransaction(transactionId, elapsedMinute, memo, new[]
                {
                    new LedgerLine(AccountCode.Cash, refundWon, 0),
                    new LedgerLine(AccountCode.OfficeFurnitureSaleIncome, 0, refundWon)
                }));
                return;
            }
            var lines = new List<LedgerLine>
            {
                new LedgerLine(AccountCode.Cash, refundWon, 0),
                new LedgerLine(AccountCode.OfficeFurnitureAssets, 0, purchaseBasisWon)
            };
            long lossWon = purchaseBasisWon - refundWon;
            if (lossWon > 0) lines.Insert(1, new LedgerLine(AccountCode.AssetDisposalLoss, lossWon, 0));
            Post(new LedgerTransaction(transactionId, elapsedMinute, memo, lines));
        }

        public void ChangeReputation(int delta)
        {
            Reputation = Math.Max(0, Math.Min(100, Reputation + delta));
        }

        internal void PostBrokerageTransfer(
            string transactionId,
            long elapsedMinute,
            long amountWon,
            bool companyToBrokerage)
        {
            RequirePositive(amountWon);
            if (companyToBrokerage && amountWon > CashWon)
            {
                throw new InvalidOperationException("회사 현금이 부족합니다.");
            }

            Post(new LedgerTransaction(
                transactionId,
                elapsedMinute,
                companyToBrokerage ? "증권 예수금 입금" : "증권 예수금 출금",
                companyToBrokerage
                    ? new[]
                    {
                        new LedgerLine(AccountCode.BrokerageAccount, amountWon, 0),
                        new LedgerLine(AccountCode.Cash, 0, amountWon)
                    }
                    : new[]
                    {
                        new LedgerLine(AccountCode.Cash, amountWon, 0),
                        new LedgerLine(AccountCode.BrokerageAccount, 0, amountWon)
                    }));
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
