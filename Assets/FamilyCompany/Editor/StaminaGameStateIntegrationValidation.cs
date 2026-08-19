#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Save;
using FamilyCompany.Simulation.Game;
using FamilyCompany.Simulation.Prototype;
using FamilyCompany.Simulation.Stamina;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public static class StaminaGameStateIntegrationValidation
    {
        [MenuItem("Family Company/Validate Stamina GameState Integration")]
        public static void Run()
        {
            try
            {
                ValidateDefaultRosterAndSaveV9();
                ValidateOneTwoFourMinutePartitioning();
                ValidateBoundaryAndMandatoryPriority();
                Debug.Log("FAMILY_COMPANY_STAMINA_GAMESTATE_INTEGRATION: PASS");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("FAMILY_COMPANY_STAMINA_GAMESTATE_INTEGRATION: FAIL");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        private static void ValidateDefaultRosterAndSaveV9()
        {
            GameState source = PrototypeStateFactory.Create(20000103);
            Require(source.Stamina.Count == 4, "four-family stamina roster");
            Require(source.Stamina.CharacterIds.SequenceEqual(
                    new[] { "father", "mother", "older_sister", "player" }),
                "stable family stamina IDs");
            Require(source.Stamina.CharacterIds.All(id =>
                    source.Stamina.GetSimulation(id).State.CurrentUnits == 10_000),
                "common initial stamina");

            new SimulationRunner(source).AdvanceMinutes(240);
            GameSaveDto dto = GameSaveMapper.ToDto(source);
            Require(dto.schemaVersion == 11 && dto.staminaState != null,
                "top-level save stamina payload");
            string before = JsonUtility.ToJson(dto.staminaState);
            GameState restored = GameSaveMapper.FromDto(dto);
            string after = JsonUtility.ToJson(restored.Stamina.ExportSnapshot());
            Require(string.Equals(before, after, StringComparison.Ordinal),
                "stamina save/load roundtrip");
            foreach (var member in restored.Family.Members)
            {
                CharacterStaminaSimulation simulation = restored.Stamina.GetSimulation(member.MemberId);
                Require(member.Energy == simulation.Profile.LegacyPercent(
                        simulation.State.CurrentUnits),
                    member.MemberId + " compatibility energy projection");
            }

            dto.schemaVersion = 8;
            dto.staminaState = null;
            GameState migrated = GameSaveMapper.FromDto(dto);
            Require(migrated.Stamina.LastProcessedMinute == dto.elapsedMinutes,
                "v8 migration starts at saved minute");
            foreach (var member in migrated.Family.Members)
            {
                CharacterStaminaSimulation simulation = migrated.Stamina.GetSimulation(member.MemberId);
                Require(simulation.State.CurrentUnits ==
                        simulation.Profile.UnitsFromLegacyPercent(member.Energy),
                    member.MemberId + " v8 energy migration");
            }
        }

        private static void ValidateOneTwoFourMinutePartitioning()
        {
            string one = RunPartition(1);
            string two = RunPartition(2);
            string four = RunPartition(4);
            Require(string.Equals(one, two, StringComparison.Ordinal), "1x/2x stamina signature");
            Require(string.Equals(one, four, StringComparison.Ordinal), "1x/4x stamina signature");
        }

        private static string RunPartition(int step)
        {
            GameState state = PrototypeStateFactory.Create(20000103 + step);
            // Normalize the seed so only call partition differs.
            state = PrototypeStateFactory.Create(20000103);
            var bridge = new ImmediateRecoveryBridge();
            state.BindStaminaRuntimeBridge(bridge);
            var runner = new SimulationRunner(state);
            const int total = 800;
            for (int advanced = 0; advanced < total; advanced += step)
                runner.AdvanceMinutes(Math.Min(step, total - advanced));
            state.UnbindStaminaRuntimeBridge(bridge);
            return JsonUtility.ToJson(state.Stamina.ExportSnapshot());
        }

        private static void ValidateBoundaryAndMandatoryPriority()
        {
            GameState state = PrototypeStateFactory.Create(20000103);
            var bridge = new ImmediateRecoveryBridge("father");
            state.BindStaminaRuntimeBridge(bridge);
            new SimulationRunner(state).AdvanceMinutes(469);
            Require(state.Time.ElapsedMinutes == 469, "GameTime stops exactly at requested boundary");
            foreach (string id in state.Stamina.CharacterIds)
            {
                CharacterStaminaSimulation simulation = state.Stamina.GetSimulation(id);
                if (id == "father")
                {
                    Require(simulation.State.RecoveryPhase == StaminaRecoveryPhase.Working,
                        "mandatory schedule blocks autonomous departure");
                    Require(simulation.State.CurrentUnits <= simulation.Profile.RecoveryThresholdUnits,
                        "priority block does not fabricate recovery");
                }
                else
                {
                    Require(simulation.State.RecoveryPhase == StaminaRecoveryPhase.Performing,
                        id + " threshold decision processed at the same GameTime minute");
                    Require(simulation.State.CurrentUnits <= simulation.Profile.RecoveryThresholdUnits,
                        id + " drains roughly 75 percent before departure");
                }
            }
        }

        private sealed class ImmediateRecoveryBridge : ICharacterStaminaRuntimeBridge
        {
            private readonly HashSet<string> _blocked;

            public ImmediateRecoveryBridge(params string[] blocked) =>
                _blocked = new HashSet<string>(blocked ?? Array.Empty<string>(), StringComparer.Ordinal);

            public bool IsOfficeRecoveryAllowed(string characterId) => !_blocked.Contains(characterId);
            public StaminaActivityKind ResolveActivity(string characterId) => StaminaActivityKind.Typing;

            public void ProcessPendingDecisions(CharacterStaminaRoster roster, long minute)
            {
                foreach (string id in roster.CharacterIds)
                {
                    CharacterStaminaSimulation simulation = roster.GetSimulation(id);
                    if (simulation.State.RecoveryPhase == StaminaRecoveryPhase.RecoveryRequested &&
                        simulation.HasPendingRuntimeDecision)
                    {
                        var candidates = new[]
                        {
                            new StaminaRecoveryCandidate(
                                StaminaRecoveryActivity.Water,
                                "water-drink",
                                "qa-water",
                                true,
                                true)
                        };
                        Require(StaminaRecoveryPlanner.TrySelect(simulation, candidates, out StaminaRecoveryPlan plan),
                            id + " immediate plan");
                        simulation.AcceptRecoveryPlan(plan, minute);
                        simulation.ConfirmSafeStopCompleted(plan.RequestKey, minute);
                        simulation.ConfirmStandUpCompleted(plan.RequestKey, minute);
                        simulation.ConfirmFacilityArrived(plan.RequestKey, minute);
                        simulation.ConfirmFacingAlignedAndPerforming(plan.RequestKey, minute);
                    }
                    if (simulation.State.RecoveryPhase == StaminaRecoveryPhase.Performing &&
                        simulation.IsRecoveryReadyToComplete)
                    {
                        string key = simulation.RecoveryRequestKey;
                        Require(simulation.CanCompleteRuntimeInteraction(key, minute), id + " completion preflight");
                        simulation.ConfirmInteractionCompleted(key, minute);
                        string returnKey = simulation.AssignedSeatReturnRequestKey;
                        simulation.ConfirmAssignedSeatReturned(returnKey, minute);
                    }
                }
            }
        }

        private static void Require(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException("Assertion failed: " + label);
        }
    }
}
#endif
