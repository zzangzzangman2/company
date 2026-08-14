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
        private const string UpperActorPrefix = "u:";

        /// <summary>Legacy stack priority for a furniture body.</summary>
        private const int BasePriority = 0;

        /// <summary>Legacy stack priority for a real authored foreground sprite.</summary>
        private const int FrontPriority = 2;

        /// <summary>Legacy stack priority for the chair's authored foreground sprite.</summary>
        private const int ChairFrontPriority = 3;

        // A live seat owns an explicit semantic stack. The engaged planes remain in force through
        // the complete reserved dismount. Released planes are used only by the planted SitDown[0]
        // entry gate; the seat claim itself is released atomically at the safe egress anchor.
        private const int SeatDeskBasePlane = 0;
        private const int SeatChairBasePlane = 1;
        private const int SeatActorEngagedPlane = 2;
        private const int SeatDeskFrontEngagedPlane = 3;
        private const int SeatChairFrontEngagedPlane = 4;
        private const int SeatActorUpperBodyEngagedPlane = 5;
        private const int SeatDeskFrontReleasedPlane = 2;
        private const int SeatChairFrontReleasedPlane = 3;
        private const int SeatActorReleasedPlane = 4;

        private readonly OfficeGrid _grid;
        private readonly OfficeGridTilemapPresenter _presenter;
        private readonly OfficeGridFurniturePresenter _furniturePresenter;
        private readonly List<OfficeHybridDepthItem> _items = new List<OfficeHybridDepthItem>();
        private readonly Dictionary<string, SpriteRenderer> _actorRenderers =
            new Dictionary<string, SpriteRenderer>(StringComparer.Ordinal);
        private readonly Dictionary<string, OfficeRuntimeAgent> _actorAgents =
            new Dictionary<string, OfficeRuntimeAgent>(StringComparer.Ordinal);
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
            _actorAgents.Clear();
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
                bool hasSeatStack = TryResolveSeatStack(
                    furniture,
                    out OfficeSeatSlot seat,
                    out bool foregroundEngaged);
                bool isChair = hasSeatStack && string.Equals(
                    furniture.FurnitureId,
                    seat.ChairFurnitureId,
                    StringComparison.Ordinal);
                if (string.Equals(
                        furniture.KindId,
                        OfficeGridLayouts.SwivelChairKind,
                        StringComparison.Ordinal))
                    _furniturePresenter.ApplyOccupiedChairForeground(
                        furniture.FurnitureId,
                        isChair && foregroundEngaged);
                string seatStackId = hasSeatStack ? seat.SeatId : string.Empty;
                _items.Add(OfficeHybridDepthItem.Furniture(
                    new OfficeDepthItem(
                        FurniturePrefix + furniture.FurnitureId,
                        furniture.Origin.X,
                        furniture.Origin.Y,
                        maxX,
                        maxY,
                        BasePriority),
                    isChair
                        ? OfficeHybridDepthRole.ChairBase
                        : OfficeHybridDepthRole.FurnitureBase,
                    furniture.KindId + ":base",
                    furniture.FurnitureId,
                    seatStackId,
                    isChair ? SeatChairBasePlane : SeatDeskBasePlane));
                if (_furniturePresenter.HasEnabledFrontOverlay(furniture.FurnitureId))
                {
                    OfficeHybridDepthRole frontRole = !hasSeatStack || !foregroundEngaged
                        ? OfficeHybridDepthRole.FurnitureFront
                        : isChair
                            ? OfficeHybridDepthRole.ChairFront
                            : OfficeHybridDepthRole.DeskFront;
                    int frontPlane = !hasSeatStack
                        ? 0
                        : foregroundEngaged
                            ? isChair
                                ? SeatChairFrontEngagedPlane
                                : SeatDeskFrontEngagedPlane
                            : isChair
                                ? SeatChairFrontReleasedPlane
                                : SeatDeskFrontReleasedPlane;
                    _items.Add(OfficeHybridDepthItem.Furniture(
                        new OfficeDepthItem(
                            FrontPrefix + furniture.FurnitureId,
                            furniture.Origin.X,
                            furniture.Origin.Y,
                            maxX,
                            maxY,
                            isChair && foregroundEngaged
                                ? ChairFrontPriority
                                : FrontPriority),
                        frontRole,
                        furniture.KindId + ":front",
                        furniture.FurnitureId,
                        seatStackId,
                        frontPlane));
                }
            }

            if (actors != null)
            {
                ResolveGridBasis(
                    out Vector2 gridOriginWorld,
                    out Vector2 basisXWorld,
                    out Vector2 basisYWorld,
                    out double basisDeterminant);
                foreach (OfficeRuntimeAgent actor in actors)
                {
                    if (actor == null || !actor.isActiveAndEnabled) continue;
                    SpriteRenderer renderer = actor.PresentationRenderer;
                    if (renderer == null || !renderer.enabled) continue;
                    ResolveActorGridContact(
                        actor.Position,
                        gridOriginWorld,
                        basisXWorld,
                        basisYWorld,
                        basisDeterminant,
                        out int pointXQ,
                        out int pointYQ);
                    bool hasSeatStack = actor.IsOccupyingSeat &&
                                        actor.ActiveSeatId.Length > 0 &&
                                        _seatsById.ContainsKey(actor.ActiveSeatId);
                    _items.Add(OfficeHybridDepthItem.Actor(
                        ActorPrefix + actor.AgentId,
                        pointXQ,
                        pointYQ,
                        "office-runtime-actor",
                        actor.AgentId,
                        hasSeatStack ? actor.ActiveSeatId : string.Empty,
                        hasSeatStack
                            ? actor.IsSeatForegroundOcclusionEngaged
                                ? SeatActorEngagedPlane
                                : SeatActorReleasedPlane
                            : 0));
                    _actorRenderers[actor.AgentId] = renderer;
                    _actorAgents[actor.AgentId] = actor;
                    if (hasSeatStack && actor.IsSeatForegroundOcclusionEngaged)
                    {
                        _items.Add(OfficeHybridDepthItem.Actor(
                            UpperActorPrefix + actor.AgentId,
                            pointXQ,
                            pointYQ,
                            "office-runtime-seated-upper-body",
                            actor.AgentId + ":upper-body",
                            actor.ActiveSeatId,
                            SeatActorUpperBodyEngagedPlane));
                    }
                }
            }

            LastItemCount = _items.Count;
            IReadOnlyDictionary<string, int> orders =
                OfficeHybridContinuousDepth.ResolveSortingOrders(_items);
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
                if (entry.Key.StartsWith(UpperActorPrefix, StringComparison.Ordinal)) continue;
                string agentId = entry.Key.Substring(ActorPrefix.Length);
                if (_actorRenderers.TryGetValue(agentId, out SpriteRenderer renderer) && renderer != null)
                    renderer.sortingOrder = entry.Value;
            }
            foreach (KeyValuePair<string, OfficeRuntimeAgent> entry in _actorAgents)
            {
                if (orders.TryGetValue(UpperActorPrefix + entry.Key, out int upperBodyOrder))
                    entry.Value.ApplySeatedUpperBodyProtection(upperBodyOrder);
                else
                    entry.Value.ClearSeatedUpperBodyProtection();
            }
            RecordSeatingDepthSamples(actors, orders);
        }

        private bool TryResolveSeatStack(
            PlacedOfficeFurniture furniture,
            out OfficeSeatSlot seat,
            out bool foregroundEngaged)
        {
            foregroundEngaged = false;
            if (!_seatsByFurnitureId.TryGetValue(furniture.FurnitureId, out seat) ||
                !_activeSeatOccupants.TryGetValue(seat.SeatId, out OfficeRuntimeAgent occupant))
            {
                seat = null;
                return false;
            }
            foregroundEngaged = occupant.IsSeatForegroundOcclusionEngaged;
            return true;
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

        private void ResolveGridBasis(
            out Vector2 originWorld,
            out Vector2 basisXWorld,
            out Vector2 basisYWorld,
            out double determinant)
        {
            Vector3 origin = _presenter.CellCenterWorld(new OfficeGridCoordinate(0, 0));
            Vector3 basisX = _grid.Width > 1
                ? _presenter.CellCenterWorld(new OfficeGridCoordinate(1, 0)) - origin
                : new Vector3(
                    OfficeGridTilemapPresenter.TileWorldWidth * 0.5f,
                    OfficeGridTilemapPresenter.TileWorldHeight * 0.5f,
                    0f);
            Vector3 basisY = _grid.Height > 1
                ? _presenter.CellCenterWorld(new OfficeGridCoordinate(0, 1)) - origin
                : new Vector3(
                    -OfficeGridTilemapPresenter.TileWorldWidth * 0.5f,
                    OfficeGridTilemapPresenter.TileWorldHeight * 0.5f,
                    0f);
            originWorld = new Vector2(origin.x, origin.y);
            basisXWorld = new Vector2(basisX.x, basisX.y);
            basisYWorld = new Vector2(basisY.x, basisY.y);
            determinant = (double)basisXWorld.x * basisYWorld.y -
                          (double)basisXWorld.y * basisYWorld.x;
            if (Math.Abs(determinant) <= 0.000000001d)
                throw new InvalidOperationException("Office grid basis is singular.");
        }

        private static void ResolveActorGridContact(
            Vector2 actorWorld,
            Vector2 originWorld,
            Vector2 basisXWorld,
            Vector2 basisYWorld,
            double determinant,
            out int pointXQ,
            out int pointYQ)
        {
            double deltaX = actorWorld.x - originWorld.x;
            double deltaY = actorWorld.y - originWorld.y;
            double gridX = (deltaX * basisYWorld.y - deltaY * basisYWorld.x) / determinant;
            double gridY = (basisXWorld.x * deltaY - basisXWorld.y * deltaX) / determinant;
            pointXQ = OfficeHybridContinuousDepth.Quantize(gridX);
            pointYQ = OfficeHybridContinuousDepth.Quantize(gridY);
        }
    }
}
