using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Experimental.Family3D.Editor
{
    /// <summary>
    /// Guards the resolution the paid Higgsfield albedos actually reach the GPU at.
    ///
    /// Every paid Higgsfield albedo is a 4096x4096 source PNG. Until 2026-08-26 they imported at
    /// Unity's default
    /// maxTextureSize of 2048 with TextureImporterCompression.Compressed, so the generated texture
    /// was halved and then lossily compressed before anyone saw it. That is measurable, and it is
    /// the direct cause of the "character is not sharp" report, so it gets a gate rather than a
    /// comment. This validation compares the imported Texture2D against the PNG header on disk;
    /// a comment in a .meta cannot regress silently past it.
    /// </summary>
    public static class Family3DHiggsfieldAlbedoImportValidation
    {
        private static readonly string[] AlbedoPaths =
        {
            "Assets/FamilyCompany/Experimental/Family3DPrototype/Candidates/" +
            "FatherV18HiggsfieldMotionV19/father-v18-higgsfield-motion-v19-albedo.png",
            "Assets/FamilyCompany/Experimental/Family3DPrototype/Candidates/" +
            "FatherV18HiggsfieldStatic/father-v18-higgsfield-static-albedo.png",
            "Assets/FamilyCompany/Experimental/Family3DPrototype/Candidates/" +
            "FatherV18HiggsfieldCasualWalk613/father-v18-higgsfield-casual-walk-613-albedo.png"
        };

        public static void Run()
        {
            var lines = new List<string>();
            foreach (string path in AlbedoPaths)
                lines.Add(Validate(path));
            Debug.Log(
                "FAMILY_3D_HIGGSFIELD_ALBEDO_IMPORT: PASS | " + string.Join(" | ", lines));
        }

        public static void RunBatch()
        {
            try
            {
                Run();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "FAMILY_3D_HIGGSFIELD_ALBEDO_IMPORT: FAIL | " +
                    exception.GetType().Name + ": " + exception.Message);
                EditorApplication.Exit(1);
            }
        }

        private static string Validate(string assetPath)
        {
            if (!File.Exists(assetPath))
                throw new InvalidOperationException(assetPath + " is missing.");

            ReadPngSize(assetPath, out int sourceWidth, out int sourceHeight);

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException(assetPath + " has no TextureImporter.");
            if (importer.maxTextureSize < sourceWidth || importer.maxTextureSize < sourceHeight)
                throw new InvalidOperationException(
                    assetPath + " caps maxTextureSize at " +
                    importer.maxTextureSize.ToString(CultureInfo.InvariantCulture) +
                    " below its " + Describe(sourceWidth, sourceHeight) + " source, discarding paid detail.");
            if (importer.textureCompression == TextureImporterCompression.Compressed ||
                importer.textureCompression == TextureImporterCompression.CompressedLQ)
                throw new InvalidOperationException(
                    assetPath + " uses " + importer.textureCompression +
                    "; a paid character albedo requires CompressedHQ or Uncompressed.");
            if (!importer.sRGBTexture)
                throw new InvalidOperationException(assetPath + " must import as sRGB.");

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (texture == null)
                throw new InvalidOperationException(assetPath + " did not load as a Texture2D.");
            if (texture.width != sourceWidth || texture.height != sourceHeight)
                throw new InvalidOperationException(
                    assetPath + " imported at " + Describe(texture.width, texture.height) +
                    " from a " + Describe(sourceWidth, sourceHeight) + " source.");

            return Path.GetFileNameWithoutExtension(assetPath) + " " +
                   Describe(texture.width, texture.height) + " " + texture.format;
        }

        private static string Describe(int width, int height) =>
            width.ToString(CultureInfo.InvariantCulture) + "x" +
            height.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// Reads width/height straight out of the PNG IHDR so the check compares the imported
        /// texture against the file on disk, not against another Unity-side value that could drift
        /// with it.
        /// </summary>
        private static void ReadPngSize(string assetPath, out int width, out int height)
        {
            using FileStream stream = File.OpenRead(assetPath);
            var header = new byte[24];
            if (stream.Read(header, 0, header.Length) != header.Length)
                throw new InvalidOperationException(assetPath + " is too short to be a PNG.");
            if (header[0] != 0x89 || header[1] != 'P' || header[2] != 'N' || header[3] != 'G')
                throw new InvalidOperationException(assetPath + " is not a PNG.");
            width = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
            height = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
        }
    }
}
