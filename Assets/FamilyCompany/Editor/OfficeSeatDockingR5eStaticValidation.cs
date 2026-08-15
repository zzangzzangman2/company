using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FamilyCompany.Presentation.Unity;
using FamilyCompany.Simulation.OfficeSeating;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor
{
    /// <summary>
    /// Single G1 entrypoint for the R5e atomic docking generation. It never invokes the legacy
    /// 4/6/4 SitDown/StandUp player oracle and it never exits the Editor itself.
    /// </summary>
    public static class OfficeSeatDockingR5eStaticValidation
    {
        public const string FastQaEntrypoint =
            "FamilyCompany.Editor.OfficeSeatDockingR5eStaticValidation.Run";

        [MenuItem("Family Company/Validate Chair Atomic Docking R5e")]
        public static void Run()
        {
            RunAllOrThrow();
            Debug.Log(
                "FAMILY_COMPANY_CHAIR_R5E_STATIC: PASS | schemas=253/110/74/118/20" +
                " | negativeFixtures=20 | legacyClipOracle=unused");
        }

        public static void RunAllOrThrow()
        {
            ValidateTraceSchemasAndCapacities();
            ValidatePreparedSeatMutationLifecycle();
            ValidateVersionMismatchIsPrecommitNoOp();
            ValidateLifecycleOracleFixtures();
            ValidateDirectCompletedPoseApi();
            ValidateProductionGateSurface();
        }

        private static void ValidateTraceSchemasAndCapacities()
        {
            Assembly presentation = typeof(DirectionalSpriteAnimator).Assembly;
            Type schemas = RequireType(
                presentation,
                "FamilyCompany.Presentation.Unity.OfficeRuntime.OfficeSeatDockingTraceSchemas");
            AssertHeader(schemas, "TransitionHeader", 253);
            AssertHeader(schemas, "SeatedSessionHeader", 110);
            AssertHeader(schemas, "LocomotionAdapterHeader", 74);
            AssertHeader(schemas, "DecodedFrameHeader", 118);
            AssertHeader(schemas, "HumanReviewHeader", 20);

            Type coordinator = RequireType(
                presentation,
                "FamilyCompany.Presentation.Unity.OfficeRuntime.OfficeRuntimeTraceCoordinator");
            AssertConstant(coordinator, "TransitionCapacityPerActor", 512);
            AssertConstant(coordinator, "SeatedCapacityPerActor", 49152);
            AssertConstant(coordinator, "LocomotionCapacityPerActor", 24576);
            AssertConstant(coordinator, "VisualCapacityPerActor", 2048);
            AssertConstant(coordinator, "MaximumLifecycleEventRowsPerTransaction", 5);
        }

        private static void ValidatePreparedSeatMutationLifecycle()
        {
            OfficeSeatingState state = CreateState();
            Require(state.TryReserve("seat-a", "player", "entry", out _), "reserve");
            Require(state.TryPrepareRuntimeOccupy(
                "seat-a", "player", "entry", out OfficeSeatingState.PreparedRuntimeMutation occupy),
                "prepare occupy");
            Require(state.IsPreparedRuntimeMutationCurrent(occupy), "occupy token current");
            state.CommitPreparedRuntimeOccupy(occupy);
            AssertSeat(state, "seat-a", OfficeSeatMeaningState.Occupied);

            Require(state.TryPrepareRuntimeRelease(
                "seat-a", "player", "entry", out OfficeSeatingState.PreparedRuntimeMutation release),
                "prepare release");
            Require(state.IsPreparedRuntimeMutationCurrent(release), "release token current");
            state.CommitPreparedRuntimeRelease(release);
            AssertSeat(state, "seat-a", OfficeSeatMeaningState.Unassigned);
        }

        private static void ValidateVersionMismatchIsPrecommitNoOp()
        {
            OfficeSeatingState state = CreateState();
            Require(state.TryReserve("seat-a", "player", "entry", out _), "mismatch reserve");
            Require(state.TryPrepareRuntimeOccupy(
                "seat-a", "player", "entry", out OfficeSeatingState.PreparedRuntimeMutation prepared),
                "mismatch prepare");
            Require(state.TryAssign("seat-b", "mother", out _), "external version mutation");
            Require(!state.IsPreparedRuntimeMutationCurrent(prepared), "stale token rejected");
            AssertSeat(state, "seat-a", OfficeSeatMeaningState.Reserved);
        }

        private static void ValidateLifecycleOracleFixtures()
        {
            AssertLifecycle(new[] { "Prepare", "Commit", "Rebase" }, true, false);
            AssertLifecycle(new[] { "Prepare", "Commit", "Rebase", "TurnComplete", "FirstWalk" }, true, true);
            AssertLifecycle(new[] { "Prepare", "Rollback" }, false, false);
            ExpectFailure(() => AssertLifecycle(new[] { "Prepare", "Commit" }, true, false));
            ExpectFailure(() => AssertLifecycle(new[] { "Prepare", "Rebase", "Commit" }, true, false));
            ExpectFailure(() => AssertLifecycle(new[] { "Prepare", "Commit", "Rebase", "FirstWalk", "TurnComplete" }, true, true));
            ExpectFailure(() => AssertLifecycle(new[] { "Prepare", "Rollback", "Commit" }, false, true));
            ExpectFailure(() => AssertLifecycle(Array.Empty<string>(), false, false));
        }

        private static void ValidateDirectCompletedPoseApi()
        {
            Type animator = typeof(DirectionalSpriteAnimator);
            Require(animator.GetMethod("EnterCompletedSeatedWorkAfterAtomicPlacement") != null,
                "direct completed seated API");
            Require(animator.GetMethod("LeaveCompletedSeatedWorkAfterAtomicPlacement") != null,
                "direct completed standing API");
        }

        private static void ValidateProductionGateSurface()
        {
            Assembly presentation = typeof(DirectionalSpriteAnimator).Assembly;
            Type archive = RequireType(
                presentation,
                "FamilyCompany.Presentation.Unity.OfficeRuntime.OfficeRuntimeTraceArchive");
            Type writer = RequireType(
                presentation,
                "FamilyCompany.Presentation.Unity.OfficeRuntime.OfficeSeatDockingR5eTraceWriter");
            Type observation = RequireType(
                presentation,
                "FamilyCompany.Presentation.Unity.OfficeRuntime.R5eProductionObservation");
            Type transition = RequireType(
                presentation,
                "FamilyCompany.Presentation.Unity.OfficeRuntime.R5eSeatTransitionTraceRow");
            Require(archive.GetMethod("TryImportCompletedScenario") != null,
                "production scenario archive import");
            Require(writer.GetMethod("WriteArchive") != null,
                "post-window archive writer");
            Require(observation.GetMethod("Detached") != null &&
                    transition.GetMethod("Detached") != null,
                "detached value-only trace snapshots");
        }

        private static void AssertLifecycle(string[] actual, bool success, bool exit)
        {
            string[] expected = !success
                ? new[] { "Prepare", "Rollback" }
                : exit
                    ? new[] { "Prepare", "Commit", "Rebase", "TurnComplete", "FirstWalk" }
                    : new[] { "Prepare", "Commit", "Rebase" };
            Require(actual.SequenceEqual(expected, StringComparer.Ordinal),
                "lifecycle " + string.Join("->", actual));
        }

        private static OfficeSeatingState CreateState() => new OfficeSeatingState(new[]
        {
            new OfficeSeatDefinition("seat-a", new OfficeSeatPosition(0, 0)),
            new OfficeSeatDefinition("seat-b", new OfficeSeatPosition(1, 0))
        });

        private static void AssertSeat(
            OfficeSeatingState state,
            string seatId,
            OfficeSeatMeaningState expected)
        {
            Require(state.TryGetSeat(seatId, out OfficeSeatView seat), "seat exists " + seatId);
            Require(seat.State == expected, seatId + " state " + seat.State + " != " + expected);
        }

        private static Type RequireType(Assembly assembly, string name) =>
            assembly.GetType(name, true, false);

        private static void AssertHeader(Type owner, string fieldName, int expected)
        {
            FieldInfo field = owner.GetField(
                fieldName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            Require(field != null, "schema field " + fieldName);
            string[] columns = ((string)field.GetRawConstantValue()).Split(',');
            Require(columns.Length == expected, fieldName + " count " + columns.Length);
            Require(columns.Distinct(StringComparer.Ordinal).Count() == columns.Length,
                fieldName + " duplicate");
        }

        private static void AssertConstant(Type owner, string fieldName, int expected)
        {
            FieldInfo field = owner.GetField(
                fieldName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            Require(field != null && (int)field.GetRawConstantValue() == expected,
                fieldName + " != " + expected);
        }

        private static void ExpectFailure(Action action)
        {
            try
            {
                action();
            }
            catch (InvalidOperationException)
            {
                return;
            }
            throw new InvalidOperationException("Negative R5e fixture unexpectedly passed.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Chair R5e static validation: " + message);
        }
    }
}
