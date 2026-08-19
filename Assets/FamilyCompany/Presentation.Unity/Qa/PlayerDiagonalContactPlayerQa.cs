using System;
using System.Collections;
using System.IO;
using FamilyCompany.Presentation.Unity.OfficeGridView;
using UnityEngine;
using UnityEngine.Rendering;

namespace FamilyCompany.Presentation.Unity.Qa
{
    public sealed class PlayerDiagonalContactPlayerQa : MonoBehaviour
    {
        private const string EnableArgument = "-playerDiagonalContactQa";
        private const string OutputArgument = "-playerDiagonalContactQaOutput";
        private static readonly int[] Directions = { 1, 3, 5, 7 };
        private static readonly string[] Names = { "southwest", "northwest", "northeast", "southeast" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallIfRequested()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), EnableArgument) < 0) return;
            new GameObject("~PlayerDiagonalContactPlayerQa").AddComponent<PlayerDiagonalContactPlayerQa>();
        }

        private IEnumerator Start()
        {
            Application.runInBackground = true;
            yield return null;
            string output = ReadRequiredArgument(OutputArgument);
            Directory.CreateDirectory(output);
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D11)
                Fail("graphics device is " + SystemInfo.graphicsDeviceType + ", expected Direct3D11");

            var actor = new GameObject("PlayerDiagonalContactQaActor");
            actor.transform.position = new Vector3(5000f, 5000f, 0f);
            var visual = new GameObject("Visual");
            visual.transform.SetParent(actor.transform, false);
            visual.transform.localScale = Vector3.one * OfficeGridCharacterMover.UniformVisualScale;
            var renderer = visual.AddComponent<SpriteRenderer>();
            var presenter = actor.AddComponent<PlayerDiagonalContactPresenter>();
            presenter.Configure(renderer);

            var cameraObject = new GameObject("PlayerDiagonalContactQaCamera");
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

            float rootStep = FamilyCompany.Simulation.Navigation.OfficeLocomotionGaitRules.DefaultStrideLength / 6f;
            for (var directionIndex = 0; directionIndex < Directions.Length; directionIndex++)
            {
                string directionOutput = Path.Combine(output, Names[directionIndex]);
                Directory.CreateDirectory(directionOutput);
                int expectedSource = Directions[directionIndex] < 4
                    ? PlayerWestContactPresenter.WestDirection
                    : PlayerEastContactPresenter.EastDirection;
                for (var phaseIndex = 0; phaseIndex < 6; phaseIndex++)
                {
                    float phase = phaseIndex / 6f;
                    actor.transform.position = new Vector3(5000f + phaseIndex * rootStep, 5000f, 0f);
                    presenter.Present(phase, Directions[directionIndex], true, false, false);
                    int expectedFrame = phase < 0.5f ? 0 : 1;
                    if (!presenter.IsActive || presenter.ActiveFrameIndex != expectedFrame ||
                        presenter.ActiveSourceDirection != expectedSource)
                        Fail($"diagonal mismatch direction={Directions[directionIndex]} phase={phase:F3}");
                    camera.transform.position = actor.transform.position + new Vector3(0f, 1.62f, -10f);
                    Capture(camera, target, Path.Combine(
                        directionOutput,
                        $"player-{Names[directionIndex]}-contact-player-phase-{phaseIndex}.png"));
                }
            }

            Debug.Log(
                "PLAYER_DIAGONAL_CONTACT_D3D11_PLAYER_QA: PASS | " +
                $"unity={Application.unityVersion} graphics={SystemInfo.graphicsDeviceType} " +
                $"device={SystemInfo.graphicsDeviceName} directions=4 phases=24 generatedPixels=0 output={output}");
            camera.targetTexture = null;
            target.Release();
            Destroy(target);
            yield return null;
            Application.Quit(0);
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
            Debug.LogError("PLAYER_DIAGONAL_CONTACT_D3D11_PLAYER_QA: FAIL | " + message);
            Application.Quit(2);
            throw new InvalidOperationException(message);
        }
    }
}
