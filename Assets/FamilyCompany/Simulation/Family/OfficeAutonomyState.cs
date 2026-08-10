using System;

namespace FamilyCompany.Simulation.Family
{
    public enum AutonomousOfficeAction
    {
        Unassigned = 0,
        FocusWork = 1,
        Administration = 2,
        Reception = 3,
        Printing = 4,
        Meeting = 5,
        ShortBreak = 6,
        DeepRest = 7,
        CoffeeBreak = 8,
        SocialChat = 9,
        OffDuty = 10,
        BurnoutRecovery = 11,
        School = 12,
        OutsideSales = 13,
        HouseholdDuty = 14,
        OutsideCommitment = 15,
        Sleep = 16
    }

    public enum OfficeSemanticLocation
    {
        None = 0,
        Desk = 1,
        Reception = 2,
        Printer = 3,
        MeetingRoom = 4,
        Lounge = 5,
        Exit = 6
    }

    public sealed class OfficeAutonomyState
    {
        public OfficeAutonomyState(
            AutonomousOfficeAction currentAction = AutonomousOfficeAction.Unassigned,
            OfficeSemanticLocation targetLocation = OfficeSemanticLocation.None,
            long actionStartedMinute = 0,
            long actionEndsMinute = 0,
            long lastProcessedMinute = 0,
            int completedWorkBlocks = 0,
            int completedBreaks = 0,
            int burnoutCount = 0,
            string lastIncidentSummary = "",
            long lastIncidentMinute = -1,
            long lastSocialEventDay = -1)
        {
            if (actionStartedMinute < 0) throw new ArgumentOutOfRangeException(nameof(actionStartedMinute));
            if (actionEndsMinute < 0) throw new ArgumentOutOfRangeException(nameof(actionEndsMinute));
            if (lastProcessedMinute < 0) throw new ArgumentOutOfRangeException(nameof(lastProcessedMinute));
            if (completedWorkBlocks < 0) throw new ArgumentOutOfRangeException(nameof(completedWorkBlocks));
            if (completedBreaks < 0) throw new ArgumentOutOfRangeException(nameof(completedBreaks));
            if (burnoutCount < 0) throw new ArgumentOutOfRangeException(nameof(burnoutCount));
            if (lastIncidentMinute < -1) throw new ArgumentOutOfRangeException(nameof(lastIncidentMinute));
            if (lastSocialEventDay < -1) throw new ArgumentOutOfRangeException(nameof(lastSocialEventDay));

            CurrentAction = currentAction;
            TargetLocation = targetLocation;
            ActionStartedMinute = actionStartedMinute;
            ActionEndsMinute = actionEndsMinute;
            LastProcessedMinute = lastProcessedMinute;
            CompletedWorkBlocks = completedWorkBlocks;
            CompletedBreaks = completedBreaks;
            BurnoutCount = burnoutCount;
            LastIncidentSummary = lastIncidentSummary ?? string.Empty;
            LastIncidentMinute = lastIncidentMinute;
            LastSocialEventDay = lastSocialEventDay;
        }

        public AutonomousOfficeAction CurrentAction { get; private set; }
        public OfficeSemanticLocation TargetLocation { get; private set; }
        public long ActionStartedMinute { get; private set; }
        public long ActionEndsMinute { get; private set; }
        public long LastProcessedMinute { get; private set; }
        public int CompletedWorkBlocks { get; private set; }
        public int CompletedBreaks { get; private set; }
        public int BurnoutCount { get; private set; }
        public string LastIncidentSummary { get; private set; }
        public long LastIncidentMinute { get; private set; }
        public long LastSocialEventDay { get; private set; }

        public string ActionLabel
        {
            get
            {
                switch (CurrentAction)
                {
                    case AutonomousOfficeAction.FocusWork: return "집중 업무";
                    case AutonomousOfficeAction.Administration: return "서류 정리";
                    case AutonomousOfficeAction.Reception: return "고객 응대";
                    case AutonomousOfficeAction.Printing: return "출력·검수";
                    case AutonomousOfficeAction.Meeting: return "업무 회의";
                    case AutonomousOfficeAction.ShortBreak: return "잠깐 휴식";
                    case AutonomousOfficeAction.DeepRest: return "소파 휴식";
                    case AutonomousOfficeAction.CoffeeBreak: return "커피 충전";
                    case AutonomousOfficeAction.SocialChat: return "가족 수다";
                    case AutonomousOfficeAction.OffDuty: return "퇴근 후 회복";
                    case AutonomousOfficeAction.BurnoutRecovery: return "번아웃 회복";
                    case AutonomousOfficeAction.School: return "학교 수업";
                    case AutonomousOfficeAction.OutsideSales: return "외부 영업";
                    case AutonomousOfficeAction.HouseholdDuty: return "가사·저녁 준비";
                    case AutonomousOfficeAction.OutsideCommitment: return "외부 일정";
                    case AutonomousOfficeAction.Sleep: return "수면";
                    default: return "다음 일 생각 중";
                }
            }
        }

        public string MoodLabel(int energy, int stress)
        {
            if (energy <= 8 || stress >= 92) return "한계";
            if (energy <= 25) return "완전 지침";
            if (stress >= 75) return "예민함";
            if (energy <= 45 || stress >= 55) return "피곤함";
            if (energy >= 75 && stress <= 25) return "기운 좋음";
            return "괜찮음";
        }

        internal void Begin(
            AutonomousOfficeAction action,
            OfficeSemanticLocation location,
            long startedMinute,
            int durationMinutes)
        {
            if (startedMinute < 0) throw new ArgumentOutOfRangeException(nameof(startedMinute));
            if (durationMinutes <= 0) throw new ArgumentOutOfRangeException(nameof(durationMinutes));
            CurrentAction = action;
            TargetLocation = location;
            ActionStartedMinute = startedMinute;
            ActionEndsMinute = checked(startedMinute + durationMinutes);
        }

        internal void MarkProcessed(long minute)
        {
            if (minute < LastProcessedMinute) throw new ArgumentOutOfRangeException(nameof(minute));
            LastProcessedMinute = minute;
        }

        internal void CompleteWorkBlock() => CompletedWorkBlocks++;
        internal void CompleteBreak() => CompletedBreaks++;

        internal void RecordBurnout(string summary, long minute)
        {
            BurnoutCount++;
            RecordIncident(summary, minute);
        }

        internal void RecordIncident(string summary, long minute)
        {
            LastIncidentSummary = summary ?? string.Empty;
            LastIncidentMinute = minute;
        }

        internal void MarkSocialEventDay(long day) => LastSocialEventDay = day;
    }
}
