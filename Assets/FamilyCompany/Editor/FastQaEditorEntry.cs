using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace FamilyCompany.Editor
{
    public static class FastQaEditorEntry
    {
        private const string ExpectedFirstScene = "Assets/FamilyCompany/Scenes/Prototype01.unity";

        public static void Run()
        {
            var total = Stopwatch.StartNew();
            try
            {
                string mode = ReadArgument("-fastQaMode", "validate");
                Debug.Log($"FAST_QA_EDITOR: START | mode={mode} unity={Application.unityVersion}");
                if (string.Equals(mode, "validate", StringComparison.OrdinalIgnoreCase))
                    RunValidations();
                else if (string.Equals(mode, "build-scripts", StringComparison.OrdinalIgnoreCase))
                    BuildPlayer(BuildOptions.BuildScriptsOnly);
                else if (string.Equals(mode, "build-normal", StringComparison.OrdinalIgnoreCase))
                    BuildPlayer(BuildOptions.None);
                else if (string.Equals(mode, "build-clean", StringComparison.OrdinalIgnoreCase))
                    BuildPlayer(BuildOptions.CleanBuildCache);
                else
                    throw new InvalidOperationException("Unknown fast QA editor mode: " + mode);
                total.Stop();
                Debug.Log($"FAST_QA_EDITOR: PASS | mode={mode} elapsedMs={total.ElapsedMilliseconds}");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                total.Stop();
                Debug.LogException(exception);
                Debug.LogError($"FAST_QA_EDITOR: FAIL | elapsedMs={total.ElapsedMilliseconds}");
                EditorApplication.Exit(1);
            }
        }

        private static void RunValidations()
        {
            string raw = ReadRequiredArgument("-fastQaMethods");
            string[] methods = raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            if (methods.Length == 0) throw new InvalidOperationException("No fast QA methods were selected.");
            foreach (string methodName in methods.Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal))
            {
                Stopwatch timer = Stopwatch.StartNew();
                MethodInfo method = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType(methodName.Substring(0, methodName.LastIndexOf('.')), false))
                    .Where(type => type != null)
                    .Select(type => type.GetMethod(methodName.Substring(methodName.LastIndexOf('.') + 1),
                        BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null))
                    .FirstOrDefault(candidate => candidate != null);
                if (method == null) throw new MissingMethodException("Fast QA method not found: " + methodName);
                try { method.Invoke(null, null); }
                catch (TargetInvocationException wrapper) { throw wrapper.InnerException ?? wrapper; }
                timer.Stop();
                Debug.Log($"FAST_QA_VALIDATION: PASS | method={methodName} elapsedMs={timer.ElapsedMilliseconds}");
            }
        }

        private static void BuildPlayer(BuildOptions options)
        {
            string output = Path.GetFullPath(ReadRequiredArgument("-fastQaBuildOutput"));
            Directory.CreateDirectory(Path.GetDirectoryName(output) ?? throw new InvalidOperationException("Build output has no directory."));
            string[] scenes = EditorBuildSettings.scenes.Where(item => item != null && item.enabled)
                .Select(item => item.path).Where(path => !string.IsNullOrWhiteSpace(path)).ToArray();
            if (scenes.Length == 0 || !string.Equals(scenes[0], ExpectedFirstScene, StringComparison.Ordinal))
                throw new InvalidOperationException("Fast QA player must start with Prototype01.");
            Stopwatch timer = Stopwatch.StartNew();
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = options | BuildOptions.StrictMode
            });
            timer.Stop();
            BuildSummary summary = report.summary;
            Debug.Log($"FAST_QA_BUILD: result={summary.result} options={options} elapsedMs={timer.ElapsedMilliseconds} " +
                      $"errors={summary.totalErrors} warnings={summary.totalWarnings} bytes={summary.totalSize}");
            if (summary.result != BuildResult.Succeeded || !File.Exists(output))
                throw new InvalidOperationException("Fast QA Windows player build failed: " + summary.result);
        }

        private static string ReadRequiredArgument(string name)
        {
            string value = ReadArgument(name, null);
            if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException("Missing command-line argument: " + name);
            return value;
        }

        private static string ReadArgument(string name, string fallback)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int index = 0; index < args.Length - 1; index++)
                if (string.Equals(args[index], name, StringComparison.Ordinal)) return args[index + 1];
            return fallback;
        }
    }
}
