using System;
using System.Collections;
using HoneyBeeRush.Core;
using HoneyBeeRush.Data;
using HoneyBeeRush.View;
using Lean.Pool;
using UnityEngine;

namespace HoneyBeeRush.Gameplay.Core
{
    public sealed class CellController : MonoBehaviour, IPoolable
    {
        [SerializeField] private MeshFilter m_meshFilter;
        [SerializeField] private MeshRenderer m_renderer;
        [SerializeField] private GameObject m_lockRoot;
        [SerializeField] private MeshRenderer m_lockRenderer;

        public int CellId { get; private set; }
        public int Id => CellId;
        public HexCoord3 Coord { get; private set; }
        public ColorType ColorType { get; private set; }
        public int Nectar { get; private set; }
        public int InitialNectar { get; private set; }
        public bool Locked { get; private set; }
        public bool Alive { get; private set; }
        public bool Exposed { get; private set; }
        public byte OpenFaces { get; private set; }

        public bool IsTargetable => Alive && !Locked && Exposed;

        public Vector3 HomeLocalPos { get; private set; }
        public Vector3 HomeScale { get; private set; }

        public void ApplyLayout(Vector3 localPosition, Vector3 scale)
        {
            HomeLocalPos = localPosition;
            HomeScale = scale;
            transform.localPosition = localPosition;
            transform.localScale = scale;
        }
        private MaterialPropertyBlock m_mpb;
        private ColorMaterialMapping m_materials;
        private Coroutine m_activeRoutine;
        private bool m_cullEnclosed;

        private static readonly Vector3[] s_boundsCorners = new Vector3[8];

        public event Action<CellController> Died;
        public event Action<CellController> Unlocked;
        public event Action<CellController> ExposureChanged;

        private void Awake()
        {
            if (m_mpb == null) m_mpb = new MaterialPropertyBlock();
            if (m_meshFilter == null) m_meshFilter = GetComponent<MeshFilter>();
            if (m_renderer == null) m_renderer = GetComponent<MeshRenderer>();
        }

        public void Initialize(int id, HexCoord3 coord, ColorType colorType, int nectar, bool locked, Vector3 localPos, Vector3 scale, ColorMaterialMapping materials)
        {
            CellId = id;
            Coord = coord;
            ColorType = colorType;
            m_materials = materials;
            InitialNectar = Mathf.Max(0, nectar);
            Nectar = InitialNectar;
            Locked = locked;
            Alive = InitialNectar > 0;
            Exposed = false;
            OpenFaces = 0;

            HomeLocalPos = localPos;
            HomeScale = scale;

            transform.localPosition = localPos;
            transform.localScale = scale;

            if (m_lockRoot != null)
            {
                m_lockRoot.SetActive(locked);
            }

            ApplyBodyMaterial();
            ApplyEmission(0f);
            RefreshVisibility();
        }

        public void SetExposure(byte openFaces)
        {
            OpenFaces = openFaces;
            bool exposed = Alive && openFaces != 0;
            bool changed = Exposed != exposed;
            Exposed = exposed;
            RefreshVisibility();
            if (changed) ExposureChanged?.Invoke(this);
        }

        public void SetEnclosedCulling(bool enabled)
        {
            m_cullEnclosed = enabled;
            RefreshVisibility();
        }

        public void RefreshVisibility()
        {
            bool visible = !m_cullEnclosed || !Alive || Exposed;
            if (m_renderer != null) m_renderer.enabled = visible;
            if (m_lockRenderer != null) m_lockRenderer.enabled = visible;
        }

        public float MeasureLocalDepth(Vector3 rootScale)
        {
            MeshFilter filter = m_meshFilter != null ? m_meshFilter : GetComponentInChildren<MeshFilter>(true);
            if (filter == null) return 0f;

            Mesh mesh = filter.sharedMesh;
            if (mesh == null) return 0f;

            Matrix4x4 toRoot = Matrix4x4.Scale(rootScale) * transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            Bounds bounds = mesh.bounds;
            Vector3 c = bounds.center;
            Vector3 e = bounds.extents;

            int index = 0;
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        s_boundsCorners[index++] = new Vector3(c.x + e.x * x, c.y + e.y * y, c.z + e.z * z);
                    }
                }
            }

            float minZ = float.MaxValue;
            float maxZ = float.MinValue;
            for (int i = 0; i < s_boundsCorners.Length; i++)
            {
                float pz = toRoot.MultiplyPoint3x4(s_boundsCorners[i]).z;
                if (pz < minZ) minZ = pz;
                if (pz > maxZ) maxZ = pz;
            }

            return Mathf.Max(0f, maxZ - minZ);
        }

        public bool ApplyDrain()
        {
            if (!Alive || Nectar <= 0) return false;

            Nectar--;
            PlayDrainPulse();

            if (Nectar <= 0)
            {
                Nectar = 0;
                KillCell();
            }

            return true;
        }

        public void Unlock()
        {
            if (!Locked) return;
            Locked = false;
            if (m_lockRoot != null) m_lockRoot.SetActive(false);
            ApplyBodyMaterial();
            ApplyEmission(0f);
            Unlocked?.Invoke(this);
        }

        public void KillCell()
        {
            if (!Alive) return;
            Alive = false;
            Nectar = 0;
            RefreshVisibility();
            Died?.Invoke(this);
            PlayDeathAnimation();
        }

        public void PlayPopIn(float delay, float duration)
        {
            StopActiveRoutine();
            m_activeRoutine = StartCoroutine(PopInRoutine(delay, duration));
        }

        private IEnumerator PopInRoutine(float delay, float duration)
        {
            transform.localScale = Vector3.zero;
            if (delay > 0f) yield return new WaitForSeconds(delay);

            float elapsed = 0f;
            duration = Mathf.Max(0.01f, duration);
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float s = 1f + Mathf.Sin(t * Mathf.PI) * 0.2f;
                transform.localScale = HomeScale * (t * s);
                yield return null;
            }
            transform.localScale = HomeScale;
            m_activeRoutine = null;
        }

        public void PlayDrainPulse()
        {
            StopActiveRoutine();
            m_activeRoutine = StartCoroutine(DrainPulseRoutine());
        }

        private IEnumerator DrainPulseRoutine()
        {
            float elapsed = 0f;
            float duration = 0.22f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float wave = Mathf.Sin(t * Mathf.PI);
                float scaleMul = 1f + wave * 0.12f;
                float emission = wave * 0.45f;
                transform.localScale = new Vector3(HomeScale.x * scaleMul, HomeScale.y * scaleMul, HomeScale.z * (1f - wave * 0.08f));
                ApplyEmission(emission);
                yield return null;
            }
            transform.localScale = HomeScale;
            ApplyEmission(0f);
            m_activeRoutine = null;
        }

        private void PlayDeathAnimation()
        {
            StopActiveRoutine();
            m_activeRoutine = StartCoroutine(DeathRoutine());
        }

        private IEnumerator DeathRoutine()
        {
            float elapsed = 0f;
            float duration = 0.18f;
            Vector3 startScale = transform.localScale;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
                ApplyEmission((1f - t) * 0.6f);
                yield return null;
            }
            transform.localScale = Vector3.zero;
            ApplyEmission(0f);
            gameObject.SetActive(false);
            m_activeRoutine = null;
        }

        private void ApplyBodyMaterial()
        {
            if (m_renderer == null || m_materials == null) return;
            m_renderer.sharedMaterial = Locked ? m_materials.CellLocked(ColorType) : m_materials.Cell(ColorType);

            if (m_lockRenderer != null)
            {
                m_lockRenderer.sharedMaterial = m_materials.LockBody(ColorType);
            }
        }

        private void ApplyEmission(float emission)
        {
            if (m_renderer == null) return;

            if (emission <= 0f)
            {
                m_renderer.SetPropertyBlock(null);
                return;
            }

            if (m_mpb == null) m_mpb = new MaterialPropertyBlock();
            m_mpb.Clear();
            m_mpb.SetFloat(ShaderIds.Emission, emission);
            m_renderer.SetPropertyBlock(m_mpb);
        }

        private void StopActiveRoutine()
        {
            if (m_activeRoutine != null)
            {
                StopCoroutine(m_activeRoutine);
                m_activeRoutine = null;
            }
        }

        public void OnSpawn()
        {
            StopActiveRoutine();
            if (m_mpb == null) m_mpb = new MaterialPropertyBlock();
            gameObject.SetActive(true);
        }

        public void OnDespawn()
        {
            StopActiveRoutine();
            Died = null;
            Unlocked = null;
            ExposureChanged = null;
            if (m_lockRoot != null) m_lockRoot.SetActive(false);
            ApplyEmission(0f);
            m_materials = null;
            ColorType = ColorType.None;
            OpenFaces = 0;
            Exposed = false;
            m_cullEnclosed = false;
            if (m_renderer != null) m_renderer.enabled = true;
            if (m_lockRenderer != null) m_lockRenderer.enabled = true;
            transform.localScale = Vector3.one;
        }

        private void OnDisable()
        {
            StopActiveRoutine();
        }
    }
}
