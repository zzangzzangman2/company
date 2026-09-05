using System;
using System.IO;
using System.Linq;
using FamilyCompany.Editor.OfficeGridQa;
using FamilyCompany.Editor.OfficeLayout;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FamilyCompany.Editor
{
    /// <summary>
    /// Batch-mode entry point for the ordinary, non-Development Windows x64 player.
    /// The PowerShell wrapper owns staging, promotion, logging and BUILD_INFO.txt.
    /// </summary>
    public static class WindowsPlayerBuild
    {
        public const string ExecuteMethod = "FamilyCompany.Editor.WindowsPlayerBuild.BuildWindowsX64";
        public const string OutputArgument = "-familyCompanyBuildOutput";
        public const string ExpectedFirstScene = "Assets/FamilyCompany/Scenes/Prototype01.unity";

        public static void BuildWindowsX64()
        {
            var outputPath = ReadRequiredArgument(OutputArgument);
            BuildWindowsX64(outputPath);
        }

        internal static void BuildWindowsX64(string requestedOutputPath)
        {
            if (string.IsNullOrWhiteSpace(requestedOutputPath))
                throw new ArgumentException("A Windows player output path is required.", nameof(requestedOutputPath));

            var outputPath = Path.GetFullPath(requestedOutputPath.Trim());
            if (!string.Equals(Path.GetExtension(outputPath), ".exe", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The Windows player output must end in .exe: " + outputPath);

            var outputDirectory = Path.GetDirectoryName(outputPath);
            if (string.IsNullOrEmpty(outputDirectory))
                throw new InvalidOperationException("The Windows player output directory is missing: " + outputPath);
            Directory.CreateDirectory(outputDirectory);

            OfficeFurnitureAssetBuilder.UpgradePoseCatalog();
            OfficeRuntimeCharacterArtCatalogBuilder.Build();
            OfficeGridValidation.Run();
            OfficeAttendanceValidation.Run();
            OfficeFurnitureTileSnapValidation.Run();
            OfficeLocomotionTransitionQa.Run();
            OfficeLayoutValidator.Run();
            OfficeCharacterDirectionQa.ValidateApprovedDirections();

            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene != null && scene.enabled)
                .Select(scene => scene.path)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .ToArray();
            ValidateScenes(scenes);

            var options = BuildOptions.None;
            if ((options & BuildOptions.Development) != 0)
                throw new InvalidOperationException("The playtest build must not use BuildOptions.Development.");

            Debug.Log(
                "FAMILY_COMPANY_WINDOWS_BUILD: START " +
                $"target=StandaloneWindows64 output={outputPath} scenes={string.Join(",", scenes)}");

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                options = options
            });

            var summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    "Windows x64 player build failed. " +
                    $"result={summary.result}, errors={summary.totalErrors}, warnings={summary.totalWarnings}");
            }

            if (!File.Exists(outputPath))
                throw new FileNotFoundException("Unity reported success but the player executable is missing.", outputPath);

            // The real Unity game draws patch loading. Only invisible workers are bundled beside it.
            var patchDirectory = Path.Combine(outputDirectory, "FamilyCompanyPatch");
            Directory.CreateDirectory(patchDirectory);
            foreach (var file in new[] { "FamilyCompany.Update.ps1", "FamilyCompany.InGame.ps1", "FamilyCompany.Restart.ps1" })
                File.Copy(Path.Combine(Directory.GetCurrentDirectory(), "Tools", "Updater", file), Path.Combine(patchDirectory, file), false);

            Debug.Log(
                "FAMILY_COMPANY_WINDOWS_BUILD: PASS " +
                $"output={outputPath} bytes={summary.totalSize} warnings={summary.totalWarnings} " +
                $"duration={summary.totalTime}");
        }

        private static void ValidateScenes(string[] scenes)
        {
            if (scenes == null || scenes.Length == 0)
                throw new InvalidOperationException("EditorBuildSettings has no enabled scenes.");
            if (!string.Equals(scenes[0], ExpectedFirstScene, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"The first enabled scene must be {ExpectedFirstScene}; found {scenes[0]}.");
            }

            for (var index = 0; index < scenes.Length; index++)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenes[index]) == null)
                    throw new InvalidOperationException("Enabled build scene is missing: " + scenes[index]);
            }
        }

        private static string ReadRequiredArgument(string argumentName)
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index < arguments.Length - 1; index++)
            {
                if (string.Equals(arguments[index], argumentName, StringComparison.Ordinal))
                    return arguments[index + 1];
            }

            throw new InvalidOperationException("Missing command-line argument: " + argumentName);
        }
    }
}
