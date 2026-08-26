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
        public const float FatherSdCycleSeconds = 0.88f;

        [SerializeField] private string familyId = string.Empty;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Animator animator;
        [SerializeField] private AnimationClip walkClip;
        [SerializeField] private AnimationClip idleClip;
        [SerializeField] private Vector3 pathCenter;
        [SerializeField] private Color labelColor = Color.white;
        [SerializeField, Range(0f, 1f)] private float poseStrength = 1f;
        [SerializeField] private bool dedicatedNaturalSdWalk;

        /// <summary>
        /// Legacy full-muscle delta-retarget diagnostic. V61 leaves this disabled because moving
        /// every muscle around the source mean also moves its unwanted hunch and cross-axis leg
        /// offsets. The narrower anatomical sanitation path below keeps the action as the motion
        /// source while correcting only the target-avatar channels that visibly fail.
        /// </summary>
        [SerializeField] private bool clipMuscleDeltaRetarget;
        [SerializeField] private bool clipAnatomicalSanitization;
        [SerializeField] private bool clipStableBodySideArms;
        [SerializeField, Range(0f, 10f)] private float naturalSdTorsoUprightDegrees = 5f;
        [SerializeField, Range(0f, 12f)] private float naturalSdArmOutwardDegrees = 2f;
        [SerializeField, Range(0f, 18f)] private float naturalSdArmSwingDegrees = 6f;
        [SerializeField, Range(0f, 24f)] private float naturalSdElbowBendDegrees = 22f;

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
        private Transform leftShoulder;
        private Transform rightShoulder;
        private Transform leftLowerArm;
        private Transform rightLowerArm;
        private Transform leftHand;
        private Transform rightHand;
        private Transform spine;
        private Transform chest;
        private Transform neck;
        private Transform head;
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
        private int leftUpperLegInOut = -1;
        private int rightUpperLegInOut = -1;
        private int leftUpperLegTwistInOut = -1;
        private int rightUpperLegTwistInOut = -1;
        private int leftLowerLegTwistInOut = -1;
        private int rightLowerLegTwistInOut = -1;
        private int leftFootTwistInOut = -1;
        private int rightFootTwistInOut = -1;
        private int leftArmDownUp = -1;
        private int rightArmDownUp = -1;
        private int leftArmFrontBack = -1;
        private int rightArmFrontBack = -1;
        private int leftArmTwistInOut = -1;
        private int rightArmTwistInOut = -1;
        private int leftForearmTwistInOut = -1;
        private int rightForearmTwistInOut = -1;
        private int leftForearmStretch = -1;
        private int rightForearmStretch = -1;
        private int spineFrontBack = -1;
        private int chestFrontBack = -1;
        private int upperChestFrontBack = -1;
        private int neckNodDownUp = -1;
        private int headNodDownUp = -1;
        private float[] clipSanitizationReferenceMuscles;
        private Vector3 clipSagittalForwardLocal = Vector3.forward;
        private float leftFootPlaneLateral;
        private float rightFootPlaneLateral;
        private bool clipFootPlaneReady;
        private Vector3 hipsRestLocalPosition;
        private Vector3 lastRootWorldPosition;
        private Quaternion lastRootWorldRotation;
        private bool leftFootPlanted;
        private bool rightFootPlanted;
        private bool leftFootContactLocked;
        private bool rightFootContactLocked;
        private bool hasLastRootPose;
        private bool initialized;
        private float lastSampledPhase01;
        private HumanPose clipSampledPose;
        private HumanPose clipRetargetOutputPose;
        private float[] clipReferenceMuscles;
        private Vector3 clipReferenceBodyPosition;
        private Quaternion clipReferenceBodyRotation = Quaternion.identity;
        private bool clipRetargetReady;
        private TransformRest[] approvedRigidArmPose = Array.Empty<TransformRest>();
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
        public float CycleSeconds => ResolveCycleSeconds();
        public bool DedicatedNaturalSdWalk => dedicatedNaturalSdWalk;
        public bool LeftFootPlanted => leftFootContactLocked;
        public bool RightFootPlanted => rightFootContactLocked;
        public float NaturalSdTorsoUprightDegrees => naturalSdTorsoUprightDegrees;
        public float NaturalSdArmOutwardDegrees => naturalSdArmOutwardDegrees;
        public float NaturalSdArmSwingDegrees => naturalSdArmSwingDegrees;
        public float NaturalSdElbowBendDegrees => naturalSdElbowBendDegrees;

        private float ResolveCycleSeconds()
        {
            return dedicatedNaturalSdWalk ? FatherSdCycleSeconds : LockedCycleSeconds;
        }

        public void Configure(
            string id,
            Transform modelRoot,
            Animator modelAnimator,
            AnimationClip sharedWalkClip,
            Vector3 center,
            Color color,
            float animationPoseStrength = 1f,
            bool useDedicatedNaturalSdWalk = false,
            AnimationClip stationaryIdleClip = null,
            bool useClipMuscleDeltaRetarget = false,
            bool useClipAnatomicalSanitization = false,
            bool useClipStableBodySideArms = false)
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
            clipMuscleDeltaRetarget = useClipMuscleDeltaRetarget;
            clipAnatomicalSanitization = useClipAnatomicalSanitization;
            clipStableBodySideArms = useClipStableBodySideArms;
        }

        public void ConfigureNaturalSdStyle(
            float torsoUprightDegrees,
            float armOutwardDegrees,
            float armSwingDegrees,
            float elbowBendDegrees)
        {
            naturalSdTorsoUprightDegrees = Mathf.Clamp(torsoUprightDegrees, 0f, 10f);
            naturalSdArmOutwardDegrees = Mathf.Clamp(armOutwardDegrees, 0f, 12f);
            naturalSdArmSwingDegrees = Mathf.Clamp(armSwingDegrees, 0f, 18f);
            naturalSdElbowBendDegrees = Mathf.Clamp(elbowBendDegrees, 0f, 24f);
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
            if (clipStableBodySideArms && !clipAnatomicalSanitization)
                throw new InvalidOperationException(
                    familyId + " stable body-side arms require clip anatomical sanitation.");

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
            leftShoulder = animator.GetBoneTransform(HumanBodyBones.LeftShoulder);
            rightShoulder = animator.GetBoneTransform(HumanBodyBones.RightShoulder);
            leftLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            rightLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            head = animator.GetBoneTransform(HumanBodyBones.Head);
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
                if (clipAnatomicalSanitization)
                    InitializeClipAnatomicalSanitization();
                phaseOffset = FindLeftForwardContactPhase();
                if (clipMuscleDeltaRetarget)
                    InitializeClipMuscleDeltaRetarget();
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

        /// <summary>
        /// Ankle-to-toe direction in host-local space, both feet averaged, y removed. Returns zero
        /// when the rig has no toe bones, which the caller must treat as "unknown" rather than
        /// "forward is +Z".
        /// </summary>
        private Vector3 MeasureToeForwardLocal()
        {
            var sum = Vector3.zero;
            var count = 0;
            if (leftToes != null && leftFoot != null)
            {
                sum += transform.InverseTransformPoint(leftToes.position) -
                       transform.InverseTransformPoint(leftFoot.position);
                count++;
            }
            if (rightToes != null && rightFoot != null)
            {
                sum += transform.InverseTransformPoint(rightToes.position) -
                       transform.InverseTransformPoint(rightFoot.position);
                count++;
            }
            if (count == 0)
                return Vector3.zero;
            sum /= count;
            sum.y = 0f;
            return sum.sqrMagnitude < 1e-10f ? Vector3.zero : sum.normalized;
        }

        /// <summary>
        /// Hips-to-head direction in host-local space, normalized. Zero when the head is unmapped.
        /// </summary>
        private Vector3 MeasureTorsoUpLocal()
        {
            if (head == null || hips == null)
                return Vector3.zero;
            Vector3 v = transform.InverseTransformPoint(head.position) -
                        transform.InverseTransformPoint(hips.position);
            return v.sqrMagnitude < 1e-10f ? Vector3.zero : v.normalized;
        }

        public PoseSnapshot ReadPoseSnapshot()
        {
            Initialize();
            Vector3 leftLocal = transform.InverseTransformPoint(leftFoot.position);
            Vector3 rightLocal = transform.InverseTransformPoint(rightFoot.position);
            Vector3 hipsLocal = transform.InverseTransformPoint(hips.position);
            Vector3 toeForward = MeasureToeForwardLocal();
            return new PoseSnapshot
            {
                toeForwardLocal = toeForward,
                torsoUpLocal = MeasureTorsoUpLocal(),
                leftFootLocal = leftLocal,
                rightFootLocal = rightLocal,
                leftFootWorld = leftFoot.position,
                rightFootWorld = rightFoot.position,
                leftHandLocal = leftHand == null
                    ? Vector3.zero
                    : transform.InverseTransformPoint(leftHand.position),
                rightHandLocal = rightHand == null
                    ? Vector3.zero
                    : transform.InverseTransformPoint(rightHand.position),
                hipsLocal = hipsLocal,
                motionPhase01 = lastSampledPhase01,
                // The clean Father rig's authored forward is local -X (the runtime host carries
                // the verified +90 degree facing offset). Its local Z is lateral, so using Z here
                // would report leg spacing as stride and could not detect alternating steps.
                footLead = dedicatedNaturalSdWalk
                    ? leftLocal.x - rightLocal.x
                    : leftLocal.z - rightLocal.z,
                leftFootPlanted = leftFootContactLocked,
                rightFootPlanted = rightFootContactLocked,
                standingHeight = Mathf.Max(standingHeight, 0.0001f),
                visualRootPositionError = Vector3.Distance(visualRoot.localPosition, visualLocalPosition),
                visualRootRotationErrorDegrees = Quaternion.Angle(visualRoot.localRotation, visualLocalRotation)
            };
        }

        private void SamplePose(double sharedMotionClock, bool isMoving)
        {
            double cycleSeconds = ResolveCycleSeconds();
            double normalized = sharedMotionClock / cycleSeconds + phaseOffset;
            float phase = Mathf.Repeat((float)normalized, 1f);
            lastSampledPhase01 = phase;
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

                if (clipRetargetReady)
                    ApplyClipMuscleDeltaRetarget();

                if (clipAnatomicalSanitization)
                    ApplyClipAnatomicalSanitization(isMoving);

                if (poseStrength < 0.9999f)
                    ApplyPoseStrength();
            }

            // Humanoid clips may contain extracted root translation/rotation. The visual child is
            // reset after every sample; the bottom-centre host above it remains the sole root.
            visualRoot.localPosition = visualLocalPosition;
            visualRoot.localRotation = visualLocalRotation;
            // V35 telemetry exposed a 0.23-unit one-frame snap from the remaining impact IK. The
            // clean rig now uses only its continuous authored leg curves and records the anatomical
            // stance phase as telemetry. Bone coordinates around host turns are not a visual-slip
            // acceptance metric; no world-space solver is allowed to pull the mesh.
            if (dedicatedNaturalSdWalk && isMoving)
                UpdateNaturalSdFootContacts(Mathf.Repeat(phase - phaseOffset, 1f));
            else
                ResetFootPlants();
        }

        /// <summary>
        /// Captures this rig's rest muscles and the clip's own mean pose, once. The mean is taken
        /// over a whole cycle of the moving clip with the idle input muted, so it is the clip's own
        /// neutral rather than any particular frame of it.
        /// </summary>
        private void InitializeClipMuscleDeltaRetarget()
        {
            humanPoseHandler = new HumanPoseHandler(animator.avatar, animator.transform);
            restHumanPose = new HumanPose();
            humanPoseHandler.GetHumanPose(ref restHumanPose);
            if (restHumanPose.muscles == null ||
                restHumanPose.muscles.Length != HumanTrait.MuscleCount)
                throw new InvalidOperationException(
                    familyId + " could not capture a complete Humanoid rest pose for retargeting.");

            clipSampledPose = new HumanPose();
            clipRetargetOutputPose = new HumanPose
            {
                muscles = new float[HumanTrait.MuscleCount]
            };
            clipReferenceMuscles = new float[HumanTrait.MuscleCount];

            const int sampleCount = 120;
            if (idleClip != null)
            {
                clipMixer.SetInputWeight(0, 0f);
                clipMixer.SetInputWeight(1, 1f);
            }
            var positionSum = Vector3.zero;
            // Averaging quaternions by summing components is only valid when they are close and
            // consistently signed, which successive frames of one walk cycle are once each is
            // aligned to the first.
            Quaternion rotationSum = default;
            var first = true;
            for (var sample = 0; sample < sampleCount; sample++)
            {
                clipPlayable.SetTime(sample / (float)sampleCount * walkClip.length);
                graph.Evaluate(0f);
                humanPoseHandler.GetHumanPose(ref clipSampledPose);
                for (var muscle = 0; muscle < clipReferenceMuscles.Length; muscle++)
                    clipReferenceMuscles[muscle] += clipSampledPose.muscles[muscle];
                positionSum += clipSampledPose.bodyPosition;
                Quaternion rotation = clipSampledPose.bodyRotation;
                if (first)
                {
                    rotationSum = rotation;
                    first = false;
                }
                else
                {
                    if (Quaternion.Dot(rotationSum, rotation) < 0f)
                        rotation = new Quaternion(-rotation.x, -rotation.y, -rotation.z, -rotation.w);
                    rotationSum = new Quaternion(
                        rotationSum.x + rotation.x,
                        rotationSum.y + rotation.y,
                        rotationSum.z + rotation.z,
                        rotationSum.w + rotation.w);
                }
            }
            for (var muscle = 0; muscle < clipReferenceMuscles.Length; muscle++)
                clipReferenceMuscles[muscle] /= sampleCount;
            clipReferenceBodyPosition = positionSum / sampleCount;
            clipReferenceBodyRotation = Normalize(rotationSum);
            if (idleClip != null)
            {
                clipMixer.SetInputWeight(0, 1f);
                clipMixer.SetInputWeight(1, 0f);
            }
            clipRetargetReady = true;
        }

        private static Quaternion Normalize(Quaternion value)
        {
            float magnitude = Mathf.Sqrt(
                value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w);
            return magnitude < 0.000001f
                ? Quaternion.identity
                : new Quaternion(
                    value.x / magnitude,
                    value.y / magnitude,
                    value.z / magnitude,
                    value.w / magnitude);
        }

        /// <summary>
        /// Reads the pose the graph just produced, removes the clip's own neutral, and writes the
        /// remainder onto this rig's rest pose.
        /// </summary>
        private void ApplyClipMuscleDeltaRetarget()
        {
            humanPoseHandler.GetHumanPose(ref clipSampledPose);
            for (var muscle = 0; muscle < clipRetargetOutputPose.muscles.Length; muscle++)
                clipRetargetOutputPose.muscles[muscle] =
                    restHumanPose.muscles[muscle] +
                    (clipSampledPose.muscles[muscle] - clipReferenceMuscles[muscle]);
            clipRetargetOutputPose.bodyPosition =
                restHumanPose.bodyPosition +
                (clipSampledPose.bodyPosition - clipReferenceBodyPosition);
            clipRetargetOutputPose.bodyRotation =
                restHumanPose.bodyRotation *
                (Quaternion.Inverse(clipReferenceBodyRotation) * clipSampledPose.bodyRotation);
            humanPoseHandler.SetHumanPose(ref clipRetargetOutputPose);
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
        }

        /// <summary>
        /// Keeps the paid clip's complete front/back leg, knee, torso and opposite-arm curves at
        /// poseStrength=1 while mapping only their neutral baselines to this avatar. This is not a
        /// replacement or weakened gait: action 613 remains the sole time-varying motion source.
        /// </summary>
        private void InitializeClipAnatomicalSanitization()
        {
            if (clipMuscleDeltaRetarget)
                throw new InvalidOperationException(
                    familyId + " cannot combine full-muscle delta retargeting with anatomical sanitation.");
            humanPoseHandler = new HumanPoseHandler(animator.avatar, animator.transform);
            restHumanPose = new HumanPose();
            humanPoseHandler.GetHumanPose(ref restHumanPose);
            if (restHumanPose.muscles == null ||
                restHumanPose.muscles.Length != HumanTrait.MuscleCount)
                throw new InvalidOperationException(
                    familyId + " could not capture a complete Humanoid rest pose for sanitation.");
            sampledHumanPose = new HumanPose
            {
                muscles = new float[HumanTrait.MuscleCount]
            };
            leftUpperLegFrontBack = FindMuscle("Left Upper Leg Front-Back");
            rightUpperLegFrontBack = FindMuscle("Right Upper Leg Front-Back");
            leftLowerLegStretch = FindMuscle("Left Lower Leg Stretch");
            rightLowerLegStretch = FindMuscle("Right Lower Leg Stretch");
            leftUpperLegInOut = FindMuscle("Left Upper Leg In-Out");
            rightUpperLegInOut = FindMuscle("Right Upper Leg In-Out");
            leftUpperLegTwistInOut = FindMuscle("Left Upper Leg Twist In-Out");
            rightUpperLegTwistInOut = FindMuscle("Right Upper Leg Twist In-Out");
            leftLowerLegTwistInOut = FindMuscle("Left Lower Leg Twist In-Out");
            rightLowerLegTwistInOut = FindMuscle("Right Lower Leg Twist In-Out");
            leftFootTwistInOut = FindMuscle("Left Foot Twist In-Out");
            rightFootTwistInOut = FindMuscle("Right Foot Twist In-Out");
            leftArmDownUp = FindMuscle("Left Arm Down-Up");
            rightArmDownUp = FindMuscle("Right Arm Down-Up");
            leftArmFrontBack = FindMuscle("Left Arm Front-Back");
            rightArmFrontBack = FindMuscle("Right Arm Front-Back");
            leftArmTwistInOut = FindMuscle("Left Arm Twist In-Out");
            rightArmTwistInOut = FindMuscle("Right Arm Twist In-Out");
            leftForearmTwistInOut = FindMuscle("Left Forearm Twist In-Out");
            rightForearmTwistInOut = FindMuscle("Right Forearm Twist In-Out");
            leftForearmStretch = FindMuscle("Left Forearm Stretch");
            rightForearmStretch = FindMuscle("Right Forearm Stretch");
            spineFrontBack = FindMuscle("Spine Front-Back");
            chestFrontBack = FindMuscle("Chest Front-Back");
            upperChestFrontBack = FindMuscle("UpperChest Front-Back");
            neckNodDownUp = FindMuscle("Neck Nod Down-Up");
            headNodDownUp = FindMuscle("Head Nod Down-Up");

            clipSanitizationReferenceMuscles = new float[HumanTrait.MuscleCount];
            const int sampleCount = 120;
            if (idleClip != null)
            {
                clipMixer.SetInputWeight(0, 0f);
                clipMixer.SetInputWeight(1, 1f);
            }
            var referencePose = new HumanPose();
            for (var sample = 0; sample < sampleCount; sample++)
            {
                clipPlayable.SetTime(sample / (float)sampleCount * walkClip.length);
                graph.Evaluate(0f);
                humanPoseHandler.GetHumanPose(ref referencePose);
                for (var muscle = 0; muscle < clipSanitizationReferenceMuscles.Length; muscle++)
                    clipSanitizationReferenceMuscles[muscle] += referencePose.muscles[muscle];
            }
            for (var muscle = 0; muscle < clipSanitizationReferenceMuscles.Length; muscle++)
                clipSanitizationReferenceMuscles[muscle] /= sampleCount;
            CaptureApprovedRigidArmPose();
            // Calibrate the fixed target-avatar walking plane from a full-weight action frame,
            // never from the idle/walk transition. Subsequent samples are projected only in the
            // lateral dimension of this same plane.
            clipPlayable.SetTime(0d);
            graph.Evaluate(0f);
            ApplyClipAnatomicalSanitization(true);
            if (idleClip != null)
            {
                clipMixer.SetInputWeight(0, 1f);
                clipMixer.SetInputWeight(1, 0f);
            }
        }

        private void ApplyClipAnatomicalSanitization(bool isMoving)
        {
            humanPoseHandler.GetHumanPose(ref sampledHumanPose);
            // Action 613 is labelled in-place but still carries source-avatar ground drift. Keep
            // its authored vertical weight shift and pelvis rotation, but let the office route own
            // ground translation. Replacing bodyRotation with the target rest rotation is invalid:
            // the source and target Humanoid body frames differ by roughly 90 degrees.
            sampledHumanPose.bodyPosition = new Vector3(
                restHumanPose.bodyPosition.x,
                sampledHumanPose.bodyPosition.y,
                restHumanPose.bodyPosition.z);
            if (isMoving)
            {
                // The source and target avatars disagree on their lateral leg axes. Preserve
                // action 613's front/back and knee timing, but remove every lateral/twist channel
                // that can turn one thigh, shin or shoe into the rejected third-leg fan.
                CopyRestMuscle(leftUpperLegInOut);
                CopyRestMuscle(rightUpperLegInOut);
                CopyRestMuscle(leftUpperLegTwistInOut);
                CopyRestMuscle(rightUpperLegTwistInOut);
                CopyRestMuscle(leftLowerLegTwistInOut);
                CopyRestMuscle(rightLowerLegTwistInOut);
                CopyRestMuscle(leftFootTwistInOut);
                CopyRestMuscle(rightFootTwistInOut);

                // V66 was rejected because it replaced Claude's action 613 with a handmade gait.
                // Keep every authored sagittal hip and knee delta at full amplitude. Subtracting
                // the source mean only maps the clip onto this rig's bind baseline; it does not
                // synthesize, attenuate or reshape the walk cycle.
                RetargetClipDeltaToRest(leftUpperLegFrontBack, 1f);
                RetargetClipDeltaToRest(rightUpperLegFrontBack, 1f);
                RetargetClipDeltaToRest(leftLowerLegStretch, 1f);
                RetargetClipDeltaToRest(rightLowerLegStretch, 1f);
            }

            // The source clip curls its taller avatar through the chest and then compensates with
            // the neck. On this large-headed SD body that reads as a hunch even when the
            // hips-to-head vector is numerically vertical. Keep pelvis weight shift and body
            // rotation, but take the forward/back spine and gaze baselines from the approved rest
            // appearance.
            RetargetClipDeltaToRest(spineFrontBack, 1f);
            RetargetClipDeltaToRest(chestFrontBack, 1f);
            RetargetClipDeltaToRest(upperChestFrontBack, 1f);
            RetargetClipDeltaToRest(neckNodDownUp, 1f);
            RetargetClipDeltaToRest(headNodDownUp, 1f);

            humanPoseHandler.SetHumanPose(ref sampledHumanPose);
            // The source Humanoid arm curves deform each short SD segment independently, which
            // makes the elbow, wrist and fingers look rubbery even at reduced amplitude. Restore
            // one stable hanging hierarchy. V72 then rotated it with a measured toe axis and tucked
            // both arms, but the user correctly saw that as arms pinned behind the body. V74 keeps
            // V72's lower-body/torso sanitation byte-for-byte and changes only this final arm step.
            // No elbow, wrist, finger, outward or tuck correction runs: only both upper-arm roots
            // rotate oppositely around the clean rig's verified fixed body-side axis.
            RestoreApprovedRigidArmPose();
            if (clipStableBodySideArms)
                ApplyStableBodySideArmSwing(lastSampledPhase01, isMoving);
            else
                ApplyApprovedRigidArmSwing(lastSampledPhase01, isMoving);
            if (isMoving)
            {
                if (!clipFootPlaneReady)
                    CaptureClipFootPlane();
                RemoveResidualFootLateral(
                    leftUpperLeg,
                    leftFoot,
                    leftFootPlaneLateral);
                RemoveResidualFootLateral(
                    rightUpperLeg,
                    rightFoot,
                    rightFootPlaneLateral);
            }
            ApplyClipUprightCalibration();
        }

        private void CaptureClipFootPlane()
        {
            clipSagittalForwardLocal = MeasureToeForwardLocal();
            if (clipSagittalForwardLocal.sqrMagnitude <= 0.000001f)
                clipSagittalForwardLocal = Vector3.forward;
            clipSagittalForwardLocal.Normalize();
            Vector3 lateral =
                Vector3.Cross(Vector3.up, clipSagittalForwardLocal).normalized;
            Vector3 hipsLocal = visualRoot.InverseTransformPoint(hips.position);
            leftFootPlaneLateral = Vector3.Dot(
                visualRoot.InverseTransformPoint(leftFoot.position) - hipsLocal,
                lateral);
            rightFootPlaneLateral = Vector3.Dot(
                visualRoot.InverseTransformPoint(rightFoot.position) - hipsLocal,
                lateral);
            clipFootPlaneReady = true;
        }

        private void RemoveResidualFootLateral(
            Transform upperLeg,
            Transform foot,
            float targetFootLateral)
        {
            if (!clipFootPlaneReady)
                return;
            Vector3 lateral =
                Vector3.Cross(Vector3.up, clipSagittalForwardLocal).normalized;
            Vector3 hipsLocal = visualRoot.InverseTransformPoint(hips.position);
            Vector3 upperLegLocal = visualRoot.InverseTransformPoint(upperLeg.position);
            Vector3 footLocal = visualRoot.InverseTransformPoint(foot.position);
            Vector3 hipToFootLocal = footLocal - upperLegLocal;
            Vector3 planarLocal = hipToFootLocal -
                                  Vector3.Dot(hipToFootLocal, lateral) * lateral;
            if (planarLocal.sqrMagnitude <= 0.000001f)
                return;
            float upperLegLateral = Vector3.Dot(
                upperLegLocal - hipsLocal,
                lateral);
            float desiredBoneLateral = targetFootLateral - upperLegLateral;
            float planarLength = Mathf.Sqrt(Mathf.Max(
                0.000001f,
                hipToFootLocal.sqrMagnitude -
                desiredBoneLateral * desiredBoneLateral));
            Vector3 desiredLocal =
                planarLocal.normalized * planarLength +
                lateral * desiredBoneLateral;
            Vector3 currentWorld = visualRoot.TransformDirection(hipToFootLocal);
            Vector3 desiredWorld = visualRoot.TransformDirection(desiredLocal);
            Quaternion footWorldRotation = foot.rotation;
            upperLeg.rotation =
                Quaternion.FromToRotation(currentWorld, desiredWorld) * upperLeg.rotation;
            // Keep the authored shoe orientation and toe roll; only the leg swing plane moves.
            foot.rotation = footWorldRotation;
        }

        private void CopyRestMuscle(int index)
        {
            sampledHumanPose.muscles[index] = restHumanPose.muscles[index];
        }

        private void ScaleClipDelta(int index, float scale)
        {
            float mean = clipSanitizationReferenceMuscles[index];
            sampledHumanPose.muscles[index] = Mathf.Clamp(
                mean + (sampledHumanPose.muscles[index] - mean) * scale,
                -1f,
                1f);
        }

        private void RetargetClipDeltaToRest(int index, float scale)
        {
            RetargetClipDeltaToTarget(index, restHumanPose.muscles[index], scale);
        }

        private void RetargetClipDeltaToTarget(int index, float target, float scale)
        {
            float mean = clipSanitizationReferenceMuscles[index];
            sampledHumanPose.muscles[index] = Mathf.Clamp(
                target + (sampledHumanPose.muscles[index] - mean) * scale,
                -1f,
                1f);
        }

        private void CaptureApprovedRigidArmPose()
        {
            sampledHumanPose.bodyPosition = restHumanPose.bodyPosition;
            sampledHumanPose.bodyRotation = restHumanPose.bodyRotation;
            Array.Copy(
                restHumanPose.muscles,
                sampledHumanPose.muscles,
                restHumanPose.muscles.Length);
            sampledHumanPose.muscles[leftArmDownUp] = -0.98f;
            sampledHumanPose.muscles[rightArmDownUp] = -0.98f;
            CopyRestMuscle(leftArmFrontBack);
            CopyRestMuscle(rightArmFrontBack);
            CopyRestMuscle(leftArmTwistInOut);
            CopyRestMuscle(rightArmTwistInOut);
            CopyRestMuscle(leftForearmTwistInOut);
            CopyRestMuscle(rightForearmTwistInOut);
            CopyRestMuscle(leftForearmStretch);
            CopyRestMuscle(rightForearmStretch);
            humanPoseHandler.SetHumanPose(ref sampledHumanPose);
            RestoreBoneRest(leftShoulder);
            RestoreBoneRest(rightShoulder);

            Transform leftRoot = leftShoulder != null ? leftShoulder : leftUpperArm;
            Transform rightRoot = rightShoulder != null ? rightShoulder : rightUpperArm;
            var result = new System.Collections.Generic.List<TransformRest>();
            for (var index = 0; index < restPose.Length; index++)
            {
                Transform current = restPose[index].Transform;
                if (current == null)
                    continue;
                bool isLeft = leftRoot != null &&
                              (current == leftRoot || current.IsChildOf(leftRoot));
                bool isRight = rightRoot != null &&
                               (current == rightRoot || current.IsChildOf(rightRoot));
                if (!isLeft && !isRight)
                    continue;
                result.Add(new TransformRest(
                    current,
                    current.localPosition,
                    current.localRotation,
                    current.localScale));
            }
            approvedRigidArmPose = result.ToArray();
        }

        private void RestoreApprovedRigidArmPose()
        {
            for (var index = 0; index < approvedRigidArmPose.Length; index++)
            {
                TransformRest rest = approvedRigidArmPose[index];
                if (rest.Transform == null)
                    continue;
                rest.Transform.localPosition = rest.LocalPosition;
                rest.Transform.localRotation = rest.LocalRotation;
                rest.Transform.localScale = rest.LocalScale;
            }
        }

        private void ApplyApprovedRigidArmSwing(float phase, bool isMoving)
        {
            if (!isMoving)
                return;
            Vector3 bodyForward = visualRoot.TransformDirection(clipSagittalForwardLocal);
            bodyForward.y = 0f;
            if (bodyForward.sqrMagnitude <= 0.000001f)
                return;
            bodyForward.Normalize();
            Vector3 bodySide = Vector3.Cross(Vector3.up, bodyForward).normalized;
            RotateRigidArmTowardBody(leftUpperArm, leftHand, bodyForward, bodySide);
            RotateRigidArmTowardBody(rightUpperArm, rightHand, bodyForward, bodySide);
            const float maximumSwingDegrees = 2f;
            float swing = maximumSwingDegrees * Mathf.Cos(phase * Mathf.PI * 2f);
            RotateBoneAroundWorldAxis(leftUpperArm, bodySide, -swing);
            RotateBoneAroundWorldAxis(rightUpperArm, bodySide, swing);
        }

        private void ApplyStableBodySideArmSwing(float phase, bool isMoving)
        {
            if (!isMoving)
                return;
            ResolveNaturalSdBodyAxes(out _, out Vector3 bodySide);
            const float maximumSwingDegrees = 6f;
            float swing = maximumSwingDegrees * Mathf.Cos(phase * Mathf.PI * 2f);
            RotateBoneAroundWorldAxis(leftUpperArm, bodySide, -swing);
            RotateBoneAroundWorldAxis(rightUpperArm, bodySide, swing);
        }

        private static void RotateRigidArmTowardBody(
            Transform upperArm,
            Transform hand,
            Vector3 bodyForward,
            Vector3 bodySide)
        {
            if (upperArm == null || hand == null)
                return;
            Vector3 current = hand.position - upperArm.position;
            if (current.sqrMagnitude <= 0.0000001f)
                return;
            const float tuckDegrees = 4f;
            Vector3 plus = Quaternion.AngleAxis(tuckDegrees, bodyForward) * current;
            Vector3 minus = Quaternion.AngleAxis(-tuckDegrees, bodyForward) * current;
            Vector3 desired = Mathf.Abs(Vector3.Dot(plus, bodySide)) <
                              Mathf.Abs(Vector3.Dot(minus, bodySide))
                ? plus
                : minus;
            upperArm.rotation = Quaternion.FromToRotation(current, desired) * upperArm.rotation;
        }

        private void LimitClipKneeExtension(int index)
        {
            sampledHumanPose.muscles[index] = Mathf.Min(
                sampledHumanPose.muscles[index],
                restHumanPose.muscles[index] - 0.075f);
        }

        private void ApplyClipUprightCalibration()
        {
            if (spine == null || head == null || hips == null)
                return;
            Quaternion headWorldRotation = head.rotation;
            // V61 stopped correcting at two degrees regardless of sign. Its measured signed lean
            // was -1.45 degrees on average (negative is backward), which is small numerically but
            // unmistakably zombie-like on this short, large-headed body. Aim one degree into the
            // anatomical travel direction instead of accepting either forward or backward lean.
            const float targetForwardLeanDegrees = 1f;
            Vector3 forwardWorld = visualRoot.TransformDirection(clipSagittalForwardLocal);
            forwardWorld.y = 0f;
            if (forwardWorld.sqrMagnitude <= 0.000001f)
                forwardWorld = visualRoot.forward;
            forwardWorld.Normalize();
            float targetRadians = targetForwardLeanDegrees * Mathf.Deg2Rad;
            Vector3 targetTorso =
                Vector3.up * Mathf.Cos(targetRadians) +
                forwardWorld * Mathf.Sin(targetRadians);
            for (var iteration = 0; iteration < 3; iteration++)
            {
                Vector3 torso = head.position - hips.position;
                if (torso.sqrMagnitude <= 0.0000001f)
                    break;
                float error = Vector3.Angle(torso, targetTorso);
                if (error <= 0.05f)
                    break;
                Vector3 axis = Vector3.Cross(torso, targetTorso);
                if (axis.sqrMagnitude <= 0.0000001f)
                    break;
                spine.rotation = Quaternion.AngleAxis(
                    error,
                    axis.normalized) * spine.rotation;
            }
            // Moving the upper-body mass must not make the face stare at the floor or ceiling.
            head.rotation = headWorldRotation;
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

            // The clean V4 bind pose is a T-pose. Establish the same compact arms-at-sides base
            // on every sample before adding a small counter-swing. Restoring the upper-arm bone
            // transforms after SetHumanPose would put the T-pose back and visibly detach both
            // sleeves/hands from the torso, as the rejected V64 diagnostic demonstrated.
            sampledHumanPose.muscles[leftArmDownUp] = -0.95f;
            sampledHumanPose.muscles[rightArmDownUp] = -0.95f;

            humanPoseHandler.SetHumanPose(ref sampledHumanPose);
            RestoreApprovedUpperBodyRest();
            ApplyNaturalSdUprightPosture();
            ApplyNaturalSdArms(phase, isMoving);
            // Keep the pelvis centred: the rejected G candidate's lateral shift looked rubbery.
            // A small, always-upward double-step rise gives the SD body readable weight without
            // pulling either planted leg below the floor.
            float bodyRise = isMoving
                ? 0.014f * (0.5f - 0.5f * Mathf.Cos(phase * Mathf.PI * 4f))
                : 0f;
            hips.localPosition = hipsRestLocalPosition + Vector3.up * bodyRise;
        }

        private void ApplyNaturalSdLeg(
            float legPhase,
            int upperLegFrontBack,
            int lowerLegStretch)
        {
            float lead = EvaluateStylizedStepLead(legPhase);
            float swingBend = EvaluateStylizedSwingKnee(legPhase);

            AddMuscle(upperLegFrontBack, 0.21f * lead);
            AddMuscle(lowerLegStretch, -0.46f * swingBend);
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

        private void UpdateNaturalSdFootContacts(float phase)
        {
            leftFootContactLocked = IsNaturalSdContactPhase(Mathf.Repeat(phase, 1f));
            rightFootContactLocked = IsNaturalSdContactPhase(Mathf.Repeat(phase + 0.5f, 1f));
            leftFootPlanted = leftFootContactLocked;
            rightFootPlanted = rightFootContactLocked;
        }

        private static bool IsNaturalSdContactPhase(float legPhase)
        {
            legPhase = Mathf.Repeat(legPhase, 1f);
            // Forward heel contact begins just before phase wrap; the foot then owns the ground
            // through the long backward stance sweep and releases when the knee recovery starts.
            return legPhase >= 0.95f || legPhase < 0.56f;
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

        private void ApplyNaturalSdUprightPosture()
        {
            if (spine == null || chest == null || naturalSdTorsoUprightDegrees <= 0.001f)
                return;

            ResolveNaturalSdBodyAxes(out Vector3 bodyForward, out Vector3 bodySide);
            Vector3 current = chest.position - spine.position;
            if (current.sqrMagnitude <= 0.0000001f)
                return;

            Vector3 plus = Quaternion.AngleAxis(
                naturalSdTorsoUprightDegrees,
                bodySide) * current;
            Vector3 minus = Quaternion.AngleAxis(
                -naturalSdTorsoUprightDegrees,
                bodySide) * current;
            Vector3 desired = Vector3.Dot(plus, bodyForward) < Vector3.Dot(minus, bodyForward)
                ? plus
                : minus;
            spine.rotation = Quaternion.FromToRotation(current, desired) * spine.rotation;

            // Keep the face level after moving the upper-body mass back over the hips. This makes
            // the short-neck SD silhouette read upright instead of turning the head with the lean.
            if (neck != null && head != null)
            {
                Vector3 neckToHead = head.position - neck.position;
                if (neckToHead.sqrMagnitude > 0.0000001f)
                    neck.rotation = Quaternion.FromToRotation(
                        neckToHead,
                        Vector3.up * neckToHead.magnitude) * neck.rotation;
            }
        }

        private void ApplyNaturalSdArms(float phase, bool isMoving)
        {
            ResolveNaturalSdBodyAxes(out Vector3 bodyForward, out Vector3 bodySide);
            RotateArmOutward(leftUpperArm, leftHand, bodyForward, bodySide);
            RotateArmOutward(rightUpperArm, rightHand, bodyForward, bodySide);

            float swing = isMoving
                ? naturalSdArmSwingDegrees * Mathf.Cos(phase * Mathf.PI * 2f)
                : 0f;
            RotateBoneAroundWorldAxis(leftUpperArm, bodySide, -swing);
            RotateBoneAroundWorldAxis(rightUpperArm, bodySide, swing);
            BendElbowForward(leftLowerArm, leftHand, bodyForward, bodySide);
            BendElbowForward(rightLowerArm, rightHand, bodyForward, bodySide);
        }

        private void RestoreBoneRest(Transform target)
        {
            if (target == null)
                return;
            for (var index = 0; index < restPose.Length; index++)
            {
                TransformRest rest = restPose[index];
                if (rest.Transform != target)
                    continue;
                target.localPosition = rest.LocalPosition;
                target.localRotation = rest.LocalRotation;
                target.localScale = rest.LocalScale;
                return;
            }
        }

        private void RotateArmOutward(
            Transform upperArm,
            Transform hand,
            Vector3 bodyForward,
            Vector3 bodySide)
        {
            if (upperArm == null || hand == null || naturalSdArmOutwardDegrees <= 0.001f)
                return;
            Vector3 current = hand.position - upperArm.position;
            if (current.sqrMagnitude <= 0.0000001f)
                return;
            float currentSide = Vector3.Dot(hand.position - hips.position, bodySide);
            float outwardSign = currentSide >= 0f ? 1f : -1f;
            Vector3 plus = Quaternion.AngleAxis(
                naturalSdArmOutwardDegrees,
                bodyForward) * current;
            Vector3 minus = Quaternion.AngleAxis(
                -naturalSdArmOutwardDegrees,
                bodyForward) * current;
            Vector3 desired = Vector3.Dot(plus, bodySide) * outwardSign >
                              Vector3.Dot(minus, bodySide) * outwardSign
                ? plus
                : minus;
            upperArm.rotation = Quaternion.FromToRotation(current, desired) * upperArm.rotation;
        }

        private void BendElbowForward(
            Transform lowerArm,
            Transform hand,
            Vector3 bodyForward,
            Vector3 bodySide)
        {
            if (lowerArm == null || hand == null || naturalSdElbowBendDegrees <= 0.001f)
                return;
            Vector3 current = hand.position - lowerArm.position;
            if (current.sqrMagnitude <= 0.0000001f)
                return;
            Vector3 plus = Quaternion.AngleAxis(
                naturalSdElbowBendDegrees,
                bodySide) * current;
            Vector3 minus = Quaternion.AngleAxis(
                -naturalSdElbowBendDegrees,
                bodySide) * current;
            Vector3 desired = Vector3.Dot(plus, bodyForward) > Vector3.Dot(minus, bodyForward)
                ? plus
                : minus;
            lowerArm.rotation = Quaternion.FromToRotation(current, desired) * lowerArm.rotation;
        }

        private static void RotateBoneAroundWorldAxis(
            Transform bone,
            Vector3 axis,
            float degrees)
        {
            if (bone == null || Mathf.Abs(degrees) <= 0.001f)
                return;
            bone.rotation = Quaternion.AngleAxis(degrees, axis) * bone.rotation;
        }

        private void ResolveNaturalSdBodyAxes(
            out Vector3 bodyForward,
            out Vector3 bodySide)
        {
            // The candidate comparison and yaw sweep proved that this rig's authored forward is
            // local -X, not Unity local +Z. The host therefore carries a +90-degree yaw offset.
            // Derive posture and arm axes from the same measured contract; using transform.forward
            // here bends the torso sideways and swings the hands out of the walking plane.
            bodyForward = -transform.right;
            bodySide = transform.forward;
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
            public Vector3 leftHandLocal;
            public Vector3 rightHandLocal;
            public Vector3 hipsLocal;

            /// <summary>
            /// Host-local direction from ankle to toes, averaged over both feet and flattened to
            /// the ground plane. This is the rig's anatomical forward, and unlike the foot swing
            /// axis it is not symmetric, so it carries a sign. It is what
            /// Docs/FATHER_V18_FACING_OFFSET_METHOD.md solves the facing offset from.
            /// </summary>
            public Vector3 toeForwardLocal;

            /// <summary>
            /// Host-local hips-to-head direction. Its lean off vertical is the torso pitch, which
            /// is what a bad retarget reference shows up as.
            /// </summary>
            public Vector3 torsoUpLocal;
            public float motionPhase01;
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
