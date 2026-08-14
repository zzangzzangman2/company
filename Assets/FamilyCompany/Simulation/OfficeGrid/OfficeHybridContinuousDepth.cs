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
            int count = items.Count;
            var result = new List<OfficeHybridDepthItem>(count);
            if (count == 0) return result;

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (OfficeHybridDepthItem item in items)
            {
                if (!ids.Add(item.Id))
                    throw new ArgumentException("Duplicate depth item id: " + item.Id, nameof(items));
            }

            var pending = new List<int>(count);
            for (var index = 0; index < count; index++) pending.Add(index);
            pending.Sort((left, right) => CompareFallback(items[left], items[right]));

            var behindCount = new int[count];
            var inFrontOf = new List<int>[count];
            for (var index = 0; index < count; index++) inFrontOf[index] = new List<int>();
            for (var firstIndex = 0; firstIndex < count; firstIndex++)
            for (var secondIndex = firstIndex + 1; secondIndex < count; secondIndex++)
            {
                switch (Compare(items[firstIndex], items[secondIndex]))
                {
                    case OfficeDepthRelation.FirstBehindSecond:
                        inFrontOf[firstIndex].Add(secondIndex);
                        behindCount[secondIndex]++;
                        break;
                    case OfficeDepthRelation.SecondBehindFirst:
                        inFrontOf[secondIndex].Add(firstIndex);
                        behindCount[firstIndex]++;
                        break;
                }
            }

            var emitted = new bool[count];
            while (result.Count < count)
            {
                var chosen = -1;
                foreach (int index in pending)
                {
                    if (emitted[index] || behindCount[index] != 0) continue;
                    chosen = index;
                    break;
                }
                if (chosen < 0)
                {
                    // Invalid spatial input can form a cycle. Break it by the same stable fallback
                    // rather than making output depend on caller insertion order.
                    foreach (int index in pending)
                    {
                        if (emitted[index]) continue;
                        chosen = index;
                        break;
                    }
                }

                emitted[chosen] = true;
                behindCount[chosen] = -1;
                result.Add(items[chosen]);
                foreach (int ahead in inFrontOf[chosen])
                    if (behindCount[ahead] > 0) behindCount[ahead]--;
            }
            return result;
        }

        public static IReadOnlyDictionary<string, int> ResolveSortingOrders(
            IReadOnlyList<OfficeHybridDepthItem> items)
        {
            IReadOnlyList<OfficeHybridDepthItem> ordered = Sort(items);
            var result = new Dictionary<string, int>(ordered.Count, StringComparer.Ordinal);
            for (var index = 0; index < ordered.Count; index++)
                result.Add(ordered[index].Id, OfficeIsometricDepth.BaseSortingOrder + index);
            return result;
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

        private static int CompareFallback(
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
