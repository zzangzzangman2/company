using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Simulation.Leisure;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public static class LeisureRulesValidation
    {
        [MenuItem("Family Company/Validate Leisure Rules")]
        public static void Run()
        {
            try
            {
                ValidateCatalogShapeAndBalance();
                ValidateYearAndDayBoundaries();
                ValidateNoFutureYearLeak();
                ValidateDeterministicRecommendations();
                ValidateEffectBoundaries();
                Debug.Log("FAMILY_COMPANY_LEISURE_RULES_VALIDATION: PASS");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("FAMILY_COMPANY_LEISURE_RULES_VALIDATION: FAIL");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        private static void ValidateCatalogShapeAndBalance()
        {
            var all = LeisureActivityCatalog.All;
            AssertTrue(all.Count >= 8, "at least eight leisure activities");
            AssertEqual(
                all.Count,
                all.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count(),
                "unique activity IDs");

            var requiredIds = new[]
            {
                "convenience_store_snack_run",
                "pc_bang_team_match",
                "video_tape_rental_night",
                "comic_book_rental_stack",
                "neighborhood_public_bath",
                "family_restaurant_dinner",
                "neighborhood_evening_walk",
                "riverside_picnic"
            };
            foreach (var requiredId in requiredIds)
            {
                AssertTrue(LeisureActivityCatalog.FindById(requiredId) != null, $"required activity {requiredId}");
            }

            foreach (var activity in all)
            {
                AssertTrue(activity.CostWon >= 0 && activity.CostWon <= 100_000, $"{activity.Id} cost range");
                AssertTrue(activity.DurationMinutes >= 30 && activity.DurationMinutes <= 240, $"{activity.Id} duration range");
                AssertTrue(activity.EnergyDelta >= -25 && activity.EnergyDelta <= 25, $"{activity.Id} energy delta range");
                AssertTrue(activity.StressDelta >= -30 && activity.StressDelta <= 0, $"{activity.Id} stress recovery range");
                AssertTrue(activity.SharedFamilyBondDelta >= 0 && activity.SharedFamilyBondDelta <= 10, $"{activity.Id} bond delta range");
                AssertTrue(
                    activity.MinimumYear >= LeisureActivityCatalog.CampaignFirstYear &&
                    activity.MaximumYearInclusive <= LeisureActivityCatalog.CampaignLastYear,
                    $"{activity.Id} campaign year range");
            }

            AssertTrue(all.Any(item => item.CostWon == 0), "free recovery option exists");
            AssertTrue(all.Any(item => item.EnergyDelta < 0), "fun activity can trade energy for stress recovery");
            AssertTrue(all.Any(item => item.SharedFamilyBondDelta >= 5), "strong family bonding option exists");
            AssertTrue(all.Any(item => item.MinimumYear > 2000), "later-year unlock exists for leak validation");
        }

        private static void ValidateYearAndDayBoundaries()
        {
            var singingRoom = Required("family_singing_room");
            AssertEqual(false, singingRoom.IsAvailableOn(new DateTime(2000, 12, 31, 18, 0, 0), 2), "singing room before introduction year");
            AssertEqual(true, singingRoom.IsAvailableOn(new DateTime(2001, 1, 5, 18, 0, 0), 2), "singing room after introduction year on Friday");

            var picnic = Required("riverside_picnic");
            AssertEqual(false, picnic.IsAvailableOn(new DateTime(2000, 1, 7, 14, 0, 0), 2), "picnic blocked on Friday");
            AssertEqual(true, picnic.IsAvailableOn(new DateTime(2000, 1, 8, 14, 0, 0), 2), "picnic opens on Saturday");
            AssertEqual(false, picnic.IsAvailableOn(new DateTime(2000, 1, 8, 14, 0, 0), 1), "picnic requires shared outing");

            var walk = Required("neighborhood_evening_walk");
            AssertEqual(true, walk.IsAvailableOn(new DateTime(2026, 12, 31, 19, 0, 0), 1), "last campaign year inclusive");
            AssertEqual(false, walk.IsAvailableOn(new DateTime(2027, 1, 1, 19, 0, 0), 1), "after campaign year excluded");
        }

        private static void ValidateNoFutureYearLeak()
        {
            var atStart = new DateTime(2000, 1, 8, 15, 30, 0);
            var visible = LeisureActivityCatalog.AvailableOn(atStart, 4);
            AssertTrue(visible.All(item => item.MinimumYear <= 2000), "2000 available list has no future activities");
            AssertTrue(visible.All(item => item.Id != "family_singing_room"), "2001 singing room hidden in 2000");
            AssertTrue(visible.All(item => item.Id != "adsl_coop_game_night"), "2002 ADSL activity hidden in 2000");

            for (var month = 1; month <= 12; month++)
            {
                var at = new DateTime(2000, month, 1, 12, 0, 0);
                for (var seed = 0; seed < 16; seed++)
                {
                    var ranked = LeisureRecommendationRules.RankRecommendations(
                        $"no-leak-{seed}",
                        at,
                        new[] { "player", "father", "mother", "older_sister" },
                        12);
                    AssertTrue(ranked.All(item => item.MinimumYear <= at.Year), $"no future recommendation at {at:yyyy-MM-dd}, seed {seed}");
                }
            }

            var in2002 = LeisureActivityCatalog.AvailableOn(new DateTime(2002, 1, 5, 20, 0, 0), 4);
            AssertTrue(in2002.Any(item => item.Id == "adsl_coop_game_night"), "ADSL activity appears in 2002");
        }

        private static void ValidateDeterministicRecommendations()
        {
            var at = new DateTime(2000, 1, 8, 14, 30, 0);
            var canonical = new[] { "player", "father", "mother", "older_sister" };
            var reorderedWithDuplicate = new[] { "mother", "player", "father", "player", "older_sister" };
            var first = Fingerprint(LeisureRecommendationRules.RankRecommendations("family-weekend", at, canonical, 6));
            var reordered = Fingerprint(LeisureRecommendationRules.RankRecommendations("family-weekend", at, reorderedWithDuplicate, 6));
            AssertEqual(first, reordered, "participant order and duplicates do not change recommendation");

            for (var replay = 0; replay < 100; replay++)
            {
                AssertEqual(
                    first,
                    Fingerprint(LeisureRecommendationRules.RankRecommendations("family-weekend", at, canonical, 6)),
                    $"deterministic recommendation replay {replay}");
            }

            var primaryPaths = new HashSet<string>(StringComparer.Ordinal);
            for (var seed = 0; seed < 32; seed++)
            {
                var recommendation = LeisureRecommendationRules.Recommend($"leisure-seed-{seed}", at, canonical);
                AssertTrue(recommendation != null, $"recommendation exists for seed {seed}");
                primaryPaths.Add(recommendation.Id);
            }
            AssertTrue(primaryPaths.Count >= 2, "multiple seeds do not collapse to one recommendation path");
        }

        private static void ValidateEffectBoundaries()
        {
            var dinner = Required("family_restaurant_dinner");
            var recovered = LeisureRecommendationRules.PreviewEffects(
                dinner,
                4,
                currentEnergy: 95,
                currentStress: 3,
                currentFamilyBond: 98);
            AssertEqual(100, recovered.EnergyAfter, "energy upper clamp");
            AssertEqual(0, recovered.StressAfter, "stress lower clamp");
            AssertEqual(100, recovered.FamilyBondAfter, "family bond upper clamp");
            AssertEqual(dinner.SharedFamilyBondDelta, recovered.AppliedFamilyBondDelta, "shared bond applied");
            AssertEqual(32_000L, recovered.CostWon, "integer-won cost preserved");
            AssertEqual(100, recovered.DurationMinutes, "integer-minute duration preserved");

            var soloWalk = LeisureRecommendationRules.PreviewEffects(
                Required("neighborhood_evening_walk"),
                1,
                currentEnergy: 0,
                currentStress: 100,
                currentFamilyBond: 50);
            AssertEqual(0, soloWalk.AppliedFamilyBondDelta, "solo activity cannot create shared family bond");
            AssertEqual(50, soloWalk.FamilyBondAfter, "solo family bond unchanged");
        }

        private static LeisureActivityDefinition Required(string id)
        {
            var activity = LeisureActivityCatalog.FindById(id);
            if (activity == null) throw new InvalidOperationException($"Missing required activity {id}.");
            return activity;
        }

        private static string Fingerprint(IEnumerable<LeisureActivityDefinition> activities)
        {
            return string.Join("|", activities.Select(item => item.Id));
        }

        private static void AssertTrue(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException($"{label}: expected true.");
        }

        private static void AssertEqual<T>(T expected, T actual, string label)
        {
            if (!Equals(expected, actual))
                throw new InvalidOperationException($"{label}: expected {expected}, got {actual}.");
        }
    }
}
