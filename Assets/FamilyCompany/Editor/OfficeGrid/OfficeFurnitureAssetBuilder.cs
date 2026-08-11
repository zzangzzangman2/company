using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor.OfficeGridQa
{
    public static class OfficeFurnitureAssetBuilder
    {
        public const string SourceFolder = "Assets/Art/Office/Tiles/Furniture/Source";
        public const string RuntimeFolder = "Assets/Art/Office/Tiles/Furniture/Runtime";
        public const int CanvasWidth = 640;
        public const int CanvasHeight = 512;
        public const int BaselinePixels = 24;
        public const float PixelsPerUnit = 180f;
        public const string ChairBackrestPath = RuntimeFolder + "/office_swivel_chair_backrest_v2.png";
        public const int ChairBackrestCutoffY = 145;

        private sealed class FurnitureSpec
        {
            public FurnitureSpec(string kindId, string stem, int width, int height)
            {
                KindId = kindId;
                Stem = stem;
                Width = width;
                Height = height;
            }

            public string KindId { get; }
            public string Stem { get; }
            public int Width { get; }
            public int Height { get; }
            public string SourcePath => $"{SourceFolder}/{Stem}_alpha_v2.png";
            public string RuntimePath => $"{RuntimeFolder}/{Stem}_v2.png";
        }

        private static readonly FurnitureSpec[] Specs =
        {
            new FurnitureSpec(OfficeGridLayouts.DeskWithPcKind, "office_workstation", 500, 360),
            new FurnitureSpec(OfficeGridLayouts.SwivelChairKind, "office_swivel_chair", 190, 280),
            new FurnitureSpec(OfficeGridLayouts.ReceptionCounterKind, "office_reception_counter", 500, 340),
            new FurnitureSpec(OfficeGridLayouts.MeetingTableKind, "office_meeting_table", 460, 300),
            new FurnitureSpec(OfficeGridLayouts.DocumentBookcaseKind, "office_document_bookcase", 300, 360),
            new FurnitureSpec(OfficeGridLayouts.FaxCopierKind, "office_fax_copier", 280, 370),
            new FurnitureSpec(OfficeGridLayouts.WaterDispenserKind, "office_water_dispenser", 190, 360),
            new FurnitureSpec(OfficeGridLayouts.SofaKind, "office_sofa", 450, 330),
            new FurnitureSpec(OfficeGridLayouts.CoffeeTableKind, "office_coffee_table", 380, 220),
            new FurnitureSpec(OfficeGridLayouts.PottedPlantKind, "office_potted_plant", 230, 330),
            new FurnitureSpec(OfficeGridLayouts.PartitionKind, "office_partition", 430, 360),
            new FurnitureSpec(OfficeGridLayouts.FilingCabinetKind, "office_filing_cabinet", 200, 370)
        };

        public static IReadOnlyList<string> KindIds => Specs.Select(item => item.KindId).ToArray();

        [MenuItem("Family Company/Art/Build Office Furniture V2")]
        public static void Build()
        {
            Directory.CreateDirectory(RuntimeFolder);
            foreach (var spec in Specs) BuildOne(spec);
            BuildChairBackrest();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var spec in Specs) ConfigureImporter(spec.RuntimePath);
            ConfigureImporter(ChairBackrestPath);
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("FAMILY_COMPANY_OFFICE_FURNITURE_V2_BUILD: PASS");
        }

        public static Sprite[] LoadFurnitureSprites()
        {
            return Specs.Select(spec =>
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spec.RuntimePath);
                if (sprite == null) throw new FileNotFoundException("Office furniture sprite is missing.", spec.RuntimePath);
                return sprite;
            }).ToArray();
        }

        public static Sprite LoadChairBackrestSprite()
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ChairBackrestPath);
            if (sprite == null) throw new FileNotFoundException("Office chair backrest sprite is missing.", ChairBackrestPath);
            return sprite;
        }

        public static void Validate()
        {
            var seenKinds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var spec in Specs)
            {
                if (!seenKinds.Add(spec.KindId))
                    throw new InvalidOperationException("Duplicate furniture kind: " + spec.KindId);
                var source = ReadTexture(spec.SourcePath);
                try
                {
                    var sourceBounds = VisibleBounds(source.GetPixels32(), source.width, source.height, 16);
                    if (sourceBounds.xMin < 24 || sourceBounds.yMin < 24 ||
                        source.width - sourceBounds.xMax < 24 || source.height - sourceBounds.yMax < 24)
                    {
                        throw new InvalidOperationException($"Furniture source touches its safety margin: {spec.SourcePath} bounds={sourceBounds}.");
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(source);
                }

                var runtime = ReadTexture(spec.RuntimePath);
                try
                {
                    if (runtime.width != CanvasWidth || runtime.height != CanvasHeight)
                        throw new InvalidOperationException($"Furniture canvas is invalid: {spec.RuntimePath}.");
                    var pixels = runtime.GetPixels32();
                    var bounds = VisibleBounds(pixels, runtime.width, runtime.height, 0);
                    if (bounds.xMin <= 0 || bounds.yMin < BaselinePixels ||
                        bounds.yMin > BaselinePixels + 1 ||
                        bounds.xMax >= CanvasWidth || bounds.yMax >= CanvasHeight)
                    {
                        throw new InvalidOperationException($"Furniture runtime bounds are invalid: {spec.RuntimePath} bounds={bounds}.");
                    }
                    for (var index = 0; index < pixels.Length; index++)
                    {
                        var pixel = pixels[index];
                        if (pixel.a != 0 && pixel.a != 255)
                            throw new InvalidOperationException($"Furniture alpha is not hard at {index}: {spec.RuntimePath}.");
                        if (pixel.a > 0 && pixel.r > 180 && pixel.b > 150 && pixel.g < 90)
                            throw new InvalidOperationException($"Furniture contains magenta fringe at {index}: {spec.RuntimePath}.");
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(runtime);
                }

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spec.RuntimePath);
                if (sprite == null || Math.Abs(sprite.pixelsPerUnit - PixelsPerUnit) > 0.01f)
                    throw new InvalidOperationException("Furniture sprite import is invalid: " + spec.RuntimePath);
            }

            if (seenKinds.Count != 12) throw new InvalidOperationException("Furniture V2 must contain exactly 12 kinds.");
            var backrest = ReadTexture(ChairBackrestPath);
            try
            {
                var bounds = VisibleBounds(backrest.GetPixels32(), backrest.width, backrest.height, 0);
                if (bounds.yMin < ChairBackrestCutoffY || bounds.yMax >= CanvasHeight)
                    throw new InvalidOperationException("Chair backrest overlay contains lower chair/base pixels.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(backrest);
            }
        }

        private static void BuildOne(FurnitureSpec spec)
        {
            var source = ReadTexture(spec.SourcePath);
            try
            {
                var sourcePixels = source.GetPixels32();
                var bounds = VisibleBounds(sourcePixels, source.width, source.height, 16);
                var output = new Color32[CanvasWidth * CanvasHeight];
                var destinationX = (CanvasWidth - spec.Width) / 2;
                var destinationY = BaselinePixels;
                for (var y = 0; y < spec.Height; y++)
                for (var x = 0; x < spec.Width; x++)
                {
                    var sourceX = bounds.xMin + Math.Min(bounds.width - 1, x * bounds.width / spec.Width);
                    var sourceY = bounds.yMin + Math.Min(bounds.height - 1, y * bounds.height / spec.Height);
                    var pixel = sourcePixels[sourceY * source.width + sourceX];
                    if (pixel.a < 128) pixel = new Color32(0, 0, 0, 0);
                    else pixel.a = 255;
                    output[(destinationY + y) * CanvasWidth + destinationX + x] = pixel;
                }

                var runtime = new Texture2D(CanvasWidth, CanvasHeight, TextureFormat.RGBA32, false, false);
                try
                {
                    runtime.SetPixels32(output);
                    runtime.Apply(false, false);
                    File.WriteAllBytes(spec.RuntimePath, runtime.EncodeToPNG());
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(runtime);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        private static void BuildChairBackrest()
        {
            var chairPath = Specs.Single(item => item.KindId == OfficeGridLayouts.SwivelChairKind).RuntimePath;
            var chair = ReadTexture(chairPath);
            try
            {
                var pixels = chair.GetPixels32();
                for (var y = 0; y < ChairBackrestCutoffY; y++)
                for (var x = 0; x < chair.width; x++)
                    pixels[y * chair.width + x] = new Color32(0, 0, 0, 0);
                chair.SetPixels32(pixels);
                chair.Apply(false, false);
                File.WriteAllBytes(ChairBackrestPath, chair.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(chair);
            }
        }

        private static Texture2D ReadTexture(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("Office furniture source is missing.", path);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            if (!texture.LoadImage(File.ReadAllBytes(path), false))
            {
                UnityEngine.Object.DestroyImmediate(texture);
                throw new InvalidDataException("Could not decode office furniture texture: " + path);
            }
            return texture;
        }

        private static RectInt VisibleBounds(Color32[] pixels, int width, int height, byte alphaThreshold)
        {
            var minimumX = width;
            var minimumY = height;
            var maximumX = -1;
            var maximumY = -1;
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                if (pixels[y * width + x].a <= alphaThreshold) continue;
                minimumX = Math.Min(minimumX, x);
                minimumY = Math.Min(minimumY, y);
                maximumX = Math.Max(maximumX, x);
                maximumY = Math.Max(maximumY, y);
            }
            if (maximumX < minimumX || maximumY < minimumY)
                throw new InvalidDataException("Furniture image contains no visible pixels.");
            return new RectInt(minimumX, minimumY, maximumX - minimumX + 1, maximumY - minimumY + 1);
        }

        private static void ConfigureImporter(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Furniture texture importer is missing: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(0.5f, BaselinePixels / (float)CanvasHeight);
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
    }
}
