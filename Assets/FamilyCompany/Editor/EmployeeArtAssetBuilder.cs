using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public static class EmployeeArtAssetBuilder
    {
        private const string EmployeeRoot = "Assets/Art/Characters/Employees";
        private const string CharacterRoot = "Assets/Art/Characters";
        private const float PixelPixelsPerUnit = 180f;

        private static readonly EmployeeSpec[] Employees =
        {
            new("kim_seoa", "KimSeoa"),
            new("lee_jian", "LeeJian"),
            new("choi_iseo", "ChoiIseo"),
            new("jung_arin", "JungArin"),
            new("park_haeun", "ParkHaeun"),
            new("han_sua", "HanSua"),
            new("oh_jiwoo", "OhJiwoo"),
            new("yoon_chaea", "YoonChaea")
        };

        private static readonly ParentSpec[] Parents =
        {
            new("father", "Father", "father_office_neutral_v1.png"),
            new("mother", "Mother", "mother_office_neutral_v1.png")
        };

        private static readonly string[] DirectionSuffixes =
        {
            "south_a", "west_a", "north_a", "east_a",
            "south_b", "west_b", "north_b", "east_b"
        };

        [MenuItem("Family Company/Build Employee Heroine Art Assets")]
        public static void Build()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            foreach (var employee in Employees)
            {
                ConfigurePortraitFolder(employee);
                ConfigureReferenceFolder(employee);
                ConfigurePixelSheet(employee.Id, employee.PixelSheetPath, employee.FrameFolder);
            }

            foreach (var parent in Parents)
            {
                ConfigureFullScreenSprite(parent.PortraitPath);
                ConfigurePixelSheet(parent.Id, parent.PixelSheetPath, parent.FrameFolder);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Validate();
            Debug.Log("Character art build complete: 8 employees, 2 parents, 74 full-screen portraits, 80 pixel frames.");
        }

        [MenuItem("Family Company/Validate Employee Heroine Art Assets")]
        public static void Validate()
        {
            var portraitCount = 0;
            var frameCount = 0;

            foreach (var employee in Employees)
            {
                var portraits = AssetDatabase.FindAssets("t:Sprite", new[] { employee.PortraitFolder });
                if (portraits.Length != 9)
                {
                    throw new InvalidDataException(
                        $"Expected 9 portraits for {employee.Id}, found {portraits.Length} in {employee.PortraitFolder}.");
                }

                var sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(employee.PixelSheetPath);
                if (sheet == null || sheet.width != 1536 || sheet.height != 1024)
                {
                    var dimensions = sheet == null ? "missing" : $"{sheet.width}x{sheet.height}";
                    throw new InvalidDataException(
                        $"Expected 1536x1024 pixel sheet for {employee.Id}, found {dimensions}.");
                }

                var frames = AssetDatabase.FindAssets("t:Sprite", new[] { employee.FrameFolder });
                if (frames.Length != DirectionSuffixes.Length)
                {
                    throw new InvalidDataException(
                        $"Expected 8 pixel frames for {employee.Id}, found {frames.Length} in {employee.FrameFolder}.");
                }

                portraitCount += portraits.Length;
                frameCount += frames.Length;
            }

            foreach (var parent in Parents)
            {
                var portrait = AssetDatabase.LoadAssetAtPath<Sprite>(parent.PortraitPath);
                if (portrait == null)
                {
                    throw new InvalidDataException($"Parent portrait is missing or not imported as a Sprite: {parent.PortraitPath}.");
                }

                var sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(parent.PixelSheetPath);
                if (sheet == null || sheet.width != 1536 || sheet.height != 1024)
                {
                    var dimensions = sheet == null ? "missing" : $"{sheet.width}x{sheet.height}";
                    throw new InvalidDataException(
                        $"Expected 1536x1024 pixel sheet for {parent.Id}, found {dimensions}.");
                }

                var frames = AssetDatabase.FindAssets("t:Sprite", new[] { parent.FrameFolder });
                if (frames.Length != DirectionSuffixes.Length)
                {
                    throw new InvalidDataException(
                        $"Expected 8 pixel frames for {parent.Id}, found {frames.Length} in {parent.FrameFolder}.");
                }

                portraitCount++;
                frameCount += frames.Length;
            }

            if (portraitCount != 74 || frameCount != 80)
            {
                throw new InvalidDataException(
                    $"Character asset totals are invalid. FullScreenPortraits={portraitCount}, frames={frameCount}.");
            }

            Debug.Log($"Character art validation passed: fullScreenPortraits={portraitCount}, frames={frameCount}.");
        }

        private static void ConfigurePortraitFolder(EmployeeSpec employee)
        {
            var portraitGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { employee.PortraitFolder });
            if (portraitGuids.Length != 9)
            {
                throw new InvalidDataException(
                    $"Expected 9 portrait textures for {employee.Id}, found {portraitGuids.Length}.");
            }

            foreach (var guid in portraitGuids)
            {
                ConfigureFullScreenSprite(AssetDatabase.GUIDToAssetPath(guid));
            }
        }

        private static void ConfigureReferenceFolder(EmployeeSpec employee)
        {
            var referenceGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { employee.ReferenceFolder });
            if (referenceGuids.Length == 0)
            {
                throw new InvalidDataException($"No identity references found for {employee.Id}.");
            }

            foreach (var guid in referenceGuids)
            {
                ConfigureFullScreenSprite(AssetDatabase.GUIDToAssetPath(guid));
            }
        }

        private static void ConfigureFullScreenSprite(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) throw new FileNotFoundException("Texture importer not found.", assetPath);

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        private static void ConfigurePixelSheet(string characterId, string pixelSheetPath, string frameFolder)
        {
            EnsureFolder(frameFolder);
            AssetDatabase.ImportAsset(pixelSheetPath, ImportAssetOptions.ForceSynchronousImport);

            var importer = AssetImporter.GetAtPath(pixelSheetPath) as TextureImporter;
            if (importer == null) throw new FileNotFoundException("Pixel sheet not found.", pixelSheetPath);

            importer.textureType = TextureImporterType.Default;
            importer.isReadable = true;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(pixelSheetPath);
            if (texture == null || texture.width != 1536 || texture.height != 1024)
            {
                var dimensions = texture == null ? "missing" : $"{texture.width}x{texture.height}";
                throw new InvalidDataException(
                    $"Expected 1536x1024 pixel sheet for {characterId}, found {dimensions}.");
            }

            for (var row = 0; row < 2; row++)
            {
                for (var column = 0; column < 4; column++)
                {
                    var frameIndex = row * 4 + column;
                    var frameName = $"{characterId}_{DirectionSuffixes[frameIndex]}";
                    WriteFrame(texture, frameFolder, frameName, column, row);
                }
            }

            importer = AssetImporter.GetAtPath(pixelSheetPath) as TextureImporter;
            if (importer == null) throw new InvalidDataException($"Pixel sheet importer disappeared: {pixelSheetPath}");
            importer.isReadable = false;
            importer.SaveAndReimport();
        }

        private static void WriteFrame(Texture2D source, string outputFolder, string frameName, int column, int row)
        {
            const int columns = 4;
            const int rows = 2;
            var cellWidth = source.width / columns;
            var cellHeight = source.height / rows;
            var pixels = source.GetPixels(column * cellWidth, source.height - ((row + 1) * cellHeight), cellWidth, cellHeight);
            var frameTexture = new Texture2D(cellWidth, cellHeight, TextureFormat.RGBA32, false)
            {
                name = frameName
            };
            frameTexture.SetPixels(pixels);
            frameTexture.Apply(false, false);

            var framePath = $"{outputFolder}/{frameName}.png";
            File.WriteAllBytes(Path.GetFullPath(framePath), frameTexture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(frameTexture);

            AssetDatabase.ImportAsset(framePath, ImportAssetOptions.ForceSynchronousImport);
            var frameImporter = AssetImporter.GetAtPath(framePath) as TextureImporter;
            if (frameImporter == null) throw new InvalidDataException($"Frame import failed: {framePath}");

            frameImporter.textureType = TextureImporterType.Sprite;
            frameImporter.spriteImportMode = SpriteImportMode.Single;
            frameImporter.spritePixelsPerUnit = PixelPixelsPerUnit;
            frameImporter.alphaIsTransparency = true;
            frameImporter.mipmapEnabled = false;
            frameImporter.filterMode = FilterMode.Point;
            frameImporter.textureCompression = TextureImporterCompression.Uncompressed;
            frameImporter.maxTextureSize = 1024;
            frameImporter.SaveAndReimport();
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) return;

            var parts = assetPath.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private sealed class EmployeeSpec
        {
            public EmployeeSpec(string id, string folderName)
            {
                Id = id;
                Root = $"{EmployeeRoot}/{folderName}";
            }

            public string Id { get; }
            public string Root { get; }
            public string PortraitFolder => $"{Root}/Portraits";
            public string ReferenceFolder => $"{Root}/References";
            public string PixelFolder => $"{Root}/Pixel";
            public string FrameFolder => $"{PixelFolder}/Frames";
            public string PixelSheetPath => $"{PixelFolder}/{Id}_pixel_walk4x2_v1.png";
        }

        private sealed class ParentSpec
        {
            public ParentSpec(string id, string folderName, string portraitFileName)
            {
                Id = id;
                Root = $"{CharacterRoot}/{folderName}";
                PortraitPath = $"{Root}/{portraitFileName}";
            }

            public string Id { get; }
            public string Root { get; }
            public string PortraitPath { get; }
            public string PixelFolder => $"{Root}/Pixel";
            public string FrameFolder => $"{PixelFolder}/Frames";
            public string PixelSheetPath => $"{PixelFolder}/{Id}_pixel_walk4x2_v1.png";
        }
    }
}
