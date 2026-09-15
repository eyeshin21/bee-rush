using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HoneyBeeRush.Common;
using HoneyBeeRush.Gameplay.Runtime;
using HoneyBeeRush.Managers;

namespace HoneyBeeRush.UI
{
    public sealed class WinMenu : UIMenu
    {
        [Header("Labels")]
        [SerializeField] private TextMeshProUGUI m_levelText;
        [SerializeField] private TextMeshProUGUI m_descriptionText;
        [SerializeField] private TextMeshProUGUI m_coinText;

        [Header("Buttons")]
        [SerializeField] private Button m_nextButton;

        [Header("Animation")]
        [SerializeField] private RectTransform m_cardRoot;
        [SerializeField] private RectTransform m_titleRoot;
        [SerializeField] private Image m_background;

        private bool m_listenersBound;
        private bool m_canContinue;
        private int m_coinReward;

        protected override void Awake()
        {
            base.Awake();
            BindButtons();
        }

        public override void Show()
        {
            base.Show();

            m_canContinue = true;
            m_coinReward = ResolveCoinReward();

            int completedLevel = UserManager.HasInstance ? UserManager.Instance.CurrentLevel : 1;

            if (m_levelText != null) m_levelText.text = "LEVEL " + completedLevel;
            if (m_coinText != null) m_coinText.text = "+" + m_coinReward;
            if (m_descriptionText != null) m_descriptionText.text = BuildTimeDescription();

            if (m_background != null)
            {
                Color color = m_background.color;
                color.a = 0f;
                m_background.color = color;
                UITween.Fade(m_background, 0.95f, FadeDuration);
            }

            PopIn(m_titleRoot, 0.05f);
            PopIn(m_cardRoot, 0.15f);

            AwardProgress(completedLevel);
        }

        private void BindButtons()
        {
            if (m_listenersBound) return;
            m_listenersBound = true;

            if (m_nextButton != null) m_nextButton.onClick.AddListener(PressedNext);
        }

        private int ResolveCoinReward()
        {
            if (!GameManager.HasInstance) return 50;

            LevelController controller = GameManager.Instance.LevelController;
            int percent = controller != null && controller.Honey != null ? controller.Honey.Percent : 100;

            return GameManager.Instance.GetCoinRewardLevel(percent);
        }

        private string BuildTimeDescription()
        {
            if (!GameManager.HasInstance) return string.Empty;

            int elapsed = Mathf.Max(0, Mathf.RoundToInt(Time.time - GameManager.Instance.startTime));
            int minutes = elapsed / 60;
            int seconds = elapsed % 60;

            if (minutes <= 0) return "It took " + seconds + (seconds == 1 ? " second" : " seconds");

            string minutePart = minutes + (minutes == 1 ? " minute" : " minutes");
            string secondPart = seconds + (seconds == 1 ? " second" : " seconds");
            return "It took " + minutePart + " and " + secondPart;
        }

        private void AwardProgress(int completedLevel)
        {
            if (!UserManager.HasInstance) return;

            UserManager user = UserManager.Instance;

            user.AddCoins(m_coinReward);
            user.SetWinLevel(user.WinLevel + 1);
            user.SetAmountWinStreak(user.AmountWinStreak + 1);
            user.SetCurrentLevel(completedLevel + 1);
        }

        public void PressedNext()
        {
            if (!m_canContinue) return;
            if (!GameManager.HasInstance) return;

            m_canContinue = false;

            Hide();

            GameManager.Instance.RecycleLevel();
            UIManager.Instance.ShowPanel<IngameMenu>();
            GameManager.Instance.StartCurrentLevel();
        }
    }
}
