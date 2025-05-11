using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;

using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;

using UnityEngine;

using UnityFramework.Addressable;

namespace AddressableEditor
{

    public class EditorSerializedProperty
    {
        public EditorSerializedProperty(string propertyName)
        {
            this.propertyName = propertyName;
        }

        readonly string propertyName;
        SerializedProperty property;

        public SerializedProperty Property => this.property;
        public string RealPropertyName => this.propertyName;
        public string PropertyName
        {
            get
            {
                string temp = char.ToUpper(this.propertyName[0]) + this.propertyName.Substring(1);
                return temp;
            }
        }

        public void Initialization(SerializedObject serializedObject)
        {
            this.property = serializedObject.FindProperty(propertyName);
        }
    }

    [CustomEditor(typeof(AddressableBuildSetting))]
    public class AddressableBuildSettingEditor : Editor
    {
        enum BuildTpye
        {
            Basic,
            ClearCache,
            ClearAll,
        }

        EditorSerializedProperty buildCompleteOpenFolder = new EditorSerializedProperty("buildCompleteOpenFolder");
        EditorSerializedProperty buildClearFolder = new EditorSerializedProperty("buildClearFolder");
        EditorSerializedProperty assetBundleBuildResults = new EditorSerializedProperty("assetBundleBuildResults");
        EditorSerializedProperty isAlwaysBuildCilerWarningDisplay = new EditorSerializedProperty("isAlwaysBuildCilerWarningDisplay");
        EditorSerializedProperty settingTitleColor = new EditorSerializedProperty("settingTitleColor");
        EditorSerializedProperty backUpPath = new EditorSerializedProperty("backUpPath");
        EditorSerializedProperty backUpFodierNameRule = new EditorSerializedProperty("backUpFodierNameRule");
        EditorSerializedProperty addressablePatternRules = new EditorSerializedProperty("addressablePatternRules");
        EditorSerializedProperty backUpFoliderNameRulesTpye = new EditorSerializedProperty("backUpFoliderNameRulesTpye");
        EditorSerializedProperty ignoreChangeGroup = new EditorSerializedProperty("ignoreChangeGroup");

        GUIStyle infomationStyle;
        GUIStyle profilePopupStyle;
        GUIStyle profileFontStyle;
        GUIStyle titleFontStyle;
        GUIStyle remoteFontStyle;
        GUIStyle remoteFoldoutFontStyle;

        Queue<AddressableAssetGroup> remoteQueue = new Queue<AddressableAssetGroup>();
        Queue<AddressableAssetGroup> localQueue = new Queue<AddressableAssetGroup>();
        Rect lastRect;
        string[] playModes = new string[3];

        static Color titleColor = Color.black;
        static BuildTpye buildTpye = BuildTpye.Basic;
        static HashSet<string> removetString = new HashSet<string>();
        static int profileIndex = 0;
        static int playModeIndex = 0;
        static bool pathRemoteToggle = true;
        static Vector2 scrollPos;

        private GUIStyle ProfilePopupStyle => profilePopupStyle == null ? EditorStyles.label : profilePopupStyle;
        private GUIStyle RemoteFoldoutFontStyle => remoteFoldoutFontStyle == null ? EditorStyles.label : remoteFoldoutFontStyle;
        private GUIStyle TitleFontStyle => titleFontStyle == null ? EditorStyles.label : titleFontStyle;
        private GUIStyle RemoteFontStyle => remoteFontStyle == null ? EditorStyles.label : remoteFontStyle;

        private void OnEnable()
        {
            buildCompleteOpenFolder.Initialization(serializedObject);
            buildClearFolder.Initialization(serializedObject);
            assetBundleBuildResults.Initialization(serializedObject);
            isAlwaysBuildCilerWarningDisplay.Initialization(serializedObject);
            settingTitleColor.Initialization(serializedObject);
            backUpPath.Initialization(serializedObject);
            backUpFodierNameRule.Initialization(serializedObject);
            addressablePatternRules.Initialization(serializedObject);
            backUpFoliderNameRulesTpye.Initialization(serializedObject);


            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            settings.RemoteCatalogBuildPath.OnValueChanged -= ValueChangeAddressableAssetSettings;
            removetString.Clear();
            removetString.Add(settings.RemoteCatalogBuildPath.GetValue(settings));

            ignoreChangeGroup.Initialization(serializedObject);


            EditorApplication.delayCall += DelayCall;

        }

        private void OnDisable()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            settings.RemoteCatalogBuildPath.OnValueChanged -= ValueChangeAddressableAssetSettings;
            EditorApplication.delayCall -= DelayCall;
        }

        private void DelayCall()
        {
            CreateTitleFontStyle();
            CreateProfilePopup();
        }

        private void ValueChangeAddressableAssetSettings(ProfileValueReference profileValueReference)
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            removetString.Clear();
            removetString.Add(settings.RemoteCatalogBuildPath.GetValue(settings));
        }

        private void CreateTitleFontStyle()
        {
            titleFontStyle = new GUIStyle()
            {
                fontSize = 20,
                fontStyle = FontStyle.BoldAndItalic,
                normal = new GUIStyleState() { textColor = settingTitleColor.Property.colorValue }
            };


            profileFontStyle = new GUIStyle()
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = new GUIStyleState() { textColor = new Color(0.6078f, 0.3490f, 0.7137f) }
            };

            remoteFontStyle = new GUIStyle()
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = new GUIStyleState() { textColor = Color.white }
            };

            remoteFoldoutFontStyle = new GUIStyle(EditorStyles.foldout)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = new GUIStyleState() { textColor = Color.white }
            };

            infomationStyle = new GUIStyle(EditorStyles.foldout)
            {
                fontSize = 30,
                fontStyle = FontStyle.BoldAndItalic,
                normal = new GUIStyleState() { textColor = settingTitleColor.Property.colorValue }
            };
        }

        private void CreateProfilePopup()
        {

            this.profilePopupStyle = new GUIStyle(EditorStyles.popup)
            {
                alignment = TextAnchor.MiddleCenter,
            };


        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawInspectorGUI();
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawInspectorGUI()
        {
            EditorGUILayout.Space(10);
            GUI.enabled = false;
            EditorGUILayout.ObjectField("Script", MonoScript.FromScriptableObject((AddressableBuildSetting)target), typeof(MonoScript), false);
            GUI.enabled = true;

            EditorGUILayout.Space(10);

            Color color = EditorGUILayout.ColorField("TitleColor", settingTitleColor.Property.colorValue, GUILayout.Width(400.0f), GUILayout.ExpandWidth(true));
            if (color != titleColor)
            {
                settingTitleColor.Property.colorValue = color;
                //CreateTitleFontStyle();
            }

            #region Options
            DrawTitle("Build Options");

            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            AddressableAssetProfileSettings profileSettings = settings.profileSettings;
            List<string> profileNames = profileSettings.GetAllProfileNames();
            string currentPofileId = profileSettings.GetProfileName(settings.activeProfileId);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.Space(15.0f);
            EditorGUILayout.LabelField("Current Profile", profileFontStyle == null ? EditorStyles.label : profileFontStyle);
            EditorGUILayout.Space(2.0f);

            if (GUILayout.Button("...", GUILayout.Width(25)))
            {
                AddressableBuildMenu.OpenAddressablesProfile();
            }
            EditorGUILayout.EndHorizontal();

            Rect popupRect = GUILayoutUtility.GetLastRect();
            const float popupHeight = 10.0f;
            popupRect.y += popupHeight;

            profileIndex = profileNames.IndexOf(currentPofileId);
            int newIndex = EditorGUILayout.Popup(profileIndex, profileNames.ToArray(), this.ProfilePopupStyle);
            if (profileIndex != newIndex)
            {
                profileIndex = newIndex;
                settings.activeProfileId = profileSettings.GetProfileId(profileNames[profileIndex]);
            }
            EditorGUILayout.Space(2.0f);

            EditorGUI.indentLevel++;
            pathRemoteToggle = EditorGUILayout.Foldout(pathRemoteToggle, new GUIContent("Remote"), RemoteFoldoutFontStyle);
            if (pathRemoteToggle)
            {
                GUI.enabled = false;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("BuildPath", GUILayout.Width(90.0f));
                EditorGUILayout.TextField(settings.RemoteCatalogBuildPath.GetValue(settings), GUILayout.ExpandWidth(true));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("LoadPath", GUILayout.Width(90.0f));
                EditorGUILayout.TextField(settings.RemoteCatalogLoadPath.GetValue(settings), GUILayout.ExpandWidth(true));
                EditorGUILayout.EndHorizontal();
                GUI.enabled = true;
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.Space(7.0f + popupHeight);

            PropertyField(buildCompleteOpenFolder, "빌드 완료후 빌드 폴더 열기");
            PropertyField(buildClearFolder, "빌드시 빌드 폴더 삭제");

            var option = (AddressableBuildSetting.BuildClearOptionFlags)(buildClearFolder.Property.intValue);
            if (option.HasFlag(AddressableBuildSetting.BuildClearOptionFlags.BackUp))
            {
                EditorGUI.indentLevel++;
                PropertyField(backUpPath, "백업 위치");
                PropertyField(backUpFoliderNameRulesTpye, "백업 폴더 이름 타입");
                EditorGUI.indentLevel++;
                EditorGUILayout.Space(3.0f);
                bool isDefulat = backUpFoliderNameRulesTpye.Property.intValue == (int)AddressableBuildSetting.BackUpFoliderNameRulesTpye.Default;
                GUI.enabled = !isDefulat;
                PropertyField(backUpFodierNameRule, "백업 폴더 이름 규칙");
                PropertyField(addressablePatternRules, "백업 폴더 이름 규칙 패턴");
                GUI.enabled = true;
                EditorGUI.indentLevel--;
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button($"Build ({buildTpye.ToString()})"))
            {
                switch (buildTpye)
                {
                    default:
                    case BuildTpye.Basic:
                        AddressableBuildMenu.BuildAddressables();
                        break;
                    case BuildTpye.ClearCache:
                        AddressableBuildMenu.BuildAddressablesWithClear();
                        break;
                    case BuildTpye.ClearAll:
                        AddressableBuildMenu.BuildAddressablesWithClearALL();
                        break;
                }
            }

            buildTpye = (BuildTpye)EditorGUILayout.EnumPopup(buildTpye, GUILayout.Width(100.0f));

            EditorGUILayout.EndHorizontal();
            #endregion

            #region Debugs
            DrawTitle("Debugs");
            PropertyField(assetBundleBuildResults, "빌드 완료 로그 활성화");


            var DataBuilders = settings.DataBuilders;
            int dataBuilderCount = DataBuilders.Count;
            //string[] dataBuilderNames = ArrayPool<string>.Shared.Rent(dataBuilderCount);

            for (int i = 0; i < dataBuilderCount; i++)
            {
                if (DataBuilders[i] is IDataBuilder dataBuilder)
                {
                    if ("Default Build Script" == dataBuilder.Name)
                        continue;
                    playModes[i] = dataBuilder.Name;
                }
            }

            EditorGUILayout.Space(2.0f);
            playModeIndex = settings.ActivePlayModeDataBuilderIndex;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("PlayMode", GUILayout.Width(60.0f));
            int newPlayModeIndex = EditorGUILayout.Popup(playModeIndex, playModes, this.ProfilePopupStyle);
            if (newPlayModeIndex != playModeIndex)
            {
                playModeIndex = newPlayModeIndex;
                settings.ActivePlayModeDataBuilderIndex = playModeIndex;
            }

            //ArrayPool<string>.Shared.Return(dataBuilderNames, true);
            EditorGUILayout.EndHorizontal();
            #endregion

            #region Labels
            DrawTitle("Labels");
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh Labels"))
            {
                ((AddressableBuildSetting)target).RefreshLabels();

                Debug.Log("Label Data Updates");
            }

            if (GUILayout.Button("...", GUILayout.Width(25)))
            {

                string fullName = $"{AddressableManager.BUILD_LABELS_PATH}/{AddressableBuildLabels.NAME}.asset";
                AddressableBuildLabels addressableBuildLabels = AssetDatabase.LoadAssetAtPath<AddressableBuildLabels>(fullName);
                if (addressableBuildLabels != null)
                {
                    Selection.activeObject = addressableBuildLabels;
                    EditorGUIUtility.PingObject(addressableBuildLabels);
                }

                Resources.UnloadAsset(addressableBuildLabels);
            }

            EditorGUILayout.EndHorizontal();
            #endregion

            #region Groups
            EditorGUILayout.Space(10.0f);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Groups", style: TitleFontStyle);

            if (GUILayout.Button("...", GUILayout.Width(25)))
            {
                AddressableBuildMenu.OpenAddressablesGroups();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(10.0f);

            if (Event.current.type == EventType.Repaint)
            {
                lastRect = GUILayoutUtility.GetLastRect();
            }

            Rect rect = lastRect;

            rect.x -= 2.0f;
            rect.y += 10.0f;
            rect.width -= 2.0f;

            OrganizeAddressableAssetGroup();

            EditorGUI.indentLevel++;
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Remote", style: RemoteFontStyle);
            EditorGUILayout.Space(3);
            EditorGUI.indentLevel++;

            DrawAddressableAssetGroup(remoteQueue);


            EditorGUI.indentLevel--;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Local", style: RemoteFontStyle);
            EditorGUILayout.Space(3);
            EditorGUI.indentLevel++;

            DrawAddressableAssetGroup(localQueue);

            EditorGUI.indentLevel--;
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(10);

            lastRect.yMax = GUILayoutUtility.GetLastRect().yMax;
            rect.yMax = lastRect.yMax;
            GUI.Box(rect, "");

            EditorGUILayout.Space(3);
            if (GUILayout.Button("Change All Groups to Remote"))
            {
                ChangeAllGroups();
            }

            EditorGUILayout.Space(3);
            EditorGUI.indentLevel++;
            PropertyField(ignoreChangeGroup);
            EditorGUI.indentLevel--;

            EditorGUI.indentLevel--;
            #endregion
        }

        private void DrawAddressableAssetGroup(Queue<AddressableAssetGroup> addressableAssetGroups)
        {
            GUI.enabled = false;
            while (addressableAssetGroups.Count > 0)
            {
                AddressableAssetGroup addressableAssetGroup = addressableAssetGroups.Dequeue();
                EditorGUILayout.ObjectField(addressableAssetGroup, typeof(AddressableAssetGroup), allowSceneObjects: false, GUILayout.Width(lastRect.size.x - 20.0f));
                EditorGUILayout.Space(3.0f);
            }
            GUI.enabled = true;
        }

        private void OrganizeAddressableAssetGroup()
        {
            remoteQueue.Clear();
            localQueue.Clear();

            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;

            var groups = settings.groups;


            foreach (var group in groups)
            {
                var schema = group.GetSchema<BundledAssetGroupSchema>();
                if (schema == null) continue;

                // 현재 선택된 프로파일 변수명 가져오기
                string buildPathValue = schema.BuildPath.GetValue(settings);
                if (removetString.Contains(buildPathValue))
                    remoteQueue.Enqueue(group);
                else
                    localQueue.Enqueue(group);
            }
        }

        private void PropertyField(EditorSerializedProperty editorSerializedProperty)
        {
            EditorGUILayout.PropertyField(editorSerializedProperty.Property);
        }

        private void PropertyField(EditorSerializedProperty editorSerializedProperty, string tooltip)
        {
            if (editorSerializedProperty == null || editorSerializedProperty.Property == null)
                return;
            EditorGUILayout.PropertyField(editorSerializedProperty.Property, new GUIContent(editorSerializedProperty.PropertyName, tooltip));
        }

        private void ChangeAllGroups()
        {
            HashSet<string> ignore = new HashSet<string>();
            int count = ignoreChangeGroup.Property.arraySize;
            for (int i = 0; i < count; i++)
            {
                if (ignoreChangeGroup.Property.GetArrayElementAtIndex(i).objectReferenceValue is AddressableAssetGroup group)
                {
                    ignore.Add(group.Name);
                }
            }


            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;

            var groups = settings.groups;


            foreach (var group in groups)
            {
                if (ignore.Contains(group.Name))
                    continue;


                var schema = group.GetSchema<BundledAssetGroupSchema>();
                if (schema != null)
                {
                    string buildPathValue = schema.BuildPath.GetValue(settings);
                    if (removetString.Contains(buildPathValue))
                        continue;


                    schema.BuildPath.SetVariableByName(settings, "Remote.BuildPath");
                    schema.LoadPath.SetVariableByName(settings, "Remote.LoadPath");
                    Debug.Log($"Group '{group.Name}'을(를) Remote로 변경");

                    Undo.RegisterCompleteObjectUndo(group, $"Groups to Remote");
                    EditorUtility.SetDirty(group);
                }


            }



        }

        private void DrawTitle(string titileName)
        {
            if (titleFontStyle == null)
                return;
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField(titileName, style: titleFontStyle);
            EditorGUILayout.Space(5);
        }
    }


}