namespace FamilyCompany.Presentation.Unity.MainNavigation
{
    public sealed class MainNavigationSession
    {
        private MainNavigationTabId _activeTab;
        private string _activeFeatureId = string.Empty;

        public bool HasActiveTab { get; private set; }
        public MainNavigationTabId ActiveTab => _activeTab;
        public bool HasActiveFeature => HasActiveTab && !string.IsNullOrEmpty(_activeFeatureId);
        public string ActiveFeatureId => HasActiveFeature ? _activeFeatureId : string.Empty;

        public void Open(MainNavigationTabId tabId)
        {
            MainNavigationCatalog.Get(tabId);
            _activeTab = tabId;
            _activeFeatureId = string.Empty;
            HasActiveTab = true;
        }

        public void OpenFeature(string featureId)
        {
            if (!HasActiveTab) throw new System.InvalidOperationException("A tab must be open before a feature route.");
            var definition = MainNavigationCatalog.Get(_activeTab);
            var exists = false;
            for (var index = 0; index < definition.Features.Count; index++)
            {
                if (!string.Equals(definition.Features[index].Id, featureId, System.StringComparison.Ordinal)) continue;
                exists = true;
                break;
            }
            if (!exists) throw new System.ArgumentException("Feature does not belong to the active tab.", nameof(featureId));
            _activeFeatureId = featureId;
        }

        public bool BackToHub()
        {
            if (!HasActiveFeature) return false;
            _activeFeatureId = string.Empty;
            return true;
        }

        public bool CloseToOffice()
        {
            if (!HasActiveTab) return false;
            _activeFeatureId = string.Empty;
            HasActiveTab = false;
            return true;
        }

        public bool HandleEscape()
        {
            return BackToHub() || CloseToOffice();
        }
    }
}
