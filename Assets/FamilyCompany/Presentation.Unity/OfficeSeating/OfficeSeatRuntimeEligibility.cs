using System;
using FamilyCompany.Simulation.OfficeSeating;

namespace FamilyCompany.Presentation.Unity.OfficeSeating
{
    /// <summary>
    /// Pure runtime eligibility rules shared by the player and NPC seating presenters.
    /// A partial topology must never gate a member that has no seat they can actually claim.
    /// </summary>
    public static class OfficeSeatRuntimeEligibility
    {
        public static bool HasClaimableSeat(
            OfficeSeatingState state,
            string memberId,
            Func<string, bool> hasUsableAuthoring)
        {
            if (state == null || string.IsNullOrWhiteSpace(memberId) || hasUsableAuthoring == null)
                return false;

            var normalizedMemberId = memberId.Trim();
            var seats = state.GetSeats();
            for (var index = 0; index < seats.Count; index++)
            {
                var seat = seats[index];
                if (seat == null || !hasUsableAuthoring(seat.SeatId)) continue;
                if (seat.State == OfficeSeatMeaningState.Reserved ||
                    seat.State == OfficeSeatMeaningState.Occupied)
                {
                    if (string.Equals(seat.RuntimeMemberId, normalizedMemberId, StringComparison.Ordinal))
                        return true;
                    continue;
                }

                if (string.IsNullOrEmpty(seat.AssignedMemberId) ||
                    string.Equals(seat.AssignedMemberId, normalizedMemberId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool SessionIdentityChanged(object boundState, object currentState)
        {
            return !ReferenceEquals(boundState, currentState);
        }
    }
}
