using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime.Qa
{
    /// <summary>
    /// One-shot evidence runner for the pre-contact-depth regression. It deliberately keeps the
    /// production sorter untouched and places each idle family member on their own chair contact,
    /// where the old nearest-cell/static-priority rule paints the complete workstation over them.
    /// The normal runner uses a different flag; this baseline flag is retained only so the exact
    /// before frame can be reproduced from an old player binary.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OfficeFurnitureDepthBaselinePlayerQa : MonoBehaviour
    {
        public const string CommandLineFlag = "-familyCompanyFurnitureDepthBaselineQa";
        public const string AfterCommandLineFlag = "-familyCompanyFurnitureDepthAfterQa";
        public const string ArtifactDirectoryArgument =
            "-familyCompanyFurnitureDepthBaselineQaArtifacts";
        public const string AfterArtifactDirectoryArgument =
            "-familyCompanyFurnitureDepthAfterQaArtifacts";

        private static readonly string[] MemberIds =
            { "player", "older_sister", "father", "mother" };
        private const int DifferenceThreshold = 6;
        private static OfficeFurnitureDepthBaselinePlayerQa _instance;

        private StarterOfficeRuntimeBootstrap _runtime;
        private string _artifactDirectory = string.Empty;
        private bool _expectFixed;
        private int _actorSortingOrder;
        private int _targetMinimumSortingOrder;
        private int _targetMaximumSortingOrder;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInstall()
        {
            if (_instance != null ||
                (!HasArgument(CommandLineFlag) && !HasArgument(AfterCommandLineFlag))) return;
            var host = new GameObject("~OfficeFurnitureDepthBaselinePlayerQa");
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<OfficeFurnitureDepthBaselinePlayerQa>();
        }

        private void Start()
        {
            _artifactDirectory = ResolveArtifactDirectory();
            _expectFixed = HasArgument(AfterCommandLineFlag);
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
                    Finish(false, 96, "Unhandled " + exception.GetType().Name + ": " + exception.Message, default);
                    yield break;
                }
                yield return yielded;
            }
        }

        private IEnumerator Run()
        {
            Debug.Log(
                "FAMILY_COMPANY_FURNITURE_DEPTH_BASELINE_QA: START | artifacts=" +
                _artifactDirectory);
            yield return null;
            PrototypeBootstrap bootstrap = Object.FindFirstObjectByType<PrototypeBootstrap>();
            if (bootstrap == null)
            {
                Finish(false, 91, "PrototypeBootstrap is missing.", default);
                yield break;
            }

            ScenePreviewJump.ShowStarterOffice();
            bootstrap.StartNewGameNow(1, false);
            ScenePreviewJump.ShowStarterOffice();
            float deadline = Time.realtimeSinceStartup + 25f;
            while (Time.realtimeSinceStartup < deadline)
            {
                _runtime = Object.FindFirstObjectByType<StarterOfficeRuntimeBootstrap>();
                if (_runtime != null && _runtime.IsReady && _runtime.World != null &&
                    _runtime.Actors.Count == MemberIds.Length) break;
                yield return null;
            }

            if (_runtime == null || !_runtime.IsReady || _runtime.World == null ||
                _runtime.Actors.Count != MemberIds.Length)
            {
                Finish(false, 92, "Starter Office runtime did not become ready with four actors.", default);
                yield break;
            }

            Dictionary<string, OfficeRuntimeAgent> actors = _runtime.Actors
                .Where(actor => actor != null)
                .ToDictionary(actor => actor.AgentId, actor => actor, StringComparer.Ordinal);
            if (MemberIds.Any(id => !actors.ContainsKey(id)))
            {
                Finish(false, 92, "Canonical family actor set is incomplete.", default);
                yield break;
            }

            foreach (string memberId in MemberIds)
            {
                OfficeSeatSlot seat = _runtime.World.Grid.SeatSlots.FirstOrDefault(
                    candidate => string.Equals(
                        candidate.SeatId,
                        "seat_" + memberId,
                        StringComparison.Ordinal));
                if (seat == null)
                {
                    Finish(false, 93, "Canonical seat is missing for " + memberId + ".", default);
                    yield break;
                }
                actors[memberId].BeginQaControl();
                actors[memberId].QaTeleportToCell(seat.Cell);
            }

            // Three complete frames ensure runtime motion has stopped and the old footprint sorter
            // has applied the same static relation repeatedly before any renderer is toggled.
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();

            OfficeRuntimeAgent focusActor = actors["older_sister"];
            OfficeSeatSlot focusSeat = _runtime.World.Grid.SeatSlots.First(
                seat => string.Equals(seat.SeatId, "seat_older_sister", StringComparison.Ordinal));
            SpriteRenderer actorRenderer = focusActor.PresentationRenderer;
            if (actorRenderer == null || actorRenderer.sprite == null)
            {
                Finish(false, 94, "Focused actor renderer is unavailable.", default);
                yield break;
            }

            var targetRenderers = new List<SpriteRenderer>();
            bool hasChairBase = AddFurnitureRenderers(
                focusSeat.ChairFurnitureId,
                targetRenderers);
            bool hasDeskBase = AddFurnitureRenderers(
                focusSeat.WorkSurfaceFurnitureId,
                targetRenderers);
            if (!hasChairBase || !hasDeskBase)
            {
                Finish(false, 94, "Focused chair/desk base renderers are incomplete.", default);
                yield break;
            }

            Camera camera = Camera.main;
            if (camera == null)
            {
                Finish(false, 94, "Camera.main is missing.", default);
                yield break;
            }

            _actorSortingOrder = actorRenderer.sortingOrder;
            _targetMinimumSortingOrder = targetRenderers
                .Where(renderer => renderer != null && renderer.enabled)
                .Min(renderer => renderer.sortingOrder);
            _targetMaximumSortingOrder = targetRenderers
                .Where(renderer => renderer != null && renderer.enabled)
                .Max(renderer => renderer.sortingOrder);

            string evidenceToken = _expectFixed ? "after" : "before";
            string overviewPath = ArtifactPath(
                "furniture-depth-" + evidenceToken + "-overview-1920x1080.png");
            CapturedFrame normal = Capture(camera, 1920, 1080, overviewPath);
            RectInt actorRect = ProjectBounds(camera, actorRenderer.bounds, normal.Width, normal.Height);
            Bounds focusBounds = actorRenderer.bounds;
            foreach (SpriteRenderer renderer in targetRenderers)
                if (renderer != null) focusBounds.Encapsulate(renderer.bounds);
            RectInt focusRect = ProjectBounds(camera, focusBounds, normal.Width, normal.Height);
            focusRect = ExpandAndClamp(focusRect, normal.Width, normal.Height, 24);
            string cropPath = ArtifactPath(
                "furniture-depth-" + evidenceToken + "-crop-200pct.png");
            SaveNearestTwoTimesCrop(normal, focusRect, cropPath);

            bool actorWasEnabled = actorRenderer.enabled;
            var targetStates = targetRenderers.Select(renderer => renderer.enabled).ToArray();
            CapturedFrame actorOnly;
            CapturedFrame targetOnly;
            CapturedFrame background;
            try
            {
                SetEnabled(targetRenderers, false);
                actorOnly = Capture(camera, 1920, 1080, string.Empty);
                actorRenderer.enabled = false;
                background = Capture(camera, 1920, 1080, string.Empty);
                SetEnabled(targetRenderers, targetStates);
                targetOnly = Capture(camera, 1920, 1080, string.Empty);
            }
            finally
            {
                actorRenderer.enabled = actorWasEnabled;
                SetEnabled(targetRenderers, targetStates);
            }

            BaselineMetrics metrics = Measure(
                normal,
                actorOnly,
                targetOnly,
                background,
                actorRect);
            bool reproduced = metrics.ActorPixels > 0 &&
                              metrics.TargetOverlapCandidates > 0 &&
                              _actorSortingOrder <= _targetMaximumSortingOrder &&
                              metrics.HeadInvalidOcclusionPixels +
                              metrics.TorsoInvalidOcclusionPixels > 0;
            bool fixedContract = metrics.ActorPixels > 0 &&
                                 metrics.TargetOverlapCandidates > 0 &&
                                 metrics.HeadOverlapCandidatePixels > 0 &&
                                 metrics.TorsoOverlapCandidatePixels > 0 &&
                                 _actorSortingOrder > _targetMaximumSortingOrder &&
                                 metrics.HeadInvalidOcclusionPixels == 0 &&
                                 metrics.TorsoInvalidOcclusionPixels == 0;
            bool pass = _expectFixed ? fixedContract : reproduced;
            Finish(
                pass,
                pass ? 0 : 95,
                _expectFixed
                    ? (fixedContract
                        ? "Continuous contact-depth keeps the front actor's head and torso unobscured."
                        : "The front actor still has unintended head/torso furniture occlusion.")
                    : (reproduced
                        ? "Old nearest-cell/static-priority upper-body regression reproduced."
                        : "The expected old upper-body regression was not reproduced."),
                metrics,
                overviewPath,
                cropPath);
        }

        private bool AddFurnitureRenderers(
            string furnitureId,
            ICollection<SpriteRenderer> output)
        {
            bool hasBase = false;
            if (_runtime.World.FurniturePresenter.TryGetRenderer(
                    furnitureId,
                    out SpriteRenderer baseRenderer) && baseRenderer != null)
            {
                output.Add(baseRenderer);
                hasBase = true;
            }
            if (_runtime.World.FurniturePresenter.FrontOverlayRenderers.TryGetValue(
                    furnitureId,
                    out SpriteRenderer frontRenderer) && frontRenderer != null)
                output.Add(frontRenderer);
            return hasBase;
        }

        private void Finish(
            bool pass,
            int exitCode,
            string message,
            BaselineMetrics metrics,
            string overviewPath = "",
            string cropPath = "")
        {
            string markerPrefix = _expectFixed
                ? "FAMILY_COMPANY_FURNITURE_DEPTH_AFTER_QA: "
                : "FAMILY_COMPANY_FURNITURE_DEPTH_BASELINE_QA: ";
            string marker = markerPrefix + (pass ? "PASS" : "FAIL");
            string result = marker + Environment.NewLine +
                            "message=" + message + Environment.NewLine +
                            "actorPixels=" + metrics.ActorPixels + Environment.NewLine +
                            "targetOverlapCandidates=" + metrics.TargetOverlapCandidates + Environment.NewLine +
                            "headOverlapCandidates=" + metrics.HeadOverlapCandidatePixels + Environment.NewLine +
                            "torsoOverlapCandidates=" + metrics.TorsoOverlapCandidatePixels + Environment.NewLine +
                            "actorSortingOrder=" + _actorSortingOrder + Environment.NewLine +
                            "targetSortingOrderRange=" + _targetMinimumSortingOrder + ".." +
                            _targetMaximumSortingOrder + Environment.NewLine +
                            "contact=canonical_seat_cell_center; expectedAfter=actor_above_complete_workstation" +
                            Environment.NewLine +
                            "headInvalidOcclusionPixels=" + metrics.HeadInvalidOcclusionPixels + Environment.NewLine +
                            "torsoInvalidOcclusionPixels=" + metrics.TorsoInvalidOcclusionPixels + Environment.NewLine +
                            "overview=" + overviewPath + Environment.NewLine +
                            "crop200=" + cropPath + Environment.NewLine;
            try
            {
                File.WriteAllText(
                    ArtifactPath(_expectFixed
                        ? "furniture-depth-after-result.txt"
                        : "furniture-depth-baseline-result.txt"),
                    result);
            }
            catch (Exception exception)
            {
                Debug.LogError("Baseline result write failed: " + exception.Message);
                if (pass) exitCode = 97;
            }
            if (pass) Debug.Log(result.TrimEnd());
            else Debug.LogError(result.TrimEnd());
            Application.Quit(exitCode);
        }

        private static BaselineMetrics Measure(
            CapturedFrame normal,
            CapturedFrame actorOnly,
            CapturedFrame targetOnly,
            CapturedFrame background,
            RectInt actorRect)
        {
            if (!normal.IsCompatible(actorOnly) || !normal.IsCompatible(targetOnly) ||
                !normal.IsCompatible(background))
                throw new InvalidOperationException("Four-pass capture sizes differ.");

            int actorPixels = 0;
            int candidates = 0;
            int headCandidates = 0;
            int torsoCandidates = 0;
            int headInvalid = 0;
            int torsoInvalid = 0;
            int yMin = Mathf.Clamp(actorRect.yMin, 0, normal.Height - 1);
            int yMax = Mathf.Clamp(actorRect.yMax, yMin + 1, normal.Height);
            int xMin = Mathf.Clamp(actorRect.xMin, 0, normal.Width - 1);
            int xMax = Mathf.Clamp(actorRect.xMax, xMin + 1, normal.Width);
            float height = Mathf.Max(1f, yMax - yMin);
            for (int y = yMin; y < yMax; y++)
            for (int x = xMin; x < xMax; x++)
            {
                int index = y * normal.Width + x;
                bool actor = IsDifferenceCore(actorOnly, background, x, y);
                if (!actor) continue;
                actorPixels++;
                bool target = IsDifferenceCore(targetOnly, background, x, y);
                if (!target) continue;
                candidates++;
                float vertical01 = (y + 0.5f - yMin) / height;
                bool isHead = vertical01 >= 0.7f;
                bool isTorso = !isHead && vertical01 >= 0.34f;
                if (isHead) headCandidates++;
                else if (isTorso) torsoCandidates++;
                if (!Different(normal.Pixels[index], actorOnly.Pixels[index])) continue;
                if (isHead) headInvalid++;
                else if (isTorso) torsoInvalid++;
            }
            return new BaselineMetrics(
                actorPixels,
                candidates,
                headCandidates,
                torsoCandidates,
                headInvalid,
                torsoInvalid);
        }

        private static bool IsDifferenceCore(
            CapturedFrame foreground,
            CapturedFrame background,
            int x,
            int y)
        {
            if (x <= 0 || y <= 0 || x >= foreground.Width - 1 ||
                y >= foreground.Height - 1) return false;
            for (int offsetY = -1; offsetY <= 1; offsetY++)
            for (int offsetX = -1; offsetX <= 1; offsetX++)
            {
                int index = (y + offsetY) * foreground.Width + x + offsetX;
                if (!Different(foreground.Pixels[index], background.Pixels[index])) return false;
            }
            return true;
        }

        private static bool Different(Color32 left, Color32 right)
        {
            int delta = Math.Abs(left.r - right.r) + Math.Abs(left.g - right.g) +
                        Math.Abs(left.b - right.b) + Math.Abs(left.a - right.a);
            return delta >= DifferenceThreshold;
        }

        private static CapturedFrame Capture(Camera source, int width, int height, string path)
        {
            var cameraObject = new GameObject("~FurnitureDepthBaselineCaptureCamera");
            Camera captureCamera = cameraObject.AddComponent<Camera>();
            captureCamera.CopyFrom(source);
            captureCamera.transform.SetPositionAndRotation(
                source.transform.position,
                source.transform.rotation);
            captureCamera.aspect = width / (float)height;
            captureCamera.enabled = false;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                filterMode = FilterMode.Point,
                antiAliasing = 1
            };
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                captureCamera.targetTexture = target;
                captureCamera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                texture.Apply(false, false);
                Color32[] pixels = texture.GetPixels32();
                if (!string.IsNullOrWhiteSpace(path)) File.WriteAllBytes(path, texture.EncodeToPNG());
                return new CapturedFrame(width, height, pixels);
            }
            finally
            {
                RenderTexture.active = previous;
                captureCamera.targetTexture = null;
                target.Release();
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(cameraObject);
            }
        }

        private static RectInt ProjectBounds(Camera camera, Bounds bounds, int width, int height)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            Vector3[] corners =
            {
                new Vector3(min.x, min.y, min.z), new Vector3(min.x, max.y, min.z),
                new Vector3(max.x, min.y, min.z), new Vector3(max.x, max.y, min.z),
                new Vector3(min.x, min.y, max.z), new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, max.z), new Vector3(max.x, max.y, max.z)
            };
            float minX = float.PositiveInfinity;
            float minY = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float maxY = float.NegativeInfinity;
            foreach (Vector3 corner in corners)
            {
                Vector3 viewport = camera.WorldToViewportPoint(corner);
                minX = Mathf.Min(minX, viewport.x * width);
                minY = Mathf.Min(minY, viewport.y * height);
                maxX = Mathf.Max(maxX, viewport.x * width);
                maxY = Mathf.Max(maxY, viewport.y * height);
            }
            return Rect.MinMaxRect(minX, minY, maxX, maxY).ToRectInt();
        }

        private static RectInt ExpandAndClamp(RectInt value, int width, int height, int padding)
        {
            int xMin = Mathf.Clamp(value.xMin - padding, 0, width - 1);
            int yMin = Mathf.Clamp(value.yMin - padding, 0, height - 1);
            int xMax = Mathf.Clamp(value.xMax + padding, xMin + 1, width);
            int yMax = Mathf.Clamp(value.yMax + padding, yMin + 1, height);
            return new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        private static void SaveNearestTwoTimesCrop(
            CapturedFrame frame,
            RectInt crop,
            string path)
        {
            int outputWidth = crop.width * 2;
            int outputHeight = crop.height * 2;
            var texture = new Texture2D(outputWidth, outputHeight, TextureFormat.RGBA32, false);
            var pixels = new Color32[outputWidth * outputHeight];
            for (int y = 0; y < crop.height; y++)
            for (int x = 0; x < crop.width; x++)
            {
                Color32 color = frame.Pixels[(crop.y + y) * frame.Width + crop.x + x];
                int outputX = x * 2;
                int outputY = y * 2;
                pixels[outputY * outputWidth + outputX] = color;
                pixels[outputY * outputWidth + outputX + 1] = color;
                pixels[(outputY + 1) * outputWidth + outputX] = color;
                pixels[(outputY + 1) * outputWidth + outputX + 1] = color;
            }
            try
            {
                texture.SetPixels32(pixels);
                texture.Apply(false, false);
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        private static void SetEnabled(IReadOnlyList<SpriteRenderer> renderers, bool enabled)
        {
            for (int index = 0; index < renderers.Count; index++)
                if (renderers[index] != null) renderers[index].enabled = enabled;
        }

        private static void SetEnabled(
            IReadOnlyList<SpriteRenderer> renderers,
            IReadOnlyList<bool> states)
        {
            for (int index = 0; index < renderers.Count; index++)
                if (renderers[index] != null) renderers[index].enabled = states[index];
        }

        private string ArtifactPath(string fileName) => Path.Combine(_artifactDirectory, fileName);

        private static bool HasArgument(string argument)
        {
            return Environment.GetCommandLineArgs().Any(
                value => string.Equals(value, argument, StringComparison.OrdinalIgnoreCase));
        }

        private static string ResolveArtifactDirectory()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index < arguments.Length - 1; index++)
            {
                if (!string.Equals(
                        arguments[index],
                        ArtifactDirectoryArgument,
                        StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(
                        arguments[index],
                        AfterArtifactDirectoryArgument,
                        StringComparison.OrdinalIgnoreCase)) continue;
                return Path.GetFullPath(arguments[index + 1]);
            }
            return Path.Combine(Application.persistentDataPath, "FurnitureDepthBaselineQa");
        }

        private readonly struct CapturedFrame
        {
            public CapturedFrame(int width, int height, Color32[] pixels)
            {
                Width = width;
                Height = height;
                Pixels = pixels;
            }

            public int Width { get; }
            public int Height { get; }
            public Color32[] Pixels { get; }
            public bool IsCompatible(CapturedFrame other) =>
                Width == other.Width && Height == other.Height &&
                Pixels != null && other.Pixels != null && Pixels.Length == other.Pixels.Length;
        }

        private readonly struct BaselineMetrics
        {
            public BaselineMetrics(
                int actorPixels,
                int targetOverlapCandidates,
                int headOverlapCandidatePixels,
                int torsoOverlapCandidatePixels,
                int headInvalidOcclusionPixels,
                int torsoInvalidOcclusionPixels)
            {
                ActorPixels = actorPixels;
                TargetOverlapCandidates = targetOverlapCandidates;
                HeadOverlapCandidatePixels = headOverlapCandidatePixels;
                TorsoOverlapCandidatePixels = torsoOverlapCandidatePixels;
                HeadInvalidOcclusionPixels = headInvalidOcclusionPixels;
                TorsoInvalidOcclusionPixels = torsoInvalidOcclusionPixels;
            }

            public int ActorPixels { get; }
            public int TargetOverlapCandidates { get; }
            public int HeadOverlapCandidatePixels { get; }
            public int TorsoOverlapCandidatePixels { get; }
            public int HeadInvalidOcclusionPixels { get; }
            public int TorsoInvalidOcclusionPixels { get; }
        }
    }

    internal static class OfficeFurnitureDepthBaselineRectExtensions
    {
        public static RectInt ToRectInt(this Rect value)
        {
            int xMin = Mathf.FloorToInt(value.xMin);
            int yMin = Mathf.FloorToInt(value.yMin);
            int xMax = Mathf.CeilToInt(value.xMax);
            int yMax = Mathf.CeilToInt(value.yMax);
            return new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
        }
    }
}
