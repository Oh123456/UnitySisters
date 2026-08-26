using UnityEditor;
using UnityEngine;

namespace UnityFramework.FSM.Editor
{
    internal static class FSMStateScriptCreator
    {
        private const string TemplateGUID = "36cd32662bbb4087802c9fe94dbcb49a";

        /// <summary>
        /// Project 창에서 선택한 폴더에 State 상속 스크립트를 생성한다.
        /// </summary>
        [MenuItem("Assets/Create/FSM/State Script", false, 82)]
        private static void CreateStateScript()
        {
            string templatePath = AssetDatabase.GUIDToAssetPath(TemplateGUID);
            if (string.IsNullOrEmpty(templatePath))
            {
                Debug.LogError("FSM State script template could not be found.");
                return;
            }

            ProjectWindowUtil.CreateScriptAssetFromTemplateFile(
                templatePath,
                "NewState.cs");
        }
    }
}
