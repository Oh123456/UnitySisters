using System.Collections;
using System.Collections.Generic;
using System.IO;

using UnityEngine;
using Newtonsoft.Json;


namespace UnityFramework
{

	public abstract class GameSettings
	{
        public abstract void Initialize();

    }

	public static class GameSettingsUtil
    {
        
        public const string SETTING_FILE_NAME = "GameSettings.json";

		public static string SAVE_PATH = $"{Application.persistentDataPath}/{SETTING_FILE_NAME}";

        public static void SaveSettings(GameSettings settings)
        {
            string json = JsonConvert.SerializeObject(settings);
            File.WriteAllText(SAVE_PATH, json);
        }

        public static T LoadGetSetting<T>() where T : GameSettings, new()
		{
            T settings = null;

            if (File.Exists(SAVE_PATH)) 
			{
                string json = File.ReadAllText(SAVE_PATH);

                settings = JsonConvert.DeserializeObject<T>(json);
            }
			else
			{
                settings = new T();
                SaveSettings(settings);
            }
            
            
            return settings;

		}
    } 
}
