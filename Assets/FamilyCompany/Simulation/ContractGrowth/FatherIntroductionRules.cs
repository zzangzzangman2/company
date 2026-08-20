using System;

namespace FamilyCompany.Simulation.ContractGrowth
{
    /// <summary>
    /// The father's friend works at 다음커뮤니케이션, a mid-size internet company. In 2000 it ran
    /// 한메일 and 다음카페 and farmed out exactly the piecework the family can already do: avatar
    /// dots, emoticon typing, board and word databases. That is why the introduction is worth
    /// something on day one while the company still has no technology.
    ///
    /// The connection is deliberately not a conglomerate. A national enterprise handing work to a
    /// four-person workshop would contradict the opening premise, which is that low technology keeps
    /// the big jobs out of reach. A mid-size client is the rung the family can actually stand on.
    /// In this game's client mapping a midsize company reachable in 2000 lands on
    /// <see cref="ContractClientTier.T3PrimeVendor"/>, so that is the tier the introduction reaches
    /// and the national tier stays fully earned.
    ///
    /// It only ever discounts the finished-contract count. Reputation, quality, on-time rate, domain
    /// hours and the company grade are untouched, because an introduction gets the meeting, not the
    /// contract: the family still has to have done work of that standard.
    /// </summary>
    public static class FatherIntroductionRules
    {
        /// <summary>Registry id of the company the father's friend works at.</summary>
        public const string IntroducedClientId = "kr_daum";

        /// <summary>Display name for the tier card copy.</summary>
        public const string IntroducedClientNameKo = "다음커뮤니케이션";

        /// <summary>
        /// The single tier the introduction reaches. A midsize 2000-era client resolves here, which
        /// is one rung below the national enterprises the family cannot service yet.
        /// </summary>
        public const ContractClientTier IntroducedTier = ContractClientTier.T3PrimeVendor;

        /// <summary>
        /// Fraction of the normal completed-contract requirement the introduction covers, in basis
        /// points. 16 contracts becomes 8.
        /// </summary>
        public const int CompletedContractDiscountBasisPoints = 5_000;

        /// <summary>Never discount below this many finished contracts, whatever the tier asks.</summary>
        public const int MinimumCompletedContracts = 4;

        /// <summary>
        /// True only for the mid-size tier. Local and regional clients need no introduction, growth
        /// companies are reachable on merit, and the national tier is not something a friend can
        /// shortcut.
        /// </summary>
        public static bool Covers(ContractClientTier tier) => tier == IntroducedTier;

        /// <summary>
        /// Completed contracts a tier asks for once the introduction is taken into account.
        /// </summary>
        public static int RequiredCompletedContracts(ContractClientTier tier, int baseRequirement)
        {
            if (baseRequirement <= 0) return 0;
            if (!Covers(tier)) return baseRequirement;
            var discounted = (int)((long)baseRequirement * CompletedContractDiscountBasisPoints / 10_000L);
            return Math.Max(MinimumCompletedContracts, Math.Min(baseRequirement, discounted));
        }

        /// <summary>
        /// Label for the tier card. It names the company so the lower number reads as the father's
        /// connection rather than as a bug.
        /// </summary>
        public static string ProgressLabelKo(
            ContractClientTier tier,
            int completedContracts,
            int baseRequirement)
        {
            var required = RequiredCompletedContracts(tier, baseRequirement);
            if (!Covers(tier) || required >= baseRequirement)
                return $"하청 완료 {completedContracts}/{required}건";
            return $"하청 완료 {completedContracts}/{required}건 · " +
                   $"아빠 친구가 있는 {IntroducedClientNameKo} 소개로 {baseRequirement}건에서 낮춤";
        }
    }
}
