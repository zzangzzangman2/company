using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Simulation.Family;

namespace FamilyCompany.Simulation.OfficeInteractions
{
    public static class OfficeInteractionCatalog
    {
        private static readonly AutonomousOfficeAction[] AllOfficeMacros =
        {
            AutonomousOfficeAction.FocusWork,
            AutonomousOfficeAction.Administration,
            AutonomousOfficeAction.Reception,
            AutonomousOfficeAction.Printing,
            AutonomousOfficeAction.Meeting,
            AutonomousOfficeAction.ShortBreak,
            AutonomousOfficeAction.DeepRest,
            AutonomousOfficeAction.CoffeeBreak,
            AutonomousOfficeAction.SocialChat,
            AutonomousOfficeAction.BurnoutRecovery
        };

        private static readonly OfficeInteractionDefinition[] Definitions = CreateDefinitions();

        public static IReadOnlyList<OfficeInteractionDefinition> All => Definitions;

        public static IReadOnlyList<OfficeInteractionCandidate> CandidatesFor(FamilyMemberState member)
        {
            if (member == null) throw new ArgumentNullException(nameof(member));
            bool meeting = member.Autonomy.CurrentAction == AutonomousOfficeAction.Meeting;
            var candidates = new List<OfficeInteractionCandidate>();
            foreach (OfficeInteractionDefinition definition in Definitions)
            {
                if (definition.CandidateScope == OfficeInteractionCandidateScope.FallbackOnly) continue;
                if (meeting != (definition.CandidateScope == OfficeInteractionCandidateScope.MeetingMacroOnly))
                    continue;
                if (definition.RequiresPreviousLocationOutsideDesk &&
                    member.Autonomy.MicroAction.TargetLocation == OfficeSemanticLocation.Desk)
                    continue;
                int weight = definition.LegacyWeightFor(member.Role);
                if (weight <= 0) continue;
                candidates.Add(new OfficeInteractionCandidate(
                    definition,
                    definition.ResolveTargetId(member.MemberId),
                    weight));
            }
            return candidates;
        }

        public static OfficeInteractionCandidate ResolveLegacyCandidate(
            FamilyMemberState member,
            OfficeMicroAction action,
            string targetId,
            OfficeSemanticLocation location,
            int weight)
        {
            OfficeInteractionCandidate[] matches = CandidatesFor(member)
                .Where(candidate =>
                    candidate.MicroAction == action &&
                    candidate.SemanticLocation == location &&
                    candidate.LegacyWeight == weight &&
                    string.Equals(candidate.TargetId, targetId, StringComparison.Ordinal))
                .ToArray();
            if (matches.Length == 0 &&
                action == OfficeMicroAction.LookingAround &&
                location == OfficeSemanticLocation.None &&
                weight == 1 &&
                string.Equals(targetId, "current:" + member.MemberId, StringComparison.Ordinal))
            {
                OfficeInteractionDefinition fallback = Definitions.Single(definition =>
                    definition.CandidateScope == OfficeInteractionCandidateScope.FallbackOnly);
                return new OfficeInteractionCandidate(fallback, targetId, weight);
            }
            if (matches.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Legacy candidate must resolve to exactly one interaction: member={member.MemberId} " +
                    $"action={action} target={targetId} location={location} weight={weight} matches={matches.Length}.");
            }
            return matches[0];
        }

        private static OfficeInteractionDefinition[] CreateDefinitions()
        {
            var result = new List<OfficeInteractionDefinition>
            {
                Define("meeting-prepare-video", OfficeMicroAction.PreparingMeeting, OfficeSemanticLocation.MeetingRoom,
                    "meeting:main", "desk_with_pc", 4, 8, 4, 0, true, true, true,
                    OfficeInteractionApproachPolicy.AssignedSeatApproach, OfficeInteractionReservationPolicy.AssignedSeat,
                    OfficeInteractionCandidateScope.MeetingMacroOnly, 24, null, AutonomousOfficeAction.Meeting),
                Define("meeting-phone-assigned-desk", OfficeMicroAction.PhoneCall, OfficeSemanticLocation.MeetingRoom,
                    "desk:{memberId}", "desk_with_pc", 3, 8, 1, 0, true, true, true,
                    OfficeInteractionApproachPolicy.AssignedSeatApproach, OfficeInteractionReservationPolicy.AssignedSeat,
                    OfficeInteractionCandidateScope.MeetingMacroOnly, 22, null, AutonomousOfficeAction.Meeting),
                Define("meeting-read-documents", OfficeMicroAction.ReadingDocument, OfficeSemanticLocation.MeetingRoom,
                    "meeting-docs:{memberId}", "desk_with_pc", 3, 7, 4, 0, true, true, true,
                    OfficeInteractionApproachPolicy.AssignedSeatApproach, OfficeInteractionReservationPolicy.AssignedSeat,
                    OfficeInteractionCandidateScope.MeetingMacroOnly, 18, null, AutonomousOfficeAction.Meeting),
                Define("meeting-type-notes", OfficeMicroAction.Typing, OfficeSemanticLocation.MeetingRoom,
                    "meeting-typing:{memberId}", "desk_with_pc", 8, 18, 4, 0, true, true, true,
                    OfficeInteractionApproachPolicy.AssignedSeatApproach, OfficeInteractionReservationPolicy.AssignedSeat,
                    OfficeInteractionCandidateScope.MeetingMacroOnly, 16, null, AutonomousOfficeAction.Meeting),
                Define("meeting-look-around", OfficeMicroAction.LookingAround, OfficeSemanticLocation.MeetingRoom,
                    "current:{memberId}", "desk_with_pc", 1, 2, 1, 0, true, true, true,
                    OfficeInteractionApproachPolicy.AssignedSeatApproach, OfficeInteractionReservationPolicy.AssignedSeat,
                    OfficeInteractionCandidateScope.MeetingMacroOnly, 8, null, AutonomousOfficeAction.Meeting),

                Define("open-stretch", OfficeMicroAction.Stretching, OfficeSemanticLocation.OpenArea,
                    "stretch:{memberId}", "", 1, 3, 1, 0, false, false, false,
                    OfficeInteractionApproachPolicy.OpenArea, OfficeInteractionReservationPolicy.None,
                    OfficeInteractionCandidateScope.StandardOfficeMacro, 6, null, AllOfficeMacros),
                Define("current-look", OfficeMicroAction.LookingAround, OfficeSemanticLocation.None,
                    "current:{memberId}", "", 1, 2, 1, 0, false, false, false,
                    OfficeInteractionApproachPolicy.CurrentPosition, OfficeInteractionReservationPolicy.None,
                    OfficeInteractionCandidateScope.StandardOfficeMacro, 5, null, AllOfficeMacros),
                Define("water-drink", OfficeMicroAction.DrinkingWater, OfficeSemanticLocation.Water,
                    "water:main", "water_dispenser", 2, 4, 1, OfficeMicroActionRules.DrinkCooldownMinutes,
                    true, false, false, OfficeInteractionApproachPolicy.AdjacentCardinal,
                    OfficeInteractionReservationPolicy.ExclusiveFurniture,
                    OfficeInteractionCandidateScope.StandardOfficeMacro, 7, null,
                    AutonomousOfficeAction.ShortBreak, AutonomousOfficeAction.DeepRest,
                    AutonomousOfficeAction.BurnoutRecovery),
                Define("lounge-chat", OfficeMicroAction.ShortConversation, OfficeSemanticLocation.Lounge,
                    "conversation:pending", "sofa", 4, 8, 2, OfficeMicroActionRules.ConversationCooldownMinutes,
                    true, false, false, OfficeInteractionApproachPolicy.SharedLoungeArea,
                    OfficeInteractionReservationPolicy.PairedConversation,
                    OfficeInteractionCandidateScope.StandardOfficeMacro, 7, null,
                    AutonomousOfficeAction.SocialChat, AutonomousOfficeAction.ShortBreak),

                Define("desk-typing", OfficeMicroAction.Typing, OfficeSemanticLocation.Desk,
                    "desk:{memberId}", "desk_with_pc", 8, 18, 1, 0, true, true, true,
                    OfficeInteractionApproachPolicy.AssignedSeatApproach, OfficeInteractionReservationPolicy.AssignedSeat,
                    OfficeInteractionCandidateScope.StandardOfficeMacro, 0,
                    Weights((FamilyRole.Player, 38), (FamilyRole.OlderSister, 9), (FamilyRole.Father, 10)),
                    AutonomousOfficeAction.FocusWork),
                Define("filing-read", OfficeMicroAction.ReadingDocument, OfficeSemanticLocation.Filing,
                    "filing:main", "document_bookcase", 3, 7, 1, 0, true, false, false,
                    OfficeInteractionApproachPolicy.AdjacentCardinal, OfficeInteractionReservationPolicy.ExclusiveFurniture,
                    OfficeInteractionCandidateScope.StandardOfficeMacro, 0,
                    Weights((FamilyRole.Player, 16), (FamilyRole.Father, 18)),
                    AutonomousOfficeAction.Administration),
                Define("copier-use", OfficeMicroAction.UsingCopier, OfficeSemanticLocation.Printer,
                    "copier:main", "fax_copier", 3, 6, 1, 0, true, false, false,
                    OfficeInteractionApproachPolicy.AdjacentCardinal, OfficeInteractionReservationPolicy.ExclusiveFurniture,
                    OfficeInteractionCandidateScope.StandardOfficeMacro, 0,
                    Weights((FamilyRole.Player, 15), (FamilyRole.OlderSister, 20), (FamilyRole.Mother, 18)),
                    AutonomousOfficeAction.Printing),
                Define("desk-phone", OfficeMicroAction.PhoneCall, OfficeSemanticLocation.Desk,
                    "desk:{memberId}", "desk_with_pc", 3, 8, 1, 0, true, true, true,
                    OfficeInteractionApproachPolicy.AssignedSeatApproach, OfficeInteractionReservationPolicy.AssignedSeat,
                    OfficeInteractionCandidateScope.StandardOfficeMacro, 0,
                    Weights((FamilyRole.Player, 8)), AutonomousOfficeAction.Reception),

                Define("filing-document", OfficeMicroAction.FilingDocument, OfficeSemanticLocation.Filing,
                    "filing:main", "document_bookcase", 2, 5, 1, 0, true, false, false,
                    OfficeInteractionApproachPolicy.AdjacentCardinal, OfficeInteractionReservationPolicy.ExclusiveFurniture,
                    OfficeInteractionCandidateScope.StandardOfficeMacro, 0,
                    Weights((FamilyRole.OlderSister, 24), (FamilyRole.Mother, 22)),
                    AutonomousOfficeAction.Administration),
                Define("reception-phone", OfficeMicroAction.PhoneCall, OfficeSemanticLocation.Reception,
                    "reception:main", "reception_counter", 3, 8, 1, 0, true, false, false,
                    OfficeInteractionApproachPolicy.AdjacentCardinal, OfficeInteractionReservationPolicy.ExclusiveFurniture,
                    OfficeInteractionCandidateScope.StandardOfficeMacro, 0,
                    Weights((FamilyRole.OlderSister, 18), (FamilyRole.Father, 24)),
                    AutonomousOfficeAction.Reception),
                Define("meeting-prepare", OfficeMicroAction.PreparingMeeting, OfficeSemanticLocation.MeetingRoom,
                    "meeting:main", "desk_with_pc", 4, 8, 4, 0, true, true, true,
                    OfficeInteractionApproachPolicy.AssignedSeatApproach, OfficeInteractionReservationPolicy.AssignedSeat,
                    OfficeInteractionCandidateScope.StandardOfficeMacro, 0,
                    Weights((FamilyRole.Father, 22)), AutonomousOfficeAction.Meeting),
                Define("desk-tidying", OfficeMicroAction.TidyingDesk, OfficeSemanticLocation.Desk,
                    "desk:{memberId}", "desk_with_pc", 3, 6, 1, 0, true, true, true,
                    OfficeInteractionApproachPolicy.AssignedSeatApproach, OfficeInteractionReservationPolicy.AssignedSeat,
                    OfficeInteractionCandidateScope.StandardOfficeMacro, 0,
                    Weights((FamilyRole.Mother, 20)), AutonomousOfficeAction.Administration),
                Define("coffee-drink", OfficeMicroAction.DrinkingCoffee, OfficeSemanticLocation.Coffee,
                    "coffee:main", "coffee_table", 4, 7, 2, OfficeMicroActionRules.DrinkCooldownMinutes,
                    true, false, false, OfficeInteractionApproachPolicy.AdjacentOrTwoCells,
                    OfficeInteractionReservationPolicy.SharedFurnitureCapacity,
                    OfficeInteractionCandidateScope.StandardOfficeMacro, 0,
                    Weights((FamilyRole.Mother, 15)), AutonomousOfficeAction.CoffeeBreak,
                    AutonomousOfficeAction.ShortBreak),

                Define("return-assigned-desk", OfficeMicroAction.ReturningToDesk, OfficeSemanticLocation.Desk,
                    "desk:{memberId}", "desk_with_pc", 2, 5, 1, 0, true, true, true,
                    OfficeInteractionApproachPolicy.AssignedSeatApproach, OfficeInteractionReservationPolicy.AssignedSeat,
                    OfficeInteractionCandidateScope.StandardOfficeMacro, 12, null, AllOfficeMacros,
                    requiresPreviousLocationOutsideDesk: true),
                Define("fallback-current-look", OfficeMicroAction.LookingAround, OfficeSemanticLocation.None,
                    "current:{memberId}", "", 1, 2, 1, 0, false, false, false,
                    OfficeInteractionApproachPolicy.CurrentPosition, OfficeInteractionReservationPolicy.None,
                    OfficeInteractionCandidateScope.FallbackOnly, 1, null, AllOfficeMacros)
            };
            return result.ToArray();
        }

        private static OfficeInteractionDefinition Define(
            string id,
            OfficeMicroAction action,
            OfficeSemanticLocation location,
            string target,
            string furnitureKind,
            int minimumDuration,
            int maximumDuration,
            int capacity,
            int cooldown,
            bool requiresFurniture,
            bool requiresSeat,
            bool requiresAssignedSeat,
            OfficeInteractionApproachPolicy approach,
            OfficeInteractionReservationPolicy reservation,
            OfficeInteractionCandidateScope scope,
            int universalWeight,
            IEnumerable<KeyValuePair<FamilyRole, int>> weights,
            params AutonomousOfficeAction[] compatibleMacros)
        {
            return Define(id, action, location, target, furnitureKind, minimumDuration, maximumDuration,
                capacity, cooldown, requiresFurniture, requiresSeat, requiresAssignedSeat, approach, reservation,
                scope, universalWeight, weights, compatibleMacros, false);
        }

        private static OfficeInteractionDefinition Define(
            string id,
            OfficeMicroAction action,
            OfficeSemanticLocation location,
            string target,
            string furnitureKind,
            int minimumDuration,
            int maximumDuration,
            int capacity,
            int cooldown,
            bool requiresFurniture,
            bool requiresSeat,
            bool requiresAssignedSeat,
            OfficeInteractionApproachPolicy approach,
            OfficeInteractionReservationPolicy reservation,
            OfficeInteractionCandidateScope scope,
            int universalWeight,
            IEnumerable<KeyValuePair<FamilyRole, int>> weights,
            IEnumerable<AutonomousOfficeAction> compatibleMacros,
            bool requiresPreviousLocationOutsideDesk)
        {
            return new OfficeInteractionDefinition(
                id, action, location, target, furnitureKind, action.ToString(), minimumDuration, maximumDuration,
                capacity, cooldown, requiresFurniture, requiresSeat, requiresAssignedSeat, true,
                approach, reservation, scope, universalWeight, weights, compatibleMacros,
                requiresPreviousLocationOutsideDesk);
        }

        private static KeyValuePair<FamilyRole, int>[] Weights(params (FamilyRole Role, int Weight)[] weights)
        {
            return weights.Select(item => new KeyValuePair<FamilyRole, int>(item.Role, item.Weight)).ToArray();
        }
    }
}
