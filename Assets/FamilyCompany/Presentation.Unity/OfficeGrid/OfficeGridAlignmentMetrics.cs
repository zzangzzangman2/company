using System;
using FamilyCompany.Presentation.Unity.OfficeGridView.Authoring;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeGridView
{
    public static class OfficeGridAlignmentMetrics
    {
        public static Vector3 SpriteAnchorWorld(SpriteRenderer renderer, Vector2 spriteRectAnchorPx)
        {
            if (renderer == null) throw new ArgumentNullException(nameof(renderer));
            Sprite sprite = renderer.sprite != null
                ? renderer.sprite
                : throw new InvalidOperationException("Sprite renderer has no active frame.");
            Vector2 localPixels = spriteRectAnchorPx - sprite.pivot;
            var local = new Vector3(
                localPixels.x / sprite.pixelsPerUnit,
                localPixels.y / sprite.pixelsPerUnit,
                0f);
            return renderer.transform.TransformPoint(local);
        }

        public static float GroundAnchorScreenErrorPx(
            Camera camera,
            SpriteRenderer furnitureRenderer,
            OfficeFurnitureVisualDefinition definition,
            Vector3 semanticFootprintWorld)
        {
            return ScreenDistance(camera, SpriteAnchorWorld(furnitureRenderer, definition.GroundAnchorPx), semanticFootprintWorld);
        }

        public static float SeatAnchorScreenErrorPx(
            Camera camera,
            SpriteRenderer characterRenderer,
            OfficeCharacterSeatPoseProfile pose,
            SpriteRenderer chairRenderer,
            OfficeFurnitureVisualDefinition chairDefinition)
        {
            return ScreenDistance(
                camera,
                SpriteAnchorWorld(characterRenderer, pose.PelvisAnchorPx),
                SpriteAnchorWorld(chairRenderer, chairDefinition.SeatAnchorPx));
        }

        public static float DeskChairCenterlineErrorPx(
            Camera camera,
            SpriteRenderer deskRenderer,
            OfficeFurnitureVisualDefinition deskDefinition,
            Vector3 expectedDeskWorld,
            SpriteRenderer chairRenderer,
            OfficeFurnitureVisualDefinition chairDefinition,
            Vector3 expectedChairWorld)
        {
            Vector2 desk = ScreenPoint(camera, SpriteAnchorWorld(deskRenderer, deskDefinition.GroundAnchorPx));
            Vector2 chair = ScreenPoint(camera, SpriteAnchorWorld(chairRenderer, chairDefinition.GroundAnchorPx));
            Vector2 expectedDesk = ScreenPoint(camera, expectedDeskWorld);
            Vector2 expectedChair = ScreenPoint(camera, expectedChairWorld);
            return Mathf.Max(
                PointLineDistance(desk, expectedChair, expectedDesk),
                PointLineDistance(chair, expectedChair, expectedDesk));
        }

        public static float DeskInteractionDepthErrorPx(
            Camera camera,
            SpriteRenderer characterRenderer,
            OfficeCharacterSeatPoseProfile pose,
            SpriteRenderer deskRenderer,
            OfficeFurnitureVisualDefinition deskDefinition)
        {
            return ScreenDistance(
                camera,
                SpriteAnchorWorld(characterRenderer, pose.DeskInteractionAnchorPx),
                SpriteAnchorWorld(deskRenderer, deskDefinition.WorkSurfaceAnchorPx));
        }

        public static float ScreenDistance(Camera camera, Vector3 firstWorld, Vector3 secondWorld)
        {
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            Vector3 first = camera.WorldToScreenPoint(firstWorld);
            Vector3 second = camera.WorldToScreenPoint(secondWorld);
            return Vector2.Distance(new Vector2(first.x, first.y), new Vector2(second.x, second.y));
        }

        private static Vector2 ScreenPoint(Camera camera, Vector3 world)
        {
            Vector3 screen = camera.WorldToScreenPoint(world);
            return new Vector2(screen.x, screen.y);
        }

        private static float PointLineDistance(Vector2 point, Vector2 lineStart, Vector2 lineEnd)
        {
            Vector2 line = lineEnd - lineStart;
            if (line.sqrMagnitude <= 0.000001f) return Vector2.Distance(point, lineStart);
            float t = Mathf.Clamp01(Vector2.Dot(point - lineStart, line) / line.sqrMagnitude);
            return Vector2.Distance(point, lineStart + line * t);
        }
    }
}
