using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FamilyCompany.Experimental.Family3D.Editor
{
    public static class Family3DIdentityCandidateValidator
    {
        private const string OutputRoot = "Artifacts/Family3DIdentityCandidates/UnityImport";

        public static void ValidatePlayerFromCommandLine()
        {
            ValidateFromCommandLine(
                "PLAYER",
                Family3DPrototypeModelImporter.PlayerCandidateModelPath,
                Family3DPrototypeModelImporter.CandidateRoot +
                "PlayerV3/player-v6-blender-identity-v3-atlas.png");
        }

        public static void ValidateAllFromCommandLine()
        {
            try
            {
                CandidateInput[] inputs =
                {
                    new CandidateInput(
                        "PLAYER",
                        Family3DPrototypeModelImporter.PlayerCandidateModelPath,
                        Family3DPrototypeModelImporter.CandidateRoot +
                        "PlayerV3/player-v6-blender-identity-v3-atlas.png"),
                    new CandidateInput(
                        "FATHER",
                        Family3DPrototypeModelImporter.FatherCandidateModelPath,
                        Family3DPrototypeModelImporter.CandidateRoot +
                        "FatherV1/father-blender-identity-v1-atlas.png"),
                    new CandidateInput(
                        "MOTHER",
                        Family3DPrototypeModelImporter.MotherCandidateModelPath,
                        Family3DPrototypeModelImporter.CandidateRoot +
                        "MotherV1/mother-blender-identity-v1-atlas.png"),
                    new CandidateInput(
                        "OLDER SISTER",
                        Family3DPrototypeModelImporter.OlderSisterCandidateModelPath,
                        Family3DPrototypeModelImporter.CandidateRoot +
                        "OlderSisterV1/older-sister-blender-identity-v1-atlas.png")
                };

                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ??
                                     throw new InvalidOperationException("Could not resolve project root.");
                string outputDirectory = Path.Combine(projectRoot, OutputRoot);
                Directory.CreateDirectory(outputDirectory);
                var receipts = new CandidateReceipt[inputs.Length];
                for (var index = 0; index < inputs.Length; index++)
                {
                    CandidateInput input = inputs[index];
                    receipts[index] = Validate(input.Id, input.ModelPath, input.AtlasPath);
                    WriteReceipt(outputDirectory, input.Id, receipts[index]);
                }

                var summary = new CandidateImportSummary
                {
                    contract = "FC-FAMILY-3D-IDENTITY-IMPORT-SUMMARY-V1",
                    status = "PASS_VISUAL_AND_MOTION_REVIEW_REQUIRED",
                    candidateCount = receipts.Length,
                    candidates = receipts,
                    productionEligible = false
                };
                string summaryPath = Path.Combine(outputDirectory, "all-import-receipt.json");
                File.WriteAllText(summaryPath, JsonUtility.ToJson(summary, true));
                Debug.Log("FAMILY_3D_IDENTITY_IMPORT_ALL: PASS | " + summaryPath);
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("FAMILY_3D_IDENTITY_IMPORT_ALL: FAIL | " + exception.Message);
                EditorApplication.Exit(1);
            }
        }

        private static void ValidateFromCommandLine(string id, string modelPath, string atlasPath)
        {
            try
            {
                CandidateReceipt receipt = Validate(id, modelPath, atlasPath);
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ??
                                     throw new InvalidOperationException("Could not resolve project root.");
                string outputDirectory = Path.Combine(projectRoot, OutputRoot);
                Directory.CreateDirectory(outputDirectory);
                string outputPath = WriteReceipt(outputDirectory, id, receipt);
                Debug.Log("FAMILY_3D_IDENTITY_IMPORT: PASS | " + id + " | " + outputPath);
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("FAMILY_3D_IDENTITY_IMPORT: FAIL | " + id + " | " + exception.Message);
                EditorApplication.Exit(1);
            }
        }

        private static string WriteReceipt(string outputDirectory, string id, CandidateReceipt receipt)
        {
            string outputPath = Path.Combine(
                outputDirectory,
                id.ToLowerInvariant().Replace(' ', '-') + "-import-receipt.json");
            File.WriteAllText(outputPath, JsonUtility.ToJson(receipt, true));
            return outputPath;
        }

        internal static CandidateReceipt Validate(string id, string modelPath, string atlasPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ??
                                 throw new InvalidOperationException("Could not resolve project root.");
            string modelFullPath = Path.Combine(projectRoot, modelPath);
            string atlasFullPath = Path.Combine(projectRoot, atlasPath);
            if (!File.Exists(modelFullPath))
                throw new FileNotFoundException("Identity candidate FBX is missing.", modelFullPath);
            if (!File.Exists(atlasFullPath))
                throw new FileNotFoundException("Identity candidate atlas is missing.", atlasFullPath);

            AssetDatabase.ImportAsset(
                atlasPath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(
                modelPath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null)
                throw new InvalidOperationException("ModelImporter is missing: " + modelPath);
            if (importer.animationType != ModelImporterAnimationType.Human ||
                importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
                throw new InvalidOperationException("Candidate did not use the required Humanoid/CreateFromThisModel importer contract.");

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (prefab == null)
                throw new InvalidOperationException("Candidate model prefab did not load: " + modelPath);
            Animator animator = prefab.GetComponent<Animator>();
            if (animator == null || animator.avatar == null ||
                !animator.avatar.isValid || !animator.avatar.isHuman)
                throw new InvalidOperationException("Candidate does not have a valid Humanoid Avatar: " + modelPath);

            SkinnedMeshRenderer[] renderers = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (renderers.Length != 1)
                throw new InvalidOperationException(
                    "Candidate must import as exactly one SkinnedMeshRenderer; found " + renderers.Length + ".");
            SkinnedMeshRenderer renderer = renderers[0];
            Mesh mesh = renderer.sharedMesh;
            if (mesh == null || mesh.vertexCount <= 0 || mesh.bindposes.Length <= 0)
                throw new InvalidOperationException("Candidate SkinnedMeshRenderer has no valid skinned mesh/bind poses.");
            if (renderer.bones == null || renderer.bones.Length < 20)
                throw new InvalidOperationException("Candidate renderer has too few skin bones: " + renderer.bones?.Length);
            if (renderer.sharedMaterials == null || renderer.sharedMaterials.Length != 1)
                throw new InvalidOperationException(
                    "Candidate must use one material/atlas; found " + renderer.sharedMaterials?.Length + ".");

            HumanBodyBones[] requiredBones =
            {
                HumanBodyBones.Hips,
                HumanBodyBones.Spine,
                HumanBodyBones.Chest,
                HumanBodyBones.UpperChest,
                HumanBodyBones.Neck,
                HumanBodyBones.Head,
                HumanBodyBones.LeftShoulder,
                HumanBodyBones.LeftUpperArm,
                HumanBodyBones.LeftLowerArm,
                HumanBodyBones.LeftHand,
                HumanBodyBones.RightShoulder,
                HumanBodyBones.RightUpperArm,
                HumanBodyBones.RightLowerArm,
                HumanBodyBones.RightHand,
                HumanBodyBones.LeftUpperLeg,
                HumanBodyBones.LeftLowerLeg,
                HumanBodyBones.LeftFoot,
                HumanBodyBones.LeftToes,
                HumanBodyBones.RightUpperLeg,
                HumanBodyBones.RightLowerLeg,
                HumanBodyBones.RightFoot,
                HumanBodyBones.RightToes
            };
            var missing = new List<string>();
            var mappingMismatches = new List<string>();
            foreach (HumanBodyBones bone in requiredBones)
            {
                Transform mapped = animator.GetBoneTransform(bone);
                if (mapped == null)
                    missing.Add(bone.ToString());
                else if (!string.Equals(mapped.name, bone.ToString(), StringComparison.Ordinal))
                    mappingMismatches.Add(bone + "->" + mapped.name);
            }
            if (missing.Count > 0)
                throw new InvalidOperationException("Required Humanoid mappings are missing: " + string.Join(", ", missing));
            if (mappingMismatches.Count > 0)
                throw new InvalidOperationException(
                    "Humanoid mappings target the wrong transforms: " + string.Join(", ", mappingMismatches));

            GameObject instance = Object.Instantiate(prefab);
            try
            {
                Bounds bounds = instance.GetComponentsInChildren<Renderer>(true)
                    .Select(item => item.bounds)
                    .Aggregate((left, right) =>
                    {
                        left.Encapsulate(right);
                        return left;
                    });
                if (bounds.size.y <= 0.5f || bounds.size.y >= 10f)
                    throw new InvalidOperationException("Candidate standing height is outside the isolated review range: " + bounds.size.y);

                Material material = renderer.sharedMaterials[0];
                Texture atlas = AssetDatabase.LoadAssetAtPath<Texture>(atlasPath);
                return new CandidateReceipt
                {
                    contract = "FC-FAMILY-3D-IDENTITY-IMPORT-V1",
                    status = "AUTO_PASS_VISUAL_AND_MOTION_REVIEW_REQUIRED",
                    familyId = id,
                    modelAsset = modelPath,
                    atlasAsset = atlasPath,
                    modelBytes = new FileInfo(modelFullPath).Length,
                    atlasBytes = new FileInfo(atlasFullPath).Length,
                    avatarName = animator.avatar.name,
                    avatarValid = animator.avatar.isValid,
                    avatarHuman = animator.avatar.isHuman,
                    skinnedMeshRendererCount = renderers.Length,
                    meshName = mesh.name,
                    vertexCount = mesh.vertexCount,
                    triangleCount = mesh.triangles.Length / 3,
                    bindPoseCount = mesh.bindposes.Length,
                    skinBoneCount = renderer.bones.Length,
                    materialCount = renderer.sharedMaterials.Length,
                    embeddedMainTexturePresent = material != null && material.mainTexture != null,
                    externalAtlasImported = atlas != null,
                    standingHeight = bounds.size.y,
                    boundsMinimumY = bounds.min.y,
                    boundsMaximumY = bounds.max.y,
                    requiredHumanoidBoneCount = requiredBones.Length,
                    missingHumanoidBones = Array.Empty<string>(),
                    mismatchedHumanoidBoneMappings = Array.Empty<string>(),
                    productionEligible = false
                };
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Serializable]
        internal sealed class CandidateReceipt
        {
            public string contract;
            public string status;
            public string familyId;
            public string modelAsset;
            public string atlasAsset;
            public long modelBytes;
            public long atlasBytes;
            public string avatarName;
            public bool avatarValid;
            public bool avatarHuman;
            public int skinnedMeshRendererCount;
            public string meshName;
            public int vertexCount;
            public int triangleCount;
            public int bindPoseCount;
            public int skinBoneCount;
            public int materialCount;
            public bool embeddedMainTexturePresent;
            public bool externalAtlasImported;
            public float standingHeight;
            public float boundsMinimumY;
            public float boundsMaximumY;
            public int requiredHumanoidBoneCount;
            public string[] missingHumanoidBones;
            public string[] mismatchedHumanoidBoneMappings;
            public bool productionEligible;
        }

        [Serializable]
        private sealed class CandidateImportSummary
        {
            public string contract;
            public string status;
            public int candidateCount;
            public CandidateReceipt[] candidates;
            public bool productionEligible;
        }

        private sealed class CandidateInput
        {
            public CandidateInput(string id, string modelPath, string atlasPath)
            {
                Id = id;
                ModelPath = modelPath;
                AtlasPath = atlasPath;
            }

            public string Id { get; }
            public string ModelPath { get; }
            public string AtlasPath { get; }
        }
    }
}
