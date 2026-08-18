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
using Object = UnityEngine.Object;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime.Qa
{
    /// <summary>
    /// Opt-in Release/D3D11 proof for the shipping eight-direction walk consumer.  It starts the
    /// normal game, drives the real player agent, samples the final renderer after each frame, and
    /// rejects direction, phase, sprite, or one-cycle world-distance drift.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterLocomotionV1PlayerQa : MonoBehaviour
    {
        public const string CommandLineFlag = "-familyCompanyCharacterLocomotionV1Qa";
        public const string ArtifactDirectoryArgument =
            "-familyCompanyCharacterLocomotionV1QaArtifacts";

        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        private static readonly string[] CharacterIds =
        {
            "player", "older_sister", "father", "mother"
        };
        private static readonly string[] DirectionTokens =
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

        private static CharacterLocomotionV1PlayerQa _instance;
        private string _artifactDirectory = string.Empty;
        private StreamWriter _trace;

        [Serializable]
        private sealed class FootAnchorCatalog
        {
            public string contract;
            public float pixelsPerUnit;
            public float visualScale;
            public float strideWorld;
            public float rootStepPixels;
            public float maximumPlayerSupportDriftPixels;
            public FootAnchorRow[] rows;
        }

        [Serializable]
        private sealed class FootAnchorRow
        {
            public string character;
            public string direction;
            public string[] supportLegs;
            public FootAnchorPoint[] supportAnchors;
        }

        [Serializable]
        private sealed class FootAnchorPoint
        {
            public float x;
            public float y;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => _instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInstall()
        {
            if (_instance != null || !HasArgument(CommandLineFlag)) return;
            var host = new GameObject("~CharacterLocomotionV1PlayerQa");
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<CharacterLocomotionV1PlayerQa>();
        }

        private void Start()
        {
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            _artifactDirectory = ResolveArgument(
                ArtifactDirectoryArgument,
                Path.Combine(Application.persistentDataPath, "CharacterLocomotionV1PlayerQa"));
            Directory.CreateDirectory(_artifactDirectory);
            _trace = new StreamWriter(ArtifactPath("character-locomotion-player-trace.csv"), false,
                new UTF8Encoding(false));
            _trace.WriteLine(
                "character,direction,phase,frame_count,gait_distance,cycle_distance,x,y,dx,dy," +
                "motion_direction,display_direction,sprite_direction,clip,sprite,flip_x," +
                "support_foot_world_x,support_foot_world_y");
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
            PrototypeBootstrap bootstrap = Object.FindFirstObjectByType<PrototypeBootstrap>();
            if (bootstrap == null)
            {
                Finish(91, "PrototypeBootstrap missing");
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
                Finish(92, "starter office runtime did not become ready");
                yield break;
            }

            OfficeRuntimeAgent player = runtime.Actors.FirstOrDefault(actor =>
                actor != null && string.Equals(actor.AgentId, "player", StringComparison.Ordinal));
            if (player == null)
            {
                Finish(93, "player runtime actor missing");
                yield break;
            }
            var start = new OfficeGridCoordinate(runtime.World.Grid.Width / 2,
                runtime.World.Grid.Height / 2);
            if (!runtime.World.Grid.Contains(start) || !runtime.World.Grid.IsWalkable(start))
            {
                Finish(94, "center QA cell is not walkable: " + start);
                yield break;
            }

            // Keep the normal collision/navigation world active, but make the catalog sweep
            // deterministic.  Otherwise the other family actors begin empty-office wandering
            // while hundreds of PNGs are encoded and can eventually block the one-stride lane.
            // Parking uses real QA teleports, so occupancy stays coherent rather than being
            // bypassed or disabled.
            var parkingCells = new[]
            {
                new OfficeGridCoordinate(1, 1),
                new OfficeGridCoordinate(runtime.World.Grid.Width - 2, 1),
                new OfficeGridCoordinate(1, runtime.World.Grid.Height - 2)
            };
            var parkedIndex = 0;
            foreach (OfficeRuntimeAgent actor in runtime.Actors)
            {
                if (actor == null || actor == player) continue;
                OfficeGridCoordinate parkingCell = parkingCells[parkedIndex++];
                if (!runtime.World.Grid.Contains(parkingCell) ||
                    !runtime.World.Grid.IsWalkable(parkingCell))
                {
                    Finish(106, "catalog QA parking cell is not walkable: " + parkingCell);
                    yield break;
                }
                actor.QaTeleportToCell(parkingCell);
                actor.QaSetDirectMovementInput(Vector2.zero);
            }

            OfficeRuntimeCharacterArtCatalog catalog =
                Resources.Load<OfficeRuntimeCharacterArtCatalog>(OfficeRuntimeCharacterArtCatalog.ResourcePath);
            if (catalog == null)
            {
                Finish(101, "OfficeRuntimeCharacterArtCatalog missing");
                yield break;
            }
            TextAsset anchorJson = Resources.Load<TextAsset>("HighMotion/FamilyLocomotionFootAnchorsV1");
            if (anchorJson == null)
            {
                Finish(107, "FamilyLocomotionFootAnchorsV1 resource missing");
                yield break;
            }
            FootAnchorCatalog footAnchors = JsonUtility.FromJson<FootAnchorCatalog>(anchorJson.text);
            if (footAnchors == null ||
                !string.Equals(footAnchors.contract, "FC-FAMILY-LOCOMOTION-FOOT-ANCHORS-V1",
                    StringComparison.Ordinal) || footAnchors.rows == null || footAnchors.rows.Length != 32 ||
                Mathf.Abs(footAnchors.pixelsPerUnit - 180f) > 0.001f ||
                Mathf.Abs(footAnchors.visualScale - 1.55f) > 0.001f ||
                Mathf.Abs(footAnchors.strideWorld - OfficeLocomotionGaitRules.DefaultStrideLength) > 0.0001f)
            {
                Finish(108, "FamilyLocomotionFootAnchorsV1 runtime contract invalid");
                yield break;
            }
            Dictionary<string, FootAnchorRow> anchorRows = footAnchors.rows.ToDictionary(
                row => row.character + "/" + row.direction, row => row, StringComparer.Ordinal);

            var cycleDistances = new List<float>();
            var stepBodyHeightRatios = new List<float>();
            var observedCadences = new List<float>();
            var supportDrifts = new List<float>();
            var contactStepDistances = new List<float>();
            var renderedCharacters = new HashSet<string>(StringComparer.Ordinal);
            var renderedLoops = 0;
            var capturedCloseups = 0;
            var capturedOverviews = 0;
            player.BeginQaControl();
            foreach (string characterId in CharacterIds)
            {
                if (!catalog.TryCopyWalkFrames(characterId, out Sprite[] walkFrames))
                {
                    Finish(102, "catalog walk frames missing: " + characterId);
                    yield break;
                }
                player.QaReplaceWalkFrames(walkFrames);
                foreach (Sprite sprite in walkFrames)
                {
                    if (sprite == null || Mathf.Abs(sprite.pixelsPerUnit - 180f) > 0.001f)
                    {
                        Finish(103, "invalid PPU/null catalog sprite: " + characterId);
                        yield break;
                    }
                }
                renderedCharacters.Add(characterId);
                for (var direction = 0; direction < DirectionVectors.Length; direction++)
                {
                    string anchorKey = characterId + "/" + DirectionTokens[direction];
                    if (!anchorRows.TryGetValue(anchorKey, out FootAnchorRow anchorRow) ||
                        anchorRow.supportLegs == null || anchorRow.supportLegs.Length != 6 ||
                        anchorRow.supportAnchors == null || anchorRow.supportAnchors.Length != 6 ||
                        anchorRow.supportLegs.Take(3).Any(leg => !string.Equals(leg, "left", StringComparison.Ordinal)) ||
                        anchorRow.supportLegs.Skip(3).Any(leg => !string.Equals(leg, "right", StringComparison.Ordinal)))
                    {
                        Finish(109, "invalid explicit support-foot phase ownership: " + anchorKey);
                        yield break;
                    }
                    player.QaTeleportToCell(start);
                    yield return null;
                    player.QaSetDirectMovementInput(DirectionVectors[direction]);
                    var captured = new HashSet<int>();
                    var supportWorldByPhase = new Vector2[6];
                    var previousFrame = -1;
                    float phaseZeroDistance = -1f;
                    // PNG encoding is synchronous in this release-player proof.  A wall-clock-only
                    // deadline therefore penalizes the walk loop for time spent writing evidence,
                    // especially late in the four-family sweep on slower GPUs.  Bound both real
                    // time and rendered samples so a stalled actor still fails without making
                    // evidence capture part of the cadence contract.
                    float deadline = Time.realtimeSinceStartup + 60f;
                    var sampledFrames = 0;
                    bool wrapped = false;
                    while (Time.realtimeSinceStartup < deadline && sampledFrames < 600 &&
                           (!wrapped || captured.Count < 6))
                    {
                        yield return new WaitForEndOfFrame();
                        sampledFrames++;
                        DirectionalLocomotionFrameTrace trace = player.CaptureLocomotionFrameTrace();
                        int phase = player.CurrentWalkFrame;
                        if (!trace.IsMoving || player.LastActualDisplacement.sqrMagnitude <= 0.000000001f)
                            continue;
                        if (trace.DisplayDirection != direction || trace.MotionDirection != direction ||
                            player.CurrentDirection != direction || player.CurrentSpriteDirection != direction ||
                            trace.FlipX)
                        {
                            Finish(95, $"character={characterId} direction mismatch expected={direction} " +
                                       $"motion={trace.MotionDirection} display={trace.DisplayDirection} " +
                                       $"sprite={player.CurrentSpriteDirection} flip={trace.FlipX}");
                            yield break;
                        }
                        string expected = $"{characterId}_{DirectionTokens[direction]}_walk_{phase}";
                        if (!string.Equals(trace.SpriteName, expected, StringComparison.Ordinal))
                        {
                            Finish(96, $"sprite mismatch expected={expected} actual={trace.SpriteName}");
                            yield break;
                        }

                        if (!captured.Contains(phase))
                        {
                            if (!TryResolveSupportFootWorld(player.PresentationRenderer, anchorRow, phase,
                                    out Vector2 supportFootWorld, out string anchorFailure))
                            {
                                Finish(110, $"character={characterId} direction={DirectionTokens[direction]} " +
                                            "support anchor failed: " + anchorFailure);
                                yield break;
                            }
                            supportWorldByPhase[phase] = supportFootWorld;
                            if (string.Equals(characterId, "player", StringComparison.Ordinal) ||
                                phase == 0 || phase == 2 || phase == 3 || phase == 5)
                            {
                                string closeup = ArtifactPath(
                                    $"{characterId}-{DirectionTokens[direction]}-phase-{phase}-close.png");
                                // Keep the complete hat/hair silhouette and both feet inside the
                                // evidence frame.  The former 1.15 framing cropped the player's
                                // newsboy cap and the sister's top hair even though the sprite was
                                // intact, making the QA artifact itself look like a product defect.
                                if (!TryCapture(player, closeup, 768, 768, 1.35f, out string failure))
                                {
                                    Finish(97, "closeup capture failed: " + failure);
                                    yield break;
                                }
                                capturedCloseups++;
                            }
                            if (phase == 2)
                            {
                                string overview = ArtifactPath(
                                    $"{characterId}-{DirectionTokens[direction]}-overview.png");
                                if (!TryCapture(player, overview, 1392, 699, 6.4f, out string failure))
                                {
                                    Finish(98, "overview capture failed: " + failure);
                                    yield break;
                                }
                                capturedOverviews++;
                            }
                            captured.Add(phase);
                            if (phase == 0 && phaseZeroDistance < 0f)
                                phaseZeroDistance = player.GaitDistance;
                            WriteTrace(characterId, direction, phase, player, trace, 0f, supportFootWorld);
                        }

                        if (previousFrame == 5 && phase == 0 && phaseZeroDistance >= 0f)
                        {
                            float cycleDistance = player.GaitDistance - phaseZeroDistance;
                            cycleDistances.Add(cycleDistance);
                            if (!TryResolveSupportFootWorld(player.PresentationRenderer, anchorRow, phase,
                                    out Vector2 wrappedSupportWorld, out string wrappedAnchorFailure))
                            {
                                Finish(110, "wrapped support anchor failed: " + wrappedAnchorFailure);
                                yield break;
                            }
                            WriteTrace(characterId, direction, phase, player, trace, cycleDistance,
                                wrappedSupportWorld);
                            float tolerance = 0.08f;
                            if (Mathf.Abs(cycleDistance - OfficeLocomotionGaitRules.DefaultStrideLength) > tolerance)
                            {
                                Finish(99, $"character={characterId} cycle/world mismatch direction={direction} " +
                                           $"actual={cycleDistance:F6} expected=" +
                                           OfficeLocomotionGaitRules.DefaultStrideLength.ToString("F6", Invariant));
                                yield break;
                            }
                            float renderedHeight = Mathf.Max(0.0001f, player.PresentationRenderer.bounds.size.y);
                            float stepBodyRatio = cycleDistance * 0.5f / renderedHeight;
                            if (stepBodyRatio < 0.18f || stepBodyRatio > 0.70f)
                            {
                                Finish(104, $"character={characterId} cadence/scale skating risk " +
                                            $"stepBodyHeightRatio={stepBodyRatio:F4}");
                                yield break;
                            }
                            float cadence = Mathf.Max(0f, trace.ActualSpeed) / cycleDistance * 2f;
                            if (cadence < 1.85f || cadence > 2.15f)
                            {
                                Finish(105, $"character={characterId} cadence={cadence:F4} steps/s");
                                yield break;
                            }
                            stepBodyHeightRatios.Add(stepBodyRatio);
                            observedCadences.Add(cadence);
                            renderedLoops++;
                            wrapped = true;
                        }
                        previousFrame = phase;
                    }
                    player.QaSetDirectMovementInput(Vector2.zero);
                    if (captured.Count != 6 || !wrapped)
                    {
                        Finish(100, $"character={characterId} incomplete direction={direction} phases=" +
                                    string.Join("/", captured.OrderBy(value => value)) + " wrapped=" + wrapped +
                                    " sampledFrames=" + sampledFrames);
                        yield break;
                    }
                    float sourcePixelsPerWorld = 180f /
                        Mathf.Max(0.0001f, Mathf.Abs(player.PresentationRenderer.transform.lossyScale.x));
                    float leftSupportDrift = Mathf.Max(
                        Mathf.Abs(Vector2.Dot(supportWorldByPhase[1] - supportWorldByPhase[0],
                            DirectionVectors[direction])),
                        Mathf.Abs(Vector2.Dot(supportWorldByPhase[2] - supportWorldByPhase[0],
                            DirectionVectors[direction]))) * sourcePixelsPerWorld;
                    float rightSupportDrift = Mathf.Max(
                        Mathf.Abs(Vector2.Dot(supportWorldByPhase[4] - supportWorldByPhase[3],
                            DirectionVectors[direction])),
                        Mathf.Abs(Vector2.Dot(supportWorldByPhase[5] - supportWorldByPhase[3],
                            DirectionVectors[direction]))) * sourcePixelsPerWorld;
                    float maximumSupportDrift = Mathf.Max(leftSupportDrift, rightSupportDrift);
                    if (maximumSupportDrift > footAnchors.maximumPlayerSupportDriftPixels)
                    {
                        Finish(111, $"character={characterId} direction={DirectionTokens[direction]} " +
                                    $"screen support-foot drift={maximumSupportDrift:F3}px > " +
                                    $"{footAnchors.maximumPlayerSupportDriftPixels:F3}px");
                        yield break;
                    }
                    float contactStep = Vector2.Dot(supportWorldByPhase[3] - supportWorldByPhase[0],
                        DirectionVectors[direction]);
                    float expectedContactStep = OfficeLocomotionGaitRules.DefaultStrideLength * 0.5f;
                    if (Mathf.Abs(contactStep - expectedContactStep) > 0.05f)
                    {
                        Finish(112, $"character={characterId} direction={DirectionTokens[direction]} " +
                                    $"alternating contact step={contactStep:F4} expected={expectedContactStep:F4}");
                        yield break;
                    }
                    supportDrifts.Add(maximumSupportDrift);
                    contactStepDistances.Add(contactStep);
                }
            }
            player.QaSetDirectMovementInput(Vector2.zero);
            player.EndQaControl();

            var result = new StringBuilder();
            result.AppendLine("FC-CHARACTER-LOCOMOTION-PLAYER-QA-V1: PASS");
            result.AppendLine("graphics=" + SystemInfo.graphicsDeviceType);
            result.AppendLine("charactersRenderedInGame=" + string.Join(",", renderedCharacters));
            result.AppendLine("characterCount=" + renderedCharacters.Count.ToString(Invariant));
            result.AppendLine("directions=8");
            result.AppendLine("renderedLoops=" + renderedLoops.ToString(Invariant));
            result.AppendLine("capturedCloseups=" + capturedCloseups.ToString(Invariant));
            result.AppendLine("capturedOverviews=" + capturedOverviews.ToString(Invariant));
            result.AppendLine("strideWorld=" +
                              OfficeLocomotionGaitRules.DefaultStrideLength.ToString("F8", Invariant));
            result.AppendLine("cycleDistances=" +
                              string.Join(",", cycleDistances.Select(value => value.ToString("F6", Invariant))));
            result.AppendLine("cadenceStepsPerSecondMinMax=" +
                              observedCadences.Min().ToString("F4", Invariant) + "," +
                              observedCadences.Max().ToString("F4", Invariant));
            result.AppendLine("worldStepBodyHeightRatioMinMax=" +
                              stepBodyHeightRatios.Min().ToString("F4", Invariant) + "," +
                              stepBodyHeightRatios.Max().ToString("F4", Invariant));
            result.AppendLine("screenSupportFootDriftSourcePxMinMax=" +
                              supportDrifts.Min().ToString("F4", Invariant) + "," +
                              supportDrifts.Max().ToString("F4", Invariant));
            result.AppendLine("alternatingContactStepWorldMinMax=" +
                              contactStepDistances.Min().ToString("F4", Invariant) + "," +
                              contactStepDistances.Max().ToString("F4", Invariant));
            File.WriteAllText(ArtifactPath("character-locomotion-player-result.txt"), result.ToString(),
                new UTF8Encoding(false));
            Finish(0, "4 family characters, 8 directions, 192 phases, and stride-owned cadence rendered");
        }

        private void WriteTrace(string characterId, int direction, int phase, OfficeRuntimeAgent actor,
            DirectionalLocomotionFrameTrace trace, float cycleDistance, Vector2 supportFootWorld)
        {
            Vector2 displacement = trace.ActualDisplacement;
            _trace.WriteLine(string.Join(",", new[]
            {
                characterId, DirectionTokens[direction], phase.ToString(Invariant),
                Time.frameCount.ToString(Invariant),
                actor.GaitDistance.ToString("F6", Invariant), cycleDistance.ToString("F6", Invariant),
                actor.Position.x.ToString("F6", Invariant), actor.Position.y.ToString("F6", Invariant),
                displacement.x.ToString("F6", Invariant), displacement.y.ToString("F6", Invariant),
                trace.MotionDirection.ToString(Invariant), trace.DisplayDirection.ToString(Invariant),
                actor.CurrentSpriteDirection.ToString(Invariant), trace.Clip, trace.SpriteName,
                trace.FlipX ? "true" : "false",
                supportFootWorld.x.ToString("F6", Invariant),
                supportFootWorld.y.ToString("F6", Invariant)
            }));
            _trace.Flush();
        }

        private static bool TryResolveSupportFootWorld(SpriteRenderer renderer, FootAnchorRow row, int phase,
            out Vector2 world, out string failure)
        {
            world = Vector2.zero;
            failure = string.Empty;
            if (renderer == null || renderer.sprite == null)
            {
                failure = "SpriteRenderer/sprite missing";
                return false;
            }
            if (phase < 0 || phase >= 6 || row?.supportAnchors == null || row.supportAnchors.Length != 6)
            {
                failure = "phase/row invalid";
                return false;
            }
            Sprite sprite = renderer.sprite;
            FootAnchorPoint anchor = row.supportAnchors[phase];
            float ppu = sprite.pixelsPerUnit;
            Vector3 local = new Vector3(
                (anchor.x - sprite.pivot.x) / ppu,
                (sprite.rect.height - anchor.y - sprite.pivot.y) / ppu,
                0f);
            Vector3 transformed = renderer.transform.TransformPoint(local);
            world = new Vector2(transformed.x, transformed.y);
            return !float.IsNaN(world.x) && !float.IsInfinity(world.x) &&
                   !float.IsNaN(world.y) && !float.IsInfinity(world.y);
        }

        private bool TryCapture(OfficeRuntimeAgent actor, string path, int width, int height,
            float orthographicSize, out string failure)
        {
            failure = string.Empty;
            Camera source = Camera.main;
            if (source == null)
            {
                failure = "Camera.main missing";
                return false;
            }
            RenderTexture previous = RenderTexture.active;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            GameObject host = null;
            try
            {
                host = new GameObject("CharacterLocomotionV1Capture")
                    { hideFlags = HideFlags.HideAndDontSave };
                Camera camera = host.AddComponent<Camera>();
                camera.CopyFrom(source);
                Vector3 sourcePosition = source.transform.position;
                camera.transform.SetPositionAndRotation(
                    new Vector3(actor.Position.x, actor.Position.y + (orthographicSize <= 2f ? 0.65f : 0f),
                        sourcePosition.z),
                    source.transform.rotation);
                camera.orthographic = true;
                camera.orthographicSize = orthographicSize;
                camera.aspect = width / (float)height;
                camera.enabled = false;
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
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
                if (host != null) Object.Destroy(host);
                Object.Destroy(target);
                Object.Destroy(pixels);
            }
        }

        private void Finish(int exitCode, string reason)
        {
            _trace?.Flush();
            _trace?.Dispose();
            _trace = null;
            string line = (exitCode == 0 ? "PASS" : "FAIL") + " | code=" + exitCode +
                          " | reason=" + reason;
            File.WriteAllText(ArtifactPath("character-locomotion-player-final.txt"),
                line + Environment.NewLine, new UTF8Encoding(false));
            if (exitCode == 0) Debug.Log("FC-CHARACTER-LOCOMOTION-PLAYER-QA-V1: " + line);
            else Debug.LogError("FC-CHARACTER-LOCOMOTION-PLAYER-QA-V1: " + line);
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
