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
            var actionable = MainNavigationCatalog.All
                .SelectMany(tab => tab.Features.Select(feature => new { tab.TabId, Feature = feature }))
                .Where(item => item.Feature.Action != MainNavigationFeatureAction.None)
                .ToArray();
            Require(actionable.Length == 1 && actionable[0].TabId == MainNavigationTabId.Investment &&
                    actionable[0].Feature.Action == MainNavigationFeatureAction.OpenStockMarket,
                "stock market must be reachable only from the investment hub");
            var building = MainNavigationCatalog.All
                .SelectMany(tab => tab.Features.Select(feature => new { tab.TabId, Feature = feature }))
                .Single(item => item.Feature.RouteId == MainNavigationRouteIds.BuildingEditorPlaceholder);
            Require(building.TabId == MainNavigationTabId.Company &&
                    building.Feature.Action == MainNavigationFeatureAction.None,
                "building editor placeholder must remain inactive inside the company hub");
            var businessRoutes = MainNavigationCatalog.All
                .SelectMany(tab => tab.Features.Select(feature => new { tab.TabId, Feature = feature }))
                .Where(item => item.Feature.RouteId == MainNavigationRouteIds.BusinessContractsPlaceholder ||
                               item.Feature.RouteId == MainNavigationRouteIds.BusinessProductsPlaceholder)
                .ToArray();
            Require(businessRoutes.Length == 2 && businessRoutes.All(item =>
                        item.TabId == MainNavigationTabId.Projects &&
                        item.Feature.Action == MainNavigationFeatureAction.None),
                "business contract/product placeholders must remain inactive inside the projects hub");

            var session = new MainNavigationSession();
            foreach (var definition in MainNavigationCatalog.All)
            {
                session.Open(definition.TabId);
                Require(session.HasActiveTab && session.ActiveTab == definition.TabId,
                    "route " + definition.Id);
                Require(session.HandleEscape() && !session.HasActiveTab,
                    "escape " + definition.Id);
            }
            Require(!session.HandleEscape(), "empty escape must fall through");

            var layouts = new[]
            {
                MainNavigationLayoutMetrics.Calculate(1280, 720, UiSafeInsets.None),
                MainNavigationLayoutMetrics.Calculate(1920, 1080, new UiSafeInsets(24, 18, 24, 18)),
                MainNavigationLayoutMetrics.Calculate(1920, 1200, UiSafeInsets.None),
                MainNavigationLayoutMetrics.Calculate(3440, 1080, new UiSafeInsets(40, 0, 40, 0))
            };
            foreach (var layout in layouts) MainNavigationLayoutMetrics.Validate(layout);

            Console.WriteLine("MAIN_NAVIGATION_HARNESS: PASS tabs=5 routes=5 stockRoute=investment-only buildingRoute=company-placeholder businessRoutes=projects-placeholders escape=PASS layouts=4");
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
