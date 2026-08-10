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

                const int direction = 4;
                AssertTrue(
                    animator.PrepareOfficeSeatingFacing(direction, OfficeSeatForegroundOcclusionMode.Default),
                    "fallback prepare");
                AssertTrue(animator.BeginSitDown(direction), "fallback sit-down");
                animator.Tick(1f);
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
                AssertTrue(animator.BeginSitDown(direction), "adapter sit-down");
                animator.Tick(1f);
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
                animator.Tick(1f);
                AssertTrue(animator.IsOfficeSeatingTransitionComplete, "stand-up clip completes");
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

        private static void InvokeLifecycle(OfficeSeatedWorkMicroActionAdapter adapter, string methodName)
        {
            var method = typeof(OfficeSeatedWorkMicroActionAdapter).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null) throw new MissingMethodException(typeof(OfficeSeatedWorkMicroActionAdapter).Name, methodName);
            method.Invoke(adapter, null);
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
    public sealed class SpriteRenderer : Renderer { }

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
