using System;

namespace FamilyCompany.Presentation.Unity.OfficeSeating
{
    /// <summary>
    /// Pure presentation state for gating player contract progress while seating changes.
    /// It never writes simulation energy, stress, time, or contract state.
    /// </summary>
    public sealed class OfficePlayerWorkGate
    {
        public bool HasActiveWork { get; private set; }
        public OfficeActivity CurrentActivity { get; private set; } = OfficeActivity.Walking;
        public bool IsSeatedWorkGateRequired { get; private set; }
        public bool IsSeatedWorkReady { get; private set; }
        public bool IsTransitionBlocked { get; private set; }
        public bool WantsOfficeSeat => HasActiveWork && CurrentActivity == OfficeActivity.Work;
        public bool CanAccumulateProgress =>
            HasActiveWork && !IsTransitionBlocked &&
            (!WantsOfficeSeat || !IsSeatedWorkGateRequired || IsSeatedWorkReady);

        public void Begin(OfficeActivity activity)
        {
            if (activity != OfficeActivity.Work &&
                activity != OfficeActivity.Meeting &&
                activity != OfficeActivity.Printing)
            {
                throw new ArgumentOutOfRangeException(nameof(activity), activity, "Unsupported player work activity.");
            }

            var leavingReadySeat = WantsOfficeSeat && IsSeatedWorkReady && activity != OfficeActivity.Work;
            HasActiveWork = true;
            CurrentActivity = activity;
            IsSeatedWorkReady = false;
            if (leavingReadySeat) IsTransitionBlocked = true;
        }

        public void End()
        {
            var leavingReadySeat = WantsOfficeSeat && IsSeatedWorkReady;
            HasActiveWork = false;
            CurrentActivity = OfficeActivity.Walking;
            IsSeatedWorkReady = false;
            if (leavingReadySeat) IsTransitionBlocked = true;
        }

        public void SetSeatedWorkGateRequired(bool required)
        {
            IsSeatedWorkGateRequired = required;
            if (!required) IsSeatedWorkReady = false;
        }

        public void SetSeatedWorkReady(bool ready)
        {
            IsSeatedWorkReady = ready && IsSeatedWorkGateRequired && WantsOfficeSeat;
        }

        public void SetTransitionBlocked(bool blocked)
        {
            IsTransitionBlocked = blocked;
        }
    }
}
