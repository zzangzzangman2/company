using System;
using FamilyCompany.Presentation.Unity.OfficeRuntime;
using FamilyCompany.Save.OfficeGrid;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor.OfficeLayout
{
    public static class OfficeLayoutValidator
    {
        [MenuItem("Family Company/QA/Validate Starter Office Semantic Layout")]
        public static void Run()
        {
            StarterOfficeLayoutAsset asset = AssetDatabase.LoadAssetAtPath<StarterOfficeLayoutAsset>(
                OfficeLayoutAssetBuilder.AssetPath);
            OfficeLayoutValidationReport report = OfficeLayoutSemanticValidator.Validate(asset);
            if (!report.IsValid)
                throw new InvalidOperationException(string.Join(" | ", report.Errors));
            var grid = asset.BuildGrid();
            var dto = OfficeGridSaveAdapter.ToDto(grid);
            var restored = OfficeGridSaveAdapter.Restore(dto);
            if (!string.Equals(grid.ComputeLayoutHash(), restored.ComputeLayoutHash(), StringComparison.Ordinal))
                throw new InvalidOperationException("Starter Office save/restore changed the semantic layout hash.");
            // A valid edited layout is intentionally allowed to differ from the code-built
            // starter template. Runtime/save parity is the invariant after authoring; strict
            // equality with CreateStarterOfficeV1 belongs only to the explicit rebuild command.
            if (!string.Equals(asset.LayoutHash, grid.ComputeLayoutHash(), StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Starter Office asset hash does not match the layout serialized in the asset.");
            Debug.Log(
                "STARTER_OFFICE_SEMANTIC_LAYOUT_VALIDATION_PASS | " +
                $"hash={grid.ComputeLayoutHash()} furniture={grid.Furniture.Count} seats={grid.SeatSlots.Count} " +
                $"warnings={report.Warnings.Count}");
        }

        public static void RunBatch()
        {
            try
            {
                Run();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }
    }
}
