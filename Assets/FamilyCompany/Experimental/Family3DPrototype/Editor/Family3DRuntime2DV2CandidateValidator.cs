using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FamilyCompany.Experimental.Family3D.Editor
{
    /// <summary>
    /// Imports and validates only the four isolated Runtime-2D identity V2 candidates.
    /// This path never edits a scene, runtime catalog, build settings, or production/default asset.
    /// </summary>
    public static class Family3DRuntime2DV2CandidateValidator
    {
        private const string Contract = "FC-FAMILY-3D-RUNTIME2D-V2-UNITY-IMPORT-V1";
        private const string OutputRoot = "Artifacts/Family3DRuntime2DV2/UnityImport";

        private static readonly CandidateDefinition[] Candidates =
        {
            new CandidateDefinition(
                "PLAYER",
                Family3DPrototypeModelImporter.PlayerRuntime2DV2CandidateModelPath,
                Family3DPrototypeModelImporter.CandidateRoot +
                "PlayerV4/player-runtime2d-identity-v4-atlas.png"),
            new CandidateDefinition(
                "FATHER",
                Family3DPrototypeModelImporter.FatherRuntime2DV2CandidateModelPath,
                Family3DPrototypeModelImporter.CandidateRoot +
                "FatherV2/father-blender-identity-v2-atlas.png"),
            new CandidateDefinition(
                "MOTHER",
                Family3DPrototypeModelImporter.MotherRuntime2DV2CandidateModelPath,
                Family3DPrototypeModelImporter.CandidateRoot +
                "MotherV2/mother-blender-identity-v2-atlas.png"),
            new CandidateDefinition(
                "OLDER SISTER",
                Family3DPrototypeModelImporter.OlderSisterRuntime2DV2CandidateModelPath,
                Family3DPrototypeModelImporter.CandidateRoot +
                "OlderSisterV2/older-sister-blender-identity-v2-atlas.png")
        };

        [MenuItem("Family Company/Experimental/Validate Runtime-2D V2 3D Candidates")]
        public static void ValidateAllFromMenu()
        {
            Run(exitEditor: false);
        }

        public static void ValidateAllFromCommandLine()
        {
            Run(exitEditor: true);
        }

        private static void Run(bool exitEditor)
        {
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ??
                                     throw new InvalidOperationException("Could not resolve project root.");
                string outputDirectory = Path.Combine(projectRoot, OutputRoot);
                Directory.CreateDirectory(outputDirectory);

                var receipts = new CandidateReceipt[Candidates.Length];
                for (var index = 0; index < Candidates.Length; index++)
                {
                    CandidateDefinition definition = Candidates[index];
                    receipts[index] = Validate(projectRoot, definition);
                    WriteReceipt(outputDirectory, definition.Id, receipts[index]);
                }

                var summary = new ImportSummary
                {
                    contract = Contract + "-SUMMARY",
                    status = "PASS_VISUAL_REVIEW_REQUIRED",
                    candidateCount = receipts.Length,
                    candidates = receipts,
                    v1HistoryPreserved = true,
                    productionMutation = false,
                    defaultRuntimeMutation = false,
                    starterOfficeMutation = false,
                    productionEligible = false
                };
                string summaryPath = Path.Combine(outputDirectory, "all-import-receipt.json");
                File.WriteAllText(summaryPath, JsonUtility.ToJson(summary, true));
                Debug.Log("FAMILY_3D_RUNTIME2D_V2_IMPORT_ALL: PASS | " + summaryPath);
                if (exitEditor)
                    EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("FAMILY_3D_RUNTIME2D_V2_IMPORT_ALL: FAIL | " + exception.Message);
                if (exitEditor)
                    EditorApplication.Exit(1);
                else
                    throw;
            }
        }

        private static CandidateReceipt Validate(string projectRoot, CandidateDefinition definition)
        {
            if (!Family3DPrototypeModelImporter.IsRuntime2DV2Candidate(definition.ModelPath))
                throw new InvalidOperationException(
                    "Runtime-2D V2 model path is not explicitly allow-listed: " + definition.ModelPath);

            Family3DIdentityCandidateValidator.CandidateReceipt structural =
                Family3DIdentityCandidateValidator.Validate(
                    definition.Id,
                    definition.ModelPath,
                    definition.AtlasPath);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(definition.ModelPath);
            if (prefab == null)
                throw new InvalidOperationException("Imported V2 prefab did not load: " + definition.ModelPath);
            SkinnedMeshRenderer[] renderers = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (renderers.Length != 1 || renderers[0].sharedMesh == null)
                throw new InvalidOperationException("V2 candidate lost its single complete skinned mesh contract.");
            Mesh mesh = renderers[0].sharedMesh;
            if (!mesh.HasVertexAttribute(VertexAttribute.TexCoord0) || mesh.uv.Length != mesh.vertexCount)
                throw new InvalidOperationException(
                    "V2 candidate must have complete UV0 coverage: " + definition.ModelPath);

            VertexAttribute[] forbiddenUvChannels =
            {
                VertexAttribute.TexCoord1,
                VertexAttribute.TexCoord2,
                VertexAttribute.TexCoord3,
                VertexAttribute.TexCoord4,
                VertexAttribute.TexCoord5,
                VertexAttribute.TexCoord6,
                VertexAttribute.TexCoord7
            };
            for (var index = 0; index < forbiddenUvChannels.Length; index++)
            {
                if (mesh.HasVertexAttribute(forbiddenUvChannels[index]))
                    throw new InvalidOperationException(
                        "V2 candidate must expose exactly one UV channel; found " +
                        forbiddenUvChannels[index] + " in " + definition.ModelPath);
            }

            string modelFullPath = Path.Combine(projectRoot, definition.ModelPath);
            string atlasFullPath = Path.Combine(projectRoot, definition.AtlasPath);
            return new CandidateReceipt
            {
                contract = Contract,
                status = "AUTO_PASS_VISUAL_REVIEW_REQUIRED",
                familyId = definition.Id,
                modelAsset = definition.ModelPath,
                atlasAsset = definition.AtlasPath,
                modelSha256 = Sha256(modelFullPath),
                atlasSha256 = Sha256(atlasFullPath),
                modelBytes = structural.modelBytes,
                atlasBytes = structural.atlasBytes,
                avatarName = structural.avatarName,
                avatarValid = structural.avatarValid,
                avatarHuman = structural.avatarHuman,
                skinnedMeshRendererCount = structural.skinnedMeshRendererCount,
                meshName = structural.meshName,
                vertexCount = structural.vertexCount,
                triangleCount = structural.triangleCount,
                bindPoseCount = structural.bindPoseCount,
                skinBoneCount = structural.skinBoneCount,
                materialCount = structural.materialCount,
                uvChannelCount = 1,
                uv0VertexCount = mesh.uv.Length,
                embeddedMainTexturePresent = structural.embeddedMainTexturePresent,
                externalAtlasImported = structural.externalAtlasImported,
                standingHeight = structural.standingHeight,
                boundsMinimumY = structural.boundsMinimumY,
                boundsMaximumY = structural.boundsMaximumY,
                requiredHumanoidBoneCount = structural.requiredHumanoidBoneCount,
                missingHumanoidBones = structural.missingHumanoidBones,
                mismatchedHumanoidBoneMappings = structural.mismatchedHumanoidBoneMappings,
                runtime2DV2AllowListed = true,
                productionEligible = false
            };
        }

        private static void WriteReceipt(string outputDirectory, string id, CandidateReceipt receipt)
        {
            string fileName = id.ToLowerInvariant().Replace(' ', '-') + "-import-receipt.json";
            File.WriteAllText(
                Path.Combine(outputDirectory, fileName),
                JsonUtility.ToJson(receipt, true));
        }

        private static string Sha256(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", string.Empty);
        }

        [Serializable]
        private sealed class CandidateReceipt
        {
            public string contract;
            public string status;
            public string familyId;
            public string modelAsset;
            public string atlasAsset;
            public string modelSha256;
            public string atlasSha256;
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
            public int uvChannelCount;
            public int uv0VertexCount;
            public bool embeddedMainTexturePresent;
            public bool externalAtlasImported;
            public float standingHeight;
            public float boundsMinimumY;
            public float boundsMaximumY;
            public int requiredHumanoidBoneCount;
            public string[] missingHumanoidBones;
            public string[] mismatchedHumanoidBoneMappings;
            public bool runtime2DV2AllowListed;
            public bool productionEligible;
        }

        [Serializable]
        private sealed class ImportSummary
        {
            public string contract;
            public string status;
            public int candidateCount;
            public CandidateReceipt[] candidates;
            public bool v1HistoryPreserved;
            public bool productionMutation;
            public bool defaultRuntimeMutation;
            public bool starterOfficeMutation;
            public bool productionEligible;
        }

        private sealed class CandidateDefinition
        {
            public CandidateDefinition(string id, string modelPath, string atlasPath)
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
