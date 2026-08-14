using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FamilyCompany.Editor
{
    /// <summary>
    /// Isolated render-clarity QA build. It deliberately does not invoke art, seating, wall, or
    /// layout builders, so parallel feature assets remain byte-for-byte untouched.
    /// </summary>
    public static class RenderClarityWindowsBuild
    {
        private const string OutputArgument = "-familyCompanyRenderQaBuildOutput";
        private const string ExpectedFirstScene = "Assets/FamilyCompany/Scenes/Prototype01.unity";

        public static void BuildWindowsX64()
        {
            string outputPath = Path.GetFullPath(ReadRequiredArgument(OutputArgument));
            if (!string.Equals(Path.GetExtension(outputPath), ".exe", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Render QA build output must end in .exe: " + outputPath);
            string outputDirectory = Path.GetDirectoryName(outputPath);
            if (string.IsNullOrWhiteSpace(outputDirectory))
                throw new InvalidOperationException("Render QA build output directory is missing.");
            Directory.CreateDirectory(outputDirectory);

            RenderClarityValidation.Run();
            string[] scenes = EditorBuildSettings.scenes
                .Where(item => item != null && item.enabled)
                .Select(item => item.path)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .ToArray();
            if (scenes.Length == 0 || !string.Equals(scenes[0], ExpectedFirstScene, StringComparison.Ordinal))
                throw new InvalidOperationException("Render QA build does not start with Prototype01.");

            Debug.Log(
                "RENDER_CLARITY_WINDOWS_BUILD: START | " +
                $"target=StandaloneWindows64 output={outputPath} scenes={string.Join(",", scenes)}");
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            BuildSummary summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException(
                    $"Render QA Windows build failed: result={summary.result}, " +
                    $"errors={summary.totalErrors}, warnings={summary.totalWarnings}.");
            if (!File.Exists(outputPath))
                throw new FileNotFoundException("Render QA player executable is missing.", outputPath);
            Debug.Log(
                "RENDER_CLARITY_WINDOWS_BUILD: PASS | " +
                $"output={outputPath} bytes={summary.totalSize} warnings={summary.totalWarnings} " +
                $"duration={summary.totalTime}");
        }

        private static string ReadRequiredArgument(string name)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index < arguments.Length - 1; index++)
                if (string.Equals(arguments[index], name, StringComparison.Ordinal))
                    return arguments[index + 1];
            throw new InvalidOperationException("Missing command-line argument: " + name);
        }
    }
}
