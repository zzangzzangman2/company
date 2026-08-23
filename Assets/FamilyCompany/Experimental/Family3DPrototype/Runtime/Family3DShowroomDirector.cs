using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace FamilyCompany.Experimental.Family3D
{
    /// <summary>
    /// Neutral, non-production review room matching the video's compare-first workflow.
    /// Four family proxies walk the same continuous four-direction route on one shared phase clock.
    /// </summary>
    [DefaultExecutionOrder(32000)]
    public sealed class Family3DShowroomDirector : MonoBehaviour
    {
        public const float MovementSpeed = 1f;
        public const float PathHalfExtent = 1.5f;
        public const float TurnDegreesPerSecond = 720f;
        public const float RouteLoopSeconds = PathHalfExtent * 8f / MovementSpeed;

        [SerializeField] private Camera reviewCamera;
        [SerializeField] private Family3DWalkActor[] actors = Array.Empty<Family3DWalkActor>();
        [SerializeField] private Vector3 cameraTarget = new Vector3(0f, 1f, 0f);

        private readonly List<ActorAccumulator> accumulators = new List<ActorAccumulator>();
        private double motionClock;
        private bool paused;
        private bool firstRenderedFramePending = true;
        private bool orbitCamera;
        private float cameraYaw = 135f;
        private string qaOutputDirectory;
        private float qaDurationSeconds = 12.25f;
        private bool qaMode;
        private bool qaCaptureActive;
        private int qaFrame;
        private readonly int[] directionSampleCounts = new int[4];
        private Vector3[] previousActorPositions = Array.Empty<Vector3>();
        private Vector3 previousExpectedRouteOffset;
        private bool hasPreviousActorPositions;
        private bool firstUpdateMeasured;
        private bool startupStateApplied;
        private float startupMaximumPositionError;
        private float startupMaximumYawErrorDegrees;
        private float firstUpdateMaximumRootStep;
        private float maximumRootStep;
        private float maximumRootStepExcess;
        private float maximumRoutePositionError;
        private float maximumRouteStepError;
        private float maximumActorYawDivergenceDegrees;
        private int rootContinuityViolationFrames;
        private int audioAuditFrames;
        private int audioOutputViolationFrames;
        private int audioPostEnforcementViolationFrames;
        private int maximumSourcesPlayingBeforeEnforcement;
        private int maximumSourcesUnmutedBeforeEnforcement;
        private float firstCapturedMotionClock = -1f;
        private float lastCapturedMotionClock = -1f;
        private float capturedMotionSeconds;
        private float capturedWallClockSeconds;
        private readonly List<string> frameMetadata = new List<string>();
        private readonly int[] capturedDirectionFrameCounts = new int[4];
        private readonly int[] capturedDirectionPoseMasks = new int[4];
        private readonly List<string> capturedDirectionTransitions = new List<string>();
        private string previousCapturedDirection = string.Empty;
        private bool firstCapturedAtRouteStart;
        private string firstCapturedDirection = string.Empty;
        private int firstCapturedPose = -1;
        private readonly HashSet<int> observedAudioSourceIds = new HashSet<int>();
        private int maximumAudioSourcesObserved;
        private int audioAudibleRiskViolationFrames;

        // The fixed review camera is at world (+X, +Y, -Z). In that projection,
        // world -Z/-X/+Z/+X read as office SW/NW/NE/SE respectively.
        private static readonly string[] DirectionNames = { "SW", "NW", "NE", "SE" };

        public void Configure(Camera camera, Family3DWalkActor[] familyActors)
        {
            reviewCamera = camera;
            actors = familyActors;
        }

        private void Awake()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            EnforceAudioMute();
            ParseCommandLine();
            foreach (Family3DWalkActor actor in actors)
            {
                actor.Initialize();
                accumulators.Add(new ActorAccumulator(actor.FamilyId));
            }

            ApplyExactStartupState();

            if (qaMode)
            {
                qaCaptureActive = true;
                StartCoroutine(RunQaCapture());
            }
        }

        private void Update()
        {
            // Production audio is created by an AfterSceneLoad runtime hook even in
            // this isolated scene. Keep the review lab audibly silent and verify the
            // real runtime state in the QA receipt instead of claiming a constant.
            AuditAudioBeforeEnforcement();
            EnforceAudioMute();
            HandleInput();
            float dt = Time.deltaTime;
            if (!paused && !firstRenderedFramePending)
                motionClock += dt;

            EvaluateRoute(motionClock, out Vector3 routeOffset, out Vector3 direction, out int directionIndex);
            Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            for (var index = 0; index < actors.Length; index++)
            {
                Family3DWalkActor actor = actors[index];
                Quaternion rotation = Quaternion.RotateTowards(
                    actor.transform.rotation,
                    targetRotation,
                    TurnDegreesPerSecond * dt);
                actor.Tick(motionClock, actor.PathCenter + routeOffset, rotation);
                if (qaCaptureActive)
                    accumulators[index].Add(actor.ReadPoseSnapshot(), NormalizedPhase);
            }

            if (qaCaptureActive)
            {
                directionSampleCounts[directionIndex]++;
                AuditRootContinuity(routeOffset);
            }

            UpdateCamera(dt);
            firstRenderedFramePending = false;
        }

        private void LateUpdate()
        {
            if (!qaMode)
                return;

            // Re-audit at the end of the frame so a later gameplay component cannot
            // restart a source after the Update-time guard without being observed.
            AuditAudioBeforeEnforcement();
            EnforceAudioMute();
            if (HasPostEnforcementAudioViolation())
                audioPostEnforcementViolationFrames++;
        }

        private void ApplyExactStartupState()
        {
            EvaluateRoute(0d, out Vector3 routeOffset, out Vector3 direction, out _);
            Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);
            previousActorPositions = new Vector3[actors.Length];
            for (var index = 0; index < actors.Length; index++)
            {
                Family3DWalkActor actor = actors[index];
                Vector3 expectedPosition = actor.PathCenter + routeOffset;
                actor.Tick(0d, expectedPosition, rotation);
                startupMaximumPositionError = Mathf.Max(
                    startupMaximumPositionError,
                    Vector3.Distance(actor.transform.position, expectedPosition));
                startupMaximumYawErrorDegrees = Mathf.Max(
                    startupMaximumYawErrorDegrees,
                    Quaternion.Angle(actor.transform.rotation, rotation));
                previousActorPositions[index] = actor.transform.position;
            }
            hasPreviousActorPositions = true;
            previousExpectedRouteOffset = routeOffset;
            startupStateApplied = true;
        }

        private void AuditRootContinuity(Vector3 expectedRouteOffset)
        {
            if (!hasPreviousActorPositions || previousActorPositions.Length != actors.Length)
                return;

            float expectedStep = Vector3.Distance(previousExpectedRouteOffset, expectedRouteOffset);
            float expectedMaximum = expectedStep + 0.002f;
            float frameMaximumStep = 0f;
            bool frameViolation = false;
            for (var index = 0; index < actors.Length; index++)
            {
                float step = Vector3.Distance(previousActorPositions[index], actors[index].transform.position);
                float routeError = Vector3.Distance(
                    actors[index].transform.position,
                    actors[index].PathCenter + expectedRouteOffset);
                float stepError = Mathf.Abs(step - expectedStep);
                frameMaximumStep = Mathf.Max(frameMaximumStep, step);
                maximumRootStep = Mathf.Max(maximumRootStep, step);
                float excess = Mathf.Max(0f, step - expectedMaximum);
                maximumRootStepExcess = Mathf.Max(maximumRootStepExcess, excess);
                maximumRoutePositionError = Mathf.Max(maximumRoutePositionError, routeError);
                maximumRouteStepError = Mathf.Max(maximumRouteStepError, stepError);
                if (index > 0)
                {
                    maximumActorYawDivergenceDegrees = Mathf.Max(
                        maximumActorYawDivergenceDegrees,
                        Quaternion.Angle(actors[0].transform.rotation, actors[index].transform.rotation));
                }
                frameViolation |= excess > 0.0001f ||
                                  routeError > 0.0001f ||
                                  stepError > 0.0001f;
                previousActorPositions[index] = actors[index].transform.position;
            }
            previousExpectedRouteOffset = expectedRouteOffset;
            if (!firstUpdateMeasured)
            {
                firstUpdateMeasured = true;
                firstUpdateMaximumRootStep = frameMaximumStep;
            }
            if (frameViolation)
                rootContinuityViolationFrames++;
        }

        private void HandleInput()
        {
            if (Input.GetKeyDown(KeyCode.Space))
                paused = !paused;
            if (Input.GetKeyDown(KeyCode.R))
            {
                motionClock = 0d;
                paused = false;
            }
            if (Input.GetKeyDown(KeyCode.O))
                orbitCamera = !orbitCamera;
            if (Input.GetKeyDown(KeyCode.Escape))
                Application.Quit();
        }

        private void UpdateCamera(float dt)
        {
            if (reviewCamera == null)
                return;
            if (orbitCamera)
                cameraYaw += 18f * dt;
            cameraYaw += Input.GetAxisRaw("Horizontal") * 36f * dt;
            reviewCamera.orthographicSize = Mathf.Clamp(
                reviewCamera.orthographicSize - Input.mouseScrollDelta.y * 0.4f,
                4.5f,
                11f);

            float radians = cameraYaw * Mathf.Deg2Rad;
            Vector3 horizontal = new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians)) * 16f;
            reviewCamera.transform.position = cameraTarget + horizontal + Vector3.up * 13f;
            reviewCamera.transform.LookAt(cameraTarget);
        }

        private float NormalizedPhase => Mathf.Repeat(
            (float)(motionClock / Family3DWalkActor.LockedCycleSeconds),
            1f);

        public static void EvaluateRoute(
            double clock,
            out Vector3 routeOffset,
            out Vector3 direction,
            out int directionIndex)
        {
            float sideLength = PathHalfExtent * 2f;
            float perimeter = sideLength * 4f;
            float distance = Mathf.Repeat((float)clock * MovementSpeed, perimeter);
            directionIndex = Mathf.Min(3, Mathf.FloorToInt(distance / sideLength));
            float segmentDistance = distance - directionIndex * sideLength;
            float t = segmentDistance / sideLength;

            // Traverse in the same office-direction order used by the 2D contract.
            // The world vectors are intentionally derived from the fixed isometric
            // camera projection rather than treating world axes as screen labels.
            Vector3 a = new Vector3(PathHalfExtent, 0f, PathHalfExtent);
            Vector3 b = new Vector3(PathHalfExtent, 0f, -PathHalfExtent);
            Vector3 c = new Vector3(-PathHalfExtent, 0f, -PathHalfExtent);
            Vector3 d = new Vector3(-PathHalfExtent, 0f, PathHalfExtent);
            switch (directionIndex)
            {
                case 0:
                    routeOffset = Vector3.LerpUnclamped(a, b, t);
                    direction = Vector3.back;
                    break;
                case 1:
                    routeOffset = Vector3.LerpUnclamped(b, c, t);
                    direction = Vector3.left;
                    break;
                case 2:
                    routeOffset = Vector3.LerpUnclamped(c, d, t);
                    direction = Vector3.forward;
                    break;
                default:
                    routeOffset = Vector3.LerpUnclamped(d, a, t);
                    direction = Vector3.right;
                    break;
            }
        }

        private static void EnforceAudioMute()
        {
            AudioListener.volume = 0f;
            AudioSource[] sources = FindObjectsByType<AudioSource>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (AudioSource source in sources)
            {
                source.mute = true;
                if (source.isPlaying)
                    source.Stop();
            }
        }

        private void AuditAudioBeforeEnforcement()
        {
            if (!qaMode)
                return;

            AudioSource[] sources = FindObjectsByType<AudioSource>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            int playing = 0;
            int unmuted = 0;
            bool audibleRisk = AudioListener.volume > 0.0001f;
            foreach (AudioSource source in sources)
            {
                observedAudioSourceIds.Add(source.GetInstanceID());
                if (source.isPlaying)
                    playing++;
                if (!source.mute && source.volume > 0.0001f)
                    unmuted++;
                if (source.isPlaying && !source.mute && source.volume > 0.0001f &&
                    (AudioListener.volume > 0.0001f || source.ignoreListenerVolume))
                    audibleRisk = true;
            }

            // The global listener is forced to zero in Awake, before the first
            // rendered frame. Any non-zero sample here would mean audible output
            // was possible during the reviewed run.
            if (AudioListener.volume > 0.0001f)
                audioOutputViolationFrames++;
            if (audibleRisk)
                audioAudibleRiskViolationFrames++;
            maximumAudioSourcesObserved = Mathf.Max(maximumAudioSourcesObserved, sources.Length);
            maximumSourcesPlayingBeforeEnforcement = Mathf.Max(
                maximumSourcesPlayingBeforeEnforcement,
                playing);
            maximumSourcesUnmutedBeforeEnforcement = Mathf.Max(
                maximumSourcesUnmutedBeforeEnforcement,
                unmuted);
            audioAuditFrames++;
        }

        private static bool HasPostEnforcementAudioViolation()
        {
            if (AudioListener.volume > 0.0001f)
                return true;
            AudioSource[] sources = FindObjectsByType<AudioSource>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (AudioSource source in sources)
            {
                if (source.isPlaying || !source.mute)
                    return true;
            }
            return false;
        }

        private void OnGUI()
        {
            EvaluateRoute(motionClock, out _, out _, out int directionIndex);
            float phase = NormalizedPhase;
            int pose = Mathf.FloorToInt(phase * 6f) % 6;
            GUIStyle panel = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 18,
                normal = { textColor = Color.white }
            };
            GUI.Box(
                new Rect(18f, 18f, 540f, 118f),
                "FAMILY 3D MOTION LAB — ISOLATED / NOT PRODUCTION\n" +
                "SAME HUMANOID CLIP  |  T=0.99380799s  |  120.7477 steps/min\n" +
                "Direction " + DirectionNames[directionIndex] + "  |  P" + pose +
                "  |  phase " + phase.ToString("F3", CultureInfo.InvariantCulture) +
                "  |  " + SystemInfo.graphicsDeviceType + "\n" +
                "SPACE pause   R resync   O auto-orbit   ←/→ orbit   wheel zoom   ESC quit",
                panel);

            if (reviewCamera != null)
            {
                GUIStyle labelStyle = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 16,
                    fontStyle = FontStyle.Bold
                };
                foreach (Family3DWalkActor actor in actors)
                {
                    Vector3 screen = reviewCamera.WorldToScreenPoint(actor.transform.position + Vector3.up * 2.7f);
                    if (screen.z <= 0f)
                        continue;
                    labelStyle.normal.textColor = actor.LabelColor;
                    const float labelWidth = 150f;
                    float labelX = Mathf.Clamp(screen.x - labelWidth * 0.5f, 4f, Screen.width - labelWidth - 4f);
                    GUI.Box(new Rect(labelX, Screen.height - screen.y - 18f, labelWidth, 30f), actor.FamilyId, labelStyle);
                }
            }

            GUIStyle footer = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.LowerCenter,
                fontSize = 16,
                normal = { textColor = new Color(1f, 0.82f, 0.2f) }
            };
            GUI.Label(
                new Rect(0f, Screen.height - 34f, Screen.width, 28f),
                "VISUAL REVIEW REQUIRED: BOTH FEET • LOW BOUNCE • WHOLE-ROOT TURN • FOUR DIRECTIONS",
                footer);
        }

        private void ParseCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length; index++)
            {
                if (string.Equals(args[index], "-family3d-qa-output", StringComparison.OrdinalIgnoreCase) &&
                    index + 1 < args.Length)
                {
                    qaOutputDirectory = Path.GetFullPath(args[++index]);
                    qaMode = true;
                }
                else if (string.Equals(args[index], "-family3d-qa-seconds", StringComparison.OrdinalIgnoreCase) &&
                         index + 1 < args.Length &&
                         float.TryParse(args[++index], NumberStyles.Float, CultureInfo.InvariantCulture, out float duration))
                {
                    qaDurationSeconds = Mathf.Clamp(duration, RouteLoopSeconds + 0.25f, 60f);
                }
            }
        }

        private IEnumerator RunQaCapture()
        {
            string frameDirectory = Path.Combine(qaOutputDirectory, "frames");
            if (Directory.Exists(frameDirectory) ||
                File.Exists(Path.Combine(qaOutputDirectory, "qa-receipt.json")) ||
                File.Exists(Path.Combine(qaOutputDirectory, "frame-metadata.csv")))
            {
                Debug.LogError("FAMILY_3D_QA_FAIL: fresh QA output required: " + qaOutputDirectory);
                Application.Quit(3);
                yield break;
            }
            Directory.CreateDirectory(qaOutputDirectory);
            Directory.CreateDirectory(frameDirectory);
            Screen.SetResolution(1280, 720, false);
            float started = Time.realtimeSinceStartup;
            double startedMotionClock = motionClock;
            float nextCapture = started;
            const float captureInterval = 1f / 30f;
            while (motionClock - startedMotionClock < qaDurationSeconds)
            {
                yield return new WaitForEndOfFrame();
                float now = Time.realtimeSinceStartup;
                if (now + 0.0001f < nextCapture)
                    continue;
                EvaluateRoute(motionClock, out _, out _, out int directionIndex);
                float phase = NormalizedPhase;
                int pose = Mathf.FloorToInt(phase * 6f) % 6;
                if (qaFrame == 0)
                {
                    firstCapturedMotionClock = (float)motionClock;
                    firstCapturedDirection = DirectionNames[directionIndex];
                    firstCapturedPose = pose;
                    firstCapturedAtRouteStart = Mathf.Abs(firstCapturedMotionClock) <= 0.0001f &&
                                                directionIndex == 0 &&
                                                pose == 0 &&
                                                startupStateApplied &&
                                                startupMaximumPositionError <= 0.0001f &&
                                                startupMaximumYawErrorDegrees <= 0.0001f;
                }
                lastCapturedMotionClock = (float)motionClock;
                capturedDirectionFrameCounts[directionIndex]++;
                capturedDirectionPoseMasks[directionIndex] |= 1 << pose;
                string capturedDirection = DirectionNames[directionIndex];
                if (!string.Equals(previousCapturedDirection, capturedDirection, StringComparison.Ordinal))
                {
                    capturedDirectionTransitions.Add(capturedDirection);
                    previousCapturedDirection = capturedDirection;
                }
                frameMetadata.Add(string.Join(",", new[]
                {
                    qaFrame.ToString(CultureInfo.InvariantCulture),
                    motionClock.ToString("F9", CultureInfo.InvariantCulture),
                    DirectionNames[directionIndex],
                    pose.ToString(CultureInfo.InvariantCulture),
                    phase.ToString("F9", CultureInfo.InvariantCulture)
                }));
                string file = Path.Combine(frameDirectory, "frame_" + qaFrame.ToString("D4") + ".png");
                ScreenCapture.CaptureScreenshot(file, 1);
                qaFrame++;
                nextCapture += captureInterval;
            }

            qaCaptureActive = false;
            capturedMotionSeconds = (float)(motionClock - startedMotionClock);
            capturedWallClockSeconds = Time.realtimeSinceStartup - started;
            File.WriteAllLines(
                Path.Combine(qaOutputDirectory, "frame-metadata.csv"),
                new[] { "frame,motionClock,direction,pose,phase" }.Concat(frameMetadata));
            yield return new WaitForSecondsRealtime(1f);
            bool passed = WriteQaReceipt();
            Application.Quit(passed ? 0 : 2);
        }

        private bool WriteQaReceipt()
        {
            AudioSource[] audioSources = FindObjectsByType<AudioSource>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            int mutedAudioSources = 0;
            int playingAudioSources = 0;
            foreach (AudioSource source in audioSources)
            {
                if (source.mute || source.volume <= 0.0001f)
                    mutedAudioSources++;
                if (source.isPlaying)
                    playingAudioSources++;
            }
            bool finalAudioMute = AudioListener.volume <= 0.0001f &&
                                  mutedAudioSources == audioSources.Length &&
                                  playingAudioSources == 0;
            bool allDirectionsObserved = true;
            foreach (int count in directionSampleCounts)
                allDirectionsObserved &= count > 0;
            bool capturedDirectionsPass = true;
            for (var index = 0; index < capturedDirectionFrameCounts.Length; index++)
            {
                capturedDirectionsPass &= capturedDirectionFrameCounts[index] > 0;
                capturedDirectionsPass &= capturedDirectionPoseMasks[index] == 0x3f;
            }
            bool capturedDirectionSequencePass = capturedDirectionTransitions.Count >= DirectionNames.Length;
            for (var index = 0; index < DirectionNames.Length && index < capturedDirectionTransitions.Count; index++)
            {
                capturedDirectionSequencePass &= string.Equals(
                    capturedDirectionTransitions[index],
                    DirectionNames[index],
                    StringComparison.Ordinal);
            }
            float capturedMotionClockSpan = Mathf.Max(0f, lastCapturedMotionClock - firstCapturedMotionClock);
            bool capturedFullRouteLoop = capturedMotionClockSpan >= RouteLoopSeconds - 0.01f;
            bool audioPass = finalAudioMute &&
                             audioAuditFrames > 0 &&
                             audioOutputViolationFrames == 0 &&
                             audioAudibleRiskViolationFrames == 0 &&
                             audioPostEnforcementViolationFrames == 0;

            var actorReceipts = new ActorReceipt[accumulators.Count];
            bool actorMotionPass = actorReceipts.Length == 4;
            string sharedClipName = string.Empty;
            float sharedPhaseOffset = 0f;
            for (var index = 0; index < accumulators.Count; index++)
            {
                ActorReceipt actorReceipt = accumulators[index].ToReceipt(actors[index]);
                actorReceipts[index] = actorReceipt;
                if (index == 0)
                {
                    sharedClipName = actorReceipt.clipName;
                    sharedPhaseOffset = actorReceipt.phaseOffset;
                }
                actorMotionPass &= actorReceipt.leadFootAlternates &&
                                   actorReceipt.samples > 0 &&
                                   actorReceipt.maximumVisualRootPositionError <= 0.0001f &&
                                   actorReceipt.maximumVisualRootRotationErrorDegrees <= 0.0001f &&
                                   string.Equals(actorReceipt.clipName, sharedClipName, StringComparison.Ordinal) &&
                                   Mathf.Abs(actorReceipt.phaseOffset - sharedPhaseOffset) <= 0.000001f;
            }
            bool rootPass = startupStateApplied &&
                            startupMaximumPositionError <= 0.0001f &&
                            startupMaximumYawErrorDegrees <= 0.0001f &&
                            rootContinuityViolationFrames == 0 &&
                            maximumActorYawDivergenceDegrees <= 0.0001f;
            bool automaticGatesPass = SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11 &&
                                      firstCapturedAtRouteStart &&
                                      allDirectionsObserved &&
                                      capturedDirectionsPass &&
                                      capturedDirectionSequencePass &&
                                      capturedFullRouteLoop &&
                                      rootPass &&
                                      audioPass &&
                                      actorMotionPass;

            var receipt = new QaReceipt
            {
                contract = "FC-FAMILY-3D-MOTION-LAB-V3",
                status = automaticGatesPass
                    ? "AUTO_PASS_VISUAL_REVIEW_REQUIRED"
                    : "AUTO_FAIL_DO_NOT_DELIVER",
                automaticGatesPass = automaticGatesPass,
                graphicsDevice = SystemInfo.graphicsDeviceType.ToString(),
                graphicsDeviceName = SystemInfo.graphicsDeviceName,
                cycleSeconds = Family3DWalkActor.LockedCycleSeconds,
                stepsPerMinute = 120.7477f,
                movementSpeed = MovementSpeed,
                capturedFrames = qaFrame,
                capturedSeconds = capturedMotionSeconds,
                requestedMotionSeconds = qaDurationSeconds,
                capturedMotionSeconds = capturedMotionSeconds,
                capturedWallClockSeconds = capturedWallClockSeconds,
                bgmMuted = audioPass,
                audioListenerVolume = AudioListener.volume,
                audioSourcesFound = audioSources.Length,
                audioSourcesMuted = mutedAudioSources,
                audioSourcesPlaying = playingAudioSources,
                audioAuditFrames = audioAuditFrames,
                audioOutputViolationFrames = audioOutputViolationFrames,
                audioAudibleRiskViolationFrames = audioAudibleRiskViolationFrames,
                audioPostEnforcementViolationFrames = audioPostEnforcementViolationFrames,
                uniqueAudioSourcesObserved = observedAudioSourceIds.Count,
                maximumAudioSourcesObserved = maximumAudioSourcesObserved,
                maximumSourcesPlayingBeforeEnforcement = maximumSourcesPlayingBeforeEnforcement,
                maximumSourcesUnmutedBeforeEnforcement = maximumSourcesUnmutedBeforeEnforcement,
                directionOrder = DirectionNames,
                directionSampleCounts = directionSampleCounts,
                allDirectionsObserved = allDirectionsObserved,
                capturedDirectionFrameCounts = capturedDirectionFrameCounts,
                capturedDirectionPoseMasks = capturedDirectionPoseMasks,
                capturedDirectionTransitions = capturedDirectionTransitions.ToArray(),
                capturedDirectionsPass = capturedDirectionsPass,
                capturedDirectionSequencePass = capturedDirectionSequencePass,
                capturedMotionClockSpan = capturedMotionClockSpan,
                capturedFullRouteLoop = capturedFullRouteLoop,
                startupStateApplied = startupStateApplied,
                startupMaximumPositionError = startupMaximumPositionError,
                startupMaximumYawErrorDegrees = startupMaximumYawErrorDegrees,
                firstUpdateMaximumRootStep = firstUpdateMaximumRootStep,
                maximumRootStep = maximumRootStep,
                maximumRootStepExcess = maximumRootStepExcess,
                maximumRoutePositionError = maximumRoutePositionError,
                maximumRouteStepError = maximumRouteStepError,
                maximumActorYawDivergenceDegrees = maximumActorYawDivergenceDegrees,
                rootContinuityViolationFrames = rootContinuityViolationFrames,
                rootContinuityPass = rootPass,
                firstCapturedMotionClock = firstCapturedMotionClock,
                lastCapturedMotionClock = lastCapturedMotionClock,
                firstCapturedAtRouteStart = firstCapturedAtRouteStart,
                firstCapturedDirection = firstCapturedDirection,
                firstCapturedPose = firstCapturedPose,
                actorMotionPass = actorMotionPass,
                actors = actorReceipts
            };

            File.WriteAllText(
                Path.Combine(qaOutputDirectory, "qa-receipt.json"),
                JsonUtility.ToJson(receipt, true));
            return automaticGatesPass;
        }

        [Serializable]
        private sealed class QaReceipt
        {
            public string contract;
            public string status;
            public bool automaticGatesPass;
            public string graphicsDevice;
            public string graphicsDeviceName;
            public float cycleSeconds;
            public float stepsPerMinute;
            public float movementSpeed;
            public int capturedFrames;
            public float capturedSeconds;
            public float requestedMotionSeconds;
            public float capturedMotionSeconds;
            public float capturedWallClockSeconds;
            public bool bgmMuted;
            public float audioListenerVolume;
            public int audioSourcesFound;
            public int audioSourcesMuted;
            public int audioSourcesPlaying;
            public int audioAuditFrames;
            public int audioOutputViolationFrames;
            public int audioAudibleRiskViolationFrames;
            public int audioPostEnforcementViolationFrames;
            public int uniqueAudioSourcesObserved;
            public int maximumAudioSourcesObserved;
            public int maximumSourcesPlayingBeforeEnforcement;
            public int maximumSourcesUnmutedBeforeEnforcement;
            public string[] directionOrder;
            public int[] directionSampleCounts;
            public bool allDirectionsObserved;
            public int[] capturedDirectionFrameCounts;
            public int[] capturedDirectionPoseMasks;
            public string[] capturedDirectionTransitions;
            public bool capturedDirectionsPass;
            public bool capturedDirectionSequencePass;
            public float capturedMotionClockSpan;
            public bool capturedFullRouteLoop;
            public bool startupStateApplied;
            public float startupMaximumPositionError;
            public float startupMaximumYawErrorDegrees;
            public float firstUpdateMaximumRootStep;
            public float maximumRootStep;
            public float maximumRootStepExcess;
            public float maximumRoutePositionError;
            public float maximumRouteStepError;
            public float maximumActorYawDivergenceDegrees;
            public int rootContinuityViolationFrames;
            public bool rootContinuityPass;
            public float firstCapturedMotionClock;
            public float lastCapturedMotionClock;
            public bool firstCapturedAtRouteStart;
            public string firstCapturedDirection;
            public int firstCapturedPose;
            public bool actorMotionPass;
            public ActorReceipt[] actors;
        }

        [Serializable]
        private sealed class ActorReceipt
        {
            public string familyId;
            public string clipName;
            public float phaseOffset;
            public float standingHeight;
            public float averageP0LeftMinusRightLead;
            public float averageP3LeftMinusRightLead;
            public bool leadFootAlternates;
            public float pelvisPeakToPeak;
            public float pelvisPeakToPeakHeightRatio;
            public float minimumLeftFootHeight;
            public float minimumRightFootHeight;
            public float maximumVisualRootPositionError;
            public float maximumVisualRootRotationErrorDegrees;
            public int samples;
        }

        private sealed class ActorAccumulator
        {
            private readonly string familyId;
            private float minPelvis = float.PositiveInfinity;
            private float maxPelvis = float.NegativeInfinity;
            private float minLeftFoot = float.PositiveInfinity;
            private float minRightFoot = float.PositiveInfinity;
            private float p0LeadTotal;
            private float p3LeadTotal;
            private float maximumVisualRootPositionError;
            private float maximumVisualRootRotationErrorDegrees;
            private int p0Count;
            private int p3Count;
            private int samples;

            public ActorAccumulator(string id)
            {
                familyId = id;
            }

            public void Add(Family3DWalkActor.PoseSnapshot pose, float phase)
            {
                minPelvis = Mathf.Min(minPelvis, pose.hipsLocal.y);
                maxPelvis = Mathf.Max(maxPelvis, pose.hipsLocal.y);
                minLeftFoot = Mathf.Min(minLeftFoot, pose.leftFootLocal.y);
                minRightFoot = Mathf.Min(minRightFoot, pose.rightFootLocal.y);
                maximumVisualRootPositionError = Mathf.Max(
                    maximumVisualRootPositionError,
                    pose.visualRootPositionError);
                maximumVisualRootRotationErrorDegrees = Mathf.Max(
                    maximumVisualRootRotationErrorDegrees,
                    pose.visualRootRotationErrorDegrees);
                if (phase < 0.035f || phase > 0.965f)
                {
                    p0LeadTotal += pose.footLead;
                    p0Count++;
                }
                if (Mathf.Abs(phase - 0.5f) < 0.035f)
                {
                    p3LeadTotal += pose.footLead;
                    p3Count++;
                }
                samples++;
            }

            public ActorReceipt ToReceipt(Family3DWalkActor actor)
            {
                float p0 = p0Count > 0 ? p0LeadTotal / p0Count : 0f;
                float p3 = p3Count > 0 ? p3LeadTotal / p3Count : 0f;
                float bounce = maxPelvis - minPelvis;
                float height = Mathf.Max(actor.StandingHeight, 0.0001f);
                return new ActorReceipt
                {
                    familyId = familyId,
                    clipName = actor.WalkClip != null ? actor.WalkClip.name : string.Empty,
                    phaseOffset = actor.PhaseOffset,
                    standingHeight = height,
                    averageP0LeftMinusRightLead = p0,
                    averageP3LeftMinusRightLead = p3,
                    leadFootAlternates = p0 > 0f && p3 < 0f,
                    pelvisPeakToPeak = bounce,
                    pelvisPeakToPeakHeightRatio = bounce / height,
                    minimumLeftFootHeight = minLeftFoot,
                    minimumRightFootHeight = minRightFoot,
                    maximumVisualRootPositionError = maximumVisualRootPositionError,
                    maximumVisualRootRotationErrorDegrees = maximumVisualRootRotationErrorDegrees,
                    samples = samples
                };
            }
        }
    }
}
