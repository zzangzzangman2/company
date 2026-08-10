using System;
using System.IO;
using System.Linq;
using FamilyCompany.Qa.NaturalBehavior;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FamilyCompany.Editor
{
    [InitializeOnLoad]
    public static class NaturalBehaviorQualityValidation
    {
        public const string AggregatePassMarker = "NATURAL_BEHAVIOR_QUALITY_GATE: PASS";
        public const string AggregateFailMarker = "NATURAL_BEHAVIOR_QUALITY_GATE: FAIL";
        public const string HookPassMarker = "NATURAL_BEHAVIOR_QA_HOOKS: PASS";
        public const string HookFailMarker = "NATURAL_BEHAVIOR_QA_HOOKS: FAIL";
        public const string ArtifactFolder = "Artifacts/NaturalBehaviorQa";
        public const string TextReportPath = ArtifactFolder + "/natural-behavior-quality-report.txt";
        public const string JsonReportPath = ArtifactFolder + "/natural-behavior-quality-report.json";

        private const string ActiveKey = "FamilyCompany.NaturalBehaviorQa.Active";
        private const string StageKey = "FamilyCompany.NaturalBehaviorQa.Stage";
        private const string BatchKey = "FamilyCompany.NaturalBehaviorQa.Batch";
        private const string FailedKey = "FamilyCompany.NaturalBehaviorQa.Failed";
        private const string FinalMarkerKey = "FamilyCompany.NaturalBehaviorQa.FinalMarker";
        private static INaturalBehaviorQaRuntimeHook _hook;
        private static NaturalBehaviorQaRecorder _recorder;
        private static NaturalBehaviorQaPlan _plan;
        private static double _startTime;
        private static double _lastTickTime;
        private static bool _hookEnded;

        static NaturalBehaviorQualityValidation()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        [MenuItem("Family Company/QA/Validate Natural Behavior Metric Library")]
        public static void ValidateMetricLibraryMenu()
        {
            var marker = NaturalBehaviorQaSelfTest.Run();
            Debug.Log(marker);
        }

        public static void ValidateMetricLibraryBatch()
        {
            try
            {
                Debug.Log(NaturalBehaviorQaSelfTest.Run());
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError("NATURAL_BEHAVIOR_QA_METRIC_LIBRARY: FAIL");
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("Family Company/QA/Run Natural Behavior Quality Gate")]
        public static void StartMenu()
        {
            Start(false);
        }

        public static void StartBatch()
        {
            Start(true);
        }

        private static void Start(bool batch)
        {
            var ownsAttempt = false;
            try
            {
                NaturalBehaviorQaLifecycleGuard.RequireCanStart(
                    SessionState.GetBool(ActiveKey, false),
                    EditorApplication.isPlayingOrWillChangePlaymode);
                if (_hook != null || _recorder != null || _plan != null)
                {
                    try
                    {
                        EndHook();
                    }
                    finally
                    {
                        ResetRuntimeFields();
                    }
                }
                ownsAttempt = true;
                var metricMarker = NaturalBehaviorQaSelfTest.Run();
                Directory.CreateDirectory(ArtifactFolder);
                File.WriteAllText(TextReportPath,
                    $"NATURAL_BEHAVIOR_QA: START{Environment.NewLine}{metricMarker}{Environment.NewLine}");
                File.WriteAllText(JsonReportPath,
                    "{\n  \"marker\": \"NATURAL_BEHAVIOR_QA: START\",\n  \"passed\": false\n}\n");
                Debug.Log(metricMarker);

                EditorSceneManager.OpenScene(PrototypeProjectBuilder.ScenePath);
                OfficeVisualV2IntegrationQa.ValidateScenePreparation();
                SessionState.SetBool(ActiveKey, true);
                SessionState.SetInt(StageKey, 1);
                SessionState.SetBool(BatchKey, batch);
                SessionState.SetBool(FailedKey, false);
                SessionState.SetString(FinalMarkerKey, AggregateFailMarker);
                Debug.Log("NATURAL_BEHAVIOR_QA_SCENE_PREPARATION: PASS");
                EditorApplication.EnterPlaymode();
            }
            catch (Exception exception)
            {
                if (ownsAttempt)
                {
                    try
                    {
                        EndHook();
                    }
                    catch (Exception endException)
                    {
                        Debug.LogException(endException);
                    }
                    WriteFailure("PREPARATION", exception);
                    ClearSessionState();
                    ResetRuntimeFields();
                }
                Debug.LogException(exception);
                if (batch) EditorApplication.Exit(1);
                else throw;
            }
        }

        private static void OnEditorUpdate()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            try
            {
                var stage = SessionState.GetInt(StageKey, 0);
                if (NaturalBehaviorQaLifecycleGuard.IsAbandonedPreparation(
                        true, stage, EditorApplication.isPlaying, EditorApplication.isPlayingOrWillChangePlaymode))
                    throw new InvalidOperationException("Natural behavior QA preparation was abandoned before Play Mode started.");
                if (stage == 1 && EditorApplication.isPlaying)
                {
                    BeginRuntimeHook();
                    SessionState.SetInt(StageKey, 2);
                    return;
                }

                if (stage == 2 && EditorApplication.isPlaying)
                {
                    TickRuntimeHook();
                    return;
                }

                if (stage == 3 && !EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    var failed = SessionState.GetBool(FailedKey, true);
                    var marker = SessionState.GetString(FinalMarkerKey, AggregateFailMarker);
                    var batch = SessionState.GetBool(BatchKey, false);
                    Debug.Log(marker);
                    ClearSessionState();
                    ResetRuntimeFields();
                    if (batch) EditorApplication.Exit(failed ? 1 : 0);
                    return;
                }

                if (stage < 1 || stage > 3)
                    throw new InvalidOperationException($"Natural behavior QA has invalid lifecycle stage {stage}.");
            }
            catch (Exception exception)
            {
                FailRuntime(exception);
            }
        }

        private static void BeginRuntimeHook()
        {
            var componentHooks = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .OfType<INaturalBehaviorQaRuntimeHook>()
                .ToArray();
            var constructibleHookTypes = TypeCache.GetTypesDerivedFrom<INaturalBehaviorQaRuntimeHook>()
                .Where(item => !item.IsAbstract && !item.IsInterface &&
                               !typeof(MonoBehaviour).IsAssignableFrom(item) &&
                               item.GetConstructor(Type.EmptyTypes) != null)
                .OrderBy(item => item.FullName, StringComparer.Ordinal)
                .ToArray();
            if (componentHooks.Length + constructibleHookTypes.Length != 1)
                throw new InvalidOperationException(
                    "Exactly one INaturalBehaviorQaRuntimeHook provider is required; found " +
                    $"components={componentHooks.Length}, constructibleEditorTypes={constructibleHookTypes.Length}.");

            _hook = componentHooks.Length == 1
                ? componentHooks[0]
                : (INaturalBehaviorQaRuntimeHook)Activator.CreateInstance(constructibleHookTypes[0]);
            if (string.IsNullOrWhiteSpace(_hook.ProviderId))
                throw new InvalidOperationException("Natural behavior QA hook ProviderId is empty.");
            if ((_hook.Capabilities & NaturalBehaviorQaCapability.All) != NaturalBehaviorQaCapability.All)
                throw new InvalidOperationException(
                    $"QA hook '{_hook.ProviderId}' declares {_hook.Capabilities}; all capabilities are required.");

            _plan = NaturalBehaviorQaPlan.CreateCanonical();
            _recorder = new NaturalBehaviorQaRecorder(_plan, _hook.Capabilities);
            _hookEnded = false;
            _hook.Begin(_plan, _recorder);
            _startTime = EditorApplication.timeSinceStartup;
            _lastTickTime = _startTime;
            Append(HookPassMarker + " | provider=" + _hook.ProviderId + " | capabilities=" + _hook.Capabilities);
        }

        private static void TickRuntimeHook()
        {
            if (_hook == null || _recorder == null || _plan == null)
                throw new InvalidOperationException("Natural behavior QA runtime state was lost.");

            var now = EditorApplication.timeSinceStartup;
            var delta = Math.Max(0d, Math.Min(0.25d, now - _lastTickTime));
            _lastTickTime = now;
            _hook.Tick(delta);

            var captureCount = 0;
            while (_hook.TryTakeCaptureRequest(out var label))
            {
                if (++captureCount > 16)
                    throw new InvalidOperationException("QA hook emitted more than 16 capture requests in one tick.");
                var captureLabel = "natural-" + SafeLabel(label);
                var capturePath = OfficeVisualV2IntegrationQa.CaptureResolutionPair(captureLabel);
                var artifact = new NaturalBehaviorQaCaptureArtifact(
                    captureLabel,
                    NaturalBehaviorQaHash.Sha256Hex(File.ReadAllBytes(capturePath)),
                    1920,
                    1080);
                _recorder.RecordCaptureArtifact(artifact);
                _hook.OnCaptureCompleted(artifact);
            }

            if (now - _startTime > _plan.MaximumWallClockSeconds)
                throw new TimeoutException(
                    $"Natural behavior QA exceeded {_plan.MaximumWallClockSeconds:F0} wall-clock seconds.");
            if (!_hook.IsComplete) return;

            EndHook();
            var result = NaturalBehaviorQualityEvaluator.Evaluate(_recorder.Build());
            File.WriteAllText(TextReportPath, NaturalBehaviorQaReportFormatter.ToText(result));
            File.WriteAllText(JsonReportPath, NaturalBehaviorQaReportFormatter.ToJson(result));
            foreach (var gate in result.Gates) Debug.Log(gate.Marker);
            OfficeVisualV2IntegrationQa.CaptureResolutionPair("natural-final");
            SessionState.SetBool(FailedKey, !result.Passed);
            SessionState.SetString(FinalMarkerKey, result.Marker);
            SessionState.SetInt(StageKey, 3);
            EditorApplication.ExitPlaymode();
        }

        private static void FailRuntime(Exception exception)
        {
            try
            {
                EndHook();
            }
            catch (Exception endException)
            {
                Debug.LogException(endException);
            }
            WriteFailure("RUNTIME", exception);
            Debug.LogError(HookFailMarker);
            Debug.LogException(exception);
            SessionState.SetBool(FailedKey, true);
            SessionState.SetString(FinalMarkerKey, AggregateFailMarker);
            SessionState.SetInt(StageKey, 3);
            if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
        }

        private static void WriteFailure(string stage, Exception exception)
        {
            Directory.CreateDirectory(ArtifactFolder);
            var message = exception == null ? "Unknown failure." : exception.ToString();
            File.WriteAllText(TextReportPath,
                $"{AggregateFailMarker}{Environment.NewLine}{HookFailMarker}{Environment.NewLine}" +
                $"FAILURE_STAGE: {stage}{Environment.NewLine}{message}{Environment.NewLine}");
            File.WriteAllText(JsonReportPath,
                "{\n  \"marker\": \"" + AggregateFailMarker + "\",\n  \"passed\": false,\n" +
                "  \"failureStage\": \"" + JsonEscape(stage) + "\",\n" +
                "  \"error\": \"" + JsonEscape(message) + "\"\n}\n");
            SessionState.SetBool(FailedKey, true);
            SessionState.SetString(FinalMarkerKey, AggregateFailMarker);
        }

        private static void EndHook()
        {
            if (_hook == null || _hookEnded) return;
            _hookEnded = true;
            _hook.End();
        }

        private static void ClearSessionState()
        {
            SessionState.EraseBool(ActiveKey);
            SessionState.EraseInt(StageKey);
            SessionState.EraseBool(BatchKey);
            SessionState.EraseBool(FailedKey);
            SessionState.EraseString(FinalMarkerKey);
        }

        private static void ResetRuntimeFields()
        {
            _hook = null;
            _recorder = null;
            _plan = null;
            _startTime = 0d;
            _lastTickTime = 0d;
            _hookEnded = false;
        }

        private static void Append(string line)
        {
            Directory.CreateDirectory(ArtifactFolder);
            File.AppendAllText(TextReportPath, line + Environment.NewLine);
            Debug.Log(line);
        }

        private static string SafeLabel(string label)
        {
            var source = string.IsNullOrWhiteSpace(label) ? "capture" : label.Trim();
            var result = new char[source.Length];
            for (var index = 0; index < source.Length; index++)
            {
                var value = source[index];
                result[index] = char.IsLetterOrDigit(value) || value == '-' || value == '_' ? value : '-';
            }
            return new string(result);
        }

        private static string JsonEscape(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }
    }
}
