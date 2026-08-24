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
                   string.Equals(path, OlderSisterCandidateModelPath, StringComparison.Ordinal);
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
                importer.humanDescription = CreateExplicitIdentityHumanDescription(importer.humanDescription);
            }
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
