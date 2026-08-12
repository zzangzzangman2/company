using System;
using System.Collections.Generic;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime
{
    [Serializable]
    public sealed class OfficeLocomotionTransitionEntry
    {
        [SerializeField] private string memberId = string.Empty;
        [SerializeField] private Sprite[] clipDirectionPoseFrames = Array.Empty<Sprite>();
        [SerializeField] private string sourceSheetSha256 = string.Empty;

        public string MemberId => memberId;
        public IReadOnlyList<Sprite> ClipDirectionPoseFrames => clipDirectionPoseFrames;
        public string SourceSheetSha256 => sourceSheetSha256 ?? string.Empty;

        public static OfficeLocomotionTransitionEntry Create(
            string id,
            Sprite[] frames,
            string sourceSha256)
        {
            return new OfficeLocomotionTransitionEntry
            {
                memberId = id ?? string.Empty,
                clipDirectionPoseFrames = frames == null ? Array.Empty<Sprite>() : (Sprite[])frames.Clone(),
                sourceSheetSha256 = sourceSha256 ?? string.Empty
            };
        }

        internal Sprite[] CopyFrames() => (Sprite[])clipDirectionPoseFrames.Clone();

        internal void Validate()
        {
            if (string.IsNullOrWhiteSpace(memberId))
                throw new InvalidOperationException("Locomotion transition member id is empty.");
            if (clipDirectionPoseFrames == null ||
                clipDirectionPoseFrames.Length != OfficeLocomotionTransitionCatalog.FramesPerMember)
            {
                throw new InvalidOperationException(
                    $"Locomotion transitions for '{memberId}' require exactly " +
                    $"{OfficeLocomotionTransitionCatalog.FramesPerMember} sprites.");
            }
            for (var index = 0; index < clipDirectionPoseFrames.Length; index++)
            {
                Sprite sprite = clipDirectionPoseFrames[index];
                if (sprite == null)
                    throw new InvalidOperationException(
                        $"Locomotion transition '{memberId}' frame {index} is missing.");
                if (Mathf.Abs(sprite.rect.width - 256f) > 0.01f ||
                    Mathf.Abs(sprite.rect.height - 256f) > 0.01f)
                {
                    throw new InvalidOperationException(
                        $"Locomotion transition '{memberId}' frame {index} must be 256x256.");
                }
                if (Mathf.Abs(sprite.pixelsPerUnit - 180f) > 0.01f)
                    throw new InvalidOperationException(
                        $"Locomotion transition '{memberId}' frame {index} must use 180 PPU.");
                if (Mathf.Abs(sprite.pivot.x - 128f) > 0.01f ||
                    Mathf.Abs(sprite.pivot.y) > 0.01f)
                    throw new InvalidOperationException(
                        $"Locomotion transition '{memberId}' frame {index} must use bottom-centre pivot.");
            }
            if (!IsSha256(sourceSheetSha256))
                throw new InvalidOperationException(
                    $"Locomotion transitions for '{memberId}' have no valid source sheet SHA-256.");
        }

        private static bool IsSha256(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length != 64) return false;
            for (var index = 0; index < value.Length; index++)
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

    [CreateAssetMenu(
        fileName = "OfficeLocomotionTransitionCatalog",
        menuName = "Family Company/Office/Locomotion Transition Catalog")]
    public sealed class OfficeLocomotionTransitionCatalog : ScriptableObject
    {
        public const int DirectionCount = 8;
        public const int ClipCount = 4;
        public const int PosesPerDirection = 2;
        public const int FramesPerClip = DirectionCount * PosesPerDirection;
        public const int FramesPerMember = ClipCount * FramesPerClip;
        public const int ExpectedMemberCount = 4;
        public const int CurrentVersion = 1;
        public const string DefaultResourcePath =
            "HighMotion/OfficeLocomotionTransitionCatalog";

        [SerializeField] private int version = CurrentVersion;
        [SerializeField] private OfficeLocomotionTransitionEntry[] members =
            Array.Empty<OfficeLocomotionTransitionEntry>();

        public int Version => version;
        public IReadOnlyList<OfficeLocomotionTransitionEntry> Members => members;

        public static OfficeLocomotionTransitionCatalog LoadDefault() =>
            Resources.Load<OfficeLocomotionTransitionCatalog>(DefaultResourcePath);

        public void Configure(OfficeLocomotionTransitionEntry[] entries)
        {
            version = CurrentVersion;
            members = entries == null
                ? Array.Empty<OfficeLocomotionTransitionEntry>()
                : (OfficeLocomotionTransitionEntry[])entries.Clone();
            Validate();
        }

        public Sprite[] CopyFrames(string memberId)
        {
            foreach (OfficeLocomotionTransitionEntry entry in members)
            {
                if (entry != null &&
                    string.Equals(entry.MemberId, memberId, StringComparison.Ordinal))
                    return entry.CopyFrames();
            }
            throw new KeyNotFoundException(
                "Locomotion transition catalog has no family member: " + memberId);
        }

        public void Validate()
        {
            if (version != CurrentVersion)
                throw new InvalidOperationException(
                    $"Locomotion transition catalog version {version} is not supported.");
            if (members == null || members.Length != ExpectedMemberCount)
                throw new InvalidOperationException(
                    $"Locomotion transition catalog requires exactly {ExpectedMemberCount} family members.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (OfficeLocomotionTransitionEntry entry in members)
            {
                if (entry == null)
                    throw new InvalidOperationException("Locomotion transition catalog contains a null entry.");
                entry.Validate();
                if (!ids.Add(entry.MemberId))
                    throw new InvalidOperationException(
                        "Duplicate locomotion transition member: " + entry.MemberId);
            }
        }
    }
}
