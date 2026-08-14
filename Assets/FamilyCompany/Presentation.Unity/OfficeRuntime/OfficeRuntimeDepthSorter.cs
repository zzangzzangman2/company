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
        private const string FrontPrefix = "n:";
        private const string ActorPrefix = "a:";

        /// <summary>Chair base under the occupant.</summary>
        private const int BasePriority = 0;

        /// <summary>The person on the seat.</summary>
        private const int OccupantPriority = 1;

        /// <summary>
        /// Backrest and near armrest. The camera looks at the occupant's back, so the parts of the
        /// chair their back rests against are between them and the camera and have to be painted
        /// over the body - otherwise the hips read as poking through the seat.
        /// </summary>
        private const int FrontPriority = 2;

        /// <summary>The chair's near back/arm redraws after the desk lip.</summary>
        private const int ChairFrontPriority = 3;

        private readonly OfficeGrid _grid;
        private readonly OfficeGridTilemapPresenter _presenter;
        private readonly OfficeGridFurniturePresenter _furniturePresenter;
        private readonly List<OfficeDepthItem> _items = new List<OfficeDepthItem>();
        private readonly Dictionary<string, SpriteRenderer> _actorRenderers =
            new Dictionary<string, SpriteRenderer>(StringComparer.Ordinal);
        private readonly Dictionary<string, OfficeSeatSlot> _seatsById =
            new Dictionary<string, OfficeSeatSlot>(StringComparer.Ordinal);
        private readonly Dictionary<string, OfficeSeatSlot> _seatsByFurnitureId =
            new Dictionary<string, OfficeSeatSlot>(StringComparer.Ordinal);
        private readonly Dictionary<string, OfficeRuntimeAgent> _activeSeatOccupants =
            new Dictionary<string, OfficeRuntimeAgent>(StringComparer.Ordinal);

        public OfficeRuntimeDepthSorter(
            OfficeGrid grid,
            OfficeGridTilemapPresenter presenter,
            OfficeGridFurniturePresenter furniturePresenter)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
            _furniturePresenter = furniturePresenter ?? throw new ArgumentNullException(nameof(furniturePresenter));
            foreach (OfficeSeatSlot seat in _grid.SeatSlots)
            {
                _seatsById.Add(seat.SeatId, seat);
                _seatsByFurnitureId[seat.ChairFurnitureId] = seat;
                if (seat.HasWorkstationBinding)
                    _seatsByFurnitureId[seat.WorkSurfaceFurnitureId] = seat;
            }
        }

        public int LastItemCount { get; private set; }

        public void Apply(IReadOnlyList<OfficeRuntimeAgent> actors)
        {
            _items.Clear();
            _actorRenderers.Clear();
            _activeSeatOccupants.Clear();

            if (actors != null)
            {
                foreach (OfficeRuntimeAgent actor in actors)
                {
                    if (actor == null || !actor.isActiveAndEnabled || !actor.IsOccupyingSeat ||
                        actor.ActiveSeatId.Length == 0) continue;
                    _activeSeatOccupants[actor.ActiveSeatId] = actor;
                }
            }

            foreach (PlacedOfficeFurniture furniture in _grid.Furniture)
            {
                int maxX = furniture.Origin.X + furniture.Width - 1;
                int maxY = furniture.Origin.Y + furniture.Height - 1;
                _items.Add(new OfficeDepthItem(
                    FurniturePrefix + furniture.FurnitureId,
                    furniture.Origin.X,
                    furniture.Origin.Y,
                    maxX,
                    maxY,
                    BasePriority));
                if (_furniturePresenter.HasEnabledFrontOverlay(furniture.FurnitureId))
                {
                    ResolveForegroundDepth(
                        furniture,
                        out int frontMinX,
                        out int frontMinY,
                        out int frontMaxX,
                        out int frontMaxY,
                        out int frontPriority);
                    _items.Add(new OfficeDepthItem(
                        FrontPrefix + furniture.FurnitureId,
                        frontMinX,
                        frontMinY,
                        frontMaxX,
                        frontMaxY,
                        frontPriority));
                }
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
                        actor.IsOccupyingSeat ? OccupantPriority : BasePriority));
                    _actorRenderers[actor.AgentId] = renderer;
                }
            }

            LastItemCount = _items.Count;
            IReadOnlyDictionary<string, int> orders = OfficeIsometricDepth.ResolveSortingOrders(_items);
            foreach (KeyValuePair<string, int> entry in orders)
            {
                if (entry.Key.StartsWith(FurniturePrefix, StringComparison.Ordinal))
                {
                    _furniturePresenter.ApplyBaseSortingOrder(
                        entry.Key.Substring(FurniturePrefix.Length),
                        entry.Value);
                    continue;
                }
                if (entry.Key.StartsWith(FrontPrefix, StringComparison.Ordinal))
                {
                    _furniturePresenter.ApplyFrontSortingOrder(
                        entry.Key.Substring(FrontPrefix.Length),
                        entry.Value);
                    continue;
                }
                string agentId = entry.Key.Substring(ActorPrefix.Length);
                if (_actorRenderers.TryGetValue(agentId, out SpriteRenderer renderer) && renderer != null)
                    renderer.sortingOrder = entry.Value;
            }
            RecordSeatingDepthSamples(actors, orders);
        }

        private void ResolveForegroundDepth(
            PlacedOfficeFurniture furniture,
            out int minX,
            out int minY,
            out int maxX,
            out int maxY,
            out int priority)
        {
            minX = furniture.Origin.X;
            minY = furniture.Origin.Y;
            maxX = furniture.Origin.X + furniture.Width - 1;
            maxY = furniture.Origin.Y + furniture.Height - 1;
            priority = FrontPriority;
            if (!_seatsByFurnitureId.TryGetValue(furniture.FurnitureId, out OfficeSeatSlot seat) ||
                !_activeSeatOccupants.TryGetValue(seat.SeatId, out OfficeRuntimeAgent occupant)) return;

            if (occupant.IsSeatForegroundOcclusionEngaged)
            {
                // Both redraw masks bind to the interaction socket while the body is behind their
                // foreground planes. This lets a 2x1 desk front share depth with its operator even
                // though the desk base keeps its full semantic footprint.
                minX = maxX = seat.Cell.X;
                minY = maxY = seat.Cell.Y;
                priority = string.Equals(
                    furniture.FurnitureId,
                    seat.ChairFurnitureId,
                    StringComparison.Ordinal)
                    ? ChairFrontPriority
                    : FrontPriority;
                return;
            }

            // The reservation remains active during the exit. Once the actor crosses the chair
            // plane, keep the redraw mask with the furniture base so it cannot slice the departing
            // body merely because the seat claim has not yet been released.
            priority = BasePriority;
        }

        private void RecordSeatingDepthSamples(
            IReadOnlyList<OfficeRuntimeAgent> actors,
            IReadOnlyDictionary<string, int> orders)
        {
            if (actors == null) return;
            foreach (OfficeRuntimeAgent actor in actors)
            {
                if (actor == null || !actor.isActiveAndEnabled || !actor.IsOccupyingSeat ||
                    actor.ActiveSeatId.Length == 0 ||
                    !_seatsById.TryGetValue(actor.ActiveSeatId, out OfficeSeatSlot seat) ||
                    !orders.TryGetValue(ActorPrefix + actor.AgentId, out int actorOrder) ||
                    !orders.TryGetValue(FurniturePrefix + seat.ChairFurnitureId, out int chairBaseOrder))
                    continue;

                bool hasChairFront = orders.TryGetValue(
                    FrontPrefix + seat.ChairFurnitureId,
                    out int chairFrontOrder);
                var deskBaseOrder = 0;
                var deskFrontOrder = 0;
                bool hasDesk = seat.HasWorkstationBinding && orders.TryGetValue(
                    FurniturePrefix + seat.WorkSurfaceFurnitureId,
                    out deskBaseOrder);
                bool hasDeskFront = hasDesk && orders.TryGetValue(
                    FrontPrefix + seat.WorkSurfaceFurnitureId,
                    out deskFrontOrder);
                actor.RecordSeatingDepthSample(new OfficeSeatingDepthSnapshot(
                    actor.Phase,
                    actor.CurrentSeatingClip,
                    actor.CurrentSeatingFrame,
                    actor.IsSeatForegroundOcclusionEngaged,
                    actorOrder,
                    chairBaseOrder,
                    hasChairFront,
                    chairFrontOrder,
                    hasDesk,
                    deskBaseOrder,
                    hasDeskFront,
                    deskFrontOrder));
            }
        }

        private OfficeGridCoordinate ResolveActorCell(OfficeRuntimeAgent actor)
        {
            if (actor.IsSeatForegroundOcclusionEngaged && actor.ActiveSeatId.Length > 0)
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
