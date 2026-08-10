using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FamilyCompany.Editor
{
    public static class PrototypeSceneCapture
    {
        private const string OutputPath = "Artifacts/Prototype01/prototype01-overview.png";

        [MenuItem("Family Company/Capture Prototype Overview")]
        public static void Capture()
        {
            EditorSceneManager.OpenScene(PrototypeProjectBuilder.ScenePath, OpenSceneMode.Single);
            var camera = Camera.main;
            if (camera == null) throw new InvalidDataException("Main Camera is missing.");

            camera.transform.position = new Vector3(0f, 24f, -24f);
            camera.transform.LookAt(new Vector3(0f, 0.5f, 0f));
            camera.fieldOfView = 52f;
            FaceBillboards(camera);

            const int width = 1600;
            const int height = 900;
            var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            try
            {
                camera.targetTexture = renderTexture;
                RenderTexture.active = renderTexture;
                camera.Render();
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                var absolute = Path.GetFullPath(OutputPath);
                Directory.CreateDirectory(Path.GetDirectoryName(absolute));
                File.WriteAllBytes(absolute, texture.EncodeToPNG());
                Debug.Log($"FAMILY_COMPANY_CAPTURE: PASS ({absolute})");
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                Object.DestroyImmediate(renderTexture);
                Object.DestroyImmediate(texture);
            }
        }

        private static void FaceBillboards(Camera camera)
        {
            foreach (var billboard in Object.FindObjectsByType<Presentation.Unity.BillboardFacingCamera>(FindObjectsSortMode.None))
            {
                var direction = billboard.transform.position - camera.transform.position;
                direction.y = 0f;
                if (direction.sqrMagnitude > 0.001f)
                {
                    billboard.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                }
            }
        }
    }
}

