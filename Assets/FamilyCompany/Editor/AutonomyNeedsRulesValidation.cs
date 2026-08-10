using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using FamilyCompany.Simulation.AutonomyNeeds;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public static class AutonomyNeedsRulesValidation
    {
        private const int ValidationSeed = 20000103;

        [MenuItem("Family Company/Validate Autonomy Needs Rules")]
        public static void Run()
        {
            try
            {
                RunAll();
                Debug.Log("FAMILY_COMPANY_AUTONOMY_NEEDS_VALIDATION: PASS");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("FAMILY_COMPANY_AUTONOMY_NEEDS_VALIDATION: FAIL");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        public static void RunAll()
        {
            ValidateFamilyProfiles();
            ValidateWorkBreakRecoveryHysteresis();
            ValidateCollapseAbsenceAndCrunchRisk();
            ValidateRelationshipOrdering();
            ValidateOptionalNeedExtension();
            ValidateOneThreeTenMinuteEquivalence();
            ValidatePersistentSnapshotBoundary();
            ValidateRepeatedDeterminism();
            ValidateInputGuards();
        }

        public static int Main()
        {
            try
            {
                RunAll();
                Console.WriteLine("FAMILY_COMPANY_AUTONOMY_NEEDS_PURE_HARNESS: PASS");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                Console.Error.WriteLine("FAMILY_COMPANY_AUTONOMY_NEEDS_PURE_HARNESS: FAIL");
                return 1;
            }
        }

        private static void ValidateFamilyProfiles()
        {
            var profiles = FamilyAutonomyNeedsProfileCatalog.All;
            AssertEqual(4, profiles.Count, "family profile count");
            AssertSequenceEqual(
                new[] { "player", "older_sister", "father", "mother" },
                profiles.Select(item => item.MemberId).ToArray(),
                "family profile IDs");

            var signatures = new HashSet<string>(StringComparer.Ordinal);
            foreach (var profile in profiles)
            {
                if (profile.ResumeEnergyBasisPoints <= profile.BreakEnergyBasisPoints ||
                    profile.ResumeStressBasisPoints >= profile.BreakStressBasisPoints ||
                    profile.ResumeFocusBasisPoints <= profile.BreakFocusBasisPoints)
                    throw new InvalidOperationException($"{profile.MemberId}: hysteresis thresholds overlap.");

                var simulator = AutonomyNeedsSimulator.Create(
                    ValidationSeed,
                    profile.MemberId,
                    0,
                    7_000,
                    3_000,
                    7_000);
                simulator.AdvanceTo(
                    60,
                    new AutonomyNeedsWorkContext(AutonomyWorkIntensity.Normal, forceCrunch: true));
                signatures.Add(
                    $"{simulator.State.EnergyBasisPoints}:{simulator.State.StressBasisPoints}:" +
                    $"{simulator.State.FocusBasisPoints}");
            }

            AssertEqual(4, signatures.Count, "four personality outcomes");
        }

        private static void ValidateWorkBreakRecoveryHysteresis()
        {
            var simulator = AutonomyNeedsSimulator.Create(
                ValidationSeed,
                FamilyAutonomyNeedsProfileCatalog.OlderSisterId,
                0,
                3_800,
                6_700,
                3_400);
            var decisions = new List<AutonomyNeedsDecisionEvent>();
            AutonomyNeedsDecisionEvent? returnDecision = null;
            AutonomyNeedsDecisionEvent? workDecision = null;

            for (var minute = 1L; minute <= 700 && workDecision == null; minute++)
            {
                var result = simulator.AdvanceTo(minute, AutonomyNeedsWorkContext.NormalWork);
                decisions.AddRange(result.Decisions);
                foreach (var decision in result.Decisions)
                {
                    if (decision.Decision == AutonomyNeedsDecisionKind.ReturnToWork)
                    {
                        returnDecision = decision;
                        var profile = simulator.Profile;
                        if (simulator.State.EnergyBasisPoints < profile.ResumeEnergyBasisPoints ||
                            simulator.State.StressBasisPoints > profile.ResumeStressBasisPoints ||
                            simulator.State.FocusBasisPoints < profile.ResumeFocusBasisPoints)
                            throw new InvalidOperationException("Return-to-work occurred before recovery thresholds were met.");
                    }
                    if (returnDecision != null && decision.Decision == AutonomyNeedsDecisionKind.Work)
                        workDecision = decision;
                }
            }

            if (returnDecision == null || workDecision == null)
                throw new InvalidOperationException("Work -> break -> recovery -> work cycle did not complete.");
            AssertDecisionChain(decisions);
            AssertEqual(1, simulator.State.CompletedBreaks, "completed recovery break count");

            var cooldownContext = new AutonomyNeedsWorkContext(
                AutonomyWorkIntensity.Normal,
                optionalNeeds: new[]
                {
                    new AutonomyOptionalNeedSignal(AutonomyOptionalNeedIds.Toilet, 10_000)
                });
            var cooldownResult = simulator.AdvanceTo(simulator.State.LastProcessedMinute + 1, cooldownContext);
            if (cooldownResult.Decisions.Any(item => item.Decision == AutonomyNeedsDecisionKind.BreakRequest))
                throw new InvalidOperationException("Break cooldown allowed immediate state oscillation.");
            AssertEqual(AutonomyNeedsMode.Work, simulator.State.Mode, "cooldown keeps work mode stable");
        }

        private static void ValidateCollapseAbsenceAndCrunchRisk()
        {
            var collapsing = AutonomyNeedsSimulator.Create(
                ValidationSeed,
                FamilyAutonomyNeedsProfileCatalog.FatherId,
                0,
                5,
                5_000,
                5_000);
            var collapseResult = collapsing.AdvanceTo(
                1,
                new AutonomyNeedsWorkContext(AutonomyWorkIntensity.Crunch, forceCrunch: true));
            AssertEqual(AutonomyNeedsMode.Collapsed, collapsing.State.Mode, "zero-energy collapse mode");
            AssertEqual(1, collapsing.State.CollapseCount, "collapse count");
            AssertEqual(AutonomyCollapseCause.EnergyDepleted, collapsing.State.LastCollapseCause, "collapse cause");
            AssertContains(collapseResult.Decisions, AutonomyNeedsDecisionKind.Collapse, "collapse decision");

            var absenceResult = collapsing.AdvanceTo(
                2,
                new AutonomyNeedsWorkContext(AutonomyWorkIntensity.Crunch, forceCrunch: true));
            AssertEqual(AutonomyNeedsMode.Absent, collapsing.State.Mode, "post-collapse absence mode");
            AssertContains(absenceResult.Decisions, AutonomyNeedsDecisionKind.Absent, "absence decision");
            AssertEqual(0, absenceResult.WorkedMinutes, "absence blocks work");

            var voluntary = AutonomyNeedsSimulator.Create(
                ValidationSeed,
                FamilyAutonomyNeedsProfileCatalog.PlayerId,
                0,
                3_900,
                6_600,
                3_600);
            var forced = AutonomyNeedsSimulator.Create(
                ValidationSeed,
                FamilyAutonomyNeedsProfileCatalog.PlayerId,
                0,
                3_900,
                6_600,
                3_600);
            var voluntaryResult = voluntary.AdvanceTo(
                1,
                new AutonomyNeedsWorkContext(AutonomyWorkIntensity.Crunch));
            var forcedResult = forced.AdvanceTo(
                1,
                new AutonomyNeedsWorkContext(AutonomyWorkIntensity.Crunch, forceCrunch: true));
            if (forcedResult.EffectiveWorkBasisPointMinutes >= voluntaryResult.EffectiveWorkBasisPointMinutes)
                throw new InvalidOperationException("Forced crunch did not reduce minute efficiency while needs were unsafe.");
            if (forced.State.CurrentRiskBasisPoints <= voluntary.State.CurrentRiskBasisPoints)
                throw new InvalidOperationException("Forced crunch did not increase health risk.");
            AssertEqual(AutonomyNeedsMode.BreakRequest, voluntary.State.Mode, "voluntary crunch accepts break request");
            AssertEqual(AutonomyNeedsMode.Work, forced.State.Mode, "forced crunch suppresses break request");
        }

        private static void ValidateRelationshipOrdering()
        {
            var ordered = new[]
            {
                new AutonomyTimedRelationshipEvent("support", 3, AutonomyRelationshipEventKind.Support, 15),
                new AutonomyTimedRelationshipEvent("conflict", 5, AutonomyRelationshipEventKind.Conflict, 12),
                new AutonomyTimedRelationshipEvent("repair", 8, AutonomyRelationshipEventKind.Reconciliation, 8)
            };
            var reversed = ordered.Reverse().ToArray();
            var first = AutonomyNeedsSimulator.CreateDefault(ValidationSeed, FamilyAutonomyNeedsProfileCatalog.MotherId);
            var second = AutonomyNeedsSimulator.CreateDefault(ValidationSeed, FamilyAutonomyNeedsProfileCatalog.MotherId);
            var baseline = AutonomyNeedsSimulator.CreateDefault(ValidationSeed, FamilyAutonomyNeedsProfileCatalog.MotherId);
            first.AdvanceTo(20, AutonomyNeedsWorkContext.NormalWork, ordered);
            second.AdvanceTo(20, AutonomyNeedsWorkContext.NormalWork, reversed);
            baseline.AdvanceTo(20, AutonomyNeedsWorkContext.NormalWork);
            AssertSnapshotEqual(first.ExportPersistentSnapshot(), second.ExportPersistentSnapshot(), "relationship input order");
            if (SnapshotFingerprint(first.ExportPersistentSnapshot()) == SnapshotFingerprint(baseline.ExportPersistentSnapshot()))
                throw new InvalidOperationException("Relationship events did not affect autonomy needs.");
        }

        private static void ValidateOptionalNeedExtension()
        {
            var simulator = AutonomyNeedsSimulator.CreateDefault(
                ValidationSeed,
                FamilyAutonomyNeedsProfileCatalog.PlayerId);
            var result = simulator.AdvanceTo(
                1,
                new AutonomyNeedsWorkContext(
                    AutonomyWorkIntensity.Normal,
                    optionalNeeds: new[]
                    {
                        new AutonomyOptionalNeedSignal(AutonomyOptionalNeedIds.Hunger, 9_000)
                    }));
            AssertEqual(AutonomyNeedsMode.BreakRequest, simulator.State.Mode, "optional need break mode");
            var request = result.Decisions.Single(item => item.Decision == AutonomyNeedsDecisionKind.BreakRequest);
            AssertEqual(AutonomyBreakCause.OptionalNeed, request.BreakCause, "optional need break cause");
        }

        private static void ValidateOneThreeTenMinuteEquivalence()
        {
            var one = RunChunked(1);
            var three = RunChunked(3);
            var ten = RunChunked(10);
            AssertSnapshotEqual(one.snapshot, three.snapshot, "1x versus 3x snapshot");
            AssertSnapshotEqual(one.snapshot, ten.snapshot, "1x versus 10x snapshot");
            AssertEqual(one.workedMinutes, three.workedMinutes, "1x versus 3x worked minutes");
            AssertEqual(one.workedMinutes, ten.workedMinutes, "1x versus 10x worked minutes");
            AssertEqual(one.effectiveWork, three.effectiveWork, "1x versus 3x effective work");
            AssertEqual(one.effectiveWork, ten.effectiveWork, "1x versus 10x effective work");
            AssertEqual(one.decisions, three.decisions, "1x versus 3x decisions");
            AssertEqual(one.decisions, ten.decisions, "1x versus 10x decisions");
        }

        private static ChunkedOutcome RunChunked(int stepMinutes)
        {
            const int targetMinute = 1_440;
            var events = new[]
            {
                new AutonomyTimedRelationshipEvent("morning_support", 100, AutonomyRelationshipEventKind.Support, 10),
                new AutonomyTimedRelationshipEvent("deadline_conflict", 333, AutonomyRelationshipEventKind.Conflict, 18),
                new AutonomyTimedRelationshipEvent("evening_repair", 900, AutonomyRelationshipEventKind.Reconciliation, 12)
            };
            var simulator = AutonomyNeedsSimulator.CreateDefault(
                ValidationSeed,
                FamilyAutonomyNeedsProfileCatalog.OlderSisterId);
            var workedMinutes = 0L;
            var effectiveWork = 0L;
            var decisionText = new StringBuilder();
            while (simulator.State.LastProcessedMinute < targetMinute)
            {
                var target = Math.Min(targetMinute, simulator.State.LastProcessedMinute + stepMinutes);
                var result = simulator.AdvanceTo(target, AutonomyNeedsWorkContext.NormalWork, events);
                workedMinutes += result.WorkedMinutes;
                effectiveWork += result.EffectiveWorkBasisPointMinutes;
                foreach (var decision in result.Decisions)
                {
                    decisionText.Append(decision.Minute).Append(':')
                        .Append((int)decision.Decision).Append(':')
                        .Append((int)decision.RecoveryActivity).Append(':')
                        .Append((int)decision.BreakCause).Append('|');
                }
            }

            return new ChunkedOutcome(
                simulator.ExportPersistentSnapshot(),
                workedMinutes,
                effectiveWork,
                decisionText.ToString());
        }

        private static void ValidatePersistentSnapshotBoundary()
        {
            var simulator = AutonomyNeedsSimulator.Create(
                ValidationSeed,
                FamilyAutonomyNeedsProfileCatalog.PlayerId,
                0,
                3_900,
                6_500,
                3_600);
            simulator.AdvanceTo(1, AutonomyNeedsWorkContext.NormalWork);
            if (string.IsNullOrEmpty(simulator.Transient.ActiveRequestToken))
                throw new InvalidOperationException("Active break request did not create a transient token.");

            var snapshot = simulator.ExportPersistentSnapshot();
            var restored = AutonomyNeedsSimulator.Restore(snapshot);
            AssertSnapshotEqual(snapshot, restored.ExportPersistentSnapshot(), "snapshot restore");
            AssertEqual(string.Empty, restored.Transient.ActiveRequestToken, "transient token excluded from snapshot");
            AssertEqual(0L, restored.Transient.LastDecisionSequence, "transient decision sequence reset");

            var forbiddenNames = new[] { "token", "route", "navigation", "destination", "decision" };
            foreach (var field in typeof(AutonomyNeedsPersistentSnapshotDto).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (forbiddenNames.Any(item => field.Name.IndexOf(item, StringComparison.OrdinalIgnoreCase) >= 0))
                    throw new InvalidOperationException($"Transient field leaked into persistent DTO: {field.Name}");
            }

            simulator.AdvanceTo(500, AutonomyNeedsWorkContext.NormalWork);
            restored.AdvanceTo(500, AutonomyNeedsWorkContext.NormalWork);
            AssertSnapshotEqual(
                simulator.ExportPersistentSnapshot(),
                restored.ExportPersistentSnapshot(),
                "post-restore deterministic continuation");
        }

        private static void ValidateRepeatedDeterminism()
        {
            string expected = null;
            for (var iteration = 0; iteration < 100; iteration++)
            {
                var simulator = AutonomyNeedsSimulator.Create(
                    ValidationSeed,
                    FamilyAutonomyNeedsProfileCatalog.OlderSisterId,
                    0,
                    3_800,
                    6_700,
                    3_400);
                var result = simulator.AdvanceTo(500, AutonomyNeedsWorkContext.NormalWork);
                var fingerprint = SnapshotFingerprint(simulator.ExportPersistentSnapshot()) + "/" +
                    string.Join(",", result.Decisions.Select(item =>
                        $"{item.Minute}:{(int)item.Decision}:{(int)item.RecoveryActivity}"));
                if (iteration == 0) expected = fingerprint;
                else AssertEqual(expected, fingerprint, $"deterministic repetition {iteration}");
            }
        }

        private static void ValidateInputGuards()
        {
            var simulator = AutonomyNeedsSimulator.CreateDefault(
                ValidationSeed,
                FamilyAutonomyNeedsProfileCatalog.PlayerId);
            AssertThrows<InvalidOperationException>(
                () => simulator.AdvanceTo(-1, AutonomyNeedsWorkContext.NormalWork),
                "backward time");
            AssertThrows<ArgumentException>(
                () => simulator.AdvanceTo(
                    1,
                    AutonomyNeedsWorkContext.NormalWork,
                    new[]
                    {
                        new AutonomyTimedRelationshipEvent("duplicate", 1, AutonomyRelationshipEventKind.Support, 1),
                        new AutonomyTimedRelationshipEvent("duplicate", 1, AutonomyRelationshipEventKind.Conflict, 1)
                    }),
                "duplicate relationship event ID");
            AssertThrows<KeyNotFoundException>(
                () => FamilyAutonomyNeedsProfileCatalog.Get("unknown"),
                "unknown profile");

            var invalid = simulator.ExportPersistentSnapshot();
            invalid.schemaVersion = 99;
            AssertThrows<InvalidOperationException>(() => AutonomyNeedsSimulator.Restore(invalid), "snapshot schema");
        }

        private static void AssertDecisionChain(IReadOnlyList<AutonomyNeedsDecisionEvent> decisions)
        {
            var requestIndex = IndexOf(decisions, AutonomyNeedsDecisionKind.BreakRequest, 0);
            if (requestIndex < 0) throw new InvalidOperationException("BreakRequest decision is missing.");
            var movementIndex = FindRecoveryMovement(decisions, requestIndex + 1);
            if (movementIndex < 0) throw new InvalidOperationException("Recovery movement decision is missing.");
            var activityIndex = FindRecoveryActivity(decisions, movementIndex + 1);
            if (activityIndex < 0) throw new InvalidOperationException("Recovery activity decision is missing.");
            var returnIndex = IndexOf(decisions, AutonomyNeedsDecisionKind.ReturnToWork, activityIndex + 1);
            if (returnIndex < 0) throw new InvalidOperationException("ReturnToWork decision is missing.");
            var workIndex = IndexOf(decisions, AutonomyNeedsDecisionKind.Work, returnIndex + 1);
            if (workIndex < 0) throw new InvalidOperationException("Work resumption decision is missing.");
        }

        private static int FindRecoveryMovement(IReadOnlyList<AutonomyNeedsDecisionEvent> decisions, int start)
        {
            for (var index = start; index < decisions.Count; index++)
            {
                var value = decisions[index].Decision;
                if (value == AutonomyNeedsDecisionKind.GoToLounge ||
                    value == AutonomyNeedsDecisionKind.GoToWater ||
                    value == AutonomyNeedsDecisionKind.GoToStretchArea)
                    return index;
            }
            return -1;
        }

        private static int FindRecoveryActivity(IReadOnlyList<AutonomyNeedsDecisionEvent> decisions, int start)
        {
            for (var index = start; index < decisions.Count; index++)
            {
                var value = decisions[index].Decision;
                if (value == AutonomyNeedsDecisionKind.LoungeRest ||
                    value == AutonomyNeedsDecisionKind.DrinkWater ||
                    value == AutonomyNeedsDecisionKind.Stretch)
                    return index;
            }
            return -1;
        }

        private static int IndexOf(
            IReadOnlyList<AutonomyNeedsDecisionEvent> decisions,
            AutonomyNeedsDecisionKind kind,
            int start)
        {
            for (var index = start; index < decisions.Count; index++)
            {
                if (decisions[index].Decision == kind) return index;
            }
            return -1;
        }

        private static void AssertContains(
            IReadOnlyList<AutonomyNeedsDecisionEvent> decisions,
            AutonomyNeedsDecisionKind kind,
            string label)
        {
            if (decisions.All(item => item.Decision != kind))
                throw new InvalidOperationException($"{label}: {kind} was not emitted.");
        }

        private static void AssertSnapshotEqual(
            AutonomyNeedsPersistentSnapshotDto expected,
            AutonomyNeedsPersistentSnapshotDto actual,
            string label)
        {
            AssertEqual(SnapshotFingerprint(expected), SnapshotFingerprint(actual), label);
        }

        private static string SnapshotFingerprint(AutonomyNeedsPersistentSnapshotDto item)
        {
            return string.Join(
                "|",
                item.schemaVersion,
                item.memberId,
                item.worldSeed,
                item.energyBasisPoints,
                item.stressBasisPoints,
                item.focusBasisPoints,
                item.mode,
                item.recoveryActivity,
                item.breakCause,
                item.lastCollapseCause,
                item.lastProcessedMinute,
                item.modeStartedMinute,
                item.cooldownUntilMinute,
                item.absenceUntilMinute,
                item.currentRiskBasisPoints,
                item.collapseCount,
                item.breakSequence,
                item.completedBreaks,
                item.cumulativeCrunchMinutes,
                item.cumulativeEffectiveWorkBasisPointMinutes);
        }

        private static void AssertSequenceEqual<T>(IReadOnlyList<T> expected, IReadOnlyList<T> actual, string label)
        {
            if (!expected.SequenceEqual(actual))
                throw new InvalidOperationException($"{label}: expected [{string.Join(",", expected)}], got [{string.Join(",", actual)}].");
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

        private static void AssertEqual<T>(T expected, T actual, string label)
        {
            if (!Equals(expected, actual))
                throw new InvalidOperationException($"{label}: expected {expected}, got {actual}.");
        }

        private sealed class ChunkedOutcome
        {
            public ChunkedOutcome(
                AutonomyNeedsPersistentSnapshotDto snapshot,
                long workedMinutes,
                long effectiveWork,
                string decisions)
            {
                this.snapshot = snapshot;
                this.workedMinutes = workedMinutes;
                this.effectiveWork = effectiveWork;
                this.decisions = decisions;
            }

            public readonly AutonomyNeedsPersistentSnapshotDto snapshot;
            public readonly long workedMinutes;
            public readonly long effectiveWork;
            public readonly string decisions;
        }
    }
}
