using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using FamilyCompany.Presentation.Unity.OfficeRuntime;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor.OfficeGridQa
{
    /// <summary>
    /// Deterministically promotes the four approved chroma-key vending sources into the additive
    /// Resources hook owned by build mode. Chroma removal remains an explicit source-preparation
    /// step so this builder never guesses a matte from generated pixels.
    /// </summary>
    public static class OfficeBuildVendingArtBuilder
    {
        private const string SourceRoot = "Assets/Art/Office/Tiles/Furniture/Source";
        private const string RuntimeRoot =
            "Assets/FamilyCompany/Presentation.Unity/Resources/OfficeBuildFurniture";
        private const int CanvasWidth = 640;
        private const int CanvasHeight = 512;
        private const int GroundY = 28;
        private const int MaximumWidth = 300;
        private const int MaximumHeight = 430;
        private const float PixelsPerUnit = 180f;

        private sealed class DirectionSpec
        {
            public DirectionSpec(string suffix, OfficeFurnitureFacing facing, bool showsOperatingFront)
            {
                Suffix = suffix;
                Facing = facing;
                ShowsOperatingFront = showsOperatingFront;
            }

            public string Suffix { get; }
            public OfficeFurnitureFacing Facing { get; }
            public bool ShowsOperatingFront { get; }
            public string SourcePath =>
                $"{SourceRoot}/office_drink_vending_machine_{Suffix}_alpha_v1.png";
            public string RuntimePath =>
                $"{RuntimeRoot}/drink_vending_machine_{Suffix}.png";
        }

        private static readonly DirectionSpec[] Directions =
        {
            new DirectionSpec("se", OfficeFurnitureFacing.SouthEast, true),
            new DirectionSpec("sw", OfficeFurnitureFacing.SouthWest, true),
            new DirectionSpec("nw", OfficeFurnitureFacing.NorthWest, false),
            new DirectionSpec("ne", OfficeFurnitureFacing.NorthEast, false)
        };

        [MenuItem("Family Company/Art/Build Office Vending Four Directions")]
        public static void BuildAndValidate()
        {
            Directory.CreateDirectory(RuntimeRoot);
            foreach (DirectionSpec direction in Directions) BuildOne(direction);
            var firstHashes = Directions.ToDictionary(
                item => item.RuntimePath,
                item => Sha256(item.RuntimePath),
                StringComparer.Ordinal);
            foreach (DirectionSpec direction in Directions) BuildOne(direction);
            foreach (DirectionSpec direction in Directions)
            {
                if (!string.Equals(
                        firstHashes[direction.RuntimePath],
                        Sha256(direction.RuntimePath),
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Vending runtime build is not deterministic: " + direction.RuntimePath);
                }
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (DirectionSpec direction in Directions) ConfigureImporter(direction.RuntimePath);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Validate();
            AssetDatabase.SaveAssets();
            Debug.Log(
                "OFFICE_BUILD_VENDING_ART_QA: PASS | directions=4 real-rotation | " +
                "runtime=640x512 RGBA-hard-alpha | ppu=180 pivot=ground | magenta-fringe=0");
        }

        public static void RunBatch()
        {
            try
            {
                BuildAndValidate();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void Validate()
        {
            var hashes = new HashSet<string>(StringComparer.Ordinal);
            foreach (DirectionSpec direction in Directions)
            {
                if (!File.Exists(direction.SourcePath))
                    throw new FileNotFoundException("Approved vending alpha source is missing.", direction.SourcePath);
                if (!hashes.Add(Sha256(direction.RuntimePath)))
                    throw new InvalidOperationException("Two vending directions produced identical runtime pixels.");

                Texture2D runtime = ReadTexture(direction.RuntimePath);
                try
                {
                    if (runtime.width != CanvasWidth || runtime.height != CanvasHeight)
                        throw new InvalidOperationException("Vending runtime canvas is invalid: " + direction.RuntimePath);
                    Color32[] pixels = runtime.GetPixels32();
                    RectInt bounds = VisibleBounds(pixels, runtime.width, runtime.height, 0);
                    if (bounds.xMin < 24 || bounds.yMin < GroundY || bounds.yMin > GroundY + 1 ||
                        bounds.xMax >= CanvasWidth - 24 || bounds.yMax >= CanvasHeight - 24)
                    {
                        throw new InvalidOperationException(
                            $"Vending runtime safety margin/ground is invalid: {direction.RuntimePath} bounds={bounds}.");
                    }

                    var colorfulProductPixels = 0;
                    foreach (Color32 pixel in pixels)
                    {
                        if (pixel.a != 0 && pixel.a != 255)
                            throw new InvalidOperationException("Vending runtime alpha is not hard: " + direction.RuntimePath);
                        if (pixel.a > 0 && pixel.r > 180 && pixel.b > 150 && pixel.g < 90)
                            throw new InvalidOperationException("Vending contains magenta fringe: " + direction.RuntimePath);
                        if (pixel.a > 0 &&
                            Math.Max(pixel.r, Math.Max(pixel.g, pixel.b)) -
                            Math.Min(pixel.r, Math.Min(pixel.g, pixel.b)) > 105 &&
                            (pixel.r > 145 || pixel.b > 145))
                        {
                            colorfulProductPixels++;
                        }
                    }

                    if (direction.ShowsOperatingFront && colorfulProductPixels < 250)
                        throw new InvalidOperationException("Vending front view lost its product display: " + direction.RuntimePath);
                    if (!direction.ShowsOperatingFront && colorfulProductPixels > 200)
                        throw new InvalidOperationException("Vending rear view unexpectedly exposes a product display: " + direction.RuntimePath);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(runtime);
                }

                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(direction.RuntimePath);
                if (sprite == null) throw new FileNotFoundException("Vending Sprite import failed.", direction.RuntimePath);
                if (Math.Abs(sprite.pixelsPerUnit - PixelsPerUnit) > 0.01f)
                    throw new InvalidOperationException("Vending Sprite PPU is invalid: " + direction.RuntimePath);
                if (Vector2.Distance(sprite.pivot, new Vector2(CanvasWidth * 0.5f, GroundY)) > 0.01f)
                    throw new InvalidOperationException("Vending Sprite pivot is not the ground anchor: " + direction.RuntimePath);

                string resourceId = "OfficeBuildFurniture/drink_vending_machine_" + direction.Suffix;
                Sprite loaded = Resources.Load<Sprite>(resourceId);
                if (loaded == null)
                    throw new InvalidOperationException("Vending Resources hook did not load: " + resourceId);
                if (!OfficeBuildFurnitureVisualLibrary.TryResolve(
                        null,
                        OfficeFurnitureCatalog.DrinkVendingMachineDefinitionId,
                        direction.Facing,
                        out var definition,
                        out bool flipX) ||
                    definition.BaseSprite != loaded ||
                    flipX)
                {
                    throw new InvalidOperationException("Vending resolver did not select exact directional art: " + resourceId);
                }
            }
        }

        private static void BuildOne(DirectionSpec direction)
        {
            Texture2D source = ReadTexture(direction.SourcePath);
            try
            {
                Color32[] sourcePixels = source.GetPixels32();
                RectInt bounds = VisibleBounds(sourcePixels, source.width, source.height, 16);
                if (bounds.xMin < 16 || bounds.yMin < 16 ||
                    source.width - bounds.xMax < 16 || source.height - bounds.yMax < 16)
                {
                    throw new InvalidOperationException(
                        $"Vending source lacks transparent safety margin: {direction.SourcePath} bounds={bounds}.");
                }

                float scale = Mathf.Min(
                    MaximumWidth / (float)bounds.width,
                    MaximumHeight / (float)bounds.height);
                int scaledWidth = Mathf.Max(1, Mathf.RoundToInt(bounds.width * scale));
                int scaledHeight = Mathf.Max(1, Mathf.RoundToInt(bounds.height * scale));
                int destinationX = (CanvasWidth - scaledWidth) / 2;
                var output = new Color32[CanvasWidth * CanvasHeight];
                for (var y = 0; y < scaledHeight; y++)
                for (var x = 0; x < scaledWidth; x++)
                {
                    int sourceX = bounds.xMin + Mathf.Min(bounds.width - 1, Mathf.FloorToInt(x / scale));
                    int sourceY = bounds.yMin + Mathf.Min(bounds.height - 1, Mathf.FloorToInt(y / scale));
                    Color32 pixel = sourcePixels[sourceY * source.width + sourceX];
                    if (pixel.a < 128)
                        pixel = new Color32(0, 0, 0, 0);
                    else
                        pixel = new Color32(pixel.r, pixel.g, pixel.b, 255);
                    output[(GroundY + y) * CanvasWidth + destinationX + x] = pixel;
                }

                WritePng(direction.RuntimePath, output);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        private static Texture2D ReadTexture(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("PNG is missing.", path);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(path), false))
            {
                UnityEngine.Object.DestroyImmediate(texture);
                throw new InvalidOperationException("Could not decode PNG: " + path);
            }
            return texture;
        }

        private static void WritePng(string path, Color32[] pixels)
        {
            var texture = new Texture2D(CanvasWidth, CanvasHeight, TextureFormat.RGBA32, false);
            try
            {
                texture.SetPixels32(pixels);
                texture.Apply(false, false);
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static RectInt VisibleBounds(IReadOnlyList<Color32> pixels, int width, int height, byte threshold)
        {
            int minX = width;
            int minY = height;
            var maxX = -1;
            var maxY = -1;
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                if (pixels[y * width + x].a <= threshold) continue;
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
            if (maxX < minX || maxY < minY) throw new InvalidOperationException("PNG contains no visible pixels.");
            return new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        private static void ConfigureImporter(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing vending TextureImporter: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(0.5f, GroundY / (float)CanvasHeight);
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        private static string Sha256(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
        }
    }
}
