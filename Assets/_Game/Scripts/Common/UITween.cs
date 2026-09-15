using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace HoneyBeeRush.Common
{
    public enum EaseType
    {
        Linear = 0,
        InQuad = 1,
        OutQuad = 2,
        InOutQuad = 3,
        OutCubic = 4,
        OutBack = 5,
        OutElastic = 6
    }

    public sealed class TweenHandle
    {
        private Coroutine m_routine;
        private TweenRunner m_runner;
        private bool m_finished;

        public bool IsActive => !m_finished && m_routine != null;

        internal void Bind(TweenRunner runner, Coroutine routine)
        {
            m_runner = runner;
            m_routine = routine;
        }

        internal void MarkFinished()
        {
            m_finished = true;
            m_routine = null;
        }

        public void Kill()
        {
            if (m_finished) return;
            m_finished = true;

            if (m_runner != null && m_routine != null) m_runner.StopTween(m_routine);
            m_routine = null;
        }
    }

    public sealed class TweenRunner : MonoBehaviour
    {
        private static TweenRunner s_instance;
        private static bool s_quitting;

        public static TweenRunner Instance
        {
            get
            {
                if (s_quitting) return null;

                if (s_instance == null)
                {
                    var host = new GameObject("[TweenRunner]");
                    host.hideFlags = HideFlags.HideAndDontSave;
                    s_instance = host.AddComponent<TweenRunner>();
                    DontDestroyOnLoad(host);
                }

                return s_instance;
            }
        }

        public Coroutine RunTween(IEnumerator routine) => StartCoroutine(routine);

        public void StopTween(Coroutine routine)
        {
            if (routine != null) StopCoroutine(routine);
        }

        private void OnApplicationQuit() => s_quitting = true;

        private void OnDestroy()
        {
            if (s_instance == this) s_instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnDomainReload()
        {
            s_instance = null;
            s_quitting = false;
        }
    }

    public static class UITween
    {
        private const float BackOvershoot = 1.70158f;

        public static float Evaluate(EaseType ease, float t)
        {
            t = Mathf.Clamp01(t);

            switch (ease)
            {
                case EaseType.InQuad: return t * t;
                case EaseType.OutQuad: return 1f - (1f - t) * (1f - t);
                case EaseType.InOutQuad: return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) * 0.5f;
                case EaseType.OutCubic: return 1f - Mathf.Pow(1f - t, 3f);
                case EaseType.OutBack:
                {
                    float u = t - 1f;
                    return 1f + (BackOvershoot + 1f) * u * u * u + BackOvershoot * u * u;
                }
                case EaseType.OutElastic:
                {
                    if (t <= 0f) return 0f;
                    if (t >= 1f) return 1f;
                    const float period = 0.3f;
                    return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * (2f * Mathf.PI / period)) + 1f;
                }
                default: return t;
            }
        }

        public static TweenHandle Fade(Graphic target, float to, float duration,
            EaseType ease = EaseType.Linear, float delay = 0f, Action onComplete = null)
        {
            if (target == null) { onComplete?.Invoke(); return null; }

            float from = target.color.a;
            return Run(duration, delay, ease, onComplete, value =>
            {
                if (target == null) return false;
                Color color = target.color;
                color.a = Mathf.LerpUnclamped(from, to, value);
                target.color = color;
                return true;
            });
        }

        public static TweenHandle Fade(CanvasGroup target, float to, float duration,
            EaseType ease = EaseType.Linear, float delay = 0f, Action onComplete = null)
        {
            if (target == null) { onComplete?.Invoke(); return null; }

            float from = target.alpha;
            return Run(duration, delay, ease, onComplete, value =>
            {
                if (target == null) return false;
                target.alpha = Mathf.LerpUnclamped(from, to, value);
                return true;
            });
        }

        public static TweenHandle Scale(Transform target, Vector3 to, float duration,
            EaseType ease = EaseType.OutBack, float delay = 0f, Action onComplete = null)
        {
            if (target == null) { onComplete?.Invoke(); return null; }

            Vector3 from = target.localScale;
            return Run(duration, delay, ease, onComplete, value =>
            {
                if (target == null) return false;
                target.localScale = Vector3.LerpUnclamped(from, to, value);
                return true;
            });
        }

        public static TweenHandle Fill(Image target, float to, float duration,
            EaseType ease = EaseType.OutQuad, float delay = 0f, Action onComplete = null)
        {
            if (target == null) { onComplete?.Invoke(); return null; }

            float from = target.fillAmount;
            return Run(duration, delay, ease, onComplete, value =>
            {
                if (target == null) return false;
                target.fillAmount = Mathf.LerpUnclamped(from, to, value);
                return true;
            });
        }

        public static TweenHandle AnchorPos(RectTransform target, Vector2 to, float duration,
            EaseType ease = EaseType.OutCubic, float delay = 0f, Action onComplete = null)
        {
            if (target == null) { onComplete?.Invoke(); return null; }

            Vector2 from = target.anchoredPosition;
            return Run(duration, delay, ease, onComplete, value =>
            {
                if (target == null) return false;
                target.anchoredPosition = Vector2.LerpUnclamped(from, to, value);
                return true;
            });
        }

        public static TweenHandle Punch(Transform target, float strength, float duration,
            Action onComplete = null)
        {
            if (target == null) { onComplete?.Invoke(); return null; }

            Vector3 baseScale = target.localScale;
            return Run(duration, 0f, EaseType.Linear, onComplete, value =>
            {
                if (target == null) return false;
                float wave = Mathf.Sin(value * Mathf.PI) * strength;
                target.localScale = baseScale * (1f + wave);
                return true;
            }, () =>
            {
                if (target != null) target.localScale = baseScale;
            });
        }

        public static TweenHandle Delay(float duration, Action onComplete)
        {
            return Run(duration, 0f, EaseType.Linear, onComplete, value => true);
        }

        public static void Kill(ref TweenHandle handle)
        {
            if (handle == null) return;
            handle.Kill();
            handle = null;
        }

        private static TweenHandle Run(float duration, float delay, EaseType ease, Action onComplete,
            Func<float, bool> apply, Action onStopped = null)
        {
            var handle = new TweenHandle();

            TweenRunner runner = TweenRunner.Instance;
            if (runner == null)
            {
                apply?.Invoke(1f);
                onStopped?.Invoke();
                onComplete?.Invoke();
                handle.MarkFinished();
                return handle;
            }

            Coroutine routine = runner.RunTween(Routine(handle, duration, delay, ease, onComplete, apply, onStopped));
            handle.Bind(runner, routine);
            return handle;
        }

        private static IEnumerator Routine(TweenHandle handle, float duration, float delay, EaseType ease,
            Action onComplete, Func<float, bool> apply, Action onStopped)
        {
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

            if (duration <= 0f)
            {
                apply(1f);
                onStopped?.Invoke();
                handle.MarkFinished();
                onComplete?.Invoke();
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                if (!apply(Evaluate(ease, t)))
                {
                    handle.MarkFinished();
                    yield break;
                }

                yield return null;
            }

            apply(Evaluate(ease, 1f));
            onStopped?.Invoke();
            handle.MarkFinished();
            onComplete?.Invoke();
        }
    }
}
