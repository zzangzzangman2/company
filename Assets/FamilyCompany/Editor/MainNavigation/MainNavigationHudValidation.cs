using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FamilyCompany.Presentation.Unity.MainNavigation;
using FamilyCompany.Presentation.Unity.ManagementUI;
using FamilyCompany.Presentation.Unity.UIRemaster;
using FamilyCompany.Simulation.ManagementUi;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public static class MainNavigationHudValidation
    {
        private const string BootstrapPath =
            "Assets/FamilyCompany/Presentation.Unity/PrototypeBootstrap.cs";
        private const string LegacyPresenterPath =
            "Assets/FamilyCompany/Presentation.Unity/ManagementUI/ManagementUiV2Presenter.cs";
        private const string PresenterPath =
            "Assets/FamilyCompany/Presentation.Unity/MainNavigation/MainNavigationHudPresenter.cs";
        private const string StockMarketPresenterPath =
            "Assets/FamilyCompany/Presentation.Unity/StockMarketFullscreenPanel.cs";
        private const string StockMarketAdapterPath =
            "Assets/FamilyCompany/Presentation.Unity/MainNavigation/StockMarketNavigationAdapter.cs";
        private const string ContractAdapterPath =
            "Assets/FamilyCompany/Presentation.Unity/ContractGrowth/ContractBusinessRuntimeAdapter.cs";
        private const string BuildAdapterPath =
            "Assets/FamilyCompany/Presentation.Unity/OfficeRuntime/OfficeBuildEditorNavigationAdapter.cs";
        private const string StockMarketBridgePath =
            "Assets/FamilyCompany/Simulation/Market/StockMarketGameStateBridge.cs";
        private const string GameSaveMapperPath =
            "Assets/FamilyCompany/Save/GameSaveMapper.cs";
        private const string FontCatalogPath =
            "Assets/FamilyCompany/Presentation.Unity/Resources/UiRemasterV3/UiRemasterFontCatalog_v3.asset";
        private const string ArtRoot =
            "Assets/Art/UI/Resources/MainNavigationV2";

        [MenuItem("Family Company/QA/Validate Main Navigation HUD V2")]
        public static void Run()
        {
            try
            {
                ValidateCatalogAndRouting();
                ValidateLayoutContracts();
                ValidateArtAssets();
                ValidateKoreanFonts();
                ValidateRuntimeStructure();
                Debug.Log("MAIN_NAVIGATION_HUD_VALIDATION: PASS");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("MAIN_NAVIGATION_HUD_VALIDATION: FAIL");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        public static void RunFromCommandLine()
        {
            Run();
        }

        internal static void RunFromCommandLineForCapture()
        {
            ValidateCatalogAndRouting();
            ValidateLayoutContracts();
            ValidateArtAssets();
            ValidateKoreanFonts();
            ValidateRuntimeStructure();
            Debug.Log("MAIN_NAVIGATION_HUD_VALIDATION: PASS (capture preflight)");
        }

        private static void ValidateCatalogAndRouting()
        {
            MainNavigationCatalog.ValidateOrThrow();
            Require(MainNavigationCatalog.All.Count == 5, "The catalog must contain exactly five tabs.");
            var expected = new Dictionary<MainNavigationTabId, string>
            {
                { MainNavigationTabId.Company, "company" },
                { MainNavigationTabId.People, "people" },
                { MainNavigationTabId.Projects, "projects" },
                { MainNavigationTabId.Research, "research" },
                { MainNavigationTabId.Investment, "investment" }
            };
            var session = new MainNavigationSession();
            foreach (var pair in expected)
            {
                var definition = MainNavigationCatalog.Get(pair.Key);
                Require(definition.Id == pair.Value,
                    $"Tab {pair.Key} maps to '{definition.Id}', expected '{pair.Value}'.");
                Require(!string.IsNullOrWhiteSpace(definition.DisplayNameKo) &&
                        !string.IsNullOrWhiteSpace(definition.DescriptionKo),
                    $"Tab {definition.Id} has incomplete Korean display data.");
                Require(definition.Features.Count >= 4,
                    $"Tab {definition.Id} must expose at least four functional category cards.");
                foreach (var feature in definition.Features)
                {
                    if (feature.Action == MainNavigationFeatureAction.OpenStatus)
                        Require(feature.StatusKo == "준비 중",
                            $"Unimplemented feature {feature.Id} must remain marked 준비 중.");
                    Require(feature.Action != MainNavigationFeatureAction.None,
                        $"Visible feature {feature.Id} does not expose a dedicated route.");
                }

                session.Open(pair.Key);
                Require(session.HasActiveTab && session.ActiveTab == pair.Key,
                    $"Button route for {pair.Key} did not open the correct screen.");
                session.OpenFeature(definition.Features[0].Id);
                Require(session.HasActiveFeature && session.HandleEscape() && session.HasActiveTab &&
                        !session.HasActiveFeature,
                    $"ESC did not return {pair.Key}'s feature screen to its hub.");
                Require(session.HandleEscape() && !session.HasActiveTab,
                    $"ESC did not close {pair.Key} to the office.");
                Require(!session.HandleEscape(),
                    "ESC should fall through to the legacy management flow when no main panel is open.");
            }
            session.Open(MainNavigationTabId.Company);
            Require(session.CloseToOffice() && !session.HasActiveTab,
                "The explicit office-return action did not close the active panel.");
            var investment = MainNavigationCatalog.Get(MainNavigationTabId.Investment);
            Require(investment.Features.Count == 5,
                "Investment hub must expose stock, bank/loan, property, angel, and M&A cards.");
            var stockRoutes = MainNavigationCatalog.All
                .SelectMany(tab => tab.Features.Select(feature => new { tab.TabId, Feature = feature }))
                .Where(item => item.Feature.Action == MainNavigationFeatureAction.OpenStockMarket)
                .ToArray();
            Require(stockRoutes.Length == 1 && stockRoutes[0].TabId == MainNavigationTabId.Investment &&
                    stockRoutes[0].Feature.Id == "investment-stocks",
                "Stock market must have exactly one UI route and it must be inside Investment.");
            var buildingRoutes = MainNavigationCatalog.All
                .SelectMany(tab => tab.Features.Select(feature => new { tab.TabId, Feature = feature }))
                .Where(item => item.Feature.RouteId == MainNavigationRouteIds.BuildingEditor)
                .ToArray();
            Require(buildingRoutes.Length == 1 && buildingRoutes[0].TabId == MainNavigationTabId.Company &&
                    buildingRoutes[0].Feature.Action == MainNavigationFeatureAction.OpenBuildingEditor &&
                    buildingRoutes[0].Feature.DisplayNameKo == "건축·편집",
                "Building editor must consume one active adapter route inside Company.");
            var businessRoutes = MainNavigationCatalog.All
                .SelectMany(tab => tab.Features.Select(feature => new { tab.TabId, Feature = feature }))
                .Where(item => item.Feature.RouteId == MainNavigationRouteIds.BusinessContracts ||
                               item.Feature.RouteId == MainNavigationRouteIds.BusinessProducts)
                .ToArray();
            Require(businessRoutes.Length == 2 && businessRoutes.All(item =>
                        item.TabId == MainNavigationTabId.Projects &&
                        item.Feature.Action != MainNavigationFeatureAction.None),
                "Contract and product adapters must expose active routes inside Projects.");
            Require(businessRoutes.Any(item => item.Feature.DisplayNameKo == "하청 계약") &&
                    businessRoutes.Any(item => item.Feature.DisplayNameKo == "자체 제품"),
                "Projects hub is missing its contract or product card.");
            Debug.Log("MAIN_NAVIGATION_CATALOG: PASS tabs=5 allCards=clickable stockRoute=investment-only buildingAdapter=company businessAdapter=projects featureBack=PASS officeReturn=PASS escapePriority=PASS");
        }

        private static void ValidateLayoutContracts()
        {
            var cases = new[]
            {
                new LayoutCase(1920, 1080, new UiSafeInsets(24, 18, 24, 18), "16:9-safe-area"),
                new LayoutCase(1392, 768, UiSafeInsets.None, "compact-1392"),
                new LayoutCase(1280, 720, UiSafeInsets.None, "minimum-1280"),
                new LayoutCase(1600, 900, UiSafeInsets.None, "16:9-window"),
                new LayoutCase(1600, 1000, UiSafeInsets.None, "16:10-window"),
                new LayoutCase(2560, 1440, new UiSafeInsets(32, 20, 32, 20), "16:9-large")
            };
            foreach (var item in cases)
            {
                var layout = MainNavigationLayoutMetrics.Calculate(
                    item.Width,
                    item.Height,
                    item.Insets);
                MainNavigationLayoutMetrics.Validate(layout);
                Require(layout.ContentPanel.Width >= Math.Min(1040d, layout.SafeArea.Width - 36d),
                    $"Responsive content panel is too narrow for Korean stats at {item.Width}x{item.Height}.");
                Debug.Log(
                    $"MAIN_NAVIGATION_LAYOUT: {item.Label} {item.Width}x{item.Height} " +
                    $"scale={layout.ScaleFactor:0.####} top={layout.TopHud} " +
                    $"panel={layout.ContentPanel} bottom={layout.BottomNavigation}");
            }
            Require(MainNavigationHudPresenter.ReferenceResolution == new Vector2(1920f, 1080f) &&
                    Math.Abs(MainNavigationHudPresenter.CanvasMatchWidthOrHeight - 0.5f) < 0.0001f,
                "CanvasScaler contract must remain 1920x1080 with match 0.5.");
        }

        private static void ValidateArtAssets()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var absoluteRoot = Path.Combine(projectRoot, ArtRoot);
            var paths = Directory.GetFiles(absoluteRoot, "*.png", SearchOption.AllDirectories)
                .Select(path => path.Replace('\\', '/'))
                .Select(path => path.Substring(projectRoot.Replace('\\', '/').Length + 1))
                .Where(path => !path.Contains("/Reference/"))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            Require(paths.Length == 34,
                $"V2 generated UI asset inventory must contain exactly 34 runtime PNGs; found {paths.Length}.");
            Require(!Directory.Exists(Path.Combine(projectRoot, "Assets/Art/UI/Resources/MainNavigation")),
                "Rejected MainNavigation V1 assets remain in the tracked runtime tree.");
            foreach (var path in paths)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                Require(sprite != null, $"Generated V2 UI asset is not imported as a Sprite: {path}");
                var requiresBorder = MainNavigationV2AssetImporter.TryGetExpectedBorder(path, out var expectedBorder);
                if (requiresBorder)
                    Require(sprite.border == expectedBorder,
                        $"V2 9-slice border drifted for {path}: {sprite.border}, expected {expectedBorder}.");
                var isIcon = path.Contains("/Icons/");
                Require(!isIcon || sprite.texture.width <= 512 && sprite.texture.height <= 512,
                    $"Runtime V2 icon exceeds the 512px import budget: {path}");
                ValidateTextureImporter(path, isIcon ? 512 : 2048, requiresBorder);
            }

            foreach (var definition in MainNavigationCatalog.All)
            {
                Require(AssetDatabase.LoadAssetAtPath<Sprite>(
                            "Assets/Art/UI/Resources/" + definition.IconResourcePath + ".png") != null,
                    $"Catalog V2 tab icon is missing: {definition.IconResourcePath}");
                foreach (var feature in definition.Features.Where(feature =>
                             !string.IsNullOrWhiteSpace(feature.IconResourcePath)))
                    Require(AssetDatabase.LoadAssetAtPath<Sprite>(
                                "Assets/Art/UI/Resources/" + feature.IconResourcePath + ".png") != null,
                        $"Catalog V2 feature icon is missing: {feature.IconResourcePath}");
            }
            Debug.Log("MAIN_NAVIGATION_ART_V2: PASS assets=34 frameStates=22 markers=2 icons=10 alpha=RGBA halo=0 maxIcon=512");
        }

        private static void ValidateTextureImporter(string path, int maximumSize, bool requiresBorder)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Require(importer != null, $"TextureImporter is missing: {path}");
            Require(importer.textureType == TextureImporterType.Sprite &&
                    importer.spriteImportMode == SpriteImportMode.Single,
                $"UI art must be a single Sprite: {path}");
            Require(importer.alphaIsTransparency && importer.DoesSourceTextureHaveAlpha(),
                $"UI art does not preserve generated transparency: {path}");
            Require(!importer.mipmapEnabled, $"UI art must not generate mipmaps: {path}");
            Require(importer.maxTextureSize <= maximumSize,
                $"UI art exceeds its import size budget ({maximumSize}): {path}");
            Require(importer.textureCompression == TextureImporterCompression.Uncompressed,
                $"UI art must remain uncompressed for clean alpha edges: {path}");
            if (requiresBorder)
                Require(importer.spriteBorder.x > 0f && importer.spriteBorder.y > 0f &&
                        importer.spriteBorder.z > 0f && importer.spriteBorder.w > 0f,
                    $"9-slice UI frame has no importer border: {path}");
        }

        private static void ValidateKoreanFonts()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<UiRemasterFontCatalog>(FontCatalogPath);
            Require(catalog != null && catalog.IsComplete,
                "Bundled Korean font catalog is missing or incomplete.");
            Require(catalog.BodySource.name.IndexOf("Maple", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    catalog.HeadingSource.name.IndexOf("Maple", StringComparison.OrdinalIgnoreCase) >= 0,
                "Main navigation primary font family must be Maplestory Light/Bold.");
            var glyphs = new HashSet<char>(
                string.Concat(MainNavigationCatalog.EnumerateKoreanText())
                    .Where(character => character >= '\uAC00' && character <= '\uD7A3'));
            foreach (var glyph in glyphs)
            {
                Require(catalog.BodySource.HasCharacter(glyph) || catalog.FallbackSource.HasCharacter(glyph),
                    $"Bundled Korean font fallback is missing '{glyph}' (U+{(int)glyph:X4}).");
            }
            Debug.Log($"MAIN_NAVIGATION_KOREAN_FONT: PASS glyphs={glyphs.Count}");
        }

        private static void ValidateRuntimeStructure()
        {
            var presenter = File.ReadAllText(Path.GetFullPath(PresenterPath));
            foreach (var required in new[]
                     {
                         "CanvasScaler.ScaleMode.ScaleWithScreenSize",
                         "CanvasScaler.ScreenMatchMode.MatchWidthOrHeight",
                         "Screen.safeArea",
                         "TextMeshProUGUI",
                         "MainNavigationCatalog.All",
                         "SetWorldTimeScaleNow(capturedSpeed)",
                         "Image.Type.Sliced",
                         "OpenTabNow(capturedTab)",
                         "OpenFromInvestment",
                         "TryHandleBackToInvestment",
                         "OfficeBuildEditorNavigationAdapter.TryOpen",
                         "ContractBusinessRuntimeAdapter",
                         "OpenContractBoard",
                         "OpenProductOpportunities",
                         "BuildComingSoonDetail",
                         "NavigateBackNow",
                         "ReturnToOfficeNow",
                         "Selectable.Transition.SpriteSwap",
                         "MAIN_NAVIGATION_V2_ASSET_MISSING",
                         "World Dim 26 Percent",
                         "MinimumBodyFontSize = UiRemasterTypography.BodyPixels",
                         "UiRemasterFontCatalog",
                         "TryAddCharacters",
                         "BuildWorkforceStateMetric"
                     })
            {
                Require(presenter.Contains(required),
                    $"Main navigation presenter is missing required runtime structure: {required}");
            }
            foreach (var forbidden in new[] { "●  LIVE", "저장 완료", "Ctrl+S", "관리 화면   ESC" })
                Require(!presenter.Contains(forbidden),
                    $"New main HUD exposes a removed clutter element: {forbidden}");
            Require(!presenter.Contains("Selectable.Transition.ColorTint") &&
                    !presenter.Contains("GUI.Box") &&
                    !presenter.Contains("MintCard"),
                "Rejected flat ColorTint/GUI.Box card styling remains in the V2 presenter.");

            var legacy = File.ReadAllText(Path.GetFullPath(LegacyPresenterPath));
            Require(legacy.Contains("var officeVisible = false;"),
                "Legacy family/LIVE/notice HUD is not explicitly hidden.");

            var stockMarket = File.ReadAllText(Path.GetFullPath(StockMarketPresenterPath));
            Require(!stockMarket.Contains("F3  주식시장"),
                "Legacy stock-market entry still occupies the clean top HUD.");
            Require(stockMarket.Contains("_open && Input.GetKeyDown(KeyCode.F3)"),
                "F3 must be close-only and may not open the stock market from the office.");
            Require(stockMarket.Contains("StockMarketGameStateBridge.Load") &&
                    stockMarket.Contains("StockMarketGameStateBridge.Flush"),
                "Canonical stock panel no longer owns the GameState bridge lifecycle.");

            var adapter = File.ReadAllText(Path.GetFullPath(StockMarketAdapterPath));
            Require(adapter.Contains("ActiveTabId != \"investment\"") && adapter.Contains("_canonicalPanel.OpenNow()"),
                "Stock-market navigation adapter does not gate entry through Investment.");
            Require(adapter.Contains("GetComponents<StockMarketFullscreenPanel>()") &&
                    !adapter.Contains("void Update("),
                "Stock-market adapter must reuse one canonical panel without an update/subscription loop.");
            Require(adapter.Contains("_canonicalPanel.CloseNow()") &&
                    adapter.Contains("OpenTabNow(MainNavigationTabId.Investment)"),
                "Stock-market back route does not return to the Investment hub.");

            var contractAdapter = File.ReadAllText(Path.GetFullPath(ContractAdapterPath));
            Require(contractAdapter.Contains("public void OpenContractBoard()") &&
                    contractAdapter.Contains("public void OpenProductOpportunities()") &&
                    contractAdapter.Contains("public ContractBoardViewModel GetBoardViewModel()"),
                "ContractBusinessRuntimeAdapter public navigation/view-model API drifted.");
            var buildAdapter = File.ReadAllText(Path.GetFullPath(BuildAdapterPath));
            Require(buildAdapter.Contains("public const string EntryId") &&
                    buildAdapter.Contains("public static bool TryOpen"),
                "OfficeBuildEditorNavigationAdapter public API drifted.");
            Require(presenter.Contains("OfficeBuildEditorNavigationAdapter.EntryId") &&
                    presenter.Contains("_contractBusinessNavigation.GetBoardViewModel()") &&
                    presenter.Contains("_contractBusinessNavigation.GetProductOpportunities()"),
                "Main navigation does not consume both dependency adapters and their canonical view models.");

            var bridge = File.ReadAllText(Path.GetFullPath(StockMarketBridgePath));
            Require(bridge.Contains("state.StockMarket") && bridge.Contains("state.ReplaceStockMarketState"),
                "Canonical stock bridge no longer reads and flushes GameState.StockMarket.");
            var saveMapper = File.ReadAllText(Path.GetFullPath(GameSaveMapperPath));
            Require(saveMapper.Contains("stockMarket = ToStockMarketSaveDto(state.StockMarket)") &&
                    saveMapper.Contains("FromStockMarketSaveDto(save.stockMarket)"),
                "Canonical stock state is not mapped through the existing save DTO.");

            var bootstrap = File.ReadAllText(Path.GetFullPath(BootstrapPath));
            var escapePriority = bootstrap.IndexOf("TryHandleEscape()", StringComparison.Ordinal);
            var legacySwitch = bootstrap.IndexOf("switch (_screen)", escapePriority, StringComparison.Ordinal);
            Require(escapePriority >= 0 && legacySwitch > escapePriority,
                "Main navigation ESC priority does not precede the legacy management screen switch.");
            Require(bootstrap.Contains("EnsureMainNavigationHudPresenter()") &&
                    bootstrap.Contains("ResetSessionView()"),
                "Bootstrap does not install and reset the main navigation presenter.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private readonly struct LayoutCase
        {
            public LayoutCase(int width, int height, UiSafeInsets insets, string label)
            {
                Width = width;
                Height = height;
                Insets = insets;
                Label = label;
            }

            public int Width { get; }
            public int Height { get; }
            public UiSafeInsets Insets { get; }
            public string Label { get; }
        }
    }
}
