using System;
using System.Linq;
using FamilyCompany.Simulation.Company;

namespace FamilyCompany.Simulation.Market
{
    public enum BrokerageTransferRejectionReason
    {
        None = 0,
        InvalidAmount = 1,
        DuplicateTransaction = 2,
        InsufficientCompanyCash = 3,
        InsufficientAvailableBrokerageCash = 4,
        Overflow = 5
    }

    public sealed class BrokerageTransferResult
    {
        internal BrokerageTransferResult(
            bool accepted,
            BrokerageTransferRejectionReason rejectionReason,
            long amountWon,
            long companyCashWon,
            long brokerageCashWon)
        {
            Accepted = accepted;
            RejectionReason = rejectionReason;
            AmountWon = amountWon;
            CompanyCashWon = companyCashWon;
            BrokerageCashWon = brokerageCashWon;
        }

        public bool Accepted { get; }
        public BrokerageTransferRejectionReason RejectionReason { get; }
        public long AmountWon { get; }
        public long CompanyCashWon { get; }
        public long BrokerageCashWon { get; }
    }

    /// <summary>
    /// Atomic boundary between the company cash ledger and the brokerage
    /// subsidiary account. Transfers are allowed during every market phase;
    /// open buy-order reservations are never withdrawable.
    /// </summary>
    public sealed class CompanyBrokerageTransferService
    {
        private readonly CompanyState _company;
        private readonly StockMarketRuntimeSession _session;

        public CompanyBrokerageTransferService(CompanyState company, StockMarketRuntimeSession session)
        {
            _company = company ?? throw new ArgumentNullException(nameof(company));
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public BrokerageTransferResult Deposit(
            string transactionId,
            long elapsedMinute,
            long amountWon)
        {
            var rejection = ValidateCommon(transactionId, amountWon);
            if (rejection != BrokerageTransferRejectionReason.None) return Rejected(rejection, amountWon);
            if (amountWon > _company.CashWon)
                return Rejected(BrokerageTransferRejectionReason.InsufficientCompanyCash, amountWon);
            try
            {
                checked { _ = _session.BrokerageCash + amountWon; }
                _company.PostBrokerageTransfer(transactionId, elapsedMinute, amountWon, true);
                _session.AdjustBrokerageCashForCompanyTransfer(amountWon);
                return Accepted(amountWon);
            }
            catch (OverflowException)
            {
                return Rejected(BrokerageTransferRejectionReason.Overflow, amountWon);
            }
        }

        public BrokerageTransferResult Deposit(long elapsedMinute, long amountWon)
        {
            return Deposit(NextTransactionId(elapsedMinute, true), elapsedMinute, amountWon);
        }

        public BrokerageTransferResult Withdraw(
            string transactionId,
            long elapsedMinute,
            long amountWon)
        {
            var rejection = ValidateCommon(transactionId, amountWon);
            if (rejection != BrokerageTransferRejectionReason.None) return Rejected(rejection, amountWon);
            if (amountWon > _session.AvailableBrokerageCash)
                return Rejected(BrokerageTransferRejectionReason.InsufficientAvailableBrokerageCash, amountWon);
            try
            {
                checked { _ = _company.CashWon + amountWon; }
                _company.PostBrokerageTransfer(transactionId, elapsedMinute, amountWon, false);
                _session.AdjustBrokerageCashForCompanyTransfer(-amountWon);
                return Accepted(amountWon);
            }
            catch (OverflowException)
            {
                return Rejected(BrokerageTransferRejectionReason.Overflow, amountWon);
            }
        }

        public BrokerageTransferResult Withdraw(long elapsedMinute, long amountWon)
        {
            return Withdraw(NextTransactionId(elapsedMinute, false), elapsedMinute, amountWon);
        }

        private BrokerageTransferRejectionReason ValidateCommon(string transactionId, long amountWon)
        {
            if (amountWon <= 0 || string.IsNullOrWhiteSpace(transactionId))
                return BrokerageTransferRejectionReason.InvalidAmount;
            return _company.Ledger.Any(item => item.TransactionId == transactionId)
                ? BrokerageTransferRejectionReason.DuplicateTransaction
                : BrokerageTransferRejectionReason.None;
        }

        private string NextTransactionId(long elapsedMinute, bool companyToBrokerage)
        {
            var direction = companyToBrokerage ? "deposit" : "withdraw";
            var prefix = $"stock-transfer-{direction}-{elapsedMinute}";
            for (var sequence = 1; sequence < int.MaxValue; sequence += 1)
            {
                var candidate = $"{prefix}-{sequence}";
                if (_company.Ledger.All(item => item.TransactionId != candidate))
                    return candidate;
            }

            throw new InvalidOperationException("Stock transfer transaction ID space is exhausted.");
        }

        private BrokerageTransferResult Accepted(long amountWon)
        {
            return new BrokerageTransferResult(
                true,
                BrokerageTransferRejectionReason.None,
                amountWon,
                _company.CashWon,
                _session.BrokerageCash);
        }

        private BrokerageTransferResult Rejected(BrokerageTransferRejectionReason reason, long amountWon)
        {
            return new BrokerageTransferResult(false, reason, amountWon, _company.CashWon, _session.BrokerageCash);
        }
    }
}
