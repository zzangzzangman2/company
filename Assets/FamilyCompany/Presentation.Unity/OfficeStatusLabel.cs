using UnityEngine;

namespace FamilyCompany.Presentation.Unity
{
    public sealed class OfficeStatusLabel : MonoBehaviour
    {
        [SerializeField] private string displayName = "직원";
        [SerializeField] private OfficeWorkerAgent agent;
        [SerializeField] private TextMesh textMesh;

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
            displayName = newDisplayName ?? string.Empty;
            Refresh();
        }

        private void LateUpdate()
        {
            Refresh();
        }

        private void Refresh()
        {
            if (agent != null && textMesh != null)
            {
                textMesh.text = $"{displayName}\n{agent.CurrentActivityLabel}";
            }
        }
    }
}
