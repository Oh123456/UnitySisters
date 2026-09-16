using UnityEditor;
using UnityEngine;

namespace UnityFramework.Animation.Editor
{
    internal static class AnimationEventCommandScriptCreator
    {
        private const string TriggerTemplateGUID = "c54d95ad45fe417388bee5e8127ed50c";
        private const string ContinuousTemplateGUID = "4d592a0afe504c658fb52a7bf290139d";

        [MenuItem("Assets/Create/UnityFramework/Animation Event/Command Script/Trigger", false, 82)]
        private static void CreateTriggerCommandScript()
        {
            CreateCommandScript(TriggerTemplateGUID);
        }

        [MenuItem("Assets/Create/UnityFramework/Animation Event/Command Script/Continuous", false, 83)]
        private static void CreateContinuousCommandScript()
        {
            CreateCommandScript(ContinuousTemplateGUID);
        }

        private static void CreateCommandScript(string templateGuid)
        {
            string templatePath = AssetDatabase.GUIDToAssetPath(templateGuid);
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
