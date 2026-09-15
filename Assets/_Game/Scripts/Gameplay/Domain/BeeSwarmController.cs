using System.Collections.Generic;
using HoneyBeeRush.Bees;
using HoneyBeeRush.Data;
using HoneyBeeRush.Gameplay.Core;
using HoneyBeeRush.View;
using Lean.Pool;
using UnityEngine;

namespace HoneyBeeRush.Gameplay.Domain
{
    public sealed class BeeSwarmController : MonoBehaviour
    {
        [SerializeField] private BeeController m_beePrefab;
        [SerializeField] private Transform m_swarmRoot;

        private readonly Dictionary<int, BeeController> m_activeBees = new Dictionary<int, BeeController>(512);
        private readonly List<int> m_despawnList = new List<int>(64);
        private BeeSim m_sim;
        private ColorMaterialMapping m_materials;
        private LayoutConfig m_layout;

        public void Init(BeeSim sim, ColorMaterialMapping materials, LayoutConfig layout)
        {
            m_sim = sim;
            m_materials = materials;
            m_layout = layout;

            if (m_swarmRoot == null)
            {
                var rootGo = new GameObject("SwarmRoot");
                m_swarmRoot = rootGo.transform;
                m_swarmRoot.SetParent(transform, false);
            }

            if (sim != null)
            {
                sim.BeeDespawned += OnSimBeeDespawned;
            }
        }

        public void Sync()
        {
            if (m_sim == null) return;

            int cap = m_sim.Capacity;
            for (int id = 0; id < cap; id++)
            {
                if (!m_sim.IsAlive(id))
                {
                    if (m_activeBees.ContainsKey(id))
                    {
                        DespawnBee(id);
                    }
                    continue;
                }

                BeeController bee;
                if (!m_activeBees.TryGetValue(id, out bee))
                {
                    bee = LeanPool.Spawn(m_beePrefab, m_swarmRoot);
                    bee.Initialize(id, m_sim.ColorOf(id), m_sim.SlotIndex(id), m_materials);
                    if (m_layout != null) bee.ApplyVisualLength(m_layout.BeeLength);
                    m_activeBees[id] = bee;
                }

                bee.SetCarrying(m_sim.HasCargo(id));
                bee.SetPose(m_sim.Position(id), m_sim.Rotation(id));
                bee.SetWingPhase(m_sim.WingPhase(id));
                if (m_layout != null) bee.ApplyVisualLength(m_layout.BeeLength * m_sim.VisualScale(id));
            }
        }

        public void ApplyLayoutSizes()
        {
            if (m_layout == null) return;

            foreach (var kvp in m_activeBees)
            {
                if (kvp.Value != null) kvp.Value.ApplyVisualLength(m_layout.BeeLength);
            }
        }

        private void OnSimBeeDespawned(int id)
        {
            DespawnBee(id);
        }

        private void DespawnBee(int id)
        {
            BeeController bee;
            if (m_activeBees.TryGetValue(id, out bee))
            {
                m_activeBees.Remove(id);
                if (bee != null && bee.gameObject.activeSelf)
                {
                    LeanPool.Despawn(bee.gameObject);
                }
            }
        }

        public void ClearSwarm()
        {
            m_despawnList.Clear();
            foreach (var kvp in m_activeBees)
            {
                m_despawnList.Add(kvp.Key);
            }
            for (int i = 0; i < m_despawnList.Count; i++)
            {
                DespawnBee(m_despawnList[i]);
            }
            m_activeBees.Clear();
        }

        private void OnDestroy()
        {
            if (m_sim != null)
            {
                m_sim.BeeDespawned -= OnSimBeeDespawned;
            }
            ClearSwarm();
        }
    }
}
