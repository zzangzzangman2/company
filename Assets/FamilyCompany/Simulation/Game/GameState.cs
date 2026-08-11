using System;
using FamilyCompany.Simulation.Company;
using FamilyCompany.Simulation.Contracts;
using FamilyCompany.Simulation.Core;
using FamilyCompany.Simulation.Events;
using FamilyCompany.Simulation.Family;
using FamilyCompany.Simulation.Market;
using FamilyCompany.Simulation.OfficeLayout;

namespace FamilyCompany.Simulation.Game
{
    public sealed class GameState
    {
        public GameState(
            int worldSeed,
            GameTime time,
            FamilyState family,
            CompanyState company,
            DeterministicEventQueue events,
            ContractPortfolio contracts = null,
            CompanyGrowthState growth = null,
            StockMarketSessionStateDto stockMarket = null,
            OfficeGrid officeGrid = null)
        {
            WorldSeed = worldSeed;
            Time = time ?? throw new ArgumentNullException(nameof(time));
            Family = family ?? throw new ArgumentNullException(nameof(family));
            Company = company ?? throw new ArgumentNullException(nameof(company));
            Events = events ?? throw new ArgumentNullException(nameof(events));
            Contracts = contracts ?? new ContractPortfolio(Family.Members.Count);
            Growth = growth ?? new CompanyGrowthState();
            StockMarket = stockMarket ?? StockMarketSessionStateDto.Uninitialized();
            OfficeGrid = officeGrid ?? OfficeGridLayouts.CreateMigrationPreview();
        }

        public int WorldSeed { get; }
        public GameTime Time { get; }
        public FamilyState Family { get; }
        public CompanyState Company { get; }
        public DeterministicEventQueue Events { get; }
        public ContractPortfolio Contracts { get; }
        public CompanyGrowthState Growth { get; }
        public StockMarketSessionStateDto StockMarket { get; private set; }
        public OfficeGrid OfficeGrid { get; }

        public void ReplaceStockMarketState(StockMarketSessionStateDto state)
        {
            StockMarket = state ?? throw new ArgumentNullException(nameof(state));
        }
    }
}
