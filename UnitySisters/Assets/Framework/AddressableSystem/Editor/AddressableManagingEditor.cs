using System.Collections;
using System.Collections.Generic;

using UnityEditor;

using UnityEngine;

using UnityFramework.Addressable.Editor;

namespace AddressableEditor
{
    public class AddressableManagingEditor : EditorWindow
    {
        class LoadTypeAddressableData
        {
            public Dictionary<string, AddressableData> addressableDatas = new Dictionary<string, AddressableData>();

            public void Clear()
            {
                foreach (var item in addressableDatas)
                    item.Value.Clear();
            }
        }

        class AddressableData
        {
            public bool isFoldout = false;
            public List<AddressableManagingData> addressableManagingDatas = new List<AddressableManagingData>();


            public void Clear()
            {
                addressableManagingDatas.Clear();
            }
        }

        enum MenuType
        {
            Unsafes,
            Safes,
            Options
        }

        enum IndicatorColor
        {
            gray = 0,
            Green = 1,
            Red = 2,
            Yellow = 3,
        }

       
        private bool isAddPlayMode = false;
        private static Dictionary<string, bool> sceneNameFoldout = new Dictionary<string, bool>();
        private static bool isTracking = true;
        private static float splitRatio = 0.15f;
        private static GUIStyle menuStyle;
        private static GUIStyle sceneNameStyle;
        private static GUIStyleState styleState;
        private static GUIStyleState styleStateBalckColor;
        private static GUIStyle foldoutStyle;
        private static Dictionary<AddressableManagingDataManager.LoadType, LoadTypeAddressableData> datas = new Dictionary<AddressableManagingDataManager.LoadType, LoadTypeAddressableData>();
        private Vector2 leftScrollPos;
        private Vector2 rightScrollPos;
        private int selectedIndex = 0;
        private float splitterWidth = 4.0f;
        private bool isResizing = false;
        private readonly string[] menuItems = { "Unsafes", "Safes", "Options" };
        private readonly string[] IndicatorColors =
        {
            $"<color=#{ColorUtility.ToHtmlStringRGB(Color.gray)}>●</color>",
            $"<color=#{ColorUtility.ToHtmlStringRGB(Color.green)}>●</color>",
            $"<color=#{ColorUtility.ToHtmlStringRGB(Color.red)}>●</color>" ,
            $"<color=#{ColorUtility.ToHtmlStringRGB(Color.yellow)}>●</color>" ,
        };

        public static void Clear()
        {
            sceneNameFoldout.Clear();
        }

        [MenuItem("Addressable/Managing")]

        public static void ShowWindow()
        {
            GetWindow<AddressableManagingEditor>().Show();

            splitRatio = EditorPrefs.GetFloat("_splitRatio", 0.15f);
            isTracking = EditorPrefs.GetBool("_isTracking", true);

        }

        private void PlayModeStateChanged(PlayModeStateChange playModeStateChange)
        {
            if (playModeStateChange == PlayModeStateChange.EnteredPlayMode)
            {
                ClaerData();
                datas.Clear();
                sceneNameFoldout.Clear();
            }
        }

        private void OnEnable()
        {
            InitiationStyle();
            UpdateData();
            AddressableManagingDataManager.OnUpdated += UpdateData;
            AddressableManagingDataManager.OnUpdated += Repaint;

            if (!isAddPlayMode)
            {
                UnityEditor.EditorApplication.playModeStateChanged += PlayModeStateChanged;
                isAddPlayMode = true;
            }
        }

        private void OnDisable()
        {
            AddressableManagingDataManager.OnUpdated -= Repaint;
            AddressableManagingDataManager.OnUpdated -= UpdateData;
        }

        private void OnGUI()
        {
            if (!CheckStyles())
                InitiationStyle();

            Rect windowRect = position;
            float splitWidth = windowRect.width * splitRatio;

            GUILayout.BeginHorizontal();

            // 왼쪽 패널 (목차 / 네비게이션)
            GUILayout.BeginVertical(GUILayout.Width(splitWidth));
            leftScrollPos = GUILayout.BeginScrollView(leftScrollPos, GUILayout.ExpandHeight(true));

            //GUILayout.Label("목차", EditorStyles.boldLabel);
            GUILayout.Space(10.0f);
            for (int i = 0; i < menuItems.Length; i++)
            {
                //EditorGUILayout.HelpBox("d", MessageType.Info);
                if (selectedIndex == i)
                    menuStyle.normal = styleStateBalckColor;
                else
                    menuStyle.normal = styleState;
                if (GUILayout.Button(menuItems[i], menuStyle))
                {
                    selectedIndex = i;
                }
                GUILayout.Space(3.0f);
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            DrawLine(splitWidth, windowRect);

            GUILayout.Space(splitterWidth); // 선 너비 추가

            // 오른쪽 패널 (선택한 항목의 상세 정보)
            GUILayout.BeginVertical(GUI.skin.box);
            rightScrollPos = GUILayout.BeginScrollView(rightScrollPos, GUILayout.ExpandHeight(true));

            if (selectedIndex == (int)MenuType.Options)
            {
                DrawOptions();
            }
            else
            {
                if (!Application.isPlaying)
                {
                    EditorGUILayout.HelpBox("This editor only works during play mode.", MessageType.Info);
                }
                if (selectedIndex == (int)MenuType.Unsafes)
                {
                    DrawResourceData(AddressableManagingDataManager.LoadType.UnsafeLoad);
                }
                else if (selectedIndex == (int)MenuType.Safes)
                {
                    DrawResourceData(AddressableManagingDataManager.LoadType.SafeLoad);
                }
            }




            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
        }

        private void DrawLine(float splitWidth, Rect windowRect)
        {
            Rect splitterRect = new Rect(splitWidth, 0, splitterWidth, windowRect.height);
            EditorGUI.DrawRect(splitterRect, new Color(0.4f, 0.4f, 0.4f, 1f)); // 회색 선 표시
            EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeHorizontal);

            if (Event.current.type == EventType.MouseDown && splitterRect.Contains(Event.current.mousePosition))
            {
                isResizing = true;
            }
            if (isResizing && Event.current.type == EventType.MouseDrag)
            {
                splitRatio = Mathf.Clamp(Event.current.mousePosition.x / windowRect.width, 0.1f, 0.9f);
                Repaint();
            }
            if (Event.current.type == EventType.MouseUp)
            {
                isResizing = false;
                EditorPrefs.SetFloat("_splitRatio", splitRatio);
            }
        }

        private void InitiationStyle()
        {
            try
            {
                menuStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
                {
                    fontSize = 15,
                    alignment = TextAnchor.MiddleCenter,
                };

                styleState = new GUIStyleState()
                {
                    textColor = Color.white,
                };

                styleStateBalckColor = new GUIStyleState()
                {
                    textColor = Color.black,
                };

                foldoutStyle = new GUIStyle(EditorStyles.foldout)
                {
                    richText = true,
                };

                sceneNameStyle = new GUIStyle(EditorStyles.foldout)
                {
                    fontSize = 15,
                };
            }
            catch
            {

            }
        }

        private bool CheckStyles()
        {
            return (menuStyle != null && styleState != null && styleStateBalckColor != null && foldoutStyle != null);
        }

        private void DrawOptions()
        {
            bool isbool = isTracking;
            isbool = EditorGUILayout.Toggle("Addressable Traking", isbool);
            if (isTracking != isbool)
            {
                isTracking = isbool;
                AddressableManagingDataManager.IsTracking = isTracking;
                EditorPrefs.SetBool("_isTracking", isTracking);
            }
        }

        private void DrawResourceData(AddressableManagingDataManager.LoadType loadType)
        {
            if (AddressableManagingDataManager.addressableManagingDatas.Count < 1)
                return;

            if (!datas.TryGetValue(loadType, out var data))
                return;

            EditorGUI.indentLevel++;

            foreach (var item in data.addressableDatas)
            {
                AddressableData addressableData = item.Value;
                if (addressableData.addressableManagingDatas.Count == 0)
                    continue;

                bool sceneFoldout = true;
                if (loadType == AddressableManagingDataManager.LoadType.SafeLoad)
                {
                    addressableData.isFoldout = EditorGUILayout.Foldout(addressableData.isFoldout, item.Key, sceneNameStyle);
                    sceneFoldout = addressableData.isFoldout;   
                }
                else
                {
                    EditorGUI.indentLevel--;
                }

                if (sceneFoldout)
                {   
                    EditorGUI.indentLevel++;

                    int count = addressableData.addressableManagingDatas.Count;
                    for (int i = 0; i < count; i++)
                    {
                        var managingData = addressableData.addressableManagingDatas[i];
                        string indicatorColor = IndicatorColors[(int)GetIndicatorColor(managingData)];
                        managingData.foldout = EditorGUILayout.Foldout(managingData.foldout, $"{indicatorColor} Name : {managingData.name}  LoadCount : {managingData.loadCount} ", foldoutStyle);

                        if (managingData.foldout)
                        {
                            EditorGUI.indentLevel++;
                            foreach (var accessKey in managingData.accessKeys)
                            {
                                EditorGUILayout.LabelField($"accessKey : {accessKey}");
                            }

                            foreach (var stackTrace in managingData.stackTraces)
                            {

                                var array = managingData.GetAddressableManagingDataStackTraces(stackTrace.Value);
                                if (array == null)
                                    continue;
                                int length = array.Length;
                                bool current = EditorGUILayout.Foldout(managingData.foldouts[(int)stackTrace.Key], $"LoadType : {stackTrace.Key}");
                                managingData.foldouts[(int)stackTrace.Key] = current;
                                if (current)
                                {
                                    EditorGUI.indentLevel++;
                                    for (int j = 0; j < length; j++)
                                    {
                                        var element = array[j];
                                        if (element == null)
                                            continue;

                                        EditorGUILayout.BeginHorizontal();
                                        GUIContent labelContent = new GUIContent($"Call Stack (Count {element.count}): {element.FunctionName}");
                                        GUIStyle labelStyle = EditorStyles.label;
                                        Vector2 size = labelStyle.CalcSize(labelContent);
                                        EditorGUILayout.LabelField(labelContent, GUILayout.Width(size.x + 60.0f));
                                        if (EditorGUILayout.LinkButton($"(at {element.Path}:{element.LineNumber} )"))
                                        {
                                            OpenScriptInEditor(element.Path, element.LineNumber);
                                        }
                                        EditorGUILayout.EndHorizontal();
                                    }
                                    EditorGUI.indentLevel--;
                                }
                                managingData.ReturnAddressableManagingDataStackTraces(array);
                            }


                            EditorGUI.indentLevel--;
                        }
                    }


                    EditorGUI.indentLevel--;
                }

            }

            EditorGUI.indentLevel--;

        }

        private IndicatorColor GetIndicatorColor(AddressableManagingData addressableManagingData)
        {
            if (addressableManagingData.loadCount < 1)
                return IndicatorColor.gray;

            int keyCount = 0;

            if (addressableManagingData.stackTraces.ContainsKey(AddressableManagingDataManager.LoadType.UnsafeLoad))
                keyCount++;

            if (addressableManagingData.stackTraces.ContainsKey(AddressableManagingDataManager.LoadType.SafeLoad))
                keyCount++;

            if (keyCount != (int)IndicatorColor.Red)
            {
                if (addressableManagingData.AccessKeysCount() == 2)
                    keyCount = (int)IndicatorColor.Yellow;
            }

            return (IndicatorColor)keyCount;
        }

        private void UpdateData()
        {
            ClaerData();
            foreach (var keyValuePair in AddressableManagingDataManager.addressableManagingDatas)
            {
                var data = keyValuePair.Value;

                foreach (var loadtpye in data.stackTraces)
                {
                    var type = loadtpye.Key;
                    if (!datas.TryGetValue(type, out var loadTypeAddressableData))
                    {
                        loadTypeAddressableData = new LoadTypeAddressableData();
                        datas.Add(type, loadTypeAddressableData);
                    }

                    string sceneName = data.seceneName;
                    if (!loadTypeAddressableData.addressableDatas.TryGetValue(sceneName, out var addressableData))
                    {
                        addressableData = new AddressableData();
                        loadTypeAddressableData.addressableDatas.Add(sceneName, addressableData);
                    }

                    addressableData.addressableManagingDatas.Add(data);
                }
            }
        }

        private void ClaerData()
        {
            foreach (var item in datas)
            {
                item.Value.Clear();
            }

            //datas.Clear();
        }

        private void OpenScriptInEditor(string path, int line)
        {
            Object obj = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (obj != null)
            {
                AssetDatabase.OpenAsset(obj, line); // 해당 줄에서 열기!
            }
            else
            {
                Debug.LogWarning($"파일을 찾을 수 없음: {path}");
            }
        }
    }

}