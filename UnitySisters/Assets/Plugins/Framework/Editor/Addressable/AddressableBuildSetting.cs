using System;
using System.Collections.Generic;
using System.IO;

using AddressableEditor.Rule;

using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;

using UnityEngine;

using UnityFramework.Addressable;

namespace AddressableEditor
{
    public class AddressableBuildSetting : ScriptableObject
    {
        public const string DEFAULT_BACKUP_FODIER_NAMERULE = "[productName]/[yyyy_MM_dd_HH_mm_ss]";
        [System.Serializable]
        public struct PatternData
        {
            public string key;
            public object value;
        }

        [System.Flags]
        public enum BuildClearOptionFlags
        {
            None = 0,
            FileClear = 1,
            ShowWarning = 1 << 1,
            BackUp = 1 << 2,
        }

        public enum BuildClearOption
        {
            None = BuildClearOptionFlags.None,
            FileClear = BuildClearOptionFlags.FileClear,
            FileClearAndWarning = BuildClearOptionFlags.FileClear | BuildClearOptionFlags.ShowWarning,
            BackUp = BuildClearOptionFlags.BackUp,
            BackUpAndClear = BuildClearOptionFlags.BackUp | BuildClearOptionFlags.FileClear,
        }

        public enum BackUpFoliderNameRulesTpye
        {
            Default = 0,
            Custom,
        }

        [SerializeField][Tooltip("")] Color settingTitleColor = Color.black;
        [SerializeField][Tooltip("")] bool buildCompleteOpenFolder = true;
        [SerializeField][Tooltip("")] BuildClearOption buildClearFolder = BuildClearOption.FileClearAndWarning;
        [SerializeField][Tooltip("")] string backUpPath = "";
        [SerializeField][Tooltip("")] string backUpFolderNameRule = "[productName]/[yyyy_MM_dd_HH_mm_ss]";
        [SerializeField][Tooltip("")] bool isAlwaysBuildCilerWarningDisplay = true;
        [SerializeField][Tooltip("")] bool assetBundleBuildResults = false;
        [SerializeField][Tooltip("")] List<MonoScript> addressablePatternRules;
        [SerializeField][Tooltip("")] BackUpFoliderNameRulesTpye backUpFoliderNameRulesTpye;
        [SerializeField][Tooltip("")] List<AddressableAssetGroup> ignoreChangeGroup;


        List<AddressablePatternRule> patternRules = new List<AddressablePatternRule>();

        BackUpFoliderNameRulesTpye previousbackUpFoliderNameRulesTpye;

        AddressableBuildLabels lastAddressableBuildLabels;

        public bool AssetBundleBuildResults => this.assetBundleBuildResults;
        public bool BuildCompleteOpenFolder => this.buildCompleteOpenFolder;
        public BuildClearOption BuildCilerFolder => this.buildClearFolder;
        public bool IsAlwaysBuildCilerWarningDisplay => this.isAlwaysBuildCilerWarningDisplay;

        public string BackUpPath => this.backUpPath;
        public string BackUpFodierNameRule
        {
            get
            {
                if (backUpFoliderNameRulesTpye == BackUpFoliderNameRulesTpye.Default)
                    return DEFAULT_BACKUP_FODIER_NAMERULE;
                return this.backUpFolderNameRule;
            }
        }



        private void Reset()
        {
            InitializeScripts(new string[] { "AddressablePatternDayRule.cs", "AddressablePatternProductNameRule.cs" });
            this.previousbackUpFoliderNameRulesTpye = BackUpFoliderNameRulesTpye.Default;


            ignoreChangeGroup.Clear();
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;

            var groups = settings.groups;


            foreach (var group in groups)
            {
                var schema = group.GetSchema<BundledAssetGroupSchema>();
                if (schema == null) continue;

                string buildPathValue = schema.BuildPath.GetValue(settings);
                if (buildPathValue == "Default Local Group")
                    ignoreChangeGroup.Add(group);
            }

            var label = CheckLabelsData();
            Resources.UnloadAsset(label);
        }

        private void OnValidate()
        {
            if (this.backUpFoliderNameRulesTpye == BackUpFoliderNameRulesTpye.Default &&
                this.previousbackUpFoliderNameRulesTpye != this.backUpFoliderNameRulesTpye)
            {
                Debug.Log("Rest BackUpFolderNameRules");
                this.backUpFolderNameRule = DEFAULT_BACKUP_FODIER_NAMERULE;
                InitializeScripts(new string[] { "AddressablePatternDayRule.cs", "AddressablePatternProductNameRule.cs" });
            }
            this.previousbackUpFoliderNameRulesTpye = this.backUpFoliderNameRulesTpye;
        }

        private void InitializeScripts(string[] scriptNames)
        {
            string[] allScripts = AssetDatabase.FindAssets("t:MonoScript");
            foreach (var scripts in allScripts)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(scripts);
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(assetPath);

                if (script == null)
                    continue;
                // 특정 파일명과 일치하는 경우만 추가
                for (int j = 0; j < scriptNames.Length; j++)
                {
                    string name = $"{script.name}.cs";
                    if (name == scriptNames[j])
                    {
                        if (!addressablePatternRules.Contains(script))
                            addressablePatternRules.Add(script);
                    }
                }
            }
        }

        public List<AddressablePatternRule> CheckIntegrityAddressablePatternRules()
        {
            patternRules.Clear();

            for (int i = addressablePatternRules.Count - 1; i >= 0; i--)
            {

                Type type = addressablePatternRules[i].GetClass();
                if (!(typeof(AddressablePatternRule).IsAssignableFrom(type)))
                {
                    addressablePatternRules.RemoveAt(i);
                }
                else
                {
                    object asdf = Activator.CreateInstance(type);
                    if (asdf is AddressablePatternRule rule)
                        patternRules.Add(rule);
                }
            }

            return patternRules;
        }

        public AddressableBuildLabels CheckLabelsData()
        {
            if (lastAddressableBuildLabels == null)
                Resources.UnloadAsset(lastAddressableBuildLabels);
            if (!AssetDatabase.IsValidFolder(AddressableManager.BUILD_LABELS_PATH))
            {
                Debug.Log($"'{AddressableManager.BUILD_LABELS_PATH}' 폴더가 없어 생성합니다.");
                Directory.CreateDirectory(AddressableManager.BUILD_LABELS_PATH);
                AssetDatabase.Refresh();
            }

            AddressableBuildLabels labels = Resources.Load<AddressableBuildLabels>(AddressableBuildLabels.NAME);
            if (labels == null)
            {
                string fullName = $"{AddressableManager.BUILD_LABELS_PATH}/{AddressableBuildLabels.NAME}.asset";

                labels = ScriptableObject.CreateInstance<AddressableBuildLabels>();
                AssetDatabase.CreateAsset(labels, fullName);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log($"Create Labels {AddressableManager.BUILD_LABELS_PATH}");
            }
            lastAddressableBuildLabels = labels;
            return labels;
        }

        public void RefreshLabels()
        {
            AddressableBuildLabels addressableBuildLabels = CheckLabelsData();


            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;

            List<string> labels = settings.GetLabels();
            HashSet<string> usedLabels = new HashSet<string>();

            foreach (var group in settings.groups)
            {
                foreach (var entry in group.entries)
                {
                    usedLabels.UnionWith(entry.labels);
                }
            }

            addressableBuildLabels.UpdateLabels(usedLabels);

            Undo.RegisterCompleteObjectUndo(addressableBuildLabels, $"Label Updates");
            EditorUtility.SetDirty(addressableBuildLabels);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

        }
    }

}