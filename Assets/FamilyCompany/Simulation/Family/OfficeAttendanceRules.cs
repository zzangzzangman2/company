using System;
using FamilyCompany.Simulation.Core;

namespace FamilyCompany.Simulation.Family
{
    public enum OfficeAttendancePhase
    {
        BeforeWork = 0,
        Working = 1,
        AfterWork = 2
    }

    /// <summary>
    /// Presentation-level office attendance shared by family and future hired actors.
    /// The common 09:00-18:00 company shift is shared with FamilyScheduleRules; this rule owns
    /// the visible door-entry/departure sequence for family and hired staff.
    /// </summary>
    public static class OfficeAttendanceRules
    {
        public const int WorkStartsMinuteOfDay = 9 * 60;
        public const int WorkEndsMinuteOfDay = 18 * 60;
        public const int StaggerGameMinutes = 1;

        // On the founding morning all four family members are already inside the empty office.
        // Later days retain the normal staggered 09:00 arrival and 18:00 departure contract.
        public static bool IsFoundingMorning(DateTime now) =>
            now >= GameTime.CampaignStart &&
            now < GameTime.CampaignStart.Date.AddMinutes(WorkStartsMinuteOfDay);

        public static OfficeAttendancePhase ResolveOfficePresentation(DateTime now) =>
            IsFoundingMorning(now) ? OfficeAttendancePhase.Working : Resolve(now);

        public static OfficeAttendancePhase Resolve(DateTime now)
        {
            if (now.DayOfWeek == DayOfWeek.Saturday || now.DayOfWeek == DayOfWeek.Sunday)
                return OfficeAttendancePhase.AfterWork;
            int minute = checked(now.Hour * 60 + now.Minute);
            if (minute < WorkStartsMinuteOfDay) return OfficeAttendancePhase.BeforeWork;
            return minute < WorkEndsMinuteOfDay
                ? OfficeAttendancePhase.Working
                : OfficeAttendancePhase.AfterWork;
        }

        public static int ArrivalMinuteOfDay(int orderedIndex) => checked(
            WorkStartsMinuteOfDay + Math.Max(0, orderedIndex) * StaggerGameMinutes);

        public static bool HasArrived(DateTime now, int orderedIndex)
        {
            if (Resolve(now) != OfficeAttendancePhase.Working) return false;
            int minute = checked(now.Hour * 60 + now.Minute);
            return minute >= ArrivalMinuteOfDay(orderedIndex);
        }
    }
}
