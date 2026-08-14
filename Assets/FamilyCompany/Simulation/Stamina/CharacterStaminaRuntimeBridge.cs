using System;

namespace FamilyCompany.Simulation.Stamina
{
    /// <summary>
    /// Transient bridge installed by the live office. GameState owns the semantic roster, while
    /// reservations, paths, actors, and claims remain outside Simulation and outside save data.
    /// </summary>
    public interface ICharacterStaminaRuntimeBridge
    {
        bool IsOfficeRecoveryAllowed(string characterId);
        StaminaActivityKind ResolveActivity(string characterId);
        void ProcessPendingDecisions(CharacterStaminaRoster roster, long gameTimeMinute);
    }
}
