using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AddressableEditor
{
    public static partial class AddressableBuildMenu
    {
        const string BUILD_SETTING_PATH = "Assets/Editor";
        const string BUILD_SETTING_NAME = "AddressableBuildSetting.asset";

        const string BUILD_WARNING = "The IsBuildCilerFolder option is enabled in Editor/AddressableBuildSetting.\r\nDuring the build process, everything except the folder will be deleted before building. \r\n\n Editor/AddressableBuildSetting에 IsBuildCilerFolder 옵션이 활성화가 도있습니다. \r\n빌드시 폴더를 제외하고 전부 삭제된후 빌드합니다.";
        const string BUILD_BACKUP_PATH_WARNING = "The backUpPath value in Editor/AddressableBuildSetting is empty.\r\nIn this case, the process will automatically proceed in the Documents folder. \r\n\n Editor/AddressableBuildSetting에 backUpPath 값이 비어 있습니다.\r\n이 경우 문서 폴더에 자동으로 진행됩니다. ";
        const string BUILD_BACKUP_RULE_WARNING = "The backUpFolderNameRule value in Editor/AddressableBuildSetting is empty.\r\nIn this case, the current time will be automatically applied. \r\n\n Editor/AddressableBuildSetting에 backUpFodierNameRule 값이 비어 있습니다. \r\n이 경우 현재 시각으로 자동으로 적용됩니다.";

    }

}