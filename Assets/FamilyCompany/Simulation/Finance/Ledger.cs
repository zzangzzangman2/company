using System;
using System.Collections.Generic;
using System.Linq;

namespace FamilyCompany.Simulation.Finance
{
    public static class AccountCode
    {
        public const string Cash = "cash";
        public const string OwnerCapital = "owner_capital";
        public const string SalesRevenue = "sales_revenue";
        public const string OperatingExpense = "operating_expense";
        public const string BrokerageAccount = "brokerage_account";
    }

    public sealed class LedgerLine
    {
        public LedgerLine(string accountCode, long debitWon, long creditWon)
        {
            if (string.IsNullOrWhiteSpace(accountCode))
            {
                throw new ArgumentException("Account code is required.", nameof(accountCode));
            }

            if (debitWon < 0 || creditWon < 0 || (debitWon == 0) == (creditWon == 0))
            {
                throw new ArgumentException("A ledger line must have exactly one positive side.");
            }

            AccountCode = accountCode;
            DebitWon = debitWon;
            CreditWon = creditWon;
        }

        public string AccountCode { get; }
        public long DebitWon { get; }
        public long CreditWon { get; }
    }

    public sealed class LedgerTransaction
    {
        private readonly List<LedgerLine> _lines;

        public LedgerTransaction(string transactionId, long elapsedMinute, string memo, IEnumerable<LedgerLine> lines)
        {
            if (string.IsNullOrWhiteSpace(transactionId))
            {
                throw new ArgumentException("Transaction ID is required.", nameof(transactionId));
            }

            if (elapsedMinute < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedMinute));
            }

            TransactionId = transactionId;
            ElapsedMinute = elapsedMinute;
            Memo = memo ?? string.Empty;
            _lines = lines == null ? throw new ArgumentNullException(nameof(lines)) : new List<LedgerLine>(lines);
            if (_lines.Count < 2 || TotalDebitWon != TotalCreditWon)
            {
                throw new InvalidOperationException("Ledger transaction is not balanced.");
            }
        }

        public string TransactionId { get; }
        public long ElapsedMinute { get; }
        public string Memo { get; }
        public IReadOnlyList<LedgerLine> Lines => _lines;
        public long TotalDebitWon => _lines.Sum(line => line.DebitWon);
        public long TotalCreditWon => _lines.Sum(line => line.CreditWon);
    }
}
