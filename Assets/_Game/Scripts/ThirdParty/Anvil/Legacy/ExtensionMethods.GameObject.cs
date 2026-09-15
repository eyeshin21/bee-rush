using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Anvil
{
    public static partial class ExtensionMethods
    {
#if UNITY_EDITOR
        public static void SetDirty(this GameObject go)
        {
            if (go != null)
            {
                EditorUtility.SetDirty(go);
            }
        }
#endif
    }
}
