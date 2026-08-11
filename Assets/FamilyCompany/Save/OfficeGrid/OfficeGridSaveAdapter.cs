using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Simulation.OfficeLayout;
using OfficeGridState = FamilyCompany.Simulation.OfficeLayout.OfficeGrid;

namespace FamilyCompany.Save.OfficeGrid
{
    public static class OfficeGridSaveAdapter
    {
        public static OfficeGridSaveDto ToDto(OfficeGridState grid)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            return new OfficeGridSaveDto
            {
                width = grid.Width,
                height = grid.Height,
                floorTiles = grid.CopyFloorTiles().Select(item => (int)item).ToList(),
                walkable = grid.CopyWalkable().ToList(),
                furniture = grid.Furniture.Select(item => new PlacedOfficeFurnitureSaveDto
                {
                    furnitureId = item.FurnitureId,
                    kindId = item.KindId,
                    x = item.Origin.X,
                    y = item.Origin.Y,
                    width = item.Width,
                    height = item.Height,
                    facing = (int)item.Facing,
                    blocksMovement = item.BlocksMovement
                }).ToList(),
                seatSlots = grid.SeatSlots.Select(item => new OfficeSeatSlotSaveDto
                {
                    seatId = item.SeatId,
                    furnitureId = item.FurnitureId,
                    workSurfaceFurnitureId = item.WorkSurfaceFurnitureId,
                    x = item.Cell.X,
                    y = item.Cell.Y,
                    approachX = item.ApproachCell.X,
                    approachY = item.ApproachCell.Y,
                    facing = (int)item.Facing
                }).ToList()
            };
        }

        public static OfficeGridState Restore(OfficeGridSaveDto dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            if (dto.schemaVersion != 1 && dto.schemaVersion != OfficeGridSaveDto.CurrentSchemaVersion)
                throw new InvalidOperationException($"Unsupported office grid schema: {dto.schemaVersion}.");
            if (dto.floorTiles == null || dto.walkable == null || dto.furniture == null || dto.seatSlots == null)
                throw new InvalidOperationException("Office grid save data is incomplete.");

            var floor = new OfficeFloorTileKind[dto.floorTiles.Count];
            for (var index = 0; index < floor.Length; index++)
            {
                var value = dto.floorTiles[index];
                if (!Enum.IsDefined(typeof(OfficeFloorTileKind), value))
                    throw new InvalidOperationException($"Unknown office floor tile at index {index}: {value}.");
                floor[index] = (OfficeFloorTileKind)value;
            }

            var furniture = new List<PlacedOfficeFurniture>(dto.furniture.Count);
            foreach (var item in dto.furniture)
            {
                if (item == null) throw new InvalidOperationException("Office grid furniture contains null.");
                if (!Enum.IsDefined(typeof(OfficeFurnitureFacing), item.facing))
                    throw new InvalidOperationException($"Unknown office furniture facing: {item.facing}.");
                furniture.Add(new PlacedOfficeFurniture(
                    item.furnitureId,
                    item.kindId,
                    new OfficeGridCoordinate(item.x, item.y),
                    item.width,
                    item.height,
                    (OfficeFurnitureFacing)item.facing,
                    item.blocksMovement));
            }

            var seats = new List<OfficeSeatSlot>(dto.seatSlots.Count);
            foreach (var item in dto.seatSlots)
            {
                if (item == null) throw new InvalidOperationException("Office grid seat slots contain null.");
                if (!Enum.IsDefined(typeof(OfficeFurnitureFacing), item.facing))
                    throw new InvalidOperationException($"Unknown office seat facing: {item.facing}.");
                var seatCell = new OfficeGridCoordinate(item.x, item.y);
                if (dto.schemaVersion == 1 || string.IsNullOrWhiteSpace(item.workSurfaceFurnitureId))
                {
                    seats.Add(new OfficeSeatSlot(
                        item.seatId,
                        item.furnitureId,
                        seatCell,
                        (OfficeFurnitureFacing)item.facing));
                }
                else
                {
                    seats.Add(new OfficeSeatSlot(
                        item.seatId,
                        item.furnitureId,
                        item.workSurfaceFurnitureId,
                        seatCell,
                        new OfficeGridCoordinate(item.approachX, item.approachY),
                        (OfficeFurnitureFacing)item.facing));
                }
            }

            return new OfficeGridState(dto.width, dto.height, floor, dto.walkable, furniture, seats);
        }
    }
}
