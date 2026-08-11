using System;
using System.Collections.Generic;
using System.IO;
using FamilyCompany.Presentation.Unity.OfficeRuntime;
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

        private const int FramePixelSize = 256;
        private const int SheetRowsPerPart = DirectionCount / 2;
        private const int CanonicalSheetWidth = WalkFrameCount * FramePixelSize;
        private const int CanonicalSheetHeight = SheetRowsPerPart * FramePixelSize;
        private const int MinimumMainSilhouettePixels = 1000;

        private static readonly byte[] PngSignature =
        {
            0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a
        };

        private static readonly string[] Directions =
        {
            "south", "southwest", "west", "northwest",
            "north", "northeast", "east", "southeast"
        };

        private static readonly string[] PartADirections =
        {
            "south", "southwest", "west", "northwest"
        };

        private static readonly string[] PartBDirections =
        {
            "north", "northeast", "east", "southeast"
        };

        private static readonly CharacterSpec[] Characters =
        {
            new("player", "Assets/Art/Characters/Player", CanonicalSheetWidth, CanonicalSheetHeight),
            new("older_sister", "Assets/Art/Characters/OlderSister", CanonicalSheetWidth, CanonicalSheetHeight),
            new("father", "Assets/Art/Characters/Father", CanonicalSheetWidth, CanonicalSheetHeight),
            new("mother", "Assets/Art/Characters/Mother", CanonicalSheetWidth, CanonicalSheetHeight),
            new("kim_seoa", "Assets/Art/Characters/Employees/KimSeoa", CanonicalSheetWidth, CanonicalSheetHeight),
            new("lee_jian", "Assets/Art/Characters/Employees/LeeJian", CanonicalSheetWidth, CanonicalSheetHeight),
            new("choi_iseo", "Assets/Art/Characters/Employees/ChoiIseo", CanonicalSheetWidth, CanonicalSheetHeight),
            new("jung_arin", "Assets/Art/Characters/Employees/JungArin", CanonicalSheetWidth, CanonicalSheetHeight),
            new("park_haeun", "Assets/Art/Characters/Employees/ParkHaeun", CanonicalSheetWidth, CanonicalSheetHeight),
            new("han_sua", "Assets/Art/Characters/Employees/HanSua", CanonicalSheetWidth, CanonicalSheetHeight),
            new("oh_jiwoo", "Assets/Art/Characters/Employees/OhJiwoo", CanonicalSheetWidth, CanonicalSheetHeight),
            new("yoon_chaea", "Assets/Art/Characters/Employees/YoonChaea", CanonicalSheetWidth, CanonicalSheetHeight)
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
                ValidateSheet(character, character.PartAPath, PartADirections);
                ValidateSheet(character, character.PartBPath, PartBDirections);
                var uniqueSprites = new HashSet<Sprite>();

                foreach (var frameName in GetFrameNames(character.Id))
                {
                    var framePath = $"{character.FrameFolder}/{frameName}.png";
                    ValidateFrameFile(character.Id, frameName, framePath);
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(framePath);
                    if (sprite == null)
                        throw new InvalidDataException($"Missing high-motion frame: {framePath}");
                    if (sprite.texture.width != FramePixelSize || sprite.texture.height != FramePixelSize)
                        throw new InvalidDataException(
                            $"Expected {FramePixelSize}x{FramePixelSize} high-motion frame for {character.Id}, " +
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
            HighMotionDirectionManifest manifest = HighMotionDirectionManifest.LoadDefault();
            for (var phase = 0; phase < WalkFrameCount; phase++)
            for (var canonicalDirection = 0; canonicalDirection < DirectionCount; canonicalDirection++)
            {
                int sourceDirection = manifest == null
                    ? canonicalDirection
                    : manifest.ResolveSourceDirection(characterId, canonicalDirection);
                names.Add($"{characterId}_{Directions[sourceDirection]}_walk_{phase}");
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

        private static void ValidateSheet(
            CharacterSpec character,
            string assetPath,
            IReadOnlyList<string> rowDirections)
        {
            if (rowDirections == null || rowDirections.Count != SheetRowsPerPart)
                throw new ArgumentException("A high-motion sheet part must define four direction rows.", nameof(rowDirections));

            if (AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath) == null)
                throw new FileNotFoundException("High-motion sheet is not available to Unity.", assetPath);

            // The canonical 1536px-wide PNG is NPOT. Validate its encoded pixels directly so an
            // importer-side power-of-two resize (for example, 1536 -> 2048) cannot change the contract.
            var sheet = LoadRawPng(assetPath);
            try
            {
                if (sheet.width != character.ExpectedSheetWidth || sheet.height != character.ExpectedSheetHeight)
                {
                    throw new InvalidDataException(
                        $"Expected canonical {character.ExpectedSheetWidth}x{character.ExpectedSheetHeight} " +
                        $"raw high-motion sheet for {character.Id}, found {sheet.width}x{sheet.height}: {assetPath}");
                }

                var pixels = sheet.GetPixels32();
                ValidateHardAlpha(character.Id, assetPath, pixels);
                ValidateSheetCells(character, assetPath, rowDirections, pixels);
                ValidateMainSilhouetteCount(character.Id, assetPath, sheet.width, sheet.height, pixels);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sheet);
            }
        }

        private static void ValidateFrameFile(string characterId, string frameName, string assetPath)
        {
            var frame = LoadRawPng(assetPath);
            try
            {
                if (frame.width != FramePixelSize || frame.height != FramePixelSize)
                {
                    throw new InvalidDataException(
                        $"Expected canonical {FramePixelSize}x{FramePixelSize} raw high-motion frame " +
                        $"for {characterId}, found {frame.width}x{frame.height}: {assetPath}");
                }

                var pixels = frame.GetPixels32();
                var opaquePixels = ValidateHardAlpha(characterId, assetPath, pixels);
                if (opaquePixels < MinimumMainSilhouettePixels)
                {
                    throw new InvalidDataException(
                        $"High-motion frame has no canonical silhouette for {characterId} " +
                        $"({frameName}, opaquePixels={opaquePixels}): {assetPath}");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(frame);
            }
        }

        private static Texture2D LoadRawPng(string assetPath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
                throw new DirectoryNotFoundException("Unable to resolve the Unity project root.");

            var absolutePath = Path.GetFullPath(
                Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)));
            if (!File.Exists(absolutePath))
                throw new FileNotFoundException("High-motion PNG not found.", assetPath);

            var encoded = File.ReadAllBytes(absolutePath);
            if (encoded.Length < PngSignature.Length)
                throw new InvalidDataException($"High-motion asset is not a PNG: {assetPath}");
            for (var index = 0; index < PngSignature.Length; index++)
            {
                if (encoded[index] != PngSignature[index])
                    throw new InvalidDataException($"High-motion asset is not a PNG: {assetPath}");
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (ImageConversion.LoadImage(texture, encoded, false)) return texture;

            UnityEngine.Object.DestroyImmediate(texture);
            throw new InvalidDataException($"Unable to decode high-motion PNG: {assetPath}");
        }

        private static int ValidateHardAlpha(string characterId, string assetPath, Color32[] pixels)
        {
            var opaquePixels = 0;
            var transparentPixels = 0;
            foreach (var pixel in pixels)
            {
                if (pixel.a == byte.MaxValue)
                {
                    opaquePixels++;
                }
                else if (pixel.a == 0)
                {
                    transparentPixels++;
                }
                else
                {
                    throw new InvalidDataException(
                        $"High-motion PNG must use hard alpha 0/255 for {characterId}; " +
                        $"found alpha={pixel.a}: {assetPath}");
                }
            }

            if (opaquePixels == 0 || transparentPixels == 0)
            {
                throw new InvalidDataException(
                    $"High-motion PNG must contain both a silhouette and transparent background " +
                    $"for {characterId}: {assetPath}");
            }

            return opaquePixels;
        }

        private static void ValidateSheetCells(
            CharacterSpec character,
            string assetPath,
            IReadOnlyList<string> rowDirections,
            Color32[] pixels)
        {
            var cellWidth = character.ExpectedSheetWidth / WalkFrameCount;
            var cellHeight = character.ExpectedSheetHeight / SheetRowsPerPart;
            if (cellWidth != FramePixelSize || cellHeight != FramePixelSize)
            {
                throw new InvalidDataException(
                    $"Invalid canonical high-motion sheet specification for {character.Id}: " +
                    $"{character.ExpectedSheetWidth}x{character.ExpectedSheetHeight}");
            }

            for (var row = 0; row < SheetRowsPerPart; row++)
            {
                for (var phase = 0; phase < WalkFrameCount; phase++)
                {
                    var opaquePixels = 0;
                    for (var yFromTop = row * cellHeight; yFromTop < (row + 1) * cellHeight; yFromTop++)
                    {
                        var sourceY = character.ExpectedSheetHeight - 1 - yFromTop;
                        var rowOffset = sourceY * character.ExpectedSheetWidth;
                        for (var x = phase * cellWidth; x < (phase + 1) * cellWidth; x++)
                        {
                            if (pixels[rowOffset + x].a == byte.MaxValue) opaquePixels++;
                        }
                    }

                    if (opaquePixels < MinimumMainSilhouettePixels)
                    {
                        throw new InvalidDataException(
                            $"Missing high-motion sheet cell for {character.Id}: " +
                            $"direction={rowDirections[row]}, phase={phase}, opaquePixels={opaquePixels}: {assetPath}");
                    }
                }
            }
        }

        private static void ValidateMainSilhouetteCount(
            string characterId,
            string assetPath,
            int width,
            int height,
            Color32[] pixels)
        {
            var visited = new bool[pixels.Length];
            var queue = new int[pixels.Length];
            var mainSilhouettes = 0;

            for (var start = 0; start < pixels.Length; start++)
            {
                if (visited[start] || pixels[start].a == 0) continue;

                var head = 0;
                var tail = 0;
                var componentPixels = 0;
                visited[start] = true;
                queue[tail++] = start;

                while (head < tail)
                {
                    var current = queue[head++];
                    componentPixels++;
                    var x = current % width;
                    var y = current / width;

                    var minX = Math.Max(0, x - 1);
                    var maxX = Math.Min(width - 1, x + 1);
                    var minY = Math.Max(0, y - 1);
                    var maxY = Math.Min(height - 1, y + 1);
                    for (var neighborY = minY; neighborY <= maxY; neighborY++)
                    {
                        var rowOffset = neighborY * width;
                        for (var neighborX = minX; neighborX <= maxX; neighborX++)
                        {
                            var neighbor = rowOffset + neighborX;
                            if (visited[neighbor] || pixels[neighbor].a == 0) continue;
                            visited[neighbor] = true;
                            queue[tail++] = neighbor;
                        }
                    }
                }

                if (componentPixels >= MinimumMainSilhouettePixels) mainSilhouettes++;
            }

            var expectedSilhouettes = SheetRowsPerPart * WalkFrameCount;
            if (mainSilhouettes != expectedSilhouettes)
            {
                throw new InvalidDataException(
                    $"Expected {expectedSilhouettes} main silhouettes in high-motion sheet for {characterId}, " +
                    $"found {mainSilhouettes}: {assetPath}");
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
            public CharacterSpec(
                string id,
                string assetRoot,
                int expectedSheetWidth,
                int expectedSheetHeight)
            {
                Id = id;
                HighMotionRoot = $"{assetRoot}/Pixel/HighMotion";
                ExpectedSheetWidth = expectedSheetWidth;
                ExpectedSheetHeight = expectedSheetHeight;
            }

            public string Id { get; }
            public string HighMotionRoot { get; }
            public int ExpectedSheetWidth { get; }
            public int ExpectedSheetHeight { get; }
            public string FrameFolder => $"{HighMotionRoot}/Frames";
            public string PartAPath => $"{HighMotionRoot}/{Id}_pixel_walk8dir6_a_v1.png";
            public string PartBPath => $"{HighMotionRoot}/{Id}_pixel_walk8dir6_b_v1.png";
        }
    }
}
