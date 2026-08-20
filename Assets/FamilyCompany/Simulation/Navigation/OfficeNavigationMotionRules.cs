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
        public const float DefaultAcceleration = 8f;
        public const float FinalApproachSlowRadius = 0.48f;
        public const float MinimumArrivalSpeedScale = 0.24f;

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

        public static float ResolveArrivalSpeedScale(float remainingDistance)
        {
            if (remainingDistance < 0f || float.IsNaN(remainingDistance) ||
                float.IsInfinity(remainingDistance))
                throw new ArgumentOutOfRangeException(nameof(remainingDistance));
            if (remainingDistance >= FinalApproachSlowRadius) return 1f;

            float normalized = remainingDistance / FinalApproachSlowRadius;
            float smooth = normalized * normalized * (3f - 2f * normalized);
            return MinimumArrivalSpeedScale + (1f - MinimumArrivalSpeedScale) * smooth;
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
        // Actual movement remains the sole facing authority. Hysteresis is limited to an adjacent
        // octant near a 22.5-degree boundary; a cardinal/lateral change (two or more octants) or a
        // heading beyond this envelope commits immediately, so the hold can never produce a
        // front-facing sideways walk.
        public const float DefaultHysteresisDegrees = 4f;
        public const float DefaultFacingStabilizationSeconds = 0.075f;
        public const float MaximumHeldFacingErrorDegrees = 30.5f;
        public const float CollisionProjectionHoldSeconds = 0f;
        // Matches OfficeNavPoint.Normalized's 1e-5 world-unit numerical-zero threshold.
        private const float MinimumMotionSquared = 0.0000000001f;

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
                bool hasSemantic = semanticDisplacement.SqrMagnitude > MinimumMotionSquared;
                if (!hasSemantic)
                {
                    return new OfficeLocomotionFacingResult(
                        new OfficeLocomotionFacingState(current, -1, 0f, projectedSeconds),
                        semanticDirection,
                        motionDirection,
                        false);
                }

                // A blocked or deliberately stopped actor may still turn toward its requested
                // heading. Commit the facing candidate immediately; the gait state owns the short
                // contact-foot pivot before movement resumes.
                int stationaryDirection = OfficeFacingHysteresisRules.ResolveDirection(
                    semanticDisplacement.X,
                    semanticDisplacement.Z,
                    current,
                    hysteresisDegrees);
                return new OfficeLocomotionFacingResult(
                    new OfficeLocomotionFacingState(
                        stationaryDirection,
                        -1,
                        0f,
                        projectedSeconds),
                    semanticDirection,
                    motionDirection,
                    true);
            }

            // Walking sprites follow actual displacement. Semantic intent can point across a
            // collision slide or against residual velocity, which otherwise renders a visible
            // backward/sideways walk even though the root is travelling elsewhere.
            OfficeNavPoint heading = motionDisplacement;
            OfficeNavPoint normalizedHeading = heading.Normalized;
            float currentAlignment = OfficeNavPoint.Dot(
                OfficeSharedLocomotionRules.DirectionVector(current),
                normalizedHeading);
            currentAlignment = Math.Max(-1f, Math.Min(1f, currentAlignment));
            float currentError = (float)(Math.Acos(currentAlignment) * 180d / Math.PI);
            // Do not pass actual displacement through OfficeFacingHysteresisRules' legacy input
            // magnitude dead-zone. ResolveFrame already proved this is measurable root motion,
            // including the final low-speed/corner sample, so its angle must remain authoritative.
            int proposed = hysteresisDegrees <= 0f
                ? motionDirection
                : currentError <= 22.5f + hysteresisDegrees
                    ? current
                    : motionDirection;
            if (proposed == current)
            {
                return new OfficeLocomotionFacingResult(
                    new OfficeLocomotionFacingState(current, -1, 0f, projectedSeconds),
                    semanticDirection,
                    motionDirection,
                    false);
            }

            int octantDistance = OfficeLocomotionGaitRules.DirectionDistance(current, proposed);
            bool commitImmediately = stabilizationSeconds <= 0f ||
                                     octantDistance >= 2 ||
                                     currentError > MaximumHeldFacingErrorDegrees + 0.0001f;
            if (commitImmediately)
            {
                return new OfficeLocomotionFacingResult(
                    new OfficeLocomotionFacingState(proposed, -1, 0f, projectedSeconds),
                    semanticDirection,
                    motionDirection,
                    false);
            }

            float candidateSeconds = state.CandidateDirection == proposed
                ? state.CandidateSeconds + deltaTime
                : deltaTime;
            if (candidateSeconds + 0.000001f < stabilizationSeconds)
            {
                return new OfficeLocomotionFacingResult(
                    new OfficeLocomotionFacingState(
                        current,
                        proposed,
                        candidateSeconds,
                        projectedSeconds),
                    semanticDirection,
                    motionDirection,
                    false);
            }

            return new OfficeLocomotionFacingResult(
                new OfficeLocomotionFacingState(
                    proposed,
                    -1,
                    0f,
                    projectedSeconds),
                semanticDirection,
                motionDirection,
                false);
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
        // One complete right/left cycle owns exactly one isometric tile-centre segment. Importing
        // KShopGo's 1.2-unit stride verbatim made our phase slip by about 20% at every tile because
        // its world scale is not ours. At the 1.0-unit/s office speed this is a calm ~0.994 s cycle.
        public const float ReferenceWalkCycleSeconds = 0.99380799f;
        public const float DefaultStrideLength = 0.99380799f;
        public const float StopSettleSeconds = 0.10f;
        // A stationary blocked/interaction-facing request owns one final heading for this short
        // planted interval. Free locomotion never waits for it before translation.
        public const float PivotSeconds = 0.06f;
        // A two-frame contact shuffle made short office moves visibly stutter. Even the first
        // measurable displacement now advances through the complete walk cycle.
        public const float ShortShuffleStrideFraction = 0f;
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
                int directionDelta = DirectionDistance(
                    state.DisplayDirection,
                    resolvedVisualDirection);
                // Moving frames never enter this branch, so actual displacement still owns facing.
                // A stationary route turn publishes only its final requested body row. Advancing
                // through adjacent octants made a 90-degree corner visibly look in two directions
                // and a reversal look in four directions before taking a step.
                if (directionDelta >= 1)
                {
                    bool beginPivot = state.Phase != OfficeLocomotionPhase.Pivot ||
                                      state.PivotTargetDirection != resolvedVisualDirection;
                    float pivotSeconds = beginPivot
                        ? deltaTime
                        : state.TransitionSeconds + deltaTime;
                    if (pivotSeconds + 0.000001f < PivotSeconds)
                    {
                        return new OfficeLocomotionGaitState(
                            OfficeLocomotionPhase.Pivot,
                            state.AccumulatedDistance,
                            0f,
                            0f,
                            pivotSeconds,
                            NearestContactFrame(state.Frame, frameCount),
                            resolvedVisualDirection,
                            resolvedVisualDirection);
                    }

                    return new OfficeLocomotionGaitState(
                        OfficeLocomotionPhase.Idle,
                        state.AccumulatedDistance,
                        0f,
                        0f,
                        0f,
                        NearestContactFrame(state.Frame, frameCount),
                        resolvedVisualDirection,
                        -1);
                }
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

        public static float StepPhase01(
            float accumulatedDistance,
            float strideLength = DefaultStrideLength)
        {
            float twoStepPhase = Phase01(accumulatedDistance, strideLength) * 2f;
            return twoStepPhase - (float)Math.Floor(twoStepPhase);
        }

        public static float PlantedFootPresentationOffset(
            float accumulatedDistance,
            float strideLength = DefaultStrideLength)
        {
            // The logical/collision root stays linear. The visual root eases from one foot contact
            // to the next: zero offset at both contacts, slow at plant, faster while the other foot
            // swings. This removes the conveyor-belt read without changing path or seat timing.
            float phase = StepPhase01(accumulatedDistance, strideLength);
            float eased = phase * phase * (3f - 2f * phase);
            return (eased - phase) * (strideLength * 0.5f);
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

        public static int DirectionDistance(int from, int to)
        {
            if (from < 0 || from >= OfficeFacingHysteresisRules.DirectionCount)
                throw new ArgumentOutOfRangeException(nameof(from));
            if (to < 0 || to >= OfficeFacingHysteresisRules.DirectionCount)
                throw new ArgumentOutOfRangeException(nameof(to));
            int delta = Math.Abs(from - to) % OfficeFacingHysteresisRules.DirectionCount;
            return Math.Min(delta, OfficeFacingHysteresisRules.DirectionCount - delta);
        }

    }

    public readonly struct OfficeSharedLocomotionFrameResult
    {
        public OfficeSharedLocomotionFrameResult(
            OfficeLocomotionFacingState facingState,
            OfficeLocomotionGaitState gaitState,
            int requestedDirection,
            int motionDirection,
            bool hasRequest,
            bool isMoving,
            float actualSpeed,
            float facingAlignmentDot,
            float facingAngularErrorDegrees,
            bool usedRequestedFacing)
        {
            FacingState = facingState;
            GaitState = gaitState;
            RequestedDirection = requestedDirection;
            MotionDirection = motionDirection;
            HasRequest = hasRequest;
            IsMoving = isMoving;
            ActualSpeed = actualSpeed;
            FacingAlignmentDot = facingAlignmentDot;
            FacingAngularErrorDegrees = facingAngularErrorDegrees;
            UsedRequestedFacing = usedRequestedFacing;
        }

        public OfficeLocomotionFacingState FacingState { get; }
        public OfficeLocomotionGaitState GaitState { get; }
        public int RequestedDirection { get; }
        public int MotionDirection { get; }
        public int DisplayDirection => GaitState.DisplayDirection;
        public OfficeLocomotionPhase Phase => GaitState.Phase;
        public bool HasRequest { get; }
        public bool IsMoving { get; }
        public float ActualSpeed { get; }
        public float FacingAlignmentDot { get; }
        public float FacingAngularErrorDegrees { get; }
        public bool UsedRequestedFacing { get; }
    }

    /// <summary>
    /// Pure shared movement/presentation boundary for player, NPC, autonomy, and contract routes.
    /// Requested heading can rotate a planted actor but can never override an actual moving frame.
    /// </summary>
    public static class OfficeSharedLocomotionRules
    {
        public const float WalkSpeedThreshold = 0.02f;
        public const float MaximumFacingErrorDegrees =
            OfficeLocomotionPresentationRules.MaximumHeldFacingErrorDegrees;
        public const float MinimumFacingAlignmentDot = 0.8616291604f;
        private const float MinimumVectorSquared = 0.0000001f;
        private const float MinimumRootDisplacement = 0.00001f;

        public static OfficeSharedLocomotionFrameResult ResolveFrame(
            OfficeLocomotionFacingState facingState,
            OfficeLocomotionGaitState gaitState,
            OfficeNavPoint requestedDisplacement,
            OfficeNavPoint actualDisplacement,
            float actualTravelDistance,
            float deltaTime,
            bool collisionProjected,
            float strideLength = OfficeLocomotionGaitRules.DefaultStrideLength,
            int frameCount = 6)
        {
            if (actualTravelDistance < 0f || float.IsNaN(actualTravelDistance) ||
                float.IsInfinity(actualTravelDistance))
                throw new ArgumentOutOfRangeException(nameof(actualTravelDistance));
            if (deltaTime < 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (strideLength <= 0f || float.IsNaN(strideLength) || float.IsInfinity(strideLength))
                throw new ArgumentOutOfRangeException(nameof(strideLength));

            bool hasRequest = requestedDisplacement.SqrMagnitude > MinimumVectorSquared;
            float netDisplacement = actualDisplacement.Magnitude;
            float actualSpeed = deltaTime > 0.000001f ? actualTravelDistance / deltaTime : 0f;
            // Any measurable root displacement remains a moving frame, even during the final
            // low-speed deceleration sample. Treating that sample as stopped lets a reversal pivot
            // begin while the body is still translating and breaks the planted-turn contract.
            bool isMoving = netDisplacement > MinimumRootDisplacement &&
                            actualTravelDistance > MinimumRootDisplacement;
            OfficeNavPoint authoritativeMotion = isMoving
                ? actualDisplacement
                : new OfficeNavPoint(0f, 0f);

            OfficeLocomotionFacingResult facing = OfficeLocomotionPresentationRules.ResolveFacing(
                facingState,
                requestedDisplacement,
                authoritativeMotion,
                deltaTime,
                collisionProjected,
                // A translating sprite must use the nearest octant of this frame's actual
                // displacement immediately. Boundary hysteresis is useful for standing look
                // input, but holding an adjacent old direction while the root moves produces the
                // visible sideways/backwards slide that this shared runtime boundary forbids.
                0f,
                0f);
            OfficeLocomotionGaitState gait = OfficeLocomotionGaitRules.Resolve(
                gaitState,
                isMoving ? actualTravelDistance : 0f,
                deltaTime,
                hasRequest,
                facing.State.VisualDirection,
                strideLength,
                frameCount);

            int requestedDirection = OfficeLocomotionPresentationRules.ResolveNearestDirection(
                requestedDisplacement,
                gait.DisplayDirection);
            int motionDirection = OfficeLocomotionPresentationRules.ResolveNearestDirection(
                authoritativeMotion,
                gait.DisplayDirection);
            float alignmentDot = 1f;
            float angularError = 0f;
            if (isMoving)
            {
                OfficeNavPoint displayHeading = DirectionVector(gait.DisplayDirection);
                alignmentDot = OfficeNavPoint.Dot(
                    displayHeading,
                    actualDisplacement.Normalized);
                alignmentDot = Math.Max(-1f, Math.Min(1f, alignmentDot));
                angularError = (float)(Math.Acos(alignmentDot) * 180d / Math.PI);
                int octantError = OfficeLocomotionGaitRules.DirectionDistance(
                    gait.DisplayDirection,
                    motionDirection);
                if (octantError > 1 ||
                    alignmentDot + 0.000001f < MinimumFacingAlignmentDot ||
                    angularError > MaximumFacingErrorDegrees + 0.0001f)
                {
                    throw new InvalidOperationException(
                        $"Moving display direction must follow actual displacement: " +
                        $"display={gait.DisplayDirection}, motion={motionDirection}, " +
                        $"dot={alignmentDot:F6}, error={angularError:F4}.");
                }
            }

            return new OfficeSharedLocomotionFrameResult(
                facing.State,
                gait,
                requestedDirection,
                motionDirection,
                hasRequest,
                isMoving,
                actualSpeed,
                alignmentDot,
                angularError,
                !isMoving && facing.UsedSemanticHeading);
        }

        public static bool RequiresStationaryPivot(
            int displayDirection,
            int requestedDirection,
            OfficeLocomotionPhase phase)
        {
            if (displayDirection < 0 || displayDirection >= OfficeFacingHysteresisRules.DirectionCount)
                throw new ArgumentOutOfRangeException(nameof(displayDirection));
            if (requestedDirection < 0 || requestedDirection >= OfficeFacingHysteresisRules.DirectionCount)
                throw new ArgumentOutOfRangeException(nameof(requestedDirection));
            // Locomotion never inserts a controller-owned stop just to change direction. Actual
            // displacement remains the facing authority, so acceleration can carry a corner or a
            // reversal continuously. Stationary interaction alignment still uses the gait's
            // short Pivot phase through AccumulateStandingFacingRequest; it is not a move gate.
            return false;
        }

        public static bool IsInteractionFacingReady(
            OfficeLocomotionGaitState gaitState,
            int displayDirection,
            int desiredDirection,
            float actualSpeed)
        {
            if (displayDirection < 0 || displayDirection >= OfficeFacingHysteresisRules.DirectionCount)
                throw new ArgumentOutOfRangeException(nameof(displayDirection));
            if (desiredDirection < 0 || desiredDirection >= OfficeFacingHysteresisRules.DirectionCount)
                throw new ArgumentOutOfRangeException(nameof(desiredDirection));
            if (actualSpeed < 0f || float.IsNaN(actualSpeed) || float.IsInfinity(actualSpeed))
                throw new ArgumentOutOfRangeException(nameof(actualSpeed));
            return actualSpeed < WalkSpeedThreshold &&
                   gaitState.Phase == OfficeLocomotionPhase.Idle &&
                   displayDirection == desiredDirection;
        }

        public static OfficeNavPoint DirectionVector(int direction)
        {
            if (direction < 0 || direction >= OfficeFacingHysteresisRules.DirectionCount)
                throw new ArgumentOutOfRangeException(nameof(direction));
            double radians = direction * Math.PI / 4d;
            return new OfficeNavPoint(
                (float)-Math.Sin(radians),
                (float)-Math.Cos(radians));
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
