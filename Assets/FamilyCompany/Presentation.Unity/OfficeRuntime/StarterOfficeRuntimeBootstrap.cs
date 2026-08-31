using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Presentation.Unity.OfficeGridView;
using FamilyCompany.Presentation.Unity.OfficeSeating;
using FamilyCompany.Presentation.Unity.OfficeSeating.Authoring;
using FamilyCompany.Presentation.Unity.OfficeWorkActions;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime
{
    public enum StarterOfficeRuntimePreparationState
    {
        NotStarted = 0,
        Preparing = 1,
        Ready = 2,
        Failed = 3
    }

    internal sealed class StarterOfficeRuntimePreparationGate
    {
        private bool _preloadSucceeded;
        private bool _coordinatorAttached;

        public StarterOfficeRuntimePreparationState State { get; private set; }
        public string FailureReason { get; private set; } = string.Empty;

        public void Begin()
        {
            _preloadSucceeded = false;
            _coordinatorAttached = false;
            FailureReason = string.Empty;
            State = StarterOfficeRuntimePreparationState.Preparing;
        }

        public void MarkPreloadSucceeded()
        {
            if (State != StarterOfficeRuntimePreparationState.Preparing)
                throw new InvalidOperationException("Runtime preload completed outside preparation.");
            _preloadSucceeded = true;
        }

        public void MarkCoordinatorAttached()
        {
            if (State != StarterOfficeRuntimePreparationState.Preparing || !_preloadSucceeded)
                throw new InvalidOperationException(
                    "Runtime coordinator cannot attach before successful preload.");
            _coordinatorAttached = true;
        }

        public void PublishReady()
        {
            if (State != StarterOfficeRuntimePreparationState.Preparing ||
                !_preloadSucceeded || !_coordinatorAttached)
                throw new InvalidOperationException(
                    "Runtime ready cannot publish before preload and coordinator attach.");
            State = StarterOfficeRuntimePreparationState.Ready;
            FailureReason = string.Empty;
        }

        public void Fail(string reason)
        {
            State = StarterOfficeRuntimePreparationState.Failed;
            FailureReason = string.IsNullOrWhiteSpace(reason) ? "unknown" : reason;
        }

        public bool TryComplete(
            Action preload,
            Action prepareAttendance,
            Action attachCoordinators,
            Action cleanup,
            string failureStage,
            out Exception failure)
        {
            if (State != StarterOfficeRuntimePreparationState.Preparing)
                throw new InvalidOperationException("Runtime preparation transaction is not active.");
            if (preload == null) throw new ArgumentNullException(nameof(preload));
            if (prepareAttendance == null) throw new ArgumentNullException(nameof(prepareAttendance));
            if (attachCoordinators == null) throw new ArgumentNullException(nameof(attachCoordinators));
            if (cleanup == null) throw new ArgumentNullException(nameof(cleanup));
            failure = null;
            try
            {
                preload();
                MarkPreloadSucceeded();
                prepareAttendance();
                attachCoordinators();
                MarkCoordinatorAttached();
                PublishReady();
                return true;
            }
            catch (Exception exception)
            {
                failure = exception;
                try
                {
                    cleanup();
                }
                catch (Exception cleanupException)
                {
                    failure = new AggregateException(exception, cleanupException);
                }
                Fail((string.IsNullOrWhiteSpace(failureStage) ? "preparation" : failureStage) + ":" +
                     failure.GetType().Name + ":" + failure.Message);
                return false;
            }
        }
    }

    [DisallowMultipleComponent]
    public sealed class StarterOfficeRuntimeBootstrap : MonoBehaviour
    {
        private const float PlayerV8ProductionCollisionRadius = 0.28f;
        private const float FatherV19ProductionCollisionRadius = 0.30f;
        private static readonly string[] FamilyMemberIds =
            { "player", "older_sister", "father", "mother" };
        // Candidates are content only until the player hires them. Creating all eight candidates
        // as live runtime actors made every presentation step, depth sort and occupancy query 3x
        // heavier and incorrectly showed unhired people in the starting company.
        private static readonly string[] MemberIds = FamilyMemberIds;
        private static readonly OfficeGridCoordinate[] PreferredSpawns =
        {
            new OfficeGridCoordinate(1, 2),
            new OfficeGridCoordinate(10, 4),
            new OfficeGridCoordinate(1, 9),
            new OfficeGridCoordinate(9, 6)
        };

        private PrototypeBootstrap _bootstrap;
        private OfficeTileMigrationPreviewBootstrap _assetSource;
        private Camera _runtimeCamera;
        private Renderer[] _legacyRenderers = Array.Empty<Renderer>();
        private GameObject _generated;
        private OfficeRuntimeWorld _world;
        private static int _activeLayoutRebuilds;
        private string _layoutHash = string.Empty;
        private bool _building;
        private readonly Dictionary<string, OfficeWorkActionFrameSet> _workActionFrameSets =
            new Dictionary<string, OfficeWorkActionFrameSet>(StringComparer.Ordinal);
        private readonly Dictionary<string, OfficeRuntimeAgentLayoutSnapshot> _layoutSnapshots =
            new Dictionary<string, OfficeRuntimeAgentLayoutSnapshot>(StringComparer.Ordinal);
        private OfficeSeatingPresentationMode _seatingPresentationMode =
            OfficeSeatingPresentationMode.SafeStaticWork;
        private readonly StarterOfficeRuntimePreparationGate _preparationGate =
            new StarterOfficeRuntimePreparationGate();

        public bool IsReady { get; private set; }
        public bool IsPreparing => _building;
        public StarterOfficeRuntimePreparationState PreparationState => _preparationGate.State;
        public string PreparationFailureReason => _preparationGate.FailureReason;
        public float NavigationPrewarmProgress { get; private set; }
        public OfficeRuntimeWorld World => _world;
        public static bool IsLayoutRebuilding => _activeLayoutRebuilds > 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _activeLayoutRebuilds = 0;
        }
        public IReadOnlyList<OfficeRuntimeAgent> Actors =>
            _world == null ? Array.Empty<OfficeRuntimeAgent>() : _world.Registry.Actors;
        public string LayoutHash => _layoutHash;
        public OfficeSeatingPresentationMode SeatingPresentationMode => _seatingPresentationMode;

        public void Configure(
            PrototypeBootstrap bootstrap,
            OfficeTileMigrationPreviewBootstrap assetSource,
            Camera runtimeCamera,
            Renderer[] legacyRenderers)
        {
            _bootstrap = bootstrap ?? throw new ArgumentNullException(nameof(bootstrap));
            _assetSource = assetSource ?? throw new ArgumentNullException(nameof(assetSource));
            _runtimeCamera = runtimeCamera ?? throw new ArgumentNullException(nameof(runtimeCamera));
            _legacyRenderers = legacyRenderers ?? Array.Empty<Renderer>();
            BuildRuntime();
        }

        public void Rebind(PrototypeBootstrap bootstrap)
        {
            _bootstrap = bootstrap ?? throw new ArgumentNullException(nameof(bootstrap));
            ApplyStarterDefinitionWhenStateUsesCodeDefault();
            if (!IsReady)
            {
                if (_building) return;
                if (_world != null && _generated != null &&
                    _world.Registry.Count == FamilyMemberIds.Length)
                {
                    _building = true;
                    IsReady = false;
                    NavigationPrewarmProgress = 0f;
                    _preparationGate.Begin();
                    StartCoroutine(CompleteRuntimePreparation());
                }
                else
                {
                    BuildRuntime();
                }
                return;
            }
            string nextHash = _bootstrap.State.OfficeGrid.ComputeLayoutHash();
            if (!string.Equals(nextHash, _layoutHash, StringComparison.Ordinal))
            {
                if (!_building) StartCoroutine(RebuildForLayoutChange());
                return;
            }
            _building = true;
            IsReady = false;
            _preparationGate.Begin();
            foreach (OfficeRuntimeAgent actor in Actors) actor.ResetRuntimeState();
            if (TryCompleteActorPreparation(out Exception failure))
            {
                IsReady = true;
                _building = false;
            }
            else
            {
                CompleteRuntimePreparationFailure(failure);
            }
        }

        /// <summary>
        /// Replaces the semantic layout and rebuilds render, collision, seats and save state from it.
        /// The single entry point for any layout change, so the editor cannot move a sprite without
        /// moving the collision footprint with it.
        /// </summary>
        public void ApplyLayout(OfficeGrid grid)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (_building) throw new InvalidOperationException("Starter Office is already rebuilding.");
            _bootstrap.State.ReplaceOfficeGrid(grid);
            IsReady = false;
            StartCoroutine(RebuildForLayoutChange());
        }

        public void ApplyLayoutForQa(OfficeGrid grid) => ApplyLayout(grid);

        private void BuildRuntime()
        {
            if (_building || _bootstrap.State == null) return;
            ApplyStarterDefinitionWhenStateUsesCodeDefault();
            _building = true;
            IsReady = false;
            _preparationGate.Begin();
            CacheWorkActionFrameSets();
            _assetSource.DestroyGeneratedPreview();
            if (_generated != null)
            {
                if (Application.isPlaying) Destroy(_generated);
                else DestroyImmediate(_generated);
            }

            OfficeGrid grid = _bootstrap.State.OfficeGrid;
            _layoutHash = grid.ComputeLayoutHash();
            _generated = new GameObject("GeneratedStarterOfficeRuntime");
            _generated.transform.SetParent(transform, false);
            var presentationRoot = new GameObject("Presentation");
            presentationRoot.transform.SetParent(_generated.transform, false);
            var presenter = presentationRoot.AddComponent<OfficeGridTilemapPresenter>();
            presenter.Configure(grid, _assetSource.CopyFloorTiles());
            var furnitureRoot = new GameObject("Furniture");
            furnitureRoot.transform.SetParent(presentationRoot.transform, false);
            var furniturePresenter = furnitureRoot.AddComponent<OfficeGridFurniturePresenter>();
            furniturePresenter.Configure(
                grid,
                presenter,
                _assetSource.FurnitureVisualCatalog);
            _world = _generated.AddComponent<OfficeRuntimeWorld>();
            _world.Configure(grid, presenter, furniturePresenter);
            try
            {
                ResolveSeatingPresentationMode();
            }
            catch (Exception exception)
            {
                FailRuntimePreparation("seat-presentation-validation", exception);
                return;
            }
            // FC-WALK-GUARDRAIL-V1: family locomotion must never swap to the separately authored
            // legacy start/stop/pivot portraits. The approved idle/walk identity is the only
            // sprite family configured on production actors.
            Debug.Log("STARTER_OFFICE_LOCOMOTION_TRANSITIONS | mode=WalkIdleOnly " +
                      "contract=FC-WALK-GUARDRAIL-V1");
            var usedSpawns = new HashSet<OfficeGridCoordinate>();
            for (var index = 0; index < MemberIds.Length; index++)
            {
                string memberId = MemberIds[index];
                OfficeGridCoordinate preferred = _layoutSnapshots.TryGetValue(
                    memberId,
                    out OfficeRuntimeAgentLayoutSnapshot snapshot)
                    ? snapshot.Cell
                    : index < PreferredSpawns.Length
                        ? PreferredSpawns[index]
                        : new OfficeGridCoordinate(1 + (index - PreferredSpawns.Length) % 6, 1);
                OfficeGridCoordinate spawn = FindSpawn(
                    preferred,
                    usedSpawns,
                    CollisionRadiusForMember(memberId));
                usedSpawns.Add(spawn);
                OfficeRuntimeAgent actor = CreateActor(memberId, memberId == "player", spawn);
                _world.RegisterActor(actor);
                if (_layoutSnapshots.TryGetValue(memberId, out snapshot) &&
                    !actor.RestoreLayoutSnapshot(snapshot))
                {
                    Debug.LogError(
                        "STARTER_OFFICE_LAYOUT_RESTORE_FAILED | member=" + memberId +
                        " | task=" + snapshot.AssignedTaskId);
                }
            }
            DisableLegacyRuntime();
            _world.ValidateCanonicalActors();
            ValidateSingleRuntimeOwnership();
            FitCamera(presenter, furniturePresenter);
            foreach (Renderer renderer in _legacyRenderers)
                if (renderer != null) renderer.enabled = false;
            NavigationPrewarmProgress = 0f;
            StartCoroutine(CompleteRuntimePreparation());
        }

        private IEnumerator CompleteRuntimePreparation()
        {
            var permittedSeatIds = new List<string> { string.Empty };
            permittedSeatIds.AddRange(_world.Grid.SeatSlots
                .Select(seat => seat.SeatId)
                .Distinct(StringComparer.Ordinal));
            var timer = System.Diagnostics.Stopwatch.StartNew();
            IEnumerator prewarm = _world.Paths.PrewarmAllStaticTraversalGraphs(
                permittedSeatIds,
                4,
                progress => NavigationPrewarmProgress = progress);
            while (true)
            {
                bool hasNext;
                object current;
                try
                {
                    hasNext = prewarm.MoveNext();
                    current = hasNext ? prewarm.Current : null;
                }
                catch (Exception exception)
                {
                    FailRuntimePreparation("navigation-prewarm", exception);
                    yield break;
                }
                if (!hasNext) break;
                yield return current;
            }
            timer.Stop();
            if (TryCompleteActorPreparation(out Exception failure))
            {
                IsReady = PreparationState == StarterOfficeRuntimePreparationState.Ready;
                _building = false;
                _layoutSnapshots.Clear();
                Debug.Log(
                    "STARTER_OFFICE_NAVIGATION_PREWARM_PASS | " +
                    "keys=" + permittedSeatIds.Count +
                    " nodes=" + _world.Paths.StaticGraphNodeCheckCount +
                    " edges=" + _world.Paths.StaticGraphEdgeCheckCount +
                    " elapsed=" + timer.Elapsed.TotalSeconds.ToString("F2") + "s");
                LogOwnershipPass();
            }
            else
            {
                CompleteRuntimePreparationFailure(failure);
            }
        }

        private bool TryCompleteActorPreparation(out Exception failure)
        {
            return _preparationGate.TryComplete(
                () =>
                {
                    foreach (OfficeRuntimeAgent actor in Actors)
                    {
                        if (actor == null)
                            throw new InvalidOperationException("Runtime preload actor is null.");
                        actor.PreloadR5eSeatPresentation();
                    }
                },
                PrepareAttendanceArrivals,
                BindCoordinators,
                CleanupFailedRuntimePreparation,
                "preload-attendance-attach",
                out failure);
        }

        private void FailRuntimePreparation(string stage, Exception exception)
        {
            try
            {
                CleanupFailedRuntimePreparation();
            }
            catch (Exception cleanupException)
            {
                exception = exception == null
                    ? cleanupException
                    : new AggregateException(exception, cleanupException);
            }
            _preparationGate.Fail(stage + ":" +
                                  (exception == null
                                      ? "unknown"
                                      : exception.GetType().Name + ":" + exception.Message));
            CompleteRuntimePreparationFailure(exception);
        }

        private void CompleteRuntimePreparationFailure(Exception exception)
        {
            _building = false;
            IsReady = false;
            Debug.LogError(
                "STARTER_OFFICE_PREPARATION_FAILED | reason=" + PreparationFailureReason +
                " | retry=rebind-or-rebuild");
            if (exception != null) Debug.LogException(exception);
        }

        private void PrepareAttendanceArrivals()
        {
            string[] actorIds = Actors.Select(actor => actor?.AgentId).ToArray();
            bool[] prepared = Actors.Select(actor =>
                actor != null && actor.PrepareAttendanceArrival()).ToArray();
            RequireCompleteAttendancePreparation(actorIds, prepared);
            Debug.Log(
                "STARTER_OFFICE_ATTENDANCE_PREWARM_PASS | routes=" + prepared.Length +
                " | entrance=(8,1)");
        }

        internal static void RequireCompleteAttendancePreparation(
            IReadOnlyList<string> actorIds,
            IReadOnlyList<bool> preparedRoutes)
        {
            OfficeRuntimeActorRegistry.ValidateCanonicalActorIds(actorIds);
            if (preparedRoutes == null || preparedRoutes.Count != FamilyMemberIds.Length)
                throw new InvalidOperationException(
                    "Starter Office requires exactly four attendance route results.");
            int prepared = 0;
            for (var index = 0; index < preparedRoutes.Count; index++)
            {
                if (preparedRoutes[index]) prepared++;
            }
            if (prepared != FamilyMemberIds.Length)
                throw new InvalidOperationException(
                    "Starter Office attendance preparation is incomplete: canonical=4 routes=" +
                    prepared + "/4.");
        }

        private void CleanupFailedRuntimePreparation()
        {
            _bootstrap?.UnbindStarterOfficeRuntime();
            foreach (OfficeRuntimeAgent actor in Actors)
            {
                if (actor == null) continue;
                actor.ResetRuntimeState();
                actor.ResetR5eSeatPresentationPreloadAfterFailure();
                actor.SetAttendanceOutside(true, false);
                _world?.Occupancy.ClearReservations(actor.AgentId);
            }
        }

        private void ApplyStarterDefinitionWhenStateUsesCodeDefault()
        {
            StarterOfficeLayoutAsset definition = StarterOfficeLayoutAsset.LoadDefault();
            if (definition == null) return;
            OfficeLayoutValidationReport report = OfficeLayoutSemanticValidator.Validate(definition);
            if (!report.IsValid)
            {
                Debug.LogError(
                    "StarterOfficeV1.asset is invalid; keeping the current GameState layout. " +
                    string.Join(" | ", report.Errors));
                return;
            }
            OfficeGrid codeDefault = OfficeGridLayouts.CreateStarterOfficeV1();
            string currentHash = _bootstrap.State.OfficeGrid.ComputeLayoutHash();
            bool usesCurrentDefault = string.Equals(
                currentHash,
                codeDefault.ComputeLayoutHash(),
                StringComparison.Ordinal);
            OfficeGrid legacyWithoutDoor = BuildLegacyStarterDefault(false);
            OfficeGrid legacySingleDoor = BuildLegacyStarterDefault(true);
            bool usesLegacyDefault = string.Equals(
                                         currentHash,
                                         legacyWithoutDoor.ComputeLayoutHash(),
                                         StringComparison.Ordinal) ||
                                     string.Equals(
                                         currentHash,
                                         legacySingleDoor.ComputeLayoutHash(),
                                         StringComparison.Ordinal);
            if (!usesCurrentDefault && !usesLegacyDefault) return;
            OfficeGrid definitionGrid = definition.BuildGrid();
            _bootstrap.State.ReplaceOfficeGrid(definitionGrid);
        }

        private static OfficeGrid BuildLegacyStarterDefault(bool includeSingleDoor)
        {
            OfficeGrid current = OfficeGridLayouts.CreateStarterOfficeV1();
            OfficeFloorTileKind[] floor = current.CopyFloorTiles();
            floor[7] = OfficeFloorTileKind.WarmWoodA;
            floor[8] = OfficeFloorTileKind.WarmWoodA;
            floor[9] = OfficeFloorTileKind.WarmWoodA;
            var furniture = current.Furniture.Where(item =>
                    !string.Equals(item.FurnitureId, "entrance_wall_left", StringComparison.Ordinal) &&
                    !string.Equals(item.FurnitureId, "entrance_door", StringComparison.Ordinal) &&
                    !string.Equals(item.FurnitureId, "entrance_wall_right", StringComparison.Ordinal))
                .ToList();
            if (includeSingleDoor)
            {
                furniture.Add(new PlacedOfficeFurniture(
                    "entrance_door",
                    OfficeGridLayouts.EntranceDoorKind,
                    new OfficeGridCoordinate(8, 1),
                    1,
                    1,
                    OfficeFurnitureFacing.SouthEast,
                    false));
            }
            return new OfficeGrid(
                current.Width,
                current.Height,
                floor,
                current.CopyWalkable(),
                furniture,
                current.SeatSlots);
        }

        private OfficeRuntimeAgent CreateActor(
            string memberId,
            bool playerControlled,
            OfficeGridCoordinate spawn)
        {
            var root = new GameObject("StarterOfficeActor_" + memberId);
            root.transform.SetParent(_generated.transform, false);
            var visual = new GameObject("VisualRoot");
            visual.transform.SetParent(root.transform, false);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sortingLayerName = "Default";
            var animator = root.AddComponent<DirectionalSpriteAnimator>();
            bool familyMember = Array.IndexOf(FamilyMemberIds, memberId) >= 0;
            bool controlledPlayer = playerControlled &&
                                    string.Equals(memberId, "player", StringComparison.Ordinal);
            bool production3DCharacter =
                string.Equals(memberId, "player", StringComparison.Ordinal) ||
                string.Equals(memberId, "father", StringComparison.Ordinal);
            // Player V8 and Father V19 own every visible pixel for their production actors. The
            // former 2D renderers remain only as hidden locomotion/seating state data until those
            // simulation clocks are separated from sprites; no missing-asset fallback can revive them.
            Sprite[] walkFrames = ResolveWalkFrames(memberId);
            animator.Configure(renderer, walkFrames);
            if (familyMember)
            {
                OfficeGridSeatingFrameSet seating = _assetSource.CopySeatingFrameSet(memberId);
                animator.ConfigureOfficeSeating(
                    seating.sitDownFrames,
                    seating.workFrames,
                    seating.standUpFrames,
                    presentationMode: _seatingPresentationMode);
                if (_workActionFrameSets.TryGetValue(memberId, out OfficeWorkActionFrameSet frameSet))
                {
                    var adapter = root.AddComponent<OfficeSeatedWorkMicroActionAdapter>();
                    adapter.Configure(_bootstrap, memberId, frameSet);
                    animator.ConfigureOfficeWorkAnimationHook(adapter);
                }
            }
            var actor = root.AddComponent<OfficeRuntimeAgent>();
            if (production3DCharacter)
            {
                renderer.forceRenderingOff = true;
                Debug.Log(
                    "FAMILY_3D_VISUAL_PRESENTATION | member=" + memberId +
                    " mode=" + (controlledPlayer ? "Production3DPlayerV8" : "Production3DFatherV19") +
                    " | " +
                    "legacy2DVisible=false fallback=false");
            }
            actor.Configure(
                _bootstrap,
                _world,
                memberId,
                playerControlled,
                renderer,
                visual.transform,
                animator,
                _assetSource.CharacterSeatPoseCatalog,
                spawn,
                OfficeRuntimeAgent.DefaultRadius,
                CollisionRadiusForMember(memberId));
            if (playerControlled)
            {
                var controller = root.AddComponent<OfficeRuntimePlayerController>();
                controller.Configure(_bootstrap, actor);
            }
            return actor;
        }

        private Sprite[] ResolveWalkFrames(string memberId)
        {
            OfficeRuntimeCharacterArtCatalog runtimeCatalog =
                OfficeRuntimeCharacterArtCatalog.LoadDefault();
            if (runtimeCatalog != null && runtimeCatalog.TryCopyWalkFrames(memberId, out Sprite[] runtimeFrames))
                return runtimeFrames;
            return _assetSource.CopyWalkFrames(memberId);
        }

        private void CacheWorkActionFrameSets()
        {
            _workActionFrameSets.Clear();
            foreach (OfficeSeatedWorkMicroActionAdapter adapter in
                     FindObjectsByType<OfficeSeatedWorkMicroActionAdapter>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (adapter == null || adapter.FrameSet == null) continue;
                string memberId = adapter.MemberId;
                if (memberId.Length == 0 ||
                    !string.Equals(adapter.FrameSet.MemberId, memberId, StringComparison.Ordinal) ||
                    _workActionFrameSets.ContainsKey(memberId)) continue;
                _workActionFrameSets.Add(memberId, adapter.FrameSet);
            }
        }

        private void ResolveSeatingPresentationMode()
        {
            var catalog = _assetSource.CharacterSeatPoseCatalog;
            const int northwest = (int)OfficeSeatFacing8.Northwest;
            try
            {
                catalog.ValidateAnimatedNorthwest(FamilyMemberIds, northwest);
                _seatingPresentationMode = OfficeSeatingPresentationMode.Animated;
                Debug.Log("STARTER_OFFICE_SEATING_PRESENTATION | mode=Animated profiles=56 facing=Northwest");
            }
            catch (Exception animatedFailure)
            {
                catalog.ValidateSafeStaticWork(FamilyMemberIds, northwest);
                _seatingPresentationMode = OfficeSeatingPresentationMode.SafeStaticWork;
                Debug.LogWarning(
                    "STARTER_OFFICE_SEATING_PRESENTATION | mode=SafeStaticWork reason=" +
                    animatedFailure.Message);
            }
        }

        private OfficeGridCoordinate FindSpawn(
            OfficeGridCoordinate preferred,
            ISet<OfficeGridCoordinate> used,
            float actorRadius)
        {
            // A layout snapshot can come from a larger editor/QA grid. Seed the search at the
            // nearest cell inside the new layout; starting outside makes every in-bounds neighbor
            // unreachable because the queue deliberately admits only contained cells.
            preferred = new OfficeGridCoordinate(
                Mathf.Clamp(preferred.X, 0, _world.Grid.Width - 1),
                Mathf.Clamp(preferred.Y, 0, _world.Grid.Height - 1));
            var queue = new Queue<OfficeGridCoordinate>();
            var visited = new HashSet<OfficeGridCoordinate> { preferred };
            queue.Enqueue(preferred);
            var offsets = new[]
            {
                new OfficeGridCoordinate(1, 0), new OfficeGridCoordinate(0, -1),
                new OfficeGridCoordinate(-1, 0), new OfficeGridCoordinate(0, 1)
            };
            while (queue.Count > 0)
            {
                OfficeGridCoordinate current = queue.Dequeue();
                if (!used.Contains(current) && _world.Grid.Contains(current) &&
                    _world.Occupancy.IsCellPassable(current, string.Empty, string.Empty, false) &&
                    _world.Occupancy.CanTraverseStatic(
                        (Vector2)_world.Presenter.CellCenterWorld(current),
                        (Vector2)_world.Presenter.CellCenterWorld(current),
                        actorRadius,
                        string.Empty))
                    return current;
                foreach (OfficeGridCoordinate offset in offsets)
                {
                    var next = new OfficeGridCoordinate(current.X + offset.X, current.Y + offset.Y);
                    if (_world.Grid.Contains(next) && visited.Add(next)) queue.Enqueue(next);
                }
            }
            throw new InvalidOperationException("Starter Office has no valid actor spawn cell.");
        }

        private static float CollisionRadiusForMember(string memberId)
        {
            if (string.Equals(memberId, "player", StringComparison.Ordinal))
                return PlayerV8ProductionCollisionRadius;
            if (string.Equals(memberId, "father", StringComparison.Ordinal))
                return FatherV19ProductionCollisionRadius;
            return OfficeRuntimeAgent.DefaultRadius;
        }

        private void DisableLegacyRuntime()
        {
            foreach (OfficeWorkerAgent legacy in FindObjectsByType<OfficeWorkerAgent>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                legacy.enabled = false;
            foreach (PrototypePlayerController player in FindObjectsByType<PrototypePlayerController>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                player.enabled = false;
            foreach (PlayerOfficeWorkInteractor work in FindObjectsByType<PlayerOfficeWorkInteractor>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                work.enabled = false;
            foreach (OfficePlayerSeatingPresenter seating in
                     FindObjectsByType<OfficePlayerSeatingPresenter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                seating.enabled = false;
            foreach (OfficeNavigationWorld navigation in FindObjectsByType<OfficeNavigationWorld>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                navigation.enabled = false;
            foreach (CharacterController controller in FindObjectsByType<CharacterController>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                controller.enabled = false;
        }

        private void BindCoordinators()
        {
            _bootstrap.BindStarterOfficeRuntime(
                Actors.Cast<IOfficeRuntimeAgent>().ToArray());
        }

        private static void ValidateSingleRuntimeOwnership()
        {
            int legacyNpcCount = FindObjectsByType<OfficeWorkerAgent>(FindObjectsSortMode.None)
                .Count(item => item != null && item.isActiveAndEnabled);
            int legacyPlayerCount = FindObjectsByType<PrototypePlayerController>(FindObjectsSortMode.None)
                .Count(item => item != null && item.isActiveAndEnabled);
            int previewActorCount = FindObjectsByType<OfficeGridCharacterMover>(FindObjectsSortMode.None)
                .Count(item => item != null && item.isActiveAndEnabled);
            if (legacyNpcCount != 0 || legacyPlayerCount != 0 || previewActorCount != 0)
                throw new InvalidOperationException(
                    "Starter Office runtime ownership is not exclusive: " +
                    $"legacyNpc={legacyNpcCount}, legacyPlayer={legacyPlayerCount}, preview={previewActorCount}.");
        }

        private void FitCamera(
            OfficeGridTilemapPresenter presenter,
            OfficeGridFurniturePresenter furniturePresenter)
        {
            Bounds bounds = presenter.FloorRenderer.bounds;
            bounds.Encapsulate(furniturePresenter.RenderBounds);
            float aspect = Screen.height > 0 ? Screen.width / (float)Screen.height : 16f / 9f;
            OfficeGridCameraFitter.Fit(_runtimeCamera, bounds, aspect);
        }

        private void LogOwnershipPass()
        {
            int activeLegacy = FindObjectsByType<OfficeWorkerAgent>(FindObjectsSortMode.None)
                .Count(item => item != null && item.isActiveAndEnabled);
            int previewActors = FindObjectsByType<OfficeGridCharacterMover>(FindObjectsSortMode.None)
                .Count(item => item != null && item.isActiveAndEnabled);
            Debug.Log(
                "STARTER_OFFICE_RUNTIME_OWNERSHIP_PASS · " +
                $"actors={Actors.Count} legacy={activeLegacy} preview={previewActors} " +
                $"layoutHash={_layoutHash}");
        }

        private IEnumerator RebuildForLayoutChange()
        {
            _activeLayoutRebuilds++;
            _building = true;
            IsReady = false;
            CaptureLayoutSnapshots();
            if (_generated != null) Destroy(_generated);
            yield return null;
            _generated = null;
            _world = null;
            _building = false;
            try
            {
                BuildRuntime();
                while (_building) yield return null;
            }
            finally
            {
                _activeLayoutRebuilds = Math.Max(0, _activeLayoutRebuilds - 1);
            }
        }

        private void CaptureLayoutSnapshots()
        {
            _layoutSnapshots.Clear();
            foreach (OfficeRuntimeAgent actor in Actors)
            {
                if (actor == null || string.IsNullOrWhiteSpace(actor.AgentId)) continue;
                _layoutSnapshots[actor.AgentId] = actor.CaptureLayoutSnapshot();
            }
        }
    }
}
