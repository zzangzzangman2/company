using System;
using System.Collections.Generic;
using FamilyCompany.Save.OfficeSeating;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public static class OfficeSeatingSaveValidation
    {
        private static readonly string[] KnownMembers =
        {
            "player", "older_sister", "father", "mother"
        };

        private static readonly string[] KnownSeats =
        {
            "desk_a", "desk_b", "desk_c", "desk_d"
        };

        [MenuItem("Family Company/Validate Office Seating Save")]
        public static void Run()
        {
            ValidateRoundTripAndFixedFields();
            ValidateInputOrderDeterminism();
            ValidateDuplicateAndEmptyRejection();
            ValidateV5MissingPayloadMigration();
            ValidateUnknownIdPolicies();
            Debug.Log("FAMILY_COMPANY_OFFICE_SEATING_SAVE_VALIDATION: PASS");
        }

        public static void RunBatch()
        {
            try
            {
                Run();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void ValidateRoundTripAndFixedFields()
        {
            var source = OfficeSeatingSaveAdapter.Capture(new[]
            {
                new OfficeSeatingAssignment(" mother ", " desk_b "),
                new OfficeSeatingAssignment("older_sister", "desk_a")
            });
            var dto = OfficeSeatingSaveAdapter.ToDto(source);
            var json = JsonUtility.ToJson(dto);
            RequireContains(json, "\"schemaVersion\":1", "sub-payload schema field");
            RequireContains(json, "\"seatAssignments\"", "assignment list field");
            RequireContains(json, "\"memberId\"", "member field");
            RequireContains(json, "\"seatId\"", "seat field");
            RequireNotContains(json, "reserved", "runtime reservation must not persist");
            RequireNotContains(json, "occupied", "runtime occupancy must not persist");
            RequireNotContains(json, "token", "runtime token must not persist");
            RequireNotContains(json, "transform", "Transform must not persist");
            RequireNotContains(json, "rotation", "rotation must not persist");
            RequireNotContains(json, "animationFrame", "animation frame must not persist");
            RequireNotContains(json, "selection", "UI selection must not persist");

            var restoredDto = JsonUtility.FromJson<OfficeSeatingSaveDto>(json);
            var restored = OfficeSeatingSaveAdapter.Restore(restoredDto, KnownMembers, KnownSeats);
            AssertEqual(2, restored.Count, "roundtrip assignment count");
            AssertAssignment(restored, 0, "mother", "desk_b");
            AssertAssignment(restored, 1, "older_sister", "desk_a");

            var futureEnvelope = new FutureGameSaveEnvelope
            {
                schemaVersion = 6,
                officeSeating = dto
            };
            var futureJson = JsonUtility.ToJson(futureEnvelope);
            RequireContains(
                futureJson,
                "\"" + OfficeSeatingSaveDto.FutureGameSaveFieldName + "\"",
                "future GameSaveDto field contract");
        }

        private static void ValidateInputOrderDeterminism()
        {
            var first = OfficeSeatingSaveAdapter.Capture(new[]
            {
                new OfficeSeatingAssignment("mother", "desk_d"),
                new OfficeSeatingAssignment("father", "desk_c"),
                new OfficeSeatingAssignment("older_sister", "desk_a")
            });
            var second = OfficeSeatingSaveAdapter.Capture(new[]
            {
                new OfficeSeatingAssignment("older_sister", "desk_a"),
                new OfficeSeatingAssignment("mother", "desk_d"),
                new OfficeSeatingAssignment("father", "desk_c")
            });
            var firstJson = JsonUtility.ToJson(OfficeSeatingSaveAdapter.ToDto(first));
            var secondJson = JsonUtility.ToJson(OfficeSeatingSaveAdapter.ToDto(second));
            AssertEqual(firstJson, secondJson, "input-order independent JSON");
        }

        private static void ValidateDuplicateAndEmptyRejection()
        {
            ExpectError(
                Dto(("mother", "desk_a"), (" mother ", "desk_b")),
                OfficeSeatingSaveError.DuplicateMember,
                "duplicate member after canonicalization");
            ExpectError(
                Dto(("mother", "desk_a"), ("father", "desk_a")),
                OfficeSeatingSaveError.DuplicateSeat,
                "duplicate seat");
            ExpectError(
                Dto((" ", "desk_a")),
                OfficeSeatingSaveError.EmptyMemberId,
                "empty member ID");
            ExpectError(
                Dto(("mother", "  ")),
                OfficeSeatingSaveError.EmptySeatId,
                "empty seat ID");
        }

        private static void ValidateV5MissingPayloadMigration()
        {
            var v5 = JsonUtility.FromJson<FutureGameSaveEnvelope>("{\"schemaVersion\":5}");
            AssertEqual(5, v5.schemaVersion, "legacy top-level schema");
            if (v5.officeSeating != null)
                throw new InvalidOperationException("A v5 payload unexpectedly created officeSeating data.");
            var migrated = OfficeSeatingSaveAdapter.Restore(v5.officeSeating, KnownMembers, KnownSeats);
            AssertEqual(0, migrated.Count, "v5 missing officeSeating migrates to empty");
        }

        private static void ValidateUnknownIdPolicies()
        {
            var unknownMember = Dto(("future_employee", "desk_b"));
            ExpectError(
                unknownMember,
                OfficeSeatingSaveError.UnknownMember,
                "unknown member reject policy");

            var unknownSeat = Dto(("mother", "future_desk"));
            ExpectError(
                unknownSeat,
                OfficeSeatingSaveError.UnknownSeat,
                "unknown seat reject policy");

            var mixed = Dto(
                ("future_employee", "desk_b"),
                ("mother", "desk_d"),
                ("father", "future_desk"),
                ("older_sister", "desk_a"));
            var skipped = OfficeSeatingSaveAdapter.Restore(
                mixed,
                KnownMembers,
                KnownSeats,
                UnknownOfficeSeatingIdPolicy.Skip);
            AssertEqual(2, skipped.Count, "unknown skip keeps only known assignments");
            AssertAssignment(skipped, 0, "mother", "desk_d");
            AssertAssignment(skipped, 1, "older_sister", "desk_a");
        }

        private static OfficeSeatingSaveDto Dto(
            params (string memberId, string seatId)[] assignments)
        {
            var dto = new OfficeSeatingSaveDto();
            foreach (var assignment in assignments)
            {
                dto.seatAssignments.Add(new OfficeSeatAssignmentSaveDto
                {
                    memberId = assignment.memberId,
                    seatId = assignment.seatId
                });
            }

            return dto;
        }

        private static void ExpectError(
            OfficeSeatingSaveDto dto,
            OfficeSeatingSaveError expected,
            string scenario)
        {
            try
            {
                OfficeSeatingSaveAdapter.Restore(dto, KnownMembers, KnownSeats);
            }
            catch (OfficeSeatingSaveValidationException exception)
            {
                AssertEqual(expected, exception.Error, scenario);
                return;
            }

            throw new InvalidOperationException(scenario + ": expected validation failure.");
        }

        private static void AssertAssignment(
            OfficeSeatingSnapshot snapshot,
            int index,
            string memberId,
            string seatId)
        {
            AssertEqual(memberId, snapshot.Assignments[index].MemberId, $"assignment {index} member");
            AssertEqual(seatId, snapshot.Assignments[index].SeatId, $"assignment {index} seat");
        }

        private static void RequireContains(string value, string expected, string scenario)
        {
            if (value == null || value.IndexOf(expected, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException(scenario + ": missing " + expected + ".");
        }

        private static void RequireNotContains(string value, string forbidden, string scenario)
        {
            if (value != null && value.IndexOf(forbidden, StringComparison.OrdinalIgnoreCase) >= 0)
                throw new InvalidOperationException(scenario + ": found " + forbidden + ".");
        }

        private static void AssertEqual<T>(T expected, T actual, string scenario)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidOperationException(
                    scenario + ": expected " + expected + ", actual " + actual + ".");
            }
        }

        [Serializable]
        private sealed class FutureGameSaveEnvelope
        {
            public int schemaVersion;
            public OfficeSeatingSaveDto officeSeating;
        }
    }
}
