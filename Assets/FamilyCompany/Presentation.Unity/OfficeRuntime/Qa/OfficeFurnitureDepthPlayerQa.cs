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
    /// Windows-player proof for continuous actor/furniture depth in the Starter Office.
    ///
    /// The runner never changes production locomotion, animation, layout, wall, doorway, or
    /// attendance code. It freezes the live runtime only inside the disposable QA player, places
    /// actors at positions derived from a furniture footprint, temporarily selects authentic
    /// walking frames through DirectionalSpriteAnimator.GetFrame, reapplies the production depth
    /// sorter, and measures four rendered passes in memory. Only the normal overview and its exact
    /// two-times crop are written as PNG evidence.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OfficeFurnitureDepthPlayerQa : MonoBehaviour
    {
        public const string CommandLineFlag = "-familyCompanyFurnitureDepthQa";
        public const string ArtifactDirectoryArgument = "-familyCompanyFurnitureDepthQaArtifacts";

        private static readonly string[] MemberIds =
            { "player", "older_sister", "father", "mother" };

        private static readonly string[] TargetKindIds =
        {
            OfficeGridLayouts.DeskWithPcKind,
            OfficeGridLayouts.DocumentBookcaseKind,
            OfficeGridLayouts.FaxCopierKind,
            OfficeGridLayouts.WaterDispenserKind,
            OfficeGridLayouts.SofaKind,
            OfficeGridLayouts.PottedPlantKind
        };

        private static readonly string[] DirectionTokens =
        {
            "south", "southwest", "west", "northwest",
            "north", "northeast", "east", "southeast"
        };

        private static readonly DepthProbeRelation[] Relations =
        {
            DepthProbeRelation.Front,
            DepthProbeRelation.ExactNearEdge,
            DepthProbeRelation.Behind
        };

        private const int OverviewWidth = 1920;
        private const int OverviewHeight = 1080;
        private const int ProbeSize = 512;
        private const int DifferenceThreshold = 6;
        private const float OpaqueCoreResidualRatio = 0.05f;
        private const int LateralProbeInsetQ = 1;
        // One-sixteenth-cell samples are still deterministic, but do not miss the narrow common
        // screen-overlap band of side-facing family silhouettes against one-cell props.
        private const int LateralProbeStepQ = OfficeHybridContinuousDepth.Quantization / 16;
        private const int RequiredMatrixCases = 4 * 6 * 8 * 3;
        private const int RequiredDirectionCases = 4 * 8;
        private const int RequiredRelationTransitions = 4 * 6 * 8;
        private const int RequiredSavedCasePng = 100;

        private static OfficeFurnitureDepthPlayerQa _instance;

        private readonly List<ActorRestoreState> _actorStates = new List<ActorRestoreState>();
        private readonly List<DepthCaseRecord> _records = new List<DepthCaseRecord>();
        private readonly HashSet<string> _caseKeys = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _directionKeys = new HashSet<string>(StringComparer.Ordinal);

        private StarterOfficeRuntimeBootstrap _runtime;
        private Camera _sourceCamera;
        private string _artifactDirectory = string.Empty;
        private string _failure = string.Empty;
        private int _failureCode;
        private bool _runtimeStateCaptured;
        private bool _worldWasEnabled;
        private float _previousTimeScale = 1f;
        private float _previousCaptureDeltaTime;
        private string _overviewPath = string.Empty;
        private string _cropPath = string.Empty;
        private int _savedCasePngCount;
        private int _adjustedLateralProbeGroupCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInstall()
        {
            if (_instance != null || !HasArgument(CommandLineFlag)) return;
            var host = new GameObject("~OfficeFurnitureDepthPlayerQa");
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<OfficeFurnitureDepthPlayerQa>();
        }

        private void Start()
        {
            _artifactDirectory = ResolveArtifactDirectory();
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
                    Fail(99, "Unhandled " + exception.GetType().Name + ": " + exception.Message);
                    RestoreRuntimeState();
                    Finish(false);
                    yield break;
                }
                yield return yielded;
            }
        }

        private IEnumerator Run()
        {
            Debug.Log(
                "FAMILY_COMPANY_FURNITURE_DEPTH_QA: START | flag=" + CommandLineFlag +
                " | artifacts=" + _artifactDirectory);

            _previousTimeScale = Time.timeScale;
            _previousCaptureDeltaTime = Time.captureDeltaTime;
            Time.timeScale = 1f;
            Time.captureDeltaTime = 1f / 60f;

            yield return null;
            PrototypeBootstrap bootstrap = Object.FindFirstObjectByType<PrototypeBootstrap>();
            if (bootstrap == null)
            {
                Fail(91, "PrototypeBootstrap is missing.");
                RestoreRuntimeState();
                Finish(false);
                yield break;
            }

            // Public loading flow only; this runner never edits or owns ScenePreviewJump.
            ScenePreviewJump.ShowStarterOffice();
            bootstrap.StartNewGameNow(1, false);
            ScenePreviewJump.ShowStarterOffice();

            float deadline = Time.realtimeSinceStartup + 30f;
            while (Time.realtimeSinceStartup < deadline)
            {
                _runtime = Object.FindFirstObjectByType<StarterOfficeRuntimeBootstrap>();
                if (_runtime != null && _runtime.IsReady && _runtime.World != null &&
                    _runtime.Actors.Count == MemberIds.Length && Camera.main != null) break;
                yield return null;
            }

            if (_runtime == null || !_runtime.IsReady || _runtime.World == null ||
                _runtime.Actors.Count != MemberIds.Length || Camera.main == null)
            {
                Fail(92, "Starter Office runtime/camera did not become ready with four actors.");
                RestoreRuntimeState();
                Finish(false);
                yield break;
            }

            _sourceCamera = Camera.main;
            Dictionary<string, OfficeRuntimeAgent> actors = _runtime.Actors
                .Where(actor => actor != null)
                .ToDictionary(actor => actor.AgentId, actor => actor, StringComparer.Ordinal);
            if (MemberIds.Any(memberId => !actors.ContainsKey(memberId)))
            {
                Fail(92, "Canonical family actor set is incomplete.");
                RestoreRuntimeState();
                Finish(false);
                yield break;
            }

            Dictionary<string, TargetFurniture> targets = ResolveTargets();
            if (TargetKindIds.Any(kindId => !targets.ContainsKey(kindId)))
            {
                Fail(93, "One or more requested real furniture kinds are missing.");
                RestoreRuntimeState();
                Finish(false);
                yield break;
            }

            CaptureRuntimeState(actors);
            _runtime.World.enabled = false;

            yield return CaptureOverviewEvidence(actors, targets);
            if (_failure.Length == 0)
                yield return RunMatrix(actors, targets);

            bool pass = _failure.Length == 0 && ValidateCoverage();
            RestoreRuntimeState();
            Finish(pass);
        }

        private IEnumerator CaptureOverviewEvidence(
            IReadOnlyDictionary<string, OfficeRuntimeAgent> actors,
            IReadOnlyDictionary<string, TargetFurniture> targets)
        {
            string[] overviewKinds =
            {
                OfficeGridLayouts.DocumentBookcaseKind,
                OfficeGridLayouts.DeskWithPcKind,
                OfficeGridLayouts.FaxCopierKind,
                OfficeGridLayouts.SofaKind
            };
            int[] overviewDirections = { 0, 3, 5, 7 };
            for (int index = 0; index < MemberIds.Length; index++)
            {
                OfficeRuntimeAgent actor = actors[MemberIds[index]];
                TargetFurniture target = targets[overviewKinds[index]];
                SetActorProbePosition(
                    actor,
                    target.Furniture,
                    DepthProbeRelation.ExactNearEdge,
                    target.Furniture.Origin.X + (target.Furniture.Width - 1) * 0.5f);
                DirectionalSpriteAnimator animator = actor.GetComponent<DirectionalSpriteAnimator>();
                Sprite frame = animator == null ? null : animator.GetFrame(overviewDirections[index], 0);
                if (frame == null)
                {
                    Fail(94, "Authentic overview frame is missing for " + MemberIds[index] + ".");
                    yield break;
                }
                actor.PresentationRenderer.sprite = frame;
                actor.PresentationRenderer.enabled = true;
            }
            _runtime.World.DepthSorter.Apply(_runtime.Actors);
            yield return new WaitForEndOfFrame();

            _overviewPath = ArtifactPath("furniture-depth-after-overview-1920x1080.png");
            CapturedFrame overview = CaptureCamera(
                _sourceCamera,
                OverviewWidth,
                OverviewHeight,
                _overviewPath,
                null,
                -1);
            OfficeRuntimeAgent focusActor = actors["older_sister"];
            TargetFurniture focusTarget = targets[OfficeGridLayouts.DocumentBookcaseKind];
            Bounds focusBounds = focusActor.PresentationRenderer.bounds;
            foreach (SpriteRenderer renderer in focusTarget.Renderers)
                if (renderer != null && renderer.enabled) focusBounds.Encapsulate(renderer.bounds);
            RectInt crop = ExpandAndClamp(
                ProjectBounds(_sourceCamera, focusBounds, overview.Width, overview.Height),
                overview.Width,
                overview.Height,
                24);
            _cropPath = ArtifactPath("furniture-depth-after-crop-200pct.png");
            SaveNearestTwoTimesCrop(overview, crop, _cropPath);
        }

        private IEnumerator RunMatrix(
            IReadOnlyDictionary<string, OfficeRuntimeAgent> actors,
            IReadOnlyDictionary<string, TargetFurniture> targets)
        {
            foreach (OfficeRuntimeAgent actor in actors.Values)
                actor.PresentationRenderer.enabled = false;

            foreach (string memberId in MemberIds)
            {
                OfficeRuntimeAgent actor = actors[memberId];
                DirectionalSpriteAnimator animator = actor.GetComponent<DirectionalSpriteAnimator>();
                if (animator == null)
                {
                    Fail(94, memberId + " has no DirectionalSpriteAnimator.");
                    yield break;
                }

                foreach (string kindId in TargetKindIds)
                {
                    TargetFurniture target = targets[kindId];
                    for (int direction = 0; direction < DirectionTokens.Length; direction++)
                    {
                        if (!TryResolveCommonLateralProbeX(
                                actor,
                                target,
                                animator,
                                direction,
                                out float commonProbeGridX,
                                out string lateralFailure))
                        {
                            Fail(
                                95,
                                $"{memberId}/{kindId}/{DirectionTokens[direction]} has no common " +
                                "footprint-contact lateral probe: " + lateralFailure);
                            yield break;
                        }

                        int previousSign = 0;
                        int signTransitions = 0;
                        int reversals = 0;
                        for (int relationIndex = 0; relationIndex < Relations.Length; relationIndex++)
                        {
                            DepthProbeRelation relation = Relations[relationIndex];
                            int walkFrame = (direction + relationIndex) % DirectionalSpriteAnimator.WalkFrameCount;
                            Sprite frame = animator.GetFrame(direction, walkFrame);
                            if (frame == null)
                            {
                                Fail(94, memberId + " missing authentic " + DirectionTokens[direction] +
                                         " walk frame " + walkFrame + ".");
                                yield break;
                            }
                            int parsedDirection = ParseSpriteDirection(frame.name);
                            if (parsedDirection != direction)
                            {
                                Fail(
                                    94,
                                    $"{memberId} authentic sprite direction mismatch: expected=" +
                                    $"{direction} parsed={parsedDirection} sprite={frame.name}.");
                                yield break;
                            }

                            Vector2 probeGridPosition = SetActorProbePosition(
                                actor,
                                target.Furniture,
                                relation,
                                commonProbeGridX);
                            SpriteRenderer actorRenderer = actor.PresentationRenderer;
                            actorRenderer.sprite = frame;
                            actorRenderer.enabled = true;
                            _runtime.World.DepthSorter.Apply(_runtime.Actors);

                            int targetMinimumOrder = target.Renderers
                                .Where(renderer => renderer != null && renderer.enabled)
                                .Min(renderer => renderer.sortingOrder);
                            int targetMaximumOrder = target.Renderers
                                .Where(renderer => renderer != null && renderer.enabled)
                                .Max(renderer => renderer.sortingOrder);
                            int sign = actorRenderer.sortingOrder > targetMaximumOrder
                                ? 1
                                : actorRenderer.sortingOrder < targetMinimumOrder
                                    ? -1
                                    : 0;
                            int stableActorOrder = actorRenderer.sortingOrder;
                            int stableTargetMinimumOrder = targetMinimumOrder;
                            int stableTargetMaximumOrder = targetMaximumOrder;
                            bool stableFlipX = actorRenderer.flipX;
                            bool stableFlipY = actorRenderer.flipY;
                            Sprite stableSprite = actorRenderer.sprite;
                            int stableFrameFlipCount = 0;
                            int stabilityFrameCount = relation == DepthProbeRelation.ExactNearEdge ? 3 : 1;
                            for (int stabilitySample = 0;
                                 stabilitySample < stabilityFrameCount;
                                 stabilitySample++)
                            {
                                yield return new WaitForEndOfFrame();
                                _runtime.World.DepthSorter.Apply(_runtime.Actors);
                                int sampledTargetMinimum = target.Renderers
                                    .Where(renderer => renderer != null && renderer.enabled)
                                    .Min(renderer => renderer.sortingOrder);
                                int sampledTargetMaximum = target.Renderers
                                    .Where(renderer => renderer != null && renderer.enabled)
                                    .Max(renderer => renderer.sortingOrder);
                                if (actorRenderer.sortingOrder != stableActorOrder ||
                                    sampledTargetMinimum != stableTargetMinimumOrder ||
                                    sampledTargetMaximum != stableTargetMaximumOrder ||
                                    actorRenderer.flipX != stableFlipX ||
                                    actorRenderer.flipY != stableFlipY ||
                                    actorRenderer.sprite != stableSprite)
                                    stableFrameFlipCount++;
                            }
                            if (previousSign != 0 && sign != previousSign)
                            {
                                signTransitions++;
                                if (previousSign < sign) reversals++;
                            }
                            previousSign = sign;

                            bool saveNormalPng = direction == 3 ||
                                                 (string.Equals(
                                                      kindId,
                                                      OfficeGridLayouts.DeskWithPcKind,
                                                      StringComparison.Ordinal) &&
                                                  relation == DepthProbeRelation.ExactNearEdge);
                            string normalPngPath = saveNormalPng
                                ? ArtifactPath(
                                    "furniture-depth-" + memberId + "-" + kindId + "-" +
                                    DirectionTokens[direction] + "-" + relation.ToString().ToLowerInvariant() +
                                    "-normal-512.png")
                                : string.Empty;
                            ProbeCapture capture = CaptureFourWay(
                                actorRenderer,
                                target.Renderers,
                                normalPngPath);
                            if (saveNormalPng) _savedCasePngCount++;
                            ProbeMetrics metrics = MeasureProbe(capture);
                            string key = BuildCaseKey(memberId, kindId, direction, relation);
                            if (!_caseKeys.Add(key))
                            {
                                Fail(95, "Duplicate matrix case: " + key);
                                yield break;
                            }
                            _directionKeys.Add(memberId + ":" + direction);

                            var record = new DepthCaseRecord(
                                memberId,
                                kindId,
                                direction,
                                walkFrame,
                                relation,
                                frame.name,
                                actorRenderer.sortingOrder,
                                targetMinimumOrder,
                                targetMaximumOrder,
                                sign,
                                probeGridPosition,
                                stabilityFrameCount,
                                stableFrameFlipCount,
                                normalPngPath,
                                actorRenderer.flipX,
                                actorRenderer.flipY,
                                metrics);
                            _records.Add(record);
                            if (!ValidateCase(record, out string caseFailure))
                            {
                                Fail(95, key + ": " + caseFailure);
                                yield break;
                            }
                            actorRenderer.enabled = false;
                        }

                        if (signTransitions != 1 || reversals != 0)
                        {
                            Fail(
                                95,
                                $"{memberId}/{kindId}/{DirectionTokens[direction]} relation order " +
                                $"transition={signTransitions}, reversal={reversals}; expected 1/0.");
                            yield break;
                        }
                    }
                }
            }
        }

        private bool ValidateCase(DepthCaseRecord record, out string failure)
        {
            ProbeMetrics metrics = record.Metrics;
            if (metrics.ActorPixels <= 0)
            {
                failure = "actor-only delta is empty";
                return false;
            }
            if (metrics.FaceActorCorePixels <= 0 ||
                metrics.HeadActorCorePixels <= 0 ||
                metrics.TorsoActorCorePixels <= 0)
            {
                failure = $"protected screen segmentation is empty: face={metrics.FaceActorCorePixels} " +
                          $"head={metrics.HeadActorCorePixels} torso={metrics.TorsoActorCorePixels}";
                return false;
            }
            if (metrics.TargetPixels <= 0)
            {
                failure = "target-only delta is empty";
                return false;
            }
            if (metrics.OverlapCandidates <= 0)
            {
                failure = "actor/target geometric overlap candidate count is zero";
                return false;
            }
            if (record.FlipX || record.FlipY)
            {
                failure = "authored 8-direction actor unexpectedly uses a renderer flip";
                return false;
            }
            if (record.StableFrameFlipCount != 0)
            {
                failure = "fixed-position consecutive rendered frames changed sprite/flip/sorting ownership";
                return false;
            }
            if (record.Relation == DepthProbeRelation.ExactNearEdge &&
                record.StableFrameCount < 3)
            {
                failure = "exact-near-edge has fewer than three consecutive rendered stability samples";
                return false;
            }

            if (record.Relation == DepthProbeRelation.Behind)
            {
                if (record.OrderSign >= 0)
                {
                    failure = "behind actor is not ordered below the complete target";
                    return false;
                }
                if (metrics.OpaqueActorOccludedPixels <= 0)
                {
                    failure = "behind relation has no positive opaque actor occlusion";
                    return false;
                }
                failure = string.Empty;
                return true;
            }

            if (record.OrderSign <= 0)
            {
                failure = "front/exact-near-edge actor is not ordered above the complete target";
                return false;
            }
            if (metrics.InvalidActorEffectPixels != 0 ||
                metrics.InvalidFaceEffectPixels != 0 ||
                metrics.InvalidHeadEffectPixels != 0 ||
                metrics.InvalidTorsoEffectPixels != 0)
            {
                failure = $"unseated protected overlap is non-zero: all={metrics.InvalidActorEffectPixels} " +
                          $"face={metrics.InvalidFaceEffectPixels} head={metrics.InvalidHeadEffectPixels} " +
                          $"torso={metrics.InvalidTorsoEffectPixels}";
                return false;
            }
            failure = string.Empty;
            return true;
        }

        private bool ValidateCoverage()
        {
            if (_records.Count != RequiredMatrixCases || _caseKeys.Count != RequiredMatrixCases)
            {
                Fail(
                    96,
                    $"Matrix coverage is {_records.Count}/{_caseKeys.Count}; expected {RequiredMatrixCases}.");
                return false;
            }
            if (_directionKeys.Count != RequiredDirectionCases)
            {
                Fail(
                    96,
                    $"Member/direction coverage is {_directionKeys.Count}; expected {RequiredDirectionCases}.");
                return false;
            }
            if (_savedCasePngCount != RequiredSavedCasePng)
            {
                Fail(
                    96,
                    $"Saved normal case PNG coverage is {_savedCasePngCount}; " +
                    $"expected {RequiredSavedCasePng} unique NW plus desk-edge cases.");
                return false;
            }
            int transitionGroups = _records.Count / Relations.Length;
            if (transitionGroups != RequiredRelationTransitions)
            {
                Fail(96, $"Relation transition coverage is {transitionGroups}; expected {RequiredRelationTransitions}.");
                return false;
            }
            return true;
        }

        private Dictionary<string, TargetFurniture> ResolveTargets()
        {
            var result = new Dictionary<string, TargetFurniture>(StringComparer.Ordinal);
            foreach (string kindId in TargetKindIds)
            {
                PlacedOfficeFurniture furniture = _runtime.World.Grid.Furniture
                    .Where(item => string.Equals(item.KindId, kindId, StringComparison.Ordinal))
                    .OrderBy(item => item.FurnitureId, StringComparer.Ordinal)
                    .FirstOrDefault();
                if (furniture == null) continue;
                var renderers = new List<SpriteRenderer>();
                if (_runtime.World.FurniturePresenter.TryGetRenderer(
                        furniture.FurnitureId,
                        out SpriteRenderer baseRenderer) && baseRenderer != null)
                    renderers.Add(baseRenderer);
                if (_runtime.World.FurniturePresenter.FrontOverlayRenderers.TryGetValue(
                        furniture.FurnitureId,
                        out SpriteRenderer frontRenderer) && frontRenderer != null)
                    renderers.Add(frontRenderer);
                if (renderers.Count > 0)
                    result.Add(kindId, new TargetFurniture(furniture, renderers.ToArray()));
            }
            return result;
        }

        private Vector2 SetActorProbePosition(
            OfficeRuntimeAgent actor,
            PlacedOfficeFurniture furniture,
            DepthProbeRelation relation,
            float gridX)
        {
            float gridY = relation switch
            {
                DepthProbeRelation.Front => furniture.Origin.Y - 0.75f,
                DepthProbeRelation.ExactNearEdge => furniture.Origin.Y - 0.5f,
                DepthProbeRelation.Behind => furniture.Origin.Y + furniture.Height - 1 + 0.75f,
                _ => throw new ArgumentOutOfRangeException(nameof(relation))
            };
            Vector3 position = GridPositionWorld(gridX, gridY);
            actor.transform.position = new Vector3(position.x, position.y, actor.transform.position.z);
            return new Vector2(gridX, gridY);
        }

        private bool TryResolveCommonLateralProbeX(
            OfficeRuntimeAgent actor,
            TargetFurniture target,
            DirectionalSpriteAnimator animator,
            int direction,
            out float gridX,
            out string failure)
        {
            List<int> candidateXQ = BuildLateralProbeCandidateXQ(target.Furniture);
            int centerXQ = candidateXQ[0];
            if (TryEvaluateLateralProbeCandidate(
                    actor,
                    target,
                    animator,
                    direction,
                    centerXQ,
                    out _,
                    out _))
            {
                gridX = centerXQ / (float)OfficeHybridContinuousDepth.Quantization;
                failure = string.Empty;
                return true;
            }

            bool found = false;
            int bestXQ = centerXQ;
            int bestScore = int.MinValue;
            string lastFailure = string.Empty;
            for (int index = 1; index < candidateXQ.Count; index++)
            {
                int candidate = candidateXQ[index];
                if (!TryEvaluateLateralProbeCandidate(
                        actor,
                        target,
                        animator,
                        direction,
                        candidate,
                        out int score,
                        out string candidateFailure))
                {
                    lastFailure = candidateFailure;
                    continue;
                }

                int candidateDistance = Math.Abs(candidate - centerXQ);
                int bestDistance = Math.Abs(bestXQ - centerXQ);
                if (!found || score > bestScore ||
                    (score == bestScore && candidateDistance < bestDistance) ||
                    (score == bestScore && candidateDistance == bestDistance && candidate < bestXQ))
                {
                    found = true;
                    bestXQ = candidate;
                    bestScore = score;
                }
            }

            if (!found)
            {
                gridX = centerXQ / (float)OfficeHybridContinuousDepth.Quantization;
                failure = "tested " + candidateXQ.Count + " Q16/inset-Q1 candidates; " + lastFailure;
                return false;
            }

            _adjustedLateralProbeGroupCount++;
            gridX = bestXQ / (float)OfficeHybridContinuousDepth.Quantization;
            failure = string.Empty;
            return true;
        }

        private bool TryEvaluateLateralProbeCandidate(
            OfficeRuntimeAgent actor,
            TargetFurniture target,
            DirectionalSpriteAnimator animator,
            int direction,
            int candidateXQ,
            out int score,
            out string failure)
        {
            SpriteRenderer actorRenderer = actor.PresentationRenderer;
            score = int.MaxValue;
            for (int relationIndex = 0; relationIndex < Relations.Length; relationIndex++)
            {
                DepthProbeRelation relation = Relations[relationIndex];
                int walkFrame = (direction + relationIndex) % DirectionalSpriteAnimator.WalkFrameCount;
                Sprite frame = animator.GetFrame(direction, walkFrame);
                if (frame == null)
                {
                    failure = "authentic frame unavailable during lateral preflight";
                    return false;
                }

                actorRenderer.sprite = frame;
                actorRenderer.enabled = true;
                SetActorProbePosition(
                    actor,
                    target.Furniture,
                    relation,
                    candidateXQ / (float)OfficeHybridContinuousDepth.Quantization);
                _runtime.World.DepthSorter.Apply(_runtime.Actors);

                int targetMinimumOrder = target.Renderers
                    .Where(renderer => renderer != null && renderer.enabled)
                    .Min(renderer => renderer.sortingOrder);
                int targetMaximumOrder = target.Renderers
                    .Where(renderer => renderer != null && renderer.enabled)
                    .Max(renderer => renderer.sortingOrder);
                ProbeMetrics metrics = MeasureProbe(CaptureFourWay(
                    actorRenderer,
                    target.Renderers,
                    string.Empty));
                if (metrics.OverlapCandidates <= 0)
                {
                    failure = relation + " overlapCandidates=0 at xQ=" + candidateXQ;
                    return false;
                }

                if (relation == DepthProbeRelation.Behind)
                {
                    if (actorRenderer.sortingOrder >= targetMinimumOrder ||
                        metrics.OpaqueActorOccludedPixels <= 0)
                    {
                        failure = relation + " lacks behind order/opaque occlusion at xQ=" + candidateXQ;
                        return false;
                    }
                    score = Math.Min(
                        score,
                        Math.Min(metrics.OverlapCandidates, metrics.OpaqueActorOccludedPixels));
                    continue;
                }

                if (actorRenderer.sortingOrder <= targetMaximumOrder ||
                    metrics.InvalidActorEffectPixels != 0)
                {
                    failure = relation + " lacks front order/zero actor effect at xQ=" + candidateXQ;
                    return false;
                }
                score = Math.Min(score, metrics.OverlapCandidates);
            }

            failure = string.Empty;
            return true;
        }

        private static List<int> BuildLateralProbeCandidateXQ(PlacedOfficeFurniture furniture)
        {
            int quantization = OfficeHybridContinuousDepth.Quantization;
            int minimumXQ = checked(
                furniture.Origin.X * quantization - OfficeHybridContinuousDepth.HalfCellQ +
                LateralProbeInsetQ);
            int maximumXQ = checked(
                (furniture.Origin.X + furniture.Width - 1) * quantization +
                OfficeHybridContinuousDepth.HalfCellQ - LateralProbeInsetQ);
            int centerXQ = checked(
                furniture.Origin.X * quantization +
                (furniture.Width - 1) * OfficeHybridContinuousDepth.HalfCellQ);
            var result = new List<int> { centerXQ };
            int maximumOffset = Math.Max(centerXQ - minimumXQ, maximumXQ - centerXQ);
            for (int offset = LateralProbeStepQ; offset < maximumOffset; offset += LateralProbeStepQ)
            {
                int lower = centerXQ - offset;
                int upper = centerXQ + offset;
                if (lower >= minimumXQ) result.Add(lower);
                if (upper <= maximumXQ) result.Add(upper);
            }
            if (!result.Contains(minimumXQ)) result.Add(minimumXQ);
            if (!result.Contains(maximumXQ)) result.Add(maximumXQ);
            return result;
        }

        private Vector3 GridPositionWorld(float gridX, float gridY)
        {
            OfficeGridTilemapPresenter presenter = _runtime.World.Presenter;
            Vector3 origin = presenter.CellCenterWorld(new OfficeGridCoordinate(0, 0));
            Vector3 basisX = presenter.CellCenterWorld(new OfficeGridCoordinate(1, 0)) - origin;
            Vector3 basisY = presenter.CellCenterWorld(new OfficeGridCoordinate(0, 1)) - origin;
            return origin + basisX * gridX + basisY * gridY;
        }

        private ProbeCapture CaptureFourWay(
            SpriteRenderer actorRenderer,
            IReadOnlyList<SpriteRenderer> targetRenderers,
            string normalPngPath)
        {
            if (actorRenderer == null || targetRenderers == null || targetRenderers.Count == 0)
                throw new ArgumentException("Four-way probe renderers are incomplete.");

            Bounds focus = actorRenderer.bounds;
            foreach (SpriteRenderer renderer in targetRenderers)
                if (renderer != null && renderer.enabled) focus.Encapsulate(renderer.bounds);
            focus.Expand(new Vector3(0.20f, 0.20f, 0f));

            bool actorEnabled = actorRenderer.enabled;
            int actorLayer = actorRenderer.gameObject.layer;
            var targetEnabled = new bool[targetRenderers.Count];
            var targetLayers = new int[targetRenderers.Count];
            for (int index = 0; index < targetRenderers.Count; index++)
            {
                targetEnabled[index] = targetRenderers[index] != null && targetRenderers[index].enabled;
                targetLayers[index] = targetRenderers[index] == null
                    ? 0
                    : targetRenderers[index].gameObject.layer;
            }

            const int probeLayer = 31;
            actorRenderer.gameObject.layer = probeLayer;
            for (int index = 0; index < targetRenderers.Count; index++)
                if (targetRenderers[index] != null) targetRenderers[index].gameObject.layer = probeLayer;

            try
            {
                actorRenderer.enabled = true;
                SetEnabled(targetRenderers, targetEnabled);
                CapturedFrame normal = CaptureCamera(
                    _sourceCamera, ProbeSize, ProbeSize, normalPngPath, focus, 1 << probeLayer);

                SetEnabled(targetRenderers, false);
                CapturedFrame actorOnly = CaptureCamera(
                    _sourceCamera, ProbeSize, ProbeSize, string.Empty, focus, 1 << probeLayer);

                actorRenderer.enabled = false;
                CapturedFrame background = CaptureCamera(
                    _sourceCamera, ProbeSize, ProbeSize, string.Empty, focus, 1 << probeLayer);

                SetEnabled(targetRenderers, targetEnabled);
                CapturedFrame targetOnly = CaptureCamera(
                    _sourceCamera, ProbeSize, ProbeSize, string.Empty, focus, 1 << probeLayer);
                return new ProbeCapture(normal, actorOnly, targetOnly, background);
            }
            finally
            {
                actorRenderer.enabled = actorEnabled;
                actorRenderer.gameObject.layer = actorLayer;
                SetEnabled(targetRenderers, targetEnabled);
                for (int index = 0; index < targetRenderers.Count; index++)
                    if (targetRenderers[index] != null)
                        targetRenderers[index].gameObject.layer = targetLayers[index];
            }
        }

        private static ProbeMetrics MeasureProbe(ProbeCapture capture)
        {
            if (!capture.Normal.IsCompatible(capture.ActorOnly) ||
                !capture.Normal.IsCompatible(capture.TargetOnly) ||
                !capture.Normal.IsCompatible(capture.Background))
                throw new InvalidOperationException("Four-way probe capture sizes differ.");

            int width = capture.Normal.Width;
            int height = capture.Normal.Height;
            var actorMask = new bool[width * height];
            var targetMask = new bool[width * height];
            int minX = width;
            int minY = height;
            int maxX = -1;
            int maxY = -1;
            int actorPixels = 0;
            int targetPixels = 0;
            for (int index = 0; index < actorMask.Length; index++)
            {
                actorMask[index] = Different(
                    capture.ActorOnly.Pixels[index],
                    capture.Background.Pixels[index]);
                targetMask[index] = Different(
                    capture.TargetOnly.Pixels[index],
                    capture.Background.Pixels[index]);
                if (actorMask[index])
                {
                    actorPixels++;
                    int x = index % width;
                    int y = index / width;
                    minX = Math.Min(minX, x);
                    minY = Math.Min(minY, y);
                    maxX = Math.Max(maxX, x);
                    maxY = Math.Max(maxY, y);
                }
                if (targetMask[index]) targetPixels++;
            }

            if (actorPixels == 0)
                return new ProbeMetrics(
                    0, targetPixels, 0,
                    0, 0, 0,
                    0, 0, 0,
                    0, 0, 0, 0,
                    0, 0);

            int candidates = 0;
            int faceCorePixels = 0;
            int headCorePixels = 0;
            int torsoCorePixels = 0;
            int faceCandidates = 0;
            int headCandidates = 0;
            int torsoCandidates = 0;
            int invalidAll = 0;
            int invalidFace = 0;
            int invalidHead = 0;
            int invalidTorso = 0;
            int occluded = 0;
            int antiAliasResidual = 0;
            float actorHeight = Mathf.Max(1f, maxY - minY + 1);
            float actorWidth = Mathf.Max(1f, maxX - minX + 1);
            for (int y = 1; y < height - 1; y++)
            for (int x = 1; x < width - 1; x++)
            {
                int index = y * width + x;
                if (!actorMask[index]) continue;
                bool actorCore = IsMaskCore(actorMask, x, y, width);
                if (!actorCore)
                {
                    if (targetMask[index] &&
                        Different(capture.Normal.Pixels[index], capture.ActorOnly.Pixels[index]))
                        antiAliasResidual++;
                    continue;
                }

                float vertical01 = (y + 0.5f - minY) / actorHeight;
                float horizontal01 = (x + 0.5f - minX) / actorWidth;
                bool inHead = vertical01 >= 0.70f;
                bool inFace = inHead && vertical01 <= 0.94f &&
                              horizontal01 >= 0.20f && horizontal01 <= 0.80f;
                bool inTorso = vertical01 >= 0.34f && vertical01 < 0.70f;
                if (inHead) headCorePixels++;
                if (inFace) faceCorePixels++;
                if (inTorso) torsoCorePixels++;
                if (!targetMask[index]) continue;

                bool targetCore = IsMaskCore(targetMask, x, y, width);
                candidates++;
                if (inHead) headCandidates++;
                if (inFace) faceCandidates++;
                if (inTorso) torsoCandidates++;
                bool affected = Different(
                    capture.Normal.Pixels[index],
                    capture.ActorOnly.Pixels[index]);
                if (affected)
                {
                    invalidAll++;
                    if (inHead)
                    {
                        invalidHead++;
                        if (inFace) invalidFace++;
                    }
                    else if (inTorso)
                    {
                        invalidTorso++;
                    }
                }

                if (targetCore && ActorResidualRatio(capture, index) <= OpaqueCoreResidualRatio)
                    occluded++;
            }

            return new ProbeMetrics(
                actorPixels,
                targetPixels,
                candidates,
                faceCorePixels,
                headCorePixels,
                torsoCorePixels,
                faceCandidates,
                headCandidates,
                torsoCandidates,
                invalidAll,
                invalidFace,
                invalidHead,
                invalidTorso,
                occluded,
                antiAliasResidual);
        }

        private static float ActorResidualRatio(ProbeCapture capture, int index)
        {
            Color32 actorDeltaA = capture.ActorOnly.Pixels[index];
            Color32 actorDeltaB = capture.Background.Pixels[index];
            Color32 residualA = capture.Normal.Pixels[index];
            Color32 residualB = capture.TargetOnly.Pixels[index];
            int denominator = ColorDistance(actorDeltaA, actorDeltaB);
            if (denominator <= 0) return 1f;
            return ColorDistance(residualA, residualB) / (float)denominator;
        }

        private static bool IsMaskCore(bool[] mask, int x, int y, int width)
        {
            int center = y * width + x;
            for (int offsetY = -1; offsetY <= 1; offsetY++)
            for (int offsetX = -1; offsetX <= 1; offsetX++)
                if (!mask[center + offsetY * width + offsetX]) return false;
            return true;
        }

        private static bool Different(Color32 left, Color32 right) =>
            ColorDistance(left, right) >= DifferenceThreshold;

        private static int ColorDistance(Color32 left, Color32 right) =>
            Math.Abs(left.r - right.r) + Math.Abs(left.g - right.g) +
            Math.Abs(left.b - right.b) + Math.Abs(left.a - right.a);

        private void CaptureRuntimeState(IReadOnlyDictionary<string, OfficeRuntimeAgent> actors)
        {
            _worldWasEnabled = _runtime.World.enabled;
            _actorStates.Clear();
            foreach (string memberId in MemberIds)
                _actorStates.Add(new ActorRestoreState(actors[memberId]));
            _runtimeStateCaptured = true;
        }

        private void RestoreRuntimeState()
        {
            if (_runtimeStateCaptured)
            {
                foreach (ActorRestoreState state in _actorStates) state.Restore();
                if (_runtime != null && _runtime.World != null)
                {
                    _runtime.World.enabled = _worldWasEnabled;
                    if (_worldWasEnabled) _runtime.World.DepthSorter.Apply(_runtime.Actors);
                }
                _runtimeStateCaptured = false;
            }
            Time.timeScale = _previousTimeScale;
            Time.captureDeltaTime = _previousCaptureDeltaTime;
        }

        private void OnDestroy()
        {
            RestoreRuntimeState();
        }

        private void Fail(int code, string message)
        {
            if (_failure.Length > 0) return;
            _failureCode = code;
            _failure = message ?? string.Empty;
        }

        private void Finish(bool pass)
        {
            string marker = pass
                ? "FAMILY_COMPANY_FURNITURE_DEPTH_QA: PASS"
                : "FAMILY_COMPANY_FURNITURE_DEPTH_QA: FAIL";
            string result = BuildResult(marker, pass);
            try
            {
                File.WriteAllText(ArtifactPath("furniture-depth-qa-result.txt"), result);
                File.WriteAllText(ArtifactPath("furniture-depth-qa-manifest.tsv"), BuildManifest());
            }
            catch (Exception exception)
            {
                Debug.LogError("Furniture depth QA artifact write failed: " + exception.Message);
                if (pass)
                {
                    pass = false;
                    _failureCode = 97;
                    _failure = "Artifact write failed: " + exception.Message;
                    result = BuildResult("FAMILY_COMPANY_FURNITURE_DEPTH_QA: FAIL", false);
                }
            }

            if (pass) Debug.Log(result.TrimEnd());
            else Debug.LogError(result.TrimEnd());
            Application.Quit(pass ? 0 : (_failureCode == 0 ? 98 : _failureCode));
        }

        private string BuildResult(string marker, bool pass)
        {
            DepthCaseRecord[] protectedRecords = _records
                .Where(record => record.Relation != DepthProbeRelation.Behind)
                .ToArray();
            DepthCaseRecord[] behindRecords = _records
                .Where(record => record.Relation == DepthProbeRelation.Behind)
                .ToArray();
            int invalidHead = protectedRecords.Sum(record => record.Metrics.InvalidHeadEffectPixels);
            int invalidTorso = protectedRecords.Sum(record => record.Metrics.InvalidTorsoEffectPixels);
            int invalidFace = protectedRecords.Sum(record => record.Metrics.InvalidFaceEffectPixels);
            int behindEffect = behindRecords.Sum(record => record.Metrics.InvalidActorEffectPixels);
            int behindOccluded = behindRecords
                .Sum(record => record.Metrics.OpaqueActorOccludedPixels);
            int stableFrameFlipCount = _records.Sum(record => record.StableFrameFlipCount);
            return marker + Environment.NewLine +
                   "message=" + (pass ? "normal continuous furniture depth matrix passed" : _failure) + Environment.NewLine +
                   "matrixCases=" + _records.Count + "/" + RequiredMatrixCases + Environment.NewLine +
                   "family=4 directions=8 kinds=6 relations=front/exact-near-edge/behind" + Environment.NewLine +
                   "protectedFaceInvalidEffectPixels=" + invalidFace + Environment.NewLine +
                   "protectedHeadInvalidEffectPixels=" + invalidHead + Environment.NewLine +
                   "protectedTorsoInvalidEffectPixels=" + invalidTorso + Environment.NewLine +
                   "behindActorEffectPixels=" + behindEffect + Environment.NewLine +
                   "behindOpaqueActorOccludedPixels=" + behindOccluded + Environment.NewLine +
                   "stableFrameFlipCount=" + stableFrameFlipCount + Environment.NewLine +
                   "adjustedLateralProbeGroups=" + _adjustedLateralProbeGroupCount + Environment.NewLine +
                   "lateralProbeRule=common_x; physical_footprint_x_inset_q1; center_then_q16; " +
                   "maximize_min_relation_overlap" + Environment.NewLine +
                   "savedCasePng=" + _savedCasePngCount + "/" + RequiredSavedCasePng + Environment.NewLine +
                   "opaqueCoreResidualRatioMax=" + OpaqueCoreResidualRatio.ToString("F3", CultureInfo.InvariantCulture) + Environment.NewLine +
                   "opaqueCoreBasis=3x3_eroded_actor_and_target; D3D11_sRGB_residual_allowance_5_percent" + Environment.NewLine +
                   "unseatedAllowedForegroundMask=empty" + Environment.NewLine +
                   "seatedAllowedForegroundMask=owned_by_OfficeSeatingTransitionPlayerQa" + Environment.NewLine +
                   "overview=" + _overviewPath + Environment.NewLine +
                   "crop200=" + _cropPath + Environment.NewLine;
        }

        private string BuildManifest()
        {
            var builder = new StringBuilder();
            builder.AppendLine(
                "member\tkind\tdirection\twalkFrame\trelation\tsprite\tactorOrder\t" +
                "targetMinOrder\ttargetMaxOrder\torderSign\tprobeGridX\tprobeGridY\tactorPixels\ttargetPixels\t" +
                "overlapCandidates\tinvalidAll\tinvalidFace\tinvalidHead\tinvalidTorso\t" +
                "opaqueActorOccluded\taaResidual\tstableFrames\tstableFrameFlipCount\t" +
                "flipX\tflipY\tfootprint\texactNearEdgeErrorQ256\tnormalPng");
            foreach (DepthCaseRecord record in _records)
            {
                ProbeMetrics metrics = record.Metrics;
                PlacedOfficeFurniture furniture = _runtime.World.Grid.Furniture
                    .Where(item => string.Equals(item.KindId, record.KindId, StringComparison.Ordinal))
                    .OrderBy(item => item.FurnitureId, StringComparer.Ordinal)
                    .First();
                int edgeErrorQ = record.Relation == DepthProbeRelation.ExactNearEdge
                    ? Math.Abs(
                        Mathf.RoundToInt(record.ProbeGridPosition.y * 256f) -
                        Mathf.RoundToInt((furniture.Origin.Y - 0.5f) * 256f))
                    : -1;
                builder.Append(record.MemberId).Append('\t')
                    .Append(record.KindId).Append('\t')
                    .Append(DirectionTokens[record.Direction]).Append('\t')
                    .Append(record.WalkFrame).Append('\t')
                    .Append(record.Relation).Append('\t')
                    .Append(record.SpriteName).Append('\t')
                    .Append(record.ActorOrder).Append('\t')
                    .Append(record.TargetMinimumOrder).Append('\t')
                    .Append(record.TargetMaximumOrder).Append('\t')
                    .Append(record.OrderSign).Append('\t')
                    .Append(record.ProbeGridPosition.x.ToString("F4", CultureInfo.InvariantCulture)).Append('\t')
                    .Append(record.ProbeGridPosition.y.ToString("F4", CultureInfo.InvariantCulture)).Append('\t')
                    .Append(metrics.ActorPixels).Append('\t')
                    .Append(metrics.TargetPixels).Append('\t')
                    .Append(metrics.OverlapCandidates).Append('\t')
                    .Append(metrics.InvalidActorEffectPixels).Append('\t')
                    .Append(metrics.InvalidFaceEffectPixels).Append('\t')
                    .Append(metrics.InvalidHeadEffectPixels).Append('\t')
                    .Append(metrics.InvalidTorsoEffectPixels).Append('\t')
                    .Append(metrics.OpaqueActorOccludedPixels).Append('\t')
                    .Append(metrics.AntiAliasResidualPixels).Append('\t')
                    .Append(record.StableFrameCount).Append('\t')
                    .Append(record.StableFrameFlipCount).Append('\t')
                    .Append(record.FlipX ? 1 : 0).Append('\t')
                    .Append(record.FlipY ? 1 : 0).Append('\t')
                    .Append(furniture.Origin.X).Append(',')
                    .Append(furniture.Origin.Y).Append(',')
                    .Append(furniture.Width).Append('x')
                    .Append(furniture.Height).Append('\t')
                    .Append(edgeErrorQ).Append('\t')
                    .Append(record.NormalPngPath).AppendLine();
            }
            return builder.ToString();
        }

        private static CapturedFrame CaptureCamera(
            Camera source,
            int width,
            int height,
            string path,
            Bounds? focusBounds,
            int cullingMask)
        {
            var cameraObject = new GameObject("~FurnitureDepthCaptureCamera");
            Camera captureCamera = cameraObject.AddComponent<Camera>();
            captureCamera.CopyFrom(source);
            captureCamera.enabled = false;
            captureCamera.aspect = width / (float)height;
            captureCamera.transform.SetPositionAndRotation(
                source.transform.position,
                source.transform.rotation);
            if (cullingMask >= 0)
            {
                captureCamera.cullingMask = cullingMask;
                captureCamera.clearFlags = CameraClearFlags.SolidColor;
                captureCamera.backgroundColor = new Color32(174, 213, 216, 255);
            }
            if (focusBounds.HasValue)
            {
                Bounds focus = focusBounds.Value;
                captureCamera.orthographic = true;
                captureCamera.aspect = width / (float)height;
                captureCamera.orthographicSize = Mathf.Max(
                    0.5f,
                    focus.extents.y * 1.08f,
                    focus.extents.x / captureCamera.aspect * 1.08f);
                captureCamera.transform.SetPositionAndRotation(
                    new Vector3(focus.center.x, focus.center.y, source.transform.position.z),
                    source.transform.rotation);
            }

            var target = new RenderTexture(
                width,
                height,
                24,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB)
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
            int xMin = Mathf.FloorToInt(minX);
            int yMin = Mathf.FloorToInt(minY);
            int xMax = Mathf.CeilToInt(maxX);
            int yMax = Mathf.CeilToInt(maxY);
            return new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        private static RectInt ExpandAndClamp(
            RectInt value,
            int width,
            int height,
            int padding)
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

        private static string BuildCaseKey(
            string memberId,
            string kindId,
            int direction,
            DepthProbeRelation relation) =>
            memberId + ":" + kindId + ":" + direction + ":" + relation;

        private static int ParseSpriteDirection(string spriteName)
        {
            if (string.IsNullOrWhiteSpace(spriteName)) return -1;
            string padded = "_" + spriteName.ToLowerInvariant().Trim('_') + "_";
            int[] matchOrder = { 1, 3, 5, 7, 0, 2, 4, 6 };
            foreach (int direction in matchOrder)
                if (padded.Contains("_" + DirectionTokens[direction] + "_")) return direction;
            return -1;
        }

        private static bool HasArgument(string argument) =>
            Environment.GetCommandLineArgs().Any(
                value => string.Equals(value, argument, StringComparison.OrdinalIgnoreCase));

        private static string ResolveArtifactDirectory()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index < arguments.Length - 1; index++)
            {
                if (!string.Equals(
                        arguments[index],
                        ArtifactDirectoryArgument,
                        StringComparison.OrdinalIgnoreCase)) continue;
                return Path.GetFullPath(arguments[index + 1]);
            }
            return Path.Combine(Application.persistentDataPath, "FurnitureDepthQa");
        }

        private enum DepthProbeRelation
        {
            Front = 0,
            ExactNearEdge = 1,
            Behind = 2
        }

        private readonly struct TargetFurniture
        {
            public TargetFurniture(
                PlacedOfficeFurniture furniture,
                SpriteRenderer[] renderers)
            {
                Furniture = furniture;
                Renderers = renderers;
            }

            public PlacedOfficeFurniture Furniture { get; }
            public SpriteRenderer[] Renderers { get; }
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

        private readonly struct ProbeCapture
        {
            public ProbeCapture(
                CapturedFrame normal,
                CapturedFrame actorOnly,
                CapturedFrame targetOnly,
                CapturedFrame background)
            {
                Normal = normal;
                ActorOnly = actorOnly;
                TargetOnly = targetOnly;
                Background = background;
            }

            public CapturedFrame Normal { get; }
            public CapturedFrame ActorOnly { get; }
            public CapturedFrame TargetOnly { get; }
            public CapturedFrame Background { get; }
        }

        private readonly struct ProbeMetrics
        {
            public ProbeMetrics(
                int actorPixels,
                int targetPixels,
                int overlapCandidates,
                int faceActorCorePixels,
                int headActorCorePixels,
                int torsoActorCorePixels,
                int faceOverlapCandidates,
                int headOverlapCandidates,
                int torsoOverlapCandidates,
                int invalidActorEffectPixels,
                int invalidFaceEffectPixels,
                int invalidHeadEffectPixels,
                int invalidTorsoEffectPixels,
                int opaqueActorOccludedPixels,
                int antiAliasResidualPixels)
            {
                ActorPixels = actorPixels;
                TargetPixels = targetPixels;
                OverlapCandidates = overlapCandidates;
                FaceActorCorePixels = faceActorCorePixels;
                HeadActorCorePixels = headActorCorePixels;
                TorsoActorCorePixels = torsoActorCorePixels;
                FaceOverlapCandidates = faceOverlapCandidates;
                HeadOverlapCandidates = headOverlapCandidates;
                TorsoOverlapCandidates = torsoOverlapCandidates;
                InvalidActorEffectPixels = invalidActorEffectPixels;
                InvalidFaceEffectPixels = invalidFaceEffectPixels;
                InvalidHeadEffectPixels = invalidHeadEffectPixels;
                InvalidTorsoEffectPixels = invalidTorsoEffectPixels;
                OpaqueActorOccludedPixels = opaqueActorOccludedPixels;
                AntiAliasResidualPixels = antiAliasResidualPixels;
            }

            public int ActorPixels { get; }
            public int TargetPixels { get; }
            public int OverlapCandidates { get; }
            public int FaceActorCorePixels { get; }
            public int HeadActorCorePixels { get; }
            public int TorsoActorCorePixels { get; }
            public int FaceOverlapCandidates { get; }
            public int HeadOverlapCandidates { get; }
            public int TorsoOverlapCandidates { get; }
            public int InvalidActorEffectPixels { get; }
            public int InvalidFaceEffectPixels { get; }
            public int InvalidHeadEffectPixels { get; }
            public int InvalidTorsoEffectPixels { get; }
            public int OpaqueActorOccludedPixels { get; }
            public int AntiAliasResidualPixels { get; }
        }

        private readonly struct DepthCaseRecord
        {
            public DepthCaseRecord(
                string memberId,
                string kindId,
                int direction,
                int walkFrame,
                DepthProbeRelation relation,
                string spriteName,
                int actorOrder,
                int targetMinimumOrder,
                int targetMaximumOrder,
                int orderSign,
                Vector2 probeGridPosition,
                int stableFrameCount,
                int stableFrameFlipCount,
                string normalPngPath,
                bool flipX,
                bool flipY,
                ProbeMetrics metrics)
            {
                MemberId = memberId;
                KindId = kindId;
                Direction = direction;
                WalkFrame = walkFrame;
                Relation = relation;
                SpriteName = spriteName;
                ActorOrder = actorOrder;
                TargetMinimumOrder = targetMinimumOrder;
                TargetMaximumOrder = targetMaximumOrder;
                OrderSign = orderSign;
                ProbeGridPosition = probeGridPosition;
                StableFrameCount = stableFrameCount;
                StableFrameFlipCount = stableFrameFlipCount;
                NormalPngPath = normalPngPath ?? string.Empty;
                FlipX = flipX;
                FlipY = flipY;
                Metrics = metrics;
            }

            public string MemberId { get; }
            public string KindId { get; }
            public int Direction { get; }
            public int WalkFrame { get; }
            public DepthProbeRelation Relation { get; }
            public string SpriteName { get; }
            public int ActorOrder { get; }
            public int TargetMinimumOrder { get; }
            public int TargetMaximumOrder { get; }
            public int OrderSign { get; }
            public Vector2 ProbeGridPosition { get; }
            public int StableFrameCount { get; }
            public int StableFrameFlipCount { get; }
            public string NormalPngPath { get; }
            public bool FlipX { get; }
            public bool FlipY { get; }
            public ProbeMetrics Metrics { get; }
        }

        private readonly struct ActorRestoreState
        {
            private readonly OfficeRuntimeAgent _actor;
            private readonly Vector3 _position;
            private readonly Sprite _sprite;
            private readonly bool _enabled;
            private readonly bool _flipX;
            private readonly bool _flipY;
            private readonly int _sortingOrder;

            public ActorRestoreState(OfficeRuntimeAgent actor)
            {
                _actor = actor;
                _position = actor.transform.position;
                SpriteRenderer renderer = actor.PresentationRenderer;
                _sprite = renderer == null ? null : renderer.sprite;
                _enabled = renderer != null && renderer.enabled;
                _flipX = renderer != null && renderer.flipX;
                _flipY = renderer != null && renderer.flipY;
                _sortingOrder = renderer == null ? 0 : renderer.sortingOrder;
            }

            public void Restore()
            {
                if (_actor == null) return;
                _actor.transform.position = _position;
                SpriteRenderer renderer = _actor.PresentationRenderer;
                if (renderer == null) return;
                renderer.sprite = _sprite;
                renderer.enabled = _enabled;
                renderer.flipX = _flipX;
                renderer.flipY = _flipY;
                renderer.sortingOrder = _sortingOrder;
            }
        }
    }
}
