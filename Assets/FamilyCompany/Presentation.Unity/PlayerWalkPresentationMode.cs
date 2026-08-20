using System;

namespace FamilyCompany.Presentation.Unity
{
    public enum PlayerWalkPresentationMode
    {
        Legacy48 = 0,
        NaturalV1 = 1,
        BakedV2 = 2,
        Player2DV2 = 3
    }

    /// <summary>
    /// Fail-safe selection for the player-only walk override. The normal game stays on the
    /// established 48-frame catalog until a baked candidate is explicitly requested.
    /// </summary>
    public static class PlayerWalkPresentationModeResolver
    {
        public const string NaturalV1Flag = "-familyCompanyPlayerNaturalWalkV1";
        public const string BakedV2Flag = "-familyCompanyPlayerBakedWalkV2";
        public const string Player2DV2Flag = "-familyCompanyPlayer2DWalkV2";

        public static PlayerWalkPresentationMode Resolve(string[] arguments = null)
        {
            string[] values = arguments ?? Environment.GetCommandLineArgs();
            bool natural = Has(values, NaturalV1Flag);
            bool baked = Has(values, BakedV2Flag);
            bool player2D = Has(values, Player2DV2Flag);
            int requested = (natural ? 1 : 0) + (baked ? 1 : 0) + (player2D ? 1 : 0);
            if (requested > 1)
                throw new InvalidOperationException(
                    "Player walk overrides are mutually exclusive.");
            if (player2D) return PlayerWalkPresentationMode.Player2DV2;
            if (baked) return PlayerWalkPresentationMode.BakedV2;
            if (natural) return PlayerWalkPresentationMode.NaturalV1;
            return PlayerWalkPresentationMode.Legacy48;
        }

        private static bool Has(string[] arguments, string expected)
        {
            if (arguments == null) return false;
            foreach (string argument in arguments)
                if (string.Equals(argument, expected, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
    }
}
