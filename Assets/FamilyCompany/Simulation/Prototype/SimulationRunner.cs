using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Simulation.Events;
using FamilyCompany.Simulation.Family;
using FamilyCompany.Simulation.Game;
using FamilyCompany.Simulation.Stamina;

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

            long targetMinute = checked(_state.Time.ElapsedMinutes + minutes);
            var dueEvents = new List<ScheduledEvent>();
            if (minutes == 0)
            {
                ProcessWorldAtCurrentMinute(dueEvents);
                return dueEvents;
            }

            while (_state.Time.ElapsedMinutes < targetMinute)
            {
                long currentMinute = _state.Time.ElapsedMinutes;
                // Resolve the semantic intent at the current integer minute before sampling its
                // stamina activity. This keeps a single long jump identical to minute-sized calls;
                // otherwise only the latter observes the initial action chosen after minute one.
                AutonomousOfficeSimulation.EnsureIntents(
                    _state.WorldSeed,
                    _state.Family,
                    currentMinute);
                ICharacterStaminaRuntimeBridge runtimeBridge = _state.StaminaRuntimeBridge;
                _state.Stamina.SetActivitiesAtCurrentMinute(characterId =>
                    runtimeBridge?.ResolveActivity(characterId) ??
                    (FamilyScheduleRules.Resolve(_state.Family.Get(characterId).Role, _state.Time.Now).Kind == FamilyScheduleKind.Sleep
                        ? StaminaActivityKind.Sleep : ResolveSemanticActivity(_state.Family.Get(characterId))));

                long nextAutonomyBoundary = checked(
                    ((currentMinute / AutonomousOfficeSimulation.PulseMinutes) + 1L) *
                    AutonomousOfficeSimulation.PulseMinutes);
                foreach (FamilyMemberState member in _state.Family.Members)
                {
                    long actionEnd = member.Autonomy.ActionEndsMinute;
                    if (actionEnd > currentMinute && actionEnd < nextAutonomyBoundary)
                        nextAutonomyBoundary = actionEnd;
                }
                long requestedStep = Math.Min(targetMinute, nextAutonomyBoundary);
                CharacterStaminaRosterAdvanceResult staminaStep = _state.Stamina.AdvanceAllTo(
                    requestedStep,
                    characterId => runtimeBridge != null &&
                                   runtimeBridge.IsOfficeRecoveryAllowed(characterId));

                long delta = staminaStep.ProcessedToMinute - currentMinute;
                if (delta < 0)
                    throw new InvalidOperationException("Stamina advanced behind GameTime.");
                if (delta > 0) _state.Time.Advance(delta);
                ProcessWorldAtCurrentMinute(dueEvents);

                if (!staminaStep.RequiresRuntimeDecision) continue;
                if (runtimeBridge == null)
                    throw new InvalidOperationException(
                        "A stamina runtime decision was produced without a bound runtime bridge.");
                runtimeBridge.ProcessPendingDecisions(
                    _state.Stamina,
                    _state.Time.ElapsedMinutes);
                _state.RefreshLegacyEnergyProjection();

                bool unresolved = _state.Stamina.CharacterIds.Any(characterId =>
                    _state.Stamina.GetSimulation(characterId).HasPendingRuntimeDecision);
                if (unresolved)
                    throw new InvalidOperationException(
                        "The stamina runtime bridge did not resolve every decision at minute " +
                        _state.Time.ElapsedMinutes + ".");
            }

            return dueEvents;
        }

        private void ProcessWorldAtCurrentMinute(ICollection<ScheduledEvent> dueEvents)
        {
            // Legacy Energy is a projection of canonical stamina. Refresh it before autonomy
            // evaluates a pulse as well as afterward, so jump size cannot change the value seen
            // by action selection at the same integer-minute boundary.
            _state.RefreshLegacyEnergyProjection();
            AutonomousOfficeSimulation.AdvanceTo(
                _state.WorldSeed,
                _state.Family,
                _state.Time.ElapsedMinutes);
            _state.RefreshLegacyEnergyProjection();
            _state.Contracts.FailOverdue(_state.Time.ElapsedMinutes, _state.Company, _state.Family);
            _state.Growth.StarterProduct.Synchronize(_state);
            _state.Growth.ResolveProductIfDue(
                _state.WorldSeed,
                _state.Time.ElapsedMinutes,
                _state.Family,
                _state.Company);
            IReadOnlyList<ScheduledEvent> due = _state.Events.DequeueDue(_state.Time.ElapsedMinutes);
            foreach (ScheduledEvent scheduledEvent in due)
            {
                dueEvents.Add(scheduledEvent);
                Apply(scheduledEvent);
            }
        }

        private static StaminaActivityKind ResolveSemanticActivity(FamilyMemberState member)
        {
            switch (member.Autonomy.CurrentAction)
            {
                case AutonomousOfficeAction.FocusWork:
                    return StaminaActivityKind.Typing;
                case AutonomousOfficeAction.Administration:
                    return StaminaActivityKind.Administration;
                case AutonomousOfficeAction.Reception:
                    return StaminaActivityKind.Reception;
                case AutonomousOfficeAction.Printing:
                    return StaminaActivityKind.Printing;
                case AutonomousOfficeAction.Meeting:
                    return StaminaActivityKind.Meeting;
                case AutonomousOfficeAction.OutsideSales:
                    return StaminaActivityKind.OutsideWork;
                case AutonomousOfficeAction.Sleep:
                    return StaminaActivityKind.Sleep;
                case AutonomousOfficeAction.School:
                case AutonomousOfficeAction.HouseholdDuty:
                case AutonomousOfficeAction.OutsideCommitment:
                case AutonomousOfficeAction.OffDuty:
                    return StaminaActivityKind.OffDuty;
                default:
                    return StaminaActivityKind.Idle;
            }
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
