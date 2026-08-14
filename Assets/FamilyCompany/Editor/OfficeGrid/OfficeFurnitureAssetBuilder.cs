using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using FamilyCompany.Presentation.Unity.OfficeGridView;
using FamilyCompany.Presentation.Unity.OfficeGridView.Authoring;
using FamilyCompany.Presentation.Unity.OfficeSeating;
using FamilyCompany.Presentation.Unity.OfficeSeating.Authoring;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor.OfficeGridQa
{
    public static class OfficeFurnitureAssetBuilder
    {
        public const string SourceFolder = "Assets/Art/Office/Tiles/Furniture/Source";
        public const string RuntimeFolder = "Assets/Art/Office/Tiles/Furniture/Runtime";
        public const string FurnitureCatalogPath =
            "Assets/FamilyCompany/Presentation.Unity/OfficeGrid/Authoring/OfficeFurnitureVisualCatalog.asset";
        public const string PoseCatalogPath =
            "Assets/FamilyCompany/Presentation.Unity/OfficeGrid/Authoring/OfficeCharacterSeatPoseCatalog.asset";
        public const int CanvasWidth = 640;
        public const int CanvasHeight = 512;
        public const int VisibleMarginPixels = 24;
        public const float PixelsPerUnit = 180f;

        private sealed class FurnitureSpec
        {
            public FurnitureSpec(
                string kindId,
                string stem,
                int maximumWidth,
                int maximumHeight,
                OfficeFurnitureFacing facing,
                Vector2 sourceGroundAnchorPx,
                Vector2 sourceSortAnchorPx,
                string version = "v2",
                string sourceStem = null,
                Vector2? sourceSeatAnchorPx = null,
                Vector2? sourceWorkSurfaceAnchorPx = null,
                Vector2[] sourceForegroundPolygon = null,
                Vector2[] sourceForegroundExclusionPolygon = null,
                Vector2? sourceOperatorSeatSocketPx = null,
                int semanticFootprintWidth = 1,
                int semanticFootprintHeight = 1)
            {
                KindId = kindId;
                Stem = stem;
                MaximumWidth = maximumWidth;
                MaximumHeight = maximumHeight;
                Facing = facing;
                SourceGroundAnchorPx = sourceGroundAnchorPx;
                SourceSortAnchorPx = sourceSortAnchorPx;
                Version = version;
                SourceStem = string.IsNullOrWhiteSpace(sourceStem) ? stem : sourceStem;
                SourceSeatAnchorPx = sourceSeatAnchorPx;
                SourceWorkSurfaceAnchorPx = sourceWorkSurfaceAnchorPx;
                SourceForegroundPolygon = sourceForegroundPolygon ?? Array.Empty<Vector2>();
                SourceForegroundExclusionPolygon = sourceForegroundExclusionPolygon ?? Array.Empty<Vector2>();
                SourceOperatorSeatSocketPx = sourceOperatorSeatSocketPx;
                SemanticFootprintWidth = semanticFootprintWidth;
                SemanticFootprintHeight = semanticFootprintHeight;
            }

            public string KindId { get; }
            public string Stem { get; }
            public int MaximumWidth { get; }
            public int MaximumHeight { get; }
            public OfficeFurnitureFacing Facing { get; }
            public string Version { get; }
            public string SourceStem { get; }
            public Vector2 SourceGroundAnchorPx { get; }
            public Vector2 SourceSortAnchorPx { get; }
            public Vector2? SourceSeatAnchorPx { get; }
            public Vector2? SourceWorkSurfaceAnchorPx { get; }
            public Vector2[] SourceForegroundPolygon { get; }
            public Vector2[] SourceForegroundExclusionPolygon { get; }
            public Vector2? SourceOperatorSeatSocketPx { get; }
            public int SemanticFootprintWidth { get; }
            public int SemanticFootprintHeight { get; }
            public string SourcePath => $"{SourceFolder}/{SourceStem}_alpha_{Version}.png";
            public string RuntimePath => $"{RuntimeFolder}/{Stem}_{Version}.png";
            public string FrontPath => $"{RuntimeFolder}/{Stem}_front_{Version}.png";

            public Vector2 RuntimeGroundAnchorPx { get; set; }
            public Vector2 RuntimeSortAnchorPx { get; set; }
            public Vector2 RuntimeSeatAnchorPx { get; set; }
            public Vector2 RuntimeWorkSurfaceAnchorPx { get; set; }
            public Vector2 RuntimeOperatorSeatSocketPx { get; set; }
            public Vector2[] RuntimeGroundFootprintPolygonPx { get; set; } = Array.Empty<Vector2>();
            public float BakedUniformScale { get; set; }
            public float SourceAspect { get; set; }
            public float RuntimeAspect { get; set; }
        }

        private static readonly FurnitureSpec[] Specs =
        {
            new FurnitureSpec(
                OfficeGridLayouts.DeskWithPcKind,
                "office_workstation",
                500,
                360,
                OfficeFurnitureFacing.SouthEast,
                new Vector2(760f, 200f),
                new Vector2(705f, 125f),
                "v4",
                // Actual CRT-keyboard contact, not the former lower desk-edge proxy.
                sourceWorkSurfaceAnchorPx: new Vector2(842.7f, 578.5f),
                // Shares the same presentation correction as the paired chair.
                sourceOperatorSeatSocketPx: new Vector2(1005.35f, 433.28f),
                semanticFootprintWidth: 2,
                sourceForegroundPolygon: new[]
                {
                    new Vector2(320f, 100f), new Vector2(1225f, 100f),
                    new Vector2(1225f, 520f), new Vector2(710f, 365f),
                    new Vector2(320f, 550f)
                },
                // The right drawer edge crosses older_sister's approved upper-body pixels.
                // Keep the lower desk occlusion, but never redraw this small region over a face.
                sourceForegroundExclusionPolygon: new[]
                {
                    new Vector2(968f, 435f), new Vector2(1016f, 435f),
                    new Vector2(1016f, 461f), new Vector2(968f, 461f)
                }),
            new FurnitureSpec(
                OfficeGridLayouts.SwivelChairKind,
                "office_swivel_chair",
                175,
                240,
                OfficeFurnitureFacing.NorthWest,
                new Vector2(647f, 227f),
                new Vector2(647f, 223f),
                "v4",
                "office_open_back_chair_northwest",
                new Vector2(619f, 588f),
                // Keep the complete open back frame and the near seat/leg outline visible in
                // front of an occupant. The exclusion leaves the cushion centre behind the body,
                // so the character reads as seated rather than pasted behind a solid chair.
                sourceForegroundPolygon: new[]
                {
                    new Vector2(390f, 210f), new Vector2(930f, 210f),
                    new Vector2(930f, 950f), new Vector2(390f, 950f)
                },
                sourceForegroundExclusionPolygon: new[]
                {
                    new Vector2(430f, 500f), new Vector2(840f, 500f),
                    new Vector2(840f, 690f), new Vector2(430f, 690f)
                }),
            new FurnitureSpec(OfficeGridLayouts.ReceptionCounterKind, "office_reception_counter", 500, 340,
                OfficeFurnitureFacing.SouthEast, new Vector2(834f, 180f), new Vector2(834f, 162f),
                semanticFootprintWidth: 2),
            new FurnitureSpec(OfficeGridLayouts.MeetingTableKind, "office_meeting_table", 460, 300,
                OfficeFurnitureFacing.SouthEast, new Vector2(887f, 175f), new Vector2(887f, 145f),
                semanticFootprintWidth: 2),
            new FurnitureSpec(OfficeGridLayouts.DocumentBookcaseKind, "office_document_bookcase", 300, 360,
                OfficeFurnitureFacing.SouthEast, new Vector2(818f, 108f), new Vector2(818f, 93f)),
            new FurnitureSpec(OfficeGridLayouts.FaxCopierKind, "office_fax_copier", 280, 370,
                OfficeFurnitureFacing.SouthEast, new Vector2(877f, 100f), new Vector2(877f, 82f)),
            new FurnitureSpec(OfficeGridLayouts.WaterDispenserKind, "office_water_dispenser", 190, 360,
                OfficeFurnitureFacing.SouthEast, new Vector2(887f, 122f), new Vector2(887f, 106f)),
            new FurnitureSpec(OfficeGridLayouts.SofaKind, "office_sofa", 450, 330,
                OfficeFurnitureFacing.SouthEast, new Vector2(895f, 150f), new Vector2(895f, 124f),
                semanticFootprintWidth: 2),
            new FurnitureSpec(OfficeGridLayouts.CoffeeTableKind, "office_coffee_table", 380, 220,
                OfficeFurnitureFacing.SouthEast, new Vector2(868f, 180f), new Vector2(868f, 150f),
                semanticFootprintWidth: 2),
            new FurnitureSpec(OfficeGridLayouts.PottedPlantKind, "office_potted_plant", 230, 330,
                OfficeFurnitureFacing.SouthEast, new Vector2(834f, 165f), new Vector2(834f, 142f)),
            new FurnitureSpec(OfficeGridLayouts.PartitionKind, "office_partition", 430, 360,
                OfficeFurnitureFacing.NorthWest, new Vector2(834f, 126f), new Vector2(834f, 108f),
                semanticFootprintHeight: 2),
            new FurnitureSpec(OfficeGridLayouts.FilingCabinetKind, "office_filing_cabinet", 200, 370,
                OfficeFurnitureFacing.SouthEast, new Vector2(884f, 100f), new Vector2(884f, 82f)),
            new FurnitureSpec(OfficeGridLayouts.EntranceDoorKind, "office_entrance_door", 175, 420,
                OfficeFurnitureFacing.SouthEast, new Vector2(316f, 172f), new Vector2(316f, 172f),
                "v1"),
            new FurnitureSpec(OfficeGridLayouts.EntranceWallKind, "office_perimeter_wall", 175, 420,
                OfficeFurnitureFacing.SouthEast, new Vector2(316f, 172f), new Vector2(316f, 172f),
                "v1"),
            new FurnitureSpec(OfficeGridLayouts.PerimeterCutawayWallKind, "office_perimeter_cutaway_wall", 175, 300,
                OfficeFurnitureFacing.SouthEast, new Vector2(316f, 172f), new Vector2(316f, 172f),
                "v1")
        };

        public static IReadOnlyList<string> KindIds => Specs.Select(item => item.KindId).ToArray();

        [MenuItem("Family Company/Art/Build Office Furniture Tycoon Alignment V2")]
        public static void Build()
        {
            Directory.CreateDirectory(RuntimeFolder);
            BuildAllRuntimePngs();
            var firstHashes = RuntimePaths().ToDictionary(path => path, Sha256, StringComparer.Ordinal);
            BuildAllRuntimePngs();
            foreach (string path in RuntimePaths())
            {
                if (!string.Equals(firstHashes[path], Sha256(path), StringComparison.Ordinal))
                    throw new InvalidOperationException("Furniture runtime build is not deterministic: " + path);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (FurnitureSpec spec in Specs)
            {
                ConfigureImporter(spec.RuntimePath, spec.RuntimeGroundAnchorPx);
                if (spec.SourceForegroundPolygon.Length > 0)
                    ConfigureImporter(spec.FrontPath, spec.RuntimeGroundAnchorPx);
            }

            CreateOrUpgradeFurnitureCatalog();
            CreateOrUpgradePoseCatalog();
            UpgradeAnimatedNorthwestPoseCatalog();
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("FAMILY_COMPANY_OFFICE_FURNITURE_TYCOON_ALIGNMENT_V2_BUILD: PASS");
        }

        [MenuItem("Family Company/Art/Build Office Perimeter Walls Only")]
        public static void BuildPerimeterWalls()
        {
            FurnitureSpec[] perimeterSpecs = Specs.Where(spec => IsPerimeterKind(spec.KindId)).ToArray();
            if (perimeterSpecs.Length != 3)
                throw new InvalidOperationException($"Expected exactly three perimeter visual specs, found {perimeterSpecs.Length}.");

            Directory.CreateDirectory(RuntimeFolder);
            foreach (FurnitureSpec spec in perimeterSpecs) BuildOne(spec);
            var firstHashes = perimeterSpecs.ToDictionary(
                spec => spec.RuntimePath,
                spec => Sha256(spec.RuntimePath),
                StringComparer.Ordinal);
            foreach (FurnitureSpec spec in perimeterSpecs) BuildOne(spec);
            foreach (FurnitureSpec spec in perimeterSpecs)
            {
                if (!string.Equals(firstHashes[spec.RuntimePath], Sha256(spec.RuntimePath), StringComparison.Ordinal))
                    throw new InvalidOperationException("Perimeter runtime build is not deterministic: " + spec.RuntimePath);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (FurnitureSpec spec in perimeterSpecs)
                ConfigureImporter(spec.RuntimePath, spec.RuntimeGroundAnchorPx);

            UpdatePerimeterWallCatalog(perimeterSpecs);
            ValidatePerimeterWalls(perimeterSpecs);
            AssetDatabase.SaveAssets();
            Debug.Log(
                "FAMILY_COMPANY_OFFICE_PERIMETER_WALL_BUILD: PASS | " +
                "kinds=3 oneTileSpan=160x80 openPassage=true chairDefinitionUntouched=true");
        }

        public static void RunPerimeterWallsBatch()
        {
            try
            {
                BuildPerimeterWalls();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void RunBatch()
        {
            try
            {
                Build();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("Family Company/Rebuild Office Chair Foreground Only")]
        public static void RebuildChairForegroundOnly()
        {
            FurnitureSpec chair = Specs.Single(spec =>
                string.Equals(spec.KindId, OfficeGridLayouts.SwivelChairKind, StringComparison.Ordinal));
            BuildOne(chair, writeBase: false, writeFront: true);
            AssetDatabase.ImportAsset(chair.FrontPath, ImportAssetOptions.ForceSynchronousImport);
            OfficeChairForegroundValidation.Validate();
            Debug.Log(
                "OFFICE_CHAIR_FOREGROUND_ONLY_BUILD: PASS sourcePixels=4161 openBack=31x56 " +
                "otherFurnitureWrites=0 catalogWrites=0");
        }

        public static void RebuildChairForegroundOnlyBatch()
        {
            try
            {
                RebuildChairForegroundOnly();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static OfficeFurnitureVisualCatalog LoadFurnitureVisualCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<OfficeFurnitureVisualCatalog>(FurnitureCatalogPath);
            if (catalog == null) throw new FileNotFoundException("Office furniture visual catalog is missing.", FurnitureCatalogPath);
            catalog.Validate();
            return catalog;
        }

        public static OfficeCharacterSeatPoseCatalog LoadCharacterSeatPoseCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<OfficeCharacterSeatPoseCatalog>(PoseCatalogPath);
            if (catalog == null) throw new FileNotFoundException("Office character seat pose catalog is missing.", PoseCatalogPath);
            catalog.Validate();
            return catalog;
        }

        public static void UpgradePoseCatalog()
        {
            UpgradeAnimatedNorthwestPoseCatalog();
            LoadCharacterSeatPoseCatalog().ValidateAnimatedNorthwest(
                new[] { "player", "older_sister", "father", "mother" },
                (int)OfficeSeatFacing8.Northwest);
        }

        [MenuItem("Family Company/Art/Approve Northwest Seating Animation V5")]
        public static void UpgradeAnimatedNorthwestPoseCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<OfficeCharacterSeatPoseCatalog>(PoseCatalogPath);
            if (catalog == null)
                throw new FileNotFoundException("Office character seat pose catalog is missing.", PoseCatalogPath);
            bool safeV4Source = catalog.CalibrationVersion == 4 && catalog.Profiles.Count == 4;
            bool bootstrapV5Source = catalog.CalibrationVersion == 5 && catalog.Profiles.Count == 4;
            bool repeatableV5Source = catalog.CalibrationVersion == 5 && catalog.Profiles.Count == 56;
            if (!safeV4Source && !bootstrapV5Source && !repeatableV5Source)
                throw new InvalidOperationException(
                    $"Refusing animated seating upgrade from unexpected catalog v{catalog.CalibrationVersion} " +
                    $"with {catalog.Profiles.Count} profiles.");

            string[] members = { "player", "older_sister", "father", "mother" };
            Vector2[][] pelvisAnchors =
            {
                Points((130,97),(137,88),(143,80),(145,77),(130,77),(130,77),(130,77),(130,77),(130,77),(130,77),(145,77),(143,80),(137,88),(130,97)),
                Points((130,87),(138,77),(145,69),(147,65),(130,77),(131,77),(131,77),(130,77),(130,77),(129,77),(147,65),(145,69),(138,77),(130,87)),
                Points((128,96),(138,86),(144,79),(145,76),(123,77),(122,77),(123,77),(123,77),(123,77),(123,77),(145,76),(144,79),(138,86),(128,96)),
                Points((131,89),(140,79),(146,70),(148,66),(126,77),(126,77),(126,77),(126,77),(126,77),(126,77),(148,66),(146,70),(140,79),(131,89))
            };
            Vector2[][] handAnchors =
            {
                Points((106,42),(89,52),(77,69),(76,68),(78,90),(67,80),(78,90),(68,91),(74,86),(78,91),(76,68),(77,69),(89,52),(106,42)),
                Points((109,50),(158,70),(170,58),(176,58),(75,108),(75,108),(75,108),(74,108),(74,108),(74,108),(176,58),(170,58),(158,70),(109,50)),
                Points((99,78),(86,72),(76,70),(75,70),(76,104),(76,104),(76,104),(76,104),(76,104),(76,104),(75,70),(76,70),(86,72),(99,78)),
                Points((99,59),(93,55),(80,69),(78,70),(88,120),(75,78),(86,91),(81,84),(84,90),(85,89),(78,70),(80,69),(93,55),(99,59))
            };
            OfficeSeatingAnimationClip[] clips =
            {
                OfficeSeatingAnimationClip.SitDown,
                OfficeSeatingAnimationClip.Work,
                OfficeSeatingAnimationClip.StandUp
            };
            int northwest = (int)OfficeSeatFacing8.Northwest;
            var profiles = new List<OfficeCharacterSeatPoseProfile>(56);
            for (var memberIndex = 0; memberIndex < members.Length; memberIndex++)
            {
                int sequenceIndex = 0;
                foreach (OfficeSeatingAnimationClip clip in clips)
                for (var frame = 0; frame < OfficeSeatingAnimationFrames.FrameCount(clip); frame++)
                {
                    string sourcePath = OfficeSeatingAnimationFrames.AssetPath(
                        members[memberIndex],
                        OfficeSeatFacing8.Northwest,
                        clip,
                        frame);
                    if (!File.Exists(sourcePath))
                        throw new FileNotFoundException("Approved seating source Sprite is missing.", sourcePath);
                    profiles.Add(OfficeCharacterSeatPoseProfile.Create(
                        members[memberIndex],
                        northwest,
                        clip,
                        frame,
                        pelvisAnchors[memberIndex][sequenceIndex],
                        handAnchors[memberIndex][sequenceIndex],
                        1f,
                        0f,
                        true,
                        Sha256(sourcePath)));
                    sequenceIndex++;
                }
            }
            catalog.ReplaceProfiles(profiles.ToArray(), OfficeCharacterSeatPoseCatalog.CurrentCalibrationVersion);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            catalog.ValidateAnimatedNorthwest(members, northwest);
            Debug.Log("OFFICE_NORTHWEST_SEATING_V5_APPROVAL_PASS | profiles=56 sources=56 scale=1 rotation=0");
        }

        private static Vector2[] Points(params (int x, int y)[] values)
        {
            return values.Select(value => new Vector2(value.x, value.y)).ToArray();
        }

        public static void Validate()
        {
            var seenKinds = new HashSet<string>(StringComparer.Ordinal);
            foreach (FurnitureSpec spec in Specs)
            {
                if (!seenKinds.Add(spec.KindId))
                    throw new InvalidOperationException("Duplicate furniture kind: " + spec.KindId);
                ValidateSourceSafetyMargin(spec);
                ValidateRuntimeTexture(spec.RuntimePath, spec, false);
                if (spec.SourceForegroundPolygon.Length > 0)
                    ValidateRuntimeTexture(spec.FrontPath, spec, true);
                if (Mathf.Abs(spec.SourceAspect - spec.RuntimeAspect) / spec.SourceAspect > 0.005f)
                    throw new InvalidOperationException($"Furniture aspect drift exceeds 0.5%: {spec.RuntimePath}.");
                if (spec.RuntimeGroundAnchorPx.x < 0f || spec.RuntimeGroundAnchorPx.y < 0f ||
                    spec.RuntimeGroundAnchorPx.x > CanvasWidth || spec.RuntimeGroundAnchorPx.y > CanvasHeight)
                    throw new InvalidOperationException("Furniture ground anchor is outside the runtime canvas: " + spec.RuntimePath);
                if (IsPerimeterKind(spec.KindId))
                {
                    Vector2 actualSpan = new Vector2(480f, 240f) * spec.BakedUniformScale;
                    Vector2 expectedSpan = new Vector2(
                        OfficeGridTilemapPresenter.TilePixelWidth * 0.5f,
                        OfficeGridTilemapPresenter.TilePixelHeight * 0.5f);
                    if (Vector2.Distance(actualSpan, expectedSpan) > 0.01f)
                        throw new InvalidOperationException(
                            $"Perimeter module does not span exactly one isometric tile: " +
                            $"{spec.KindId} actual={actualSpan} expected={expectedSpan}.");
                }
            }

            if (seenKinds.Count != 15) throw new InvalidOperationException("Furniture catalog must contain exactly 15 kinds.");
            LoadFurnitureVisualCatalog().Validate();
            OfficeCharacterSeatPoseCatalog poseCatalog = LoadCharacterSeatPoseCatalog();
            string[] members = { "player", "older_sister", "father", "mother" };
            poseCatalog.ValidateAnimatedNorthwest(members, (int)OfficeSeatFacing8.Northwest);
            foreach (OfficeCharacterSeatPoseProfile profile in poseCatalog.Profiles)
            {
                string sourcePath = OfficeSeatingAnimationFrames.AssetPath(
                    profile.MemberId,
                    (OfficeSeatFacing8)profile.DirectionIndex,
                    profile.Clip,
                    profile.FrameIndex);
                string actualSha = Sha256(sourcePath);
                if (!string.Equals(actualSha, profile.SourceSpriteSha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        $"Approved seating Sprite SHA mismatch: {profile.MemberId}/{profile.Clip}/{profile.FrameIndex}.");
            }
        }

        private static void ValidatePerimeterWalls(IReadOnlyList<FurnitureSpec> perimeterSpecs)
        {
            foreach (FurnitureSpec spec in perimeterSpecs)
            {
                ValidateSourceSafetyMargin(spec);
                ValidateRuntimeTexture(spec.RuntimePath, spec, false);
                if (Mathf.Abs(spec.SourceAspect - spec.RuntimeAspect) / spec.SourceAspect > 0.005f)
                    throw new InvalidOperationException($"Perimeter aspect drift exceeds 0.5%: {spec.RuntimePath}.");
                Vector2 actualSpan = new Vector2(480f, 240f) * spec.BakedUniformScale;
                Vector2 expectedSpan = new Vector2(
                    OfficeGridTilemapPresenter.TilePixelWidth * 0.5f,
                    OfficeGridTilemapPresenter.TilePixelHeight * 0.5f);
                if (Vector2.Distance(actualSpan, expectedSpan) > 0.01f)
                    throw new InvalidOperationException(
                        $"Perimeter module does not span exactly one isometric tile: " +
                        $"{spec.KindId} actual={actualSpan} expected={expectedSpan}.");
            }

            OfficeFurnitureVisualCatalog catalog = LoadFurnitureVisualCatalog();
            foreach (FurnitureSpec spec in perimeterSpecs)
                catalog.Resolve(spec.KindId, spec.Facing).Validate();
        }

        private static void BuildAllRuntimePngs()
        {
            foreach (FurnitureSpec spec in Specs) BuildOne(spec);
        }

        private static IEnumerable<string> RuntimePaths()
        {
            foreach (FurnitureSpec spec in Specs)
            {
                yield return spec.RuntimePath;
                if (spec.SourceForegroundPolygon.Length > 0) yield return spec.FrontPath;
            }
        }

        private static void BuildOne(
            FurnitureSpec spec,
            bool writeBase = true,
            bool writeFront = true)
        {
            Texture2D source = ReadTexture(spec.SourcePath);
            try
            {
                Color32[] sourcePixels = source.GetPixels32();
                RectInt bounds = VisibleBounds(sourcePixels, source.width, source.height, 16);
                ValidateSourceAnchor(spec.SourceGroundAnchorPx, bounds, spec, "ground");
                ValidateSourceAnchor(spec.SourceSortAnchorPx, bounds, spec, "sort");
                if (spec.SourceSeatAnchorPx.HasValue)
                    ValidateSourceAnchor(spec.SourceSeatAnchorPx.Value, bounds, spec, "seat");
                if (spec.SourceWorkSurfaceAnchorPx.HasValue)
                    ValidateSourceAnchor(spec.SourceWorkSurfaceAnchorPx.Value, bounds, spec, "work surface");
                if (spec.SourceOperatorSeatSocketPx.HasValue)
                    ValidateSourceCanvasAnchor(spec.SourceOperatorSeatSocketPx.Value, source, spec, "operator seat socket");

                float scale = IsPerimeterKind(spec.KindId)
                    ? 1f / 3f
                    : Mathf.Min(
                        spec.MaximumWidth / (float)bounds.width,
                        spec.MaximumHeight / (float)bounds.height);
                int scaledWidth = Mathf.Max(1, Mathf.RoundToInt(bounds.width * scale));
                int scaledHeight = Mathf.Max(1, Mathf.RoundToInt(bounds.height * scale));
                int destinationX = (CanvasWidth - scaledWidth) / 2;
                int destinationY = VisibleMarginPixels;
                if (destinationX < VisibleMarginPixels || destinationY < VisibleMarginPixels ||
                    destinationX + scaledWidth >= CanvasWidth - VisibleMarginPixels ||
                    destinationY + scaledHeight >= CanvasHeight - VisibleMarginPixels)
                    throw new InvalidOperationException($"Furniture does not fit runtime safety margin: {spec.RuntimePath}.");

                var output = new Color32[CanvasWidth * CanvasHeight];
                var front = new Color32[CanvasWidth * CanvasHeight];
                for (int y = 0; y < scaledHeight; y++)
                for (int x = 0; x < scaledWidth; x++)
                {
                    int sourceX = bounds.xMin + Mathf.Min(bounds.width - 1, Mathf.FloorToInt(x / scale));
                    int sourceY = bounds.yMin + Mathf.Min(bounds.height - 1, Mathf.FloorToInt(y / scale));
                    Color32 pixel = sourcePixels[sourceY * source.width + sourceX];
                    pixel = pixel.a < 128 ? new Color32(0, 0, 0, 0) : new Color32(pixel.r, pixel.g, pixel.b, 255);
                    int outputIndex = (destinationY + y) * CanvasWidth + destinationX + x;
                    output[outputIndex] = pixel;
                    Vector2 sourcePoint = new Vector2(sourceX + 0.5f, sourceY + 0.5f);
                    if (pixel.a > 0 && PointInPolygon(sourcePoint, spec.SourceForegroundPolygon) &&
                        !PointInPolygon(sourcePoint, spec.SourceForegroundExclusionPolygon))
                        front[outputIndex] = pixel;
                }

                spec.BakedUniformScale = scale;
                spec.SourceAspect = bounds.width / (float)bounds.height;
                spec.RuntimeAspect = scaledWidth / (float)scaledHeight;
                spec.RuntimeGroundAnchorPx = TransformAnchor(spec.SourceGroundAnchorPx, bounds, scale, destinationX, destinationY);
                spec.RuntimeSortAnchorPx = TransformAnchor(spec.SourceSortAnchorPx, bounds, scale, destinationX, destinationY);
                if (spec.SourceSeatAnchorPx.HasValue)
                    spec.RuntimeSeatAnchorPx = TransformAnchor(spec.SourceSeatAnchorPx.Value, bounds, scale, destinationX, destinationY);
                if (spec.SourceWorkSurfaceAnchorPx.HasValue)
                    spec.RuntimeWorkSurfaceAnchorPx = TransformAnchor(spec.SourceWorkSurfaceAnchorPx.Value, bounds, scale, destinationX, destinationY);
                if (spec.SourceOperatorSeatSocketPx.HasValue)
                    spec.RuntimeOperatorSeatSocketPx = TransformAnchor(spec.SourceOperatorSeatSocketPx.Value, bounds, scale, destinationX, destinationY);
                spec.RuntimeGroundFootprintPolygonPx = CanonicalFootprintPolygon(
                    spec.RuntimeGroundAnchorPx,
                    spec.SemanticFootprintWidth,
                    spec.SemanticFootprintHeight);

                if (writeBase) WritePng(spec.RuntimePath, output);
                if (writeFront && spec.SourceForegroundPolygon.Length > 0)
                    WritePng(spec.FrontPath, front);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        private static void CreateOrUpgradeFurnitureCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<OfficeFurnitureVisualCatalog>(FurnitureCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<OfficeFurnitureVisualCatalog>();
                AssetDatabase.CreateAsset(catalog, FurnitureCatalogPath);
            }

            OfficeFurnitureVisualDefinition[] definitions = Specs.Select(spec =>
                OfficeFurnitureVisualDefinition.Create(
                    spec.KindId,
                    spec.Facing,
                    RequiredSprite(spec.RuntimePath),
                    spec.SourceForegroundPolygon.Length == 0 ? null : RequiredSprite(spec.FrontPath),
                    spec.RuntimeGroundAnchorPx,
                    spec.RuntimeSortAnchorPx,
                    spec.RuntimeSeatAnchorPx,
                    spec.RuntimeWorkSurfaceAnchorPx,
                    1f,
                    spec.SourceSeatAnchorPx.HasValue,
                    spec.SourceWorkSurfaceAnchorPx.HasValue,
                    spec.SourceForegroundPolygon.Length > 0,
                    spec.RuntimeGroundFootprintPolygonPx,
                    spec.SemanticFootprintWidth,
                    spec.SemanticFootprintHeight,
                    spec.RuntimeOperatorSeatSocketPx,
                    spec.SourceOperatorSeatSocketPx.HasValue)).ToArray();
            catalog.ReplaceDefinitions(definitions, OfficeFurnitureVisualCatalog.CurrentCalibrationVersion);
            EditorUtility.SetDirty(catalog);
        }

        private static void UpdatePerimeterWallCatalog(IReadOnlyList<FurnitureSpec> perimeterSpecs)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<OfficeFurnitureVisualCatalog>(FurnitureCatalogPath);
            if (catalog == null)
                throw new FileNotFoundException("Office furniture visual catalog is missing.", FurnitureCatalogPath);

            OfficeFurnitureVisualDefinition[] definitions = catalog.Definitions.ToArray();
            OfficeFurnitureVisualDefinition[] preservedDefinitions = (OfficeFurnitureVisualDefinition[])definitions.Clone();
            OfficeFurnitureVisualDefinition chairDefinition = definitions.Single(definition =>
                definition != null &&
                string.Equals(definition.KindId, OfficeGridLayouts.SwivelChairKind, StringComparison.Ordinal));
            Sprite chairFrontOverlay = chairDefinition.FrontOverlaySprite;
            bool chairFrontOverlayWhenOccupied = chairDefinition.FrontOverlayWhenOccupied;

            foreach (FurnitureSpec spec in perimeterSpecs)
            {
                int[] matches = definitions
                    .Select((definition, index) => new { definition, index })
                    .Where(item => item.definition != null &&
                                   string.Equals(item.definition.KindId, spec.KindId, StringComparison.Ordinal) &&
                                   item.definition.Facing == spec.Facing)
                    .Select(item => item.index)
                    .ToArray();
                if (matches.Length != 1)
                    throw new InvalidOperationException(
                        $"Expected one existing perimeter catalog entry for {spec.KindId}/{spec.Facing}, found {matches.Length}.");
                definitions[matches[0]] = CreateVisualDefinition(spec);
            }

            catalog.ReplaceDefinitions(definitions, catalog.CalibrationVersion);
            for (int index = 0; index < definitions.Length; index++)
            {
                if (IsPerimeterKind(definitions[index].KindId)) continue;
                if (!ReferenceEquals(catalog.Definitions[index], preservedDefinitions[index]))
                    throw new InvalidOperationException(
                        $"Perimeter-only catalog update replaced unrelated definition at index {index}: {definitions[index].KindId}.");
            }
            if (!ReferenceEquals(chairDefinition, catalog.Definitions.Single(definition =>
                    definition != null &&
                    string.Equals(definition.KindId, OfficeGridLayouts.SwivelChairKind, StringComparison.Ordinal))) ||
                chairDefinition.FrontOverlaySprite != chairFrontOverlay ||
                chairDefinition.FrontOverlayWhenOccupied != chairFrontOverlayWhenOccupied)
            {
                throw new InvalidOperationException(
                    "Perimeter-only catalog update attempted to modify the swivel-chair overlay linkage or occupied-overlay flag.");
            }
            EditorUtility.SetDirty(catalog);
        }

        private static OfficeFurnitureVisualDefinition CreateVisualDefinition(FurnitureSpec spec)
        {
            return OfficeFurnitureVisualDefinition.Create(
                spec.KindId,
                spec.Facing,
                RequiredSprite(spec.RuntimePath),
                spec.SourceForegroundPolygon.Length == 0 ? null : RequiredSprite(spec.FrontPath),
                spec.RuntimeGroundAnchorPx,
                spec.RuntimeSortAnchorPx,
                spec.RuntimeSeatAnchorPx,
                spec.RuntimeWorkSurfaceAnchorPx,
                1f,
                spec.SourceSeatAnchorPx.HasValue,
                spec.SourceWorkSurfaceAnchorPx.HasValue,
                spec.SourceForegroundPolygon.Length > 0,
                spec.RuntimeGroundFootprintPolygonPx,
                spec.SemanticFootprintWidth,
                spec.SemanticFootprintHeight,
                spec.RuntimeOperatorSeatSocketPx,
                spec.SourceOperatorSeatSocketPx.HasValue);
        }

        private static void CreateOrUpgradePoseCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<OfficeCharacterSeatPoseCatalog>(PoseCatalogPath);
            bool created = catalog == null;
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<OfficeCharacterSeatPoseCatalog>();
                AssetDatabase.CreateAsset(catalog, PoseCatalogPath);
            }

            if (!created && catalog.Profiles.Count > 0)
            {
                if (catalog.CalibrationVersion != OfficeCharacterSeatPoseCatalog.CurrentCalibrationVersion)
                    throw new InvalidOperationException(
                        $"Character seat pose catalog v{catalog.CalibrationVersion} requires manual migration to v{OfficeCharacterSeatPoseCatalog.CurrentCalibrationVersion}; the builder will not auto-approve or overwrite it.");
                catalog.Validate();
                return;
            }

            int northWest = (int)OfficeSeatFacing8.Northwest;
            var profiles = new List<OfficeCharacterSeatPoseProfile>(4)
            {
                CreateUnapprovedSafeStaticProfile("player", northWest, new Vector2(145f, 65f), new Vector2(75f, 90f)),
                CreateUnapprovedSafeStaticProfile("older_sister", northWest, new Vector2(145f, 85f), new Vector2(75f, 110f)),
                CreateUnapprovedSafeStaticProfile("father", northWest, new Vector2(155f, 80f), new Vector2(85f, 105f)),
                CreateUnapprovedSafeStaticProfile("mother", northWest, new Vector2(140f, 55f), new Vector2(70f, 80f))
            };
            catalog.ReplaceProfiles(profiles.ToArray(), OfficeCharacterSeatPoseCatalog.CurrentCalibrationVersion);
            EditorUtility.SetDirty(catalog);
        }

        private static OfficeCharacterSeatPoseProfile CreateUnapprovedSafeStaticProfile(
            string memberId,
            int direction,
            Vector2 pelvisAnchorPx,
            Vector2 handAnchorPx)
        {
            return OfficeCharacterSeatPoseProfile.Create(
                memberId,
                direction,
                OfficeSeatingAnimationClip.Work,
                0,
                pelvisAnchorPx,
                handAnchorPx,
                1f,
                0f,
                false,
                string.Empty);
        }

        private static Sprite RequiredSprite(string path)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new FileNotFoundException("Office furniture sprite is missing.", path);
            return sprite;
        }

        private static void ValidateSourceSafetyMargin(FurnitureSpec spec)
        {
            Texture2D source = ReadTexture(spec.SourcePath);
            try
            {
                Color32[] pixels = source.GetPixels32();
                RectInt bounds = VisibleBounds(pixels, source.width, source.height, 16);
                if (bounds.xMin < 24 || bounds.yMin < 24 ||
                    source.width - bounds.xMax < 24 || source.height - bounds.yMax < 24)
                    throw new InvalidOperationException($"Furniture source touches its safety margin: {spec.SourcePath} bounds={bounds}.");
                if (IsPerimeterKind(spec.KindId))
                {
                    ValidateVisibleEndpoint(pixels, source.width, source.height, spec.SourceGroundAnchorPx, spec);
                    ValidateVisibleEndpoint(
                        pixels,
                        source.width,
                        source.height,
                        spec.SourceGroundAnchorPx + new Vector2(480f, 240f),
                        spec);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        private static void ValidateRuntimeTexture(string path, FurnitureSpec spec, bool overlay)
        {
            Texture2D runtime = ReadTexture(path);
            try
            {
                if (runtime.width != CanvasWidth || runtime.height != CanvasHeight)
                    throw new InvalidOperationException("Furniture runtime canvas is invalid: " + path);
                Color32[] pixels = runtime.GetPixels32();
                RectInt bounds = VisibleBounds(pixels, runtime.width, runtime.height, 0);
                if (bounds.xMin < VisibleMarginPixels || bounds.yMin < VisibleMarginPixels ||
                    bounds.xMax >= CanvasWidth - VisibleMarginPixels || bounds.yMax >= CanvasHeight - VisibleMarginPixels)
                    throw new InvalidOperationException($"Furniture runtime safety margin is invalid: {path} bounds={bounds}.");
                for (int index = 0; index < pixels.Length; index++)
                {
                    Color32 pixel = pixels[index];
                    if (pixel.a != 0 && pixel.a != 255)
                        throw new InvalidOperationException($"Furniture alpha is not hard at {index}: {path}.");
                    if (pixel.a > 0 && pixel.r > 180 && pixel.b > 150 && pixel.g < 90)
                        throw new InvalidOperationException($"Furniture contains magenta fringe at {index}: {path}.");
                }
                if (!overlay && string.Equals(
                        spec.KindId,
                        OfficeGridLayouts.EntranceDoorKind,
                        StringComparison.Ordinal))
                {
                    ValidateOpenEntranceCenter(pixels, spec, path);
                    ValidateExteriorThresholdOnly(pixels, spec, path);
                }

                Sprite sprite = RequiredSprite(path);
                if (Math.Abs(sprite.pixelsPerUnit - PixelsPerUnit) > 0.01f)
                    throw new InvalidOperationException("Furniture sprite PPU is invalid: " + path);
                Vector2 expectedPivot = spec.RuntimeGroundAnchorPx;
                if (Vector2.Distance(sprite.pivot, expectedPivot) > 0.01f)
                    throw new InvalidOperationException($"Furniture sprite ground pivot is invalid: {path}, {sprite.pivot} != {expectedPivot}.");
                if (overlay && spec.SourceForegroundPolygon.Length == 0)
                    throw new InvalidOperationException("Unexpected furniture foreground overlay: " + path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(runtime);
            }
        }

        private static Vector2 TransformAnchor(Vector2 sourceAnchor, RectInt bounds, float scale, int destinationX, int destinationY)
        {
            return new Vector2(
                destinationX + (sourceAnchor.x - bounds.xMin) * scale,
                destinationY + (sourceAnchor.y - bounds.yMin) * scale);
        }

        private static bool IsPerimeterKind(string kindId)
        {
            return string.Equals(kindId, OfficeGridLayouts.EntranceDoorKind, StringComparison.Ordinal) ||
                   string.Equals(kindId, OfficeGridLayouts.EntranceWallKind, StringComparison.Ordinal) ||
                   string.Equals(kindId, OfficeGridLayouts.PerimeterCutawayWallKind, StringComparison.Ordinal);
        }

        private static void ValidateOpenEntranceCenter(
            IReadOnlyList<Color32> pixels,
            FurnitureSpec spec,
            string path)
        {
            Vector2 passageCenter = spec.RuntimeGroundAnchorPx + new Vector2(80f, 40f);
            int minimumX = Mathf.RoundToInt(passageCenter.x - 36f);
            int maximumX = Mathf.RoundToInt(passageCenter.x + 36f);
            // A thin sill may cross the floor plane. The clear-body probe begins above it so
            // the validator rejects a door leaf or center wall without rejecting the threshold.
            int minimumY = Mathf.RoundToInt(passageCenter.y + 52f);
            int maximumY = Mathf.RoundToInt(passageCenter.y + 145f);
            for (int y = minimumY; y <= maximumY; y++)
            for (int x = minimumX; x <= maximumX; x++)
            {
                if (x < 0 || x >= CanvasWidth || y < 0 || y >= CanvasHeight) continue;
                if (pixels[y * CanvasWidth + x].a == 0) continue;
                throw new InvalidOperationException(
                    $"Entrance art must keep its central tile passage fully open: {path} pixel=({x},{y}).");
            }
        }

        private static void ValidateExteriorThresholdOnly(
            IReadOnlyList<Color32> pixels,
            FurnitureSpec spec,
            string path)
        {
            var opaqueCount = 0;
            var interiorOrVerticalCount = 0;
            var maximumExteriorDepth = 0f;
            for (var y = 0; y < CanvasHeight; y++)
            for (var x = 0; x < CanvasWidth; x++)
            {
                if (pixels[y * CanvasWidth + x].a == 0) continue;
                opaqueCount++;
                float sampleX = x + 0.5f;
                float sampleY = y + 0.5f;
                float innerEdgeY = spec.RuntimeGroundAnchorPx.y +
                                   0.5f * (sampleX - spec.RuntimeGroundAnchorPx.x);
                if (sampleY > innerEdgeY + 0.5f) interiorOrVerticalCount++;
                maximumExteriorDepth = Mathf.Max(maximumExteriorDepth, innerEdgeY - sampleY);
            }

            if (opaqueCount == 0 || interiorOrVerticalCount != 0 || maximumExteriorDepth > 8.5f)
                throw new InvalidOperationException(
                    $"Entrance must be a thin exterior-only threshold with no leaf, jamb or lintel: " +
                    $"{path} opaque={opaqueCount} interiorOrVertical={interiorOrVerticalCount} " +
                    $"maxExteriorDepth={maximumExteriorDepth:F3}px.");
        }

        private static void ValidateVisibleEndpoint(
            IReadOnlyList<Color32> pixels,
            int width,
            int height,
            Vector2 endpoint,
            FurnitureSpec spec)
        {
            int centerX = Mathf.RoundToInt(endpoint.x);
            int centerY = Mathf.RoundToInt(endpoint.y);
            for (int offsetY = -2; offsetY <= 2; offsetY++)
            for (int offsetX = -2; offsetX <= 2; offsetX++)
            {
                if (offsetX * offsetX + offsetY * offsetY > 4) continue;
                int x = centerX + offsetX;
                int y = centerY + offsetY;
                if (x < 0 || x >= width || y < 0 || y >= height) continue;
                if (pixels[y * width + x].a > 16) return;
            }
            throw new InvalidOperationException(
                $"Perimeter source has no visible connection pixel within 2px of endpoint {endpoint}: {spec.SourcePath}.");
        }

        private static void ValidateSourceAnchor(Vector2 anchor, RectInt bounds, FurnitureSpec spec, string name)
        {
            if (anchor.x < bounds.xMin || anchor.x > bounds.xMax || anchor.y < bounds.yMin || anchor.y > bounds.yMax)
                throw new InvalidOperationException($"Furniture {name} anchor {anchor} is outside source visible bounds {bounds}: {spec.SourcePath}.");
        }

        private static void ValidateSourceCanvasAnchor(
            Vector2 anchor,
            Texture2D source,
            FurnitureSpec spec,
            string name)
        {
            if (anchor.x < 0f || anchor.y < 0f || anchor.x > source.width || anchor.y > source.height)
                throw new InvalidOperationException($"Furniture {name} {anchor} is outside source canvas {source.width}x{source.height}: {spec.SourcePath}.");
        }

        private static Vector2[] CanonicalFootprintPolygon(Vector2 center, int width, int height)
        {
            Vector2 basisX = new Vector2(OfficeGridTilemapPresenter.TilePixelWidth * 0.5f,
                OfficeGridTilemapPresenter.TilePixelHeight * 0.5f);
            Vector2 basisY = new Vector2(-OfficeGridTilemapPresenter.TilePixelWidth * 0.5f,
                OfficeGridTilemapPresenter.TilePixelHeight * 0.5f);
            Vector2 extentX = basisX * (width * 0.5f);
            Vector2 extentY = basisY * (height * 0.5f);
            return new[]
            {
                center - extentX - extentY,
                center + extentX - extentY,
                center + extentX + extentY,
                center - extentX + extentY
            };
        }

        private static bool PointInPolygon(Vector2 point, IReadOnlyList<Vector2> polygon)
        {
            if (polygon == null || polygon.Count < 3) return false;
            bool inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[j];
                bool intersects = ((a.y > point.y) != (b.y > point.y)) &&
                                  point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x;
                if (intersects) inside = !inside;
            }
            return inside;
        }

        private static void WritePng(string path, Color32[] pixels)
        {
            var texture = new Texture2D(CanvasWidth, CanvasHeight, TextureFormat.RGBA32, false, false);
            try
            {
                texture.SetPixels32(pixels);
                texture.Apply(false, false);
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static Texture2D ReadTexture(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("Office furniture source is missing.", path);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            if (!texture.LoadImage(File.ReadAllBytes(path), false))
            {
                UnityEngine.Object.DestroyImmediate(texture);
                throw new InvalidDataException("Could not decode office furniture texture: " + path);
            }
            return texture;
        }

        private static RectInt VisibleBounds(Color32[] pixels, int width, int height, byte alphaThreshold)
        {
            int minimumX = width;
            int minimumY = height;
            int maximumX = -1;
            int maximumY = -1;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                if (pixels[y * width + x].a <= alphaThreshold) continue;
                minimumX = Math.Min(minimumX, x);
                minimumY = Math.Min(minimumY, y);
                maximumX = Math.Max(maximumX, x);
                maximumY = Math.Max(maximumY, y);
            }
            if (maximumX < minimumX || maximumY < minimumY)
                throw new InvalidDataException("Furniture image contains no visible pixels.");
            return new RectInt(minimumX, minimumY, maximumX - minimumX + 1, maximumY - minimumY + 1);
        }

        private static void ConfigureImporter(string path, Vector2 pivotPx)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Furniture texture importer is missing: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(pivotPx.x / CanvasWidth, pivotPx.y / CanvasHeight);
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        private static string Sha256(string path)
        {
            using var algorithm = SHA256.Create();
            using var stream = File.OpenRead(path);
            return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", string.Empty);
        }
    }
}
