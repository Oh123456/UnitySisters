namespace CoreSystem.Editor
{
    using CoreSystem.PureComponents;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using UnityFramework.PoolObject;

    [CustomEditor(typeof(CustomMonoBehaviour), true)]
    public class CustomMonoBehaviourEditor : Editor
    {
        private CustomMonoBehaviour customMonoBehaviour;
        private bool currentTargetisVaildScene = false;
        private GameObject activeGameObject = null;

        private void OnEnable()
        {
            PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null)
            {
                customMonoBehaviour = target as CustomMonoBehaviour;
                if (prefabStage.prefabContentsRoot != customMonoBehaviour.gameObject)
                    return;
                customMonoBehaviour.InitializeEditorPureComponent(false);
            }
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            activeGameObject = Selection.activeGameObject;
            if (activeGameObject == null)
                return;
            currentTargetisVaildScene = activeGameObject.scene.IsValid();
            GUILayout.Space(10.0f);
            PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage == null)
            {
                OpenPrefab();
                return;
            }

            if (!currentTargetisVaildScene)
            {
                OpenPrefab();
                return;
            }

            if (customMonoBehaviour == null)
                return;
           
            if (customMonoBehaviour.GetAllPureComponent(out ArrayPoolObject<PureComponent> array))
            {
                using (array)
                {
                    int length = array.Length;
                    for (int i = 0; i < length; i++)
                    {
                        var type = array[i].GetType();
                        GUILayout.Label(type.Name);
                    }
                }
            }

            GUILayout.Space(10.0f);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Save PureComponent"))
            {
                SaveData();
            }

            if (GUILayout.Button("Refresh PureComponent"))
            {
                SaveData();
                customMonoBehaviour.InitializeEditorPureComponent(true);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void OpenPrefab()
        {
            GameObject prefabAsset = null;
            if (currentTargetisVaildScene)
            {
                prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(activeGameObject);
                if (prefabAsset == null)
                {
                    EditorGUILayout.BeginVertical("box");
                    EditorGUILayout.Space(5.0f);

                    EditorGUILayout.LabelField("You can only edit PureComponent if it's a prefab. Please create a prefab to continue.");

                    EditorGUILayout.Space(5.0f);
                    EditorGUILayout.EndVertical(); 
                    return;
                }
            }

            if (GUILayout.Button("Open Prefab"))
            {
                string path = string.Empty;
                if (currentTargetisVaildScene)
                    path = AssetDatabase.GetAssetPath(prefabAsset);
                else
                    path = AssetDatabase.GetAssetPath(target);
                

                PrefabAssetType prefabAssetType = PrefabUtility.GetPrefabAssetType(target);
                if (prefabAssetType == PrefabAssetType.Regular ||
                    prefabAssetType == PrefabAssetType.Variant)
                {
                    PrefabStageUtility.OpenPrefab(path);
                }
            }
        }

        private void SaveData()
        {
            
        }
    } 
}
