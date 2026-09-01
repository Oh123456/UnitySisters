using System;
using System.Collections.Generic;
using System.Reflection;

using UnityEditor;
using UnityEngine;

using UnityFramework.Utility;

namespace UnityFramework.Editor
{
    [CustomPropertyDrawer(typeof(InterfaceReference<>), true)]
    internal sealed class InterfaceReferenceDrawer : PropertyDrawer
    {
        private const string ComponentPropertyName = "component";

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty componentProperty = property.FindPropertyRelative(ComponentPropertyName);
            if (componentProperty == null)
            {
                EditorGUI.LabelField(position, label.text, "Missing component field");
                return;
            }

            Type interfaceType = ResolveInterfaceType(fieldInfo, property);
            if (interfaceType == null)
            {
                EditorGUI.LabelField(position, label.text, "Could not resolve interface type");
                return;
            }

            if (!interfaceType.IsInterface)
            {
                EditorGUI.LabelField(position, label.text, $"{interfaceType.Name} is not an interface");
                componentProperty.objectReferenceValue = null;
                return;
            }

            MonoBehaviour currentComponent = componentProperty.objectReferenceValue as MonoBehaviour;
            if (currentComponent != null && !interfaceType.IsInstanceOfType(currentComponent))
            {
                currentComponent = null;
                componentProperty.objectReferenceValue = null;
            }

            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.BeginChangeCheck();

            UnityEngine.Object selectedObject = EditorGUI.ObjectField(
                position,
                label,
                currentComponent,
                typeof(UnityEngine.Object),
                true);

            if (EditorGUI.EndChangeCheck())
            {
                componentProperty.objectReferenceValue = ResolveAssignableComponent(selectedObject, interfaceType);
            }

            EditorGUI.EndProperty();
        }

        private static MonoBehaviour ResolveAssignableComponent(UnityEngine.Object selectedObject, Type interfaceType)
        {
            if (selectedObject == null)
            {
                return null;
            }

            if (selectedObject is MonoBehaviour monoBehaviour && interfaceType.IsInstanceOfType(monoBehaviour))
            {
                return monoBehaviour;
            }

            if (selectedObject is GameObject gameObject)
            {
                return FindAssignableComponent(gameObject, interfaceType);
            }

            if (selectedObject is Component component)
            {
                return FindAssignableComponent(component.gameObject, interfaceType);
            }

            return null;
        }

        private static MonoBehaviour FindAssignableComponent(GameObject gameObject, Type interfaceType)
        {
            if (gameObject == null)
            {
                return null;
            }

            MonoBehaviour[] components = gameObject.GetComponents<MonoBehaviour>();

            for (int i = 0; i < components.Length; i++)
            {
                MonoBehaviour component = components[i];
                if (component != null && interfaceType.IsInstanceOfType(component))
                {
                    return component;
                }
            }

            return null;
        }

        private static Type ResolveInterfaceType(FieldInfo fieldInfo, SerializedProperty property)
        {
            Type fieldType = fieldInfo.FieldType;
            if (TryGetInterfaceReferenceType(fieldType, out Type interfaceType))
            {
                return interfaceType;
            }

            Type elementType = GetCollectionElementType(fieldType);
            if (TryGetInterfaceReferenceType(elementType, out interfaceType))
            {
                return interfaceType;
            }

            Type managedReferenceType = GetManagedReferenceType(property);
            if (TryGetInterfaceReferenceType(managedReferenceType, out interfaceType))
            {
                return interfaceType;
            }

            return null;
        }

        private static bool TryGetInterfaceReferenceType(Type type, out Type interfaceType)
        {
            while (type != null)
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(InterfaceReference<>))
                {
                    interfaceType = type.GetGenericArguments()[0];
                    return true;
                }

                type = type.BaseType;
            }

            interfaceType = null;
            return false;
        }

        private static Type GetCollectionElementType(Type type)
        {
            if (type == null || type == typeof(string))
            {
                return null;
            }

            if (type.IsArray)
            {
                return type.GetElementType();
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                return type.GetGenericArguments()[0];
            }

            return null;
        }

        private static Type GetManagedReferenceType(SerializedProperty property)
        {
            string typeName = property.managedReferenceFullTypename;
            if (string.IsNullOrEmpty(typeName))
            {
                return null;
            }

            int splitIndex = typeName.IndexOf(' ');
            if (splitIndex < 0)
            {
                return null;
            }

            string assemblyName = typeName.Substring(0, splitIndex);
            string className = typeName.Substring(splitIndex + 1);
            return Type.GetType($"{className}, {assemblyName}");
        }
    }
}
