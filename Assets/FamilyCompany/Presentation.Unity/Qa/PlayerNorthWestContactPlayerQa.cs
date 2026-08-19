using System;
using System.Collections;
using System.IO;
using FamilyCompany.Presentation.Unity.OfficeGridView;
using UnityEngine;
using UnityEngine.Rendering;

namespace FamilyCompany.Presentation.Unity.Qa
{
    public sealed class PlayerNorthWestContactPlayerQa : MonoBehaviour
    {
        private const string EnableArgument = "-playerNorthWestContactQa";
        private const string OutputArgument = "-playerNorthWestContactQaOutput";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallIfRequested()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), EnableArgument) < 0) return;
            new GameObject("~PlayerNorthWestContactPlayerQa").AddComponent<PlayerNorthWestContactPlayerQa>();
        }

        private IEnumerator Start()
        {
            Application.runInBackground = true;
            yield return null;
            string output = ReadRequiredArgument(OutputArgument);
            Directory.CreateDirectory(output);
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D11)
                Fail("graphics device is " + SystemInfo.graphicsDeviceType + ", expected Direct3D11");

            var cameraObject = new GameObject("PlayerNorthWestContactQaCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 2.05f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(238, 233, 220, 255);
            var target = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32)
            {
                filterMode = FilterMode.Point,
                antiAliasing = 1
            };
            target.Create();
            camera.targetTexture = target;

            CaptureNorth(camera, target, Path.Combine(output, "north"));
            CaptureWest(camera, target, Path.Combine(output, "west"));

            Debug.Log(
                "PLAYER_NORTH_WEST_CONTACT_D3D11_PLAYER_QA: PASS | " +
                $"unity={Application.unityVersion} graphics={SystemInfo.graphicsDeviceType} " +
                $"device={SystemInfo.graphicsDeviceName} directions=2 phases=12 sourceFrames=4 output={output}");
            camera.targetTexture = null;
            target.Release();
            Destroy(target);
            yield return null;
            Application.Quit(0);
        }

        private static void CaptureNorth(Camera camera, RenderTexture target, string output)
        {
            Directory.CreateDirectory(output);
            var actor = CreateActor("PlayerNorthContactQaActor", out SpriteRenderer renderer);
            var presenter = actor.AddComponent<PlayerNorthContactPresenter>();
            presenter.Configure(renderer);
            for (var phaseIndex = 0; phaseIndex < 6; phaseIndex++)
            {
                float phase = phaseIndex / 6f;
                PositionActor(actor, camera, phaseIndex);
                presenter.Present(phase, PlayerNorthContactPresenter.NorthDirection, true, false, false);
                ValidateFrame("north", phase, presenter.IsActive, presenter.ActiveFrameIndex);
                Capture(camera, target, Path.Combine(output, $"player-north-contact-player-phase-{phaseIndex}.png"));
            }
            Destroy(actor);
        }

        private static void CaptureWest(Camera camera, RenderTexture target, string output)
        {
            Directory.CreateDirectory(output);
            var actor = CreateActor("PlayerWestContactQaActor", out SpriteRenderer renderer);
            var presenter = actor.AddComponent<PlayerWestContactPresenter>();
            presenter.Configure(renderer);
            for (var phaseIndex = 0; phaseIndex < 6; phaseIndex++)
            {
                float phase = phaseIndex / 6f;
                PositionActor(actor, camera, phaseIndex);
                presenter.Present(phase, PlayerWestContactPresenter.WestDirection, true, false, false);
                ValidateFrame("west", phase, presenter.IsActive, presenter.ActiveFrameIndex);
                Capture(camera, target, Path.Combine(output, $"player-west-contact-player-phase-{phaseIndex}.png"));
            }
            Destroy(actor);
        }

        private static GameObject CreateActor(string name, out SpriteRenderer renderer)
        {
            var actor = new GameObject(name);
            actor.transform.position = new Vector3(5000f, 5000f, 0f);
            var visual = new GameObject("Visual");
            visual.transform.SetParent(actor.transform, false);
            visual.transform.localScale = Vector3.one * OfficeGridCharacterMover.UniformVisualScale;
            renderer = visual.AddComponent<SpriteRenderer>();
            return actor;
        }

        private static void PositionActor(GameObject actor, Camera camera, int phaseIndex)
        {
            float rootStep = FamilyCompany.Simulation.Navigation.OfficeLocomotionGaitRules.DefaultStrideLength / 6f;
            actor.transform.position = new Vector3(5000f + phaseIndex * rootStep, 5000f, 0f);
            camera.transform.position = actor.transform.position + new Vector3(0f, 1.62f, -10f);
        }

        private static void ValidateFrame(string direction, float phase, bool active, int actual)
        {
            int expected = phase < 0.5f ? 0 : 1;
            if (!active || actual != expected)
                Fail($"{direction} phase mismatch phase={phase:F3} expected={expected} actual={actual}");
        }

        private static void Capture(Camera camera, RenderTexture target, string path)
        {
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.Render();
                RenderTexture.active = target;
                var texture = new Texture2D(512, 512, TextureFormat.RGBA32, false, false)
                {
                    filterMode = FilterMode.Point
                };
                texture.ReadPixels(new Rect(0f, 0f, 512, 512), 0, 0, false);
                texture.Apply(false, false);
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Destroy(texture);
            }
            finally
            {
                RenderTexture.active = previous;
            }
        }

        private static string ReadRequiredArgument(string argumentName)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index < arguments.Length - 1; index++)
            {
                if (string.Equals(arguments[index], argumentName, StringComparison.Ordinal))
                    return Path.GetFullPath(arguments[index + 1]);
            }
            Fail("missing command-line argument " + argumentName);
            return string.Empty;
        }

        private static void Fail(string message)
        {
            Debug.LogError("PLAYER_NORTH_WEST_CONTACT_D3D11_PLAYER_QA: FAIL | " + message);
            Application.Quit(2);
            throw new InvalidOperationException(message);
        }
    }
}
