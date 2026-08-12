using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Simulation.Core;
using FamilyCompany.Simulation.Family;

namespace FamilyCompany.Simulation.OfficeInteractions
{
    public sealed class OfficeInteractionScoreBreakdown
    {
        public OfficeInteractionScoreBreakdown(
            string interactionId,
            string offerId,
            string targetId,
            OfficeMicroAction microAction,
            OfficeSemanticLocation location,
            bool rejected,
            string rejectionReason,
            int baseRoleAffinity,
            int macroCompatibility,
            int needUrgency,
            int novelty,
            int distanceUtility,
            int availabilityUtility,
            int returnToWorkUtility,
            int socialUtility,
            int repetitionPenalty,
            int scheduleRiskPenalty,
            int congestionPenalty)
        {
            InteractionId = interactionId ?? string.Empty;
            OfferId = offerId ?? string.Empty;
            TargetId = targetId ?? string.Empty;
            MicroAction = microAction;
            Location = location;
            Rejected = rejected;
            RejectionReason = rejectionReason ?? string.Empty;
            BaseRoleAffinity = baseRoleAffinity;
            MacroCompatibility = macroCompatibility;
            NeedUrgency = needUrgency;
            Novelty = novelty;
            DistanceUtility = distanceUtility;
            AvailabilityUtility = availabilityUtility;
            ReturnToWorkUtility = returnToWorkUtility;
            SocialUtility = socialUtility;
            RepetitionPenalty = repetitionPenalty;
            ScheduleRiskPenalty = scheduleRiskPenalty;
            CongestionPenalty = congestionPenalty;
            TotalScore = rejected
                ? int.MinValue
                : baseRoleAffinity + macroCompatibility + needUrgency + novelty + distanceUtility +
                  availabilityUtility + returnToWorkUtility + socialUtility - repetitionPenalty -
                  scheduleRiskPenalty - congestionPenalty;
        }

        public string InteractionId { get; }
        public string OfferId { get; }
        public string TargetId { get; }
        public OfficeMicroAction MicroAction { get; }
        public OfficeSemanticLocation Location { get; }
        public bool Rejected { get; }
        public string RejectionReason { get; }
        public int BaseRoleAffinity { get; }
        public int MacroCompatibility { get; }
        public int NeedUrgency { get; }
        public int Novelty { get; }
        public int DistanceUtility { get; }
        public int AvailabilityUtility { get; }
        public int ReturnToWorkUtility { get; }
        public int SocialUtility { get; }
        public int RepetitionPenalty { get; }
        public int ScheduleRiskPenalty { get; }
        public int CongestionPenalty { get; }
        public int TotalScore { get; }

        public string DeterminismSignature()
        {
            return string.Join("|", new[]
            {
                InteractionId,
                OfferId,
                TargetId,
                ((int)MicroAction).ToString(),
                ((int)Location).ToString(),
                Rejected ? "1" : "0",
                RejectionReason,
                BaseRoleAffinity.ToString(),
                MacroCompatibility.ToString(),
                NeedUrgency.ToString(),
                Novelty.ToString(),
                DistanceUtility.ToString(),
                AvailabilityUtility.ToString(),
                ReturnToWorkUtility.ToString(),
                SocialUtility.ToString(),
                RepetitionPenalty.ToString(),
                ScheduleRiskPenalty.ToString(),
                CongestionPenalty.ToString(),
                TotalScore.ToString()
            });
        }
    }

    public static class OfficeInteractionScoring
    {
        public const int TopBandRange = 120;

        public static OfficeInteractionScoreBreakdown Score(
            FamilyMemberState member,
            OfficeInteractionCandidate candidate)
        {
            if (member == null) throw new ArgumentNullException(nameof(member));
            if (candidate == null) throw new ArgumentNullException(nameof(candidate));

            OfficeMicroActionState state = member.Autonomy.MicroAction;
            int baseAffinity = checked(candidate.LegacyWeight * 20);
            int macro = candidate.Definition.IsMacroCompatible(member.Autonomy.CurrentAction) ? 200 : 0;
            int need = NeedUrgency(member, candidate.MicroAction);
            int locationBit = (int)candidate.SemanticLocation;
            int novelty = locationBit > 0 && locationBit < 31 &&
                          (state.VisitedLocationMask & (1 << locationBit)) == 0
                ? 100
                : 0;
            int repetition = candidate.MicroAction == state.LastAction ||
                             string.Equals(candidate.TargetId, state.LastTargetId, StringComparison.Ordinal)
                ? 100
                : 0;
            return new OfficeInteractionScoreBreakdown(
                candidate.InteractionId,
                candidate.OfferId,
                candidate.TargetId,
                candidate.MicroAction,
                candidate.SemanticLocation,
                false,
                string.Empty,
                baseAffinity,
                macro,
                need,
                novelty,
                0,
                50,
                0,
                0,
                repetition,
                0,
                0);
        }

        public static OfficeInteractionScoreBreakdown SelectShadow(
            int worldSeed,
            FamilyMemberState member,
            IReadOnlyList<OfficeInteractionScoreBreakdown> scoredCandidates)
        {
            if (member == null) throw new ArgumentNullException(nameof(member));
            if (scoredCandidates == null) throw new ArgumentNullException(nameof(scoredCandidates));
            OfficeInteractionScoreBreakdown[] eligible = scoredCandidates
                .Where(item => !item.Rejected)
                .OrderBy(item => item.OfferId, StringComparer.Ordinal)
                .ToArray();
            if (eligible.Length == 0) return null;

            int maximum = eligible.Max(item => item.TotalScore);
            int threshold = maximum - TopBandRange;
            OfficeInteractionScoreBreakdown[] band = eligible
                .Where(item => item.TotalScore >= threshold)
                .ToArray();
            int totalWeight = band.Sum(item => 1 + Math.Max(0, item.TotalScore - threshold));
            int roll = StableRandom.StableRandomInt(
                $"office-interaction-pick-v1:{worldSeed}:{member.MemberId}:" +
                $"{member.Autonomy.ActionStartedMinute}:{member.Autonomy.MicroAction.SequenceIndex + 1}",
                totalWeight);
            foreach (OfficeInteractionScoreBreakdown item in band)
            {
                roll -= 1 + Math.Max(0, item.TotalScore - threshold);
                if (roll < 0) return item;
            }
            return band[band.Length - 1];
        }

        private static int NeedUrgency(FamilyMemberState member, OfficeMicroAction action)
        {
            bool recovery = action == OfficeMicroAction.DrinkingWater ||
                            action == OfficeMicroAction.DrinkingCoffee ||
                            action == OfficeMicroAction.Stretching ||
                            action == OfficeMicroAction.LookingAround ||
                            action == OfficeMicroAction.ShortConversation;
            int recoveryNeed = ((50 - member.Energy) + (member.Stress - 50)) / 2;
            return Math.Max(-100, Math.Min(100, recovery ? recoveryNeed : -recoveryNeed));
        }
    }
}
