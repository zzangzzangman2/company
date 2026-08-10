using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public static class HighMotionCharacterArtBuilder
    {
        public const int DirectionCount = 8;
        public const int WalkFrameCount = 6;
        public const int FramesPerCharacter = DirectionCount * WalkFrameCount;
        public const int CharacterCount = 12;
        public const int ExpectedTotalFrames = CharacterCount * FramesPerCharacter;
        public const float PixelPixelsPerUnit = 180f;

        private static readonly string[] Directions =
        {
            "south", "southwest", "west", "northwest",
            "north", "northeast", "east", "southeast"
        };

        private static readonly CharacterSpec[] Characters =
        {
            new("player", "Assets/Art/Characters/Player"),
            new("older_sister", "Assets/Art/Characters/OlderSister"),
            new("father", "Assets/Art/Characters/Father"),
            new("mother", "Assets/Art/Characters/Mother"),
            new("kim_seoa", "Assets/Art/Characters/Employees/KimSeoa"),
            new("lee_jian", "Assets/Art/Characters/Employees/LeeJian"),
            new("choi_iseo", "Assets/Art/Characters/Employees/ChoiIseo"),
            new("jung_arin", "Assets/Art/Characters/Employees/JungArin"),
            new("park_haeun", "Assets/Art/Characters/Employees/ParkHaeun"),
            new("han_sua", "Assets/Art/Characters/Employees/HanSua"),
            new("oh_jiwoo", "Assets/Art/Characters/Employees/OhJiwoo"),
            new("yoon_chaea", "Assets/Art/Characters/Employees/YoonChaea")
        };

        [MenuItem("Family Company/Build High Motion Character Art Assets")]
        public static void Build()
        {
            ConfigureAll();
            Validate();
            Debug.Log(
                "High-motion character art build complete: " +
                $"characters={CharacterCount}, directions={DirectionCount}, walkFrames={WalkFrameCount}, " +
                $"sprites={ExpectedTotalFrames}.");
        }

        public static void ConfigureAll()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var character in Characters)
            {
                ConfigureSheet(character.PartAPath);
                ConfigureSheet(character.PartBPath);
                ConfigureFrames(character);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        [MenuItem("Family Company/Validate High Motion Character Art Assets")]
        public static void Validate()
        {
            var totalFrames = 0;
            foreach (var character in Characters)
            {
                ValidateSheet(character.Id, character.PartAPath);
                ValidateSheet(character.Id, character.PartBPath);
                var uniqueSprites = new HashSet<Sprite>();

                foreach (var frameName in GetFrameNames(character.Id))
                {
                    var framePath = $"{character.FrameFolder}/{frameName}.png";
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(framePath);
                    if (sprite == null)
                        throw new InvalidDataException($"Missing high-motion frame: {framePath}");
                    if (sprite.texture.width != 256 || sprite.texture.height != 256)
                        throw new InvalidDataException(
                            $"Expected 256x256 high-motion frame for {character.Id}, " +
                            $"found {sprite.texture.width}x{sprite.texture.height}: {framePath}");
                    if (!uniqueSprites.Add(sprite))
                        throw new InvalidDataException($"Duplicate high-motion sprite reference for {character.Id}: {framePath}");
                    ValidateFrameImporter(character.Id, framePath);
                    totalFrames++;
                }

                if (uniqueSprites.Count != FramesPerCharacter)
                    throw new InvalidDataException(
                        $"Expected {FramesPerCharacter} unique high-motion sprites for {character.Id}, " +
                        $"found {uniqueSprites.Count}.");
            }

            if (totalFrames != ExpectedTotalFrames)
                throw new InvalidDataException(
                    $"High-motion frame total is invalid. Expected {ExpectedTotalFrames}, found {totalFrames}.");

            Debug.Log(
                $"High-motion character art validation passed: characters={CharacterCount}, sprites={totalFrames}.");
        }

        public static string GetFrameFolder(string characterId)
        {
            foreach (var character in Characters)
            {
                if (string.Equals(character.Id, characterId, StringComparison.Ordinal))
                    return character.FrameFolder;
            }

            throw new ArgumentOutOfRangeException(nameof(characterId), characterId, "Unknown high-motion character.");
        }

        public static string[] GetFrameNames(string characterId)
        {
            var names = new List<string>(FramesPerCharacter);
            for (var phase = 0; phase < WalkFrameCount; phase++)
            {
                foreach (var direction in Directions)
                    names.Add($"{characterId}_{direction}_walk_{phase}");
            }

            return names.ToArray();
        }

        private static void ConfigureSheet(string assetPath)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) throw new FileNotFoundException("High-motion sheet not found.", assetPath);

            importer.textureType = TextureImporterType.Default;
            importer.isReadable = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        private static void ConfigureFrames(CharacterSpec character)
        {
            foreach (var frameName in GetFrameNames(character.Id))
            {
                var framePath = $"{character.FrameFolder}/{frameName}.png";
                AssetDatabase.ImportAsset(framePath, ImportAssetOptions.ForceSynchronousImport);
                var importer = AssetImporter.GetAtPath(framePath) as TextureImporter;
                if (importer == null) throw new FileNotFoundException("High-motion frame not found.", framePath);

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = PixelPixelsPerUnit;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 256;

                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = new Vector2(0.5f, 0f);
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
        }

        private static void ValidateSheet(string characterId, string assetPath)
        {
            var sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (sheet == null || sheet.width != 1536 || sheet.height != 1024)
            {
                var dimensions = sheet == null ? "missing" : $"{sheet.width}x{sheet.height}";
                throw new InvalidDataException(
                    $"Expected 1536x1024 high-motion sheet for {characterId}, found {dimensions}: {assetPath}");
            }
        }

        private static void ValidateFrameImporter(string characterId, string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null || importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Single || importer.mipmapEnabled ||
                importer.filterMode != FilterMode.Point ||
                Math.Abs(importer.spritePixelsPerUnit - PixelPixelsPerUnit) > 0.001f)
            {
                throw new InvalidDataException(
                    $"Invalid high-motion sprite importer for {characterId}: {assetPath}");
            }

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (settings.spriteAlignment != (int)SpriteAlignment.Custom ||
                Vector2.Distance(settings.spritePivot, new Vector2(0.5f, 0f)) > 0.0001f)
            {
                throw new InvalidDataException(
                    $"High-motion foot pivot must be bottom-center for {characterId}: {assetPath}");
            }
        }

        private sealed class CharacterSpec
        {
            public CharacterSpec(string id, string assetRoot)
            {
                Id = id;
                HighMotionRoot = $"{assetRoot}/Pixel/HighMotion";
            }

            public string Id { get; }
            public string HighMotionRoot { get; }
            public string FrameFolder => $"{HighMotionRoot}/Frames";
            public string PartAPath => $"{HighMotionRoot}/{Id}_pixel_walk8dir6_a_v1.png";
            public string PartBPath => $"{HighMotionRoot}/{Id}_pixel_walk8dir6_b_v1.png";
        }
    }
}
