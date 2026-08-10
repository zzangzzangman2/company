using System;
using System.Collections.Generic;
using FamilyCompany.Simulation.Events;
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

            var oldDay = _state.Time.ElapsedMinutes / 1440;
            _state.Time.Advance(minutes);
            var newDay = _state.Time.ElapsedMinutes / 1440;
            for (var day = oldDay; day < newDay; day++)
            {
                foreach (var member in _state.Family.Members)
                {
                    member.ChangeEnergy(-8);
                    member.ChangeStress(1);
                }
            }

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

