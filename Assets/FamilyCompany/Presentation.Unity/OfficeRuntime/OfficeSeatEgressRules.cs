using System;
using System.Collections.Generic;
using FamilyCompany.Simulation.OfficeLayout;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime
{
    public enum OfficeSeatEgressKind
    {
        None = 0,
        Front = 1,
        Left = 2,
        Right = 3
    }

    public readonly struct OfficeSeatEgressCandidate
    {
        public OfficeSeatEgressCandidate(
            OfficeSeatEgressKind kind,
            OfficeGridCoordinate targetCell)
        {
            if (kind == OfficeSeatEgressKind.None) throw new ArgumentOutOfRangeException(nameof(kind));
            Kind = kind;
            TargetCell = targetCell;
        }

        public OfficeSeatEgressKind Kind { get; }
        public OfficeGridCoordinate TargetCell { get; }
    }

    /// <summary>
    /// Resolves only the seat-local egress preference. Walkability, body clearance, continuous
    /// collision and dynamic reservations remain owned by the runtime occupancy service.
    /// </summary>
    public static class OfficeSeatEgressRules
    {
        public const int CandidateCount = 3;

        public static IReadOnlyList<OfficeSeatEgressCandidate> ResolveCandidates(OfficeSeatSlot seat)
        {
            if (seat == null) throw new ArgumentNullException(nameof(seat));
            int frontX = seat.ApproachCell.X - seat.Cell.X;
            int frontY = seat.ApproachCell.Y - seat.Cell.Y;
            if (Math.Abs(frontX) + Math.Abs(frontY) != 1)
                throw new InvalidOperationException(
                    "Seat approach must be a cardinal front socket: " + seat.SeatId + ".");

            // Grid-space counter-clockwise/clockwise rotations preserve the authored front socket
            // for every chair rotation. A rear candidate is deliberately never produced.
            int leftX = -frontY;
            int leftY = frontX;
            int rightX = frontY;
            int rightY = -frontX;
            return new[]
            {
                new OfficeSeatEgressCandidate(OfficeSeatEgressKind.Front, seat.ApproachCell),
                new OfficeSeatEgressCandidate(
                    OfficeSeatEgressKind.Left,
                    new OfficeGridCoordinate(seat.Cell.X + leftX, seat.Cell.Y + leftY)),
                new OfficeSeatEgressCandidate(
                    OfficeSeatEgressKind.Right,
                    new OfficeGridCoordinate(seat.Cell.X + rightX, seat.Cell.Y + rightY))
            };
        }

        public static bool TrySelectCandidate(
            OfficeSeatSlot seat,
            Func<OfficeSeatEgressCandidate, bool> tryAccept,
            out OfficeSeatEgressCandidate selected)
        {
            if (tryAccept == null) throw new ArgumentNullException(nameof(tryAccept));
            IReadOnlyList<OfficeSeatEgressCandidate> candidates = ResolveCandidates(seat);
            for (var index = 0; index < candidates.Count; index++)
            {
                OfficeSeatEgressCandidate candidate = candidates[index];
                if (!tryAccept(candidate)) continue;
                selected = candidate;
                return true;
            }
            selected = default;
            return false;
        }
    }
}
