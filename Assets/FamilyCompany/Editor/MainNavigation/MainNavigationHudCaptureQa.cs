using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public static class MainNavigationHudCaptureQa
    {
        public const string ArtifactFolder = "Artifacts/MainNavigationHudQa";
        public const string PlayerPath = ArtifactFolder + "/Player/FamilyCompanyMainNavigationHudQa.exe";

        [MenuItem("Family Company/QA/Build Main Navigation HUD D3D11 Player")]
        public static void BuildD3D11QaPlayer()
        {
            MainNavigationHudValidation.RunFromCommandLineForCapture();
            var playerPath = Path.GetFullPath(PlayerPath);
            Directory.CreateDirectory(Path.GetDirectoryName(playerPath) ?? ArtifactFolder);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[]
                {
                    PrototypeProjectBuilder.ScenePath,
                    "Assets/FamilyCompany/Scenes/OfficeTileMigrationPreview.unity"
                },
                locationPathName = playerPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Main navigation HUD QA player build failed: {report.summary.result}, " +
                    $"errors={report.summary.totalErrors}, warnings={report.summary.totalWarnings}");
            }

            Debug.Log(
                $"MAIN_NAVIGATION_D3D11_PLAYER_BUILD: PASS | {playerPath} | bytes={report.summary.totalSize} | " +
                $"warnings={report.summary.totalWarnings}");
        }
    }
}
