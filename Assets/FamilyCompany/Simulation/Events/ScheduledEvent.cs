using System;

namespace FamilyCompany.Simulation.Events
{
    public sealed class ScheduledEvent
    {
        public ScheduledEvent(string eventId, long dueMinute, int priority, string kind, string payload = "")
        {
            if (string.IsNullOrWhiteSpace(eventId))
            {
                throw new ArgumentException("Event ID is required.", nameof(eventId));
            }

            if (dueMinute < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(dueMinute));
            }

            EventId = eventId;
            DueMinute = dueMinute;
            Priority = priority;
            Kind = kind ?? string.Empty;
            Payload = payload ?? string.Empty;
        }

        public string EventId { get; }
        public long DueMinute { get; }
        public int Priority { get; }
        public string Kind { get; }
        public string Payload { get; }
    }
}

