using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Presentation.Unity.OfficeGridView;
using FamilyCompany.Presentation.Unity.OfficeSeating;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime
{
    [DisallowMultipleComponent]
    public sealed class StarterOfficeRuntimeBootstrap : MonoBehaviour
    {
        private static readonly string[] MemberIds =
            { "player", "older_sister", "father", "mother" };
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
        private string _layoutHash = string.Empty;
        private bool _building;

        public bool IsReady { get; private set; }
        public OfficeRuntimeWorld World => _world;
        public IReadOnlyList<OfficeRuntimeAgent> Actors =>
            _world == null ? Array.Empty<OfficeRuntimeAgent>() : _world.Registry.Actors;
        public string LayoutHash => _layoutHash;

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

            var usedSpawns = new HashSet<OfficeGridCoordinate>();
            for (var index = 0; index < MemberIds.Length; index++)
            {
                string memberId = MemberIds[index];
                OfficeGridCoordinate spawn = FindSpawn(PreferredSpawns[index], usedSpawns);
                usedSpawns.Add(spawn);
                OfficeRuntimeAgent actor = CreateActor(memberId, memberId == "player", spawn);
                _world.RegisterActor(actor);
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
            string codeDefaultHash = OfficeGridLayouts.CreateStarterOfficeV1().ComputeLayoutHash();
            if (!string.Equals(
                    _bootstrap.State.OfficeGrid.ComputeLayoutHash(),
                    codeDefaultHash,
                    StringComparison.Ordinal)) return;
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
            animator.Configure(renderer, _assetSource.CopyWalkFrames(memberId));
            OfficeGridSeatingFrameSet seating = _assetSource.CopySeatingFrameSet(memberId);
            animator.ConfigureOfficeSeating(
                seating.sitDownFrames,
                seating.workFrames,
                seating.standUpFrames,
                presentationMode: OfficeSeatingPresentationMode.SafeStaticWork);
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

        private OfficeGridCoordinate FindSpawn(
            OfficeGridCoordinate preferred,
            ISet<OfficeGridCoordinate> used)
        {
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
            _building = true;
            IsReady = false;
            if (_generated != null) Destroy(_generated);
            yield return null;
            _generated = null;
            _world = null;
            _building = false;
            BuildRuntime();
        }
    }
}
