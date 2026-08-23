using System;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Experimental.Family3D.Editor
{
    public sealed class Family3DPrototypeModelImporter : AssetPostprocessor
    {
        public const string PrototypeRoot = "Assets/FamilyCompany/Experimental/Family3DPrototype/";
        public const string ModelPath = PrototypeRoot + "ThirdParty/StylooChibi/allinone.fbx";

        private void OnPreprocessModel()
        {
            if (!string.Equals(assetPath, ModelPath, StringComparison.Ordinal))
                return;

            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.importBlendShapes = false;
            importer.importVisibility = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.optimizeGameObjects = false;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.resampleCurves = false;
        }

        private void OnPostprocessModel(GameObject model)
        {
            if (!string.Equals(assetPath, ModelPath, StringComparison.Ordinal) || model == null)
                return;
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
                renderer.receiveShadows = true;
        }
    }
}
