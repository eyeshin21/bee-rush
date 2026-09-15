using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Anvil
{
    public interface IUIButton
    {
        public GameObject gameObject { get; }
        void SetInteractable(bool interactable);

        void AddListener(Action callback);
        void RemoveListener(Action callback);
    }
    public interface ILabeledUIButton : IUIButton
    {
        void SetLabel(string label);
    }
    public interface IIconedUIButton : IUIButton
    {
        void SetIcon(Sprite icon);
    }
    public interface IAnimatedUIButton : IUIButton
    {
        void PlayAnimation(string animationName);
    }
    public interface IToggleUIButton : IUIButton
    {
        void Switch();
        void SetIsOn(bool isOn);
        bool GetIsOn();
    }

    public class UIButton : MonoBehaviour, IUIButton, IPointerDownHandler, IPointerUpHandler
    {
        protected enum ClickAnimationState
        {
            Idle,
            Pressing,
            Releasing
        }

        [SerializeField] protected Button _button;
        [SerializeField] protected float _doubleClickBlockDuration = 0.2f;

        [Header("Click Animation")]
        [SerializeField] protected bool _useClickAnimation = true;
        [SerializeField] protected Transform _clickAnimationTarget;
        [SerializeField][Range(0.5f, 1.5f)] protected float _pressedScale = 0.92f;
        [SerializeField][Range(0f, 0.5f)] protected float _pressDuration = 0.07f;
        [SerializeField][Range(0f, 1f)] protected float _releaseDuration = 0.18f;
        [SerializeField][Range(0f, 3f)] protected float _releaseOvershoot = 2f;

        protected float _clickCoolDown = 0;
        protected Action _onClickAction;

        private ClickAnimationState _clickAnimationState = ClickAnimationState.Idle;
        private Vector3 _restingScale = Vector3.one;
        private Vector3 _lastWrittenScale = Vector3.one;
        private bool _hasRestingScale = false;
        private float _clickAnimationTime = 0;
        private float _pressFromScale = 1f;
        private float _releaseFromScale = 1f;
        private bool _isPointerDown = false;

        protected virtual void Awake()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(()=>
            {
                OnClick();
            });

            if (_clickAnimationTarget == null)
            {
                _clickAnimationTarget = transform;
            }
        }

        protected virtual bool OnClick()
        {
            if (_clickCoolDown > 0)
            {
                return false;
            }
            _clickCoolDown = _doubleClickBlockDuration;

            _onClickAction?.Invoke();
            return true;
        }

        public virtual void OnPointerDown(PointerEventData eventData)
        {
            if (!CanPlayClickAnimation())
            {
                return;
            }
            _isPointerDown = true;
            StartPressAnimation();
        }

        public virtual void OnPointerUp(PointerEventData eventData)
        {
            if (!_isPointerDown)
            {
                return;
            }
            _isPointerDown = false;
            StartReleaseAnimation();
        }

        public void PlayClickAnimation()
        {
            if (!CanPlayClickAnimation())
            {
                return;
            }
            _isPointerDown = false;
            StartPressAnimation();
        }

        public void SetClickAnimationEnabled(bool value)
        {
            _useClickAnimation = value;
            if (!value)
            {
                ResetClickAnimation();
            }
        }

        protected virtual bool CanPlayClickAnimation()
        {
            if (!_useClickAnimation || _clickAnimationTarget == null)
            {
                return false;
            }
            return _button == null || _button.interactable;
        }

        private void StartPressAnimation()
        {
            _pressFromScale = GetCurrentFactor();
            _clickAnimationState = ClickAnimationState.Pressing;
            _clickAnimationTime = 0;
        }

        private void StartReleaseAnimation()
        {
            if (_clickAnimationState != ClickAnimationState.Pressing)
            {
                return;
            }
            _releaseFromScale = GetPressFactor(_clickAnimationTime);
            _clickAnimationState = ClickAnimationState.Releasing;
            _clickAnimationTime = 0;
        }

        private float GetCurrentFactor()
        {
            if (_clickAnimationState == ClickAnimationState.Pressing)
            {
                return GetPressFactor(_clickAnimationTime);
            }
            if (_clickAnimationState == ClickAnimationState.Releasing)
            {
                return GetReleaseFactor(_clickAnimationTime);
            }
            return 1f;
        }

        private void ResetClickAnimation()
        {
            _isPointerDown = false;
            if (_clickAnimationState == ClickAnimationState.Idle)
            {
                return;
            }
            EndClickAnimation();
        }

        private float GetPressFactor(float time)
        {
            float t = _pressDuration <= 0 ? 1f : Mathf.Clamp01(time / _pressDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            return Mathf.Lerp(_pressFromScale, _pressedScale, eased);
        }

        private float GetReleaseFactor(float time)
        {
            float t = _releaseDuration <= 0 ? 1f : Mathf.Clamp01(time / _releaseDuration);
            float c1 = 1.70158f * _releaseOvershoot;
            float c3 = c1 + 1f;
            float p = t - 1f;
            float eased = 1f + c3 * p * p * p + c1 * p * p;
            return Mathf.LerpUnclamped(_releaseFromScale, 1f, eased);
        }

        private void ApplyClickAnimationScale(float factor)
        {
            if (_clickAnimationTarget == null)
            {
                return;
            }
            var current = _clickAnimationTarget.localScale;
            if (!_hasRestingScale || current != _lastWrittenScale)
            {
                _restingScale = current;
                _hasRestingScale = true;
            }

            var next = _restingScale * factor;
            _clickAnimationTarget.localScale = next;
            _lastWrittenScale = next;
        }

        private void EndClickAnimation()
        {
            ApplyClickAnimationScale(1f);
            _clickAnimationState = ClickAnimationState.Idle;
            _hasRestingScale = false;
        }

        private void UpdateClickAnimation(float deltaTime)
        {
            if (_clickAnimationState == ClickAnimationState.Idle)
            {
                return;
            }

            if (_clickAnimationTarget == null)
            {
                _clickAnimationState = ClickAnimationState.Idle;
                return;
            }

            _clickAnimationTime += deltaTime;

            if (_clickAnimationState == ClickAnimationState.Pressing)
            {
                ApplyClickAnimationScale(GetPressFactor(_clickAnimationTime));
                if (!_isPointerDown && _clickAnimationTime >= _pressDuration)
                {
                    StartReleaseAnimation();
                }
                return;
            }

            ApplyClickAnimationScale(GetReleaseFactor(_clickAnimationTime));
            if (_clickAnimationTime >= _releaseDuration)
            {
                EndClickAnimation();
            }
        }

        public virtual void AddListener(Action callback)
        {
            _onClickAction += callback;
        }
        public virtual void AddListenerOnce(Action callback)
        {
            _onClickAction += Wrapper;
            return;

            void Wrapper()
            {
                callback?.Invoke();
                RemoveListener(Wrapper);
            }
        }
        public virtual void ClearListeners()
        {
            _onClickAction = null;
        }
        public virtual void RemoveListener(Action callback)
        {
            _onClickAction -= callback;
        }
        public void SetInteractable(bool interactable)
        {
            if (_button != null)
            {
                _button.interactable = interactable;
            }
        }

        public void SimulateClick()
        {
            PlayClickAnimation();
            OnClick();
        }

        protected virtual void OnDisable()
        {
            ResetClickAnimation();
        }

        protected virtual void Update()
        {
            if (_clickCoolDown > 0)
            {
                _clickCoolDown -= Time.unscaledDeltaTime;
            }
            UpdateClickAnimation(Time.unscaledDeltaTime);
        }

        protected static bool HasAnimationState(GameObject target, string animationName)
        {
            if (target == null || string.IsNullOrEmpty(animationName))
            {
                return false;
            }

            int stateHash = Animator.StringToHash(animationName);
            var animators = target.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                var animator = animators[i];
                if (animator.runtimeAnimatorController != null && animator.HasState(0, stateHash))
                {
                    return true;
                }
            }

            var legacyAnimations = target.GetComponentsInChildren<Animation>(true);
            for (int i = 0; i < legacyAnimations.Length; i++)
            {
                if (legacyAnimations[i].GetClip(animationName) != null)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public static class UIButtonExtensions
    {
        public static void AddListenerSafe(this GameObject gameObject, Action callback)
        {
            if (gameObject == null || callback == null)
            {
                return;
            }
            var button = gameObject.GetComponent<IUIButton>();
            if (button == null)
            {
                return;
            }
            button.AddListener(callback);
        }
        public static void AddListenerSafe(this IUIButton button, Action callback)
        {
            if (button == null || callback == null)
            {
                return;
            }
            button.AddListener(callback);
        }
        public static void SetLabelSafe(this IUIButton button, string label)
        {
            if (button == null)
            {
                return;
            }

            if (button is ILabeledUIButton labeledbutton)
            {
                labeledbutton.SetLabel(label);
            }
        }
        public static void SetIconSafe(this IUIButton button, Sprite icon)
        {
            if (button == null)
            {
                return;
            }

            if (button is IIconedUIButton iconButton)
            {
                iconButton.SetIcon(icon);
            }
        }
        public static void PlayClickAnimation(this IUIButton button)
        {
            if (button == null)
            {
                return;
            }

            if (button is IAnimatedUIButton animatedButton)
            {
                animatedButton.PlayAnimation("ButtonPress");
                return;
            }

            if (button is UIButton uiButton)
            {
                uiButton.PlayClickAnimation();
            }
        }
    }
}
