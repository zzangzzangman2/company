using System;
using System.Collections;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FamilyCompany.Presentation.Unity.MainNavigation
{
    // Explicit, isolated presentation QA. No initial save, native input, patch activation or world reskin.
    public sealed class UnifiedOfficeHudPlayerQa : MonoBehaviour
    {
        private string _output;
        private bool _runtimeError;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!Environment.GetCommandLineArgs().Contains("-unifiedOfficeHudQa")) return;
            var host = new GameObject("~UnifiedOfficeHudQa");
            DontDestroyOnLoad(host);
            host.AddComponent<UnifiedOfficeHudPlayerQa>();
        }

        private void Start()
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-unifiedOfficeHudOutput");
            if (index < 0 || index + 1 >= args.Length) { Application.Quit(1); return; }
            _output = Path.GetFullPath(args[index + 1]);
            Directory.CreateDirectory(_output);
            Application.runInBackground = true;
            AudioListener.volume = 0;
            Application.logMessageReceived += OnLog;
            StartCoroutine(RunSafely());
        }

        private void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) _runtimeError = true;
        }

        private void OnDestroy() => Application.logMessageReceived -= OnLog;

        private IEnumerator RunSafely()
        {
            var routine = Run();
            while (true)
            {
                object current;
                try { if (!routine.MoveNext()) break; current = routine.Current; }
                catch (Exception error)
                {
                    File.WriteAllText(Path.Combine(_output, "result.txt"), "UNIFIED_OFFICE_HUD_QA: FAIL\n" + error);
                    Debug.LogException(error);
                    Application.Quit(1);
                    yield break;
                }
                yield return current;
            }
            File.WriteAllText(Path.Combine(_output, "result.txt"),
                "UNIFIED_OFFICE_HUD_QA: PASS\nOne surface; default panel closed; live cash/date; " +
                "5 tabs; Business toggle; Escape; 1x/2x/4x; pause/resume; safe bounds; no overflow; runtimeErrors=0.\n" +
                "Fresh normal game; EventSystem pointer events, not native mouse; no saves or updater activation.\n");
            Application.Quit(0);
        }

        private IEnumerator Run()
        {
            Require(!Application.isBatchMode, "Full-frame capture needs a hidden private-desktop presented Player.");
            var bootstrap = FindFirstObjectByType<PrototypeBootstrap>();
            Require(bootstrap != null, "Bootstrap missing.");
            bootstrap.StartNewGameNow(1, false);
            var deadline = Time.realtimeSinceStartup + 60;
            var presenter = FindFirstObjectByType<MainNavigationHudPresenter>();
            while (presenter == null || presenter.GetSpeedButtonForQa(1) == null ||
                   !presenter.GetSpeedButtonForQa(1).gameObject.activeInHierarchy || ScenePreviewJump.IsPresentationLoading)
            {
                Require(Time.realtimeSinceStartup < deadline, "HUD startup timed out.");
                yield return null;
                presenter = FindFirstObjectByType<MainNavigationHudPresenter>();
            }
            yield return new WaitForSecondsRealtime(1);
            Require(!presenter.HasOpenPanel, "Office is obscured on entry.");
            foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1024, 768),
                         new Vector2Int(1600, 1000), new Vector2Int(1920, 1080) })
            {
                Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
                yield return new WaitForSecondsRealtime(.8f);
                Canvas.ForceUpdateCanvases();
                Require(!ScenePreviewJump.IsPresentationLoading, "Loading overlay still covers the office.");
                Require(Screen.width == size.x && Screen.height == size.y, "Resolution mismatch.");
                ValidateHeader(presenter, bootstrap);
                var path = Path.Combine(_output, $"office-{size.x}x{size.y}.png");
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(path);
                yield return new WaitForSecondsRealtime(.5f);
                Require(File.Exists(path) && new FileInfo(path).Length > 10000, "Frame not captured.");
            }
            foreach (var definition in MainNavigationCatalog.All)
            {
                Click(presenter.GetTabButtonForQa(definition.TabId));
                Require(presenter.HasOpenPanel && presenter.ActiveTabId == definition.Id, "Tab route failed.");
                Click(presenter.GetTabButtonForQa(definition.TabId));
                Require(!presenter.HasOpenPanel, "Repeated tab did not close to office.");
            }
            Click(presenter.GetTabButtonForQa(MainNavigationTabId.Projects));
            Require(presenter.TryHandleEscape() && !presenter.HasOpenPanel, "Business Escape did not close.");
            foreach (var speed in new[] { 2, 4, 1 })
            {
                Click(presenter.GetSpeedButtonForQa(speed));
                Require(Mathf.Approximately(bootstrap.WorldTimeScale, speed), "Live speed route failed.");
            }
            var pause = presenter.GetComponentsInChildren<Button>(true).Single(b => b.name == "Main Navigation Pause");
            Click(pause);
            Require(bootstrap.UiScreen == PrototypeUiScreen.PauseMenu && Time.timeScale == 0, "Pause route failed.");
            bootstrap.ResumeGameNow();
            Require(bootstrap.UiScreen == PrototypeUiScreen.Playing, "Resume failed.");
            bootstrap.State.Company.RecordSale("hud-qa-only", 0, 1234);
            yield return new WaitForSecondsRealtime(.3f);
            ValidateHeader(presenter, bootstrap);
            Require(!_runtimeError, "Runtime error was logged.");
        }

        private static void ValidateHeader(MainNavigationHudPresenter presenter, PrototypeBootstrap bootstrap)
        {
            var top = presenter.GetComponentsInChildren<RectTransform>(true)
                .Single(r => r.name == "Main Navigation Top HUD");
            Require(top.GetComponent<Image>().color == Color.white, "Header is not a shared white surface.");
            Require(!presenter.GetComponentsInChildren<RectTransform>(true).Any(r => r.name.Contains("Name Badge") ||
                r.name.Contains("Date Time Badge")), "Detached header badges remain.");
            var texts = top.GetComponentsInChildren<TMP_Text>();
            Require(texts.Any(t => t.text == $"₩ {bootstrap.State.Company.CashWon:N0}"), "Cash is not canonical/live.");
            Require(texts.Any(t => t.text.StartsWith(bootstrap.State.Time.Now.ToString("yyyy. MM. dd"))), "Date is not live.");
            var topRect = Bounds(top);
            foreach (var text in texts)
            {
                text.ForceMeshUpdate();
                var rect = Bounds(text.rectTransform);
                Require(!text.isTextOverflowing, "HUD text overflows: " + text.text);
                Require(rect.xMin >= topRect.xMin && rect.xMax <= topRect.xMax &&
                    Mathf.Abs(rect.center.y - topRect.center.y) < 2, "Header is not one aligned row: " + text.text);
            }
            foreach (var tab in MainNavigationCatalog.All)
            {
                var bounds = Bounds(presenter.GetTabButtonForQa(tab.TabId).GetComponent<RectTransform>());
                Require(bounds.xMin >= 0 && bounds.xMax <= Screen.width && bounds.yMin >= 0 &&
                    bounds.yMax <= Screen.height && bounds.width >= 44 && bounds.height >= 44, "Invalid tab hit target.");
            }
        }

        private static Rect Bounds(RectTransform rect)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private static void Click(Button button)
        {
            Require(button != null && button.isActiveAndEnabled && button.interactable, "Button is not available.");
            var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler);
        }

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
