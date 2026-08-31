using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using FamilyCompany.Presentation.Unity.OfficeGridView;
using FamilyCompany.Simulation.Game;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime.Qa
{
    /// <summary>
    /// Release-player proof that the production Player V8 and Father V19 share the real runtime:
    /// they approach each other through authoritative dynamic occupancy without penetration, then
    /// independently route to their purchased V31 workstation sets and work at the same time.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OfficePlayerFather3DInteractionPlayerQa : MonoBehaviour
    {
        public const string CommandLineFlag = "-familyCompanyPlayerFather3DInteractionQa";
        public const string ArtifactDirectoryArgument =
            "-familyCompanyPlayerFather3DInteractionArtifacts";

        private string artifactDirectory = string.Empty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInstall()
        {
            if (!HasFlag(CommandLineFlag) ||
                Object.FindFirstObjectByType<OfficePlayerFather3DInteractionPlayerQa>() != null)
                return;
            var host = new GameObject("~OfficePlayerFather3DInteractionPlayerQa");
            DontDestroyOnLoad(host);
            host.AddComponent<OfficePlayerFather3DInteractionPlayerQa>();
        }

        private void Start()
        {
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            artifactDirectory = ArgumentValue(ArtifactDirectoryArgument);
            if (string.IsNullOrWhiteSpace(artifactDirectory))
                artifactDirectory = Path.Combine(
                    Application.persistentDataPath,
                    "OfficePlayerFather3DInteractionPlayerQa");
            Directory.CreateDirectory(artifactDirectory);
            StartCoroutine(RunGuarded());
        }

        private IEnumerator RunGuarded()
        {
            IEnumerator run = Run();
            while (true)
            {
                object yielded;
                try
                {
                    if (!run.MoveNext()) yield break;
                    yielded = run.Current;
                }
                catch (Exception exception)
                {
                    Finish(false, "unhandled=" + exception.GetType().Name + ":" + exception.Message);
                    yield break;
                }
                yield return yielded;
            }
        }

        private IEnumerator Run()
        {
            PrototypeBootstrap bootstrap = Object.FindFirstObjectByType<PrototypeBootstrap>();
            if (bootstrap == null)
            {
                Finish(false, "PrototypeBootstrap missing");
                yield break;
            }

            ScenePreviewJump.ShowStarterOffice();
            bootstrap.StartNewGameNow(1, false);
            ScenePreviewJump.ShowStarterOffice();
            bootstrap.SetWorldTimeScaleNow(1f);

            float deadline = Time.realtimeSinceStartup + 30f;
            StarterOfficeRuntimeBootstrap runtime = null;
            while (Time.realtimeSinceStartup < deadline)
            {
                runtime = Object.FindFirstObjectByType<StarterOfficeRuntimeBootstrap>();
                if (runtime != null && runtime.IsReady && runtime.World != null &&
                    runtime.Actors.Count == 4 && bootstrap.State != null)
                    break;
                yield return null;
            }
            if (runtime == null || !runtime.IsReady || runtime.World == null ||
                bootstrap.State == null)
            {
                Finish(false, "starter office runtime did not become ready");
                yield break;
            }
            yield return new WaitForSecondsRealtime(3.5f);

            if (!TryFindActors(runtime, out OfficeRuntimeAgent player, out OfficeRuntimeAgent father))
            {
                Finish(false, "Player or Father runtime actor missing");
                yield break;
            }

            OfficeGridCoordinate playerStart = new OfficeGridCoordinate(4, 6);
            OfficeGridCoordinate fatherStart = new OfficeGridCoordinate(8, 6);
            player.QaTeleportToCell(playerStart);
            father.QaTeleportToCell(fatherStart);
            ParkOtherActors(runtime, player, father);
            for (var frame = 0; frame < 3; frame++) yield return null;

            Vector2 playerInitial = player.Position;
            Vector2 fatherInitial = father.Position;
            Vector2 approach = (fatherInitial - playerInitial).normalized;
            if (approach.sqrMagnitude < 0.99f)
            {
                Finish(false, "mutual approach direction is degenerate");
                yield break;
            }
            runtime.World.Occupancy.ResetMetrics();
            player.QaSetDirectMovementInput(approach);
            father.QaSetDirectMovementInput(-approach);
            var playerProjectedFrames = 0;
            var fatherProjectedFrames = 0;
            float minimumPairMargin = float.PositiveInfinity;
            deadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                if (player.WasCollisionProjected) playerProjectedFrames++;
                if (father.WasCollisionProjected) fatherProjectedFrames++;
                float margin = Vector2.Distance(player.Position, father.Position) -
                               (player.DynamicAgentRadius + father.DynamicAgentRadius);
                minimumPairMargin = Mathf.Min(minimumPairMargin, margin);
                if (runtime.World.Occupancy.BlockedAgentMoveCount > 0 &&
                    minimumPairMargin <= 0.04f)
                    break;
            }
            player.QaSetDirectMovementInput(Vector2.zero);
            father.QaSetDirectMovementInput(Vector2.zero);
            for (var frame = 0; frame < 3; frame++) yield return null;

            float playerTravel = Vector2.Distance(playerInitial, player.Position);
            float fatherTravel = Vector2.Distance(fatherInitial, father.Position);
            int blockedAgentMoves = runtime.World.Occupancy.BlockedAgentMoveCount;
            int approachPenetrations = runtime.World.Occupancy.AgentPenetrationCount;
            if (!TryMeasureProductionActorPixelOverlap(
                    out int productionActorOverlapPixels,
                    out int playerRenderedPixels,
                    out int fatherRenderedPixels,
                    out string pixelOverlapFailure))
            {
                Finish(false, "production actor pixel-overlap measurement failed: " +
                              pixelOverlapFailure);
                yield break;
            }
            if (playerTravel < 0.25f || fatherTravel < 0.25f ||
                blockedAgentMoves <= 0 || minimumPairMargin > 0.08f ||
                minimumPairMargin < -0.0105f || approachPenetrations != 0)
            {
                Finish(
                    false,
                    "mutual avoidance gate failed playerTravel=" + playerTravel.ToString("F4") +
                    " fatherTravel=" + fatherTravel.ToString("F4") +
                    " margin=" + minimumPairMargin.ToString("F5") +
                    " blocked=" + blockedAgentMoves +
                    " penetrations=" + approachPenetrations);
                yield break;
            }
            if (productionActorOverlapPixels != 0)
            {
                Finish(
                    false,
                    "production actors visually overlap at collision stop pixels=" +
                    productionActorOverlapPixels +
                    " playerPixels=" + playerRenderedPixels +
                    " fatherPixels=" + fatherRenderedPixels);
                yield break;
            }

            if (!TryCaptureOverview(
                    Path.Combine(artifactDirectory, "player-father-avoidance.png"),
                    out string avoidanceCaptureFailure))
            {
                Finish(false, "avoidance capture failed: " + avoidanceCaptureFailure);
                yield break;
            }

            player.QaTeleportToCell(new OfficeGridCoordinate(1, 1));
            father.QaTeleportToCell(new OfficeGridCoordinate(11, 11));
            ParkOtherActors(runtime, player, father);

            GameState state = bootstrap.State;
            OfficeGridCoordinate[] workstationOrigins =
            {
                new OfficeGridCoordinate(4, 4),
                new OfficeGridCoordinate(9, 4),
                new OfficeGridCoordinate(9, 9)
            };
            for (var index = 0; index < workstationOrigins.Length; index++)
            {
                OfficeFurnitureCommandResult purchase =
                    OfficeFurnitureTransactionService.PurchaseAndPlaceWorkstation(
                        state,
                        "qa-player-father-workstation-purchase-" + index,
                        "qa-player-father-workstation-" + index,
                        workstationOrigins[index],
                        (OfficeFurnitureFacing)index);
                if (!purchase.Success)
                {
                    Finish(
                        false,
                        "workstation purchase " + index + " failed=" + purchase.Failure +
                        ":" + purchase.Message);
                    yield break;
                }
            }

            runtime.ApplyLayoutForQa(state.OfficeGrid);
            deadline = Time.realtimeSinceStartup + 30f;
            while (runtime.IsPreparing && Time.realtimeSinceStartup < deadline)
                yield return null;
            if (!runtime.IsReady || runtime.World == null)
            {
                Finish(false, "workstation layout rebuild did not become ready");
                yield break;
            }
            for (var frame = 0; frame < 5; frame++) yield return null;

            if (!TryFindActors(runtime, out player, out father))
            {
                Finish(false, "Player or Father missing after workstation rebuild");
                yield break;
            }
            OfficeSeatSlot playerSeat = runtime.World.Grid.SeatSlots.FirstOrDefault(seat =>
                string.Equals(seat.SeatId, "seat_player", StringComparison.Ordinal));
            OfficeSeatSlot fatherSeat = runtime.World.Grid.SeatSlots.FirstOrDefault(seat =>
                string.Equals(seat.SeatId, "seat_father", StringComparison.Ordinal));
            if (playerSeat == null || fatherSeat == null)
            {
                Finish(
                    false,
                    "canonical seats missing; seats=" + string.Join(
                        ",",
                        runtime.World.Grid.SeatSlots.Select(seat => seat.SeatId)));
                yield break;
            }

            runtime.World.Occupancy.ResetMetrics();
            bool playerAccepted = player.QaBeginSeatedWorkAtSeat(
                playerSeat.SeatId,
                "player-father-production-work");
            bool fatherAccepted = father.QaBeginSeatedWorkAtSeat(
                fatherSeat.SeatId,
                "player-father-production-work");
            if (!playerAccepted || !fatherAccepted)
            {
                Finish(
                    false,
                    "simultaneous work route rejected player=" + playerAccepted +
                    " father=" + fatherAccepted);
                yield break;
            }

            deadline = Time.realtimeSinceStartup + 40f;
            while (Time.realtimeSinceStartup < deadline &&
                   (player.Phase != OfficeRuntimeAgentPhase.Working ||
                    father.Phase != OfficeRuntimeAgentPhase.Working))
                yield return null;
            if (player.Phase != OfficeRuntimeAgentPhase.Working ||
                father.Phase != OfficeRuntimeAgentPhase.Working)
            {
                Finish(
                    false,
                    "both actors did not reach Working player=" + player.Phase +
                    " father=" + father.Phase +
                    " playerBlocker=" + player.LastMovementBlocker +
                    " fatherBlocker=" + father.LastMovementBlocker);
                yield break;
            }
            yield return new WaitForSecondsRealtime(0.65f);
            for (var frame = 0; frame < 3; frame++) yield return null;

            GameObject productionRoot = GameObject.Find("~Family3DProductionPresenter");
            GameObject playerHost = GameObject.Find("PlayerV8ProductionHost");
            GameObject fatherHost = GameObject.Find("FatherV19ProductionHost");
            if (productionRoot == null || playerHost == null || fatherHost == null ||
                !TryMeasureKnees(playerHost, out float playerLeftKnee, out float playerRightKnee) ||
                !TryMeasureKnees(fatherHost, out float fatherLeftKnee, out float fatherRightKnee))
            {
                Finish(false, "production 3D hosts or Humanoid leg bones are incomplete");
                yield break;
            }
            if (!ApprovedKnee(playerLeftKnee, 80f) ||
                !ApprovedKnee(playerRightKnee, 80f) ||
                !ApprovedKnee(fatherLeftKnee, 70f) ||
                !ApprovedKnee(fatherRightKnee, 70f))
            {
                Finish(
                    false,
                    "seated knee gate failed player=" + playerLeftKnee.ToString("F2") +
                    "/" + playerRightKnee.ToString("F2") +
                    " father=" + fatherLeftKnee.ToString("F2") +
                    "/" + fatherRightKnee.ToString("F2"));
                yield break;
            }

            int visibleRetired = CountVisibleRetired(runtime, player, father);
            int staticViolations = runtime.World.Occupancy.StaticViolationCount;
            int interactionViolations = runtime.World.Occupancy.InteractionViolationCount;
            int workPenetrations = runtime.World.Occupancy.AgentPenetrationCount;
            if (visibleRetired != 0 || staticViolations != 0 ||
                interactionViolations != 0 || workPenetrations != 0)
            {
                Finish(
                    false,
                    "working clearance gate failed retired=" + visibleRetired +
                    " static=" + staticViolations +
                    " interaction=" + interactionViolations +
                    " penetrations=" + workPenetrations);
                yield break;
            }

            string workingScreenshot = Path.Combine(
                artifactDirectory,
                "player-father-working.png");
            if (!TryCaptureOverview(workingScreenshot, out string workingCaptureFailure))
            {
                Finish(false, "working capture failed: " + workingCaptureFailure);
                yield break;
            }

            var result = new StringBuilder();
            result.AppendLine("FAMILY_COMPANY_PLAYER_FATHER_3D_INTERACTION: PASS");
            result.AppendLine("releasePlayer=true");
            result.AppendLine("renderer=D3D11");
            result.AppendLine("actors=player,father");
            result.AppendLine("production3DHosts=2");
            result.AppendLine("mutualApproachPlayerTravel=" + playerTravel.ToString("F5"));
            result.AppendLine("mutualApproachFatherTravel=" + fatherTravel.ToString("F5"));
            result.AppendLine("minimumPairSeparationMargin=" + minimumPairMargin.ToString("F6"));
            result.AppendLine("blockedAgentMoves=" + blockedAgentMoves);
            result.AppendLine("playerCollisionProjectedFrames=" + playerProjectedFrames);
            result.AppendLine("fatherCollisionProjectedFrames=" + fatherProjectedFrames);
            result.AppendLine("approachAgentPenetrations=" + approachPenetrations);
            result.AppendLine("productionActorOverlapPixels=" + productionActorOverlapPixels);
            result.AppendLine("playerRenderedPixels=" + playerRenderedPixels);
            result.AppendLine("fatherRenderedPixels=" + fatherRenderedPixels);
            result.AppendLine("workstations=3");
            result.AppendLine("playerSeat=" + playerSeat.SeatId);
            result.AppendLine("fatherSeat=" + fatherSeat.SeatId);
            result.AppendLine("playerPhase=" + player.Phase);
            result.AppendLine("fatherPhase=" + father.Phase);
            result.AppendLine("playerKnees=" + playerLeftKnee.ToString("F2") + "/" +
                              playerRightKnee.ToString("F2"));
            result.AppendLine("fatherKnees=" + fatherLeftKnee.ToString("F2") + "/" +
                              fatherRightKnee.ToString("F2"));
            result.AppendLine("workingStaticViolations=" + staticViolations);
            result.AppendLine("workingInteractionViolations=" + interactionViolations);
            result.AppendLine("workingAgentPenetrations=" + workPenetrations);
            result.AppendLine("retiredVisible=" + visibleRetired);
            File.WriteAllText(
                Path.Combine(artifactDirectory, "player-father-3d-interaction-result.txt"),
                result.ToString());
            Finish(
                true,
                "actors=player,father margin=" + minimumPairMargin.ToString("F5") +
                " blocked=" + blockedAgentMoves +
                " penetrations=0 phases=" + player.Phase + "/" + father.Phase +
                " seats=" + playerSeat.SeatId + "/" + fatherSeat.SeatId +
                " retiredVisible=0");
        }

        private static bool TryFindActors(
            StarterOfficeRuntimeBootstrap runtime,
            out OfficeRuntimeAgent player,
            out OfficeRuntimeAgent father)
        {
            player = runtime.Actors.FirstOrDefault(actor => actor != null && string.Equals(
                actor.AgentId,
                "player",
                StringComparison.Ordinal));
            father = runtime.Actors.FirstOrDefault(actor => actor != null && string.Equals(
                actor.AgentId,
                "father",
                StringComparison.Ordinal));
            return player != null && father != null;
        }

        private static void ParkOtherActors(
            StarterOfficeRuntimeBootstrap runtime,
            OfficeRuntimeAgent player,
            OfficeRuntimeAgent father)
        {
            OfficeGridCoordinate[] cells =
            {
                new OfficeGridCoordinate(11, 1),
                new OfficeGridCoordinate(1, 11)
            };
            var index = 0;
            foreach (OfficeRuntimeAgent actor in runtime.Actors)
            {
                if (actor == null || ReferenceEquals(actor, player) || ReferenceEquals(actor, father))
                    continue;
                actor.QaTeleportToCell(cells[Mathf.Min(index, cells.Length - 1)]);
                actor.QaSetDirectMovementInput(Vector2.zero);
                index++;
            }
        }

        private static bool TryMeasureKnees(
            GameObject host,
            out float leftKnee,
            out float rightKnee)
        {
            leftKnee = rightKnee = 0f;
            Animator animator = host.GetComponentInChildren<Animator>(true);
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
                return false;
            Transform leftUpper = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            Transform leftLower = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rightUpper = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            Transform rightLower = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            if (leftUpper == null || leftLower == null || leftFoot == null ||
                rightUpper == null || rightLower == null || rightFoot == null)
                return false;
            leftKnee = Vector3.Angle(
                leftUpper.position - leftLower.position,
                leftFoot.position - leftLower.position);
            rightKnee = Vector3.Angle(
                rightUpper.position - rightLower.position,
                rightFoot.position - rightLower.position);
            return true;
        }

        private static bool ApprovedKnee(float angle, float minimum) =>
            angle >= minimum && angle <= 140f;

        private static int CountVisibleRetired(
            StarterOfficeRuntimeBootstrap runtime,
            OfficeRuntimeAgent player,
            OfficeRuntimeAgent father)
        {
            var count = 0;
            if (IsVisible(player.PresentationRenderer)) count++;
            if (IsVisible(player.SeatedUpperBodyProtectionRenderer)) count++;
            if (IsVisible(father.PresentationRenderer)) count++;
            if (IsVisible(father.SeatedUpperBodyProtectionRenderer)) count++;
            foreach (OfficeSeatSlot seat in runtime.World.Grid.SeatSlots)
            {
                count += CountVisibleFurnitureRenderers(
                    runtime.World.FurniturePresenter,
                    seat.WorkSurfaceFurnitureId);
                count += CountVisibleFurnitureRenderers(
                    runtime.World.FurniturePresenter,
                    seat.ChairFurnitureId);
            }
            return count;
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

        private static bool IsVisible(Renderer renderer) =>
            renderer != null && renderer.enabled && !renderer.forceRenderingOff &&
            renderer.gameObject.activeInHierarchy;

        private static bool TryMeasureProductionActorPixelOverlap(
            out int overlapPixels,
            out int playerPixels,
            out int fatherPixels,
            out string failure)
        {
            overlapPixels = 0;
            playerPixels = 0;
            fatherPixels = 0;
            failure = string.Empty;
            GameObject player = GameObject.Find("PlayerV8ProductionHost");
            GameObject father = GameObject.Find("FatherV19ProductionHost");
            Camera overlay = Object.FindObjectsByType<Camera>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate != null && string.Equals(
                    candidate.gameObject.name,
                    "Family3DProductionOverlayCamera",
                    StringComparison.Ordinal));
            if (player == null || father == null || overlay == null)
            {
                failure = "production hosts or overlay camera missing";
                return false;
            }
            Renderer[] playerRenderers = player.GetComponentsInChildren<Renderer>(true);
            Renderer[] fatherRenderers = father.GetComponentsInChildren<Renderer>(true);
            if (playerRenderers.Length == 0 || fatherRenderers.Length == 0)
            {
                failure = "production renderers missing";
                return false;
            }

            const int width = 640;
            const int height = 360;
            var target = new RenderTexture(
                width,
                height,
                24,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            var pixels = new Texture2D(width, height, TextureFormat.RGBA32, false);
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = overlay.targetTexture;
            CameraClearFlags previousClearFlags = overlay.clearFlags;
            Color previousBackground = overlay.backgroundColor;
            bool[] playerForceOff = playerRenderers.Select(renderer =>
                renderer.forceRenderingOff).ToArray();
            bool[] fatherForceOff = fatherRenderers.Select(renderer =>
                renderer.forceRenderingOff).ToArray();
            try
            {
                overlay.targetTexture = target;
                overlay.clearFlags = CameraClearFlags.SolidColor;
                overlay.backgroundColor = Color.clear;
                for (var index = 0; index < playerRenderers.Length; index++)
                    playerRenderers[index].forceRenderingOff = false;
                for (var index = 0; index < fatherRenderers.Length; index++)
                    fatherRenderers[index].forceRenderingOff = true;
                overlay.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                pixels.Apply(false, false);
                Color32[] playerSample = pixels.GetPixels32();
                var playerMask = new bool[playerSample.Length];
                for (var index = 0; index < playerSample.Length; index++)
                    if (playerSample[index].a > 32)
                    {
                        playerMask[index] = true;
                        playerPixels++;
                    }

                for (var index = 0; index < playerRenderers.Length; index++)
                    playerRenderers[index].forceRenderingOff = true;
                for (var index = 0; index < fatherRenderers.Length; index++)
                    fatherRenderers[index].forceRenderingOff = false;
                overlay.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                pixels.Apply(false, false);
                Color32[] fatherSample = pixels.GetPixels32();
                for (var index = 0; index < fatherSample.Length; index++)
                    if (fatherSample[index].a > 32)
                    {
                        fatherPixels++;
                        if (playerMask[index]) overlapPixels++;
                    }
                if (playerPixels < 50 || fatherPixels < 50)
                {
                    failure = "actor silhouette was not rendered player=" + playerPixels +
                              " father=" + fatherPixels;
                    return false;
                }
                return true;
            }
            catch (Exception exception)
            {
                failure = exception.GetType().Name + ":" + exception.Message;
                return false;
            }
            finally
            {
                for (var index = 0; index < playerRenderers.Length; index++)
                    playerRenderers[index].forceRenderingOff = playerForceOff[index];
                for (var index = 0; index < fatherRenderers.Length; index++)
                    fatherRenderers[index].forceRenderingOff = fatherForceOff[index];
                overlay.targetTexture = previousTarget;
                overlay.clearFlags = previousClearFlags;
                overlay.backgroundColor = previousBackground;
                RenderTexture.active = previousActive;
                Object.Destroy(target);
                Object.Destroy(pixels);
            }
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
                captureHost = new GameObject("OfficePlayerFather3DInteractionCapture")
                    { hideFlags = HideFlags.HideAndDontSave };
                Camera camera = captureHost.AddComponent<Camera>();
                camera.CopyFrom(source);
                camera.transform.SetPositionAndRotation(
                    source.transform.position,
                    source.transform.rotation);
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
                    failure = "production 3D overlay camera missing";
                    return false;
                }
                previousOverlayTarget = overlay.targetTexture;
                overlay.targetTexture = target;
                overlay.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                pixels.Apply(false, false);
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
                if (overlay != null) overlay.targetTexture = previousOverlayTarget;
                RenderTexture.active = previous;
                if (captureHost != null) Object.Destroy(captureHost);
                Object.Destroy(target);
                Object.Destroy(pixels);
            }
        }

        private void Finish(bool pass, string detail)
        {
            string status = pass ? "PASS" : "FAIL";
            File.WriteAllText(
                Path.Combine(artifactDirectory, "player-father-3d-interaction-final.txt"),
                "FAMILY_COMPANY_PLAYER_FATHER_3D_INTERACTION: " + status +
                Environment.NewLine + detail + Environment.NewLine);
            if (pass)
                Debug.Log("FAMILY_COMPANY_PLAYER_FATHER_3D_INTERACTION: PASS | " + detail);
            else
                Debug.LogError("FAMILY_COMPANY_PLAYER_FATHER_3D_INTERACTION: FAIL | " + detail);
            Application.Quit(pass ? 0 : 1);
        }

        private static bool HasFlag(string flag) => Environment.GetCommandLineArgs().Any(argument =>
            string.Equals(argument, flag, StringComparison.OrdinalIgnoreCase));

        private static string ArgumentValue(string argument)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index + 1 < arguments.Length; index++)
                if (string.Equals(
                        arguments[index],
                        argument,
                        StringComparison.OrdinalIgnoreCase))
                    return arguments[index + 1];
            return string.Empty;
        }
    }
}
