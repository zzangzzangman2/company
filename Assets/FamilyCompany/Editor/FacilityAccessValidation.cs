using System;
using System.Linq;
using FamilyCompany.Simulation.Progression;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public static class FacilityAccessValidation
    {
        [MenuItem("Family Company/Validate Facility Access")]
        public static void Run()
        {
            try
            {
                ValidateInitialLocks();
                ValidateMilestoneUnlocks();
                ValidateAcquisitionQualification();
                ValidateDevelopmentGateOverride();
                ValidateOrderIndependenceAndIdempotence();
                Debug.Log("FAMILY_COMPANY_FACILITY_ACCESS_VALIDATION: PASS");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("FAMILY_COMPANY_FACILITY_ACCESS_VALIDATION: FAIL");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        private static void ValidateInitialLocks()
        {
            var state = new FacilityAccessState();
            foreach (CompanyFacility facility in Enum.GetValues(typeof(CompanyFacility)))
            {
                AssertEqual(false, state.IsUnlocked(facility), $"initial {facility} lock");
            }

            AssertMissing(
                state,
                CompanyFacility.Bank,
                FacilityAccessRequirement.FatherAccompaniedBankVisit);
            AssertMissing(
                state,
                CompanyFacility.Hiring,
                FacilityAccessRequirement.BusinessRegistrationCompleted,
                FacilityAccessRequirement.AdultHiringApprovalGranted);
        }

        private static void ValidateMilestoneUnlocks()
        {
            var state = new FacilityAccessState();
            AssertEqual(true, state.RecordMilestone(FacilityMilestone.FatherAccompaniedBankVisit), "record father bank visit");
            AssertEqual(true, state.IsUnlocked(CompanyFacility.Bank), "father accompaniment unlocks bank");
            AssertEqual(false, state.IsUnlocked(CompanyFacility.ResearchAndDevelopment), "bank visit does not unlock R&D");

            state.RecordMilestone(FacilityMilestone.FirstContractCompleted);
            AssertEqual(true, state.IsUnlocked(CompanyFacility.ResearchAndDevelopment), "first contract unlocks R&D");

            state.RecordMilestone(FacilityMilestone.BusinessRegistrationCompleted);
            AssertEqual(false, state.IsUnlocked(CompanyFacility.Hiring), "registration alone does not unlock hiring");
            AssertMissing(
                state,
                CompanyFacility.Hiring,
                FacilityAccessRequirement.AdultHiringApprovalGranted);
            state.RecordMilestone(FacilityMilestone.AdultHiringApprovalGranted);
            AssertEqual(true, state.IsUnlocked(CompanyFacility.Hiring), "registration and adult approval unlock hiring");

            state.RecordMilestone(FacilityMilestone.FirstOwnedBusinessFounded);
            AssertEqual(true, state.IsUnlocked(CompanyFacility.CorporateStockAccount), "first business unlocks corporate stock account");
        }

        private static void ValidateAcquisitionQualification()
        {
            var state = new FacilityAccessState();
            state.UpdateAcquisitionQualification(
                FacilityAccessState.AcquisitionRequiredReputation,
                FacilityAccessState.AcquisitionRequiredCapitalWon - 1);
            AssertMissing(state, CompanyFacility.Acquisitions, FacilityAccessRequirement.AcquisitionCapital);

            state.UpdateAcquisitionQualification(
                FacilityAccessState.AcquisitionRequiredReputation - 1,
                FacilityAccessState.AcquisitionRequiredCapitalWon);
            AssertMissing(state, CompanyFacility.Acquisitions, FacilityAccessRequirement.AcquisitionReputation);

            state.UpdateAcquisitionQualification(
                FacilityAccessState.AcquisitionRequiredReputation,
                FacilityAccessState.AcquisitionRequiredCapitalWon);
            AssertEqual(true, state.IsUnlocked(CompanyFacility.Acquisitions), "exact acquisition thresholds unlock menu");
        }

        private static void ValidateDevelopmentGateOverride()
        {
            var state = new FacilityAccessState(facilityStoryGatesEnabled: false);
            AssertEqual(5, state.UnlockedFacilities().Count, "development override unlocks every facility");
            state.SetFacilityStoryGatesEnabled(true);
            AssertEqual(0, state.UnlockedFacilities().Count, "reenabling gates restores semantic locks");
        }

        private static void ValidateOrderIndependenceAndIdempotence()
        {
            var forward = new FacilityAccessState();
            var reverse = new FacilityAccessState();
            var milestones = Enum.GetValues(typeof(FacilityMilestone)).Cast<FacilityMilestone>().ToArray();
            foreach (var milestone in milestones) forward.RecordMilestone(milestone);
            foreach (var milestone in milestones.Reverse()) reverse.RecordMilestone(milestone);
            forward.UpdateAcquisitionQualification(40, 25_000_000);
            reverse.UpdateAcquisitionQualification(40, 25_000_000);

            AssertEqual(
                AccessFingerprint(forward),
                AccessFingerprint(reverse),
                "milestone order produces the same access state");
            AssertEqual(false, forward.RecordMilestone(FacilityMilestone.FirstContractCompleted), "duplicate milestone is idempotent");
            AssertEqual(5, forward.UnlockedFacilities().Count, "all qualified facilities unlocked");
        }

        private static string AccessFingerprint(FacilityAccessState state)
        {
            return string.Join(
                "|",
                Enum.GetValues(typeof(CompanyFacility))
                    .Cast<CompanyFacility>()
                    .Select(facility => $"{(int)facility}:{state.IsUnlocked(facility)}"));
        }

        private static void AssertMissing(
            FacilityAccessState state,
            CompanyFacility facility,
            params FacilityAccessRequirement[] expected)
        {
            var actual = state.Evaluate(facility).MissingRequirements.ToArray();
            if (!expected.SequenceEqual(actual))
            {
                throw new InvalidOperationException(
                    $"{facility} missing requirements: expected [{string.Join(",", expected)}], " +
                    $"got [{string.Join(",", actual)}].");
            }
        }

        private static void AssertEqual<T>(T expected, T actual, string label)
        {
            if (!Equals(expected, actual))
                throw new InvalidOperationException($"{label}: expected {expected}, got {actual}.");
        }
    }
}
