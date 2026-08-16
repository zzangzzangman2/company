using System;
using System.Collections.Generic;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime
{
    public sealed class OfficeRuntimeActorRegistry
    {
        private static readonly string[] CanonicalFamilyIds =
            { "player", "older_sister", "father", "mother" };
        private static readonly Comparison<OfficeRuntimeAgent> AgentOrder =
            (left, right) => CompareActorIds(left.AgentId, right.AgentId);
        private readonly Dictionary<string, OfficeRuntimeAgent> _actors =
            new Dictionary<string, OfficeRuntimeAgent>(StringComparer.Ordinal);
        private readonly List<OfficeRuntimeAgent> _orderedActors =
            new List<OfficeRuntimeAgent>(12);

        public int Count => _actors.Count;
        public IReadOnlyList<OfficeRuntimeAgent> Actors => _orderedActors;

        public void Register(OfficeRuntimeAgent actor)
        {
            if (actor == null) throw new ArgumentNullException(nameof(actor));
            if (!_actors.TryAdd(actor.AgentId, actor))
                throw new InvalidOperationException("Duplicate Starter Office actor: " + actor.AgentId);
            _orderedActors.Add(actor);
            _orderedActors.Sort(AgentOrder);
        }

        public bool TryGet(string memberId, out OfficeRuntimeAgent actor) =>
            _actors.TryGetValue(memberId ?? string.Empty, out actor);

        internal static int CompareActorIds(string left, string right) =>
            string.Compare(left, right, StringComparison.Ordinal);

        public void ValidateCanonicalFamily()
        {
            foreach (string memberId in CanonicalFamilyIds)
            {
                if (!_actors.ContainsKey(memberId))
                    throw new InvalidOperationException("Missing Starter Office actor: " + memberId);
            }
        }
    }
}
