using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using FamilyCompany.Presentation.Unity;
using FamilyCompany.Presentation.Unity.OfficeGridView;
using FamilyCompany.Presentation.Unity.OfficeGridView.Authoring;
using FamilyCompany.Presentation.Unity.OfficeSeating;
using FamilyCompany.Presentation.Unity.OfficeSeating.Authoring;
using FamilyCompany.Save.OfficeGrid;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FamilyCompany.Editor.OfficeGridQa
{
    [InitializeOnLoad]
    public static class OfficeTileMigrationQa
    {
        public const string PreviewScenePath = "Assets/FamilyCompany/Scenes/OfficeTileMigrationPreview.unity";
        public const string ArtifactFolder = "Artifacts/OfficeTileMigrationQa";
        public const string CapturePath = ArtifactFolder + "/office-tile-t3-1920x1080.png";
        public const string FurnitureOverviewCapturePath = ArtifactFolder + "/office-tile-t4-furniture-1920x1080.png";
        public const string OcclusionCapturePath = ArtifactFolder + "/office-tile-t4-occlusion-1920x1080.png";
        public const string SeatedCapturePath = ArtifactFolder + "/office-tile-t5-seated-1920x1080.png";
        public const string ReportPath = ArtifactFolder + "/office-tile-migration-qa.txt";
        public const string TycoonOverviewCapturePath = ArtifactFolder + "/after-office-tile-tycoon-overview-1920x1080.png";
        public const string TycoonSeatedCapturePath = ArtifactFolder + "/after-office-tile-tycoon-seated-1920x1080.png";
        public const string TycoonAnchorsCapturePath = ArtifactFolder + "/after-office-tile-tycoon-anchors-1920x1080.png";
        public const string TycoonOcclusionCapturePath = ArtifactFolder + "/after-office-tile-tycoon-occlusion-1920x1080.png";
        public const string TycoonReportPath = ArtifactFolder + "/office-tile-tycoon-alignment-report.txt";

        private const string ActiveKey = "FamilyCompany.OfficeTileMigrationQa.Active";
        private const string StageKey = "FamilyCompany.OfficeTileMigrationQa.Stage";
        private const string StartKey = "FamilyCompany.OfficeTileMigrationQa.Start";
        private const string FailureKey = "FamilyCompany.OfficeTileMigrationQa.Failure";
        private const string ModeKey = "FamilyCompany.OfficeTileMigrationQa.Mode";
        private const string ModeT3 = "T3";
        private const string ModeT45 = "T45";
        private const float CaptureAfterSeconds = 4f;
        private const float FurnitureCaptureAfterSeconds = 0.05f;
        private const float OcclusionCaptureAfterSeconds = 0.15f;
        private const float InitialSeatedValidationAfterSeconds = 45f;
        private const float T45ValidationAfterSeconds = 60f;

        private sealed class FurnitureTransformSnapshot
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 LocalScale;
            public Transform Parent;
        }

        private static readonly Dictionary<string, FurnitureTransformSnapshot> FurnitureSnapshots =
            new Dictionary<string, FurnitureTransformSnapshot>(StringComparer.Ordinal);

        private static readonly string[] CharacterIds =
        {
            "player", "older_sister", "father", "mother"
        };

        static OfficeTileMigrationQa()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        [MenuItem("Family Company/QA/Build And Validate Office Tile T2")]
        public static void BuildAndValidateT2()
        {
            BuildPreviewScene(false);
            Debug.Log("FAMILY_COMPANY_OFFICE_TILE_T2_VALIDATION: PASS");
        }

        public static void BuildAndValidateT2Batch()
        {
            try
            {
                BuildAndValidateT2();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("Family Company/QA/Capture Office Tile T3 PlayMode")]
        public static void StartT3Batch()
        {
            try
            {
                Directory.CreateDirectory(ArtifactFolder);
                File.WriteAllText(
                    ReportPath,
                    "Office Tile Migration QA\n",
                    System.Text.Encoding.UTF8);
                BuildPreviewScene(false);
                SessionState.SetBool(ActiveKey, true);
                SessionState.SetInt(StageKey, 1);
                SessionState.SetFloat(StartKey, 0f);
                SessionState.SetString(FailureKey, string.Empty);
                SessionState.SetString(ModeKey, ModeT3);
                Append("PLAYMODE_REQUEST | stage=T3 | resolution=1920x1080 | captureAfter=4s");
                EditorSceneManager.OpenScene(PreviewScenePath, OpenSceneMode.Single);
                EditorApplication.EnterPlaymode();
            }
            catch (Exception exception)
            {
                Append("PREP_FAIL | " + exception);
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("Family Company/QA/Capture Office Tile T4-T5 PlayMode")]
        public static void StartT4T5Batch()
        {
            try
            {
                Directory.CreateDirectory(ArtifactFolder);
                File.WriteAllText(ReportPath, "Office Tile Migration QA\n", System.Text.Encoding.UTF8);
                File.WriteAllText(TycoonReportPath, "Office Tycoon Alignment V1 QA\n", System.Text.Encoding.UTF8);
                FurnitureSnapshots.Clear();
                BuildPreviewScene(true);
                SessionState.SetBool(ActiveKey, true);
                SessionState.SetInt(StageKey, 1);
                SessionState.SetFloat(StartKey, 0f);
                SessionState.SetString(FailureKey, string.Empty);
                SessionState.SetString(ModeKey, ModeT45);
                Append("PLAYMODE_REQUEST | stage=T4-T5 | resolution=1920x1080 | transformWindow=60s | initialSeated=45s | finalAfter=60s");
                EditorSceneManager.OpenScene(PreviewScenePath, OpenSceneMode.Single);
                EditorApplication.EnterPlaymode();
            }
            catch (Exception exception)
            {
                Append("PREP_FAIL | " + exception);
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        internal static void BuildPreviewScene(
            bool includeT45,
            OfficeTilePreviewLayout layout = OfficeTilePreviewLayout.MigrationPreview)
        {
            OfficeGridValidation.Run();
            OfficeTileAssetBuilder.Build();
            HighMotionCharacterArtBuilder.Validate();
            if (includeT45) OfficeFurnitureAssetBuilder.Build();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(174, 213, 216, 255);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 50f;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<PixelatedCameraEffect>().Configure(540);

            var bootstrapObject = new GameObject("OfficeTileMigrationPreviewBootstrap");
            var bootstrap = bootstrapObject.AddComponent<OfficeTileMigrationPreviewBootstrap>();
            bootstrap.ConfigureLayoutForEditor(layout);
            bootstrap.ConfigureForEditor(
                OfficeTileAssetBuilder.LoadFloorTiles(),
                LoadCharacterFrames("player"),
                LoadCharacterFrames("older_sister"),
                LoadCharacterFrames("father"),
                LoadCharacterFrames("mother"),
                true);
            if (includeT45)
            {
                bootstrap.ConfigureFurnitureAndSeatingForEditor(
                    OfficeFurnitureAssetBuilder.LoadFurnitureVisualCatalog(),
                    OfficeFurnitureAssetBuilder.LoadCharacterSeatPoseCatalog(),
                    CharacterIds.Select(LoadSeatingFrameSet).ToArray());
            }
            bootstrap.BuildPreview();
            ValidateT2(bootstrap, camera);
            if (includeT45) ValidateT4Static(bootstrap);
            OfficeGridCameraFitter.Fit(camera, bootstrap.CombinedRenderBounds, 16f / 9f);

            var generated = bootstrap.transform.Find("GeneratedOfficeTilePreview");
            if (generated != null) UnityEngine.Object.DestroyImmediate(generated.gameObject);
            var sceneFolder = Path.GetDirectoryName(PreviewScenePath);
            if (!string.IsNullOrEmpty(sceneFolder)) Directory.CreateDirectory(sceneFolder);
            EditorSceneManager.SaveScene(scene, PreviewScenePath);
            AssetDatabase.SaveAssets();
        }

        private static void ValidateT2(OfficeTileMigrationPreviewBootstrap bootstrap, Camera camera)
        {
            var presenter = bootstrap.Presenter;
            Require(presenter != null, "T2 presenter is missing.");
            Require(presenter.SemanticGrid.Width == 13 && presenter.SemanticGrid.Height == 13,
                "T2 semantic grid is not 13x13.");
            var renderedCellCount = 0;
            foreach (var position in presenter.FloorTilemap.cellBounds.allPositionsWithin)
            {
                if (presenter.FloorTilemap.HasTile(position)) renderedCellCount++;
            }
            Require(renderedCellCount == 169, $"T2 rendered {renderedCellCount} cells instead of 169.");
            Require(presenter.UnityGrid.cellLayout == GridLayout.CellLayout.Isometric,
                "T2 Unity Grid is not Isometric.");
            Require(Mathf.Abs(presenter.UnityGrid.cellSize.x - OfficeGridTilemapPresenter.TileWorldWidth) < 0.0001f,
                "T2 tile world width is invalid.");
            Require(Mathf.Abs(presenter.UnityGrid.cellSize.y - OfficeGridTilemapPresenter.TileWorldHeight) < 0.0001f,
                "T2 tile world height is invalid.");
            ValidateCornerProjection(camera, presenter, 16f / 9f, "16:9");
            ValidateCornerProjection(camera, presenter, 4f / 3f, "4:3");
            Append("T2_PASS | grid=13x13 | tile=320x160 | ppu=180 | unityGrid=Isometric | aspects=16:9,4:3");
        }

        private static void ValidateCornerProjection(
            Camera camera,
            OfficeGridTilemapPresenter presenter,
            float aspect,
            string label)
        {
            OfficeGridCameraFitter.Fit(camera, presenter.FloorRenderer.bounds, aspect);
            var corners = new[]
            {
                new OfficeGridCoordinate(0, 0),
                new OfficeGridCoordinate(presenter.SemanticGrid.Width - 1, 0),
                new OfficeGridCoordinate(0, presenter.SemanticGrid.Height - 1),
                new OfficeGridCoordinate(presenter.SemanticGrid.Width - 1, presenter.SemanticGrid.Height - 1)
            };
            foreach (var corner in corners)
            {
                var viewport = camera.WorldToViewportPoint(presenter.CellCenterWorld(corner));
                Require(viewport.z > 0f, $"{label} corner {corner} is behind the camera.");
                Require(viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f,
                    $"{label} corner {corner} is outside the viewport: {viewport}.");
            }
        }

        private static void OnEditorUpdate()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            var mode = SessionState.GetString(ModeKey, ModeT3);
            if (string.Equals(mode, ModeT45, StringComparison.Ordinal))
            {
                OnT45EditorUpdate();
                return;
            }

            var stage = SessionState.GetInt(StageKey, 0);
            if (stage == 1)
            {
                if (!EditorApplication.isPlaying) return;
                var start = SessionState.GetFloat(StartKey, 0f);
                if (start <= 0f)
                {
                    SessionState.SetFloat(StartKey, (float)EditorApplication.timeSinceStartup);
                    return;
                }

                if (EditorApplication.timeSinceStartup - start < CaptureAfterSeconds) return;
                try
                {
                    ValidateAndCaptureT3();
                    SessionState.SetString(FailureKey, string.Empty);
                }
                catch (Exception exception)
                {
                    SessionState.SetString(FailureKey, exception.ToString());
                    Append("T3_FAIL | " + exception);
                    Debug.LogException(exception);
                }

                SessionState.SetInt(StageKey, 2);
                EditorApplication.ExitPlaymode();
                return;
            }

            if (stage != 2 || EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
            var failure = SessionState.GetString(FailureKey, string.Empty);
            SessionState.SetBool(ActiveKey, false);
            SessionState.EraseInt(StageKey);
            SessionState.EraseFloat(StartKey);
            SessionState.EraseString(ModeKey);
            if (failure.Length == 0)
            {
                Debug.Log("FAMILY_COMPANY_OFFICE_TILE_T3_VALIDATION: PASS");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError(failure);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        private static void OnT45EditorUpdate()
        {
            var stage = SessionState.GetInt(StageKey, 0);
            if (stage >= 1 && stage <= 4)
            {
                if (!EditorApplication.isPlaying) return;
                var start = SessionState.GetFloat(StartKey, 0f);
                if (start <= 0f)
                {
                    SessionState.SetFloat(StartKey, (float)EditorApplication.timeSinceStartup);
                    return;
                }
                var elapsed = EditorApplication.timeSinceStartup - start;
                try
                {
                    if (stage == 1 && elapsed >= FurnitureCaptureAfterSeconds)
                    {
                        CaptureT45(FurnitureOverviewCapturePath);
                        CaptureT45(TycoonOverviewCapturePath);
                        SnapshotFurnitureTransforms();
                        Append("T4_OVERVIEW_CAPTURE | path=" + FurnitureOverviewCapturePath + " | tycoonPath=" + TycoonOverviewCapturePath);
                        SessionState.SetInt(StageKey, 2);
                    }
                    else if (stage == 2 && elapsed >= OcclusionCaptureAfterSeconds)
                    {
                        ValidateAndCaptureOcclusion();
                        SessionState.SetInt(StageKey, 3);
                    }
                    else if (stage == 3 && elapsed >= InitialSeatedValidationAfterSeconds)
                    {
                        ValidateInitialTycoonAlignmentAndRequestReseat();
                        SessionState.SetInt(StageKey, 4);
                    }
                    else if (stage == 4 && elapsed >= T45ValidationAfterSeconds)
                    {
                        ValidateAndCaptureT45();
                        SessionState.SetString(FailureKey, string.Empty);
                        SessionState.SetInt(StageKey, 5);
                        EditorApplication.ExitPlaymode();
                    }
                }
                catch (Exception exception)
                {
                    SessionState.SetString(FailureKey, exception.ToString());
                    Append("T4_T5_FAIL | " + exception);
                    Debug.LogException(exception);
                    SessionState.SetInt(StageKey, 5);
                    EditorApplication.ExitPlaymode();
                }
                return;
            }

            if (stage != 5 || EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
            var failure = SessionState.GetString(FailureKey, string.Empty);
            SessionState.SetBool(ActiveKey, false);
            SessionState.EraseInt(StageKey);
            SessionState.EraseFloat(StartKey);
            SessionState.EraseString(ModeKey);
            if (failure.Length == 0)
            {
                Debug.Log("FAMILY_COMPANY_OFFICE_TILE_T4_T5_VALIDATION: PASS");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError(failure);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        private static void ValidateAndCaptureT3()
        {
            var bootstrap = UnityEngine.Object.FindFirstObjectByType<OfficeTileMigrationPreviewBootstrap>();
            var camera = Camera.main;
            Require(bootstrap != null && bootstrap.Presenter != null, "T3 preview bootstrap is missing.");
            Require(camera != null, "T3 camera is missing.");
            Require(bootstrap.Movers.Count == 4, $"T3 expected four family movers, found {bootstrap.Movers.Count}.");
            OfficeGridCameraFitter.Fit(camera, bootstrap.Presenter.FloorRenderer.bounds, 16f / 9f);

            foreach (var mover in bootstrap.Movers)
            {
                Require(mover.DistanceTravelled > 0.5f,
                    $"{mover.name} did not travel far enough: {mover.DistanceTravelled:F3}.");
                Require(!mover.CanEnter(new OfficeGridCoordinate(6, 6)),
                    $"{mover.name} can enter blocked cell (6,6).");
                var boundsRatio = mover.RenderedBoundsHeightRatio(camera);
                Require(boundsRatio >= 0.14f && boundsRatio <= 0.18f,
                    $"{mover.name} rendered bounds ratio is {boundsRatio:F4}.");
                var visibleRatio = ResolveVisibleAlphaHeightRatio(mover.TargetRenderer.sprite, mover.VisualRoot.lossyScale.y, camera);
                Require(visibleRatio >= 0.14f && visibleRatio <= 0.18f,
                    $"{mover.name} visible alpha ratio is {visibleRatio:F4}.");
                var semanticScale = mover.transform.lossyScale;
                var scale = mover.VisualRoot.lossyScale;
                Require(Vector3.Distance(semanticScale, Vector3.one) < 0.0001f,
                    $"{mover.name} semantic root scale is not one: {semanticScale}.");
                Require(Mathf.Abs(scale.x - scale.y) < 0.0001f && Mathf.Abs(scale.y - scale.z) < 0.0001f,
                    $"{mover.name} VisualRoot accumulated scale is non-uniform: {scale}.");
                Require(mover.Animator.IsMoving, $"{mover.name} animator is not moving.");
                Append($"CHARACTER_PASS | id={mover.name} | distance={mover.DistanceTravelled:F3} | boundsRatio={boundsRatio:F4} | visibleRatio={visibleRatio:F4} | scale={scale.x:F3}");
            }

            var ordered = bootstrap.Movers.OrderBy(item => item.transform.position.y).ToArray();
            Require(ordered[0].TargetRenderer.sortingOrder > ordered[ordered.Length - 1].TargetRenderer.sortingOrder,
                "Dynamic (x+y) sorting order does not place lower characters in front.");
            ValidateCornerProjection(camera, bootstrap.Presenter, 16f / 9f, "T3 16:9");
            ValidateCornerProjection(camera, bootstrap.Presenter, 4f / 3f, "T3 4:3");
            OfficeGridCameraFitter.Fit(camera, bootstrap.Presenter.FloorRenderer.bounds, 16f / 9f);
            Capture(camera, CapturePath, 1920, 1080);
            Append("T3_PASS | family=4 | movement=realUpdate | blockedCell=reject | sorting=x+y | capture=" + CapturePath);
        }

        private static void ValidateT4Static(OfficeTileMigrationPreviewBootstrap bootstrap)
        {
            var grid = bootstrap.Presenter.SemanticGrid;
            bool starter = bootstrap.Layout == OfficeTilePreviewLayout.StarterOfficeV1;
            int expectedFurnitureCount = starter ? 17 : 18;
            int expectedKindCount = starter ? 11 : 12;
            Require(grid.Furniture.Count >= 8, "T4 furniture count is below eight.");
            Require(grid.Furniture.Select(item => item.KindId).Distinct(StringComparer.Ordinal).Count() >= 4,
                "T4 furniture kind count is below four.");
            Require(grid.Furniture.Count == expectedFurnitureCount,
                $"T4 expected {expectedFurnitureCount} placed furniture objects for {bootstrap.Layout}, found {grid.Furniture.Count}.");
            Require(grid.Furniture.Select(item => item.KindId).Distinct(StringComparer.Ordinal).Count() == expectedKindCount,
                $"T4 expected {expectedKindCount} furniture kinds for {bootstrap.Layout}.");
            Require(bootstrap.FurniturePresenter != null &&
                    bootstrap.FurniturePresenter.Renderers.Count == grid.Furniture.Count,
                "T4 furniture renderers do not match semantic furniture.");
            Require(bootstrap.FurniturePresenter.VisualCatalog != null &&
                    bootstrap.FurniturePresenter.VisualCatalog.Definitions.Count == 12,
                "T4 visual catalog must contain 12 definitions.");
            bootstrap.FurniturePresenter.VisualCatalog.Validate();
            foreach (var item in grid.Furniture)
            {
                Require(bootstrap.FurniturePresenter.TryGetDefinition(item.FurnitureId, out var definition),
                    "Missing visual definition for " + item.FurnitureId);
                Require(definition.Facing == item.Facing && definition.UniformScale > 0f,
                    "Furniture visual facing/scale is invalid: " + item.FurnitureId);
                Require(bootstrap.FurniturePresenter.TryGetSemanticRoot(item.FurnitureId, out var root),
                    "Furniture semantic root is missing: " + item.FurnitureId);
                Require(root.localScale == Vector3.one, "Furniture semantic root scale is not one: " + item.FurnitureId);
                Require(root.GetComponent<Rigidbody>() == null && root.GetComponent<CharacterController>() == null,
                    "Furniture semantic root has a forbidden movement component: " + item.FurnitureId);
                Require(bootstrap.FurniturePresenter.TryGetRenderer(item.FurnitureId, out var renderer),
                    "Furniture base renderer is missing: " + item.FurnitureId);
                var visualScale = renderer.transform.localScale;
                Require(Mathf.Abs(visualScale.x - visualScale.y) <= 0.001f &&
                        Mathf.Abs(visualScale.y - visualScale.z) <= 0.001f,
                    "Furniture VisualRoot scale is non-uniform: " + item.FurnitureId);
            }
            Require(grid.SeatSlots.Count == 4, "T5 preview requires four seats.");

            var deskSeatCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            var chairSeatCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var seat in grid.SeatSlots)
            {
                var chair = FindFurniture(grid, seat.FurnitureId);
                Require(chair.KindId == OfficeGridLayouts.SwivelChairKind,
                    $"Seat {seat.SeatId} does not reference a swivel chair.");
                Require(!chair.BlocksMovement, $"Chair {chair.FurnitureId} blocks movement.");
                Require(chair.Origin.Equals(seat.Cell), $"Chair {chair.FurnitureId} does not share seat cell.");
                Require(grid.IsWalkable(seat.Cell), $"Seat {seat.SeatId} is not walkable.");

                Require(seat.HasWorkstationBinding, $"Seat {seat.SeatId} has no explicit workstation binding.");
                Require(grid.IsWalkable(seat.ApproachCell), $"Seat {seat.SeatId} approach is not walkable.");
                Require(Math.Abs(seat.ApproachCell.X - seat.Cell.X) + Math.Abs(seat.ApproachCell.Y - seat.Cell.Y) == 1,
                    $"Seat {seat.SeatId} approach is not cardinally adjacent.");
                var deskId = seat.WorkSurfaceFurnitureId;
                var desk = FindFurniture(grid, deskId);
                Require(desk.KindId == OfficeGridLayouts.DeskWithPcKind && desk.BlocksMovement,
                    $"Seat {seat.SeatId} has no blocking workstation.");
                var deskCell = NearestFootprintCell(desk, seat.Cell, out var distance);
                Require(distance == 1, $"Seat {seat.SeatId} is not cardinally adjacent to its desk.");
                var expectedFacing = FacingFromDelta(deskCell.X - seat.Cell.X, deskCell.Y - seat.Cell.Y);
                Require(seat.Facing == expectedFacing,
                    $"Seat {seat.SeatId} faces {seat.Facing}, expected {expectedFacing} toward desk.");
                Require(chair.Facing == seat.Facing,
                    $"Chair {chair.FurnitureId} facing does not match seat facing.");
                deskSeatCounts[deskId] = deskSeatCounts.TryGetValue(deskId, out var deskCount) ? deskCount + 1 : 1;
                chairSeatCounts[chair.FurnitureId] =
                    chairSeatCounts.TryGetValue(chair.FurnitureId, out var chairCount) ? chairCount + 1 : 1;
            }
            Require(deskSeatCounts.Count == 4 && deskSeatCounts.Values.All(count => count >= 1),
                "Every family desk must have at least one seat.");
            Require(chairSeatCounts.Count == 4 && chairSeatCounts.Values.All(count => count == 1),
                "Every seat must have exactly one chair.");
            Append($"T4_STATIC_PASS | layout={bootstrap.Layout} | furniture={expectedFurnitureCount} | kinds={expectedKindCount} | desks=4 | chairs=4 | seats=4 | seatInvariants=pass");
        }

        private static void ValidateAndCaptureOcclusion()
        {
            var bootstrap = UnityEngine.Object.FindFirstObjectByType<OfficeTileMigrationPreviewBootstrap>();
            var camera = Camera.main;
            Require(bootstrap != null && camera != null, "T4 occlusion preview is missing.");
            var player = bootstrap.Movers.Single(item => item.name.EndsWith("player", StringComparison.Ordinal));
            Require(bootstrap.FurniturePresenter.TryGetRenderer("desk_father", out var deskRenderer),
                "T4 father desk renderer is missing.");
            Require(player.TargetRenderer.sortingOrder < deskRenderer.sortingOrder,
                $"Character behind desk is not sorted behind it: character={player.TargetRenderer.sortingOrder}, desk={deskRenderer.sortingOrder}.");
            Require(player.TargetRenderer.bounds.Intersects(deskRenderer.bounds),
                "Character-behind-desk capture does not contain a visual overlap.");
            CaptureT45(OcclusionCapturePath);
            Append($"T4_OCCLUSION_PASS | character={player.TargetRenderer.sortingOrder} | desk={deskRenderer.sortingOrder} | overlap=true | path={OcclusionCapturePath}");
        }

        private static void SnapshotFurnitureTransforms()
        {
            var bootstrap = UnityEngine.Object.FindFirstObjectByType<OfficeTileMigrationPreviewBootstrap>();
            Require(bootstrap != null && bootstrap.FurniturePresenter != null, "Furniture snapshot preview is missing.");
            FurnitureSnapshots.Clear();
            foreach (var item in bootstrap.Presenter.SemanticGrid.Furniture)
            {
                Require(bootstrap.FurniturePresenter.TryGetSemanticRoot(item.FurnitureId, out var root),
                    "Furniture semantic root is missing: " + item.FurnitureId);
                FurnitureSnapshots.Add(item.FurnitureId, new FurnitureTransformSnapshot
                {
                    Position = root.position,
                    Rotation = root.rotation,
                    LocalScale = root.localScale,
                    Parent = root.parent
                });
            }
            AppendTycoon($"TRANSFORM_SNAPSHOT | count={FurnitureSnapshots.Count} | time=0s");
        }

        private static void ValidateInitialTycoonAlignmentAndRequestReseat()
        {
            var bootstrap = UnityEngine.Object.FindFirstObjectByType<OfficeTileMigrationPreviewBootstrap>();
            var camera = Camera.main;
            Require(bootstrap != null && camera != null, "Initial tycoon alignment preview is missing.");
            Require(bootstrap.SeatedWorkers.Count == 4 && bootstrap.SeatedWorkers.All(item => item.IsWorking),
                "Initial tycoon alignment requires four working family members.");
            ValidateTycoonAlignmentMetrics(bootstrap, camera, "INITIAL_45S");
            CaptureT45(SeatedCapturePath);
            CaptureT45(TycoonSeatedCapturePath);
            CaptureT45(TycoonOcclusionCapturePath);
            Require(bootstrap.AlignmentDebugOverlay != null, "Alignment debug overlay is missing.");
            bootstrap.AlignmentDebugOverlay.SetOverlayEnabled(true);
            bootstrap.AlignmentDebugOverlay.RefreshImmediate();
            CaptureT45(TycoonAnchorsCapturePath);
            bootstrap.AlignmentDebugOverlay.SetOverlayEnabled(false);
            foreach (var worker in bootstrap.SeatedWorkers) worker.RequestStandAndReseat();
            AppendTycoon(
                $"CAPTURE_PASS | seated={TycoonSeatedCapturePath} | anchors={TycoonAnchorsCapturePath} | occlusion={TycoonOcclusionCapturePath}");
            Append("TYCOON_ALIGNMENT_INITIAL_PASS | time=45s | standAndReseat=4");
        }

        private static void ValidateTycoonAlignmentMetrics(
            OfficeTileMigrationPreviewBootstrap bootstrap,
            Camera camera,
            string phase)
        {
            var previousTarget = camera.targetTexture;
            var metricsTarget = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32);
            try
            {
                camera.targetTexture = metricsTarget;
                var grid = bootstrap.Presenter.SemanticGrid;
                foreach (var item in grid.Furniture)
                {
                    Require(bootstrap.FurniturePresenter.TryGetRenderer(item.FurnitureId, out var renderer),
                        "Furniture renderer is missing: " + item.FurnitureId);
                    Require(bootstrap.FurniturePresenter.TryGetDefinition(item.FurnitureId, out var definition),
                        "Furniture definition is missing: " + item.FurnitureId);
                    float footprintError = OfficeGridAlignmentMetrics.Maximum(
                        OfficeGridAlignmentMetrics.FootprintCornerErrorsPx(
                            camera,
                            renderer,
                            definition,
                            bootstrap.Presenter.FootprintCornersWorld(item)));
                    Require(footprintError <= 2f,
                        $"{item.FurnitureId} footprint corner error is {footprintError:F3}px.");
                    AppendTycoon($"FOOTPRINT | phase={phase} | furniture={item.FurnitureId} | maxErrorPx={footprintError:F3}");
                }

                foreach (var worker in bootstrap.SeatedWorkers)
                {
                    var seat = FindSeat(grid, worker.SeatId);
                    Require(bootstrap.FurniturePresenter.TryGetDefinition(seat.WorkSurfaceFurnitureId, out var deskDefinition),
                        "Desk definition is missing: " + seat.WorkSurfaceFurnitureId);

                    float pelvisError = worker.PelvisSeatScreenError(camera);
                    float chairDeskError = worker.ChairDeskSeatScreenError(camera);
                    float interactionError = worker.HandWorkScreenError(camera);
                    float operatorSemanticError = OfficeGridAlignmentMetrics.ScreenDistance(
                        camera,
                        bootstrap.FurniturePresenter.OperatorSeatSocketWorld(seat.WorkSurfaceFurnitureId),
                        bootstrap.Presenter.SubcellAnchorWorld(seat.OperatorAnchor));
                    Vector2 characterVector = worker.PoseProfile.RenderedHandFromPelvisPx(
                        OfficeGridCharacterMover.UniformVisualScale);
                    Vector2 deskVector =
                        (deskDefinition.OperatorWorkSocketPx - deskDefinition.OperatorSeatSocketPx) *
                        deskDefinition.UniformScale;
                    float vectorAngle = OfficeGridAlignmentMetrics.VectorAngleDifferenceDegrees(characterVector, deskVector);
                    float vectorLength = OfficeGridAlignmentMetrics.VectorLengthRelativeError(characterVector, deskVector);
                    Require(pelvisError <= 2f,
                        $"{worker.MemberId} pelvis-to-seat error is {pelvisError:F3}px.");
                    Require(chairDeskError <= 2f,
                        $"{worker.MemberId} chair-to-desk seat error is {chairDeskError:F3}px.");
                    Require(interactionError <= 4f,
                        $"{worker.MemberId} hand-to-work-surface error is {interactionError:F3}px.");
                    Require(vectorAngle <= 2f,
                        $"{worker.MemberId} pelvis-to-hand vector direction differs by {vectorAngle:F3} degrees.");
                    Require(vectorLength <= 0.04f,
                        $"{worker.MemberId} pelvis-to-hand vector length differs by {vectorLength * 100f:F3}%.");
                    Require(worker.FootError() <= 0.001f,
                        $"{worker.MemberId} semantic root-to-seat error is {worker.FootError():F6} world units.");
                    AppendTycoon(
                        $"SEAT | phase={phase} | member={worker.MemberId} | pelvisSeatPx={pelvisError:F3} | chairDeskPx={chairDeskError:F3} | " +
                        $"operatorSemanticPx={operatorSemanticError:F3} | interactionPx={interactionError:F3} | " +
                        $"vectorDegrees={vectorAngle:F3} | vectorLengthPercent={vectorLength * 100f:F3} | rootWorld={worker.FootError():F6}");
                }
            }
            finally
            {
                camera.targetTexture = previousTarget;
                UnityEngine.Object.DestroyImmediate(metricsTarget);
            }
        }

        private static void ValidateFurnitureTransformsUnchanged()
        {
            var bootstrap = UnityEngine.Object.FindFirstObjectByType<OfficeTileMigrationPreviewBootstrap>();
            Require(bootstrap != null && FurnitureSnapshots.Count == bootstrap.Presenter.SemanticGrid.Furniture.Count,
                "Furniture transform snapshot count changed.");
            foreach (var item in bootstrap.Presenter.SemanticGrid.Furniture)
            {
                Require(FurnitureSnapshots.TryGetValue(item.FurnitureId, out var snapshot),
                    "Furniture snapshot is missing: " + item.FurnitureId);
                Require(bootstrap.FurniturePresenter.TryGetSemanticRoot(item.FurnitureId, out var root),
                    "Furniture semantic root is missing: " + item.FurnitureId);
                Require(root.position == snapshot.Position, item.FurnitureId + " position changed during 60 seconds.");
                Require(root.rotation == snapshot.Rotation, item.FurnitureId + " rotation changed during 60 seconds.");
                Require(root.localScale == snapshot.LocalScale, item.FurnitureId + " scale changed during 60 seconds.");
                Require(ReferenceEquals(root.parent, snapshot.Parent), item.FurnitureId + " parent changed during 60 seconds.");
            }
            AppendTycoon("TRANSFORM_PASS | window=60s | position=exact | rotation=exact | scale=exact | parent=exact");
        }

        private static void ValidateAndCaptureT45()
        {
            var bootstrap = UnityEngine.Object.FindFirstObjectByType<OfficeTileMigrationPreviewBootstrap>();
            var camera = Camera.main;
            Require(bootstrap != null && bootstrap.Presenter != null, "T4-T5 preview bootstrap is missing.");
            Require(camera != null, "T4-T5 camera is missing.");
            ValidateT4Static(bootstrap);
            ValidateFurnitureTransformsUnchanged();
            Require(bootstrap.CollisionMonitor != null && bootstrap.CollisionMonitor.SampleCount >= 120,
                "T4 collision monitor did not collect enough per-frame samples.");
            Require(bootstrap.CollisionMonitor.BlockedCellViolationCount == 0,
                "T4 character entered blocked furniture cell: " + bootstrap.CollisionMonitor.FirstViolation);
            Require(bootstrap.SeatedWorkers.Count == 4, "T5 expected four seated workers.");
            foreach (var worker in bootstrap.SeatedWorkers)
            {
                Append($"SEATING_SNAPSHOT | id={worker.MemberId} | seat={worker.SeatId} | phase={worker.Phase} | error={worker.FootError():F4}");
            }
            Require(bootstrap.SeatedWorkers.All(item => item.IsWorking),
                "T5 not every family member reached seated work.");
            Require(bootstrap.SeatedWorkers.All(item => item.HasActiveClaim && item.IsSeatOccupied),
                "T5 not every working family member owns an occupied runtime seat claim.");
            Require(bootstrap.SeatedWorkers.Select(item => item.SeatId).Distinct(StringComparer.Ordinal).Count() == 4,
                "T5 family seat assignment contains duplicates.");

            foreach (var worker in bootstrap.SeatedWorkers)
            {
                Require(worker.FootError() <= 0.001f,
                    $"{worker.MemberId} seated foot error is {worker.FootError():F4}.");
                Require(worker.VisualResetError <= 0.001f,
                    $"{worker.MemberId} VisualRoot reset error is {worker.VisualResetError:F6}.");
                Require(worker.DirectionIndex == DirectionIndex(worker.Facing),
                    $"{worker.MemberId} seated facing is incorrect.");
                var mover = worker.GetComponent<OfficeGridCharacterMover>();
                Require(bootstrap.FurniturePresenter.SeatOcclusionMatches(
                        FindSeat(bootstrap.Presenter.SemanticGrid, worker.SeatId),
                        mover.TargetRenderer.sortingOrder),
                    $"{worker.MemberId} chair occlusion order is incorrect.");
                Append($"SEATED_PASS | id={worker.MemberId} | seat={worker.SeatId} | error={worker.FootError():F4} | facing={worker.Facing} | phase=Working");
            }

            ValidateTycoonAlignmentMetrics(bootstrap, camera, "FINAL_60S");

            var grid = bootstrap.Presenter.SemanticGrid;
            foreach (var desk in grid.Furniture.Where(item => item.KindId == OfficeGridLayouts.DeskWithPcKind))
            {
                foreach (var mover in bootstrap.Movers)
                    Require(!mover.CanEnter(desk.Origin), $"{mover.name} can enter desk footprint {desk.Origin}.");
                Require(bootstrap.FurniturePresenter.TryGetRenderer(desk.FurnitureId, out var deskRenderer),
                    "Desk renderer is missing: " + desk.FurnitureId);
                var behindCell = new OfficeGridCoordinate(desk.Origin.X, Math.Min(grid.Height - 1, desk.Origin.Y + 1));
                var behindOrder = OfficeGridCharacterMover.ResolveDynamicSortingOrder(
                    bootstrap.Presenter.CellCenterWorld(behindCell));
                Require(behindOrder < deskRenderer.sortingOrder,
                    $"Grid x+y sorting does not place upper row behind {desk.FurnitureId}.");
            }

            var restored = OfficeGridSaveAdapter.Restore(OfficeGridSaveAdapter.ToDto(grid));
            Require(grid.ComputeLayoutHash() == restored.ComputeLayoutHash(),
                "T4-T5 furniture/seat save roundtrip hash changed.");
            var playerMover = bootstrap.Movers.Single(item => item.name.EndsWith("player", StringComparison.Ordinal));
            Require(playerMover.DistanceTravelled > 35f,
                $"T4 30-second player movement distance is too short: {playerMover.DistanceTravelled:F3}.");
            CaptureT45(SeatedCapturePath);
            CaptureT45(TycoonSeatedCapturePath);
            AppendTycoon(
                $"RUNTIME_PASS | window=60s | familyWorking=4 | occupiedClaims=4 | collisions=0 | unsupportedFallbacks=0 | visualResetMax={bootstrap.SeatedWorkers.Max(item => item.VisualResetError):F6}");
            Append($"T4_T5_PASS | collisionSamples={bootstrap.CollisionMonitor.SampleCount} | violations=0 | playerDistance={playerMover.DistanceTravelled:F3} | familySeated=4 | uniqueSeats=4 | saveHash={grid.ComputeLayoutHash()} | capture={SeatedCapturePath}");
        }

        private static void CaptureT45(string path)
        {
            var bootstrap = UnityEngine.Object.FindFirstObjectByType<OfficeTileMigrationPreviewBootstrap>();
            var camera = Camera.main;
            Require(bootstrap != null && camera != null, "T4-T5 capture target is missing.");
            OfficeGridCameraFitter.Fit(camera, bootstrap.CombinedRenderBounds, 16f / 9f);
            Capture(camera, path, 1920, 1080);
        }

        private static PlacedOfficeFurniture FindFurniture(OfficeGrid grid, string furnitureId)
        {
            return grid.Furniture.Single(item => string.Equals(item.FurnitureId, furnitureId, StringComparison.Ordinal));
        }

        private static OfficeSeatSlot FindSeat(OfficeGrid grid, string seatId)
        {
            return grid.SeatSlots.Single(item => string.Equals(item.SeatId, seatId, StringComparison.Ordinal));
        }

        private static OfficeGridCoordinate NearestFootprintCell(
            PlacedOfficeFurniture furniture,
            OfficeGridCoordinate origin,
            out int distance)
        {
            var best = furniture.Origin;
            distance = int.MaxValue;
            for (var y = furniture.Origin.Y; y < furniture.Origin.Y + furniture.Height; y++)
            for (var x = furniture.Origin.X; x < furniture.Origin.X + furniture.Width; x++)
            {
                var candidateDistance = Math.Abs(x - origin.X) + Math.Abs(y - origin.Y);
                if (candidateDistance >= distance) continue;
                distance = candidateDistance;
                best = new OfficeGridCoordinate(x, y);
            }
            return best;
        }

        private static OfficeFurnitureFacing FacingFromDelta(int deltaX, int deltaY)
        {
            if (deltaX == 1 && deltaY == 0) return OfficeFurnitureFacing.NorthEast;
            if (deltaX == -1 && deltaY == 0) return OfficeFurnitureFacing.SouthWest;
            if (deltaX == 0 && deltaY == 1) return OfficeFurnitureFacing.NorthWest;
            if (deltaX == 0 && deltaY == -1) return OfficeFurnitureFacing.SouthEast;
            throw new InvalidOperationException($"Seat-to-desk delta is not cardinal: ({deltaX},{deltaY}).");
        }

        private static int DirectionIndex(OfficeFurnitureFacing facing)
        {
            return facing switch
            {
                OfficeFurnitureFacing.SouthEast => (int)OfficeSeatFacing8.Southeast,
                OfficeFurnitureFacing.SouthWest => (int)OfficeSeatFacing8.Southwest,
                OfficeFurnitureFacing.NorthWest => (int)OfficeSeatFacing8.Northwest,
                OfficeFurnitureFacing.NorthEast => (int)OfficeSeatFacing8.Northeast,
                _ => throw new ArgumentOutOfRangeException(nameof(facing))
            };
        }

        private static float ResolveVisibleAlphaHeightRatio(Sprite sprite, float scale, Camera camera)
        {
            if (sprite == null) throw new ArgumentNullException(nameof(sprite));
            var path = AssetDatabase.GetAssetPath(sprite);
            var bytes = File.ReadAllBytes(path);
            var raw = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!raw.LoadImage(bytes, false)) throw new InvalidDataException("Failed to read sprite pixels: " + path);
                var pixels = raw.GetPixels32();
                var minimumY = raw.height;
                var maximumY = -1;
                for (var y = 0; y < raw.height; y++)
                for (var x = 0; x < raw.width; x++)
                {
                    if (pixels[y * raw.width + x].a == 0) continue;
                    minimumY = Math.Min(minimumY, y);
                    maximumY = Math.Max(maximumY, y);
                }
                if (maximumY < minimumY) throw new InvalidDataException("Sprite has no visible pixels: " + path);
                var visibleWorldHeight = (maximumY - minimumY + 1) / sprite.pixelsPerUnit * scale;
                return visibleWorldHeight / (camera.orthographicSize * 2f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(raw);
            }
        }

        internal static void Capture(Camera camera, string path, int width, int height)
        {
            var absolute = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute));
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                filterMode = FilterMode.Point
            };
            var output = new Texture2D(width, height, TextureFormat.RGB24, false);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                RenderTexture.active = target;
                camera.Render();
                output.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                output.Apply(false, false);
                File.WriteAllBytes(absolute, output.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(output);
            }
        }

        private static Sprite[] LoadCharacterFrames(string characterId)
        {
            var folder = HighMotionCharacterArtBuilder.GetFrameFolder(characterId);
            return HighMotionCharacterArtBuilder.GetFrameNames(characterId)
                .Select(name =>
                {
                    var path = folder + "/" + name + ".png";
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    if (sprite == null) throw new FileNotFoundException("High-motion frame is missing.", path);
                    return sprite;
                })
                .ToArray();
        }

        private static OfficeGridSeatingFrameSet LoadSeatingFrameSet(string memberId)
        {
            return new OfficeGridSeatingFrameSet
            {
                memberId = memberId,
                sitDownFrames = LoadSeatingClip(memberId, OfficeSeatingAnimationClip.SitDown),
                workFrames = LoadSeatingClip(memberId, OfficeSeatingAnimationClip.Work),
                standUpFrames = LoadSeatingClip(memberId, OfficeSeatingAnimationClip.StandUp)
            };
        }

        private static Sprite[] LoadSeatingClip(string memberId, OfficeSeatingAnimationClip clip)
        {
            var frames = new List<Sprite>();
            for (var frame = 0; frame < OfficeSeatingAnimationFrames.FrameCount(clip); frame++)
            for (var direction = 0; direction < OfficeSeatingAnimationFrames.DirectionCount; direction++)
            {
                var path = OfficeSeatingAnimationFrames.AssetPath(
                    memberId,
                    (OfficeSeatFacing8)direction,
                    clip,
                    frame);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null) throw new FileNotFoundException("Office seating frame is missing.", path);
                frames.Add(sprite);
            }
            return frames.ToArray();
        }

        private static void Append(string line)
        {
            Directory.CreateDirectory(ArtifactFolder);
            File.AppendAllText(
                ReportPath,
                DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + " | " + line + Environment.NewLine,
                System.Text.Encoding.UTF8);
        }

        private static void AppendTycoon(string line)
        {
            Directory.CreateDirectory(ArtifactFolder);
            File.AppendAllText(
                TycoonReportPath,
                DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + " | " + line + Environment.NewLine,
                System.Text.Encoding.UTF8);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
