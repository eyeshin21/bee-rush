using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HoneyBeeRush.Common;
using HoneyBeeRush.Gameplay.Runtime;
using HoneyBeeRush.Managers;

namespace HoneyBeeRush.UI
{
    public sealed class IngameMenu : UIMenu
    {
        [Header("Header")]
        [SerializeField] private TextMeshProUGUI m_levelText;
        [SerializeField] private TextMeshProUGUI m_honeyText;
        [SerializeField] private Image m_honeyFill;
        [SerializeField] private RectTransform m_headerRoot;

        [Header("Buttons")]
        [SerializeField] private Button m_settingsButton;
        [SerializeField] private Button m_replayButton;

        [Header("Boosters")]
        [SerializeField] private RectTransform m_boosterRoot;
        [SerializeField] private Button m_shuffleButton;
        [SerializeField] private TextMeshProUGUI m_shuffleCountText;
        [SerializeField] private GameObject m_shuffleBadgeRoot;
        [SerializeField] private GameObject m_shuffleAddIcon;

        private TweenHandle m_fillTween;
        private float m_displayedPercent;
        private bool m_controlEnabled = true;
        private bool m_listenersBound;

        private LevelController LevelController =>
            GameManager.HasInstance ? GameManager.Instance.LevelController : null;

        protected override void Awake()
        {
            base.Awake();
            BindButtons();
        }

        private void OnEnable()
        {
            EventManager.StartListening(EventVariables.LoadLevel, OnLoadLevel);
            EventManager.StartListening(EventVariables.EndGame, OnEndGame);
            EventManager.StartListening(EventVariables.HoneyChanged, OnHoneyChanged);
            EventManager.StartListening(EventVariables.Revive, OnRevive);

            if (UserManager.HasInstance) UserManager.Instance.OnResourcesChanged += RefreshBoosters;

            RefreshBoosters();
        }

        private void OnDisable()
        {
            EventManager.StopListening(EventVariables.LoadLevel, OnLoadLevel);
            EventManager.StopListening(EventVariables.EndGame, OnEndGame);
            EventManager.StopListening(EventVariables.HoneyChanged, OnHoneyChanged);
            EventManager.StopListening(EventVariables.Revive, OnRevive);

            if (UserManager.HasInstance) UserManager.Instance.OnResourcesChanged -= RefreshBoosters;

            UITween.Kill(ref m_fillTween);
        }

        public override void Show()
        {
            base.Show();

            m_controlEnabled = true;

            RefreshLevelLabel();
            ResetProgress();
            RefreshBoosters();

            if (m_headerRoot != null)
            {
                float delay = Tuning != null ? Tuning.HudAppearDelay : 0.26f;
                Vector2 target = m_headerRoot.anchoredPosition;

                m_headerRoot.anchoredPosition = target + new Vector2(0f, 220f);
                UITween.AnchorPos(m_headerRoot, target, PopDuration * 1.5f, EaseType.OutCubic, delay);
            }
        }

        private void BindButtons()
        {
            if (m_listenersBound) return;
            m_listenersBound = true;

            if (m_settingsButton != null) m_settingsButton.onClick.AddListener(PressedSettings);
            if (m_replayButton != null) m_replayButton.onClick.AddListener(PressedReplay);
            if (m_shuffleButton != null) m_shuffleButton.onClick.AddListener(PressedShuffle);
        }

        public void RefreshLevelLabel()
        {
            if (m_levelText == null) return;

            int level = UserManager.HasInstance ? UserManager.Instance.CurrentLevel : 1;
            m_levelText.text = "LEVEL " + level;
        }

        public void SetProgress(int percent)
        {
            percent = Mathf.Clamp(percent, 0, 100);

            if (m_honeyText != null) m_honeyText.text = percent + "%";
            if (m_honeyFill == null) return;

            float target = percent * 0.01f;
            if (target <= m_displayedPercent) return;

            m_displayedPercent = target;

            UITween.Kill(ref m_fillTween);
            float duration = Tuning != null ? Tuning.ProgressAnimDuration : 0.2f;
            m_fillTween = UITween.Fill(m_honeyFill, target, duration, EaseType.OutQuad);
        }

        public void ResetProgress()
        {
            UITween.Kill(ref m_fillTween);

            m_displayedPercent = 0f;
            if (m_honeyFill != null) m_honeyFill.fillAmount = 0f;
            if (m_honeyText != null) m_honeyText.text = "0%";
        }

        public void RefreshBoosters()
        {
            if (!UserManager.HasInstance) return;

            int shuffle = UserManager.Instance.GetBoosterCount(BoosterType.Shuffle);

            if (m_shuffleCountText != null)
            {
                m_shuffleCountText.text = shuffle.ToString();
                m_shuffleCountText.gameObject.SetActive(shuffle > 0);
            }

            if (m_shuffleBadgeRoot != null) m_shuffleBadgeRoot.SetActive(shuffle > 0);

            if (m_shuffleAddIcon != null) m_shuffleAddIcon.SetActive(shuffle <= 0);
        }

        public void EnableBoosterRow(bool enabled)
        {
            if (m_boosterRoot != null) m_boosterRoot.gameObject.SetActive(enabled);
        }

        public void PressedSettings()
        {
            if (!m_controlEnabled) return;
            UIManager.Instance.ShowPanel<SettingMenu>();
        }

        public void PressedReplay()
        {
            if (!m_controlEnabled) return;
            if (!GameManager.HasInstance) return;

            m_controlEnabled = false;

            GameManager.Instance.RecycleLevel();
            GameManager.Instance.StartCurrentLevel();

            m_controlEnabled = true;
        }

        public void PressedShuffle()
        {
            if (!m_controlEnabled) return;

            LevelController controller = LevelController;
            if (controller == null) return;

            if (!controller.CanShuffle())
            {
                if (m_shuffleButton != null) UITween.Punch(m_shuffleButton.transform, 0.12f, 0.25f);
                return;
            }

            if (!UserManager.Instance.TrySpendBooster(BoosterType.Shuffle))
            {
                if (m_shuffleAddIcon != null) UITween.Punch(m_shuffleAddIcon.transform, 0.18f, 0.3f);
                return;
            }

            controller.ApplyShuffleBooster();
            RefreshBoosters();
        }

        private void OnLoadLevel()
        {
            m_controlEnabled = true;
            RefreshLevelLabel();
            ResetProgress();
        }

        private void OnEndGame() => m_controlEnabled = false;

        private void OnRevive() => m_controlEnabled = true;

        private void OnHoneyChanged() => SetProgress(EventManager.GetInt(EventVariables.HoneyChanged));
    }
}
