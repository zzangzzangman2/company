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

        public string MemberId => memberId;
        public int DirectionIndex => directionIndex;
        public OfficeSeatingAnimationClip Clip => clip;
        public int FrameIndex => frameIndex;
        public Vector2 PelvisAnchorPx => pelvisAnchorPx;
        public Vector2 DeskInteractionAnchorPx => deskInteractionAnchorPx;
        public Vector2 HandAnchorPx => deskInteractionAnchorPx;
        public float UniformScale => uniformScale;
        public float RotationDegrees => rotationDegrees;

        public static OfficeCharacterSeatPoseProfile Create(
            string memberId,
            int directionIndex,
            OfficeSeatingAnimationClip clip,
            int frameIndex,
            Vector2 pelvisAnchorPx,
            Vector2 deskInteractionAnchorPx,
            float uniformScale = 1f,
            float rotationDegrees = 0f)
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
                rotationDegrees = rotationDegrees
            };
        }

        public void ApplyCalibration(
            Vector2 newPelvisAnchorPx,
            Vector2 newHandAnchorPx,
            float newUniformScale,
            float newRotationDegrees)
        {
            pelvisAnchorPx = newPelvisAnchorPx;
            deskInteractionAnchorPx = newHandAnchorPx;
            uniformScale = newUniformScale;
            rotationDegrees = newRotationDegrees;
            Validate(new Vector2(256f, 256f));
        }

        public Vector2 RenderedHandFromPelvisPx(float baseUniformScale)
        {
            Vector2 vector = (deskInteractionAnchorPx - pelvisAnchorPx) *
                             (baseUniformScale * uniformScale);
            float radians = rotationDegrees * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(radians);
            float sine = Mathf.Sin(radians);
            return new Vector2(
                vector.x * cosine - vector.y * sine,
                vector.x * sine + vector.y * cosine);
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
            if (uniformScale <= 0f || float.IsNaN(uniformScale) || float.IsInfinity(uniformScale))
                throw new InvalidOperationException($"Character seat pose '{memberId}/{clip}/{frameIndex}' has invalid scale {uniformScale}.");
            if (float.IsNaN(rotationDegrees) || float.IsInfinity(rotationDegrees) || Mathf.Abs(rotationDegrees) > 30f)
                throw new InvalidOperationException($"Character seat pose '{memberId}/{clip}/{frameIndex}' has invalid rotation {rotationDegrees}.");

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
    }

    [CreateAssetMenu(menuName = "Family Company/Office/Character Seat Pose Catalog")]
    public sealed class OfficeCharacterSeatPoseCatalog : ScriptableObject
    {
        public const int CurrentCalibrationVersion = 3;
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
