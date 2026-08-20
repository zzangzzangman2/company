using System;
using FamilyCompany.Simulation.Company;
using FamilyCompany.Simulation.Core;
using FamilyCompany.Simulation.Events;
using FamilyCompany.Simulation.Family;
using FamilyCompany.Simulation.Game;

namespace FamilyCompany.Simulation.Prototype
{
    public static class PrototypeStateFactory
    {
        /// <summary>
        /// The father's severance. It is the company's entire opening balance, which is why the
        /// first month is spent on subcontract work rather than on anything the family owns.
        /// </summary>
        public const long StartingCapitalWon = 5_000_000;

        /// <summary>Ledger reason for the opening balance, so the source of the money is on record.</summary>
        public const string OpeningCapitalReasonKo = "아빠 퇴직금 출자";

        public static GameState Create(int worldSeed = 20000103)
        {
            var time = new GameTime();
            var family = new FamilyState(new[]
            {
                new FamilyMemberState("player", "나", FamilyRole.Player, new DateTime(1985, 8, 10), "개발·제작", 100, 60, 5),
                new FamilyMemberState("older_sister", "누나", FamilyRole.OlderSister, new DateTime(1979, 11, 20), "운영·고객 응대", 90, 65, 8),
                new FamilyMemberState("father", "아빠", FamilyRole.Father, new DateTime(1953, 6, 15), "계약·영업", 85, 60, 10),
                new FamilyMemberState("mother", "엄마", FamilyRole.Mother, new DateTime(1955, 9, 2), "재무·품질 검사", 88, 65, 9)
            });
            var company = new CompanyState("우리 가족회사");
            company.ContributeCapital("opening-capital", 0, StartingCapitalWon, OpeningCapitalReasonKo);
            var events = new DeterministicEventQueue(new[]
            {
                new ScheduledEvent("day-001-family-briefing", 60, 0, "family_briefing")
            });
            return new GameState(worldSeed, time, family, company, events);
        }
    }
}
