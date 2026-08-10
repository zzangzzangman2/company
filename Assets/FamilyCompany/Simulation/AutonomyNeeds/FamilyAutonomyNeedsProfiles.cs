using System;
using System.Collections.Generic;
using System.Linq;

namespace FamilyCompany.Simulation.AutonomyNeeds
{
    public sealed class FamilyAutonomyNeedsProfile
    {
        public FamilyAutonomyNeedsProfile(
            string memberId,
            int initialEnergyBasisPoints,
            int initialStressBasisPoints,
            int initialFocusBasisPoints,
            int energyDrainMultiplierBasisPoints,
            int stressGainMultiplierBasisPoints,
            int focusDrainMultiplierBasisPoints,
            int recoveryMultiplierBasisPoints,
            int breakEnergyBasisPoints,
            int breakStressBasisPoints,
            int breakFocusBasisPoints,
            int resumeEnergyBasisPoints,
            int resumeStressBasisPoints,
            int resumeFocusBasisPoints,
            int minimumBreakMinutes,
            int breakCooldownMinutes,
            int absenceMinutes,
            int loungeWeight,
            int waterWeight,
            int stretchWeight)
        {
            if (string.IsNullOrWhiteSpace(memberId))
                throw new ArgumentException("Member ID is required.", nameof(memberId));
            ValidateBasisPoints(initialEnergyBasisPoints, nameof(initialEnergyBasisPoints));
            ValidateBasisPoints(initialStressBasisPoints, nameof(initialStressBasisPoints));
            ValidateBasisPoints(initialFocusBasisPoints, nameof(initialFocusBasisPoints));
            ValidatePositiveMultiplier(energyDrainMultiplierBasisPoints, nameof(energyDrainMultiplierBasisPoints));
            ValidatePositiveMultiplier(stressGainMultiplierBasisPoints, nameof(stressGainMultiplierBasisPoints));
            ValidatePositiveMultiplier(focusDrainMultiplierBasisPoints, nameof(focusDrainMultiplierBasisPoints));
            ValidatePositiveMultiplier(recoveryMultiplierBasisPoints, nameof(recoveryMultiplierBasisPoints));
            ValidateBasisPoints(breakEnergyBasisPoints, nameof(breakEnergyBasisPoints));
            ValidateBasisPoints(breakStressBasisPoints, nameof(breakStressBasisPoints));
            ValidateBasisPoints(breakFocusBasisPoints, nameof(breakFocusBasisPoints));
            ValidateBasisPoints(resumeEnergyBasisPoints, nameof(resumeEnergyBasisPoints));
            ValidateBasisPoints(resumeStressBasisPoints, nameof(resumeStressBasisPoints));
            ValidateBasisPoints(resumeFocusBasisPoints, nameof(resumeFocusBasisPoints));
            if (resumeEnergyBasisPoints <= breakEnergyBasisPoints)
                throw new ArgumentException("Resume energy must exceed break-request energy.", nameof(resumeEnergyBasisPoints));
            if (resumeStressBasisPoints >= breakStressBasisPoints)
                throw new ArgumentException("Resume stress must be below break-request stress.", nameof(resumeStressBasisPoints));
            if (resumeFocusBasisPoints <= breakFocusBasisPoints)
                throw new ArgumentException("Resume focus must exceed break-request focus.", nameof(resumeFocusBasisPoints));
            if (minimumBreakMinutes <= 0) throw new ArgumentOutOfRangeException(nameof(minimumBreakMinutes));
            if (breakCooldownMinutes <= 0) throw new ArgumentOutOfRangeException(nameof(breakCooldownMinutes));
            if (absenceMinutes <= 0) throw new ArgumentOutOfRangeException(nameof(absenceMinutes));
            if (loungeWeight < 0 || waterWeight < 0 || stretchWeight < 0 ||
                loungeWeight + waterWeight + stretchWeight != 100)
                throw new ArgumentException("Recovery activity weights must be non-negative and total 100.");

            MemberId = memberId;
            InitialEnergyBasisPoints = initialEnergyBasisPoints;
            InitialStressBasisPoints = initialStressBasisPoints;
            InitialFocusBasisPoints = initialFocusBasisPoints;
            EnergyDrainMultiplierBasisPoints = energyDrainMultiplierBasisPoints;
            StressGainMultiplierBasisPoints = stressGainMultiplierBasisPoints;
            FocusDrainMultiplierBasisPoints = focusDrainMultiplierBasisPoints;
            RecoveryMultiplierBasisPoints = recoveryMultiplierBasisPoints;
            BreakEnergyBasisPoints = breakEnergyBasisPoints;
            BreakStressBasisPoints = breakStressBasisPoints;
            BreakFocusBasisPoints = breakFocusBasisPoints;
            ResumeEnergyBasisPoints = resumeEnergyBasisPoints;
            ResumeStressBasisPoints = resumeStressBasisPoints;
            ResumeFocusBasisPoints = resumeFocusBasisPoints;
            MinimumBreakMinutes = minimumBreakMinutes;
            BreakCooldownMinutes = breakCooldownMinutes;
            AbsenceMinutes = absenceMinutes;
            LoungeWeight = loungeWeight;
            WaterWeight = waterWeight;
            StretchWeight = stretchWeight;
        }

        public string MemberId { get; }
        public int InitialEnergyBasisPoints { get; }
        public int InitialStressBasisPoints { get; }
        public int InitialFocusBasisPoints { get; }
        public int EnergyDrainMultiplierBasisPoints { get; }
        public int StressGainMultiplierBasisPoints { get; }
        public int FocusDrainMultiplierBasisPoints { get; }
        public int RecoveryMultiplierBasisPoints { get; }
        public int BreakEnergyBasisPoints { get; }
        public int BreakStressBasisPoints { get; }
        public int BreakFocusBasisPoints { get; }
        public int ResumeEnergyBasisPoints { get; }
        public int ResumeStressBasisPoints { get; }
        public int ResumeFocusBasisPoints { get; }
        public int MinimumBreakMinutes { get; }
        public int BreakCooldownMinutes { get; }
        public int AbsenceMinutes { get; }
        public int LoungeWeight { get; }
        public int WaterWeight { get; }
        public int StretchWeight { get; }

        private static void ValidateBasisPoints(int value, string parameterName)
        {
            if (value < 0 || value > AutonomyNeedsRules.BasisPointDenominator)
                throw new ArgumentOutOfRangeException(parameterName);
        }

        private static void ValidatePositiveMultiplier(int value, string parameterName)
        {
            if (value <= 0 || value > 20_000) throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    public static class FamilyAutonomyNeedsProfileCatalog
    {
        public const string PlayerId = "player";
        public const string OlderSisterId = "older_sister";
        public const string FatherId = "father";
        public const string MotherId = "mother";

        private static readonly FamilyAutonomyNeedsProfile[] Profiles =
        {
            new FamilyAutonomyNeedsProfile(
                PlayerId, 10_000, 500, 8_200,
                9_800, 11_000, 11_200, 10_500,
                4_000, 6_500, 3_500,
                6_700, 4_000, 6_200,
                12, 45, 240,
                45, 30, 25),
            new FamilyAutonomyNeedsProfile(
                OlderSisterId, 9_000, 800, 8_000,
                10_000, 10_400, 9_200, 10_200,
                3_700, 6_800, 3_200,
                6_300, 4_300, 5_800,
                10, 40, 210,
                35, 30, 35),
            new FamilyAutonomyNeedsProfile(
                FatherId, 8_500, 1_000, 7_600,
                8_800, 9_000, 9_800, 9_500,
                3_200, 7_400, 3_000,
                5_800, 4_800, 5_400,
                15, 50, 300,
                50, 15, 35),
            new FamilyAutonomyNeedsProfile(
                MotherId, 8_800, 900, 7_900,
                9_300, 8_500, 9_000, 10_000,
                3_600, 7_000, 3_300,
                6_200, 4_400, 5_700,
                12, 45, 240,
                45, 20, 35)
        };

        private static readonly IReadOnlyDictionary<string, FamilyAutonomyNeedsProfile> ById =
            Profiles.ToDictionary(item => item.MemberId, StringComparer.Ordinal);

        public static IReadOnlyList<FamilyAutonomyNeedsProfile> All => Profiles;

        public static FamilyAutonomyNeedsProfile Get(string memberId)
        {
            if (string.IsNullOrWhiteSpace(memberId))
                throw new ArgumentException("Member ID is required.", nameof(memberId));
            if (!ById.TryGetValue(memberId, out var profile))
                throw new KeyNotFoundException($"Unknown family autonomy-needs profile: {memberId}");
            return profile;
        }
    }
}
