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
        [SerializeField] private Vector2[] groundFootprintPolygonPx = Array.Empty<Vector2>();
        [SerializeField] private int semanticFootprintWidth = 1;
        [SerializeField] private int semanticFootprintHeight = 1;
        [SerializeField] private Vector2 seatAnchorPx;
        [SerializeField] private Vector2 workSurfaceAnchorPx;
        [SerializeField] private Vector2 operatorSeatSocketPx;
        [SerializeField] private float uniformScale = 1f;
        [SerializeField] private bool hasSeatAnchor;
        [SerializeField] private bool hasWorkSurfaceAnchor;
        [SerializeField] private bool hasOperatorSeatSocket;
        [SerializeField] private bool frontOverlayWhenOccupied;

        public string KindId => kindId;
        public OfficeFurnitureFacing Facing => facing;
        public Sprite BaseSprite => baseSprite;
        public Sprite FrontOverlaySprite => frontOverlaySprite;
        public Vector2 GroundAnchorPx => groundAnchorPx;
        public Vector2 SortAnchorPx => sortAnchorPx;
        public IReadOnlyList<Vector2> GroundFootprintPolygonPx => groundFootprintPolygonPx;
        public int SemanticFootprintWidth => semanticFootprintWidth;
        public int SemanticFootprintHeight => semanticFootprintHeight;
        public Vector2 SeatAnchorPx => seatAnchorPx;
        public Vector2 WorkSurfaceAnchorPx => workSurfaceAnchorPx;
        public Vector2 OperatorWorkSocketPx => workSurfaceAnchorPx;
        public Vector2 OperatorSeatSocketPx => operatorSeatSocketPx;
        public float UniformScale => uniformScale;
        public bool HasSeatAnchor => hasSeatAnchor;
        public bool HasWorkSurfaceAnchor => hasWorkSurfaceAnchor;
        public bool HasOperatorWorkSocket => hasWorkSurfaceAnchor;
        public bool HasOperatorSeatSocket => hasOperatorSeatSocket;
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
            bool frontOverlayWhenOccupied,
            Vector2[] groundFootprintPolygonPx,
            int semanticFootprintWidth,
            int semanticFootprintHeight,
            Vector2 operatorSeatSocketPx,
            bool hasOperatorSeatSocket)
        {
            return new OfficeFurnitureVisualDefinition
            {
                kindId = kindId ?? string.Empty,
                facing = facing,
                baseSprite = baseSprite,
                frontOverlaySprite = frontOverlaySprite,
                groundAnchorPx = groundAnchorPx,
                sortAnchorPx = sortAnchorPx,
                groundFootprintPolygonPx = groundFootprintPolygonPx == null
                    ? Array.Empty<Vector2>()
                    : (Vector2[])groundFootprintPolygonPx.Clone(),
                semanticFootprintWidth = semanticFootprintWidth,
                semanticFootprintHeight = semanticFootprintHeight,
                seatAnchorPx = seatAnchorPx,
                workSurfaceAnchorPx = workSurfaceAnchorPx,
                operatorSeatSocketPx = operatorSeatSocketPx,
                uniformScale = uniformScale,
                hasSeatAnchor = hasSeatAnchor,
                hasWorkSurfaceAnchor = hasWorkSurfaceAnchor,
                hasOperatorSeatSocket = hasOperatorSeatSocket,
                frontOverlayWhenOccupied = frontOverlayWhenOccupied
            };
        }

        public void ApplyCalibration(
            Vector2 newGroundAnchorPx,
            Vector2 newSortAnchorPx,
            Vector2[] newGroundFootprintPolygonPx,
            Vector2 newSeatAnchorPx,
            Vector2 newOperatorSeatSocketPx,
            Vector2 newOperatorWorkSocketPx,
            float newUniformScale)
        {
            groundAnchorPx = newGroundAnchorPx;
            sortAnchorPx = newSortAnchorPx;
            groundFootprintPolygonPx = newGroundFootprintPolygonPx == null
                ? Array.Empty<Vector2>()
                : (Vector2[])newGroundFootprintPolygonPx.Clone();
            if (hasSeatAnchor) seatAnchorPx = newSeatAnchorPx;
            if (hasOperatorSeatSocket) operatorSeatSocketPx = newOperatorSeatSocketPx;
            if (hasWorkSurfaceAnchor) workSurfaceAnchorPx = newOperatorWorkSocketPx;
            uniformScale = newUniformScale;
            Validate();
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
            if (semanticFootprintWidth <= 0 || semanticFootprintHeight <= 0)
                throw new InvalidOperationException($"Furniture visual '{kindId}/{facing}' has an invalid semantic footprint.");
            if (groundFootprintPolygonPx == null || groundFootprintPolygonPx.Length != 4)
                throw new InvalidOperationException($"Furniture visual '{kindId}/{facing}' requires four ground footprint points.");
            float signedAreaTwice = 0f;
            for (int index = 0; index < groundFootprintPolygonPx.Length; index++)
            {
                Vector2 point = groundFootprintPolygonPx[index];
                Vector2 next = groundFootprintPolygonPx[(index + 1) % groundFootprintPolygonPx.Length];
                ValidateFinite(point, "ground footprint");
                signedAreaTwice += point.x * next.y - next.x * point.y;
            }
            if (Mathf.Abs(signedAreaTwice) < 0.01f)
                throw new InvalidOperationException($"Furniture visual '{kindId}/{facing}' ground footprint winding is degenerate.");
            if (hasSeatAnchor)
            {
                ValidateAnchor(seatAnchorPx, nameof(seatAnchorPx));
            }

            if (hasWorkSurfaceAnchor)
            {
                ValidateAnchor(workSurfaceAnchorPx, nameof(workSurfaceAnchorPx));
            }

            if (hasOperatorSeatSocket)
            {
                ValidateAnchor(operatorSeatSocketPx, nameof(operatorSeatSocketPx));
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
            ValidateFinite(anchor, anchorName);
            Rect rect = baseSprite.rect;
            if (anchor.x < 0f || anchor.y < 0f || anchor.x > rect.width || anchor.y > rect.height)
            {
                throw new InvalidOperationException(
                    $"Furniture visual '{kindId}/{facing}' {anchorName} {anchor} is outside {rect.size}.");
            }
        }

        private void ValidateFinite(Vector2 point, string pointName)
        {
            if (float.IsNaN(point.x) || float.IsNaN(point.y) ||
                float.IsInfinity(point.x) || float.IsInfinity(point.y))
                throw new InvalidOperationException($"Furniture visual '{kindId}/{facing}' {pointName} is not finite.");
        }
    }

    [CreateAssetMenu(menuName = "Family Company/Office/Furniture Visual Catalog")]
    public sealed class OfficeFurnitureVisualCatalog : ScriptableObject
    {
        public const int CurrentCalibrationVersion = 2;

        [SerializeField] private int calibrationVersion;
        [SerializeField] private OfficeFurnitureVisualDefinition[] definitions = Array.Empty<OfficeFurnitureVisualDefinition>();

        public int CalibrationVersion => calibrationVersion;
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

        /// <summary>
        /// Resolves a facing that may only exist as the horizontal mirror of an authored one.
        /// Isometric SouthEast mirrors to SouthWest and NorthWest to NorthEast, so flipping the
        /// sprite on X turns a piece to face the other way without inventing pixels. Facings that
        /// are neither authored nor a mirror return false - the caller must refuse rather than draw
        /// the wrong side.
        /// </summary>
        public bool TryResolveWithMirror(
            string kindId,
            OfficeFurnitureFacing facing,
            out OfficeFurnitureVisualDefinition definition,
            out bool flipX)
        {
            flipX = false;
            definition = null;
            foreach (OfficeFurnitureVisualDefinition candidate in definitions)
            {
                if (candidate == null || !string.Equals(candidate.KindId, kindId, StringComparison.Ordinal))
                    continue;
                if (candidate.Facing == facing)
                {
                    definition = candidate;
                    flipX = false;
                    return true;
                }
                if (MirrorOf(candidate.Facing) == facing)
                {
                    definition = candidate;
                    flipX = true;
                }
            }
            return definition != null;
        }

        public static OfficeFurnitureFacing MirrorOf(OfficeFurnitureFacing facing)
        {
            switch (facing)
            {
                case OfficeFurnitureFacing.SouthEast: return OfficeFurnitureFacing.SouthWest;
                case OfficeFurnitureFacing.SouthWest: return OfficeFurnitureFacing.SouthEast;
                case OfficeFurnitureFacing.NorthWest: return OfficeFurnitureFacing.NorthEast;
                default: return OfficeFurnitureFacing.NorthWest;
            }
        }

        public void ReplaceDefinitions(OfficeFurnitureVisualDefinition[] values, int newCalibrationVersion)
        {
            definitions = values ?? Array.Empty<OfficeFurnitureVisualDefinition>();
            calibrationVersion = newCalibrationVersion;
            Validate();
        }

        public void Validate()
        {
            if (calibrationVersion != CurrentCalibrationVersion)
                throw new InvalidOperationException($"Furniture visual calibration version {calibrationVersion} is not supported.");
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
