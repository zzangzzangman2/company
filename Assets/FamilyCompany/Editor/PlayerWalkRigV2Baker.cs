using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using FamilyCompany.Presentation.Unity;
using FamilyCompany.Presentation.Unity.OfficeGridView;
using FamilyCompany.Simulation.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace FamilyCompany.Editor
{
    public static class PlayerWalkRigV2Baker
    {
        public const string ContractPath = "ArtSources/PlayerWalkRigV2/rig-contract.json";
        private const string RequiredContract = "FC-PLAYER-WALK-RIG-V2";

        [MenuItem("Family Company/Art/Bake Player Walk Rig V2")]
        public static void Run()
        {
            PlayerWalkRigV2BakeContract contract = LoadContract();
            Bake(contract);
        }

        public static void RunFromCommandLine()
        {
            try
            {
                Run();
                Debug.Log("PLAYER_WALK_RIG_V2_BAKER: PASS");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("PLAYER_WALK_RIG_V2_BAKER: FAIL | " + exception.Message);
                EditorApplication.Exit(1);
            }
        }

        internal static void Bake(PlayerWalkRigV2BakeContract contract)
        {
            ValidateContract(contract);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(contract.rigPrefabPath);
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(contract.animationClipPath);
            if (prefab == null) throw new InvalidOperationException("Rig prefab is missing: " + contract.rigPrefabPath);
            if (clip == null) throw new InvalidOperationException("Walk clip is missing: " + contract.animationClipPath);
            if (clip.length <= 0f) throw new InvalidOperationException("Walk clip length must be positive.");

            Scene previewScene = SceneManager.GetActiveScene();
            GameObject instance = null;
            GameObject cameraHost = null;
            RenderTexture target = null;
            Texture2D pixels = null;
            try
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, previewScene);
                if (instance == null) throw new InvalidOperationException("Could not instantiate the rig prefab.");
                RequireAuthoredRig(instance, contract);

                Transform rootMotion = FindRequired(instance.transform, contract.rootMotionTransform);
                Transform leftFoot = FindRequired(instance.transform, contract.leftFootContactTransform);
                Transform rightFoot = FindRequired(instance.transform, contract.rightFootContactTransform);
                Transform pelvis = FindRequired(instance.transform, contract.pelvisTransform);
                TransformSnapshot[] authoredPose = CaptureTransforms(instance);

                cameraHost = new GameObject("PlayerWalkRigV2BakeCamera");
                SceneManager.MoveGameObjectToScene(cameraHost, previewScene);
                Camera camera = cameraHost.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = contract.canvasHeight / (2f * contract.pixelsPerUnit);
                camera.aspect = contract.canvasWidth / (float)contract.canvasHeight;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                camera.allowHDR = false;
                camera.allowMSAA = false;
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = 100f;
                camera.transform.position = new Vector3(
                    0f,
                    contract.canvasHeight / (2f * contract.pixelsPerUnit),
                    -10f);
                camera.transform.rotation = Quaternion.identity;
                camera.enabled = false;

                target = new RenderTexture(
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
                camera.targetTexture = target;
                pixels = new Texture2D(
                    contract.canvasWidth,
                    contract.canvasHeight,
                    TextureFormat.RGBA32,
                    false,
                    false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };

                Directory.CreateDirectory(contract.outputDirectory);
                var receipt = new PlayerBakedWalkV2BakeReceipt
                {
                    direction = contract.direction,
                    canvasWidth = contract.canvasWidth,
                    canvasHeight = contract.canvasHeight,
                    pixelsPerUnit = contract.pixelsPerUnit,
                    strideWorld = contract.strideWorld,
                    visualScale = contract.visualScale,
                    sourcePsbPath = contract.sourcePsbPath,
                    sourcePsbSha256 = contract.sourcePsbSha256,
                    poses = new PlayerBakedWalkV2BakePoseReceipt[PlayerBakedWalkCatalogV2.PoseCount]
                };

                AnimationMode.StartAnimationMode();
                try
                {
                    for (var pose = 0; pose < PlayerBakedWalkCatalogV2.PoseCount; pose++)
                    {
                        RestoreTransforms(authoredPose);
                        instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                        float sampleTime = clip.length * pose / PlayerBakedWalkCatalogV2.PoseCount;
                        AnimationMode.SampleAnimationClip(instance, clip, sampleTime);
                        UpdateIkManagers(instance);
                        StabilizeShoeArt(instance);

                        Vector3 sampledRoot = rootMotion.position;
                        Vector3 leftWorldBeforeRootRemoval = leftFoot.position;
                        Vector3 rightWorldBeforeRootRemoval = rightFoot.position;
                        Vector3 rootPlanar = new Vector3(sampledRoot.x, sampledRoot.y, 0f);
                        instance.transform.position -= rootPlanar;
                        Vector2 leftAnchor = ToCanvasPx(leftFoot.position, contract);
                        Vector2 rightAnchor = ToCanvasPx(rightFoot.position, contract);
                        Vector2 pelvisAnchor = ToCanvasPx(pelvis.position, contract);

                        ValidateAnchor(leftAnchor, contract, "leftFoot", pose);
                        ValidateAnchor(rightAnchor, contract, "rightFoot", pose);
                        ValidateAnchor(pelvisAnchor, contract, "pelvis", pose);
                        ValidateWorldFootPlantBeforeBake(
                            pose,
                            leftWorldBeforeRootRemoval,
                            rightWorldBeforeRootRemoval,
                            receipt.poses,
                            contract);

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
                        ForceHardAlpha(pixels);
                        string outputPath = NormalizeAssetPath(Path.Combine(
                            contract.outputDirectory,
                            $"player_{contract.direction}_walk_{pose}_v2.png"));
                        File.WriteAllBytes(outputPath, pixels.EncodeToPNG());
                        receipt.poses[pose] = new PlayerBakedWalkV2BakePoseReceipt
                        {
                            pose = pose,
                            supportLeg = pose < 4 ? "left" : "right",
                            spritePath = outputPath,
                            rootWorld = new Vector2(sampledRoot.x, sampledRoot.y),
                            leftFootAnchorPx = leftAnchor,
                            rightFootAnchorPx = rightAnchor,
                            pelvisAnchorPx = pelvisAnchor
                        };
                    }
                }
                finally
                {
                    AnimationMode.StopAnimationMode();
                }

                string frameRoot = Path.GetDirectoryName(contract.outputDirectory) ?? contract.outputDirectory;
                string catalogRoot = Path.GetDirectoryName(frameRoot) ?? frameRoot;
                string receiptPath = NormalizeAssetPath(Path.Combine(
                    catalogRoot,
                    "source-receipt-" + contract.direction + ".json"));
                File.WriteAllText(receiptPath, JsonUtility.ToJson(receipt, true));
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                ConfigureImportedFrames(receipt);
                AssetDatabase.ImportAsset(receiptPath, ImportAssetOptions.ForceSynchronousImport);
                PlayerBakedWalkV2Validation.ValidateReceiptAndPngs(
                    receipt,
                    Array.IndexOf(PlayerBakedWalkCatalogV2.DirectionNames, contract.direction));
                Debug.Log(
                    $"PLAYER_WALK_RIG_V2_BAKER: PASS | direction={contract.direction} poses=8 " +
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

        private static void ValidateContract(PlayerWalkRigV2BakeContract contract)
        {
            if (contract == null) throw new ArgumentNullException(nameof(contract));
            if (!string.Equals(contract.contract, RequiredContract, StringComparison.Ordinal))
                throw new InvalidOperationException("Player walk rig V2 contract ID is invalid.");
            int direction = Array.IndexOf(PlayerBakedWalkCatalogV2.DirectionNames, contract.direction);
            if (direction < 0) throw new InvalidOperationException("Player walk rig direction is invalid.");
            if (contract.canvasWidth <= 0 || contract.canvasHeight <= 0 || contract.pixelsPerUnit <= 0f)
                throw new InvalidOperationException("Player walk rig canvas/PPU is invalid.");
            if (Mathf.Abs(contract.pixelsPerUnit - PlayerBakedWalkV2TextureImporter.PixelsPerUnit) > 0.001f)
                throw new InvalidOperationException("Player walk rig PPU differs from the V2 importer.");
            if (Mathf.Abs(contract.strideWorld - OfficeLocomotionGaitRules.DefaultStrideLength) > 0.000001f)
                throw new InvalidOperationException("Player walk rig stride differs from runtime.");
            if (Mathf.Abs(contract.visualScale - OfficeGridCharacterMover.UniformVisualScale) > 0.000001f)
                throw new InvalidOperationException("Player walk rig visual scale differs from runtime.");
            if (!File.Exists(contract.sourcePsbPath))
                throw new FileNotFoundException("Player walk source PSB is missing.", contract.sourcePsbPath);
            string actualSha = Sha256(contract.sourcePsbPath);
            if (!string.Equals(actualSha, contract.sourcePsbSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"Player walk source PSB SHA mismatch expected={contract.sourcePsbSha256} actual={actualSha}.");
            if (contract.requiredLayers == null || contract.requiredLayers.Length < 12)
                throw new InvalidOperationException("Player walk rig requires at least 12 explicitly named layers.");
        }

        private static void RequireAuthoredRig(GameObject instance, PlayerWalkRigV2BakeContract contract)
        {
            foreach (string layer in contract.requiredLayers)
                FindRequired(instance.transform, layer);
            bool hasLimbSolver = instance.GetComponentsInChildren<Component>(true).Any(component =>
                component != null && string.Equals(component.GetType().Name, "LimbSolver2D", StringComparison.Ordinal));
            if (!hasLimbSolver)
                throw new InvalidOperationException("Player walk rig has no LimbSolver2D authoring component.");
            SpriteRenderer[] renderers = instance.GetComponentsInChildren<SpriteRenderer>(true);
            if (renderers.Length < 12)
                throw new InvalidOperationException("Player walk rig must expose at least 12 rigid SpriteRenderer parts.");
            foreach (SpriteRenderer renderer in renderers)
            {
                if (renderer.sprite == null)
                    throw new InvalidOperationException("Player walk rig contains a SpriteRenderer with no Sprite.");
                if (renderer.sprite.texture.filterMode != FilterMode.Point)
                    throw new InvalidOperationException("Player walk rig part texture must use Point filtering.");
            }
        }

        private static void UpdateIkManagers(GameObject instance)
        {
            Component[] managers = instance.GetComponentsInChildren<Component>(true)
                .Where(component => component != null &&
                    string.Equals(component.GetType().Name, "IKManager2D", StringComparison.Ordinal))
                .ToArray();
            if (managers.Length == 0)
                throw new InvalidOperationException("Player walk rig has no IKManager2D authoring component.");
            foreach (Component manager in managers)
            {
                var update = manager.GetType().GetMethod("UpdateManager", Type.EmptyTypes);
                if (update == null)
                    throw new InvalidOperationException("IKManager2D.UpdateManager is unavailable.");
                update.Invoke(manager, null);
            }
        }

        private static TransformSnapshot[] CaptureTransforms(GameObject root) =>
            root.GetComponentsInChildren<Transform>(true)
                .Select(value => new TransformSnapshot(
                    value,
                    value.localPosition,
                    value.localRotation,
                    value.localScale))
                .ToArray();

        private static void StabilizeShoeArt(GameObject instance)
        {
            foreach (string name in new[] { "shoe_L_art", "shoe_R_art" })
            {
                Transform shoe = FindRequired(instance.transform, name);
                shoe.rotation = Quaternion.identity;
            }
        }

        private static void RestoreTransforms(IEnumerable<TransformSnapshot> snapshots)
        {
            foreach (TransformSnapshot snapshot in snapshots)
            {
                if (snapshot.Transform == null) continue;
                snapshot.Transform.localPosition = snapshot.LocalPosition;
                snapshot.Transform.localRotation = snapshot.LocalRotation;
                snapshot.Transform.localScale = snapshot.LocalScale;
            }
        }

        private readonly struct TransformSnapshot
        {
            public TransformSnapshot(
                Transform transform,
                Vector3 localPosition,
                Quaternion localRotation,
                Vector3 localScale)
            {
                Transform = transform;
                LocalPosition = localPosition;
                LocalRotation = localRotation;
                LocalScale = localScale;
            }

            public Transform Transform { get; }
            public Vector3 LocalPosition { get; }
            public Quaternion LocalRotation { get; }
            public Vector3 LocalScale { get; }
        }

        private static void ValidateWorldFootPlantBeforeBake(
            int pose,
            Vector3 leftWorld,
            Vector3 rightWorld,
            PlayerBakedWalkV2BakePoseReceipt[] previous,
            PlayerWalkRigV2BakeContract contract)
        {
            if (pose == 0 || pose == 4) return;
            int originPose = pose < 4 ? 0 : 4;
            PlayerBakedWalkV2BakePoseReceipt origin = previous[originPose];
            if (origin == null) return;
            // The final source-pixel drift gate below is authoritative. This pre-bake check keeps
            // a rig with a visibly moving IK target from being rendered at all.
            Vector2 current = pose < 4 ? (Vector2)leftWorld : (Vector2)rightWorld;
            Vector2 originRoot = origin.rootWorld;
            Vector2 originAnchor = pose < 4 ? origin.leftFootAnchorPx : origin.rightFootAnchorPx;
            Vector2 originWorld = new Vector2(
                (originAnchor.x - contract.canvasWidth * 0.5f) / contract.pixelsPerUnit + originRoot.x,
                originAnchor.y / contract.pixelsPerUnit + originRoot.y);
            float driftPx = (current - originWorld).magnitude * contract.pixelsPerUnit;
            if (driftPx > PlayerBakedWalkV2Validation.MaximumSupportDrift2dPx)
                throw new InvalidOperationException(
                    $"Player walk rig IK support foot drifts {driftPx:F3}px at pose {pose}; " +
                    $"current={current.x:F6},{current.y:F6} " +
                    $"origin={originWorld.x:F6},{originWorld.y:F6}.");
        }

        private static void ConfigureImportedFrames(PlayerBakedWalkV2BakeReceipt receipt)
        {
            foreach (PlayerBakedWalkV2BakePoseReceipt pose in receipt.poses)
            {
                AssetDatabase.ImportAsset(pose.spritePath, ImportAssetOptions.ForceSynchronousImport);
                var importer = AssetImporter.GetAtPath(pose.spritePath) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("Baked PNG has no TextureImporter.");
                importer.SaveAndReimport();
            }
        }

        private static void ForceHardAlpha(Texture2D texture)
        {
            Color32[] values = texture.GetPixels32();
            for (var index = 0; index < values.Length; index++)
            {
                Color32 value = values[index];
                if (value.a < 128)
                {
                    values[index] = new Color32(0, 0, 0, 0);
                }
                else
                {
                    value.a = 255;
                    values[index] = value;
                }
            }
            texture.SetPixels32(values);
            texture.Apply(false, false);
        }

        private static Vector2 ToCanvasPx(Vector3 world, PlayerWalkRigV2BakeContract contract) =>
            new Vector2(
                world.x * contract.pixelsPerUnit + contract.canvasWidth * 0.5f,
                world.y * contract.pixelsPerUnit);

        private static void ValidateAnchor(
            Vector2 anchor,
            PlayerWalkRigV2BakeContract contract,
            string label,
            int pose)
        {
            if (float.IsNaN(anchor.x) || float.IsInfinity(anchor.x) ||
                float.IsNaN(anchor.y) || float.IsInfinity(anchor.y) ||
                anchor.x < 0f || anchor.x > contract.canvasWidth ||
                anchor.y < 0f || anchor.y > contract.canvasHeight)
                throw new InvalidOperationException(
                    $"Player walk rig pose {pose} {label} anchor {anchor} is outside the fixed canvas.");
        }

        private static Transform FindRequired(Transform root, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("Player walk rig transform name is empty.");
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
                if (string.Equals(candidate.name, name, StringComparison.Ordinal))
                    return candidate;
            throw new InvalidOperationException("Player walk rig transform is missing: " + name);
        }

        private static PlayerWalkRigV2BakeContract LoadContract()
        {
            if (!File.Exists(ContractPath))
                throw new FileNotFoundException("Player walk rig V2 contract is missing.", ContractPath);
            PlayerWalkRigV2BakeContract contract = JsonUtility.FromJson<PlayerWalkRigV2BakeContract>(
                File.ReadAllText(ContractPath));
            if (contract == null) throw new InvalidOperationException("Player walk rig V2 contract JSON is invalid.");
            return contract;
        }

        private static string Sha256(string path)
        {
            using SHA256 sha = SHA256.Create();
            using FileStream stream = File.OpenRead(path);
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
        }

        private static string NormalizeAssetPath(string value) => value.Replace('\\', '/');
    }
}
