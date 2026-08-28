using System;
using System.Collections.Generic;
using UnityEngine;

namespace FamilyCompany.Experimental.Family3D
{
    /// <summary>
    /// Runtime-only 3D replacement for one real Starter Office CRT workstation. The semantic
    /// desk, chair, seat claim and route stay owned by the production runtime; this object only
    /// replaces their pixels on the isolated QA layer so the V19 body can be judged while seated.
    /// </summary>
    public sealed class Family3DWorkstationQa : MonoBehaviour
    {
        private readonly List<Material> ownedMaterials = new List<Material>();
        private readonly List<Mesh> ownedMeshes = new List<Mesh>();
        private float seatedVisualYawOffsetDegrees;
        private Vector3 deskFootprintCenterLocal;
        private Vector3 keyboardGroundLocal;
        private Vector3 gridRightLocalUnit;
        private Vector3 gridForwardLocalUnit;
        private float deskFootprintWidthWorld;
        private float deskFootprintDepthWorld;

        public Vector3 SeatGroundWorld => transform.position;
        public Vector3 ForwardWorld => transform.forward;
        public Quaternion GridRotationWorld => transform.rotation;
        public Quaternion SeatedRotationWorld =>
            transform.rotation * Quaternion.Euler(0f, seatedVisualYawOffsetDegrees, 0f);
        public float SeatedVisualYawOffsetDegrees => seatedVisualYawOffsetDegrees;
        public float CushionWorldY { get; private set; }
        public Vector3 KeyboardWorld { get; private set; }
        public Vector3 WorkSurfaceWorld { get; private set; }
        public Vector3 DeskTopCenterWorld { get; private set; }
        public Vector3 DeskFootprintCenterWorld =>
            transform.TransformPoint(deskFootprintCenterLocal);
        public float DeskFootprintWidthWorld => deskFootprintWidthWorld;
        public float DeskFootprintDepthWorld => deskFootprintDepthWorld;
        public float GridAxisOrthogonalityErrorDegrees { get; private set; }
        public float SeatToKeyboardGroundDistance { get; private set; }

        public static Family3DWorkstationQa Create(
            Transform parent,
            int layer,
            Vector3 seatGroundWorld,
            Vector3 gridRightWorld,
            Vector3 forwardWorld,
            Vector3 deskFootprintCenterWorld,
            float deskFootprintWidthWorld,
            float deskFootprintDepthWorld,
            Vector3 keyboardGroundWorld,
            float characterHeight,
            float visualYawOffsetDegrees)
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

            var root = new GameObject("FatherV19_Full3D_CrtWorkstation_QaOnly");
            root.transform.SetParent(parent, false);
            // Furniture roots are a map-grid contract. Keep the desk, CRT and keyboard on the
            // exact canonical seat axis so later desks/tables share one placement rule. Only the
            // swivel chair and seated actor use the readability offset around the seat anchor.
            root.transform.SetPositionAndRotation(
                seatGroundWorld,
                Quaternion.LookRotation(forwardWorld.normalized, Vector3.up));
            SetLayerRecursively(root, layer);

            var result = root.AddComponent<Family3DWorkstationQa>();
            result.seatedVisualYawOffsetDegrees = visualYawOffsetDegrees;
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
            Material chairTeal = CreateMaterial(
                "Chair_WarmLeather",
                new Color(0.42f, 0.16f, 0.07f, 1f),
                0f,
                0.32f);
            Material chairTrim = CreateMaterial(
                "Chair_DarkTrim",
                new Color(0.035f, 0.075f, 0.075f, 1f),
                0.12f,
                0.34f);
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

            float seatY = 0.265f * h;
            CushionWorldY = transform.position.y + seatY + 0.028f * h;

            var chairPivotObject = new GameObject("Chair_SwivelPivot");
            chairPivotObject.transform.SetParent(transform, false);
            chairPivotObject.transform.localRotation = Quaternion.Euler(
                0f,
                seatedVisualYawOffsetDegrees,
                0f);
            chairPivotObject.layer = layer;
            Transform chairPivot = chairPivotObject.transform;

            // Keep the chair visually real but deliberately open-backed. The canonical father
            // seat faces away from the isometric camera, so a conventional solid high back hid
            // the torso, elbows and both legs even though the pose itself was valid. Two slim
            // uprights and one lumbar rail preserve a readable chair silhouette without masking
            // the body that this QA exists to judge.
            AddBox("Chair_Cushion", new Vector3(0f, seatY, -0.050f * h),
                new Vector3(0.30f, 0.050f, 0.23f) * h, chairTeal, layer,
                new Vector3(5f, 0f, 0f), chairPivot);
            foreach (float x in new[] { -0.135f, 0.135f })
                AddBox("Chair_BackUpright", new Vector3(x * h, 0.385f * h, -0.185f * h),
                    new Vector3(0.035f, 0.26f, 0.04f) * h, chairTrim, layer,
                    new Vector3(-7f, 0f, 0f), chairPivot);
            AddBox("Chair_LumbarRail", new Vector3(0f, 0.405f * h, -0.19f * h),
                new Vector3(0.30f, 0.085f, 0.045f) * h, chairTeal, layer,
                new Vector3(-7f, 0f, 0f), chairPivot);
            AddCylinder("Chair_Stem", new Vector3(0f, 0.132f * h, -0.015f * h),
                0.03f * h, 0.225f * h, chairTrim, layer, chairPivot);
            AddCylinder("Chair_RoundFoot", new Vector3(0f, 0.025f * h, -0.015f * h),
                0.14f * h, 0.025f * h, chairTrim, layer, chairPivot);

            // Horizontal placement comes only from the semantic two-tile desk footprint. The
            // isometric viewport-to-ground mapping is intentionally oblique: its two tile axes
            // are not perpendicular in this QA world. Consequently every rectangular furniture
            // part is built as a parallelepiped in the mapped grid basis. Rotating an ordinary
            // cube can align one tile axis or the other, but can never land all four corners on
            // the real semantic footprint.
            float topY = 0.455f * h;
            float deskWidth = deskFootprintWidthWorld * 0.90f;
            float deskDepth = deskFootprintDepthWorld * 0.86f;
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

            float serviceSide = Mathf.Sign(deskRight - keyboardGrid.x);
            if (Mathf.Abs(serviceSide) < 0.5f) serviceSide = 1f;
            float drawerRight = deskRight + serviceSide * deskWidth * 0.34f;
            float drawerForward = deskForward + deskDepth * 0.03f;
            AddGridBox("Desk_Drawers", GridLocal(drawerRight, 0.255f * h, drawerForward),
                Mathf.Min(0.20f * h, deskWidth * 0.24f),
                0.32f * h,
                Mathf.Min(0.25f * h, deskDepth * 0.68f),
                deskWood,
                layer);
            for (var drawer = 0; drawer < 3; drawer++)
            {
                float y = (0.17f + drawer * 0.09f) * h;
                AddGridBox("Desk_DrawerLine_" + drawer,
                    GridLocal(drawerRight, y, frontForward - 0.010f * h),
                    0.165f * h, 0.010f * h, 0.010f * h, deskEdge, layer);
                AddGridBox("Desk_DrawerHandle_" + drawer,
                    GridLocal(drawerRight, y + 0.028f * h, frontForward - 0.018f * h),
                    0.07f * h, 0.015f * h, 0.018f * h, chairTrim, layer);
            }

            // CRT, keyboard, mouse, phone, papers and a mug make this read as a working desk in
            // the same small isometric map view, not as an anonymous brown block. Keyboard X/Z is
            // the real authored operator-work socket, clamped only far enough inside the top to
            // keep the physical keyboard from overhanging the semantic footprint.
            float keyboardWidth = Mathf.Min(0.35f * h, deskWidth * 0.43f);
            float keyboardDepth = Mathf.Min(0.125f * h, deskDepth * 0.33f);
            float keyboardRight = Mathf.Clamp(
                keyboardGrid.x,
                deskRight - deskWidth * 0.5f + keyboardWidth * 0.5f,
                deskRight + deskWidth * 0.5f - keyboardWidth * 0.5f);
            float keyboardForward = Mathf.Clamp(
                keyboardGrid.y,
                frontForward + keyboardDepth * 0.5f + 0.008f * h,
                backForward - keyboardDepth * 0.5f - 0.008f * h);
            float monitorForward = Mathf.Clamp(
                keyboardForward + deskDepth * 0.28f,
                deskForward,
                backForward - 0.11f * h);
            // Keep the monitor on the same semantic top, but slightly off the operator's face
            // line. The exact work socket still owns the keyboard and both hands; this lateral
            // shift makes the seated head, elbows and typing motion readable at map scale.
            float monitorRight = Mathf.Clamp(
                keyboardRight - 0.12f * h,
                deskRight - deskWidth * 0.5f + 0.17f * h,
                deskRight + deskWidth * 0.5f - 0.17f * h);

            AddGridBox("Crt_Base", GridLocal(monitorRight, 0.505f * h, monitorForward),
                0.22f * h, 0.06f * h, 0.17f * h, beige, layer);
            AddGridBox("Crt_Body",
                GridLocal(monitorRight, 0.635f * h, monitorForward + 0.02f * h),
                0.34f * h, 0.25f * h, 0.20f * h, beige, layer);
            AddGridBox("Crt_Bezel",
                GridLocal(monitorRight, 0.635f * h, monitorForward - 0.086f * h),
                0.285f * h, 0.195f * h, 0.016f * h, dark, layer);
            AddGridBox("Crt_Screen",
                GridLocal(monitorRight, 0.635f * h, monitorForward - 0.097f * h),
                0.245f * h, 0.155f * h, 0.010f * h, screen, layer);
            for (var line = 0; line < 4; line++)
                AddGridBox("Crt_TextLine_" + line,
                    GridLocal(monitorRight + (-0.04f + (line % 2) * 0.025f) * h,
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
            Vector3 seatGround = SeatGroundWorld;
            Vector3 keyboardGround = KeyboardWorld;
            seatGround.y = keyboardGround.y = 0f;
            SeatToKeyboardGroundDistance = Vector3.Distance(seatGround, keyboardGround);
            for (var row = 0; row < 4; row++)
            for (var column = 0; column < 9; column++)
                AddGridBox("Key_" + row + "_" + column,
                    GridLocal(keyboardRight + (-0.156f + column * 0.039f) * h,
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
                0.045f * h, 0.08f * h, chairTeal, layer);
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

        private Material CreateMaterial(string materialName, Color colour, float metallic, float smoothness)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null)
                throw new InvalidOperationException("Standard shader is missing from the QA build.");
            var material = new Material(shader)
            {
                name = "FatherV19Qa_" + materialName,
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
            if (collider != null) Destroy(collider);
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

            Vector3 right = gridRightLocalUnit * (width * 0.5f);
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
            if (collider != null) Destroy(collider);
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
                    Destroy(ownedMeshes[index]);
            ownedMeshes.Clear();
            for (var index = 0; index < ownedMaterials.Count; index++)
                if (ownedMaterials[index] != null)
                    Destroy(ownedMaterials[index]);
            ownedMaterials.Clear();
        }
    }
}
