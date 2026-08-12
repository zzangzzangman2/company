using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Simulation.Core;

namespace FamilyCompany.Simulation.Family
{
    public enum OfficeMicroAction
    {
        None = 0,
        Typing = 1,
        ReadingDocument = 2,
        FilingDocument = 3,
        UsingCopier = 4,
        DrinkingWater = 5,
        DrinkingCoffee = 6,
        Stretching = 7,
        LookingAround = 8,
        ShortConversation = 9,
        PhoneCall = 10,
        TidyingDesk = 11,
        PreparingMeeting = 12,
        ReturningToDesk = 13
    }

    public sealed class OfficeMicroActionState
    {
        public OfficeMicroActionState(
            OfficeMicroAction action = OfficeMicroAction.None,
            string targetId = "",
            OfficeSemanticLocation targetLocation = OfficeSemanticLocation.None,
            long startedMinute = 0,
            long endsMinute = 0,
            int sequenceIndex = 0,
            string partnerMemberId = "",
            long macroActionStartedMinute = -1,
            OfficeMicroAction lastAction = OfficeMicroAction.None,
            string lastTargetId = "",
            long lastTargetEndedMinute = -100000,
            long lastWaterStartedMinute = -100000,
            long lastCoffeeStartedMinute = -100000,
            long lastConversationStartedMinute = -100000,
            string lastConversationPartnerId = "",
            long deskResidenceStartedMinute = -1,
            int visitedLocationMask = 0)
        {
            if (startedMinute < 0) throw new ArgumentOutOfRangeException(nameof(startedMinute));
            if (endsMinute < 0) throw new ArgumentOutOfRangeException(nameof(endsMinute));
            if (sequenceIndex < 0) throw new ArgumentOutOfRangeException(nameof(sequenceIndex));
            Action = action;
            TargetId = targetId ?? string.Empty;
            TargetLocation = targetLocation;
            StartedMinute = startedMinute;
            EndsMinute = endsMinute;
            SequenceIndex = sequenceIndex;
            PartnerMemberId = partnerMemberId ?? string.Empty;
            MacroActionStartedMinute = macroActionStartedMinute;
            LastAction = lastAction;
            LastTargetId = lastTargetId ?? string.Empty;
            LastTargetEndedMinute = lastTargetEndedMinute;
            LastWaterStartedMinute = lastWaterStartedMinute;
            LastCoffeeStartedMinute = lastCoffeeStartedMinute;
            LastConversationStartedMinute = lastConversationStartedMinute;
            LastConversationPartnerId = lastConversationPartnerId ?? string.Empty;
            DeskResidenceStartedMinute = deskResidenceStartedMinute;
            VisitedLocationMask = visitedLocationMask;
        }

        public OfficeMicroAction Action { get; private set; }
        public string TargetId { get; private set; }
        public OfficeSemanticLocation TargetLocation { get; private set; }
        public long StartedMinute { get; private set; }
        public long EndsMinute { get; private set; }
        public int SequenceIndex { get; private set; }
        public string PartnerMemberId { get; private set; }
        public long MacroActionStartedMinute { get; private set; }
        public OfficeMicroAction LastAction { get; private set; }
        public string LastTargetId { get; private set; }
        public long LastTargetEndedMinute { get; private set; }
        public long LastWaterStartedMinute { get; private set; }
        public long LastCoffeeStartedMinute { get; private set; }
        public long LastConversationStartedMinute { get; private set; }
        public string LastConversationPartnerId { get; private set; }
        public long DeskResidenceStartedMinute { get; private set; }
        public int VisitedLocationMask { get; private set; }

        public string ActionLabel => OfficeMicroActionRules.ActionLabel(Action);

        internal void Begin(
            OfficeMicroAction action,
            string targetId,
            OfficeSemanticLocation targetLocation,
            long startedMinute,
            int durationMinutes,
            long macroActionStartedMinute,
            string partnerMemberId = "")
        {
            if (startedMinute < 0) throw new ArgumentOutOfRangeException(nameof(startedMinute));
            if (durationMinutes <= 0) throw new ArgumentOutOfRangeException(nameof(durationMinutes));
            if (Action != OfficeMicroAction.None)
            {
                LastAction = Action;
                LastTargetId = TargetId;
                LastTargetEndedMinute = EndsMinute;
            }

            bool continuesAtDesk = targetLocation == OfficeSemanticLocation.Desk &&
                                   TargetLocation == OfficeSemanticLocation.Desk &&
                                   EndsMinute == startedMinute &&
                                   DeskResidenceStartedMinute >= 0;
            DeskResidenceStartedMinute = targetLocation == OfficeSemanticLocation.Desk
                ? continuesAtDesk ? DeskResidenceStartedMinute : startedMinute
                : -1;
            Action = action;
            TargetId = targetId ?? string.Empty;
            TargetLocation = targetLocation;
            StartedMinute = startedMinute;
            EndsMinute = checked(startedMinute + durationMinutes);
            SequenceIndex++;
            PartnerMemberId = partnerMemberId ?? string.Empty;
            MacroActionStartedMinute = macroActionStartedMinute;
            if ((int)targetLocation > 0 && (int)targetLocation < 31)
                VisitedLocationMask |= 1 << (int)targetLocation;
            if (action == OfficeMicroAction.DrinkingWater) LastWaterStartedMinute = startedMinute;
            if (action == OfficeMicroAction.DrinkingCoffee) LastCoffeeStartedMinute = startedMinute;
            if (action == OfficeMicroAction.ShortConversation)
            {
                LastConversationStartedMinute = startedMinute;
                LastConversationPartnerId = PartnerMemberId;
            }
        }

        internal void ClearForMacro(long minute, long macroActionStartedMinute)
        {
            if (Action != OfficeMicroAction.None)
            {
                LastAction = Action;
                LastTargetId = TargetId;
                LastTargetEndedMinute = Math.Min(EndsMinute, minute);
            }
            Action = OfficeMicroAction.None;
            TargetId = string.Empty;
            TargetLocation = OfficeSemanticLocation.None;
            StartedMinute = minute;
            EndsMinute = minute;
            PartnerMemberId = string.Empty;
            MacroActionStartedMinute = macroActionStartedMinute;
            DeskResidenceStartedMinute = -1;
        }
    }

    public static class OfficeMicroActionRules
    {
        public const int SharedTargetCooldownMinutes = 15;
        public const int DrinkCooldownMinutes = 45;
        public const int ConversationCooldownMinutes = 90;
        public const int MaximumDeskResidenceMinutes = 45;

        public static string ActionLabel(OfficeMicroAction action)
        {
            return action switch
            {
                OfficeMicroAction.Typing => "타이핑",
                OfficeMicroAction.ReadingDocument => "서류 읽기",
                OfficeMicroAction.FilingDocument => "서류 정리",
                OfficeMicroAction.UsingCopier => "복사기 사용",
                OfficeMicroAction.DrinkingWater => "물 마시기",
                OfficeMicroAction.DrinkingCoffee => "커피 마시기",
                OfficeMicroAction.Stretching => "스트레칭",
                OfficeMicroAction.LookingAround => "주변 살피기",
                OfficeMicroAction.ShortConversation => "짧은 대화",
                OfficeMicroAction.PhoneCall => "전화 업무",
                OfficeMicroAction.TidyingDesk => "책상 정리",
                OfficeMicroAction.PreparingMeeting => "회의 준비",
                OfficeMicroAction.ReturningToDesk => "자리로 복귀",
                _ => string.Empty
            };
        }

        public static int Capacity(string targetId)
        {
            if (string.IsNullOrEmpty(targetId)) return int.MaxValue;
            if (targetId.StartsWith("coffee:", StringComparison.Ordinal)) return 2;
            if (targetId.StartsWith("meeting:", StringComparison.Ordinal)) return 4;
            if (targetId.StartsWith("conversation:", StringComparison.Ordinal)) return 2;
            if (targetId.StartsWith("desk:", StringComparison.Ordinal) ||
                targetId.StartsWith("current:", StringComparison.Ordinal) ||
                targetId.StartsWith("stretch:", StringComparison.Ordinal)) return 1;
            return 1;
        }

        public static bool IsOfficeMacro(AutonomousOfficeAction action)
        {
            return action == AutonomousOfficeAction.FocusWork ||
                   action == AutonomousOfficeAction.Administration ||
                   action == AutonomousOfficeAction.Reception ||
                   action == AutonomousOfficeAction.Printing ||
                   action == AutonomousOfficeAction.Meeting ||
                   action == AutonomousOfficeAction.ShortBreak ||
                   action == AutonomousOfficeAction.DeepRest ||
                   action == AutonomousOfficeAction.CoffeeBreak ||
                   action == AutonomousOfficeAction.SocialChat ||
                   action == AutonomousOfficeAction.BurnoutRecovery;
        }
    }

    public static class OfficePresentationMicroActionSimulation
    {
        private sealed class Candidate
        {
            public Candidate(
                OfficeMicroAction action,
                string targetId,
                OfficeSemanticLocation location,
                int weight)
            {
                Action = action;
                TargetId = targetId;
                Location = location;
                Weight = weight;
            }

            public OfficeMicroAction Action { get; }
            public string TargetId { get; }
            public OfficeSemanticLocation Location { get; }
            public int Weight { get; }
        }

        public static void EnsureActions(int worldSeed, FamilyState family, long minute)
        {
            if (family == null) throw new ArgumentNullException(nameof(family));
            if (minute < 0) throw new ArgumentOutOfRangeException(nameof(minute));
            NormalizeConversationPairs(family, minute);
            FamilyMemberState[] ordered = family.Members
                .OrderBy(member => StableRandom.StableRandomInt(
                    $"office-micro-priority-v1:{worldSeed}:{minute}:{member.MemberId}",
                    100000))
                .ThenBy(member => member.MemberId, StringComparer.Ordinal)
                .ToArray();
            foreach (FamilyMemberState member in ordered)
            {
                OfficeAutonomyState autonomy = member.Autonomy;
                OfficeMicroActionState state = autonomy.MicroAction;
                if (!OfficeMicroActionRules.IsOfficeMacro(autonomy.CurrentAction))
                {
                    if (state.Action != OfficeMicroAction.None ||
                        state.MacroActionStartedMinute != autonomy.ActionStartedMinute)
                        state.ClearForMacro(minute, autonomy.ActionStartedMinute);
                    continue;
                }
                if (state.MacroActionStartedMinute == autonomy.ActionStartedMinute &&
                    state.Action != OfficeMicroAction.None && state.EndsMinute > minute) continue;
                BeginNext(worldSeed, family, member, minute);
            }
        }

        private static void NormalizeConversationPairs(FamilyState family, long minute)
        {
            foreach (FamilyMemberState member in family.Members)
            {
                OfficeMicroActionState micro = member.Autonomy.MicroAction;
                if (micro.Action != OfficeMicroAction.ShortConversation) continue;
                FamilyMemberState partner = family.Members.FirstOrDefault(candidate =>
                    string.Equals(candidate.MemberId, micro.PartnerMemberId, StringComparison.Ordinal));
                OfficeMicroActionState partnerMicro = partner?.Autonomy.MicroAction;
                bool valid = OfficeMicroActionRules.IsOfficeMacro(member.Autonomy.CurrentAction) &&
                             micro.MacroActionStartedMinute == member.Autonomy.ActionStartedMinute &&
                             partner != null &&
                             OfficeMicroActionRules.IsOfficeMacro(partner.Autonomy.CurrentAction) &&
                             partnerMicro.MacroActionStartedMinute == partner.Autonomy.ActionStartedMinute &&
                             partnerMicro.Action == OfficeMicroAction.ShortConversation &&
                             string.Equals(partnerMicro.PartnerMemberId, member.MemberId, StringComparison.Ordinal) &&
                             string.Equals(partnerMicro.TargetId, micro.TargetId, StringComparison.Ordinal) &&
                             partnerMicro.StartedMinute == micro.StartedMinute &&
                             partnerMicro.EndsMinute == micro.EndsMinute;
                if (valid) continue;
                micro.ClearForMacro(minute, member.Autonomy.ActionStartedMinute);
                if (partnerMicro != null &&
                    partnerMicro.Action == OfficeMicroAction.ShortConversation &&
                    string.Equals(partnerMicro.PartnerMemberId, member.MemberId, StringComparison.Ordinal))
                {
                    partnerMicro.ClearForMacro(minute, partner.Autonomy.ActionStartedMinute);
                }
            }
        }

        public static void AdvanceTo(int worldSeed, FamilyState family, long elapsedMinute)
        {
            if (family == null) throw new ArgumentNullException(nameof(family));
            while (true)
            {
                long next = family.Members
                    .Where(member => member.Autonomy.MicroAction.Action != OfficeMicroAction.None &&
                                     member.Autonomy.MicroAction.EndsMinute <= elapsedMinute)
                    .Select(member => member.Autonomy.MicroAction.EndsMinute)
                    .DefaultIfEmpty(long.MaxValue)
                    .Min();
                if (next == long.MaxValue) break;
                EnsureActions(worldSeed, family, next);
            }
            EnsureActions(worldSeed, family, elapsedMinute);
        }

        private static void BeginNext(
            int worldSeed,
            FamilyState family,
            FamilyMemberState member,
            long minute)
        {
            OfficeMicroActionState state = member.Autonomy.MicroAction;
            List<Candidate> candidates = Candidates(member).Where(candidate =>
                IsAllowed(family, member, candidate, minute)).ToList();
            int visitedCount = CountBits(state.VisitedLocationMask);
            if (visitedCount < 3 && state.SequenceIndex >= 1)
            {
                List<Candidate> unvisited = candidates.Where(candidate =>
                    candidate.Location != OfficeSemanticLocation.None &&
                    (state.VisitedLocationMask & (1 << (int)candidate.Location)) == 0).ToList();
                if (unvisited.Count > 0) candidates = unvisited;
            }
            if (candidates.Count == 0)
            {
                candidates.Add(new Candidate(
                    OfficeMicroAction.LookingAround,
                    "current:" + member.MemberId,
                    OfficeSemanticLocation.None,
                    1));
            }

            Candidate selected = WeightedPick(worldSeed, member, state, candidates);
            if (selected.Action == OfficeMicroAction.ShortConversation &&
                TryBeginConversation(worldSeed, family, member, minute)) return;
            if (selected.Action == OfficeMicroAction.ShortConversation)
            {
                candidates.Remove(selected);
                selected = candidates.Count == 0
                    ? new Candidate(
                        OfficeMicroAction.LookingAround,
                        "current:" + member.MemberId,
                        OfficeSemanticLocation.None,
                        1)
                    : WeightedPick(worldSeed, member, state, candidates);
            }
            int duration = DurationMinutes(worldSeed, member.MemberId, state.SequenceIndex + 1, selected.Action);
            state.Begin(
                selected.Action,
                selected.TargetId,
                selected.Location,
                minute,
                duration,
                member.Autonomy.ActionStartedMinute);
        }

        private static IEnumerable<Candidate> Candidates(FamilyMemberState member)
        {
            string desk = "desk:" + member.MemberId;
            string current = "current:" + member.MemberId;
            var common = new List<Candidate>
            {
                new Candidate(OfficeMicroAction.Stretching, "stretch:" + member.MemberId, OfficeSemanticLocation.OpenArea, 6),
                new Candidate(OfficeMicroAction.LookingAround, current, OfficeSemanticLocation.None, 5),
                new Candidate(OfficeMicroAction.DrinkingWater, "water:main", OfficeSemanticLocation.Water, 7),
                new Candidate(OfficeMicroAction.ShortConversation, "conversation:pending", OfficeSemanticLocation.Lounge, 7)
            };
            switch (member.Role)
            {
                case FamilyRole.Player:
                    common.Add(new Candidate(OfficeMicroAction.Typing, desk, OfficeSemanticLocation.Desk, 38));
                    common.Add(new Candidate(OfficeMicroAction.ReadingDocument, "filing:main", OfficeSemanticLocation.Filing, 16));
                    common.Add(new Candidate(OfficeMicroAction.UsingCopier, "copier:main", OfficeSemanticLocation.Printer, 15));
                    common.Add(new Candidate(OfficeMicroAction.PhoneCall, desk, OfficeSemanticLocation.Desk, 8));
                    break;
                case FamilyRole.OlderSister:
                    common.Add(new Candidate(OfficeMicroAction.FilingDocument, "filing:main", OfficeSemanticLocation.Filing, 24));
                    common.Add(new Candidate(OfficeMicroAction.UsingCopier, "copier:main", OfficeSemanticLocation.Printer, 20));
                    common.Add(new Candidate(OfficeMicroAction.PhoneCall, "reception:main", OfficeSemanticLocation.Reception, 18));
                    common.Add(new Candidate(OfficeMicroAction.Typing, desk, OfficeSemanticLocation.Desk, 9));
                    break;
                case FamilyRole.Father:
                    common.Add(new Candidate(OfficeMicroAction.PhoneCall, "reception:main", OfficeSemanticLocation.Reception, 24));
                    common.Add(new Candidate(OfficeMicroAction.PreparingMeeting, "meeting:main", OfficeSemanticLocation.MeetingRoom, 22));
                    common.Add(new Candidate(OfficeMicroAction.ReadingDocument, "filing:main", OfficeSemanticLocation.Filing, 18));
                    common.Add(new Candidate(OfficeMicroAction.Typing, desk, OfficeSemanticLocation.Desk, 10));
                    break;
                case FamilyRole.Mother:
                    common.Add(new Candidate(OfficeMicroAction.TidyingDesk, desk, OfficeSemanticLocation.Desk, 20));
                    common.Add(new Candidate(OfficeMicroAction.FilingDocument, "filing:main", OfficeSemanticLocation.Filing, 22));
                    common.Add(new Candidate(OfficeMicroAction.UsingCopier, "copier:main", OfficeSemanticLocation.Printer, 18));
                    common.Add(new Candidate(OfficeMicroAction.DrinkingCoffee, "coffee:main", OfficeSemanticLocation.Coffee, 15));
                    break;
            }
            if (member.Autonomy.MicroAction.TargetLocation != OfficeSemanticLocation.Desk)
                common.Add(new Candidate(OfficeMicroAction.ReturningToDesk, desk, OfficeSemanticLocation.Desk, 12));
            return common;
        }

        private static bool IsAllowed(
            FamilyState family,
            FamilyMemberState member,
            Candidate candidate,
            long minute)
        {
            OfficeMicroActionState state = member.Autonomy.MicroAction;
            if (candidate.Action == state.Action || candidate.Action == state.LastAction) return false;
            if (candidate.Action == OfficeMicroAction.DrinkingWater &&
                minute - state.LastWaterStartedMinute < OfficeMicroActionRules.DrinkCooldownMinutes) return false;
            if (candidate.Action == OfficeMicroAction.DrinkingCoffee &&
                minute - state.LastCoffeeStartedMinute < OfficeMicroActionRules.DrinkCooldownMinutes) return false;
            if (string.Equals(candidate.TargetId, state.LastTargetId, StringComparison.Ordinal) &&
                minute - state.LastTargetEndedMinute < OfficeMicroActionRules.SharedTargetCooldownMinutes) return false;
            bool filingCopierBounce =
                (state.Action == OfficeMicroAction.FilingDocument && candidate.Action == OfficeMicroAction.UsingCopier) ||
                (state.Action == OfficeMicroAction.UsingCopier && candidate.Action == OfficeMicroAction.FilingDocument);
            if (filingCopierBounce) return false;
            if (candidate.Location == OfficeSemanticLocation.Desk && state.DeskResidenceStartedMinute >= 0 &&
                minute - state.DeskResidenceStartedMinute >= OfficeMicroActionRules.MaximumDeskResidenceMinutes)
                return false;
            if (candidate.Action == OfficeMicroAction.ShortConversation) return true;
            int inUse = family.Members.Count(other =>
                !ReferenceEquals(other, member) &&
                other.Autonomy.MicroAction.EndsMinute > minute &&
                string.Equals(other.Autonomy.MicroAction.TargetId, candidate.TargetId, StringComparison.Ordinal));
            return inUse < OfficeMicroActionRules.Capacity(candidate.TargetId);
        }

        private static Candidate WeightedPick(
            int worldSeed,
            FamilyMemberState member,
            OfficeMicroActionState state,
            IReadOnlyList<Candidate> candidates)
        {
            int total = candidates.Sum(candidate => Math.Max(1, candidate.Weight));
            int roll = StableRandom.StableRandomInt(
                $"office-micro-v1:{worldSeed}:{member.MemberId}:{member.Autonomy.ActionStartedMinute}:{state.SequenceIndex + 1}",
                total);
            for (var index = 0; index < candidates.Count; index++)
            {
                roll -= Math.Max(1, candidates[index].Weight);
                if (roll < 0) return candidates[index];
            }
            return candidates[candidates.Count - 1];
        }

        private static bool TryBeginConversation(
            int worldSeed,
            FamilyState family,
            FamilyMemberState member,
            long minute)
        {
            FamilyMemberState[] partners = family.Members
                .Where(other => !ReferenceEquals(other, member) &&
                                OfficeMicroActionRules.IsOfficeMacro(other.Autonomy.CurrentAction) &&
                                (other.Autonomy.MicroAction.Action == OfficeMicroAction.None ||
                                 other.Autonomy.MicroAction.EndsMinute <= minute) &&
                                !(other.Autonomy.MicroAction.LastConversationPartnerId == member.MemberId &&
                                  minute - other.Autonomy.MicroAction.LastConversationStartedMinute <
                                  OfficeMicroActionRules.ConversationCooldownMinutes))
                .OrderBy(other => other.MemberId, StringComparer.Ordinal)
                .ToArray();
            if (partners.Length == 0) return false;
            int partnerIndex = StableRandom.StableRandomInt(
                $"office-micro-partner-v1:{worldSeed}:{member.MemberId}:{minute}:{member.Autonomy.MicroAction.SequenceIndex + 1}",
                partners.Length);
            FamilyMemberState partner = partners[partnerIndex];
            string first = string.CompareOrdinal(member.MemberId, partner.MemberId) < 0
                ? member.MemberId
                : partner.MemberId;
            string second = first == member.MemberId ? partner.MemberId : member.MemberId;
            string target = $"conversation:{first}:{second}:{minute}";
            int duration = DurationMinutes(
                worldSeed,
                first + ":" + second,
                member.Autonomy.MicroAction.SequenceIndex + 1,
                OfficeMicroAction.ShortConversation);
            member.Autonomy.MicroAction.Begin(
                OfficeMicroAction.ShortConversation,
                target,
                OfficeSemanticLocation.Lounge,
                minute,
                duration,
                member.Autonomy.ActionStartedMinute,
                partner.MemberId);
            partner.Autonomy.MicroAction.Begin(
                OfficeMicroAction.ShortConversation,
                target,
                OfficeSemanticLocation.Lounge,
                minute,
                duration,
                partner.Autonomy.ActionStartedMinute,
                member.MemberId);
            return true;
        }

        private static int DurationMinutes(
            int worldSeed,
            string memberId,
            int sequenceIndex,
            OfficeMicroAction action)
        {
            int minimum;
            int maximum;
            switch (action)
            {
                case OfficeMicroAction.Typing: minimum = 8; maximum = 18; break;
                case OfficeMicroAction.ReadingDocument: minimum = 3; maximum = 7; break;
                case OfficeMicroAction.FilingDocument: minimum = 2; maximum = 5; break;
                case OfficeMicroAction.UsingCopier: minimum = 3; maximum = 6; break;
                case OfficeMicroAction.DrinkingWater: minimum = 2; maximum = 4; break;
                case OfficeMicroAction.DrinkingCoffee: minimum = 4; maximum = 7; break;
                case OfficeMicroAction.Stretching: minimum = 1; maximum = 3; break;
                case OfficeMicroAction.LookingAround: minimum = 1; maximum = 2; break;
                case OfficeMicroAction.ShortConversation: minimum = 4; maximum = 8; break;
                case OfficeMicroAction.PhoneCall: minimum = 3; maximum = 8; break;
                case OfficeMicroAction.TidyingDesk: minimum = 3; maximum = 6; break;
                case OfficeMicroAction.PreparingMeeting: minimum = 4; maximum = 8; break;
                case OfficeMicroAction.ReturningToDesk: minimum = 2; maximum = 5; break;
                default: minimum = 1; maximum = 2; break;
            }
            return minimum + StableRandom.StableRandomInt(
                $"office-micro-duration-v1:{worldSeed}:{memberId}:{sequenceIndex}:{(int)action}",
                maximum - minimum + 1);
        }

        private static int CountBits(int value)
        {
            var count = 0;
            while (value != 0)
            {
                count += value & 1;
                value >>= 1;
            }
            return count;
        }
    }
}
