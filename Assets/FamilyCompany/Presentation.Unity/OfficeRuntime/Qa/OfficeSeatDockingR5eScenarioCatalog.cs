using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using FamilyCompany.Simulation.OfficeLayout;
using FamilyCompany.Simulation.OfficeSeating;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime.Qa
{
    internal enum R5eScenarioKind
    {
        BaseMatrix = 0,
        SeededObstacles = 1,
        AllExitsBlocked = 2,
        Contention = 3,
        FaultEntry = 4,
        FaultExit = 5,
        VersionEntry = 6,
        VersionExit = 7
    }

    internal readonly struct R5eScenarioCase
    {
        public R5eScenarioCase(
            ulong id,
            string caseId,
            R5eScenarioKind kind,
            string actorId,
            int rotationQuarterTurns,
            int arrivalDirection,
            int seed,
            int faultInjectionId,
            int contentionIndex)
        {
            Id = id;
            CaseId = caseId;
            Kind = kind;
            ActorId = actorId;
            RotationQuarterTurns = rotationQuarterTurns;
            ArrivalDirection = arrivalDirection;
            Seed = seed;
            FaultInjectionId = faultInjectionId;
            ContentionIndex = contentionIndex;
        }

        public ulong Id { get; }
        public string CaseId { get; }
        public R5eScenarioKind Kind { get; }
        public string ActorId { get; }
        public int RotationQuarterTurns { get; }
        public int ArrivalDirection { get; }
        public int Seed { get; }
        public int FaultInjectionId { get; }
        public int ContentionIndex { get; }
    }

    internal sealed class OfficeSeatDockingR5eScenarioPlan
    {
        public OfficeSeatDockingR5eScenarioPlan(
            string sha256,
            int seed,
            string[] actors,
            R5eContentionPermutation[] contention,
            R5eScenarioCase[] cases)
        {
            Sha256 = sha256;
            Seed = seed;
            Actors = actors;
            Contention = contention;
            Cases = cases;
        }

        public string Sha256 { get; }
        public int Seed { get; }
        public string[] Actors { get; }
        public R5eContentionPermutation[] Contention { get; }
        public R5eScenarioCase[] Cases { get; }
    }

    [Serializable]
    internal sealed class R5eScenarioCatalogDto
    {
        public string schemaVersion = string.Empty;
        public int seed;
        public string[] actors = Array.Empty<string>();
        public int[] chairRotations = Array.Empty<int>();
        public string[] directions = Array.Empty<string>();
        public int baseMatrixCaseCount;
        public int expectedTotalCaseCount;
        public R5eLayoutDto[] layouts = Array.Empty<R5eLayoutDto>();
        public R5eContentionPermutation[] contentionPermutations =
            Array.Empty<R5eContentionPermutation>();
        public int[] faultInjectionIds = Array.Empty<int>();
        public string[] requiredEvents = Array.Empty<string>();
        public R5eAcceptanceDto acceptance = new R5eAcceptanceDto();
    }

    [Serializable]
    internal sealed class R5eLayoutDto
    {
        public string id = string.Empty;
        public string kind = string.Empty;
        public int seed;
    }

    [Serializable]
    internal sealed class R5eContentionPermutation
    {
        public string[] order = Array.Empty<string>();
    }

    [Serializable]
    internal sealed class R5eAcceptanceDto
    {
        public int allExitsBlockedCommitCount;
        public int duplicateOccupancyCount;
        public int clearMaskedViolationCount;
        public int wrongFacingCount;
        public int strafeCount;
        public float minimumForwardDot;
        public float maximumRenderedDisplacementWorld;
        public float maximumGameplayFrameMs;
    }

    internal static class OfficeSeatDockingR5eScenarioCatalog
    {
        public const string SchemaVersion = "classic-seat-docking-r5e-scenarios-v1";
        public const int Seed = 58193017;
        public const int BaseCaseCount = 128;
        public const int TotalCaseCount = 158;
        public const string ExpectedSha256 = "96E281FBCD41061AD1418DDC506B35CD91C4ACB284882CF1755ECC0A1AAE8453";

        private static readonly string[] RequiredActors =
            { "player", "older_sister", "father", "mother" };
        private static readonly int[] RequiredRotations = { 0, 90, 180, 270 };
        private static readonly int[] ArrivalDx = { 0, -1, -1, -1, 0, 1, 1, 1 };
        private static readonly int[] ArrivalDy = { -1, -1, 0, 1, 1, 1, 0, -1 };
        private static readonly string[] RequiredDirections =
        {
            "south", "southwest", "west", "northwest",
            "north", "northeast", "east", "southeast"
        };

        public static OfficeSeatDockingR5eScenarioPlan ParseAndValidate(TextAsset asset)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            return ParseAndValidateJson(asset.text);
        }

        /// <summary>
        /// Pure counterpart used by the no-Unity-process production fixture. Runtime and the
        /// fixture deliberately share this exact hash/schema/seed parser and case generator.
        /// </summary>
        public static OfficeSeatDockingR5eScenarioPlan ParseAndValidateJson(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            string normalized = json.Replace("\r\n", "\n").Replace('\r', '\n');
            string sha = Sha256(normalized);
            if (!string.Equals(sha, ExpectedSha256, StringComparison.Ordinal))
                throw new InvalidOperationException("R5e scenario catalog SHA-256 mismatch: " + sha);
            R5eScenarioCatalogDto dto = ParseExactDto(normalized);
            if (dto == null || dto.schemaVersion != SchemaVersion || dto.seed != Seed ||
                dto.baseMatrixCaseCount != BaseCaseCount || dto.expectedTotalCaseCount != TotalCaseCount)
                throw new InvalidOperationException("R5e scenario catalog identity/count mismatch.");
            RequireExact(dto.actors, RequiredActors, "actors");
            RequireExact(dto.chairRotations, RequiredRotations, "chair rotations");
            RequireExact(dto.directions, RequiredDirections, "arrival directions");
            if (dto.layouts == null || dto.layouts.Length != 4 ||
                dto.layouts[0].kind != "open" ||
                dto.layouts[1].kind != "seeded-obstacles" ||
                dto.layouts[2].kind != "seeded-obstacles" ||
                dto.layouts[3].kind != "all-exits-blocked" ||
                dto.contentionPermutations == null || dto.contentionPermutations.Length != 4 ||
                dto.faultInjectionIds == null || dto.faultInjectionIds.Length != 6)
                throw new InvalidOperationException("R5e scenario catalog matrix is incomplete.");
            for (var index = 0; index < dto.contentionPermutations.Length; index++)
                RequirePermutation(dto.contentionPermutations[index], index);
            for (var index = 0; index < dto.faultInjectionIds.Length; index++)
                if (dto.faultInjectionIds[index] != index + 1)
                    throw new InvalidOperationException("R5e fault injection IDs must be exactly 1..6.");
            if (dto.acceptance == null || dto.acceptance.allExitsBlockedCommitCount != 0 ||
                dto.acceptance.duplicateOccupancyCount != 0 ||
                dto.acceptance.clearMaskedViolationCount != 0 ||
                dto.acceptance.wrongFacingCount != 0 || dto.acceptance.strafeCount != 0 ||
                Math.Abs(dto.acceptance.minimumForwardDot - 0.92f) > 0.000001f ||
                Math.Abs(dto.acceptance.maximumRenderedDisplacementWorld - 0.099f) > 0.000001f ||
                Math.Abs(dto.acceptance.maximumGameplayFrameMs - 50f) > 0.000001f)
                throw new InvalidOperationException("R5e acceptance oracle was weakened.");

            var cases = new R5eScenarioCase[TotalCaseCount];
            var cursor = 0;
            ulong id = 1;
            for (var rotation = 0; rotation < 4; rotation++)
            for (var actor = 0; actor < RequiredActors.Length; actor++)
            for (var direction = 0; direction < 8; direction++)
            {
                cases[cursor++] = new R5eScenarioCase(
                    id++, "base-r" + rotation + "-a" + actor + "-d" + direction,
                    R5eScenarioKind.BaseMatrix, RequiredActors[actor], rotation, direction,
                    Seed, 0, -1);
            }
            for (var layout = 1; layout <= 2; layout++)
            for (var actor = 0; actor < RequiredActors.Length; actor++)
                cases[cursor++] = new R5eScenarioCase(
                    id++, "seeded-l" + layout + "-a" + actor,
                    R5eScenarioKind.SeededObstacles, RequiredActors[actor], layout, actor * 2,
                    dto.layouts[layout].seed, 0, -1);
            for (var actor = 0; actor < RequiredActors.Length; actor++)
                cases[cursor++] = new R5eScenarioCase(
                    id++, "blocked-a" + actor, R5eScenarioKind.AllExitsBlocked,
                    RequiredActors[actor], actor, actor * 2, dto.layouts[3].seed, 0, -1);
            for (var permutation = 0; permutation < dto.contentionPermutations.Length; permutation++)
                cases[cursor++] = new R5eScenarioCase(
                    id++, "contention-p" + permutation, R5eScenarioKind.Contention,
                    dto.contentionPermutations[permutation].order[0], permutation, permutation * 2,
                    Seed, 0, permutation);
            for (var fault = 1; fault <= 6; fault++)
            {
                cases[cursor++] = new R5eScenarioCase(
                    id++, "fault-entry-" + fault, R5eScenarioKind.FaultEntry,
                    RequiredActors[(fault - 1) % 4], 0, fault % 8, Seed, fault, -1);
                cases[cursor++] = new R5eScenarioCase(
                    id++, "fault-exit-" + fault, R5eScenarioKind.FaultExit,
                    RequiredActors[(fault - 1) % 4], 0, fault % 8, Seed, fault, -1);
            }
            cases[cursor++] = new R5eScenarioCase(
                id++, "version-entry", R5eScenarioKind.VersionEntry,
                "player", 0, 0, Seed, 0, -1);
            cases[cursor++] = new R5eScenarioCase(
                id, "version-exit", R5eScenarioKind.VersionExit,
                "player", 0, 4, Seed, 0, -1);
            if (cursor != cases.Length)
                throw new InvalidOperationException("R5e generated scenario count mismatch: " + cursor);
            return new OfficeSeatDockingR5eScenarioPlan(
                sha, dto.seed, (string[])dto.actors.Clone(), dto.contentionPermutations, cases);
        }

        private static R5eScenarioCatalogDto ParseExactDto(string json)
        {
            var dto = new R5eScenarioCatalogDto
            {
                schemaVersion = RequiredString(json, "schemaVersion"),
                seed = RequiredInt(json, "seed"),
                actors = RequiredStringArray(json, "actors"),
                chairRotations = RequiredIntArray(json, "chairRotations"),
                directions = RequiredStringArray(json, "directions"),
                baseMatrixCaseCount = RequiredInt(json, "baseMatrixCaseCount"),
                expectedTotalCaseCount = RequiredInt(json, "expectedTotalCaseCount"),
                faultInjectionIds = RequiredIntArray(json, "faultInjectionIds"),
                requiredEvents = RequiredStringArray(json, "requiredEvents")
            };

            string layoutsBody = RequiredArrayBody(json, "layouts");
            MatchCollection layoutMatches = Regex.Matches(
                layoutsBody,
                "\\{\\s*\\\"id\\\"\\s*:\\s*\\\"(?<id>[^\\\"]+)\\\"\\s*,\\s*" +
                "\\\"kind\\\"\\s*:\\s*\\\"(?<kind>[^\\\"]+)\\\"\\s*,\\s*" +
                "\\\"seed\\\"\\s*:\\s*(?<seed>-?[0-9]+)\\s*\\}",
                RegexOptions.CultureInvariant);
            dto.layouts = new R5eLayoutDto[layoutMatches.Count];
            for (var index = 0; index < layoutMatches.Count; index++)
            {
                Match item = layoutMatches[index];
                dto.layouts[index] = new R5eLayoutDto
                {
                    id = item.Groups["id"].Value,
                    kind = item.Groups["kind"].Value,
                    seed = int.Parse(item.Groups["seed"].Value)
                };
            }

            string contentionBody = RequiredArrayBody(json, "contentionPermutations");
            MatchCollection contentionMatches = Regex.Matches(
                contentionBody,
                "\\{\\s*\\\"order\\\"\\s*:\\s*\\[(?<order>[^\\]]*)\\]\\s*\\}",
                RegexOptions.CultureInvariant);
            dto.contentionPermutations = new R5eContentionPermutation[contentionMatches.Count];
            for (var index = 0; index < contentionMatches.Count; index++)
            {
                dto.contentionPermutations[index] = new R5eContentionPermutation
                {
                    order = ParseQuotedValues(contentionMatches[index].Groups["order"].Value)
                };
            }

            string acceptance = RequiredObjectBody(json, "acceptance");
            dto.acceptance = new R5eAcceptanceDto
            {
                allExitsBlockedCommitCount = RequiredInt(acceptance, "allExitsBlockedCommitCount"),
                duplicateOccupancyCount = RequiredInt(acceptance, "duplicateOccupancyCount"),
                clearMaskedViolationCount = RequiredInt(acceptance, "clearMaskedViolationCount"),
                wrongFacingCount = RequiredInt(acceptance, "wrongFacingCount"),
                strafeCount = RequiredInt(acceptance, "strafeCount"),
                minimumForwardDot = RequiredFloat(acceptance, "minimumForwardDot"),
                maximumRenderedDisplacementWorld = RequiredFloat(acceptance, "maximumRenderedDisplacementWorld"),
                maximumGameplayFrameMs = RequiredFloat(acceptance, "maximumGameplayFrameMs")
            };
            return dto;
        }

        private static string RequiredString(string json, string name)
        {
            Match match = Regex.Match(
                json,
                "\\\"" + Regex.Escape(name) + "\\\"\\s*:\\s*\\\"(?<value>[^\\\"]*)\\\"",
                RegexOptions.CultureInvariant);
            if (!match.Success) throw new InvalidOperationException("R5e catalog field missing: " + name);
            return match.Groups["value"].Value;
        }

        private static int RequiredInt(string json, string name)
        {
            Match match = Regex.Match(
                json,
                "\\\"" + Regex.Escape(name) + "\\\"\\s*:\\s*(?<value>-?[0-9]+)",
                RegexOptions.CultureInvariant);
            if (!match.Success) throw new InvalidOperationException("R5e catalog integer missing: " + name);
            return int.Parse(match.Groups["value"].Value);
        }

        private static float RequiredFloat(string json, string name)
        {
            Match match = Regex.Match(
                json,
                "\\\"" + Regex.Escape(name) + "\\\"\\s*:\\s*(?<value>-?[0-9]+(?:\\.[0-9]+)?)",
                RegexOptions.CultureInvariant);
            if (!match.Success) throw new InvalidOperationException("R5e catalog number missing: " + name);
            return float.Parse(
                match.Groups["value"].Value,
                System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string[] RequiredStringArray(string json, string name) =>
            ParseQuotedValues(RequiredArrayBody(json, name));

        private static int[] RequiredIntArray(string json, string name)
        {
            string body = RequiredArrayBody(json, name);
            MatchCollection matches = Regex.Matches(body, "-?[0-9]+", RegexOptions.CultureInvariant);
            var result = new int[matches.Count];
            for (var index = 0; index < matches.Count; index++)
                result[index] = int.Parse(matches[index].Value);
            return result;
        }

        private static string[] ParseQuotedValues(string body)
        {
            MatchCollection matches = Regex.Matches(
                body,
                "\\\"(?<value>[^\\\"]*)\\\"",
                RegexOptions.CultureInvariant);
            var result = new string[matches.Count];
            for (var index = 0; index < matches.Count; index++)
                result[index] = matches[index].Groups["value"].Value;
            return result;
        }

        private static string RequiredArrayBody(string json, string name)
        {
            return RequiredDelimitedBody(json, name, '[', ']', "array");
        }

        private static string RequiredObjectBody(string json, string name)
        {
            return RequiredDelimitedBody(json, name, '{', '}', "object");
        }

        private static string RequiredDelimitedBody(
            string json,
            string name,
            char open,
            char close,
            string kind)
        {
            int key = json.IndexOf("\"" + name + "\"", StringComparison.Ordinal);
            if (key < 0) throw new InvalidOperationException("R5e catalog " + kind + " missing: " + name);
            int colon = json.IndexOf(':', key + name.Length + 2);
            int start = colon < 0 ? -1 : json.IndexOf(open, colon + 1);
            if (start < 0) throw new InvalidOperationException("R5e catalog " + kind + " missing: " + name);
            int depth = 0;
            bool quoted = false;
            bool escaped = false;
            for (var index = start; index < json.Length; index++)
            {
                char value = json[index];
                if (quoted)
                {
                    if (escaped) escaped = false;
                    else if (value == '\\') escaped = true;
                    else if (value == '"') quoted = false;
                    continue;
                }
                if (value == '"')
                {
                    quoted = true;
                    continue;
                }
                if (value == open) depth++;
                else if (value == close && --depth == 0)
                    return json.Substring(start + 1, index - start - 1);
            }
            throw new InvalidOperationException("R5e catalog " + kind + " is unterminated: " + name);
        }

        public static OfficeGrid RotateLayout(OfficeGrid source, int quarterTurns)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            int turns = ((quarterTurns % 4) + 4) % 4;
            int width = turns % 2 == 0 ? source.Width : source.Height;
            int height = turns % 2 == 0 ? source.Height : source.Width;
            var floor = new OfficeFloorTileKind[width * height];
            var walkable = new bool[width * height];
            for (var y = 0; y < source.Height; y++)
            for (var x = 0; x < source.Width; x++)
            {
                OfficeGridCoordinate next = RotateCell(
                    new OfficeGridCoordinate(x, y), source.Width, source.Height, turns);
                int nextIndex = next.Y * width + next.X;
                var old = new OfficeGridCoordinate(x, y);
                floor[nextIndex] = source.FloorAt(old);
                walkable[nextIndex] = source.IsWalkable(old);
            }

            var furniture = new List<PlacedOfficeFurniture>(source.Furniture.Count);
            foreach (PlacedOfficeFurniture item in source.Furniture)
            {
                OfficeGridCoordinate first = RotateCell(item.Origin, source.Width, source.Height, turns);
                OfficeGridCoordinate last = RotateCell(
                    new OfficeGridCoordinate(item.Origin.X + item.Width - 1,
                        item.Origin.Y + item.Height - 1),
                    source.Width, source.Height, turns);
                var origin = new OfficeGridCoordinate(
                    Math.Min(first.X, last.X), Math.Min(first.Y, last.Y));
                int itemWidth = Math.Abs(first.X - last.X) + 1;
                int itemHeight = Math.Abs(first.Y - last.Y) + 1;
                furniture.Add(new PlacedOfficeFurniture(
                    item.FurnitureId,
                    item.KindId,
                    origin,
                    itemWidth,
                    itemHeight,
                    RotateAnchor(item.PlacementAnchor, source.Width, source.Height, turns),
                    RotateFacing(item.Facing, turns),
                    item.BlocksMovement));
            }
            var seats = new List<OfficeSeatSlot>(source.SeatSlots.Count);
            foreach (OfficeSeatSlot seat in source.SeatSlots)
            {
                seats.Add(new OfficeSeatSlot(
                    seat.SeatId,
                    seat.ChairFurnitureId,
                    seat.WorkSurfaceFurnitureId,
                    RotateCell(seat.Cell, source.Width, source.Height, turns),
                    RotateCell(seat.ApproachCell, source.Width, source.Height, turns),
                    RotateAnchor(seat.OperatorAnchor, source.Width, source.Height, turns),
                    RotateFacing(seat.Facing, turns)));
            }
            return new OfficeGrid(width, height, floor, walkable, furniture, seats);
        }

        public static OfficeGrid CreateLayoutForCase(
            OfficeGrid approvedBase,
            in R5eScenarioCase scenario)
        {
            OfficeGrid rotated = RotateLayout(approvedBase, scenario.RotationQuarterTurns);
            if (scenario.Kind != R5eScenarioKind.SeededObstacles) return rotated;
            var floor = new OfficeFloorTileKind[rotated.Width * rotated.Height];
            var walkable = new bool[floor.Length];
            var protectedCells = new HashSet<OfficeGridCoordinate>();
            for (var index = 0; index < rotated.SeatSlots.Count; index++)
            {
                OfficeSeatSlot seat = rotated.SeatSlots[index];
                protectedCells.Add(seat.Cell);
                protectedCells.Add(seat.ApproachCell);
                IReadOnlyList<OfficeSeatEgressCandidate> exits =
                    OfficeSeatEgressRules.ResolveCandidates(seat);
                for (var exit = 0; exit < exits.Count; exit++)
                    protectedCells.Add(exits[exit].TargetCell);
            }
            for (var y = 0; y < rotated.Height; y++)
            for (var x = 0; x < rotated.Width; x++)
            {
                var cell = new OfficeGridCoordinate(x, y);
                int flat = y * rotated.Width + x;
                floor[flat] = rotated.FloorAt(cell);
                walkable[flat] = rotated.IsWalkable(cell);
            }
            uint state = unchecked((uint)scenario.Seed);
            int placed = 0;
            int attempts = rotated.Width * rotated.Height * 2;
            while (attempts-- > 0 && placed < 8)
            {
                state = state * 1664525u + 1013904223u;
                int x = (int)(state % (uint)rotated.Width);
                state = state * 1664525u + 1013904223u;
                int y = (int)(state % (uint)rotated.Height);
                var cell = new OfficeGridCoordinate(x, y);
                int flat = y * rotated.Width + x;
                if (!walkable[flat] || protectedCells.Contains(cell)) continue;
                walkable[flat] = false;
                placed++;
            }
            if (placed != 8) throw new InvalidOperationException(
                "R5e seeded obstacle layout could not place exact blockers: " + placed);
            return new OfficeGrid(
                rotated.Width,
                rotated.Height,
                floor,
                walkable,
                rotated.Furniture,
                rotated.SeatSlots);
        }

        public static OfficeGridCoordinate FindArrivalCell(
            OfficeGrid grid,
            OfficeSeatSlot seat,
            int direction)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (seat == null) throw new ArgumentNullException(nameof(seat));
            int index = ((direction % 8) + 8) % 8;
            for (var distance = 3; distance >= 1; distance--)
            {
                var candidate = new OfficeGridCoordinate(
                    seat.ApproachCell.X + ArrivalDx[index] * distance,
                    seat.ApproachCell.Y + ArrivalDy[index] * distance);
                if (grid.Contains(candidate) && grid.IsWalkable(candidate)) return candidate;
            }
            return seat.ApproachCell;
        }

        private static OfficeGridCoordinate RotateCell(
            OfficeGridCoordinate cell,
            int width,
            int height,
            int turns)
        {
            int x = cell.X;
            int y = cell.Y;
            int currentWidth = width;
            int currentHeight = height;
            for (var step = 0; step < turns; step++)
            {
                int nextX = currentHeight - 1 - y;
                int nextY = x;
                x = nextX;
                y = nextY;
                int swap = currentWidth;
                currentWidth = currentHeight;
                currentHeight = swap;
            }
            return new OfficeGridCoordinate(x, y);
        }

        private static OfficeGridSubcellAnchor RotateAnchor(
            OfficeGridSubcellAnchor anchor,
            int width,
            int height,
            int turns)
        {
            int x2 = anchor.X2;
            int y2 = anchor.Y2;
            int currentWidth = width;
            int currentHeight = height;
            for (var step = 0; step < turns; step++)
            {
                int nextX2 = (currentHeight - 1) * 2 - y2;
                int nextY2 = x2;
                x2 = nextX2;
                y2 = nextY2;
                int swap = currentWidth;
                currentWidth = currentHeight;
                currentHeight = swap;
            }
            return new OfficeGridSubcellAnchor(x2, y2);
        }

        private static OfficeFurnitureFacing RotateFacing(
            OfficeFurnitureFacing facing,
            int turns)
        {
            int value = (int)facing;
            for (var step = 0; step < turns; step++)
            {
                value = value switch
                {
                    0 => 3,
                    1 => 0,
                    2 => 1,
                    3 => 2,
                    _ => throw new ArgumentOutOfRangeException(nameof(facing))
                };
            }
            return (OfficeFurnitureFacing)value;
        }

        private static void RequireExact<T>(T[] actual, T[] expected, string label)
        {
            if (actual == null || actual.Length != expected.Length)
                throw new InvalidOperationException("R5e catalog " + label + " count mismatch.");
            for (var index = 0; index < expected.Length; index++)
                if (!EqualityComparer<T>.Default.Equals(actual[index], expected[index]))
                    throw new InvalidOperationException("R5e catalog " + label + " order mismatch.");
        }

        private static void RequirePermutation(R5eContentionPermutation permutation, int index)
        {
            if (permutation == null || permutation.order == null ||
                permutation.order.Length != RequiredActors.Length)
                throw new InvalidOperationException("R5e contention permutation is incomplete: " + index);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var actor = 0; actor < permutation.order.Length; actor++)
                if (Array.IndexOf(RequiredActors, permutation.order[actor]) < 0 ||
                    !seen.Add(permutation.order[actor]))
                    throw new InvalidOperationException("R5e contention permutation is invalid: " + index);
        }

        private static string Sha256(string value)
        {
            using SHA256 hash = SHA256.Create();
            byte[] bytes = hash.ComputeHash(Encoding.UTF8.GetBytes(value));
            var builder = new StringBuilder(64);
            for (var index = 0; index < bytes.Length; index++) builder.Append(bytes[index].ToString("X2"));
            return builder.ToString();
        }
    }
}
