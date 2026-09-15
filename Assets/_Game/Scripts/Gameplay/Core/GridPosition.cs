using System;
using UnityEngine;

namespace HoneyBeeRush.Gameplay.Core
{
    [Serializable]
    public struct GridPosition : IEquatable<GridPosition>, IComparable<GridPosition>
    {
        [SerializeField] private int row;
        [SerializeField] private int column;

        public int Row => row;
        public int Column => column;

        public GridPosition(int row, int column)
        {
            this.row = row;
            this.column = column;
        }

        public int CompareTo(GridPosition other)
        {
            int rowComparison = row.CompareTo(other.row);
            return rowComparison != 0 ? rowComparison : column.CompareTo(other.column);
        }

        public bool Equals(GridPosition other)
        {
            return row == other.row && column == other.column;
        }

        public override bool Equals(object obj)
        {
            return obj is GridPosition other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (row * 397) ^ column;
            }
        }

        public override string ToString()
        {
            return $"({row}, {column})";
        }

        public static bool operator ==(GridPosition left, GridPosition right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GridPosition left, GridPosition right)
        {
            return !left.Equals(right);
        }
    }
}
