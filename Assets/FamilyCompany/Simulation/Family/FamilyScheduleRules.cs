using System;

namespace FamilyCompany.Simulation.Family
{
    public enum FamilyScheduleKind
    {
        CompanyTime = 0,
        School = 1,
        OutsideSales = 2,
        HouseholdDuty = 3,
        OutsideCommitment = 4,
        PersonalTime = 5,
        Sleep = 6
    }

    public sealed class FamilyScheduleSlot
    {
        public FamilyScheduleSlot(FamilyScheduleKind kind, string label, bool canPerformCompanyWork)
        {
            Kind = kind;
            Label = label ?? string.Empty;
            CanPerformCompanyWork = canPerformCompanyWork;
        }

        public FamilyScheduleKind Kind { get; }
        public string Label { get; }
        public bool CanPerformCompanyWork { get; }
    }

    public static class FamilyScheduleRules
    {
        private static readonly FamilyScheduleSlot CompanyTime =
            new FamilyScheduleSlot(FamilyScheduleKind.CompanyTime, "회사 일 가능", true);

        public static FamilyScheduleSlot Resolve(FamilyRole role, DateTime now)
        {
            var hour = now.Hour;
            var weekend = now.DayOfWeek == DayOfWeek.Saturday || now.DayOfWeek == DayOfWeek.Sunday;
            if (hour >= 23 || hour < 7)
            {
                return new FamilyScheduleSlot(FamilyScheduleKind.Sleep, "수면 시간", false);
            }

            if (hour < 8)
            {
                return new FamilyScheduleSlot(FamilyScheduleKind.PersonalTime, "출근 준비", false);
            }

            switch (role)
            {
                case FamilyRole.Player:
                    if (!weekend && hour >= 9 && hour < 18)
                        return new FamilyScheduleSlot(FamilyScheduleKind.School, "학교 수업", false);
                    break;

                case FamilyRole.OlderSister:
                    if (!weekend && hour >= 9 && hour < 14)
                        return new FamilyScheduleSlot(FamilyScheduleKind.OutsideCommitment, "학교·외부 일정", false);
                    if (now.DayOfWeek == DayOfWeek.Saturday && hour >= 10 && hour < 15)
                        return new FamilyScheduleSlot(FamilyScheduleKind.OutsideCommitment, "주말 아르바이트", false);
                    break;

                case FamilyRole.Father:
                    if (!weekend && hour >= 10 && hour < 17)
                        return new FamilyScheduleSlot(FamilyScheduleKind.OutsideSales, "대외 영업", false);
                    break;

                case FamilyRole.Mother:
                    if (hour >= 18 && hour < 20)
                        return new FamilyScheduleSlot(FamilyScheduleKind.HouseholdDuty, "가사·저녁 준비", false);
                    if (weekend && hour >= 8 && hour < 11)
                        return new FamilyScheduleSlot(FamilyScheduleKind.HouseholdDuty, "주말 가사", false);
                    break;
            }

            return CompanyTime;
        }
    }
}
