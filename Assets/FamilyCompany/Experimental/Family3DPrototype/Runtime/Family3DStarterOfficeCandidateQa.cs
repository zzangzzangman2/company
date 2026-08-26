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
        // See Docs/FATHER_V18_FACING_OFFSET_METHOD.md.
        [SerializeField] private float fatherMotionFacingOffsetDegreesAsset = 90f;
        [SerializeField] private bool fatherClipAnatomicalSanitizationAsset;
        [SerializeField] private float fatherMotionStrideOfficeUnitsAsset;
        [SerializeField] private bool fatherClipMuscleDeltaRetargetAsset;
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
            float fallbackScale = 1f,
            float qaGroundY = 0f)
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
                fatherMapWalkQa =
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
                    StartCoroutine(RunFatherMapWalkProof());
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
                if (fatherProofRouteActive && father != null && father.IsMoving)
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
                FatherUsesCleanBipedCasualWalk + " productionEligible=false.",
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
                fatherHiggsfieldIdleRun && fatherClipAnatomicalSanitizationAsset);
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
            if (sourceOfficeCamera != null && qaOverlayCamera != null)
            {
                Vector3 sourceWorld = new Vector3(
                    position.x,
                    position.y,
                    actor.transform.position.z);
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
                position,
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
            if (fatherHiggsfieldIdleRun &&
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
            for (var index = 0; index < bindings.Count; index++)
            {
                Binding binding = bindings[index];
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
                            : FatherUsesCleanBipedCasualWalk
                                ? "one locked uniform model scale calibrated from idle projected bounds; clean V4 T-pose/heat-map skin with stable whole shirt/collar panels; static-FBX surface material; Claude-reference Casual_Walk_inplace action 613 at poseStrength=1 and full authored sagittal/arm dynamics"
                            : fatherCleanBipedNaturalWalk
                                ? "one locked uniform scale from the paid static Father V18 rest bounds; handcrafted two-contact SD biped cycle; no generated moving mesh"
                            : fatherHiggsfieldIdleRun
                                ? "one locked uniform model scale calibrated from idle-0 projected bounds to the live Father sprite; no per-pose rescaling"
                            : "live SpriteRenderer bounds viewport height / QA projected viewport height per metre",
                    supportedPhases = new[] { "Idle(standing)", "Navigating(walking)" },
                    unsupportedPhasePolicy =
                        "Approaching/alignment/seating/work/egress/outside skip 3D and restore original 2D forceRenderingOff",
                    sortingDepthPolicy =
                        "sortingLayerID/name/order and source transform Z are observed only and never assigned",
                    sharedCycleSeconds = fatherStaticRootMotionOnly
                        ? 0f
                        : fatherCleanBipedNaturalWalk
                            ? Family3DWalkActor.FatherSdCycleSeconds
                            : Family3DWalkActor.LockedCycleSeconds,
                    staticMapScaleTolerance = StaticMapScaleTolerance,
                    movingSampleFrames = movingSampleFrames,
                    fatherMapWalkQa = fatherMapWalkQa,
                    fatherMapWalkSourceFamilyId = fatherMapWalkQa ? "father" : string.Empty,
                    fatherMovingSampleFrames = fatherMovingSampleFrames,
                    fatherProofRoutePolicy = fatherMapWalkQa
                        ? "actual Father OfficeRuntimeAgent; one clear 3x3 perimeter; two continuous circuits"
                        : string.Empty,
                    fatherProofRouteCompleted = fatherProofRouteCompleted,
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
                fatherProofRouteCompleted
                    ? fatherStaticRootMotionOnly
                        ? "FATHER_V18_STATIC_MAP_MOVE_PROOF_COMPLETE"
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
