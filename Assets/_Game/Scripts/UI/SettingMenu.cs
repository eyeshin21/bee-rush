using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HoneyBeeRush.Managers;

namespace HoneyBeeRush.UI
{
    public sealed class SettingMenu : UIMenu
    {
        [Header("Toggles")]
        [SerializeField] private Button m_soundButton;
        [SerializeField] private Button m_musicButton;
        [SerializeField] private Button m_vibrationButton;

        [Header("Toggle State")]
        [SerializeField] private Image m_soundIcon;
        [SerializeField] private Image m_musicIcon;
        [SerializeField] private Image m_vibrationIcon;
        [SerializeField] private Color m_onColor = Color.white;
        [SerializeField] private Color m_offColor = new Color(1f, 1f, 1f, 0.35f);

        [Header("Toggle Icon Sprites")]
        [SerializeField] private Sprite m_soundOnSprite;
        [SerializeField] private Sprite m_soundOffSprite;
        [SerializeField] private Sprite m_musicOnSprite;
        [SerializeField] private Sprite m_musicOffSprite;
        [SerializeField] private Sprite m_vibrationOnSprite;
        [SerializeField] private Sprite m_vibrationOffSprite;

        [Header("Toggle Plates")]
        [SerializeField] private Image m_soundPlate;
        [SerializeField] private Image m_musicPlate;
        [SerializeField] private Image m_vibrationPlate;
        [SerializeField] private Sprite m_plateOnSprite;
        [SerializeField] private Sprite m_plateOffSprite;

        [Header("Buttons")]
        [SerializeField] private Button m_closeButton;
        [SerializeField] private TextMeshProUGUI m_versionText;

        [Header("Animation")]
        [SerializeField] private RectTransform m_cardRoot;
        [SerializeField] private Image m_background;

        private bool m_listenersBound;

        protected override void Awake()
        {
            base.Awake();
            BindButtons();
        }

        public override void Show()
        {
            base.Show();

            RefreshToggles();

            if (m_versionText != null) m_versionText.text = "v" + Application.version;

            if (m_background != null)
            {
                Color color = m_background.color;
                color.a = 0f;
                m_background.color = color;
                Common.UITween.Fade(m_background, 0.85f, FadeDuration);
            }

            PopIn(m_cardRoot, 0.05f);
        }

        private void BindButtons()
        {
            if (m_listenersBound) return;
            m_listenersBound = true;

            if (m_soundButton != null) m_soundButton.onClick.AddListener(ToggleSound);
            if (m_musicButton != null) m_musicButton.onClick.AddListener(ToggleMusic);
            if (m_vibrationButton != null) m_vibrationButton.onClick.AddListener(ToggleVibration);
            if (m_closeButton != null) m_closeButton.onClick.AddListener(PressedClose);
        }

        public void ToggleSound()
        {
            if (!UserManager.HasInstance) return;

            UserManager.Instance.SetSoundEnabled(!UserManager.Instance.SoundEnabled);
            RefreshToggles();
        }

        public void ToggleMusic()
        {
            if (!UserManager.HasInstance) return;

            UserManager.Instance.SetMusicEnabled(!UserManager.Instance.MusicEnabled);
            RefreshToggles();
        }

        public void ToggleVibration()
        {
            if (!UserManager.HasInstance) return;

            UserManager.Instance.SetVibrationEnabled(!UserManager.Instance.VibrationEnabled);
            RefreshToggles();
        }

        public void PressedClose() => Hide();

        private void RefreshToggles()
        {
            if (!UserManager.HasInstance) return;

            UserManager user = UserManager.Instance;

            ApplyToggleIcon(m_soundIcon, m_soundOnSprite, m_soundOffSprite, user.SoundEnabled);
            ApplyToggleIcon(m_musicIcon, m_musicOnSprite, m_musicOffSprite, user.MusicEnabled);
            ApplyToggleIcon(m_vibrationIcon, m_vibrationOnSprite, m_vibrationOffSprite, user.VibrationEnabled);

            ApplyTogglePlate(m_soundPlate, user.SoundEnabled);
            ApplyTogglePlate(m_musicPlate, user.MusicEnabled);
            ApplyTogglePlate(m_vibrationPlate, user.VibrationEnabled);
        }

        private void ApplyToggleIcon(Image icon, Sprite onSprite, Sprite offSprite, bool enabled)
        {
            if (icon == null) return;

            if (onSprite == null || offSprite == null)
            {
                ApplyToggleColor(icon, enabled);
                return;
            }

            icon.sprite = enabled ? onSprite : offSprite;
            icon.color = m_onColor;
        }

        private void ApplyTogglePlate(Image plate, bool enabled)
        {
            if (plate == null) return;
            if (m_plateOnSprite == null || m_plateOffSprite == null) return;

            plate.sprite = enabled ? m_plateOnSprite : m_plateOffSprite;
        }

        private void ApplyToggleColor(Image icon, bool enabled)
        {
            if (icon == null) return;
            icon.color = enabled ? m_onColor : m_offColor;
        }
    }
}
