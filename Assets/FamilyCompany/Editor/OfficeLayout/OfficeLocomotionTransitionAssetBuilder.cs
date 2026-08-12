using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using FamilyCompany.Presentation.Unity.OfficeRuntime;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor.OfficeLayout
{
    public static class OfficeLocomotionTransitionAssetBuilder
    {
        public const string AssetPath =
            "Assets/FamilyCompany/Content/Resources/HighMotion/OfficeLocomotionTransitionCatalog.asset";
        private const string Root =
            "Assets/Art/Characters/Family/LocomotionTransitionsV1";
        private const int FrameSize = 256;
        private const int TargetBottomPadding = 8;
        private const int MinimumVisiblePixels = 7000;
        private const float PixelsPerUnit = 180f;

        private static readonly string[] Members =
            { "player", "older_sister", "father", "mother" };
        private static readonly string[] Directions =
        {
            "south", "southwest", "west", "northwest",
            "north", "northeast", "east", "southeast"
        };
        private static readonly string[] Poses = { "a", "b" };
        private static readonly string[] Clips =
            { "turn_in_place", "walk_start", "walk_stop", "short_shuffle" };

        [MenuItem("Family Company/Art/Build Office Locomotion Transitions V1")]
        public static void Build()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var entries = new List<OfficeLocomotionTransitionEntry>(Members.Length);
            foreach (string member in Members)
            {
                var sprites = new List<Sprite>(
                    OfficeLocomotionTransitionCatalog.FramesPerMember);
                foreach (string clip in Clips)
                foreach (string direction in Directions)
                foreach (string pose in Poses)
                {
                    string path = FramePath(member, clip, direction, pose);
                    ConfigureFrameImporter(path);
                    Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    if (sprite == null)
                        throw new FileNotFoundException(
                            "Unity could not import locomotion transition frame.", path);
                    sprites.Add(sprite);
                }

                entries.Add(OfficeLocomotionTransitionEntry.Create(
                    member,
                    sprites.ToArray(),
                    ComputeCombinedSourceSha256(member)));
            }

            OfficeLocomotionTransitionCatalog catalog =
                AssetDatabase.LoadAssetAtPath<OfficeLocomotionTransitionCatalog>(AssetPath);
            if (catalog == null)
            {
                string folder = Path.GetDirectoryName(AssetPath)?.Replace('\\', '/');
                if (!string.IsNullOrWhiteSpace(folder) && !AssetDatabase.IsValidFolder(folder))
                    throw new DirectoryNotFoundException(
                        "Locomotion catalog asset folder is missing: " + folder);
                catalog = ScriptableObject.CreateInstance<OfficeLocomotionTransitionCatalog>();
                AssetDatabase.CreateAsset(catalog, AssetPath);
            }

            catalog.Configure(entries.ToArray());
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Validate();
            Debug.Log(
                "OFFICE_LOCOMOTION_TRANSITION_BUILD_PASS | members=4 clips=4 directions=8 poses=2 frames=256 bottomPadding=8");
        }

        [MenuItem("Family Company/QA/Validate Office Locomotion Transitions V1")]
        public static void Validate()
        {
            OfficeLocomotionTransitionCatalog catalog =
                AssetDatabase.LoadAssetAtPath<OfficeLocomotionTransitionCatalog>(AssetPath);
            if (catalog == null)
                throw new FileNotFoundException("Locomotion transition catalog is missing.", AssetPath);
            catalog.Validate();

            var unique = new HashSet<Sprite>();
            foreach (string member in Members)
            {
                Sprite[] sprites = catalog.CopyFrames(member);
                if (sprites.Length != OfficeLocomotionTransitionCatalog.FramesPerMember)
                    throw new InvalidDataException(
                        $"Expected 64 transition frames for {member}; found {sprites.Length}.");
                foreach (Sprite sprite in sprites)
                {
                    unique.Add(sprite);
                    ValidateRawFrame(AssetDatabase.GetAssetPath(sprite));
                }
            }
            if (unique.Count != 256)
                throw new InvalidDataException(
                    $"Expected 256 unique locomotion transition frames; found {unique.Count}.");
            Debug.Log(
                "OFFICE_LOCOMOTION_TRANSITION_ASSET_QA_PASS | members=4 clips=4 directions=8 poses=2 slots=256 uniqueArt=256 hardAlpha=true bottomPadding=8");
        }

        private static string FramePath(
            string member,
            string clip,
            string direction,
            string pose) =>
            $"{Root}/{member}/Frames/{member}_{direction}_{clip}_{pose}.png";

        private static string SourceChromaPath(string member, string clip) =>
            $"{Root}/{member}/Source/{member}_{clip}_4x4_chroma_v1.png";

        private static void ConfigureFrameImporter(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                throw new FileNotFoundException("Locomotion transition PNG is missing.", path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = FrameSize;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(0.5f, 0f);
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        private static void ValidateRawFrame(string assetPath)
        {
            string absolutePath = ToAbsolutePath(assetPath);
            byte[] encoded = File.ReadAllBytes(absolutePath);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!ImageConversion.LoadImage(texture, encoded, false) ||
                    texture.width != FrameSize || texture.height != FrameSize)
                    throw new InvalidDataException(
                        "Locomotion transition frame must be an encoded 256x256 PNG: " + assetPath);
                Color32[] pixels = texture.GetPixels32();
                var visible = 0;
                var minimumVisibleY = FrameSize;
                for (var y = 0; y < FrameSize; y++)
                for (var x = 0; x < FrameSize; x++)
                {
                    byte alpha = pixels[y * FrameSize + x].a;
                    if (alpha != 0 && alpha != byte.MaxValue)
                        throw new InvalidDataException(
                            $"Locomotion transition frame must use hard alpha: {assetPath} alpha={alpha}.");
                    if (alpha == 0) continue;
                    visible++;
                    minimumVisibleY = Mathf.Min(minimumVisibleY, y);
                }
                if (visible < MinimumVisiblePixels)
                    throw new InvalidDataException(
                        $"Locomotion transition silhouette is too small: {assetPath} pixels={visible}.");
                int bottomPadding = minimumVisibleY;
                if (bottomPadding != TargetBottomPadding)
                    throw new InvalidDataException(
                        $"Locomotion transition foot anchor is not normalized: {assetPath} padding={bottomPadding}.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static string ComputeCombinedSourceSha256(string member)
        {
            using SHA256 sha = SHA256.Create();
            using var stream = new MemoryStream();
            foreach (string clip in Clips)
            {
                string assetPath = SourceChromaPath(member, clip);
                string absolutePath = ToAbsolutePath(assetPath);
                if (!File.Exists(absolutePath))
                    throw new FileNotFoundException(
                        "Locomotion transition source is missing.", assetPath);
                byte[] bytes = File.ReadAllBytes(absolutePath);
                stream.Write(bytes, 0, bytes.Length);
            }
            stream.Position = 0;
            return string.Concat(sha.ComputeHash(stream).Select(value => value.ToString("x2")));
        }

        private static string ToAbsolutePath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrWhiteSpace(projectRoot))
                throw new DirectoryNotFoundException("Unable to resolve Unity project root.");
            return Path.GetFullPath(Path.Combine(
                projectRoot,
                assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }
    }
}
