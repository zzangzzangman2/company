using System;
using System.IO;
using FamilyCompany.Presentation.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace FamilyCompany.Editor
{
    public static class PrototypeProjectBuilder
    {
        public const string ScenePath = "Assets/FamilyCompany/Scenes/Prototype01.unity";
        public const string SisterPortraitAssetPath = "Assets/Art/Characters/OlderSister/older_sister_casual_neutral_v2.png";
        public const string SisterPixelSheetPath = "Assets/Art/Characters/OlderSister/Pixel/older_sister_pixel_walk4x2_v2.png";
        public const string SisterFrameFolder = "Assets/Art/Characters/OlderSister/Pixel/Frames";
        private const string MaterialFolder = "Assets/FamilyCompany/Generated/Materials";
        private static readonly string[] SisterFrameNames =
        {
            "sister_south_a", "sister_west_a", "sister_north_a", "sister_east_a",
            "sister_south_b", "sister_west_b", "sister_north_b", "sister_east_b"
        };

        private static Material _wood;
        private static Material _lightWood;
        private static Material _cream;
        private static Material _mint;
        private static Material _peach;
        private static Material _teal;
        private static Material _beige;
        private static Material _plant;
        private static Material _dark;

        [MenuItem("Family Company/Build Office V0.2")]
        public static void Build()
        {
            EnsureFolder("Assets/FamilyCompany/Scenes");
            EnsureFolder("Assets/FamilyCompany/Generated");
            EnsureFolder(MaterialFolder);
            ConfigureSisterPixelSheet();
            CreateMaterials();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Prototype01";

            var systems = new GameObject("Systems");
            systems.AddComponent<PrototypeBootstrap>();

            var environment = new GameObject("Environment");
            CreateHome(environment.transform);
            CreateStreet(environment.transform);
            var officeLayout = CreateOffice(environment.transform);

            var characters = new GameObject("Characters");
            var player = CreatePlayer(characters.transform);
            CreateParentPlaceholders(characters.transform);
            CreateSister(characters.transform, officeLayout);
            CreateMovingWorkers(characters.transform, officeLayout);
            CreateCamera(player.transform);
            CreateLighting();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"FAMILY_COMPANY_BUILD: PASS OFFICE_V0_2 ({ScenePath})");
        }

        private static void CreateMaterials()
        {
            _wood = GetMaterial("PixelWood", new Color(0.63f, 0.40f, 0.24f));
            _lightWood = GetMaterial("PixelLightWood", new Color(0.79f, 0.59f, 0.38f));
            _cream = GetMaterial("PixelCream", new Color(0.93f, 0.89f, 0.77f));
            _mint = GetMaterial("PixelMint", new Color(0.50f, 0.72f, 0.64f));
            _peach = GetMaterial("PixelPeach", new Color(0.93f, 0.49f, 0.51f));
            _teal = GetMaterial("PixelTeal", new Color(0.20f, 0.61f, 0.66f));
            _beige = GetMaterial("PixelBeige", new Color(0.82f, 0.78f, 0.65f));
            _plant = GetMaterial("PixelPlant", new Color(0.24f, 0.57f, 0.32f));
            _dark = GetMaterial("PixelDark", new Color(0.18f, 0.22f, 0.25f));
        }

        private static void CreateHome(Transform parent)
        {
            var home = new GameObject("HOME");
            home.transform.SetParent(parent);
            var floor = GetMaterial("HomeFloor", new Color(0.78f, 0.68f, 0.55f));
            CreateCube("Home Floor", new Vector3(-12f, -0.15f, 0f), new Vector3(10f, 0.3f, 12f), floor, home.transform);
            CreateCube("Home Back Wall", new Vector3(-12f, 1.5f, 5.8f), new Vector3(10f, 3f, 0.3f), _cream, home.transform);
            CreateCube("Home Front Wall", new Vector3(-12f, 1.5f, -5.8f), new Vector3(10f, 3f, 0.3f), _cream, home.transform);
            CreateCube("Home Outer Wall", new Vector3(-16.8f, 1.5f, 0f), new Vector3(0.3f, 3f, 12f), _cream, home.transform);
            CreateCube("Kitchen Table", new Vector3(-13f, 0.45f, 1.4f), new Vector3(3.2f, 0.8f, 1.7f), _wood, home.transform);
            CreateLabel("집", new Vector3(-12f, 3.4f, 5.55f), home.transform);
        }

        private static void CreateStreet(Transform parent)
        {
            var street = new GameObject("STREET");
            street.transform.SetParent(parent);
            var asphalt = GetMaterial("Street", new Color(0.29f, 0.31f, 0.34f));
            CreateCube("Street Floor", new Vector3(0f, -0.2f, 0f), new Vector3(14f, 0.2f, 12f), asphalt, street.transform);
            CreateCube("North Sidewalk", new Vector3(0f, -0.05f, 5.1f), new Vector3(14f, 0.25f, 1.4f), _cream, street.transform);
            CreateCube("South Sidewalk", new Vector3(0f, -0.05f, -5.1f), new Vector3(14f, 0.25f, 1.4f), _cream, street.transform);
            CreateLabel("거리", new Vector3(0f, 0.25f, 4.8f), street.transform);
        }

        private static OfficeLayout CreateOffice(Transform parent)
        {
            var office = new GameObject("FAMILY OFFICE V0.2");
            office.transform.SetParent(parent);
            CreateCube("Office Floor", new Vector3(14f, -0.15f, 0f), new Vector3(16f, 0.3f, 14f), _lightWood, office.transform);
            for (var z = -6; z <= 6; z++)
            {
                CreateCube($"Floor Plank {z}", new Vector3(14f, 0.012f, z), new Vector3(15.8f, 0.018f, 0.025f), _wood, office.transform, false);
            }

            CreateCube("Office Back Wall", new Vector3(14f, 1.5f, 6.8f), new Vector3(16f, 3f, 0.3f), _cream, office.transform);
            CreateCube("Office Front Cutaway Wall", new Vector3(14f, 0.35f, -6.8f), new Vector3(16f, 0.7f, 0.3f), _cream, office.transform);
            CreateCube("Office Outer Wall", new Vector3(21.8f, 1.5f, 0f), new Vector3(0.3f, 3f, 14f), _cream, office.transform);
            CreateCube("Meeting Partition Back", new Vector3(17.25f, 0.9f, 5.2f), new Vector3(0.18f, 1.8f, 3.1f), _mint, office.transform);
            CreateCube("Meeting Partition Front", new Vector3(17.25f, 0.9f, 1.2f), new Vector3(0.18f, 1.8f, 1.6f), _mint, office.transform);

            CreateReception(office.transform);
            CreateWorkstation("Desk A", new Vector3(11.4f, 4.4f), office.transform);
            CreateWorkstation("Desk B", new Vector3(14.6f, 4.4f), office.transform);
            CreateWorkstation("Desk C", new Vector3(11.4f, -2.3f), office.transform);
            CreateWorkstation("Desk D", new Vector3(14.6f, -2.3f), office.transform);
            CreateMeetingNook(office.transform);
            CreateLounge(office.transform);
            CreatePrinterStation(office.transform);
            CreateShelf("Document Shelf", new Vector3(7.3f, 0.9f, 5.7f), office.transform);
            CreatePlant(new Vector3(20.7f, 0f, 5.7f), office.transform);
            CreatePlant(new Vector3(20.7f, 0f, -5.8f), office.transform);
            CreateLabel("우리 가족회사", new Vector3(14f, 3.35f, 6.55f), office.transform);

            var waypointRoot = new GameObject("Office Waypoints");
            waypointRoot.transform.SetParent(office.transform);
            var layout = new OfficeLayout
            {
                Reception = CreateWaypoint("reception", new Vector3(8.5f, 0.05f, -3.35f), OfficeActivity.Reception, 2.5f, 4.5f, waypointRoot.transform),
                CorridorWest = CreateWaypoint("corridor_west", new Vector3(9.5f, 0.05f, 0f), OfficeActivity.Walking, 0f, 0f, waypointRoot.transform),
                CorridorCenter = CreateWaypoint("corridor_center", new Vector3(14.2f, 0.05f, 0f), OfficeActivity.Walking, 0f, 0f, waypointRoot.transform),
                CorridorEast = CreateWaypoint("corridor_east", new Vector3(18.3f, 0.05f, 0f), OfficeActivity.Walking, 0f, 0f, waypointRoot.transform),
                DeskA = CreateWaypoint("desk_a", new Vector3(11.4f, 0.05f, 3.35f), OfficeActivity.Work, 3f, 6f, waypointRoot.transform),
                DeskB = CreateWaypoint("desk_b", new Vector3(14.6f, 0.05f, 3.35f), OfficeActivity.Work, 3f, 6f, waypointRoot.transform),
                DeskC = CreateWaypoint("desk_c", new Vector3(11.4f, 0.05f, -3.35f), OfficeActivity.Work, 3f, 6f, waypointRoot.transform),
                DeskD = CreateWaypoint("desk_d", new Vector3(14.6f, 0.05f, -3.35f), OfficeActivity.Work, 3f, 6f, waypointRoot.transform),
                Printer = CreateWaypoint("printer", new Vector3(8.6f, 0.05f, 3.75f), OfficeActivity.Printing, 1.5f, 3f, waypointRoot.transform),
                Meeting = CreateWaypoint("meeting", new Vector3(18.6f, 0.05f, 3.05f), OfficeActivity.Meeting, 3f, 5f, waypointRoot.transform),
                Lounge = CreateWaypoint("lounge", new Vector3(18.8f, 0.05f, -3.6f), OfficeActivity.Break, 2.5f, 5f, waypointRoot.transform)
            };
            return layout;
        }

        private static void CreateReception(Transform parent)
        {
            CreateCube("Reception Desk", new Vector3(8.5f, 0.55f, -4.65f), new Vector3(3.2f, 1.1f, 1.1f), _wood, parent);
            CreateCube("Reception Top", new Vector3(8.5f, 1.15f, -4.65f), new Vector3(3.4f, 0.15f, 1.25f), _lightWood, parent, false);
            CreateCube("Reception Phone", new Vector3(7.8f, 1.35f, -4.6f), new Vector3(0.45f, 0.18f, 0.3f), _dark, parent, false);
            CreateCube("Reception Pen Cup", new Vector3(9.5f, 1.35f, -4.55f), new Vector3(0.2f, 0.35f, 0.2f), _teal, parent, false);
        }

        private static void CreateWorkstation(string name, Vector3 xz, Transform parent)
        {
            CreateCube(name, new Vector3(xz.x, 0.55f, xz.y), new Vector3(2.5f, 1.0f, 1.2f), _wood, parent);
            CreateCube($"{name} Monitor", new Vector3(xz.x, 1.45f, xz.y + 0.12f), new Vector3(0.85f, 0.72f, 0.32f), _beige, parent, false);
            CreateCube($"{name} Screen", new Vector3(xz.x, 1.47f, xz.y - 0.055f), new Vector3(0.61f, 0.45f, 0.02f), _teal, parent, false);
            CreateCube($"{name} Keyboard", new Vector3(xz.x, 1.11f, xz.y - 0.38f), new Vector3(0.8f, 0.08f, 0.27f), _beige, parent, false);
            CreateCube($"{name} Phone", new Vector3(xz.x - 0.88f, 1.14f, xz.y - 0.2f), new Vector3(0.35f, 0.16f, 0.28f), _dark, parent, false);
            CreateCube($"{name} Chair Seat", new Vector3(xz.x, 0.48f, xz.y - 1.05f), new Vector3(0.72f, 0.18f, 0.72f), _mint, parent, false);
            CreateCube($"{name} Chair Back", new Vector3(xz.x, 0.92f, xz.y - 1.38f), new Vector3(0.72f, 0.85f, 0.16f), _mint, parent, false);
        }

        private static void CreateMeetingNook(Transform parent)
        {
            CreateCube("Meeting Table", new Vector3(19.4f, 0.55f, 4.65f), new Vector3(3.5f, 0.85f, 1.8f), _lightWood, parent);
            CreateCube("Meeting Chair A", new Vector3(18.5f, 0.45f, 3.35f), new Vector3(0.75f, 0.8f, 0.75f), _mint, parent, false);
            CreateCube("Meeting Chair B", new Vector3(20.3f, 0.45f, 3.35f), new Vector3(0.75f, 0.8f, 0.75f), _mint, parent, false);
            CreateCube("Whiteboard", new Vector3(19.4f, 1.65f, 6.55f), new Vector3(2.8f, 1.35f, 0.12f), _beige, parent, false);
        }

        private static void CreateLounge(Transform parent)
        {
            CreateCube("Lounge Sofa Seat", new Vector3(20.1f, 0.42f, -4.9f), new Vector3(2.7f, 0.5f, 1.0f), _peach, parent);
            CreateCube("Lounge Sofa Back", new Vector3(20.1f, 0.9f, -5.35f), new Vector3(2.7f, 1.0f, 0.3f), _peach, parent);
            CreateCube("Coffee Table", new Vector3(18.4f, 0.35f, -4.8f), new Vector3(1.7f, 0.55f, 1.0f), _lightWood, parent);
            CreateCube("Water Dispenser", new Vector3(20.9f, 0.9f, -2.8f), new Vector3(0.7f, 1.8f, 0.65f), _beige, parent);
            CreateCube("Water Bottle", new Vector3(20.9f, 1.95f, -2.8f), new Vector3(0.48f, 0.55f, 0.45f), _teal, parent, false);
            CreateShelf("Snack Shelf", new Vector3(21.35f, 0.9f, -0.9f), parent);
        }

        private static void CreatePrinterStation(Transform parent)
        {
            CreateCube("Printer Cabinet", new Vector3(7.5f, 0.55f, 3.8f), new Vector3(1.2f, 1.1f, 1.2f), _wood, parent);
            CreateCube("Fax Printer", new Vector3(7.5f, 1.35f, 3.8f), new Vector3(1.0f, 0.55f, 0.9f), _beige, parent);
            CreateCube("Printer Tray", new Vector3(7.5f, 1.45f, 3.25f), new Vector3(0.75f, 0.12f, 0.42f), _dark, parent, false);
        }

        private static void CreateShelf(string name, Vector3 position, Transform parent)
        {
            CreateCube(name, position, new Vector3(1.1f, 1.8f, 0.55f), _wood, parent);
            for (var row = 0; row < 3; row++)
            {
                CreateCube($"{name} Shelf {row}", position + new Vector3(0f, -0.55f + row * 0.55f, -0.31f), new Vector3(0.95f, 0.08f, 0.12f), _cream, parent, false);
            }
        }

        private static void CreatePlant(Vector3 position, Transform parent)
        {
            CreateCube("Plant Pot", position + new Vector3(0f, 0.25f, 0f), new Vector3(0.55f, 0.5f, 0.55f), _beige, parent, false);
            var crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            crown.name = "Plant Crown";
            crown.transform.SetParent(parent);
            crown.transform.position = position + new Vector3(0f, 0.85f, 0f);
            crown.transform.localScale = new Vector3(0.85f, 0.95f, 0.85f);
            crown.GetComponent<Renderer>().sharedMaterial = _plant;
            Object.DestroyImmediate(crown.GetComponent<Collider>());
        }

        private static GameObject CreatePlayer(Transform parent)
        {
            var playerMaterial = GetMaterial("Player", new Color(0.24f, 0.55f, 0.92f));
            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player (14) - DIRECT CONTROL";
            player.transform.SetParent(parent);
            player.transform.position = new Vector3(-10.5f, 1f, -1.5f);
            player.GetComponent<Renderer>().sharedMaterial = playerMaterial;
            Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());
            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            player.AddComponent<PrototypePlayerController>();
            CreateLabel("나 · 14살\n직접 이동", player.transform.position + new Vector3(0f, 1.65f, 0f), player.transform);
            return player;
        }

        private static void CreateParentPlaceholders(Transform parent)
        {
            var fatherMaterial = GetMaterial("FatherPlaceholder", new Color(0.32f, 0.43f, 0.64f));
            var motherMaterial = GetMaterial("MotherPlaceholder", new Color(0.79f, 0.45f, 0.55f));
            CreatePlaceholder("Father Placeholder (46)", "아빠 자리 · 46살\n최종 에셋 대기", new Vector3(-14.3f, 1f, -1.5f), fatherMaterial, parent);
            CreatePlaceholder("Mother Placeholder (44)", "엄마 자리 · 44살\n최종 에셋 대기", new Vector3(-12.4f, 1f, 2.9f), motherMaterial, parent);
        }

        private static void CreateSister(Transform parent, OfficeLayout office)
        {
            var frames = LoadSisterFrames();
            var route = new[]
            {
                office.Reception, office.CorridorWest, office.DeskA, office.CorridorWest,
                office.Printer, office.CorridorWest, office.CorridorCenter, office.CorridorEast,
                office.Lounge, office.CorridorEast, office.Meeting, office.CorridorEast,
                office.CorridorCenter, office.CorridorWest
            };
            var sister = new GameObject("Older Sister (20) - PIXEL MOVING");
            sister.transform.SetParent(parent);
            sister.transform.position = office.Reception.transform.position;
            var controller = sister.AddComponent<CharacterController>();
            controller.height = 1.7f;
            controller.radius = 0.28f;
            controller.center = new Vector3(0f, 0.85f, 0f);
            controller.skinWidth = 0.04f;

            var visual = new GameObject("Pixel Visual");
            visual.transform.SetParent(sister.transform, false);
            visual.transform.localPosition = new Vector3(0f, 1.05f, 0f);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = frames[0];
            renderer.sortingOrder = 20;
            renderer.spriteSortPoint = SpriteSortPoint.Pivot;
            visual.AddComponent<BillboardFacingCamera>();
            var animator = sister.AddComponent<DirectionalSpriteAnimator>();
            animator.Configure(renderer, frames, 0.2f);
            var agent = sister.AddComponent<OfficeWorkerAgent>();
            agent.Configure("older_sister", route, 1.65f, 0, animator);
            CreateStatusLabel("누나 · 20살", agent, sister.transform, 2.25f);
        }

        private static void CreateMovingWorkers(Transform parent, OfficeLayout office)
        {
            var blue = GetMaterial("WorkerBlue", new Color(0.32f, 0.64f, 0.82f));
            var coral = GetMaterial("WorkerCoral", new Color(0.88f, 0.49f, 0.39f));
            var routeA = new[]
            {
                office.DeskC, office.CorridorCenter, office.CorridorWest, office.Printer,
                office.CorridorWest, office.CorridorCenter, office.CorridorEast, office.Lounge,
                office.CorridorEast, office.CorridorCenter
            };
            var routeB = new[]
            {
                office.DeskB, office.CorridorCenter, office.CorridorEast, office.Meeting,
                office.CorridorEast, office.CorridorCenter, office.CorridorWest, office.Reception,
                office.CorridorWest, office.CorridorCenter
            };
            CreateWorkerPlaceholder("employee_a", "직원 A", blue, routeA, 0, 1.45f, parent);
            CreateWorkerPlaceholder("employee_b", "직원 B", coral, routeB, 0, 1.55f, parent);
        }

        private static void CreateWorkerPlaceholder(
            string agentId,
            string displayName,
            Material material,
            OfficeWaypoint[] route,
            int startIndex,
            float speed,
            Transform parent)
        {
            var root = new GameObject($"{displayName} - MOVING PLACEHOLDER");
            root.transform.SetParent(parent);
            root.transform.position = route[startIndex].transform.position;
            var controller = root.AddComponent<CharacterController>();
            controller.height = 1.45f;
            controller.radius = 0.3f;
            controller.center = new Vector3(0f, 0.72f, 0f);

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Chibi Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.62f, 0f);
            body.transform.localScale = new Vector3(0.48f, 0.52f, 0.48f);
            body.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(body.GetComponent<Collider>());
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Chibi Head";
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.22f, 0f);
            head.transform.localScale = new Vector3(0.68f, 0.68f, 0.68f);
            head.GetComponent<Renderer>().sharedMaterial = _beige;
            Object.DestroyImmediate(head.GetComponent<Collider>());

            var agent = root.AddComponent<OfficeWorkerAgent>();
            agent.Configure(agentId, route, speed, startIndex);
            CreateStatusLabel(displayName, agent, root.transform, 1.95f);
        }

        private static void CreateCamera(Transform player)
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.58f, 0.76f, 0.80f);
            camera.orthographic = true;
            camera.orthographicSize = 7.2f;
            var offset = new Vector3(-9.5f, 12.5f, -9.5f);
            cameraObject.transform.position = player.position + offset;
            cameraObject.transform.LookAt(player.position + Vector3.up * 0.8f);
            var follow = cameraObject.AddComponent<IsometricCameraFollow>();
            follow.Configure(player, offset, 7.2f);
            cameraObject.AddComponent<PixelatedCameraEffect>().Configure(360);
        }

        private static void CreateLighting()
        {
            var lightObject = new GameObject("Sun");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.05f;
            light.color = new Color(1f, 0.94f, 0.82f);
            lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            RenderSettings.ambientLight = new Color(0.61f, 0.61f, 0.58f);
        }

        private static OfficeWaypoint CreateWaypoint(
            string id,
            Vector3 position,
            OfficeActivity activity,
            float minimumStay,
            float maximumStay,
            Transform parent)
        {
            var waypointObject = new GameObject($"Waypoint - {id}");
            waypointObject.transform.SetParent(parent);
            waypointObject.transform.position = position;
            var waypoint = waypointObject.AddComponent<OfficeWaypoint>();
            waypoint.Configure(id, activity, minimumStay, maximumStay);
            return waypoint;
        }

        private static Sprite[] LoadSisterFrames()
        {
            var result = new Sprite[SisterFrameNames.Length];
            for (var index = 0; index < SisterFrameNames.Length; index++)
            {
                var path = $"{SisterFrameFolder}/{SisterFrameNames[index]}.png";
                result[index] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (result[index] == null)
                {
                    throw new InvalidDataException($"Missing sister pixel frame: {path}");
                }
            }

            return result;
        }

        private static void ConfigureSisterPixelSheet()
        {
            EnsureFolder(SisterFrameFolder);
            AssetDatabase.ImportAsset(SisterPixelSheetPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(SisterPixelSheetPath) as TextureImporter;
            if (importer == null)
            {
                throw new FileNotFoundException("Sister pixel sheet not found.", SisterPixelSheetPath);
            }

            importer.textureType = TextureImporterType.Default;
            importer.isReadable = true;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(SisterPixelSheetPath);
            if (texture == null)
            {
                throw new InvalidDataException("Sister pixel sheet could not be loaded.");
            }

            if (texture.width % 4 != 0 || texture.height % 2 != 0)
            {
                throw new InvalidDataException($"Sister sheet must be a 4x2 grid: {texture.width}x{texture.height}");
            }

            var cellWidth = texture.width / 4;
            var cellHeight = texture.height / 2;
            for (var row = 0; row < 2; row++)
            {
                for (var column = 0; column < 4; column++)
                {
                    var frameIndex = row * 4 + column;
                    var frameName = SisterFrameNames[frameIndex];
                    var pixels = texture.GetPixels(
                        column * cellWidth,
                        texture.height - (row + 1) * cellHeight,
                        cellWidth,
                        cellHeight);
                    var frameTexture = new Texture2D(cellWidth, cellHeight, TextureFormat.RGBA32, false);
                    frameTexture.name = frameName;
                    frameTexture.SetPixels(pixels);
                    frameTexture.Apply(false, false);
                    var framePath = $"{SisterFrameFolder}/{frameName}.png";
                    File.WriteAllBytes(Path.GetFullPath(framePath), frameTexture.EncodeToPNG());
                    Object.DestroyImmediate(frameTexture);
                    AssetDatabase.ImportAsset(framePath, ImportAssetOptions.ForceSynchronousImport);
                    var frameImporter = AssetImporter.GetAtPath(framePath) as TextureImporter;
                    if (frameImporter == null) throw new InvalidDataException($"Frame import failed: {framePath}");
                    frameImporter.textureType = TextureImporterType.Sprite;
                    frameImporter.spriteImportMode = SpriteImportMode.Single;
                    frameImporter.spritePixelsPerUnit = 180f;
                    frameImporter.alphaIsTransparency = true;
                    frameImporter.mipmapEnabled = false;
                    frameImporter.filterMode = FilterMode.Point;
                    frameImporter.textureCompression = TextureImporterCompression.Uncompressed;
                    frameImporter.maxTextureSize = 1024;
                    frameImporter.SaveAndReimport();
                }
            }

            importer = AssetImporter.GetAtPath(SisterPixelSheetPath) as TextureImporter;
            importer.isReadable = false;
            importer.SaveAndReimport();
        }

        private static void CreateStatusLabel(string displayName, OfficeWorkerAgent agent, Transform parent, float height)
        {
            var labelObject = CreateLabel(displayName, parent.position + Vector3.up * height, parent);
            var textMesh = labelObject.GetComponent<TextMesh>();
            textMesh.fontSize = 46;
            textMesh.characterSize = 0.035f;
            labelObject.AddComponent<OfficeStatusLabel>().Configure(displayName, agent, textMesh);
        }

        private static GameObject CreatePlaceholder(string name, string label, Vector3 position, Material material, Transform parent)
        {
            var placeholder = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            placeholder.name = name;
            placeholder.transform.SetParent(parent);
            placeholder.transform.position = position;
            placeholder.GetComponent<Renderer>().sharedMaterial = material;
            CreateLabel(label, position + new Vector3(0f, 1.7f, 0f), placeholder.transform);
            return placeholder;
        }

        private static GameObject CreateCube(
            string name,
            Vector3 position,
            Vector3 scale,
            Material material,
            Transform parent,
            bool withCollider = true)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent);
            cube.transform.position = position;
            cube.transform.localScale = scale;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            if (!withCollider)
            {
                Object.DestroyImmediate(cube.GetComponent<Collider>());
            }

            return cube;
        }

        private static GameObject CreateLabel(string text, Vector3 position, Transform parent)
        {
            var labelObject = new GameObject($"Label - {text.Replace('\n', ' ')}");
            labelObject.transform.SetParent(parent);
            labelObject.transform.position = position;
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

        private sealed class OfficeLayout
        {
            public OfficeWaypoint Reception;
            public OfficeWaypoint CorridorWest;
            public OfficeWaypoint CorridorCenter;
            public OfficeWaypoint CorridorEast;
            public OfficeWaypoint DeskA;
            public OfficeWaypoint DeskB;
            public OfficeWaypoint DeskC;
            public OfficeWaypoint DeskD;
            public OfficeWaypoint Printer;
            public OfficeWaypoint Meeting;
            public OfficeWaypoint Lounge;
        }
    }
}
