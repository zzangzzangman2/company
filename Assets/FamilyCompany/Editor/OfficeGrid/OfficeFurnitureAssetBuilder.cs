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
                sourceWorkSurfaceAnchorPx: new Vector2(663f, 266f),
                sourceOperatorSeatSocketPx: new Vector2(919.115f, 174.598f),
                semanticFootprintWidth: 2,
                sourceForegroundPolygon: new[]
                {
                    new Vector2(320f, 100f), new Vector2(1225f, 100f),
                    new Vector2(1225f, 520f), new Vector2(710f, 365f),
                    new Vector2(320f, 550f)
                }),
            new FurnitureSpec(
                OfficeGridLayouts.SwivelChairKind,
                "office_swivel_chair",
                175,
                260,
                OfficeFurnitureFacing.NorthWest,
                new Vector2(620f, 300f),
                new Vector2(620f, 235f),
                "v3",
                "office_swivel_chair_northwest",
                new Vector2(600f, 650f)),
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
                OfficeFurnitureFacing.SouthEast, new Vector2(884f, 100f), new Vector2(884f, 82f))
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
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("FAMILY_COMPANY_OFFICE_FURNITURE_TYCOON_ALIGNMENT_V2_BUILD: PASS");
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
            CreateOrUpgradePoseCatalog();
            AssetDatabase.SaveAssets();
            LoadCharacterSeatPoseCatalog().Validate();
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
            }

            if (seenKinds.Count != 12) throw new InvalidOperationException("Furniture catalog must contain exactly 12 kinds.");
            LoadFurnitureVisualCatalog().Validate();
            LoadCharacterSeatPoseCatalog().Validate();
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

        private static void BuildOne(FurnitureSpec spec)
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

                float scale = Mathf.Min(
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
                    if (pixel.a > 0 && PointInPolygon(new Vector2(sourceX + 0.5f, sourceY + 0.5f), spec.SourceForegroundPolygon))
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

                WritePng(spec.RuntimePath, output);
                if (spec.SourceForegroundPolygon.Length > 0) WritePng(spec.FrontPath, front);
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

            if (catalog.CalibrationVersion == OfficeFurnitureVisualCatalog.CurrentCalibrationVersion &&
                catalog.Definitions.Count == Specs.Length)
                return;

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

        private static void CreateOrUpgradePoseCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<OfficeCharacterSeatPoseCatalog>(PoseCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<OfficeCharacterSeatPoseCatalog>();
                AssetDatabase.CreateAsset(catalog, PoseCatalogPath);
            }

            if (catalog.CalibrationVersion == OfficeCharacterSeatPoseCatalog.CurrentCalibrationVersion &&
                catalog.Profiles.Count == 56)
                return;

            int northWest = (int)OfficeSeatFacing8.Northwest;
            var profiles = new List<OfficeCharacterSeatPoseProfile>(56);
            AddPoseProfiles(
                profiles, "player", northWest,
                new Vector2(151f, 65f), new Vector2(91f, 86f),
                1.174293f, -0.350355f);
            AddPoseProfiles(
                profiles, "older_sister", northWest,
                new Vector2(142f, 96f), new Vector2(86f, 113f),
                1.27552985f, -2.75361f);
            AddPoseProfiles(
                profiles, "father", northWest,
                new Vector2(157f, 86f), new Vector2(103f, 116f),
                1.20841674f, 9.414203f);
            AddPoseProfiles(
                profiles, "mother", northWest,
                new Vector2(150f, 89f), new Vector2(83f, 96f),
                1.10812479f, -13.675914f);
            catalog.ReplaceProfiles(profiles.ToArray(), OfficeCharacterSeatPoseCatalog.CurrentCalibrationVersion);
            EditorUtility.SetDirty(catalog);
        }

        private static void AddPoseProfiles(
            ICollection<OfficeCharacterSeatPoseProfile> profiles,
            string memberId,
            int direction,
            Vector2 pelvisAnchorPx,
            Vector2 handAnchorPx,
            float uniformScale,
            float rotationDegrees)
        {
            var clips = new[]
            {
                OfficeSeatingAnimationClip.SitDown,
                OfficeSeatingAnimationClip.Work,
                OfficeSeatingAnimationClip.StandUp
            };
            foreach (OfficeSeatingAnimationClip clip in clips)
            for (int frame = 0; frame < OfficeSeatingAnimationFrames.FrameCount(clip); frame++)
            {
                profiles.Add(OfficeCharacterSeatPoseProfile.Create(
                    memberId,
                    direction,
                    clip,
                    frame,
                    pelvisAnchorPx,
                    handAnchorPx,
                    uniformScale,
                    rotationDegrees));
            }
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
                RectInt bounds = VisibleBounds(source.GetPixels32(), source.width, source.height, 16);
                if (bounds.xMin < 24 || bounds.yMin < 24 ||
                    source.width - bounds.xMax < 24 || source.height - bounds.yMax < 24)
                    throw new InvalidOperationException($"Furniture source touches its safety margin: {spec.SourcePath} bounds={bounds}.");
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
