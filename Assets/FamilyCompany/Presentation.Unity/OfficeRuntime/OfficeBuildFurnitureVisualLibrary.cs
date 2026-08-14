using System;
using System.Linq;
using System.Collections.Generic;
using FamilyCompany.Presentation.Unity.OfficeGridView.Authoring;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime
{
    /// <summary>
    /// Additive directional art resolver owned by build mode. It leaves the shared authored visual
    /// catalog untouched, avoiding conflicts with wall and seating branches. Exact Resources art
    /// wins; existing catalog/mirror art remains the fallback for every canonical prop.
    /// </summary>
    public static class OfficeBuildFurnitureVisualLibrary
    {
        private const string ResourceRoot = "OfficeBuildFurniture/";
        private static readonly Dictionary<OfficeFurnitureFacing, Sprite> ProceduralVending =
            new Dictionary<OfficeFurnitureFacing, Sprite>();

        public static bool TryResolve(
            OfficeFurnitureVisualCatalog catalog,
            string kindId,
            OfficeFurnitureFacing facing,
            out OfficeFurnitureVisualDefinition definition,
            out bool flipX)
        {
            flipX = false;
            Sprite sprite = Resources.Load<Sprite>(ResourceRoot + ResourceName(kindId, facing));
            if (sprite != null)
            {
                definition = DirectionalDefinition(catalog, kindId, facing, sprite);
                return true;
            }
            if (catalog != null && catalog.TryResolveWithMirror(kindId, facing, out definition, out flipX))
                return true;
            if (string.Equals(
                    kindId,
                    OfficeFurnitureCatalog.DrinkVendingMachineDefinitionId,
                    StringComparison.Ordinal))
            {
                if (!ProceduralVending.TryGetValue(facing, out Sprite fallback))
                {
                    fallback = CreateProceduralVending(facing);
                    ProceduralVending.Add(facing, fallback);
                }
                definition = DirectionalDefinition(catalog, kindId, facing, fallback);
                return true;
            }
            // Presentation safety net for a quarter turn whose exact additive Resources art has
            // not imported yet. The semantic footprint/facing remains correct and the editor never
            // crashes; QA/reporting still flags this as non-final directional art.
            OfficeFurnitureVisualDefinition nearest = catalog?.Definitions.FirstOrDefault(item =>
                item != null && string.Equals(item.KindId, kindId, StringComparison.Ordinal));
            if (nearest != null)
            {
                definition = DirectionalDefinition(catalog, kindId, facing, nearest.BaseSprite);
                return true;
            }
            definition = null;
            return false;
        }

        public static Sprite Thumbnail(
            OfficeFurnitureVisualCatalog catalog,
            string kindId,
            OfficeFurnitureFacing facing)
        {
            return TryResolve(catalog, kindId, facing, out OfficeFurnitureVisualDefinition definition, out _)
                ? definition.BaseSprite
                : null;
        }

        private static OfficeFurnitureVisualDefinition DirectionalDefinition(
            OfficeFurnitureVisualCatalog catalog,
            string kindId,
            OfficeFurnitureFacing facing,
            Sprite sprite)
        {
            OfficeFurnitureVisualDefinition source = catalog?.Definitions.FirstOrDefault(item =>
                item != null && string.Equals(item.KindId, kindId, StringComparison.Ordinal));
            OfficeGridCoordinate footprint = OfficeFurnitureCatalog.Require(kindId).FootprintFor(facing);
            float width = sprite.rect.width;
            float groundY = Mathf.Clamp(source?.GroundAnchorPx.y ?? 28f, 16f, sprite.rect.height - 1f);
            var ground = new Vector2(width * 0.5f, groundY);
            Vector2[] polygon = footprint.X == 1 && footprint.Y == 1
                ? Diamond(ground, 160f, 80f)
                : Diamond(ground, 160f * footprint.X, 80f * footprint.Y);
            return OfficeFurnitureVisualDefinition.Create(
                kindId,
                facing,
                sprite,
                null,
                ground,
                ground,
                source?.SeatAnchorPx ?? Vector2.zero,
                source?.WorkSurfaceAnchorPx ?? Vector2.zero,
                1f,
                source?.HasSeatAnchor ?? false,
                source?.HasWorkSurfaceAnchor ?? false,
                false,
                polygon,
                footprint.X,
                footprint.Y,
                source?.OperatorSeatSocketPx ?? Vector2.zero,
                source?.HasOperatorSeatSocket ?? false);
        }

        private static Vector2[] Diamond(Vector2 anchor, float width, float height)
        {
            float halfWidth = Mathf.Min(width * 0.5f, anchor.x - 1f);
            float halfHeight = Mathf.Min(height * 0.5f, anchor.y - 1f);
            return new[]
            {
                new Vector2(anchor.x, anchor.y + halfHeight),
                new Vector2(anchor.x + halfWidth, anchor.y),
                new Vector2(anchor.x, anchor.y - halfHeight),
                new Vector2(anchor.x - halfWidth, anchor.y)
            };
        }

        private static string ResourceName(string kindId, OfficeFurnitureFacing facing) =>
            (kindId ?? string.Empty).Trim() + "_" + FacingSuffix(facing);

        private static string FacingSuffix(OfficeFurnitureFacing facing)
        {
            switch (facing)
            {
                case OfficeFurnitureFacing.SouthEast: return "se";
                case OfficeFurnitureFacing.SouthWest: return "sw";
                case OfficeFurnitureFacing.NorthWest: return "nw";
                default: return "ne";
            }
        }

        private static Sprite CreateProceduralVending(OfficeFurnitureFacing facing)
        {
            const int width = 320;
            const int height = 256;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "drink_vending_machine_" + FacingSuffix(facing) + "_fallback",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = Enumerable.Repeat(new Color32(0, 0, 0, 0), width * height).ToArray();
            Color32 ink = new Color32(45, 55, 50, 255);
            Color32 cream = new Color32(226, 201, 150, 255);
            Color32 mint = new Color32(121, 164, 139, 255);
            Color32 dark = new Color32(53, 65, 59, 255);
            bool frontLeft = facing == OfficeFurnitureFacing.SouthWest ||
                             facing == OfficeFurnitureFacing.NorthWest;
            int bodyX = frontLeft ? 105 : 115;
            int bodyY = 31;
            Fill(pixels, width, bodyX, bodyY, 100, 162, ink);
            Fill(pixels, width, bodyX + 3, bodyY + 3, 94, 156, cream);
            int sideX = frontLeft ? bodyX + 65 : bodyX + 3;
            Fill(pixels, width, sideX, bodyY + 6, 30, 148, frontLeft ? cream : mint);
            int frontX = frontLeft ? bodyX + 5 : bodyX + 31;
            Fill(pixels, width, frontX, bodyY + 35, 62, 103, ink);
            Fill(pixels, width, frontX + 3, bodyY + 38, 56, 78, new Color32(65, 78, 69, 255));
            Color32[] products =
            {
                new Color32(190, 76, 54, 255), new Color32(218, 151, 50, 255),
                new Color32(91, 150, 92, 255), new Color32(70, 122, 161, 255)
            };
            for (int row = 0; row < 3; row++)
            for (int column = 0; column < 4; column++)
                Fill(pixels, width, frontX + 7 + column * 12, bodyY + 46 + row * 21,
                    8, 15, products[(row + column) & 3]);
            Fill(pixels, width, frontX + 7, bodyY + 18, 47, 14, dark);
            Fill(pixels, width, frontLeft ? bodyX + 72 : bodyX + 8, bodyY + 68, 15, 36, ink);
            Fill(pixels, width, frontLeft ? bodyX + 76 : bodyX + 12, bodyY + 75, 7, 7, mint);
            // Fixed upper-left highlight and small foot keep the procedural guard readable while
            // preserving real transparent padding and a bottom-center pivot.
            Fill(pixels, width, bodyX + 5, bodyY + 151, 72, 3, new Color32(250, 232, 190, 255));
            Fill(pixels, width, bodyX + 13, bodyY - 3, 76, 5, dark);
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(
                texture,
                new Rect(0, 0, width, height),
                new Vector2(0.5f, 0.12f),
                100f,
                0,
                SpriteMeshType.FullRect);
            sprite.name = texture.name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static void Fill(
            Color32[] pixels,
            int textureWidth,
            int x,
            int y,
            int width,
            int height,
            Color32 color)
        {
            for (int py = Math.Max(0, y); py < Math.Min(pixels.Length / textureWidth, y + height); py++)
            for (int px = Math.Max(0, x); px < Math.Min(textureWidth, x + width); px++)
                pixels[py * textureWidth + px] = color;
        }
    }
}
