using System;
using FamilyCompany.Simulation.Navigation;

namespace FamilyCompany.Editor
{
    public sealed class OfficeSharedLocomotionStrictReport
    {
        internal OfficeSharedLocomotionStrictReport(
            int characterDirectionChecks,
            int movingFrames,
            int reverseCases,
            int reverseFacingFrames,
            int movingDuringPivotFrames,
            int cornerCases,
            int unnecessaryCornerStops,
            int blockedPivotCases,
            int collisionSlideCases,
            int lowMagnitudeBoundaryCases,
            int gaitPartitionCases,
            int interactionFacingCases,
            float maximumFacingErrorDegrees,
            float minimumFacingAlignmentDot,
            float maximumGaitClosureError,
            long managedHeapGrowthBytesPerTenThousandSteps)
        {
            CharacterDirectionChecks = characterDirectionChecks;
            MovingFrames = movingFrames;
            ReverseCases = reverseCases;
            ReverseFacingFrames = reverseFacingFrames;
            MovingDuringPivotFrames = movingDuringPivotFrames;
            CornerCases = cornerCases;
            UnnecessaryCornerStops = unnecessaryCornerStops;
            BlockedPivotCases = blockedPivotCases;
            CollisionSlideCases = collisionSlideCases;
            LowMagnitudeBoundaryCases = lowMagnitudeBoundaryCases;
            GaitPartitionCases = gaitPartitionCases;
            InteractionFacingCases = interactionFacingCases;
            MaximumFacingErrorDegrees = maximumFacingErrorDegrees;
            MinimumFacingAlignmentDot = minimumFacingAlignmentDot;
            MaximumGaitClosureError = maximumGaitClosureError;
            ManagedHeapGrowthBytesPerTenThousandSteps = managedHeapGrowthBytesPerTenThousandSteps;
        }

        public int CharacterDirectionChecks { get; }
        public int MovingFrames { get; }
        public int ReverseCases { get; }
        public int ReverseFacingFrames { get; }
        public int MovingDuringPivotFrames { get; }
        public int CornerCases { get; }
        public int UnnecessaryCornerStops { get; }
        public int BlockedPivotCases { get; }
        public int CollisionSlideCases { get; }
        public int LowMagnitudeBoundaryCases { get; }
        public int GaitPartitionCases { get; }
        public int InteractionFacingCases { get; }
        public float MaximumFacingErrorDegrees { get; }
        public float MinimumFacingAlignmentDot { get; }
        public float MaximumGaitClosureError { get; }
        public long ManagedHeapGrowthBytesPerTenThousandSteps { get; }

        public override string ToString()
        {
            return
                $"characters=12 directionChecks={CharacterDirectionChecks} movingFrames={MovingFrames} " +
                $"maxFacingError={MaximumFacingErrorDegrees:F4}deg minDot={MinimumFacingAlignmentDot:F6} " +
                $"reversals={ReverseCases} reverseFacingFrames={ReverseFacingFrames} " +
                $"movingDuringPivot={MovingDuringPivotFrames} corners={CornerCases} " +
                $"unnecessaryCornerStops={UnnecessaryCornerStops} blockedPivots={BlockedPivotCases} " +
                $"collisionSlides={CollisionSlideCases} " +
                $"lowMagnitudeBoundaries={LowMagnitudeBoundaryCases} " +
                $"gaitPartitions={GaitPartitionCases} " +
                $"maxGaitClosureError={MaximumGaitClosureError:F8} " +
                $"interactionFacing={InteractionFacingCases} " +
                $"managedHeapGrowth10k={ManagedHeapGrowthBytesPerTenThousandSteps}";
        }
    }

    public static class OfficeSharedLocomotionStrictValidation
    {
        private static readonly string[] MemberIds =
        {
            "player", "older_sister", "father", "mother",
            "kim_seoa", "lee_jian", "choi_iseo", "jung_arin",
            "park_haeun", "han_sua", "oh_jiwoo", "yoon_chaea"
        };

        private struct HarnessState
        {
            public OfficeLocomotionFacingState Facing;
            public OfficeLocomotionGaitState Gait;

            public static HarnessState Initial(int direction)
            {
                return new HarnessState
                {
                    Facing = OfficeLocomotionFacingState.Initial(direction),
                    Gait = OfficeLocomotionGaitState.Initial(direction)
                };
            }
        }

        private sealed class Metrics
        {
            public int CharacterDirectionChecks;
            public int MovingFrames;
            public int ReverseCases;
            public int ReverseFacingFrames;
            public int MovingDuringPivotFrames;
            public int CornerCases;
            public int UnnecessaryCornerStops;
            public int BlockedPivotCases;
            public int CollisionSlideCases;
            public int LowMagnitudeBoundaryCases;
            public int GaitPartitionCases;
            public int InteractionFacingCases;
            public float MaximumFacingErrorDegrees;
            public float MinimumFacingAlignmentDot = 1f;
            public float MaximumGaitClosureError;
        }

        public static OfficeSharedLocomotionStrictReport Run()
        {
            var metrics = new Metrics();
            ValidateEveryCharacterAndDirection(metrics);
            ValidateReversals(metrics);
            ValidateCorners(metrics);
            ValidateBlockedTurnInPlace(metrics);
            ValidateCollisionSlides(metrics);
            ValidateLowSpeedDecelerationBoundary(metrics);
            ValidateDistanceGait(metrics);
            ValidateInteractionFacingHook(metrics);
            long managedHeapGrowth = MeasurePureRuleManagedHeapGrowth();

            Require(metrics.ReverseFacingFrames == 0, "reverse-facing moving frames must be zero");
            Require(metrics.MovingDuringPivotFrames == 0, "moving during a planted pivot must be zero");
            Require(metrics.UnnecessaryCornerStops == 0, "45/90 degree steering must not stop unnecessarily");
            Require(metrics.MaximumFacingErrorDegrees <=
                    OfficeSharedLocomotionRules.MaximumFacingErrorDegrees + 0.0001f,
                "moving facing error exceeded 22.5 degrees");
            Require(metrics.MinimumFacingAlignmentDot + 0.000001f >=
                    OfficeSharedLocomotionRules.MinimumFacingAlignmentDot,
                "moving facing dot fell below cos(22.5 degrees)");
            Require(managedHeapGrowth == 0,
                "pure locomotion rules grew the managed heap during steady-state stepping");

            return new OfficeSharedLocomotionStrictReport(
                metrics.CharacterDirectionChecks,
                metrics.MovingFrames,
                metrics.ReverseCases,
                metrics.ReverseFacingFrames,
                metrics.MovingDuringPivotFrames,
                metrics.CornerCases,
                metrics.UnnecessaryCornerStops,
                metrics.BlockedPivotCases,
                metrics.CollisionSlideCases,
                metrics.LowMagnitudeBoundaryCases,
                metrics.GaitPartitionCases,
                metrics.InteractionFacingCases,
                metrics.MaximumFacingErrorDegrees,
                metrics.MinimumFacingAlignmentDot,
                metrics.MaximumGaitClosureError,
                managedHeapGrowth);
        }

        private static void ValidateEveryCharacterAndDirection(Metrics metrics)
        {
            const float edgeOffset = 22.499f;
            for (var member = 0; member < MemberIds.Length; member++)
            for (var direction = 0; direction < OfficeFacingHysteresisRules.DirectionCount; direction++)
            {
                for (var sample = -1; sample <= 1; sample++)
                {
                    float angle = direction * 45f + edgeOffset * sample;
                    OfficeNavPoint heading = HeadingFromSouthAngle(angle);
                    HarnessState state = HarnessState.Initial((direction + 4) % 8);
                    OfficeSharedLocomotionFrameResult frame = Step(
                        ref state,
                        heading * 0.05f,
                        heading * 0.08f,
                        0.05f);
                    Require(frame.IsMoving, MemberIds[member] + " open-space sample did not move");
                    Require(frame.DisplayDirection == frame.MotionDirection,
                        MemberIds[member] + " displayed a non-motion octant");
                    RecordMovingFrame(frame, metrics);
                    metrics.CharacterDirectionChecks++;
                }
            }
        }

        private static void ValidateReversals(Metrics metrics)
        {
            const float deltaTime = 1f / 60f;
            const float speed = 1.65f;
            for (var member = 0; member < MemberIds.Length; member++)
            for (var direction = 0; direction < 8; direction++)
            {
                OfficeNavPoint forward = OfficeSharedLocomotionRules.DirectionVector(direction);
                int reverseDirection = (direction + 4) % 8;
                OfficeNavPoint reverse = OfficeSharedLocomotionRules.DirectionVector(reverseDirection);
                HarnessState state = HarnessState.Initial(direction);
                OfficeNavPoint velocity = forward * speed;
                Step(ref state, forward * deltaTime, forward * (speed * deltaTime), deltaTime);
                bool observedPivot = false;
                bool resumedReverse = false;
                int previousPivotDirection = state.Gait.DisplayDirection;

                for (var frameIndex = 0; frameIndex < 180; frameIndex++)
                {
                    bool hold = OfficeSharedLocomotionRules.RequiresStationaryPivot(
                        state.Gait.DisplayDirection,
                        reverseDirection,
                        state.Gait.Phase);
                    OfficeNavPoint targetVelocity = hold
                        ? new OfficeNavPoint(0f, 0f)
                        : reverse * speed;
                    OfficeMotionIntegrationResult motion =
                        OfficeNavigationMotionIntegrator.IntegrateVelocity(
                            velocity,
                            targetVelocity,
                            7.5f,
                            deltaTime);
                    velocity = motion.Velocity;
                    OfficeSharedLocomotionFrameResult frame = Step(
                        ref state,
                        reverse * deltaTime,
                        motion.Displacement,
                        deltaTime);
                    if (frame.IsMoving)
                    {
                        RecordMovingFrame(frame, metrics);
                        if (frame.FacingAlignmentDot + 0.000001f <
                            OfficeSharedLocomotionRules.MinimumFacingAlignmentDot)
                            metrics.ReverseFacingFrames++;
                    }
                    if (frame.Phase == OfficeLocomotionPhase.Pivot)
                    {
                        observedPivot = true;
                        if (frame.IsMoving) metrics.MovingDuringPivotFrames++;
                        int step = OfficeLocomotionGaitRules.DirectionDistance(
                            previousPivotDirection,
                            frame.DisplayDirection);
                        Require(step <= 1, MemberIds[member] + " pivot skipped an adjacent octant");
                        previousPivotDirection = frame.DisplayDirection;
                    }
                    if (frame.IsMoving && frame.DisplayDirection == reverseDirection)
                        resumedReverse = true;
                    if (resumedReverse && frameIndex > 20) break;
                }

                Require(observedPivot, MemberIds[member] + " reversal never entered pivot");
                Require(resumedReverse, MemberIds[member] + " reversal never resumed in the new direction");
                metrics.ReverseCases++;
            }
        }

        private static void ValidateCorners(Metrics metrics)
        {
            const float deltaTime = 1f / 60f;
            const float speed = 1.65f;
            for (var turn = -1; turn <= 1; turn += 2)
            for (var octants = 1; octants <= 2; octants++)
            {
                int fromDirection = 0;
                int targetDirection = (fromDirection + turn * octants + 8) % 8;
                OfficeNavPoint from = OfficeSharedLocomotionRules.DirectionVector(fromDirection);
                OfficeNavPoint target = OfficeSharedLocomotionRules.DirectionVector(targetDirection);
                HarnessState state = HarnessState.Initial(fromDirection);
                OfficeNavPoint velocity = from * speed;
                Step(ref state, from * deltaTime, from * (speed * deltaTime), deltaTime);
                for (var frameIndex = 0; frameIndex < 36; frameIndex++)
                {
                    OfficeMotionIntegrationResult motion =
                        OfficeNavigationMotionIntegrator.IntegrateVelocity(
                            velocity,
                            target * speed,
                            7.5f,
                            deltaTime);
                    velocity = motion.Velocity;
                    OfficeSharedLocomotionFrameResult frame = Step(
                        ref state,
                        target * deltaTime,
                        motion.Displacement,
                        deltaTime);
                    if (!frame.IsMoving)
                    {
                        metrics.UnnecessaryCornerStops++;
                        continue;
                    }
                    RecordMovingFrame(frame, metrics);
                    Require(frame.Phase != OfficeLocomotionPhase.Pivot,
                        "a moving 45/90 degree corner entered a fake presentation pivot");
                }
                metrics.CornerCases++;
            }
        }

        private static void ValidateBlockedTurnInPlace(Metrics metrics)
        {
            const float deltaTime = 1f / 60f;
            for (var member = 0; member < MemberIds.Length; member++)
            for (var targetDirection = 0; targetDirection < 8; targetDirection++)
            {
                int initialDirection = (targetDirection + 4) % 8;
                OfficeNavPoint requested = OfficeSharedLocomotionRules.DirectionVector(targetDirection);
                HarnessState state = HarnessState.Initial(initialDirection);
                float gaitDistance = state.Gait.AccumulatedDistance;
                int priorDirection = initialDirection;
                bool observedPivot = false;
                bool ready = false;
                for (var frameIndex = 0; frameIndex < 60; frameIndex++)
                {
                    OfficeSharedLocomotionFrameResult frame = Step(
                        ref state,
                        requested * deltaTime,
                        new OfficeNavPoint(0f, 0f),
                        deltaTime,
                        true);
                    Require(!frame.IsMoving, MemberIds[member] + " blocked pivot moved the root");
                    Require(frame.Phase != OfficeLocomotionPhase.Walk &&
                            frame.Phase != OfficeLocomotionPhase.StartStep,
                        MemberIds[member] + " blocked input played a walking phase");
                    Require(Math.Abs(frame.GaitState.AccumulatedDistance - gaitDistance) <= 0.000001f,
                        MemberIds[member] + " blocked pivot advanced walk distance");
                    int step = OfficeLocomotionGaitRules.DirectionDistance(
                        priorDirection,
                        frame.DisplayDirection);
                    Require(step <= 1, MemberIds[member] + " blocked pivot skipped an octant");
                    priorDirection = frame.DisplayDirection;
                    observedPivot |= frame.Phase == OfficeLocomotionPhase.Pivot;
                    ready = OfficeSharedLocomotionRules.IsInteractionFacingReady(
                        frame.GaitState,
                        frame.DisplayDirection,
                        targetDirection,
                        frame.ActualSpeed);
                    if (ready) break;
                }
                Require(observedPivot && ready,
                    MemberIds[member] + " blocked turn did not finish the requested facing");
                metrics.BlockedPivotCases++;
            }
        }

        private static void ValidateCollisionSlides(Metrics metrics)
        {
            for (var member = 0; member < MemberIds.Length; member++)
            for (var actualDirection = 0; actualDirection < 8; actualDirection++)
            {
                int requestedDirection = (actualDirection + 2) % 8;
                OfficeNavPoint requested = OfficeSharedLocomotionRules.DirectionVector(requestedDirection);
                OfficeNavPoint actual = OfficeSharedLocomotionRules.DirectionVector(actualDirection);
                HarnessState state = HarnessState.Initial(requestedDirection);
                OfficeSharedLocomotionFrameResult frame = Step(
                    ref state,
                    requested * 0.05f,
                    actual * 0.06f,
                    0.05f,
                    true);
                Require(frame.DisplayDirection == actualDirection,
                    MemberIds[member] + " collision slide followed requested rather than actual motion");
                Require(!frame.UsedRequestedFacing,
                    MemberIds[member] + " collision slide retained semantic facing");
                RecordMovingFrame(frame, metrics);
                metrics.CollisionSlideCases++;
            }
        }

        private static void ValidateLowSpeedDecelerationBoundary(Metrics metrics)
        {
            const float deltaTime = 1f / 60f;
            OfficeNavPoint forward = OfficeSharedLocomotionRules.DirectionVector(0);
            OfficeNavPoint reverse = OfficeSharedLocomotionRules.DirectionVector(4);
            HarnessState state = HarnessState.Initial(0);
            OfficeSharedLocomotionFrameResult frame = Step(
                ref state,
                reverse * deltaTime,
                forward * 0.00002f,
                deltaTime);
            Require(frame.IsMoving,
                "a measurable final deceleration displacement was treated as stopped");
            Require(frame.DisplayDirection == 0,
                "a final deceleration displacement began the reverse pivot before stopping");
            Require(frame.Phase != OfficeLocomotionPhase.Pivot,
                "a planted pivot began while measurable root displacement remained");
            RecordMovingFrame(frame, metrics);
            metrics.LowMagnitudeBoundaryCases++;

            frame = Step(
                ref state,
                reverse * deltaTime,
                new OfficeNavPoint(0f, 0f),
                deltaTime);
            Require(!frame.IsMoving && frame.Phase == OfficeLocomotionPhase.Pivot,
                "the reverse pivot did not begin on the first fully stopped frame");

            OfficeNavPoint tinyCorner = OfficeSharedLocomotionRules.DirectionVector(3);
            HarnessState cornerState = HarnessState.Initial(2);
            frame = Step(
                ref cornerState,
                tinyCorner * deltaTime,
                tinyCorner * 0.00002f,
                deltaTime);
            Require(frame.IsMoving && frame.DisplayDirection == 3,
                "a small measurable corner displacement retained the legacy direction dead-zone");
            RecordMovingFrame(frame, metrics);
            metrics.LowMagnitudeBoundaryCases++;
        }

        private static void ValidateDistanceGait(Metrics metrics)
        {
            const float stride = OfficeLocomotionGaitRules.DefaultStrideLength;
            float referencePhase = -1f;
            int[] frameRates = { 30, 60, 120 };
            float[] speeds = { 0.35f, 1.65f, 3.2f };
            float[] timeScales = { 1f, 2f, 4f };
            for (var rateIndex = 0; rateIndex < frameRates.Length; rateIndex++)
            for (var speedIndex = 0; speedIndex < speeds.Length; speedIndex++)
            for (var scaleIndex = 0; scaleIndex < timeScales.Length; scaleIndex++)
            {
                HarnessState state = HarnessState.Initial(0);
                OfficeNavPoint heading = OfficeSharedLocomotionRules.DirectionVector(0);
                float remaining = stride * 5f;
                float deltaTime = 1f / frameRates[rateIndex];
                float distancePerFrame = speeds[speedIndex] * timeScales[scaleIndex] * deltaTime;
                while (remaining > 0.000001f)
                {
                    float distance = Math.Min(remaining, distancePerFrame);
                    Step(ref state, heading * deltaTime, heading * distance, deltaTime);
                    remaining -= distance;
                }
                float phase = OfficeLocomotionGaitRules.Phase01(
                    state.Gait.AccumulatedDistance,
                    stride);
                float closureError = Math.Min(phase, 1f - phase);
                metrics.MaximumGaitClosureError = Math.Max(
                    metrics.MaximumGaitClosureError,
                    closureError);
                if (referencePhase < 0f) referencePhase = phase;
                float partitionError = Math.Abs(referencePhase - phase);
                partitionError = Math.Min(partitionError, 1f - partitionError);
                Require(partitionError <= 0.00001f,
                    "distance gait changed across speed, time scale, or frame rate");
                metrics.GaitPartitionCases++;
            }

            Require(metrics.MaximumGaitClosureError <= 0.00001f,
                "whole-stride travel did not close the foot phase");

            HarnessState stopped = HarnessState.Initial(0);
            OfficeNavPoint south = OfficeSharedLocomotionRules.DirectionVector(0);
            Step(ref stopped, south * 0.1f, south * 0.32f, 0.1f);
            float beforeStop = stopped.Gait.AccumulatedDistance;
            Step(ref stopped, new OfficeNavPoint(0f, 0f), new OfficeNavPoint(0f, 0f), 0.1f);
            Require(Math.Abs(stopped.Gait.AccumulatedDistance - beforeStop) <= 0.000001f,
                "stopped frame advanced gait distance");
            OfficeNavPoint north = OfficeSharedLocomotionRules.DirectionVector(4);
            Step(ref stopped, north * 0.05f, new OfficeNavPoint(0f, 0f), 0.05f);
            Require(Math.Abs(stopped.Gait.AccumulatedDistance - beforeStop) <= 0.000001f,
                "pivot frame advanced gait distance");

            // A render frame may contain multiple fixed simulation substeps around a corner.
            // Facing follows their net displacement, while foot phase follows the full travelled
            // arc rather than the shorter length of the summed vector.
            HarnessState aggregated = HarnessState.Initial(0);
            OfficeNavPoint firstSegment = OfficeSharedLocomotionRules.DirectionVector(0) * 0.03f;
            OfficeNavPoint secondSegment = OfficeSharedLocomotionRules.DirectionVector(2) * 0.04f;
            OfficeNavPoint netDisplacement = firstSegment + secondSegment;
            OfficeSharedLocomotionFrameResult aggregatedFrame =
                OfficeSharedLocomotionRules.ResolveFrame(
                    aggregated.Facing,
                    aggregated.Gait,
                    netDisplacement,
                    netDisplacement,
                    0.07f,
                    0.05f,
                    false);
            Require(Math.Abs(aggregatedFrame.GaitState.AccumulatedDistance - 0.07f) <= 0.000001f,
                "substep aggregation shortened actual gait travel at a corner");
            Require(aggregatedFrame.DisplayDirection == aggregatedFrame.MotionDirection,
                "substep aggregation did not face the net actual displacement");
        }

        private static void ValidateInteractionFacingHook(Metrics metrics)
        {
            HarnessState state = HarnessState.Initial(0);
            OfficeNavPoint desired = OfficeSharedLocomotionRules.DirectionVector(4);
            bool sawNotReady = false;
            for (var frameIndex = 0; frameIndex < 60; frameIndex++)
            {
                OfficeSharedLocomotionFrameResult frame = Step(
                    ref state,
                    desired * (1f / 60f),
                    new OfficeNavPoint(0f, 0f),
                    1f / 60f);
                bool ready = OfficeSharedLocomotionRules.IsInteractionFacingReady(
                    frame.GaitState,
                    frame.DisplayDirection,
                    4,
                    frame.ActualSpeed);
                if (frame.Phase == OfficeLocomotionPhase.Pivot)
                {
                    sawNotReady = true;
                    Require(!ready, "interaction became ready before pivot completion");
                }
                if (!ready) continue;
                Require(frame.DisplayDirection == 4 && frame.Phase == OfficeLocomotionPhase.Idle,
                    "interaction readiness did not require planted final facing");
                metrics.InteractionFacingCases++;
                break;
            }
            Require(sawNotReady && metrics.InteractionFacingCases == 1,
                "interaction facing hook did not complete after a planted pivot");
        }

        private static long MeasurePureRuleManagedHeapGrowth()
        {
            HarnessState state = HarnessState.Initial(0);
            OfficeNavPoint request = OfficeSharedLocomotionRules.DirectionVector(0) * 0.016f;
            OfficeNavPoint actual = OfficeSharedLocomotionRules.DirectionVector(0) * 0.0264f;
            for (var index = 0; index < 256; index++)
                Step(ref state, request, actual, 0.016f);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long before = GC.GetTotalMemory(false);
            for (var index = 0; index < 10000; index++)
                Step(ref state, request, actual, 0.016f);
            return Math.Max(0L, GC.GetTotalMemory(false) - before);
        }

        private static OfficeSharedLocomotionFrameResult Step(
            ref HarnessState state,
            OfficeNavPoint requestedDisplacement,
            OfficeNavPoint actualDisplacement,
            float deltaTime,
            bool collisionProjected = false)
        {
            OfficeSharedLocomotionFrameResult result = OfficeSharedLocomotionRules.ResolveFrame(
                state.Facing,
                state.Gait,
                requestedDisplacement,
                actualDisplacement,
                actualDisplacement.Magnitude,
                deltaTime,
                collisionProjected);
            state.Facing = result.FacingState;
            state.Gait = result.GaitState;
            return result;
        }

        private static void RecordMovingFrame(
            OfficeSharedLocomotionFrameResult frame,
            Metrics metrics)
        {
            Require(frame.IsMoving, "moving-frame metric received a stopped frame");
            metrics.MovingFrames++;
            metrics.MaximumFacingErrorDegrees = Math.Max(
                metrics.MaximumFacingErrorDegrees,
                frame.FacingAngularErrorDegrees);
            metrics.MinimumFacingAlignmentDot = Math.Min(
                metrics.MinimumFacingAlignmentDot,
                frame.FacingAlignmentDot);
        }

        private static OfficeNavPoint HeadingFromSouthAngle(float angleDegrees)
        {
            double radians = angleDegrees * Math.PI / 180d;
            return new OfficeNavPoint(
                (float)-Math.Sin(radians),
                (float)-Math.Cos(radians));
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
