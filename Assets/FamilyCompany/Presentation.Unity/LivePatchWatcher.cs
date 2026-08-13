using System;
using System.Diagnostics;
using System.IO;
using FamilyCompany.Infrastructure.Unity;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace FamilyCompany.Presentation.Unity
{
    /// <summary>
    /// 새 플레이테스트 빌드가 나오면 화면 구석에 알리고 Ctrl+R로 재시작하는 개발용 도구다.
    ///
    /// 빌드 스크립트를 고치지 않는다. 실행 중인 exe 파일의 수정 시각만 본다.
    /// Build-FamilyCompanyWindows.ps1은 승격 단계에서 출력 폴더를 통째로 갈아치우는데,
    /// 실행 중인 exe가 있어도 이 이동은 성공한다(같은 볼륨 rename). 그래서 같은 경로의
    /// 파일 시각이 갱신되는 것으로 새 빌드를 감지할 수 있다.
    ///
    /// 외부 콘텐츠 폴더가 연결된 개발 환경에서만 붙는다. 배포 패키지에서는 자동으로 꺼진다.
    /// </summary>
    public sealed class LivePatchWatcher : MonoBehaviour
    {
        public const float CheckIntervalSeconds = 5f;

        /// <summary>파일 시각과 프로세스 시작 시각의 시계 오차를 흡수한다.</summary>
        private static readonly TimeSpan Margin = TimeSpan.FromSeconds(5);

        private string _executablePath;
        private DateTime _processStartUtc;
        private float _nextCheck;
        private GUIStyle _bannerStyle;

        public bool NewBuildAvailable { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInstall()
        {
            if (Application.isEditor) return;              // 에디터에서는 의미가 없다.
            if (!LiveContentPath.IsEnabled) return;        // 개발 환경 opt-in과 같은 신호를 쓴다.
            if (FindFirstObjectByType<LivePatchWatcher>() != null) return;

            var host = new GameObject("~LivePatchWatcher");
            DontDestroyOnLoad(host);
            host.AddComponent<LivePatchWatcher>();
        }

        private void Start()
        {
            _executablePath = ResolveExecutablePath();
            _processStartUtc = ResolveProcessStartUtc();
            Debug.Log($"[LivePatch] 새 빌드 감시 시작 · {_executablePath}");
        }

        private void Update()
        {
            if (!NewBuildAvailable && Time.unscaledTime >= _nextCheck)
            {
                _nextCheck = Time.unscaledTime + CheckIntervalSeconds;
                if (HasNewerBuild())
                {
                    NewBuildAvailable = true;
                    Debug.Log("[LivePatch] 새 빌드가 준비되었다. Ctrl+R로 재시작한다.");
                }
            }

            if (!NewBuildAvailable) return;
            if (!Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl)) return;
            if (!Input.GetKeyDown(KeyCode.R)) return;

            RestartIntoNewBuild();
        }

        private void OnGUI()
        {
            if (!NewBuildAvailable) return;

            if (_bannerStyle == null)
            {
                _bannerStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 16,
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };
                _bannerStyle.normal.textColor = Color.white;
            }

            const float width = 300f;
            const float height = 34f;
            var rect = new Rect(Screen.width - width - 16f, 16f, width, height);

            var previous = GUI.color;
            GUI.color = new Color(0.10f, 0.45f, 0.20f, 0.92f);
            GUI.Box(rect, GUIContent.none);
            GUI.color = previous;
            GUI.Label(rect, "새 빌드 준비됨 · Ctrl+R 재시작", _bannerStyle);
        }

        private bool HasNewerBuild()
        {
            if (string.IsNullOrEmpty(_executablePath)) return false;

            try
            {
                if (!File.Exists(_executablePath)) return false;   // 승격 도중일 수 있다.
                var writtenUtc = File.GetLastWriteTimeUtc(_executablePath);
                return writtenUtc > _processStartUtc + Margin;
            }
            catch (IOException)
            {
                return false;      // 빌드가 쓰는 중이면 다음 주기에 다시 본다.
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private void RestartIntoNewBuild()
        {
            if (string.IsNullOrEmpty(_executablePath) || !File.Exists(_executablePath))
            {
                Debug.LogWarning("[LivePatch] 재시작할 실행 파일을 찾지 못했다.");
                return;
            }

            // 자기 자신을 바로 실행하면 파일이 잠겨 있다.
            // PowerShell에 잠깐 기다렸다 띄우게 맡기고 자기는 종료한다.
            var arguments =
                "-NoProfile -WindowStyle Hidden -Command " +
                $"\"Start-Sleep -Milliseconds 900; Start-Process -FilePath '{_executablePath}'\"";

            try
            {
                Process.Start(new ProcessStartInfo("powershell.exe", arguments)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
            }
            catch (Exception exception)
            {
                Debug.LogError($"[LivePatch] 재시작 실행에 실패했다: {exception.Message}");
                return;
            }

            Debug.Log("[LivePatch] 새 빌드로 재시작한다.");
            Application.Quit();
        }

        /// <summary>
        /// Application.productName 은 쓰지 않는다. 이 프로젝트는 productName 이
        /// FamilyCompanyPrototype 인데 실제 실행 파일은 FamilyCompany.exe 다.
        /// 데이터 폴더 이름이 항상 "&lt;exe 이름&gt;_Data" 라는 Unity 규칙에서 유도한다.
        /// </summary>
        private static string ResolveExecutablePath()
        {
            try
            {
                var dataDirectory = new DirectoryInfo(Application.dataPath);
                var buildRoot = dataDirectory.Parent;
                if (buildRoot == null) return null;

                const string dataSuffix = "_Data";
                if (dataDirectory.Name.EndsWith(dataSuffix, StringComparison.Ordinal))
                {
                    var stem = dataDirectory.Name.Substring(0, dataDirectory.Name.Length - dataSuffix.Length);
                    var candidate = Path.Combine(buildRoot.FullName, stem + ".exe");
                    if (File.Exists(candidate)) return candidate;
                }

                // 규칙이 깨졌으면 크래시 핸들러를 뺀 유일한 exe 를 찾는다.
                foreach (var file in buildRoot.GetFiles("*.exe"))
                {
                    if (file.Name.IndexOf("CrashHandler", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    return file.FullName;
                }

                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static DateTime ResolveProcessStartUtc()
        {
            try
            {
                return Process.GetCurrentProcess().StartTime.ToUniversalTime();
            }
            catch (Exception)
            {
                return DateTime.UtcNow;   // 못 읽으면 지금부터 센다.
            }
        }
    }
}
