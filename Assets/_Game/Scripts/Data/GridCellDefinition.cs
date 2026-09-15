using System;
using HoneyBeeRush.Gameplay.Core;

namespace HoneyBeeRush.Data
{
    [Serializable]
    public sealed class GridCellDefinition
    {
        public GridPosition position;
        public GridCellType cellType;
        public CrateDef crate;
    }
}
