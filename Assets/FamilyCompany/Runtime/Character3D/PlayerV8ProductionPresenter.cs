using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Presentation.Unity.OfficeGridView;
using FamilyCompany.Presentation.Unity.OfficeRuntime;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEngine;

namespace FamilyCompany.Runtime.Character3D
{
    /// <summary>
    /// Authoritative visible presentation for the production protagonist and V31 workstation sets.
    /// Simulation, pathfinding, tile occupancy, purchase placement, save IDs and seat claims remain
    /// owned by StarterOfficeRuntimeBootstrap. This adapter projects those authoritative objects to
    /// the approved Player V8/V31 3D layer and permanently suppresses their retired sprite pixels.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    [DisallowMultipleComponent]
    public sealed class PlayerV8ProductionPresenter : MonoBehaviour
    {
        public const string Contract = "FC-PLAYER-V8-PRODUCTION-PRESENTATION-V1";
        public const int ProductionLayer = 30;
        public const float ApprovedModelScale = 1.024378657f;
        public const float ApprovedTargetHeight = 1.857258558f;
        public const float ApprovedStrideOfficeUnits = 0.7950477f;
        public const float ApprovedCycleSeconds = 1.4f;
        public const float ApprovedFacingOffsetDegrees = 0f;
        public const float TurnSeconds = 0.18f;

        private const string ModelResourcePath =
            "Production3D/PlayerV8/player-v8-production";
        private const string AlbedoResourcePath =
            "Production3D/PlayerV8/player-v8-albedo";
        private const string MaterialResourcePath =
            "Production3D/PlayerV8/PlayerV8ProductionSurface";
        private const string WalkClipName = "PlayerV6_Casual_Walk_inplace";
        private const float MovementEpsilonSqr = 0.000001f;

        private static PlayerV8ProductionPresenter instance;

        private readonly List<Family3DWorkstation> workstations =
            new List<Family3DWorkstation>();
        private readonly Dictionary<string, Family3DWorkstation> workstationBySeatId =
            new Dictionary<string, Family3DWorkstation>(StringComparer.Ordinal);
        private readonly HashSet<Renderer> maskedFurnitureRenderers = new HashSet<Renderer>();
        private readonly HashSet<Light> maskedSceneLights = new HashSet<Light>();

        private StarterOfficeRuntimeBootstrap starter;
        private OfficeRuntimeAgent player;
        private GameObject presentationRoot;
        private GameObject playerHost;
        private GameObject playerModel;
        private Family3DWalkActor walkActor;
        private Camera overlayCamera;
        private Light presentationLight;
        private Material playerRuntimeMaterial;
        private Camera sourceOfficeCamera;
        private bool bindFailureLogged;
        private float seatedBlend01;
        private double workClockSeconds;
        private Vector3 lastGroundPosition;
        private bool hasGroundPosition;
        private Quaternion travelYaw = Quaternion.identity;
        private bool hasTravelYaw;
        private Quaternion blendedYaw = Quaternion.identity;
        private bool hasBlendedYaw;
        private Quaternion activeTurnTarget = Quaternion.identity;
        private float activeTurnRate;
        private bool hasActiveTurn;

        public static PlayerV8ProductionPresenter Instance => instance;
        public bool IsBound => player != null && playerHost != null && walkActor != null;
        public string BoundPlayerId => player == null ? string.Empty : player.AgentId;
        public int WorkstationCount => workstations.Count;
        public int VisibleLegacyPlayerRendererCount => CountVisibleLegacyPlayerRenderers();
        public int VisibleLegacyWorkstationRendererCount =>
            maskedFurnitureRenderers.Count(renderer => IsRendererVisible(renderer));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (instance != null)
                return;
            var host = new GameObject("~PlayerV8ProductionPresenter");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<PlayerV8ProductionPresenter>();
        }

        private IEnumerator Start()
        {
            Application.runInBackground = true;
            while (enabled)
            {
                TryBindWhenReady();
                yield return null;
            }
        }

        private void LateUpdate()
        {
            if (IsIsolatedFamily3DQaActive())
            {
                if (IsBound)
                    ReleaseBinding();
                return;
            }

            if (!BindingStillMatchesStarter())
            {
                if (IsBound)
                    ReleaseBinding();
                TryBindWhenReady();
            }
            if (!IsBound)
                return;

            sourceOfficeCamera = Camera.main;
            MaintainLayerIsolation();
            HideRetiredPresentation();
            UpdatePlayerPresentation();
        }

        private void TryBindWhenReady()
        {
            if (IsBound || IsIsolatedFamily3DQaActive())
                return;
            StarterOfficeRuntimeBootstrap candidate =
                FindFirstObjectByType<StarterOfficeRuntimeBootstrap>();
            if (candidate == null || !candidate.IsReady || candidate.World == null)
                return;
            OfficeRuntimeAgent candidatePlayer = candidate.Actors.FirstOrDefault(actor =>
                actor != null && string.Equals(actor.AgentId, "player", StringComparison.Ordinal));
            if (candidatePlayer == null)
                return;

            try
            {
                Bind(candidate, candidatePlayer);
                bindFailureLogged = false;
            }
            catch (Exception exception)
            {
                ReleaseBinding();
                if (!bindFailureLogged)
                {
                    bindFailureLogged = true;
                    Debug.LogException(exception, this);
                    Debug.LogError(
                        "PLAYER_V8_PRODUCTION: FAIL_CLOSED | " + exception.Message,
                        this);
                }
            }
        }

        private void Bind(
            StarterOfficeRuntimeBootstrap runtime,
            OfficeRuntimeAgent runtimePlayer)
        {
            starter = runtime ?? throw new ArgumentNullException(nameof(runtime));
            player = runtimePlayer ?? throw new ArgumentNullException(nameof(runtimePlayer));
            sourceOfficeCamera = Camera.main;
            if (sourceOfficeCamera == null)
                throw new InvalidOperationException("Production office camera is unavailable.");

            EnsureOverlayPresentation();
            MaintainLayerIsolation();

            GameObject modelPrefab = Resources.Load<GameObject>(ModelResourcePath);
            Texture2D albedo = Resources.Load<Texture2D>(AlbedoResourcePath);
            Material surface = Resources.Load<Material>(MaterialResourcePath);
            AnimationClip walkClip = Resources.LoadAll<AnimationClip>(ModelResourcePath)
                .FirstOrDefault(clip => clip != null &&
                    string.Equals(clip.name, WalkClipName, StringComparison.Ordinal));
            if (modelPrefab == null)
                throw new InvalidOperationException("Player V8 production FBX is missing.");
            if (albedo == null)
                throw new InvalidOperationException("Player V8 production albedo is missing.");
            if (surface == null || surface.shader == null)
                throw new InvalidOperationException("Player V8 production surface is missing.");
            if (walkClip == null || !walkClip.isHumanMotion)
                throw new InvalidOperationException("Player V8 authored Humanoid walk clip is missing.");

            presentationRoot = new GameObject("PlayerV8AndV31ProductionPresentation");
            presentationRoot.transform.SetParent(transform, false);
            playerHost = new GameObject("PlayerV8ProductionHost");
            playerHost.SetActive(false);
            playerHost.transform.SetParent(presentationRoot.transform, false);
            Vector3 ground = MapOfficeActorToProductionGround(player);
            playerHost.transform.SetPositionAndRotation(
                ground,
                MapOfficeDirectionToUnityYaw(player.CurrentDirection));
            SetLayerRecursively(playerHost, ProductionLayer);

            playerModel = Instantiate(modelPrefab, playerHost.transform, false);
            playerModel.name = "PlayerV8ProductionModel";
            SetLayerRecursively(playerModel, ProductionLayer);

            SkinnedMeshRenderer[] skinned =
                playerModel.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (skinned.Length != 1)
                throw new InvalidOperationException(
                    "Player V8 must contain exactly one complete skinned mesh; found " +
                    skinned.Length + ".");
            Animator animator = playerModel.GetComponent<Animator>() ??
                                playerModel.GetComponentInChildren<Animator>(true);
            if (animator == null || animator.avatar == null ||
                !animator.avatar.isValid || !animator.avatar.isHuman)
                throw new InvalidOperationException(
                    "Player V8 production FBX has no valid Humanoid Avatar.");

            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            skinned[0].updateWhenOffscreen = true;
            playerRuntimeMaterial = new Material(surface)
            {
                name = "PlayerV8ProductionSurface_Runtime",
                mainTexture = albedo,
                color = Color.white
            };
            skinned[0].sharedMaterial = playerRuntimeMaterial;

            playerModel.transform.localScale *= ApprovedModelScale;
            Bounds scaledBounds = EncapsulateBounds(skinned);
            playerModel.transform.position += Vector3.up * (0f - scaledBounds.min.y);

            walkActor = playerHost.AddComponent<Family3DWalkActor>();
            walkActor.Configure(
                "player",
                playerModel.transform,
                animator,
                walkClip,
                ground,
                Color.white,
                1f,
                false,
                null,
                false,
                false,
                false,
                ApprovedCycleSeconds,
                true);
            playerHost.SetActive(true);
            walkActor.Initialize();
            walkActor.RebaseVisualRootAfterScale();
            float heightError = Mathf.Abs(walkActor.StandingHeight - ApprovedTargetHeight);
            if (heightError > 0.02f)
                throw new InvalidOperationException(
                    "Player V8 production height drifted from its approved map scale: actual=" +
                    walkActor.StandingHeight.ToString("F6") + " approved=" +
                    ApprovedTargetHeight.ToString("F6") + ".");

            CreateV31Workstations();
            player.SetExternalDirectionalSeatingPresentation(true);
            HideRetiredPresentation();
            ResetMotionState();
            Debug.Log(
                "PLAYER_V8_PRODUCTION: BOUND | contract=" + Contract +
                " player=player scale=" + ApprovedModelScale.ToString("F9") +
                " height=" + walkActor.StandingHeight.ToString("F6") +
                " stride=" + ApprovedStrideOfficeUnits.ToString("F7") +
                " workstations=" + workstations.Count +
                " legacyPlayerVisible=0 legacyWorkstationVisible=0",
                this);
        }

        private void EnsureOverlayPresentation()
        {
            if (overlayCamera == null)
            {
                var cameraHost = new GameObject("PlayerV8ProductionOverlayCamera");
                cameraHost.transform.SetParent(transform, false);
                cameraHost.transform.position = new Vector3(0f, 12f, -12f);
                cameraHost.transform.LookAt(Vector3.zero);
                overlayCamera = cameraHost.AddComponent<Camera>();
                overlayCamera.orthographic = true;
                overlayCamera.orthographicSize = 6.5f;
                overlayCamera.nearClipPlane = 0.05f;
                overlayCamera.farClipPlane = 100f;
                overlayCamera.clearFlags = CameraClearFlags.Depth;
                overlayCamera.cullingMask = 1 << ProductionLayer;
                overlayCamera.depth = 100f;
                overlayCamera.allowHDR = false;
                overlayCamera.allowMSAA = true;
            }
            overlayCamera.enabled = true;

            if (presentationLight == null)
            {
                var lightHost = new GameObject("PlayerV8ProductionDirectionalLight");
                lightHost.transform.SetParent(transform, false);
                lightHost.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
                presentationLight = lightHost.AddComponent<Light>();
                presentationLight.type = LightType.Directional;
                presentationLight.intensity = 0.38f;
                presentationLight.color = new Color(1f, 0.94f, 0.86f);
                presentationLight.shadows = LightShadows.Soft;
                presentationLight.cullingMask = 1 << ProductionLayer;
            }
        }

        private void MaintainLayerIsolation()
        {
            if (sourceOfficeCamera != null && sourceOfficeCamera != overlayCamera)
                sourceOfficeCamera.cullingMask &= ~(1 << ProductionLayer);
            if (overlayCamera != null)
            {
                overlayCamera.cullingMask = 1 << ProductionLayer;
                overlayCamera.enabled = true;
            }
            foreach (Light light in FindObjectsByType<Light>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (light == null || light == presentationLight || maskedSceneLights.Contains(light))
                    continue;
                light.cullingMask &= ~(1 << ProductionLayer);
                maskedSceneLights.Add(light);
            }
        }

        private void CreateV31Workstations()
        {
            OfficeRuntimeWorld world = starter.World;
            for (var index = 0; index < world.Grid.SeatSlots.Count; index++)
            {
                OfficeSeatSlot seat = world.Grid.SeatSlots[index];
                if (!seat.HasWorkstationBinding)
                    continue;
                if (!world.FurniturePresenter.TryGetFurniture(
                        seat.WorkSurfaceFurnitureId,
                        out PlacedOfficeFurniture desk) || desk == null)
                    throw new InvalidOperationException(
                        "Bound V31 workstation is missing: " + seat.WorkSurfaceFurnitureId);

                Vector3 basisSource = world.Presenter.CellCenterWorld(seat.Cell);
                Vector3 basisGround = MapOfficeWorldToProductionGround(basisSource);
                Vector3 gridRight = MapOfficeWorldToProductionGround(
                    basisSource + world.Presenter.CellBasisXWorld()) - basisGround;
                Vector3 gridForward = MapOfficeWorldToProductionGround(
                    basisSource + world.Presenter.CellBasisYWorld()) - basisGround;
                gridRight.y = gridForward.y = 0f;

                Vector3[] sourceCorners = world.Presenter.FootprintCornersWorld(desk);
                if (sourceCorners == null || sourceCorners.Length != 4)
                    throw new InvalidOperationException(
                        "V31 workstation requires four semantic footprint corners.");
                var groundCorners = new Vector3[4];
                Vector3 footprintCenter = Vector3.zero;
                for (var corner = 0; corner < groundCorners.Length; corner++)
                {
                    groundCorners[corner] = MapOfficeWorldToProductionGround(sourceCorners[corner]);
                    footprintCenter += groundCorners[corner];
                }
                footprintCenter *= 0.25f;
                float footprintWidth = Vector3.Distance(groundCorners[0], groundCorners[1]);
                float footprintDepth = Vector3.Distance(groundCorners[0], groundCorners[3]);

                Vector3 workstationRight = gridRight;
                Vector3 workstationForward = gridForward;
                int turns = ((int)desk.Facing - (int)OfficeFurnitureFacing.SouthEast + 4) & 3;
                for (var turn = 0; turn < turns; turn++)
                {
                    Vector3 previousRight = workstationRight;
                    workstationRight = -workstationForward;
                    workstationForward = previousRight;
                    float previousWidth = footprintWidth;
                    footprintWidth = footprintDepth;
                    footprintDepth = previousWidth;
                }

                Vector3 seatGround = MapOfficeWorldToProductionGround(
                    world.Workstations.DeskSeatSocketWorld(seat));
                Vector3 keyboardGround = MapOfficeWorldToProductionGround(
                    world.Workstations.DeskWorkSocketWorld(seat));
                Family3DWorkstation workstation = Family3DWorkstation.Create(
                    presentationRoot.transform,
                    ProductionLayer,
                    seat.SeatId,
                    seatGround,
                    workstationRight,
                    workstationForward,
                    footprintCenter,
                    footprintWidth,
                    footprintDepth,
                    keyboardGround,
                    walkActor.StandingHeight,
                    ApprovedFacingOffsetDegrees,
                    0f);
                workstations.Add(workstation);
                workstationBySeatId.Add(seat.SeatId, workstation);
                HideFurniture(seat.WorkSurfaceFurnitureId);
                HideFurniture(seat.ChairFurnitureId);
            }
        }

        private void UpdatePlayerPresentation()
        {
            if (player.Phase == OfficeRuntimeAgentPhase.Outside)
            {
                playerHost.SetActive(false);
                return;
            }
            if (!playerHost.activeSelf)
                playerHost.SetActive(true);

            Vector3 actorGround = MapOfficeActorToProductionGround(player);
            Family3DWorkstation workstation = ResolveActiveWorkstation();
            OfficeRuntimeAgentPhase phase = player.Phase;
            bool seatFacingPhase =
                phase == OfficeRuntimeAgentPhase.AligningSeat ||
                phase == OfficeRuntimeAgentPhase.RotatingToSeat ||
                phase == OfficeRuntimeAgentPhase.SittingDown ||
                phase == OfficeRuntimeAgentPhase.Working ||
                phase == OfficeRuntimeAgentPhase.FinishingWork ||
                phase == OfficeRuntimeAgentPhase.StandingUp;
            Quaternion rotation = seatFacingPhase && workstation != null
                ? workstation.SeatedRotationWorld
                : ResolveBlendedYaw(actorGround);
            bool wantsSeatedPose =
                phase == OfficeRuntimeAgentPhase.SittingDown ||
                phase == OfficeRuntimeAgentPhase.Working ||
                phase == OfficeRuntimeAgentPhase.FinishingWork;
            seatedBlend01 = Mathf.MoveTowards(
                seatedBlend01,
                wantsSeatedPose ? 1f : 0f,
                Mathf.Max(Time.unscaledDeltaTime, 0f) / 0.42f);
            if (phase == OfficeRuntimeAgentPhase.Working)
                workClockSeconds += Math.Max(Time.unscaledDeltaTime, 0f);

            if (workstation != null && (seatedBlend01 > 0.0001f || seatFacingPhase))
            {
                float positionBlend = Mathf.SmoothStep(0f, 1f, seatedBlend01);
                Vector3 rootPosition = Vector3.Lerp(
                    actorGround,
                    workstation.SeatGroundWorld,
                    positionBlend);
                rootPosition.y = 0f;
                walkActor.TickSeatedDeskWork(
                    workClockSeconds,
                    rootPosition,
                    rotation,
                    seatedBlend01,
                    phase == OfficeRuntimeAgentPhase.Working);
                Family3DWalkActor.PoseSnapshot seatedPose = walkActor.ReadPoseSnapshot();
                float seatedRootY =
                    workstation.CushionWorldY +
                    0.113f * seatedPose.standingHeight -
                    seatedPose.hipsLocal.y;
                rootPosition.y = Mathf.Lerp(0f, seatedRootY, positionBlend);
                rootPosition += workstation.SeatedBodyForwardWorld *
                                (0.07f * seatedPose.standingHeight * positionBlend);
                playerHost.transform.position = rootPosition;
                walkActor.AlignSeatedDeskLimbs(
                    workstation.KeyboardWorld,
                    workstation.SeatedBodyForwardWorld,
                    0f,
                    seatedBlend01,
                    workClockSeconds,
                    phase == OfficeRuntimeAgentPhase.Working);
                return;
            }

            bool moving = player.LastActualDisplacement.sqrMagnitude > MovementEpsilonSqr;
            double clipCycles = player.GaitDistance / ApprovedStrideOfficeUnits;
            double motionClock =
                (clipCycles - walkActor.PhaseOffset) * walkActor.CycleSeconds;
            walkActor.Tick(motionClock, actorGround, rotation, moving);
        }

        private Family3DWorkstation ResolveActiveWorkstation()
        {
            if (!string.IsNullOrEmpty(player.ActiveSeatId) &&
                workstationBySeatId.TryGetValue(player.ActiveSeatId, out Family3DWorkstation active))
                return active;
            workstationBySeatId.TryGetValue("seat_player", out Family3DWorkstation canonical);
            return canonical;
        }

        private Quaternion ResolveBlendedYaw(Vector3 groundPosition)
        {
            Quaternion target = ResolveTravelYaw(groundPosition);
            if (!hasBlendedYaw)
            {
                blendedYaw = target;
                hasBlendedYaw = true;
                return target;
            }
            const float turnRestartDegrees = 5f;
            float remaining = Quaternion.Angle(blendedYaw, target);
            if (!hasActiveTurn || Quaternion.Angle(activeTurnTarget, target) > turnRestartDegrees)
            {
                activeTurnTarget = target;
                activeTurnRate = remaining / TurnSeconds;
                hasActiveTurn = true;
            }
            float rate = Mathf.Max(360f, activeTurnRate);
            blendedYaw = Quaternion.RotateTowards(blendedYaw, target, rate * Time.deltaTime);
            if (remaining <= 0.01f)
                hasActiveTurn = false;
            return blendedYaw;
        }

        private Quaternion ResolveTravelYaw(Vector3 groundPosition)
        {
            if (hasGroundPosition)
            {
                Vector3 delta = groundPosition - lastGroundPosition;
                delta.y = 0f;
                if (delta.sqrMagnitude > 0.00000001f)
                {
                    travelYaw = Quaternion.LookRotation(delta.normalized, Vector3.up) *
                                Quaternion.Euler(0f, ApprovedFacingOffsetDegrees, 0f);
                    hasTravelYaw = true;
                }
            }
            lastGroundPosition = groundPosition;
            hasGroundPosition = true;
            return hasTravelYaw
                ? travelYaw
                : MapOfficeDirectionToUnityYaw(player.CurrentDirection);
        }

        private void HideRetiredPresentation()
        {
            if (player?.PresentationRenderer != null)
                player.PresentationRenderer.forceRenderingOff = true;
            if (player?.SeatedUpperBodyProtectionRenderer != null)
                player.SeatedUpperBodyProtectionRenderer.forceRenderingOff = true;
            foreach (OfficeSeatSlot seat in starter.World.Grid.SeatSlots)
            {
                if (!seat.HasWorkstationBinding)
                    continue;
                HideFurniture(seat.WorkSurfaceFurnitureId);
                HideFurniture(seat.ChairFurnitureId);
            }
        }

        private void HideFurniture(string furnitureId)
        {
            OfficeGridFurniturePresenter presenter = starter.World.FurniturePresenter;
            if (presenter.TryGetRenderer(furnitureId, out SpriteRenderer baseRenderer))
                HideRenderer(baseRenderer);
            if (presenter.FrontOverlayRenderers.TryGetValue(
                    furnitureId,
                    out SpriteRenderer frontRenderer))
                HideRenderer(frontRenderer);
            if (presenter.OccupiedChairLowerBodyRenderers.TryGetValue(
                    furnitureId,
                    out SpriteRenderer lowerRenderer))
                HideRenderer(lowerRenderer);
        }

        private void HideRenderer(Renderer renderer)
        {
            if (renderer == null)
                return;
            renderer.forceRenderingOff = true;
            maskedFurnitureRenderers.Add(renderer);
        }

        private Vector3 MapOfficeActorToProductionGround(OfficeRuntimeAgent actor)
        {
            Vector2 position = actor.Position;
            return MapOfficeWorldToProductionGround(
                new Vector3(position.x, position.y, actor.transform.position.z));
        }

        private Vector3 MapOfficeWorldToProductionGround(Vector3 sourceWorld)
        {
            if (sourceOfficeCamera != null && overlayCamera != null)
            {
                Vector3 viewport = sourceOfficeCamera.WorldToViewportPoint(sourceWorld);
                if (viewport.z > 0f)
                {
                    Ray ray = overlayCamera.ViewportPointToRay(
                        new Vector3(viewport.x, viewport.y, 0f));
                    var groundPlane = new Plane(Vector3.up, Vector3.zero);
                    if (groundPlane.Raycast(ray, out float distance) && distance >= 0f)
                        return ray.GetPoint(distance);
                }
            }
            return new Vector3(sourceWorld.x, 0f, sourceWorld.y);
        }

        public static Quaternion MapOfficeDirectionToUnityYaw(int direction)
        {
            int octant = (direction % 8 + 8) % 8;
            return Quaternion.Euler(0f, (octant - 4) * 45f, 0f);
        }

        private bool BindingStillMatchesStarter()
        {
            if (!IsBound || starter == null || !starter.IsReady || starter.World == null)
                return false;
            return starter.Actors.Any(actor => ReferenceEquals(actor, player));
        }

        private static bool IsIsolatedFamily3DQaActive()
        {
            return FindFirstObjectByType<
                FamilyCompany.Experimental.Family3D.Family3DStarterOfficeCandidateQa>() != null;
        }

        private int CountVisibleLegacyPlayerRenderers()
        {
            var visible = 0;
            if (player?.PresentationRenderer != null &&
                IsRendererVisible(player.PresentationRenderer))
                visible++;
            if (player?.SeatedUpperBodyProtectionRenderer != null &&
                IsRendererVisible(player.SeatedUpperBodyProtectionRenderer))
                visible++;
            return visible;
        }

        private static bool IsRendererVisible(Renderer renderer)
        {
            return renderer != null && renderer.enabled && !renderer.forceRenderingOff &&
                   renderer.gameObject.activeInHierarchy;
        }

        private static Bounds EncapsulateBounds(Renderer[] renderers)
        {
            if (renderers == null || renderers.Length == 0)
                throw new InvalidOperationException("Player V8 has no renderer bounds.");
            Bounds bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
                bounds.Encapsulate(renderers[index].bounds);
            return bounds;
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform)
                SetLayerRecursively(child.gameObject, layer);
        }

        private void ResetMotionState()
        {
            seatedBlend01 = 0f;
            workClockSeconds = 0d;
            hasGroundPosition = false;
            hasTravelYaw = false;
            hasBlendedYaw = false;
            hasActiveTurn = false;
            travelYaw = blendedYaw = activeTurnTarget = Quaternion.identity;
            activeTurnRate = 0f;
        }

        private void ReleaseBinding()
        {
            if (player != null)
                player.SetExternalDirectionalSeatingPresentation(false);
            if (presentationRoot != null)
            {
                presentationRoot.SetActive(false);
                Destroy(presentationRoot);
            }
            if (playerRuntimeMaterial != null)
                Destroy(playerRuntimeMaterial);
            presentationRoot = null;
            playerHost = null;
            playerModel = null;
            walkActor = null;
            playerRuntimeMaterial = null;
            player = null;
            starter = null;
            workstations.Clear();
            workstationBySeatId.Clear();
            maskedFurnitureRenderers.Clear();
            ResetMotionState();
        }

        private void OnDestroy()
        {
            ReleaseBinding();
            if (ReferenceEquals(instance, this))
                instance = null;
        }
    }
}
