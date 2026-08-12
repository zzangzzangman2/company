using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime
{
    [CreateAssetMenu(
        fileName = "HighMotionDirectionManifest",
        menuName = "Family Company/Art/High Motion Direction Manifest")]
    public sealed class HighMotionDirectionManifest : ScriptableObject
    {
        public const int DirectionCount = 8;
        public const int WalkFrameCount = 6;
        public const int FrameApprovalCount = DirectionCount * WalkFrameCount;
        public const string DefaultResourcePath = "HighMotion/HighMotionDirectionManifest";

        [Serializable]
        public sealed class CharacterDirectionEntry
        {
            [SerializeField] private string memberId = string.Empty;
            [SerializeField] private int[] sourceDirectionForCanonical =
                { 0, 1, 2, 3, 4, 5, 6, 7 };
            [SerializeField] private bool[] visualApproval = new bool[DirectionCount];
            [SerializeField] private bool[] frameVisualApproval = new bool[FrameApprovalCount];

            public string MemberId => memberId;
            public IReadOnlyList<int> SourceDirectionForCanonical => sourceDirectionForCanonical;
            public IReadOnlyList<bool> VisualApproval => visualApproval;
            public IReadOnlyList<bool> FrameVisualApproval => frameVisualApproval;

            internal static CharacterDirectionEntry Identity(string id) => new CharacterDirectionEntry
            {
                memberId = id,
                sourceDirectionForCanonical = Enumerable.Range(0, DirectionCount).ToArray(),
                visualApproval = new bool[DirectionCount],
                frameVisualApproval = new bool[FrameApprovalCount]
            };

            internal void SetVisualApproval(int canonicalDirection, bool approved)
            {
                if (canonicalDirection < 0 || canonicalDirection >= DirectionCount)
                    throw new ArgumentOutOfRangeException(nameof(canonicalDirection));
                EnsureArrays();
                visualApproval[canonicalDirection] = approved;
            }

            internal void SetFrameVisualApproval(int canonicalDirection, int phase, bool approved)
            {
                if (canonicalDirection < 0 || canonicalDirection >= DirectionCount)
                    throw new ArgumentOutOfRangeException(nameof(canonicalDirection));
                if (phase < 0 || phase >= WalkFrameCount)
                    throw new ArgumentOutOfRangeException(nameof(phase));
                EnsureArrays();
                frameVisualApproval[phase * DirectionCount + canonicalDirection] = approved;
            }

            internal int Resolve(int canonicalDirection)
            {
                EnsureArrays();
                return sourceDirectionForCanonical[canonicalDirection];
            }

            internal void Validate()
            {
                if (string.IsNullOrWhiteSpace(memberId))
                    throw new InvalidOperationException("Direction manifest member ID is missing.");
                EnsureArrays();
                if (sourceDirectionForCanonical.Distinct().Count() != DirectionCount ||
                    sourceDirectionForCanonical.Any(value => value < 0 || value >= DirectionCount))
                    throw new InvalidOperationException(
                        $"Direction manifest for {memberId} must be a permutation of 0..7.");
            }

            private void EnsureArrays()
            {
                if (sourceDirectionForCanonical == null || sourceDirectionForCanonical.Length != DirectionCount)
                    throw new InvalidOperationException(
                        $"Direction manifest for {memberId} must contain eight source rows.");
                if (visualApproval == null || visualApproval.Length != DirectionCount)
                    visualApproval = new bool[DirectionCount];
                if (frameVisualApproval == null || frameVisualApproval.Length != FrameApprovalCount)
                    frameVisualApproval = new bool[FrameApprovalCount];
            }
        }

        [SerializeField] private int version = 1;
        [SerializeField] private List<CharacterDirectionEntry> characters =
            new List<CharacterDirectionEntry>();

        public int Version => version;
        public IReadOnlyList<CharacterDirectionEntry> Characters => characters;

        public static HighMotionDirectionManifest LoadDefault() =>
            Resources.Load<HighMotionDirectionManifest>(DefaultResourcePath);

        public int ResolveSourceDirection(string memberId, int canonicalDirection)
        {
            if (canonicalDirection < 0 || canonicalDirection >= DirectionCount)
                throw new ArgumentOutOfRangeException(nameof(canonicalDirection));
            CharacterDirectionEntry entry = characters.FirstOrDefault(item =>
                string.Equals(item.MemberId, memberId, StringComparison.Ordinal));
            if (entry == null)
                throw new InvalidOperationException(
                    "Direction manifest has no character entry: " + memberId);
            return entry.Resolve(canonicalDirection);
        }

        public bool SetVisualApproval(string memberId, int canonicalDirection, bool approved)
        {
            CharacterDirectionEntry entry = characters.FirstOrDefault(item =>
                string.Equals(item.MemberId, memberId, StringComparison.Ordinal));
            if (entry == null) return false;
            entry.SetVisualApproval(canonicalDirection, approved);
            return true;
        }

        public bool SetFrameVisualApproval(
            string memberId,
            int canonicalDirection,
            int phase,
            bool approved)
        {
            CharacterDirectionEntry entry = characters.FirstOrDefault(item =>
                string.Equals(item.MemberId, memberId, StringComparison.Ordinal));
            if (entry == null) return false;
            entry.SetFrameVisualApproval(canonicalDirection, phase, approved);
            return true;
        }

        public void ConfigureIdentity(IEnumerable<string> memberIds)
        {
            if (memberIds == null) throw new ArgumentNullException(nameof(memberIds));
            version = 2;
            characters = memberIds
                .Select(value => (value ?? string.Empty).Trim())
                .Where(value => value.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .Select(CharacterDirectionEntry.Identity)
                .ToList();
            Validate();
        }

        public void Validate()
        {
            if (version <= 0) throw new InvalidOperationException("Direction manifest version is invalid.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (CharacterDirectionEntry entry in characters)
            {
                entry.Validate();
                if (!ids.Add(entry.MemberId))
                    throw new InvalidOperationException(
                        "Direction manifest contains a duplicate member: " + entry.MemberId);
            }
        }
    }
}
