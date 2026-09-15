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
    public sealed class CrateQueueController : MonoBehaviour
    {
        [SerializeField] private CrateController m_cratePrefab;
        [SerializeField] private Transform m_queueRoot;

        private readonly List<List<CrateController>> m_columns = new List<List<CrateController>>();
        private readonly Dictionary<int, CrateController> m_byCrateId = new Dictionary<int, CrateController>();
        private readonly List<CrateController> m_takeBuffer = new List<CrateController>(8);
        private LayoutConfig m_layout;
        private int m_remainingCount;

        public event Action<CrateController> CrateSelected;
        public event Action<int> ColumnAdvanced;
        public event Action<CrateController> CrateRevealed;
        public event Action<CrateController> CrateDelayCleared;

        public int ColumnCount => m_columns.Count;
        public int Total => m_remainingCount;
        public bool NoCratesInColumns => m_remainingCount == 0;
        public float CrateFlightTime => 0.32f;

        public void Build(LevelData level, ColorMaterialMapping materials, LayoutConfig layout)
        {
            ClearQueue();
            m_layout = layout;

            if (m_queueRoot == null)
            {
                var rootGo = new GameObject("CrateRoot");
                m_queueRoot = rootGo.transform;
                m_queueRoot.SetParent(transform, false);
            }

            int columnCount = level != null ? level.ColumnCount : 0;
            if (columnCount < 0) columnCount = 0;
            for (int i = 0; i < columnCount; i++)
            {
                m_columns.Add(new List<CrateController>());
            }

            if (level != null && level.crates != null)
            {
                int nextId = 0;
                for (int i = 0; i < level.crates.Count; i++)
                {
                    CrateDef def = level.crates[i];
                    if (def == null) continue;

                    while (def.column >= m_columns.Count)
                    {
                        m_columns.Add(new List<CrateController>());
                    }

                    List<CrateController> column = m_columns[def.column];
                    int depth = column.Count;

                    CrateController crate = LeanPool.Spawn(m_cratePrefab, m_queueRoot);
                    crate.Initialize(nextId, def, def.column, depth, materials);
                    crate.LayoutScale = 1f;

                    crate.Tapped += OnCrateTappedInternal;
                    crate.RevealedChanged += OnCrateRevealedInternal;
                    crate.DelayCleared += OnCrateDelayClearedInternal;

                    column.Add(crate);
                    m_byCrateId[nextId] = crate;
                    nextId++;
                    m_remainingCount++;
                }
            }

            for (int c = 0; c < m_columns.Count; c++)
            {
                List<CrateController> col = m_columns[c];
                if (col.Count > 0)
                {
                    col[0].Reveal();
                }
            }

            RefreshQueueLayout(true);
        }

        public void ClearQueue()
        {
            for (int c = 0; c < m_columns.Count; c++)
            {
                List<CrateController> col = m_columns[c];
                for (int i = 0; i < col.Count; i++)
                {
                    CrateController crate = col[i];
                    if (crate != null && crate.gameObject.activeSelf)
                    {
                        crate.Tapped -= OnCrateTappedInternal;
                        crate.RevealedChanged -= OnCrateRevealedInternal;
                        crate.DelayCleared -= OnCrateDelayClearedInternal;
                        LeanPool.Despawn(crate.gameObject);
                    }
                }
            }
            m_columns.Clear();
            m_byCrateId.Clear();
            m_remainingCount = 0;
        }

        public CrateController Front(int column)
        {
            if (column < 0 || column >= m_columns.Count) return null;
            List<CrateController> list = m_columns[column];
            return list.Count > 0 ? list[0] : null;
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
            if (!crate.IsPickable) return false;

            int col = crate.ColumnIndex;
            if (Front(col) != crate) return false;

            if (crate.LinkGroup <= 0)
            {
                return freeSlots >= 1;
            }

            int needed = 0;
            for (int c = 0; c < m_columns.Count; c++)
            {
                CrateController front = Front(c);
                if (front == null || front.LinkGroup != crate.LinkGroup) continue;
                if (!front.IsPickable) return false;
                needed++;
            }

            return needed > 0 && needed <= freeSlots;
        }

        public bool AnyFrontLaunchable(int freeSlots)
        {
            for (int c = 0; c < m_columns.Count; c++)
            {
                CrateController front = Front(c);
                if (front == null) continue;
                if (CanLaunch(front, freeSlots)) return true;
            }
            return false;
        }

        public List<CrateController> TakeForLaunch(CrateController crate)
        {
            m_takeBuffer.Clear();
            if (crate == null) return m_takeBuffer;

            if (crate.LinkGroup <= 0)
            {
                RemoveFrontCrate(crate.ColumnIndex);
                m_takeBuffer.Add(crate);
            }
            else
            {
                int group = crate.LinkGroup;
                for (int c = 0; c < m_columns.Count; c++)
                {
                    CrateController front = Front(c);
                    if (front != null && front.LinkGroup == group && front.IsPickable)
                    {
                        RemoveFrontCrate(c);
                        m_takeBuffer.Add(front);
                    }
                }
            }

            return m_takeBuffer;
        }

        private void RemoveFrontCrate(int column)
        {
            if (column < 0 || column >= m_columns.Count) return;
            List<CrateController> list = m_columns[column];
            if (list.Count == 0) return;

            CrateController front = list[0];
            list.RemoveAt(0);
            if (m_remainingCount > 0) m_remainingCount--;

            for (int i = 0; i < list.Count; i++)
            {
                list[i].SetDepth(i);
            }

            ColumnAdvanced?.Invoke(column);
        }

        public void OnMoveCommitted()
        {
            for (int c = 0; c < m_columns.Count; c++)
            {
                List<CrateController> col = m_columns[c];
                for (int d = 0; d < col.Count; d++)
                {
                    CrateController crate = col[d];
                    if (crate.DelayLocked)
                    {
                        crate.DecrementDelay();
                    }
                }
                if (col.Count > 0)
                {
                    col[0].Reveal();
                }
            }

            RefreshQueueLayout(false);
        }

        public void RefreshQueueLayout(bool instant)
        {
            int cols = m_columns.Count;
            if (m_layout == null) return;

            for (int col = 0; col < cols; col++)
            {
                List<CrateController> list = m_columns[col];
                for (int d = 0; d < list.Count; d++)
                {
                    CrateController c = list[d];
                    bool visible = d < m_layout.queueVisibleRows;
                    c.gameObject.SetActive(visible);
                    if (!visible) continue;

                    Vector3 targetPos = m_layout.QueuePosition(col, cols, d);
                    float scale = d == 0 ? 1f : (d == 1 ? 0.88f : 0.78f);
                    c.LayoutScale = scale;

                    if (instant)
                    {
                        c.transform.localPosition = targetPos;
                        c.transform.localScale = Vector3.one * scale;
                    }
                    else
                    {
                        c.PlaySlideTo(targetPos, scale, 0.22f);
                    }
                }
            }
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

        private void OnCrateTappedInternal(CrateController crate)
        {
            CrateSelected?.Invoke(crate);
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
            ClearQueue();
        }
    }
}
