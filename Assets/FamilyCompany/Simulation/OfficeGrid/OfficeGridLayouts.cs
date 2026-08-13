using System;
using System.Collections.Generic;

namespace FamilyCompany.Simulation.OfficeLayout
{
    public static class OfficeGridLayouts
    {
        public const int MigrationPreviewWidth = 13;
        public const int MigrationPreviewHeight = 13;
        public const int StarterOfficeWidth = 13;
        public const int StarterOfficeHeight = 13;

        public const string DeskWithPcKind = "desk_with_pc";
        public const string SwivelChairKind = "swivel_chair";
        public const string ReceptionCounterKind = "reception_counter";
        public const string MeetingTableKind = "meeting_table";
        public const string DocumentBookcaseKind = "document_bookcase";
        public const string FaxCopierKind = "fax_copier";
        public const string WaterDispenserKind = "water_dispenser";
        public const string SofaKind = "sofa";
        public const string CoffeeTableKind = "coffee_table";
        public const string PottedPlantKind = "potted_plant";
        public const string PartitionKind = "partition";
        public const string FilingCabinetKind = "filing_cabinet";
        public const string EntranceDoorKind = "entrance_door";
        public const string EntranceWallKind = "entrance_wall";

        public static OfficeGrid CreateMigrationPreview()
        {
            return CreateOfficeLayout(includeMigrationPartition: true);
        }

        public static OfficeGrid CreateStarterOfficeV1()
        {
            return CreateOfficeLayout(includeMigrationPartition: false);
        }

        private static OfficeGrid CreateOfficeLayout(bool includeMigrationPartition)
        {
            var width = StarterOfficeWidth;
            var height = StarterOfficeHeight;
            var floor = new OfficeFloorTileKind[width * height];
            var walkable = new bool[floor.Length];
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var index = y * width + x;
                floor[index] = (OfficeFloorTileKind)(1 + PositiveModulo(x * 3 + y * 5, 3));
                walkable[index] = x > 0 && x < width - 1 && y > 0 && y < height - 1;
            }
            // Remove the floor directly outside the entrance facade. This leaves the first
            // walkable interior cell at (8,1) and makes the wall/door read as the room boundary.
            floor[7] = OfficeFloorTileKind.Void;
            floor[8] = OfficeFloorTileKind.Void;
            floor[9] = OfficeFloorTileKind.Void;

            var furniture = new List<PlacedOfficeFurniture>();
            var seats = new List<OfficeSeatSlot>();
            AddWorkstation(furniture, seats, "player", 2, 4, 2, 3);
            AddWorkstation(furniture, seats, "older_sister", 7, 4, 7, 3);
            AddWorkstation(furniture, seats, "father", 2, 8, 2, 7);
            AddWorkstation(furniture, seats, "mother", 7, 8, 7, 7);

            AddBlocking(furniture, "reception", ReceptionCounterKind, 4, 1, 2, 1, OfficeFurnitureFacing.SouthEast);
            AddBlocking(furniture, "meeting", MeetingTableKind, 4, 10, 2, 1, OfficeFurnitureFacing.SouthEast);
            AddBlocking(furniture, "bookcase", DocumentBookcaseKind, 1, 10, 1, 1, OfficeFurnitureFacing.SouthEast);
            AddBlocking(furniture, "copier", FaxCopierKind, 10, 2, 1, 1, OfficeFurnitureFacing.SouthEast);
            AddBlocking(furniture, "water", WaterDispenserKind, 11, 5, 1, 1, OfficeFurnitureFacing.SouthEast);
            AddBlocking(furniture, "sofa", SofaKind, 9, 10, 2, 1, OfficeFurnitureFacing.SouthEast);
            AddBlocking(furniture, "coffee", CoffeeTableKind, 9, 8, 2, 1, OfficeFurnitureFacing.SouthEast);
            AddBlocking(furniture, "plant", PottedPlantKind, 11, 10, 1, 1, OfficeFurnitureFacing.SouthEast);
            if (includeMigrationPartition)
                AddBlocking(furniture, "partition", PartitionKind, 6, 6, 1, 2, OfficeFurnitureFacing.NorthWest);
            AddBlocking(furniture, "filing", FilingCabinetKind, 11, 8, 1, 1, OfficeFurnitureFacing.SouthEast);
            AddEntranceFacade(furniture);

            foreach (var item in furniture)
            {
                if (!item.BlocksMovement) continue;
                for (var itemY = item.Origin.Y; itemY < item.Origin.Y + item.Height; itemY++)
                for (var itemX = item.Origin.X; itemX < item.Origin.X + item.Width; itemX++)
                    SetWalkable(walkable, width, itemX, itemY, false);
            }

            return new OfficeGrid(width, height, floor, walkable, furniture, seats);
        }

        private static void AddEntranceFacade(ICollection<PlacedOfficeFurniture> furniture)
        {
            // The visual facade sits on the non-walkable outer boundary. Actors appear on the
            // first interior cell (8,1), directly behind the opening, so the doorway reads as an
            // exterior entrance instead of an isolated prop standing in the middle of the floor.
            furniture.Add(new PlacedOfficeFurniture(
                "entrance_wall_left",
                EntranceWallKind,
                new OfficeGridCoordinate(7, 0),
                1,
                1,
                OfficeFurnitureFacing.SouthEast,
                false));
            furniture.Add(new PlacedOfficeFurniture(
                "entrance_door",
                EntranceDoorKind,
                new OfficeGridCoordinate(8, 0),
                1,
                1,
                OfficeFurnitureFacing.SouthEast,
                false));
            furniture.Add(new PlacedOfficeFurniture(
                "entrance_wall_right",
                EntranceWallKind,
                new OfficeGridCoordinate(9, 0),
                1,
                1,
                OfficeFurnitureFacing.SouthEast,
                false));
        }

        private static void AddWorkstation(
            ICollection<PlacedOfficeFurniture> furniture,
            ICollection<OfficeSeatSlot> seats,
            string memberId,
            int deskX,
            int deskY,
            int chairX,
            int chairY)
        {
            var deskId = "desk_" + memberId;
            var chairId = "chair_" + memberId;
            var seatId = "seat_" + memberId;
            furniture.Add(new PlacedOfficeFurniture(
                deskId,
                DeskWithPcKind,
                new OfficeGridCoordinate(deskX, deskY),
                2,
                1,
                OfficeFurnitureFacing.SouthEast,
                true));
            furniture.Add(new PlacedOfficeFurniture(
                chairId,
                SwivelChairKind,
                new OfficeGridCoordinate(chairX, chairY),
                1,
                1,
                OfficeGridSubcellAnchor.FromCellCenter(new OfficeGridCoordinate(chairX, chairY)),
                OfficeFurnitureFacing.NorthWest,
                false));
            seats.Add(new OfficeSeatSlot(
                seatId,
                chairId,
                deskId,
                new OfficeGridCoordinate(chairX, chairY),
                new OfficeGridCoordinate(chairX, chairY - 1),
                new OfficeGridSubcellAnchor(checked(chairX * 2 + 1), checked(chairY * 2 + 1)),
                OfficeFurnitureFacing.NorthWest));
        }

        private static void AddBlocking(
            ICollection<PlacedOfficeFurniture> furniture,
            string furnitureId,
            string kindId,
            int x,
            int y,
            int width,
            int height,
            OfficeFurnitureFacing facing)
        {
            furniture.Add(new PlacedOfficeFurniture(
                furnitureId,
                kindId,
                new OfficeGridCoordinate(x, y),
                width,
                height,
                facing,
                true));
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
