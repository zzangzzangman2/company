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

        /// <summary>Names the player sees. An id like water_dispenser tells them nothing.</summary>
        private static readonly Dictionary<string, string> KindNames =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { OfficeGridLayouts.DeskWithPcKind, "업무 책상" },
                { OfficeGridLayouts.SwivelChairKind, "사무 의자" },
                { OfficeGridLayouts.ReceptionCounterKind, "접수대" },
                { OfficeGridLayouts.MeetingTableKind, "회의 탁자" },
                { OfficeGridLayouts.DocumentBookcaseKind, "서류 책장" },
                { OfficeGridLayouts.FaxCopierKind, "팩스·복사기" },
                { OfficeGridLayouts.WaterDispenserKind, "정수기" },
                { OfficeGridLayouts.SofaKind, "소파" },
                { OfficeGridLayouts.CoffeeTableKind, "커피 테이블" },
                { OfficeGridLayouts.PottedPlantKind, "화분" },
                { OfficeGridLayouts.PartitionKind, "파티션" },
                { OfficeGridLayouts.FilingCabinetKind, "서류 캐비닛" }
            };

        private readonly OfficeLayoutEditModeSkin _skin = new OfficeLayoutEditModeSkin();
        private readonly List<OfficeGrid> _undo = new List<OfficeGrid>();
        private readonly List<GameObject> _overlay = new List<GameObject>();

        private StarterOfficeRuntimeBootstrap _runtime;
        private Camera _camera;
        private Sprite _cellSprite;
        private string _selectedId = string.Empty;
        private string _hoverId = string.Empty;
        private bool _dragging;
        private OfficeGridCoordinate _dragOrigin;
        private OfficeGridCoordinate _dragCurrent;
        private string _toast = string.Empty;
        private float _toastUntil;
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

        private void OnDisable() => ClearOverlay();

        private void Toggle()
        {
            IsOpen = !IsOpen;
            _selectedId = string.Empty;
            _hoverId = string.Empty;
            _dragging = false;
            ClearOverlay();
            Say(IsOpen ? "배치 편집 · 물건을 끌어서 옮기세요" : "편집을 닫았습니다");
        }

        // ------------------------------------------------------------------ picking
        /// <summary>
        /// Picks by drawn sprite, not by floor cell. A water dispenser or a bookcase is drawn far
        /// above the tile it stands on, so cell picking only ever caught wide flat things like desks
        /// and made everything else look immovable. Ties go to the frontmost sprite, which is the one
        /// the player believes they clicked.
        /// </summary>
        private string PickAt(Vector3 worldPoint)
        {
            OfficeGrid grid = Grid;
            if (grid == null) return string.Empty;
            var best = string.Empty;
            int bestOrder = int.MinValue;
            foreach (KeyValuePair<string, SpriteRenderer> entry
                     in _runtime.World.FurniturePresenter.Renderers)
            {
                SpriteRenderer renderer = entry.Value;
                if (renderer == null || !renderer.enabled || renderer.sprite == null) continue;
                Bounds bounds = renderer.bounds;
                if (worldPoint.x < bounds.min.x || worldPoint.x > bounds.max.x) continue;
                if (worldPoint.y < bounds.min.y || worldPoint.y > bounds.max.y) continue;
                if (renderer.sortingOrder <= bestOrder) continue;
                bestOrder = renderer.sortingOrder;
                best = entry.Key;
            }
            if (best.Length > 0) return best;

            // nothing drawn under the cursor: fall back to whatever occupies that floor cell
            OfficeGridCoordinate cell = _runtime.World.Presenter.NearestCell(worldPoint);
            if (!grid.Contains(cell)) return string.Empty;
            foreach (PlacedOfficeFurniture item in grid.Furniture)
            {
                if (cell.X < item.Origin.X || cell.X > item.Origin.X + item.Width - 1) continue;
                if (cell.Y < item.Origin.Y || cell.Y > item.Origin.Y + item.Height - 1) continue;
                return item.FurnitureId;
            }
            return string.Empty;
        }

        private void HandlePointer()
        {
            if (_camera == null || Grid == null) return;
            if (IsPointerOverPanel(Input.mousePosition))
            {
                _hoverId = string.Empty;
                return;
            }
            Vector3 world = _camera.ScreenToWorldPoint(Input.mousePosition);
            world.z = 0f;
            OfficeGridCoordinate cell = _runtime.World.Presenter.NearestCell(world);
            if (!_dragging) _hoverId = PickAt(world);

            if (Input.GetMouseButtonDown(0))
            {
                _selectedId = PickAt(world);
                if (_selectedId.Length == 0) return;
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
                Move(_dragCurrent.X - _dragOrigin.X, _dragCurrent.Y - _dragOrigin.Y);
            }
        }

        private void HandleKeys()
        {
            if (Input.GetKeyDown(KeyCode.Z) &&
                (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
            {
                Undo();
                return;
            }
            if (_selectedId.Length == 0) return;
            if (Input.GetKeyDown(KeyCode.LeftArrow)) Move(-1, 0);
            if (Input.GetKeyDown(KeyCode.RightArrow)) Move(1, 0);
            if (Input.GetKeyDown(KeyCode.UpArrow)) Move(0, 1);
            if (Input.GetKeyDown(KeyCode.DownArrow)) Move(0, -1);
            if (Input.GetKeyDown(KeyCode.R)) Rotate();
            if (Input.GetKeyDown(KeyCode.Delete)) Remove();
        }

        // ------------------------------------------------------------------ edits
        private void Move(int deltaX, int deltaY)
        {
            if (deltaX == 0 && deltaY == 0) return;
            OfficeGrid grid = Grid;
            if (grid == null || _selectedId.Length == 0) return;
            Apply(grid, OfficeLayoutEditRules.MoveFurniture(grid, _selectedId, deltaX, deltaY), "옮겼습니다");
        }

        private void Rotate()
        {
            OfficeGrid grid = Grid;
            if (grid == null || _selectedId.Length == 0) return;
            Apply(grid, OfficeLayoutEditRules.RotateFurniture(grid, _selectedId), "돌렸습니다");
        }

        private void Remove()
        {
            OfficeGrid grid = Grid;
            if (grid == null || _selectedId.Length == 0) return;
            OfficeLayoutEditResult result = OfficeLayoutEditRules.RemoveFurniture(grid, _selectedId);
            if (result.Success) _selectedId = string.Empty;
            Apply(grid, result, "치웠습니다");
        }

        private void Apply(OfficeGrid previous, OfficeLayoutEditResult result, string label)
        {
            if (!result.Success)
            {
                if (result.Failure != OfficeLayoutEditFailure.NothingToDo) Say(result.Message);
                return;
            }
            _undo.Add(previous);
            if (_undo.Count > UndoDepth) _undo.RemoveAt(0);
            _runtime.ApplyLayout(result.Grid);
            ClearOverlay();
            Say(label);
        }

        private void Undo()
        {
            if (_undo.Count == 0)
            {
                Say("되돌릴 편집이 없습니다");
                return;
            }
            OfficeGrid previous = _undo[_undo.Count - 1];
            _undo.RemoveAt(_undo.Count - 1);
            _runtime.ApplyLayout(previous);
            ClearOverlay();
            Say("되돌렸습니다");
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
            string signature = string.Join(
                "|",
                _selectedId,
                _hoverId,
                _dragging ? _dragCurrent + ">" + _dragOrigin : "-",
                grid.ComputeLayoutHash());
            if (string.Equals(signature, _overlaySignature, StringComparison.Ordinal)) return;
            _overlaySignature = signature;
            ClearOverlay();
            EnsureCellSprite();

            if (_hoverId.Length > 0 && !string.Equals(_hoverId, _selectedId, StringComparison.Ordinal))
                foreach (OfficeGridCoordinate cell in GroupCells(grid, _hoverId))
                    DrawCell(cell, new Color(1f, 1f, 1f, 0.20f));

            if (_selectedId.Length == 0) return;
            int deltaX = _dragging ? _dragCurrent.X - _dragOrigin.X : 0;
            int deltaY = _dragging ? _dragCurrent.Y - _dragOrigin.Y : 0;
            bool valid = (deltaX == 0 && deltaY == 0) ||
                         OfficeLayoutEditRules.MoveFurniture(grid, _selectedId, deltaX, deltaY).Success;
            Color tint = valid
                ? new Color(0.36f, 0.80f, 0.48f, 0.45f)
                : new Color(0.86f, 0.32f, 0.28f, 0.50f);
            foreach (OfficeGridCoordinate cell in GroupCells(grid, _selectedId))
            {
                var moved = new OfficeGridCoordinate(cell.X + deltaX, cell.Y + deltaY);
                if (grid.Contains(moved)) DrawCell(moved, tint);
            }
        }

        /// <summary>Cells the whole object covers - a workstation reports desk, chair and approach.</summary>
        private IEnumerable<OfficeGridCoordinate> GroupCells(OfficeGrid grid, string furnitureId)
        {
            PlacedOfficeFurniture item = grid.Furniture.FirstOrDefault(f =>
                string.Equals(f.FurnitureId, furnitureId, StringComparison.Ordinal));
            if (item == null) return Array.Empty<OfficeGridCoordinate>();
            OfficeSeatSlot owner = OwnerSeat(grid, furnitureId);
            if (owner == null) return OfficeLayoutEditRules.FootprintCells(item);

            var cells = new List<OfficeGridCoordinate>();
            foreach (PlacedOfficeFurniture part in grid.Furniture)
            {
                if (!string.Equals(part.FurnitureId, owner.ChairFurnitureId, StringComparison.Ordinal) &&
                    !string.Equals(part.FurnitureId, owner.WorkSurfaceFurnitureId, StringComparison.Ordinal))
                    continue;
                cells.AddRange(OfficeLayoutEditRules.FootprintCells(part));
            }
            cells.Add(owner.ApproachCell);
            return cells;
        }

        private static OfficeSeatSlot OwnerSeat(OfficeGrid grid, string furnitureId) =>
            grid.SeatSlots.FirstOrDefault(seat =>
                string.Equals(seat.ChairFurnitureId, furnitureId, StringComparison.Ordinal) ||
                string.Equals(seat.WorkSurfaceFurnitureId, furnitureId, StringComparison.Ordinal));

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
                float edge = Mathf.Abs(x - width * 0.5f) / (width * 0.5f) +
                             Mathf.Abs(y - height * 0.5f) / (height * 0.5f);
                texture.SetPixel(
                    x,
                    y,
                    edge <= 1f ? (edge >= 0.86f ? Color.white : new Color(1f, 1f, 1f, 0.62f)) : clear);
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
            _toastUntil = Time.unscaledTime + 3f;
        }

        private string DisplayName(OfficeGrid grid, string furnitureId)
        {
            PlacedOfficeFurniture item = grid?.Furniture.FirstOrDefault(f =>
                string.Equals(f.FurnitureId, furnitureId, StringComparison.Ordinal));
            if (item == null) return string.Empty;
            string name = KindNames.TryGetValue(item.KindId, out string known) ? known : item.KindId;
            OfficeSeatSlot owner = OwnerSeat(grid, furnitureId);
            return owner == null ? name : name + " (워크스테이션)";
        }

        // ------------------------------------------------------------------ panel
        private Rect PanelRect()
        {
            float width = _skin.Round(300);
            float height = _skin.Round(232);
            return new Rect(Screen.width - width - _skin.Round(22), _skin.Round(22), width, height);
        }

        private bool IsPointerOverPanel(Vector3 mousePosition)
        {
            if (!IsOpen) return false;
            var point = new Vector2(mousePosition.x, Screen.height - mousePosition.y);
            return PanelRect().Contains(point);
        }

        private void OnGUI()
        {
            if (!IsOpen) return;
            _skin.EnsureBuilt();
            OfficeGrid grid = Grid;
            Rect panel = PanelRect();

            GUI.DrawTexture(
                new Rect(panel.x + 4f, panel.y + 6f, panel.width, panel.height),
                _skin.ShadowTexture);
            GUI.Box(panel, GUIContent.none, _skin.PanelStyle);

            float header = _skin.Round(42);
            GUI.Box(new Rect(panel.x, panel.y, panel.width, header), "  사무실 배치", _skin.HeaderStyle);

            float pad = _skin.Round(16);
            float x = panel.x + pad;
            float width = panel.width - pad * 2f;
            float y = panel.y + header + _skin.Round(12);
            float line = _skin.Round(23);

            bool has = _selectedId.Length > 0 && grid != null;
            string title = has
                ? DisplayName(grid, _selectedId)
                : (_hoverId.Length > 0 ? DisplayName(grid, _hoverId) : "물건을 클릭하세요");
            GUI.Label(new Rect(x, y, width, line + _skin.Round(4)), title, _skin.TitleStyle);
            y += line + _skin.Round(6);

            GUI.Label(
                new Rect(x, y, width, line),
                has ? "끌어서 옮기기 · 방향키로 한 칸씩" : "정수기·화분·소파 전부 옮길 수 있습니다",
                _skin.HintStyle);
            y += line + _skin.Round(8);

            float buttonHeight = _skin.Round(38);
            float gap = _skin.Round(8);
            float half = (width - gap) * 0.5f;
            bool canRotate = has && OfficeLayoutEditRules.CanRotate(grid, _selectedId);

            if (Button(new Rect(x, y, half, buttonHeight), "회전  R", canRotate)) Rotate();
            if (Button(new Rect(x + half + gap, y, half, buttonHeight), "치우기", has, danger: true)) Remove();
            y += buttonHeight + gap;
            if (Button(new Rect(x, y, half, buttonHeight), "되돌리기", _undo.Count > 0)) Undo();
            if (Button(new Rect(x + half + gap, y, half, buttonHeight), "내보내기", grid != null)) Export();
            y += buttonHeight + _skin.Round(6);

            if (has && !canRotate)
            {
                GUI.Label(new Rect(x, y, width, line), "책상·의자는 방향별 아트가 없어 회전 불가", _skin.HintStyle);
                y += line;
            }

            GUI.Label(
                new Rect(x, panel.yMax - _skin.Round(30), width, line),
                grid == null
                    ? "레이아웃 없음"
                    : $"가구 {grid.Furniture.Count} · 편집 {_undo.Count}회 · F2 닫기",
                _skin.ChipStyle);

            if (_toast.Length > 0 && Time.unscaledTime < _toastUntil)
            {
                Vector2 size = _skin.ToastStyle.CalcSize(new GUIContent(_toast));
                GUI.Label(
                    new Rect((Screen.width - size.x) * 0.5f, Screen.height - _skin.Round(92), size.x, size.y),
                    _toast,
                    _skin.ToastStyle);
            }
        }

        private bool Button(Rect rect, string label, bool enabled, bool danger = false)
        {
            GUIStyle style = !enabled
                ? _skin.DisabledButtonStyle
                : (danger ? _skin.DangerButtonStyle : _skin.ButtonStyle);
            return GUI.Button(rect, label, style) && enabled;
        }
    }
}
