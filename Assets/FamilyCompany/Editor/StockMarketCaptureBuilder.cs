using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public static class StockMarketCaptureBuilder
    {
        public const string ArtifactFolder = "Artifacts/StockMarketLandscape";
        public const string PlayerPath = ArtifactFolder + "/Player/FamilyCompanyStockQa.exe";

        [MenuItem("Family Company/Build Stock Market Landscape QA Player")]
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
                throw new InvalidOperationException(
                    $"Stock QA player build failed: {report.summary.result}, {report.summary.totalErrors} errors");
            Debug.Log($"FAMILY_COMPANY_STOCK_BUILD: PASS ({Path.GetFullPath(PlayerPath)})");
        }
    }
}
