using System;
using System.Collections.Generic;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime
{
    public readonly struct OfficeSeatEgressAnchor
    {
        public OfficeSeatEgressAnchor(
            OfficeSeatEgressKind kind,
            OfficeGridCoordinate cell,
            Vector3 world)
        {
            if (kind == OfficeSeatEgressKind.None) throw new ArgumentOutOfRangeException(nameof(kind));
            Kind = kind;
            Cell = cell;
            World = world;
        }

        public OfficeSeatEgressKind Kind { get; }
        public OfficeGridCoordinate Cell { get; }
        public Vector3 World { get; }
    }

    /// <summary>
    /// Complete runtime socket contract for one seat. All values are derived from the semantic
    /// layout and approved furniture/pose catalogs; no scene child Transform owns another copy.
    /// </summary>
    public readonly struct OfficeSeatInteractionAnchors
    {
        public OfficeSeatInteractionAnchors(
            string seatId,
            Vector3 approachWorld,
            Vector3 alignmentWorld,
            Vector3 pelvisWorld,
            bool hasHandWorld,
            Vector3 handWorld,
            IReadOnlyList<OfficeSeatEgressAnchor> egress)
        {
            SeatId = string.IsNullOrWhiteSpace(seatId)
                ? throw new ArgumentException("Seat ID cannot be empty.", nameof(seatId))
                : seatId.Trim();
            ApproachWorld = approachWorld;
            AlignmentWorld = alignmentWorld;
            PelvisWorld = pelvisWorld;
            HasHandWorld = hasHandWorld;
            HandWorld = handWorld;
            Egress = egress ?? throw new ArgumentNullException(nameof(egress));
            if (Egress.Count != OfficeSeatEgressRules.CandidateCount)
                throw new ArgumentException("A seat must expose front/left/right egress anchors.", nameof(egress));
        }

        public string SeatId { get; }
        public Vector3 ApproachWorld { get; }
        public Vector3 AlignmentWorld { get; }
        public Vector3 PelvisWorld { get; }
        public bool HasHandWorld { get; }
        public Vector3 HandWorld { get; }
        public IReadOnlyList<OfficeSeatEgressAnchor> Egress { get; }
    }
}
