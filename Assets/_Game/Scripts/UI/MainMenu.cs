using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HoneyBeeRush.Common;
using HoneyBeeRush.Managers;

namespace HoneyBeeRush.UI
{
    public sealed class MainMenu : UIMenu
    {
        [Header("Labels")]
        [SerializeField] private TextMeshProUGUI m_levelText;
        [SerializeField] private TextMeshProUGUI m_coinText;

        [Header("Buttons")]
        [SerializeField] private Button m_playButton;
        [SerializeField] private Button m_settingsButton;

        [Header("Animation")]
        [SerializeField] private RectTransform m_playRoot;
        [SerializeField] private RectTransform m_titleRoot;

        private bool m_listenersBound;
        private bool m_controlEnabled = true;

        protected override void Awake()
        {
            base.Awake();
            BindButtons();
        }

        private void OnEnable()
        {
            if (UserManager.HasInstance) UserManager.Instance.OnResourcesChanged += RefreshLabels;
        }

        private void OnDisable()
        {
            if (UserManager.HasInstance) UserManager.Instance.OnResourcesChanged -= RefreshLabels;
        }

        public override void Show()
        {
            base.Show();

            m_controlEnabled = true;
            RefreshLabels();

            PopIn(m_titleRoot, 0.05f);
            PopIn(m_playRoot, 0.15f);
        }

        private void BindButtons()
        {
            if (m_listenersBound) return;
            m_listenersBound = true;

            if (m_playButton != null) m_playButton.onClick.AddListener(PressedPlay);
            if (m_settingsButton != null) m_settingsButton.onClick.AddListener(PressedSettings);
        }

        public void RefreshLabels()
        {
            if (!UserManager.HasInstance) return;

            if (m_levelText != null) m_levelText.text = "LEVEL " + UserManager.Instance.CurrentLevel;
            if (m_coinText != null) m_coinText.text = UserManager.Instance.Coins.ToString();
        }

        public void PressedPlay()
        {
            if (!m_controlEnabled) return;
            if (!GameManager.HasInstance) return;

            m_controlEnabled = false;

            Hide();
            UIManager.Instance.ShowPanel<IngameMenu>();
            GameManager.Instance.StartCurrentLevel();
        }

        public void PressedSettings()
        {
            if (!m_controlEnabled) return;
            UIManager.Instance.ShowPanel<SettingMenu>();
        }
    }
}
