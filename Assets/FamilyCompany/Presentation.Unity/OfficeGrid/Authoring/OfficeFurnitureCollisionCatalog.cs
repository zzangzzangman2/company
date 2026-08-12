using System;
using System.Collections.Generic;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeGridView.Authoring
{
    [Serializable]
    public sealed class OfficeFurnitureCollisionProfile
    {
        [SerializeField] private string kindId = string.Empty;
        [SerializeField] private OfficeFurnitureFacing facing = OfficeFurnitureFacing.NorthWest;
        [SerializeField] private int semanticFootprintWidth = 1;
        [SerializeField] private int semanticFootprintHeight = 1;
        [SerializeField] private float clearancePadding;
        [SerializeField] private string[] occupiedRows = Array.Empty<string>();

        public string KindId => kindId;
        public OfficeFurnitureFacing Facing => facing;
        public int SemanticFootprintWidth => semanticFootprintWidth;
        public int SemanticFootprintHeight => semanticFootprintHeight;
        public float ClearancePadding => clearancePadding;
        public IReadOnlyList<string> OccupiedRows => occupiedRows;

        public bool IsOccupied(int subcellX, int subcellY)
        {
            if (subcellX < 0 || subcellY < 0 ||
                subcellX >= semanticFootprintWidth * OfficeFurnitureCollisionCatalog.SubcellsPerCell ||
                subcellY >= semanticFootprintHeight * OfficeFurnitureCollisionCatalog.SubcellsPerCell)
                return false;
            return occupiedRows[subcellY][subcellX] == '#';
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(kindId))
                throw new InvalidOperationException("Furniture collision profile kind id is empty.");
            if (semanticFootprintWidth <= 0 || semanticFootprintHeight <= 0)
                throw new InvalidOperationException(
                    $"Furniture collision profile '{kindId}/{facing}' has an invalid semantic footprint.");
            if (clearancePadding < 0f || float.IsNaN(clearancePadding) || float.IsInfinity(clearancePadding))
                throw new InvalidOperationException(
                    $"Furniture collision profile '{kindId}/{facing}' has invalid clearance padding {clearancePadding}.");

            int expectedWidth = semanticFootprintWidth * OfficeFurnitureCollisionCatalog.SubcellsPerCell;
            int expectedHeight = semanticFootprintHeight * OfficeFurnitureCollisionCatalog.SubcellsPerCell;
            if (occupiedRows == null || occupiedRows.Length != expectedHeight)
                throw new InvalidOperationException(
                    $"Furniture collision profile '{kindId}/{facing}' requires {expectedHeight} authored rows.");
            bool anyOccupied = false;
            for (var rowIndex = 0; rowIndex < occupiedRows.Length; rowIndex++)
            {
                string row = occupiedRows[rowIndex] ?? string.Empty;
                if (row.Length != expectedWidth)
                    throw new InvalidOperationException(
                        $"Furniture collision profile '{kindId}/{facing}' row {rowIndex} requires {expectedWidth} subcells.");
                for (var column = 0; column < row.Length; column++)
                {
                    if (row[column] != '#' && row[column] != '.')
                        throw new InvalidOperationException(
                            $"Furniture collision profile '{kindId}/{facing}' row {rowIndex} contains '{row[column]}'.");
                    anyOccupied |= row[column] == '#';
                }
            }
            if (!anyOccupied)
                throw new InvalidOperationException(
                    $"Furniture collision profile '{kindId}/{facing}' has no occupied subcells.");
        }
    }

    [CreateAssetMenu(menuName = "Family Company/Office/Furniture Collision Catalog")]
    public sealed class OfficeFurnitureCollisionCatalog : ScriptableObject
    {
        public const int CurrentProfileVersion = 1;
        public const int SubcellsPerCell = 4;
        public const string DefaultResourcePath = "OfficeFurnitureCollisionCatalog";

        [SerializeField] private int profileVersion;
        [SerializeField] private OfficeFurnitureCollisionProfile[] profiles =
            Array.Empty<OfficeFurnitureCollisionProfile>();

        public int ProfileVersion => profileVersion;
        public IReadOnlyList<OfficeFurnitureCollisionProfile> Profiles => profiles;

        public bool TryResolve(
            string kindId,
            OfficeFurnitureFacing facing,
            int semanticWidth,
            int semanticHeight,
            out OfficeFurnitureCollisionProfile profile)
        {
            foreach (OfficeFurnitureCollisionProfile candidate in profiles)
            {
                if (candidate == null ||
                    !string.Equals(candidate.KindId, kindId, StringComparison.Ordinal) ||
                    candidate.Facing != facing ||
                    candidate.SemanticFootprintWidth != semanticWidth ||
                    candidate.SemanticFootprintHeight != semanticHeight)
                    continue;
                profile = candidate;
                return true;
            }
            profile = null;
            return false;
        }

        public void Validate()
        {
            if (profileVersion != CurrentProfileVersion)
                throw new InvalidOperationException(
                    $"Furniture collision profile version {profileVersion} is not supported.");
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (OfficeFurnitureCollisionProfile profile in profiles)
            {
                if (profile == null)
                    throw new InvalidOperationException("Furniture collision catalog contains a null profile.");
                profile.Validate();
                string key = $"{profile.KindId}:{(int)profile.Facing}";
                if (!keys.Add(key))
                    throw new InvalidOperationException($"Duplicate furniture collision profile '{key}'.");
            }
        }
    }
}
