using System;
using System.IO;
using FamilyCompany.Presentation.Unity.OfficeGridView.Authoring;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor.OfficeGridQa
{
    /// <summary>
    /// Guards the four user-approved V31 open-back chair renders. The retired green chair used a
    /// hand-cut foreground mask; V31 uses one clean directional sprite and no legacy overlay.
    /// </summary>
    public static class OfficeChairForegroundValidation
    {
        private const string Folder =
            "Assets/FamilyCompany/Content/Resources/OfficeBuildFurniture";

        [MenuItem("Family Company/Validate V31 Chair Directional Integrity")]
        public static void Validate()
        {
            OfficeFurnitureVisualCatalog catalog = OfficeFurnitureAssetBuilder.LoadFurnitureVisualCatalog();
            OfficeFurnitureVisualDefinition chair = catalog.Resolve(
                OfficeGridLayouts.SwivelChairKind,
                OfficeFurnitureFacing.NorthWest);
            Sprite canonical = RequiredSprite(Folder + "/swivel_chair_nw.png");
            Require(chair.BaseSprite == canonical, "Canonical V31 chair Sprite is not catalogued.");
            Require(chair.FrontOverlaySprite == null,
                "V31 chair must not retain the old green-chair foreground overlay.");
            Require(!chair.FrontOverlayWhenOccupied,
                "V31 chair must not enable a retired foreground overlay.");
            Require(Vector2.Distance(chair.GroundAnchorPx, new Vector2(320f, 64f)) <= 0.001f,
                "V31 chair ground anchor drifted.");
            Require(Vector2.Distance(chair.SeatAnchorPx, new Vector2(432.085f, 248.044f)) <= 0.001f,
                "V31 chair seat anchor drifted.");

            foreach (string suffix in new[] { "se", "sw", "nw", "ne" })
            {
                string path = Folder + "/swivel_chair_" + suffix + ".png";
                Sprite sprite = RequiredSprite(path);
                Require(sprite.rect.size == new Vector2(640f, 512f),
                    "V31 chair canvas differs from 640x512: " + path);
                Require(Mathf.Approximately(sprite.pixelsPerUnit, 180f),
                    "V31 chair PPU differs from 180: " + path);
                Require(HasVisibleAlpha(path), "V31 chair render is empty: " + path);
            }

            Debug.Log(
                "OFFICE_V31_CHAIR_DIRECTIONAL_VALIDATION: PASS sprites=4 canvas=640x512 " +
                "ppu=180 openBack=true legacyForeground=false seatAnchor=directional");
        }

        public static void RunBatch()
        {
            try
            {
                Validate();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static bool HasVisibleAlpha(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("V31 chair Sprite is missing.", path);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(path), false))
                    throw new InvalidDataException("Could not decode V31 chair texture: " + path);
                Color32[] pixels = texture.GetPixels32();
                for (var index = 0; index < pixels.Length; index++)
                    if (pixels[index].a > 0) return true;
                return false;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static Sprite RequiredSprite(string path)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new FileNotFoundException("V31 chair Sprite is missing.", path);
            return sprite;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
