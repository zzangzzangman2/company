using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using FamilyCompany.Experimental.Family3D;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FamilyCompany.Experimental.Family3D.Editor
{
    /// <summary>
    /// Copies the real Prototype01 scene into Experimental, adds only the candidate QA adapter,
    /// and builds that explicit copied scene.  The production scene, catalogs, bootstrap and
    /// EditorBuildSettings are never written.
    /// </summary>
    public static class Family3DStarterOfficeCandidateQaBuilder
    {
        public const string Contract = "FC-FAMILY-3D-STARTER-OFFICE-CANDIDATE-QA-V1";
        public const string ProductionScenePath = "Assets/FamilyCompany/Scenes/Prototype01.unity";
        public const string PreviewScenePath =
            "Assets/FamilyCompany/Scenes/OfficeTileMigrationPreview.unity";
        public const string QaScenePath =
            "Assets/FamilyCompany/Experimental/Family3DPrototype/Scenes/Family3DStarterOfficeCandidateQa.unity";
        public const string WalkClipPath =
            "Assets/FamilyCompany/Editor/PlayerWalkHumanoidAuthoring/PlayerHumanoidWalk.fbx";
        public const string PlayerModelPath =
            "Assets/FamilyCompany/Experimental/Family3DPrototype/Candidates/PlayerV3/player-v6-blender-humanoid-v3.fbx";
        public const string OlderSisterModelPath =
            "Assets/FamilyCompany/Experimental/Family3DPrototype/Candidates/OlderSisterV1/older-sister-blender-humanoid-v1.fbx";
        public const string FatherModelPath =
            "Assets/FamilyCompany/Experimental/Family3DPrototype/Candidates/FatherV1/father-blender-humanoid-v1.fbx";
        public const string MotherModelPath =
            "Assets/FamilyCompany/Experimental/Family3DPrototype/Candidates/MotherV1/mother-blender-humanoid-v1.fbx";
        public const string DefaultBuildRoot =
            "Artifacts/Family3DStarterOfficeCandidateQaV1/BuildRun1";

        private static readonly CandidateDefinition[] Candidates =
        {
            new CandidateDefinition("player", PlayerModelPath),
            new CandidateDefinition("older_sister", OlderSisterModelPath),
            new CandidateDefinition("father", FatherModelPath),
            new CandidateDefinition("mother", MotherModelPath)
        };

        [MenuItem("Family Company/Experimental/Build Starter Office 3D Candidate QA")]
        public static void BuildMenu()
        {
            Build(ResolveBuildRoot());
        }

        [MenuItem("Family Company/Experimental/Create Starter Office 3D Candidate QA Scene Only")]
        public static void CreateSceneOnlyMenu()
        {
            ThrowIfInteractiveSceneIsDirty();
            AssetBundle bundle = LoadAssets();
            CreateIsolatedQaScene(bundle);
            Debug.Log("FAMILY_3D_STARTER_OFFICE_QA_SCENE: PASS | " + QaScenePath);
        }

        public static void BuildFromCommandLine()
        {
            try
            {
                Build(ResolveBuildRoot());
                Debug.Log("FAMILY_3D_STARTER_OFFICE_QA_BUILD: PASS");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("FAMILY_3D_STARTER_OFFICE_QA_BUILD: FAIL | " + exception.Message);
                EditorApplication.Exit(1);
            }
        }

        public static void CreateSceneOnlyFromCommandLine()
        {
            try
            {
                AssetBundle bundle = LoadAssets();
                CreateIsolatedQaScene(bundle);
                Debug.Log("FAMILY_3D_STARTER_OFFICE_QA_SCENE: PASS | " + QaScenePath);
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("FAMILY_3D_STARTER_OFFICE_QA_SCENE: FAIL | " + exception.Message);
                EditorApplication.Exit(1);
            }
        }

        private static void Build(string buildRoot)
        {
            ThrowIfInteractiveSceneIsDirty();
            string productionSceneBefore = Sha256Asset(ProductionScenePath);
            string previewSceneBefore = Sha256Asset(PreviewScenePath);
            string buildSettingsBefore = CaptureBuildSettings();
            string buildSettingsFileBefore = Sha256File(ProjectPath("ProjectSettings/EditorBuildSettings.asset"));

            AssetBundle bundle = LoadAssets();
            int qaLayer = CreateIsolatedQaScene(bundle);

            Directory.CreateDirectory(buildRoot);
            string executablePath = Path.Combine(buildRoot, "FamilyCompanyStarterOffice3DCandidateQa.exe");
            var options = new BuildPlayerOptions
            {
                // ScenePreviewJump intentionally installs only when the real additive office scene
                // is present in the player. Keep both paths explicit; never persist Build Settings.
                scenes = new[] { QaScenePath, PreviewScenePath },
                locationPathName = executablePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);

            string productionSceneAfter = Sha256Asset(ProductionScenePath);
            string previewSceneAfter = Sha256Asset(PreviewScenePath);
            string buildSettingsAfter = CaptureBuildSettings();
            string buildSettingsFileAfter = Sha256File(ProjectPath("ProjectSettings/EditorBuildSettings.asset"));
            bool productionMutation =
                !string.Equals(productionSceneBefore, productionSceneAfter, StringComparison.Ordinal) ||
                !string.Equals(previewSceneBefore, previewSceneAfter, StringComparison.Ordinal) ||
                !string.Equals(buildSettingsBefore, buildSettingsAfter, StringComparison.Ordinal) ||
                !string.Equals(buildSettingsFileBefore, buildSettingsFileAfter, StringComparison.Ordinal);

            WriteReceipt(
                buildRoot,
                executablePath,
                report,
                bundle,
                qaLayer,
                productionSceneBefore,
                productionSceneAfter,
                previewSceneBefore,
                previewSceneAfter,
                buildSettingsFileBefore,
                buildSettingsFileAfter,
                productionMutation);

            if (productionMutation)
                throw new InvalidOperationException(
                    "Production scene or EditorBuildSettings changed during isolated QA build.");
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException(
                    "Isolated Starter Office candidate QA build failed: " + report.summary.result +
                    " errors=" + report.summary.totalErrors);
        }

        private static AssetBundle LoadAssets()
        {
            var prefabs = new GameObject[Candidates.Length];
            for (var index = 0; index < Candidates.Length; index++)
            {
                CandidateDefinition definition = Candidates[index];
                AssetDatabase.ImportAsset(
                    definition.ModelPath,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(definition.ModelPath);
                if (prefab == null)
                    throw new InvalidOperationException(
                        "Candidate prefab did not load: " + definition.ModelPath);
                Animator animator = prefab.GetComponent<Animator>();
                if (animator == null || animator.avatar == null ||
                    !animator.avatar.isValid || !animator.avatar.isHuman)
                    throw new InvalidOperationException(
                        definition.FamilyId + " candidate is missing a valid Humanoid Avatar.");
                int rendererCount = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length;
                if (rendererCount != 1)
                    throw new InvalidOperationException(
                        definition.FamilyId + " expected one SkinnedMeshRenderer; found " + rendererCount + ".");
                prefabs[index] = prefab;
            }

            AssetDatabase.ImportAsset(
                WalkClipPath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AnimationClip walkClip = AssetDatabase.LoadAllAssetsAtPath(WalkClipPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(candidate =>
                    !candidate.name.StartsWith("__preview__", StringComparison.Ordinal) &&
                    candidate.isHumanMotion);
            if (walkClip == null)
                throw new InvalidOperationException(
                    "Shared Humanoid walk clip did not load: " + WalkClipPath);
            return new AssetBundle(prefabs, walkClip);
        }

        private static int CreateIsolatedQaScene(AssetBundle bundle)
        {
            EnsureAssetFolder("Assets/FamilyCompany/Experimental/Family3DPrototype/Scenes");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(QaScenePath) != null &&
                !AssetDatabase.DeleteAsset(QaScenePath))
                throw new InvalidOperationException("Could not replace isolated QA scene: " + QaScenePath);
            if (!AssetDatabase.CopyAsset(ProductionScenePath, QaScenePath))
                throw new InvalidOperationException(
                    "Could not copy production scene to isolated QA path. Source was not written.");
            AssetDatabase.ImportAsset(QaScenePath, ImportAssetOptions.ForceSynchronousImport);

            Scene scene = EditorSceneManager.OpenScene(QaScenePath, OpenSceneMode.Single);
            int qaLayer = FindUnusedSceneLayer(scene);
            var root = new GameObject("~Family3DStarterOfficeCandidateQa_ExperimentalOnly");
            var qa = root.AddComponent<Family3DStarterOfficeCandidateQa>();
            Camera camera = CreateOverlayCamera(root.transform, qaLayer);
            CreateCandidateLight(root.transform, qaLayer);
            qa.Configure(
                bundle.Prefabs[0],
                bundle.Prefabs[1],
                bundle.Prefabs[2],
                bundle.Prefabs[3],
                bundle.WalkClip,
                camera,
                qaLayer);
            EditorUtility.SetDirty(qa);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, QaScenePath))
                throw new InvalidOperationException("Could not save isolated QA scene: " + QaScenePath);
            return qaLayer;
        }

        private static Camera CreateOverlayCamera(Transform parent, int layer)
        {
            var host = new GameObject("Family3DStarterOfficeQaOverlayCamera");
            host.transform.SetParent(parent, false);
            host.transform.position = new Vector3(0f, 12f, -12f);
            host.transform.LookAt(Vector3.zero);
            var camera = host.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6.5f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.Depth;
            camera.cullingMask = 1 << layer;
            camera.depth = 100f;
            camera.allowHDR = false;
            camera.allowMSAA = true;
            return camera;
        }

        private static void CreateCandidateLight(Transform parent, int layer)
        {
            var host = new GameObject("Family3DStarterOfficeQaCandidateLight");
            host.transform.SetParent(parent, false);
            host.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            var light = host.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.color = new Color(1f, 0.94f, 0.86f);
            light.shadows = LightShadows.Soft;
            light.cullingMask = 1 << layer;
        }

        private static int FindUnusedSceneLayer(Scene scene)
        {
            var used = new bool[32];
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                used[transform.gameObject.layer] = true;
            for (var layer = 31; layer >= 8; layer--)
            {
                if (!used[layer])
                    return layer;
            }
            throw new InvalidOperationException("No isolated user layer is free in copied QA scene.");
        }

        private static void WriteReceipt(
            string buildRoot,
            string executablePath,
            BuildReport report,
            AssetBundle bundle,
            int qaLayer,
            string productionSceneBefore,
            string productionSceneAfter,
            string previewSceneBefore,
            string previewSceneAfter,
            string buildSettingsBefore,
            string buildSettingsAfter,
            bool productionMutation)
        {
            var assets = new CandidateReceipt[Candidates.Length];
            for (var index = 0; index < Candidates.Length; index++)
            {
                assets[index] = new CandidateReceipt
                {
                    familyId = Candidates[index].FamilyId,
                    modelAsset = Candidates[index].ModelPath,
                    modelSha256 = Sha256Asset(Candidates[index].ModelPath),
                    humanoidAvatar = bundle.Prefabs[index].GetComponent<Animator>().avatar.name
                };
            }

            string json = JsonUtility.ToJson(new BuildReceipt
            {
                contract = Contract,
                status = report.summary.result == BuildResult.Succeeded
                    ? "ISOLATED_QA_BUILD_COMPLETE_VISUAL_REVIEW_REQUIRED"
                    : "ISOLATED_QA_BUILD_FAILED",
                productionMutation = productionMutation,
                productionEligible = false,
                productionScene = ProductionScenePath,
                isolatedScene = QaScenePath,
                productionSceneSha256Before = productionSceneBefore,
                productionSceneSha256After = productionSceneAfter,
                previewScene = PreviewScenePath,
                previewSceneSha256Before = previewSceneBefore,
                previewSceneSha256After = previewSceneAfter,
                editorBuildSettingsSha256Before = buildSettingsBefore,
                editorBuildSettingsSha256After = buildSettingsAfter,
                buildSettingsPersistedMutation =
                    !string.Equals(buildSettingsBefore, buildSettingsAfter, StringComparison.Ordinal),
                isolationMechanism =
                    "AssetDatabase.CopyAsset production scene -> Experimental; explicit QA copy + read-only additive office BuildPlayerOptions.scenes only",
                executable = Path.GetFullPath(executablePath),
                qaLayer = qaLayer,
                coordinateMapping =
                    "production Camera.WorldToViewportPoint(actor XY/Z) -> overlay ViewportPointToRay -> Y=0 plane; raw XZ fallback",
                directionMapping = "direction South..SE 0..7 -> yaw=(direction-4)*45 degrees",
                motionMapping =
                    "Position + LastActualDisplacement + GaitPhase01 + CurrentDirection -> Family3DWalkActor",
                scalePolicy = "live production SpriteRenderer bounds projected viewport height",
                source2DPolicy =
                    "QA-only Renderer.forceRenderingOff; sorting layer/order and transform depth never assigned",
                supportedPhases = new[] { "Idle(standing)", "Navigating(walking)" },
                unsupportedSeatedPolicy =
                    "seat approach/transitions/work/egress skip 3D and restore original 2D presentation",
                walkClipAsset = WalkClipPath,
                walkClipSha256 = Sha256Asset(WalkClipPath),
                walkClipName = bundle.WalkClip.name,
                walkClipLength = bundle.WalkClip.length,
                lockedCycleSeconds = Family3DWalkActor.LockedCycleSeconds,
                candidates = assets,
                buildResult = report.summary.result.ToString(),
                buildBytes = (long)report.summary.totalSize
            }, true);
            File.WriteAllText(Path.Combine(buildRoot, "build-receipt.json"), json);
        }

        private static string ResolveBuildRoot()
        {
            string projectRoot = ProjectPath(string.Empty);
            string root = Path.GetFullPath(Path.Combine(projectRoot, DefaultBuildRoot));
            string[] args = Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length - 1; index++)
            {
                if (!string.Equals(
                        args[index],
                        "-family3d-starter-office-qa-build-output",
                        StringComparison.OrdinalIgnoreCase))
                    continue;
                root = Path.GetFullPath(args[index + 1]);
                break;
            }
            string allowed = Path.GetFullPath(Path.Combine(
                    projectRoot,
                    "Artifacts/Family3DStarterOfficeCandidateQaV1"))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string candidate = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!candidate.StartsWith(
                    allowed + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Refusing non-isolated QA build output: " + candidate);
            return candidate;
        }

        private static string CaptureBuildSettings()
        {
            return string.Join(
                "|",
                EditorBuildSettings.scenes.Select(scene => (scene.enabled ? "1:" : "0:") + scene.path));
        }

        private static string Sha256Asset(string assetPath)
        {
            return Sha256File(ProjectPath(assetPath));
        }

        private static string Sha256File(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", string.Empty);
        }

        private static string ProjectPath(string relative)
        {
            string root = Directory.GetParent(Application.dataPath)?.FullName ??
                          throw new InvalidOperationException("Could not resolve Unity project root.");
            return Path.GetFullPath(Path.Combine(root, relative));
        }

        private static void ThrowIfInteractiveSceneIsDirty()
        {
            if (Application.isBatchMode)
                return;
            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                if (scene.isDirty)
                    throw new InvalidOperationException(
                        "Refusing to replace an open dirty scene: " + scene.path);
            }
        }

        private static void EnsureAssetFolder(string assetPath)
        {
            string normalized = assetPath.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(normalized))
                return;
            int split = normalized.LastIndexOf('/');
            if (split <= 0)
                throw new InvalidOperationException("Invalid asset folder: " + normalized);
            string parent = normalized.Substring(0, split);
            EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, normalized.Substring(split + 1));
        }

        [Serializable]
        private sealed class BuildReceipt
        {
            public string contract;
            public string status;
            public bool productionMutation;
            public bool productionEligible;
            public string productionScene;
            public string isolatedScene;
            public string productionSceneSha256Before;
            public string productionSceneSha256After;
            public string previewScene;
            public string previewSceneSha256Before;
            public string previewSceneSha256After;
            public string editorBuildSettingsSha256Before;
            public string editorBuildSettingsSha256After;
            public bool buildSettingsPersistedMutation;
            public string isolationMechanism;
            public string executable;
            public int qaLayer;
            public string coordinateMapping;
            public string directionMapping;
            public string motionMapping;
            public string scalePolicy;
            public string source2DPolicy;
            public string[] supportedPhases;
            public string unsupportedSeatedPolicy;
            public string walkClipAsset;
            public string walkClipSha256;
            public string walkClipName;
            public float walkClipLength;
            public float lockedCycleSeconds;
            public CandidateReceipt[] candidates;
            public string buildResult;
            public long buildBytes;
        }

        [Serializable]
        private sealed class CandidateReceipt
        {
            public string familyId;
            public string modelAsset;
            public string modelSha256;
            public string humanoidAvatar;
        }

        private sealed class AssetBundle
        {
            public AssetBundle(GameObject[] prefabs, AnimationClip walkClip)
            {
                Prefabs = prefabs;
                WalkClip = walkClip;
            }

            public GameObject[] Prefabs { get; }
            public AnimationClip WalkClip { get; }
        }

        private readonly struct CandidateDefinition
        {
            public CandidateDefinition(string familyId, string modelPath)
            {
                FamilyId = familyId;
                ModelPath = modelPath;
            }

            public string FamilyId { get; }
            public string ModelPath { get; }
        }
    }
}
