using System;
using FamilyCompany.Presentation.Unity;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime
{
    [Serializable]
    public sealed class OfficeRuntimeCharacterArtEntry
    {
        [SerializeField] private string memberId = string.Empty;
        [SerializeField] private Sprite[] walkFrames = Array.Empty<Sprite>();

        public string MemberId => (memberId ?? string.Empty).Trim();

        public Sprite[] CopyWalkFrames()
        {
            if (walkFrames == null || walkFrames.Length != DirectionalSpriteAnimator.RequiredFrameCount)
                throw new InvalidOperationException(MemberId + " requires 48 walk frames.");
            return (Sprite[])walkFrames.Clone();
        }

        public void Configure(string id, Sprite[] frames)
        {
            memberId = (id ?? string.Empty).Trim();
            walkFrames = frames == null ? Array.Empty<Sprite>() : (Sprite[])frames.Clone();
        }
    }

    [CreateAssetMenu(menuName = "Family Company/Office/Runtime Character Art Catalog")]
    public sealed class OfficeRuntimeCharacterArtCatalog : ScriptableObject
    {
        public const string ResourcePath = "HighMotion/OfficeRuntimeCharacterArtCatalog";
        [SerializeField] private OfficeRuntimeCharacterArtEntry[] characters =
            Array.Empty<OfficeRuntimeCharacterArtEntry>();

        public static OfficeRuntimeCharacterArtCatalog LoadDefault() =>
            Resources.Load<OfficeRuntimeCharacterArtCatalog>(ResourcePath);

        public bool TryCopyWalkFrames(string memberId, out Sprite[] frames)
        {
            foreach (OfficeRuntimeCharacterArtEntry entry in characters)
            {
                if (entry != null && string.Equals(entry.MemberId, memberId, StringComparison.Ordinal))
                {
                    frames = entry.CopyWalkFrames();
                    return true;
                }
            }
            frames = Array.Empty<Sprite>();
            return false;
        }

        public void Configure(OfficeRuntimeCharacterArtEntry[] entries)
        {
            characters = entries ?? Array.Empty<OfficeRuntimeCharacterArtEntry>();
        }
    }
}
