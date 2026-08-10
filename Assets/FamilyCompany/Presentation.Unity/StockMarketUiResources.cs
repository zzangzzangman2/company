using UnityEngine;

namespace FamilyCompany.Presentation.Unity
{
    /// <summary>
    /// Resource anchor that keeps the project-owned Korean font in standalone
    /// builds without duplicating the TTF or relying on an operating-system font.
    /// </summary>
    public sealed class StockMarketUiResources : ScriptableObject
    {
        [SerializeField] private Font primaryFont = null;
        [SerializeField] private Font boldFont = null;
        [SerializeField] private Font fallbackFont = null;

        public Font PrimaryFont => primaryFont;
        public Font BoldFont => boldFont;
        public Font FallbackFont => fallbackFont;
    }
}
