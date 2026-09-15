using UnityEngine;
using HoneyBeeRush.View;

namespace HoneyBeeRush.Gameplay.Config
{
    [CreateAssetMenu(menuName = "Honey Bee Rush/Tuning Config", fileName = "TuningConfig")]
    public sealed class TuningConfig : ScriptableObject
    {
        public const string ResourcePath = "Data/TuningConfig";

        private static TuningConfig s_instance;

        public static event System.Action LayoutChanged;

        public static TuningConfig Instance
        {
            get
            {
                if (s_instance == null) s_instance = Resources.Load<TuningConfig>(ResourcePath);
                return s_instance;
            }
        }

        [Header("Global")]
        [Tooltip("Divides every duration below. Raise it to fast-forward the whole game for testing.")]
        [SerializeField] private float m_globalSpeedMultiplier = 1f;

        [Header("Layout")]
        [Tooltip("World placement and sizing for the board, slot row, crate grid, hive and bee flight " +
                 "planes. The camera is NOT configured here - position and FOV are authored on the " +
                 "Camera itself and nothing at runtime overwrites them.")]
        [SerializeField] private LayoutConfig m_layout = new LayoutConfig();

        [Header("Board View")]
        [Tooltip("When off, the 3D hex board keeps its authored view angles and drag input is ignored.")]
        [SerializeField] private bool m_boardRotationEnabled = true;
        [Tooltip("Degrees the board turns for a drag spanning the full screen height.")]
        [SerializeField] private float m_boardDragDegreesPerScreenHeight = 260f;
        [Tooltip("Pitch limit, in degrees either side of head-on. Yaw is unlimited.")]
        [SerializeField] private float m_boardMaxPitch = 75f;
        [Tooltip("How quickly the spin left over after a flick decays. Higher stops sooner.")]
        [SerializeField] private float m_boardInertiaDamping = 5f;
        [Tooltip("Degrees per second below which post-release spin stops.")]
        [SerializeField] private float m_boardMinInertiaSpeed = 4f;

        [Header("Grid Board")]
        [Tooltip("Column count every level's crate board is built at, regardless of how many columns its crates author.")]
        [SerializeField] private int m_defaultGridWidth = 15;
        [Tooltip("Row count a level falls back to when it does not author its own board height.")]
        [SerializeField] private int m_defaultGridHeight = 20;
        [Tooltip("Deepest row a crate may occupy. Levels stacked deeper than this are repacked across more columns.")]
        [SerializeField] private int m_gridRowBudget = 8;

        [Header("Slots")]
        [Tooltip("When on, a level's authored launchSlots wins over startingSlots.")]
        [SerializeField] private bool m_useAuthoredLaunchSlots = true;
        [SerializeField] private int m_startingSlots = 5;
        [Tooltip("Hard ceiling the Add Slot booster cannot push the launch row past.")]
        [SerializeField] private int m_maxSlots = 7;

        [Header("Bees")]
        [Tooltip("Gap between each bee leaving an occupied slot.")]
        [SerializeField] private float m_beeSpawnInterval = 0.18f;

        [Header("Crate Travel")]
        [Tooltip("Time a launched crate spends hopping across one grid cell on its way to the exit row.")]
        [SerializeField] private float m_crateHopStepDuration = 0.10f;
        [Tooltip("World units per second for the jump from the exit row into the launch slot. Airtime is " +
                 "clamped between the two jump durations below so a short hop still reads as a jump.")]
        [SerializeField] private float m_crateFlySpeed = 15f;
        [Tooltip("Height of the jump arc above the higher of its two endpoints, before distance scaling.")]
        [SerializeField] private float m_crateJumpHeight = 0.85f;
        [Tooltip("Extra arc height added per world unit of jump distance.")]
        [SerializeField] private float m_crateJumpHeightPerUnit = 0.16f;
        [Tooltip("Ceiling on the arc height, so a cross-board jump cannot fly off the top of the screen.")]
        [SerializeField] private float m_crateJumpMaxHeight = 2.1f;
        [SerializeField] private float m_crateJumpMinDuration = 0.34f;
        [SerializeField] private float m_crateJumpMaxDuration = 0.62f;
        [Tooltip("Degrees the crate leans into the jump. It levels out exactly on landing.")]
        [SerializeField] private float m_crateJumpTilt = 14f;
        [Tooltip("How far the arc pops toward the camera at its apex.")]
        [SerializeField] private float m_crateJumpDepthPop = 0.45f;

        [Header("Outcome")]
        [Tooltip("How long the board must stay congested before the level is called lost. Prevents a " +
                 "transient full-slot frame from ending the session.")]
        [SerializeField] private float m_loseDetectDelay = 2.5f;
        [Tooltip("Beat between the last cell clearing and the win being raised, so the finale reads.")]
        [SerializeField] private float m_winSettleDelay = 0.95f;
        [Tooltip("Delay between the session ending and its result panel appearing.")]
        [SerializeField] private float m_endGameDelay = 1.5f;
        [Tooltip("Crates cleared off the grid when the player revives after a loss.")]
        [SerializeField] private int m_reviveClearedCrates = 3;

        [Header("Reward")]
        [SerializeField] private int m_coinsPerWin = 50;
        [Tooltip("Extra coins granted per whole percent of honey above the win threshold.")]
        [SerializeField] private int m_bonusCoinsPerHoneyPercent;

        [Header("UI")]
        [SerializeField] private float m_panelFadeDuration = 0.25f;
        [SerializeField] private float m_panelPopDuration = 0.25f;
        [SerializeField] private float m_progressAnimDuration = 0.2f;
        [Tooltip("Delay before the in-game HUD slides in, so the level is already visible behind it.")]
        [SerializeField] private float m_hudAppearDelay = 0.26f;

        private void OnValidate()
        {
            s_instance = this;
            if (LayoutChanged != null) LayoutChanged();
        }

        public float GlobalSpeedMultiplier => Mathf.Clamp(m_globalSpeedMultiplier, 0.1f, 10f);

        public LayoutConfig Layout => m_layout ?? (m_layout = new LayoutConfig());

        public Core.BoardViewSettings BoardView => new Core.BoardViewSettings
        {
            rotationEnabled = m_boardRotationEnabled,
            dragDegreesPerScreenHeight = m_boardDragDegreesPerScreenHeight > 0f ? m_boardDragDegreesPerScreenHeight : 260f,
            maxPitch = Mathf.Clamp(m_boardMaxPitch, 0f, 89f),
            inertiaDamping = Mathf.Max(0.01f, m_boardInertiaDamping),
            minInertiaSpeed = Mathf.Max(0f, m_boardMinInertiaSpeed)
        };

        public int DefaultGridWidth => Mathf.Clamp(m_defaultGridWidth <= 0 ? 15 : m_defaultGridWidth, 1, 64);
        public int DefaultGridHeight => Mathf.Clamp(m_defaultGridHeight <= 0 ? 20 : m_defaultGridHeight, 1, 64);
        public int GridRowBudget => Mathf.Clamp(m_gridRowBudget <= 0 ? 8 : m_gridRowBudget, 1, 64);

        public float CrateHopStepDuration => (m_crateHopStepDuration <= 0f ? 0.10f : m_crateHopStepDuration) / GlobalSpeedMultiplier;
        public float CrateFlySpeed => (m_crateFlySpeed <= 0f ? 15f : m_crateFlySpeed) * GlobalSpeedMultiplier;
        public float CrateJumpHeight => m_crateJumpHeight <= 0f ? 0.85f : m_crateJumpHeight;
        public float CrateJumpHeightPerUnit => m_crateJumpHeightPerUnit <= 0f ? 0.16f : m_crateJumpHeightPerUnit;
        public float CrateJumpMaxHeight => m_crateJumpMaxHeight <= 0f ? 2.1f : m_crateJumpMaxHeight;
        public float CrateJumpMinDuration => (m_crateJumpMinDuration <= 0f ? 0.34f : m_crateJumpMinDuration) / GlobalSpeedMultiplier;
        public float CrateJumpMaxDuration => (m_crateJumpMaxDuration <= 0f ? 0.62f : m_crateJumpMaxDuration) / GlobalSpeedMultiplier;
        public float CrateJumpTilt => m_crateJumpTilt <= 0f ? 14f : m_crateJumpTilt;
        public float CrateJumpDepthPop => m_crateJumpDepthPop <= 0f ? 0.45f : m_crateJumpDepthPop;

        public Core.CrateTravelSettings CrateTravel => new Core.CrateTravelSettings
        {
            hopStepDuration = CrateHopStepDuration,
            flySpeed = CrateFlySpeed,
            jumpHeight = CrateJumpHeight,
            jumpHeightPerUnit = CrateJumpHeightPerUnit,
            jumpMaxHeight = CrateJumpMaxHeight,
            jumpMinDuration = CrateJumpMinDuration,
            jumpMaxDuration = CrateJumpMaxDuration,
            jumpTilt = CrateJumpTilt,
            jumpDepthPop = CrateJumpDepthPop
        };

        public int ResolveBoardWidth(int usedWidth)
        {
            return Mathf.Clamp(Mathf.Max(DefaultGridWidth, Mathf.Max(1, usedWidth)), 1, 64);
        }

        public int ResolveBoardHeight(int authoredHeight, int usedHeight)
        {
            int height = authoredHeight > 0 ? authoredHeight : DefaultGridHeight;
            return Mathf.Clamp(Mathf.Max(height, Mathf.Max(1, usedHeight)), 1, 64);
        }

        public bool UseAuthoredLaunchSlots => m_useAuthoredLaunchSlots;
        public int StartingSlots => Mathf.Clamp(m_startingSlots, 1, MaxSlots);
        public int MaxSlots => Mathf.Clamp(m_maxSlots, 1, 12);

        public float BeeSpawnInterval => Mathf.Max(0.01f, m_beeSpawnInterval) / GlobalSpeedMultiplier;


        public float LoseDetectDelay => Mathf.Max(0f, m_loseDetectDelay) / GlobalSpeedMultiplier;
        public float WinSettleDelay => Mathf.Max(0f, m_winSettleDelay) / GlobalSpeedMultiplier;
        public float EndGameDelay => Mathf.Max(0f, m_endGameDelay) / GlobalSpeedMultiplier;
        public int ReviveClearedCrates => Mathf.Max(0, m_reviveClearedCrates);

        public int CoinsPerWin => Mathf.Max(0, m_coinsPerWin);
        public int BonusCoinsPerHoneyPercent => Mathf.Max(0, m_bonusCoinsPerHoneyPercent);

        public float PanelFadeDuration => Mathf.Max(0f, m_panelFadeDuration) / GlobalSpeedMultiplier;
        public float PanelPopDuration => Mathf.Max(0.01f, m_panelPopDuration) / GlobalSpeedMultiplier;
        public float ProgressAnimDuration => Mathf.Max(0f, m_progressAnimDuration) / GlobalSpeedMultiplier;
        public float HudAppearDelay => Mathf.Max(0f, m_hudAppearDelay) / GlobalSpeedMultiplier;

        public int ResolveSlotCount(int authoredSlots)
        {
            int count = m_useAuthoredLaunchSlots && authoredSlots > 0 ? authoredSlots : StartingSlots;
            return Mathf.Clamp(count, 1, MaxSlots);
        }

        public int CoinRewardFor(int honeyPercent)
        {
            return CoinsPerWin + BonusCoinsPerHoneyPercent * Mathf.Clamp(honeyPercent, 0, 100);
        }
    }
}
