using UnityEditor;
using UnityEngine;

namespace UnityFramework.BattleSystem.Editor
{
    internal static class BattleAttributeSetScriptCreator
    {
        private const string TemplateGUID = "f31b98e2dd264fd18f56369d8a87df61";

        [MenuItem("Assets/Create/UnityFramework/Battle System/Attribute Set Script", false, 82)]
        private static void CreateAttributeSetScript()
        {
            string templatePath = AssetDatabase.GUIDToAssetPath(TemplateGUID);
            if (string.IsNullOrEmpty(templatePath))
            {
                Debug.LogError("Battle Attribute Set script template could not be found.");
                return;
            }

            ProjectWindowUtil.CreateScriptAssetFromTemplateFile(
                templatePath,
                "NewBattleAttributeSet.cs");
        }
    }
}
