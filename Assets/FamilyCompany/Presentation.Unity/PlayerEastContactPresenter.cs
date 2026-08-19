using System;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity
{
    /// <summary>
    /// East-only, source-exact contact baseline. The existing animator remains
    /// authoritative for every non-east direction and non-walking pose.
    /// </summary>
    public sealed class PlayerEastContactPresenter : MonoBehaviour
    {
        public const int EastDirection = 6;
        private const string ResourceRoot =
            "FamilyCompany/PlayerEastContactV1/Frames/player_east_contact_";

        private readonly Sprite[] _contacts = new Sprite[2];
        private SpriteRenderer _renderer;
        private bool _configured;

        public bool IsActive { get; private set; }
        public bool IsVisible => _renderer != null && _renderer.enabled;
        public int ActiveFrameIndex { get; private set; } = -1;

        public void Configure(SpriteRenderer renderer)
        {
            _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
            for (var index = 0; index < _contacts.Length; index++)
            {
                _contacts[index] = Resources.Load<Sprite>(ResourceRoot + index + "_v1");
                if (_contacts[index] == null)
                    throw new InvalidOperationException("Missing Player east contact resource " + index);
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
            bool useContact = !presentationAway && !legacyPoseOwnsPresentation &&
                              moving && direction == EastDirection;
            IsActive = useContact;
            ActiveFrameIndex = useContact ? (Mathf.Repeat(gaitPhase01, 1f) < 0.5f ? 0 : 1) : -1;
            if (useContact) _renderer.sprite = _contacts[ActiveFrameIndex];
        }

    }
}
