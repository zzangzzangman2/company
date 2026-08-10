using System;
using System.Collections.Generic;
using FamilyCompany.Simulation.Events;
using FamilyCompany.Simulation.Family;
using FamilyCompany.Simulation.Game;

namespace FamilyCompany.Simulation.Prototype
{
    public sealed class SimulationRunner
    {
        private readonly GameState _state;

        public SimulationRunner(GameState state)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
        }

        public IReadOnlyList<ScheduledEvent> AdvanceMinutes(long minutes)
        {
            if (minutes < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(minutes));
            }

            _state.Time.Advance(minutes);
            AutonomousOfficeSimulation.AdvanceTo(
                _state.WorldSeed,
                _state.Family,
                _state.Time.ElapsedMinutes);
            _state.Contracts.FailOverdue(_state.Time.ElapsedMinutes, _state.Company, _state.Family);
            _state.Growth.ResolveProductIfDue(
                _state.WorldSeed,
                _state.Time.ElapsedMinutes,
                _state.Family,
                _state.Company);
            var due = _state.Events.DequeueDue(_state.Time.ElapsedMinutes);
            foreach (var scheduledEvent in due)
            {
                Apply(scheduledEvent);
            }

            return due;
        }

        private void Apply(ScheduledEvent scheduledEvent)
        {
            if (scheduledEvent.Kind == "family_briefing")
            {
                foreach (var member in _state.Family.Members)
                {
                    member.ChangeTrust(1);
                }
            }
        }
    }
}
