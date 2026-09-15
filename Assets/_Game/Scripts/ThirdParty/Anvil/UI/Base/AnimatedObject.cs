using System;
using System.Collections;
using UnityEngine;

namespace Anvil
{
    public interface IAnimated
    {
        public void PlayAnimation(string animationName);
        public void PlayAnimation(string animationName, Action callback);
    }

    public class AnimatedObject : MonoBehaviour, IAnimated
    {
        [SerializeField] private bool _turnOffWhenUnUsed = false;
        private Animator _animator;

        protected virtual void Awake()
        {
            _animator = GetComponent<Animator>();
        }

        public virtual void PlayAnimation(string animationName)
        {
            PlayAnimation(animationName, null);
        }

        public virtual void PlayAnimation(string animationName, Action callback)
        {
            if (_animator == null || string.IsNullOrEmpty(animationName))
            {
                callback?.Invoke();
                return;
            }

            DisableAnimator(false);
            _animator.Play(animationName, 0, 0f);

            if (!isActiveAndEnabled)
            {
                callback?.Invoke();
                return;
            }

            StopAllCoroutines();
            StartCoroutine(WaitForAnimation(callback));
        }

        private IEnumerator WaitForAnimation(Action callback)
        {
            yield return null;
            var state = _animator.GetCurrentAnimatorStateInfo(0);
            yield return new WaitForSeconds(state.length);

            if (_turnOffWhenUnUsed)
            {
                DisableAnimator(true);
            }
            callback?.Invoke();
        }

        private void DisableAnimator(bool disable)
        {
            if (_animator != null)
            {
                _animator.enabled = !disable;
            }
        }
    }
}
