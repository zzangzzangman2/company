using System.Collections;
using FamilyCompany.Presentation.Unity.OfficeGridView;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FamilyCompany.Presentation.Unity
{
    /// <summary>
    /// Keeps Prototype01's simulation and IMGUI management layer alive while the Starter Office
    /// tile scene becomes the only rendered world. F9 is a one-way recovery shortcut; the removed
    /// OfficeVisualV2 presentation is never restored.
    /// </summary>
    public sealed class ScenePreviewJump : MonoBehaviour
    {
        public const KeyCode JumpKey = KeyCode.F9;
        public const string PreviewSceneName = "OfficeTileMigrationPreview";

        private static ScenePreviewJump _instance;
        private bool _loading;
        private bool _tileOfficeActive;
        private Renderer[] _legacyRenderers = System.Array.Empty<Renderer>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInstall()
        {
            if (_instance != null || FindPreviewBuildIndex() < 0) return;
            var host = new GameObject("~StarterOfficeTileRuntime");
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<ScenePreviewJump>();
        }

        public static void ShowStarterOffice()
        {
            if (_instance == null)
            {
                AutoInstall();
                if (_instance == null)
                {
                    Debug.LogError("[StarterOfficeTileRuntime] 타일 사무실 씬이 빌드에 없습니다.");
                    return;
                }
            }

            _instance.BeginShowStarterOffice();
        }

        private void Start()
        {
            Debug.Log("[StarterOfficeTileRuntime] 처음하기/불러오기 = Starter 타일 사무실 · F9 = 단방향 복구");
            if (System.Array.IndexOf(
                    System.Environment.GetCommandLineArgs(),
                    "-familyCompanyTileRuntimeQa") >= 0)
                StartCoroutine(RunPlayerQa());
        }

        private void Update()
        {
            if (Input.GetKeyDown(JumpKey)) BeginShowStarterOffice();
        }

        private void LateUpdate()
        {
            if (!_tileOfficeActive) return;
            foreach (var renderer in _legacyRenderers)
            {
                if (renderer != null && renderer.enabled) renderer.enabled = false;
            }
        }

        private void BeginShowStarterOffice()
        {
            if (_tileOfficeActive || _loading) return;
            StartCoroutine(LoadStarterOffice());
        }

        private IEnumerator LoadStarterOffice()
        {
            _loading = true;
            var previewScene = SceneManager.GetSceneByName(PreviewSceneName);
            if (!previewScene.isLoaded)
            {
                var operation = SceneManager.LoadSceneAsync(PreviewSceneName, LoadSceneMode.Additive);
                if (operation == null)
                {
                    _loading = false;
                    Debug.LogError("[StarterOfficeTileRuntime] 타일 사무실 씬 로드를 시작하지 못했습니다.");
                    yield break;
                }
                yield return operation;
                previewScene = SceneManager.GetSceneByName(PreviewSceneName);
            }

            if (!previewScene.IsValid() || !previewScene.isLoaded)
            {
                _loading = false;
                Debug.LogError("[StarterOfficeTileRuntime] 타일 사무실 씬 로드 후 검증에 실패했습니다.");
                yield break;
            }

            var bootstrap = FindBootstrap(previewScene);
            if (bootstrap == null)
            {
                _loading = false;
                Debug.LogError("[StarterOfficeTileRuntime] OfficeTileMigrationPreviewBootstrap이 없습니다.");
                yield break;
            }

            bootstrap.ConfigureLayoutForEditor(OfficeTilePreviewLayout.StarterOfficeV1);
            bootstrap.BuildPreview();

            Camera previewCamera = null;
            foreach (var root in previewScene.GetRootGameObjects())
            {
                var cameras = root.GetComponentsInChildren<Camera>(true);
                if (cameras.Length > 0 && previewCamera == null) previewCamera = cameras[0];
                foreach (var listener in root.GetComponentsInChildren<AudioListener>(true))
                    listener.enabled = false;
            }

            if (previewCamera == null)
            {
                _loading = false;
                Debug.LogError("[StarterOfficeTileRuntime] 타일 사무실 카메라가 없습니다.");
                yield break;
            }

            var legacyScene = SceneManager.GetSceneAt(0);
            _legacyRenderers = CollectRenderers(legacyScene);
            foreach (var renderer in _legacyRenderers)
                if (renderer != null) renderer.enabled = false;

            foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (camera == previewCamera) continue;
                camera.enabled = false;
                if (camera.CompareTag("MainCamera")) camera.tag = "Untagged";
            }

            previewCamera.tag = "MainCamera";
            previewCamera.enabled = true;
            _tileOfficeActive = true;
            _loading = false;
            Debug.Log(
                "[StarterOfficeTileRuntime] PASS · StarterOfficeV1 기본 표시 · " +
                $"legacyRenderers={_legacyRenderers.Length}");
        }

        private IEnumerator RunPlayerQa()
        {
            yield return null;
            var bootstrap = Object.FindFirstObjectByType<PrototypeBootstrap>();
            if (bootstrap == null)
            {
                Debug.LogError("FAMILY_COMPANY_STARTER_TILE_MAIN_FLOW: FAIL · PrototypeBootstrap missing");
                Application.Quit(31);
                yield break;
            }

            bootstrap.StartNewGameNow(1, false);
            for (var frame = 0; frame < 900 && !_tileOfficeActive; frame++) yield return null;
            if (!_tileOfficeActive)
            {
                Debug.LogError("FAMILY_COMPANY_STARTER_TILE_MAIN_FLOW: FAIL · tile office activation timeout");
                Application.Quit(32);
                yield break;
            }

            var previewScene = SceneManager.GetSceneByName(PreviewSceneName);
            var preview = FindBootstrap(previewScene);
            if (preview == null || preview.Layout != OfficeTilePreviewLayout.StarterOfficeV1 ||
                preview.Presenter == null || preview.FurniturePresenter == null || preview.Movers.Count != 4)
            {
                Debug.LogError("FAMILY_COMPANY_STARTER_TILE_MAIN_FLOW: FAIL · Starter runtime invariant");
                Application.Quit(33);
                yield break;
            }

            Debug.Log(
                "FAMILY_COMPANY_STARTER_TILE_MAIN_FLOW: PASS · " +
                $"layout={preview.Layout} furniture={preview.Presenter.SemanticGrid.Furniture.Count} " +
                $"characters={preview.Movers.Count} legacyRenderers={_legacyRenderers.Length}");
            yield return null;
            Application.Quit(0);
        }

        private static OfficeTileMigrationPreviewBootstrap FindBootstrap(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var bootstrap = root.GetComponentInChildren<OfficeTileMigrationPreviewBootstrap>(true);
                if (bootstrap != null) return bootstrap;
            }
            return null;
        }

        private static Renderer[] CollectRenderers(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return System.Array.Empty<Renderer>();
            var result = new System.Collections.Generic.List<Renderer>();
            foreach (var root in scene.GetRootGameObjects())
                result.AddRange(root.GetComponentsInChildren<Renderer>(true));
            return result.ToArray();
        }

        private static int FindPreviewBuildIndex()
        {
            for (var index = 0; index < SceneManager.sceneCountInBuildSettings; index++)
            {
                var path = SceneUtility.GetScenePathByBuildIndex(index);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == PreviewSceneName) return index;
            }
            return -1;
        }
    }
}
