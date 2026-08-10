using System;
using System.IO;
using System.Linq;
using FamilyCompany.Infrastructure.Unity;
using FamilyCompany.Save;
using FamilyCompany.Simulation.Contracts;
using FamilyCompany.Simulation.Core;
using FamilyCompany.Simulation.Events;
using FamilyCompany.Simulation.Prototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public static class PrototypeValidation
    {
        [MenuItem("Family Company/Validate Prototype 0.1")]
        public static void Run()
        {
            try
            {
                ValidateStartingFamily();
                ValidateStableRandom();
                ValidateEventOrdering();
                ValidateTimeAndLedger();
                ValidateFourPersonContractScope();
                ValidateContractLifecycle();
                ValidateSaveRoundTrip();
                ValidateSaveSlots();
                ValidateWideFrontendSettings();
                ValidateAssetsAndScene();
                Debug.Log("FAMILY_COMPANY_VALIDATION: PASS");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("FAMILY_COMPANY_VALIDATION: FAIL");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        private static void ValidateStartingFamily()
        {
            var state = PrototypeStateFactory.Create();
            AssertEqual(14, state.Family.Get("player").AgeAt(state.Time), "player age");
            AssertEqual(20, state.Family.Get("older_sister").AgeAt(state.Time), "sister age");
            AssertEqual(46, state.Family.Get("father").AgeAt(state.Time), "father age");
            AssertEqual(44, state.Family.Get("mother").AgeAt(state.Time), "mother age");
        }

        private static void ValidateStableRandom()
        {
            AssertEqual(1726110163, StableRandom.StableHash31("family-company"), "stable hash fixture");
            AssertEqual(877381839, StableRandom.StableRandomWord31("family-company"), "random word fixture");
            AssertEqual(25, StableRandom.StableRandomInt("family-company", 37), "random int fixture");
            for (var bound = 1; bound <= 100; bound++)
            {
                var key = $"validation:{bound}";
                var first = StableRandom.StableRandomInt(key, bound);
                AssertEqual(first, StableRandom.StableRandomInt(key, bound), "random replay");
                if (first < 0 || first >= bound) throw new InvalidOperationException("Random result is out of bounds.");
            }
        }

        private static void ValidateEventOrdering()
        {
            var queue = new DeterministicEventQueue(new[]
            {
                new ScheduledEvent("z", 10, 1, "test"),
                new ScheduledEvent("b", 10, 0, "test"),
                new ScheduledEvent("a", 10, 0, "test"),
                new ScheduledEvent("early", 5, 9, "test")
            });
            var order = string.Join(",", queue.DequeueDue(10).Select(item => item.EventId));
            AssertEqual("early,a,b,z", order, "event order");
        }

        private static void ValidateTimeAndLedger()
        {
            var state = PrototypeStateFactory.Create();
            var runner = new SimulationRunner(state);
            var due = runner.AdvanceMinutes(60);
            AssertEqual(60L, state.Time.ElapsedMinutes, "time advance");
            AssertEqual(1, due.Count, "due event count");
            AssertEqual(5_000_000L, state.Company.CashWon, "opening cash");
            foreach (var transaction in state.Company.Ledger)
            {
                AssertEqual(transaction.TotalDebitWon, transaction.TotalCreditWon, "balanced ledger");
            }
        }

        private static void ValidateSaveRoundTrip()
        {
            var source = PrototypeStateFactory.Create(314159);
            var offer = BootstrapContractCatalog.CreateOffer(
                source.WorldSeed,
                "save-validation-client",
                "저장 검증용 고객사",
                7);
            var acceptance = source.Contracts.Accept(offer, source.Company, source.Time.ElapsedMinutes);
            AssertEqual(true, acceptance.Accepted, "save contract acceptance");
            var work = source.Contracts.RecordWork(
                offer.OfferId,
                "older_sister",
                Math.Min(3, offer.EstimatedPersonHours),
                source.Time.ElapsedMinutes,
                source.Family,
                source.Company);
            AssertEqual(true, work.Applied, "save contract partial work");
            new SimulationRunner(source).AdvanceMinutes(1500);
            var json = JsonUtility.ToJson(GameSaveMapper.ToDto(source));
            var restored = GameSaveMapper.FromDto(JsonUtility.FromJson<GameSaveDto>(json));
            AssertEqual(source.WorldSeed, restored.WorldSeed, "save seed");
            AssertEqual(source.Time.ElapsedMinutes, restored.Time.ElapsedMinutes, "save time");
            AssertEqual(source.Company.CashWon, restored.Company.CashWon, "save cash");
            AssertEqual(source.Family.Get("older_sister").Energy, restored.Family.Get("older_sister").Energy, "save sister energy");
            AssertEqual(source.Events.Count, restored.Events.Count, "save event count");
            AssertEqual(2, JsonUtility.FromJson<GameSaveDto>(json).schemaVersion, "save schema version");
            AssertEqual(source.Contracts.Contracts.Count, restored.Contracts.Contracts.Count, "save contract count");
            var restoredContract = restored.Contracts.Get(offer.OfferId);
            AssertEqual(acceptance.Contract.Status, restoredContract.Status, "save contract status");
            AssertEqual(acceptance.Contract.CompletedPersonHours, restoredContract.CompletedPersonHours, "save contract work");
            AssertEqual(acceptance.Contract.Contributions.Count, restoredContract.Contributions.Count, "save contract contributions");
        }

        private static void ValidateSaveSlots()
        {
            var directory = Path.Combine(Path.GetTempPath(), $"family-company-save-slots-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            try
            {
                for (var slot = UnityJsonSaveRepository.MinimumSlot; slot <= UnityJsonSaveRepository.MaximumSlot; slot++)
                {
                    var repository = new UnityJsonSaveRepository(slot, directory);
                    AssertEqual(false, repository.Exists, $"empty save slot {slot}");
                    var state = PrototypeStateFactory.Create(20000103 + slot);
                    new SimulationRunner(state).AdvanceMinutes(slot * 60);
                    repository.Save(GameSaveMapper.ToDto(state));
                    AssertEqual(true, repository.Exists, $"written save slot {slot}");
                    AssertEqual(true, repository.TryLoad(out var restored), $"load save slot {slot}");
                    AssertEqual(state.WorldSeed, restored.worldSeed, $"save slot {slot} seed");
                    AssertEqual(state.Time.ElapsedMinutes, restored.elapsedMinutes, $"save slot {slot} time");
                }

                var firstSlot = new UnityJsonSaveRepository(1, directory);
                firstSlot.Save(GameSaveMapper.ToDto(PrototypeStateFactory.Create(999)));
                AssertEqual(true, File.Exists(firstSlot.Location + ".bak"), "save slot backup");

                var legacyDirectory = Path.Combine(directory, "legacy");
                Directory.CreateDirectory(legacyDirectory);
                File.WriteAllText(
                    Path.Combine(legacyDirectory, "family-company-prototype-save.json"),
                    JsonUtility.ToJson(GameSaveMapper.ToDto(PrototypeStateFactory.Create(777)), true));
                var legacySlot = new UnityJsonSaveRepository(1, legacyDirectory);
                AssertEqual(true, legacySlot.Exists, "legacy save slot detection");
                AssertEqual(true, legacySlot.TryLoad(out var legacy), "legacy save slot load");
                AssertEqual(777, legacy.worldSeed, "legacy save slot seed");
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        private static void ValidateWideFrontendSettings()
        {
            var projectSettings = File.ReadAllText("ProjectSettings/ProjectSettings.asset");
            if (!projectSettings.Contains("defaultScreenWidth: 1920") ||
                !projectSettings.Contains("defaultScreenHeight: 1080") ||
                !projectSettings.Contains("defaultScreenWidthWeb: 1280") ||
                !projectSettings.Contains("defaultScreenHeightWeb: 720") ||
                !projectSettings.Contains("resizableWindow: 1") ||
                !projectSettings.Contains("allowFullscreenSwitch: 1") ||
                !projectSettings.Contains("fullscreenMode: 1"))
            {
                throw new InvalidOperationException("Wide fullscreen player settings are incomplete.");
            }
        }

        private static void ValidateContractLifecycle()
        {
            var state = PrototypeStateFactory.Create();
            var offer = new SubcontractOffer(
                "lifecycle-contract",
                "lifecycle-validation-client",
                "계약 생명주기 검증용 고객사",
                ContractServiceType.DataEntryAndQualityAssurance,
                "소형 상품 데이터 입력",
                4,
                20,
                7,
                100_000,
                900_000,
                0);
            var acceptance = state.Contracts.Accept(offer, state.Company, state.Time.ElapsedMinutes);
            AssertEqual(true, acceptance.Accepted, "contract accepted");
            AssertEqual(4_900_000L, state.Company.CashWon, "contract upfront cash");
            var memberIds = new[] { "player", "older_sister", "father", "mother" };
            ContractWorkResult finalWork = null;
            foreach (var memberId in memberIds)
            {
                finalWork = state.Contracts.RecordWork(
                    offer.OfferId,
                    memberId,
                    5,
                    state.Time.ElapsedMinutes,
                    state.Family,
                    state.Company);
            }

            AssertEqual(true, finalWork != null && finalWork.Completed, "contract completion");
            AssertEqual(900_000L, finalWork.RewardWon, "contract settlement reward");
            AssertEqual(5_800_000L, state.Company.CashWon, "contract settled cash");
            AssertEqual(2, state.Company.Reputation, "contract completion reputation");
            AssertEqual(SubcontractStatus.Completed, acceptance.Contract.Status, "contract completed status");
            AssertEqual(20, acceptance.Contract.CompletedPersonHours, "contract completed hours");
            AssertEqual(4, acceptance.Contract.Contributions.Count, "contract contributor count");

            var duplicate = state.Contracts.Accept(offer, state.Company, state.Time.ElapsedMinutes);
            AssertEqual(false, duplicate.Accepted, "duplicate contract acceptance");
            AssertEqual(ContractRejectionReason.DuplicateOffer, duplicate.Decision.RejectionReason, "duplicate contract reason");
            foreach (var transaction in state.Company.Ledger)
            {
                AssertEqual(transaction.TotalDebitWon, transaction.TotalCreditWon, "contract ledger balance");
            }

            var overdueState = PrototypeStateFactory.Create(20000104);
            overdueState.Company.ChangeReputation(10);
            var overdueOffer = new SubcontractOffer(
                "overdue-contract",
                "overdue-validation-client",
                "기한초과 검증용 고객사",
                ContractServiceType.WebsiteMaintenance,
                "긴급 홈페이지 갱신",
                2,
                16,
                1,
                50_000,
                500_000,
                0);
            AssertEqual(true, overdueState.Contracts.Accept(
                overdueOffer,
                overdueState.Company,
                overdueState.Time.ElapsedMinutes).Accepted, "overdue contract accepted");
            new SimulationRunner(overdueState).AdvanceMinutes(1441);
            AssertEqual(SubcontractStatus.Failed, overdueState.Contracts.Get(overdueOffer.OfferId).Status, "overdue contract failed");
            AssertEqual(8, overdueState.Company.Reputation, "overdue reputation penalty");
        }

        private static void ValidateFourPersonContractScope()
        {
            var state = PrototypeStateFactory.Create();
            var policy = new SmallTeamContractPolicy(state.Family.Members.Count);
            for (var sequence = 0; sequence < 32; sequence++)
            {
                var offer = BootstrapContractCatalog.CreateOffer(
                    state.WorldSeed,
                    "validation-client",
                    "계약 검증용 고객사",
                    sequence);
                var decision = policy.Evaluate(
                    offer,
                    state.Company.CashWon,
                    state.Company.Reputation,
                    0,
                    0);
                if (!decision.CanAccept)
                {
                    throw new InvalidOperationException(
                        $"Starter contract {offer.OfferId} was rejected: {decision.RejectionReason}");
                }

                if (offer.RequiredWorkers > 4 || offer.EstimatedPersonHours > 80 || offer.RewardWon > 2_500_000)
                {
                    throw new InvalidOperationException("Starter contract exceeds the four-person bootstrap scope.");
                }

                var replay = BootstrapContractCatalog.CreateOffer(
                    state.WorldSeed,
                    "validation-client",
                    "계약 검증용 고객사",
                    sequence);
                AssertEqual(offer.OfferId, replay.OfferId, "contract deterministic ID");
                AssertEqual(offer.ServiceType, replay.ServiceType, "contract deterministic template");
            }

            var oversized = new SubcontractOffer(
                "oversized",
                "validation-client",
                "계약 검증용 고객사",
                ContractServiceType.SmallBusinessTool,
                "대기업 전사 시스템 구축",
                12,
                1000,
                30,
                10_000_000,
                100_000_000,
                0);
            var rejected = policy.Evaluate(
                oversized,
                state.Company.CashWon,
                state.Company.Reputation,
                0,
                0);
            AssertEqual(false, rejected.CanAccept, "oversized contract acceptance");
            AssertEqual(ContractRejectionReason.TeamTooSmall, rejected.RejectionReason, "oversized contract reason");
        }

        private static void ValidateAssetsAndScene()
        {
            var sisterFrames = AssetDatabase.FindAssets("t:Sprite", new[] { PrototypeProjectBuilder.SisterFrameFolder });
            AssertEqual(8, sisterFrames.Length, "sister directional frame count");
            var playerFrames = AssetDatabase.FindAssets("t:Sprite", new[] { PrototypeProjectBuilder.PlayerFrameFolder });
            AssertEqual(8, playerFrames.Length, "player directional frame count");
            var officeModules = AssetDatabase.FindAssets("t:Sprite", new[] { PrototypeProjectBuilder.OfficeModuleFolder });
            AssertEqual(12, officeModules.Length, "office pixel module count");
            var titleHero = AssetDatabase.LoadAssetAtPath<Texture2D>(PrototypeProjectBuilder.TitleHeroAssetPath);
            if (titleHero == null || titleHero.width < 1600 || titleHero.height < 900)
            {
                throw new InvalidOperationException("Widescreen generated title hero is missing or too small.");
            }
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(PrototypeProjectBuilder.ScenePath);
            if (scene == null) throw new InvalidOperationException("Prototype scene is missing.");

            EditorSceneManager.OpenScene(PrototypeProjectBuilder.ScenePath);
            var camera = Camera.main;
            if (camera == null || !camera.orthographic)
            {
                throw new InvalidOperationException("Orthographic main camera is missing.");
            }

            if (camera.GetComponent<Presentation.Unity.PixelatedCameraEffect>() == null)
            {
                throw new InvalidOperationException("Pixelated camera effect is missing.");
            }

            var playerController = UnityEngine.Object.FindFirstObjectByType<Presentation.Unity.PrototypePlayerController>();
            if (playerController == null || playerController.GetComponent<Presentation.Unity.DirectionalSpriteAnimator>() == null)
            {
                throw new InvalidOperationException("Player pixel movement visual is missing.");
            }

            var bootstrap = UnityEngine.Object.FindFirstObjectByType<Presentation.Unity.PrototypeBootstrap>();
            if (bootstrap == null) throw new InvalidOperationException("Prototype bootstrap is missing.");
            bootstrap.InitializeNow();
            AssertEqual(Presentation.Unity.PrototypeUiScreen.MainMenu, bootstrap.UiScreen, "initial frontend screen");
            bootstrap.StartNewGameNow(2, false);
            AssertEqual(true, bootstrap.HasSession, "new game session");
            AssertEqual(2, bootstrap.ActiveSlot, "new game slot");
            AssertEqual(Presentation.Unity.PrototypeUiScreen.Playing, bootstrap.UiScreen, "new game frontend screen");
            bootstrap.ShowPauseMenuNow();
            AssertEqual(Presentation.Unity.PrototypeUiScreen.PauseMenu, bootstrap.UiScreen, "pause frontend screen");
            bootstrap.ResumeGameNow();
            AssertEqual(Presentation.Unity.PrototypeUiScreen.Playing, bootstrap.UiScreen, "resume frontend screen");
            var coordinator = bootstrap.InitializeOfficeTaskBridgeNow();
            var agents = UnityEngine.Object.FindObjectsByType<Presentation.Unity.OfficeWorkerAgent>(FindObjectsSortMode.None);
            if (agents.Length < 3)
            {
                throw new InvalidOperationException($"Expected at least three moving office agents, got {agents.Length}.");
            }

            foreach (var agent in agents)
            {
                if (agent.RouteCount < 4)
                {
                    throw new InvalidOperationException($"Agent {agent.AgentId} has an incomplete route.");
                }
            }

            if (agents.All(agent => agent.AgentId != "older_sister"))
            {
                throw new InvalidOperationException("Moving older sister agent is missing.");
            }

            if (agents.All(agent => agent.AgentId != "father") || agents.All(agent => agent.AgentId != "mother"))
            {
                throw new InvalidOperationException("Moving parent placeholder agents are missing.");
            }

            if (agents.Any(agent => agent.AgentId == "employee_a" || agent.AgentId == "employee_b"))
            {
                throw new InvalidOperationException("The four-person starting company still contains hired employee placeholders.");
            }

            foreach (var movingAgent in agents)
            {
                foreach (var candidate in agents)
                {
                    candidate.GetComponent<CharacterController>().enabled = candidate == movingAgent;
                }

                movingAgent.InitializeNow();
                var start = movingAgent.transform.position;
                for (var index = 0; index < 600; index++)
                {
                    movingAgent.Tick(0.05f);
                }

                if (Vector3.Distance(start, movingAgent.transform.position) < 0.5f || movingAgent.CompletedStops < 1)
                {
                    throw new InvalidOperationException(
                        $"Agent {movingAgent.AgentId} did not physically traverse the office route.");
                }
            }

            ValidatePhysicalContractWork(bootstrap, coordinator, agents);
        }

        private static void ValidatePhysicalContractWork(
            Presentation.Unity.PrototypeBootstrap bootstrap,
            Presentation.Unity.OfficeContractTaskCoordinator coordinator,
            Presentation.Unity.OfficeWorkerAgent[] agents)
        {
            if (coordinator == null) throw new InvalidOperationException("Office contract task coordinator is missing.");
            var offer = new SubcontractOffer(
                "physical-office-contract",
                "physical-validation-client",
                "실제 이동 검증용 고객사",
                ContractServiceType.WebsiteMaintenance,
                "홈페이지 출력물 최종 확인",
                1,
                4,
                2,
                50_000,
                300_000,
                0);
            var acceptance = bootstrap.State.Contracts.Accept(
                offer,
                bootstrap.State.Company,
                bootstrap.State.Time.ElapsedMinutes);
            AssertEqual(true, acceptance.Accepted, "physical contract accepted");

            var sister = agents.First(agent => agent.AgentId == "older_sister");
            foreach (var candidate in agents)
            {
                candidate.GetComponent<CharacterController>().enabled = candidate == sister;
            }

            sister.InitializeNow();
            coordinator.ResetAssignments();
            coordinator.InitializeNow();
            var start = sister.transform.position;
            AssertEqual(true, coordinator.AssignContractWork(offer.OfferId, "older_sister", 4), "physical contract assigned");
            for (var index = 0; index < 1600 && coordinator.CompletedTaskCount == 0; index++)
            {
                sister.Tick(0.05f);
            }

            if (Vector3.Distance(start, sister.transform.position) < 0.5f)
            {
                throw new InvalidOperationException("Assigned family member did not physically move to contract work.");
            }

            AssertEqual(1, coordinator.CompletedTaskCount, "physical task completion count");
            AssertEqual(offer.OfferId, coordinator.LastCompletedOfferId, "physical task offer ID");
            AssertEqual(true, coordinator.LastWorkResult != null && coordinator.LastWorkResult.Completed, "physical task contract completion");
            AssertEqual(SubcontractStatus.Completed, bootstrap.State.Contracts.Get(offer.OfferId).Status, "physical contract status");
        }

        private static void AssertEqual<T>(T expected, T actual, string label)
        {
            if (!Equals(expected, actual))
            {
                throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
            }
        }
    }
}
