using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Simulation.Family;
using FamilyCompany.Simulation.Game;
using FamilyCompany.Simulation.Stamina;
using FamilyCompany.Simulation.Workforce;

namespace FamilyCompany.Presentation.Unity.MainNavigation
{
    public sealed class WorkforceSkillViewModel
    {
        public WorkforceSkillViewModel(WorkSkillId skillId, string labelKo, int value, long experience, long nextExperience)
        {
            SkillId = skillId;
            LabelKo = labelKo;
            Value = value;
            Experience = experience;
            NextExperience = nextExperience;
        }
        public WorkSkillId SkillId { get; }
        public string LabelKo { get; }
        public int Value { get; }
        public long Experience { get; }
        public long NextExperience { get; }
    }

    public sealed class WorkforceRosterMemberViewModel
    {
        public WorkforceRosterMemberViewModel(
            string memberId,
            string displayName,
            string roleKo,
            string employmentTypeKo,
            int staminaBasisPoints,
            int stress,
            int trust,
            int stressResistancePercent,
            string potentialGrade,
            IEnumerable<WorkforceSkillViewModel> skills)
        {
            MemberId = memberId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            RoleKo = roleKo ?? string.Empty;
            EmploymentTypeKo = employmentTypeKo ?? string.Empty;
            StaminaBasisPoints = Math.Max(0, Math.Min(10_000, staminaBasisPoints));
            Stress = Math.Max(0, Math.Min(100, stress));
            Trust = Math.Max(0, Math.Min(100, trust));
            StressResistancePercent = Math.Max(0, Math.Min(100, stressResistancePercent));
            PotentialGrade = potentialGrade ?? string.Empty;
            Skills = (skills ?? throw new ArgumentNullException(nameof(skills))).ToArray();
        }
        public string MemberId { get; }
        public string DisplayName { get; }
        public string RoleKo { get; }
        public string EmploymentTypeKo { get; }
        public int StaminaBasisPoints { get; }
        public int Stress { get; }
        public int Trust { get; }
        public int StressResistancePercent { get; }
        public string PotentialGrade { get; }
        public IReadOnlyList<WorkforceSkillViewModel> Skills { get; }
    }

    public static class WorkforceRosterViewModelRules
    {
        public static IReadOnlyList<WorkforceRosterMemberViewModel> Create(GameState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            return state.Family.Members.Select(member => CreateMember(state, member)).ToArray();
        }

        private static WorkforceRosterMemberViewModel CreateMember(GameState state, FamilyMemberState member)
        {
            var staminaBasisPoints = state.Stamina.TryRead(member.MemberId, out CharacterStaminaReadSnapshot stamina)
                ? stamina.RatioBasisPoints
                : member.Energy * 100;
            var skills = Enum.GetValues(typeof(WorkSkillId)).Cast<WorkSkillId>().Select(skillId =>
                new WorkforceSkillViewModel(
                    skillId,
                    SkillLabel(skillId),
                    member.Capability.Skills.Get(skillId),
                    member.Capability.Experience(skillId),
                    member.Capability.ExperienceToNext(skillId)));
            return new WorkforceRosterMemberViewModel(
                member.MemberId,
                member.DisplayName,
                string.IsNullOrWhiteSpace(member.CompanyDuty) ? RoleLabel(member.Role) : member.CompanyDuty,
                "창업 가족",
                staminaBasisPoints,
                member.Stress,
                member.Trust,
                WorkforceStressRules.ResistancePercent(member.Capability.StressGainBasisPoints),
                WorkforcePotentialGradeRules.DisplayLetter(member.Capability.PotentialGrade),
                skills);
        }

        public static string SkillLabel(WorkSkillId skillId)
        {
            switch (skillId)
            {
                case WorkSkillId.Engineering: return "기술개발";
                case WorkSkillId.Planning: return "기획";
                case WorkSkillId.Creative: return "창작";
                case WorkSkillId.Business: return "사업";
                case WorkSkillId.Operations: return "운영";
                case WorkSkillId.Collaboration: return "협업";
                default: throw new ArgumentOutOfRangeException(nameof(skillId));
            }
        }

        private static string RoleLabel(FamilyRole role)
        {
            switch (role)
            {
                case FamilyRole.Player: return "기술·현장";
                case FamilyRole.OlderSister: return "기획·관리";
                case FamilyRole.Father: return "영업";
                case FamilyRole.Mother: return "운영";
                default: return "구성원";
            }
        }
    }
}
