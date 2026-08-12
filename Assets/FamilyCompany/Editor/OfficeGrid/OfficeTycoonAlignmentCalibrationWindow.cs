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
    public sealed class OfficeTycoonAlignmentCalibrationWindow : EditorWindow
    {
        private enum Mode
        {
            Furniture,
            Character,
            Workstation
        }

        private enum FurnitureHandle
        {
            None,
            Ground,
            Sort,
            Seat,
            OperatorSeat,
            OperatorWork,
            Footprint
        }

        private static readonly string[] MemberIds = { "player", "older_sister", "father", "mother" };
        private static readonly float[] ZoomValues = { 1f, 2f, 4f };
        private static readonly string[] ZoomLabels = { "100%", "200%", "400%" };

        private Mode _mode;
        private OfficeFurnitureVisualCatalog _furnitureCatalog;
        private OfficeCharacterSeatPoseCatalog _poseCatalog;
        private int _furnitureIndex;
        private int _memberIndex;
        private OfficeSeatFacing8 _facing = OfficeSeatFacing8.Northwest;
        private OfficeSeatingAnimationClip _clip = OfficeSeatingAnimationClip.Work;
        private int _frameIndex;
        private int _zoomIndex;
        private Vector2 _scroll;
        private Vector2[] _footprint = Array.Empty<Vector2>();
        private Vector2 _ground;
        private Vector2 _sort;
        private Vector2 _seat;
        private Vector2 _operatorSeat;
        private Vector2 _operatorWork;
        private float _furnitureScale = 1f;
        private Vector2 _pelvis;
        private Vector2 _hand;
        private string _loadedFurnitureKey = string.Empty;
        private string _loadedPoseKey = string.Empty;
        private FurnitureHandle _dragHandle;
        private int _dragFootprintIndex = -1;
        private bool _compositeApproved;

        [MenuItem("Family Company/Office/Office Tycoon Alignment Calibration")]
        public static void Open()
        {
            var window = GetWindow<OfficeTycoonAlignmentCalibrationWindow>();
            window.titleContent = new GUIContent("Office Alignment V3");
            window.minSize = new Vector2(900f, 650f);
            window.Show();
        }

        private void OnEnable()
        {
            ReloadCatalogs();
        }

        private void OnGUI()
        {
            DrawHeader();
            if (_furnitureCatalog == null || _poseCatalog == null)
            {
                EditorGUILayout.HelpBox(
                    "Calibration assets are missing. Run Family Company/Art/Build Office Furniture Tycoon Alignment V2 once.",
                    MessageType.Warning);
                if (GUILayout.Button("Build and reload"))
                {
                    OfficeFurnitureAssetBuilder.Build();
                    ReloadCatalogs();
                }
                return;
            }

            _mode = (Mode)GUILayout.Toolbar((int)_mode, new[] { "Furniture", "Character", "Workstation composite" });
            EditorGUILayout.Space(4f);
            switch (_mode)
            {
                case Mode.Furniture:
                    DrawFurnitureMode();
                    break;
                case Mode.Character:
                    DrawCharacterMode();
                    break;
                case Mode.Workstation:
                    DrawWorkstationMode();
                    break;
            }
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Office Tycoon Alignment V3 — translation-only seated calibration", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Reload", EditorStyles.toolbarButton)) ReloadCatalogs();
            }
        }

        private void DrawFurnitureMode()
        {
            string[] labels = _furnitureCatalog.Definitions
                .Select(item => $"{item.KindId} / {item.Facing}")
                .ToArray();
            _furnitureIndex = Mathf.Clamp(_furnitureIndex, 0, Math.Max(0, labels.Length - 1));
            _furnitureIndex = EditorGUILayout.Popup("Furniture", _furnitureIndex, labels);
            OfficeFurnitureVisualDefinition definition = _furnitureCatalog.Definitions[_furnitureIndex];
            LoadFurnitureIfNeeded(definition);
            _zoomIndex = GUILayout.Toolbar(_zoomIndex, ZoomLabels, GUILayout.Width(260f));

            EditorGUI.BeginChangeCheck();
            _ground = EditorGUILayout.Vector2Field("Ground anchor (runtime px)", _ground);
            _sort = EditorGUILayout.Vector2Field("Sort anchor (runtime px)", _sort);
            if (definition.HasSeatAnchor) _seat = EditorGUILayout.Vector2Field("Chair seat anchor (runtime px)", _seat);
            if (definition.HasOperatorSeatSocket)
                _operatorSeat = EditorGUILayout.Vector2Field("Desk operator seat socket", _operatorSeat);
            if (definition.HasOperatorWorkSocket)
                _operatorWork = EditorGUILayout.Vector2Field("Desk operator work socket", _operatorWork);
            _furnitureScale = EditorGUILayout.FloatField("Uniform scale", _furnitureScale);
            if (EditorGUI.EndChangeCheck()) _compositeApproved = false;

            float zoom = ZoomValues[_zoomIndex];
            Sprite sprite = definition.BaseSprite;
            Vector2 contentSize = sprite.rect.size * zoom + new Vector2(40f, 40f);
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
            Rect canvas = GUILayoutUtility.GetRect(contentSize.x, contentSize.y);
            Rect spriteRect = new Rect(canvas.x + 20f, canvas.y + 20f, sprite.rect.width * zoom, sprite.rect.height * zoom);
            EditorGUI.DrawRect(canvas, new Color(0.12f, 0.14f, 0.16f));
            DrawSprite(sprite, spriteRect, Color.white);
            if (definition.FrontOverlaySprite != null)
                DrawSprite(definition.FrontOverlaySprite, spriteRect, new Color(1f, 1f, 1f, 0.28f));
            DrawFurnitureGuides(spriteRect, sprite.rect.size, definition);
            HandleFurnitureDrag(spriteRect, sprite.rect.size, definition);
            EditorGUILayout.EndScrollView();

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Footprint points: " + string.Join("  ", _footprint.Select((p, i) => $"P{i} {p.x:F1},{p.y:F1}")));
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(!_compositeApproved))
                {
                    if (GUILayout.Button("Save approved furniture calibration", GUILayout.Width(245f)))
                        SaveFurniture(definition);
                }
            }
            EditorGUILayout.HelpBox(
                "Edits intentionally cannot be saved until the Workstation composite tab passes and is approved.",
                _compositeApproved ? MessageType.Info : MessageType.Warning);
        }

        private void DrawCharacterMode()
        {
            _memberIndex = EditorGUILayout.Popup("Member", _memberIndex, MemberIds);
            _facing = (OfficeSeatFacing8)EditorGUILayout.EnumPopup("Facing", _facing);
            _clip = (OfficeSeatingAnimationClip)EditorGUILayout.EnumPopup("Clip", _clip);
            _frameIndex = EditorGUILayout.IntSlider("Frame", _frameIndex, 0, OfficeSeatingAnimationFrames.FrameCount(_clip) - 1);
            OfficeCharacterSeatPoseProfile profile = TryResolvePose();
            if (profile == null)
            {
                EditorGUILayout.HelpBox("No authored profile exists for this facing/clip/frame. No runtime fallback is permitted.", MessageType.Warning);
                return;
            }
            LoadPoseIfNeeded(profile);

            EditorGUI.BeginChangeCheck();
            _pelvis = EditorGUILayout.Vector2Field("Pelvis (frame px)", _pelvis);
            _hand = EditorGUILayout.Vector2Field("Hand / interaction (frame px)", _hand);
            EditorGUILayout.LabelField("Uniform scale", "1.000 (locked)");
            EditorGUILayout.LabelField("Whole-Sprite rotation", "0.000° (locked)");
            if (EditorGUI.EndChangeCheck()) _compositeApproved = false;

            Sprite current = LoadPoseSprite(MemberIds[_memberIndex], _facing, _clip, _frameIndex);
            Sprite previous = _frameIndex > 0 ? LoadPoseSprite(MemberIds[_memberIndex], _facing, _clip, _frameIndex - 1) : null;
            Sprite next = _frameIndex + 1 < OfficeSeatingAnimationFrames.FrameCount(_clip)
                ? LoadPoseSprite(MemberIds[_memberIndex], _facing, _clip, _frameIndex + 1)
                : null;
            _zoomIndex = GUILayout.Toolbar(_zoomIndex, ZoomLabels, GUILayout.Width(260f));
            float zoom = ZoomValues[_zoomIndex];
            Rect canvas = GUILayoutUtility.GetRect(current.rect.width * zoom + 40f, current.rect.height * zoom + 40f);
            EditorGUI.DrawRect(canvas, new Color(0.12f, 0.14f, 0.16f));
            Rect spriteRect = new Rect(canvas.x + 20f, canvas.y + 20f, current.rect.width * zoom, current.rect.height * zoom);
            if (previous != null) DrawSprite(previous, spriteRect, new Color(0.2f, 0.65f, 1f, 0.16f));
            if (next != null) DrawSprite(next, spriteRect, new Color(1f, 0.5f, 0.2f, 0.16f));
            DrawSprite(current, spriteRect, Color.white);
            DrawPoint(PixelToGui(_pelvis, spriteRect, current.rect.size), Color.magenta, "pelvis");
            DrawPoint(PixelToGui(_hand, spriteRect, current.rect.size), Color.cyan, "hand");
            HandlePoseDrag(spriteRect, current.rect.size);

            OfficeCharacterSeatPoseProfile previousProfile = _frameIndex > 0
                ? TryResolvePose(_frameIndex - 1)
                : null;
            float renderedPoseScale = OfficeGridCharacterMover.UniformVisualScale;
            float pelvisDrift = previousProfile == null ? 0f : Vector2.Distance(previousProfile.PelvisAnchorPx, _pelvis) * renderedPoseScale;
            float handDrift = previousProfile == null ? 0f : Vector2.Distance(previousProfile.HandAnchorPx, _hand) * renderedPoseScale;
            EditorGUILayout.LabelField($"Previous frame drift — pelvis {pelvisDrift:F2}px / hand {handDrift:F2}px");
            using (new EditorGUI.DisabledScope(!_compositeApproved))
            {
                if (GUILayout.Button("Save approved character frame calibration", GUILayout.Width(270f))) SavePose(profile);
            }
        }

        private void DrawWorkstationMode()
        {
            _memberIndex = EditorGUILayout.Popup("Member", _memberIndex, MemberIds);
            _facing = OfficeSeatFacing8.Northwest;
            _clip = OfficeSeatingAnimationClip.Work;
            _frameIndex = EditorGUILayout.IntSlider("Work frame", _frameIndex, 0, OfficeSeatingAnimationFrames.WorkFrameCount - 1);
            OfficeFurnitureVisualDefinition desk = _furnitureCatalog.Resolve(OfficeGridLayouts.DeskWithPcKind, OfficeFurnitureFacing.SouthEast);
            OfficeFurnitureVisualDefinition chair = _furnitureCatalog.Resolve(OfficeGridLayouts.SwivelChairKind, OfficeFurnitureFacing.NorthWest);
            OfficeCharacterSeatPoseProfile pose = TryResolvePose();
            if (pose == null) return;
            LoadPoseIfNeeded(pose);

            Rect canvas = GUILayoutUtility.GetRect(820f, 470f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(canvas, new Color(0.42f, 0.72f, 0.74f));
            Vector2 targetSeat = new Vector2(canvas.center.x + 35f, canvas.center.y + 65f);
            DrawFloorDiamond(targetSeat + new Vector2(0f, 115f));
            DrawAlignedSprite(desk.BaseSprite, desk.OperatorSeatSocketPx, targetSeat, desk.UniformScale, Color.white);
            DrawAlignedSprite(chair.BaseSprite, chair.SeatAnchorPx, targetSeat, chair.UniformScale, Color.white);
            Sprite character = LoadPoseSprite(MemberIds[_memberIndex], _facing, _clip, _frameIndex);
            float renderedPoseScale = OfficeGridCharacterMover.UniformVisualScale;
            DrawAlignedSprite(
                character,
                _pelvis,
                targetSeat,
                renderedPoseScale,
                Color.white);
            if (desk.FrontOverlaySprite != null)
                DrawAlignedSprite(desk.FrontOverlaySprite, desk.OperatorSeatSocketPx, targetSeat, desk.UniformScale, Color.white);
            if (chair.FrontOverlaySprite != null)
                DrawAlignedSprite(chair.FrontOverlaySprite, chair.SeatAnchorPx, targetSeat, chair.UniformScale, Color.white);

            float chairSeatError = 0f;
            float pelvisSeatError = 0f;
            Vector2 characterHandFromSeat = (_hand - _pelvis) * renderedPoseScale;
            Vector2 deskWorkFromSeat = (desk.OperatorWorkSocketPx - desk.OperatorSeatSocketPx) * desk.UniformScale;
            float handWorkError = Vector2.Distance(characterHandFromSeat, deskWorkFromSeat);
            float vectorAngleError = OfficeGridAlignmentMetrics.VectorAngleDifferenceDegrees(
                characterHandFromSeat,
                deskWorkFromSeat);
            float vectorLengthError = OfficeGridAlignmentMetrics.VectorLengthRelativeError(
                characterHandFromSeat,
                deskWorkFromSeat);
            float footprintError = FootprintResidual(desk);
            bool pass = chairSeatError <= 2f && pelvisSeatError <= 2f && handWorkError <= 4f &&
                         vectorAngleError <= 2f && vectorLengthError <= 0.04f && footprintError <= 2f;
            EditorGUILayout.LabelField(
                $"pelvis↔seat {pelvisSeatError:F2}px    chair↔desk seat {chairSeatError:F2}px    hand↔work {handWorkError:F2}px    footprint max {footprintError:F2}px",
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                $"hand vector direction {vectorAngleError:F2}° / length {vectorLengthError * 100f:F2}% (limits 2° / 4%)",
                EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                pass
                    ? "Translation-only values pass. Inspect the full body, chair, desk edge, head, legs, and actual keyboard contact before human approval."
                    : "Composite is outside V3 tolerances. Re-click real pelvis/hand or fix the Sprite/work socket; scale and rotation cannot be used.",
                pass ? MessageType.Info : MessageType.Error);
            using (new EditorGUI.DisabledScope(!pass))
            {
                if (GUILayout.Button("I inspected the composite — approve current calibration", GUILayout.Height(32f)))
                    _compositeApproved = true;
            }
        }

        private void DrawFurnitureGuides(Rect spriteRect, Vector2 spriteSize, OfficeFurnitureVisualDefinition definition)
        {
            var footprintGui = _footprint.Select(point => PixelToGui(point, spriteRect, spriteSize)).ToArray();
            DrawClosedLine(footprintGui, Color.white, 2f);
            DrawPoint(PixelToGui(_ground, spriteRect, spriteSize), Color.red, "ground");
            DrawPoint(PixelToGui(_sort, spriteRect, spriteSize), Color.yellow, "sort");
            for (int index = 0; index < footprintGui.Length; index++) DrawPoint(footprintGui[index], Color.white, "P" + index);
            if (definition.HasSeatAnchor) DrawPoint(PixelToGui(_seat, spriteRect, spriteSize), Color.green, "seat");
            if (definition.HasOperatorSeatSocket) DrawPoint(PixelToGui(_operatorSeat, spriteRect, spriteSize), new Color(1f, 0.5f, 0f), "operator seat");
            if (definition.HasOperatorWorkSocket) DrawPoint(PixelToGui(_operatorWork, spriteRect, spriteSize), Color.cyan, "operator work");
        }

        private void HandleFurnitureDrag(Rect rect, Vector2 size, OfficeFurnitureVisualDefinition definition)
        {
            Event current = Event.current;
            if (current.type == EventType.MouseDown && current.button == 0)
            {
                _dragHandle = HitFurnitureHandle(current.mousePosition, rect, size, definition, out _dragFootprintIndex);
                if (_dragHandle != FurnitureHandle.None) current.Use();
            }
            else if (current.type == EventType.MouseDrag && _dragHandle != FurnitureHandle.None)
            {
                Vector2 pixel = GuiToPixel(current.mousePosition, rect, size);
                switch (_dragHandle)
                {
                    case FurnitureHandle.Ground: _ground = pixel; break;
                    case FurnitureHandle.Sort: _sort = pixel; break;
                    case FurnitureHandle.Seat: _seat = pixel; break;
                    case FurnitureHandle.OperatorSeat: _operatorSeat = pixel; break;
                    case FurnitureHandle.OperatorWork: _operatorWork = pixel; break;
                    case FurnitureHandle.Footprint: _footprint[_dragFootprintIndex] = pixel; break;
                }
                _compositeApproved = false;
                current.Use();
                Repaint();
            }
            else if (current.type == EventType.MouseUp)
            {
                _dragHandle = FurnitureHandle.None;
                _dragFootprintIndex = -1;
            }
        }

        private FurnitureHandle HitFurnitureHandle(
            Vector2 mouse,
            Rect rect,
            Vector2 size,
            OfficeFurnitureVisualDefinition definition,
            out int footprintIndex)
        {
            footprintIndex = -1;
            for (int index = 0; index < _footprint.Length; index++)
            {
                if (Vector2.Distance(mouse, PixelToGui(_footprint[index], rect, size)) <= 9f)
                {
                    footprintIndex = index;
                    return FurnitureHandle.Footprint;
                }
            }
            if (Vector2.Distance(mouse, PixelToGui(_ground, rect, size)) <= 9f) return FurnitureHandle.Ground;
            if (Vector2.Distance(mouse, PixelToGui(_sort, rect, size)) <= 9f) return FurnitureHandle.Sort;
            if (definition.HasSeatAnchor && Vector2.Distance(mouse, PixelToGui(_seat, rect, size)) <= 9f) return FurnitureHandle.Seat;
            if (definition.HasOperatorSeatSocket && Vector2.Distance(mouse, PixelToGui(_operatorSeat, rect, size)) <= 9f) return FurnitureHandle.OperatorSeat;
            if (definition.HasOperatorWorkSocket && Vector2.Distance(mouse, PixelToGui(_operatorWork, rect, size)) <= 9f) return FurnitureHandle.OperatorWork;
            return FurnitureHandle.None;
        }

        private void HandlePoseDrag(Rect rect, Vector2 size)
        {
            Event current = Event.current;
            if (current.type == EventType.MouseDown && current.button == 0)
            {
                if (Vector2.Distance(current.mousePosition, PixelToGui(_pelvis, rect, size)) <= 10f)
                    _dragHandle = FurnitureHandle.Seat;
                else if (Vector2.Distance(current.mousePosition, PixelToGui(_hand, rect, size)) <= 10f)
                    _dragHandle = FurnitureHandle.OperatorWork;
                if (_dragHandle != FurnitureHandle.None) current.Use();
            }
            else if (current.type == EventType.MouseDrag && _dragHandle != FurnitureHandle.None)
            {
                Vector2 point = GuiToPixel(current.mousePosition, rect, size);
                if (_dragHandle == FurnitureHandle.Seat) _pelvis = point;
                if (_dragHandle == FurnitureHandle.OperatorWork) _hand = point;
                _compositeApproved = false;
                current.Use();
                Repaint();
            }
            else if (current.type == EventType.MouseUp)
            {
                _dragHandle = FurnitureHandle.None;
            }
        }

        private void LoadFurnitureIfNeeded(OfficeFurnitureVisualDefinition definition)
        {
            string key = definition.KindId + ":" + definition.Facing;
            if (string.Equals(key, _loadedFurnitureKey, StringComparison.Ordinal)) return;
            _loadedFurnitureKey = key;
            _ground = definition.GroundAnchorPx;
            _sort = definition.SortAnchorPx;
            _seat = definition.SeatAnchorPx;
            _operatorSeat = definition.OperatorSeatSocketPx;
            _operatorWork = definition.OperatorWorkSocketPx;
            _furnitureScale = definition.UniformScale;
            _footprint = definition.GroundFootprintPolygonPx.ToArray();
            _compositeApproved = false;
        }

        private void LoadPoseIfNeeded(OfficeCharacterSeatPoseProfile profile)
        {
            string key = $"{profile.MemberId}:{profile.DirectionIndex}:{profile.Clip}:{profile.FrameIndex}";
            if (string.Equals(key, _loadedPoseKey, StringComparison.Ordinal)) return;
            _loadedPoseKey = key;
            _pelvis = profile.PelvisAnchorPx;
            _hand = profile.HandAnchorPx;
            _compositeApproved = false;
        }

        private void SaveFurniture(OfficeFurnitureVisualDefinition definition)
        {
            Undo.RecordObject(_furnitureCatalog, "Approve office furniture calibration");
            definition.ApplyCalibration(_ground, _sort, _footprint, _seat, _operatorSeat, _operatorWork, _furnitureScale);
            _furnitureCatalog.Validate();
            EditorUtility.SetDirty(_furnitureCatalog);
            AssetDatabase.SaveAssets();
        }

        private void SavePose(OfficeCharacterSeatPoseProfile profile)
        {
            Undo.RecordObject(_poseCatalog, "Approve office character pose calibration");
            Sprite sprite = LoadPoseSprite(profile.MemberId, (OfficeSeatFacing8)profile.DirectionIndex, profile.Clip, profile.FrameIndex);
            profile.ApplyCalibration(_pelvis, _hand, 1f, true, ComputeSpriteSha256(sprite));
            _poseCatalog.Validate();
            EditorUtility.SetDirty(_poseCatalog);
            AssetDatabase.SaveAssets();
        }

        private OfficeCharacterSeatPoseProfile TryResolvePose(int? frame = null)
        {
            try
            {
                return _poseCatalog.Resolve(MemberIds[_memberIndex], (int)_facing, _clip, frame ?? _frameIndex);
            }
            catch (KeyNotFoundException)
            {
                return null;
            }
        }

        private static Sprite LoadPoseSprite(
            string memberId,
            OfficeSeatFacing8 facing,
            OfficeSeatingAnimationClip clip,
            int frame)
        {
            string path = OfficeSeatingAnimationFrames.AssetPath(memberId, facing, clip, frame);
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new InvalidOperationException("Missing seating calibration frame: " + path);
            return sprite;
        }

        private void ReloadCatalogs()
        {
            _furnitureCatalog = AssetDatabase.LoadAssetAtPath<OfficeFurnitureVisualCatalog>(OfficeFurnitureAssetBuilder.FurnitureCatalogPath);
            _poseCatalog = AssetDatabase.LoadAssetAtPath<OfficeCharacterSeatPoseCatalog>(OfficeFurnitureAssetBuilder.PoseCatalogPath);
            _loadedFurnitureKey = string.Empty;
            _loadedPoseKey = string.Empty;
            _compositeApproved = false;
            Repaint();
        }

        private static float FootprintResidual(OfficeFurnitureVisualDefinition definition)
        {
            Vector2[] expected = CanonicalFootprint(
                definition.GroundAnchorPx,
                definition.SemanticFootprintWidth,
                definition.SemanticFootprintHeight);
            float maximum = 0f;
            for (int index = 0; index < 4; index++)
                maximum = Mathf.Max(maximum, Vector2.Distance(expected[index], definition.GroundFootprintPolygonPx[index]));
            return maximum * definition.UniformScale;
        }

        private static Vector2[] CanonicalFootprint(Vector2 center, int width, int height)
        {
            Vector2 basisX = new Vector2(OfficeGridTilemapPresenter.TilePixelWidth * 0.5f, OfficeGridTilemapPresenter.TilePixelHeight * 0.5f);
            Vector2 basisY = new Vector2(-OfficeGridTilemapPresenter.TilePixelWidth * 0.5f, OfficeGridTilemapPresenter.TilePixelHeight * 0.5f);
            Vector2 extentX = basisX * (width * 0.5f);
            Vector2 extentY = basisY * (height * 0.5f);
            return new[] { center - extentX - extentY, center + extentX - extentY, center + extentX + extentY, center - extentX + extentY };
        }

        private static void DrawSprite(Sprite sprite, Rect rect, Color tint)
        {
            Color previous = GUI.color;
            GUI.color = tint;
            Rect textureRect = sprite.textureRect;
            Rect uv = new Rect(
                textureRect.x / sprite.texture.width,
                textureRect.y / sprite.texture.height,
                textureRect.width / sprite.texture.width,
                textureRect.height / sprite.texture.height);
            GUI.DrawTextureWithTexCoords(rect, sprite.texture, uv, true);
            GUI.color = previous;
        }

        private static void DrawAlignedSprite(
            Sprite sprite,
            Vector2 anchorPx,
            Vector2 targetGui,
            float scale,
            Color color,
            float rotationDegrees = 0f)
        {
            const float previewScale = 0.72f;
            float pixelScale = previewScale * scale;
            Vector2 size = sprite.rect.size;
            Vector2 anchorFromTopLeft = new Vector2(anchorPx.x, size.y - anchorPx.y) * pixelScale;
            Rect rect = new Rect(targetGui - anchorFromTopLeft, size * pixelScale);
            Matrix4x4 previous = GUI.matrix;
            if (Mathf.Abs(rotationDegrees) > 0.0001f)
                GUIUtility.RotateAroundPivot(-rotationDegrees, targetGui);
            DrawSprite(sprite, rect, color);
            GUI.matrix = previous;
        }

        private static string ComputeSpriteSha256(Sprite sprite)
        {
            string path = AssetDatabase.GetAssetPath(sprite);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new InvalidOperationException("Cannot hash seating Sprite: " + path);
            using SHA256 sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", string.Empty);
        }

        private static Vector2 PixelToGui(Vector2 pixel, Rect rect, Vector2 spriteSize)
        {
            return new Vector2(
                rect.x + pixel.x / spriteSize.x * rect.width,
                rect.y + (spriteSize.y - pixel.y) / spriteSize.y * rect.height);
        }

        private static Vector2 GuiToPixel(Vector2 gui, Rect rect, Vector2 spriteSize)
        {
            return new Vector2(
                Mathf.Clamp((gui.x - rect.x) / rect.width * spriteSize.x, 0f, spriteSize.x),
                Mathf.Clamp(spriteSize.y - (gui.y - rect.y) / rect.height * spriteSize.y, 0f, spriteSize.y));
        }

        private static void DrawPoint(Vector2 point, Color color, string label)
        {
            Handles.BeginGUI();
            Handles.color = color;
            Handles.DrawSolidDisc(point, Vector3.forward, 5f);
            Handles.EndGUI();
            GUI.Label(new Rect(point + new Vector2(7f, -9f), new Vector2(120f, 18f)), label, EditorStyles.miniBoldLabel);
        }

        private static void DrawClosedLine(IReadOnlyList<Vector2> points, Color color, float width)
        {
            if (points == null || points.Count < 2) return;
            var vertices = new Vector3[points.Count + 1];
            for (int index = 0; index < points.Count; index++) vertices[index] = points[index];
            vertices[points.Count] = points[0];
            Handles.BeginGUI();
            Handles.color = color;
            Handles.DrawAAPolyLine(width, vertices);
            Handles.EndGUI();
        }

        private static void DrawFloorDiamond(Vector2 center)
        {
            Vector2[] points =
            {
                center + new Vector2(0f, -60f),
                center + new Vector2(120f, 0f),
                center + new Vector2(0f, 60f),
                center + new Vector2(-120f, 0f)
            };
            DrawClosedLine(points, new Color(1f, 1f, 1f, 0.7f), 2f);
        }
    }
}
