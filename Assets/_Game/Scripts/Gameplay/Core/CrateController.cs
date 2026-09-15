using System;
using System.Collections;
using System.Collections.Generic;
using HoneyBeeRush.Data;
using Lean.Pool;
using TMPro;
using UnityEngine;

namespace HoneyBeeRush.Gameplay.Core
{
    public sealed class CrateController : MonoBehaviour, IPoolable
    {
        [SerializeField] private MeshRenderer m_renderer;
        [SerializeField] private MeshRenderer m_lidRenderer;
        [SerializeField] private BoxCollider m_collider;
        [SerializeField] private TextMeshPro m_mainLabel;
        [SerializeField] private GameObject m_lockRoot;
        [SerializeField] private TextMeshPro m_lockLabel;
        [SerializeField] private MeshRenderer m_lockMeshRenderer;
        [SerializeField] private float m_referenceRadius = 0.478f;

        [Header("Active / Inactive")]
        [SerializeField] private Transform m_model;
        [Tooltip("Local axis of the model that extrudes the hex prism. The mesh is rotated -90 on X, so " +
                 "its local Y is the block depth. Scaling it makes the block stand higher off the board " +
                 "without distorting the hexagon face. The number label is pushed forward to match.")]
        [SerializeField] private Vector3 m_activeHeightAxis = new Vector3(0f, 1f, 0f);
        [SerializeField] private float m_activeHeightScale = 2f;
        [SerializeField] private float m_inactiveTextAlpha = 0.3f;
        [SerializeField] private float m_stateTransitionDuration = 0.22f;

        [Header("Lid")]
        [SerializeField] private Transform m_lid;
        [SerializeField] private Vector3 m_lidOpenEuler = new Vector3(-100f, 0f, 0f);
        [SerializeField] private float m_lidOpenDuration = 0.16f;
        [SerializeField] private float m_lidCloseDuration = 0.12f;
        [SerializeField] private float m_lidReleaseThreshold = 0.9f;
        [SerializeField] private float m_lidCloseDelay = 0.35f;
        [SerializeField] private float m_lidLastBeeHold = 0.12f;

        [Header("Bee Spawn")]
        [SerializeField] private Transform m_beeSpawnPoint;
        [SerializeField] private float m_beeSpawnScatter = 0.35f;
        [SerializeField] private float m_beeGrowDuration = 0.15f;
        [SerializeField] private float m_beeSpawnLidClearance = 0.15f;

        public int CrateId { get; private set; }
        public ColorType ColorType { get; private set; }
        public int BeeCount { get; private set; }
        public int RemainingBees { get; private set; }
        public int ColumnIndex { get; private set; }
        public int DepthInColumn { get; private set; }
        public GridPosition GridPosition { get; set; }
        public bool Hidden { get; private set; }
        public int LinkGroup { get; private set; }
        public int ReleaseDelay { get; private set; }
        public int MovesRemaining { get; private set; }
        public bool Revealed { get; private set; }
        public bool InSlot { get; private set; }
        public bool IsTravelling { get; private set; }

        public bool DelayLocked => MovesRemaining > 0;

        public bool IsPickable
        {
            get => m_isPickable;
            set
            {
                if (m_isPickable == value) return;

                m_isPickable = value;
                ApplyMaterials();
                ApplyActiveState(value, true);
            }
        }

        public bool CanBeSelected => IsPickable && !InSlot && Revealed && !DelayLocked;
        public bool IsActiveState => m_isPickable;

        public float LayoutScale { get; set; } = 1f;

        public float ReferenceRadius => m_referenceRadius > 0f ? m_referenceRadius : 0.478f;

        public bool IsLidOpen => m_lidTargetOpen;

        public bool IsLidReadyForRelease => m_lid == null || (m_lidTargetOpen && m_lidProgress >= LidReleaseThreshold);

        public float LidCloseDelay => m_lidCloseDelay > 0f ? m_lidCloseDelay : 0.35f;

        public float BeeSpawnScatter => m_beeSpawnPoint != null ? (m_beeSpawnScatter > 0f ? m_beeSpawnScatter : 0.35f) : 1f;

        public float BeeGrowDuration => m_beeSpawnPoint != null ? (m_beeGrowDuration > 0f ? m_beeGrowDuration : 0.15f) : 0f;

        private const float LidOpenOvershoot = 0.85f;

        private ColorMaterialMapping m_materials;
        private Coroutine m_activeRoutine;
        private Coroutine m_feedbackRoutine;
        private Coroutine m_stateRoutine;
        private Coroutine m_lidRoutine;
        private Coroutine m_revealRoutine;
        private bool m_isPickable;
        private Vector3 m_authoredModelScale;
        private bool m_authoredModelCached;
        private Vector3 m_authoredLabelLocalPos;
        private float m_modelPivotRootZ;
        private float m_bodyFrontRootZ;
        private float m_lidFrontOffset;
        private float m_labelClearance;
        private Vector3 m_authoredLidLocalPos;
        private Quaternion m_authoredLidLocalRot = Quaternion.identity;
        private bool m_authoredLidCached;
        private float m_lidOpenTipOffsetZ;
        private bool m_lidOpenTipCached;
        private float m_lidProgress;
        private bool m_lidTargetOpen;
        private int m_lidRoutineToken;

        private float ActiveHeightScale => m_activeHeightScale > 0f ? m_activeHeightScale : 2f;

        private Vector3 ActiveHeightAxis =>
            m_activeHeightAxis.sqrMagnitude > 0.0001f ? m_activeHeightAxis : new Vector3(0f, 1f, 0f);
        private float InactiveTextAlpha => m_inactiveTextAlpha > 0f ? m_inactiveTextAlpha : 0.3f;
        private float StateTransitionDuration => m_stateTransitionDuration > 0f ? m_stateTransitionDuration : 0.22f;

        private Vector3 LidOpenEuler =>
            m_lidOpenEuler.sqrMagnitude > 0.0001f ? m_lidOpenEuler : new Vector3(-100f, 0f, 0f);
        private float LidOpenDuration => m_lidOpenDuration > 0f ? m_lidOpenDuration : 0.16f;
        private float LidCloseDuration => m_lidCloseDuration > 0f ? m_lidCloseDuration : 0.12f;
        private float LidReleaseThreshold => m_lidReleaseThreshold > 0f ? Mathf.Min(1f, m_lidReleaseThreshold) : 0.9f;
        private float LidLastBeeHold => m_lidLastBeeHold > 0f ? m_lidLastBeeHold : 0.12f;
        private float BeeSpawnLidClearance => m_beeSpawnLidClearance > 0f ? m_beeSpawnLidClearance : 0.15f;

        private Transform Model
        {
            get
            {
                if (m_model != null) return m_model;
                return m_renderer != null ? m_renderer.transform : null;
            }
        }

        public event Action<CrateController> Tapped;
        public event Action<CrateController> RevealedChanged;
        public event Action<CrateController> DelayCleared;

        private void Awake()
        {
            if (m_renderer == null) m_renderer = GetComponent<MeshRenderer>();
            if (m_collider == null) m_collider = GetComponent<BoxCollider>();
            CacheAuthoredModelScale();
        }

        private void CacheAuthoredModelScale()
        {
            if (!m_authoredLidCached && m_lid != null)
            {
                m_authoredLidLocalPos = m_lid.localPosition;
                m_authoredLidLocalRot = m_lid.localRotation;
                m_authoredLidCached = true;

                if (m_lidRenderer != null)
                {
                    m_lidOpenTipOffsetZ = ComputeLidOpenTipOffsetZ();
                    m_lidOpenTipCached = true;
                }
            }

            if (m_authoredModelCached) return;

            Transform model = Model;
            if (model == null) return;

            m_authoredModelScale = model.localScale;
            m_modelPivotRootZ = transform.InverseTransformPoint(model.position).z;
            m_bodyFrontRootZ = m_renderer != null ? RootMinZ(m_renderer) : m_modelPivotRootZ;

            float frontZ = m_lidRenderer != null ? Mathf.Min(m_bodyFrontRootZ, RootMinZ(m_lidRenderer)) : m_bodyFrontRootZ;
            m_lidFrontOffset = m_bodyFrontRootZ - frontZ;

            if (m_mainLabel != null)
            {
                m_authoredLabelLocalPos = m_mainLabel.transform.localPosition;
                m_labelClearance = frontZ - m_authoredLabelLocalPos.z;
            }

            m_authoredModelCached = true;
        }

        private float RootMinZ(Renderer target)
        {
            return MinZ(transform.worldToLocalMatrix * target.transform.localToWorldMatrix, target.localBounds);
        }

        private float ComputeLidOpenTipOffsetZ()
        {
            Transform parent = m_lid.parent;
            Matrix4x4 parentToRoot = transform.worldToLocalMatrix * (parent != null ? parent.localToWorldMatrix : Matrix4x4.identity);
            Matrix4x4 rendererToLid = m_lid.worldToLocalMatrix * m_lidRenderer.transform.localToWorldMatrix;
            Bounds local = m_lidRenderer.localBounds;

            float pivotZ = parentToRoot.MultiplyPoint3x4(m_authoredLidLocalPos).z;
            float releaseZ = MinZ(parentToRoot * LidPoseMatrix(LidReleaseThreshold) * rendererToLid, local);
            float openZ = MinZ(parentToRoot * LidPoseMatrix(1f) * rendererToLid, local);

            return Mathf.Min(releaseZ, openZ) - pivotZ;
        }

        private Matrix4x4 LidPoseMatrix(float pose)
        {
            return Matrix4x4.TRS(
                m_authoredLidLocalPos,
                m_authoredLidLocalRot * Quaternion.Euler(LidOpenEuler * pose),
                m_lid.localScale);
        }

        private static float MinZ(Matrix4x4 toRoot, Bounds local)
        {
            float min = float.MaxValue;

            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = new Vector3(
                    (i & 1) == 0 ? local.min.x : local.max.x,
                    (i & 2) == 0 ? local.min.y : local.max.y,
                    (i & 4) == 0 ? local.min.z : local.max.z);

                float z = toRoot.MultiplyPoint3x4(corner).z;
                if (z < min) min = z;
            }

            return min;
        }

        public void Initialize(int id, CrateDef def, int columnIndex, int depth, ColorMaterialMapping materials)
        {
            CrateId = id;
            m_materials = materials;
            ColorType = def != null ? def.colorType : ColorType.None;
            BeeCount = def != null && def.beeCount > 0 ? def.beeCount : 0;
            RemainingBees = BeeCount;
            ColumnIndex = columnIndex;
            DepthInColumn = depth;
            GridPosition = new GridPosition(depth, columnIndex);
            Hidden = def != null && def.hidden;
            LinkGroup = def != null ? def.linkGroup : 0;
            ReleaseDelay = def != null && def.releaseDelay > 0 ? def.releaseDelay : 0;
            MovesRemaining = ReleaseDelay;
            Revealed = !Hidden || depth == 0;
            InSlot = false;
            IsTravelling = false;
            LayoutScale = 1f;
            transform.localRotation = Quaternion.identity;

            m_isPickable = false;
            ApplyActiveState(false, false);
            StopLidRoutine();
            SnapLid(false);

            UpdateVisualState();
        }

        public void ApplyVisualRadius(float radius)
        {
            LayoutScale = Mathf.Max(0.01f, radius) / ReferenceRadius;
            transform.localScale = Vector3.one * LayoutScale;
        }

        public void ApplyWorldScale(float worldScale)
        {
            Transform parent = transform.parent;
            float parentScale = parent != null ? parent.lossyScale.x : 1f;
            if (parentScale < 0.0001f) parentScale = 1f;

            LayoutScale = Mathf.Max(0.001f, worldScale) / parentScale;
            transform.localScale = Vector3.one * LayoutScale;
        }

        public void SetGridPosition(GridPosition pos)
        {
            GridPosition = pos;
            ColumnIndex = pos.Column;
            DepthInColumn = pos.Row;
        }

        public void SetDepth(int depth)
        {
            DepthInColumn = depth;
            GridPosition = new GridPosition(depth, ColumnIndex);
        }

        public void SetColumn(int column)
        {
            ColumnIndex = column;
            GridPosition = new GridPosition(DepthInColumn, column);
        }

        public void SetInSlot(bool inSlot)
        {
            InSlot = inSlot;
            if (m_collider != null) m_collider.enabled = !inSlot;
        }

        public void SetRemainingBees(int count)
        {
            RemainingBees = Mathf.Max(0, count);
            UpdateLabel();
        }

        public void ConsumeBee()
        {
            if (RemainingBees > 0)
            {
                RemainingBees--;
                UpdateLabel();
            }
        }

        public void DecrementDelay()
        {
            if (MovesRemaining <= 0) return;
            MovesRemaining--;
            UpdateVisualState();
            if (MovesRemaining == 0)
            {
                DelayCleared?.Invoke(this);
            }
        }

        public void Reveal()
        {
            if (Revealed) return;
            Revealed = true;
            UpdateVisualState();
            RevealedChanged?.Invoke(this);
            PlayRevealAnimation();
        }

        public void UpdateVisualState()
        {
            ApplyMaterials();
            UpdateLabel();
            UpdateLock();
        }

        private void ApplyMaterials()
        {
            if (m_materials == null) return;
            if (m_renderer == null && m_lidRenderer == null) return;

            Material material = ResolveBodyMaterial();
            if (m_renderer != null) m_renderer.sharedMaterial = material;
            if (m_lidRenderer != null) m_lidRenderer.sharedMaterial = material;
        }

        private Material ResolveBodyMaterial()
        {
            if (!Revealed) return m_materials.CrateHidden;
            if (DelayLocked) return m_materials.CrateLocked(ColorType);
            return m_isPickable ? m_materials.CrateActive(ColorType) : m_materials.CrateInactive(ColorType);
        }

        private void UpdateLabel()
        {
            bool show = Revealed && RemainingBees > 0;

            string text = show ? RemainingBees.ToString() : string.Empty;
            if (m_mainLabel != null) m_mainLabel.text = text;
        }

        private void UpdateLock()
        {
            bool hasLock = DelayLocked;
            if (m_lockRoot != null) m_lockRoot.SetActive(hasLock);
            if (!hasLock) return;

            if (m_lockLabel != null)
            {
                m_lockLabel.text = MovesRemaining.ToString();
            }

            if (m_lockMeshRenderer != null && m_materials != null)
            {
                m_lockMeshRenderer.sharedMaterial = m_materials.LockBody(ColorType);
            }
        }

        private void ApplyActiveState(bool active, bool animate)
        {
            CacheAuthoredModelScale();
            StopStateRoutine();

            if (!animate || !isActiveAndEnabled)
            {
                SetStateInstant(active);
                return;
            }

            m_stateRoutine = StartCoroutine(StateRoutine(active));
        }

        private void SetStateInstant(bool active)
        {
            ApplyModelHeight(active ? ActiveHeightScale : 1f);
            ApplyTextAlpha(active ? 1f : InactiveTextAlpha);
        }

        private IEnumerator StateRoutine(bool active)
        {
            float fromHeight = CurrentHeightMultiplier();
            float toHeight = active ? ActiveHeightScale : 1f;
            float fromAlpha = m_mainLabel != null ? m_mainLabel.alpha : 1f;
            float toAlpha = active ? 1f : InactiveTextAlpha;
            float duration = StateTransitionDuration;

            try
            {
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    float ease = active ? OutBack(t) : 1f - (1f - t) * (1f - t);

                    ApplyModelHeight(Mathf.LerpUnclamped(fromHeight, toHeight, ease));
                    ApplyTextAlpha(Mathf.Lerp(fromAlpha, toAlpha, t));
                    yield return null;
                }
            }
            finally
            {
                SetStateInstant(active);
                m_stateRoutine = null;
            }
        }

        private void ApplyModelHeight(float heightMultiplier)
        {
            Transform model = Model;
            if (model == null || !m_authoredModelCached) return;

            Vector3 axis = ActiveHeightAxis;
            Vector3 factor = new Vector3(
                Mathf.LerpUnclamped(1f, heightMultiplier, Mathf.Clamp01(axis.x)),
                Mathf.LerpUnclamped(1f, heightMultiplier, Mathf.Clamp01(axis.y)),
                Mathf.LerpUnclamped(1f, heightMultiplier, Mathf.Clamp01(axis.z)));

            model.localScale = Vector3.Scale(m_authoredModelScale, factor);

            if (m_lid != null && m_authoredLidCached && m_lid.parent == model.parent)
            {
                Vector3 modelLocalPos = model.localPosition;
                m_lid.localPosition = modelLocalPos + Vector3.Scale(m_authoredLidLocalPos - modelLocalPos, factor);
            }

            KeepLabelInFront(heightMultiplier);
        }

        private void KeepLabelInFront(float heightMultiplier)
        {
            if (m_mainLabel == null) return;

            float bodyFrontZ = m_modelPivotRootZ + (m_bodyFrontRootZ - m_modelPivotRootZ) * heightMultiplier;
            float frontZ = bodyFrontZ - m_lidFrontOffset;

            m_mainLabel.transform.localPosition = new Vector3(
                m_authoredLabelLocalPos.x,
                m_authoredLabelLocalPos.y,
                frontZ - m_labelClearance);
        }

        private void ApplyTextAlpha(float alpha)
        {
            if (m_mainLabel != null) m_mainLabel.alpha = Mathf.Clamp01(alpha);
        }

        private float CurrentHeightMultiplier()
        {
            Transform model = Model;
            if (model == null || !m_authoredModelCached) return 1f;

            Vector3 axis = ActiveHeightAxis;
            if (axis.y > 0.5f && m_authoredModelScale.y > 0.0001f) return model.localScale.y / m_authoredModelScale.y;
            if (axis.z > 0.5f && m_authoredModelScale.z > 0.0001f) return model.localScale.z / m_authoredModelScale.z;
            if (axis.x > 0.5f && m_authoredModelScale.x > 0.0001f) return model.localScale.x / m_authoredModelScale.x;
            return 1f;
        }

        private void StopStateRoutine()
        {
            if (m_stateRoutine != null)
            {
                StopCoroutine(m_stateRoutine);
                m_stateRoutine = null;
            }
        }

        public Vector3 BeeSpawnPosition(Vector3 fallback)
        {
            if (m_beeSpawnPoint == null) return fallback;

            Vector3 position = m_beeSpawnPoint.position;
            if (m_lid == null) return position;

            CacheAuthoredModelScale();
            if (!m_lidOpenTipCached) return position;

            Vector3 local = transform.InverseTransformPoint(position);
            float clearZ = transform.InverseTransformPoint(m_lid.position).z + m_lidOpenTipOffsetZ - BeeSpawnLidClearance;
            if (local.z <= clearZ) return position;

            local.z = clearZ;
            return transform.TransformPoint(local);
        }

        public void SetLidOpen(bool open)
        {
            if (m_lid == null)
            {
                m_lidTargetOpen = open;
                m_lidProgress = open ? 1f : 0f;
                return;
            }

            if (m_lidTargetOpen == open)
            {
                if (m_lidRoutine != null) return;
                if (m_lidProgress == (open ? 1f : 0f)) return;
            }

            if (!isActiveAndEnabled)
            {
                StopLidRoutine();
                SnapLid(open);
                return;
            }

            StopLidRoutine();
            m_lidTargetOpen = open;
            m_lidRoutine = StartCoroutine(LidRoutine(open));
        }

        private IEnumerator LidRoutine(bool open)
        {
            int token = m_lidRoutineToken;
            float from = m_lidProgress;
            float to = open ? 1f : 0f;
            float duration = Mathf.Max(0.01f, (open ? LidOpenDuration : LidCloseDuration) * Mathf.Abs(to - from));

            try
            {
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    float ease = open ? LidOpenEase(t) : t * t;
                    float pose = Mathf.LerpUnclamped(from, to, ease);

                    m_lidProgress = Mathf.Clamp01(pose);
                    ApplyLidPose(pose);
                    yield return null;
                }
            }
            finally
            {
                if (token == m_lidRoutineToken)
                {
                    SnapLid(open);
                    m_lidRoutine = null;
                }
            }
        }

        private static float LidOpenEase(float t)
        {
            float u = t - 1f;
            return 1f + (LidOpenOvershoot + 1f) * u * u * u + LidOpenOvershoot * u * u;
        }

        private void SnapLid(bool open)
        {
            m_lidTargetOpen = open;
            m_lidProgress = open ? 1f : 0f;
            ApplyLidPose(m_lidProgress);
        }

        private void ApplyLidPose(float pose)
        {
            if (m_lid == null) return;

            CacheAuthoredModelScale();
            if (!m_authoredLidCached) return;

            m_lid.localRotation = m_authoredLidLocalRot * Quaternion.Euler(LidOpenEuler * pose);
        }

        private void StopLidRoutine()
        {
            m_lidRoutineToken++;
            if (m_lidRoutine != null)
            {
                StopCoroutine(m_lidRoutine);
                m_lidRoutine = null;
            }
        }

        public void PlaySlideTo(Vector3 targetLocalPos, float duration)
        {
            PlaySlideTo(targetLocalPos, LayoutScale, duration);
        }

        public void PlaySlideTo(Vector3 targetLocalPos, float targetScale, float duration)
        {
            StopActiveRoutine();
            m_activeRoutine = StartCoroutine(SlideRoutine(targetLocalPos, targetScale, duration));
        }

        private IEnumerator SlideRoutine(Vector3 targetLocalPos, float targetScale, float duration)
        {
            Vector3 startPos = transform.localPosition;
            Vector3 startScale = transform.localScale;
            Vector3 endScale = Vector3.one * targetScale;
            LayoutScale = targetScale;
            float elapsed = 0f;
            duration = Mathf.Max(0.01f, duration);
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = 1f - Mathf.Pow(1f - t, 3f);
                transform.localPosition = Vector3.Lerp(startPos, targetLocalPos, ease);
                transform.localScale = Vector3.Lerp(startScale, endScale, ease);
                yield return null;
            }
            transform.localPosition = targetLocalPos;
            transform.localScale = endScale;
            m_activeRoutine = null;
        }

        public void PlayTravelToSlot(IReadOnlyList<Vector3> gridPath, Transform slotAnchor, CrateTravelSettings settings, Action onArrived = null)
        {
            StopActiveRoutine();
            StopFeedbackRoutine();
            StopRevealRoutine();
            StopLidRoutine();
            SnapLid(false);
            transform.localScale = Vector3.one * LayoutScale;
            transform.localRotation = Quaternion.identity;
            IsTravelling = true;
            m_activeRoutine = StartCoroutine(TravelRoutine(gridPath, slotAnchor, settings, onArrived));
        }

        private IEnumerator TravelRoutine(IReadOnlyList<Vector3> gridPath, Transform slotAnchor, CrateTravelSettings settings, Action onArrived)
        {
            gameObject.SetActive(true);

            float worldScale = transform.lossyScale.x;
            Vector3 baseScale = transform.localScale;
            Quaternion baseRotation = transform.localRotation;

            try
            {
                int hopCount = gridPath != null ? gridPath.Count : 0;
                float step = settings.hopStepDuration > 0f ? settings.hopStepDuration : 0.10f;

                for (int i = 0; i < hopCount; i++)
                {
                    yield return HopRoutine(gridPath[i], step, baseScale);
                }

                if (slotAnchor != null)
                {
                    yield return AnticipateRoutine(baseScale);
                    yield return JumpRoutine(slotAnchor, settings, baseScale, baseRotation);
                }

                if (slotAnchor != null)
                {
                    transform.SetParent(slotAnchor, false);
                    transform.localPosition = Vector3.zero;
                    transform.localRotation = Quaternion.identity;
                    ApplyWorldScale(worldScale);

                    yield return LandRoutine(transform.localScale);
                }
                else
                {
                    transform.localScale = baseScale;
                    transform.localRotation = baseRotation;
                }
            }
            finally
            {
                IsTravelling = false;
                m_activeRoutine = null;
            }

            if (onArrived != null) onArrived.Invoke();
        }

        private IEnumerator HopRoutine(Vector3 targetWorldPos, float duration, Vector3 baseScale)
        {
            Vector3 start = transform.position;
            float elapsed = 0f;
            duration = Mathf.Max(0.01f, duration);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = 1f - (1f - t) * (1f - t);
                transform.position = Vector3.Lerp(start, targetWorldPos, ease);

                float pulse = Mathf.Sin(t * Mathf.PI);
                ApplyStretch(baseScale, 1f + pulse * 0.10f);
                yield return null;
            }

            transform.position = targetWorldPos;
            transform.localScale = baseScale;
        }

        private IEnumerator AnticipateRoutine(Vector3 baseScale)
        {
            float elapsed = 0f;
            float duration = 0.08f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                ApplyStretch(baseScale, Mathf.Lerp(1f, 0.82f, t));
                yield return null;
            }
        }

        private IEnumerator JumpRoutine(Transform slotAnchor, CrateTravelSettings settings, Vector3 baseScale, Quaternion baseRotation)
        {
            Vector3 start = transform.position;
            Vector3 firstTarget = slotAnchor.position;

            float baseHeight = ResolveJumpHeight(settings);
            float distance = Vector3.Distance(start, firstTarget);
            float apex = Mathf.Clamp(
                baseHeight + distance * ResolveJumpHeightPerUnit(settings),
                baseHeight,
                ResolveJumpMaxHeight(settings));

            float speed = settings.flySpeed > 0f ? settings.flySpeed : 15f;
            float duration = Mathf.Clamp(distance / speed, ResolveJumpMinDuration(settings), ResolveJumpMaxDuration(settings));

            float tilt = -Mathf.Sign(firstTarget.x - start.x) * ResolveJumpTilt(settings);
            float depthPop = ResolveJumpDepthPop(settings);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                if (slotAnchor == null) yield break;
                Vector3 target = slotAnchor.position;

                float ease = Mathf.Lerp(t, 1f - (1f - t) * (1f - t), 0.35f);
                float arc = 4f * t * (1f - t);

                transform.position = new Vector3(
                    Mathf.Lerp(start.x, target.x, ease),
                    Mathf.Lerp(start.y, target.y, ease) + apex * arc,
                    Mathf.Lerp(start.z, target.z, ease) - depthPop * arc);

                transform.localRotation = baseRotation * Quaternion.Euler(0f, 0f, tilt * Mathf.Sin(t * Mathf.PI));
                ApplyStretch(baseScale, ResolveFlightStretch(t));
                yield return null;
            }

            transform.localRotation = baseRotation;
            transform.localScale = baseScale;
        }

        private static float ResolveFlightStretch(float t)
        {
            if (t < 0.28f) return Mathf.Lerp(1.18f, 1f, t / 0.28f);
            if (t > 0.74f) return Mathf.Lerp(1f, 1.12f, (t - 0.74f) / 0.26f);
            return 1f;
        }

        private IEnumerator LandRoutine(Vector3 baseScale)
        {
            float elapsed = 0f;
            float squashTime = 0.09f;
            while (elapsed < squashTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / squashTime);
                float ease = 1f - (1f - t) * (1f - t);
                ApplyStretch(baseScale, Mathf.Lerp(1.12f, 0.78f, ease));
                yield return null;
            }

            elapsed = 0f;
            float settleTime = 0.18f;
            while (elapsed < settleTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / settleTime);
                ApplyStretch(baseScale, Mathf.Lerp(0.78f, 1f, OutBack(t)));
                yield return null;
            }

            transform.localScale = baseScale;
        }

        private static float OutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        private void ApplyStretch(Vector3 baseScale, float stretch)
        {
            stretch = Mathf.Max(0.01f, stretch);
            float lateral = 1f / Mathf.Sqrt(stretch);
            transform.localScale = new Vector3(
                baseScale.x * lateral,
                baseScale.y * stretch,
                baseScale.z * lateral);
        }

        private static float ResolveJumpHeight(CrateTravelSettings s) => s.jumpHeight > 0f ? s.jumpHeight : 0.85f;

        private static float ResolveJumpHeightPerUnit(CrateTravelSettings s) => s.jumpHeightPerUnit > 0f ? s.jumpHeightPerUnit : 0.16f;

        private static float ResolveJumpMaxHeight(CrateTravelSettings s) => s.jumpMaxHeight > 0f ? s.jumpMaxHeight : 2.1f;

        private static float ResolveJumpMinDuration(CrateTravelSettings s) => s.jumpMinDuration > 0f ? s.jumpMinDuration : 0.34f;

        private static float ResolveJumpMaxDuration(CrateTravelSettings s) => s.jumpMaxDuration > 0f ? s.jumpMaxDuration : 0.62f;

        private static float ResolveJumpTilt(CrateTravelSettings s) => s.jumpTilt > 0f ? s.jumpTilt : 14f;

        private static float ResolveJumpDepthPop(CrateTravelSettings s) => s.jumpDepthPop > 0f ? s.jumpDepthPop : 0.45f;

        public void PlayInvalidFeedback()
        {
            if (!CanPlayFeedback()) return;
            m_feedbackRoutine = StartCoroutine(InvalidFeedbackRoutine());
        }

        private IEnumerator InvalidFeedbackRoutine()
        {
            Vector3 baseScale = transform.localScale;
            Quaternion baseRotation = transform.localRotation;

            try
            {
                float elapsed = 0f;
                float duration = 0.34f;

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    float wave = Mathf.Sin(t * Mathf.PI * 6f) * (1f - t);

                    transform.localRotation = baseRotation * Quaternion.Euler(0f, 0f, wave * 13f);
                    ApplyStretch(baseScale, 1f + wave * 0.07f);
                    yield return null;
                }
            }
            finally
            {
                transform.localRotation = baseRotation;
                transform.localScale = baseScale;
                m_feedbackRoutine = null;
            }
        }

        public void PlayShake()
        {
            if (!CanPlayFeedback()) return;
            m_feedbackRoutine = StartCoroutine(ShakeRoutine());
        }

        private IEnumerator ShakeRoutine()
        {
            Vector3 basePos = transform.localPosition;

            try
            {
                float elapsed = 0f;
                float duration = 0.25f;
                float amp = 0.08f;

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float decay = 1f - Mathf.Clamp01(elapsed / duration);
                    float offset = Mathf.Sin(elapsed * 45f) * amp * decay;
                    transform.localPosition = basePos + new Vector3(offset, 0f, 0f);
                    yield return null;
                }
            }
            finally
            {
                transform.localPosition = basePos;
                m_feedbackRoutine = null;
            }
        }

        private bool CanPlayFeedback()
        {
            if (m_feedbackRoutine != null) return false;
            if (IsTravelling || InSlot) return false;
            return isActiveAndEnabled;
        }

        private void PlayRevealAnimation()
        {
            if (IsTravelling || !isActiveAndEnabled) return;
            StopRevealRoutine();
            m_revealRoutine = StartCoroutine(RevealRoutine());
        }

        private IEnumerator RevealRoutine()
        {
            float elapsed = 0f;
            float duration = 0.2f;
            Vector3 baseScale = transform.localScale;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float bounce = 1f + Mathf.Sin(t * Mathf.PI) * 0.18f;
                transform.localScale = baseScale * bounce;
                yield return null;
            }
            transform.localScale = baseScale;
            m_revealRoutine = null;
        }

        public void PlayDestroy(Action onComplete)
        {
            StopActiveRoutine();
            StopFeedbackRoutine();
            StopStateRoutine();
            StopLidRoutine();
            m_activeRoutine = StartCoroutine(DestroyRoutine(onComplete));
        }

        private IEnumerator DestroyRoutine(Action onComplete)
        {
            if (m_lid != null && m_lidProgress > 0f)
            {
                float hold = LidLastBeeHold;
                float held = 0f;
                while (held < hold)
                {
                    held += Time.deltaTime;
                    yield return null;
                }

                m_lidTargetOpen = false;
                yield return LidRoutine(false);
            }

            float elapsed = 0f;
            float duration = 0.18f;
            Vector3 startScale = transform.localScale;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
                yield return null;
            }
            transform.localScale = Vector3.zero;
            m_activeRoutine = null;
            onComplete?.Invoke();
        }

        public void RaiseTapped()
        {
            Tapped?.Invoke(this);
        }

        private void StopActiveRoutine()
        {
            IsTravelling = false;
            if (m_activeRoutine != null)
            {
                StopCoroutine(m_activeRoutine);
                m_activeRoutine = null;
            }
        }

        private void StopFeedbackRoutine()
        {
            if (m_feedbackRoutine != null)
            {
                StopCoroutine(m_feedbackRoutine);
                m_feedbackRoutine = null;
            }
        }

        private void StopRevealRoutine()
        {
            if (m_revealRoutine != null)
            {
                StopCoroutine(m_revealRoutine);
                m_revealRoutine = null;
            }
        }

        public void OnSpawn()
        {
            StopActiveRoutine();
            StopFeedbackRoutine();
            StopRevealRoutine();
            StopStateRoutine();
            StopLidRoutine();
            CacheAuthoredModelScale();
            m_isPickable = false;
            SetStateInstant(false);
            SnapLid(false);
            transform.localRotation = Quaternion.identity;
            if (m_collider != null) m_collider.enabled = true;
            gameObject.SetActive(true);
        }

        public void OnDespawn()
        {
            StopActiveRoutine();
            StopFeedbackRoutine();
            StopRevealRoutine();
            StopStateRoutine();
            StopLidRoutine();
            m_isPickable = false;
            ApplyModelHeight(1f);
            ApplyTextAlpha(1f);
            SnapLid(false);
            Tapped = null;
            RevealedChanged = null;
            DelayCleared = null;
            m_materials = null;
            ColorType = ColorType.None;
            InSlot = false;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }

        private void OnDisable()
        {
            StopActiveRoutine();
            StopFeedbackRoutine();
            StopRevealRoutine();
            StopStateRoutine();
            StopLidRoutine();
        }
    }
}
