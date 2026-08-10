using System;
using System.Collections.Generic;
using System.IO;
using FamilyCompany.Presentation.Unity.OfficeSeating;
using FamilyCompany.Simulation.OfficeSeating;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public static class OfficeSeatingRulesValidation
    {
        [MenuItem("Family Company/Validate Office Seating Rules")]
        public static void Run()
        {
            try
            {
                RunAllOrThrow();
                Debug.Log("FAMILY_COMPANY_OFFICE_SEATING_RULES_VALIDATION: PASS");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("FAMILY_COMPANY_OFFICE_SEATING_RULES_VALIDATION: FAIL");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        public static void RunAllOrThrow()
        {
            ValidateTwoMembersCompeteForOneSeat();
            ValidateOneMemberAssignmentMoveAndSingleClaim();
            ValidateReservationAndTokenBoundaries();
            ValidateIdempotentRelease();
            ValidatePersistentSnapshotImportExport();
            ValidateNearestSeatDeterminism();
            ValidatePartialTopologyMemberGate();
            ValidateSharedRuntimeClaimOwnership();
            ValidateSessionIdentityBoundary();
            ValidatePrecisionApproachSettlement();
            ValidateIntegrationSourceContracts();
            ValidateInputBoundaries();
            ValidateOneHundredRepeatedRuns();
        }

        public static int Main()
        {
            RunAllOrThrow();
            Console.WriteLine("FAMILY_COMPANY_OFFICE_SEATING_RULES_STANDALONE: PASS");
            return 0;
        }

        private static void ValidateTwoMembersCompeteForOneSeat()
        {
            var state = CreateState();
            AssertTrue(state.TryReserve("desk-a", "older_sister", "task-a", out var first), "first member reserves seat");
            AssertEqual(OfficeSeatMeaningState.Reserved, first.State, "first reservation state");
            AssertFalse(state.TryReserve("desk-a", "father", "task-b", out var second), "second member cannot reserve same seat");
            AssertEqual(OfficeSeatOperationFailure.SeatClaimedByOtherMember, second.Failure, "competition failure reason");
            AssertSeat(state, "desk-a", OfficeSeatMeaningState.Reserved, string.Empty, "older_sister");
        }

        private static void ValidateOneMemberAssignmentMoveAndSingleClaim()
        {
            var state = CreateState();
            AssertTrue(state.TryAssign("desk-a", "older_sister", out var first), "initial assignment");
            AssertTrue(state.TryAssign("desk-b", "older_sister", out var moved), "assignment moves atomically");
            AssertEqual("desk-a", moved.PreviousAssignedSeatId, "previous assignment reported");
            AssertSeat(state, "desk-a", OfficeSeatMeaningState.Unassigned, string.Empty, string.Empty);
            AssertSeat(state, "desk-b", OfficeSeatMeaningState.Assigned, "older_sister", string.Empty);

            AssertTrue(state.TryReserve("desk-b", "older_sister", "work-1", out _), "assigned owner reserves own seat");
            AssertFalse(state.TryReserve("desk-a", "older_sister", "work-2", out var duplicateClaim), "member cannot reserve two seats");
            AssertEqual(OfficeSeatOperationFailure.MemberHasActiveClaim, duplicateClaim.Failure, "single active seat reason");
            AssertFalse(state.TryAssign("desk-a", "older_sister", out var moveWhileActive), "assignment cannot move during active claim");
            AssertEqual(OfficeSeatOperationFailure.MemberHasActiveClaim, moveWhileActive.Failure, "active member blocks assignment move");

            AssertTrue(state.TryOccupy("desk-b", "older_sister", "work-1", out _), "owner occupies reserved seat");
            AssertFalse(state.TryAssign("desk-b", "father", out var forced), "UI cannot force reassign occupied seat");
            AssertEqual(OfficeSeatOperationFailure.SeatHasActiveClaim, forced.Failure, "occupied target blocks reassign");
            AssertFalse(state.TryUnassign("desk-b", "older_sister", out var forcedUnassign), "UI cannot unassign occupied seat");
            AssertEqual(OfficeSeatOperationFailure.SeatHasActiveClaim, forcedUnassign.Failure, "occupied target blocks unassign");
        }

        private static void ValidateReservationAndTokenBoundaries()
        {
            var state = CreateState();
            AssertFalse(state.TryOccupy("desk-a", "father", "token-a", out var noReserve), "occupy requires reserve");
            AssertEqual(OfficeSeatOperationFailure.ReservationRequired, noReserve.Failure, "reserve required reason");

            AssertTrue(state.TryReserve("desk-a", "father", "token-a", out _), "reserve with token");
            AssertFalse(state.TryOccupy("desk-a", "father", "token-b", out var wrongOccupy), "occupy rejects wrong token");
            AssertEqual(OfficeSeatOperationFailure.TokenMismatch, wrongOccupy.Failure, "occupy token mismatch reason");
            AssertFalse(state.TryRelease("desk-a", "father", "token-b", out var wrongRelease), "release rejects wrong active token");
            AssertEqual(OfficeSeatOperationFailure.TokenMismatch, wrongRelease.Failure, "release token mismatch reason");
            AssertTrue(state.TryOccupy("token-a", out var occupied), "occupy with matching token only");
            AssertTrue(occupied.Changed, "first occupy changes state");
            AssertTrue(state.TryOccupy("token-a", out var repeated), "repeated matching occupy is safe");
            AssertFalse(repeated.Changed, "repeated occupy is unchanged");

            AssertFalse(state.TryReserve("desk-b", "mother", "token-a", out var reusedToken), "active token cannot claim another seat");
            AssertEqual(OfficeSeatOperationFailure.TokenAlreadyActive, reusedToken.Failure, "active token uniqueness reason");
        }

        private static void ValidateIdempotentRelease()
        {
            var state = CreateState();
            AssertTrue(state.TryReserve("desk-c", "mother", "outing-transition", out _), "reserve before cancellation");
            AssertTrue(state.TryRelease("outing-transition", out var first), "first token-only release succeeds");
            AssertTrue(first.Changed, "first release changes state");
            AssertTrue(state.TryRelease("outing-transition", out var second), "second token-only release succeeds");
            AssertFalse(second.Changed, "second release is idempotent");
            AssertTrue(state.TryReleaseForMember("mother", "outing-transition", out var inactiveRelease), "inactive member release succeeds");
            AssertFalse(inactiveRelease.Changed, "inactive member release is idempotent");
            AssertSeat(state, "desk-c", OfficeSeatMeaningState.Unassigned, string.Empty, string.Empty);
        }

        private static void ValidatePersistentSnapshotImportExport()
        {
            var source = CreateState();
            AssertTrue(source.TryAssign("desk-a", "father", out _), "source father assignment");
            AssertTrue(source.TryAssign("desk-c", "mother", out _), "source mother assignment");
            AssertTrue(source.TryReserve("desk-a", "father", "transient-token", out _), "source transient reservation");
            var snapshot = source.ExportPersistentAssignments();
            AssertEqual(2, snapshot.Assignments.Count, "snapshot contains only persistent assignments");

            var restored = CreateState();
            AssertTrue(restored.TryImportPersistentAssignments(snapshot, out var imported), "snapshot imports");
            AssertEqual(2, imported.ImportedAssignmentCount, "import count");
            AssertSeat(restored, "desk-a", OfficeSeatMeaningState.Assigned, "father", string.Empty);
            AssertSeat(restored, "desk-c", OfficeSeatMeaningState.Assigned, "mother", string.Empty);

            var baseline = SnapshotFingerprint(restored.ExportPersistentAssignments());
            var duplicateSeat = new OfficeSeatingAssignmentSnapshot(new[]
            {
                new OfficeSeatAssignment("desk-a", "father"),
                new OfficeSeatAssignment("desk-a", "mother")
            });
            AssertFalse(restored.TryImportPersistentAssignments(duplicateSeat, out var duplicateSeatResult), "duplicate seat import rejected");
            AssertEqual(OfficeSeatingImportFailure.DuplicateSeat, duplicateSeatResult.Failure, "duplicate seat reason");
            AssertEqual(baseline, SnapshotFingerprint(restored.ExportPersistentAssignments()), "duplicate seat import is atomic");

            var duplicateMember = new OfficeSeatingAssignmentSnapshot(new[]
            {
                new OfficeSeatAssignment("desk-a", "father"),
                new OfficeSeatAssignment("desk-b", "father")
            });
            AssertFalse(restored.TryImportPersistentAssignments(duplicateMember, out var duplicateMemberResult), "duplicate member import rejected");
            AssertEqual(OfficeSeatingImportFailure.DuplicateMember, duplicateMemberResult.Failure, "duplicate member reason");
            AssertEqual(baseline, SnapshotFingerprint(restored.ExportPersistentAssignments()), "duplicate member import is atomic");

            var unknown = new OfficeSeatingAssignmentSnapshot(new[]
            {
                new OfficeSeatAssignment("desk-unknown", "player")
            });
            AssertFalse(restored.TryImportPersistentAssignments(unknown, out var unknownResult), "unknown seat import rejected");
            AssertEqual(OfficeSeatingImportFailure.UnknownSeat, unknownResult.Failure, "unknown seat reason");
            AssertEqual(baseline, SnapshotFingerprint(restored.ExportPersistentAssignments()), "unknown import is atomic");

            AssertTrue(restored.TryReserve("desk-a", "father", "active", out _), "active reservation before import");
            AssertFalse(restored.TryImportPersistentAssignments(snapshot, out var activeResult), "import cannot overwrite active runtime claim");
            AssertEqual(OfficeSeatingImportFailure.ActiveClaimsPresent, activeResult.Failure, "active import reason");
        }

        private static void ValidateNearestSeatDeterminism()
        {
            var state = new OfficeSeatingState(new[]
            {
                Seat("seat-z", 1, 0),
                Seat("seat-a", -1, 0),
                Seat("seat-near", 0, 0.5)
            });
            AssertTrue(state.TryAssign("seat-near", "father", out _), "nearest seat assigned to other member");
            AssertTrue(state.TryFindNearestAvailableSeat(new OfficeSeatPosition(0, 0), "mother", out var selected), "available tie found");
            AssertEqual("seat-a", selected.SeatId, "distance tie uses seatId ordinal");

            var reordered = new OfficeSeatingState(new[]
            {
                Seat("seat-near", 0, 0.5),
                Seat("seat-a", -1, 0),
                Seat("seat-z", 1, 0)
            });
            AssertTrue(reordered.TryAssign("seat-near", "father", out _), "reordered nearest assigned");
            AssertTrue(reordered.TryFindNearestAvailableSeat(new OfficeSeatPosition(0, 0), "mother", out var reorderedSelection), "reordered tie found");
            AssertEqual(selected.SeatId, reorderedSelection.SeatId, "definition input order does not affect nearest seat");
        }

        private static void ValidatePartialTopologyMemberGate()
        {
            var state = new OfficeSeatingState(new[]
            {
                Seat("desk-a", -1, 0),
                Seat("desk-b", 1, 0),
                Seat("desk-c", -1, 2),
                Seat("desk-d", 1, 2)
            });
            AssertTrue(state.TryAssign("desk-a", "older_sister", out _), "partial gate sister assignment");
            AssertTrue(state.TryAssign("desk-b", "mother", out _), "partial gate mother assignment");
            AssertTrue(state.TryAssign("desk-c", "father", out _), "partial gate father assignment");
            AssertTrue(state.TryAssign("desk-d", "player", out _), "partial gate player assignment");

            bool HasPartialAuthoring(string seatId) => seatId == "desk-a" || seatId == "desk-b";
            AssertTrue(
                OfficeSeatRuntimeEligibility.HasClaimableSeat(state, "older_sister", HasPartialAuthoring),
                "partial gate keeps sister seating");
            AssertTrue(
                OfficeSeatRuntimeEligibility.HasClaimableSeat(state, "mother", HasPartialAuthoring),
                "partial gate keeps mother seating");
            AssertFalse(
                OfficeSeatRuntimeEligibility.HasClaimableSeat(state, "father", HasPartialAuthoring),
                "missing Desk C disables father seating gate");
            AssertFalse(
                OfficeSeatRuntimeEligibility.HasClaimableSeat(state, "player", HasPartialAuthoring),
                "missing Desk D disables player seating gate");
        }

        private static void ValidateSharedRuntimeClaimOwnership()
        {
            var state = CreateState();
            AssertTrue(
                OfficeSeatRuntimeClaim.TryReserve(
                    state,
                    "desk-a",
                    "father",
                    "shared-token",
                    out var first,
                    out _),
                "first runtime wrapper reserve");
            AssertTrue(
                OfficeSeatRuntimeClaim.TryReserve(
                    state,
                    "desk-a",
                    "father",
                    "shared-token",
                    out var repeated,
                    out _),
                "repeated runtime wrapper reserve");
            AssertTrue(ReferenceEquals(first, repeated), "repeated reserve returns the single owning wrapper");
            AssertTrue(repeated.TryOccupy(out _), "shared wrapper occupies");
            AssertTrue(first.IsOccupied, "shared wrapper occupation is visible to all callers");
            AssertTrue(
                OfficeSeatRuntimeClaim.TryReserve(
                    state,
                    "desk-a",
                    "father",
                    "shared-token",
                    out var occupiedRetry,
                    out _),
                "occupied runtime wrapper retry");
            AssertTrue(ReferenceEquals(first, occupiedRetry), "occupied retry preserves the single owning wrapper");
            repeated.Dispose();
            AssertTrue(first.IsReleased, "shared wrapper release is visible to all callers");
            AssertSeat(state, "desk-a", OfficeSeatMeaningState.Unassigned, string.Empty, string.Empty);
        }

        private static void ValidateSessionIdentityBoundary()
        {
            var first = new object();
            var second = new object();
            AssertFalse(
                OfficeSeatRuntimeEligibility.SessionIdentityChanged(first, first),
                "same GameState identity preserves binding");
            AssertTrue(
                OfficeSeatRuntimeEligibility.SessionIdentityChanged(first, second),
                "new/load GameState identity forces rebind");
            AssertTrue(
                OfficeSeatRuntimeEligibility.SessionIdentityChanged(null, first),
                "initial GameState identity forces bind");
        }

        private static void ValidatePrecisionApproachSettlement()
        {
            const double targetX = 14.375d;
            const double targetZ = -1.125d;
            var x = targetX - 0.079d;
            var z = targetZ + 0.061d;
            var arrived = false;
            for (var frame = 0; frame < 120 && !arrived; frame++)
            {
                var step = OfficeSeatPrecisionMotion.Advance(
                    x,
                    z,
                    targetX,
                    targetZ,
                    OfficeSeatPrecisionMotion.ApproachSpeedMetersPerSecond,
                    1d / 60d);
                x = step.X;
                z = step.Z;
                arrived = step.Arrived;
            }

            AssertTrue(arrived, "precision approach arrives");
            AssertEqual(targetX, x, "precision approach exact X");
            AssertEqual(targetZ, z, "precision approach exact Z");
        }

        private static void ValidateIntegrationSourceContracts()
        {
            var root = Directory.GetCurrentDirectory();
            var coordinator = ReadSource(root, "Assets/FamilyCompany/Presentation.Unity/OfficeAutonomyCoordinator.cs");
            var worker = ReadSource(root, "Assets/FamilyCompany/Presentation.Unity/OfficeWorkerAgent.cs");
            var player = ReadSource(root, "Assets/FamilyCompany/Presentation.Unity/OfficeSeating/OfficePlayerSeatingPresenter.cs");
            var animator = ReadSource(root, "Assets/FamilyCompany/Presentation.Unity/DirectionalSpriteAnimator.cs");
            var claim = ReadSource(root, "Assets/FamilyCompany/Presentation.Unity/OfficeSeating/OfficeSeatRuntimeClaim.cs");

            RequireContains(coordinator, "OfficeSeatRuntimeEligibility.HasClaimableSeat(", "member claimable gate");
            RequireContains(coordinator, "_boundGameState", "GameState identity binding");
            RequireContains(coordinator, "SessionIdentityChanged(", "session identity rebind");
            RequireContains(coordinator, "_seatingState = CreateSeatingState(definitions);", "fresh transient state on rebind");
            AssertFalse(
                worker.Contains("_seatRuntimeEnabled && _assignedWaypoint.Activity == OfficeActivity.Work"),
                "partial seating cannot pause fallback contract productivity");
            RequireContains(worker, "spriteAnimator.isActiveAndEnabled", "NPC animator lifecycle validity");
            RequireContains(worker, "spriteAnimator.HasOfficeSeatingFrames", "NPC frame-loss validity");
            RequireContains(worker, "TickPrecisionMoveToApproach(approach, deltaTime)", "NPC precision approach path");
            AssertFalse(worker.Contains("MoveTowardPosition(approach, deltaTime)"), "arrivalDistance snap removed from approach");
            RequireContains(player, "_animator.isActiveAndEnabled", "player animator lifecycle validity");
            RequireContains(player, "RestorePlayerMovement();", "player movement writer rollback");
            RequireContains(animator, "private void OnDisable()", "animator disable abort");
            RequireContains(animator, "private void OnDestroy()", "animator destroy abort");
            RequireContains(animator, "AbortOfficeSeatingPresentation();", "animator transition abort");
            RequireContains(claim, "ConditionalWeakTable<OfficeSeatingState, ClaimRegistry>", "single wrapper registry");
            RequireContains(claim, "claim = existing;", "duplicate reserve shares owner");
        }

        private static string ReadSource(string root, string relativePath)
        {
            var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) throw new FileNotFoundException("Seating integration source is missing.", path);
            return File.ReadAllText(path);
        }

        private static void RequireContains(string source, string fragment, string label)
        {
            AssertTrue(source.Contains(fragment), label);
        }

        private static void ValidateInputBoundaries()
        {
            AssertThrows<ArgumentException>(
                () => new OfficeSeatingState(new[] { Seat("desk-a", 0, 0), Seat("desk-a", 1, 1) }),
                "duplicate seat definitions rejected");
            AssertThrows<ArgumentOutOfRangeException>(
                () => new OfficeSeatPosition(double.NaN, 0),
                "NaN coordinate rejected");

            var state = CreateState();
            AssertFalse(state.TryAssign("missing", "player", out var unknown), "unknown assignment rejected");
            AssertEqual(OfficeSeatOperationFailure.UnknownSeat, unknown.Failure, "unknown assignment reason");
            AssertFalse(state.TryReserve("desk-a", "player", " ", out var invalidToken), "blank token rejected");
            AssertEqual(OfficeSeatOperationFailure.InvalidToken, invalidToken.Failure, "blank token reason");
            AssertTrue(state.TryAssign("desk-a", "player", out _), "player assigned");
            AssertFalse(state.TryReserve("desk-a", "father", "father-task", out var nonOwner), "assigned seat protects owner");
            AssertEqual(OfficeSeatOperationFailure.SeatAssignedToOtherMember, nonOwner.Failure, "assigned owner reason");
        }

        private static void ValidateOneHundredRepeatedRuns()
        {
            string expected = null;
            for (var run = 0; run < 100; run++)
            {
                var state = run % 2 == 0
                    ? CreateState()
                    : new OfficeSeatingState(new[]
                    {
                        Seat("desk-c", 0, 2),
                        Seat("desk-a", -1, 0),
                        Seat("desk-b", 1, 0)
                    });
                AssertTrue(state.TryAssign("desk-a", "older_sister", out _), "repeat assign sister");
                AssertTrue(state.TryAssign("desk-c", "mother", out _), "repeat assign mother");
                AssertTrue(state.TryReserve("desk-a", "older_sister", "repeat-work", out _), "repeat reserve");
                AssertTrue(state.TryOccupy("repeat-work", out _), "repeat occupy");
                AssertTrue(state.TryRelease("repeat-work", out _), "repeat release");
                AssertTrue(state.TryFindNearestAvailableSeat(new OfficeSeatPosition(0, 0), "father", out var nearest), "repeat nearest");
                var fingerprint = nearest.SeatId + "|" + SnapshotFingerprint(state.ExportPersistentAssignments());
                if (expected == null) expected = fingerprint;
                AssertEqual(expected, fingerprint, "deterministic repeated run " + run);
            }
        }

        private static OfficeSeatingState CreateState()
        {
            return new OfficeSeatingState(new[]
            {
                Seat("desk-b", 1, 0),
                Seat("desk-a", -1, 0),
                Seat("desk-c", 0, 2)
            });
        }

        private static OfficeSeatDefinition Seat(string id, double x, double z)
        {
            return new OfficeSeatDefinition(id, new OfficeSeatPosition(x, z));
        }

        private static void AssertSeat(
            OfficeSeatingState state,
            string seatId,
            OfficeSeatMeaningState expectedState,
            string expectedAssignedMemberId,
            string expectedRuntimeMemberId)
        {
            AssertTrue(state.TryGetSeat(seatId, out var seat), "seat exists: " + seatId);
            AssertEqual(expectedState, seat.State, "seat state: " + seatId);
            AssertEqual(expectedAssignedMemberId, seat.AssignedMemberId, "assigned member: " + seatId);
            AssertEqual(expectedRuntimeMemberId, seat.RuntimeMemberId, "runtime member: " + seatId);
        }

        private static string SnapshotFingerprint(OfficeSeatingAssignmentSnapshot snapshot)
        {
            var parts = new string[snapshot.Assignments.Count];
            for (var index = 0; index < snapshot.Assignments.Count; index++)
            {
                var assignment = snapshot.Assignments[index];
                parts[index] = assignment.SeatId + "=" + assignment.MemberId;
            }
            return string.Join(";", parts);
        }

        private static void AssertTrue(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException(label + ": expected true");
        }

        private static void AssertFalse(bool condition, string label)
        {
            if (condition) throw new InvalidOperationException(label + ": expected false");
        }

        private static void AssertEqual<T>(T expected, T actual, string label)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException(label + ": expected " + expected + ", got " + actual);
        }

        private static void AssertThrows<TException>(Action action, string label)
            where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException)
            {
                return;
            }
            throw new InvalidOperationException(label + ": expected " + typeof(TException).Name);
        }
    }
}

#if OFFICE_SEATING_RUNTIME_STANDALONE
namespace UnityEditor
{
    [System.AttributeUsage(System.AttributeTargets.Method)]
    public sealed class MenuItemAttribute : System.Attribute
    {
        public MenuItemAttribute(string path) { }
    }

    public static class EditorApplication
    {
        public static void Exit(int code) { }
    }
}

namespace UnityEngine
{
    public static class Application
    {
        public static bool isBatchMode => false;
    }

    public static class Debug
    {
        public static void Log(object value) { }
        public static void LogError(object value) { }
        public static void LogException(System.Exception exception) { }
    }
}
#endif
