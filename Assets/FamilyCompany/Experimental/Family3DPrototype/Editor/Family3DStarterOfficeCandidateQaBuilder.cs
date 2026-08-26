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
        public const string FatherV18StaticQaScenePath =
            "Assets/FamilyCompany/Experimental/Family3DPrototype/Scenes/Family3DFatherV18HiggsfieldStaticMapQa.unity";
        public const string FatherV18MotionQaScenePath =
            "Assets/FamilyCompany/Experimental/Family3DPrototype/Scenes/Family3DFatherV18HiggsfieldNativeRunMapQaV22.unity";
        public const string WalkClipPath =
            "Assets/FamilyCompany/Editor/PlayerWalkHumanoidAuthoring/PlayerHumanoidWalk.fbx";
        public const string PlayerModelPath =
            "Assets/FamilyCompany/Experimental/Family3DPrototype/Candidates/PlayerV3/player-v6-blender-humanoid-v3.fbx";
        public const string OlderSisterModelPath =
            "Assets/FamilyCompany/Experimental/Family3DPrototype/Candidates/OlderSisterV1/older-sister-blender-humanoid-v1.fbx";
        public const string FatherModelPath =
            "Assets/FamilyCompany/Experimental/Family3DPrototype/Candidates/FatherApprovedV14NaturalWalkRigV1/father-approved-v14-natural-walk-rig-v1.fbx";
        public const string FatherV18StaticModelPath =
            "Assets/FamilyCompany/Experimental/Family3DPrototype/Candidates/FatherV18HiggsfieldStatic/father-v18-higgsfield-static.fbx";
        public const string FatherV18StaticTexturePath =
            "Assets/FamilyCompany/Experimental/Family3DPrototype/Candidates/FatherV18HiggsfieldStatic/father-v18-higgsfield-static-albedo.png";
        public const string FatherV18MotionIdleClipPath =
            "Assets/FamilyCompany/Experimental/Family3DPrototype/Candidates/FatherV18HiggsfieldMotionV19/father-v18-higgsfield-motion-v19-idle.fbx";
        // The video imports each generated animation GLB with its own mesh, skin, and bind pose.
        // Use run-644's native body while moving instead of retargeting its non-identical skin onto
        // the independently generated idle-0 body (V19's stretched-leg failure).
        public const string FatherV18MotionModelPath =
            "Assets/FamilyCompany/Experimental/Family3DPrototype/Candidates/FatherV18HiggsfieldMotionV19/father-v18-higgsfield-motion-v19-run-644.fbx";
        public const string FatherV18MotionRunClipPath =
            "Assets/FamilyCompany/Experimental/Family3DPrototype/Candidates/FatherV18HiggsfieldMotionV19/father-v18-higgsfield-motion-v19-run-644.fbx";
        public const string FatherV18MotionTexturePath =
            "Assets/FamilyCompany/Experimental/Family3DPrototype/Candidates/FatherV18HiggsfieldMotionV19/father-v18-higgsfield-motion-v19-albedo.png";
        public const string MotherModelPath =
            "Assets/FamilyCompany/Experimental/Family3DPrototype/Candidates/MotherV1/mother-blender-humanoid-v1.fbx";
        public const string DefaultBuildRoot =
            "Artifacts/Family3DStarterOfficeCandidateQaV1/BuildRun1";
        public const string FatherV18StaticDefaultBuildRoot =
            "Artifacts/Family3DStarterOfficeCandidateQaV1/FatherV18HiggsfieldStaticMapBuildV18";
        public const string FatherV18MotionDefaultBuildRoot =
            "Artifacts/Family3DStarterOfficeCandidateQaV1/FatherV18HiggsfieldNativeRunMapBuildV22";

        /// <summary>
        /// The QA scene must reference this material as an asset. Unity strips any shader that no
        /// scene or material asset pulls in, so the old runtime <c>Shader.Find("Unlit/Texture")</c>
        /// resolved in the Editor and returned null in every built player, silently falling back to
        /// Sprites/Default and rendering the Father as a dark vertex-coloured silhouette.
        /// </summary>
        public const string ExactAlbedoMaterialPath =
            "Assets/FamilyCompany/Experimental/Family3DPrototype/Materials/" +
            "FatherV18HiggsfieldExactAlbedoUnlit.mat";

        private const string ExactAlbedoShaderName = "Unlit/Texture";

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
            Build(ResolveBuildRoot(false), false);
        }

        [MenuItem("Family Company/Experimental/Create Starter Office 3D Candidate QA Scene Only")]
        public static void CreateSceneOnlyMenu()
        {
            ThrowIfInteractiveSceneIsDirty();
            AssetBundle bundle = LoadAssets(false);
            CreateIsolatedQaScene(bundle);
            Debug.Log("FAMILY_3D_STARTER_OFFICE_QA_SCENE: PASS | " + QaScenePath);
        }

        public static void BuildFromCommandLine()
        {
            try
            {
                Build(ResolveBuildRoot(false), false);
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
                AssetBundle bundle = LoadAssets(false);
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

        public static void BuildFatherV18StaticFromCommandLine()
        {
            try
            {
                Build(ResolveBuildRoot(true), true);
                Debug.Log("FAMILY_3D_FATHER_V18_STATIC_MAP_QA_BUILD: PASS");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError(
                    "FAMILY_3D_FATHER_V18_STATIC_MAP_QA_BUILD: FAIL | " + exception.Message);
                EditorApplication.Exit(1);
            }
        }

        public static void BuildFatherV18MotionFromCommandLine()
        {
            try
            {
                Build(ResolveBuildRoot(false, true), false, true);
                Debug.Log("FAMILY_3D_FATHER_V18_HIGGSFIELD_MOTION_MAP_QA_BUILD: PASS");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError(
                    "FAMILY_3D_FATHER_V18_HIGGSFIELD_MOTION_MAP_QA_BUILD: FAIL | " +
                    exception.Message);
                EditorApplication.Exit(1);
            }
        }

        private static void Build(
            string buildRoot,
            bool fatherV18StaticOnly,
            bool fatherV18MotionOnly = false)
        {
            ThrowIfInteractiveSceneIsDirty();
            string productionSceneBefore = Sha256Asset(ProductionScenePath);
            string previewSceneBefore = Sha256Asset(PreviewScenePath);
            string buildSettingsBefore = CaptureBuildSettings();
            string buildSettingsFileBefore = Sha256File(ProjectPath("ProjectSettings/EditorBuildSettings.asset"));

            AssetBundle bundle = LoadAssets(fatherV18StaticOnly, fatherV18MotionOnly);
            int qaLayer = CreateIsolatedQaScene(bundle);

            Directory.CreateDirectory(buildRoot);
            string executablePath = Path.Combine(
                buildRoot,
                fatherV18StaticOnly
                    ? "FamilyCompanyFatherV18HiggsfieldStaticMapQa.exe"
                    : fatherV18MotionOnly
                        ? "FamilyCompanyFatherV18HiggsfieldMotionMapQa.exe"
                    : "FamilyCompanyStarterOffice3DCandidateQa.exe");
            var options = new BuildPlayerOptions
            {
                // ScenePreviewJump intentionally installs only when the real additive office scene
                // is present in the player. Keep both paths explicit; never persist Build Settings.
                scenes = new[] { bundle.QaScenePath, PreviewScenePath },
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

        private static AssetBundle LoadAssets(
            bool fatherV18StaticOnly,
            bool fatherV18MotionOnly = false)
        {
            if (fatherV18StaticOnly && fatherV18MotionOnly)
                throw new InvalidOperationException("Father V18 static and motion modes are mutually exclusive.");
            CandidateDefinition[] definitions = fatherV18StaticOnly
                ? new[] { new CandidateDefinition("father", FatherV18StaticModelPath) }
                : fatherV18MotionOnly
                    ? new[] { new CandidateDefinition("father", FatherV18MotionModelPath) }
                : Candidates;
            var prefabs = new GameObject[definitions.Length];
            for (var index = 0; index < definitions.Length; index++)
            {
                CandidateDefinition definition = definitions[index];
                AssetDatabase.ImportAsset(
                    definition.ModelPath,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(definition.ModelPath);
                if (prefab == null)
                    throw new InvalidOperationException(
                        "Candidate prefab did not load: " + definition.ModelPath);
                if (fatherV18StaticOnly)
                {
                    int meshCount = prefab.GetComponentsInChildren<MeshRenderer>(true).Length;
                    int skinnedCount = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length;
                    if (meshCount != 1 || skinnedCount != 0)
                        throw new InvalidOperationException(
                            "Father V18 static candidate must contain one MeshRenderer and no " +
                            "SkinnedMeshRenderer; mesh=" + meshCount + " skinned=" + skinnedCount + ".");
                }
                else
                {
                    Animator animator = prefab.GetComponent<Animator>();
                    if (animator == null || animator.avatar == null ||
                        !animator.avatar.isValid || !animator.avatar.isHuman)
                        throw new InvalidOperationException(
                            definition.FamilyId + " candidate is missing a valid Humanoid Avatar.");
                    int rendererCount = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length;
                    if (rendererCount != 1)
                        throw new InvalidOperationException(
                            definition.FamilyId + " expected one SkinnedMeshRenderer; found " +
                            rendererCount + ".");
                }
                prefabs[index] = prefab;
            }

            if (fatherV18StaticOnly)
            {
                AssetDatabase.ImportAsset(
                    FatherV18StaticTexturePath,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(FatherV18StaticTexturePath);
                if (texture == null)
                    throw new InvalidOperationException(
                        "Father V18 static texture did not load: " + FatherV18StaticTexturePath);
                return new AssetBundle(
                    prefabs,
                    null,
                    null,
                    texture,
                    definitions,
                    true,
                    false,
                    FatherV18StaticQaScenePath);
            }

            if (fatherV18MotionOnly)
            {
                AssetDatabase.ImportAsset(
                    FatherV18MotionTexturePath,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(FatherV18MotionTexturePath);
                if (texture == null)
                    throw new InvalidOperationException(
                        "Father V18 motion texture did not load: " + FatherV18MotionTexturePath);

                AnimationClip idleClip = LoadHumanClip(FatherV18MotionIdleClipPath, "Idle");
                AnimationClip runClip = LoadHumanClip(
                    FatherV18MotionRunClipPath,
                    "Lean_Forward_Sprint_inplace");
                return new AssetBundle(
                    prefabs,
                    runClip,
                    idleClip,
                    texture,
                    definitions,
                    false,
                    true,
                    FatherV18MotionQaScenePath);
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
            return new AssetBundle(
                prefabs,
                walkClip,
                null,
                null,
                definitions,
                false,
                false,
                QaScenePath);
        }

        private static AnimationClip LoadHumanClip(string assetPath, string nameFragment)
        {
            AssetDatabase.ImportAsset(
                assetPath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .OfType<AnimationClip>()
                .Where(candidate =>
                    !candidate.name.StartsWith("__preview__", StringComparison.Ordinal) &&
                    candidate.isHumanMotion)
                .ToArray();
            AnimationClip clip = clips.FirstOrDefault(candidate =>
                                     candidate.name.IndexOf(
                                         nameFragment,
                                         StringComparison.OrdinalIgnoreCase) >= 0) ??
                                 clips.FirstOrDefault();
            if (clip == null)
                throw new InvalidOperationException(
                    "Humanoid clip containing '" + nameFragment + "' did not load: " + assetPath);
            return clip;
        }

        /// <summary>
        /// Creates or repairs the scene-referenced Unlit/Texture material. Shader.Find is safe here
        /// because this runs in the Editor; the point of the asset is that the scene reference
        /// carries the shader into the player, where Shader.Find would not.
        /// </summary>
        private static Material EnsureExactAlbedoMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(ExactAlbedoMaterialPath);
            if (existing != null &&
                existing.shader != null &&
                string.Equals(existing.shader.name, ExactAlbedoShaderName, StringComparison.Ordinal))
                return existing;

            Shader shader = Shader.Find(ExactAlbedoShaderName);
            if (shader == null)
                throw new InvalidOperationException(
                    ExactAlbedoShaderName + " is not available in this Editor installation.");

            string directory = Path.GetDirectoryName(ProjectPath(ExactAlbedoMaterialPath));
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var material = new Material(shader)
            {
                name = "FatherV18HiggsfieldExactAlbedoUnlit",
                color = Color.white
            };
            AssetDatabase.CreateAsset(material, ExactAlbedoMaterialPath);
            AssetDatabase.ImportAsset(
                ExactAlbedoMaterialPath,
                ImportAssetOptions.ForceSynchronousImport);
            var created = AssetDatabase.LoadAssetAtPath<Material>(ExactAlbedoMaterialPath);
            if (created == null)
                throw new InvalidOperationException(
                    "Could not create " + ExactAlbedoMaterialPath + ".");
            return created;
        }

        private static int CreateIsolatedQaScene(AssetBundle bundle)
        {
            EnsureAssetFolder("Assets/FamilyCompany/Experimental/Family3DPrototype/Scenes");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(bundle.QaScenePath) != null &&
                !AssetDatabase.DeleteAsset(bundle.QaScenePath))
                throw new InvalidOperationException(
                    "Could not replace isolated QA scene: " + bundle.QaScenePath);
            if (!AssetDatabase.CopyAsset(ProductionScenePath, bundle.QaScenePath))
                throw new InvalidOperationException(
                    "Could not copy production scene to isolated QA path. Source was not written.");
            AssetDatabase.ImportAsset(
                bundle.QaScenePath,
                ImportAssetOptions.ForceSynchronousImport);

            Scene scene = EditorSceneManager.OpenScene(bundle.QaScenePath, OpenSceneMode.Single);
            int qaLayer = FindUnusedSceneLayer(scene);
            var root = new GameObject("~Family3DStarterOfficeCandidateQa_ExperimentalOnly");
            var qa = root.AddComponent<Family3DStarterOfficeCandidateQa>();
            Camera camera = CreateOverlayCamera(root.transform, qaLayer);
            CreateCandidateLight(root.transform, qaLayer);
            if (bundle.FatherV18StaticOnly)
                qa.ConfigureFatherStaticRootMotionOnly(
                    bundle.Prefabs[0],
                    bundle.StaticAlbedo,
                    EnsureExactAlbedoMaterial(),
                    camera,
                    qaLayer);
            else if (bundle.FatherV18MotionOnly)
                qa.ConfigureFatherHiggsfieldIdleRun(
                    bundle.Prefabs[0],
                    bundle.StaticAlbedo,
                    EnsureExactAlbedoMaterial(),
                    bundle.IdleClip,
                    bundle.WalkClip,
                    camera,
                    qaLayer);
            else
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
            if (!EditorSceneManager.SaveScene(scene, bundle.QaScenePath))
                throw new InvalidOperationException(
                    "Could not save isolated QA scene: " + bundle.QaScenePath);
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
            var assets = new CandidateReceipt[bundle.Definitions.Length];
            for (var index = 0; index < bundle.Definitions.Length; index++)
            {
                Animator animator = bundle.Prefabs[index].GetComponent<Animator>();
                assets[index] = new CandidateReceipt
                {
                    familyId = bundle.Definitions[index].FamilyId,
                    modelAsset = bundle.Definitions[index].ModelPath,
                    modelSha256 = Sha256Asset(bundle.Definitions[index].ModelPath),
                    humanoidAvatar = animator == null || animator.avatar == null
                        ? string.Empty
                        : animator.avatar.name,
                    staticRootMotionOnly = bundle.FatherV18StaticOnly
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
                isolatedScene = bundle.QaScenePath,
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
                motionMapping = bundle.FatherV18StaticOnly
                    ? "actual Father OfficeRuntimeAgent position + direction -> static V18 root translation/yaw; no limb rig"
                    : bundle.FatherV18MotionOnly
                        ? "actual Father OfficeRuntimeAgent position/direction/GaitPhase01 -> native run-644 body/skin/clip; idle-0 Humanoid clip only while stationary; applyRootMotion=false"
                    : "Position + LastActualDisplacement + GaitPhase01 + CurrentDirection -> Family3DWalkActor",
                scalePolicy = bundle.FatherV18StaticOnly
                    ? "every frame source Father sprite projected bounds height == V18 renderer projected bounds height; <=0.5% error; grounded"
                    : bundle.FatherV18MotionOnly
                        ? "one locked uniform scale calibrated from idle-0 projected bounds to the live Father sprite; no per-pose rescaling"
                    : "live production SpriteRenderer bounds projected viewport height",
                source2DPolicy =
                    "QA-only Renderer.forceRenderingOff; sorting layer/order and transform depth never assigned",
                supportedPhases = new[] { "Idle(standing)", "Navigating(walking)" },
                unsupportedSeatedPolicy =
                    "seat approach/transitions/work/egress skip 3D and restore original 2D presentation",
                fatherV18StaticOnly = bundle.FatherV18StaticOnly,
                fatherV18MotionOnly = bundle.FatherV18MotionOnly,
                staticMapScaleTolerance = Family3DStarterOfficeCandidateQa.StaticMapScaleTolerance,
                staticTextureAsset = bundle.FatherV18StaticOnly
                    ? FatherV18StaticTexturePath
                    : bundle.FatherV18MotionOnly
                        ? FatherV18MotionTexturePath
                        : string.Empty,
                staticTextureSha256 = bundle.FatherV18StaticOnly
                    ? Sha256Asset(FatherV18StaticTexturePath)
                    : bundle.FatherV18MotionOnly
                        ? Sha256Asset(FatherV18MotionTexturePath)
                        : string.Empty,
                idleClipAsset = bundle.FatherV18MotionOnly
                    ? FatherV18MotionIdleClipPath
                    : string.Empty,
                idleClipSha256 = bundle.FatherV18MotionOnly
                    ? Sha256Asset(FatherV18MotionIdleClipPath)
                    : string.Empty,
                idleClipName = bundle.IdleClip == null ? string.Empty : bundle.IdleClip.name,
                idleClipLength = bundle.IdleClip == null ? 0f : bundle.IdleClip.length,
                walkClipAsset = bundle.FatherV18StaticOnly
                    ? string.Empty
                    : bundle.FatherV18MotionOnly
                        ? FatherV18MotionRunClipPath
                        : WalkClipPath,
                walkClipSha256 = bundle.FatherV18StaticOnly
                    ? string.Empty
                    : bundle.FatherV18MotionOnly
                        ? Sha256Asset(FatherV18MotionRunClipPath)
                        : Sha256Asset(WalkClipPath),
                walkClipName = bundle.WalkClip == null ? string.Empty : bundle.WalkClip.name,
                walkClipLength = bundle.WalkClip == null ? 0f : bundle.WalkClip.length,
                lockedCycleSeconds = bundle.FatherV18StaticOnly
                    ? 0f
                    : Family3DWalkActor.LockedCycleSeconds,
                candidates = assets,
                buildResult = report.summary.result.ToString(),
                buildBytes = (long)report.summary.totalSize
            }, true);
            File.WriteAllText(Path.Combine(buildRoot, "build-receipt.json"), json);
        }

        private static string ResolveBuildRoot(
            bool fatherV18StaticOnly,
            bool fatherV18MotionOnly = false)
        {
            string projectRoot = ProjectPath(string.Empty);
            string root = Path.GetFullPath(Path.Combine(
                projectRoot,
                fatherV18StaticOnly
                    ? FatherV18StaticDefaultBuildRoot
                    : fatherV18MotionOnly
                        ? FatherV18MotionDefaultBuildRoot
                        : DefaultBuildRoot));
            string outputArgument = fatherV18StaticOnly
                ? "-family3d-father-v18-static-qa-build-output"
                : fatherV18MotionOnly
                    ? "-family3d-father-v18-motion-qa-build-output"
                    : "-family3d-starter-office-qa-build-output";
            string[] args = Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length - 1; index++)
            {
                if (!string.Equals(
                        args[index],
                        outputArgument,
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
            public bool fatherV18StaticOnly;
            public bool fatherV18MotionOnly;
            public float staticMapScaleTolerance;
            public string staticTextureAsset;
            public string staticTextureSha256;
            public string idleClipAsset;
            public string idleClipSha256;
            public string idleClipName;
            public float idleClipLength;
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
            public bool staticRootMotionOnly;
        }

        private sealed class AssetBundle
        {
            public AssetBundle(
                GameObject[] prefabs,
                AnimationClip walkClip,
                AnimationClip idleClip,
                Texture2D staticAlbedo,
                CandidateDefinition[] definitions,
                bool fatherV18StaticOnly,
                bool fatherV18MotionOnly,
                string qaScenePath)
            {
                Prefabs = prefabs;
                WalkClip = walkClip;
                IdleClip = idleClip;
                StaticAlbedo = staticAlbedo;
                Definitions = definitions;
                FatherV18StaticOnly = fatherV18StaticOnly;
                FatherV18MotionOnly = fatherV18MotionOnly;
                QaScenePath = qaScenePath;
            }

            public GameObject[] Prefabs { get; }
            public AnimationClip WalkClip { get; }
            public AnimationClip IdleClip { get; }
            public Texture2D StaticAlbedo { get; }
            public CandidateDefinition[] Definitions { get; }
            public bool FatherV18StaticOnly { get; }
            public bool FatherV18MotionOnly { get; }
            public string QaScenePath { get; }
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
