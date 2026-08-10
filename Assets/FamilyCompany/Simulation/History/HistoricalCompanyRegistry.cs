using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using FamilyCompany.Simulation.Market;

namespace FamilyCompany.Simulation.History
{
    public sealed class HistoricalCompanyName
    {
        public HistoricalCompanyName(
            string legalNameKo,
            string legalNameEn,
            string displayNameKo,
            DateTime fromDate,
            DateTime? toDate,
            bool needsReview)
        {
            LegalNameKo = legalNameKo ?? string.Empty;
            LegalNameEn = legalNameEn ?? string.Empty;
            DisplayNameKo = displayNameKo ?? string.Empty;
            FromDate = fromDate.Date;
            ToDate = toDate?.Date;
            NeedsReview = needsReview;
        }

        public string LegalNameKo { get; }
        public string LegalNameEn { get; }
        public string DisplayNameKo { get; }
        public DateTime FromDate { get; }
        public DateTime? ToDate { get; }
        public bool NeedsReview { get; }

        public bool IsActiveAt(DateTime date)
        {
            var day = date.Date;
            return FromDate <= day && (!ToDate.HasValue || day <= ToDate.Value);
        }
    }

    public sealed class HistoricalListingRecord
    {
        public HistoricalListingRecord(
            string market,
            string ticker,
            string status,
            DateTime fromDate,
            DateTime? toDate,
            bool needsReview)
        {
            Market = market ?? string.Empty;
            Ticker = ticker ?? string.Empty;
            Status = status ?? string.Empty;
            FromDate = fromDate.Date;
            ToDate = toDate?.Date;
            NeedsReview = needsReview;
        }

        public string Market { get; }
        public string Ticker { get; }
        public string Status { get; }
        public DateTime FromDate { get; }
        public DateTime? ToDate { get; }
        public bool NeedsReview { get; }

        public bool IsListedAt(DateTime date)
        {
            var day = date.Date;
            return string.Equals(Status, "listed", StringComparison.Ordinal) &&
                   FromDate <= day &&
                   (!ToDate.HasValue || day <= ToDate.Value);
        }

        public bool IsDomesticExchange =>
            string.Equals(Market, "KOSPI", StringComparison.Ordinal) ||
            string.Equals(Market, "KOSDAQ", StringComparison.Ordinal);
    }

    public sealed class HistoricalCompanyDefinition
    {
        public HistoricalCompanyDefinition(
            string companyId,
            string countryCode,
            string companySizeTier,
            string detailLevel,
            string playerReachIn2000,
            IReadOnlyList<string> industryIds,
            IReadOnlyList<HistoricalCompanyName> nameHistory,
            IReadOnlyList<HistoricalListingRecord> listingHistory,
            bool needsReview)
        {
            CompanyId = string.IsNullOrWhiteSpace(companyId)
                ? throw new ArgumentException("Company ID is required.", nameof(companyId))
                : companyId;
            CountryCode = countryCode ?? string.Empty;
            CompanySizeTier = companySizeTier ?? string.Empty;
            DetailLevel = detailLevel ?? string.Empty;
            PlayerReachIn2000 = playerReachIn2000 ?? string.Empty;
            IndustryIds = industryIds ?? Array.Empty<string>();
            NameHistory = nameHistory ?? Array.Empty<HistoricalCompanyName>();
            ListingHistory = listingHistory ?? Array.Empty<HistoricalListingRecord>();
            NeedsReview = needsReview;
        }

        public string CompanyId { get; }
        public string CountryCode { get; }
        public string CompanySizeTier { get; }
        public string DetailLevel { get; }
        public string PlayerReachIn2000 { get; }
        public IReadOnlyList<string> IndustryIds { get; }
        public IReadOnlyList<HistoricalCompanyName> NameHistory { get; }
        public IReadOnlyList<HistoricalListingRecord> ListingHistory { get; }
        public bool NeedsReview { get; }

        public HistoricalCompanyName NameAt(DateTime date)
        {
            return NameHistory
                .Where(item => item.IsActiveAt(date))
                .OrderByDescending(item => item.FromDate)
                .FirstOrDefault();
        }

        public string DisplayNameAt(DateTime date)
        {
            return NameAt(date)?.DisplayNameKo ?? CompanyId;
        }
    }

    public sealed class MarketSecurityDefinition
    {
        public MarketSecurityDefinition(
            string companyId,
            string displayNameKo,
            string exchange,
            string ticker,
            DateTime listingDate,
            DateTime? delistingDate,
            bool needsReview)
        {
            CompanyId = companyId;
            DisplayNameKo = displayNameKo;
            Exchange = exchange;
            Ticker = ticker;
            ListingDate = listingDate.Date;
            DelistingDate = delistingDate?.Date;
            NeedsReview = needsReview;
        }

        public string CompanyId { get; }
        public string DisplayNameKo { get; }
        public string Exchange { get; }
        public string Ticker { get; }
        public DateTime ListingDate { get; }
        public DateTime? DelistingDate { get; }
        public bool NeedsReview { get; }
        public string PriceRuleMarket =>
            string.Equals(Exchange, "KOSDAQ", StringComparison.Ordinal)
                ? MarketPricingRules.GrowthMarketName
                : "미래시장";
    }

    /// <summary>
    /// Runtime projection of Claude Korea History V1. It is immutable baseline
    /// data; a save stores only diverged world state keyed by CompanyId.
    /// </summary>
    public sealed class HistoricalCompanyRegistry
    {
        private readonly IReadOnlyDictionary<string, HistoricalCompanyDefinition> _byId;

        public HistoricalCompanyRegistry(
            int schemaVersion,
            IEnumerable<HistoricalCompanyDefinition> companies)
        {
            SchemaVersion = schemaVersion;
            var ordered = (companies ?? throw new ArgumentNullException(nameof(companies)))
                .OrderBy(item => item.CompanyId, StringComparer.Ordinal)
                .ToArray();
            var byId = new Dictionary<string, HistoricalCompanyDefinition>(StringComparer.Ordinal);
            foreach (var company in ordered)
            {
                if (byId.ContainsKey(company.CompanyId))
                    throw new InvalidOperationException($"Duplicate history company ID: {company.CompanyId}");
                byId.Add(company.CompanyId, company);
            }
            Companies = Array.AsReadOnly(ordered);
            _byId = new ReadOnlyDictionary<string, HistoricalCompanyDefinition>(byId);
        }

        public int SchemaVersion { get; }
        public IReadOnlyList<HistoricalCompanyDefinition> Companies { get; }

        public HistoricalCompanyDefinition Get(string companyId)
        {
            if (!_byId.TryGetValue(companyId, out var company))
                throw new KeyNotFoundException($"Unknown history company: {companyId}");
            return company;
        }

        public IReadOnlyList<MarketSecurityDefinition> ListedSecuritiesAt(DateTime date)
        {
            var securities = new List<MarketSecurityDefinition>();
            foreach (var company in Companies)
            {
                foreach (var listing in company.ListingHistory)
                {
                    if (!listing.IsDomesticExchange || !listing.IsListedAt(date)) continue;
                    securities.Add(new MarketSecurityDefinition(
                        company.CompanyId,
                        company.DisplayNameAt(date),
                        listing.Market,
                        listing.Ticker,
                        listing.FromDate,
                        listing.ToDate,
                        company.NeedsReview || listing.NeedsReview));
                }
            }
            return Array.AsReadOnly(securities
                .OrderBy(item => item.Exchange, StringComparer.Ordinal)
                .ThenBy(item => item.Ticker, StringComparer.Ordinal)
                .ThenBy(item => item.CompanyId, StringComparer.Ordinal)
                .ToArray());
        }

        public static DateTime ParseRequiredDate(string value, string fieldName)
        {
            if (DateTime.TryParseExact(
                    value,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var date))
                return date.Date;
            throw new FormatException($"Invalid {fieldName} date: {value}");
        }

        public static DateTime? ParseOptionalDate(string value, string fieldName)
        {
            return string.IsNullOrEmpty(value) ? (DateTime?)null : ParseRequiredDate(value, fieldName);
        }
    }
}
