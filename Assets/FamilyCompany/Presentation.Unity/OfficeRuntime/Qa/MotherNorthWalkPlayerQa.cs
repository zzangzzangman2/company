using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime.Qa
{
    /// <summary>
    /// Opt-in Release-player visual proof for the mother's north/back walk loop. It is isolated
    /// from normal gameplay and only runs when its command-line flag is present.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MotherNorthWalkPlayerQa : MonoBehaviour
    {
        public const string CommandLineFlag = "-familyCompanyMotherNorthWalkQa";
        public const string ArtifactDirectoryArgument = "-familyCompanyMotherNorthWalkQaArtifacts";

        private static MotherNorthWalkPlayerQa _instance;
        private string _artifactDirectory = string.Empty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => _instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInstall()
        {
            if (_instance != null || !HasArgument(CommandLineFlag)) return;
            var host = new GameObject("~MotherNorthWalkPlayerQa");
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<MotherNorthWalkPlayerQa>();
        }

        private void Start()
        {
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            _artifactDirectory = ResolveArgument(
                ArtifactDirectoryArgument,
                Path.Combine(Application.persistentDataPath, "MotherNorthWalkQa"));
            Directory.CreateDirectory(_artifactDirectory);
            StartCoroutine(RunGuarded());
        }

        private IEnumerator RunGuarded()
        {
            IEnumerator run = Run();
            while (true)
            {
                object yielded;
                try
                {
                    if (!run.MoveNext()) yield break;
                    yielded = run.Current;
                }
                catch (Exception exception)
                {
                    Finish(90, "unhandled=" + exception.GetType().Name + ":" + exception.Message, null);
                    yield break;
                }
                yield return yielded;
            }
        }

        private IEnumerator Run()
        {
            PrototypeBootstrap bootstrap = Object.FindFirstObjectByType<PrototypeBootstrap>();
            if (bootstrap == null)
            {
                Finish(91, "PrototypeBootstrap missing", null);
                yield break;
            }

            ScenePreviewJump.ShowStarterOffice();
            bootstrap.StartNewGameNow(1, false);
            ScenePreviewJump.ShowStarterOffice();
            bootstrap.SetWorldTimeScaleNow(1f);

            float readyDeadline = Time.realtimeSinceStartup + 30f;
            StarterOfficeRuntimeBootstrap runtime = null;
            while (Time.realtimeSinceStartup < readyDeadline)
            {
                runtime = Object.FindFirstObjectByType<StarterOfficeRuntimeBootstrap>();
                if (runtime != null && runtime.IsReady && runtime.World != null && runtime.Actors.Count == 4)
                    break;
                yield return null;
            }
            if (runtime == null || !runtime.IsReady || runtime.World == null)
            {
                Finish(92, "starter office runtime did not become ready", null);
                yield break;
            }

            OfficeRuntimeAgent mother = runtime.Actors.FirstOrDefault(actor =>
                actor != null && string.Equals(actor.AgentId, "mother", StringComparison.Ordinal));
            if (mother == null)
            {
                Finish(93, "mother runtime actor missing", null);
                yield break;
            }

            if (!TryFindNorthRun(
                    runtime,
                    mother,
                    out OfficeGridCoordinate start,
                    out OfficeGridCoordinate goal))
            {
                Finish(94, "no direct visual-north tile step exists", null);
                yield break;
            }

            mother.QaTeleportToCell(start);
            runtime.World.Occupancy.ResetMetrics();
            mother.QaSetDirectMovementInput(Vector2.up);

            var captured = new HashSet<int>();
            var sprites = new SortedDictionary<int, string>();
            float deadline = Time.realtimeSinceStartup + 15f;
            while (Time.realtimeSinceStartup < deadline && captured.Count < 6)
            {
                yield return new WaitForEndOfFrame();
                int frame = mother.CurrentWalkFrame;
                if (mother.CurrentDirection != 4 ||
                    mother.LastActualDisplacement.sqrMagnitude <= 0.0000000001f ||
                    frame < 0 || frame >= 6 ||
                    captured.Contains(frame)) continue;
                string expected = "mother_north_walk_" + frame.ToString(CultureInfo.InvariantCulture);
                string sprite = mother.CurrentSpriteName ?? string.Empty;
                if (!string.Equals(sprite, expected, StringComparison.Ordinal))
                {
                    // A planted start/pivot/stop transition can legitimately report the upcoming
                    // distance-frame index before the six-frame walk loop becomes visible.
                    if (sprite.StartsWith("mother_north_walk_", StringComparison.Ordinal) &&
                        (sprite.Contains("_start_") ||
                         sprite.Contains("_pivot_") ||
                         sprite.Contains("_stop_"))) continue;
                    Finish(
                        96,
                        "north frame sprite mismatch frame=" + frame + " expected=" + expected +
                        " actual=" + sprite,
                        mother);
                    yield break;
                }
                string capturePath = ArtifactPath(
                    "mother-north-runtime-frame-" + frame.ToString(CultureInfo.InvariantCulture) + ".png");
                if (!TryCaptureCloseup(mother, capturePath, out string failure))
                {
                    Finish(97, "capture failed frame=" + frame + " reason=" + failure, mother);
                    yield break;
                }
                captured.Add(frame);
                sprites.Add(frame, sprite);
                Debug.Log(
                    "MOTHER_NORTH_WALK_QA_FRAME | phase=" + frame + " sprite=" + sprite +
                    " direction=" + mother.CurrentDirection + " position=" + mother.Position.ToString("F3"));
            }

            if (captured.Count != 6)
            {
                Finish(
                    98,
                    "incomplete north cycle captured=" + string.Join("/", captured.OrderBy(value => value)) +
                    " currentDirection=" + mother.CurrentDirection + " currentFrame=" + mother.CurrentWalkFrame +
                    " currentSprite=" + mother.CurrentSpriteName,
                    mother);
                yield break;
            }

            mother.QaSetDirectMovementInput(Vector2.zero);
            mother.EndQaControl();
            var result = new StringBuilder();
            result.AppendLine("FAMILY_COMPANY_MOTHER_NORTH_WALK_QA: PASS");
            result.AppendLine("releasePlayer=true");
            result.AppendLine("direction=North(4)");
            result.AppendLine("clearance=" + start + "->" + goal);
            result.AppendLine("frames=0,1,2,3,4,5");
            foreach (KeyValuePair<int, string> pair in sprites)
                result.AppendLine("frame=" + pair.Key + " sprite=" + pair.Value);
            File.WriteAllText(ArtifactPath("mother-north-walk-result.txt"), result.ToString());
            Finish(0, "six imported north frames rendered in Release player", mother);
        }

        private static bool TryFindNorthRun(
            StarterOfficeRuntimeBootstrap runtime,
            OfficeRuntimeAgent mother,
            out OfficeGridCoordinate start,
            out OfficeGridCoordinate goal)
        {
            // Isometric cell X+ and Y+ cancel their horizontal projections and add their
            // vertical projections. A semantic Y-only run appears northeast on screen, so a
            // true visual-north proof must walk the grid diagonal (x+n, y+n).
            for (var x = 1; x < runtime.World.Grid.Width - 2; x++)
            {
                for (var y = 1; y < runtime.World.Grid.Height - 2; y++)
                {
                    var valid = true;
                    for (var offset = 0; offset < 3; offset++)
                    {
                        var candidate = new OfficeGridCoordinate(x + offset, y + offset);
                        if (!runtime.World.Grid.IsWalkable(candidate))
                        {
                            valid = false;
                            break;
                        }
                    }
                    if (!valid) continue;
                    var candidateStart = new OfficeGridCoordinate(x, y);
                    var candidateGoal = new OfficeGridCoordinate(x + 2, y + 2);
                    Vector3 startWorld3 = runtime.World.Presenter.CellCenterWorld(candidateStart);
                    Vector3 goalWorld3 = runtime.World.Presenter.CellCenterWorld(candidateGoal);
                    var startWorld = new Vector2(startWorld3.x, startWorld3.y);
                    var goalWorld = new Vector2(goalWorld3.x, goalWorld3.y);
                    if (!runtime.World.Occupancy.CanTraverseStatic(
                            startWorld,
                            goalWorld,
                            mother.AgentRadius,
                            string.Empty)) continue;
                    start = candidateStart;
                    goal = candidateGoal;
                    return true;
                }
            }
            start = default;
            goal = default;
            return false;
        }

        private bool TryCaptureCloseup(
            OfficeRuntimeAgent mother,
            string path,
            out string failure)
        {
            failure = string.Empty;
            Camera source = Camera.main;
            if (source == null)
            {
                failure = "Camera.main missing";
                return false;
            }

            RenderTexture previous = RenderTexture.active;
            var target = new RenderTexture(768, 768, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var pixels = new Texture2D(768, 768, TextureFormat.RGB24, false);
            GameObject captureHost = null;
            try
            {
                captureHost = new GameObject("MotherNorthWalkQaCapture") { hideFlags = HideFlags.HideAndDontSave };
                Camera camera = captureHost.AddComponent<Camera>();
                camera.CopyFrom(source);
                Vector3 sourcePosition = source.transform.position;
                Vector2 actorPosition = mother.Position;
                camera.transform.SetPositionAndRotation(
                    new Vector3(actorPosition.x, actorPosition.y + 0.65f, sourcePosition.z),
                    source.transform.rotation);
                camera.orthographic = true;
                camera.orthographicSize = 1.15f;
                camera.aspect = 1f;
                camera.enabled = false;
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0f, 0f, 768, 768), 0, 0, false);
                pixels.Apply(false, false);
                File.WriteAllBytes(path, pixels.EncodeToPNG());
                return File.Exists(path) && new FileInfo(path).Length > 1024L;
            }
            catch (Exception exception)
            {
                failure = exception.GetType().Name + ":" + exception.Message;
                return false;
            }
            finally
            {
                RenderTexture.active = previous;
                if (captureHost != null) Object.Destroy(captureHost);
                Object.Destroy(target);
                Object.Destroy(pixels);
            }
        }

        private void Finish(int exitCode, string reason, OfficeRuntimeAgent mother)
        {
            string line = (exitCode == 0 ? "PASS" : "FAIL") +
                          " | code=" + exitCode + " | reason=" + reason +
                          " | frame=" + (mother == null ? -1 : mother.CurrentWalkFrame) +
                          " | sprite=" + (mother == null ? "missing" : mother.CurrentSpriteName);
            File.WriteAllText(ArtifactPath("mother-north-walk-final.txt"), line + Environment.NewLine);
            if (exitCode == 0) Debug.Log("FAMILY_COMPANY_MOTHER_NORTH_WALK_QA: " + line);
            else Debug.LogError("FAMILY_COMPANY_MOTHER_NORTH_WALK_QA: " + line);
            Application.Quit(exitCode);
        }

        private string ArtifactPath(string name) => Path.Combine(_artifactDirectory, name);

        private static string ResolveArgument(string name, string fallback)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index < arguments.Length - 1; index++)
            {
                if (string.Equals(arguments[index], name, StringComparison.OrdinalIgnoreCase))
                    return arguments[index + 1];
            }
            return fallback;
        }

        private static bool HasArgument(string name) => Environment.GetCommandLineArgs().Any(
            argument => string.Equals(argument, name, StringComparison.OrdinalIgnoreCase));
    }
}
