using FamilyCompany.Infrastructure.Unity;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity
{
    /// <summary>
    /// 개발용 데이터 핫리로드다. F5를 누르면 외부 콘텐츠 JSON을 다시 읽는다.
    ///
    /// 씬에 배치하지 않는다. <see cref="LiveContentPath.IsEnabled"/>가 true일 때만 스스로
    /// 붙으므로 PrototypeProjectBuilder의 씬 구성에 손댈 필요가 없다.
    ///
    /// 플레이테스트 빌드가 의도적으로 비-Development이므로 <c>DEVELOPMENT_BUILD</c>로
    /// 감싸지 않는다. 대신 외부 콘텐츠 폴더가 없으면 아무 것도 설치되지 않는다.
    /// </summary>
    public sealed class LiveContentReloader : MonoBehaviour
    {
        public const KeyCode DefaultReloadKey = KeyCode.F5;

        private KeyCode _reloadKey = DefaultReloadKey;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInstall()
        {
            if (!LiveContentPath.IsEnabled) return;
            if (FindFirstObjectByType<LiveContentReloader>() != null) return;

            var host = new GameObject("~LiveContentReloader");
            DontDestroyOnLoad(host);
            host.AddComponent<LiveContentReloader>();
        }

        private void Start()
        {
            Debug.Log($"[LiveContent] 외부 콘텐츠 사용 중: {LiveContentPath.Root} (F5로 다시 읽기)");

            // 어느 원본을 실제로 읽었는지 시작할 때 한 번 알린다.
            // 외부 JSON이 깨져 있으면 조용히 내장본으로 넘어가지 않고 여기서 드러난다.
            var catalog = FindFirstObjectByType<KoreaHistoryV1RuntimeCatalog>();
            if (catalog == null) return;

            try
            {
                catalog.InitializeNow();
                var origin = catalog.LoadedSource == HistoryCatalogSource.LiveContent
                    ? "외부 파일"
                    : "빌드 내장본";
                Debug.Log($"[LiveContent] 등록부 {catalog.Registry.Companies.Count}행 · {origin}");
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"[LiveContent] 등록부 초기 읽기 실패: {exception.Message}");
            }
        }

        private void Update()
        {
            if (!Input.GetKeyDown(_reloadKey)) return;
            ReloadNow();
        }

        /// <summary>외부 콘텐츠를 다시 읽고 결과 문구를 돌려준다.</summary>
        public string ReloadNow()
        {
            string message;

            if (!LiveContentPath.RootExists)
            {
                message = "외부 콘텐츠 폴더가 사라져 다시 읽지 않았다.";
            }
            else
            {
                var catalog = FindFirstObjectByType<KoreaHistoryV1RuntimeCatalog>();
                if (catalog == null)
                {
                    message = "이 씬에는 다시 읽을 콘텐츠 카탈로그가 없다.";
                }
                else if (catalog.TryReloadFromDisk(out var source, out var failureReason))
                {
                    var rows = catalog.Registry.Companies.Count;
                    message = source == HistoryCatalogSource.LiveContent
                        ? $"콘텐츠 다시 읽음 · 회사 {rows}행 · 외부 파일"
                        : $"콘텐츠 다시 읽음 · 회사 {rows}행 · 빌드 내장본";
                }
                else
                {
                    // 파싱에 실패하면 기존 데이터를 유지한다. 실행 중에 데이터가 비는 편이 더 나쁘다.
                    message = $"다시 읽기 실패, 이전 데이터를 유지한다: {failureReason}";
                }
            }

            Debug.Log($"[LiveContent] {message}");

            var bootstrap = FindFirstObjectByType<PrototypeBootstrap>();
            if (bootstrap != null) bootstrap.SetWorldNotice(message);

            return message;
        }
    }
}
