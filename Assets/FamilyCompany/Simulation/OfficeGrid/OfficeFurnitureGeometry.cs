using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace FamilyCompany.Simulation.OfficeLayout
{
    public enum OfficeFurnitureGeometrySocketKind
    {
        InteractionAccess = 0,
        SeatEgressFront = 1,
        SeatEgressLeft = 2,
        SeatEgressRight = 3
    }

    /// <summary>
    /// Exact furniture-local ground coordinate in quarter-cell units. These points describe the
    /// contact geometry on the floor, never the sprite alpha envelope.
    /// </summary>
    public readonly struct OfficeFurnitureLocalPoint : IEquatable<OfficeFurnitureLocalPoint>
    {
        public OfficeFurnitureLocalPoint(int x4, int y4)
        {
            X4 = x4;
            Y4 = y4;
        }

        public int X4 { get; }
        public int Y4 { get; }

        public bool Equals(OfficeFurnitureLocalPoint other) => X4 == other.X4 && Y4 == other.Y4;
        public override bool Equals(object obj) => obj is OfficeFurnitureLocalPoint other && Equals(other);
        public override int GetHashCode() => unchecked((X4 * 397) ^ Y4);
        public override string ToString() => $"({X4}/4,{Y4}/4)";
    }

    public readonly struct OfficeFurniturePixelPoint : IEquatable<OfficeFurniturePixelPoint>
    {
        public OfficeFurniturePixelPoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }
        public int Y { get; }

        public bool Equals(OfficeFurniturePixelPoint other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is OfficeFurniturePixelPoint other && Equals(other);
        public override int GetHashCode() => unchecked((X * 397) ^ Y);
        public override string ToString() => $"({X}px,{Y}px)";
    }

    public sealed class OfficeFurnitureVisualOcclusionRegion
    {
        public const int ReferenceTilePixelWidth = 320;
        public const int ReferenceTilePixelHeight = 160;

        public OfficeFurnitureVisualOcclusionRegion(
            IEnumerable<OfficeFurnitureLocalPoint> groundProjectionPolygon,
            int heightPixels)
        {
            if (groundProjectionPolygon == null) throw new ArgumentNullException(nameof(groundProjectionPolygon));
            var points = groundProjectionPolygon.ToArray();
            ValidatePolygon(points, "visual occlusion");
            if (heightPixels <= 0) throw new ArgumentOutOfRangeException(nameof(heightPixels));
            GroundProjectionPolygon = Array.AsReadOnly(points);
            HeightPixels = heightPixels;
            GroundProjectionPolygonPixels = Array.AsReadOnly(points.Select(ToIsometricPixels).ToArray());
        }

        public IReadOnlyList<OfficeFurnitureLocalPoint> GroundProjectionPolygon { get; }
        public IReadOnlyList<OfficeFurniturePixelPoint> GroundProjectionPolygonPixels { get; }
        public int HeightPixels { get; }

        public static OfficeFurniturePixelPoint ToIsometricPixels(OfficeFurnitureLocalPoint point)
        {
            // One grid X step is (+160,+80) px and one grid Y step is (-160,+80) px.
            return new OfficeFurniturePixelPoint(
                checked((point.X4 - point.Y4) * (ReferenceTilePixelWidth / 8)),
                checked((point.X4 + point.Y4) * (ReferenceTilePixelHeight / 8)));
        }

        internal static void ValidatePolygon(
            IReadOnlyList<OfficeFurnitureLocalPoint> points,
            string label)
        {
            if (points == null || points.Count < 3)
                throw new ArgumentException(label + " polygon requires at least three points.");
            long signedAreaTwice = 0;
            for (var index = 0; index < points.Count; index++)
            {
                OfficeFurnitureLocalPoint current = points[index];
                OfficeFurnitureLocalPoint next = points[(index + 1) % points.Count];
                signedAreaTwice += (long)current.X4 * next.Y4 - (long)next.X4 * current.Y4;
            }
            if (signedAreaTwice == 0)
                throw new ArgumentException(label + " polygon is degenerate.");
        }
    }

    public sealed class OfficeFurnitureGeometrySocket
    {
        public OfficeFurnitureGeometrySocket(
            OfficeFurnitureGeometrySocketKind kind,
            int slotIndex,
            OfficeGridCoordinate cellOffset,
            OfficeFurnitureLocalPoint anchor,
            OfficeFurnitureFacing desiredActorFacing)
        {
            if (slotIndex < 0) throw new ArgumentOutOfRangeException(nameof(slotIndex));
            int minX4 = checked(cellOffset.X * OfficeFurnitureGeometryProfile.SubcellsPerCell);
            int minY4 = checked(cellOffset.Y * OfficeFurnitureGeometryProfile.SubcellsPerCell);
            if (anchor.X4 < minX4 || anchor.X4 >= minX4 + OfficeFurnitureGeometryProfile.SubcellsPerCell ||
                anchor.Y4 < minY4 || anchor.Y4 >= minY4 + OfficeFurnitureGeometryProfile.SubcellsPerCell)
                throw new ArgumentException("Socket anchor must be inside its declared cell.", nameof(anchor));
            Kind = kind;
            SlotIndex = slotIndex;
            CellOffset = cellOffset;
            Anchor = anchor;
            DesiredActorFacing = desiredActorFacing;
        }

        public OfficeFurnitureGeometrySocketKind Kind { get; }
        public int SlotIndex { get; }
        public OfficeGridCoordinate CellOffset { get; }
        public OfficeFurnitureLocalPoint Anchor { get; }
        public OfficeFurnitureFacing DesiredActorFacing { get; }

        public bool IsSeatEgress => Kind == OfficeFurnitureGeometrySocketKind.SeatEgressFront ||
                                    Kind == OfficeFurnitureGeometrySocketKind.SeatEgressLeft ||
                                    Kind == OfficeFurnitureGeometrySocketKind.SeatEgressRight;
    }

    /// <summary>
    /// One immutable, rotation-specific furniture geometry profile. Collision rows are baked from
    /// SolidGroundPolygon; they are not authored independently and can never drift from it.
    /// </summary>
    public sealed class OfficeFurnitureGeometryProfile
    {
        public const int SubcellsPerCell = 4;

        public OfficeFurnitureGeometryProfile(
            OfficeFurnitureFacing facing,
            int footprintWidth,
            int footprintHeight,
            IEnumerable<OfficeFurnitureLocalPoint> solidGroundPolygon,
            OfficeFurnitureVisualOcclusionRegion visualOcclusion,
            IEnumerable<OfficeFurnitureGeometrySocket> sockets)
        {
            if (footprintWidth <= 0) throw new ArgumentOutOfRangeException(nameof(footprintWidth));
            if (footprintHeight <= 0) throw new ArgumentOutOfRangeException(nameof(footprintHeight));
            if (solidGroundPolygon == null) throw new ArgumentNullException(nameof(solidGroundPolygon));
            var polygon = solidGroundPolygon.ToArray();
            OfficeFurnitureVisualOcclusionRegion.ValidatePolygon(polygon, "solid ground");
            int maxX4 = checked(footprintWidth * SubcellsPerCell);
            int maxY4 = checked(footprintHeight * SubcellsPerCell);
            if (polygon.Any(point => point.X4 < 0 || point.Y4 < 0 || point.X4 > maxX4 || point.Y4 > maxY4))
                throw new ArgumentException("Solid ground polygon must remain inside the semantic footprint.");

            Facing = facing;
            FootprintWidth = footprintWidth;
            FootprintHeight = footprintHeight;
            SolidGroundPolygon = Array.AsReadOnly(polygon);
            VisualOcclusion = visualOcclusion ?? throw new ArgumentNullException(nameof(visualOcclusion));
            var socketArray = sockets == null ? Array.Empty<OfficeFurnitureGeometrySocket>() : sockets.ToArray();
            if (socketArray.Any(item => item == null))
                throw new ArgumentException("Geometry sockets cannot contain null.", nameof(sockets));
            Sockets = Array.AsReadOnly(socketArray);
            InteractionAccessSockets = Array.AsReadOnly(socketArray
                .Where(item => item.Kind == OfficeFurnitureGeometrySocketKind.InteractionAccess)
                .ToArray());
            SeatEgressSockets = Array.AsReadOnly(socketArray.Where(item => item.IsSeatEgress).ToArray());
            BakedSolidGroundRows = Array.AsReadOnly(BakeRows(polygon, footprintWidth, footprintHeight));
            SolidSubcellCount = BakedSolidGroundRows.Sum(row => row.Count(character => character == '#'));
            if (SolidSubcellCount == 0)
                throw new ArgumentException("Solid ground polygon did not cover any collision subcells.");
        }

        public OfficeFurnitureFacing Facing { get; }
        public int FootprintWidth { get; }
        public int FootprintHeight { get; }
        public IReadOnlyList<OfficeFurnitureLocalPoint> SolidGroundPolygon { get; }
        public IReadOnlyList<string> BakedSolidGroundRows { get; }
        public int SolidSubcellCount { get; }
        public OfficeFurnitureVisualOcclusionRegion VisualOcclusion { get; }
        public IReadOnlyList<OfficeFurnitureGeometrySocket> Sockets { get; }
        public IReadOnlyList<OfficeFurnitureGeometrySocket> InteractionAccessSockets { get; }
        public IReadOnlyList<OfficeFurnitureGeometrySocket> SeatEgressSockets { get; }

        public bool IsSolidGroundSubcell(int subcellX, int subcellY)
        {
            if (subcellX < 0 || subcellY < 0 ||
                subcellX >= FootprintWidth * SubcellsPerCell ||
                subcellY >= FootprintHeight * SubcellsPerCell)
                return false;
            return BakedSolidGroundRows[subcellY][subcellX] == '#';
        }

        internal OfficeFurnitureGeometryProfile RotateClockwise()
        {
            int oldWidth4 = checked(FootprintWidth * SubcellsPerCell);
            OfficeFurnitureLocalPoint[] solid = SolidGroundPolygon
                .Select(point => new OfficeFurnitureLocalPoint(point.Y4, oldWidth4 - point.X4))
                .Reverse()
                .ToArray();
            OfficeFurnitureLocalPoint[] occlusion = VisualOcclusion.GroundProjectionPolygon
                .Select(point => new OfficeFurnitureLocalPoint(point.Y4, oldWidth4 - point.X4))
                .Reverse()
                .ToArray();
            OfficeFurnitureGeometrySocket[] rotatedSockets = Sockets.Select(socket =>
            {
                int newCellX = socket.CellOffset.Y;
                int newCellY = FootprintWidth - 1 - socket.CellOffset.X;
                return new OfficeFurnitureGeometrySocket(
                    socket.Kind,
                    socket.SlotIndex,
                    new OfficeGridCoordinate(newCellX, newCellY),
                    new OfficeFurnitureLocalPoint(socket.Anchor.Y4, oldWidth4 - socket.Anchor.X4),
                    QuarterTurnClockwise(socket.DesiredActorFacing));
            }).ToArray();
            return new OfficeFurnitureGeometryProfile(
                QuarterTurnClockwise(Facing),
                FootprintHeight,
                FootprintWidth,
                solid,
                new OfficeFurnitureVisualOcclusionRegion(occlusion, VisualOcclusion.HeightPixels),
                rotatedSockets);
        }

        private static string[] BakeRows(
            IReadOnlyList<OfficeFurnitureLocalPoint> polygon,
            int width,
            int height)
        {
            var rows = new string[height * SubcellsPerCell];
            for (var y = 0; y < rows.Length; y++)
            {
                var characters = new char[width * SubcellsPerCell];
                for (var x = 0; x < characters.Length; x++)
                    characters[x] = PointInsidePolygon(x + 0.5, y + 0.5, polygon) ? '#' : '.';
                rows[y] = new string(characters);
            }
            return rows;
        }

        private static bool PointInsidePolygon(
            double x,
            double y,
            IReadOnlyList<OfficeFurnitureLocalPoint> polygon)
        {
            var inside = false;
            for (int current = 0, previous = polygon.Count - 1;
                 current < polygon.Count;
                 previous = current++)
            {
                OfficeFurnitureLocalPoint a = polygon[current];
                OfficeFurnitureLocalPoint b = polygon[previous];
                bool crosses = (a.Y4 > y) != (b.Y4 > y) &&
                               x < (double)(b.X4 - a.X4) * (y - a.Y4) / (b.Y4 - a.Y4) + a.X4;
                if (crosses) inside = !inside;
            }
            return inside;
        }

        private static OfficeFurnitureFacing QuarterTurnClockwise(OfficeFurnitureFacing facing) =>
            (OfficeFurnitureFacing)(((int)facing + 1) & 3);
    }

    public sealed class OfficeFurnitureGeometryDefinition
    {
        private readonly ReadOnlyDictionary<OfficeFurnitureFacing, OfficeFurnitureGeometryProfile> _profiles;

        private OfficeFurnitureGeometryDefinition(
            string definitionId,
            IDictionary<OfficeFurnitureFacing, OfficeFurnitureGeometryProfile> profiles)
        {
            DefinitionId = (definitionId ?? string.Empty).Trim();
            if (DefinitionId.Length == 0) throw new ArgumentException("Definition ID is required.", nameof(definitionId));
            if (profiles == null || profiles.Count != 4)
                throw new ArgumentException("All four rotation profiles are required.", nameof(profiles));
            foreach (OfficeFurnitureFacing facing in Enum.GetValues(typeof(OfficeFurnitureFacing)))
            {
                if (!profiles.TryGetValue(facing, out OfficeFurnitureGeometryProfile profile) ||
                    profile == null || profile.Facing != facing)
                    throw new ArgumentException("Missing or mismatched furniture geometry facing: " + facing);
            }
            _profiles = new ReadOnlyDictionary<OfficeFurnitureFacing, OfficeFurnitureGeometryProfile>(
                new Dictionary<OfficeFurnitureFacing, OfficeFurnitureGeometryProfile>(profiles));
        }

        public string DefinitionId { get; }
        public IReadOnlyDictionary<OfficeFurnitureFacing, OfficeFurnitureGeometryProfile> Profiles => _profiles;

        public OfficeFurnitureGeometryProfile ForFacing(OfficeFurnitureFacing facing) => _profiles[facing];

        public static OfficeFurnitureGeometryDefinition FromSouthEast(
            string definitionId,
            OfficeFurnitureGeometryProfile southEast)
        {
            if (southEast == null) throw new ArgumentNullException(nameof(southEast));
            if (southEast.Facing != OfficeFurnitureFacing.SouthEast)
                throw new ArgumentException("Canonical geometry must face SouthEast.", nameof(southEast));
            var profiles = new Dictionary<OfficeFurnitureFacing, OfficeFurnitureGeometryProfile>();
            OfficeFurnitureGeometryProfile current = southEast;
            for (var turn = 0; turn < 4; turn++)
            {
                profiles.Add(current.Facing, current);
                current = current.RotateClockwise();
            }
            return new OfficeFurnitureGeometryDefinition(definitionId, profiles);
        }
    }

    public sealed class OfficeFurnitureWorldSocket
    {
        internal OfficeFurnitureWorldSocket(
            OfficeFurnitureGeometrySocket source,
            OfficeGridCoordinate worldCell,
            OfficeFurnitureLocalPoint worldAnchor)
        {
            Source = source;
            WorldCell = worldCell;
            WorldAnchor = worldAnchor;
        }

        public OfficeFurnitureGeometrySocket Source { get; }
        public OfficeGridCoordinate WorldCell { get; }
        public OfficeFurnitureLocalPoint WorldAnchor { get; }
        public OfficeFurnitureGeometrySocketKind Kind => Source.Kind;
        public int SlotIndex => Source.SlotIndex;
        public OfficeFurnitureFacing DesiredActorFacing => Source.DesiredActorFacing;
    }

    public sealed class OfficeFurnitureGeometrySnapshot
    {
        internal OfficeFurnitureGeometrySnapshot(
            string definitionId,
            OfficeGridCoordinate origin,
            OfficeFurnitureGeometryProfile profile)
        {
            DefinitionId = definitionId;
            Origin = origin;
            Profile = profile;
            int originX4 = checked(origin.X * OfficeFurnitureGeometryProfile.SubcellsPerCell);
            int originY4 = checked(origin.Y * OfficeFurnitureGeometryProfile.SubcellsPerCell);
            WorldSolidGroundPolygon = Array.AsReadOnly(profile.SolidGroundPolygon
                .Select(point => new OfficeFurnitureLocalPoint(point.X4 + originX4, point.Y4 + originY4))
                .ToArray());
            WorldVisualOcclusionGroundPolygon = Array.AsReadOnly(profile.VisualOcclusion.GroundProjectionPolygon
                .Select(point => new OfficeFurnitureLocalPoint(point.X4 + originX4, point.Y4 + originY4))
                .ToArray());
            WorldSockets = Array.AsReadOnly(profile.Sockets.Select(socket =>
                new OfficeFurnitureWorldSocket(
                    socket,
                    new OfficeGridCoordinate(origin.X + socket.CellOffset.X, origin.Y + socket.CellOffset.Y),
                    new OfficeFurnitureLocalPoint(socket.Anchor.X4 + originX4, socket.Anchor.Y4 + originY4)))
                .ToArray());
            InteractionAccessSockets = Array.AsReadOnly(WorldSockets
                .Where(item => item.Kind == OfficeFurnitureGeometrySocketKind.InteractionAccess)
                .ToArray());
            SeatEgressSockets = Array.AsReadOnly(WorldSockets.Where(item => item.Source.IsSeatEgress).ToArray());
        }

        public string DefinitionId { get; }
        public OfficeGridCoordinate Origin { get; }
        public OfficeFurnitureGeometryProfile Profile { get; }
        public IReadOnlyList<OfficeFurnitureLocalPoint> WorldSolidGroundPolygon { get; }
        public IReadOnlyList<OfficeFurnitureLocalPoint> WorldVisualOcclusionGroundPolygon { get; }
        public IReadOnlyList<OfficeFurnitureWorldSocket> WorldSockets { get; }
        public IReadOnlyList<OfficeFurnitureWorldSocket> InteractionAccessSockets { get; }
        public IReadOnlyList<OfficeFurnitureWorldSocket> SeatEgressSockets { get; }
    }

    /// <summary>
    /// Read-only contract for movement/placement consumers. It publishes geometry only; path,
    /// reservation, seat-claim, and interaction lifecycle remain owned by their runtime services.
    /// </summary>
    public interface IReadOnlyOfficeFurnitureGeometryQuery
    {
        bool TryResolve(
            string definitionId,
            OfficeGridCoordinate origin,
            OfficeFurnitureFacing facing,
            out OfficeFurnitureGeometrySnapshot geometry);

        OfficeFurnitureGeometrySnapshot Resolve(PlacedOfficeFurniture furniture);
    }

    public sealed class OfficeFurnitureGeometryQuery : IReadOnlyOfficeFurnitureGeometryQuery
    {
        public static readonly OfficeFurnitureGeometryQuery Shared = new OfficeFurnitureGeometryQuery();

        private OfficeFurnitureGeometryQuery()
        {
        }

        public bool TryResolve(
            string definitionId,
            OfficeGridCoordinate origin,
            OfficeFurnitureFacing facing,
            out OfficeFurnitureGeometrySnapshot geometry)
        {
            OfficeFurnitureDefinition definition = OfficeFurnitureCatalog.Find(definitionId);
            if (definition?.Geometry == null)
            {
                geometry = null;
                return false;
            }
            geometry = new OfficeFurnitureGeometrySnapshot(
                definition.DefinitionId,
                origin,
                definition.Geometry.ForFacing(facing));
            return true;
        }

        public OfficeFurnitureGeometrySnapshot Resolve(PlacedOfficeFurniture furniture)
        {
            if (furniture == null) throw new ArgumentNullException(nameof(furniture));
            if (!TryResolve(furniture.KindId, furniture.Origin, furniture.Facing, out OfficeFurnitureGeometrySnapshot result))
                throw new KeyNotFoundException("No canonical geometry for furniture: " + furniture.KindId);
            if (result.Profile.FootprintWidth != furniture.Width ||
                result.Profile.FootprintHeight != furniture.Height)
                throw new InvalidOperationException(
                    $"Furniture '{furniture.FurnitureId}' footprint {furniture.Width}x{furniture.Height} " +
                    $"does not match definition geometry {result.Profile.FootprintWidth}x{result.Profile.FootprintHeight}.");
            return result;
        }
    }

    internal static class OfficeFurnitureGeometryFactory
    {
        public static OfficeFurnitureGeometryDefinition Create(
            string definitionId,
            int width,
            int height,
            OfficeFurnitureCapability capabilities,
            int capacity,
            OfficeFurnitureAccessPolicy accessPolicy)
        {
            OfficeFurnitureLocalPoint[] solid = SolidPolygon(definitionId, width, height);
            var occlusion = new OfficeFurnitureVisualOcclusionRegion(
                Rectangle(width, height),
                OcclusionHeightPixels(definitionId));
            OfficeFurnitureGeometrySocket[] sockets = BuildSockets(
                width,
                height,
                capabilities,
                capacity,
                accessPolicy).ToArray();
            return OfficeFurnitureGeometryDefinition.FromSouthEast(
                definitionId,
                new OfficeFurnitureGeometryProfile(
                    OfficeFurnitureFacing.SouthEast,
                    width,
                    height,
                    solid,
                    occlusion,
                    sockets));
        }

        private static IEnumerable<OfficeFurnitureGeometrySocket> BuildSockets(
            int width,
            int height,
            OfficeFurnitureCapability capabilities,
            int capacity,
            OfficeFurnitureAccessPolicy accessPolicy)
        {
            var result = new List<OfficeFurnitureGeometrySocket>();
            if (accessPolicy != OfficeFurnitureAccessPolicy.None &&
                capabilities != OfficeFurnitureCapability.None)
            {
                List<OfficeGridCoordinate> perimeter = FrontPerimeter(width, height);
                int slots = Math.Max(1, capacity);
                for (var slot = 0; slot < slots; slot++)
                {
                    // Every semantic use slot receives explicit front/side/back alternatives.
                    // Reservation services may claim one of them; geometry itself stays read-only.
                    foreach (OfficeGridCoordinate cell in perimeter)
                    {
                        result.Add(Socket(
                            OfficeFurnitureGeometrySocketKind.InteractionAccess,
                            slot,
                            cell,
                            FacingTowardFootprint(cell, width, height)));
                    }
                }
            }

            if ((capabilities & OfficeFurnitureCapability.Seat) != 0)
            {
                int seats = Math.Max(1, capacity);
                for (var seat = 0; seat < seats; seat++)
                {
                    int seatX = Math.Min(width - 1, seat % width);
                    result.Add(Socket(
                        OfficeFurnitureGeometrySocketKind.SeatEgressFront,
                        seat,
                        new OfficeGridCoordinate(seatX, -1),
                        OfficeFurnitureFacing.NorthWest));
                    result.Add(Socket(
                        OfficeFurnitureGeometrySocketKind.SeatEgressLeft,
                        seat,
                        new OfficeGridCoordinate(-1, Math.Min(height - 1, seat / width)),
                        OfficeFurnitureFacing.NorthEast));
                    result.Add(Socket(
                        OfficeFurnitureGeometrySocketKind.SeatEgressRight,
                        seat,
                        new OfficeGridCoordinate(width, Math.Min(height - 1, seat / width)),
                        OfficeFurnitureFacing.SouthWest));
                }
            }
            return result;
        }

        private static List<OfficeGridCoordinate> FrontPerimeter(int width, int height)
        {
            var result = new List<OfficeGridCoordinate>();
            for (var x = 0; x < width; x++) result.Add(new OfficeGridCoordinate(x, -1));
            for (var y = 0; y < height; y++) result.Add(new OfficeGridCoordinate(width, y));
            for (var x = width - 1; x >= 0; x--) result.Add(new OfficeGridCoordinate(x, height));
            for (var y = height - 1; y >= 0; y--) result.Add(new OfficeGridCoordinate(-1, y));
            return result;
        }

        private static OfficeFurnitureGeometrySocket Socket(
            OfficeFurnitureGeometrySocketKind kind,
            int slot,
            OfficeGridCoordinate cell,
            OfficeFurnitureFacing desiredFacing)
        {
            return new OfficeFurnitureGeometrySocket(
                kind,
                slot,
                cell,
                new OfficeFurnitureLocalPoint(
                    checked(cell.X * OfficeFurnitureGeometryProfile.SubcellsPerCell + 2),
                    checked(cell.Y * OfficeFurnitureGeometryProfile.SubcellsPerCell + 2)),
                desiredFacing);
        }

        private static OfficeFurnitureFacing FacingTowardFootprint(
            OfficeGridCoordinate cell,
            int width,
            int height)
        {
            if (cell.Y < 0) return OfficeFurnitureFacing.NorthWest;
            if (cell.X >= width) return OfficeFurnitureFacing.SouthWest;
            if (cell.Y >= height) return OfficeFurnitureFacing.SouthEast;
            return OfficeFurnitureFacing.NorthEast;
        }

        private static OfficeFurnitureLocalPoint[] SolidPolygon(string id, int width, int height)
        {
            int width4 = checked(width * OfficeFurnitureGeometryProfile.SubcellsPerCell);
            int height4 = checked(height * OfficeFurnitureGeometryProfile.SubcellsPerCell);
            if (string.Equals(id, OfficeGridLayouts.PottedPlantKind, StringComparison.Ordinal))
                return new[]
                {
                    new OfficeFurnitureLocalPoint(1, 1), new OfficeFurnitureLocalPoint(3, 1),
                    new OfficeFurnitureLocalPoint(3, 3), new OfficeFurnitureLocalPoint(1, 3)
                };
            if (string.Equals(id, OfficeGridLayouts.WaterDispenserKind, StringComparison.Ordinal))
                return new[]
                {
                    new OfficeFurnitureLocalPoint(1, 0), new OfficeFurnitureLocalPoint(3, 0),
                    new OfficeFurnitureLocalPoint(3, 2), new OfficeFurnitureLocalPoint(4, 2),
                    new OfficeFurnitureLocalPoint(4, 3), new OfficeFurnitureLocalPoint(3, 3),
                    new OfficeFurnitureLocalPoint(3, 4), new OfficeFurnitureLocalPoint(1, 4),
                    new OfficeFurnitureLocalPoint(1, 3), new OfficeFurnitureLocalPoint(0, 3),
                    new OfficeFurnitureLocalPoint(0, 2), new OfficeFurnitureLocalPoint(1, 2)
                };
            if (width4 == 4 && height4 == 4)
                return new[]
                {
                    new OfficeFurnitureLocalPoint(2, 0), new OfficeFurnitureLocalPoint(4, 2),
                    new OfficeFurnitureLocalPoint(2, 4), new OfficeFurnitureLocalPoint(0, 2)
                };
            if (width4 == 4)
                return new[]
                {
                    new OfficeFurnitureLocalPoint(2, 0), new OfficeFurnitureLocalPoint(4, 2),
                    new OfficeFurnitureLocalPoint(4, height4 - 2), new OfficeFurnitureLocalPoint(2, height4),
                    new OfficeFurnitureLocalPoint(0, height4 - 2), new OfficeFurnitureLocalPoint(0, 2)
                };
            if (height4 == 4)
                return new[]
                {
                    new OfficeFurnitureLocalPoint(2, 0), new OfficeFurnitureLocalPoint(width4 - 2, 0),
                    new OfficeFurnitureLocalPoint(width4, 2), new OfficeFurnitureLocalPoint(width4 - 2, 4),
                    new OfficeFurnitureLocalPoint(2, 4), new OfficeFurnitureLocalPoint(0, 2)
                };
            if (width4 >= 4 && height4 >= 4)
                return new[]
                {
                    new OfficeFurnitureLocalPoint(1, 0), new OfficeFurnitureLocalPoint(width4 - 1, 0),
                    new OfficeFurnitureLocalPoint(width4, 1), new OfficeFurnitureLocalPoint(width4, height4 - 1),
                    new OfficeFurnitureLocalPoint(width4 - 1, height4), new OfficeFurnitureLocalPoint(1, height4),
                    new OfficeFurnitureLocalPoint(0, height4 - 1), new OfficeFurnitureLocalPoint(0, 1)
                };
            return Rectangle(width, height);
        }

        private static OfficeFurnitureLocalPoint[] Rectangle(int width, int height)
        {
            int width4 = checked(width * OfficeFurnitureGeometryProfile.SubcellsPerCell);
            int height4 = checked(height * OfficeFurnitureGeometryProfile.SubcellsPerCell);
            return new[]
            {
                new OfficeFurnitureLocalPoint(0, 0), new OfficeFurnitureLocalPoint(width4, 0),
                new OfficeFurnitureLocalPoint(width4, height4), new OfficeFurnitureLocalPoint(0, height4)
            };
        }

        private static int OcclusionHeightPixels(string id)
        {
            if (string.Equals(id, OfficeGridLayouts.DeskWithPcKind, StringComparison.Ordinal)) return 360;
            if (string.Equals(id, OfficeGridLayouts.SwivelChairKind, StringComparison.Ordinal)) return 250;
            if (string.Equals(id, OfficeGridLayouts.DocumentBookcaseKind, StringComparison.Ordinal)) return 400;
            if (string.Equals(id, OfficeGridLayouts.FaxCopierKind, StringComparison.Ordinal)) return 300;
            if (string.Equals(id, OfficeGridLayouts.WaterDispenserKind, StringComparison.Ordinal)) return 380;
            if (string.Equals(id, OfficeFurnitureCatalog.DrinkVendingMachineDefinitionId, StringComparison.Ordinal)) return 430;
            if (string.Equals(id, OfficeGridLayouts.SofaKind, StringComparison.Ordinal)) return 280;
            if (string.Equals(id, OfficeGridLayouts.PottedPlantKind, StringComparison.Ordinal)) return 380;
            return 320;
        }
    }
}
