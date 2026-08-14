using UnityEngine;

namespace FamilyCompany.Presentation.Unity.UIRemaster
{
    [CreateAssetMenu(menuName = "Family Company/UI Remaster Font Catalog", fileName = "UiRemasterFontCatalog_v3")]
    public sealed class UiRemasterFontCatalog : ScriptableObject
    {
        public const string RequiredContractVersion = "ui-remaster-fonts-v3";

        [SerializeField] private string contractVersion = RequiredContractVersion;
        [SerializeField] private Font bodySource = null;
        [SerializeField] private Font headingSource = null;
        [SerializeField] private Font fallbackSource = null;

        public string ContractVersion => contractVersion ?? string.Empty;
        public Font BodySource => bodySource;
        public Font HeadingSource => headingSource;
        public Font FallbackSource => fallbackSource;
        public bool IsComplete =>
            ContractVersion == RequiredContractVersion &&
            bodySource != null && headingSource != null && fallbackSource != null;
    }
}
