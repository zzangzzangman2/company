using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public static class OfficePerformanceWindowsBuild
    {
        public const string OutputArgument = "-familyCompanyPerformanceBuildOutput";

        public static void BuildWindowsX64()
        {
            string outputPath = ReadRequiredArgument(OutputArgument);
            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene != null && scene.enabled)
                .Select(scene => scene.path)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .ToArray();
            if (scenes.Length == 0 || !string.Equals(
                    scenes[0],
                    WindowsPlayerBuild.ExpectedFirstScene,
                    StringComparison.Ordinal))
                throw new InvalidOperationException("Performance build scene order is invalid.");

            string fullOutputPath = Path.GetFullPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath) ??
                                      throw new InvalidOperationException("Performance output directory missing."));
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = fullOutputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException(
                    "Performance player build failed: " + report.summary.result);
            Debug.Log(
                "FAMILY_COMPANY_PERFORMANCE_BUILD: PASS | output=" + fullOutputPath +
                " duration=" + report.summary.totalTime +
                " bytes=" + report.summary.totalSize);
        }

        private static string ReadRequiredArgument(string argumentName)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index < arguments.Length - 1; index++)
                if (string.Equals(arguments[index], argumentName, StringComparison.Ordinal))
                    return arguments[index + 1];
            throw new InvalidOperationException("Missing command-line argument: " + argumentName);
        }
    }
}
