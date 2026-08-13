using System;
using System.Collections.Generic;
using FamilyCompany.Simulation.OfficeLayout;

namespace FamilyCompany.Simulation.Navigation
{
    /// <summary>
    /// Keeps semantic path progress tied to cells the actor has actually crossed while the
    /// presentation layer is free to steer toward a farther waypoint on the same straight run.
    /// </summary>
    public static class OfficeSemanticPathProgressRules
    {
        public static int AdvanceThroughOccupiedCell(
            IReadOnlyList<OfficeGridCoordinate> semanticPath,
            int nextWaypointIndex,
            int presentationTargetIndex,
            OfficeGridCoordinate occupiedCell)
        {
            ValidatePathIndex(semanticPath, nextWaypointIndex, nameof(nextWaypointIndex));
            ValidatePathIndex(semanticPath, presentationTargetIndex, nameof(presentationTargetIndex));
            if (presentationTargetIndex < nextWaypointIndex)
                throw new ArgumentOutOfRangeException(
                    nameof(presentationTargetIndex),
                    "Presentation target cannot precede semantic progress.");

            // The presentation target remains pending until the arrival-distance check reaches it.
            // Only intermediate cells that the actor is actually occupying can move this cursor.
            for (var index = presentationTargetIndex - 1; index >= nextWaypointIndex; index--)
            {
                if (semanticPath[index].Equals(occupiedCell)) return index + 1;
            }

            return nextWaypointIndex;
        }

        public static bool CanLookAheadWithoutSkippingTurn(
            IReadOnlyList<OfficeGridCoordinate> semanticPath,
            int nextWaypointIndex,
            int candidateTargetIndex)
        {
            ValidatePathIndex(semanticPath, nextWaypointIndex, nameof(nextWaypointIndex));
            ValidatePathIndex(semanticPath, candidateTargetIndex, nameof(candidateTargetIndex));
            if (candidateTargetIndex < nextWaypointIndex)
                throw new ArgumentOutOfRangeException(
                    nameof(candidateTargetIndex),
                    "Candidate target cannot precede semantic progress.");
            if (candidateTargetIndex == nextWaypointIndex) return true;

            // Include the incoming cell. Without it, advancing the progress cursor onto a corner
            // makes the outgoing leg look straight and lets presentation cut across the turn.
            int runStartIndex = Math.Max(0, nextWaypointIndex - 1);
            OfficeGridCoordinate runStart = semanticPath[runStartIndex];
            bool sameX = true;
            bool sameY = true;
            for (var index = runStartIndex + 1; index <= candidateTargetIndex; index++)
            {
                sameX &= semanticPath[index].X == runStart.X;
                sameY &= semanticPath[index].Y == runStart.Y;
            }

            return sameX || sameY;
        }

        private static void ValidatePathIndex(
            IReadOnlyList<OfficeGridCoordinate> semanticPath,
            int index,
            string parameterName)
        {
            if (semanticPath == null) throw new ArgumentNullException(nameof(semanticPath));
            if (index < 0 || index >= semanticPath.Count)
                throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
