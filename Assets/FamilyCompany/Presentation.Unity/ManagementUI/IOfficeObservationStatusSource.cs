namespace FamilyCompany.Presentation.Unity.ManagementUI
{
    public enum OfficeObservationStatusKind
    {
        Idle,
        Moving,
        Seated,
        Typing,
        Mouse,
        Drinking,
        Meeting,
        Printing,
        Break,
        Outside
    }

    public interface IOfficeObservationStatusSource
    {
        string MemberId { get; }
        OfficeObservationStatusKind StatusKind { get; }
        string StatusDetail { get; }
    }
}
