using System;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor
{
    /// <summary>
    /// Forces every model dropped into the humanoid bake folder to import as a Humanoid with an
    /// Avatar, and every texture beside it to Point filtering. Downloaded rigs default to Generic,
    /// which <see cref="PlayerWalkHumanoidBaker"/> rejects, and to Bilinear, which would soften the
    /// baked dot pixels.
    /// </summary>
    public sealed class PlayerWalkHumanoidModelImporter : AssetPostprocessor
    {
        public const string AuthoringRoot =
            "Assets/FamilyCompany/Editor/PlayerWalkHumanoidAuthoring/";
        public const string BaseRigAssetPath = AuthoringRoot + "PlayerHumanoidBase.fbx";

        private bool InAuthoringRoot =>
            assetPath.StartsWith(AuthoringRoot, StringComparison.Ordinal);

        private void OnPreprocessModel()
        {
            if (!InAuthoringRoot) return;
            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.importAnimation = true;
            importer.importBlendShapes = false;
            importer.importVisibility = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.optimizeGameObjects = false;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.resampleCurves = false;
            importer.weldVertices = false;

            // The base rig owns the Avatar. Every clip file copies that Avatar so one skeleton
            // definition drives all of them and Unity does not invent a second bone mapping.
            bool isBaseRig = string.Equals(assetPath, BaseRigAssetPath, StringComparison.Ordinal);
            if (isBaseRig)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                return;
            }

            var baseRig = AssetDatabase.LoadAssetAtPath<GameObject>(BaseRigAssetPath);
            if (baseRig == null)
            {
                // The base rig is not imported yet. Fall back to a self-owned Avatar so this file
                // still yields a Humanoid; re-importing later picks up the shared Avatar.
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                return;
            }
            var animator = baseRig.GetComponent<Animator>();
            Avatar avatar = animator != null ? animator.avatar : null;
            if (avatar == null || !avatar.isValid)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                return;
            }
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar = avatar;
        }

        private void OnPreprocessTexture()
        {
            if (!InAuthoringRoot) return;
            var importer = (TextureImporter)assetImporter;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
        }

        /// <summary>
        /// A downloaded FBX embeds its textures with whatever filtering the exporter chose. The
        /// bake rejects non-Point textures, so repoint them here instead of failing the bake.
        /// </summary>
        private void OnPostprocessModel(GameObject model)
        {
            if (!InAuthoringRoot || model == null) return;
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material == null || !material.HasProperty("_MainTex")) continue;
                if (material.GetTexture("_MainTex") is Texture2D texture)
                    texture.filterMode = FilterMode.Point;
            }
        }
    }
}
