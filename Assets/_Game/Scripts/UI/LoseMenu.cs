using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HoneyBeeRush.Common;
using HoneyBeeRush.Managers;

namespace HoneyBeeRush.UI
{
    public sealed class LoseMenu : UIMenu
    {
        [Header("Labels")]
        [SerializeField] private TextMeshProUGUI m_titleText;
        [SerializeField] private TextMeshProUGUI m_levelText;
        [SerializeField] private TextMeshProUGUI m_reviveCostText;

        [Header("Buttons")]
        [SerializeField] private Button m_retryButton;
        [SerializeField] private Button m_reviveButton;
        [SerializeField] private Button m_quitButton;

        [Header("Animation")]
        [SerializeField] private RectTransform m_cardRoot;
        [SerializeField] private Image m_background;

        [Header("Revive")]
        [SerializeField] private int m_reviveCoinCost = 100;

        private bool m_listenersBound;
        private bool m_controlEnabled;
        private bool m_reviveMode;

        protected override void Awake()
        {
            base.Awake();
            BindButtons();
        }

        public void SetReviveMode(bool reviveMode)
        {
            m_reviveMode = reviveMode;
            ApplyMode();
        }

        public override void Show()
        {
            base.Show();

            m_controlEnabled = true;

            if (m_levelText != null && UserManager.HasInstance)
            {
                m_levelText.text = "LEVEL " + UserManager.Instance.CurrentLevel;
            }

            ApplyMode();

            if (m_background != null)
            {
                Color color = m_background.color;
                color.a = 0f;
                m_background.color = color;
                UITween.Fade(m_background, 0.92f, FadeDuration);
            }

            PopIn(m_cardRoot, 0.1f);
        }

        protected override void OnHidden()
        {
            base.OnHidden();
            m_reviveMode = false;
        }

        private void BindButtons()
        {
            if (m_listenersBound) return;
            m_listenersBound = true;

            if (m_retryButton != null) m_retryButton.onClick.AddListener(PressedRetry);
            if (m_reviveButton != null) m_reviveButton.onClick.AddListener(PressedRevive);
            if (m_quitButton != null) m_quitButton.onClick.AddListener(PressedQuit);
        }

        private void ApplyMode()
        {
            if (m_titleText != null) m_titleText.text = m_reviveMode ? "OUT OF MOVES" : "LEVEL FAILED";
            if (m_reviveCostText != null) m_reviveCostText.text = m_reviveCoinCost.ToString();

            if (m_reviveButton != null) m_reviveButton.gameObject.SetActive(m_reviveMode);
            if (m_retryButton != null) m_retryButton.gameObject.SetActive(!m_reviveMode);
        }

        public void PressedRevive()
        {
            if (!m_controlEnabled || !m_reviveMode) return;
            if (!UserManager.HasInstance) return;

            if (!UserManager.Instance.TrySpendCoins(m_reviveCoinCost))
            {
                if (m_reviveCostText != null) UITween.Punch(m_reviveCostText.transform, 0.2f, 0.3f);
                return;
            }

            m_controlEnabled = false;

            Hide();
            UIManager.Instance.ShowPanel<IngameMenu>();
            EventManager.EmitEvent(EventVariables.Revive);
        }

        public void PressedRetry()
        {
            if (!m_controlEnabled) return;
            if (!GameManager.HasInstance) return;

            m_controlEnabled = false;

            Hide();

            GameManager.Instance.RecycleLevel();
            UIManager.Instance.ShowPanel<IngameMenu>();
            GameManager.Instance.StartCurrentLevel();
        }

        public void PressedQuit()
        {
            if (!m_controlEnabled) return;
            if (!GameManager.HasInstance) return;

            m_controlEnabled = false;

            Hide();

            GameManager.Instance.RecycleLevel();
            UIManager.Instance.HidePanel<IngameMenu>();
            UIManager.Instance.ShowPanel<MainMenu>();
        }
    }
}
