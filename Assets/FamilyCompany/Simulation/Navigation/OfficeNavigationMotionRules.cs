using System;
using System.Collections.Generic;

namespace FamilyCompany.Simulation.Navigation
{
    public readonly struct OfficeTrafficAgentState
    {
        public OfficeTrafficAgentState(
            string agentId,
            OfficeNavPoint position,
            OfficeNavPoint desiredVelocity,
            float radius,
            float stuckSeconds)
        {
            AgentId = string.IsNullOrWhiteSpace(agentId)
                ? throw new ArgumentException("Agent ID is required.", nameof(agentId))
                : agentId;
            if (radius <= 0f || float.IsNaN(radius) || float.IsInfinity(radius))
                throw new ArgumentOutOfRangeException(nameof(radius));
            Position = position;
            DesiredVelocity = desiredVelocity;
            Radius = radius;
            StuckSeconds = Math.Max(0f, stuckSeconds);
        }

        public string AgentId { get; }
        public OfficeNavPoint Position { get; }
        public OfficeNavPoint DesiredVelocity { get; }
        public float Radius { get; }
        public float StuckSeconds { get; }
    }

    public readonly struct OfficeTrafficDecision
    {
        public OfficeTrafficDecision(
            float forwardScale,
            OfficeNavPoint recoveryDirection,
            float recoveryWeight,
            bool isYielding,
            bool shouldReplan)
        {
            ForwardScale = forwardScale;
            RecoveryDirection = recoveryDirection;
            RecoveryWeight = recoveryWeight;
            IsYielding = isYielding;
            ShouldReplan = shouldReplan;
        }

        public float ForwardScale { get; }
        public OfficeNavPoint RecoveryDirection { get; }
        public float RecoveryWeight { get; }
        public bool IsYielding { get; }
        public bool ShouldReplan { get; }
    }

    public static class OfficeNavigationTrafficRules
    {
        public const float PredictionSeconds = 0.55f;
        public const float RecoveryThresholdSeconds = 0.80f;
        public const float ReplanThresholdSeconds = 1.10f;

        public static OfficeTrafficDecision Resolve(
            OfficeTrafficAgentState self,
            IReadOnlyList<OfficeTrafficAgentState> peers)
        {
            if (peers == null) throw new ArgumentNullException(nameof(peers));
            var forwardScale = 1f;
            var yielding = false;
            var shouldReplan = false;
            var recoveryDirection = new OfficeNavPoint(0f, 0f);
            var recoveryWeight = 0f;
            string selectedPeerId = null;
            for (var index = 0; index < peers.Count; index++)
            {
                var peer = peers[index];
                if (string.Equals(self.AgentId, peer.AgentId, StringComparison.Ordinal)) continue;
                if (!WillConflict(self, peer)) continue;
                var selfHasPriority = string.CompareOrdinal(self.AgentId, peer.AgentId) < 0;
                if (selfHasPriority)
                {
                    forwardScale = Math.Min(forwardScale, 0.82f);
                    continue;
                }

                yielding = true;
                forwardScale = 0f;
                if (self.StuckSeconds < RecoveryThresholdSeconds) continue;
                if (selectedPeerId != null && string.CompareOrdinal(peer.AgentId, selectedPeerId) >= 0) continue;
                selectedPeerId = peer.AgentId;
                var forward = self.DesiredVelocity.Normalized;
                var sideSign = StableSideSign(self.AgentId, peer.AgentId);
                var lateral = new OfficeNavPoint(-forward.Z * sideSign, forward.X * sideSign);
                var retreat = forward * -0.35f;
                recoveryDirection = (lateral + retreat).Normalized;
                recoveryWeight = 0.72f;
                shouldReplan = self.StuckSeconds >= ReplanThresholdSeconds;
            }

            return new OfficeTrafficDecision(
                forwardScale,
                recoveryDirection,
                recoveryWeight,
                yielding,
                shouldReplan);
        }

        private static bool WillConflict(OfficeTrafficAgentState self, OfficeTrafficAgentState peer)
        {
            var relativePosition = peer.Position - self.Position;
            var relativeVelocity = peer.DesiredVelocity - self.DesiredVelocity;
            var combinedRadius = self.Radius + peer.Radius + 0.12f;
            if (relativePosition.SqrMagnitude <= combinedRadius * combinedRadius) return true;
            var relativeSpeedSquared = relativeVelocity.SqrMagnitude;
            if (relativeSpeedSquared <= 0.0001f) return false;
            var time = -OfficeNavPoint.Dot(relativePosition, relativeVelocity) / relativeSpeedSquared;
            time = Math.Max(0f, Math.Min(PredictionSeconds, time));
            var closest = relativePosition + relativeVelocity * time;
            return closest.SqrMagnitude <= combinedRadius * combinedRadius;
        }

        private static float StableSideSign(string left, string right)
        {
            var first = string.CompareOrdinal(left, right) <= 0 ? left : right;
            var second = string.CompareOrdinal(left, right) <= 0 ? right : left;
            unchecked
            {
                uint hash = 2166136261;
                var text = first + "|" + second;
                for (var index = 0; index < text.Length; index++)
                {
                    hash ^= text[index];
                    hash *= 16777619;
                }

                return (hash & 1) == 0 ? -1f : 1f;
            }
        }
    }

    public static class OfficeFacingHysteresisRules
    {
        public const int DirectionCount = 8;

        public static int ResolveDirection(
            float horizontal,
            float vertical,
            int currentDirection,
            float hysteresisDegrees = 7.5f)
        {
            if (currentDirection < 0 || currentDirection >= DirectionCount)
                throw new ArgumentOutOfRangeException(nameof(currentDirection));
            if (hysteresisDegrees < 0f || hysteresisDegrees >= 22.5f ||
                float.IsNaN(hysteresisDegrees) || float.IsInfinity(hysteresisDegrees))
                throw new ArgumentOutOfRangeException(nameof(hysteresisDegrees));
            if (float.IsNaN(horizontal) || float.IsInfinity(horizontal) ||
                float.IsNaN(vertical) || float.IsInfinity(vertical))
                throw new ArgumentOutOfRangeException(nameof(horizontal));
            if (horizontal * horizontal + vertical * vertical <= 0.000001f) return currentDirection;

            var angle = Math.Atan2(-horizontal, -vertical) * 180d / Math.PI;
            if (angle < 0d) angle += 360d;
            var currentCenter = currentDirection * 45d;
            var delta = Math.Abs(ShortestAngle(angle - currentCenter));
            if (delta <= 22.5d + hysteresisDegrees) return currentDirection;
            return ((int)Math.Floor(angle / 45d + 0.5d)) % DirectionCount;
        }

        private static double ShortestAngle(double value)
        {
            value %= 360d;
            if (value > 180d) value -= 360d;
            if (value < -180d) value += 360d;
            return value;
        }
    }
}
