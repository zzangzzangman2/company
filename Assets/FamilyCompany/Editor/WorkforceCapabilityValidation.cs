using System;
using System.IO;
using System.Linq;
using FamilyCompany.Presentation.Unity.MainNavigation;
using FamilyCompany.Save;
using FamilyCompany.Simulation.ContractGrowth;
using FamilyCompany.Simulation.Contracts;
using FamilyCompany.Simulation.Prototype;
using FamilyCompany.Simulation.Workforce;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public static class WorkforceCapabilityValidation
    {
        [MenuItem("Family Company/Validate Workforce Capabilities V10")]
        public static void Run()
        {
            try
            {
                ValidatePotentialGrades();
                ValidateStarterFamily();
                ValidateLegacyMigrationAndSave();
                ValidateWeightsRateAndChunkIndependence();
                ValidateAuthoritativeContractContribution();
                ValidateRosterViewModel();
                ValidateRemovedLegacyAuthority();
                Debug.Log("WORKFORCE_CAPABILITY_V10_VALIDATION: PASS skills=6 grades=S-F save=v10 chunk=1x/2x/4x roster=4");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("WORKFORCE_CAPABILITY_V10_VALIDATION: FAIL");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        private static void ValidatePotentialGrades()
        {
            AssertGrade(100, WorkforcePotentialGrade.S); AssertGrade(90, WorkforcePotentialGrade.S);
            AssertGrade(89, WorkforcePotentialGrade.A); AssertGrade(80, WorkforcePotentialGrade.A);
            AssertGrade(79, WorkforcePotentialGrade.B); AssertGrade(65, WorkforcePotentialGrade.B);
            AssertGrade(64, WorkforcePotentialGrade.C); AssertGrade(50, WorkforcePotentialGrade.C);
            AssertGrade(49, WorkforcePotentialGrade.D); AssertGrade(35, WorkforcePotentialGrade.D);
            AssertGrade(34, WorkforcePotentialGrade.F); AssertGrade(0, WorkforcePotentialGrade.F);
        }

        private static void ValidateStarterFamily()
        {
            var state = PrototypeStateFactory.Create(20000103);
            AssertCapability(state.Family.Get("player").Capability, 58, 61, 47, 32, 62, 55, "A", 58);
            AssertCapability(state.Family.Get("older_sister").Capability, 37, 52, 44, 55, 62, 72, "B", 65);
            AssertCapability(state.Family.Get("father").Capability, 24, 45, 23, 68, 54, 61, "D", 72);
            AssertCapability(state.Family.Get("mother").Capability, 32, 55, 35, 46, 60, 70, "C", 76);
        }

        private static void ValidateLegacyMigrationAndSave()
        {
            var state = PrototypeStateFactory.Create(1234);
            var legacy = GameSaveMapper.ToDto(state);
            legacy.schemaVersion = 9;
            foreach (var dto in legacy.family)
            {
                var member = state.Family.Get(dto.memberId);
                dto.capability = null;
                dto.development = member.Stats.Development;
                dto.speed = member.Stats.Speed;
                dto.stamina = member.Stats.Stamina;
                dto.planning = member.Stats.Planning;
                dto.art = member.Stats.Art;
                dto.sales = member.Stats.Sales;
                dto.mental = member.Stats.Mental;
                dto.teamwork = member.Stats.Teamwork;
                dto.loyalty = member.Stats.Loyalty;
                dto.potential = member.Stats.Potential;
            }
            var migrated = GameSaveMapper.FromDto(legacy);
            var saved = GameSaveMapper.ToDto(migrated);
            Require(saved.schemaVersion == 10 && saved.family.All(item => item.capability != null),
                "v1-v9 migration did not emit complete v10 capability snapshots.");
            AssertCapability(migrated.Family.Get("player").Capability, 58, 61, 47, 32, 62, 55, "A", 58);
            var json = JsonUtility.ToJson(saved);
            var restored = GameSaveMapper.FromDto(JsonUtility.FromJson<GameSaveDto>(json));
            Require(JsonUtility.ToJson(saved.family[0].capability) ==
                    JsonUtility.ToJson(restored.Family.Get(saved.family[0].memberId).Capability.ExportSnapshot()),
                "v10 capability save/load is not stable.");
        }

        private static void ValidateWeightsRateAndChunkIndependence()
        {
            var weights = new WorkSkillWeights(2000, 2000, 1500, 1500, 1500, 1500);
            var task = new WorkTaskProfile("qa.general", weights, weights, weights);
            var low = NewCapability("low", 0, 50);
            var middle = NewCapability("middle", 50, 50);
            var high = NewCapability("high", 100, 50);
            Require(WorkforcePerformanceRules.CalculateWorkRateBasisPoints(low, task) == 7000, "skill 0 must mean 70% rate.");
            Require(WorkforcePerformanceRules.CalculateWorkRateBasisPoints(middle, task) == 10000, "skill 50 must mean 100% rate.");
            Require(WorkforcePerformanceRules.CalculateWorkRateBasisPoints(high, task) == 13000, "skill 100 must mean 130% rate.");
            Require(WorkforcePerformanceRules.CalculateGameMinutesPerPersonHour(low, task) == 86,
                "skill 0 must require 86 integer GameTime minutes per person-hour.");
            Require(WorkforcePerformanceRules.CalculateGameMinutesPerPersonHour(middle, task) == 60,
                "skill 50 must require 60 integer GameTime minutes per person-hour.");
            Require(WorkforcePerformanceRules.CalculateGameMinutesPerPersonHour(high, task) == 47,
                "skill 100 must require 47 integer GameTime minutes per person-hour.");

            var one = NewCapability("same", 50, 72);
            var two = NewCapability("same", 50, 72);
            var four = NewCapability("same", 50, 72);
            WorkforceGrowthRules.ApplyAuthoritativeContributionMinutes(one, task, 480);
            for (var index = 0; index < 2; index++)
                WorkforceGrowthRules.ApplyAuthoritativeContributionMinutes(two, task, 240);
            for (var index = 0; index < 4; index++)
                WorkforceGrowthRules.ApplyAuthoritativeContributionMinutes(four, task, 120);
            var expected = JsonUtility.ToJson(one.ExportSnapshot());
            Require(expected == JsonUtility.ToJson(two.ExportSnapshot()) &&
                    expected == JsonUtility.ToJson(four.ExportSnapshot()),
                "XP differs between 1x/2x/4x authoritative time chunks.");
        }

        private static void ValidateAuthoritativeContractContribution()
        {
            var state = PrototypeStateFactory.Create(55);
            var offer = new SubcontractOffer("workforce-qa", "qa-client", "QA 고객", ContractServiceType.WebsiteMaintenance,
                "능력치 연결 검증", 1, 1, 5, 0, 100_000, 0, requiredCapability: 0);
            Require(state.Contracts.Accept(offer, state.Company, state.Family, state.Growth, 0).Accepted,
                "Capability-backed contract was not accepted.");
            var before = state.Family.Get("player").Capability.ExportSnapshot();
            var capability = state.Family.Get("player").Capability;
            var task = ContractWorkTaskProfiles.Resolve(LegacyContractTemplateCatalog.ResolveSpecialty(offer));
            var requiredMinutes = WorkforcePerformanceRules.CalculateGameMinutesPerPersonHour(capability, task);
            Require(!state.Contracts.RecordWork(offer.OfferId, "player", 1, requiredMinutes - 1,
                    state.Family, state.Company).Applied,
                "Sub-hour render/UI time created contract work.");
            var result = state.Contracts.RecordWork(offer.OfferId, "player", 1, requiredMinutes,
                state.Family, state.Company);
            var after = state.Family.Get("player").Capability.ExportSnapshot();
            Require(result.Completed && after.progress.Sum(item => item.experience * 100_000_000L + item.fixedPointRemainder) >
                    before.progress.Sum(item => item.experience * 100_000_000L + item.fixedPointRemainder),
                "Applied authoritative contract time did not create capability XP.");
        }

        private static void ValidateRosterViewModel()
        {
            var roster = WorkforceRosterViewModelRules.Create(PrototypeStateFactory.Create(77));
            Require(roster.Count == 4 && roster.All(item => item.EmploymentTypeKo == "창업 가족"),
                "Starting roster must contain only the four founding family members.");
            Require(roster.All(item => item.Skills.Count == 6 && item.PotentialGrade.Length == 1),
                "Roster must expose six skills and one potential letter grade.");
            Require(roster.All(item => item.StaminaBasisPoints >= 0 && item.StressResistancePercent >= 0),
                "Current state read model is incomplete.");
        }

        private static void ValidateRemovedLegacyAuthority()
        {
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, "FamilyCompany"));
            var allowed = Path.DirectorySeparatorChar + "Workforce" + Path.DirectorySeparatorChar +
                          "LegacyWorkforceCapabilityMigration.cs";
            var offenders = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.EndsWith(allowed, StringComparison.OrdinalIgnoreCase) &&
                               !path.Contains(Path.DirectorySeparatorChar + "Editor" + Path.DirectorySeparatorChar))
                .Where(path =>
                {
                    var source = File.ReadAllText(path);
                    return source.Contains("Stats.Speed") || source.Contains("Stats.Stamina") || source.Contains("Stats.Mental");
                }).ToArray();
            Require(offenders.Length == 0,
                "Removed legacy Speed/Stamina/Mental remains in new authority: " + string.Join(", ", offenders));
            var contractSpeedCompatibilityFiles = new[]
            {
                Path.Combine("Simulation", "Contracts", "SubcontractOffer.cs"),
                Path.Combine("Simulation", "Contracts", "BootstrapContractCatalog.cs"),
                Path.Combine("Save", "GameSaveDto.cs"),
                Path.Combine("Save", "GameSaveMapper.cs")
            };
            var contractSpeedOffenders = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains(Path.DirectorySeparatorChar + "Editor" + Path.DirectorySeparatorChar))
                .Where(path => contractSpeedCompatibilityFiles.All(relative =>
                    !path.EndsWith(relative, StringComparison.OrdinalIgnoreCase)))
                .Where(path => File.ReadAllText(path).Contains("RequiredSpeed"))
                .ToArray();
            Require(contractSpeedOffenders.Length == 0,
                "Legacy contract speed escaped its DTO/catalog compatibility boundary: " +
                string.Join(", ", contractSpeedOffenders));
            var presenter = File.ReadAllText(Path.Combine(root, "Presentation.Unity", "MainNavigation", "MainNavigationHudPresenter.cs"));
            var exactPotentialTokens = new[]
            {
                "selected.Potential}",
                "selected.Potential:",
                ".Capability.Potential}",
                ".Capability.Potential:",
                ".Potential.ToString("
            };
            Require(exactPotentialTokens.All(token => !presenter.Contains(token)),
                "Employee UI leaks exact potential.");
            Require(typeof(WorkforceRosterMemberViewModel).GetProperty("Potential") == null,
                "Employee roster view model exposes exact potential instead of its letter grade.");
        }

        private static WorkforceCapabilityState NewCapability(string id, int allSkills, int potential) =>
            new WorkforceCapabilityState(id, new WorkSkillSet(allSkills, allSkills, allSkills, allSkills, allSkills, allSkills),
                potential, WorkforceStressRules.NeutralStressGainBasisPoints);

        private static void AssertGrade(int value, WorkforcePotentialGrade expected) =>
            Require(WorkforcePotentialGradeRules.Resolve(value) == expected, $"Potential {value} grade mismatch.");

        private static void AssertCapability(WorkforceCapabilityState state, int e, int p, int c, int b, int o, int co,
            string grade, int resistance)
        {
            Require(state.Skills.Engineering == e && state.Skills.Planning == p && state.Skills.Creative == c &&
                    state.Skills.Business == b && state.Skills.Operations == o && state.Skills.Collaboration == co,
                state.MemberId + " capability mismatch.");
            Require(WorkforcePotentialGradeRules.DisplayLetter(state.PotentialGrade) == grade,
                state.MemberId + " potential grade mismatch.");
            Require(WorkforceStressRules.ResistancePercent(state.StressGainBasisPoints) == resistance,
                state.MemberId + " stress resistance mismatch.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
