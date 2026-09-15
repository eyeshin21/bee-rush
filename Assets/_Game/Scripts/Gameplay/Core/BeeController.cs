using HoneyBeeRush.Data;
using Lean.Pool;
using UnityEngine;

namespace HoneyBeeRush.Gameplay.Core
{
    public sealed class BeeController : MonoBehaviour, IPoolable
    {
        [SerializeField] private Transform m_rig;
        [SerializeField] private GameObject m_beeVisual;
        [SerializeField] private MeshRenderer m_bodyRenderer;
        [SerializeField] private Transform m_wingL;
        [SerializeField] private Transform m_wingR;
        [SerializeField] private MeshRenderer m_wingLRenderer;
        [SerializeField] private MeshRenderer m_wingRRenderer;
        [SerializeField] private int m_bodyMaterialIndex;

        [SerializeField] private GameObject m_carryVisual;
        [SerializeField] private MeshRenderer m_carryBodyRenderer;
        [SerializeField] private MeshRenderer m_carryCargoRenderer;
        [SerializeField] private Transform m_carryWingL;
        [SerializeField] private Transform m_carryWingR;
        [SerializeField] private int m_carryBodyMaterialIndex;

        [SerializeField] private float m_referenceLength = 0.324f;

        private Vector3 m_authoredRigScale;
        private bool m_authoredRigCached;
        private float m_appliedLength = -1f;

        public int BeeId { get; private set; } = -1;
        public ColorType ColorType { get; private set; } = ColorType.None;
        public int SlotIndex { get; private set; } = -1;
        public bool IsCarrying { get; private set; }

        private Material[] m_bodyMaterials;
        private MeshRenderer m_cachedFor;
        private ColorMaterialMapping m_materials;

        private MeshRenderer ActiveBodyRenderer =>
            IsCarrying && m_carryBodyRenderer != null ? m_carryBodyRenderer : m_bodyRenderer;

        private int ActiveBodyMaterialIndex => IsCarrying ? m_carryBodyMaterialIndex : m_bodyMaterialIndex;

        private Transform ActiveWingL => IsCarrying && m_carryWingL != null ? m_carryWingL : m_wingL;

        private Transform ActiveWingR => IsCarrying && m_carryWingR != null ? m_carryWingR : m_wingR;

        public void Initialize(int id, ColorType colorType, int slotIndex, ColorMaterialMapping materials)
        {
            BeeId = id;
            ColorType = colorType;
            SlotIndex = slotIndex;
            m_materials = materials;

            ApplyCarryVisual(false);
            RefreshMaterials();
        }

        public void SetCarrying(bool carrying)
        {
            if (IsCarrying == carrying) return;

            ApplyCarryVisual(carrying);
            RefreshMaterials();
        }

        private void ApplyCarryVisual(bool carrying)
        {
            IsCarrying = carrying && m_carryVisual != null;

            if (m_beeVisual != null) m_beeVisual.SetActive(!IsCarrying);
            if (m_carryVisual != null) m_carryVisual.SetActive(IsCarrying);
        }

        private void RefreshMaterials()
        {
            if (m_materials == null) return;

            ApplyMaterial(m_materials.Bee(ColorType));

            if (IsCarrying && m_carryCargoRenderer != null)
            {
                Material cargo = m_materials.Cell(ColorType);
                if (cargo != null) m_carryCargoRenderer.sharedMaterial = cargo;
            }
        }

        public void ApplyMaterial(Material material)
        {
            MeshRenderer renderer = ActiveBodyRenderer;
            if (renderer == null || material == null) return;

            CacheBodyMaterials(renderer);
            if (m_bodyMaterials == null || m_bodyMaterials.Length == 0) return;

            int index = Mathf.Clamp(ActiveBodyMaterialIndex, 0, m_bodyMaterials.Length - 1);
            if (m_bodyMaterials[index] == material) return;

            m_bodyMaterials[index] = material;
            renderer.sharedMaterials = m_bodyMaterials;
        }

        public void ApplyVisualLength(float length)
        {
            if (m_rig == null) return;

            if (!m_authoredRigCached)
            {
                m_authoredRigScale = m_rig.localScale;
                m_authoredRigCached = true;
            }

            if (m_appliedLength == length) return;
            m_appliedLength = length;

            float reference = m_referenceLength > 0f ? m_referenceLength : 0.324f;
            m_rig.localScale = m_authoredRigScale * (Mathf.Max(0.001f, length) / reference);
        }

        public void SetPose(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
        }

        public void SetWingPhase(float phase)
        {
            float sweep = Mathf.Sin(phase) * 26f;
            Transform left = ActiveWingL;
            Transform right = ActiveWingR;
            if (left != null) left.localRotation = Quaternion.Euler(0f, 0f, sweep);
            if (right != null) right.localRotation = Quaternion.Euler(0f, 0f, -sweep);
        }

        private void CacheBodyMaterials(MeshRenderer renderer)
        {
            if (renderer == null) return;
            if (m_bodyMaterials != null && m_cachedFor == renderer) return;

            m_bodyMaterials = renderer.sharedMaterials;
            m_cachedFor = renderer;
        }

        public void OnSpawn()
        {
            gameObject.SetActive(true);
        }

        public void OnDespawn()
        {
            BeeId = -1;
            ColorType = ColorType.None;
            SlotIndex = -1;
            m_materials = null;
            m_appliedLength = -1f;
            ApplyCarryVisual(false);
        }
    }
}
