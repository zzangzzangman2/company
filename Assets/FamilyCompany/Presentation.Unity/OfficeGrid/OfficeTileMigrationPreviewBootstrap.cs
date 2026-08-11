using System;
using System.Collections.Generic;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace FamilyCompany.Presentation.Unity.OfficeGridView
{
    [DisallowMultipleComponent]
    public sealed class OfficeTileMigrationPreviewBootstrap : MonoBehaviour
    {
        [SerializeField] private bool includeCharacters = true;
        [SerializeField] private TileBase[] floorTiles = Array.Empty<TileBase>();
        [SerializeField] private Sprite[] playerFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] sisterFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] fatherFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] motherFrames = Array.Empty<Sprite>();

        private OfficeGridTilemapPresenter _presenter;
        private readonly List<OfficeGridCharacterMover> _movers = new List<OfficeGridCharacterMover>();

        public OfficeGridTilemapPresenter Presenter => _presenter;
        public IReadOnlyList<OfficeGridCharacterMover> Movers => _movers;

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

        public void BuildPreview()
        {
            var existing = transform.Find("GeneratedOfficeTilePreview");
            if (existing != null)
            {
                if (Application.isPlaying) Destroy(existing.gameObject);
                else DestroyImmediate(existing.gameObject);
            }

            _movers.Clear();
            var semanticGrid = OfficeGridLayouts.CreateMigrationPreview();
            var generated = new GameObject("GeneratedOfficeTilePreview");
            generated.transform.SetParent(transform, false);
            _presenter = generated.AddComponent<OfficeGridTilemapPresenter>();
            _presenter.Configure(semanticGrid, floorTiles);
            if (!includeCharacters) return;

            CreateCharacter("player", playerFrames, new[]
            {
                Cell(2, 2), Cell(10, 2), Cell(10, 10), Cell(2, 10)
            });
            CreateCharacter("older_sister", sisterFrames, new[]
            {
                Cell(3, 3), Cell(9, 3), Cell(9, 9), Cell(3, 9)
            });
            CreateCharacter("father", fatherFrames, new[]
            {
                Cell(2, 9), Cell(2, 3), Cell(5, 3), Cell(5, 9)
            });
            CreateCharacter("mother", motherFrames, new[]
            {
                Cell(10, 3), Cell(10, 9), Cell(8, 9), Cell(8, 3)
            });
        }

        private void Awake()
        {
            BuildPreview();
        }

        private void CreateCharacter(string characterId, Sprite[] frames, OfficeGridCoordinate[] route)
        {
            var root = new GameObject("GridCharacter_" + characterId);
            root.transform.SetParent(_presenter.transform, false);
            var mover = root.AddComponent<OfficeGridCharacterMover>();
            mover.Configure(_presenter.SemanticGrid, _presenter, frames, route);
            _movers.Add(mover);
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
