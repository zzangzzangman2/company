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
        public static GameState Create(int worldSeed = 20000103)
        {
            var time = new GameTime();
            var family = new FamilyState(new[]
            {
                new FamilyMemberState("player", "나", FamilyRole.Player, new DateTime(1985, 8, 10), "아이디어·제품·시장 조사", 100, 60, 5),
                new FamilyMemberState("older_sister", "누나", FamilyRole.OlderSister, new DateTime(1979, 11, 20), "운영·고객 응대·사무 지원", 90, 65, 8),
                new FamilyMemberState("father", "아빠", FamilyRole.Father, new DateTime(1953, 6, 15), "법정대리·계약·은행·영업", 85, 60, 10),
                new FamilyMemberState("mother", "엄마", FamilyRole.Mother, new DateTime(1955, 9, 2), "재무·회계·급여·가계", 88, 65, 9)
            });
            var company = new CompanyState("우리 가족회사");
            company.ContributeCapital("opening-capital", 0, 5_000_000);
            var events = new DeterministicEventQueue(new[]
            {
                new ScheduledEvent("day-001-family-briefing", 60, 0, "family_briefing")
            });
            return new GameState(worldSeed, time, family, company, events);
        }
    }
}

