using System.Collections.Generic;
using UnityEngine;
using HoneyBeeRush.Core;
using HoneyBeeRush.Data;
using HoneyBeeRush.Gameplay.Core;
using HoneyBeeRush.Gameplay.Domain;

namespace HoneyBeeRush.Bees
{
    public sealed class BeeSim
    {
        private const int InitialCapacity = 128;
        private const int BucketCount = 4096;
        private const int BucketMask = BucketCount - 1;
        private const int MaxAssignmentsPerTick = 64;
        private const byte HomeStageApproach = 0;
        private const byte HomeStageMouth = 1;
        private const byte HomeStageEnter = 2;
        private const byte HomeStageEgress = 3;
        private const float BlockedHoverFraction = 0.6f;
        private const float EgressWanderMul = 0.15f;
        private const float EgressSpeedMul = 0.6f;
        private const float TwoPi = 6.2831853f;
        private const float FinaleExitHeight = 12f;
        private const float GrowStartScale = 0.2f;
        private const float Sqrt2 = 1.41421356f;
        private const float MinVolumeAxis = 0.05f;
        private const float OrbitRadius = 1.12f;
        private const float AntipodalDot = -0.985f;
        private const float InsideDirectDot = 0.35f;
        private const float MaxPitchLimit = 85f;

        private readonly BoardController boardController;
        private readonly BeeConfig cfg;

        private Vector3[] pos;
        private Vector3[] vel;
        private Vector3[] heading;
        private Vector3[] anchor;
        private Vector3[] wanderPoint;
        private Vector3[] enterFrom;
        private Quaternion[] rot;
        private BeeState[] state;
        private bool[] alive;
        private bool[] cargo;
        private ColorType[] colorIdx;
        private int[] slotIdx;
        private int[] targetCell;
        private int[] targetFace;
        private int[] egressCell;
        private int[] egressFace;
        private float[] speedMul;
        private float[] wingPhase;
        private float[] phase;
        private float[] drainTimer;
        private float[] targetTimer;
        private float[] idleTimer;
        private float[] wanderAngle;
        private float[] wanderRepick;
        private float[] headingAngle;
        private float[] flightPitch;
        private float[] turnRate;
        private float[] finaleTimer;
        private float[] finaleAngle;
        private float[] finaleRise;
        private float[] homeTimer;
        private float[] enterTimer;
        private float[] visualScale;
        private float[] growTimer;
        private float[] growDuration;
        private byte[] homeStage;
        private int[] cellX;
        private int[] cellY;
        private int[] nextInBucket;
        private int[] freeIds;

        private readonly int[] bucketHead = new int[BucketCount];
        private readonly Dictionary<int, int> claims = new Dictionary<int, int>(512);
        private readonly HashSet<int> consumed = new HashSet<int>();

        private int count;
        private int freeCount;
        private int aliveCount;
        private int assignCursor;

        private Vector3 hivePos = Vector3.zero;
        private Vector3 hiveForward = Vector3.back;
        private float cruiseZ = -1.6f;
        private float time;
        private bool finale;

        private bool m_hasVolume;
        private Vector3 m_volCenter = Vector3.zero;
        private Quaternion m_volRot = Quaternion.identity;
        private Quaternion m_volInvRot = Quaternion.identity;
        private Vector3 m_volAxes = Vector3.one;
        private float m_volRadius;
        private float m_flightZ = -1.6f;
        private Vector3 m_camUnit = Vector3.back;
        private bool m_hasPrevVolume;
        private Vector3 m_prevVolCenter = Vector3.zero;
        private Quaternion m_prevVolRot = Quaternion.identity;
        private float m_orbitLeadRad = 40f * Mathf.Deg2Rad;
        private float m_orbitCos = Mathf.Cos(40f * Mathf.Deg2Rad);

        public System.Func<int, Vector3> CellWorldPosition { get; set; }
        public float TimeScale { get; set; } = 1f;

        public event System.Action<int> DrainStarted;
        public event System.Action<int> DrainCompleted;
        public event System.Action<int, bool> Returned;
        public event System.Action<int> BeeDespawned;

        public BeeSim(BoardController boardController, BeeConfig config)
        {
            this.boardController = boardController;
            cfg = config ?? new BeeConfig();
            Allocate(InitialCapacity);
            for (int b = 0; b < BucketCount; b++) bucketHead[b] = -1;
        }

        public int AliveCount { get { return aliveCount; } }

        public int Capacity { get { return pos.Length; } }

        public void SetHive(Vector3 hivePos, Vector3 hiveForward)
        {
            this.hivePos = hivePos;
            this.hiveForward = hiveForward.sqrMagnitude > 0.0001f ? hiveForward.normalized : Vector3.back;
        }

        public void SetFlightPlane(float cruiseZ)
        {
            this.cruiseZ = cruiseZ;
            m_flightZ = m_hasVolume ? Mathf.Min(cruiseZ, m_volCenter.z - m_volRadius - cfg.BoardClearance) : cruiseZ;
        }

        public int Spawn(Vector3 spawnPos, int slotIndex, ColorType colorType)
        {
            return Spawn(spawnPos, slotIndex, colorType, 1f, 0f);
        }

        public int Spawn(Vector3 spawnPos, int slotIndex, ColorType colorType, float scatterScale, float growDuration)
        {
            int id;
            if (freeCount > 0)
            {
                id = freeIds[--freeCount];
            }
            else
            {
                EnsureCapacity(count + 1);
                id = count;
                count++;
            }

            float scatter = cfg.spawnScatter * Mathf.Max(0f, scatterScale);
            float sx = Random.Range(-scatter, scatter);
            float sy = Random.Range(-scatter, scatter);
            float sz = Random.Range(-scatter, scatter) * 0.5f;

            pos[id] = new Vector3(spawnPos.x + sx, spawnPos.y + sy, spawnPos.z + sz);
            anchor[id] = spawnPos;

            float kick = cfg.maxSpeedOut * 0.35f;
            Vector3 dir = new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(0.5f, 1.2f), -0.55f);
            float dm = dir.magnitude;
            vel[id] = dm > 0.0001f ? dir * (kick / dm) : Vector3.up * kick;
            float hxy = vel[id].x * vel[id].x + vel[id].y * vel[id].y;
            if (hxy > 1e-8f)
            {
                float invh = 1f / Mathf.Sqrt(hxy);
                heading[id] = new Vector3(vel[id].x * invh, vel[id].y * invh, 0f);
            }
            else
            {
                heading[id] = Vector3.up;
            }
            headingAngle[id] = Mathf.Atan2(heading[id].y, heading[id].x) * Mathf.Rad2Deg;
            flightPitch[id] = 0f;
            turnRate[id] = 0f;
            rot[id] = Quaternion.identity;

            state[id] = finale ? BeeState.Finale : BeeState.Approaching;
            alive[id] = true;
            cargo[id] = false;
            colorIdx[id] = colorType;
            slotIdx[id] = slotIndex;
            targetCell[id] = -1;
            targetFace[id] = -1;
            egressCell[id] = -1;
            egressFace[id] = -1;
            speedMul[id] = Random.Range(1f - cfg.speedJitter, 1f + cfg.speedJitter);
            wingPhase[id] = Random.Range(0f, TwoPi);
            phase[id] = Random.Range(0f, TwoPi);
            drainTimer[id] = 0f;
            targetTimer[id] = 0f;
            idleTimer[id] = 0f;
            wanderAngle[id] = Random.Range(0f, TwoPi);
            wanderRepick[id] = 0f;
            wanderPoint[id] = spawnPos;
            finaleTimer[id] = 0f;
            finaleAngle[id] = Random.Range(0f, TwoPi);
            finaleRise[id] = 0f;
            homeTimer[id] = 0f;
            enterTimer[id] = 0f;
            enterFrom[id] = pos[id];
            bool grows = growDuration > 0f;
            growTimer[id] = 0f;
            this.growDuration[id] = grows ? growDuration : 0f;
            visualScale[id] = grows ? GrowStartScale : 1f;
            homeStage[id] = HomeStageApproach;
            cellX[id] = 0;
            cellY[id] = 0;
            nextInBucket[id] = -1;

            aliveCount++;
            return id;
        }

        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            dt *= TimeScale;
            if (dt <= 0f) return;
            if (dt > 0.1f) dt = 0.1f;

            time += dt;

            CacheBoardVolume();
            ClearClaims();
            CollectClaims();
            Assign();
            Move(dt);
            DrainTick(dt);
        }

        public bool IsAlive(int id)
        {
            return id >= 0 && id < count && alive[id];
        }

        public Vector3 Position(int id)
        {
            return IsAlive(id) ? pos[id] : Vector3.zero;
        }

        public Quaternion Rotation(int id)
        {
            return IsAlive(id) ? rot[id] : Quaternion.identity;
        }

        public ColorType ColorOf(int id)
        {
            return IsAlive(id) ? colorIdx[id] : ColorType.None;
        }

        public BeeState State(int id)
        {
            return IsAlive(id) ? state[id] : BeeState.Dead;
        }

        public float WingPhase(int id)
        {
            return IsAlive(id) ? wingPhase[id] : 0f;
        }

        public int SlotIndex(int id)
        {
            return IsAlive(id) ? slotIdx[id] : -1;
        }

        public bool HasCargo(int id)
        {
            return IsAlive(id) && cargo[id];
        }

        public float VisualScale(int id)
        {
            return IsAlive(id) ? visualScale[id] : 1f;
        }

        public int TargetCell(int id)
        {
            return IsAlive(id) ? targetCell[id] : -1;
        }

        public int PendingDrainCount(ColorType colorType)
        {
            int n = 0;
            for (int i = 0; i < count; i++)
            {
                if (!alive[i]) continue;
                if (colorIdx[i] != colorType) continue;
                if (cargo[i]) continue;

                BeeState s = state[i];
                if (s == BeeState.Approaching || s == BeeState.ToCell || s == BeeState.Draining) n++;
            }
            return n;
        }

        public int CountInState(BeeState state)
        {
            int n = 0;
            for (int i = 0; i < count; i++)
            {
                if (!alive[i]) continue;
                if (this.state[i] == state) n++;
            }
            return n;
        }

        public void SetFinale(bool on)
        {
            if (finale == on) return;
            finale = on;

            for (int i = 0; i < count; i++)
            {
                if (!alive[i]) continue;

                if (on)
                {
                    bool had = cargo[i];
                    cargo[i] = false;
                    targetCell[i] = -1;
                    targetFace[i] = -1;
                    drainTimer[i] = 0f;
                    targetTimer[i] = 0f;
                    idleTimer[i] = 0f;
                    state[i] = BeeState.Finale;
                    finaleTimer[i] = 0f;
                    finaleRise[i] = 0f;
                    homeStage[i] = HomeStageApproach;
                    enterTimer[i] = 0f;
                    ClearGrow(i);
                    visualScale[i] = 1f;
                    float dx = pos[i].x - hivePos.x;
                    float dy = pos[i].y - hivePos.y;
                    finaleAngle[i] = (dx * dx + dy * dy) > 0.0001f ? Mathf.Atan2(dy, dx) : Random.Range(0f, TwoPi);
                    if (had)
                    {
                        int slot = slotIdx[i];
                        slotIdx[i] = -1;
                        if (slot >= 0 && Returned != null) Returned(slot, true);
                    }
                }
                else if (state[i] == BeeState.Finale)
                {
                    targetCell[i] = -1;
                    targetFace[i] = -1;
                    BeginReturn(i);
                }
            }
        }

        public void RecallAll()
        {
            for (int i = 0; i < count; i++)
            {
                if (!alive[i]) continue;
                targetCell[i] = -1;
                targetFace[i] = -1;
                drainTimer[i] = 0f;
                targetTimer[i] = 0f;
                idleTimer[i] = 0f;
                BeginReturn(i);
            }
            claims.Clear();
        }

        public void ClearAll()
        {
            for (int i = 0; i < count; i++)
            {
                if (!alive[i]) continue;
                alive[i] = false;
                state[i] = BeeState.Dead;
                targetCell[i] = -1;
                targetFace[i] = -1;
                cargo[i] = false;
                ClearGrow(i);
                visualScale[i] = 1f;
                if (BeeDespawned != null) BeeDespawned(i);
            }

            count = 0;
            freeCount = 0;
            aliveCount = 0;
            assignCursor = 0;
            finale = false;
            time = 0f;
            m_hasVolume = false;
            m_hasPrevVolume = false;
            claims.Clear();
            consumed.Clear();
            for (int b = 0; b < BucketCount; b++) bucketHead[b] = -1;
        }

        private void CacheBoardVolume()
        {
            m_prevVolCenter = m_volCenter;
            m_prevVolRot = m_volRot;
            m_hasPrevVolume = m_hasVolume;

            float clearance = cfg.BoardClearance;
            m_orbitLeadRad = Mathf.Clamp(cfg.OrbitLeadAngle, 1f, 180f) * Mathf.Deg2Rad;
            m_orbitCos = Mathf.Cos(m_orbitLeadRad);

            m_hasVolume = boardController != null && boardController.HasVolume;
            if (!m_hasVolume)
            {
                m_hasPrevVolume = false;
                m_volCenter = Vector3.zero;
                m_volRot = Quaternion.identity;
                m_volInvRot = Quaternion.identity;
                m_volAxes = Vector3.one;
                m_volRadius = 0f;
                m_flightZ = cruiseZ;
                m_camUnit = Vector3.back;
                return;
            }

            m_volCenter = boardController.BoardWorldCenter;

            Quaternion r = boardController.BoardWorldRotation;
            float rq = r.x * r.x + r.y * r.y + r.z * r.z + r.w * r.w;
            if (rq < 1e-6f)
            {
                r = Quaternion.identity;
            }
            else if (Mathf.Abs(rq - 1f) > 1e-3f)
            {
                float rinv = 1f / Mathf.Sqrt(rq);
                r = new Quaternion(r.x * rinv, r.y * rinv, r.z * rinv, r.w * rinv);
            }
            m_volRot = r;
            m_volInvRot = Quaternion.Inverse(r);

            Vector3 half = boardController.BoardWorldHalfExtents;
            m_volAxes = new Vector3(
                Mathf.Max(MinVolumeAxis, Mathf.Abs(half.x) * Sqrt2 + clearance),
                Mathf.Max(MinVolumeAxis, Mathf.Abs(half.y) * Sqrt2 + clearance),
                Mathf.Max(MinVolumeAxis, Mathf.Abs(half.z) * Sqrt2 + clearance));

            m_volRadius = Mathf.Max(0f, boardController.BoardWorldRadius);
            m_flightZ = Mathf.Min(cruiseZ, m_volCenter.z - m_volRadius - clearance);

            Vector3 cam = DirToUnit(Vector3.back);
            float camSq = cam.sqrMagnitude;
            m_camUnit = camSq > 1e-8f ? cam / Mathf.Sqrt(camSq) : Vector3.back;
        }

        private Vector3 ToUnit(Vector3 world)
        {
            Vector3 l = m_volInvRot * (world - m_volCenter);
            return new Vector3(l.x / m_volAxes.x, l.y / m_volAxes.y, l.z / m_volAxes.z);
        }

        private Vector3 FromUnit(Vector3 u)
        {
            return m_volCenter + m_volRot * Vector3.Scale(u, m_volAxes);
        }

        private Vector3 DirToUnit(Vector3 dir)
        {
            Vector3 l = m_volInvRot * dir;
            return new Vector3(l.x / m_volAxes.x, l.y / m_volAxes.y, l.z / m_volAxes.z);
        }

        private float LaneDistance(Vector3 w, Vector3 n)
        {
            float hover = cfg.drainHoverHeight;
            if (!m_hasVolume) return hover + cfg.LaneExtra;

            Vector3 a = ToUnit(w);
            Vector3 d = DirToUnit(n);
            float qa = Vector3.Dot(d, d);
            float qb = Vector3.Dot(a, d);
            float qc = Vector3.Dot(a, a) - 1f;
            float disc = qb * qb - qa * qc;

            float t = 0f;
            if (qa >= 1e-8f && disc >= 0f)
            {
                t = (-qb + Mathf.Sqrt(disc)) / qa;
                if (t < 0f) t = 0f;
            }

            return Mathf.Max(t, hover) + cfg.LaneExtra;
        }

        private Vector3 RouteAround(Vector3 from, Vector3 goal)
        {
            if (!m_hasVolume) return goal;

            Vector3 a = ToUnit(from);
            Vector3 b = ToUnit(goal);
            Vector3 ab = b - a;
            float abSq = ab.sqrMagnitude;
            float s = abSq > 1e-8f ? Mathf.Clamp01(-Vector3.Dot(a, ab) / abSq) : 0f;
            Vector3 closest = a + ab * s;
            if (closest.sqrMagnitude >= 1f) return goal;

            float ra = a.magnitude;
            float rb = b.magnitude;
            Vector3 da = ra > 0.0001f ? a / ra : m_camUnit;
            Vector3 db = rb > 0.0001f ? b / rb : m_camUnit;
            float cosAB = Vector3.Dot(da, db);

            if (rb < 1f && cosAB >= m_orbitCos) return goal;

            if (ra < 1f)
            {
                if (cosAB >= InsideDirectDot) return goal;
                return FromUnit(da * OrbitRadius);
            }

            Vector3 dir;
            if (cosAB < AntipodalDot)
            {
                Vector3 side = m_camUnit - da * Vector3.Dot(m_camUnit, da);
                float sideSq = side.sqrMagnitude;
                if (sideSq < 1e-6f)
                {
                    side = Vector3.Cross(da, Vector3.up);
                    sideSq = side.sqrMagnitude;
                    if (sideSq < 1e-6f)
                    {
                        side = Vector3.Cross(da, Vector3.right);
                        sideSq = side.sqrMagnitude;
                    }
                }
                side /= Mathf.Sqrt(Mathf.Max(sideSq, 1e-12f));
                dir = Vector3.RotateTowards(da, side, m_orbitLeadRad, 0f);
            }
            else
            {
                dir = Vector3.RotateTowards(da, db, m_orbitLeadRad, 0f);
            }

            return FromUnit(dir * Mathf.Max(ra, OrbitRadius));
        }

        private float HoverFor(int cellId, int face)
        {
            float hover = cfg.drainHoverHeight;
            if (boardController == null) return hover;

            float free = boardController.FaceFreeDistance(cellId, face);
            if (float.IsPositiveInfinity(free)) return hover;
            return Mathf.Min(hover, free * BlockedHoverFraction);
        }

        private bool TryTargetNormal(int i, int cellId, Vector3 from, bool requireOpen, out Vector3 normal)
        {
            normal = Vector3.back;

            int face = targetFace[i];
            bool usable = face >= 0 && face < HexCoord3.DirectionCount;
            if (usable && requireOpen) usable = (boardController.OpenFacesOf(cellId) & (1 << face)) != 0;

            if (!usable)
            {
                face = boardController.ChooseApproachFace(cellId, from);
                if (face < 0 || face >= HexCoord3.DirectionCount)
                {
                    targetFace[i] = -1;
                    return false;
                }
                targetFace[i] = face;
            }

            Vector3 n = boardController.WorldFaceNormal(face);
            float sq = n.sqrMagnitude;
            if (sq > 1e-8f) normal = n / Mathf.Sqrt(sq);
            return true;
        }

        private void ClearClaims()
        {
            claims.Clear();
            consumed.Clear();
        }

        private void CollectClaims()
        {
            if (boardController == null) return;

            for (int i = 0; i < count; i++)
            {
                if (!alive[i]) continue;
                int t = targetCell[i];
                if (t < 0) continue;

                bool valid = IsTargetValid(i, t);
                if (valid)
                {
                    int owner;
                    if (claims.TryGetValue(t, out owner))
                    {
                        if (owner != i) valid = false;
                    }
                    else
                    {
                        claims[t] = i;
                    }
                }

                if (!valid) DropTarget(i);
            }
        }

        private bool IsTargetValid(int i, int cellId)
        {
            if (consumed.Contains(cellId)) return false;

            if (boardController == null) return false;

            CellController c = boardController.CellById(cellId);
            if (c == null || !c.Alive) return false;
            if (c.ColorType != colorIdx[i]) return false;
            if (state[i] == BeeState.Draining) return true;

            int face = targetFace[i];
            if (face < 0 || face >= HexCoord3.DirectionCount) return false;
            if ((boardController.OpenFacesOf(cellId) & (1 << face)) == 0) return false;

            return c.IsTargetable;
        }

        private void DropTarget(int i)
        {
            targetCell[i] = -1;
            targetFace[i] = -1;
            targetTimer[i] = 0f;
            idleTimer[i] = 0f;
            drainTimer[i] = 0f;
            if (state[i] == BeeState.Draining || state[i] == BeeState.ToCell) state[i] = BeeState.Approaching;
        }

        private void Assign()
        {
            if (boardController == null || CellWorldPosition == null || count == 0) return;

            int budget = MaxAssignmentsPerTick;
            int scanned = 0;
            float hover = cfg.drainHoverHeight;
            float hoverReach = Mathf.Abs(hover);
            float backPenalty = cfg.BackFacePenalty;
            float farPenalty = cfg.FarSidePenalty;

            while (scanned < count && budget > 0)
            {
                int i = assignCursor;
                assignCursor++;
                if (assignCursor >= count) assignCursor = 0;
                scanned++;

                if (!alive[i]) continue;
                if (targetCell[i] >= 0) continue;
                BeeState s = state[i];
                if (s != BeeState.Approaching && s != BeeState.ToCell) continue;

                IReadOnlyList<CellController> cands = boardController.TargetableOfColor(colorIdx[i]);
                if (cands == null || cands.Count == 0) continue;

                budget--;

                int best = -1;
                int bestFace = -1;
                float bestCost = float.MaxValue;
                Vector3 p = pos[i];

                for (int k = 0; k < cands.Count; k++)
                {
                    CellController c = cands[k];
                    if (c == null || !c.Alive || !c.IsTargetable) continue;
                    int id = c.CellId;
                    if (claims.ContainsKey(id) || consumed.Contains(id)) continue;

                    Vector3 w = CellWorldPosition(id);
                    float reach = (w - p).magnitude - hoverReach;
                    if (reach > 0f && reach * reach >= bestCost) continue;

                    int face = boardController.ChooseApproachFace(id, p);
                    if (face < 0 || face >= HexCoord3.DirectionCount) continue;

                    Vector3 n = boardController.WorldFaceNormal(face);
                    Vector3 hoverPt = w + n * HoverFor(id, face);
                    float cost = (hoverPt - p).sqrMagnitude;
                    if (n.z > 0f) cost += n.z * backPenalty;
                    if (Vector3.Dot(n, p - w) < 0f) cost += farPenalty;

                    if (cost < bestCost)
                    {
                        bestCost = cost;
                        best = id;
                        bestFace = face;
                    }
                }

                if (best < 0) continue;

                claims[best] = i;
                targetCell[i] = best;
                targetFace[i] = bestFace;
                targetTimer[i] = 0f;
                idleTimer[i] = 0f;
            }
        }

        private void Move(float dt)
        {
            BuildHash();

            for (int i = 0; i < count; i++)
            {
                if (!alive[i]) continue;

                switch (state[i])
                {
                    case BeeState.Draining:
                        MoveDrain(i, dt);
                        break;
                    case BeeState.ToHive:
                        MoveHome(i, dt);
                        break;
                    case BeeState.Finale:
                        MoveFinale(i, dt);
                        break;
                    default:
                        MoveOut(i, dt);
                        break;
                }

                if (!alive[i]) continue;

                if (growDuration[i] > 0f) TickGrow(i, dt);

                UpdateRotation(i, dt);

                float f = state[i] == BeeState.Draining ? cfg.wingFlapFreqDrain : cfg.wingFlapFreq;
                float wp = wingPhase[i] + dt * f;
                if (wp >= TwoPi) wp -= TwoPi * Mathf.Floor(wp / TwoPi);
                wingPhase[i] = wp;
            }
        }

        private void TickGrow(int i, float dt)
        {
            growTimer[i] += dt;
            float t = Mathf.Clamp01(growTimer[i] / growDuration[i]);
            visualScale[i] = Mathf.Lerp(GrowStartScale, 1f, t);
            if (t >= 1f) ClearGrow(i);
        }

        private void ClearGrow(int i)
        {
            growTimer[i] = 0f;
            growDuration[i] = 0f;
        }

        private void MoveOut(int i, float dt)
        {
            Vector3 p = pos[i];
            int t = targetCell[i];
            float speed = cfg.maxSpeedOut;
            Vector3 seek;
            Vector3 n = Vector3.back;

            if (t >= 0 && (CellWorldPosition == null || boardController == null || !TryTargetNormal(i, t, p, true, out n)))
            {
                DropTarget(i);
                t = -1;
            }

            if (t >= 0)
            {
                Vector3 w = CellWorldPosition(t);
                float hover = HoverFor(t, targetFace[i]);
                float lane = Mathf.Max(LaneDistance(w, n), hover);

                Vector3 o = p - w;
                float along = Vector3.Dot(o, n);
                float lateral = (o - n * along).magnitude;

                float margin = cfg.diveMargin;
                float exit = margin * cfg.DiveHysteresis;
                bool diving = along > 0f && (state[i] == BeeState.ToCell ? lateral < exit : lateral < margin);
                state[i] = diving ? BeeState.ToCell : BeeState.Approaching;

                Vector3 goal;
                float slowDist;
                if (diving)
                {
                    float blend = 1f - Mathf.Clamp01(lateral / Mathf.Max(0.0001f, exit));
                    blend = blend * blend * (3f - 2f * blend);
                    goal = w + n * Mathf.Lerp(lane, hover, blend);

                    Vector3 hoverPt = w + n * hover;
                    float arrive = cfg.cellArriveRadius;
                    if ((hoverPt - p).sqrMagnitude < arrive * arrive)
                    {
                        BeginDrain(i, t);
                        return;
                    }

                    slowDist = (goal - p).magnitude;
                }
                else
                {
                    Vector3 mouth = w + n * lane;
                    goal = RouteAround(p, mouth);
                    slowDist = (mouth - p).magnitude;
                }

                Vector3 to = goal - p;
                float d = to.magnitude;
                seek = d > 0.0001f ? to / d : -n;

                float slow = Mathf.Clamp01(slowDist / Mathf.Max(0.0001f, margin));
                slow = slow * slow * (3f - 2f * slow);
                speed *= Mathf.Max(cfg.ArriveEase, slow);

                targetTimer[i] += dt;
                idleTimer[i] = 0f;
                if (targetTimer[i] > cfg.stuckTimeout)
                {
                    targetTimer[i] = 0f;
                    targetCell[i] = -1;
                    targetFace[i] = -1;
                    state[i] = BeeState.Approaching;
                }
            }
            else
            {
                idleTimer[i] += dt;
                if (idleTimer[i] > cfg.stuckTimeout)
                {
                    idleTimer[i] = 0f;
                    targetCell[i] = -1;
                    targetFace[i] = -1;
                    BeginReturn(i);
                    return;
                }

                state[i] = BeeState.Approaching;

                wanderRepick[i] -= dt;
                Vector3 wp = wanderPoint[i];
                float wdx = wp.x - p.x;
                float wdy = wp.y - p.y;
                if (wanderRepick[i] <= 0f || (wdx * wdx + wdy * wdy) < 0.09f)
                {
                    float a = Random.Range(0f, TwoPi);
                    float r = Random.Range(0.35f, 1f) * Mathf.Max(0.2f, cfg.hiveApproachDist);
                    wp = new Vector3(anchor[i].x + Mathf.Cos(a) * r, anchor[i].y + Mathf.Sin(a) * r, m_flightZ);
                    wanderPoint[i] = wp;
                    wanderRepick[i] = Random.Range(0.7f, 1.9f);
                }

                Vector3 routed = RouteAround(p, wp);
                Vector3 to = routed - p;
                float d = to.magnitude;
                seek = d > 0.0001f ? to / d : Vector3.up;
                speed *= 0.45f;
            }

            Steer(i, seek, speed, dt);
        }

        private void MoveDrain(int i, float dt)
        {
            int t = targetCell[i];
            Vector3 p = pos[i];

            if (m_hasPrevVolume)
            {
                Quaternion delta = m_volRot * Quaternion.Inverse(m_prevVolRot);
                p = m_volCenter + delta * (p - m_prevVolCenter);
                pos[i] = p;
                vel[i] = delta * vel[i];
            }

            Vector3 n;
            if (t < 0 || CellWorldPosition == null || boardController == null || !TryTargetNormal(i, t, p, false, out n))
            {
                DropTarget(i);
                return;
            }

            Vector3 w = CellWorldPosition(t);
            float hover = HoverFor(t, targetFace[i]);
            float bob = Mathf.Sin((time + phase[i]) * 7f) * Mathf.Min(cfg.drainBob, hover * 0.5f);

            Vector3 sep = Separation(i);
            Vector3 sepPlanar = sep - n * Vector3.Dot(sep, n);
            Vector3 goal = w + n * (hover + bob) + sepPlanar * (cfg.separationWeight * 0.02f);

            Vector3 wish = (goal - p) * cfg.DrainApproachGain;
            float maxDrain = cfg.maxSpeedOut * 0.35f;
            float wsq = wish.sqrMagnitude;
            if (wsq > maxDrain * maxDrain && wsq > 1e-8f)
            {
                float ws = maxDrain / Mathf.Sqrt(wsq);
                wish.x *= ws;
                wish.y *= ws;
                wish.z *= ws;
            }

            float dk = 1f - Mathf.Exp(-cfg.DrainDamping * dt);
            Vector3 dv = vel[i];
            dv.x += (wish.x - dv.x) * dk;
            dv.y += (wish.y - dv.y) * dk;
            dv.z += (wish.z - dv.z) * dk;
            vel[i] = dv;

            pos[i] = new Vector3(p.x + dv.x * dt, p.y + dv.y * dt, p.z + dv.z * dt);
        }

        private void BeginReturn(int i)
        {
            state[i] = BeeState.ToHive;
            homeStage[i] = HomeStageApproach;
            egressCell[i] = -1;
            egressFace[i] = -1;
            homeTimer[i] = 0f;
            enterTimer[i] = 0f;
            ClearGrow(i);
            visualScale[i] = 1f;
        }

        public Vector3 HiveEntryPoint
        {
            get { return hivePos + hiveForward * cfg.HiveEntryStandoff; }
        }

        private void MoveHome(int i, float dt)
        {
            if (homeStage[i] == HomeStageEnter)
            {
                MoveEnter(i, dt);
                return;
            }

            homeTimer[i] += dt;
            if (homeTimer[i] > cfg.HomeEnterTimeout)
            {
                BeginEnter(i);
                return;
            }

            if (homeStage[i] == HomeStageEgress)
            {
                if (MoveEgress(i, dt)) return;
                homeStage[i] = HomeStageApproach;
                egressCell[i] = -1;
                egressFace[i] = -1;
            }

            Vector3 entry = HiveEntryPoint;
            float entryRadius = cfg.HiveEntryArriveRadius;

            if (homeStage[i] == HomeStageApproach)
            {
                Vector3 toEntry = entry - pos[i];
                float dEntry = toEntry.magnitude;

                if (dEntry > entryRadius)
                {
                    Vector3 routed = RouteAround(pos[i], entry);
                    Vector3 toRouted = routed - pos[i];
                    float dRouted = toRouted.magnitude;
                    Vector3 approach = dRouted > 0.0001f ? toRouted / dRouted : toEntry / Mathf.Max(0.0001f, dEntry);
                    float loose = Mathf.Clamp01(dEntry / Mathf.Max(0.0001f, entryRadius * 4f));
                    float ease = Mathf.Clamp01(dEntry / Mathf.Max(0.0001f, entryRadius * 3f));
                    Steer(i, approach, cfg.maxSpeedHome * Mathf.Max(0.3f, ease), dt, loose, 1f);
                    return;
                }

                homeStage[i] = HomeStageMouth;
            }

            Vector3 p = pos[i];
            Vector3 toHive = hivePos - p;
            float dHive = toHive.magnitude;

            if (dHive < cfg.HomeArriveRadius)
            {
                BeginEnter(i);
                return;
            }

            Vector3 offset = p - hivePos;
            float along = Vector3.Dot(offset, hiveForward);
            float lateral = (offset - hiveForward * along).magnitude;

            if (lateral > entryRadius * 2.2f || along < -cfg.HomeArriveRadius)
            {
                homeStage[i] = HomeStageApproach;
                return;
            }

            Vector3 dir = toHive / Mathf.Max(0.0001f, dHive);
            float glide = cfg.maxSpeedHome * Mathf.Clamp(dHive / Mathf.Max(0.0001f, cfg.HomeArriveRadius * 2.5f), 0.35f, 1f);

            Vector3 v = Vector3.MoveTowards(vel[i], dir * glide, cfg.accel * dt);
            vel[i] = v;

            Vector3 np = new Vector3(p.x + v.x * dt, p.y + v.y * dt, p.z + v.z * dt);
            ClampZ(i, ref np, p.z);
            pos[i] = np;
        }

        private bool MoveEgress(int i, float dt)
        {
            int cell = egressCell[i];
            int face = egressFace[i];
            if (!m_hasVolume || cell < 0 || face < 0 || CellWorldPosition == null || boardController == null) return false;

            Vector3 w = CellWorldPosition(cell);
            Vector3 n = boardController.WorldFaceNormal(face);
            float lane = LaneDistance(w, n);

            Vector3 p = pos[i];
            if (Vector3.Dot(p - w, n) >= lane - cfg.LaneExtra * 0.5f || ToUnit(p).sqrMagnitude >= 1f) return false;

            Vector3 to = (w + n * lane) - p;
            float d = to.magnitude;
            Vector3 seek = d > 0.0001f ? to / d : n;
            Steer(i, seek, cfg.maxSpeedHome * EgressSpeedMul, dt, EgressWanderMul, 0.5f);
            return true;
        }

        private void BeginEnter(int i)
        {
            homeStage[i] = HomeStageEnter;
            enterTimer[i] = 0f;
            enterFrom[i] = pos[i];
            ClearGrow(i);
            visualScale[i] = 1f;
        }

        private void MoveEnter(int i, float dt)
        {
            float duration = cfg.HiveEnterDuration;
            enterTimer[i] += dt;

            float t = Mathf.Clamp01(enterTimer[i] / duration);
            float inv = 1f - t;
            float ease = 1f - inv * inv;

            Vector3 target = hivePos - hiveForward * cfg.HiveEnterDepth;
            Vector3 np = Vector3.Lerp(enterFrom[i], target, ease);
            np.y -= Mathf.Sin(t * Mathf.PI) * cfg.hiveEnterDip;

            vel[i] = (np - pos[i]) / Mathf.Max(dt, 0.0001f);
            pos[i] = np;

            float shrinkStart = Mathf.Clamp01(cfg.hiveEnterShrinkStart);
            float shrink = t <= shrinkStart
                ? 1f
                : 1f - Mathf.SmoothStep(0f, 1f, (t - shrinkStart) / Mathf.Max(0.0001f, 1f - shrinkStart));
            visualScale[i] = Mathf.Clamp(shrink, 0.01f, 1f);

            if (t >= 1f) Deliver(i);
        }

        private void MoveFinale(int i, float dt)
        {
            finaleTimer[i] += dt;

            if (finaleTimer[i] <= cfg.finaleLoopDuration)
            {
                finaleAngle[i] += (TwoPi / Mathf.Max(0.05f, cfg.finaleLoopDuration)) * dt;
                finaleRise[i] += cfg.finaleLoopUpSpeed * dt;

                Vector3 goal = new Vector3(
                    hivePos.x + Mathf.Cos(finaleAngle[i]) * cfg.finaleLoopRadius,
                    hivePos.y + Mathf.Sin(finaleAngle[i]) * cfg.finaleLoopRadius + finaleRise[i],
                    m_flightZ);

                Vector3 routed = RouteAround(pos[i], goal);
                Vector3 to = routed - pos[i];
                float d = to.magnitude;
                Vector3 seek = d > 0.0001f ? to / d : Vector3.up;
                Steer(i, seek, cfg.maxSpeedHome, dt);
                return;
            }

            Vector3 p = pos[i];
            Vector3 v = vel[i];
            Vector3 wish = new Vector3(v.x * 0.2f, cfg.finaleExitSpeed, 0f);
            v = Vector3.MoveTowards(v, wish, cfg.accel * dt);
            vel[i] = v;

            Vector3 np = p + v * dt;
            ClampZ(i, ref np, p.z);
            pos[i] = np;

            if (np.y - hivePos.y > FinaleExitHeight) Despawn(i);
        }

        private void Steer(int i, Vector3 seek, float maxSpeed, float dt)
        {
            Steer(i, seek, maxSpeed, dt, 1f, 1f);
        }

        private void Steer(int i, Vector3 seek, float maxSpeed, float dt, float wanderMul, float separationMul)
        {
            Vector3 sep = Separation(i);
            Vector3 wander = WanderVector(i, dt);

            float sepW = cfg.separationWeight * separationMul;
            float wanderW = cfg.wanderWeight * wanderMul;

            Vector3 desired;
            desired.x = seek.x + sep.x * sepW + wander.x * wanderW;
            desired.y = seek.y + sep.y * sepW + wander.y * wanderW;
            desired.z = seek.z + sep.z * sepW + wander.z * wanderW;
            desired.y += Mathf.Sin(time * cfg.waggleFreq + phase[i]) * cfg.outboundBobAmp * wanderMul;

            float ms = maxSpeed * speedMul[i];
            float dsq = desired.sqrMagnitude;

            Vector3 wish;
            if (dsq > 1e-8f)
            {
                float inv = ms / Mathf.Sqrt(dsq);
                wish = new Vector3(desired.x * inv, desired.y * inv, desired.z * inv);
            }
            else
            {
                wish = Vector3.zero;
            }

            Vector3 v = Vector3.MoveTowards(vel[i], wish, cfg.accel * dt);

            float damp = 1f - Mathf.Exp(-cfg.turnDamping * dt);
            v.x += (wish.x - v.x) * damp;
            v.y += (wish.y - v.y) * damp;
            v.z += (wish.z - v.z) * damp;

            float vs = v.sqrMagnitude;
            if (vs > ms * ms && vs > 1e-8f)
            {
                float s = ms / Mathf.Sqrt(vs);
                v.x *= s;
                v.y *= s;
                v.z *= s;
            }

            vel[i] = v;

            Vector3 p = pos[i];
            Vector3 np = new Vector3(p.x + v.x * dt, p.y + v.y * dt, p.z + v.z * dt);
            ClampZ(i, ref np, p.z);
            pos[i] = np;
        }

        private Vector3 WanderVector(int i, float dt)
        {
            float drift = (Mathf.PerlinNoise(phase[i], time * cfg.wanderFreq) - 0.5f) * 2f;
            float a = wanderAngle[i] + drift * cfg.wanderFreq * dt * 4f;
            wanderAngle[i] = a;
            return new Vector3(Mathf.Cos(a) * 0.5f, Mathf.Sin(a) * 0.5f, Mathf.Sin(a * 0.5f) * 0.12f);
        }

        private Vector3 Separation(int i)
        {
            float r = cfg.separationRadius;
            if (r <= 0f) return Vector3.zero;

            float r2 = r * r;
            Vector3 p = pos[i];
            float ax = 0f;
            float ay = 0f;
            float az = 0f;

            int bx = cellX[i];
            int by = cellY[i];

            for (int ox = -1; ox <= 1; ox++)
            {
                for (int oy = -1; oy <= 1; oy++)
                {
                    int b = BucketOf(bx + ox, by + oy);
                    for (int j = bucketHead[b]; j >= 0; j = nextInBucket[j])
                    {
                        if (j == i) continue;
                        if (!alive[j]) continue;

                        float dx = p.x - pos[j].x;
                        float dy = p.y - pos[j].y;
                        float dz = p.z - pos[j].z;
                        float d2 = dx * dx + dy * dy + dz * dz;
                        if (d2 >= r2) continue;
                        if (d2 <= 1e-6f) continue;

                        float d = Mathf.Sqrt(d2);
                        float w = (1f - d / r) / d;
                        ax += dx * w;
                        ay += dy * w;
                        az += dz * w;
                    }
                }
            }

            return new Vector3(ax, ay, az * 0.35f);
        }

        private void BuildHash()
        {
            for (int b = 0; b < BucketCount; b++) bucketHead[b] = -1;

            float size = Mathf.Max(0.0001f, cfg.separationRadius * 2f);
            float inv = 1f / size;

            for (int i = 0; i < count; i++)
            {
                nextInBucket[i] = -1;
                if (!alive[i]) continue;

                int cx = Mathf.FloorToInt(pos[i].x * inv);
                int cy = Mathf.FloorToInt(pos[i].y * inv);
                cellX[i] = cx;
                cellY[i] = cy;

                int b = BucketOf(cx, cy);
                nextInBucket[i] = bucketHead[b];
                bucketHead[b] = i;
            }
        }

        private static int BucketOf(int x, int y)
        {
            int h = (x * 73856093) ^ (y * 19349663);
            return h & BucketMask;
        }

        private void UpdateRotation(int i, float dt)
        {
            Vector3 v = vel[i];
            float vxy = v.x * v.x + v.y * v.y;
            float gate = cfg.HeadingMinSpeed;
            float gateSq = gate * gate;
            float hk = 1f - Mathf.Exp(-cfg.HeadingDamping * dt);
            float step = 0f;

            if (vxy > gateSq)
            {
                float target = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
                step = Mathf.DeltaAngle(headingAngle[i], target) * hk;
                float cap = cfg.HeadingMaxTurnRate * dt;
                if (step > cap) step = cap;
                else if (step < -cap) step = -cap;
                headingAngle[i] += step;
            }

            float maxPitch = Mathf.Min(cfg.MaxFlightPitch, MaxPitchLimit);
            float targetPitch = 0f;
            if (vxy + v.z * v.z > gateSq)
            {
                float planar = Mathf.Sqrt(vxy);
                targetPitch = Mathf.Clamp(Mathf.Atan2(-v.z, Mathf.Max(planar, 0.0001f)) * Mathf.Rad2Deg, -maxPitch, maxPitch);
            }
            float pitch = flightPitch[i] + (targetPitch - flightPitch[i]) * hk;
            pitch = Mathf.Clamp(pitch, -maxPitch, maxPitch);
            flightPitch[i] = pitch;

            float rad = headingAngle[i] * Mathf.Deg2Rad;
            float prad = pitch * Mathf.Deg2Rad;
            float cosYaw = Mathf.Cos(rad);
            float sinYaw = Mathf.Sin(rad);
            float cosPitch = Mathf.Cos(prad);
            heading[i] = new Vector3(cosYaw, sinYaw, 0f);
            Vector3 f = new Vector3(cosYaw * cosPitch, sinYaw * cosPitch, -Mathf.Sin(prad));

            float rate = step / Mathf.Max(0.0001f, dt);
            float ks = 1f - Mathf.Exp(-8f * dt);
            turnRate[i] += (rate - turnRate[i]) * ks;

            float roll = Mathf.Clamp(turnRate[i] / 180f, -1f, 1f) * cfg.bankRoll;
            float yaw = Mathf.Sin(time * cfg.waggleFreq + phase[i]) * cfg.waggleYawAmp;

            rot[i] = Quaternion.LookRotation(f, Vector3.back) * Quaternion.Euler(0f, yaw, -roll);
        }

        private void DrainTick(float dt)
        {
            for (int i = 0; i < count; i++)
            {
                if (!alive[i]) continue;
                if (state[i] != BeeState.Draining) continue;

                int drained = targetCell[i];
                int drainedFace = targetFace[i];
                if (drained >= 0 && drainedFace >= 0 && CellWorldPosition != null && boardController != null)
                {
                    Vector3 hoverPt = CellWorldPosition(drained) + boardController.WorldFaceNormal(drainedFace) * HoverFor(drained, drainedFace);
                    float leash = cfg.diveMargin;
                    if ((pos[i] - hoverPt).sqrMagnitude > leash * leash)
                    {
                        state[i] = BeeState.ToCell;
                        drainTimer[i] = 0f;
                        continue;
                    }
                }

                drainTimer[i] += dt;
                if (drainTimer[i] < cfg.drainTime) continue;

                int cell = targetCell[i];
                drainTimer[i] = 0f;
                targetCell[i] = -1;
                targetFace[i] = -1;
                targetTimer[i] = 0f;
                idleTimer[i] = 0f;
                cargo[i] = true;
                BeginReturn(i);
                if (cell >= 0 && drainedFace >= 0)
                {
                    egressCell[i] = cell;
                    egressFace[i] = drainedFace;
                    homeStage[i] = HomeStageEgress;
                }

                if (cell >= 0)
                {
                    consumed.Add(cell);
                    claims[cell] = i;
                    if (DrainCompleted != null) DrainCompleted(cell);
                }
            }
        }

        private void BeginDrain(int i, int cellId)
        {
            state[i] = BeeState.Draining;
            drainTimer[i] = 0f;
            targetTimer[i] = 0f;
            idleTimer[i] = 0f;
            claims[cellId] = i;
            if (DrainStarted != null) DrainStarted(cellId);
        }

        private void Deliver(int i)
        {
            bool had = cargo[i];
            int slot = slotIdx[i];
            slotIdx[i] = -1;
            cargo[i] = false;
            if (slot >= 0 && Returned != null) Returned(slot, had);
            Despawn(i);
        }

        private void Despawn(int i)
        {
            if (!alive[i]) return;

            alive[i] = false;
            state[i] = BeeState.Dead;
            targetCell[i] = -1;
            targetFace[i] = -1;
            egressCell[i] = -1;
            egressFace[i] = -1;
            cargo[i] = false;
            homeStage[i] = HomeStageApproach;
            enterTimer[i] = 0f;
            ClearGrow(i);
            visualScale[i] = 1f;
            aliveCount--;
            if (aliveCount < 0) aliveCount = 0;

            if (BeeDespawned != null) BeeDespawned(i);

            if (freeCount >= freeIds.Length) System.Array.Resize(ref freeIds, freeIds.Length * 2);
            freeIds[freeCount++] = i;
        }

        private bool HasActiveTarget(int i)
        {
            if (targetCell[i] < 0) return false;
            BeeState s = state[i];
            return s == BeeState.Approaching || s == BeeState.ToCell || s == BeeState.Draining;
        }

        private float ZLimit(int i)
        {
            float plane = m_flightZ + 0.05f;
            BeeState s = state[i];
            if (s == BeeState.ToHive || s == BeeState.Finale)
            {
                return hivePos.z > plane ? hivePos.z : plane;
            }
            return plane;
        }

        private void ClampZ(int i, ref Vector3 p, float prevZ)
        {
            if (HasActiveTarget(i)) return;
            if (state[i] == BeeState.ToHive && homeStage[i] == HomeStageEgress) return;

            float limit = ZLimit(i);
            if (prevZ > limit) limit = prevZ;
            if (p.z > limit) p.z = limit;
        }

        private void Allocate(int cap)
        {
            pos = new Vector3[cap];
            vel = new Vector3[cap];
            heading = new Vector3[cap];
            anchor = new Vector3[cap];
            wanderPoint = new Vector3[cap];
            enterFrom = new Vector3[cap];
            rot = new Quaternion[cap];
            state = new BeeState[cap];
            alive = new bool[cap];
            cargo = new bool[cap];
            colorIdx = new ColorType[cap];
            slotIdx = new int[cap];
            targetCell = new int[cap];
            targetFace = new int[cap];
            egressCell = new int[cap];
            egressFace = new int[cap];
            speedMul = new float[cap];
            wingPhase = new float[cap];
            phase = new float[cap];
            drainTimer = new float[cap];
            targetTimer = new float[cap];
            idleTimer = new float[cap];
            wanderAngle = new float[cap];
            wanderRepick = new float[cap];
            headingAngle = new float[cap];
            flightPitch = new float[cap];
            turnRate = new float[cap];
            finaleTimer = new float[cap];
            finaleAngle = new float[cap];
            finaleRise = new float[cap];
            homeTimer = new float[cap];
            enterTimer = new float[cap];
            visualScale = new float[cap];
            growTimer = new float[cap];
            growDuration = new float[cap];
            homeStage = new byte[cap];
            cellX = new int[cap];
            cellY = new int[cap];
            nextInBucket = new int[cap];
            freeIds = new int[cap];
            for (int k = 0; k < cap; k++)
            {
                targetFace[k] = -1;
                egressCell[k] = -1;
                egressFace[k] = -1;
            }
        }

        private void EnsureCapacity(int n)
        {
            int cap = pos.Length;
            if (cap >= n) return;
            while (cap < n) cap *= 2;

            int oldCap = targetFace.Length;

            System.Array.Resize(ref pos, cap);
            System.Array.Resize(ref vel, cap);
            System.Array.Resize(ref heading, cap);
            System.Array.Resize(ref anchor, cap);
            System.Array.Resize(ref wanderPoint, cap);
            System.Array.Resize(ref enterFrom, cap);
            System.Array.Resize(ref rot, cap);
            System.Array.Resize(ref state, cap);
            System.Array.Resize(ref alive, cap);
            System.Array.Resize(ref cargo, cap);
            System.Array.Resize(ref colorIdx, cap);
            System.Array.Resize(ref slotIdx, cap);
            System.Array.Resize(ref targetCell, cap);
            System.Array.Resize(ref targetFace, cap);
            System.Array.Resize(ref egressCell, cap);
            System.Array.Resize(ref egressFace, cap);
            System.Array.Resize(ref speedMul, cap);
            System.Array.Resize(ref wingPhase, cap);
            System.Array.Resize(ref phase, cap);
            System.Array.Resize(ref drainTimer, cap);
            System.Array.Resize(ref targetTimer, cap);
            System.Array.Resize(ref idleTimer, cap);
            System.Array.Resize(ref wanderAngle, cap);
            System.Array.Resize(ref wanderRepick, cap);
            System.Array.Resize(ref headingAngle, cap);
            System.Array.Resize(ref flightPitch, cap);
            System.Array.Resize(ref turnRate, cap);
            System.Array.Resize(ref finaleTimer, cap);
            System.Array.Resize(ref finaleAngle, cap);
            System.Array.Resize(ref finaleRise, cap);
            System.Array.Resize(ref homeTimer, cap);
            System.Array.Resize(ref enterTimer, cap);
            System.Array.Resize(ref visualScale, cap);
            System.Array.Resize(ref growTimer, cap);
            System.Array.Resize(ref growDuration, cap);
            System.Array.Resize(ref homeStage, cap);
            System.Array.Resize(ref cellX, cap);
            System.Array.Resize(ref cellY, cap);
            System.Array.Resize(ref nextInBucket, cap);
            if (freeIds.Length < cap) System.Array.Resize(ref freeIds, cap);
            for (int k = oldCap; k < cap; k++)
            {
                targetFace[k] = -1;
                egressCell[k] = -1;
                egressFace[k] = -1;
            }
        }
    }
}
