using System;
using System.Collections.Generic;
using System.Linq;
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
            int stress = 0,
            EmployeeStats stats = null,
            IEnumerable<CareerMemoryState> careerMemories = null,
            OfficeAutonomyState autonomy = null)
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
            Stats = stats ?? EmployeeStats.StarterFor(role);
            Autonomy = autonomy ?? new OfficeAutonomyState();
            _careerMemories = careerMemories == null
                ? new List<CareerMemoryState>()
                : new List<CareerMemoryState>(careerMemories);
            if (_careerMemories.Select(item => item.MemoryId).Distinct(StringComparer.Ordinal).Count() != _careerMemories.Count)
            {
                throw new InvalidOperationException("Career memory IDs must be unique per member.");
            }
        }

        public string MemberId { get; }
        public string DisplayName { get; }
        public FamilyRole Role { get; }
        public DateTime BirthDate { get; }
        public string CompanyDuty { get; }
        public int Energy { get; private set; }
        public int Trust { get; private set; }
        public int Stress { get; private set; }
        public EmployeeStats Stats { get; }
        public OfficeAutonomyState Autonomy { get; }
        private readonly List<CareerMemoryState> _careerMemories;
        public IReadOnlyList<CareerMemoryState> CareerMemories => _careerMemories;

        public int AgeAt(GameTime time)
        {
            return time.AgeOn(BirthDate);
        }

        public void ChangeEnergy(int delta) => Energy = Clamp100(Energy + delta);
        internal void SetEnergyProjection(int percent) => Energy = Clamp100(percent);
        public void ChangeTrust(int delta) => Trust = Clamp100(Trust + delta);
        public void ChangeStress(int delta) => Stress = Clamp100(Stress + delta);

        public void RecordCareerMemory(CareerMemoryState memory)
        {
            if (memory == null) throw new ArgumentNullException(nameof(memory));
            if (_careerMemories.Any(item => item.MemoryId == memory.MemoryId)) return;
            _careerMemories.Add(memory);
            ChangeTrust(memory.BondDelta);
        }

        private static int Clamp100(int value)
        {
            return Math.Max(0, Math.Min(100, value));
        }
    }
}
