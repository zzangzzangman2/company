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
        private float seatedVisualYawOffsetDegrees;

        public Vector3 SeatGroundWorld => transform.position;
        public Vector3 ForwardWorld => transform.forward;
        public Quaternion GridRotationWorld => transform.rotation;
        public Quaternion SeatedRotationWorld =>
            transform.rotation * Quaternion.Euler(0f, seatedVisualYawOffsetDegrees, 0f);
        public float SeatedVisualYawOffsetDegrees => seatedVisualYawOffsetDegrees;
        public float CushionWorldY { get; private set; }
        public Vector3 KeyboardWorld { get; private set; }
        public Vector3 WorkSurfaceWorld { get; private set; }

        public static Family3DWorkstationQa Create(
            Transform parent,
            int layer,
            Vector3 seatGroundWorld,
            Vector3 forwardWorld,
            float characterHeight,
            float visualYawOffsetDegrees)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            forwardWorld.y = 0f;
            if (forwardWorld.sqrMagnitude <= 0.000001f)
                throw new ArgumentException("Workstation forward must be non-zero.", nameof(forwardWorld));

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

            // Desk dimensions are keyed to the approved V19 body height, so the same actual map
            // camera scale cannot make the furniture gigantic or toy-sized.
            float topY = 0.455f * h;
            float deskZ = 0.44f * h;
            AddBox("Desk_Top", new Vector3(0f, topY, deskZ),
                new Vector3(0.84f, 0.055f, 0.38f) * h, deskWood, layer);
            AddBox("Desk_FrontEdge", new Vector3(0f, topY - 0.022f * h, 0.248f * h),
                new Vector3(0.86f, 0.070f, 0.035f) * h, deskEdge, layer);
            foreach (float x in new[] { -0.365f, 0.365f })
            foreach (float z in new[] { 0.29f, 0.59f })
                AddBox("Desk_Leg", new Vector3(x * h, 0.215f * h, z * h),
                    new Vector3(0.060f, 0.43f, 0.060f) * h, deskEdge, layer);
            AddBox("Desk_Drawers", new Vector3(0.30f * h, 0.255f * h, 0.45f * h),
                new Vector3(0.20f, 0.32f, 0.25f) * h, deskWood, layer);
            for (var drawer = 0; drawer < 3; drawer++)
            {
                float y = (0.17f + drawer * 0.09f) * h;
                AddBox("Desk_DrawerLine_" + drawer,
                    new Vector3(0.30f * h, y, 0.318f * h),
                    new Vector3(0.165f, 0.010f, 0.010f) * h, deskEdge, layer);
                AddBox("Desk_DrawerHandle_" + drawer,
                    new Vector3(0.30f * h, y + 0.028f * h, 0.31f * h),
                    new Vector3(0.07f, 0.015f, 0.018f) * h, chairTrim, layer);
            }

            // CRT, keyboard, mouse, phone, papers and a mug make this read as a working desk in
            // the same small isometric map view, not as an anonymous brown block.
            AddBox("Crt_Base", new Vector3(-0.07f * h, 0.505f * h, 0.52f * h),
                new Vector3(0.22f, 0.06f, 0.17f) * h, beige, layer);
            AddBox("Crt_Body", new Vector3(-0.07f * h, 0.635f * h, 0.54f * h),
                new Vector3(0.34f, 0.25f, 0.20f) * h, beige, layer, new Vector3(-3f, 0f, 0f));
            AddBox("Crt_Bezel", new Vector3(-0.07f * h, 0.635f * h, 0.434f * h),
                new Vector3(0.285f, 0.195f, 0.016f) * h, dark, layer, new Vector3(-3f, 0f, 0f));
            AddBox("Crt_Screen", new Vector3(-0.07f * h, 0.635f * h, 0.423f * h),
                new Vector3(0.245f, 0.155f, 0.010f) * h, screen, layer, new Vector3(-3f, 0f, 0f));
            for (var line = 0; line < 4; line++)
                AddBox("Crt_TextLine_" + line,
                    new Vector3((-0.11f + (line % 2) * 0.025f) * h,
                        (0.675f - line * 0.03f) * h,
                        0.415f * h),
                    new Vector3((0.125f + (line % 2) * 0.045f) * h, 0.006f * h, 0.005f * h),
                    screenGlow,
                    layer,
                    new Vector3(-3f, 0f, 0f));

            Transform keyboard = AddBox("Keyboard", new Vector3(-0.04f * h, 0.495f * h, 0.300f * h),
                new Vector3(0.35f, 0.03f, 0.125f) * h, beige, layer, new Vector3(5f, 0f, 0f));
            KeyboardWorld = keyboard.position;
            WorkSurfaceWorld = transform.TransformPoint(new Vector3(0f, topY, 0.30f * h));
            for (var row = 0; row < 4; row++)
            for (var column = 0; column < 9; column++)
                AddBox("Key_" + row + "_" + column,
                    new Vector3((-0.17f + column * 0.039f) * h,
                        (0.512f + row * 0.0015f) * h,
                        (0.263f + row * 0.025f) * h),
                    new Vector3(0.028f, 0.010f, 0.017f) * h,
                    key,
                    layer,
                    new Vector3(5f, 0f, 0f));
            AddBox("Mouse", new Vector3(0.22f * h, 0.497f * h, 0.315f * h),
                new Vector3(0.060f, 0.038f, 0.085f) * h, dark, layer, new Vector3(5f, 0f, 0f));
            AddBox("Telephone", new Vector3(-0.30f * h, 0.515f * h, 0.40f * h),
                new Vector3(0.135f, 0.06f, 0.11f) * h, dark, layer);
            AddBox("Paper", new Vector3(0.23f * h, 0.493f * h, 0.50f * h),
                new Vector3(0.17f, 0.010f, 0.14f) * h, paper, layer, new Vector3(0f, 7f, 0f));
            AddCylinder("Mug", new Vector3(0.32f * h, 0.535f * h, 0.43f * h),
                0.045f * h, 0.08f * h, chairTeal, layer);
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
            for (var index = 0; index < ownedMaterials.Count; index++)
                if (ownedMaterials[index] != null)
                    Destroy(ownedMaterials[index]);
            ownedMaterials.Clear();
        }
    }
}
