namespace CoreSystem.Editor
{
    using CoreSystem.PureComponents;
    using System.Reflection;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using UnityFramework.PoolObject;

    [CustomEditor(typeof(CustomMonoBehaviour), true)]
    public partial class CustomMonoBehaviourEditor : Editor
    {
        private enum Result
        {
            Continue,
            Return,
            Success
        }

        private CustomMonoBehaviour customMonoBehaviour;
        private bool currentTargetisVaildScene = false;
        private GameObject activeGameObject = null;
        private string guid;

        private void OnEnable()
        {
            PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null)
            {
                customMonoBehaviour = target as CustomMonoBehaviour;
                if (prefabStage.prefabContentsRoot != customMonoBehaviour.gameObject)
                    return;
                customMonoBehaviour.InitializeEditorPureComponent(false);

                guid = AssetDatabase.AssetPathToGUID(prefabStage.assetPath);
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
                        EditorGUILayout.Space(10.0f);
                        DrawInspector(array[i], true);
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

        private void DrawInspector(object component, bool isHeader)
        {
            System.Type type = component.GetType();
            string typeName = type.Name;

            string foldoutKey = $"{guid}_{typeName}";

            bool current = SessionState.GetBool(foldoutKey, true);

            bool foldout = BeginFoldoutGroup(isHeader, current, typeName);


            if (current != foldout)
                SessionState.SetBool(foldoutKey, foldout);

            // 폴딩이 닫히면 랜더 X 
            if (current)
            {

                FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (fields == null)
                    return;
                int length = fields.Length;
                if (length == 0)
                    return;

                for (int i = 0; i < length; i++)
                {
                    FieldInfo field = fields[i];
                    if (DrawField(field, component) == Result.Return)
                        return;


                }

            }
            EndFoldoutGroup(isHeader);
        }

        private bool BeginFoldoutGroup(bool isHeader ,bool foldout, string name)
        {
            bool returnValue = false;
            if (isHeader)
            {
                returnValue = EditorGUILayout.BeginFoldoutHeaderGroup(foldout, name);
            }
            else
            {
                returnValue = EditorGUILayout.Foldout(foldout, name);
                EditorGUI.indentLevel++;
            }
            return returnValue;
        }

        private void EndFoldoutGroup(bool isHeader)
        {
            if (isHeader)
                EditorGUILayout.EndFoldoutHeaderGroup();
            else
                EditorGUI.indentLevel--;
        }

        private Result DrawField(FieldInfo field , object component)
        {
            System.Attribute attr = field.GetCustomAttribute<PureComponentFieldAttribute>();
            if (attr == null)
                return Result.Continue;

            object value = field.GetValue(component);
            if (value == null)
            {
                EditorGUILayout.LabelField(field.Name);
                return Result.Continue;
            }

            attr = field.FieldType.GetCustomAttribute<PureComponentDataAttribute>();

            if (attr != null)
            {
                //TODO:: 헤데에게는 다른 표시할것
                DrawInspector(value, false);
                return Result.Continue;
            }

            System.Type valueType = value.GetType();

            if (darwFields.TryGetValue(valueType, out var action))
            {
                action(field.Name, value);
            }
            else
            {
                EditorGUILayout.LabelField(field.Name);
            }

            return Result.Success;
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
