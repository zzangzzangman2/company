using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FamilyCompany.Presentation.Unity
{
    /// <summary>
    /// 빌드에 포함된 미리보기 씬으로 바로 건너뛰는 개발용 단축키다.
    ///
    /// F9를 누르면 타일 사무실 미리보기 씬과 기본 씬을 오간다.
    /// 씬 배치가 필요 없도록 스스로 붙으며, 미리보기 씬이 빌드에 없으면 아무 것도 하지 않는다.
    ///
    /// 릴리스 패키징에서는 미리보기 씬을 EditorBuildSettings에서 빼면 이 기능도 자동으로 꺼진다.
    /// </summary>
    public sealed class ScenePreviewJump : MonoBehaviour
    {
        public const KeyCode JumpKey = KeyCode.F9;
        public const string PreviewSceneName = "OfficeTileMigrationPreview";

        private static bool _installed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInstall()
        {
            if (_installed) return;
            if (FindPreviewBuildIndex() < 0) return;

            var host = new GameObject("~ScenePreviewJump");
            DontDestroyOnLoad(host);
            host.AddComponent<ScenePreviewJump>();
            _installed = true;
        }

        /// <summary>도메인 리로드를 끈 상태에서도 static 상태가 남지 않게 한다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _installed = false;
        }

        private void Start()
        {
            Debug.Log($"[ScenePreviewJump] F9 = 타일 사무실 미리보기 전환 (현재 씬 {SceneManager.GetActiveScene().name})");
        }

        private void Update()
        {
            if (!Input.GetKeyDown(JumpKey)) return;
            Toggle();
        }

        public void Toggle()
        {
            var previewIndex = FindPreviewBuildIndex();
            if (previewIndex < 0)
            {
                Debug.LogWarning("[ScenePreviewJump] 미리보기 씬이 빌드에 없다.");
                return;
            }

            var goingToPreview = SceneManager.GetActiveScene().buildIndex != previewIndex;
            var targetIndex = goingToPreview ? previewIndex : 0;

            Debug.Log($"[ScenePreviewJump] 씬 전환 -> buildIndex {targetIndex}");
            Time.timeScale = 1f;   // 일시정지 메뉴에서 눌렀을 수 있다.
            SceneManager.LoadScene(targetIndex, LoadSceneMode.Single);
        }

        private static int FindPreviewBuildIndex()
        {
            var count = SceneManager.sceneCountInBuildSettings;
            for (var index = 0; index < count; index++)
            {
                var path = SceneUtility.GetScenePathByBuildIndex(index);
                if (string.IsNullOrEmpty(path)) continue;
                if (Path.GetFileNameWithoutExtension(path) == PreviewSceneName) return index;
            }

            return -1;
        }
    }
}
