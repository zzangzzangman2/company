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
            "Assets/FamilyCompany/Experimental/Family3DPrototype/Scenes/Family3DFatherV18CleanBipedStableArmWalkMapQaV74.unity";
        public const string FatherV19MotionQaScenePath =
            "Assets/FamilyCompany/Experimental/Family3DPrototype/Scenes/Family3DFatherV19MeshyOnePackage613MapQa.unity";
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
        // V74 restores V72's exact lower-body, torso, direction, stride and action-613 retarget
        // contract. The user rejected V73 because changing the whole model/skin package also
        // changed the legs. Only the final arm post-process changes: V72's behind-body tuck is
        // replaced by straight rigid arms with a small fixed-axis opposite upper-arm swing.
        public const string FatherV18MotionModelPath =
            "Assets/FamilyCompany/Experimental/Family3DPrototype/Candidates/FatherV18CleanBipedRigV4/father-v18-clean-biped-rig-v4.fbx";
        public const float FatherV18CasualWalkFacingOffsetDegrees = -16.9219f;
        public const float FatherV18CasualWalkStrideOfficeUnits = 0.675f;
        public const string FatherV18MotionIdleClipPath =
            "Assets/FamilyCompany/Experimental/Family3DPrototype/Candidates/FatherV18HiggsfieldCasualWalk613/father-v18-higgsfield-casual-walk-613-idle.fbx";
        public const string FatherV18MotionWalkClipPath =
            "Assets/FamilyCompany/Experimental/Family3DPrototype/Candidates/FatherV18HiggsfieldCasualWalk613/father-v18-higgsfield-casual-walk-613-walk.fbx";
        public const string FatherV18MotionTexturePath =
            "Assets/FamilyCompany/Experimental/Family3DPrototype/Candidates/FatherV18HiggsfieldStatic/father-v18-higgsfield-static-albedo.png";
        public const string FatherV19MotionModelPath =
            "Assets/FamilyCompany/Experimental/Family3DPrototype/Candidates/FatherV19MeshyOnePackage613/father-v19-meshy-one-package-613.fbx";
        public const string FatherV19MotionTexturePath =
            "Assets/FamilyCompany/Experimental/Family3DPrototype/Candidates/FatherV19MeshyOnePackage613/father-v19-meshy-one-package-albedo.png";
        public const float FatherV19FacingOffsetDegrees = 0f;
        // One forced-map circuit is 7.950477 office units. Ten authored cycles per circuit both
        // matches the measured planted-foot velocity (least-squares optimum 0.812345) and makes
        // the multi-direction proof close on the same pose without a GIF seam.
        public const float FatherV19StrideOfficeUnits = 0.7950477f;
        public const float FatherV19AuthoredCycleSeconds = 1.4f;
        public const string MotherModelPath =
            "Assets/FamilyCompany/Experimental/Family3DPrototype/Candidates/MotherV1/mother-blender-humanoid-v1.fbx";
        public const string DefaultBuildRoot =
            "Artifacts/Family3DStarterOfficeCandidateQaV1/BuildRun1";
        public const string FatherV18StaticDefaultBuildRoot =
            "Artifacts/Family3DStarterOfficeCandidateQaV1/FatherV18HiggsfieldStaticMapBuildV18";
        public const string FatherV18MotionDefaultBuildRoot =
            "Artifacts/Family3DStarterOfficeCandidateQaV1/FatherV18CleanBipedStableArmWalkMapBuildV74";
        public const string FatherV19MotionDefaultBuildRoot =
            "Artifacts/Family3DStarterOfficeCandidateQaV1/FatherV19MeshyOnePackage613MapBuildV2";

        /// <summary>
        /// The moving proof must use the exact imported static-model surface material. V61/V62
        /// replaced it with Unlit/Texture and made the approved teal/charcoal appearance dark and
        /// flat. This scene-referenced clone keeps the same shader and properties as the static FBX.
        /// </summary>
        public const string ExactAlbedoMaterialPath =
            "Assets/FamilyCompany/Experimental/Family3DPrototype/Materials/" +
            "FatherV18HiggsfieldStaticSurface.mat";

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
                Debug.Log("FAMILY_3D_FATHER_V18_CLEAN_BIPED_STABLE_ARM_WALK_MAP_QA_BUILD: PASS");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError(
                    "FAMILY_3D_FATHER_V18_CLEAN_BIPED_STABLE_ARM_WALK_MAP_QA_BUILD: FAIL | " +
                    exception.Message);
                EditorApplication.Exit(1);
            }
        }

        public static void BuildFatherV19MotionFromCommandLine()
        {
            try
            {
                Build(ResolveBuildRoot(false, false, true), false, false, true);
                Debug.Log("FAMILY_3D_FATHER_V19_MESHY_ONE_PACKAGE_613_MAP_QA_BUILD: PASS");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError(
                    "FAMILY_3D_FATHER_V19_MESHY_ONE_PACKAGE_613_MAP_QA_BUILD: FAIL | " +
                    exception.Message);
                EditorApplication.Exit(1);
            }
        }

        private static void Build(
            string buildRoot,
            bool fatherV18StaticOnly,
            bool fatherV18MotionOnly = false,
            bool fatherV19MotionOnly = false)
        {
            ThrowIfInteractiveSceneIsDirty();
            string productionSceneBefore = Sha256Asset(ProductionScenePath);
            string previewSceneBefore = Sha256Asset(PreviewScenePath);
            string buildSettingsBefore = CaptureBuildSettings();
            string buildSettingsFileBefore = Sha256File(ProjectPath("ProjectSettings/EditorBuildSettings.asset"));

            AssetBundle bundle = LoadAssets(
                fatherV18StaticOnly,
                fatherV18MotionOnly,
                fatherV19MotionOnly);
            int qaLayer = CreateIsolatedQaScene(bundle);

            Directory.CreateDirectory(buildRoot);
            string executablePath = Path.Combine(
                buildRoot,
                fatherV18StaticOnly
                    ? "FamilyCompanyFatherV18HiggsfieldStaticMapQa.exe"
                    : fatherV18MotionOnly
                        ? "FamilyCompanyFatherV18StableArmWalkV74MapQa.exe"
                    : fatherV19MotionOnly
                        ? "FamilyCompanyFatherV19MeshyOnePackage613MapQa.exe"
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
            bool fatherV18MotionOnly = false,
            bool fatherV19MotionOnly = false)
        {
            int exclusiveModes = (fatherV18StaticOnly ? 1 : 0) +
                                 (fatherV18MotionOnly ? 1 : 0) +
                                 (fatherV19MotionOnly ? 1 : 0);
            if (exclusiveModes > 1)
                throw new InvalidOperationException("Father QA candidate modes are mutually exclusive.");
            CandidateDefinition[] definitions = fatherV18StaticOnly
                ? new[] { new CandidateDefinition("father", FatherV18StaticModelPath) }
                : fatherV18MotionOnly
                    ? new[] { new CandidateDefinition("father", FatherV18MotionModelPath) }
                : fatherV19MotionOnly
                    ? new[] { new CandidateDefinition("father", FatherV19MotionModelPath) }
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
                AnimationClip motionWalkClip = LoadHumanClip(
                    FatherV18MotionWalkClipPath,
                    "Casual_Walk_inplace");
                return new AssetBundle(
                    prefabs,
                    motionWalkClip,
                    idleClip,
                    texture,
                    definitions,
                    false,
                    true,
                    false,
                    FatherV18MotionQaScenePath);
            }

            if (fatherV19MotionOnly)
            {
                AssetDatabase.ImportAsset(
                    FatherV19MotionTexturePath,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(FatherV19MotionTexturePath);
                if (texture == null)
                    throw new InvalidOperationException(
                        "Father V19 one-package texture did not load: " + FatherV19MotionTexturePath);
                AnimationClip motionWalkClip = LoadHumanClip(
                    FatherV19MotionModelPath,
                    "Casual_Walk_inplace");
                return new AssetBundle(
                    prefabs,
                    motionWalkClip,
                    null,
                    texture,
                    definitions,
                    false,
                    false,
                    true,
                    FatherV19MotionQaScenePath);
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
        /// Clones the approved static FBX material into a scene-referenced asset.
        /// </summary>
        private static Material EnsureExactAlbedoMaterial()
        {
            GameObject staticPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                FatherV18StaticModelPath);
            Renderer staticRenderer = staticPrefab == null
                ? null
                : staticPrefab.GetComponentInChildren<Renderer>(true);
            Material source = staticRenderer == null ? null : staticRenderer.sharedMaterial;
            if (source == null || source.shader == null)
                throw new InvalidOperationException(
                    "Father V18 static source material could not be loaded from " +
                    FatherV18StaticModelPath);
            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(
                FatherV18StaticTexturePath);
            if (albedo == null)
                throw new InvalidOperationException(
                    "Father V18 static albedo could not be loaded from " +
                    FatherV18StaticTexturePath);

            string directory = Path.GetDirectoryName(ProjectPath(ExactAlbedoMaterialPath));
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var existing = AssetDatabase.LoadAssetAtPath<Material>(ExactAlbedoMaterialPath);
            if (existing == null)
            {
                existing = new Material(source);
                AssetDatabase.CreateAsset(existing, ExactAlbedoMaterialPath);
            }
            else
            {
                EditorUtility.CopySerialized(source, existing);
            }
            existing.name = "FatherV18HiggsfieldStaticSurface";
            existing.mainTexture = albedo;
            existing.color = Color.white;
            EditorUtility.SetDirty(existing);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(
                ExactAlbedoMaterialPath,
                ImportAssetOptions.ForceSynchronousImport);
            var created = AssetDatabase.LoadAssetAtPath<Material>(ExactAlbedoMaterialPath);
            if (created == null)
                throw new InvalidOperationException(
                    "Could not create static-surface material " + ExactAlbedoMaterialPath + ".");
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
                    qaLayer,
                    FatherV18CasualWalkFacingOffsetDegrees,
                    FatherV18CasualWalkStrideOfficeUnits,
                    false,
                    true,
                    true);
            else if (bundle.FatherV19MotionOnly)
                qa.ConfigureFatherHiggsfieldIdleRun(
                    bundle.Prefabs[0],
                    bundle.StaticAlbedo,
                    ResolveImportedSurfaceMaterial(bundle.Prefabs[0]),
                    null,
                    bundle.WalkClip,
                    camera,
                    qaLayer,
                    FatherV19FacingOffsetDegrees,
                    FatherV19StrideOfficeUnits,
                    false,
                    false,
                    false,
                    sourceAuthoredCycleSeconds: FatherV19AuthoredCycleSeconds);
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

        private static Material ResolveImportedSurfaceMaterial(GameObject prefab)
        {
            Renderer renderer = prefab == null
                ? null
                : prefab.GetComponentInChildren<Renderer>(true);
            Material material = renderer == null ? null : renderer.sharedMaterial;
            if (material == null || material.shader == null)
                throw new InvalidOperationException(
                    "Imported Father V19 surface material is missing from the one-package FBX.");
            return material;
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
                directionMapping =
                    bundle.FatherV19MotionOnly
                        ? "measured QA ground displacement -> LookRotation + candidate-specific measured facing offset; 360 degrees/second corner blend"
                        : "measured QA ground displacement -> LookRotation + restored V72 clean-rig -16.9219 degree measured model-forward offset; 360 degrees/second corner blend",
                motionMapping = bundle.FatherV18StaticOnly
                    ? "actual Father OfficeRuntimeAgent position + direction -> static V18 root translation/yaw; no limb rig"
                    : bundle.FatherV18MotionOnly
                        ? "actual Father OfficeRuntimeAgent position/direction/GaitPhase01 -> V72 clean V4 lower-body/torso sanitation and action 613 at poseStrength 1; only final arms use stable V66 body-side swing"
                    : bundle.FatherV19MotionOnly
                        ? "actual Father position/direction/GaitDistance -> unchanged one-package Meshy skin, bind skeleton, and authored action 613; no retarget, sanitation, IK rewrite, or procedural limb motion"
                    : "Position + LastActualDisplacement + GaitPhase01 + CurrentDirection -> Family3DWalkActor",
                scalePolicy = bundle.FatherV18StaticOnly
                    ? "every frame source Father sprite projected bounds height == V18 renderer projected bounds height; <=0.5% error; grounded"
                    : bundle.FatherV18MotionOnly
                        ? "one locked uniform scale calibrated from the restored V72 clean V4 idle bounds to the live Father sprite; no per-pose rescaling"
                    : bundle.FatherV19MotionOnly
                        ? "one locked uniform scale calibrated from the one-package authored pose to the live Father sprite; no per-pose rescaling"
                    : "live production SpriteRenderer bounds projected viewport height",
                source2DPolicy =
                    "QA-only Renderer.forceRenderingOff; sorting layer/order and transform depth never assigned",
                supportedPhases = new[] { "Idle(standing)", "Navigating(walking)" },
                unsupportedSeatedPolicy =
                    "seat approach/transitions/work/egress skip 3D and restore original 2D presentation",
                fatherV18StaticOnly = bundle.FatherV18StaticOnly,
                fatherV18MotionOnly = bundle.FatherV18MotionOnly,
                fatherV19MotionOnly = bundle.FatherV19MotionOnly,
                staticMapScaleTolerance = Family3DStarterOfficeCandidateQa.StaticMapScaleTolerance,
                staticTextureAsset = bundle.FatherV18StaticOnly
                    ? FatherV18StaticTexturePath
                    : bundle.FatherV18MotionOnly
                        ? FatherV18MotionTexturePath
                    : bundle.FatherV19MotionOnly
                        ? FatherV19MotionTexturePath
                        : string.Empty,
                staticTextureSha256 = bundle.FatherV18StaticOnly
                    ? Sha256Asset(FatherV18StaticTexturePath)
                    : bundle.FatherV18MotionOnly
                        ? Sha256Asset(FatherV18MotionTexturePath)
                    : bundle.FatherV19MotionOnly
                        ? Sha256Asset(FatherV19MotionTexturePath)
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
                        ? FatherV18MotionWalkClipPath
                    : bundle.FatherV19MotionOnly
                        ? FatherV19MotionModelPath
                        : WalkClipPath,
                walkClipSha256 = bundle.FatherV18StaticOnly
                    ? string.Empty
                    : bundle.FatherV18MotionOnly
                        ? Sha256Asset(FatherV18MotionWalkClipPath)
                    : bundle.FatherV19MotionOnly
                        ? Sha256Asset(FatherV19MotionModelPath)
                        : Sha256Asset(WalkClipPath),
                walkClipName = bundle.WalkClip == null ? string.Empty : bundle.WalkClip.name,
                walkClipLength = bundle.WalkClip == null ? 0f : bundle.WalkClip.length,
                lockedCycleSeconds = bundle.FatherV18StaticOnly
                    ? 0f
                    : bundle.FatherV18MotionOnly
                        ? Family3DWalkActor.LockedCycleSeconds
                    : bundle.FatherV19MotionOnly
                        ? FatherV19AuthoredCycleSeconds
                        : Family3DWalkActor.LockedCycleSeconds,
                nativeModelClipPackage = bundle.FatherV19MotionOnly,
                motionPostProcessing = bundle.FatherV18MotionOnly
                    ? "V72 legs/pelvis/torso/head unchanged; behind-body tuck disabled; straight rigid arms with fixed-axis opposite upper-arm swing 6 degrees; elbow/wrist/finger/outward/tuck correction zero"
                    : bundle.FatherV19MotionOnly
                        ? "none; native one-package skin and action sampled at poseStrength 1"
                    : string.Empty,
                candidates = assets,
                buildResult = report.summary.result.ToString(),
                buildBytes = (long)report.summary.totalSize
            }, true);
            File.WriteAllText(Path.Combine(buildRoot, "build-receipt.json"), json);
        }

        private static string ResolveBuildRoot(
            bool fatherV18StaticOnly,
            bool fatherV18MotionOnly = false,
            bool fatherV19MotionOnly = false)
        {
            string projectRoot = ProjectPath(string.Empty);
            string root = Path.GetFullPath(Path.Combine(
                projectRoot,
                fatherV18StaticOnly
                    ? FatherV18StaticDefaultBuildRoot
                    : fatherV18MotionOnly
                        ? FatherV18MotionDefaultBuildRoot
                    : fatherV19MotionOnly
                        ? FatherV19MotionDefaultBuildRoot
                        : DefaultBuildRoot));
            string outputArgument = fatherV18StaticOnly
                ? "-family3d-father-v18-static-qa-build-output"
                : fatherV18MotionOnly
                    ? "-family3d-father-v18-motion-qa-build-output"
                : fatherV19MotionOnly
                    ? "-family3d-father-v19-motion-qa-build-output"
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
            public bool fatherV19MotionOnly;
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
            public bool nativeModelClipPackage;
            public string motionPostProcessing;
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
                bool fatherV19MotionOnly,
                string qaScenePath)
            {
                Prefabs = prefabs;
                WalkClip = walkClip;
                IdleClip = idleClip;
                StaticAlbedo = staticAlbedo;
                Definitions = definitions;
                FatherV18StaticOnly = fatherV18StaticOnly;
                FatherV18MotionOnly = fatherV18MotionOnly;
                FatherV19MotionOnly = fatherV19MotionOnly;
                QaScenePath = qaScenePath;
            }

            public GameObject[] Prefabs { get; }
            public AnimationClip WalkClip { get; }
            public AnimationClip IdleClip { get; }
            public Texture2D StaticAlbedo { get; }
            public CandidateDefinition[] Definitions { get; }
            public bool FatherV18StaticOnly { get; }
            public bool FatherV18MotionOnly { get; }
            public bool FatherV19MotionOnly { get; }
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
