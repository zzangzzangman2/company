using System;

namespace FamilyCompany.Presentation.Unity.OfficeWorkActions
{
    public enum OfficeWorkExitReason
    {
        StandUp = 0,
        Meeting = 1,
        Printing = 2,
        Moving = 3,
        OutsideSchedule = 4
    }

    public enum OfficeWorkStandHandoffStatus
    {
        WaitingForCurrentAction = 0,
        ReadyToStand = 1
    }

    /// <summary>
    /// Small integration contract for the seating owner. The owner calls SitDown first, then
    /// SeatedWork after occupancy. Before any departure it requests a handoff and must wait for
    /// ReadyToStand before starting stand-up or navigation. No gameplay writer is exposed here.
    /// </summary>
    public interface IOfficeWorkSeatingPresentationHook
    {
        event Action StandHandoffReady;

        bool IsUsingExistingWorkLoop { get; }
        bool IsStandHandoffReady { get; }
        bool OwnsSpriteWriter { get; }

        void NotifySitDownStarted();
        bool NotifySeatedWorkStarted(int worldSeed, string memberId, long sessionStartedMinute);
        OfficeWorkStandHandoffStatus RequestStandHandoff(OfficeWorkExitReason reason);
        void NotifyStandUpStarted();
    }
}
