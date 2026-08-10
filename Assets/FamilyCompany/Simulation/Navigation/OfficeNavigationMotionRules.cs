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

    public readonly struct OfficeMotionIntegrationResult
    {
        public OfficeMotionIntegrationResult(OfficeNavPoint velocity, OfficeNavPoint displacement)
        {
            Velocity = velocity;
            Displacement = displacement;
        }

        public OfficeNavPoint Velocity { get; }
        public OfficeNavPoint Displacement { get; }
    }

    public static class OfficeNavigationMotionIntegrator
    {
        public const float MaximumStableStepSeconds = 0.05f;

        public static OfficeMotionIntegrationResult IntegrateVelocity(
            OfficeNavPoint currentVelocity,
            OfficeNavPoint targetVelocity,
            float changePerSecond,
            float deltaTime)
        {
            if (changePerSecond <= 0f || float.IsNaN(changePerSecond) || float.IsInfinity(changePerSecond))
                throw new ArgumentOutOfRangeException(nameof(changePerSecond));
            if (deltaTime < 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (deltaTime <= 0f)
                return new OfficeMotionIntegrationResult(currentVelocity, new OfficeNavPoint(0f, 0f));

            var difference = targetVelocity - currentVelocity;
            var differenceMagnitude = difference.Magnitude;
            if (differenceMagnitude <= 0.000001f)
                return new OfficeMotionIntegrationResult(targetVelocity, targetVelocity * deltaTime);

            var timeToTarget = differenceMagnitude / changePerSecond;
            if (timeToTarget >= deltaTime)
            {
                var next = currentVelocity + difference * (changePerSecond * deltaTime / differenceMagnitude);
                return new OfficeMotionIntegrationResult(
                    next,
                    (currentVelocity + next) * (0.5f * deltaTime));
            }

            var rampDisplacement = (currentVelocity + targetVelocity) * (0.5f * timeToTarget);
            var steadyDisplacement = targetVelocity * (deltaTime - timeToTarget);
            return new OfficeMotionIntegrationResult(targetVelocity, rampDisplacement + steadyDisplacement);
        }

        public static OfficeNavPoint ClampDisplacement(OfficeNavPoint displacement, float maximumDistance)
        {
            if (maximumDistance < 0f || float.IsNaN(maximumDistance) || float.IsInfinity(maximumDistance))
                throw new ArgumentOutOfRangeException(nameof(maximumDistance));
            var magnitude = displacement.Magnitude;
            if (magnitude <= maximumDistance || magnitude <= 0.000001f) return displacement;
            return displacement * (maximumDistance / magnitude);
        }

        public static int CalculateStepCount(float deltaTime)
        {
            if (deltaTime < 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            return deltaTime <= 0f
                ? 0
                : Math.Max(1, (int)Math.Ceiling(deltaTime / MaximumStableStepSeconds - 0.000001f));
        }

        public static float ResolveStepDelta(float deltaTime, int stepIndex, int stepCount)
        {
            if (deltaTime < 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (stepCount != CalculateStepCount(deltaTime))
                throw new ArgumentOutOfRangeException(nameof(stepCount));
            if (stepIndex < 0 || stepIndex >= stepCount)
                throw new ArgumentOutOfRangeException(nameof(stepIndex));
            if (stepIndex < stepCount - 1) return MaximumStableStepSeconds;
            return deltaTime - MaximumStableStepSeconds * (stepCount - 1);
        }
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
                for (var index = 0; index < first.Length; index++)
                {
                    hash ^= first[index];
                    hash *= 16777619;
                }

                hash ^= '|';
                hash *= 16777619;
                for (var index = 0; index < second.Length; index++)
                {
                    hash ^= second[index];
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
