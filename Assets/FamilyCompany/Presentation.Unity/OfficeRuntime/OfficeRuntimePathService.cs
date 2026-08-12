using System;
using System.Collections.Generic;
using FamilyCompany.Presentation.Unity.OfficeGridView;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime
{
    public sealed class OfficeRuntimePathService
    {
        private static readonly OfficeGridCoordinate[] Neighbors =
        {
            new OfficeGridCoordinate(1, 0),
            new OfficeGridCoordinate(0, -1),
            new OfficeGridCoordinate(-1, 0),
            new OfficeGridCoordinate(0, 1)
        };

        private readonly OfficeGrid _grid;
        private readonly OfficeRuntimeOccupancy _occupancy;
        private readonly OfficeGridTilemapPresenter _presenter;

        public OfficeRuntimePathService(
            OfficeGrid grid,
            OfficeRuntimeOccupancy occupancy,
            OfficeGridTilemapPresenter presenter)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _occupancy = occupancy ?? throw new ArgumentNullException(nameof(occupancy));
            _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
        }

        public IReadOnlyList<OfficeGridCoordinate> FindPath(
            string agentId,
            OfficeGridCoordinate start,
            OfficeGridCoordinate goal,
            string permittedSeatId = "",
            bool avoidDynamic = false)
        {
            if (!_grid.Contains(start) || !_grid.Contains(goal)) return Array.Empty<OfficeGridCoordinate>();
            var queue = new Queue<OfficeGridCoordinate>();
            var visited = new HashSet<OfficeGridCoordinate> { start };
            var parent = new Dictionary<OfficeGridCoordinate, OfficeGridCoordinate>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                OfficeGridCoordinate current = queue.Dequeue();
                if (current.Equals(goal)) break;
                for (var index = 0; index < Neighbors.Length; index++)
                {
                    var offset = Neighbors[index];
                    var next = new OfficeGridCoordinate(current.X + offset.X, current.Y + offset.Y);
                    if (!_grid.Contains(next) || visited.Contains(next)) continue;
                    // A moving peer may still be leaving the final goal, so the goal remains
                    // statically testable. Replans avoid every dynamic cell before that goal.
                    bool includeDynamic = avoidDynamic && !next.Equals(goal);
                    if (!_occupancy.IsCellPassable(next, agentId, permittedSeatId, includeDynamic)) continue;
                    visited.Add(next);
                    parent[next] = current;
                    queue.Enqueue(next);
                }
            }

            if (!visited.Contains(goal)) return Array.Empty<OfficeGridCoordinate>();
            var path = new List<OfficeGridCoordinate> { goal };
            while (!path[path.Count - 1].Equals(start)) path.Add(parent[path[path.Count - 1]]);
            path.Reverse();
            return path;
        }

        public int ResolvePresentationTargetIndex(
            IReadOnlyList<OfficeGridCoordinate> semanticPath,
            int semanticStartIndex,
            string agentId,
            Vector2 currentPosition,
            float radius,
            string permittedSeatId,
            int maximumLookAhead = 6)
        {
            if (semanticPath == null) throw new ArgumentNullException(nameof(semanticPath));
            if (semanticPath.Count == 0) return -1;
            if (semanticStartIndex < 0 || semanticStartIndex >= semanticPath.Count)
                throw new ArgumentOutOfRangeException(nameof(semanticStartIndex));
            if (maximumLookAhead < 1) throw new ArgumentOutOfRangeException(nameof(maximumLookAhead));
            int furthest = Math.Min(semanticPath.Count - 1, semanticStartIndex + maximumLookAhead - 1);
            for (var index = furthest; index >= semanticStartIndex; index--)
            {
                if (!IsStraightSemanticRun(semanticPath, semanticStartIndex, index)) continue;
                Vector3 target3 = _presenter.CellCenterWorld(semanticPath[index]);
                Vector2 target = new Vector2(target3.x, target3.y);
                if (_occupancy.CanTraverseStatic(
                        currentPosition,
                        target,
                        radius,
                        permittedSeatId) &&
                    _occupancy.HasPresentationClearance(
                        agentId,
                        currentPosition,
                        target,
                        radius)) return index;
            }
            return semanticStartIndex;
        }

        private static bool IsStraightSemanticRun(
            IReadOnlyList<OfficeGridCoordinate> path,
            int startIndex,
            int endIndex)
        {
            if (endIndex <= startIndex) return true;
            OfficeGridCoordinate start = path[startIndex];
            bool sameX = true;
            bool sameY = true;
            for (var index = startIndex + 1; index <= endIndex; index++)
            {
                sameX &= path[index].X == start.X;
                sameY &= path[index].Y == start.Y;
            }
            return sameX || sameY;
        }
    }
}
