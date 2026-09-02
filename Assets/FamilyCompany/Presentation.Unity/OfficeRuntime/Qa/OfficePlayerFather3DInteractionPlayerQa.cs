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
        private const string FootTilePhaseSweepFlag =
            "-familyCompanyFootTilePhaseSweep";
        private const string FootTilePhaseSweepAxisArgument =
            "-familyCompanyFootTilePhaseSweepAxis";
        private const string FootTileFastSweepFlag =
            "-familyCompanyFootTileFastSweep";

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

            bool footTilePhaseSweep = HasFlag(FootTilePhaseSweepFlag);
            bool measureRenderedShoes = !footTilePhaseSweep || !HasFlag(FootTileFastSweepFlag);

            // A moving head-on sample is useful for ranges, but it does not separate camera,
            // whole-body scale and head/body proportion. Put both approved one-package actors on
            // the exact same semantic tile and reset their distance clock before capturing each
            // one independently with the same camera, light and floor pixels.
            ParkOtherActors(runtime, player, father);
            var ratioReferenceCell = new OfficeGridCoordinate(6, 6);
            player.QaTeleportToCell(ratioReferenceCell);
            father.QaTeleportToCell(ratioReferenceCell);
            for (var frame = 0; frame < 3; frame++) yield return null;
            var sameTileShoeCentroidDeltaPixels = new Vector2(
                float.PositiveInfinity,
                float.PositiveInfinity);
            if (!footTilePhaseSweep &&
                !TryCaptureSameTileRatioEvidence(
                    runtime,
                    player,
                    father,
                    ratioReferenceCell,
                    artifactDirectory,
                    out sameTileShoeCentroidDeltaPixels,
                    out string ratioFailure))
            {
                Finish(false, "same-tile ratio capture failed: " + ratioFailure);
                yield break;
            }

            // Keep a full walk cycle before contact. The wider eight-cell approach is visual QA,
            // not just a collision probe: at the locked runtime speed it records more than the
            // complete 1.4-second authored cycle before the two visible bodies meet.
            bool verticalFootTileSweep = footTilePhaseSweep &&
                                         ArgumentValue(FootTilePhaseSweepAxisArgument)
                                             .Equals("y", StringComparison.OrdinalIgnoreCase);
            OfficeGridCoordinate playerStart = verticalFootTileSweep
                ? new OfficeGridCoordinate(6, 2)
                : new OfficeGridCoordinate(2, 6);
            OfficeGridCoordinate fatherStart = verticalFootTileSweep
                ? new OfficeGridCoordinate(6, 10)
                : new OfficeGridCoordinate(10, 6);
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
            var playerShoeToAgentPixelSamples = new List<Vector2>();
            var fatherShoeToAgentPixelSamples = new List<Vector2>();
            var playerAgentCenterPixelSamples = new List<Vector2>();
            float playerMinimumRenderedShoePixelTileMargin = float.PositiveInfinity;
            float fatherMinimumRenderedShoePixelTileMargin = float.PositiveInfinity;
            int playerRenderedShoeOutsideFrames = 0;
            int fatherRenderedShoeOutsideFrames = 0;
            int playerRenderedPlantedShoeOutsideFrames = 0;
            int fatherRenderedPlantedShoeOutsideFrames = 0;
            int playerRenderedShoeMeasuredFrames = 0;
            int fatherRenderedShoeMeasuredFrames = 0;
            var renderedShoePixelTrace = new StringBuilder();
            renderedShoePixelTrace.AppendLine(
                "frame,actor,left_pixels,right_pixels,left_outside_pixels," +
                "right_outside_pixels,left_min_tile_margin_px,right_min_tile_margin_px," +
                "rendered_width,rendered_height,head_width,head_height,head_to_height," +
                "shoulder_width,torso_width,leg_width,leg_height,silhouette_pixels," +
                "screen_occupation_percent,shoe_centroid_x,shoe_centroid_y," +
                "agent_center_x,agent_center_y,shoe_to_agent_x,shoe_to_agent_y");
            float playerMinimumPlantedFootLineClearancePx = float.PositiveInfinity;
            float fatherMinimumPlantedFootLineClearancePx = float.PositiveInfinity;
            int playerPlantedFootLineTouchFrames = 0;
            int fatherPlantedFootLineTouchFrames = 0;
            int playerPlantedFootContactSamples = 0;
            int fatherPlantedFootContactSamples = 0;
            var footTileTrace = new StringBuilder();
            footTileTrace.AppendLine(
                "frame,actor,phase,left_contact,right_contact,left_grid_x,left_grid_y," +
                "right_grid_x,right_grid_y,left_toe_grid_x,left_toe_grid_y," +
                "right_toe_grid_x,right_toe_grid_y,left_ankle_line_px,right_ankle_line_px," +
                "left_shoe_line_px,right_shoe_line_px,min_contact_shoe_line_px");
            float playerMaximumCenterLineError = 0f;
            float fatherMaximumCenterLineError = 0f;
            string approachFrameDirectory = Path.Combine(artifactDirectory, "approach-frames");
            if (!footTilePhaseSweep)
                Directory.CreateDirectory(approachFrameDirectory);
            int previousCaptureFramerate = Time.captureFramerate;
            Time.captureFramerate = 24;
            float minimumPairMargin = float.PositiveInfinity;
            var playerGroundSamples = new List<GroundClearanceSample>();
            var fatherGroundSamples = new List<GroundClearanceSample>();
            deadline = Time.realtimeSinceStartup + 30f;
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return new WaitForEndOfFrame();
                if (!footTilePhaseSweep)
                {
                    string approachFramePath = Path.Combine(
                        approachFrameDirectory,
                        "approach-" + approachFrameCount.ToString("D3") + ".png");
                    if (!TryCaptureOverview(approachFramePath, out string approachCaptureFailure))
                    {
                        Time.captureFramerate = previousCaptureFramerate;
                        Finish(false, "approach frame capture failed: " + approachCaptureFailure);
                        yield break;
                    }
                }
                approachFrameCount++;
                if (TryMeasureGroundClearance(
                        "PlayerV8ProductionHost",
                        out GroundClearanceSample playerGroundSample))
                    playerGroundSamples.Add(playerGroundSample);
                if (TryMeasureGroundClearance(
                        "FatherV19ProductionHost",
                        out GroundClearanceSample fatherGroundSample))
                    fatherGroundSamples.Add(fatherGroundSample);
                if (TryMeasureFootTileLineClearance(
                        runtime,
                        player,
                        "PlayerV8ProductionHost",
                        out FootTileLineSample playerTileSample))
                {
                    AppendFootTileTrace(footTileTrace, approachFrameCount - 1, "player", playerTileSample);
                    if (player.LastActualDisplacement.sqrMagnitude > 0.000000001f &&
                        playerTileSample.hasContact)
                    {
                        playerPlantedFootContactSamples++;
                        playerMinimumPlantedFootLineClearancePx = Mathf.Min(
                            playerMinimumPlantedFootLineClearancePx,
                            playerTileSample.minimumContactLineClearancePx);
                        if (playerTileSample.minimumContactLineClearancePx < 2f)
                            playerPlantedFootLineTouchFrames++;
                    }
                }
                if (TryMeasureFootTileLineClearance(
                        runtime,
                        father,
                        "FatherV19ProductionHost",
                        out FootTileLineSample fatherTileSample))
                {
                    AppendFootTileTrace(footTileTrace, approachFrameCount - 1, "father", fatherTileSample);
                    if (father.LastActualDisplacement.sqrMagnitude > 0.000000001f &&
                        fatherTileSample.hasContact)
                    {
                        fatherPlantedFootContactSamples++;
                        fatherMinimumPlantedFootLineClearancePx = Mathf.Min(
                            fatherMinimumPlantedFootLineClearancePx,
                            fatherTileSample.minimumContactLineClearancePx);
                        if (fatherTileSample.minimumContactLineClearancePx < 2f)
                            fatherPlantedFootLineTouchFrames++;
                    }
                }
                // Exact sweeps use the same real skinned-shoe pixel measurement as final evidence.
                // A fast proxy-only sweep exists only to prune a large fixed-parameter grid; every
                // shortlisted and final value must rerun the exact renderer below.
                if (measureRenderedShoes)
                {
                    if (!TryMeasureRenderedShoePixelTileContainment(
                            runtime,
                            player,
                            "PlayerV8ProductionHost",
                            out RenderedShoePixelSample playerShoePixels,
                            out string playerShoeFailure))
                    {
                        Time.captureFramerate = previousCaptureFramerate;
                        Finish(false, "player rendered-shoe pixel measurement failed: " +
                                      playerShoeFailure);
                        yield break;
                    }
                    AppendRenderedShoePixelTrace(
                        renderedShoePixelTrace,
                        approachFrameCount - 1,
                        "player",
                        playerShoePixels);
                    playerShoeToAgentPixelSamples.Add(playerShoePixels.shoeToAgentPixels);
                    playerAgentCenterPixelSamples.Add(playerShoePixels.agentCenterPixels);
                    playerRenderedShoeMeasuredFrames++;
                    playerMinimumRenderedShoePixelTileMargin = Mathf.Min(
                        playerMinimumRenderedShoePixelTileMargin,
                        Mathf.Min(
                            playerShoePixels.leftMinimumTileMarginPixels,
                            playerShoePixels.rightMinimumTileMarginPixels));
                    if (playerShoePixels.leftOutsidePixelCount > 0 ||
                        playerShoePixels.rightOutsidePixelCount > 0)
                        playerRenderedShoeOutsideFrames++;
                    if ((playerTileSample.leftContact &&
                         playerShoePixels.leftOutsidePixelCount > 0) ||
                        (playerTileSample.rightContact &&
                         playerShoePixels.rightOutsidePixelCount > 0))
                        playerRenderedPlantedShoeOutsideFrames++;

                    if (!TryMeasureRenderedShoePixelTileContainment(
                            runtime,
                            father,
                            "FatherV19ProductionHost",
                            out RenderedShoePixelSample fatherShoePixels,
                            out string fatherShoeFailure))
                    {
                        Time.captureFramerate = previousCaptureFramerate;
                        Finish(false, "father rendered-shoe pixel measurement failed: " +
                                      fatherShoeFailure);
                        yield break;
                    }
                    AppendRenderedShoePixelTrace(
                        renderedShoePixelTrace,
                        approachFrameCount - 1,
                        "father",
                        fatherShoePixels);
                    fatherShoeToAgentPixelSamples.Add(fatherShoePixels.shoeToAgentPixels);
                    fatherRenderedShoeMeasuredFrames++;
                    fatherMinimumRenderedShoePixelTileMargin = Mathf.Min(
                        fatherMinimumRenderedShoePixelTileMargin,
                        Mathf.Min(
                            fatherShoePixels.leftMinimumTileMarginPixels,
                            fatherShoePixels.rightMinimumTileMarginPixels));
                    if (fatherShoePixels.leftOutsidePixelCount > 0 ||
                        fatherShoePixels.rightOutsidePixelCount > 0)
                        fatherRenderedShoeOutsideFrames++;
                    if ((fatherTileSample.leftContact &&
                         fatherShoePixels.leftOutsidePixelCount > 0) ||
                        (fatherTileSample.rightContact &&
                         fatherShoePixels.rightOutsidePixelCount > 0))
                        fatherRenderedPlantedShoeOutsideFrames++;
                }
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
            File.WriteAllText(
                Path.Combine(artifactDirectory, "player-father-foot-tile-trace.csv"),
                footTileTrace.ToString(),
                new UTF8Encoding(false));
            File.WriteAllText(
                Path.Combine(
                    artifactDirectory,
                    "player-father-rendered-shoe-pixel-tile-trace.csv"),
                renderedShoePixelTrace.ToString(),
                new UTF8Encoding(false));
            player.QaSetDirectMovementInput(Vector2.zero);
            father.QaSetDirectMovementInput(Vector2.zero);
            for (var frame = 0; frame < 3; frame++) yield return null;

            if (footTilePhaseSweep)
            {
                string sweepResult =
                    "minimumPlantedShoeTileLineClearancePx=" +
                    Invariant(playerMinimumPlantedFootLineClearancePx) + "/" +
                    Invariant(fatherMinimumPlantedFootLineClearancePx) + Environment.NewLine +
                    "plantedShoeTileLineTouchFrames=" +
                    playerPlantedFootLineTouchFrames + "/" +
                    fatherPlantedFootLineTouchFrames + Environment.NewLine +
                    "minimumRenderedShoePixelTileMarginPx=" +
                    Invariant(playerMinimumRenderedShoePixelTileMargin) + "/" +
                    Invariant(fatherMinimumRenderedShoePixelTileMargin) + Environment.NewLine +
                    "renderedShoePixelOutsideFrames=" +
                    playerRenderedShoeOutsideFrames + "/" +
                    fatherRenderedShoeOutsideFrames + Environment.NewLine +
                    "renderedPlantedShoePixelOutsideFrames=" +
                    playerRenderedPlantedShoeOutsideFrames + "/" +
                    fatherRenderedPlantedShoeOutsideFrames + Environment.NewLine +
                    "renderedShoePixelMeasuredFrames=" +
                    playerRenderedShoeMeasuredFrames + "/" +
                    fatherRenderedShoeMeasuredFrames + Environment.NewLine +
                    "frames=" + approachFrameCount + Environment.NewLine;
                File.WriteAllText(
                    Path.Combine(artifactDirectory, "player-father-foot-tile-sweep-result.txt"),
                    sweepResult,
                    new UTF8Encoding(false));
                Finish(true, "foot tile phase sweep completed");
                yield break;
            }

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
            float playerMedianShoeLaneOffsetPixels = float.PositiveInfinity;
            float fatherMedianShoeLaneOffsetPixels = float.PositiveInfinity;
            float dynamicShoeLaneDeltaPixels = float.PositiveInfinity;
            if (playerAgentCenterPixelSamples.Count >= 2 &&
                playerShoeToAgentPixelSamples.Count == playerAgentCenterPixelSamples.Count &&
                fatherShoeToAgentPixelSamples.Count == playerAgentCenterPixelSamples.Count)
            {
                Vector2 screenTravel =
                    playerAgentCenterPixelSamples[playerAgentCenterPixelSamples.Count - 1] -
                    playerAgentCenterPixelSamples[0];
                if (screenTravel.sqrMagnitude > 0.0001f)
                {
                    Vector2 direction = screenTravel.normalized;
                    var laneNormal = new Vector2(-direction.y, direction.x);
                    playerMedianShoeLaneOffsetPixels = Median(
                        playerShoeToAgentPixelSamples
                            .Select(sample => Vector2.Dot(sample, laneNormal))
                            .ToList());
                    fatherMedianShoeLaneOffsetPixels = Median(
                        fatherShoeToAgentPixelSamples
                            .Select(sample => Vector2.Dot(sample, laneNormal))
                            .ToList());
                    dynamicShoeLaneDeltaPixels =
                        fatherMedianShoeLaneOffsetPixels - playerMedianShoeLaneOffsetPixels;
                }
            }
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
            float playerGroundMeshMedian = GroundClearanceMedian(
                playerGroundSamples, sample => sample.bakedMeshMinY);
            float fatherGroundMeshMedian = GroundClearanceMedian(
                fatherGroundSamples, sample => sample.bakedMeshMinY);
            float groundClearanceMeshDelta = fatherGroundMeshMedian - playerGroundMeshMedian;
            if (playerTravel < 0.25f || fatherTravel < 0.25f || approachFrameCount < 24 ||
                playerHeightSamples.Count < 4 || playerFootMidpointPixelErrors.Count < 4 ||
                blockedAgentMoves <= 0 || minimumPairMargin > 0.08f ||
                minimumPairMargin < -0.0105f || approachPenetrations != 0 ||
                playerStartTileCenterError > 0.0001f || fatherStartTileCenterError > 0.0001f ||
                playerMaximumCenterLineError > 0.0005f ||
                fatherMaximumCenterLineError > 0.0005f ||
                (legacy2DScaleCandidate &&
                 (playerMedianFootMidpointPixelError > 4f ||
                  playerMaximumFootMidpointPixelError > 8f ||
                  // Father is gated on the same bone-based foot-midpoint tile error as Player.
                  // dynamicShoeLaneDeltaPixels stays informational only: it projects a 2D shoe
                  // pixel centroid whose height differs per shoe mesh, so equal lane medians were
                  // reached by moving Father's feet onto the tile corner (2026-09-02 candidates).
                  fatherMedianFootMidpointPixelError > 4f ||
                  fatherMaximumFootMidpointPixelError > 8f ||
                  // Same visual floor: the lowest skinned vertex over the walk may not float
                  // more than 0.05 office units apart between the two actors.
                  playerGroundSamples.Count < 24 ||
                  fatherGroundSamples.Count < 24 ||
                  !(Mathf.Abs(groundClearanceMeshDelta) <= 0.05f) ||
                  playerPlantedFootContactSamples < 24 ||
                  fatherPlantedFootContactSamples < 24 ||
                  playerRenderedShoeMeasuredFrames != approachFrameCount ||
                  fatherRenderedShoeMeasuredFrames != approachFrameCount)))
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
                    " sameTileShoeCentroidDeltaPx=" +
                    sameTileShoeCentroidDeltaPixels.x.ToString("F3") + "/" +
                    sameTileShoeCentroidDeltaPixels.y.ToString("F3") +
                    " groundClearanceMeshMedian=" + playerGroundMeshMedian.ToString("F4") + "/" +
                    fatherGroundMeshMedian.ToString("F4") +
                    " dynamicShoeLaneMedianPx=" +
                    playerMedianShoeLaneOffsetPixels.ToString("F3") + "/" +
                    fatherMedianShoeLaneOffsetPixels.ToString("F3") +
                    " delta=" + dynamicShoeLaneDeltaPixels.ToString("F3") +
                    " plantedLinePx=" +
                    playerMinimumPlantedFootLineClearancePx.ToString("F3") + "/" +
                    fatherMinimumPlantedFootLineClearancePx.ToString("F3") +
                    " plantedLineTouches=" + playerPlantedFootLineTouchFrames + "/" +
                    fatherPlantedFootLineTouchFrames +
                    " renderedShoeMarginPx=" +
                    playerMinimumRenderedShoePixelTileMargin.ToString("F3") + "/" +
                    fatherMinimumRenderedShoePixelTileMargin.ToString("F3") +
                    " renderedShoeOutsideFrames=" +
                    playerRenderedShoeOutsideFrames + "/" +
                    fatherRenderedShoeOutsideFrames +
                    " renderedPlantedShoeOutsideFrames=" +
                    playerRenderedPlantedShoeOutsideFrames + "/" +
                    fatherRenderedPlantedShoeOutsideFrames +
                    " renderedShoeMeasuredFrames=" +
                    playerRenderedShoeMeasuredFrames + "/" +
                    fatherRenderedShoeMeasuredFrames +
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

            // Separate, deterministic whole-body turn proof.  The long head-on sequence above
            // covers more than two complete action-613 loops, while this L-shaped input records
            // the production 0.18 s yaw blend without changing bones or applying framewise root
            // corrections.  It is QA-only setup; the real desk routes below still run afterward.
            string turnFrameDirectory = Path.Combine(artifactDirectory, "turn-frames");
            Directory.CreateDirectory(turnFrameDirectory);
            player.QaTeleportToCell(new OfficeGridCoordinate(10, 10));
            father.QaTeleportToCell(new OfficeGridCoordinate(4, 6));
            ParkOtherActors(runtime, player, father);
            for (var frame = 0; frame < 3; frame++) yield return null;
            previousCaptureFramerate = Time.captureFramerate;
            Time.captureFramerate = 24;
            const int firstTurnLegFrames = 22;
            const int totalTurnFrames = 48;
            for (var frame = 0; frame < totalTurnFrames; frame++)
            {
                player.QaSetDirectMovementInput(Vector2.zero);
                father.QaSetDirectMovementInput(
                    frame < firstTurnLegFrames ? Vector2.right : Vector2.up);
                yield return new WaitForEndOfFrame();
                string turnFramePath = Path.Combine(
                    turnFrameDirectory,
                    "turn-" + frame.ToString("D3") + ".png");
                if (!TryCaptureOverview(turnFramePath, out string turnCaptureFailure))
                {
                    father.QaSetDirectMovementInput(Vector2.zero);
                    Time.captureFramerate = previousCaptureFramerate;
                    Finish(false, "turn frame capture failed: " + turnCaptureFailure);
                    yield break;
                }
            }
            father.QaSetDirectMovementInput(Vector2.zero);
            Time.captureFramerate = previousCaptureFramerate;
            for (var frame = 0; frame < 3; frame++) yield return null;

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

            // Desk detour proof. The straight lines (3,8)->(3,2) and (7,8)->(11,8) cross the blocking
            // V31 desk footprints at cells (3..4,5) and (9..10,8), so both agents must route around a
            // desk. Every frame position is traced and every second frame is captured.
            var detourPlayerTarget = new OfficeGridCoordinate(3, 2);
            var detourFatherTarget = new OfficeGridCoordinate(11, 8);
            player.QaTeleportToCell(new OfficeGridCoordinate(3, 8));
            father.QaTeleportToCell(new OfficeGridCoordinate(7, 8));
            for (var frame = 0; frame < 3; frame++) yield return null;
            bool playerDetourAccepted = player.QaMoveToCell(detourPlayerTarget, "player-father-desk-detour");
            bool fatherDetourAccepted = father.QaMoveToCell(detourFatherTarget, "player-father-desk-detour");
            string detourFrameDirectory = Path.Combine(artifactDirectory, "detour-frames");
            Directory.CreateDirectory(detourFrameDirectory);
            var detourTrace = new StringBuilder();
            detourTrace.AppendLine(
                "frame,actor,phase,grid_x,grid_y,position_x,position_y,radius," +
                "desk_penetrating_vertices,desk_min_clearance,body_reach");
            Vector2 detourOrigin = runtime.World.Presenter.CellCenterWorld(new OfficeGridCoordinate(0, 0));
            Vector2 detourBasisX = runtime.World.Presenter.CellBasisXWorld();
            Vector2 detourBasisY = runtime.World.Presenter.CellBasisYWorld();
            var detourFrame = 0;
            var detourCaptured = 0;
            // Visible-body versus desk geometry: every skinned vertex of each actor is tested
            // against the desk/CRT/keyboard renderer bounds each frame. Index 0 = Player, 1 = Father.
            Renderer[] deskParts = FindDeskPartRenderers();
            var deskPenetrationFrames = new int[2];
            var deskMaxPenetratingVertices = new int[2];
            var deskMinVertexClearance = new[] { float.PositiveInfinity, float.PositiveInfinity };
            var deskBodyHorizontalReach = new float[2];
            deadline = Time.realtimeSinceStartup + 150f;
            while (Time.realtimeSinceStartup < deadline && detourFrame < 700 &&
                   !(player.QaReachedCell(detourPlayerTarget) && father.QaReachedCell(detourFatherTarget)))
            {
                yield return new WaitForEndOfFrame();
                foreach (OfficeRuntimeAgent detourActor in new[] { player, father })
                {
                    TryResolveGridCoordinate(
                        detourActor.Position, detourOrigin, detourBasisX, detourBasisY, out Vector2 detourGrid);
                    int actorIndex = detourActor == player ? 0 : 1;
                    bool measured = MeasureDeskMeshPenetration(
                        actorIndex == 0 ? "PlayerV8ProductionHost" : "FatherV19ProductionHost",
                        deskParts,
                        out int penetratingVertices,
                        out float vertexClearance,
                        out float horizontalReach);
                    detourTrace.Append(detourFrame).Append(',')
                        .Append(detourActor == player ? "player" : "father").Append(',')
                        .Append(detourActor.Phase).Append(',')
                        .Append(Invariant(detourGrid.x)).Append(',')
                        .Append(Invariant(detourGrid.y)).Append(',')
                        .Append(Invariant(detourActor.Position.x)).Append(',')
                        .Append(Invariant(detourActor.Position.y)).Append(',')
                        .Append(Invariant(detourActor.AgentRadius)).Append(',')
                        .Append(measured ? penetratingVertices : -1).Append(',')
                        .Append(measured ? Invariant(vertexClearance) : "nan").Append(',')
                        .Append(measured ? Invariant(horizontalReach) : "nan").AppendLine();
                    if (measured)
                    {
                        deskBodyHorizontalReach[actorIndex] = Mathf.Max(
                            deskBodyHorizontalReach[actorIndex], horizontalReach);
                        if (penetratingVertices > 0) deskPenetrationFrames[actorIndex]++;
                        deskMaxPenetratingVertices[actorIndex] = Mathf.Max(
                            deskMaxPenetratingVertices[actorIndex], penetratingVertices);
                        deskMinVertexClearance[actorIndex] = Mathf.Min(
                            deskMinVertexClearance[actorIndex], vertexClearance);
                    }
                }
                if (detourFrame % 2 == 0 && detourCaptured < 400 &&
                    TryCaptureOverview(
                        Path.Combine(detourFrameDirectory, "detour-" + detourFrame.ToString("D3") + ".png"),
                        out _))
                    detourCaptured++;
                detourFrame++;
            }
            bool playerDetourReached = player.QaReachedCell(detourPlayerTarget);
            bool fatherDetourReached = father.QaReachedCell(detourFatherTarget);
            int detourStaticViolations = runtime.World.Occupancy.StaticViolationCount;
            int detourInteractionViolations = runtime.World.Occupancy.InteractionViolationCount;
            File.WriteAllText(
                Path.Combine(artifactDirectory, "player-father-desk-detour-trace.csv"),
                detourTrace.ToString());
            if (!playerDetourAccepted || !fatherDetourAccepted || !playerDetourReached || !fatherDetourReached)
            {
                Finish(
                    false,
                    "desk detour did not complete accepted=" + playerDetourAccepted + "/" + fatherDetourAccepted +
                    " reached=" + playerDetourReached + "/" + fatherDetourReached +
                    " frames=" + detourFrame +
                    " playerBlocker=" + player.LastMovementBlocker +
                    " fatherBlocker=" + father.LastMovementBlocker);
                yield break;
            }
            player.QaTeleportToCell(new OfficeGridCoordinate(1, 1));
            father.QaTeleportToCell(new OfficeGridCoordinate(11, 11));
            for (var frame = 0; frame < 3; frame++) yield return null;

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

            // Seat-route evidence: every third rendered frame until both actors are Working, plus
            // per-frame agent grid positions and the blocking furniture footprints, so the review
            // can prove the walk goes around the V31 desks rather than through them.
            string routeFrameDirectory = Path.Combine(artifactDirectory, "route-frames");
            Directory.CreateDirectory(routeFrameDirectory);
            var routeTrace = new StringBuilder();
            routeTrace.AppendLine("frame,actor,phase,grid_x,grid_y,position_x,position_y,radius");
            Vector2 routeOrigin = runtime.World.Presenter.CellCenterWorld(new OfficeGridCoordinate(0, 0));
            Vector2 routeBasisX = runtime.World.Presenter.CellBasisXWorld();
            Vector2 routeBasisY = runtime.World.Presenter.CellBasisYWorld();
            var routeFrame = 0;
            var routeCaptured = 0;
            deadline = Time.realtimeSinceStartup + 40f;
            while (Time.realtimeSinceStartup < deadline &&
                   (player.Phase != OfficeRuntimeAgentPhase.Working ||
                    father.Phase != OfficeRuntimeAgentPhase.Working))
            {
                yield return new WaitForEndOfFrame();
                foreach (OfficeRuntimeAgent routeActor in new[] { player, father })
                {
                    TryResolveGridCoordinate(
                        routeActor.Position, routeOrigin, routeBasisX, routeBasisY, out Vector2 routeGrid);
                    routeTrace.Append(routeFrame).Append(',')
                        .Append(routeActor == player ? "player" : "father").Append(',')
                        .Append(routeActor.Phase).Append(',')
                        .Append(Invariant(routeGrid.x)).Append(',')
                        .Append(Invariant(routeGrid.y)).Append(',')
                        .Append(Invariant(routeActor.Position.x)).Append(',')
                        .Append(Invariant(routeActor.Position.y)).Append(',')
                        .Append(Invariant(routeActor.AgentRadius)).AppendLine();
                }
                if (routeFrame % 3 == 0 && routeCaptured < 400 &&
                    TryCaptureOverview(
                        Path.Combine(routeFrameDirectory, "route-" + routeFrame.ToString("D3") + ".png"),
                        out _))
                    routeCaptured++;
                routeFrame++;
            }
            File.WriteAllText(Path.Combine(artifactDirectory, "player-father-route-trace.csv"), routeTrace.ToString());
            var footprints = new StringBuilder();
            footprints.AppendLine("furniture_id,blocks_movement,origin_x,origin_y,width,height");
            foreach (PlacedOfficeFurniture furniture in runtime.World.Grid.Furniture)
                footprints.Append(furniture.FurnitureId).Append(',')
                    .Append(furniture.BlocksMovement).Append(',')
                    .Append(furniture.Origin.X).Append(',').Append(furniture.Origin.Y).Append(',')
                    .Append(furniture.Width).Append(',').Append(furniture.Height).AppendLine();
            File.WriteAllText(Path.Combine(artifactDirectory, "office-furniture-footprints.csv"), footprints.ToString());
            var partBounds = new StringBuilder();
            partBounds.AppendLine("part,min_x,min_y,min_z,max_x,max_y,max_z");
            foreach (Renderer part in FindDeskPartRenderers())
            {
                Bounds bounds = part.bounds;
                partBounds.Append(part.transform.parent == null ? part.name : part.transform.parent.name + "/" + part.name).Append(',')
                    .Append(Invariant(bounds.min.x)).Append(',').Append(Invariant(bounds.min.y)).Append(',').Append(Invariant(bounds.min.z)).Append(',')
                    .Append(Invariant(bounds.max.x)).Append(',').Append(Invariant(bounds.max.y)).Append(',').Append(Invariant(bounds.max.z)).AppendLine();
            }
            File.WriteAllText(Path.Combine(artifactDirectory, "office-desk-part-bounds.csv"), partBounds.ToString());
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
            result.AppendLine(
                "FAMILY_COMPANY_PLAYER_FATHER_3D_INTERACTION: " +
                (legacy2DScaleCandidate
                    ? "CANDIDATE_USER_APPROVAL_REQUIRED"
                    : "PASS"));
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
            result.AppendLine("sameTileShoeCentroidDeltaPx=" +
                              sameTileShoeCentroidDeltaPixels.x.ToString("F3") + "/" +
                              sameTileShoeCentroidDeltaPixels.y.ToString("F3"));
            // Vertical grounding, world units above the host ground: lowest Foot/Toes bone,
            // lowest baked skinned vertex, renderer bounds minimum. Order Player/Father, median/min.
            result.AppendLine("walkGroundClearanceBoneY=" + FormatGroundClearance(
                                  playerGroundSamples, fatherGroundSamples, sample => sample.boneMinY));
            result.AppendLine("walkGroundClearanceMeshY=" + FormatGroundClearance(
                                  playerGroundSamples, fatherGroundSamples, sample => sample.bakedMeshMinY));
            result.AppendLine("walkGroundClearanceBoundsY=" + FormatGroundClearance(
                                  playerGroundSamples, fatherGroundSamples, sample => sample.boundsMinY));
            result.AppendLine("dynamicShoeLaneOffsetMedianPx=" +
                              playerMedianShoeLaneOffsetPixels.ToString("F3") + "/" +
                              fatherMedianShoeLaneOffsetPixels.ToString("F3") + "/" +
                              dynamicShoeLaneDeltaPixels.ToString("F3"));
            result.AppendLine("minimumPlantedShoeTileLineClearancePx=" +
                              playerMinimumPlantedFootLineClearancePx.ToString("F3") + "/" +
                              fatherMinimumPlantedFootLineClearancePx.ToString("F3"));
            result.AppendLine("plantedFootContactSamples=" +
                              playerPlantedFootContactSamples + "/" +
                              fatherPlantedFootContactSamples);
            result.AppendLine("plantedShoeTileLineTouchFrames=" +
                              playerPlantedFootLineTouchFrames + "/" +
                              fatherPlantedFootLineTouchFrames);
            result.AppendLine("minimumRenderedShoePixelTileMarginPx=" +
                              playerMinimumRenderedShoePixelTileMargin.ToString("F3") + "/" +
                              fatherMinimumRenderedShoePixelTileMargin.ToString("F3"));
            result.AppendLine("renderedShoePixelOutsideFrames=" +
                              playerRenderedShoeOutsideFrames + "/" +
                              fatherRenderedShoeOutsideFrames);
            result.AppendLine("renderedPlantedShoePixelOutsideFrames=" +
                              playerRenderedPlantedShoeOutsideFrames + "/" +
                              fatherRenderedPlantedShoeOutsideFrames);
            result.AppendLine("renderedShoePixelMeasuredFrames=" +
                              playerRenderedShoeMeasuredFrames + "/" +
                              fatherRenderedShoeMeasuredFrames);
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
            result.AppendLine("deskDetourFrames=" + detourFrame);
            result.AppendLine("deskDetourReached=" + playerDetourReached + "/" + fatherDetourReached);
            result.AppendLine("deskDetourStaticViolations=" + detourStaticViolations);
            result.AppendLine("deskDetourInteractionViolations=" + detourInteractionViolations);
            result.AppendLine("deskDetourMeshPenetrationFrames=" +
                              deskPenetrationFrames[0] + "/" + deskPenetrationFrames[1]);
            result.AppendLine("deskDetourMaxPenetratingVertices=" +
                              deskMaxPenetratingVertices[0] + "/" + deskMaxPenetratingVertices[1]);
            result.AppendLine("deskDetourMinVertexToDeskXZ=" +
                              deskMinVertexClearance[0].ToString("F4") + "/" +
                              deskMinVertexClearance[1].ToString("F4"));
            // Largest horizontal distance of any visible vertex from the agent centre while walking
            // (arm swing included): the static clearance radius must cover this to keep the body
            // out of furniture.
            result.AppendLine("walkBodyHorizontalReach=" +
                              deskBodyHorizontalReach[0].ToString("F4") + "/" +
                              deskBodyHorizontalReach[1].ToString("F4"));
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

        private static Renderer[] FindDeskPartRenderers()
        {
            return Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(renderer => renderer != null && renderer.enabled &&
                                   (renderer.gameObject.name.StartsWith("Desk_", StringComparison.Ordinal) ||
                                    renderer.gameObject.name.StartsWith("Crt_", StringComparison.Ordinal) ||
                                    renderer.gameObject.name.StartsWith("Keyboard_", StringComparison.Ordinal)))
                .ToArray();
        }

        /// <summary>
        /// Counts skinned vertices of one production host that lie inside any desk/CRT/keyboard
        /// renderer bounds (visible body inside furniture), and returns the smallest horizontal
        /// distance from any vertex below the part's top to that part's XZ rectangle.
        /// </summary>
        private static bool MeasureDeskMeshPenetration(
            string hostName,
            Renderer[] deskParts,
            out int penetratingVertices,
            out float minimumClearance,
            out float horizontalReach)
        {
            penetratingVertices = 0;
            minimumClearance = float.PositiveInfinity;
            horizontalReach = 0f;
            GameObject host = GameObject.Find(hostName);
            SkinnedMeshRenderer skinned = host == null ? null : host.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (skinned == null || skinned.sharedMesh == null || deskParts == null || deskParts.Length == 0)
                return false;
            Vector3 hostPosition = host.transform.position;
            // Desk parts are grid-aligned boxes rotated against the world axes, so their world AABB
            // over-reports overlap. Test each vertex in the part's own local space against the
            // mesh's local bounds instead (exact for the authored box/cylinder parts).
            var nearby = new List<DeskPartBox>();
            foreach (Renderer part in deskParts)
            {
                if (part == null) continue;
                Bounds bounds = part.bounds;
                Vector3 closest = bounds.ClosestPoint(hostPosition);
                if (new Vector2(closest.x - hostPosition.x, closest.z - hostPosition.z).magnitude > 2.5f)
                    continue;
                MeshFilter filter = part.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                nearby.Add(new DeskPartBox
                {
                    Transform = part.transform,
                    LocalBounds = filter.sharedMesh.bounds,
                    Scale = part.transform.lossyScale,
                    WorldTop = bounds.max.y
                });
            }
            var baked = new Mesh();
            skinned.BakeMesh(baked, true);
            var vertices = new List<Vector3>(skinned.sharedMesh.vertexCount);
            baked.GetVertices(vertices);
            Transform rendererTransform = skinned.transform;
            Vector3 rendererPosition = rendererTransform.position;
            Quaternion rendererRotation = rendererTransform.rotation;
            // Every fifth vertex keeps the 209k-vertex meshes measurable within one QA frame budget.
            for (var index = 0; index < vertices.Count; index += 5)
            {
                Vector3 world = rendererPosition + rendererRotation * vertices[index];
                float reach = new Vector2(world.x - hostPosition.x, world.z - hostPosition.z).magnitude;
                if (reach > horizontalReach) horizontalReach = reach;
                var inside = false;
                for (var partIndex = 0; partIndex < nearby.Count; partIndex++)
                {
                    DeskPartBox box = nearby[partIndex];
                    Vector3 local = box.Transform.InverseTransformPoint(world);
                    Bounds bounds = box.LocalBounds;
                    if (bounds.Contains(local))
                    {
                        inside = true;
                        break;
                    }
                    if (world.y > box.WorldTop) continue;
                    float dx = Mathf.Max(bounds.min.x - local.x, 0f, local.x - bounds.max.x) * Mathf.Abs(box.Scale.x);
                    float dz = Mathf.Max(bounds.min.z - local.z, 0f, local.z - bounds.max.z) * Mathf.Abs(box.Scale.z);
                    float clearance = Mathf.Sqrt(dx * dx + dz * dz);
                    if (clearance < minimumClearance) minimumClearance = clearance;
                }
                if (inside)
                {
                    penetratingVertices++;
                    minimumClearance = 0f;
                }
            }
            Object.Destroy(baked);
            return true;
        }

        private struct DeskPartBox
        {
            public Transform Transform;
            public Bounds LocalBounds;
            public Vector3 Scale;
            public float WorldTop;
        }

        private struct GroundClearanceSample
        {
            public float boneMinY;
            public float bakedMeshMinY;
            public float boundsMinY;
        }

        /// <summary>
        /// Vertical grounding of one production host, in world units above the host ground point:
        /// the lowest Foot/Toes bone, the lowest actually skinned vertex (BakeMesh) and the renderer
        /// bounds minimum used by the presenter ground lift. A mesh minimum well above zero means
        /// the visible soles float and read as standing on the far tile line in the isometric view.
        /// </summary>
        private static bool TryMeasureGroundClearance(string hostName, out GroundClearanceSample sample)
        {
            sample = default;
            GameObject host = GameObject.Find(hostName);
            if (host == null) return false;
            Animator animator = host.GetComponentInChildren<Animator>(true);
            SkinnedMeshRenderer skinned = host.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (animator == null || skinned == null || skinned.sharedMesh == null) return false;
            // Production ground plane is world y = 0 (Plane(Vector3.up, zero) in the presenter);
            // the host transform itself may carry the candidate standing ground correction.
            const float groundY = 0f;
            float boneMin = float.PositiveInfinity;
            foreach (HumanBodyBones bone in new[]
                     {
                         HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot,
                         HumanBodyBones.LeftToes, HumanBodyBones.RightToes
                     })
            {
                Transform boneTransform = animator.GetBoneTransform(bone);
                if (boneTransform != null)
                    boneMin = Mathf.Min(boneMin, boneTransform.position.y - groundY);
            }
            if (float.IsInfinity(boneMin)) return false;
            var baked = new Mesh();
            skinned.BakeMesh(baked, true);
            Vector3[] vertices = baked.vertices;
            Transform rendererTransform = skinned.transform;
            float meshMin = float.PositiveInfinity;
            for (var index = 0; index < vertices.Length; index++)
            {
                Vector3 world = rendererTransform.position + rendererTransform.rotation * vertices[index];
                if (world.y < meshMin) meshMin = world.y;
            }
            Object.Destroy(baked);
            if (float.IsInfinity(meshMin)) return false;
            sample = new GroundClearanceSample
            {
                boneMinY = boneMin,
                bakedMeshMinY = meshMin - groundY,
                boundsMinY = skinned.bounds.min.y - groundY
            };
            return true;
        }

        private static string FormatGroundClearance(
            List<GroundClearanceSample> playerSamples,
            List<GroundClearanceSample> fatherSamples,
            Func<GroundClearanceSample, float> selector)
        {
            return GroundClearanceMedian(playerSamples, selector).ToString("F4") + "/" +
                   GroundClearanceMinimum(playerSamples, selector).ToString("F4") + "/" +
                   GroundClearanceMedian(fatherSamples, selector).ToString("F4") + "/" +
                   GroundClearanceMinimum(fatherSamples, selector).ToString("F4");
        }

        private static float GroundClearanceMedian(
            List<GroundClearanceSample> samples,
            Func<GroundClearanceSample, float> selector)
        {
            if (samples.Count == 0) return float.NaN;
            List<float> values = samples.Select(selector).OrderBy(value => value).ToList();
            int middle = values.Count / 2;
            return values.Count % 2 == 1 ? values[middle] : 0.5f * (values[middle - 1] + values[middle]);
        }

        private static float GroundClearanceMinimum(
            List<GroundClearanceSample> samples,
            Func<GroundClearanceSample, float> selector)
        {
            return samples.Count == 0 ? float.NaN : samples.Min(selector);
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

        private static bool TryCaptureSameTileRatioEvidence(
            StarterOfficeRuntimeBootstrap runtime,
            OfficeRuntimeAgent player,
            OfficeRuntimeAgent father,
            OfficeGridCoordinate referenceCell,
            string outputDirectory,
            out Vector2 shoeCentroidDeltaPixels,
            out string failure)
        {
            shoeCentroidDeltaPixels = new Vector2(
                float.PositiveInfinity,
                float.PositiveInfinity);
            failure = string.Empty;
            Vector2 expectedCenter = runtime.World.Presenter.CellCenterWorld(referenceCell);
            if (Vector2.Distance(player.Position, expectedCenter) > 0.0001f ||
                Vector2.Distance(father.Position, expectedCenter) > 0.0001f)
            {
                failure = "actors are not on the exact same reference tile";
                return false;
            }

            if (!TryCaptureOverviewWithOnlyProductionHost(
                    "PlayerV8ProductionHost",
                    Path.Combine(outputDirectory, "ratio-player-same-tile.png"),
                    out failure) ||
                !TryCaptureOverviewWithOnlyProductionHost(
                    "FatherV19ProductionHost",
                    Path.Combine(outputDirectory, "ratio-father-same-tile.png"),
                    out failure))
                return false;

            if (!TryRenderActorMaskAndMeasureBody(
                    "PlayerV8ProductionHost",
                    Path.Combine(outputDirectory, "ratio-player-isolated.png"),
                    out ActorBodyPixelMetrics playerMetrics,
                    out failure) ||
                !TryRenderActorMaskAndMeasureBody(
                    "FatherV19ProductionHost",
                    Path.Combine(outputDirectory, "ratio-father-isolated.png"),
                    out ActorBodyPixelMetrics fatherMetrics,
                    out failure))
                return false;

            shoeCentroidDeltaPixels = new Vector2(
                fatherMetrics.shoeCentroidX - playerMetrics.shoeCentroidX,
                fatherMetrics.shoeCentroidY - playerMetrics.shoeCentroidY);

            var result = new StringBuilder();
            result.AppendLine("FATHER_PLAYER_SAME_TILE_PIXEL_RATIO: CAPTURED_USER_REVIEW_REQUIRED");
            if (TryMeasureGroundClearance("PlayerV8ProductionHost", out GroundClearanceSample playerGround) &&
                TryMeasureGroundClearance("FatherV19ProductionHost", out GroundClearanceSample fatherGround))
            {
                result.AppendLine("sameTileGroundClearanceBoneY=" +
                                  playerGround.boneMinY.ToString("F4") + "/" + fatherGround.boneMinY.ToString("F4"));
                result.AppendLine("sameTileGroundClearanceMeshY=" +
                                  playerGround.bakedMeshMinY.ToString("F4") + "/" + fatherGround.bakedMeshMinY.ToString("F4"));
                result.AppendLine("sameTileGroundClearanceBoundsY=" +
                                  playerGround.boundsMinY.ToString("F4") + "/" + fatherGround.boundsMinY.ToString("F4"));
            }
            result.AppendLine("productionEligible=False");
            result.AppendLine("referenceCell=" + referenceCell.X + "," + referenceCell.Y);
            result.AppendLine("sameCameraLightTile=True");
            result.AppendLine("measurementResolution=1280x720");
            AppendActorBodyPixelMetrics(result, "player", playerMetrics);
            AppendActorBodyPixelMetrics(result, "father", fatherMetrics);
            result.AppendLine("fatherToPlayerHeightRatio=" +
                              Invariant(fatherMetrics.height / (float)playerMetrics.height));
            result.AppendLine("fatherToPlayerHeadHeightRatio=" +
                              Invariant(fatherMetrics.headHeight / (float)playerMetrics.headHeight));
            result.AppendLine("fatherToPlayerHeadWidthRatio=" +
                              Invariant(fatherMetrics.headWidth / (float)playerMetrics.headWidth));
            result.AppendLine("fatherToPlayerShoulderWidthRatio=" +
                              Invariant(fatherMetrics.shoulderWidth /
                                        (float)playerMetrics.shoulderWidth));
            result.AppendLine("fatherToPlayerTorsoWidthRatio=" +
                              Invariant(fatherMetrics.torsoWidth / (float)playerMetrics.torsoWidth));
            result.AppendLine("fatherToPlayerLegHeightRatio=" +
                              Invariant(fatherMetrics.legHeight / (float)playerMetrics.legHeight));
            result.AppendLine("fatherToPlayerShoeAreaRatio=" +
                              Invariant(fatherMetrics.shoePixels /
                                        (float)Mathf.Max(playerMetrics.shoePixels, 1)));
            result.AppendLine("sameTileShoeCentroidDeltaPx=" +
                              Invariant(shoeCentroidDeltaPixels.x) + "/" +
                              Invariant(shoeCentroidDeltaPixels.y));
            result.AppendLine("fatherToPlayerSilhouetteAreaRatio=" +
                              Invariant(fatherMetrics.pixels / (float)playerMetrics.pixels));
            File.WriteAllText(
                Path.Combine(outputDirectory, "father-player-same-tile-pixel-ratio.txt"),
                result.ToString(),
                new UTF8Encoding(false));
            return true;
        }

        private static void AppendActorBodyPixelMetrics(
            StringBuilder result,
            string actor,
            ActorBodyPixelMetrics metrics)
        {
            result.AppendLine(actor + "RenderedBounds=" + metrics.width + "x" + metrics.height);
            result.AppendLine(actor + "HeadBounds=" +
                              metrics.headWidth + "x" + metrics.headHeight);
            result.AppendLine(actor + "HeadToHeightRatio=" + Invariant(metrics.headToHeightRatio));
            result.AppendLine(actor + "ShoulderTorsoWidths=" +
                              metrics.shoulderWidth + "/" + metrics.torsoWidth);
            result.AppendLine(actor + "LegBounds=" +
                              metrics.legWidth + "x" + metrics.legHeight);
            result.AppendLine(actor + "ShoeBoundsPixels=" +
                              metrics.shoeWidth + "x" + metrics.shoeHeight + "/" +
                              metrics.shoePixels);
            result.AppendLine(actor + "ShoeCentroidPx=" +
                              Invariant(metrics.shoeCentroidX) + "/" +
                              Invariant(metrics.shoeCentroidY));
            result.AppendLine(actor + "SilhouettePixels=" + metrics.pixels);
            result.AppendLine(actor + "ScreenOccupationPercent=" +
                              metrics.screenOccupationPercent.ToString(
                                  "F6",
                                  System.Globalization.CultureInfo.InvariantCulture));
        }

        private static bool TryCaptureOverviewWithOnlyProductionHost(
            string visibleHostName,
            string path,
            out string failure)
        {
            failure = string.Empty;
            GameObject player = GameObject.Find("PlayerV8ProductionHost");
            GameObject father = GameObject.Find("FatherV19ProductionHost");
            GameObject visible = GameObject.Find(visibleHostName);
            if (player == null || father == null || visible == null)
            {
                failure = "production hosts missing";
                return false;
            }
            Renderer[] playerRenderers = player.GetComponentsInChildren<Renderer>(true);
            Renderer[] fatherRenderers = father.GetComponentsInChildren<Renderer>(true);
            Renderer[] visibleRenderers = visible.GetComponentsInChildren<Renderer>(true);
            Renderer[] all = playerRenderers.Concat(fatherRenderers).Distinct().ToArray();
            bool[] original = all.Select(renderer => renderer.forceRenderingOff).ToArray();
            try
            {
                for (var index = 0; index < all.Length; index++)
                    all[index].forceRenderingOff = true;
                for (var index = 0; index < visibleRenderers.Length; index++)
                    visibleRenderers[index].forceRenderingOff = false;
                return TryCaptureOverview(path, out failure);
            }
            finally
            {
                for (var index = 0; index < all.Length; index++)
                    all[index].forceRenderingOff = original[index];
            }
        }

        private static bool TryRenderActorMaskAndMeasureBody(
            string productionHostName,
            string savePath,
            out ActorBodyPixelMetrics metrics,
            out string failure)
        {
            metrics = default;
            if (!TryRenderIsolatedProductionHost(
                    productionHostName,
                    savePath,
                    out Color32[] sample,
                    out int width,
                    out int height,
                    out Camera overlay,
                    out Animator animator,
                    out failure))
                return false;
            if (!TryMeasureActorBodyPixels(
                    sample,
                    width,
                    height,
                    overlay,
                    animator,
                    out metrics))
            {
                failure = "body pixel segmentation failed";
                return false;
            }
            if (!TryRenderIsolatedProductionShoes(
                    productionHostName,
                    string.IsNullOrWhiteSpace(savePath)
                        ? string.Empty
                        : Path.Combine(
                            Path.GetDirectoryName(savePath) ?? string.Empty,
                            Path.GetFileNameWithoutExtension(savePath) + "-shoes.png"),
                    out Color32[] shoeSample,
                    out int shoeImageWidth,
                    out int shoeImageHeight,
                    out _,
                    out _,
                    out failure))
                return false;
            MeasureMaskRegionBounds(
                shoeSample,
                shoeImageWidth,
                0,
                shoeImageWidth - 1,
                0,
                shoeImageHeight - 1,
                out metrics.shoeWidth,
                out metrics.shoeHeight,
                out metrics.shoePixels);
            if (!TryMeasureMaskCentroid(
                    shoeSample,
                    shoeImageWidth,
                    out metrics.shoeCentroidX,
                    out metrics.shoeCentroidY))
            {
                failure = "shoe pixel centroid failed";
                return false;
            }
            return true;
        }

        private static bool TryMeasureMaskCentroid(
            Color32[] sample,
            int imageWidth,
            out float centroidX,
            out float centroidY)
        {
            double sumX = 0d;
            double sumY = 0d;
            var count = 0;
            for (var index = 0; index < sample.Length; index++)
            {
                if (sample[index].a <= 32) continue;
                sumX += index % imageWidth + 0.5d;
                sumY += index / imageWidth + 0.5d;
                count++;
            }
            centroidX = count == 0 ? float.PositiveInfinity : (float)(sumX / count);
            centroidY = count == 0 ? float.PositiveInfinity : (float)(sumY / count);
            return count > 0;
        }

        private static bool TryRenderIsolatedProductionHost(
            string productionHostName,
            string savePath,
            out Color32[] sample,
            out int width,
            out int height,
            out Camera overlay,
            out Animator animator,
            out string failure)
        {
            const int captureWidth = 1280;
            const int captureHeight = 720;
            sample = Array.Empty<Color32>();
            width = captureWidth;
            height = captureHeight;
            overlay = Object.FindObjectsByType<Camera>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate != null && string.Equals(
                    candidate.gameObject.name,
                    "Family3DProductionOverlayCamera",
                    StringComparison.Ordinal));
            animator = null;
            failure = string.Empty;
            GameObject player = GameObject.Find("PlayerV8ProductionHost");
            GameObject father = GameObject.Find("FatherV19ProductionHost");
            GameObject targetHost = GameObject.Find(productionHostName);
            if (player == null || father == null || targetHost == null || overlay == null)
            {
                failure = "production hosts or overlay camera missing";
                return false;
            }
            animator = targetHost.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                failure = "target Animator missing";
                return false;
            }
            Renderer[] playerRenderers = player.GetComponentsInChildren<Renderer>(true);
            Renderer[] fatherRenderers = father.GetComponentsInChildren<Renderer>(true);
            Renderer[] targetRenderers = targetHost.GetComponentsInChildren<Renderer>(true);
            Renderer[] all = playerRenderers.Concat(fatherRenderers).Distinct().ToArray();
            bool[] original = all.Select(renderer => renderer.forceRenderingOff).ToArray();
            var target = new RenderTexture(
                captureWidth,
                captureHeight,
                24,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            var texture = new Texture2D(
                captureWidth,
                captureHeight,
                TextureFormat.RGBA32,
                false);
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = overlay.targetTexture;
            CameraClearFlags previousClearFlags = overlay.clearFlags;
            Color previousBackground = overlay.backgroundColor;
            try
            {
                for (var index = 0; index < all.Length; index++)
                    all[index].forceRenderingOff = true;
                for (var index = 0; index < targetRenderers.Length; index++)
                    targetRenderers[index].forceRenderingOff = false;
                overlay.targetTexture = target;
                overlay.clearFlags = CameraClearFlags.SolidColor;
                overlay.backgroundColor = Color.clear;
                overlay.Render();
                RenderTexture.active = target;
                texture.ReadPixels(
                    new Rect(0f, 0f, captureWidth, captureHeight),
                    0,
                    0,
                    false);
                texture.Apply(false, false);
                sample = texture.GetPixels32();
                if (!string.IsNullOrWhiteSpace(savePath))
                    File.WriteAllBytes(savePath, texture.EncodeToPNG());
                return sample.Any(pixel => pixel.a > 32);
            }
            catch (Exception exception)
            {
                failure = exception.GetType().Name + ":" + exception.Message;
                return false;
            }
            finally
            {
                for (var index = 0; index < all.Length; index++)
                    all[index].forceRenderingOff = original[index];
                overlay.targetTexture = previousTarget;
                overlay.clearFlags = previousClearFlags;
                overlay.backgroundColor = previousBackground;
                RenderTexture.active = previousActive;
                Object.Destroy(target);
                Object.Destroy(texture);
            }
        }

        private static bool TryRenderIsolatedProductionShoes(
            string productionHostName,
            string savePath,
            out Color32[] sample,
            out int width,
            out int height,
            out Camera overlay,
            out Animator animator,
            out string failure)
        {
            const int captureWidth = 1280;
            const int captureHeight = 720;
            sample = Array.Empty<Color32>();
            width = captureWidth;
            height = captureHeight;
            overlay = Object.FindObjectsByType<Camera>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate != null && string.Equals(
                    candidate.gameObject.name,
                    "Family3DProductionOverlayCamera",
                    StringComparison.Ordinal));
            animator = null;
            failure = string.Empty;
            GameObject player = GameObject.Find("PlayerV8ProductionHost");
            GameObject father = GameObject.Find("FatherV19ProductionHost");
            GameObject targetHost = GameObject.Find(productionHostName);
            if (player == null || father == null || targetHost == null || overlay == null)
            {
                failure = "production hosts or overlay camera missing";
                return false;
            }
            animator = targetHost.GetComponentInChildren<Animator>(true);
            SkinnedMeshRenderer source = targetHost.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (animator == null || source == null || source.sharedMesh == null)
            {
                failure = "target Animator or skinned mesh missing";
                return false;
            }
            Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform leftToe = animator.GetBoneTransform(HumanBodyBones.LeftToes);
            Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            Transform rightToe = animator.GetBoneTransform(HumanBodyBones.RightToes);
            if (leftFoot == null || leftToe == null || rightFoot == null || rightToe == null)
            {
                failure = "shoe bones missing";
                return false;
            }

            Transform[] bones = source.bones;
            var shoeBoneIndices = new HashSet<int>();
            for (var index = 0; index < bones.Length; index++)
                if (bones[index] == leftFoot || bones[index] == leftToe ||
                    bones[index] == rightFoot || bones[index] == rightToe)
                    shoeBoneIndices.Add(index);
            Mesh sourceMesh = source.sharedMesh;
            BoneWeight[] weights = sourceMesh.boneWeights;
            if (shoeBoneIndices.Count < 2 || weights.Length != sourceMesh.vertexCount)
            {
                failure = "shoe skin weights unavailable";
                return false;
            }
            var shoeTriangles = new List<int>();
            for (var subMesh = 0; subMesh < sourceMesh.subMeshCount; subMesh++)
            {
                int[] triangles = sourceMesh.GetTriangles(subMesh);
                for (var triangle = 0; triangle + 2 < triangles.Length; triangle += 3)
                {
                    var weightedVertices = 0;
                    for (var corner = 0; corner < 3; corner++)
                    {
                        BoneWeight weight = weights[triangles[triangle + corner]];
                        float shoeWeight = 0f;
                        if (shoeBoneIndices.Contains(weight.boneIndex0)) shoeWeight += weight.weight0;
                        if (shoeBoneIndices.Contains(weight.boneIndex1)) shoeWeight += weight.weight1;
                        if (shoeBoneIndices.Contains(weight.boneIndex2)) shoeWeight += weight.weight2;
                        if (shoeBoneIndices.Contains(weight.boneIndex3)) shoeWeight += weight.weight3;
                        if (shoeWeight >= 0.35f) weightedVertices++;
                    }
                    if (weightedVertices < 2) continue;
                    shoeTriangles.Add(triangles[triangle]);
                    shoeTriangles.Add(triangles[triangle + 1]);
                    shoeTriangles.Add(triangles[triangle + 2]);
                }
            }
            if (shoeTriangles.Count < 6)
            {
                failure = "no foot-weighted shoe triangles found";
                return false;
            }

            Renderer[] playerRenderers = player.GetComponentsInChildren<Renderer>(true);
            Renderer[] fatherRenderers = father.GetComponentsInChildren<Renderer>(true);
            Renderer[] all = playerRenderers.Concat(fatherRenderers).Distinct().ToArray();
            bool[] original = all.Select(renderer => renderer.forceRenderingOff).ToArray();
            Mesh shoeMesh = Object.Instantiate(sourceMesh);
            shoeMesh.name = sourceMesh.name + "_QaShoePixels";
            shoeMesh.subMeshCount = 1;
            shoeMesh.SetTriangles(shoeTriangles, 0, true);
            GameObject shoeHost = new GameObject("~QaRenderedShoePixels")
                { hideFlags = HideFlags.HideAndDontSave };
            shoeHost.layer = source.gameObject.layer;
            shoeHost.transform.SetParent(source.transform.parent, false);
            shoeHost.transform.localPosition = source.transform.localPosition;
            shoeHost.transform.localRotation = source.transform.localRotation;
            shoeHost.transform.localScale = source.transform.localScale;
            SkinnedMeshRenderer shoeRenderer = shoeHost.AddComponent<SkinnedMeshRenderer>();
            shoeRenderer.sharedMesh = shoeMesh;
            shoeRenderer.rootBone = source.rootBone;
            shoeRenderer.bones = source.bones;
            shoeRenderer.localBounds = source.localBounds;
            shoeRenderer.updateWhenOffscreen = true;
            // Sprites/Default is already retained by the office build and provides a stable
            // opaque-white mask without depending on an otherwise stripped QA-only shader.
            Shader unlit = Shader.Find("Sprites/Default");
            if (unlit == null)
            {
                Object.Destroy(shoeHost);
                Object.Destroy(shoeMesh);
                failure = "Sprites/Default shader missing";
                return false;
            }
            var shoeMaterial = new Material(unlit)
            {
                name = "QaRenderedShoePixelMask",
                color = Color.white,
                hideFlags = HideFlags.HideAndDontSave
            };
            shoeRenderer.sharedMaterial = shoeMaterial;
            var target = new RenderTexture(
                captureWidth,
                captureHeight,
                24,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            var texture = new Texture2D(captureWidth, captureHeight, TextureFormat.RGBA32, false);
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = overlay.targetTexture;
            CameraClearFlags previousClearFlags = overlay.clearFlags;
            Color previousBackground = overlay.backgroundColor;
            try
            {
                for (var index = 0; index < all.Length; index++)
                    all[index].forceRenderingOff = true;
                overlay.targetTexture = target;
                overlay.clearFlags = CameraClearFlags.SolidColor;
                overlay.backgroundColor = Color.clear;
                overlay.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0f, 0f, captureWidth, captureHeight), 0, 0, false);
                texture.Apply(false, false);
                sample = texture.GetPixels32();
                if (!string.IsNullOrWhiteSpace(savePath))
                    File.WriteAllBytes(savePath, texture.EncodeToPNG());
                if (!sample.Any(pixel => pixel.a > 32))
                {
                    failure = "shoe-only render contained no opaque pixels";
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
                for (var index = 0; index < all.Length; index++)
                    all[index].forceRenderingOff = original[index];
                overlay.targetTexture = previousTarget;
                overlay.clearFlags = previousClearFlags;
                overlay.backgroundColor = previousBackground;
                RenderTexture.active = previousActive;
                Object.Destroy(target);
                Object.Destroy(texture);
                Object.Destroy(shoeHost);
                Object.Destroy(shoeMaterial);
                Object.Destroy(shoeMesh);
            }
        }

        private static bool TryMeasureActorBodyPixels(
            Color32[] sample,
            int width,
            int height,
            Camera overlay,
            Animator animator,
            out ActorBodyPixelMetrics metrics)
        {
            metrics = default;
            Transform neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            Transform rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            Transform leftToe = animator.GetBoneTransform(HumanBodyBones.LeftToes);
            Transform rightToe = animator.GetBoneTransform(HumanBodyBones.RightToes);
            if (neck == null || hips == null || leftUpperArm == null || rightUpperArm == null ||
                leftFoot == null || rightFoot == null || leftToe == null || rightToe == null)
                return false;

            Vector2 neckPixel = ProjectOverlayPixel(overlay, neck.position, width, height);
            Vector2 hipsPixel = ProjectOverlayPixel(overlay, hips.position, width, height);
            Vector2 leftShoulderPixel = ProjectOverlayPixel(
                overlay,
                leftUpperArm.position,
                width,
                height);
            Vector2 rightShoulderPixel = ProjectOverlayPixel(
                overlay,
                rightUpperArm.position,
                width,
                height);
            Vector2 leftAnklePixel = ProjectOverlayPixel(overlay, leftFoot.position, width, height);
            Vector2 rightAnklePixel = ProjectOverlayPixel(overlay, rightFoot.position, width, height);
            Vector2 leftToePixel = ProjectOverlayPixel(overlay, leftToe.position, width, height);
            Vector2 rightToePixel = ProjectOverlayPixel(overlay, rightToe.position, width, height);

            int minX = width;
            int minY = height;
            int maxX = -1;
            int maxY = -1;
            var silhouettePixels = 0;
            for (var index = 0; index < sample.Length; index++)
            {
                if (sample[index].a <= 32) continue;
                silhouettePixels++;
                int x = index % width;
                int y = index / width;
                minX = Mathf.Min(minX, x);
                minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x);
                maxY = Mathf.Max(maxY, y);
            }
            if (maxX < minX || maxY < minY || silhouettePixels < 50)
                return false;

            int neckY = Mathf.Clamp(Mathf.RoundToInt(neckPixel.y), minY, maxY);
            int hipsY = Mathf.Clamp(Mathf.RoundToInt(hipsPixel.y), minY, maxY);
            int shoulderY = Mathf.Clamp(
                Mathf.RoundToInt((leftShoulderPixel.y + rightShoulderPixel.y) * 0.5f),
                minY,
                maxY);
            MeasureMaskRegionBounds(
                sample,
                width,
                minX,
                maxX,
                neckY,
                maxY,
                out int headWidth,
                out int headHeight,
                out _);
            int shoulderWidth = MeasureMaximumRowWidth(
                sample,
                width,
                minX,
                maxX,
                Mathf.Max(minY, shoulderY - 4),
                Mathf.Min(maxY, shoulderY + 4));
            int torsoWidth = MeasureMaximumRowWidth(
                sample,
                width,
                minX,
                maxX,
                Mathf.Min(hipsY, neckY),
                Mathf.Max(hipsY, neckY));
            MeasureMaskRegionBounds(
                sample,
                width,
                minX,
                maxX,
                minY,
                hipsY,
                out int legWidth,
                out int legHeight,
                out _);
            MeasureShoePixelBounds(
                sample,
                width,
                minX,
                maxX,
                minY,
                maxY,
                leftAnklePixel,
                leftToePixel,
                rightAnklePixel,
                rightToePixel,
                out int shoeWidth,
                out int shoeHeight,
                out int shoePixels);

            int renderedHeight = maxY - minY + 1;
            metrics = new ActorBodyPixelMetrics
            {
                pixels = silhouettePixels,
                width = maxX - minX + 1,
                height = renderedHeight,
                headWidth = headWidth,
                headHeight = headHeight,
                headToHeightRatio = headHeight / (float)Mathf.Max(renderedHeight, 1),
                shoulderWidth = shoulderWidth,
                torsoWidth = torsoWidth,
                legWidth = legWidth,
                legHeight = legHeight,
                shoeWidth = shoeWidth,
                shoeHeight = shoeHeight,
                shoePixels = shoePixels,
                screenOccupationPercent =
                    silhouettePixels * 100f / (width * (float)height)
            };
            return headWidth > 0 && headHeight > 0 && shoulderWidth > 0 &&
                   torsoWidth > 0 && legHeight > 0 && shoePixels >= 8;
        }

        private static void MeasureMaskRegionBounds(
            Color32[] sample,
            int imageWidth,
            int minimumX,
            int maximumX,
            int minimumY,
            int maximumY,
            out int width,
            out int height,
            out int pixels)
        {
            int foundMinX = imageWidth;
            int foundMinY = int.MaxValue;
            int foundMaxX = -1;
            int foundMaxY = -1;
            pixels = 0;
            for (int y = minimumY; y <= maximumY; y++)
            {
                int row = y * imageWidth;
                for (int x = minimumX; x <= maximumX; x++)
                {
                    if (sample[row + x].a <= 32) continue;
                    pixels++;
                    foundMinX = Mathf.Min(foundMinX, x);
                    foundMinY = Mathf.Min(foundMinY, y);
                    foundMaxX = Mathf.Max(foundMaxX, x);
                    foundMaxY = Mathf.Max(foundMaxY, y);
                }
            }
            width = foundMaxX < foundMinX ? 0 : foundMaxX - foundMinX + 1;
            height = foundMaxY < foundMinY ? 0 : foundMaxY - foundMinY + 1;
        }

        private static int MeasureMaximumRowWidth(
            Color32[] sample,
            int imageWidth,
            int minimumX,
            int maximumX,
            int minimumY,
            int maximumY)
        {
            var maximumWidth = 0;
            for (int y = minimumY; y <= maximumY; y++)
            {
                int row = y * imageWidth;
                int rowMin = imageWidth;
                int rowMax = -1;
                for (int x = minimumX; x <= maximumX; x++)
                {
                    if (sample[row + x].a <= 32) continue;
                    rowMin = Mathf.Min(rowMin, x);
                    rowMax = Mathf.Max(rowMax, x);
                }
                if (rowMax >= rowMin)
                    maximumWidth = Mathf.Max(maximumWidth, rowMax - rowMin + 1);
            }
            return maximumWidth;
        }

        private static void MeasureShoePixelBounds(
            Color32[] sample,
            int imageWidth,
            int minimumX,
            int maximumX,
            int minimumY,
            int maximumY,
            Vector2 leftAnkle,
            Vector2 leftToe,
            Vector2 rightAnkle,
            Vector2 rightToe,
            out int width,
            out int height,
            out int pixels)
        {
            // At the fixed 1280x720 measurement resolution the visible shoe is only about
            // 5-7 px away from its ankle-to-toe axis.  The earlier 14 px radius admitted the
            // lower shin and reported 250-400 "shoe" pixels per foot.  Keep this mask tight so
            // the sampled contour follows the rendered shoe rather than the trouser leg.
            const float soleRadiusPixels = 7f;
            int foundMinX = imageWidth;
            int foundMinY = int.MaxValue;
            int foundMaxX = -1;
            int foundMaxY = -1;
            pixels = 0;
            float maximumShoeY = Mathf.Max(
                Mathf.Max(leftAnkle.y, leftToe.y),
                Mathf.Max(rightAnkle.y, rightToe.y)) + 3f;
            for (int y = minimumY; y <= maximumY && y <= maximumShoeY; y++)
            {
                int row = y * imageWidth;
                for (int x = minimumX; x <= maximumX; x++)
                {
                    if (sample[row + x].a <= 32) continue;
                    var point = new Vector2(x + 0.5f, y + 0.5f);
                    float distance = Mathf.Min(
                        DistancePointToSegment(point, leftAnkle, leftToe),
                        DistancePointToSegment(point, rightAnkle, rightToe));
                    if (distance > soleRadiusPixels) continue;
                    pixels++;
                    foundMinX = Mathf.Min(foundMinX, x);
                    foundMinY = Mathf.Min(foundMinY, y);
                    foundMaxX = Mathf.Max(foundMaxX, x);
                    foundMaxY = Mathf.Max(foundMaxY, y);
                }
            }
            width = foundMaxX < foundMinX ? 0 : foundMaxX - foundMinX + 1;
            height = foundMaxY < foundMinY ? 0 : foundMaxY - foundMinY + 1;
        }

        private static bool TryMeasureRenderedShoePixelTileContainment(
            StarterOfficeRuntimeBootstrap runtime,
            OfficeRuntimeAgent agent,
            string productionHostName,
            out RenderedShoePixelSample result,
            out string failure)
        {
            result = default;
            failure = string.Empty;
            if (!TryRenderIsolatedProductionHost(
                    productionHostName,
                    string.Empty,
                    out Color32[] bodySample,
                    out int width,
                    out int height,
                    out Camera overlay,
                    out Animator animator,
                    out failure))
                return false;
            if (!TryMeasureActorBodyPixels(
                    bodySample,
                    width,
                    height,
                    overlay,
                    animator,
                    out ActorBodyPixelMetrics bodyMetrics))
            {
                failure = "body pixel segmentation failed during shoe measurement";
                return false;
            }
            if (!TryRenderIsolatedProductionShoes(
                    productionHostName,
                    string.Empty,
                    out Color32[] sample,
                    out int shoeWidth,
                    out int shoeHeight,
                    out overlay,
                    out animator,
                    out failure))
                return false;
            if (shoeWidth != width || shoeHeight != height)
            {
                failure = "shoe/body render dimensions differ";
                return false;
            }
            Camera source = Camera.main;
            Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            Transform leftToe = animator.GetBoneTransform(HumanBodyBones.LeftToes);
            Transform rightToe = animator.GetBoneTransform(HumanBodyBones.RightToes);
            if (source == null || leftFoot == null || rightFoot == null ||
                leftToe == null || rightToe == null)
            {
                failure = "camera or foot/toe bones missing";
                return false;
            }
            Vector2 leftAnklePixel = ProjectOverlayPixel(overlay, leftFoot.position, width, height);
            Vector2 rightAnklePixel = ProjectOverlayPixel(overlay, rightFoot.position, width, height);
            Vector2 leftToePixel = ProjectOverlayPixel(overlay, leftToe.position, width, height);
            Vector2 rightToePixel = ProjectOverlayPixel(overlay, rightToe.position, width, height);
            if (!TryBuildRenderedTilePolygon(
                    runtime,
                    agent,
                    source,
                    overlay,
                    leftFoot.position,
                    leftToe.position,
                    width,
                    height,
                    out Vector2[] leftTile) ||
                !TryBuildRenderedTilePolygon(
                    runtime,
                    agent,
                    source,
                    overlay,
                    rightFoot.position,
                    rightToe.position,
                    width,
                    height,
                    out Vector2[] rightTile))
            {
                failure = "rendered shoe tile polygon mapping failed";
                return false;
            }

            int leftPixels = 0;
            int rightPixels = 0;
            int leftOutside = 0;
            int rightOutside = 0;
            float leftMargin = float.PositiveInfinity;
            float rightMargin = float.PositiveInfinity;
            double shoePixelSumX = 0d;
            double shoePixelSumY = 0d;
            var shoePixelCount = 0;
            for (var index = 0; index < sample.Length; index++)
            {
                if (sample[index].a <= 32) continue;
                int x = index % width;
                int y = index / width;
                var point = new Vector2(x + 0.5f, y + 0.5f);
                shoePixelSumX += point.x;
                shoePixelSumY += point.y;
                shoePixelCount++;
                float leftDistance = DistancePointToSegment(
                    point,
                    leftAnklePixel,
                    leftToePixel);
                float rightDistance = DistancePointToSegment(
                    point,
                    rightAnklePixel,
                    rightToePixel);
                if (leftDistance <= rightDistance)
                {
                    leftPixels++;
                    float margin = SignedConvexPolygonMargin(point, leftTile);
                    leftMargin = Mathf.Min(leftMargin, margin);
                    if (margin <= 0f) leftOutside++;
                }
                else
                {
                    rightPixels++;
                    float margin = SignedConvexPolygonMargin(point, rightTile);
                    rightMargin = Mathf.Min(rightMargin, margin);
                    if (margin <= 0f) rightOutside++;
                }
            }
            if (leftPixels < 4 || rightPixels < 4 ||
                shoePixelCount < 8 || float.IsInfinity(leftMargin) || float.IsInfinity(rightMargin))
            {
                failure = "both rendered shoe clusters were not measurable left=" + leftPixels +
                          " right=" + rightPixels;
                return false;
            }
            var shoeCentroidPixels = new Vector2(
                (float)(shoePixelSumX / shoePixelCount),
                (float)(shoePixelSumY / shoePixelCount));
            Vector3 agentViewport = source.WorldToViewportPoint(new Vector3(
                agent.Position.x,
                agent.Position.y,
                agent.transform.position.z));
            var agentCenterPixels = new Vector2(
                agentViewport.x * width,
                agentViewport.y * height);
            result = new RenderedShoePixelSample
            {
                leftPixelCount = leftPixels,
                rightPixelCount = rightPixels,
                leftOutsidePixelCount = leftOutside,
                rightOutsidePixelCount = rightOutside,
                leftMinimumTileMarginPixels = leftMargin,
                rightMinimumTileMarginPixels = rightMargin,
                shoeCentroidPixels = shoeCentroidPixels,
                agentCenterPixels = agentCenterPixels,
                shoeToAgentPixels = shoeCentroidPixels - agentCenterPixels,
                bodyMetrics = bodyMetrics
            };
            return true;
        }

        private static bool TryBuildRenderedTilePolygon(
            StarterOfficeRuntimeBootstrap runtime,
            OfficeRuntimeAgent agent,
            Camera source,
            Camera overlay,
            Vector3 ankleWorld,
            Vector3 toeWorld,
            int width,
            int height,
            out Vector2[] polygon)
        {
            polygon = Array.Empty<Vector2>();
            Vector3 sourcePoint = new Vector3(
                agent.Position.x,
                agent.Position.y,
                agent.transform.position.z);
            float sourceDepth = source.WorldToViewportPoint(sourcePoint).z;
            ankleWorld.y = 0f;
            toeWorld.y = 0f;
            Vector3 soleMidpoint = (ankleWorld + toeWorld) * 0.5f;
            if (sourceDepth <= 0f ||
                !TryMapOverlayPointToOfficeWorld(
                    source,
                    overlay,
                    sourceDepth,
                    soleMidpoint,
                    out Vector2 soleOffice))
                return false;
            Vector2 origin = runtime.World.Presenter.CellCenterWorld(
                new OfficeGridCoordinate(0, 0));
            Vector2 basisX = runtime.World.Presenter.CellBasisXWorld();
            Vector2 basisY = runtime.World.Presenter.CellBasisYWorld();
            if (!TryResolveGridCoordinate(
                    soleOffice,
                    origin,
                    basisX,
                    basisY,
                    out Vector2 soleGrid))
                return false;
            var cell = new Vector2(Mathf.Round(soleGrid.x), Mathf.Round(soleGrid.y));
            Vector2 center = origin + basisX * cell.x + basisY * cell.y;
            Vector2[] officeCorners =
            {
                center - 0.5f * basisX - 0.5f * basisY,
                center + 0.5f * basisX - 0.5f * basisY,
                center + 0.5f * basisX + 0.5f * basisY,
                center - 0.5f * basisX + 0.5f * basisY
            };
            polygon = officeCorners.Select(corner =>
            {
                Vector3 viewport = source.WorldToViewportPoint(
                    new Vector3(corner.x, corner.y, agent.transform.position.z));
                return new Vector2(viewport.x * width, viewport.y * height);
            }).ToArray();
            return polygon.All(point => !float.IsNaN(point.x) && !float.IsNaN(point.y));
        }

        private static Vector2 ProjectOverlayPixel(
            Camera overlay,
            Vector3 world,
            int width,
            int height)
        {
            Vector3 viewport = overlay.WorldToViewportPoint(world);
            return new Vector2(viewport.x * width, viewport.y * height);
        }

        private static float DistancePointToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 segment = end - start;
            float lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= 0.000001f) return Vector2.Distance(point, start);
            float t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSquared);
            return Vector2.Distance(point, start + segment * t);
        }

        private static float SignedConvexPolygonMargin(Vector2 point, Vector2[] polygon)
        {
            float windingSign = 0f;
            float minimumLineDistance = float.PositiveInfinity;
            bool inside = true;
            for (var index = 0; index < polygon.Length; index++)
            {
                Vector2 start = polygon[index];
                Vector2 end = polygon[(index + 1) % polygon.Length];
                Vector2 edge = end - start;
                float cross = Cross(edge, point - start);
                if (Mathf.Abs(cross) > 0.0001f)
                {
                    float sign = Mathf.Sign(cross);
                    if (Mathf.Approximately(windingSign, 0f)) windingSign = sign;
                    else if (sign != windingSign) inside = false;
                }
                minimumLineDistance = Mathf.Min(
                    minimumLineDistance,
                    Mathf.Abs(cross) / Mathf.Max(edge.magnitude, 0.000001f));
            }
            return inside ? minimumLineDistance : -minimumLineDistance;
        }

        private static void AppendRenderedShoePixelTrace(
            StringBuilder trace,
            int frame,
            string actor,
            RenderedShoePixelSample sample)
        {
            trace.Append(frame).Append(',').Append(actor).Append(',')
                .Append(sample.leftPixelCount).Append(',')
                .Append(sample.rightPixelCount).Append(',')
                .Append(sample.leftOutsidePixelCount).Append(',')
                .Append(sample.rightOutsidePixelCount).Append(',')
                .Append(Invariant(sample.leftMinimumTileMarginPixels)).Append(',')
                .Append(Invariant(sample.rightMinimumTileMarginPixels)).Append(',')
                .Append(sample.bodyMetrics.width).Append(',')
                .Append(sample.bodyMetrics.height).Append(',')
                .Append(sample.bodyMetrics.headWidth).Append(',')
                .Append(sample.bodyMetrics.headHeight).Append(',')
                .Append(Invariant(sample.bodyMetrics.headToHeightRatio)).Append(',')
                .Append(sample.bodyMetrics.shoulderWidth).Append(',')
                .Append(sample.bodyMetrics.torsoWidth).Append(',')
                .Append(sample.bodyMetrics.legWidth).Append(',')
                .Append(sample.bodyMetrics.legHeight).Append(',')
                .Append(sample.bodyMetrics.pixels).Append(',')
                .Append(Invariant(sample.bodyMetrics.screenOccupationPercent)).Append(',')
                .Append(Invariant(sample.shoeCentroidPixels.x)).Append(',')
                .Append(Invariant(sample.shoeCentroidPixels.y)).Append(',')
                .Append(Invariant(sample.agentCenterPixels.x)).Append(',')
                .Append(Invariant(sample.agentCenterPixels.y)).Append(',')
                .Append(Invariant(sample.shoeToAgentPixels.x)).Append(',')
                .Append(Invariant(sample.shoeToAgentPixels.y)).AppendLine();
        }

        private static bool TryMeasureFootTileLineClearance(
            StarterOfficeRuntimeBootstrap runtime,
            OfficeRuntimeAgent agent,
            string productionHostName,
            out FootTileLineSample sample)
        {
            // Humanoid Foot is the ankle pivot, not the front of the rendered shoe. The old
            // check therefore passed while the toe visibly covered a tile line. Measure an
            // expanded ankle-to-toe sole axis and subtract a conservative rendered half-width.
            const float heelExtensionRatio = 0.65f;
            const float toeExtensionRatio = 0.45f;
            const float shoeHalfWidthSafetyPixels = 4f;
            sample = default;
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
            Component walkActor = host == null
                ? null
                : host.GetComponents<Component>().FirstOrDefault(component =>
                    component != null && string.Equals(
                        component.GetType().FullName,
                        "FamilyCompany.Runtime.Character3D.Family3DWalkActor",
                        StringComparison.Ordinal));
            Transform leftFoot = animator?.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rightFoot = animator?.GetBoneTransform(HumanBodyBones.RightFoot);
            Transform leftToe = animator?.GetBoneTransform(HumanBodyBones.LeftToes);
            Transform rightToe = animator?.GetBoneTransform(HumanBodyBones.RightToes);
            if (runtime?.World?.Presenter == null || source == null || overlay == null ||
                walkActor == null || leftFoot == null || rightFoot == null ||
                leftToe == null || rightToe == null || !source.orthographic)
                return false;

            Vector3 sourcePoint = new Vector3(
                agent.Position.x,
                agent.Position.y,
                agent.transform.position.z);
            float sourceDepth = source.WorldToViewportPoint(sourcePoint).z;
            Vector3 leftFootGround = leftFoot.position;
            Vector3 rightFootGround = rightFoot.position;
            Vector3 leftToeGround = leftToe.position;
            Vector3 rightToeGround = rightToe.position;
            leftFootGround.y = 0f;
            rightFootGround.y = 0f;
            leftToeGround.y = 0f;
            rightToeGround.y = 0f;
            if (sourceDepth <= 0f ||
                !TryMapOverlayPointToOfficeWorld(
                    source,
                    overlay,
                    sourceDepth,
                    leftFootGround,
                    out Vector2 leftOffice) ||
                !TryMapOverlayPointToOfficeWorld(
                    source,
                    overlay,
                    sourceDepth,
                    rightFootGround,
                    out Vector2 rightOffice) ||
                !TryMapOverlayPointToOfficeWorld(
                    source,
                    overlay,
                    sourceDepth,
                    leftToeGround,
                    out Vector2 leftToeOffice) ||
                !TryMapOverlayPointToOfficeWorld(
                    source,
                    overlay,
                    sourceDepth,
                    rightToeGround,
                    out Vector2 rightToeOffice))
                return false;

            Vector2 origin = runtime.World.Presenter.CellCenterWorld(
                new OfficeGridCoordinate(0, 0));
            Vector2 basisX = runtime.World.Presenter.CellBasisXWorld();
            Vector2 basisY = runtime.World.Presenter.CellBasisYWorld();
            if (!TryResolveGridCoordinate(leftOffice, origin, basisX, basisY, out Vector2 leftGrid) ||
                !TryResolveGridCoordinate(rightOffice, origin, basisX, basisY, out Vector2 rightGrid) ||
                !TryResolveGridCoordinate(leftToeOffice, origin, basisX, basisY, out Vector2 leftToeGrid) ||
                !TryResolveGridCoordinate(rightToeOffice, origin, basisX, basisY, out Vector2 rightToeGrid))
                return false;

            float leftLine = NearestGridLineClearancePixels(
                leftOffice,
                leftGrid,
                origin,
                basisX,
                basisY,
                source);
            float rightLine = NearestGridLineClearancePixels(
                rightOffice,
                rightGrid,
                origin,
                basisX,
                basisY,
                source);
            Vector2 leftHeelGrid = Vector2.LerpUnclamped(
                leftGrid,
                leftToeGrid,
                -heelExtensionRatio);
            Vector2 leftTipGrid = Vector2.LerpUnclamped(
                leftGrid,
                leftToeGrid,
                1f + toeExtensionRatio);
            Vector2 rightHeelGrid = Vector2.LerpUnclamped(
                rightGrid,
                rightToeGrid,
                -heelExtensionRatio);
            Vector2 rightTipGrid = Vector2.LerpUnclamped(
                rightGrid,
                rightToeGrid,
                1f + toeExtensionRatio);
            float leftShoeLine = NearestGridLineClearancePixelsForSegment(
                                     leftHeelGrid,
                                     leftTipGrid,
                                     basisX,
                                     basisY,
                                     source) -
                                 shoeHalfWidthSafetyPixels;
            float rightShoeLine = NearestGridLineClearancePixelsForSegment(
                                      rightHeelGrid,
                                      rightTipGrid,
                                      basisX,
                                      basisY,
                                      source) -
                                  shoeHalfWidthSafetyPixels;
            if (!TryReadWalkActorContactTelemetry(
                    walkActor,
                    out float phase,
                    out bool leftContact,
                    out bool rightContact))
                return false;
            float minimumContact = float.PositiveInfinity;
            if (leftContact) minimumContact = Mathf.Min(minimumContact, leftShoeLine);
            if (rightContact) minimumContact = Mathf.Min(minimumContact, rightShoeLine);
            sample = new FootTileLineSample
            {
                phase01 = phase,
                leftContact = leftContact,
                rightContact = rightContact,
                hasContact = leftContact || rightContact,
                leftGrid = leftGrid,
                rightGrid = rightGrid,
                leftToeGrid = leftToeGrid,
                rightToeGrid = rightToeGrid,
                leftLineClearancePx = leftLine,
                rightLineClearancePx = rightLine,
                leftShoeLineClearancePx = leftShoeLine,
                rightShoeLineClearancePx = rightShoeLine,
                minimumContactLineClearancePx = minimumContact
            };
            return true;
        }

        private static bool TryReadWalkActorContactTelemetry(
            Component walkActor,
            out float phase,
            out bool leftContact,
            out bool rightContact)
        {
            phase = 0f;
            leftContact = false;
            rightContact = false;
            if (walkActor == null)
                return false;
            Type type = walkActor.GetType();
            System.Reflection.PropertyInfo leftProperty = type.GetProperty("LeftFootPlanted");
            System.Reflection.PropertyInfo rightProperty = type.GetProperty("RightFootPlanted");
            System.Reflection.MethodInfo snapshotMethod = type.GetMethod("ReadPoseSnapshot");
            if (leftProperty == null || rightProperty == null || snapshotMethod == null)
                return false;
            object snapshot = snapshotMethod.Invoke(walkActor, null);
            System.Reflection.FieldInfo phaseField = snapshot?.GetType().GetField("motionPhase01");
            if (phaseField == null)
                return false;
            phase = Convert.ToSingle(phaseField.GetValue(snapshot));
            leftContact = Convert.ToBoolean(leftProperty.GetValue(walkActor));
            rightContact = Convert.ToBoolean(rightProperty.GetValue(walkActor));
            return true;
        }

        private static bool TryMapOverlayPointToOfficeWorld(
            Camera source,
            Camera overlay,
            float sourceDepth,
            Vector3 overlayWorld,
            out Vector2 officeWorld)
        {
            officeWorld = Vector2.zero;
            Vector3 viewport = overlay.WorldToViewportPoint(overlayWorld);
            if (viewport.z <= 0f)
                return false;
            Vector3 sourceWorld = source.ViewportToWorldPoint(
                new Vector3(viewport.x, viewport.y, sourceDepth));
            officeWorld = new Vector2(sourceWorld.x, sourceWorld.y);
            return true;
        }

        private static bool TryResolveGridCoordinate(
            Vector2 point,
            Vector2 origin,
            Vector2 basisX,
            Vector2 basisY,
            out Vector2 grid)
        {
            grid = Vector2.zero;
            float determinant = basisX.x * basisY.y - basisX.y * basisY.x;
            if (Mathf.Abs(determinant) <= 0.000001f)
                return false;
            Vector2 delta = point - origin;
            grid = new Vector2(
                (delta.x * basisY.y - delta.y * basisY.x) / determinant,
                (basisX.x * delta.y - basisX.y * delta.x) / determinant);
            return true;
        }

        private static float NearestGridLineClearancePixels(
            Vector2 point,
            Vector2 grid,
            Vector2 origin,
            Vector2 basisX,
            Vector2 basisY,
            Camera source)
        {
            float boundaryX = Mathf.Round(grid.x - 0.5f) + 0.5f;
            float boundaryY = Mathf.Round(grid.y - 0.5f) + 0.5f;
            Vector2 pointOnXLine = origin + basisX * boundaryX + basisY * grid.y;
            Vector2 pointOnYLine = origin + basisX * grid.x + basisY * boundaryY;
            float distanceX = Mathf.Abs(Cross(point - pointOnXLine, basisY)) /
                              Mathf.Max(basisY.magnitude, 0.000001f);
            float distanceY = Mathf.Abs(Cross(point - pointOnYLine, basisX)) /
                              Mathf.Max(basisX.magnitude, 0.000001f);
            float pixelsPerWorld = 720f / (2f * source.orthographicSize);
            return Mathf.Min(distanceX, distanceY) * pixelsPerWorld;
        }

        private static float NearestGridLineClearancePixelsForSegment(
            Vector2 startGrid,
            Vector2 endGrid,
            Vector2 basisX,
            Vector2 basisY,
            Camera source)
        {
            float determinant = Mathf.Abs(Cross(basisX, basisY));
            float pixelsPerWorld = 720f / (2f * source.orthographicSize);
            float xLineSpacingWorld = determinant / Mathf.Max(basisY.magnitude, 0.000001f);
            float yLineSpacingWorld = determinant / Mathf.Max(basisX.magnitude, 0.000001f);
            float xClearance = NearestHalfIntegerClearanceForInterval(
                startGrid.x,
                endGrid.x) * xLineSpacingWorld * pixelsPerWorld;
            float yClearance = NearestHalfIntegerClearanceForInterval(
                startGrid.y,
                endGrid.y) * yLineSpacingWorld * pixelsPerWorld;
            return Mathf.Min(xClearance, yClearance);
        }

        private static float NearestHalfIntegerClearanceForInterval(float first, float second)
        {
            float minimum = Mathf.Min(first, second);
            float maximum = Mathf.Max(first, second);
            float firstBoundaryAtOrAboveMinimum = Mathf.Ceil(minimum - 0.5f) + 0.5f;
            if (firstBoundaryAtOrAboveMinimum <= maximum + 0.000001f)
                return 0f;
            return Mathf.Min(
                DistanceToNearestHalfInteger(first),
                DistanceToNearestHalfInteger(second));
        }

        private static float DistanceToNearestHalfInteger(float value) =>
            Mathf.Abs(value - (Mathf.Round(value - 0.5f) + 0.5f));

        private static float Cross(Vector2 left, Vector2 right) =>
            left.x * right.y - left.y * right.x;

        private static void AppendFootTileTrace(
            StringBuilder trace,
            int frame,
            string actor,
            FootTileLineSample sample)
        {
            trace.Append(frame).Append(',').Append(actor).Append(',')
                .Append(Invariant(sample.phase01)).Append(',')
                .Append(sample.leftContact ? "true" : "false").Append(',')
                .Append(sample.rightContact ? "true" : "false").Append(',')
                .Append(Invariant(sample.leftGrid.x)).Append(',')
                .Append(Invariant(sample.leftGrid.y)).Append(',')
                .Append(Invariant(sample.rightGrid.x)).Append(',')
                .Append(Invariant(sample.rightGrid.y)).Append(',')
                .Append(Invariant(sample.leftToeGrid.x)).Append(',')
                .Append(Invariant(sample.leftToeGrid.y)).Append(',')
                .Append(Invariant(sample.rightToeGrid.x)).Append(',')
                .Append(Invariant(sample.rightToeGrid.y)).Append(',')
                .Append(Invariant(sample.leftLineClearancePx)).Append(',')
                .Append(Invariant(sample.rightLineClearancePx)).Append(',')
                .Append(Invariant(sample.leftShoeLineClearancePx)).Append(',')
                .Append(Invariant(sample.rightShoeLineClearancePx)).Append(',')
                .Append(Invariant(sample.minimumContactLineClearancePx)).AppendLine();
        }

        private static string Invariant(float value) =>
            value.ToString("F6", System.Globalization.CultureInfo.InvariantCulture);

        private struct ActorBodyPixelMetrics
        {
            public int pixels;
            public int width;
            public int height;
            public int headWidth;
            public int headHeight;
            public float headToHeightRatio;
            public int shoulderWidth;
            public int torsoWidth;
            public int legWidth;
            public int legHeight;
            public int shoeWidth;
            public int shoeHeight;
            public int shoePixels;
            public float shoeCentroidX;
            public float shoeCentroidY;
            public float screenOccupationPercent;
        }

        private struct RenderedShoePixelSample
        {
            public int leftPixelCount;
            public int rightPixelCount;
            public int leftOutsidePixelCount;
            public int rightOutsidePixelCount;
            public float leftMinimumTileMarginPixels;
            public float rightMinimumTileMarginPixels;
            public Vector2 shoeCentroidPixels;
            public Vector2 agentCenterPixels;
            public Vector2 shoeToAgentPixels;
            public ActorBodyPixelMetrics bodyMetrics;
        }

        private struct FootTileLineSample
        {
            public float phase01;
            public bool leftContact;
            public bool rightContact;
            public bool hasContact;
            public Vector2 leftGrid;
            public Vector2 rightGrid;
            public Vector2 leftToeGrid;
            public Vector2 rightToeGrid;
            public float leftLineClearancePx;
            public float rightLineClearancePx;
            public float leftShoeLineClearancePx;
            public float rightShoeLineClearancePx;
            public float minimumContactLineClearancePx;
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
            bool reviewCandidate = pass && HasFlag(Legacy2DScaleCandidateFlag);
            string status = reviewCandidate
                ? "CANDIDATE_USER_APPROVAL_REQUIRED"
                : pass ? "PASS" : "FAIL";
            File.WriteAllText(
                Path.Combine(artifactDirectory, "player-father-3d-interaction-final.txt"),
                "FAMILY_COMPANY_PLAYER_FATHER_3D_INTERACTION: " + status +
                Environment.NewLine + detail + Environment.NewLine);
            if (reviewCandidate)
                Debug.Log(
                    "FAMILY_COMPANY_PLAYER_FATHER_3D_INTERACTION: " + status + " | " + detail);
            else if (pass)
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
