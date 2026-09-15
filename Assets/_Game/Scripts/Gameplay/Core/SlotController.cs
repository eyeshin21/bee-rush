using System;
using System.Collections;
using HoneyBeeRush.View;
using Lean.Pool;
using UnityEngine;

namespace HoneyBeeRush.Gameplay.Core
{
    public sealed class SlotController : MonoBehaviour, IPoolable
    {
        [SerializeField] private MeshRenderer m_padRenderer;
        [SerializeField] private Transform m_anchor;
        [SerializeField] private Transform m_spawnPoint;
        [SerializeField] private float m_referenceRadius = 0.478f;

        [Header("Full Warning")]
        [SerializeField] private Color m_warningColor = new Color(0.93f, 0.16f, 0.16f, 1f);
        [SerializeField] private float m_warningDuration = 0.5f;
        [SerializeField] private int m_warningPulses = 2;

        private Vector3 m_authoredPadScale;
        private bool m_authoredPadCached;
        private MaterialPropertyBlock m_mpb;
        private Coroutine m_warningRoutine;

        private float WarningDuration => m_warningDuration > 0f ? m_warningDuration : 0.5f;
        private int WarningPulses => m_warningPulses > 0 ? m_warningPulses : 2;
        private Color WarningColor => m_warningColor.a > 0f ? m_warningColor : new Color(0.93f, 0.16f, 0.16f, 1f);

        public int SlotIndex { get; private set; }
        public CrateController CurrentCrate { get; private set; }
        public int ToSpawn { get; private set; }
        public int LiveBees { get; private set; }
        public float SpawnTimer { get; set; }
        public float LidWaitTimer { get; set; }
        public bool IsClogged { get; set; }
        public bool IsThrottled { get; set; }
        public bool ArrivedInSlot { get; private set; }

        public bool IsOccupied => CurrentCrate != null;
        public CrateController Crate => CurrentCrate;
        public bool IsFree => !IsOccupied;
        public bool HasWork => IsOccupied && (ToSpawn > 0 || LiveBees > 0);

        public Transform Anchor => m_anchor != null ? m_anchor : transform;

        public void ApplyVisualRadius(float radius)
        {
            if (m_padRenderer == null) return;

            if (!m_authoredPadCached)
            {
                m_authoredPadScale = m_padRenderer.transform.localScale;
                m_authoredPadCached = true;
            }

            float reference = m_referenceRadius > 0f ? m_referenceRadius : 0.478f;
            m_padRenderer.transform.localScale = m_authoredPadScale * (Mathf.Max(0.01f, radius) / reference);
        }
        public Vector3 AnchorWorldPos => m_anchor != null ? m_anchor.position : transform.position;
        public Vector3 SpawnWorldPos => m_spawnPoint != null ? m_spawnPoint.position : transform.position + new Vector3(0f, 0.22f, -1.62f);

        private Coroutine m_activeRoutine;

        public bool IsWarning => m_warningRoutine != null;

        public event Action<SlotController> Occupied;
        public event Action<SlotController> Freed;

        private void Awake()
        {
            if (m_padRenderer == null) m_padRenderer = GetComponentInChildren<MeshRenderer>();
            if (m_anchor == null) m_anchor = transform;
            if (m_spawnPoint == null) m_spawnPoint = transform;
        }

        public void Initialize(int index)
        {
            SlotIndex = index;
            CurrentCrate = null;
            ToSpawn = 0;
            LiveBees = 0;
            SpawnTimer = 0f;
            LidWaitTimer = 0f;
            IsClogged = false;
            IsThrottled = false;
            ArrivedInSlot = false;
        }

        public void SetIndex(int index)
        {
            SlotIndex = index;
        }

        public void Occupy(CrateController crate)
        {
            CurrentCrate = crate;
            if (crate != null)
            {
                crate.SetInSlot(true);
                ToSpawn = crate.BeeCount;
                LiveBees = 0;
                SpawnTimer = float.MaxValue;
                LidWaitTimer = 0f;
                IsClogged = false;
                IsThrottled = false;
                ArrivedInSlot = false;
            }
            Occupied?.Invoke(this);
        }

        public void BeginSpawning()
        {
            if (ArrivedInSlot) return;
            ArrivedInSlot = true;
            SpawnTimer = 0f;
        }

        public void DiscardRemainingSpawns()
        {
            if (ToSpawn <= 0) return;

            ToSpawn = 0;
            IsClogged = false;
            IsThrottled = false;
            if (CurrentCrate != null) CurrentCrate.SetRemainingBees(0);
        }

        public void MarkSpawned()
        {
            if (ToSpawn > 0)
            {
                ToSpawn--;
                LiveBees++;
                if (CurrentCrate != null)
                {
                    CurrentCrate.SetRemainingBees(ToSpawn);
                }
            }
        }

        public void OnBeeReturned(bool hadCargo)
        {
            if (LiveBees > 0)
            {
                LiveBees--;
            }
        }

        public bool TryFreeIfDone()
        {
            if (!IsOccupied) return false;
            if (ToSpawn > 0) return false;

            Free();
            return true;
        }

        public void Free()
        {
            CurrentCrate = null;
            ToSpawn = 0;
            LiveBees = 0;
            SpawnTimer = 0f;
            LidWaitTimer = 0f;
            IsClogged = false;
            IsThrottled = false;
            ArrivedInSlot = false;
            Freed?.Invoke(this);
        }

        public void PlayFullWarning()
        {
            if (m_padRenderer == null) return;
            if (m_warningRoutine != null) return;
            if (!isActiveAndEnabled) return;

            m_warningRoutine = StartCoroutine(FullWarningRoutine());
        }

        private IEnumerator FullWarningRoutine()
        {
            if (m_mpb == null) m_mpb = new MaterialPropertyBlock();

            Material source = m_padRenderer.sharedMaterial;
            Color baseColor = source != null && source.HasProperty(ShaderIds.Color)
                ? source.GetColor(ShaderIds.Color)
                : Color.white;

            try
            {
                float duration = WarningDuration;
                float pulses = WarningPulses;
                float elapsed = 0f;

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    float wave = Mathf.Abs(Mathf.Sin(t * Mathf.PI * pulses));

                    m_mpb.Clear();
                    m_mpb.SetColor(ShaderIds.Color, Color.Lerp(baseColor, WarningColor, wave));
                    m_padRenderer.SetPropertyBlock(m_mpb);
                    yield return null;
                }
            }
            finally
            {
                if (m_padRenderer != null) m_padRenderer.SetPropertyBlock(null);
                m_warningRoutine = null;
            }
        }

        private void StopWarningRoutine()
        {
            if (m_warningRoutine != null)
            {
                StopCoroutine(m_warningRoutine);
                m_warningRoutine = null;
            }
            if (m_padRenderer != null) m_padRenderer.SetPropertyBlock(null);
        }

        public void PlayAppear(float duration)
        {
            StopActiveRoutine();
            m_activeRoutine = StartCoroutine(AppearRoutine(duration));
        }

        private IEnumerator AppearRoutine(float duration)
        {
            transform.localScale = Vector3.zero;
            float elapsed = 0f;
            duration = Mathf.Max(0.01f, duration);
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float s = 1f + Mathf.Sin(t * Mathf.PI) * 0.15f;
                transform.localScale = Vector3.one * (t * s);
                yield return null;
            }
            transform.localScale = Vector3.one;
            m_activeRoutine = null;
        }

        public void PlayReflowTo(Vector3 targetLocalPos, float duration)
        {
            StopActiveRoutine();
            m_activeRoutine = StartCoroutine(ReflowRoutine(targetLocalPos, duration));
        }

        private IEnumerator ReflowRoutine(Vector3 targetLocalPos, float duration)
        {
            Vector3 start = transform.localPosition;
            float elapsed = 0f;
            duration = Mathf.Max(0.01f, duration);
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = 1f - Mathf.Pow(1f - t, 2f);
                transform.localPosition = Vector3.Lerp(start, targetLocalPos, ease);
                yield return null;
            }
            transform.localPosition = targetLocalPos;
            m_activeRoutine = null;
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
            StopWarningRoutine();
            transform.localScale = Vector3.one;
            gameObject.SetActive(true);
        }

        public void OnDespawn()
        {
            StopActiveRoutine();
            StopWarningRoutine();
            CurrentCrate = null;
            ToSpawn = 0;
            LiveBees = 0;
            SpawnTimer = 0f;
            LidWaitTimer = 0f;
            IsClogged = false;
            IsThrottled = false;
            ArrivedInSlot = false;
            Occupied = null;
            Freed = null;
            transform.localScale = Vector3.one;
        }

        private void OnDisable()
        {
            StopActiveRoutine();
            StopWarningRoutine();
        }
    }
}
