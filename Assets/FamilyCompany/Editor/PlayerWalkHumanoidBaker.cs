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
        private const float FeetViewportY = 0.03f;

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

                AnimationMode.StartAnimationMode();
                try
                {
                    WalkCyclePlan plan = PlanCycle(instance, clip, hips, leftFoot, rightFoot, contract);
                    ApplyUniformScale(instance, plan.RequiredUniformScale);
                    Debug.Log(
                        "PLAYER_WALK_HUMANOID_BAKER: plan | " +
                        $"cycleSeconds={plan.CycleSeconds:F4} clipCycleDistance={plan.MeasuredCycleDistance:F5} " +
                        $"requiredDistance={RequiredCycleWorldDistance:F5} scale={plan.RequiredUniformScale:F5} " +
                        $"leftPlantTime={plan.LeftPlantStartSeconds:F4} pitch={OfficeCameraPitchDegrees:F4}");

                    cameraHost = new GameObject("PlayerWalkHumanoidBakeCamera");
                    SceneManager.MoveGameObjectToScene(cameraHost, previewScene);
                    Camera camera = cameraHost.AddComponent<Camera>();
                    ConfigureCamera(camera, contract);

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
            string outputDirectory = NormalizeAssetPath(Path.Combine(
                PlayerBakedWalkV2TextureImporter.FrameRoot,
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
                sourcePsbPath = contract.walkClipPath,
                sourcePsbSha256 = Sha256(contract.rigPrefabPath),
                poses = new PlayerBakedWalkV2BakePoseReceipt[PlayerBakedWalkCatalogV2.PoseCount]
            };

            float yaw = ModelYawDegrees(direction);
            for (var pose = 0; pose < PlayerBakedWalkCatalogV2.PoseCount; pose++)
            {
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, yaw, 0f));
                AnimationMode.SampleAnimationClip(instance, clip, plan.PoseSampleSeconds[pose]);

                // The catalog stores in-place poses; the runtime advances the root itself. Drop the
                // clip's own planar travel so every pose shares one origin, exactly as the V2 baker
                // does, then measure the anchors the foot-lock validation will re-expand.
                Vector3 sampledRoot = hips.position;
                var rootPlanar = new Vector3(sampledRoot.x, 0f, sampledRoot.z);
                instance.transform.position -= rootPlanar;

                Vector2 leftAnchor = ToCanvasPx(camera, leftFoot.position, contract);
                Vector2 rightAnchor = ToCanvasPx(camera, rightFoot.position, contract);
                Vector2 pelvisAnchor = ToCanvasPx(camera, hips.position, contract);
                RequireAnchorInside(leftAnchor, contract, "leftFoot", directionName, pose);
                RequireAnchorInside(rightAnchor, contract, "rightFoot", directionName, pose);
                RequireAnchorInside(pelvisAnchor, contract, "pelvis", directionName, pose);
                RequireAnchorInside(
                    ToCanvasPx(camera, head.position, contract),
                    contract,
                    "head",
                    directionName,
                    pose);

                Render(camera, target, pixels, contract);
                ForceHardAlpha(pixels);

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
                    pelvisAnchorPx = pelvisAnchor
                };
            }

            string receiptPath = NormalizeAssetPath(
                "Assets/Resources/FamilyCompany/PlayerBakedWalkV2/source-receipt-" +
                directionName + ".json");
            File.WriteAllText(receiptPath, JsonUtility.ToJson(receipt, true));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(receiptPath, ImportAssetOptions.ForceSynchronousImport);
            PlayerBakedWalkV2Validation.ValidateReceiptAndPngs(receipt, direction);
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
            var leftHeight = new float[PhaseSampleCount];
            var rightHeight = new float[PhaseSampleCount];
            var travel = new float[PhaseSampleCount];

            Vector3 previousRoot = Vector3.zero;
            for (var index = 0; index < PhaseSampleCount; index++)
            {
                float time = cycleSeconds * index / (PhaseSampleCount - 1);
                times[index] = time;
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                AnimationMode.SampleAnimationClip(instance, clip, time);
                Vector3 root = hips.position;
                leftHeight[index] = leftFoot.position.y;
                rightHeight[index] = rightFoot.position.y;
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

            int plantIndex = FindLeftPlantStart(leftHeight, rightHeight);
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
        /// The left support phase starts where the left foot is planted and the right foot is
        /// about to lift. Score every sample by how much lower the left foot sits than the right
        /// and take the start of the widest such run, which is the heel strike.
        /// </summary>
        private static int FindLeftPlantStart(float[] leftHeight, float[] rightHeight)
        {
            int count = leftHeight.Length;
            float leftFloor = leftHeight.Min();
            float rightFloor = rightHeight.Min();
            float span = Mathf.Max(leftHeight.Max() - leftFloor, rightHeight.Max() - rightFloor);
            if (span <= 0.0001f)
                throw new InvalidOperationException(
                    "Walk clip never lifts either foot. This is not a walk cycle.");
            float threshold = span * 0.15f;

            var planted = new bool[count];
            for (var index = 0; index < count; index++)
                planted[index] = leftHeight[index] - leftFloor <= threshold &&
                                 rightHeight[index] - rightFloor > threshold;

            int bestStart = -1;
            int bestLength = 0;
            for (var index = 0; index < count; index++)
            {
                if (!planted[index]) continue;
                int length = 0;
                while (length < count && planted[(index + length) % count]) length++;
                if (length > bestLength)
                {
                    bestLength = length;
                    bestStart = index;
                }
                index += Math.Max(0, length - 1);
            }
            if (bestStart < 0)
                throw new InvalidOperationException(
                    "Could not find a left-foot support phase in the walk clip. The catalog " +
                    "requires poses 0-3 to stand on the left foot.");
            return bestStart;
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
            camera.transform.position +=
                camera.transform.right * ((viewport.x - 0.5f) * worldWidth) +
                camera.transform.up * ((viewport.y - FeetViewportY) * worldHeight);
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
        public int canvasWidth = 384;
        public int canvasHeight = 512;
        public float pixelsPerUnit = 324f;

        /// <summary>Length of one full two-step cycle. Zero uses the whole clip.</summary>
        public float cycleSeconds;

        /// <summary>Root travel over one cycle, when the clip was exported In Place.</summary>
        public float cycleDistanceOverride;

        /// <summary>Bypasses the measured stride-derived scale when a rig needs hand tuning.</summary>
        public float uniformScaleOverride;
    }
}
