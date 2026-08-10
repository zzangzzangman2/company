using UnityEngine;

namespace FamilyCompany.Presentation.Unity.ManagementUI
{
    [CreateAssetMenu(menuName = "Family Company/Management UI Skin Catalog", fileName = "ManagementUiSkin_v1")]
    public sealed class ManagementUiSkinCatalog : ScriptableObject
    {
        [SerializeField] private string contractVersion = "management-ui-skin-v1";
        [SerializeField] private Sprite panel = null;
        [SerializeField] private Sprite card = null;
        [SerializeField] private Sprite button = null;
        [SerializeField] private Sprite buttonDisabled = null;
        [SerializeField] private Sprite tab = null;

        public string ContractVersion => contractVersion ?? string.Empty;
        public Sprite Panel => panel;
        public Sprite Card => card;
        public Sprite Button => button;
        public Sprite ButtonDisabled => buttonDisabled;
        public Sprite Tab => tab;
        public bool IsComplete =>
            ContractVersion == "management-ui-skin-v1" &&
            IsSliced(panel) && IsSliced(card) && IsSliced(button) &&
            IsSliced(buttonDisabled) && IsSliced(tab);

        private static bool IsSliced(Sprite sprite)
        {
            if (sprite == null) return false;
            var border = sprite.border;
            return border.x > 0f && border.y > 0f && border.z > 0f && border.w > 0f;
        }
    }
}
