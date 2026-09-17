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
    public sealed class BoardController : MonoBehaviour
    {
        private const float ApproachViewPenalty = 0.75f;
        private const float ClearFaceBonus = 4f;
        private const float RaycastRadiusScale = 1.05f;
        private const float DragVelocitySharpness = 20f;
        private const float MaxViewAngularSpeed = 1440f;
        private const float MinInertiaFloor = 0.001f;

        [SerializeField] private CellController m_cellPrefab;
        [SerializeField] private TileShardController m_shardPrefab;
        [SerializeField] private Transform m_boardRoot;

        private readonly List<CellController> m_cells = new List<CellController>();
        private readonly List<int> m_bucketIndex = new List<int>();
        private readonly List<byte> m_clearFaces = new List<byte>();
        private readonly Dictionary<HexCoord3, CellController> m_byCoord = new Dictionary<HexCoord3, CellController>();
        private readonly Dictionary<ColorType, List<CellController>> m_targetableByColor = new Dictionary<ColorType, List<CellController>>();
        private readonly Dictionary<ColorType, int> m_remainingNectarByColor = new Dictionary<ColorType, int>();
        private static readonly List<CellController> s_emptyBucket = new List<CellController>();
        private static readonly Vector3[] s_localFaceNormals = BuildLocalFaceNormals();

        private ColorMaterialMapping m_materials;
        private int m_aliveCount;
        private int m_totalNectarRemaining;
        private int m_totalNectarInitial;
        private float m_boardScale = 1f;
        private LevelData m_level;
        private LayoutConfig m_layout;

        private float m_layerPitch;
        private Vector3 m_localCenter;
        private Vector3 m_localHalfExtents;
        private float m_radiusLocal;
        private HexCoord3 m_latticeMin;
        private HexCoord3 m_latticeMax;
        private Vector3 m_viewForward = Vector3.forward;
        private float m_viewDepthSlope;

        private BoardViewSettings m_viewSettings = BoardViewSettings.Default;
        private float m_viewYaw;
        private float m_viewPitch;
        private float m_yawVelocity;
        private float m_pitchVelocity;
        private bool m_dragging;

        public event Action<CellController> CellDied;
        public event Action<CellController> CellUnlocked;
        public event Action TargetableChanged;

        public IReadOnlyList<CellController> Cells => m_cells;
        public int AliveCount => m_aliveCount;
        public int TotalNectarInitial => m_totalNectarInitial;
        public int TotalNectarRemaining => m_totalNectarRemaining;
        public bool IsCleared => m_aliveCount == 0 && m_totalNectarRemaining == 0;
        public float BoardScale => m_boardScale;
        public Transform BoardRoot => m_boardRoot;

        public bool HasVolume => m_cells.Count > 0 && m_boardRoot != null;
        public Vector3 BoardWorldCenter => m_boardRoot != null ? m_boardRoot.position : transform.position;
        public Quaternion BoardWorldRotation => m_boardRoot != null ? m_boardRoot.rotation : Quaternion.identity;
        public Vector3 BoardWorldHalfExtents => m_localHalfExtents * m_boardScale;
        public float BoardWorldRadius => m_radiusLocal * m_boardScale;
        public float LayerPitch => m_layerPitch;

        public float ViewYaw => m_viewYaw;
        public float ViewPitch => m_viewPitch;
        public bool IsDraggingView => m_dragging;
        public bool IsRotationEnabled => m_viewSettings.rotationEnabled;

        public void Build(LevelData level, ColorMaterialMapping materials, LayoutConfig layout)
        {
            ClearBoard();

            m_materials = materials;
            m_level = level;
            m_layout = layout;

            if (m_boardRoot == null)
            {
                var rootGo = new GameObject("BoardRoot");
                m_boardRoot = rootGo.transform;
                m_boardRoot.SetParent(transform, false);
            }

            ResetView();

            if (level == null || level.cells == null || layout == null) return;

            Vector3 visualScale = CellController.ComputeVisualScale(m_cellPrefab, layout.CellWidth, layout.CellDepth);
            m_layerPitch = ComputeLayerPitch(layout);
            float hexSize = layout.hexSize;

            for (int i = 0; i < level.cells.Count; i++)
            {
                CellDef def = level.cells[i];
                if (def == null) continue;

                HexCoord3 coord = new HexCoord3(def.q, def.r, def.layer);
                if (m_byCoord.ContainsKey(coord)) continue;

                Vector3 localPos = HexLayout.ToLocal3(coord, hexSize, m_layerPitch);

                CellController cell = LeanPool.Spawn(m_cellPrefab, m_boardRoot);
                cell.Initialize(m_cells.Count, coord, def.colorType, def.nectar, def.locked, localPos, visualScale, layout.CellWidth, layout.CellDepth, materials);
                cell.SetEnclosedCulling(layout.cullEnclosedCells);

                cell.Died += OnCellDiedInternal;
                cell.Unlocked += OnCellUnlockedInternal;

                m_cells.Add(cell);
                m_bucketIndex.Add(-1);
                m_clearFaces.Add(0);
                m_byCoord.Add(coord, cell);

                if (!m_targetableByColor.ContainsKey(cell.ColorType))
                {
                    m_targetableByColor.Add(cell.ColorType, new List<CellController>());
                }

                if (cell.Alive) m_aliveCount++;
                m_totalNectarInitial += cell.InitialNectar;

                int curNectar;
                if (m_remainingNectarByColor.TryGetValue(cell.ColorType, out curNectar))
                {
                    m_remainingNectarByColor[cell.ColorType] = curNectar + cell.Nectar;
                }
                else
                {
                    m_remainingNectarByColor.Add(cell.ColorType, cell.Nectar);
                }
            }

            ApplyLayoutInternal(true);
            ComputeLatticeBounds();
            RecomputeAllClearFaces();

            m_totalNectarRemaining = m_totalNectarInitial;
            RecomputeAllExposure();

            float delayStep = Mathf.Min(0.0022f, 1.2f / Mathf.Max(1, m_cells.Count));
            for (int i = 0; i < m_cells.Count; i++)
            {
                m_cells[i].PlayPopIn(i * delayStep, 0.28f);
            }
        }

        public void ApplyLayoutSizes()
        {
            if (m_layout == null || m_level == null || m_boardRoot == null || m_cells.Count == 0) return;

            for (int i = 0; i < m_cells.Count; i++)
            {
                CellController cell = m_cells[i];
                if (cell != null) cell.SetEnclosedCulling(m_layout.cullEnclosedCells);
            }

            ApplyLayoutInternal(false);
        }

        private void ApplyLayoutInternal(bool positionAllCells)
        {
            LayoutConfig layout = m_layout;
            if (layout == null || m_boardRoot == null || m_cells.Count == 0) return;

            Vector3 visualScale = CellController.ComputeVisualScale(m_cellPrefab, layout.CellWidth, layout.CellDepth);
            m_layerPitch = ComputeLayerPitch(layout);

            float hexSize = layout.hexSize;
            float absHex = Mathf.Abs(hexSize);
            Vector3 half = new Vector3(absHex * HexLayout.Sqrt3 * 0.5f, absHex, m_layerPitch * 0.5f);

            bool any = false;
            Vector3 min = Vector3.zero;
            Vector3 max = Vector3.zero;
            for (int i = 0; i < m_cells.Count; i++)
            {
                CellController cell = m_cells[i];
                if (cell == null) continue;

                Vector3 p = HexLayout.ToLocal3(cell.Coord, hexSize, m_layerPitch);
                if (!any)
                {
                    min = p - half;
                    max = p + half;
                    any = true;
                }
                else
                {
                    min = Vector3.Min(min, p - half);
                    max = Vector3.Max(max, p + half);
                }
            }

            if (!any) return;

            Vector3 size = new Vector3(
                Mathf.Max(0.001f, max.x - min.x),
                Mathf.Max(0.001f, max.y - min.y),
                Mathf.Max(0.001f, max.z - min.z));
            m_localCenter = (min + max) * 0.5f;
            m_localHalfExtents = size * 0.5f;

            float cellRadius = Mathf.Sqrt(absHex * absHex + half.z * half.z);
            float farthest = 0f;
            for (int i = 0; i < m_cells.Count; i++)
            {
                CellController cell = m_cells[i];
                if (cell == null) continue;

                Vector3 localPos = HexLayout.ToLocal3(cell.Coord, hexSize, m_layerPitch) - m_localCenter;
                float distance = localPos.magnitude;
                if (distance > farthest) farthest = distance;

                if (positionAllCells || cell.Alive || !cell.gameObject.activeSelf)
                {
                    cell.ApplyLayout(localPos, visualScale, layout.CellWidth, layout.CellDepth);
                }
            }
            m_radiusLocal = farthest + cellRadius;

            float frontFit = Mathf.Min(layout.BoardFitWidth / size.x, layout.BoardFitHeight / size.y);
            float rotateFit = layout.BoardRotateFitDiameter / Mathf.Max(0.0001f, 2f * m_radiusLocal);
            float gridScale = m_level != null ? m_level.gridScaleMultiplier : layout.referenceGridScale;
            float authored = Mathf.Clamp(gridScale / Mathf.Max(0.0001f, layout.referenceGridScale), 0.5f, 1f);
            m_boardScale = Mathf.Max(0.0001f, Mathf.Min(frontFit, rotateFit) * authored);

            m_boardRoot.localScale = new Vector3(m_boardScale, m_boardScale, m_boardScale);

            Vector2 authoredOffset = m_level != null ? m_level.gridPositionOffset : Vector2.zero;
            Vector2 offset = ClampOffsetInsidePanel(layout, authoredOffset, size * m_boardScale);
            float z = Mathf.Min(0f, layout.panelZ - m_radiusLocal * m_boardScale - layout.BoardPanelClearance);
            m_boardRoot.localPosition = new Vector3(
                layout.boardCentre.x + offset.x,
                layout.boardCentre.y + offset.y + z * m_viewDepthSlope,
                z);

            ApplyViewRotation();
        }

        private static float ComputeLayerPitch(LayoutConfig layout)
        {
            float pitch = layout.CellDepth / Mathf.Max(0.0001f, layout.tileGapScale);
            return Mathf.Max(0.0001f, pitch * layout.LayerSpacing);
        }

        private static Vector2 ClampOffsetInsidePanel(LayoutConfig layout, Vector2 authoredOffset, Vector3 contentSize)
        {
            float slackX = Mathf.Max(0f, (layout.PanelWidth - layout.BoardPanelPadding * 2f - contentSize.x) * 0.5f);
            float slackY = Mathf.Max(0f, (layout.PanelHeight - layout.BoardPanelPadding * 2f - contentSize.y) * 0.5f);
            return new Vector2(
                Mathf.Clamp(authoredOffset.x, -slackX, slackX),
                Mathf.Clamp(authoredOffset.y, -slackY, slackY));
        }

        public void SetViewForward(Vector3 forward)
        {
            float sq = forward.sqrMagnitude;
            m_viewForward = sq > 1e-8f ? forward / Mathf.Sqrt(sq) : Vector3.forward;
            m_viewDepthSlope = Mathf.Abs(m_viewForward.z) > 0.0001f ? m_viewForward.y / m_viewForward.z : 0f;

            if (m_cells.Count > 0) ApplyLayoutInternal(false);
        }

        public void ConfigureView(BoardViewSettings settings)
        {
            bool wasEnabled = m_viewSettings.rotationEnabled;
            m_viewSettings = settings;

            if (!settings.rotationEnabled)
            {
                m_dragging = false;
                m_yawVelocity = 0f;
                m_pitchVelocity = 0f;
                if (wasEnabled && m_level != null)
                {
                    ResetView();
                    return;
                }
            }

            m_viewPitch = ClampPitch(m_viewPitch);
            ApplyViewRotation();
        }

        public void ResetView()
        {
            m_dragging = false;
            m_yawVelocity = 0f;
            m_pitchVelocity = 0f;

            float yaw = m_level != null ? m_level.boardViewYaw : 0f;
            float pitch = m_level != null ? m_level.boardViewPitch : 0f;
            SetViewAngles(yaw, pitch);
        }

        public void SetViewAngles(float yaw, float pitch)
        {
            m_viewYaw = WrapYaw(yaw);
            m_viewPitch = ClampPitch(pitch);
            ApplyViewRotation();
        }

        public bool RaycastBoardVolume(Ray ray)
        {
            if (!HasVolume) return false;

            float radius = BoardWorldRadius * RaycastRadiusScale;
            if (radius <= 0f) return false;

            Vector3 oc = ray.origin - BoardWorldCenter;
            float c = Vector3.Dot(oc, oc) - radius * radius;
            if (c <= 0f) return true;

            float b = Vector3.Dot(oc, ray.direction);
            if (b > 0f) return false;

            return b * b - c >= 0f;
        }

        public void BeginViewDrag()
        {
            if (!IsRotationEnabled || !HasVolume) return;

            m_dragging = true;
            m_yawVelocity = 0f;
            m_pitchVelocity = 0f;
        }

        public void DragView(Vector2 screenDelta, float dt)
        {
            if (!m_dragging) return;

            if (!IsRotationEnabled)
            {
                EndViewDrag();
                return;
            }

            if (dt <= 0f) return;

            float degreesPerPixel = m_viewSettings.dragDegreesPerScreenHeight / Mathf.Max(1, Screen.height);
            float yawDelta = -screenDelta.x * degreesPerPixel;
            float previousPitch = m_viewPitch;

            m_viewYaw = WrapYaw(m_viewYaw + yawDelta);
            m_viewPitch = ClampPitch(m_viewPitch + screenDelta.y * degreesPerPixel);

            float instantYaw = Mathf.Clamp(yawDelta / dt, -MaxViewAngularSpeed, MaxViewAngularSpeed);
            float instantPitch = Mathf.Clamp((m_viewPitch - previousPitch) / dt, -MaxViewAngularSpeed, MaxViewAngularSpeed);
            float blend = 1f - Mathf.Exp(-DragVelocitySharpness * dt);
            m_yawVelocity = Mathf.Lerp(m_yawVelocity, instantYaw, blend);
            m_pitchVelocity = Mathf.Lerp(m_pitchVelocity, instantPitch, blend);

            ApplyViewRotation();
        }

        public void EndViewDrag()
        {
            m_dragging = false;
            if (!IsRotationEnabled)
            {
                m_yawVelocity = 0f;
                m_pitchVelocity = 0f;
            }
        }

        public void TickView(float dt)
        {
            if (m_dragging || dt <= 0f) return;

            if (!IsRotationEnabled)
            {
                m_yawVelocity = 0f;
                m_pitchVelocity = 0f;
                return;
            }

            float speed = Mathf.Sqrt(m_yawVelocity * m_yawVelocity + m_pitchVelocity * m_pitchVelocity);
            if (speed < Mathf.Max(MinInertiaFloor, m_viewSettings.minInertiaSpeed))
            {
                m_yawVelocity = 0f;
                m_pitchVelocity = 0f;
                return;
            }

            m_viewYaw = WrapYaw(m_viewYaw + m_yawVelocity * dt);

            float wantedPitch = m_viewPitch + m_pitchVelocity * dt;
            m_viewPitch = ClampPitch(wantedPitch);
            if (m_viewPitch != wantedPitch) m_pitchVelocity = 0f;

            float decay = Mathf.Exp(-Mathf.Max(0f, m_viewSettings.inertiaDamping) * dt);
            m_yawVelocity *= decay;
            m_pitchVelocity *= decay;

            ApplyViewRotation();
        }

        private void ApplyViewRotation()
        {
            if (m_boardRoot == null) return;

            float roll = m_level != null ? m_level.gridRotation : 0f;
            m_boardRoot.localRotation =
                Quaternion.AngleAxis(m_viewPitch, Vector3.right) *
                Quaternion.AngleAxis(m_viewYaw, Vector3.up) *
                Quaternion.Euler(0f, 0f, roll);
        }

        private static float WrapYaw(float yaw)
        {
            if (float.IsNaN(yaw) || float.IsInfinity(yaw)) return 0f;

            float wrapped = Mathf.Repeat(yaw + 180f, 360f) - 180f;
            if (wrapped <= -180f) wrapped += 360f;
            return wrapped;
        }

        private float ClampPitch(float pitch)
        {
            if (float.IsNaN(pitch) || float.IsInfinity(pitch)) return 0f;

            float limit = Mathf.Clamp(m_viewSettings.maxPitch, 0f, 89f);
            return Mathf.Clamp(pitch, -limit, limit);
        }

        public void ClearBoard()
        {
            for (int i = 0; i < m_cells.Count; i++)
            {
                CellController c = m_cells[i];
                if (c == null) continue;

                c.Died -= OnCellDiedInternal;
                c.Unlocked -= OnCellUnlockedInternal;

                if (LeanPool.Links.ContainsKey(c.gameObject))
                {
                    LeanPool.Despawn(c.gameObject);
                }
            }
            m_cells.Clear();
            m_bucketIndex.Clear();
            m_clearFaces.Clear();
            m_byCoord.Clear();
            m_targetableByColor.Clear();
            m_remainingNectarByColor.Clear();
            m_aliveCount = 0;
            m_totalNectarRemaining = 0;
            m_totalNectarInitial = 0;
            m_localHalfExtents = Vector3.zero;
            m_localCenter = Vector3.zero;
            m_radiusLocal = 0f;
            m_dragging = false;
            m_yawVelocity = 0f;
            m_pitchVelocity = 0f;
        }

        public CellController CellById(int id)
        {
            if (id < 0 || id >= m_cells.Count) return null;
            return m_cells[id];
        }

        public bool TryGetCell(HexCoord3 coord, out CellController cell)
        {
            return m_byCoord.TryGetValue(coord, out cell);
        }

        public Vector3 CellWorldPosition(int cellId)
        {
            CellController c = CellById(cellId);
            if (c != null && m_boardRoot != null)
            {
                return m_boardRoot.TransformPoint(c.HomeLocalPos);
            }
            return Vector3.zero;
        }

        public byte OpenFacesOf(int cellId)
        {
            CellController c = CellById(cellId);
            if (c == null || !c.Alive) return 0;
            return c.OpenFaces;
        }

        public byte ClearFacesOf(int cellId)
        {
            if (cellId < 0 || cellId >= m_clearFaces.Count) return 0;
            return m_clearFaces[cellId];
        }

        public float FaceFreeDistance(int cellId, int face)
        {
            CellController cell = CellById(cellId);
            if (cell == null || face < 0 || face >= HexCoord3.DirectionCount) return 0f;
            if ((m_clearFaces[cellId] & (1 << face)) != 0) return float.PositiveInfinity;

            int steps = 0;
            HexCoord3 direction = HexCoord3.Directions[face];
            HexCoord3 probe = cell.Coord;
            while (true)
            {
                probe = probe + direction;
                if (!IsInsideLattice(probe)) return float.PositiveInfinity;

                CellController occupant;
                if (m_byCoord.TryGetValue(probe, out occupant) && occupant != null && occupant.Alive) break;
                steps++;
            }

            float stepLocal = face >= HexCoord3.LateralDirectionCount
                ? m_layerPitch
                : HexLayout.Sqrt3 * Mathf.Abs(m_layout != null ? m_layout.hexSize : 0.5f);
            return (steps + 0.5f) * stepLocal * m_boardScale;
        }

        public Vector3 WorldFaceNormal(int face)
        {
            if (m_boardRoot == null) return Vector3.back;
            return m_boardRoot.rotation * LocalFaceNormal(face);
        }

        public int ChooseApproachFace(int cellId, Vector3 fromWorld)
        {
            CellController cell = CellById(cellId);
            if (cell == null || !cell.Alive) return -1;

            int mask = cell.OpenFaces;
            if (mask == 0) return -1;

            Vector3 toBee = fromWorld - CellWorldPosition(cellId);
            float distance = toBee.magnitude;
            Vector3 dirToBee = distance > 0.0001f ? toBee / distance : Vector3.zero;
            Quaternion rotation = BoardWorldRotation;
            int clear = m_clearFaces[cellId];

            int best = -1;
            float bestScore = float.NegativeInfinity;
            for (int d = 0; d < HexCoord3.DirectionCount && mask != 0; d++)
            {
                int bit = 1 << d;
                if ((mask & bit) == 0) continue;
                mask &= ~bit;

                Vector3 normal = rotation * s_localFaceNormals[d];
                float score = Vector3.Dot(normal, dirToBee) - ApproachViewPenalty * Mathf.Max(0f, Vector3.Dot(normal, m_viewForward));
                if ((clear & bit) != 0) score += ClearFaceBonus;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = d;
                }
            }

            return best;
        }

        public bool HasTargetableColor(ColorType colorType)
        {
            List<CellController> bucket;
            return m_targetableByColor.TryGetValue(colorType, out bucket) && bucket.Count > 0;
        }

        public int CountTargetable(ColorType colorType)
        {
            List<CellController> bucket;
            return m_targetableByColor.TryGetValue(colorType, out bucket) ? bucket.Count : 0;
        }

        public IReadOnlyList<CellController> TargetableOfColor(ColorType colorType)
        {
            List<CellController> bucket;
            if (m_targetableByColor.TryGetValue(colorType, out bucket)) return bucket;
            return s_emptyBucket;
        }

        public int RemainingNectarOfColor(ColorType colorType)
        {
            int remaining;
            return m_remainingNectarByColor.TryGetValue(colorType, out remaining) ? remaining : 0;
        }

        public bool ApplyDrain(int cellId)
        {
            CellController cell = CellById(cellId);
            if (cell == null || !cell.Alive || cell.Nectar <= 0) return false;

            bool drained = cell.ApplyDrain();
            if (!drained) return false;

            if (m_totalNectarRemaining > 0) m_totalNectarRemaining--;

            int rem;
            if (m_remainingNectarByColor.TryGetValue(cell.ColorType, out rem))
            {
                m_remainingNectarByColor[cell.ColorType] = rem > 0 ? rem - 1 : 0;
            }

            return true;
        }

        private void OnCellDiedInternal(CellController cell)
        {
            if (cell == null) return;

            byte facesAtDeath = cell.OpenFaces;
            if (m_aliveCount > 0) m_aliveCount--;

            UpdateTargetableMembership(cell);
            UnlockNeighbors(cell);
            UpdateClearFacesAround(cell);
            RecomputeAround(cell);
            SpawnShards(cell, facesAtDeath);

            CellDied?.Invoke(cell);
            TargetableChanged?.Invoke();
        }

        private void OnCellUnlockedInternal(CellController cell)
        {
            if (cell == null) return;
            UpdateTargetableMembership(cell);
            CellUnlocked?.Invoke(cell);
            TargetableChanged?.Invoke();
        }

        private void UnlockNeighbors(CellController cell)
        {
            HexCoord3 origin = cell.Coord;
            for (int d = 0; d < HexCoord3.DirectionCount; d++)
            {
                CellController neighbor;
                if (!m_byCoord.TryGetValue(origin.Neighbor(d), out neighbor)) continue;
                if (neighbor == null || !neighbor.Alive || !neighbor.Locked) continue;

                neighbor.Unlock();
            }
        }

        public void RecomputeAllExposure()
        {
            for (int i = 0; i < m_cells.Count; i++)
            {
                CellController cell = m_cells[i];
                if (cell == null) continue;
                cell.SetExposure(ComputeOpenFaces(cell));
                UpdateTargetableMembership(cell);
            }
            TargetableChanged?.Invoke();
        }

        public void RecomputeAround(CellController justDied)
        {
            if (justDied == null) return;

            justDied.SetExposure(ComputeOpenFaces(justDied));
            UpdateTargetableMembership(justDied);

            HexCoord3 origin = justDied.Coord;
            for (int d = 0; d < HexCoord3.DirectionCount; d++)
            {
                CellController neighbor;
                if (!m_byCoord.TryGetValue(origin.Neighbor(d), out neighbor)) continue;
                if (neighbor == null) continue;

                neighbor.SetExposure(ComputeOpenFaces(neighbor));
                UpdateTargetableMembership(neighbor);
            }
        }

        private byte ComputeOpenFaces(CellController cell)
        {
            if (cell == null || !cell.Alive) return 0;
            return ComputeFaceMask(cell.Coord);
        }

        private byte ComputeFaceMask(HexCoord3 origin)
        {
            int mask = 0;
            for (int d = 0; d < HexCoord3.DirectionCount; d++)
            {
                CellController neighbor;
                if (!m_byCoord.TryGetValue(origin.Neighbor(d), out neighbor) || neighbor == null || !neighbor.Alive)
                {
                    mask |= 1 << d;
                }
            }
            return (byte)mask;
        }

        private void UpdateTargetableMembership(CellController cell)
        {
            int id = cell.CellId;
            if (id < 0 || id >= m_bucketIndex.Count || m_cells[id] != cell) return;

            List<CellController> bucket;
            if (!m_targetableByColor.TryGetValue(cell.ColorType, out bucket)) return;

            int index = m_bucketIndex[id];
            bool isTargetable = cell.IsTargetable;

            if (isTargetable && index < 0)
            {
                m_bucketIndex[id] = bucket.Count;
                bucket.Add(cell);
            }
            else if (!isTargetable && index >= 0)
            {
                int last = bucket.Count - 1;
                CellController moved = bucket[last];
                bucket[index] = moved;
                m_bucketIndex[moved.CellId] = index;
                bucket.RemoveAt(last);
                m_bucketIndex[id] = -1;
            }
        }

        private void ComputeLatticeBounds()
        {
            int minQ = int.MaxValue, minR = int.MaxValue, minL = int.MaxValue;
            int maxQ = int.MinValue, maxR = int.MinValue, maxL = int.MinValue;
            for (int i = 0; i < m_cells.Count; i++)
            {
                CellController cell = m_cells[i];
                if (cell == null) continue;

                HexCoord3 c = cell.Coord;
                if (c.Q < minQ) minQ = c.Q;
                if (c.Q > maxQ) maxQ = c.Q;
                if (c.R < minR) minR = c.R;
                if (c.R > maxR) maxR = c.R;
                if (c.Layer < minL) minL = c.Layer;
                if (c.Layer > maxL) maxL = c.Layer;
            }

            if (minQ > maxQ)
            {
                m_latticeMin = new HexCoord3(0, 0, 0);
                m_latticeMax = new HexCoord3(-1, -1, -1);
                return;
            }

            m_latticeMin = new HexCoord3(minQ, minR, minL);
            m_latticeMax = new HexCoord3(maxQ, maxR, maxL);
        }

        private bool IsInsideLattice(HexCoord3 c)
        {
            return c.Q >= m_latticeMin.Q && c.Q <= m_latticeMax.Q
                && c.R >= m_latticeMin.R && c.R <= m_latticeMax.R
                && c.Layer >= m_latticeMin.Layer && c.Layer <= m_latticeMax.Layer;
        }

        private bool IsRayClear(HexCoord3 origin, int dir)
        {
            HexCoord3 direction = HexCoord3.Directions[dir];
            HexCoord3 probe = origin;
            while (true)
            {
                probe = probe + direction;
                if (!IsInsideLattice(probe)) return true;

                CellController occupant;
                if (m_byCoord.TryGetValue(probe, out occupant) && occupant != null && occupant.Alive) return false;
            }
        }

        private void RecomputeAllClearFaces()
        {
            for (int i = 0; i < m_cells.Count; i++)
            {
                CellController cell = m_cells[i];
                if (cell == null) continue;

                int mask = 0;
                for (int d = 0; d < HexCoord3.DirectionCount; d++)
                {
                    if (IsRayClear(cell.Coord, d)) mask |= 1 << d;
                }
                m_clearFaces[i] = (byte)mask;
            }
        }

        private void UpdateClearFacesAround(CellController dead)
        {
            HexCoord3 origin = dead.Coord;
            for (int d = 0; d < HexCoord3.DirectionCount; d++)
            {
                HexCoord3 direction = HexCoord3.Directions[d];
                HexCoord3 probe = origin;
                CellController hit = null;
                while (true)
                {
                    probe = probe + direction;
                    if (!IsInsideLattice(probe)) break;

                    CellController occupant;
                    if (m_byCoord.TryGetValue(probe, out occupant) && occupant != null && occupant.Alive)
                    {
                        hit = occupant;
                        break;
                    }
                }

                if (hit == null) continue;

                int back = HexCoord3.OppositeDirection(d);
                int bit = 1 << back;
                if ((m_clearFaces[hit.CellId] & bit) != 0) continue;
                if (IsRayClear(hit.Coord, back)) m_clearFaces[hit.CellId] = (byte)(m_clearFaces[hit.CellId] | bit);
            }
        }

        private void SpawnShards(CellController cell, byte facesAtDeath)
        {
            if (m_shardPrefab == null || cell == null) return;

            int mask = facesAtDeath != 0 ? facesAtDeath : ComputeFaceMask(cell.Coord);
            if (mask == 0) mask = HexCoord3.AllFacesMask;

            Quaternion rotation = BoardWorldRotation;
            Vector3 normal = Vector3.back;
            float mostFacing = float.PositiveInfinity;
            for (int d = 0; d < HexCoord3.DirectionCount; d++)
            {
                if ((mask & (1 << d)) == 0) continue;

                Vector3 candidate = rotation * s_localFaceNormals[d];
                if (candidate.z < mostFacing)
                {
                    mostFacing = candidate.z;
                    normal = candidate;
                }
            }

            Vector3 worldPos = CellWorldPosition(cell.CellId);
            Material shardMaterial = m_materials != null ? m_materials.Shard(cell.ColorType) : null;
            int count = 5;
            float shardBaseScale = 0.115f * m_boardScale;
            for (int i = 0; i < count; i++)
            {
                var shard = LeanPool.Spawn(m_shardPrefab, transform);

                Vector3 offsetSpread = UnityEngine.Random.insideUnitSphere;
                offsetSpread -= normal * Vector3.Dot(offsetSpread, normal);
                Vector3 jitter = (offsetSpread * 0.09f + normal * UnityEngine.Random.Range(-0.02f, 0.05f)) * m_boardScale;

                Vector3 lateral = UnityEngine.Random.insideUnitSphere;
                lateral -= normal * Vector3.Dot(lateral, normal);

                float s = shardBaseScale * UnityEngine.Random.Range(0.5f, 1.5f);
                Vector3 dir = (normal * 1f + lateral * 0.6f + Vector3.up * 0.35f).normalized;
                Vector3 vel = dir * 3.1f * UnityEngine.Random.Range(0.6f, 1.35f);
                shard.Initialize(worldPos + jitter, vel, shardMaterial, s);
            }
        }

        private static Vector3 LocalFaceNormal(int face)
        {
            if (face >= 0 && face < s_localFaceNormals.Length) return s_localFaceNormals[face];
            return HexLayout.FaceNormal(face);
        }

        private static Vector3[] BuildLocalFaceNormals()
        {
            var normals = new Vector3[HexCoord3.DirectionCount];
            for (int d = 0; d < normals.Length; d++)
            {
                normals[d] = HexLayout.FaceNormal(d);
            }
            return normals;
        }

        private void OnDestroy()
        {
            ClearBoard();
        }
    }
}
