using System;
using System.Collections.Generic;

namespace FamilyCompany.Save.OfficeFurniture
{
    [Serializable]
    public sealed class OfficeFurnitureInventorySaveDto
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public List<OfficeFurnitureInstanceSaveDto> instances = new List<OfficeFurnitureInstanceSaveDto>();
    }

    [Serializable]
    public sealed class OfficeFurnitureInstanceSaveDto
    {
        public string instanceId = string.Empty;
        public string definitionId = string.Empty;
        public int placementState;
        public int gridX;
        public int gridY;
        public int rotation;
        public int purchaseBasisState;
        public long purchaseBasisWon;
        public long acquiredMinute;
        public string purchaseTransactionId = string.Empty;
    }
}
