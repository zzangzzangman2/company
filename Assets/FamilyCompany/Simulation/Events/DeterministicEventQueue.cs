using System;
using System.Collections.Generic;
using System.Linq;

namespace FamilyCompany.Simulation.Events
{
    public sealed class DeterministicEventQueue
    {
        private readonly List<ScheduledEvent> _events;

        public DeterministicEventQueue(IEnumerable<ScheduledEvent> events = null)
        {
            _events = events == null ? new List<ScheduledEvent>() : new List<ScheduledEvent>(events);
            EnsureUniqueIds();
            Sort();
        }

        public int Count => _events.Count;

        public void Enqueue(ScheduledEvent scheduledEvent)
        {
            if (scheduledEvent == null)
            {
                throw new ArgumentNullException(nameof(scheduledEvent));
            }

            if (_events.Any(item => item.EventId == scheduledEvent.EventId))
            {
                throw new InvalidOperationException($"Duplicate event ID: {scheduledEvent.EventId}");
            }

            _events.Add(scheduledEvent);
            Sort();
        }

        public IReadOnlyList<ScheduledEvent> DequeueDue(long elapsedMinute)
        {
            var count = 0;
            while (count < _events.Count && _events[count].DueMinute <= elapsedMinute)
            {
                count++;
            }

            if (count == 0)
            {
                return Array.Empty<ScheduledEvent>();
            }

            var due = _events.GetRange(0, count);
            _events.RemoveRange(0, count);
            return due;
        }

        public IReadOnlyList<ScheduledEvent> Snapshot()
        {
            return _events.ToArray();
        }

        private void Sort()
        {
            _events.Sort((left, right) =>
            {
                var due = left.DueMinute.CompareTo(right.DueMinute);
                if (due != 0) return due;
                var priority = left.Priority.CompareTo(right.Priority);
                if (priority != 0) return priority;
                return string.CompareOrdinal(left.EventId, right.EventId);
            });
        }

        private void EnsureUniqueIds()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var scheduledEvent in _events)
            {
                if (!ids.Add(scheduledEvent.EventId))
                {
                    throw new InvalidOperationException($"Duplicate event ID: {scheduledEvent.EventId}");
                }
            }
        }
    }
}

