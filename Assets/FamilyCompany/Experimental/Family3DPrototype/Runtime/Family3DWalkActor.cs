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

        [SerializeField] private string familyId = string.Empty;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Animator animator;
        [SerializeField] private AnimationClip walkClip;
        [SerializeField] private Vector3 pathCenter;
        [SerializeField] private Color labelColor = Color.white;

        private PlayableGraph graph;
        private AnimationClipPlayable clipPlayable;
        private Transform leftFoot;
        private Transform rightFoot;
        private Transform hips;
        private Vector3 visualLocalPosition;
        private Quaternion visualLocalRotation;
        private float phaseOffset;
        private float standingHeight;
        private bool initialized;

        public string FamilyId => familyId;
        public Vector3 PathCenter => pathCenter;
        public Color LabelColor => labelColor;
        public AnimationClip WalkClip => walkClip;
        public float PhaseOffset => phaseOffset;
        public float StandingHeight => standingHeight;

        public void Configure(
            string id,
            Transform modelRoot,
            Animator modelAnimator,
            AnimationClip sharedWalkClip,
            Vector3 center,
            Color color)
        {
            familyId = id;
            visualRoot = modelRoot;
            animator = modelAnimator;
            walkClip = sharedWalkClip;
            pathCenter = center;
            labelColor = color;
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
            if (visualRoot == null || animator == null || walkClip == null)
                throw new InvalidOperationException(familyId + " is missing its visual root, Animator, or shared walk clip.");
            if (animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
                throw new InvalidOperationException(familyId + " does not have a valid Humanoid Avatar.");

            visualLocalPosition = visualRoot.localPosition;
            visualLocalRotation = visualRoot.localRotation;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.Rebind();

            graph = PlayableGraph.Create("Family3DWalk_" + familyId);
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            clipPlayable = AnimationClipPlayable.Create(graph, walkClip);
            clipPlayable.SetApplyFootIK(true);
            clipPlayable.SetApplyPlayableIK(true);
            AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Walk", animator);
            output.SetSourcePlayable(clipPlayable);
            graph.Play();

            leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (leftFoot == null || rightFoot == null || hips == null)
                throw new InvalidOperationException(familyId + " is missing Humanoid foot or hips mappings.");

            phaseOffset = FindLeftForwardContactPhase();
            SamplePose(0d);
            standingHeight = MeasureStandingHeight();
            initialized = true;
        }

        public void Tick(double sharedMotionClock, Vector3 worldPosition, Quaternion rootRotation)
        {
            Initialize();
            SamplePose(sharedMotionClock);

            // The outside root is the only owner of translation and direction. Retargeted root
            // curves are forcibly discarded so head, waist, and legs cannot turn independently.
            transform.SetPositionAndRotation(worldPosition, rootRotation);
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
                hipsLocal = hipsLocal,
                footLead = leftLocal.z - rightLocal.z,
                standingHeight = Mathf.Max(standingHeight, 0.0001f),
                visualRootPositionError = Vector3.Distance(visualRoot.localPosition, visualLocalPosition),
                visualRootRotationErrorDegrees = Quaternion.Angle(visualRoot.localRotation, visualLocalRotation)
            };
        }

        private void SamplePose(double sharedMotionClock)
        {
            double normalized = sharedMotionClock / LockedCycleSeconds + phaseOffset;
            double clipTime = Mathf.Repeat((float)normalized, 1f) * walkClip.length;
            clipPlayable.SetTime(clipTime);
            graph.Evaluate(0f);

            // Humanoid clips may contain extracted root translation/rotation. The visual child is
            // reset after every sample; the bottom-centre host above it remains the sole root.
            visualRoot.localPosition = visualLocalPosition;
            visualRoot.localRotation = visualLocalRotation;
        }

        private float FindLeftForwardContactPhase()
        {
            const int sampleCount = 180;
            float bestLead = float.NegativeInfinity;
            float bestPhase = 0f;
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
            initialized = false;
        }

        [Serializable]
        public struct PoseSnapshot
        {
            public Vector3 leftFootLocal;
            public Vector3 rightFootLocal;
            public Vector3 hipsLocal;
            public float footLead;
            public float standingHeight;
            public float visualRootPositionError;
            public float visualRootRotationErrorDegrees;
        }
    }
}
