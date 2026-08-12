using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Simulation.Family;

namespace FamilyCompany.Simulation.OfficeInteractions
{
    public enum OfficeInteractionApproachPolicy
    {
        CurrentPosition = 0,
        AdjacentCardinal = 1,
        AdjacentOrTwoCells = 2,
        AssignedSeatApproach = 3,
        SharedLoungeArea = 4,
        OpenArea = 5
    }

    public enum OfficeInteractionReservationPolicy
    {
        None = 0,
        ExclusiveFurniture = 1,
        SharedFurnitureCapacity = 2,
        AssignedSeat = 3,
        PairedConversation = 4,
        GroupMeeting = 5
    }

    public enum OfficeInteractionCandidateScope
    {
        StandardOfficeMacro = 0,
        MeetingMacroOnly = 1,
        FallbackOnly = 2
    }

    /// <summary>
    /// Pure simulation description of one office interaction. It advertises semantic data only;
    /// Unity objects, renderers, paths, and runtime reservations remain outside this layer.
    /// </summary>
    public sealed class OfficeInteractionDefinition
    {
        private readonly Dictionary<FamilyRole, int> _roleBaseAffinity;
        private readonly AutonomousOfficeAction[] _compatibleMacroActions;

        public OfficeInteractionDefinition(
            string interactionId,
            OfficeMicroAction microAction,
            OfficeSemanticLocation semanticLocation,
            string targetIdTemplate,
            string furnitureKindId,
            string presentationActivityId,
            int minimumDurationMinutes,
            int maximumDurationMinutes,
            int capacity,
            int cooldownMinutes,
            bool requiresFurniture,
            bool requiresSeat,
            bool requiresAssignedSeat,
            bool isInterruptible,
            OfficeInteractionApproachPolicy approachPolicy,
            OfficeInteractionReservationPolicy reservationPolicy,
            OfficeInteractionCandidateScope candidateScope,
            int universalBaseAffinity,
            IEnumerable<KeyValuePair<FamilyRole, int>> roleBaseAffinity,
            IEnumerable<AutonomousOfficeAction> compatibleMacroActions,
            bool requiresPreviousLocationOutsideDesk = false)
        {
            if (string.IsNullOrWhiteSpace(interactionId))
                throw new ArgumentException("Interaction ID is required.", nameof(interactionId));
            if (microAction == OfficeMicroAction.None)
                throw new ArgumentOutOfRangeException(nameof(microAction));
            if (string.IsNullOrWhiteSpace(targetIdTemplate))
                throw new ArgumentException("Target ID template is required.", nameof(targetIdTemplate));
            if (minimumDurationMinutes <= 0)
                throw new ArgumentOutOfRangeException(nameof(minimumDurationMinutes));
            if (maximumDurationMinutes < minimumDurationMinutes)
                throw new ArgumentOutOfRangeException(nameof(maximumDurationMinutes));
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            if (cooldownMinutes < 0) throw new ArgumentOutOfRangeException(nameof(cooldownMinutes));
            if (universalBaseAffinity < 0) throw new ArgumentOutOfRangeException(nameof(universalBaseAffinity));

            InteractionId = interactionId.Trim();
            MicroAction = microAction;
            SemanticLocation = semanticLocation;
            TargetIdTemplate = targetIdTemplate.Trim();
            FurnitureKindId = furnitureKindId?.Trim() ?? string.Empty;
            PresentationActivityId = presentationActivityId?.Trim() ?? string.Empty;
            MinimumDurationMinutes = minimumDurationMinutes;
            MaximumDurationMinutes = maximumDurationMinutes;
            Capacity = capacity;
            CooldownMinutes = cooldownMinutes;
            RequiresFurniture = requiresFurniture;
            RequiresSeat = requiresSeat;
            RequiresAssignedSeat = requiresAssignedSeat;
            IsInterruptible = isInterruptible;
            ApproachPolicy = approachPolicy;
            ReservationPolicy = reservationPolicy;
            CandidateScope = candidateScope;
            UniversalBaseAffinity = universalBaseAffinity;
            RequiresPreviousLocationOutsideDesk = requiresPreviousLocationOutsideDesk;
            _roleBaseAffinity = roleBaseAffinity == null
                ? new Dictionary<FamilyRole, int>()
                : roleBaseAffinity.ToDictionary(pair => pair.Key, pair => pair.Value);
            if (_roleBaseAffinity.Values.Any(value => value <= 0))
                throw new ArgumentOutOfRangeException(nameof(roleBaseAffinity));
            _compatibleMacroActions = compatibleMacroActions == null
                ? Array.Empty<AutonomousOfficeAction>()
                : compatibleMacroActions.Distinct().OrderBy(action => action).ToArray();
        }

        public string InteractionId { get; }
        public OfficeMicroAction MicroAction { get; }
        public OfficeSemanticLocation SemanticLocation { get; }
        public string TargetIdTemplate { get; }
        public string FurnitureKindId { get; }
        public string PresentationActivityId { get; }
        public int MinimumDurationMinutes { get; }
        public int MaximumDurationMinutes { get; }
        public int Capacity { get; }
        public int CooldownMinutes { get; }
        public bool RequiresFurniture { get; }
        public bool RequiresSeat { get; }
        public bool RequiresAssignedSeat { get; }
        public bool IsInterruptible { get; }
        public OfficeInteractionApproachPolicy ApproachPolicy { get; }
        public OfficeInteractionReservationPolicy ReservationPolicy { get; }
        public OfficeInteractionCandidateScope CandidateScope { get; }
        public int UniversalBaseAffinity { get; }
        public bool RequiresPreviousLocationOutsideDesk { get; }
        public IReadOnlyDictionary<FamilyRole, int> RoleBaseAffinity => _roleBaseAffinity;
        public IReadOnlyList<AutonomousOfficeAction> CompatibleMacroActions => _compatibleMacroActions;

        public int LegacyWeightFor(FamilyRole role)
        {
            return _roleBaseAffinity.TryGetValue(role, out int affinity)
                ? affinity
                : UniversalBaseAffinity;
        }

        public bool IsMacroCompatible(AutonomousOfficeAction macroAction)
        {
            return Array.IndexOf(_compatibleMacroActions, macroAction) >= 0;
        }

        public string ResolveTargetId(string memberId)
        {
            return TargetIdTemplate.Replace("{memberId}", memberId ?? string.Empty);
        }
    }

    public sealed class OfficeInteractionCandidate
    {
        public OfficeInteractionCandidate(
            OfficeInteractionDefinition definition,
            string targetId,
            int legacyWeight)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            TargetId = targetId ?? string.Empty;
            LegacyWeight = legacyWeight;
            OfferId = definition.InteractionId + "@" + TargetId;
        }

        public OfficeInteractionDefinition Definition { get; }
        public string InteractionId => Definition.InteractionId;
        public string OfferId { get; }
        public OfficeMicroAction MicroAction => Definition.MicroAction;
        public OfficeSemanticLocation SemanticLocation => Definition.SemanticLocation;
        public string TargetId { get; }
        public int LegacyWeight { get; }
    }

    public sealed class OfficeInteractionCandidateSnapshot
    {
        public OfficeInteractionCandidateSnapshot(
            OfficeMicroAction action,
            string targetId,
            OfficeSemanticLocation location,
            int weight)
        {
            Action = action;
            TargetId = targetId ?? string.Empty;
            Location = location;
            Weight = weight;
        }

        public OfficeMicroAction Action { get; }
        public string TargetId { get; }
        public OfficeSemanticLocation Location { get; }
        public int Weight { get; }
    }
}
