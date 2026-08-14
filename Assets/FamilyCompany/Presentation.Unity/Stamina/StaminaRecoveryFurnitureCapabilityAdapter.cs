using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Presentation.Unity.OfficeRuntime;
using FamilyCompany.Simulation.OfficeLayout;
using FamilyCompany.Simulation.Stamina;

namespace FamilyCompany.Presentation.Unity.Stamina
{
    /// <summary>
    /// The only production mapping from live build-editor capabilities into stamina candidates.
    /// Restroom intentionally has no mapping until a real definition and placed facility exist.
    /// </summary>
    public sealed class StaminaRecoveryFurnitureCapabilityAdapter :
        IStaminaRecoveryCapabilityQueryAdapter
    {
        private readonly StarterOfficeRuntimeBootstrap _runtime;
        private readonly OfficeRuntimeFurnitureCapabilityAdapter _capabilities;

        public StaminaRecoveryFurnitureCapabilityAdapter(
            StarterOfficeRuntimeBootstrap runtime,
            Simulation.Game.GameState state)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _capabilities = new OfficeRuntimeFurnitureCapabilityAdapter(
                runtime,
                state ?? throw new ArgumentNullException(nameof(state)));
        }

        public StaminaRecoveryCapabilityQueryResult Query(
            StaminaRecoveryCapabilityQuery query)
        {
            if (!_runtime.IsReady || _runtime.World == null ||
                !_runtime.World.Registry.TryGet(query.CharacterId, out OfficeRuntimeAgent actor) ||
                actor == null || actor.IsPresentationAway)
            {
                return new StaminaRecoveryCapabilityQueryResult(
                    query,
                    Math.Max(0, _runtime.World?.Occupancy.Revision ?? 0),
                    Array.Empty<StaminaRecoveryCandidate>());
            }

            OfficeGridCoordinate start = _runtime.World.Presenter.NearestCell(actor.transform.position);
            var result = new List<StaminaRecoveryCandidate>();
            Add(result, OfficeFurnitureCapability.WaterSource,
                StaminaRecoveryActivity.Water, "water-drink", actor, start);
            Add(result, OfficeFurnitureCapability.DrinkVending,
                StaminaRecoveryActivity.Water, "vending-drink", actor, start);
            Add(result, OfficeFurnitureCapability.RestSeat,
                StaminaRecoveryActivity.Lounge, "lounge-rest", actor, start);

            return new StaminaRecoveryCapabilityQueryResult(
                query,
                Math.Max(0, _runtime.World.Occupancy.Revision),
                result
                    .OrderBy(item => item.Activity)
                    .ThenBy(item => item.InteractionId, StringComparer.Ordinal)
                    .ThenBy(item => item.RuntimeFurnitureInstanceId, StringComparer.Ordinal));
        }

        private void Add(
            ICollection<StaminaRecoveryCandidate> result,
            OfficeFurnitureCapability capability,
            StaminaRecoveryActivity activity,
            string interactionId,
            OfficeRuntimeAgent actor,
            OfficeGridCoordinate start)
        {
            IReadOnlyList<OfficeFurnitureCapabilityCandidate> candidates =
                _capabilities.FindAvailableForAgent(
                    capability,
                    actor.AgentId,
                    start,
                    actor.AgentRadius);
            foreach (OfficeFurnitureCapabilityCandidate candidate in candidates)
            {
                // FindAvailableForAgent already proved placement, reachability, and live capacity.
                result.Add(new StaminaRecoveryCandidate(
                    activity,
                    interactionId,
                    candidate.InstanceId,
                    true,
                    true));
            }
        }
    }
}
