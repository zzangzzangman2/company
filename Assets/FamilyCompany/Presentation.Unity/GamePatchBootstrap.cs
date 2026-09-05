using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace FamilyCompany.Presentation.Unity
{
    // Transient boot UI only. The updater owns immutable snapshots; no gameplay/save state lives here.
    public sealed class GamePatchBootstrap : MonoBehaviour
    {
        [Serializable] private sealed class ProgressMessage
        {
            public string phase, detail;
            public long done, total;
            public double percent;
        }
        [Serializable] private sealed class PatchResult { public string status, directory, manifestHash; }
        private static GamePatchBootstrap _instance;
        public static bool IsBlocking => _instance != null && _instance._blocking;
        private readonly ConcurrentQueue<string> _messages = new ConcurrentQueue<string>();
        private Process _worker;
        private string _gameDirectory, _workerDirectory, _installRoot, _runRoot, _qaRoot;
        private string _status = "최신 버전을 확인하고 있습니다", _detail = "", _phase = "check";
        private double _percent = -1;
        private bool _blocking = true, _failed, _offlineAvailable, _restarting, _captured, _previousBackground;
        private float _started;
        private GUIStyle _button;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => _instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (Application.isEditor || Application.platform != RuntimePlatform.WindowsPlayer) return;
            string game = Directory.GetParent(Application.dataPath)?.FullName;
            string workers = Path.Combine(game ?? "", "FamilyCompanyPatch");
            bool qaPlayer = Debug.isDebugBuild || Path.GetFileName(Application.dataPath) == "FamilyCompany_FastQa_Data";
            string qa = qaPlayer ? ReadArgument("-familyCompanyInGamePatchQa") : null;
            if (string.IsNullOrEmpty(qa) && !File.Exists(Path.Combine(workers, "FamilyCompany.InGame.ps1"))) return;
            var host = new GameObject("~InGamePatchLoading");
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<GamePatchBootstrap>();
            _instance._gameDirectory = game;
            _instance._qaRoot = qa;
            _instance._workerDirectory = string.IsNullOrEmpty(qa) ? workers : Path.GetFullPath(qa);
            _instance._installRoot = string.IsNullOrEmpty(qa)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FamilyCompany", "PatchedGame")
                : Path.Combine(qa, "install");
        }

        private void Start()
        {
            Debug.Log("IN_GAME_PATCH_START actualUnityUi=true qa=" + !string.IsNullOrEmpty(_qaRoot));
            _previousBackground = Application.runInBackground;
            Application.runInBackground = true;
            StartAttempt(false);
        }

        private static string ReadArgument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }

        private static string Quote(string value)
        {
            if (string.IsNullOrEmpty(value) || value.IndexOfAny(new[] {'"', '\r', '\n'}) >= 0)
                throw new InvalidOperationException("Invalid patch path.");
            return "\"" + value.TrimEnd('\\') + "\"";
        }

        private Process StartHidden(string script, string arguments, bool capture)
        {
            var info = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                "WindowsPowerShell", "v1.0", "powershell.exe"),
                "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File " + Quote(script) + " " + arguments)
            {
                UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = capture, RedirectStandardError = capture
            };
            if (capture) { info.StandardOutputEncoding = Encoding.UTF8; info.StandardErrorEncoding = Encoding.UTF8; }
            var process = new Process { StartInfo = info };
            if (capture)
            {
                process.OutputDataReceived += (_, e) => { if (e.Data != null) _messages.Enqueue(e.Data); };
                process.ErrorDataReceived += (_, e) => { if (e.Data != null) _messages.Enqueue("WORKER " + e.Data); };
            }
            process.Start();
            if (capture) { process.BeginOutputReadLine(); process.BeginErrorReadLine(); }
            return process;
        }

        private void StartAttempt(bool offline)
        {
            if (_worker != null && !_worker.HasExited) return;
            _worker?.Dispose(); _worker = null;
            _failed = false; _offlineAvailable = false; _percent = -1; _phase = "check";
            _status = "최신 버전을 확인하고 있습니다"; _detail = "";
            _started = Time.realtimeSinceStartup;
            try
            {
                string parent = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FamilyCompany", "InGamePatchRuns");
                _runRoot = Path.Combine(parent, Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(_runRoot);
                _worker = StartHidden(Path.Combine(_workerDirectory, "FamilyCompany.InGame.ps1"),
                    "-GameDirectory " + Quote(_gameDirectory) + " -ResultPath " + Quote(Path.Combine(_runRoot, "result.json")) +
                    " -InstallRoot " + Quote(_installRoot) + " -CancelPath " + Quote(Path.Combine(_runRoot, "cancel.request")) +
                    (offline ? " -OfflineOnly" : ""), true);
            }
            catch (Exception e) { Fail(e.Message); }
        }

        private void Update()
        {
            while (_messages.TryDequeue(out string line))
            {
                File.AppendAllText(Path.Combine(_runRoot, "worker.txt"), line + "\n");
                if (!line.StartsWith("FC_PROGRESS ", StringComparison.Ordinal)) continue;
                var message = JsonUtility.FromJson<ProgressMessage>(line.Substring(12));
                _phase = message.phase; _percent = message.percent;
                switch (_phase)
                {
                    case "download": _status = message.total == 0 ? "다운로드할 파일이 없습니다" : "패치를 다운로드하고 있습니다"; break;
                    case "verify": _status = "게임 파일의 무결성을 확인하고 있습니다"; break;
                    case "check-files": _status = "변경된 파일을 확인하고 있습니다"; break;
                    case "reuse": _status = "변경 없는 파일을 재사용하고 있습니다"; break;
                    case "expand": _status = "다운로드한 파일을 풀고 있습니다"; break;
                    case "activate": _status = "새 버전 준비를 마무리하고 있습니다"; break;
                    case "ready": _status = "게임을 시작할 준비가 되었습니다"; break;
                    case "error": Fail("패치 서버에 연결하거나 파일을 검증하지 못했습니다."); break;
                    default: _status = "최신 버전을 확인하고 있습니다"; break;
                }
                _detail = message.total > 0 && (_phase == "download" || _phase == "verify")
                    ? $"{message.done / 1048576.0:0.00} / {message.total / 1048576.0:0.00} MiB\n{message.detail}"
                    : (_phase == "error" ? "다시 확인하거나 게임을 종료해 주세요." : message.detail ?? "");
                Debug.Log($"IN_GAME_PATCH_PROGRESS phase={_phase} done={message.done} total={message.total} percent={_percent:0.0}");
            }
            if (!string.IsNullOrEmpty(_qaRoot) && !_captured && _phase == "download" && _percent >= 20)
            {
                _captured = true;
                ScreenCapture.CaptureScreenshot(Path.Combine(_qaRoot, "in-game-patch.png"));
            }
            if (!string.IsNullOrEmpty(_qaRoot) && Time.realtimeSinceStartup - _started > 120) { Fail("QA timeout"); Application.Quit(1); }
            if (_worker == null || !_worker.HasExited || _restarting) return;
            _worker.WaitForExit();
            if (!_messages.IsEmpty) return;
            int exitCode = _worker.ExitCode; _worker.Dispose(); _worker = null;
            if (exitCode != 0) { Fail("패치를 완료하지 못했습니다."); return; }
            try
            {
                var result = JsonUtility.FromJson<PatchResult>(File.ReadAllText(Path.Combine(_runRoot, "result.json")));
                if (!string.IsNullOrEmpty(_qaRoot))
                {
                    File.WriteAllText(Path.Combine(_qaRoot, "unity-patch-result.json"), JsonUtility.ToJson(result, true));
                    StartCoroutine(FinishQa()); return;
                }
                if (result.status == "offline-ready")
                { _failed = true; _offlineAvailable = true; _status = "최신 버전을 확인하지 못했습니다"; _detail = "무결성을 확인한 이전 설치본으로 시작할 수 있습니다."; return; }
                if (string.Equals(Path.GetFullPath(result.directory), Path.GetFullPath(_gameDirectory), StringComparison.OrdinalIgnoreCase))
                { _blocking = false; Application.runInBackground = _previousBackground; return; }
                StartCoroutine(RestartWhenReady(result));
            }
            catch (Exception e) { Fail(e.Message); }
        }

        private IEnumerator FinishQa()
        {
            _restarting = true;
            yield return null; yield return null;
            Application.Quit(_captured ? 0 : 1);
        }

        private IEnumerator RestartWhenReady(PatchResult result)
        {
            _restarting = true; _percent = -1; _status = "패치 적용을 위해 게임을 다시 시작합니다";
            string ready = Path.Combine(_runRoot, "restart-ready.json");
            using (var helper = TryStartRestart(result, ready))
            {
                if (helper == null) { _restarting = false; yield break; }
                float deadline = Time.realtimeSinceStartup + 120;
                while (!File.Exists(ready) && !helper.HasExited && Time.realtimeSinceStartup < deadline) yield return null;
                if (File.Exists(ready) && !helper.HasExited) Application.Quit();
                else { _restarting = false; Fail("자동 재시작을 준비하지 못했습니다. 현재 게임은 유지됩니다."); }
            }
        }

        private Process TryStartRestart(PatchResult result, string ready)
        {
            try
            {
                using var self = Process.GetCurrentProcess();
                return StartHidden(Path.Combine(_workerDirectory, "FamilyCompany.Restart.ps1"),
                "-ParentId " + self.Id + " -ParentStartTicks " + self.StartTime.ToUniversalTime().Ticks +
                " -GameDirectory " + Quote(_gameDirectory) + " -InstallRoot " + Quote(_installRoot) +
                " -PendingDirectory " + Quote(result.directory) + " -ExpectedManifestHash " + Quote(result.manifestHash) +
                " -ReadyPath " + Quote(ready), false);
            }
            catch (Exception error) { Fail("자동 재시작 준비 실패: " + error.Message); return null; }
        }

        private void Fail(string reason) { _failed = true; _status = reason; _percent = -1; Debug.LogWarning("IN_GAME_PATCH_UNAVAILABLE " + reason); }

        private void OnGUI()
        {
            if (!_blocking) return;
            int oldDepth = GUI.depth; GUI.depth = -10000;
            ScenePreviewJump.DrawPatchLoading(_status, _detail, _percent);
            if (_failed && !_restarting)
            {
                if (_button == null)
                {
                    UIRemaster.UiRemasterTypography.TryLoadFonts(out var bodyFont, out _, out _, out _);
                    _button = new GUIStyle(GUI.skin.button) { fontSize = 18, font = bodyFont };
                }
                var panel = UIRemaster.UiRemasterLayout.CalculateLoading(Screen.width, Screen.height).Panel;
                if (GUI.Button(new Rect(panel.x, panel.yMax + 12, 150, 42), "다시 확인", _button)) StartAttempt(false);
                if (_offlineAvailable && GUI.Button(new Rect(panel.x + 160, panel.yMax + 12, 190, 42), "이전 버전으로 시작", _button)) StartAttempt(true);
                if (GUI.Button(new Rect(panel.xMax - 100, panel.yMax + 12, 100, 42), "종료", _button)) Application.Quit();
            }
            GUI.depth = oldDepth;
        }

        private void OnApplicationQuit()
        {
            if (_worker != null && !_worker.HasExited && _runRoot != null)
                File.WriteAllText(Path.Combine(_runRoot, "cancel.request"), "cancel");
        }
    }
}
