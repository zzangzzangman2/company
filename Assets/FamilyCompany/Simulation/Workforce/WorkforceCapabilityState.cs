using System;
using System.Collections.Generic;
using System.Linq;

namespace FamilyCompany.Simulation.Workforce
{
    public enum WorkSkillId
    {
        Engineering = 0,
        Planning = 1,
        Creative = 2,
        Business = 3,
        Operations = 4,
        Collaboration = 5
    }

    public enum WorkforcePotentialGrade
    {
        S = 0,
        A = 1,
        B = 2,
        C = 3,
        D = 4,
        F = 5
    }

    [Serializable]
    public sealed class WorkSkillProgressSnapshotDto
    {
        public int skillId;
        public long experience;
        public long fixedPointRemainder;
    }

    [Serializable]
    public sealed class WorkforceCapabilitySnapshotDto
    {
        public int schemaVersion = 1;
        public string memberId = string.Empty;
        public int engineering;
        public int planning;
        public int creative;
        public int business;
        public int operations;
        public int collaboration;
        public int potential;
        public int stressGainBasisPoints = WorkforceStressRules.NeutralStressGainBasisPoints;
        public List<WorkSkillProgressSnapshotDto> progress = new List<WorkSkillProgressSnapshotDto>();
    }

    public sealed class WorkSkillSet
    {
        public WorkSkillSet(
            int engineering,
            int planning,
            int creative,
            int business,
            int operations,
            int collaboration)
        {
            Engineering = Validate(engineering, nameof(engineering));
            Planning = Validate(planning, nameof(planning));
            Creative = Validate(creative, nameof(creative));
            Business = Validate(business, nameof(business));
            Operations = Validate(operations, nameof(operations));
            Collaboration = Validate(collaboration, nameof(collaboration));
        }

        public int Engineering { get; }
        public int Planning { get; }
        public int Creative { get; }
        public int Business { get; }
        public int Operations { get; }
        public int Collaboration { get; }

        public int Get(WorkSkillId skillId)
        {
            switch (skillId)
            {
                case WorkSkillId.Engineering: return Engineering;
                case WorkSkillId.Planning: return Planning;
                case WorkSkillId.Creative: return Creative;
                case WorkSkillId.Business: return Business;
                case WorkSkillId.Operations: return Operations;
                case WorkSkillId.Collaboration: return Collaboration;
                default: throw new ArgumentOutOfRangeException(nameof(skillId));
            }
        }

        public WorkSkillSet With(WorkSkillId skillId, int value)
        {
            return new WorkSkillSet(
                skillId == WorkSkillId.Engineering ? value : Engineering,
                skillId == WorkSkillId.Planning ? value : Planning,
                skillId == WorkSkillId.Creative ? value : Creative,
                skillId == WorkSkillId.Business ? value : Business,
                skillId == WorkSkillId.Operations ? value : Operations,
                skillId == WorkSkillId.Collaboration ? value : Collaboration);
        }

        private static int Validate(int value, string parameterName)
        {
            if (value < 0 || value > 100) throw new ArgumentOutOfRangeException(parameterName);
            return value;
        }
    }

    public sealed class WorkforceCapabilityState
    {
        private readonly Dictionary<WorkSkillId, WorkSkillProgressSnapshotDto> _progress;

        public WorkforceCapabilityState(
            string memberId,
            WorkSkillSet skills,
            int potential,
            int stressGainBasisPoints,
            IEnumerable<WorkSkillProgressSnapshotDto> progress = null)
        {
            if (string.IsNullOrWhiteSpace(memberId)) throw new ArgumentException("Member ID is required.", nameof(memberId));
            MemberId = memberId;
            Skills = skills ?? throw new ArgumentNullException(nameof(skills));
            if (potential < 0 || potential > 100) throw new ArgumentOutOfRangeException(nameof(potential));
            Potential = potential;
            StressGainBasisPoints = WorkforceStressRules.ClampStressGainBasisPoints(stressGainBasisPoints);
            _progress = new Dictionary<WorkSkillId, WorkSkillProgressSnapshotDto>();
            foreach (WorkSkillId skillId in Enum.GetValues(typeof(WorkSkillId)))
                _progress.Add(skillId, new WorkSkillProgressSnapshotDto { skillId = (int)skillId });
            if (progress == null) return;
            var seen = new HashSet<WorkSkillId>();
            foreach (var item in progress)
            {
                if (item == null || !Enum.IsDefined(typeof(WorkSkillId), item.skillId))
                    throw new InvalidOperationException("Capability progress contains an unknown skill.");
                var skillId = (WorkSkillId)item.skillId;
                if (!seen.Add(skillId))
                    throw new InvalidOperationException("Capability progress contains a duplicate skill.");
                if (item.experience < 0 || item.fixedPointRemainder < 0 ||
                    item.fixedPointRemainder >= WorkforceGrowthRules.FixedPointDenominator)
                    throw new InvalidOperationException("Capability progress is outside its persistent range.");
                _progress[skillId].experience = item.experience;
                _progress[skillId].fixedPointRemainder = item.fixedPointRemainder;
            }
        }

        public string MemberId { get; }
        public WorkSkillSet Skills { get; private set; }
        public int Potential { get; }
        public int StressGainBasisPoints { get; }
        public WorkforcePotentialGrade PotentialGrade => WorkforcePotentialGradeRules.Resolve(Potential);

        public long Experience(WorkSkillId skillId) => _progress[skillId].experience;
        public long FixedPointRemainder(WorkSkillId skillId) => _progress[skillId].fixedPointRemainder;
        public long ExperienceToNext(WorkSkillId skillId) =>
            WorkforceGrowthRules.ExperienceRequiredForNextLevel(Skills.Get(skillId));

        internal void ReplaceProgress(WorkSkillId skillId, int value, long experience, long remainder)
        {
            Skills = Skills.With(skillId, value);
            _progress[skillId].experience = experience;
            _progress[skillId].fixedPointRemainder = remainder;
        }

        public WorkforceCapabilitySnapshotDto ExportSnapshot()
        {
            return new WorkforceCapabilitySnapshotDto
            {
                memberId = MemberId,
                engineering = Skills.Engineering,
                planning = Skills.Planning,
                creative = Skills.Creative,
                business = Skills.Business,
                operations = Skills.Operations,
                collaboration = Skills.Collaboration,
                potential = Potential,
                stressGainBasisPoints = StressGainBasisPoints,
                progress = Enum.GetValues(typeof(WorkSkillId)).Cast<WorkSkillId>()
                    .OrderBy(item => (int)item)
                    .Select(item => new WorkSkillProgressSnapshotDto
                    {
                        skillId = (int)item,
                        experience = _progress[item].experience,
                        fixedPointRemainder = _progress[item].fixedPointRemainder
                    }).ToList()
            };
        }

        public static WorkforceCapabilityState ImportSnapshot(
            WorkforceCapabilitySnapshotDto snapshot,
            string expectedMemberId)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.schemaVersion != 1) throw new InvalidOperationException("Unsupported workforce capability snapshot.");
            if (!string.Equals(snapshot.memberId, expectedMemberId, StringComparison.Ordinal))
                throw new InvalidOperationException("Workforce capability member ID does not match its owner.");
            return new WorkforceCapabilityState(
                expectedMemberId,
                new WorkSkillSet(snapshot.engineering, snapshot.planning, snapshot.creative,
                    snapshot.business, snapshot.operations, snapshot.collaboration),
                snapshot.potential,
                snapshot.stressGainBasisPoints,
                snapshot.progress);
        }
    }
}
