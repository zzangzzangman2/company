using System;
using System.Collections;
using System.IO;
using FamilyCompany.Presentation.Unity.OfficeGridView;
using FamilyCompany.Simulation.Game;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime.Qa
{
    /// <summary>
    /// Player proof that the V31 workstation set, not any retired Sprite, renders in all four
    /// production shop rotations on the authoritative grid.
    /// </summary>
    public sealed class OfficeV31WorkstationVisualPlayerQa : MonoBehaviour
    {
        public const string CommandLineFlag = "-familyCompanyOfficeV31WorkstationVisualQa";
        public const string ArtifactDirectoryArgument =
            "-familyCompanyOfficeV31WorkstationVisualArtifacts";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInstall()
        {
            if (!HasFlag(CommandLineFlag) ||
                Object.FindFirstObjectByType<OfficeV31WorkstationVisualPlayerQa>() != null) return;
            var host = new GameObject("~OfficeV31WorkstationVisualPlayerQa");
            DontDestroyOnLoad(host);
            host.AddComponent<OfficeV31WorkstationVisualPlayerQa>();
        }

        private IEnumerator Start()
        {
            string directory = ArgumentValue(ArtifactDirectoryArgument);
            if (string.IsNullOrWhiteSpace(directory))
                directory = Path.Combine(
                    Application.persistentDataPath,
                    "OfficeV31WorkstationVisualPlayerQa");
            Directory.CreateDirectory(directory);

            PrototypeBootstrap bootstrap = Object.FindFirstObjectByType<PrototypeBootstrap>();
            if (bootstrap == null)
            {
                Finish(directory, false, "PrototypeBootstrap missing");
                yield break;
            }

            ScenePreviewJump.ShowStarterOffice();
            bootstrap.StartNewGameNow(1, false);
            ScenePreviewJump.ShowStarterOffice();
            float deadline = Time.realtimeSinceStartup + 30f;
            StarterOfficeRuntimeBootstrap runtime = null;
            while (Time.realtimeSinceStartup < deadline)
            {
                runtime = Object.FindFirstObjectByType<StarterOfficeRuntimeBootstrap>();
                if (runtime != null && runtime.IsReady && runtime.World != null &&
                    bootstrap.State != null) break;
                yield return null;
            }
            if (runtime == null || !runtime.IsReady || runtime.World == null || bootstrap.State == null)
            {
                Finish(directory, false, "Starter office runtime did not become ready");
                yield break;
            }
            // Let ScenePreviewJump finish its loading-overlay completion phase before deliberately
            // toggling IsReady for the QA layout rebuild; otherwise its independent UI coroutine
            // can report a false setup failure while this valid rebuild is in progress.
            yield return new WaitForSecondsRealtime(3.5f);

            GameState state = bootstrap.State;
            OfficeGridCoordinate[] seats =
            {
                new OfficeGridCoordinate(4, 4),
                new OfficeGridCoordinate(9, 4),
                new OfficeGridCoordinate(9, 9),
                new OfficeGridCoordinate(4, 9)
            };
            for (var direction = 0; direction < 4; direction++)
            {
                string instanceId = "qa-v31-workstation-" + direction;
                OfficeFurnitureCommandResult result =
                    OfficeFurnitureTransactionService.PurchaseAndPlaceWorkstation(
                        state,
                        "qa-v31-workstation-purchase-" + direction,
                        instanceId,
                        seats[direction],
                        (OfficeFurnitureFacing)direction);
                if (!result.Success)
                {
                    Finish(
                        directory,
                        false,
                        "direction=" + direction + " purchase=" + result.Failure + ":" + result.Message);
                    yield break;
                }
            }

            runtime.ApplyLayoutForQa(state.OfficeGrid);
            deadline = Time.realtimeSinceStartup + 30f;
            do
            {
                yield return null;
            } while (runtime.IsPreparing && Time.realtimeSinceStartup < deadline);
            if (!runtime.IsReady)
            {
                Finish(directory, false, "four-direction layout rebuild did not become ready");
                yield break;
            }
            for (var frame = 0; frame < 4; frame++) yield return null;

            TextAsset bakeManifest = Resources.Load<TextAsset>(
                "OfficeBuildFurniture/v31_workstation_sprite_manifest");
            if (bakeManifest == null ||
                bakeManifest.text.IndexOf("meshAxes=orthogonal-90deg", StringComparison.Ordinal) < 0 ||
                bakeManifest.text.IndexOf(
                    "projectedTileBasisPx=160,80|-160,80",
                    StringComparison.Ordinal) < 0)
            {
                Finish(
                    directory,
                    false,
                    "V31 directional sprites were not baked from orthogonal true-isometric geometry");
                yield break;
            }

            string[] deskSpriteNames =
            {
                "desk_with_pc_se", "desk_with_pc_sw", "desk_with_pc_nw", "desk_with_pc_ne"
            };
            string[] chairSpriteNames =
            {
                "swivel_chair_nw", "swivel_chair_ne", "swivel_chair_se", "swivel_chair_sw"
            };
            foreach (OfficeFurnitureFacing facing in Enum.GetValues(typeof(OfficeFurnitureFacing)))
            {
                int direction = (int)facing;
                if (!OfficeBuildFurnitureVisualLibrary.TryResolve(
                        runtime.World.FurniturePresenter.VisualCatalog,
                        OfficeGridLayouts.DeskWithPcKind,
                        facing,
                        out var desk,
                        out bool deskFlip) ||
                    !OfficeBuildFurnitureVisualLibrary.TryResolve(
                        runtime.World.FurniturePresenter.VisualCatalog,
                        OfficeGridLayouts.SwivelChairKind,
                        (OfficeFurnitureFacing)(((int)facing + 2) & 3),
                        out var chair,
                        out bool chairFlip) ||
                    deskFlip || chairFlip || desk.BaseSprite == null || chair.BaseSprite == null ||
                    !string.Equals(desk.BaseSprite.name, deskSpriteNames[direction], StringComparison.Ordinal) ||
                    !string.Equals(chair.BaseSprite.name, chairSpriteNames[direction], StringComparison.Ordinal))
                {
                    Finish(directory, false, "directional V31 resource resolution failed: " + facing);
                    yield break;
                }
            }

            Camera camera = Camera.main;
            if (camera == null)
            {
                Finish(directory, false, "starter office camera missing");
                yield break;
            }
            float maximumTileCornerErrorPx = 0f;
            for (var direction = 0; direction < 4; direction++)
            {
                string deskId = "qa-v31-workstation-" + direction;
                string chairId = OfficeFurnitureTransactionService.WorkstationChairInstanceId(deskId);
                string[] memberIds = { deskId, chairId };
                for (var member = 0; member < memberIds.Length; member++)
                {
                    string furnitureId = memberIds[member];
                    if (!runtime.World.FurniturePresenter.TryGetFurniture(
                            furnitureId, out PlacedOfficeFurniture placed) ||
                        !runtime.World.FurniturePresenter.TryGetRenderer(
                            furnitureId, out SpriteRenderer renderer) ||
                        !runtime.World.FurniturePresenter.TryGetDefinition(
                            furnitureId, out var definition))
                    {
                        Finish(directory, false, "missing rendered workstation member: " + furnitureId);
                        yield break;
                    }
                    Vector3[] expected = runtime.World.Presenter.FootprintCornersWorld(placed);
                    float error = OfficeGridAlignmentMetrics.Maximum(
                        OfficeGridAlignmentMetrics.FootprintCornerErrorsPx(
                            camera,
                            renderer,
                            definition,
                            expected));
                    maximumTileCornerErrorPx = Mathf.Max(maximumTileCornerErrorPx, error);
                }
            }
            if (maximumTileCornerErrorPx > 0.01f)
            {
                Finish(
                    directory,
                    false,
                    "workstation art is off the authoritative tile footprint: " +
                    maximumTileCornerErrorPx.ToString("F4") + "px");
                yield break;
            }

            OfficeRuntimeAgent player = null;
            foreach (OfficeRuntimeAgent actor in runtime.Actors)
                if (actor != null && string.Equals(
                        actor.AgentId,
                        "player",
                        StringComparison.Ordinal))
                {
                    player = actor;
                    break;
                }
            if (player == null || state.OfficeGrid.SeatSlots.Count == 0)
            {
                Finish(directory, false, "production Player V8 or workstation seat missing");
                yield break;
            }
            OfficeSeatSlot targetSeat = state.OfficeGrid.SeatSlots[0];
            if (!player.QaBeginSeatedWorkAtSeat(targetSeat.SeatId, "player-v8-production"))
            {
                Finish(directory, false, "Player V8 seated-work route was rejected");
                yield break;
            }
            deadline = Time.realtimeSinceStartup + 30f;
            while (Time.realtimeSinceStartup < deadline &&
                   player.Phase != OfficeRuntimeAgentPhase.Working)
                yield return null;
            if (player.Phase != OfficeRuntimeAgentPhase.Working)
            {
                Finish(
                    directory,
                    false,
                    "Player V8 did not reach Working; phase=" + player.Phase);
                yield break;
            }
            // The production sit blend is measured in real seconds, not rendered frame count.
            // A hidden Player can produce 45 frames in far less than 0.42 s and would otherwise
            // capture the actor still descending in front of the chair while already in Working.
            yield return new WaitForSecondsRealtime(0.65f);
            for (var frame = 0; frame < 3; frame++)
                yield return null;

            GameObject productionHost = GameObject.Find("~Family3DProductionPresenter");
            GameObject productionActor = GameObject.Find("PlayerV8ProductionHost");
            GameObject productionWorkstation = GameObject.Find(
                "V31_AtomicWorkstationSet_OriginalChair_" + targetSeat.SeatId);
            var workstationCount = 0;
            foreach (Transform candidate in Object.FindObjectsByType<Transform>(
                         FindObjectsInactive.Exclude,
                         FindObjectsSortMode.None))
                if (candidate != null && candidate.name.StartsWith(
                        "V31_AtomicWorkstationSet_OriginalChair_",
                        StringComparison.Ordinal))
                    workstationCount++;
            Animator productionAnimator = productionActor == null
                ? null
                : productionActor.GetComponentInChildren<Animator>();
            Transform chairPivot = productionWorkstation == null
                ? null
                : productionWorkstation.transform.Find("Chair_SwivelPivot");
            Transform leftUpperLeg = productionAnimator == null
                ? null
                : productionAnimator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            Transform leftLowerLeg = productionAnimator == null
                ? null
                : productionAnimator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            Transform leftFoot = productionAnimator == null
                ? null
                : productionAnimator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rightUpperLeg = productionAnimator == null
                ? null
                : productionAnimator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            Transform rightLowerLeg = productionAnimator == null
                ? null
                : productionAnimator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            Transform rightFoot = productionAnimator == null
                ? null
                : productionAnimator.GetBoneTransform(HumanBodyBones.RightFoot);
            if (productionHost == null || productionActor == null ||
                productionWorkstation == null || workstationCount != 4 ||
                chairPivot == null || leftUpperLeg == null || leftLowerLeg == null ||
                leftFoot == null || rightUpperLeg == null || rightLowerLeg == null ||
                rightFoot == null)
            {
                Finish(
                    directory,
                    false,
                    "production Player/workstation binding is incomplete; sets=" +
                    workstationCount);
                yield break;
            }

            float leftKneeBend = Vector3.Angle(
                leftUpperLeg.position - leftLowerLeg.position,
                leftFoot.position - leftLowerLeg.position);
            float rightKneeBend = Vector3.Angle(
                rightUpperLeg.position - rightLowerLeg.position,
                rightFoot.position - rightLowerLeg.position);
            const float approvedHeight = 1.857258558f;
            Vector3 seatedOffset = productionActor.transform.position - chairPivot.position;
            seatedOffset.y = 0f;
            float expectedSeatedOffset = 0.07f * approvedHeight;
            if (leftKneeBend < 80f || leftKneeBend > 140f ||
                rightKneeBend < 80f || rightKneeBend > 140f ||
                Mathf.Abs(seatedOffset.magnitude - expectedSeatedOffset) > 0.001f)
            {
                Finish(
                    directory,
                    false,
                    "Player V8 seated-body gate failed: knee=" +
                    leftKneeBend.ToString("F2") + "/" + rightKneeBend.ToString("F2") +
                    " seatOffset=" + seatedOffset.magnitude.ToString("F5") +
                    " expected=" + expectedSeatedOffset.ToString("F5"));
                yield break;
            }
            int visibleRetiredRenderers =
                IsVisible(player.PresentationRenderer) ? 1 : 0;
            if (IsVisible(player.SeatedUpperBodyProtectionRenderer))
                visibleRetiredRenderers++;
            foreach (OfficeSeatSlot seat in state.OfficeGrid.SeatSlots)
            {
                visibleRetiredRenderers += CountVisibleFurnitureRenderers(
                    runtime.World.FurniturePresenter,
                    seat.WorkSurfaceFurnitureId);
                visibleRetiredRenderers += CountVisibleFurnitureRenderers(
                    runtime.World.FurniturePresenter,
                    seat.ChairFurnitureId);
            }
            if (visibleRetiredRenderers != 0)
            {
                Finish(
                    directory,
                    false,
                    "retired Player/workstation sprite renderer visible=" +
                    visibleRetiredRenderers);
                yield break;
            }

            string screenshot = Path.Combine(directory, "v31-workstation-four-directions.png");
            if (!TryCaptureOverview(screenshot, out string captureFailure))
            {
                Finish(directory, false, "four-direction capture failed: " + captureFailure);
                yield break;
            }

            Finish(
                directory,
                true,
                "sets=4 directionalDesk=4 directionalChair=4 legacyFlip=0 meshAxes=90deg " +
                "tileBasis=160x80 seats=" +
                state.OfficeGrid.SeatSlots.Count + " furniture=" + state.OfficeGrid.Furniture.Count +
                " maxTileCornerError=" + maximumTileCornerErrorPx.ToString("F4") +
                "px playerPhase=" + player.Phase +
                " knee=" + leftKneeBend.ToString("F2") + "/" +
                rightKneeBend.ToString("F2") +
                " seatOffset=" + seatedOffset.magnitude.ToString("F5") +
                " retiredVisible=0");
        }

        private static int CountVisibleFurnitureRenderers(
            OfficeGridFurniturePresenter presenter,
            string furnitureId)
        {
            var count = 0;
            if (presenter.TryGetRenderer(furnitureId, out SpriteRenderer baseRenderer) &&
                IsVisible(baseRenderer))
                count++;
            if (presenter.FrontOverlayRenderers.TryGetValue(
                    furnitureId,
                    out SpriteRenderer frontRenderer) &&
                IsVisible(frontRenderer))
                count++;
            if (presenter.OccupiedChairLowerBodyRenderers.TryGetValue(
                    furnitureId,
                    out SpriteRenderer lowerRenderer) &&
                IsVisible(lowerRenderer))
                count++;
            return count;
        }

        private static bool IsVisible(Renderer renderer)
        {
            return renderer != null && renderer.enabled && !renderer.forceRenderingOff &&
                   renderer.gameObject.activeInHierarchy;
        }

        private static bool TryCaptureOverview(string path, out string failure)
        {
            failure = string.Empty;
            Camera source = Camera.main;
            if (source == null)
            {
                failure = "Camera.main missing";
                return false;
            }

            const int width = 1280;
            const int height = 720;
            RenderTexture previous = RenderTexture.active;
            var target = new RenderTexture(
                width,
                height,
                24,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            GameObject captureHost = null;
            Camera overlay = null;
            RenderTexture previousOverlayTarget = null;
            try
            {
                captureHost = new GameObject("OfficeV31WorkstationVisualCapture")
                    { hideFlags = HideFlags.HideAndDontSave };
                Camera camera = captureHost.AddComponent<Camera>();
                camera.CopyFrom(source);
                camera.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                camera.aspect = width / (float)height;
                camera.enabled = false;
                camera.targetTexture = target;
                camera.Render();
                foreach (Camera candidate in Object.FindObjectsByType<Camera>(
                             FindObjectsInactive.Include,
                             FindObjectsSortMode.None))
                    if (candidate != null && string.Equals(
                            candidate.gameObject.name,
                            "Family3DProductionOverlayCamera",
                            StringComparison.Ordinal))
                    {
                        overlay = candidate;
                        break;
                    }
                if (overlay == null)
                {
                    failure = "Player V8 production overlay camera missing";
                    return false;
                }
                previousOverlayTarget = overlay.targetTexture;
                overlay.targetTexture = target;
                overlay.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                pixels.Apply(false, false);

                Color32[] colors = pixels.GetPixels32();
                bool containsRenderedColor = false;
                for (var index = 0; index < colors.Length; index += 64)
                {
                    Color32 color = colors[index];
                    if (color.r > 12 || color.g > 12 || color.b > 12)
                    {
                        containsRenderedColor = true;
                        break;
                    }
                }
                if (!containsRenderedColor)
                {
                    failure = "capture is entirely black";
                    return false;
                }

                File.WriteAllBytes(path, pixels.EncodeToPNG());
                return File.Exists(path) && new FileInfo(path).Length > 1024L;
            }
            catch (Exception exception)
            {
                failure = exception.GetType().Name + ":" + exception.Message;
                return false;
            }
            finally
            {
                if (overlay != null)
                    overlay.targetTexture = previousOverlayTarget;
                RenderTexture.active = previous;
                if (captureHost != null) Object.Destroy(captureHost);
                Object.Destroy(target);
                Object.Destroy(pixels);
            }
        }

        private static void Finish(string directory, bool pass, string detail)
        {
            string status = pass ? "PASS" : "FAIL";
            File.WriteAllText(
                Path.Combine(directory, "v31-workstation-visual-result.txt"),
                "FAMILY_COMPANY_OFFICE_V31_WORKSTATION_VISUAL: " + status +
                Environment.NewLine + detail + Environment.NewLine);
            Debug.Log("FAMILY_COMPANY_OFFICE_V31_WORKSTATION_VISUAL: " + status + " | " + detail);
            Application.Quit(pass ? 0 : 1);
        }

        private static bool HasFlag(string flag)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length; index++)
                if (string.Equals(args[index], flag, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static string ArgumentValue(string argument)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (var index = 0; index + 1 < args.Length; index++)
                if (string.Equals(args[index], argument, StringComparison.OrdinalIgnoreCase))
                    return args[index + 1];
            return string.Empty;
        }
    }
}
