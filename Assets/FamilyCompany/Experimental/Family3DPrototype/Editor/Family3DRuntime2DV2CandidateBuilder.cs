using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using FamilyCompany.Experimental.Family3D;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace FamilyCompany.Experimental.Family3D.Editor
{
    /// <summary>
    /// Builds a strictly isolated showroom from the four Runtime-2D V2 candidates.
    /// It never changes the production character catalog or default player build.
    /// </summary>
    public static class Family3DRuntime2DV2CandidateBuilder
    {
        public const string Contract = "FC-FAMILY-3D-RUNTIME2D-V2-CANDIDATE-LAB-V1";
        public const string ScenePath =
            "Assets/FamilyCompany/Experimental/Family3DPrototype/Scenes/Family3DRuntime2DV2CandidateLab.unity";
        public const string WalkClipPath =
            "Assets/FamilyCompany/Editor/PlayerWalkHumanoidAuthoring/PlayerHumanoidWalk.fbx";
        public const string GeneratedMaterialRoot =
            "Assets/FamilyCompany/Experimental/Family3DPrototype/Generated/Runtime2DV2CandidateMaterials";
        public const string DefaultBuildRoot = "Artifacts/Family3DRuntime2DV2/BuildRun1";

        private static readonly CandidateDefinition[] Definitions =
        {
            new CandidateDefinition(
                "PLAYER",
                Family3DPrototypeModelImporter.PlayerRuntime2DV2CandidateModelPath,
                Family3DPrototypeModelImporter.CandidateRoot +
                "PlayerV4/player-runtime2d-identity-v4-atlas.png",
                -6f,
                3.20f,
                new Color(0.98f, 0.32f, 0.28f)),
            new CandidateDefinition(
                "FATHER",
                Family3DPrototypeModelImporter.FatherRuntime2DV2CandidateModelPath,
                Family3DPrototypeModelImporter.CandidateRoot +
                "FatherV2/father-blender-identity-v2-atlas.png",
                -2f,
                3.65f,
                new Color(0.25f, 0.78f, 0.74f)),
            new CandidateDefinition(
                "MOTHER",
                Family3DPrototypeModelImporter.MotherRuntime2DV2CandidateModelPath,
                Family3DPrototypeModelImporter.CandidateRoot +
                "MotherV2/mother-blender-identity-v2-atlas.png",
                2f,
                3.48f,
                new Color(1f, 0.62f, 0.53f)),
            new CandidateDefinition(
                "OLDER SISTER",
                Family3DPrototypeModelImporter.OlderSisterRuntime2DV2CandidateModelPath,
                Family3DPrototypeModelImporter.CandidateRoot +
                "OlderSisterV2/older-sister-blender-identity-v2-atlas.png",
                6f,
                3.42f,
                new Color(0.58f, 0.65f, 1f))
        };

        [MenuItem("Family Company/Experimental/Build Family Runtime-2D V2 Candidate Lab")]
        public static void BuildMenu()
        {
            Build(ResolveBuildRoot());
        }

        public static void BuildFromCommandLine()
        {
            try
            {
                Build(ResolveBuildRoot());
                Debug.Log("FAMILY_3D_RUNTIME2D_V2_CANDIDATE_BUILD: PASS");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("FAMILY_3D_RUNTIME2D_V2_CANDIDATE_BUILD: FAIL | " + exception.Message);
                EditorApplication.Exit(1);
            }
        }

        private static void Build(string buildRoot)
        {
            ThrowIfInteractiveSceneIsDirty();
            AnimationClip walkClip = LoadSharedWalkClip();
            var prefabs = new GameObject[Definitions.Length];
            var importReceipts = new Family3DIdentityCandidateValidator.CandidateReceipt[Definitions.Length];
            for (var index = 0; index < Definitions.Length; index++)
            {
                CandidateDefinition definition = Definitions[index];
                importReceipts[index] = Family3DIdentityCandidateValidator.Validate(
                    definition.Id,
                    definition.ModelPath,
                    definition.AtlasPath);
                prefabs[index] = AssetDatabase.LoadAssetAtPath<GameObject>(definition.ModelPath);
                if (prefabs[index] == null)
                    throw new InvalidOperationException("Candidate prefab did not load: " + definition.ModelPath);
            }

            EnsureAssetFolder("Assets/FamilyCompany/Experimental/Family3DPrototype/Scenes");
            EnsureAssetFolder(GeneratedMaterialRoot);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Family3DRuntime2DV2CandidateLab";
            Camera camera = CreateCamera();
            CreateLighting();
            CreateFloorAndGrid();

            var actors = new Family3DWalkActor[Definitions.Length];
            var appliedScales = new float[Definitions.Length];
            for (var index = 0; index < Definitions.Length; index++)
            {
                actors[index] = CreateActor(
                    scene,
                    prefabs[index],
                    walkClip,
                    Definitions[index],
                    out appliedScales[index]);
            }

            var directorObject = new GameObject("Family3DRuntime2DV2ShowroomDirector");
            var director = directorObject.AddComponent<Family3DShowroomDirector>();
            director.Configure(camera, actors);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Could not save isolated Runtime-2D V2 scene: " + ScenePath);

            Directory.CreateDirectory(buildRoot);
            string executablePath = Path.Combine(buildRoot, "FamilyCompany3DRuntime2DV2CandidateLab.exe");
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = executablePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException(
                    "Isolated identity Windows build failed: " + report.summary.result +
                    " errors=" + report.summary.totalErrors);

            WriteBuildReceipt(
                buildRoot,
                executablePath,
                walkClip,
                report,
                importReceipts,
                appliedScales);
        }

        private static AnimationClip LoadSharedWalkClip()
        {
            AssetDatabase.ImportAsset(
                WalkClipPath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(WalkClipPath);
            if (clip == null)
            {
                clip = AssetDatabase.LoadAllAssetsAtPath(WalkClipPath)
                    .OfType<AnimationClip>()
                    .FirstOrDefault(candidate =>
                        !candidate.name.StartsWith("__preview__", StringComparison.Ordinal));
            }
            if (clip == null || !clip.isHumanMotion)
                throw new InvalidOperationException("Shared Humanoid walk clip is missing: " + WalkClipPath);
            return clip;
        }

        private static Family3DWalkActor CreateActor(
            Scene scene,
            GameObject prefab,
            AnimationClip walkClip,
            CandidateDefinition definition,
            out float appliedScale)
        {
            var host = new GameObject("Family3DRuntime2DV2_" + definition.Id.Replace(' ', '_'));
            Family3DShowroomDirector.EvaluateRoute(
                0d,
                out Vector3 startupOffset,
                out Vector3 startupDirection,
                out _);
            host.transform.position = definition.Center + startupOffset;
            host.transform.rotation = Quaternion.LookRotation(startupDirection, Vector3.up);

            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            if (model == null)
                throw new InvalidOperationException("Could not instantiate identity candidate: " + definition.Id);
            model.name = definition.Id.Replace(' ', '_') + "_CompleteSkinnedBody";
            model.transform.SetParent(host.transform, false);

            SkinnedMeshRenderer[] renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (renderers.Length != 1)
                throw new InvalidOperationException(
                    definition.Id + " expected one complete SkinnedMeshRenderer; found " + renderers.Length + ".");
            renderers[0].enabled = true;
            renderers[0].shadowCastingMode = ShadowCastingMode.On;
            renderers[0].receiveShadows = true;
            renderers[0].updateWhenOffscreen = true;
            renderers[0].sharedMaterial = CreateAtlasMaterial(definition);

            float sourceHeight = MeasureRendererHeight(renderers);
            appliedScale = definition.TargetHeight / Mathf.Max(sourceHeight, 0.0001f);
            model.transform.localScale = model.transform.localScale * appliedScale;

            Animator animator = model.GetComponent<Animator>();
            if (animator == null || animator.avatar == null ||
                !animator.avatar.isValid || !animator.avatar.isHuman)
                throw new InvalidOperationException(definition.Id + " lost its valid Humanoid Avatar on instantiate.");
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            GroundVisual(host.transform, model.transform, renderers);
            var actor = host.AddComponent<Family3DWalkActor>();
            actor.Configure(
                definition.Id,
                model.transform,
                animator,
                walkClip,
                definition.Center,
                definition.LabelColor);
            CreatePathOutline(definition);
            return actor;
        }

        private static float MeasureRendererHeight(Renderer[] renderers)
        {
            if (renderers.Length == 0)
                return 0f;
            Bounds bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
                bounds.Encapsulate(renderers[index].bounds);
            return bounds.size.y;
        }

        private static Material CreateAtlasMaterial(CandidateDefinition definition)
        {
            Texture2D atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(definition.AtlasPath);
            if (atlas == null)
                throw new InvalidOperationException("Candidate atlas did not load: " + definition.AtlasPath);

            string safeId = definition.Id.Replace(' ', '_');
            string path = GeneratedMaterialRoot + "/" + safeId + "_Runtime2DV2Atlas.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Standard");
            if (shader == null)
                throw new InvalidOperationException("Required Standard shader was not found.");
            if (material == null)
            {
                material = new Material(shader) { name = safeId + "_Runtime2DV2Atlas" };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            material.color = Color.white;
            material.mainTexture = atlas;
            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", 0f);
            if (material.HasProperty("_Glossiness"))
                material.SetFloat("_Glossiness", 0.04f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void GroundVisual(Transform host, Transform visual, Renderer[] renderers)
        {
            Bounds bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
                bounds.Encapsulate(renderers[index].bounds);
            visual.position += Vector3.up * (host.position.y - bounds.min.y);
        }

        private static Camera CreateCamera()
        {
            var cameraObject = new GameObject("Family3DRuntime2DV2ReviewCamera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 7.6f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.075f, 0.085f, 0.105f, 1f);
            camera.allowHDR = false;
            camera.allowMSAA = true;
            cameraObject.AddComponent<AudioListener>();
            camera.transform.position = new Vector3(11.31f, 14f, -11.31f);
            camera.transform.LookAt(new Vector3(0f, 1.55f, 0f));
            return camera;
        }

        private static void CreateLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.56f, 0.61f, 0.72f);
            RenderSettings.ambientEquatorColor = new Color(0.34f, 0.37f, 0.43f);
            RenderSettings.ambientGroundColor = new Color(0.18f, 0.19f, 0.22f);
            RenderSettings.ambientIntensity = 0.8f;
            RenderSettings.reflectionIntensity = 0f;

            var keyObject = new GameObject("Runtime2DV2KeyLight");
            var key = keyObject.AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.15f;
            key.color = new Color(1f, 0.94f, 0.86f);
            key.shadows = LightShadows.Soft;
            key.shadowStrength = 0.62f;
            keyObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

            var fillObject = new GameObject("Runtime2DV2FillLight");
            var fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.42f;
            fill.color = new Color(0.56f, 0.69f, 1f);
            fill.shadows = LightShadows.None;
            fillObject.transform.rotation = Quaternion.Euler(38f, 148f, 0f);
        }

        private static void CreateFloorAndGrid()
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Runtime2DV2NeutralReviewFloor";
            floor.transform.position = new Vector3(0f, -0.015f, 0f);
            floor.transform.localScale = new Vector3(2.8f, 1f, 2.0f);
            Object.DestroyImmediate(floor.GetComponent<Collider>());
            floor.GetComponent<Renderer>().sharedMaterial = CreateMaterial(
                "identity_review_floor",
                "Standard",
                new Color(0.19f, 0.205f, 0.235f));

            Material minor = CreateMaterial(
                "identity_grid_minor",
                "Sprites/Default",
                new Color(0.36f, 0.39f, 0.45f, 0.55f));
            Material major = CreateMaterial(
                "identity_grid_major",
                "Sprites/Default",
                new Color(0.56f, 0.60f, 0.69f, 0.72f));
            for (var coordinate = -14; coordinate <= 14; coordinate++)
            {
                Material material = coordinate % 5 == 0 ? major : minor;
                float width = coordinate % 5 == 0 ? 0.018f : 0.008f;
                CreateLine(
                    "Runtime2DV2GridX_" + coordinate,
                    new[] { new Vector3(coordinate, 0.002f, -10f), new Vector3(coordinate, 0.002f, 10f) },
                    material,
                    width,
                    false);
                CreateLine(
                    "Runtime2DV2GridZ_" + coordinate,
                    new[] { new Vector3(-14f, 0.002f, coordinate), new Vector3(14f, 0.002f, coordinate) },
                    material,
                    width,
                    false);
            }
        }

        private static void CreatePathOutline(CandidateDefinition definition)
        {
            float half = Family3DShowroomDirector.PathHalfExtent;
            Vector3 center = definition.Center + Vector3.up * 0.012f;
            Vector3[] points =
            {
                center + new Vector3(-half, 0f, -half),
                center + new Vector3(half, 0f, -half),
                center + new Vector3(half, 0f, half),
                center + new Vector3(-half, 0f, half)
            };
            CreateLine(
                definition.Id.Replace(' ', '_') + "_Runtime2DV2CandidateRoute",
                points,
                CreateMaterial(
                    "identity_path_" + definition.Id.ToLowerInvariant().Replace(' ', '_'),
                    "Sprites/Default",
                    definition.LabelColor * 0.8f),
                0.035f,
                true);
        }

        private static Material CreateMaterial(string name, string shaderName, Color color)
        {
            string path = GeneratedMaterialRoot + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
                throw new InvalidOperationException("Review shader is missing: " + shaderName);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.color = color;
            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", 0f);
            if (material.HasProperty("_Glossiness"))
                material.SetFloat("_Glossiness", 0.08f);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        private static void CreateLine(
            string name,
            Vector3[] points,
            Material material,
            float width,
            bool loop)
        {
            var host = new GameObject(name);
            var line = host.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = loop;
            line.positionCount = points.Length;
            line.SetPositions(points);
            line.startWidth = width;
            line.endWidth = width;
            line.sharedMaterial = material;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.numCornerVertices = 2;
        }

        private static string ResolveBuildRoot()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ??
                                 throw new InvalidOperationException("Could not resolve canonical project root.");
            string root = Path.GetFullPath(Path.Combine(projectRoot, DefaultBuildRoot));
            string[] args = Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length; index++)
            {
                if (!string.Equals(args[index], "-family3d-build-output", StringComparison.OrdinalIgnoreCase) ||
                    index + 1 >= args.Length)
                    continue;
                root = Path.GetFullPath(args[++index]);
            }
            string allowedRoot = Path.GetFullPath(Path.Combine(projectRoot, "Artifacts/Family3DRuntime2DV2"))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string fullRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!fullRoot.StartsWith(allowedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Refusing non-isolated Runtime-2D V2 build output: " + fullRoot);
            return fullRoot;
        }

        private static void WriteBuildReceipt(
            string buildRoot,
            string executablePath,
            AnimationClip walkClip,
            BuildReport report,
            Family3DIdentityCandidateValidator.CandidateReceipt[] imports,
            float[] appliedScales)
        {
            var candidates = new CandidateBuildReceipt[Definitions.Length];
            for (var index = 0; index < Definitions.Length; index++)
            {
                CandidateDefinition definition = Definitions[index];
                candidates[index] = new CandidateBuildReceipt
                {
                    familyId = definition.Id,
                    modelAsset = definition.ModelPath,
                    modelSha256 = Sha256(definition.ModelPath),
                    atlasAsset = definition.AtlasPath,
                    atlasSha256 = Sha256(definition.AtlasPath),
                    avatarValid = imports[index].avatarValid,
                    avatarHuman = imports[index].avatarHuman,
                    skinnedMeshRendererCount = imports[index].skinnedMeshRendererCount,
                    vertexCount = imports[index].vertexCount,
                    targetStandingHeight = definition.TargetHeight,
                    appliedUniformScale = appliedScales[index]
                };
            }
            var receipt = new BuildReceipt
            {
                contract = Contract,
                status = "ISOLATED_BUILD_COMPLETE_D3D11_VISUAL_REVIEW_REQUIRED",
                productionMutation = false,
                scene = ScenePath,
                executable = Path.GetFullPath(executablePath),
                sharedWalkClipAsset = WalkClipPath,
                sharedWalkClipName = walkClip.name,
                sharedWalkClipLength = walkClip.length,
                lockedCycleSeconds = Family3DWalkActor.LockedCycleSeconds,
                actorCount = candidates.Length,
                candidates = candidates,
                buildResult = report.summary.result.ToString(),
                buildBytes = (long)report.summary.totalSize,
                productionEligible = false
            };
            File.WriteAllText(
                Path.Combine(buildRoot, "build-receipt.json"),
                JsonUtility.ToJson(receipt, true));
        }

        private static string Sha256(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ??
                                 throw new InvalidOperationException("Could not resolve project root.");
            using (FileStream stream = File.OpenRead(Path.Combine(projectRoot, assetPath)))
            using (SHA256 hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", string.Empty);
        }

        private static void ThrowIfInteractiveSceneIsDirty()
        {
            if (Application.isBatchMode)
                return;
            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                if (scene.isDirty)
                    throw new InvalidOperationException("Refusing to replace an open dirty scene: " + scene.path);
            }
        }

        private static void EnsureAssetFolder(string assetPath)
        {
            string normalized = assetPath.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(normalized))
                return;
            int separator = normalized.LastIndexOf('/');
            string parent = normalized.Substring(0, separator);
            string name = normalized.Substring(separator + 1);
            EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        [Serializable]
        private sealed class BuildReceipt
        {
            public string contract;
            public string status;
            public bool productionMutation;
            public string scene;
            public string executable;
            public string sharedWalkClipAsset;
            public string sharedWalkClipName;
            public float sharedWalkClipLength;
            public float lockedCycleSeconds;
            public int actorCount;
            public CandidateBuildReceipt[] candidates;
            public string buildResult;
            public long buildBytes;
            public bool productionEligible;
        }

        [Serializable]
        private sealed class CandidateBuildReceipt
        {
            public string familyId;
            public string modelAsset;
            public string modelSha256;
            public string atlasAsset;
            public string atlasSha256;
            public bool avatarValid;
            public bool avatarHuman;
            public int skinnedMeshRendererCount;
            public int vertexCount;
            public float targetStandingHeight;
            public float appliedUniformScale;
        }

        private sealed class CandidateDefinition
        {
            public CandidateDefinition(
                string id,
                string modelPath,
                string atlasPath,
                float laneOffset,
                float targetHeight,
                Color labelColor)
            {
                Id = id;
                ModelPath = modelPath;
                AtlasPath = atlasPath;
                Center = new Vector3(laneOffset, 0f, laneOffset);
                TargetHeight = targetHeight;
                LabelColor = labelColor;
            }

            public string Id { get; }
            public string ModelPath { get; }
            public string AtlasPath { get; }
            public Vector3 Center { get; }
            public float TargetHeight { get; }
            public Color LabelColor { get; }
        }
    }
}
