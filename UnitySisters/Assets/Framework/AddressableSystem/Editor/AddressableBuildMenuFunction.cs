using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;

using UnityEngine;

using Debug = UnityEngine.Debug;

namespace AddressableEditor
{
    public static partial class AddressableBuildMenu
    {
        static AddressableBuildSetting currentAddressableBuildSetting;

        // 캐시 삭제
        private static void ClearCache(AddressableAssetSettings settings)
        {
            Debug.Log("기존 Addressable 빌드 데이터 삭제...");
            Caching.ClearCache();
            AddressableAssetSettings.CleanPlayerContent(settings.ActivePlayerDataBuilder);
        }

        // 빌드 시작
        private static void BuildPlayerContent(AddressableAssetSettings settings)
        {

            AddressableBuildSetting.BuildClearOption option = currentAddressableBuildSetting.BuildCilerFolder;
            AddressableBuildSetting.BuildClearOptionFlags flags = (AddressableBuildSetting.BuildClearOptionFlags)option;

            if (flags.HasFlag(AddressableBuildSetting.BuildClearOptionFlags.BackUp))
                BuildBackUp(settings);

            if (flags.HasFlag(AddressableBuildSetting.BuildClearOptionFlags.FileClear))
            {

                if (flags.HasFlag(AddressableBuildSetting.BuildClearOptionFlags.ShowWarning))
                {
                    if (!ShowWarningEditorPopUp(BUILD_WARNING))
                        return;
                }

                BuildClearFolder(settings);
            }



            Debug.Log("Addressable 빌드 시작...");
            AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);

            if (result == null)
            {
                Debug.LogError("Addressable Error");
                return;
            }

            if (currentAddressableBuildSetting.AssetBundleBuildResults)
                ShowAssetBundleBuildResults(result);

            if (currentAddressableBuildSetting.BuildCompleteOpenFolder)
                OpenBuilFolder(settings);
            Debug.Log("Addressable 빌드 완료!");
        }

        // 어드레서블 확인
        private static bool CheckAddressableSetting()
        {
            bool isSetting = AddressableAssetSettingsDefaultObject.SettingsExists;
            if (!isSetting)
                Debug.LogError("Addressable 설정이 존재하지 않습니다. 먼저 Addressable을 설정하세요!");
            CheckBuildSetting();
            return isSetting;
        }

        private static void CheckBuildSetting()
        {
            if (!AssetDatabase.IsValidFolder(BUILD_SETTING_PATH))
            {
                Debug.Log($"'{BUILD_SETTING_PATH}' 폴더가 없어 생성합니다.");
                Directory.CreateDirectory(BUILD_SETTING_PATH);
                AssetDatabase.Refresh();
            }

            string fullName = $"{BUILD_SETTING_PATH}/{BUILD_SETTING_NAME}";

            if (currentAddressableBuildSetting == null)
                currentAddressableBuildSetting = AssetDatabase.LoadAssetAtPath<AddressableBuildSetting>(fullName);
            // 빌드 세팅 만들고 저장하기
            if (currentAddressableBuildSetting == null)
            {
                currentAddressableBuildSetting = ScriptableObject.CreateInstance<AddressableBuildSetting>();

                AssetDatabase.CreateAsset(currentAddressableBuildSetting, fullName);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log($"Create BuildSetting {BUILD_SETTING_PATH}");
            }

        }

        private static void OpenFolder(string path)
        {
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                Process.Start("explorer.exe", path.Replace('/', '\\'));
            }
            else if (Application.platform == RuntimePlatform.OSXEditor)
            {
                Process.Start("open", path);
            }
            else if (Application.platform == RuntimePlatform.LinuxEditor)
            {
                Process.Start("xdg-open", path);
            }
            else
            {
                Debug.LogError(" 현재 플랫폼에서 폴더 열기를 지원하지 않습니다.");
            }
        }

        private static void ShowAssetBundleBuildResults(AddressablesPlayerBuildResult result)
        {

            var assetBundleBuildResults = result.AssetBundleBuildResults;

            for (int i = 0; i < assetBundleBuildResults.Count; i++)
            {
                AddressablesPlayerBuildResult.BundleBuildResult bundleBuildResult = assetBundleBuildResults[i];
                Debug.Log($"FilePath : {bundleBuildResult.FilePath} \n Hash {bundleBuildResult.Hash} \n Group : {bundleBuildResult.SourceAssetGroup.Name} \n InternalBundleName : {bundleBuildResult.InternalBundleName} \n Crc : {bundleBuildResult.Crc} \n");
            }

        }

        private static void OpenBuilFolder(AddressableAssetSettings settings)
        {
            string buildPath = settings.RemoteCatalogBuildPath.GetValue(settings);
            OpenFolder(buildPath);
        }

        private static void BuildClearFolder(AddressableAssetSettings settings)
        {
            Debug.Log("폴더 정리중.....");
            try
            {
                string buildPath = settings.RemoteCatalogBuildPath.GetValue(settings);

                string[] files = Directory.GetFiles(buildPath);

                foreach (string file in files)
                {
                    File.Delete(file);
                    Debug.LogWarning($"파일 삭제 {file}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError(e.Message);
            }
        }

        private static void BuildBackUp(AddressableAssetSettings settings, bool isRemoveOrigin = true)
        {
            string backUpPath = currentAddressableBuildSetting.BackUpPath;
            if (string.IsNullOrEmpty(backUpPath))
            {
                if (!ShowWarningEditorPopUp(BUILD_BACKUP_PATH_WARNING))
                    return;

                backUpPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
            }


            string backUpFodierNameRule = currentAddressableBuildSetting.BackUpFodierNameRule;
            if (string.IsNullOrEmpty(backUpFodierNameRule))
            {
                if (!ShowWarningEditorPopUp(BUILD_BACKUP_RULE_WARNING))
                    return;

                backUpFodierNameRule = "[productName][yyyy_MM_dd_HH_mm_ss]";
            }

            var rules = currentAddressableBuildSetting.CheckIntegrityAddressablePatternRules();


            string pattern = @"\[(.*?)\]";
            MatchCollection matches = Regex.Matches(backUpFodierNameRule, pattern);

            foreach (Match match in matches)
            {
                // 임시
                // 다른 룰을 적용시킬 방법은 추후에..


                for (int i = 0; i < rules.Count; i++)
                {
                    var rule = rules[i];
                    if (rule.EqualsKey(match.Groups[1].Value))
                    {
                        backUpPath = Path.Combine(backUpPath, rule.GetValue());
                        //backUpPath = $"{backUpPath}{rule.GetValue()}";
                    }
                }
            }

            backUpPath = Path.Combine(backUpPath, EditorUserBuildSettings.activeBuildTarget.ToString());

            string buildPath = settings.RemoteCatalogBuildPath.GetValue(settings);

            if (!Directory.Exists(buildPath))
            {
                Debug.LogWarning($"{buildPath} 에 값이 없습니다.");
            }
            else
            {
                if (!Directory.Exists(backUpPath))
                {
                    Directory.CreateDirectory(backUpPath);
                    Debug.Log($"대상 폴더 생성: {backUpPath}");
                }

                // 빌드 오브젝트
                string[] files = Directory.GetFiles(buildPath);

                foreach (var file in files)
                {
                    string newFileName = Path.Combine(backUpPath, Path.GetFileName(file));

                    File.Copy(file, newFileName);  // 파일을 새로운 이름으로 복사
                    if (isRemoveOrigin)
                        File.Delete(file);  // 원본 파일 삭제
                }
            }

        }

        private static bool ShowWarningEditorPopUp(string text)
        {
            return EditorUtility.DisplayDialog("Warning", text, "Continue", "Cancel");
        }
    }

}