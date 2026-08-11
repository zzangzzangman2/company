using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FamilyCompany.Presentation.Unity.OfficeGridView;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace FamilyCompany.Editor.OfficeGridQa
{
    public static class OfficeTileAssetBuilder
    {
        public const string SourcePath = "Assets/Art/Office/Tiles/Source/office_floor_tiles_wood_alpha_v1.png";
        public const string OutputFolder = "Assets/Art/Office/Tiles/Floor";

        private static readonly string[] OutputPaths =
        {
            OutputFolder + "/office_floor_wood_a_v1.png",
            OutputFolder + "/office_floor_wood_b_v1.png",
            OutputFolder + "/office_floor_wood_c_v1.png"
        };

        private static readonly string[] TileAssetPaths =
        {
            OutputFolder + "/office_floor_wood_a_v1.asset",
            OutputFolder + "/office_floor_wood_b_v1.asset",
            OutputFolder + "/office_floor_wood_c_v1.asset"
        };

        [MenuItem("Family Company/Build Office Tile Assets T2")]
        public static void Build()
        {
            Directory.CreateDirectory(OutputFolder);
            AssetDatabase.ImportAsset(SourcePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(SourcePath) as TextureImporter;
            if (importer == null) throw new FileNotFoundException("Office tile source importer is missing.", SourcePath);
            if (!importer.isReadable)
            {
                importer.isReadable = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            var source = AssetDatabase.LoadAssetAtPath<Texture2D>(SourcePath);
            if (source == null) throw new FileNotFoundException("Office tile source texture is missing.", SourcePath);
            var components = FindOpaqueComponents(source)
                .OrderByDescending(item => item.PixelCount)
                .Take(OutputPaths.Length)
                .OrderBy(item => item.MinimumX)
                .ToArray();
            if (components.Length != OutputPaths.Length)
                throw new InvalidOperationException($"Expected three office floor components, found {components.Length}.");

            var pixels = source.GetPixels32();
            for (var index = 0; index < components.Length; index++)
            {
                var tile = ResampleComponent(source.width, source.height, pixels, components[index]);
                File.WriteAllBytes(OutputPaths[index], tile.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tile);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var path in OutputPaths) ConfigureOutputImporter(path);
            for (var index = 0; index < OutputPaths.Length; index++)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(OutputPaths[index]);
                var tile = AssetDatabase.LoadAssetAtPath<Tile>(TileAssetPaths[index]);
                if (tile == null)
                {
                    tile = ScriptableObject.CreateInstance<Tile>();
                    AssetDatabase.CreateAsset(tile, TileAssetPaths[index]);
                }
                tile.name = Path.GetFileNameWithoutExtension(TileAssetPaths[index]);
                tile.sprite = sprite;
                tile.color = Color.white;
                tile.colliderType = Tile.ColliderType.None;
                EditorUtility.SetDirty(tile);
            }
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("FAMILY_COMPANY_OFFICE_TILE_ASSET_BUILD: PASS");
        }

        public static Sprite[] LoadFloorSprites()
        {
            var result = new Sprite[OutputPaths.Length];
            for (var index = 0; index < OutputPaths.Length; index++)
            {
                result[index] = AssetDatabase.LoadAssetAtPath<Sprite>(OutputPaths[index]);
                if (result[index] == null)
                    throw new FileNotFoundException("Office floor sprite is missing.", OutputPaths[index]);
            }

            return result;
        }

        public static TileBase[] LoadFloorTiles()
        {
            var result = new TileBase[TileAssetPaths.Length];
            for (var index = 0; index < TileAssetPaths.Length; index++)
            {
                result[index] = AssetDatabase.LoadAssetAtPath<TileBase>(TileAssetPaths[index]);
                if (result[index] == null)
                    throw new FileNotFoundException("Office floor tile asset is missing.", TileAssetPaths[index]);
            }

            return result;
        }

        public static void Validate()
        {
            for (var index = 0; index < OutputPaths.Length; index++)
            {
                var path = OutputPaths[index];
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (texture == null || sprite == null || importer == null)
                    throw new InvalidOperationException("Office floor tile import is incomplete: " + path);
                Require(texture.width == OfficeGridTilemapPresenter.TilePixelWidth, "tile width", path);
                Require(texture.height == OfficeGridTilemapPresenter.TilePixelHeight, "tile height", path);
                Require(importer.filterMode == FilterMode.Point, "Point filter", path);
                Require(!importer.mipmapEnabled, "mipmap disabled", path);
                Require(importer.textureCompression == TextureImporterCompression.Uncompressed, "uncompressed texture", path);
                Require(Mathf.Abs(importer.spritePixelsPerUnit - OfficeGridTilemapPresenter.PixelsPerUnit) < 0.01f,
                    "180 PPU", path);
                Require(Mathf.Abs(sprite.rect.width - OfficeGridTilemapPresenter.TilePixelWidth) < 0.01f,
                    "sprite width", path);
                Require(Mathf.Abs(sprite.rect.height - OfficeGridTilemapPresenter.TilePixelHeight) < 0.01f,
                    "sprite height", path);
                Require(AssetDatabase.LoadAssetAtPath<TileBase>(TileAssetPaths[index]) != null,
                    "Tile asset", TileAssetPaths[index]);
            }
        }

        private static void ConfigureOutputImporter(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Output importer is missing: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = OfficeGridTilemapPresenter.PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
        }

        private static List<OpaqueComponent> FindOpaqueComponents(Texture2D source)
        {
            var pixels = source.GetPixels32();
            var visited = new bool[pixels.Length];
            var queue = new int[pixels.Length];
            var result = new List<OpaqueComponent>();
            for (var start = 0; start < pixels.Length; start++)
            {
                if (visited[start] || pixels[start].a == 0) continue;
                var read = 0;
                var write = 0;
                visited[start] = true;
                queue[write++] = start;
                var component = new OpaqueComponent(source.width, source.height);
                while (read < write)
                {
                    var current = queue[read++];
                    var x = current % source.width;
                    var y = current / source.width;
                    component.Include(x, y);
                    Visit(x - 1, y);
                    Visit(x + 1, y);
                    Visit(x, y - 1);
                    Visit(x, y + 1);

                    void Visit(int neighborX, int neighborY)
                    {
                        if (neighborX < 0 || neighborX >= source.width || neighborY < 0 || neighborY >= source.height)
                            return;
                        var neighbor = neighborY * source.width + neighborX;
                        if (visited[neighbor] || pixels[neighbor].a == 0) return;
                        visited[neighbor] = true;
                        queue[write++] = neighbor;
                    }
                }

                if (component.PixelCount >= 128) result.Add(component);
            }

            return result;
        }

        private static Texture2D ResampleComponent(
            int sourceWidth,
            int sourceHeight,
            Color32[] source,
            OpaqueComponent component)
        {
            var outputWidth = OfficeGridTilemapPresenter.TilePixelWidth;
            var outputHeight = OfficeGridTilemapPresenter.TilePixelHeight;
            var target = new Texture2D(outputWidth, outputHeight, TextureFormat.RGBA32, false, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            var output = new Color32[outputWidth * outputHeight];
            for (var y = 0; y < outputHeight; y++)
            for (var x = 0; x < outputWidth; x++)
            {
                var sourceX = component.MinimumX +
                              Mathf.RoundToInt(x * (component.Width - 1f) / (outputWidth - 1f));
                var sourceY = component.MinimumY +
                              Mathf.RoundToInt(y * (component.Height - 1f) / (outputHeight - 1f));
                sourceX = Mathf.Clamp(sourceX, 0, sourceWidth - 1);
                sourceY = Mathf.Clamp(sourceY, 0, sourceHeight - 1);
                var pixel = source[sourceY * sourceWidth + sourceX];
                var magentaFringe = pixel.r > pixel.g + 60 && pixel.b > pixel.g + 45;
                if (pixel.a < 128 || magentaFringe)
                    pixel = new Color32(0, 0, 0, 0);
                else
                    pixel.a = 255;
                output[y * outputWidth + x] = pixel;
            }

            target.SetPixels32(output);
            target.Apply(false, false);
            return target;
        }

        private static void Require(bool condition, string requirement, string path)
        {
            if (!condition) throw new InvalidOperationException($"{path}: failed {requirement}.");
        }

        private sealed class OpaqueComponent
        {
            public OpaqueComponent(int width, int height)
            {
                MinimumX = width;
                MinimumY = height;
                MaximumX = -1;
                MaximumY = -1;
            }

            public int MinimumX { get; private set; }
            public int MinimumY { get; private set; }
            public int MaximumX { get; private set; }
            public int MaximumY { get; private set; }
            public int PixelCount { get; private set; }
            public int Width => MaximumX - MinimumX + 1;
            public int Height => MaximumY - MinimumY + 1;

            public void Include(int x, int y)
            {
                MinimumX = Math.Min(MinimumX, x);
                MinimumY = Math.Min(MinimumY, y);
                MaximumX = Math.Max(MaximumX, x);
                MaximumY = Math.Max(MaximumY, y);
                PixelCount++;
            }
        }
    }
}
