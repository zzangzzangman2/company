using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace FamilyCompany.Editor
{
    /// <summary>
    /// Replaces Mixamo's probe mesh with the canonical protagonist costume while keeping the
    /// downloaded Humanoid skeleton and animation untouched. The visual is intentionally built
    /// from simple closed volumes: it bakes cleanly to hard-alpha dot art, follows every Mixamo
    /// bone, and does not inherit the disconnected paper-doll anatomy of the previous candidate.
    /// </summary>
    internal static class PlayerWalkCanonicalVisualBuilder
    {
        public const string PresetId = "canonical-protagonist-v1";

        internal sealed class Handle : IDisposable
        {
            private readonly List<Material> _materials;

            public Handle(List<Material> materials) => _materials = materials;

            public void Dispose()
            {
                foreach (Material material in _materials)
                    if (material != null) Object.DestroyImmediate(material);
                _materials.Clear();
            }
        }

        internal static Handle Attach(GameObject rig)
        {
            if (rig == null) throw new ArgumentNullException(nameof(rig));

            Transform hips = Find(rig.transform, "mixamorig:Hips");
            Transform spine1 = Find(rig.transform, "mixamorig:Spine1");
            Transform spine2 = Find(rig.transform, "mixamorig:Spine2");
            Transform neck = Find(rig.transform, "mixamorig:Neck");
            Transform head = Find(rig.transform, "mixamorig:Head");
            Transform leftShoulder = Find(rig.transform, "mixamorig:LeftShoulder");
            Transform rightShoulder = Find(rig.transform, "mixamorig:RightShoulder");
            Transform leftArm = Find(rig.transform, "mixamorig:LeftArm");
            Transform leftForeArm = Find(rig.transform, "mixamorig:LeftForeArm");
            Transform leftHand = Find(rig.transform, "mixamorig:LeftHand");
            Transform rightArm = Find(rig.transform, "mixamorig:RightArm");
            Transform rightForeArm = Find(rig.transform, "mixamorig:RightForeArm");
            Transform rightHand = Find(rig.transform, "mixamorig:RightHand");
            Transform leftUpLeg = Find(rig.transform, "mixamorig:LeftUpLeg");
            Transform leftLeg = Find(rig.transform, "mixamorig:LeftLeg");
            Transform leftFoot = Find(rig.transform, "mixamorig:LeftFoot");
            Transform rightUpLeg = Find(rig.transform, "mixamorig:RightUpLeg");
            Transform rightLeg = Find(rig.transform, "mixamorig:RightLeg");
            Transform rightFoot = Find(rig.transform, "mixamorig:RightFoot");

            foreach (Renderer renderer in rig.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = false;

            var materials = new List<Material>();
            Material jacket = MakeMaterial(materials, "Hero Jacket", "#F5F0E7");
            Material jacketShade = MakeMaterial(materials, "Hero Jacket Shade", "#D9D4CC");
            Material stripe = MakeMaterial(materials, "Hero Shirt Stripe", "#F1E6C9");
            Material navy = MakeMaterial(materials, "Hero Navy", "#43516D");
            Material red = MakeMaterial(materials, "Hero Cap Red", "#C94F5B");
            Material redShade = MakeMaterial(materials, "Hero Cap Shade", "#9F3945");
            Material yellow = MakeMaterial(materials, "Hero Shirt Yellow", "#E2BF55");
            Material gold = MakeMaterial(materials, "Hero Cap Pin", "#D9AD45");
            Material skin = MakeMaterial(materials, "Hero Skin", "#EFB494");
            Material hair = MakeMaterial(materials, "Hero Hair", "#563D38");
            Material ink = MakeMaterial(materials, "Hero Ink", "#28283A");
            Material sole = MakeMaterial(materials, "Hero Shoe", "#302D38");

            Vector3 up = (head.position - hips.position).normalized;
            Vector3 right = (rightShoulder.position - leftShoulder.position).normalized;
            Vector3 forward = Vector3.Cross(right, up).normalized;
            if (Vector3.Dot(forward, rig.transform.forward) < 0f) forward = -forward;
            Quaternion bodyRotation = Quaternion.LookRotation(forward, up);

            // Jacket and striped shirt. The cream outer volume overlaps every shoulder/hip seam;
            // the front panel preserves the canonical striped-shirt read at sprite scale.
            Vector3 torsoCenter = Vector3.Lerp(hips.position, neck.position, 0.55f);
            AddVolume("Hero_JacketTorso", PrimitiveType.Capsule, spine1, torsoCenter,
                bodyRotation, new Vector3(0.42f, 0.31f, 0.25f), jacket);
            AddVolume("Hero_ShirtPanel", PrimitiveType.Cube, spine2,
                torsoCenter + forward * 0.145f, bodyRotation,
                new Vector3(0.22f, 0.34f, 0.025f), navy);
            for (var stripeIndex = -2; stripeIndex <= 2; stripeIndex++)
            {
                Material band = stripeIndex == 0
                    ? yellow
                    : Mathf.Abs(stripeIndex) == 2
                        ? red
                        : stripe;
                AddVolume("Hero_ShirtStripe_" + stripeIndex, PrimitiveType.Cube, spine2,
                    torsoCenter + up * (stripeIndex * 0.065f) + forward * 0.174f,
                    bodyRotation,
                    new Vector3(0.21f, 0.022f, 0.012f), band);
            }
            AddVolume("Hero_JacketLeftFront", PrimitiveType.Cube, spine2,
                torsoCenter - right * 0.19f + forward * 0.17f, bodyRotation,
                new Vector3(0.075f, 0.35f, 0.035f), jacketShade);
            AddVolume("Hero_JacketRightFront", PrimitiveType.Cube, spine2,
                torsoCenter + right * 0.19f + forward * 0.17f, bodyRotation,
                new Vector3(0.075f, 0.35f, 0.035f), jacket);

            // Hood behind the head, then skin/hair/hat in front. The broad red crown and forward
            // brim are the direction-readable newsboy-cap silhouette from CANON.md.
            AddVolume("Hero_Hood", PrimitiveType.Sphere, neck,
                neck.position + up * 0.13f - forward * 0.055f, bodyRotation,
                new Vector3(0.42f, 0.45f, 0.28f), jacketShade);
            Vector3 faceCenter = head.position + up * 0.105f;
            AddVolume("Hero_Head", PrimitiveType.Sphere, head, faceCenter, bodyRotation,
                new Vector3(0.32f, 0.38f, 0.30f), skin);
            AddVolume("Hero_Hair", PrimitiveType.Sphere, head,
                faceCenter + up * 0.105f - forward * 0.035f, bodyRotation,
                new Vector3(0.34f, 0.20f, 0.31f), hair);
            AddVolume("Hero_CapCrown", PrimitiveType.Sphere, head,
                faceCenter + up * 0.205f - forward * 0.005f, bodyRotation,
                new Vector3(0.36f, 0.14f, 0.32f), red);
            AddVolume("Hero_CapBand", PrimitiveType.Cube, head,
                faceCenter + up * 0.16f + forward * 0.005f, bodyRotation,
                new Vector3(0.34f, 0.035f, 0.30f), redShade);
            AddVolume("Hero_CapBrim", PrimitiveType.Cube, head,
                faceCenter + up * 0.19f + forward * 0.11f, bodyRotation,
                new Vector3(0.34f, 0.025f, 0.12f), red);
            AddVolume("Hero_CapPin", PrimitiveType.Sphere, head,
                faceCenter + up * 0.16f + forward * 0.185f, bodyRotation,
                Vector3.one * 0.035f, gold);
            AddVolume("Hero_LeftEye", PrimitiveType.Sphere, head,
                faceCenter - up * 0.015f - right * 0.055f + forward * 0.175f,
                bodyRotation, Vector3.one * 0.022f, ink);
            AddVolume("Hero_RightEye", PrimitiveType.Sphere, head,
                faceCenter - up * 0.015f + right * 0.055f + forward * 0.175f,
                bodyRotation, Vector3.one * 0.022f, ink);
            AddVolume("Hero_Mouth", PrimitiveType.Cube, head,
                faceCenter - up * 0.085f + forward * 0.172f,
                bodyRotation, new Vector3(0.055f, 0.012f, 0.012f), redShade);

            // Sleeves, hands, trousers, and shoes are bone-owned closed volumes. Joint spheres
            // overlap their neighbours so no frame can separate into mechanical paper-doll parts.
            AddJoint("Hero_LeftShoulder", leftArm, 0.17f, jacket);
            AddLimb("Hero_LeftUpperSleeve", leftArm, leftForeArm, 0.14f, jacket);
            AddJoint("Hero_LeftElbow", leftForeArm, 0.145f, jacketShade);
            AddLimb("Hero_LeftLowerSleeve", leftForeArm, leftHand, 0.12f, jacketShade);
            AddJoint("Hero_LeftHand", leftHand, 0.12f, skin);
            AddJoint("Hero_RightShoulder", rightArm, 0.17f, jacket);
            AddLimb("Hero_RightUpperSleeve", rightArm, rightForeArm, 0.14f, jacket);
            AddJoint("Hero_RightElbow", rightForeArm, 0.145f, jacketShade);
            AddLimb("Hero_RightLowerSleeve", rightForeArm, rightHand, 0.12f, jacketShade);
            AddJoint("Hero_RightHand", rightHand, 0.12f, skin);

            AddVolume("Hero_Hips", PrimitiveType.Capsule, hips, hips.position + up * 0.015f,
                bodyRotation, new Vector3(0.38f, 0.145f, 0.24f), navy);
            AddLimb("Hero_LeftThigh", leftUpLeg, leftLeg, 0.20f, navy);
            AddJoint("Hero_LeftKnee", leftLeg, 0.20f, navy);
            AddLimb("Hero_LeftShin", leftLeg, leftFoot, 0.16f, navy);
            AddLimb("Hero_RightThigh", rightUpLeg, rightLeg, 0.20f, navy);
            AddJoint("Hero_RightKnee", rightLeg, 0.20f, navy);
            AddLimb("Hero_RightShin", rightLeg, rightFoot, 0.16f, navy);
            AddShoe("Hero_LeftShoe", leftFoot);
            AddShoe("Hero_RightShoe", rightFoot);

            return new Handle(materials);

            void AddShoe(string name, Transform foot)
            {
                AddVolume(name, PrimitiveType.Cube, foot,
                    foot.position + up * 0.02f + forward * 0.11f,
                    bodyRotation, new Vector3(0.18f, 0.10f, 0.32f), sole);
            }
        }

        private static void AddLimb(
            string name,
            Transform parent,
            Transform child,
            float radius,
            Material material)
        {
            Vector3 direction = child.position - parent.position;
            float length = direction.magnitude;
            if (length <= 0.0001f)
                throw new InvalidOperationException("Canonical visual limb has zero length: " + name);
            AddVolume(
                name,
                PrimitiveType.Capsule,
                parent,
                Vector3.Lerp(parent.position, child.position, 0.5f),
                Quaternion.FromToRotation(Vector3.up, direction / length),
                new Vector3(radius, length * 0.5f + radius * 0.25f, radius),
                material);
        }

        private static void AddJoint(string name, Transform parent, float radius, Material material) =>
            AddVolume(
                name,
                PrimitiveType.Sphere,
                parent,
                parent.position,
                Quaternion.identity,
                Vector3.one * radius,
                material);

        private static void AddVolume(
            string name,
            PrimitiveType type,
            Transform parent,
            Vector3 worldPosition,
            Quaternion worldRotation,
            Vector3 worldScale,
            Material material)
        {
            GameObject volume = GameObject.CreatePrimitive(type);
            volume.name = name;
            volume.transform.SetPositionAndRotation(worldPosition, worldRotation);
            volume.transform.localScale = worldScale;
            volume.transform.SetParent(parent, true);
            if (volume.TryGetComponent(out Collider collider)) Object.DestroyImmediate(collider);
            MeshRenderer renderer = volume.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private static Material MakeMaterial(List<Material> materials, string name, string html)
        {
            Shader shader = Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default") ??
                            Shader.Find("Standard");
            if (shader == null)
                throw new InvalidOperationException("No deterministic color shader is available for walk bake.");
            if (!ColorUtility.TryParseHtmlString(html, out Color color))
                throw new InvalidOperationException("Canonical visual color is invalid: " + html);
            var material = new Material(shader)
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave,
                color = color
            };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            materials.Add(material);
            return material;
        }

        private static Transform Find(Transform root, string name)
        {
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
                if (string.Equals(candidate.name, name, StringComparison.Ordinal))
                    return candidate;
            throw new InvalidOperationException("Canonical visual requires Mixamo bone: " + name);
        }
    }
}
