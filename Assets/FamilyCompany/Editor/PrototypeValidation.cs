using System;
using System.Linq;
using FamilyCompany.Save;
using FamilyCompany.Simulation.Core;
using FamilyCompany.Simulation.Events;
using FamilyCompany.Simulation.Prototype;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public static class PrototypeValidation
    {
        [MenuItem("Family Company/Validate Prototype 0.1")]
        public static void Run()
        {
            try
            {
                ValidateStartingFamily();
                ValidateStableRandom();
                ValidateEventOrdering();
                ValidateTimeAndLedger();
                ValidateSaveRoundTrip();
                ValidateAssetsAndScene();
                Debug.Log("FAMILY_COMPANY_VALIDATION: PASS");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("FAMILY_COMPANY_VALIDATION: FAIL");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        private static void ValidateStartingFamily()
        {
            var state = PrototypeStateFactory.Create();
            AssertEqual(14, state.Family.Get("player").AgeAt(state.Time), "player age");
            AssertEqual(20, state.Family.Get("older_sister").AgeAt(state.Time), "sister age");
            AssertEqual(46, state.Family.Get("father").AgeAt(state.Time), "father age");
            AssertEqual(44, state.Family.Get("mother").AgeAt(state.Time), "mother age");
        }

        private static void ValidateStableRandom()
        {
            AssertEqual(1726110163, StableRandom.StableHash31("family-company"), "stable hash fixture");
            AssertEqual(877381839, StableRandom.StableRandomWord31("family-company"), "random word fixture");
            AssertEqual(25, StableRandom.StableRandomInt("family-company", 37), "random int fixture");
            for (var bound = 1; bound <= 100; bound++)
            {
                var key = $"validation:{bound}";
                var first = StableRandom.StableRandomInt(key, bound);
                AssertEqual(first, StableRandom.StableRandomInt(key, bound), "random replay");
                if (first < 0 || first >= bound) throw new InvalidOperationException("Random result is out of bounds.");
            }
        }

        private static void ValidateEventOrdering()
        {
            var queue = new DeterministicEventQueue(new[]
            {
                new ScheduledEvent("z", 10, 1, "test"),
                new ScheduledEvent("b", 10, 0, "test"),
                new ScheduledEvent("a", 10, 0, "test"),
                new ScheduledEvent("early", 5, 9, "test")
            });
            var order = string.Join(",", queue.DequeueDue(10).Select(item => item.EventId));
            AssertEqual("early,a,b,z", order, "event order");
        }

        private static void ValidateTimeAndLedger()
        {
            var state = PrototypeStateFactory.Create();
            var runner = new SimulationRunner(state);
            var due = runner.AdvanceMinutes(60);
            AssertEqual(60L, state.Time.ElapsedMinutes, "time advance");
            AssertEqual(1, due.Count, "due event count");
            AssertEqual(5_000_000L, state.Company.CashWon, "opening cash");
            foreach (var transaction in state.Company.Ledger)
            {
                AssertEqual(transaction.TotalDebitWon, transaction.TotalCreditWon, "balanced ledger");
            }
        }

        private static void ValidateSaveRoundTrip()
        {
            var source = PrototypeStateFactory.Create(314159);
            new SimulationRunner(source).AdvanceMinutes(1500);
            var json = JsonUtility.ToJson(GameSaveMapper.ToDto(source));
            var restored = GameSaveMapper.FromDto(JsonUtility.FromJson<GameSaveDto>(json));
            AssertEqual(source.WorldSeed, restored.WorldSeed, "save seed");
            AssertEqual(source.Time.ElapsedMinutes, restored.Time.ElapsedMinutes, "save time");
            AssertEqual(source.Company.CashWon, restored.Company.CashWon, "save cash");
            AssertEqual(source.Family.Get("older_sister").Energy, restored.Family.Get("older_sister").Energy, "save sister energy");
            AssertEqual(source.Events.Count, restored.Events.Count, "save event count");
        }

        private static void ValidateAssetsAndScene()
        {
            var sister = AssetDatabase.LoadAssetAtPath<Sprite>(PrototypeProjectBuilder.SisterAssetPath);
            if (sister == null) throw new InvalidOperationException("Canonical sister sprite is missing or not imported as a Sprite.");
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(PrototypeProjectBuilder.ScenePath);
            if (scene == null) throw new InvalidOperationException("Prototype scene is missing.");
        }

        private static void AssertEqual<T>(T expected, T actual, string label)
        {
            if (!Equals(expected, actual))
            {
                throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
            }
        }
    }
}

