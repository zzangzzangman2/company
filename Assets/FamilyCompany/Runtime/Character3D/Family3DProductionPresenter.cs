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
    /// Authoritative visible presentation for the production Player V8, Father V19 and V31
    /// workstation sets.
    /// Simulation, pathfinding, tile occupancy, purchase placement, save IDs and seat claims remain
    /// owned by StarterOfficeRuntimeBootstrap. This adapter projects those authoritative objects to
    /// the approved Player V8/V31 3D layer and permanently suppresses their retired sprite pixels.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    [DisallowMultipleComponent]
    public sealed class Family3DProductionPresenter : MonoBehaviour
    {
        public const string Contract = "FC-PLAYER-FATHER-3D-PRODUCTION-PRESENTATION-V1";
        public const int ProductionLayer = 30;
        public const float PlayerApprovedModelScale = 1.024378657f;
        public const float PlayerApprovedTargetHeight = 1.857258558f;
        public const float FatherApprovedModelScale = 1.012728333f;
        public const float FatherApprovedTargetHeight = 1.885507822f;
        public const float ApprovedStrideOfficeUnits = 0.7950477f;
        public const float ApprovedCycleSeconds = 1.4f;
        public const float ApprovedFacingOffsetDegrees = 0f;
        public const float TurnSeconds = 0.18f;

        private const string PlayerModelResourcePath =
            "Production3D/PlayerV8/player-v8-production";
        private const string PlayerAlbedoResourcePath =
            "Production3D/PlayerV8/player-v8-albedo";
        private const string PlayerMaterialResourcePath =
            "Production3D/PlayerV8/PlayerV8ProductionSurface";
        private const string PlayerWalkClipName = "PlayerV6_Casual_Walk_inplace";
        private const string FatherModelResourcePath =
            "Production3D/FatherV19/father-v19-production";
        private const string FatherAlbedoResourcePath =
            "Production3D/FatherV19/father-v19-albedo";
        private const string FatherMaterialResourcePath =
            "Production3D/FatherV19/FatherV19ProductionSurface";
        private const string FatherWalkClipName = "FatherV19_Casual_Walk_inplace";
        private const float MovementEpsilonSqr = 0.000001f;

        private static Family3DProductionPresenter instance;

        private readonly List<Family3DWorkstation> workstations =
            new List<Family3DWorkstation>();
        private readonly Dictionary<string, Family3DWorkstation> workstationBySeatId =
            new Dictionary<string, Family3DWorkstation>(StringComparer.Ordinal);
        private readonly HashSet<Renderer> maskedFurnitureRenderers = new HashSet<Renderer>();
        private readonly HashSet<Light> maskedSceneLights = new HashSet<Light>();
        private readonly List<CharacterBinding> characters = new List<CharacterBinding>();
        private readonly Dictionary<string, CharacterBinding> characterById =
            new Dictionary<string, CharacterBinding>(StringComparer.Ordinal);

        private StarterOfficeRuntimeBootstrap starter;
        private GameObject presentationRoot;
        private Camera overlayCamera;
        private Light presentationLight;
        private Camera sourceOfficeCamera;
        private bool bindFailureLogged;

        public static Family3DProductionPresenter Instance => instance;
        public bool IsBound => characters.Count == 2 && characters.All(binding => binding.IsBound);
        public int BoundCharacterCount => characters.Count(binding => binding.IsBound);
        public int WorkstationCount => workstations.Count;
        public int VisibleLegacyCharacterRendererCount => CountVisibleLegacyCharacterRenderers();
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
            var host = new GameObject("~Family3DProductionPresenter");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<Family3DProductionPresenter>();
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
            for (var index = 0; index < characters.Count; index++)
                UpdateCharacterPresentation(characters[index]);
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
            OfficeRuntimeAgent candidateFather = candidate.Actors.FirstOrDefault(actor =>
                actor != null && string.Equals(actor.AgentId, "father", StringComparison.Ordinal));
            if (candidatePlayer == null || candidateFather == null)
                return;

            try
            {
                Bind(candidate, candidatePlayer, candidateFather);
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
                        "FAMILY_3D_PRODUCTION: FAIL_CLOSED | " + exception.Message,
                        this);
                }
            }
        }

        private void Bind(
            StarterOfficeRuntimeBootstrap runtime,
            OfficeRuntimeAgent runtimePlayer,
            OfficeRuntimeAgent runtimeFather)
        {
            starter = runtime ?? throw new ArgumentNullException(nameof(runtime));
            if (runtimePlayer == null) throw new ArgumentNullException(nameof(runtimePlayer));
            if (runtimeFather == null) throw new ArgumentNullException(nameof(runtimeFather));
            sourceOfficeCamera = Camera.main;
            if (sourceOfficeCamera == null)
                throw new InvalidOperationException("Production office camera is unavailable.");

            EnsureOverlayPresentation();
            MaintainLayerIsolation();

            presentationRoot = new GameObject("PlayerV8FatherV19AndV31ProductionPresentation");
            presentationRoot.transform.SetParent(transform, false);
            CharacterBinding player = CreateCharacterBinding(
                runtimePlayer,
                "PlayerV8",
                PlayerModelResourcePath,
                PlayerAlbedoResourcePath,
                PlayerMaterialResourcePath,
                PlayerWalkClipName,
                PlayerApprovedModelScale,
                PlayerApprovedTargetHeight);
            CharacterBinding father = CreateCharacterBinding(
                runtimeFather,
                "FatherV19",
                FatherModelResourcePath,
                FatherAlbedoResourcePath,
                FatherMaterialResourcePath,
                FatherWalkClipName,
                FatherApprovedModelScale,
                FatherApprovedTargetHeight);
            characters.Add(player);
            characters.Add(father);
            characterById.Add(player.AgentId, player);
            characterById.Add(father.AgentId, father);

            CreateV31Workstations(player.WalkActor.StandingHeight);
            for (var index = 0; index < characters.Count; index++)
                characters[index].Agent.SetExternalDirectionalSeatingPresentation(true);
            HideRetiredPresentation();
            Debug.Log(
                "FAMILY_3D_PRODUCTION: BOUND | contract=" + Contract +
                " actors=player,father playerScale=" + player.AppliedScale.ToString("F9") +
                " playerHeight=" + player.WalkActor.StandingHeight.ToString("F6") +
                " fatherScale=" + father.AppliedScale.ToString("F9") +
                " fatherHeight=" + father.WalkActor.StandingHeight.ToString("F6") +
                " stride=" + ApprovedStrideOfficeUnits.ToString("F7") +
                " workstations=" + workstations.Count +
                " legacyCharacterVisible=0 legacyWorkstationVisible=0",
                this);
        }

        private CharacterBinding CreateCharacterBinding(
            OfficeRuntimeAgent agent,
            string productionName,
            string modelResourcePath,
            string albedoResourcePath,
            string materialResourcePath,
            string walkClipName,
            float lockedModelScale,
            float approvedTargetHeight)
        {
            GameObject modelPrefab = Resources.Load<GameObject>(modelResourcePath);
            Texture2D albedo = Resources.Load<Texture2D>(albedoResourcePath);
            Material surface = Resources.Load<Material>(materialResourcePath);
            AnimationClip walkClip = Resources.LoadAll<AnimationClip>(modelResourcePath)
                .FirstOrDefault(clip => clip != null &&
                    string.Equals(clip.name, walkClipName, StringComparison.Ordinal));
            if (modelPrefab == null || albedo == null || surface == null || surface.shader == null ||
                walkClip == null || !walkClip.isHumanMotion)
                throw new InvalidOperationException(
                    productionName + " production mesh/albedo/material/Humanoid clip is incomplete.");

            var host = new GameObject(productionName + "ProductionHost");
            host.SetActive(false);
            host.transform.SetParent(presentationRoot.transform, false);
            Vector3 ground = MapOfficeActorToProductionGround(agent);
            host.transform.SetPositionAndRotation(
                ground,
                MapOfficeDirectionToUnityYaw(agent.CurrentDirection));
            SetLayerRecursively(host, ProductionLayer);

            GameObject model = Instantiate(modelPrefab, host.transform, false);
            model.name = productionName + "ProductionModel";
            SetLayerRecursively(model, ProductionLayer);
            SkinnedMeshRenderer[] skinned = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (skinned.Length != 1)
                throw new InvalidOperationException(
                    productionName + " must contain exactly one complete skinned mesh; found " +
                    skinned.Length + ".");
            Animator animator = model.GetComponent<Animator>() ??
                                model.GetComponentInChildren<Animator>(true);
            if (animator == null || animator.avatar == null ||
                !animator.avatar.isValid || !animator.avatar.isHuman)
                throw new InvalidOperationException(
                    productionName + " has no valid Humanoid Avatar.");

            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            skinned[0].updateWhenOffscreen = true;
            var runtimeMaterial = new Material(surface)
            {
                name = productionName + "ProductionSurface_Runtime",
                mainTexture = albedo,
                color = Color.white
            };
            skinned[0].sharedMaterial = runtimeMaterial;

            if (lockedModelScale <= 0f || approvedTargetHeight <= 0f)
                throw new InvalidOperationException(
                    productionName + " requires a receipt-locked model scale and map height.");
            float appliedScale = lockedModelScale;
            model.transform.localScale *= appliedScale;
            Bounds scaledBounds = EncapsulateBounds(skinned);
            model.transform.position += Vector3.up * (0f - scaledBounds.min.y);

            var walkActor = host.AddComponent<Family3DWalkActor>();
            walkActor.Configure(
                agent.AgentId,
                model.transform,
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
            host.SetActive(true);
            walkActor.Initialize();
            walkActor.RebaseVisualRootAfterScale();
            if (Mathf.Abs(walkActor.StandingHeight - approvedTargetHeight) > 0.02f)
                throw new InvalidOperationException(
                    productionName + " map height drifted: actual=" +
                    walkActor.StandingHeight.ToString("F6") + " approved=" +
                    approvedTargetHeight.ToString("F6") + ".");
            return new CharacterBinding(
                agent,
                host,
                model,
                walkActor,
                runtimeMaterial,
                appliedScale,
                approvedTargetHeight);
        }

        private void EnsureOverlayPresentation()
        {
            if (overlayCamera == null)
            {
                var cameraHost = new GameObject("Family3DProductionOverlayCamera");
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
                var lightHost = new GameObject("Family3DProductionDirectionalLight");
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

        private void CreateV31Workstations(float referenceCharacterHeight)
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
                    referenceCharacterHeight,
                    ApprovedFacingOffsetDegrees,
                    0f);
                workstations.Add(workstation);
                workstationBySeatId.Add(seat.SeatId, workstation);
                HideFurniture(seat.WorkSurfaceFurnitureId);
                HideFurniture(seat.ChairFurnitureId);
            }
        }

        private void UpdateCharacterPresentation(CharacterBinding binding)
        {
            OfficeRuntimeAgent actor = binding.Agent;
            if (actor.Phase == OfficeRuntimeAgentPhase.Outside)
            {
                binding.Host.SetActive(false);
                return;
            }
            if (!binding.Host.activeSelf)
                binding.Host.SetActive(true);

            Vector3 actorGround = MapOfficeActorToProductionGround(actor);
            Family3DWorkstation workstation = ResolveActiveWorkstation(binding);
            OfficeRuntimeAgentPhase phase = actor.Phase;
            bool seatFacingPhase =
                phase == OfficeRuntimeAgentPhase.AligningSeat ||
                phase == OfficeRuntimeAgentPhase.RotatingToSeat ||
                phase == OfficeRuntimeAgentPhase.SittingDown ||
                phase == OfficeRuntimeAgentPhase.Working ||
                phase == OfficeRuntimeAgentPhase.FinishingWork ||
                phase == OfficeRuntimeAgentPhase.StandingUp;
            Quaternion rotation = seatFacingPhase && workstation != null
                ? workstation.SeatedRotationWorld
                : ResolveBlendedYaw(binding, actorGround);
            bool wantsSeatedPose =
                phase == OfficeRuntimeAgentPhase.SittingDown ||
                phase == OfficeRuntimeAgentPhase.Working ||
                phase == OfficeRuntimeAgentPhase.FinishingWork;
            binding.SeatedBlend01 = Mathf.MoveTowards(
                binding.SeatedBlend01,
                wantsSeatedPose ? 1f : 0f,
                Mathf.Max(Time.unscaledDeltaTime, 0f) / 0.42f);
            if (phase == OfficeRuntimeAgentPhase.Working)
                binding.WorkClockSeconds += Math.Max(Time.unscaledDeltaTime, 0f);

            if (workstation != null && (binding.SeatedBlend01 > 0.0001f || seatFacingPhase))
            {
                float positionBlend = Mathf.SmoothStep(0f, 1f, binding.SeatedBlend01);
                Vector3 rootPosition = Vector3.Lerp(
                    actorGround,
                    workstation.SeatGroundWorld,
                    positionBlend);
                rootPosition.y = 0f;
                binding.WalkActor.TickSeatedDeskWork(
                    binding.WorkClockSeconds,
                    rootPosition,
                    rotation,
                    binding.SeatedBlend01,
                    phase == OfficeRuntimeAgentPhase.Working);
                Family3DWalkActor.PoseSnapshot seatedPose = binding.WalkActor.ReadPoseSnapshot();
                float seatedRootY =
                    workstation.CushionWorldY +
                    0.113f * seatedPose.standingHeight -
                    seatedPose.hipsLocal.y;
                rootPosition.y = Mathf.Lerp(0f, seatedRootY, positionBlend);
                rootPosition += workstation.SeatedBodyForwardWorld *
                                (0.07f * seatedPose.standingHeight * positionBlend);
                binding.Host.transform.position = rootPosition;
                binding.WalkActor.AlignSeatedDeskLimbs(
                    workstation.KeyboardWorld,
                    workstation.SeatedBodyForwardWorld,
                    0f,
                    binding.SeatedBlend01,
                    binding.WorkClockSeconds,
                    phase == OfficeRuntimeAgentPhase.Working);
                return;
            }

            bool moving = actor.LastActualDisplacement.sqrMagnitude > MovementEpsilonSqr;
            double clipCycles = actor.GaitDistance / ApprovedStrideOfficeUnits;
            double motionClock =
                (clipCycles - binding.WalkActor.PhaseOffset) * binding.WalkActor.CycleSeconds;
            binding.WalkActor.Tick(motionClock, actorGround, rotation, moving);
        }

        private Family3DWorkstation ResolveActiveWorkstation(CharacterBinding binding)
        {
            if (!string.IsNullOrEmpty(binding.Agent.ActiveSeatId) &&
                workstationBySeatId.TryGetValue(
                    binding.Agent.ActiveSeatId,
                    out Family3DWorkstation active))
                return active;
            workstationBySeatId.TryGetValue(
                "seat_" + binding.AgentId,
                out Family3DWorkstation canonical);
            return canonical;
        }

        private Quaternion ResolveBlendedYaw(CharacterBinding binding, Vector3 groundPosition)
        {
            Quaternion target = ResolveTravelYaw(binding, groundPosition);
            if (!binding.HasBlendedYaw)
            {
                binding.BlendedYaw = target;
                binding.HasBlendedYaw = true;
                return target;
            }
            const float turnRestartDegrees = 5f;
            float remaining = Quaternion.Angle(binding.BlendedYaw, target);
            if (!binding.HasActiveTurn ||
                Quaternion.Angle(binding.ActiveTurnTarget, target) > turnRestartDegrees)
            {
                binding.ActiveTurnTarget = target;
                binding.ActiveTurnRate = remaining / TurnSeconds;
                binding.HasActiveTurn = true;
            }
            float rate = Mathf.Max(360f, binding.ActiveTurnRate);
            binding.BlendedYaw = Quaternion.RotateTowards(
                binding.BlendedYaw,
                target,
                rate * Time.deltaTime);
            if (remaining <= 0.01f)
                binding.HasActiveTurn = false;
            return binding.BlendedYaw;
        }

        private Quaternion ResolveTravelYaw(CharacterBinding binding, Vector3 groundPosition)
        {
            if (binding.HasGroundPosition)
            {
                Vector3 delta = groundPosition - binding.LastGroundPosition;
                delta.y = 0f;
                if (delta.sqrMagnitude > 0.00000001f)
                {
                    binding.TravelYaw = Quaternion.LookRotation(delta.normalized, Vector3.up) *
                                        Quaternion.Euler(0f, ApprovedFacingOffsetDegrees, 0f);
                    binding.HasTravelYaw = true;
                }
            }
            binding.LastGroundPosition = groundPosition;
            binding.HasGroundPosition = true;
            return binding.HasTravelYaw
                ? binding.TravelYaw
                : MapOfficeDirectionToUnityYaw(binding.Agent.CurrentDirection);
        }

        private void HideRetiredPresentation()
        {
            for (var index = 0; index < characters.Count; index++)
            {
                OfficeRuntimeAgent actor = characters[index].Agent;
                if (actor?.PresentationRenderer != null)
                    actor.PresentationRenderer.forceRenderingOff = true;
                if (actor?.SeatedUpperBodyProtectionRenderer != null)
                    actor.SeatedUpperBodyProtectionRenderer.forceRenderingOff = true;
            }
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
            for (var index = 0; index < characters.Count; index++)
                if (!starter.Actors.Any(actor =>
                        ReferenceEquals(actor, characters[index].Agent)))
                    return false;
            return true;
        }

        private static bool IsIsolatedFamily3DQaActive()
        {
            return FindFirstObjectByType<
                FamilyCompany.Experimental.Family3D.Family3DStarterOfficeCandidateQa>() != null;
        }

        private int CountVisibleLegacyCharacterRenderers()
        {
            var visible = 0;
            for (var index = 0; index < characters.Count; index++)
            {
                OfficeRuntimeAgent actor = characters[index].Agent;
                if (actor?.PresentationRenderer != null &&
                    IsRendererVisible(actor.PresentationRenderer))
                    visible++;
                if (actor?.SeatedUpperBodyProtectionRenderer != null &&
                    IsRendererVisible(actor.SeatedUpperBodyProtectionRenderer))
                    visible++;
            }
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
                throw new InvalidOperationException("Production character has no renderer bounds.");
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

        private void ReleaseBinding()
        {
            for (var index = 0; index < characters.Count; index++)
            {
                CharacterBinding binding = characters[index];
                if (binding.Agent != null)
                    binding.Agent.SetExternalDirectionalSeatingPresentation(false);
                if (binding.RuntimeMaterial != null)
                    Destroy(binding.RuntimeMaterial);
            }
            if (presentationRoot != null)
            {
                presentationRoot.SetActive(false);
                Destroy(presentationRoot);
            }
            presentationRoot = null;
            starter = null;
            characters.Clear();
            characterById.Clear();
            workstations.Clear();
            workstationBySeatId.Clear();
            maskedFurnitureRenderers.Clear();
        }

        private sealed class CharacterBinding
        {
            public CharacterBinding(
                OfficeRuntimeAgent agent,
                GameObject host,
                GameObject model,
                Family3DWalkActor walkActor,
                Material runtimeMaterial,
                float appliedScale,
                float approvedHeight)
            {
                Agent = agent;
                Host = host;
                Model = model;
                WalkActor = walkActor;
                RuntimeMaterial = runtimeMaterial;
                AppliedScale = appliedScale;
                ApprovedHeight = approvedHeight;
            }

            public string AgentId => Agent == null ? string.Empty : Agent.AgentId;
            public bool IsBound => Agent != null && Host != null && Model != null && WalkActor != null;
            public OfficeRuntimeAgent Agent { get; }
            public GameObject Host { get; }
            public GameObject Model { get; }
            public Family3DWalkActor WalkActor { get; }
            public Material RuntimeMaterial { get; }
            public float AppliedScale { get; }
            public float ApprovedHeight { get; }
            public float SeatedBlend01 { get; set; }
            public double WorkClockSeconds { get; set; }
            public Vector3 LastGroundPosition { get; set; }
            public bool HasGroundPosition { get; set; }
            public Quaternion TravelYaw { get; set; } = Quaternion.identity;
            public bool HasTravelYaw { get; set; }
            public Quaternion BlendedYaw { get; set; } = Quaternion.identity;
            public bool HasBlendedYaw { get; set; }
            public Quaternion ActiveTurnTarget { get; set; } = Quaternion.identity;
            public float ActiveTurnRate { get; set; }
            public bool HasActiveTurn { get; set; }
        }

        private void OnDestroy()
        {
            ReleaseBinding();
            if (ReferenceEquals(instance, this))
                instance = null;
        }
    }
}
