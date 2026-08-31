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
        public const string MotherCandidateModelPath =
            CandidateRoot + "MotherV1/mother-blender-humanoid-v1.fbx";
        public const string OlderSisterCandidateModelPath =
            CandidateRoot + "OlderSisterV1/older-sister-blender-humanoid-v1.fbx";
        public const string PlayerRuntime2DV2CandidateModelPath =
            CandidateRoot + "PlayerV4/player-runtime2d-humanoid-v4.fbx";
        public const string MotherRuntime2DV2CandidateModelPath =
            CandidateRoot + "MotherV2/mother-blender-humanoid-v2.fbx";
        public const string OlderSisterRuntime2DV2CandidateModelPath =
            CandidateRoot + "OlderSisterV2/older-sister-blender-humanoid-v2.fbx";
        public const string FatherApprovedV14CandidateModelPath =
            CandidateRoot + "FatherApprovedV14/father-approved-v14-rigged.fbx";
        public const string FatherApprovedV14NaturalWalkRigModelPath =
            CandidateRoot +
            "FatherApprovedV14NaturalWalkRigV1/father-approved-v14-natural-walk-rig-v1.fbx";
        public const string FatherV18HiggsfieldMotionModelPath =
            CandidateRoot +
            "FatherV18HiggsfieldMotionV19/father-v18-higgsfield-motion-v19-idle.fbx";
        public const string FatherV18HiggsfieldRunClipPath =
            CandidateRoot +
            "FatherV18HiggsfieldMotionV19/father-v18-higgsfield-motion-v19-run-644.fbx";

        // Action 613 Casual_Walk_inplace, generated 2026-08-26 from the same Tripo base as idle-0
        // and run-644. It replaces the sprint as the moving clip: measured on the source GLBs, the
        // sprint drops the hips 15 % and bobs them 3.7x more because it has a flight phase, and its
        // stride implies 1.79 u/s against a 0.666 u/s office walk. These two import with the same
        // Humanoid mapping and loop/root-lock settings as the V19 pair.
        public const string FatherV18HiggsfieldCasualWalkModelPath =
            CandidateRoot +
            "FatherV18HiggsfieldCasualWalk613/father-v18-higgsfield-casual-walk-613-idle.fbx";
        public const string FatherV18HiggsfieldCasualWalkClipPath =
            CandidateRoot +
            "FatherV18HiggsfieldCasualWalk613/father-v18-higgsfield-casual-walk-613-walk.fbx";
        public const string FatherV18CleanBipedRigPath =
            CandidateRoot +
            "FatherV18CleanBipedRigV1/father-v18-clean-biped-rig-v1.fbx";
        public const string FatherV18CleanBipedRigV2Path =
            CandidateRoot +
            "FatherV18CleanBipedRigV2/father-v18-clean-biped-rig-v2.fbx";
        public const string FatherV18CleanBipedRigV4Path =
            CandidateRoot +
            "FatherV18CleanBipedRigV4/father-v18-clean-biped-rig-v4.fbx";
        public const string FatherV19MeshyOnePackage613Path =
            CandidateRoot +
            "FatherV19MeshyOnePackage613/father-v19-meshy-one-package-613.fbx";

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
                   string.Equals(path, MotherCandidateModelPath, StringComparison.Ordinal) ||
                   string.Equals(path, OlderSisterCandidateModelPath, StringComparison.Ordinal) ||
                   string.Equals(path, FatherApprovedV14CandidateModelPath, StringComparison.Ordinal) ||
                   string.Equals(path, FatherApprovedV14NaturalWalkRigModelPath, StringComparison.Ordinal) ||
                   string.Equals(path, FatherV18CleanBipedRigPath, StringComparison.Ordinal) ||
                   string.Equals(path, FatherV18CleanBipedRigV2Path, StringComparison.Ordinal) ||
                   string.Equals(path, FatherV18CleanBipedRigV4Path, StringComparison.Ordinal) ||
                   string.Equals(path, FatherV19MeshyOnePackage613Path, StringComparison.Ordinal) ||
                   IsFatherV18HiggsfieldMotion(path) ||
                   IsRuntime2DV2Candidate(path);
        }

        private static bool IsFatherV18HiggsfieldMotion(string path)
        {
            return string.Equals(path, FatherV18HiggsfieldMotionModelPath, StringComparison.Ordinal) ||
                   string.Equals(path, FatherV18HiggsfieldRunClipPath, StringComparison.Ordinal) ||
                   string.Equals(path, FatherV18HiggsfieldCasualWalkModelPath, StringComparison.Ordinal) ||
                   string.Equals(path, FatherV18HiggsfieldCasualWalkClipPath, StringComparison.Ordinal) ||
                   string.Equals(path, FatherV19MeshyOnePackage613Path, StringComparison.Ordinal);
        }

        public static bool IsRuntime2DV2Candidate(string path)
        {
            return string.Equals(path, PlayerRuntime2DV2CandidateModelPath, StringComparison.Ordinal) ||
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
            importer.importAnimation = proxy || IsFatherV18HiggsfieldMotion(assetPath);
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
                    : string.Equals(assetPath, FatherV18CleanBipedRigPath, StringComparison.Ordinal) ||
                      string.Equals(assetPath, FatherV18CleanBipedRigV2Path, StringComparison.Ordinal) ||
                      string.Equals(assetPath, FatherV18CleanBipedRigV4Path, StringComparison.Ordinal)
                        ? CreateExplicitFatherV18CleanBipedHumanDescription(importer.humanDescription)
                    : IsFatherV18HiggsfieldMotion(assetPath)
                        ? CreateExplicitFatherV18HiggsfieldHumanDescription(importer.humanDescription)
                        : CreateExplicitIdentityHumanDescription(importer.humanDescription);
                if (IsFatherV18HiggsfieldMotion(assetPath))
                {
                    ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
                    bool fatherV18CasualWalk = string.Equals(
                        assetPath,
                        FatherV18HiggsfieldCasualWalkClipPath,
                        StringComparison.Ordinal);
                    bool fatherV19OnePackage = string.Equals(
                        assetPath,
                        FatherV19MeshyOnePackage613Path,
                        StringComparison.Ordinal);
                    if ((fatherV18CasualWalk || fatherV19OnePackage) &&
                        clips.Length == 0)
                    {
                        // Unity 6 can expose the FBX take as an implicit clip while returning an
                        // empty defaultClipAnimations array. In that case the old loop silently did
                        // nothing and the player sampled all 127 frames (two gait cycles) inside one
                        // office cycle. Create the intended single-cycle clip explicitly.
                        clips = new[]
                        {
                            new ModelImporterClipAnimation
                            {
                                name = fatherV19OnePackage
                                    ? "FatherV19_Casual_Walk_inplace"
                                    : "Casual_Walk_inplace",
                                takeName = fatherV19OnePackage
                                    ? "FatherV19_Casual_Walk_inplace"
                                    : "Scene",
                                firstFrame = fatherV19OnePackage ? 1f : 16f,
                                lastFrame = fatherV19OnePackage ? 43f : 58f
                            }
                        };
                    }
                    for (var index = 0; index < clips.Length; index++)
                    {
                        // Casual_Walk_inplace exports 127 frames holding two complete gait cycles,
                        // but Family3DWalkActor maps one phase wrap onto the whole clip and
                        // ApplyFootPlant latches once per wrap, so a two-cycle clip would plant on
                        // only one of the two. Measured on the source curve, the left-leg swing
                        // crosses its mean upward at frames 15.67, 57.85 and 100.27; frames 16 to 58
                        // are therefore one cycle, and the 0.006 s of phase the integer boundary
                        // gives up is 0.4 % of a cycle.
                        if (fatherV18CasualWalk)
                        {
                            clips[index].firstFrame = 16f;
                            clips[index].lastFrame = 58f;
                        }
                        else if (fatherV19OnePackage)
                        {
                            // The new one-package action contains three repeated authored cycles.
                            // Direct bone sampling measured the minimum full-pose recurrence at an
                            // exact 42 frames (1.4 s at 30 fps). Keep one untouched cycle; sampling
                            // all 127 frames would triple the cadence and hide contact problems.
                            clips[index].firstFrame = 1f;
                            clips[index].lastFrame = 43f;
                        }
                        clips[index].loopTime = true;
                        clips[index].loopPose = true;
                        clips[index].lockRootRotation = true;
                        clips[index].lockRootHeightY = true;
                        clips[index].lockRootPositionXZ = true;
                        clips[index].keepOriginalOrientation = true;
                        clips[index].keepOriginalPositionY = true;
                        clips[index].keepOriginalPositionXZ = true;
                    }
                    if (clips.Length > 0)
                        importer.clipAnimations = clips;
                }
            }
        }

        private static HumanDescription CreateExplicitFatherV18HiggsfieldHumanDescription(
            HumanDescription description)
        {
            description.human = new[]
            {
                Map("Hips", "Hips"),
                Map("Spine", "Spine02"),
                Map("Chest", "Spine01"),
                Map("UpperChest", "Spine"),
                Map("Neck", "neck"),
                Map("Head", "Head"),
                Map("LeftShoulder", "LeftShoulder"),
                Map("LeftUpperArm", "LeftArm"),
                Map("LeftLowerArm", "LeftForeArm"),
                Map("LeftHand", "LeftHand"),
                Map("RightShoulder", "RightShoulder"),
                Map("RightUpperArm", "RightArm"),
                Map("RightLowerArm", "RightForeArm"),
                Map("RightHand", "RightHand"),
                Map("LeftUpperLeg", "LeftUpLeg"),
                Map("LeftLowerLeg", "LeftLeg"),
                Map("LeftFoot", "LeftFoot"),
                Map("LeftToes", "LeftToeBase"),
                Map("RightUpperLeg", "RightUpLeg"),
                Map("RightLowerLeg", "RightLeg"),
                Map("RightFoot", "RightFoot"),
                Map("RightToes", "RightToeBase")
            };
            return description;
        }

        private static HumanDescription CreateExplicitFatherV18CleanBipedHumanDescription(
            HumanDescription description)
        {
            description.human = new[]
            {
                Map("Hips", "Hips"),
                Map("Spine", "Spine"),
                Map("Chest", "Spine01"),
                Map("UpperChest", "Spine02"),
                Map("Neck", "neck"),
                Map("Head", "Head"),
                Map("LeftShoulder", "LeftShoulder"),
                Map("LeftUpperArm", "LeftArm"),
                Map("LeftLowerArm", "LeftForeArm"),
                Map("LeftHand", "LeftHand"),
                Map("RightShoulder", "RightShoulder"),
                Map("RightUpperArm", "RightArm"),
                Map("RightLowerArm", "RightForeArm"),
                Map("RightHand", "RightHand"),
                Map("LeftUpperLeg", "LeftUpLeg"),
                Map("LeftLowerLeg", "LeftLeg"),
                Map("LeftFoot", "LeftFoot"),
                Map("LeftToes", "LeftToeBase"),
                Map("RightUpperLeg", "RightUpLeg"),
                Map("RightLowerLeg", "RightLeg"),
                Map("RightFoot", "RightFoot"),
                Map("RightToes", "RightToeBase")
            };
            return description;
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
