using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using FamilyCompany.Simulation.Core;

namespace FamilyCompany.Simulation.OfficeWorkActions
{
    public enum OfficeWorkMicroAction
    {
        None = 0,
        Typing = 1,
        Mouse = 2,
        Drink = 3,
        BriefIdle = 4
    }

    [Flags]
    public enum OfficeWorkMicroActionAvailability
    {
        None = 0,
        Typing = 1 << 0,
        Mouse = 1 << 1,
        Drink = 1 << 2,
        BriefIdle = 1 << 3,
        All = Typing | Mouse | Drink | BriefIdle
    }

    public enum OfficeWorkMicroActionContext
    {
        SitDown = 0,
        SeatedWork = 1,
        StandUp = 2,
        Meeting = 3,
        Printing = 4,
        Moving = 5,
        OutsideSchedule = 6
    }

    public enum OfficeWorkMicroActionTransitionKind
    {
        ActionStarted = 0,
        ActionCompleted = 1,
        StopRequested = 2,
        StandHandoffReady = 3
    }

    public sealed class OfficeWorkMicroActionTransition
    {
        internal OfficeWorkMicroActionTransition(
            long atMilliseconds,
            OfficeWorkMicroActionTransitionKind kind,
            OfficeWorkMicroAction action,
            OfficeWorkMicroActionContext context,
            int actionSequence)
        {
            AtMilliseconds = atMilliseconds;
            Kind = kind;
            Action = action;
            Context = context;
            ActionSequence = actionSequence;
        }

        public long AtMilliseconds { get; }
        public OfficeWorkMicroActionTransitionKind Kind { get; }
        public OfficeWorkMicroAction Action { get; }
        public OfficeWorkMicroActionContext Context { get; }
        public int ActionSequence { get; }
    }

    public static class OfficeWorkMicroActionAvailabilityRules
    {
        public const int DirectionCount = 8;

        public static OfficeWorkMicroActionAvailability Resolve(
            bool typingUsable,
            bool mouseUsable,
            bool drinkUsable,
            bool briefIdleUsable)
        {
            var value = OfficeWorkMicroActionAvailability.None;
            if (typingUsable) value |= OfficeWorkMicroActionAvailability.Typing;
            if (mouseUsable) value |= OfficeWorkMicroActionAvailability.Mouse;
            if (drinkUsable) value |= OfficeWorkMicroActionAvailability.Drink;
            if (briefIdleUsable) value |= OfficeWorkMicroActionAvailability.BriefIdle;
            return value;
        }

        public static bool IsFrameChannelUsable(
            int totalFrameCount,
            bool containsMissingFrame,
            int millisecondsPerFrame)
        {
            return totalFrameCount >= DirectionCount &&
                   totalFrameCount % DirectionCount == 0 &&
                   !containsMissingFrame &&
                   millisecondsPerFrame > 0;
        }

        public static bool ShouldUseExistingWorkLoop(OfficeWorkMicroActionAvailability availability)
        {
            return availability == OfficeWorkMicroActionAvailability.None;
        }

        public static bool Includes(
            OfficeWorkMicroActionAvailability availability,
            OfficeWorkMicroAction action)
        {
            var flag = FlagFor(action);
            return flag != OfficeWorkMicroActionAvailability.None && (availability & flag) == flag;
        }

        public static OfficeWorkMicroActionAvailability FlagFor(OfficeWorkMicroAction action)
        {
            switch (action)
            {
                case OfficeWorkMicroAction.Typing:
                    return OfficeWorkMicroActionAvailability.Typing;
                case OfficeWorkMicroAction.Mouse:
                    return OfficeWorkMicroActionAvailability.Mouse;
                case OfficeWorkMicroAction.Drink:
                    return OfficeWorkMicroActionAvailability.Drink;
                case OfficeWorkMicroAction.BriefIdle:
                    return OfficeWorkMicroActionAvailability.BriefIdle;
                default:
                    return OfficeWorkMicroActionAvailability.None;
            }
        }
    }

    /// <summary>
    /// Deterministic visual-only sequence for seated office work. It owns no gameplay state and
    /// advances from integer milliseconds so frame partitioning cannot change the action order.
    /// </summary>
    public sealed class OfficeWorkMicroActionStateMachine
    {
        public const long DefaultDrinkCooldownMilliseconds = 240_000L;

        private const int TypingWeight = 72;
        private const int MouseWeight = 18;
        private const int DrinkWeight = 3;
        private const int BriefIdleWeight = 7;
        private const long FirstMouseMinimumMilliseconds = 45_000L;
        private const long FirstMouseMaximumMilliseconds = 90_000L;
        private const long MouseIntervalMinimumMilliseconds = 55_000L;
        private const long MouseIntervalMaximumMilliseconds = 110_000L;
        private const long FirstDrinkMinimumMilliseconds = 300_000L;
        private const long FirstDrinkMaximumMilliseconds = 480_000L;
        private const long DrinkIntervalMinimumMilliseconds = 330_000L;
        private const long DrinkIntervalMaximumMilliseconds = 540_000L;

        private readonly int _worldSeed;
        private readonly string _memberId;
        private readonly long _sessionStartedMinute;
        private readonly OfficeWorkMicroActionAvailability _availability;
        private long _processedMilliseconds;
        private long _actionStartedMilliseconds;
        private long _actionEndsMilliseconds;
        private long _drinkCooldownUntilMilliseconds;
        private long _mouseDeadlineMilliseconds;
        private long _drinkDeadlineMilliseconds;
        private int _actionSequence;
        private int _completedDrinkCount;
        private OfficeWorkMicroAction _currentAction;
        private OfficeWorkMicroAction _lastCompletedAction;
        private int _sameActionStreak;
        private bool _stopRequested;
        private bool _standHandoffReady;
        private bool _standHandoffReadyEmitted;

        public OfficeWorkMicroActionStateMachine(
            int worldSeed,
            string memberId,
            long sessionStartedMinute,
            OfficeWorkMicroActionAvailability availability)
        {
            if (string.IsNullOrWhiteSpace(memberId))
                throw new ArgumentException("Member ID is required.", nameof(memberId));
            if (sessionStartedMinute < 0)
                throw new ArgumentOutOfRangeException(nameof(sessionStartedMinute));
            if ((availability & ~OfficeWorkMicroActionAvailability.All) != 0)
                throw new ArgumentOutOfRangeException(nameof(availability));

            _worldSeed = worldSeed;
            _memberId = memberId.Trim();
            _sessionStartedMinute = sessionStartedMinute;
            _availability = availability;
            _mouseDeadlineMilliseconds = ResolveInterval(
                "first-mouse",
                0,
                FirstMouseMinimumMilliseconds,
                FirstMouseMaximumMilliseconds);
            _drinkDeadlineMilliseconds = ResolveInterval(
                "first-drink",
                0,
                FirstDrinkMinimumMilliseconds,
                FirstDrinkMaximumMilliseconds);
        }

        public int WorldSeed => _worldSeed;
        public string MemberId => _memberId;
        public long SessionStartedMinute => _sessionStartedMinute;
        public OfficeWorkMicroActionAvailability Availability => _availability;
        public OfficeWorkMicroAction CurrentAction => _currentAction;
        public long ProcessedMilliseconds => _processedMilliseconds;
        public long ActionStartedMilliseconds => _actionStartedMilliseconds;
        public long ActionEndsMilliseconds => _actionEndsMilliseconds;
        public long CurrentActionElapsedMilliseconds => _currentAction == OfficeWorkMicroAction.None
            ? 0L
            : Math.Max(0L, _processedMilliseconds - _actionStartedMilliseconds);
        public long DrinkCooldownUntilMilliseconds => _drinkCooldownUntilMilliseconds;
        public int ActionSequence => _actionSequence;
        public int CompletedDrinkCount => _completedDrinkCount;
        public bool IsStopRequested => _stopRequested;
        public bool IsStandHandoffReady => _standHandoffReady;

        public IReadOnlyList<OfficeWorkMicroActionTransition> AdvanceTo(
            long targetMilliseconds,
            OfficeWorkMicroActionContext context)
        {
            if (targetMilliseconds < _processedMilliseconds)
                throw new InvalidOperationException("Office work micro-action time cannot move backwards.");
            if (!IsValidContext(context))
                throw new ArgumentOutOfRangeException(nameof(context));

            var transitions = new List<OfficeWorkMicroActionTransition>();
            if (context != OfficeWorkMicroActionContext.SeatedWork)
                RequestStop(context, transitions);

            while (true)
            {
                if (_currentAction == OfficeWorkMicroAction.None)
                {
                    if (_stopRequested || context != OfficeWorkMicroActionContext.SeatedWork)
                    {
                        MarkStandHandoffReady(context, transitions);
                        _processedMilliseconds = targetMilliseconds;
                        break;
                    }

                    if (!TryStartNextAction(_processedMilliseconds, context, transitions))
                    {
                        _processedMilliseconds = targetMilliseconds;
                        break;
                    }
                }

                if (_actionEndsMilliseconds > targetMilliseconds)
                {
                    _processedMilliseconds = targetMilliseconds;
                    break;
                }

                var completedAt = _actionEndsMilliseconds;
                _processedMilliseconds = completedAt;
                CompleteCurrentAction(completedAt, context, transitions);
            }

            return new ReadOnlyCollection<OfficeWorkMicroActionTransition>(transitions);
        }

        private void RequestStop(
            OfficeWorkMicroActionContext context,
            ICollection<OfficeWorkMicroActionTransition> transitions)
        {
            if (_stopRequested) return;
            _stopRequested = true;
            transitions.Add(new OfficeWorkMicroActionTransition(
                _processedMilliseconds,
                OfficeWorkMicroActionTransitionKind.StopRequested,
                _currentAction,
                context,
                _actionSequence));
        }

        private void MarkStandHandoffReady(
            OfficeWorkMicroActionContext context,
            ICollection<OfficeWorkMicroActionTransition> transitions)
        {
            _standHandoffReady = true;
            if (_standHandoffReadyEmitted) return;
            _standHandoffReadyEmitted = true;
            transitions.Add(new OfficeWorkMicroActionTransition(
                _processedMilliseconds,
                OfficeWorkMicroActionTransitionKind.StandHandoffReady,
                OfficeWorkMicroAction.None,
                context,
                _actionSequence));
        }

        private bool TryStartNextAction(
            long atMilliseconds,
            OfficeWorkMicroActionContext context,
            ICollection<OfficeWorkMicroActionTransition> transitions)
        {
            var action = ChooseNextAction(atMilliseconds);
            if (action == OfficeWorkMicroAction.None) return false;

            var sequence = _actionSequence;
            var duration = ResolveActionDuration(action, sequence);
            _actionSequence++;
            _currentAction = action;
            _actionStartedMilliseconds = atMilliseconds;
            _actionEndsMilliseconds = checked(atMilliseconds + duration);
            transitions.Add(new OfficeWorkMicroActionTransition(
                atMilliseconds,
                OfficeWorkMicroActionTransitionKind.ActionStarted,
                action,
                context,
                sequence));
            return true;
        }

        private void CompleteCurrentAction(
            long atMilliseconds,
            OfficeWorkMicroActionContext context,
            ICollection<OfficeWorkMicroActionTransition> transitions)
        {
            var completed = _currentAction;
            var completedSequence = _actionSequence - 1;
            transitions.Add(new OfficeWorkMicroActionTransition(
                atMilliseconds,
                OfficeWorkMicroActionTransitionKind.ActionCompleted,
                completed,
                context,
                completedSequence));

            if (_lastCompletedAction == completed)
                _sameActionStreak++;
            else
            {
                _lastCompletedAction = completed;
                _sameActionStreak = 1;
            }

            if (completed == OfficeWorkMicroAction.Mouse)
            {
                _mouseDeadlineMilliseconds = checked(atMilliseconds + ResolveInterval(
                    "mouse-deadline",
                    completedSequence,
                    MouseIntervalMinimumMilliseconds,
                    MouseIntervalMaximumMilliseconds));
            }
            else if (completed == OfficeWorkMicroAction.Drink)
            {
                _completedDrinkCount++;
                _drinkCooldownUntilMilliseconds = checked(atMilliseconds + DefaultDrinkCooldownMilliseconds);
                _drinkDeadlineMilliseconds = checked(atMilliseconds + ResolveInterval(
                    "drink-deadline",
                    completedSequence,
                    DrinkIntervalMinimumMilliseconds,
                    DrinkIntervalMaximumMilliseconds));
            }

            _currentAction = OfficeWorkMicroAction.None;
            _actionStartedMilliseconds = atMilliseconds;
            _actionEndsMilliseconds = atMilliseconds;
            if (_stopRequested) MarkStandHandoffReady(context, transitions);
        }

        private OfficeWorkMicroAction ChooseNextAction(long atMilliseconds)
        {
            if (_availability == OfficeWorkMicroActionAvailability.None)
                return OfficeWorkMicroAction.None;
            if (_actionSequence == 0 && Includes(OfficeWorkMicroAction.Typing))
                return OfficeWorkMicroAction.Typing;
            if (Includes(OfficeWorkMicroAction.Drink) &&
                atMilliseconds >= _drinkDeadlineMilliseconds &&
                atMilliseconds >= _drinkCooldownUntilMilliseconds)
                return OfficeWorkMicroAction.Drink;
            if (Includes(OfficeWorkMicroAction.Mouse) && atMilliseconds >= _mouseDeadlineMilliseconds)
                return OfficeWorkMicroAction.Mouse;

            var candidates = new List<WeightedAction>(4);
            AddCandidate(candidates, OfficeWorkMicroAction.Typing, TypingWeight, atMilliseconds);
            AddCandidate(candidates, OfficeWorkMicroAction.Mouse, MouseWeight, atMilliseconds);
            AddCandidate(candidates, OfficeWorkMicroAction.Drink, DrinkWeight, atMilliseconds);
            AddCandidate(candidates, OfficeWorkMicroAction.BriefIdle, BriefIdleWeight, atMilliseconds);
            if (candidates.Count == 0) return OfficeWorkMicroAction.None;

            if (_sameActionStreak >= 2 && candidates.Count > 1)
                candidates.RemoveAll(item => item.Action == _lastCompletedAction);

            var totalWeight = 0;
            foreach (var item in candidates) totalWeight += item.Weight;
            var value = StableRandom.StableRandomInt(Key("choice", _actionSequence), totalWeight);
            foreach (var item in candidates)
            {
                if (value < item.Weight) return item.Action;
                value -= item.Weight;
            }

            throw new InvalidOperationException("Weighted office work action selection fell through.");
        }

        private void AddCandidate(
            ICollection<WeightedAction> candidates,
            OfficeWorkMicroAction action,
            int weight,
            long atMilliseconds)
        {
            if (!Includes(action)) return;
            if (action == OfficeWorkMicroAction.Drink && atMilliseconds < _drinkCooldownUntilMilliseconds) return;
            candidates.Add(new WeightedAction(action, weight));
        }

        private bool Includes(OfficeWorkMicroAction action)
        {
            return OfficeWorkMicroActionAvailabilityRules.Includes(_availability, action);
        }

        private long ResolveActionDuration(OfficeWorkMicroAction action, int sequence)
        {
            switch (action)
            {
                case OfficeWorkMicroAction.Typing:
                    return ResolveInterval("duration-typing", sequence, 3_400L, 8_200L);
                case OfficeWorkMicroAction.Mouse:
                    return ResolveInterval("duration-mouse", sequence, 1_200L, 3_000L);
                case OfficeWorkMicroAction.Drink:
                    return ResolveInterval("duration-drink", sequence, 2_300L, 4_100L);
                case OfficeWorkMicroAction.BriefIdle:
                    return ResolveInterval("duration-idle", sequence, 900L, 2_200L);
                default:
                    throw new ArgumentOutOfRangeException(nameof(action));
            }
        }

        private long ResolveInterval(string purpose, int sequence, long minimum, long maximum)
        {
            if (minimum < 0 || maximum < minimum || maximum - minimum >= int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(maximum));
            var range = checked((int)(maximum - minimum + 1L));
            return minimum + StableRandom.StableRandomInt(Key(purpose, sequence), range);
        }

        private string Key(string purpose, int sequence)
        {
            return $"office-work-micro-action-v1:{_worldSeed}:{_memberId}:{_sessionStartedMinute}:{purpose}:{sequence}";
        }

        private static bool IsValidContext(OfficeWorkMicroActionContext context)
        {
            return context >= OfficeWorkMicroActionContext.SitDown &&
                   context <= OfficeWorkMicroActionContext.OutsideSchedule;
        }

        private readonly struct WeightedAction
        {
            public WeightedAction(OfficeWorkMicroAction action, int weight)
            {
                Action = action;
                Weight = weight;
            }

            public OfficeWorkMicroAction Action { get; }
            public int Weight { get; }
        }
    }
}
