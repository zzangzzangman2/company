using System;
using FamilyCompany.Simulation.Family;

namespace FamilyCompany.Simulation.Workforce
{
    public static class WorkforceStarterCapabilityCatalog
    {
        public static WorkforceCapabilityState Create(string memberId, FamilyRole role)
        {
            switch (role)
            {
                case FamilyRole.Player:
                    return State(memberId, 58, 61, 47, 32, 62, 55, 86, 58);
                case FamilyRole.OlderSister:
                    return State(memberId, 37, 52, 44, 55, 62, 72, 74, 65);
                case FamilyRole.Father:
                    return State(memberId, 24, 45, 23, 68, 54, 61, 48, 72);
                case FamilyRole.Mother:
                    return State(memberId, 32, 55, 35, 46, 60, 70, 57, 76);
                default: throw new ArgumentOutOfRangeException(nameof(role));
            }
        }

        private static WorkforceCapabilityState State(
            string memberId,
            int engineering,
            int planning,
            int creative,
            int business,
            int operations,
            int collaboration,
            int potential,
            int stressResistance)
        {
            return new WorkforceCapabilityState(
                memberId,
                new WorkSkillSet(engineering, planning, creative, business, operations, collaboration),
                potential,
                WorkforceStressRules.ClampStressGainBasisPoints(12_000 - stressResistance * 40));
        }
    }

    /// <summary>
    /// The only authority boundary allowed to read the removed legacy Speed, Stamina and Mental
    /// values. It is used only once while migrating a v1-v9 save into the v10 capability snapshot.
    /// </summary>
    public static class LegacyWorkforceCapabilityMigration
    {
        public static WorkforceCapabilityState Migrate(string memberId, EmployeeStats legacy)
        {
            if (legacy == null) throw new ArgumentNullException(nameof(legacy));
            var operations = checked((legacy.Speed + legacy.Planning + legacy.Stamina + 1) / 3);
            var stressGainBasisPoints = WorkforceStressRules.ClampStressGainBasisPoints(
                12_000 - legacy.Mental * 40);
            return new WorkforceCapabilityState(
                memberId,
                new WorkSkillSet(
                    legacy.Development,
                    legacy.Planning,
                    legacy.Art,
                    legacy.Sales,
                    operations,
                    legacy.Teamwork),
                legacy.Potential,
                stressGainBasisPoints);
        }
    }
}
