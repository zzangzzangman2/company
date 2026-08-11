using System;
using System.Collections.Generic;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace FamilyCompany.Presentation.Unity.OfficeGridView
{
    [Serializable]
    public sealed class OfficeGridSeatingFrameSet
    {
        public string memberId = string.Empty;
        public Sprite[] sitDownFrames = Array.Empty<Sprite>();
        public Sprite[] workFrames = Array.Empty<Sprite>();
        public Sprite[] standUpFrames = Array.Empty<Sprite>();
    }

    [DisallowMultipleComponent]
    public sealed class OfficeTileMigrationPreviewBootstrap : MonoBehaviour
    {
        [SerializeField] private bool includeCharacters = true;
        [SerializeField] private bool includeFurniture;
        [SerializeField] private bool seatCharacters;
        [SerializeField] private TileBase[] floorTiles = Array.Empty<TileBase>();
        [SerializeField] private string[] furnitureKindIds = Array.Empty<string>();
        [SerializeField] private Sprite[] furnitureSprites = Array.Empty<Sprite>();
        [SerializeField] private Sprite chairBackrestSprite;
        [SerializeField] private Sprite[] playerFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] sisterFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] fatherFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] motherFrames = Array.Empty<Sprite>();
        [SerializeField] private OfficeGridSeatingFrameSet[] seatingFrameSets =
            Array.Empty<OfficeGridSeatingFrameSet>();

        private OfficeGridTilemapPresenter _presenter;
        private OfficeGridFurniturePresenter _furniturePresenter;
        private OfficeGridCollisionMonitor _collisionMonitor;
        private readonly List<OfficeGridCharacterMover> _movers = new List<OfficeGridCharacterMover>();
        private readonly List<OfficeGridSeatedWorker> _seatedWorkers = new List<OfficeGridSeatedWorker>();

        public OfficeGridTilemapPresenter Presenter => _presenter;
        public OfficeGridFurniturePresenter FurniturePresenter => _furniturePresenter;
        public OfficeGridCollisionMonitor CollisionMonitor => _collisionMonitor;
        public IReadOnlyList<OfficeGridCharacterMover> Movers => _movers;
        public IReadOnlyList<OfficeGridSeatedWorker> SeatedWorkers => _seatedWorkers;

        public Bounds CombinedRenderBounds
        {
            get
            {
                if (_presenter == null) return new Bounds(transform.position, Vector3.zero);
                var bounds = _presenter.FloorRenderer.bounds;
                if (_furniturePresenter != null) bounds.Encapsulate(_furniturePresenter.RenderBounds);
                return bounds;
            }
        }

        public void ConfigureForEditor(
            TileBase[] newFloorTiles,
            Sprite[] newPlayerFrames,
            Sprite[] newSisterFrames,
            Sprite[] newFatherFrames,
            Sprite[] newMotherFrames,
            bool withCharacters)
        {
            floorTiles = CloneRequired(newFloorTiles, 3, nameof(newFloorTiles));
            playerFrames = CloneRequired(newPlayerFrames, DirectionalSpriteAnimator.RequiredFrameCount, nameof(newPlayerFrames));
            sisterFrames = CloneRequired(newSisterFrames, DirectionalSpriteAnimator.RequiredFrameCount, nameof(newSisterFrames));
            fatherFrames = CloneRequired(newFatherFrames, DirectionalSpriteAnimator.RequiredFrameCount, nameof(newFatherFrames));
            motherFrames = CloneRequired(newMotherFrames, DirectionalSpriteAnimator.RequiredFrameCount, nameof(newMotherFrames));
            includeCharacters = withCharacters;
        }

        public void ConfigureFurnitureAndSeatingForEditor(
            string[] newFurnitureKindIds,
            Sprite[] newFurnitureSprites,
            Sprite newChairBackrestSprite,
            OfficeGridSeatingFrameSet[] newSeatingFrameSets)
        {
            if (newFurnitureKindIds == null || newFurnitureSprites == null ||
                newFurnitureKindIds.Length != newFurnitureSprites.Length || newFurnitureKindIds.Length != 12)
            {
                throw new ArgumentException("Office furniture preview requires 12 kind/sprite bindings.");
            }
            furnitureKindIds = (string[])newFurnitureKindIds.Clone();
            furnitureSprites = CloneRequired(newFurnitureSprites, 12, nameof(newFurnitureSprites));
            chairBackrestSprite = newChairBackrestSprite != null
                ? newChairBackrestSprite
                : throw new ArgumentNullException(nameof(newChairBackrestSprite));
            if (newSeatingFrameSets == null || newSeatingFrameSets.Length != 4)
                throw new ArgumentException("Office seating preview requires four family frame sets.");
            seatingFrameSets = newSeatingFrameSets;
            includeFurniture = true;
            seatCharacters = true;
        }

        public void BuildPreview()
        {
            var existing = transform.Find("GeneratedOfficeTilePreview");
            if (existing != null)
            {
                if (Application.isPlaying) Destroy(existing.gameObject);
                else DestroyImmediate(existing.gameObject);
            }

            _movers.Clear();
            _seatedWorkers.Clear();
            _furniturePresenter = null;
            _collisionMonitor = null;
            var semanticGrid = OfficeGridLayouts.CreateMigrationPreview();
            var generated = new GameObject("GeneratedOfficeTilePreview");
            generated.transform.SetParent(transform, false);
            _presenter = generated.AddComponent<OfficeGridTilemapPresenter>();
            _presenter.Configure(semanticGrid, floorTiles);
            if (includeFurniture)
            {
                var furnitureRoot = new GameObject("Furniture");
                furnitureRoot.transform.SetParent(generated.transform, false);
                _furniturePresenter = furnitureRoot.AddComponent<OfficeGridFurniturePresenter>();
                _furniturePresenter.Configure(
                    semanticGrid,
                    _presenter,
                    furnitureKindIds,
                    furnitureSprites,
                    chairBackrestSprite);
            }
            if (!includeCharacters) return;

            if (seatCharacters)
            {
                CreateSeatedCharacter("player", playerFrames, "seat_player", new[]
                {
                    Cell(2, 9), Cell(1, 9), Cell(1, 5), Cell(1, 2), Cell(9, 2),
                    Cell(9, 6), Cell(7, 6), Cell(7, 5), Cell(1, 5), Cell(1, 9), Cell(2, 9)
                }, 30f);
                CreateSeatedCharacter("older_sister", sisterFrames, "seat_older_sister", new[]
                {
                    Cell(10, 4), Cell(9, 4)
                }, 0f);
                CreateSeatedCharacter("father", fatherFrames, "seat_father", new[]
                {
                    Cell(1, 9), Cell(1, 8)
                }, 0f);
                CreateSeatedCharacter("mother", motherFrames, "seat_mother", new[]
                {
                    Cell(9, 6), Cell(8, 6)
                }, 0f);
            }
            else
            {
                CreateCharacter("player", playerFrames, new[]
                {
                    Cell(1, 2), Cell(9, 2), Cell(9, 6), Cell(7, 6), Cell(7, 5), Cell(1, 5)
                });
                CreateCharacter("older_sister", sisterFrames, new[]
                {
                    Cell(10, 3), Cell(9, 3), Cell(9, 5), Cell(10, 5)
                });
                CreateCharacter("father", fatherFrames, new[]
                {
                    Cell(1, 6), Cell(5, 6), Cell(5, 9), Cell(1, 9)
                });
                CreateCharacter("mother", motherFrames, new[]
                {
                    Cell(8, 5), Cell(10, 5), Cell(10, 7), Cell(8, 7)
                });
            }

            _collisionMonitor = generated.AddComponent<OfficeGridCollisionMonitor>();
            _collisionMonitor.Configure(semanticGrid, _presenter, _movers);
        }

        private void Awake()
        {
            BuildPreview();
        }

        private OfficeGridCharacterMover CreateCharacter(
            string characterId,
            Sprite[] frames,
            OfficeGridCoordinate[] route)
        {
            var root = new GameObject("GridCharacter_" + characterId);
            root.transform.SetParent(_presenter.transform, false);
            var mover = root.AddComponent<OfficeGridCharacterMover>();
            mover.Configure(_presenter.SemanticGrid, _presenter, frames, route);
            _movers.Add(mover);
            return mover;
        }

        private void CreateSeatedCharacter(
            string characterId,
            Sprite[] frames,
            string seatId,
            OfficeGridCoordinate[] route,
            float navigationDelaySeconds)
        {
            var mover = CreateCharacter(characterId, frames, route);
            var frameSet = FindFrameSet(characterId);
            var seatedWorker = mover.gameObject.AddComponent<OfficeGridSeatedWorker>();
            seatedWorker.Configure(
                characterId,
                seatId,
                _presenter.SemanticGrid,
                _presenter,
                _furniturePresenter,
                mover,
                frameSet.sitDownFrames,
                frameSet.workFrames,
                frameSet.standUpFrames,
                navigationDelaySeconds);
            _seatedWorkers.Add(seatedWorker);
        }

        private OfficeGridSeatingFrameSet FindFrameSet(string memberId)
        {
            foreach (var frameSet in seatingFrameSets)
            {
                if (frameSet != null && string.Equals(frameSet.memberId, memberId, StringComparison.Ordinal))
                    return frameSet;
            }
            throw new InvalidOperationException("Missing grid seating frames for " + memberId + ".");
        }

        private static OfficeGridCoordinate Cell(int x, int y) => new OfficeGridCoordinate(x, y);

        private static T[] CloneRequired<T>(T[] source, int expectedCount, string parameterName)
            where T : UnityEngine.Object
        {
            if (source == null || source.Length != expectedCount)
                throw new ArgumentException($"Expected {expectedCount} assets.", parameterName);
            var result = (T[])source.Clone();
            for (var index = 0; index < result.Length; index++)
            {
                if (result[index] == null) throw new ArgumentException($"Asset {index} is null.", parameterName);
            }

            return result;
        }
    }
}
