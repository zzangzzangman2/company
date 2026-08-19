using System;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity
{
    /// <summary>
    /// Four-direction visual fallback for diagonal movement. The canonical
    /// 4x2 sheet has no diagonal art, so diagonals reuse its exact west/east
    /// contacts instead of exposing unrelated generated legacy frames.
    /// </summary>
    public sealed class PlayerDiagonalContactPresenter : MonoBehaviour
    {
        private const string EastResourceRoot =
            "FamilyCompany/PlayerEastContactV1/Frames/player_east_contact_";
        private const string WestResourceRoot =
            "FamilyCompany/PlayerWestContactV1/Frames/player_west_contact_";

        private readonly Sprite[] _eastContacts = new Sprite[2];
        private readonly Sprite[] _westContacts = new Sprite[2];
        private SpriteRenderer _renderer;
        private bool _configured;

        public bool IsActive { get; private set; }
        public int ActiveFrameIndex { get; private set; } = -1;
        public int ActiveSourceDirection { get; private set; } = -1;

        public void Configure(SpriteRenderer renderer)
        {
            _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
            for (var index = 0; index < 2; index++)
            {
                _eastContacts[index] = Resources.Load<Sprite>(EastResourceRoot + index + "_v1");
                _westContacts[index] = Resources.Load<Sprite>(WestResourceRoot + index + "_v1");
                if (_eastContacts[index] == null || _westContacts[index] == null)
                    throw new InvalidOperationException("Missing Player diagonal contact resource " + index);
            }
            _configured = true;
        }

        public void Present(
            float gaitPhase01,
            int direction,
            bool moving,
            bool legacyPoseOwnsPresentation,
            bool presentationAway)
        {
            if (!_configured) return;
            bool westDiagonal = direction == 1 || direction == 3;
            bool eastDiagonal = direction == 5 || direction == 7;
            bool useContact = !presentationAway && !legacyPoseOwnsPresentation && moving &&
                              (westDiagonal || eastDiagonal);
            IsActive = useContact;
            ActiveFrameIndex = useContact ? (Mathf.Repeat(gaitPhase01, 1f) < 0.5f ? 0 : 1) : -1;
            ActiveSourceDirection = useContact
                ? (westDiagonal ? PlayerWestContactPresenter.WestDirection : PlayerEastContactPresenter.EastDirection)
                : -1;
            if (!useContact) return;
            _renderer.sprite = westDiagonal
                ? _westContacts[ActiveFrameIndex]
                : _eastContacts[ActiveFrameIndex];
        }
    }
}
