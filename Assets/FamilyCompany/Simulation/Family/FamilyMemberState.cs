using System;
using FamilyCompany.Simulation.Core;

namespace FamilyCompany.Simulation.Family
{
    public sealed class FamilyMemberState
    {
        public FamilyMemberState(
            string memberId,
            string displayName,
            FamilyRole role,
            DateTime birthDate,
            string companyDuty,
            int energy = 100,
            int trust = 50,
            int stress = 0)
        {
            if (string.IsNullOrWhiteSpace(memberId))
            {
                throw new ArgumentException("Member ID is required.", nameof(memberId));
            }

            MemberId = memberId;
            DisplayName = displayName ?? string.Empty;
            Role = role;
            BirthDate = birthDate.Date;
            CompanyDuty = companyDuty ?? string.Empty;
            Energy = Clamp100(energy);
            Trust = Clamp100(trust);
            Stress = Clamp100(stress);
        }

        public string MemberId { get; }
        public string DisplayName { get; }
        public FamilyRole Role { get; }
        public DateTime BirthDate { get; }
        public string CompanyDuty { get; }
        public int Energy { get; private set; }
        public int Trust { get; private set; }
        public int Stress { get; private set; }

        public int AgeAt(GameTime time)
        {
            return time.AgeOn(BirthDate);
        }

        public void ChangeEnergy(int delta) => Energy = Clamp100(Energy + delta);
        public void ChangeTrust(int delta) => Trust = Clamp100(Trust + delta);
        public void ChangeStress(int delta) => Stress = Clamp100(Stress + delta);

        private static int Clamp100(int value)
        {
            return Math.Max(0, Math.Min(100, value));
        }
    }
}

