using System;
using System.Collections.Generic;
using FamilyCompany.Presentation.Unity.OfficeSeating;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeGridView.Authoring
{
    [Serializable]
    public sealed class OfficeCharacterSeatPoseProfile
    {
        [SerializeField] private string memberId = string.Empty;
        [SerializeField] private int directionIndex;
        [SerializeField] private OfficeSeatingAnimationClip clip = OfficeSeatingAnimationClip.Work;
        [SerializeField] private int frameIndex;
        [SerializeField] private Vector2 pelvisAnchorPx;
        [SerializeField] private Vector2 deskInteractionAnchorPx;
        [SerializeField] private float uniformScale = 1f;
        [SerializeField] private float rotationDegrees;
        [SerializeField] private bool humanApproved;
        [SerializeField] private string sourceSpriteSha256 = string.Empty;

        public string MemberId => memberId;
        public int DirectionIndex => directionIndex;
        public OfficeSeatingAnimationClip Clip => clip;
        public int FrameIndex => frameIndex;
        public Vector2 PelvisAnchorPx => pelvisAnchorPx;
        public Vector2 DeskInteractionAnchorPx => deskInteractionAnchorPx;
        public Vector2 HandAnchorPx => deskInteractionAnchorPx;
        public float UniformScale => uniformScale;
        public float RotationDegrees => rotationDegrees;
        public bool HumanApproved => humanApproved;
        public string SourceSpriteSha256 => sourceSpriteSha256 ?? string.Empty;

        public static OfficeCharacterSeatPoseProfile Create(
            string memberId,
            int directionIndex,
            OfficeSeatingAnimationClip clip,
            int frameIndex,
            Vector2 pelvisAnchorPx,
            Vector2 deskInteractionAnchorPx,
            float uniformScale = 1f,
            float rotationDegrees = 0f,
            bool humanApproved = false,
            string sourceSpriteSha256 = "")
        {
            return new OfficeCharacterSeatPoseProfile
            {
                memberId = memberId ?? string.Empty,
                directionIndex = directionIndex,
                clip = clip,
                frameIndex = frameIndex,
                pelvisAnchorPx = pelvisAnchorPx,
                deskInteractionAnchorPx = deskInteractionAnchorPx,
                uniformScale = uniformScale,
                rotationDegrees = rotationDegrees,
                humanApproved = humanApproved,
                sourceSpriteSha256 = sourceSpriteSha256 ?? string.Empty
            };
        }

        public void ApplyCalibration(
            Vector2 newPelvisAnchorPx,
            Vector2 newHandAnchorPx,
            float newUniformScale,
            bool approved,
            string newSourceSpriteSha256)
        {
            pelvisAnchorPx = newPelvisAnchorPx;
            deskInteractionAnchorPx = newHandAnchorPx;
            uniformScale = newUniformScale;
            rotationDegrees = 0f;
            humanApproved = approved;
            sourceSpriteSha256 = newSourceSpriteSha256 ?? string.Empty;
            Validate(new Vector2(256f, 256f));
        }

        public Vector2 RenderedHandFromPelvisPx(float baseUniformScale)
        {
            Vector2 vector = (deskInteractionAnchorPx - pelvisAnchorPx) *
                             (baseUniformScale * uniformScale);
            return vector;
        }

        public void Validate(Vector2 canvasSizePx)
        {
            if (string.IsNullOrWhiteSpace(memberId))
            {
                throw new InvalidOperationException("Character seat pose member id is empty.");
            }

            if (directionIndex < 0)
            {
                throw new InvalidOperationException($"Character seat pose '{memberId}' has invalid direction {directionIndex}.");
            }

            if (!Enum.IsDefined(typeof(OfficeSeatingAnimationClip), clip))
                throw new InvalidOperationException($"Character seat pose '{memberId}' has invalid clip {clip}.");
            int frameCount = OfficeSeatingAnimationFrames.FrameCount(clip);
            if (frameIndex < 0 || frameIndex >= frameCount)
                throw new InvalidOperationException($"Character seat pose '{memberId}/{clip}' has invalid frame {frameIndex}.");
            if (float.IsNaN(uniformScale) || float.IsInfinity(uniformScale) ||
                uniformScale < 0.97f || uniformScale > 1.03f)
                throw new InvalidOperationException(
                    $"Character seat pose '{memberId}/{clip}/{frameIndex}' scale {uniformScale} is outside 0.97..1.03.");
            if (float.IsNaN(rotationDegrees) || float.IsInfinity(rotationDegrees) ||
                Mathf.Abs(rotationDegrees) > 0.01f)
                throw new InvalidOperationException(
                    $"Character seat pose '{memberId}/{clip}/{frameIndex}' rotation {rotationDegrees} must be zero.");
            if (humanApproved && !IsSha256(sourceSpriteSha256))
                throw new InvalidOperationException(
                    $"Character seat pose '{memberId}/{clip}/{frameIndex}' approval has no valid source Sprite SHA-256.");

            ValidateAnchor(pelvisAnchorPx, nameof(pelvisAnchorPx), canvasSizePx);
            ValidateAnchor(deskInteractionAnchorPx, nameof(deskInteractionAnchorPx), canvasSizePx);
        }

        private void ValidateAnchor(Vector2 anchor, string anchorName, Vector2 canvasSizePx)
        {
            if (anchor.x < 0f || anchor.y < 0f || anchor.x > canvasSizePx.x || anchor.y > canvasSizePx.y)
            {
                throw new InvalidOperationException(
                    $"Character seat pose '{memberId}/{directionIndex}' {anchorName} {anchor} is outside {canvasSizePx}.");
            }
        }

        private static bool IsSha256(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length != 64) return false;
            for (int index = 0; index < value.Length; index++)
            {
                char current = value[index];
                bool hexadecimal = current >= '0' && current <= '9' ||
                                   current >= 'a' && current <= 'f' ||
                                   current >= 'A' && current <= 'F';
                if (!hexadecimal) return false;
            }
            return true;
        }
    }

    [CreateAssetMenu(menuName = "Family Company/Office/Character Seat Pose Catalog")]
    public sealed class OfficeCharacterSeatPoseCatalog : ScriptableObject
    {
        public const int CurrentCalibrationVersion = 4;
        private static readonly Vector2 PoseCanvasSizePx = new Vector2(256f, 256f);

        [SerializeField] private int calibrationVersion;
        [SerializeField] private OfficeCharacterSeatPoseProfile[] profiles = Array.Empty<OfficeCharacterSeatPoseProfile>();

        public int CalibrationVersion => calibrationVersion;
        public IReadOnlyList<OfficeCharacterSeatPoseProfile> Profiles => profiles;

        public OfficeCharacterSeatPoseProfile Resolve(string memberId, int directionIndex)
        {
            return Resolve(memberId, directionIndex, OfficeSeatingAnimationClip.Work, 0);
        }

        public OfficeCharacterSeatPoseProfile Resolve(
            string memberId,
            int directionIndex,
            OfficeSeatingAnimationClip clip,
            int frameIndex)
        {
            foreach (OfficeCharacterSeatPoseProfile profile in profiles)
            {
                if (profile != null &&
                    string.Equals(profile.MemberId, memberId, StringComparison.Ordinal) &&
                    profile.DirectionIndex == directionIndex &&
                    profile.Clip == clip &&
                    profile.FrameIndex == frameIndex)
                {
                    return profile;
                }
            }

            throw new KeyNotFoundException($"Character seat pose '{memberId}/{directionIndex}/{clip}/{frameIndex}' is not registered.");
        }

        public OfficeCharacterSeatPoseProfile ResolveApproved(
            string memberId,
            int directionIndex,
            OfficeSeatingAnimationClip clip,
            int frameIndex)
        {
            OfficeCharacterSeatPoseProfile result = Resolve(memberId, directionIndex, clip, frameIndex);
            if (!result.HumanApproved)
                throw new InvalidOperationException(
                    $"Character seat pose '{memberId}/{directionIndex}/{clip}/{frameIndex}' is not human-approved.");
            return result;
        }

        public void ValidateSafeStaticWork(IEnumerable<string> memberIds, int directionIndex)
        {
            if (memberIds == null) throw new ArgumentNullException(nameof(memberIds));
            Validate();
            foreach (string memberId in memberIds)
            {
                OfficeCharacterSeatPoseProfile profile = ResolveApproved(
                    memberId,
                    directionIndex,
                    OfficeSeatingAnimationClip.Work,
                    0);
                profile.Validate(PoseCanvasSizePx);
            }
        }

        public void ReplaceProfiles(OfficeCharacterSeatPoseProfile[] values, int newCalibrationVersion)
        {
            profiles = values ?? Array.Empty<OfficeCharacterSeatPoseProfile>();
            calibrationVersion = newCalibrationVersion;
            Validate();
        }

        public void Validate()
        {
            if (calibrationVersion != CurrentCalibrationVersion)
                throw new InvalidOperationException($"Character pose calibration version {calibrationVersion} is not supported.");
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (OfficeCharacterSeatPoseProfile profile in profiles)
            {
                if (profile == null)
                {
                    throw new InvalidOperationException("Character seat pose catalog contains a null profile.");
                }

                profile.Validate(PoseCanvasSizePx);
                string key = $"{profile.MemberId}:{profile.DirectionIndex}:{(int)profile.Clip}:{profile.FrameIndex}";
                if (!keys.Add(key))
                {
                    throw new InvalidOperationException($"Duplicate character seat pose profile '{key}'.");
                }
            }
        }
    }
}
