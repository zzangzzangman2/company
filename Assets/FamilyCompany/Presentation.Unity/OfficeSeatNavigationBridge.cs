using System;
using FamilyCompany.Presentation.Unity.OfficeSeating.Authoring;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity
{
    public sealed class OfficeSeatApproachRequest
    {
        public OfficeSeatApproachRequest(
            string requestId,
            string seatId,
            Vector3 approachPosition,
            Vector3 sitPosition,
            Vector3 lookDirection,
            OfficeSeatFacing8 facing)
        {
            RequestId = string.IsNullOrWhiteSpace(requestId)
                ? throw new ArgumentException("Request ID is required.", nameof(requestId))
                : requestId;
            SeatId = string.IsNullOrWhiteSpace(seatId)
                ? throw new ArgumentException("Seat ID is required.", nameof(seatId))
                : seatId;
            ApproachPosition = approachPosition;
            SitPosition = sitPosition;
            LookDirection = lookDirection;
            Facing = facing;
        }

        public string RequestId { get; }
        public string SeatId { get; }
        public Vector3 ApproachPosition { get; }
        public Vector3 SitPosition { get; }
        public Vector3 LookDirection { get; }
        public OfficeSeatFacing8 Facing { get; }
    }

    public readonly struct OfficeSeatApproachHandoff
    {
        public OfficeSeatApproachHandoff(
            string requestId,
            string seatId,
            Vector3 reachedFootpoint,
            Vector3 sitPosition,
            Vector3 lookDirection,
            OfficeSeatFacing8 facing)
        {
            RequestId = requestId;
            SeatId = seatId;
            ReachedFootpoint = reachedFootpoint;
            SitPosition = sitPosition;
            LookDirection = lookDirection;
            Facing = facing;
        }

        public string RequestId { get; }
        public string SeatId { get; }
        public Vector3 ReachedFootpoint { get; }
        public Vector3 SitPosition { get; }
        public Vector3 LookDirection { get; }
        public OfficeSeatFacing8 Facing { get; }
    }

    public static class OfficeSeatNavigationBridge
    {
        public static bool TryRequestApproach(
            OfficeWorkerAgent agent,
            OfficeSeatAuthoring seat,
            string requestId,
            Action<OfficeWorkerAgent, OfficeSeatApproachHandoff> onReady)
        {
            if (agent == null) throw new ArgumentNullException(nameof(agent));
            if (seat == null) throw new ArgumentNullException(nameof(seat));
            if (!seat.TryBuildDefinition(out var definition, out var report) || report.HasErrors)
                return false;
            var request = new OfficeSeatApproachRequest(
                requestId,
                definition.SeatId,
                ToVector(definition.ApproachPosition),
                ToVector(definition.SitPosition),
                new Vector3(definition.LookDirectionX, 0f, definition.LookDirectionZ),
                definition.ResolvedFacing);
            return agent.TryBeginSeatApproach(request, onReady);
        }

        private static Vector3 ToVector(OfficeSeatPosition value) => new Vector3(value.X, value.Y, value.Z);
    }
}
