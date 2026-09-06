using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FamilyCompany.Presentation.Unity.OfficeRuntime;
using FamilyCompany.Simulation.Contracts;
using FamilyCompany.Simulation.Company;
using FamilyCompany.Simulation.Prototype;
using FamilyCompany.Save;
using FamilyCompany.Simulation.OfficeLayout;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FamilyCompany.Presentation.Unity.MainNavigation
{
    /// <summary>Opt-in development-player test. No native input, no save, no teleport or pose injection.
    /// Purchases use the transaction API; contract buttons use managed UI pointer events.</summary>
    public sealed class StarterProductPlayerQa : MonoBehaviour
    {
        private string _directory;
        private readonly StringBuilder _report = new StringBuilder();
        private readonly List<string> _errors = new List<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var args = Environment.GetCommandLineArgs();
            bool explicitReleaseObservation = args.Contains("-familyCompanyManualGameplayObservation");
            if ((Path.GetFileName(Application.dataPath) != "FamilyCompany_FastQa_Data" && !explicitReleaseObservation) ||
                !args.Contains("-starterProductQa")) return;
            var host = new GameObject("~StarterProductPlayerQa");
            DontDestroyOnLoad(host);
            host.AddComponent<StarterProductPlayerQa>();
        }

        private void Awake() => Application.logMessageReceived += OnLog;
        private void OnDestroy() => Application.logMessageReceived -= OnLog;
        private void OnLog(string text, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) _errors.Add(text);
        }

        private IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-starterProductArtifacts");
            if (i < 0 || i + 1 >= args.Length) { Application.Quit(1); yield break; }
            _directory = Path.GetFullPath(args[i + 1]);
            Directory.CreateDirectory(_directory);
            Application.runInBackground = true;
            AudioListener.volume = 0;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            IEnumerator test = Run();
            while (true)
            {
                object current;
                try
                {
                    if (!test.MoveNext()) break;
                    current = test.Current;
                }
                catch (Exception ex) { Finish(false, ex.ToString()); yield break; }
                yield return current;
            }
            Finish(_errors.Count == 0, string.Join("\n", _errors));
        }

        private IEnumerator Run()
        {
            var bootstrap = FindFirstObjectByType<PrototypeBootstrap>();
            Require(bootstrap != null, "bootstrap missing");
            ScenePreviewJump.ShowStarterOffice();
            bootstrap.StartNewGameNow(1, false);
            ScenePreviewJump.ShowStarterOffice();
            float end = Time.realtimeSinceStartup + 40;
            StarterOfficeRuntimeBootstrap runtime;
            do
            {
                runtime = FindFirstObjectByType<StarterOfficeRuntimeBootstrap>();
                Require(Time.realtimeSinceStartup < end, "office ready timeout");
                yield return null;
            } while (runtime == null || !runtime.IsReady || runtime.Actors.Count != 4 || ScenePreviewJump.IsPresentationLoading);
            var state = bootstrap.State;
            Require(state.Company.CashWon == 5000000 && state.OfficeGrid.SeatSlots.Count == 0, "normal empty opening");
            var cells = new[] { new OfficeGridCoordinate(4, 4), new OfficeGridCoordinate(9, 4),
                new OfficeGridCoordinate(9, 9), new OfficeGridCoordinate(4, 9) };
            for (int n = 0; n < 4; n++)
            {
                var purchase = OfficeFurnitureTransactionService.PurchaseAndPlaceWorkstation(state,
                    "starter-qa-purchase-" + n, "starter-qa-desk-" + n, cells[n], (OfficeFurnitureFacing)n);
                Require(purchase.Success, "desk purchase " + purchase.Message);
            }
            runtime.ApplyLayoutForQa(state.OfficeGrid);
            end = Time.realtimeSinceStartup + 30;
            do { Require(Time.realtimeSinceStartup < end, "layout timeout"); yield return null; } while (!runtime.IsReady);
            yield return new WaitForSecondsRealtime(2);
            Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
            yield return new WaitForSecondsRealtime(0.7f);
            Require(state.Company.CashWon == 3400000 && state.OfficeGrid.SeatSlots.Count == 4, "four purchased sets");
            bootstrap.SetWorldTimeScaleNow(4);
            var hud = FindFirstObjectByType<MainNavigationHudPresenter>();
            Require(hud != null, "HUD missing");
            hud.OpenTabNow(MainNavigationTabId.Projects);
            yield return null;
            while (!ClickFeatureWhenReady(hud)) yield return null;
            yield return new WaitForSecondsRealtime(0.6f);
            ValidateText();
            ScreenCapture.CaptureScreenshot(Path.Combine(_directory, "01-learning.png"));
            yield return new WaitForSecondsRealtime(0.2f);
            long before = state.Company.CashWon;
            Click(FindButton("하청 수락"));
            yield return new WaitForSecondsRealtime(0.6f);
            var work = state.Contracts.Contracts.Single();
            Require(state.Company.CashWon == before - work.Offer.UpfrontCostWon && work.CompletedPersonHours == 0,
                "UI acceptance charges only the real upfront fee");
            var father = runtime.Actors.Single(a => a.AgentId == "father");
            int requestedMinutes = ContractPortfolio.MinutesPerPersonHour(work, state.Family.Get("father")) *
                Math.Min(4, work.RemainingPersonHours);
            string fatherLabel = state.Family.Get("father").DisplayName + " · 최대 4인시";
            Click(FindButton(fatherLabel));
            yield return null;
            Require(father.HasAssignedTask, "managed UI click reaches coordinator");
            ValidateText();
            ScreenCapture.CaptureScreenshot(Path.Combine(_directory, "02-assigned.png"));
            yield return new WaitForSecondsRealtime(0.2f);
            hud.ReturnToOfficeNow();
            long started = state.Time.ElapsedMinutes;
            int seatedSamples = 0, travelSamples = 0;
            int creditedBeforeSeat = 0;
            bool sawSeat = false;
            long firstSeatedMinute = -1;
            var previous = father.Position;
            float nextTrace = 0;
            var trace = new StringBuilder("realSeconds,minute,phase,activity,assigned,remaining,personHours,energy,taskResult,position,cell,destination,pathLength,reservationBlocker,movementBlocker\n");
            end = Time.realtimeSinceStartup + 180;
            while (work.CompletedPersonHours == 0)
            {
                if (Time.realtimeSinceStartup >= nextTrace)
                {
                    nextTrace = Time.realtimeSinceStartup + 5;
                    var command = father.CaptureLayoutSnapshot();
                    var tasks = FindFirstObjectByType<OfficeContractTaskCoordinator>();
                    trace.AppendLine($"{Time.realtimeSinceStartup:F1},{state.Time.ElapsedMinutes},{father.Phase},{father.CurrentActivity},{father.HasAssignedTask},{command.AssignedWorkRemainingMinutes},{work.CompletedPersonHours},{state.Family.Get("father").Energy},{tasks?.LastWorkResult?.RejectionReason}," +
                        $"{father.Position.ToString().Replace(',', ':')},{father.CurrentCell.ToString().Replace(',', ':')},{father.ActiveDestinationCell.ToString().Replace(',', ':')},{father.SemanticPathLength},{father.LastReservationBlocker},{father.LastMovementBlocker}");
                    File.WriteAllText(Path.Combine(_directory, "physical-work.csv"), trace.ToString());
                    ScreenCapture.CaptureScreenshot(Path.Combine(_directory, "trace-" + ((int)Time.realtimeSinceStartup / 20) + ".png"));
                }
                Require(Time.realtimeSinceStartup < end, "real desk work did not credit within 180 seconds / " + father.Phase + " / " + bootstrap.WorldNotice);
                if (father.HasAssignedTask && father.Phase == OfficeRuntimeAgentPhase.Working)
                {
                    if (!sawSeat) firstSeatedMinute = state.Time.ElapsedMinutes;
                    sawSeat = true;
                    seatedSamples++;
                }
                if (Vector2.Distance(previous, father.Position) > 0.0001f) travelSamples++;
                previous = father.Position;
                if (!sawSeat && work.CompletedPersonHours > 0) creditedBeforeSeat++;
                yield return null;
            }
            Require(sawSeat && seatedSamples > 10 && creditedBeforeSeat == 0, "credited only after real seating and work");
            Require(work.CompletedPersonHours == Math.Min(4, work.Offer.EstimatedPersonHours), "requested block actually credited");
            Require(state.Time.ElapsedMinutes - firstSeatedMinute >= requestedMinutes - 1,
                "the initial work block cannot count the earlier commute");
            _report.AppendLine("normalNewGame=true furniturePurchase=transactionAPI nativePointer=false managedPointer=raycast+click");
            _report.AppendLine("normalClock=4x noTimeJump=true noRouteOrPoseInjection=true noSaveWrites=true");
            _report.AppendLine($"work={work.Offer.Title} credited={work.CompletedPersonHours} elapsedMinutes={state.Time.ElapsedMinutes-started} seatedSamples={seatedSamples} travelSamples={travelSamples}");
            _report.AppendLine($"requiredDeskMinutes={requestedMinutes} observedDeskMinutes={state.Time.ElapsedMinutes-firstSeatedMinute}");
            ScreenCapture.CaptureScreenshot(Path.Combine(_directory, "03-physical-work.png"));
            yield return new WaitForSecondsRealtime(0.5f);
            hud.OpenTabNow(MainNavigationTabId.Projects);
            yield return null;
            while (!ClickFeatureWhenReady(hud)) yield return null;
            yield return new WaitForSecondsRealtime(0.6f);
            ValidateText();
            ScreenCapture.CaptureScreenshot(Path.Combine(_directory, "04-progress.png"));
            yield return new WaitForSecondsRealtime(0.5f);
            Require(File.Exists(Path.Combine(_directory, "04-progress.png")), "capture missing");
            _report.AppendLine("scope=normal first-lesson acceptance and 4-person-hour physical work only; full business lifecycle is covered separately by pure simulation tests");
            if (Environment.GetCommandLineArgs().Contains("-starterProductLifecycleQa"))
            {
                // Explicit checkpoint setup, NOT native play or automatic game progression.
                // Finish lesson history and seed 20/24 development hours through the pure core.
                var adapter = FindFirstObjectByType<ContractGrowth.ContractBusinessRuntimeAdapter>();
                var runtimeStamina = state.StaminaRuntimeBridge;
                // Seed the checkpoint using semantic schedules, not a live actor's frozen
                // one-frame desk activity projected across several simulated days.
                state.UnbindStaminaRuntimeBridge(runtimeStamina);
                var runner = new SimulationRunner(state);
                int guard = 0;
                while (!StarterProductState.HasRequiredKnowHow(state))
                {
                    var active = state.Contracts.Contracts.FirstOrDefault(c => c.Status == SubcontractStatus.Active);
                    if (active == null)
                    {
                        Require(adapter.TryAcceptStarterLesson().Succeeded, "checkpoint lesson acceptance");
                        active = state.Contracts.Contracts.First(c => c.Status == SubcontractStatus.Active);
                    }
                    runner.AdvanceMinutes(60);
                    if (active.Status == SubcontractStatus.Active)
                        state.Contracts.RecordWork(active.Offer.OfferId, "father", 1, state.Time.ElapsedMinutes, state.Family, state.Company);
                    Require(++guard < 1500 && active.Status != SubcontractStatus.Failed, "lesson checkpoint setup");
                }
                var product = state.Growth.StarterProduct;
                Require(product.TryStartDevelopment(state, out _), "checkpoint investment");
                var development = product.CurrentWork(state);
                while (development.RemainingPersonHours > 4)
                {
                    runner.AdvanceMinutes(60);
                    state.Contracts.RecordWork(development.Offer.OfferId, "father", 1, state.Time.ElapsedMinutes, state.Family, state.Company);
                    Require(++guard < 2500 && development.Status == SubcontractStatus.Active, "development checkpoint setup");
                }
                var nextMorning = state.Time.Now.Date.AddDays(1).AddHours(9).AddMinutes(5);
                // A checkpoint may finish on Friday. Respect the normal family's
                // weekend availability instead of expecting an invalid assignment.
                while (nextMorning.DayOfWeek == DayOfWeek.Saturday || nextMorning.DayOfWeek == DayOfWeek.Sunday)
                    nextMorning = nextMorning.AddDays(1);
                bootstrap.AdvanceTimeNow((long)(nextMorning - state.Time.Now).TotalMinutes);
                state.BindStaminaRuntimeBridge(runtimeStamina);
                runtime.ApplyLayoutForQa(state.OfficeGrid);
                yield return new WaitForSecondsRealtime(3);
                while (!runtime.IsReady) yield return null;
                father = runtime.Actors.Single(a => a.AgentId == "father");
                hud.OpenTabNow(MainNavigationTabId.Projects);
                yield return null;
                while (!ClickFeatureWhenReady(hud)) yield return null;
                yield return new WaitForSecondsRealtime(0.6f);
                ValidateText();
                ScreenCapture.CaptureScreenshot(Path.Combine(_directory, "05-development-checkpoint.png"));
                Click(FindButton(fatherLabel));
                yield return null;
                Require(father.HasAssignedTask, "development UI assignment reaches real actor / " + bootstrap.WorldNotice);
                hud.ReturnToOfficeNow();
                end = Time.realtimeSinceStartup + 160;
                int developmentSeated = 0;
                while (development.Status == SubcontractStatus.Active)
                {
                    Require(Time.realtimeSinceStartup < end, "actual final development block timeout / " + father.Phase);
                    if (father.Phase == OfficeRuntimeAgentPhase.Working) developmentSeated++;
                    yield return null;
                }
                Require(development.Status == SubcontractStatus.Completed && developmentSeated > 10, "real development completion");
                yield return new WaitForSecondsRealtime(0.7f);
                hud.OpenTabNow(MainNavigationTabId.Projects);
                yield return null;
                while (!ClickFeatureWhenReady(hud)) yield return null;
                yield return new WaitForSecondsRealtime(0.6f);
                ValidateText();
                ScreenCapture.CaptureScreenshot(Path.Combine(_directory, "06-ready-for-sale.png"));
                long beforeSale = state.Company.CashWon;
                Click(FindButton("시험 판매 시작"));
                yield return new WaitForSecondsRealtime(0.6f);
                Require(product.Phase == StarterProductPhase.Trading && state.Company.CashWon == beforeSale + 180000,
                    "UI trial starts exactly three licences");
                Click(FindButton("이번 주 유지보수 접수"));
                yield return new WaitForSecondsRealtime(0.6f);
                var support = product.CurrentWork(state);
                Require(support.RemainingPersonHours == 2, "two support hours required");
                // The lesson checkpoint spent Father's stamina. Exercise normal team
                // assignment instead of ignoring energy and expecting unpaid exhausted work.
                var supporter = runtime.Actors.Where(a => a.AgentId != "player")
                    .OrderByDescending(a => state.Family.Get(a.AgentId).Energy).First();
                Require(state.Family.Get(supporter.AgentId).Energy >= 35, "support worker has actual stamina");
                Click(FindButton(state.Family.Get(supporter.AgentId).DisplayName + " · 최대 4인시"));
                yield return null;
                Require(supporter.HasAssignedTask, "support UI assignment reaches real actor");
                hud.ReturnToOfficeNow();
                end = Time.realtimeSinceStartup + 100;
                int supportSeated = 0;
                var supportTrace = new StringBuilder("second,minute,phase,assigned,remaining,hours,energy,result,blocker\n");
                float nextSupportTrace = 0;
                while (support.Status == SubcontractStatus.Active)
                {
                    if (Time.realtimeSinceStartup >= nextSupportTrace)
                    {
                        nextSupportTrace = Time.realtimeSinceStartup + 2;
                        var tasks = FindFirstObjectByType<OfficeContractTaskCoordinator>();
                        supportTrace.AppendLine($"{Time.realtimeSinceStartup:F1},{state.Time.Now:O},{supporter.Phase},{supporter.HasAssignedTask},{supporter.CaptureLayoutSnapshot().AssignedWorkRemainingMinutes},{support.CompletedPersonHours},{state.Family.Get(supporter.AgentId).Energy},{tasks?.LastWorkResult?.RejectionReason},{supporter.LastMovementBlocker}");
                        File.WriteAllText(Path.Combine(_directory, "support-work.csv"), supportTrace.ToString());
                    }
                    Require(Time.realtimeSinceStartup < end, "actual support block timeout / " + supporter.Phase + " / " + bootstrap.WorldNotice);
                    if (supporter.Phase == OfficeRuntimeAgentPhase.Working) supportSeated++;
                    yield return null;
                }
                Require(support.Status == SubcontractStatus.Completed && supportSeated > 10, "real support completion");
                long beforeBilling = state.Company.CashWon;
                state.UnbindStaminaRuntimeBridge(runtimeStamina);
                bootstrap.AdvanceTimeNow(product.NextBillingMinute + 1 - state.Time.ElapsedMinutes);
                state.BindStaminaRuntimeBridge(runtimeStamina);
                Require(product.BillingPeriod == 1 && product.LastPeriodRevenueWon >= 60000 &&
                    state.Company.CashWon == beforeBilling + product.LastPeriodRevenueWon, "weekly bill after actual support");
                var restored = GameSaveMapper.FromDto(GameSaveMapper.ToDto(state));
                new SimulationRunner(restored).AdvanceMinutes(0);
                Require(restored.Company.CashWon == state.Company.CashWon && restored.Growth.StarterProduct.BillingPeriod == 1,
                    "completed billing survives in-memory save roundtrip without duplicate cash");
                hud.OpenTabNow(MainNavigationTabId.Projects);
                yield return null;
                while (!ClickFeatureWhenReady(hud)) yield return null;
                yield return new WaitForSecondsRealtime(0.6f);
                ValidateText();
                ScreenCapture.CaptureScreenshot(Path.Combine(_directory, "07-weekly-billing.png"));
                yield return new WaitForSecondsRealtime(0.5f);
                _report.AppendLine($"checkpointIntegration=PASS actualDevelopmentHours=4 developmentSeated={developmentSeated} actualSupportHours=2 supportWorker={supporter.AgentId} supportSeated={supportSeated} firstSale=180000 weekRevenue={product.LastPeriodRevenueWon} customers={product.Customers}");
                _report.AppendLine("checkpointScope=lesson history and first 20 development hours seeded through core; normal 4x real work for final development/support; billing-only time jump; not uninterrupted full-week native play");
            }
        }

        private static Button FindButton(string name) => FindObjectsByType<Button>(FindObjectsSortMode.None)
            .FirstOrDefault(b => b.name == name && b.gameObject.activeInHierarchy);

        private float _featureWaitStarted = -1;
        private static string _lastClickDetail = string.Empty;

        private bool ClickFeatureWhenReady(MainNavigationHudPresenter hud)
        {
            if (Click(hud.GetFeatureButtonForQa("projects-products"), true))
            {
                _featureWaitStarted = -1;
                return true;
            }
            if (_featureWaitStarted < 0) _featureWaitStarted = Time.realtimeSinceStartup;
            Require(Time.realtimeSinceStartup - _featureWaitStarted < 3f, "feature button did not become clickable / " + _lastClickDetail);
            return false;
        }

        private static bool Click(Button button, bool waitUntilReady = false)
        {
            if (waitUntilReady && (button == null || !button.interactable)) return false;
            Require(button != null && button.interactable, "button missing/disabled");
            var scroll = button.GetComponentInParent<ScrollRect>();
            PointerEventData data = null;
            bool found = false;
            for (int step = 0; step <= 10; step++)
            {
                if (scroll != null) scroll.verticalNormalizedPosition = 1f - step / 10f;
                Canvas.ForceUpdateCanvases();
                var rect = (RectTransform)button.transform;
                var point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
                data = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
                var hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(data, hits);
                found = hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button;
                _lastClickDetail = button.name + " point=" + point + " screen=" + Screen.width + "x" + Screen.height +
                    " topHits=" + string.Join(";", hits.Take(3).Select(h => h.gameObject.name));
                if (found || scroll == null) break;
            }
            if (!found && waitUntilReady) return false;
            Require(found, "button is clipped/occluded: " + _lastClickDetail);
            ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler);
            return true;
        }

        private static void ValidateText()
        {
            Canvas.ForceUpdateCanvases();
            var card = GameObject.Find("Starter Product Workflow");
            Require(card != null, "workflow card missing");
            foreach (var text in card.GetComponentsInChildren<TMP_Text>())
            {
                text.ForceMeshUpdate();
                Require(!text.isTextOverflowing, "workflow text overflow: " + text.text);
            }
        }

        private void Finish(bool ok, string detail)
        {
            var report = "STARTER_PRODUCT_PLAYER_QA=" + (ok ? "PASS" : "FAIL") + "\n" + _report + detail;
            File.WriteAllText(Path.Combine(_directory, "result.txt"), report);
            Debug.Log(report);
            Application.Quit(ok ? 0 : 1);
        }

        private static void Require(bool ok, string message)
        {
            if (!ok) throw new InvalidOperationException("StarterProductPlayerQa: " + message);
        }
    }
}
