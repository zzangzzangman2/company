using System;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeSeating
{
    /// <summary>
    /// Injection boundary for a future OfficeWorkMicroActionStateMachine and
    /// OfficeWorkActionFrameSet. A rejected or absent hook uses the canonical
    /// six-frame seated Work loop configured on DirectionalSpriteAnimator.
    /// </summary>
    public interface IOfficeSeatedWorkAnimationHook
    {
        bool TryBegin(int lockedDirection, out IOfficeSeatedWorkAnimationSession session);
    }

    public interface IOfficeSeatedWorkAnimationSession : IDisposable
    {
        Sprite CurrentSprite { get; }
        bool IsSafeToStand { get; }
        void Tick(float deltaTime);
        void RequestSafeStop();
    }
}
