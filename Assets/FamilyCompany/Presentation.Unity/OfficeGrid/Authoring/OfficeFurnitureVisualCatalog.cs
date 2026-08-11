using System;
using System.Collections.Generic;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeGridView.Authoring
{
    [Serializable]
    public sealed class OfficeFurnitureVisualDefinition
    {
        [SerializeField] private string kindId = string.Empty;
        [SerializeField] private OfficeFurnitureFacing facing = OfficeFurnitureFacing.NorthWest;
        [SerializeField] private Sprite baseSprite;
        [SerializeField] private Sprite frontOverlaySprite;
        [SerializeField] private Vector2 groundAnchorPx;
        [SerializeField] private Vector2 sortAnchorPx;
        [SerializeField] private Vector2 seatAnchorPx;
        [SerializeField] private Vector2 workSurfaceAnchorPx;
        [SerializeField] private float uniformScale = 1f;
        [SerializeField] private bool hasSeatAnchor;
        [SerializeField] private bool hasWorkSurfaceAnchor;
        [SerializeField] private bool frontOverlayWhenOccupied;

        public string KindId => kindId;
        public OfficeFurnitureFacing Facing => facing;
        public Sprite BaseSprite => baseSprite;
        public Sprite FrontOverlaySprite => frontOverlaySprite;
        public Vector2 GroundAnchorPx => groundAnchorPx;
        public Vector2 SortAnchorPx => sortAnchorPx;
        public Vector2 SeatAnchorPx => seatAnchorPx;
        public Vector2 WorkSurfaceAnchorPx => workSurfaceAnchorPx;
        public float UniformScale => uniformScale;
        public bool HasSeatAnchor => hasSeatAnchor;
        public bool HasWorkSurfaceAnchor => hasWorkSurfaceAnchor;
        public bool FrontOverlayWhenOccupied => frontOverlayWhenOccupied;

        public static OfficeFurnitureVisualDefinition Create(
            string kindId,
            OfficeFurnitureFacing facing,
            Sprite baseSprite,
            Sprite frontOverlaySprite,
            Vector2 groundAnchorPx,
            Vector2 sortAnchorPx,
            Vector2 seatAnchorPx,
            Vector2 workSurfaceAnchorPx,
            float uniformScale,
            bool hasSeatAnchor,
            bool hasWorkSurfaceAnchor,
            bool frontOverlayWhenOccupied)
        {
            return new OfficeFurnitureVisualDefinition
            {
                kindId = kindId ?? string.Empty,
                facing = facing,
                baseSprite = baseSprite,
                frontOverlaySprite = frontOverlaySprite,
                groundAnchorPx = groundAnchorPx,
                sortAnchorPx = sortAnchorPx,
                seatAnchorPx = seatAnchorPx,
                workSurfaceAnchorPx = workSurfaceAnchorPx,
                uniformScale = uniformScale,
                hasSeatAnchor = hasSeatAnchor,
                hasWorkSurfaceAnchor = hasWorkSurfaceAnchor,
                frontOverlayWhenOccupied = frontOverlayWhenOccupied
            };
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(kindId))
            {
                throw new InvalidOperationException("Furniture visual kind id is empty.");
            }

            if (baseSprite == null)
            {
                throw new InvalidOperationException($"Furniture visual '{kindId}/{facing}' has no base sprite.");
            }

            if (uniformScale <= 0f || float.IsNaN(uniformScale) || float.IsInfinity(uniformScale))
            {
                throw new InvalidOperationException($"Furniture visual '{kindId}/{facing}' has invalid uniform scale {uniformScale}.");
            }

            ValidateAnchor(groundAnchorPx, nameof(groundAnchorPx));
            ValidateAnchor(sortAnchorPx, nameof(sortAnchorPx));
            if (hasSeatAnchor)
            {
                ValidateAnchor(seatAnchorPx, nameof(seatAnchorPx));
            }

            if (hasWorkSurfaceAnchor)
            {
                ValidateAnchor(workSurfaceAnchorPx, nameof(workSurfaceAnchorPx));
            }

            if (frontOverlaySprite != null)
            {
                if (frontOverlaySprite.rect.size != baseSprite.rect.size ||
                    !Mathf.Approximately(frontOverlaySprite.pixelsPerUnit, baseSprite.pixelsPerUnit) ||
                    frontOverlaySprite.pivot != baseSprite.pivot)
                {
                    throw new InvalidOperationException(
                        $"Furniture visual '{kindId}/{facing}' overlay must share base sprite size, PPU, and pivot.");
                }
            }
        }

        private void ValidateAnchor(Vector2 anchor, string anchorName)
        {
            Rect rect = baseSprite.rect;
            if (anchor.x < 0f || anchor.y < 0f || anchor.x > rect.width || anchor.y > rect.height)
            {
                throw new InvalidOperationException(
                    $"Furniture visual '{kindId}/{facing}' {anchorName} {anchor} is outside {rect.size}.");
            }
        }
    }

    [CreateAssetMenu(menuName = "Family Company/Office/Furniture Visual Catalog")]
    public sealed class OfficeFurnitureVisualCatalog : ScriptableObject
    {
        [SerializeField] private OfficeFurnitureVisualDefinition[] definitions = Array.Empty<OfficeFurnitureVisualDefinition>();

        public IReadOnlyList<OfficeFurnitureVisualDefinition> Definitions => definitions;

        public OfficeFurnitureVisualDefinition Resolve(string kindId, OfficeFurnitureFacing facing)
        {
            foreach (OfficeFurnitureVisualDefinition definition in definitions)
            {
                if (definition != null &&
                    string.Equals(definition.KindId, kindId, StringComparison.Ordinal) &&
                    definition.Facing == facing)
                {
                    return definition;
                }
            }

            throw new KeyNotFoundException($"Furniture visual '{kindId}/{facing}' is not registered.");
        }

        public void ReplaceDefinitions(OfficeFurnitureVisualDefinition[] values)
        {
            definitions = values ?? Array.Empty<OfficeFurnitureVisualDefinition>();
            Validate();
        }

        public void Validate()
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (OfficeFurnitureVisualDefinition definition in definitions)
            {
                if (definition == null)
                {
                    throw new InvalidOperationException("Furniture visual catalog contains a null definition.");
                }

                definition.Validate();
                string key = $"{definition.KindId}:{(int)definition.Facing}";
                if (!keys.Add(key))
                {
                    throw new InvalidOperationException($"Duplicate furniture visual definition '{key}'.");
                }
            }
        }
    }
}
