using System.Collections.Generic;
using AYellowpaper.SerializedCollections;
using UnityEngine;

namespace HoneyBeeRush.Data
{
    public enum ColorMaterialSet
    {
        CrateActive = 0,
        CrateLocked = 1,
        Cell = 2,
        CellLocked = 3,
        Bee = 4,
        LockBody = 5,
        Shard = 6,
        CrateInactive = 7
    }

    [CreateAssetMenu(fileName = "ColorMaterialMapping", menuName = "Honey Bee Rush/Color Material Mapping")]
    public sealed class ColorMaterialMapping : ScriptableObject
    {
        [SerializeField] private SerializedDictionary<ColorType, Material> m_crateActive = new SerializedDictionary<ColorType, Material>();
        [SerializeField] private SerializedDictionary<ColorType, Material> m_crateLocked = new SerializedDictionary<ColorType, Material>();
        [SerializeField] private SerializedDictionary<ColorType, Material> m_cell = new SerializedDictionary<ColorType, Material>();
        [SerializeField] private SerializedDictionary<ColorType, Material> m_cellLocked = new SerializedDictionary<ColorType, Material>();
        [SerializeField] private SerializedDictionary<ColorType, Material> m_bee = new SerializedDictionary<ColorType, Material>();
        [SerializeField] private SerializedDictionary<ColorType, Material> m_lockBody = new SerializedDictionary<ColorType, Material>();
        [SerializeField] private SerializedDictionary<ColorType, Material> m_shard = new SerializedDictionary<ColorType, Material>();
        [SerializeField] private SerializedDictionary<ColorType, Material> m_crateInactive = new SerializedDictionary<ColorType, Material>();

        [SerializeField] private Material m_crateHidden;
        [SerializeField] private Material m_fallback;

        private HashSet<int> m_reportedMisses;

        public Material CrateHidden => m_crateHidden != null ? m_crateHidden : m_fallback;

        public Material CrateActive(ColorType colorType) => Resolve(ColorMaterialSet.CrateActive, colorType);

        public Material CrateLocked(ColorType colorType) => Resolve(ColorMaterialSet.CrateLocked, colorType);

        public Material Cell(ColorType colorType) => Resolve(ColorMaterialSet.Cell, colorType);

        public Material CellLocked(ColorType colorType) => Resolve(ColorMaterialSet.CellLocked, colorType);

        public Material Bee(ColorType colorType) => Resolve(ColorMaterialSet.Bee, colorType);

        public Material LockBody(ColorType colorType) => Resolve(ColorMaterialSet.LockBody, colorType);

        public Material Shard(ColorType colorType) => Resolve(ColorMaterialSet.Shard, colorType);

        public Material CrateInactive(ColorType colorType) => Resolve(ColorMaterialSet.CrateInactive, colorType);

        private SerializedDictionary<ColorType, Material> SetOf(ColorMaterialSet set)
        {
            switch (set)
            {
                case ColorMaterialSet.CrateActive: return m_crateActive;
                case ColorMaterialSet.CrateLocked: return m_crateLocked;
                case ColorMaterialSet.Cell: return m_cell;
                case ColorMaterialSet.CellLocked: return m_cellLocked;
                case ColorMaterialSet.Bee: return m_bee;
                case ColorMaterialSet.LockBody: return m_lockBody;
                case ColorMaterialSet.Shard: return m_shard;
                case ColorMaterialSet.CrateInactive: return m_crateInactive;
                default: return null;
            }
        }

        private Material Resolve(ColorMaterialSet set, ColorType colorType)
        {
            SerializedDictionary<ColorType, Material> source = SetOf(set);

            Material material;
            if (source != null && source.TryGetValue(colorType, out material) && material != null)
            {
                return material;
            }

            ReportMiss(set, colorType);
            return m_fallback;
        }

        private void ReportMiss(ColorMaterialSet set, ColorType colorType)
        {
            int key = (int)set * 1000 + (int)colorType;
            if (m_reportedMisses == null)
            {
                m_reportedMisses = new HashSet<int>();
            }

            if (!m_reportedMisses.Add(key))
            {
                return;
            }

            Debug.LogWarning("[ColorMaterialMapping] " + name + " has no material for " + colorType + " in set " + set + ".", this);
        }

#if UNITY_EDITOR
        public void EditorAssign(ColorMaterialSet set, ColorType colorType, Material material)
        {
            SerializedDictionary<ColorType, Material> target = SetOf(set);
            if (target == null) return;
            target[colorType] = material;
        }

        public bool EditorTryGet(ColorMaterialSet set, ColorType colorType, out Material material)
        {
            material = null;
            SerializedDictionary<ColorType, Material> source = SetOf(set);
            return source != null && source.TryGetValue(colorType, out material) && material != null;
        }

        public void EditorClear(ColorMaterialSet set)
        {
            SerializedDictionary<ColorType, Material> target = SetOf(set);
            if (target != null) target.Clear();
        }

        public void EditorSetCrateHidden(Material material)
        {
            m_crateHidden = material;
        }

        public void EditorSetFallback(Material material)
        {
            m_fallback = material;
        }
#endif
    }
}
