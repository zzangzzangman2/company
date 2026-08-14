using UnityEngine;

namespace FamilyCompany.Presentation.Unity.Stamina
{
    /// <summary>Installs one roster-driven presenter without scene or per-character objects.</summary>
    internal static class OverheadStaminaBarRuntimeInstaller
    {
        private static OverheadStaminaBarPresenter _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForRuntime()
        {
            _instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallAfterSceneLoad()
        {
            if (_instance != null) return;
            _instance = UnityEngine.Object.FindFirstObjectByType<OverheadStaminaBarPresenter>(
                FindObjectsInactive.Include);
            if (_instance != null) return;

            var root = new GameObject("~OverheadStaminaUi");
            UnityEngine.Object.DontDestroyOnLoad(root);
            _instance = root.AddComponent<OverheadStaminaBarPresenter>();
        }
    }
}
