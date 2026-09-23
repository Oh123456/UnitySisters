using UnityEditor;
using UnityEngine;

namespace UnityFramework.BattleSystem.Editor
{
    [FilePath(
        "ProjectSettings/UnityFrameworkBattleSystemSettings.asset",
        FilePathAttribute.Location.ProjectFolder)]
    internal sealed class BuffModifierSystemSettings : ScriptableSingleton<BuffModifierSystemSettings>
    {
        internal const string DefaultAssemblyName = "Assembly-CSharp";

        [SerializeField] private string searchAssemblyName = DefaultAssemblyName;

        internal string SearchAssemblyName => string.IsNullOrWhiteSpace(searchAssemblyName)
            ? DefaultAssemblyName
            : searchAssemblyName;

        internal void SetSearchAssemblyName(string assemblyName)
        {
            searchAssemblyName = string.IsNullOrWhiteSpace(assemblyName)
                ? DefaultAssemblyName
                : assemblyName;
            Save(true);
        }
    }
}
