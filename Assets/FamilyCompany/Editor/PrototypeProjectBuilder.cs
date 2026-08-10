using System.IO;
using FamilyCompany.Presentation.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FamilyCompany.Editor
{
    public static class PrototypeProjectBuilder
    {
        public const string ScenePath = "Assets/FamilyCompany/Scenes/Prototype01.unity";
        public const string SisterAssetPath = "Assets/Art/Characters/OlderSister/older_sister_casual_neutral_v2.png";
        private const string MaterialFolder = "Assets/FamilyCompany/Generated/Materials";

        [MenuItem("Family Company/Build Prototype 0.1")]
        public static void Build()
        {
            EnsureFolder("Assets/FamilyCompany/Scenes");
            EnsureFolder("Assets/FamilyCompany/Generated");
            EnsureFolder(MaterialFolder);
            ConfigureSisterTexture();

            var homeMaterial = GetMaterial("HomeFloor", new Color(0.78f, 0.68f, 0.55f));
            var streetMaterial = GetMaterial("Street", new Color(0.29f, 0.31f, 0.34f));
            var officeMaterial = GetMaterial("OfficeFloor", new Color(0.54f, 0.67f, 0.64f));
            var wallMaterial = GetMaterial("Wall", new Color(0.87f, 0.84f, 0.76f));
            var playerMaterial = GetMaterial("Player", new Color(0.24f, 0.55f, 0.92f));
            var fatherMaterial = GetMaterial("FatherPlaceholder", new Color(0.32f, 0.43f, 0.64f));
            var motherMaterial = GetMaterial("MotherPlaceholder", new Color(0.79f, 0.45f, 0.55f));
            var furnitureMaterial = GetMaterial("Furniture", new Color(0.39f, 0.27f, 0.20f));

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Prototype01";

            var systems = new GameObject("Systems");
            systems.AddComponent<PrototypeBootstrap>();

            var environment = new GameObject("Environment");
            var home = new GameObject("HOME");
            home.transform.SetParent(environment.transform);
            CreateCube("Home Floor", new Vector3(-12f, -0.15f, 0f), new Vector3(10f, 0.3f, 12f), homeMaterial, home.transform);
            CreateCube("Home Back Wall", new Vector3(-12f, 1.5f, 5.8f), new Vector3(10f, 3f, 0.3f), wallMaterial, home.transform);
            CreateCube("Home Front Wall", new Vector3(-12f, 1.5f, -5.8f), new Vector3(10f, 3f, 0.3f), wallMaterial, home.transform);
            CreateCube("Home Outer Wall", new Vector3(-16.8f, 1.5f, 0f), new Vector3(0.3f, 3f, 12f), wallMaterial, home.transform);
            CreateCube("Kitchen Table", new Vector3(-13f, 0.45f, 1.4f), new Vector3(3.2f, 0.8f, 1.7f), furnitureMaterial, home.transform);
            CreateLabel("집", new Vector3(-12f, 3.4f, 5.55f), home.transform);

            var street = new GameObject("STREET");
            street.transform.SetParent(environment.transform);
            CreateCube("Street Floor", new Vector3(0f, -0.2f, 0f), new Vector3(14f, 0.2f, 12f), streetMaterial, street.transform);
            CreateCube("North Sidewalk", new Vector3(0f, -0.05f, 5.1f), new Vector3(14f, 0.25f, 1.4f), wallMaterial, street.transform);
            CreateCube("South Sidewalk", new Vector3(0f, -0.05f, -5.1f), new Vector3(14f, 0.25f, 1.4f), wallMaterial, street.transform);
            CreateLabel("거리", new Vector3(0f, 0.25f, 4.8f), street.transform);

            var office = new GameObject("SMALL OFFICE");
            office.transform.SetParent(environment.transform);
            CreateCube("Office Floor", new Vector3(12f, -0.15f, 0f), new Vector3(10f, 0.3f, 12f), officeMaterial, office.transform);
            CreateCube("Office Back Wall", new Vector3(12f, 1.5f, 5.8f), new Vector3(10f, 3f, 0.3f), wallMaterial, office.transform);
            CreateCube("Office Front Wall", new Vector3(12f, 1.5f, -5.8f), new Vector3(10f, 3f, 0.3f), wallMaterial, office.transform);
            CreateCube("Office Outer Wall", new Vector3(16.8f, 1.5f, 0f), new Vector3(0.3f, 3f, 12f), wallMaterial, office.transform);
            CreateCube("Work Desk A", new Vector3(10.8f, 0.45f, 1.8f), new Vector3(2.8f, 0.8f, 1.3f), furnitureMaterial, office.transform);
            CreateCube("Work Desk B", new Vector3(14f, 0.45f, 1.8f), new Vector3(2.8f, 0.8f, 1.3f), furnitureMaterial, office.transform);
            CreateCube("Reception Desk", new Vector3(9.5f, 0.55f, -2.4f), new Vector3(1.2f, 1f, 3.2f), furnitureMaterial, office.transform);
            CreateLabel("작은 사무실", new Vector3(12f, 3.4f, 5.55f), office.transform);

            var characters = new GameObject("Characters");
            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player (14)";
            player.transform.SetParent(characters.transform);
            player.transform.position = new Vector3(-10.5f, 1f, -1.5f);
            player.GetComponent<Renderer>().sharedMaterial = playerMaterial;
            Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());
            player.AddComponent<CharacterController>();
            player.AddComponent<PrototypePlayerController>();
            CreateLabel("나 · 14살", player.transform.position + new Vector3(0f, 1.6f, 0f), player.transform, false);

            CreatePlaceholder("Father Placeholder (46)", "아빠 자리 · 46살\n최종 에셋 대기", new Vector3(-14.3f, 1f, -1.5f), fatherMaterial, characters.transform);
            CreatePlaceholder("Mother Placeholder (44)", "엄마 자리 · 44살\n최종 에셋 대기", new Vector3(-12.4f, 1f, 2.9f), motherMaterial, characters.transform);
            CreateSister(characters.transform);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.69f, 0.78f);
            camera.fieldOfView = 48f;
            cameraObject.transform.position = player.transform.position + new Vector3(-10f, 13f, -10f);
            cameraObject.transform.LookAt(player.transform.position + Vector3.up);
            var follow = cameraObject.AddComponent<IsometricCameraFollow>();
            follow.SetTarget(player.transform);

            var lightObject = new GameObject("Sun");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            RenderSettings.ambientLight = new Color(0.56f, 0.56f, 0.56f);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"FAMILY_COMPANY_BUILD: PASS ({ScenePath})");
        }

        private static void ConfigureSisterTexture()
        {
            AssetDatabase.ImportAsset(SisterAssetPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(SisterAssetPath) as TextureImporter;
            if (importer == null)
            {
                throw new FileNotFoundException("Canonical sister asset not found.", SisterAssetPath);
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 512f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        private static void CreateSister(Transform parent)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SisterAssetPath);
            if (sprite == null) throw new FileNotFoundException("Canonical sister sprite import failed.", SisterAssetPath);
            var sister = new GameObject("Older Sister (20) - CANONICAL");
            sister.transform.SetParent(parent);
            sister.transform.position = new Vector3(12.8f, 1.45f, -1.6f);
            var renderer = sister.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 10;
            sister.AddComponent<BillboardFacingCamera>();
            CreateLabel("누나 · 20살\n운영 / 고객 응대", sister.transform.position + new Vector3(0f, 1.75f, 0f), sister.transform, false);
        }

        private static GameObject CreatePlaceholder(string name, string label, Vector3 position, Material material, Transform parent)
        {
            var placeholder = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            placeholder.name = name;
            placeholder.transform.SetParent(parent);
            placeholder.transform.position = position;
            placeholder.GetComponent<Renderer>().sharedMaterial = material;
            CreateLabel(label, position + new Vector3(0f, 1.7f, 0f), placeholder.transform, false);
            return placeholder;
        }

        private static GameObject CreateCube(string name, Vector3 position, Vector3 scale, Material material, Transform parent)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent);
            cube.transform.position = position;
            cube.transform.localScale = scale;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            return cube;
        }

        private static GameObject CreateLabel(string text, Vector3 position, Transform parent, bool faceForward = true)
        {
            var labelObject = new GameObject($"Label - {text.Replace('\n', ' ')}");
            labelObject.transform.SetParent(parent);
            labelObject.transform.position = position;
            if (faceForward) labelObject.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            var textMesh = labelObject.AddComponent<TextMesh>();
            textMesh.text = text;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.fontSize = 52;
            textMesh.characterSize = 0.05f;
            textMesh.color = Color.white;
            labelObject.AddComponent<BillboardFacingCamera>();
            return labelObject;
        }

        private static Material GetMaterial(string name, Color color)
        {
            var path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Standard") ?? Shader.Find("Unlit/Color");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}

