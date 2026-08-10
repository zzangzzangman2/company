using System;
using FamilyCompany.Presentation.Unity.OfficeSeating.UI;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeSeating.Authoring
{
    [DisallowMultipleComponent]
    public sealed class OfficeSeatAuthoring : MonoBehaviour, IOfficeSeatHotspotProvider
    {
        [SerializeField] private string seatId = string.Empty;
        [SerializeField] private string seatDisplayName = string.Empty;
        [SerializeField] private Transform approachAnchor;
        [SerializeField] private Transform sitAnchor;
        [SerializeField] private Transform computerLookTarget;
        [SerializeField] private OfficeWaypoint semanticDestination;
        [SerializeField] private Collider clickHotspot;
        [SerializeField] private OfficeSeatForegroundOcclusionMode foregroundOcclusionMode;
        [SerializeField] private bool validateExpectedFacing;
        [SerializeField] private OfficeSeatFacing8 expectedFacing = OfficeSeatFacing8.North;

        public string SeatId => (seatId ?? string.Empty).Trim();
        public string SeatDisplayName
        {
            get
            {
                var normalized = (seatDisplayName ?? string.Empty).Trim();
                return normalized.Length == 0 ? SeatId : normalized;
            }
        }
        public Transform ApproachAnchor => approachAnchor;
        public Transform SitAnchor => sitAnchor;
        public Transform ComputerLookTarget => computerLookTarget;
        public OfficeWaypoint SemanticDestination => semanticDestination;
        public Collider ClickHotspot => clickHotspot;
        public OfficeSeatForegroundOcclusionMode ForegroundOcclusionMode => foregroundOcclusionMode;
        public bool ValidateExpectedFacing => validateExpectedFacing;
        public OfficeSeatFacing8 ExpectedFacing => expectedFacing;
        public bool HasRuntimeAnchors =>
            approachAnchor != null && sitAnchor != null && computerLookTarget != null &&
            IsFinite(approachAnchor.position) && IsFinite(sitAnchor.position) &&
            IsFinite(computerLookTarget.position);
        public bool IsRuntimeValid => isActiveAndEnabled && HasRuntimeAnchors;

        public void Configure(
            string newSeatId,
            Transform newApproachAnchor,
            Transform newSitAnchor,
            Transform newComputerLookTarget,
            Collider newClickHotspot,
            OfficeSeatForegroundOcclusionMode newOcclusionMode =
                OfficeSeatForegroundOcclusionMode.Default,
            bool enforceExpectedFacing = false,
            OfficeSeatFacing8 newExpectedFacing = OfficeSeatFacing8.North,
            string newDisplayName = "",
            OfficeWaypoint newSemanticDestination = null)
        {
            seatId = newSeatId ?? string.Empty;
            seatDisplayName = newDisplayName ?? string.Empty;
            approachAnchor = newApproachAnchor;
            sitAnchor = newSitAnchor;
            computerLookTarget = newComputerLookTarget;
            semanticDestination = newSemanticDestination;
            clickHotspot = newClickHotspot;
            foregroundOcclusionMode = newOcclusionMode;
            validateExpectedFacing = enforceExpectedFacing;
            expectedFacing = newExpectedFacing;
        }

        public bool OwnsSeatHotspot(Collider candidate)
        {
            return candidate != null && candidate == clickHotspot;
        }

        public bool TryResolveFacing(out OfficeSeatFacing8 facing)
        {
            facing = OfficeSeatFacing8.South;
            if (sitAnchor == null || computerLookTarget == null ||
                !IsFinite(sitAnchor.position) || !IsFinite(computerLookTarget.position))
            {
                return false;
            }

            return OfficeSeatGeometryRules.TryResolveLookDirection(
                PositionOf(sitAnchor),
                PositionOf(computerLookTarget),
                out _,
                out _,
                out facing);
        }

        public OfficeSeatValidationReport ValidateAuthoring()
        {
            var report = new OfficeSeatValidationReport();
            var canonicalId = SeatId;
            if (canonicalId.Length == 0)
            {
                report.AddError("empty_seat_id", canonicalId, "Office seat ID cannot be empty.");
            }
            else if (!string.Equals(seatId, canonicalId, StringComparison.Ordinal))
            {
                report.AddError(
                    "noncanonical_seat_id",
                    canonicalId,
                    $"Office seat ID must not contain surrounding whitespace: '{seatId}'.");
            }

            ValidateAnchor(report, canonicalId, approachAnchor, "approach_anchor", "approach anchor");
            ValidateAnchor(report, canonicalId, sitAnchor, "sit_anchor", "sit anchor");
            ValidateAnchor(report, canonicalId, computerLookTarget, "computer_look_target", "computer look target");

            if (approachAnchor != null && sitAnchor != null &&
                IsFinite(approachAnchor.position) && IsFinite(sitAnchor.position))
            {
                var distance = PositionOf(approachAnchor).FlatDistanceTo(PositionOf(sitAnchor));
                if (distance < OfficeSeatGeometryRules.MinimumApproachToSitDistance ||
                    distance > OfficeSeatGeometryRules.MaximumApproachToSitDistance)
                {
                    report.AddError(
                        "approach_sit_distance_out_of_range",
                        canonicalId,
                        $"Approach-to-sit XZ distance must be {OfficeSeatGeometryRules.MinimumApproachToSitDistance:F2}.." +
                        $"{OfficeSeatGeometryRules.MaximumApproachToSitDistance:F2}m, found {distance:F3}m.");
                }
            }

            OfficeSeatFacing8 resolvedFacing = OfficeSeatFacing8.South;
            var facingResolved = false;
            if (sitAnchor != null && computerLookTarget != null &&
                IsFinite(sitAnchor.position) && IsFinite(computerLookTarget.position))
            {
                facingResolved = OfficeSeatGeometryRules.TryResolveLookDirection(
                    PositionOf(sitAnchor),
                    PositionOf(computerLookTarget),
                    out _,
                    out _,
                    out resolvedFacing);
                if (!facingResolved)
                {
                    report.AddError(
                        "computer_look_target_too_close",
                        canonicalId,
                        $"Computer look target must be at least {OfficeSeatGeometryRules.MinimumComputerLookDistance:F2}m " +
                        "away from the sit anchor in XZ.");
                }
            }

            if (!OfficeSeatGeometryRules.IsValidOcclusionMode(foregroundOcclusionMode))
            {
                report.AddError(
                    "invalid_foreground_occlusion_mode",
                    canonicalId,
                    $"Invalid foreground occlusion mode: {(int)foregroundOcclusionMode}.");
            }

            if (validateExpectedFacing)
            {
                if (!OfficeSeatGeometryRules.IsValidFacing(expectedFacing))
                {
                    report.AddError(
                        "invalid_expected_facing",
                        canonicalId,
                        $"Invalid expected facing: {(int)expectedFacing}.");
                }
                else if (facingResolved && resolvedFacing != expectedFacing)
                {
                    report.AddError(
                        "expected_facing_mismatch",
                        canonicalId,
                        $"Expected {expectedFacing}, resolved {resolvedFacing} from the computer look target.");
                }
            }

            ValidateHotspot(report, canonicalId);
            return report;
        }

        public bool TryBuildDefinition(
            out OfficeSeatDefinition definition,
            out OfficeSeatValidationReport report)
        {
            report = ValidateAuthoring();
            definition = null;
            if (report.HasErrors) return false;

            OfficeSeatGeometryRules.TryResolveLookDirection(
                PositionOf(sitAnchor),
                PositionOf(computerLookTarget),
                out var lookX,
                out var lookZ,
                out var resolvedFacing);
            definition = new OfficeSeatDefinition(
                SeatId,
                PositionOf(approachAnchor),
                PositionOf(sitAnchor),
                PositionOf(computerLookTarget),
                lookX,
                lookZ,
                resolvedFacing,
                foregroundOcclusionMode,
                validateExpectedFacing,
                expectedFacing);
            return true;
        }

        private void ValidateHotspot(OfficeSeatValidationReport report, string canonicalId)
        {
            if (clickHotspot == null)
            {
                report.AddError(
                    "missing_click_hotspot",
                    canonicalId,
                    "Office seat requires an explicitly authored click hotspot Collider.");
                return;
            }

            if (!clickHotspot.enabled)
            {
                report.AddError(
                    "disabled_click_hotspot",
                    canonicalId,
                    "Office seat click hotspot Collider must be enabled.");
            }

            if (!clickHotspot.isTrigger)
            {
                report.AddWarning(
                    "nontrigger_click_hotspot",
                    canonicalId,
                    "Office seat click hotspot should be a trigger to avoid blocking character movement.");
            }

            var hotspotTransform = clickHotspot.transform;
            if (hotspotTransform != transform && !hotspotTransform.IsChildOf(transform))
            {
                report.AddError(
                    "external_click_hotspot",
                    canonicalId,
                    "Office seat click hotspot must be on the seat object or one of its children.");
            }
        }

        private static void ValidateAnchor(
            OfficeSeatValidationReport report,
            string canonicalId,
            Transform anchor,
            string code,
            string label)
        {
            if (anchor == null)
            {
                report.AddError("missing_" + code, canonicalId, $"Office seat {label} is missing.");
                return;
            }

            if (!IsFinite(anchor.position))
            {
                report.AddError("nonfinite_" + code, canonicalId, $"Office seat {label} contains NaN or Infinity.");
            }
        }

        private static OfficeSeatPosition PositionOf(Transform value)
        {
            var position = value.position;
            return new OfficeSeatPosition(position.x, position.y, position.z);
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }
}
