using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public static class PrototypeFrontendCapture
    {
        public const string ArtifactFolder = "Artifacts/FrontendV04";
        public const string PlayerPath = ArtifactFolder + "/Player/FamilyCompanyFrontendQa.exe";
        public const string ScreenshotPath = ArtifactFolder + "/frontend-main-menu-1920x1080.png";

        [MenuItem("Family Company/Build Frontend V0.4 QA Player")]
        public static void BuildQaPlayer()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(PlayerPath)));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { PrototypeProjectBuilder.ScenePath },
                locationPathName = PlayerPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Frontend QA player build failed: {report.summary.result}, {report.summary.totalErrors} errors");
            }

            Debug.Log(
                $"FAMILY_COMPANY_FRONTEND_BUILD: PASS ({Path.GetFullPath(PlayerPath)}, {report.summary.totalSize} bytes)");
        }
    }
}
