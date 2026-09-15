using UnityEngine;

namespace HoneyBeeRush.Common
{
    public class SingletonMono<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T s_instance;

        public static T Instance
        {
            get
            {
                if (s_instance == null) s_instance = FindAnyObjectByType<T>(FindObjectsInactive.Include);
                return s_instance;
            }
        }

        protected static void ReleaseInstance()
        {
            s_instance = null;
        }
    }

    public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T s_instance;

        public static T Instance
        {
            get
            {
                if (s_instance == null)
                {
                    s_instance = FindAnyObjectByType<T>(FindObjectsInactive.Include);
                    if (s_instance == null)
                    {
                        var host = new GameObject(typeof(T).Name);
                        s_instance = host.AddComponent<T>();
                    }
                }
                return s_instance;
            }
        }

        [SerializeField] private bool m_canDestroyOnLoad;

        public virtual void Awake()
        {
            if (s_instance == null)
            {
                s_instance = this as T;
                if (!m_canDestroyOnLoad) DontDestroyOnLoad(gameObject);
                return;
            }

            if (s_instance.GetInstanceID() != GetInstanceID()) Destroy(gameObject);
        }

        protected virtual void OnDestroy()
        {
            if (s_instance != null && s_instance.GetInstanceID() == GetInstanceID()) s_instance = null;
        }
    }
}
