using System;
using System.Collections.Generic;
using FamilyCompany.Presentation.Unity.OfficeSeating.UI;
using FamilyCompany.Simulation.OfficeSeating;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public static class OfficeSeatingUiValidation
    {
        [MenuItem("Family Company/Validate Office Seating UI")]
        public static void Run()
        {
            try
            {
                RunPureOrThrow();
                ValidateHotspotRaycastBoundary();
                ValidateImageGenSkinContract();
                Debug.Log("FAMILY_COMPANY_OFFICE_SEATING_UI_VALIDATION: PASS");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("FAMILY_COMPANY_OFFICE_SEATING_UI_VALIDATION: FAIL");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        public static void RunPureOrThrow()
        {
            ValidateSixteenByNineLayouts();
            ValidateUiAssignmentCompetition();
            ValidateOccupiedUnassignRejection();
            ValidateEscapeAndModalRestoration();
            ValidateResourcePathContract();
        }

        public static int Main()
        {
            RunPureOrThrow();
            Console.WriteLine("FAMILY_COMPANY_OFFICE_SEATING_UI_STANDALONE: PASS");
            return 0;
        }

        private static void ValidateSixteenByNineLayouts()
        {
            var fullHd = OfficeSeatingUiLayout.Calculate(1920, 1080);
            var hd = OfficeSeatingUiLayout.Calculate(1280, 720);

            AssertNear(1f, fullHd.Scale, 0.0001f, "1920 scale");
            AssertNear(600f, fullHd.Panel.Width, 0.001f, "1920 panel width");
            AssertNear(780f, fullHd.Panel.Height, 0.001f, "1920 panel height");
            AssertNear(2f / 3f, hd.Scale, 0.0001f, "1280 scale");
            AssertNear(400f, hd.Panel.Width, 0.001f, "1280 panel width");
            AssertNear(520f, hd.Panel.Height, 0.001f, "1280 panel height");
            AssertNear(1.5f, fullHd.Panel.Width / hd.Panel.Width, 0.001f, "resolution width ratio");
            AssertInsideScreen(fullHd, "1920 layout");
            AssertInsideScreen(hd, "1280 layout");
            AssertTrue(fullHd.MemberList.Height > fullHd.Actions.Height, "full HD member list remains usable");
            AssertTrue(hd.MemberList.Height > hd.Actions.Height, "HD member list remains usable");
        }

        private static void ValidateUiAssignmentCompetition()
        {
            var state = CreateState();
            var actions = new OfficeSeatPlacementActions(state);
            var first = actions.TryAssign("desk-a", "older_sister");
            var second = actions.TryAssign("desk-a", "father");

            AssertTrue(first.Succeeded, "first UI assignment succeeds");
            AssertFalse(second.Succeeded, "same seat second UI assignment rejected");
            AssertEqual(
                OfficeSeatOperationFailure.SeatAssignedToOtherMember,
                second.Failure,
                "same seat competition reason");
            AssertTrue(second.KoreanMessage.Contains("다른 구성원"), "competition has Korean feedback");
            AssertTrue(state.TryGetSeat("desk-a", out var seat), "assigned seat exists");
            AssertEqual("older_sister", seat.AssignedMemberId, "first UI assignment preserved");
        }

        private static void ValidateOccupiedUnassignRejection()
        {
            var state = CreateState();
            var actions = new OfficeSeatPlacementActions(state);
            AssertTrue(actions.TryAssign("desk-b", "mother").Succeeded, "mother assigned before occupy");
            AssertTrue(state.TryReserve("desk-b", "mother", "runtime-work", out _), "mother reserves seat");
            AssertTrue(state.TryOccupy("runtime-work", out _), "mother occupies seat");
            AssertTrue(state.TryGetSeat("desk-b", out var occupied), "occupied seat exists");
            AssertFalse(OfficeSeatPlacementActions.CanChangeAssignment(occupied), "occupied buttons disabled");

            var unassign = actions.TryUnassign("desk-b");
            AssertFalse(unassign.Succeeded, "occupied UI unassign rejected");
            AssertEqual(OfficeSeatOperationFailure.SeatHasActiveClaim, unassign.Failure, "occupied UI reason");
            AssertTrue(unassign.KoreanMessage.Contains("사용 중"), "occupied rejection has Korean feedback");
            AssertTrue(state.TryGetSeat("desk-b", out occupied), "occupied seat remains");
            AssertEqual("mother", occupied.AssignedMemberId, "occupied assignment preserved");
            AssertEqual(OfficeSeatMeaningState.Occupied, occupied.State, "occupied runtime state preserved");
        }

        private static void ValidateEscapeAndModalRestoration()
        {
            var baseline = OfficeModalInputState.ActiveLeaseCount;
            var outerLease = OfficeModalInputState.Acquire("validation-outer-modal");
            var session = new OfficeSeatPlacementSession();
            try
            {
                AssertEqual(baseline + 1, OfficeModalInputState.ActiveLeaseCount, "outer modal acquired");
                session.Open(new OfficeSeatSelection("desk-a", "업무 책상 A"));
                AssertTrue(session.IsOpen, "seat panel session opened");
                AssertTrue(OfficeModalInputState.IsInputBlocked, "modal blocks input");
                AssertEqual(baseline + 2, OfficeModalInputState.ActiveLeaseCount, "seat modal lease acquired");
                AssertFalse(session.HandleEscape(false), "no escape leaves panel open");
                AssertTrue(session.HandleEscape(true), "escape closes panel");
                AssertFalse(session.IsOpen, "panel closed by escape");
                AssertEqual(baseline + 1, OfficeModalInputState.ActiveLeaseCount, "seat lease restored without releasing outer modal");
            }
            finally
            {
                session.Dispose();
                outerLease.Dispose();
            }
            AssertEqual(baseline, OfficeModalInputState.ActiveLeaseCount, "modal count restored to baseline");
        }

        private static void ValidateResourcePathContract()
        {
            AssertEqual(
                "Assets/Art/UI/Resources/OfficeSeating/office_seat_assignment_panel_v1.png",
                OfficeSeatPlacementPanel.SkinAssetPath,
                "ImageGen asset path contract");
            AssertEqual(
                "OfficeSeating/office_seat_assignment_panel_v1",
                OfficeSeatPlacementPanel.SkinResourcePath,
                "Unity Resources load path contract");
        }

        private static void ValidateHotspotRaycastBoundary()
        {
            const int validationLayer = 31;
            var root = new GameObject("OfficeSeatUiValidationRoot") { layer = validationLayer };
            var hotspotObject = new GameObject("SeatHotspot") { layer = validationLayer };
            var ordinaryColliderObject = new GameObject("OrdinaryDeskCollider") { layer = validationLayer };
            var cameraObject = new GameObject("OfficeSeatUiValidationCamera") { layer = validationLayer };
            try
            {
                root.transform.position = new Vector3(10_000f, 0f, 0f);
                hotspotObject.transform.SetParent(root.transform, false);
                ordinaryColliderObject.transform.SetParent(root.transform, false);
                var hotspot = hotspotObject.AddComponent<BoxCollider>();
                hotspot.size = new Vector3(1.5f, 1.5f, 0.6f);
                hotspot.isTrigger = true;
                var ordinary = ordinaryColliderObject.AddComponent<BoxCollider>();
                ordinary.size = new Vector3(1.5f, 1.5f, 0.6f);
                ordinaryColliderObject.transform.localPosition = new Vector3(0f, 0f, -3f);
                ordinaryColliderObject.SetActive(false);

                var provider = root.AddComponent<OfficeSeatingUiValidationHotspotProvider>();
                provider.Configure("desk-validation", "검증 책상", hotspot);
                AssertTrue(
                    OfficeSeatClickController.TryResolveHotspot(hotspot, out var directSelection),
                    "declared hotspot resolves");
                AssertEqual("desk-validation", directSelection.SeatId, "declared hotspot seat ID");
                AssertFalse(
                    OfficeSeatClickController.TryResolveHotspot(ordinary, out _),
                    "ordinary collider under provider is not a hotspot");

                var camera = cameraObject.AddComponent<Camera>();
                camera.transform.position = new Vector3(10_000f, 0f, -10f);
                camera.transform.rotation = Quaternion.identity;
                camera.pixelRect = new Rect(0f, 0f, 1280f, 720f);
                camera.fieldOfView = 60f;
                var controller = cameraObject.AddComponent<OfficeSeatClickController>();
                controller.Configure(camera, 1 << validationLayer);
                Physics.SyncTransforms();
                var screenPoint = camera.WorldToScreenPoint(hotspot.bounds.center);
                AssertTrue(
                    controller.TrySelectAt(screenPoint, out var raySelection),
                    "ScreenPointToRay selects actual hotspot");
                AssertEqual("desk-validation", raySelection.SeatId, "raycast hotspot seat ID");

                ordinaryColliderObject.SetActive(true);
                Physics.SyncTransforms();
                AssertFalse(
                    controller.TrySelectAt(screenPoint, out _),
                    "nearest ordinary collider is not misidentified or clicked through");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ValidateImageGenSkinContract()
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(OfficeSeatPlacementPanel.SkinAssetPath);
            if (texture == null)
            {
                Debug.LogWarning(
                    "FAMILY_COMPANY_OFFICE_SEATING_UI_IMAGEGEN: FINAL_QA_REQUIRED · missing " +
                    OfficeSeatPlacementPanel.SkinAssetPath);
                return;
            }
            Debug.Log("FAMILY_COMPANY_OFFICE_SEATING_UI_IMAGEGEN: READY");
        }

        private static OfficeSeatingState CreateState()
        {
            return new OfficeSeatingState(new[]
            {
                new OfficeSeatDefinition("desk-a", new OfficeSeatPosition(-1, 0)),
                new OfficeSeatDefinition("desk-b", new OfficeSeatPosition(1, 0))
            });
        }

        private static void AssertInsideScreen(OfficeSeatPlacementLayout layout, string label)
        {
            AssertTrue(layout.Panel.X >= 0f, label + " panel left");
            AssertTrue(layout.Panel.Y >= 0f, label + " panel top");
            AssertTrue(layout.Panel.XMax <= layout.ScreenWidth, label + " panel right");
            AssertTrue(layout.Panel.YMax <= layout.ScreenHeight, label + " panel bottom");
            AssertTrue(layout.Actions.YMax <= layout.Panel.YMax, label + " actions inside panel");
        }

        private static void AssertNear(float expected, float actual, float tolerance, string label)
        {
            if (Math.Abs(expected - actual) > tolerance)
                throw new InvalidOperationException(label + ": expected " + expected + ", got " + actual);
        }

        private static void AssertTrue(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException(label + ": expected true");
        }

        private static void AssertFalse(bool condition, string label)
        {
            if (condition) throw new InvalidOperationException(label + ": expected false");
        }

        private static void AssertEqual<T>(T expected, T actual, string label)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException(label + ": expected " + expected + ", got " + actual);
        }
    }

    public sealed class OfficeSeatingUiValidationHotspotProvider : MonoBehaviour, IOfficeSeatHotspotProvider
    {
        private string _seatId;
        private string _displayName;
        private Collider _hotspot;

        public string SeatId => _seatId;
        public string SeatDisplayName => _displayName;

        public void Configure(string seatId, string displayName, Collider hotspot)
        {
            _seatId = seatId;
            _displayName = displayName;
            _hotspot = hotspot;
        }

        public bool OwnsSeatHotspot(Collider candidate)
        {
            return candidate != null && candidate == _hotspot;
        }
    }
}
