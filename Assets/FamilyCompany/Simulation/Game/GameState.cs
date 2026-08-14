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
            OfficeGrid officeGrid = null,
            OfficeFurnitureInventoryState officeFurnitureInventory = null)
        {
            WorldSeed = worldSeed;
            Time = time ?? throw new ArgumentNullException(nameof(time));
            Family = family ?? throw new ArgumentNullException(nameof(family));
            Company = company ?? throw new ArgumentNullException(nameof(company));
            Events = events ?? throw new ArgumentNullException(nameof(events));
            Contracts = contracts ?? new ContractPortfolio(Family.Members.Count);
            Growth = growth ?? new CompanyGrowthState();
            StockMarket = stockMarket ?? StockMarketSessionStateDto.Uninitialized();
            OfficeGrid = officeGrid ?? OfficeGridLayouts.CreateStarterOfficeV1();
            OfficeFurnitureInventory = officeFurnitureInventory ??
                                       OfficeFurnitureInventoryState.MigrateFromGrid(OfficeGrid, Time.ElapsedMinutes);
        }

        public int WorldSeed { get; }
        public GameTime Time { get; }
        public FamilyState Family { get; }
        public CompanyState Company { get; }
        public DeterministicEventQueue Events { get; }
        public ContractPortfolio Contracts { get; }
        public CompanyGrowthState Growth { get; }
        public StockMarketSessionStateDto StockMarket { get; private set; }
        public OfficeGrid OfficeGrid { get; private set; }
        public OfficeFurnitureInventoryState OfficeFurnitureInventory { get; private set; }

        public void ReplaceStockMarketState(StockMarketSessionStateDto state)
        {
            StockMarket = state ?? throw new ArgumentNullException(nameof(state));
        }

        public void ReplaceOfficeGrid(OfficeGrid grid)
        {
            OfficeGrid = grid ?? throw new ArgumentNullException(nameof(grid));
        }

        /// <summary>
        /// Swaps the semantic layout and its ownership records together after a command has fully
        /// validated both values. Runtime actor identity and transient family/contract state are
        /// intentionally outside this diff.
        /// </summary>
        public void ReplaceOfficeState(OfficeGrid grid, OfficeFurnitureInventoryState inventory)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            OfficeGrid = grid;
            OfficeFurnitureInventory = inventory;
        }
    }
}
