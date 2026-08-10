using System;
using System.IO;
using System.Linq;
using FamilyCompany.Presentation.Unity;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor
{
    [InitializeOnLoad]
    public static class OfficeVisualV2CalibrationQa
    {
        private const string ActiveKey = "FamilyCompany.OfficeVisualV2CalibrationQa.Active";
        private const string StageKey = "FamilyCompany.OfficeVisualV2CalibrationQa.Stage";
        private const string StepKey = "FamilyCompany.OfficeVisualV2CalibrationQa.Step";
        private const string NextKey = "FamilyCompany.OfficeVisualV2CalibrationQa.Next";
        private const string Folder = "Artifacts/OfficeVisualV2CalibrationQa";
        private const string Report = Folder + "/calibration-qa.txt";
        private static readonly float[] Scales = { 0.95f, 1f, 1.05f };

        static OfficeVisualV2CalibrationQa()
        {
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
        }

        public static void StartBatch()
        {
            try
            {
                Directory.CreateDirectory(Folder);
                File.WriteAllText(Report,
                    $"OFFICE_VISUAL_V2_CALIBRATION_QA | {DateTime.Now:O}{Environment.NewLine}");
                PrototypeProjectBuilder.Build();
                SessionState.SetBool(ActiveKey, true);
                SessionState.SetInt(StageKey, 1);
                SessionState.SetInt(StepKey, 0);
                EditorApplication.EnterPlaymode();
            }
            catch (Exception exception)
            {
                Append("PREP_FAIL | " + exception);
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void Update()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            try
            {
                var stage = SessionState.GetInt(StageKey, 0);
                if (stage == 1 && EditorApplication.isPlaying)
                {
                    PreparePlayMode();
                    SessionState.SetFloat(NextKey, (float)EditorApplication.timeSinceStartup + 1f);
                    SessionState.SetInt(StageKey, 2);
                    return;
                }

                if (stage == 2 && EditorApplication.isPlaying &&
                    EditorApplication.timeSinceStartup >= SessionState.GetFloat(NextKey, 0f))
                {
                    var step = SessionState.GetInt(StepKey, 0);
                    if (step < Scales.Length)
                    {
                        CaptureScale(step);
                        SessionState.SetInt(StepKey, step + 1);
                        SessionState.SetFloat(NextKey, (float)EditorApplication.timeSinceStartup + 0.5f);
                        return;
                    }

                    SessionState.SetInt(StageKey, 3);
                    EditorApplication.ExitPlaymode();
                    return;
                }

                if (stage == 3 && !EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    Append("CALIBRATION_CAPTURE_PASS | scales=0.95,1.00,1.05 | resolutions=1280x720,1920x1080");
                    SessionState.EraseBool(ActiveKey);
                    EditorApplication.Exit(0);
                }
            }
            catch (Exception exception)
            {
                Append("CALIBRATION_CAPTURE_FAIL | " + exception);
                Debug.LogException(exception);
                SessionState.EraseBool(ActiveKey);
                if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
                else EditorApplication.Exit(1);
            }
        }

        private static void PreparePlayMode()
        {
            var camera = Camera.main ?? throw new InvalidOperationException("Main camera is missing.");
            var follow = camera.GetComponent<IsometricCameraFollow>();
            if (follow != null) follow.enabled = false;
            camera.transform.position = new Vector3(14f, 13.5f, -13.5f);
            camera.transform.LookAt(new Vector3(14f, 0.6f, 0f));
            camera.orthographicSize = 6.6f;

            foreach (var coordinator in UnityEngine.Object.FindObjectsByType<OfficeAutonomyCoordinator>(FindObjectsSortMode.None))
                coordinator.enabled = false;
            foreach (var coordinator in UnityEngine.Object.FindObjectsByType<OfficeContractTaskCoordinator>(FindObjectsSortMode.None))
                coordinator.enabled = false;

            var player = UnityEngine.Object.FindFirstObjectByType<PrototypePlayerController>()
                         ?? throw new InvalidOperationException("Player is missing.");
            player.enabled = false;
            Place(player.transform, OfficeVisualV2Calibration.ArtPixelToWorld(OfficeVisualV2Calibration.DeskAApproachArt));

            var agents = UnityEngine.Object.FindObjectsByType<OfficeWorkerAgent>(FindObjectsSortMode.None)
                .ToDictionary(item => item.AgentId, StringComparer.Ordinal);
            Place(agents["older_sister"].transform,
                OfficeVisualV2Calibration.ArtPixelToWorld(OfficeVisualV2Calibration.DeskBApproachArt));
            var deskC = UnityEngine.Object.FindObjectsByType<OfficeWaypoint>(FindObjectsSortMode.None)
                .First(item => string.Equals(item.WaypointId, "desk_c", StringComparison.Ordinal));
            Place(agents["father"].transform, deskC.transform.position);
            agents["father"].SetAutonomousDestination("calibration-desk-c", deskC, "calibration");
            Place(agents["mother"].transform,
                OfficeVisualV2Calibration.ArtPixelToWorld(OfficeVisualV2Calibration.DeskDApproachArt));
            foreach (var agent in agents.Values) agent.enabled = false;

            Append("PLACEMENT | player=desk_a | sister=desk_b | father=desk_c | mother=desk_d");
        }

        private static void CaptureScale(int step)
        {
            var scale = Scales[step];
            var presenter = UnityEngine.Object.FindFirstObjectByType<OfficeVisualV2Presenter>()
                            ?? throw new InvalidOperationException("OfficeVisualV2Presenter is missing.");
            presenter.SetCharacterVisualScaleForQa(scale);
            var label = $"scale-{Mathf.RoundToInt(scale * 100):000}";
            OfficeVisualV2IntegrationQa.CaptureResolutionPair(label);
            var camera = Camera.main;
            foreach (var animator in UnityEngine.Object.FindObjectsByType<DirectionalSpriteAnimator>(FindObjectsSortMode.None)
                         .OrderBy(item => item.name, StringComparer.Ordinal))
            {
                if (presenter.TryGetCharacterArtFootPixel(animator, camera, out var foot))
                    Append($"FOOT | scale={scale:F2} | actor={animator.name} | art=({foot.x:F2},{foot.y:F2})");
            }
        }

        private static void Place(Transform root, Vector3 position)
        {
            var controller = root.GetComponent<CharacterController>();
            var enabled = controller != null && controller.enabled;
            if (controller != null) controller.enabled = false;
            root.position = position;
            if (controller != null) controller.enabled = enabled;
        }

        private static void Append(string text)
        {
            Directory.CreateDirectory(Folder);
            File.AppendAllText(Report, text + Environment.NewLine);
            Debug.Log(text);
        }
    }
}
