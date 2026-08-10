using System;
using System.Collections.Generic;
using FamilyCompany.Simulation.OfficeSeating;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeSeating.UI
{
    public interface IOfficeSeatHotspotProvider
    {
        string SeatId { get; }
        string SeatDisplayName { get; }
        bool OwnsSeatHotspot(Collider candidate);
    }

    public sealed class OfficeSeatSelection
    {
        public OfficeSeatSelection(string seatId, string displayName)
        {
            SeatId = OfficeSeatingUiIds.NormalizeRequired(seatId, nameof(seatId));
            var normalizedDisplayName = displayName == null ? string.Empty : displayName.Trim();
            DisplayName = normalizedDisplayName.Length == 0 ? SeatId : normalizedDisplayName;
        }

        public string SeatId { get; }
        public string DisplayName { get; }
    }

    public sealed class OfficeSeatPlacementMemberOption
    {
        public OfficeSeatPlacementMemberOption(string memberId, string displayName)
        {
            MemberId = OfficeSeatingUiIds.NormalizeRequired(memberId, nameof(memberId));
            DisplayName = OfficeSeatingUiIds.NormalizeRequired(displayName, nameof(displayName));
        }

        public string MemberId { get; }
        public string DisplayName { get; }
    }

    public sealed class OfficeSeatPlacementActionResult
    {
        internal OfficeSeatPlacementActionResult(
            bool succeeded,
            bool changed,
            OfficeSeatOperationFailure failure,
            string koreanMessage)
        {
            Succeeded = succeeded;
            Changed = changed;
            Failure = failure;
            KoreanMessage = koreanMessage ?? string.Empty;
        }

        public bool Succeeded { get; }
        public bool Changed { get; }
        public OfficeSeatOperationFailure Failure { get; }
        public string KoreanMessage { get; }
    }

    public sealed class OfficeSeatPlacementActions
    {
        private readonly OfficeSeatingState _state;

        public OfficeSeatPlacementActions(OfficeSeatingState state)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
        }

        public bool TryGetSeat(string seatId, out OfficeSeatView seat)
        {
            return _state.TryGetSeat(seatId, out seat);
        }

        public OfficeSeatPlacementActionResult TryAssign(string seatId, string memberId)
        {
            if (!_state.TryGetSeat(seatId, out var seat))
                return Rejected(OfficeSeatOperationFailure.UnknownSeat);
            if (!CanChangeAssignment(seat))
                return Rejected(OfficeSeatOperationFailure.SeatHasActiveClaim);

            if (_state.TryAssign(seat.SeatId, memberId, out var operation))
            {
                return new OfficeSeatPlacementActionResult(
                    true,
                    operation.Changed,
                    OfficeSeatOperationFailure.None,
                    operation.Changed ? "좌석 배정을 완료했습니다." : "이미 이 좌석에 배정되어 있습니다.");
            }

            return Rejected(operation.Failure);
        }

        public OfficeSeatPlacementActionResult TryUnassign(string seatId)
        {
            if (!_state.TryGetSeat(seatId, out var seat))
                return Rejected(OfficeSeatOperationFailure.UnknownSeat);
            if (!CanChangeAssignment(seat))
                return Rejected(OfficeSeatOperationFailure.SeatHasActiveClaim);
            if (string.IsNullOrEmpty(seat.AssignedMemberId))
            {
                return new OfficeSeatPlacementActionResult(
                    false,
                    false,
                    OfficeSeatOperationFailure.None,
                    "현재 해제할 장기 배정이 없습니다.");
            }

            if (_state.TryUnassign(seat.SeatId, seat.AssignedMemberId, out var operation))
            {
                return new OfficeSeatPlacementActionResult(
                    true,
                    operation.Changed,
                    OfficeSeatOperationFailure.None,
                    operation.Changed ? "좌석 배정을 해제했습니다." : "이미 배정이 해제되어 있습니다.");
            }

            return Rejected(operation.Failure);
        }

        public static bool CanChangeAssignment(OfficeSeatView seat)
        {
            return seat != null &&
                   seat.State != OfficeSeatMeaningState.Reserved &&
                   seat.State != OfficeSeatMeaningState.Occupied;
        }

        private static OfficeSeatPlacementActionResult Rejected(OfficeSeatOperationFailure failure)
        {
            return new OfficeSeatPlacementActionResult(
                false,
                false,
                failure,
                OfficeSeatKoreanText.Failure(failure));
        }
    }

    public static class OfficeSeatKoreanText
    {
        public static string State(OfficeSeatMeaningState state)
        {
            switch (state)
            {
                case OfficeSeatMeaningState.Assigned: return "배정됨";
                case OfficeSeatMeaningState.Reserved: return "이동 예약 중";
                case OfficeSeatMeaningState.Occupied: return "사용 중";
                default: return "미배정";
            }
        }

        public static string Failure(OfficeSeatOperationFailure failure)
        {
            switch (failure)
            {
                case OfficeSeatOperationFailure.InvalidSeatId: return "좌석 ID가 올바르지 않습니다.";
                case OfficeSeatOperationFailure.InvalidMemberId: return "구성원 ID가 올바르지 않습니다.";
                case OfficeSeatOperationFailure.InvalidToken: return "좌석 작업 token이 올바르지 않습니다.";
                case OfficeSeatOperationFailure.UnknownSeat: return "등록되지 않은 좌석입니다.";
                case OfficeSeatOperationFailure.SeatAssignedToOtherMember:
                    return "이미 다른 구성원에게 배정된 자리입니다. 먼저 기존 배정을 해제하세요.";
                case OfficeSeatOperationFailure.SeatHasActiveClaim:
                    return "예약 또는 사용 중인 자리는 배정하거나 해제할 수 없습니다.";
                case OfficeSeatOperationFailure.SeatClaimedByOtherMember:
                    return "다른 구성원이 이동하거나 사용 중인 자리입니다.";
                case OfficeSeatOperationFailure.MemberHasActiveClaim:
                    return "이 구성원은 이미 다른 좌석을 예약하거나 사용 중입니다.";
                case OfficeSeatOperationFailure.ReservationRequired:
                    return "좌석을 먼저 예약한 뒤 사용해야 합니다.";
                case OfficeSeatOperationFailure.SeatAlreadyOccupied: return "이미 사용 중인 자리입니다.";
                case OfficeSeatOperationFailure.TokenMismatch: return "현재 좌석 작업과 token이 일치하지 않습니다.";
                case OfficeSeatOperationFailure.TokenAlreadyActive: return "이 token은 이미 다른 좌석에서 사용 중입니다.";
                default: return "좌석 배정을 변경하지 못했습니다.";
            }
        }
    }

    public static class OfficeModalInputState
    {
        private static readonly object Gate = new object();
        private static readonly HashSet<long> ActiveLeaseIds = new HashSet<long>();
        private static long _nextLeaseId;

        public static event Action<bool> BlockStateChanged;

        public static bool IsInputBlocked
        {
            get
            {
                lock (Gate) return ActiveLeaseIds.Count > 0;
            }
        }

        public static int ActiveLeaseCount
        {
            get
            {
                lock (Gate) return ActiveLeaseIds.Count;
            }
        }

        public static OfficeModalInputLease Acquire(string reason)
        {
            var normalizedReason = string.IsNullOrWhiteSpace(reason) ? "office-modal" : reason.Trim();
            long leaseId;
            var notify = false;
            lock (Gate)
            {
                leaseId = checked(++_nextLeaseId);
                notify = ActiveLeaseIds.Count == 0;
                ActiveLeaseIds.Add(leaseId);
            }
            if (notify) BlockStateChanged?.Invoke(true);
            return new OfficeModalInputLease(leaseId, normalizedReason);
        }

        internal static void Release(long leaseId)
        {
            var notify = false;
            lock (Gate)
            {
                if (!ActiveLeaseIds.Remove(leaseId)) return;
                notify = ActiveLeaseIds.Count == 0;
            }
            if (notify) BlockStateChanged?.Invoke(false);
        }
    }

    public sealed class OfficeModalInputLease : IDisposable
    {
        private readonly long _leaseId;
        private bool _disposed;

        internal OfficeModalInputLease(long leaseId, string reason)
        {
            _leaseId = leaseId;
            Reason = reason;
        }

        public string Reason { get; }
        public bool IsDisposed => _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            OfficeModalInputState.Release(_leaseId);
        }
    }

    public sealed class OfficeSeatPlacementSession : IDisposable
    {
        private OfficeModalInputLease _inputLease;

        public bool IsOpen => _inputLease != null;
        public OfficeSeatSelection Selection { get; private set; }

        public void Open(OfficeSeatSelection selection)
        {
            Selection = selection ?? throw new ArgumentNullException(nameof(selection));
            if (_inputLease == null)
                _inputLease = OfficeModalInputState.Acquire("office-seat-placement");
        }

        public bool HandleEscape(bool escapePressed)
        {
            if (!escapePressed || !IsOpen) return false;
            Close();
            return true;
        }

        public void Close()
        {
            Selection = null;
            _inputLease?.Dispose();
            _inputLease = null;
        }

        public void Dispose()
        {
            Close();
        }
    }

    internal static class OfficeSeatingUiIds
    {
        public static string NormalizeRequired(string value, string parameterName)
        {
            var normalized = value == null ? string.Empty : value.Trim();
            if (normalized.Length == 0)
                throw new ArgumentException("A stable non-empty ordinal ID is required.", parameterName);
            return normalized;
        }
    }
}
