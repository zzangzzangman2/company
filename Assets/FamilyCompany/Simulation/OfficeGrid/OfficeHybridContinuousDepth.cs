using System;
using System.Collections.Generic;

namespace FamilyCompany.Simulation.OfficeLayout
{
    public enum OfficeHybridDepthItemKind
    {
        Furniture = 0,
        Actor = 1
    }

    /// <summary>
    /// Semantic painter roles. Values are deliberately ordered back-to-front for an exact-depth
    /// fallback; an engaged seat uses its explicit SeatPlane instead.
    /// </summary>
    public enum OfficeHybridDepthRole
    {
        FurnitureBase = 0,
        ChairBase = 1,
        FurnitureFront = 2,
        Actor = 3,
        DeskFront = 4,
        ChairFront = 5
    }

    /// <summary>
    /// One pure-data item for hybrid isometric depth. Furniture retains the legacy integer
    /// footprint and stack priority verbatim. Actors carry a quantized continuous grid contact.
    /// No camera, renderer bounds, sprite height, or animation frame participates in this key.
    /// </summary>
    public readonly struct OfficeHybridDepthItem
    {
        private readonly OfficeDepthItem _furnitureFootprint;

        private OfficeHybridDepthItem(
            string id,
            OfficeHybridDepthItemKind kind,
            OfficeDepthItem furnitureFootprint,
            int pointXQ,
            int pointYQ,
            OfficeHybridDepthRole role,
            string semanticId,
            string instanceId,
            string seatStackId,
            int seatPlane)
        {
            Id = Required(id, nameof(id));
            Kind = kind;
            _furnitureFootprint = furnitureFootprint;
            PointXQ = pointXQ;
            PointYQ = pointYQ;
            Role = role;
            SemanticId = Required(semanticId, nameof(semanticId));
            InstanceId = Required(instanceId, nameof(instanceId));
            SeatStackId = seatStackId == null ? string.Empty : seatStackId.Trim();
            SeatPlane = seatPlane;
        }

        public static OfficeHybridDepthItem Furniture(
            OfficeDepthItem legacyFootprint,
            OfficeHybridDepthRole role,
            string semanticId,
            string instanceId,
            string seatStackId = "",
            int seatPlane = 0)
        {
            return new OfficeHybridDepthItem(
                legacyFootprint.Id,
                OfficeHybridDepthItemKind.Furniture,
                legacyFootprint,
                0,
                0,
                role,
                semanticId,
                instanceId,
                seatStackId,
                seatPlane);
        }

        public static OfficeHybridDepthItem Actor(
            string id,
            int pointXQ,
            int pointYQ,
            string semanticId,
            string instanceId,
            string seatStackId = "",
            int seatPlane = 0)
        {
            return new OfficeHybridDepthItem(
                id,
                OfficeHybridDepthItemKind.Actor,
                default,
                pointXQ,
                pointYQ,
                OfficeHybridDepthRole.Actor,
                semanticId,
                instanceId,
                seatStackId,
                seatPlane);
        }

        public static OfficeHybridDepthItem ActorAtGridPosition(
            string id,
            double gridX,
            double gridY,
            string semanticId,
            string instanceId,
            string seatStackId = "",
            int seatPlane = 0)
        {
            return Actor(
                id,
                OfficeHybridContinuousDepth.Quantize(gridX),
                OfficeHybridContinuousDepth.Quantize(gridY),
                semanticId,
                instanceId,
                seatStackId,
                seatPlane);
        }

        public string Id { get; }
        public OfficeHybridDepthItemKind Kind { get; }
        public bool IsFurniture => Kind == OfficeHybridDepthItemKind.Furniture;
        public bool IsActor => Kind == OfficeHybridDepthItemKind.Actor;
        public int PointXQ { get; }
        public int PointYQ { get; }
        public OfficeHybridDepthRole Role { get; }
        public string SemanticId { get; }
        public string InstanceId { get; }
        public string SeatStackId { get; }
        public bool HasSeatStack => SeatStackId.Length > 0;
        public int SeatPlane { get; }

        public OfficeDepthItem FurnitureFootprint
        {
            get
            {
                if (!IsFurniture)
                    throw new InvalidOperationException("An actor has no furniture footprint.");
                return _furnitureFootprint;
            }
        }

        internal long FallbackNearXQ => IsFurniture
            ? checked((long)_furnitureFootprint.MinX * OfficeHybridContinuousDepth.Quantization)
            : PointXQ;
        internal long FallbackNearYQ => IsFurniture
            ? checked((long)_furnitureFootprint.MinY * OfficeHybridContinuousDepth.Quantization)
            : PointYQ;
        internal long FallbackFarXQ => IsFurniture
            ? checked((long)_furnitureFootprint.MaxX * OfficeHybridContinuousDepth.Quantization)
            : PointXQ;
        internal long FallbackFarYQ => IsFurniture
            ? checked((long)_furnitureFootprint.MaxY * OfficeHybridContinuousDepth.Quantization)
            : PointYQ;

        internal long PhysicalNearXQ => IsFurniture
            ? checked((long)_furnitureFootprint.MinX * OfficeHybridContinuousDepth.Quantization -
                      OfficeHybridContinuousDepth.HalfCellQ)
            : PointXQ;
        internal long PhysicalNearYQ => IsFurniture
            ? checked((long)_furnitureFootprint.MinY * OfficeHybridContinuousDepth.Quantization -
                      OfficeHybridContinuousDepth.HalfCellQ)
            : PointYQ;
        internal long PhysicalFarXQ => IsFurniture
            ? checked((long)_furnitureFootprint.MaxX * OfficeHybridContinuousDepth.Quantization +
                      OfficeHybridContinuousDepth.HalfCellQ)
            : PointXQ;
        internal long PhysicalFarYQ => IsFurniture
            ? checked((long)_furnitureFootprint.MaxY * OfficeHybridContinuousDepth.Quantization +
                      OfficeHybridContinuousDepth.HalfCellQ)
            : PointYQ;

        private static string Required(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Depth identity is required.", parameterName);
            return value.Trim();
        }
    }

    /// <summary>
    /// Reusable storage for the per-frame runtime depth sort. Results remain valid until the next
    /// call that uses the same workspace. Tests and one-shot callers can keep using the allocating
    /// overloads below.
    /// </summary>
    public sealed class OfficeHybridDepthSortWorkspace
    {
        internal readonly HashSet<string> Ids = new HashSet<string>(StringComparer.Ordinal);
        internal readonly List<int> Pending;
        internal readonly List<OfficeHybridDepthItem> Ordered;
        internal readonly Dictionary<string, int> Orders;
        internal int[] BehindCount;
        internal List<int>[] InFrontOf;
        internal bool[] Emitted;
        private IReadOnlyList<OfficeHybridDepthItem> _items;
        private readonly Comparison<int> _pendingComparison;

        public OfficeHybridDepthSortWorkspace(int capacity = 0)
        {
            if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            Pending = new List<int>(capacity);
            Ordered = new List<OfficeHybridDepthItem>(capacity);
            Orders = new Dictionary<string, int>(capacity, StringComparer.Ordinal);
            BehindCount = new int[capacity];
            InFrontOf = new List<int>[capacity];
            Emitted = new bool[capacity];
            for (var index = 0; index < capacity; index++) InFrontOf[index] = new List<int>();
            _pendingComparison = ComparePending;
        }

        internal void Prepare(IReadOnlyList<OfficeHybridDepthItem> items)
        {
            _items = items;
            int count = items.Count;
            if (BehindCount.Length < count)
            {
                int previous = BehindCount.Length;
                int capacity = Math.Max(count, Math.Max(4, previous * 2));
                Array.Resize(ref BehindCount, capacity);
                Array.Resize(ref InFrontOf, capacity);
                Array.Resize(ref Emitted, capacity);
                for (var index = previous; index < capacity; index++)
                    InFrontOf[index] = new List<int>();
                if (Pending.Capacity < capacity) Pending.Capacity = capacity;
                if (Ordered.Capacity < capacity) Ordered.Capacity = capacity;
                Orders.EnsureCapacity(capacity);
            }
            Ids.Clear();
            Pending.Clear();
            Ordered.Clear();
            Orders.Clear();
            Array.Clear(BehindCount, 0, count);
            Array.Clear(Emitted, 0, count);
            for (var index = 0; index < count; index++) InFrontOf[index].Clear();
        }

        internal void SortPending() => Pending.Sort(_pendingComparison);

        private int ComparePending(int left, int right) =>
            OfficeHybridContinuousDepth.CompareFallback(_items[left], _items[right]);
    }

    /// <summary>
    /// Stable hybrid painter ordering. Integer furniture-only input is exactly compatible with
    /// <see cref="OfficeIsometricDepth"/>. Continuous actor contacts use two isometric grid axes,
    /// because a scalar x+y key cannot correctly classify both sides of a multi-cell footprint.
    /// </summary>
    public static class OfficeHybridContinuousDepth
    {
        public const int Quantization = 256;
        public const int HalfCellQ = Quantization / 2;

        public static int Quantize(double gridCoordinate)
        {
            if (double.IsNaN(gridCoordinate) || double.IsInfinity(gridCoordinate))
                throw new ArgumentOutOfRangeException(nameof(gridCoordinate));
            return checked((int)Math.Round(
                gridCoordinate * Quantization,
                MidpointRounding.AwayFromZero));
        }

        public static OfficeDepthRelation Compare(
            OfficeHybridDepthItem first,
            OfficeHybridDepthItem second)
        {
            if (string.Equals(first.Id, second.Id, StringComparison.Ordinal))
                return OfficeDepthRelation.Unrelated;

            if (first.HasSeatStack && second.HasSeatStack &&
                string.Equals(first.SeatStackId, second.SeatStackId, StringComparison.Ordinal) &&
                first.SeatPlane != second.SeatPlane)
            {
                return first.SeatPlane < second.SeatPlane
                    ? OfficeDepthRelation.FirstBehindSecond
                    : OfficeDepthRelation.SecondBehindFirst;
            }

            if (first.IsFurniture && second.IsFurniture)
                return OfficeIsometricDepth.Compare(
                    first.FurnitureFootprint,
                    second.FurnitureFootprint);

            return CompareRectangles(
                first.PhysicalNearXQ,
                first.PhysicalNearYQ,
                first.PhysicalFarXQ,
                first.PhysicalFarYQ,
                second.PhysicalNearXQ,
                second.PhysicalNearYQ,
                second.PhysicalFarXQ,
                second.PhysicalFarYQ);
        }

        public static IReadOnlyList<OfficeHybridDepthItem> Sort(
            IReadOnlyList<OfficeHybridDepthItem> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            return Sort(items, new OfficeHybridDepthSortWorkspace(items.Count));
        }

        public static IReadOnlyList<OfficeHybridDepthItem> Sort(
            IReadOnlyList<OfficeHybridDepthItem> items,
            OfficeHybridDepthSortWorkspace workspace)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (workspace == null) throw new ArgumentNullException(nameof(workspace));
            int count = items.Count;
            workspace.Prepare(items);
            if (count == 0) return workspace.Ordered;

            for (var index = 0; index < count; index++)
            {
                OfficeHybridDepthItem item = items[index];
                if (!workspace.Ids.Add(item.Id))
                    throw new ArgumentException(
                        "Duplicate depth item id: " + item.Id,
                        nameof(items));
            }

            for (var index = 0; index < count; index++) workspace.Pending.Add(index);
            workspace.SortPending();

            for (var firstIndex = 0; firstIndex < count; firstIndex++)
            for (var secondIndex = firstIndex + 1; secondIndex < count; secondIndex++)
            {
                switch (Compare(items[firstIndex], items[secondIndex]))
                {
                    case OfficeDepthRelation.FirstBehindSecond:
                        workspace.InFrontOf[firstIndex].Add(secondIndex);
                        workspace.BehindCount[secondIndex]++;
                        break;
                    case OfficeDepthRelation.SecondBehindFirst:
                        workspace.InFrontOf[secondIndex].Add(firstIndex);
                        workspace.BehindCount[firstIndex]++;
                        break;
                }
            }

            while (workspace.Ordered.Count < count)
            {
                var chosen = -1;
                for (var pendingIndex = 0; pendingIndex < workspace.Pending.Count; pendingIndex++)
                {
                    int index = workspace.Pending[pendingIndex];
                    if (workspace.Emitted[index] || workspace.BehindCount[index] != 0) continue;
                    chosen = index;
                    break;
                }
                if (chosen < 0)
                {
                    // Invalid spatial input can form a cycle. Break it by the same stable fallback
                    // rather than making output depend on caller insertion order.
                    for (var pendingIndex = 0;
                         pendingIndex < workspace.Pending.Count;
                         pendingIndex++)
                    {
                        int index = workspace.Pending[pendingIndex];
                        if (workspace.Emitted[index]) continue;
                        chosen = index;
                        break;
                    }
                }

                workspace.Emitted[chosen] = true;
                workspace.BehindCount[chosen] = -1;
                workspace.Ordered.Add(items[chosen]);
                List<int> aheadItems = workspace.InFrontOf[chosen];
                for (var index = 0; index < aheadItems.Count; index++)
                {
                    int ahead = aheadItems[index];
                    if (workspace.BehindCount[ahead] > 0) workspace.BehindCount[ahead]--;
                }
            }
            return workspace.Ordered;
        }

        public static IReadOnlyDictionary<string, int> ResolveSortingOrders(
            IReadOnlyList<OfficeHybridDepthItem> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            return ResolveSortingOrders(
                items,
                new OfficeHybridDepthSortWorkspace(items.Count));
        }

        public static IReadOnlyDictionary<string, int> ResolveSortingOrders(
            IReadOnlyList<OfficeHybridDepthItem> items,
            OfficeHybridDepthSortWorkspace workspace)
        {
            IReadOnlyList<OfficeHybridDepthItem> ordered = Sort(items, workspace);
            for (var index = 0; index < ordered.Count; index++)
                workspace.Orders.Add(
                    ordered[index].Id,
                    OfficeIsometricDepth.BaseSortingOrder + index);
            return workspace.Orders;
        }

        private static OfficeDepthRelation CompareRectangles(
            long firstNearX,
            long firstNearY,
            long firstFarX,
            long firstFarY,
            long secondNearX,
            long secondNearY,
            long secondFarX,
            long secondFarY)
        {
            bool firstPastX = firstNearX > secondFarX;
            bool secondPastX = secondNearX > firstFarX;
            bool firstPastY = firstNearY > secondFarY;
            bool secondPastY = secondNearY > firstFarY;
            if ((firstPastX && secondPastY) || (secondPastX && firstPastY))
                return OfficeDepthRelation.Unrelated;
            if (firstPastX || firstPastY) return OfficeDepthRelation.FirstBehindSecond;
            if (secondPastX || secondPastY) return OfficeDepthRelation.SecondBehindFirst;
            return OfficeDepthRelation.Unrelated;
        }

        internal static int CompareFallback(
            OfficeHybridDepthItem left,
            OfficeHybridDepthItem right)
        {
            if (left.IsFurniture && right.IsFurniture)
                return CompareLegacyFurnitureFallback(
                    left.FurnitureFootprint,
                    right.FurnitureFootprint);

            long leftFar = checked(left.FallbackFarXQ + left.FallbackFarYQ);
            long rightFar = checked(right.FallbackFarXQ + right.FallbackFarYQ);
            if (leftFar != rightFar) return rightFar.CompareTo(leftFar);
            long leftNear = checked(left.FallbackNearXQ + left.FallbackNearYQ);
            long rightNear = checked(right.FallbackNearXQ + right.FallbackNearYQ);
            if (leftNear != rightNear) return rightNear.CompareTo(leftNear);
            if (left.Role != right.Role) return left.Role.CompareTo(right.Role);
            if (left.SeatPlane != right.SeatPlane)
                return left.SeatPlane.CompareTo(right.SeatPlane);
            long leftLateral = checked(left.FallbackNearXQ - left.FallbackNearYQ);
            long rightLateral = checked(right.FallbackNearXQ - right.FallbackNearYQ);
            if (leftLateral != rightLateral) return leftLateral.CompareTo(rightLateral);
            int semantic = string.CompareOrdinal(left.SemanticId, right.SemanticId);
            if (semantic != 0) return semantic;
            int instance = string.CompareOrdinal(left.InstanceId, right.InstanceId);
            if (instance != 0) return instance;
            return string.CompareOrdinal(left.Id, right.Id);
        }

        private static int CompareLegacyFurnitureFallback(
            OfficeDepthItem left,
            OfficeDepthItem right)
        {
            int leftFar = checked(left.MaxX + left.MaxY);
            int rightFar = checked(right.MaxX + right.MaxY);
            if (leftFar != rightFar) return rightFar.CompareTo(leftFar);
            int leftNear = checked(left.MinX + left.MinY);
            int rightNear = checked(right.MinX + right.MinY);
            if (leftNear != rightNear) return rightNear.CompareTo(leftNear);
            if (left.StackPriority != right.StackPriority)
                return left.StackPriority.CompareTo(right.StackPriority);
            return string.CompareOrdinal(left.Id, right.Id);
        }
    }
}
