using System;

namespace FamilyCompany.Simulation.Family
{
    public sealed class EmployeeStats
    {
        public EmployeeStats(
            int development,
            int speed,
            int stamina,
            int planning,
            int art,
            int sales,
            int mental,
            int teamwork,
            int loyalty,
            int potential)
        {
            Development = Clamp100(development);
            Speed = Clamp100(speed);
            Stamina = Clamp100(stamina);
            Planning = Clamp100(planning);
            Art = Clamp100(art);
            Sales = Clamp100(sales);
            Mental = Clamp100(mental);
            Teamwork = Clamp100(teamwork);
            Loyalty = Clamp100(loyalty);
            Potential = Clamp100(potential);
        }

        public int Development { get; }
        public int Speed { get; }
        public int Stamina { get; }
        public int Planning { get; }
        public int Art { get; }
        public int Sales { get; }
        public int Mental { get; }
        public int Teamwork { get; }
        public int Loyalty { get; }
        public int Potential { get; }

        public static EmployeeStats StarterFor(FamilyRole role)
        {
            switch (role)
            {
                case FamilyRole.Player:
                    return new EmployeeStats(58, 52, 72, 61, 47, 32, 58, 55, 100, 86);
                case FamilyRole.OlderSister:
                    return new EmployeeStats(37, 58, 75, 52, 44, 55, 65, 72, 100, 74);
                case FamilyRole.Father:
                    return new EmployeeStats(24, 36, 82, 45, 23, 68, 72, 61, 100, 48);
                case FamilyRole.Mother:
                    return new EmployeeStats(32, 48, 78, 55, 35, 46, 76, 70, 100, 57);
                default:
                    throw new ArgumentOutOfRangeException(nameof(role));
            }
        }

        private static int Clamp100(int value)
        {
            return Math.Max(0, Math.Min(100, value));
        }
    }
}
