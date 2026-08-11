using System;
using System.Collections.Generic;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeGridView
{
    [DisallowMultipleComponent]
    public sealed class OfficeGridCollisionMonitor : MonoBehaviour
    {
        private OfficeGrid _grid;
        private OfficeGridTilemapPresenter _presenter;
        private IReadOnlyList<OfficeGridCharacterMover> _movers;

        public int SampleCount { get; private set; }
        public int BlockedCellViolationCount { get; private set; }
        public string FirstViolation { get; private set; } = string.Empty;

        public void Configure(
            OfficeGrid grid,
            OfficeGridTilemapPresenter presenter,
            IReadOnlyList<OfficeGridCharacterMover> movers)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
            _movers = movers ?? throw new ArgumentNullException(nameof(movers));
            SampleCount = 0;
            BlockedCellViolationCount = 0;
            FirstViolation = string.Empty;
        }

        private void Update()
        {
            if (_grid == null || _presenter == null || _movers == null) return;
            for (var index = 0; index < _movers.Count; index++)
            {
                var mover = _movers[index];
                if (mover == null) continue;
                SampleCount++;
                var cell = _presenter.NearestCell(mover.transform.position);
                if (_grid.IsWalkable(cell)) continue;
                BlockedCellViolationCount++;
                if (FirstViolation.Length == 0)
                    FirstViolation = $"{mover.name} entered nearest blocked cell {cell} at {mover.transform.position}.";
            }
        }
    }
}
