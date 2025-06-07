namespace CoreSystem.Editor
{
    using CoreSystem.PureComponents;
    using UnityEditor;
    using UnityEngine;

    public class PureComponentEditor : EditorWindow
    {

        [MenuItem("Tool/PureComponentEditor")]
        public static void ShowWindow()
        {
            PureComponentEditor pureComponentEditor = GetWindow<PureComponentEditor>();
            pureComponentEditor.Show();
        }

        private void OnGUI()
        {
            GameObject go = Selection.activeGameObject as GameObject;

            
            if (go != null && go.TryGetComponent<CustomMonoBehaviour>(out var component))
            {
                System.Type componentType = component.GetType();
                EditorGUILayout.ObjectField("SelectionGameObject", component, componentType, false);
            }

        }
    }

}