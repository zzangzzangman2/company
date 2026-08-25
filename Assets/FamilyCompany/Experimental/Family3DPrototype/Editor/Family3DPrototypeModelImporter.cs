using System;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Experimental.Family3D.Editor
{
    public sealed class Family3DPrototypeModelImporter : AssetPostprocessor
    {
        public const string PrototypeRoot = "Assets/FamilyCompany/Experimental/Family3DPrototype/";
        public const string ModelPath = PrototypeRoot + "ThirdParty/StylooChibi/allinone.fbx";
        public const string CandidateRoot = PrototypeRoot + "Candidates/";
        public const string PlayerCandidateModelPath =
            CandidateRoot + "PlayerV3/player-v6-blender-humanoid-v3.fbx";
        public const string FatherCandidateModelPath =
            CandidateRoot + "FatherV1/father-blender-humanoid-v1.fbx";
        public const string MotherCandidateModelPath =
            CandidateRoot + "MotherV1/mother-blender-humanoid-v1.fbx";
        public const string OlderSisterCandidateModelPath =
            CandidateRoot + "OlderSisterV1/older-sister-blender-humanoid-v1.fbx";
        public const string PlayerRuntime2DV2CandidateModelPath =
            CandidateRoot + "PlayerV4/player-runtime2d-humanoid-v4.fbx";
        public const string FatherRuntime2DV2CandidateModelPath =
            CandidateRoot + "FatherV2/father-blender-humanoid-v2.fbx";
        public const string MotherRuntime2DV2CandidateModelPath =
            CandidateRoot + "MotherV2/mother-blender-humanoid-v2.fbx";
        public const string OlderSisterRuntime2DV2CandidateModelPath =
            CandidateRoot + "OlderSisterV2/older-sister-blender-humanoid-v2.fbx";
        public const string FatherApprovedV14CandidateModelPath =
            CandidateRoot + "FatherApprovedV14/father-approved-v14-rigged.fbx";
        public const string FatherApprovedV14NaturalWalkRigModelPath =
            CandidateRoot +
            "FatherApprovedV14NaturalWalkRigV1/father-approved-v14-natural-walk-rig-v1.fbx";

        private static readonly string[] IdentityHumanoidBoneNames =
        {
            "Hips",
            "Spine",
            "Chest",
            "UpperChest",
            "Neck",
            "Head",
            "LeftShoulder",
            "LeftUpperArm",
            "LeftLowerArm",
            "LeftHand",
            "RightShoulder",
            "RightUpperArm",
            "RightLowerArm",
            "RightHand",
            "LeftUpperLeg",
            "LeftLowerLeg",
            "LeftFoot",
            "LeftToes",
            "RightUpperLeg",
            "RightLowerLeg",
            "RightFoot",
            "RightToes"
        };

        public static bool IsIdentityCandidate(string path)
        {
            return string.Equals(path, PlayerCandidateModelPath, StringComparison.Ordinal) ||
                   string.Equals(path, FatherCandidateModelPath, StringComparison.Ordinal) ||
                   string.Equals(path, MotherCandidateModelPath, StringComparison.Ordinal) ||
                   string.Equals(path, OlderSisterCandidateModelPath, StringComparison.Ordinal) ||
                   string.Equals(path, FatherApprovedV14CandidateModelPath, StringComparison.Ordinal) ||
                   string.Equals(path, FatherApprovedV14NaturalWalkRigModelPath, StringComparison.Ordinal) ||
                   IsRuntime2DV2Candidate(path);
        }

        public static bool IsRuntime2DV2Candidate(string path)
        {
            return string.Equals(path, PlayerRuntime2DV2CandidateModelPath, StringComparison.Ordinal) ||
                   string.Equals(path, FatherRuntime2DV2CandidateModelPath, StringComparison.Ordinal) ||
                   string.Equals(path, MotherRuntime2DV2CandidateModelPath, StringComparison.Ordinal) ||
                   string.Equals(path, OlderSisterRuntime2DV2CandidateModelPath, StringComparison.Ordinal);
        }

        private void OnPreprocessModel()
        {
            bool proxy = string.Equals(assetPath, ModelPath, StringComparison.Ordinal);
            bool identityCandidate = IsIdentityCandidate(assetPath);
            if (!proxy && !identityCandidate)
                return;

            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = proxy;
            importer.importBlendShapes = false;
            importer.importVisibility = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.optimizeGameObjects = false;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.resampleCurves = false;
            if (identityCandidate)
            {
                importer.meshCompression = ModelImporterMeshCompression.Off;
                importer.isReadable = true;
                importer.humanDescription = IsFatherApprovedV14Rig(assetPath)
                    ? CreateExplicitFatherApprovedV14HumanDescription(importer.humanDescription)
                    : CreateExplicitIdentityHumanDescription(importer.humanDescription);
            }
        }

        private static bool IsFatherApprovedV14Rig(string path)
        {
            return string.Equals(path, FatherApprovedV14CandidateModelPath, StringComparison.Ordinal) ||
                   string.Equals(path, FatherApprovedV14NaturalWalkRigModelPath, StringComparison.Ordinal);
        }

        private static HumanDescription CreateExplicitFatherApprovedV14HumanDescription(
            HumanDescription description)
        {
            var mappings = new[]
            {
                Map("Hips", "Bip001 Pelvis"),
                Map("Spine", "Bip001 Spine"),
                Map("Chest", "Bip001 Spine1"),
                Map("Neck", "Bip001 Neck"),
                Map("Head", "Bip001 Head"),
                Map("LeftShoulder", "Bip001 L Clavicle"),
                Map("LeftUpperArm", "Bip001 L UpperArm"),
                Map("LeftLowerArm", "Bip001 L Forearm"),
                Map("LeftHand", "Bip001 L Hand"),
                Map("RightShoulder", "Bip001 R Clavicle"),
                Map("RightUpperArm", "Bip001 R UpperArm"),
                Map("RightLowerArm", "Bip001 R Forearm"),
                Map("RightHand", "Bip001 R Hand"),
                Map("LeftUpperLeg", "Bip001 L Thigh"),
                Map("LeftLowerLeg", "Bip001 L Calf"),
                Map("LeftFoot", "Bip001 L Foot"),
                Map("LeftToes", "Bip001 L Toe0"),
                Map("RightUpperLeg", "Bip001 R Thigh"),
                Map("RightLowerLeg", "Bip001 R Calf"),
                Map("RightFoot", "Bip001 R Foot"),
                Map("RightToes", "Bip001 R Toe0")
            };
            description.human = mappings;
            return description;
        }

        private static HumanBone Map(string humanName, string boneName)
        {
            return new HumanBone
            {
                humanName = humanName,
                boneName = boneName,
                limit = new HumanLimit { useDefaultValues = true }
            };
        }

        private static HumanDescription CreateExplicitIdentityHumanDescription(HumanDescription description)
        {
            var mappings = new HumanBone[IdentityHumanoidBoneNames.Length];
            for (var index = 0; index < IdentityHumanoidBoneNames.Length; index++)
            {
                string boneName = IdentityHumanoidBoneNames[index];
                mappings[index] = new HumanBone
                {
                    boneName = boneName,
                    humanName = boneName,
                    limit = new HumanLimit { useDefaultValues = true }
                };
            }
            description.human = mappings;
            return description;
        }

        private void OnPostprocessModel(GameObject model)
        {
            if ((!string.Equals(assetPath, ModelPath, StringComparison.Ordinal) && !IsIdentityCandidate(assetPath)) ||
                model == null)
                return;
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
                renderer.receiveShadows = true;
        }
    }
}
