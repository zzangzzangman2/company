using System;
using System.Collections.Generic;
using System.Linq;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime
{
    public sealed class OfficeRuntimeActorRegistry
    {
        private static readonly string[] CanonicalFamilyIds =
            { "player", "older_sister", "father", "mother" };
        private readonly Dictionary<string, OfficeRuntimeAgent> _actors =
            new Dictionary<string, OfficeRuntimeAgent>(StringComparer.Ordinal);

        public int Count => _actors.Count;
        public IReadOnlyList<OfficeRuntimeAgent> Actors => _actors.Values
            .OrderBy(item => item.AgentId, StringComparer.Ordinal)
            .ToArray();

        public void Register(OfficeRuntimeAgent actor)
        {
            if (actor == null) throw new ArgumentNullException(nameof(actor));
            if (!_actors.TryAdd(actor.AgentId, actor))
                throw new InvalidOperationException("Duplicate Starter Office actor: " + actor.AgentId);
        }

        public bool TryGet(string memberId, out OfficeRuntimeAgent actor) =>
            _actors.TryGetValue(memberId ?? string.Empty, out actor);

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
