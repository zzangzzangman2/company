using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using FamilyCompany.Simulation.OfficeWorkActions;
#if !OFFICE_WORK_STANDALONE
using UnityEditor;
using UnityEngine;
#endif

namespace FamilyCompany.Editor
{
    public static class OfficeWorkMicroActionValidation
    {
        private const int ValidationSeed = 2_000_081;
        private const long SessionStartedMinute = 4_321L;
        private const long ThirtyMinutesMilliseconds = 1_800_000L;

        private static readonly string[] FamilyMemberIds =
        {
            "player",
            "older_sister",
            "father",
            "mother"
        };

#if !OFFICE_WORK_STANDALONE
        [MenuItem("Family Company/Validate Office Work Micro Actions")]
        public static void Run()
        {
            try
            {
                RunAllOrThrow();
                Debug.Log("FAMILY_COMPANY_OFFICE_WORK_MICRO_ACTION_VALIDATION: PASS");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("FAMILY_COMPANY_OFFICE_WORK_MICRO_ACTION_VALIDATION: FAIL");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }
#endif

        public static int Main()
        {
            RunAllOrThrow();
            Console.WriteLine("FAMILY_COMPANY_OFFICE_WORK_MICRO_ACTION_STANDALONE: PASS");
            return 0;
        }

        public static void RunAllOrThrow()
        {
            ValidateThirtyMinuteCoverageAndWeights();
            ValidateDrinkCooldown();
            ValidateDeterminismAndChunking();
            ValidateSafeStandHandoffAndBlockedContexts();
            ValidateMissingFrameFallbackAndPartialAvailability();
            ValidateFourMemberIndependentSequences();
            ValidatePresentationOnlyStateBoundary();
            ValidateInputGuards();
        }

        private static void ValidateThirtyMinuteCoverageAndWeights()
        {
            foreach (var memberId in FamilyMemberIds)
            {
                var run = RunMachine(memberId, 1_000L);
                AssertStarted(run, OfficeWorkMicroAction.Typing, memberId + " typing exposure");
                AssertStarted(run, OfficeWorkMicroAction.Mouse, memberId + " mouse exposure");
                AssertStarted(run, OfficeWorkMicroAction.Drink, memberId + " drink exposure");
                var typing = run.CompletedDuration(OfficeWorkMicroAction.Typing);
                var other = run.CompletedDuration(OfficeWorkMicroAction.Mouse) +
                            run.CompletedDuration(OfficeWorkMicroAction.Drink) +
                            run.CompletedDuration(OfficeWorkMicroAction.BriefIdle);
                if (typing <= other)
                {
                    throw new InvalidOperationException(
                        $"{memberId}: typing is not the primary completed action duration ({typing} <= {other}).");
                }
            }
        }

        private static void ValidateDrinkCooldown()
        {
            foreach (var memberId in FamilyMemberIds)
            {
                var run = RunMachine(memberId, 317L);
                var starts = run.Transitions.Where(item =>
                        item.Kind == OfficeWorkMicroActionTransitionKind.ActionStarted &&
                        item.Action == OfficeWorkMicroAction.Drink)
                    .ToArray();
                var completions = run.Transitions.Where(item =>
                        item.Kind == OfficeWorkMicroActionTransitionKind.ActionCompleted &&
                        item.Action == OfficeWorkMicroAction.Drink)
                    .ToDictionary(item => item.ActionSequence, item => item.AtMilliseconds);
                if (starts.Length < 2)
                    throw new InvalidOperationException($"{memberId}: expected at least two drinks in 30 minutes.");
                if (starts.Length > 6)
                    throw new InvalidOperationException($"{memberId}: drink frequency is not low ({starts.Length} starts).");
                for (var index = 1; index < starts.Length; index++)
                {
                    var previousSequence = starts[index - 1].ActionSequence;
                    if (!completions.TryGetValue(previousSequence, out var previousCompleted))
                        throw new InvalidOperationException("Previous drink completion is missing.");
                    var gap = starts[index].AtMilliseconds - previousCompleted;
                    if (gap < OfficeWorkMicroActionStateMachine.DefaultDrinkCooldownMilliseconds)
                    {
                        throw new InvalidOperationException(
                            $"{memberId}: drink cooldown was violated ({gap}ms).");
                    }
                }
            }
        }

        private static void ValidateDeterminismAndChunking()
        {
            foreach (var memberId in FamilyMemberIds)
            {
                var direct = RunMachine(memberId, ThirtyMinutesMilliseconds);
                var oneSecond = RunMachine(memberId, 1_000L);
                var uneven = RunMachine(memberId, 137L);
                AssertEqual(direct.Fingerprint, oneSecond.Fingerprint, memberId + " direct/one-second determinism");
                AssertEqual(direct.Fingerprint, uneven.Fingerprint, memberId + " direct/uneven determinism");

                for (var repetition = 0; repetition < 25; repetition++)
                {
                    AssertEqual(
                        direct.Fingerprint,
                        RunMachine(memberId, 2_003L).Fingerprint,
                        memberId + " repeated determinism " + repetition);
                }
            }
        }

        private static void ValidateSafeStandHandoffAndBlockedContexts()
        {
            var machine = CreateMachine("older_sister", OfficeWorkMicroActionAvailability.All);
            machine.AdvanceTo(0L, OfficeWorkMicroActionContext.SeatedWork);
            machine.AdvanceTo(100L, OfficeWorkMicroActionContext.SeatedWork);
            var active = machine.CurrentAction;
            var actionEnd = machine.ActionEndsMilliseconds;
            if (active == OfficeWorkMicroAction.None)
                throw new InvalidOperationException("Expected an active action before stand handoff.");

            var request = machine.AdvanceTo(100L, OfficeWorkMicroActionContext.Meeting);
            AssertContains(request, OfficeWorkMicroActionTransitionKind.StopRequested, "meeting stop request");
            AssertEqual(active, machine.CurrentAction, "current action survives stop request");
            AssertFalse(machine.IsStandHandoffReady, "handoff waits for action completion");
            machine.AdvanceTo(actionEnd - 1L, OfficeWorkMicroActionContext.Meeting);
            AssertEqual(active, machine.CurrentAction, "current action remains before exact end");
            var completion = machine.AdvanceTo(actionEnd, OfficeWorkMicroActionContext.Meeting);
            AssertContains(completion, OfficeWorkMicroActionTransitionKind.ActionCompleted, "safe completion");
            AssertContains(completion, OfficeWorkMicroActionTransitionKind.StandHandoffReady, "stand readiness");
            AssertEqual(OfficeWorkMicroAction.None, machine.CurrentAction, "no action after handoff");
            var after = machine.AdvanceTo(actionEnd + 60_000L, OfficeWorkMicroActionContext.Meeting);
            AssertFalse(
                after.Any(item => item.Kind == OfficeWorkMicroActionTransitionKind.ActionStarted),
                "no action starts after stop request");

            var blocked = new[]
            {
                OfficeWorkMicroActionContext.SitDown,
                OfficeWorkMicroActionContext.StandUp,
                OfficeWorkMicroActionContext.Meeting,
                OfficeWorkMicroActionContext.Printing,
                OfficeWorkMicroActionContext.Moving,
                OfficeWorkMicroActionContext.OutsideSchedule
            };
            foreach (var context in blocked)
            {
                var fresh = CreateMachine("father", OfficeWorkMicroActionAvailability.All);
                var transitions = fresh.AdvanceTo(30_000L, context);
                AssertFalse(
                    transitions.Any(item => item.Kind == OfficeWorkMicroActionTransitionKind.ActionStarted),
                    context + " blocks new actions");
                AssertTrue(fresh.IsStandHandoffReady, context + " immediate handoff");
            }
        }

        private static void ValidateMissingFrameFallbackAndPartialAvailability()
        {
            AssertFalse(
                OfficeWorkMicroActionAvailabilityRules.IsFrameChannelUsable(0, false, 110),
                "empty frame channel");
            AssertFalse(
                OfficeWorkMicroActionAvailabilityRules.IsFrameChannelUsable(16, true, 110),
                "channel containing missing frame");
            AssertFalse(
                OfficeWorkMicroActionAvailabilityRules.IsFrameChannelUsable(10, false, 110),
                "channel not divisible by eight directions");
            AssertTrue(
                OfficeWorkMicroActionAvailabilityRules.IsFrameChannelUsable(16, false, 110),
                "complete two-frame eight-direction channel");

            var none = OfficeWorkMicroActionAvailabilityRules.Resolve(false, false, false, false);
            AssertTrue(
                OfficeWorkMicroActionAvailabilityRules.ShouldUseExistingWorkLoop(none),
                "missing frame set keeps existing work loop");
            var noFrames = CreateMachine("mother", none);
            var noFrameTransitions = noFrames.AdvanceTo(ThirtyMinutesMilliseconds, OfficeWorkMicroActionContext.SeatedWork);
            AssertFalse(
                noFrameTransitions.Any(item => item.Kind == OfficeWorkMicroActionTransitionKind.ActionStarted),
                "missing frame set starts nothing");

            var partial = OfficeWorkMicroActionAvailabilityRules.Resolve(true, false, true, false);
            AssertFalse(
                OfficeWorkMicroActionAvailabilityRules.ShouldUseExistingWorkLoop(partial),
                "partial frame set activates available actions");
            var partialMachine = CreateMachine("player", partial);
            var partialTransitions = partialMachine.AdvanceTo(
                ThirtyMinutesMilliseconds,
                OfficeWorkMicroActionContext.SeatedWork);
            AssertTrue(
                partialTransitions.Any(item =>
                    item.Kind == OfficeWorkMicroActionTransitionKind.ActionStarted &&
                    item.Action == OfficeWorkMicroAction.Typing),
                "partial typing appears");
            AssertTrue(
                partialTransitions.Any(item =>
                    item.Kind == OfficeWorkMicroActionTransitionKind.ActionStarted &&
                    item.Action == OfficeWorkMicroAction.Drink),
                "partial drink appears");
            AssertFalse(
                partialTransitions.Any(item =>
                    item.Kind == OfficeWorkMicroActionTransitionKind.ActionStarted &&
                    (item.Action == OfficeWorkMicroAction.Mouse ||
                     item.Action == OfficeWorkMicroAction.BriefIdle)),
                "missing partial actions are excluded");
        }

        private static void ValidateFourMemberIndependentSequences()
        {
            const long step = 211L;
            var machines = FamilyMemberIds.ToDictionary(
                item => item,
                item => CreateMachine(item, OfficeWorkMicroActionAvailability.All),
                StringComparer.Ordinal);
            var transitions = FamilyMemberIds.ToDictionary(
                item => item,
                _ => new List<OfficeWorkMicroActionTransition>(),
                StringComparer.Ordinal);
            foreach (var memberId in FamilyMemberIds)
                transitions[memberId].AddRange(machines[memberId].AdvanceTo(0L, OfficeWorkMicroActionContext.SeatedWork));

            for (var target = step; target <= ThirtyMinutesMilliseconds; target += step)
            {
                var clamped = Math.Min(target, ThirtyMinutesMilliseconds);
                for (var index = FamilyMemberIds.Length - 1; index >= 0; index--)
                {
                    var memberId = FamilyMemberIds[index];
                    transitions[memberId].AddRange(
                        machines[memberId].AdvanceTo(clamped, OfficeWorkMicroActionContext.SeatedWork));
                }
            }
            if (machines[FamilyMemberIds[0]].ProcessedMilliseconds < ThirtyMinutesMilliseconds)
            {
                foreach (var memberId in FamilyMemberIds)
                {
                    transitions[memberId].AddRange(
                        machines[memberId].AdvanceTo(
                            ThirtyMinutesMilliseconds,
                            OfficeWorkMicroActionContext.SeatedWork));
                }
            }

            var fingerprints = new HashSet<string>(StringComparer.Ordinal);
            foreach (var memberId in FamilyMemberIds)
            {
                var interleaved = Fingerprint(transitions[memberId]);
                var isolated = RunMachine(memberId, step).Fingerprint;
                AssertEqual(isolated, interleaved, memberId + " interleaved independence");
                if (!fingerprints.Add(interleaved))
                    throw new InvalidOperationException("Two family members produced the same 30-minute action sequence.");
            }
        }

        private static void ValidatePresentationOnlyStateBoundary()
        {
            var forbiddenTypeFragments = new[]
            {
                "GameState",
                "FamilyMemberState",
                "AutonomyNeeds",
                "CompanyState",
                "Ledger"
            };
            var fields = typeof(OfficeWorkMicroActionStateMachine).GetFields(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var field in fields)
            {
                var typeName = field.FieldType.FullName ?? field.FieldType.Name;
                if (forbiddenTypeFragments.Any(item =>
                        typeName.IndexOf(item, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    throw new InvalidOperationException(
                        $"Visual micro-action state references a forbidden gameplay writer type: {field.Name} ({typeName}).");
                }
            }
        }

        private static void ValidateInputGuards()
        {
            AssertThrows<ArgumentException>(
                () => new OfficeWorkMicroActionStateMachine(1, " ", 0, OfficeWorkMicroActionAvailability.All),
                "empty member ID");
            AssertThrows<ArgumentOutOfRangeException>(
                () => new OfficeWorkMicroActionStateMachine(1, "player", -1, OfficeWorkMicroActionAvailability.All),
                "negative session minute");
            AssertThrows<ArgumentOutOfRangeException>(
                () => new OfficeWorkMicroActionStateMachine(
                    1,
                    "player",
                    0,
                    (OfficeWorkMicroActionAvailability)128),
                "invalid availability");
            var machine = CreateMachine("player", OfficeWorkMicroActionAvailability.All);
            machine.AdvanceTo(1_000L, OfficeWorkMicroActionContext.SeatedWork);
            AssertThrows<InvalidOperationException>(
                () => machine.AdvanceTo(999L, OfficeWorkMicroActionContext.SeatedWork),
                "backward visual time");
            AssertThrows<ArgumentOutOfRangeException>(
                () => machine.AdvanceTo(1_000L, (OfficeWorkMicroActionContext)99),
                "invalid context");
        }

        private static OfficeWorkMicroActionStateMachine CreateMachine(
            string memberId,
            OfficeWorkMicroActionAvailability availability)
        {
            return new OfficeWorkMicroActionStateMachine(
                ValidationSeed,
                memberId,
                SessionStartedMinute,
                availability);
        }

        private static RunOutcome RunMachine(string memberId, long stepMilliseconds)
        {
            if (stepMilliseconds <= 0) throw new ArgumentOutOfRangeException(nameof(stepMilliseconds));
            var machine = CreateMachine(memberId, OfficeWorkMicroActionAvailability.All);
            var transitions = new List<OfficeWorkMicroActionTransition>();
            transitions.AddRange(machine.AdvanceTo(0L, OfficeWorkMicroActionContext.SeatedWork));
            var target = 0L;
            while (target < ThirtyMinutesMilliseconds)
            {
                target = Math.Min(ThirtyMinutesMilliseconds, checked(target + stepMilliseconds));
                transitions.AddRange(machine.AdvanceTo(target, OfficeWorkMicroActionContext.SeatedWork));
            }
            return new RunOutcome(transitions);
        }

        private static string Fingerprint(IEnumerable<OfficeWorkMicroActionTransition> transitions)
        {
            var builder = new StringBuilder();
            foreach (var item in transitions)
            {
                builder.Append(item.AtMilliseconds).Append(':')
                    .Append((int)item.Kind).Append(':')
                    .Append((int)item.Action).Append(':')
                    .Append((int)item.Context).Append(':')
                    .Append(item.ActionSequence).Append('|');
            }
            return builder.ToString();
        }

        private static void AssertStarted(RunOutcome run, OfficeWorkMicroAction action, string label)
        {
            AssertTrue(
                run.Transitions.Any(item =>
                    item.Kind == OfficeWorkMicroActionTransitionKind.ActionStarted &&
                    item.Action == action),
                label);
        }

        private static void AssertContains(
            IEnumerable<OfficeWorkMicroActionTransition> transitions,
            OfficeWorkMicroActionTransitionKind kind,
            string label)
        {
            AssertTrue(transitions.Any(item => item.Kind == kind), label);
        }

        private static void AssertTrue(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException(label + ": expected true.");
        }

        private static void AssertFalse(bool condition, string label)
        {
            if (condition) throw new InvalidOperationException(label + ": expected false.");
        }

        private static void AssertEqual<T>(T expected, T actual, string label)
        {
            if (!Equals(expected, actual))
                throw new InvalidOperationException($"{label}: expected {expected}, got {actual}.");
        }

        private static void AssertThrows<TException>(Action action, string label) where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException)
            {
                return;
            }
            throw new InvalidOperationException($"{label}: expected {typeof(TException).Name}.");
        }

        private sealed class RunOutcome
        {
            public RunOutcome(IReadOnlyList<OfficeWorkMicroActionTransition> transitions)
            {
                Transitions = transitions;
                Fingerprint = OfficeWorkMicroActionValidation.Fingerprint(transitions);
            }

            public IReadOnlyList<OfficeWorkMicroActionTransition> Transitions { get; }
            public string Fingerprint { get; }

            public long CompletedDuration(OfficeWorkMicroAction action)
            {
                var starts = Transitions.Where(item =>
                        item.Kind == OfficeWorkMicroActionTransitionKind.ActionStarted &&
                        item.Action == action)
                    .ToDictionary(item => item.ActionSequence, item => item.AtMilliseconds);
                var total = 0L;
                foreach (var completion in Transitions.Where(item =>
                             item.Kind == OfficeWorkMicroActionTransitionKind.ActionCompleted &&
                             item.Action == action))
                {
                    if (!starts.TryGetValue(completion.ActionSequence, out var started))
                        throw new InvalidOperationException("Action completion has no matching start.");
                    total = checked(total + completion.AtMilliseconds - started);
                }
                return total;
            }
        }
    }
}
