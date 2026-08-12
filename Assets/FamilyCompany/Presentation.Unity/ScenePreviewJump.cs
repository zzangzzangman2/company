using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FamilyCompany.Presentation.Unity.OfficeGridView;
using FamilyCompany.Presentation.Unity.OfficeRuntime;
using FamilyCompany.Presentation.Unity.OfficeSeating;
using FamilyCompany.Simulation.Contracts;
using FamilyCompany.Simulation.Family;
using FamilyCompany.Simulation.Navigation;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace FamilyCompany.Presentation.Unity
{
    /// <summary>
    /// Keeps Prototype01's simulation and IMGUI management layer alive while the Starter Office
    /// tile scene becomes the only rendered world. F9 is a one-way recovery shortcut; the removed
    /// OfficeVisualV2 presentation is never restored.
    /// </summary>
    public sealed class ScenePreviewJump : MonoBehaviour
    {
        public const KeyCode JumpKey = KeyCode.F9;
        public const string PreviewSceneName = "OfficeTileMigrationPreview";

        private static ScenePreviewJump _instance;
        private bool _loading;
        private bool _tileOfficeActive;
        private Renderer[] _legacyRenderers = System.Array.Empty<Renderer>();
        private StarterOfficeRuntimeBootstrap _starterRuntime;
        private string _playerQaFailure = string.Empty;
        private int _playerQaExitCode;

        private static readonly string[] QaMemberIds =
            { "player", "older_sister", "father", "mother" };
        private static readonly string[] QaDirectionNames =
            { "South", "SouthWest", "West", "NorthWest", "North", "NorthEast", "East", "SouthEast" };
        private static readonly Vector2[] QaDirectionVectors =
        {
            new Vector2(0f, -1f), new Vector2(-1f, -1f),
            new Vector2(-1f, 0f), new Vector2(-1f, 1f),
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(1f, 0f), new Vector2(1f, -1f)
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInstall()
        {
            if (_instance != null || FindPreviewBuildIndex() < 0) return;
            var host = new GameObject("~StarterOfficeTileRuntime");
            if (Application.isPlaying) DontDestroyOnLoad(host);
            _instance = host.AddComponent<ScenePreviewJump>();
        }

        public static void ShowStarterOffice()
        {
            if (!Application.isPlaying) return;
            if (_instance == null)
            {
                AutoInstall();
                if (_instance == null)
                {
                    Debug.LogError("[StarterOfficeTileRuntime] 타일 사무실 씬이 빌드에 없습니다.");
                    return;
                }
            }

            _instance.BeginShowStarterOffice();
        }

        private void Start()
        {
            Debug.Log("[StarterOfficeTileRuntime] 처음하기/불러오기 = Starter 타일 사무실 · F2 = 배치 편집 · F9 = 단방향 복구");
            if (System.Array.IndexOf(
                    System.Environment.GetCommandLineArgs(),
                    "-familyCompanyTileRuntimeQa") >= 0)
                StartCoroutine(RunExtendedPlayerQa());
        }

        private void Update()
        {
            if (Input.GetKeyDown(JumpKey)) BeginShowStarterOffice();
        }

        private void LateUpdate()
        {
            if (!_tileOfficeActive) return;
            foreach (var renderer in _legacyRenderers)
            {
                if (renderer != null && renderer.enabled) renderer.enabled = false;
            }
        }

        private void BeginShowStarterOffice()
        {
            if (_tileOfficeActive)
            {
                var bootstrap = Object.FindFirstObjectByType<PrototypeBootstrap>();
                if (bootstrap != null) _starterRuntime?.Rebind(bootstrap);
                return;
            }
            if (_loading) return;
            StartCoroutine(LoadStarterOffice());
        }

        private IEnumerator LoadStarterOffice()
        {
            _loading = true;
            var previewScene = SceneManager.GetSceneByName(PreviewSceneName);
            if (!previewScene.isLoaded)
            {
                var operation = SceneManager.LoadSceneAsync(PreviewSceneName, LoadSceneMode.Additive);
                if (operation == null)
                {
                    _loading = false;
                    Debug.LogError("[StarterOfficeTileRuntime] 타일 사무실 씬 로드를 시작하지 못했습니다.");
                    yield break;
                }
                yield return operation;
                previewScene = SceneManager.GetSceneByName(PreviewSceneName);
            }

            if (!previewScene.IsValid() || !previewScene.isLoaded)
            {
                _loading = false;
                Debug.LogError("[StarterOfficeTileRuntime] 타일 사무실 씬 로드 후 검증에 실패했습니다.");
                yield break;
            }

            var bootstrap = FindBootstrap(previewScene);
            if (bootstrap == null)
            {
                _loading = false;
                Debug.LogError("[StarterOfficeTileRuntime] OfficeTileMigrationPreviewBootstrap이 없습니다.");
                yield break;
            }

            bootstrap.DestroyGeneratedPreview();

            Camera previewCamera = null;
            foreach (var root in previewScene.GetRootGameObjects())
            {
                var cameras = root.GetComponentsInChildren<Camera>(true);
                if (cameras.Length > 0 && previewCamera == null) previewCamera = cameras[0];
                foreach (var listener in root.GetComponentsInChildren<AudioListener>(true))
                    listener.enabled = false;
            }

            if (previewCamera == null)
            {
                _loading = false;
                Debug.LogError("[StarterOfficeTileRuntime] 타일 사무실 카메라가 없습니다.");
                yield break;
            }

            var legacyScene = SceneManager.GetSceneAt(0);
            _legacyRenderers = CollectRenderers(legacyScene);
            foreach (var renderer in _legacyRenderers)
                if (renderer != null) renderer.enabled = false;

            foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (camera == previewCamera) continue;
                camera.enabled = false;
                if (camera.CompareTag("MainCamera")) camera.tag = "Untagged";
            }

            previewCamera.tag = "MainCamera";
            previewCamera.enabled = true;
            var gameBootstrap = Object.FindFirstObjectByType<PrototypeBootstrap>();
            if (gameBootstrap == null)
            {
                _loading = false;
                Debug.LogError("[StarterOfficeTileRuntime] PrototypeBootstrap이 없습니다.");
                yield break;
            }
            _starterRuntime = bootstrap.GetComponent<StarterOfficeRuntimeBootstrap>();
            if (_starterRuntime == null)
                _starterRuntime = bootstrap.gameObject.AddComponent<StarterOfficeRuntimeBootstrap>();
            _starterRuntime.Configure(gameBootstrap, bootstrap, previewCamera, _legacyRenderers);
            var layoutEditor = _starterRuntime.GetComponent<OfficeLayoutEditModeController>();
            if (layoutEditor == null)
                layoutEditor = _starterRuntime.gameObject.AddComponent<OfficeLayoutEditModeController>();
            layoutEditor.Configure(_starterRuntime, previewCamera);
            if (!_starterRuntime.IsReady)
            {
                _loading = false;
                Debug.LogError("[StarterOfficeTileRuntime] Starter Office Runtime 구성에 실패했습니다.");
                yield break;
            }
            _tileOfficeActive = true;
            _loading = false;
            Debug.Log(
                "[StarterOfficeTileRuntime] PASS · StarterOfficeV1 기본 표시 · " +
                $"legacyRenderers={_legacyRenderers.Length} actors={_starterRuntime.Actors.Count}");
        }

        private IEnumerator RunPlayerQa()
        {
            yield return null;
            var bootstrap = Object.FindFirstObjectByType<PrototypeBootstrap>();
            if (bootstrap == null)
            {
                Debug.LogError("FAMILY_COMPANY_STARTER_TILE_MAIN_FLOW: FAIL · PrototypeBootstrap missing");
                Application.Quit(31);
                yield break;
            }

            bootstrap.StartNewGameNow(1, false);
            for (var frame = 0; frame < 900 && !_tileOfficeActive; frame++) yield return null;
            if (!_tileOfficeActive)
            {
                Debug.LogError("FAMILY_COMPANY_STARTER_TILE_MAIN_FLOW: FAIL · tile office activation timeout");
                Application.Quit(32);
                yield break;
            }

            if (_starterRuntime == null || !_starterRuntime.IsReady ||
                _starterRuntime.World == null || _starterRuntime.Actors.Count != 4)
            {
                Debug.LogError("FAMILY_COMPANY_STARTER_TILE_MAIN_FLOW: FAIL · Starter runtime invariant");
                Application.Quit(33);
                yield break;
            }

            Debug.Log(
                "FAMILY_COMPANY_STARTER_TILE_MAIN_FLOW: PASS · " +
                $"layoutHash={_starterRuntime.LayoutHash} furniture={_starterRuntime.World.Grid.Furniture.Count} " +
                $"characters={_starterRuntime.Actors.Count} legacyRenderers={_legacyRenderers.Length}");
            yield return null;
            Application.Quit(0);
        }

        private IEnumerator RunExtendedPlayerQa()
        {
            yield return null;
            PrototypeBootstrap bootstrap = Object.FindFirstObjectByType<PrototypeBootstrap>();
            if (bootstrap == null)
            {
                Debug.LogError("FAMILY_COMPANY_STARTER_TILE_MAIN_FLOW: FAIL | PrototypeBootstrap missing");
                Application.Quit(31);
                yield break;
            }

            bootstrap.StartNewGameNow(1, false);
            for (var frame = 0; frame < 900 && !_tileOfficeActive; frame++) yield return null;
            if (!_tileOfficeActive || _starterRuntime == null || !_starterRuntime.IsReady ||
                _starterRuntime.World == null || _starterRuntime.Actors.Count != 4)
            {
                Debug.LogError("FAMILY_COMPANY_STARTER_TILE_MAIN_FLOW: FAIL | Starter runtime activation timeout");
                Application.Quit(33);
                yield break;
            }

            float previousTimeScale = Time.timeScale;
            Time.timeScale = 4f;

            yield return RunAutonomousMeetingSeatingQa(bootstrap);
            if (QuitIfPlayerQaFailed(previousTimeScale)) yield break;
            yield return RunFourWayIntersectionQa();
            if (QuitIfPlayerQaFailed(previousTimeScale)) yield break;
            yield return RunRuntimeDeskPlacementQa();
            if (QuitIfPlayerQaFailed(previousTimeScale)) yield break;
            yield return RunNarrowCorridorQa();
            if (QuitIfPlayerQaFailed(previousTimeScale)) yield break;
            yield return RunEightDirectionMovementQa();
            if (QuitIfPlayerQaFailed(previousTimeScale)) yield break;

            _starterRuntime.ApplyLayoutForQa(OfficeGridLayouts.CreateStarterOfficeV1());
            yield return WaitForRuntimeReady(46, "restore StarterOfficeV1");
            if (QuitIfPlayerQaFailed(previousTimeScale)) yield break;
            yield return RunMicroActionDestinationQa();
            if (QuitIfPlayerQaFailed(previousTimeScale)) yield break;
            yield return RunPlayerCollisionQa();
            if (QuitIfPlayerQaFailed(previousTimeScale)) yield break;
            yield return RunFourSeatWorkQa();
            if (QuitIfPlayerQaFailed(previousTimeScale)) yield break;
            yield return RunContractAndSaveLoadQa(bootstrap);
            if (QuitIfPlayerQaFailed(previousTimeScale)) yield break;

            OfficeRuntimeOccupancy occupancy = _starterRuntime.World.Occupancy;
            Debug.Log(
                "FAMILY_COMPANY_STARTER_TILE_MAIN_FLOW: PASS | " +
                $"layoutHash={_starterRuntime.LayoutHash} furniture={_starterRuntime.World.Grid.Furniture.Count} " +
                $"characters={_starterRuntime.Actors.Count} legacyRenderers={_legacyRenderers.Length} " +
                $"replans={_starterRuntime.World.ReplanCount} arrivals={_starterRuntime.World.ArrivalCount} " +
                $"blockedStaticAttempts={occupancy.StaticViolationCount} " +
                $"blockedInteractionAttempts={occupancy.InteractionViolationCount} " +
                $"agentPenetrations={occupancy.AgentPenetrationCount}");
            Time.timeScale = previousTimeScale;
            yield return null;
            Application.Quit(0);
        }

        private IEnumerator RunAutonomousMeetingSeatingQa(PrototypeBootstrap bootstrap)
        {
            Dictionary<string, OfficeRuntimeAgent> actors = RequiredQaActors();
            if (actors == null) yield break;
            string[] meetingMembers = bootstrap.State.Family.Members
                .Where(member => !string.Equals(member.MemberId, "player", StringComparison.Ordinal) &&
                                 member.Autonomy.TargetLocation == OfficeSemanticLocation.MeetingRoom)
                .Select(member => member.MemberId)
                .OrderBy(memberId => memberId, StringComparer.Ordinal)
                .ToArray();
            if (meetingMembers.Length == 0)
            {
                FailPlayerQa(37, "seeded morning schedule did not exercise an autonomous NPC meeting");
                yield break;
            }

            float started = Time.time;
            while (Time.time - started < 45f && meetingMembers.Any(memberId =>
                       !actors[memberId].IsSeated ||
                       actors[memberId].CurrentActivity != OfficeActivity.Meeting))
                yield return null;

            foreach (string memberId in meetingMembers)
            {
                OfficeRuntimeAgent actor = actors[memberId];
                string expectedSeatId = "seat_" + memberId;
                if (!actor.IsSeated || actor.CurrentActivity != OfficeActivity.Meeting ||
                    !string.Equals(actor.ActiveSeatId, expectedSeatId, StringComparison.Ordinal))
                {
                    FailPlayerQa(
                        38,
                        $"autonomous meeting did not remain seated for {memberId}: " +
                        $"phase={actor.Phase} activity={actor.CurrentActivity} seat={actor.ActiveSeatId}");
                    yield break;
                }
                OfficeSeatSlot seat = _starterRuntime.World.Workstations.RequiredSeat(actor.ActiveSeatId);
                if (!_starterRuntime.World.FurniturePresenter.TryGetRenderer(
                        seat.ChairFurnitureId,
                        out SpriteRenderer chairRenderer) || !chairRenderer.enabled)
                {
                    FailPlayerQa(39, "occupied meeting chair renderer disappeared for " + memberId);
                    yield break;
                }
            }

            OfficeSeatSlot emptyPlayerSeat = _starterRuntime.World.Workstations.RequiredSeat("seat_player");
            if (!_starterRuntime.World.FurniturePresenter.TryGetRenderer(
                    emptyPlayerSeat.ChairFurnitureId,
                    out SpriteRenderer emptyChairBase) || !emptyChairBase.enabled ||
                !_starterRuntime.World.FurniturePresenter.FrontOverlayRenderers.TryGetValue(
                    emptyPlayerSeat.ChairFurnitureId,
                    out SpriteRenderer emptyChairFront) || !emptyChairFront.enabled)
            {
                FailPlayerQa(39, "unoccupied player chair did not retain its complete visible sprite");
                yield break;
            }

            string capturePath = QaArtifactPath("starter-office-autonomous-meeting-seated.png");
            if (!TryCaptureQaCameraFrame(capturePath, out string captureFailure))
            {
                FailPlayerQa(39, "autonomous meeting capture failed: " + captureFailure);
                yield break;
            }
            Debug.Log(
                "STARTER_OFFICE_AUTONOMOUS_MEETING_SEATING_QA_PASS | members=" +
                string.Join(",", meetingMembers) +
                " | activity=Meeting seatedAt=assigned-workstation " +
                "occupiedChairVisible=true emptyChairVisible=true | capture=" + capturePath);
        }

        private IEnumerator RunFourWayIntersectionQa()
        {
            Dictionary<string, OfficeRuntimeAgent> actors = RequiredQaActors();
            if (actors == null) yield break;
            var starts = new Dictionary<string, OfficeGridCoordinate>(StringComparer.Ordinal)
            {
                ["player"] = new OfficeGridCoordinate(6, 5),
                ["older_sister"] = new OfficeGridCoordinate(6, 7),
                ["father"] = new OfficeGridCoordinate(5, 6),
                ["mother"] = new OfficeGridCoordinate(7, 6)
            };
            var goals = new Dictionary<string, OfficeGridCoordinate>(StringComparer.Ordinal)
            {
                ["player"] = starts["older_sister"],
                ["older_sister"] = starts["player"],
                ["father"] = starts["mother"],
                ["mother"] = starts["father"]
            };
            foreach (string memberId in QaMemberIds) actors[memberId].QaTeleportToCell(starts[memberId]);
            _starterRuntime.World.Occupancy.ResetMetrics();
            int replansBefore = _starterRuntime.World.ReplanCount;
            int arrivalsBefore = _starterRuntime.World.ArrivalCount;
            foreach (string memberId in QaMemberIds)
            {
                if (actors[memberId].QaMoveToCell(goals[memberId], "four-way")) continue;
                FailPlayerQa(40, "four-way route could not be created for " + memberId);
                yield break;
            }

            float started = Time.time;
            while (Time.time - started < 60f &&
                   QaMemberIds.Any(memberId => !actors[memberId].QaReachedCell(goals[memberId])))
                yield return null;
            if (QaMemberIds.Any(memberId => !actors[memberId].QaReachedCell(goals[memberId])))
            {
                FailPlayerQa(
                    41,
                    "four-way crossing did not finish within 60 simulated seconds | " +
                    QaActorSummary(actors, goals) + " | " + OccupancyMetricSummary());
                yield break;
            }
            if (!RequireZeroActualViolations("four-way", 42)) yield break;
            Debug.Log(
                "STARTER_OFFICE_FOUR_WAY_QA_PASS | duration=" + (Time.time - started).ToString("F2") +
                " | " + RouteMetricSummary(replansBefore, arrivalsBefore) + " | " + OccupancyMetricSummary());
        }

        private IEnumerator RunRuntimeDeskPlacementQa()
        {
            Dictionary<string, OfficeRuntimeAgent> actors = RequiredQaActors();
            if (actors == null) yield break;
            actors["player"].QaTeleportToCell(new OfficeGridCoordinate(5, 6));
            actors["older_sister"].QaTeleportToCell(new OfficeGridCoordinate(9, 2));
            actors["father"].QaTeleportToCell(new OfficeGridCoordinate(1, 9));
            actors["mother"].QaTeleportToCell(new OfficeGridCoordinate(9, 6));
            if (!actors["player"].QaMoveToCell(new OfficeGridCoordinate(7, 6), "before-desk-placement"))
            {
                FailPlayerQa(43, "pre-placement route could not be created");
                yield break;
            }
            float motionStart = Time.time;
            while (Time.time - motionStart < 0.35f) yield return null;

            string previousHash = _starterRuntime.LayoutHash;
            _starterRuntime.ApplyLayoutForQa(CreateRuntimeDeskQaLayout());
            yield return WaitForRuntimeReady(44, "runtime desk placement");
            if (_playerQaFailure.Length > 0) yield break;
            if (string.Equals(previousHash, _starterRuntime.LayoutHash, StringComparison.Ordinal))
            {
                FailPlayerQa(44, "runtime desk placement did not revise the semantic layout hash");
                yield break;
            }

            actors = RequiredQaActors();
            if (actors == null) yield break;
            actors["player"].QaTeleportToCell(new OfficeGridCoordinate(5, 6));
            actors["older_sister"].QaTeleportToCell(new OfficeGridCoordinate(9, 2));
            actors["father"].QaTeleportToCell(new OfficeGridCoordinate(1, 9));
            actors["mother"].QaTeleportToCell(new OfficeGridCoordinate(9, 6));
            _starterRuntime.World.Occupancy.ResetMetrics();
            int replansBefore = _starterRuntime.World.ReplanCount;
            int arrivalsBefore = _starterRuntime.World.ArrivalCount;
            if (!actors["player"].QaMoveToCell(new OfficeGridCoordinate(7, 6), "after-desk-placement"))
            {
                FailPlayerQa(44, "post-placement detour route could not be created");
                yield break;
            }
            float started = Time.time;
            while (Time.time - started < 30f &&
                   !actors["player"].QaReachedCell(new OfficeGridCoordinate(7, 6)))
                yield return null;
            if (!actors["player"].QaReachedCell(new OfficeGridCoordinate(7, 6)))
            {
                FailPlayerQa(44, "post-placement route did not detour around the new desk");
                yield break;
            }
            if (!RequireZeroActualViolations("runtime-desk-placement", 44)) yield break;
            Debug.Log(
                "STARTER_OFFICE_RUNTIME_DESK_PLACEMENT_QA_PASS | previousHash=" + previousHash +
                " | revisedHash=" + _starterRuntime.LayoutHash + " | " +
                RouteMetricSummary(replansBefore, arrivalsBefore) + " | " + OccupancyMetricSummary());

            _starterRuntime.ApplyLayoutForQa(OfficeGridLayouts.CreateStarterOfficeV1());
            yield return WaitForRuntimeReady(45, "runtime desk removal");
            if (_playerQaFailure.Length > 0) yield break;
            actors = RequiredQaActors();
            if (actors == null) yield break;
            var reopenedStart = new OfficeGridCoordinate(5, 6);
            var reopenedCenter = new OfficeGridCoordinate(6, 6);
            var reopenedGoal = new OfficeGridCoordinate(7, 6);
            actors["player"].QaTeleportToCell(reopenedStart);
            actors["older_sister"].QaTeleportToCell(new OfficeGridCoordinate(9, 2));
            actors["father"].QaTeleportToCell(new OfficeGridCoordinate(1, 9));
            actors["mother"].QaTeleportToCell(new OfficeGridCoordinate(9, 6));
            IReadOnlyList<OfficeGridCoordinate> reopenedPath = _starterRuntime.World.FindPath(
                "player",
                reopenedStart,
                reopenedGoal,
                string.Empty);
            if (reopenedPath.Count != 3 || !reopenedPath[1].Equals(reopenedCenter) ||
                !actors["player"].QaMoveToCell(reopenedGoal, "after-desk-removal"))
            {
                FailPlayerQa(45, "desk removal did not reopen the direct center path");
                yield break;
            }
            started = Time.time;
            while (Time.time - started < 15f && !actors["player"].QaReachedCell(reopenedGoal))
                yield return null;
            if (!actors["player"].QaReachedCell(reopenedGoal))
            {
                FailPlayerQa(45, "player did not arrive after desk removal");
                yield break;
            }
            Debug.Log(
                "STARTER_OFFICE_RUNTIME_DESK_REMOVAL_QA_PASS | restoredHash=" +
                _starterRuntime.LayoutHash + " | directPathCells=" + reopenedPath.Count);
        }

        private IEnumerator RunNarrowCorridorQa()
        {
            _starterRuntime.ApplyLayoutForQa(CreateNarrowCorridorQaLayout());
            yield return WaitForRuntimeReady(47, "narrow corridor layout");
            if (_playerQaFailure.Length > 0) yield break;
            Dictionary<string, OfficeRuntimeAgent> actors = RequiredQaActors();
            if (actors == null) yield break;
            var playerStart = new OfficeGridCoordinate(3, 6);
            var sisterStart = new OfficeGridCoordinate(9, 6);
            var sisterGoal = new OfficeGridCoordinate(2, 6);
            actors["player"].QaTeleportToCell(playerStart);
            actors["older_sister"].QaTeleportToCell(sisterStart);
            actors["father"].QaTeleportToCell(new OfficeGridCoordinate(2, 2));
            actors["mother"].QaTeleportToCell(new OfficeGridCoordinate(10, 10));
            _starterRuntime.World.Occupancy.ResetMetrics();
            int replansBefore = _starterRuntime.World.ReplanCount;
            int arrivalsBefore = _starterRuntime.World.ArrivalCount;
            if (!actors["player"].QaMoveToCell(sisterStart, "narrow-corridor") ||
                !actors["older_sister"].QaMoveToCell(sisterGoal, "narrow-corridor"))
            {
                FailPlayerQa(48, "narrow corridor routes could not be created");
                yield break;
            }
            float started = Time.time;
            while (Time.time - started < 60f &&
                   (!actors["player"].QaReachedCell(sisterStart) ||
                    !actors["older_sister"].QaReachedCell(sisterGoal)))
                yield return null;
            if (!actors["player"].QaReachedCell(sisterStart) ||
                !actors["older_sister"].QaReachedCell(sisterGoal))
            {
                var goals = new Dictionary<string, OfficeGridCoordinate>(StringComparer.Ordinal)
                {
                    ["player"] = sisterStart,
                    ["older_sister"] = sisterGoal,
                    ["father"] = new OfficeGridCoordinate(2, 2),
                    ["mother"] = new OfficeGridCoordinate(10, 10)
                };
                FailPlayerQa(
                    49,
                    "narrow corridor deterministic yielding did not finish within 60 simulated seconds | " +
                    QaActorSummary(actors, goals) + " | " + OccupancyMetricSummary());
                yield break;
            }
            if (!RequireZeroActualViolations("narrow-corridor", 50)) yield break;
            Debug.Log(
                "STARTER_OFFICE_NARROW_CORRIDOR_QA_PASS | duration=" + (Time.time - started).ToString("F2") +
                " | " + RouteMetricSummary(replansBefore, arrivalsBefore) + " | " + OccupancyMetricSummary());
        }

        private IEnumerator RunEightDirectionMovementQa()
        {
            _starterRuntime.ApplyLayoutForQa(CreateDirectionQaLayout());
            yield return WaitForRuntimeReady(51, "open direction layout");
            if (_playerQaFailure.Length > 0) yield break;
            Dictionary<string, OfficeRuntimeAgent> actors = RequiredQaActors();
            if (actors == null) yield break;
            OfficeRuntimeAgent player = actors["player"];
            actors["older_sister"].QaTeleportToCell(new OfficeGridCoordinate(3, 3));
            actors["father"].QaTeleportToCell(new OfficeGridCoordinate(21, 3));
            actors["mother"].QaTeleportToCell(new OfficeGridCoordinate(21, 21));
            _starterRuntime.World.Occupancy.ResetMetrics();
            for (var direction = 0; direction < QaDirectionVectors.Length; direction++)
            {
                player.QaTeleportToCell(new OfficeGridCoordinate(12, 12));
                player.QaSetPlayerInput(QaDirectionVectors[direction]);
                Vector2 observedDisplacement = Vector2.zero;
                Vector2 observedFrameDisplacement = Vector2.zero;
                Vector2 observedSemanticDisplacement = Vector2.zero;
                float observedSpeed = 0f;
                int observedSemanticDirection = player.SemanticDirection;
                int observedMotionDirection = player.MotionDirection;
                int observedVisualDirection = player.CurrentDirection;
                int observedWalkFrame = player.CurrentWalkFrame;
                float observedGaitDistance = player.GaitDistance;
                float observedGaitPhase = player.GaitPhase01;
                OfficeLocomotionPhase observedLocomotionPhase = player.LocomotionPhase;
                bool observedProjection = false;
                string observedSprite = string.Empty;
                float started = Time.time;
                while (Time.time - started < 2f)
                {
                    yield return null;
                    if (player.LastActualDisplacement.sqrMagnitude > observedDisplacement.sqrMagnitude)
                        observedDisplacement = player.LastActualDisplacement;
                    if (player.AccumulatedFrameDisplacement.sqrMagnitude <= 0.0000001f) continue;
                    observedFrameDisplacement = player.AccumulatedFrameDisplacement;
                    observedSemanticDisplacement = player.SemanticFrameDisplacement;
                    observedSpeed = player.ActualPresentationSpeed;
                    observedSemanticDirection = player.SemanticDirection;
                    observedMotionDirection = player.MotionDirection;
                    observedVisualDirection = player.CurrentDirection;
                    observedWalkFrame = player.CurrentWalkFrame;
                    observedGaitDistance = player.GaitDistance;
                    observedGaitPhase = player.GaitPhase01;
                    observedLocomotionPhase = player.LocomotionPhase;
                    observedProjection = player.WasCollisionProjected;
                    observedSprite = player.CurrentSpriteName;
                }
                player.QaSetPlayerInput(Vector2.zero);
                yield return null;
                if (observedDisplacement.sqrMagnitude <= 0.0000001f)
                {
                    FailPlayerQa(51, "player produced no displacement for " + QaDirectionNames[direction]);
                    yield break;
                }
                int expected = DirectionalSpriteAnimator.ResolveTileDirection(observedDisplacement);
                int expectedWalkFrame = OfficeLocomotionGaitRules.DistanceFrame(
                    observedGaitDistance,
                    player.StrideLength,
                    6);
                float expectedGaitPhase = OfficeLocomotionGaitRules.Phase01(
                    observedGaitDistance,
                    player.StrideLength);
                if (expected != direction || player.CurrentDirection != direction ||
                    observedSemanticDirection != direction || observedMotionDirection != direction ||
                    observedVisualDirection != direction || observedProjection || observedSpeed < 1.4f ||
                    observedWalkFrame != expectedWalkFrame ||
                    Mathf.Abs(Mathf.DeltaAngle(observedGaitPhase * 360f, expectedGaitPhase * 360f)) > 0.05f ||
                    (observedLocomotionPhase != OfficeLocomotionPhase.StartStep &&
                     observedLocomotionPhase != OfficeLocomotionPhase.Walk))
                {
                    FailPlayerQa(
                        52,
                        $"direction mismatch {QaDirectionNames[direction]}: vector={observedDisplacement} " +
                        $"frame={observedFrameDisplacement} semantic={observedSemanticDisplacement} " +
                        $"expected={direction} math={expected} semanticDir={observedSemanticDirection} " +
                        $"motionDir={observedMotionDirection} visualDir={observedVisualDirection} " +
                        $"projected={observedProjection} speed={observedSpeed:F3} " +
                        $"locomotion={observedLocomotionPhase} gaitDistance={observedGaitDistance:F3} " +
                        $"gaitPhase={observedGaitPhase:F4}/{expectedGaitPhase:F4} " +
                        $"walkFrame={observedWalkFrame}/{expectedWalkFrame}");
                    yield break;
                }
                Debug.Log(
                    $"STARTER_OFFICE_DIRECTION_SAMPLE_PASS | index={direction} name={QaDirectionNames[direction]} " +
                    $"stepDisplacement={observedDisplacement} frameDisplacement={observedFrameDisplacement} " +
                    $"semanticDisplacement={observedSemanticDisplacement} actualSpeed={observedSpeed:F3} " +
                    $"semanticDir={observedSemanticDirection} motionDir={observedMotionDirection} " +
                    $"visualDir={observedVisualDirection} projected={observedProjection} " +
                    $"locomotion={observedLocomotionPhase} gaitDistance={observedGaitDistance:F3} " +
                    $"gaitPhase={observedGaitPhase:F4} walkFrame={observedWalkFrame} " +
                    $"spriteAssetPath=Assets/Art/Characters/Player/Pixel/HighMotion/Frames/{observedSprite}.png");
            }
            if (!RequireZeroActualViolations("eight-direction-player", 53)) yield break;
            Debug.Log("STARTER_OFFICE_EIGHT_DIRECTION_QA_PASS | samples=8 | " + OccupancyMetricSummary());
        }

        private IEnumerator RunPlayerCollisionQa()
        {
            Dictionary<string, OfficeRuntimeAgent> actors = RequiredQaActors();
            if (actors == null) yield break;
            OfficeRuntimeAgent player = actors["player"];
            var starts = new[]
            {
                new OfficeGridCoordinate(2, 5),
                new OfficeGridCoordinate(4, 2),
                new OfficeGridCoordinate(5, 6)
            };
            var targets = new[]
            {
                new OfficeGridCoordinate(2, 4),
                new OfficeGridCoordinate(4, 1),
                new OfficeGridCoordinate(7, 6)
            };
            var labels = new[] { "desk", "reception-counter", "npc" };
            for (var scenario = 0; scenario < labels.Length; scenario++)
            {
                player.QaTeleportToCell(starts[scenario]);
                actors["older_sister"].QaTeleportToCell(
                    scenario == 2 ? targets[scenario] : new OfficeGridCoordinate(9, 2));
                actors["father"].QaTeleportToCell(new OfficeGridCoordinate(1, 9));
                actors["mother"].QaTeleportToCell(new OfficeGridCoordinate(9, 6));
                _starterRuntime.World.Occupancy.ResetMetrics();
                Vector3 startWorld = _starterRuntime.World.Presenter.CellCenterWorld(starts[scenario]);
                Vector3 targetWorld = _starterRuntime.World.Presenter.CellCenterWorld(targets[scenario]);
                player.QaSetPlayerInput(
                    new Vector2(targetWorld.x - startWorld.x, targetWorld.y - startWorld.y).normalized);
                float started = Time.time;
                Vector2 previous = player.Position;
                float maximumFrameDisplacement = 0f;
                float mismatchedFacingSeconds = 0f;
                int reverseFacingFrames = 0;
                int projectedFrames = 0;
                while (Time.time - started < 10f)
                {
                    yield return null;
                    maximumFrameDisplacement = Mathf.Max(
                        maximumFrameDisplacement,
                        Vector2.Distance(previous, player.Position));
                    previous = player.Position;
                    if (player.AccumulatedFrameDisplacement.sqrMagnitude <= 0.0000001f) continue;
                    if (player.WasCollisionProjected) projectedFrames++;
                    int expectedDirection = player.UsedSemanticHeading
                        ? player.SemanticDirection
                        : player.MotionDirection;
                    int directionDelta = Mathf.Abs(player.CurrentDirection - expectedDirection);
                    directionDelta = Mathf.Min(directionDelta, DirectionalSpriteAnimator.DirectionCount - directionDelta);
                    if (directionDelta >= 3) reverseFacingFrames++;
                    if (directionDelta >= 2) mismatchedFacingSeconds += Time.deltaTime;
                    else mismatchedFacingSeconds = 0f;
                    if (reverseFacingFrames > 0 || mismatchedFacingSeconds > 0.15f)
                    {
                        FailPlayerQa(
                            65 + scenario,
                            $"player {labels[scenario]} facing diverged: semanticDir={player.SemanticDirection} " +
                            $"motionDir={player.MotionDirection} visualDir={player.CurrentDirection} " +
                            $"usedSemantic={player.UsedSemanticHeading} projected={player.WasCollisionProjected} " +
                            $"mismatchSeconds={mismatchedFacingSeconds:F3} reverseFrames={reverseFacingFrames}");
                        yield break;
                    }
                }
                player.QaSetPlayerInput(Vector2.zero);
                yield return null;
                if (!RequireZeroActualViolations("player-" + labels[scenario], 65 + scenario)) yield break;
                OfficeRuntimeOccupancy occupancy = _starterRuntime.World.Occupancy;
                bool collisionWasExercised = scenario == 2
                    ? occupancy.BlockedAgentMoveCount > 0
                    : occupancy.BlockedStaticMoveCount > 0;
                if (!collisionWasExercised || maximumFrameDisplacement > 0.50f)
                {
                    FailPlayerQa(
                        65 + scenario,
                        $"player {labels[scenario]} collision was not safely exercised: " +
                        $"maxFrameDelta={maximumFrameDisplacement:F4} {OccupancyMetricSummary()}");
                    yield break;
                }
                Debug.Log(
                    $"STARTER_OFFICE_PLAYER_COLLISION_SAMPLE_PASS | target={labels[scenario]} " +
                    $"duration=10.00 timeScale={Time.timeScale:F1} maxFrameDelta={maximumFrameDisplacement:F4} " +
                    $"projectedFrames={projectedFrames} reverseFacingFrames={reverseFacingFrames} " +
                    $"maxMismatchSeconds={mismatchedFacingSeconds:F3} replans=0 arrivals=0 | " +
                    OccupancyMetricSummary());
            }
            Debug.Log("STARTER_OFFICE_PLAYER_COLLISION_QA_PASS | scenarios=3 | timeScale=4");
        }

        private IEnumerator RunMicroActionDestinationQa()
        {
            Dictionary<string, OfficeRuntimeAgent> actors = RequiredQaActors();
            if (actors == null) yield break;
            OfficeRuntimeAgent player = actors["player"];
            var locations = new[]
            {
                OfficeSemanticLocation.Filing,
                OfficeSemanticLocation.Printer,
                OfficeSemanticLocation.Water,
                OfficeSemanticLocation.Coffee,
                OfficeSemanticLocation.OpenArea
            };
            foreach (OfficeSemanticLocation location in locations)
            {
                string scenarioId = "micro-destination-" + location;
                if (!_starterRuntime.World.Workstations.TryResolveDestination(
                        location,
                        "player",
                        scenarioId,
                        out OfficeRuntimeDestination expectedDestination))
                {
                    FailPlayerQa(72, "micro-action destination could not be resolved: " + location);
                    yield break;
                }

                var playerStart = new OfficeGridCoordinate(5, 6);
                player.QaTeleportToCell(playerStart);
                ParkQaActorsAwayFrom(actors, "player", playerStart, expectedDestination.Cell);
                // Teleports intentionally bypass traversal. Start the measurement only after every
                // actor is parked on a radius-clear cell so setup cannot pollute collision metrics.
                _starterRuntime.World.Occupancy.ResetMetrics();
                if (!player.QaBeginSemanticLocation(
                        location,
                        scenarioId,
                        out OfficeGridCoordinate destination))
                {
                    FailPlayerQa(72, "micro-action destination could not be resolved: " + location);
                    yield break;
                }
                if (!destination.Equals(expectedDestination.Cell))
                {
                    FailPlayerQa(
                        72,
                        $"micro-action destination changed during deterministic resolution: {location} " +
                        $"expected={expectedDestination.Cell} actual={destination}");
                    yield break;
                }

                float started = Time.time;
                while (Time.time - started < 20f && !player.QaReachedCell(destination))
                    yield return null;
                if (!player.QaReachedCell(destination))
                {
                    FailPlayerQa(
                        73,
                        $"micro-action destination was unreachable: {location} target={destination} " +
                        $"position={player.Position} phase={player.Phase} stuck={player.StuckSeconds:F2} | " +
                        OccupancyMetricSummary());
                    yield break;
                }
                if (!RequireZeroActualViolations("micro-destination-" + location, 74)) yield break;
                Debug.Log(
                    $"STARTER_OFFICE_MICRO_DESTINATION_SAMPLE_PASS | location={location} " +
                    $"cell={destination} | {OccupancyMetricSummary()}");
            }
            foreach (OfficeRuntimeAgent actor in actors.Values) actor.EndQaControl();
            Debug.Log(
                "STARTER_OFFICE_MICRO_DESTINATION_QA_PASS | " +
                "locations=Filing,Printer,Water,Coffee,OpenArea unreachable=0");
            yield return null;
        }

        private void ParkQaActorsAwayFrom(
            IReadOnlyDictionary<string, OfficeRuntimeAgent> actors,
            string activeMemberId,
            OfficeGridCoordinate activeStart,
            OfficeGridCoordinate destination)
        {
            OfficeRuntimeWorld world = _starterRuntime.World;
            Vector2 destinationWorld = world.Presenter.CellCenterWorld(destination);
            var reserved = new List<Vector2>
            {
                world.Presenter.CellCenterWorld(activeStart)
            };
            List<OfficeGridCoordinate> parkingCells = Enumerable.Range(1, world.Grid.Height - 2)
                .SelectMany(y => Enumerable.Range(1, world.Grid.Width - 2)
                    .Select(x => new OfficeGridCoordinate(x, y)))
                .Where(cell => world.Occupancy.IsCellPassable(cell, string.Empty, string.Empty, false))
                .Where(cell =>
                {
                    Vector2 center = world.Presenter.CellCenterWorld(cell);
                    return world.Occupancy.CanTraverseStatic(
                        center,
                        center,
                        OfficeRuntimeAgent.DefaultRadius,
                        string.Empty);
                })
                .OrderByDescending(cell =>
                    Vector2.SqrMagnitude((Vector2)world.Presenter.CellCenterWorld(cell) - destinationWorld))
                .ThenBy(cell => cell.Y)
                .ThenBy(cell => cell.X)
                .ToList();

            foreach (KeyValuePair<string, OfficeRuntimeAgent> item in actors
                         .Where(item => !string.Equals(item.Key, activeMemberId, StringComparison.Ordinal))
                         .OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                OfficeGridCoordinate parkingCell = parkingCells.First(cell =>
                {
                    Vector2 center = world.Presenter.CellCenterWorld(cell);
                    return Vector2.Distance(center, destinationWorld) >= 1.5f &&
                           reserved.All(position => Vector2.Distance(position, center) >= 1.0f);
                });
                item.Value.QaTeleportToCell(parkingCell);
                reserved.Add(world.Presenter.CellCenterWorld(parkingCell));
            }
        }

        private IEnumerator RunFourSeatWorkQa()
        {
            Dictionary<string, OfficeRuntimeAgent> actors = RequiredQaActors();
            if (actors == null) yield break;
            _starterRuntime.World.Occupancy.ResetMetrics();
            foreach (string memberId in QaMemberIds)
            {
                actors[memberId].BeginQaControl();
                if (actors[memberId].QaBeginSeatedWork("four-seat-work")) continue;
                FailPlayerQa(54, "seat work route could not be created for " + memberId);
                yield break;
            }
            float started = Time.time;
            while (Time.time - started < 45f && QaMemberIds.Any(memberId => !actors[memberId].IsSeated))
                yield return null;
            if (QaMemberIds.Any(memberId => !actors[memberId].IsSeated))
            {
                var goals = QaMemberIds.ToDictionary(
                    memberId => memberId,
                    memberId => _starterRuntime.World.Occupancy.CurrentCell(memberId),
                    StringComparer.Ordinal);
                FailPlayerQa(
                    55,
                    "all four assigned workstations were not seated within 45 simulated seconds | " +
                    QaActorSummary(actors, goals) + " | seats=" +
                    string.Join(",", QaMemberIds.Select(memberId =>
                        memberId + ":" + actors[memberId].ActiveSeatId)) + " | " +
                    OccupancyMetricSummary());
                yield break;
            }
            float workLoopStarted = Time.time;
            while (Time.time - workLoopStarted < 8f &&
                   QaMemberIds.Any(memberId => actors[memberId].ObservedWorkFrameCount < 6))
                yield return null;
            if (QaMemberIds.Any(memberId => actors[memberId].ObservedSitDownFrameCount < 4 ||
                                                actors[memberId].ObservedWorkFrameCount < 6))
            {
                FailPlayerQa(
                    56,
                    "animated seating did not expose every SitDown/Work frame: " +
                    string.Join(",", QaMemberIds.Select(memberId =>
                        $"{memberId}=sit{actors[memberId].ObservedSitDownFrameCount}/work{actors[memberId].ObservedWorkFrameCount}")));
                yield break;
            }
            string[] claims = QaMemberIds.Select(memberId => actors[memberId].ActiveSeatId).ToArray();
            if (claims.Any(string.IsNullOrWhiteSpace) || claims.Distinct(StringComparer.Ordinal).Count() != 4)
            {
                FailPlayerQa(56, "seat claims are missing or duplicated: " + string.Join(",", claims));
                yield break;
            }
            foreach (string memberId in QaMemberIds)
            {
                OfficeRuntimeAgent actor = actors[memberId];
                OfficeSeatSlot seat = _starterRuntime.World.Workstations.RequiredSeat(actor.ActiveSeatId);
                string expectedSpritePrefix = memberId + "_northwest_sit_work_";
                int actualOrder = actor.PresentationRenderer == null
                    ? int.MinValue
                    : actor.PresentationRenderer.sortingOrder;
                int chairOrder = int.MaxValue;
                int deskOrder = int.MaxValue;
                if (_starterRuntime.World.FurniturePresenter.TryGetRenderer(
                        seat.ChairFurnitureId, out SpriteRenderer chairRenderer))
                    chairOrder = chairRenderer.sortingOrder;
                if (seat.HasWorkstationBinding &&
                    _starterRuntime.World.FurniturePresenter.TryGetRenderer(
                        seat.WorkSurfaceFurnitureId, out SpriteRenderer deskRenderer))
                    deskOrder = deskRenderer.sortingOrder;
                bool depthCorrect = actualOrder > chairOrder && actualOrder > deskOrder;
                Debug.Log(
                    $"STARTER_OFFICE_WORKSTATION_ALIGNMENT_SAMPLE | member={memberId} " +
                    $"seatContact={actor.SeatContactErrorPx:F3}px chairDesk={actor.ChairDeskErrorPx:F3}px " +
                    $"rotation={actor.VisualRotationErrorDegrees:F4}deg " +
                    $"scaleDeviation={actor.VisualScaleDeviation:P3} direction={actor.CurrentDirection} " +
                    $"sprite={actor.CurrentSpriteName} mode={actor.SeatingPresentationMode} " +
                    $"frames={actor.ObservedSitDownFrameCount}/4,{actor.ObservedWorkFrameCount}/6 " +
                    $"anchorError={actor.MaxAnimatedAnchorErrorPx:F3}px " +
                    $"pelvisStep={actor.MaxTransitionPelvisStepPx:F3}px " +
                    $"monotonicViolations={actor.TransitionMonotonicViolationCount} " +
                    $"sorting={actualOrder} chair={chairOrder} desk={deskOrder}");
                bool presentationMatches = actor.SeatContactErrorPx <= 1f &&
                    actor.VisualRotationErrorDegrees <= 0.01f &&
                    actor.VisualScaleDeviation <= 0.001f && actor.CurrentDirection == 3 &&
                    actor.SeatingPresentationMode == OfficeSeatingPresentationMode.Animated &&
                    actor.ObservedSitDownFrameCount == 4 && actor.ObservedWorkFrameCount == 6 &&
                    actor.MaxAnimatedAnchorErrorPx <= 1f &&
                    actor.MaxTransitionPelvisStepPx <= 2f &&
                    actor.TransitionMonotonicViolationCount == 0 &&
                    actor.CurrentSpriteName.StartsWith(expectedSpritePrefix, StringComparison.Ordinal) &&
                    depthCorrect;
                if (presentationMatches) continue;
                FailPlayerQa(
                    57,
                    $"seated contact placement failed for {memberId}: " +
                    $"seatContact={actor.SeatContactErrorPx:F2}px rotation={actor.VisualRotationErrorDegrees:F4}deg " +
                        $"scaleDeviation={actor.VisualScaleDeviation:P3} direction={actor.CurrentDirection} " +
                        $"sprite={actor.CurrentSpriteName} mode={actor.SeatingPresentationMode} " +
                        $"frames={actor.ObservedSitDownFrameCount}/4,{actor.ObservedWorkFrameCount}/6 " +
                        $"anchorError={actor.MaxAnimatedAnchorErrorPx:F3}px " +
                        $"pelvisStep={actor.MaxTransitionPelvisStepPx:F3}px " +
                        $"monotonicViolations={actor.TransitionMonotonicViolationCount} " +
                        $"sorting={actualOrder} chair={chairOrder} desk={deskOrder}");
                yield break;
            }
            string capturePath = QaArtifactPath("starter-office-four-seat-work.png");
            if (!TryCaptureQaCameraFrame(capturePath, out string captureFailure))
            {
                FailPlayerQa(58, "four-seat visual capture failed: " + captureFailure);
                yield break;
            }
            Debug.Log("STARTER_OFFICE_FOUR_SEAT_CAPTURE | path=" + capturePath);
            foreach (string memberId in QaMemberIds)
            {
                if (TryCaptureQaWorkstationCloseup(memberId, actors[memberId], out string closeupPath,
                        out string closeupFailure))
                {
                    Debug.Log($"SEATED_SPRITE_ROOT_CAUSE_V3_CLOSEUP | member={memberId} path={closeupPath}");
                    continue;
                }
                FailPlayerQa(58, $"{memberId} workstation close-up failed: {closeupFailure}");
                yield break;
            }
            foreach (string memberId in QaMemberIds)
            {
                if (TryCaptureQaChairOverlayComparison(
                        memberId,
                        actors[memberId],
                        out string overlayOnPath,
                        out string overlayOffPath,
                        out string overlayFailure))
                {
                    Debug.Log(
                        $"STARTER_OFFICE_CHAIR_OVERLAY_COMPARISON | member={memberId} " +
                        $"on={overlayOnPath} off={overlayOffPath}");
                    continue;
                }
                FailPlayerQa(58, $"{memberId} chair overlay comparison failed: {overlayFailure}");
                yield break;
            }
            if (!RequireZeroActualViolations("four-seat-work", 58)) yield break;
            foreach (string memberId in QaMemberIds)
            {
                if (actors[memberId].QaRequestStand()) continue;
                FailPlayerQa(58, "animated stand-up could not begin for " + memberId);
                yield break;
            }
            float standStarted = Time.time;
            while (Time.time - standStarted < 12f &&
                   QaMemberIds.Any(memberId => actors[memberId].ObservedStandUpFrameCount < 4))
                yield return null;
            if (QaMemberIds.Any(memberId => actors[memberId].ObservedStandUpFrameCount < 4))
            {
                FailPlayerQa(
                    58,
                    "animated seating did not expose every StandUp frame: " +
                    string.Join(",", QaMemberIds.Select(memberId =>
                        $"{memberId}=stand{actors[memberId].ObservedStandUpFrameCount}")));
                yield break;
            }
            if (QaMemberIds.Any(memberId =>
                    actors[memberId].MaxTransitionPelvisStepPx > 2f ||
                    actors[memberId].TransitionMonotonicViolationCount != 0))
            {
                FailPlayerQa(
                    58,
                    "continuous seating motion failed: " +
                    string.Join(",", QaMemberIds.Select(memberId =>
                        $"{memberId}=maxStep{actors[memberId].MaxTransitionPelvisStepPx:F3}px/" +
                        $"reverse{actors[memberId].TransitionMonotonicViolationCount}")));
                yield break;
            }
            Debug.Log(
                "STARTER_OFFICE_FOUR_SEAT_WORK_QA_PASS | seats=" + string.Join(",", claims) +
                " | animation=4x(SitDown4+Work6+StandUp4) mode=Animated " +
                "placement=continuous,maxPelvisStep<=2px,monotonic,anchorError<=1px," +
                "seatContact<=1px,rotation=0,scale=canonical,sorting=chairFloor+1 | " +
                OccupancyMetricSummary());
            foreach (OfficeRuntimeAgent actor in actors.Values) actor.EndQaControl();
            yield return null;
        }

        private static string QaArtifactPath(string fileName)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index < arguments.Length - 1; index++)
            {
                if (!string.Equals(arguments[index], "-logFile", StringComparison.OrdinalIgnoreCase)) continue;
                string directory = Path.GetDirectoryName(Path.GetFullPath(arguments[index + 1]));
                if (!string.IsNullOrWhiteSpace(directory)) return Path.Combine(directory, fileName);
            }
            return Path.Combine(Application.persistentDataPath, fileName);
        }

        private string RouteMetricSummary(int replansBefore, int arrivalsBefore)
        {
            return $"replans={_starterRuntime.World.ReplanCount - replansBefore} " +
                   $"arrivals={_starterRuntime.World.ArrivalCount - arrivalsBefore}";
        }

        private static bool TryCaptureQaCameraFrame(string path, out string failure)
        {
            return TryCaptureQaCameraFrame(path, 1392, 699, out failure);
        }

        private bool TryCaptureQaWorkstationCloseup(
            string memberId,
            OfficeRuntimeAgent actor,
            out string path,
            out string failure)
        {
            path = QaArtifactPath(memberId.Replace('_', '-') + "-work-closeup.png");
            failure = string.Empty;
            Camera camera = Camera.main;
            if (camera == null)
            {
                failure = "Camera.main is missing";
                return false;
            }
            if (actor == null || actor.PresentationRenderer == null)
            {
                failure = "actor presentation renderer is missing";
                return false;
            }

            OfficeSeatSlot seat = _starterRuntime.World.Workstations.RequiredSeat(actor.ActiveSeatId);
            Bounds bounds = actor.PresentationRenderer.bounds;
            EncapsulateFurnitureRenderers(seat.ChairFurnitureId, ref bounds);
            if (seat.HasWorkstationBinding) EncapsulateFurnitureRenderers(seat.WorkSurfaceFurnitureId, ref bounds);

            Vector3 previousPosition = camera.transform.position;
            Quaternion previousRotation = camera.transform.rotation;
            float previousSize = camera.orthographicSize;
            try
            {
                camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, previousPosition.z);
                camera.orthographicSize = Mathf.Max(1.1f, Mathf.Max(bounds.extents.x, bounds.extents.y) * 1.18f);
                return TryCaptureQaCameraFrame(path, 1024, 1024, out failure);
            }
            finally
            {
                camera.transform.position = previousPosition;
                camera.transform.rotation = previousRotation;
                camera.orthographicSize = previousSize;
            }
        }

        private bool TryCaptureQaChairOverlayComparison(
            string memberId,
            OfficeRuntimeAgent actor,
            out string overlayOnPath,
            out string overlayOffPath,
            out string failure)
        {
            string stem = memberId.Replace('_', '-');
            overlayOnPath = QaArtifactPath(stem + "-chair-overlay-on.png");
            overlayOffPath = QaArtifactPath(stem + "-chair-overlay-off.png");
            failure = string.Empty;
            Camera camera = Camera.main;
            if (camera == null || actor == null || actor.PresentationRenderer == null)
            {
                failure = "camera or actor presentation renderer is missing";
                return false;
            }

            OfficeSeatSlot seat = _starterRuntime.World.Workstations.RequiredSeat(actor.ActiveSeatId);
            if (!_starterRuntime.World.FurniturePresenter.FrontOverlayRenderers.TryGetValue(
                    seat.ChairFurnitureId,
                    out SpriteRenderer chairOverlay) || chairOverlay == null)
            {
                failure = "chair front overlay renderer is missing for " + seat.ChairFurnitureId;
                return false;
            }

            bool previousOverlayEnabled = chairOverlay.enabled;
            Vector3 previousPosition = camera.transform.position;
            Quaternion previousRotation = camera.transform.rotation;
            float previousSize = camera.orthographicSize;
            try
            {
                chairOverlay.enabled = true;
                Bounds bounds = actor.PresentationRenderer.bounds;
                EncapsulateFurnitureRenderers(seat.ChairFurnitureId, ref bounds);
                if (seat.HasWorkstationBinding)
                    EncapsulateFurnitureRenderers(seat.WorkSurfaceFurnitureId, ref bounds);
                camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, previousPosition.z);
                camera.orthographicSize = Mathf.Max(
                    1.1f,
                    Mathf.Max(bounds.extents.x, bounds.extents.y) * 1.18f);
                if (!TryCaptureQaCameraFrame(overlayOnPath, 1024, 1024, out failure)) return false;
                chairOverlay.enabled = false;
                return TryCaptureQaCameraFrame(overlayOffPath, 1024, 1024, out failure);
            }
            finally
            {
                chairOverlay.enabled = previousOverlayEnabled;
                camera.transform.position = previousPosition;
                camera.transform.rotation = previousRotation;
                camera.orthographicSize = previousSize;
            }
        }

        private void EncapsulateFurnitureRenderers(string furnitureId, ref Bounds bounds)
        {
            if (!_starterRuntime.World.FurniturePresenter.TryGetSemanticRoot(furnitureId, out Transform root)) return;
            foreach (SpriteRenderer renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.enabled && renderer.gameObject.activeInHierarchy) bounds.Encapsulate(renderer.bounds);
            }
        }

        private static bool TryCaptureQaCameraFrame(string path, int width, int height, out string failure)
        {
            failure = string.Empty;
            Camera camera = Camera.main;
            if (camera == null)
            {
                failure = "Camera.main is missing";
                return false;
            }

            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                pixels.Apply(false, false);
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
                File.WriteAllBytes(path, pixels.EncodeToPNG());
                if (!File.Exists(path) || new FileInfo(path).Length == 0)
                {
                    failure = "capture file is missing or empty";
                    return false;
                }
                return true;
            }
            catch (Exception exception)
            {
                failure = exception.GetType().Name + ": " + exception.Message;
                return false;
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                target.Release();
                Object.Destroy(target);
                Object.Destroy(pixels);
            }
        }

        private IEnumerator RunContractAndSaveLoadQa(PrototypeBootstrap bootstrap)
        {
            SubcontractOffer offer = BootstrapContractCatalog.CreateOffer(
                bootstrap.State.WorldSeed,
                "starter-runtime-qa-client",
                "Starter Runtime QA Client",
                0);
            bootstrap.AcceptOfferNow(offer);
            SubcontractState contract = bootstrap.State.Contracts.Get(offer.OfferId);
            if (contract.Status != SubcontractStatus.Active)
            {
                FailPlayerQa(59, "QA contract was not accepted");
                yield break;
            }
            OfficeContractTaskCoordinator coordinator = Object.FindFirstObjectByType<OfficeContractTaskCoordinator>();
            int completedBefore = coordinator == null ? 0 : coordinator.CompletedTaskCount;
            bootstrap.AssignContractWorkNow(offer.OfferId, "mother");
            if (coordinator == null || coordinator.PendingCount == 0)
            {
                FailPlayerQa(
                    60,
                    "runtime contract assignment was not queued" +
                    (coordinator == null ? string.Empty : ": " + coordinator.LastAssignmentFailureLabel));
                yield break;
            }
            float started = Time.time;
            while (Time.time - started < 45f && coordinator.CompletedTaskCount == completedBefore)
                yield return null;
            if (coordinator.CompletedTaskCount == completedBefore ||
                !string.Equals(coordinator.LastCompletedOfferId, offer.OfferId, StringComparison.Ordinal))
            {
                FailPlayerQa(61, "runtime contract work did not complete through the canonical mother actor");
                yield break;
            }

            const int qaSaveSlot = 3;
            string savedLayoutHash = bootstrap.State.OfficeGrid.ComputeLayoutHash();
            long savedMinutes = bootstrap.State.Time.ElapsedMinutes;
            if (!bootstrap.SaveSlotNow(qaSaveSlot))
            {
                FailPlayerQa(62, "slot 3 save failed: " + bootstrap.WorldNotice);
                yield break;
            }
            bootstrap.AdvanceTimeNow(15);
            if (!bootstrap.LoadSlotNow(qaSaveSlot))
            {
                FailPlayerQa(63, "slot 3 load failed: " + bootstrap.WorldNotice);
                yield break;
            }
            for (var frame = 0; frame < 60; frame++) yield return null;
            string restoredStateHash = bootstrap.State.OfficeGrid.ComputeLayoutHash();
            if (bootstrap.State.Time.ElapsedMinutes != savedMinutes ||
                !string.Equals(restoredStateHash, savedLayoutHash, StringComparison.Ordinal) ||
                !string.Equals(_starterRuntime.LayoutHash, savedLayoutHash, StringComparison.Ordinal))
            {
                FailPlayerQa(
                    64,
                    $"save/load runtime mismatch: minutes={bootstrap.State.Time.ElapsedMinutes}/{savedMinutes} " +
                    $"stateHash={restoredStateHash} runtimeHash={_starterRuntime.LayoutHash} expected={savedLayoutHash}");
                yield break;
            }
            Debug.Log(
                "STARTER_OFFICE_CONTRACT_SAVE_LOAD_QA_PASS | offer=" + offer.OfferId +
                " | member=mother | slot=3 | layoutHash=" + savedLayoutHash +
                " | elapsedMinutes=" + savedMinutes);
        }

        private IEnumerator WaitForRuntimeReady(int exitCode, string label)
        {
            for (var frame = 0; frame < 900 && (_starterRuntime == null || !_starterRuntime.IsReady); frame++)
                yield return null;
            if (_starterRuntime == null || !_starterRuntime.IsReady || _starterRuntime.World == null)
                FailPlayerQa(exitCode, label + " rebuild timed out");
        }

        private Dictionary<string, OfficeRuntimeAgent> RequiredQaActors()
        {
            if (_starterRuntime == null || !_starterRuntime.IsReady || _starterRuntime.World == null)
            {
                FailPlayerQa(35, "Starter runtime is not ready for a QA scenario");
                return null;
            }
            Dictionary<string, OfficeRuntimeAgent> result = _starterRuntime.Actors
                .Where(item => item != null)
                .ToDictionary(item => item.AgentId, StringComparer.Ordinal);
            if (result.Count != QaMemberIds.Length || QaMemberIds.Any(memberId => !result.ContainsKey(memberId)))
            {
                FailPlayerQa(36, "canonical four-actor registry changed during QA");
                return null;
            }
            return result;
        }

        private string QaActorSummary(
            IReadOnlyDictionary<string, OfficeRuntimeAgent> actors,
            IReadOnlyDictionary<string, OfficeGridCoordinate> goals)
        {
            return string.Join(
                " ; ",
                QaMemberIds.Select(memberId =>
                {
                    OfficeRuntimeAgent actor = actors[memberId];
                    OfficeGridCoordinate cell = _starterRuntime.World.Occupancy.CurrentCell(memberId);
                    return $"{memberId}:cell={cell},goal={goals[memberId]},position={actor.Position}," +
                           $"phase={actor.Phase},stuck={actor.StuckSeconds:F2},desired={actor.DesiredVelocity}";
                }));
        }

        private bool RequireZeroActualViolations(string scenario, int exitCode)
        {
            OfficeRuntimeOccupancy occupancy = _starterRuntime.World.Occupancy;
            if (occupancy.StaticViolationCount == 0 && occupancy.InteractionViolationCount == 0 &&
                occupancy.AgentPenetrationCount == 0) return true;
            FailPlayerQa(exitCode, scenario + " recorded actual occupancy violations: " + OccupancyMetricSummary());
            return false;
        }

        private string OccupancyMetricSummary()
        {
            OfficeRuntimeOccupancy occupancy = _starterRuntime.World.Occupancy;
            return $"static={occupancy.StaticViolationCount} interaction={occupancy.InteractionViolationCount} " +
                   $"penetration={occupancy.AgentPenetrationCount} blockedStatic={occupancy.BlockedStaticMoveCount} " +
                   $"blockedInteraction={occupancy.BlockedInteractionMoveCount} " +
                   $"blockedAgent={occupancy.BlockedAgentMoveCount} " +
                   $"minimumSeparationMargin={occupancy.MinimumAgentSeparationMargin:F4}";
        }

        private static OfficeGrid CreateRuntimeDeskQaLayout()
        {
            OfficeGrid source = OfficeGridLayouts.CreateStarterOfficeV1();
            bool[] walkable = source.CopyWalkable();
            var blockedCell = new OfficeGridCoordinate(6, 6);
            walkable[blockedCell.Y * source.Width + blockedCell.X] = false;
            List<PlacedOfficeFurniture> furniture = source.Furniture.ToList();
            furniture.Add(new PlacedOfficeFurniture(
                "qa_runtime_desk",
                OfficeGridLayouts.DeskWithPcKind,
                blockedCell,
                1,
                1,
                OfficeFurnitureFacing.SouthEast,
                true));
            return new OfficeGrid(
                source.Width,
                source.Height,
                source.CopyFloorTiles(),
                walkable,
                furniture,
                source.SeatSlots);
        }

        private static OfficeGrid CreateNarrowCorridorQaLayout()
        {
            const int width = 13;
            const int height = 13;
            var floor = new OfficeFloorTileKind[width * height];
            var walkable = new bool[floor.Length];
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                int index = y * width + x;
                floor[index] = (OfficeFloorTileKind)(1 + (x * 3 + y * 5) % 3);
                bool leftRoom = x >= 1 && x <= 4 && y >= 1 && y <= 11;
                bool rightRoom = x >= 8 && x <= 11 && y >= 1 && y <= 11;
                bool corridor = y == 6 && x >= 4 && x <= 8;
                walkable[index] = leftRoom || rightRoom || corridor;
            }
            return new OfficeGrid(width, height, floor, walkable);
        }

        private static OfficeGrid CreateDirectionQaLayout()
        {
            const int width = 25;
            const int height = 25;
            var floor = new OfficeFloorTileKind[width * height];
            var walkable = new bool[floor.Length];
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                int index = y * width + x;
                floor[index] = (OfficeFloorTileKind)(1 + (x * 3 + y * 5) % 3);
                walkable[index] = x > 0 && x < width - 1 && y > 0 && y < height - 1;
            }
            return new OfficeGrid(width, height, floor, walkable);
        }

        private void FailPlayerQa(int exitCode, string message)
        {
            if (_playerQaFailure.Length > 0) return;
            _playerQaExitCode = exitCode;
            _playerQaFailure = message ?? "unknown failure";
        }

        private bool QuitIfPlayerQaFailed(float previousTimeScale)
        {
            if (_playerQaFailure.Length == 0) return false;
            Debug.LogError(
                "FAMILY_COMPANY_STARTER_TILE_MAIN_FLOW: FAIL | code=" + _playerQaExitCode +
                " | " + _playerQaFailure);
            Time.timeScale = previousTimeScale;
            Application.Quit(_playerQaExitCode == 0 ? 30 : _playerQaExitCode);
            return true;
        }

        private static OfficeTileMigrationPreviewBootstrap FindBootstrap(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var bootstrap = root.GetComponentInChildren<OfficeTileMigrationPreviewBootstrap>(true);
                if (bootstrap != null) return bootstrap;
            }
            return null;
        }

        private static Renderer[] CollectRenderers(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return System.Array.Empty<Renderer>();
            var result = new System.Collections.Generic.List<Renderer>();
            foreach (var root in scene.GetRootGameObjects())
                result.AddRange(root.GetComponentsInChildren<Renderer>(true));
            return result.ToArray();
        }

        private static int FindPreviewBuildIndex()
        {
            for (var index = 0; index < SceneManager.sceneCountInBuildSettings; index++)
            {
                var path = SceneUtility.GetScenePathByBuildIndex(index);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == PreviewSceneName) return index;
            }
            return -1;
        }
    }
}
