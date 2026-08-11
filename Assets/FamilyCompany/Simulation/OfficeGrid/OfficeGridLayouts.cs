using System;

namespace FamilyCompany.Simulation.OfficeLayout
{
    public static class OfficeGridLayouts
    {
        public const int MigrationPreviewWidth = 13;
        public const int MigrationPreviewHeight = 13;

        public static OfficeGrid CreateMigrationPreview()
        {
            var width = MigrationPreviewWidth;
            var height = MigrationPreviewHeight;
            var floor = new OfficeFloorTileKind[width * height];
            var walkable = new bool[floor.Length];
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var index = y * width + x;
                floor[index] = (OfficeFloorTileKind)(1 + PositiveModulo(x * 3 + y * 5, 3));
                walkable[index] = x > 0 && x < width - 1 && y > 0 && y < height - 1;
            }

            // A small service-core footprint proves that presentation movement cannot enter
            // a semantic blocked cell before furniture is migrated in T4.
            SetWalkable(walkable, width, 6, 6, false);
            SetWalkable(walkable, width, 6, 7, false);
            return new OfficeGrid(width, height, floor, walkable);
        }

        private static void SetWalkable(bool[] walkable, int width, int x, int y, bool value)
        {
            walkable[checked(y * width + x)] = value;
        }

        private static int PositiveModulo(int value, int divisor)
        {
            var remainder = value % divisor;
            return remainder < 0 ? remainder + divisor : remainder;
        }
    }
}
