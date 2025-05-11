using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets;
using System.IO;

using Debug = UnityEngine.Debug;

namespace AddressableEditor
{
    public static partial class AddressableBuildMenu
    {
        [MenuItem("Addressable/Groups/Groups", false, -19)]
        public static void OpenAddressablesGroups()
        {
            if (!CheckAddressableSetting())
                return;
            EditorApplication.ExecuteMenuItem("Window/Asset Management/Addressables/Groups");
        }

        [MenuItem("Addressable/Groups/Profiles", false, -18)]
        public static void OpenAddressablesProfile()
        {
            if (!CheckAddressableSetting())
                return;
            EditorApplication.ExecuteMenuItem("Window/Asset Management/Addressables/Profiles");
        }

        [MenuItem("Addressable/Build Pipeline/Build", false, 1)]
        public static void BuildAddressables()
        {
            if (!CheckAddressableSetting())
                return;
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            BuildPlayerContent(settings);
        }

        [MenuItem("Addressable/Build Pipeline/Build (Clear Build Cache)", false, 2)]
        public static void BuildAddressablesWithClear()
        {
            if (!CheckAddressableSetting())
                return;

            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;

            ClearCache(settings);
            BuildPlayerContent(settings);
        }

        [MenuItem("Addressable/Build Pipeline/Build (Clear ALL)", false, 3)]
        public static void BuildAddressablesWithClearALL()
        {
            if (!CheckAddressableSetting())
                return;

            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;

            Debug.Log("모든 Addressable 빌드 데이터 삭제...");
            foreach (var builder in settings.DataBuilders)
            {
                if (builder is IDataBuilder dataBuilder)
                    AddressableAssetSettings.CleanPlayerContent(dataBuilder);
            }

            string buildPath = "Library/com.unity.addressables";
            if (Directory.Exists(buildPath))
            {
                Directory.Delete(buildPath, true);
                Debug.Log($"Addressable 빌드 폴더 삭제 완료: {buildPath}");
            }

            ClearCache(settings);
            BuildPlayerContent(settings);
        }

        [MenuItem("Addressable/Development/Remote Build Path Open", false, 101)]
        public static void Debgedevelopment()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            string buildPath = settings.RemoteCatalogBuildPath.GetValue(settings);
            Debug.Log($"RemoteCatalogBuildPath_{buildPath}");
            if (!Directory.Exists(buildPath))
            {
                Directory.CreateDirectory(buildPath);
            }

            OpenFolder(buildPath);
        }

        [MenuItem("Addressable/Development/Create Build Setting", false, 102)]
        public static void CreateDevelopmentBuildSetting()
        {
            CheckBuildSetting();
        }

        [MenuItem("Addressable/Development/BackUp Test", false, 103)]
        public static void BackUpDevelopmentTest()
        {
            CheckBuildSetting();
            BuildBackUp(AddressableAssetSettingsDefaultObject.Settings, isRemoveOrigin: false);
        }


    } 
}