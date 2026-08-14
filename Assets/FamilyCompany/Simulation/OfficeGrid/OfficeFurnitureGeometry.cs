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
        SeatEgressRight = 3,
        SeatContact = 4,
        WorkstationOperator = 5,
        KeyboardWork = 6,
        MonitorCenter = 7
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
            IEnumerable<OfficeFurnitureGeometrySocket> sockets,
            float clearancePaddingWorld = 0f)
        {
            if (footprintWidth <= 0) throw new ArgumentOutOfRangeException(nameof(footprintWidth));
            if (footprintHeight <= 0) throw new ArgumentOutOfRangeException(nameof(footprintHeight));
            if (solidGroundPolygon == null) throw new ArgumentNullException(nameof(solidGroundPolygon));
            if (clearancePaddingWorld < 0f || float.IsNaN(clearancePaddingWorld) ||
                float.IsInfinity(clearancePaddingWorld))
                throw new ArgumentOutOfRangeException(nameof(clearancePaddingWorld));
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
            ClearancePaddingWorld = clearancePaddingWorld;
            var socketArray = sockets == null ? Array.Empty<OfficeFurnitureGeometrySocket>() : sockets.ToArray();
            if (socketArray.Any(item => item == null))
                throw new ArgumentException("Geometry sockets cannot contain null.", nameof(sockets));
            Sockets = Array.AsReadOnly(socketArray);
            InteractionAccessSockets = Array.AsReadOnly(socketArray
                .Where(item => item.Kind == OfficeFurnitureGeometrySocketKind.InteractionAccess)
                .ToArray());
            SeatEgressSockets = Array.AsReadOnly(socketArray.Where(item => item.IsSeatEgress).ToArray());
            SeatContactSockets = Array.AsReadOnly(socketArray
                .Where(item => item.Kind == OfficeFurnitureGeometrySocketKind.SeatContact).ToArray());
            WorkstationOperatorSockets = Array.AsReadOnly(socketArray
                .Where(item => item.Kind == OfficeFurnitureGeometrySocketKind.WorkstationOperator).ToArray());
            KeyboardWorkSockets = Array.AsReadOnly(socketArray
                .Where(item => item.Kind == OfficeFurnitureGeometrySocketKind.KeyboardWork).ToArray());
            MonitorCenterSockets = Array.AsReadOnly(socketArray
                .Where(item => item.Kind == OfficeFurnitureGeometrySocketKind.MonitorCenter).ToArray());
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
        public float ClearancePaddingWorld { get; }
        public IReadOnlyList<OfficeFurnitureGeometrySocket> Sockets { get; }
        public IReadOnlyList<OfficeFurnitureGeometrySocket> InteractionAccessSockets { get; }
        public IReadOnlyList<OfficeFurnitureGeometrySocket> SeatEgressSockets { get; }
        public IReadOnlyList<OfficeFurnitureGeometrySocket> SeatContactSockets { get; }
        public IReadOnlyList<OfficeFurnitureGeometrySocket> WorkstationOperatorSockets { get; }
        public IReadOnlyList<OfficeFurnitureGeometrySocket> KeyboardWorkSockets { get; }
        public IReadOnlyList<OfficeFurnitureGeometrySocket> MonitorCenterSockets { get; }

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
            OfficeFurnitureLocalPoint[] solid = SolidGroundPolygon
                .Select(point => OfficeFurnitureRotationTransform.RotateLocalPointClockwise(
                    point, FootprintWidth))
                .Reverse()
                .ToArray();
            OfficeFurnitureLocalPoint[] occlusion = VisualOcclusion.GroundProjectionPolygon
                .Select(point => OfficeFurnitureRotationTransform.RotateLocalPointClockwise(
                    point, FootprintWidth))
                .Reverse()
                .ToArray();
            OfficeFurnitureGeometrySocket[] rotatedSockets = Sockets.Select(socket =>
            {
                OfficeGridCoordinate cell = OfficeFurnitureRotationTransform.RotateCellOffsetClockwise(
                    socket.CellOffset, FootprintWidth);
                return new OfficeFurnitureGeometrySocket(
                    socket.Kind,
                    socket.SlotIndex,
                    cell,
                    OfficeFurnitureRotationTransform.RotateLocalPointClockwise(
                        socket.Anchor, FootprintWidth),
                    OfficeFurnitureRotationTransform.QuarterTurnClockwise(socket.DesiredActorFacing));
            }).ToArray();
            return new OfficeFurnitureGeometryProfile(
                OfficeFurnitureRotationTransform.QuarterTurnClockwise(Facing),
                FootprintHeight,
                FootprintWidth,
                solid,
                new OfficeFurnitureVisualOcclusionRegion(occlusion, VisualOcclusion.HeightPixels),
                rotatedSockets,
                ClearancePaddingWorld);
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

        public static OfficeFurnitureGeometryDefinition FromCanonical(
            string definitionId,
            OfficeFurnitureGeometryProfile canonical)
        {
            if (canonical == null) throw new ArgumentNullException(nameof(canonical));
            var profiles = new Dictionary<OfficeFurnitureFacing, OfficeFurnitureGeometryProfile>();
            OfficeFurnitureGeometryProfile current = canonical;
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
            SeatContactSockets = Array.AsReadOnly(WorldSockets
                .Where(item => item.Kind == OfficeFurnitureGeometrySocketKind.SeatContact).ToArray());
            WorkstationOperatorSockets = Array.AsReadOnly(WorldSockets
                .Where(item => item.Kind == OfficeFurnitureGeometrySocketKind.WorkstationOperator).ToArray());
            KeyboardWorkSockets = Array.AsReadOnly(WorldSockets
                .Where(item => item.Kind == OfficeFurnitureGeometrySocketKind.KeyboardWork).ToArray());
            MonitorCenterSockets = Array.AsReadOnly(WorldSockets
                .Where(item => item.Kind == OfficeFurnitureGeometrySocketKind.MonitorCenter).ToArray());
        }

        public string DefinitionId { get; }
        public OfficeGridCoordinate Origin { get; }
        public OfficeFurnitureGeometryProfile Profile { get; }
        public IReadOnlyList<OfficeFurnitureLocalPoint> WorldSolidGroundPolygon { get; }
        public IReadOnlyList<OfficeFurnitureLocalPoint> WorldVisualOcclusionGroundPolygon { get; }
        public IReadOnlyList<OfficeFurnitureWorldSocket> WorldSockets { get; }
        public IReadOnlyList<OfficeFurnitureWorldSocket> InteractionAccessSockets { get; }
        public IReadOnlyList<OfficeFurnitureWorldSocket> SeatEgressSockets { get; }
        public IReadOnlyList<OfficeFurnitureWorldSocket> SeatContactSockets { get; }
        public IReadOnlyList<OfficeFurnitureWorldSocket> WorkstationOperatorSockets { get; }
        public IReadOnlyList<OfficeFurnitureWorldSocket> KeyboardWorkSockets { get; }
        public IReadOnlyList<OfficeFurnitureWorldSocket> MonitorCenterSockets { get; }
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
            OfficeFurnitureAccessPolicy accessPolicy,
            OfficeFurnitureFacing canonicalFacing)
        {
            OfficeFurnitureLocalPoint[] solid = SolidPolygon(definitionId, width, height);
            var occlusion = new OfficeFurnitureVisualOcclusionRegion(
                Rectangle(width, height),
                OcclusionHeightPixels(definitionId));
            OfficeFurnitureGeometrySocket[] sockets = BuildSockets(
                definitionId,
                width,
                height,
                capabilities,
                capacity,
                accessPolicy,
                canonicalFacing).ToArray();
            return OfficeFurnitureGeometryDefinition.FromCanonical(
                definitionId,
                new OfficeFurnitureGeometryProfile(
                    canonicalFacing,
                    width,
                    height,
                    solid,
                    occlusion,
                    sockets,
                    ClearancePaddingWorld(definitionId)));
        }

        private static IEnumerable<OfficeFurnitureGeometrySocket> BuildSockets(
            string definitionId,
            int width,
            int height,
            OfficeFurnitureCapability capabilities,
            int capacity,
            OfficeFurnitureAccessPolicy accessPolicy,
            OfficeFurnitureFacing canonicalFacing)
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
                    var seatCell = new OfficeGridCoordinate(seatX, 0);
                    OfficeFurnitureFacing away = OfficeFurnitureRotationTransform.Opposite(canonicalFacing);
                    OfficeGridCoordinate frontStep = OfficeFurnitureRotationTransform.FacingStep(away);
                    OfficeGridCoordinate leftStep = OfficeFurnitureRotationTransform.FacingStep(
                        OfficeFurnitureRotationTransform.QuarterTurnClockwise(away));
                    OfficeGridCoordinate rightStep = OfficeFurnitureRotationTransform.FacingStep(
                        OfficeFurnitureRotationTransform.QuarterTurnClockwise(
                            OfficeFurnitureRotationTransform.QuarterTurnClockwise(
                                OfficeFurnitureRotationTransform.QuarterTurnClockwise(away))));
                    OfficeGridCoordinate frontCell = Add(seatCell, frontStep);
                    OfficeGridCoordinate leftCell = Add(seatCell, leftStep);
                    OfficeGridCoordinate rightCell = Add(seatCell, rightStep);
                    result.Add(Socket(
                        OfficeFurnitureGeometrySocketKind.SeatContact,
                        seat,
                        seatCell,
                        canonicalFacing));
                    result.Add(Socket(
                        OfficeFurnitureGeometrySocketKind.SeatEgressFront,
                        seat,
                        frontCell,
                        FacingTowardFootprint(frontCell, width, height)));
                    result.Add(Socket(
                        OfficeFurnitureGeometrySocketKind.SeatEgressLeft,
                        seat,
                        leftCell,
                        FacingTowardFootprint(leftCell, width, height)));
                    result.Add(Socket(
                        OfficeFurnitureGeometrySocketKind.SeatEgressRight,
                        seat,
                        rightCell,
                        FacingTowardFootprint(rightCell, width, height)));
                }
            }

            if (string.Equals(definitionId, OfficeGridLayouts.DeskWithPcKind, StringComparison.Ordinal))
            {
                // SouthEast is the canonical desk orientation. Its operator chair occupies the
                // explicit front socket at local (0,-1) and faces NorthWest. All other directions
                // are generated by the same quarter-turn transform as collision and egress.
                result.Add(Socket(
                    OfficeFurnitureGeometrySocketKind.WorkstationOperator,
                    0,
                    new OfficeGridCoordinate(0, -1),
                    OfficeFurnitureFacing.NorthWest));
                result.Add(new OfficeFurnitureGeometrySocket(
                    OfficeFurnitureGeometrySocketKind.KeyboardWork,
                    0,
                    new OfficeGridCoordinate(0, 0),
                    new OfficeFurnitureLocalPoint(2, 1),
                    OfficeFurnitureFacing.NorthWest));
                result.Add(new OfficeFurnitureGeometrySocket(
                    OfficeFurnitureGeometrySocketKind.MonitorCenter,
                    0,
                    new OfficeGridCoordinate(0, 0),
                    new OfficeFurnitureLocalPoint(2, 3),
                    OfficeFurnitureFacing.NorthWest));
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

        private static OfficeGridCoordinate Add(OfficeGridCoordinate left, OfficeGridCoordinate right) =>
            new OfficeGridCoordinate(left.X + right.X, left.Y + right.Y);

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
            // These orthogonal contours are traced around the approved 4x4-per-cell ground-contact
            // rows. Unlike a mathematical diamond sampled on its boundary, they bake symmetrically
            // on both axes and therefore reproduce the exact collision profile consumed by runtime.
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
            if (string.Equals(id, OfficeGridLayouts.MeetingTableKind, StringComparison.Ordinal))
                return new[]
                {
                    new OfficeFurnitureLocalPoint(1, 0), new OfficeFurnitureLocalPoint(7, 0),
                    new OfficeFurnitureLocalPoint(7, 4), new OfficeFurnitureLocalPoint(1, 4)
                };
            if (string.Equals(id, OfficeGridLayouts.CoffeeTableKind, StringComparison.Ordinal))
                return new[]
                {
                    new OfficeFurnitureLocalPoint(2, 0), new OfficeFurnitureLocalPoint(6, 0),
                    new OfficeFurnitureLocalPoint(6, 1), new OfficeFurnitureLocalPoint(7, 1),
                    new OfficeFurnitureLocalPoint(7, 3), new OfficeFurnitureLocalPoint(6, 3),
                    new OfficeFurnitureLocalPoint(6, 4), new OfficeFurnitureLocalPoint(2, 4),
                    new OfficeFurnitureLocalPoint(2, 3), new OfficeFurnitureLocalPoint(1, 3),
                    new OfficeFurnitureLocalPoint(1, 1), new OfficeFurnitureLocalPoint(2, 1)
                };
            if (string.Equals(id, OfficeGridLayouts.PartitionKind, StringComparison.Ordinal))
                return new[]
                {
                    new OfficeFurnitureLocalPoint(1, 0), new OfficeFurnitureLocalPoint(3, 0),
                    new OfficeFurnitureLocalPoint(3, height4), new OfficeFurnitureLocalPoint(1, height4)
                };
            if (width4 == 4 && height4 == 4)
                return new[]
                {
                    new OfficeFurnitureLocalPoint(1, 0), new OfficeFurnitureLocalPoint(3, 0),
                    new OfficeFurnitureLocalPoint(3, 1), new OfficeFurnitureLocalPoint(4, 1),
                    new OfficeFurnitureLocalPoint(4, 3), new OfficeFurnitureLocalPoint(3, 3),
                    new OfficeFurnitureLocalPoint(3, 4), new OfficeFurnitureLocalPoint(1, 4),
                    new OfficeFurnitureLocalPoint(1, 3), new OfficeFurnitureLocalPoint(0, 3),
                    new OfficeFurnitureLocalPoint(0, 1), new OfficeFurnitureLocalPoint(1, 1)
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
                    new OfficeFurnitureLocalPoint(1, 0), new OfficeFurnitureLocalPoint(width4 - 1, 0),
                    new OfficeFurnitureLocalPoint(width4 - 1, 1), new OfficeFurnitureLocalPoint(width4, 1),
                    new OfficeFurnitureLocalPoint(width4, 3), new OfficeFurnitureLocalPoint(width4 - 1, 3),
                    new OfficeFurnitureLocalPoint(width4 - 1, 4), new OfficeFurnitureLocalPoint(1, 4),
                    new OfficeFurnitureLocalPoint(1, 3), new OfficeFurnitureLocalPoint(0, 3),
                    new OfficeFurnitureLocalPoint(0, 1), new OfficeFurnitureLocalPoint(1, 1)
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

        private static float ClearancePaddingWorld(string id)
        {
            if (string.Equals(id, OfficeGridLayouts.SwivelChairKind, StringComparison.Ordinal) ||
                string.Equals(id, OfficeGridLayouts.CoffeeTableKind, StringComparison.Ordinal) ||
                string.Equals(id, OfficeGridLayouts.PottedPlantKind, StringComparison.Ordinal))
                return 0.01f;
            return 0.02f;
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
