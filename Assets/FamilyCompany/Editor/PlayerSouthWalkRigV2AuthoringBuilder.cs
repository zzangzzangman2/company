using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using FamilyCompany.Presentation.Unity;
using FamilyCompany.Presentation.Unity.OfficeGridView;
using FamilyCompany.Simulation.Navigation;
using UnityEditor;
using UnityEditor.U2D.PSD;
using UnityEngine;
using UnityEngine.U2D.IK;
using Object = UnityEngine.Object;

namespace FamilyCompany.Editor
{
    public static class PlayerSouthWalkRigV2AuthoringBuilder
    {
        private const string SourcePsbPath = "ArtSources/PlayerWalkRigV2/PlayerWalkRig_south.psb";
        private const string ManifestPath = "ArtSources/PlayerWalkRigV2/south-layer-manifest.json";
        private const string ImportedDirectory = "Assets/FamilyCompany/Editor/PlayerWalkRigV2Authoring";
        private const string ImportedPsbPath = ImportedDirectory + "/PlayerWalkRig_south.psb";
        private const string PrefabPath = ImportedDirectory + "/PlayerWalkRig_south.prefab";
        private const string ClipPath = ImportedDirectory + "/PlayerWalkRig_south_walk_v2.anim";
        private const string OutputDirectory =
            "Assets/Resources/FamilyCompany/PlayerBakedWalkV2/Frames/south";

        private static readonly string[] PainterOrder =
        {
            "thigh_R", "shin_R", "shoe_R",
            "upper_arm_R", "forearm_R", "hand_R",
            "pelvis", "torso",
            "thigh_L", "shin_L", "shoe_L",
            "upper_arm_L", "forearm_L", "hand_L",
            "neck_head_face", "hair_front", "hat"
        };

        [MenuItem("Family Company/Art/Build Player South Walk Rig V2 Authoring")]
        public static void Run()
        {
            SouthLayerManifest manifest = LoadManifest();
            ValidateManifest(manifest);
            ImportPsb();
            BuildPrefabAndClip(manifest);
            WriteBakeContract(manifest);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log(
                "PLAYER_SOUTH_WALK_RIG_V2_AUTHORING: PASS | layers=17 " +
                "limbSolvers=2 poses=8 psb=" + SourcePsbPath);
        }

        public static void RunFromCommandLine()
        {
            try
            {
                Run();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("PLAYER_SOUTH_WALK_RIG_V2_AUTHORING: FAIL | " + exception.Message);
                EditorApplication.Exit(1);
            }
        }

        private static void ImportPsb()
        {
            if (!File.Exists(SourcePsbPath))
                throw new FileNotFoundException("Player walk authoring PSB is missing.", SourcePsbPath);
            Directory.CreateDirectory(ImportedDirectory);
            byte[] source = File.ReadAllBytes(SourcePsbPath);
            if (!File.Exists(ImportedPsbPath) || !File.ReadAllBytes(ImportedPsbPath).SequenceEqual(source))
                File.WriteAllBytes(ImportedPsbPath, source);
            AssetDatabase.ImportAsset(ImportedPsbPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(ImportedPsbPath) as PSDImporter;
            if (importer == null)
                throw new InvalidOperationException("Unity PSD Importer did not claim the PSB source.");
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.useMosaicMode = true;
            importer.useCharacterMode = true;
            importer.spritePixelsPerUnit = PlayerBakedWalkV2TextureImporter.PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mosiacPadding = 4;
            importer.spriteSizeExpand = 2;
            importer.SaveAndReimport();
        }

        private static void BuildPrefabAndClip(SouthLayerManifest manifest)
        {
            GameObject imported = AssetDatabase.LoadAssetAtPath<GameObject>(ImportedPsbPath);
            if (imported == null)
                throw new InvalidOperationException("Imported PSB did not produce a paper-doll GameObject.");

            GameObject sourceInstance = null;
            GameObject rig = null;
            try
            {
                sourceInstance = Object.Instantiate(imported);
                sourceInstance.hideFlags = HideFlags.HideAndDontSave;
                rig = new GameObject("PlayerWalkRig_south");
                Transform root = CreateTransform(rig.transform, "Root", Vector2.zero);
                Transform artRoot = CreateTransform(root, "ArtRoot", Vector2.zero);

                var renderers = sourceInstance.GetComponentsInChildren<SpriteRenderer>(true)
                    .ToDictionary(value => value.gameObject.name, StringComparer.Ordinal);
                foreach (string layerName in PainterOrder)
                {
                    if (!renderers.TryGetValue(layerName, out SpriteRenderer sourceRenderer))
                        throw new InvalidOperationException("Imported PSB layer is missing: " + layerName);
                    GameObject part = Object.Instantiate(sourceRenderer.gameObject);
                    part.name = layerName + "_art";
                    part.hideFlags = HideFlags.None;
                    part.transform.SetParent(artRoot, false);
                    part.transform.localPosition = LayerCenterWorld(manifest, layerName);
                    part.transform.localRotation = Quaternion.identity;
                    part.transform.localScale = Vector3.one;
                    SpriteRenderer renderer = part.GetComponent<SpriteRenderer>();
                    renderer.sortingOrder = AuthoredSortingOrder(layerName);
                    renderer.maskInteraction = SpriteMaskInteraction.None;
                    if (renderer.sprite == null || renderer.sprite.texture.filterMode != FilterMode.Point)
                        throw new InvalidOperationException("PSB layer is not a Point-filtered Sprite: " + layerName);
                }

                Transform pelvis = CreateCanvasTransform(root, "pelvis", manifest, "pelvis");
                Transform hipL = CreateCanvasTransform(pelvis, "hip_L", manifest, "hip_L");
                Transform kneeL = CreateCanvasTransform(hipL, "knee_L", manifest, "knee_L");
                Transform footL = CreateCanvasTransform(kneeL, "FootContact_L", manifest, "foot_L");
                Transform hipR = CreateCanvasTransform(pelvis, "hip_R", manifest, "hip_R");
                Transform kneeR = CreateCanvasTransform(hipR, "knee_R", manifest, "knee_R");
                Transform footR = CreateCanvasTransform(kneeR, "FootContact_R", manifest, "foot_R");
                Transform shoulderL = CreateCanvasTransform(pelvis, "shoulder_L", manifest, "shoulder_L");
                Transform elbowL = CreateCanvasTransform(shoulderL, "elbow_L", manifest, "elbow_L");
                Transform wristL = CreateCanvasTransform(elbowL, "wrist_L", manifest, "wrist_L");
                Transform shoulderR = CreateCanvasTransform(pelvis, "shoulder_R", manifest, "shoulder_R");
                Transform elbowR = CreateCanvasTransform(shoulderR, "elbow_R", manifest, "elbow_R");
                Transform wristR = CreateCanvasTransform(elbowR, "wrist_R", manifest, "wrist_R");

                ParentPart(artRoot, "pelvis_art", pelvis);
                ParentPart(artRoot, "torso_art", pelvis);
                ParentPart(artRoot, "neck_head_face_art", pelvis);
                ParentPart(artRoot, "hair_front_art", pelvis);
                ParentPart(artRoot, "hat_art", pelvis);
                ParentPart(artRoot, "thigh_L_art", hipL);
                ParentPart(artRoot, "shin_L_art", kneeL);
                ParentPart(artRoot, "shoe_L_art", footL);
                ParentPart(artRoot, "thigh_R_art", hipR);
                ParentPart(artRoot, "shin_R_art", kneeR);
                ParentPart(artRoot, "shoe_R_art", footR);
                ParentPart(artRoot, "upper_arm_L_art", shoulderL);
                ParentPart(artRoot, "forearm_L_art", elbowL);
                ParentPart(artRoot, "hand_L_art", wristL);
                ParentPart(artRoot, "upper_arm_R_art", shoulderR);
                ParentPart(artRoot, "forearm_R_art", elbowR);
                ParentPart(artRoot, "hand_R_art", wristR);
                Object.DestroyImmediate(artRoot.gameObject);

                Transform targetL = CreateCanvasTransform(rig.transform, "Target_L", manifest, "foot_L");
                Transform targetR = CreateCanvasTransform(rig.transform, "Target_R", manifest, "foot_R");
                LimbSolver2D solverL = CreateLimbSolver(root, "IK_L", footL, targetL, false);
                LimbSolver2D solverR = CreateLimbSolver(root, "IK_R", footR, targetR, true);
                IKManager2D manager = rig.AddComponent<IKManager2D>();
                manager.alwaysUpdate = true;
                manager.AddSolver(solverL);
                manager.AddSolver(solverR);
                manager.UpdateManager();

                Directory.CreateDirectory(ImportedDirectory);
                PrefabUtility.SaveAsPrefabAsset(rig, PrefabPath);
                BuildAnimationClip(manifest);
            }
            finally
            {
                if (sourceInstance != null) Object.DestroyImmediate(sourceInstance);
                if (rig != null) Object.DestroyImmediate(rig);
            }
        }

        private static LimbSolver2D CreateLimbSolver(
            Transform parent,
            string name,
            Transform effector,
            Transform target,
            bool flip)
        {
            GameObject host = new GameObject(name);
            host.transform.SetParent(parent, false);
            LimbSolver2D solver = host.AddComponent<LimbSolver2D>();
            solver.GetChain(0).effector = effector;
            solver.GetChain(0).target = target;
            solver.flip = flip;
            solver.Initialize();
            if (!solver.isValid)
                throw new InvalidOperationException("Authored limb solver is invalid: " + name);
            return solver;
        }

        private static void BuildAnimationClip(SouthLayerManifest manifest)
        {
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath) is AnimationClip existing)
                AssetDatabase.DeleteAsset(ClipPath);
            var clip = new AnimationClip
            {
                name = "PlayerWalkRig_south_walk_v2",
                frameRate = PlayerBakedWalkCatalogV2.PoseCount,
                wrapMode = WrapMode.Loop
            };

            float strideStep = OfficeLocomotionGaitRules.DefaultStrideLength /
                               PlayerBakedWalkCatalogV2.PoseCount /
                               OfficeGridCharacterMover.UniformVisualScale;
            float pixelsPerUnit = manifest.pixelsPerUnit;
            Vector2 footL = CanvasPoint(manifest, "foot_L");
            Vector2 footR = CanvasPoint(manifest, "foot_R");
            float[] times = Enumerable.Range(0, 9).Select(value => value / 8f).ToArray();
            float[] rootY = Enumerable.Range(0, 9).Select(value => -strideStep * value).ToArray();
            SetFloatCurve(clip, "Root", "m_LocalPosition.y", times, rootY);
            SetFloatCurve(clip, "Root", "m_LocalPosition.x", times, new float[9]);

            float halfStride = strideStep * 4f;
            float stepPx = strideStep * pixelsPerUnit;
            float liftPx = 18f;
            float trailingPx = 60f;
            float nearContactPx = 40f;
            float passingPx = footL.y * pixelsPerUnit + 2f * stepPx + liftPx;
            var leftX = new[]
                { footL.x, footL.x, footL.x, footL.x, footL.x, -0.03f, 0f, -0.04f, footL.x };
            var leftY = new[]
            {
                footL.y, footL.y, footL.y, footL.y,
                (trailingPx - 4f * stepPx) / pixelsPerUnit,
                (68f - 5f * stepPx) / pixelsPerUnit,
                (passingPx - 6f * stepPx) / pixelsPerUnit,
                (nearContactPx - 7f * stepPx) / pixelsPerUnit,
                footL.y - halfStride * 2f
            };
            var rightX = new[] { footR.x, 0.03f, 0f, footR.x - 0.04f, footR.x, footR.x, footR.x, footR.x, footR.x };
            var rightY = new[]
            {
                trailingPx / pixelsPerUnit,
                (68f - stepPx) / pixelsPerUnit,
                (passingPx - 2f * stepPx) / pixelsPerUnit,
                (nearContactPx - 3f * stepPx) / pixelsPerUnit,
                footR.y - halfStride,
                footR.y - halfStride,
                footR.y - halfStride,
                footR.y - halfStride,
                footR.y - halfStride
            };
            SetFloatCurve(clip, "Target_L", "m_LocalPosition.x", times, leftX);
            SetFloatCurve(clip, "Target_L", "m_LocalPosition.y", times, leftY);
            SetFloatCurve(clip, "Target_R", "m_LocalPosition.x", times, rightX);
            SetFloatCurve(clip, "Target_R", "m_LocalPosition.y", times, rightY);

            float px = 1f / pixelsPerUnit;
            float pelvisY = CanvasPoint(manifest, "pelvis").y;
            SetFloatCurve(clip, "Root/pelvis", "m_LocalPosition.y", times,
                new[]
                {
                    pelvisY, pelvisY + px, pelvisY + 2f * px, pelvisY + px,
                    pelvisY, pelvisY + px, pelvisY + 2f * px, pelvisY + px, pelvisY
                });
            SetFloatCurve(clip, "Root/pelvis/shoulder_L", "localEulerAnglesRaw.z", times,
                new[] { -7f, -4f, 0f, 4f, 7f, 4f, 0f, -4f, -7f });
            SetFloatCurve(clip, "Root/pelvis/shoulder_R", "localEulerAnglesRaw.z", times,
                new[] { 7f, 4f, 0f, -4f, -7f, -4f, 0f, 4f, 7f });
            SetFloatCurve(clip, "Root/pelvis/shoulder_L/elbow_L", "localEulerAnglesRaw.z", times,
                new[] { 4f, 3f, 1f, -1f, -3f, -1f, 1f, 3f, 4f });
            SetFloatCurve(clip, "Root/pelvis/shoulder_R/elbow_R", "localEulerAnglesRaw.z", times,
                new[] { -4f, -3f, -1f, 1f, 3f, 1f, -1f, -3f, -4f });
            SetRendererCurve(clip, "Root/pelvis/hip_L/thigh_L_art", times,
                new[] { 6f, 6f, 6f, 6f, 0f, 0f, 0f, 0f, 6f });
            SetRendererCurve(clip, "Root/pelvis/hip_L/knee_L/shin_L_art", times,
                new[] { 7f, 7f, 7f, 7f, 1f, 1f, 1f, 1f, 7f });
            SetRendererCurve(clip, "Root/pelvis/hip_L/knee_L/FootContact_L/shoe_L_art", times,
                new[] { 9f, 9f, 9f, 9f, 9f, 9f, 9f, 9f, 9f });
            SetRendererCurve(clip, "Root/pelvis/hip_R/thigh_R_art", times,
                new[] { 0f, 0f, 0f, 0f, 6f, 6f, 6f, 6f, 0f });
            SetRendererCurve(clip, "Root/pelvis/hip_R/knee_R/shin_R_art", times,
                new[] { 1f, 1f, 1f, 1f, 7f, 7f, 7f, 7f, 1f });
            SetRendererCurve(clip, "Root/pelvis/hip_R/knee_R/FootContact_R/shoe_R_art", times,
                new[] { 9f, 9f, 9f, 9f, 9f, 9f, 9f, 9f, 9f });

            AssetDatabase.CreateAsset(clip, ClipPath);
            var serialized = new SerializedObject(clip);
            SerializedProperty settings = serialized.FindProperty("m_AnimationClipSettings");
            settings.FindPropertyRelative("m_LoopTime").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();
        }

        private static void WriteBakeContract(SouthLayerManifest manifest)
        {
            var contract = new PlayerWalkRigV2BakeContract
            {
                contract = "FC-PLAYER-WALK-RIG-V2",
                direction = "south",
                rigPrefabPath = PrefabPath,
                animationClipPath = ClipPath,
                sourcePsbPath = SourcePsbPath,
                outputDirectory = OutputDirectory,
                rootMotionTransform = "Root",
                leftFootContactTransform = "FootContact_L",
                rightFootContactTransform = "FootContact_R",
                pelvisTransform = "pelvis",
                canvasWidth = manifest.canvas[0],
                canvasHeight = manifest.canvas[1],
                pixelsPerUnit = manifest.pixelsPerUnit,
                strideWorld = OfficeLocomotionGaitRules.DefaultStrideLength,
                visualScale = OfficeGridCharacterMover.UniformVisualScale,
                sourcePsbSha256 = Sha256(SourcePsbPath),
                requiredLayers = PainterOrder.Select(value => value + "_art").ToArray()
            };
            Directory.CreateDirectory(Path.GetDirectoryName(PlayerWalkRigV2Baker.ContractPath) ?? ".");
            File.WriteAllText(PlayerWalkRigV2Baker.ContractPath, JsonUtility.ToJson(contract, true) + Environment.NewLine);
        }

        private static void SetFloatCurve(
            AnimationClip clip,
            string path,
            string property,
            IReadOnlyList<float> times,
            IReadOnlyList<float> values)
        {
            if (times.Count != values.Count) throw new ArgumentException("Curve key counts differ.");
            var keys = new Keyframe[times.Count];
            for (var index = 0; index < keys.Length; index++)
                keys[index] = new Keyframe(times[index], values[index], 0f, 0f);
            clip.SetCurve(path, typeof(Transform), property, new AnimationCurve(keys));
        }

        private static void SetRendererCurve(
            AnimationClip clip,
            string path,
            IReadOnlyList<float> times,
            IReadOnlyList<float> values)
        {
            if (times.Count != values.Count) throw new ArgumentException("Curve key counts differ.");
            var keys = new Keyframe[times.Count];
            for (var index = 0; index < keys.Length; index++)
                keys[index] = new Keyframe(times[index], values[index], float.PositiveInfinity, float.PositiveInfinity);
            clip.SetCurve(path, typeof(SpriteRenderer), "m_SortingOrder", new AnimationCurve(keys));
        }

        private static int AuthoredSortingOrder(string layerName)
        {
            return layerName switch
            {
                "thigh_R" => 0,
                "shin_R" => 1,
                "shoe_R" => 2,
                "thigh_L" => 6,
                "shin_L" => 7,
                "shoe_L" => 8,
                "pelvis" => 10,
                "torso" => 11,
                "upper_arm_R" => 12,
                "forearm_R" => 13,
                "hand_R" => 14,
                "upper_arm_L" => 15,
                "forearm_L" => 16,
                "hand_L" => 17,
                "neck_head_face" => 18,
                "hair_front" => 19,
                "hat" => 20,
                _ => throw new InvalidOperationException("Unknown authored sorting layer: " + layerName)
            };
        }

        private static Transform CreateTransform(Transform parent, string name, Vector2 localPosition)
        {
            var value = new GameObject(name).transform;
            value.SetParent(parent, false);
            value.localPosition = localPosition;
            return value;
        }

        private static Transform CreateCanvasTransform(
            Transform parent,
            string name,
            SouthLayerManifest manifest,
            string pointName)
        {
            Vector2 world = CanvasPoint(manifest, pointName);
            var value = new GameObject(name).transform;
            value.position = world;
            value.SetParent(parent, true);
            return value;
        }

        private static Vector2 CanvasPoint(SouthLayerManifest manifest, string pointName)
        {
            SerializablePoint point = manifest.jointsCanvasPx.FirstOrDefault(value =>
                string.Equals(value.name, pointName, StringComparison.Ordinal));
            if (point == null || point.value == null || point.value.Length != 2)
                throw new InvalidOperationException("Rig joint is missing: " + pointName);
            return new Vector2(
                (point.value[0] - manifest.canvas[0] * 0.5f) / manifest.pixelsPerUnit,
                point.value[1] / manifest.pixelsPerUnit);
        }

        private static Vector2 LayerCenterWorld(SouthLayerManifest manifest, string layerName)
        {
            LayerEntry layer = manifest.layers.FirstOrDefault(value =>
                string.Equals(value.name, layerName, StringComparison.Ordinal));
            if (layer == null || layer.width <= 0 || layer.height <= 0)
                throw new InvalidOperationException("Rig layer geometry is missing: " + layerName);
            return new Vector2(
                (layer.left + layer.width * 0.5f - manifest.canvas[0] * 0.5f) /
                manifest.pixelsPerUnit,
                (manifest.canvas[1] - layer.top - layer.height * 0.5f) /
                manifest.pixelsPerUnit);
        }

        private static void ParentPart(Transform root, string name, Transform parent)
        {
            Transform part = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(value =>
                string.Equals(value.name, name, StringComparison.Ordinal));
            if (part == null) throw new InvalidOperationException("Rig art part is missing: " + name);
            Vector3 worldPosition = part.position;
            Quaternion worldRotation = part.rotation;
            Vector3 worldScale = part.lossyScale;
            part.SetParent(parent, false);
            part.localPosition = parent.InverseTransformPoint(worldPosition);
            part.localRotation = Quaternion.Inverse(parent.rotation) * worldRotation;
            part.localScale = new Vector3(
                worldScale.x / parent.lossyScale.x,
                worldScale.y / parent.lossyScale.y,
                worldScale.z / parent.lossyScale.z);
        }

        private static SouthLayerManifest LoadManifest()
        {
            if (!File.Exists(ManifestPath))
                throw new FileNotFoundException("Player walk layer manifest is missing.", ManifestPath);
            string json = File.ReadAllText(ManifestPath);
            // JsonUtility cannot deserialize dictionary properties, so normalize the known joint object.
            SouthLayerManifestRaw raw = JsonUtility.FromJson<SouthLayerManifestRaw>(json);
            if (raw == null) throw new InvalidOperationException("Player walk layer manifest JSON is invalid.");
            raw.jointsCanvasPx ??= new JointObject();
            return new SouthLayerManifest
            {
                contract = raw.contract,
                psb = raw.psb,
                psbSha256 = raw.psbSha256,
                canvas = raw.canvas,
                pixelsPerUnit = raw.pixelsPerUnit,
                generatedFullFrames = raw.generatedFullFrames,
                interpolatedPixels = raw.interpolatedPixels,
                layers = raw.layers,
                jointsCanvasPx = raw.jointsCanvasPx.ToPoints()
            };
        }

        private static void ValidateManifest(SouthLayerManifest manifest)
        {
            if (!string.Equals(manifest.contract, "FC-PLAYER-WALK-RIG-V2-LAYERS", StringComparison.Ordinal))
                throw new InvalidOperationException("Player walk layer manifest contract is invalid.");
            if (!string.Equals(manifest.psb, SourcePsbPath, StringComparison.Ordinal) ||
                !string.Equals(manifest.psbSha256, Sha256(SourcePsbPath), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Player walk PSB path/SHA is invalid.");
            if (manifest.canvas == null || manifest.canvas.Length != 2 ||
                manifest.canvas[0] != 384 || manifest.canvas[1] != 512 ||
                Mathf.Abs(manifest.pixelsPerUnit - PlayerBakedWalkV2TextureImporter.PixelsPerUnit) > 0.001f)
                throw new InvalidOperationException("Player walk layer canvas/PPU is invalid.");
            if (manifest.generatedFullFrames || manifest.interpolatedPixels)
                throw new InvalidOperationException("Player walk rig source may not contain generated/interpolated frames.");
            string[] names = manifest.layers?.Select(value => value.name).ToArray() ?? Array.Empty<string>();
            if (!PainterOrder.All(names.Contains))
                throw new InvalidOperationException("Player walk manifest does not contain every required layer.");
        }

        private static string Sha256(string path)
        {
            using SHA256 sha = SHA256.Create();
            using FileStream stream = File.OpenRead(path);
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
        }

        [Serializable]
        private sealed class SouthLayerManifestRaw
        {
            public string contract;
            public string psb;
            public string psbSha256;
            public int[] canvas;
            public float pixelsPerUnit;
            public bool generatedFullFrames;
            public bool interpolatedPixels;
            public LayerEntry[] layers;
            public JointObject jointsCanvasPx;
        }

        private sealed class SouthLayerManifest
        {
            public string contract;
            public string psb;
            public string psbSha256;
            public int[] canvas;
            public float pixelsPerUnit;
            public bool generatedFullFrames;
            public bool interpolatedPixels;
            public LayerEntry[] layers;
            public SerializablePoint[] jointsCanvasPx;
        }

        [Serializable]
        private sealed class LayerEntry
        {
            public string name;
            public int left;
            public int top;
            public int width;
            public int height;
        }

        private sealed class SerializablePoint
        {
            public string name;
            public int[] value;
        }

        [Serializable]
        private sealed class JointObject
        {
            public int[] pelvis;
            public int[] hip_L;
            public int[] knee_L;
            public int[] ankle_L;
            public int[] foot_L;
            public int[] hip_R;
            public int[] knee_R;
            public int[] ankle_R;
            public int[] foot_R;
            public int[] shoulder_L;
            public int[] elbow_L;
            public int[] wrist_L;
            public int[] shoulder_R;
            public int[] elbow_R;
            public int[] wrist_R;

            public SerializablePoint[] ToPoints() => new[]
            {
                P(nameof(pelvis), pelvis), P(nameof(hip_L), hip_L), P(nameof(knee_L), knee_L),
                P(nameof(ankle_L), ankle_L), P(nameof(foot_L), foot_L), P(nameof(hip_R), hip_R),
                P(nameof(knee_R), knee_R), P(nameof(ankle_R), ankle_R), P(nameof(foot_R), foot_R),
                P(nameof(shoulder_L), shoulder_L), P(nameof(elbow_L), elbow_L), P(nameof(wrist_L), wrist_L),
                P(nameof(shoulder_R), shoulder_R), P(nameof(elbow_R), elbow_R), P(nameof(wrist_R), wrist_R)
            };

            private static SerializablePoint P(string name, int[] value) =>
                new SerializablePoint { name = name, value = value };
        }
    }
}
