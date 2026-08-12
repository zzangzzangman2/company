using System;
using System.Collections.Generic;
using System.Reflection;
using FamilyCompany.Presentation.Unity;
using FamilyCompany.Presentation.Unity.OfficeSeating;
using FamilyCompany.Presentation.Unity.OfficeSeating.Authoring;
using FamilyCompany.Presentation.Unity.OfficeWorkActions;
using FamilyCompany.Simulation.OfficeWorkActions;
using UnityEngine;
#if !OFFICE_WORK_ADAPTER_STANDALONE
using UnityEditor;
#endif

namespace FamilyCompany.Editor
{
    public static class OfficeSeatedWorkMicroActionAdapterValidation
    {
        private const int ValidationSeed = 2_000_081;
        private const long ValidationMinute = 4_321L;

#if !OFFICE_WORK_ADAPTER_STANDALONE
        [MenuItem("Family Company/Validate Office Seated Work Micro-Action Adapter")]
        public static void Run()
        {
            try
            {
                RunAllOrThrow();
                Debug.Log("FAMILY_COMPANY_OFFICE_WORK_ADAPTER_VALIDATION: PASS");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Debug.LogError("FAMILY_COMPANY_OFFICE_WORK_ADAPTER_VALIDATION: FAIL");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }
#endif

        public static int Main()
        {
            RunAllOrThrow();
            Console.WriteLine("FAMILY_COMPANY_OFFICE_WORK_ADAPTER_STANDALONE: PASS");
            return 0;
        }

        public static void RunAllOrThrow()
        {
            ValidateWriterBoundary();
            ValidateRejectedSessions();
            ValidatePartialArtAndEightDirections();
            ValidateSafeStopAndIdempotentDisposal();
            ValidateDisableDestroyIdempotence();
            ValidatePushPresenterWaitingLifecycle();
            ValidatePushPresenterFrameLossRollback();
#if OFFICE_WORK_ADAPTER_STANDALONE
            ValidatePushPresenterStartupExceptionRollback();
#endif
#if !OFFICE_WORK_ADAPTER_STANDALONE
            ValidateDirectionalAnimatorOwnershipAndFallback();
#endif
        }

        private static void ValidateWriterBoundary()
        {
            var adapterType = typeof(OfficeSeatedWorkMicroActionAdapter);
            AssertNoWriterFields(adapterType);
            foreach (var nested in adapterType.GetNestedTypes(BindingFlags.NonPublic))
                AssertNoWriterFields(nested);
            if (!typeof(IOfficeSeatedWorkAnimationHook).IsAssignableFrom(adapterType))
                throw new InvalidOperationException("Adapter does not implement the seating pull hook.");
            if (typeof(IOfficeWorkSeatingPresentationHook).IsAssignableFrom(adapterType))
                throw new InvalidOperationException("Pull adapter must not implement the push presentation hook.");
        }

        private static void ValidateRejectedSessions()
        {
            using (var fixture = new ValidationFixture())
            {
                var adapter = fixture.CreateAdapter();
                adapter.Configure(fixture.Bootstrap, "player", null);
                AssertFalse(adapter.TryBegin(0, out _), "missing frame set");

                var empty = fixture.CreateFrameSet("player", Array.Empty<OfficeWorkActionClip>());
                adapter.Configure(fixture.Bootstrap, "player", empty);
                AssertFalse(adapter.TryBegin(0, out _), "Availability.None");

                var mismatchFrames = fixture.CreateSprites(16, "mismatch");
                var mismatch = fixture.CreateTypingFrameSet("father", mismatchFrames);
                adapter.Configure(fixture.Bootstrap, "player", mismatch);
                AssertFalse(adapter.TryBegin(0, out _), "member mismatch");

                adapter.Configure(null, "player", mismatch);
                AssertFalse(adapter.TryBegin(0, out _), "missing bootstrap");

                adapter.Configure(fixture.Bootstrap, "player", mismatch);
                AssertFalse(adapter.TryBegin(-1, out _), "negative direction");
                AssertFalse(
                    adapter.TryBegin(OfficeWorkMicroActionAvailabilityRules.DirectionCount, out _),
                    "direction overflow");
            }
        }

        private static void ValidatePartialArtAndEightDirections()
        {
            using (var fixture = new ValidationFixture())
            {
                var frames = fixture.CreateSprites(16, "typing");
                var frameSet = fixture.CreateTypingFrameSet("player", frames);
                AssertEqual(
                    OfficeWorkMicroActionAvailability.Typing,
                    frameSet.Availability,
                    "partial art availability");

                var adapter = fixture.CreateAdapter();
                adapter.Configure(fixture.Bootstrap, "player", frameSet);
                for (var direction = 0;
                     direction < OfficeWorkMicroActionAvailabilityRules.DirectionCount;
                     direction++)
                {
                    AssertTrue(adapter.TryBegin(direction, out var session), $"direction {direction} begin");
                    AssertSame(frames[direction], session.CurrentSprite, $"direction {direction} first frame");
                    session.Tick(0.101f);
                    AssertSame(
                        frames[OfficeWorkMicroActionAvailabilityRules.DirectionCount + direction],
                        session.CurrentSprite,
                        $"direction {direction} final frame");
                    session.Dispose();
                }
            }
        }

        private static void ValidateSafeStopAndIdempotentDisposal()
        {
            using (var fixture = new ValidationFixture())
            {
                var frameSet = fixture.CreateTypingFrameSet(
                    "player",
                    fixture.CreateSprites(16, "safe-stop"));
                var adapter = fixture.CreateAdapter();
                adapter.Configure(fixture.Bootstrap, "player", frameSet);
                AssertTrue(adapter.TryBegin(4, out var session), "safe-stop session begin");
                AssertFalse(session.IsSafeToStand, "active action blocks stand");

                session.RequestSafeStop();
                session.RequestSafeStop();
                AssertFalse(session.IsSafeToStand, "safe-stop waits for current action");
                for (var tick = 0; tick < 80 && !session.IsSafeToStand; tick++)
                    session.Tick(0.25f);
                AssertTrue(session.IsSafeToStand, "current action completes before stand");
                AssertSame(null, session.CurrentSprite, "ready session yields fallback sprite");

                session.Dispose();
                session.Dispose();
                session.RequestSafeStop();
                session.Tick(1f);
                AssertTrue(session.IsSafeToStand, "disposed session remains safe");
                AssertFalse(adapter.HasActiveSession, "disposed session is not active");
            }
        }

        private static void ValidateDisableDestroyIdempotence()
        {
            using (var fixture = new ValidationFixture())
            {
                var frameSet = fixture.CreateTypingFrameSet(
                    "player",
                    fixture.CreateSprites(16, "lifecycle"));
                var adapter = fixture.CreateAdapter();
                adapter.Configure(fixture.Bootstrap, "player", frameSet);
                AssertTrue(adapter.TryBegin(2, out var session), "lifecycle session begin");

                InvokeLifecycle(adapter, "OnDisable");
                InvokeLifecycle(adapter, "OnDisable");
                AssertTrue(session.IsSafeToStand, "disable disposes active session");
                AssertSame(null, session.CurrentSprite, "disable clears pulled sprite");
                AssertFalse(adapter.HasActiveSession, "disable clears adapter session reference");

                InvokeLifecycle(adapter, "OnDestroy");
                InvokeLifecycle(adapter, "OnDestroy");
                session.Dispose();
                AssertTrue(session.IsSafeToStand, "destroy/dispose remain idempotent");
            }
        }

        private static void ValidatePushPresenterWaitingLifecycle()
        {
            using (var fixture = new ValidationFixture())
            {
                var presenter = fixture.CreatePushPresenter();
                var renderer = fixture.CreateSpriteRenderer();
                var writer = fixture.CreateWorkLoopWriter();
                var frameSet = fixture.CreateTypingFrameSet(
                    "player",
                    fixture.CreateSprites(16, "push-lifecycle"));
                presenter.Configure(renderer, writer, frameSet, OfficeSeatFacing8.North);

                var readyEvents = 0;
                var readyInsideEvent = false;
                presenter.StandHandoffReady += () =>
                {
                    readyEvents++;
                    readyInsideEvent = presenter.IsStandHandoffReady;
                };
                AssertTrue(
                    presenter.NotifySeatedWorkStarted(ValidationSeed, "player", ValidationMinute),
                    "push presenter begins");
                AssertFalse(writer.enabled, "push presenter temporarily suspends fallback writer");
                AssertEqual(
                    OfficeWorkStandHandoffStatus.WaitingForCurrentAction,
                    presenter.RequestStandHandoff(OfficeWorkExitReason.StandUp),
                    "push presenter waits for active action");

                InvokeLifecycle(presenter, "OnDisable");
                AssertEqual(1, readyEvents, "disable publishes one readiness event");
                AssertTrue(readyInsideEvent, "readiness is observable inside disable event");
                AssertTrue(presenter.IsStandHandoffReady, "disable leaves explicit ready state");
                AssertTrue(writer.enabled, "disable restores fallback writer");
                AssertFalse(presenter.OwnsSpriteWriter, "disable releases sprite ownership");

                InvokeLifecycle(presenter, "OnDisable");
                InvokeLifecycle(presenter, "OnDestroy");
                InvokeLifecycle(presenter, "OnDestroy");
                AssertEqual(1, readyEvents, "disable/destroy notification is idempotent");
                AssertTrue(presenter.IsStandHandoffReady, "destroy preserves published readiness");
            }
        }

        private static void ValidatePushPresenterFrameLossRollback()
        {
            using (var fixture = new ValidationFixture())
            {
                var presenter = fixture.CreatePushPresenter();
                var renderer = fixture.CreateSpriteRenderer();
                var writer = fixture.CreateWorkLoopWriter();
                var frameSet = fixture.CreateTypingFrameSet(
                    "player",
                    fixture.CreateSprites(16, "push-frame-loss"));
                presenter.Configure(renderer, writer, frameSet, OfficeSeatFacing8.North);
                AssertTrue(
                    presenter.NotifySeatedWorkStarted(ValidationSeed, "player", ValidationMinute),
                    "frame-loss session begins");
                AssertFalse(writer.enabled, "frame-loss session owns writer before mutation");

                frameSet.Configure("player", Array.Empty<OfficeWorkActionClip>());
                presenter.TickMilliseconds(1L);
                AssertTrue(presenter.IsUsingExistingWorkLoop, "frame loss falls back to Work6 writer");
                AssertTrue(writer.enabled, "frame loss restores prior enabled writer state");
                AssertFalse(presenter.OwnsSpriteWriter, "frame loss clears micro writer ownership");

                var readyEvents = 0;
                presenter.StandHandoffReady += () => readyEvents++;
                AssertEqual(
                    OfficeWorkStandHandoffStatus.ReadyToStand,
                    presenter.RequestStandHandoff(OfficeWorkExitReason.Moving),
                    "fallback session hands off immediately");
                AssertEqual(1, readyEvents, "fallback handoff event count");
            }

            using (var fixture = new ValidationFixture())
            {
                var presenter = fixture.CreatePushPresenter();
                var renderer = fixture.CreateSpriteRenderer();
                var writer = fixture.CreateWorkLoopWriter();
                writer.enabled = false;
                var frameSet = fixture.CreateTypingFrameSet(
                    "player",
                    fixture.CreateSprites(16, "push-disabled-writer"));
                presenter.Configure(renderer, writer, frameSet, OfficeSeatFacing8.North);
                AssertTrue(
                    presenter.NotifySeatedWorkStarted(ValidationSeed, "player", ValidationMinute),
                    "disabled-writer session begins");
                frameSet.Configure("player", Array.Empty<OfficeWorkActionClip>());
                presenter.TickMilliseconds(1L);
                AssertFalse(writer.enabled, "rollback preserves previously disabled writer state");
            }
        }

#if OFFICE_WORK_ADAPTER_STANDALONE
        private static void ValidatePushPresenterStartupExceptionRollback()
        {
            using (var fixture = new ValidationFixture())
            {
                var presenter = fixture.CreatePushPresenter();
                var renderer = new ThrowingSpriteRenderer();
                var writer = fixture.CreateWorkLoopWriter();
                var frameSet = fixture.CreateTypingFrameSet(
                    "player",
                    fixture.CreateSprites(16, "push-throw"));
                presenter.Configure(renderer, writer, frameSet, OfficeSeatFacing8.North);
                renderer.ThrowOnSet = true;

                AssertFalse(
                    presenter.NotifySeatedWorkStarted(ValidationSeed, "player", ValidationMinute),
                    "startup frame exception selects fallback");
                AssertTrue(writer.enabled, "startup exception restores fallback writer");
                AssertTrue(presenter.IsUsingExistingWorkLoop, "startup exception reports Work6 fallback");
                AssertFalse(presenter.OwnsSpriteWriter, "startup exception releases ownership");
            }
        }
#endif

#if !OFFICE_WORK_ADAPTER_STANDALONE
        private static void ValidateDirectionalAnimatorOwnershipAndFallback()
        {
            using (var fixture = new ValidationFixture())
            {
                var root = fixture.CreateGameObject("Adapter Animator Validation");
                var renderer = root.AddComponent<SpriteRenderer>();
                var animator = root.AddComponent<DirectionalSpriteAnimator>();
                var walkFrames = fixture.CreateSprites(DirectionalSpriteAnimator.RequiredFrameCount, "walk");
                var sitFrames = fixture.CreateSprites(OfficeSeatingAnimationFrames.SitDownSpriteCount, "sit");
                var workFrames = fixture.CreateSprites(OfficeSeatingAnimationFrames.WorkSpriteCount, "work");
                var standFrames = fixture.CreateSprites(OfficeSeatingAnimationFrames.StandUpSpriteCount, "stand");
                animator.Configure(renderer, walkFrames);
                animator.ConfigureOfficeSeating(sitFrames, workFrames, standFrames);
                AssertApproximately(0.62f, animator.SitDownDurationSeconds, 0.0001f, "sit-down duration");
                AssertApproximately(0.56f, animator.StandUpDurationSeconds, 0.0001f, "stand-up duration");

                var sitFrameMask = 0;
                var standFrameMask = 0;
                animator.OfficeFrameApplied += (clip, frame, _) =>
                {
                    if (clip == OfficeSeatingAnimationClip.SitDown) sitFrameMask |= 1 << frame;
                    if (clip == OfficeSeatingAnimationClip.StandUp) standFrameMask |= 1 << frame;
                };

                const int direction = 4;
                AssertTrue(
                    animator.PrepareOfficeSeatingFacing(direction, OfficeSeatForegroundOcclusionMode.Default),
                    "fallback prepare");
                AssertTrue(animator.BeginSitDown(direction), "fallback sit-down");
                CompleteTransition(animator, 0.62f, "fallback sit-down");
                AssertEqual(0b1111, sitFrameMask, "fallback exposes all four sit poses");
                AssertTrue(animator.BeginSeatedWork(), "fallback work begin");
                animator.Tick(0.15f);
                AssertSame(
                    workFrames[OfficeSeatingAnimationFrames.DirectionCount + direction],
                    animator.CurrentSprite,
                    "no-art six-frame fallback advances");

                animator.ResumeWalkingAfterSeating();
                var actionFrames = fixture.CreateSprites(16, "adapter-work");
                var frameSet = fixture.CreateTypingFrameSet("player", actionFrames);
                var adapter = root.AddComponent<OfficeSeatedWorkMicroActionAdapter>();
                adapter.Configure(fixture.Bootstrap, "player", frameSet);
                animator.ConfigureOfficeWorkAnimationHook(adapter);
                AssertTrue(
                    animator.PrepareOfficeSeatingFacing(direction, OfficeSeatForegroundOcclusionMode.Default),
                    "adapter prepare");
                sitFrameMask = 0;
                AssertTrue(animator.BeginSitDown(direction), "adapter sit-down");
                CompleteTransition(animator, 0.62f, "adapter sit-down");
                AssertEqual(0b1111, sitFrameMask, "adapter exposes all four sit poses");
                AssertTrue(animator.BeginSeatedWork(), "adapter work begin");
                AssertTrue(animator.enabled, "DirectionalSpriteAnimator remains enabled during work");
                animator.Tick(0.01f);
                AssertSame(actionFrames[direction], animator.CurrentSprite, "animator pulls adapter frame");

                animator.RequestOfficeWorkSafeStop();
                for (var tick = 0; tick < 80 && !animator.IsOfficeWorkSafeToStand; tick++)
                    animator.Tick(0.25f);
                AssertTrue(animator.IsOfficeWorkSafeToStand, "animator observes safe-stop handoff");
                AssertTrue(animator.BeginStandUp(), "stand-up begins after handoff");
                AssertTrue(animator.enabled, "DirectionalSpriteAnimator remains enabled for stand-up");
                AssertFalse(animator.IsOfficeWorkHookActive, "stand-up disposes hook session once");
                CompleteTransition(animator, 0.56f, "stand-up");
                AssertEqual(0b1111, standFrameMask, "stand-up exposes all four poses");
                animator.ResumeWalkingAfterSeating();
                AssertTrue(animator.enabled, "DirectionalSpriteAnimator remains enabled after seating");
            }
        }
#endif

        private static void AssertNoWriterFields(Type type)
        {
            foreach (var field in type.GetFields(
                         BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                         BindingFlags.DeclaredOnly))
            {
                var fieldType = field.FieldType;
                if (typeof(Renderer).IsAssignableFrom(fieldType) ||
                    fieldType.Name == nameof(DirectionalSpriteAnimator) ||
                    field.Name.IndexOf("writer", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    throw new InvalidOperationException(
                        $"Pull adapter owns a forbidden sprite writer field: {type.Name}.{field.Name}.");
                }
            }
        }

        private static void InvokeLifecycle(object target, string methodName)
        {
            var method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null) throw new MissingMethodException(target.GetType().Name, methodName);
            method.Invoke(target, null);
        }

        private static void AssertTrue(bool value, string label)
        {
            if (!value) throw new InvalidOperationException(label + " expected true.");
        }

        private static void AssertFalse(bool value, string label)
        {
            if (value) throw new InvalidOperationException(label + " expected false.");
        }

        private static void AssertSame(object expected, object actual, string label)
        {
            if (!ReferenceEquals(expected, actual))
                throw new InvalidOperationException(label + " reference mismatch.");
        }

        private static void AssertEqual<T>(T expected, T actual, string label)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException($"{label}: expected {expected}, actual {actual}.");
        }

        private static void AssertApproximately(float expected, float actual, float tolerance, string label)
        {
            if (Mathf.Abs(expected - actual) > tolerance)
                throw new InvalidOperationException($"{label}: expected {expected}, actual {actual}.");
        }

        private static void CompleteTransition(
            DirectionalSpriteAnimator animator,
            float expectedDuration,
            string label)
        {
            const float tickSeconds = 0.01f;
            var elapsed = 0f;
            var previousProgress = animator.CurrentOfficeSeatingProgress01;
            for (var tick = 0; tick < 200 && !animator.IsOfficeSeatingTransitionComplete; tick++)
            {
                animator.Tick(tickSeconds);
                elapsed += tickSeconds;
                float progress = animator.CurrentOfficeSeatingProgress01;
                AssertTrue(progress + 0.000001f >= previousProgress, label + " progress is monotonic");
                previousProgress = progress;
            }
            AssertTrue(animator.IsOfficeSeatingTransitionComplete, label + " completes");
            AssertApproximately(expectedDuration, elapsed, tickSeconds + 0.0001f, label + " elapsed time");
        }

        private sealed class ValidationFixture : IDisposable
        {
#if !OFFICE_WORK_ADAPTER_STANDALONE
            private readonly List<UnityEngine.Object> _owned = new List<UnityEngine.Object>();
            private readonly Texture2D _texture;
#endif
            private int _spriteSequence;

            public ValidationFixture()
            {
#if OFFICE_WORK_ADAPTER_STANDALONE
                Bootstrap = new PrototypeBootstrap
                {
                    State = new AdapterValidationGameState(
                        ValidationSeed,
                        new AdapterValidationGameTime(ValidationMinute))
                };
#else
                var bootstrapObject = CreateGameObject("Adapter Validation Bootstrap");
                Bootstrap = bootstrapObject.AddComponent<PrototypeBootstrap>();
                Bootstrap.InitializeNow();
                _texture = new Texture2D(2, 2);
                _owned.Add(_texture);
#endif
            }

            public PrototypeBootstrap Bootstrap { get; }

            public OfficeSeatedWorkMicroActionAdapter CreateAdapter()
            {
#if OFFICE_WORK_ADAPTER_STANDALONE
                return new OfficeSeatedWorkMicroActionAdapter();
#else
                return CreateGameObject("Office Work Adapter").AddComponent<OfficeSeatedWorkMicroActionAdapter>();
#endif
            }

            public OfficeWorkMicroActionPresenter CreatePushPresenter()
            {
#if OFFICE_WORK_ADAPTER_STANDALONE
                return new OfficeWorkMicroActionPresenter();
#else
                return CreateGameObject("Office Work Push Presenter").AddComponent<OfficeWorkMicroActionPresenter>();
#endif
            }

            public SpriteRenderer CreateSpriteRenderer()
            {
#if OFFICE_WORK_ADAPTER_STANDALONE
                return new SpriteRenderer();
#else
                return CreateGameObject("Office Work Sprite Renderer").AddComponent<SpriteRenderer>();
#endif
            }

            public Behaviour CreateWorkLoopWriter()
            {
#if OFFICE_WORK_ADAPTER_STANDALONE
                return new TestWorkLoopWriter();
#else
                return CreateGameObject("Office Work Fallback Writer").AddComponent<BillboardFacingCamera>();
#endif
            }

#if !OFFICE_WORK_ADAPTER_STANDALONE
            public GameObject CreateGameObject(string name)
            {
                var gameObject = new GameObject(name);
                _owned.Add(gameObject);
                return gameObject;
            }
#endif

            public Sprite[] CreateSprites(int count, string prefix)
            {
                var result = new Sprite[count];
                for (var index = 0; index < count; index++)
                {
#if OFFICE_WORK_ADAPTER_STANDALONE
                    result[index] = new Sprite($"{prefix}:{_spriteSequence++}");
#else
                    result[index] = Sprite.Create(
                        _texture,
                        new Rect(0f, 0f, _texture.width, _texture.height),
                        new Vector2(0.5f, 0.5f));
                    result[index].name = $"{prefix}:{_spriteSequence++}";
                    _owned.Add(result[index]);
#endif
                }
                return result;
            }

            public OfficeWorkActionFrameSet CreateTypingFrameSet(string memberId, Sprite[] frames)
            {
                var clip = new OfficeWorkActionClip();
                clip.Configure(OfficeWorkMicroAction.Typing, frames, 100, false);
                return CreateFrameSet(memberId, new[] { clip });
            }

            public OfficeWorkActionFrameSet CreateFrameSet(
                string memberId,
                OfficeWorkActionClip[] clips)
            {
#if OFFICE_WORK_ADAPTER_STANDALONE
                var result = new OfficeWorkActionFrameSet();
#else
                var result = ScriptableObject.CreateInstance<OfficeWorkActionFrameSet>();
                _owned.Add(result);
#endif
                result.Configure(memberId, clips);
                return result;
            }

            public void Dispose()
            {
#if !OFFICE_WORK_ADAPTER_STANDALONE
                for (var index = _owned.Count - 1; index >= 0; index--)
                {
                    if (_owned[index] != null) UnityEngine.Object.DestroyImmediate(_owned[index]);
                }
                _owned.Clear();
#endif
            }
        }

#if OFFICE_WORK_ADAPTER_STANDALONE
        private sealed class TestWorkLoopWriter : MonoBehaviour { }

        private sealed class ThrowingSpriteRenderer : SpriteRenderer
        {
            private Sprite _sprite;

            public bool ThrowOnSet { get; set; }

            public override Sprite sprite
            {
                get => _sprite;
                set
                {
                    if (ThrowOnSet) throw new InvalidOperationException("Injected sprite apply failure.");
                    _sprite = value;
                }
            }
        }
#endif
    }
}

#if OFFICE_WORK_ADAPTER_STANDALONE
namespace UnityEngine
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class DisallowMultipleComponent : Attribute { }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SerializeField : Attribute { }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class TooltipAttribute : Attribute
    {
        public TooltipAttribute(string value) { }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class MinAttribute : Attribute
    {
        public MinAttribute(float value) { }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class CreateAssetMenuAttribute : Attribute
    {
        public string fileName;
        public string menuName;
    }

    public class Object { }

    public class Behaviour : Object
    {
        public bool enabled = true;
        public bool isActiveAndEnabled = true;
    }

    public class MonoBehaviour : Behaviour { }
    public class ScriptableObject : Object { }
    public class Renderer : Behaviour { }
    public class SpriteRenderer : Renderer
    {
        public virtual Sprite sprite { get; set; }
    }

    public static class Time
    {
        public static float deltaTime { get; set; }
    }

    public sealed class Sprite : Object
    {
        public Sprite(string id)
        {
            Id = id;
        }

        public string Id { get; }
    }
}

namespace FamilyCompany.Presentation.Unity
{
    public sealed class PrototypeBootstrap : UnityEngine.MonoBehaviour
    {
        public AdapterValidationGameState State { get; set; }
    }

    public sealed class AdapterValidationGameState
    {
        public AdapterValidationGameState(int worldSeed, AdapterValidationGameTime time)
        {
            WorldSeed = worldSeed;
            Time = time;
        }

        public int WorldSeed { get; }
        public AdapterValidationGameTime Time { get; }
    }

    public sealed class AdapterValidationGameTime
    {
        public AdapterValidationGameTime(long elapsedMinutes)
        {
            ElapsedMinutes = elapsedMinutes;
        }

        public long ElapsedMinutes { get; }
    }

    public sealed class DirectionalSpriteAnimator { }
}
#endif
