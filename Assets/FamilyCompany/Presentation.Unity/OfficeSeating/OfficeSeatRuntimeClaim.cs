using System;
using FamilyCompany.Simulation.OfficeSeating;

namespace FamilyCompany.Presentation.Unity.OfficeSeating
{
    /// <summary>
    /// Owns one transient reserve/occupy/release token against the pure seating state.
    /// The claim changes seat meaning only; it never advances autonomy needs or family stats.
    /// </summary>
    public sealed class OfficeSeatRuntimeClaim : IDisposable
    {
        private readonly OfficeSeatingState _state;

        private OfficeSeatRuntimeClaim(
            OfficeSeatingState state,
            string seatId,
            string memberId,
            string token)
        {
            _state = state;
            SeatId = seatId;
            MemberId = memberId;
            Token = token;
        }

        public string SeatId { get; }
        public string MemberId { get; }
        public string Token { get; }
        public bool IsOccupied { get; private set; }
        public bool IsReleased { get; private set; }

        public static bool TryReserve(
            OfficeSeatingState state,
            string seatId,
            string memberId,
            string token,
            out OfficeSeatRuntimeClaim claim,
            out OfficeSeatOperationResult result)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            claim = null;
            if (!state.TryReserve(seatId, memberId, token, out result)) return false;

            claim = new OfficeSeatRuntimeClaim(
                state,
                result.SeatId,
                result.MemberId,
                token.Trim());
            return true;
        }

        public bool TryOccupy(out OfficeSeatOperationResult result)
        {
            if (IsReleased)
            {
                result = null;
                return false;
            }

            if (!_state.TryOccupy(SeatId, MemberId, Token, out result)) return false;
            IsOccupied = true;
            return true;
        }

        public bool TryRelease(out OfficeSeatOperationResult result)
        {
            if (IsReleased)
            {
                result = null;
                return true;
            }

            if (!_state.TryRelease(SeatId, MemberId, Token, out result)) return false;
            IsOccupied = false;
            IsReleased = true;
            return true;
        }

        public void Dispose()
        {
            TryRelease(out _);
        }
    }
}
