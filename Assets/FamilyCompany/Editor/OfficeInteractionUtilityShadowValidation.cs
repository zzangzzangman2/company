using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FamilyCompany.Simulation.Family;
using FamilyCompany.Simulation.Game;
using FamilyCompany.Simulation.OfficeInteractions;
using FamilyCompany.Simulation.Prototype;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public static class OfficeInteractionUtilityShadowValidation
    {
        private const int SeedCount = 128;
        private const int ValidationMinutes = 4 * 60;

        [Serializable]
        private sealed class ScoreTraceArtifact
        {
            public int worldSeed;
            public string memberId;
            public long minute;
            public string macroAction;
            public int sequenceIndex;
            public string authoritativeOfferId;
            public string shadowOfferId;
            public int durationMinutes;
            public string partnerId;
            public string resolvedTargetId;
            public ScoreArtifact[] scores;
        }

        [Serializable]
        private sealed class ScoreArtifact
        {
            public string offerId;
            public string interactionId;
            public string targetId;
            public string action;
            public string location;
            public int total;
            public int baseRoleAffinity;
            public int macroCompatibility;
            public int needUrgency;
            public int novelty;
            public int distanceUtility;
            public int availabilityUtility;
            public int returnToWorkUtility;
            public int socialUtility;
            public int repetitionPenalty;
            public int scheduleRiskPenalty;
            public int congestionPenalty;
        }

        [Serializable]
        private sealed class ComparisonArtifact
        {
            public int worldSeed;
            public string memberId;
            public long minute;
            public int sequenceIndex;
            public string legacyOfferId;
            public string shadowOfferId;
            public bool diverged;
        }

        [Serializable]
        private sealed class ArtifactArray<T>
        {
            public T[] items;
        }

        [MenuItem("Family Company/Validate Office Interaction Utility Shadow")]
        public static void Run()
        {
            try
            {
                RunValidation();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("OFFICE_INTERACTION_UTILITY_SHADOW_VALIDATION: FAIL");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        private static void RunValidation()
        {
            List<OfficeInteractionSelectionTrace> first = CaptureAllSeeds();
            List<OfficeInteractionSelectionTrace> second = CaptureAllSeeds();
            string firstSignature = BuildSignature(first);
            string secondSignature = BuildSignature(second);
            Require(string.Equals(firstSignature, secondSignature, StringComparison.Ordinal),
                "shadow trace changed on deterministic replay");
            ValidateOrderIndependence(first);

            string artifactRoot = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", "Artifacts", "OfficeInteractionUtilityShadow"));
            Directory.CreateDirectory(artifactRoot);
            WriteArtifacts(artifactRoot, first, firstSignature);
            int divergences = first.Count(item => item.Diverged);
            Debug.Log(
                "OFFICE_INTERACTION_UTILITY_SHADOW_VALIDATION: PASS | " +
                $"seeds={SeedCount} traces={first.Count} scores={first.Sum(item => item.Scores.Count)} " +
                $"divergences={divergences} signature={firstSignature}");
        }

        private static List<OfficeInteractionSelectionTrace> CaptureAllSeeds()
        {
            var result = new List<OfficeInteractionSelectionTrace>();
            for (var seedIndex = 0; seedIndex < SeedCount; seedIndex++)
            {
                int seed = 20000103 + seedIndex * 7919;
                GameState state = PrototypeStateFactory.Create(seed);
                var runner = new SimulationRunner(state);
                void Handler(OfficeInteractionSelectionTrace trace) => result.Add(trace);
                OfficeInteractionShadowDiagnostics.TraceRecorded += Handler;
                try
                {
                    runner.AdvanceMinutes(ValidationMinutes);
                }
                finally
                {
                    OfficeInteractionShadowDiagnostics.TraceRecorded -= Handler;
                }
            }
            return result;
        }

        private static void ValidateOrderIndependence(IEnumerable<OfficeInteractionSelectionTrace> traces)
        {
            foreach (OfficeInteractionSelectionTrace trace in traces)
            {
                FamilyMemberState member = CreateSelectionMember(trace);
                OfficeInteractionScoreBreakdown[] reversed = trace.Scores.Reverse().ToArray();
                OfficeInteractionScoreBreakdown selected = OfficeInteractionScoring.SelectShadow(
                    trace.WorldSeed, member, reversed);
                Require(string.Equals(selected?.OfferId ?? string.Empty, trace.ShadowOfferId,
                        StringComparison.Ordinal),
                    $"candidate order changed shadow selection for {trace.MemberId}/{trace.SequenceIndex}");
                string[] forwardScores = trace.Scores.Select(score => score.DeterminismSignature())
                    .OrderBy(item => item, StringComparer.Ordinal).ToArray();
                string[] reverseScores = reversed.Select(score => score.DeterminismSignature())
                    .OrderBy(item => item, StringComparer.Ordinal).ToArray();
                Require(forwardScores.SequenceEqual(reverseScores), "candidate order changed score trace");
            }
        }

        private static FamilyMemberState CreateSelectionMember(OfficeInteractionSelectionTrace trace)
        {
            FamilyMemberState source = PrototypeStateFactory.Create(trace.WorldSeed).Family.Get(trace.MemberId);
            var micro = new OfficeMicroActionState(
                sequenceIndex: Math.Max(0, trace.SequenceIndex - 1),
                macroActionStartedMinute: trace.MacroActionStartedMinute);
            var autonomy = new OfficeAutonomyState(
                trace.MacroAction,
                OfficeSemanticLocation.Desk,
                trace.MacroActionStartedMinute,
                trace.MacroActionStartedMinute + 1,
                microAction: micro);
            return new FamilyMemberState(
                source.MemberId,
                source.DisplayName,
                source.Role,
                source.BirthDate,
                source.CompanyDuty,
                source.Energy,
                source.Trust,
                source.Stress,
                source.Stats,
                source.CareerMemories,
                autonomy);
        }

        private static string BuildSignature(IEnumerable<OfficeInteractionSelectionTrace> traces)
        {
            string canonical = string.Join("\n---\n", traces
                .Select(trace => trace.DeterminismSignature())
                .OrderBy(value => value, StringComparer.Ordinal));
            using var sha = SHA256.Create();
            byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical));
            return string.Concat(digest.Select(value => value.ToString("x2")));
        }

        private static void WriteArtifacts(
            string root,
            IReadOnlyList<OfficeInteractionSelectionTrace> traces,
            string signature)
        {
            ScoreTraceArtifact[] scoreArtifacts = traces.Select(ToScoreTraceArtifact).ToArray();
            ComparisonArtifact[] comparisons = traces.Select(trace => new ComparisonArtifact
            {
                worldSeed = trace.WorldSeed,
                memberId = trace.MemberId,
                minute = trace.Minute,
                sequenceIndex = trace.SequenceIndex,
                legacyOfferId = trace.AuthoritativeOfferId,
                shadowOfferId = trace.ShadowOfferId,
                diverged = trace.Diverged
            }).ToArray();
            int divergentCount = comparisons.Count(item => item.diverged);
            string summary =
                "# Office Interaction Utility Shadow\n\n" +
                $"- seeds: {SeedCount}\n- minutes per seed: {ValidationMinutes}\n" +
                $"- traces: {traces.Count}\n- score rows: {traces.Sum(item => item.Scores.Count)}\n" +
                $"- divergent selections: {divergentCount}\n- authoritative selector: legacy WeightedPick\n" +
                $"- shadow signature: `{signature}`\n";
            File.WriteAllText(Path.Combine(root, "summary.md"), summary, new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(root, "score-traces.json"),
                JsonUtility.ToJson(new ArtifactArray<ScoreTraceArtifact> { items = scoreArtifacts }, true),
                new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(root, "selection-comparison.json"),
                JsonUtility.ToJson(new ArtifactArray<ComparisonArtifact> { items = comparisons }, true),
                new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(root, "divergent-selections.md"),
                BuildDivergenceReport(traces), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(root, "determinism-signature.txt"),
                signature + Environment.NewLine, new UTF8Encoding(false));
        }

        private static ScoreTraceArtifact ToScoreTraceArtifact(OfficeInteractionSelectionTrace trace)
        {
            return new ScoreTraceArtifact
            {
                worldSeed = trace.WorldSeed,
                memberId = trace.MemberId,
                minute = trace.Minute,
                macroAction = trace.MacroAction.ToString(),
                sequenceIndex = trace.SequenceIndex,
                authoritativeOfferId = trace.AuthoritativeOfferId,
                shadowOfferId = trace.ShadowOfferId,
                durationMinutes = trace.AuthoritativeDurationMinutes,
                partnerId = trace.AuthoritativePartnerId,
                resolvedTargetId = trace.AuthoritativeTargetId,
                scores = trace.Scores.Select(score => new ScoreArtifact
                {
                    offerId = score.OfferId,
                    interactionId = score.InteractionId,
                    targetId = score.TargetId,
                    action = score.MicroAction.ToString(),
                    location = score.Location.ToString(),
                    total = score.TotalScore,
                    baseRoleAffinity = score.BaseRoleAffinity,
                    macroCompatibility = score.MacroCompatibility,
                    needUrgency = score.NeedUrgency,
                    novelty = score.Novelty,
                    distanceUtility = score.DistanceUtility,
                    availabilityUtility = score.AvailabilityUtility,
                    returnToWorkUtility = score.ReturnToWorkUtility,
                    socialUtility = score.SocialUtility,
                    repetitionPenalty = score.RepetitionPenalty,
                    scheduleRiskPenalty = score.ScheduleRiskPenalty,
                    congestionPenalty = score.CongestionPenalty
                }).ToArray()
            };
        }

        private static string BuildDivergenceReport(IEnumerable<OfficeInteractionSelectionTrace> traces)
        {
            OfficeInteractionSelectionTrace[] divergences = traces.Where(item => item.Diverged).ToArray();
            var builder = new StringBuilder("# Divergent Shadow Selections\n\n");
            if (divergences.Length == 0) return builder.Append("No divergences.\n").ToString();
            foreach (OfficeInteractionSelectionTrace trace in divergences)
            {
                builder.Append("## seed ").Append(trace.WorldSeed).Append(" / minute ")
                    .Append(trace.Minute).Append(" / ").Append(trace.MemberId).Append('\n')
                    .Append("- legacy: `").Append(trace.AuthoritativeOfferId).Append("`\n")
                    .Append("- utility-shadow: `").Append(trace.ShadowOfferId).Append("`\n\n");
                foreach (OfficeInteractionScoreBreakdown score in trace.Scores
                             .OrderByDescending(item => item.TotalScore)
                             .ThenBy(item => item.OfferId, StringComparer.Ordinal))
                {
                    builder.Append("  - ").Append(score.OfferId).Append(": total=")
                        .Append(score.TotalScore).Append(" base=").Append(score.BaseRoleAffinity)
                        .Append(" macro=").Append(score.MacroCompatibility)
                        .Append(" need=").Append(score.NeedUrgency)
                        .Append(" novelty=").Append(score.Novelty)
                        .Append(" availability=").Append(score.AvailabilityUtility)
                        .Append(" repetition=-").Append(score.RepetitionPenalty).Append('\n');
                }
                builder.Append('\n');
            }
            return builder.ToString();
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
