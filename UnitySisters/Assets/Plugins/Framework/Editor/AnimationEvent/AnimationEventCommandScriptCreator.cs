using UnityEditor;
using UnityEngine;

namespace UnityFramework.Animation.Editor
{
    internal static class AnimationEventCommandScriptCreator
    {
        private const string TemplateGUID = "c54d95ad45fe417388bee5e8127ed50c";

        [MenuItem("Assets/Create/UnityFramework/Animation Event/Command Script", false, 82)]
        private static void CreateCommandScript()
        {
            string templatePath = AssetDatabase.GUIDToAssetPath(TemplateGUID);
            if (string.IsNullOrEmpty(templatePath))
            {
                Debug.LogError("Animation Event Command script template could not be found.");
                return;
            }

            ProjectWindowUtil.CreateScriptAssetFromTemplateFile(
                templatePath,
                "NewAnimationEventCommand.cs");
        }
    }
}
