using System;
using FamilyCompany.Presentation.Unity.OfficeRuntime;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor.OfficeLayout
{
    public static class OfficeLayoutAssetBuilder
    {
        public const string AssetPath =
            "Assets/FamilyCompany/Content/Resources/OfficeLayouts/StarterOfficeV1.asset";

        [MenuItem("Family Company/Office/Rebuild Starter Office V1 Definition")]
        public static void RebuildDefault()
        {
            StarterOfficeLayoutAsset asset = AssetDatabase.LoadAssetAtPath<StarterOfficeLayoutAsset>(AssetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<StarterOfficeLayoutAsset>();
                AssetDatabase.CreateAsset(asset, AssetPath);
            }
            Undo.RecordObject(asset, "Rebuild Starter Office V1");
            asset.Capture(OfficeGridLayouts.CreateStarterOfficeV1(), "starter_office_v1", 1);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            ValidateHash(asset);
            Selection.activeObject = asset;
            Debug.Log($"STARTER_OFFICE_LAYOUT_ASSET_BUILD_PASS | hash={asset.LayoutHash} | path={AssetPath}");
        }

        public static void RunBatch()
        {
            try
            {
                RebuildDefault();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void ValidateHash(StarterOfficeLayoutAsset asset)
        {
            if (asset == null) throw new InvalidOperationException("StarterOfficeV1.asset is missing.");
            OfficeLayoutValidationReport report = OfficeLayoutSemanticValidator.Validate(asset);
            if (!report.IsValid)
                throw new InvalidOperationException(string.Join(" | ", report.Errors));
            string expected = OfficeGridLayouts.CreateStarterOfficeV1().ComputeLayoutHash();
            string actual = asset.BuildGrid().ComputeLayoutHash();
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Starter Office definition hash mismatch: expected {expected}, actual {actual}.");
        }
    }
}
