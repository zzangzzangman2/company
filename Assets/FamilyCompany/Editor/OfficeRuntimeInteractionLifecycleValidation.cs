using System;
using System.Collections.Generic;
using FamilyCompany.Presentation.Unity.OfficeRuntime;
using FamilyCompany.Simulation.OfficeInteractions;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public static class OfficeRuntimeInteractionLifecycleValidation
    {
        private static readonly OfficeGridCoordinate CellA = new OfficeGridCoordinate(2, 3);
        private static readonly OfficeGridCoordinate CellB = new OfficeGridCoordinate(3, 2);
        private static readonly OfficeGridCoordinate CellC = new OfficeGridCoordinate(4, 3);

        [MenuItem("Family Company/Validate Office Runtime Interaction Lifecycle")]
        public static void Run()
        {
            ValidateDeterministicTraversalAndExclusiveFallback();
            ValidateSharedCapacityAndIdempotentTerminals();
            ValidateLiveArrivalFailureCleansUp();
            ValidateExistingAndUnsupportedOwnersStaySeparate();
            ValidateHandleOwnershipAndTokenGenerations();
            Debug.Log("OFFICE_RUNTIME_INTERACTION_LIFECYCLE_VALIDATION: PASS");
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

        private static void ValidateDeterministicTraversalAndExclusiveFallback()
        {
            OfficeInteractionDefinition water = Definition("water-drink");
            var source = new MutableOfferSource();
            source.Set(
                water.InteractionId,
                Offer(water, "water-z", CellC),
                Offer(water, "water-a", CellA));

            var firstService = new OfficeRuntimeInteractionLifecycleService(source.Resolve);
            var replayService = new OfficeRuntimeInteractionLifecycleService(source.Resolve);
            OfficeRuntimeInteractionRequest request = Request(water.InteractionId, "father", "deterministic");
            Require(firstService.TryBegin(request, out var first, out var failure),
                "first deterministic request should reserve");
            AssertEqual(OfficeRuntimeInteractionFailureCode.None, failure.Code, "first request failure");
            Require(replayService.TryBegin(request, out var replay, out _),
                "replayed deterministic request should reserve");
            AssertEqual(first.OfferId, replay.OfferId, "deterministic offer traversal");
            AssertEqual(first.ApproachCell, replay.ApproachCell, "deterministic cell traversal");

            OfficeRuntimeInteractionRequest secondRequest = Request(
                water.InteractionId,
                "mother",
                "deterministic");
            Require(firstService.TryBegin(secondRequest, out var second, out _),
                "exclusive contention should traverse to another actual furniture instance");
            Require(!string.Equals(first.FurnitureId, second.FurnitureId, StringComparison.Ordinal),
                "exclusive claims must not share one physical water dispenser");
            AssertEqual(2, firstService.ActiveReservationCount, "exclusive active count");
            firstService.AbortAll();
            replayService.AbortAll();
        }

        private static void ValidateSharedCapacityAndIdempotentTerminals()
        {
            OfficeInteractionDefinition coffee = Definition("coffee-drink");
            var source = new MutableOfferSource();
            source.Set(coffee.InteractionId, Offer(coffee, "coffee-main", CellA, CellB, CellC));
            var service = new OfficeRuntimeInteractionLifecycleService(source.Resolve);

            Require(service.TryBegin(Request(coffee.InteractionId, "father", "shared"), out var father, out _),
                "first shared claim");
            Require(service.TryBegin(Request(coffee.InteractionId, "mother", "shared"), out var mother, out _),
                "second shared claim");
            Require(!father.ApproachCell.Equals(mother.ApproachCell),
                "shared users need distinct approach cells");
            Require(!service.TryBegin(
                    Request(coffee.InteractionId, "older_sister", "shared"),
                    out _,
                    out var fullFailure),
                "third shared claim must fail capacity two");
            AssertEqual(
                OfficeRuntimeInteractionFailureCode.ReservationRejected,
                fullFailure.Code,
                "shared capacity failure");

            OfficeGridCoordinate wrongCell = father.ApproachCell.Equals(CellA) ? CellB : CellA;
            if (wrongCell.Equals(father.ApproachCell)) wrongCell = CellC;
            Require(!father.TryValidateArrival(wrongCell, out var mismatch),
                "arrival on a different approach cell must fail");
            AssertEqual(
                OfficeRuntimeInteractionFailureCode.ArrivalCellMismatch,
                mismatch.Code,
                "arrival mismatch failure");
            Require(father.IsActive, "early arrival validation must retain the reservation");
            Require(father.TryValidateArrival(father.ApproachCell, out _), "live arrival should validate");
            Require(father.TryComplete(out _), "first completion should succeed");
            Require(father.TryComplete(out _), "completion should be idempotent");
            Require(father.TryRelease(out _), "release after completion should be idempotent");
            AssertEqual(
                OfficeRuntimeInteractionHandleState.Completed,
                father.State,
                "release must preserve completed outcome");

            Require(mother.TryAbort(out _), "first abort should succeed");
            Require(mother.TryAbort(out _), "abort should be idempotent");
            Require(mother.TryRelease(out _), "release after abort should be idempotent");
            AssertEqual(0, service.ActiveReservationCount, "terminal cleanup active count");
        }

        private static void ValidateLiveArrivalFailureCleansUp()
        {
            OfficeInteractionDefinition filing = Definition("filing-read");
            var source = new MutableOfferSource();
            source.Set(filing.InteractionId, Offer(filing, "bookcase-main", CellA));
            var service = new OfficeRuntimeInteractionLifecycleService(source.Resolve);
            Require(service.TryBegin(Request(filing.InteractionId, "father", "live"), out var handle, out _),
                "live validation fixture should reserve");

            source.Set(filing.InteractionId);
            Require(!handle.TryValidateArrival(handle.ApproachCell, out var failure),
                "removed live furniture offer must fail at arrival");
            AssertEqual(
                OfficeRuntimeInteractionFailureCode.LiveOfferUnavailable,
                failure.Code,
                "removed offer failure");
            AssertEqual(OfficeRuntimeInteractionHandleState.Aborted, handle.State, "stale handle outcome");
            Require(handle.IsReleased, "stale live offer must release its reservation");
            Require(handle.TryAbort(out _), "automatic stale-offer abort should be idempotent");
            AssertEqual(0, service.ActiveReservationCount, "stale live offer cleanup count");

            source.Set(filing.InteractionId, Offer(filing, "bookcase-main", CellA));
            Require(service.TryBegin(Request(filing.InteractionId, "mother", "live"), out var replacement, out _),
                "stale cleanup should free the physical furniture");
            replacement.TryAbort(out _);
        }

        private static void ValidateExistingAndUnsupportedOwnersStaySeparate()
        {
            var source = new MutableOfferSource();
            var service = new OfficeRuntimeInteractionLifecycleService(source.Resolve);

            Require(!service.TryBegin(Request("current-look", "father", "none"), out _, out var none),
                "None policy belongs to the existing destination path");
            AssertEqual(
                OfficeRuntimeInteractionFailureCode.PolicyManagedByExistingPath,
                none.Code,
                "None policy routing");
            Require(!service.TryBegin(Request("desk-typing", "father", "seat"), out _, out var seat),
                "AssignedSeat policy belongs to seating");
            AssertEqual(
                OfficeRuntimeInteractionFailureCode.PolicyManagedByExistingPath,
                seat.Code,
                "AssignedSeat routing");
            Require(!service.TryBegin(Request("lounge-chat", "father", "pair"), out _, out var paired),
                "paired conversation must fail closed");
            AssertEqual(
                OfficeRuntimeInteractionFailureCode.UnsupportedReservationPolicy,
                paired.Code,
                "paired policy routing");
            Require(!service.TryBegin(Request("missing-interaction", "father", "missing"), out _, out var missing),
                "unknown interaction must fail closed");
            AssertEqual(
                OfficeRuntimeInteractionFailureCode.UnknownInteraction,
                missing.Code,
                "unknown interaction routing");
            AssertEqual(0, source.CallCount, "unsupported owners must not query live offers");
            AssertEqual(0, service.ActiveReservationCount, "unsupported owners must not reserve");
        }

        private static void ValidateHandleOwnershipAndTokenGenerations()
        {
            OfficeInteractionDefinition copier = Definition("copier-use");
            var source = new MutableOfferSource();
            source.Set(copier.InteractionId, Offer(copier, "copier-main", CellA));
            var service = new OfficeRuntimeInteractionLifecycleService(source.Resolve);
            var foreignService = new OfficeRuntimeInteractionLifecycleService(source.Resolve);
            OfficeRuntimeInteractionRequest request = Request(copier.InteractionId, "mother", "retry");

            Require(service.TryBegin(request, out var original, out _), "original handle should reserve");
            Require(service.TryBegin(request, out var repeated, out _), "repeated active request should resolve");
            Require(ReferenceEquals(original, repeated), "repeated active request must return one handle");
            Require(!foreignService.TryAbort(original, out var foreign), "foreign service must reject handle");
            AssertEqual(
                OfficeRuntimeInteractionFailureCode.ForeignHandle,
                foreign.Code,
                "foreign handle failure");

            string originalToken = original.Token;
            Require(original.TryRelease(out _), "explicit release should succeed");
            Require(original.TryRelease(out _), "explicit release should be idempotent");
            Require(service.TryBegin(request, out var retry, out _),
                "same stable intent can create a new attempt after cleanup");
            Require(!string.Equals(originalToken, retry.Token, StringComparison.Ordinal),
                "released reservation tokens must never be reused");
            retry.TryAbort(out _);
        }

        private static OfficeRuntimeInteractionRequest Request(
            string interactionId,
            string memberId,
            string stableKey)
        {
            return new OfficeRuntimeInteractionRequest(
                interactionId,
                memberId,
                stableKey,
                new OfficeGridCoordinate(1, 1));
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

        private static void AssertEqual<T>(T expected, T actual, string scenario)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException(
                    $"{scenario}: expected {expected}, actual {actual}.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private sealed class MutableOfferSource
        {
            private readonly Dictionary<string, IReadOnlyList<OfficeInteractionOffer>> _offers =
                new Dictionary<string, IReadOnlyList<OfficeInteractionOffer>>(StringComparer.Ordinal);

            public int CallCount { get; private set; }

            public void Set(string interactionId, params OfficeInteractionOffer[] offers)
            {
                _offers[interactionId] = offers ?? Array.Empty<OfficeInteractionOffer>();
            }

            public IReadOnlyList<OfficeInteractionOffer> Resolve(
                OfficeInteractionDefinition definition,
                string memberId,
                OfficeGridCoordinate start,
                string permittedSeatId,
                float radius)
            {
                CallCount++;
                return _offers.TryGetValue(
                    definition.InteractionId,
                    out IReadOnlyList<OfficeInteractionOffer> offers)
                    ? offers
                    : Array.Empty<OfficeInteractionOffer>();
            }
        }
    }
}
