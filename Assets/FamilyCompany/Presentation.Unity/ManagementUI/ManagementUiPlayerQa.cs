using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FamilyCompany.Simulation.ManagementUi;
using FamilyCompany.Simulation.Prototype;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FamilyCompany.Presentation.Unity.ManagementUI
{
    /// <summary>
    /// Release-player-only visual QA. It is inert unless its explicit command-line flag is present.
    /// </summary>
    public sealed class ManagementUiPlayerQa : MonoBehaviour
    {
        public const string EnableArgument = "-familyCompanyManagementUiQa";
        public const string OutputArgument = "-familyCompanyManagementUiQaOutput";
        public const string Gate1080OnlyArgument = "-familyCompanyManagementUiQa1080Only";
        public const string Gate1440OnlyArgument = "-familyCompanyManagementUiQa1440Only";

        private string _outputFolder;
        private string _reportPath;
        private int _missingGlyphLogs;
        private int _exceptionLogs;
        private int _duplicateComponentLogs;
        private int _captures;
        private FrameStats _managementOpenTransition;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallForQa()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), EnableArgument) < 0) return;
            var host = new GameObject("~ManagementUiPlayerQa");
            DontDestroyOnLoad(host);
            host.AddComponent<ManagementUiPlayerQa>();
        }

        private void Awake()
        {
            Application.logMessageReceived += HandleLog;
        }

        private void Start()
        {
            Application.runInBackground = true;
            _outputFolder = ReadArgument(OutputArgument);
            if (string.IsNullOrWhiteSpace(_outputFolder))
                _outputFolder = Path.GetFullPath("Artifacts/ManagementUiQa");
            _outputFolder = Path.GetFullPath(_outputFolder);
            Directory.CreateDirectory(_outputFolder);
            foreach (var path in Directory.GetFiles(_outputFolder, "*.png", SearchOption.TopDirectoryOnly))
                File.Delete(path);
            _reportPath = Path.Combine(_outputFolder, "management-ui-player-qa.txt");
            File.WriteAllText(
                _reportPath,
                $"Management UI P0 Release D3D11 QA | {DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}",
                System.Text.Encoding.UTF8);
            StartCoroutine(RunSafely());
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= HandleLog;
        }

        private IEnumerator RunSafely()
        {
            var routine = RunQa();
            while (true)
            {
                object current;
                try
                {
                    if (!routine.MoveNext()) break;
                    current = routine.Current;
                }
                catch (Exception exception)
                {
                    Append("PLAYER_QA_FAIL | " + exception);
                    Debug.LogException(exception);
                    Application.Quit(1);
                    yield break;
                }
                yield return current;
            }

            Require(_exceptionLogs == 0, "Player log contains exceptions: " + _exceptionLogs);
            Require(_duplicateComponentLogs == 0, "Player log contains duplicate-component diagnostics: " + _duplicateComponentLogs);
            Append($"PLAYER_QA_PASS | captures={_captures} missingGlyphLogs={_missingGlyphLogs} " +
                   $"exceptions={_exceptionLogs} duplicateComponents={_duplicateComponentLogs} " +
                   "flows=new-game>contract>progress>office,save,load,speed observationGuide=C f8Showcase=untouched");
            yield return new WaitForSecondsRealtime(0.25f);
            Application.Quit(0);
        }

        private IEnumerator RunQa()
        {
            var arguments = Environment.GetCommandLineArgs();
            var gate1080Only = Array.IndexOf(arguments, Gate1080OnlyArgument) >= 0;
            var gate1440Only = Array.IndexOf(arguments, Gate1440OnlyArgument) >= 0;
            Require(!gate1080Only || !gate1440Only, "1080-only and 1440-only QA gates are mutually exclusive.");
            Append($"PLAYER_START | graphics={SystemInfo.graphicsDeviceType} device={SystemInfo.graphicsDeviceName}");
            Require(SystemInfo.graphicsDeviceType.ToString().IndexOf("Direct3D11", StringComparison.OrdinalIgnoreCase) >= 0,
                "QA player is not running on D3D11: " + SystemInfo.graphicsDeviceType);

            Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
            yield return WaitForResolution(1920, 1080, 15f);

            PrototypeBootstrap bootstrap = null;
            ManagementUiV2Presenter presenter = null;
            var deadline = Time.realtimeSinceStartup + 30f;
            while (Time.realtimeSinceStartup < deadline)
            {
                bootstrap = FindFirstObjectByType<PrototypeBootstrap>();
                presenter = FindFirstObjectByType<ManagementUiV2Presenter>();
                if (bootstrap != null && presenter != null) break;
                yield return null;
            }
            Require(bootstrap != null, "PrototypeBootstrap is missing in the QA player.");
            Require(presenter != null, "ManagementUiV2Presenter is missing in the QA player.");

            bootstrap.StartNewGameNow(1, false);
            bootstrap.SetWorldTimeScaleNow(1f);
            yield return MeasureTransitionFrames(
                "new-game-loading",
                () => !ScenePreviewJump.IsPresentationLoading,
                45f,
                30,
                null);
            Require(!ScenePreviewJump.IsPresentationLoading, "Starter office did not finish loading within 45 seconds.");
            Require(presenter.IsManagementDataPrewarmedForCurrentStateForQa,
                "Management UI prewarm does not match the current GameState.");
            Require(presenter.IsManagementPrewarmHiddenForQa,
                "Management UI prewarm was not active-hidden with input/raycast disabled.");
            Require(bootstrap.State.Contracts.Contracts.Count == 0,
                "Loading prewarm executed a contract button event.");
            Require(bootstrap.State.Company.CashWon == PrototypeStateFactory.StartingCapitalWon,
                "Loading prewarm mutated starting cash.");
            var initialButtonTreeCount = presenter.GetManagementRootForQa().GetComponentsInChildren<Button>(true).Length;
            var initialTextTreeCount = presenter.GetManagementRootForQa().GetComponentsInChildren<TMP_Text>(true).Length;
            var initialFontAssetCount = CountManagementRuntimeFontAssets();
            var initialFontAtlasCount = CountManagementRuntimeFontAtlases();
            var initialCanvasCount = presenter.ManagementCanvasCountForQa;
            Require(initialCanvasCount == 1, "Management prewarm must own exactly one Canvas root; found " + initialCanvasCount);
            Require(presenter.ManagementButtonListenerHostCountForQa == initialButtonTreeCount,
                "Management listener-host count differs from the button tree count after prewarm.");
            bootstrap.ShowManagementNow();
            yield return MeasureTransitionFrames(
                "management-open",
                () => presenter.IsManagementVisibleForQa,
                15f,
                90,
                stats => _managementOpenTransition = stats);
            Require(_managementOpenTransition.MaximumMilliseconds < 50f,
                $"Management-open transition contains a 50ms frame: {_managementOpenTransition.MaximumMilliseconds:0.###}ms");
            Require(_managementOpenTransition.FramesAtOrAbove50Milliseconds == 0,
                "Management-open transition contains frames at or above 50ms.");

            if (!gate1440Only)
            {
                yield return ValidateBidirectionalTransitions(
                    bootstrap,
                    presenter,
                    initialButtonTreeCount,
                    initialTextTreeCount,
                    initialFontAssetCount,
                    initialFontAtlasCount,
                    initialCanvasCount,
                    "initial");
            }

            if (!gate1440Only)
            {
                yield return CaptureAndValidate(presenter, "management-1920x1080.png", 1920, 1080);
                yield return CaptureGlyphProof(presenter, "glyph-proof-1920x1080.png", 1920, 1080);
                ValidateRoutes(bootstrap, presenter);
                yield return WaitForManagementRoot(presenter, 5f);
                yield return ValidateSessionRecycle(
                    bootstrap,
                    presenter,
                    initialButtonTreeCount,
                    initialTextTreeCount,
                    initialFontAssetCount,
                    initialFontAtlasCount,
                    initialCanvasCount);
                Require(presenter.MissingGlyphCountForQa == 0, "Presenter reported missing glyphs at the 1080p gate.");
                Require(_missingGlyphLogs == 0, "Player log contains missing-glyph/tofu diagnostics at the 1080p gate.");
                Append("R2_1080_GATE_PASS | reviewerTargets=offer-spread,text-safe,family-title,button-surface,glyphs");
                if (gate1080Only) yield break;
            }

            // This QA machine has a 1920x1080 physical display. Render the canonical
            // 2560x1440 target from a 1280x720 16:9 window with Unity supersampling.
            Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
            yield return WaitForResolution(1280, 720, 15f);
            bootstrap.ShowManagementNow();
            yield return WaitForManagementRoot(presenter, 5f);
            yield return CaptureAndValidate(presenter, "management-2560x1440.png", 2560, 1440, 2);
            yield return CaptureGlyphProof(presenter, "glyph-proof-2560x1440.png", 2560, 1440, 2);

            Require(presenter.MissingGlyphCountForQa == 0, "Presenter reported missing glyphs.");
            Require(_missingGlyphLogs == 0, "Player log contains missing-glyph/tofu diagnostics.");
            Append("R2_1440_GATE_PASS | reviewerTargets=offer-spread,text-safe,family-title,button-surface,glyphs");
        }

        private IEnumerator ValidateBidirectionalTransitions(
            PrototypeBootstrap bootstrap,
            ManagementUiV2Presenter presenter,
            int expectedButtonTreeCount,
            int expectedTextTreeCount,
            int expectedFontAssetCount,
            int expectedFontAtlasCount,
            int expectedCanvasCount,
            string phase)
        {
            var root = presenter.GetManagementRootForQa();
            Require(root != null && presenter.IsManagementVisibleForQa,
                "Bidirectional transition gate requires a visible, interactive management root.");
            Require(FindTextContaining(root, "사무실 보기") != null && FindTextContaining(root, "ESC") != null,
                "Office-view button or shortcut guidance is missing from management UI.");

            FrameStats buttonClose = default;
            yield return MeasureTriggeredTransitionFrames(
                $"management-to-office-button-{phase}",
                () => Click(FindButton(root, "사무실 보기")),
                () => bootstrap.UiScreen == PrototypeUiScreen.Playing && presenter.IsManagementPrewarmHiddenForQa,
                15f,
                30,
                stats => buttonClose = stats);
            Require(buttonClose.FramesAtOrAbove50Milliseconds == 0,
                $"Management-to-office button transition has a >=50ms frame: {buttonClose.MaximumMilliseconds:0.###}ms");
            AssertStableRuntimeTree(presenter, expectedButtonTreeCount, expectedTextTreeCount,
                expectedFontAssetCount, expectedFontAtlasCount, expectedCanvasCount, phase + "-button-close");

            FrameStats shortcutOpen = default;
            yield return MeasureTriggeredTransitionFrames(
                $"office-to-management-shortcut-{phase}",
                bootstrap.ShowManagementNow,
                () => bootstrap.UiScreen == PrototypeUiScreen.Management && presenter.IsManagementVisibleForQa,
                15f,
                30,
                stats => shortcutOpen = stats);
            Require(shortcutOpen.FramesAtOrAbove50Milliseconds == 0,
                $"Office-to-management shortcut transition has a >=50ms frame: {shortcutOpen.MaximumMilliseconds:0.###}ms");
            AssertStableRuntimeTree(presenter, expectedButtonTreeCount, expectedTextTreeCount,
                expectedFontAssetCount, expectedFontAtlasCount, expectedCanvasCount, phase + "-shortcut-open");

            FrameStats shortcutClose = default;
            yield return MeasureTriggeredTransitionFrames(
                $"management-to-office-shortcut-{phase}",
                bootstrap.CloseManagementNow,
                () => bootstrap.UiScreen == PrototypeUiScreen.Playing && presenter.IsManagementPrewarmHiddenForQa,
                15f,
                30,
                stats => shortcutClose = stats);
            Require(shortcutClose.FramesAtOrAbove50Milliseconds == 0,
                $"Management-to-office shortcut transition has a >=50ms frame: {shortcutClose.MaximumMilliseconds:0.###}ms");
            AssertStableRuntimeTree(presenter, expectedButtonTreeCount, expectedTextTreeCount,
                expectedFontAssetCount, expectedFontAtlasCount, expectedCanvasCount, phase + "-shortcut-close");

            FrameStats buttonPathRestore = default;
            yield return MeasureTriggeredTransitionFrames(
                $"office-to-management-button-path-{phase}",
                bootstrap.ShowManagementNow,
                () => bootstrap.UiScreen == PrototypeUiScreen.Management && presenter.IsManagementVisibleForQa,
                15f,
                30,
                stats => buttonPathRestore = stats);
            Require(buttonPathRestore.FramesAtOrAbove50Milliseconds == 0,
                $"Office-to-management restore transition has a >=50ms frame: {buttonPathRestore.MaximumMilliseconds:0.###}ms");
            AssertStableRuntimeTree(presenter, expectedButtonTreeCount, expectedTextTreeCount,
                expectedFontAssetCount, expectedFontAtlasCount, expectedCanvasCount, phase + "-restored");
            Require(_exceptionLogs == 0 && _duplicateComponentLogs == 0,
                $"Bidirectional transition phase {phase} emitted an exception or duplicate-component error.");
            Append($"BIDIRECTIONAL_TRANSITION_PASS | phase={phase} buttonPath=1 shortcutPath=1 " +
                   $"roots={expectedCanvasCount} listenerHosts={expectedButtonTreeCount} fontAssets={expectedFontAssetCount} " +
                   $"fontAtlases={expectedFontAtlasCount} accumulated=0 exceptions=0 framesAtOrAbove50Ms=0");
        }

        private IEnumerator MeasureTriggeredTransitionFrames(
            string label,
            Action trigger,
            Func<bool> completed,
            float timeoutSeconds,
            int settledFrameCount,
            Action<FrameStats> setResult)
        {
            yield return null;
            trigger();
            yield return MeasureTransitionFrames(label, completed, timeoutSeconds, settledFrameCount, setResult);
        }

        private IEnumerator MeasureTransitionFrames(
            string label,
            Func<bool> completed,
            float timeoutSeconds,
            int settledFrameCount,
            Action<FrameStats> setResult)
        {
            var samples = new List<float>();
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            var startedAt = Time.realtimeSinceStartup;
            do
            {
                yield return null;
                samples.Add(Time.unscaledDeltaTime * 1000f);
            } while (!completed() && Time.realtimeSinceStartup < deadline);
            Require(completed(), label + " did not complete before its timeout.");
            var completionMilliseconds = (Time.realtimeSinceStartup - startedAt) * 1000f;
            for (var frame = 0; frame < settledFrameCount; frame++)
            {
                yield return null;
                samples.Add(Time.unscaledDeltaTime * 1000f);
            }
            samples.Sort();
            var stats = new FrameStats(
                samples.Count,
                Percentile(samples, 0.95f),
                Percentile(samples, 0.99f),
                samples[samples.Count - 1],
                samples.FindAll(value => value >= 50f).Count,
                completionMilliseconds);
            setResult?.Invoke(stats);
            Append(
                $"TRANSITION_FRAME_TIME | label={label} frames={stats.SampleCount} " +
                $"completionMs={stats.CompletionMilliseconds:0.###} " +
                $"p95Ms={stats.P95Milliseconds:0.###} p99Ms={stats.P99Milliseconds:0.###} " +
                $"maxMs={stats.MaximumMilliseconds:0.###} framesAtOrAbove50Ms={stats.FramesAtOrAbove50Milliseconds}");
        }

        private IEnumerator ValidateSessionRecycle(
            PrototypeBootstrap bootstrap,
            ManagementUiV2Presenter presenter,
            int expectedButtonTreeCount,
            int expectedTextTreeCount,
            int expectedFontAssetCount,
            int expectedFontAtlasCount,
            int expectedCanvasCount)
        {
            var previousState = bootstrap.State;
            bootstrap.StartNewGameNow(1, false);
            bootstrap.SetWorldTimeScaleNow(1f);
            Require(!ReferenceEquals(previousState, bootstrap.State),
                "Starting a replacement session reused the previous GameState instance.");
            yield return MeasureTransitionFrames(
                "session-restart-loading",
                () => !ScenePreviewJump.IsPresentationLoading,
                45f,
                30,
                null);
            var root = presenter.GetManagementRootForQa();
            Require(root != null && presenter.IsManagementPrewarmHiddenForQa,
                "Replacement-session prewarm did not preserve active-hidden non-interactive state.");
            Require(presenter.IsManagementDataPrewarmedForCurrentStateForQa,
                "Replacement-session prewarm retained a stale GameState reference.");
            Require(bootstrap.State.Contracts.Contracts.Count == 0,
                "Replacement-session prewarm executed a contract listener.");
            Require(bootstrap.State.Company.CashWon == PrototypeStateFactory.StartingCapitalWon,
                "Replacement-session prewarm mutated starting cash.");
            Require(root.GetComponentsInChildren<Button>(true).Length == expectedButtonTreeCount,
                "Replacement-session prewarm accumulated duplicate buttons/listeners.");
            Require(root.GetComponentsInChildren<TMP_Text>(true).Length == expectedTextTreeCount,
                "Replacement-session prewarm accumulated duplicate UI text objects.");
            Require(CountManagementRuntimeFontAssets() == expectedFontAssetCount,
                "Replacement-session prewarm accumulated runtime TMP font assets/atlases.");
            Require(CountManagementRuntimeFontAtlases() == expectedFontAtlasCount,
                "Replacement-session prewarm accumulated runtime TMP atlas textures.");
            Require(presenter.ManagementCanvasCountForQa == expectedCanvasCount,
                "Replacement-session prewarm accumulated management Canvas roots.");
            Require(presenter.ManagementButtonListenerHostCountForQa == expectedButtonTreeCount,
                "Replacement-session prewarm accumulated listener hosts.");

            bootstrap.ShowManagementNow();
            FrameStats reopened = default;
            yield return MeasureTransitionFrames(
                "management-open-after-state-replace",
                () => presenter.IsManagementVisibleForQa,
                15f,
                90,
                stats => reopened = stats);
            Require(reopened.MaximumMilliseconds < 50f,
                $"Replacement-session management open contains a 50ms frame: {reopened.MaximumMilliseconds:0.###}ms");
            Require(reopened.FramesAtOrAbove50Milliseconds == 0,
                "Replacement-session management open contains frames at or above 50ms.");
            Require(root.GetComponentsInChildren<Button>(true).Length == expectedButtonTreeCount,
                "Showing the prewarmed tree accumulated duplicate controls.");
            AssertStableRuntimeTree(presenter, expectedButtonTreeCount, expectedTextTreeCount,
                expectedFontAssetCount, expectedFontAtlasCount, expectedCanvasCount, "replacement-session-show");
            var accept = FindButton(root, "계약 검토 후 수락");
            Click(accept);
            Require(bootstrap.State.Contracts.Contracts.Count == 1,
                "A single prewarmed contract click did not execute exactly once.");
            Append(
                $"SESSION_RECYCLE_PASS | staleState=0 hiddenDuringLoading=1 prewarmEvents=0 " +
                $"buttons={expectedButtonTreeCount} texts={expectedTextTreeCount} runtimeFonts={expectedFontAssetCount} " +
                $"fontAtlases={expectedFontAtlasCount} roots={expectedCanvasCount} listenerGrowth=0 " +
                $"duplicateObjects=0 duplicateClickEffects=0 atlasGrowth=0 exceptions=0");
        }

        private static int CountManagementRuntimeFontAssets()
        {
            var count = 0;
            foreach (var font in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                if (font != null && font.name.StartsWith("Management UI ", StringComparison.Ordinal)) count++;
            return count;
        }

        private static int CountManagementRuntimeFontAtlases()
        {
            var count = 0;
            foreach (var font in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
            {
                if (font == null || !font.name.StartsWith("Management UI ", StringComparison.Ordinal)) continue;
                count += font.atlasTextures != null ? font.atlasTextures.Length : 0;
            }
            return count;
        }

        private static void AssertStableRuntimeTree(
            ManagementUiV2Presenter presenter,
            int expectedButtonTreeCount,
            int expectedTextTreeCount,
            int expectedFontAssetCount,
            int expectedFontAtlasCount,
            int expectedCanvasCount,
            string phase)
        {
            var root = presenter.GetManagementRootForQa();
            Require(root.GetComponentsInChildren<Button>(true).Length == expectedButtonTreeCount,
                phase + " accumulated button/listener hosts.");
            Require(root.GetComponentsInChildren<TMP_Text>(true).Length == expectedTextTreeCount,
                phase + " accumulated text objects.");
            Require(presenter.ManagementButtonListenerHostCountForQa == expectedButtonTreeCount,
                phase + " listener-host count drifted.");
            Require(presenter.ManagementCanvasCountForQa == expectedCanvasCount,
                phase + " Canvas root count drifted.");
            Require(CountManagementRuntimeFontAssets() == expectedFontAssetCount,
                phase + " runtime font asset count drifted.");
            Require(CountManagementRuntimeFontAtlases() == expectedFontAtlasCount,
                phase + " runtime font atlas count drifted.");
        }

        private static float Percentile(List<float> sortedSamples, float percentile)
        {
            Require(sortedSamples != null && sortedSamples.Count > 0, "Frame-time percentile requires samples.");
            var index = Mathf.Clamp(Mathf.CeilToInt(sortedSamples.Count * percentile) - 1, 0, sortedSamples.Count - 1);
            return sortedSamples[index];
        }

        private IEnumerator WaitForResolution(int width, int height, float seconds)
        {
            var deadline = Time.realtimeSinceStartup + seconds;
            while ((Screen.width != width || Screen.height != height) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Require(Screen.width == width && Screen.height == height,
                $"Resolution mismatch requested={width}x{height} actual={Screen.width}x{Screen.height}");
            yield return new WaitForSecondsRealtime(0.5f);
        }

        private static IEnumerator WaitForManagementRoot(ManagementUiV2Presenter presenter, float seconds)
        {
            var deadline = Time.realtimeSinceStartup + seconds;
            while ((presenter.GetManagementRootForQa() == null ||
                    !presenter.IsManagementVisibleForQa) &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;
            Require(presenter.GetManagementRootForQa() != null && presenter.IsManagementVisibleForQa,
                "Management UI root did not become active.");
            yield return null;
        }

        private IEnumerator CaptureAndValidate(
            ManagementUiV2Presenter presenter,
            string fileName,
            int expectedWidth,
            int expectedHeight,
            int superSize = 1)
        {
            Canvas.ForceUpdateCanvases();
            var root = presenter.GetManagementRootForQa();
            var canvas = root.GetComponent<Canvas>();
            Require(canvas != null && canvas.pixelPerfect,
                "Management Canvas must use pixel-perfect snapping for the 8px border-safe contract.");
            LayoutRebuilder.ForceRebuildLayoutImmediate(root.GetComponent<RectTransform>());
            Canvas.ForceUpdateCanvases();
            var metrics = Measure(root, superSize);
            Require(metrics.MissingGlyphs == 0, "Missing glyphs: " + metrics.MissingGlyphs);
            Require(metrics.OverflowTexts == 0, "Clipped/overflowing texts: " + metrics.OverflowTexts);
            Require(metrics.TextOverlaps == 0, "Sibling text overlaps: " + metrics.TextOverlaps);
            Require(metrics.BorderSafeTextViolations == 0,
                "Text glyph bounds violate 8px border-safe content inset: " + metrics.BorderSafeTextViolations +
                " | " + metrics.BorderSafeTextDetails);
            Require(metrics.MinimumFrameInnerClearancePixels >=
                    (float)ManagementUiR1RegressionFixture.MinimumTextInnerClearance,
                $"Minimum text/frame-inner clearance is below 8.000px: {metrics.MinimumFrameInnerClearancePixels:0.000}px");
            Require(metrics.FamilyTitleFrameOverlapPixels <= 0.01f,
                "Family title overlaps the card frame: " + metrics.FamilyTitleFrameOverlapPixels);
            Require(metrics.PanelOutsidePixels == 0, "Panel outside-screen pixels: " + metrics.PanelOutsidePixels);
            Require(metrics.ButtonHitErrorPixels <= 0.01f, "Button visual/click rect error: " + metrics.ButtonHitErrorPixels);
            Require(metrics.ButtonRaycastMisses == 0, "Button center raycast misses: " + metrics.ButtonRaycastMisses);
            Require(metrics.SmallButtons == 0, "Buttons below 48px target: " + metrics.SmallButtons);
            Require(metrics.OfferCardWidthSpreadPixels <= ManagementUiR1RegressionFixture.MaximumOfferBorderWidthSpread + 0.01f,
                "Offer-card runtime width spread exceeds 2px: " + metrics.OfferCardWidthSpreadPixels);
            Require(ManagementUiV2Presenter.TightButtonOpaqueAreaRatio >= ManagementUiLayoutMetrics.MinimumButtonOpaqueCoverage &&
                    ManagementUiV2Presenter.TightButtonOpaqueWidthRatio >= ManagementUiLayoutMetrics.MinimumButtonOpaqueCoverage &&
                    ManagementUiV2Presenter.TightButtonOpaqueHeightRatio >= ManagementUiLayoutMetrics.MinimumButtonOpaqueCoverage,
                "Tight button surface coverage is below 80%.");
            Append($"LAYOUT_PASS | renderTarget={expectedWidth}x{expectedHeight} window={Screen.width}x{Screen.height} supersize={superSize} " +
                   $"texts={metrics.TextCount} buttons={metrics.ButtonCount} " +
                   $"missingGlyphs=0 overflowTexts=0 textOverlaps=0 borderSafeTextViolations=0 panelOutsidePixels=0 " +
                   $"minimumFrameInnerClearancePx={metrics.MinimumFrameInnerClearancePixels:0.000} canvasPixelPerfect=1 " +
                   $"familyTitleFrameOverlapPx={metrics.FamilyTitleFrameOverlapPixels:0.###} " +
                   $"offerCardBorderWidthsPx={string.Join(",", Array.ConvertAll(metrics.OfferCardWidthsPixels, value => value.ToString("0.###")))} " +
                   $"offerCardWidthSpreadPx={metrics.OfferCardWidthSpreadPixels:0.###} buttonHitErrorPx={metrics.ButtonHitErrorPixels:0.###} " +
                   $"buttonSurface=area:{ManagementUiV2Presenter.TightButtonOpaqueAreaRatio:P1},width:{ManagementUiV2Presenter.TightButtonOpaqueWidthRatio:P0},height:{ManagementUiV2Presenter.TightButtonOpaqueHeightRatio:P0} " +
                   $"buttonRaycastMisses=0 smallButtons=0 minRenderedTextPx={metrics.MinimumRenderedTextPixels:0.##}");
            yield return Capture(fileName, expectedWidth, expectedHeight, superSize);
        }

        private IEnumerator CaptureGlyphProof(
            ManagementUiV2Presenter presenter,
            string fileName,
            int expectedWidth,
            int expectedHeight,
            int superSize = 1)
        {
            var root = presenter.GetManagementRootForQa();
            var font = root.GetComponentInChildren<TMP_Text>(true).font;
            var panel = new GameObject("QA Korean Glyph Proof", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(root.transform, false);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(1460f, 196f);
            panel.GetComponent<Image>().color = new Color(1f, 0.99f, 0.96f, 1f);
            var textObject = new GameObject("QA Korean Glyph Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(panel.transform, false);
            var textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(28f, 20f);
            textRect.offsetMax = new Vector2(-28f, -20f);
            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = 27f;
            text.color = new Color(0.09f, 0.15f, 0.14f, 1f);
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            text.richText = false;
            text.text = ManagementUiV2Presenter.KoreanGlyphQaSample;
            text.ForceMeshUpdate();
            Require(!text.isTextOverflowing, "Rendered Korean glyph proof overflowed its panel.");
            foreach (var character in ManagementUiV2Presenter.KoreanGlyphQaSample)
            {
                if (char.IsWhiteSpace(character)) continue;
                Require(text.font.HasCharacter(character, true, true), "Rendered test string lacks glyph: " + character);
            }
            Canvas.ForceUpdateCanvases();
            yield return Capture(fileName, expectedWidth, expectedHeight, superSize);
            Destroy(panel);
            yield return null;
            Append($"GLYPH_RENDER_PASS | resolution={expectedWidth}x{expectedHeight} sample=complete,jamo,digits,won,punctuation missing=0 tofu=0 clipped=0");
        }

        private void ValidateRoutes(PrototypeBootstrap bootstrap, ManagementUiV2Presenter presenter)
        {
            var root = presenter.GetManagementRootForQa();
            Click(FindButton(root, "4×"));
            Require(Mathf.Approximately(bootstrap.WorldTimeScale, 4f), "4× speed button did not update world speed.");
            Click(FindButton(root, "1×"));
            Require(Mathf.Approximately(bootstrap.WorldTimeScale, 1f), "1× speed button did not restore world speed.");

            var offer = FindButton(root, "계약 검토 후 수락");
            Require(offer != null, "Initial contract accept route is missing.");
            Click(offer);
            Canvas.ForceUpdateCanvases();
            Require(FindText(root, "진행 계약  1/2") != null, "Accepted contract did not enter progress strip.");

            Click(FindButton(root, "저장"));
            Require(bootstrap.UiScreen == PrototypeUiScreen.SaveSlots, "Save button route did not open save slots.");
            bootstrap.ShowManagementNow();
            Click(FindButton(root, "불러오기"));
            Require(bootstrap.UiScreen == PrototypeUiScreen.LoadSlots, "Load button route did not open load slots.");
            bootstrap.ShowManagementNow();

            var observationBefore = bootstrap.IsOfficeObservationCamera;
            Click(FindButton(root, "사무실 보기"));
            Require(bootstrap.UiScreen == PrototypeUiScreen.Playing, "Office-view button did not close management.");
            bootstrap.ToggleOfficeObservationCameraNow();
            Require(bootstrap.IsOfficeObservationCamera != observationBefore, "C camera route did not toggle observation mode.");
            bootstrap.ToggleOfficeObservationCameraNow();
            bootstrap.ShowManagementNow();
            Append("FUNCTION_PASS | contractAccepted=1 progressVisible=1 officeView=1 save=1 load=1 speed=1x,4x");
        }

        private static LayoutMetrics Measure(GameObject root, int renderScale)
        {
            var metrics = new LayoutMetrics
            {
                MinimumRenderedTextPixels = float.MaxValue,
                MinimumFrameInnerClearancePixels = float.MaxValue
            };
            var texts = root.GetComponentsInChildren<TMP_Text>(true);
            var activeTexts = new List<TMP_Text>();
            foreach (var text in texts)
            {
                if (!text.gameObject.activeInHierarchy || string.IsNullOrEmpty(text.text)) continue;
                activeTexts.Add(text);
                metrics.TextCount++;
                text.ForceMeshUpdate();
                if (text.isTextOverflowing || text.preferredHeight > text.rectTransform.rect.height + 1f)
                    metrics.OverflowTexts++;
                metrics.MinimumRenderedTextPixels = Mathf.Min(
                    metrics.MinimumRenderedTextPixels,
                    text.fontSize * text.canvas.scaleFactor);
                foreach (var character in text.text)
                {
                    if (char.IsWhiteSpace(character)) continue;
                    if (!text.font.HasCharacter(character, true, true)) metrics.MissingGlyphs++;
                }
                // Every visible label, including all button labels, must sit at least 8
                // physical Canvas pixels inside the frame's 9-slice inner edge.
                var container = FindBorderContainer(text, root.transform);
                if (container != null)
                {
                    // Validate the actual render target pixel grid, not sub-pixel layout
                    // floats. Frame edges use Canvas pixel snapping and glyph bounds round
                    // outward so the reported clearance is conservative and integral.
                    var innerRect = PixelSnappedRect(FrameInnerRect(container), renderScale);
                    var glyphRect = PixelOutwardRect(GlyphScreenRect(text), renderScale);
                    var innerClearance = RectClearance(innerRect, glyphRect);
                    metrics.MinimumFrameInnerClearancePixels = Mathf.Min(
                        metrics.MinimumFrameInnerClearancePixels,
                        innerClearance);
                    if (innerClearance < ManagementUiR1RegressionFixture.MinimumTextInnerClearance)
                    {
                        metrics.BorderSafeTextViolations++;
                        metrics.BorderSafeTextDetails +=
                            $"{text.transform.parent.name}/{text.gameObject.name}:{innerClearance:0.000}px;";
                    }
                    if (text.gameObject.name == "Family Title")
                    {
                        metrics.FamilyTitleFrameOverlapPixels = Mathf.Max(
                            metrics.FamilyTitleFrameOverlapPixels,
                            RectDeficit(innerRect, glyphRect));
                    }
                }
            }

            for (var first = 0; first < activeTexts.Count; first++)
            for (var second = first + 1; second < activeTexts.Count; second++)
            {
                if (activeTexts[first].transform.parent != activeTexts[second].transform.parent) continue;
                if (ScreenRect(activeTexts[first].rectTransform).Overlaps(ScreenRect(activeTexts[second].rectTransform)))
                    metrics.TextOverlaps++;
            }

            foreach (var image in root.GetComponentsInChildren<Image>(true))
            {
                if (!image.gameObject.activeInHierarchy) continue;
                var rect = PixelSnappedRect(ScreenRect(image.rectTransform), renderScale);
                metrics.PanelOutsidePixels += OutsidePixels(
                    rect,
                    Screen.width * Mathf.Max(1, renderScale),
                    Screen.height * Mathf.Max(1, renderScale));
            }

            var offerWidths = new List<float>();
            foreach (var image in root.GetComponentsInChildren<Image>(true))
            {
                if (!image.gameObject.activeInHierarchy ||
                    !image.gameObject.name.StartsWith("Offer ", StringComparison.Ordinal)) continue;
                offerWidths.Add(PixelSnappedRect(ScreenRect(image.rectTransform), renderScale).width);
            }
            Require(offerWidths.Count == 3, "Expected exactly three runtime offer cards; found " + offerWidths.Count);
            metrics.OfferCardWidthsPixels = offerWidths.ToArray();
            metrics.OfferCardWidthSpreadPixels = Mathf.Max(offerWidths.ToArray()) - Mathf.Min(offerWidths.ToArray());

            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                if (!button.gameObject.activeInHierarchy) continue;
                metrics.ButtonCount++;
                var buttonRect = ScreenRect(button.GetComponent<RectTransform>());
                var canvas = button.GetComponentInParent<Canvas>();
                var minimumTarget = (float)Simulation.ManagementUi.ManagementUiLayoutMetrics.MinimumClickTarget *
                                    (canvas != null ? canvas.scaleFactor : 1f);
                if (buttonRect.width + 0.01f < minimumTarget || buttonRect.height + 0.01f < minimumTarget)
                    metrics.SmallButtons++;
                var imageRect = button.targetGraphic != null
                    ? ScreenRect(button.targetGraphic.rectTransform)
                    : new Rect(float.PositiveInfinity, float.PositiveInfinity, 0f, 0f);
                metrics.ButtonHitErrorPixels = Mathf.Max(metrics.ButtonHitErrorPixels, RectDelta(buttonRect, imageRect));
                if (!CenterRaycastHits(button, buttonRect.center)) metrics.ButtonRaycastMisses++;
            }

            if (metrics.MinimumRenderedTextPixels == float.MaxValue) metrics.MinimumRenderedTextPixels = 0f;
            if (metrics.MinimumFrameInnerClearancePixels == float.MaxValue)
                metrics.MinimumFrameInnerClearancePixels = 0f;
            return metrics;
        }

        private static bool CenterRaycastHits(Button expected, Vector2 center)
        {
            if (EventSystem.current == null) return false;
            var data = new PointerEventData(EventSystem.current) { position = center };
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(data, results);
            foreach (var result in results)
            {
                var button = result.gameObject.GetComponentInParent<Button>();
                if (button == expected) return true;
                if (button != null) return false;
            }
            return false;
        }

        private static Image FindBorderContainer(TMP_Text text, Transform root)
        {
            var current = text.transform.parent;
            while (current != null && current != root)
            {
                var image = current.GetComponent<Image>();
                if (image != null && image.sprite != null)
                    return image;
                current = current.parent;
            }
            return null;
        }

        private static Rect FrameInnerRect(Image image)
        {
            var rect = ScreenRect(image.rectTransform);
            var canvas = image.GetComponentInParent<Canvas>();
            var scale = canvas != null ? canvas.scaleFactor : 1f;
            var border = image.sprite.border;
            return Rect.MinMaxRect(
                rect.xMin + border.x * scale,
                rect.yMin + border.y * scale,
                rect.xMax - border.z * scale,
                rect.yMax - border.w * scale);
        }

        private static Rect GlyphScreenRect(TMP_Text text)
        {
            var bounds = text.textBounds;
            var minimumWorld = text.rectTransform.TransformPoint(bounds.min);
            var maximumWorld = text.rectTransform.TransformPoint(bounds.max);
            var minimum = RectTransformUtility.WorldToScreenPoint(null, minimumWorld);
            var maximum = RectTransformUtility.WorldToScreenPoint(null, maximumWorld);
            return Rect.MinMaxRect(
                Mathf.Min(minimum.x, maximum.x),
                Mathf.Min(minimum.y, maximum.y),
                Mathf.Max(minimum.x, maximum.x),
                Mathf.Max(minimum.y, maximum.y));
        }

        private static float RectDeficit(Rect container, Rect content)
        {
            return Mathf.Max(
                0f,
                container.xMin - content.xMin,
                container.yMin - content.yMin,
                content.xMax - container.xMax,
                content.yMax - container.yMax);
        }

        private static float RectClearance(Rect container, Rect content)
        {
            return Mathf.Min(
                content.xMin - container.xMin,
                content.yMin - container.yMin,
                container.xMax - content.xMax,
                container.yMax - content.yMax);
        }

        private static Rect PixelSnappedRect(Rect rect, int renderScale)
        {
            var scale = Mathf.Max(1, renderScale);
            return Rect.MinMaxRect(
                Mathf.Round(rect.xMin * scale),
                Mathf.Round(rect.yMin * scale),
                Mathf.Round(rect.xMax * scale),
                Mathf.Round(rect.yMax * scale));
        }

        private static Rect PixelOutwardRect(Rect rect, int renderScale)
        {
            var scale = Mathf.Max(1, renderScale);
            return Rect.MinMaxRect(
                Mathf.Floor(rect.xMin * scale),
                Mathf.Floor(rect.yMin * scale),
                Mathf.Ceil(rect.xMax * scale),
                Mathf.Ceil(rect.yMax * scale));
        }

        private IEnumerator Capture(string fileName, int expectedWidth, int expectedHeight, int superSize)
        {
            var path = Path.Combine(_outputFolder, fileName);
            if (File.Exists(path)) File.Delete(path);
            ScreenCapture.CaptureScreenshot(path, superSize);
            var deadline = Time.realtimeSinceStartup + 15f;
            while ((!File.Exists(path) || new FileInfo(path).Length < 1024) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Require(File.Exists(path) && new FileInfo(path).Length >= 1024, "Screenshot was not written: " + path);
            ReadPngSize(path, out var width, out var height);
            Require(width == expectedWidth && height == expectedHeight,
                $"Screenshot size mismatch file={fileName} expected={expectedWidth}x{expectedHeight} actual={width}x{height}");
            _captures++;
            Append($"CAPTURE_PASS | file={fileName} size={width}x{height} bytes={new FileInfo(path).Length}");
        }

        private static Button FindButton(GameObject root, string exactLabel)
        {
            if (root == null) return null;
            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                var label = button.GetComponentInChildren<TMP_Text>(true);
                if (label != null && string.Equals(label.text, exactLabel, StringComparison.Ordinal)) return button;
            }
            return null;
        }

        private static TMP_Text FindText(GameObject root, string exactText)
        {
            if (root == null) return null;
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                if (string.Equals(text.text, exactText, StringComparison.Ordinal)) return text;
            return null;
        }

        private static TMP_Text FindTextContaining(GameObject root, string value)
        {
            if (root == null || string.IsNullOrEmpty(value)) return null;
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                if (!string.IsNullOrEmpty(text.text) && text.text.IndexOf(value, StringComparison.Ordinal) >= 0) return text;
            return null;
        }

        private static void Click(Button button)
        {
            Require(button != null, "Expected button is missing.");
            Require(button.interactable, "Expected button is disabled: " + button.name);
            button.onClick.Invoke();
        }

        private static Rect ScreenRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var minimum = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            var maximum = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
            return Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
        }

        private static int OutsidePixels(Rect rect, int renderTargetWidth, int renderTargetHeight)
        {
            var left = Mathf.Max(0f, -rect.xMin);
            var right = Mathf.Max(0f, rect.xMax - renderTargetWidth);
            var bottom = Mathf.Max(0f, -rect.yMin);
            var top = Mathf.Max(0f, rect.yMax - renderTargetHeight);
            return Mathf.CeilToInt(left + right + bottom + top);
        }

        private static float RectDelta(Rect first, Rect second)
        {
            return Mathf.Max(
                Mathf.Abs(first.xMin - second.xMin),
                Mathf.Abs(first.yMin - second.yMin),
                Mathf.Abs(first.xMax - second.xMax),
                Mathf.Abs(first.yMax - second.yMax));
        }

        private static void ReadPngSize(string path, out int width, out int height)
        {
            var bytes = File.ReadAllBytes(path);
            Require(bytes.Length >= 24 && bytes[0] == 137 && bytes[1] == 80 && bytes[2] == 78 && bytes[3] == 71,
                "Invalid PNG: " + path);
            width = ReadBigEndian(bytes, 16);
            height = ReadBigEndian(bytes, 20);
        }

        private static int ReadBigEndian(byte[] bytes, int offset)
        {
            return (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];
        }

        private void HandleLog(string condition, string stackTrace, LogType type)
        {
            if (condition.IndexOf("MISSING_GLYPH", StringComparison.OrdinalIgnoreCase) >= 0 ||
                condition.IndexOf("Unicode value", StringComparison.OrdinalIgnoreCase) >= 0 &&
                condition.IndexOf("font asset", StringComparison.OrdinalIgnoreCase) >= 0)
                _missingGlyphLogs++;
            if (condition.IndexOf("already added", StringComparison.OrdinalIgnoreCase) >= 0 ||
                condition.IndexOf("can only contain one", StringComparison.OrdinalIgnoreCase) >= 0 &&
                condition.IndexOf("LayoutGroup", StringComparison.OrdinalIgnoreCase) >= 0)
                _duplicateComponentLogs++;
            if (type == LogType.Exception)
            {
                _exceptionLogs++;
                if (!string.IsNullOrEmpty(_reportPath)) Append("PLAYER_QA_FAIL | " + condition);
                Application.Quit(1);
            }
        }

        private string ReadArgument(string name)
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index < arguments.Length - 1; index++)
                if (string.Equals(arguments[index], name, StringComparison.Ordinal)) return arguments[index + 1];
            return string.Empty;
        }

        private void Append(string line)
        {
            File.AppendAllText(_reportPath, line + Environment.NewLine, System.Text.Encoding.UTF8);
            Debug.Log("MANAGEMENT_UI_PLAYER_QA | " + line);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private sealed class LayoutMetrics
        {
            public int TextCount;
            public int ButtonCount;
            public int MissingGlyphs;
            public int OverflowTexts;
            public int TextOverlaps;
            public int BorderSafeTextViolations;
            public string BorderSafeTextDetails = string.Empty;
            public int PanelOutsidePixels;
            public int ButtonRaycastMisses;
            public int SmallButtons;
            public float ButtonHitErrorPixels;
            public float MinimumRenderedTextPixels;
            public float MinimumFrameInnerClearancePixels;
            public float OfferCardWidthSpreadPixels;
            public float[] OfferCardWidthsPixels = Array.Empty<float>();
            public float FamilyTitleFrameOverlapPixels;
        }

        private readonly struct FrameStats
        {
            public FrameStats(
                int sampleCount,
                float p95Milliseconds,
                float p99Milliseconds,
                float maximumMilliseconds,
                int framesAtOrAbove50Milliseconds,
                float completionMilliseconds)
            {
                SampleCount = sampleCount;
                P95Milliseconds = p95Milliseconds;
                P99Milliseconds = p99Milliseconds;
                MaximumMilliseconds = maximumMilliseconds;
                FramesAtOrAbove50Milliseconds = framesAtOrAbove50Milliseconds;
                CompletionMilliseconds = completionMilliseconds;
            }

            public int SampleCount { get; }
            public float P95Milliseconds { get; }
            public float P99Milliseconds { get; }
            public float MaximumMilliseconds { get; }
            public int FramesAtOrAbove50Milliseconds { get; }
            public float CompletionMilliseconds { get; }
        }
    }
}
