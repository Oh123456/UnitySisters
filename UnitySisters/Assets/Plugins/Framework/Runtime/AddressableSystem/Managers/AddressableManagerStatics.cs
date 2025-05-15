using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;


namespace UnityFramework.Addressable
{
    public partial class AddressableManager
    {


        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void AddressableLog(object msg)
        {
            AddressableLog(msg, Color.white);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void AddressableLog(object msg, Color color)
        {
            Debug.Log($"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{msg}</color>");
        }
    }

}