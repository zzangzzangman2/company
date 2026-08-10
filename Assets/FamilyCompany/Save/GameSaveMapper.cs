using System;
using System.Linq;
using FamilyCompany.Simulation.Company;
using FamilyCompany.Simulation.Core;
using FamilyCompany.Simulation.Events;
using FamilyCompany.Simulation.Family;
using FamilyCompany.Simulation.Finance;
using FamilyCompany.Simulation.Game;

namespace FamilyCompany.Save
{
    public static class GameSaveMapper
    {
        public static GameSaveDto ToDto(GameState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            return new GameSaveDto
            {
                worldSeed = state.WorldSeed,
                elapsedMinutes = state.Time.ElapsedMinutes,
                company = new CompanySaveDto
                {
                    companyName = state.Company.CompanyName,
                    cashWon = state.Company.CashWon,
                    reputation = state.Company.Reputation
                },
                family = state.Family.Members.Select(member => new FamilyMemberSaveDto
                {
                    memberId = member.MemberId,
                    displayName = member.DisplayName,
                    role = (int)member.Role,
                    birthYear = member.BirthDate.Year,
                    birthMonth = member.BirthDate.Month,
                    birthDay = member.BirthDate.Day,
                    companyDuty = member.CompanyDuty,
                    energy = member.Energy,
                    trust = member.Trust,
                    stress = member.Stress
                }).ToList(),
                events = state.Events.Snapshot().Select(item => new ScheduledEventSaveDto
                {
                    eventId = item.EventId,
                    dueMinute = item.DueMinute,
                    priority = item.Priority,
                    kind = item.Kind,
                    payload = item.Payload
                }).ToList(),
                ledger = state.Company.Ledger.Select(transaction => new LedgerTransactionSaveDto
                {
                    transactionId = transaction.TransactionId,
                    elapsedMinute = transaction.ElapsedMinute,
                    memo = transaction.Memo,
                    lines = transaction.Lines.Select(line => new LedgerLineSaveDto
                    {
                        accountCode = line.AccountCode,
                        debitWon = line.DebitWon,
                        creditWon = line.CreditWon
                    }).ToList()
                }).ToList()
            };
        }

        public static GameState FromDto(GameSaveDto save)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            if (save.schemaVersion != 1) throw new InvalidOperationException($"Unsupported save schema: {save.schemaVersion}");
            if (save.company == null || save.family == null || save.events == null || save.ledger == null)
            {
                throw new InvalidOperationException("Save data is incomplete.");
            }

            var ledger = save.ledger.Select(transaction => new LedgerTransaction(
                transaction.transactionId,
                transaction.elapsedMinute,
                transaction.memo,
                transaction.lines.Select(line => new LedgerLine(line.accountCode, line.debitWon, line.creditWon))));
            var company = new CompanyState(save.company.companyName, save.company.cashWon, save.company.reputation, ledger);
            var family = new FamilyState(save.family.Select(member => new FamilyMemberState(
                member.memberId,
                member.displayName,
                (FamilyRole)member.role,
                new DateTime(member.birthYear, member.birthMonth, member.birthDay),
                member.companyDuty,
                member.energy,
                member.trust,
                member.stress)));
            var events = new DeterministicEventQueue(save.events.Select(item => new ScheduledEvent(
                item.eventId,
                item.dueMinute,
                item.priority,
                item.kind,
                item.payload)));
            return new GameState(save.worldSeed, new GameTime(save.elapsedMinutes), family, company, events);
        }
    }
}

