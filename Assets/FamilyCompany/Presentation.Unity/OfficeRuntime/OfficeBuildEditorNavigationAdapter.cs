using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime
{
    /// <summary>
    /// Stable integration seam for the separate company-hub/HUD branch. Production UI should
    /// expose one "건축·편집" card under the existing 회사 hub and call TryOpen with EntryId.
    /// No sixth bottom tab is required or supported here.
    /// </summary>
    public static class OfficeBuildEditorNavigationAdapter
    {
        public const string EntryId = OfficeLayoutEditModeController.NavigationEntryId;

        public static bool TryOpen(string navigationId, out string failure)
        {
            OfficeLayoutEditModeController controller =
                Object.FindFirstObjectByType<OfficeLayoutEditModeController>();
            if (controller == null)
            {
                failure = "사무실 건축·편집 controller를 찾을 수 없습니다.";
                return false;
            }
            return controller.OpenFromNavigation(navigationId, out failure);
        }
    }
}
