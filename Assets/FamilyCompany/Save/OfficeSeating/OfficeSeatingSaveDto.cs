using System;
using System.Collections.Generic;

namespace FamilyCompany.Save.OfficeSeating
{
    /// <summary>
    /// Persistent office-seat ownership only. Runtime reservations, occupancy tokens,
    /// transforms, animation state and UI state intentionally do not belong here.
    /// </summary>
    [Serializable]
    public sealed class OfficeSeatingSaveDto
    {
        public const int CurrentSchemaVersion = 1;
        public const string FutureGameSaveFieldName = "officeSeating";

        public int schemaVersion = CurrentSchemaVersion;
        public List<OfficeSeatAssignmentSaveDto> seatAssignments =
            new List<OfficeSeatAssignmentSaveDto>();
    }

    [Serializable]
    public sealed class OfficeSeatAssignmentSaveDto
    {
        public string memberId = string.Empty;
        public string seatId = string.Empty;
    }
}
