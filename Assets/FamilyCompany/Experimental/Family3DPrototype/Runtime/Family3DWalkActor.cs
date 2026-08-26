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
            RestoreBoneRest(leftUpperArm);
            RestoreBoneRest(rightUpperArm);
            RestoreBoneRest(leftLowerArm);
            RestoreBoneRest(rightLowerArm);
            RestoreBoneRest(leftHand);
            RestoreBoneRest(rightHand);

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
