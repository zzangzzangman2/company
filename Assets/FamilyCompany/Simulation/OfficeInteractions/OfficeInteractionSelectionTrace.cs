using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Simulation.Family;

namespace FamilyCompany.Simulation.OfficeInteractions
{
    public sealed class OfficeInteractionSelectionTrace
    {
        public OfficeInteractionSelectionTrace(
            int worldSeed,
            string memberId,
            long minute,
            AutonomousOfficeAction macroAction,
            long macroActionStartedMinute,
            int sequenceIndex,
            string authoritativeOfferId,
            string shadowOfferId,
            IEnumerable<OfficeInteractionScoreBreakdown> scores,
            int authoritativeDurationMinutes = 0,
            string authoritativePartnerId = "",
            string authoritativeTargetId = "")
        {
            WorldSeed = worldSeed;
            MemberId = memberId ?? string.Empty;
            Minute = minute;
            MacroAction = macroAction;
            MacroActionStartedMinute = macroActionStartedMinute;
            SequenceIndex = sequenceIndex;
            AuthoritativeOfferId = authoritativeOfferId ?? string.Empty;
            ShadowOfferId = shadowOfferId ?? string.Empty;
            AuthoritativeDurationMinutes = authoritativeDurationMinutes;
            AuthoritativePartnerId = authoritativePartnerId ?? string.Empty;
            AuthoritativeTargetId = authoritativeTargetId ?? string.Empty;
            Scores = (scores ?? Array.Empty<OfficeInteractionScoreBreakdown>())
                .OrderBy(score => score.OfferId, StringComparer.Ordinal)
                .ToArray();
        }

        public int WorldSeed { get; }
        public string MemberId { get; }
        public long Minute { get; }
        public AutonomousOfficeAction MacroAction { get; }
        public long MacroActionStartedMinute { get; }
        public int SequenceIndex { get; }
        public string AuthoritativeOfferId { get; }
        public string ShadowOfferId { get; }
        public int AuthoritativeDurationMinutes { get; }
        public string AuthoritativePartnerId { get; }
        public string AuthoritativeTargetId { get; }
        public bool Diverged => !string.Equals(AuthoritativeOfferId, ShadowOfferId, StringComparison.Ordinal);
        public IReadOnlyList<OfficeInteractionScoreBreakdown> Scores { get; }

        public string DeterminismSignature()
        {
            return string.Join("\n", new[]
            {
                WorldSeed.ToString(),
                MemberId,
                Minute.ToString(),
                ((int)MacroAction).ToString(),
                MacroActionStartedMinute.ToString(),
                SequenceIndex.ToString(),
                AuthoritativeOfferId,
                ShadowOfferId,
                AuthoritativeDurationMinutes.ToString(),
                AuthoritativePartnerId,
                AuthoritativeTargetId,
                string.Join("\n", Scores.Select(score => score.DeterminismSignature()))
            });
        }

        public OfficeInteractionSelectionTrace WithAuthoritativeOutcome(
            int durationMinutes,
            string partnerId,
            string targetId)
        {
            return new OfficeInteractionSelectionTrace(
                WorldSeed,
                MemberId,
                Minute,
                MacroAction,
                MacroActionStartedMinute,
                SequenceIndex,
                AuthoritativeOfferId,
                ShadowOfferId,
                Scores,
                durationMinutes,
                partnerId,
                targetId);
        }
    }

    /// <summary>
    /// Optional diagnostics hook. With no subscriber the shadow calculation has no retained state,
    /// and therefore does not alter save data or authoritative simulation behavior.
    /// </summary>
    public static class OfficeInteractionShadowDiagnostics
    {
        public static event Action<OfficeInteractionSelectionTrace> TraceRecorded;

        public static void Record(OfficeInteractionSelectionTrace trace)
        {
            TraceRecorded?.Invoke(trace);
        }
    }
}
