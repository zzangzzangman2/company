using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using FamilyCompany.Experimental.Family3D;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FamilyCompany.Experimental.Family3D.Editor
{
    /// <summary>
    /// Bakes the user-approved V31 procedural workstation into deterministic directional sprites.
    /// Production remains a 2D isometric game, so baking preserves character/furniture sorting
    /// while removing every dependency on the retired gold desk and green chair art.
    /// </summary>
    public static class Family3DWorkstationSpriteAuthoring
    {
        private const int Width = 640;
        private const int Height = 512;
        private const int GroundAnchorY = 64;
        private const float PixelsPerUnit = 180f;
        private const float RenderPixelsPerWorldUnit = 209.52f;
        // A 30-degree true-isometric camera projects this orthogonal world-space tile to the
        // production 160x80 diamond. Furniture can therefore stay rectangular without losing
        // exact tile-axis alignment. Character-relative vertical dimensions are compensated for
        // the camera-up change from sqrt(1/2) to sqrt(3/4).
        private const float CharacterHeight = 1.53951067f;
        private const float TileWorldUnit = 1.07996454f;
        private const string OutputDirectory =
            "Assets/FamilyCompany/Content/Resources/OfficeBuildFurniture";
        private const string ManifestPath =
            OutputDirectory + "/v31_workstation_sprite_manifest.txt";

        private static readonly string[] DeskSuffixes = { "se", "sw", "nw", "ne" };
        // A desk faces the opposite direction from its chair. These are written by chair facing,
        // which is the key used by OfficeBuildFurnitureVisualLibrary at runtime.
        private static readonly string[] ChairSuffixes = { "nw", "ne", "se", "sw" };

        [MenuItem("Family Company/Art/Bake V31 Workstation Directional Sprites")]
        public static void BakeMenu() => Bake();

        public static void BakeBatch()
        {
            try
            {
                Bake();
                Debug.Log("V31_WORKSTATION_SPRITE_BAKE: PASS");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("V31_WORKSTATION_SPRITE_BAKE: FAIL | " + exception.Message);
                EditorApplication.Exit(1);
            }
        }

        private static void Bake()
        {
            Directory.CreateDirectory(OutputDirectory);
            var manifest = new StringBuilder();
            manifest.AppendLine("FC-V31-WORKSTATION-DIRECTIONAL-SPRITES-V1");
            manifest.AppendLine("canvas=640x512");
            manifest.AppendLine("pixelsPerUnit=180");
            manifest.AppendLine("groundAnchorPx=320,64");
            manifest.AppendLine("meshAxes=orthogonal-90deg");
            manifest.AppendLine("projectedTileBasisPx=160,80|-160,80");
            manifest.AppendLine(
                "source=Family3DWorkstationQa V31 orthogonal true-isometric atomic geometry");

            for (var turns = 0; turns < 4; turns++)
                BakeDirection(turns, manifest);

            File.WriteAllText(ManifestPath, manifest.ToString(), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(ManifestPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static void BakeDirection(int turns, StringBuilder manifest)
        {
            const int layer = 30;
            GameObject root = null;
            GameObject cameraObject = null;
            GameObject lightObject = null;
            RenderTexture renderTexture = null;
            Texture2D capture = null;
            try
            {
                // Keep the physical desk axes orthogonal. The true-isometric bake camera, rather
                // than a skewed mesh, turns these X/Z axes into the production 160x80 diamond.
                // Quarter-turning this basis now preserves every 90-degree furniture corner and
                // still follows OfficeLayoutEditRules.RotateCellClockwise exactly.
                Vector3 canonicalRight = Vector3.right * TileWorldUnit;
                Vector3 canonicalForward = Vector3.forward * TileWorldUnit;
                Vector3 gridRight;
                Vector3 gridForward;
                switch (turns)
                {
                    case 1:
                        gridRight = -canonicalForward;
                        gridForward = canonicalRight;
                        break;
                    case 2:
                        gridRight = -canonicalRight;
                        gridForward = -canonicalForward;
                        break;
                    case 3:
                        gridRight = canonicalForward;
                        gridForward = -canonicalRight;
                        break;
                    default:
                        gridRight = canonicalRight;
                        gridForward = canonicalForward;
                        break;
                }
                Vector3 seatCellAnchor = Vector3.zero;
                // The seat cell is the visible chair cell. A 2x1 desk sits behind it with the
                // chair under the desk's right-hand side, so the desk footprint centre is half a
                // cell left plus one cell behind the chair. This is the same integer placement
                // used by OfficeLayoutEditRules.CreateWorkstationPlacement.
                Vector3 deskAnchor = -gridRight * 0.5f + gridForward;

                // Grid coefficients measured from the accepted V31 runtime receipt. Recompose
                // them in the orthogonal directional basis so CRT, keyboard, chair and operator
                // centreline rotate together without shearing the desk mesh.
                var keyboardGrid = new Vector2(0.48898797f, -0.26360459f);
                Vector3 keyboardWorld = deskAnchor +
                                        gridRight * keyboardGrid.x +
                                        gridForward * keyboardGrid.y;

                root = new GameObject("V31_WorkstationSpriteBake_" + turns);
                Family3DWorkstationQa workstation = Family3DWorkstationQa.Create(
                    root.transform,
                    layer,
                    "direction_" + turns,
                    seatCellAnchor,
                    gridRight,
                    gridForward,
                    deskAnchor,
                    TileWorldUnit * 2f,
                    TileWorldUnit,
                    keyboardWorld,
                    CharacterHeight,
                    0f,
                    0f);
                if (workstation.GridAxisOrthogonalityErrorDegrees > 0.001f)
                    throw new InvalidOperationException(
                        "Workstation bake attempted to shear a rectangular furniture mesh: " +
                        workstation.GridAxisOrthogonalityErrorDegrees.ToString("F6") + " degrees.");

                cameraObject = new GameObject("V31_WorkstationSpriteCamera");
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.orthographic = true;
                camera.orthographicSize = Height / (2f * RenderPixelsPerWorldUnit);
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 50f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                camera.cullingMask = 1 << layer;
                camera.allowHDR = false;
                camera.allowMSAA = false;

                lightObject = new GameObject("V31_WorkstationSpriteLight");
                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(1f, 0.91f, 0.78f, 1f);
                light.intensity = 1.18f;
                light.shadows = LightShadows.None;
                light.cullingMask = 1 << layer;
                lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

                renderTexture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
                {
                    name = "V31_WorkstationSpriteBake",
                    antiAliasing = 1,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                renderTexture.Create();
                camera.targetTexture = renderTexture;
                capture = new Texture2D(Width, Height, TextureFormat.RGBA32, false, false);

                PositionCamera(camera, deskAnchor);
                Vector2 projectedOrigin = WorldToPixel(camera, deskAnchor);
                Vector2 projectedRight = WorldToPixel(camera, deskAnchor + gridRight) - projectedOrigin;
                Vector2 projectedForward = WorldToPixel(camera, deskAnchor + gridForward) - projectedOrigin;
                Vector2 expectedRight;
                Vector2 expectedForward;
                switch (turns)
                {
                    case 1:
                        expectedRight = new Vector2(160f, -80f);
                        expectedForward = new Vector2(160f, 80f);
                        break;
                    case 2:
                        expectedRight = new Vector2(-160f, -80f);
                        expectedForward = new Vector2(160f, -80f);
                        break;
                    case 3:
                        expectedRight = new Vector2(-160f, 80f);
                        expectedForward = new Vector2(-160f, -80f);
                        break;
                    default:
                        expectedRight = new Vector2(160f, 80f);
                        expectedForward = new Vector2(-160f, 80f);
                        break;
                }
                if (Vector2.Distance(projectedRight, expectedRight) > 0.01f ||
                    Vector2.Distance(projectedForward, expectedForward) > 0.01f)
                    throw new InvalidOperationException(
                        "Orthogonal furniture no longer projects to the exact production tile axes.");

                Transform chair = workstation.transform.Find("Chair_SwivelPivot");
                if (chair == null)
                    throw new InvalidOperationException("V31 chair root was not created.");

                chair.gameObject.SetActive(false);
                PositionCamera(camera, deskAnchor);
                string deskPath = OutputDirectory + "/desk_with_pc_" + DeskSuffixes[turns] + ".png";
                RenderPng(camera, renderTexture, capture, deskPath);
                ConfigureSpriteImporter(deskPath);

                SetDirectChildrenActive(workstation.transform, false);
                chair.gameObject.SetActive(true);
                // The source QA keeps its approved continuous V31 chair composition, but the
                // production Sprite is a standalone one-cell prop. Centre the bake on its actual
                // swivel-foot contact so (320,64) is a real ground point, not the old empty pivot.
                PositionCamera(camera, workstation.ChairGroundWorld);
                string chairPath = OutputDirectory + "/swivel_chair_" + ChairSuffixes[turns] + ".png";
                RenderPng(camera, renderTexture, capture, chairPath);
                ConfigureSpriteImporter(chairPath);

                Vector3 chairCushion = workstation.ChairGroundWorld;
                chairCushion.y = workstation.CushionWorldY;
                Vector2 chairSeat = WorldToPixel(camera, chairCushion);
                // Work-surface data is measured in the desk-anchored camera, so restore it before
                // recording the metadata used by the runtime directional resolver. The operator
                // socket is the snapped semantic chair cell, not the source QA's continuous chair
                // offset, so chair collision, green preview and seated actor all share one tile.
                Vector3 semanticChairCushion = seatCellAnchor;
                semanticChairCushion.y = workstation.CushionWorldY;
                PositionCamera(camera, deskAnchor);
                Vector2 deskSeatFromDesk = WorldToPixel(camera, semanticChairCushion);
                Vector2 workSurface = WorldToPixel(camera, workstation.WorkSurfaceWorld);
                manifest.AppendLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "desk_{0}: operatorSeat={1:F3},{2:F3}; workSurface={3:F3},{4:F3}",
                    DeskSuffixes[turns],
                    deskSeatFromDesk.x,
                    deskSeatFromDesk.y,
                    workSurface.x,
                    workSurface.y));
                manifest.AppendLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "chair_{0}: seat={1:F3},{2:F3}; ground={3},{4}",
                    ChairSuffixes[turns],
                    chairSeat.x,
                    chairSeat.y,
                    Width / 2,
                    GroundAnchorY));
            }
            finally
            {
                RenderTexture.active = null;
                if (renderTexture != null)
                {
                    renderTexture.Release();
                    UnityEngine.Object.DestroyImmediate(renderTexture);
                }
                if (capture != null) UnityEngine.Object.DestroyImmediate(capture);
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
                if (lightObject != null) UnityEngine.Object.DestroyImmediate(lightObject);
            }
        }

        private static void PositionCamera(Camera camera, Vector3 groundAnchor)
        {
            Quaternion rotation = Quaternion.LookRotation(
                new Vector3(0.61237244f, -0.5f, 0.61237244f),
                Vector3.up);
            camera.transform.rotation = rotation;
            float verticalShift = (Height * 0.5f - GroundAnchorY) /
                                  RenderPixelsPerWorldUnit;
            camera.transform.position = groundAnchor - camera.transform.forward * 12f +
                                        camera.transform.up * verticalShift;
        }

        private static void RenderPng(
            Camera camera,
            RenderTexture renderTexture,
            Texture2D capture,
            string assetPath)
        {
            RenderTexture previous = RenderTexture.active;
            camera.Render();
            RenderTexture.active = renderTexture;
            capture.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0, false);
            capture.Apply(false, false);
            File.WriteAllBytes(assetPath, capture.EncodeToPNG());
            RenderTexture.active = previous;
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        }

        private static void ConfigureSpriteImporter(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException("Generated sprite did not import: " + assetPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.spritePivot = new Vector2(0.5f, 0.5f);
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static void SetDirectChildrenActive(Transform root, bool active)
        {
            var children = new List<GameObject>();
            foreach (Transform child in root) children.Add(child.gameObject);
            for (var index = 0; index < children.Count; index++)
                children[index].SetActive(active);
        }

        private static Vector2 WorldToPixel(Camera camera, Vector3 world)
        {
            Vector3 viewport = camera.WorldToViewportPoint(world);
            return new Vector2(viewport.x * Width, viewport.y * Height);
        }

    }
}
