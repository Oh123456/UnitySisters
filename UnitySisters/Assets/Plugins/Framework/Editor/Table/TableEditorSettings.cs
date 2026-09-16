using UnityEditor;
using UnityEngine;

namespace UnityFramework.Table.Editor
{
    [FilePath("ProjectSettings/UnityFrameworkTableEditorSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class TableEditorSettings : ScriptableSingleton<TableEditorSettings>
    {
        internal const string DefaultRootFolder = "Assets/Samples/Table";

        [SerializeField] private string rootFolderGuid;

        internal string RootFolderPath
        {
            get
            {
                string path = AssetDatabase.GUIDToAssetPath(rootFolderGuid);
                return AssetDatabase.IsValidFolder(path) ? path : DefaultRootFolder;
            }
        }

        internal void SetRootFolder(string folderPath)
        {
            rootFolderGuid = AssetDatabase.AssetPathToGUID(folderPath);
            Save(true);
        }
    }
}
