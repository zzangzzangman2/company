using System;
using System.Collections.Generic;

namespace FamilyCompany.Save.OfficeGrid
{
    [Serializable]
    public sealed class OfficeGridSaveDto
    {
        public const int CurrentSchemaVersion = 3;

        public int schemaVersion = CurrentSchemaVersion;
        public int width;
        public int height;
        public List<int> floorTiles = new List<int>();
        public List<bool> walkable = new List<bool>();
        public List<PlacedOfficeFurnitureSaveDto> furniture = new List<PlacedOfficeFurnitureSaveDto>();
        public List<OfficeSeatSlotSaveDto> seatSlots = new List<OfficeSeatSlotSaveDto>();
    }

    [Serializable]
    public sealed class PlacedOfficeFurnitureSaveDto
    {
        public string furnitureId = string.Empty;
        public string kindId = string.Empty;
        public int x;
        public int y;
        public int width;
        public int height;
        public int facing;
        public bool blocksMovement;
    }

    [Serializable]
    public sealed class OfficeSeatSlotSaveDto
    {
        public string seatId = string.Empty;
        public string furnitureId = string.Empty;
        public string workSurfaceFurnitureId = string.Empty;
        public int x;
        public int y;
        public int approachX;
        public int approachY;
        public int operatorX2;
        public int operatorY2;
        public int facing;
    }
}
