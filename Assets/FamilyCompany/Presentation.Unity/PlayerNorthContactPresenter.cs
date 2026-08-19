using System;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity
{
    /// <summary>
    /// North-only, source-exact contact baseline. Every unrelated direction
    /// and non-walking pose remains owned by the existing animator.
    /// </summary>
    public sealed class PlayerNorthContactPresenter : MonoBehaviour
    {
        public const int NorthDirection = 4;
        private const string ResourceRoot =
            "FamilyCompany/PlayerNorthContactV1/Frames/player_north_contact_";

        private readonly Sprite[] _contacts = new Sprite[2];
        private SpriteRenderer _renderer;
        private bool _configured;

        public bool IsActive { get; private set; }
        public int ActiveFrameIndex { get; private set; } = -1;

        public void Configure(SpriteRenderer renderer)
        {
            _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
            for (var index = 0; index < _contacts.Length; index++)
            {
                _contacts[index] = Resources.Load<Sprite>(ResourceRoot + index + "_v1");
                if (_contacts[index] == null)
                    throw new InvalidOperationException("Missing Player north contact resource " + index);
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
                              moving && direction == NorthDirection;
            IsActive = useContact;
            ActiveFrameIndex = useContact ? (Mathf.Repeat(gaitPhase01, 1f) < 0.5f ? 0 : 1) : -1;
            if (useContact) _renderer.sprite = _contacts[ActiveFrameIndex];
        }
    }
}
