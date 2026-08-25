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
        private const int MaximumFatherCompositeFrames = 180;

        [Header("Candidate prefabs (Experimental only)")]
        [SerializeField] private GameObject playerCandidate;
        [SerializeField] private GameObject olderSisterCandidate;
        [SerializeField] private GameObject fatherCandidate;
        [SerializeField] private GameObject motherCandidate;
        [SerializeField] private AnimationClip sharedHumanoidWalkClip;

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
            new List<FatherCaptureSample>(MaximumFatherCompositeFrames);
        private StarterOfficeRuntimeBootstrap starter;
        private bool bindAttemptActive;
        private bool shuttingDown;
        private int movingSampleFrames;
        private int compositeCapturedFrames;
        private int fatherMovingSampleFrames;
        private bool fatherMapWalkQa;
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
        public static Quaternion MapOfficeDirectionToUnityYaw(int direction)
        {
            int octant = (direction % 8 + 8) % 8;
            return Quaternion.Euler(0f, (octant - 4) * 45f, 0f);
        }

        private IEnumerator Start()
        {
            float autoQuitSeconds;
            try
            {
                autoQuitSeconds = ResolveAutoQuitSeconds();
                fatherMapWalkQa = HasCommandLineFlag("-family3d-father-map-walk-qa");
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
                    if (HasExplicitRuntimeOutput() &&
                        compositeCapturedFrames < MaximumFatherCompositeFrames &&
                        (fatherMovingSampleFrames == 1 || fatherMovingSampleFrames % 6 == 0))
                        CaptureCompositeQaFrame(
                            sourceOfficeCamera,
                            "father-stylized-sd-map-walk-v17",
                            father);
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
                "FAMILY_3D_FATHER_NATURAL_WALK_QA: starting two continuous circuits on one " +
                "actual Starter Office map; productionEligible=false.",
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
                            "father-stylized-sd-map-walk-v17-c" + circuit + "-leg" + leg))
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
            WriteRuntimeReceipt("FATHER_NATURAL_MAP_WALK_PROOF_COMPLETE");
            Debug.Log(
                "FAMILY_3D_FATHER_NATURAL_WALK_QA: COMPLETE | circuits=2 captures=" +
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
            if (liveActors.Count != definitions.Length)
                throw new InvalidOperationException(
                    $"Expected {definitions.Length} Starter Office actors; found {liveActors.Count}.");

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
            model.name = definition.FamilyId + "_CandidateCompleteSkinnedBody";
            SetLayerRecursively(model, qaLayer);

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

            bool useFatherNaturalSdWalk = definition.Prefab == fatherCandidate;
            if (useFatherNaturalSdWalk)
                targetHeight *= 0.55f;

            float appliedScale = targetHeight / candidateHeight;
            model.transform.localScale *= appliedScale;
            candidateBounds = EncapsulateBounds(skinned);
            model.transform.position += Vector3.up * (groundY - candidateBounds.min.y);

            var walkActor = host.AddComponent<Family3DWalkActor>();
            const float poseStrength = 1f;
            walkActor.Configure(
                definition.FamilyId,
                model.transform,
                animator,
                sharedHumanoidWalkClip,
                qaPosition,
                Color.white,
                poseStrength,
                useFatherNaturalSdWalk);

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
            Quaternion worldRotation = MapOfficeDirectionToUnityYaw(binding.Agent.CurrentDirection);
            float gaitPhase01 = Mathf.Repeat(binding.Agent.GaitPhase01, 1f);
            bool isMoving =
                binding.Agent.LastActualDisplacement.sqrMagnitude > MovementEpsilonSqr;
            double motionClock =
                (gaitPhase01 - binding.WalkActor.PhaseOffset) * Family3DWalkActor.LockedCycleSeconds;
            binding.WalkActor.Tick(motionClock, worldPosition, worldRotation, isMoving);
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

        private void CaptureCompositeQaFrame(
            Camera sourceOfficeCamera,
            string filePrefix = "office-moving",
            Binding captureBinding = null)
        {
            if (sourceOfficeCamera == null || qaOverlayCamera == null)
                return;
            const int width = 1280;
            const int height = 720;
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
            Family3DWalkActor.PoseSnapshot pose = binding.WalkActor.ReadPoseSnapshot();
            fatherCaptureSamples.Add(new FatherCaptureSample
            {
                frameIndex = frameIndex,
                realtimeSeconds = Time.realtimeSinceStartup,
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
                hipsLocal = pose.hipsLocal,
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
            if (starter == null || starter.Actors.Count != bindings.Count)
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
            if (sharedHumanoidWalkClip == null || !sharedHumanoidWalkClip.isHumanMotion)
                throw new InvalidOperationException("Shared Humanoid walk clip is missing or not Humanoid.");
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
                    coordinateMapping =
                        "Office actor XY -> production Camera.WorldToViewportPoint -> QA " +
                        "Camera.ViewportPointToRay -> Y=ground plane; raw (x,y)->(x,groundY,y) fallback",
                    directionMapping = "South..SE direction 0..7 -> yaw=(direction-4)*45 degrees",
                    scalePolicy =
                        "live SpriteRenderer bounds viewport height / QA projected viewport height per metre",
                    supportedPhases = new[] { "Idle(standing)", "Navigating(walking)" },
                    unsupportedPhasePolicy =
                        "Approaching/alignment/seating/work/egress/outside skip 3D and restore original 2D forceRenderingOff",
                    sortingDepthPolicy =
                        "sortingLayerID/name/order and source transform Z are observed only and never assigned",
                    sharedCycleSeconds = Family3DWalkActor.LockedCycleSeconds,
                    movingSampleFrames = movingSampleFrames,
                    fatherMapWalkQa = fatherMapWalkQa,
                    fatherMapWalkSourceFamilyId = fatherMapWalkQa ? "father" : string.Empty,
                    fatherMovingSampleFrames = fatherMovingSampleFrames,
                    fatherProofRoutePolicy = fatherMapWalkQa
                        ? "actual Father OfficeRuntimeAgent; one clear 3x3 perimeter; two continuous circuits"
                        : string.Empty,
                    fatherProofRouteCompleted = fatherProofRouteCompleted,
                    fatherCaptureSampleCount = fatherCaptureSamples.Count,
                    fatherCaptureSamples = fatherCaptureSamples.ToArray(),
                    compositeCapturedFrames = compositeCapturedFrames,
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

        private void OnApplicationQuit()
        {
            WriteRuntimeReceipt(
                fatherProofRouteCompleted
                    ? "FATHER_NATURAL_MAP_WALK_PROOF_COMPLETE"
                    : IsBound
                        ? "APPLICATION_QUIT_AFTER_BIND"
                        : "APPLICATION_QUIT_UNBOUND");
        }

        private void OnDisable()
        {
            shuttingDown = true;
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
            public string coordinateMapping;
            public string directionMapping;
            public string scalePolicy;
            public string[] supportedPhases;
            public string unsupportedPhasePolicy;
            public string sortingDepthPolicy;
            public float sharedCycleSeconds;
            public int movingSampleFrames;
            public bool fatherMapWalkQa;
            public string fatherMapWalkSourceFamilyId;
            public int fatherMovingSampleFrames;
            public string fatherProofRoutePolicy;
            public bool fatherProofRouteCompleted;
            public int fatherCaptureSampleCount;
            public FatherCaptureSample[] fatherCaptureSamples;
            public int compositeCapturedFrames;
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
            public Vector3 hipsLocal;
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
                float poseStrength)
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
                WasSupportedLastFrame = true;
                MinimumObservedGaitPhase01 = 1f;
            }

            public string FamilyId { get; }
            public OfficeRuntimeAgent Agent { get; }
            public GameObject Host { get; }
            public GameObject Model { get; }
            public Family3DWalkActor WalkActor { get; }
            public RendererState MainSource { get; }
            public float SourceSpriteWorldHeight { get; }
            public float SourceSpriteViewportHeight { get; }
            public float Target3DHeight { get; }
            public float AppliedModelScale { get; }
            public float PoseStrength { get; }
            public int UnsupportedPhaseFallbackCount { get; set; }
            public bool WasSupportedLastFrame { get; set; }
            public Vector2 LastObservedDisplacement { get; set; }
            public float LastObservedGaitPhase01 { get; set; }
            public int LastObservedDirection { get; set; }
            public bool IsMoving { get; set; }
            public int MovingFrameCount { get; set; }
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
