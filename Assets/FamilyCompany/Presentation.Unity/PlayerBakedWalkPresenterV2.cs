using System;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity
{
    /// <summary>
    /// Player-only consumer for authored eight-direction, eight-pose baked walk frames.
    /// It never changes the actor root, collision, path, stride, seating, or renderer count.
    /// </summary>
    public sealed class PlayerBakedWalkPresenterV2 : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private PlayerBakedWalkCatalogV2 _catalog;
        private bool _configured;
        private int _lastContactPose;

        public bool IsActive { get; private set; }
        public bool IsTurning { get; private set; }
        public int VisibleWalkPose { get; private set; } = -1;
        public int VisibleWalkDirection { get; private set; } = -1;
        public string VisibleWalkSpriteName { get; private set; } = string.Empty;
        public PlayerWalkSupportLegV2 VisibleSupportLeg { get; private set; }
        public Vector2 VisibleSupportFootWorld { get; private set; }

        public void Configure(SpriteRenderer renderer, PlayerBakedWalkCatalogV2 catalog)
        {
            _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _catalog.Validate();
            _configured = true;
            ResetState();
        }

        public void Present(
            float gaitPhase01,
            int direction,
            bool moving,
            bool legacyPoseOwnsPresentation,
            bool presentationAway,
            bool plantedTurnActive,
            float plantedTurnProgress01,
            int plantedTurnFromDirection,
            int plantedTurnTargetDirection)
        {
            if (!_configured) return;
            if (presentationAway || legacyPoseOwnsPresentation)
            {
                ResetState();
                return;
            }

            if (plantedTurnActive)
            {
                PresentTurn(
                    Mathf.Clamp01(plantedTurnProgress01),
                    plantedTurnFromDirection,
                    plantedTurnTargetDirection);
                return;
            }

            if (!moving)
            {
                ResetState();
                return;
            }

            if (direction < 0 || direction >= PlayerBakedWalkCatalogV2.DirectionCount)
                throw new ArgumentOutOfRangeException(nameof(direction));
            int pose = Mathf.Min(
                PlayerBakedWalkCatalogV2.PoseCount - 1,
                Mathf.FloorToInt(Mathf.Repeat(gaitPhase01, 1f) * PlayerBakedWalkCatalogV2.PoseCount));
            if (pose == 0 || pose == 4) _lastContactPose = pose;
            Apply(direction, pose, false);
        }

        private void PresentTurn(float progress, int fromDirection, int targetDirection)
        {
            if (fromDirection < 0 || fromDirection >= PlayerBakedWalkCatalogV2.DirectionCount ||
                targetDirection < 0 || targetDirection >= PlayerBakedWalkCatalogV2.DirectionCount)
            {
                ResetState();
                return;
            }

            int displayDirection;
            if (progress < 0.30f)
            {
                displayDirection = fromDirection;
            }
            else if (progress < 0.70f)
            {
                int clockwise = (targetDirection - fromDirection + 8) % 8;
                int signedDelta = clockwise <= 4 ? clockwise : clockwise - 8;
                int middleStep = signedDelta == 0
                    ? 0
                    : signedDelta > 0
                        ? Mathf.Max(1, (signedDelta + 1) / 2)
                        : Mathf.Min(-1, (signedDelta - 1) / 2);
                displayDirection = (fromDirection + middleStep + 8) % 8;
            }
            else
            {
                displayDirection = targetDirection;
            }
            Apply(displayDirection, _lastContactPose == 4 ? 4 : 0, true);
        }

        private void Apply(int direction, int pose, bool turning)
        {
            PlayerBakedWalkDirectionV2 row = _catalog.DirectionAt(direction);
            Sprite sprite = row.SpriteAt(pose);
            _renderer.sprite = sprite;
            IsActive = true;
            IsTurning = turning;
            VisibleWalkPose = pose;
            VisibleWalkDirection = direction;
            VisibleWalkSpriteName = sprite.name ?? string.Empty;
            VisibleSupportLeg = row.SupportLegAt(pose);
            Vector2 anchor = row.SupportFootAnchorAt(pose);
            Vector3 local = new Vector3(
                (anchor.x - sprite.pivot.x) / sprite.pixelsPerUnit,
                (anchor.y - sprite.pivot.y) / sprite.pixelsPerUnit,
                0f);
            Vector3 world = _renderer.transform.TransformPoint(local);
            VisibleSupportFootWorld = new Vector2(world.x, world.y);
        }

        private void ResetState()
        {
            IsActive = false;
            IsTurning = false;
            VisibleWalkPose = -1;
            VisibleWalkDirection = -1;
            VisibleWalkSpriteName = string.Empty;
            VisibleSupportLeg = PlayerWalkSupportLegV2.None;
            VisibleSupportFootWorld = Vector2.zero;
        }
    }
}
