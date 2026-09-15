using System;
using System.Collections.Generic;
using HoneyBeeRush.Gameplay.Core;

namespace HoneyBeeRush.Data
{
    [Serializable]
    public sealed class GridCellBoardData
    {
        public int width;
        public int height;
        public List<GridCellDefinition> gridCells = new List<GridCellDefinition>();

        [NonSerialized] private Dictionary<GridPosition, GridCellDefinition> m_index;

        public void RebuildIndex()
        {
            if (m_index == null)
            {
                m_index = new Dictionary<GridPosition, GridCellDefinition>(gridCells != null ? gridCells.Count : 0);
            }
            else
            {
                m_index.Clear();
            }

            if (gridCells == null) return;

            for (int i = 0; i < gridCells.Count; i++)
            {
                GridCellDefinition cell = gridCells[i];
                if (cell == null) continue;
                m_index[cell.position] = cell;
            }
        }

        public void InvalidateIndex()
        {
            m_index = null;
        }

        public void AddCell(GridCellDefinition cell)
        {
            if (cell == null) return;
            if (gridCells == null) gridCells = new List<GridCellDefinition>();

            gridCells.Add(cell);
            if (m_index != null) m_index[cell.position] = cell;
        }

        public GridCellDefinition GetCellAt(GridPosition position)
        {
            if (gridCells == null) return null;
            if (m_index == null) RebuildIndex();

            GridCellDefinition def;
            return m_index.TryGetValue(position, out def) ? def : null;
        }

        public bool IsDeadCell(GridPosition position)
        {
            GridCellDefinition def = GetCellAt(position);
            return def != null && def.cellType == GridCellType.DeadCell;
        }

        public bool IsInside(GridPosition position)
        {
            return position.Row >= 0 && position.Row < height && position.Column >= 0 && position.Column < width;
        }
    }
}
