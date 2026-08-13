using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FamilyCompany.Presentation.Unity;
using FamilyCompany.Simulation.Contracts;
using FamilyCompany.Simulation.Family;
using FamilyCompany.Simulation.Prototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FamilyCompany.Editor
{
    [InitializeOnLoad]
    public static class CharacterOfficeRuntimeQa
    {
        private const string ArtifactFolder = "Artifacts/CharacterOfficeRuntimeQa";
        private const string ReportPath = ArtifactFolder + "/character-office-runtime-qa.txt";
        private const string ActiveKey = "FamilyCompany.CharacterOfficeRuntimeQa.Active";
        private const string StageKey = "FamilyCompany.CharacterOfficeRuntimeQa.Stage";
        private const string StartKey = "FamilyCompany.CharacterOfficeRuntimeQa.Start";
        private const string ScenarioKey = "FamilyCompany.CharacterOfficeRuntimeQa.Scenario";
        private const string NextLogKey = "FamilyCompany.CharacterOfficeRuntimeQa.NextLog";
        private const string LastTickKey = "FamilyCompany.CharacterOfficeRuntimeQa.LastTick";
        private const string FailedKey = "FamilyCompany.CharacterOfficeRuntimeQa.Failed";
        private const string OutsideSeenKey = "FamilyCompany.CharacterOfficeRuntimeQa.OutsideSeen";
        private const string ReturnSeenKey = "FamilyCompany.CharacterOfficeRuntimeQa.ReturnSeen";
        private static readonly string[] NpcIds = { "older_sister", "father", "mother" };
        private static readonly string[] CandidateIds =
        {
            "kim_seoa", "lee_jian", "choi_iseo", "jung_arin",
            "park_haeun", "han_sua", "oh_jiwoo", "yoon_chaea"
        };

        static CharacterOfficeRuntimeQa()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        [MenuItem("Family Company/QA/Rebuild And Validate Character Office Runtime")]
        public static void RebuildAndValidate()
        {
            PrepareSceneAndStaticQa();
            Debug.Log($"CHARACTER_OFFICE_RUNTIME_QA: STATIC PASS ({ReportPath})");
        }

        public static void RebuildAndValidateBatch()
        {
            try
            {
                PrepareSceneAndStaticQa();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Append($"STATIC_FAIL | {exception}");
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void StartThirtySecondPlayModeBatch()
        {
            try
            {
                PrepareSceneAndStaticQa();
                SessionState.SetBool(ActiveKey, true);
                SessionState.SetInt(StageKey, 1);
                SessionState.SetInt(ScenarioKey, 0);
                SessionState.SetBool(FailedKey, false);
                SessionState.SetBool(OutsideSeenKey, false);
                SessionState.SetBool(ReturnSeenKey, false);
                Append("PLAYMODE_REQUEST | resolution=1920x1080 | duration=30s | timeScale=4");
                EditorApplication.EnterPlaymode();
            }
            catch (Exception exception)
            {
                Append($"PLAYMODE_PREP_FAIL | {exception}");
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void ValidateSceneLinkage()
        {
            EditorSceneManager.OpenScene(PrototypeProjectBuilder.ScenePath);
            HighMotionCharacterArtBuilder.Validate();

            var player = UnityEngine.Object.FindFirstObjectByType<PrototypePlayerController>();
            Require(player != null, "Player PrototypePlayerController is missing.");
            Require(player.GetComponent<OfficeWorkerAgent>() == null,
                "Player must remain direct-controlled and must not have OfficeWorkerAgent.");
            Require(player.GetComponent<PlayerOfficeWorkInteractor>() != null,
                "Player direct office work interactor is missing.");
            ValidateFamilyAnimator("player", player.gameObject);

            var agents = UnityEngine.Object.FindObjectsByType<OfficeWorkerAgent>(FindObjectsSortMode.None)
                .OrderBy(item => item.AgentId, StringComparer.Ordinal)
                .ToArray();
            Require(agents.Length == NpcIds.Length,
                $"Initial office must contain exactly three NPC agents; found {agents.Length}.");
            Require(agents.Select(item => item.AgentId).SequenceEqual(NpcIds.OrderBy(item => item, StringComparer.Ordinal)),
                $"Initial NPC IDs are invalid: {string.Join(",", agents.Select(item => item.AgentId))}");

            foreach (var agent in agents)
            {
                ValidateFamilyAnimator(agent.AgentId, agent.gameObject);
                Require(agent.RouteCount >= 4, $"{agent.AgentId} has an incomplete navigation route.");
                Require(agent.GetComponent<MeshRenderer>() == null,
                    $"{agent.AgentId} still has a capsule/mesh placeholder renderer on its root.");
            }

            var allNames = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Select(item => item.name)
                .ToArray();
            Require(allNames.All(item => item.IndexOf("PLACEHOLDER", StringComparison.OrdinalIgnoreCase) < 0 &&
                                         item.IndexOf("임시 에셋", StringComparison.Ordinal) < 0),
                "The rebuilt scene still contains a placeholder object or label.");
            foreach (var candidateId in CandidateIds)
            {
                Require(allNames.All(item => item.IndexOf(candidateId, StringComparison.OrdinalIgnoreCase) < 0),
                    $"Unhired employee candidate appears in the initial scene: {candidateId}");
            }

            Require(UnityEngine.Object.FindFirstObjectByType<OfficeAutonomyCoordinator>() != null,
                "OfficeAutonomyCoordinator is missing from the scene.");
            Require(UnityEngine.Object.FindFirstObjectByType<OfficeContractTaskCoordinator>() != null,
                "OfficeContractTaskCoordinator is missing from the scene.");

            ValidateDirectionMath();
            ValidateAnimatorPlayback(player.GetComponent<DirectionalSpriteAnimator>());
            ValidateOfficeWaypointsAndCorridors();
            ValidateEveryNpcReachesEverySemanticDestination(agents);
            ValidateAutonomyBranches();
            OfficeVisualV2IntegrationQa.ValidateScenePreparation();
            Append("SCENE_LINKAGE_PASS | family=4 | npcAgents=3 | framesPerFamily=48 | candidates=0");
        }

        private static void PrepareSceneAndStaticQa()
        {
            Directory.CreateDirectory(ArtifactFolder);
            File.WriteAllText(ReportPath,
                $"Character Office Runtime QA | {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n", System.Text.Encoding.UTF8);
            Append("CANON | player=no-hat,direct-control | npc=older_sister,father,mother");
            PrototypeProjectBuilder.Build();
            ValidateSceneLinkage();
            AssetDatabase.SaveAssets();
            Append("STATIC_PASS");
        }

        private static void ValidateFamilyAnimator(string characterId, GameObject root)
        {
            var animator = root.GetComponent<DirectionalSpriteAnimator>();
            var renderer = root.GetComponentInChildren<SpriteRenderer>(true);
            Require(animator != null, $"{characterId} DirectionalSpriteAnimator is missing.");
            Require(renderer != null, $"{characterId} SpriteRenderer is missing.");
            Require(animator.ConfiguredFrameCount == DirectionalSpriteAnimator.RequiredFrameCount,
                $"{characterId} serialized frame count is {animator.ConfiguredFrameCount}, expected 48.");

            var unique = new HashSet<Sprite>();
            var expectedFolder = HighMotionCharacterArtBuilder.GetFrameFolder(characterId) + "/";
            for (var phase = 0; phase < DirectionalSpriteAnimator.WalkFrameCount; phase++)
            {
                for (var direction = 0; direction < DirectionalSpriteAnimator.DirectionCount; direction++)
                {
                    var sprite = animator.GetFrame(direction, phase);
                    Require(sprite != null, $"{characterId} has a null frame at direction={direction}, phase={phase}.");
                    Require(unique.Add(sprite),
                        $"{characterId} has a duplicate frame at direction={direction}, phase={phase}.");
                    var path = AssetDatabase.GetAssetPath(sprite).Replace('\\', '/');
                    Require(path.StartsWith(expectedFolder, StringComparison.Ordinal),
                        $"{characterId} frame uses a non-canonical asset: {path}");
                    Require(Mathf.Abs(sprite.pivot.x - sprite.rect.width * 0.5f) < 0.01f &&
                            Mathf.Abs(sprite.pivot.y) < 0.01f,
                        $"{characterId} frame pivot is not bottom-center: {path}");
                }
            }

            Require(unique.Count == DirectionalSpriteAnimator.RequiredFrameCount,
                $"{characterId} does not contain 48 unique high-motion frames.");
            Require(root.name.IndexOf("PLACEHOLDER", StringComparison.OrdinalIgnoreCase) < 0,
                $"{characterId} root still uses a placeholder name.");
            Append($"FAMILY_SPRITES_PASS | id={characterId} | frames=48 | unique=48 | pivot=bottom-center");
        }

        private static void ValidateDirectionMath()
        {
            var axes = new[]
            {
                new Vector2(0f, -1f), new Vector2(-1f, -1f), new Vector2(-1f, 0f), new Vector2(-1f, 1f),
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(1f, -1f)
            };
            for (var direction = 0; direction < axes.Length; direction++)
            {
                var resolved = DirectionalSpriteAnimator.ResolveDirectionFromAxes(axes[direction].x, axes[direction].y);
                Require(resolved == direction,
                    $"Camera-relative direction mismatch: expected {direction}, got {resolved}.");
            }

            Append("DIRECTION_PASS | cameraRelativeOctants=0,1,2,3,4,5,6,7");
        }

        private static void ValidateAnimatorPlayback(DirectionalSpriteAnimator animator)
        {
            var originalPosition = animator.GetComponentInChildren<SpriteRenderer>().transform.localPosition;
            animator.SetWorldVelocity(Vector3.forward * 1.8f);
            var visited = new HashSet<int>();
            for (var index = 0; index < DirectionalSpriteAnimator.WalkFrameCount; index++)
            {
                animator.Tick(animator.EffectiveFrameSeconds + 0.001f);
                visited.Add(animator.CurrentWalkFrame);
            }

            Require(visited.Count == DirectionalSpriteAnimator.WalkFrameCount,
                $"Animator did not traverse all six walk phases; got {string.Join(",", visited)}.");
            var normalSeconds = animator.EffectiveFrameSeconds;
            animator.SetWorldVelocity(Vector3.forward * 4f);
            var fastSeconds = animator.EffectiveFrameSeconds;
            animator.SetWorldVelocity(Vector3.zero);
            animator.Tick(0.5f);
            Require(animator.CurrentWalkFrame == animator.IdleWalkFrame,
                "Animator did not return to the canonical idle phase.");
            Require(fastSeconds < normalSeconds, "Animator cadence does not respond to movement speed.");
            Require(animator.GetComponentInChildren<SpriteRenderer>().transform.localPosition == originalPosition,
                "Sprite frame changes shifted the visual transform away from its foot baseline.");
            Append($"ANIMATION_PASS | phases=6 | idle={animator.IdleWalkFrame} | base={animator.BaseFrameSeconds:F3} | fast={fastSeconds:F3}");
        }

        private static void ValidateOfficeWaypointsAndCorridors()
        {
            var waypoints = UnityEngine.Object.FindObjectsByType<OfficeWaypoint>(FindObjectsSortMode.None);
            Require(waypoints.Count(item => item.Activity == OfficeActivity.Walking) >= 3,
                "Office requires at least three safe corridor waypoints.");
            Require(waypoints.Any(item => item.Activity == OfficeActivity.Outside), "Office exit waypoint is missing.");
            foreach (var required in new[]
                     {
                         OfficeActivity.Reception, OfficeActivity.Work, OfficeActivity.Printing,
                         OfficeActivity.Meeting, OfficeActivity.Break
                     })
            {
                Require(waypoints.Any(item => item.Activity == required), $"Office waypoint is missing: {required}");
            }

            Physics.SyncTransforms();
            var corridors = waypoints.Where(item => item.IsMainCorridor)
                .OrderBy(item => item.transform.position.x)
                .ThenBy(item => item.WaypointId, StringComparer.Ordinal)
                .ToArray();
            Require(corridors.Length >= 3, "Office requires at least three main corridor waypoints.");
            for (var index = 1; index < corridors.Length; index++)
            {
                AssertClearHorizontalSegment(corridors[index - 1].transform.position, corridors[index].transform.position,
                    $"{corridors[index - 1].WaypointId}->{corridors[index].WaypointId}");
            }

            foreach (var destination in waypoints.Where(item => item.Activity != OfficeActivity.Walking))
            {
                var approachPath = destination.ApproachPath;
                Require(approachPath.All(item => item != null),
                    $"Office destination {destination.WaypointId} contains a null approach waypoint.");
                var entry = approachPath.Length > 0 ? approachPath[0] : destination;
                var corridor = corridors.OrderBy(item =>
                        (item.transform.position - entry.transform.position).sqrMagnitude)
                    .First();
                AssertClearHorizontalSegment(corridor.transform.position, entry.transform.position,
                    $"{corridor.WaypointId}->{entry.WaypointId}");
                for (var index = 1; index < approachPath.Length; index++)
                {
                    AssertClearHorizontalSegment(
                        approachPath[index - 1].transform.position,
                        approachPath[index].transform.position,
                        $"{approachPath[index - 1].WaypointId}->{approachPath[index].WaypointId}");
                }

                if (entry != destination)
                {
                    var lastApproach = approachPath[approachPath.Length - 1];
                    AssertClearHorizontalSegment(lastApproach.transform.position, destination.transform.position,
                        $"{lastApproach.WaypointId}->{destination.WaypointId}");
                }
            }

            Append($"NAVIGATION_GEOMETRY_PASS | mainCorridors={corridors.Length} | " +
                   $"approachWaypoints={waypoints.Count(item => item.Activity == OfficeActivity.Walking && !item.IsMainCorridor)} | " +
                   $"destinations={waypoints.Count(item => item.Activity != OfficeActivity.Walking)}");
        }

        private static void ValidateEveryNpcReachesEverySemanticDestination(OfficeWorkerAgent[] agents)
        {
            var waypoints = UnityEngine.Object.FindObjectsByType<OfficeWaypoint>(FindObjectsSortMode.None);
            var activitySequence = new[]
            {
                OfficeActivity.Work,
                OfficeActivity.Reception,
                OfficeActivity.Printing,
                OfficeActivity.Meeting,
                OfficeActivity.Break,
                OfficeActivity.Outside,
                OfficeActivity.Work
            };
            var originalPositions = agents.ToDictionary(item => item, item => item.transform.position);
            var controllers = agents.ToDictionary(item => item, item => item.GetComponent<CharacterController>());
            try
            {
                foreach (var movingAgent in agents)
                {
                    foreach (var pair in controllers) pair.Value.enabled = pair.Key == movingAgent;
                    movingAgent.InitializeNow();
                    foreach (var activity in activitySequence)
                    {
                        var target = waypoints
                            .Where(item => item.Activity == activity)
                            .OrderBy(item => item.WaypointId, StringComparer.Ordinal)
                            .First();
                        movingAgent.SetAutonomousDestination(
                            $"central-qa:{movingAgent.AgentId}:{activity}:{target.WaypointId}",
                            target,
                            $"QA {activity}");

                        var reached = false;
                        for (var tick = 0; tick < 3000; tick++)
                        {
                            movingAgent.Tick(0.05f);
                            if (activity == OfficeActivity.Outside)
                            {
                                reached = movingAgent.IsPresentationAway;
                            }
                            else
                            {
                                reached = movingAgent.CurrentActivity == activity &&
                                          movingAgent.TargetWaypoint == target;
                            }

                            if (reached) break;
                        }

                        Require(reached,
                            $"{movingAgent.AgentId} could not reach semantic destination {activity} ({target.WaypointId}); " +
                            $"position={movingAgent.transform.position}, target={target.transform.position}, " +
                            $"currentActivity={movingAgent.CurrentActivity}.");
                    }

                    Require(!movingAgent.IsPresentationAway,
                        $"{movingAgent.AgentId} did not reappear and walk back into the office after exit QA.");
                    Append($"NPC_ALL_DESTINATIONS_PASS | id={movingAgent.AgentId} | desk,reception,printer,meeting,lounge,exit,return");
                }
            }
            finally
            {
                foreach (var pair in originalPositions) pair.Key.transform.position = pair.Value;
                foreach (var pair in controllers) pair.Value.enabled = true;
            }
        }

        private static void AssertClearHorizontalSegment(Vector3 start, Vector3 end, string label)
        {
            start.y = 0.9f;
            end.y = 0.9f;
            var direction = end - start;
            var distance = direction.magnitude;
            if (distance <= 0.001f) return;
            var hits = Physics.RaycastAll(start, direction.normalized, Mathf.Max(0f, distance - 0.22f),
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                if (hit.collider.GetComponentInParent<OfficeWorkerAgent>() != null) continue;
                if (hit.collider.GetComponentInParent<PrototypePlayerController>() != null) continue;
                throw new InvalidOperationException(
                    $"Navigation segment {label} crosses collider {hit.collider.name} at {hit.point}.");
            }
        }

        private static void ValidateAutonomyBranches()
        {
            var lowEnergy = PrototypeStateFactory.Create(20000103);
            lowEnergy.Family.Get("older_sister").ChangeEnergy(-100);
            AutonomousOfficeSimulation.EnsureIntents(lowEnergy.WorldSeed, lowEnergy.Family, 0);
            Require(lowEnergy.Family.Get("older_sister").Autonomy.TargetLocation == OfficeSemanticLocation.Lounge,
                "Low energy did not force lounge recovery.");

            var highStress = PrototypeStateFactory.Create(20000104);
            highStress.Family.Get("father").ChangeStress(100);
            AutonomousOfficeSimulation.EnsureIntents(highStress.WorldSeed, highStress.Family, 0);
            Require(highStress.Family.Get("father").Autonomy.CurrentAction == AutonomousOfficeAction.BurnoutRecovery,
                "Excess stress did not force burnout recovery.");

            foreach (var id in NpcIds) ValidateScheduledExit(id, 550, AutonomousOfficeAction.OffDuty);

            var returnState = PrototypeStateFactory.Create(20000105);
            AutonomousOfficeSimulation.EnsureIntents(returnState.WorldSeed, returnState.Family, 10);
            foreach (var id in NpcIds)
            {
                Require(returnState.Family.Get(id).Autonomy.TargetLocation != OfficeSemanticLocation.Exit,
                    $"{id} did not resume an in-office intent at 09:00.");
            }

            Append("AUTONOMY_BRANCH_PASS | recovery=lounge | schedule=exit | return=in-office");
        }

        private static void ValidateScheduledExit(string memberId, long elapsedMinute, AutonomousOfficeAction expectedAction)
        {
            var state = PrototypeStateFactory.Create(20001000 + (int)elapsedMinute);
            AutonomousOfficeSimulation.EnsureIntents(state.WorldSeed, state.Family, elapsedMinute);
            var member = state.Family.Get(memberId);
            Require(member.Autonomy.CurrentAction == expectedAction &&
                    member.Autonomy.TargetLocation == OfficeSemanticLocation.Exit,
                $"{memberId} schedule at minute {elapsedMinute} did not target the exit.");
        }

        private static void OnEditorUpdate()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            try
            {
                var stage = SessionState.GetInt(StageKey, 0);
                if (stage == 1 && EditorApplication.isPlaying)
                {
                    InitializePlayModeScenario();
                    SessionState.SetFloat(StartKey, (float)EditorApplication.timeSinceStartup);
                    SessionState.SetFloat(LastTickKey, (float)EditorApplication.timeSinceStartup);
                    SessionState.SetFloat(NextLogKey, 0f);
                    SessionState.SetInt(StageKey, 2);
                    return;
                }

                if (stage == 2 && EditorApplication.isPlaying)
                {
                    TickPlayModeScenario();
                    return;
                }

                if (stage == 3 && !EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    var failed = SessionState.GetBool(FailedKey, false);
                    Append(failed ? "PLAYMODE_FAIL" : "PLAYMODE_PASS");
                    SessionState.EraseBool(ActiveKey);
                    EditorApplication.Exit(failed ? 1 : 0);
                }
            }
            catch (Exception exception)
            {
                Append($"PLAYMODE_EXCEPTION | {exception}");
                Debug.LogException(exception);
                SessionState.SetBool(FailedKey, true);
                SessionState.SetInt(StageKey, 3);
                if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
            }
        }

        private static void InitializePlayModeScenario()
        {
            Screen.SetResolution(1920, 1080, false);
            Time.timeScale = 4f;
            var camera = Camera.main;
            Require(camera != null, "PlayMode main camera is missing.");
            var follow = camera.GetComponent<IsometricCameraFollow>();
            if (follow != null) follow.enabled = false;
            camera.transform.position = new Vector3(14f, 13.5f, -13.5f);
            camera.transform.LookAt(new Vector3(14f, 0.6f, 0f));
            camera.orthographicSize = 6.6f;
            var bootstrap = UnityEngine.Object.FindFirstObjectByType<PrototypeBootstrap>();
            Require(bootstrap != null, "PlayMode bootstrap is missing.");
            bootstrap.StartNewGameNow(1, false);
            var coordinator = bootstrap.InitializeOfficeTaskBridgeNow();
            Require(coordinator != null, "PlayMode contract coordinator is missing.");

            var offer = new SubcontractOffer(
                "character-office-runtime-qa-contract",
                "character-office-runtime-qa-client",
                "중앙 QA 고객사",
                ContractServiceType.WebsiteMaintenance,
                "사무실 이동 우선순위 검증",
                1,
                12,
                2,
                0,
                300_000,
                0);
            var acceptance = bootstrap.State.Contracts.Accept(
                offer, bootstrap.State.Company, bootstrap.State.Time.ElapsedMinutes);
            Require(acceptance.Accepted, "PlayMode QA contract could not be accepted.");
            foreach (var id in NpcIds)
            {
                Require(coordinator.AssignContractWork(offer.OfferId, id, 1),
                    $"PlayMode contract assignment failed for {id}: {coordinator.LastAssignmentFailureLabel}");
            }

            var targets = GetAgents().Select(item => item.TargetWaypoint).ToArray();
            Require(targets.All(item => item != null) && targets.Distinct().Count() == targets.Length,
                "Contract work-point collision avoidance did not reserve distinct desks.");
            Append("PLAYMODE_CONTRACT_PRIORITY_PASS | assigned=3 | distinctWorkpoints=3");
        }

        private static void TickPlayModeScenario()
        {
            var now = (float)EditorApplication.timeSinceStartup;
            var lastTick = SessionState.GetFloat(LastTickKey, now);
            var acceleratedDelta = Mathf.Clamp(now - lastTick, 0f, 0.25f) * 4f;
            SessionState.SetFloat(LastTickKey, now);
            foreach (var agent in GetAgents()) agent.Tick(acceleratedDelta);

            var elapsed = (float)(EditorApplication.timeSinceStartup - SessionState.GetFloat(StartKey, 0f));
            var nextLog = SessionState.GetFloat(NextLogKey, 0f);
            if (elapsed >= nextLog)
            {
                LogPlaySnapshot(elapsed);
                SessionState.SetFloat(NextLogKey, nextLog + 1f);
            }

            var scenario = SessionState.GetInt(ScenarioKey, 0);
            if (scenario == 0 && elapsed >= 8f)
            {
                ForceRecoveryScenario();
                SessionState.SetInt(ScenarioKey, 1);
                Capture(elapsed, "recovery");
            }
            else if (scenario == 1 && elapsed >= 14f)
            {
                ForceOutsideScenario();
                SessionState.SetInt(ScenarioKey, 2);
                Capture(elapsed, "outside_departure");
            }
            else if (scenario == 2 && elapsed >= 20f)
            {
                ObserveOutsideAndForceReturnScenario();
                SessionState.SetInt(ScenarioKey, 3);
                Capture(elapsed, "return_sister_father");
            }
            else if (scenario == 3 && elapsed >= 26f)
            {
                ObserveAllReturnedScenario();
                SessionState.SetInt(ScenarioKey, 4);
                Capture(elapsed, "all_returned");
            }

            ObserveRuntimeInvariants();
            if (elapsed < 30f) return;
            FinalizePlayModeChecks();
            SessionState.SetInt(StageKey, 3);
            Time.timeScale = 1f;
            EditorApplication.ExitPlaymode();
        }

        private static void ForceRecoveryScenario()
        {
            var bootstrap = UnityEngine.Object.FindFirstObjectByType<PrototypeBootstrap>();
            Require(GetAgents().All(item => !item.HasAssignedTask && item.CompletedAssignments >= 1),
                "Priority contract work did not complete before the recovery scenario.");
            foreach (var id in NpcIds)
            {
                var member = bootstrap.State.Family.Get(id);
                member.ChangeEnergy(-100);
                member.ChangeStress(100);
            }

            new SimulationRunner(bootstrap.State).AdvanceMinutes(30);
            UnityEngine.Object.FindFirstObjectByType<OfficeAutonomyCoordinator>().RefreshNow();
            Require(GetAgents().All(item => item.TargetWaypoint != null &&
                                            item.TargetWaypoint.Activity == OfficeActivity.Break),
                "Forced low-energy/high-stress recovery did not route every NPC to the lounge.");
            Append("PLAYMODE_RECOVERY_ROUTE_PASS | target=lounge");
        }

        private static void ForceOutsideScenario()
        {
            var bootstrap = UnityEngine.Object.FindFirstObjectByType<PrototypeBootstrap>();
            foreach (var id in NpcIds)
            {
                var member = bootstrap.State.Family.Get(id);
                member.ChangeEnergy(100);
                member.ChangeStress(-100);
            }

            new SimulationRunner(bootstrap.State).AdvanceMinutes(870);
            UnityEngine.Object.FindFirstObjectByType<OfficeAutonomyCoordinator>().RefreshNow();
            var agents = GetAgents().ToDictionary(item => item.AgentId, StringComparer.Ordinal);
            Require(agents.Values.All(item => item.TargetWaypoint.Activity == OfficeActivity.Outside),
                "Simultaneous family sleep departure did not target the office exit.");
            Append("PLAYMODE_DEPARTURE_ROUTE_PASS | minute=900 | sister=exit | father=exit | mother=exit");
        }

        private static void ObserveOutsideAndForceReturnScenario()
        {
            var agents = GetAgents().ToDictionary(item => item.AgentId, StringComparer.Ordinal);
            Require(agents.Values.All(item => item.IsPresentationAway),
                "All three NPCs did not reach the shared exit and hide before their return time.");
            SessionState.SetBool(OutsideSeenKey, true);

            var bootstrap = UnityEngine.Object.FindFirstObjectByType<PrototypeBootstrap>();
            new SimulationRunner(bootstrap.State).AdvanceMinutes(540);
            UnityEngine.Object.FindFirstObjectByType<OfficeAutonomyCoordinator>().RefreshNow();
            Require(agents.Values.All(item => !item.IsPresentationAway &&
                                               item.TargetWaypoint.Activity != OfficeActivity.Outside),
                "All three NPCs did not reappear at the exit for their return walk.");
            SessionState.SetBool(ReturnSeenKey, true);
            Append("PLAYMODE_RETURN_PASS | minute=1440 | sister=visible | father=visible | mother=visible");
        }

        private static void ObserveAllReturnedScenario()
        {
            var agents = GetAgents().ToDictionary(item => item.AgentId, StringComparer.Ordinal);
            Require(agents.Values.All(item => !item.IsPresentationAway &&
                                               item.CurrentActivity != OfficeActivity.Outside &&
                                               item.transform.position.x > 8f),
                "All three NPCs did not visibly walk back inside after reappearing at the exit.");
            Append("PLAYMODE_ALL_RETURNED_PASS | positions=inside-office");
        }

        private static void ObserveRuntimeInvariants()
        {
            var visibleAgents = GetAgents().Where(item => !item.IsPresentationAway).ToArray();
            for (var left = 0; left < visibleAgents.Length; left++)
            {
                for (var right = left + 1; right < visibleAgents.Length; right++)
                {
                    var distance = Vector3.Distance(visibleAgents[left].transform.position,
                        visibleAgents[right].transform.position);
                    Require(distance >= 0.42f,
                        $"Visible NPC collision overlap: {visibleAgents[left].AgentId}/{visibleAgents[right].AgentId}={distance:F3}");
                }
            }

            foreach (var label in UnityEngine.Object.FindObjectsByType<OfficeStatusLabel>(FindObjectsSortMode.None))
            {
                if (label.Agent == null || label.Agent.IsPresentationAway) continue;
                var text = label.GetComponent<TextMesh>()?.text ?? string.Empty;
                Require(!text.EndsWith("\n이동 중", StringComparison.Ordinal),
                    $"Status label lost semantic state while moving: {label.Agent.AgentId}");
            }
        }

        private static void FinalizePlayModeChecks()
        {
            var agents = GetAgents();
            Require(agents.All(item => item.CompletedAssignments >= 1),
                "Not every NPC completed its priority contract assignment.");
            Require(agents.All(item => item.HasAutonomousDestination),
                "An NPC failed to resume an autonomous destination after contract completion.");
            Require(SessionState.GetBool(OutsideSeenKey, false),
                "The departure-hidden branch was not observed in PlayMode.");
            Require(SessionState.GetBool(ReturnSeenKey, false),
                "The return-visible branch was not observed in PlayMode.");
            Capture(30f, "final");
            OfficeVisualV2IntegrationQa.CaptureResolutionPair("30s-final");
            Append("PLAYMODE_FINAL_PASS | duration=30s | contractResume=3 | departureReturn=observed");
        }

        private static OfficeWorkerAgent[] GetAgents()
        {
            return UnityEngine.Object.FindObjectsByType<OfficeWorkerAgent>(FindObjectsSortMode.None)
                .OrderBy(item => item.AgentId, StringComparer.Ordinal)
                .ToArray();
        }

        private static void LogPlaySnapshot(float elapsed)
        {
            var parts = new List<string>();
            foreach (var agent in GetAgents())
            {
                var animator = agent.GetComponent<DirectionalSpriteAnimator>();
                var target = agent.TargetWaypoint;
                parts.Add(
                    $"{agent.AgentId}@({agent.transform.position.x:F2},{agent.transform.position.z:F2})/" +
                    $"{agent.CurrentActivity}/away={agent.IsPresentationAway}/dir={animator.CurrentDirection}/frame={animator.CurrentWalkFrame}" +
                    $"/seat={agent.SeatingPhase}/claim={agent.HasActiveSeatClaim}" +
                    $"/clip={(animator.CurrentOfficeSeatingClip.HasValue ? animator.CurrentOfficeSeatingClip.Value.ToString() : "none")}" +
                    $"/hook={animator.IsOfficeWorkHookActive}/safeStand={animator.IsOfficeWorkSafeToStand}" +
                    $"/target={(target == null ? "none" : target.Activity.ToString())}");
            }

            Append($"PLAY_SNAPSHOT | t={elapsed:F1} | {string.Join(" | ", parts)}");
        }

        private static void Capture(float elapsed, string label)
        {
            var absolute = Path.GetFullPath($"{ArtifactFolder}/office-{elapsed:00}-{label}.png");
            var camera = Camera.main;
            Require(camera != null, "Cannot capture the office without a main camera.");
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var renderTexture = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            var texture = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = renderTexture;
                RenderTexture.active = renderTexture;
                camera.Render();
                texture.ReadPixels(new Rect(0f, 0f, 1920f, 1080f), 0, 0, false);
                texture.Apply(false, false);
                File.WriteAllBytes(absolute, texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }

            Append($"CAPTURE_PASS | {absolute} | bytes={new FileInfo(absolute).Length}");
        }

        private static void Append(string line)
        {
            Directory.CreateDirectory(ArtifactFolder);
            File.AppendAllText(ReportPath, line + Environment.NewLine, System.Text.Encoding.UTF8);
            Debug.Log($"CHARACTER_OFFICE_RUNTIME_QA | {line}");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
