using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using FamilyCompany.Presentation.Unity;
using FamilyCompany.Presentation.Unity.OfficeGridView;
using FamilyCompany.Presentation.Unity.OfficeRuntime;
using UnityEngine;

namespace FamilyCompany.Runtime.Character3D
{
    // Explicit diagnostic session, not a PASS oracle. Production inputs remain native UI inputs.
    // Setup uses the public new-game entry without saving. No actor control, route or pose injection.
    [DefaultExecutionOrder(20001)]
    public sealed class Family3DManualGameplayObserver : MonoBehaviour
    {
        public const string Flag = "-familyCompanyManualGameplayObservation";
        private string directory;
        private PrototypeBootstrap bootstrap;
        private StarterOfficeRuntimeBootstrap runtime;
        private StreamWriter trace;
        private StreamWriter geometry;
        private float nextGeometry;
        private int errors, frame, capture;
        private float nextSample, nextCapture;
        private bool nextDayRequested;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!Environment.GetCommandLineArgs().Contains(Flag)) return;
            var host = new GameObject("~ManualGameplayObserver");
            DontDestroyOnLoad(host);
            host.AddComponent<Family3DManualGameplayObserver>();
        }

        private IEnumerator Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, Flag);
            if (index < 0 || index + 1 >= args.Length || !Path.IsPathRooted(args[index + 1]))
                throw new ArgumentException("Observer needs an absolute evidence directory.");
            directory = Path.GetFullPath(args[index + 1]);
            Directory.CreateDirectory(directory);
            trace = new StreamWriter(Path.Combine(directory, "observations.csv"), false, new UTF8Encoding(false));
            trace.AutoFlush = true;
            geometry = new StreamWriter(Path.Combine(directory, "chair-geometry.csv"), false, new UTF8Encoding(false));
            geometry.AutoFlush = true;
            geometry.WriteLine("seconds,seat,turn,chairTileErrorPx,stemTileErrorPx,monitorAxisError,keyboardAxisError,member,phase,handMidpointError,standingHeight");
            trace.WriteLine("frame,seconds,clock,ready,cash,inventory,seats,pointerCommits,member,phase,away,x,y,destination,pathIndex,pathLength,seat,seatDirection,arrivalCount,workFrames,gaitDistance,displacement,staticViolations,interactionViolations,agentPenetrations,bodies,legacyCharacters,legacyFurniture,bgm,sfx,listenerVolume,outputPeak,errors");
            Application.logMessageReceived += OnLog;
            Application.runInBackground = true;
            bootstrap = FindFirstObjectByType<PrototypeBootstrap>();
            bootstrap.StartNewGameNow(1, false);
            ScenePreviewJump.ShowStarterOffice();
            Debug.Log("MANUAL_GAMEPLAY_OBSERVATION setup=public-unsaved-new-game actorControl=false routeInjection=false patchNetworkBypassed=true; F8=capture F9=next-day-setup F10=finish");
            yield return null;
        }

        private void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
        }

        private void LateUpdate()
        {
            if (trace == null || bootstrap == null || bootstrap.State == null) return;
            frame++;
            runtime = FindFirstObjectByType<StarterOfficeRuntimeBootstrap>();
            if (runtime == null || runtime.World == null) return;
            if (Input.GetKeyDown(KeyCode.F8)) Capture();
            if (Input.GetKeyDown(KeyCode.F9) && !nextDayRequested)
            {
                nextDayRequested = true;
                StartCoroutine(PrepareNextDay());
            }
            if (Input.GetKeyDown(KeyCode.F10))
            {
                File.WriteAllText(Path.Combine(directory, "observation-ended.txt"),
                    "CAPTURED, NOT AUTO-PASS; runtimeErrors=" + errors + "; frames=" + frame);
                Application.Quit(errors == 0 ? 0 : 1);
            }
            if (Time.realtimeSinceStartup < nextSample) return;
            nextSample = Time.realtimeSinceStartup + 0.1f;
            var state = bootstrap.State;
            var editor = FindFirstObjectByType<OfficeLayoutEditModeController>();
            var presenter = Family3DProductionPresenter.Instance;
            var audio = GameAudioCoordinator.Instance;
            var samples = new float[256];
            AudioListener.GetOutputData(samples, 0);
            float peak = samples.Max(value => Mathf.Abs(value));
            var rows = new StringBuilder();
            foreach (var actor in runtime.Actors)
            {
                rows.AppendLine(string.Join(",", frame, F(Time.realtimeSinceStartup), state.Time.Now.ToString("s"),
                    runtime.IsReady, state.Company.CashWon, state.OfficeFurnitureInventory.Instances.Count,
                    state.OfficeGrid.SeatSlots.Count, editor == null ? -1 : editor.DiagnosticPointerCommitCount,
                    actor.AgentId, actor.Phase, actor.IsPresentationAway, F(actor.Position.x), F(actor.Position.y),
                    actor.ActiveDestinationCell.ToString().Replace(',', ':'), actor.PresentationPathIndex,
                    actor.SemanticPathLength, actor.ActiveSeatId, actor.ExpectedSeatDirection,
                    actor.AttendanceSeatArrivalCount, actor.ObservedWorkFrameCount, F(actor.GaitDistance),
                    F(actor.LastActualDisplacement.magnitude), runtime.World.Occupancy.StaticViolationCount,
                    runtime.World.Occupancy.InteractionViolationCount, runtime.World.Occupancy.AgentPenetrationCount,
                    presenter == null ? -1 : presenter.BoundCharacterCount,
                    presenter == null ? -1 : presenter.VisibleLegacyCharacterRendererCount,
                    presenter == null ? -1 : presenter.VisibleLegacyWorkstationRendererCount,
                    F(audio.BgmVolume), F(audio.SfxVolume), F(AudioListener.volume), F(peak), errors));
            }
            trace.Write(rows.ToString());
            File.WriteAllText(Path.Combine(directory, "latest.csv"), rows.ToString());
            if (presenter != null && presenter.IsBound && Time.realtimeSinceStartup >= nextGeometry)
            {
                nextGeometry = Time.realtimeSinceStartup + 1f;
                ObserveChairGeometry(presenter);
            }
            if (Time.realtimeSinceStartup >= nextCapture && runtime.IsReady)
            {
                nextCapture = Time.realtimeSinceStartup + 10;
                Capture();
            }
        }

        private IEnumerator PrepareNextDay()
        {
            // Only skip the unobserved afternoon/night. Observe both normal departure and the full
            // next-day 08:50 -> 09:04 scheduler window; never inject an individual due time or route.
            DateTime evening = bootstrap.State.Time.Now.Date.AddHours(17).AddMinutes(50);
            if (bootstrap.State.Time.Now < evening)
                bootstrap.AdvanceTimeNow((long)(evening - bootstrap.State.Time.Now).TotalMinutes);
            bootstrap.SetWorldTimeScaleNow(4f);
            Debug.Log("MANUAL_GAMEPLAY_SETUP observing normal departure before next day");
            float deadline = Time.realtimeSinceStartup + 120;
            while (runtime.Actors.Any(actor => !actor.IsPresentationAway) && Time.realtimeSinceStartup < deadline)
                yield return null;
            if (runtime.Actors.Any(actor => !actor.IsPresentationAway))
            {
                Debug.LogError("MANUAL_GAMEPLAY_SETUP departure timeout; next-day observation not started");
                yield break;
            }
            DateTime morning = bootstrap.State.Time.Now.Date.AddDays(1).AddHours(8).AddMinutes(50);
            bootstrap.AdvanceTimeNow((long)(morning - bootstrap.State.Time.Now).TotalMinutes);
            bootstrap.SetWorldTimeScaleNow(1f);
            Debug.Log("MANUAL_GAMEPLAY_NEXT_DAY_OBSERVATION_START normal-clock=true normal-scheduler=true");
        }

        private void Capture()
        {
            ScreenCapture.CaptureScreenshot(Path.Combine(directory, "screen-" + (capture++).ToString("D4") + ".png"));
        }
        private void ObserveChairGeometry(Family3DProductionPresenter presenter)
        {
            Camera overlay = Camera.allCameras.FirstOrDefault(c => c.name == "Family3DProductionOverlayCamera");
            if (overlay == null || Camera.main == null) return;
            var bindings = (IEnumerable)typeof(Family3DProductionPresenter)
                .GetField("characters", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(presenter);
            foreach (Family3DWorkstation desk in presenter.GetComponentsInChildren<Family3DWorkstation>())
            {
                var seat = runtime.World.Grid.SeatSlots.FirstOrDefault(s => s.SeatId == desk.WorkstationSetId);
                if (seat == null) continue;
                Vector2 tile = Camera.main.WorldToScreenPoint(runtime.World.Presenter.CellCenterWorld(seat.Cell));
                Vector3 stem = desk.transform.Find("Chair_SwivelPivot/Chair_Stem").position; stem.y = 0;
                float chairError = Vector2.Distance(tile, overlay.WorldToScreenPoint(desk.ChairGroundWorld));
                float stemError = Vector2.Distance(tile, overlay.WorldToScreenPoint(stem));
                Vector3 key = desk.KeyboardWorld - desk.ChairGroundWorld; key.y = 0;
                Vector3 screen = desk.MonitorWorld - desk.ChairGroundWorld; screen.y = 0;
                bool occupied = false;
                foreach (object binding in bindings)
                {
                    Type type = binding.GetType();
                    var agent = (OfficeRuntimeAgent)type.GetProperty("Agent").GetValue(binding);
                    if (agent.ActiveSeatId != seat.SeatId) continue;
                    var body = (Family3DWalkActor)type.GetProperty("WalkActor").GetValue(binding);
                    var host = (GameObject)type.GetProperty("Host").GetValue(binding);
                    Animator avatar = host.GetComponentInChildren<Animator>();
                    Vector3 hands = (avatar.GetBoneTransform(HumanBodyBones.LeftHand).position +
                                     avatar.GetBoneTransform(HumanBodyBones.RightHand).position) * 0.5f;
                    Vector3 expected = desk.KeyboardWorld + Vector3.up * (0.022f * body.StandingHeight) -
                                       desk.SeatedBodyForwardWorld * (0.035f * body.StandingHeight);
                    geometry.WriteLine(string.Join(",", F(Time.realtimeSinceStartup), seat.SeatId,
                        seat.Facing, F(chairError), F(stemError), F(Vector3.Cross(screen, desk.ForwardWorld).magnitude),
                        F(Vector3.Cross(key, desk.ForwardWorld).magnitude), agent.AgentId, agent.Phase,
                        F(Vector3.Distance(hands, expected)), F(body.StandingHeight)));
                    occupied = true;
                }
                if (!occupied) geometry.WriteLine(string.Join(",", F(Time.realtimeSinceStartup), seat.SeatId,
                    seat.Facing, F(chairError), F(stemError), F(Vector3.Cross(screen, desk.ForwardWorld).magnitude),
                    F(Vector3.Cross(key, desk.ForwardWorld).magnitude), "none", "none", "", ""));
            }
        }
        private static string F(float value) => value.ToString("F6", CultureInfo.InvariantCulture);
        private void OnDestroy() { Application.logMessageReceived -= OnLog; trace?.Dispose(); geometry?.Dispose(); }
    }
}
