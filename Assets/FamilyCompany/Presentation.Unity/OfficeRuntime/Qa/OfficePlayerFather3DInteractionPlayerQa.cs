using System;
using System.Collections;
using System.Collections.Generic;
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
        private const string Legacy2DScaleCandidateFlag =
            "-familyCompanyLegacy2DScaleCandidate";

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

            // Keep a full walk cycle before contact. The wider eight-cell approach is visual QA,
            // not just a collision probe: at the locked runtime speed it records more than the
            // complete 1.4-second authored cycle before the two visible bodies meet.
            OfficeGridCoordinate playerStart = new OfficeGridCoordinate(2, 6);
            OfficeGridCoordinate fatherStart = new OfficeGridCoordinate(10, 6);
            player.QaTeleportToCell(playerStart);
            father.QaTeleportToCell(fatherStart);
            ParkOtherActors(runtime, player, father);
            for (var frame = 0; frame < 3; frame++) yield return null;

            Vector2 playerInitial = player.Position;
            Vector2 fatherInitial = father.Position;
            float playerStartTileCenterError = Vector2.Distance(
                playerInitial,
                (Vector2)runtime.World.Presenter.CellCenterWorld(playerStart));
            float fatherStartTileCenterError = Vector2.Distance(
                fatherInitial,
                (Vector2)runtime.World.Presenter.CellCenterWorld(fatherStart));
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
            var approachFrameCount = 0;
            var playerPixelSamples = new List<int>();
            var fatherPixelSamples = new List<int>();
            var playerWidthSamples = new List<int>();
            var playerHeightSamples = new List<int>();
            var fatherWidthSamples = new List<int>();
            var fatherHeightSamples = new List<int>();
            var playerHeadSamples = new List<int>();
            var playerTorsoSamples = new List<int>();
            var fatherHeadSamples = new List<int>();
            var fatherTorsoSamples = new List<int>();
            var playerLumaSamples = new List<float>();
            var playerSaturationSamples = new List<float>();
            var fatherLumaSamples = new List<float>();
            var fatherSaturationSamples = new List<float>();
            var playerFootMidpointPixelErrors = new List<float>();
            var fatherFootMidpointPixelErrors = new List<float>();
            var playerFootLocalXSamples = new List<float>();
            var playerFootLocalZSamples = new List<float>();
            var fatherFootLocalXSamples = new List<float>();
            var fatherFootLocalZSamples = new List<float>();
            float playerMaximumCenterLineError = 0f;
            float fatherMaximumCenterLineError = 0f;
            string approachFrameDirectory = Path.Combine(artifactDirectory, "approach-frames");
            Directory.CreateDirectory(approachFrameDirectory);
            int previousCaptureFramerate = Time.captureFramerate;
            Time.captureFramerate = 24;
            float minimumPairMargin = float.PositiveInfinity;
            deadline = Time.realtimeSinceStartup + 30f;
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return new WaitForEndOfFrame();
                string approachFramePath = Path.Combine(
                    approachFrameDirectory,
                    "approach-" + approachFrameCount.ToString("D3") + ".png");
                if (!TryCaptureOverview(approachFramePath, out string approachCaptureFailure))
                {
                    Time.captureFramerate = previousCaptureFramerate;
                    Finish(false, "approach frame capture failed: " + approachCaptureFailure);
                    yield break;
                }
                approachFrameCount++;
                if ((approachFrameCount - 1) % 6 == 0)
                {
                    if (TryMeasureProductionActorPixelOverlap(
                            out _,
                            out int samplePlayerPixels,
                            out int sampleFatherPixels,
                            out int samplePlayerWidth,
                            out int samplePlayerHeight,
                            out int sampleFatherWidth,
                            out int sampleFatherHeight,
                            out int samplePlayerHead,
                            out int samplePlayerTorso,
                            out int sampleFatherHead,
                            out int sampleFatherTorso,
                            out float samplePlayerLuma,
                            out float samplePlayerSaturation,
                            out float sampleFatherLuma,
                            out float sampleFatherSaturation,
                            out _))
                    {
                        playerPixelSamples.Add(samplePlayerPixels);
                        fatherPixelSamples.Add(sampleFatherPixels);
                        playerWidthSamples.Add(samplePlayerWidth);
                        playerHeightSamples.Add(samplePlayerHeight);
                        fatherWidthSamples.Add(sampleFatherWidth);
                        fatherHeightSamples.Add(sampleFatherHeight);
                        playerHeadSamples.Add(samplePlayerHead);
                        playerTorsoSamples.Add(samplePlayerTorso);
                        fatherHeadSamples.Add(sampleFatherHead);
                        fatherTorsoSamples.Add(sampleFatherTorso);
                        playerLumaSamples.Add(samplePlayerLuma);
                        playerSaturationSamples.Add(samplePlayerSaturation);
                        fatherLumaSamples.Add(sampleFatherLuma);
                        fatherSaturationSamples.Add(sampleFatherSaturation);
                        if (TryMeasureTileCenterPixelAlignment(
                                player,
                                "PlayerV8ProductionHost",
                                out float playerFootMidpointPixelError,
                                out Vector2 playerFootLocalOffset))
                        {
                            playerFootMidpointPixelErrors.Add(playerFootMidpointPixelError);
                            playerFootLocalXSamples.Add(playerFootLocalOffset.x);
                            playerFootLocalZSamples.Add(playerFootLocalOffset.y);
                        }
                        if (TryMeasureTileCenterPixelAlignment(
                                father,
                                "FatherV19ProductionHost",
                                out float fatherFootMidpointPixelError,
                                out Vector2 fatherFootLocalOffset))
                        {
                            fatherFootMidpointPixelErrors.Add(fatherFootMidpointPixelError);
                            fatherFootLocalXSamples.Add(fatherFootLocalOffset.x);
                            fatherFootLocalZSamples.Add(fatherFootLocalOffset.y);
                        }
                    }
                }
                playerMaximumCenterLineError = Mathf.Max(
                    playerMaximumCenterLineError,
                    DistanceToCenterLine(player.Position, playerInitial, approach));
                fatherMaximumCenterLineError = Mathf.Max(
                    fatherMaximumCenterLineError,
                    DistanceToCenterLine(father.Position, fatherInitial, -approach));
                if (player.WasCollisionProjected) playerProjectedFrames++;
                if (father.WasCollisionProjected) fatherProjectedFrames++;
                float margin = Vector2.Distance(player.Position, father.Position) -
                               (player.DynamicAgentRadius + father.DynamicAgentRadius);
                minimumPairMargin = Mathf.Min(minimumPairMargin, margin);
                if (runtime.World.Occupancy.BlockedAgentMoveCount > 0 &&
                    minimumPairMargin <= 0.04f)
                    break;
            }
            Time.captureFramerate = previousCaptureFramerate;
            player.QaSetDirectMovementInput(Vector2.zero);
            father.QaSetDirectMovementInput(Vector2.zero);
            for (var frame = 0; frame < 3; frame++) yield return null;

            float playerTravel = Vector2.Distance(playerInitial, player.Position);
            float fatherTravel = Vector2.Distance(fatherInitial, father.Position);
            int blockedAgentMoves = runtime.World.Occupancy.BlockedAgentMoveCount;
            int approachPenetrations = runtime.World.Occupancy.AgentPenetrationCount;
            float playerMedianFootMidpointPixelError = playerFootMidpointPixelErrors.Count == 0
                ? float.PositiveInfinity
                : Median(playerFootMidpointPixelErrors);
            float fatherMedianFootMidpointPixelError = fatherFootMidpointPixelErrors.Count == 0
                ? float.PositiveInfinity
                : Median(fatherFootMidpointPixelErrors);
            float playerMaximumFootMidpointPixelError = playerFootMidpointPixelErrors.Count == 0
                ? float.PositiveInfinity
                : playerFootMidpointPixelErrors.Max();
            float fatherMaximumFootMidpointPixelError = fatherFootMidpointPixelErrors.Count == 0
                ? float.PositiveInfinity
                : fatherFootMidpointPixelErrors.Max();
            float playerMedianFootLocalX = playerFootLocalXSamples.Count == 0
                ? float.PositiveInfinity
                : Median(playerFootLocalXSamples);
            float playerMedianFootLocalZ = playerFootLocalZSamples.Count == 0
                ? float.PositiveInfinity
                : Median(playerFootLocalZSamples);
            float fatherMedianFootLocalX = fatherFootLocalXSamples.Count == 0
                ? float.PositiveInfinity
                : Median(fatherFootLocalXSamples);
            float fatherMedianFootLocalZ = fatherFootLocalZSamples.Count == 0
                ? float.PositiveInfinity
                : Median(fatherFootLocalZSamples);
            if (!TryMeasureProductionActorPixelOverlap(
                    out int productionActorOverlapPixels,
                    out int contactPlayerPixels,
                    out int contactFatherPixels,
                    out int contactPlayerWidth,
                    out int contactPlayerHeight,
                    out int contactFatherWidth,
                    out int contactFatherHeight,
                    out int contactPlayerHead,
                    out int contactPlayerTorso,
                    out int contactFatherHead,
                    out int contactFatherTorso,
                    out _,
                    out _,
                    out _,
                    out _,
                    out string pixelOverlapFailure))
            {
                Finish(false, "production actor pixel-overlap measurement failed: " +
                              pixelOverlapFailure);
                yield break;
            }
            bool legacy2DScaleCandidate = HasFlag(Legacy2DScaleCandidateFlag);
            if (playerTravel < 0.25f || fatherTravel < 0.25f || approachFrameCount < 24 ||
                playerHeightSamples.Count < 4 || playerFootMidpointPixelErrors.Count < 4 ||
                blockedAgentMoves <= 0 || minimumPairMargin > 0.08f ||
                minimumPairMargin < -0.0105f || approachPenetrations != 0 ||
                playerStartTileCenterError > 0.0001f || fatherStartTileCenterError > 0.0001f ||
                playerMaximumCenterLineError > 0.0005f ||
                fatherMaximumCenterLineError > 0.0005f ||
                (legacy2DScaleCandidate &&
                 (playerMedianFootMidpointPixelError > 4f ||
                  fatherMedianFootMidpointPixelError > 4f ||
                  playerMaximumFootMidpointPixelError > 8f ||
                  fatherMaximumFootMidpointPixelError > 8f)))
            {
                Finish(
                    false,
                    "mutual avoidance gate failed playerTravel=" + playerTravel.ToString("F4") +
                    " fatherTravel=" + fatherTravel.ToString("F4") +
                    " frames=" + approachFrameCount +
                    " samples=" + playerHeightSamples.Count +
                    " margin=" + minimumPairMargin.ToString("F5") +
                    " startTile=" + playerStartTileCenterError.ToString("F6") + "/" +
                    fatherStartTileCenterError.ToString("F6") +
                    " line=" + playerMaximumCenterLineError.ToString("F6") + "/" +
                    fatherMaximumCenterLineError.ToString("F6") +
                    " footPx=" + playerMedianFootMidpointPixelError.ToString("F3") + "/" +
                    playerMaximumFootMidpointPixelError.ToString("F3") + "/" +
                    fatherMedianFootMidpointPixelError.ToString("F3") + "/" +
                    fatherMaximumFootMidpointPixelError.ToString("F3") +
                    " footLocal=" + playerMedianFootLocalX.ToString("F6") + "/" +
                    playerMedianFootLocalZ.ToString("F6") + "/" +
                    fatherMedianFootLocalX.ToString("F6") + "/" +
                    fatherMedianFootLocalZ.ToString("F6") +
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
                    " playerPixels=" + contactPlayerPixels +
                    " fatherPixels=" + contactFatherPixels);
                yield break;
            }
            int playerRenderedPixels = Median(playerPixelSamples);
            int fatherRenderedPixels = Median(fatherPixelSamples);
            int playerRenderedWidth = Median(playerWidthSamples);
            int playerRenderedHeight = Median(playerHeightSamples);
            int fatherRenderedWidth = Median(fatherWidthSamples);
            int fatherRenderedHeight = Median(fatherHeightSamples);
            int playerHeadWidth = Median(playerHeadSamples);
            int playerTorsoWidth = Median(playerTorsoSamples);
            int fatherHeadWidth = Median(fatherHeadSamples);
            int fatherTorsoWidth = Median(fatherTorsoSamples);
            float playerMeanLuma = Median(playerLumaSamples);
            float playerMeanSaturation = Median(playerSaturationSamples);
            float fatherMeanLuma = Median(fatherLumaSamples);
            float fatherMeanSaturation = Median(fatherSaturationSamples);
            float renderedAreaDifference = Mathf.Abs(
                fatherRenderedPixels - playerRenderedPixels) /
                (float)Mathf.Max(playerRenderedPixels, 1);
            bool sizeFailed = legacy2DScaleCandidate
                ? Mathf.Abs(playerRenderedHeight - 89) > 2 ||
                  Mathf.Abs(fatherRenderedHeight - 94) > 2 ||
                  Mathf.Abs(fatherRenderedWidth - playerRenderedWidth) > 7
                : Mathf.Abs(fatherRenderedHeight - playerRenderedHeight) > 6 ||
                  Mathf.Abs(fatherRenderedWidth - playerRenderedWidth) > 4;
            if (sizeFailed ||
                Mathf.Abs(fatherHeadWidth - playerHeadWidth) > 1 ||
                Mathf.Abs(fatherTorsoWidth - playerTorsoWidth) > 2 ||
                renderedAreaDifference > 0.10f)
            {
                Finish(
                    false,
                    "production actor visual-size standard failed player=" +
                    playerRenderedWidth + "x" + playerRenderedHeight + "/" +
                    playerRenderedPixels + "px father=" + fatherRenderedWidth + "x" +
                              fatherRenderedHeight + "/" + fatherRenderedPixels + "px areaDifference=" +
                    renderedAreaDifference.ToString("F4") + " head=" + playerHeadWidth + "/" +
                    fatherHeadWidth + " torso=" + playerTorsoWidth + "/" + fatherTorsoWidth +
                    " profile=" +
                    (legacy2DScaleCandidate ? "Legacy2DMatchedCandidate" : "ApprovedProduction"));
                yield break;
            }
            float luminanceRatio = fatherMeanLuma / Mathf.Max(playerMeanLuma, 0.001f);
            if (legacy2DScaleCandidate &&
                (playerMeanLuma < 45f || fatherMeanLuma < 45f ||
                 luminanceRatio < 0.70f || luminanceRatio > 1.30f ||
                 playerMeanSaturation < 0.12f || fatherMeanSaturation < 0.12f))
            {
                Finish(
                    false,
                    "legacy-2D matched colour gate failed luma=" +
                    playerMeanLuma.ToString("F2") + "/" + fatherMeanLuma.ToString("F2") +
                    " ratio=" + luminanceRatio.ToString("F3") + " saturation=" +
                    playerMeanSaturation.ToString("F3") + "/" +
                    fatherMeanSaturation.ToString("F3"));
                yield break;
            }
            Debug.Log(
                "FAMILY_COMPANY_PLAYER_FATHER_VISUAL_SIZE: PASS | player=" +
                playerRenderedWidth + "x" + playerRenderedHeight + "/" +
                playerRenderedPixels + "px head=" + playerHeadWidth + " torso=" +
                playerTorsoWidth + " father=" + fatherRenderedWidth + "x" +
                fatherRenderedHeight + "/" + fatherRenderedPixels + "px head=" +
                fatherHeadWidth + " torso=" + fatherTorsoWidth + " areaDifference=" +
                renderedAreaDifference.ToString("F4") + " luma=" +
                playerMeanLuma.ToString("F2") + "/" + fatherMeanLuma.ToString("F2") +
                " saturation=" + playerMeanSaturation.ToString("F3") + "/" +
                fatherMeanSaturation.ToString("F3") + " profile=" +
                (legacy2DScaleCandidate ? "Legacy2DMatchedCandidate" : "ApprovedProduction"));

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
            float playerSeatTileCenterError = Vector2.Distance(
                player.Position,
                (Vector2)runtime.World.Presenter.CellCenterWorld(playerSeat.Cell));
            float fatherSeatTileCenterError = Vector2.Distance(
                father.Position,
                (Vector2)runtime.World.Presenter.CellCenterWorld(fatherSeat.Cell));
            if (playerSeatTileCenterError > 0.001f || fatherSeatTileCenterError > 0.001f)
            {
                Finish(
                    false,
                    "working actor is not on semantic tile center player=" +
                    playerSeatTileCenterError.ToString("F6") + " father=" +
                    fatherSeatTileCenterError.ToString("F6"));
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
            result.AppendLine("scaleProfile=" +
                              (legacy2DScaleCandidate
                                  ? "Legacy2DMatchedCandidate"
                                  : "ApprovedProduction"));
            result.AppendLine("productionEligible=" + (!legacy2DScaleCandidate));
            result.AppendLine("mutualApproachPlayerTravel=" + playerTravel.ToString("F5"));
            result.AppendLine("mutualApproachFatherTravel=" + fatherTravel.ToString("F5"));
            result.AppendLine("mutualApproachFrames=" + approachFrameCount);
            result.AppendLine("mutualApproachMetricSamples=" + playerHeightSamples.Count);
            result.AppendLine("startTileCenterError=" +
                              playerStartTileCenterError.ToString("F6") + "/" +
                              fatherStartTileCenterError.ToString("F6"));
            result.AppendLine("maximumWalkCenterLineError=" +
                              playerMaximumCenterLineError.ToString("F6") + "/" +
                              fatherMaximumCenterLineError.ToString("F6"));
            result.AppendLine("footMidpointTilePixelErrorMedianMax=" +
                              playerMedianFootMidpointPixelError.ToString("F3") + "/" +
                              playerMaximumFootMidpointPixelError.ToString("F3") + "/" +
                              fatherMedianFootMidpointPixelError.ToString("F3") + "/" +
                              fatherMaximumFootMidpointPixelError.ToString("F3"));
            result.AppendLine("footMidpointLocalOffsetMedian=" +
                              playerMedianFootLocalX.ToString("F6") + "/" +
                              playerMedianFootLocalZ.ToString("F6") + "/" +
                              fatherMedianFootLocalX.ToString("F6") + "/" +
                              fatherMedianFootLocalZ.ToString("F6"));
            result.AppendLine("playerRenderedHeightRange=" + playerHeightSamples.Min() + "/" +
                              playerRenderedHeight + "/" + playerHeightSamples.Max());
            result.AppendLine("fatherRenderedHeightRange=" + fatherHeightSamples.Min() + "/" +
                              fatherRenderedHeight + "/" + fatherHeightSamples.Max());
            result.AppendLine("minimumPairSeparationMargin=" + minimumPairMargin.ToString("F6"));
            result.AppendLine("blockedAgentMoves=" + blockedAgentMoves);
            result.AppendLine("playerCollisionProjectedFrames=" + playerProjectedFrames);
            result.AppendLine("fatherCollisionProjectedFrames=" + fatherProjectedFrames);
            result.AppendLine("approachAgentPenetrations=" + approachPenetrations);
            result.AppendLine("productionActorOverlapPixels=" + productionActorOverlapPixels);
            result.AppendLine("playerRenderedPixels=" + playerRenderedPixels);
            result.AppendLine("fatherRenderedPixels=" + fatherRenderedPixels);
            result.AppendLine("playerRenderedBounds=" + playerRenderedWidth + "x" +
                              playerRenderedHeight);
            result.AppendLine("fatherRenderedBounds=" + fatherRenderedWidth + "x" +
                              fatherRenderedHeight);
            result.AppendLine("playerHeadTorsoWidths=" + playerHeadWidth + "/" +
                              playerTorsoWidth);
            result.AppendLine("fatherHeadTorsoWidths=" + fatherHeadWidth + "/" +
                              fatherTorsoWidth);
            result.AppendLine("playerMeanLumaSaturation=" +
                              playerMeanLuma.ToString("F2") + "/" +
                              playerMeanSaturation.ToString("F3"));
            result.AppendLine("fatherMeanLumaSaturation=" +
                              fatherMeanLuma.ToString("F2") + "/" +
                              fatherMeanSaturation.ToString("F3"));
            result.AppendLine("contactRenderedBounds=" + contactPlayerWidth + "x" +
                              contactPlayerHeight + "/" + contactFatherWidth + "x" +
                              contactFatherHeight);
            result.AppendLine("contactRenderedPixels=" + contactPlayerPixels + "/" +
                              contactFatherPixels);
            result.AppendLine("contactHeadTorsoWidths=" + contactPlayerHead + "/" +
                              contactPlayerTorso + "/" + contactFatherHead + "/" +
                              contactFatherTorso);
            result.AppendLine("workstations=3");
            result.AppendLine("playerSeat=" + playerSeat.SeatId);
            result.AppendLine("fatherSeat=" + fatherSeat.SeatId);
            result.AppendLine("playerPhase=" + player.Phase);
            result.AppendLine("fatherPhase=" + father.Phase);
            result.AppendLine("workingSeatTileCenterError=" +
                              playerSeatTileCenterError.ToString("F6") + "/" +
                              fatherSeatTileCenterError.ToString("F6"));
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
            Vector3 leftUpperLocal = animator.transform.InverseTransformPoint(leftUpper.position);
            Vector3 leftLowerLocal = animator.transform.InverseTransformPoint(leftLower.position);
            Vector3 leftFootLocal = animator.transform.InverseTransformPoint(leftFoot.position);
            Vector3 rightUpperLocal = animator.transform.InverseTransformPoint(rightUpper.position);
            Vector3 rightLowerLocal = animator.transform.InverseTransformPoint(rightLower.position);
            Vector3 rightFootLocal = animator.transform.InverseTransformPoint(rightFoot.position);
            leftKnee = Vector3.Angle(
                leftUpperLocal - leftLowerLocal,
                leftFootLocal - leftLowerLocal);
            rightKnee = Vector3.Angle(
                rightUpperLocal - rightLowerLocal,
                rightFootLocal - rightLowerLocal);
            return true;
        }

        private static bool ApprovedKnee(float angle, float minimum) =>
            // The Father presentation uses a locked horizontal-only silhouette correction. Its
            // world-space bone triangle reads about one degree wider even though the Humanoid pose
            // is unchanged; 145 still rejects a visibly straight 180-degree leg.
            angle >= minimum && angle <= 145f;

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
            out int playerWidth,
            out int playerHeight,
            out int fatherWidth,
            out int fatherHeight,
            out int playerHeadWidth,
            out int playerTorsoWidth,
            out int fatherHeadWidth,
            out int fatherTorsoWidth,
            out float playerMeanLuma,
            out float playerMeanSaturation,
            out float fatherMeanLuma,
            out float fatherMeanSaturation,
            out string failure)
        {
            overlapPixels = 0;
            playerPixels = 0;
            fatherPixels = 0;
            playerWidth = 0;
            playerHeight = 0;
            fatherWidth = 0;
            fatherHeight = 0;
            playerHeadWidth = 0;
            playerTorsoWidth = 0;
            fatherHeadWidth = 0;
            fatherTorsoWidth = 0;
            playerMeanLuma = 0f;
            playerMeanSaturation = 0f;
            fatherMeanLuma = 0f;
            fatherMeanSaturation = 0f;
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

            const int width = 1280;
            const int height = 720;
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
                MeasureRenderedColour(
                    playerSample,
                    out playerMeanLuma,
                    out playerMeanSaturation);
                var playerMask = new bool[playerSample.Length];
                int playerMinX = width;
                int playerMinY = height;
                int playerMaxX = -1;
                int playerMaxY = -1;
                for (var index = 0; index < playerSample.Length; index++)
                    if (playerSample[index].a > 32)
                    {
                        playerMask[index] = true;
                        playerPixels++;
                        int x = index % width;
                        int y = index / width;
                        playerMinX = Mathf.Min(playerMinX, x);
                        playerMinY = Mathf.Min(playerMinY, y);
                        playerMaxX = Mathf.Max(playerMaxX, x);
                        playerMaxY = Mathf.Max(playerMaxY, y);
                    }
                if (playerMaxX >= playerMinX && playerMaxY >= playerMinY)
                {
                    playerWidth = playerMaxX - playerMinX + 1;
                    playerHeight = playerMaxY - playerMinY + 1;
                    MeasureUpperBodyWidths(
                        playerMask,
                        width,
                        playerMinY,
                        playerMaxY,
                        out playerHeadWidth,
                        out playerTorsoWidth);
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
                MeasureRenderedColour(
                    fatherSample,
                    out fatherMeanLuma,
                    out fatherMeanSaturation);
                var fatherMask = new bool[fatherSample.Length];
                int fatherMinX = width;
                int fatherMinY = height;
                int fatherMaxX = -1;
                int fatherMaxY = -1;
                for (var index = 0; index < fatherSample.Length; index++)
                    if (fatherSample[index].a > 32)
                    {
                        fatherMask[index] = true;
                        fatherPixels++;
                        int x = index % width;
                        int y = index / width;
                        fatherMinX = Mathf.Min(fatherMinX, x);
                        fatherMinY = Mathf.Min(fatherMinY, y);
                        fatherMaxX = Mathf.Max(fatherMaxX, x);
                        fatherMaxY = Mathf.Max(fatherMaxY, y);
                        if (playerMask[index]) overlapPixels++;
                    }
                if (fatherMaxX >= fatherMinX && fatherMaxY >= fatherMinY)
                {
                    fatherWidth = fatherMaxX - fatherMinX + 1;
                    fatherHeight = fatherMaxY - fatherMinY + 1;
                    MeasureUpperBodyWidths(
                        fatherMask,
                        width,
                        fatherMinY,
                        fatherMaxY,
                        out fatherHeadWidth,
                        out fatherTorsoWidth);
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

        private static void MeasureUpperBodyWidths(
            bool[] mask,
            int imageWidth,
            int minY,
            int maxY,
            out int headWidth,
            out int torsoWidth)
        {
            headWidth = 0;
            torsoWidth = 0;
            int silhouetteHeight = maxY - minY + 1;
            int headBottom = maxY - Mathf.CeilToInt(silhouetteHeight * 0.34f);
            int torsoTop = maxY - Mathf.FloorToInt(silhouetteHeight * 0.28f);
            int torsoBottom = maxY - Mathf.CeilToInt(silhouetteHeight * 0.68f);
            for (int y = minY; y <= maxY; y++)
            {
                int rowMin = imageWidth;
                int rowMax = -1;
                int rowOffset = y * imageWidth;
                for (int x = 0; x < imageWidth; x++)
                {
                    if (!mask[rowOffset + x])
                        continue;
                    rowMin = Mathf.Min(rowMin, x);
                    rowMax = Mathf.Max(rowMax, x);
                }
                if (rowMax < rowMin)
                    continue;
                int rowWidth = rowMax - rowMin + 1;
                if (y >= headBottom)
                    headWidth = Mathf.Max(headWidth, rowWidth);
                if (y >= torsoBottom && y <= torsoTop)
                    torsoWidth = Mathf.Max(torsoWidth, rowWidth);
            }
        }

        private static void MeasureRenderedColour(
            Color32[] sample,
            out float meanLuma,
            out float meanSaturation)
        {
            double luma = 0d;
            double saturation = 0d;
            var count = 0;
            for (var index = 0; index < sample.Length; index++)
            {
                Color32 color = sample[index];
                if (color.a <= 32)
                    continue;
                int maximum = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
                int minimum = Mathf.Min(color.r, Mathf.Min(color.g, color.b));
                luma += 0.2126d * color.r + 0.7152d * color.g + 0.0722d * color.b;
                if (maximum > 0)
                    saturation += (maximum - minimum) / (double)maximum;
                count++;
            }
            meanLuma = count == 0 ? 0f : (float)(luma / count);
            meanSaturation = count == 0 ? 0f : (float)(saturation / count);
        }

        private static float DistanceToCenterLine(
            Vector2 point,
            Vector2 origin,
            Vector2 normalizedDirection)
        {
            Vector2 delta = point - origin;
            return Mathf.Abs(delta.x * normalizedDirection.y -
                             delta.y * normalizedDirection.x);
        }

        private static bool TryMeasureTileCenterPixelAlignment(
            OfficeRuntimeAgent agent,
            string productionHostName,
            out float footMidpointPixelError,
            out Vector2 footMidpointLocalOffset)
        {
            footMidpointPixelError = 0f;
            footMidpointLocalOffset = Vector2.zero;
            Camera source = Camera.main;
            Camera overlay = Object.FindObjectsByType<Camera>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate != null && string.Equals(
                    candidate.gameObject.name,
                    "Family3DProductionOverlayCamera",
                    StringComparison.Ordinal));
            GameObject host = GameObject.Find(productionHostName);
            Animator animator = host == null
                ? null
                : host.GetComponentInChildren<Animator>(true);
            Transform leftFoot = animator?.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rightFoot = animator?.GetBoneTransform(HumanBodyBones.RightFoot);
            if (source == null || overlay == null || host == null ||
                leftFoot == null || rightFoot == null)
                return false;

            Vector3 sourcePoint = new Vector3(
                agent.Position.x,
                agent.Position.y,
                agent.transform.position.z);
            Vector3 sourceViewport = source.WorldToViewportPoint(sourcePoint);
            Ray semanticRay = overlay.ViewportPointToRay(
                new Vector3(sourceViewport.x, sourceViewport.y, 0f));
            var groundPlane = new Plane(Vector3.up, Vector3.zero);
            if (!groundPlane.Raycast(semanticRay, out float semanticDistance) ||
                semanticDistance < 0f)
                return false;
            Vector3 semanticRoot = semanticRay.GetPoint(semanticDistance);
            if (sourceViewport.z <= 0f)
                return false;

            Vector3 footMidpoint = (leftFoot.position + rightFoot.position) * 0.5f;
            footMidpoint.y = 0f;
            Vector3 hostGround = semanticRoot;
            hostGround.y = 0f;
            Vector3 localGroundOffset = host.transform.InverseTransformDirection(
                footMidpoint - hostGround);
            footMidpointLocalOffset = new Vector2(localGroundOffset.x, localGroundOffset.z);
            Vector3 footViewport = overlay.WorldToViewportPoint(footMidpoint);
            Vector3 rootViewport = overlay.WorldToViewportPoint(hostGround);
            if (footViewport.z <= 0f || rootViewport.z <= 0f)
                return false;
            footMidpointPixelError = ViewportPixelDistance(footViewport, rootViewport);
            return true;
        }

        private static float ViewportPixelDistance(Vector3 left, Vector3 right)
        {
            const float width = 1280f;
            const float height = 720f;
            return new Vector2(
                    (left.x - right.x) * width,
                    (left.y - right.y) * height)
                .magnitude;
        }

        private static int Median(List<int> values)
        {
            int[] ordered = values.OrderBy(value => value).ToArray();
            return ordered[ordered.Length / 2];
        }

        private static float Median(List<float> values)
        {
            float[] ordered = values.OrderBy(value => value).ToArray();
            int middle = ordered.Length / 2;
            return ordered.Length % 2 == 0
                ? (ordered[middle - 1] + ordered[middle]) * 0.5f
                : ordered[middle];
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
