using System;
using System.Collections.Generic;
using UnityEngine;
using HoneyBeeRush.Bees;
using HoneyBeeRush.Core;
using HoneyBeeRush.Data;
using HoneyBeeRush.Gameplay.Config;
using HoneyBeeRush.Gameplay.Core;
using HoneyBeeRush.Gameplay.Domain;
using HoneyBeeRush.Managers;
using HoneyBeeRush.View;

namespace HoneyBeeRush.Gameplay.Runtime
{
    public sealed partial class LevelController : MonoBehaviour
    {
        [Header("Domains")]
        [SerializeField] private BoardController m_board;
        [SerializeField] private GridBoardController m_gridBoard;
        [SerializeField] private SlotBoardController m_slots;
        [SerializeField] private BeeSwarmController m_beeSwarm;

        [Header("Views")]
        [SerializeField] private Camera m_camera;
        [SerializeField] private EnvironmentView m_envView;
        [SerializeField] private HiveView m_hiveView;

        [Header("Config")]
        [SerializeField] private TuningConfig m_tuning;
        [SerializeField] private ColorMaterialMapping m_colorMaterials;
        [SerializeField] private BeeConfig m_beeConfig = new BeeConfig();

        private static readonly LayoutConfig s_fallbackLayout = new LayoutConfig();

        private LevelData m_level;
        private HoneyMeter m_honey;
        private BeeSim m_bees;

        private GamePhase m_phase = GamePhase.Loading;
        private bool m_sessionEnded;
        private bool m_inputLocked;
        private bool m_environmentBuilt;
        private float m_loseTimer;
        private float m_winDelay;
        private bool m_winRaised;
        private int m_movesThisLevel;
        private float m_globalTimeScale = 1f;

        private bool m_pointerDown;
        private bool m_pointerBlocked;
        private bool m_pointerIsTouch;
        private int m_pointerFingerId;
        private Vector2 m_lastPointerPos;

        private readonly List<CrateController> m_launchBuffer = new List<CrateController>(8);

        public event Action<bool> OnSessionEnded;

        public GamePhase Phase => m_phase;
        public bool IsSessionEnded => m_sessionEnded;
        public int MovesThisLevel => m_movesThisLevel;
        public LevelData Level => m_level;
        public HoneyMeter Honey => m_honey;
        public BoardController Board => m_board;
        public GridBoardController GridBoard => m_gridBoard;
        public SlotBoardController Slots => m_slots;
        public BeeSwarmController Swarm => m_beeSwarm;
        public LayoutConfig Layout => Tuning != null ? Tuning.Layout : s_fallbackLayout;

        public float GlobalTimeScale
        {
            get => m_globalTimeScale;
            set => m_globalTimeScale = Mathf.Max(0f, value);
        }

        public TuningConfig Tuning
        {
            get
            {
                if (m_tuning == null) m_tuning = TuningConfig.Instance;
                return m_tuning;
            }
        }

        private void OnEnable()
        {
            TuningConfig.LayoutChanged += ApplyLayoutSizes;
        }

        private void OnDisable()
        {
            TuningConfig.LayoutChanged -= ApplyLayoutSizes;
        }

        public void ApplyLayoutSizes()
        {
            if (m_board != null)
            {
                m_board.ConfigureView(Tuning != null ? Tuning.BoardView : BoardViewSettings.Default);
                if (m_camera == null) m_camera = Camera.main;
                if (m_camera != null) m_board.SetViewForward(m_camera.transform.forward);
                m_board.ApplyLayoutSizes();
            }
            if (m_bees != null) m_bees.SetFlightPlane(Layout.beeCruiseZ);
            if (m_gridBoard != null) m_gridBoard.ApplyLayoutSizes();
            if (m_slots != null) m_slots.ApplyLayoutSizes();
            if (m_beeSwarm != null) m_beeSwarm.ApplyLayoutSizes();
            if (m_hiveView != null)
            {
                m_hiveView.ApplyLayoutSizes(Layout);
                if (m_bees != null) m_bees.SetHive(m_hiveView.EntrancePosition(Layout), m_hiveView.EntranceForward());
            }
        }

        public void SetData(LevelData level)
        {
            if (level == null)
            {
                Debug.LogError("[HoneyBeeRush] SetData called with a null level.");
                return;
            }

            if (!HasRequiredReferences()) return;

            DisposeSession();

            m_level = level;
            m_sessionEnded = false;
            m_inputLocked = false;
            m_winRaised = false;
            m_loseTimer = 0f;
            m_winDelay = 0f;
            m_movesThisLevel = 0;

            string report;
            if (!LevelLibrary.ValidatePerColorCapacity(level, out report))
            {
                Debug.LogWarning("[HoneyBeeRush] Level " + level.index + " capacity imbalance:\n" + report);
            }

            BuildSession(level);

            m_phase = GamePhase.Playing;
        }

        public void DisposeSession()
        {
            m_sessionEnded = true;
            m_inputLocked = true;
            m_phase = GamePhase.Loading;

            StopAllCoroutines();
            TearDownSession();

            m_level = null;
            m_loseTimer = 0f;
            m_winDelay = 0f;
            m_winRaised = false;
            m_movesThisLevel = 0;
            m_launchBuffer.Clear();
        }

        private void Update()
        {
            if (m_phase == GamePhase.Loading) return;

            float dt = Time.deltaTime * m_globalTimeScale;

            HandlePointer(dt > 0f);
            if (m_board != null) m_board.TickView(Time.deltaTime);

            if (dt <= 0f) return;

            if (m_phase == GamePhase.Playing)
            {
                ProcessSpawners(dt);
            }

            if (m_bees != null) m_bees.Tick(dt);
            if (m_hiveView != null) m_hiveView.Tick(dt);
            if (m_beeSwarm != null) m_beeSwarm.Sync();

            if (m_phase == GamePhase.Playing)
            {
                FreeFinishedSlots();
                RefreshCloggedFlags();
                EvaluateOutcomes(dt);
            }
            else if (m_phase == GamePhase.Won)
            {
                TickWinDelay(dt);
            }
        }

        private bool IsTapAccepted
        {
            get
            {
                if (m_sessionEnded || m_inputLocked) return false;
                if (m_phase != GamePhase.Playing) return false;
                if (GameManager.HasInstance && !GameManager.Instance.canControl) return false;
                return true;
            }
        }

        private bool CanDragBoard
        {
            get
            {
                if (m_board == null || !m_board.IsRotationEnabled) return false;
                if (m_phase == GamePhase.Won) return true;
                if (m_phase != GamePhase.Playing) return false;
                if (GameManager.HasInstance && !GameManager.Instance.canControl) return false;
                return true;
            }
        }

        private void HandlePointer(bool allowLaunch)
        {
            bool canDrag = CanDragBoard;
            if (!canDrag && m_board != null && m_board.IsDraggingView) m_board.EndViewDrag();

            bool pressed;
            bool held;
            bool released;
            bool isTouch;
            int fingerId;
            Vector2 position;
            ReadPointer(out pressed, out held, out released, out isTouch, out fingerId, out position);

            if (m_pointerDown && m_pointerIsTouch && isTouch && fingerId != m_pointerFingerId)
            {
                EndPointer();
                return;
            }

            if (pressed)
            {
                if (m_pointerDown) EndPointer();
                BeginPointer(position, isTouch, fingerId, allowLaunch, canDrag);
            }
            else if (held && m_pointerDown)
            {
                if (!m_pointerBlocked && m_board != null && m_board.IsDraggingView)
                {
                    m_board.DragView(position - m_lastPointerPos, Time.deltaTime);
                }
                m_lastPointerPos = position;
            }

            if (m_pointerDown && (released || (!pressed && !held)))
            {
                EndPointer();
            }

            if (!m_pointerDown && m_board != null && m_board.IsDraggingView) m_board.EndViewDrag();
        }

        private void BeginPointer(Vector2 position, bool isTouch, int fingerId, bool allowLaunch, bool canDrag)
        {
            m_pointerDown = true;
            m_pointerIsTouch = isTouch;
            m_pointerFingerId = fingerId;
            m_lastPointerPos = position;
            m_pointerBlocked = UIManager.Instance != null && UIManager.Instance.IsPointerOverUI();
            if (m_pointerBlocked) return;

            if (m_camera == null) m_camera = Camera.main;
            if (m_camera == null) return;

            Ray ray = m_camera.ScreenPointToRay(position);

            if (m_gridBoard != null)
            {
                int crateId = m_gridBoard.RaycastCrateId(ray);
                if (crateId >= 0)
                {
                    if (allowLaunch && IsTapAccepted) TryLaunchCrateById(crateId);
                    return;
                }
            }

            if (canDrag && m_board.RaycastBoardVolume(ray)) m_board.BeginViewDrag();
        }

        private void EndPointer()
        {
            m_pointerDown = false;
            m_pointerBlocked = false;
            m_pointerIsTouch = false;
            m_pointerFingerId = -1;
            if (m_board != null && m_board.IsDraggingView) m_board.EndViewDrag();
        }

        private static void ReadPointer(out bool pressed, out bool held, out bool released, out bool isTouch, out int fingerId, out Vector2 position)
        {
            pressed = false;
            held = false;
            released = false;
            isTouch = false;
            fingerId = -1;
            position = default;

            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                isTouch = true;
                fingerId = touch.fingerId;
                position = touch.position;

                switch (touch.phase)
                {
                    case TouchPhase.Began:
                        pressed = true;
                        held = true;
                        break;
                    case TouchPhase.Moved:
                    case TouchPhase.Stationary:
                        held = true;
                        break;
                    case TouchPhase.Ended:
                    case TouchPhase.Canceled:
                        released = true;
                        break;
                }
                return;
            }

            position = Input.mousePosition;
            pressed = Input.GetMouseButtonDown(0);
            held = Input.GetMouseButton(0);
            released = Input.GetMouseButtonUp(0);
        }

        private void OnCrateTapped(CrateController crate)
        {
            if (!IsTapAccepted || crate == null) return;
            TryLaunch(crate);
        }

        public bool TryLaunchCrateById(int crateId)
        {
            CrateController crate = m_gridBoard != null ? m_gridBoard.CrateById(crateId) : null;
            return crate != null && TryLaunch(crate);
        }

        public bool TryLaunch(CrateController crate)
        {
            if (m_phase != GamePhase.Playing || crate == null) return false;
            if (m_gridBoard == null || m_slots == null) return false;

            int freeSlots = m_slots.FreeCount;
            if (!m_gridBoard.CanLaunch(crate, freeSlots))
            {
                if (crate.CanBeSelected && m_gridBoard.NeedsMoreSlots(crate, freeSlots))
                {
                    m_slots.PlayFullWarning();
                }

                crate.PlayInvalidFeedback();
                return false;
            }

            List<CrateController> taken = m_gridBoard.TakeForLaunch(crate);
            if (taken == null || taken.Count == 0) return false;

            m_launchBuffer.Clear();
            for (int i = 0; i < taken.Count; i++) m_launchBuffer.Add(taken[i]);

            CrateTravelSettings travel = Tuning != null ? Tuning.CrateTravel : default(CrateTravelSettings);
            Transform travelRoot = m_gridBoard.CellRoot;

            for (int i = 0; i < m_launchBuffer.Count; i++)
            {
                SlotController slot = m_slots.FreeSlot();
                if (slot == null) break;

                CrateController launched = m_launchBuffer[i];
                IReadOnlyList<Vector3> path = m_gridBoard.FindPathToExit(launched);

                slot.Occupy(launched);

                if (travelRoot != null)
                {
                    launched.transform.SetParent(travelRoot, true);
                }

                SlotController target = slot;
                CrateController travelling = launched;
                launched.PlayTravelToSlot(path, slot.Anchor, travel, () =>
                {
                    if (target.CurrentCrate == travelling) target.BeginSpawning();
                });
            }

            m_launchBuffer.Clear();
            m_movesThisLevel++;
            m_gridBoard.OnMoveCommitted();

            Common.EventManager.EmitEvent(Common.EventVariables.MoveCommitted, m_movesThisLevel);
            return true;
        }

        private bool HasRequiredReferences()
        {
            if (m_board != null && m_gridBoard != null && m_slots != null && m_beeSwarm != null) return true;

            Debug.LogError("[HoneyBeeRush] LevelController is missing a domain controller reference " +
                           "(Board / GridBoard / Slots / Swarm). Run Honey Bee Rush/Build Gameplay Scene.");
            return false;
        }
    }
}
