using UnityEngine;
using UnityEngine.UI;

namespace FamilyCompany.Presentation.Unity.MainNavigation
{
    /// <summary>Grid columns use the actual available rect, never a desktop-sized fixed cell width.</summary>
    [RequireComponent(typeof(GridLayoutGroup))]
    public sealed class CasualResponsiveGrid : MonoBehaviour
    {
        public int Columns = 2;
        public float CellHeightPixels = 166f;
        public float GapPixels = 14f;
        private void LateUpdate()
        {
            var grid = GetComponent<GridLayoutGroup>();
            var rect = (RectTransform)transform;
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return; // Detached / being destroyed, no layout consumer remains.
            var scale = Mathf.Max(.01f, canvas.scaleFactor);
            var columns = Mathf.Max(1, Columns);
            var gap = GapPixels / scale;
            var width = Mathf.Max(0, rect.rect.width - grid.padding.horizontal);
            var size = new Vector2(Mathf.Max(1, (width - gap * (columns - 1)) / columns), CellHeightPixels / scale);
            if (grid.cellSize == size && grid.spacing == new Vector2(gap, gap) &&
                grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount && grid.constraintCount == columns) return;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            grid.cellSize = size;
            grid.spacing = new Vector2(gap, gap);
            LayoutRebuilder.MarkLayoutForRebuild(rect);
        }
    }
}
