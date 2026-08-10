using System;
using FamilyCompany.Presentation.Unity;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public static class OfficeSoundscapeValidation
    {
        [MenuItem("Family Company/Validate Office Soundscape")]
        public static void Run()
        {
            var unknown = default(OfficeSoundscapeObservation);
            var walking = Observe(OfficeSoundscapeZone.Inside, OfficeSoundscapeStage.Walking);
            var outside = Observe(OfficeSoundscapeZone.Outside, OfficeSoundscapeStage.Outside);

            AssertCue("first observation is silent", unknown, walking, OfficeSoundCue.None);
            AssertCue("outside first observation is silent", unknown, outside, OfficeSoundCue.None);
            AssertCue(
                "leaving plays one door sequence",
                walking,
                outside,
                OfficeSoundCue.DoorOpen | OfficeSoundCue.DoorClose);
            AssertCue("remaining outside is silent", outside, outside, OfficeSoundCue.None);
            AssertCue(
                "returning plays one door sequence",
                outside,
                walking,
                OfficeSoundCue.DoorOpen | OfficeSoundCue.DoorClose);

            var contractWalk = Observe(
                OfficeSoundscapeZone.Inside,
                OfficeSoundscapeStage.Walking,
                true,
                "office-contract:alpha",
                "printer-a");
            var printerArrival = Observe(
                OfficeSoundscapeZone.Inside,
                OfficeSoundscapeStage.Printing,
                true,
                "office-contract:alpha",
                "printer-a");
            AssertCue("printer arrival plays paper", contractWalk, printerArrival, OfficeSoundCue.Paper);
            AssertCue("printer stay does not repeat", printerArrival, printerArrival, OfficeSoundCue.None);

            var workWalk = Observe(
                OfficeSoundscapeZone.Inside,
                OfficeSoundscapeStage.Walking,
                true,
                "office-contract:beta",
                "work-desk-b");
            var workArrival = Observe(
                OfficeSoundscapeZone.Inside,
                OfficeSoundscapeStage.Work,
                true,
                "office-contract:beta",
                "work-desk-b");
            AssertCue(
                "contract work arrival plays ambience",
                workWalk,
                workArrival,
                OfficeSoundCue.ContractAmbient);
            AssertCue("contract work stay does not repeat", workArrival, workArrival, OfficeSoundCue.None);

            var newWorkAssignment = Observe(
                OfficeSoundscapeZone.Inside,
                OfficeSoundscapeStage.Work,
                true,
                "office-contract:gamma",
                "work-desk-c");
            AssertCue(
                "new contract identity at a work point is an arrival",
                workArrival,
                newWorkAssignment,
                OfficeSoundCue.ContractAmbient);

            var ordinaryWorkArrival = Observe(
                OfficeSoundscapeZone.Inside,
                OfficeSoundscapeStage.Work,
                false,
                string.Empty,
                "work-desk-a");
            AssertCue("ordinary work does not play contract ambience", walking, ordinaryWorkArrival, OfficeSoundCue.None);

            var meetingArrival = Observe(
                OfficeSoundscapeZone.Inside,
                OfficeSoundscapeStage.Meeting,
                true,
                "office-contract:meeting",
                "meeting-table");
            AssertCue(
                "contract meeting arrival plays ambience",
                workWalk,
                meetingArrival,
                OfficeSoundCue.ContractAmbient);

            Debug.Log("FAMILY_COMPANY_OFFICE_SOUNDSCAPE_VALIDATION: PASS");
        }

        private static OfficeSoundscapeObservation Observe(
            OfficeSoundscapeZone zone,
            OfficeSoundscapeStage stage,
            bool isContract = false,
            string taskId = "",
            string targetId = "")
        {
            return new OfficeSoundscapeObservation(zone, stage, isContract, taskId, targetId);
        }

        private static void AssertCue(
            string scenario,
            OfficeSoundscapeObservation previous,
            OfficeSoundscapeObservation current,
            OfficeSoundCue expected)
        {
            var actual = OfficeSoundscapeTransitionRules.Resolve(previous, current);
            if (actual != expected)
            {
                throw new InvalidOperationException(
                    scenario + ": expected " + expected + ", actual " + actual + ".");
            }
        }
    }
}
