using System;
using FamilyCompany.Presentation.Unity.OfficeGridView.Authoring;
using FamilyCompany.Presentation.Unity.OfficeGridView;
using FamilyCompany.Presentation.Unity.OfficeRuntime;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor.OfficeGridQa
{
    public static class OfficeFurnitureTileSnapValidation
    {
        [MenuItem("Family Company/Validate/Office Furniture Tile Snap")]
        public static void Run()
        {
            OfficeFurnitureVisualCatalog catalog = OfficeFurnitureAssetBuilder.LoadFurnitureVisualCatalog();
            catalog.Validate();
            foreach (OfficeFurnitureVisualDefinition definition in catalog.Definitions)
            {
                Vector2 centroid = Vector2.zero;
                foreach (Vector2 point in definition.GroundFootprintPolygonPx) centroid += point;
                centroid /= definition.GroundFootprintPolygonPx.Count;
                if (Vector2.Distance(centroid, definition.GroundAnchorPx) > 0.001f)
                    throw new InvalidOperationException(
                        $"Ground anchor is not the calibrated footprint center: {definition.KindId}.");
            }

            FamilyCompany.Simulation.OfficeLayout.OfficeGrid grid = OfficeGridLayouts.CreateStarterOfficeV1();
            foreach (PlacedOfficeFurniture item in grid.Furniture)
            {
                if (!item.HasCanonicalPlacementAnchor)
                    throw new InvalidOperationException("Non-canonical starter placement: " + item.FurnitureId);
            }

            StarterOfficeLayoutAsset asset = StarterOfficeLayoutAsset.LoadDefault();
            if (asset == null) throw new InvalidOperationException("StarterOfficeV1 asset is missing.");
            foreach (PlacedOfficeFurniture item in asset.BuildGrid().Furniture)
            {
                if (!item.HasCanonicalPlacementAnchor)
                    throw new InvalidOperationException("Non-canonical asset placement: " + item.FurnitureId);
            }

            Debug.Log(
                $"OFFICE_FURNITURE_TILE_SNAP_VALIDATION: PASS | " +
                $"furniture={grid.Furniture.Count} definitions={catalog.Definitions.Count} " +
                "grid=half-cell pivot=footprint-center presentationOffset=0");
        }

        public static void RunVisualBatch()
        {
            try
            {
                Run();
                OfficeTileMigrationQa.BuildPreviewScene(true, OfficeTilePreviewLayout.StarterOfficeV1);
                UnityEngine.SceneManagement.Scene scene =
                    UnityEditor.SceneManagement.EditorSceneManager.OpenScene(OfficeTileMigrationQa.PreviewScenePath);
                OfficeTileMigrationPreviewBootstrap bootstrap =
                    UnityEngine.Object.FindFirstObjectByType<OfficeTileMigrationPreviewBootstrap>();
                if (bootstrap == null)
                    throw new InvalidOperationException("Starter Office preview bootstrap is missing.");
                bootstrap.BuildPreview();
                Camera camera = Camera.main;
                if (camera == null) throw new InvalidOperationException("Starter Office preview camera is missing.");

                float maximumCornerErrorPx = 0f;
                float maximumCentroidErrorPx = 0f;
                foreach (PlacedOfficeFurniture item in bootstrap.Presenter.SemanticGrid.Furniture)
                {
                    if (!bootstrap.FurniturePresenter.TryGetRenderer(item.FurnitureId, out SpriteRenderer renderer) ||
                        !bootstrap.FurniturePresenter.TryGetDefinition(
                            item.FurnitureId, out OfficeFurnitureVisualDefinition definition))
                        throw new InvalidOperationException("Missing furniture visual: " + item.FurnitureId);
                    Vector3[] expected = bootstrap.Presenter.FootprintCornersWorld(item);
                    maximumCornerErrorPx = Mathf.Max(
                        maximumCornerErrorPx,
                        OfficeGridAlignmentMetrics.Maximum(
                            OfficeGridAlignmentMetrics.FootprintCornerErrorsPx(
                                camera, renderer, definition, expected)));
                    Vector3[] actual = bootstrap.FurniturePresenter.GroundFootprintWorld(item.FurnitureId);
                    Vector3 actualCenter = Vector3.zero;
                    Vector3 expectedCenter = Vector3.zero;
                    for (var index = 0; index < actual.Length; index++)
                    {
                        actualCenter += actual[index];
                        expectedCenter += expected[index];
                    }
                    actualCenter /= actual.Length;
                    expectedCenter /= expected.Length;
                    maximumCentroidErrorPx = Mathf.Max(
                        maximumCentroidErrorPx,
                        OfficeGridAlignmentMetrics.ScreenDistance(camera, actualCenter, expectedCenter));
                }
                if (maximumCornerErrorPx > 0.01f || maximumCentroidErrorPx > 0.01f)
                    throw new InvalidOperationException(
                        $"Furniture tile residual exceeds tolerance: corner={maximumCornerErrorPx:F4}px " +
                        $"center={maximumCentroidErrorPx:F4}px.");
                Debug.Log(
                    $"OFFICE_FURNITURE_TILE_SNAP_VISUAL: PASS | scene={scene.name} " +
                    $"furniture={bootstrap.Presenter.SemanticGrid.Furniture.Count} " +
                    $"maxCorner={maximumCornerErrorPx:F4}px maxCenter={maximumCentroidErrorPx:F4}px");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
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
