using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Simulation.Family;
using FamilyCompany.Simulation.OfficeInteractions;
using FamilyCompany.Simulation.Prototype;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public static class OfficeInteractionCatalogValidation
    {
        private static readonly AutonomousOfficeAction[] StandardMacros =
        {
            AutonomousOfficeAction.FocusWork,
            AutonomousOfficeAction.Administration,
            AutonomousOfficeAction.Reception,
            AutonomousOfficeAction.Printing,
            AutonomousOfficeAction.ShortBreak,
            AutonomousOfficeAction.DeepRest,
            AutonomousOfficeAction.CoffeeBreak,
            AutonomousOfficeAction.SocialChat,
            AutonomousOfficeAction.BurnoutRecovery
        };

        [MenuItem("Family Company/Validate Office Interaction Catalog")]
        public static void Run()
        {
            try
            {
                ValidateDefinitions();
                int parityCases = ValidateLegacyParity();
                Debug.Log(
                    "OFFICE_INTERACTION_CATALOG_VALIDATION: PASS | " +
                    $"definitions={OfficeInteractionCatalog.All.Count} microActions=13 parityCases={parityCases}");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("OFFICE_INTERACTION_CATALOG_VALIDATION: FAIL");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        private static void ValidateDefinitions()
        {
            IReadOnlyList<OfficeInteractionDefinition> definitions = OfficeInteractionCatalog.All;
            Require(definitions.Count > 0, "interaction catalog is empty");
            Require(definitions.Select(item => item.InteractionId).Distinct(StringComparer.Ordinal).Count() ==
                    definitions.Count,
                "interaction IDs must be unique");
            Require(definitions.All(item => item.MicroAction != OfficeMicroAction.None),
                "MicroAction.None must not be defined");
            Require(definitions.All(item => item.MinimumDurationMinutes <= item.MaximumDurationMinutes),
                "duration range is invalid");
            Require(definitions.All(item => item.Capacity >= 1), "capacity must be positive");
            Require(definitions.All(item => item.CooldownMinutes >= 0), "cooldown must not be negative");
            Require(definitions.Where(item => item.RequiresFurniture)
                    .All(item => !string.IsNullOrWhiteSpace(item.FurnitureKindId)),
                "furniture-backed definition has no furniture kind");
            Require(definitions.Where(item => !item.RequiresFurniture)
                    .All(item => string.IsNullOrEmpty(item.FurnitureKindId)),
                "open/current definition must not name furniture");
            Require(definitions.Where(item => item.SemanticLocation == OfficeSemanticLocation.Desk)
                    .All(item => item.ReservationPolicy == OfficeInteractionReservationPolicy.AssignedSeat),
                "desk definitions must use assigned seat reservation");
            OfficeInteractionDefinition conversation = definitions.Single(item =>
                item.MicroAction == OfficeMicroAction.ShortConversation);
            Require(conversation.ReservationPolicy == OfficeInteractionReservationPolicy.PairedConversation,
                "conversation must use paired reservation");
            OfficeMicroAction[] actions = definitions.Select(item => item.MicroAction).Distinct().OrderBy(item => item).ToArray();
            OfficeMicroAction[] expected = Enum.GetValues(typeof(OfficeMicroAction)).Cast<OfficeMicroAction>()
                .Where(item => item != OfficeMicroAction.None).OrderBy(item => item).ToArray();
            Require(actions.SequenceEqual(expected), "catalog must cover all 13 micro actions");
        }

        private static int ValidateLegacyParity()
        {
            var parityCases = 0;
            foreach (AutonomousOfficeAction macro in StandardMacros.Concat(new[] { AutonomousOfficeAction.Meeting }))
            {
                var state = PrototypeStateFactory.Create(20000103 + (int)macro);
                foreach (FamilyMemberState source in state.Family.Members)
                {
                    foreach (OfficeSemanticLocation previousLocation in new[]
                             {
                                 OfficeSemanticLocation.Desk,
                                 OfficeSemanticLocation.Filing
                             })
                    {
                        var member = new FamilyMemberState(
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
                            new OfficeAutonomyState(
                                macro,
                                OfficeSemanticLocation.Desk,
                                0,
                                60,
                                microAction: new OfficeMicroActionState(
                                    targetLocation: previousLocation)));
                        AssertParity(member);
                        parityCases++;
                    }
                }
            }
            return parityCases;
        }

        private static void AssertParity(FamilyMemberState member)
        {
            string[] legacy = OfficePresentationMicroActionSimulation.LegacyCandidateSnapshots(member)
                .Select(Key).OrderBy(item => item, StringComparer.Ordinal).ToArray();
            string[] catalog = OfficeInteractionCatalog.CandidatesFor(member)
                .Select(item => Key(new OfficeInteractionCandidateSnapshot(
                    item.MicroAction,
                    item.TargetId,
                    item.SemanticLocation,
                    item.LegacyWeight)))
                .OrderBy(item => item, StringComparer.Ordinal).ToArray();
            Require(legacy.SequenceEqual(catalog),
                $"catalog parity changed for {member.MemberId}/{member.Autonomy.CurrentAction}\n" +
                "legacy=" + string.Join(",", legacy) + "\ncatalog=" + string.Join(",", catalog));
        }

        private static string Key(OfficeInteractionCandidateSnapshot item)
        {
            return $"{(int)item.Action}|{(int)item.Location}|{item.TargetId}|{item.Weight}";
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
