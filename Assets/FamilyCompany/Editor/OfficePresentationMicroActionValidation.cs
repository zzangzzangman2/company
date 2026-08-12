using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Save;
using FamilyCompany.Simulation.Family;
using FamilyCompany.Simulation.Game;
using FamilyCompany.Simulation.Prototype;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public static class OfficePresentationMicroActionValidation
    {
        private const int ValidationSeed = 20000103;
        private const int ValidationMinutes = 4 * 60;
        private static readonly string[] NpcIds = { "older_sister", "father", "mother" };

        private sealed class Sample
        {
            public string MemberId;
            public OfficeMicroAction Action;
            public OfficeSemanticLocation Location;
            public string TargetId;
            public long StartedMinute;
            public long EndsMinute;
            public int SequenceIndex;
            public string PartnerId;
        }

        [MenuItem("Family Company/Validate Office Presentation Micro Actions")]
        public static void Run()
        {
            try
            {
                ValidateFourHours();
                ValidateSaveAndJumpDeterminism();
                ValidatePresentationOnlyBoundary();
                Debug.Log("FAMILY_COMPANY_OFFICE_PRESENTATION_MICRO_ACTION_VALIDATION: PASS");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("FAMILY_COMPANY_OFFICE_PRESENTATION_MICRO_ACTION_VALIDATION: FAIL");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        private static void ValidateFourHours()
        {
            GameState state = PrototypeStateFactory.Create(ValidationSeed);
            var runner = new SimulationRunner(state);
            var samples = NpcIds.ToDictionary(
                id => id,
                _ => new List<Sample>(),
                StringComparer.Ordinal);
            var lastSequence = NpcIds.ToDictionary(id => id, _ => -1, StringComparer.Ordinal);
            for (var minute = 1; minute <= ValidationMinutes; minute++)
            {
                runner.AdvanceMinutes(1);
                ValidateReservationsAndPartners(state, minute);
                foreach (string memberId in NpcIds)
                {
                    FamilyMemberState member = state.Family.Get(memberId);
                    OfficeMicroActionState micro = member.Autonomy.MicroAction;
                    if (micro.Action == OfficeMicroAction.None ||
                        micro.SequenceIndex == lastSequence[memberId]) continue;
                    lastSequence[memberId] = micro.SequenceIndex;
                    samples[memberId].Add(new Sample
                    {
                        MemberId = memberId,
                        Action = micro.Action,
                        Location = micro.TargetLocation,
                        TargetId = micro.TargetId,
                        StartedMinute = micro.StartedMinute,
                        EndsMinute = micro.EndsMinute,
                        SequenceIndex = micro.SequenceIndex,
                        PartnerId = micro.PartnerMemberId
                    });
                }
            }

            foreach (string memberId in NpcIds)
            {
                List<Sample> memberSamples = samples[memberId];
                int uniqueLocations = memberSamples
                    .Where(sample => sample.Location != OfficeSemanticLocation.None &&
                                     sample.Location != OfficeSemanticLocation.Exit)
                    .Select(sample => sample.Location)
                    .Distinct()
                    .Count();
                Require(uniqueLocations >= 3, $"{memberId} visited only {uniqueLocations} office locations");
                for (var index = 1; index < memberSamples.Count; index++)
                {
                    Require(
                        memberSamples[index - 1].Action != memberSamples[index].Action,
                        $"{memberId} repeated {memberSamples[index].Action} consecutively");
                    bool filingCopierBounce =
                        memberSamples[index - 1].Action == OfficeMicroAction.FilingDocument &&
                        memberSamples[index].Action == OfficeMicroAction.UsingCopier ||
                        memberSamples[index - 1].Action == OfficeMicroAction.UsingCopier &&
                        memberSamples[index].Action == OfficeMicroAction.FilingDocument;
                    Require(!filingCopierBounce, $"{memberId} bounced directly between filing and copier");
                }
                Require(MaximumDeskResidence(memberSamples) <= OfficeMicroActionRules.MaximumDeskResidenceMinutes,
                    $"{memberId} exceeded the 45 minute desk presentation limit");
                Debug.Log(
                    $"OFFICE_PRESENTATION_MICRO_ACTION_4H_SAMPLE | member={memberId} " +
                    $"sequences={memberSamples.Count} uniqueLocations={uniqueLocations} " +
                    $"maxDeskMinutes={MaximumDeskResidence(memberSamples)} actions=" +
                    string.Join(",", memberSamples.Select(sample =>
                        $"{sample.StartedMinute}:{sample.Action}@{sample.Location}")));
            }
        }

        private static void ValidateReservationsAndPartners(GameState state, int minute)
        {
            OfficeMicroActionState[] active = state.Family.Members
                .Select(member => member.Autonomy.MicroAction)
                .Where(micro => micro.Action != OfficeMicroAction.None && micro.EndsMinute > minute)
                .ToArray();
            foreach (IGrouping<string, OfficeMicroActionState> group in active
                         .Where(micro => !string.IsNullOrEmpty(micro.TargetId))
                         .GroupBy(micro => micro.TargetId, StringComparer.Ordinal))
            {
                Require(group.Count() <= OfficeMicroActionRules.Capacity(group.Key),
                    $"reservation capacity exceeded at {group.Key}");
            }

            foreach (FamilyMemberState member in state.Family.Members)
            {
                OfficeMicroActionState micro = member.Autonomy.MicroAction;
                if (micro.Action != OfficeMicroAction.ShortConversation || micro.EndsMinute <= minute) continue;
                FamilyMemberState partner = state.Family.Members.FirstOrDefault(candidate =>
                    string.Equals(candidate.MemberId, micro.PartnerMemberId, StringComparison.Ordinal));
                Require(partner != null, $"conversation partner missing for {member.MemberId}");
                OfficeMicroActionState partnerMicro = partner.Autonomy.MicroAction;
                Require(partnerMicro.Action == OfficeMicroAction.ShortConversation &&
                        string.Equals(partnerMicro.PartnerMemberId, member.MemberId, StringComparison.Ordinal) &&
                        string.Equals(partnerMicro.TargetId, micro.TargetId, StringComparison.Ordinal) &&
                        partnerMicro.StartedMinute == micro.StartedMinute &&
                        partnerMicro.EndsMinute == micro.EndsMinute,
                    $"conversation pair mismatch for {member.MemberId}/{partner.MemberId}");
            }
        }

        private static int MaximumDeskResidence(IReadOnlyList<Sample> samples)
        {
            var maximum = 0;
            var started = -1L;
            var ended = -1L;
            foreach (Sample sample in samples.OrderBy(item => item.StartedMinute))
            {
                if (sample.Location == OfficeSemanticLocation.Desk)
                {
                    if (started < 0 || sample.StartedMinute > ended)
                        started = sample.StartedMinute;
                    ended = Math.Max(ended, sample.EndsMinute);
                    maximum = Math.Max(maximum, (int)(ended - started));
                }
                else
                {
                    started = -1;
                    ended = -1;
                }
            }
            return maximum;
        }

        private static void ValidateSaveAndJumpDeterminism()
        {
            GameState stepped = PrototypeStateFactory.Create(ValidationSeed);
            var steppedRunner = new SimulationRunner(stepped);
            for (var minute = 0; minute < ValidationMinutes; minute++) steppedRunner.AdvanceMinutes(1);

            GameState bulk = PrototypeStateFactory.Create(ValidationSeed);
            new SimulationRunner(bulk).AdvanceMinutes(ValidationMinutes);
            AssertSerializedEqual(stepped, bulk, "one-minute steps versus four-hour jump");

            GameState saveSource = PrototypeStateFactory.Create(ValidationSeed);
            var sourceRunner = new SimulationRunner(saveSource);
            for (var minute = 0; minute < ValidationMinutes / 2; minute++) sourceRunner.AdvanceMinutes(1);
            GameSaveDto save = GameSaveMapper.ToDto(saveSource);
            Require(save.schemaVersion == 7, "micro-action save must use schema v7");
            GameState restored = GameSaveMapper.FromDto(save);
            var restoredRunner = new SimulationRunner(restored);
            for (var minute = ValidationMinutes / 2; minute < ValidationMinutes; minute++)
                restoredRunner.AdvanceMinutes(1);
            AssertSerializedEqual(stepped, restored, "save/load continuation");
        }

        private static void ValidatePresentationOnlyBoundary()
        {
            GameState state = PrototypeStateFactory.Create(ValidationSeed + 19);
            AutonomousOfficeSimulation.EnsureIntents(state.WorldSeed, state.Family, 0);
            var before = state.Family.Members.ToDictionary(
                member => member.MemberId,
                member => $"{member.Energy}:{member.Stress}:{member.Autonomy.CompletedWorkBlocks}:" +
                          member.Autonomy.CompletedBreaks,
                StringComparer.Ordinal);
            OfficePresentationMicroActionSimulation.AdvanceTo(state.WorldSeed, state.Family, ValidationMinutes);
            foreach (FamilyMemberState member in state.Family.Members)
            {
                string after = $"{member.Energy}:{member.Stress}:{member.Autonomy.CompletedWorkBlocks}:" +
                               member.Autonomy.CompletedBreaks;
                Require(string.Equals(before[member.MemberId], after, StringComparison.Ordinal),
                    $"micro-action mutated macro economy for {member.MemberId}");
            }
        }

        private static void AssertSerializedEqual(GameState expected, GameState actual, string label)
        {
            string expectedJson = JsonUtility.ToJson(GameSaveMapper.ToDto(expected));
            string actualJson = JsonUtility.ToJson(GameSaveMapper.ToDto(actual));
            Require(string.Equals(expectedJson, actualJson, StringComparison.Ordinal),
                label + " serialized states differ");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
