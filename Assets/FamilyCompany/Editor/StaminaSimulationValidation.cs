using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FamilyCompany.Simulation.Stamina;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
#endif

namespace FamilyCompany.Editor
{
    public static class StaminaSimulationValidation
    {
        private const int ValidationSeed = 20000103;
        private static readonly string[] FamilyIds =
            { "player", "older_sister", "father", "mother" };

#if UNITY_EDITOR
        [MenuItem("Family Company/Validate Common Stamina Simulation")]
        public static void Run()
        {
            try
            {
                RunAll();
                Debug.Log("FAMILY_COMPANY_STAMINA_SIMULATION_VALIDATION: PASS");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("FAMILY_COMPANY_STAMINA_SIMULATION_VALIDATION: FAIL");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }
#endif

        public static int Main()
        {
            try
            {
                RunAll();
                Console.WriteLine("FAMILY_COMPANY_STAMINA_PURE_HARNESS: PASS");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                Console.Error.WriteLine("FAMILY_COMPANY_STAMINA_PURE_HARNESS: FAIL");
                return 1;
            }
        }

        public static void RunAll()
        {
            ValidateCommonCatalogAndOverrides();
            ValidateExactThresholdBoundary();
            ValidateAuthoritativeActivitySegmentsAndPause();
            ValidateThresholdYieldAndLargeJumps();
            ValidateAtomicRosterClock();
            ValidateDeterministicSemanticPlanning();
            ValidateCompletionOnlyRecoveryLifecycle();
            ValidateFailureCorrelationAndReturnRetry();
            ValidateRuntimeLossRestoreNormalization();
            ValidateStableSnapshotAndProfileFingerprint();
            ValidateLegacyMigrationAndFutureEmployeeRoster();
            ValidateSemanticSnapshotBoundary();
            ValidateInputAndOverflowGuards();
        }

        private static void ValidateCommonCatalogAndOverrides()
        {
            CharacterStaminaCatalog catalog = CharacterStaminaCatalog.CreateCommonDefault();
            CharacterStaminaProfile common = catalog.DefaultProfile;
            AssertEqual(10_000, common.MaxUnits, "default max");
            AssertEqual(10_000, common.InitialUnits, "default initial");
            AssertEqual(2_500, common.RecoveryThresholdUnits, "default 25 percent threshold");
            AssertEqual(3_500, common.ResumeThresholdUnits, "default 35 percent resume floor");
            AssertEqual(5_000, common.CautionThresholdUnits, "default caution threshold");
            AssertEqual(16, common.DrainUnitsPerGameMinute(StaminaActivityKind.Typing),
                "typing drain");
            AssertTrue(common.Recovery(StaminaRecoveryActivity.Water)
                    .SupportsInteractionId("vending-drink"),
                "water recovery supports placed DrinkVending capability");
            AssertEqual(16, common.DrainUnitsPerGameMinute(StaminaActivityKind.Meeting),
                "meeting drain");
            AssertEqual(3_500,
                common.Recovery(StaminaRecoveryActivity.Water).MaximumRecoveryUnits,
                "water recovery total");
            AssertEqual(4_000,
                common.Recovery(StaminaRecoveryActivity.Restroom).MaximumRecoveryUnits,
                "restroom recovery total");
            AssertEqual(5_000,
                common.Recovery(StaminaRecoveryActivity.Lounge).MaximumRecoveryUnits,
                "lounge recovery total");

            var roster = new CharacterStaminaRoster(ValidationSeed, catalog, FamilyIds);
            foreach (string memberId in FamilyIds)
            {
                CharacterStaminaReadSnapshot read = roster.GetSimulation(memberId).Read();
                AssertEqual(10_000, read.MaxUnits, memberId + " common max");
                AssertEqual(10_000, read.CurrentUnits, memberId + " common current");
                AssertEqual(10_000, read.RatioBasisPoints, memberId + " full ratio");
            }

            CharacterStaminaProfile overrideProfile = new CharacterStaminaProfile(
                12_000,
                11_000,
                2_500,
                3_500,
                5_000,
                common.DrainDefinitions,
                new[]
                {
                    new StaminaRecoveryDefinition(
                        StaminaRecoveryActivity.Water, "water-drink", 4, 1_100, 35),
                    new StaminaRecoveryDefinition(
                        StaminaRecoveryActivity.Restroom, "restroom-use", 8, 550, 30),
                    new StaminaRecoveryDefinition(
                        StaminaRecoveryActivity.Lounge, "lounge-rest", 20, 250, 35)
                });
            var overrides = new[]
            {
                new KeyValuePair<string, CharacterStaminaProfile>(
                    "employee_special",
                    overrideProfile)
            };
            var overrideCatalog = new CharacterStaminaCatalog(common, overrides);
            AssertSame(overrideProfile, overrideCatalog.Resolve("employee_special"),
                "catalog override");
            AssertSame(common, overrideCatalog.Resolve("employee_future"),
                "future employee fallback");
        }

        private static void ValidateExactThresholdBoundary()
        {
            CharacterStaminaCatalog catalog = CharacterStaminaCatalog.CreateCommonDefault();
            CharacterStaminaSimulation at26 = CharacterStaminaSimulation.CreateAt(
                ValidationSeed,
                "at26",
                catalog,
                0,
                2_600);
            StaminaAdvanceResult before = at26.AdvanceTo(0);
            AssertFalse(before.RequiresRuntimeDecision, "26 percent creates no departure intent");
            AssertEqual(StaminaRecoveryPhase.Working, at26.State.RecoveryPhase,
                "26 percent remains working");

            CharacterStaminaSimulation at25 = CharacterStaminaSimulation.CreateAt(
                ValidationSeed,
                "at25",
                catalog,
                0,
                2_500);
            StaminaAdvanceResult threshold = at25.AdvanceTo(0);
            AssertTrue(threshold.RequiresRuntimeDecision, "25 percent requests recovery");
            AssertEqual(1, threshold.Transitions.Count, "single threshold transition");
            AssertEqual(StaminaRecoveryPhase.RecoveryRequested, at25.State.RecoveryPhase,
                "25 percent departure intent phase");

            CharacterStaminaProfile common = catalog.DefaultProfile;
            CharacterStaminaProfile oddMax = new CharacterStaminaProfile(
                101,
                101,
                2_500,
                3_500,
                5_000,
                common.DrainDefinitions,
                common.RecoveryDefinitions);
            var oddCatalog = new CharacterStaminaCatalog(oddMax);
            CharacterStaminaSimulation odd26 = CharacterStaminaSimulation.CreateAt(
                ValidationSeed, "odd26", oddCatalog, 0, 26);
            AssertFalse(odd26.AdvanceTo(0).RequiresRuntimeDecision,
                "26/101 remains above exact threshold");
            CharacterStaminaSimulation odd25 = CharacterStaminaSimulation.CreateAt(
                ValidationSeed, "odd25", oddCatalog, 0, 25);
            AssertTrue(odd25.AdvanceTo(0).RequiresRuntimeDecision,
                "25/101 is below exact threshold");

            CharacterStaminaSimulation outside = CharacterStaminaSimulation.CreateAt(
                ValidationSeed, "outside", catalog, 0, 2_500);
            outside.SetActivity(StaminaActivityKind.OffDuty, 0);
            outside.AdvanceTo(30, allowOfficeRecoveryRequest: false);
            AssertEqual(StaminaRecoveryPhase.Working, outside.State.RecoveryPhase,
                "outside character creates no office recovery intent");
            AssertTrue(outside.AdvanceTo(30, allowOfficeRecoveryRequest: true)
                    .RequiresRuntimeDecision,
                "same GameTime can evaluate office eligibility without drain");
        }

        private static void ValidateAuthoritativeActivitySegmentsAndPause()
        {
            CharacterStaminaCatalog catalog = CharacterStaminaCatalog.CreateCommonDefault();
            CharacterStaminaSimulation segmented = CharacterStaminaSimulation.CreateDefault(
                ValidationSeed, "segmented", catalog);
            segmented.SetActivity(StaminaActivityKind.DeskWork, 0);
            segmented.AdvanceTo(30, allowOfficeRecoveryRequest: false);
            segmented.SetActivity(StaminaActivityKind.Meeting, 30);
            segmented.AdvanceTo(60, allowOfficeRecoveryRequest: false);
            AssertEqual(9_160, segmented.State.CurrentUnits,
                "30 desk plus 30 meeting uses exact segments");

            CharacterStaminaSimulation perMinute = CharacterStaminaSimulation.CreateDefault(
                ValidationSeed, "per-minute", catalog);
            perMinute.SetActivity(StaminaActivityKind.DeskWork, 0);
            for (long minute = 1; minute <= 30; minute++)
                perMinute.AdvanceTo(minute, allowOfficeRecoveryRequest: false);
            perMinute.SetActivity(StaminaActivityKind.Meeting, 30);
            for (long minute = 31; minute <= 60; minute++)
                perMinute.AdvanceTo(minute, allowOfficeRecoveryRequest: false);
            AssertEqual(segmented.State.CurrentUnits, perMinute.State.CurrentUnits,
                "partition independent segmented activity drain");

            CharacterStaminaSimulation framed = CharacterStaminaSimulation.CreateDefault(
                ValidationSeed, "framed", catalog);
            framed.SetActivity(StaminaActivityKind.Typing, 0);
            for (int frame = 0; frame < 240; frame++)
                framed.AdvanceTo(frame / 4, allowOfficeRecoveryRequest: false);
            CharacterStaminaSimulation direct = CharacterStaminaSimulation.CreateDefault(
                ValidationSeed, "direct", catalog);
            direct.SetActivity(StaminaActivityKind.Typing, 0);
            direct.AdvanceTo(59, allowOfficeRecoveryRequest: false);
            AssertEqual(direct.State.CurrentUnits, framed.State.CurrentUnits,
                "frame partition does not change GameTime drain");

            int pausedBefore = framed.State.CurrentUnits;
            long pausedMinute = framed.State.LastProcessedMinute;
            for (int frame = 0; frame < 600; frame++)
                framed.AdvanceTo(pausedMinute, allowOfficeRecoveryRequest: false);
            AssertEqual(pausedBefore, framed.State.CurrentUnits,
                "paused GameTime changes stamina by zero");
        }

        private static void ValidateThresholdYieldAndLargeJumps()
        {
            CharacterStaminaCatalog catalog = CharacterStaminaCatalog.CreateCommonDefault();
            CharacterStaminaSimulation jump = CharacterStaminaSimulation.CreateAt(
                ValidationSeed, "jump", catalog, 0, 2_600);
            jump.SetActivity(StaminaActivityKind.Typing, 0);
            StaminaAdvanceResult boundary = jump.AdvanceTo(1_000);
            AssertTrue(boundary.RequiresRuntimeDecision, "long jump yields for recovery intent");
            AssertFalse(boundary.ReachedRequestedMinute, "long jump stops at first decision");
            AssertEqual(7L, boundary.ProcessedToMinute, "threshold crossing minute");
            AssertEqual(2_488, jump.State.CurrentUnits, "threshold crossing drain");

            CharacterStaminaSimulation minuteSteps = CharacterStaminaSimulation.CreateAt(
                ValidationSeed, "minute-steps", catalog, 0, 2_600);
            minuteSteps.SetActivity(StaminaActivityKind.Typing, 0);
            StaminaAdvanceResult stepResult = null;
            for (long minute = 1; minute <= 1_000; minute++)
            {
                stepResult = minuteSteps.AdvanceTo(minute);
                if (stepResult.RequiresRuntimeDecision) break;
            }
            AssertEqual(jump.State.CurrentUnits, minuteSteps.State.CurrentUnits,
                "direct and minute threshold boundary match");
            AssertEqual(jump.State.LastProcessedMinute, minuteSteps.State.LastProcessedMinute,
                "direct and minute boundary time match");

            CharacterStaminaSimulation multiYear = CharacterStaminaSimulation.CreateDefault(
                ValidationSeed, "multi-year", catalog);
            multiYear.SetActivity(StaminaActivityKind.Typing, 0);
            multiYear.AdvanceTo(20_000_000, allowOfficeRecoveryRequest: false);
            AssertEqual(0, multiYear.State.CurrentUnits, "large jump drains arithmetically to zero");
            AssertEqual(20_000_000L, multiYear.State.LastProcessedMinute,
                "large jump reaches target");

            CharacterStaminaSimulation maximum = CharacterStaminaSimulation.CreateDefault(
                ValidationSeed, "maximum", catalog);
            maximum.SetActivity(StaminaActivityKind.OffDuty, 0);
            maximum.AdvanceTo(long.MaxValue, allowOfficeRecoveryRequest: false);
            maximum.AdvanceTo(long.MaxValue, allowOfficeRecoveryRequest: false);
            AssertEqual(long.MaxValue, maximum.State.LastProcessedMinute,
                "long max same-minute no-op does not overflow");
        }

        private static void ValidateAtomicRosterClock()
        {
            CharacterStaminaCatalog catalog = CharacterStaminaCatalog.CreateCommonDefault();
            var roster = CharacterStaminaRoster.MigrateLegacyEnergyPercents(
                ValidationSeed,
                catalog,
                new[]
                {
                    new KeyValuePair<string, int>("low", 25),
                    new KeyValuePair<string, int>("high", 100)
                },
                0);
            roster.SetActivitiesAtCurrentMinute(_ => StaminaActivityKind.Typing);
            CharacterStaminaRosterAdvanceResult result = roster.AdvanceAllTo(100);
            AssertEqual(0L, result.ProcessedToMinute,
                "roster yields before partially advancing another member");
            AssertTrue(result.RequiresRuntimeDecision, "roster reports low member decision");
            AssertEqual(0L, roster.GetSimulation("high").State.LastProcessedMinute,
                "high member remains on roster clock");
            AssertEqual(StaminaRecoveryPhase.RecoveryRequested,
                roster.GetSimulation("low").State.RecoveryPhase,
                "low member requested recovery");

            AssertThrows<InvalidOperationException>(
                () => roster.AddCharacter("late", 1),
                "future employee must join at current roster GameTime");
            roster.AddCharacter("new_employee", roster.LastProcessedMinute);

            CharacterStaminaRosterSnapshotDto invalidClock = roster.ExportSnapshot();
            invalidClock.characters[0].lastProcessedMinute++;
            AssertThrows<InvalidOperationException>(
                () => CharacterStaminaRoster.Restore(invalidClock, catalog),
                "roster restore rejects mismatched member clocks");

            long before = roster.LastProcessedMinute;
            AssertThrows<InvalidOperationException>(
                () => roster.AdvanceAllTo(before - 1),
                "roster rejects backward time before mutation");
            AssertEqual(before, roster.LastProcessedMinute, "backward failure is atomic");

            var fresh = new CharacterStaminaRoster(
                ValidationSeed, catalog, new[] { "a", "z" });
            AssertThrows<ArgumentOutOfRangeException>(
                () => fresh.SetActivitiesAtCurrentMinute(id =>
                    id == "z" ? StaminaActivityKind.None : StaminaActivityKind.Typing),
                "activity delegate is preflighted");
            AssertEqual(StaminaActivityKind.Idle,
                fresh.GetSimulation("a").State.CurrentActivity,
                "failed activity batch mutates no earlier member");
        }

        private static void ValidateDeterministicSemanticPlanning()
        {
            CharacterStaminaCatalog catalog = CharacterStaminaCatalog.CreateCommonDefault();
            StaminaRecoveryCandidate[] candidates = AllCandidates();
            CharacterStaminaSimulation firstSimulation = RequestedSimulation("planner", catalog);
            var adapter = new FakeCapabilityQueryAdapter(17, candidates);
            AssertTrue(StaminaRecoveryPlanner.TrySelect(
                    firstSimulation, adapter, out StaminaRecoveryPlan first),
                "capability adapter selects a semantic interaction");
            AssertEqual(1, adapter.QueryCount, "capability adapter queried once");
            AssertEqual(firstSimulation.CharacterId, adapter.LastQuery.CharacterId,
                "capability query character correlation");
            AssertEqual(firstSimulation.RecoveryRequestKey,
                adapter.LastQuery.RecoveryRequestKey,
                "capability query recovery correlation");
            AssertEqual(firstSimulation.State.LastProcessedMinute,
                adapter.LastQuery.GameTimeMinute,
                "capability query authoritative GameTime minute");
            AssertTrue(StaminaRecoveryPlanner.TrySelect(
                    firstSimulation, candidates.Reverse(), out StaminaRecoveryPlan reversed),
                "reversed planner input selects");
            AssertPlanEqual(first, reversed, "candidate order independence");
            AssertTrue(typeof(StaminaRecoveryPlan).GetProperty("FacilityId") == null,
                "pure plan does not double-own a concrete facility");
            AssertTrue(typeof(StaminaRecoveryPlan).GetProperty("RuntimeFurnitureInstanceId") == null,
                "pure plan does not pin capability-query instance output");

            var mismatchedAdapter = new FakeCapabilityQueryAdapter(18, candidates)
            {
                ReturnMismatchedContext = true
            };
            AssertThrows<InvalidOperationException>(
                () => StaminaRecoveryPlanner.TrySelect(firstSimulation, mismatchedAdapter, out _),
                "capability query context mismatch fails closed");

            var observed = new HashSet<StaminaRecoveryActivity>();
            for (int index = 0; index < 2_000 && observed.Count < 3; index++)
            {
                CharacterStaminaSimulation simulation = RequestedSimulation(
                    "planner-" + index, catalog);
                AssertTrue(StaminaRecoveryPlanner.TrySelect(
                        simulation, candidates, out StaminaRecoveryPlan plan),
                    "weighted planner selection " + index);
                observed.Add(plan.Activity);
            }
            AssertEqual(3, observed.Count, "deterministic weighted selection reaches all activities");

            StaminaRecoveryCandidate[] onlyRestroom = candidates.Select(item =>
                new StaminaRecoveryCandidate(
                    item.Activity,
                    item.InteractionId,
                    item.RuntimeFurnitureInstanceId,
                    item.IsReachable,
                    item.Activity == StaminaRecoveryActivity.Restroom)).ToArray();
            CharacterStaminaSimulation fallback = RequestedSimulation("fallback", catalog);
            AssertTrue(StaminaRecoveryPlanner.TrySelect(
                    fallback, onlyRestroom, out StaminaRecoveryPlan restroom),
                "planner filters live capacity input");
            AssertEqual(StaminaRecoveryActivity.Restroom, restroom.Activity,
                "only usable semantic activity selected");

            StaminaRecoveryCandidate[] none = candidates.Select(item =>
                new StaminaRecoveryCandidate(
                    item.Activity,
                    item.InteractionId,
                    item.RuntimeFurnitureInstanceId,
                    false,
                    false)).ToArray();
            AssertFalse(StaminaRecoveryPlanner.TrySelect(fallback, none, out _),
                "no usable offer creates no plan");
        }

        private static void ValidateCompletionOnlyRecoveryLifecycle()
        {
            CharacterStaminaCatalog catalog = CharacterStaminaCatalog.CreateCommonDefault();
            foreach (StaminaRecoveryActivity activity in new[]
                     {
                         StaminaRecoveryActivity.Water,
                         StaminaRecoveryActivity.Restroom,
                         StaminaRecoveryActivity.Lounge
                     })
            {
                CharacterStaminaSimulation simulation = RequestedSimulation(
                    "lifecycle-" + activity, catalog);
                StaminaRecoveryPlan plan = SelectOnly(simulation, activity);
                string key = plan.RequestKey;
                simulation.AcceptRecoveryPlan(plan, 0);
                AssertEqual(StaminaRecoveryPhase.SafeStopping, simulation.State.RecoveryPhase,
                    activity + " safe stop");
                simulation.ConfirmSafeStopCompleted(key, 0);
                simulation.ConfirmStandUpCompleted(key, 0);
                simulation.SetActivity(StaminaActivityKind.Walking, 0);

                simulation.AdvanceTo(2);
                int afterTravel = simulation.State.CurrentUnits;
                AssertEqual(2_492, afterTravel, activity + " travel drains but never recovers");
                simulation.ConfirmFacilityArrived(key, 2);
                simulation.SetActivity(StaminaActivityKind.Idle, 2);
                simulation.ConfirmFacingAlignedAndPerforming(key, 2);
                AssertFalse(simulation.CanCompleteRuntimeInteraction(key, 2),
                    activity + " runtime must not release claim before duration");

                int duration = simulation.Profile.Recovery(activity).DurationGameMinutes;
                StaminaAdvanceResult performing = simulation.AdvanceTo(2 + duration);
                AssertTrue(performing.RequiresRuntimeDecision,
                    activity + " yields when performance duration completes");
                AssertEqual(afterTravel, simulation.State.CurrentUnits,
                    activity + " partial/ready performance grants zero before Complete");
                AssertTrue(simulation.IsRecoveryReadyToComplete,
                    activity + " completion readiness");
                AssertTrue(simulation.CanCompleteRuntimeInteraction(
                        key, simulation.State.LastProcessedMinute),
                    activity + " runtime Complete preflight succeeds at duration");
                long readyMinute = simulation.State.LastProcessedMinute;
                StaminaAdvanceResult stickyReady = simulation.AdvanceTo(readyMinute + 50);
                AssertTrue(stickyReady.RequiresRuntimeDecision,
                    activity + " ready decision remains sticky until Complete");
                AssertEqual(readyMinute, stickyReady.ProcessedToMinute,
                    activity + " ready state cannot skip its Complete boundary");

                StaminaTransition completed = simulation.ConfirmInteractionCompleted(
                    key, simulation.State.LastProcessedMinute);
                int expectedRecovery = simulation.Profile.Recovery(activity).MaximumRecoveryUnits;
                AssertEqual(expectedRecovery, completed.UnitsDelta,
                    activity + " completion applies configured recovery once");
                AssertEqual(afterTravel + expectedRecovery, simulation.State.CurrentUnits,
                    activity + " recovered value");
                AssertEqual(StaminaRecoveryPhase.ReturningToAssignedSeat,
                    simulation.State.RecoveryPhase,
                    activity + " forced assigned-seat return");

                string returnKey = simulation.AssignedSeatReturnRequestKey;
                simulation.SetActivity(StaminaActivityKind.Walking,
                    simulation.State.LastProcessedMinute);
                simulation.AdvanceTo(simulation.State.LastProcessedMinute + 1);
                simulation.ConfirmAssignedSeatReturned(
                    returnKey, simulation.State.LastProcessedMinute);
                AssertEqual(StaminaRecoveryPhase.Working, simulation.State.RecoveryPhase,
                    activity + " assigned-seat return completes lifecycle");
                AssertFalse(simulation.AdvanceTo(simulation.State.LastProcessedMinute)
                        .RequiresRuntimeDecision,
                    activity + " recovery does not immediately bounce below threshold");
            }
        }

        private static void ValidateFailureCorrelationAndReturnRetry()
        {
            CharacterStaminaCatalog catalog = CharacterStaminaCatalog.CreateCommonDefault();
            CharacterStaminaSimulation selection = RequestedSimulation("selection", catalog);
            string failedKey = selection.RecoveryRequestKey;
            selection.RecordRecoverySelectionFailure(
                failedKey,
                StaminaRecoveryAbortReason.ReservationUnavailable,
                0);
            AssertNotEqual(failedKey, selection.RecoveryRequestKey,
                "failed atomic claim changes deterministic retry key");
            AssertFalse(selection.State.CanCreateDepartureIntent,
                "selection backoff suppresses same-minute departure intent");
            var backoffAdapter = new FakeCapabilityQueryAdapter(19, AllCandidates());
            AssertFalse(StaminaRecoveryPlanner.TrySelect(
                    selection, backoffAdapter, out _),
                "selection backoff rejects same-minute replanning");
            AssertEqual(0, backoffAdapter.QueryCount,
                "selection backoff does not query furniture capabilities");
            AssertThrows<InvalidOperationException>(
                () => selection.RecordRecoverySelectionFailure(
                    failedKey,
                    StaminaRecoveryAbortReason.ReservationUnavailable,
                    0),
                "stale selection failure is rejected");

            StaminaAdvanceResult retryReady = selection.AdvanceTo(100);
            AssertTrue(retryReady.RequiresRuntimeDecision,
                "selection retry yields at deterministic retry minute");
            AssertEqual(1L, retryReady.ProcessedToMinute,
                "selection failure uses one GameTime minute backoff");
            AssertTrue(selection.State.CanCreateDepartureIntent,
                "departure intent becomes available at retry minute");
            StaminaRecoveryPlan plan = SelectOnly(selection, StaminaRecoveryActivity.Water);
            string activeKey = plan.RequestKey;
            selection.AcceptRecoveryPlan(plan, 1);
            selection.ConfirmSafeStopCompleted(activeKey, 1);
            selection.ConfirmStandUpCompleted(activeKey, 1);
            selection.ConfirmFacilityArrived(activeKey, 1);
            selection.ConfirmFacingAlignedAndPerforming(activeKey, 1);
            selection.AdvanceTo(2);
            int beforeAbort = selection.State.CurrentUnits;
            selection.AbortRecoveryPlan(
                activeKey,
                StaminaRecoveryAbortReason.LayoutChanged,
                2);
            AssertEqual(beforeAbort, selection.State.CurrentUnits,
                "aborted partial performance applies no recovery");
            AssertEqual(StaminaRecoveryPhase.RecoveryRequested,
                selection.State.RecoveryPhase,
                "runtime terminal abort returns to reselection");
            AssertNotEqual(activeKey, selection.RecoveryRequestKey,
                "aborted request gets new correlation key");

            CharacterStaminaSimulation returning = CompleteAtFacility(
                "return-retry", catalog, StaminaRecoveryActivity.Restroom);
            string firstReturnKey = returning.AssignedSeatReturnRequestKey;
            returning.RequestAssignedSeatReturnRetry(
                firstReturnKey,
                StaminaRecoveryAbortReason.AssignedSeatUnavailable,
                returning.State.LastProcessedMinute);
            string retryKey = returning.AssignedSeatReturnRequestKey;
            AssertNotEqual(firstReturnKey, retryKey, "return retry gets new correlation key");
            AssertThrows<InvalidOperationException>(
                () => returning.ConfirmAssignedSeatReturned(
                    firstReturnKey, returning.State.LastProcessedMinute),
                "stale assigned-seat completion is rejected");
            returning.ConfirmAssignedSeatReturned(retryKey, returning.State.LastProcessedMinute);
            AssertEqual(StaminaRecoveryPhase.Working, returning.State.RecoveryPhase,
                "retry can finish assigned-seat return");
        }

        private static void ValidateRuntimeLossRestoreNormalization()
        {
            CharacterStaminaCatalog catalog = CharacterStaminaCatalog.CreateCommonDefault();

            CharacterStaminaSimulation traveling = RequestedSimulation("load-travel", catalog);
            StaminaRecoveryPlan travelPlan = SelectOnly(
                traveling, StaminaRecoveryActivity.Restroom);
            string travelKey = travelPlan.RequestKey;
            traveling.AcceptRecoveryPlan(travelPlan, 0);
            traveling.ConfirmSafeStopCompleted(travelKey, 0);
            traveling.ConfirmStandUpCompleted(travelKey, 0);
            traveling.SetActivity(StaminaActivityKind.Walking, 0);
            traveling.AdvanceTo(4);
            int travelUnits = traveling.State.CurrentUnits;
            int travelAttempt = traveling.State.SelectionAttempt;
            CharacterStaminaSimulation restoredTravel = CharacterStaminaSimulation.Restore(
                traveling.ExportSnapshot(), catalog);
            AssertEqual(StaminaRecoveryPhase.RecoveryRequested,
                restoredTravel.State.RecoveryPhase,
                "travel load releases transient plan and reselects");
            AssertEqual(travelAttempt + 1, restoredTravel.State.SelectionAttempt,
                "travel load advances retry attempt");
            AssertEqual(StaminaRecoveryActivity.None,
                restoredTravel.State.RecoveryActivity,
                "travel load forgets transient interaction choice");
            restoredTravel.SetActivity(StaminaActivityKind.Idle,
                restoredTravel.State.LastProcessedMinute);
            long restoredTravelMinute = restoredTravel.State.LastProcessedMinute;
            StaminaAdvanceResult travelSticky = restoredTravel.AdvanceTo(
                restoredTravelMinute + 20);
            AssertTrue(travelSticky.RequiresRuntimeDecision,
                "travel load reselection is sticky");
            AssertEqual(restoredTravelMinute, travelSticky.ProcessedToMinute,
                "travel load cannot skip live reacquisition decision");
            AssertEqual(travelUnits, restoredTravel.State.CurrentUnits,
                "travel load cannot recover before live reacquisition");

            CharacterStaminaSimulation performing = RequestedSimulation("load-perform", catalog);
            StaminaRecoveryPlan performPlan = SelectOnly(
                performing, StaminaRecoveryActivity.Lounge);
            string performKey = performPlan.RequestKey;
            performing.AcceptRecoveryPlan(performPlan, 0);
            performing.ConfirmSafeStopCompleted(performKey, 0);
            performing.ConfirmStandUpCompleted(performKey, 0);
            performing.ConfirmFacilityArrived(performKey, 0);
            performing.ConfirmFacingAlignedAndPerforming(performKey, 0);
            performing.AdvanceTo(5);
            int beforeLoad = performing.State.CurrentUnits;
            CharacterStaminaSimulation restoredPerform = CharacterStaminaSimulation.Restore(
                performing.ExportSnapshot(), catalog);
            AssertEqual(StaminaRecoveryPhase.RecoveryRequested,
                restoredPerform.State.RecoveryPhase,
                "performing load requires a new actual arrival");
            AssertEqual(0, restoredPerform.State.RecoveryMinutesApplied,
                "performing load discards pending uncommitted grant");
            long restoredPerformMinute = restoredPerform.State.LastProcessedMinute;
            StaminaAdvanceResult performSticky = restoredPerform.AdvanceTo(
                restoredPerformMinute + 100);
            AssertTrue(performSticky.RequiresRuntimeDecision,
                "performing load reselection is sticky");
            AssertEqual(restoredPerformMinute, performSticky.ProcessedToMinute,
                "performing load cannot skip fresh live interaction");
            AssertEqual(beforeLoad, restoredPerform.State.CurrentUnits,
                "performing load grants zero without Complete");

            StaminaRecoveryPlan reacquired = SelectOnly(
                restoredPerform, StaminaRecoveryActivity.Water);
            string reacquiredKey = reacquired.RequestKey;
            long minute = restoredPerform.State.LastProcessedMinute;
            restoredPerform.AcceptRecoveryPlan(reacquired, minute);
            restoredPerform.ConfirmSafeStopCompleted(reacquiredKey, minute);
            restoredPerform.ConfirmStandUpCompleted(reacquiredKey, minute);
            restoredPerform.ConfirmFacilityArrived(reacquiredKey, minute);
            restoredPerform.ConfirmFacingAlignedAndPerforming(reacquiredKey, minute);
            restoredPerform.AdvanceTo(
                minute + restoredPerform.Profile.Recovery(StaminaRecoveryActivity.Water)
                    .DurationGameMinutes);
            restoredPerform.ConfirmInteractionCompleted(
                reacquiredKey, restoredPerform.State.LastProcessedMinute);
            AssertTrue(restoredPerform.State.CurrentUnits > beforeLoad,
                "reacquired actual Complete finally recovers");
        }

        private static void ValidateStableSnapshotAndProfileFingerprint()
        {
            CharacterStaminaCatalog catalog = CharacterStaminaCatalog.CreateCommonDefault();
            CharacterStaminaSimulation working = CharacterStaminaSimulation.CreateDefault(
                ValidationSeed, "working-save", catalog);
            working.SetActivity(StaminaActivityKind.DeskWork, 0);
            working.AdvanceTo(120, allowOfficeRecoveryRequest: false);
            CharacterStaminaSnapshotDto workingSnapshot = working.ExportSnapshot();
            CharacterStaminaSimulation workingRestored = CharacterStaminaSimulation.Restore(
                workingSnapshot, catalog);
            AssertSnapshotEqual(workingSnapshot, workingRestored.ExportSnapshot(),
                "stable working roundtrip");

            CharacterStaminaSimulation returning = CompleteAtFacility(
                "return-save", catalog, StaminaRecoveryActivity.Water);
            CharacterStaminaSnapshotDto returnSnapshot = returning.ExportSnapshot();
            CharacterStaminaSimulation returnRestored = CharacterStaminaSimulation.Restore(
                returnSnapshot, catalog);
            AssertSnapshotEqual(returnSnapshot, returnRestored.ExportSnapshot(),
                "completed return roundtrip");
            returnRestored.ConfirmAssignedSeatReturned(
                returnRestored.AssignedSeatReturnRequestKey,
                returnRestored.State.LastProcessedMinute);

            CharacterStaminaProfile common = catalog.DefaultProfile;
            StaminaDrainDefinition[] changedDrain = common.DrainDefinitions.Select(item =>
                new StaminaDrainDefinition(
                    item.Activity,
                    item.Activity == StaminaActivityKind.Typing
                        ? item.UnitsPerGameMinute + 1
                        : item.UnitsPerGameMinute)).ToArray();
            var changedCatalog = new CharacterStaminaCatalog(new CharacterStaminaProfile(
                common.MaxUnits,
                common.InitialUnits,
                common.RecoveryThresholdBasisPoints,
                common.ResumeThresholdBasisPoints,
                common.CautionThresholdBasisPoints,
                changedDrain,
                common.RecoveryDefinitions));
            AssertThrows<InvalidOperationException>(
                () => CharacterStaminaSimulation.Restore(workingSnapshot, changedCatalog),
                "same max but changed profile fingerprint requires migration");

            CharacterStaminaSimulation left = CharacterStaminaSimulation.CreateDefault(
                ValidationSeed, "deterministic", catalog);
            CharacterStaminaSimulation right = CharacterStaminaSimulation.CreateDefault(
                ValidationSeed, "deterministic", catalog);
            left.SetActivity(StaminaActivityKind.Administration, 0);
            right.SetActivity(StaminaActivityKind.Administration, 0);
            left.AdvanceTo(50, allowOfficeRecoveryRequest: false);
            for (long minute = 1; minute <= 50; minute++)
                right.AdvanceTo(minute, allowOfficeRecoveryRequest: false);
            AssertSnapshotEqual(left.ExportSnapshot(), right.ExportSnapshot(),
                "same GameTime timeline is deterministic");
        }

        private static void ValidateLegacyMigrationAndFutureEmployeeRoster()
        {
            CharacterStaminaCatalog catalog = CharacterStaminaCatalog.CreateCommonDefault();
            var legacy = FamilyIds.Concat(new[] { "employee_future" })
                .Select(id => new KeyValuePair<string, int>(id, 80));
            CharacterStaminaRoster roster = CharacterStaminaRoster.MigrateLegacyEnergyPercents(
                ValidationSeed,
                catalog,
                legacy,
                777);
            AssertEqual(5, roster.Count, "future employee migrates without a role switch");
            AssertEqual(777L, roster.LastProcessedMinute, "legacy roster seeds save time");
            foreach (string characterId in roster.CharacterIds)
            {
                CharacterStaminaReadSnapshot read = roster.GetSimulation(characterId).Read();
                AssertEqual(8_000, read.CurrentUnits, characterId + " legacy percent conversion");
                AssertEqual(777L, read.LastProcessedMinute, characterId + " no historical replay");
            }

            CharacterStaminaRosterSnapshotDto snapshot = roster.ExportSnapshot();
            CharacterStaminaRoster restored = CharacterStaminaRoster.Restore(snapshot, catalog);
            AssertEqual(roster.Count, restored.Count, "roster save roundtrip count");
            AssertEqual(roster.LastProcessedMinute, restored.LastProcessedMinute,
                "roster save roundtrip clock");
            foreach (string characterId in roster.CharacterIds)
                AssertSnapshotEqual(
                    roster.GetSimulation(characterId).ExportSnapshot(),
                    restored.GetSimulation(characterId).ExportSnapshot(),
                    characterId + " roster roundtrip");
        }

        private static void ValidateSemanticSnapshotBoundary()
        {
            string[] forbidden =
            {
                "claim", "offer", "facility", "instance", "capability", "path", "route",
                "transform", "gameobject"
            };
            foreach (FieldInfo field in typeof(CharacterStaminaSnapshotDto).GetFields(
                         BindingFlags.Instance | BindingFlags.Public))
            {
                string lower = field.Name.ToLowerInvariant();
                foreach (string token in forbidden)
                    AssertFalse(lower.Contains(token),
                        "character snapshot excludes transient " + token + " field");
            }
            foreach (FieldInfo field in typeof(CharacterStaminaRosterSnapshotDto).GetFields(
                         BindingFlags.Instance | BindingFlags.Public))
            {
                string lower = field.Name.ToLowerInvariant();
                foreach (string token in forbidden)
                    AssertFalse(lower.Contains(token),
                        "roster snapshot excludes transient " + token + " field");
            }
            AssertTrue(typeof(StaminaRecoveryPlan).GetProperty("FacilityId") == null,
                "semantic planner cannot pin a physical claim target");
            AssertTrue(typeof(StaminaRecoveryPlan).GetProperty("RuntimeFurnitureInstanceId") == null,
                "semantic planner cannot save or pin a queried furniture instance");
        }

        private static void ValidateInputAndOverflowGuards()
        {
            CharacterStaminaCatalog catalog = CharacterStaminaCatalog.CreateCommonDefault();
            CharacterStaminaProfile common = catalog.DefaultProfile;
            CharacterStaminaSimulation simulation = CharacterStaminaSimulation.CreateDefault(
                ValidationSeed, "guards", catalog);
            AssertThrows<ArgumentOutOfRangeException>(
                () => simulation.SetActivity(StaminaActivityKind.None, 0),
                "missing activity mapping fails closed");
            simulation.AdvanceTo(10, allowOfficeRecoveryRequest: false);
            AssertThrows<InvalidOperationException>(
                () => simulation.AdvanceTo(9, allowOfficeRecoveryRequest: false),
                "backward simulation time rejected");

            AssertThrows<ArgumentOutOfRangeException>(
                () => new StaminaRecoveryDefinition(
                    StaminaRecoveryActivity.Water,
                    "overflow",
                    int.MaxValue,
                    2,
                    1),
                "recovery product overflow rejected at data construction");
            AssertThrows<ArgumentException>(
                () => new CharacterStaminaProfile(
                    10_000,
                    10_000,
                    2_500,
                    3_500,
                    5_000,
                    common.DrainDefinitions.Concat(new StaminaDrainDefinition[] { null }),
                    common.RecoveryDefinitions),
                "null drain produces intentional validation exception");

            CharacterStaminaSnapshotDto invalid = simulation.ExportSnapshot();
            invalid.recoveryPhase = (int)StaminaRecoveryPhase.SafeStopping;
            invalid.recoveryActivity = (int)StaminaRecoveryActivity.Water;
            invalid.requestSequence = 1;
            invalid.recoveryMinutesApplied = 1;
            AssertThrows<InvalidOperationException>(
                () => CharacterStaminaSimulation.Restore(invalid, catalog),
                "pre-performing progress snapshot rejected");

            CharacterStaminaProfile huge = new CharacterStaminaProfile(
                int.MaxValue,
                int.MaxValue,
                2_500,
                3_500,
                5_000,
                common.DrainDefinitions,
                new[]
                {
                    new StaminaRecoveryDefinition(
                        StaminaRecoveryActivity.Water, "water-drink", 1, 800_000_000, 1),
                    new StaminaRecoveryDefinition(
                        StaminaRecoveryActivity.Restroom, "restroom-use", 1, 800_000_000, 1),
                    new StaminaRecoveryDefinition(
                        StaminaRecoveryActivity.Lounge, "lounge-rest", 1, 800_000_000, 1)
                });
            var hugeCatalog = new CharacterStaminaCatalog(huge);
            CharacterStaminaSimulation hugeSimulation = CharacterStaminaSimulation.CreateAt(
                ValidationSeed, "huge", hugeCatalog, 0, 0);
            hugeSimulation.AdvanceTo(0);
            StaminaRecoveryPlan hugePlan = SelectOnly(
                hugeSimulation, StaminaRecoveryActivity.Water);
            string hugeKey = hugePlan.RequestKey;
            hugeSimulation.AcceptRecoveryPlan(hugePlan, 0);
            hugeSimulation.ConfirmSafeStopCompleted(hugeKey, 0);
            hugeSimulation.ConfirmStandUpCompleted(hugeKey, 0);
            hugeSimulation.ConfirmFacilityArrived(hugeKey, 0);
            hugeSimulation.ConfirmFacingAlignedAndPerforming(hugeKey, 0);
            hugeSimulation.AdvanceTo(1);
            hugeSimulation.ConfirmInteractionCompleted(hugeKey, 1);
            AssertEqual(800_000_000, hugeSimulation.State.CurrentUnits,
                "large valid recovery uses non-overflowing long arithmetic");

            var drainList = (IList<StaminaDrainDefinition>)common.DrainDefinitions;
            AssertThrows<NotSupportedException>(
                () => drainList[0] = new StaminaDrainDefinition(StaminaActivityKind.Idle, 0),
                "profile drain view is immutable");
        }

        private static CharacterStaminaSimulation RequestedSimulation(
            string characterId,
            CharacterStaminaCatalog catalog)
        {
            CharacterStaminaSimulation simulation = CharacterStaminaSimulation.CreateAt(
                ValidationSeed,
                characterId,
                catalog,
                0,
                catalog.Resolve(characterId).RecoveryThresholdUnits);
            StaminaAdvanceResult result = simulation.AdvanceTo(0);
            AssertTrue(result.RequiresRuntimeDecision, characterId + " request setup");
            return simulation;
        }

        private static StaminaRecoveryPlan SelectOnly(
            CharacterStaminaSimulation simulation,
            StaminaRecoveryActivity activity)
        {
            StaminaRecoveryDefinition definition = simulation.Profile.Recovery(activity);
            var candidates = new[]
            {
                new StaminaRecoveryCandidate(
                    activity,
                    definition.InteractionId,
                    "qa-instance-" + activity,
                    true,
                    true)
            };
            AssertTrue(StaminaRecoveryPlanner.TrySelect(simulation, candidates, out StaminaRecoveryPlan plan),
                activity + " focused selection");
            return plan;
        }

        private static CharacterStaminaSimulation CompleteAtFacility(
            string characterId,
            CharacterStaminaCatalog catalog,
            StaminaRecoveryActivity activity)
        {
            CharacterStaminaSimulation simulation = RequestedSimulation(characterId, catalog);
            StaminaRecoveryPlan plan = SelectOnly(simulation, activity);
            string key = plan.RequestKey;
            long minute = simulation.State.LastProcessedMinute;
            simulation.AcceptRecoveryPlan(plan, minute);
            simulation.ConfirmSafeStopCompleted(key, minute);
            simulation.ConfirmStandUpCompleted(key, minute);
            simulation.ConfirmFacilityArrived(key, minute);
            simulation.ConfirmFacingAlignedAndPerforming(key, minute);
            simulation.AdvanceTo(
                minute + simulation.Profile.Recovery(activity).DurationGameMinutes);
            simulation.ConfirmInteractionCompleted(key, simulation.State.LastProcessedMinute);
            return simulation;
        }

        private static StaminaRecoveryCandidate[] AllCandidates()
        {
            return new[]
            {
                new StaminaRecoveryCandidate(
                    StaminaRecoveryActivity.Water, "water-drink", "qa-instance-water-b", true, true),
                new StaminaRecoveryCandidate(
                    StaminaRecoveryActivity.Water, "water-drink", "qa-instance-water-a", true, true),
                new StaminaRecoveryCandidate(
                    StaminaRecoveryActivity.Restroom, "restroom-use", "qa-instance-restroom-a", true, true),
                new StaminaRecoveryCandidate(
                    StaminaRecoveryActivity.Lounge, "lounge-rest", "qa-instance-rest-seat-a", true, true)
            };
        }

        private sealed class FakeCapabilityQueryAdapter : IStaminaRecoveryCapabilityQueryAdapter
        {
            private readonly long _sourceRevision;
            private readonly StaminaRecoveryCandidate[] _candidates;

            public FakeCapabilityQueryAdapter(
                long sourceRevision,
                IEnumerable<StaminaRecoveryCandidate> candidates)
            {
                _sourceRevision = sourceRevision;
                _candidates = candidates.ToArray();
            }

            public int QueryCount { get; private set; }
            public StaminaRecoveryCapabilityQuery LastQuery { get; private set; }
            public bool ReturnMismatchedContext { get; set; }

            public StaminaRecoveryCapabilityQueryResult Query(
                StaminaRecoveryCapabilityQuery query)
            {
                QueryCount++;
                LastQuery = query;
                StaminaRecoveryCapabilityQuery echoed = ReturnMismatchedContext
                    ? new StaminaRecoveryCapabilityQuery(
                        query.CharacterId + "-mismatch",
                        query.RecoveryRequestKey,
                        query.GameTimeMinute)
                    : query;
                return new StaminaRecoveryCapabilityQueryResult(
                    echoed,
                    _sourceRevision,
                    _candidates);
            }
        }

        private static void AssertPlanEqual(
            StaminaRecoveryPlan expected,
            StaminaRecoveryPlan actual,
            string label)
        {
            AssertEqual(expected.RequestKey, actual.RequestKey, label + " key");
            AssertEqual(expected.Activity, actual.Activity, label + " activity");
            AssertEqual(expected.InteractionId, actual.InteractionId, label + " interaction");
        }

        private static void AssertSnapshotEqual(
            CharacterStaminaSnapshotDto expected,
            CharacterStaminaSnapshotDto actual,
            string label)
        {
            foreach (FieldInfo field in typeof(CharacterStaminaSnapshotDto).GetFields(
                         BindingFlags.Instance | BindingFlags.Public))
                AssertEqual(field.GetValue(expected), field.GetValue(actual),
                    label + " field " + field.Name);
        }

        private static void AssertTrue(bool value, string label)
        {
            if (!value) throw new InvalidOperationException("Assertion failed: " + label);
        }

        private static void AssertFalse(bool value, string label)
        {
            if (value) throw new InvalidOperationException("Assertion failed: " + label);
        }

        private static void AssertSame(object expected, object actual, string label)
        {
            if (!ReferenceEquals(expected, actual))
                throw new InvalidOperationException("Assertion failed: " + label);
        }

        private static void AssertNotEqual<T>(T unexpected, T actual, string label)
        {
            if (EqualityComparer<T>.Default.Equals(unexpected, actual))
                throw new InvalidOperationException(
                    $"Assertion failed: {label}; unexpected={unexpected}.");
        }

        private static void AssertEqual<T>(T expected, T actual, string label)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException(
                    $"Assertion failed: {label}; expected={expected}, actual={actual}.");
        }

        private static void AssertThrows<T>(Action action, string label) where T : Exception
        {
            try
            {
                action();
            }
            catch (T)
            {
                return;
            }
            throw new InvalidOperationException(
                $"Assertion failed: {label}; expected {typeof(T).Name}.");
        }
    }
}
