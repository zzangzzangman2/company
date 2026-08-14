using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Simulation.Core;

namespace FamilyCompany.Simulation.Stamina
{
    /// <summary>
    /// A transient view produced by the external furniture-capability query adapter. The instance
    /// ID is query output only: stamina never invents, catalogs, saves, or pins it in a plan.
    /// Reservation ownership remains in OfficeInteractionReservationBook.
    /// </summary>
    public sealed class StaminaRecoveryCandidate
    {
        public StaminaRecoveryCandidate(
            StaminaRecoveryActivity activity,
            string interactionId,
            string runtimeFurnitureInstanceId,
            bool isReachable,
            bool hasCapacity)
        {
            if (!Enum.IsDefined(typeof(StaminaRecoveryActivity), activity) ||
                activity == StaminaRecoveryActivity.None)
                throw new ArgumentOutOfRangeException(nameof(activity));
            if (string.IsNullOrWhiteSpace(interactionId))
                throw new ArgumentException("Interaction ID is required.", nameof(interactionId));
            if (string.IsNullOrWhiteSpace(runtimeFurnitureInstanceId))
                throw new ArgumentException(
                    "Queried runtime furniture instance ID is required.",
                    nameof(runtimeFurnitureInstanceId));
            Activity = activity;
            InteractionId = interactionId.Trim();
            RuntimeFurnitureInstanceId = runtimeFurnitureInstanceId.Trim();
            IsReachable = isReachable;
            HasCapacity = hasCapacity;
        }

        public StaminaRecoveryActivity Activity { get; }
        public string InteractionId { get; }
        public string RuntimeFurnitureInstanceId { get; }
        public bool IsReachable { get; }
        public bool HasCapacity { get; }
        public bool IsUsable => IsReachable && HasCapacity;
    }

    /// <summary>
    /// Deterministic semantic choice only. It deliberately omits a concrete facility: the existing
    /// runtime interaction lifecycle must select and claim one live offer atomically.
    /// </summary>
    public sealed class StaminaRecoveryPlan
    {
        internal StaminaRecoveryPlan(
            string requestKey,
            StaminaRecoveryActivity activity,
            string interactionId)
        {
            RequestKey = requestKey;
            Activity = activity;
            InteractionId = interactionId;
        }

        public string RequestKey { get; }
        public StaminaRecoveryActivity Activity { get; }
        public string InteractionId { get; }
    }

    public static class StaminaRecoveryPlanner
    {
        /// <summary>
        /// Production entry point. The adapter owns translation from the finalized external
        /// furniture-capability query; stamina receives only an immutable, transient candidate view.
        /// </summary>
        public static bool TrySelect(
            CharacterStaminaSimulation simulation,
            IStaminaRecoveryCapabilityQueryAdapter capabilityQueryAdapter,
            out StaminaRecoveryPlan plan)
        {
            if (simulation == null) throw new ArgumentNullException(nameof(simulation));
            if (capabilityQueryAdapter == null)
                throw new ArgumentNullException(nameof(capabilityQueryAdapter));
            if (!simulation.State.CanCreateDepartureIntent ||
                string.IsNullOrEmpty(simulation.RecoveryRequestKey))
            {
                plan = null;
                return false;
            }

            var query = new StaminaRecoveryCapabilityQuery(
                simulation.CharacterId,
                simulation.RecoveryRequestKey,
                simulation.State.LastProcessedMinute);
            StaminaRecoveryCapabilityQueryResult result =
                capabilityQueryAdapter.Query(query) ??
                throw new InvalidOperationException(
                    "Furniture capability query adapter returned null.");
            if (!string.Equals(result.Query.CharacterId, query.CharacterId,
                    StringComparison.Ordinal) ||
                !string.Equals(result.Query.RecoveryRequestKey, query.RecoveryRequestKey,
                    StringComparison.Ordinal) ||
                result.Query.GameTimeMinute != query.GameTimeMinute)
                throw new InvalidOperationException(
                    "Furniture capability query result does not match its stamina request.");
            return TrySelect(simulation, result.Candidates, out plan);
        }

        /// <summary>
        /// Low-level deterministic selector for adapter implementations and pure validation. Runtime
        /// coordinators must use the capability-query overload so no local furniture catalog or scene
        /// lookup can become an alternate source of truth.
        /// </summary>
        public static bool TrySelect(
            CharacterStaminaSimulation simulation,
            IEnumerable<StaminaRecoveryCandidate> candidates,
            out StaminaRecoveryPlan plan)
        {
            if (simulation == null) throw new ArgumentNullException(nameof(simulation));
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            if (simulation.State.RecoveryPhase != StaminaRecoveryPhase.RecoveryRequested ||
                !simulation.HasPendingRuntimeDecision ||
                string.IsNullOrEmpty(simulation.RecoveryRequestKey))
            {
                plan = null;
                return false;
            }

            StaminaRecoveryCandidate[] items = candidates.ToArray();
            if (items.Any(item => item == null))
                throw new ArgumentException("Recovery candidates cannot contain null.", nameof(candidates));
            for (int left = 0; left < items.Length; left++)
            for (int right = left + 1; right < items.Length; right++)
            {
                if (items[left].Activity == items[right].Activity &&
                    string.Equals(items[left].InteractionId, items[right].InteractionId,
                        StringComparison.Ordinal) &&
                    string.Equals(items[left].RuntimeFurnitureInstanceId,
                        items[right].RuntimeFurnitureInstanceId,
                        StringComparison.Ordinal))
                    throw new ArgumentException(
                        "Recovery candidates must be unique.",
                        nameof(candidates));
            }

            StaminaRecoveryCandidate[] usable = items
                .Where(item => item.IsUsable)
                .Where(item => simulation.Profile.Recovery(item.Activity)
                    .SupportsInteractionId(item.InteractionId))
                .OrderBy(item => item.Activity)
                .ThenBy(item => item.RuntimeFurnitureInstanceId, StringComparer.Ordinal)
                .ThenBy(item => item.InteractionId, StringComparer.Ordinal)
                .ToArray();
            if (usable.Length == 0)
            {
                plan = null;
                return false;
            }

            IGrouping<StaminaRecoveryActivity, StaminaRecoveryCandidate>[] groups = usable
                .GroupBy(item => item.Activity)
                .OrderBy(group => group.Key)
                .ToArray();
            int totalWeight = groups.Sum(group =>
                simulation.Profile.Recovery(group.Key).SelectionWeight);
            int roll = StableRandom.StableRandomInt(
                simulation.RecoveryRequestKey + ":activity",
                totalWeight);
            IGrouping<StaminaRecoveryActivity, StaminaRecoveryCandidate> selectedGroup = null;
            int cursor = 0;
            foreach (IGrouping<StaminaRecoveryActivity, StaminaRecoveryCandidate> group in groups)
            {
                cursor += simulation.Profile.Recovery(group.Key).SelectionWeight;
                if (roll >= cursor) continue;
                selectedGroup = group;
                break;
            }
            if (selectedGroup == null) selectedGroup = groups[groups.Length - 1];

            StaminaRecoveryCandidate selected = selectedGroup
                .OrderBy(item => item.InteractionId, StringComparer.Ordinal)
                .ThenBy(item => item.RuntimeFurnitureInstanceId, StringComparer.Ordinal)
                .First();
            plan = new StaminaRecoveryPlan(
                simulation.RecoveryRequestKey,
                selected.Activity,
                selected.InteractionId);
            return true;
        }
    }
}
