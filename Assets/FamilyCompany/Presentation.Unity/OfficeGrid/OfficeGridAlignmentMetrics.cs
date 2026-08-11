using System;
using System.Collections.Generic;
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

        public static float[] FootprintCornerErrorsPx(
            Camera camera,
            SpriteRenderer furnitureRenderer,
            OfficeFurnitureVisualDefinition definition,
            IReadOnlyList<Vector3> expectedFootprintWorld)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (expectedFootprintWorld == null) throw new ArgumentNullException(nameof(expectedFootprintWorld));
            if (definition.GroundFootprintPolygonPx.Count != expectedFootprintWorld.Count)
                throw new ArgumentException("Actual and expected footprint point counts differ.", nameof(expectedFootprintWorld));
            var errors = new float[expectedFootprintWorld.Count];
            for (int index = 0; index < errors.Length; index++)
            {
                errors[index] = ScreenDistance(
                    camera,
                    SpriteAnchorWorld(furnitureRenderer, definition.GroundFootprintPolygonPx[index]),
                    expectedFootprintWorld[index]);
            }
            return errors;
        }

        public static float Maximum(IReadOnlyList<float> values)
        {
            if (values == null || values.Count == 0) return float.PositiveInfinity;
            float maximum = values[0];
            for (int index = 1; index < values.Count; index++) maximum = Mathf.Max(maximum, values[index]);
            return maximum;
        }

        public static float ChairToDeskSeatSocketErrorPx(
            Camera camera,
            SpriteRenderer chairRenderer,
            OfficeFurnitureVisualDefinition chairDefinition,
            SpriteRenderer deskRenderer,
            OfficeFurnitureVisualDefinition deskDefinition)
        {
            return ScreenDistance(
                camera,
                SpriteAnchorWorld(chairRenderer, chairDefinition.SeatAnchorPx),
                SpriteAnchorWorld(deskRenderer, deskDefinition.OperatorSeatSocketPx));
        }

        public static float PelvisToOperatorSocketErrorPx(
            Camera camera,
            SpriteRenderer characterRenderer,
            OfficeCharacterSeatPoseProfile pose,
            SpriteRenderer deskRenderer,
            OfficeFurnitureVisualDefinition deskDefinition)
        {
            return ScreenDistance(
                camera,
                SpriteAnchorWorld(characterRenderer, pose.PelvisAnchorPx),
                SpriteAnchorWorld(deskRenderer, deskDefinition.OperatorSeatSocketPx));
        }

        public static float HandToWorkSocketErrorPx(
            Camera camera,
            SpriteRenderer characterRenderer,
            OfficeCharacterSeatPoseProfile pose,
            SpriteRenderer deskRenderer,
            OfficeFurnitureVisualDefinition deskDefinition)
        {
            return ScreenDistance(
                camera,
                SpriteAnchorWorld(characterRenderer, pose.HandAnchorPx),
                SpriteAnchorWorld(deskRenderer, deskDefinition.OperatorWorkSocketPx));
        }

        public static float WorldDisplacementScreenPx(Camera camera, Vector3 origin, Vector3 displacement)
        {
            return ScreenDistance(camera, origin, origin + displacement);
        }

        public static float VectorAngleDifferenceDegrees(Vector2 first, Vector2 second)
        {
            if (first.sqrMagnitude <= 0.000001f || second.sqrMagnitude <= 0.000001f)
                return float.PositiveInfinity;
            float cosine = Mathf.Clamp(Vector2.Dot(first.normalized, second.normalized), -1f, 1f);
            return Mathf.Acos(cosine) * Mathf.Rad2Deg;
        }

        public static float VectorLengthRelativeError(Vector2 actual, Vector2 expected)
        {
            float expectedLength = expected.magnitude;
            if (expectedLength <= 0.000001f) return float.PositiveInfinity;
            return Mathf.Abs(actual.magnitude - expectedLength) / expectedLength;
        }

        public static float ScreenDistance(Camera camera, Vector3 firstWorld, Vector3 secondWorld)
        {
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            Vector3 first = camera.WorldToScreenPoint(firstWorld);
            Vector3 second = camera.WorldToScreenPoint(secondWorld);
            return Vector2.Distance(new Vector2(first.x, first.y), new Vector2(second.x, second.y));
        }

    }
}
