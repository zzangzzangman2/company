using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FamilyCompany.Infrastructure.Unity;
using FamilyCompany.Presentation.Unity;
using FamilyCompany.Presentation.Unity.OfficeSeating;
using FamilyCompany.Presentation.Unity.OfficeSeating.Authoring;
using FamilyCompany.Presentation.Unity.OfficeWorkActions;
using FamilyCompany.Simulation.OfficeSeating;
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
        public const string PlayerPixelSheetPath = "Assets/Art/Characters/Player/Pixel/HighMotion/player_pixel_walk8dir6_a_v1.png";
        public const string PlayerFrameFolder = "Assets/Art/Characters/Player/Pixel/HighMotion/Frames";
        public const string SisterPortraitAssetPath = "Assets/Art/Characters/OlderSister/older_sister_casual_neutral_v2.png";
        public const string SisterPixelSheetPath = "Assets/Art/Characters/OlderSister/Pixel/HighMotion/older_sister_pixel_walk8dir6_a_v1.png";
        public const string TitleHeroAssetPath = "Assets/Art/UI/Resources/Title/family_company_title_hero_v1.png";
        public const string KoreaHistoryRegistryAssetPath = "Assets/FamilyCompany/Content/History/company_registry_korea_2000_2026.json";
        public const string SisterFrameFolder = "Assets/Art/Characters/OlderSister/Pixel/HighMotion/Frames";
        private const string MaterialFolder = "Assets/FamilyCompany/Generated/Materials";
        private static readonly string[] PlayerFrameNames =
            HighMotionCharacterArtBuilder.GetFrameNames("player");
        private static readonly string[] SisterFrameNames =
            HighMotionCharacterArtBuilder.GetFrameNames("older_sister");
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
            HighMotionCharacterArtBuilder.Validate();
            OfficePresentationAssetIntegration.EnsureFrameSets();
            CreateMaterials();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Prototype01";

            var systems = new GameObject("Systems");
            var bootstrap = systems.AddComponent<PrototypeBootstrap>();
            bootstrap.InitializeNow();
            var historyRegistry = AssetDatabase.LoadAssetAtPath<TextAsset>(KoreaHistoryRegistryAssetPath);
            if (historyRegistry == null)
                throw new InvalidOperationException($"Missing Korea History V1 registry: {KoreaHistoryRegistryAssetPath}");
            systems.AddComponent<KoreaHistoryV1RuntimeCatalog>().Configure(historyRegistry);

            var environment = new GameObject("Environment");
            CreateHome(environment.transform);
            CreateStreet(environment.transform);
            var officeLayout = CreateOffice(environment.transform);

            var characters = new GameObject("Characters");
            var player = CreatePlayer(characters.transform);
            player.AddComponent<PlayerOfficeWorkInteractor>().Configure(bootstrap, officeLayout.AllWaypoints);
            CreateSister(characters.transform, officeLayout);
            CreateMovingFamilyMembers(characters.transform, officeLayout);
            var seating = CreateOfficeSeatingRuntime(officeLayout);
            ConfigureOfficeSeatingAnimations(player, characters);
            var coordinator = systems.AddComponent<OfficeContractTaskCoordinator>();
            coordinator.Configure(
                bootstrap,
                characters.GetComponentsInChildren<OfficeWorkerAgent>(),
                officeLayout.AllWaypoints);
            var autonomyCoordinator = systems.AddComponent<OfficeAutonomyCoordinator>();
            autonomyCoordinator.Configure(
                bootstrap,
                characters.GetComponentsInChildren<OfficeWorkerAgent>(),
                officeLayout.AllWaypoints);
            autonomyCoordinator.ConfigureSeatingRuntime(seating.Registry, seating.State);
            autonomyCoordinator.InitializeNow();
            var cameraFollow = CreateCamera(player.transform);
            cameraFollow.ConfigureOfficeFraming(new Vector3(14f, 0f, 0f), new Vector2(16f, 14f), 6.6f);
            officeLayout.Root.gameObject.AddComponent<OfficeVisualV2Presenter>().Configure(
                player.transform,
                characters.transform,
                environment.transform,
                new Vector3(14f, 0f, 0f),
                new Vector2(16f, 14f));
            CreateLighting();
            OfficeSeatingBuilderValidation.ValidateCurrentScene();

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
                Root = office.transform,
                Reception = CreateCalibratedWaypoint("reception", OfficeVisualV2Calibration.ReceptionArt, OfficeActivity.Reception, 2.5f, 4.5f, waypointRoot.transform),
                CorridorWest = CreateCalibratedWaypoint("corridor_west", OfficeVisualV2Calibration.CorridorWestArt, OfficeActivity.Walking, 0f, 0f, waypointRoot.transform),
                CorridorCenter = CreateCalibratedWaypoint("corridor_center", OfficeVisualV2Calibration.CorridorCenterArt, OfficeActivity.Walking, 0f, 0f, waypointRoot.transform),
                // The calibrated east art point lies beyond the front meeting partition.
                // Keep navigation on the verified west-side staging lane and project only
                // the character visual to the independently measured art anchor.
                CorridorEast = CreateWaypoint("corridor_east", new Vector3(16.3f, 0.05f, -1.25f), OfficeActivity.Walking, 0f, 0f, waypointRoot.transform),
                DeskA = CreateCalibratedWaypoint("desk_a", OfficeVisualV2Calibration.DeskAApproachArt, OfficeActivity.Work, 3f, 6f, waypointRoot.transform),
                DeskB = CreateCalibratedWaypoint("desk_b", OfficeVisualV2Calibration.DeskBApproachArt, OfficeActivity.Work, 3f, 6f, waypointRoot.transform),
                // Desk C's exact inverse art point overlaps the legacy reception collider.
                // Keep its controller in the narrow verified aisle and project only the visual
                // foot to the independently measured (650,820) anchor.
                DeskC = CreateWaypoint("desk_c", new Vector3(9.9f, 0.05f, -3.7f), OfficeActivity.Work, 3f, 6f, waypointRoot.transform),
                DeskD = CreateCalibratedWaypoint("desk_d", OfficeVisualV2Calibration.DeskDApproachArt, OfficeActivity.Work, 3f, 6f, waypointRoot.transform),
                Printer = CreateCalibratedWaypoint("printer", OfficeVisualV2Calibration.PrinterArt, OfficeActivity.Printing, 1.5f, 3f, waypointRoot.transform),
                Meeting = CreateCalibratedWaypoint("meeting", OfficeVisualV2Calibration.MeetingArt, OfficeActivity.Meeting, 3f, 5f, waypointRoot.transform),
                Lounge = CreateCalibratedWaypoint("lounge", OfficeVisualV2Calibration.LoungeSafeArt, OfficeActivity.Break, 2.5f, 5f, waypointRoot.transform),
                Exit = CreateCalibratedWaypoint("office_exit", OfficeVisualV2Calibration.ExitArt, OfficeActivity.Outside, 0f, 0f, waypointRoot.transform)
            };
            layout.CorridorEast.ConfigureArtAnchor(OfficeVisualV2Calibration.CorridorEastArt);
            layout.DeskC.ConfigureArtAnchor(OfficeVisualV2Calibration.DeskCApproachArt);
            layout.DeskCStaging = CreateWaypoint("desk_c_staging", new Vector3(9.7f, 0.05f, -1.25f), OfficeActivity.Walking, 0f, 0f, waypointRoot.transform);
            layout.DeskCSide = CreateWaypoint("desk_c_side", new Vector3(9.7f, 0.05f, -3.5f), OfficeActivity.Walking, 0f, 0f, waypointRoot.transform);
            layout.DeskDStaging = CreateWaypoint("desk_d_staging", new Vector3(16.3f, 0.05f, -1.25f), OfficeActivity.Walking, 0f, 0f, waypointRoot.transform);
            layout.DeskDSide = CreateWaypoint("desk_d_side", new Vector3(16.3f, 0.05f, -4.35f), OfficeActivity.Walking, 0f, 0f, waypointRoot.transform);
            layout.ReceptionSide = CreateWaypoint("reception_side", new Vector3(16.3f, 0.05f, -6.15f), OfficeActivity.Walking, 0f, 0f, waypointRoot.transform);
            layout.ExitApproach = CreateWaypoint("exit_approach", new Vector3(6.2f, 0.05f, 1.9f), OfficeActivity.Walking, 0f, 0f, waypointRoot.transform);
            layout.DeskC.ConfigureApproach(layout.DeskCStaging, layout.DeskCSide);
            layout.DeskD.ConfigureApproach(layout.DeskDStaging, layout.DeskDSide);
            layout.Reception.ConfigureApproach(layout.DeskDStaging, layout.DeskDSide, layout.ReceptionSide);
            layout.Lounge.ConfigureApproach(layout.DeskDStaging, layout.DeskDSide);
            layout.Exit.ConfigureApproach(layout.ExitApproach);
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
            var frames = LoadPlayerFrames();
            var player = new GameObject("Player (14) - PIXEL DIRECT CONTROL");
            player.transform.SetParent(parent);
            player.transform.position = new Vector3(-10.5f, 0.05f, -1.5f);
            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.45f;
            controller.radius = 0.3f;
            controller.center = new Vector3(0f, 0.72f, 0f);
            controller.skinWidth = 0.04f;

            var visual = new GameObject("Pixel Visual");
            visual.transform.SetParent(player.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = frames[DirectionalSpriteAnimator.DirectionCount * 2];
            renderer.sortingOrder = 22;
            renderer.spriteSortPoint = SpriteSortPoint.Pivot;
            visual.AddComponent<BillboardFacingCamera>();
            var animator = player.AddComponent<DirectionalSpriteAnimator>();
            animator.Configure(renderer, frames, 0.11f);
            player.AddComponent<PrototypePlayerController>();
            CreateLabel("나 · 14살\n직접 이동", player.transform.position + new Vector3(0f, 2.3f, 0f), player.transform);
            return player;
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
            visual.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = frames[DirectionalSpriteAnimator.DirectionCount * 2];
            renderer.sortingOrder = 20;
            renderer.spriteSortPoint = SpriteSortPoint.Pivot;
            visual.AddComponent<BillboardFacingCamera>();
            var animator = sister.AddComponent<DirectionalSpriteAnimator>();
            animator.Configure(renderer, frames, 0.11f);
            var agent = sister.AddComponent<OfficeWorkerAgent>();
            agent.Configure("older_sister", route, 1.65f, 0, animator);
            CreateStatusLabel("누나 · 20살", agent, sister.transform, 2.25f);
        }

        private static void CreateMovingFamilyMembers(Transform parent, OfficeLayout office)
        {
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
            CreateFamilyWorker("father", "아빠 · 46살", routeA, 0, 1.45f, parent);
            CreateFamilyWorker("mother", "엄마 · 44살", routeB, 0, 1.55f, parent);
        }

        private static OfficeSeatingBuildResult CreateOfficeSeatingRuntime(OfficeLayout office)
        {
            var root = new GameObject("Office Seating Runtime");
            root.transform.SetParent(office.Root, false);
            var seats = new[]
            {
                CreateOfficeSeat(
                    "desk_a", "업무 책상 A · 누나",
                    OfficeVisualV2Calibration.DeskAApproachArt,
                    OfficeVisualV2Calibration.DeskASitArt,
                    OfficeVisualV2Calibration.DeskAMonitorArt,
                    office.DeskA, "older_sister", root.transform),
                CreateOfficeSeat(
                    "desk_b", "업무 책상 B · 엄마",
                    OfficeVisualV2Calibration.DeskBApproachArt,
                    OfficeVisualV2Calibration.DeskBSitArt,
                    OfficeVisualV2Calibration.DeskBMonitorArt,
                    office.DeskB, "mother", root.transform),
                CreateOfficeSeat(
                    "desk_c", "업무 책상 C · 아빠",
                    OfficeVisualV2Calibration.DeskCApproachArt,
                    OfficeVisualV2Calibration.DeskCSitArt,
                    OfficeVisualV2Calibration.DeskCMonitorArt,
                    office.DeskC, "father", root.transform),
                CreateOfficeSeat(
                    "desk_d", "업무 책상 D · 플레이어",
                    OfficeVisualV2Calibration.DeskDApproachArt,
                    OfficeVisualV2Calibration.DeskDSitArt,
                    OfficeVisualV2Calibration.DeskDMonitorArt,
                    office.DeskD, "player", root.transform)
            };

            var registry = root.AddComponent<OfficeSeatRegistry>();
            registry.Configure(seats);
            registry.Rebuild();
            if (registry.SeatCount != 4)
                throw new InvalidOperationException($"Office seating registry requires four valid desks, got {registry.SeatCount}.");

            var definitions = registry.Definitions.Select(item =>
                new FamilyCompany.Simulation.OfficeSeating.OfficeSeatDefinition(
                    item.SeatId,
                    new FamilyCompany.Simulation.OfficeSeating.OfficeSeatPosition(
                        item.SitPosition.X,
                        item.SitPosition.Z)));
            var state = new OfficeSeatingState(definitions);
            AssignSeat(state, "desk_a", "older_sister");
            AssignSeat(state, "desk_b", "mother");
            AssignSeat(state, "desk_c", "father");
            AssignSeat(state, "desk_d", "player");
            return new OfficeSeatingBuildResult(registry, state);
        }

        private static OfficeSeatAuthoring CreateOfficeSeat(
            string seatId,
            string displayName,
            Vector2 approachArt,
            Vector2 sitArt,
            Vector2 monitorArt,
            OfficeWaypoint semanticDestination,
            string longTermAssignedMemberId,
            Transform parent)
        {
            var root = new GameObject("Office Seat - " + seatId);
            root.transform.SetParent(parent, false);
            root.transform.position = OfficeVisualV2Calibration.ArtPixelToWorld(sitArt);
            var approach = CreateSeatAnchor("Approach Anchor", approachArt, root.transform);
            var sit = CreateSeatAnchor("Sit Anchor", sitArt, root.transform);
            var look = CreateSeatAnchor("Computer Look Target", monitorArt, root.transform);
            var hotspot = root.AddComponent<BoxCollider>();
            hotspot.isTrigger = true;
            hotspot.center = new Vector3(0f, 0.65f, 0f);
            hotspot.size = new Vector3(0.90f, 1.30f, 0.90f);

            var authoring = root.AddComponent<OfficeSeatAuthoring>();
            authoring.Configure(
                seatId,
                approach,
                sit,
                look,
                hotspot,
                OfficeSeatForegroundOcclusionMode.BehindForeground,
                false,
                OfficeSeatFacing8.North,
                displayName,
                semanticDestination);
            if (!authoring.TryResolveFacing(out var facing))
                throw new InvalidOperationException($"Office seat '{seatId}' cannot resolve ComputerLookTarget facing.");
            authoring.Configure(
                seatId,
                approach,
                sit,
                look,
                hotspot,
                OfficeSeatForegroundOcclusionMode.BehindForeground,
                true,
                facing,
                displayName,
                semanticDestination,
                longTermAssignedMemberId);
            return authoring;
        }

        private static Transform CreateSeatAnchor(string name, Vector2 artPixel, Transform parent)
        {
            var anchor = new GameObject(name).transform;
            anchor.SetParent(parent);
            anchor.position = OfficeVisualV2Calibration.ArtPixelToWorld(artPixel);
            return anchor;
        }

        private static void AssignSeat(OfficeSeatingState state, string seatId, string memberId)
        {
            if (!state.TryAssign(seatId, memberId, out var result))
                throw new InvalidOperationException(
                    $"Failed to assign office seat '{seatId}' to '{memberId}': {result.Failure}.");
        }

        private static void ConfigureOfficeSeatingAnimations(GameObject player, GameObject characters)
        {
            const string actionFrameSetRoot = "Assets/FamilyCompany/Content/OfficeWorkActions";
            var bindings = new Dictionary<string, DirectionalSpriteAnimator>(StringComparer.Ordinal)
            {
                { "player", player.GetComponent<DirectionalSpriteAnimator>() }
            };
            foreach (var agent in characters.GetComponentsInChildren<OfficeWorkerAgent>())
            {
                if (!bindings.TryAdd(agent.AgentId, agent.SpriteAnimator))
                    throw new InvalidOperationException("Duplicate family seating animator: " + agent.AgentId);
            }
            foreach (var memberId in new[] { "player", "older_sister", "father", "mother" })
            {
                if (!bindings.TryGetValue(memberId, out var animator) || animator == null)
                    throw new InvalidOperationException("Missing family seating animator: " + memberId);
                animator.ConfigureOfficeSeating(
                    LoadOfficeSeatingFrames(memberId, OfficeSeatingAnimationClip.SitDown),
                    LoadOfficeSeatingFrames(memberId, OfficeSeatingAnimationClip.Work),
                    LoadOfficeSeatingFrames(memberId, OfficeSeatingAnimationClip.StandUp));

                var frameSetPath = $"{actionFrameSetRoot}/{memberId}_office_work_actions.asset";
                var frameSet = AssetDatabase.LoadAssetAtPath<OfficeWorkActionFrameSet>(frameSetPath);
                if (frameSet == null)
                {
                    animator.ConfigureOfficeWorkAnimationHook(null);
                    continue;
                }

                var bootstrap = Object.FindFirstObjectByType<PrototypeBootstrap>();
                if (bootstrap == null)
                    throw new InvalidOperationException("PrototypeBootstrap is required for office work micro-actions.");
                var adapter = animator.GetComponent<OfficeSeatedWorkMicroActionAdapter>();
                if (adapter == null)
                    adapter = animator.gameObject.AddComponent<OfficeSeatedWorkMicroActionAdapter>();
                adapter.Configure(bootstrap, memberId, frameSet);
                animator.ConfigureOfficeWorkAnimationHook(adapter);
            }

            if (player.GetComponent<OfficePlayerSeatingPresenter>() == null)
                player.AddComponent<OfficePlayerSeatingPresenter>();
        }

        private static Sprite[] LoadOfficeSeatingFrames(
            string memberId,
            OfficeSeatingAnimationClip clip)
        {
            var frameCount = OfficeSeatingAnimationFrames.FrameCount(clip);
            var result = new Sprite[frameCount * OfficeSeatingAnimationFrames.DirectionCount];
            for (var frame = 0; frame < frameCount; frame++)
            {
                for (var direction = 0; direction < OfficeSeatingAnimationFrames.DirectionCount; direction++)
                {
                    var index = OfficeSeatingAnimationFrames.FlattenedIndex(clip, direction, frame);
                    var path = OfficeSeatingAnimationFrames.AssetPath(
                        memberId,
                        (OfficeSeatFacing8)direction,
                        clip,
                        frame);
                    result[index] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    if (result[index] == null)
                        throw new InvalidDataException("Missing office seating frame: " + path);
                }
            }
            return result;
        }

        private static void CreateFamilyWorker(
            string agentId,
            string displayName,
            OfficeWaypoint[] route,
            int startIndex,
            float speed,
            Transform parent)
        {
            var frames = LoadFrames(
                HighMotionCharacterArtBuilder.GetFrameFolder(agentId),
                HighMotionCharacterArtBuilder.GetFrameNames(agentId),
                agentId);
            var root = new GameObject($"{displayName} - PIXEL MOVING");
            root.transform.SetParent(parent);
            root.transform.position = route[startIndex].transform.position;
            var controller = root.AddComponent<CharacterController>();
            controller.height = 1.7f;
            controller.radius = 0.3f;
            controller.center = new Vector3(0f, 0.85f, 0f);
            controller.skinWidth = 0.04f;

            var visual = new GameObject("Pixel Visual");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = frames[DirectionalSpriteAnimator.DirectionCount * 2];
            renderer.sortingOrder = 20;
            renderer.spriteSortPoint = SpriteSortPoint.Pivot;
            visual.AddComponent<BillboardFacingCamera>();
            var animator = root.AddComponent<DirectionalSpriteAnimator>();
            animator.Configure(renderer, frames, 0.11f);

            var agent = root.AddComponent<OfficeWorkerAgent>();
            agent.Configure(agentId, route, speed, startIndex, animator);
            CreateStatusLabel(displayName, agent, root.transform, 1.75f);
        }

        private static IsometricCameraFollow CreateCamera(Transform player)
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
            cameraObject.AddComponent<PixelatedCameraEffect>().ConfigureAdaptive(360, 540);
            return follow;
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

        private static OfficeWaypoint CreateCalibratedWaypoint(
            string id,
            Vector2 artPixel,
            OfficeActivity activity,
            float minimumStay,
            float maximumStay,
            Transform parent)
        {
            var waypoint = CreateWaypoint(
                id,
                OfficeVisualV2Calibration.ArtPixelToWorld(artPixel),
                activity,
                minimumStay,
                maximumStay,
                parent);
            waypoint.ConfigureArtAnchor(artPixel);
            return waypoint;
        }

        private static Sprite[] LoadSisterFrames()
        {
            return LoadFrames(SisterFrameFolder, SisterFrameNames, "sister");
        }

        private static Sprite[] LoadPlayerFrames()
        {
            return LoadFrames(PlayerFrameFolder, PlayerFrameNames, "player");
        }

        private static Sprite[] LoadFrames(string folder, string[] frameNames, string label)
        {
            var result = new Sprite[frameNames.Length];
            for (var index = 0; index < frameNames.Length; index++)
            {
                var path = $"{folder}/{frameNames[index]}.png";
                result[index] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (result[index] == null)
                {
                    throw new InvalidDataException($"Missing {label} pixel frame: {path}");
                }
            }

            return result;
        }

        private static void CreateStatusLabel(string displayName, OfficeWorkerAgent agent, Transform parent, float height)
        {
            var labelObject = CreateLabel(displayName, parent.position + Vector3.up * height, parent);
            var textMesh = labelObject.GetComponent<TextMesh>();
            textMesh.fontSize = 46;
            textMesh.characterSize = 0.035f;
            labelObject.AddComponent<OfficeStatusLabel>().Configure(displayName, agent, textMesh);
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
            public Transform Root;
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
            public OfficeWaypoint Exit;
            public OfficeWaypoint DeskCStaging;
            public OfficeWaypoint DeskCSide;
            public OfficeWaypoint DeskDStaging;
            public OfficeWaypoint DeskDSide;
            public OfficeWaypoint ReceptionSide;
            public OfficeWaypoint ExitApproach;

            public OfficeWaypoint[] AllWaypoints => new[]
            {
                Reception, CorridorWest, CorridorCenter, CorridorEast,
                DeskA, DeskB, DeskC, DeskD, Printer, Meeting, Lounge, Exit,
                DeskCStaging, DeskCSide, DeskDStaging, DeskDSide, ReceptionSide, ExitApproach
            };
        }

        private sealed class OfficeSeatingBuildResult
        {
            public OfficeSeatingBuildResult(OfficeSeatRegistry registry, OfficeSeatingState state)
            {
                Registry = registry;
                State = state;
            }

            public OfficeSeatRegistry Registry { get; }
            public OfficeSeatingState State { get; }
        }
    }
}
