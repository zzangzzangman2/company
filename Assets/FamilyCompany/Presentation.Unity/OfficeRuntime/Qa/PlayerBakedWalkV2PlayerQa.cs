using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using FamilyCompany.Simulation.Navigation;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime.Qa
{
    [DisallowMultipleComponent]
    public sealed class PlayerBakedWalkV2PlayerQa : MonoBehaviour
    {
        public const string CommandLineFlag = "-familyCompanyPlayerBakedWalkV2Qa";
        public const string ArtifactDirectoryArgument =
            "-familyCompanyPlayerBakedWalkV2QaArtifacts";
        private const float MaximumProjectedSupportDriftPx = 1.0f;
        private const float MaximumSupportDrift2dPx = 1.5f;
        private const float MaximumContactStepErrorPx = 1.0f;
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        private static readonly string[] DirectionNames =
        {
            "south", "southwest", "west", "northwest",
            "north", "northeast", "east", "southeast"
        };
        private static readonly Vector2[] DirectionVectors =
        {
            Vector2.down,
            new Vector2(-1f, -1f).normalized,
            Vector2.left,
            new Vector2(-1f, 1f).normalized,
            Vector2.up,
            new Vector2(1f, 1f).normalized,
            Vector2.right,
            new Vector2(1f, -1f).normalized
        };

        [Serializable]
        private sealed class StaticQaReceipt
        {
            public string contract;
            public int directions;
            public int poses;
            public int waistGapViolations;
            public int detachedAlphaViolations;
            public string validationProfile;
            public float directionMedianHeightDeltaPercent;
            public string catalogSourceReceiptSha256;
        }

        private static PlayerBakedWalkV2PlayerQa _instance;
        private string _artifactDirectory = string.Empty;
        private StreamWriter _trace;
        private int _wrongVisibleDirectionFrames;
        private float _maximumProjectedSupportDrift;
        private float _maximumSupportDrift2d;
        private float _maximumContactStepError;
        private readonly bool[] _directionScreenshotRequested = new bool[8];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => _instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInstall()
        {
            if (_instance != null || !HasArgument(CommandLineFlag)) return;
            var host = new GameObject("~PlayerBakedWalkV2PlayerQa");
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<PlayerBakedWalkV2PlayerQa>();
        }

        private void Start()
        {
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 120;
            _artifactDirectory = ResolveArgument(
                ArtifactDirectoryArgument,
                Path.Combine(Application.persistentDataPath, "PlayerBakedWalkV2PlayerQa"));
            Directory.CreateDirectory(_artifactDirectory);
            _trace = new StreamWriter(ArtifactPath("player-baked-walk-v2-trace.csv"), false,
                new UTF8Encoding(false));
            _trace.WriteLine(
                "direction,pose,frame,gait_phase,root_x,root_y,support_leg," +
                "support_world_x,support_world_y,boundary_world_x,boundary_world_y,sprite");
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
                    Finish(90, "unhandled=" + exception.GetType().Name + ":" + exception.Message);
                    yield break;
                }
                yield return yielded;
            }
        }

        private IEnumerator Run()
        {
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D11)
            {
                Finish(91, "graphics must be Direct3D11; actual=" + SystemInfo.graphicsDeviceType);
                yield break;
            }
            PlayerBakedWalkCatalogV2 catalog = PlayerBakedWalkCatalogV2.LoadDefault();
            if (catalog == null)
            {
                Finish(92, "PlayerBakedWalkCatalogV2 missing");
                yield break;
            }
            catalog.Validate();
            TextAsset staticQaAsset = Resources.Load<TextAsset>(
                "FamilyCompany/PlayerBakedWalkV2/PlayerBakedWalkStaticQaV2");
            StaticQaReceipt staticQa = staticQaAsset == null
                ? null
                : JsonUtility.FromJson<StaticQaReceipt>(staticQaAsset.text);
            if (staticQa == null ||
                !string.Equals(staticQa.contract, "FC-PLAYER-BAKED-WALK-V2-STATIC-QA", StringComparison.Ordinal) ||
                 staticQa.directions != 8 || staticQa.poses != 64 ||
                 staticQa.waistGapViolations != 0 || staticQa.detachedAlphaViolations != 0 ||
                 (string.Equals(staticQa.validationProfile, "humanoid-v1", StringComparison.Ordinal) &&
                  staticQa.directionMedianHeightDeltaPercent > 5.000001f) ||
                 !string.Equals(staticQa.catalogSourceReceiptSha256, catalog.SourceReceiptSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                Finish(93, "static QA receipt missing, stale, or failed");
                yield break;
            }

            PrototypeBootstrap bootstrap = Object.FindFirstObjectByType<PrototypeBootstrap>();
            if (bootstrap == null)
            {
                Finish(94, "PrototypeBootstrap missing");
                yield break;
            }
            ScenePreviewJump.ShowStarterOffice();
            bootstrap.StartNewGameNow(1, false);
            ScenePreviewJump.ShowStarterOffice();
            bootstrap.SetWorldTimeScaleNow(1f);

            StarterOfficeRuntimeBootstrap runtime = null;
            float readyDeadline = Time.realtimeSinceStartup + 30f;
            while (Time.realtimeSinceStartup < readyDeadline)
            {
                runtime = Object.FindFirstObjectByType<StarterOfficeRuntimeBootstrap>();
                if (runtime != null && runtime.IsReady && runtime.World != null && runtime.Actors.Count == 4)
                    break;
                yield return null;
            }
            if (runtime == null || !runtime.IsReady || runtime.World == null)
            {
                Finish(95, "starter office runtime did not become ready");
                yield break;
            }
            OfficeRuntimeAgent player = runtime.Actors.FirstOrDefault(actor =>
                actor != null && string.Equals(actor.AgentId, "player", StringComparison.Ordinal));
            if (player == null || player.PlayerWalkMode != PlayerWalkPresentationMode.BakedV2)
            {
                Finish(96, "player is not using the explicit BakedV2 presenter");
                yield break;
            }

            var center = new OfficeGridCoordinate(runtime.World.Grid.Width / 2,
                runtime.World.Grid.Height / 2);
            if (!runtime.World.Grid.Contains(center) || !runtime.World.Grid.IsWalkable(center))
            {
                Finish(97, "center QA cell is not walkable");
                yield break;
            }
            ParkOtherActors(runtime, player);
            player.BeginQaControl();
            for (var direction = 0; direction < DirectionVectors.Length; direction++)
            {
                player.QaTeleportToCell(center);
                player.QaSetDirectMovementInput(DirectionVectors[direction]);
                yield return CaptureDirection(player, direction);
                if (_wrongVisibleDirectionFrames != 0)
                {
                    Finish(98, "wrong visible direction frames=" + _wrongVisibleDirectionFrames);
                    yield break;
                }
            }

            int[] turnTargets = { 1, 2, 4 };
            foreach (int targetDirection in turnTargets)
                yield return ValidateTurn(player, center, 0, targetDirection);
            player.QaSetDirectMovementInput(Vector2.zero);
            player.EndQaControl();
            yield return WaitForDirectionScreenshots();

            var result = new StringBuilder();
            result.AppendLine("PLAYER_BAKED_WALK_V2: PASS");
            result.AppendLine("graphics=" + SystemInfo.graphicsDeviceType);
            result.AppendLine("directions=8");
            result.AppendLine("poses=64");
            result.AppendLine("turnChecks=45,90,180");
            result.AppendLine("maxSupportDriftProjectedPx=" +
                              _maximumProjectedSupportDrift.ToString("F6", Invariant));
            result.AppendLine("maxSupportDrift2dPx=" +
                              _maximumSupportDrift2d.ToString("F6", Invariant));
            result.AppendLine("maxContactStepErrorPx=" +
                              _maximumContactStepError.ToString("F6", Invariant));
            result.AppendLine("directionMedianHeightDeltaPercent=" +
                              staticQa.directionMedianHeightDeltaPercent.ToString("F6", Invariant));
            result.AppendLine("waistGapViolations=" + staticQa.waistGapViolations);
            result.AppendLine("detachedAlphaViolations=" + staticQa.detachedAlphaViolations);
            result.AppendLine("wrongVisibleDirectionFrames=" + _wrongVisibleDirectionFrames);
            result.AppendLine("directionScreenshots=8");
            File.WriteAllText(ArtifactPath("player-baked-walk-v2-result.txt"), result.ToString(),
                new UTF8Encoding(false));
            Finish(0, "eight directions, 64 baked poses, support-foot lock, and turns passed");
        }

        private IEnumerator CaptureDirection(OfficeRuntimeAgent player, int direction)
        {
            var boundaryWorld = new Vector2[PlayerBakedWalkCatalogV2.PoseCount];
            var captured = new bool[PlayerBakedWalkCatalogV2.PoseCount];
            bool startedAtZero = false;
            int previousPose = -1;
            int observedTransitions = 0;
            float deadline = Time.realtimeSinceStartup + 30f;
            while (Time.realtimeSinceStartup < deadline && observedTransitions < 9)
            {
                yield return new WaitForEndOfFrame();
                int pose = player.VisibleWalkPose;
                if (pose < 0 || player.LastActualDisplacement.sqrMagnitude <= 0.000000001f) continue;
                if (player.VisibleWalkDirection != direction || player.CurrentDirection != direction)
                {
                    _wrongVisibleDirectionFrames++;
                    continue;
                }
                string expectedSprite = $"player_{DirectionNames[direction]}_walk_{pose}_v2";
                if (!string.Equals(player.VisibleWalkSpriteName, expectedSprite, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        $"Visible baked sprite mismatch expected={expectedSprite} actual={player.VisibleWalkSpriteName}.");
                PlayerWalkSupportLegV2 expectedSupport = pose < 4
                    ? PlayerWalkSupportLegV2.Left
                    : PlayerWalkSupportLegV2.Right;
                if (player.VisibleSupportLeg != expectedSupport)
                    throw new InvalidOperationException($"Visible support leg mismatch at pose {pose}.");
                if (pose == previousPose) continue;
                previousPose = pose;
                if (!startedAtZero)
                {
                    if (pose != 0) continue;
                    startedAtZero = true;
                }
                observedTransitions++;
                if (pose == 0 && captured.All(value => value)) break;
                if (captured[pose]) continue;

                float withinPose = Mathf.Repeat(player.GaitPhase01 * PlayerBakedWalkCatalogV2.PoseCount, 1f);
                Vector2 correctedBoundary = player.VisibleSupportFootWorld -
                                            DirectionVectors[direction] *
                                            (withinPose * OfficeLocomotionGaitRules.DefaultStrideLength /
                                             PlayerBakedWalkCatalogV2.PoseCount);
                boundaryWorld[pose] = correctedBoundary;
                captured[pose] = true;
                if (pose == 2 && !_directionScreenshotRequested[direction])
                {
                    _directionScreenshotRequested[direction] = true;
                    ScreenCapture.CaptureScreenshot(ArtifactPath(
                        "player-baked-walk-v2-" + DirectionNames[direction] + ".png"));
                }
                _trace.WriteLine(string.Join(",", new[]
                {
                    DirectionNames[direction],
                    pose.ToString(Invariant),
                    Time.frameCount.ToString(Invariant),
                    player.GaitPhase01.ToString("F8", Invariant),
                    player.Position.x.ToString("F8", Invariant),
                    player.Position.y.ToString("F8", Invariant),
                    player.VisibleSupportLeg.ToString(),
                    player.VisibleSupportFootWorld.x.ToString("F8", Invariant),
                    player.VisibleSupportFootWorld.y.ToString("F8", Invariant),
                    correctedBoundary.x.ToString("F8", Invariant),
                    correctedBoundary.y.ToString("F8", Invariant),
                    player.VisibleWalkSpriteName
                }));
                _trace.Flush();
            }
            if (!captured.All(value => value))
                throw new InvalidOperationException(
                    $"Direction {DirectionNames[direction]} did not expose all eight poses.");
            ValidateRuntimeFootLock(player, direction, boundaryWorld);
        }

        private IEnumerator WaitForDirectionScreenshots()
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < deadline)
            {
                bool complete = true;
                for (var direction = 0; direction < DirectionNames.Length; direction++)
                {
                    string path = ArtifactPath(
                        "player-baked-walk-v2-" + DirectionNames[direction] + ".png");
                    if (_directionScreenshotRequested[direction] && File.Exists(path)) continue;
                    complete = false;
                    break;
                }
                if (complete) yield break;
                yield return new WaitForEndOfFrame();
            }
            throw new InvalidOperationException("Timed out writing the eight direction screenshots.");
        }

        private void ValidateRuntimeFootLock(
            OfficeRuntimeAgent player,
            int direction,
            Vector2[] boundaryWorld)
        {
            float sourcePixelsPerWorld = player.PresentationRenderer.sprite.pixelsPerUnit /
                                         Mathf.Max(0.0001f,
                                             Mathf.Abs(player.PresentationRenderer.transform.lossyScale.x));
            ValidateGroup(0, 3);
            ValidateGroup(4, 7);
            float contactStepWorld = Vector2.Dot(
                boundaryWorld[4] - boundaryWorld[0], DirectionVectors[direction]);
            float errorPx = Mathf.Abs(contactStepWorld -
                                      OfficeLocomotionGaitRules.DefaultStrideLength * 0.5f) *
                            sourcePixelsPerWorld;
            _maximumContactStepError = Mathf.Max(_maximumContactStepError, errorPx);
            if (errorPx > MaximumContactStepErrorPx)
                throw new InvalidOperationException(
                    $"Runtime contact-step error direction={DirectionNames[direction]} error={errorPx:F3}px.");

            void ValidateGroup(int start, int end)
            {
                for (int pose = start + 1; pose <= end; pose++)
                {
                    Vector2 delta = boundaryWorld[pose] - boundaryWorld[start];
                    float projected = Mathf.Abs(Vector2.Dot(delta, DirectionVectors[direction])) *
                                      sourcePixelsPerWorld;
                    float distance = delta.magnitude * sourcePixelsPerWorld;
                    _maximumProjectedSupportDrift = Mathf.Max(_maximumProjectedSupportDrift, projected);
                    _maximumSupportDrift2d = Mathf.Max(_maximumSupportDrift2d, distance);
                    if (projected > MaximumProjectedSupportDriftPx || distance > MaximumSupportDrift2dPx)
                        throw new InvalidOperationException(
                            $"Runtime support drift direction={DirectionNames[direction]} " +
                            $"poses={start}->{pose} projected={projected:F3}px 2D={distance:F3}px.");
                }
            }
        }

        private IEnumerator ValidateTurn(
            OfficeRuntimeAgent player,
            OfficeGridCoordinate center,
            int fromDirection,
            int targetDirection)
        {
            player.QaTeleportToCell(center);
            player.QaSetDirectMovementInput(DirectionVectors[fromDirection]);
            yield return WaitForVisibleDirection(player, fromDirection, 5f);
            player.QaSetDirectMovementInput(DirectionVectors[targetDirection]);
            yield return WaitForVisibleDirection(player, targetDirection, 5f);
            if (player.VisibleWalkPose < 0 ||
                !player.VisibleWalkSpriteName.Contains("_" + DirectionNames[targetDirection] + "_"))
                throw new InvalidOperationException(
                    $"Turn {fromDirection}->{targetDirection} did not remain on a baked V2 row.");
        }

        private static IEnumerator WaitForVisibleDirection(
            OfficeRuntimeAgent player,
            int direction,
            float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return new WaitForEndOfFrame();
                if (player.VisibleWalkDirection == direction && player.VisibleWalkPose >= 0)
                    yield break;
            }
            throw new InvalidOperationException("Timed out waiting for visible direction " + direction + ".");
        }

        private static void ParkOtherActors(
            StarterOfficeRuntimeBootstrap runtime,
            OfficeRuntimeAgent player)
        {
            var parking = new[]
            {
                new OfficeGridCoordinate(1, 1),
                new OfficeGridCoordinate(runtime.World.Grid.Width - 2, 1),
                new OfficeGridCoordinate(1, runtime.World.Grid.Height - 2)
            };
            var index = 0;
            foreach (OfficeRuntimeAgent actor in runtime.Actors)
            {
                if (actor == null || actor == player) continue;
                actor.QaTeleportToCell(parking[index++]);
                actor.QaSetDirectMovementInput(Vector2.zero);
            }
        }

        private void Finish(int exitCode, string reason)
        {
            _trace?.Flush();
            _trace?.Dispose();
            _trace = null;
            string line = (exitCode == 0 ? "PASS" : "FAIL") + " | code=" + exitCode +
                          " | reason=" + reason;
            File.WriteAllText(ArtifactPath("player-baked-walk-v2-final.txt"),
                line + Environment.NewLine, new UTF8Encoding(false));
            if (exitCode == 0) Debug.Log("PLAYER_BAKED_WALK_V2: " + line);
            else Debug.LogError("PLAYER_BAKED_WALK_V2: " + line);
            Application.Quit(exitCode);
        }

        private string ArtifactPath(string name) => Path.Combine(_artifactDirectory, name);

        private static string ResolveArgument(string name, string fallback)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index < arguments.Length - 1; index++)
                if (string.Equals(arguments[index], name, StringComparison.OrdinalIgnoreCase))
                    return arguments[index + 1];
            return fallback;
        }

        private static bool HasArgument(string name) => Environment.GetCommandLineArgs().Any(
            argument => string.Equals(argument, name, StringComparison.OrdinalIgnoreCase));
    }
}
