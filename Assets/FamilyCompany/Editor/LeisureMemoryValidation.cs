using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Simulation.Core;
using FamilyCompany.Simulation.Leisure;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public static class LeisureMemoryValidation
    {
        private static readonly string[] FutureLanguage =
        {
            "다음에",
            "앞으로",
            "언젠가",
            "나중에",
            "훗날",
            "예정",
            "될 것이다",
            "2001년",
            "2002년",
            "2026년"
        };

        [MenuItem("Family Company/Validate Leisure Memories")]
        public static void Run()
        {
            try
            {
                ValidateCatalogCoverageAndShape();
                ValidateParticipantOrderIndependence();
                ValidateIdenticalInputDeterminism();
                ValidateSeedPhraseDistribution();
                ValidateActualOutcomeAndTimeBoundaries();
                Debug.Log("FAMILY_COMPANY_LEISURE_MEMORY_VALIDATION: PASS");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("FAMILY_COMPANY_LEISURE_MEMORY_VALIDATION: FAIL");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        private static void ValidateCatalogCoverageAndShape()
        {
            var canonicalIds = new HashSet<string>(
                LeisureActivityCatalog.All.Select(activity => activity.Id),
                StringComparer.Ordinal);
            var memoryIds = new HashSet<string>(LeisureMemoryRules.SupportedActivityIds, StringComparer.Ordinal);
            AssertEqual(12, canonicalIds.Count, "canonical leisure count");
            AssertEqual(12, memoryIds.Count, "memory template count");
            AssertTrue(canonicalIds.SetEquals(memoryIds), "all canonical activities have exactly one memory template");

            foreach (var activity in LeisureActivityCatalog.All)
            {
                var participantCount = Math.Max(2, activity.MinimumParticipants);
                var participants = FamilyIds(participantCount);
                var elapsedMinute = ElapsedAt(activity.MinimumYear, 6, 1);
                var memory = LeisureMemoryRules.CreateCompletedMemory(
                    activity.Id,
                    elapsedMinute,
                    participants,
                    activity.SharedFamilyBondDelta,
                    20000103);

                AssertEqual(activity.Id, memory.ActivityId, activity.Id + " activity identity");
                AssertEqual(elapsedMinute, memory.ElapsedMinute, activity.Id + " elapsed minute");
                AssertEqual(activity.SharedFamilyBondDelta, memory.AppliedBondDelta, activity.Id + " actual bond delta");
                AssertTrue(memory.MemoryId.StartsWith("leisure-memory-v1:", StringComparison.Ordinal), activity.Id + " versioned ID");
                AssertTrue(memory.SummaryKo.Length > 0 && memory.SummaryKo.Length <= 80, activity.Id + " short Korean summary");
                AssertTrue(memory.RecallImportance >= 0 && memory.RecallImportance <= 100, activity.Id + " importance boundary");
                AssertTrue(memory.PhraseVariantsKo.Count >= 2 && memory.PhraseVariantsKo.Count <= 3, activity.Id + " phrase count");
                AssertEqual(memory.SummaryKo, memory.PhraseVariantsKo[0], activity.Id + " selected summary");
                AssertEqual(
                    memory.PhraseVariantsKo.Count,
                    memory.PhraseVariantsKo.Distinct(StringComparer.Ordinal).Count(),
                    activity.Id + " distinct phrases");
                AssertEqual(
                    memory.RelationshipTags.Count,
                    memory.RelationshipTags.Distinct().Count(),
                    activity.Id + " distinct relationship tags");

                foreach (var phrase in memory.PhraseVariantsKo)
                {
                    AssertNoFutureLanguage(phrase, activity.Id);
                }
            }
        }

        private static void ValidateParticipantOrderIndependence()
        {
            const long elapsedMinute = 8_000;
            var canonical = LeisureMemoryRules.CreateCompletedMemory(
                "family_restaurant_dinner",
                elapsedMinute,
                new[] { "father", "mother", "older_sister", "player" },
                5,
                7744);
            var reordered = LeisureMemoryRules.CreateCompletedMemory(
                "family_restaurant_dinner",
                elapsedMinute,
                new[] { " player ", "mother", "father", "mother", "older_sister" },
                5,
                7744);

            AssertEqual(Fingerprint(canonical), Fingerprint(reordered), "participant order and duplicates are irrelevant");
            AssertEqual(
                "father|mother|older_sister|player",
                string.Join("|", canonical.ParticipantFamilyIds),
                "participant IDs use stable ordinal order");
        }

        private static void ValidateIdenticalInputDeterminism()
        {
            var expected = Fingerprint(LeisureMemoryRules.CreateCompletedMemory(
                "riverside_picnic",
                12_345,
                new[] { "mother", "player" },
                4,
                5102));

            for (var replay = 0; replay < 100; replay++)
            {
                var actual = Fingerprint(LeisureMemoryRules.CreateCompletedMemory(
                    "riverside_picnic",
                    12_345,
                    new[] { "player", "mother" },
                    4,
                    5102));
                AssertEqual(expected, actual, "deterministic replay " + replay);
            }
        }

        private static void ValidateSeedPhraseDistribution()
        {
            var summaries = new HashSet<string>(StringComparer.Ordinal);
            var memoryIds = new HashSet<string>(StringComparer.Ordinal);
            for (var worldSeed = 0; worldSeed < 64; worldSeed++)
            {
                var memory = LeisureMemoryRules.CreateCompletedMemory(
                    "pc_bang_team_match",
                    9_000,
                    new[] { "player", "older_sister" },
                    2,
                    worldSeed);
                summaries.Add(memory.SummaryKo);
                memoryIds.Add(memory.MemoryId);
            }

            AssertTrue(summaries.Count >= 2, "different seeds distribute primary phrases");
            AssertEqual(64, memoryIds.Count, "world seed participates in immutable memory ID");
        }

        private static void ValidateActualOutcomeAndTimeBoundaries()
        {
            var withoutBond = LeisureMemoryRules.CreateCompletedMemory(
                "neighborhood_evening_walk",
                5_000,
                new[] { "player", "mother" },
                0,
                99);
            var withBond = LeisureMemoryRules.CreateCompletedMemory(
                "neighborhood_evening_walk",
                5_000,
                new[] { "mother", "player" },
                2,
                99);
            AssertTrue(withBond.MemoryId != withoutBond.MemoryId, "actual applied bond changes memory identity");
            AssertTrue(withBond.RecallImportance > withoutBond.RecallImportance, "actual applied bond changes importance");
            AssertTrue(
                !withoutBond.RelationshipTags.Contains(LeisureRelationshipTag.BondStrengthened),
                "zero applied bond has no strengthened tag");
            AssertTrue(
                withBond.RelationshipTags.Contains(LeisureRelationshipTag.BondStrengthened),
                "positive applied bond has strengthened tag");

            AssertThrows<ArgumentException>(
                () => LeisureMemoryRules.CreateCompletedMemory(
                    "adsl_coop_game_night",
                    0,
                    new[] { "player", "older_sister" },
                    3,
                    1),
                "future activity is rejected before its introduction year");
            AssertThrows<ArgumentException>(
                () => LeisureMemoryRules.CreateCompletedMemory(
                    "activity_not_performed",
                    10_000,
                    new[] { "player" },
                    0,
                    1),
                "unknown or unperformed activity is rejected");
            AssertThrows<ArgumentException>(
                () => LeisureMemoryRules.CreateCompletedMemory(
                    "neighborhood_evening_walk",
                    10_000,
                    new[] { "player" },
                    1,
                    1),
                "solo activity cannot claim a shared bond result");
        }

        private static IReadOnlyList<string> FamilyIds(int count)
        {
            var ids = new[] { "player", "older_sister", "father", "mother" };
            return ids.Take(count).ToArray();
        }

        private static long ElapsedAt(int year, int month, int day)
        {
            return checked((long)(new DateTime(year, month, day, 12, 0, 0) - GameTime.CampaignStart).TotalMinutes);
        }

        private static string Fingerprint(LeisureMemoryResult memory)
        {
            return string.Join(
                "~",
                memory.MemoryId,
                memory.ActivityId,
                memory.ElapsedMinute,
                string.Join("|", memory.ParticipantFamilyIds),
                memory.AppliedBondDelta,
                memory.SummaryKo,
                string.Join("|", memory.RelationshipTags),
                memory.RecallImportance,
                string.Join("|", memory.PhraseVariantsKo));
        }

        private static void AssertNoFutureLanguage(string phrase, string activityId)
        {
            foreach (var fragment in FutureLanguage)
            {
                AssertTrue(
                    phrase.IndexOf(fragment, StringComparison.Ordinal) < 0,
                    activityId + " has no future-language fragment " + fragment);
            }
        }

        private static void AssertThrows<TException>(Action action, string label)
            where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException)
            {
                return;
            }

            throw new InvalidOperationException(label + ": expected " + typeof(TException).Name + ".");
        }

        private static void AssertTrue(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException(label + ": expected true.");
        }

        private static void AssertEqual<T>(T expected, T actual, string label)
        {
            if (!Equals(expected, actual))
                throw new InvalidOperationException(label + ": expected " + expected + ", got " + actual + ".");
        }
    }
}
