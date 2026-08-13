using System;
using System.Collections.Generic;
using FamilyCompany.Simulation.Family;
using FamilyCompany.Simulation.OfficeInteractions;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public static class OfficeInteractionReservationBookValidation
    {
        private static readonly OfficeGridCoordinate CellA = new OfficeGridCoordinate(2, 3);
        private static readonly OfficeGridCoordinate CellB = new OfficeGridCoordinate(3, 2);
        private static readonly OfficeGridCoordinate CellC = new OfficeGridCoordinate(4, 3);

        [MenuItem("Family Company/Validate Office Interaction Reservations")]
        public static void Run()
        {
            ValidateExclusiveFurnitureIsSharedAcrossDefinitions();
            ValidateSharedCapacityAndDistinctApproachCells();
            ValidateTokenAndMemberIdempotency();
            ValidateUnsupportedPoliciesFailClosed();
            ValidateDefinitionOfferIntegrity();
            ValidateMemberCleanup();
            Debug.Log("OFFICE_INTERACTION_RESERVATION_BOOK_VALIDATION: PASS");
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

        private static void ValidateExclusiveFurnitureIsSharedAcrossDefinitions()
        {
            OfficeInteractionDefinition read = Definition("filing-read");
            OfficeInteractionDefinition file = Definition("filing-document");
            OfficeInteractionOffer readOffer = Offer(read, "bookcase", CellA, CellB);
            OfficeInteractionOffer fileOffer = Offer(file, "bookcase", CellA, CellB);
            var book = new OfficeInteractionReservationBook();

            Require(book.TryReserve(
                read, readOffer, "father", "read-token", CellA, out _, out var failure),
                "first exclusive bookcase claim should succeed");
            AssertEqual(OfficeInteractionReservationFailure.None, failure, "first exclusive failure");
            Require(!book.TryReserve(
                file, fileOffer, "mother", "file-token", CellB, out _, out failure),
                "different interaction IDs must share the same physical bookcase");
            AssertEqual(OfficeInteractionReservationFailure.CapacityReached, failure,
                "cross-definition exclusive capacity");
            AssertEqual(1, book.ActiveCountForFurniture("bookcase"), "bookcase active count");

            Require(book.TryRelease("read-token", out _), "exclusive release should succeed");
            Require(book.TryReserve(
                file, fileOffer, "mother", "file-token", CellB, out _, out failure),
                "released physical furniture should become claimable");
            AssertEqual(OfficeInteractionReservationFailure.None, failure, "claim after release");
        }

        private static void ValidateSharedCapacityAndDistinctApproachCells()
        {
            OfficeInteractionDefinition coffee = Definition("coffee-drink");
            OfficeInteractionOffer offer = Offer(coffee, "coffee", CellA, CellB, CellC);
            var book = new OfficeInteractionReservationBook();

            Require(book.TryReserve(
                coffee, offer, "mother", "coffee-mother", CellA, out var first, out _),
                "first shared-capacity claim should succeed");
            Require(book.TryReserve(
                coffee, offer, "father", "coffee-father", CellB, out _, out _),
                "second shared-capacity claim should succeed");
            AssertEqual(2, book.ActiveReservationCount, "shared capacity active count");

            Require(!book.TryReserve(
                coffee, offer, "older_sister", "coffee-same-cell", CellA, out _, out var failure),
                "an approach cell cannot be double-booked");
            AssertEqual(OfficeInteractionReservationFailure.ApproachCellOccupied, failure,
                "shared same-cell failure");
            Require(!book.TryReserve(
                coffee, offer, "older_sister", "coffee-third", CellC, out _, out failure),
                "shared furniture capacity two must reject a third member");
            AssertEqual(OfficeInteractionReservationFailure.CapacityReached, failure,
                "shared capacity failure");

            Require(book.TryRelease(first.Token, out var released), "shared claim release should succeed");
            Require(released.IsReleased, "released claim should expose terminal state");
            Require(book.TryRelease(first.Token, out var releasedAgain), "release must be idempotent");
            Require(ReferenceEquals(released, releasedAgain), "idempotent release must return the same claim");
            AssertEqual(1, book.ActiveReservationCount, "idempotent release active count");
            Require(book.TryReserve(
                coffee, offer, "older_sister", "coffee-third", CellC, out _, out failure),
                "released shared slot should become available");
            AssertEqual(OfficeInteractionReservationFailure.None, failure, "shared claim after release");
        }

        private static void ValidateTokenAndMemberIdempotency()
        {
            OfficeInteractionDefinition water = Definition("water-drink");
            OfficeInteractionOffer offer = Offer(water, "water", CellA, CellB);
            var book = new OfficeInteractionReservationBook();

            Require(book.TryReserve(
                water, offer, "father", "stable-token", CellA, out var original, out _),
                "original token claim should succeed");
            Require(book.TryReserve(
                water, offer, "father", "stable-token", CellA, out var repeated, out var failure),
                "identical token claim should be idempotent");
            Require(ReferenceEquals(original, repeated), "idempotent claim must return the same object");
            AssertEqual(1, book.ActiveReservationCount, "idempotent token active count");

            Require(!book.TryReserve(
                water, offer, "father", "stable-token", CellB, out _, out failure),
                "one token cannot silently change its approach cell");
            AssertEqual(OfficeInteractionReservationFailure.TokenConflict, failure, "token conflict");
            Require(!book.TryReserve(
                water, offer, "father", "second-token", CellB, out _, out failure),
                "one member cannot consume a second facility slot");
            AssertEqual(OfficeInteractionReservationFailure.MemberAlreadyReserved, failure,
                "member duplicate claim");

            Require(book.TryRelease("stable-token", out _), "stable token release should succeed");
            Require(!book.TryReserve(
                water, offer, "father", "stable-token", CellA, out _, out failure),
                "released tokens must not be reused");
            AssertEqual(OfficeInteractionReservationFailure.TokenAlreadyReleased, failure,
                "released token reuse");
            Require(!book.ContainsActiveToken("stable-token"), "released token must not remain active");
        }

        private static void ValidateUnsupportedPoliciesFailClosed()
        {
            var book = new OfficeInteractionReservationBook();
            OfficeInteractionDefinition assignedSeat = Definition("desk-typing");
            Require(!book.TryReserve(
                assignedSeat,
                Offer(assignedSeat, "desk_father", CellA),
                "father",
                "assigned-seat",
                CellA,
                out _,
                out var failure), "assigned seats must remain externally managed");
            AssertEqual(OfficeInteractionReservationFailure.UnsupportedReservationPolicy, failure,
                "assigned-seat policy");

            OfficeInteractionDefinition paired = Definition("lounge-chat");
            Require(!book.TryReserve(
                paired,
                Offer(paired, "sofa", CellA, CellB),
                "father",
                "paired-chat",
                CellA,
                out _,
                out failure), "paired conversations must fail closed until atomic group claims exist");
            AssertEqual(OfficeInteractionReservationFailure.UnsupportedReservationPolicy, failure,
                "paired policy");

            OfficeInteractionDefinition group = GroupMeetingDefinition();
            Require(!book.TryReserve(
                group,
                Offer(group, "meeting-table", CellA, CellB, CellC),
                "father",
                "group-meeting",
                CellA,
                out _,
                out failure), "group meetings must fail closed until atomic group claims exist");
            AssertEqual(OfficeInteractionReservationFailure.UnsupportedReservationPolicy, failure,
                "group policy");

            OfficeInteractionDefinition none = Definition("current-look");
            Require(!book.TryReserve(
                none,
                new OfficeInteractionOffer(
                    "current-look@virtual:father",
                    none.InteractionId,
                    string.Empty,
                    string.Empty,
                    none.SemanticLocation,
                    new[] { CellA },
                    none.Capacity),
                "father",
                "no-reservation",
                CellA,
                out _,
                out failure), "policy None should not create a claim");
            AssertEqual(OfficeInteractionReservationFailure.NoReservationRequired, failure,
                "no-reservation policy");
            AssertEqual(0, book.ActiveReservationCount, "unsupported policy mutation count");
        }

        private static void ValidateDefinitionOfferIntegrity()
        {
            OfficeInteractionDefinition water = Definition("water-drink");
            var book = new OfficeInteractionReservationBook();
            var wrongCapacity = new OfficeInteractionOffer(
                water.InteractionId + "@water",
                water.InteractionId,
                "water",
                water.FurnitureKindId,
                water.SemanticLocation,
                new[] { CellA },
                water.Capacity + 1);
            Require(!book.TryReserve(
                water, wrongCapacity, "father", "wrong-capacity", CellA, out _, out var failure),
                "definition/offer capacity mismatch must fail closed");
            AssertEqual(OfficeInteractionReservationFailure.DefinitionOfferMismatch, failure,
                "definition/offer mismatch");

            OfficeInteractionOffer valid = Offer(water, "water", CellA);
            Require(!book.TryReserve(
                water, valid, "father", "wrong-cell", CellB, out _, out failure),
                "a claim must use an advertised approach cell");
            AssertEqual(OfficeInteractionReservationFailure.ApproachCellNotOffered, failure,
                "unadvertised approach cell");
            AssertEqual(0, book.ActiveReservationCount, "integrity failure mutation count");
        }

        private static void ValidateMemberCleanup()
        {
            OfficeInteractionDefinition water = Definition("water-drink");
            OfficeInteractionDefinition filing = Definition("filing-read");
            var book = new OfficeInteractionReservationBook();
            Require(book.TryReserve(
                water, Offer(water, "water", CellA), "father", "father-water", CellA, out _, out _),
                "father cleanup fixture");
            Require(book.TryReserve(
                filing, Offer(filing, "bookcase", CellB), "mother", "mother-filing", CellB, out _, out _),
                "mother cleanup fixture");

            AssertEqual(1, book.ReleaseAllForMember("father"), "member cleanup release count");
            AssertEqual(0, book.ReleaseAllForMember("father"), "member cleanup idempotency");
            AssertEqual(1, book.ActiveReservationCount, "member cleanup must preserve other claims");
            Require(!book.IsApproachCellReserved(CellA), "released member cell should be free");
            Require(book.IsApproachCellReserved(CellB), "other member cell should remain reserved");
        }

        private static OfficeInteractionDefinition Definition(string interactionId)
        {
            if (!OfficeInteractionCatalog.TryGetDefinition(interactionId, out var definition))
                throw new InvalidOperationException("Unknown interaction definition: " + interactionId);
            return definition;
        }

        private static OfficeInteractionOffer Offer(
            OfficeInteractionDefinition definition,
            string furnitureId,
            params OfficeGridCoordinate[] cells)
        {
            return new OfficeInteractionOffer(
                definition.InteractionId + "@" + furnitureId,
                definition.InteractionId,
                furnitureId,
                definition.FurnitureKindId,
                definition.SemanticLocation,
                cells,
                definition.Capacity);
        }

        private static OfficeInteractionDefinition GroupMeetingDefinition()
        {
            return new OfficeInteractionDefinition(
                "test-group-meeting",
                OfficeMicroAction.PreparingMeeting,
                OfficeSemanticLocation.MeetingRoom,
                "meeting:test",
                "meeting_table",
                "PreparingMeeting",
                1,
                2,
                3,
                0,
                true,
                false,
                false,
                true,
                OfficeInteractionApproachPolicy.AdjacentCardinal,
                OfficeInteractionReservationPolicy.GroupMeeting,
                OfficeInteractionCandidateScope.StandardOfficeMacro,
                1,
                Array.Empty<KeyValuePair<FamilyRole, int>>(),
                new[] { AutonomousOfficeAction.Meeting });
        }

        private static void AssertEqual<T>(T expected, T actual, string scenario)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException($"{scenario}: expected {expected}, actual {actual}.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
