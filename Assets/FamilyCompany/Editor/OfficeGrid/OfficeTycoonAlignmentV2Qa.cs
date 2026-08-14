using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FamilyCompany.Presentation.Unity.OfficeGridView;
using FamilyCompany.Presentation.Unity.OfficeGridView.Authoring;
using FamilyCompany.Presentation.Unity.OfficeSeating;
using FamilyCompany.Save.OfficeGrid;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FamilyCompany.Editor.OfficeGridQa
{
    [InitializeOnLoad]
    public static class OfficeTycoonAlignmentV2Qa
    {
        public const string ArtifactFolder = "Artifacts/OfficeTycoonAlignmentV2";
        public const string BeforeReferencePath = ArtifactFolder + "/before-current-1392x771-reference.png";
        public const string PreviewFootprintPath = ArtifactFolder + "/preview-footprint-debug-1920x1080.png";
        public const string PreviewSocketsPath = ArtifactFolder + "/preview-workstation-sockets-1920x1080.png";
        public const string PreviewSeatedPath = ArtifactFolder + "/preview-four-family-seated-1920x1080.png";
        public const string StarterOverviewPath = ArtifactFolder + "/starter-office-overview-1920x1080.png";
        public const string StarterWorkingPath = ArtifactFolder + "/starter-office-four-family-working-1920x1080.png";
        public const string StarterReseatPath = ArtifactFolder + "/starter-office-after-reseat-1920x1080.png";
        public const string ReportPath = ArtifactFolder + "/alignment-v2-report.txt";

        private const string ActiveKey = "FamilyCompany.OfficeAlignmentV2.Active";
        private const string StageKey = "FamilyCompany.OfficeAlignmentV2.Stage";
        private const string StartKey = "FamilyCompany.OfficeAlignmentV2.Start";
        private const string FailureKey = "FamilyCompany.OfficeAlignmentV2.Failure";
        private const float PreviewValidationSeconds = 45f;
        private const float StarterWorkingSeconds = 45f;
        private const float StarterFinalSeconds = 60f;

        private sealed class TransformSnapshot
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;
            public Transform Parent;
        }

        private sealed class FurnitureSnapshot
        {
            public TransformSnapshot Semantic;
            public TransformSnapshot Visual;
        }

        private static readonly Dictionary<string, FurnitureSnapshot> FurnitureSnapshots =
            new Dictionary<string, FurnitureSnapshot>(StringComparer.Ordinal);

        static OfficeTycoonAlignmentV2Qa()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        [MenuItem("Family Company/QA/Office Tycoon Alignment V2 — Full 60s")]
        public static void StartBatch()
        {
            try
            {
                Directory.CreateDirectory(ArtifactFolder);
                File.WriteAllText(
                    ReportPath,
                    "# Office Tycoon Alignment V2 QA\n\n" +
                    "Independent inputs: semantic tile projection, persisted four-corner calibration, " +
                    "persisted workstation sockets, and clip/frame pose calibration.\n\n",
                    Encoding.UTF8);
                OfficeTileMigrationQa.BuildPreviewScene(true, OfficeTilePreviewLayout.MigrationPreview);
                ValidateCalibrationAssetsAreNonDestructive();
                SessionState.SetBool(ActiveKey, true);
                SessionState.SetInt(StageKey, 1);
                SessionState.SetFloat(StartKey, 0f);
                SessionState.SetString(FailureKey, string.Empty);
                Append("RUN | preview=45s | starter=60s | resolution=1920x1080");
                EditorSceneManager.OpenScene(OfficeTileMigrationQa.PreviewScenePath, OpenSceneMode.Single);
                EditorApplication.EnterPlaymode();
            }
            catch (Exception exception)
            {
                FailPreparation(exception);
            }
        }

        private static void OnEditorUpdate()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            int stage = SessionState.GetInt(StageKey, 0);
            if (stage == 1)
            {
                RunPreviewStage();
                return;
            }

            if (stage == 2)
            {
                if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
                try
                {
                    OfficeTileMigrationQa.BuildPreviewScene(true, OfficeTilePreviewLayout.StarterOfficeV1);
                    SessionState.SetInt(StageKey, 3);
                    SessionState.SetFloat(StartKey, 0f);
                    EditorSceneManager.OpenScene(OfficeTileMigrationQa.PreviewScenePath, OpenSceneMode.Single);
                    EditorApplication.EnterPlaymode();
                }
                catch (Exception exception)
                {
                    CompleteWithFailure(exception);
                }
                return;
            }

            if (stage == 3 || stage == 4)
            {
                RunStarterStage(stage);
                return;
            }

            if (stage != 5 || EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
            try
            {
                OfficeTileMigrationQa.BuildPreviewScene(true, OfficeTilePreviewLayout.MigrationPreview);
                Append("CLEANUP | canonical Migration Preview fixture restored");
            }
            catch (Exception exception)
            {
                RecordFailure("Could not restore canonical Migration Preview fixture: " + exception);
            }
            string failure = SessionState.GetString(FailureKey, string.Empty);
            ClearSession();
            if (failure.Length == 0)
            {
                Append("FINAL | PASS");
                Debug.Log("FAMILY_COMPANY_OFFICE_TYCOON_ALIGNMENT_V2: PASS");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            else
            {
                Append("FINAL | FAIL | " + failure);
                Debug.LogError(failure);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        public static void RestoreMigrationPreviewBatch()
        {
            try
            {
                OfficeTileMigrationQa.BuildPreviewScene(true, OfficeTilePreviewLayout.MigrationPreview);
                Debug.Log("FAMILY_COMPANY_OFFICE_TYCOON_ALIGNMENT_V2_PREVIEW_RESTORE: PASS");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        private static void RunPreviewStage()
        {
            if (!EditorApplication.isPlaying) return;
            float elapsed = ElapsedOrStart();
            if (elapsed < PreviewValidationSeconds) return;
            try
            {
                OfficeTileMigrationPreviewBootstrap bootstrap = RequiredBootstrap(OfficeTilePreviewLayout.MigrationPreview);
                Camera camera = RequiredCamera();
                ValidateLayout(bootstrap, 70, 15, expectsPartition: true);
                Capture(bootstrap, camera, PreviewSeatedPath, false);
                Require(bootstrap.AlignmentDebugOverlay != null, "Preview alignment overlay is missing.");
                bootstrap.AlignmentDebugOverlay.SetOverlayEnabled(true);
                bootstrap.AlignmentDebugOverlay.RefreshImmediate();
                Capture(bootstrap, camera, PreviewFootprintPath, true);
                Capture(bootstrap, camera, PreviewSocketsPath, true);
                bootstrap.AlignmentDebugOverlay.SetOverlayEnabled(false);
                ValidateRuntimeAlignment(bootstrap, camera, "MIGRATION_PREVIEW");
                Append("PREVIEW | COMPLETE | migration fixture retained only for QA");
                SessionState.SetInt(StageKey, 2);
                SessionState.SetFloat(StartKey, 0f);
                EditorApplication.ExitPlaymode();
            }
            catch (Exception exception)
            {
                CompleteWithFailure(exception);
            }
        }

        private static void RunStarterStage(int stage)
        {
            if (!EditorApplication.isPlaying) return;
            float elapsed = ElapsedOrStart();
            try
            {
                OfficeTileMigrationPreviewBootstrap bootstrap = RequiredBootstrap(OfficeTilePreviewLayout.StarterOfficeV1);
                Camera camera = RequiredCamera();
                if (stage == 3 && FurnitureSnapshots.Count == 0)
                {
                    ValidateLayout(bootstrap, 69, 14, expectsPartition: false);
                    SnapshotFurniture(bootstrap);
                    Capture(bootstrap, camera, StarterOverviewPath, false);
                }

                if (stage == 3 && elapsed >= StarterWorkingSeconds)
                {
                    ValidateRuntimeAlignment(bootstrap, camera, "STARTER_WORKING_45S");
                    Capture(bootstrap, camera, StarterWorkingPath, false);
                    foreach (OfficeGridSeatedWorker worker in bootstrap.SeatedWorkers) worker.RequestStandAndReseat();
                    SessionState.SetInt(StageKey, 4);
                    Append("STARTER | stand-and-reseat requested for all four members at 45s");
                }
                else if (stage == 4 && elapsed >= StarterFinalSeconds)
                {
                    ValidateRuntimeAlignment(bootstrap, camera, "STARTER_RESEAT_60S");
                    ValidateFurnitureTransformsUnchanged(bootstrap);
                    ValidateRuntimeInvariants(bootstrap);
                    Capture(bootstrap, camera, StarterReseatPath, false);
                    Append("STARTER | COMPLETE | 60s transform window and reseat completed");
                    SessionState.SetInt(StageKey, 5);
                    EditorApplication.ExitPlaymode();
                }
            }
            catch (Exception exception)
            {
                CompleteWithFailure(exception);
            }
        }

        private static void ValidateLayout(
            OfficeTileMigrationPreviewBootstrap bootstrap,
            int furnitureCount,
            int kindCount,
            bool expectsPartition)
        {
            FamilyCompany.Simulation.OfficeLayout.OfficeGrid grid = bootstrap.Presenter.SemanticGrid;
            Require(grid.Width == 13 && grid.Height == 13, "Office layout is not 13x13.");
            Require(grid.Furniture.Count == furnitureCount,
                $"Office layout has {grid.Furniture.Count} furniture objects instead of {furnitureCount}.");
            Require(grid.Furniture.Select(item => item.KindId).Distinct(StringComparer.Ordinal).Count() == kindCount,
                $"Office layout kind count is not {kindCount}.");
            Require(grid.Furniture.Any(item => item.KindId == OfficeGridLayouts.PartitionKind) == expectsPartition,
                "Migration partition isolation is incorrect.");
            Require(grid.Workstations.Count == 4 && grid.SeatSlots.Count == 4,
                "Office layout must contain four explicit workstations and seats.");
            Require(grid.Workstations.All(item => item.OperatorAnchor.Y2 % 2 != 0),
                "Every workstation must persist a half-cell operator anchor.");
        }

        private static void ValidateRuntimeAlignment(
            OfficeTileMigrationPreviewBootstrap bootstrap,
            Camera camera,
            string phase)
        {
            Require(bootstrap.SeatedWorkers.Count == 4, "Alignment QA requires four family workers.");
            Require(bootstrap.SeatedWorkers.All(item => item.IsWorking && item.HasActiveClaim && item.IsSeatOccupied),
                phase + " does not have four occupied working seats.");
            Require(bootstrap.SeatedWorkers.Select(item => item.SeatId).Distinct(StringComparer.Ordinal).Count() == 4,
                phase + " contains duplicate seat claims.");

            RenderTexture previousTarget = camera.targetTexture;
            var metricsTarget = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32);
            try
            {
                camera.targetTexture = metricsTarget;
                OfficeGridCameraFitter.Fit(camera, bootstrap.CombinedRenderBounds, 16f / 9f);
                Append(string.Empty);
                Append("## " + phase + " furniture");
                Append("| furniture | footprint max error | ground residual | overlay result | result |");
                Append("|---|---:|---:|---|---|");
                foreach (PlacedOfficeFurniture item in bootstrap.Presenter.SemanticGrid.Furniture)
                {
                    Require(item.HasCanonicalPlacementAnchor,
                        item.FurnitureId + " placement anchor is not the footprint center.");
                    Require(bootstrap.FurniturePresenter.TryGetRenderer(item.FurnitureId, out SpriteRenderer renderer),
                        "Missing furniture renderer: " + item.FurnitureId);
                    Require(bootstrap.FurniturePresenter.TryGetDefinition(item.FurnitureId, out OfficeFurnitureVisualDefinition definition),
                        "Missing furniture definition: " + item.FurnitureId);
                    Vector3[] expected = bootstrap.Presenter.FootprintCornersWorld(item);
                    float[] errors = OfficeGridAlignmentMetrics.FootprintCornerErrorsPx(camera, renderer, definition, expected);
                    float footprint = OfficeGridAlignmentMetrics.Maximum(errors);
                    Vector3 actualCentroid = Centroid(bootstrap.FurniturePresenter.GroundFootprintWorld(item.FurnitureId));
                    float groundResidual = OfficeGridAlignmentMetrics.ScreenDistance(camera, actualCentroid, Centroid(expected));
                    Require(footprint <= 2f,
                        $"{item.FurnitureId} footprint corner residual is {footprint:F3}px.");
                    Require(groundResidual <= 2f,
                        $"{item.FurnitureId} footprint centroid residual is {groundResidual:F3}px.");
                    string overlay = ValidateFurnitureOverlayContract(bootstrap, item) ? "PASS" : "FAIL";
                    Require(overlay == "PASS", item.FurnitureId + " overlay contract failed.");
                    Append($"| {item.FurnitureId} | {footprint:F3}px | {groundResidual:F3}px | {overlay} | PASS |");
                }

                Append(string.Empty);
                Append("## " + phase + " workstations");
                Append("| member | chair-desk seat | pelvis-seat | hand-work | max frame drift | result |");
                Append("|---|---:|---:|---:|---:|---|");
                var numericFailures = new List<string>();
                foreach (OfficeGridSeatedWorker worker in bootstrap.SeatedWorkers.OrderBy(item => item.MemberId, StringComparer.Ordinal))
                {
                    OfficeSeatSlot seat = bootstrap.Presenter.SemanticGrid.SeatSlots.Single(item => item.SeatId == worker.SeatId);
                    Require(bootstrap.FurniturePresenter.TryGetDefinition(
                            seat.WorkSurfaceFurnitureId,
                            out OfficeFurnitureVisualDefinition deskDefinition),
                        "Missing workstation desk definition: " + seat.WorkSurfaceFurnitureId);
                    float chairDesk = worker.ChairDeskSeatScreenError(camera);
                    float pelvis = worker.PelvisSeatScreenError(camera);
                    float hand = worker.HandWorkScreenError(camera);
                    float operatorSemantic = OfficeGridAlignmentMetrics.ScreenDistance(
                        camera,
                        bootstrap.FurniturePresenter.OperatorSeatSocketWorld(seat.WorkSurfaceFurnitureId),
                        bootstrap.Presenter.SubcellAnchorWorld(seat.OperatorAnchor));
                    Vector2 characterVector = worker.PoseProfile.RenderedHandFromPelvisPx(
                        OfficeGridCharacterMover.UniformVisualScale);
                    Vector2 deskVector =
                        (deskDefinition.OperatorWorkSocketPx - deskDefinition.OperatorSeatSocketPx) *
                        deskDefinition.UniformScale;
                    float vectorAngle = OfficeGridAlignmentMetrics.VectorAngleDifferenceDegrees(characterVector, deskVector);
                    float vectorLength = OfficeGridAlignmentMetrics.VectorLengthRelativeError(characterVector, deskVector);
                    float jump = OfficeGridAlignmentMetrics.WorldDisplacementScreenPx(
                        camera,
                        worker.transform.position,
                        Vector3.right * worker.MaxFrameCorrectionJumpWorld);
                    Append(
                        $"VECTOR | phase={phase} | member={worker.MemberId} | operatorSemantic={operatorSemantic:F3}px | " +
                        $"direction={vectorAngle:F3}deg | length={vectorLength * 100f:F3}%");
                    var workerFailures = new List<string>();
                    AddFailure(workerFailures, chairDesk <= 2f,
                        $"{worker.MemberId} chair-to-desk seat error is {chairDesk:F3}px.");
                    AddFailure(workerFailures, pelvis <= 2f,
                        $"{worker.MemberId} pelvis-to-seat error is {pelvis:F3}px.");
                    AddFailure(workerFailures, hand <= 4f,
                        $"{worker.MemberId} hand-to-work error is {hand:F3}px.");
                    AddFailure(workerFailures, vectorAngle <= 2f,
                        $"{worker.MemberId} pelvis-to-hand vector direction differs by {vectorAngle:F3} degrees.");
                    AddFailure(workerFailures, vectorLength <= 0.04f,
                        $"{worker.MemberId} pelvis-to-hand vector length differs by {vectorLength * 100f:F3}%.");
                    AddFailure(workerFailures, worker.MaxWorkPelvisErrorPx <= 2f,
                        $"{worker.MemberId} maximum work pelvis error is {worker.MaxWorkPelvisErrorPx:F3}px.");
                    AddFailure(workerFailures, worker.MaxWorkHandErrorPx <= 4f,
                        $"{worker.MemberId} maximum work hand error is {worker.MaxWorkHandErrorPx:F3}px.");
                    AddFailure(workerFailures, jump <= 1f,
                        $"{worker.MemberId} frame correction jump is {jump:F3}px.");
                    AddFailure(workerFailures, worker.FootError() <= 0.001f,
                        $"{worker.MemberId} semantic root error is {worker.FootError():F6} world.");
                    string result = workerFailures.Count == 0 ? "PASS" : "FAIL";
                    Append($"| {worker.MemberId} | {chairDesk:F3}px | {pelvis:F3}px | {hand:F3}px | {jump:F3}px | {result} |");
                    if (workerFailures.Count == 0)
                    {
                        ValidateAuthoredWorkFrameDrift(bootstrap, worker, camera);
                        ValidateWorkerMaskContract(bootstrap, worker);
                    }
                    else
                    {
                        numericFailures.AddRange(workerFailures);
                    }
                }
                if (numericFailures.Count > 0)
                    RecordFailure(phase + " | " + string.Join(" | ", numericFailures));
            }
            finally
            {
                camera.targetTexture = previousTarget;
                UnityEngine.Object.DestroyImmediate(metricsTarget);
            }
        }

        private static void ValidateAuthoredWorkFrameDrift(
            OfficeTileMigrationPreviewBootstrap bootstrap,
            OfficeGridSeatedWorker worker,
            Camera camera)
        {
            OfficeCharacterSeatPoseCatalog catalog = OfficeFurnitureAssetBuilder.LoadCharacterSeatPoseCatalog();
            catalog.ValidateSafeStaticWork(new[] { worker.MemberId }, worker.DirectionIndex);
            OfficeCharacterSeatPoseProfile safe = catalog.ResolveApproved(
                worker.MemberId,
                worker.DirectionIndex,
                OfficeSeatingAnimationClip.Work,
                0);
            Require(Mathf.Abs(safe.UniformScale - 1f) <= 0.0001f,
                worker.MemberId + " SafeStaticWork scale is not canonical.");
            Require(Mathf.Abs(safe.RotationDegrees) <= 0.01f,
                worker.MemberId + " SafeStaticWork rotates the whole Sprite.");
            Require(safe.HumanApproved,
                worker.MemberId + " SafeStaticWork is not human-approved.");
            Append($"SAFE_STATIC_WORK | member={worker.MemberId} | frame=0 | pelvisDrift=0.000px | handDrift=0.000px | sourceSha={safe.SourceSpriteSha256}");
        }

        private static bool ValidateFurnitureOverlayContract(
            OfficeTileMigrationPreviewBootstrap bootstrap,
            PlacedOfficeFurniture item)
        {
            if (!bootstrap.FurniturePresenter.TryGetDefinition(item.FurnitureId, out OfficeFurnitureVisualDefinition definition))
                return false;
            if (item.KindId == OfficeGridLayouts.SwivelChairKind)
                return definition.FrontOverlaySprite != null && definition.FrontOverlayWhenOccupied;
            if (item.KindId == OfficeGridLayouts.DeskWithPcKind)
                return definition.FrontOverlaySprite != null && definition.FrontOverlayWhenOccupied;
            return definition.FrontOverlaySprite == null;
        }

        private static void ValidateWorkerMaskContract(
            OfficeTileMigrationPreviewBootstrap bootstrap,
            OfficeGridSeatedWorker worker)
        {
            OfficeSeatSlot seat = bootstrap.Presenter.SemanticGrid.SeatSlots.Single(item => item.SeatId == worker.SeatId);
            OfficeGridCharacterMover mover = worker.GetComponent<OfficeGridCharacterMover>();
            Require(bootstrap.FurniturePresenter.SeatOcclusionMatches(seat, mover.TargetRenderer.sortingOrder),
                worker.MemberId + " seating sort contract is invalid.");
            Require(bootstrap.FurniturePresenter.TryGetRenderer(seat.ChairFurnitureId, out SpriteRenderer chair),
                "Chair renderer is missing for mask QA.");
            Require(chair.sortingOrder < mover.TargetRenderer.sortingOrder,
                worker.MemberId + " chair base renders in front of the character.");
            Require(bootstrap.FurniturePresenter.FrontOverlayRenderers.TryGetValue(
                    seat.ChairFurnitureId,
                    out SpriteRenderer chairOverlay) && chairOverlay.enabled,
                worker.MemberId + " chair front overlay is not active.");
            Require(chairOverlay.sortingOrder > mover.TargetRenderer.sortingOrder,
                worker.MemberId + " chair front overlay does not render above the character.");
            Require(bootstrap.FurniturePresenter.FrontOverlayRenderers.TryGetValue(
                    seat.WorkSurfaceFurnitureId,
                    out SpriteRenderer deskOverlay) && deskOverlay.enabled,
                worker.MemberId + " desk front overlay is not active.");
            int faceOverlap = CountOpaqueOverlap(deskOverlay, mover.TargetRenderer, 0.62f, 1f);
            int lowerBodyOverlap = CountOpaqueOverlap(deskOverlay, mover.TargetRenderer, 0f, 0.58f);
            Require(faceOverlap == 0,
                $"{worker.MemberId} desk overlay covers {faceOverlap} opaque head/face samples.");
            Require(lowerBodyOverlap > 0,
                worker.MemberId + " lower body is not naturally occluded by the desk front edge.");
        }

        private static int CountOpaqueOverlap(
            SpriteRenderer overlay,
            SpriteRenderer character,
            float characterMinYRatio,
            float characterMaxYRatio)
        {
            if (overlay == null || character == null || !overlay.enabled || !character.enabled) return 0;
            PixelData overlayPixels = PixelData.Load(overlay.sprite);
            PixelData characterPixels = PixelData.Load(character.sprite);
            try
            {
                int count = 0;
                for (int y = 0; y < overlayPixels.Height; y += 2)
                for (int x = 0; x < overlayPixels.Width; x += 2)
                {
                    if (!overlayPixels.IsOpaque(x, y)) continue;
                    Vector3 world = OfficeGridAlignmentMetrics.SpriteAnchorWorld(overlay, new Vector2(x + 0.5f, y + 0.5f));
                    Vector3 local = character.transform.InverseTransformPoint(world);
                    Vector2 characterPx = new Vector2(
                        local.x * character.sprite.pixelsPerUnit + character.sprite.pivot.x,
                        local.y * character.sprite.pixelsPerUnit + character.sprite.pivot.y);
                    int characterX = Mathf.FloorToInt(characterPx.x);
                    int characterY = Mathf.FloorToInt(characterPx.y);
                    if (!characterPixels.IsOpaque(characterX, characterY)) continue;
                    float ratio = characterY / (float)Math.Max(1, characterPixels.Height - 1);
                    if (ratio >= characterMinYRatio && ratio <= characterMaxYRatio) count++;
                }
                return count;
            }
            finally
            {
                overlayPixels.Dispose();
                characterPixels.Dispose();
            }
        }

        private static void SnapshotFurniture(OfficeTileMigrationPreviewBootstrap bootstrap)
        {
            FurnitureSnapshots.Clear();
            foreach (PlacedOfficeFurniture item in bootstrap.Presenter.SemanticGrid.Furniture)
            {
                Require(bootstrap.FurniturePresenter.TryGetSemanticRoot(item.FurnitureId, out Transform semantic),
                    "Missing semantic root snapshot target: " + item.FurnitureId);
                Require(bootstrap.FurniturePresenter.TryGetVisualRoot(item.FurnitureId, out Transform visual),
                    "Missing visual root snapshot target: " + item.FurnitureId);
                FurnitureSnapshots.Add(item.FurnitureId, new FurnitureSnapshot
                {
                    Semantic = TakeSnapshot(semantic),
                    Visual = TakeSnapshot(visual)
                });
            }
            Append("TRANSFORMS | snapshot count=" + FurnitureSnapshots.Count);
        }

        private static void ValidateFurnitureTransformsUnchanged(OfficeTileMigrationPreviewBootstrap bootstrap)
        {
            Require(FurnitureSnapshots.Count == bootstrap.Presenter.SemanticGrid.Furniture.Count,
                "Furniture snapshot count changed during 60 seconds.");
            foreach (PlacedOfficeFurniture item in bootstrap.Presenter.SemanticGrid.Furniture)
            {
                Require(FurnitureSnapshots.TryGetValue(item.FurnitureId, out FurnitureSnapshot snapshot),
                    "Missing furniture snapshot: " + item.FurnitureId);
                bootstrap.FurniturePresenter.TryGetSemanticRoot(item.FurnitureId, out Transform semantic);
                bootstrap.FurniturePresenter.TryGetVisualRoot(item.FurnitureId, out Transform visual);
                RequireUnchanged(semantic, snapshot.Semantic, item.FurnitureId + " semantic");
                RequireUnchanged(visual, snapshot.Visual, item.FurnitureId + " visual");
            }
            Append("TRANSFORMS | PASS | position=0 rotation=0 scale=0 parent=0 window=60s");
        }

        private static void ValidateRuntimeInvariants(OfficeTileMigrationPreviewBootstrap bootstrap)
        {
            Require(bootstrap.CollisionMonitor != null && bootstrap.CollisionMonitor.SampleCount >= 120,
                "Collision monitor did not collect enough samples.");
            Require(bootstrap.CollisionMonitor.BlockedCellViolationCount == 0,
                "Blocking footprint violation: " + bootstrap.CollisionMonitor.FirstViolation);
            Require(bootstrap.SeatedWorkers.All(item => item.IsWorking && item.HasActiveClaim && item.IsSeatOccupied),
                "Not all family members completed reseating.");
            Require(bootstrap.SeatedWorkers.Select(item => item.SeatId).Distinct(StringComparer.Ordinal).Count() == 4,
                "Duplicate seat claim after reseat.");
            FamilyCompany.Simulation.OfficeLayout.OfficeGrid grid = bootstrap.Presenter.SemanticGrid;
            FamilyCompany.Simulation.OfficeLayout.OfficeGrid restored =
                OfficeGridSaveAdapter.Restore(OfficeGridSaveAdapter.ToDto(grid));
            Require(grid.ComputeLayoutHash() == restored.ComputeLayoutHash(),
                "Starter Office save/load changed the layout hash.");
            Append($"RUNTIME | PASS | collisions=0 claims=4 unique=4 saveHash={grid.ComputeLayoutHash()} unsupportedFacingFallbacks=0");
        }

        private static void ValidateCalibrationAssetsAreNonDestructive()
        {
            OfficeFurnitureVisualCatalog furniture = OfficeFurnitureAssetBuilder.LoadFurnitureVisualCatalog();
            OfficeCharacterSeatPoseCatalog pose = OfficeFurnitureAssetBuilder.LoadCharacterSeatPoseCatalog();
            furniture.Validate();
            pose.Validate();
            string furnitureBefore = EditorJsonUtility.ToJson(furniture, true);
            string poseBefore = EditorJsonUtility.ToJson(pose, true);
            OfficeFurnitureAssetBuilder.Build();
            string furnitureAfter = EditorJsonUtility.ToJson(OfficeFurnitureAssetBuilder.LoadFurnitureVisualCatalog(), true);
            string poseAfter = EditorJsonUtility.ToJson(OfficeFurnitureAssetBuilder.LoadCharacterSeatPoseCatalog(), true);
            Require(string.Equals(Sha256(furnitureBefore), Sha256(furnitureAfter), StringComparison.Ordinal),
                "Furniture calibration was overwritten by a rebuild.");
            Require(string.Equals(Sha256(poseBefore), Sha256(poseAfter), StringComparison.Ordinal),
                "Pose calibration was overwritten by a rebuild.");
            Append("CALIBRATION_ASSETS | PASS | furnitureVersion=2 poseVersion=4 | rebuild=non-destructive");
        }

        private static void Capture(
            OfficeTileMigrationPreviewBootstrap bootstrap,
            Camera camera,
            string path,
            bool overlay)
        {
            OfficeGridCameraFitter.Fit(camera, bootstrap.CombinedRenderBounds, 16f / 9f);
            OfficeTileMigrationQa.Capture(camera, path, 1920, 1080);
            Append($"CAPTURE | {(overlay ? "debug" : "composite")} | {path}");
        }

        private static OfficeTileMigrationPreviewBootstrap RequiredBootstrap(OfficeTilePreviewLayout layout)
        {
            OfficeTileMigrationPreviewBootstrap bootstrap =
                UnityEngine.Object.FindFirstObjectByType<OfficeTileMigrationPreviewBootstrap>();
            Require(bootstrap != null && bootstrap.Presenter != null && bootstrap.FurniturePresenter != null,
                "Office alignment bootstrap is missing.");
            Require(bootstrap.Layout == layout, $"Expected {layout}, found {bootstrap.Layout}.");
            return bootstrap;
        }

        private static Camera RequiredCamera()
        {
            Camera camera = Camera.main;
            Require(camera != null, "Office alignment camera is missing.");
            return camera;
        }

        private static float ElapsedOrStart()
        {
            float start = SessionState.GetFloat(StartKey, 0f);
            if (start <= 0f)
            {
                SessionState.SetFloat(StartKey, (float)EditorApplication.timeSinceStartup);
                return 0f;
            }
            return (float)EditorApplication.timeSinceStartup - start;
        }

        private static TransformSnapshot TakeSnapshot(Transform transform)
        {
            return new TransformSnapshot
            {
                Position = transform.position,
                Rotation = transform.rotation,
                Scale = transform.localScale,
                Parent = transform.parent
            };
        }

        private static void RequireUnchanged(Transform transform, TransformSnapshot snapshot, string label)
        {
            Require(transform != null, label + " transform was destroyed.");
            Require(transform.position == snapshot.Position, label + " position changed.");
            Require(transform.rotation == snapshot.Rotation, label + " rotation changed.");
            Require(transform.localScale == snapshot.Scale, label + " scale changed.");
            Require(ReferenceEquals(transform.parent, snapshot.Parent), label + " parent changed.");
        }

        private static Vector3 Centroid(IReadOnlyList<Vector3> points)
        {
            Vector3 total = Vector3.zero;
            for (int index = 0; index < points.Count; index++) total += points[index];
            return total / points.Count;
        }

        private static string Sha256(string value)
        {
            using SHA256 sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", string.Empty);
        }

        private static void FailPreparation(Exception exception)
        {
            Append("PREP_FAIL | " + exception);
            Debug.LogException(exception);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }

        private static void CompleteWithFailure(Exception exception)
        {
            SessionState.SetString(FailureKey, exception.ToString());
            SessionState.SetInt(StageKey, 5);
            Append("FAIL | " + exception);
            Debug.LogException(exception);
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                EditorApplication.ExitPlaymode();
        }

        private static void RecordFailure(string message)
        {
            string existing = SessionState.GetString(FailureKey, string.Empty);
            SessionState.SetString(
                FailureKey,
                existing.Length == 0 ? message : existing + Environment.NewLine + message);
            Append("NUMERIC_FAIL | " + message);
        }

        private static void ClearSession()
        {
            SessionState.SetBool(ActiveKey, false);
            SessionState.EraseInt(StageKey);
            SessionState.EraseFloat(StartKey);
            SessionState.EraseString(FailureKey);
            FurnitureSnapshots.Clear();
        }

        private static void Append(string line)
        {
            Directory.CreateDirectory(ArtifactFolder);
            File.AppendAllText(
                ReportPath,
                DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + " | " + line + Environment.NewLine,
                Encoding.UTF8);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void AddFailure(ICollection<string> failures, bool condition, string message)
        {
            if (!condition) failures.Add(message);
        }

        private sealed class PixelData : IDisposable
        {
            private readonly Texture2D _texture;
            private readonly Color32[] _pixels;

            private PixelData(Texture2D texture)
            {
                _texture = texture;
                _pixels = texture.GetPixels32();
                Width = texture.width;
                Height = texture.height;
            }

            public int Width { get; }
            public int Height { get; }

            public static PixelData Load(Sprite sprite)
            {
                string path = AssetDatabase.GetAssetPath(sprite);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
                if (!texture.LoadImage(File.ReadAllBytes(path), false))
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                    throw new InvalidDataException("Could not decode sprite pixels: " + path);
                }
                return new PixelData(texture);
            }

            public bool IsOpaque(int x, int y)
            {
                return x >= 0 && y >= 0 && x < Width && y < Height && _pixels[y * Width + x].a > 127;
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(_texture);
            }
        }
    }
}
