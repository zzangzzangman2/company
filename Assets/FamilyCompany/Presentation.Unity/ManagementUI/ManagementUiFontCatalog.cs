using UnityEngine;

namespace FamilyCompany.Presentation.Unity.ManagementUI
{
    [CreateAssetMenu(menuName = "Family Company/Management UI Font Catalog", fileName = "ManagementUiFontCatalog_v1")]
    public sealed class ManagementUiFontCatalog : ScriptableObject
    {
        [SerializeField] private string contractVersion = "management-ui-fonts-v1";
        [SerializeField] private Font bodySource = null;
        [SerializeField] private Font headingSource = null;
        [SerializeField] private Font fallbackSource = null;

        public string ContractVersion => contractVersion ?? string.Empty;
        public Font BodySource => bodySource;
        public Font HeadingSource => headingSource;
        public Font FallbackSource => fallbackSource;
        public bool IsComplete =>
            ContractVersion == "management-ui-fonts-v1" &&
            bodySource != null && headingSource != null && fallbackSource != null;
    }
}
