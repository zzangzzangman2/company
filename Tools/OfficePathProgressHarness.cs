using System;
using System.Collections.Generic;
using FamilyCompany.Simulation.Navigation;
using FamilyCompany.Simulation.OfficeLayout;

internal static class OfficePathProgressHarness
{
    public static int Main()
    {
        try
        {
            ValidateLongStraightProgress();
            ValidateCornerBoundary();
            ValidateReverseAxisProgress();
            ValidateGuards();
            Console.WriteLine("OFFICE_PATH_PROGRESS_EXTERNAL: PASS");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void ValidateLongStraightProgress()
    {
        OfficeGridCoordinate[] path = HorizontalPath(9);
        var cursor = 1;
        const int presentationTarget = 6;

        AssertEqual(1, Advance(path, cursor, presentationTarget, 0, 0), "start cell is not consumed twice");
        cursor = Advance(path, cursor, presentationTarget, 1, 0);
        AssertEqual(2, cursor, "first crossed cell");
        cursor = Advance(path, cursor, presentationTarget, 4, 0);
        AssertEqual(5, cursor, "skipped render samples still catch up to occupied path cell");
        cursor = Advance(path, cursor, presentationTarget, 2, 0);
        AssertEqual(5, cursor, "cursor never regresses when an old cell is reported");
        cursor = Advance(path, cursor, presentationTarget, 5, 0);
        AssertEqual(6, cursor, "reservation starts at the next uncrossed cell");
        cursor = Advance(path, cursor, presentationTarget, 6, 0);
        AssertEqual(6, cursor, "presentation target stays pending until arrival");
        cursor = Advance(path, cursor, presentationTarget, 4, 1);
        AssertEqual(6, cursor, "off-path yield cell does not change semantic progress");

        const int finalTarget = 8;
        cursor = Advance(path, cursor, finalTarget, 7, 0);
        AssertEqual(finalTarget, cursor, "cell before final target advances to the final target index");
        cursor = Advance(path, cursor, finalTarget, 8, 0);
        AssertEqual(finalTarget, cursor, "final target index is held until the caller completes arrival");
    }

    private static void ValidateCornerBoundary()
    {
        OfficeGridCoordinate[] path =
        {
            Cell(0, 0),
            Cell(1, 0),
            Cell(2, 0),
            Cell(2, 1),
            Cell(2, 2),
            Cell(2, 3)
        };

        Require(
            OfficeSemanticPathProgressRules.CanLookAheadWithoutSkippingTurn(path, 1, 2),
            "incoming straight run should reach the corner cell");
        Require(
            !OfficeSemanticPathProgressRules.CanLookAheadWithoutSkippingTurn(path, 2, 3),
            "cursor progress must not make presentation cut across a pending corner");
        Require(
            OfficeSemanticPathProgressRules.CanLookAheadWithoutSkippingTurn(path, 3, 5),
            "outgoing run should become available after the corner arrival advances the cursor");

        var cursor = Advance(path, 1, 2, 1, 0);
        AssertEqual(2, cursor, "cell before corner is crossed");
        cursor = Advance(path, cursor, 2, 2, 0);
        AssertEqual(2, cursor, "corner target is held for the caller's precise arrival check");
    }

    private static void ValidateReverseAxisProgress()
    {
        OfficeGridCoordinate[] path =
        {
            Cell(5, 4),
            Cell(4, 4),
            Cell(3, 4),
            Cell(2, 4),
            Cell(1, 4)
        };
        Require(
            OfficeSemanticPathProgressRules.CanLookAheadWithoutSkippingTurn(path, 1, 4),
            "negative-axis cardinal run should remain straight");
        AssertEqual(4, Advance(path, 1, 4, 2, 4), "negative-axis crossed cell");
    }

    private static void ValidateGuards()
    {
        OfficeGridCoordinate[] path = HorizontalPath(3);
        AssertThrows<ArgumentNullException>(() =>
            OfficeSemanticPathProgressRules.AdvanceThroughOccupiedCell(null, 0, 0, Cell(0, 0)));
        AssertThrows<ArgumentOutOfRangeException>(() =>
            OfficeSemanticPathProgressRules.AdvanceThroughOccupiedCell(path, -1, 1, Cell(0, 0)));
        AssertThrows<ArgumentOutOfRangeException>(() =>
            OfficeSemanticPathProgressRules.AdvanceThroughOccupiedCell(path, 2, 1, Cell(0, 0)));
        AssertThrows<ArgumentOutOfRangeException>(() =>
            OfficeSemanticPathProgressRules.CanLookAheadWithoutSkippingTurn(path, 1, 3));
    }

    private static int Advance(
        IReadOnlyList<OfficeGridCoordinate> path,
        int cursor,
        int target,
        int occupiedX,
        int occupiedY) =>
        OfficeSemanticPathProgressRules.AdvanceThroughOccupiedCell(
            path,
            cursor,
            target,
            Cell(occupiedX, occupiedY));

    private static OfficeGridCoordinate[] HorizontalPath(int count)
    {
        var result = new OfficeGridCoordinate[count];
        for (var index = 0; index < count; index++) result[index] = Cell(index, 0);
        return result;
    }

    private static OfficeGridCoordinate Cell(int x, int y) => new OfficeGridCoordinate(x, y);

    private static void AssertEqual<T>(T expected, T actual, string scenario)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{scenario}: expected {expected}, actual {actual}.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void AssertThrows<TException>(Action action)
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

        throw new InvalidOperationException("Expected " + typeof(TException).Name + ".");
    }
}
