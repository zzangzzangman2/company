using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Presentation.Unity.OfficeGridView;
using FamilyCompany.Presentation.Unity.OfficeRuntime;
using FamilyCompany.Simulation.OfficeLayout;
using FamilyCompany.Simulation.Navigation;
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
        public const string Legacy2DScaleCandidateFlag =
            "-familyCompanyLegacy2DScaleCandidate";
        public const string Legacy2DScaleCandidatePhaseOffsetArgument =
            "-familyCompanyLegacy2DScaleCandidatePhaseOffsetCycles";
        public const string Legacy2DScaleCandidateStrideArgument =
            "-familyCompanyLegacy2DScaleCandidateStrideOfficeUnits";
        public const int ProductionLayer = 30;
        public const float PlayerApprovedModelScale = 1.024378657f;
        public const float PlayerApprovedTargetHeight = 1.857258558f;
        public const float FatherStandardizedModelScale = 0.950318127f;
        public const float FatherStandardizedTargetHeight = 1.769311871f;
        public const float FatherStandardizedHorizontalScale = 0.92f;
        // Candidate-only values derived from all 48 retained legacy HighMotion walk sprites at
        // 180 PPU, runtime visual scale 1.55 and the shipping 16:9 camera. They preserve the old
        // Player/Father screen-height relationship while keeping their rendered head widths equal.
        // They are never selected without the explicit candidate flag.
        public const float PlayerLegacy2DMatchedModelScale = 1.263885643f;
        public const float PlayerLegacy2DMatchedTargetHeight = 2.291498763f;
        public const float FatherLegacy2DMatchedModelScale = 1.306909878f;
        public const float FatherLegacy2DMatchedTargetHeight = 2.454888000f;
        public const float FatherLegacy2DMatchedHorizontalScale = 0.806840529f;
        public const float FatherLegacy2DMatchedNeutralFill = 0.82f;
        // Candidate-only absolute brightness. The fixed neutral shader multiplies albedo by
        // saturate(ambient + key * form), so the rendered body can never exceed the albedo and the
        // approved albedos are dark (isolated same-tile render luma 93.9 / 73.7, HSV value
        // 0.41 / 0.33 on 2026-09-02). The user asked for brighter, mutually consistent 3D actors;
        // the tint gain scales RGB (hue and saturation preserved until clipping) and the Father's
        // larger gain narrows the Father/Player luma ratio while keeping his darker outfit darker.
        public const float PlayerLegacy2DMatchedBrightnessGain = 1.26f;
        public const float FatherLegacy2DMatchedBrightnessGain = 1.28f;
        public static readonly Vector2 PlayerLegacy2DMatchedFootCenterOffsetLocal =
            new Vector2(0.050989f, 0.214083f);
        public static readonly Vector2 FatherLegacy2DMatchedFootCenterOffsetLocal =
            // Restores the measured foot-midpoint-at-root value. The 2026-09-02 candidates
            // (0.0375, 0.5) and (-0.24, 0.5) were tuned against a 2D shoe-pixel centroid that mixes
            // rendered shoe height with floor position; they pushed the Father's planted feet
            // 0.38 cells forward / 0.28 cells left onto the tile corner (57/61 planted frames on a
            // line versus 8/61 here). Judge tile centring with bone-based planted-foot clearance.
            new Vector2(0.037517f, 0.138023f);
        public const float ApprovedStrideOfficeUnits = 0.7950477f;
        // Candidate-only cadence alignment: action 613 contains two alternating steps, so exactly
        // two isometric tile-centre distances per cycle places each successive landing one tile
        // apart. Root travel remains continuously owned by OfficeRuntimeAgent; no contact-frame
        // translation correction is applied. Production/default retains its approved stride.
        public const float Legacy2DMatchedTileSynchronizedStrideOfficeUnits = 1.98761598f;
        public const float Legacy2DMatchedTileSafePhaseOffsetCycles = 0.40f;
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
        private bool legacy2DScaleCandidate;
        private float legacy2DScaleCandidatePhaseOffsetCycles;
        private float effectiveStrideOfficeUnits = ApprovedStrideOfficeUnits;

        public static Family3DProductionPresenter Instance => instance;
        public bool IsBound => characters.Count == OfficeFamily3DVisualRoster.FamilyCount &&
                               characters.All(binding => binding.IsBound);
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
                // A layout rebuild can destroy semantic agents before this LateUpdate. Such
                // partially invalid bindings still own 3D hosts and must be released as well.
                if (characters.Count > 0)
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
            if (candidatePlayer == null || candidateFather == null ||
                candidate.Actors.Count(actor => actor != null &&
                    OfficeFamily3DVisualRoster.ModelMemberId(actor.AgentId).Length > 0) !=
                OfficeFamily3DVisualRoster.FamilyCount)
                return;

            try
            {
                if (characters.Count > 0)
                    ReleaseBinding();
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
            legacy2DScaleCandidate = IsLegacy2DScaleCandidateActive();
            legacy2DScaleCandidatePhaseOffsetCycles = legacy2DScaleCandidate
                ? ResolveLegacy2DScaleCandidatePhaseOffsetCycles()
                : 0f;
            effectiveStrideOfficeUnits = legacy2DScaleCandidate
                ? ResolveLegacy2DScaleCandidateStrideOfficeUnits()
                : ApprovedStrideOfficeUnits;
            CharacterBinding player = CreateCharacterBinding(
                runtimePlayer,
                "PlayerV8",
                PlayerModelResourcePath,
                PlayerAlbedoResourcePath,
                PlayerMaterialResourcePath,
                PlayerWalkClipName,
                legacy2DScaleCandidate
                    ? PlayerLegacy2DMatchedModelScale
                    : PlayerApprovedModelScale,
                legacy2DScaleCandidate
                    ? PlayerLegacy2DMatchedTargetHeight
                    : PlayerApprovedTargetHeight,
                1f,
                -1f,
                legacy2DScaleCandidate ? PlayerLegacy2DMatchedBrightnessGain : 1f,
                legacy2DScaleCandidate
                    ? PlayerLegacy2DMatchedFootCenterOffsetLocal
                    : Vector2.zero);
            CharacterBinding father = CreateCharacterBinding(
                runtimeFather,
                "FatherV19",
                FatherModelResourcePath,
                FatherAlbedoResourcePath,
                legacy2DScaleCandidate
                    ? PlayerMaterialResourcePath
                    : FatherMaterialResourcePath,
                FatherWalkClipName,
                legacy2DScaleCandidate
                    ? FatherLegacy2DMatchedModelScale
                    : FatherStandardizedModelScale,
                legacy2DScaleCandidate
                    ? FatherLegacy2DMatchedTargetHeight
                    : FatherStandardizedTargetHeight,
                legacy2DScaleCandidate
                    ? FatherLegacy2DMatchedHorizontalScale
                    : FatherStandardizedHorizontalScale,
                legacy2DScaleCandidate
                    ? FatherLegacy2DMatchedNeutralFill
                    : -1f,
                legacy2DScaleCandidate ? FatherLegacy2DMatchedBrightnessGain : 1f,
                legacy2DScaleCandidate
                    ? FatherLegacy2DMatchedFootCenterOffsetLocal
                    : Vector2.zero);
            characters.Add(player);
            characters.Add(father);
            characterById.Add(player.AgentId, player);
            characterById.Add(father.AgentId, father);
            AlignCandidateStandingGround(player, father);
            if (!legacy2DScaleCandidate)
            {
                float referenceGround = CalibrateDefaultStanding(player);
                float fatherGround = CalibrateDefaultStanding(father);
                father.StandingGroundLiftCorrection = referenceGround - fatherGround;
            }
            foreach (OfficeRuntimeAgent actor in starter.Actors.Where(actor =>
                         OfficeFamily3DVisualRoster.IsTemporaryStandIn(actor.AgentId)))
            {
                bool usePlayer = OfficeFamily3DVisualRoster.ModelMemberId(actor.AgentId) == "player";
                CharacterBinding source = usePlayer ? player : father;
                CharacterBinding standIn = CreateCharacterBinding(
                    actor,
                    OfficeFamily3DVisualRoster.ProductionName(actor.AgentId),
                    usePlayer ? PlayerModelResourcePath : FatherModelResourcePath,
                    usePlayer ? PlayerAlbedoResourcePath : FatherAlbedoResourcePath,
                    usePlayer || legacy2DScaleCandidate ? PlayerMaterialResourcePath : FatherMaterialResourcePath,
                    usePlayer ? PlayerWalkClipName : FatherWalkClipName,
                    source.AppliedScale,
                    source.ApprovedHeight,
                    usePlayer ? 1f : legacy2DScaleCandidate
                        ? FatherLegacy2DMatchedHorizontalScale : FatherStandardizedHorizontalScale,
                    !usePlayer && legacy2DScaleCandidate ? FatherLegacy2DMatchedNeutralFill : -1f,
                    legacy2DScaleCandidate
                        ? usePlayer ? PlayerLegacy2DMatchedBrightnessGain : FatherLegacy2DMatchedBrightnessGain
                        : 1f,
                    source.StandingFootCenterOffsetLocal);
                characters.Add(standIn);
                characterById.Add(standIn.AgentId, standIn);
                AlignCandidateStandingGround(player, standIn);
                if (!legacy2DScaleCandidate)
                    standIn.StandingGroundLiftCorrection = source.StandingGroundLiftCorrection;
            }

            // Furniture dimensions are an approved V31 tile contract and must not grow when a
            // character-scale candidate is evaluated.
            CreateV31Workstations(PlayerApprovedTargetHeight);
            for (var index = 0; index < characters.Count; index++)
                characters[index].Agent.SetExternalDirectionalSeatingPresentation(true);
            HideRetiredPresentation();
            Debug.Log(
                "FAMILY_3D_PRODUCTION: BOUND | contract=" + Contract +
                " actors=player,older_sister,father,mother temporaryStandIns=older_sister:PlayerV8,mother:FatherV19 playerScale=" + player.AppliedScale.ToString("F9") +
                " playerHeight=" + player.WalkActor.StandingHeight.ToString("F6") +
                " fatherScale=" + father.AppliedScale.ToString("F9") +
                " fatherHorizontalScale=" +
                (legacy2DScaleCandidate
                    ? FatherLegacy2DMatchedHorizontalScale
                    : FatherStandardizedHorizontalScale).ToString("F3") +
                " fatherHeight=" + father.WalkActor.StandingHeight.ToString("F6") +
                " scaleProfile=" +
                (legacy2DScaleCandidate ? "Legacy2DMatchedCandidate" : "ApprovedProduction") +
                " productionEligible=False runtimeReview=TileCentreCollisionRetestRequired" +
                " approvedPackageBaseline=" + (!legacy2DScaleCandidate) +
                " stride=" + effectiveStrideOfficeUnits.ToString("F7") +
                " candidatePhaseOffsetCycles=" +
                legacy2DScaleCandidatePhaseOffsetCycles.ToString("F6") +
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
            float approvedTargetHeight,
            float horizontalScale,
            float neutralFillOverride,
            float brightnessGain,
            Vector2 footCenterOffsetLocal)
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
            host.transform.position += host.transform.rotation * new Vector3(
                footCenterOffsetLocal.x,
                0f,
                footCenterOffsetLocal.y);
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
                color = new Color(brightnessGain, brightnessGain, brightnessGain, 1f)
            };
            if (neutralFillOverride >= 0f && runtimeMaterial.HasProperty("_AmbientFactor"))
                runtimeMaterial.SetFloat("_AmbientFactor", neutralFillOverride);
            skinned[0].sharedMaterial = runtimeMaterial;

            if (lockedModelScale <= 0f || approvedTargetHeight <= 0f || horizontalScale <= 0f)
                throw new InvalidOperationException(
                    productionName + " requires locked vertical, horizontal and map-height scales.");
            float appliedScale = lockedModelScale;
            model.transform.localScale = Vector3.Scale(
                model.transform.localScale,
                new Vector3(
                    appliedScale * horizontalScale,
                    appliedScale,
                    appliedScale * horizontalScale));
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
                approvedTargetHeight,
                footCenterOffsetLocal);
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

                // An elevated sprite socket is not a floor coordinate. Projecting it onto
                // the ground displaced the chair differently for every facing.
                Vector3 seatGround = basisGround;
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
                    0f,
                    centerChairOnSeatCell: true);
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
            var tuning = OfficeDevelopmentTuningSession.Current;
            float stride = tuning?.Stride ?? effectiveStrideOfficeUnits;
            if (binding.LastStride > 0f && Math.Abs(stride - binding.LastStride) > 0.000001f)
                binding.StrideContinuityBias += actor.GaitDistance / binding.LastStride - actor.GaitDistance / stride;
            binding.LastStride = stride;
            double clipCycles = actor.GaitDistance / stride + binding.StrideContinuityBias +
                                (tuning?.Phase ?? legacy2DScaleCandidatePhaseOffsetCycles);
            double motionClock =
                (clipCycles - binding.WalkActor.PhaseOffset) * binding.WalkActor.CycleSeconds;
            bool playerBody = OfficeFamily3DVisualRoster.ModelMemberId(binding.AgentId) == "player";
            Vector3 visualGround = actorGround + rotation * new Vector3(
                binding.StandingFootCenterOffsetLocal.x + (tuning == null ? 0f : playerBody ? tuning.PlayerFootX : tuning.FatherFootX),
                0f,
                binding.StandingFootCenterOffsetLocal.y + (tuning == null ? 0f : playerBody ? tuning.PlayerFootZ : tuning.FatherFootZ)) +
                Vector3.up * binding.StandingGroundLiftCorrection;
            binding.WalkActor.Tick(motionClock, visualGround, rotation, moving);
        }

        private static float CalibrateDefaultStanding(CharacterBinding binding)
        {
            // Measure this complete package at its unchanged production scale. Correct only the
            // constant standing ground anchor; no contact-frame root shift, rig edit or seated IK.
            Vector3 ground = binding.Host.transform.position;
            Quaternion yaw = binding.Host.transform.rotation;
            var skin = binding.Model.GetComponentInChildren<SkinnedMeshRenderer>(true);
            var mesh = new Mesh();
            var points = new List<Vector3>(skin.sharedMesh.vertexCount);
            Vector3 midpointSum = Vector3.zero;
            float minimum = float.PositiveInfinity;
            const int phases = 48;
            try
            {
                for (int i = 0; i < phases; i++)
                {
                    binding.WalkActor.Tick((2d + (double)i / phases) * ApprovedCycleSeconds, ground, yaw, true);
                    var pose = binding.WalkActor.ReadPoseSnapshot();
                    midpointSum += Quaternion.Inverse(yaw) * ((pose.leftFootWorld + pose.rightFootWorld) * 0.5f - ground);
                    skin.BakeMesh(mesh, true);
                    mesh.GetVertices(points);
                    foreach (Vector3 vertex in points)
                        minimum = Mathf.Min(minimum, (skin.transform.position + skin.transform.rotation * vertex).y - ground.y);
                }
                Vector3 mean = midpointSum / phases;
                binding.StandingFootCenterOffsetLocal = new Vector2(-mean.x, -mean.z);
                if (binding.StandingFootCenterOffsetLocal.magnitude > 0.35f || float.IsInfinity(minimum))
                    throw new InvalidOperationException("Standing calibration exceeded safe package bounds.");
                Debug.Log("FAMILY_3D_STANDING_CALIBRATION: actor=" + binding.AgentId +
                    " offset=" + binding.StandingFootCenterOffsetLocal.ToString("F6") + " cycleMinimum=" + minimum.ToString("F6"));
                return minimum;
            }
            finally
            {
                Destroy(mesh);
                binding.WalkActor.Tick(0d, ground, yaw, false);
            }
        }

        // Candidate-only vertical grounding. The bounds lift grounds the bind pose, but both
        // production walk clips carry the hips higher than that pose, so the lowest skinned vertex
        // over a full cycle floats above the 3D ground: Player about 0.14 and Father about 0.44
        // office units (measured 2026-09-02). Player is the approved visual reference whose soles
        // read as standing on the 2D floor, so Father is lowered by the difference. This is a
        // constant standing/walking correction; seated presentation and production/default are
        // untouched and no per-contact-frame root adjustment is introduced.
        private const int CandidateGroundSamplePhases = 24;

        private void AlignCandidateStandingGround(CharacterBinding reference, CharacterBinding target)
        {
            if (!legacy2DScaleCandidate)
                return;
            float referenceMinimum = MeasureWalkCycleLowestVertex(reference);
            float targetMinimum = MeasureWalkCycleLowestVertex(target);
            target.StandingGroundLiftCorrection = referenceMinimum - targetMinimum;
            Debug.Log(
                "FAMILY_3D_CANDIDATE_STANDING_GROUND: referenceCycleLowestVertex=" +
                referenceMinimum.ToString("F4") +
                " targetCycleLowestVertex=" + targetMinimum.ToString("F4") +
                " targetCorrection=" + target.StandingGroundLiftCorrection.ToString("F4"),
                this);
        }

        private static float MeasureWalkCycleLowestVertex(CharacterBinding binding)
        {
            SkinnedMeshRenderer skinned = binding.Model.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (skinned == null || skinned.sharedMesh == null)
                return 0f;
            Vector3 ground = binding.Host.transform.position;
            Quaternion rotation = binding.Host.transform.rotation;
            double cycleSeconds = binding.WalkActor.CycleSeconds;
            var baked = new Mesh();
            var vertices = new List<Vector3>(skinned.sharedMesh.vertexCount);
            float lowest = float.PositiveInfinity;
            for (var phase = 0; phase < CandidateGroundSamplePhases; phase++)
            {
                double clock = cycleSeconds * phase / CandidateGroundSamplePhases;
                binding.WalkActor.Tick(clock, ground, rotation, true);
                skinned.BakeMesh(baked, true);
                baked.GetVertices(vertices);
                Transform rendererTransform = skinned.transform;
                Vector3 rendererPosition = rendererTransform.position;
                Quaternion rendererRotation = rendererTransform.rotation;
                for (var index = 0; index < vertices.Count; index++)
                {
                    float y = (rendererPosition + rendererRotation * vertices[index]).y;
                    if (y < lowest)
                        lowest = y;
                }
            }
            UnityEngine.Object.Destroy(baked);
            binding.WalkActor.RebaseVisualRootAfterScale();
            return float.IsInfinity(lowest) ? 0f : lowest - ground.y;
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

        private static bool IsLegacy2DScaleCandidateActive()
        {
            return Environment.GetCommandLineArgs().Any(argument =>
                string.Equals(
                    argument,
                    Legacy2DScaleCandidateFlag,
                    StringComparison.OrdinalIgnoreCase));
        }

        private static float ResolveLegacy2DScaleCandidatePhaseOffsetCycles()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index + 1 < arguments.Length; index++)
            {
                if (!string.Equals(
                        arguments[index],
                        Legacy2DScaleCandidatePhaseOffsetArgument,
                        StringComparison.OrdinalIgnoreCase))
                    continue;
                if (float.TryParse(
                        arguments[index + 1],
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out float parsed))
                    return Mathf.Repeat(parsed, 1f);
            }
            return Legacy2DMatchedTileSafePhaseOffsetCycles;
        }

        private static float ResolveLegacy2DScaleCandidateStrideOfficeUnits()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index + 1 < arguments.Length; index++)
            {
                if (!string.Equals(
                        arguments[index],
                        Legacy2DScaleCandidateStrideArgument,
                        StringComparison.OrdinalIgnoreCase))
                    continue;
                if (float.TryParse(
                        arguments[index + 1],
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out float parsed) && parsed >= 0.5f && parsed <= 2f)
                    return parsed;
            }
            return Legacy2DMatchedTileSynchronizedStrideOfficeUnits;
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
            legacy2DScaleCandidate = false;
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
                float approvedHeight,
                Vector2 standingFootCenterOffsetLocal)
            {
                Agent = agent;
                Host = host;
                Model = model;
                WalkActor = walkActor;
                RuntimeMaterial = runtimeMaterial;
                AppliedScale = appliedScale;
                ApprovedHeight = approvedHeight;
                StandingFootCenterOffsetLocal = standingFootCenterOffsetLocal;
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
            public Vector2 StandingFootCenterOffsetLocal { get; set; }
            public float LastStride { get; set; }
            public double StrideContinuityBias { get; set; }
            public float StandingGroundLiftCorrection { get; set; }
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
