using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using FamilyCompany.Simulation.OfficeSeating;

namespace FamilyCompany.Presentation.Unity.OfficeSeating
{
    /// <summary>
    /// Owns one transient reserve/occupy/release token against the pure seating state.
    /// The claim changes seat meaning only; it never advances autonomy needs or family stats.
    /// </summary>
    public sealed class OfficeSeatRuntimeClaim : IDisposable
    {
        private static readonly ConditionalWeakTable<OfficeSeatingState, ClaimRegistry> Registries =
            new ConditionalWeakTable<OfficeSeatingState, ClaimRegistry>();

        private readonly OfficeSeatingState _state;
        private readonly ClaimRegistry _registry;

        private OfficeSeatRuntimeClaim(
            OfficeSeatingState state,
            string seatId,
            string memberId,
            string token,
            ClaimRegistry registry)
        {
            _state = state;
            _registry = registry;
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

            var normalizedToken = token.Trim();
            var registry = Registries.GetValue(state, _ => new ClaimRegistry());
            lock (registry.Claims)
            {
                if (registry.Claims.TryGetValue(normalizedToken, out var weak) &&
                    weak.TryGetTarget(out var existing) &&
                    !existing.IsReleased &&
                    string.Equals(existing.SeatId, result.SeatId, StringComparison.Ordinal) &&
                    string.Equals(existing.MemberId, result.MemberId, StringComparison.Ordinal))
                {
                    claim = existing;
                    return true;
                }

                claim = new OfficeSeatRuntimeClaim(
                    state,
                    result.SeatId,
                    result.MemberId,
                    normalizedToken,
                    registry);
                registry.Claims[normalizedToken] = new WeakReference<OfficeSeatRuntimeClaim>(claim);
            }
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

        public bool TryPrepareOccupy(out OfficeSeatingState.PreparedRuntimeMutation prepared)
        {
            prepared = default;
            return !IsReleased && !IsOccupied &&
                   _state.TryPrepareRuntimeOccupy(SeatId, MemberId, Token, out prepared);
        }

        public bool TryPrepareRelease(out OfficeSeatingState.PreparedRuntimeMutation prepared)
        {
            prepared = default;
            return !IsReleased && IsOccupied &&
                   _state.TryPrepareRuntimeRelease(SeatId, MemberId, Token, out prepared);
        }

        public bool IsPreparedMutationCurrent(
            in OfficeSeatingState.PreparedRuntimeMutation prepared) =>
            !IsReleased && _state.IsPreparedRuntimeMutationCurrent(prepared);

        public void CommitPreparedOccupy(
            in OfficeSeatingState.PreparedRuntimeMutation prepared)
        {
            _state.CommitPreparedRuntimeOccupy(prepared);
            IsOccupied = true;
        }

        public void CommitPreparedRelease(
            in OfficeSeatingState.PreparedRuntimeMutation prepared)
        {
            _state.CommitPreparedRuntimeRelease(prepared);
            IsOccupied = false;
            IsReleased = true;
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
            lock (_registry.Claims)
            {
                if (_registry.Claims.TryGetValue(Token, out var weak) &&
                    weak.TryGetTarget(out var owner) && ReferenceEquals(owner, this))
                {
                    _registry.Claims.Remove(Token);
                }
            }
            return true;
        }

        public void Dispose()
        {
            TryRelease(out _);
        }

        private sealed class ClaimRegistry
        {
            public readonly Dictionary<string, WeakReference<OfficeSeatRuntimeClaim>> Claims =
                new Dictionary<string, WeakReference<OfficeSeatRuntimeClaim>>(StringComparer.Ordinal);
        }
    }
}
