using System;
using System.Collections.Generic;
using HoneyBeeRush.Gameplay.Core;
using HoneyBeeRush.View;
using Lean.Pool;
using UnityEngine;

namespace HoneyBeeRush.Gameplay.Domain
{
    public sealed class SlotBoardController : MonoBehaviour
    {
        [SerializeField] private SlotController m_slotPrefab;
        [SerializeField] private Transform m_slotRoot;

        private readonly List<SlotController> m_slots = new List<SlotController>();
        private LayoutConfig m_layout;

        public event Action<SlotController> SlotOccupied;
        public event Action<SlotController> SlotFreed;

        public IReadOnlyList<SlotController> Slots => m_slots;
        public int SlotCount => m_slots.Count;

        public int FreeCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < m_slots.Count; i++)
                {
                    if (m_slots[i] != null && m_slots[i].IsFree) count++;
                }
                return count;
            }
        }

        public bool AllOccupied => FreeCount == 0;

        public void Build(int slotCount, LayoutConfig layout)
        {
            ClearSlots();
            m_layout = layout;

            if (m_slotRoot == null)
            {
                var rootGo = new GameObject("SlotRoot");
                m_slotRoot = rootGo.transform;
                m_slotRoot.SetParent(transform, false);
            }

            slotCount = Mathf.Max(1, slotCount);
            for (int i = 0; i < slotCount; i++)
            {
                SlotController slot = LeanPool.Spawn(m_slotPrefab, m_slotRoot);
                Vector3 p = layout != null ? layout.SlotPosition(i, slotCount) : new Vector3(i * 1.4f, -3.05f, -0.55f);
                slot.transform.localPosition = p;
                slot.Initialize(i);
                if (layout != null) slot.ApplyVisualRadius(layout.SlotRadius);

                slot.Occupied += OnSlotOccupiedInternal;
                slot.Freed += OnSlotFreedInternal;

                m_slots.Add(slot);
            }
        }

        public void ApplyLayoutSizes()
        {
            if (m_layout == null) return;

            for (int i = 0; i < m_slots.Count; i++)
            {
                if (m_slots[i] != null) m_slots[i].ApplyVisualRadius(m_layout.SlotRadius);
            }
        }

        public void ClearSlots()
        {
            for (int i = 0; i < m_slots.Count; i++)
            {
                SlotController slot = m_slots[i];
                if (slot != null && slot.gameObject.activeSelf)
                {
                    slot.Occupied -= OnSlotOccupiedInternal;
                    slot.Freed -= OnSlotFreedInternal;
                    DespawnCratesUnder(slot);
                    LeanPool.Despawn(slot.gameObject);
                }
            }
            m_slots.Clear();
        }

        private static void DespawnCratesUnder(SlotController slot)
        {
            CrateController[] crates = slot.GetComponentsInChildren<CrateController>(true);
            for (int i = 0; i < crates.Length; i++)
            {
                CrateController crate = crates[i];
                if (crate != null && crate.gameObject.activeSelf) LeanPool.Despawn(crate.gameObject);
            }
        }

        public void PlayFullWarning()
        {
            for (int i = 0; i < m_slots.Count; i++)
            {
                if (m_slots[i] != null) m_slots[i].PlayFullWarning();
            }
        }

        public SlotController FreeSlot()
        {
            for (int i = 0; i < m_slots.Count; i++)
            {
                if (m_slots[i] != null && m_slots[i].IsFree)
                {
                    return m_slots[i];
                }
            }
            return null;
        }

        public SlotController SlotByIndex(int index)
        {
            if (index < 0 || index >= m_slots.Count) return null;
            return m_slots[index];
        }

        public void SetSlotCount(int targetCount)
        {
            if (targetCount < 1) targetCount = 1;
            if (m_layout == null) return;

            while (m_slots.Count < targetCount)
            {
                int idx = m_slots.Count;
                SlotController slot = LeanPool.Spawn(m_slotPrefab, m_slotRoot);
                slot.Initialize(idx);
                slot.Occupied += OnSlotOccupiedInternal;
                slot.Freed += OnSlotFreedInternal;
                m_slots.Add(slot);
            }

            while (m_slots.Count > targetCount)
            {
                SlotController last = m_slots[m_slots.Count - 1];
                if (last.IsOccupied) break;
                m_slots.RemoveAt(m_slots.Count - 1);
                last.Occupied -= OnSlotOccupiedInternal;
                last.Freed -= OnSlotFreedInternal;
                LeanPool.Despawn(last.gameObject);
            }

            for (int i = 0; i < m_slots.Count; i++)
            {
                Vector3 p = m_layout.SlotPosition(i, m_slots.Count);
                m_slots[i].SetIndex(i);
                m_slots[i].PlayReflowTo(p, 0.25f);
            }
        }

        private void OnSlotOccupiedInternal(SlotController slot)
        {
            SlotOccupied?.Invoke(slot);
        }

        private void OnSlotFreedInternal(SlotController slot)
        {
            SlotFreed?.Invoke(slot);
        }

        private void OnDestroy()
        {
            ClearSlots();
        }
    }
}
