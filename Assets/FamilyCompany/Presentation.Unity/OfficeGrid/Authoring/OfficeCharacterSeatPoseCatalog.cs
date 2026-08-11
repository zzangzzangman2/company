using System;
using System.Collections.Generic;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeGridView.Authoring
{
    [Serializable]
    public sealed class OfficeCharacterSeatPoseProfile
    {
        [SerializeField] private string memberId = string.Empty;
        [SerializeField] private int directionIndex;
        [SerializeField] private Vector2 pelvisAnchorPx;
        [SerializeField] private Vector2 deskInteractionAnchorPx;

        public string MemberId => memberId;
        public int DirectionIndex => directionIndex;
        public Vector2 PelvisAnchorPx => pelvisAnchorPx;
        public Vector2 DeskInteractionAnchorPx => deskInteractionAnchorPx;

        public static OfficeCharacterSeatPoseProfile Create(
            string memberId,
            int directionIndex,
            Vector2 pelvisAnchorPx,
            Vector2 deskInteractionAnchorPx)
        {
            return new OfficeCharacterSeatPoseProfile
            {
                memberId = memberId ?? string.Empty,
                directionIndex = directionIndex,
                pelvisAnchorPx = pelvisAnchorPx,
                deskInteractionAnchorPx = deskInteractionAnchorPx
            };
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
        private static readonly Vector2 PoseCanvasSizePx = new Vector2(256f, 256f);

        [SerializeField] private OfficeCharacterSeatPoseProfile[] profiles = Array.Empty<OfficeCharacterSeatPoseProfile>();

        public IReadOnlyList<OfficeCharacterSeatPoseProfile> Profiles => profiles;

        public OfficeCharacterSeatPoseProfile Resolve(string memberId, int directionIndex)
        {
            foreach (OfficeCharacterSeatPoseProfile profile in profiles)
            {
                if (profile != null &&
                    string.Equals(profile.MemberId, memberId, StringComparison.Ordinal) &&
                    profile.DirectionIndex == directionIndex)
                {
                    return profile;
                }
            }

            throw new KeyNotFoundException($"Character seat pose '{memberId}/{directionIndex}' is not registered.");
        }

        public void ReplaceProfiles(OfficeCharacterSeatPoseProfile[] values)
        {
            profiles = values ?? Array.Empty<OfficeCharacterSeatPoseProfile>();
            Validate();
        }

        public void Validate()
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (OfficeCharacterSeatPoseProfile profile in profiles)
            {
                if (profile == null)
                {
                    throw new InvalidOperationException("Character seat pose catalog contains a null profile.");
                }

                profile.Validate(PoseCanvasSizePx);
                string key = $"{profile.MemberId}:{profile.DirectionIndex}";
                if (!keys.Add(key))
                {
                    throw new InvalidOperationException($"Duplicate character seat pose profile '{key}'.");
                }
            }
        }
    }
}
