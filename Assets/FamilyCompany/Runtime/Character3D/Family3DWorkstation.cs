using System;
using System.Collections.Generic;
using UnityEngine;

namespace FamilyCompany.Runtime.Character3D
{
    /// <summary>
    /// Runtime-only 3D replacement for one real Starter Office CRT workstation. The semantic
    /// desk, chair, seat claim and route stay owned by the production runtime; this object only
    /// replaces their pixels on the isolated QA layer so the V19 body can be judged while seated.
    /// </summary>
    public sealed class Family3DWorkstation : MonoBehaviour
    {
        private readonly List<Material> ownedMaterials = new List<Material>();
        private readonly List<Mesh> ownedMeshes = new List<Mesh>();
        private float seatedVisualYawOffsetDegrees;
        private float actorModelForwardYawOffsetDegrees;
        private Quaternion seatedActorLocalRotation = Quaternion.identity;
        private Quaternion chairFacingLocalRotation = Quaternion.identity;
        private Vector3 resolvedSeatGroundLocal;
        private Vector3 deskFootprintCenterLocal;
        private Vector3 keyboardGroundLocal;
        private Vector3 gridRightLocalUnit;
        private Vector3 gridForwardLocalUnit;
        private float deskFootprintWidthWorld;
        private float deskFootprintDepthWorld;
        private bool centerChairOnSeatCell;

        public Vector3 SemanticSeatGroundWorld => transform.position;
        public Vector3 SeatGroundWorld => transform.TransformPoint(resolvedSeatGroundLocal);
        public Vector3 ChairGroundWorld { get; private set; }
        public string WorkstationSetId { get; private set; } = string.Empty;
        public Vector3 ForwardWorld => transform.forward;
        public Quaternion GridRotationWorld => transform.rotation;
        public Quaternion SeatedRotationWorld =>
            transform.rotation * seatedActorLocalRotation;
        public Quaternion ChairRotationWorld =>
            transform.rotation * chairFacingLocalRotation;
        public Vector3 SeatedBodyForwardWorld =>
            SeatedRotationWorld *
            (Quaternion.Euler(0f, -actorModelForwardYawOffsetDegrees, 0f) * Vector3.forward);
        public float SeatedVisualYawOffsetDegrees => seatedVisualYawOffsetDegrees;
        public float ActorModelForwardYawOffsetDegrees => actorModelForwardYawOffsetDegrees;
        public float SeatToKeyboardFacingErrorDegrees { get; private set; }
        public float CushionWorldY { get; private set; }
        public Vector3 KeyboardWorld { get; private set; }
        public Vector3 MonitorWorld { get; private set; }
        public Vector3 MonitorScreenOutwardWorld { get; private set; }
        public Vector3 WorkSurfaceWorld { get; private set; }
        public Vector3 DeskTopCenterWorld { get; private set; }
        public Vector3 DeskFootprintCenterWorld =>
            transform.TransformPoint(deskFootprintCenterLocal);
        public float DeskFootprintWidthWorld => deskFootprintWidthWorld;
        public float DeskFootprintDepthWorld => deskFootprintDepthWorld;
        public float GridAxisOrthogonalityErrorDegrees { get; private set; }
        public float SeatToKeyboardGroundDistance { get; private set; }
        public float MaximumSeatToKeyboardGroundDistanceWorld { get; private set; }
        public float KeyboardToMonitorScreenGroundDistanceWorld { get; private set; }
        public float MinimumKeyboardToMonitorScreenGroundDistanceWorld { get; private set; }
        public float KeyboardInsetFromDeskFrontWorld { get; private set; }
        public float SeatToDeskFrontClearanceWorld { get; private set; }
        public float MinimumSeatToDeskFrontClearanceWorld { get; private set; }
        public float SeatToMonitorFacingErrorDegrees { get; private set; }
        public float ChairToMonitorFacingErrorDegrees { get; private set; }
        public float MonitorScreenToSeatFacingErrorDegrees { get; private set; }
        public float SemanticSeatToScreenFacingSeatDistance { get; private set; }

        /// <summary>
        /// Counts vertices of the actually deformed character skin that are strictly inside each
        /// solid chair primitive. A small normalized inset treats surface contact as contact rather
        /// than penetration. This catches pelvis/thigh intersections that joint-point gates miss.
        /// </summary>
        public ChairSkinPenetration MeasureChairSkinPenetration(
            IReadOnlyList<Vector3> worldSkinVertices,
            IReadOnlyList<Family3DWalkActor.SeatedSkinRegion> skinRegions)
        {
            if (worldSkinVertices == null)
                throw new ArgumentNullException(nameof(worldSkinVertices));
            if (skinRegions == null || skinRegions.Count != worldSkinVertices.Count)
                throw new ArgumentException(
                    "Skin regions must match the world skin vertex count.",
                    nameof(skinRegions));
            Transform chair = transform.Find("Chair_SwivelPivot");
            if (chair == null)
                throw new InvalidOperationException("Chair swivel root is missing.");

            Transform cushion = RequireChairPart(chair, "Chair_Cushion");
            Transform lumbar = RequireChairPart(chair, "Chair_LumbarRail");
            Transform stem = RequireChairPart(chair, "Chair_Stem");
            Transform roundFoot = RequireChairPart(chair, "Chair_RoundFoot");
            Transform[] uprights = chair.GetComponentsInChildren<Transform>(true);
            var result = new ChairSkinPenetration
            {
                sampledSkinVertexCount = worldSkinVertices.Count,
                cushionMinimumLocalY = float.PositiveInfinity,
                cushionMaximumLocalY = float.NegativeInfinity
            };
            for (var vertexIndex = 0; vertexIndex < worldSkinVertices.Count; vertexIndex++)
            {
                Vector3 vertex = worldSkinVertices[vertexIndex];
                if (IsStrictlyInsideUnitBox(cushion, vertex))
                {
                    result.cushionVertexCount++;
                    switch (skinRegions[vertexIndex])
                    {
                        case Family3DWalkActor.SeatedSkinRegion.PelvisOrTorso:
                            result.cushionPelvisOrTorsoVertexCount++;
                            break;
                        case Family3DWalkActor.SeatedSkinRegion.UpperLeg:
                            result.cushionUpperLegVertexCount++;
                            break;
                        case Family3DWalkActor.SeatedSkinRegion.LowerLeg:
                            result.cushionLowerLegVertexCount++;
                            break;
                        case Family3DWalkActor.SeatedSkinRegion.Foot:
                            result.cushionFootVertexCount++;
                            break;
                        default:
                            result.cushionOtherVertexCount++;
                            break;
                    }
                    float cushionLocalY = cushion.InverseTransformPoint(vertex).y;
                    result.cushionMinimumLocalY =
                        Mathf.Min(result.cushionMinimumLocalY, cushionLocalY);
                    result.cushionMaximumLocalY =
                        Mathf.Max(result.cushionMaximumLocalY, cushionLocalY);
                }
                if (IsStrictlyInsideUnitBox(lumbar, vertex))
                    result.lumbarVertexCount++;
                if (IsStrictlyInsideUnitCylinder(stem, vertex))
                    result.stemVertexCount++;
                if (IsStrictlyInsideUnitCylinder(roundFoot, vertex))
                    result.roundFootVertexCount++;
                for (var partIndex = 0; partIndex < uprights.Length; partIndex++)
                {
                    Transform part = uprights[partIndex];
                    if (!part.name.StartsWith("Chair_BackUpright", StringComparison.Ordinal) ||
                        !IsStrictlyInsideUnitBox(part, vertex))
                        continue;
                    result.backUprightVertexCount++;
                    break;
                }
            }
            result.totalPenetratingVertexCount =
                result.cushionVertexCount +
                result.backUprightVertexCount +
                result.lumbarVertexCount +
                result.stemVertexCount +
                result.roundFootVertexCount;
            if (result.cushionVertexCount == 0)
                result.cushionMinimumLocalY = result.cushionMaximumLocalY = 0f;
            return result;
        }

        private static Transform RequireChairPart(Transform chair, string partName)
        {
            Transform result = chair.Find(partName);
            if (result == null)
                throw new InvalidOperationException(partName + " is missing from the chair.");
            return result;
        }

        private static bool IsStrictlyInsideUnitBox(Transform part, Vector3 worldPoint)
        {
            Vector3 local = part.InverseTransformPoint(worldPoint);
            const float insetExtent = 0.485f;
            return Mathf.Abs(local.x) < insetExtent &&
                   Mathf.Abs(local.y) < insetExtent &&
                   Mathf.Abs(local.z) < insetExtent;
        }

        private static bool IsStrictlyInsideUnitCylinder(Transform part, Vector3 worldPoint)
        {
            Vector3 local = part.InverseTransformPoint(worldPoint);
            const float insetRadius = 0.97f;
            return Mathf.Abs(local.y) < insetRadius &&
                   local.x * local.x + local.z * local.z < insetRadius * insetRadius;
        }

        [Serializable]
        public struct ChairSkinPenetration
        {
            public int sampledSkinVertexCount;
            public int totalPenetratingVertexCount;
            public int cushionVertexCount;
            public int backUprightVertexCount;
            public int lumbarVertexCount;
            public int stemVertexCount;
            public int roundFootVertexCount;
            public float cushionMinimumLocalY;
            public float cushionMaximumLocalY;
            public int cushionPelvisOrTorsoVertexCount;
            public int cushionUpperLegVertexCount;
            public int cushionLowerLegVertexCount;
            public int cushionFootVertexCount;
            public int cushionOtherVertexCount;
        }

        public static Family3DWorkstation Create(
            Transform parent,
            int layer,
            string workstationId,
            Vector3 seatGroundWorld,
            Vector3 gridRightWorld,
            Vector3 forwardWorld,
            Vector3 deskFootprintCenterWorld,
            float deskFootprintWidthWorld,
            float deskFootprintDepthWorld,
            Vector3 keyboardGroundWorld,
            float characterHeight,
            float modelForwardYawOffsetDegrees,
            float visualYawOffsetDegrees,
            bool centerChairOnSeatCell = false)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            gridRightWorld.y = 0f;
            forwardWorld.y = 0f;
            deskFootprintCenterWorld.y = seatGroundWorld.y;
            keyboardGroundWorld.y = seatGroundWorld.y;
            if (gridRightWorld.sqrMagnitude <= 0.000001f)
                throw new ArgumentException("Workstation grid-right must be non-zero.", nameof(gridRightWorld));
            if (forwardWorld.sqrMagnitude <= 0.000001f)
                throw new ArgumentException("Workstation forward must be non-zero.", nameof(forwardWorld));
            if (deskFootprintWidthWorld <= 0.0001f || deskFootprintDepthWorld <= 0.0001f)
                throw new ArgumentOutOfRangeException(
                    nameof(deskFootprintWidthWorld),
                    "Workstation semantic footprint must have positive width and depth.");

            string safeId = string.IsNullOrWhiteSpace(workstationId)
                ? "unassigned"
                : workstationId.Trim();
            // One root owns the complete desk/chair set. Placement, relocation and rotation can
            // therefore never leave the chair behind or break its authored sitting alignment.
            var root = new GameObject(
                "V31_AtomicWorkstationSet_OriginalChair_" + safeId);
            root.transform.SetParent(parent, false);
            // Furniture roots are a map-grid contract. Keep the desk, CRT and keyboard on the
            // exact canonical seat axis so later desks/tables share one placement rule. Camera
            // readability is never allowed to change a workstation's physical facing.
            root.transform.SetPositionAndRotation(
                seatGroundWorld,
                Quaternion.LookRotation(forwardWorld.normalized, Vector3.up));
            SetLayerRecursively(root, layer);

            var result = root.AddComponent<Family3DWorkstation>();
            result.WorkstationSetId = safeId;
            result.seatedVisualYawOffsetDegrees = visualYawOffsetDegrees;
            result.actorModelForwardYawOffsetDegrees = modelForwardYawOffsetDegrees;
            result.centerChairOnSeatCell = centerChairOnSeatCell;
            result.deskFootprintCenterLocal = root.transform.InverseTransformPoint(
                deskFootprintCenterWorld);
            result.keyboardGroundLocal = root.transform.InverseTransformPoint(keyboardGroundWorld);
            result.gridRightLocalUnit = root.transform.InverseTransformDirection(
                gridRightWorld.normalized);
            result.gridForwardLocalUnit = root.transform.InverseTransformDirection(
                forwardWorld.normalized);
            result.deskFootprintWidthWorld = deskFootprintWidthWorld;
            result.deskFootprintDepthWorld = deskFootprintDepthWorld;
            result.GridAxisOrthogonalityErrorDegrees = Mathf.Abs(
                90f - Vector3.Angle(gridRightWorld, forwardWorld));
            result.Build(layer, Mathf.Max(characterHeight, 0.25f));
            result.SeatToKeyboardFacingErrorDegrees = result.MeasureSeatedFacingError(
                result.KeyboardWorld);
            result.SeatToMonitorFacingErrorDegrees = result.MeasureSeatedFacingError(
                result.MonitorWorld);
            result.ChairToMonitorFacingErrorDegrees = result.MeasureChairFacingError(
                result.MonitorWorld);
            result.MonitorScreenToSeatFacingErrorDegrees =
                result.MeasureMonitorScreenToSeatFacingError();
            result.SemanticSeatToScreenFacingSeatDistance = Vector3.Distance(
                result.SemanticSeatGroundWorld,
                result.SeatGroundWorld);
            if (result.SeatToKeyboardFacingErrorDegrees > 0.1f ||
                result.SeatToMonitorFacingErrorDegrees > 0.1f ||
                result.ChairToMonitorFacingErrorDegrees > 0.1f ||
                result.MonitorScreenToSeatFacingErrorDegrees > 0.1f)
                throw new InvalidOperationException(
                    "Screen, keyboard, chair and actor centreline failed: " +
                    "seatToKeyboard=" +
                    result.SeatToKeyboardFacingErrorDegrees.ToString("F4") +
                    " seatToMonitor=" +
                    result.SeatToMonitorFacingErrorDegrees.ToString("F4") +
                    " chairToMonitor=" +
                    result.ChairToMonitorFacingErrorDegrees.ToString("F4") +
                    " screenToSeat=" +
                    result.MonitorScreenToSeatFacingErrorDegrees.ToString("F4") + ".");
            return result;
        }

        private void Build(int layer, float h)
        {
            Material deskWood = CreateMaterial(
                "Desk_WarmWalnut",
                new Color(0.30f, 0.15f, 0.075f, 1f),
                0f,
                0.24f);
            Material deskEdge = CreateMaterial(
                "Desk_DarkEdge",
                new Color(0.105f, 0.055f, 0.035f, 1f),
                0f,
                0.18f);
            Material beige = CreateMaterial(
                "Computer_WarmBeige",
                new Color(0.68f, 0.62f, 0.49f, 1f),
                0f,
                0.19f);
            Material dark = CreateMaterial(
                "Computer_DarkPlastic",
                new Color(0.045f, 0.055f, 0.052f, 1f),
                0f,
                0.16f);
            Material screen = CreateMaterial(
                "Crt_DeepGreenScreen",
                new Color(0.018f, 0.16f, 0.13f, 1f),
                0f,
                0.34f);
            Material screenGlow = CreateMaterial(
                "Crt_ScreenGlow",
                new Color(0.08f, 0.72f, 0.55f, 1f),
                0f,
                0.25f);
            Material chairUpholstery = CreateMaterial(
                "Chair_NeutralGraphite",
                new Color(0.18f, 0.18f, 0.18f, 1f),
                0f,
                0.28f);
            Material chairTrim = CreateMaterial(
                "Chair_CharcoalTrim",
                new Color(0.050f, 0.050f, 0.050f, 1f),
                0.08f,
                0.30f);
            Material mugTeal = CreateMaterial(
                "Mug_DeepTeal",
                new Color(0.035f, 0.23f, 0.19f, 1f),
                0f,
                0.30f);
            Material key = CreateMaterial(
                "Keyboard_Keys",
                new Color(0.16f, 0.16f, 0.14f, 1f),
                0f,
                0.18f);
            Material paper = CreateMaterial(
                "Desk_Paper",
                new Color(0.91f, 0.84f, 0.67f, 1f),
                0f,
                0.08f);

            // Preserve the user-approved V31 chair exactly. Character-specific seating clearance
            // belongs to the actor pose and must never shorten this stem, base, cushion or back.
            float seatY = 0.265f * h;
            CushionWorldY = transform.position.y + seatY + 0.028f * h;

            var chairPivotObject = new GameObject("Chair_SwivelPivot");
            chairPivotObject.transform.SetParent(transform, false);
            // The chair uses furniture +Z while each imported body has a separately measured model
            // forward. Solve the two rotations independently even when this candidate's measured
            // offset happens to be zero. A visual presentation offset must never rotate the chair
            // back away from the seat-to-monitor centreline, as the rejected V17 proof did.
            chairPivotObject.transform.localRotation = chairFacingLocalRotation;
            chairPivotObject.layer = layer;
            Transform chairPivot = chairPivotObject.transform;

            // Keep the chair visually real but deliberately open-backed. The canonical father
            // seat faces away from the isometric camera, so a conventional solid high back hid
            // the torso, elbows and both legs even though the pose itself was valid. Two slim
            // uprights and one lumbar rail preserve a readable chair silhouette without masking
            // the body that this QA exists to judge.
            AddBox("Chair_Cushion", new Vector3(0f, seatY, -0.025f * h),
                new Vector3(0.30f, 0.050f, 0.23f) * h, chairUpholstery, layer,
                new Vector3(5f, 0f, 0f), chairPivot);
            foreach (float x in new[] { -0.115f, 0.115f })
                AddBox("Chair_BackUpright", new Vector3(x * h, 0.385f * h, -0.120f * h),
                    new Vector3(0.035f, 0.26f, 0.04f) * h, chairTrim, layer,
                    new Vector3(-7f, 0f, 0f), chairPivot);
            AddBox("Chair_LumbarRail", new Vector3(0f, 0.405f * h, -0.125f * h),
                new Vector3(0.30f, 0.085f, 0.045f) * h, chairUpholstery, layer,
                new Vector3(-7f, 0f, 0f), chairPivot);
            AddCylinder("Chair_Stem", new Vector3(0f, 0.132f * h, 0f),
                0.03f * h, 0.225f * h, chairTrim, layer, chairPivot);
            AddCylinder("Chair_RoundFoot", new Vector3(0f, 0.025f * h, 0f),
                0.14f * h, 0.025f * h, chairTrim, layer, chairPivot);

            // Horizontal placement comes only from the semantic two-tile desk footprint. The
            // isometric viewport-to-ground mapping is intentionally oblique: its two tile axes
            // are not perpendicular in this QA world. Consequently every rectangular furniture
            // part is built as a parallelepiped in the mapped grid basis. Rotating an ordinary
            // cube can align one tile axis or the other, but can never land all four corners on
            // the real semantic footprint.
            float topY = 0.455f * h;
            float deskWidth = deskFootprintWidthWorld * 0.90f;
            // In the tile-centred set the tabletop uses its full reserved row depth. The
            // old 14% empty strip unnecessarily lengthened the reach across the chair tile.
            float deskDepth = deskFootprintDepthWorld * (centerChairOnSeatCell ? 1f : 0.86f);
            Vector2 deskGrid = LocalToGridCoordinates(deskFootprintCenterLocal);
            Vector2 keyboardGrid = LocalToGridCoordinates(keyboardGroundLocal);
            float deskRight = deskGrid.x;
            float deskForward = deskGrid.y;
            float frontForward = deskForward - deskDepth * 0.5f;
            float backForward = deskForward + deskDepth * 0.5f;
            float sideInset = Mathf.Min(0.055f * h, deskWidth * 0.08f);
            float depthInset = Mathf.Min(0.055f * h, deskDepth * 0.14f);

            Transform deskTop = AddGridBox("Desk_Top", GridLocal(deskRight, topY, deskForward),
                deskWidth, 0.055f * h, deskDepth, deskWood, layer);
            DeskTopCenterWorld = deskTop.position;
            AddGridBox("Desk_FrontEdge",
                GridLocal(deskRight, topY - 0.022f * h, frontForward),
                deskWidth + 0.02f * h, 0.070f * h, 0.035f * h,
                deskEdge,
                layer);
            foreach (float right in new[]
                     {
                         deskRight - deskWidth * 0.5f + sideInset,
                         deskRight + deskWidth * 0.5f - sideInset
                     })
            foreach (float forward in new[]
                     {
                         frontForward + depthInset,
                         backForward - depthInset
                     })
                AddGridBox("Desk_Leg", GridLocal(right, 0.215f * h, forward),
                    0.060f * h, 0.43f * h, 0.060f * h, deskEdge, layer);

            float serviceSide = Mathf.Sign(deskRight - (centerChairOnSeatCell ? 0f : keyboardGrid.x));
            if (Mathf.Abs(serviceSide) < 0.5f) serviceSide = 1f;
            float drawerRight = deskRight + serviceSide * deskWidth * 0.34f;
            float drawerForward = deskForward + deskDepth * 0.03f;
            float drawerWidth = Mathf.Min(0.20f * h, deskWidth * 0.24f);
            float drawerDepth = Mathf.Min(0.25f * h, deskDepth * 0.68f);
            // Drawer details belong to the cabinet's own front plane. Using the whole desk's
            // frontForward left all three rails and handles floating in the knee opening, where
            // the isometric view made them read as jagged spikes beside the left leg.
            float drawerFaceForward = drawerForward - drawerDepth * 0.5f;
            AddGridBox("Desk_Drawers", GridLocal(drawerRight, 0.255f * h, drawerForward),
                drawerWidth,
                0.32f * h,
                drawerDepth,
                deskWood,
                layer);
            for (var drawer = 0; drawer < 3; drawer++)
            {
                float y = (0.17f + drawer * 0.09f) * h;
                AddGridBox("Desk_DrawerLine_" + drawer,
                    GridLocal(drawerRight, y, drawerFaceForward - 0.005f * h),
                    drawerWidth * 0.82f, 0.010f * h, 0.010f * h, deskEdge, layer);
                AddGridBox("Desk_DrawerHandle_" + drawer,
                    GridLocal(drawerRight, y + 0.028f * h,
                        drawerFaceForward - 0.012f * h),
                    drawerWidth * 0.35f, 0.015f * h, 0.018f * h, chairTrim, layer);
            }

            // CRT, keyboard, mouse, phone, papers and a mug make this read as a working desk in
            // the same small isometric map view, not as an anonymous brown block. Keyboard X/Z is
            // the real authored operator-work socket laterally. The 2D work socket's depth cannot
            // be used as physical furniture depth: in this oblique projection it resolves to the
            // far edge of the top. V23 kept that depth and consequently pulled the chair and the
            // Father's torso through the desk to reach the keys. A real keyboard belongs on the
            // operator-facing front row, with its complete depth and a small palm-rest inset inside
            // the semantic footprint.
            float keyboardWidth = Mathf.Min(0.35f * h, deskWidth * 0.43f);
            float keyboardDepth = Mathf.Min(0.125f * h, deskDepth * 0.33f);
            float authoredKeyboardRight = Mathf.Clamp(
                keyboardGrid.x,
                deskRight - deskWidth * 0.5f + keyboardWidth * 0.5f,
                deskRight + deskWidth * 0.5f - keyboardWidth * 0.5f);
            float keyboardForward =
                frontForward + keyboardDepth * 0.5f + (centerChairOnSeatCell ? 0.002f : 0.020f) * h;
            float monitorForward = Mathf.Clamp(
                keyboardForward + deskDepth * 0.43f,
                deskForward,
                backForward - 0.11f * h);
            float monitorRight = authoredKeyboardRight;
            float screenForward = monitorForward - 0.097f * h;
            // In this oblique mapped grid, equal right coordinates do not form a line normal to
            // the screen plane. Shift the physical keyboard by the exact skew compensation so
            // screen, keyboard and chair occupy one real perpendicular centreline.
            float gridSkew = Vector3.Dot(gridRightLocalUnit, gridForwardLocalUnit);
            // The occupied chair cell and the desk cell directly behind it share grid x=0.
            // Keep the screen and keyboard on that tile axis. Their own rectangular parts
            // use a perpendicular right axis; a projected desk basis is not a monitor normal.
            if (centerChairOnSeatCell) { monitorRight = 0f; gridSkew = 0f; }
            float keyboardRight =
                monitorRight + gridSkew * (screenForward - keyboardForward);

            AddGridBox("Crt_Base", GridLocal(monitorRight, 0.505f * h, monitorForward),
                0.22f * h, 0.06f * h, 0.17f * h, beige, layer);
            AddGridBox("Crt_Body",
                GridLocal(monitorRight, 0.635f * h, monitorForward + 0.02f * h),
                0.34f * h, 0.25f * h, 0.20f * h, beige, layer);
            AddGridBox("Crt_Bezel",
                GridLocal(monitorRight, 0.635f * h, monitorForward - 0.086f * h),
                0.285f * h, 0.195f * h, 0.016f * h, dark, layer);
            Transform monitor = AddGridBox("Crt_Screen",
                GridLocal(monitorRight, 0.635f * h, screenForward),
                0.245f * h, 0.155f * h, 0.010f * h, screen, layer);
            MonitorWorld = monitor.position;
            for (var line = 0; line < 4; line++)
                AddGridBox("Crt_TextLine_" + line,
                    OperatorLocal(monitorRight + (-0.04f + (line % 2) * 0.025f) * h,
                        (0.675f - line * 0.03f) * h,
                        monitorForward - 0.105f * h),
                    (0.125f + (line % 2) * 0.045f) * h,
                    0.006f * h,
                    0.005f * h,
                    screenGlow,
                    layer);

            Transform keyboard = AddGridBox("Keyboard",
                GridLocal(keyboardRight, 0.495f * h, keyboardForward),
                keyboardWidth,
                0.03f * h,
                keyboardDepth,
                beige,
                layer);
            KeyboardWorld = keyboard.position;
            WorkSurfaceWorld = transform.TransformPoint(
                GridLocal(keyboardRight, topY, keyboardForward));
            for (var row = 0; row < 4; row++)
            for (var column = 0; column < 9; column++)
                AddGridBox("Key_" + row + "_" + column,
                OperatorLocal(keyboardRight + (-0.156f + column * 0.039f) * h,
                        (0.512f + row * 0.0015f) * h,
                        keyboardForward + (-0.037f + row * 0.025f) * h),
                    0.028f * h,
                    0.010f * h,
                    0.017f * h,
                    key,
                    layer);
            float mouseRight = Mathf.Clamp(
                keyboardRight + serviceSide * 0.22f * h,
                deskRight - deskWidth * 0.42f,
                deskRight + deskWidth * 0.42f);
            AddGridBox("Mouse", GridLocal(mouseRight, 0.497f * h,
                    keyboardForward + 0.015f * h),
                0.060f * h, 0.038f * h, 0.085f * h, dark, layer);
            float accessoryRight = deskRight + serviceSide * deskWidth * 0.32f;
            AddGridBox("Telephone",
                GridLocal(accessoryRight, 0.515f * h, deskForward),
                0.135f * h, 0.06f * h, 0.11f * h, dark, layer);
            AddGridBox("Paper",
                GridLocal(accessoryRight, 0.493f * h, backForward - deskDepth * 0.22f),
                0.17f * h, 0.010f * h, 0.14f * h, paper, layer);
            AddCylinder("Mug", GridLocal(accessoryRight + serviceSide * 0.09f * h,
                    0.535f * h, deskForward - deskDepth * 0.08f),
                0.045f * h, 0.08f * h, mugTeal, layer);

            // Restore the exact user-selected V29 chair/actor/CRT composition. The screen face is
            // the -gridForward side, and its real mesh normal is perpendicular to gridRight in
            // this oblique mapped basis.
            Vector3 screenOutwardLocal = Vector3.Cross(
                Vector3.up,
                gridRightLocalUnit).normalized;
            if (Vector3.Dot(screenOutwardLocal, -gridForwardLocalUnit) < 0f)
                screenOutwardLocal = -screenOutwardLocal;
            if (centerChairOnSeatCell) screenOutwardLocal = -gridForwardLocalUnit;
            Vector3 keyboardGroundForSeatLocal = transform.InverseTransformPoint(KeyboardWorld);
            keyboardGroundForSeatLocal.y = 0f;
            // This is the exact V29 screen-front reach point shown in the user's reference. Keep
            // chair and actor here together; the V31 change is grouping only, not a redesign.
            resolvedSeatGroundLocal = centerChairOnSeatCell
                ? Vector3.zero
                : keyboardGroundForSeatLocal + screenOutwardLocal * (0.28f * h);

            // Measure at the operator's lane, not the middle of a two-cell desk. In an
            // oblique projection the latter adds a spurious half-cell clearance offset.
            Vector3 deskFrontGroundLocal = GridLocal(
                centerChairOnSeatCell ? keyboardRight : deskRight, 0f, frontForward);
            Vector3 keyboardGroundMeasuredLocal = keyboardGroundForSeatLocal;
            KeyboardInsetFromDeskFrontWorld = -Vector3.Dot(
                keyboardGroundMeasuredLocal - deskFrontGroundLocal,
                screenOutwardLocal);
            SeatToDeskFrontClearanceWorld = Vector3.Dot(
                resolvedSeatGroundLocal - deskFrontGroundLocal,
                screenOutwardLocal);
            // Includes the chair cushion's 0.09h forward reach plus torso volume and a visible
            // gap. This prevents a numerically aligned seat from ever being accepted while the
            // character or chair is still embedded in the desk.
            MinimumSeatToDeskFrontClearanceWorld = 0.14f * h;
            if (KeyboardInsetFromDeskFrontWorld <= 0f)
                throw new InvalidOperationException(
                    "Keyboard must remain inside the operator-facing desk edge.");
            if (SeatToDeskFrontClearanceWorld < MinimumSeatToDeskFrontClearanceWorld)
                throw new InvalidOperationException(
                    "Chair and seated actor must remain completely outside the desk front edge.");
            chairPivot.localPosition = resolvedSeatGroundLocal;
            ChairGroundWorld = chairPivot.position;
            MonitorScreenOutwardWorld = transform.TransformDirection(
                screenOutwardLocal).normalized;

            Vector3 seatGround = SeatGroundWorld;
            Vector3 keyboardGround = KeyboardWorld;
            seatGround.y = keyboardGround.y = 0f;
            SeatToKeyboardGroundDistance = Vector3.Distance(seatGround, keyboardGround);
            // A tile-centred chair retains the half-tile aisle. Verify actual avatar hand
            // errors independently; the old compact reach moved the chair into that aisle.
            MaximumSeatToKeyboardGroundDistanceWorld = (centerChairOnSeatCell ? 0.50f : 0.30f) * h;
            if (SeatToKeyboardGroundDistance > MaximumSeatToKeyboardGroundDistanceWorld)
                throw new InvalidOperationException(
                    "Chair must remain within the seated character's compact keyboard reach.");
            Vector3 monitorGround = MonitorWorld;
            monitorGround.y = 0f;
            KeyboardToMonitorScreenGroundDistanceWorld = Vector3.Distance(
                keyboardGround,
                monitorGround);
            // V24/V25 put the screen only about four centimetres behind the keyboard. Hands were
            // correctly on the keys but projected against the lower bezel. Require a distinct
            // keyboard-to-screen band so typing still reads correctly in the isometric camera.
            MinimumKeyboardToMonitorScreenGroundDistanceWorld = 0.07f * h;
            if (KeyboardToMonitorScreenGroundDistanceWorld <
                MinimumKeyboardToMonitorScreenGroundDistanceWorld)
                throw new InvalidOperationException(
                    "CRT screen must remain visibly behind the physical keyboard.");

            Vector3 monitorDirectionWorld = MonitorWorld - SeatGroundWorld;
            monitorDirectionWorld.y = 0f;
            if (monitorDirectionWorld.sqrMagnitude <= 0.000001f)
                throw new InvalidOperationException(
                    "CRT monitor must be separated from its operator seat.");
            Quaternion exactMonitorFacingWorld = Quaternion.LookRotation(
                monitorDirectionWorld.normalized,
                Vector3.up);
            chairFacingLocalRotation = Quaternion.Inverse(transform.rotation) *
                                       exactMonitorFacingWorld;
            seatedActorLocalRotation = Quaternion.Inverse(transform.rotation) *
                                       exactMonitorFacingWorld *
                                       Quaternion.Euler(
                                           0f,
                                           actorModelForwardYawOffsetDegrees +
                                           seatedVisualYawOffsetDegrees,
                                           0f);
            chairPivot.localRotation = chairFacingLocalRotation;
        }

        private float MeasureSeatedFacingError(Vector3 targetWorld)
        {
            Vector3 targetDirection = targetWorld - SeatGroundWorld;
            targetDirection.y = 0f;
            Vector3 seatedForward = SeatedBodyForwardWorld;
            seatedForward.y = 0f;
            return targetDirection.sqrMagnitude <= 0.000001f
                ? 180f
                : Vector3.Angle(seatedForward, targetDirection);
        }

        private float MeasureChairFacingError(Vector3 targetWorld)
        {
            Vector3 targetDirection = targetWorld - SeatGroundWorld;
            targetDirection.y = 0f;
            Vector3 chairForward = ChairRotationWorld * Vector3.forward;
            chairForward.y = 0f;
            return targetDirection.sqrMagnitude <= 0.000001f
                ? 180f
                : Vector3.Angle(chairForward, targetDirection);
        }

        private float MeasureMonitorScreenToSeatFacingError()
        {
            Vector3 screenToSeat = SeatGroundWorld - MonitorWorld;
            screenToSeat.y = 0f;
            Vector3 screenOutward = MonitorScreenOutwardWorld;
            screenOutward.y = 0f;
            return screenToSeat.sqrMagnitude <= 0.000001f ||
                   screenOutward.sqrMagnitude <= 0.000001f
                ? 180f
                : Vector3.Angle(screenOutward, screenToSeat);
        }

        private Vector2 LocalToGridCoordinates(Vector3 localPosition)
        {
            float determinant = gridRightLocalUnit.x * gridForwardLocalUnit.z -
                                gridForwardLocalUnit.x * gridRightLocalUnit.z;
            if (Mathf.Abs(determinant) <= 0.000001f)
                throw new InvalidOperationException(
                    "Mapped office grid axes are parallel and cannot define a desk footprint.");
            float right = (localPosition.x * gridForwardLocalUnit.z -
                           gridForwardLocalUnit.x * localPosition.z) / determinant;
            float forward = (gridRightLocalUnit.x * localPosition.z -
                             localPosition.x * gridRightLocalUnit.z) / determinant;
            return new Vector2(right, forward);
        }

        private Vector3 GridLocal(float right, float y, float forward)
        {
            return gridRightLocalUnit * right + Vector3.up * y +
                   gridForwardLocalUnit * forward;
        }

        private Vector3 OperatorLocal(float right, float y, float forward)
        {
            Vector3 operatorRight = centerChairOnSeatCell
                ? Vector3.Cross(Vector3.up, gridForwardLocalUnit).normalized : gridRightLocalUnit;
            return operatorRight * right + Vector3.up * y + gridForwardLocalUnit * forward;
        }

        private Material CreateMaterial(string materialName, Color colour, float metallic, float smoothness)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null)
                throw new InvalidOperationException("Standard shader is missing from the production build.");
            var material = new Material(shader)
            {
                name = "V31Production_" + materialName,
                color = colour
            };
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", Color.black);
            material.DisableKeyword("_EMISSION");
            ownedMaterials.Add(material);
            return material;
        }

        private Transform AddBox(
            string objectName,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            int layer,
            Vector3? localEuler = null,
            Transform localParent = null)
        {
            GameObject value = GameObject.CreatePrimitive(PrimitiveType.Cube);
            value.name = objectName;
            value.transform.SetParent(localParent == null ? transform : localParent, false);
            value.transform.localPosition = localPosition;
            value.transform.localRotation = Quaternion.Euler(localEuler ?? Vector3.zero);
            value.transform.localScale = localScale;
            value.layer = layer;
            Renderer renderer = value.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            Collider collider = value.GetComponent<Collider>();
            if (collider != null) ReleaseOwnedObject(collider);
            return value.transform;
        }

        private Transform AddGridBox(
            string objectName,
            Vector3 localPosition,
            float width,
            float height,
            float depth,
            Material material,
            int layer)
        {
            var value = new GameObject(objectName);
            value.transform.SetParent(transform, false);
            value.transform.localPosition = localPosition;
            value.transform.localRotation = Quaternion.identity;
            value.transform.localScale = Vector3.one;
            value.layer = layer;

            bool operatorPart = centerChairOnSeatCell &&
                (objectName.StartsWith("Crt_", StringComparison.Ordinal) ||
                 objectName.StartsWith("Key_", StringComparison.Ordinal) || objectName == "Keyboard");
            Vector3 right = (operatorPart
                ? Vector3.Cross(Vector3.up, gridForwardLocalUnit).normalized : gridRightLocalUnit) * (width * 0.5f);
            Vector3 forward = gridForwardLocalUnit * (depth * 0.5f);
            Vector3 up = Vector3.up * (height * 0.5f);
            var mesh = new Mesh { name = objectName + "_MappedGridMesh" };
            mesh.vertices = new[]
            {
                -right - forward - up,
                right - forward - up,
                right + forward - up,
                -right + forward - up,
                -right - forward + up,
                right - forward + up,
                right + forward + up,
                -right + forward + up
            };
            int[] triangles =
            {
                0, 1, 2, 0, 2, 3,
                4, 6, 5, 4, 7, 6,
                0, 4, 5, 0, 5, 1,
                1, 5, 6, 1, 6, 2,
                2, 6, 7, 2, 7, 3,
                3, 7, 4, 3, 4, 0
            };
            if (Vector3.Dot(Vector3.Cross(gridRightLocalUnit, gridForwardLocalUnit),
                    Vector3.up) > 0f)
                for (var triangle = 0; triangle < triangles.Length; triangle += 3)
                {
                    int swap = triangles[triangle + 1];
                    triangles[triangle + 1] = triangles[triangle + 2];
                    triangles[triangle + 2] = swap;
                }
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            value.AddComponent<MeshFilter>().sharedMesh = mesh;
            value.AddComponent<MeshRenderer>().sharedMaterial = material;
            ownedMeshes.Add(mesh);
            return value.transform;
        }

        private Transform AddCylinder(
            string objectName,
            Vector3 localPosition,
            float radius,
            float height,
            Material material,
            int layer,
            Transform localParent = null)
        {
            GameObject value = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            value.name = objectName;
            value.transform.SetParent(localParent == null ? transform : localParent, false);
            value.transform.localPosition = localPosition;
            value.transform.localRotation = Quaternion.identity;
            // Unity's cylinder primitive is two units high and one unit in radius.
            value.transform.localScale = new Vector3(radius, height * 0.5f, radius);
            value.layer = layer;
            value.GetComponent<Renderer>().sharedMaterial = material;
            Collider collider = value.GetComponent<Collider>();
            if (collider != null) ReleaseOwnedObject(collider);
            return value.transform;
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform)
                SetLayerRecursively(child.gameObject, layer);
        }

        private void OnDestroy()
        {
            for (var index = 0; index < ownedMeshes.Count; index++)
                if (ownedMeshes[index] != null)
                    ReleaseOwnedObject(ownedMeshes[index]);
            ownedMeshes.Clear();
            for (var index = 0; index < ownedMaterials.Count; index++)
                if (ownedMaterials[index] != null)
                    ReleaseOwnedObject(ownedMaterials[index]);
            ownedMaterials.Clear();
        }

        private static void ReleaseOwnedObject(UnityEngine.Object value)
        {
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }
}
