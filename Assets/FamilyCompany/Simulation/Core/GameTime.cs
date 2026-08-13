using System;

namespace FamilyCompany.Simulation.Core
{
    public sealed class GameTime
    {
        public static readonly DateTime CampaignStart = new DateTime(2000, 1, 3, 8, 50, 0, DateTimeKind.Unspecified);

        public GameTime(long elapsedMinutes = 0)
        {
            if (elapsedMinutes < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedMinutes));
            }

            ElapsedMinutes = elapsedMinutes;
        }

        public long ElapsedMinutes { get; private set; }
        public DateTime Now => CampaignStart.AddMinutes(ElapsedMinutes);

        public void Advance(long minutes)
        {
            if (minutes < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(minutes));
            }

            ElapsedMinutes = checked(ElapsedMinutes + minutes);
        }

        public int AgeOn(DateTime birthDate)
        {
            var now = Now.Date;
            var age = now.Year - birthDate.Year;
            if (birthDate.Date > now.AddYears(-age))
            {
                age--;
            }

            return age;
        }
    }
}
