using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FamilyCompany.Presentation.Unity;
using FamilyCompany.Presentation.Unity.OfficeGridView;
using FamilyCompany.Simulation.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace FamilyCompany.Editor
{
    /// <summary>
    /// Bakes all eight walk directions from a single downloaded humanoid rig and a single walk
    /// clip, by yawing the model 45 degrees per direction in front of a fixed office-angle camera.
    /// The walk cycle is never authored here; it arrives from Mixamo or Quaternius. Output is the
    /// same PNG + receipt pair <see cref="PlayerBakedWalkV2CatalogBuilder"/> already consumes, so
    /// the runtime, the catalog, and the dot look are unchanged.
    /// </summary>
    public static class PlayerWalkHumanoidBaker
    {
        public const string ContractPath = "ArtSources/PlayerWalkHumanoid/humanoid-contract.json";
        public const string RequiredContract = "FC-PLAYER-WALK-HUMANOID-V1";

        // IsometricCameraFollow frames the office from officeOffset (0, 13.5, -13.5) toward
        // officeCenter + up * officeLookHeight (0.6). That is a yaw-free view down +Z, pitched by
        // atan((13.5 - 0.6) / 13.5). Characters must be rendered at the same pitch or their
        // foreshortening will not match the floor they stand on.
        public const float OfficeCameraHeight = 13.5f;
        public const float OfficeCameraDepth = 13.5f;
        public const float OfficeCameraLookHeight = 0.6f;

        // Direction 0 (south) faces -Z, straight at the camera. DirectionVector(d) in
        // OfficeSharedLocomotionRules is (-sin(d*45), -cos(d*45)), so the model yaw that points a
        // +Z-forward humanoid along direction d is 180 - d * 45 degrees.
        public const float SouthModelYawDegrees = 180f;

        // Sample density used to find foot contacts and to integrate root travel. One sample per
        // ~1.4ms of a one-second clip is far finer than the eight poses we keep.
        private const int PhaseSampleCount = 720;
        public const float DefaultFeetViewportY = 0.14f;
        public const float DefaultTargetVisibleHeightPx = 380f;
        // Deliberately NOT the V2 frame root. Both bakers target the same eight direction
        // names, so sharing a folder means whichever ran last silently wins.
        public const string DefaultOutputRoot =
            "Assets/Resources/FamilyCompany/PlayerBakedWalkHumanoidV1/";

        private static readonly Vector2[] DirectionVectors =
        {
            Vector2.down,
            new Vector2(-1f, -1f).normalized,
            Vector2.left,
            new Vector2(-1f, 1f).normalized,
            Vector2.up,
            new Vector2(1f, 1f).normalized,
            Vector2.right,
            new Vector2(1f, -1f).normalized
        };

        [MenuItem("Family Company/Art/Bake Player Walk From Humanoid Rig")]
        public static void Run()
        {
            Bake(LoadContract(ContractPath));
        }

        public static void RunFromCommandLine()
        {
            try
            {
                Run();
                Debug.Log("PLAYER_WALK_HUMANOID_BAKER: PASS");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("PLAYER_WALK_HUMANOID_BAKER: FAIL | " + exception.Message);
                EditorApplication.Exit(1);
            }
        }

        public static float OfficeCameraPitchDegrees =>
            Mathf.Atan2(OfficeCameraHeight - OfficeCameraLookHeight, OfficeCameraDepth) *
            Mathf.Rad2Deg;

        /// <summary>
        /// World distance one full walk cycle must cover in bake space. The runtime stride is
        /// authored in office units and the sprite is displayed at <see cref="OfficeGridCharacterMover.UniformVisualScale"/>,
        /// so the rig has to be scaled until its own cycle covers exactly this much.
        /// </summary>
        public static float RequiredCycleWorldDistance =>
            OfficeLocomotionGaitRules.DefaultStrideLength / OfficeGridCharacterMover.UniformVisualScale;

        public static float ModelYawDegrees(int direction)
        {
            if (direction < 0 || direction >= PlayerBakedWalkCatalogV2.DirectionCount)
                throw new ArgumentOutOfRangeException(nameof(direction));
            return SouthModelYawDegrees - direction * 45f;
        }

        internal static void Bake(PlayerWalkHumanoidBakeContract contract)
        {
            ValidateContract(contract);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(contract.rigPrefabPath);
            if (prefab == null)
                throw new InvalidOperationException("Humanoid rig prefab is missing: " + contract.rigPrefabPath);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(contract.walkClipPath);
            if (clip == null)
                throw new InvalidOperationException("Walk clip is missing: " + contract.walkClipPath);
            if (clip.length <= 0f)
                throw new InvalidOperationException("Walk clip length must be positive.");

            Scene previewScene = SceneManager.GetActiveScene();
            GameObject instance = null;
            GameObject cameraHost = null;
            RenderTexture target = null;
            Texture2D pixels = null;
            PlayerWalkCanonicalVisualBuilder.Handle canonicalVisual = null;
            try
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, previewScene);
                if (instance == null)
                    throw new InvalidOperationException("Could not instantiate the humanoid rig prefab.");
                RequireHumanoidRig(instance);

                Transform hips = FindRequired(instance.transform, contract.hipsTransform);
                Transform leftFoot = FindRequired(instance.transform, contract.leftFootTransform);
                Transform rightFoot = FindRequired(instance.transform, contract.rightFootTransform);
                Transform head = FindRequired(instance.transform, contract.headTransform);

                if (string.Equals(
                        contract.visualPreset,
                        PlayerWalkCanonicalVisualBuilder.PresetId,
                        StringComparison.Ordinal))
                    canonicalVisual = PlayerWalkCanonicalVisualBuilder.Attach(instance);

                AnimationMode.StartAnimationMode();
                try
                {
                    WalkCyclePlan plan = PlanCycle(instance, clip, hips, leftFoot, rightFoot, contract);
                    cameraHost = new GameObject("PlayerWalkHumanoidBakeCamera");
                    SceneManager.MoveGameObjectToScene(cameraHost, previewScene);
                    Camera camera = cameraHost.AddComponent<Camera>();
                    ConfigureCamera(camera, contract);

                    plan.RequiredUniformScale = ResolveUniformScale(
                        instance,
                        clip,
                        plan,
                        camera,
                        hips,
                        contract,
                        ModelYawDegrees(0));
                    ApplyUniformScale(instance, plan.RequiredUniformScale);
                    Debug.Log(
                        "PLAYER_WALK_HUMANOID_BAKER: plan | " +
                        $"cycleSeconds={plan.CycleSeconds:F4} clipCycleDistance={plan.MeasuredCycleDistance:F5} " +
                        $"requiredDistance={RequiredCycleWorldDistance:F5} scale={plan.RequiredUniformScale:F5} " +
                        $"targetHeightPx={ResolveTargetVisibleHeightPx(contract):F1} " +
                        $"leftPlantTime={plan.LeftPlantStartSeconds:F4} pitch={OfficeCameraPitchDegrees:F4}");

                    target = CreateTarget(contract);
                    camera.targetTexture = target;
                    pixels = CreateReadback(contract);

                    for (var direction = 0; direction < PlayerBakedWalkCatalogV2.DirectionCount; direction++)
                        BakeDirection(
                            direction,
                            contract,
                            instance,
                            clip,
                            plan,
                            camera,
                            target,
                            pixels,
                            hips,
                            leftFoot,
                            rightFoot,
                            head);

                    string outputRoot = ResolveOutputRoot(contract);
                    PlayerBakedWalkV2Validation.ValidateHumanoidDirectionSet(outputRoot);
                }
                finally
                {
                    AnimationMode.StopAnimationMode();
                }

                Debug.Log(
                    "PLAYER_WALK_HUMANOID_BAKER: PASS | directions=8 poses=64 " +
                    $"canvas={contract.canvasWidth}x{contract.canvasHeight}");
            }
            finally
            {
                if (target != null)
                {
                    target.Release();
                    Object.DestroyImmediate(target);
                }
                if (pixels != null) Object.DestroyImmediate(pixels);
                if (cameraHost != null) Object.DestroyImmediate(cameraHost);
                if (instance != null) Object.DestroyImmediate(instance);
                canonicalVisual?.Dispose();
            }
        }

        private static void BakeDirection(
            int direction,
            PlayerWalkHumanoidBakeContract contract,
            GameObject instance,
            AnimationClip clip,
            WalkCyclePlan plan,
            Camera camera,
            RenderTexture target,
            Texture2D pixels,
            Transform hips,
            Transform leftFoot,
            Transform rightFoot,
            Transform head)
        {
            string directionName = PlayerBakedWalkCatalogV2.DirectionNames[direction];
            string outputRoot = ResolveOutputRoot(contract);
            string outputDirectory = NormalizeAssetPath(Path.Combine(
                outputRoot + "Frames/",
                directionName));
            Directory.CreateDirectory(outputDirectory);

            var receipt = new PlayerBakedWalkV2BakeReceipt
            {
                direction = directionName,
                canvasWidth = contract.canvasWidth,
                canvasHeight = contract.canvasHeight,
                pixelsPerUnit = contract.pixelsPerUnit,
                strideWorld = OfficeLocomotionGaitRules.DefaultStrideLength,
                visualScale = OfficeGridCharacterMover.UniformVisualScale,
                validationProfile = PlayerBakedWalkV2Validation.HumanoidValidationProfile,
                sourcePsbPath = contract.walkClipPath,
                sourcePsbSha256 = Sha256(contract.rigPrefabPath),
                poses = new PlayerBakedWalkV2BakePoseReceipt[PlayerBakedWalkCatalogV2.PoseCount]
            };

            float yaw = ModelYawDegrees(direction);
            float directionUniformScale = contract.uniformScaleOverride > 0f
                ? plan.RequiredUniformScale
                : ResolveUniformScale(
                    instance,
                    clip,
                    plan,
                    camera,
                    hips,
                    contract,
                    yaw);
            Vector2 heading = DirectionVectors[direction];
            float rootStepPx = receipt.strideWorld / PlayerBakedWalkCatalogV2.PoseCount *
                               receipt.pixelsPerUnit / receipt.visualScale;
            float feetViewportY = contract.feetViewportY > 0f
                ? contract.feetViewportY
                : DefaultFeetViewportY;
            var contactCenterPx = new Vector2(
                contract.canvasWidth * 0.5f,
                contract.canvasHeight * feetViewportY);
            for (var pose = 0; pose < PlayerBakedWalkCatalogV2.PoseCount; pose++)
            {
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                AnimationMode.SampleAnimationClip(instance, clip, plan.PoseSampleSeconds[pose]);

                // SampleAnimationClip writes the root transform itself, so the direction yaw must be
                // composed on top of the sampled pose. Setting it beforehand is silently discarded
                // and renders all eight directions identically.
                ApplyUniformScale(instance, directionUniformScale);
                instance.transform.RotateAround(Vector3.zero, Vector3.up, yaw);

                // The catalog stores in-place poses; the runtime advances the root itself. Drop the
                // clip's own planar travel so every pose shares one origin, exactly as the V2 baker
                // does, then measure the anchors the foot-lock validation will re-expand.
                Vector3 sampledRoot = hips.position;
                var rootPlanar = new Vector3(sampledRoot.x, 0f, sampledRoot.z);
                instance.transform.position -= rootPlanar;

                Vector2 leftAnchor = ToCanvasPx(camera, leftFoot.position, contract);
                Vector2 rightAnchor = ToCanvasPx(camera, rightFoot.position, contract);
                Vector2 pelvisAnchor = ToCanvasPx(camera, hips.position, contract);
                Vector2 headAnchor = ToCanvasPx(camera, head.position, contract);

                Render(camera, target, pixels, contract);
                ForceHardAlpha(pixels);

                // Visual size and runtime stride are independent. Align the sampled support foot
                // to the exact in-place trajectory expected by the runtime instead of shrinking
                // the whole character until the clip's authored root travel happens to match it.
                Vector2 supportAnchor = pose < 4 ? leftAnchor : rightAnchor;
                float supportPhase = pose % 4 - 1.5f;
                Vector2 targetSupport = contactCenterPx - heading * (rootStepPx * supportPhase);
                var pixelShift = new Vector2Int(
                    Mathf.RoundToInt(targetSupport.x - supportAnchor.x),
                    Mathf.RoundToInt(targetSupport.y - supportAnchor.y));
                TranslatePixels(pixels, pixelShift);
                Vector2 appliedShift = pixelShift;
                leftAnchor += appliedShift;
                rightAnchor += appliedShift;
                pelvisAnchor += appliedShift;
                headAnchor += appliedShift;

                RequireAnchorInside(leftAnchor, contract, "leftFoot", directionName, pose);
                RequireAnchorInside(rightAnchor, contract, "rightFoot", directionName, pose);
                RequireAnchorInside(pelvisAnchor, contract, "pelvis", directionName, pose);
                RequireAnchorInside(
                    headAnchor,
                    contract,
                    "head",
                    directionName,
                    pose);

                string outputPath = NormalizeAssetPath(Path.Combine(
                    outputDirectory,
                    $"player_{directionName}_walk_{pose}_v2.png"));
                File.WriteAllBytes(outputPath, pixels.EncodeToPNG());
                receipt.poses[pose] = new PlayerBakedWalkV2BakePoseReceipt
                {
                    pose = pose,
                    supportLeg = pose < 4 ? "left" : "right",
                    spritePath = outputPath,
                    rootWorld = new Vector2(sampledRoot.x, sampledRoot.z),
                    leftFootAnchorPx = leftAnchor,
                    rightFootAnchorPx = rightAnchor,
                    pelvisAnchorPx = pelvisAnchor,
                    leftFootHeightWorld = leftFoot.position.y,
                    rightFootHeightWorld = rightFoot.position.y
                };
            }

            string receiptPath = NormalizeAssetPath(
                outputRoot + "source-receipt-" + directionName + ".json");
            File.WriteAllText(receiptPath, JsonUtility.ToJson(receipt, true));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(receiptPath, ImportAssetOptions.ForceSynchronousImport);
            PlayerBakedWalkV2Validation.ValidateHumanoidReceiptAndPngs(receipt, direction);
            Debug.Log($"PLAYER_WALK_HUMANOID_BAKER: direction={directionName} poses=8 OK");
        }

        // ---------------------------------------------------------------- cycle planning

        private sealed class WalkCyclePlan
        {
            public float CycleSeconds;
            public float MeasuredCycleDistance;
            public float RequiredUniformScale;
            public float LeftPlantStartSeconds;
            public float[] PoseSampleSeconds = Array.Empty<float>();
        }

        /// <summary>
        /// A downloaded clip starts at an arbitrary phase and its root does not advance uniformly
        /// in time. The catalog demands the opposite on both counts: poses 0-3 must stand on the
        /// left foot, and the support foot must appear locked once a uniform stride/8 is added back.
        /// So find the left-foot plant, then resample by equal root travel rather than equal time.
        /// </summary>
        private static WalkCyclePlan PlanCycle(
            GameObject instance,
            AnimationClip clip,
            Transform hips,
            Transform leftFoot,
            Transform rightFoot,
            PlayerWalkHumanoidBakeContract contract)
        {
            float cycleSeconds = contract.cycleSeconds > 0f
                ? Mathf.Min(contract.cycleSeconds, clip.length)
                : clip.length;
            var times = new float[PhaseSampleCount];
            var leftWorld = new Vector3[PhaseSampleCount];
            var rightWorld = new Vector3[PhaseSampleCount];
            var travel = new float[PhaseSampleCount];

            Vector3 previousRoot = Vector3.zero;
            for (var index = 0; index < PhaseSampleCount; index++)
            {
                float time = cycleSeconds * index / (PhaseSampleCount - 1);
                times[index] = time;
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                AnimationMode.SampleAnimationClip(instance, clip, time);
                Vector3 root = hips.position;
                leftWorld[index] = leftFoot.position;
                rightWorld[index] = rightFoot.position;
                if (index == 0)
                {
                    travel[index] = 0f;
                }
                else
                {
                    var step = new Vector3(root.x - previousRoot.x, 0f, root.z - previousRoot.z);
                    travel[index] = travel[index - 1] + step.magnitude;
                }
                previousRoot = root;
            }

            float measured = travel[PhaseSampleCount - 1];
            if (measured <= 0.0001f)
                throw new InvalidOperationException(
                    "Walk clip has no root travel. Export it with root motion, or set " +
                    "cycleDistanceOverride in the contract so the stride can be reconstructed.");
            if (contract.cycleDistanceOverride > 0f) measured = contract.cycleDistanceOverride;

            int plantIndex = FindLeftPlantStart(leftWorld, rightWorld);
            float requiredScale = RequiredCycleWorldDistance / measured;
            if (contract.uniformScaleOverride > 0f) requiredScale = contract.uniformScaleOverride;

            var poseSeconds = new float[PlayerBakedWalkCatalogV2.PoseCount];
            for (var pose = 0; pose < poseSeconds.Length; pose++)
            {
                float fraction = pose / (float)PlayerBakedWalkCatalogV2.PoseCount;
                float wantedTravel = travel[plantIndex] + measured * fraction;
                poseSeconds[pose] = ResolveTimeAtTravel(times, travel, wantedTravel, cycleSeconds);
            }

            return new WalkCyclePlan
            {
                CycleSeconds = cycleSeconds,
                MeasuredCycleDistance = measured,
                RequiredUniformScale = requiredScale,
                LeftPlantStartSeconds = times[plantIndex],
                PoseSampleSeconds = poseSeconds
            };
        }

        /// <summary>
        /// The planted foot is the one standing still in the world while the other one travels.
        /// Ankle height cannot decide this: at push-off the support ankle is raised, so a
        /// lowest-ankle test selects the swing phase instead and puts the whole cycle half a
        /// period out, which reads as hopping on one leg.
        /// </summary>
        private static int FindLeftPlantStart(Vector3[] leftWorld, Vector3[] rightWorld)
        {
            // Index 0 and the last index are the same phase of a looping clip; scanning the
            // duplicate would let a run wrap through itself.
            int count = leftWorld.Length - 1;
            if (count < 8)
                throw new InvalidOperationException("Walk clip has too few phase samples.");

            var leftIsSlower = new bool[count];
            var anyMotion = false;
            for (var index = 0; index < count; index++)
            {
                int next = (index + 1) % count;
                float leftStep = HorizontalDistance(leftWorld[index], leftWorld[next]);
                float rightStep = HorizontalDistance(rightWorld[index], rightWorld[next]);
                leftIsSlower[index] = leftStep < rightStep;
                if (leftStep > 0.000001f || rightStep > 0.000001f) anyMotion = true;
            }
            if (!anyMotion)
                throw new InvalidOperationException(
                    "Walk clip never moves either foot. This is not a walk cycle.");

            int bestStart = -1;
            int bestLength = 0;
            for (var index = 0; index < count; index++)
            {
                // Only a run boundary can begin the support phase; otherwise every sample inside
                // one run offers its own shorter tail as a candidate.
                if (!leftIsSlower[index] || leftIsSlower[(index - 1 + count) % count]) continue;
                var length = 0;
                while (length < count && leftIsSlower[(index + length) % count]) length++;
                if (length > bestLength)
                {
                    bestLength = length;
                    bestStart = index;
                }
            }
            if (bestStart < 0)
                throw new InvalidOperationException(
                    "Could not find a left-foot support phase in the walk clip. The catalog " +
                    "requires poses 0-3 to stand on the left foot.");
            return bestStart;
        }

        private static float HorizontalDistance(Vector3 from, Vector3 to)
        {
            float dx = to.x - from.x;
            float dz = to.z - from.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static float ResolveTimeAtTravel(
            float[] times,
            float[] travel,
            float wantedTravel,
            float cycleSeconds)
        {
            float total = travel[travel.Length - 1];
            float wrapped = wantedTravel;
            var laps = 0;
            while (wrapped > total && laps < 4)
            {
                wrapped -= total;
                laps++;
            }
            for (var index = 1; index < travel.Length; index++)
            {
                if (travel[index] < wrapped) continue;
                float segment = travel[index] - travel[index - 1];
                float blend = segment <= 0.0000001f
                    ? 0f
                    : (wrapped - travel[index - 1]) / segment;
                return Mathf.Repeat(
                    Mathf.Lerp(times[index - 1], times[index], blend),
                    cycleSeconds);
            }
            return Mathf.Repeat(times[times.Length - 1], cycleSeconds);
        }

        // ---------------------------------------------------------------- rendering

        private static void ConfigureCamera(Camera camera, PlayerWalkHumanoidBakeContract contract)
        {
            camera.orthographic = true;
            camera.orthographicSize = contract.canvasHeight / (2f * contract.pixelsPerUnit);
            camera.aspect = contract.canvasWidth / (float)contract.canvasHeight;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 100f;
            camera.enabled = false;
            camera.transform.rotation = Quaternion.Euler(OfficeCameraPitchDegrees, 0f, 0f);
            camera.transform.position = -camera.transform.forward * 10f;

            // Orthographic projection is linear, so one solve places the character's ground plane
            // at the bottom of the canvas and centres it horizontally.
            Vector3 viewport = camera.WorldToViewportPoint(Vector3.zero);
            float worldWidth = 2f * camera.orthographicSize * camera.aspect;
            float worldHeight = 2f * camera.orthographicSize;
            float feetViewportY = contract.feetViewportY > 0f
                ? contract.feetViewportY
                : DefaultFeetViewportY;
            camera.transform.position +=
                camera.transform.right * ((viewport.x - 0.5f) * worldWidth) +
                camera.transform.up * ((viewport.y - feetViewportY) * worldHeight);
        }

        private static RenderTexture CreateTarget(PlayerWalkHumanoidBakeContract contract)
        {
            var target = new RenderTexture(
                contract.canvasWidth,
                contract.canvasHeight,
                24,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB)
            {
                antiAliasing = 1,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            target.Create();
            return target;
        }

        private static Texture2D CreateReadback(PlayerWalkHumanoidBakeContract contract) =>
            new Texture2D(contract.canvasWidth, contract.canvasHeight, TextureFormat.RGBA32, false, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

        private static void Render(
            Camera camera,
            RenderTexture target,
            Texture2D pixels,
            PlayerWalkHumanoidBakeContract contract)
        {
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(
                    new Rect(0f, 0f, contract.canvasWidth, contract.canvasHeight),
                    0,
                    0,
                    false);
                pixels.Apply(false, false);
            }
            finally
            {
                RenderTexture.active = previous;
            }
        }

        private static void TranslatePixels(Texture2D pixels, Vector2Int shift)
        {
            if (shift == Vector2Int.zero) return;
            int width = pixels.width;
            int height = pixels.height;
            Color32[] source = pixels.GetPixels32();
            var destination = new Color32[source.Length];
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                int targetX = x + shift.x;
                int targetY = y + shift.y;
                if (targetX < 0 || targetX >= width || targetY < 0 || targetY >= height) continue;
                destination[targetY * width + targetX] = source[y * width + x];
            }
            pixels.SetPixels32(destination);
            pixels.Apply(false, false);
        }

        private static float ResolveUniformScale(
            GameObject instance,
            AnimationClip clip,
            WalkCyclePlan plan,
            Camera camera,
            Transform hips,
            PlayerWalkHumanoidBakeContract contract,
            float yawDegrees)
        {
            if (contract.uniformScaleOverride > 0f) return contract.uniformScaleOverride;

            float targetHeightPx = ResolveTargetVisibleHeightPx(contract);
            float measuredAtUnitScale = MeasureMedianVisibleHeightPxAtUnitScale(
                instance,
                clip,
                plan,
                camera,
                hips,
                contract,
                yawDegrees);
            if (measuredAtUnitScale <= 0.001f)
                throw new InvalidOperationException(
                    "Humanoid rig has no measurable rendered height at unit scale.");
            return targetHeightPx / measuredAtUnitScale;
        }

        private static float MeasureMedianVisibleHeightPxAtUnitScale(
            GameObject instance,
            AnimationClip clip,
            WalkCyclePlan plan,
            Camera camera,
            Transform hips,
            PlayerWalkHumanoidBakeContract contract,
            float yaw)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy)
                .ToArray();
            if (renderers.Length == 0)
                throw new InvalidOperationException("Humanoid rig has no enabled renderer to measure.");

            var heights = new List<float>(PlayerBakedWalkCatalogV2.PoseCount);
            for (var pose = 0; pose < PlayerBakedWalkCatalogV2.PoseCount; pose++)
            {
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                AnimationMode.SampleAnimationClip(instance, clip, plan.PoseSampleSeconds[pose]);
                ApplyUniformScale(instance, 1f);
                instance.transform.RotateAround(Vector3.zero, Vector3.up, yaw);
                Vector3 sampledRoot = hips.position;
                instance.transform.position -= new Vector3(sampledRoot.x, 0f, sampledRoot.z);

                float minimumY = float.PositiveInfinity;
                float maximumY = float.NegativeInfinity;
                foreach (Renderer renderer in renderers)
                {
                    Bounds bounds = renderer.bounds;
                    Vector3 center = bounds.center;
                    Vector3 extents = bounds.extents;
                    for (var corner = 0; corner < 8; corner++)
                    {
                        var world = new Vector3(
                            center.x + ((corner & 1) == 0 ? -extents.x : extents.x),
                            center.y + ((corner & 2) == 0 ? -extents.y : extents.y),
                            center.z + ((corner & 4) == 0 ? -extents.z : extents.z));
                        float y = camera.WorldToViewportPoint(world).y * contract.canvasHeight;
                        minimumY = Mathf.Min(minimumY, y);
                        maximumY = Mathf.Max(maximumY, y);
                    }
                }
                heights.Add(maximumY - minimumY);
            }
            heights.Sort();
            int middle = heights.Count / 2;
            return heights.Count % 2 == 0
                ? (heights[middle - 1] + heights[middle]) * 0.5f
                : heights[middle];
        }

        private static float ResolveTargetVisibleHeightPx(PlayerWalkHumanoidBakeContract contract) =>
            contract.targetVisibleHeightPx > 0f
                ? contract.targetVisibleHeightPx
                : DefaultTargetVisibleHeightPx;

        private static Vector2 ToCanvasPx(
            Camera camera,
            Vector3 world,
            PlayerWalkHumanoidBakeContract contract)
        {
            Vector3 viewport = camera.WorldToViewportPoint(world);
            return new Vector2(viewport.x * contract.canvasWidth, viewport.y * contract.canvasHeight);
        }

        /// <summary>
        /// The catalog forbids partial alpha entirely, so a rendered edge either belongs to the
        /// character or it does not exist. Half-covered pixels are resolved by majority coverage.
        /// </summary>
        private static void ForceHardAlpha(Texture2D pixels)
        {
            Color32[] data = pixels.GetPixels32();
            for (var index = 0; index < data.Length; index++)
            {
                Color32 current = data[index];
                if (current.a == 0 || current.a == 255) continue;
                current.a = current.a >= 128 ? (byte)255 : (byte)0;
                data[index] = current;
            }
            pixels.SetPixels32(data);
            pixels.Apply(false, false);
        }

        // ---------------------------------------------------------------- validation

        private static void ValidateContract(PlayerWalkHumanoidBakeContract contract)
        {
            if (contract == null) throw new ArgumentNullException(nameof(contract));
            if (!string.Equals(contract.contract, RequiredContract, StringComparison.Ordinal))
                throw new InvalidOperationException("Humanoid walk contract ID is invalid.");
            if (contract.canvasWidth <= 0 || contract.canvasHeight <= 0)
                throw new InvalidOperationException("Humanoid walk canvas is invalid.");
            if (Mathf.Abs(contract.pixelsPerUnit - PlayerBakedWalkV2TextureImporter.PixelsPerUnit) > 0.001f)
                throw new InvalidOperationException(
                    "Humanoid walk PPU must match the V2 importer " +
                    PlayerBakedWalkV2TextureImporter.PixelsPerUnit + ".");
            if (string.IsNullOrWhiteSpace(contract.rigPrefabPath))
                throw new InvalidOperationException("Humanoid walk contract has no rig prefab path.");
            if (string.IsNullOrWhiteSpace(contract.walkClipPath))
                throw new InvalidOperationException("Humanoid walk contract has no walk clip path.");
            if (!string.IsNullOrWhiteSpace(contract.visualPreset) &&
                !string.Equals(
                    contract.visualPreset,
                    PlayerWalkCanonicalVisualBuilder.PresetId,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Unknown humanoid walk visual preset: " + contract.visualPreset);
            float targetHeight = ResolveTargetVisibleHeightPx(contract);
            if (targetHeight < 64f || targetHeight > contract.canvasHeight - 16f)
                throw new InvalidOperationException(
                    $"Humanoid target visible height {targetHeight:F1}px is outside the safe canvas range.");
        }

        private static void RequireHumanoidRig(GameObject instance)
        {
            var animator = instance.GetComponentInChildren<Animator>(true);
            if (animator == null)
                throw new InvalidOperationException(
                    "Humanoid rig has no Animator. Import the FBX with Animation Type = Humanoid.");
            if (animator.avatar == null || !animator.avatar.isValid)
                throw new InvalidOperationException(
                    "Humanoid rig Avatar is missing or invalid. Configure the Avatar in the FBX importer.");
            if (!animator.avatar.isHuman)
                throw new InvalidOperationException(
                    "Humanoid rig Avatar is not a Humanoid. Generic rigs cannot be retargeted here.");
            if (instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length == 0)
                throw new InvalidOperationException("Humanoid rig has no SkinnedMeshRenderer.");
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material == null || !material.HasProperty("_MainTex")) continue;
                var texture = material.GetTexture("_MainTex") as Texture2D;
                if (texture != null && texture.filterMode != FilterMode.Point)
                    throw new InvalidOperationException(
                        "Humanoid rig texture " + texture.name +
                        " must use Point filtering to keep the dot look.");
            }
        }

        private static void ApplyUniformScale(GameObject instance, float scale)
        {
            if (scale <= 0f || float.IsNaN(scale) || float.IsInfinity(scale))
                throw new InvalidOperationException($"Humanoid rig uniform scale {scale} is invalid.");
            instance.transform.localScale = Vector3.one * scale;
        }

        private static void RequireAnchorInside(
            Vector2 anchor,
            PlayerWalkHumanoidBakeContract contract,
            string label,
            string direction,
            int pose)
        {
            if (float.IsNaN(anchor.x) || float.IsNaN(anchor.y) ||
                anchor.x < 0f || anchor.x > contract.canvasWidth ||
                anchor.y < 0f || anchor.y > contract.canvasHeight)
                throw new InvalidOperationException(
                    $"Humanoid walk {direction} pose {pose} {label} anchor {anchor} left the " +
                    $"{contract.canvasWidth}x{contract.canvasHeight} canvas. Lower " +
                    "uniformScaleOverride or widen the canvas.");
        }

        // ---------------------------------------------------------------- helpers

        private static PlayerWalkHumanoidBakeContract LoadContract(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("Humanoid walk contract is missing.", path);
            var contract = JsonUtility.FromJson<PlayerWalkHumanoidBakeContract>(File.ReadAllText(path));
            if (contract == null)
                throw new InvalidOperationException("Humanoid walk contract JSON is invalid.");
            return contract;
        }

        private static Transform FindRequired(Transform root, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("Humanoid walk contract has an empty bone name.");
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
                if (string.Equals(candidate.name, name, StringComparison.Ordinal))
                    return candidate;
            throw new InvalidOperationException(
                $"Humanoid rig has no transform named '{name}'. Mixamo names are prefixed " +
                "mixamorig:, for example mixamorig:Hips and mixamorig:LeftFoot.");
        }

        private static string NormalizeAssetPath(string path) => path.Replace('\\', '/');

        private static string ResolveOutputRoot(PlayerWalkHumanoidBakeContract contract) =>
            string.IsNullOrWhiteSpace(contract.outputRoot)
                ? DefaultOutputRoot
                : NormalizeAssetPath(contract.outputRoot.TrimEnd('/')) + "/";

        private static string Sha256(string path)
        {
            using var algorithm = System.Security.Cryptography.SHA256.Create();
            using FileStream stream = File.OpenRead(path);
            return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", string.Empty);
        }
    }

    [Serializable]
    internal sealed class PlayerWalkHumanoidBakeContract
    {
        public string contract = string.Empty;
        public string rigPrefabPath = string.Empty;
        public string walkClipPath = string.Empty;
        public string hipsTransform = "mixamorig:Hips";
        public string leftFootTransform = "mixamorig:LeftFoot";
        public string rightFootTransform = "mixamorig:RightFoot";
        public string headTransform = "mixamorig:Head";
        public int canvasWidth = 512;
        public int canvasHeight = 512;
        public float pixelsPerUnit = 324f;

        /// <summary>Length of one full two-step cycle. Zero uses the whole clip.</summary>
        public float cycleSeconds;

        /// <summary>Root travel over one cycle, when the clip was exported In Place.</summary>
        public float cycleDistanceOverride;

        /// <summary>Bypasses the measured stride-derived scale when a rig needs hand tuning.</summary>
        public float uniformScaleOverride;

        /// <summary>
        /// Median rendered silhouette height in pixels. This is deliberately independent from the
        /// animation clip's root travel; runtime stride is enforced by support-foot alignment.
        /// </summary>
        public float targetVisibleHeightPx;

        /// <summary>Optional closed-volume costume attached to the downloaded Mixamo skeleton.</summary>
        public string visualPreset = string.Empty;

        /// <summary>
        /// Where the rig's ground plane sits in the canvas, 0..1 from the bottom. A pitched
        /// camera projects the leading foot below that plane, so the default leaves room.
        /// Zero uses <see cref="PlayerWalkHumanoidBaker.DefaultFeetViewportY"/>.
        /// </summary>
        public float feetViewportY;

        /// <summary>
        /// Where frames and receipts are written. Empty uses
        /// <see cref="PlayerWalkHumanoidBaker.DefaultOutputRoot"/>, which is intentionally
        /// separate from the V2 paper-doll root.
        /// </summary>
        public string outputRoot = string.Empty;

    }
}
