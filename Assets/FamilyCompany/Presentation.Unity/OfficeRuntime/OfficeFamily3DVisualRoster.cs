using System;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime
{
    /// <summary>
    /// Temporary presentation only: four distinct family agents use two approved 3D packages.
    /// Never rewrite family IDs, roles, schedules, saves or seat ownership to match the stand-in.
    /// Replace the two explicit stand-in mappings when their own approved packages are ready.
    /// </summary>
    public static class OfficeFamily3DVisualRoster
    {
        public const int FamilyCount = 4;

        public static string ModelMemberId(string memberId) => memberId switch
        {
            "player" => "player",
            "older_sister" => "player",
            "father" => "father",
            "mother" => "father",
            _ => string.Empty
        };

        public static bool IsTemporaryStandIn(string memberId) =>
            memberId == "older_sister" || memberId == "mother";

        public static string ProductionName(string memberId) => memberId switch
        {
            "player" => "PlayerV8",
            "older_sister" => "OlderSisterPlayerV8StandIn",
            "father" => "FatherV19",
            "mother" => "MotherFatherV19StandIn",
            _ => throw new ArgumentException("No approved 3D presentation for " + memberId)
        };
    }
}
