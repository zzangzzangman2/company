using System;
using System.Collections.Generic;
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
    public static class Family3DPrototypeBuilder
    {
        public const string ScenePath =
            "Assets/FamilyCompany/Experimental/Family3DPrototype/Scenes/Family3DMotionLab.unity";
        public const string ModelPath = Family3DPrototypeModelImporter.ModelPath;
        public const string WalkClipPath =
            "Assets/FamilyCompany/Editor/PlayerWalkHumanoidAuthoring/PlayerHumanoidWalk.fbx";
        public const string GeneratedRoot =
            "Assets/FamilyCompany/Experimental/Family3DPrototype/Generated";
        public const string DefaultBuildRoot = "Artifacts/Family3DPrototypeV3/BuildRun3";
        public const string Contract = "FC-FAMILY-3D-MOTION-LAB-V3";

        [MenuItem("Family Company/Experimental/Build Isolated Family 3D Motion Lab")]
        public static void BuildMenu()
        {
            Build(ResolveBuildRoot());
        }

        public static void BuildFromCommandLine()
        {
            try
            {
                Build(ResolveBuildRoot());
                Debug.Log("FAMILY_3D_MOTION_LAB_BUILD: PASS");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("FAMILY_3D_MOTION_LAB_BUILD: FAIL | " + exception.Message);
                EditorApplication.Exit(1);
            }
        }

        private static void Build(string buildRoot)
        {
            ThrowIfInteractiveSceneIsDirty();
            EnsureModelImporter();
            GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (modelPrefab == null)
                throw new InvalidOperationException("3D proxy model is missing: " + ModelPath);
            Animator sourceAnimator = modelPrefab.GetComponent<Animator>();
            if (sourceAnimator == null || sourceAnimator.avatar == null ||
                !sourceAnimator.avatar.isValid || !sourceAnimator.avatar.isHuman)
                throw new InvalidOperationException("allinone.fbx did not import as a valid Humanoid Avatar.");

            AnimationClip walkClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(WalkClipPath);
            if (walkClip == null)
                walkClip = AssetDatabase.LoadAllAssetsAtPath(WalkClipPath)
                    .OfType<AnimationClip>()
                    .FirstOrDefault(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal));
            if (walkClip == null || !walkClip.isHumanMotion)
                throw new InvalidOperationException("Approved Mixamo walk clip is missing or not Humanoid: " + WalkClipPath);

            EnsureAssetFolder("Assets/FamilyCompany/Experimental/Family3DPrototype/Scenes");
            EnsureAssetFolder(GeneratedRoot);
            EnsureAssetFolder(GeneratedRoot + "/Materials");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Family3DMotionLab";
            Camera camera = CreateCamera();
            CreateLighting();
            CreateFloorAndGrid();

            Variant[] variants = CreateVariants();
            var actors = new Family3DWalkActor[variants.Length];
            for (var index = 0; index < variants.Length; index++)
                actors[index] = CreateActor(scene, modelPrefab, walkClip, variants[index]);

            var directorObject = new GameObject("Family3DShowroomDirector");
            var director = directorObject.AddComponent<Family3DShowroomDirector>();
            director.Configure(camera, actors);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Could not save isolated 3D scene: " + ScenePath);

            Directory.CreateDirectory(buildRoot);
            string executablePath = Path.Combine(buildRoot, "FamilyCompany3DMotionLab.exe");
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
                    "Isolated Windows build failed: " + report.summary.result +
                    " errors=" + report.summary.totalErrors);

            WriteBuildReceipt(buildRoot, executablePath, walkClip, sourceAnimator.avatar, report);
            Debug.Log(
                "FAMILY_3D_MOTION_LAB_BUILD: output=" + executablePath +
                " size=" + report.summary.totalSize +
                " clip=" + walkClip.name +
                " clipLength=" + walkClip.length.ToString("F6"));
        }

        private static void EnsureModelImporter()
        {
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null)
                throw new InvalidOperationException("ModelImporter is missing: " + ModelPath);

            bool changed = importer.animationType != ModelImporterAnimationType.Human ||
                           importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel ||
                           !importer.importAnimation || importer.optimizeGameObjects;
            if (!changed)
                return;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.importBlendShapes = false;
            importer.importVisibility = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.optimizeGameObjects = false;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.resampleCurves = false;
            importer.SaveAndReimport();
        }

        private static Camera CreateCamera()
        {
            var cameraObject = new GameObject("Family3DReviewCamera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 7.4f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.075f, 0.085f, 0.105f, 1f);
            camera.allowHDR = false;
            camera.allowMSAA = true;
            cameraObject.AddComponent<AudioListener>();
            camera.transform.position = new Vector3(11.31f, 14f, -11.31f);
            camera.transform.LookAt(new Vector3(0f, 1f, 0f));
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

            var keyObject = new GameObject("KeyLight");
            var key = keyObject.AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.15f;
            key.color = new Color(1f, 0.94f, 0.86f);
            key.shadows = LightShadows.Soft;
            key.shadowStrength = 0.62f;
            keyObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

            var fillObject = new GameObject("FillLight");
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
            floor.name = "NeutralReviewFloor";
            floor.transform.position = new Vector3(0f, -0.015f, 0f);
            floor.transform.localScale = new Vector3(2.8f, 1f, 2.0f);
            Object.DestroyImmediate(floor.GetComponent<Collider>());
            floor.GetComponent<Renderer>().sharedMaterial = CreateSolidMaterial(
                "review_floor",
                new Color(0.19f, 0.205f, 0.235f));

            Material minor = CreateLineMaterial("grid_minor", new Color(0.36f, 0.39f, 0.45f, 0.55f));
            Material major = CreateLineMaterial("grid_major", new Color(0.56f, 0.60f, 0.69f, 0.72f));
            for (var coordinate = -14; coordinate <= 14; coordinate++)
            {
                Material lineMaterial = coordinate % 5 == 0 ? major : minor;
                float width = coordinate % 5 == 0 ? 0.018f : 0.008f;
                CreateLine(
                    "GridX_" + coordinate,
                    new[] { new Vector3(coordinate, 0.002f, -10f), new Vector3(coordinate, 0.002f, 10f) },
                    lineMaterial,
                    width,
                    false);
                CreateLine(
                    "GridZ_" + coordinate,
                    new[] { new Vector3(-14f, 0.002f, coordinate), new Vector3(14f, 0.002f, coordinate) },
                    lineMaterial,
                    width,
                    false);
            }
        }

        private static Family3DWalkActor CreateActor(
            Scene scene,
            GameObject modelPrefab,
            AnimationClip walkClip,
            Variant variant)
        {
            var host = new GameObject("Family3D_" + variant.Id);
            Family3DShowroomDirector.EvaluateRoute(
                0d,
                out Vector3 startupOffset,
                out Vector3 startupDirection,
                out _);
            host.transform.position = variant.Center + startupOffset;
            host.transform.rotation = Quaternion.LookRotation(startupDirection, Vector3.up);

            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab, scene);
            if (model == null)
                throw new InvalidOperationException("Could not instantiate allinone proxy for " + variant.Id);
            model.name = variant.Id + "_UnifiedSkinnedBody";
            model.transform.SetParent(host.transform, false);
            model.transform.localScale = model.transform.localScale * variant.Scale;

            var enabled = new HashSet<string>(variant.Renderers, StringComparer.Ordinal);
            SkinnedMeshRenderer[] renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (SkinnedMeshRenderer renderer in renderers)
            {
                renderer.enabled = enabled.Contains(renderer.name);
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                if (renderer.enabled)
                    ApplyVariantMaterials(renderer, variant);
            }
            string[] missing = enabled.Where(name => renderers.All(renderer => renderer.name != name)).ToArray();
            if (missing.Length > 0)
                throw new InvalidOperationException(variant.Id + " renderer(s) missing: " + string.Join(", ", missing));

            Animator animator = model.GetComponent<Animator>();
            if (animator == null)
                animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            GroundVisual(host.transform, model.transform, renderers.Where(renderer => renderer.enabled).ToArray());

            var actor = host.AddComponent<Family3DWalkActor>();
            actor.Configure(variant.Id, model.transform, animator, walkClip, variant.Center, variant.LabelColor);
            CreatePathOutline(variant);
            return actor;
        }

        private static void GroundVisual(Transform host, Transform visual, Renderer[] renderers)
        {
            if (renderers.Length == 0)
                throw new InvalidOperationException(host.name + " has no enabled renderers.");
            Bounds bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
                bounds.Encapsulate(renderers[index].bounds);
            float delta = host.position.y - bounds.min.y;
            visual.position += Vector3.up * delta;
        }

        private static void CreatePathOutline(Variant variant)
        {
            float h = Family3DShowroomDirector.PathHalfExtent;
            Vector3 c = variant.Center + Vector3.up * 0.012f;
            Vector3[] points =
            {
                c + new Vector3(-h, 0f, -h),
                c + new Vector3(h, 0f, -h),
                c + new Vector3(h, 0f, h),
                c + new Vector3(-h, 0f, h)
            };
            CreateLine(
                variant.Id + "_ContinuousFourDirectionRoute",
                points,
                CreateLineMaterial("path_" + variant.Id.ToLowerInvariant(), variant.LabelColor * 0.8f),
                0.035f,
                true);
        }

        private static void ApplyVariantMaterials(Renderer renderer, Variant variant)
        {
            Material[] shared = renderer.sharedMaterials;
            for (var index = 0; index < shared.Length; index++)
            {
                Material source = shared[index];
                if (source == null)
                    continue;
                Color tint = variant.ResolveTint(renderer.name);
                string assetName = Sanitize(variant.Id + "_" + renderer.name + "_" + index);
                string assetPath = GeneratedRoot + "/Materials/" + assetName + ".mat";
                Material target = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
                if (target == null)
                {
                    target = new Material(source) { name = assetName };
                    AssetDatabase.CreateAsset(target, assetPath);
                }
                else
                {
                    EditorUtility.CopySerialized(source, target);
                    target.name = assetName;
                }
                if (target.HasProperty("_Color"))
                    target.color = Multiply(source.color, tint);
                if (target.HasProperty("_Metallic"))
                    target.SetFloat("_Metallic", 0f);
                if (target.HasProperty("_Glossiness"))
                    target.SetFloat("_Glossiness", 0.12f);
                shared[index] = target;
                EditorUtility.SetDirty(target);
                AssetDatabase.SaveAssetIfDirty(target);
            }
            renderer.sharedMaterials = shared;
        }

        private static Material CreateSolidMaterial(string name, Color color)
        {
            string path = GeneratedRoot + "/Materials/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Standard");
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.color = color;
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Glossiness", 0.08f);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        private static Material CreateLineMaterial(string name, Color color)
        {
            string path = GeneratedRoot + "/Materials/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Sprites/Default");
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.color = color;
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

        private static Variant[] CreateVariants()
        {
            const string body = "character_low";
            const string eyes = "eyes";
            const string lashes = "eyelashes";
            const string tooth = "tooth";
            return new[]
            {
                new Variant(
                    "PLAYER",
                    -6f,
                    1f,
                    new Color(0.98f, 0.32f, 0.28f),
                    new[] { body, eyes, lashes, tooth, "hairT", "chemise", "pants", "shoe" },
                    new Dictionary<string, Color>
                    {
                        { "hairT", new Color(0.34f, 0.22f, 0.17f) },
                        { "chemise", new Color(1f, 1f, 1f) },
                        { "pants", new Color(0.38f, 0.52f, 0.88f) },
                        { "shoe", new Color(1f, 0.36f, 0.32f) }
                    }),
                new Variant(
                    "FATHER",
                    -2f,
                    1.08f,
                    new Color(0.25f, 0.78f, 0.74f),
                    new[] { body, eyes, lashes, tooth, "hairone", "chemise", "pants", "ceinture", "bottes" },
                    new Dictionary<string, Color>
                    {
                        { "hairone", new Color(0.25f, 0.17f, 0.12f) },
                        { "chemise", new Color(0.34f, 0.82f, 0.78f) },
                        { "pants", new Color(0.36f, 0.39f, 0.44f) },
                        { "ceinture", new Color(0.52f, 0.31f, 0.18f) },
                        { "bottes", new Color(0.47f, 0.29f, 0.18f) }
                    }),
                new Variant(
                    "MOTHER",
                    2f,
                    1.04f,
                    new Color(1f, 0.62f, 0.53f),
                    new[] { body, eyes, lashes, tooth, "hairtail", "shirt", "skirt", "shoe" },
                    new Dictionary<string, Color>
                    {
                        { "hairtail", new Color(0.39f, 0.25f, 0.17f) },
                        { "shirt", new Color(1f, 0.70f, 0.62f) },
                        { "skirt", new Color(0.30f, 0.72f, 0.69f) },
                        { "shoe", new Color(0.40f, 0.27f, 0.21f) }
                    }),
                new Variant(
                    "OLDER SISTER",
                    6f,
                    1.01f,
                    new Color(0.58f, 0.65f, 1f),
                    new[] { body, eyes, lashes, tooth, "hairtail", "shirt", "ninjasuitshort" },
                    new Dictionary<string, Color>
                    {
                        { "hairtail", new Color(0.17f, 0.12f, 0.11f) },
                        { "shirt", new Color(0.20f, 0.21f, 0.25f) },
                        { "ninjasuitshort", new Color(0.24f, 0.30f, 0.56f) }
                    })
            };
        }

        private static string ResolveBuildRoot()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ??
                                 throw new InvalidOperationException("Could not resolve canonical project root from Application.dataPath.");
            string root = Path.GetFullPath(Path.Combine(projectRoot, DefaultBuildRoot));
            string[] args = Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length; index++)
            {
                if (!string.Equals(args[index], "-family3d-build-output", StringComparison.OrdinalIgnoreCase) ||
                    index + 1 >= args.Length)
                    continue;
                root = Path.GetFullPath(args[++index]);
            }
            return ValidateBuildRoot(root);
        }

        private static string ValidateBuildRoot(string candidate)
        {
            string fullCandidate = Path.GetFullPath(candidate).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ??
                                 throw new InvalidOperationException("Could not resolve canonical project root from Application.dataPath.");
            string allowedRoot = Path.GetFullPath(Path.Combine(projectRoot, "Artifacts/Family3DPrototypeV3"))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string allowedPrefix = allowedRoot + Path.DirectorySeparatorChar;
            if (!fullCandidate.StartsWith(allowedPrefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "Refusing non-isolated build output. Required root: " + allowedPrefix + " | requested: " + fullCandidate);
            return fullCandidate;
        }

        private static void WriteBuildReceipt(
            string buildRoot,
            string executablePath,
            AnimationClip walkClip,
            Avatar avatar,
            BuildReport report)
        {
            string json = JsonUtility.ToJson(new BuildReceipt
            {
                contract = Contract,
                status = "ISOLATED_BUILD_COMPLETE_VISUAL_REVIEW_REQUIRED",
                productionMutationAssertion = "NOT_ASSERTED_BY_BUILDER__VERIFY_WITH_EXTERNAL_PRE_POST_SNAPSHOT",
                isolationMechanism = "EXPLICIT_EXPERIMENTAL_SCENE_AND_EXPLICIT_BUILD_OUTPUT__NO_ASSETDATABASE_SAVEASSETS",
                scene = ScenePath,
                executable = Path.GetFullPath(executablePath),
                modelAsset = ModelPath,
                modelSha256 = Sha256(ModelPath),
                modelAvatar = avatar.name,
                modelAvatarValid = avatar.isValid,
                modelAvatarHuman = avatar.isHuman,
                walkClipAsset = WalkClipPath,
                walkClipName = walkClip.name,
                walkClipLength = walkClip.length,
                lockedCycleSeconds = Family3DWalkActor.LockedCycleSeconds,
                actorCount = 4,
                buildResult = report.summary.result.ToString(),
                buildBytes = (long)report.summary.totalSize
            }, true);
            File.WriteAllText(Path.Combine(buildRoot, "build-receipt.json"), json);
        }

        private static string Sha256(string assetPath)
        {
            using (FileStream stream = File.OpenRead(Path.GetFullPath(assetPath)))
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
                if (!scene.isDirty)
                    continue;
                throw new InvalidOperationException(
                    "Refusing to replace an open dirty scene. Save or close it explicitly, then run the isolated builder again: " +
                    scene.path);
            }
        }

        private static Color Multiply(Color left, Color right)
        {
            return new Color(left.r * right.r, left.g * right.g, left.b * right.b, left.a * right.a);
        }

        private static string Sanitize(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
                value = value.Replace(invalid, '_');
            return value.Replace(' ', '_').Replace('.', '_');
        }

        private static void EnsureAssetFolder(string assetPath)
        {
            string normalized = assetPath.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(normalized))
                return;
            string parent = normalized.Substring(0, normalized.LastIndexOf('/'));
            string name = normalized.Substring(normalized.LastIndexOf('/') + 1);
            EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        [Serializable]
        private sealed class BuildReceipt
        {
            public string contract;
            public string status;
            public string productionMutationAssertion;
            public string isolationMechanism;
            public string scene;
            public string executable;
            public string modelAsset;
            public string modelSha256;
            public string modelAvatar;
            public bool modelAvatarValid;
            public bool modelAvatarHuman;
            public string walkClipAsset;
            public string walkClipName;
            public float walkClipLength;
            public float lockedCycleSeconds;
            public int actorCount;
            public string buildResult;
            public long buildBytes;
        }

        private sealed class Variant
        {
            private readonly Dictionary<string, Color> tints;

            public Variant(
                string id,
                float laneOffset,
                float scale,
                Color labelColor,
                string[] renderers,
                Dictionary<string, Color> rendererTints)
            {
                Id = id;
                Center = new Vector3(laneOffset, 0f, laneOffset);
                Scale = scale;
                LabelColor = labelColor;
                Renderers = renderers;
                tints = rendererTints;
            }

            public string Id { get; }
            public Vector3 Center { get; }
            public float Scale { get; }
            public Color LabelColor { get; }
            public string[] Renderers { get; }

            public Color ResolveTint(string rendererName)
            {
                return tints.TryGetValue(rendererName, out Color tint) ? tint : Color.white;
            }
        }
    }
}
