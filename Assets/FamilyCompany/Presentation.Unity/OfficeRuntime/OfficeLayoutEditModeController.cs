using System;
using System.Collections.Generic;
using System.Linq;
using FamilyCompany.Presentation.Unity.OfficeGridView;
using FamilyCompany.Presentation.Unity.OfficeGridView.Authoring;
using FamilyCompany.Simulation.OfficeLayout;
using FamilyCompany.Simulation.Game;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity.OfficeRuntime
{
    /// <summary>
    /// Transaction-backed in-world build editor. Preview values never enter occupancy/pathfinding;
    /// only a confirmed command changes GameState and triggers Starter runtime's actor-preserving
    /// atomic diff rebuild.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OfficeLayoutEditModeController : MonoBehaviour
    {
        public const string NavigationEntryId = "company.hub.build_editor";

        private enum PendingSource { None, Purchase, Stored }
        private enum Confirmation { None, Store, Sell }

        private readonly struct BuildStateSnapshot
        {
            public BuildStateSnapshot(GameState state)
            {
                Cash = state.Company.CashWon;
                Ledger = state.Company.Ledger.Count;
                Inventory = state.OfficeFurnitureInventory.Instances.Count;
                Furniture = state.OfficeGrid.Furniture.Count;
                EditableFurniture = state.OfficeGrid.Furniture.Count(item =>
                    OfficeFurnitureCatalog.Find(item.KindId)?.IsPlayerEditable == true);
                GridHash = state.OfficeGrid.ComputeLayoutHash();
            }

            public long Cash { get; }
            public int Ledger { get; }
            public int Inventory { get; }
            public int Furniture { get; }
            public int EditableFurniture { get; }
            public string GridHash { get; }
        }

        private readonly OfficeLayoutEditModeSkin _skin = new OfficeLayoutEditModeSkin();
        private readonly List<GameObject> _overlay = new List<GameObject>();
        private StarterOfficeRuntimeBootstrap _runtime;
        private Camera _camera;
        private PrototypeBootstrap _mainBootstrap;
        private bool _mainBootstrapWasEnabled;
        private float _previousTimeScale = 1f;
        private Sprite _cellSprite;
        private GameObject _ghost;
        private string _selectedId = string.Empty;
        private PendingSource _pendingSource;
        private string _pendingDefinitionId = string.Empty;
        private string _pendingStoredInstanceId = string.Empty;
        private OfficeGridCoordinate _previewOrigin;
        private OfficeFurnitureFacing _previewRotation;
        private OfficeLayoutEditResult _previewEdit;
        private string _previewMessage = string.Empty;
        private string _previewSignature = string.Empty;
        private bool _dragging;
        private OfficeGridCoordinate _dragPointerOrigin;
        private OfficeGridCoordinate _dragFurnitureOrigin;
        private Vector2 _catalogScroll;
        private int _categoryIndex;
        private Confirmation _confirmation;
        private string _confirmationCommandId = string.Empty;
        private string _toast = string.Empty;
        private float _toastUntil;
        private int _instanceSequence;

        private static readonly OfficeFurnitureCategory?[] CategoryCycle =
        {
            null,
            OfficeFurnitureCategory.Work,
            OfficeFurnitureCategory.Seating,
            OfficeFurnitureCategory.OfficeEquipment,
            OfficeFurnitureCategory.Storage,
            OfficeFurnitureCategory.Refreshment,
            OfficeFurnitureCategory.Rest,
            OfficeFurnitureCategory.Decoration,
            OfficeFurnitureCategory.Divider
        };

        private static readonly string[] CategoryNames =
            { "전체", "업무", "좌석", "기기", "수납", "음료", "휴식", "장식", "구획" };

        public bool IsOpen { get; private set; }

        // Player-build diagnostics used by the native-pointer placement gate. The gate may
        // prepare a purchase preview through the narrow QA hook below, but the state mutation
        // still has to arrive through HandlePointer's real Input.GetMouseButtonDown(0) branch.
        public int DiagnosticPointerCommitCount { get; private set; }
        public int DiagnosticStateMutationCount { get; private set; }
        public OfficeGridCoordinate DiagnosticLastPointerCommitCell { get; private set; }
        public string DiagnosticLastMutationInstanceId { get; private set; } = string.Empty;
        public bool PreviewValidForPlayerQa => PreviewValid;
        public OfficeGridCoordinate PreviewOriginForPlayerQa => _previewOrigin;

        public void Configure(StarterOfficeRuntimeBootstrap runtime, Camera camera)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _camera = camera ?? throw new ArgumentNullException(nameof(camera));
        }

        public bool OpenFromNavigation(string navigationId, out string failure)
        {
            if (!string.Equals(navigationId, NavigationEntryId, StringComparison.Ordinal))
            {
                failure = "알 수 없는 건축 모드 navigation ID입니다.";
                return false;
            }
            return Open(out failure);
        }

        public bool Open(out string failure)
        {
            if (IsOpen)
            {
                failure = string.Empty;
                return true;
            }
            _mainBootstrap = FindFirstObjectByType<PrototypeBootstrap>();
            if (_runtime == null || !_runtime.IsReady || _mainBootstrap?.State == null || _runtime.World == null)
            {
                failure = "사무실 월드가 아직 준비되지 않았습니다.";
                return false;
            }
            IsOpen = true;
            _previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            if (_mainBootstrap != null)
            {
                _mainBootstrapWasEnabled = _mainBootstrap.enabled;
                _mainBootstrap.enabled = false;
            }
            ClearPending();
            failure = string.Empty;
            Debug.Log("OFFICE_BUILD_EDITOR_OPEN | timeScale=" + Time.timeScale);
            Say("사무실 관리 · 배치 중에는 게임 시간과 AI가 정지됩니다");
            return true;
        }

        public bool BeginPurchaseForPlayerQa(string definitionId, out string failure)
        {
            if (!IsOpen || State == null || Grid == null)
            {
                failure = "사무실 배치 모드가 준비되지 않았습니다.";
                return false;
            }
            OfficeFurnitureDefinition definition = OfficeFurnitureCatalog.Find(definitionId);
            if (definition == null || !definition.IsPurchasable)
            {
                failure = "구매 가능한 가구 정의가 아닙니다: " + (definitionId ?? string.Empty);
                return false;
            }
            BeginPurchase(definition);
            failure = string.Empty;
            return true;
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            ClearPending();
            ClearVisuals();
            if (_mainBootstrap != null) _mainBootstrap.enabled = _mainBootstrapWasEnabled;
            Time.timeScale = _previousTimeScale;
            Say("사무실 관리를 닫았습니다");
        }

        private GameState State => _mainBootstrap == null ? null : _mainBootstrap.State;
        private OfficeGrid Grid => State?.OfficeGrid;
        private OfficeFurnitureInventoryState Inventory => State?.OfficeFurnitureInventory;

        private void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!IsOpen && Input.GetKeyDown(KeyCode.F2))
            {
                if (!Open(out string error)) Say(error);
            }
#endif
            if (!IsOpen || _runtime == null || !_runtime.IsReady || Grid == null) return;
            HandleKeys();
            HandlePointer();
            RefreshPreview();
        }

        private void OnDisable()
        {
            if (IsOpen) Close();
            ClearVisuals();
        }

        private void HandleKeys()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Input.GetKeyDown(KeyCode.F2))
            {
                Close();
                return;
            }
#endif
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
            {
                if (_confirmation != Confirmation.None || HasPendingChange()) ClearPending();
                else Close();
                return;
            }
            if (Input.GetKeyDown(KeyCode.R))
            {
                if (_pendingSource != PendingSource.None || _selectedId.Length > 0)
                {
                    _previewRotation = OfficeLayoutEditRules.QuarterTurnClockwise(_previewRotation);
                    InvalidatePreview();
                }
            }
            if (_selectedId.Length == 0 || _pendingSource != PendingSource.None) return;
            if (Input.GetKeyDown(KeyCode.LeftArrow)) Nudge(-1, 0);
            if (Input.GetKeyDown(KeyCode.RightArrow)) Nudge(1, 0);
            if (Input.GetKeyDown(KeyCode.UpArrow)) Nudge(0, 1);
            if (Input.GetKeyDown(KeyCode.DownArrow)) Nudge(0, -1);
        }

        private void HandlePointer()
        {
            if (_camera == null || IsPointerOverUi(Input.mousePosition)) return;
            Vector3 world = _camera.ScreenToWorldPoint(Input.mousePosition);
            world.z = 0f;
            OfficeGridCoordinate cell = _runtime.World.Presenter.NearestCell(world);
            if (_pendingSource != PendingSource.None)
            {
                _previewOrigin = cell;
                InvalidatePreview();
                if (Input.GetMouseButtonDown(0))
                {
                    DiagnosticPointerCommitCount++;
                    DiagnosticLastPointerCommitCell = cell;
                    Debug.Log(
                        "OFFICE_BUILD_POINTER_COMMIT | source=" + _pendingSource +
                        " cell=" + cell.X + ":" + cell.Y);
                    ConfirmPreview();
                }
                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                string picked = PickAt(world);
                if (picked.Length == 0)
                {
                    Select(string.Empty);
                    return;
                }
                Select(picked);
                _dragging = true;
                _dragPointerOrigin = cell;
                _dragFurnitureOrigin = _previewOrigin;
            }
            if (_dragging && Input.GetMouseButton(0))
            {
                _previewOrigin = new OfficeGridCoordinate(
                    _dragFurnitureOrigin.X + cell.X - _dragPointerOrigin.X,
                    _dragFurnitureOrigin.Y + cell.Y - _dragPointerOrigin.Y);
                InvalidatePreview();
            }
            if (_dragging && Input.GetMouseButtonUp(0)) _dragging = false;
        }

        private string PickAt(Vector3 worldPoint)
        {
            string best = string.Empty;
            int bestOrder = int.MinValue;
            foreach (KeyValuePair<string, SpriteRenderer> entry in _runtime.World.FurniturePresenter.Renderers)
            {
                SpriteRenderer renderer = entry.Value;
                if (renderer == null || !renderer.enabled || renderer.sprite == null ||
                    !renderer.bounds.Contains(new Vector3(worldPoint.x, worldPoint.y, renderer.bounds.center.z)) ||
                    renderer.sortingOrder <= bestOrder) continue;
                PlacedOfficeFurniture item = Grid.Furniture.FirstOrDefault(value =>
                    string.Equals(value.FurnitureId, entry.Key, StringComparison.Ordinal));
                if (item == null || OfficeFurnitureCatalog.Find(item.KindId)?.IsPlayerEditable != true) continue;
                best = entry.Key;
                bestOrder = renderer.sortingOrder;
            }
            return best;
        }

        private void Select(string instanceId)
        {
            ClearPending();
            _selectedId = instanceId ?? string.Empty;
            OfficeFurnitureInstanceState instance = Inventory?.Find(_selectedId);
            if (instance == null || instance.PlacementState != OfficeFurniturePlacementState.Placed)
            {
                _selectedId = string.Empty;
                return;
            }
            _previewOrigin = instance.GridOrigin;
            _previewRotation = instance.Rotation;
            InvalidatePreview();
        }

        private void Nudge(int x, int y)
        {
            _previewOrigin = new OfficeGridCoordinate(_previewOrigin.X + x, _previewOrigin.Y + y);
            InvalidatePreview();
        }

        private void BeginPurchase(OfficeFurnitureDefinition definition)
        {
            ClearPending();
            _pendingSource = PendingSource.Purchase;
            _pendingDefinitionId = definition.DefinitionId;
            _previewRotation = definition.DesiredFacing;
            _previewOrigin = new OfficeGridCoordinate(6, 6);
            InvalidatePreview();
            Debug.Log(
                "OFFICE_BUILD_PREVIEW_BEGIN | source=purchase definition=" +
                definition.DefinitionId + " origin=6:6");
            Say("마우스로 위치 선택 · R 회전 · 확정 전에는 돈이 차감되지 않습니다");
        }

        private void BeginStored(OfficeFurnitureInstanceState instance)
        {
            ClearPending();
            _pendingSource = PendingSource.Stored;
            _pendingDefinitionId = instance.DefinitionId;
            _pendingStoredInstanceId = instance.InstanceId;
            _previewOrigin = new OfficeGridCoordinate(6, 6);
            _previewRotation = instance.Rotation;
            InvalidatePreview();
        }

        private void ClearPending()
        {
            _confirmation = Confirmation.None;
            _confirmationCommandId = string.Empty;
            _pendingSource = PendingSource.None;
            _pendingDefinitionId = string.Empty;
            _pendingStoredInstanceId = string.Empty;
            _selectedId = string.Empty;
            _dragging = false;
            _previewEdit = null;
            _previewMessage = string.Empty;
            InvalidatePreview();
        }

        private bool HasPendingChange()
        {
            if (_pendingSource != PendingSource.None) return true;
            OfficeFurnitureInstanceState instance = Inventory?.Find(_selectedId);
            return instance != null &&
                   (!instance.GridOrigin.Equals(_previewOrigin) || instance.Rotation != _previewRotation);
        }

        private void RefreshPreview()
        {
            string signature = string.Join("|", Grid.ComputeLayoutHash(), _selectedId, (int)_pendingSource,
                _pendingDefinitionId, _pendingStoredInstanceId, _previewOrigin, (int)_previewRotation,
                State.Company.CashWon);
            if (string.Equals(signature, _previewSignature, StringComparison.Ordinal)) return;
            _previewSignature = signature;
            _previewMessage = string.Empty;
            _previewEdit = BuildPreview();
            if (_previewEdit != null && !_previewEdit.Success) _previewMessage = _previewEdit.Message;
            if (_pendingSource == PendingSource.Purchase)
            {
                OfficeFurnitureDefinition definition = OfficeFurnitureCatalog.Require(_pendingDefinitionId);
                long price = OfficeFurnitureEconomyConfig.GameplayPrice(definition.PurchasePriceWon);
                if (State.Company.CashWon < price) _previewMessage = "자금 부족";
            }
            RefreshVisuals();
        }

        private OfficeLayoutEditResult BuildPreview()
        {
            if (_pendingSource != PendingSource.None)
            {
                string id = _pendingSource == PendingSource.Stored
                    ? _pendingStoredInstanceId
                    : "__purchase_preview__";
                return OfficeLayoutEditRules.PlaceFurniture(
                    Grid, id, _pendingDefinitionId, _previewOrigin, _previewRotation);
            }
            OfficeFurnitureInstanceState instance = Inventory?.Find(_selectedId);
            if (instance == null) return null;
            OfficeGrid candidate = Grid;
            if (!instance.GridOrigin.Equals(_previewOrigin))
            {
                OfficeLayoutEditResult moved = OfficeLayoutEditRules.MoveFurniture(
                    candidate, instance.InstanceId,
                    _previewOrigin.X - instance.GridOrigin.X,
                    _previewOrigin.Y - instance.GridOrigin.Y);
                if (!moved.Success) return moved;
                candidate = moved.Grid;
            }
            int turns = ((int)_previewRotation - (int)instance.Rotation + 4) & 3;
            for (int index = 0; index < turns; index++)
            {
                OfficeLayoutEditResult rotated = OfficeLayoutEditRules.RotateFurniture(candidate, instance.InstanceId);
                if (!rotated.Success) return rotated;
                candidate = rotated.Grid;
            }
            return OfficeLayoutEditResult.Ok(candidate);
        }

        private bool PreviewValid
        {
            get
            {
                if (_previewEdit == null || !_previewEdit.Success || _previewMessage.Length > 0) return false;
                return _pendingSource != PendingSource.None || HasPendingChange();
            }
        }

        private void ConfirmPreview()
        {
            RefreshPreview();
            if (!PreviewValid)
            {
                Say(_previewMessage.Length > 0 ? _previewMessage : "변경할 내용이 없습니다");
                return;
            }
            var before = new BuildStateSnapshot(State);
            OfficeFurnitureCommandResult result;
            if (_pendingSource == PendingSource.Purchase)
            {
                string instanceId = NextInstanceId(_pendingDefinitionId);
                result = OfficeFurnitureTransactionService.PurchaseAndPlace(
                    State,
                    "office-furniture-buy:" + instanceId,
                    instanceId,
                    _pendingDefinitionId,
                    _previewOrigin,
                    _previewRotation);
            }
            else if (_pendingSource == PendingSource.Stored)
            {
                result = OfficeFurnitureTransactionService.PlaceStored(
                    State, _pendingStoredInstanceId, _previewOrigin, _previewRotation);
            }
            else
            {
                result = OfficeFurnitureTransactionService.Relocate(
                    State, _selectedId, _previewOrigin, _previewRotation, IsFurnitureInUse);
            }
            HandleCommand(result, result.ChargedWon > 0
                ? $"구매·배치 완료 · {Won(result.ChargedWon)} 차감"
                : "배치 변경을 확정했습니다", before);
        }

        private void ConfirmDestructive()
        {
            if (_confirmation == Confirmation.None || _selectedId.Length == 0) return;
            var before = new BuildStateSnapshot(State);
            OfficeFurnitureCommandResult result = _confirmation == Confirmation.Store
                ? OfficeFurnitureTransactionService.Store(State, _selectedId, IsFurnitureInUse)
                : OfficeFurnitureTransactionService.Sell(
                    State, _confirmationCommandId, _selectedId, IsFurnitureInUse);
            HandleCommand(result, _confirmation == Confirmation.Store
                ? "보관함으로 옮겼습니다"
                : $"판매 완료 · {Won(result.RefundedWon)} 환급", before);
        }

        private void HandleCommand(
            OfficeFurnitureCommandResult result,
            string success,
            BuildStateSnapshot before)
        {
            if (!result.Success)
            {
                Say(result.Message);
                _confirmation = Confirmation.None;
                return;
            }
            ClearPending();
            ClearVisuals();
            _runtime.ApplyLayout(State.OfficeGrid);
            var after = new BuildStateSnapshot(State);
            DiagnosticStateMutationCount++;
            DiagnosticLastMutationInstanceId = result.InstanceId ?? string.Empty;
            PlacedOfficeFurniture placed = State.OfficeGrid.Furniture.FirstOrDefault(item =>
                string.Equals(item.FurnitureId, result.InstanceId, StringComparison.Ordinal));
            string anchor = "none";
            if (placed != null &&
                _runtime.World.FurniturePresenter.TryGetSemanticRoot(
                    result.InstanceId,
                    out Transform semanticRoot) &&
                semanticRoot != null)
            {
                Vector3 expected = _runtime.World.Presenter.SubcellAnchorWorld(placed.PlacementAnchor);
                anchor =
                    "origin=" + placed.Origin.X + ":" + placed.Origin.Y +
                    " anchor2=" + placed.PlacementAnchor.X2 + ":" + placed.PlacementAnchor.Y2 +
                    " expected=" + expected.ToString("F6") +
                    " rendered=" + semanticRoot.position.ToString("F6") +
                    " anchorError=" + Vector3.Distance(expected, semanticRoot.position).ToString("F8");
            }
            Debug.Log(
                "OFFICE_BUILD_STATE_MUTATION | instance=" + result.InstanceId +
                " charged=" + result.ChargedWon +
                " refunded=" + result.RefundedWon +
                " cash=" + before.Cash + "->" + after.Cash +
                " ledger=" + before.Ledger + "->" + after.Ledger +
                " inventory=" + before.Inventory + "->" + after.Inventory +
                " furniture=" + before.Furniture + "->" + after.Furniture +
                " editable=" + before.EditableFurniture + "->" + after.EditableFurniture +
                " gridHash=" + before.GridHash + "->" + after.GridHash +
                " " + anchor);
            Say(success);
        }

        private bool IsFurnitureInUse(string instanceId)
        {
            if (_runtime?.World == null) return true;
            if (_runtime.World.Interactions?.ActiveHandles.Any(handle =>
                    string.Equals(handle.FurnitureId, instanceId, StringComparison.Ordinal)) == true) return true;
            OfficeSeatSlot seat = Grid.SeatSlots.FirstOrDefault(item =>
                string.Equals(item.ChairFurnitureId, instanceId, StringComparison.Ordinal) ||
                string.Equals(item.WorkSurfaceFurnitureId, instanceId, StringComparison.Ordinal));
            foreach (OfficeRuntimeAgent actor in _runtime.Actors)
            {
                if (actor == null) continue;
                if (string.Equals(actor.ActiveInteractionFurnitureId, instanceId, StringComparison.Ordinal)) return true;
                if (seat != null && string.Equals(actor.ActiveSeatId, seat.SeatId, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private string NextInstanceId(string definitionId)
        {
            string prefix = "furn-" + definitionId + "-" + State.Time.ElapsedMinutes + "-";
            string candidate;
            do candidate = prefix + (++_instanceSequence).ToString("D4");
            while (Inventory.Find(candidate) != null);
            return candidate;
        }

        private void RefreshVisuals()
        {
            ClearVisuals();
            if (_previewEdit == null) return;
            bool valid = _previewEdit.Success && _previewMessage.Length == 0;
            Color tint = valid
                ? new Color(0.28f, 0.92f, 0.45f, 0.48f)
                : new Color(0.96f, 0.25f, 0.22f, 0.52f);
            string targetId = _pendingSource == PendingSource.Purchase
                ? "__purchase_preview__"
                : (_pendingSource == PendingSource.Stored ? _pendingStoredInstanceId : _selectedId);
            if (_previewEdit.Success)
            {
                foreach (OfficeGridCoordinate cell in GroupCells(_previewEdit.Grid, targetId)) DrawCell(cell, tint);
            }
            else
            {
                OfficeFurnitureDefinition definition = OfficeFurnitureCatalog.Find(TargetDefinitionId());
                if (definition != null)
                {
                    OfficeGridCoordinate footprint = definition.FootprintFor(_previewRotation);
                    for (int y = 0; y < footprint.Y; y++)
                    for (int x = 0; x < footprint.X; x++)
                    {
                        var cell = new OfficeGridCoordinate(_previewOrigin.X + x, _previewOrigin.Y + y);
                        if (Grid.Contains(cell)) DrawCell(cell, tint);
                    }
                }
            }
            if (_pendingSource != PendingSource.None || HasPendingChange()) DrawGhost(valid);
        }

        private string TargetDefinitionId()
        {
            if (_pendingSource != PendingSource.None) return _pendingDefinitionId;
            return Inventory?.Find(_selectedId)?.DefinitionId ?? string.Empty;
        }

        private IEnumerable<OfficeGridCoordinate> GroupCells(OfficeGrid grid, string furnitureId)
        {
            PlacedOfficeFurniture item = grid.Furniture.FirstOrDefault(value =>
                string.Equals(value.FurnitureId, furnitureId, StringComparison.Ordinal));
            if (item == null) return Array.Empty<OfficeGridCoordinate>();
            OfficeSeatSlot owner = grid.SeatSlots.FirstOrDefault(seat =>
                string.Equals(seat.ChairFurnitureId, furnitureId, StringComparison.Ordinal) ||
                string.Equals(seat.WorkSurfaceFurnitureId, furnitureId, StringComparison.Ordinal));
            if (owner == null) return OfficeLayoutEditRules.FootprintCells(item);
            var result = new List<OfficeGridCoordinate> { owner.ApproachCell };
            foreach (PlacedOfficeFurniture part in grid.Furniture)
                if (string.Equals(part.FurnitureId, owner.ChairFurnitureId, StringComparison.Ordinal) ||
                    string.Equals(part.FurnitureId, owner.WorkSurfaceFurnitureId, StringComparison.Ordinal))
                    result.AddRange(OfficeLayoutEditRules.FootprintCells(part));
            return result;
        }

        private void DrawGhost(bool valid)
        {
            OfficeFurnitureVisualCatalog catalog = _runtime.World.FurniturePresenter.VisualCatalog;
            if (!OfficeBuildFurnitureVisualLibrary.TryResolve(
                    catalog, TargetDefinitionId(), _previewRotation,
                    out OfficeFurnitureVisualDefinition visual, out bool flipX)) return;
            OfficeFurnitureDefinition definition = OfficeFurnitureCatalog.Require(TargetDefinitionId());
            OfficeGridCoordinate footprint = definition.FootprintFor(_previewRotation);
            OfficeGridSubcellAnchor anchor = PlacedOfficeFurniture.DefaultPlacementAnchor(
                _previewOrigin, footprint.X, footprint.Y);
            _ghost = new GameObject("OfficeBuildGhost");
            _ghost.transform.SetParent(transform, false);
            _ghost.transform.position = _runtime.World.Presenter.SubcellAnchorWorld(anchor);
            SpriteRenderer renderer = _ghost.AddComponent<SpriteRenderer>();
            renderer.sprite = visual.BaseSprite;
            renderer.flipX = flipX;
            renderer.sortingLayerName = "Default";
            renderer.sortingOrder = 30001;
            renderer.color = valid
                ? new Color(0.45f, 1f, 0.58f, 0.62f)
                : new Color(1f, 0.38f, 0.34f, 0.62f);
        }

        private void DrawCell(OfficeGridCoordinate cell, Color color)
        {
            EnsureCellSprite();
            var marker = new GameObject("OfficeBuildFootprint");
            marker.transform.SetParent(transform, false);
            marker.transform.position = _runtime.World.Presenter.CellCenterWorld(cell);
            SpriteRenderer renderer = marker.AddComponent<SpriteRenderer>();
            renderer.sprite = _cellSprite;
            renderer.color = color;
            renderer.sortingOrder = 30000;
            _overlay.Add(marker);
        }

        private void ClearVisuals()
        {
            foreach (GameObject marker in _overlay) if (marker != null) Destroy(marker);
            _overlay.Clear();
            if (_ghost != null) Destroy(_ghost);
            _ghost = null;
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
            Color clear = new Color(1f, 1f, 1f, 0f);
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float edge = Mathf.Abs(x - width * 0.5f) / (width * 0.5f) +
                             Mathf.Abs(y - height * 0.5f) / (height * 0.5f);
                texture.SetPixel(x, y, edge <= 1f
                    ? (edge >= 0.86f ? Color.white : new Color(1f, 1f, 1f, 0.62f))
                    : clear);
            }
            texture.Apply();
            _cellSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f), OfficeGridTilemapPresenter.PixelsPerUnit);
            _cellSprite.hideFlags = HideFlags.HideAndDontSave;
        }

        private void InvalidatePreview() => _previewSignature = string.Empty;

        private void Say(string message)
        {
            _toast = message ?? string.Empty;
            _toastUntil = Time.unscaledTime + 4f;
        }

        private Rect PanelRect()
        {
            float width = _skin.Round(440);
            float height = Mathf.Min(Screen.height - _skin.Round(32), _skin.Round(850));
            return new Rect(Screen.width - width - _skin.Round(16), _skin.Round(16), width, height);
        }

        private bool IsPointerOverUi(Vector3 mousePosition)
        {
            Vector2 point = new Vector2(mousePosition.x, Screen.height - mousePosition.y);
            return PanelRect().Contains(point) || (_confirmation != Confirmation.None && ConfirmRect().Contains(point));
        }

        private Rect ConfirmRect()
        {
            float width = _skin.Round(390);
            float height = _skin.Round(210);
            return new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
        }

        private void OnGUI()
        {
            if (!IsOpen) return;
            _skin.EnsureBuilt();
            Rect panel = PanelRect();
            GUI.DrawTexture(new Rect(panel.x + 4, panel.y + 6, panel.width, panel.height), _skin.ShadowTexture);
            GUI.Box(panel, GUIContent.none, _skin.PanelStyle);
            GUI.Box(new Rect(panel.x, panel.y, panel.width, _skin.Round(46)), "  사무실 관리 · 구매/배치", _skin.HeaderStyle);
            float pad = _skin.Round(14);
            float x = panel.x + pad;
            float y = panel.y + _skin.Round(58);
            float width = panel.width - pad * 2;
            float line = _skin.Round(24);
            long cash = State.Company.CashWon;
            GUI.Label(new Rect(x, y, width * 0.45f, line), "현재 회사 자금", _skin.BodyStyle);
            GUI.Label(new Rect(x + width * 0.45f, y, width * 0.55f, line), Won(cash), _skin.ValueStyle);
            y += line + _skin.Round(6);

            if (Button(new Rect(x, y, _skin.Round(105), _skin.Round(34)),
                    "분류: " + CategoryNames[_categoryIndex], true))
                _categoryIndex = (_categoryIndex + 1) % CategoryCycle.Length;
            GUI.Label(new Rect(x + _skin.Round(116), y, width - _skin.Round(116), _skin.Round(34)),
                "가격은 2000년 KRW 기준 · 게임 배율 25%", _skin.HintStyle);
            y += _skin.Round(42);

            float detailsHeight = _skin.Round(190);
            float catalogHeight = Mathf.Max(_skin.Round(180), panel.yMax - y - detailsHeight - pad);
            Rect scrollRect = new Rect(x, y, width, catalogHeight);
            List<OfficeFurnitureDefinition> definitions = OfficeFurnitureCatalog.Purchasable
                .Where(item => !CategoryCycle[_categoryIndex].HasValue ||
                               item.Category == CategoryCycle[_categoryIndex].Value)
                .ToList();
            float rowHeight = _skin.Round(82);
            Rect view = new Rect(0, 0, width - _skin.Round(18), definitions.Count * rowHeight);
            _catalogScroll = GUI.BeginScrollView(scrollRect, _catalogScroll, view);
            for (int index = 0; index < definitions.Count; index++)
                DrawCatalogRow(new Rect(0, index * rowHeight, view.width, rowHeight - _skin.Round(5)), definitions[index]);
            GUI.EndScrollView();
            y += catalogHeight + _skin.Round(8);
            DrawDetails(new Rect(x, y, width, panel.yMax - y - pad));

            if (_confirmation != Confirmation.None) DrawConfirmation();
            if (_toast.Length > 0 && Time.unscaledTime < _toastUntil)
            {
                Vector2 size = _skin.ToastStyle.CalcSize(new GUIContent(_toast));
                GUI.Label(new Rect((Screen.width - size.x) * 0.5f,
                    Screen.height - _skin.Round(84), size.x, size.y), _toast, _skin.ToastStyle);
            }
        }

        private void DrawCatalogRow(Rect rect, OfficeFurnitureDefinition definition)
        {
            GUI.Box(rect, GUIContent.none, _skin.ChipStyle);
            Rect icon = new Rect(rect.x + _skin.Round(6), rect.y + _skin.Round(6),
                _skin.Round(66), rect.height - _skin.Round(12));
            DrawSprite(icon, OfficeBuildFurnitureVisualLibrary.Thumbnail(
                _runtime.World.FurniturePresenter.VisualCatalog, definition.DefinitionId, definition.DesiredFacing));
            float x = icon.xMax + _skin.Round(8);
            float textWidth = rect.xMax - x - _skin.Round(92);
            GUI.Label(new Rect(x, rect.y + _skin.Round(7), textWidth, _skin.Round(22)),
                definition.KoreanDisplayName, _skin.TitleStyle);
            long price = OfficeFurnitureEconomyConfig.GameplayPrice(definition.PurchasePriceWon);
            GUI.Label(new Rect(x, rect.y + _skin.Round(31), textWidth, _skin.Round(20)),
                $"{Won(price)} · {definition.BaseWidth}×{definition.BaseHeight} · {CapabilityText(definition)}",
                _skin.HintStyle);
            int owned = Inventory.CountOwned(definition.DefinitionId);
            int placed = Inventory.CountPlaced(definition.DefinitionId);
            GUI.Label(new Rect(x, rect.y + _skin.Round(52), textWidth, _skin.Round(19)),
                $"보유 {owned} / 배치 {placed}", _skin.HintStyle);
            float bx = rect.xMax - _skin.Round(84);
            if (Button(new Rect(bx, rect.y + _skin.Round(8), _skin.Round(78), _skin.Round(30)), "구매", true))
                BeginPurchase(definition);
            OfficeFurnitureInstanceState stored = Inventory.Instances.FirstOrDefault(item =>
                item.PlacementState == OfficeFurniturePlacementState.Stored &&
                string.Equals(item.DefinitionId, definition.DefinitionId, StringComparison.Ordinal));
            if (Button(new Rect(bx, rect.y + _skin.Round(44), _skin.Round(78), _skin.Round(30)),
                    "보관 배치", stored != null)) BeginStored(stored);
        }

        private void DrawDetails(Rect rect)
        {
            GUI.Box(rect, GUIContent.none, _skin.ChipStyle);
            float pad = _skin.Round(10);
            float x = rect.x + pad;
            float y = rect.y + pad;
            float width = rect.width - pad * 2;
            OfficeFurnitureDefinition definition = OfficeFurnitureCatalog.Find(TargetDefinitionId());
            if (definition == null)
            {
                GUI.Label(new Rect(x, y, width, _skin.Round(24)), "가구를 선택하거나 카탈로그에서 구매하세요", _skin.BodyStyle);
                GUI.Label(new Rect(x, y + _skin.Round(29), width, _skin.Round(42)),
                    "선택/집기 · 미리보기 · 타일 중심 스냅 · R 90° 회전\nESC/우클릭 취소 · 확정 전에는 차감 없음",
                    _skin.HintStyle);
                if (Button(new Rect(x, rect.yMax - _skin.Round(42), width, _skin.Round(34)), "뒤로가기", true)) Close();
                return;
            }

            GUI.Label(new Rect(x, y, width, _skin.Round(24)), definition.KoreanDisplayName, _skin.TitleStyle);
            y += _skin.Round(26);
            long price = OfficeFurnitureEconomyConfig.GameplayPrice(definition.PurchasePriceWon);
            long after = _pendingSource == PendingSource.Purchase ? State.Company.CashWon - price : State.Company.CashWon;
            GUI.Label(new Rect(x, y, width, _skin.Round(20)),
                $"{definition.BaseWidth}×{definition.BaseHeight} · {CapabilityText(definition)} · 유지비 {Won(definition.DailyMaintenanceWon)}/일",
                _skin.HintStyle);
            y += _skin.Round(22);
            GUI.Label(new Rect(x, y, width, _skin.Round(22)),
                _pendingSource == PendingSource.Purchase
                    ? $"구매 {Won(price)} → 확정 후 잔액 {Won(after)}"
                    : $"R 90° 회전 · 현재 방향 {_previewRotation}",
                _skin.BodyStyle);
            y += _skin.Round(24);
            bool valid = PreviewValid;
            GUI.Label(new Rect(x, y, width, _skin.Round(38)),
                valid ? "● 유효한 위치" : "● " + (_previewMessage.Length > 0 ? _previewMessage : "위치 또는 회전을 변경하세요"),
                valid ? _skin.ValueStyle : _skin.HintStyle);
            y += _skin.Round(40);
            float gap = _skin.Round(6);
            float third = (width - gap * 2) / 3f;
            if (Button(new Rect(x, y, third, _skin.Round(34)), "확정", valid)) ConfirmPreview();
            if (Button(new Rect(x + third + gap, y, third, _skin.Round(34)), "취소", true)) ClearPending();
            bool hasPlaced = _selectedId.Length > 0;
            if (Button(new Rect(x + (third + gap) * 2, y, third, _skin.Round(34)), "보관", hasPlaced))
                _confirmation = Confirmation.Store;
            y += _skin.Round(40);
            if (Button(new Rect(x, y, width, _skin.Round(34)), "판매", hasPlaced, danger: true))
            {
                _confirmation = Confirmation.Sell;
                _confirmationCommandId = "office-furniture-sell:" + _selectedId + ":" + State.Time.ElapsedMinutes;
            }
        }

        private void DrawConfirmation()
        {
            Rect rect = ConfirmRect();
            GUI.DrawTexture(new Rect(rect.x + 4, rect.y + 6, rect.width, rect.height), _skin.ShadowTexture);
            GUI.Box(rect, GUIContent.none, _skin.PanelStyle);
            GUI.Box(new Rect(rect.x, rect.y, rect.width, _skin.Round(44)),
                _confirmation == Confirmation.Sell ? "  판매 확인" : "  보관 확인", _skin.HeaderStyle);
            OfficeFurnitureInstanceState instance = Inventory.Find(_selectedId);
            OfficeFurnitureDefinition definition = instance == null ? null : OfficeFurnitureCatalog.Find(instance.DefinitionId);
            long refund = instance == null || definition == null ? 0 :
                OfficeFurnitureEconomyConfig.ResaleValue(instance.PurchaseBasisWon, definition.ResaleRateBasisPoints);
            string text = _confirmation == Confirmation.Sell
                ? $"{definition?.KoreanDisplayName}\n판매 환급 {Won(refund)} · 구매 basis {Won(instance?.PurchaseBasisWon ?? 0)}"
                : $"{definition?.KoreanDisplayName}\n회사 자금 변화 없이 보관함으로 이동합니다.";
            GUI.Label(new Rect(rect.x + _skin.Round(18), rect.y + _skin.Round(60),
                rect.width - _skin.Round(36), _skin.Round(70)), text, _skin.BodyStyle);
            float width = (rect.width - _skin.Round(54)) * 0.5f;
            float y = rect.yMax - _skin.Round(54);
            if (Button(new Rect(rect.x + _skin.Round(18), y, width, _skin.Round(36)), "확인", true,
                    danger: _confirmation == Confirmation.Sell)) ConfirmDestructive();
            if (Button(new Rect(rect.x + _skin.Round(36) + width, y, width, _skin.Round(36)), "취소", true))
                _confirmation = Confirmation.None;
        }

        private void DrawSprite(Rect rect, Sprite sprite)
        {
            if (sprite == null || sprite.texture == null) return;
            Rect textureRect = sprite.textureRect;
            Rect uv = new Rect(textureRect.x / sprite.texture.width, textureRect.y / sprite.texture.height,
                textureRect.width / sprite.texture.width, textureRect.height / sprite.texture.height);
            GUI.DrawTextureWithTexCoords(rect, sprite.texture, uv, true);
        }

        private bool Button(Rect rect, string label, bool enabled, bool danger = false)
        {
            GUIStyle style = !enabled ? _skin.DisabledButtonStyle :
                (danger ? _skin.DangerButtonStyle : _skin.ButtonStyle);
            return GUI.Button(rect, label, style) && enabled;
        }

        private static string CapabilityText(OfficeFurnitureDefinition definition)
        {
            if (definition.Capabilities == OfficeFurnitureCapability.None) return "장식/구획";
            return definition.NeedCapabilityTag.Length > 0 ? definition.NeedCapabilityTag : definition.Capabilities.ToString();
        }

        private static string Won(long value) => "₩" + value.ToString("N0");
    }
}
