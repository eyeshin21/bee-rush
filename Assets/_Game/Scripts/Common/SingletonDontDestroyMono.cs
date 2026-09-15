using UnityEngine;

namespace HoneyBeeRush.Common
{
    public class SingletonDontDestroyMono<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T s_instance;

        public static T Instance
        {
            get
            {
                if (s_instance == null) s_instance = FindAnyObjectByType<T>(FindObjectsInactive.Include);

                if (s_instance == null)
                {
                    var host = new GameObject(typeof(T).Name);
                    s_instance = host.AddComponent<T>();
                }

                return s_instance;
            }
        }

        public static bool HasInstance => s_instance != null;

        protected virtual void Awake()
        {
            if (s_instance != null && s_instance.GetInstanceID() != GetInstanceID())
            {
                Destroy(gameObject);
                return;
            }

            s_instance = this as T;
            DontDestroyOnLoad(gameObject);
        }

        protected virtual void OnDestroy()
        {
            if (s_instance != null && s_instance.GetInstanceID() == GetInstanceID()) s_instance = null;
        }
    }
}
