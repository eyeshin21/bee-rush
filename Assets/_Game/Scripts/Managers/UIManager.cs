using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using HoneyBeeRush.Common;
using HoneyBeeRush.UI;

namespace HoneyBeeRush.Managers
{
    public sealed class PanelInstance<T> where T : MonoBehaviour
    {
        private readonly string m_prefabName;
        private readonly Transform m_parent;
        private T m_panel;

        public bool IsInstantiated => m_panel != null;

        public PanelInstance(string prefabName, Transform parent)
        {
            m_prefabName = prefabName;
            m_parent = parent;
        }

        public T Panel
        {
            get
            {
                if (m_panel != null) return m_panel;

                m_panel = FindExisting();
                if (m_panel != null) return m_panel;

                var prefab = Resources.Load<GameObject>(UIManager.PanelResourceFolder + "/" + m_prefabName);
                if (prefab == null)
                {
                    Debug.LogError("[UIManager] No panel prefab at Resources/" + UIManager.PanelResourceFolder +
                                   "/" + m_prefabName + ". Run Honey Bee Rush/UI/Build UI Prefabs.");
                    return null;
                }

                GameObject spawned = UnityEngine.Object.Instantiate(prefab, m_parent);
                spawned.name = m_prefabName;
                m_panel = spawned.GetComponent<T>();

                if (m_panel == null)
                {
                    Debug.LogError("[UIManager] Prefab " + m_prefabName + " carries no " + typeof(T).Name + ".");
                    UnityEngine.Object.Destroy(spawned);
                }

                return m_panel;
            }
        }

        private T FindExisting()
        {
            if (m_parent == null) return null;

            for (int i = 0; i < m_parent.childCount; i++)
            {
                T candidate = m_parent.GetChild(i).GetComponent<T>();
                if (candidate != null) return candidate;
            }

            return null;
        }
    }

    [DefaultExecutionOrder(-700)]
    public sealed class UIManager : Singleton<UIManager>
    {
        public const string PanelResourceFolder = "UI";

        [Header("Canvas")]
        [SerializeField] private Transform m_panelRoot;
        [SerializeField] private Canvas m_canvas;

        [Header("Startup")]
        [Tooltip("Show the main menu when the UI comes up. Turn off for a scene that starts straight " +
                 "into a level, such as the level editor preview.")]
        [SerializeField] private bool m_showMainMenuOnStart = true;

        private readonly Dictionary<string, object> m_panelInstances = new Dictionary<string, object>();

        public Canvas Canvas => m_canvas;
        public Transform PanelRoot => EnsurePanelRoot();

        public MainMenu MainMenu => GetPanel<MainMenu>();
        public IngameMenu IngameMenu => GetPanel<IngameMenu>();
        public WinMenu WinMenu => GetPanel<WinMenu>();
        public LoseMenu LoseMenu => GetPanel<LoseMenu>();
        public SettingMenu SettingMenu => GetPanel<SettingMenu>();

        public override void Awake()
        {
            base.Awake();
            EnsurePanelRoot();
        }

        private void Start()
        {
            if (m_showMainMenuOnStart) ShowPanel<MainMenu>();
        }

        public T GetPanel<T>() where T : MonoBehaviour
        {
            string key = typeof(T).Name;

            if (!m_panelInstances.TryGetValue(key, out object stored))
            {
                stored = new PanelInstance<T>(key, EnsurePanelRoot());
                m_panelInstances.Add(key, stored);
            }

            if (!(stored is PanelInstance<T> instance))
            {
                Debug.LogError("[UIManager] Panel slot " + key + " holds an unexpected type.");
                return null;
            }

            return instance.Panel;
        }

        public T ShowPanel<T>(Action<T> configure = null) where T : UIMenu
        {
            T panel = GetPanel<T>();
            if (panel == null) return null;

            configure?.Invoke(panel);
            panel.Show();
            return panel;
        }

        public void HidePanel<T>() where T : UIMenu
        {
            if (!m_panelInstances.TryGetValue(typeof(T).Name, out object stored)) return;
            if (!(stored is PanelInstance<T> instance) || !instance.IsInstantiated) return;

            instance.Panel.Hide();
        }

        public bool IsPanelOpen<T>() where T : UIMenu
        {
            if (!m_panelInstances.TryGetValue(typeof(T).Name, out object stored)) return false;
            if (!(stored is PanelInstance<T> instance) || !instance.IsInstantiated) return false;

            return instance.Panel.gameObject.activeInHierarchy;
        }

        public bool IsPointerOverUI()
        {
            EventSystem current = EventSystem.current;
            if (current == null) return false;

            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                return current.IsPointerOverGameObject(touch.fingerId);
            }

            return current.IsPointerOverGameObject();
        }

        private Transform EnsurePanelRoot()
        {
            if (m_panelRoot != null) return m_panelRoot;

            if (m_canvas == null) m_canvas = GetComponentInChildren<Canvas>(true);

            if (m_canvas == null)
            {
                var canvasHost = new GameObject("UICanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvasHost.transform.SetParent(transform, false);

                m_canvas = canvasHost.GetComponent<Canvas>();
                m_canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                m_canvas.sortingOrder = 100;

                var scaler = canvasHost.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080f, 1920f);
                scaler.matchWidthOrHeight = 0.5f;
            }

            if (EventSystem.current == null)
            {
                var eventHost = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                eventHost.transform.SetParent(transform, false);
            }

            m_panelRoot = m_canvas.transform;
            return m_panelRoot;
        }
    }
}
