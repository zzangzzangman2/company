using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FamilyCompany.Editor
{
    [InitializeOnLoad]
    public static class PrototypeFrontendCapture
    {
        public const string OutputPath = "Artifacts/FrontendV04/frontend-main-menu-1920x1080.png";
        private const string PendingKey = "FamilyCompany.FrontendCapture.Pending";
        private const string FrameKey = "FamilyCompany.FrontendCapture.Frame";
        private const string BatchKey = "FamilyCompany.FrontendCapture.Batch";

        static PrototypeFrontendCapture()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        [MenuItem("Family Company/Capture Frontend V0.4")]
        public static void Capture()
        {
            var absolute = Path.GetFullPath(OutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute));
            if (File.Exists(absolute)) File.Delete(absolute);
            SessionState.SetBool(PendingKey, true);
            SessionState.SetInt(FrameKey, 0);
            SessionState.SetBool(BatchKey, Application.isBatchMode);
            EditorSceneManager.OpenScene(PrototypeProjectBuilder.ScenePath, OpenSceneMode.Single);
            if (!EditorApplication.isPlaying) EditorApplication.EnterPlaymode();
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(PendingKey, false) || !EditorApplication.isPlaying) return;
            var frame = SessionState.GetInt(FrameKey, 0) + 1;
            SessionState.SetInt(FrameKey, frame);
            if (frame == 10)
            {
                Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
            }

            if (frame == 45)
            {
                ScreenCapture.CaptureScreenshot(Path.GetFullPath(OutputPath));
            }

            if (frame >= 60 && File.Exists(Path.GetFullPath(OutputPath)))
            {
                SessionState.SetBool(PendingKey, false);
                Debug.Log($"FAMILY_COMPANY_FRONTEND_CAPTURE: PASS ({Path.GetFullPath(OutputPath)})");
                Finish(0);
                return;
            }

            if (frame < 300) return;
            SessionState.SetBool(PendingKey, false);
            Debug.LogError("FAMILY_COMPANY_FRONTEND_CAPTURE: FAIL (screenshot timeout)");
            Finish(1);
        }

        private static void Finish(int exitCode)
        {
            if (SessionState.GetBool(BatchKey, false)) EditorApplication.Exit(exitCode);
            else EditorApplication.ExitPlaymode();
        }
    }
}
