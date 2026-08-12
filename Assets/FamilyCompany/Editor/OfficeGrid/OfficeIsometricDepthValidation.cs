using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor.OfficeGrid
{
    /// <summary>
    /// Footprint depth ordering, checked without a scene. Mirrors the offline harness used while the
    /// rule was written, so a regression shows up in batchmode too:
    /// Unity.exe -batchmode -nographics -quit -projectPath . -executeMethod
    ///   FamilyCompany.Editor.OfficeGrid.OfficeIsometricDepthValidation.RunBatch
    /// </summary>
    public static class OfficeIsometricDepthValidation
    {
        [MenuItem("Family Company/Validate/Office Isometric Depth")]
        public static void Run()
        {
            var failures = new List<string>();

            var desk = new OfficeDepthItem("desk", 2, 4, 3, 4);
            var chair = new OfficeDepthItem("chair", 2, 3, 2, 3);
            var seated = new OfficeDepthItem("seated", 2, 3, 2, 3, 1);
            IReadOnlyDictionary<string, int> station =
                OfficeIsometricDepth.ResolveSortingOrders(new[] { desk, chair, seated });
            Require(failures, station["desk"] < station["chair"], "desk draws behind the chair south of it");
            Require(failures, station["chair"] < station["seated"], "occupant draws in front of their chair");
            Require(failures, station["desk"] < station["seated"], "desk never covers the person at it");

            var walks = new (int X, int Y, bool InFront)[]
            {
                (2, 3, true), (3, 3, true), (3, 2, true), (1, 4, true),
                (2, 5, false), (3, 5, false), (4, 4, false)
            };
            foreach ((int x, int y, bool inFront) in walks)
            {
                IReadOnlyDictionary<string, int> pass = OfficeIsometricDepth.ResolveSortingOrders(
                    new[] { desk, OfficeDepthItem.Cell("mover", x, y) });
                Require(
                    failures,
                    (pass["mover"] > pass["desk"]) == inFront,
                    $"walker at ({x},{y}) is {(inFront ? "in front of" : "behind")} the 2x1 desk");
            }

            var random = new System.Random(20260812);
            for (var trial = 0; trial < 200; trial++)
            {
                var items = new List<OfficeDepthItem>();
                var taken = new HashSet<long>();
                int count = 2 + random.Next(16);
                for (var index = 0; index < count; index++)
                {
                    int x = random.Next(0, 13);
                    int y = random.Next(0, 13);
                    int maxX = Math.Min(12, x + random.Next(0, 3));
                    int maxY = Math.Min(12, y + random.Next(0, 2));
                    var cells = new List<long>();
                    for (int cx = x; cx <= maxX; cx++)
                    for (int cy = y; cy <= maxY; cy++) cells.Add(cx * 100L + cy);
                    if (cells.Any(taken.Contains)) continue;
                    foreach (long cell in cells) taken.Add(cell);
                    items.Add(new OfficeDepthItem("item" + index, x, y, maxX, maxY));
                    if (maxX == x && maxY == y && random.Next(3) == 0)
                        items.Add(new OfficeDepthItem("sitter" + index, x, y, x, y, 1));
                }
                if (items.Count < 2) continue;

                IReadOnlyList<OfficeDepthItem> sorted = OfficeIsometricDepth.Sort(items);
                var position = new Dictionary<string, int>(StringComparer.Ordinal);
                for (var index = 0; index < sorted.Count; index++) position[sorted[index].Id] = index;
                foreach (OfficeDepthItem a in items)
                foreach (OfficeDepthItem b in items)
                {
                    if (string.Equals(a.Id, b.Id, StringComparison.Ordinal)) continue;
                    if (OfficeIsometricDepth.Compare(a, b) != OfficeDepthRelation.FirstBehindSecond) continue;
                    Require(failures, position[a.Id] < position[b.Id], $"trial {trial}: {a} draws before {b}");
                }
                IReadOnlyList<OfficeDepthItem> shuffled = OfficeIsometricDepth.Sort(
                    items.OrderBy(_ => random.Next()).ToList());
                for (var index = 0; index < sorted.Count; index++)
                    Require(
                        failures,
                        string.Equals(sorted[index].Id, shuffled[index].Id, StringComparison.Ordinal),
                        $"trial {trial}: input order does not change the result");
            }

            if (failures.Count > 0)
                throw new InvalidOperationException(
                    "OFFICE_ISOMETRIC_DEPTH_VALIDATION: FAIL | " + string.Join(" | ", failures.Take(10)));
            Debug.Log("OFFICE_ISOMETRIC_DEPTH_VALIDATION: PASS | cases=" + (3 + walks.Length) + " | trials=200");
        }

        public static void RunBatch()
        {
            try
            {
                Run();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError(exception.Message);
                EditorApplication.Exit(1);
            }
        }

        private static void Require(ICollection<string> failures, bool condition, string label)
        {
            if (!condition) failures.Add(label);
        }
    }
}
