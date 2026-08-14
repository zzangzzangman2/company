using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using FamilyCompany.Simulation.Company;
using FamilyCompany.Simulation.History;

namespace FamilyCompany.Simulation.ContractGrowth
{
    public sealed class ContractClientTierCatalog
    {
        public const string LegacySamsungElectronicsId = "samsung-electronics";
        public const string LegacyLgElectronicsId = "lg-electronics";
        public const string LegacySkTelecomId = "sk-telecom";

        private static readonly IReadOnlyDictionary<string, string> LegacyAliases =
            new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [LegacySamsungElectronicsId] = "kr_samsung_electronics",
                [LegacyLgElectronicsId] = "kr_lg_electronics_1958",
                [LegacySkTelecomId] = "kr_sk_telecom"
            });

        private readonly ContractClientDefinition[] _clients;
        private readonly IReadOnlyDictionary<string, ContractClientDefinition> _byId;
        private readonly IReadOnlyDictionary<string, ContractClientDefinition> _historicalById;

        private ContractClientTierCatalog(
            IEnumerable<ContractClientDefinition> clients,
            IEnumerable<ContractClientDefinition> historicalClients)
        {
            _clients = (clients ?? throw new ArgumentNullException(nameof(clients)))
                .OrderBy(item => item.Tier)
                .ThenBy(item => item.ClientId, StringComparer.Ordinal)
                .ToArray();
            var dictionary = new Dictionary<string, ContractClientDefinition>(StringComparer.Ordinal);
            foreach (var client in _clients)
            {
                if (!dictionary.TryAdd(client.ClientId, client))
                    throw new InvalidOperationException($"Duplicate contract client ID: {client.ClientId}");
            }
            _byId = new ReadOnlyDictionary<string, ContractClientDefinition>(dictionary);
            _historicalById = new ReadOnlyDictionary<string, ContractClientDefinition>(
                (historicalClients ?? Array.Empty<ContractClientDefinition>())
                .ToDictionary(item => item.ClientId, item => item, StringComparer.Ordinal));
        }

        public IReadOnlyList<ContractClientDefinition> Clients => _clients;

        public static ContractClientTierCatalog Create(HistoricalCompanyRegistry registry, DateTime date)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            var clients = StarterLocalClients().ToList();
            var historicalClients = new List<ContractClientDefinition>();
            foreach (var company in registry.Companies.OrderBy(item => item.CompanyId, StringComparer.Ordinal))
            {
                var name = company.NameAt(date);
                var industries = ResolveBusinessIndustries(company.IndustryIds);
                var historicalName = name?.DisplayNameKo ?? company.NameHistory
                    .OrderBy(item => item.FromDate)
                    .Select(item => item.DisplayNameKo)
                    .FirstOrDefault() ?? company.CompanyId;
                var definition = new ContractClientDefinition(
                    company.CompanyId,
                    historicalName,
                    ResolveTier(company),
                    industries,
                    ResolveSpecialties(industries),
                    true,
                    company.CompanySizeTier,
                    company.PlayerReachIn2000,
                    NeutralIconFor(industries[0]),
                    string.Empty);
                historicalClients.Add(definition);
                if (name != null) clients.Add(definition);
            }
            return new ContractClientTierCatalog(clients, historicalClients);
        }

        public ContractClientDefinition Get(string clientId)
        {
            if (TryGet(clientId, out var client)) return client;
            throw new KeyNotFoundException($"Unknown contract client: {clientId}");
        }

        public bool TryGet(string clientId, out ContractClientDefinition client)
        {
            client = null;
            if (string.IsNullOrWhiteSpace(clientId)) return false;
            if (_byId.TryGetValue(clientId, out client)) return true;
            return LegacyAliases.TryGetValue(clientId, out var canonicalId) &&
                   _byId.TryGetValue(canonicalId, out client);
        }

        public ContractClientDefinition ResolveSavedClient(
            string clientId,
            string savedDisplayName,
            BusinessIndustry industry)
        {
            if (TryGet(clientId, out var client)) return client;
            if (_historicalById.TryGetValue(clientId, out client)) return client;
            if (LegacyAliases.TryGetValue(clientId, out var canonicalId) &&
                _historicalById.TryGetValue(canonicalId, out client)) return client;
            return new ContractClientDefinition(
                clientId,
                string.IsNullOrWhiteSpace(savedDisplayName) ? clientId : savedDisplayName,
                ContractClientTier.T0LocalBusiness,
                new[] { industry },
                ResolveSpecialties(new[] { industry }),
                false,
                "legacy_or_local",
                "legacy_offer",
                NeutralIconFor(industry));
        }

        public IReadOnlyList<ContractClientDefinition> ForTierAndIndustry(
            ContractClientTier tier,
            BusinessIndustry industry)
        {
            return _clients
                .Where(item => item.Tier == tier && item.Industries.Contains(industry))
                .OrderBy(item => item.ClientId, StringComparer.Ordinal)
                .ToArray();
        }

        private static IEnumerable<ContractClientDefinition> StarterLocalClients()
        {
            yield return new ContractClientDefinition(
                "local_sinchon_photo_studio",
                "신촌 사진관",
                ContractClientTier.T0LocalBusiness,
                new[] { BusinessIndustry.WebAndSoftware },
                new[] { ContractSpecialty.WebContent, ContractSpecialty.DataQualityAssurance },
                false,
                "sole_proprietor",
                "starter_local",
                "icon_local_shop");
            yield return new ContractClientDefinition(
                "local_jongno_typing_academy",
                "종로 타자학원",
                ContractClientTier.T0LocalBusiness,
                new[] { BusinessIndustry.WebAndSoftware, BusinessIndustry.HardwareAndPc },
                new[] { ContractSpecialty.DataQualityAssurance, ContractSpecialty.OfficeNetwork },
                false,
                "sole_proprietor",
                "starter_local",
                "icon_local_office");
            yield return new ContractClientDefinition(
                "local_mapo_video_rental",
                "마포 비디오·만화 대여점",
                ContractClientTier.T0LocalBusiness,
                new[] { BusinessIndustry.FashionRetailAndOffline },
                new[] { ContractSpecialty.RetailOperations, ContractSpecialty.BusinessSoftware },
                false,
                "sole_proprietor",
                "starter_local",
                "icon_local_retail");
            yield return new ContractClientDefinition(
                "local_yongsan_mobile_shop",
                "용산 휴대폰 수리점",
                ContractClientTier.T0LocalBusiness,
                new[] { BusinessIndustry.FeaturePhoneAndMobile, BusinessIndustry.HardwareAndPc },
                new[] { ContractSpecialty.MobileContent, ContractSpecialty.HardwareOperations },
                false,
                "sole_proprietor",
                "starter_local",
                "icon_local_mobile");
        }

        private static ContractClientTier ResolveTier(HistoricalCompanyDefinition company)
        {
            var reach = company.PlayerReachIn2000 ?? string.Empty;
            var size = company.CompanySizeTier ?? string.Empty;
            if (string.Equals(reach, "tier1_prime", StringComparison.Ordinal))
                return ContractClientTier.T3PrimeVendor;
            if (string.Equals(reach, "tier2_client", StringComparison.Ordinal))
            {
                if (string.Equals(size, "conglomerate", StringComparison.Ordinal) ||
                    string.Equals(size, "large", StringComparison.Ordinal))
                    return ContractClientTier.T4NationalEnterprise;
                if (string.Equals(size, "midsize", StringComparison.Ordinal))
                    return ContractClientTier.T3PrimeVendor;
                return ContractClientTier.T2GrowthCompany;
            }
            if (string.Equals(reach, "tier3_peer", StringComparison.Ordinal))
            {
                return string.Equals(size, "midsize", StringComparison.Ordinal)
                    ? ContractClientTier.T2GrowthCompany
                    : ContractClientTier.T1RegionalSmallBusiness;
            }
            if (string.Equals(size, "conglomerate", StringComparison.Ordinal) ||
                string.Equals(size, "large", StringComparison.Ordinal))
                return ContractClientTier.T4NationalEnterprise;
            if (string.Equals(size, "midsize", StringComparison.Ordinal))
                return ContractClientTier.T2GrowthCompany;
            return ContractClientTier.T1RegionalSmallBusiness;
        }

        private static BusinessIndustry[] ResolveBusinessIndustries(IReadOnlyList<string> industryIds)
        {
            var result = new HashSet<BusinessIndustry>();
            foreach (var industryId in industryIds ?? Array.Empty<string>())
            {
                var value = industryId ?? string.Empty;
                if (ContainsAny(value, "mobile", "handset")) result.Add(BusinessIndustry.FeaturePhoneAndMobile);
                if (ContainsAny(value, "semiconductor", "memory", "display", "electronics", "appliance", "digital_music"))
                    result.Add(BusinessIndustry.HardwareAndPc);
                if (ContainsAny(value, "ecommerce", "retail", "fashion")) result.Add(BusinessIndustry.FashionRetailAndOffline);
                if (ContainsAny(value, "it_services", "software", "internet", "portal", "search", "video_game", "advertising", "payments", "security", "isp"))
                    result.Add(BusinessIndustry.WebAndSoftware);
            }
            if (result.Count == 0) result.Add(BusinessIndustry.WebAndSoftware);
            return result.OrderBy(item => item).ToArray();
        }

        private static ContractSpecialty[] ResolveSpecialties(IEnumerable<BusinessIndustry> industries)
        {
            var result = new HashSet<ContractSpecialty>();
            foreach (var industry in industries)
            {
                switch (industry)
                {
                    case BusinessIndustry.WebAndSoftware:
                        result.Add(ContractSpecialty.WebContent);
                        result.Add(ContractSpecialty.BusinessSoftware);
                        result.Add(ContractSpecialty.DataQualityAssurance);
                        break;
                    case BusinessIndustry.FeaturePhoneAndMobile:
                        result.Add(ContractSpecialty.MobileContent);
                        result.Add(ContractSpecialty.Localization);
                        break;
                    case BusinessIndustry.HardwareAndPc:
                        result.Add(ContractSpecialty.HardwareOperations);
                        result.Add(ContractSpecialty.OfficeNetwork);
                        break;
                    case BusinessIndustry.FashionRetailAndOffline:
                        result.Add(ContractSpecialty.RetailOperations);
                        result.Add(ContractSpecialty.BusinessSoftware);
                        break;
                }
            }
            return result.OrderBy(item => item).ToArray();
        }

        private static string NeutralIconFor(BusinessIndustry industry)
        {
            return industry switch
            {
                BusinessIndustry.WebAndSoftware => "icon_industry_web",
                BusinessIndustry.FeaturePhoneAndMobile => "icon_industry_mobile",
                BusinessIndustry.HardwareAndPc => "icon_industry_hardware",
                BusinessIndustry.FashionRetailAndOffline => "icon_industry_retail",
                _ => "icon_industry_neutral"
            };
        }

        private static bool ContainsAny(string value, params string[] fragments)
        {
            return fragments.Any(fragment => value.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
}
