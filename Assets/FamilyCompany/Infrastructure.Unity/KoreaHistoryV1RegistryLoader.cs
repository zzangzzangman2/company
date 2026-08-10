using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using FamilyCompany.Simulation.History;
using UnityEngine;

namespace FamilyCompany.Infrastructure.Unity
{
#pragma warning disable 0649 // JsonUtility populates these serialized DTO fields.
    [Serializable]
    internal sealed class KoreaCompanyRegistryDto
    {
        public int schemaVersion;
        public KoreaCompanyDto[] companies;
    }

    [Serializable]
    internal sealed class KoreaCompanyDto
    {
        public string companyId;
        public string countryCode;
        public string[] industryIds;
        public string companySizeTier;
        public string detailLevel;
        public string playerReachIn2000;
        public KoreaCompanyNameDto[] nameHistory;
        public ListingRecordDto[] listingHistory;
        public bool needsReview;
    }

    [Serializable]
    internal sealed class KoreaCompanyNameDto
    {
        public string legalNameKo;
        public string legalNameEn;
        public string displayNameKo;
        public string fromDate;
        public string toDate;
        public bool needsReview;
    }

    [Serializable]
    internal sealed class ListingRecordDto
    {
        public string market;
        public string ticker;
        public string status;
        public string fromDate;
        public string toDate;
        public bool needsReview;
    }
#pragma warning restore 0649

    public static class KoreaHistoryV1RegistryLoader
    {
        public static HistoricalCompanyRegistry FromTextAsset(TextAsset registryAsset)
        {
            if (registryAsset == null) throw new ArgumentNullException(nameof(registryAsset));
            return FromJson(registryAsset.text);
        }

        public static HistoricalCompanyRegistry FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("Korea History V1 registry JSON is required.", nameof(json));
            var dto = JsonUtility.FromJson<KoreaCompanyRegistryDto>(json);
            if (dto == null || dto.companies == null)
                throw new InvalidOperationException("Korea History V1 registry has no companies array.");

            var companies = new List<HistoricalCompanyDefinition>(dto.companies.Length);
            foreach (var company in dto.companies)
            {
                if (company == null) continue;
                var names = new List<HistoricalCompanyName>();
                foreach (var name in company.nameHistory ?? Array.Empty<KoreaCompanyNameDto>())
                {
                    names.Add(new HistoricalCompanyName(
                        name.legalNameKo,
                        name.legalNameEn,
                        name.displayNameKo,
                        HistoricalCompanyRegistry.ParseRequiredDate(name.fromDate, "nameHistory.fromDate"),
                        HistoricalCompanyRegistry.ParseOptionalDate(name.toDate, "nameHistory.toDate"),
                        name.needsReview));
                }
                var listings = new List<HistoricalListingRecord>();
                foreach (var listing in company.listingHistory ?? Array.Empty<ListingRecordDto>())
                {
                    listings.Add(new HistoricalListingRecord(
                        listing.market,
                        listing.ticker,
                        listing.status,
                        HistoricalCompanyRegistry.ParseRequiredDate(listing.fromDate, "listingHistory.fromDate"),
                        HistoricalCompanyRegistry.ParseOptionalDate(listing.toDate, "listingHistory.toDate"),
                        listing.needsReview));
                }
                companies.Add(new HistoricalCompanyDefinition(
                    company.companyId,
                    company.countryCode,
                    company.companySizeTier,
                    company.detailLevel,
                    company.playerReachIn2000,
                    new ReadOnlyCollection<string>(company.industryIds ?? Array.Empty<string>()),
                    new ReadOnlyCollection<HistoricalCompanyName>(names),
                    new ReadOnlyCollection<HistoricalListingRecord>(listings),
                    company.needsReview));
            }
            return new HistoricalCompanyRegistry(dto.schemaVersion, companies);
        }
    }
}
