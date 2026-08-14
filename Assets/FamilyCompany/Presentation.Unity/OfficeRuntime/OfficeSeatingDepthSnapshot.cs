using FamilyCompany.Presentation.Unity.OfficeSeating;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime
{
    /// <summary>Post-sort evidence for one rendered seating frame.</summary>
    public readonly struct OfficeSeatingDepthSnapshot
    {
        public OfficeSeatingDepthSnapshot(
            OfficeRuntimeAgentPhase phase,
            OfficeSeatingAnimationClip? clip,
            int frame,
            bool occlusionEngaged,
            int actorOrder,
            int chairBaseOrder,
            bool hasChairFront,
            int chairFrontOrder,
            bool hasDesk,
            int deskBaseOrder,
            bool hasDeskFront,
            int deskFrontOrder)
        {
            IsValid = true;
            Phase = phase;
            Clip = clip;
            Frame = frame;
            OcclusionEngaged = occlusionEngaged;
            ActorOrder = actorOrder;
            ChairBaseOrder = chairBaseOrder;
            HasChairFront = hasChairFront;
            ChairFrontOrder = chairFrontOrder;
            HasDesk = hasDesk;
            DeskBaseOrder = deskBaseOrder;
            HasDeskFront = hasDeskFront;
            DeskFrontOrder = deskFrontOrder;
        }

        public bool IsValid { get; }
        public OfficeRuntimeAgentPhase Phase { get; }
        public OfficeSeatingAnimationClip? Clip { get; }
        public int Frame { get; }
        public bool OcclusionEngaged { get; }
        public int ActorOrder { get; }
        public int ChairBaseOrder { get; }
        public bool HasChairFront { get; }
        public int ChairFrontOrder { get; }
        public bool HasDesk { get; }
        public int DeskBaseOrder { get; }
        public bool HasDeskFront { get; }
        public int DeskFrontOrder { get; }

        public bool IsValidStack
        {
            get
            {
                if (!IsValid || ChairBaseOrder >= ActorOrder) return false;
                if (HasDesk && (DeskBaseOrder >= ChairBaseOrder || DeskBaseOrder >= ActorOrder))
                    return false;
                if (OcclusionEngaged)
                {
                    if (!HasChairFront || ChairFrontOrder <= ActorOrder) return false;
                    if (HasDesk &&
                        (!HasDeskFront ||
                         DeskFrontOrder <= ActorOrder ||
                         ChairFrontOrder <= DeskFrontOrder)) return false;
                }
                else
                {
                    if (HasChairFront && ChairFrontOrder >= ActorOrder) return false;
                    if (HasDeskFront && DeskFrontOrder >= ActorOrder) return false;
                }
                return true;
            }
        }
    }
}
