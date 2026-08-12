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

        public static float ResolveVelocityChangeRate(
            OfficeNavPoint currentVelocity,
            OfficeNavPoint targetVelocity,
            float baseChangePerSecond,
            bool directPlayerControl)
        {
            if (baseChangePerSecond <= 0f || float.IsNaN(baseChangePerSecond) ||
                float.IsInfinity(baseChangePerSecond))
                throw new ArgumentOutOfRangeException(nameof(baseChangePerSecond));
            if (!directPlayerControl) return baseChangePerSecond;
            if (currentVelocity.SqrMagnitude <= 0.000001f) return baseChangePerSecond;
            if (targetVelocity.SqrMagnitude <= 0.000001f) return baseChangePerSecond * 1.7f;

            float alignment = OfficeNavPoint.Dot(currentVelocity.Normalized, targetVelocity.Normalized);
            return alignment <= -0.70710678f
                ? baseChangePerSecond * 1.8f
                : baseChangePerSecond;
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

    public static class OfficeCollisionSlideRules
    {
        public static OfficeNavPoint SelectBestAxisSlide(
            OfficeNavPoint intendedDisplacement,
            OfficeNavPoint semanticVelocity,
            OfficeNavPoint previousDisplacement,
            bool canMoveX,
            bool canMoveZ,
            string stableKey)
        {
            bool hasX = canMoveX && Math.Abs(intendedDisplacement.X) > 0.000001f;
            bool hasZ = canMoveZ && Math.Abs(intendedDisplacement.Z) > 0.000001f;
            if (!hasX && !hasZ) return new OfficeNavPoint(0f, 0f);
            var xOnly = new OfficeNavPoint(intendedDisplacement.X, 0f);
            var zOnly = new OfficeNavPoint(0f, intendedDisplacement.Z);
            if (!hasX) return zOnly;
            if (!hasZ) return xOnly;

            float xScore = Score(xOnly, semanticVelocity, previousDisplacement);
            float zScore = Score(zOnly, semanticVelocity, previousDisplacement);
            if (Math.Abs(xScore - zScore) > 0.000001f)
                return xScore > zScore ? xOnly : zOnly;
            return StablePreferX(stableKey) ? xOnly : zOnly;
        }

        private static float Score(
            OfficeNavPoint candidate,
            OfficeNavPoint semanticVelocity,
            OfficeNavPoint previousDisplacement)
        {
            OfficeNavPoint semantic = semanticVelocity.SqrMagnitude > 0.000001f
                ? semanticVelocity.Normalized
                : candidate.Normalized;
            float progress = OfficeNavPoint.Dot(candidate, semantic);
            float alignment = OfficeNavPoint.Dot(candidate.Normalized, semantic) * candidate.Magnitude * 0.05f;
            float continuity = previousDisplacement.SqrMagnitude > 0.000001f
                ? Math.Max(0f, OfficeNavPoint.Dot(candidate.Normalized, previousDisplacement.Normalized)) *
                  candidate.Magnitude * 0.02f
                : 0f;
            return progress + alignment + continuity;
        }

        private static bool StablePreferX(string stableKey)
        {
            unchecked
            {
                uint hash = 2166136261;
                string value = stableKey ?? string.Empty;
                for (var index = 0; index < value.Length; index++)
                {
                    hash ^= value[index];
                    hash *= 16777619;
                }
                return (hash & 1u) == 0u;
            }
        }
    }

    public readonly struct OfficeLocomotionFacingState
    {
        public OfficeLocomotionFacingState(
            int visualDirection,
            int candidateDirection,
            float candidateSeconds,
            float projectedSeconds)
        {
            if (visualDirection < 0 || visualDirection >= OfficeFacingHysteresisRules.DirectionCount)
                throw new ArgumentOutOfRangeException(nameof(visualDirection));
            if (candidateDirection < -1 || candidateDirection >= OfficeFacingHysteresisRules.DirectionCount)
                throw new ArgumentOutOfRangeException(nameof(candidateDirection));
            VisualDirection = visualDirection;
            CandidateDirection = candidateDirection;
            CandidateSeconds = Math.Max(0f, candidateSeconds);
            ProjectedSeconds = Math.Max(0f, projectedSeconds);
        }

        public int VisualDirection { get; }
        public int CandidateDirection { get; }
        public float CandidateSeconds { get; }
        public float ProjectedSeconds { get; }

        public static OfficeLocomotionFacingState Initial(int visualDirection)
        {
            return new OfficeLocomotionFacingState(visualDirection, -1, 0f, 0f);
        }
    }

    public readonly struct OfficeLocomotionFacingResult
    {
        public OfficeLocomotionFacingResult(
            OfficeLocomotionFacingState state,
            int semanticDirection,
            int motionDirection,
            bool usedSemanticHeading)
        {
            State = state;
            SemanticDirection = semanticDirection;
            MotionDirection = motionDirection;
            UsedSemanticHeading = usedSemanticHeading;
        }

        public OfficeLocomotionFacingState State { get; }
        public int SemanticDirection { get; }
        public int MotionDirection { get; }
        public bool UsedSemanticHeading { get; }
    }

    public static class OfficeLocomotionPresentationRules
    {
        public const float DefaultHysteresisDegrees = 4f;
        public const float DefaultFacingStabilizationSeconds = 0.075f;
        public const float CollisionProjectionHoldSeconds = 0.15f;
        private const float MinimumMotionSquared = 0.0000001f;
        private const float CosineFortyFiveDegrees = 0.70710678f;

        public static OfficeLocomotionFacingResult ResolveFacing(
            OfficeLocomotionFacingState state,
            OfficeNavPoint semanticDisplacement,
            OfficeNavPoint motionDisplacement,
            float deltaTime,
            bool collisionProjected,
            float hysteresisDegrees = DefaultHysteresisDegrees,
            float stabilizationSeconds = DefaultFacingStabilizationSeconds)
        {
            if (deltaTime < 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (hysteresisDegrees < 0f || hysteresisDegrees >= 22.5f ||
                float.IsNaN(hysteresisDegrees) || float.IsInfinity(hysteresisDegrees))
                throw new ArgumentOutOfRangeException(nameof(hysteresisDegrees));
            if (stabilizationSeconds < 0f || float.IsNaN(stabilizationSeconds) ||
                float.IsInfinity(stabilizationSeconds))
                throw new ArgumentOutOfRangeException(nameof(stabilizationSeconds));

            int current = state.VisualDirection;
            int semanticDirection = ResolveNearestDirection(semanticDisplacement, current);
            int motionDirection = ResolveNearestDirection(motionDisplacement, current);
            float projectedSeconds = collisionProjected
                ? state.ProjectedSeconds + deltaTime
                : 0f;
            if (motionDisplacement.SqrMagnitude <= MinimumMotionSquared)
            {
                return new OfficeLocomotionFacingResult(
                    new OfficeLocomotionFacingState(current, -1, 0f, projectedSeconds),
                    semanticDirection,
                    motionDirection,
                    false);
            }

            bool hasSemantic = semanticDisplacement.SqrMagnitude > MinimumMotionSquared;
            bool aligned = !hasSemantic ||
                           OfficeNavPoint.Dot(
                               semanticDisplacement.Normalized,
                               motionDisplacement.Normalized) >= CosineFortyFiveDegrees;
            bool useSemantic = hasSemantic &&
                               ((!aligned && !collisionProjected) ||
                                (collisionProjected && projectedSeconds <= CollisionProjectionHoldSeconds));
            OfficeNavPoint heading = useSemantic ? semanticDisplacement : motionDisplacement;
            int proposed = OfficeFacingHysteresisRules.ResolveDirection(
                heading.X,
                heading.Z,
                current,
                hysteresisDegrees);
            if (proposed == current)
            {
                return new OfficeLocomotionFacingResult(
                    new OfficeLocomotionFacingState(current, -1, 0f, projectedSeconds),
                    semanticDirection,
                    motionDirection,
                    useSemantic);
            }

            float candidateSeconds = state.CandidateDirection == proposed
                ? state.CandidateSeconds + deltaTime
                : deltaTime;
            int candidateDirection = proposed;
            if (candidateSeconds + 0.000001f >= stabilizationSeconds)
            {
                current = proposed;
                candidateDirection = -1;
                candidateSeconds = 0f;
            }
            return new OfficeLocomotionFacingResult(
                new OfficeLocomotionFacingState(
                    current,
                    candidateDirection,
                    candidateSeconds,
                    projectedSeconds),
                semanticDirection,
                motionDirection,
                useSemantic);
        }

        public static int ResolveNearestDirection(OfficeNavPoint heading, int fallbackDirection)
        {
            if (fallbackDirection < 0 || fallbackDirection >= OfficeFacingHysteresisRules.DirectionCount)
                throw new ArgumentOutOfRangeException(nameof(fallbackDirection));
            if (heading.SqrMagnitude <= MinimumMotionSquared) return fallbackDirection;
            double angle = Math.Atan2(-heading.X, -heading.Z) * 180d / Math.PI;
            if (angle < 0d) angle += 360d;
            return ((int)Math.Floor(angle / 45d + 0.5d)) % OfficeFacingHysteresisRules.DirectionCount;
        }
    }

    public enum OfficeLocomotionPhase
    {
        Idle = 0,
        StartStep = 1,
        Walk = 2,
        Stopping = 3,
        Pivot = 4,
        ShortShuffle = 5
    }

    public readonly struct OfficeLocomotionGaitState
    {
        public OfficeLocomotionGaitState(
            OfficeLocomotionPhase phase,
            float accumulatedDistance,
            float episodeDistance,
            float stopSeconds,
            float transitionSeconds,
            int frame,
            int displayDirection,
            int pivotTargetDirection)
        {
            if (accumulatedDistance < 0f || float.IsNaN(accumulatedDistance) ||
                float.IsInfinity(accumulatedDistance))
                throw new ArgumentOutOfRangeException(nameof(accumulatedDistance));
            if (episodeDistance < 0f || float.IsNaN(episodeDistance) ||
                float.IsInfinity(episodeDistance))
                throw new ArgumentOutOfRangeException(nameof(episodeDistance));
            if (stopSeconds < 0f || float.IsNaN(stopSeconds) || float.IsInfinity(stopSeconds))
                throw new ArgumentOutOfRangeException(nameof(stopSeconds));
            if (transitionSeconds < 0f || float.IsNaN(transitionSeconds) ||
                float.IsInfinity(transitionSeconds))
                throw new ArgumentOutOfRangeException(nameof(transitionSeconds));
            if (frame < 0) throw new ArgumentOutOfRangeException(nameof(frame));
            if (displayDirection < 0 || displayDirection >= OfficeFacingHysteresisRules.DirectionCount)
                throw new ArgumentOutOfRangeException(nameof(displayDirection));
            if (pivotTargetDirection < -1 ||
                pivotTargetDirection >= OfficeFacingHysteresisRules.DirectionCount)
                throw new ArgumentOutOfRangeException(nameof(pivotTargetDirection));
            Phase = phase;
            AccumulatedDistance = accumulatedDistance;
            EpisodeDistance = episodeDistance;
            StopSeconds = stopSeconds;
            TransitionSeconds = transitionSeconds;
            Frame = frame;
            DisplayDirection = displayDirection;
            PivotTargetDirection = pivotTargetDirection;
        }

        public OfficeLocomotionPhase Phase { get; }
        public float AccumulatedDistance { get; }
        public float EpisodeDistance { get; }
        public float StopSeconds { get; }
        public float TransitionSeconds { get; }
        public int Frame { get; }
        public int DisplayDirection { get; }
        public int PivotTargetDirection { get; }

        public static OfficeLocomotionGaitState Initial(int direction)
        {
            return new OfficeLocomotionGaitState(
                OfficeLocomotionPhase.Idle,
                0f,
                0f,
                0f,
                0f,
                0,
                direction,
                -1);
        }
    }

    public static class OfficeLocomotionGaitRules
    {
        public const float DefaultStrideLength = 1.08f;
        public const float StopSettleSeconds = 0.10f;
        public const float PivotSeconds = 0.075f;
        public const float ShortShuffleStrideFraction = 0.30f;
        private const float MinimumDistance = 0.000001f;

        public static OfficeLocomotionGaitState Resolve(
            OfficeLocomotionGaitState state,
            float actualDistance,
            float deltaTime,
            bool motionRequested,
            int resolvedVisualDirection,
            float strideLength = DefaultStrideLength,
            int frameCount = 6)
        {
            if (actualDistance < 0f || float.IsNaN(actualDistance) || float.IsInfinity(actualDistance))
                throw new ArgumentOutOfRangeException(nameof(actualDistance));
            if (deltaTime < 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (strideLength <= 0f || float.IsNaN(strideLength) || float.IsInfinity(strideLength))
                throw new ArgumentOutOfRangeException(nameof(strideLength));
            if (frameCount < 2) throw new ArgumentOutOfRangeException(nameof(frameCount));
            if (resolvedVisualDirection < 0 ||
                resolvedVisualDirection >= OfficeFacingHysteresisRules.DirectionCount)
                throw new ArgumentOutOfRangeException(nameof(resolvedVisualDirection));

            if (actualDistance > MinimumDistance)
            {
                float accumulated = state.AccumulatedDistance + actualDistance;
                bool freshEpisode = state.Phase == OfficeLocomotionPhase.Idle;
                float episode = freshEpisode ? actualDistance : state.EpisodeDistance + actualDistance;
                float shuffleDistance = strideLength * ShortShuffleStrideFraction;
                int directionDelta = CircularDirectionDistance(
                    state.DisplayDirection,
                    resolvedVisualDirection);

                bool beginPivot = directionDelta >= 3 &&
                                  (state.Phase != OfficeLocomotionPhase.Pivot ||
                                   state.PivotTargetDirection != resolvedVisualDirection);
                if (beginPivot)
                {
                    return new OfficeLocomotionGaitState(
                        OfficeLocomotionPhase.Pivot,
                        accumulated,
                        episode,
                        0f,
                        deltaTime,
                        NearestContactFrame(state.Frame, frameCount),
                        state.DisplayDirection,
                        resolvedVisualDirection);
                }

                if (state.Phase == OfficeLocomotionPhase.Pivot)
                {
                    float pivotSeconds = state.TransitionSeconds + deltaTime;
                    if (pivotSeconds + 0.000001f < PivotSeconds)
                    {
                        return new OfficeLocomotionGaitState(
                            OfficeLocomotionPhase.Pivot,
                            accumulated,
                            episode,
                            0f,
                            pivotSeconds,
                            NearestContactFrame(state.Frame, frameCount),
                            state.DisplayDirection,
                            state.PivotTargetDirection);
                    }
                    resolvedVisualDirection = state.PivotTargetDirection >= 0
                        ? state.PivotTargetDirection
                        : resolvedVisualDirection;
                }

                OfficeLocomotionPhase movingPhase = episode < shuffleDistance
                    ? OfficeLocomotionPhase.StartStep
                    : OfficeLocomotionPhase.Walk;
                int frame = movingPhase == OfficeLocomotionPhase.Walk
                    ? DistanceFrame(accumulated, strideLength, frameCount)
                    : ShuffleFrame(episode, shuffleDistance, frameCount);
                return new OfficeLocomotionGaitState(
                    movingPhase,
                    accumulated,
                    episode,
                    0f,
                    0f,
                    frame,
                    resolvedVisualDirection,
                    -1);
            }

            if (motionRequested)
            {
                return new OfficeLocomotionGaitState(
                    state.Phase,
                    state.AccumulatedDistance,
                    state.EpisodeDistance,
                    0f,
                    state.TransitionSeconds,
                    state.Frame,
                    state.DisplayDirection,
                    state.PivotTargetDirection);
            }

            float stopSeconds = state.StopSeconds + deltaTime;
            if (state.Phase == OfficeLocomotionPhase.Idle ||
                stopSeconds + 0.000001f >= StopSettleSeconds)
            {
                return new OfficeLocomotionGaitState(
                    OfficeLocomotionPhase.Idle,
                    state.AccumulatedDistance,
                    0f,
                    stopSeconds,
                    0f,
                    NearestContactFrame(state.Frame, frameCount),
                    state.DisplayDirection,
                    -1);
            }

            OfficeLocomotionPhase stoppingPhase =
                state.EpisodeDistance < strideLength * ShortShuffleStrideFraction
                    ? OfficeLocomotionPhase.ShortShuffle
                    : OfficeLocomotionPhase.Stopping;
            return new OfficeLocomotionGaitState(
                stoppingPhase,
                state.AccumulatedDistance,
                state.EpisodeDistance,
                stopSeconds,
                0f,
                state.Frame,
                state.DisplayDirection,
                -1);
        }

        public static float Phase01(float accumulatedDistance, float strideLength)
        {
            if (accumulatedDistance < 0f || float.IsNaN(accumulatedDistance) ||
                float.IsInfinity(accumulatedDistance))
                throw new ArgumentOutOfRangeException(nameof(accumulatedDistance));
            if (strideLength <= 0f || float.IsNaN(strideLength) || float.IsInfinity(strideLength))
                throw new ArgumentOutOfRangeException(nameof(strideLength));
            double cycles = accumulatedDistance / strideLength;
            return (float)(cycles - Math.Floor(cycles));
        }

        public static int DistanceFrame(float accumulatedDistance, float strideLength, int frameCount)
        {
            if (frameCount < 2) throw new ArgumentOutOfRangeException(nameof(frameCount));
            int frame = (int)Math.Floor(Phase01(accumulatedDistance, strideLength) * frameCount);
            return Math.Min(frameCount - 1, Math.Max(0, frame));
        }

        private static int ShuffleFrame(float episodeDistance, float shuffleDistance, int frameCount)
        {
            if (shuffleDistance <= MinimumDistance) return 0;
            return episodeDistance / shuffleDistance < 0.5f ? 0 : frameCount / 2;
        }

        private static int NearestContactFrame(int frame, int frameCount)
        {
            int first = 0;
            int second = frameCount / 2;
            int normalized = ((frame % frameCount) + frameCount) % frameCount;
            int distanceToFirst = Math.Min(normalized, frameCount - normalized);
            int rawDistanceToSecond = Math.Abs(normalized - second);
            int distanceToSecond = Math.Min(rawDistanceToSecond, frameCount - rawDistanceToSecond);
            return distanceToFirst <= distanceToSecond ? first : second;
        }

        private static int CircularDirectionDistance(int from, int to)
        {
            int delta = Math.Abs(from - to) % OfficeFacingHysteresisRules.DirectionCount;
            return Math.Min(delta, OfficeFacingHysteresisRules.DirectionCount - delta);
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
