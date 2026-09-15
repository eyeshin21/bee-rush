using System.Collections.Generic;
using UnityEngine;
using HoneyBeeRush.Core;

namespace HoneyBeeRush.Data
{
    [System.Serializable]
    public sealed class CellDef
    {
        public int q;
        public int r;
        public int layer;
        public ColorType colorType;
        public int nectar;
        public bool locked;
    }

    [System.Serializable]
    public sealed class CrateDef
    {
        public ColorType colorType;
        public int beeCount;
        public int column;
        public bool hidden;
        public int linkGroup;
        public int releaseDelay;
    }

    [CreateAssetMenu(menuName = "Honey Bee Rush/Level Data")]
    public sealed class LevelData : ScriptableObject
    {
        public const int CellFormatPlanar = 0;
        public const int CellFormatVolume = 1;

        public int index = 1;
        public DifficultyTier difficulty = DifficultyTier.Easy;
        public string notes = string.Empty;
        public float winRateTarget = 0.5f;
        public float gridScaleMultiplier = 1f;
        public Vector2 gridPositionOffset = Vector2.zero;
        public float gridRotation;
        public int cellFormat = CellFormatPlanar;
        public float boardViewYaw;
        public float boardViewPitch;
        public List<CellDef> cells = new List<CellDef>();
        public List<CrateDef> crates = new List<CrateDef>();
        public int launchSlots = 5;
        public int gridBoardHeight = 20;
        public GridCellBoardData gridBoardData;

        public void Normalize()
        {
            index = Mathf.Max(1, index);
            launchSlots = Mathf.Max(1, launchSlots);
            winRateTarget = Mathf.Clamp01(winRateTarget);

            if (gridScaleMultiplier <= 0f) gridScaleMultiplier = 1f;
            if (gridBoardHeight <= 0) gridBoardHeight = 20;
            if (cells == null) cells = new List<CellDef>();
            if (crates == null) crates = new List<CrateDef>();
        }

        public int TotalNectar
        {
            get
            {
                int sum = 0;
                if (cells != null)
                {
                    for (int i = 0; i < cells.Count; i++)
                    {
                        CellDef cell = cells[i];
                        if (cell != null)
                        {
                            sum += cell.nectar;
                        }
                    }
                }

                return sum;
            }
        }

        public int TotalBees
        {
            get
            {
                int sum = 0;
                if (crates != null)
                {
                    for (int i = 0; i < crates.Count; i++)
                    {
                        CrateDef crate = crates[i];
                        if (crate != null)
                        {
                            sum += crate.beeCount;
                        }
                    }
                }

                return sum;
            }
        }

        public int ColumnCount
        {
            get
            {
                if (crates == null || crates.Count == 0)
                {
                    return 0;
                }

                int max = -1;
                for (int i = 0; i < crates.Count; i++)
                {
                    CrateDef crate = crates[i];
                    if (crate != null && crate.column > max)
                    {
                        max = crate.column;
                    }
                }

                return max < 0 ? 0 : max + 1;
            }
        }

        public int CellCount => cells != null ? cells.Count : 0;

        public bool IsVolumetric => cellFormat == CellFormatVolume;

        public int LayerCount
        {
            get
            {
                if (cells == null || cells.Count == 0) return 0;

                int min = int.MaxValue;
                int max = int.MinValue;
                for (int i = 0; i < cells.Count; i++)
                {
                    CellDef cell = cells[i];
                    if (cell == null) continue;
                    if (cell.layer < min) min = cell.layer;
                    if (cell.layer > max) max = cell.layer;
                }

                return max < min ? 0 : max - min + 1;
            }
        }

        public int CrateCount => crates != null ? crates.Count : 0;

        public GridCellBoardData GetOrCreateGridBoardData()
        {
            if (gridBoardData != null && gridBoardData.gridCells != null && gridBoardData.gridCells.Count > 0)
            {
                gridBoardData.InvalidateIndex();
                return gridBoardData;
            }

            Gameplay.Config.TuningConfig tuning = Gameplay.Config.TuningConfig.Instance;
            int defaultWidth = tuning != null ? tuning.DefaultGridWidth : 15;
            int defaultHeight = tuning != null ? tuning.DefaultGridHeight : 20;
            int rowBudget = tuning != null ? tuning.GridRowBudget : 8;

            List<List<CrateDef>> columns = BuildCrateColumns(rowBudget, defaultWidth);

            int usedWidth = columns.Count;
            int usedHeight = 0;
            for (int i = 0; i < columns.Count; i++)
            {
                if (columns[i].Count > usedHeight) usedHeight = columns[i].Count;
            }

            var data = new GridCellBoardData();
            data.width = tuning != null
                ? tuning.ResolveBoardWidth(usedWidth)
                : Mathf.Max(defaultWidth, Mathf.Max(1, usedWidth));
            data.height = tuning != null
                ? tuning.ResolveBoardHeight(gridBoardHeight, usedHeight)
                : Mathf.Max(gridBoardHeight > 0 ? gridBoardHeight : defaultHeight, Mathf.Max(1, usedHeight));

            int columnOffset = Mathf.Max(0, (data.width - usedWidth) / 2);
            var occupied = new HashSet<HoneyBeeRush.Gameplay.Core.GridPosition>();

            for (int column = 0; column < columns.Count; column++)
            {
                List<CrateDef> stack = columns[column];
                for (int row = 0; row < stack.Count; row++)
                {
                    CrateDef def = stack[row];
                    var position = new HoneyBeeRush.Gameplay.Core.GridPosition(row, columnOffset + column);

                    data.AddCell(new GridCellDefinition
                    {
                        position = position,
                        cellType = def.hidden
                            ? GridCellType.MysteryCrate
                            : (def.releaseDelay > 0 ? GridCellType.FrozenCrate : GridCellType.StandardCrate),
                        crate = def
                    });

                    occupied.Add(position);
                }
            }

            for (int row = 0; row < data.height; row++)
            {
                for (int column = 0; column < data.width; column++)
                {
                    var position = new HoneyBeeRush.Gameplay.Core.GridPosition(row, column);
                    if (occupied.Contains(position)) continue;

                    data.AddCell(new GridCellDefinition
                    {
                        position = position,
                        cellType = GridCellType.DeadCell,
                        crate = null
                    });
                }
            }

            return data;
        }

        private List<List<CrateDef>> BuildCrateColumns(int rowBudget, int maxColumns)
        {
            var columns = new List<List<CrateDef>>();
            if (crates == null || crates.Count == 0) return columns;

            var byColumn = new SortedDictionary<int, List<CrateDef>>();
            for (int i = 0; i < crates.Count; i++)
            {
                CrateDef def = crates[i];
                if (def == null) continue;

                List<CrateDef> stack;
                if (!byColumn.TryGetValue(def.column, out stack))
                {
                    stack = new List<CrateDef>();
                    byColumn.Add(def.column, stack);
                }
                stack.Add(def);
            }

            if (byColumn.Count == 0) return columns;

            int authoredDepth = 0;
            var ordered = new List<CrateDef>(crates.Count);
            foreach (KeyValuePair<int, List<CrateDef>> pair in byColumn)
            {
                if (pair.Value.Count > authoredDepth) authoredDepth = pair.Value.Count;
                ordered.AddRange(pair.Value);
            }

            if (authoredDepth <= rowBudget)
            {
                foreach (KeyValuePair<int, List<CrateDef>> pair in byColumn)
                {
                    columns.Add(pair.Value);
                }
                return columns;
            }

            int needed = Mathf.CeilToInt(ordered.Count / (float)Mathf.Max(1, rowBudget));
            int columnCount = Mathf.Clamp(Mathf.Max(byColumn.Count, needed), 1, Mathf.Max(1, maxColumns));
            int depth = Mathf.CeilToInt(ordered.Count / (float)columnCount);

            for (int column = 0; column < columnCount; column++)
            {
                var stack = new List<CrateDef>(depth);
                int start = column * depth;
                for (int row = 0; row < depth && start + row < ordered.Count; row++)
                {
                    stack.Add(ordered[start + row]);
                }
                if (stack.Count > 0) columns.Add(stack);
            }

            return columns;
        }
    }
}
