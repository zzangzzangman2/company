using System;
using System.Collections.Generic;
using FamilyCompany.Simulation.OfficeLayout;

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

        public OfficeRuntimePathService(OfficeGrid grid, OfficeRuntimeOccupancy occupancy)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _occupancy = occupancy ?? throw new ArgumentNullException(nameof(occupancy));
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
    }
}
