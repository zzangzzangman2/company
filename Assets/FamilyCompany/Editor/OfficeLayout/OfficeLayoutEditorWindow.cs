using System;
using System.Linq;
using FamilyCompany.Presentation.Unity.OfficeRuntime;
using UnityEditor;
using UnityEngine;

namespace FamilyCompany.Editor.OfficeLayout
{
    public sealed class OfficeLayoutEditorWindow : EditorWindow
    {
        private StarterOfficeLayoutAsset _asset;
        private string _selectedFurnitureId = string.Empty;
        private Vector2Int _selectedCell = new Vector2Int(1, 1);
        private Vector2 _scroll;
        private int _placeableIndex;
        private string _workstationMemberId = "worker";
        private OfficeLayoutValidationReport _report;

        [MenuItem("Family Company/Office/Starter Office Layout Editor")]
        public static void Open()
        {
            GetWindow<OfficeLayoutEditorWindow>("Starter Office Layout").Show();
        }

        private void OnEnable()
        {
            if (_asset == null)
                _asset = AssetDatabase.LoadAssetAtPath<StarterOfficeLayoutAsset>(
                    OfficeLayoutAssetBuilder.AssetPath);
            Undo.undoRedoPerformed += OnUndoRedo;
            RefreshValidation();
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        private void OnUndoRedo()
        {
            RefreshValidation();
            Repaint();
        }

        private void OnGUI()
        {
            DrawToolbar();
            if (_asset == null)
            {
                EditorGUILayout.HelpBox(
                    "StarterOfficeV1.asset is missing. Use Rebuild Starter Office V1 Definition.",
                    MessageType.Error);
                if (GUILayout.Button("Build StarterOfficeV1.asset"))
                {
                    OfficeLayoutAssetBuilder.RebuildDefault();
                    _asset = AssetDatabase.LoadAssetAtPath<StarterOfficeLayoutAsset>(
                        OfficeLayoutAssetBuilder.AssetPath);
                    RefreshValidation();
                }
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.BeginHorizontal();
            DrawGrid();
            GUILayout.Space(10f);
            EditorGUILayout.BeginVertical(GUILayout.MinWidth(310f));
            DrawSelectionPanel();
            GUILayout.Space(8f);
            DrawPlacementPanel();
            GUILayout.Space(8f);
            DrawValidation();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            _asset = (StarterOfficeLayoutAsset)EditorGUILayout.ObjectField(
                _asset,
                typeof(StarterOfficeLayoutAsset),
                false,
                GUILayout.MinWidth(260f));
            if (GUILayout.Button("Validate", EditorStyles.toolbarButton)) RefreshValidation();
            using (new EditorGUI.DisabledScope(_asset == null || _report == null || !_report.IsValid))
            {
                if (GUILayout.Button("Save", EditorStyles.toolbarButton))
                {
                    EditorUtility.SetDirty(_asset);
                    AssetDatabase.SaveAssets();
                    Debug.Log($"STARTER_OFFICE_LAYOUT_EDITOR_SAVE | hash={_asset.LayoutHash}");
                }
                if (GUILayout.Button("Preview Data", EditorStyles.toolbarButton))
                {
                    Selection.activeObject = _asset;
                    EditorGUIUtility.PingObject(_asset);
                    Debug.Log(
                        $"STARTER_OFFICE_LAYOUT_EDITOR_PREVIEW_PASS | hash={_asset.LayoutHash} " +
                        $"cell={_selectedCell.x},{_selectedCell.y}");
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawGrid()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(_asset.Width * 32f + 8f));
            EditorGUILayout.LabelField(
                $"Grid {_asset.Width}x{_asset.Height} — select a cell; colors: hard red, interaction amber",
                EditorStyles.boldLabel);
            for (var y = _asset.Height - 1; y >= 0; y--)
            {
                EditorGUILayout.BeginHorizontal();
                for (var x = 0; x < _asset.Width; x++)
                {
                    Color previous = GUI.backgroundColor;
                    GUI.backgroundColor = CellColor(x, y);
                    if (GUILayout.Button($"{x},{y}", GUILayout.Width(30f), GUILayout.Height(24f)))
                    {
                        _selectedCell = new Vector2Int(x, y);
                        StarterOfficeLayoutAsset.FurnitureRecord hit = _asset.Furniture.LastOrDefault(item =>
                            x >= item.OriginX && x < item.OriginX + item.Width &&
                            y >= item.OriginY && y < item.OriginY + item.Height);
                        if (hit != null) _selectedFurnitureId = hit.FurnitureId;
                    }
                    GUI.backgroundColor = previous;
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }

        private Color CellColor(int x, int y)
        {
            bool selected = _selectedCell.x == x && _selectedCell.y == y;
            bool interaction = _asset.Seats.Any(item => item.CellX == x && item.CellY == y);
            StarterOfficeLayoutAsset.FurnitureRecord hard = _asset.Furniture.FirstOrDefault(item =>
                item.BlocksMovement &&
                x >= item.OriginX && x < item.OriginX + item.Width &&
                y >= item.OriginY && y < item.OriginY + item.Height);
            bool selectedFurniture = _asset.Furniture.Any(item =>
                item.FurnitureId == _selectedFurnitureId &&
                x >= item.OriginX && x < item.OriginX + item.Width &&
                y >= item.OriginY && y < item.OriginY + item.Height);
            if (selected) return new Color(0.35f, 0.85f, 1f);
            if (selectedFurniture) return new Color(0.35f, 1f, 0.45f);
            if (interaction) return new Color(1f, 0.65f, 0.18f);
            if (hard != null) return new Color(1f, 0.35f, 0.35f);
            if (x == 0 || y == 0 || x == _asset.Width - 1 || y == _asset.Height - 1)
                return new Color(0.42f, 0.42f, 0.42f);
            return new Color(0.8f, 0.95f, 0.82f);
        }

        private void DrawSelectionPanel()
        {
            EditorGUILayout.LabelField("Selection", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Cell", $"({_selectedCell.x}, {_selectedCell.y})");
            string[] ids = new[] { "<none>" }
                .Concat(_asset.Furniture.Select(item => item.FurnitureId))
                .ToArray();
            int currentIndex = Math.Max(0, Array.IndexOf(ids, _selectedFurnitureId));
            int nextIndex = EditorGUILayout.Popup("Furniture", currentIndex, ids);
            _selectedFurnitureId = nextIndex == 0 ? string.Empty : ids[nextIndex];
            StarterOfficeLayoutAsset.FurnitureRecord selected = _asset.Furniture.FirstOrDefault(
                item => item.FurnitureId == _selectedFurnitureId);
            if (selected == null) return;

            EditorGUILayout.LabelField("Kind", selected.KindId);
            EditorGUILayout.LabelField(
                "Hard footprint",
                $"({selected.OriginX},{selected.OriginY}) {selected.Width}x{selected.Height}");
            EditorGUILayout.LabelField(
                "Semantic anchor",
                $"({selected.PlacementX2}/2, {selected.PlacementY2}/2)");
            EditorGUILayout.LabelField("Blocks movement", selected.BlocksMovement.ToString());

            EditorGUILayout.LabelField("0.5-cell semantic movement", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("← 0.5")) Mutate("Move furniture anchor", () =>
                _asset.TranslateFurnitureAnchor(_selectedFurnitureId, -1, 0));
            if (GUILayout.Button("↑ 0.5")) Mutate("Move furniture anchor", () =>
                _asset.TranslateFurnitureAnchor(_selectedFurnitureId, 0, 1));
            if (GUILayout.Button("↓ 0.5")) Mutate("Move furniture anchor", () =>
                _asset.TranslateFurnitureAnchor(_selectedFurnitureId, 0, -1));
            if (GUILayout.Button("0.5 →")) Mutate("Move furniture anchor", () =>
                _asset.TranslateFurnitureAnchor(_selectedFurnitureId, 1, 0));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("Whole footprint movement", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("← 1")) MoveFootprint(-1, 0);
            if (GUILayout.Button("↑ 1")) MoveFootprint(0, 1);
            if (GUILayout.Button("↓ 1")) MoveFootprint(0, -1);
            if (GUILayout.Button("1 →")) MoveFootprint(1, 0);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Rotate 90°")) Mutate("Rotate furniture", () =>
                _asset.RotateFurnitureClockwise(_selectedFurnitureId));
            if (GUILayout.Button("Duplicate"))
            {
                Mutate("Duplicate furniture", () =>
                {
                    string copy = _asset.DuplicateFurniture(_selectedFurnitureId);
                    if (copy.Length == 0) return false;
                    _selectedFurnitureId = copy;
                    return true;
                });
            }
            if (GUILayout.Button("Delete")) Mutate("Delete furniture", () =>
                _asset.DeleteFurniture(_selectedFurnitureId));
            EditorGUILayout.EndHorizontal();
            if (_asset.Seats.Any(item => item.ChairFurnitureId == _selectedFurnitureId) &&
                GUILayout.Button("Use selected cell as seat approach"))
                Mutate("Set seat approach", () => _asset.SetSeatApproachForChair(
                    _selectedFurnitureId, _selectedCell.x, _selectedCell.y));
        }

        private void DrawPlacementPanel()
        {
            EditorGUILayout.LabelField("Placement", EditorStyles.boldLabel);
            string[] names = OfficePlaceableCatalog.All.Select(item => item.KindId).ToArray();
            _placeableIndex = EditorGUILayout.Popup("Placeable", _placeableIndex, names);
            OfficePlaceableDefinition definition = OfficePlaceableCatalog.All[_placeableIndex];
            EditorGUILayout.LabelField(
                "Footprint / clearance",
                $"{definition.HardFootprint.Width}x{definition.HardFootprint.Height} / {definition.ExtraClearance:0.00}");
            if (GUILayout.Button("Place at selected cell"))
            {
                Mutate("Place office furniture", () =>
                {
                    _selectedFurnitureId = _asset.AddFurniture(
                        definition.KindId,
                        _selectedCell.x,
                        _selectedCell.y,
                        definition.HardFootprint.Width,
                        definition.HardFootprint.Height,
                        definition.BlocksMovement);
                    return true;
                });
            }

            _workstationMemberId = EditorGUILayout.TextField("Workstation member", _workstationMemberId);
            if (GUILayout.Button("Place Workstation Blueprint"))
            {
                Mutate("Place workstation blueprint", () =>
                {
                    _selectedFurnitureId = _asset.AddWorkstationBlueprint(
                        _workstationMemberId,
                        _selectedCell.x,
                        _selectedCell.y);
                    return true;
                });
            }
        }

        private void DrawValidation()
        {
            EditorGUILayout.LabelField("Live validation", EditorStyles.boldLabel);
            if (_report == null) RefreshValidation();
            if (_report.IsValid)
                EditorGUILayout.HelpBox(
                    $"VALID — save/preview enabled. Warnings: {_report.Warnings.Count}",
                    _report.Warnings.Count == 0 ? MessageType.Info : MessageType.Warning);
            foreach (string error in _report.Errors)
                EditorGUILayout.HelpBox(error, MessageType.Error);
            foreach (string warning in _report.Warnings)
                EditorGUILayout.HelpBox(warning, MessageType.Warning);
        }

        private void MoveFootprint(int deltaX, int deltaY)
        {
            Mutate("Move furniture footprint", () =>
                _asset.TranslateFurnitureFootprint(_selectedFurnitureId, deltaX, deltaY));
        }

        private void Mutate(string undoName, Func<bool> mutation)
        {
            if (_asset == null || mutation == null) return;
            Undo.RecordObject(_asset, undoName);
            if (!mutation()) return;
            EditorUtility.SetDirty(_asset);
            RefreshValidation();
            Repaint();
        }

        private void RefreshValidation()
        {
            _report = OfficeLayoutSemanticValidator.Validate(_asset);
        }
    }
}
