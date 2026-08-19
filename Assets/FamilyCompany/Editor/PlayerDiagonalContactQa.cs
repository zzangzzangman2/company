using System;
using System.IO;
using FamilyCompany.Presentation.Unity;
using FamilyCompany.Presentation.Unity.OfficeGridView;
using FamilyCompany.Simulation.Navigation;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public static class PlayerDiagonalContactQa
    {
        private const string OutputRelative = "Artifacts/PlayerDiagonalContactV1/D3D11EditorFrames";
        private static readonly int[] Directions = { 1, 3, 5, 7 };
        private static readonly string[] Names = { "southwest", "northwest", "northeast", "southeast" };

        public static void RunBatch()
        {
            try
            {
                Run();
                Debug.Log("PLAYER_DIAGONAL_CONTACT_EDITOR_QA: PASS | directions=4 phases=24 generatedPixels=0");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void Run()
        {
            string output = Path.GetFullPath(OutputRelative);
            Directory.CreateDirectory(output);
            var actor = new GameObject("PlayerDiagonalContactQaActor");
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
                filterMode = FilterMode.Point
            };
            camera.targetTexture = target;

            float rootStep = OfficeLocomotionGaitRules.DefaultStrideLength / 6f;
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
                    actor.transform.position = new Vector3(phaseIndex * rootStep, 0f, 0f);
                    presenter.Present(phase, Directions[directionIndex], true, false, false);
                    int expectedFrame = phase < 0.5f ? 0 : 1;
                    if (!presenter.IsActive || presenter.ActiveFrameIndex != expectedFrame ||
                        presenter.ActiveSourceDirection != expectedSource)
                        throw new InvalidOperationException(
                            $"diagonal mismatch direction={Directions[directionIndex]} phase={phase:F3}");
                    camera.transform.position = new Vector3(actor.transform.position.x, 1.62f, -10f);
                    Render(camera, target, Path.Combine(
                        directionOutput,
                        $"player-{Names[directionIndex]}-contact-phase-{phaseIndex}.png"));
                }
            }

            presenter.Present(0f, PlayerSouthContactPresenter.SouthDirection, true, false, false);
            if (presenter.IsActive) throw new InvalidOperationException("diagonal presenter claimed cardinal direction");
            presenter.Present(0f, 1, true, true, false);
            if (presenter.IsActive) throw new InvalidOperationException("diagonal presenter claimed legacy pose");

            camera.targetTexture = null;
            RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(cameraObject);
            UnityEngine.Object.DestroyImmediate(actor);
        }

        private static void Render(Camera camera, RenderTexture target, string path)
        {
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            texture.Apply(false, false);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            RenderTexture.active = previous;
        }
    }
}
