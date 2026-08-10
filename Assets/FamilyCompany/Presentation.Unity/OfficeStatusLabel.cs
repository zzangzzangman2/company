using UnityEngine;

namespace FamilyCompany.Presentation.Unity
{
    public sealed class OfficeStatusLabel : MonoBehaviour
    {
        private static readonly Color32 NeutralColor = new Color32(245, 245, 238, 255);
        private static readonly Color32 ContractColor = new Color32(100, 220, 255, 255);
        private static readonly Color32 WorkColor = new Color32(170, 220, 255, 255);
        private static readonly Color32 RestColor = new Color32(135, 240, 170, 255);
        private static readonly Color32 CoffeeColor = new Color32(255, 205, 90, 255);
        private static readonly Color32 SleepColor = new Color32(195, 175, 255, 255);
        private static readonly Color32 LimitColor = new Color32(255, 105, 115, 255);

        [SerializeField] private string displayName = "직원";
        [SerializeField] private OfficeWorkerAgent agent;
        [SerializeField] private TextMesh textMesh;
        private string _lastMeaningfulActivity = string.Empty;
        private string _lastRenderedText = string.Empty;
        private Color32 _lastRenderedColor;
        private bool _hasRenderedColor;

        public OfficeWorkerAgent Agent => agent;

        public void Configure(string newDisplayName, OfficeWorkerAgent newAgent, TextMesh newTextMesh)
        {
            displayName = newDisplayName ?? string.Empty;
            agent = newAgent;
            textMesh = newTextMesh;
            Refresh();
        }

        public void SetDisplayName(string newDisplayName)
        {
            displayName = NormalizeDisplayName(newDisplayName);
            Refresh();
        }

        private void LateUpdate()
        {
            Refresh();
        }

        private void Refresh()
        {
            if (agent == null || textMesh == null) return;

            var currentActivity = agent.CurrentActivityLabel ?? string.Empty;
            var displayActivity = ResolveDisplayActivity(currentActivity);
            var renderedText = $"{displayName}\n{displayActivity}";
            var renderedColor = ResolveColor(displayActivity);
            if (_lastRenderedText != renderedText)
            {
                textMesh.text = renderedText;
                _lastRenderedText = renderedText;
            }

            if (_hasRenderedColor && ColorsEqual(_lastRenderedColor, renderedColor)) return;
            textMesh.color = renderedColor;
            _lastRenderedColor = renderedColor;
            _hasRenderedColor = true;
        }

        private string ResolveDisplayActivity(string currentActivity)
        {
            if (!string.IsNullOrWhiteSpace(currentActivity) && currentActivity != "이동 중")
            {
                _lastMeaningfulActivity = currentActivity;
                return currentActivity;
            }

            return string.IsNullOrWhiteSpace(_lastMeaningfulActivity)
                ? "이동 중"
                : $"{_lastMeaningfulActivity} · 이동 중";
        }

        private static Color32 ResolveColor(string activity)
        {
            if (ContainsAny(activity, "한계", "완전 지침")) return LimitColor;
            if (activity.Contains("계약")) return ContractColor;
            if (activity.Contains("커피")) return CoffeeColor;
            if (ContainsAny(activity, "수면", "소파", "번아웃")) return SleepColor;
            if (ContainsAny(activity, "휴식", "수다", "회복", "퇴근")) return RestColor;
            if (ContainsAny(activity, "업무", "서류", "고객 응대", "출력", "회의", "영업")) return WorkColor;
            return NeutralColor;
        }

        private static bool ContainsAny(string value, params string[] tokens)
        {
            if (string.IsNullOrEmpty(value)) return false;
            foreach (var token in tokens)
            {
                if (value.Contains(token)) return true;
            }

            return false;
        }

        private static bool ColorsEqual(Color32 left, Color32 right)
        {
            return left.r == right.r && left.g == right.g && left.b == right.b && left.a == right.a;
        }

        private string NormalizeDisplayName(string value)
        {
            var normalized = value ?? string.Empty;
            if (GetComponentInParent<DirectionalSpriteAnimator>() != null)
            {
                normalized = normalized.Replace(" · 임시 에셋", string.Empty);
            }

            return normalized;
        }
    }
}
