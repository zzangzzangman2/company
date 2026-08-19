using System;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity
{
    /// <summary>
    /// Player-only four-pose gait and planted cardinal turn presentation.
    /// Navigation owns the root; this class only chooses ground-aligned sprites.
    /// </summary>
    public sealed class PlayerNaturalWalkPresenter : MonoBehaviour
    {
        private const int South = 0;
        private const int West = 1;
        private const int North = 2;
        private const int East = 3;

        private static readonly string[] DirectionNames = { "south", "west", "north", "east" };
        private static readonly string[] ContactRoots =
        {
            "FamilyCompany/PlayerSouthContactV1/Frames/player_south_contact_",
            "FamilyCompany/PlayerWestContactV1/Frames/player_west_contact_",
            "FamilyCompany/PlayerNorthContactV1/Frames/player_north_contact_",
            "FamilyCompany/PlayerEastContactV1/Frames/player_east_contact_"
        };

        private readonly Sprite[,] _contacts = new Sprite[4, 2];
        private readonly Sprite[,] _toes = new Sprite[4, 2];
        private readonly Sprite[,] _passes = new Sprite[4, 2];
        private readonly Sprite[,] _lands = new Sprite[4, 2];
        private SpriteRenderer _renderer;
        private bool _configured;
        private int _lastContactIndex;

        public bool IsActive { get; private set; }
        public bool IsTurning { get; private set; }
        public int ActiveSourceDirection { get; private set; } = -1;
        public int ActivePose { get; private set; } = -1;

        public void Configure(SpriteRenderer renderer)
        {
            _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
            for (var sourceDirection = 0; sourceDirection < 4; sourceDirection++)
            for (var index = 0; index < 2; index++)
            {
                _contacts[sourceDirection, index] = Resources.Load<Sprite>(
                    ContactRoots[sourceDirection] + index + "_v1");
                _passes[sourceDirection, index] = Resources.Load<Sprite>(
                    "FamilyCompany/PlayerNaturalWalkV1/Frames/player_" +
                    DirectionNames[sourceDirection] + "_pass_" + index + "_v1");
                _toes[sourceDirection, index] = Resources.Load<Sprite>(
                    "FamilyCompany/PlayerNaturalWalkV1/Frames/player_" +
                    DirectionNames[sourceDirection] + "_toe_" + index + "_v1");
                _lands[sourceDirection, index] = Resources.Load<Sprite>(
                    "FamilyCompany/PlayerNaturalWalkV1/Frames/player_" +
                    DirectionNames[sourceDirection] + "_land_" + index + "_v1");
                if (_contacts[sourceDirection, index] == null ||
                    _toes[sourceDirection, index] == null ||
                    _passes[sourceDirection, index] == null ||
                    _lands[sourceDirection, index] == null)
                    throw new InvalidOperationException(
                        "Missing Player natural walk sprite direction=" +
                        DirectionNames[sourceDirection] + " index=" + index);
            }
            _configured = true;
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
            bool available = !presentationAway && !legacyPoseOwnsPresentation;
            if (!available)
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

            int sourceDirection = MapVisualToSource(direction);
            float phase = Mathf.Repeat(gaitPhase01, 1f);
            int pose = Mathf.Min(7, Mathf.FloorToInt(phase * 8f));
            IsActive = true;
            IsTurning = false;
            ActiveSourceDirection = sourceDirection;
            ActivePose = pose;
            switch (pose)
            {
                case 0:
                    _lastContactIndex = 0;
                    _renderer.sprite = _contacts[sourceDirection, 0];
                    break;
                case 1:
                    _renderer.sprite = _toes[sourceDirection, 0];
                    break;
                case 2:
                    _renderer.sprite = _passes[sourceDirection, 0];
                    break;
                case 3:
                    _lastContactIndex = 1;
                    _renderer.sprite = _lands[sourceDirection, 1];
                    break;
                case 4:
                    _lastContactIndex = 1;
                    _renderer.sprite = _contacts[sourceDirection, 1];
                    break;
                case 5:
                    _renderer.sprite = _toes[sourceDirection, 1];
                    break;
                case 6:
                    _renderer.sprite = _passes[sourceDirection, 1];
                    break;
                default:
                    _lastContactIndex = 0;
                    _renderer.sprite = _lands[sourceDirection, 0];
                    break;
            }
        }

        private void PresentTurn(float progress, int fromDirection, int targetDirection)
        {
            if (fromDirection < 0 || fromDirection >= 8 ||
                targetDirection < 0 || targetDirection >= 8)
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

            int sourceDirection = MapVisualToSource(displayDirection);
            IsActive = true;
            IsTurning = true;
            ActiveSourceDirection = sourceDirection;
            ActivePose = _lastContactIndex;
            _renderer.sprite = _contacts[sourceDirection, _lastContactIndex];
        }

        private void ResetState()
        {
            IsActive = false;
            IsTurning = false;
            ActiveSourceDirection = -1;
            ActivePose = -1;
        }

        private static int MapVisualToSource(int direction)
        {
            return direction switch
            {
                0 => South,
                1 => West,
                2 => West,
                3 => West,
                4 => North,
                5 => East,
                6 => East,
                7 => East,
                _ => throw new ArgumentOutOfRangeException(nameof(direction))
            };
        }
    }
}
