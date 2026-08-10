using System;
using System.Collections.Generic;
using FamilyCompany.Presentation.Unity.OfficeSeating.Authoring;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FamilyCompany.Editor
{
    public static class OfficeSeatAuthoringValidation
    {
        [MenuItem("Family Company/Validate Office Seat Authoring")]
        public static void Run()
        {
            ValidatePureGeometry();
            ValidateAuthoringAndDefinition();
            ValidateFailureModes();
            ValidateRegistryDuplicateRejection();
            Debug.Log("FAMILY_COMPANY_OFFICE_SEAT_AUTHORING_VALIDATION: PASS");
        }

        private static void ValidatePureGeometry()
        {
            AssertEqual(OfficeSeatFacing8.South, OfficeSeatGeometryRules.QuantizeDirection(0f, -1f), "south");
            AssertEqual(OfficeSeatFacing8.Southwest, OfficeSeatGeometryRules.QuantizeDirection(-1f, -1f), "southwest");
            AssertEqual(OfficeSeatFacing8.West, OfficeSeatGeometryRules.QuantizeDirection(-1f, 0f), "west");
            AssertEqual(OfficeSeatFacing8.Northwest, OfficeSeatGeometryRules.QuantizeDirection(-1f, 1f), "northwest");
            AssertEqual(OfficeSeatFacing8.North, OfficeSeatGeometryRules.QuantizeDirection(0f, 1f), "north");
            AssertEqual(OfficeSeatFacing8.Northeast, OfficeSeatGeometryRules.QuantizeDirection(1f, 1f), "northeast");
            AssertEqual(OfficeSeatFacing8.East, OfficeSeatGeometryRules.QuantizeDirection(1f, 0f), "east");
            AssertEqual(OfficeSeatFacing8.Southeast, OfficeSeatGeometryRules.QuantizeDirection(1f, -1f), "southeast");
            if (new OfficeSeatPosition(float.NaN, 0f, 0f).IsFinite)
                throw new InvalidOperationException("NaN office seat position was accepted.");
            if (new OfficeSeatPosition(0f, float.PositiveInfinity, 0f).IsFinite)
                throw new InvalidOperationException("Infinite office seat position was accepted.");
        }

        private static void ValidateAuthoringAndDefinition()
        {
            using (var fixture = new SeatFixture("desk_a"))
            {
                var report = fixture.Authoring.ValidateAuthoring();
                if (report.HasErrors) throw new InvalidOperationException(report.FormatErrors());
                if (!fixture.Authoring.TryBuildDefinition(out var definition, out _))
                    throw new InvalidOperationException("Valid seat did not build a definition.");
                AssertEqual("desk_a", definition.SeatId, "definition seat ID");
                AssertEqual(OfficeSeatFacing8.North, definition.ResolvedFacing, "definition facing");
                AssertNear(0f, definition.LookDirectionX, 0.0001f, "look X");
                AssertNear(1f, definition.LookDirectionZ, 0.0001f, "look Z");
                AssertNear(0.65f, definition.ApproachPosition.FlatDistanceTo(definition.SitPosition), 0.001f,
                    "approach distance");
            }
        }

        private static void ValidateFailureModes()
        {
            var missingRoot = new GameObject("Missing Seat Authoring");
            try
            {
                var missing = missingRoot.AddComponent<OfficeSeatAuthoring>();
                var report = missing.ValidateAuthoring();
                RequireIssue(report, "empty_seat_id", OfficeSeatValidationSeverity.Error);
                RequireIssue(report, "missing_approach_anchor", OfficeSeatValidationSeverity.Error);
                RequireIssue(report, "missing_sit_anchor", OfficeSeatValidationSeverity.Error);
                RequireIssue(report, "missing_computer_look_target", OfficeSeatValidationSeverity.Error);
                RequireIssue(report, "missing_click_hotspot", OfficeSeatValidationSeverity.Error);
            }
            finally
            {
                Object.DestroyImmediate(missingRoot);
            }

            using (var fixture = new SeatFixture("desk_b"))
            {
                fixture.Look.localPosition = new Vector3(0f, 1f, 0.05f);
                RequireIssue(
                    fixture.Authoring.ValidateAuthoring(),
                    "computer_look_target_too_close",
                    OfficeSeatValidationSeverity.Error);
                fixture.Look.localPosition = new Vector3(0f, 1f, 1f);

                fixture.Approach.localPosition = new Vector3(0f, 0f, -3f);
                RequireIssue(
                    fixture.Authoring.ValidateAuthoring(),
                    "approach_sit_distance_out_of_range",
                    OfficeSeatValidationSeverity.Error);
                fixture.Approach.localPosition = new Vector3(0f, 0f, -0.65f);

                fixture.Hotspot.isTrigger = false;
                RequireIssue(
                    fixture.Authoring.ValidateAuthoring(),
                    "nontrigger_click_hotspot",
                    OfficeSeatValidationSeverity.Warning);
                fixture.Hotspot.isTrigger = true;

                fixture.Hotspot.enabled = false;
                RequireIssue(
                    fixture.Authoring.ValidateAuthoring(),
                    "disabled_click_hotspot",
                    OfficeSeatValidationSeverity.Error);
                fixture.Hotspot.enabled = true;

                fixture.Authoring.Configure(
                    "desk_b",
                    fixture.Approach,
                    fixture.Sit,
                    fixture.Look,
                    fixture.Hotspot,
                    OfficeSeatForegroundOcclusionMode.Default,
                    true,
                    OfficeSeatFacing8.South);
                RequireIssue(
                    fixture.Authoring.ValidateAuthoring(),
                    "expected_facing_mismatch",
                    OfficeSeatValidationSeverity.Error);
            }
        }

        private static void ValidateRegistryDuplicateRejection()
        {
            using (var first = new SeatFixture("desk_a"))
            using (var second = new SeatFixture("desk_a"))
            {
                var report = OfficeSeatRegistry.ValidateAuthoringCollection(new[]
                {
                    first.Authoring,
                    second.Authoring
                });
                RequireIssue(report, "duplicate_seat_id", OfficeSeatValidationSeverity.Error);
            }
        }

        private static void RequireIssue(
            OfficeSeatValidationReport report,
            string code,
            OfficeSeatValidationSeverity severity)
        {
            foreach (var issue in report.Issues)
            {
                if (issue.Code == code && issue.Severity == severity) return;
            }

            throw new InvalidOperationException($"Expected {severity} issue '{code}'.");
        }

        private static void AssertNear(float expected, float actual, float tolerance, string scenario)
        {
            if (Mathf.Abs(expected - actual) > tolerance)
                throw new InvalidOperationException($"{scenario}: expected {expected}, actual {actual}.");
        }

        private static void AssertEqual<T>(T expected, T actual, string scenario)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException($"{scenario}: expected {expected}, actual {actual}.");
        }

        private sealed class SeatFixture : IDisposable
        {
            public SeatFixture(string seatId)
            {
                Root = new GameObject("Seat Fixture " + seatId);
                Authoring = Root.AddComponent<OfficeSeatAuthoring>();
                Approach = Child("Approach", new Vector3(0f, 0f, -0.65f));
                Sit = Child("Sit", Vector3.zero);
                Look = Child("Computer Look", new Vector3(0f, 1f, 1f));
                Hotspot = Root.AddComponent<BoxCollider>();
                Hotspot.isTrigger = true;
                Authoring.Configure(
                    seatId,
                    Approach,
                    Sit,
                    Look,
                    Hotspot,
                    OfficeSeatForegroundOcclusionMode.Default,
                    true,
                    OfficeSeatFacing8.North);
            }

            public GameObject Root { get; }
            public OfficeSeatAuthoring Authoring { get; }
            public Transform Approach { get; }
            public Transform Sit { get; }
            public Transform Look { get; }
            public BoxCollider Hotspot { get; }

            public void Dispose()
            {
                if (Root != null) Object.DestroyImmediate(Root);
            }

            private Transform Child(string name, Vector3 localPosition)
            {
                var child = new GameObject(name).transform;
                child.SetParent(Root.transform, false);
                child.localPosition = localPosition;
                return child;
            }
        }
    }
}
