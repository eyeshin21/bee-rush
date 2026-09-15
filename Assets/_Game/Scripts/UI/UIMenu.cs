using UnityEngine;
using HoneyBeeRush.Common;
using HoneyBeeRush.Gameplay.Config;

namespace HoneyBeeRush.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class UIMenu : MonoBehaviour
    {
        [Header("Menu")]
        [Tooltip("Off destroys the panel on Hide instead of deactivating it, so a rarely used popup " +
                 "does not keep its hierarchy alive.")]
        [SerializeField] private bool m_canCache = true;

        [Tooltip("Fade the panel in and out on Show/Hide.")]
        [SerializeField] private bool m_fadeOnToggle = true;

        private CanvasGroup m_canvasGroup;
        private RectTransform m_rect;
        private TweenHandle m_fadeTween;

        protected CanvasGroup Group => EnsureGroup();
        protected bool IsShowing { get; private set; }

        protected TuningConfig Tuning => TuningConfig.Instance;

        protected float FadeDuration => Tuning != null ? Tuning.PanelFadeDuration : 0.25f;
        protected float PopDuration => Tuning != null ? Tuning.PanelPopDuration : 0.25f;

        protected virtual void Awake()
        {
            EnsureGroup();
            StretchToParent();
        }

        public virtual void Show()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            StretchToParent();

            IsShowing = true;

            CanvasGroup group = EnsureGroup();
            group.blocksRaycasts = true;
            group.interactable = true;

            UITween.Kill(ref m_fadeTween);

            if (!m_fadeOnToggle)
            {
                group.alpha = 1f;
                OnShown();
                return;
            }

            group.alpha = 0f;
            m_fadeTween = UITween.Fade(group, 1f, FadeDuration, EaseType.OutQuad, 0f, OnShown);
        }

        public virtual void Hide()
        {
            if (!IsShowing && !gameObject.activeSelf) return;

            IsShowing = false;

            CanvasGroup group = EnsureGroup();
            group.blocksRaycasts = false;
            group.interactable = false;

            UITween.Kill(ref m_fadeTween);

            if (!m_fadeOnToggle || !gameObject.activeInHierarchy)
            {
                Close();
                return;
            }

            m_fadeTween = UITween.Fade(group, 0f, FadeDuration, EaseType.InQuad, 0f, Close);
        }

        protected virtual void OnShown()
        {
        }

        protected virtual void OnHidden()
        {
        }

        protected void Close()
        {
            if (this == null) return;

            OnHidden();

            if (!m_canCache)
            {
                Destroy(gameObject);
                return;
            }

            gameObject.SetActive(false);
        }

        protected void PopIn(Transform target, float delay = 0f)
        {
            if (target == null) return;

            target.localScale = Vector3.zero;
            UITween.Scale(target, Vector3.one, PopDuration, EaseType.OutBack, delay);
        }

        private CanvasGroup EnsureGroup()
        {
            if (m_canvasGroup == null) m_canvasGroup = GetComponent<CanvasGroup>();
            if (m_canvasGroup == null) m_canvasGroup = gameObject.AddComponent<CanvasGroup>();

            return m_canvasGroup;
        }

        private void StretchToParent()
        {
            if (m_rect == null) m_rect = transform as RectTransform;
            if (m_rect == null) return;

            m_rect.anchorMin = Vector2.zero;
            m_rect.anchorMax = Vector2.one;
            m_rect.offsetMin = Vector2.zero;
            m_rect.offsetMax = Vector2.zero;
            m_rect.localScale = Vector3.one;
        }

        protected virtual void OnDestroy()
        {
            UITween.Kill(ref m_fadeTween);
        }
    }
}
