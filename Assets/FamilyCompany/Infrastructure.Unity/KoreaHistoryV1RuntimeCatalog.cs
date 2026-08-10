using System;
using System.Collections.Generic;
using FamilyCompany.Simulation.History;
using UnityEngine;

namespace FamilyCompany.Infrastructure.Unity
{
    /// <summary>
    /// Scene-owned reference that makes the Korea History V1 registry part of
    /// player builds while exposing only the immutable pure-C# projection.
    /// </summary>
    public sealed class KoreaHistoryV1RuntimeCatalog : MonoBehaviour
    {
        [SerializeField] private TextAsset _companyRegistryAsset;
        private HistoricalCompanyRegistry _registry;

        public bool IsConfigured => _companyRegistryAsset != null;
        public HistoricalCompanyRegistry Registry
        {
            get
            {
                InitializeNow();
                return _registry;
            }
        }

        public void Configure(TextAsset companyRegistryAsset)
        {
            _companyRegistryAsset = companyRegistryAsset != null
                ? companyRegistryAsset
                : throw new ArgumentNullException(nameof(companyRegistryAsset));
            _registry = null;
        }

        public void InitializeNow()
        {
            if (_registry != null) return;
            if (_companyRegistryAsset == null)
                throw new InvalidOperationException("Korea History V1 company registry asset is not configured.");
            _registry = KoreaHistoryV1RegistryLoader.FromTextAsset(_companyRegistryAsset);
        }

        public IReadOnlyList<MarketSecurityDefinition> ListedSecuritiesAt(DateTime date)
        {
            return Registry.ListedSecuritiesAt(date);
        }
    }
}
