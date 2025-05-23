using UnityEngine;

namespace CoreSystem
{
    internal class Bootstrapper
    {
        private Bootstrapper() { }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Initialize()
        {
            ProjectSetting projectSetting = Resources.Load<ProjectSetting>("ProjectSetting");
            if (projectSetting == null)
            {
                Debug.LogError("ProjectSetting is Null");
                Application.Quit();
                return;
            }

            projectSetting.Initialize();
        }
    } 
}
