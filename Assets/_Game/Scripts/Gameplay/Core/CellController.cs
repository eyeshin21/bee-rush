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
        [SerializeField] private float m_referenceWidth = 0.8227f;
        [SerializeField] private float m_referenceDepth = 0.8227f;
        [SerializeField] private int m_visualDepthAxis = 1;
        [SerializeField] private float m_lockRadiusRatio = 0.43f;
        [SerializeField] private float m_lockStandoff = 0.35f;

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

        public Transform VisualRoot => m_meshFilter != null ? m_meshFilter.transform : null;

        public void ApplyLayout(Vector3 localPosition, Vector3 visualScale, float cellWidth, float cellDepth)
        {
            HomeLocalPos = localPosition;
            HomeScale = Vector3.one;
            transform.localPosition = localPosition;
            transform.localScale = Vector3.one;
            ApplyVisualSizing(visualScale, cellWidth, cellDepth);
        }

        public void ApplyVisualSizing(Vector3 visualScale, float cellWidth, float cellDepth)
        {
            CaptureAuthoredVisual();

            Transform visual = VisualRoot;
            if (visual != null && visual != transform) visual.localScale = visualScale;

            if (m_lockRoot == null) return;

            float ratio = m_lockRadiusRatio > 0f ? m_lockRadiusRatio : 0.43f;
            float standoff = m_lockStandoff > 0f ? m_lockStandoff : 0.35f;
            float lockScale = Mathf.Max(0.0001f, cellWidth * 0.5f * ratio);

            Transform lockTransform = m_lockRoot.transform;
            lockTransform.localScale = new Vector3(lockScale, lockScale, lockScale);
            lockTransform.localPosition = new Vector3(0f, 0f, -(cellDepth * 0.5f + lockScale * standoff));
        }

        public bool MeasureVisualMesh(out float lateralExtent, out float depthExtent, out int depthAxis)
        {
            lateralExtent = 0f;
            depthExtent = 0f;
            depthAxis = 1;

            MeshFilter filter = m_meshFilter != null ? m_meshFilter : GetComponentInChildren<MeshFilter>(true);
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null) return false;

            Vector3[] vertices = mesh.vertices;
            if (vertices == null || vertices.Length == 0) return false;

            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            var radius = new Vector3();
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 v = vertices[i];
                for (int axis = 0; axis < 3; axis++)
                {
                    if (v[axis] < min[axis]) min[axis] = v[axis];
                    if (v[axis] > max[axis]) max[axis] = v[axis];

                    float p1 = v[(axis + 1) % 3];
                    float p2 = v[(axis + 2) % 3];
                    float r = Mathf.Sqrt(p1 * p1 + p2 * p2);
                    if (r > radius[axis]) radius[axis] = r;
                }
            }

            depthAxis = 0;
            for (int axis = 1; axis < 3; axis++)
            {
                if (radius[axis] < radius[depthAxis]) depthAxis = axis;
            }

            depthExtent = max[depthAxis] - min[depthAxis];
            lateralExtent = float.MaxValue;
            for (int axis = 0; axis < 3; axis++)
            {
                if (axis == depthAxis) continue;
                float extent = max[axis] - min[axis];
                if (extent < lateralExtent) lateralExtent = extent;
            }

            return lateralExtent > 0.000001f && depthExtent > 0.000001f;
        }

        public static Vector3 ComputeVisualScale(CellController prefab, float targetWidth, float targetDepth)
        {
            if (prefab == null) return Vector3.one;

            float meshWidth;
            float meshDepth;
            int depthAxis;
            if (prefab.MeasureVisualMesh(out meshWidth, out meshDepth, out depthAxis))
            {
                float measured = Mathf.Abs(targetWidth) / meshWidth;
                Vector3 scale = new Vector3(measured, measured, measured);
                scale[depthAxis] = Mathf.Abs(targetDepth) / meshDepth;
                return scale;
            }

            prefab.CaptureAuthoredVisual();
            float planar = prefab.m_referenceWidth > 0f ? targetWidth / prefab.m_referenceWidth : 1f;
            float depth = prefab.m_referenceDepth > 0f ? targetDepth / prefab.m_referenceDepth : 1f;
            int axis = Mathf.Clamp(prefab.m_visualDepthAxis, 0, 2);

            Vector3 fallback = prefab.m_authoredVisualScale * planar;
            fallback[axis] = prefab.m_authoredVisualScale[axis] * depth;
            return fallback;
        }
        private Vector3 m_authoredVisualScale = Vector3.one;
        private bool m_visualCaptured;
        private MaterialPropertyBlock m_mpb;
        private ColorMaterialMapping m_materials;
        private Coroutine m_activeRoutine;
        private bool m_cullEnclosed;

        public event Action<CellController> Died;
        public event Action<CellController> Unlocked;
        public event Action<CellController> ExposureChanged;

        private void Awake()
        {
            if (m_mpb == null) m_mpb = new MaterialPropertyBlock();
            if (m_meshFilter == null) m_meshFilter = GetComponent<MeshFilter>();
            if (m_renderer == null) m_renderer = GetComponent<MeshRenderer>();
            CaptureAuthoredVisual();
        }

        private void CaptureAuthoredVisual()
        {
            if (m_visualCaptured) return;

            Transform visual = VisualRoot;
            m_authoredVisualScale = visual != null && visual != transform ? visual.localScale : Vector3.one;
            m_visualCaptured = true;
        }

        public void BakeVisualReference(float cellWidth, float cellDepth, int depthAxis)
        {
            m_referenceWidth = cellWidth;
            m_referenceDepth = cellDepth;
            m_visualDepthAxis = Mathf.Clamp(depthAxis, 0, 2);
            m_visualCaptured = false;
        }

        public void Initialize(int id, HexCoord3 coord, ColorType colorType, int nectar, bool locked, Vector3 localPos, Vector3 visualScale, float cellWidth, float cellDepth, ColorMaterialMapping materials)
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
            HomeScale = Vector3.one;

            transform.localPosition = localPos;
            transform.localScale = Vector3.one;
            ApplyVisualSizing(visualScale, cellWidth, cellDepth);

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
