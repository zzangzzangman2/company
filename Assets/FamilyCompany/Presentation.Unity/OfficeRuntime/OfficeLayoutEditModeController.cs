using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FamilyCompany.Presentation.Unity.OfficeGridView;
using FamilyCompany.Simulation.OfficeLayout;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime
{
    /// <summary>
    /// In-game layout editor. F2 opens it on top of the running office, so what the player arranges
    /// is the real render path, the real footprint sort and the real collision - there is no second
    /// preview that can disagree with the game.
    ///
    /// Every change goes through <see cref="OfficeLayoutEditRules"/> and then
    /// <see cref="StarterOfficeRuntimeBootstrap.ApplyLayout"/>, which rebuilds render, occupancy,
    /// seats and save state from the same grid. Moving a sprite without moving its collision is not
    /// expressible here.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OfficeLayoutEditModeController : MonoBehaviour
    {
        public const KeyCode ToggleKey = KeyCode.F2;
        private const int UndoDepth = 32;

        private readonly OfficeLayoutEditModeSkin _skin = new OfficeLayoutEditModeSkin();
        private readonly List<OfficeGrid> _undo = new List<OfficeGrid>();
        private readonly List<GameObject> _overlay = new List<GameObject>();

        private StarterOfficeRuntimeBootstrap _runtime;
        private Camera _camera;
        private Sprite _cellSprite;
        private string _selectedFurnitureId = string.Empty;
        private bool _dragging;
        private OfficeGridCoordinate _dragOrigin;
        private OfficeGridCoordinate _dragCurrent;
        private string _toast = string.Empty;
        private float _toastUntil;
        private bool _showAllFootprints = true;
        private string _overlaySignature = string.Empty;

        public bool IsOpen { get; private set; }

        public void Configure(StarterOfficeRuntimeBootstrap runtime, Camera camera)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _camera = camera ?? throw new ArgumentNullException(nameof(camera));
        }

        private OfficeGrid Grid => _runtime != null && _runtime.World != null ? _runtime.World.Grid : null;

        private void Update()
        {
            if (Input.GetKeyDown(ToggleKey)) Toggle();
            if (!IsOpen || _runtime == null || !_runtime.IsReady) return;
            HandlePointer();
            HandleKeys();
            RefreshOverlay();
        }

        private void OnDisable()
        {
            ClearOverlay();
        }

        private void Toggle()
        {
            IsOpen = !IsOpen;
            _selectedFurnitureId = string.Empty;
            _dragging = false;
            ClearOverlay();
            Say(IsOpen ? "배치 편집 모드" : "편집 종료");
        }

        // ------------------------------------------------------------------ input
        private void HandlePointer()
        {
            if (_camera == null || Grid == null) return;
            if (IsPointerOverPanel(Input.mousePosition)) return;
            if (!TryPointerCell(out OfficeGridCoordinate cell)) return;

            if (Input.GetMouseButtonDown(0))
            {
                string hit = FurnitureAt(cell);
                if (hit.Length == 0)
                {
                    _selectedFurnitureId = string.Empty;
                    return;
                }
                _selectedFurnitureId = hit;
                _dragging = true;
                _dragOrigin = cell;
                _dragCurrent = cell;
                return;
            }

            if (_dragging && Input.GetMouseButton(0))
            {
                _dragCurrent = cell;
                return;
            }

            if (_dragging && Input.GetMouseButtonUp(0))
            {
                _dragging = false;
                int deltaX = _dragCurrent.X - _dragOrigin.X;
                int deltaY = _dragCurrent.Y - _dragOrigin.Y;
                if (deltaX != 0 || deltaY != 0) Move(deltaX, deltaY);
            }
        }

        private void HandleKeys()
        {
            if (_selectedFurnitureId.Length == 0) return;
            if (Input.GetKeyDown(KeyCode.LeftArrow)) Move(-1, 0);
            if (Input.GetKeyDown(KeyCode.RightArrow)) Move(1, 0);
            if (Input.GetKeyDown(KeyCode.UpArrow)) Move(0, 1);
            if (Input.GetKeyDown(KeyCode.DownArrow)) Move(0, -1);
            if (Input.GetKeyDown(KeyCode.Delete)) Remove();
            if (Input.GetKeyDown(KeyCode.Z) &&
                (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))) Undo();
        }

        private bool TryPointerCell(out OfficeGridCoordinate cell)
        {
            cell = default;
            if (_runtime.World == null) return false;
            Vector3 world = _camera.ScreenToWorldPoint(Input.mousePosition);
            world.z = 0f;
            cell = _runtime.World.Presenter.NearestCell(world);
            return Grid.Contains(cell);
        }

        private string FurnitureAt(OfficeGridCoordinate cell)
        {
            foreach (PlacedOfficeFurniture item in Grid.Furniture)
            {
                if (cell.X < item.Origin.X || cell.X > item.Origin.X + item.Width - 1) continue;
                if (cell.Y < item.Origin.Y || cell.Y > item.Origin.Y + item.Height - 1) continue;
                return item.FurnitureId;
            }
            foreach (OfficeSeatSlot seat in Grid.SeatSlots)
                if (seat.Cell.Equals(cell)) return seat.ChairFurnitureId;
            return string.Empty;
        }

        // ------------------------------------------------------------------ edits
        private void Move(int deltaX, int deltaY)
        {
            OfficeGrid grid = Grid;
            if (grid == null || _selectedFurnitureId.Length == 0) return;
            OfficeLayoutEditResult result = OfficeLayoutEditRules.MoveFurniture(
                grid, _selectedFurnitureId, deltaX, deltaY);
            if (!result.Success)
            {
                Say(result.Message);
                return;
            }
            Commit(grid, result.Grid, "이동");
        }

        private void Remove()
        {
            OfficeGrid grid = Grid;
            if (grid == null || _selectedFurnitureId.Length == 0) return;
            OfficeLayoutEditResult result = OfficeLayoutEditRules.RemoveFurniture(grid, _selectedFurnitureId);
            if (!result.Success)
            {
                Say(result.Message);
                return;
            }
            _selectedFurnitureId = string.Empty;
            Commit(grid, result.Grid, "삭제");
        }

        private void Undo()
        {
            if (_undo.Count == 0)
            {
                Say("되돌릴 편집이 없습니다.");
                return;
            }
            OfficeGrid previous = _undo[_undo.Count - 1];
            _undo.RemoveAt(_undo.Count - 1);
            _runtime.ApplyLayout(previous);
            Say("되돌렸습니다.");
        }

        private void Commit(OfficeGrid previous, OfficeGrid next, string label)
        {
            _undo.Add(previous);
            if (_undo.Count > UndoDepth) _undo.RemoveAt(0);
            _runtime.ApplyLayout(next);
            ClearOverlay();
            Say($"{label} 완료 · 해시 {next.ComputeLayoutHash().Substring(0, 8)}");
        }

        private void Export()
        {
            OfficeGrid grid = Grid;
            if (grid == null) return;
            try
            {
                string path = Path.Combine(Application.persistentDataPath, "starter-office-layout.txt");
                var lines = new List<string> { "width " + grid.Width, "height " + grid.Height };
                foreach (PlacedOfficeFurniture item in grid.Furniture)
                    lines.Add(
                        $"furniture {item.FurnitureId} {item.KindId} {item.Origin.X} {item.Origin.Y} " +
                        $"{item.Width} {item.Height} {item.PlacementAnchor.X2} {item.PlacementAnchor.Y2} " +
                        $"{(int)item.Facing} {(item.BlocksMovement ? 1 : 0)}");
                foreach (OfficeSeatSlot seat in grid.SeatSlots)
                    lines.Add(
                        $"seat {seat.SeatId} {seat.ChairFurnitureId} {seat.WorkSurfaceFurnitureId} " +
                        $"{seat.Cell.X} {seat.Cell.Y} {seat.ApproachCell.X} {seat.ApproachCell.Y} " +
                        $"{seat.OperatorAnchor.X2} {seat.OperatorAnchor.Y2} {(int)seat.Facing}");
                lines.Add("hash " + grid.ComputeLayoutHash());
                File.WriteAllLines(path, lines);
                Say("내보냈습니다 · " + path);
            }
            catch (Exception exception)
            {
                Say("내보내기 실패 · " + exception.Message);
            }
        }

        // ------------------------------------------------------------------ world overlay
        private void RefreshOverlay()
        {
            OfficeGrid grid = Grid;
            if (grid == null) return;
            // Rebuilding forty sprites every frame would churn the heap for nothing; the overlay only
            // changes when the selection, the drag cell, the toggle or the layout itself changes.
            string signature = string.Join(
                "|",
                _selectedFurnitureId,
                _dragging ? _dragCurrent.ToString() : "-",
                _dragging ? _dragOrigin.ToString() : "-",
                _showAllFootprints ? "1" : "0",
                grid.ComputeLayoutHash());
            if (string.Equals(signature, _overlaySignature, StringComparison.Ordinal)) return;
            _overlaySignature = signature;
            ClearOverlay();
            EnsureCellSprite();

            if (_showAllFootprints)
            {
                foreach (PlacedOfficeFurniture item in grid.Furniture)
                {
                    if (!item.BlocksMovement) continue;
                    foreach (OfficeGridCoordinate cell in OfficeLayoutEditRules.FootprintCells(item))
                        DrawCell(cell, new Color(0.85f, 0.35f, 0.30f, 0.16f));
                }
                foreach (OfficeSeatSlot seat in grid.SeatSlots)
                {
                    DrawCell(seat.Cell, new Color(0.32f, 0.72f, 0.66f, 0.30f));
                    DrawCell(seat.ApproachCell, new Color(0.95f, 0.80f, 0.35f, 0.22f));
                }
            }

            if (_selectedFurnitureId.Length == 0) return;
            PlacedOfficeFurniture selected = grid.Furniture.FirstOrDefault(item =>
                string.Equals(item.FurnitureId, _selectedFurnitureId, StringComparison.Ordinal));
            if (selected == null) return;

            int deltaX = _dragging ? _dragCurrent.X - _dragOrigin.X : 0;
            int deltaY = _dragging ? _dragCurrent.Y - _dragOrigin.Y : 0;
            bool valid = deltaX == 0 && deltaY == 0 ||
                         OfficeLayoutEditRules.MoveFurniture(grid, _selectedFurnitureId, deltaX, deltaY).Success;
            Color tint = valid
                ? new Color(0.36f, 0.74f, 0.45f, 0.42f)
                : new Color(0.82f, 0.34f, 0.31f, 0.45f);
            foreach (OfficeGridCoordinate cell in GroupCells(grid, selected))
            {
                var moved = new OfficeGridCoordinate(cell.X + deltaX, cell.Y + deltaY);
                if (grid.Contains(moved)) DrawCell(moved, tint);
            }
        }

        private IEnumerable<OfficeGridCoordinate> GroupCells(OfficeGrid grid, PlacedOfficeFurniture selected)
        {
            OfficeSeatSlot owner = grid.SeatSlots.FirstOrDefault(seat =>
                string.Equals(seat.ChairFurnitureId, selected.FurnitureId, StringComparison.Ordinal) ||
                string.Equals(seat.WorkSurfaceFurnitureId, selected.FurnitureId, StringComparison.Ordinal));
            if (owner == null) return OfficeLayoutEditRules.FootprintCells(selected);

            var cells = new List<OfficeGridCoordinate>();
            foreach (PlacedOfficeFurniture item in grid.Furniture)
            {
                if (!string.Equals(item.FurnitureId, owner.ChairFurnitureId, StringComparison.Ordinal) &&
                    !string.Equals(item.FurnitureId, owner.WorkSurfaceFurnitureId, StringComparison.Ordinal))
                    continue;
                cells.AddRange(OfficeLayoutEditRules.FootprintCells(item));
            }
            cells.Add(owner.ApproachCell);
            return cells;
        }

        private void DrawCell(OfficeGridCoordinate cell, Color color)
        {
            if (!Grid.Contains(cell)) return;
            var marker = new GameObject("EditOverlayCell");
            marker.transform.SetParent(transform, false);
            marker.transform.position = _runtime.World.Presenter.CellCenterWorld(cell);
            var renderer = marker.AddComponent<SpriteRenderer>();
            renderer.sprite = _cellSprite;
            renderer.color = color;
            renderer.sortingLayerName = "Default";
            renderer.sortingOrder = 30000;
            _overlay.Add(marker);
        }

        private void ClearOverlay()
        {
            foreach (GameObject marker in _overlay)
                if (marker != null) Destroy(marker);
            _overlay.Clear();
            _overlaySignature = string.Empty;
        }

        private void EnsureCellSprite()
        {
            if (_cellSprite != null) return;
            const int width = 320;
            const int height = 160;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point
            };
            var clear = new Color(1f, 1f, 1f, 0f);
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                float dx = Mathf.Abs(x - width * 0.5f) / (width * 0.5f);
                float dy = Mathf.Abs(y - height * 0.5f) / (height * 0.5f);
                float edge = dx + dy;
                texture.SetPixel(x, y, edge <= 1f ? (edge >= 0.88f ? Color.white : new Color(1f, 1f, 1f, 0.75f)) : clear);
            }
            texture.Apply();
            _cellSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f),
                OfficeGridTilemapPresenter.PixelsPerUnit);
            _cellSprite.hideFlags = HideFlags.HideAndDontSave;
        }

        private void Say(string message)
        {
            _toast = message ?? string.Empty;
            _toastUntil = Time.unscaledTime + 3.2f;
        }

        // ------------------------------------------------------------------ panel
        private Rect PanelRect()
        {
            float width = _skin.Round(330);
            float height = _skin.Round(430);
            return new Rect(Screen.width - width - _skin.Round(24), _skin.Round(24), width, height);
        }

        private bool IsPointerOverPanel(Vector3 mousePosition)
        {
            if (!IsOpen) return false;
            Rect rect = PanelRect();
            var point = new Vector2(mousePosition.x, Screen.height - mousePosition.y);
            return rect.Contains(point);
        }

        private void OnGUI()
        {
            if (!IsOpen) return;
            _skin.EnsureBuilt();
            Rect panel = PanelRect();

            GUI.color = Color.white;
            GUI.DrawTexture(
                new Rect(panel.x + 4f, panel.y + 6f, panel.width, panel.height),
                _skin.ShadowTexture);
            GUI.Box(panel, GUIContent.none, _skin.PanelStyle);

            float header = _skin.Round(44);
            GUI.Box(new Rect(panel.x, panel.y, panel.width, header), "  사무실 배치 편집", _skin.HeaderStyle);

            float pad = _skin.Round(18);
            float x = panel.x + pad;
            float width = panel.width - pad * 2f;
            float y = panel.y + header + _skin.Round(14);
            float line = _skin.Round(24);

            OfficeGrid grid = Grid;
            PlacedOfficeFurniture selected = grid == null || _selectedFurnitureId.Length == 0
                ? null
                : grid.Furniture.FirstOrDefault(item =>
                    string.Equals(item.FurnitureId, _selectedFurnitureId, StringComparison.Ordinal));
            OfficeSeatSlot owner = selected == null || grid == null
                ? null
                : grid.SeatSlots.FirstOrDefault(seat =>
                    string.Equals(seat.ChairFurnitureId, selected.FurnitureId, StringComparison.Ordinal) ||
                    string.Equals(seat.WorkSurfaceFurnitureId, selected.FurnitureId, StringComparison.Ordinal));

            GUI.Label(new Rect(x, y, width, line), selected == null ? "선택 없음" : selected.KindId, _skin.TitleStyle);
            y += line;
            GUI.Label(
                new Rect(x, y, width, line),
                selected == null
                    ? "가구를 클릭해 선택하세요"
                    : (owner != null ? "워크스테이션 · 책상+의자+좌석 함께 이동" : "단일 가구"),
                _skin.HintStyle);
            y += line + _skin.Round(6);

            if (selected != null)
            {
                Row(x, ref y, width, line, "위치", $"({selected.Origin.X}, {selected.Origin.Y})");
                Row(x, ref y, width, line, "크기", $"{selected.Width} x {selected.Height}");
                Row(x, ref y, width, line, "통행", selected.BlocksMovement ? "막음" : "통과");
                if (owner != null)
                {
                    Row(x, ref y, width, line, "좌석", $"({owner.Cell.X}, {owner.Cell.Y})");
                    Row(x, ref y, width, line, "접근칸", $"({owner.ApproachCell.X}, {owner.ApproachCell.Y})");
                }
                y += _skin.Round(8);
            }

            float buttonHeight = _skin.Round(36);
            float gap = _skin.Round(8);
            float half = (width - gap) * 0.5f;

            bool hasSelection = selected != null;
            if (Button(new Rect(x, y, half, buttonHeight), "← 왼쪽", hasSelection)) Move(-1, 0);
            if (Button(new Rect(x + half + gap, y, half, buttonHeight), "오른쪽 →", hasSelection)) Move(1, 0);
            y += buttonHeight + gap;
            if (Button(new Rect(x, y, half, buttonHeight), "↑ 뒤로", hasSelection)) Move(0, 1);
            if (Button(new Rect(x + half + gap, y, half, buttonHeight), "↓ 앞으로", hasSelection)) Move(0, -1);
            y += buttonHeight + gap;

            if (Button(new Rect(x, y, half, buttonHeight), "회전", false)) { }
            if (Button(new Rect(x + half + gap, y, half, buttonHeight), "삭제", hasSelection, danger: true)) Remove();
            y += buttonHeight + _skin.Round(4);
            GUI.Label(new Rect(x, y, width, line), "회전은 방향별 가구·좌석 아트가 준비되면 열립니다", _skin.HintStyle);
            y += line + gap;

            if (Button(new Rect(x, y, half, buttonHeight), "되돌리기", _undo.Count > 0)) Undo();
            if (Button(new Rect(x + half + gap, y, half, buttonHeight), "내보내기", grid != null)) Export();
            y += buttonHeight + gap;

            string toggleLabel = _showAllFootprints ? "점유 표시 끄기" : "점유 표시 켜기";
            if (Button(new Rect(x, y, width, buttonHeight), toggleLabel, true)) _showAllFootprints = !_showAllFootprints;
            y += buttonHeight + gap;

            GUI.Label(
                new Rect(x, y, width, line * 3f),
                "드래그로 이동 · 방향키 한 칸 · Delete 삭제 · Ctrl+Z 되돌리기 · F2 닫기",
                _skin.HintStyle);

            float legendY = panel.yMax - _skin.Round(30);
            GUI.Label(new Rect(x, legendY, width, line), Legend(grid), _skin.ChipStyle);

            if (_toast.Length > 0 && Time.unscaledTime < _toastUntil)
            {
                var size = _skin.ToastStyle.CalcSize(new GUIContent(_toast));
                var rect = new Rect(
                    (Screen.width - size.x) * 0.5f,
                    Screen.height - _skin.Round(96),
                    size.x,
                    size.y);
                GUI.Label(rect, _toast, _skin.ToastStyle);
            }
        }

        private string Legend(OfficeGrid grid)
        {
            if (grid == null) return "레이아웃 없음";
            return $"가구 {grid.Furniture.Count} · 좌석 {grid.SeatSlots.Count} · 편집 {_undo.Count}회";
        }

        private void Row(float x, ref float y, float width, float line, string label, string value)
        {
            GUI.Label(new Rect(x, y, width * 0.45f, line), label, _skin.BodyStyle);
            GUI.Label(new Rect(x + width * 0.45f, y, width * 0.55f, line), value, _skin.ValueStyle);
            y += line;
        }

        private bool Button(Rect rect, string label, bool enabled, bool danger = false)
        {
            GUIStyle style = !enabled
                ? _skin.DisabledButtonStyle
                : (danger ? _skin.DangerButtonStyle : _skin.ButtonStyle);
            bool pressed = GUI.Button(rect, label, style);
            return pressed && enabled;
        }
    }
}
