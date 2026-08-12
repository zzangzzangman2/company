using System;
using System.Collections.Generic;

namespace FamilyCompany.Simulation.OfficeLayout
{
    /// <summary>
    /// One occupant of the depth graph: a semantic cell footprint plus the tie-break priority used
    /// when two things stand on the same cells (an occupant and the chair under them).
    /// </summary>
    public readonly struct OfficeDepthItem : IEquatable<OfficeDepthItem>
    {
        public OfficeDepthItem(string id, int minX, int minY, int maxX, int maxY, int stackPriority = 0)
        {
            Id = string.IsNullOrWhiteSpace(id)
                ? throw new ArgumentException("Depth item id is required.", nameof(id))
                : id;
            if (maxX < minX) throw new ArgumentOutOfRangeException(nameof(maxX));
            if (maxY < minY) throw new ArgumentOutOfRangeException(nameof(maxY));
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
            StackPriority = stackPriority;
        }

        public static OfficeDepthItem Cell(string id, int x, int y, int stackPriority = 0) =>
            new OfficeDepthItem(id, x, y, x, y, stackPriority);

        public string Id { get; }
        public int MinX { get; }
        public int MinY { get; }
        public int MaxX { get; }
        public int MaxY { get; }

        /// <summary>Higher wins when two items share cells. A seated person beats their chair.</summary>
        public int StackPriority { get; }

        public bool Equals(OfficeDepthItem other) => string.Equals(Id, other.Id, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is OfficeDepthItem other && Equals(other);
        public override int GetHashCode() => Id.GetHashCode();
        public override string ToString() => $"{Id}[{MinX},{MinY}..{MaxX},{MaxY}]";
    }

    public enum OfficeDepthRelation
    {
        Unrelated = 0,
        FirstBehindSecond = 1,
        SecondBehindFirst = 2
    }

    /// <summary>
    /// Painter's order for the isometric office.
    ///
    /// The grid's screen mapping is basisX = (+half tile, +half tile) and basisY = (-half tile,
    /// +half tile), so both +x and +y move a cell away from the camera. An item is therefore behind
    /// another when its whole footprint sits past the other along either axis. Items that overlap on
    /// both axes stand on the same ground and are separated by <see cref="OfficeDepthItem.StackPriority"/>.
    ///
    /// This replaces sorting each sprite by a single anchor point. One anchor cannot describe a 2x1
    /// desk: whichever point is chosen, some placement puts a character on the wrong side of it -
    /// which is exactly how desk legs ended up drawn across a seated body. Ordering by the footprint
    /// makes any arrangement the layout editor can produce correct without special cases.
    /// </summary>
    public static class OfficeIsometricDepth
    {
        /// <summary>Sorting order given to the furthest item; every following item gets one more.</summary>
        public const int BaseSortingOrder = 1000;

        public static OfficeDepthRelation Compare(OfficeDepthItem first, OfficeDepthItem second)
        {
            bool firstPastX = first.MinX > second.MaxX;
            bool secondPastX = second.MinX > first.MaxX;
            bool firstPastY = first.MinY > second.MaxY;
            bool secondPastY = second.MinY > first.MaxY;

            // Separated along both axes but in opposite directions: the two footprints sit on
            // different diagonals of the floor and their sprites cannot overlap on screen. Claiming
            // an order here is what makes the two axis rules contradict each other and the graph
            // cycle.
            if ((firstPastX && secondPastY) || (secondPastX && firstPastY))
                return OfficeDepthRelation.Unrelated;

            if (firstPastX || firstPastY) return OfficeDepthRelation.FirstBehindSecond;
            if (secondPastX || secondPastY) return OfficeDepthRelation.SecondBehindFirst;

            // Stacking applies only to items standing on exactly the same cells - an occupant and
            // the chair beneath them. Ordering partly overlapping footprints by priority would add
            // an edge that can close a cycle with the two axis rules, and a valid layout never
            // produces one: blocking furniture cannot overlap, and a person only shares a cell with
            // the seat they claimed.
            bool sameFootprint = first.MinX == second.MinX && first.MaxX == second.MaxX &&
                                 first.MinY == second.MinY && first.MaxY == second.MaxY;
            if (sameFootprint && first.StackPriority != second.StackPriority)
                return first.StackPriority < second.StackPriority
                    ? OfficeDepthRelation.FirstBehindSecond
                    : OfficeDepthRelation.SecondBehindFirst;
            return OfficeDepthRelation.Unrelated;
        }

        /// <summary>
        /// Orders items back to front. The result is a stable topological order of the "is behind"
        /// relation: ties and unrelated pairs fall back to the far corner of the footprint and then
        /// to the id, so the same layout always produces the same orders.
        /// </summary>
        public static IReadOnlyList<OfficeDepthItem> Sort(IReadOnlyList<OfficeDepthItem> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            int count = items.Count;
            var result = new List<OfficeDepthItem>(count);
            if (count == 0) return result;

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (OfficeDepthItem item in items)
            {
                if (!ids.Add(item.Id))
                    throw new ArgumentException("Duplicate depth item id: " + item.Id, nameof(items));
            }

            // candidate order first, so equal-depth items resolve deterministically
            var pending = new List<int>(count);
            for (var index = 0; index < count; index++) pending.Add(index);
            pending.Sort((left, right) => CompareFallback(items[left], items[right]));

            var behindCount = new int[count];
            var inFrontOf = new List<int>[count];
            for (var index = 0; index < count; index++) inFrontOf[index] = new List<int>();
            for (var a = 0; a < count; a++)
            for (var b = a + 1; b < count; b++)
            {
                switch (Compare(items[a], items[b]))
                {
                    case OfficeDepthRelation.FirstBehindSecond:
                        inFrontOf[a].Add(b);
                        behindCount[b]++;
                        break;
                    case OfficeDepthRelation.SecondBehindFirst:
                        inFrontOf[b].Add(a);
                        behindCount[a]++;
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
                    // A cycle can only appear if two footprints straddle each other, which the grid
                    // cannot express. Emit the remaining items in fallback order rather than hang.
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

        /// <summary>Sorting order per item id, back to front, starting at <see cref="BaseSortingOrder"/>.</summary>
        public static IReadOnlyDictionary<string, int> ResolveSortingOrders(
            IReadOnlyList<OfficeDepthItem> items)
        {
            IReadOnlyList<OfficeDepthItem> ordered = Sort(items);
            var result = new Dictionary<string, int>(ordered.Count, StringComparer.Ordinal);
            for (var index = 0; index < ordered.Count; index++)
                result.Add(ordered[index].Id, BaseSortingOrder + index);
            return result;
        }

        private static int CompareFallback(OfficeDepthItem left, OfficeDepthItem right)
        {
            int leftFar = left.MaxX + left.MaxY;
            int rightFar = right.MaxX + right.MaxY;
            if (leftFar != rightFar) return rightFar.CompareTo(leftFar);
            int leftNear = left.MinX + left.MinY;
            int rightNear = right.MinX + right.MinY;
            if (leftNear != rightNear) return rightNear.CompareTo(leftNear);
            if (left.StackPriority != right.StackPriority)
                return left.StackPriority.CompareTo(right.StackPriority);
            return string.CompareOrdinal(left.Id, right.Id);
        }
    }
}
