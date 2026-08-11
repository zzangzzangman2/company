using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using FamilyCompany.Presentation.Unity;
using FamilyCompany.Presentation.Unity.OfficeGridView;
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
        public const string ReportPath = ArtifactFolder + "/office-tile-migration-qa.txt";

        private const string ActiveKey = "FamilyCompany.OfficeTileMigrationQa.Active";
        private const string StageKey = "FamilyCompany.OfficeTileMigrationQa.Stage";
        private const string StartKey = "FamilyCompany.OfficeTileMigrationQa.Start";
        private const string FailureKey = "FamilyCompany.OfficeTileMigrationQa.Failure";
        private const float CaptureAfterSeconds = 4f;

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
            BuildPreviewScene();
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
                BuildPreviewScene();
                SessionState.SetBool(ActiveKey, true);
                SessionState.SetInt(StageKey, 1);
                SessionState.SetFloat(StartKey, 0f);
                SessionState.SetString(FailureKey, string.Empty);
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

        private static void BuildPreviewScene()
        {
            OfficeGridValidation.Run();
            OfficeTileAssetBuilder.Build();
            HighMotionCharacterArtBuilder.Validate();

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
            bootstrap.ConfigureForEditor(
                OfficeTileAssetBuilder.LoadFloorTiles(),
                LoadCharacterFrames("player"),
                LoadCharacterFrames("older_sister"),
                LoadCharacterFrames("father"),
                LoadCharacterFrames("mother"),
                true);
            bootstrap.BuildPreview();
            ValidateT2(bootstrap, camera);
            OfficeGridCameraFitter.Fit(camera, bootstrap.Presenter.FloorRenderer.bounds, 16f / 9f);

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
                var visibleRatio = ResolveVisibleAlphaHeightRatio(mover.TargetRenderer.sprite, mover.transform.lossyScale.y, camera);
                Require(visibleRatio >= 0.14f && visibleRatio <= 0.18f,
                    $"{mover.name} visible alpha ratio is {visibleRatio:F4}.");
                var scale = mover.transform.lossyScale;
                Require(Mathf.Abs(scale.x - scale.y) < 0.0001f && Mathf.Abs(scale.y - scale.z) < 0.0001f,
                    $"{mover.name} accumulated scale is non-uniform: {scale}.");
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

        private static void Capture(Camera camera, string path, int width, int height)
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

        private static void Append(string line)
        {
            Directory.CreateDirectory(ArtifactFolder);
            File.AppendAllText(
                ReportPath,
                DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + " | " + line + Environment.NewLine,
                System.Text.Encoding.UTF8);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
