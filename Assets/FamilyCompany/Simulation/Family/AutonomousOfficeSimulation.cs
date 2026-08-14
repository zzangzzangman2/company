using System;
using System.Linq;
using FamilyCompany.Simulation.Company;
using FamilyCompany.Simulation.Core;
using FamilyCompany.Simulation.Workforce;

namespace FamilyCompany.Simulation.Family
{
    public static class AutonomousOfficeSimulation
    {
        public const int PulseMinutes = 30;

        public static void EnsureIntents(int worldSeed, FamilyState family, long elapsedMinute)
        {
            if (family == null) throw new ArgumentNullException(nameof(family));
            if (elapsedMinute < 0) throw new ArgumentOutOfRangeException(nameof(elapsedMinute));
            foreach (var member in family.Members)
            {
                if (member.Autonomy.CurrentAction == AutonomousOfficeAction.Unassigned ||
                    member.Autonomy.ActionEndsMinute <= elapsedMinute)
                {
                    ChooseNextAction(worldSeed, family, member, elapsedMinute);
                }
            }
            OfficePresentationMicroActionSimulation.EnsureActions(worldSeed, family, elapsedMinute);
        }

        public static void AdvanceTo(int worldSeed, FamilyState family, long elapsedMinute)
        {
            if (family == null) throw new ArgumentNullException(nameof(family));
            if (elapsedMinute < 0) throw new ArgumentOutOfRangeException(nameof(elapsedMinute));
            var firstProcessedMinute = family.Members[0].Autonomy.LastProcessedMinute;
            if (family.Members.Any(item => item.Autonomy.LastProcessedMinute != firstProcessedMinute))
            {
                throw new InvalidOperationException("All family autonomy states must share one processed minute.");
            }
            if (firstProcessedMinute > elapsedMinute)
            {
                throw new InvalidOperationException("Autonomy time moved backwards.");
            }

            foreach (var member in family.Members)
            {
                var autonomy = member.Autonomy;
                if (autonomy.CurrentAction == AutonomousOfficeAction.Unassigned ||
                    autonomy.ActionEndsMinute <= autonomy.LastProcessedMinute)
                {
                    ChooseNextAction(worldSeed, family, member, autonomy.LastProcessedMinute);
                }
            }
            OfficePresentationMicroActionSimulation.EnsureActions(worldSeed, family, firstProcessedMinute);

            var cursor = firstProcessedMinute;
            while (NextPulseBoundary(cursor) <= elapsedMinute)
            {
                var boundary = NextPulseBoundary(cursor);
                if (boundary > 0)
                    OfficePresentationMicroActionSimulation.AdvanceTo(worldSeed, family, boundary - 1);
                foreach (var member in family.Members)
                {
                    ApplyPulse(member);
                }

                foreach (var member in family.Members)
                {
                    member.Autonomy.MarkProcessed(boundary);
                }

                foreach (var member in family.Members)
                {
                    var autonomy = member.Autonomy;
                    if (boundary >= autonomy.ActionEndsMinute ||
                        NeedsEmergencyRecovery(member) ||
                        ScheduleRequiresActionChange(member, boundary))
                    {
                        ChooseNextAction(worldSeed, family, member, boundary);
                    }
                }
                OfficePresentationMicroActionSimulation.EnsureActions(worldSeed, family, boundary);

                cursor = boundary;
            }

            OfficePresentationMicroActionSimulation.AdvanceTo(worldSeed, family, elapsedMinute);

            foreach (var member in family.Members)
            {
                member.Autonomy.MarkProcessed(elapsedMinute);
            }
        }

        private static long NextPulseBoundary(long minute)
        {
            return checked(((minute / PulseMinutes) + 1L) * PulseMinutes);
        }

        private static bool NeedsEmergencyRecovery(FamilyMemberState member)
        {
            return member.Energy <= 5 || member.Stress >= 95;
        }

        private static bool ScheduleRequiresActionChange(FamilyMemberState member, long minute)
        {
            OfficeAttendancePhase attendance = OfficeAttendanceRules.Resolve(
                GameTime.CampaignStart.AddMinutes(minute));
            var schedule = FamilyScheduleRules.Resolve(
                member.Role,
                GameTime.CampaignStart.AddMinutes(minute));
            var action = member.Autonomy.CurrentAction;
            var scheduledAction = action == AutonomousOfficeAction.School ||
                                  action == AutonomousOfficeAction.OutsideSales ||
                                  action == AutonomousOfficeAction.HouseholdDuty ||
                                  action == AutonomousOfficeAction.OutsideCommitment ||
                                  action == AutonomousOfficeAction.Sleep ||
                                  action == AutonomousOfficeAction.OffDuty;
            if (attendance == OfficeAttendancePhase.Working && schedule.CanPerformCompanyWork)
                return scheduledAction;
            if (action == AutonomousOfficeAction.BurnoutRecovery || action == AutonomousOfficeAction.DeepRest)
                return false;
            return !scheduledAction;
        }

        private static void ApplyPulse(FamilyMemberState member)
        {
            var action = member.Autonomy.CurrentAction;
            switch (action)
            {
                case AutonomousOfficeAction.FocusWork:
                case AutonomousOfficeAction.Administration:
                case AutonomousOfficeAction.Reception:
                case AutonomousOfficeAction.Printing:
                case AutonomousOfficeAction.Meeting:
                    const int energyCost = 2;
                    var baseStressGain = action == AutonomousOfficeAction.Meeting &&
                                         member.Capability.Skills.Collaboration < 60 ? 4 : 3;
                    var stressGain = WorkforceStressRules.ApplyAuthoritativeStressGain(
                        baseStressGain,
                        member.Capability.StressGainBasisPoints);
                    member.ChangeEnergy(-energyCost);
                    member.ChangeStress(stressGain);
                    member.Autonomy.CompleteWorkBlock();
                    break;

                case AutonomousOfficeAction.ShortBreak:
                    member.ChangeEnergy(6);
                    member.ChangeStress(-5);
                    member.Autonomy.CompleteBreak();
                    break;

                case AutonomousOfficeAction.DeepRest:
                    member.ChangeEnergy(8);
                    member.ChangeStress(-7);
                    member.Autonomy.CompleteBreak();
                    break;

                case AutonomousOfficeAction.CoffeeBreak:
                    member.ChangeEnergy(10);
                    member.ChangeStress(2);
                    member.Autonomy.CompleteBreak();
                    break;

                case AutonomousOfficeAction.SocialChat:
                    member.ChangeEnergy(3);
                    member.ChangeStress(-6);
                    member.Autonomy.CompleteBreak();
                    break;

                case AutonomousOfficeAction.BurnoutRecovery:
                    member.ChangeEnergy(10);
                    member.ChangeStress(-9);
                    member.Autonomy.CompleteBreak();
                    break;

                case AutonomousOfficeAction.OffDuty:
                    member.ChangeEnergy(6);
                    member.ChangeStress(-5);
                    break;

                case AutonomousOfficeAction.School:
                    member.ChangeEnergy(-1);
                    member.ChangeStress(1);
                    break;

                case AutonomousOfficeAction.OutsideSales:
                    member.ChangeEnergy(-2);
                    member.ChangeStress(1);
                    break;

                case AutonomousOfficeAction.HouseholdDuty:
                case AutonomousOfficeAction.OutsideCommitment:
                    member.ChangeEnergy(-1);
                    break;

                case AutonomousOfficeAction.Sleep:
                    member.ChangeEnergy(8);
                    member.ChangeStress(-6);
                    break;
            }
        }

        private static void ChooseNextAction(
            int worldSeed,
            FamilyState family,
            FamilyMemberState member,
            long minute)
        {
            var autonomy = member.Autonomy;
            if (member.Energy <= 5 || member.Stress >= 95)
            {
                autonomy.Begin(AutonomousOfficeAction.BurnoutRecovery, OfficeSemanticLocation.Lounge, minute, 120);
                autonomy.RecordBurnout($"{member.DisplayName}이 한계에 도달해 모든 일을 멈추고 쉬기 시작했다.", minute);
                return;
            }

            var now = GameTime.CampaignStart.AddMinutes(minute);
            OfficeAttendancePhase attendance = OfficeAttendanceRules.Resolve(now);
            if (attendance != OfficeAttendancePhase.Working)
            {
                int duration = attendance == OfficeAttendancePhase.BeforeWork
                    ? Math.Max(
                        1,
                        OfficeAttendanceRules.WorkStartsMinuteOfDay -
                        checked(now.Hour * 60 + now.Minute))
                    : PulseMinutes;
                // The first campaign action begins at 08:50 and must expire exactly at 09:00.
                // Otherwise the presentation admits actors while stale OffDuty intent immediately
                // sends NPCs back through the exit until the next generic 30-minute pulse.
                autonomy.Begin(AutonomousOfficeAction.OffDuty, OfficeSemanticLocation.Exit, minute, duration);
                return;
            }
            var schedule = FamilyScheduleRules.Resolve(member.Role, now);
            if (!schedule.CanPerformCompanyWork)
            {
                BeginScheduledCommitment(member, schedule, minute);
                return;
            }

            if (member.Energy <= 25 || member.Stress >= 82)
            {
                autonomy.Begin(AutonomousOfficeAction.DeepRest, OfficeSemanticLocation.Lounge, minute, 90);
                autonomy.RecordIncident($"{member.DisplayName}이 휴게실 소파에서 깜빡 잠들었다.", minute);
                return;
            }

            if (member.Energy <= 45 || member.Stress >= 67)
            {
                var recoveryChoice = StableRandom.StableRandomInt(
                    $"office-recovery:{worldSeed}:{member.MemberId}:{minute}",
                    100);
                if (member.Stress >= 67 && recoveryChoice < 45)
                {
                    BeginSocialChat(worldSeed, family, member, minute);
                    return;
                }

                if (member.Energy <= 38 && recoveryChoice >= 72)
                {
                    autonomy.Begin(AutonomousOfficeAction.CoffeeBreak, OfficeSemanticLocation.Lounge, minute, PulseMinutes);
                    autonomy.RecordIncident($"{member.DisplayName}이 종이컵 커피로 버티기로 했다.", minute);
                    return;
                }

                autonomy.Begin(AutonomousOfficeAction.ShortBreak, OfficeSemanticLocation.Lounge, minute, 60);
                autonomy.RecordIncident($"{member.DisplayName}이 눈치를 보다 휴게실로 향했다.", minute);
                return;
            }

            var socialRoll = StableRandom.StableRandomInt(
                $"office-social-roll:{worldSeed}:{member.MemberId}:{minute}",
                100);
            if (member.Stress >= 50 && socialRoll < 25)
            {
                BeginSocialChat(worldSeed, family, member, minute);
                return;
            }

            BeginRoleWork(worldSeed, member, minute);
        }

        private static void BeginScheduledCommitment(
            FamilyMemberState member,
            FamilyScheduleSlot schedule,
            long minute)
        {
            var action = AutonomousOfficeAction.OffDuty;
            var location = OfficeSemanticLocation.Lounge;
            switch (schedule.Kind)
            {
                case FamilyScheduleKind.School:
                    action = AutonomousOfficeAction.School;
                    location = OfficeSemanticLocation.Exit;
                    break;
                case FamilyScheduleKind.OutsideSales:
                    action = AutonomousOfficeAction.OutsideSales;
                    location = OfficeSemanticLocation.Exit;
                    break;
                case FamilyScheduleKind.HouseholdDuty:
                    action = AutonomousOfficeAction.HouseholdDuty;
                    location = OfficeSemanticLocation.Exit;
                    break;
                case FamilyScheduleKind.OutsideCommitment:
                    action = AutonomousOfficeAction.OutsideCommitment;
                    location = OfficeSemanticLocation.Exit;
                    break;
                case FamilyScheduleKind.Sleep:
                    action = AutonomousOfficeAction.Sleep;
                    location = OfficeSemanticLocation.Exit;
                    break;
            }

            member.Autonomy.Begin(action, location, minute, PulseMinutes);
        }

        private static void BeginRoleWork(int worldSeed, FamilyMemberState member, long minute)
        {
            var roll = StableRandom.StableRandomInt(
                $"office-role-work:{worldSeed}:{member.MemberId}:{minute}",
                100);
            var duration = 60 + StableRandom.StableRandomInt(
                $"office-role-duration:{worldSeed}:{member.MemberId}:{minute}",
                3) * PulseMinutes;
            var action = AutonomousOfficeAction.FocusWork;
            var location = OfficeSemanticLocation.Desk;

            switch (member.Role)
            {
                case FamilyRole.OlderSister:
                    if (roll < 35)
                    {
                        action = AutonomousOfficeAction.Reception;
                        location = OfficeSemanticLocation.Reception;
                    }
                    else if (roll < 55)
                    {
                        action = AutonomousOfficeAction.Meeting;
                        location = OfficeSemanticLocation.MeetingRoom;
                    }
                    else if (roll < 68)
                    {
                        action = AutonomousOfficeAction.Printing;
                        location = OfficeSemanticLocation.Printer;
                    }
                    else
                    {
                        action = AutonomousOfficeAction.Administration;
                    }
                    break;

                case FamilyRole.Father:
                    if (roll < 38)
                    {
                        action = AutonomousOfficeAction.Reception;
                        location = OfficeSemanticLocation.Reception;
                    }
                    else if (roll < 68)
                    {
                        action = AutonomousOfficeAction.Meeting;
                        location = OfficeSemanticLocation.MeetingRoom;
                    }
                    else
                    {
                        action = AutonomousOfficeAction.Administration;
                    }
                    break;

                case FamilyRole.Mother:
                    if (roll < 30)
                    {
                        action = AutonomousOfficeAction.Printing;
                        location = OfficeSemanticLocation.Printer;
                    }
                    else if (roll < 48)
                    {
                        action = AutonomousOfficeAction.Meeting;
                        location = OfficeSemanticLocation.MeetingRoom;
                    }
                    else
                    {
                        action = AutonomousOfficeAction.Administration;
                    }
                    break;

                case FamilyRole.Player:
                    action = roll < 75
                        ? AutonomousOfficeAction.FocusWork
                        : AutonomousOfficeAction.Meeting;
                    location = action == AutonomousOfficeAction.Meeting
                        ? OfficeSemanticLocation.MeetingRoom
                        : OfficeSemanticLocation.Desk;
                    break;
            }

            member.Autonomy.Begin(action, location, minute, duration);
            if (action == AutonomousOfficeAction.Printing && roll % 7 == 0)
            {
                member.Autonomy.RecordIncident($"{member.DisplayName}이 말썽 난 프린터의 종이 걸림을 해결했다.", minute);
            }
        }

        private static void BeginSocialChat(
            int worldSeed,
            FamilyState family,
            FamilyMemberState member,
            long minute)
        {
            member.Autonomy.Begin(
                AutonomousOfficeAction.SocialChat,
                OfficeSemanticLocation.Lounge,
                minute,
                PulseMinutes);
            var others = family.Members.Where(item => item.MemberId != member.MemberId).ToArray();
            if (others.Length == 0) return;
            var other = others[StableRandom.StableRandomInt(
                $"office-chat-partner:{worldSeed}:{member.MemberId}:{minute}",
                others.Length)];
            var day = minute / 1440L;
            var pair = string.CompareOrdinal(member.MemberId, other.MemberId) < 0
                ? $"{member.MemberId}:{other.MemberId}"
                : $"{other.MemberId}:{member.MemberId}";
            var tense = member.Stress >= 75 && other.Stress >= 65;
            var summary = tense
                ? $"{member.DisplayName}과 {other.DisplayName}이 예민한 상태에서 말다툼했다."
                : $"{member.DisplayName}과 {other.DisplayName}이 휴게실에서 속마음을 나눴다.";
            family.RecordPairCareerMemory(
                $"office-social:{pair}:{day}",
                member.MemberId,
                other.MemberId,
                BusinessIndustry.WebAndSoftware,
                tense ? CareerMemoryKind.OfficeConflict : CareerMemoryKind.OfficeBond,
                summary,
                minute,
                tense ? -1 : 1);
            member.Autonomy.MarkSocialEventDay(day);
            member.Autonomy.RecordIncident(summary, minute);
        }
    }
}
