using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using FamilyCompany.Presentation.Unity.OfficeGridView;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime.Qa
{
    /// <summary>
    /// Release/D3D11 proof that the source-exact Player contacts are consumed by the real
    /// OfficeRuntimeAgent while its logical root travels exact adjacent tile-center segments.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerTilePathContactPlayerQa : MonoBehaviour
    {
        public const string CommandLineFlag = "-playerTilePathContactQa";
        public const string ArtifactDirectoryArgument = "-playerTilePathContactQaOutput";

        private const float SegmentToleranceWorld = 0.0005f;
        private const float EndpointToleranceWorld = 0.0005f;
        private const float VisualRootToleranceWorld = 0.0005f;
        private const float CaptureIntervalSeconds = 1f / 6f;
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        private static PlayerTilePathContactPlayerQa _instance;

        private string _artifactDirectory = string.Empty;
        private StreamWriter _trace;
        private int _captureIndex;
        private float _nextCaptureAt;
        private float _maximumSegmentDeviation;
        private float _maximumEndpointError;
        private float _maximumVisualRootOffset;
        private int _collisionProjectedFrames;
        private int _spriteViolationCount;
        private int _movingFrames;
        private int _capturedFrames;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => _instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInstall()
        {
            if (_instance != null || !HasArgument(CommandLineFlag)) return;
            var host = new GameObject("~PlayerTilePathContactPlayerQa");
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<PlayerTilePathContactPlayerQa>();
        }

        private void Start()
        {
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            _artifactDirectory = ResolveRequiredArgument(ArtifactDirectoryArgument);
            Directory.CreateDirectory(_artifactDirectory);
            _trace = new StreamWriter(ArtifactPath("player-tile-path-trace.csv"), false, new UTF8Encoding(false));
            _trace.WriteLine(
                "frame,leg,from_x,from_y,to_x,to_y,root_x,root_y,nearest_x,nearest_y," +
                "segment_deviation,visual_root_offset,endpoint_error,path_index,path_length," +
                "motion_direction,display_direction,gait_phase,sprite,collision_projected");
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
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Direct3D11)
            {
                Finish(91, "graphics=" + SystemInfo.graphicsDeviceType);
                yield break;
            }

            PrototypeBootstrap bootstrap = Object.FindFirstObjectByType<PrototypeBootstrap>();
            if (bootstrap == null)
            {
                Finish(92, "PrototypeBootstrap missing");
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
                Finish(93, "starter office runtime not ready");
                yield break;
            }

            OfficeRuntimeAgent player = runtime.Actors.FirstOrDefault(actor =>
                actor != null && string.Equals(actor.AgentId, "player", StringComparison.Ordinal));
            if (player == null)
            {
                Finish(94, "player actor missing");
                yield break;
            }

            if (!TryFindClearLoop(runtime, player.AgentRadius, out OfficeGridCoordinate[] loop))
            {
                Finish(95, "no clear 3x3 perimeter loop");
                yield break;
            }
            ParkOtherActors(runtime, player, loop);
            player.QaTeleportToCell(loop[0]);
            yield return null;
            yield return new WaitForEndOfFrame();

            if (!TryCreateCaptureCamera(runtime, loop, out Camera camera, out RenderTexture target,
                    out GameObject cameraHost, out GameObject pathOverlay, out Material pathMaterial,
                    out string cameraFailure))
            {
                Finish(96, "capture setup failed=" + cameraFailure);
                yield break;
            }

            try
            {
                Capture(camera, target);
                for (var leg = 0; leg < loop.Length - 1; leg++)
                {
                    OfficeGridCoordinate fromCell = loop[leg];
                    OfficeGridCoordinate toCell = loop[leg + 1];
                    Vector2 from = runtime.World.Presenter.CellCenterWorld(fromCell);
                    Vector2 to = runtime.World.Presenter.CellCenterWorld(toCell);
                    if (!player.QaMoveToCell(toCell, "player-tile-path-leg-" + leg))
                    {
                        Finish(97, "QaMoveToCell rejected leg=" + leg + " target=" + toCell);
                        yield break;
                    }

                    float legDeadline = Time.realtimeSinceStartup + 12f;
                    while (!player.QaReachedCell(toCell) && Time.realtimeSinceStartup < legDeadline)
                    {
                        yield return new WaitForEndOfFrame();
                        Sample(runtime, player, leg, fromCell, toCell, from, to, 0f);
                        if (Time.realtimeSinceStartup >= _nextCaptureAt)
                        {
                            Capture(camera, target);
                            _nextCaptureAt = Time.realtimeSinceStartup + CaptureIntervalSeconds;
                        }
                    }
                    if (!player.QaReachedCell(toCell))
                    {
                        Finish(98, "tile arrival timeout leg=" + leg + " target=" + toCell);
                        yield break;
                    }

                    float endpointError = Vector2.Distance(player.Position, to);
                    _maximumEndpointError = Mathf.Max(_maximumEndpointError, endpointError);
                    Sample(runtime, player, leg, fromCell, toCell, from, to, endpointError);
                    Capture(camera, target);
                }

                Vector2 startCenter = runtime.World.Presenter.CellCenterWorld(loop[0]);
                float finalError = Vector2.Distance(player.Position, startCenter);
                _maximumEndpointError = Mathf.Max(_maximumEndpointError, finalError);
                bool passed = _maximumSegmentDeviation <= SegmentToleranceWorld &&
                              _maximumEndpointError <= EndpointToleranceWorld &&
                              _maximumVisualRootOffset <= VisualRootToleranceWorld &&
                              _collisionProjectedFrames == 0 &&
                              _spriteViolationCount == 0 &&
                              _movingFrames > 0 &&
                              _capturedFrames > 8;
                WriteResult(loop, finalError, passed);
                if (!passed)
                {
                    Finish(99, "tile-center invariant failed");
                    yield break;
                }
                Finish(0, "8 adjacent tile-center legs rendered by real OfficeRuntimeAgent");
            }
            finally
            {
                if (camera != null) camera.targetTexture = null;
                if (target != null) target.Release();
                if (target != null) Object.Destroy(target);
                if (cameraHost != null) Object.Destroy(cameraHost);
                if (pathOverlay != null) Object.Destroy(pathOverlay);
                if (pathMaterial != null) Object.Destroy(pathMaterial);
                player.EndQaControl();
            }
        }

        private void Sample(
            StarterOfficeRuntimeBootstrap runtime,
            OfficeRuntimeAgent player,
            int leg,
            OfficeGridCoordinate fromCell,
            OfficeGridCoordinate toCell,
            Vector2 from,
            Vector2 to,
            float endpointError)
        {
            Vector2 root = player.Position;
            float deviation = DistanceToSegment(root, from, to);
            _maximumSegmentDeviation = Mathf.Max(_maximumSegmentDeviation, deviation);
            Vector3 rendererWorld3 = player.PresentationRenderer.transform.position;
            float visualOffset = Vector2.Distance(root, new Vector2(rendererWorld3.x, rendererWorld3.y));
            _maximumVisualRootOffset = Mathf.Max(_maximumVisualRootOffset, visualOffset);
            if (player.WasCollisionProjected) _collisionProjectedFrames++;

            DirectionalLocomotionFrameTrace locomotion = player.CaptureLocomotionFrameTrace();
            if (player.LastActualDisplacement.sqrMagnitude > 0.000000001f)
            {
                _movingFrames++;
                if (!ExpectedNaturalWalkSprite(locomotion.DisplayDirection, locomotion.SpriteName))
                    _spriteViolationCount++;
            }
            OfficeGridCoordinate nearest = runtime.World.Presenter.NearestCell(player.Position);
            _trace.WriteLine(string.Join(",", new[]
            {
                Time.frameCount.ToString(Invariant),
                leg.ToString(Invariant),
                fromCell.X.ToString(Invariant), fromCell.Y.ToString(Invariant),
                toCell.X.ToString(Invariant), toCell.Y.ToString(Invariant),
                F(root.x), F(root.y),
                nearest.X.ToString(Invariant), nearest.Y.ToString(Invariant),
                F(deviation), F(visualOffset), F(endpointError),
                player.PresentationPathIndex.ToString(Invariant),
                player.SemanticPathLength.ToString(Invariant),
                locomotion.MotionDirection.ToString(Invariant),
                locomotion.DisplayDirection.ToString(Invariant),
                F(player.GaitPhase01),
                Csv(locomotion.SpriteName),
                player.WasCollisionProjected ? "true" : "false"
            }));
            _trace.Flush();
        }

        private static bool ExpectedNaturalWalkSprite(int direction, string spriteName)
        {
            if (string.IsNullOrEmpty(spriteName)) return false;
            string sourceDirection = direction switch
            {
                0 => "south",
                1 => "west",
                2 => "west",
                3 => "west",
                4 => "north",
                5 => "east",
                6 => "east",
                7 => "east",
                _ => string.Empty
            };
            return spriteName.StartsWith("player_" + sourceDirection + "_contact_", StringComparison.Ordinal) ||
                   spriteName.StartsWith("player_" + sourceDirection + "_toe_", StringComparison.Ordinal) ||
                   spriteName.StartsWith("player_" + sourceDirection + "_pass_", StringComparison.Ordinal) ||
                   spriteName.StartsWith("player_" + sourceDirection + "_land_", StringComparison.Ordinal);
        }

        private static bool TryFindClearLoop(
            StarterOfficeRuntimeBootstrap runtime,
            float radius,
            out OfficeGridCoordinate[] loop)
        {
            int[,] offsets =
            {
                { 0, 0 }, { 1, 0 }, { 2, 0 }, { 2, 1 }, { 2, 2 },
                { 1, 2 }, { 0, 2 }, { 0, 1 }, { 0, 0 }
            };
            for (var y = 1; y < runtime.World.Grid.Height - 3; y++)
            {
                for (var x = 1; x < runtime.World.Grid.Width - 3; x++)
                {
                    var candidate = new OfficeGridCoordinate[offsets.GetLength(0)];
                    bool valid = true;
                    for (var index = 0; index < candidate.Length; index++)
                    {
                        candidate[index] = new OfficeGridCoordinate(x + offsets[index, 0], y + offsets[index, 1]);
                        if (!runtime.World.Grid.IsWalkable(candidate[index]))
                        {
                            valid = false;
                            break;
                        }
                    }
                    if (!valid) continue;
                    for (var index = 0; index < candidate.Length - 1; index++)
                    {
                        Vector2 from = runtime.World.Presenter.CellCenterWorld(candidate[index]);
                        Vector2 to = runtime.World.Presenter.CellCenterWorld(candidate[index + 1]);
                        if (!runtime.World.Occupancy.CanTraverseStatic(from, to, radius, string.Empty))
                        {
                            valid = false;
                            break;
                        }
                    }
                    if (valid)
                    {
                        loop = candidate;
                        return true;
                    }
                }
            }
            loop = Array.Empty<OfficeGridCoordinate>();
            return false;
        }


        private static void ParkOtherActors(
            StarterOfficeRuntimeBootstrap runtime,
            OfficeRuntimeAgent player,
            IReadOnlyCollection<OfficeGridCoordinate> loop)
        {
            var occupied = new HashSet<OfficeGridCoordinate>(loop);
            var candidates = new List<OfficeGridCoordinate>();
            for (var y = 1; y < runtime.World.Grid.Height - 1; y++)
            for (var x = 1; x < runtime.World.Grid.Width - 1; x++)
            {
                var cell = new OfficeGridCoordinate(x, y);
                if (!occupied.Contains(cell) && runtime.World.Grid.IsWalkable(cell)) candidates.Add(cell);
            }
            int candidateIndex = candidates.Count - 1;
            foreach (OfficeRuntimeAgent actor in runtime.Actors)
            {
                if (actor == null || actor == player) continue;
                if (candidateIndex < 0) throw new InvalidOperationException("no parking cell for " + actor.AgentId);
                actor.QaTeleportToCell(candidates[candidateIndex--]);
                actor.QaSetDirectMovementInput(Vector2.zero);
            }
        }

        private static bool TryCreateCaptureCamera(
            StarterOfficeRuntimeBootstrap runtime,
            IReadOnlyList<OfficeGridCoordinate> loop,
            out Camera camera,
            out RenderTexture target,
            out GameObject cameraHost,
            out GameObject pathOverlay,
            out Material pathMaterial,
            out string failure)
        {
            camera = null;
            target = null;
            cameraHost = null;
            pathOverlay = null;
            pathMaterial = null;
            failure = string.Empty;
            Camera source = Camera.main;
            if (source == null)
            {
                failure = "Camera.main missing";
                return false;
            }
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                failure = "Sprites/Default shader missing";
                return false;
            }

            Vector3[] centers = loop.Select(runtime.World.Presenter.CellCenterWorld).ToArray();
            float minX = centers.Min(point => point.x);
            float maxX = centers.Max(point => point.x);
            float minY = centers.Min(point => point.y);
            float maxY = centers.Max(point => point.y);
            var focus = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f + 0.62f);

            cameraHost = new GameObject("PlayerTilePathCaptureCamera") { hideFlags = HideFlags.HideAndDontSave };
            camera = cameraHost.AddComponent<Camera>();
            camera.CopyFrom(source);
            camera.transform.SetPositionAndRotation(
                new Vector3(focus.x, focus.y, source.transform.position.z),
                source.transform.rotation);
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(3.15f, (maxY - minY) * 0.5f + 2.1f);
            camera.aspect = 16f / 9f;
            camera.enabled = false;
            target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                filterMode = FilterMode.Point,
                antiAliasing = 1
            };
            target.Create();
            camera.targetTexture = target;

            pathOverlay = new GameObject("PlayerTilePathExactCenterOverlay")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            var line = pathOverlay.AddComponent<LineRenderer>();
            pathMaterial = new Material(shader) { color = new Color(0.04f, 0.92f, 0.86f, 0.82f) };
            line.sharedMaterial = pathMaterial;
            line.useWorldSpace = true;
            line.positionCount = centers.Length;
            line.SetPositions(centers);
            line.startWidth = 0.025f;
            line.endWidth = 0.025f;
            line.numCapVertices = 0;
            line.numCornerVertices = 0;
            // Keep the exact-center guide visible over the floor but below every actor/furniture
            // so the QA overlay never paints across the character it is proving.
            line.sortingOrder = OfficeGridTilemapPresenter.FloorSortingOrder + 1;
            return true;
        }

        private void Capture(Camera camera, RenderTexture target)
        {
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.Render();
                RenderTexture.active = target;
                var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false, false)
                {
                    filterMode = FilterMode.Point
                };
                texture.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0, false);
                texture.Apply(false, false);
                File.WriteAllBytes(
                    ArtifactPath($"player-tile-path-frame-{_captureIndex:D4}.png"),
                    texture.EncodeToPNG());
                Object.Destroy(texture);
                _captureIndex++;
                _capturedFrames++;
            }
            finally
            {
                RenderTexture.active = previous;
            }
        }

        private void WriteResult(OfficeGridCoordinate[] loop, float finalError, bool passed)
        {
            var result = new StringBuilder();
            result.AppendLine("FC-PLAYER-TILE-PATH-CONTACT-QA-V1: " + (passed ? "PASS" : "FAIL"));
            result.AppendLine("graphics=" + SystemInfo.graphicsDeviceType);
            result.AppendLine("device=" + SystemInfo.graphicsDeviceName);
            result.AppendLine("route=" + string.Join("->", loop.Select(cell => cell.ToString())));
            result.AppendLine("adjacentTileLegs=8");
            result.AppendLine("movingFrames=" + _movingFrames.ToString(Invariant));
            result.AppendLine("capturedFrames=" + _capturedFrames.ToString(Invariant));
            result.AppendLine("maximumSegmentDeviationWorld=" + F(_maximumSegmentDeviation));
            result.AppendLine("maximumEndpointErrorWorld=" + F(_maximumEndpointError));
            result.AppendLine("maximumVisualRootOffsetWorld=" + F(_maximumVisualRootOffset));
            result.AppendLine("finalStartCenterErrorWorld=" + F(finalError));
            result.AppendLine("collisionProjectedFrames=" + _collisionProjectedFrames.ToString(Invariant));
            result.AppendLine("spriteViolationCount=" + _spriteViolationCount.ToString(Invariant));
            result.AppendLine("segmentToleranceWorld=" + F(SegmentToleranceWorld));
            result.AppendLine("endpointToleranceWorld=" + F(EndpointToleranceWorld));
            File.WriteAllText(ArtifactPath("player-tile-path-result.txt"), result.ToString(), new UTF8Encoding(false));
        }

        private void Finish(int exitCode, string reason)
        {
            _trace?.Flush();
            _trace?.Dispose();
            _trace = null;
            string line = (exitCode == 0 ? "PASS" : "FAIL") + " | code=" + exitCode + " | reason=" + reason;
            File.WriteAllText(ArtifactPath("player-tile-path-final.txt"), line + Environment.NewLine,
                new UTF8Encoding(false));
            if (exitCode == 0) Debug.Log("FC-PLAYER-TILE-PATH-CONTACT-QA-V1: " + line);
            else Debug.LogError("FC-PLAYER-TILE-PATH-CONTACT-QA-V1: " + line);
            Application.Quit(exitCode);
        }

        private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 delta = end - start;
            if (delta.sqrMagnitude <= 0.000000001f) return Vector2.Distance(point, start);
            float t = Mathf.Clamp01(Vector2.Dot(point - start, delta) / delta.sqrMagnitude);
            return Vector2.Distance(point, start + delta * t);
        }

        private string ArtifactPath(string name) => Path.Combine(_artifactDirectory, name);
        private static string F(float value) => value.ToString("F8", Invariant);
        private static string Csv(string value) => "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";

        private static string ResolveRequiredArgument(string name)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index < arguments.Length - 1; index++)
                if (string.Equals(arguments[index], name, StringComparison.OrdinalIgnoreCase))
                    return Path.GetFullPath(arguments[index + 1]);
            throw new InvalidOperationException("Missing command-line argument " + name);
        }

        private static bool HasArgument(string name) => Environment.GetCommandLineArgs().Any(
            argument => string.Equals(argument, name, StringComparison.OrdinalIgnoreCase));
    }
}
