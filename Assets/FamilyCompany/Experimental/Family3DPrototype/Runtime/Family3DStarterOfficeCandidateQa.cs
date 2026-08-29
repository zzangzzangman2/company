using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using FamilyCompany.Presentation.Unity.OfficeRuntime;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEngine;

namespace FamilyCompany.Experimental.Family3D
{
    /// <summary>
    /// Production-free presentation adapter for viewing the four isolated 3D candidates while
    /// the real Starter Office simulation remains authoritative.  It never writes production
    /// state: live OfficeRuntimeAgent position, displacement, gait phase and direction are read
    /// only, and the existing SpriteRenderers are hidden solely through forceRenderingOff.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Family3DStarterOfficeCandidateQa : MonoBehaviour
    {
        public const string Contract = "FC-FAMILY-3D-STARTER-OFFICE-CANDIDATE-QA-V1";
        public const int RequiredActorCount = 4;
        public const float MovementEpsilonSqr = 0.000001f;
        public const float StaticMapScaleTolerance = 0.005f;
        private const int DefaultMaximumFatherCompositeFrames = 180;

        // Capture contract, rewritten 2026-08-26.
        //
        // The V22 capture wrote one telemetry sample only when it wrote a PNG, and it wrote a PNG
        // every sixth moving frame. Because a 1280x720 encode is slow and its cost varies, the
        // samples landed at a mean interval of 0.0973 s with a 0.5327 s worst case — an effective
        // 10.3 fps against a ~1 s gait cycle, right at the Nyquist boundary. 17.4 s of run should
        // hold about 17.5 phase wraps and only 12 were observable, so reading the period off the
        // wraps produced 1.389 s, an aliasing artifact rather than a measurement.
        //
        // Two changes remove that. Time.captureDeltaTime pins the simulation to a fixed step, so a
        // slow encode can no longer stretch the gait between samples, and telemetry is recorded on
        // every moving frame regardless of whether that frame is also written to disk. PNGs stay on
        // a stride purely to bound disk cost; they are no longer what defines the sample rate.
        private const float FatherCaptureDeltaSeconds = 1f / 60f;
        private const int MaximumFatherTelemetrySamples = 3600;
        // Measured from the forced proof route: one circuit advances 7.950477 Unity/office gait
        // units. Nine procedural cycles per circuit makes position, facing and foot phase close on
        // the same frame, avoiding the 0.32-cycle GIF seam seen with the inherited 0.8526 value.
        private const float FatherCleanBipedNaturalStrideOfficeUnits = 0.8833864f;
        // 60 fps telemetry stays exhaustive, while every eighth frame yields a 7.5 fps visual
        // proof covering both 11.2-second route circuits instead of V33's first six seconds only.
        private const int DefaultFatherCompositeFrameStride = 8;

        [Header("Candidate prefabs (Experimental only)")]
        [SerializeField] private GameObject playerCandidate;
        [SerializeField] private GameObject olderSisterCandidate;
        [SerializeField] private GameObject fatherCandidate;
        [SerializeField] private GameObject motherCandidate;
        [SerializeField] private AnimationClip sharedHumanoidWalkClip;
        [SerializeField] private AnimationClip fatherHiggsfieldIdleClip;
        [SerializeField] private bool fatherStaticRootMotionOnly;
        [SerializeField] private bool fatherHiggsfieldIdleRun;
        [SerializeField] private bool fatherCleanBipedNaturalWalk;

        // Facing offset and stride belong to the body-and-clip pair, not to the project. Putting
        // Each clean-rig/action revision is measured again, so both are configured per candidate
        // and the command line only overrides them for controlled comparisons.
        // See Docs/FAMILY_3D_WORKSTATION_CHARACTER_REUSE_CONTRACT_2026-08-28.md.
        [SerializeField] private float fatherMotionFacingOffsetDegreesAsset = 90f;
        [SerializeField] private bool fatherClipAnatomicalSanitizationAsset;
        [SerializeField] private float fatherMotionStrideOfficeUnitsAsset;
        [SerializeField] private bool fatherClipMuscleDeltaRetargetAsset;
        [SerializeField] private bool fatherClipStableBodySideArmsAsset;
        [SerializeField] private float fatherMotionCycleSecondsAsset;
        [SerializeField] private Texture2D fatherStaticAlbedo;

        // Serialized rather than resolved by Shader.Find at runtime. Unity strips any shader no
        // scene or material asset references, and Unlit/Texture was in exactly that position: the
        // editor found it, every built player did not, and the code quietly fell back to
        // Sprites/Default. A sprite shader on a skinned 3D mesh multiplies by vertex colour and
        // runs with ZWrite off, which is why V18 through V22 all rendered the Father as a dark,
        // depth-sorted-wrong silhouette while his albedo was correct the whole time. Referencing a
        // material asset from the scene is what actually keeps the shader in the build.
        [SerializeField] private Material fatherExactAlbedoMaterial;

        [Header("Isolated overlay")]
        [SerializeField] private Camera qaOverlayCamera;
        [SerializeField, Range(0, 31)] private int qaLayer = 31;
        [SerializeField] private float groundY;
        [SerializeField] private float fallbackOfficeWorldToQaScale = 1f;
        [SerializeField] private float starterReadyTimeoutSeconds = 120f;

        private readonly List<Binding> bindings = new List<Binding>(RequiredActorCount);
        private readonly Dictionary<Camera, int> sourceCameraCullingMasks =
            new Dictionary<Camera, int>();
        private readonly List<FatherCaptureSample> fatherCaptureSamples =
            new List<FatherCaptureSample>(MaximumFatherTelemetrySamples);
        private double fatherCaptureSimulationSeconds;
        private StarterOfficeRuntimeBootstrap starter;
        private bool bindAttemptActive;
        private bool shuttingDown;
        private int movingSampleFrames;
        private int compositeCapturedFrames;
        private int fatherMovingSampleFrames;
        private bool fatherMapWalkQa;
        private bool fatherDeskWorkQa;
        private bool fatherFourDirectionDeskPoseQa;
        private bool fatherDeskWorkProofActive;
        private bool fatherDeskWorkProofCompleted;
        private int fatherDeskWorkSampleFrames;
        private int fatherDeskWorkFrames;
        private double fatherDeskWorkClockSeconds;
        private float fatherDeskSeatedBlend01;
        private string fatherDeskLastPhase = string.Empty;
        private readonly List<string> fatherDeskObservedPhases = new List<string>();
        private Family3DWorkstationQa fatherDeskWorkstation;
        private OfficeSeatSlot fatherDeskSeat;
        private readonly List<Family3DWorkstationQa> v27Workstations =
            new List<Family3DWorkstationQa>();
        private readonly List<OfficeSeatSlot> v27WorkstationSeats =
            new List<OfficeSeatSlot>();
        private int v27ExpectedWorkstationCount;
        private int v27VisibleLegacyWorkstationRendererCount;
        private Vector2Int fatherDeskFootprintOrigin;
        private Vector2Int fatherDeskFootprintSize;
        private string[] fatherDeskBlockedCells = Array.Empty<string>();
        private bool fatherDeskBlockedCellsNonWalkable;
        private float fatherDeskLegacyChairAnchorOffsetWorld;
        private float fatherDeskResolvedChairActorSocketErrorWorld;
        private readonly List<FatherDeskDirectionReceipt> fatherDeskDirectionReceipts =
            new List<FatherDeskDirectionReceipt>(4);
        private bool fatherDeskFourDirectionProofCompleted;
        private string fatherDeskCapturePrefix =
            "father-v19-full-3d-desk-work-actual-map";
        private readonly List<RendererState> hiddenSourceFurniture = new List<RendererState>();
        private float fatherMotionStrideOfficeUnits;
        private float fatherMotionYawDegreesPerSecond;
        private float fatherMotionFacingOffsetDegrees;
        private float fatherMotionTurnSeconds;
        private bool fatherMotionYawSweep;
        private int fatherCompositeFrameStride = DefaultFatherCompositeFrameStride;
        private int maximumFatherCompositeFrames = DefaultMaximumFatherCompositeFrames;
        private int fatherCompositeCaptureWidth = 1280;
        private int fatherCompositeCaptureHeight = 720;
        private bool fatherProofRouteActive;
        private bool fatherProofRouteCompleted;
        private int fatherProofRouteCircuit = -1;
        private int fatherProofRouteLeg = -1;
        private int minimumCompositeLumaRange = 255;
        private int maximumCompositeLumaRange;

        public bool IsBound { get; private set; }
        public string FailureReason { get; private set; } = string.Empty;
        public int BindingCount => bindings.Count;
        public bool ProductionMutation => false;
        public bool ProductionEligible => false;

        private bool FatherUsesCleanBipedCasualWalk =>
            fatherHiggsfieldIdleRun && fatherClipAnatomicalSanitizationAsset;

        private bool FatherUsesStableBodySideArmWalk =>
            FatherUsesCleanBipedCasualWalk && fatherClipStableBodySideArmsAsset;

        private bool FatherUsesNative613Package =>
            fatherHiggsfieldIdleRun &&
            fatherHiggsfieldIdleClip == null &&
            !fatherClipMuscleDeltaRetargetAsset &&
            !fatherClipAnatomicalSanitizationAsset;

        public void Configure(
            GameObject player,
            GameObject olderSister,
            GameObject father,
            GameObject mother,
            AnimationClip sharedWalk,
            Camera overlayCamera,
            int isolatedLayer,
            float fallbackScale = 1f,
            float qaGroundY = 0f)
        {
            playerCandidate = player;
            olderSisterCandidate = olderSister;
            fatherCandidate = father;
            motherCandidate = mother;
            sharedHumanoidWalkClip = sharedWalk;
            qaOverlayCamera = overlayCamera;
            qaLayer = Mathf.Clamp(isolatedLayer, 0, 31);
            fallbackOfficeWorldToQaScale = Mathf.Max(0.0001f, fallbackScale);
            groundY = qaGroundY;
            fatherStaticRootMotionOnly = false;
            fatherHiggsfieldIdleRun = false;
            fatherCleanBipedNaturalWalk = false;
            fatherHiggsfieldIdleClip = null;
            fatherStaticAlbedo = null;
            fatherMotionCycleSecondsAsset = 0f;
        }

        public void ConfigureFatherStaticRootMotionOnly(
            GameObject father,
            Texture2D albedo,
            Material exactAlbedoMaterial,
            Camera overlayCamera,
            int isolatedLayer,
            float fallbackScale = 1f,
            float qaGroundY = 0f)
        {
            playerCandidate = null;
            olderSisterCandidate = null;
            fatherCandidate = father;
            motherCandidate = null;
            sharedHumanoidWalkClip = null;
            fatherStaticAlbedo = albedo;
            fatherExactAlbedoMaterial = exactAlbedoMaterial;
            qaOverlayCamera = overlayCamera;
            qaLayer = Mathf.Clamp(isolatedLayer, 0, 31);
            fallbackOfficeWorldToQaScale = Mathf.Max(0.0001f, fallbackScale);
            groundY = qaGroundY;
            fatherStaticRootMotionOnly = true;
            fatherHiggsfieldIdleRun = false;
            fatherCleanBipedNaturalWalk = false;
            fatherHiggsfieldIdleClip = null;
            fatherMotionCycleSecondsAsset = 0f;
        }

        public void ConfigureFatherHiggsfieldIdleRun(
            GameObject father,
            Texture2D albedo,
            Material exactAlbedoMaterial,
            AnimationClip idleClip,
            AnimationClip runClip,
            Camera overlayCamera,
            int isolatedLayer,
            float facingOffsetDegrees = 90f,
            float strideOfficeUnits = 0f,
            bool clipMuscleDeltaRetarget = false,
            bool clipAnatomicalSanitization = false,
            bool clipStableBodySideArms = false,
            float fallbackScale = 1f,
            float qaGroundY = 0f,
            float sourceAuthoredCycleSeconds = 0f)
        {
            playerCandidate = null;
            olderSisterCandidate = null;
            fatherCandidate = father;
            motherCandidate = null;
            sharedHumanoidWalkClip = runClip;
            fatherHiggsfieldIdleClip = idleClip;
            fatherStaticAlbedo = albedo;
            fatherExactAlbedoMaterial = exactAlbedoMaterial;
            qaOverlayCamera = overlayCamera;
            qaLayer = Mathf.Clamp(isolatedLayer, 0, 31);
            fallbackOfficeWorldToQaScale = Mathf.Max(0.0001f, fallbackScale);
            groundY = qaGroundY;
            fatherStaticRootMotionOnly = false;
            fatherHiggsfieldIdleRun = true;
            fatherCleanBipedNaturalWalk = false;
            fatherMotionFacingOffsetDegreesAsset = facingOffsetDegrees;
            fatherMotionStrideOfficeUnitsAsset = strideOfficeUnits;
            fatherClipMuscleDeltaRetargetAsset = clipMuscleDeltaRetarget;
            fatherClipAnatomicalSanitizationAsset = clipAnatomicalSanitization;
            fatherClipStableBodySideArmsAsset = clipStableBodySideArms;
            fatherMotionCycleSecondsAsset = Mathf.Max(0f, sourceAuthoredCycleSeconds);
        }

        public void ConfigureFatherCleanBipedNaturalWalk(
            GameObject father,
            Texture2D albedo,
            Material exactAlbedoMaterial,
            Camera overlayCamera,
            int isolatedLayer,
            float fallbackScale = 1f,
            float qaGroundY = 0f)
        {
            playerCandidate = null;
            olderSisterCandidate = null;
            fatherCandidate = father;
            motherCandidate = null;
            sharedHumanoidWalkClip = null;
            fatherHiggsfieldIdleClip = null;
            fatherMotionCycleSecondsAsset = 0f;
            fatherStaticAlbedo = albedo;
            fatherExactAlbedoMaterial = exactAlbedoMaterial;
            qaOverlayCamera = overlayCamera;
            qaLayer = Mathf.Clamp(isolatedLayer, 0, 31);
            fallbackOfficeWorldToQaScale = Mathf.Max(0.0001f, fallbackScale);
            groundY = qaGroundY;
            fatherStaticRootMotionOnly = false;
            fatherHiggsfieldIdleRun = false;
            fatherCleanBipedNaturalWalk = true;
        }

        /// <summary>
        /// Deterministic fallback when either camera is unavailable.  Normal QA rendering uses
        /// MapOfficeActorToQaGround so the production and overlay viewport coordinates agree.
        /// </summary>
        public static Vector3 MapOfficeXYToUnityXZ(
            Vector2 officePosition,
            float scale,
            float targetGroundY)
        {
            return new Vector3(
                officePosition.x * scale,
                targetGroundY,
                officePosition.y * scale);
        }

        /// <summary>
        /// Office directions are South, SW, W, NW, North, NE, East, SE.  Candidate model forward
        /// is +Z, therefore North (4) is yaw 0 and the exact mapping is (direction - 4) * 45.
        /// </summary>
        /// <summary>
        /// Faces the direction the actor actually travels, measured from its own ground positions.
        ///
        /// The octant table is not that direction. MapOfficeDirectionToUnityYaw spaces the eight
        /// office facings 45 degrees apart in QA world space, but the office-to-QA ground mapping
        /// does not scale X and Z equally, so a diagonal leg travels at 54.7 degrees while the table
        /// says 45. Measured over a full V27 route the error alternated +9.7 and -9.7 degrees by
        /// octant, which is the sqrt(2) skew of that mapping, and the feet swept that far off the
        /// travel line every frame. Deriving the facing from consecutive ground positions removes
        /// the skew by construction and needs no table. The table remains the fallback for the
        /// frames before the actor has moved at all.
        /// </summary>
        private static Quaternion ResolveTravelYaw(
            Binding binding,
            Vector3 groundPosition,
            float facingOffsetDegrees)
        {
            if (binding.HasQaGroundPosition)
            {
                Vector3 delta = groundPosition - binding.LastQaGroundPosition;
                delta.y = 0f;
                if (delta.sqrMagnitude > 1e-8f)
                {
                    // The imported body's forward is not +Z, so LookRotation alone points it wrong.
                    // The offset is a measured property of this rig, not a guess: hold the actor at
                    // known yaw values with -family3d-father-v18-motion-yaw-sweep, photograph each,
                    // and read off which yaw shows the back of the head. Do not infer it from foot
                    // swing or toe direction; a swing axis is symmetric and carries no sign.
                    binding.TravelYaw =
                        Quaternion.LookRotation(delta.normalized, Vector3.up) *
                        Quaternion.Euler(0f, facingOffsetDegrees, 0f);
                    binding.HasTravelYaw = true;
                }
            }
            binding.LastQaGroundPosition = groundPosition;
            binding.HasQaGroundPosition = true;
            return binding.HasTravelYaw
                ? binding.TravelYaw
                : MapOfficeDirectionToUnityYaw(binding.Agent.CurrentDirection);
        }

        /// <summary>
        /// Turns the host toward the travel direction at a bounded rate rather than snapping.
        ///
        /// The office resolves facing to eight octants, so a corner is a 90 degree step delivered in
        /// one frame. At the office walking speed the Father covers about 0.17 units while turning
        /// 90 degrees at this rate, which is roughly what walking a corner looks like; snapping
        /// covers none. Only the first frame after binding adopts the target outright, so a spawn
        /// does not spin; every later change, a 180 degree reversal included, is rate bounded.
        /// </summary>
        private Quaternion ResolveBlendedYaw(Binding binding, Vector3 groundPosition)
        {
            // Diagnostic sweep: hold the actor at a known yaw that steps 45 degrees every 40 moving
            // frames, so a single run photographs all eight facings and the mapping from yaw to the
            // side the camera sees can be read off the frames instead of inferred.
            if (fatherMotionYawSweep)
            {
                float swept = 15f * Mathf.Floor(fatherMovingSampleFrames / 8f);
                binding.BlendedYaw = Quaternion.Euler(0f, swept, 0f);
                binding.HasBlendedYaw = true;
                return binding.BlendedYaw;
            }

            Quaternion target = ResolveTravelYaw(
                binding, groundPosition, fatherMotionFacingOffsetDegrees);
            if (!binding.HasBlendedYaw)
            {
                binding.BlendedYaw = target;
                binding.HasBlendedYaw = true;
                return target;
            }

            // Constant rate for the whole turn, fixed when the turn starts. The travel direction
            // itself swings a corner in 2 frames, so every frame after that is the body catching
            // up, and the office is already moving the other way for all of them.
            //
            // The rate has to be set from the angle the turn began with, not from the angle still
            // remaining. Dividing the remaining angle by the turn time is an exponential approach:
            // it slows as it closes and never actually arrives, which measured as 0.33 s for a 91
            // degree corner against the 0.18 s the divisor implied. Latching the rate at the start
            // makes the turn linear and it lands on time.
            const float turnRestartDegrees = 5f;
            float toTarget = Quaternion.Angle(binding.BlendedYaw, target);
            if (!binding.HasActiveTurn ||
                Quaternion.Angle(binding.ActiveTurnTarget, target) > turnRestartDegrees)
            {
                binding.ActiveTurnTarget = target;
                binding.ActiveTurnRate = toTarget / Mathf.Max(fatherMotionTurnSeconds, 0.0001f);
                binding.HasActiveTurn = true;
            }

            float rate = Mathf.Max(
                fatherMotionYawDegreesPerSecond > 0f ? fatherMotionYawDegreesPerSecond : 360f,
                binding.ActiveTurnRate);
            binding.BlendedYaw = Quaternion.RotateTowards(
                binding.BlendedYaw, target, rate * Time.deltaTime);
            if (toTarget <= 0.01f)
                binding.HasActiveTurn = false;
            return binding.BlendedYaw;
        }

        public static Quaternion MapOfficeDirectionToUnityYaw(int direction)
        {
            int octant = (direction % 8 + 8) % 8;
            return Quaternion.Euler(0f, (octant - 4) * 45f, 0f);
        }

        private IEnumerator Start()
        {
            Application.runInBackground = true;
            float autoQuitSeconds;
            try
            {
                autoQuitSeconds = ResolveAutoQuitSeconds();
                fatherFourDirectionDeskPoseQa = HasCommandLineFlag(
                    "-family3d-father-v19-four-direction-desk-pose-qa");
                fatherDeskWorkQa = fatherFourDirectionDeskPoseQa ||
                    HasCommandLineFlag("-family3d-father-v19-desk-work-qa");
                fatherMapWalkQa = fatherDeskWorkQa ||
                    HasCommandLineFlag("-family3d-father-map-walk-qa") ||
                    HasCommandLineFlag("-family3d-father-v18-static-map-qa") ||
                    HasCommandLineFlag("-family3d-father-v18-motion-map-qa");
                fatherCompositeFrameStride = ResolvePositiveIntArgument(
                    "-family3d-father-map-capture-stride",
                    DefaultFatherCompositeFrameStride,
                    1,
                    12);
                maximumFatherCompositeFrames = ResolvePositiveIntArgument(
                    "-family3d-father-map-maximum-captures",
                    DefaultMaximumFatherCompositeFrames,
                    24,
                    900);
                fatherCompositeCaptureWidth = ResolvePositiveIntArgument(
                    "-family3d-father-map-capture-width",
                    1280,
                    1280,
                    3840);
                fatherCompositeCaptureHeight = ResolvePositiveIntArgument(
                    "-family3d-father-map-capture-height",
                    720,
                    720,
                    2160);
            }
            catch (Exception exception)
            {
                Fail(exception.Message);
                yield break;
            }

            bindAttemptActive = true;
            float elapsed = 0f;
            while (!shuttingDown)
            {
                if (starter == null)
                    starter = FindFirstObjectByType<StarterOfficeRuntimeBootstrap>();

                if (starter != null &&
                    starter.PreparationState == StarterOfficeRuntimePreparationState.Failed)
                {
                    Fail("Starter Office preparation failed: " + starter.PreparationFailureReason);
                    yield break;
                }

                if (starter != null && starter.IsReady && starter.Actors.Count == RequiredActorCount)
                    break;

                if (elapsed >= starterReadyTimeoutSeconds)
                {
                    Fail(
                        "Timed out waiting for a ready four-agent StarterOfficeRuntimeBootstrap " +
                        $"after {starterReadyTimeoutSeconds:F1}s.");
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (shuttingDown)
                yield break;

            try
            {
                BindAll();
                if (fatherMapWalkQa)
                {
                    if (HasCommandLineFlag("-familyCompanyMovementLayoutQa"))
                        throw new InvalidOperationException(
                            "Dedicated Father map-walk proof cannot be combined with the multi-layout movement QA.");

                    // Pinned only after the office reports ready, so the readiness wait above keeps
                    // running on real time and its timeout still means what it says. From here the
                    // route advances in exact fixed steps and the wall clock only bounds the run
                    // through the realtime auto-quit.
                    Time.captureDeltaTime = FatherCaptureDeltaSeconds;
                    fatherMotionStrideOfficeUnits = fatherCleanBipedNaturalWalk
                        ? FatherCleanBipedNaturalStrideOfficeUnits
                        : ResolveFatherMotionStrideOfficeUnits();
                    fatherMotionYawDegreesPerSecond = ResolveFatherMotionYawDegreesPerSecond();
                    fatherMotionFacingOffsetDegrees = ResolveFatherMotionFacingOffsetDegrees();
                    fatherMotionTurnSeconds = ResolveFatherMotionTurnSeconds();
                    fatherMotionYawSweep = HasCommandLineFlag("-family3d-father-v18-motion-yaw-sweep");
                    StartCoroutine(
                        fatherFourDirectionDeskPoseQa
                            ? RunFatherFourDirectionDeskPoseProof()
                            : fatherDeskWorkQa
                            ? RunFatherDeskWorkProof()
                            : RunFatherMapWalkProof());
                }
                WriteRuntimeReceipt("BOUND");
            }
            catch (Exception exception)
            {
                Fail(exception.GetType().Name + ": " + exception.Message);
                Debug.LogException(exception, this);
            }
            finally
            {
                bindAttemptActive = false;
            }

            if (autoQuitSeconds > 0f && IsBound)
            {
                yield return new WaitForSecondsRealtime(autoQuitSeconds);
                WriteRuntimeReceipt("AUTO_QUIT_AFTER_RUNTIME_SAMPLE");
                Application.Quit(IsBound ? 0 : 2);
            }
        }

        private void LateUpdate()
        {
            if (shuttingDown || starter == null)
                return;

            // ScenePreviewJump correctly disables every non-office camera while it installs the
            // additive office. This camera exists only in the copied QA scene, so reclaim it after
            // the real Starter Office bootstrap appears and keep the production camera untouched.
            if (qaOverlayCamera != null && !qaOverlayCamera.enabled)
                qaOverlayCamera.enabled = true;

            if (!IsBound)
            {
                if (!bindAttemptActive && starter.IsReady && starter.Actors.Count == RequiredActorCount)
                {
                    try
                    {
                        BindAll();
                        WriteRuntimeReceipt("REBOUND");
                    }
                    catch (Exception exception)
                    {
                        Fail(exception.GetType().Name + ": " + exception.Message);
                        Debug.LogException(exception, this);
                    }
                }
                return;
            }

            if (!BindingsStillMatchStarter())
            {
                ReleaseBindings();
                if (starter.IsReady && starter.Actors.Count == RequiredActorCount)
                {
                    try
                    {
                        BindAll();
                        WriteRuntimeReceipt("REBOUND_AFTER_RUNTIME_REBUILD");
                    }
                    catch (Exception exception)
                    {
                        Fail(exception.GetType().Name + ": " + exception.Message);
                        Debug.LogException(exception, this);
                    }
                }
                return;
            }

            Camera sourceOfficeCamera = Camera.main;
            ExcludeQaLayerFromSourceCamera(sourceOfficeCamera);
            for (var index = 0; index < bindings.Count; index++)
                UpdateBinding(bindings[index], sourceOfficeCamera);

            bool anyMoving = false;
            for (var index = 0; index < bindings.Count; index++)
                anyMoving |= bindings[index].IsMoving;
            if (anyMoving)
            {
                movingSampleFrames++;
                if (!fatherMapWalkQa && HasExplicitRuntimeOutput() && compositeCapturedFrames < 3 &&
                    (movingSampleFrames == 1 || movingSampleFrames % 120 == 0))
                    CaptureCompositeQaFrame(sourceOfficeCamera);
            }

            if (fatherMapWalkQa)
            {
                Binding father = bindings.Find(candidate =>
                    string.Equals(candidate.FamilyId, "father", StringComparison.Ordinal));
                if (fatherDeskWorkProofActive && father != null)
                {
                    fatherDeskWorkSampleFrames++;
                    if (father.IsMoving)
                        fatherMovingSampleFrames++;
                    fatherCaptureSimulationSeconds += Time.captureDeltaTime > 0f
                        ? Time.captureDeltaTime
                        : Time.unscaledDeltaTime;
                    if (HasExplicitRuntimeOutput())
                    {
                        if (fatherCaptureSamples.Count < MaximumFatherTelemetrySamples)
                            RecordFatherCaptureSample(father, fatherDeskWorkSampleFrames);
                        if (!fatherFourDirectionDeskPoseQa &&
                            compositeCapturedFrames < maximumFatherCompositeFrames &&
                            (fatherDeskWorkSampleFrames == 1 ||
                             fatherDeskWorkSampleFrames % fatherCompositeFrameStride == 0))
                            CaptureCompositeQaFrame(
                                sourceOfficeCamera,
                                fatherDeskCapturePrefix);
                    }
                }
                else if (fatherProofRouteActive && father != null && father.IsMoving)
                {
                    fatherMovingSampleFrames++;
                    fatherCaptureSimulationSeconds += Time.captureDeltaTime > 0f
                        ? Time.captureDeltaTime
                        : Time.unscaledDeltaTime;
                    if (HasExplicitRuntimeOutput())
                    {
                        if (fatherCaptureSamples.Count < MaximumFatherTelemetrySamples)
                            RecordFatherCaptureSample(father, fatherMovingSampleFrames);
                        int stride = fatherStaticRootMotionOnly ? 12 : fatherCompositeFrameStride;
                        if (compositeCapturedFrames < maximumFatherCompositeFrames &&
                            (fatherMovingSampleFrames == 1 ||
                             fatherMovingSampleFrames % stride == 0))
                            CaptureCompositeQaFrame(
                                sourceOfficeCamera,
                                fatherStaticRootMotionOnly
                                    ? "father-v18-higgsfield-static-map-walk"
                                    : FatherUsesNative613Package
                                        ? "father-v18-native-613-map-walk"
                                    : FatherUsesStableBodySideArmWalk
                                        ? "father-v18-clean-biped-stable-arm-walk-map"
                                    : FatherUsesCleanBipedCasualWalk
                                        ? "father-v18-clean-biped-casual-walk-map"
                                    : fatherCleanBipedNaturalWalk
                                        ? "father-v18-clean-biped-natural-map-walk"
                                    : fatherHiggsfieldIdleRun
                                        ? "father-v18-higgsfield-idle-run-map-walk"
                                    : "father-stylized-sd-map-walk-v17");
                    }
                }
            }
        }

        private IEnumerator RunFatherDeskWorkProof()
        {
            yield return null;
            if (starter == null || starter.World == null)
            {
                Fail("Father desk-work proof could not resolve the Starter Office runtime world.");
                Application.Quit(2);
                yield break;
            }

            // A normal new game intentionally starts as an empty shell. This QA request is the
            // next furnished-office gate, so install the canonical StarterOfficeV1 layout through
            // the existing QA-only rebuild entry point. No scene, default asset or save is written.
            OfficeGrid furnished = OfficeGridLayouts.CreateStarterOfficeV1();
            string furnishedHash = furnished.ComputeLayoutHash();
            if (!string.Equals(starter.LayoutHash, furnishedHash, StringComparison.Ordinal))
            {
                starter.ApplyLayoutForQa(furnished);
                float rebuildDeadline = Time.realtimeSinceStartup + 30f;
                while (Time.realtimeSinceStartup < rebuildDeadline &&
                       (!starter.IsReady ||
                        !string.Equals(starter.LayoutHash, furnishedHash, StringComparison.Ordinal) ||
                        !IsBound))
                    yield return null;
                if (!starter.IsReady ||
                    !string.Equals(starter.LayoutHash, furnishedHash, StringComparison.Ordinal))
                {
                    Fail("Canonical furnished Starter Office did not rebuild for desk-work QA.");
                    Application.Quit(2);
                    yield break;
                }
                yield return new WaitForEndOfFrame();
            }

            Binding fatherBinding = bindings.Find(candidate =>
                string.Equals(candidate.FamilyId, "father", StringComparison.Ordinal));
            if (fatherBinding == null)
            {
                Fail("Father desk-work proof could not resolve the rebuilt Father binding.");
                Application.Quit(2);
                yield break;
            }

            OfficeRuntimeAgent father = fatherBinding.Agent;
            OfficeSeatSlot seat;
            try
            {
                seat = starter.World.Workstations.RequiredSeat("seat_father");
                if (!seat.HasWorkstationBinding)
                    throw new InvalidOperationException(
                        "The real Father seat has no bound work surface.");
                SetupV27Workstations(fatherBinding, seat, Camera.main);
            }
            catch (Exception exception)
            {
                Fail("Father desk-work setup failed: " + exception.Message);
                Debug.LogException(exception, this);
                Application.Quit(2);
                yield break;
            }

            var protectedCells = new[]
            {
                OfficeRuntimeWorkstationService.StarterEntranceCell,
                seat.ApproachCell,
                seat.Cell
            };
            ParkOtherActorsForFatherLoop(father, protectedCells);
            father.QaTeleportToCell(OfficeRuntimeWorkstationService.StarterEntranceCell);
            father.QaSetDirectMovementInput(Vector2.zero);
            Time.timeScale = 1f;
            yield return null;
            yield return new WaitForEndOfFrame();

            // Measure only this real entrance-to-seat run. A valid proof may have blocked move
            // attempts while steering, but it may never commit a pose inside a desk, an unowned
            // chair, or another actor.
            starter.World.Occupancy.ResetMetrics();
            fatherDeskWorkProofActive = true;
            if (!father.QaBeginSeatedWorkAtSeat(seat.SeatId, "father-v19-full-3d-desk-work"))
            {
                Fail("The real Father agent rejected its assigned seated-work destination.");
                Application.Quit(2);
                yield break;
            }

            Debug.Log(
                "FAMILY_3D_FATHER_DESK_WORK_QA: starting real route/seat claim; seat=" +
                seat.SeatId + " desk=" + seat.WorkSurfaceFurnitureId +
                " chair=" + seat.ChairFurnitureId +
                " productionEligible=false.",
                this);

            float deadline = Time.realtimeSinceStartup + 120f;
            while (father.Phase != OfficeRuntimeAgentPhase.Working &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;
            if (father.Phase != OfficeRuntimeAgentPhase.Working)
            {
                Fail("Father did not reach Working; last phase=" + father.Phase + ".");
                Application.Quit(2);
                yield break;
            }

            // Six deterministic seconds show several complete 0.8-second typing loops while the
            // real seat remains claimed. Count simulation frames rather than encoder wall time.
            const int requiredWorkFrames = 360;
            for (var frame = 0; frame < requiredWorkFrames; frame++)
            {
                if (father.Phase != OfficeRuntimeAgentPhase.Working)
                {
                    Fail("Father left Working before the desk-work proof completed.");
                    Application.Quit(2);
                    yield break;
                }
                yield return null;
            }

            RefreshV27SourceFurnitureMask();
            v27VisibleLegacyWorkstationRendererCount =
                CountVisibleLegacyWorkstationRenderers();
            if (v27Workstations.Count != v27ExpectedWorkstationCount ||
                v27VisibleLegacyWorkstationRendererCount != 0 ||
                starter.World.Occupancy.StaticViolationCount != 0 ||
                starter.World.Occupancy.InteractionViolationCount != 0 ||
                starter.World.Occupancy.AgentPenetrationCount != 0)
            {
                Fail(
                    "Atomic workstation/avoidance proof failed: created=" +
                    v27Workstations.Count + "/" + v27ExpectedWorkstationCount +
                    " visibleLegacyRenderers=" +
                    v27VisibleLegacyWorkstationRendererCount +
                    " staticViolations=" +
                    starter.World.Occupancy.StaticViolationCount +
                    " interactionViolations=" +
                    starter.World.Occupancy.InteractionViolationCount +
                    " agentPenetrations=" +
                    starter.World.Occupancy.AgentPenetrationCount + ".");
                Application.Quit(2);
                yield break;
            }

            fatherDeskWorkProofActive = false;
            fatherDeskWorkProofCompleted = true;
            WriteRuntimeReceipt("FATHER_V19_FULL_3D_ALL_WORKSTATIONS_PROOF_COMPLETE");
            Debug.Log(
                "FAMILY_3D_FATHER_DESK_WORK_QA: COMPLETE | phases=" +
                string.Join(">", fatherDeskObservedPhases) +
                " workFrames=" + fatherDeskWorkFrames +
                " captures=" + compositeCapturedFrames +
                " productionEligible=false",
                this);
            yield return new WaitForEndOfFrame();
            Application.Quit(0);
        }

        /// <summary>
        /// Proves the seated Father against the exact four rotations produced by the production
        /// workstation placement rule. The furnished starter fixture happens to author all four
        /// desks in one direction, so replaying its four seats cannot prove a bought desk that the
        /// player subsequently turns. Each pass therefore starts from the real empty-office shell,
        /// places one atomic desk/chair/seat set through PlaceWorkstation, and lets the real Father
        /// claim and enter that seat before the Humanoid desk pose and endpoint IK are judged.
        /// </summary>
        private IEnumerator RunFatherFourDirectionDeskPoseProof()
        {
            yield return null;
            if (starter == null || starter.World == null)
            {
                Fail("Four-direction Father desk-pose proof could not resolve the runtime world.");
                Application.Quit(2);
                yield break;
            }

            fatherDeskDirectionReceipts.Clear();
            fatherDeskFourDirectionProofCompleted = false;
            var facings = new[]
            {
                OfficeFurnitureFacing.SouthEast,
                OfficeFurnitureFacing.SouthWest,
                OfficeFurnitureFacing.NorthWest,
                OfficeFurnitureFacing.NorthEast
            };
            var seatCell = new OfficeGridCoordinate(6, 6);

            for (var directionIndex = 0; directionIndex < facings.Length; directionIndex++)
            {
                OfficeFurnitureFacing deskFacing = facings[directionIndex];
                OfficeLayoutEditResult placed = OfficeLayoutEditRules.PlaceWorkstation(
                    OfficeGridLayouts.CreateNewGameEmptyOfficeV1(),
                    "desk_father",
                    "chair_father",
                    "seat_father",
                    seatCell,
                    deskFacing);
                if (!placed.Success || placed.Grid == null)
                {
                    Fail(
                        "Production workstation placement rejected Father direction " +
                        deskFacing + ": " + placed.Failure + " " + placed.Message);
                    Application.Quit(2);
                    yield break;
                }

                fatherDeskWorkProofActive = false;
                string expectedHash = placed.Grid.ComputeLayoutHash();
                starter.ApplyLayoutForQa(placed.Grid);
                float rebuildDeadline = Time.realtimeSinceStartup + 30f;
                while (Time.realtimeSinceStartup < rebuildDeadline &&
                       (!starter.IsReady ||
                        !string.Equals(starter.LayoutHash, expectedHash, StringComparison.Ordinal) ||
                        !IsBound))
                    yield return null;
                if (!starter.IsReady || !IsBound ||
                    !string.Equals(starter.LayoutHash, expectedHash, StringComparison.Ordinal))
                {
                    Fail("Father direction " + deskFacing + " did not rebuild to the requested layout.");
                    Application.Quit(2);
                    yield break;
                }
                yield return new WaitForEndOfFrame();

                Binding fatherBinding = bindings.Find(candidate =>
                    string.Equals(candidate.FamilyId, "father", StringComparison.Ordinal));
                if (fatherBinding == null)
                {
                    Fail("Father binding is missing after the " + deskFacing + " layout rebuild.");
                    Application.Quit(2);
                    yield break;
                }

                OfficeRuntimeAgent father = fatherBinding.Agent;
                OfficeSeatSlot seat;
                try
                {
                    seat = starter.World.Workstations.RequiredSeat("seat_father");
                    SetupV27Workstations(fatherBinding, seat, Camera.main);
                    ParkOtherActorsForFatherLoop(
                        father,
                        new[] { seat.ApproachCell, seat.Cell });
                }
                catch (Exception exception)
                {
                    Fail("Father " + deskFacing + " workstation setup failed: " + exception.Message);
                    Debug.LogException(exception, this);
                    Application.Quit(2);
                    yield break;
                }

                fatherDeskSeatedBlend01 = 0f;
                fatherDeskWorkClockSeconds = 0d;
                fatherDeskCapturePrefix =
                    "father-v19-four-direction-" + deskFacing.ToString().ToLowerInvariant();
                fatherProofRouteCircuit = directionIndex;
                fatherProofRouteLeg = -1;
                // Use the same proven three-cell cardinal arrival fixture as the exhaustive R5e
                // seat-docking matrix. Starting on the approach cell skips its navigation handoff;
                // starting at the office entrance can approach this isolated centre fixture on a
                // diagonal and stop at the final path threshold. The cardinal arrival still runs
                // the complete path, approach, turn and atomic seat claim for every quarter-turn.
                father.QaTeleportToCell(
                    FindFatherDeskArrivalCell(
                        starter.World.Grid,
                        seat,
                        directionIndex * 2));
                father.SetExternalDirectionalSeatingPresentation(true);
                // Loading/management UI is allowed to leave office time paused. The accepted
                // desk-work proof explicitly resumes simulation before routing; keep the same
                // contract here or the agent correctly remains at its arrival cell forever.
                Time.timeScale = 1f;
                starter.World.Occupancy.ResetMetrics();
                if (!father.QaBeginSeatedWorkAtSeat(
                        seat.SeatId,
                        "father-v19-four-direction-" + deskFacing))
                {
                    Fail("Father rejected the real " + deskFacing + " seat destination.");
                    Application.Quit(2);
                    yield break;
                }

                float seatDeadline = Time.realtimeSinceStartup + 30f;
                while (father.Phase != OfficeRuntimeAgentPhase.Working &&
                       Time.realtimeSinceStartup < seatDeadline)
                    yield return null;
                if (father.Phase != OfficeRuntimeAgentPhase.Working)
                {
                    Fail("Father did not reach Working for " + deskFacing +
                         "; last phase=" + father.Phase +
                         " position=" + father.Position +
                         " desiredVelocity=" + father.DesiredVelocity +
                         " movementBlocker=" + father.LastMovementBlocker +
                         " reservationBlocker=" + father.LastReservationBlocker + ".");
                    Application.Quit(2);
                    yield break;
                }

                fatherDeskWorkProofActive = true;
                const int settleAndTypingFrames = 72;
                for (var frame = 0; frame < settleAndTypingFrames; frame++)
                {
                    if (father.Phase != OfficeRuntimeAgentPhase.Working)
                    {
                        Fail("Father left Working during the " + deskFacing + " pose proof.");
                        Application.Quit(2);
                        yield break;
                    }
                    yield return null;
                    if (HasExplicitRuntimeOutput() &&
                        (frame == 31 || frame == 51 || frame == 71))
                        CaptureCompositeQaFrame(
                            Camera.main,
                            fatherDeskCapturePrefix,
                            fatherBinding);
                }
                fatherDeskWorkProofActive = false;

                try
                {
                    ValidateAndRecordFatherDeskDirection(
                        directionIndex,
                        deskFacing,
                        seat,
                        fatherBinding);
                }
                catch (Exception exception)
                {
                    Fail("Father " + deskFacing + " seated-pose gate failed: " + exception.Message);
                    Debug.LogException(exception, this);
                    Application.Quit(2);
                    yield break;
                }
            }

            if (fatherDeskDirectionReceipts.Count != 4)
            {
                Fail("Father four-direction proof produced " +
                     fatherDeskDirectionReceipts.Count + "/4 direction receipts.");
                Application.Quit(2);
                yield break;
            }
            for (var left = 0; left < fatherDeskDirectionReceipts.Count; left++)
            for (var right = left + 1; right < fatherDeskDirectionReceipts.Count; right++)
            {
                float yawSeparation = Mathf.Abs(Mathf.DeltaAngle(
                    fatherDeskDirectionReceipts[left].seatedRootWorldYawDegrees,
                    fatherDeskDirectionReceipts[right].seatedRootWorldYawDegrees));
                if (yawSeparation >= 45f)
                    continue;
                Fail(
                    "Father desk rotations collapsed to the same 3D body yaw: " +
                    fatherDeskDirectionReceipts[left].deskFacing + " vs " +
                    fatherDeskDirectionReceipts[right].deskFacing + " separation=" +
                    yawSeparation.ToString("F4") + " degrees.");
                Application.Quit(2);
                yield break;
            }

            fatherDeskWorkProofCompleted = true;
            fatherDeskFourDirectionProofCompleted = true;
            WriteRuntimeReceipt("FATHER_V19_FOUR_DIRECTION_DESK_POSE_PROOF_COMPLETE");
            Debug.Log(
                "FAMILY_3D_FATHER_FOUR_DIRECTION_DESK_POSE_QA: PASS | " +
                "semanticQuarterTurns=4/4 actualSeatClaims=4/4 bodyToDesk=4/4 " +
                "handsToKeyboard=4/4 feetUnderChair=4/4 captures=" +
                compositeCapturedFrames + " productionEligible=false",
                this);
            yield return new WaitForEndOfFrame();
            Application.Quit(0);
        }

        private static OfficeGridCoordinate FindFatherDeskArrivalCell(
            OfficeGrid grid,
            OfficeSeatSlot seat,
            int direction)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (seat == null) throw new ArgumentNullException(nameof(seat));
            int[] dx = { 0, -1, -1, -1, 0, 1, 1, 1 };
            int[] dy = { -1, -1, 0, 1, 1, 1, 0, -1 };
            int index = ((direction % 8) + 8) % 8;
            for (var distance = 3; distance >= 1; distance--)
            {
                var candidate = new OfficeGridCoordinate(
                    seat.ApproachCell.X + dx[index] * distance,
                    seat.ApproachCell.Y + dy[index] * distance);
                if (grid.Contains(candidate) && grid.IsWalkable(candidate))
                    return candidate;
            }
            return seat.ApproachCell;
        }

        private void ValidateAndRecordFatherDeskDirection(
            int directionIndex,
            OfficeFurnitureFacing deskFacing,
            OfficeSeatSlot seat,
            Binding fatherBinding)
        {
            if (fatherDeskWorkstation == null || fatherBinding == null ||
                fatherBinding.WalkActor == null)
                throw new InvalidOperationException("The resolved workstation or Father rig is missing.");

            Family3DWorkstationQa workstation = fatherDeskWorkstation;
            Family3DWalkActor.PoseSnapshot pose = fatherBinding.WalkActor.ReadPoseSnapshot();
            float height = Mathf.Max(pose.standingHeight, 0.25f);
            Vector3 forward = workstation.SeatedBodyForwardWorld;
            forward.y = 0f;
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3 leftHandWorld = fatherBinding.Host.transform.TransformPoint(pose.leftHandLocal);
            Vector3 rightHandWorld = fatherBinding.Host.transform.TransformPoint(pose.rightHandLocal);
            Vector3 handMidpoint = (leftHandWorld + rightHandWorld) * 0.5f;
            Vector3 expectedHandMidpoint = workstation.KeyboardWorld +
                                           Vector3.up * (0.022f * height) -
                                           forward * (0.035f * height);
            float handMidpointError = Vector3.Distance(handMidpoint, expectedHandMidpoint);
            float handOrder = Vector3.Dot(rightHandWorld - leftHandWorld, right);

            Vector3 footMidpoint = (pose.leftFootWorld + pose.rightFootWorld) * 0.5f;
            Vector3 expectedFootMidpoint = fatherBinding.Host.transform.position +
                                           forward * (0.09f * height);
            expectedFootMidpoint.y = groundY + 0.158f * height;
            float footMidpointError = Vector3.Distance(footMidpoint, expectedFootMidpoint);
            float footOrder = Vector3.Dot(pose.rightFootWorld - pose.leftFootWorld, right);
            float leftKneeBendDegrees = Vector3.Angle(
                pose.leftHipWorld - pose.leftKneeWorld,
                pose.leftFootWorld - pose.leftKneeWorld);
            float rightKneeBendDegrees = Vector3.Angle(
                pose.rightHipWorld - pose.rightKneeWorld,
                pose.rightFootWorld - pose.rightKneeWorld);
            Vector3 leftFootFromChair = pose.leftFootWorld - workstation.ChairGroundWorld;
            Vector3 rightFootFromChair = pose.rightFootWorld - workstation.ChairGroundWorld;
            leftFootFromChair.y = 0f;
            rightFootFromChair.y = 0f;
            float leftFootChairRadialClearance = leftFootFromChair.magnitude;
            float rightFootChairRadialClearance = rightFootFromChair.magnitude;
            float leftFootChairForwardClearance = Vector3.Dot(leftFootFromChair, forward);
            float rightFootChairForwardClearance = Vector3.Dot(rightFootFromChair, forward);
            Vector3 leftKneeFromChair = pose.leftKneeWorld - workstation.ChairGroundWorld;
            Vector3 rightKneeFromChair = pose.rightKneeWorld - workstation.ChairGroundWorld;
            leftKneeFromChair.y = 0f;
            rightKneeFromChair.y = 0f;
            float leftKneeChairForwardClearance = Vector3.Dot(leftKneeFromChair, forward);
            float rightKneeChairForwardClearance = Vector3.Dot(rightKneeFromChair, forward);
            var skinVertices = new List<Vector3>(8192);
            var skinRegions =
                new List<Family3DWalkActor.SeatedSkinRegion>(8192);
            int sampledSkinVertexCount =
                fatherBinding.WalkActor.CollectCurrentWorldSkinVertices(
                    skinVertices,
                    skinRegions);
            Family3DWorkstationQa.ChairSkinPenetration chairSkinPenetration =
                workstation.MeasureChairSkinPenetration(skinVertices, skinRegions);
            Vector3 expectedSeatedVisualRoot = workstation.SeatGroundWorld +
                                               forward * (0.07f * height);
            Vector3 rootDelta = fatherBinding.Host.transform.position - expectedSeatedVisualRoot;
            rootDelta.y = 0f;
            float seatRootGroundError = rootDelta.magnitude;
            float rootFacingError = Quaternion.Angle(
                fatherBinding.Host.transform.rotation,
                workstation.SeatedRotationWorld);

            const float facingToleranceDegrees = 0.1f;
            float maximumEndpointError = 0.18f * height;
            float minimumLimbSeparation = 0.05f * height;
            float minimumFootChairRadialClearance = 0.19f * height;
            float minimumFootChairForwardClearance = 0.14f * height;
            float minimumKneeChairForwardClearance = 0.12f * height;
            if ((int)deskFacing != directionIndex ||
                workstation.SeatToKeyboardFacingErrorDegrees > facingToleranceDegrees ||
                workstation.SeatToMonitorFacingErrorDegrees > facingToleranceDegrees ||
                workstation.ChairToMonitorFacingErrorDegrees > facingToleranceDegrees ||
                workstation.MonitorScreenToSeatFacingErrorDegrees > facingToleranceDegrees ||
                rootFacingError > facingToleranceDegrees ||
                seatRootGroundError > 0.001f ||
                handMidpointError > maximumEndpointError ||
                footMidpointError > maximumEndpointError ||
                handOrder < minimumLimbSeparation ||
                footOrder < minimumLimbSeparation ||
                leftKneeBendDegrees < 80f || leftKneeBendDegrees > 140f ||
                rightKneeBendDegrees < 80f || rightKneeBendDegrees > 140f ||
                leftFootChairRadialClearance < minimumFootChairRadialClearance ||
                rightFootChairRadialClearance < minimumFootChairRadialClearance ||
                leftFootChairForwardClearance < minimumFootChairForwardClearance ||
                rightFootChairForwardClearance < minimumFootChairForwardClearance ||
                leftKneeChairForwardClearance < minimumKneeChairForwardClearance ||
                rightKneeChairForwardClearance < minimumKneeChairForwardClearance ||
                sampledSkinVertexCount <= 0 ||
                chairSkinPenetration.totalPenetratingVertexCount != 0 ||
                starter.World.Occupancy.StaticViolationCount != 0 ||
                starter.World.Occupancy.InteractionViolationCount != 0 ||
                starter.World.Occupancy.AgentPenetrationCount != 0)
                throw new InvalidOperationException(
                    "directionIndex=" + directionIndex +
                    " deskFacing=" + deskFacing +
                    " seatFacing=" + seat.Facing +
                    " rootFacingError=" + rootFacingError.ToString("F4") +
                    " seatRootGroundError=" + seatRootGroundError.ToString("F5") +
                    " handMidpointError=" + handMidpointError.ToString("F5") +
                    " footMidpointError=" + footMidpointError.ToString("F5") +
                    " handOrder=" + handOrder.ToString("F5") +
                    " footOrder=" + footOrder.ToString("F5") +
                    " kneeBend=" + leftKneeBendDegrees.ToString("F2") + "/" +
                    rightKneeBendDegrees.ToString("F2") +
                    " footChairRadial=" + leftFootChairRadialClearance.ToString("F5") + "/" +
                    rightFootChairRadialClearance.ToString("F5") +
                    " footChairForward=" + leftFootChairForwardClearance.ToString("F5") + "/" +
                    rightFootChairForwardClearance.ToString("F5") +
                    " kneeChairForward=" + leftKneeChairForwardClearance.ToString("F5") + "/" +
                    rightKneeChairForwardClearance.ToString("F5") +
                    " chairSkinPenetration=" +
                    chairSkinPenetration.totalPenetratingVertexCount + "[" +
                    chairSkinPenetration.cushionVertexCount + "/" +
                    chairSkinPenetration.backUprightVertexCount + "/" +
                    chairSkinPenetration.lumbarVertexCount + "/" +
                    chairSkinPenetration.stemVertexCount + "/" +
                    chairSkinPenetration.roundFootVertexCount + "]" +
                    " cushionLocalY=" +
                    chairSkinPenetration.cushionMinimumLocalY.ToString("F4") + "/" +
                    chairSkinPenetration.cushionMaximumLocalY.ToString("F4") +
                    " cushionRegions=" +
                    chairSkinPenetration.cushionPelvisOrTorsoVertexCount + "/" +
                    chairSkinPenetration.cushionUpperLegVertexCount + "/" +
                    chairSkinPenetration.cushionLowerLegVertexCount + "/" +
                    chairSkinPenetration.cushionFootVertexCount + "/" +
                    chairSkinPenetration.cushionOtherVertexCount +
                    " occupancy=" + starter.World.Occupancy.StaticViolationCount + "/" +
                    starter.World.Occupancy.InteractionViolationCount + "/" +
                    starter.World.Occupancy.AgentPenetrationCount + ".");

            fatherDeskDirectionReceipts.Add(new FatherDeskDirectionReceipt
            {
                directionIndex = directionIndex,
                deskFacing = deskFacing.ToString(),
                seatFacing = seat.Facing.ToString(),
                approachCell = seat.ApproachCell.ToString(),
                semanticQuarterTurns = directionIndex,
                seatedRootWorldYawDegrees =
                    fatherBinding.Host.transform.rotation.eulerAngles.y,
                rootFacingErrorDegrees = rootFacingError,
                seatToKeyboardFacingErrorDegrees =
                    workstation.SeatToKeyboardFacingErrorDegrees,
                seatToMonitorFacingErrorDegrees =
                    workstation.SeatToMonitorFacingErrorDegrees,
                chairToMonitorFacingErrorDegrees =
                    workstation.ChairToMonitorFacingErrorDegrees,
                monitorScreenToSeatFacingErrorDegrees =
                    workstation.MonitorScreenToSeatFacingErrorDegrees,
                seatRootGroundError = seatRootGroundError,
                handMidpointError = handMidpointError,
                handLateralOrder = handOrder,
                footMidpointError = footMidpointError,
                footLateralOrder = footOrder,
                leftKneeBendDegrees = leftKneeBendDegrees,
                rightKneeBendDegrees = rightKneeBendDegrees,
                leftFootChairRadialClearance = leftFootChairRadialClearance,
                rightFootChairRadialClearance = rightFootChairRadialClearance,
                leftFootChairForwardClearance = leftFootChairForwardClearance,
                rightFootChairForwardClearance = rightFootChairForwardClearance,
                leftKneeChairForwardClearance = leftKneeChairForwardClearance,
                rightKneeChairForwardClearance = rightKneeChairForwardClearance,
                sampledSkinVertexCount = sampledSkinVertexCount,
                chairSkinPenetratingVertexCount =
                    chairSkinPenetration.totalPenetratingVertexCount,
                chairCushionPenetratingVertexCount = chairSkinPenetration.cushionVertexCount,
                chairBackPenetratingVertexCount =
                    chairSkinPenetration.backUprightVertexCount,
                chairLumbarPenetratingVertexCount = chairSkinPenetration.lumbarVertexCount,
                chairStemPenetratingVertexCount = chairSkinPenetration.stemVertexCount,
                chairFootPenetratingVertexCount = chairSkinPenetration.roundFootVertexCount,
                chairCushionPenetrationMinimumLocalY =
                    chairSkinPenetration.cushionMinimumLocalY,
                chairCushionPenetrationMaximumLocalY =
                    chairSkinPenetration.cushionMaximumLocalY,
                staticViolationCount = starter.World.Occupancy.StaticViolationCount,
                interactionViolationCount = starter.World.Occupancy.InteractionViolationCount,
                agentPenetrationCount = starter.World.Occupancy.AgentPenetrationCount
            });
        }

        private void SetupV27Workstations(
            Binding fatherBinding,
            OfficeSeatSlot fatherSeat,
            Camera sourceOfficeCamera)
        {
            v27Workstations.Clear();
            v27WorkstationSeats.Clear();
            v27ExpectedWorkstationCount = 0;
            fatherDeskWorkstation = null;

            for (var index = 0; index < starter.World.Grid.SeatSlots.Count; index++)
            {
                OfficeSeatSlot seat = starter.World.Grid.SeatSlots[index];
                if (!seat.HasWorkstationBinding)
                    continue;
                v27ExpectedWorkstationCount++;
                bool isFatherSeat = string.Equals(
                    seat.SeatId,
                    fatherSeat.SeatId,
                    StringComparison.Ordinal);
                Family3DWorkstationQa workstation = CreateV27Workstation(
                    fatherBinding,
                    seat,
                    sourceOfficeCamera,
                    isFatherSeat);
                v27Workstations.Add(workstation);
                v27WorkstationSeats.Add(seat);
                HideSourceFurniture(seat.ChairFurnitureId);
                HideSourceFurniture(seat.WorkSurfaceFurnitureId);
            }

            if (fatherDeskWorkstation == null)
                throw new InvalidOperationException(
                    "Father V27 workstation was not created from the live seat set.");
            if (v27Workstations.Count != v27ExpectedWorkstationCount)
                throw new InvalidOperationException(
                    "Not every semantic workstation received a V27 visual replacement.");
        }

        private Family3DWorkstationQa CreateV27Workstation(
            Binding fatherBinding,
            OfficeSeatSlot seat,
            Camera sourceOfficeCamera,
            bool captureFatherReceipt)
        {
            if (!starter.World.FurniturePresenter.TryGetFurniture(
                    seat.WorkSurfaceFurnitureId,
                    out PlacedOfficeFurniture desk) || desk == null)
                throw new InvalidOperationException(
                    "Workstation semantic furniture is unavailable: " +
                    seat.WorkSurfaceFurnitureId);

            // Map the authoritative semantic footprint and calibrated interaction sockets, not
            // the visible sprite bounds and not a character-height guess. Purchase placement,
            // collision, save/load, furniture ghost and this 3D proof now all share the same
            // integer origin and footprint.
            Vector3 basisSource = starter.World.Presenter.CellCenterWorld(seat.Cell);
            Vector3 basisQa = MapOfficeWorldToQaGround(basisSource, sourceOfficeCamera);
            Vector3 gridRight = MapOfficeWorldToQaGround(
                basisSource + starter.World.Presenter.CellBasisXWorld(),
                sourceOfficeCamera) - basisQa;
            Vector3 gridForward = MapOfficeWorldToQaGround(
                basisSource + starter.World.Presenter.CellBasisYWorld(),
                sourceOfficeCamera) - basisQa;
            gridRight.y = gridForward.y = 0f;

            Vector3[] sourceCorners = starter.World.Presenter.FootprintCornersWorld(desk);
            if (sourceCorners == null || sourceCorners.Length != 4)
                throw new InvalidOperationException(
                    "Workstation semantic footprint must contain four corners.");
            var qaCorners = new Vector3[4];
            Vector3 deskFootprintCenter = Vector3.zero;
            for (var index = 0; index < qaCorners.Length; index++)
            {
                qaCorners[index] = MapOfficeWorldToQaGround(
                    sourceCorners[index],
                    sourceOfficeCamera);
                deskFootprintCenter += qaCorners[index];
            }
            deskFootprintCenter *= 0.25f;
            float deskFootprintWidth = Vector3.Distance(qaCorners[0], qaCorners[1]);
            float deskFootprintDepth = Vector3.Distance(qaCorners[0], qaCorners[3]);

            // The semantic footprint corners remain expressed in the map's global X/Y bases.
            // A rotated workstation cannot therefore pass those same unrotated bases into the 3D
            // builder: doing so moves the footprint around the seat while every CRT and seated
            // Father still faces the original direction. Rotate the complete local frame with the
            // same (dx,dy)->(dy,-dx) rule as OfficeLayoutEditRules.RotateCellClockwise and swap the
            // measured extents on odd turns. Desk, monitor, keyboard, chair and actor then remain
            // one rigid quarter-turned set on the authoritative tiles.
            Vector3 workstationRight = gridRight;
            Vector3 workstationForward = gridForward;
            int workstationTurns =
                ((int)desk.Facing - (int)OfficeFurnitureFacing.SouthEast + 4) & 3;
            for (var turn = 0; turn < workstationTurns; turn++)
            {
                Vector3 previousRight = workstationRight;
                workstationRight = -workstationForward;
                workstationForward = previousRight;
                float previousWidth = deskFootprintWidth;
                deskFootprintWidth = deskFootprintDepth;
                deskFootprintDepth = previousWidth;
            }

            Vector3 deskSeatSource = starter.World.Workstations.DeskSeatSocketWorld(seat);
            Vector3 chairSeatSource = starter.World.Workstations.ChairSeatAnchorWorld(seat);
            Vector3 workSource = starter.World.Workstations.DeskWorkSocketWorld(seat);
            Vector3 seatGround = MapOfficeWorldToQaGround(
                deskSeatSource,
                sourceOfficeCamera);
            Vector3 chairSeatGround = MapOfficeWorldToQaGround(
                chairSeatSource,
                sourceOfficeCamera);
            Vector3 keyboardGround = MapOfficeWorldToQaGround(workSource, sourceOfficeCamera);

            if (captureFatherReceipt)
            {
                fatherDeskLegacyChairAnchorOffsetWorld = Vector3.Distance(
                    seatGround,
                    chairSeatGround);
                fatherDeskFootprintOrigin = new Vector2Int(desk.Origin.X, desk.Origin.Y);
                fatherDeskFootprintSize = new Vector2Int(desk.Width, desk.Height);
                fatherDeskBlockedCells = new string[desk.Width * desk.Height];
                fatherDeskBlockedCellsNonWalkable = desk.BlocksMovement;
                var blockedIndex = 0;
                for (var y = desk.Origin.Y; y < desk.Origin.Y + desk.Height; y++)
                for (var x = desk.Origin.X; x < desk.Origin.X + desk.Width; x++)
                {
                    var cell = new OfficeGridCoordinate(x, y);
                    fatherDeskBlockedCells[blockedIndex++] = x + ":" + y;
                    fatherDeskBlockedCellsNonWalkable &= !starter.World.Grid.IsWalkable(cell);
                }
            }

            Family3DWorkstationQa workstation = Family3DWorkstationQa.Create(
                transform,
                qaLayer,
                seat.SeatId,
                seatGround,
                workstationRight,
                workstationForward,
                deskFootprintCenter,
                deskFootprintWidth,
                deskFootprintDepth,
                keyboardGround,
                fatherBinding.WalkActor.StandingHeight,
                fatherMotionFacingOffsetDegrees,
                0f);
            if (captureFatherReceipt)
            {
                fatherDeskSeat = seat;
                fatherDeskWorkstation = workstation;
                fatherDeskResolvedChairActorSocketErrorWorld = Vector3.Distance(
                    workstation.ChairGroundWorld,
                    workstation.SeatGroundWorld);
            }
            return workstation;
        }

        private void RefreshV27SourceFurnitureMask()
        {
            for (var index = 0; index < v27WorkstationSeats.Count; index++)
            {
                OfficeSeatSlot seat = v27WorkstationSeats[index];
                HideSourceFurniture(seat.ChairFurnitureId);
                HideSourceFurniture(seat.WorkSurfaceFurnitureId);
            }
        }

        private int CountVisibleLegacyWorkstationRenderers()
        {
            var counted = new HashSet<Renderer>();
            var presenter = starter.World.FurniturePresenter;
            for (var index = 0; index < v27WorkstationSeats.Count; index++)
            {
                OfficeSeatSlot seat = v27WorkstationSeats[index];
                AddRendererIfPresent(
                    presenter.TryGetRenderer(
                        seat.WorkSurfaceFurnitureId,
                        out SpriteRenderer deskBase) ? deskBase : null,
                    counted);
                AddRendererIfPresent(
                    presenter.TryGetRenderer(
                        seat.ChairFurnitureId,
                        out SpriteRenderer chairBase) ? chairBase : null,
                    counted);
                AddRendererIfPresent(
                    presenter.FrontOverlayRenderers.TryGetValue(
                        seat.WorkSurfaceFurnitureId,
                        out SpriteRenderer deskFront) ? deskFront : null,
                    counted);
                AddRendererIfPresent(
                    presenter.FrontOverlayRenderers.TryGetValue(
                        seat.ChairFurnitureId,
                        out SpriteRenderer chairFront) ? chairFront : null,
                    counted);
                AddRendererIfPresent(
                    presenter.OccupiedChairLowerBodyRenderers.TryGetValue(
                        seat.ChairFurnitureId,
                        out SpriteRenderer chairLower) ? chairLower : null,
                    counted);
            }

            var visible = 0;
            foreach (Renderer renderer in counted)
                if (renderer != null && renderer.enabled &&
                    !renderer.forceRenderingOff && renderer.gameObject.activeInHierarchy)
                    visible++;
            return visible;
        }

        private static void AddRendererIfPresent(
            Renderer renderer,
            ISet<Renderer> renderers)
        {
            if (renderer != null)
                renderers.Add(renderer);
        }

        private string[] BuildV27ReplacedSeatIds()
        {
            var result = new string[v27WorkstationSeats.Count];
            for (var index = 0; index < result.Length; index++)
                result[index] = v27WorkstationSeats[index].SeatId;
            return result;
        }

        private void HideSourceFurniture(string furnitureId)
        {
            var presenter = starter.World.FurniturePresenter;
            if (presenter.TryGetRenderer(furnitureId, out SpriteRenderer baseRenderer))
                HideSourceFurnitureRenderer(baseRenderer);
            if (presenter.FrontOverlayRenderers.TryGetValue(
                    furnitureId,
                    out SpriteRenderer frontRenderer))
                HideSourceFurnitureRenderer(frontRenderer);
            if (presenter.OccupiedChairLowerBodyRenderers.TryGetValue(
                    furnitureId,
                    out SpriteRenderer lowerBodyRenderer))
                HideSourceFurnitureRenderer(lowerBodyRenderer);
        }

        private void HideSourceFurnitureRenderer(Renderer renderer)
        {
            if (renderer == null)
                return;
            for (var index = 0; index < hiddenSourceFurniture.Count; index++)
                if (hiddenSourceFurniture[index].Renderer == renderer)
                    return;
            var state = new RendererState(renderer);
            state.SetQaHidden(true);
            hiddenSourceFurniture.Add(state);
        }

        private IEnumerator RunFatherMapWalkProof()
        {
            yield return null;
            Binding fatherBinding = bindings.Find(candidate =>
                string.Equals(candidate.FamilyId, "father", StringComparison.Ordinal));
            if (fatherBinding == null || starter == null || starter.World == null)
            {
                Fail("Father natural-walk proof could not resolve the live Father binding/runtime world.");
                Application.Quit(2);
                yield break;
            }

            OfficeRuntimeAgent father = fatherBinding.Agent;
            if (!TryFindClearFatherLoop(father.AgentRadius, out OfficeGridCoordinate[] loop))
            {
                Fail("Father natural-walk proof could not find a clear 3x3 perimeter loop.");
                Application.Quit(2);
                yield break;
            }

            ParkOtherActorsForFatherLoop(father, loop);
            father.QaTeleportToCell(loop[0]);
            father.QaSetDirectMovementInput(Vector2.zero);
            Time.timeScale = 1f;
            yield return null;
            yield return new WaitForEndOfFrame();

            fatherProofRouteActive = true;
            Debug.Log(
                "FAMILY_3D_FATHER_MAP_MOVE_QA: starting two continuous circuits on one " +
                "actual Starter Office map; staticRootMotionOnly=" +
                fatherStaticRootMotionOnly + " higgsfieldIdleRun=" +
                fatherHiggsfieldIdleRun + " cleanBipedNaturalWalk=" +
                fatherCleanBipedNaturalWalk + " cleanBipedCasualWalk=" +
                FatherUsesCleanBipedCasualWalk + " stableBodySideArms=" +
                FatherUsesStableBodySideArmWalk + " productionEligible=false.",
                this);

            for (var circuit = 0; circuit < 2; circuit++)
            {
                fatherProofRouteCircuit = circuit;
                for (var leg = 0; leg < loop.Length - 1; leg++)
                {
                    fatherProofRouteLeg = leg;
                    OfficeGridCoordinate target = loop[leg + 1];
                    if (!father.QaMoveToCell(
                            target,
                            (fatherStaticRootMotionOnly
                                ? "father-v18-higgsfield-static-map-walk"
                                : FatherUsesNative613Package
                                    ? "father-v18-native-613-map-walk"
                                : FatherUsesStableBodySideArmWalk
                                    ? "father-v18-clean-biped-stable-arm-walk-map"
                                : FatherUsesCleanBipedCasualWalk
                                    ? "father-v18-clean-biped-casual-walk-map"
                                : fatherCleanBipedNaturalWalk
                                    ? "father-v18-clean-biped-natural-map-walk"
                                : fatherHiggsfieldIdleRun
                                    ? "father-v18-higgsfield-idle-run-map-walk"
                                : "father-stylized-sd-map-walk-v17") +
                            "-c" + circuit + "-leg" + leg))
                    {
                        Fail("Father natural-walk proof route was rejected at circuit " +
                             circuit + ", leg " + leg + ".");
                        Application.Quit(2);
                        yield break;
                    }

                    float deadline = Time.realtimeSinceStartup + 12f;
                    while (!father.QaReachedCell(target) &&
                           Time.realtimeSinceStartup < deadline)
                        yield return null;
                    if (!father.QaReachedCell(target))
                    {
                        Fail("Father natural-walk proof timed out at circuit " +
                             circuit + ", leg " + leg + ".");
                        Application.Quit(2);
                        yield break;
                    }
                }
            }

            fatherProofRouteActive = false;
            fatherProofRouteCompleted = true;
            fatherProofRouteCircuit = 2;
            fatherProofRouteLeg = -1;
            WriteRuntimeReceipt(
                fatherStaticRootMotionOnly
                    ? "FATHER_V18_STATIC_MAP_MOVE_PROOF_COMPLETE"
                    : FatherUsesNative613Package
                        ? "FATHER_V18_NATIVE_613_WALK_MAP_PROOF_COMPLETE"
                    : FatherUsesStableBodySideArmWalk
                        ? "FATHER_V18_CLEAN_BIPED_STABLE_ARM_WALK_MAP_PROOF_COMPLETE"
                    : FatherUsesCleanBipedCasualWalk
                        ? "FATHER_V18_CLEAN_BIPED_CLAUDE_WALK_MAP_PROOF_COMPLETE"
                    : fatherCleanBipedNaturalWalk
                        ? "FATHER_V18_CLEAN_BIPED_NATURAL_MAP_PROOF_COMPLETE"
                    : fatherHiggsfieldIdleRun
                        ? "FATHER_V18_HIGGSFIELD_IDLE_RUN_MAP_PROOF_COMPLETE"
                    : "FATHER_NATURAL_MAP_WALK_PROOF_COMPLETE");
            Debug.Log(
                "FAMILY_3D_FATHER_MAP_MOVE_QA: COMPLETE | circuits=2 captures=" +
                compositeCapturedFrames + " productionEligible=false",
                this);
            yield return new WaitForEndOfFrame();
            Application.Quit(0);
        }

        private bool TryFindClearFatherLoop(
            float radius,
            out OfficeGridCoordinate[] loop)
        {
            int[,] offsets =
            {
                { 0, 0 }, { 1, 0 }, { 2, 0 }, { 2, 1 }, { 2, 2 },
                { 1, 2 }, { 0, 2 }, { 0, 1 }, { 0, 0 }
            };
            for (var y = 1; y < starter.World.Grid.Height - 3; y++)
            {
                for (var x = 1; x < starter.World.Grid.Width - 3; x++)
                {
                    var candidate = new OfficeGridCoordinate[offsets.GetLength(0)];
                    bool valid = true;
                    for (var index = 0; index < candidate.Length; index++)
                    {
                        candidate[index] = new OfficeGridCoordinate(
                            x + offsets[index, 0],
                            y + offsets[index, 1]);
                        if (!starter.World.Grid.IsWalkable(candidate[index]))
                        {
                            valid = false;
                            break;
                        }
                    }
                    if (!valid)
                        continue;
                    for (var index = 0; index < candidate.Length - 1; index++)
                    {
                        Vector2 from = starter.World.Presenter.CellCenterWorld(candidate[index]);
                        Vector2 to = starter.World.Presenter.CellCenterWorld(candidate[index + 1]);
                        if (starter.World.Occupancy.CanTraverseStatic(from, to, radius, string.Empty))
                            continue;
                        valid = false;
                        break;
                    }
                    if (valid)
                    {
                        loop = candidate;
                        return true;
                    }
                }
            }
            loop = Array.Empty<OfficeGridCoordinate>();
            return false;
        }

        private void ParkOtherActorsForFatherLoop(
            OfficeRuntimeAgent father,
            IReadOnlyCollection<OfficeGridCoordinate> loop)
        {
            var occupied = new HashSet<OfficeGridCoordinate>(loop);
            var parking = new List<OfficeGridCoordinate>();
            for (var y = 1; y < starter.World.Grid.Height - 1; y++)
            for (var x = 1; x < starter.World.Grid.Width - 1; x++)
            {
                var cell = new OfficeGridCoordinate(x, y);
                if (!occupied.Contains(cell) && starter.World.Grid.IsWalkable(cell))
                    parking.Add(cell);
            }

            int parkingIndex = parking.Count - 1;
            foreach (OfficeRuntimeAgent actor in starter.Actors)
            {
                if (actor == null || actor == father)
                    continue;
                if (parkingIndex < 0)
                    throw new InvalidOperationException(
                        "Father natural-walk proof has no parking cell for " + actor.AgentId + ".");
                actor.QaTeleportToCell(parking[parkingIndex--]);
                actor.QaSetDirectMovementInput(Vector2.zero);
            }
        }

        private void BindAll()
        {
            ValidateConfiguration();
            ReleaseBindings();

            var liveActors = new Dictionary<string, OfficeRuntimeAgent>(StringComparer.Ordinal);
            foreach (OfficeRuntimeAgent actor in starter.Actors)
            {
                if (actor == null || string.IsNullOrWhiteSpace(actor.AgentId))
                    throw new InvalidOperationException("Starter Office exposed a null or unnamed actor.");
                if (!liveActors.TryAdd(actor.AgentId, actor))
                    throw new InvalidOperationException("Duplicate Starter Office actor id: " + actor.AgentId);
            }

            CandidateDefinition[] definitions = CandidateDefinitions();
            if (liveActors.Count != RequiredActorCount)
                throw new InvalidOperationException(
                    $"Expected {RequiredActorCount} live Starter Office actors; found {liveActors.Count}.");

            Camera sourceOfficeCamera = Camera.main;
            ExcludeQaLayerFromSourceCamera(sourceOfficeCamera);
            for (var index = 0; index < definitions.Length; index++)
            {
                CandidateDefinition definition = definitions[index];
                if (!liveActors.TryGetValue(definition.FamilyId, out OfficeRuntimeAgent actor))
                    throw new InvalidOperationException(
                        "Starter Office actor is missing required family id: " + definition.FamilyId);
                bindings.Add(CreateBinding(definition, actor, sourceOfficeCamera));
            }

            IsBound = true;
            FailureReason = string.Empty;
            Debug.Log(
                $"FAMILY_3D_STARTER_OFFICE_QA: BOUND {bindings.Count} candidates; " +
                "productionMutation=false productionEligible=false", this);
        }

        private Binding CreateBinding(
            CandidateDefinition definition,
            OfficeRuntimeAgent actor,
            Camera sourceOfficeCamera)
        {
            SpriteRenderer sourceRenderer = actor.PresentationRenderer;
            if (sourceRenderer == null || sourceRenderer.sprite == null)
                throw new InvalidOperationException(
                    definition.FamilyId + " has no live SpriteRenderer/sprite for bounds-based scaling.");

            Vector3 qaPosition = MapOfficeActorToQaGround(actor, sourceOfficeCamera);
            Quaternion qaRotation = MapOfficeDirectionToUnityYaw(actor.CurrentDirection);
            var host = new GameObject("Family3DStarterOfficeQa_" + definition.FamilyId);
            host.SetActive(false);
            host.transform.SetParent(transform, false);
            host.transform.SetPositionAndRotation(qaPosition, qaRotation);
            SetLayerRecursively(host, qaLayer);

            GameObject model = Instantiate(definition.Prefab, host.transform, false);
            model.name = definition.FamilyId + "_CandidateModel";
            SetLayerRecursively(model, qaLayer);

            if (fatherStaticRootMotionOnly &&
                string.Equals(definition.FamilyId, "father", StringComparison.Ordinal))
                return CreateStaticFatherBinding(
                    definition,
                    actor,
                    sourceOfficeCamera,
                    sourceRenderer,
                    host,
                    model);

            SkinnedMeshRenderer[] skinned = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (skinned.Length != 1)
            {
                DestroyQaObject(host);
                throw new InvalidOperationException(
                    definition.FamilyId + " expected one complete SkinnedMeshRenderer; found " +
                    skinned.Length + ".");
            }

            Animator animator = model.GetComponent<Animator>();
            if (animator == null)
                animator = model.GetComponentInChildren<Animator>(true);
            if (animator == null || animator.avatar == null ||
                !animator.avatar.isValid || !animator.avatar.isHuman)
            {
                DestroyQaObject(host);
                throw new InvalidOperationException(
                    definition.FamilyId + " candidate has no valid Humanoid Animator/Avatar.");
            }

            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            skinned[0].updateWhenOffscreen = true;

            if ((fatherHiggsfieldIdleRun || fatherCleanBipedNaturalWalk) &&
                string.Equals(definition.FamilyId, "father", StringComparison.Ordinal))
                ApplyFatherV18HiggsfieldMaterial(skinned);

            Bounds candidateBounds = EncapsulateBounds(skinned);
            float candidateHeight = Mathf.Max(candidateBounds.size.y, 0.0001f);
            float spriteViewportHeight = MeasureSpriteViewportHeight(sourceRenderer, sourceOfficeCamera);
            float targetHeight = ResolveQaHeightForViewport(spriteViewportHeight, qaPosition);
            if (targetHeight <= 0.0001f)
            {
                targetHeight = Mathf.Max(
                    sourceRenderer.bounds.size.y * fallbackOfficeWorldToQaScale,
                    0.0001f);
            }

            bool useFatherNaturalSdWalk =
                definition.Prefab == fatherCandidate && !fatherHiggsfieldIdleRun;
            if (useFatherNaturalSdWalk && !fatherCleanBipedNaturalWalk)
                targetHeight *= 0.55f;

            float appliedScale = targetHeight / candidateHeight;
            model.transform.localScale *= appliedScale;
            candidateBounds = EncapsulateBounds(skinned);
            model.transform.position += Vector3.up * (groundY - candidateBounds.min.y);

            var walkActor = host.AddComponent<Family3DWalkActor>();
            float poseStrength = fatherHiggsfieldIdleRun
                ? ResolveFatherMotionPoseStrength()
                : 1f;
            walkActor.Configure(
                definition.FamilyId,
                model.transform,
                animator,
                sharedHumanoidWalkClip,
                qaPosition,
                Color.white,
                poseStrength,
                useFatherNaturalSdWalk,
                fatherHiggsfieldIdleRun ? fatherHiggsfieldIdleClip : null,
                fatherHiggsfieldIdleRun && ResolveClipMuscleDeltaRetarget(fatherClipMuscleDeltaRetargetAsset),
                fatherHiggsfieldIdleRun && fatherClipAnatomicalSanitizationAsset,
                fatherHiggsfieldIdleRun && fatherClipStableBodySideArmsAsset,
                fatherHiggsfieldIdleRun ? fatherMotionCycleSecondsAsset : 0f);
            if (fatherCleanBipedNaturalWalk)
            {
                walkActor.ConfigureNaturalSdStyle(
                    ResolveCommandLineFloat(
                        "-family3d-father-clean-biped-torso-upright-degrees",
                        0f,
                        0f,
                        10f,
                        "Father clean-biped torso upright"),
                    ResolveCommandLineFloat(
                        "-family3d-father-clean-biped-arm-outward-degrees",
                        1f,
                        0f,
                        12f,
                        "Father clean-biped arm outward"),
                    ResolveCommandLineFloat(
                        "-family3d-father-clean-biped-arm-swing-degrees",
                        8f,
                        0f,
                        18f,
                        "Father clean-biped arm swing"),
                    ResolveCommandLineFloat(
                        "-family3d-father-clean-biped-elbow-bend-degrees",
                        12f,
                        0f,
                        24f,
                        "Father clean-biped elbow bend"));
            }

            var binding = new Binding(
                definition.FamilyId,
                actor,
                host,
                model,
                walkActor,
                model.GetComponentsInChildren<Renderer>(true),
                sourceRenderer,
                sourceRenderer.bounds.size.y,
                spriteViewportHeight,
                targetHeight,
                appliedScale,
                poseStrength);

            host.SetActive(true);
            if (fatherHiggsfieldIdleRun || fatherCleanBipedNaturalWalk)
            {
                ApplyExactStaticMapScale(binding, sourceOfficeCamera, true);
                walkActor.RebaseVisualRootAfterScale();
            }
            return binding;
        }

        private void ApplyFatherV18HiggsfieldMaterial(Renderer[] renderers)
        {
            if (fatherStaticAlbedo == null)
                throw new InvalidOperationException("Father V18 motion albedo is missing.");
            // The user's accepted reference is the static FBX as rendered on this same map. Copy
            // that exact surface material; replacing it with Unlit/Texture made V61/V62 dark and
            // visibly changed the hair, shirt, trousers, hands and shoes.
            if (fatherExactAlbedoMaterial == null || fatherExactAlbedoMaterial.shader == null)
                throw new InvalidOperationException(
                    "Father V18 static-surface material is missing from the QA scene.");
            for (var index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                var material = new Material(fatherExactAlbedoMaterial)
                {
                    name = "FatherV18HiggsfieldMotion_StaticSurface_QaRuntimeMaterial"
                };
                material.mainTexture = fatherStaticAlbedo;
                material.color = Color.white;
                renderer.sharedMaterial = material;
            }
        }

        private Binding CreateStaticFatherBinding(
            CandidateDefinition definition,
            OfficeRuntimeAgent actor,
            Camera sourceOfficeCamera,
            SpriteRenderer sourceRenderer,
            GameObject host,
            GameObject model)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            int meshRendererCount = model.GetComponentsInChildren<MeshRenderer>(true).Length;
            int skinnedRendererCount = model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length;
            if (renderers.Length != 1 || meshRendererCount != 1 || skinnedRendererCount != 0)
            {
                DestroyQaObject(host);
                throw new InvalidOperationException(
                    definition.FamilyId +
                    " static candidate must contain exactly one MeshRenderer and no " +
                    "SkinnedMeshRenderer; renderers=" + renderers.Length +
                    " mesh=" + meshRendererCount + " skinned=" + skinnedRendererCount + ".");
            }
            if (fatherStaticAlbedo == null)
            {
                DestroyQaObject(host);
                throw new InvalidOperationException("Father V18 static albedo is missing.");
            }

            Material sourceMaterial = renderers[0].sharedMaterial;
            Shader shader = sourceMaterial == null ? Shader.Find("Standard") : sourceMaterial.shader;
            if (shader == null)
            {
                DestroyQaObject(host);
                throw new InvalidOperationException("Father V18 static material shader is missing.");
            }
            var qaMaterial = sourceMaterial == null
                ? new Material(shader)
                : new Material(sourceMaterial);
            qaMaterial.name = "FatherV18HiggsfieldStatic_QaRuntimeMaterial";
            qaMaterial.mainTexture = fatherStaticAlbedo;
            qaMaterial.color = Color.white;
            renderers[0].sharedMaterial = qaMaterial;

            float sourceViewportHeight = MeasureBoundsViewportHeight(
                sourceRenderer.bounds,
                sourceOfficeCamera);
            float candidateViewportHeight = MeasureBoundsViewportHeight(
                EncapsulateBounds(renderers),
                qaOverlayCamera);
            if (sourceViewportHeight <= 0.000001f || candidateViewportHeight <= 0.000001f)
            {
                DestroyQaObject(host);
                throw new InvalidOperationException(
                    "Father V18 static map-scale projection could not be measured.");
            }

            float appliedScale = sourceViewportHeight / candidateViewportHeight;
            model.transform.localScale *= appliedScale;
            Bounds scaledBounds = EncapsulateBounds(renderers);
            model.transform.position += Vector3.up * (groundY - scaledBounds.min.y);

            var binding = new Binding(
                definition.FamilyId,
                actor,
                host,
                model,
                null,
                renderers,
                sourceRenderer,
                sourceRenderer.bounds.size.y,
                sourceViewportHeight,
                scaledBounds.size.y,
                appliedScale,
                0f,
                true);

            host.SetActive(true);
            ApplyExactStaticMapScale(binding, sourceOfficeCamera, true);
            return binding;
        }

        private void UpdateBinding(Binding binding, Camera sourceOfficeCamera)
        {
            binding.EnsureSeatedProtectionSnapshot();
            if (fatherMapWalkQa &&
                !string.Equals(binding.FamilyId, "father", StringComparison.Ordinal))
            {
                binding.SetSource2DHidden(false);
                binding.SetCandidateVisible(false);
                binding.IsMoving = false;
                return;
            }
            if (fatherDeskWorkQa &&
                fatherDeskWorkstation != null &&
                string.Equals(binding.FamilyId, "father", StringComparison.Ordinal))
            {
                UpdateFatherDeskWorkBinding(binding, sourceOfficeCamera);
                return;
            }
            bool supported = IsStandingOrWalkingPhase(binding.Agent.Phase);
            if (!supported)
            {
                if (binding.WasSupportedLastFrame)
                    binding.UnsupportedPhaseFallbackCount++;
                binding.WasSupportedLastFrame = false;
                binding.SetSource2DHidden(false);
                binding.SetCandidateVisible(false);
                return;
            }

            binding.WasSupportedLastFrame = true;
            binding.SetSource2DHidden(true);
            binding.SetCandidateVisible(true);

            Vector3 worldPosition = MapOfficeActorToQaGround(binding.Agent, sourceOfficeCamera);
            Quaternion worldRotation = ResolveBlendedYaw(binding, worldPosition);
            float gaitPhase01 = Mathf.Repeat(binding.Agent.GaitPhase01, 1f);
            bool isMoving =
                binding.Agent.LastActualDisplacement.sqrMagnitude > MovementEpsilonSqr;
            if (binding.StaticRootMotionOnly)
            {
                binding.Host.transform.SetPositionAndRotation(worldPosition, worldRotation);
                ApplyExactStaticMapScale(binding, sourceOfficeCamera, false);
            }
            else
            {
                // The office gait phase advances with distance, not time: OfficeLocomotionGaitRules
                // completes one cycle per DefaultStrideLength of travel, and DirectionalSpriteAnimator
                // rejects any other stride, so the 2D cadence is locked project-wide. A 3D clip whose
                // own stride differs therefore cannot be matched by retiming; it has to be driven by
                // the same distance against its own stride. GaitDistance is monotonic, so unlike
                // gaitPhase01 it carries no wrap for a non-integer stride ratio to break on.
                double clipCycles =
                    (fatherHiggsfieldIdleRun || fatherCleanBipedNaturalWalk) &&
                    fatherMotionStrideOfficeUnits > 0f
                    ? binding.Agent.GaitDistance / fatherMotionStrideOfficeUnits
                    : gaitPhase01;
                double motionClock =
                    (clipCycles - binding.WalkActor.PhaseOffset) *
                    binding.WalkActor.CycleSeconds;
                binding.WalkActor.Tick(motionClock, worldPosition, worldRotation, isMoving);
            }
            binding.LastObservedDisplacement = binding.Agent.LastActualDisplacement;
            binding.LastObservedGaitPhase01 = gaitPhase01;
            binding.LastObservedDirection = binding.Agent.CurrentDirection;
            binding.IsMoving = isMoving;
            if (binding.IsMoving)
            {
                binding.MovingFrameCount++;
                int direction = (binding.LastObservedDirection % 8 + 8) % 8;
                binding.ObservedDirectionMask |= 1 << direction;
                binding.MinimumObservedGaitPhase01 = Mathf.Min(
                    binding.MinimumObservedGaitPhase01,
                    gaitPhase01);
                binding.MaximumObservedGaitPhase01 = Mathf.Max(
                    binding.MaximumObservedGaitPhase01,
                    gaitPhase01);
            }
        }

        private void UpdateFatherDeskWorkBinding(Binding binding, Camera sourceOfficeCamera)
        {
            // The occupied-chair presenter creates its lower-body foreground renderer only after
            // the seat is claimed. Hiding furniture once during setup therefore misses that late
            // renderer and leaves a green chair crop beside the 3D chair. Refresh the QA-only
            // renderer mask every frame; semantic furniture, occupancy and blocking stay intact.
            RefreshV27SourceFurnitureMask();
            v27VisibleLegacyWorkstationRendererCount =
                CountVisibleLegacyWorkstationRenderers();
            if (binding.Agent.Phase == OfficeRuntimeAgentPhase.Outside)
            {
                binding.SetSource2DHidden(false);
                binding.SetCandidateVisible(false);
                binding.IsMoving = false;
                return;
            }
            binding.WasSupportedLastFrame = true;
            binding.SetSource2DHidden(true);
            binding.SetCandidateVisible(true);

            OfficeRuntimeAgentPhase phase = binding.Agent.Phase;
            string phaseName = phase.ToString();
            if (!string.Equals(fatherDeskLastPhase, phaseName, StringComparison.Ordinal))
            {
                fatherDeskLastPhase = phaseName;
                if (!fatherDeskObservedPhases.Contains(phaseName))
                    fatherDeskObservedPhases.Add(phaseName);
            }

            Vector3 actorGround = MapOfficeActorToQaGround(binding.Agent, sourceOfficeCamera);
            bool isMoving =
                binding.Agent.LastActualDisplacement.sqrMagnitude > MovementEpsilonSqr;
            bool isSeatFacingPhase =
                phase == OfficeRuntimeAgentPhase.AligningSeat ||
                phase == OfficeRuntimeAgentPhase.RotatingToSeat ||
                phase == OfficeRuntimeAgentPhase.SittingDown ||
                phase == OfficeRuntimeAgentPhase.Working ||
                phase == OfficeRuntimeAgentPhase.FinishingWork ||
                phase == OfficeRuntimeAgentPhase.StandingUp;
            Quaternion rotation = isSeatFacingPhase
                ? fatherDeskWorkstation.SeatedRotationWorld
                : ResolveBlendedYaw(binding, actorGround);

            bool wantsSeatedPose =
                phase == OfficeRuntimeAgentPhase.SittingDown ||
                phase == OfficeRuntimeAgentPhase.Working ||
                phase == OfficeRuntimeAgentPhase.FinishingWork;
            float blendRate = Time.captureDeltaTime > 0f
                ? Time.captureDeltaTime / 0.42f
                : Time.unscaledDeltaTime / 0.42f;
            fatherDeskSeatedBlend01 = Mathf.MoveTowards(
                fatherDeskSeatedBlend01,
                wantsSeatedPose ? 1f : 0f,
                blendRate);

            if (phase == OfficeRuntimeAgentPhase.Working)
            {
                fatherDeskWorkFrames++;
                fatherDeskWorkClockSeconds += Time.captureDeltaTime > 0f
                    ? Time.captureDeltaTime
                    : Time.unscaledDeltaTime;
            }

            if (fatherDeskSeatedBlend01 > 0.0001f || isSeatFacingPhase)
            {
                Vector3 seatGround = fatherDeskWorkstation.SeatGroundWorld;
                float positionBlend = Mathf.SmoothStep(0f, 1f, fatherDeskSeatedBlend01);
                Vector3 rootPosition = Vector3.Lerp(actorGround, seatGround, positionBlend);
                rootPosition.y = groundY;
                binding.WalkActor.TickSeatedDeskWork(
                    fatherDeskWorkClockSeconds,
                    rootPosition,
                    rotation,
                    fatherDeskSeatedBlend01,
                    phase == OfficeRuntimeAgentPhase.Working);
                Family3DWalkActor.PoseSnapshot seatedPose =
                    binding.WalkActor.ReadPoseSnapshot();
                float seatedRootY =
                    fatherDeskWorkstation.CushionWorldY +
                    0.113f * seatedPose.standingHeight -
                    seatedPose.hipsLocal.y;
                rootPosition.y = Mathf.Lerp(groundY, seatedRootY, positionBlend);
                // Keep the semantic agent/seat claim at the chair pivot, but place the visible
                // pelvis on the front half of the cushion. The vertical 0.113h is the measured
                // hips-joint-to-skin contact thickness; without it the cushion cuts through the
                // lower torso. The semantic occupancy anchor remains unchanged.
                rootPosition += fatherDeskWorkstation.SeatedBodyForwardWorld *
                                (0.07f * seatedPose.standingHeight * positionBlend);
                binding.Host.transform.position = rootPosition;
                binding.WalkActor.AlignSeatedDeskLimbs(
                    fatherDeskWorkstation.KeyboardWorld,
                    fatherDeskWorkstation.SeatedBodyForwardWorld,
                    groundY,
                    fatherDeskSeatedBlend01,
                    fatherDeskWorkClockSeconds,
                    phase == OfficeRuntimeAgentPhase.Working);
            }
            else
            {
                float gaitPhase01 = Mathf.Repeat(binding.Agent.GaitPhase01, 1f);
                double clipCycles = fatherMotionStrideOfficeUnits > 0f
                    ? binding.Agent.GaitDistance / fatherMotionStrideOfficeUnits
                    : gaitPhase01;
                double motionClock =
                    (clipCycles - binding.WalkActor.PhaseOffset) *
                    binding.WalkActor.CycleSeconds;
                binding.WalkActor.Tick(motionClock, actorGround, rotation, isMoving);
            }

            float observedGait = Mathf.Repeat(binding.Agent.GaitPhase01, 1f);
            binding.LastObservedDisplacement = binding.Agent.LastActualDisplacement;
            binding.LastObservedGaitPhase01 = observedGait;
            binding.LastObservedDirection = binding.Agent.CurrentDirection;
            binding.IsMoving = isMoving;
            if (isMoving)
            {
                binding.MovingFrameCount++;
                int direction = (binding.LastObservedDirection % 8 + 8) % 8;
                binding.ObservedDirectionMask |= 1 << direction;
                binding.MinimumObservedGaitPhase01 = Mathf.Min(
                    binding.MinimumObservedGaitPhase01,
                    observedGait);
                binding.MaximumObservedGaitPhase01 = Mathf.Max(
                    binding.MaximumObservedGaitPhase01,
                    observedGait);
            }
        }

        private void ApplyExactStaticMapScale(
            Binding binding,
            Camera sourceOfficeCamera,
            bool throwOnMismatch)
        {
            Renderer[] renderers = binding.Model.GetComponentsInChildren<Renderer>(true);
            float targetViewportHeight = MeasureBoundsViewportHeight(
                binding.MainSource.Renderer.bounds,
                sourceOfficeCamera);
            float currentViewportHeight = MeasureBoundsViewportHeight(
                EncapsulateBounds(renderers),
                qaOverlayCamera);
            if (targetViewportHeight <= 0.000001f || currentViewportHeight <= 0.000001f)
            {
                if (throwOnMismatch)
                    throw new InvalidOperationException(
                        "Father V18 static map-scale projection became unmeasurable.");
                return;
            }

            float correction = targetViewportHeight / currentViewportHeight;
            binding.Model.transform.localScale *= correction;
            binding.AppliedModelScale *= correction;
            Bounds bounds = EncapsulateBounds(renderers);
            binding.Model.transform.position += Vector3.up * (groundY - bounds.min.y);
            bounds = EncapsulateBounds(renderers);

            float actualViewportHeight = MeasureBoundsViewportHeight(bounds, qaOverlayCamera);
            float ratio = actualViewportHeight / targetViewportHeight;
            float scaleError = Mathf.Abs(ratio - 1f);
            float groundError = Mathf.Abs(bounds.min.y - groundY);
            binding.LastScaleMatchRatio = ratio;
            binding.MinimumScaleMatchRatio = Mathf.Min(binding.MinimumScaleMatchRatio, ratio);
            binding.MaximumScaleMatchRatio = Mathf.Max(binding.MaximumScaleMatchRatio, ratio);
            binding.MaximumScaleError = Mathf.Max(binding.MaximumScaleError, scaleError);
            binding.MaximumGroundError = Mathf.Max(binding.MaximumGroundError, groundError);
            binding.Target3DHeight = bounds.size.y;

            if (scaleError <= StaticMapScaleTolerance && groundError <= 0.001f)
                return;

            string reason =
                "Father V18 static map-scale gate failed: targetViewport=" +
                targetViewportHeight.ToString("F6", CultureInfo.InvariantCulture) +
                " actualViewport=" +
                actualViewportHeight.ToString("F6", CultureInfo.InvariantCulture) +
                " ratio=" + ratio.ToString("F6", CultureInfo.InvariantCulture) +
                " groundError=" + groundError.ToString("F6", CultureInfo.InvariantCulture) + ".";
            if (throwOnMismatch)
                throw new InvalidOperationException(reason);
            Fail(reason);
            Application.Quit(2);
        }

        private void CaptureCompositeQaFrame(
            Camera sourceOfficeCamera,
            string filePrefix = "office-moving",
            Binding captureBinding = null)
        {
            if (sourceOfficeCamera == null || qaOverlayCamera == null)
                return;
            int width = fatherCompositeCaptureWidth;
            int height = fatherCompositeCaptureHeight;
            RenderTexture target = RenderTexture.GetTemporary(
                width,
                height,
                24,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Default);
            var readback = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousSourceTarget = sourceOfficeCamera.targetTexture;
            RenderTexture previousOverlayTarget = qaOverlayCamera.targetTexture;
            try
            {
                sourceOfficeCamera.targetTexture = target;
                qaOverlayCamera.targetTexture = target;
                sourceOfficeCamera.Render();
                qaOverlayCamera.Render();
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                readback.Apply(false, false);
                int lumaRange = AuditLumaRange(readback);
                minimumCompositeLumaRange = Mathf.Min(minimumCompositeLumaRange, lumaRange);
                maximumCompositeLumaRange = Mathf.Max(maximumCompositeLumaRange, lumaRange);
                string root = ResolveRuntimeReceiptRoot();
                string frames = Path.Combine(root, "frames");
                Directory.CreateDirectory(frames);
                string path = Path.Combine(
                    frames,
                    filePrefix + "-" + compositeCapturedFrames.ToString("D2") + ".png");
                File.WriteAllBytes(path, ImageConversion.EncodeToPNG(readback));
                if (captureBinding != null)
                    RecordFatherCaptureSample(captureBinding, compositeCapturedFrames);
                compositeCapturedFrames++;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "FAMILY_3D_STARTER_OFFICE_QA: composite capture failed | " +
                    exception.Message,
                    this);
            }
            finally
            {
                sourceOfficeCamera.targetTexture = previousSourceTarget;
                qaOverlayCamera.targetTexture = previousOverlayTarget;
                RenderTexture.active = previousActive;
                Destroy(readback);
                RenderTexture.ReleaseTemporary(target);
            }
        }

        private void RecordFatherCaptureSample(Binding binding, int frameIndex)
        {
            Family3DWalkActor.PoseSnapshot pose = binding.WalkActor == null
                ? default
                : binding.WalkActor.ReadPoseSnapshot();
            fatherCaptureSamples.Add(new FatherCaptureSample
            {
                frameIndex = frameIndex,
                // realtimeSeconds keeps the wall clock for diagnosing encode cost. Rate maths must
                // use simulationSeconds: once captureDeltaTime is pinned the two deliberately
                // diverge, and the wall clock is the one that stretches under a slow PNG write.
                realtimeSeconds = Time.realtimeSinceStartup,
                simulationSeconds = (float)fatherCaptureSimulationSeconds,
                // The applied yaw, not the office octant. Without it the receipt records only the
                // discrete facing and the blend cannot be measured from the evidence.
                rootWorldYawDegrees = binding.Host == null
                    ? 0f
                    : binding.Host.transform.rotation.eulerAngles.y,
                targetYawDegrees =
                    MapOfficeDirectionToUnityYaw(binding.Agent.CurrentDirection).eulerAngles.y,
                routeCircuit = fatherProofRouteCircuit,
                routeLeg = fatherProofRouteLeg,
                officePosition = binding.Agent.Position,
                rootWorldPosition = binding.Host == null
                    ? Vector3.zero
                    : binding.Host.transform.position,
                gaitPhase01 = binding.LastObservedGaitPhase01,
                direction = binding.LastObservedDirection,
                actualDisplacement = binding.LastObservedDisplacement,
                leftFootLocal = pose.leftFootLocal,
                rightFootLocal = pose.rightFootLocal,
                leftFootWorld = pose.leftFootWorld,
                rightFootWorld = pose.rightFootWorld,
                leftHandLocal = pose.leftHandLocal,
                rightHandLocal = pose.rightHandLocal,
                hipsLocal = pose.hipsLocal,
                toeForwardLocal = pose.toeForwardLocal,
                torsoUpLocal = pose.torsoUpLocal,
                motionPhase01 = pose.motionPhase01,
                leftFootPlanted = pose.leftFootPlanted,
                rightFootPlanted = pose.rightFootPlanted
            });
        }

        private static int AuditLumaRange(Texture2D texture)
        {
            var pixels = texture.GetRawTextureData<Color32>();
            int minimum = 255;
            int maximum = 0;
            const int stride = 997;
            for (var index = 0; index < pixels.Length; index += stride)
            {
                Color32 pixel = pixels[index];
                int luma = (pixel.r * 54 + pixel.g * 183 + pixel.b * 19) >> 8;
                minimum = Mathf.Min(minimum, luma);
                maximum = Mathf.Max(maximum, luma);
            }
            return maximum - minimum;
        }

        private void ExcludeQaLayerFromSourceCamera(Camera sourceOfficeCamera)
        {
            if (sourceOfficeCamera == null || sourceOfficeCamera == qaOverlayCamera)
                return;
            if (!sourceCameraCullingMasks.ContainsKey(sourceOfficeCamera))
                sourceCameraCullingMasks.Add(sourceOfficeCamera, sourceOfficeCamera.cullingMask);
            sourceOfficeCamera.cullingMask &= ~(1 << qaLayer);
        }

        private void RestoreSourceCameraCullingMasks()
        {
            foreach (KeyValuePair<Camera, int> entry in sourceCameraCullingMasks)
            {
                if (entry.Key != null)
                    entry.Key.cullingMask = entry.Value;
            }
            sourceCameraCullingMasks.Clear();
        }

        private Vector3 MapOfficeActorToQaGround(
            OfficeRuntimeAgent actor,
            Camera sourceOfficeCamera)
        {
            Vector2 position = actor.Position;
            return MapOfficeWorldToQaGround(
                new Vector3(position.x, position.y, actor.transform.position.z),
                sourceOfficeCamera);
        }

        private Vector3 MapOfficeWorldToQaGround(
            Vector3 sourceWorld,
            Camera sourceOfficeCamera)
        {
            if (sourceOfficeCamera != null && qaOverlayCamera != null)
            {
                Vector3 viewport = sourceOfficeCamera.WorldToViewportPoint(sourceWorld);
                if (viewport.z > 0f)
                {
                    Ray ray = qaOverlayCamera.ViewportPointToRay(
                        new Vector3(viewport.x, viewport.y, 0f));
                    var qaGround = new Plane(Vector3.up, new Vector3(0f, groundY, 0f));
                    if (qaGround.Raycast(ray, out float distance) && distance >= 0f)
                        return ray.GetPoint(distance);
                }
            }

            return MapOfficeXYToUnityXZ(
                new Vector2(sourceWorld.x, sourceWorld.y),
                fallbackOfficeWorldToQaScale,
                groundY);
        }

        private float MeasureSpriteViewportHeight(
            SpriteRenderer renderer,
            Camera sourceOfficeCamera)
        {
            if (renderer == null || sourceOfficeCamera == null)
                return 0f;
            Bounds bounds = renderer.bounds;
            Vector3 bottom = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            Vector3 top = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
            Vector3 bottomViewport = sourceOfficeCamera.WorldToViewportPoint(bottom);
            Vector3 topViewport = sourceOfficeCamera.WorldToViewportPoint(top);
            if (bottomViewport.z <= 0f || topViewport.z <= 0f)
                return 0f;
            return Mathf.Abs(topViewport.y - bottomViewport.y);
        }

        private static float MeasureBoundsViewportHeight(Bounds bounds, Camera camera)
        {
            if (camera == null)
                return 0f;
            Vector3 minimum = bounds.min;
            Vector3 maximum = bounds.max;
            float minimumY = float.PositiveInfinity;
            float maximumY = float.NegativeInfinity;
            for (var x = 0; x < 2; x++)
            for (var y = 0; y < 2; y++)
            for (var z = 0; z < 2; z++)
            {
                Vector3 corner = new Vector3(
                    x == 0 ? minimum.x : maximum.x,
                    y == 0 ? minimum.y : maximum.y,
                    z == 0 ? minimum.z : maximum.z);
                Vector3 viewport = camera.WorldToViewportPoint(corner);
                if (viewport.z <= 0f)
                    return 0f;
                minimumY = Mathf.Min(minimumY, viewport.y);
                maximumY = Mathf.Max(maximumY, viewport.y);
            }
            return Mathf.Max(0f, maximumY - minimumY);
        }

        private float ResolveQaHeightForViewport(float spriteViewportHeight, Vector3 qaGround)
        {
            if (qaOverlayCamera == null || spriteViewportHeight <= 0.000001f)
                return 0f;
            float bottom = qaOverlayCamera.WorldToViewportPoint(qaGround).y;
            float oneMetre = qaOverlayCamera.WorldToViewportPoint(qaGround + Vector3.up).y;
            float viewportHeightPerMetre = Mathf.Abs(oneMetre - bottom);
            return viewportHeightPerMetre <= 0.000001f
                ? 0f
                : spriteViewportHeight / viewportHeightPerMetre;
        }

        private bool BindingsStillMatchStarter()
        {
            if (starter == null || starter.Actors.Count != RequiredActorCount)
                return false;
            for (var index = 0; index < bindings.Count; index++)
            {
                Binding binding = bindings[index];
                bool found = false;
                foreach (OfficeRuntimeAgent actor in starter.Actors)
                {
                    if (!ReferenceEquals(actor, binding.Agent))
                        continue;
                    found = true;
                    break;
                }
                if (!found)
                    return false;
            }
            return true;
        }

        private void ValidateConfiguration()
        {
            if (starter == null || !starter.IsReady)
                throw new InvalidOperationException("Starter Office must be ready before candidate binding.");
            if (qaOverlayCamera == null)
                throw new InvalidOperationException("QA overlay camera is not configured.");
            if ((fatherStaticRootMotionOnly || fatherHiggsfieldIdleRun ||
                 fatherCleanBipedNaturalWalk) &&
                fatherStaticAlbedo == null)
                throw new InvalidOperationException("Father V18 albedo is missing.");
            if (!fatherStaticRootMotionOnly && !fatherCleanBipedNaturalWalk &&
                (sharedHumanoidWalkClip == null || !sharedHumanoidWalkClip.isHumanMotion))
                throw new InvalidOperationException("Shared Humanoid walk clip is missing or not Humanoid.");
            if (fatherHiggsfieldIdleRun && !FatherUsesNative613Package &&
                (fatherHiggsfieldIdleClip == null || !fatherHiggsfieldIdleClip.isHumanMotion))
                throw new InvalidOperationException("Father V18 Higgsfield idle clip is missing or not Humanoid.");
            CandidateDefinition[] definitions = CandidateDefinitions();
            for (var index = 0; index < definitions.Length; index++)
            {
                if (definitions[index].Prefab == null)
                    throw new InvalidOperationException(
                        definitions[index].FamilyId + " candidate prefab is missing.");
            }
        }

        private CandidateDefinition[] CandidateDefinitions()
        {
            if (fatherStaticRootMotionOnly || fatherHiggsfieldIdleRun ||
                fatherCleanBipedNaturalWalk)
            {
                return new[]
                {
                    new CandidateDefinition("father", fatherCandidate)
                };
            }
            return new[]
            {
                new CandidateDefinition("player", playerCandidate),
                new CandidateDefinition("older_sister", olderSisterCandidate),
                new CandidateDefinition("father", fatherCandidate),
                new CandidateDefinition("mother", motherCandidate)
            };
        }

        private static bool IsStandingOrWalkingPhase(OfficeRuntimeAgentPhase phase)
        {
            return phase == OfficeRuntimeAgentPhase.Idle ||
                   phase == OfficeRuntimeAgentPhase.Navigating;
        }

        private static Bounds EncapsulateBounds(Renderer[] renderers)
        {
            if (renderers == null || renderers.Length == 0)
                throw new InvalidOperationException("Candidate has no renderer bounds.");
            Bounds result = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
                result.Encapsulate(renderers[index].bounds);
            return result;
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform)
                SetLayerRecursively(child.gameObject, layer);
        }

        private static void DestroyQaObject(UnityEngine.Object value)
        {
            if (value == null)
                return;
            if (Application.isPlaying)
                Destroy(value);
            else
                DestroyImmediate(value);
        }

        private void ReleaseBindings()
        {
            for (var index = 0; index < hiddenSourceFurniture.Count; index++)
                hiddenSourceFurniture[index].RestoreForceRenderingOff();
            hiddenSourceFurniture.Clear();
            for (var workstationIndex = 0;
                 workstationIndex < v27Workstations.Count;
                 workstationIndex++)
            {
                Family3DWorkstationQa workstation = v27Workstations[workstationIndex];
                if (workstation == null)
                    continue;
                workstation.gameObject.SetActive(false);
                DestroyQaObject(workstation.gameObject);
            }
            v27Workstations.Clear();
            v27WorkstationSeats.Clear();
            fatherDeskWorkstation = null;
            fatherDeskSeat = null;
            for (var index = 0; index < bindings.Count; index++)
            {
                Binding binding = bindings[index];
                if (binding.Agent != null)
                    binding.Agent.SetExternalDirectionalSeatingPresentation(false);
                binding.RestoreSourceRenderers();
                // Destroy is deferred in a player. Hide the outgoing host immediately so a
                // same-frame runtime/layout rebind cannot render both old and new candidates.
                binding.SetCandidateVisible(false);
                if (binding.Host != null)
                    binding.Host.SetActive(false);
                DestroyQaObject(binding.Host);
            }
            bindings.Clear();
            IsBound = false;
        }

        private void Fail(string reason)
        {
            FailureReason = string.IsNullOrWhiteSpace(reason) ? "unknown" : reason;
            // A later candidate can fail after earlier hosts have already been created. Restore
            // every source renderer and remove those partial hosts before writing the failure.
            ReleaseBindings();
            bindAttemptActive = false;
            Debug.LogError("FAMILY_3D_STARTER_OFFICE_QA: FAIL | " + FailureReason, this);
            WriteRuntimeReceipt("FAILED");
        }

        private void WriteRuntimeReceipt(string status)
        {
            if (!Application.isPlaying)
                return;
            try
            {
                string root = ResolveRuntimeReceiptRoot();
                Directory.CreateDirectory(root);
                var candidates = new RuntimeCandidateReceipt[bindings.Count];
                for (var index = 0; index < bindings.Count; index++)
                {
                    Binding binding = bindings[index];
                    candidates[index] = new RuntimeCandidateReceipt
                    {
                        familyId = binding.FamilyId,
                        sourceSpriteWorldHeight = binding.SourceSpriteWorldHeight,
                        sourceSpriteViewportHeight = binding.SourceSpriteViewportHeight,
                        target3DHeight = binding.Target3DHeight,
                        appliedModelScale = binding.AppliedModelScale,
                        poseStrength = binding.PoseStrength,
                        staticRootMotionOnly = binding.StaticRootMotionOnly,
                        lastScaleMatchRatio = binding.LastScaleMatchRatio,
                        minimumScaleMatchRatio = binding.MinimumScaleMatchRatio,
                        maximumScaleMatchRatio = binding.MaximumScaleMatchRatio,
                        maximumScaleError = binding.MaximumScaleError,
                        maximumGroundError = binding.MaximumGroundError,
                        sourceSortingLayerId = binding.MainSource.SortingLayerId,
                        sourceSortingLayerName = binding.MainSource.SortingLayerName,
                        sourceSortingOrder = binding.MainSource.SortingOrder,
                        sourceWorldZ = binding.MainSource.WorldZ,
                        unsupportedPhaseFallbackCount = binding.UnsupportedPhaseFallbackCount,
                        lastObservedDisplacement = binding.LastObservedDisplacement,
                        lastObservedGaitPhase01 = binding.LastObservedGaitPhase01,
                        lastObservedDirection = binding.LastObservedDirection,
                        movingAtReceipt = binding.IsMoving,
                        movingFrameCount = binding.MovingFrameCount,
                        observedDirectionMask = binding.ObservedDirectionMask,
                        minimumObservedGaitPhase01 = binding.MovingFrameCount > 0
                            ? binding.MinimumObservedGaitPhase01
                            : 0f,
                        maximumObservedGaitPhase01 = binding.MaximumObservedGaitPhase01,
                        qaHostWorldPosition = binding.Host == null
                            ? Vector3.zero
                            : binding.Host.transform.position
                    };
                }

                string json = JsonUtility.ToJson(new RuntimeReceipt
                {
                    contract = Contract,
                    receiptStatus = status,
                    failureReason = FailureReason,
                    productionMutation = false,
                    productionEligible = false,
                    starterReady = starter != null && starter.IsReady,
                    actorCount = bindings.Count,
                    starterActorCount = starter == null ? 0 : starter.Actors.Count,
                    fatherStaticRootMotionOnly = fatherStaticRootMotionOnly,
                    fatherHiggsfieldIdleRun = fatherHiggsfieldIdleRun,
                    fatherCleanBipedNaturalWalk = fatherCleanBipedNaturalWalk,
                    fatherCleanBipedCasualWalk = FatherUsesCleanBipedCasualWalk,
                    fatherStableBodySideArmWalk = FatherUsesStableBodySideArmWalk,
                    fatherNative613Package = FatherUsesNative613Package,
                    coordinateMapping =
                        "Office actor XY -> production Camera.WorldToViewportPoint -> QA " +
                        "Camera.ViewportPointToRay -> Y=ground plane; raw (x,y)->(x,groundY,y) fallback",
                    directionMapping =
                        "measured QA ground displacement -> LookRotation + per-rig measured " +
                        "authored-forward offset; 360 degrees/second corner blend",
                    scalePolicy =
                        fatherStaticRootMotionOnly
                            ? "every frame: live Father SpriteRenderer projected bounds height == " +
                              "Father V18 projected renderer bounds height; tolerance <= 0.5%; grounded"
                            : FatherUsesNative613Package
                                ? "one locked uniform scale from the native action-613 rendered bounds; visible mesh/Avatar/skin/clip share one FBX; static-FBX surface material; no idle cross-retarget, anatomical sanitation, rigid-arm override, or procedural gait"
                            : FatherUsesStableBodySideArmWalk
                                ? "V72 clean V4 lower-body/torso/action-613 contract unchanged; static-FBX surface; only the final rigid-arm tuck is replaced by straight rigid arms with a fixed-axis 6-degree opposite upper-arm swing; no elbow/wrist/finger/outward/tuck correction"
                            : FatherUsesCleanBipedCasualWalk
                                ? "one locked uniform model scale calibrated from idle projected bounds; clean V4 T-pose/heat-map skin with stable whole shirt/collar panels; static-FBX surface material; Claude-reference Casual_Walk_inplace action 613 at poseStrength=1 and full authored sagittal/arm dynamics"
                            : fatherCleanBipedNaturalWalk
                                ? "one locked uniform scale from the paid static Father V18 rest bounds; handcrafted two-contact SD biped cycle; no generated moving mesh"
                            : fatherHiggsfieldIdleRun
                                ? "one locked uniform model scale calibrated from idle-0 projected bounds to the live Father sprite; no per-pose rescaling"
                            : "live SpriteRenderer bounds viewport height / QA projected viewport height per metre",
                    supportedPhases = fatherDeskWorkQa
                        ? new[]
                        {
                            "Idle", "Navigating", "ApproachingSeat", "AligningSeat",
                            "RotatingToSeat", "SittingDown", "Working", "FinishingWork",
                            "StandingUp", "LeavingSeat"
                        }
                        : new[] { "Idle(standing)", "Navigating(walking)" },
                    unsupportedPhasePolicy =
                        fatherDeskWorkQa
                            ? "Outside only restores 2D; the real Father seat lifecycle stays full 3D"
                            : "Approaching/alignment/seating/work/egress/outside skip 3D and restore original 2D forceRenderingOff",
                    sortingDepthPolicy =
                        "sortingLayerID/name/order and source transform Z are observed only and never assigned",
                    sharedCycleSeconds = fatherStaticRootMotionOnly
                        ? 0f
                        : bindings.Count > 0 && bindings[0].WalkActor != null
                            ? bindings[0].WalkActor.CycleSeconds
                            : fatherCleanBipedNaturalWalk
                                ? Family3DWalkActor.FatherSdCycleSeconds
                                : Family3DWalkActor.LockedCycleSeconds,
                    staticMapScaleTolerance = StaticMapScaleTolerance,
                    movingSampleFrames = movingSampleFrames,
                    fatherMapWalkQa = fatherMapWalkQa,
                    fatherMapWalkSourceFamilyId = fatherMapWalkQa ? "father" : string.Empty,
                    fatherMovingSampleFrames = fatherMovingSampleFrames,
                    fatherProofRoutePolicy = fatherFourDirectionDeskPoseQa
                        ? "actual Father OfficeRuntimeAgent; production PlaceWorkstation from the " +
                          "empty office shell at SouthEast/SouthWest/NorthWest/NorthEast; four " +
                          "real claims, seat transitions, Working poses and keyboard/foot IK gates"
                        : fatherDeskWorkQa
                        ? "actual Father OfficeRuntimeAgent; Starter entrance to real seat_father; " +
                          "real route, claim, approach, rotation, SitDown and Working"
                        : fatherMapWalkQa
                            ? "actual Father OfficeRuntimeAgent; one clear 3x3 perimeter; two continuous circuits"
                        : string.Empty,
                    fatherProofRouteCompleted = fatherProofRouteCompleted,
                    fatherDeskWorkQa = fatherDeskWorkQa,
                    fatherDeskWorkProofCompleted = fatherDeskWorkProofCompleted,
                    fatherFourDirectionDeskPoseQa = fatherFourDirectionDeskPoseQa,
                    fatherDeskFourDirectionProofCompleted =
                        fatherDeskFourDirectionProofCompleted,
                    fatherDeskDirectionCount = fatherDeskDirectionReceipts.Count,
                    fatherDeskDirections = fatherDeskDirectionReceipts.ToArray(),
                    v27ExpectedWorkstationCount = v27ExpectedWorkstationCount,
                    v27CreatedWorkstationCount = v27Workstations.Count,
                    v27ReplacedSeatIds = BuildV27ReplacedSeatIds(),
                    v27VisibleLegacyWorkstationRendererCount =
                        v27VisibleLegacyWorkstationRendererCount,
                    v31AtomicOriginalChairWorkstationSetCount = v27Workstations.Count,
                    workstationSetPlacementPolicy =
                        "user-selected V29 desk, CRT, keyboard, chair and seated composition is " +
                        "unchanged; desk + chair share one atomic V31 visual root; production " +
                        "hard/interaction occupancy and complete workstation move/rotate binding " +
                        "remain unchanged",
                    occupancyStaticViolationCount = starter == null || starter.World == null
                        ? -1
                        : starter.World.Occupancy.StaticViolationCount,
                    occupancyInteractionViolationCount = starter == null || starter.World == null
                        ? -1
                        : starter.World.Occupancy.InteractionViolationCount,
                    occupancyAgentPenetrationCount = starter == null || starter.World == null
                        ? -1
                        : starter.World.Occupancy.AgentPenetrationCount,
                    occupancyBlockedStaticMoveCount = starter == null || starter.World == null
                        ? -1
                        : starter.World.Occupancy.BlockedStaticMoveCount,
                    occupancyBlockedInteractionMoveCount = starter == null || starter.World == null
                        ? -1
                        : starter.World.Occupancy.BlockedInteractionMoveCount,
                    fatherDeskSeatId = fatherDeskSeat == null ? string.Empty : fatherDeskSeat.SeatId,
                    fatherDeskFurnitureId = fatherDeskSeat == null
                        ? string.Empty
                        : fatherDeskSeat.WorkSurfaceFurnitureId,
                    fatherDeskChairId = fatherDeskSeat == null
                        ? string.Empty
                        : fatherDeskSeat.ChairFurnitureId,
                    fatherDeskObservedPhases = fatherDeskObservedPhases.ToArray(),
                    fatherDeskWorkSampleFrames = fatherDeskWorkSampleFrames,
                    fatherDeskWorkFrames = fatherDeskWorkFrames,
                    fatherDeskSeatedBlend01 = fatherDeskSeatedBlend01,
                    fatherDeskKeyboardWorld = fatherDeskWorkstation == null
                        ? Vector3.zero
                        : fatherDeskWorkstation.KeyboardWorld,
                    fatherDeskGridYawDegrees = fatherDeskWorkstation == null
                        ? 0f
                        : fatherDeskWorkstation.GridRotationWorld.eulerAngles.y,
                    fatherDeskSeatedVisualYawOffsetDegrees = fatherDeskWorkstation == null
                        ? 0f
                        : fatherDeskWorkstation.SeatedVisualYawOffsetDegrees,
                    fatherDeskSeatToKeyboardFacingErrorDegrees = fatherDeskWorkstation == null
                        ? 0f
                        : fatherDeskWorkstation.SeatToKeyboardFacingErrorDegrees,
                    fatherDeskSeatToMonitorFacingErrorDegrees = fatherDeskWorkstation == null
                        ? 0f
                        : fatherDeskWorkstation.SeatToMonitorFacingErrorDegrees,
                    fatherDeskChairToMonitorFacingErrorDegrees = fatherDeskWorkstation == null
                        ? 0f
                        : fatherDeskWorkstation.ChairToMonitorFacingErrorDegrees,
                    fatherDeskActorModelForwardYawOffsetDegrees = fatherDeskWorkstation == null
                        ? 0f
                        : fatherDeskWorkstation.ActorModelForwardYawOffsetDegrees,
                    fatherDeskMonitorScreenToSeatFacingErrorDegrees =
                        fatherDeskWorkstation == null
                            ? 0f
                            : fatherDeskWorkstation.MonitorScreenToSeatFacingErrorDegrees,
                    fatherDeskSemanticSeatToScreenFacingSeatDistance =
                        fatherDeskWorkstation == null
                            ? 0f
                            : fatherDeskWorkstation.SemanticSeatToScreenFacingSeatDistance,
                    fatherDeskPlacementPolicy =
                        "shop/layout integer origin + semantic footprint owns 3D top center/size; " +
                        "semantic BlocksMovement cells own navigation; calibrated desk seat/work " +
                        "socket seeds the route and lateral keyboard alignment; physical keyboard " +
                        "depth is constrained to the operator-facing front row; physical CRT " +
                        "screen outward normal owns the final chair/actor side and centreline; " +
                        "chair and seated body must clear the physical desk-front plane; chair +Z " +
                        "and measured actor-body forward face the screen with zero readability yaw offset",
                    fatherDeskFootprintOrigin = fatherDeskFootprintOrigin,
                    fatherDeskFootprintSize = fatherDeskFootprintSize,
                    fatherDeskBlockedCells = fatherDeskBlockedCells,
                    fatherDeskBlockedCellsNonWalkable = fatherDeskBlockedCellsNonWalkable,
                    fatherDeskLegacyChairAnchorOffsetWorld =
                        fatherDeskLegacyChairAnchorOffsetWorld,
                    fatherDeskResolvedChairActorSocketErrorWorld =
                        fatherDeskResolvedChairActorSocketErrorWorld,
                    fatherDeskFootprintCenterWorld = fatherDeskWorkstation == null
                        ? Vector3.zero
                        : fatherDeskWorkstation.DeskFootprintCenterWorld,
                    fatherDeskTopCenterWorld = fatherDeskWorkstation == null
                        ? Vector3.zero
                        : fatherDeskWorkstation.DeskTopCenterWorld,
                    fatherDeskFootprintWidthWorld = fatherDeskWorkstation == null
                        ? 0f
                        : fatherDeskWorkstation.DeskFootprintWidthWorld,
                    fatherDeskFootprintDepthWorld = fatherDeskWorkstation == null
                        ? 0f
                        : fatherDeskWorkstation.DeskFootprintDepthWorld,
                    fatherDeskGridAxisOrthogonalityErrorDegrees = fatherDeskWorkstation == null
                        ? 0f
                        : fatherDeskWorkstation.GridAxisOrthogonalityErrorDegrees,
                    fatherDeskSeatToKeyboardGroundDistance = fatherDeskWorkstation == null
                        ? 0f
                        : fatherDeskWorkstation.SeatToKeyboardGroundDistance,
                    fatherDeskMaximumSeatToKeyboardGroundDistance =
                        fatherDeskWorkstation == null
                            ? 0f
                            : fatherDeskWorkstation.MaximumSeatToKeyboardGroundDistanceWorld,
                    fatherDeskKeyboardToMonitorScreenGroundDistance =
                        fatherDeskWorkstation == null
                            ? 0f
                            : fatherDeskWorkstation.KeyboardToMonitorScreenGroundDistanceWorld,
                    fatherDeskMinimumKeyboardToMonitorScreenGroundDistance =
                        fatherDeskWorkstation == null
                            ? 0f
                            : fatherDeskWorkstation.MinimumKeyboardToMonitorScreenGroundDistanceWorld,
                    fatherDeskKeyboardInsetFromDeskFrontWorld = fatherDeskWorkstation == null
                        ? 0f
                        : fatherDeskWorkstation.KeyboardInsetFromDeskFrontWorld,
                    fatherDeskSeatToDeskFrontClearanceWorld = fatherDeskWorkstation == null
                        ? 0f
                        : fatherDeskWorkstation.SeatToDeskFrontClearanceWorld,
                    fatherDeskMinimumSeatToDeskFrontClearanceWorld =
                        fatherDeskWorkstation == null
                            ? 0f
                            : fatherDeskWorkstation.MinimumSeatToDeskFrontClearanceWorld,
                    fatherCaptureSampleCount = fatherCaptureSamples.Count,
                    fatherMotionStrideOfficeUnits = fatherMotionStrideOfficeUnits,
                    fatherMotionYawDegreesPerSecond = fatherMotionYawDegreesPerSecond,
                    fatherMotionFacingOffsetDegrees = fatherMotionFacingOffsetDegrees,
                    fatherMotionTurnSeconds = fatherMotionTurnSeconds,
                    fatherCaptureDeltaSeconds = Time.captureDeltaTime,
                    fatherCaptureSimulationSeconds = (float)fatherCaptureSimulationSeconds,
                    fatherCaptureSamples = fatherCaptureSamples.ToArray(),
                    compositeCapturedFrames = compositeCapturedFrames,
                    compositeCaptureFrameStride = fatherStaticRootMotionOnly
                        ? 12
                        : fatherCompositeFrameStride,
                    compositeCaptureFramesPerSecond = fatherStaticRootMotionOnly
                        ? 5f
                        : 60f / fatherCompositeFrameStride,
                    minimumCompositeLumaRange = compositeCapturedFrames > 0
                        ? minimumCompositeLumaRange
                        : 0,
                    maximumCompositeLumaRange = maximumCompositeLumaRange,
                    compositeVisualContentPass = compositeCapturedFrames > 0 &&
                                                 minimumCompositeLumaRange >= 24,
                    candidates = candidates
                }, true);
                File.WriteAllText(Path.Combine(root, "runtime-receipt.json"), json);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "FAMILY_3D_STARTER_OFFICE_QA: runtime receipt write failed | " +
                    exception.Message, this);
            }
        }

        private static string ResolveRuntimeReceiptRoot()
        {
            string result = Path.Combine(
                Application.persistentDataPath,
                "Family3DStarterOfficeCandidateQa");
            string[] args = Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length - 1; index++)
            {
                if (!string.Equals(
                        args[index],
                        "-family3d-starter-office-qa-runtime-output",
                        StringComparison.OrdinalIgnoreCase))
                    continue;
                result = Path.GetFullPath(args[index + 1]);
                break;
            }
            return result;
        }

        private static bool HasExplicitRuntimeOutput()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length - 1; index++)
            {
                if (string.Equals(
                        args[index],
                        "-family3d-starter-office-qa-runtime-output",
                        StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static bool HasCommandLineFlag(string flag)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length; index++)
            {
                if (string.Equals(args[index], flag, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static float ResolveCommandLineFloat(
            string flag,
            float fallback,
            float minimum,
            float maximum,
            string label)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length - 1; index++)
            {
                if (!string.Equals(args[index], flag, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!float.TryParse(
                        args[index + 1],
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out float value) ||
                    value < minimum || value > maximum)
                    throw new InvalidOperationException(
                        label + " must be in [" + minimum.ToString(CultureInfo.InvariantCulture) +
                        ", " + maximum.ToString(CultureInfo.InvariantCulture) + "].");
                return value;
            }
            return fallback;
        }

        private static int ResolvePositiveIntArgument(
            string flag,
            int fallback,
            int minimum,
            int maximum)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length - 1; index++)
            {
                if (!string.Equals(args[index], flag, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!int.TryParse(
                        args[index + 1],
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int value) ||
                    value < minimum || value > maximum)
                    throw new InvalidOperationException(
                        flag + " must be an integer in [" + minimum + ", " + maximum + "].");
                return value;
            }
            return fallback;
        }

        private static float ResolveAutoQuitSeconds()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length - 1; index++)
            {
                if (!string.Equals(
                        args[index],
                        "-family3d-starter-office-qa-auto-quit-seconds",
                        StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!float.TryParse(
                        args[index + 1],
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out float seconds) ||
                    seconds <= 0f || seconds > 120f)
                    throw new InvalidOperationException(
                        "Starter Office QA auto-quit seconds must be in (0, 120].");
                return seconds;
            }
            return 0f;
        }

        /// <summary>
        /// Office-unit distance the Father V18 moving clip's stride covers, the value that makes the
        /// feet match the ground. It is not the office stride: OfficeLocomotionGaitRules fixes that
        /// at DefaultStrideLength for every actor and DirectionalSpriteAnimator throws on any other
        /// value, so a clip with a different stride is matched here instead of there.
        ///
        /// Solve it from planted-foot world drift, not from the whole airborne foot range. V61
        /// compared hidden two-circuit runs at 0.65, 0.675, 0.70 and 0.7226; 0.675 produced the
        /// smallest combined low-foot displacement. Re-measure after any clip, pose mapping, or
        /// projected-height change.
        /// </summary>
        /// <summary>
        /// Turn rate in degrees per second. 360 puts a 90 degree corner at a quarter second, which
        /// is about how long a walking person takes, and a 180 degree reversal at half a second.
        /// Raising it toward a snap defeats the blend; lowering it makes the Father visibly walk
        /// sideways out of corners.
        /// </summary>
        /// <summary>
        /// Yaw added after LookRotation so the body's own forward, not Unity's +Z, ends up along
        /// travel. Overridable so the four right-angle candidates can be compared in one sitting
        /// rather than argued about.
        /// </summary>
        /// <summary>
        /// Upper bound on how long any turn may take, in seconds. The office switches facing in one
        /// frame, so every frame the 3D body spends catching up is a frame it walks sideways; this
        /// caps that window regardless of turn size. Too small and the turn reads as a snap, which
        /// is what the blend exists to avoid.
        /// </summary>
        private static float ResolveFatherMotionTurnSeconds()
        {
            const float defaultTurnSeconds = 0.18f;
            string[] args = Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length - 1; index++)
            {
                if (!string.Equals(
                        args[index],
                        "-family3d-father-v18-motion-turn-seconds",
                        StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!float.TryParse(
                        args[index + 1],
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out float seconds) ||
                    seconds < 0.02f || seconds > 1f)
                    throw new InvalidOperationException(
                        "Father V18 turn seconds must be in [0.02, 1].");
                return seconds;
            }
            return defaultTurnSeconds;
        }

        /// <summary>
        /// Per-candidate default, overridable both ways from the command line so the same build can
        /// be compared with and without the retarget.
        /// </summary>
        private static bool ResolveClipMuscleDeltaRetarget(bool assetDefault)
        {
            if (HasCommandLineFlag("-family3d-father-v18-motion-clip-delta-retarget"))
                return true;
            if (HasCommandLineFlag("-family3d-father-v18-motion-no-clip-delta-retarget"))
                return false;
            return assetDefault;
        }

        private float ResolveFatherMotionFacingOffsetDegrees()
        {
            float defaultOffsetDegrees = fatherMotionFacingOffsetDegreesAsset;
            string[] args = Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length - 1; index++)
            {
                if (!string.Equals(
                        args[index],
                        "-family3d-father-v18-motion-facing-offset-degrees",
                        StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!float.TryParse(
                        args[index + 1],
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out float degrees) ||
                    degrees < -360f || degrees > 360f)
                    throw new InvalidOperationException(
                        "Father V18 facing offset must be in [-360, 360] degrees.");
                return degrees;
            }
            return defaultOffsetDegrees;
        }

        private static float ResolveFatherMotionYawDegreesPerSecond()
        {
            const float defaultDegreesPerSecond = 360f;
            string[] args = Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length - 1; index++)
            {
                if (!string.Equals(
                        args[index],
                        "-family3d-father-v18-motion-yaw-degrees-per-second",
                        StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!float.TryParse(
                        args[index + 1],
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out float degrees) ||
                    degrees < 45f || degrees > 3600f)
                    throw new InvalidOperationException(
                        "Father V18 motion yaw rate must be in [45, 3600] degrees per second.");
                return degrees;
            }
            return defaultDegreesPerSecond;
        }

        private float ResolveFatherMotionStrideOfficeUnits()
        {
            float defaultStrideOfficeUnits = fatherMotionStrideOfficeUnitsAsset > 0f
                ? fatherMotionStrideOfficeUnitsAsset
                : 0.8526f;
            string[] args = Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length - 1; index++)
            {
                if (!string.Equals(
                        args[index],
                        "-family3d-father-v18-motion-stride-office-units",
                        StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!float.TryParse(
                        args[index + 1],
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out float stride) ||
                    stride < 0.2f || stride > 2f)
                    throw new InvalidOperationException(
                        "Father V18 motion stride must be in [0.2, 2] office units.");
                return stride;
            }
            return defaultStrideOfficeUnits;
        }

        private static float ResolveFatherMotionPoseStrength()
        {
            // Restored to 1.0 on 2026-08-26. The previous 0.45 came from a still-silhouette A/B
            // that judged the split-kick without measuring travel. ApplyPoseStrength slerps every
            // bone toward the rest pose, so damping the silhouette also halved the stride: the legs
            // delivered 0.29-0.34 body heights per cycle against 0.56 of body travel, forcing
            // 0.33-0.40 u of slip every cycle. Re-measure stride against office speed before
            // selecting any value below 1.0, and never select one from stills again.
            const float defaultStrength = 1f;
            string[] args = Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length - 1; index++)
            {
                if (!string.Equals(
                        args[index],
                        "-family3d-father-v18-motion-pose-strength",
                        StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!float.TryParse(
                        args[index + 1],
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out float strength) ||
                    strength < 0.25f || strength > 1f)
                    throw new InvalidOperationException(
                        "Father V18 motion pose strength must be in [0.25, 1].");
                return strength;
            }
            return defaultStrength;
        }

        private void OnApplicationQuit()
        {
            WriteRuntimeReceipt(
                fatherDeskFourDirectionProofCompleted
                    ? "FATHER_V19_FOUR_DIRECTION_DESK_POSE_PROOF_COMPLETE"
                    : fatherDeskWorkProofCompleted
                    ? "FATHER_V19_FULL_3D_ALL_WORKSTATIONS_PROOF_COMPLETE"
                    : fatherProofRouteCompleted
                    ? fatherStaticRootMotionOnly
                        ? "FATHER_V18_STATIC_MAP_MOVE_PROOF_COMPLETE"
                        : FatherUsesNative613Package
                            ? "FATHER_V18_NATIVE_613_WALK_MAP_PROOF_COMPLETE"
                        : FatherUsesStableBodySideArmWalk
                            ? "FATHER_V18_CLEAN_BIPED_STABLE_ARM_WALK_MAP_PROOF_COMPLETE"
                        : FatherUsesCleanBipedCasualWalk
                            ? "FATHER_V18_CLEAN_BIPED_CLAUDE_WALK_MAP_PROOF_COMPLETE"
                        : fatherCleanBipedNaturalWalk
                            ? "FATHER_V18_CLEAN_BIPED_NATURAL_MAP_PROOF_COMPLETE"
                        : fatherHiggsfieldIdleRun
                            ? "FATHER_V18_HIGGSFIELD_IDLE_RUN_MAP_PROOF_COMPLETE"
                        : "FATHER_NATURAL_MAP_WALK_PROOF_COMPLETE"
                    : IsBound
                        ? "APPLICATION_QUIT_AFTER_BIND"
                        : "APPLICATION_QUIT_UNBOUND");
        }

        private void OnDisable()
        {
            shuttingDown = true;
            // Time.captureDeltaTime is global and survives leaving play mode, so an Editor run of
            // this QA would otherwise leave the Editor pinned to a fixed step after the scene
            // stops. Released here rather than only on quit, because the built player exits anyway
            // and the Editor is the case that would actually be damaged.
            Time.captureDeltaTime = 0f;
            ReleaseBindings();
            RestoreSourceCameraCullingMasks();
        }

        [Serializable]
        private sealed class RuntimeReceipt
        {
            public string contract;
            public string receiptStatus;
            public string failureReason;
            public bool productionMutation;
            public bool productionEligible;
            public bool starterReady;
            public int actorCount;
            public int starterActorCount;
            public bool fatherStaticRootMotionOnly;
            public bool fatherHiggsfieldIdleRun;
            public bool fatherCleanBipedNaturalWalk;
            public bool fatherCleanBipedCasualWalk;
            public bool fatherStableBodySideArmWalk;
            public bool fatherNative613Package;
            public string coordinateMapping;
            public string directionMapping;
            public string scalePolicy;
            public string[] supportedPhases;
            public string unsupportedPhasePolicy;
            public string sortingDepthPolicy;
            public float sharedCycleSeconds;
            public float staticMapScaleTolerance;
            public int movingSampleFrames;
            public bool fatherMapWalkQa;
            public string fatherMapWalkSourceFamilyId;
            public int fatherMovingSampleFrames;
            public string fatherProofRoutePolicy;
            public bool fatherProofRouteCompleted;
            public bool fatherDeskWorkQa;
            public bool fatherDeskWorkProofCompleted;
            public bool fatherFourDirectionDeskPoseQa;
            public bool fatherDeskFourDirectionProofCompleted;
            public int fatherDeskDirectionCount;
            public FatherDeskDirectionReceipt[] fatherDeskDirections;
            public int v27ExpectedWorkstationCount;
            public int v27CreatedWorkstationCount;
            public string[] v27ReplacedSeatIds;
            public int v27VisibleLegacyWorkstationRendererCount;
            public int v31AtomicOriginalChairWorkstationSetCount;
            public string workstationSetPlacementPolicy;
            public int occupancyStaticViolationCount;
            public int occupancyInteractionViolationCount;
            public int occupancyAgentPenetrationCount;
            public int occupancyBlockedStaticMoveCount;
            public int occupancyBlockedInteractionMoveCount;
            public string fatherDeskSeatId;
            public string fatherDeskFurnitureId;
            public string fatherDeskChairId;
            public string[] fatherDeskObservedPhases;
            public int fatherDeskWorkSampleFrames;
            public int fatherDeskWorkFrames;
            public float fatherDeskSeatedBlend01;
            public Vector3 fatherDeskKeyboardWorld;
            public float fatherDeskGridYawDegrees;
            public float fatherDeskSeatedVisualYawOffsetDegrees;
            public float fatherDeskSeatToKeyboardFacingErrorDegrees;
            public float fatherDeskSeatToMonitorFacingErrorDegrees;
            public float fatherDeskChairToMonitorFacingErrorDegrees;
            public float fatherDeskActorModelForwardYawOffsetDegrees;
            public float fatherDeskMonitorScreenToSeatFacingErrorDegrees;
            public float fatherDeskSemanticSeatToScreenFacingSeatDistance;
            public string fatherDeskPlacementPolicy;
            public Vector2Int fatherDeskFootprintOrigin;
            public Vector2Int fatherDeskFootprintSize;
            public string[] fatherDeskBlockedCells;
            public bool fatherDeskBlockedCellsNonWalkable;
            public float fatherDeskLegacyChairAnchorOffsetWorld;
            public float fatherDeskResolvedChairActorSocketErrorWorld;
            public Vector3 fatherDeskFootprintCenterWorld;
            public Vector3 fatherDeskTopCenterWorld;
            public float fatherDeskFootprintWidthWorld;
            public float fatherDeskFootprintDepthWorld;
            public float fatherDeskGridAxisOrthogonalityErrorDegrees;
            public float fatherDeskSeatToKeyboardGroundDistance;
            public float fatherDeskMaximumSeatToKeyboardGroundDistance;
            public float fatherDeskKeyboardToMonitorScreenGroundDistance;
            public float fatherDeskMinimumKeyboardToMonitorScreenGroundDistance;
            public float fatherDeskKeyboardInsetFromDeskFrontWorld;
            public float fatherDeskSeatToDeskFrontClearanceWorld;
            public float fatherDeskMinimumSeatToDeskFrontClearanceWorld;
            public int fatherCaptureSampleCount;
            public float fatherMotionStrideOfficeUnits;
            public float fatherMotionYawDegreesPerSecond;
            public float fatherMotionFacingOffsetDegrees;
            public float fatherMotionTurnSeconds;
            public float fatherCaptureDeltaSeconds;
            public float fatherCaptureSimulationSeconds;
            public FatherCaptureSample[] fatherCaptureSamples;
            public int compositeCapturedFrames;
            public int compositeCaptureFrameStride;
            public float compositeCaptureFramesPerSecond;
            public int minimumCompositeLumaRange;
            public int maximumCompositeLumaRange;
            public bool compositeVisualContentPass;
            public RuntimeCandidateReceipt[] candidates;
        }

        [Serializable]
        private sealed class FatherDeskDirectionReceipt
        {
            public int directionIndex;
            public string deskFacing;
            public string seatFacing;
            public string approachCell;
            public int semanticQuarterTurns;
            public float seatedRootWorldYawDegrees;
            public float rootFacingErrorDegrees;
            public float seatToKeyboardFacingErrorDegrees;
            public float seatToMonitorFacingErrorDegrees;
            public float chairToMonitorFacingErrorDegrees;
            public float monitorScreenToSeatFacingErrorDegrees;
            public float seatRootGroundError;
            public float handMidpointError;
            public float handLateralOrder;
            public float footMidpointError;
            public float footLateralOrder;
            public float leftKneeBendDegrees;
            public float rightKneeBendDegrees;
            public float leftFootChairRadialClearance;
            public float rightFootChairRadialClearance;
            public float leftFootChairForwardClearance;
            public float rightFootChairForwardClearance;
            public float leftKneeChairForwardClearance;
            public float rightKneeChairForwardClearance;
            public int sampledSkinVertexCount;
            public int chairSkinPenetratingVertexCount;
            public int chairCushionPenetratingVertexCount;
            public int chairBackPenetratingVertexCount;
            public int chairLumbarPenetratingVertexCount;
            public int chairStemPenetratingVertexCount;
            public int chairFootPenetratingVertexCount;
            public float chairCushionPenetrationMinimumLocalY;
            public float chairCushionPenetrationMaximumLocalY;
            public int staticViolationCount;
            public int interactionViolationCount;
            public int agentPenetrationCount;
        }

        [Serializable]
        private sealed class FatherCaptureSample
        {
            public int frameIndex;
            public float realtimeSeconds;
            public float simulationSeconds;
            public float rootWorldYawDegrees;
            public float targetYawDegrees;
            public int routeCircuit;
            public int routeLeg;
            public Vector2 officePosition;
            public Vector3 rootWorldPosition;
            public float gaitPhase01;
            public int direction;
            public Vector2 actualDisplacement;
            public Vector3 leftFootLocal;
            public Vector3 rightFootLocal;
            public Vector3 leftFootWorld;
            public Vector3 rightFootWorld;
            public Vector3 leftHandLocal;
            public Vector3 rightHandLocal;
            public Vector3 hipsLocal;
            public Vector3 toeForwardLocal;
            public Vector3 torsoUpLocal;
            public float motionPhase01;
            public bool leftFootPlanted;
            public bool rightFootPlanted;
        }

        [Serializable]
        private sealed class RuntimeCandidateReceipt
        {
            public string familyId;
            public float sourceSpriteWorldHeight;
            public float sourceSpriteViewportHeight;
            public float target3DHeight;
            public float appliedModelScale;
            public float poseStrength;
            public bool staticRootMotionOnly;
            public float lastScaleMatchRatio;
            public float minimumScaleMatchRatio;
            public float maximumScaleMatchRatio;
            public float maximumScaleError;
            public float maximumGroundError;
            public int sourceSortingLayerId;
            public string sourceSortingLayerName;
            public int sourceSortingOrder;
            public float sourceWorldZ;
            public int unsupportedPhaseFallbackCount;
            public Vector2 lastObservedDisplacement;
            public float lastObservedGaitPhase01;
            public int lastObservedDirection;
            public bool movingAtReceipt;
            public int movingFrameCount;
            public int observedDirectionMask;
            public float minimumObservedGaitPhase01;
            public float maximumObservedGaitPhase01;
            public Vector3 qaHostWorldPosition;
        }

        private readonly struct CandidateDefinition
        {
            public CandidateDefinition(string familyId, GameObject prefab)
            {
                FamilyId = familyId;
                Prefab = prefab;
            }

            public string FamilyId { get; }
            public GameObject Prefab { get; }
        }

        private sealed class Binding
        {
            private readonly RendererState[] candidateRenderers;
            private RendererState seatedProtection;

            public Binding(
                string familyId,
                OfficeRuntimeAgent agent,
                GameObject host,
                GameObject model,
                Family3DWalkActor walkActor,
                Renderer[] modelRenderers,
                SpriteRenderer mainSource,
                float sourceSpriteWorldHeight,
                float sourceSpriteViewportHeight,
                float target3DHeight,
                float appliedModelScale,
                float poseStrength,
                bool staticRootMotionOnly = false)
            {
                FamilyId = familyId;
                Agent = agent;
                Host = host;
                Model = model;
                WalkActor = walkActor;
                MainSource = new RendererState(mainSource);
                candidateRenderers = new RendererState[modelRenderers.Length];
                for (var index = 0; index < modelRenderers.Length; index++)
                    candidateRenderers[index] = new RendererState(modelRenderers[index]);
                SourceSpriteWorldHeight = sourceSpriteWorldHeight;
                SourceSpriteViewportHeight = sourceSpriteViewportHeight;
                Target3DHeight = target3DHeight;
                AppliedModelScale = appliedModelScale;
                PoseStrength = poseStrength;
                StaticRootMotionOnly = staticRootMotionOnly;
                WasSupportedLastFrame = true;
                MinimumObservedGaitPhase01 = 1f;
                MinimumScaleMatchRatio = float.PositiveInfinity;
            }

            public string FamilyId { get; }
            public OfficeRuntimeAgent Agent { get; }
            public GameObject Host { get; }
            public GameObject Model { get; }
            public Family3DWalkActor WalkActor { get; }
            public RendererState MainSource { get; }
            public float SourceSpriteWorldHeight { get; }
            public float SourceSpriteViewportHeight { get; }
            public float Target3DHeight { get; set; }
            public float AppliedModelScale { get; set; }
            public float PoseStrength { get; }
            public bool StaticRootMotionOnly { get; }
            public float LastScaleMatchRatio { get; set; }
            public float MinimumScaleMatchRatio { get; set; }
            public float MaximumScaleMatchRatio { get; set; }
            public float MaximumScaleError { get; set; }
            public float MaximumGroundError { get; set; }
            public int UnsupportedPhaseFallbackCount { get; set; }
            public bool WasSupportedLastFrame { get; set; }
            public Vector2 LastObservedDisplacement { get; set; }
            public float LastObservedGaitPhase01 { get; set; }
            public int LastObservedDirection { get; set; }
            public bool IsMoving { get; set; }
            public int MovingFrameCount { get; set; }

            /// <summary>
            /// Yaw actually applied to the 3D host, blended toward the office facing instead of
            /// snapping to it. The office agent resolves facing to one of eight octants, so an
            /// unblended host jumps 45 or 90 degrees in a single frame at every corner.
            /// </summary>
            public Quaternion BlendedYaw { get; set; } = Quaternion.identity;
            public bool HasBlendedYaw { get; set; }

            /// <summary>
            /// Rate latched when the current turn began, so the turn is linear rather than an
            /// exponential approach that never lands. Reset once the blend reaches its target.
            /// </summary>
            public Quaternion ActiveTurnTarget { get; set; } = Quaternion.identity;
            public float ActiveTurnRate { get; set; }
            public bool HasActiveTurn { get; set; }

            /// <summary>
            /// Previous ground position, used to face the direction the actor actually moves in.
            /// </summary>
            public Vector3 LastQaGroundPosition { get; set; }
            public bool HasQaGroundPosition { get; set; }
            public Quaternion TravelYaw { get; set; } = Quaternion.identity;
            public bool HasTravelYaw { get; set; }
            public int ObservedDirectionMask { get; set; }
            public float MinimumObservedGaitPhase01 { get; set; }
            public float MaximumObservedGaitPhase01 { get; set; }

            public void EnsureSeatedProtectionSnapshot()
            {
                SpriteRenderer current = Agent.SeatedUpperBodyProtectionRenderer;
                if (current == null)
                {
                    seatedProtection = null;
                    return;
                }
                if (seatedProtection == null || seatedProtection.Renderer != current)
                    seatedProtection = new RendererState(current);
            }

            public void SetSource2DHidden(bool hidden)
            {
                MainSource.SetQaHidden(hidden);
                seatedProtection?.SetQaHidden(hidden);
            }

            public void SetCandidateVisible(bool visible)
            {
                for (var index = 0; index < candidateRenderers.Length; index++)
                    candidateRenderers[index].SetQaHidden(!visible);
            }

            public void RestoreSourceRenderers()
            {
                MainSource.RestoreForceRenderingOff();
                seatedProtection?.RestoreForceRenderingOff();
            }
        }

        private sealed class RendererState
        {
            private readonly bool initialForceRenderingOff;

            public RendererState(Renderer renderer)
            {
                Renderer = renderer;
                initialForceRenderingOff = renderer != null && renderer.forceRenderingOff;
                if (renderer is SpriteRenderer sprite)
                {
                    SortingLayerId = sprite.sortingLayerID;
                    SortingLayerName = sprite.sortingLayerName;
                    SortingOrder = sprite.sortingOrder;
                    WorldZ = sprite.transform.position.z;
                }
                else
                {
                    SortingLayerId = 0;
                    SortingLayerName = string.Empty;
                    SortingOrder = 0;
                    WorldZ = renderer == null ? 0f : renderer.transform.position.z;
                }
            }

            public Renderer Renderer { get; }
            public int SortingLayerId { get; }
            public string SortingLayerName { get; }
            public int SortingOrder { get; }
            public float WorldZ { get; }

            public void SetQaHidden(bool hidden)
            {
                if (Renderer != null)
                    Renderer.forceRenderingOff = initialForceRenderingOff || hidden;
            }

            public void RestoreForceRenderingOff()
            {
                if (Renderer != null)
                    Renderer.forceRenderingOff = initialForceRenderingOff;
            }
        }
    }
}
