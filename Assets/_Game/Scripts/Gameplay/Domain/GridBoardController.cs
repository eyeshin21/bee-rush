using System;
using System.Collections.Generic;
using HoneyBeeRush.Core;
using HoneyBeeRush.Data;
using HoneyBeeRush.Gameplay.Core;
using HoneyBeeRush.View;
using Lean.Pool;
using UnityEngine;

namespace HoneyBeeRush.Gameplay.Domain
{
    public sealed partial class GridBoardController : MonoBehaviour
    {
        [SerializeField] private GridCellController m_cellPrefab;
        [SerializeField] private CrateController m_cratePrefab;
        [SerializeField] private Transform m_cellRoot;

        private readonly List<GridCellController> m_cells = new List<GridCellController>();
        private readonly Dictionary<GridPosition, GridCellController> m_cellLookup = new Dictionary<GridPosition, GridCellController>();
        private readonly List<CrateController> m_releasedCrates = new List<CrateController>();
        private readonly Dictionary<int, CrateController> m_byCrateId = new Dictionary<int, CrateController>();
        private readonly List<CrateController> m_takeBuffer = new List<CrateController>(8);

        private GridCellBoardData m_boardData;
        private LayoutConfig m_layout;
        private Func<bool> m_canSelectCrate;
        private Action<CrateController> m_onCrateSelected;
        private int m_remainingCount;
        private float m_boardScale = 1f;

        public event Action<CrateController> CrateSelected;
        public event Action<CrateController> CrateRevealed;
        public event Action<CrateController> CrateDelayCleared;

        public IReadOnlyList<GridCellController> Cells => m_cells;
        public int ColumnCount => m_boardData != null ? m_boardData.width : 0;
        public int RowCount => m_boardData != null ? m_boardData.height : 0;
        public int Total => m_remainingCount;
        public bool NoCratesInColumns => m_remainingCount == 0;
        public float BoardScale => m_boardScale;
        public Transform CellRoot => m_cellRoot;

        public IReadOnlyList<CrateController> Crates
        {
            get
            {
                var list = new List<CrateController>(m_cells.Count);
                for (int i = 0; i < m_cells.Count; i++)
                {
                    var c = m_cells[i].CrateController;
                    if (c != null && !c.InSlot)
                    {
                        list.Add(c);
                    }
                }
                return list;
            }
        }

        public IReadOnlyList<CrateController> PickableCrates
        {
            get
            {
                var list = new List<CrateController>(m_cells.Count);
                for (int i = 0; i < m_cells.Count; i++)
                {
                    var c = m_cells[i].CrateController;
                    if (c != null && c.IsPickable && !c.InSlot && !c.DelayLocked)
                    {
                        list.Add(c);
                    }
                }
                return list;
            }
        }

        public void Build(LevelData level, ColorMaterialMapping materials, LayoutConfig layout, Func<bool> canSelect = null, Action<CrateController> onSelected = null)
        {
            if (level == null)
            {
                ClearBoard();
                return;
            }

            GridCellBoardData data = level.GetOrCreateGridBoardData();
            Build(data, materials, layout, canSelect, onSelected);
        }

        public void Build(GridCellBoardData gridCellBoardData, ColorMaterialMapping materials, LayoutConfig layout, Func<bool> canSelect, Action<CrateController> onSelected)
        {
            ClearBoard();

            m_boardData = gridCellBoardData;
            m_layout = layout;
            m_canSelectCrate = canSelect;
            m_onCrateSelected = onSelected;

            if (m_cellRoot == null)
            {
                var rootGo = new GameObject("CellRoot");
                m_cellRoot = rootGo.transform;
                m_cellRoot.SetParent(transform, false);
            }

            ApplyBoardFit(m_boardData != null ? m_boardData.width : 1);

            if (m_boardData == null || m_cellPrefab == null)
            {
                return;
            }

            int nextId = 0;
            for (int row = 0; row < m_boardData.height; row++)
            {
                for (int col = 0; col < m_boardData.width; col++)
                {
                    var pos = new GridPosition(row, col);
                    GridCellDefinition def = m_boardData.GetCellAt(pos);

                    GridCellController cell = LeanPool.Spawn(m_cellPrefab, m_cellRoot);
                    InjectCellPrefab(cell);
                    cell.transform.localPosition = ToLocalPosition(pos, m_boardData.width);
                    cell.Initialize(pos, def, m_boardData, materials, OnCrateTappedInternal, nextId);

                    if (cell.CrateController != null)
                    {
                        CrateController crate = cell.CrateController;
                        if (m_layout != null) crate.ApplyVisualRadius(m_layout.CrateRadius);
                        crate.RevealedChanged += OnCrateRevealedInternal;
                        crate.DelayCleared += OnCrateDelayClearedInternal;
                        m_byCrateId[nextId] = crate;
                        nextId++;
                        m_remainingCount++;
                    }

                    m_cells.Add(cell);
                    m_cellLookup[pos] = cell;
                }
            }

            RefreshPickableCrates();
        }

        private void InjectCellPrefab(GridCellController cell)
        {
            if (m_cratePrefab != null)
            {
                var f = typeof(GridCellController).GetField("m_cratePrefab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (f != null && f.GetValue(cell) == null)
                {
                    f.SetValue(cell, m_cratePrefab);
                }
            }
        }

        public Vector3 ToLocalPosition(GridPosition position, int width)
        {
            float pitch = m_layout != null ? m_layout.GridCellPitch : 1.10f;
            float centeredColumn = position.Column - (width - 1) * 0.5f;
            return new Vector3(centeredColumn * pitch, -position.Row * pitch, 0);
        }

        private void ApplyBoardFit(int width)
        {
            m_boardScale = m_layout != null ? m_layout.GridBoardScale(width) : 1f;
            if (m_boardScale <= 0f) m_boardScale = 1f;

            if (m_cellRoot == null) return;

            m_cellRoot.localScale = new Vector3(m_boardScale, m_boardScale, m_boardScale);
            m_cellRoot.localPosition = new Vector3(
                0f,
                m_layout != null ? m_layout.GridBoardTopY : -3.95f,
                m_layout != null ? m_layout.GridBoardZ : -0.50f);
        }

        public void ApplyLayoutSizes()
        {
            if (m_boardData == null || m_cells.Count == 0) return;

            ApplyBoardFit(m_boardData.width);

            for (int i = 0; i < m_cells.Count; i++)
            {
                GridCellController cell = m_cells[i];
                if (cell == null) continue;

                cell.transform.localPosition = ToLocalPosition(cell.GridPosition, m_boardData.width);
                if (m_layout != null && cell.CrateController != null)
                {
                    cell.CrateController.ApplyVisualRadius(m_layout.CrateRadius);
                }
            }
        }

        public void RefreshPickableCrates()
        {
            m_releasedCrates.RemoveAll(c => c == null || c.RemainingBees <= 0);

            for (int i = 0; i < m_cells.Count; i++)
            {
                GridCellController cell = m_cells[i];
                CrateController crate = cell.CrateController;
                if (crate == null || crate.InSlot) continue;

                bool hasPath = HasPathToExit(cell.GridPosition);
                crate.IsPickable = hasPath && !crate.DelayLocked;

                if (crate.IsPickable && crate.Hidden && !crate.Revealed)
                {
                    crate.Reveal();
                }
            }
        }

        public bool HasPathToExit(GridPosition start)
        {
            if (m_boardData == null) return false;

            var queue = new Queue<GridPosition>();
            var visited = new HashSet<GridPosition>();
            queue.Enqueue(start);
            visited.Add(start);

            while (queue.Count > 0)
            {
                GridPosition current = queue.Dequeue();
                if (current.Row == 0 && IsPassable(current, start))
                {
                    return true;
                }

                foreach (GridPosition next in GetNeighbors(current))
                {
                    if (visited.Contains(next) || !IsInside(next) || !IsPassable(next, start))
                    {
                        continue;
                    }

                    visited.Add(next);
                    queue.Enqueue(next);
                }
            }

            return false;
        }

        public IReadOnlyList<Vector3> FindPathToExit(CrateController crate)
        {
            if (crate == null || m_boardData == null)
            {
                return Array.Empty<Vector3>();
            }

            GridPosition start = crate.GridPosition;
            var queue = new Queue<GridPosition>();
            var visited = new HashSet<GridPosition>();
            var parent = new Dictionary<GridPosition, GridPosition>();
            queue.Enqueue(start);
            visited.Add(start);

            while (queue.Count > 0)
            {
                GridPosition current = queue.Dequeue();
                if (current.Row == 0 && IsPassable(current, start))
                {
                    return BuildWorldPath(parent, start, current);
                }

                foreach (GridPosition next in GetNeighbors(current))
                {
                    if (visited.Contains(next) || !IsInside(next) || !IsPassable(next, start))
                    {
                        continue;
                    }

                    visited.Add(next);
                    parent[next] = current;
                    queue.Enqueue(next);
                }
            }

            return new[] { GetWorldPosition(start) };
        }

        private IReadOnlyList<Vector3> BuildWorldPath(Dictionary<GridPosition, GridPosition> parent, GridPosition start, GridPosition exit)
        {
            var positions = new List<GridPosition>();
            GridPosition current = exit;
            while (current != start)
            {
                positions.Add(current);
                current = parent[current];
            }

            positions.Reverse();
            var worldPositions = new List<Vector3>(positions.Count);
            for (int i = 0; i < positions.Count; i++)
            {
                worldPositions.Add(GetWorldPosition(positions[i]));
            }
            return worldPositions;
        }

        public Vector3 GetWorldPosition(GridPosition position)
        {
            Transform root = m_cellRoot != null ? m_cellRoot : transform;
            return root.TransformPoint(ToLocalPosition(position, m_boardData != null ? m_boardData.width : 1));
        }

        private bool IsPassable(GridPosition position, GridPosition start)
        {
            if (position == start) return true;
            if (m_boardData == null) return true;

            GridCellDefinition definition = m_boardData.GetCellAt(position);
            if (definition == null || definition.cellType == GridCellType.Empty) return true;

            if (definition.cellType == GridCellType.StandardCrate ||
                definition.cellType == GridCellType.MysteryCrate ||
                definition.cellType == GridCellType.FrozenCrate)
            {
                return !HasActiveCrateAt(position);
            }

            return false;
        }

        public bool HasActiveCrateAt(GridPosition position)
        {
            GridCellController cell = GetCell(position);
            if (cell == null) return false;

            CrateController crate = cell.CrateController;
            return crate != null && !crate.InSlot;
        }

        private bool IsInside(GridPosition position)
        {
            return m_boardData != null && m_boardData.IsInside(position);
        }

        private static IEnumerable<GridPosition> GetNeighbors(GridPosition position)
        {
            yield return new GridPosition(position.Row - 1, position.Column);
            yield return new GridPosition(position.Row, position.Column - 1);
            yield return new GridPosition(position.Row, position.Column + 1);
            yield return new GridPosition(position.Row + 1, position.Column);
        }

        public GridCellController GetCell(GridPosition position)
        {
            GridCellController cell;
            return m_cellLookup.TryGetValue(position, out cell) ? cell : null;
        }

        public CrateController CrateById(int crateId)
        {
            CrateController c;
            m_byCrateId.TryGetValue(crateId, out c);
            return c;
        }

        public bool CanLaunch(CrateController crate, int freeSlots)
        {
            if (crate == null || freeSlots <= 0) return false;
            if (!crate.IsPickable || crate.InSlot || crate.DelayLocked) return false;

            if (crate.LinkGroup <= 0)
            {
                return freeSlots >= 1;
            }

            int needed = 0;
            for (int i = 0; i < m_cells.Count; i++)
            {
                CrateController other = m_cells[i].CrateController;
                if (other == null || other.LinkGroup != crate.LinkGroup) continue;
                if (!other.IsPickable || other.InSlot || other.DelayLocked) return false;
                needed++;
            }

            return needed > 0 && needed <= freeSlots;
        }

        public bool NeedsMoreSlots(CrateController crate, int freeSlots)
        {
            if (crate == null) return false;
            if (!crate.IsPickable || crate.InSlot || crate.DelayLocked) return false;

            if (crate.LinkGroup <= 0) return freeSlots < 1;

            int needed = 0;
            for (int i = 0; i < m_cells.Count; i++)
            {
                CrateController other = m_cells[i].CrateController;
                if (other == null || other.LinkGroup != crate.LinkGroup) continue;
                if (!other.IsPickable || other.InSlot || other.DelayLocked) return false;
                needed++;
            }

            return needed > 0 && needed > freeSlots;
        }

        public bool AnyPickableLaunchable(int freeSlots)
        {
            for (int i = 0; i < m_cells.Count; i++)
            {
                CrateController crate = m_cells[i].CrateController;
                if (crate == null || !crate.IsPickable || crate.InSlot) continue;
                if (CanLaunch(crate, freeSlots)) return true;
            }
            return false;
        }

        public bool AnyProductive(BoardController board)
        {
            if (board == null) return false;
            for (int i = 0; i < m_cells.Count; i++)
            {
                CrateController crate = m_cells[i].CrateController;
                if (crate == null || !crate.IsPickable || crate.InSlot) continue;
                if (board.HasTargetableColor(crate.ColorType)) return true;
            }
            return false;
        }

        public List<CrateController> TakeForLaunch(CrateController crate)
        {
            m_takeBuffer.Clear();
            if (crate == null) return m_takeBuffer;

            if (crate.LinkGroup <= 0)
            {
                DetachAndRegister(crate);
                m_takeBuffer.Add(crate);
            }
            else
            {
                int group = crate.LinkGroup;
                for (int i = 0; i < m_cells.Count; i++)
                {
                    CrateController other = m_cells[i].CrateController;
                    if (other != null && other.LinkGroup == group && other.IsPickable && !other.InSlot)
                    {
                        DetachAndRegister(other);
                        m_takeBuffer.Add(other);
                    }
                }
            }

            return m_takeBuffer;
        }

        private void DetachAndRegister(CrateController crate)
        {
            GridCellController cell = GetCell(crate.GridPosition);
            if (cell != null)
            {
                cell.DetachCrateController();
            }
            crate.SetInSlot(true);
            if (m_remainingCount > 0) m_remainingCount--;
            if (!m_releasedCrates.Contains(crate))
            {
                m_releasedCrates.Add(crate);
            }
        }

        public void OnMoveCommitted()
        {
            for (int i = 0; i < m_cells.Count; i++)
            {
                CrateController crate = m_cells[i].CrateController;
                if (crate != null && crate.DelayLocked)
                {
                    crate.DecrementDelay();
                }
            }

            RefreshPickableCrates();
        }

        public void TrySelectCrate(int crateId)
        {
            CrateController crate = CrateById(crateId);
            if (crate == null || !crate.IsPickable || crate.InSlot || crate.DelayLocked)
            {
                return;
            }

            if (m_canSelectCrate != null && !m_canSelectCrate.Invoke())
            {
                return;
            }

            m_onCrateSelected?.Invoke(crate);
            CrateSelected?.Invoke(crate);
        }

        public void ShakeCrate(int crateId)
        {
            CrateController crate = CrateById(crateId);
            if (crate != null)
            {
                crate.PlayShake();
            }
        }

        public int RaycastCrateId(Ray ray)
        {
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 100f))
            {
                CrateController crate = hit.collider.GetComponentInParent<CrateController>();
                if (crate != null) return crate.CrateId;
            }
            return -1;
        }

        public bool AreAllCratesCleared()
        {
            if (m_cells.Count == 0) return false;
            for (int i = 0; i < m_cells.Count; i++)
            {
                CrateController c = m_cells[i].CrateController;
                if (c != null && !c.InSlot && c.RemainingBees > 0) return false;
            }
            for (int i = 0; i < m_releasedCrates.Count; i++)
            {
                CrateController c = m_releasedCrates[i];
                if (c != null && c.RemainingBees > 0) return false;
            }
            return true;
        }

        public bool AreAllCratesPicked()
        {
            if (m_cells.Count == 0) return false;
            for (int i = 0; i < m_cells.Count; i++)
            {
                CrateController c = m_cells[i].CrateController;
                if (c != null && !c.InSlot) return false;
            }
            return true;
        }

        public void ClearBoard()
        {
            for (int i = 0; i < m_cells.Count; i++)
            {
                GridCellController cell = m_cells[i];
                if (cell != null)
                {
                    CrateController crate = cell.CrateController;
                    if (crate != null)
                    {
                        crate.Tapped -= OnCrateTappedInternal;
                        crate.RevealedChanged -= OnCrateRevealedInternal;
                        crate.DelayCleared -= OnCrateDelayClearedInternal;
                    }
                    cell.Clear();
                    LeanPool.Despawn(cell.gameObject);
                }
            }
            m_cells.Clear();
            m_cellLookup.Clear();

            for (int i = 0; i < m_releasedCrates.Count; i++)
            {
                CrateController crate = m_releasedCrates[i];
                if (crate != null && crate.gameObject.activeSelf)
                {
                    LeanPool.Despawn(crate.gameObject);
                }
            }
            m_releasedCrates.Clear();

            m_byCrateId.Clear();
            m_remainingCount = 0;

            if (m_cellRoot != null)
            {
                for (int i = m_cellRoot.childCount - 1; i >= 0; i--)
                {
                    LeanPool.Despawn(m_cellRoot.GetChild(i).gameObject);
                }
            }
        }

        private void OnCrateTappedInternal(CrateController crate)
        {
            if (crate != null)
            {
                TrySelectCrate(crate.CrateId);
            }
        }

        private void OnCrateRevealedInternal(CrateController crate)
        {
            CrateRevealed?.Invoke(crate);
        }

        private void OnCrateDelayClearedInternal(CrateController crate)
        {
            CrateDelayCleared?.Invoke(crate);
        }

        private void OnDestroy()
        {
            ClearBoard();
        }
    }
}
