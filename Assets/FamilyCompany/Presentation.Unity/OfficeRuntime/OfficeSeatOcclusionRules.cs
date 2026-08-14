using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime
{
    /// <summary>
    /// Shared, member-independent rule for binding furniture redraw masks to an occupied seat.
    /// The reservation remains alive until the approach cell is reached, but the foreground plane
    /// stops following the actor after the body has crossed away from the chair.
    /// </summary>
    public static class OfficeSeatOcclusionRules
    {
        public const float LeavingSeatReleaseProgress01 = 0.35f;

        public static OfficeSeatOcclusionState Evaluate(
            OfficeRuntimeAgentPhase phase,
            Vector2 actorWorld,
            Vector2 operatorWorld,
            Vector2 approachWorld)
        {
            float exitProgress = ResolveExitProgress01(actorWorld, operatorWorld, approachWorld);
            bool engaged = phase == OfficeRuntimeAgentPhase.SittingDown ||
                           phase == OfficeRuntimeAgentPhase.Working ||
                           phase == OfficeRuntimeAgentPhase.FinishingWork ||
                           phase == OfficeRuntimeAgentPhase.StandingUp ||
                           (phase == OfficeRuntimeAgentPhase.LeavingSeat &&
                            exitProgress < LeavingSeatReleaseProgress01);
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
