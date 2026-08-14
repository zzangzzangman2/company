using FamilyCompany.Presentation.Unity.OfficeSeating;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime
{
    /// <summary>
    /// Shared, member-independent rule for binding furniture redraw masks to an occupied seat.
    /// The foreground remains bound for the complete reserved dismount. It is released atomically
    /// with the seat claim only after the actor reaches a collision-cleared safe anchor.
    /// </summary>
    public static class OfficeSeatOcclusionRules
    {
        public static OfficeSeatOcclusionState Evaluate(
            OfficeRuntimeAgentPhase phase,
            Vector2 actorWorld,
            Vector2 operatorWorld,
            Vector2 approachWorld,
            OfficeSeatingAnimationClip? clip = null,
            int frame = -1)
        {
            float exitProgress = ResolveExitProgress01(actorWorld, operatorWorld, approachWorld);
            bool engaged = phase == OfficeRuntimeAgentPhase.SittingDown ||
                           phase == OfficeRuntimeAgentPhase.Working ||
                           phase == OfficeRuntimeAgentPhase.FinishingWork ||
                           phase == OfficeRuntimeAgentPhase.StandingUp ||
                           phase == OfficeRuntimeAgentPhase.LeavingSeat;
            // SitDown[0] is the planted standing pose at the approach side of the chair. Keeping
            // the foreground released for this one common frame prevents the chair back from
            // redrawing across a torso that has not crossed into the seat yet.
            if (phase == OfficeRuntimeAgentPhase.SittingDown &&
                clip == OfficeSeatingAnimationClip.SitDown &&
                frame == 0)
                engaged = false;
            return new OfficeSeatOcclusionState(engaged, exitProgress);
        }

        public static float ResolveExitProgress01(
            Vector2 actorWorld,
            Vector2 operatorWorld,
            Vector2 approachWorld)
        {
            Vector2 exit = approachWorld - operatorWorld;
            float lengthSquared = exit.sqrMagnitude;
            if (lengthSquared <= 0.000001f) return 1f;
            return Mathf.Clamp01(Vector2.Dot(actorWorld - operatorWorld, exit) / lengthSquared);
        }
    }

    public readonly struct OfficeSeatOcclusionState
    {
        public OfficeSeatOcclusionState(bool foregroundEngaged, float exitProgress01)
        {
            ForegroundEngaged = foregroundEngaged;
            ExitProgress01 = Mathf.Clamp01(exitProgress01);
        }

        public bool ForegroundEngaged { get; }
        public float ExitProgress01 { get; }
    }
}
