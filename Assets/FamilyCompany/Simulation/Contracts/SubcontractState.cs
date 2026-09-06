using System;
using System.Collections.Generic;
using System.Linq;

namespace FamilyCompany.Simulation.Contracts
{
    public sealed class ContractWorkerContribution
    {
        public ContractWorkerContribution(string memberId, int personHours)
        {
            if (string.IsNullOrWhiteSpace(memberId)) throw new ArgumentException("Member ID is required.", nameof(memberId));
            if (personHours <= 0) throw new ArgumentOutOfRangeException(nameof(personHours));
            MemberId = memberId;
            PersonHours = personHours;
        }

        public string MemberId { get; }
        public int PersonHours { get; private set; }

        internal void AddHours(int personHours)
        {
            if (personHours <= 0) throw new ArgumentOutOfRangeException(nameof(personHours));
            PersonHours = checked(PersonHours + personHours);
        }
    }

    public sealed class SubcontractState
    {
        private readonly List<ContractWorkerContribution> _contributions;

        public SubcontractState(SubcontractOffer offer, long acceptedMinute)
            : this(offer, acceptedMinute, SubcontractStatus.Active, 0, -1, null)
        {
        }

        public SubcontractState(
            SubcontractOffer offer,
            long acceptedMinute,
            SubcontractStatus status,
            int completedPersonHours,
            long resolvedMinute,
            IEnumerable<ContractWorkerContribution> contributions,
            int workRateBasisPoints = 10000,
            int qualityBonus = 0,
            int resolvedQuality = -1,
            long dueMinute = -1)
        {
            Offer = offer ?? throw new ArgumentNullException(nameof(offer));
            if (!Enum.IsDefined(typeof(SubcontractStatus), status)) throw new ArgumentOutOfRangeException(nameof(status));
            if (acceptedMinute < 0) throw new ArgumentOutOfRangeException(nameof(acceptedMinute));
            if (completedPersonHours < 0 || completedPersonHours > offer.EstimatedPersonHours)
            {
                throw new ArgumentOutOfRangeException(nameof(completedPersonHours));
            }

            if (status == SubcontractStatus.Active && resolvedMinute != -1)
            {
                throw new InvalidOperationException("An active contract cannot have a resolved minute.");
            }

            if (status == SubcontractStatus.Active && completedPersonHours == offer.EstimatedPersonHours)
            {
                throw new InvalidOperationException("A fully worked contract must be completed.");
            }

            if (status != SubcontractStatus.Active && resolvedMinute < acceptedMinute)
            {
                throw new InvalidOperationException("A resolved contract requires a valid resolved minute.");
            }

            if (status == SubcontractStatus.Completed && completedPersonHours != offer.EstimatedPersonHours)
            {
                throw new InvalidOperationException("A completed contract must contain all required work.");
            }

            AcceptedMinute = acceptedMinute;
            DueMinute = checked(acceptedMinute + offer.DeadlineDays * 1440L);
            if (dueMinute != -1)
            {
                if (dueMinute <= acceptedMinute || dueMinute > DueMinute || (offer.IsExternal && dueMinute != DueMinute))
                    throw new ArgumentOutOfRangeException(nameof(dueMinute));
                DueMinute = dueMinute;
            }
            Status = status;
            CompletedPersonHours = completedPersonHours;
            ResolvedMinute = resolvedMinute;
            if (workRateBasisPoints < 10000 || workRateBasisPoints > 12000) throw new ArgumentOutOfRangeException(nameof(workRateBasisPoints));
            if (qualityBonus < 0 || qualityBonus > 12) throw new ArgumentOutOfRangeException(nameof(qualityBonus));
            if (resolvedQuality < -1 || resolvedQuality > 100) throw new ArgumentOutOfRangeException(nameof(resolvedQuality));
            WorkRateBasisPoints = workRateBasisPoints;
            QualityBonus = qualityBonus;
            ResolvedQuality = resolvedQuality;
            _contributions = contributions == null
                ? new List<ContractWorkerContribution>()
                : contributions.Select(item => new ContractWorkerContribution(item.MemberId, item.PersonHours)).ToList();

            if (_contributions.Select(item => item.MemberId).Distinct(StringComparer.Ordinal).Count() != _contributions.Count)
            {
                throw new InvalidOperationException("Contract worker contribution IDs must be unique.");
            }

            if (_contributions.Sum(item => item.PersonHours) != CompletedPersonHours)
            {
                throw new InvalidOperationException("Contract worker contributions must equal completed person-hours.");
            }
        }

        public SubcontractOffer Offer { get; }
        public long AcceptedMinute { get; }
        public long DueMinute { get; }
        public SubcontractStatus Status { get; private set; }
        public int CompletedPersonHours { get; private set; }
        public int RemainingPersonHours => Offer.EstimatedPersonHours - CompletedPersonHours;
        public long ResolvedMinute { get; private set; }
        public IReadOnlyList<ContractWorkerContribution> Contributions => _contributions;
        public int WorkRateBasisPoints { get; }
        public int QualityBonus { get; }
        public int ResolvedQuality { get; private set; }

        internal int AddWork(string memberId, int requestedPersonHours)
        {
            if (Status != SubcontractStatus.Active) throw new InvalidOperationException("Only active contracts can receive work.");
            if (string.IsNullOrWhiteSpace(memberId)) throw new ArgumentException("Member ID is required.", nameof(memberId));
            if (requestedPersonHours <= 0) throw new ArgumentOutOfRangeException(nameof(requestedPersonHours));

            var applied = Math.Min(requestedPersonHours, RemainingPersonHours);
            var contribution = _contributions.FirstOrDefault(item => item.MemberId == memberId);
            if (contribution == null)
            {
                _contributions.Add(new ContractWorkerContribution(memberId, applied));
            }
            else
            {
                contribution.AddHours(applied);
            }

            CompletedPersonHours = checked(CompletedPersonHours + applied);
            return applied;
        }

        internal void MarkCompleted(long elapsedMinute, int quality = -1)
        {
            if (Status != SubcontractStatus.Active) throw new InvalidOperationException("Contract is not active.");
            if (RemainingPersonHours != 0) throw new InvalidOperationException("Contract work is incomplete.");
            if (elapsedMinute < AcceptedMinute || elapsedMinute > DueMinute)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedMinute));
            }

            Status = SubcontractStatus.Completed;
            ResolvedMinute = elapsedMinute;
            ResolvedQuality = quality;
        }

        internal void MarkFailed(long elapsedMinute)
        {
            if (Status != SubcontractStatus.Active) throw new InvalidOperationException("Contract is not active.");
            if (elapsedMinute <= DueMinute) throw new ArgumentOutOfRangeException(nameof(elapsedMinute));
            Status = SubcontractStatus.Failed;
            ResolvedMinute = elapsedMinute;
        }
    }
}
