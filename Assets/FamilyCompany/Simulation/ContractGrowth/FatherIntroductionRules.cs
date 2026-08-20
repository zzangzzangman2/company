using System;

namespace FamilyCompany.Simulation.ContractGrowth
{
    /// <summary>
    /// The father's friend at a conglomerate. It is the reason a four-person family workshop gets
    /// looked at by a prime vendor at all, so it lowers the completed-contract count the upper client
    /// tiers ask for.
    ///
    /// It only ever discounts the count. Reputation, quality, on-time rate, domain hours and the
    /// company grade are untouched, because an introduction gets the meeting, not the contract: the
    /// family still has to have done work of that standard. That is also why the first board stays
    /// local businesses — the connection opens a door later, it does not hand over a big job on day
    /// one while the company has no technology.
    /// </summary>
    public static class FatherIntroductionRules
    {
        /// <summary>
        /// Fraction of the normal completed-contract requirement the introduction covers for the
        /// prime and national tiers, in basis points. 16 contracts becomes 8, 28 becomes 14.
        /// </summary>
        public const int CompletedContractDiscountBasisPoints = 5_000;

        /// <summary>Never discount below this many finished contracts, whatever the tier asks.</summary>
        public const int MinimumCompletedContracts = 4;

        /// <summary>
        /// The tiers the introduction reaches. The local and regional clients need no introduction,
        /// and T2 growth companies are already reachable on merit.
        /// </summary>
        public static bool Covers(ContractClientTier tier) =>
            tier == ContractClientTier.T3PrimeVendor || tier == ContractClientTier.T4NationalEnterprise;

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
        /// Label for the tier card, so the player can see why the bar moved rather than wondering
        /// whether the requirement is a bug.
        /// </summary>
        public static string ProgressLabelKo(
            ContractClientTier tier,
            int completedContracts,
            int baseRequirement)
        {
            var required = RequiredCompletedContracts(tier, baseRequirement);
            if (!Covers(tier) || required >= baseRequirement)
                return $"하청 완료 {completedContracts}/{required}건";
            return $"하청 완료 {completedContracts}/{required}건 · 아빠의 소개로 {baseRequirement}건에서 낮춤";
        }
    }
}
