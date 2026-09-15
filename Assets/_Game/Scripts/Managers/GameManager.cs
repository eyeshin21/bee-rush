using System.Collections;
using UnityEngine;
using HoneyBeeRush.Common;
using HoneyBeeRush.Core;
using HoneyBeeRush.Data;
using HoneyBeeRush.Gameplay.Config;
using HoneyBeeRush.Gameplay.Runtime;
using HoneyBeeRush.UI;

namespace HoneyBeeRush.Managers
{
    [DefaultExecutionOrder(-800)]
    public sealed class GameManager : SingletonDontDestroyMono<GameManager>
    {
        public const string LevelFolder = "Levels";
        public const string EditorTestLevelKey = "HBR_EditorTestLevel";

        [Header("Campaign")]
        [SerializeField] private int m_maxLevel = 64;

        [Header("Session")]
        [SerializeField] private LevelController m_levelController;
        [SerializeField] private TuningConfig m_tuning;

        private LevelData m_currentLevelData;
        private int m_currentLevelDataLevel = -1;
        private bool m_isRevive;

        [HideInInspector] public bool canControl;
        [HideInInspector] public float startTime;
        [HideInInspector] public int tapCheat;
        [HideInInspector] public float startTimeCheat;

        [HideInInspector] public float screenWidth;
        [HideInInspector] public float screenHeight;
        [HideInInspector] public float safeAreaYBottom;
        [HideInInspector] public float safeAreaYTop;
        [HideInInspector] public Vector2 minAnchor;
        [HideInInspector] public Vector2 maxAnchor;

        public int MaxLevel => Mathf.Max(1, m_maxLevel);

        public LevelController LevelController => EnsureLevelController();

        public TuningConfig Tuning
        {
            get
            {
                if (m_tuning == null) m_tuning = TuningConfig.Instance;
                return m_tuning;
            }
        }

        public int CurrentLevel
        {
            get => UserManager.Instance.CurrentLevel;
            set => UserManager.Instance.SetCurrentLevel(value);
        }

        public LevelData CurrentLevelData
        {
            get
            {
                if (m_currentLevelData == null || m_currentLevelDataLevel != CurrentLevel)
                {
                    m_currentLevelData = LoadLevel(CurrentLevel);
                    m_currentLevelDataLevel = CurrentLevel;
                }
                return m_currentLevelData;
            }
            set
            {
                m_currentLevelData = value;
                m_currentLevelDataLevel = value != null ? CurrentLevel : -1;
            }
        }

        private void Start()
        {
            CacheSafeArea();

            BindLevelController();
            EventManager.StartListening(EventVariables.Revive, OnRevive);

#if UNITY_EDITOR
            if (ConsumeEditorTestLevel()) StartCoroutine(AutoStartLevelEditor());
#endif
        }

        protected override void OnDestroy()
        {
            EventManager.StopListening(EventVariables.Revive, OnRevive);
            if (m_levelController != null) m_levelController.OnSessionEnded -= OnSessionEnded;

            base.OnDestroy();
        }

        private void CacheSafeArea()
        {
            Rect safeArea = Screen.safeArea;
            minAnchor = safeArea.position;
            maxAnchor = minAnchor + safeArea.size;

            minAnchor.x /= Screen.width;
            minAnchor.y /= Screen.height;
            maxAnchor.x /= Screen.width;
            maxAnchor.y /= Screen.height;

            float ratio = Screen.width / (float)Screen.height;
            const float ratioDefault = 720f / 1280f;

            if (ratio <= ratioDefault)
            {
                screenWidth = 1080f;
                screenHeight = Screen.height * (screenWidth / Screen.width);
            }
            else
            {
                screenHeight = 1920f;
                screenWidth = Screen.width * (screenHeight / Screen.height);
            }

            safeAreaYBottom = minAnchor.y * screenHeight;
            safeAreaYTop = (1f - maxAnchor.y) * screenHeight;
        }

        private LevelController EnsureLevelController()
        {
            if (m_levelController == null) BindLevelController();
            return m_levelController;
        }

        private void BindLevelController()
        {
            if (m_levelController == null) m_levelController = FindAnyObjectByType<LevelController>(FindObjectsInactive.Include);
            if (m_levelController == null) return;

            m_levelController.OnSessionEnded -= OnSessionEnded;
            m_levelController.OnSessionEnded += OnSessionEnded;
        }

        private void OnSessionEnded(bool win) => EndGame(win);

        private void OnRevive()
        {
            LevelController controller = EnsureLevelController();
            if (controller == null) return;

            controller.ReopenAfterRevive(Tuning != null ? Tuning.ReviveClearedCrates : 3);
            canControl = true;
        }

        public void StartCurrentLevel() => StartLevel(CurrentLevel);

        public void StartLevel(int level)
        {
            canControl = true;
            m_isRevive = false;
            startTime = Time.time;
            CurrentLevel = level;

            EventManager.EmitEvent(EventVariables.LoadLevel);

            LevelController controller = EnsureLevelController();
            if (controller == null)
            {
                Debug.LogError("[HoneyBeeRush] No LevelController in the scene, cannot start a level.");
                return;
            }

            if (!controller.gameObject.activeSelf) controller.gameObject.SetActive(true);
            controller.SetData(CurrentLevelData);

            EventManager.EmitEvent(EventVariables.StartPlayGame);
        }

        public LevelData LoadLevel(int level)
        {
            int resolved = ResolveLevelNumber(level);

            string path = LevelFolder + "/Level_" + resolved;
            var data = Resources.Load<LevelData>(path);

            if (data == null)
            {
                Debug.LogError("[HoneyBeeRush] Level data not found at Resources/" + path + ".");
                data = Resources.Load<LevelData>(LevelFolder + "/Level_1");
            }

            return data;
        }

        public int ResolveLevelNumber(int level)
        {
            if (level <= MaxLevel) return Mathf.Max(1, level);

            int loopStart = Mathf.Max(1, Mathf.RoundToInt(MaxLevel * 0.5f));
            if (loopStart >= MaxLevel) return MaxLevel;

            return new System.Random(level).Next(loopStart, MaxLevel + 1);
        }

        public DifficultyTier GetDifficulty(int level)
        {
            LevelData data = LoadLevel(level);
            return data != null ? data.difficulty : DifficultyTier.Easy;
        }

        public int GetCoinRewardLevel(int honeyPercent)
        {
            return Tuning != null ? Tuning.CoinRewardFor(honeyPercent) : 50;
        }

        public void EndGame(bool win)
        {
            EndGame(win, Tuning != null ? Tuning.EndGameDelay : 1.5f);
        }

        public void EndGame(bool win, float delay)
        {
            if (!canControl) return;

            EventManager.EmitEvent(EventVariables.EndGame);
            canControl = false;

            if (win)
            {
                StartCoroutine(ShowOutcomeAfter(delay, true));
                return;
            }

            if (!m_isRevive)
            {
                m_isRevive = true;
                StartCoroutine(ShowReviveAfter(0.1f));
                return;
            }

            StartCoroutine(ShowOutcomeAfter(delay, false));
        }

        private IEnumerator ShowOutcomeAfter(float delay, bool win)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);

            UIManager.Instance.HidePanel<IngameMenu>();

            if (win) UIManager.Instance.ShowPanel<WinMenu>();
            else UIManager.Instance.ShowPanel<LoseMenu>();
        }

        private IEnumerator ShowReviveAfter(float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            UIManager.Instance.ShowPanel<LoseMenu>(panel => panel.SetReviveMode(true));
        }

        public void RecycleLevel()
        {
            canControl = false;
            EventManager.EmitEvent(EventVariables.RecycleLevel);
            if (m_levelController != null) m_levelController.DisposeSession();
        }

        public GameObject InstantiatePrefab(string assetName)
        {
            if (string.IsNullOrEmpty(assetName)) return null;

            var prefab = Resources.Load<GameObject>(assetName);
            return prefab != null ? Instantiate(prefab) : null;
        }

        public Sprite LoadSprite(string assetName) =>
            string.IsNullOrEmpty(assetName) ? null : Resources.Load<Sprite>(assetName);

#if UNITY_EDITOR
        private bool ConsumeEditorTestLevel()
        {
            if (!UnityEditor.EditorPrefs.HasKey(EditorTestLevelKey)) return false;

            int testLevel = UnityEditor.EditorPrefs.GetInt(EditorTestLevelKey);
            UnityEditor.EditorPrefs.DeleteKey(EditorTestLevelKey);

            if (testLevel <= 0) return false;

            CurrentLevel = testLevel;
            return true;
        }

        private IEnumerator AutoStartLevelEditor()
        {
            yield return null;

            UIManager.Instance.HidePanel<MainMenu>();
            UIManager.Instance.ShowPanel<IngameMenu>();

            StartCurrentLevel();
        }

        private void Update()
        {
            if (!canControl) return;

            var selected = UnityEngine.EventSystems.EventSystem.current != null
                ? UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject
                : null;
            if (selected != null && selected.GetComponent<TMPro.TMP_InputField>() != null) return;

            if (Input.GetKeyDown(KeyCode.RightArrow)) EditorJumpLevel(1);
            else if (Input.GetKeyDown(KeyCode.LeftArrow)) EditorJumpLevel(-1);
        }

        private void EditorJumpLevel(int delta)
        {
            int target = Mathf.Clamp(CurrentLevel + delta, 1, MaxLevel);
            if (target == CurrentLevel) return;

            CurrentLevelData = null;
            RecycleLevel();
            StartLevel(target);
        }
#endif
    }
}
