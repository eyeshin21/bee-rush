using Lean.Pool;
using UnityEngine;

namespace HoneyBeeRush.Gameplay.Core
{
    public sealed class TileShardController : MonoBehaviour, IPoolable
    {
        [SerializeField] private MeshFilter m_meshFilter;
        [SerializeField] private MeshRenderer m_renderer;

        private Vector3 m_velocity;
        private Vector3 m_spinAxis;
        private float m_spinRate;
        private float m_gravity = 11f;
        private float m_lifeTimer;
        private float m_lifeDuration = 0.62f;
        private Vector3 m_baseScale = Vector3.one * 0.04f;
        private bool m_active;

        private void Awake()
        {
            if (m_meshFilter == null) m_meshFilter = GetComponent<MeshFilter>();
            if (m_renderer == null) m_renderer = GetComponent<MeshRenderer>();
        }

        public void Initialize(Vector3 spawnPos, Vector3 velocity, Material material, float scale)
        {
            transform.position = spawnPos;
            m_velocity = velocity;
            m_baseScale = Vector3.one * scale;
            transform.localScale = m_baseScale;
            m_spinAxis = Random.onUnitSphere;
            m_spinRate = Random.Range(360f, 900f);
            m_lifeTimer = 0f;
            m_lifeDuration = 0.62f * Random.Range(0.75f, 1.2f);
            m_active = true;

            if (m_renderer != null && material != null)
            {
                m_renderer.sharedMaterial = material;
            }
        }

        private void Update()
        {
            if (!m_active) return;

            float dt = Time.deltaTime;
            m_lifeTimer += dt;
            if (m_lifeTimer >= m_lifeDuration)
            {
                m_active = false;
                LeanPool.Despawn(gameObject);
                return;
            }

            m_velocity.y -= m_gravity * dt;
            transform.position += m_velocity * dt;
            transform.Rotate(m_spinAxis, m_spinRate * dt, Space.World);

            float t = 1f - (m_lifeTimer / m_lifeDuration);
            transform.localScale = m_baseScale * Mathf.Clamp01(t * 1.4f);
        }

        public void OnSpawn()
        {
            m_active = true;
            m_lifeTimer = 0f;
            gameObject.SetActive(true);
        }

        public void OnDespawn()
        {
            m_active = false;
        }

        private void OnDisable()
        {
            m_active = false;
        }
    }
}
