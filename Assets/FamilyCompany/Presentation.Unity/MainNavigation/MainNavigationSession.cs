namespace FamilyCompany.Presentation.Unity.MainNavigation
{
    public sealed class MainNavigationSession
    {
        private MainNavigationTabId _activeTab;

        public bool HasActiveTab { get; private set; }
        public MainNavigationTabId ActiveTab => _activeTab;

        public void Open(MainNavigationTabId tabId)
        {
            MainNavigationCatalog.Get(tabId);
            _activeTab = tabId;
            HasActiveTab = true;
        }

        public bool CloseToOffice()
        {
            if (!HasActiveTab) return false;
            HasActiveTab = false;
            return true;
        }

        public bool HandleEscape()
        {
            return CloseToOffice();
        }
    }
}
