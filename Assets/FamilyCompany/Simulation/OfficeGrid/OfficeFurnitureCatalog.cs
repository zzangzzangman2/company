using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace FamilyCompany.Simulation.OfficeLayout
{
    public enum OfficeFurnitureCategory
    {
        Work = 0,
        Seating = 1,
        OfficeEquipment = 2,
        Storage = 3,
        Refreshment = 4,
        Rest = 5,
        Decoration = 6,
        Divider = 7,
        Structure = 8
    }

    [Flags]
    public enum OfficeFurnitureCapability
    {
        None = 0,
        WorkDesk = 1 << 0,
        Seat = 1 << 1,
        WaterSource = 1 << 2,
        DrinkVending = 1 << 3,
        CoffeeSource = 1 << 4,
        RestSeat = 1 << 5,
        Printer = 1 << 6,
        Filing = 1 << 7
    }

    public enum OfficeFurnitureAccessPolicy
    {
        None = 0,
        AdjacentCardinal = 1,
        AdjacentOrTwoCells = 2,
        SeatCell = 3
    }

    /// <summary>
    /// Data-only furniture definition. Sprite fields are stable presentation keys rather than
    /// Unity objects so Simulation remains engine independent.
    /// </summary>
    public sealed class OfficeFurnitureDefinition
    {
        public OfficeFurnitureDefinition(
            string definitionId,
            string koreanDisplayName,
            OfficeFurnitureCategory category,
            int baseWidth,
            int baseHeight,
            string visualAssetId,
            string frontOverlayAssetId,
            string anchorProfileId,
            long purchasePriceWon,
            int resaleRateBasisPoints,
            long dailyMaintenanceWon,
            OfficeFurnitureCapability capabilities,
            int capacity,
            OfficeFurnitureAccessPolicy accessPolicy,
            OfficeFurnitureFacing desiredFacing,
            bool blocksNavigation,
            string needCapabilityTag = "",
            int maximumOwned = 0,
            string unlockId = "",
            bool isPlayerEditable = true)
        {
            DefinitionId = Required(definitionId, nameof(definitionId));
            KoreanDisplayName = Required(koreanDisplayName, nameof(koreanDisplayName));
            if (baseWidth <= 0 || baseHeight <= 0) throw new ArgumentOutOfRangeException(nameof(baseWidth));
            if (purchasePriceWon < 0) throw new ArgumentOutOfRangeException(nameof(purchasePriceWon));
            if (resaleRateBasisPoints < 0 || resaleRateBasisPoints > 10000)
                throw new ArgumentOutOfRangeException(nameof(resaleRateBasisPoints));
            if (dailyMaintenanceWon < 0) throw new ArgumentOutOfRangeException(nameof(dailyMaintenanceWon));
            if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            if (maximumOwned < 0) throw new ArgumentOutOfRangeException(nameof(maximumOwned));

            Category = category;
            BaseWidth = baseWidth;
            BaseHeight = baseHeight;
            VisualAssetId = Optional(visualAssetId);
            FrontOverlayAssetId = Optional(frontOverlayAssetId);
            AnchorProfileId = Optional(anchorProfileId);
            PurchasePriceWon = purchasePriceWon;
            ResaleRateBasisPoints = resaleRateBasisPoints;
            DailyMaintenanceWon = dailyMaintenanceWon;
            Capabilities = capabilities;
            Capacity = capacity;
            AccessPolicy = accessPolicy;
            DesiredFacing = desiredFacing;
            BlocksNavigation = blocksNavigation;
            NeedCapabilityTag = Optional(needCapabilityTag);
            MaximumOwned = maximumOwned;
            UnlockId = Optional(unlockId);
            IsPlayerEditable = isPlayerEditable;
            Geometry = isPlayerEditable
                ? OfficeFurnitureGeometryFactory.Create(
                    DefinitionId,
                    baseWidth,
                    baseHeight,
                    capabilities,
                    capacity,
                    accessPolicy)
                : null;
        }

        public string DefinitionId { get; }
        public string KoreanDisplayName { get; }
        public OfficeFurnitureCategory Category { get; }
        public int BaseWidth { get; }
        public int BaseHeight { get; }
        public string VisualAssetId { get; }
        public string FrontOverlayAssetId { get; }
        public string AnchorProfileId { get; }
        public long PurchasePriceWon { get; }
        public int ResaleRateBasisPoints { get; }
        public long DailyMaintenanceWon { get; }
        public string Currency => OfficeFurnitureEconomyConfig.Currency;
        public int BaseYear => OfficeFurnitureEconomyConfig.BaseYear;
        public int MaximumOwned { get; }
        public string UnlockId { get; }
        public OfficeFurnitureCapability Capabilities { get; }
        public int Capacity { get; }
        public OfficeFurnitureAccessPolicy AccessPolicy { get; }
        public OfficeFurnitureFacing DesiredFacing { get; }
        public bool BlocksNavigation { get; }
        public string NeedCapabilityTag { get; }
        public bool IsPlayerEditable { get; }
        public bool IsPurchasable => IsPlayerEditable && PurchasePriceWon > 0;
        public OfficeFurnitureGeometryDefinition Geometry { get; }

        public OfficeGridCoordinate FootprintFor(OfficeFurnitureFacing facing)
        {
            if (Geometry != null)
            {
                OfficeFurnitureGeometryProfile profile = Geometry.ForFacing(facing);
                return new OfficeGridCoordinate(profile.FootprintWidth, profile.FootprintHeight);
            }
            bool quarterTurn = facing == OfficeFurnitureFacing.SouthWest ||
                               facing == OfficeFurnitureFacing.NorthEast;
            return quarterTurn
                ? new OfficeGridCoordinate(BaseHeight, BaseWidth)
                : new OfficeGridCoordinate(BaseWidth, BaseHeight);
        }

        public bool HasCapability(OfficeFurnitureCapability capability) =>
            (Capabilities & capability) == capability;

        private static string Required(string value, string parameter)
        {
            string canonical = Optional(value);
            if (canonical.Length == 0) throw new ArgumentException("Value is required.", parameter);
            return canonical;
        }

        private static string Optional(string value) => (value ?? string.Empty).Trim();
    }

    /// <summary>
    /// A single, explicit economy scale. Definition prices remain 2000 KRW reference prices;
    /// gameplay never applies per-item hidden multipliers.
    /// </summary>
    public static class OfficeFurnitureEconomyConfig
    {
        public const string Currency = "KRW";
        public const int BaseYear = 2000;
        public const int GameplayPriceScaleBasisPoints = 2500;

        public static long GameplayPrice(long baseYearPriceWon)
        {
            if (baseYearPriceWon < 0) throw new ArgumentOutOfRangeException(nameof(baseYearPriceWon));
            return checked((baseYearPriceWon * GameplayPriceScaleBasisPoints + 5000L) / 10000L);
        }

        public static long ResaleValue(long purchaseBasisWon, int resaleRateBasisPoints)
        {
            if (purchaseBasisWon < 0) throw new ArgumentOutOfRangeException(nameof(purchaseBasisWon));
            if (resaleRateBasisPoints < 0 || resaleRateBasisPoints > 10000)
                throw new ArgumentOutOfRangeException(nameof(resaleRateBasisPoints));
            return checked((purchaseBasisWon * resaleRateBasisPoints + 5000L) / 10000L);
        }
    }

    public static class OfficeFurnitureCatalog
    {
        // Kept here instead of OfficeGridLayouts so the parallel wall/door task can evolve the
        // structural layout file without taking a merge dependency on the build-mode catalog.
        public const string DrinkVendingMachineDefinitionId = "drink_vending_machine";

        private static readonly ReadOnlyCollection<OfficeFurnitureDefinition> CanonicalDefinitions =
            Array.AsReadOnly(new[]
            {
                D(OfficeGridLayouts.DeskWithPcKind, "CRT 업무 책상·회전의자 세트", OfficeFurnitureCategory.Work, 2, 1,
                    1400000, 3500, 900, OfficeFurnitureCapability.WorkDesk, 1,
                    OfficeFurnitureAccessPolicy.AdjacentCardinal, true, "WorkDesk"),
                D(OfficeGridLayouts.SwivelChairKind, "사무용 회전의자", OfficeFurnitureCategory.Seating, 1, 1,
                    200000, 5500, 100, OfficeFurnitureCapability.Seat, 1,
                    OfficeFurnitureAccessPolicy.SeatCell, false, "Seat", OfficeFurnitureFacing.NorthWest),
                D(OfficeGridLayouts.ReceptionCounterKind, "접수 카운터", OfficeFurnitureCategory.Work, 2, 1,
                    360000, 5000, 250, OfficeFurnitureCapability.WorkDesk, 1,
                    OfficeFurnitureAccessPolicy.AdjacentCardinal, true, "WorkDesk"),
                D(OfficeGridLayouts.MeetingTableKind, "4인 회의 탁자", OfficeFurnitureCategory.Work, 2, 1,
                    420000, 5000, 250, OfficeFurnitureCapability.WorkDesk, 4,
                    OfficeFurnitureAccessPolicy.AdjacentCardinal, true, "Meeting"),
                D(OfficeGridLayouts.DocumentBookcaseKind, "문서 책장", OfficeFurnitureCategory.Storage, 1, 1,
                    180000, 5500, 120, OfficeFurnitureCapability.Filing, 1,
                    OfficeFurnitureAccessPolicy.AdjacentCardinal, true, "Filing"),
                D(OfficeGridLayouts.FaxCopierKind, "팩스·레이저 복합기", OfficeFurnitureCategory.OfficeEquipment, 1, 1,
                    2400000, 3000, 2200, OfficeFurnitureCapability.Printer, 1,
                    OfficeFurnitureAccessPolicy.AdjacentCardinal, true, "Printer"),
                D(OfficeGridLayouts.WaterDispenserKind, "냉온수 정수기", OfficeFurnitureCategory.Refreshment, 1, 1,
                    380000, 4000, 700, OfficeFurnitureCapability.WaterSource, 1,
                    OfficeFurnitureAccessPolicy.AdjacentCardinal, true, "WaterSource"),
                D(DrinkVendingMachineDefinitionId, "음료·간식 자판기", OfficeFurnitureCategory.Refreshment, 1, 1,
                    2600000, 4500, 3000, OfficeFurnitureCapability.DrinkVending, 1,
                    OfficeFurnitureAccessPolicy.AdjacentCardinal, true, "DrinkVending"),
                D(OfficeGridLayouts.SofaKind, "2인 휴식 소파", OfficeFurnitureCategory.Rest, 2, 1,
                    520000, 5000, 300, OfficeFurnitureCapability.Seat | OfficeFurnitureCapability.RestSeat, 2,
                    OfficeFurnitureAccessPolicy.AdjacentOrTwoCells, true, "RestSeat"),
                D(OfficeGridLayouts.CoffeeTableKind, "커피 서비스 테이블", OfficeFurnitureCategory.Refreshment, 2, 1,
                    210000, 5000, 500, OfficeFurnitureCapability.CoffeeSource, 2,
                    OfficeFurnitureAccessPolicy.AdjacentOrTwoCells, true, "CoffeeSource"),
                D(OfficeGridLayouts.PottedPlantKind, "실내 화분", OfficeFurnitureCategory.Decoration, 1, 1,
                    55000, 2500, 100, OfficeFurnitureCapability.None, 0,
                    OfficeFurnitureAccessPolicy.None, true),
                D(OfficeGridLayouts.PartitionKind, "사무용 파티션", OfficeFurnitureCategory.Divider, 1, 2,
                    130000, 5000, 80, OfficeFurnitureCapability.None, 0,
                    OfficeFurnitureAccessPolicy.None, true, facing: OfficeFurnitureFacing.NorthWest),
                D(OfficeGridLayouts.FilingCabinetKind, "4단 철제 서류함", OfficeFurnitureCategory.Storage, 1, 1,
                    220000, 5500, 150, OfficeFurnitureCapability.Filing, 1,
                    OfficeFurnitureAccessPolicy.AdjacentCardinal, true, "Filing"),
                Structural(OfficeGridLayouts.EntranceDoorKind),
                Structural(OfficeGridLayouts.EntranceWallKind),
                Structural(OfficeGridLayouts.PerimeterCutawayWallKind)
            });

        private static readonly Dictionary<string, OfficeFurnitureDefinition> ById =
            CanonicalDefinitions.ToDictionary(item => item.DefinitionId, StringComparer.Ordinal);

        public static IReadOnlyList<OfficeFurnitureDefinition> All => CanonicalDefinitions;
        public static IEnumerable<OfficeFurnitureDefinition> Purchasable =>
            CanonicalDefinitions.Where(item => item.IsPurchasable);
        // Only the approved 3D workstation is currently sold. Keep the other definitions and
        // generic transactions for existing saves/fixtures; they are not new-game shop offers.
        public static IEnumerable<OfficeFurnitureDefinition> ShopOffers =>
            Purchasable.Where(item => IsWorkstationSetOffer(item.DefinitionId));

        public static bool IsWorkstationSetOffer(string definitionId) =>
            string.Equals(definitionId, OfficeGridLayouts.DeskWithPcKind, StringComparison.Ordinal);

        public static bool IsShopOffer(string definitionId) =>
            ShopOffers.Any(item => string.Equals(item.DefinitionId, definitionId, StringComparison.Ordinal));

        public static long GameplayShopPrice(OfficeFurnitureDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            long price = OfficeFurnitureEconomyConfig.GameplayPrice(definition.PurchasePriceWon);
            if (!IsWorkstationSetOffer(definition.DefinitionId)) return price;
            return checked(GameplayWorkstationComponentPrice(false) + GameplayWorkstationComponentPrice(true));
        }

        public static long GameplayWorkstationComponentPrice(bool chair)
        {
            var tuning = Navigation.OfficeDevelopmentTuningSession.Current;
            if (tuning != null)
            {
                long chairBasis = tuning.WorkstationPriceWon / 8;
                return chair ? chairBasis : tuning.WorkstationPriceWon - chairBasis;
            }
            return OfficeFurnitureEconomyConfig.GameplayPrice(Require(chair
                ? OfficeGridLayouts.SwivelChairKind : OfficeGridLayouts.DeskWithPcKind).PurchasePriceWon);
        }

        public static long ShopDailyMaintenanceWon(OfficeFurnitureDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (!IsWorkstationSetOffer(definition.DefinitionId)) return definition.DailyMaintenanceWon;
            return checked(definition.DailyMaintenanceWon +
                           Require(OfficeGridLayouts.SwivelChairKind).DailyMaintenanceWon);
        }

        public static OfficeFurnitureDefinition Find(string definitionId)
        {
            if (string.IsNullOrWhiteSpace(definitionId)) return null;
            ById.TryGetValue(definitionId.Trim(), out OfficeFurnitureDefinition value);
            return value;
        }

        public static OfficeFurnitureDefinition Require(string definitionId) =>
            Find(definitionId) ?? throw new KeyNotFoundException("Unknown office furniture definition: " + definitionId);

        private static OfficeFurnitureDefinition D(
            string id,
            string name,
            OfficeFurnitureCategory category,
            int width,
            int height,
            long price,
            int resale,
            long maintenance,
            OfficeFurnitureCapability capabilities,
            int capacity,
            OfficeFurnitureAccessPolicy access,
            bool blocking,
            string needTag = "",
            OfficeFurnitureFacing facing = OfficeFurnitureFacing.SouthEast)
        {
            return new OfficeFurnitureDefinition(
                id, name, category, width, height, id, id + ":front", id + ":anchors",
                price, resale, maintenance, capabilities, capacity, access, facing, blocking, needTag);
        }

        private static OfficeFurnitureDefinition Structural(string id)
        {
            return new OfficeFurnitureDefinition(
                id, id, OfficeFurnitureCategory.Structure, 1, 1, id, string.Empty, id + ":anchors",
                0, 0, 0, OfficeFurnitureCapability.None, 0, OfficeFurnitureAccessPolicy.None,
                OfficeFurnitureFacing.SouthEast, false, isPlayerEditable: false);
        }
    }
}
