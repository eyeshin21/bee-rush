using UnityEngine;
using System.Reflection;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Anvil
{
    public class CheckValueChanged : PropertyAttribute
    {
        private string _methodName;
        private MethodInfo _methodInfo;

        public CheckValueChanged(string methodName)
        {
            _methodName = methodName;
        }

#if UNITY_EDITOR
        public void OnValueChanged(SerializedProperty property)
        {
            // Lazy init
            if (_methodInfo == null)
            {
                var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
                var targetObject = property.serializedObject.targetObject;
                _methodInfo = targetObject.GetType().GetMethod(_methodName, bindingFlags);
            }

            if (_methodInfo != null)
            {
                _methodInfo.Invoke(property.serializedObject.targetObject, null);
            }
        }
#endif
    }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(CheckValueChanged))]
    public class CheckValueChangedDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var checkValueChanged = attribute as CheckValueChanged;
            var propertyType = property.propertyType;
            if (propertyType == SerializedPropertyType.Enum)
            {
                var value = property.enumValueIndex;
                EditorGUI.PropertyField(position, property, label);
                if (value != property.enumValueIndex)
                {
                    property.serializedObject.ApplyModifiedProperties();
                    checkValueChanged.OnValueChanged(property);
                }
            }
            else if (propertyType == SerializedPropertyType.Boolean)
            {
                var value = property.boolValue;
                EditorGUI.PropertyField(position, property, label);
                if (value != property.boolValue)
                {
                    property.serializedObject.ApplyModifiedProperties();
                    checkValueChanged.OnValueChanged(property);
                }
            }
            else if (propertyType == SerializedPropertyType.ObjectReference)
            {
                var value = property.objectReferenceValue;
                EditorGUI.PropertyField(position, property, label);
                if (value != property.objectReferenceValue)
                {
                    property.serializedObject.ApplyModifiedProperties();
                    checkValueChanged.OnValueChanged(property);
                }
            }
            else
            {
                EditorGUI.BeginChangeCheck();
                EditorGUI.PropertyField(position, property, label);
                if (EditorGUI.EndChangeCheck())
                {
                    property.serializedObject.ApplyModifiedProperties();
                    checkValueChanged.OnValueChanged(property);
                }
            }
        }
    }
#endif
}