using System.Collections.Generic;
using UnityEngine;

namespace HoneyBeeRush.Core
{
    [System.Serializable]
    public struct ColorEntry
    {
        public int index;
        public string name;
        public Color color;
        public int family;
    }

    public sealed class ColorCatalog : ScriptableObject
    {
        public const string UnknownName = "Unknown";

        public ColorEntry[] entries;

        private Dictionary<int, int> _slotByIndex;
        private ColorEntry[] _mappedSource;
        private int _mappedLength = -1;

        public int Count => entries != null ? entries.Length : 0;

        public static Color MissingColor => new Color(1f, 0f, 1f, 1f);

        public Color ColorAt(int index)
        {
            return TryGetSlot(index, out int slot) ? entries[slot].color : MissingColor;
        }

        public string NameAt(int index)
        {
            if (!TryGetSlot(index, out int slot))
            {
                return UnknownName;
            }

            string name = entries[slot].name;
            return string.IsNullOrEmpty(name) ? UnknownName : name;
        }

        public int FamilyAt(int index)
        {
            return TryGetSlot(index, out int slot) ? entries[slot].family : -1;
        }

        public bool Contains(int index)
        {
            return TryGetSlot(index, out _);
        }

        public bool TryGetEntry(int index, out ColorEntry entry)
        {
            if (TryGetSlot(index, out int slot))
            {
                entry = entries[slot];
                return true;
            }

            entry = default;
            return false;
        }

        public void RebuildIndex()
        {
            if (_slotByIndex == null)
            {
                _slotByIndex = new Dictionary<int, int>(Count);
            }
            else
            {
                _slotByIndex.Clear();
            }

            if (entries != null)
            {
                for (int i = 0; i < entries.Length; i++)
                {
                    _slotByIndex[entries[i].index] = i;
                }
            }

            _mappedSource = entries;
            _mappedLength = Count;
        }

        private bool TryGetSlot(int index, out int slot)
        {
            EnsureIndex();
            return _slotByIndex.TryGetValue(index, out slot);
        }

        private void EnsureIndex()
        {
            if (_slotByIndex != null && ReferenceEquals(_mappedSource, entries) && _mappedLength == Count)
            {
                return;
            }

            RebuildIndex();
        }

        private void OnValidate()
        {
            RebuildIndex();
        }
    }
}
