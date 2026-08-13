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
    [DisallowMultipleComponent]
    public sealed class StarterOfficeRuntimeBootstrap : MonoBehaviour
    {
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
        private OfficeLocomotionTransitionCatalog _locomotionTransitionCatalog;
        private readonly Dictionary<string, OfficeWorkActionFrameSet> _workActionFrameSets =
            new Dictionary<string, OfficeWorkActionFrameSet>(StringComparer.Ordinal);
        private readonly Dictionary<string, OfficeRuntimeAgentLayoutSnapshot> _layoutSnapshots =
            new Dictionary<string, OfficeRuntimeAgentLayoutSnapshot>(StringComparer.Ordinal);
        private OfficeSeatingPresentationMode _seatingPresentationMode =
            OfficeSeatingPresentationMode.SafeStaticWork;

        public bool IsReady { get; private set; }
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
                BuildRuntime();
                return;
            }
            string nextHash = _bootstrap.State.OfficeGrid.ComputeLayoutHash();
            if (!string.Equals(nextHash, _layoutHash, StringComparison.Ordinal))
            {
                if (!_building) StartCoroutine(RebuildForLayoutChange());
                return;
            }
            foreach (OfficeRuntimeAgent actor in Actors) actor.ResetRuntimeState();
            BindCoordinators();
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
            ResolveSeatingPresentationMode();
            _locomotionTransitionCatalog = OfficeLocomotionTransitionCatalog.LoadDefault();
            if (_locomotionTransitionCatalog == null)
            {
                Debug.LogWarning(
                    "STARTER_OFFICE_LOCOMOTION_TRANSITIONS | mode=WalkFallback reason=CatalogMissing");
            }
            else
            {
                _locomotionTransitionCatalog.Validate();
            }
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
                OfficeGridCoordinate spawn = FindSpawn(preferred, usedSpawns);
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
            BindCoordinators();
            FitCamera(presenter, furniturePresenter);
            foreach (Renderer renderer in _legacyRenderers)
                if (renderer != null) renderer.enabled = false;
            IsReady = true;
            _building = false;
            _layoutSnapshots.Clear();
            LogOwnershipPass();
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
            OfficeLayoutEditResult legacyWithoutDoor =
                OfficeLayoutEditRules.RemoveFurniture(codeDefault, "entrance_door");
            bool usesLegacyDefault = legacyWithoutDoor.Success && string.Equals(
                currentHash,
                legacyWithoutDoor.Grid.ComputeLayoutHash(),
                StringComparison.Ordinal);
            if (!usesCurrentDefault && !usesLegacyDefault) return;
            OfficeGrid definitionGrid = definition.BuildGrid();
            _bootstrap.State.ReplaceOfficeGrid(definitionGrid);
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
            Sprite[] walkFrames = _assetSource.CopyWalkFrames(memberId);
            animator.Configure(renderer, walkFrames);
            if (familyMember && _locomotionTransitionCatalog != null)
                animator.ConfigureLocomotionTransitions(
                    _locomotionTransitionCatalog.CopyFrames(memberId));
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
            actor.Configure(
                _bootstrap,
                _world,
                memberId,
                playerControlled,
                renderer,
                visual.transform,
                animator,
                _assetSource.CharacterSeatPoseCatalog,
                spawn);
            if (playerControlled)
            {
                var controller = root.AddComponent<OfficeRuntimePlayerController>();
                controller.Configure(_bootstrap, actor);
            }
            return actor;
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
            ISet<OfficeGridCoordinate> used)
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
                    _world.Occupancy.IsCellPassable(current, string.Empty, string.Empty, false))
                    return current;
                foreach (OfficeGridCoordinate offset in offsets)
                {
                    var next = new OfficeGridCoordinate(current.X + offset.X, current.Y + offset.Y);
                    if (_world.Grid.Contains(next) && visited.Add(next)) queue.Enqueue(next);
                }
            }
            throw new InvalidOperationException("Starter Office has no valid actor spawn cell.");
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
