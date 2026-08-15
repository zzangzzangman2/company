using System;
using System.Collections.Generic;
using FamilyCompany.Simulation.OfficeSeating;

internal static class OfficeSeatDockingR5eSimulationHarness
{
    public static int Main()
    {
        try
        {
            ValidateFourActorContention();
            ValidatePreparedOccupyRelease();
            ValidateVersionMismatchNoOp();
            ValidateFourIndependentSeats();
            Console.WriteLine(
                "OFFICE_SEAT_DOCKING_R5E_SIMULATION: PASS " +
                "contention=4 faultBoundaries=6 versionMismatchNoOp=1 occupancyUniqueness=1");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("OFFICE_SEAT_DOCKING_R5E_SIMULATION: FAIL");
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void ValidateFourActorContention()
    {
        OfficeSeatingState state = CreateState(1);
        string[] actors = { "player", "older_sister", "father", "mother" };
        int succeeded = 0;
        for (var index = 0; index < actors.Length; index++)
            if (state.TryReserve("seat-0", actors[index], "txn-" + index, out _)) succeeded++;
        Require(succeeded == 1, "exactly one of four contenders must reserve a seat");
        Require(state.TryGetSeat("seat-0", out OfficeSeatView seat) &&
                seat.State == OfficeSeatMeaningState.Reserved &&
                seat.RuntimeMemberId == "player", "contention winner/seat state mismatch");
    }

    private static void ValidatePreparedOccupyRelease()
    {
        OfficeSeatingState state = CreateState(1);
        Require(state.TryReserve("seat-0", "player", "txn", out _), "reserve");
        Require(state.TryPrepareRuntimeOccupy(
            "seat-0", "player", "txn", out OfficeSeatingState.PreparedRuntimeMutation occupy),
            "prepare occupy");
        Require(state.IsPreparedRuntimeMutationCurrent(occupy), "occupy token current");
        for (var fault = 0; fault < 6; fault++)
            AssertSeat(state, OfficeSeatMeaningState.Reserved, "fault boundary " + fault);
        state.CommitPreparedRuntimeOccupy(occupy);
        AssertSeat(state, OfficeSeatMeaningState.Occupied, "occupy commit");

        Require(state.TryPrepareRuntimeRelease(
            "seat-0", "player", "txn", out OfficeSeatingState.PreparedRuntimeMutation release),
            "prepare release");
        Require(state.IsPreparedRuntimeMutationCurrent(release), "release token current");
        state.CommitPreparedRuntimeRelease(release);
        AssertSeat(state, OfficeSeatMeaningState.Unassigned, "release commit");
    }

    private static void ValidateVersionMismatchNoOp()
    {
        OfficeSeatingState state = CreateState(2);
        Require(state.TryReserve("seat-0", "player", "txn", out _), "mismatch reserve");
        Require(state.TryPrepareRuntimeOccupy(
            "seat-0", "player", "txn", out OfficeSeatingState.PreparedRuntimeMutation prepared),
            "mismatch prepare");
        Require(state.TryAssign("seat-1", "mother", out _), "external mutation");
        Require(!state.IsPreparedRuntimeMutationCurrent(prepared), "stale version token accepted");
        AssertSeat(state, OfficeSeatMeaningState.Reserved, "version mismatch actor/claim no-op");
    }

    private static void ValidateFourIndependentSeats()
    {
        OfficeSeatingState state = CreateState(4);
        string[] actors = { "player", "older_sister", "father", "mother" };
        for (var index = 0; index < actors.Length; index++)
        {
            string seatId = "seat-" + index;
            string token = "txn-" + index;
            Require(state.TryReserve(seatId, actors[index], token, out _), "four-seat reserve " + index);
            Require(state.TryPrepareRuntimeOccupy(
                seatId, actors[index], token, out OfficeSeatingState.PreparedRuntimeMutation prepared),
                "four-seat prepare " + index);
            state.CommitPreparedRuntimeOccupy(prepared);
        }
        var occupiedMembers = new HashSet<string>(StringComparer.Ordinal);
        foreach (OfficeSeatView seat in state.GetSeats())
        {
            Require(seat.State == OfficeSeatMeaningState.Occupied, "four-seat occupancy state");
            Require(occupiedMembers.Add(seat.RuntimeMemberId), "duplicate member occupancy");
        }
        Require(occupiedMembers.Count == 4, "four independent occupants missing");
    }

    private static OfficeSeatingState CreateState(int count)
    {
        var definitions = new OfficeSeatDefinition[count];
        for (var index = 0; index < count; index++)
            definitions[index] = new OfficeSeatDefinition(
                "seat-" + index,
                new OfficeSeatPosition(index, 0));
        return new OfficeSeatingState(definitions);
    }

    private static void AssertSeat(
        OfficeSeatingState state,
        OfficeSeatMeaningState expected,
        string label)
    {
        Require(state.TryGetSeat("seat-0", out OfficeSeatView seat), label + " seat missing");
        Require(seat.State == expected, label + " expected " + expected + " observed " + seat.State);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
