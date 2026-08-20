using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FamilyCompany.Editor
{
    /// <summary>
    /// Exports the downloaded Mixamo walk as six locked motion-reference poses. This does not
    /// author or promote game art; it only supplies joints/contact evidence for the 2D east walk.
    /// </summary>
    public static class PlayerWalkMotionReferenceExporter
    {
        private const string RigPath =
            "Assets/FamilyCompany/Editor/PlayerWalkHumanoidAuthoring/PlayerHumanoidBase.fbx";
        private const string ClipPath =
            "Assets/FamilyCompany/Editor/PlayerWalkHumanoidAuthoring/PlayerHumanoidWalk.fbx";
        private const int SearchIntervals = 720;
        private const int PoseCount = 6;

        public static void RunFromCommandLine()
        {
            try
            {
                Export();
                Debug.Log("PLAYER_WALK_MOTION_REFERENCE: PASS");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("PLAYER_WALK_MOTION_REFERENCE: FAIL | " + exception.Message);
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("Family Company/Art/Export Mixamo Walk Motion Reference")]
        public static void Export()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RigPath);
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            if (prefab == null) throw new InvalidOperationException("Missing rig: " + RigPath);
            if (clip == null) throw new InvalidOperationException("Missing clip: " + ClipPath);
            if (clip.length <= 0f) throw new InvalidOperationException("Walk clip has no duration.");

            GameObject instance = null;
            try
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                if (instance == null) throw new InvalidOperationException("Could not instantiate rig.");
                Animator animator = instance.GetComponent<Animator>();
                if (animator == null || animator.avatar == null || !animator.avatar.isValid ||
                    !animator.avatar.isHuman)
                    throw new InvalidOperationException("Mixamo reference rig is not a valid Humanoid.");

                var bones = ResolveBones(animator);
                AnimationMode.StartAnimationMode();
                try
                {
                    float phaseZero = FindLeftContactStart(instance, clip, bones.LeftFoot, bones.RightFoot);
                    var receipt = new MotionReceipt
                    {
                        contract = "FC-PLAYER-EAST-MIXAMO-MOTION-REFERENCE-V1",
                        status = "REFERENCE_ONLY_NOT_GAME_ART",
                        sourceRig = RigPath,
                        sourceClip = ClipPath,
                        sourceClipLengthSeconds = clip.length,
                        kshopReferenceCycleSeconds = 0.8f,
                        eastYawDegrees = 90f,
                        phaseZeroSourceSeconds = phaseZero,
                        sampleIndices24 = new[] { 0, 4, 8, 12, 16, 20 },
                        poses = new MotionPose[PoseCount]
                    };

                    for (var pose = 0; pose < PoseCount; pose++)
                    {
                        float normalized = pose / (float)PoseCount;
                        float sourceTime = Mathf.Repeat(phaseZero + clip.length * normalized, clip.length);
                        Sample(instance, clip, sourceTime);

                        // SampleAnimationClip owns the root transform. Compose east yaw afterwards,
                        // then remove planar root travel so the exported skeleton is in-place.
                        // Mixamo's authored forward is +Z. +90 degrees maps it to screen/world +X.
                        instance.transform.RotateAround(Vector3.zero, Vector3.up, 90f);
                        Vector3 planar = new Vector3(bones.Hips.position.x, 0f, bones.Hips.position.z);
                        instance.transform.position -= planar;

                        receipt.poses[pose] = CapturePose(pose, normalized, sourceTime, bones);
                    }

                    string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ??
                                         throw new InvalidOperationException("Cannot resolve project root.");
                    string outputDirectory = Path.Combine(
                        projectRoot, "Artifacts", "PlayerEastMixamoTraceCandidate");
                    Directory.CreateDirectory(outputDirectory);
                    string outputPath = Path.Combine(outputDirectory, "mixamo-east-6pose-joints.json");
                    File.WriteAllText(outputPath, JsonUtility.ToJson(receipt, true));
                    Debug.Log(
                        $"PLAYER_WALK_MOTION_REFERENCE: wrote={outputPath} " +
                        $"clip={clip.length:F6}s phaseZero={phaseZero:F6}s poses={PoseCount}");
                }
                finally
                {
                    AnimationMode.StopAnimationMode();
                }
            }
            finally
            {
                if (instance != null) Object.DestroyImmediate(instance);
            }
        }

        private static void Sample(GameObject instance, AnimationClip clip, float time)
        {
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            AnimationMode.SampleAnimationClip(instance, clip, time);
        }

        private static float FindLeftContactStart(
            GameObject instance,
            AnimationClip clip,
            Transform leftFoot,
            Transform rightFoot)
        {
            var left = new Vector3[SearchIntervals + 1];
            var right = new Vector3[SearchIntervals + 1];
            for (var index = 0; index <= SearchIntervals; index++)
            {
                float time = clip.length * index / SearchIntervals;
                Sample(instance, clip, time);
                left[index] = leftFoot.position;
                right[index] = rightFoot.position;
            }

            var leftIsSlower = new bool[SearchIntervals];
            for (var index = 0; index < SearchIntervals; index++)
            {
                int next = (index + 1) % SearchIntervals;
                float leftStep = HorizontalDistance(left[index], left[next]);
                float rightStep = HorizontalDistance(right[index], right[next]);
                leftIsSlower[index] = leftStep < rightStep;
            }

            int bestStart = -1;
            int bestLength = 0;
            for (var index = 0; index < SearchIntervals; index++)
            {
                int previous = (index - 1 + SearchIntervals) % SearchIntervals;
                if (!leftIsSlower[index] || leftIsSlower[previous]) continue;
                var length = 0;
                while (length < SearchIntervals && leftIsSlower[(index + length) % SearchIntervals])
                    length++;
                if (length <= bestLength) continue;
                bestLength = length;
                bestStart = index;
            }

            if (bestStart < 0)
                throw new InvalidOperationException("Could not find the Mixamo left-foot contact boundary.");
            return clip.length * bestStart / SearchIntervals;
        }

        private static float HorizontalDistance(Vector3 from, Vector3 to)
        {
            float dx = to.x - from.x;
            float dz = to.z - from.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static MotionPose CapturePose(
            int pose,
            float normalized,
            float sourceTime,
            BoneSet bones)
        {
            Transform hips = bones.Hips;
            return new MotionPose
            {
                pose = pose,
                kshopResampledIndex24 = pose * 4,
                normalizedPhase = normalized,
                sourceSeconds = sourceTime,
                kshopReferenceSeconds = 0.8f * normalized,
                expectedSupportLeg = pose < 3 ? "left" : "right",
                hips = CaptureJoint(hips, hips),
                head = CaptureJoint(bones.Head, hips),
                leftUpperLeg = CaptureJoint(bones.LeftUpperLeg, hips),
                leftKnee = CaptureJoint(bones.LeftKnee, hips),
                leftAnkle = CaptureJoint(bones.LeftFoot, hips),
                leftToe = CaptureJoint(bones.LeftToe, hips),
                rightUpperLeg = CaptureJoint(bones.RightUpperLeg, hips),
                rightKnee = CaptureJoint(bones.RightKnee, hips),
                rightAnkle = CaptureJoint(bones.RightFoot, hips),
                rightToe = CaptureJoint(bones.RightToe, hips),
                leftShoulder = CaptureJoint(bones.LeftUpperArm, hips),
                leftElbow = CaptureJoint(bones.LeftLowerArm, hips),
                leftWrist = CaptureJoint(bones.LeftHand, hips),
                rightShoulder = CaptureJoint(bones.RightUpperArm, hips),
                rightElbow = CaptureJoint(bones.RightLowerArm, hips),
                rightWrist = CaptureJoint(bones.RightHand, hips),
                leftFootHeightWorld = bones.LeftFoot.position.y,
                leftToeHeightWorld = bones.LeftToe.position.y,
                rightFootHeightWorld = bones.RightFoot.position.y,
                rightToeHeightWorld = bones.RightToe.position.y
            };
        }

        private static JointSample CaptureJoint(Transform joint, Transform hips)
        {
            Vector3 world = joint.position;
            Vector3 relative = world - hips.position;
            float pitch = PlayerWalkHumanoidBaker.OfficeCameraPitchDegrees;
            Vector3 cameraUp = Quaternion.Euler(pitch, 0f, 0f) * Vector3.up;
            Vector3 cameraForward = Quaternion.Euler(pitch, 0f, 0f) * Vector3.forward;
            return new JointSample
            {
                world = world,
                hipsRelativeWorld = relative,
                eastScreen = new Vector2(relative.x, Vector3.Dot(relative, cameraUp)),
                cameraDepth = Vector3.Dot(relative, cameraForward)
            };
        }

        private static BoneSet ResolveBones(Animator animator) => new BoneSet
        {
            Hips = RequireBone(animator, HumanBodyBones.Hips),
            Head = RequireBone(animator, HumanBodyBones.Head),
            LeftUpperLeg = RequireBone(animator, HumanBodyBones.LeftUpperLeg),
            LeftKnee = RequireBone(animator, HumanBodyBones.LeftLowerLeg),
            LeftFoot = RequireBone(animator, HumanBodyBones.LeftFoot),
            LeftToe = RequireBone(animator, HumanBodyBones.LeftToes),
            RightUpperLeg = RequireBone(animator, HumanBodyBones.RightUpperLeg),
            RightKnee = RequireBone(animator, HumanBodyBones.RightLowerLeg),
            RightFoot = RequireBone(animator, HumanBodyBones.RightFoot),
            RightToe = RequireBone(animator, HumanBodyBones.RightToes),
            LeftUpperArm = RequireBone(animator, HumanBodyBones.LeftUpperArm),
            LeftLowerArm = RequireBone(animator, HumanBodyBones.LeftLowerArm),
            LeftHand = RequireBone(animator, HumanBodyBones.LeftHand),
            RightUpperArm = RequireBone(animator, HumanBodyBones.RightUpperArm),
            RightLowerArm = RequireBone(animator, HumanBodyBones.RightLowerArm),
            RightHand = RequireBone(animator, HumanBodyBones.RightHand)
        };

        private static Transform RequireBone(Animator animator, HumanBodyBones bone)
        {
            Transform transform = animator.GetBoneTransform(bone);
            if (transform == null) throw new InvalidOperationException("Humanoid bone missing: " + bone);
            return transform;
        }

        private sealed class BoneSet
        {
            public Transform Hips;
            public Transform Head;
            public Transform LeftUpperLeg;
            public Transform LeftKnee;
            public Transform LeftFoot;
            public Transform LeftToe;
            public Transform RightUpperLeg;
            public Transform RightKnee;
            public Transform RightFoot;
            public Transform RightToe;
            public Transform LeftUpperArm;
            public Transform LeftLowerArm;
            public Transform LeftHand;
            public Transform RightUpperArm;
            public Transform RightLowerArm;
            public Transform RightHand;
        }

        [Serializable]
        private sealed class MotionReceipt
        {
            public string contract;
            public string status;
            public string sourceRig;
            public string sourceClip;
            public float sourceClipLengthSeconds;
            public float kshopReferenceCycleSeconds;
            public float eastYawDegrees;
            public float phaseZeroSourceSeconds;
            public int[] sampleIndices24;
            public MotionPose[] poses;
        }

        [Serializable]
        private sealed class MotionPose
        {
            public int pose;
            public int kshopResampledIndex24;
            public float normalizedPhase;
            public float sourceSeconds;
            public float kshopReferenceSeconds;
            public string expectedSupportLeg;
            public JointSample hips;
            public JointSample head;
            public JointSample leftUpperLeg;
            public JointSample leftKnee;
            public JointSample leftAnkle;
            public JointSample leftToe;
            public JointSample rightUpperLeg;
            public JointSample rightKnee;
            public JointSample rightAnkle;
            public JointSample rightToe;
            public JointSample leftShoulder;
            public JointSample leftElbow;
            public JointSample leftWrist;
            public JointSample rightShoulder;
            public JointSample rightElbow;
            public JointSample rightWrist;
            public float leftFootHeightWorld;
            public float leftToeHeightWorld;
            public float rightFootHeightWorld;
            public float rightToeHeightWorld;
        }

        [Serializable]
        private sealed class JointSample
        {
            public Vector3 world;
            public Vector3 hipsRelativeWorld;
            public Vector2 eastScreen;
            public float cameraDepth;
        }
    }
}
