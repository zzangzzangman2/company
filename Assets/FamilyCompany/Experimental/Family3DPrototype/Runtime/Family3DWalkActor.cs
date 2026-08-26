using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace FamilyCompany.Experimental.Family3D
{
    /// <summary>
    /// One bottom-centre movement root driving one complete skinned humanoid.
    /// The animation graph is sampled from a shared clock so all family members stay phase locked.
    /// </summary>
    public sealed class Family3DWalkActor : MonoBehaviour
    {
        public const float LockedCycleSeconds = 0.99380799f;
        private const float FatherSdCycleSeconds = 0.88f;

        [SerializeField] private string familyId = string.Empty;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Animator animator;
        [SerializeField] private AnimationClip walkClip;
        [SerializeField] private AnimationClip idleClip;
        [SerializeField] private Vector3 pathCenter;
        [SerializeField] private Color labelColor = Color.white;
        [SerializeField, Range(0f, 1f)] private float poseStrength = 1f;
        [SerializeField] private bool dedicatedNaturalSdWalk;

        private PlayableGraph graph;
        private AnimationClipPlayable clipPlayable;
        private AnimationClipPlayable idlePlayable;
        private AnimationMixerPlayable clipMixer;
        private Transform leftFoot;
        private Transform rightFoot;
        private Transform hips;
        private Transform leftUpperLeg;
        private Transform rightUpperLeg;
        private Transform leftLowerLeg;
        private Transform rightLowerLeg;
        private Transform leftToes;
        private Transform rightToes;
        private Transform leftUpperArm;
        private Transform rightUpperArm;
        private Transform leftLowerArm;
        private Transform rightLowerArm;
        private Transform leftHand;
        private Transform rightHand;
        private Vector3 visualLocalPosition;
        private Quaternion visualLocalRotation;
        private float phaseOffset;
        private float standingHeight;
        private TransformRest[] restPose = Array.Empty<TransformRest>();
        private HumanPoseHandler humanPoseHandler;
        private HumanPose restHumanPose;
        private HumanPose sampledHumanPose;
        private int leftUpperLegFrontBack = -1;
        private int rightUpperLegFrontBack = -1;
        private int leftLowerLegStretch = -1;
        private int rightLowerLegStretch = -1;
        private int leftArmDownUp = -1;
        private int rightArmDownUp = -1;
        private int leftArmFrontBack = -1;
        private int rightArmFrontBack = -1;
        private int leftForearmStretch = -1;
        private int rightForearmStretch = -1;
        private Vector3 hipsRestLocalPosition;
        private Vector3 leftFootPlantWorld;
        private Vector3 rightFootPlantWorld;
        private Vector3 lastRootWorldPosition;
        private Quaternion lastRootWorldRotation;
        private bool leftFootPlanted;
        private bool rightFootPlanted;
        private bool leftFootContactLocked;
        private bool rightFootContactLocked;
        private bool hasLastRootPose;
        private bool initialized;
        private float moveBlend01;
        private double idleClock;

        public string FamilyId => familyId;
        public Vector3 PathCenter => pathCenter;
        public Color LabelColor => labelColor;
        public AnimationClip WalkClip => walkClip;
        public AnimationClip IdleClip => idleClip;
        public float PhaseOffset => phaseOffset;
        public float StandingHeight => standingHeight;
        public float PoseStrength => poseStrength;
        public bool DedicatedNaturalSdWalk => dedicatedNaturalSdWalk;
        public bool LeftFootPlanted => leftFootContactLocked;
        public bool RightFootPlanted => rightFootContactLocked;

        public void Configure(
            string id,
            Transform modelRoot,
            Animator modelAnimator,
            AnimationClip sharedWalkClip,
            Vector3 center,
            Color color,
            float animationPoseStrength = 1f,
            bool useDedicatedNaturalSdWalk = false,
            AnimationClip stationaryIdleClip = null)
        {
            familyId = id;
            visualRoot = modelRoot;
            animator = modelAnimator;
            walkClip = sharedWalkClip;
            idleClip = stationaryIdleClip;
            pathCenter = center;
            labelColor = color;
            poseStrength = Mathf.Clamp01(animationPoseStrength);
            dedicatedNaturalSdWalk = useDedicatedNaturalSdWalk;
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
                Initialize();
        }

        private void OnDisable()
        {
            DestroyGraph();
        }

        private void OnDestroy()
        {
            DestroyGraph();
        }

        public void Initialize()
        {
            if (initialized)
                return;
            if (visualRoot == null || animator == null || (!dedicatedNaturalSdWalk && walkClip == null))
                throw new InvalidOperationException(
                    familyId + " is missing its visual root, Animator, or required shared walk clip.");
            if (animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
                throw new InvalidOperationException(familyId + " does not have a valid Humanoid Avatar.");

            visualLocalPosition = visualRoot.localPosition;
            visualLocalRotation = visualRoot.localRotation;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.Rebind();
            CaptureRestPose();

            leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            leftUpperLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            rightUpperLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            leftLowerLeg = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            rightLowerLeg = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            leftToes = animator.GetBoneTransform(HumanBodyBones.LeftToes);
            rightToes = animator.GetBoneTransform(HumanBodyBones.RightToes);
            leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            leftLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            rightLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (leftFoot == null || rightFoot == null || hips == null)
                throw new InvalidOperationException(familyId + " is missing Humanoid foot or hips mappings.");
            hipsRestLocalPosition = hips.localPosition;

            if (dedicatedNaturalSdWalk)
            {
                InitializeNaturalSdPose();
                phaseOffset = 0f;
            }
            else
            {
                graph = PlayableGraph.Create("Family3DWalk_" + familyId);
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                clipPlayable = AnimationClipPlayable.Create(graph, walkClip);
                clipPlayable.SetApplyFootIK(true);
                clipPlayable.SetApplyPlayableIK(true);
                AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Walk", animator);
                if (idleClip != null)
                {
                    idlePlayable = AnimationClipPlayable.Create(graph, idleClip);
                    idlePlayable.SetApplyFootIK(true);
                    idlePlayable.SetApplyPlayableIK(true);
                    clipMixer = AnimationMixerPlayable.Create(graph, 2, true);
                    graph.Connect(idlePlayable, 0, clipMixer, 0);
                    graph.Connect(clipPlayable, 0, clipMixer, 1);
                    clipMixer.SetInputWeight(0, 1f);
                    clipMixer.SetInputWeight(1, 0f);
                    output.SetSourcePlayable(clipMixer);
                    moveBlend01 = 0f;
                    idleClock = 0d;
                }
                else
                {
                    output.SetSourcePlayable(clipPlayable);
                }
                graph.Play();
                phaseOffset = FindLeftForwardContactPhase();
            }

            SamplePose(0d, false);
            standingHeight = MeasureStandingHeight();
            initialized = true;
        }

        public void Tick(
            double sharedMotionClock,
            Vector3 worldPosition,
            Quaternion rootRotation,
            bool isMoving = true)
        {
            Initialize();
            bool rootDiscontinuity = !hasLastRootPose ||
                                     Vector3.Distance(lastRootWorldPosition, worldPosition) > 0.25f ||
                                     Quaternion.Angle(lastRootWorldRotation, rootRotation) > 60f;
            transform.SetPositionAndRotation(worldPosition, rootRotation);
            if (!isMoving || rootDiscontinuity)
                ResetFootPlants();
            SamplePose(sharedMotionClock, isMoving);

            // The outside root is the only owner of translation and direction. Retargeted root
            // curves are forcibly discarded so head, waist, and legs cannot turn independently.
            lastRootWorldPosition = worldPosition;
            lastRootWorldRotation = rootRotation;
            hasLastRootPose = true;
        }

        public void RebaseVisualRootAfterScale()
        {
            Initialize();
            visualLocalPosition = visualRoot.localPosition;
            visualLocalRotation = visualRoot.localRotation;
            standingHeight = MeasureStandingHeight();
            ResetFootPlants();
            hasLastRootPose = false;
        }

        public PoseSnapshot ReadPoseSnapshot()
        {
            Initialize();
            Vector3 leftLocal = transform.InverseTransformPoint(leftFoot.position);
            Vector3 rightLocal = transform.InverseTransformPoint(rightFoot.position);
            Vector3 hipsLocal = transform.InverseTransformPoint(hips.position);
            return new PoseSnapshot
            {
                leftFootLocal = leftLocal,
                rightFootLocal = rightLocal,
                leftFootWorld = leftFoot.position,
                rightFootWorld = rightFoot.position,
                hipsLocal = hipsLocal,
                footLead = leftLocal.z - rightLocal.z,
                leftFootPlanted = leftFootContactLocked,
                rightFootPlanted = rightFootContactLocked,
                standingHeight = Mathf.Max(standingHeight, 0.0001f),
                visualRootPositionError = Vector3.Distance(visualRoot.localPosition, visualLocalPosition),
                visualRootRotationErrorDegrees = Quaternion.Angle(visualRoot.localRotation, visualLocalRotation)
            };
        }

        private void SamplePose(double sharedMotionClock, bool isMoving)
        {
            double cycleSeconds = dedicatedNaturalSdWalk ? FatherSdCycleSeconds : LockedCycleSeconds;
            double normalized = sharedMotionClock / cycleSeconds + phaseOffset;
            float phase = Mathf.Repeat((float)normalized, 1f);
            if (dedicatedNaturalSdWalk)
            {
                ApplyNaturalSdPose(phase, isMoving);
            }
            else
            {
                double clipTime = phase * walkClip.length;
                clipPlayable.SetTime(clipTime);
                if (idleClip != null)
                {
                    idleClock += Time.unscaledDeltaTime;
                    idlePlayable.SetTime(idleClock % Math.Max(idleClip.length, 0.0001f));
                    moveBlend01 = Mathf.MoveTowards(
                        moveBlend01,
                        isMoving ? 1f : 0f,
                        Time.unscaledDeltaTime / 0.12f);
                    clipMixer.SetInputWeight(0, 1f - moveBlend01);
                    clipMixer.SetInputWeight(1, moveBlend01);
                }
                graph.Evaluate(0f);

                if (poseStrength < 0.9999f)
                    ApplyPoseStrength();
            }

            // Humanoid clips may contain extracted root translation/rotation. The visual child is
            // reset after every sample; the bottom-centre host above it remains the sole root.
            visualRoot.localPosition = visualLocalPosition;
            visualRoot.localRotation = visualLocalRotation;
            // Ungated on 2026-08-26. The imported-clip branch previously had no ground constraint
            // of any kind, so leftFootPlanted/rightFootPlanted were false in 180 of 180 V22 samples
            // and QA could neither prevent slip nor detect it. Both branches align phase 0 to the
            // left foot's forward contact — the SD path by construction, the imported path through
            // FindLeftForwardContactPhase — which is the alignment ApplyFootPlant expects.
            if (isMoving)
                ApplyFootPlants(phase);
        }

        private void InitializeNaturalSdPose()
        {
            humanPoseHandler = new HumanPoseHandler(animator.avatar, animator.transform);
            restHumanPose = new HumanPose();
            humanPoseHandler.GetHumanPose(ref restHumanPose);
            if (restHumanPose.muscles == null ||
                restHumanPose.muscles.Length != HumanTrait.MuscleCount)
                throw new InvalidOperationException(
                    familyId + " could not capture a complete Humanoid rest pose.");

            sampledHumanPose = new HumanPose
            {
                bodyPosition = restHumanPose.bodyPosition,
                bodyRotation = restHumanPose.bodyRotation,
                muscles = (float[])restHumanPose.muscles.Clone()
            };

            leftUpperLegFrontBack = FindMuscle("Left Upper Leg Front-Back");
            rightUpperLegFrontBack = FindMuscle("Right Upper Leg Front-Back");
            leftLowerLegStretch = FindMuscle("Left Lower Leg Stretch");
            rightLowerLegStretch = FindMuscle("Right Lower Leg Stretch");
            leftArmDownUp = FindMuscle("Left Arm Down-Up");
            rightArmDownUp = FindMuscle("Right Arm Down-Up");
            leftArmFrontBack = FindMuscle("Left Arm Front-Back");
            rightArmFrontBack = FindMuscle("Right Arm Front-Back");
            leftForearmStretch = FindMuscle("Left Forearm Stretch");
            rightForearmStretch = FindMuscle("Right Forearm Stretch");
        }

        private void ApplyNaturalSdPose(float phase, bool isMoving)
        {
            Array.Copy(
                restHumanPose.muscles,
                sampledHumanPose.muscles,
                restHumanPose.muscles.Length);
            sampledHumanPose.bodyPosition = restHumanPose.bodyPosition;
            sampledHumanPose.bodyRotation = restHumanPose.bodyRotation;

            if (isMoving)
            {
                float leftLegPhase = phase;
                float rightLegPhase = Mathf.Repeat(phase + 0.5f, 1f);
                ApplyNaturalSdLeg(leftLegPhase, leftUpperLegFrontBack, leftLowerLegStretch);
                ApplyNaturalSdLeg(rightLegPhase, rightUpperLegFrontBack, rightLowerLegStretch);
            }

            // Blue Archive-style SD motion keeps the torso readable while the bent arms make a
            // clear opposite swing. H failed because reducing the rubbery pelvis also reduced the
            // arms until they looked frozen.
            AddMuscle(leftArmDownUp, -0.60f);
            AddMuscle(rightArmDownUp, -0.60f);
            AddMuscle(leftForearmStretch, -0.24f);
            AddMuscle(rightForearmStretch, -0.24f);
            if (isMoving)
            {
                float legLead = EvaluateStylizedStepLead(phase);
                AddMuscle(leftArmFrontBack, -0.16f * legLead);
                AddMuscle(rightArmFrontBack, 0.16f * legLead);
                // The forward hand closes toward the waist while the rear elbow opens slightly.
                // This keeps the counter-swing readable after the map camera scales the actor down.
                AddMuscle(leftForearmStretch, 0.045f * legLead);
                AddMuscle(rightForearmStretch, -0.045f * legLead);
            }

            humanPoseHandler.SetHumanPose(ref sampledHumanPose);
            RestoreApprovedUpperBodyRest();
            // No procedural pelvis translation. The G candidate combined a lateral hips offset
            // with IK residual correction and made the entire lower body wobble and stretch.
            hips.localPosition = hipsRestLocalPosition;
        }

        private void ApplyNaturalSdLeg(
            float legPhase,
            int upperLegFrontBack,
            int lowerLegStretch)
        {
            float lead = EvaluateStylizedStepLead(legPhase);
            float swingBend = EvaluateStylizedSwingKnee(legPhase);

            AddMuscle(upperLegFrontBack, 0.18f * lead);
            AddMuscle(lowerLegStretch, -0.34f * swingBend);
        }

        private static float EvaluateStylizedStepLead(float phase)
        {
            phase = Mathf.Repeat(phase, 1f);
            // The planted leg sweeps backward for most of the cycle to match root travel. The
            // return happens in a short lifted interval, producing a readable step rather than
            // H's held-leg glide or G's continuous rubbery sine wave.
            if (phase < 0.08f)
                return Mathf.Lerp(1f, 0.82f, Mathf.InverseLerp(0f, 0.08f, phase));
            if (phase < 0.46f)
                return Mathf.Lerp(0.82f, -1f, Mathf.InverseLerp(0.08f, 0.46f, phase));
            if (phase < 0.56f)
                return -1f;
            if (phase < 0.78f)
                return Mathf.Lerp(-1f, 0.12f, Smooth01(Mathf.InverseLerp(0.56f, 0.78f, phase)));
            if (phase < 0.94f)
                return Mathf.Lerp(0.12f, 1f, Smooth01(Mathf.InverseLerp(0.78f, 0.94f, phase)));
            return 1f;
        }

        private static float EvaluateStylizedSwingKnee(float phase)
        {
            phase = Mathf.Repeat(phase, 1f);
            if (phase < 0.08f)
                return 0.08f * (1f - Mathf.InverseLerp(0f, 0.08f, phase));
            if (phase < 0.56f || phase >= 0.95f)
                return 0f;
            if (phase < 0.72f)
                return Smooth01(Mathf.InverseLerp(0.56f, 0.72f, phase));
            return 1f - Smooth01(Mathf.InverseLerp(0.72f, 0.95f, phase));
        }

        private static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        private void ApplyFootPlants(float phase)
        {
            ApplyFootPlant(
                Mathf.Repeat(phase, 1f),
                leftUpperLeg,
                leftLowerLeg,
                leftFoot,
                ref leftFootPlanted,
                ref leftFootPlantWorld,
                ref leftFootContactLocked);
            ApplyFootPlant(
                Mathf.Repeat(phase + 0.5f, 1f),
                rightUpperLeg,
                rightLowerLeg,
                rightFoot,
                ref rightFootPlanted,
                ref rightFootPlantWorld,
                ref rightFootContactLocked);
        }

        private static void ApplyFootPlant(
            float legPhase,
            Transform upperLeg,
            Transform lowerLeg,
            Transform foot,
            ref bool planted,
            ref Vector3 plantWorld,
            ref bool contactLocked)
        {
            const float contactLockEnd = 0.05f;
            const float toeOffEnd = 0.11f;
            if (legPhase >= toeOffEnd)
            {
                planted = false;
                contactLocked = false;
                return;
            }

            if (!planted)
            {
                plantWorld = foot.position;
                planted = true;
            }
            contactLocked = legPhase < contactLockEnd;

            // Only stabilize the impact. The authored backward stance sweep owns the visible
            // contact; a long IK lock would reintroduce G's rubbery leg deformation.
            float release01 = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(contactLockEnd, toeOffEnd, legPhase));
            Vector3 target = Vector3.Lerp(plantWorld, foot.position, release01);
            SolveTwoBonePlant(upperLeg, lowerLeg, foot, target);
        }

        private static void SolveTwoBonePlant(
            Transform upperLeg,
            Transform lowerLeg,
            Transform foot,
            Vector3 target)
        {
            if (upperLeg == null || lowerLeg == null || foot == null)
                return;

            for (var iteration = 0; iteration < 2; iteration++)
            {
                RotateJointToward(lowerLeg, foot, target, 0.45f, 6f);
                RotateJointToward(upperLeg, foot, target, 0.35f, 5f);
            }
        }

        private static void RotateJointToward(
            Transform joint,
            Transform effector,
            Vector3 target,
            float strength,
            float maximumDegrees)
        {
            Vector3 current = effector.position - joint.position;
            Vector3 desired = target - joint.position;
            if (current.sqrMagnitude <= 0.0000001f || desired.sqrMagnitude <= 0.0000001f)
                return;
            Quaternion correction = Quaternion.FromToRotation(current, desired);
            float correctionDegrees = Quaternion.Angle(Quaternion.identity, correction);
            if (correctionDegrees > maximumDegrees)
                correction = Quaternion.Slerp(
                    Quaternion.identity,
                    correction,
                    maximumDegrees / correctionDegrees);
            joint.rotation = Quaternion.Slerp(Quaternion.identity, correction, strength) * joint.rotation;
        }

        private void ResetFootPlants()
        {
            leftFootPlanted = false;
            rightFootPlanted = false;
            leftFootContactLocked = false;
            rightFootContactLocked = false;
        }

        private void RestoreApprovedUpperBodyRest()
        {
            for (var index = 0; index < restPose.Length; index++)
            {
                TransformRest rest = restPose[index];
                if (rest.Transform == null || IsProceduralLimbBone(rest.Transform))
                    continue;
                rest.Transform.localPosition = rest.LocalPosition;
                rest.Transform.localRotation = rest.LocalRotation;
                rest.Transform.localScale = rest.LocalScale;
            }
        }

        private bool IsProceduralLimbBone(Transform candidate)
        {
            return candidate == leftUpperLeg ||
                   candidate == rightUpperLeg ||
                   candidate == leftLowerLeg ||
                   candidate == rightLowerLeg ||
                   candidate == leftFoot ||
                   candidate == rightFoot ||
                   candidate == leftToes ||
                   candidate == rightToes ||
                   candidate == leftUpperArm ||
                   candidate == rightUpperArm ||
                   candidate == leftLowerArm ||
                   candidate == rightLowerArm ||
                   candidate == leftHand ||
                   candidate == rightHand;
        }

        private static int FindMuscle(string muscleName)
        {
            string[] names = HumanTrait.MuscleName;
            for (var index = 0; index < names.Length; index++)
            {
                if (string.Equals(names[index], muscleName, StringComparison.Ordinal))
                    return index;
            }
            throw new InvalidOperationException("Required Humanoid muscle is missing: " + muscleName);
        }

        private void AddMuscle(int index, float delta)
        {
            sampledHumanPose.muscles[index] = Mathf.Clamp(
                sampledHumanPose.muscles[index] + delta,
                -1f,
                1f);
        }

        private void CaptureRestPose()
        {
            Transform[] transforms = visualRoot.GetComponentsInChildren<Transform>(true);
            var result = new System.Collections.Generic.List<TransformRest>(transforms.Length);
            for (var index = 0; index < transforms.Length; index++)
            {
                Transform current = transforms[index];
                if (current == visualRoot)
                    continue;
                result.Add(new TransformRest(
                    current,
                    current.localPosition,
                    current.localRotation,
                    current.localScale));
            }
            restPose = result.ToArray();
        }

        private void ApplyPoseStrength()
        {
            for (var index = 0; index < restPose.Length; index++)
            {
                TransformRest rest = restPose[index];
                if (rest.Transform == null)
                    continue;
                rest.Transform.localPosition = Vector3.Lerp(
                    rest.LocalPosition,
                    rest.Transform.localPosition,
                    poseStrength);
                rest.Transform.localRotation = Quaternion.Slerp(
                    rest.LocalRotation,
                    rest.Transform.localRotation,
                    poseStrength);
                rest.Transform.localScale = rest.LocalScale;
            }
        }

        private float FindLeftForwardContactPhase()
        {
            const int sampleCount = 180;
            float bestLead = float.NegativeInfinity;
            float bestPhase = 0f;
            if (idleClip != null)
            {
                clipMixer.SetInputWeight(0, 0f);
                clipMixer.SetInputWeight(1, 1f);
            }
            for (var sample = 0; sample < sampleCount; sample++)
            {
                float phase = sample / (float)sampleCount;
                clipPlayable.SetTime(phase * walkClip.length);
                graph.Evaluate(0f);
                visualRoot.localPosition = visualLocalPosition;
                visualRoot.localRotation = visualLocalRotation;
                Vector3 leftLocal = transform.InverseTransformPoint(leftFoot.position);
                Vector3 rightLocal = transform.InverseTransformPoint(rightFoot.position);
                float lead = leftLocal.z - rightLocal.z;
                if (lead <= bestLead)
                    continue;
                bestLead = lead;
                bestPhase = phase;
            }
            if (idleClip != null)
            {
                clipMixer.SetInputWeight(0, 1f);
                clipMixer.SetInputWeight(1, 0f);
            }
            return bestPhase;
        }

        private float MeasureStandingHeight()
        {
            Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
            bool found = false;
            Bounds bounds = default;
            foreach (Renderer renderer in renderers)
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    continue;
                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            return found ? Mathf.Max(bounds.size.y, 0.0001f) : 1f;
        }

        private void DestroyGraph()
        {
            if (graph.IsValid())
                graph.Destroy();
            if (humanPoseHandler != null)
            {
                humanPoseHandler.Dispose();
                humanPoseHandler = null;
            }
            ResetFootPlants();
            hasLastRootPose = false;
            moveBlend01 = 0f;
            idleClock = 0d;
            initialized = false;
        }

        [Serializable]
        public struct PoseSnapshot
        {
            public Vector3 leftFootLocal;
            public Vector3 rightFootLocal;
            public Vector3 leftFootWorld;
            public Vector3 rightFootWorld;
            public Vector3 hipsLocal;
            public float footLead;
            public bool leftFootPlanted;
            public bool rightFootPlanted;
            public float standingHeight;
            public float visualRootPositionError;
            public float visualRootRotationErrorDegrees;
        }

        private readonly struct TransformRest
        {
            public TransformRest(
                Transform transform,
                Vector3 localPosition,
                Quaternion localRotation,
                Vector3 localScale)
            {
                Transform = transform;
                LocalPosition = localPosition;
                LocalRotation = localRotation;
                LocalScale = localScale;
            }

            public Transform Transform { get; }
            public Vector3 LocalPosition { get; }
            public Quaternion LocalRotation { get; }
            public Vector3 LocalScale { get; }
        }
    }
}
