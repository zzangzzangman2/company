using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime.Qa
{
    /// <summary>
    /// Opt-in Release-player evidence recorder. It never creates or loads a session: the title,
    /// slot selection and all player input still travel through the shipping UI. Once a session
    /// exists, the recorder samples the final SpriteRenderer after each rendered frame so actual
    /// displacement, resolved facing and the consumed sprite cannot come from different frames.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OfficeMovementGameplayPlayerQa : MonoBehaviour
    {
        public const string CommandLineFlag = "-familyCompanyMovementGameplayQa";
        public const string ArtifactDirectoryArgument =
            "-familyCompanyMovementGameplayQaArtifacts";
        public const string QuitAfterArgument =
            "-familyCompanyMovementGameplayQaQuitAfter";

        private const float DefaultQuitAfterSeconds = 195f;
        private const int FourTimesStartMinuteOfDay = 9 * 60 + 20;
        private const float MovingDistanceEpsilon = 0.000001f;
        private const float TeleportDistance = 0.35f;
        private const float ActorOverlapTolerance = 0.01f;
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        private static OfficeMovementGameplayPlayerQa _instance;

        private readonly Dictionary<string, DirectionHistory> _directionHistory =
            new Dictionary<string, DirectionHistory>(StringComparer.Ordinal);
        private readonly Dictionary<string, OfficeRuntimeAgentPhase> _lastPhase =
            new Dictionary<string, OfficeRuntimeAgentPhase>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _violationCounts =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private StreamWriter _frameWriter;
        private StreamWriter _eventWriter;
        private PrototypeBootstrap _bootstrap;
        private StarterOfficeRuntimeBootstrap _runtime;
        private string _artifactDirectory = string.Empty;
        private float _sessionStartedAt = -1f;
        private float _quitAfterSeconds = DefaultQuitAfterSeconds;
        private bool _speedRaised;
        private bool _finished;
        private int _sampledFrames;
        private int _sampledActorFrames;
        private int _lateralFrames;
        private int _collisionProjectedFrames;
        private int _staticPenetrationFrames;
        private int _actorOverlapFrames;
        private int _minimumMinuteOfDay = int.MaxValue;
        private int _maximumMinuteOfDay = int.MinValue;

        private sealed class DirectionHistory
        {
            public int Previous = -1;
            public int BeforePrevious = -1;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInstall()
        {
            if (_instance != null || !HasArgument(CommandLineFlag)) return;
            var host = new GameObject("~OfficeMovementGameplayPlayerQa");
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<OfficeMovementGameplayPlayerQa>();
        }

        private void Start()
        {
            Application.runInBackground = true;
            // Keep the evidence stream aligned with a normal 60 Hz presentation instead of
            // letting an uncapped Release player emit hundreds of duplicate rendered samples.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            _artifactDirectory = ResolveArgument(
                ArtifactDirectoryArgument,
                Path.Combine(Application.persistentDataPath, "MovementGameplayQa"));
            _quitAfterSeconds = ResolvePositiveFloat(
                QuitAfterArgument,
                DefaultQuitAfterSeconds);
            Directory.CreateDirectory(_artifactDirectory);
            _frameWriter = CreateWriter("movement-frame-trace.csv");
            _eventWriter = CreateWriter("movement-events.csv");
            _frameWriter.WriteLine(
                "wall_seconds,frame,screen,game_date,game_time,elapsed_minute,world_scale," +
                "member,agent_phase,x,y,dx,dy,actual_speed,desired_vx,desired_vy," +
                "motion_direction,display_direction,sprite_direction,locomotion_phase," +
                "gait_phase,walk_frame,clip,sprite,flip_x,is_moving,collision_projected," +
                "static_clear,blocker,stuck_seconds,path_index,path_length,seat_id," +
                "seating_clip,seating_frame");
            _eventWriter.WriteLine("wall_seconds,frame,game_time,event,detail");
            WriteEvent("RECORDER_STARTED", $"quit_after={F(_quitAfterSeconds)}");
            Application.quitting += HandleApplicationQuitting;
            StartCoroutine(SampleRenderedFrames());
        }

        private void Update()
        {
            if (_finished) return;
            if (_bootstrap == null)
                _bootstrap = Object.FindFirstObjectByType<PrototypeBootstrap>();
            if (_runtime == null || !_runtime.IsReady)
                _runtime = Object.FindFirstObjectByType<StarterOfficeRuntimeBootstrap>();
            if (_bootstrap == null || !_bootstrap.HasSession || _bootstrap.State == null) return;

            if (_sessionStartedAt < 0f)
            {
                _sessionStartedAt = Time.realtimeSinceStartup;
                WriteEvent("SESSION_STARTED", $"slot={_bootstrap.ActiveSlot}");
            }

            DateTime now = _bootstrap.State.Time.Now;
            int minuteOfDay = now.Hour * 60 + now.Minute;
            if (!_speedRaised && minuteOfDay >= FourTimesStartMinuteOfDay &&
                _bootstrap.UiScreen == PrototypeUiScreen.Playing)
            {
                _bootstrap.SetWorldTimeScaleNow(4f);
                _speedRaised = true;
                WriteEvent("WORLD_SPEED_CHANGED", "scale=4 reason=capture-18:00-exit");
            }

            if (_sessionStartedAt >= 0f &&
                Time.realtimeSinceStartup - _sessionStartedAt >= _quitAfterSeconds)
            {
                FinishAndQuit();
            }
        }

        private IEnumerator SampleRenderedFrames()
        {
            var wait = new WaitForEndOfFrame();
            while (!_finished)
            {
                yield return wait;
                if (_bootstrap == null || !_bootstrap.HasSession || _bootstrap.State == null ||
                    _runtime == null || !_runtime.IsReady || _runtime.World == null) continue;
                SampleFrame();
            }
        }

        private void SampleFrame()
        {
            DateTime now = _bootstrap.State.Time.Now;
            int minuteOfDay = now.Hour * 60 + now.Minute;
            _minimumMinuteOfDay = Math.Min(_minimumMinuteOfDay, minuteOfDay);
            _maximumMinuteOfDay = Math.Max(_maximumMinuteOfDay, minuteOfDay);
            float wallSeconds = _sessionStartedAt < 0f
                ? 0f
                : Time.realtimeSinceStartup - _sessionStartedAt;
            OfficeRuntimeAgent[] actors = _runtime.Actors
                .Where(actor => actor != null)
                .OrderBy(actor => actor.AgentId, StringComparer.Ordinal)
                .ToArray();

            foreach (OfficeRuntimeAgent actor in actors)
            {
                DirectionalLocomotionFrameTrace trace = actor.CaptureLocomotionFrameTrace();
                bool staticClear = actor.IsPresentationAway ||
                                   actor.IsAttendanceIngressActive ||
                                   _runtime.World.Occupancy.CanTraverseStatic(
                                       actor.Position,
                                       actor.Position,
                                       actor.AgentRadius,
                                       actor.ActiveSeatId);
                WriteFrame(wallSeconds, now, actor, trace, staticClear);
                InspectActorFrame(wallSeconds, now, actor, trace, staticClear);
            }
            InspectActorPairs(wallSeconds, now, actors);
            _sampledFrames++;
            _sampledActorFrames += actors.Length;
            if ((_sampledFrames % 120) == 0)
            {
                _frameWriter.Flush();
                _eventWriter.Flush();
                WriteSummary(false);
            }
        }

        private void WriteFrame(
            float wallSeconds,
            DateTime now,
            OfficeRuntimeAgent actor,
            DirectionalLocomotionFrameTrace trace,
            bool staticClear)
        {
            string[] values =
            {
                F(wallSeconds),
                Time.frameCount.ToString(Invariant),
                _bootstrap.UiScreen.ToString(),
                now.ToString("yyyy-MM-dd", Invariant),
                now.ToString("HH:mm", Invariant),
                _bootstrap.State.Time.ElapsedMinutes.ToString(Invariant),
                F(_bootstrap.WorldTimeScale),
                actor.AgentId,
                actor.Phase.ToString(),
                F(actor.Position.x), F(actor.Position.y),
                F(trace.ActualDisplacement.x), F(trace.ActualDisplacement.y),
                F(trace.ActualSpeed),
                F(actor.DesiredVelocity.x), F(actor.DesiredVelocity.y),
                trace.MotionDirection.ToString(Invariant),
                trace.DisplayDirection.ToString(Invariant),
                actor.CurrentSpriteDirection.ToString(Invariant),
                trace.Phase.ToString(),
                F(actor.GaitPhase01),
                actor.CurrentWalkFrame.ToString(Invariant),
                trace.Clip ?? string.Empty,
                trace.SpriteName ?? string.Empty,
                trace.FlipX ? "true" : "false",
                trace.IsMoving ? "true" : "false",
                actor.WasCollisionProjected ? "true" : "false",
                staticClear ? "true" : "false",
                actor.LastMovementBlocker,
                F(actor.StuckSeconds),
                actor.PresentationPathIndex.ToString(Invariant),
                actor.SemanticPathLength.ToString(Invariant),
                actor.ActiveSeatId,
                actor.CurrentSeatingClip?.ToString() ?? string.Empty,
                actor.CurrentSeatingFrame.ToString(Invariant)
            };
            _frameWriter.WriteLine(string.Join(",", values.Select(Csv)));
        }

        private void InspectActorFrame(
            float wallSeconds,
            DateTime now,
            OfficeRuntimeAgent actor,
            DirectionalLocomotionFrameTrace trace,
            bool staticClear)
        {
            Vector2 displacement = trace.ActualDisplacement;
            bool translated = displacement.sqrMagnitude > MovingDistanceEpsilon;
            if (actor.WasCollisionProjected) _collisionProjectedFrames++;
            if (!staticClear)
            {
                _staticPenetrationFrames++;
                Violation("STATIC_PENETRATION", wallSeconds, now, actor.AgentId,
                    $"position=({F(actor.Position.x)} {F(actor.Position.y)}) seat={actor.ActiveSeatId}");
            }
            if (displacement.magnitude > TeleportDistance)
                Violation("TELEPORT", wallSeconds, now, actor.AgentId,
                    $"distance={F(displacement.magnitude)} phase={actor.Phase}");

            // The runtime grid is rendered through a 2:1 isometric transform: a semantic
            // southwest step can therefore have |screen dx| == 2 * |screen dy|. Classify the
            // lateral contract from the direction resolved from this frame's actual displacement,
            // rather than treating raw screen-axis dominance as west/east.
            int displacementDirection = translated
                ? DirectionalSpriteAnimator.ResolveTileDirection(displacement)
                : -1;
            bool lateral = displacementDirection == 2 || displacementDirection == 6;
            if (lateral)
            {
                _lateralFrames++;
                int expectedDirection = displacementDirection;
                string expectedToken = expectedDirection == 2 ? "west" : "east";
                bool locomotionClip = (trace.Clip ?? string.Empty).StartsWith(
                                          "Walk",
                                          StringComparison.Ordinal) ||
                                      (trace.Clip ?? string.Empty).StartsWith(
                                          "Transition/",
                                          StringComparison.Ordinal);
                bool valid = trace.MotionDirection == expectedDirection &&
                             trace.DisplayDirection == expectedDirection &&
                             actor.CurrentSpriteDirection == expectedDirection &&
                             (trace.SpriteName ?? string.Empty).IndexOf(
                                 expectedToken,
                                 StringComparison.OrdinalIgnoreCase) >= 0 &&
                             locomotionClip &&
                             trace.IsMoving &&
                             !trace.FlipX;
                if (!valid)
                    Violation("LATERAL_SPRITE", wallSeconds, now, actor.AgentId,
                        $"dx={F(displacement.x)} dy={F(displacement.y)} motion={trace.MotionDirection} " +
                        $"display={trace.DisplayDirection} spriteDir={actor.CurrentSpriteDirection} " +
                        $"clip={trace.Clip} sprite={trace.SpriteName} flipX={trace.FlipX}");
            }

            if (translated && actor.Phase == OfficeRuntimeAgentPhase.Navigating &&
                (!(trace.Clip ?? string.Empty).StartsWith("Walk", StringComparison.Ordinal) &&
                 !(trace.Clip ?? string.Empty).StartsWith("Transition/", StringComparison.Ordinal)))
                Violation("FOOT_SLIDE", wallSeconds, now, actor.AgentId,
                    $"phase={actor.Phase} clip={trace.Clip} speed={F(trace.ActualSpeed)}");

            if (translated && trace.IsMoving)
            {
                if (!_directionHistory.TryGetValue(actor.AgentId, out DirectionHistory history))
                {
                    history = new DirectionHistory();
                    _directionHistory.Add(actor.AgentId, history);
                }
                if (history.BeforePrevious >= 0 &&
                    history.BeforePrevious == trace.DisplayDirection &&
                    history.Previous != trace.DisplayDirection)
                    Violation("ONE_FRAME_DIRECTION_BOUNCE", wallSeconds, now, actor.AgentId,
                        $"directions={history.BeforePrevious}/{history.Previous}/{trace.DisplayDirection}");
                history.BeforePrevious = history.Previous;
                history.Previous = trace.DisplayDirection;
            }
            else if (_directionHistory.TryGetValue(actor.AgentId, out DirectionHistory stoppedHistory))
            {
                stoppedHistory.Previous = -1;
                stoppedHistory.BeforePrevious = -1;
            }

            float stuckThreshold = 6f * Mathf.Max(1f, _bootstrap.WorldTimeScale);
            if (actor.StuckSeconds >= stuckThreshold)
                Violation("STUCK_REPATH", wallSeconds, now, actor.AgentId,
                    $"stuck={F(actor.StuckSeconds)} threshold={F(stuckThreshold)} " +
                    $"blocker={actor.LastMovementBlocker}");

            if (!_lastPhase.TryGetValue(actor.AgentId, out OfficeRuntimeAgentPhase previousPhase) ||
                previousPhase != actor.Phase)
            {
                WriteEvent(
                    "PHASE_CHANGED",
                    $"member={actor.AgentId} from={previousPhase} to={actor.Phase}");
                _lastPhase[actor.AgentId] = actor.Phase;
            }
        }

        private void InspectActorPairs(float wallSeconds, DateTime now, OfficeRuntimeAgent[] actors)
        {
            for (var left = 0; left < actors.Length; left++)
            for (var right = left + 1; right < actors.Length; right++)
            {
                OfficeRuntimeAgent first = actors[left];
                OfficeRuntimeAgent second = actors[right];
                if (first.IsPresentationAway || second.IsPresentationAway) continue;
                float margin = Vector2.Distance(first.Position, second.Position) -
                               (first.AgentRadius + second.AgentRadius);
                if (margin >= -ActorOverlapTolerance) continue;
                _actorOverlapFrames++;
                Violation("ACTOR_OVERLAP", wallSeconds, now,
                    first.AgentId + "+" + second.AgentId,
                    $"margin={F(margin)} phases={first.Phase}/{second.Phase}");
            }
        }

        private void Violation(
            string category,
            float wallSeconds,
            DateTime now,
            string member,
            string detail)
        {
            _violationCounts.TryGetValue(category, out int count);
            _violationCounts[category] = count + 1;
            // Preserve every same-frame lateral failure. For repetitive spatial/stuck failures,
            // the CSV frame trace remains complete while the compact event stream is throttled.
            if (string.Equals(category, "LATERAL_SPRITE", StringComparison.Ordinal) ||
                count < 20 || (count % 120) == 0)
                WriteEvent(category, $"member={member} {detail}", wallSeconds, now);
        }

        private void WriteEvent(string eventName, string detail)
        {
            DateTime now = _bootstrap != null && _bootstrap.State != null
                ? _bootstrap.State.Time.Now
                : default;
            float wall = _sessionStartedAt < 0f
                ? Time.realtimeSinceStartup
                : Time.realtimeSinceStartup - _sessionStartedAt;
            WriteEvent(eventName, detail, wall, now);
        }

        private void WriteEvent(string eventName, string detail, float wallSeconds, DateTime now)
        {
            if (_eventWriter == null) return;
            string time = now == default ? string.Empty : now.ToString("HH:mm", Invariant);
            _eventWriter.WriteLine(string.Join(",", new[]
            {
                Csv(F(wallSeconds)),
                Csv(Time.frameCount.ToString(Invariant)),
                Csv(time),
                Csv(eventName),
                Csv(detail)
            }));
            _eventWriter.Flush();
        }

        private void FinishAndQuit()
        {
            if (_finished) return;
            WriteEvent("RECORDER_FINISHED", "automatic quit after requested gameplay duration");
            WriteSummary(true);
            _finished = true;
            CloseWriters();
            Application.Quit(_violationCounts.Count == 0 ? 0 : 97);
        }

        private void HandleApplicationQuitting()
        {
            if (!_finished) WriteSummary(true);
            CloseWriters();
        }

        private void OnDestroy()
        {
            Application.quitting -= HandleApplicationQuitting;
            if (!_finished) WriteSummary(true);
            CloseWriters();
        }

        private void WriteSummary(bool final)
        {
            if (string.IsNullOrEmpty(_artifactDirectory)) return;
            string timeRange = _minimumMinuteOfDay == int.MaxValue
                ? "none"
                : MinuteLabel(_minimumMinuteOfDay) + "-" + MinuteLabel(_maximumMinuteOfDay);
            var lines = new List<string>
            {
                "OFFICE_MOVEMENT_GAMEPLAY_QA",
                "status=" + (final ? "FINAL" : "RUNNING"),
                "sampled_frames=" + _sampledFrames.ToString(Invariant),
                "sampled_actor_frames=" + _sampledActorFrames.ToString(Invariant),
                "lateral_frames=" + _lateralFrames.ToString(Invariant),
                "collision_projected_frames=" + _collisionProjectedFrames.ToString(Invariant),
                "static_penetration_frames=" + _staticPenetrationFrames.ToString(Invariant),
                "actor_overlap_frames=" + _actorOverlapFrames.ToString(Invariant),
                "game_time_range=" + timeRange,
                "world_speed_raised=" + _speedRaised,
                "violation_categories=" + _violationCounts.Count.ToString(Invariant)
            };
            foreach (KeyValuePair<string, int> item in _violationCounts.OrderBy(item => item.Key))
                lines.Add("violation_" + item.Key.ToLowerInvariant() + "=" + item.Value.ToString(Invariant));
            File.WriteAllLines(Path.Combine(_artifactDirectory, "movement-summary.txt"), lines);
        }

        private StreamWriter CreateWriter(string name)
        {
            var stream = new FileStream(
                Path.Combine(_artifactDirectory, name),
                FileMode.Create,
                FileAccess.Write,
                FileShare.Read);
            return new StreamWriter(stream) { AutoFlush = false };
        }

        private void CloseWriters()
        {
            _frameWriter?.Flush();
            _eventWriter?.Flush();
            _frameWriter?.Dispose();
            _eventWriter?.Dispose();
            _frameWriter = null;
            _eventWriter = null;
        }

        private static string ResolveArgument(string name, string fallback)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index < arguments.Length - 1; index++)
                if (string.Equals(arguments[index], name, StringComparison.Ordinal))
                    return arguments[index + 1];
            return fallback;
        }

        private static float ResolvePositiveFloat(string name, float fallback)
        {
            string value = ResolveArgument(name, string.Empty);
            return float.TryParse(value, NumberStyles.Float, Invariant, out float parsed) && parsed > 0f
                ? parsed
                : fallback;
        }

        private static bool HasArgument(string name) =>
            Environment.GetCommandLineArgs().Any(
                argument => string.Equals(argument, name, StringComparison.Ordinal));

        private static string Csv(string value)
        {
            string safe = value ?? string.Empty;
            return safe.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0
                ? safe
                : "\"" + safe.Replace("\"", "\"\"") + "\"";
        }

        private static string F(float value) => value.ToString("0.000000", Invariant);

        private static string MinuteLabel(int minuteOfDay) =>
            (minuteOfDay / 60).ToString("00", Invariant) + ":" +
            (minuteOfDay % 60).ToString("00", Invariant);
    }
}
