using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Presentation.Unity.MainNavigation;
using FamilyCompany.Simulation.ManagementUi;

internal static class MainNavigationHudHarness
{
    private static int Main()
    {
        try
        {
            MainNavigationCatalog.ValidateOrThrow();
            Require(MainNavigationCatalog.All.Count == 5, "catalog count");
            Require(MainNavigationCatalog.All.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() == 5,
                "duplicate tab ID");
            var stockRoutes = MainNavigationCatalog.All
                .SelectMany(tab => tab.Features.Select(feature => new { tab.TabId, Feature = feature }))
                .Where(item => item.Feature.Action == MainNavigationFeatureAction.OpenStockMarket)
                .ToArray();
            Require(stockRoutes.Length == 1 && stockRoutes[0].TabId == MainNavigationTabId.Investment,
                "stock market must be reachable only from the investment hub");
            Require(MainNavigationCatalog.All.SelectMany(item => item.Features)
                    .All(item => item.Action != MainNavigationFeatureAction.None),
                "every visible card must have a dedicated route");
            var building = MainNavigationCatalog.All
                .SelectMany(tab => tab.Features.Select(feature => new { tab.TabId, Feature = feature }))
                .Single(item => item.Feature.RouteId == MainNavigationRouteIds.BuildingEditor);
            Require(building.TabId == MainNavigationTabId.Company &&
                    building.Feature.Action == MainNavigationFeatureAction.OpenBuildingEditor,
                "building editor must be active inside the company hub");
            var businessRoutes = MainNavigationCatalog.All
                .SelectMany(tab => tab.Features.Select(feature => new { tab.TabId, Feature = feature }))
                .Where(item => item.Feature.RouteId == MainNavigationRouteIds.BusinessContracts ||
                               item.Feature.RouteId == MainNavigationRouteIds.BusinessProducts)
                .ToArray();
            Require(businessRoutes.Length == 2 && businessRoutes.All(item =>
                        item.TabId == MainNavigationTabId.Projects &&
                        item.Feature.Action != MainNavigationFeatureAction.None),
                "business contract/product routes must be active inside the projects hub");

            var session = new MainNavigationSession();
            foreach (var definition in MainNavigationCatalog.All)
            {
                session.Open(definition.TabId);
                Require(session.HasActiveTab && session.ActiveTab == definition.TabId,
                    "route " + definition.Id);
                session.OpenFeature(definition.Features[0].Id);
                Require(session.HasActiveFeature && session.HandleEscape() && session.HasActiveTab &&
                        !session.HasActiveFeature,
                    "feature back " + definition.Id);
                Require(session.HandleEscape() && !session.HasActiveTab,
                    "escape " + definition.Id);
            }
            Require(!session.HandleEscape(), "empty escape must fall through");

            var layouts = new[]
            {
                MainNavigationLayoutMetrics.Calculate(1920, 1080, new UiSafeInsets(24, 18, 24, 18)),
                MainNavigationLayoutMetrics.Calculate(1600, 900, UiSafeInsets.None),
                MainNavigationLayoutMetrics.Calculate(1600, 1000, UiSafeInsets.None),
                MainNavigationLayoutMetrics.Calculate(2560, 1440, new UiSafeInsets(32, 20, 32, 20))
            };
            foreach (var layout in layouts) MainNavigationLayoutMetrics.Validate(layout);

            Console.WriteLine("MAIN_NAVIGATION_HARNESS: PASS tabs=5 allCards=clickable stockRoute=investment-only buildingAdapter=company businessAdapter=projects featureBack=PASS layouts=4");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("MAIN_NAVIGATION_HARNESS: FAIL " + exception);
            return 1;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
