using System;
using System.Collections.Generic;
using FamilyCompany.Presentation.Unity.OfficeGridView;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime
{
    /// <summary>
    /// Assigns every sprite in the office a sorting order from one footprint sort, once per frame.
    ///
    /// Ordering each sprite by its own anchor point cannot describe a 2x1 desk, which is how desk
    /// legs ended up drawn across a seated body and why the seated occupant needed a hand written
    /// exception. <see cref="OfficeIsometricDepth"/> orders whole cell footprints instead, so any
    /// arrangement the layout editor can produce comes out right without special cases.
    /// </summary>
    public sealed class OfficeRuntimeDepthSorter
    {
        private const string FurniturePrefix = "f:";
        private const string ActorPrefix = "a:";

        private readonly OfficeGrid _grid;
        private readonly OfficeGridTilemapPresenter _presenter;
        private readonly OfficeGridFurniturePresenter _furniturePresenter;
        private readonly List<OfficeDepthItem> _items = new List<OfficeDepthItem>();
        private readonly Dictionary<string, SpriteRenderer> _actorRenderers =
            new Dictionary<string, SpriteRenderer>(StringComparer.Ordinal);

        public OfficeRuntimeDepthSorter(
            OfficeGrid grid,
            OfficeGridTilemapPresenter presenter,
            OfficeGridFurniturePresenter furniturePresenter)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
            _furniturePresenter = furniturePresenter ?? throw new ArgumentNullException(nameof(furniturePresenter));
        }

        public int LastItemCount { get; private set; }

        public void Apply(IReadOnlyList<OfficeRuntimeAgent> actors)
        {
            _items.Clear();
            _actorRenderers.Clear();

            foreach (PlacedOfficeFurniture furniture in _grid.Furniture)
            {
                _items.Add(new OfficeDepthItem(
                    FurniturePrefix + furniture.FurnitureId,
                    furniture.Origin.X,
                    furniture.Origin.Y,
                    furniture.Origin.X + furniture.Width - 1,
                    furniture.Origin.Y + furniture.Height - 1));
            }

            if (actors != null)
            {
                foreach (OfficeRuntimeAgent actor in actors)
                {
                    if (actor == null || !actor.isActiveAndEnabled) continue;
                    SpriteRenderer renderer = actor.PresentationRenderer;
                    if (renderer == null || !renderer.enabled) continue;
                    OfficeGridCoordinate cell = ResolveActorCell(actor);
                    // A seated actor shares the chair's cell and must draw in front of it.
                    _items.Add(OfficeDepthItem.Cell(
                        ActorPrefix + actor.AgentId,
                        cell.X,
                        cell.Y,
                        actor.IsSeated ? 1 : 0));
                    _actorRenderers[actor.AgentId] = renderer;
                }
            }

            LastItemCount = _items.Count;
            IReadOnlyDictionary<string, int> orders = OfficeIsometricDepth.ResolveSortingOrders(_items);
            foreach (KeyValuePair<string, int> entry in orders)
            {
                if (entry.Key.StartsWith(FurniturePrefix, StringComparison.Ordinal))
                {
                    _furniturePresenter.ApplySortingOrder(
                        entry.Key.Substring(FurniturePrefix.Length),
                        entry.Value);
                    continue;
                }
                string agentId = entry.Key.Substring(ActorPrefix.Length);
                if (_actorRenderers.TryGetValue(agentId, out SpriteRenderer renderer) && renderer != null)
                    renderer.sortingOrder = entry.Value;
            }
        }

        private OfficeGridCoordinate ResolveActorCell(OfficeRuntimeAgent actor)
        {
            if (actor.IsSeated && actor.ActiveSeatId.Length > 0)
            {
                foreach (OfficeSeatSlot seat in _grid.SeatSlots)
                {
                    if (string.Equals(seat.SeatId, actor.ActiveSeatId, StringComparison.Ordinal))
                        return seat.Cell;
                }
            }
            return _presenter.NearestCell(actor.transform.position);
        }
    }
}
