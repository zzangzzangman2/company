using System;
using System.Collections.Generic;
using FamilyCompany.Simulation.History;
using UnityEngine;

namespace FamilyCompany.Infrastructure.Unity
{
    /// <summary>어느 원본에서 등록부를 읽었는지 알린다. 진단과 화면 표시에만 쓴다.</summary>
    public enum HistoryCatalogSource
    {
        None = 0,
        EmbeddedAsset = 1,
        LiveContent = 2
    }

    /// <summary>
    /// Scene-owned reference that makes the Korea History V1 registry part of
    /// player builds while exposing only the immutable pure-C# projection.
    ///
    /// 개발 중에는 <see cref="LiveContentPath"/>의 외부 JSON을 먼저 읽고, 없으면 빌드에
    /// 내장된 TextAsset으로 되돌아간다. 릴리스 빌드에서는 항상 내장본만 쓴다.
    /// </summary>
    public sealed class KoreaHistoryV1RuntimeCatalog : MonoBehaviour
    {
        public const string RegistryRelativePath =
            "History/company_registry_korea_2000_2026.json";

        [SerializeField] private TextAsset _companyRegistryAsset;
        private HistoricalCompanyRegistry _registry;
        private HistoryCatalogSource _loadedSource = HistoryCatalogSource.None;

        public bool IsConfigured =>
            _companyRegistryAsset != null || LiveContentPath.Exists(RegistryRelativePath);

        /// <summary>마지막으로 성공한 읽기의 원본이다.</summary>
        public HistoryCatalogSource LoadedSource => _loadedSource;

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
            _loadedSource = HistoryCatalogSource.None;
        }

        public void InitializeNow()
        {
            if (_registry != null) return;
            _registry = LoadRegistry(out _loadedSource);
        }

        /// <summary>
        /// 외부 JSON을 다시 읽는다. 성공하면 true다.
        /// 파싱에 실패하면 기존 등록부를 그대로 두고 false를 돌려준다.
        /// 실행 중에 데이터가 사라지는 것보다 옛 데이터가 남는 편이 안전하다.
        /// </summary>
        public bool TryReloadFromDisk(out HistoryCatalogSource source, out string failureReason)
        {
            source = _loadedSource;
            failureReason = null;

            try
            {
                var reloaded = LoadRegistry(out var reloadedSource);
                _registry = reloaded;
                _loadedSource = reloadedSource;
                source = reloadedSource;
                return true;
            }
            catch (Exception exception)
            {
                failureReason = exception.Message;
                return false;
            }
        }

        public IReadOnlyList<MarketSecurityDefinition> ListedSecuritiesAt(DateTime date)
        {
            return Registry.ListedSecuritiesAt(date);
        }

        private HistoricalCompanyRegistry LoadRegistry(out HistoryCatalogSource source)
        {
            if (LiveContentPath.TryReadAllText(RegistryRelativePath, out var liveJson))
            {
                source = HistoryCatalogSource.LiveContent;
                return KoreaHistoryV1RegistryLoader.FromJson(liveJson);
            }

            if (_companyRegistryAsset == null)
            {
                throw new InvalidOperationException(
                    "Korea History V1 company registry asset is not configured and no live content was found.");
            }

            source = HistoryCatalogSource.EmbeddedAsset;
            return KoreaHistoryV1RegistryLoader.FromTextAsset(_companyRegistryAsset);
        }
    }
}
