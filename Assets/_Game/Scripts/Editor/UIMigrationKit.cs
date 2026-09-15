using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

namespace HoneyBeeRush.EditorTools
{
    public static class UIMig
    {
        public static Transform Find(GameObject root, string path)
        {
            Transform t = root.transform.Find(path);
            if (t == null) Debug.LogError("UIMIG: path not found '" + path + "'");
            return t;
        }

        public static void Wire(SerializedObject so, GameObject root, string field, string path, System.Type type)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p == null)
            {
                Debug.LogError("UIMIG: no serialized field '" + field + "' on " + so.targetObject.GetType().Name);
                return;
            }

            Transform t = Find(root, path);
            if (t == null) return;

            Object value = type == typeof(RectTransform) ? (Object)(t as RectTransform)
                         : type == typeof(GameObject) ? (Object)t.gameObject
                         : t.GetComponent(type);

            if (value == null)
            {
                Debug.LogError("UIMIG: '" + path + "' has no " + type.Name);
                return;
            }

            p.objectReferenceValue = value;
        }

        public static void WireAsset(SerializedObject so, string field, Object asset)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p == null) { Debug.LogError("UIMIG: no serialized field '" + field + "'"); return; }
            if (asset == null) { Debug.LogError("UIMIG: null asset for '" + field + "'"); return; }
            p.objectReferenceValue = asset;
        }

        public static Sprite Sprite(string assetPath)
        {
            Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (s == null) Debug.LogError("UIMIG: sprite not found " + assetPath);
            return s;
        }

        public static int StripMissing(GameObject root)
        {
            int n = 0;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                n += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            return n;
        }

        public static void StripNestedCanvases(GameObject root)
        {
            foreach (GraphicRaycaster g in root.GetComponentsInChildren<GraphicRaycaster>(true))
                if (g != null) Object.DestroyImmediate(g, true);

            foreach (CanvasScaler s in root.GetComponentsInChildren<CanvasScaler>(true))
                if (s != null) Object.DestroyImmediate(s, true);

            foreach (Canvas c in root.GetComponentsInChildren<Canvas>(true))
                if (c != null) Object.DestroyImmediate(c, true);
        }

        public static void StripComponent<T>(GameObject root, string path) where T : Component
        {
            Transform t = path == null ? root.transform : Find(root, path);
            if (t == null) return;
            T c = t.GetComponent<T>();
            if (c != null) Object.DestroyImmediate(c, true);
        }

        public static void ClearPersistentCalls(GameObject root)
        {
            foreach (Button b in root.GetComponentsInChildren<Button>(true))
            {
                var so = new SerializedObject(b);
                SerializedProperty calls = so.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
                if (calls != null && calls.arraySize > 0)
                {
                    calls.ClearArray();
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        public static void Delete(GameObject root, string path)
        {
            Transform t = root.transform.Find(path);
            if (t != null) Object.DestroyImmediate(t.gameObject, true);
        }

        public static void SetActive(GameObject root, string path, bool active)
        {
            Transform t = Find(root, path);
            if (t != null) t.gameObject.SetActive(active);
        }

        public static void SetText(GameObject root, string path, string text)
        {
            Transform t = Find(root, path);
            if (t == null) return;
            var tmp = t.GetComponent<TMPro.TextMeshProUGUI>();
            if (tmp == null) { Debug.LogError("UIMIG: no TMP at " + path); return; }
            tmp.text = text;
        }

        public static void SetSprite(GameObject root, string path, string spriteAssetPath)
        {
            Transform t = Find(root, path);
            if (t == null) return;
            Image img = t.GetComponent<Image>();
            if (img == null) { Debug.LogError("UIMIG: no Image at " + path); return; }
            Sprite s = Sprite(spriteAssetPath);
            if (s != null) img.sprite = s;
        }

        public static GameObject Clone(GameObject root, string srcPath, string newName, string parentPath)
        {
            Transform src = Find(root, srcPath);
            if (src == null) return null;

            Transform parent = string.IsNullOrEmpty(parentPath) ? src.parent : Find(root, parentPath);
            if (parent == null) return null;

            GameObject copy = Object.Instantiate(src.gameObject, parent);
            copy.name = newName;

            RectTransform a = src as RectTransform;
            RectTransform b = copy.transform as RectTransform;
            if (a != null && b != null)
            {
                b.anchorMin = a.anchorMin;
                b.anchorMax = a.anchorMax;
                b.pivot = a.pivot;
                b.anchoredPosition = a.anchoredPosition;
                b.sizeDelta = a.sizeDelta;
                b.localScale = a.localScale;
            }
            return copy;
        }

        public static GameObject NewNode(GameObject root, string parentPath, string name,
                                         Vector2 anchoredPos, Vector2 size)
        {
            Transform parent = string.IsNullOrEmpty(parentPath) ? root.transform : Find(root, parentPath);
            if (parent == null) return null;

            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = go.transform as RectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            rt.localScale = Vector3.one;
            return go;
        }

        public static void Stretch(GameObject go)
        {
            var rt = go.transform as RectTransform;
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        public static void SetSize(GameObject root, string path, Vector2 size)
        {
            Transform t = Find(root, path);
            if (t == null) return;
            var rt = t as RectTransform;
            if (rt != null) rt.sizeDelta = size;
        }

        public static void SetPos(GameObject root, string path, Vector2 pos)
        {
            Transform t = Find(root, path);
            if (t == null) return;
            var rt = t as RectTransform;
            if (rt != null) rt.anchoredPosition = pos;
        }

        public static GameObject Open(string donorPath, string newRootName)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(donorPath);
            if (root == null) { Debug.LogError("UIMIG: donor not found " + donorPath); return null; }

            root.name = newRootName;
            int stripped = StripMissing(root);
            StripNestedCanvases(root);

            if (root.GetComponent<CanvasGroup>() == null) root.AddComponent<CanvasGroup>();
            Stretch(root);

            Debug.Log("UIMIG: opened " + donorPath + " as " + newRootName + " (stripped " + stripped + " missing scripts)");
            return root;
        }

        public static void Save(GameObject root, string dest)
        {
            ClearPersistentCalls(root);
            PrefabUtility.SaveAsPrefabAsset(root, dest);
            PrefabUtility.UnloadPrefabContents(root);
            Debug.Log("UIMIG: SAVED " + dest);
        }
    }
}
