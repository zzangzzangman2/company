using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor.OfficeGrid
{
    /// <summary>
    /// Pure hybrid-depth regression suite. It proves that continuous actor contacts improve only
    /// actor-involved comparisons while every furniture-only topology and fallback stays legacy
    /// compatible, including under shuffled input and a forced invalid cycle.
    /// </summary>
    public static class OfficeHybridContinuousDepthValidation
    {
        private const int Q = OfficeHybridContinuousDepth.Quantization;
        private static int _legacyItemCount;
        private static int _mixedItemCount;
        private static long _workspaceAllocatedBytes;

        [MenuItem("Family Company/Validate/Office Hybrid Continuous Depth")]
        public static void Run()
        {
            _legacyItemCount = 0;
            _mixedItemCount = 0;
            _workspaceAllocatedBytes = 0L;
            ValidateSignedQuantizationAndFramePartitioning();
            ValidateLegacyFurniturePermutations();
            ValidateLegacyFurnitureRandomEquivalence();
            ValidateHalfEdgeSweepWithoutNearestCellPop();
            ValidateEightDirectionsAndFootprintShapes();
            ValidateOrdinalCultureIndependentTies();
            ValidateNormalForegroundMixedFallback();
            ValidateSeatStackPlanesAndRelease();
            ValidateTallFurnitureIgnoresVisualHeight();
            ValidateRandomMixedShuffleAndCycleUniqueness();
            ValidateReusableWorkspace();
            Debug.Log(
                "OFFICE_HYBRID_CONTINUOUS_DEPTH_VALIDATION: PASS " +
                $"Q={Q} legacyItems={_legacyItemCount} mixedItems={_mixedItemCount} " +
                "permutations=120 directions=8 footprints=4 frameRates=30/60/144 " +
                "normalFrontCases=48 normalFront=base<front<actor " +
                "seatPlanes=0<1<2<3<4 tallHeightIgnored=true " +
                $"workspaceAlloc100={_workspaceAllocatedBytes}B");
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
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void ValidateSignedQuantizationAndFramePartitioning()
        {
            double[] centers = { -7.5d, -2d, -0.5d, 0d, 4.5d, 11d };
            foreach (double center in centers)
            {
                int centerQ = OfficeHybridContinuousDepth.Quantize(center);
                Require(
                    OfficeHybridContinuousDepth.Quantize(center - 0.00001d) == centerQ &&
                    OfficeHybridContinuousDepth.Quantize(center + 0.00001d) == centerQ,
                    $"Quantization jittered around exact center {center}.");
                Require(
                    OfficeHybridContinuousDepth.Quantize(center + 0.49d / Q) == centerQ,
                    $"Positive in-bucket noise escaped at {center}.");
                Require(
                    OfficeHybridContinuousDepth.Quantize(center - 0.49d / Q) == centerQ,
                    $"Negative in-bucket noise escaped at {center}.");
                Require(
                    OfficeHybridContinuousDepth.Quantize(center + 0.51d / Q) == centerQ + 1,
                    $"Positive bucket crossing failed at {center}.");
                Require(
                    OfficeHybridContinuousDepth.Quantize(center - 0.51d / Q) == centerQ - 1,
                    $"Negative bucket crossing failed at {center}.");
            }

            const double startX = -1.25d;
            const double startY = 4.75d;
            const double velocityX = 3.125d;
            const double velocityY = -2.375d;
            int expectedX = OfficeHybridContinuousDepth.Quantize(startX + velocityX);
            int expectedY = OfficeHybridContinuousDepth.Quantize(startY + velocityY);
            int[] frameRates = { 30, 60, 144 };
            foreach (int frameRate in frameRates)
            {
                double x = startX;
                double y = startY;
                for (var frame = 0; frame < frameRate; frame++)
                {
                    x += velocityX / frameRate;
                    y += velocityY / frameRate;
                }
                Require(
                    OfficeHybridContinuousDepth.Quantize(x) == expectedX &&
                    OfficeHybridContinuousDepth.Quantize(y) == expectedY,
                    $"Frame partition {frameRate}Hz changed the final quantized contact.");
            }
        }

        private static void ValidateLegacyFurniturePermutations()
        {
            var legacy = new List<OfficeDepthItem>
            {
                new OfficeDepthItem("desk", 2, 4, 3, 4),
                OfficeDepthItem.Cell("chair", 2, 3),
                OfficeDepthItem.Cell("chair-front", 2, 3, 2),
                new OfficeDepthItem("cabinet", 6, 6, 6, 8),
                new OfficeDepthItem("sofa", 0, 7, 1, 7)
            };
            string expected = JoinIds(OfficeIsometricDepth.Sort(legacy));
            var permutations = 0;
            Permute(legacy, 0, candidate =>
            {
                string actual = JoinHybridIds(OfficeHybridContinuousDepth.Sort(
                    candidate.Select(ToHybridFurniture).ToArray()));
                Require(actual == expected,
                    $"Furniture permutation {permutations} changed legacy order: {actual} != {expected}.");
                permutations++;
            });
            Require(permutations == 120, "Five furniture items did not produce 120 permutations.");
            _legacyItemCount += legacy.Count * permutations;
        }

        private static void ValidateLegacyFurnitureRandomEquivalence()
        {
            var random = new System.Random(2026081401);
            for (var trial = 0; trial < 1000; trial++)
            {
                List<OfficeDepthItem> legacy = RandomNonOverlappingFurniture(random, trial);
                IReadOnlyList<OfficeDepthItem> expected = OfficeIsometricDepth.Sort(legacy);
                var hybrid = legacy.Select(ToHybridFurniture).ToList();
                AssertHybridSequence(hybrid, JoinIds(expected), $"legacy trial {trial}");
                for (var shuffle = 0; shuffle < 2; shuffle++)
                {
                    Shuffle(hybrid, random);
                    AssertHybridSequence(
                        hybrid,
                        JoinIds(expected),
                        $"legacy trial {trial} shuffle {shuffle}");
                }
                _legacyItemCount += legacy.Count;
            }
            Require(_legacyItemCount >= 10000,
                $"Legacy equivalence covered only {_legacyItemCount} items.");
        }

        private static void ValidateHalfEdgeSweepWithoutNearestCellPop()
        {
            OfficeHybridDepthItem desk = Furniture(
                "desk",
                2,
                4,
                3,
                4,
                OfficeHybridDepthRole.FurnitureBase);
            var walks = new (double X, double Y, bool InFront)[]
            {
                (2d, 3d, true), (3d, 3d, true), (3d, 2d, true), (1d, 4d, true),
                (2d, 5d, false), (3d, 5d, false), (4d, 4d, false)
            };
            foreach ((double x, double y, bool inFront) in walks)
            {
                OfficeHybridDepthItem actor = Actor("walker", x, y);
                IReadOnlyDictionary<string, int> orders =
                    OfficeHybridContinuousDepth.ResolveSortingOrders(new[] { desk, actor });
                Require(
                    (orders[actor.Id] > orders[desk.Id]) == inFront,
                    $"Continuous walker ({x},{y}) disagreed with the legacy 2x1 desk side.");
            }

            OfficeHybridDepthItem beforeNear = Actor("before-near", 3d, 3.49d);
            OfficeHybridDepthItem afterNear = Actor("after-near", 3d, 3.51d);
            OfficeHybridDepthItem beforeFar = Actor("before-far", 3d, 4.49d);
            OfficeHybridDepthItem afterFar = Actor("after-far", 3d, 4.51d);
            Require(
                OfficeHybridContinuousDepth.Compare(beforeNear, desk) ==
                OfficeDepthRelation.SecondBehindFirst,
                "Actor immediately before the physical near edge is not in front of the desk.");
            Require(
                OfficeHybridContinuousDepth.Compare(afterNear, desk) == OfficeDepthRelation.Unrelated,
                "Actor immediately inside the footprint did not cross the physical near edge.");
            Require(
                OfficeHybridContinuousDepth.Compare(beforeFar, desk) == OfficeDepthRelation.Unrelated,
                "Actor immediately inside the far edge escaped the footprint.");
            Require(
                OfficeHybridContinuousDepth.Compare(afterFar, desk) ==
                OfficeDepthRelation.FirstBehindSecond,
                "Actor immediately after the physical far edge is not behind the desk.");

            int beforeNearestCellOrder = OfficeHybridContinuousDepth.ResolveSortingOrders(
                new[] { desk, beforeNear })[beforeNear.Id];
            int afterNearestCellOrder = OfficeHybridContinuousDepth.ResolveSortingOrders(
                new[] { desk, afterNear })[afterNear.Id];
            Require(
                beforeNearestCellOrder == afterNearestCellOrder,
                "Crossing the nearest-cell midpoint caused a visible order pop.");

            var previousOrder = int.MinValue;
            var orderChanges = 0;
            for (var sample = 0; sample <= 3072; sample++)
            {
                double y = 2.5d + sample / 1024d;
                OfficeHybridDepthItem actor = Actor("sweep", 3d, y);
                int order = OfficeHybridContinuousDepth.ResolveSortingOrders(
                    new[] { desk, actor })[actor.Id];
                if (previousOrder != int.MinValue && order != previousOrder) orderChanges++;
                previousOrder = order;
            }
            Require(orderChanges <= 1,
                $"Continuous half-edge sweep changed actor order {orderChanges} times.");
            _mixedItemCount += walks.Length * 2 + 8 + 3073 * 2;
        }

        private static void ValidateEightDirectionsAndFootprintShapes()
        {
            var shapes = new (int Width, int Height)[]
            {
                (1, 1), (2, 1), (1, 3), (2, 2)
            };
            foreach ((int width, int height) in shapes)
            {
                OfficeHybridDepthItem furniture = Furniture(
                    $"shape-{width}x{height}",
                    4,
                    6,
                    4 + width - 1,
                    6 + height - 1,
                    OfficeHybridDepthRole.FurnitureBase);
                OfficeDepthItem footprint = furniture.FurnitureFootprint;
                double nearX = footprint.MinX - 0.5d;
                double nearY = footprint.MinY - 0.5d;
                double farX = footprint.MaxX + 0.5d;
                double farY = footprint.MaxY + 0.5d;
                double middleX = (nearX + farX) * 0.5d;
                double middleY = (nearY + farY) * 0.5d;
                var directions = new (string Name, double X, double Y, OfficeDepthRelation Relation)[]
                {
                    ("west", nearX - 1d, middleY, OfficeDepthRelation.SecondBehindFirst),
                    ("east", farX + 1d, middleY, OfficeDepthRelation.FirstBehindSecond),
                    ("south", middleX, nearY - 1d, OfficeDepthRelation.SecondBehindFirst),
                    ("north", middleX, farY + 1d, OfficeDepthRelation.FirstBehindSecond),
                    ("southwest", nearX - 1d, nearY - 1d, OfficeDepthRelation.SecondBehindFirst),
                    ("northeast", farX + 1d, farY + 1d, OfficeDepthRelation.FirstBehindSecond),
                    ("northwest", nearX - 1d, farY + 1d, OfficeDepthRelation.Unrelated),
                    ("southeast", farX + 1d, nearY - 1d, OfficeDepthRelation.Unrelated)
                };
                foreach ((string name, double x, double y, OfficeDepthRelation relation) in directions)
                {
                    OfficeHybridDepthItem actor = Actor($"{width}x{height}-{name}", x, y);
                    Require(
                        OfficeHybridContinuousDepth.Compare(actor, furniture) == relation,
                        $"{width}x{height} footprint failed {name} relation {relation}.");
                }
                _mixedItemCount += directions.Length * 2;
            }
        }

        private static void ValidateOrdinalCultureIndependentTies()
        {
            var items = new List<OfficeHybridDepthItem>
            {
                OfficeHybridDepthItem.Actor("id-i-dot", 512, 512, "İ-role", "z-instance"),
                OfficeHybridDepthItem.Actor("id-i", 512, 512, "I-role", "가-instance"),
                OfficeHybridDepthItem.Actor("id-lower", 512, 512, "i-role", "A-instance"),
                OfficeHybridDepthItem.Actor("id-same-a", 512, 512, "same", "A-instance"),
                OfficeHybridDepthItem.Actor("id-same-z", 512, 512, "same", "Z-instance")
            };
            CultureInfo previousCulture = CultureInfo.CurrentCulture;
            CultureInfo previousUiCulture = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ko-KR");
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ko-KR");
                string korean = JoinHybridIds(OfficeHybridContinuousDepth.Sort(items));
                items.Reverse();
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
                string english = JoinHybridIds(OfficeHybridContinuousDepth.Sort(items));
                Require(korean == english,
                    $"Culture or input order changed ordinal ties: {korean} != {english}.");
                Require(
                    english.IndexOf("id-same-a", StringComparison.Ordinal) <
                    english.IndexOf("id-same-z", StringComparison.Ordinal),
                    "Instance ids are not compared ordinal after an equal semantic id.");
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
                CultureInfo.CurrentUICulture = previousUiCulture;
            }
            _mixedItemCount += items.Count * 2;
        }

        private static void ValidateSeatStackPlanesAndRelease()
        {
            string[] engagedPhases =
                { "SittingDown", "Working", "FinishingWork", "StandingUp", "LeavingSeat@0.349" };
            foreach (string phase in engagedPhases)
            {
                List<OfficeHybridDepthItem> stack = EngagedSeatStack("seat-a");
                IReadOnlyList<OfficeHybridDepthItem> sorted =
                    OfficeHybridContinuousDepth.Sort(stack);
                string actual = JoinHybridIds(sorted);
                const string expected = "desk-base,chair-base,actor,desk-front,chair-front";
                Require(actual == expected, $"{phase} seat planes were {actual}, expected {expected}.");
                for (var index = 1; index < sorted.Count; index++)
                {
                    Require(
                        OfficeHybridContinuousDepth.Compare(sorted[index - 1], sorted[index]) ==
                        OfficeDepthRelation.FirstBehindSecond,
                        $"{phase} plane {index - 1}->{index} was not forced.");
                }
                _mixedItemCount += stack.Count;
            }

            OfficeHybridDepthItem deskBase = Furniture(
                "released-desk-base", 2, 4, 3, 4, OfficeHybridDepthRole.FurnitureBase);
            OfficeHybridDepthItem chairBase = Furniture(
                "released-chair-base", 2, 3, 2, 3, OfficeHybridDepthRole.FurnitureBase);
            OfficeHybridDepthItem actor = Actor("released-actor", 2d, 3d);
            OfficeHybridDepthItem deskFront = Furniture(
                "released-desk-front", 2, 4, 3, 4, OfficeHybridDepthRole.FurnitureFront,
                legacyStackPriority: 1);
            OfficeHybridDepthItem chairFront = Furniture(
                "released-chair-front", 2, 3, 2, 3, OfficeHybridDepthRole.FurnitureFront,
                legacyStackPriority: 1);
            IReadOnlyDictionary<string, int> released = OfficeHybridContinuousDepth.ResolveSortingOrders(
                new[] { actor, chairFront, deskFront, chairBase, deskBase });
            Require(released[chairBase.Id] < released[actor.Id],
                "Released chair base did not remain below the actor.");
            Require(released[deskBase.Id] < released[actor.Id],
                "Released desk base did not remain below the actor.");
            Require(released[chairFront.Id] < released[actor.Id],
                "Released chair front still sliced the actor.");
            Require(released[deskFront.Id] < released[actor.Id],
                "Released desk front still sliced the actor.");
            _mixedItemCount += released.Count;
        }

        private static void ValidateNormalForegroundMixedFallback()
        {
            CultureInfo previousCulture = CultureInfo.CurrentCulture;
            CultureInfo previousUiCulture = CultureInfo.CurrentUICulture;
            try
            {
                var expectedByPosition = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { "center", "normal-base,normal-front,walking-actor" },
                    { "near-edge", "normal-base,normal-front,walking-actor" },
                    { "far-edge", "walking-actor,normal-base,normal-front" },
                    { "behind", "walking-actor,normal-base,normal-front" }
                };
                var positions = new Dictionary<string, (double X, double Y)>(StringComparer.Ordinal)
                {
                    { "center", (4d, 6d) },
                    { "near-edge", (4d, 5.5d) },
                    { "far-edge", (4d, 6.5d) },
                    { "behind", (4d, 6.51d) }
                };
                string[] cultures = { "ko-KR", "en-US" };
                foreach (string cultureName in cultures)
                {
                    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
                    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName);
                    foreach (KeyValuePair<string, (double X, double Y)> position in positions)
                    {
                        var items = new List<OfficeHybridDepthItem>
                        {
                            Furniture(
                                "normal-base", 4, 6, 4, 6,
                                OfficeHybridDepthRole.FurnitureBase,
                                legacyStackPriority: 0),
                            Furniture(
                                "normal-front", 4, 6, 4, 6,
                                OfficeHybridDepthRole.FurnitureFront,
                                legacyStackPriority: 1),
                            Actor("walking-actor", position.Value.X, position.Value.Y)
                        };
                        var permutationCount = 0;
                        Permute(items, 0, candidate =>
                        {
                            string actual = JoinHybridIds(
                                OfficeHybridContinuousDepth.Sort(candidate));
                            Require(
                                actual == expectedByPosition[position.Key],
                                $"{cultureName} {position.Key} normal foreground order was " +
                                $"{actual}, expected {expectedByPosition[position.Key]}.");
                            permutationCount++;
                        });
                        Require(permutationCount == 6,
                            $"{cultureName} {position.Key} did not cover all six input permutations.");
                        _mixedItemCount += items.Count * permutationCount;
                    }
                }
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
                CultureInfo.CurrentUICulture = previousUiCulture;
            }
        }

        private static void ValidateTallFurnitureIgnoresVisualHeight()
        {
            int[] syntheticVisualHeights = { 1, 100, 1000 };
            string expected = string.Empty;
            foreach (int ignoredVisualHeight in syntheticVisualHeights)
            {
                OfficeHybridDepthItem tall = Furniture(
                    "tall-cabinet", 7, 5, 7, 7, OfficeHybridDepthRole.FurnitureBase);
                OfficeHybridDepthItem inFront = Actor("front-actor", 6d, 5d);
                OfficeHybridDepthItem behind = Actor("back-actor", 8d, 8d);
                string actual = JoinHybridIds(OfficeHybridContinuousDepth.Sort(
                    new[] { tall, inFront, behind }));
                if (expected.Length == 0) expected = actual;
                Require(actual == expected,
                    $"Synthetic visual height {ignoredVisualHeight} changed depth order.");
                Require(
                    OfficeHybridContinuousDepth.Compare(inFront, tall) ==
                    OfficeDepthRelation.SecondBehindFirst &&
                    OfficeHybridContinuousDepth.Compare(behind, tall) ==
                    OfficeDepthRelation.FirstBehindSecond,
                    $"Tall furniture ground footprint failed at visual height {ignoredVisualHeight}.");
                _mixedItemCount += 3;
            }
            Require(
                typeof(OfficeHybridDepthItem).GetProperty("VisualHeight") == null &&
                typeof(OfficeHybridDepthItem).GetProperty("RendererBounds") == null,
                "Visual height or renderer bounds leaked into the pure depth API.");
        }

        private static void ValidateRandomMixedShuffleAndCycleUniqueness()
        {
            var random = new System.Random(2026081402);
            for (var trial = 0; trial < 750; trial++)
            {
                int count = 4 + random.Next(13);
                var items = new List<OfficeHybridDepthItem>(count);
                for (var index = 0; index < count; index++)
                {
                    if (index % 3 == 0)
                    {
                        int x = random.Next(-2, 14);
                        int y = random.Next(-2, 14);
                        items.Add(Furniture(
                            $"mixed-f-{trial}-{index}",
                            x,
                            y,
                            x + random.Next(0, 3),
                            y + random.Next(0, 3),
                            OfficeHybridDepthRole.FurnitureBase));
                    }
                    else
                    {
                        int qx = random.Next(-2 * Q, 15 * Q);
                        int qy = random.Next(-2 * Q, 15 * Q);
                        items.Add(OfficeHybridDepthItem.Actor(
                            $"mixed-a-{trial}-{index}",
                            qx,
                            qy,
                            "actor",
                            $"agent-{trial}-{index}"));
                    }
                }

                IReadOnlyList<OfficeHybridDepthItem> sorted = OfficeHybridContinuousDepth.Sort(items);
                AssertUniqueAndSequential(sorted, $"mixed trial {trial}");
                string expected = JoinHybridIds(sorted);
                for (var shuffle = 0; shuffle < 3; shuffle++)
                {
                    Shuffle(items, random);
                    AssertHybridSequence(items, expected, $"mixed trial {trial} shuffle {shuffle}");
                }
                _mixedItemCount += items.Count;
            }

            // Deliberately invalid geometry: forced A<B, geometric B<C, geometric C<A. The stable
            // cycle break must emit each item once and must not depend on caller insertion order.
            var cycle = new List<OfficeHybridDepthItem>
            {
                OfficeHybridDepthItem.Actor("cycle-a", 0, 0, "actor", "a", "invalid-seat", 0),
                Furniture(
                    "cycle-b", 5, 5, 5, 5, OfficeHybridDepthRole.ChairFront,
                    "invalid-seat", 4),
                OfficeHybridDepthItem.Actor("cycle-c", 3 * Q, 3 * Q, "actor", "c")
            };
            string cycleExpected = JoinHybridIds(OfficeHybridContinuousDepth.Sort(cycle));
            for (var shuffle = 0; shuffle < 12; shuffle++)
            {
                Shuffle(cycle, random);
                IReadOnlyList<OfficeHybridDepthItem> sorted = OfficeHybridContinuousDepth.Sort(cycle);
                Require(JoinHybridIds(sorted) == cycleExpected,
                    "Stable cycle break changed with input order.");
                AssertUniqueAndSequential(sorted, "forced cycle");
            }
            _mixedItemCount += cycle.Count * 12;
        }

        private static List<OfficeDepthItem> RandomNonOverlappingFurniture(
            System.Random random,
            int trial)
        {
            var result = new List<OfficeDepthItem>();
            var taken = new HashSet<long>();
            int requested = 8 + random.Next(13);
            for (var attempt = 0; attempt < requested * 8 && result.Count < requested; attempt++)
            {
                int minX = random.Next(-2, 15);
                int minY = random.Next(-2, 15);
                int maxX = minX + random.Next(0, 3);
                int maxY = minY + random.Next(0, 3);
                var cells = new List<long>();
                for (int x = minX; x <= maxX; x++)
                for (int y = minY; y <= maxY; y++) cells.Add(((long)x << 32) ^ (uint)y);
                if (cells.Any(taken.Contains)) continue;
                foreach (long cell in cells) taken.Add(cell);
                result.Add(new OfficeDepthItem(
                    $"legacy-{trial}-{result.Count}",
                    minX,
                    minY,
                    maxX,
                    maxY,
                    random.Next(0, 3)));
            }
            if (result.Count < 2)
            {
                result.Add(OfficeDepthItem.Cell($"legacy-{trial}-fallback-a", 0, 0));
                result.Add(OfficeDepthItem.Cell($"legacy-{trial}-fallback-b", 2, 2));
            }
            return result;
        }

        private static List<OfficeHybridDepthItem> EngagedSeatStack(string seatStackId)
        {
            return new List<OfficeHybridDepthItem>
            {
                Furniture(
                    "desk-base", 2, 4, 3, 4, OfficeHybridDepthRole.FurnitureBase,
                    seatStackId, 0),
                Furniture(
                    "chair-base", 2, 3, 2, 3, OfficeHybridDepthRole.ChairBase,
                    seatStackId, 1),
                OfficeHybridDepthItem.Actor(
                    "actor", -17 * Q, 29 * Q, "actor", "family-member", seatStackId, 2),
                Furniture(
                    "desk-front", 2, 4, 3, 4, OfficeHybridDepthRole.DeskFront,
                    seatStackId, 3),
                Furniture(
                    "chair-front", 2, 3, 2, 3, OfficeHybridDepthRole.ChairFront,
                    seatStackId, 4)
            };
        }

        private static OfficeHybridDepthItem ToHybridFurniture(OfficeDepthItem item)
        {
            return OfficeHybridDepthItem.Furniture(
                item,
                OfficeHybridDepthRole.FurnitureBase,
                "legacy-furniture",
                item.Id);
        }

        private static OfficeHybridDepthItem Furniture(
            string id,
            int minX,
            int minY,
            int maxX,
            int maxY,
            OfficeHybridDepthRole role,
            string seatStackId = "",
            int seatPlane = 0,
            int legacyStackPriority = 0)
        {
            return OfficeHybridDepthItem.Furniture(
                new OfficeDepthItem(id, minX, minY, maxX, maxY, legacyStackPriority),
                role,
                role.ToString(),
                id,
                seatStackId,
                seatPlane);
        }

        private static OfficeHybridDepthItem Actor(string id, double x, double y)
        {
            return OfficeHybridDepthItem.ActorAtGridPosition(
                id,
                x,
                y,
                "actor",
                id);
        }

        private static void AssertHybridSequence(
            IReadOnlyList<OfficeHybridDepthItem> items,
            string expected,
            string label)
        {
            IReadOnlyList<OfficeHybridDepthItem> sorted = OfficeHybridContinuousDepth.Sort(items);
            string actual = JoinHybridIds(sorted);
            Require(actual == expected, $"{label}: {actual} != {expected}.");
            AssertUniqueAndSequential(sorted, label);
        }

        private static void ValidateReusableWorkspace()
        {
            var items = new List<OfficeHybridDepthItem>();
            for (var y = 0; y < 7; y++)
            for (var x = 0; x < 10; x++)
                items.Add(Furniture(
                    $"workspace-{x}-{y}",
                    x,
                    y,
                    x,
                    y,
                    OfficeHybridDepthRole.FurnitureBase));
            items.Add(Actor("workspace-actor-a", 2.25d, 3.50d));
            items.Add(Actor("workspace-actor-b", 7.75d, 4.25d));

            var workspace = new OfficeHybridDepthSortWorkspace(items.Count);
            IReadOnlyDictionary<string, int> first =
                OfficeHybridContinuousDepth.ResolveSortingOrders(items, workspace);
            string expected = string.Join(
                ",",
                first.OrderBy(pair => pair.Value).Select(pair => pair.Key));
            IReadOnlyDictionary<string, int> second =
                OfficeHybridContinuousDepth.ResolveSortingOrders(items, workspace);
            string actual = string.Join(
                ",",
                second.OrderBy(pair => pair.Value).Select(pair => pair.Key));
            Require(ReferenceEquals(first, second), "workspace result dictionary was replaced");
            Require(actual == expected, "workspace reuse changed depth order");

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (var iteration = 0; iteration < 100; iteration++)
                OfficeHybridContinuousDepth.ResolveSortingOrders(items, workspace);
            _workspaceAllocatedBytes = Math.Max(
                0L,
                GC.GetAllocatedBytesForCurrentThread() - before);
            Require(
                _workspaceAllocatedBytes <= 1024L,
                $"workspace allocated {_workspaceAllocatedBytes} bytes over 100 warmed sorts");
        }

        private static void AssertUniqueAndSequential(
            IReadOnlyList<OfficeHybridDepthItem> sorted,
            string label)
        {
            Require(sorted.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() == sorted.Count,
                label + " emitted a duplicate or omitted item.");
            IReadOnlyDictionary<string, int> orders =
                OfficeHybridContinuousDepth.ResolveSortingOrders(sorted);
            for (var index = 0; index < sorted.Count; index++)
            {
                Require(
                    orders[sorted[index].Id] == OfficeIsometricDepth.BaseSortingOrder + index,
                    $"{label} did not assign a sequential sorting order at {index}.");
            }
        }

        private static string JoinIds(IEnumerable<OfficeDepthItem> items)
        {
            return string.Join(",", items.Select(item => item.Id));
        }

        private static string JoinHybridIds(IEnumerable<OfficeHybridDepthItem> items)
        {
            return string.Join(",", items.Select(item => item.Id));
        }

        private static void Shuffle<T>(IList<T> items, System.Random random)
        {
            for (var index = items.Count - 1; index > 0; index--)
            {
                int other = random.Next(index + 1);
                T value = items[index];
                items[index] = items[other];
                items[other] = value;
            }
        }

        private static void Permute<T>(IList<T> items, int index, Action<IReadOnlyList<T>> visit)
        {
            if (index >= items.Count)
            {
                visit(items.ToArray());
                return;
            }
            for (var candidate = index; candidate < items.Count; candidate++)
            {
                T value = items[index];
                items[index] = items[candidate];
                items[candidate] = value;
                Permute(items, index + 1, visit);
                value = items[index];
                items[index] = items[candidate];
                items[candidate] = value;
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
