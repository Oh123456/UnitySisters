using System.Collections;
using System.Collections.Generic;


using UnityEditor;

using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;
using System.Buffers;
using System.Text.RegularExpressions;





#if USE_ADDRESSABLE_TASK
using System.Threading.Tasks;
#else
using Cysharp.Threading.Tasks;
#endif

#if UNITY_EDITOR
namespace UnityFramework.Addressable.Editor
{
    using static UnityFramework.Addressable.Editor.AddressableManagingDataManager;
    public static partial class AddressableManagingDataManager
    {
        public enum LoadType
        {
            UnsafeLoad = 0,
            SafeLoad,
            Max,
        }

        public const int UNSAFE_LOAD_STACK_COUNT = 3;
        public const int SAFE_LOAD_STACK_COUNT = 4;
        public static Dictionary<string, AddressableManagingData> addressableManagingDatas = new Dictionary<string, AddressableManagingData>();
        private static bool? isTracking;

        public static event System.Action OnUpdated;

        public static bool IsTracking
        {
            private get
            {
                if (isTracking == null)
                    isTracking = EditorPrefs.GetBool("_isTracking", true);
                return isTracking.Value;
            }

            set
            {
                isTracking = value;
            }
        }

        public static void TrackEditorLoad<T>(AsyncOperationHandle<T> handle, LoadType loadType, object key, System.Action<string> outAssetKey)
        {
            System.Diagnostics.StackTrace stackTrace = null;


            if (IsTracking)
            {
                stackTrace = new System.Diagnostics.StackTrace(true);
            }


            handle.Completed += async (AsyncOperationHandle<T> handle) =>
            {
                Object loadedAsset = handle.Result as Object;
                if (loadedAsset == null)
                {
                    AddressableManager.AddressableLog($"{handle.DebugName} is Not Object Type", Color.yellow);
                    return;
                }
                var loadResource = Addressables.LoadResourceLocationsAsync(key);

#if USE_ADDRESSABLE_TASK
                await loadResource.Task; 
#else
                await loadResource.ToUniTask();
#endif
                if (loadResource.Status != AsyncOperationStatus.Succeeded && loadResource.Result.Count <= 0)
                {
                    AddressableManager.AddressableLog(" 이 Addressable 에셋은 프로젝트 내부에 없음.", Color.red);
                    return;
                }

                string assetGUID = loadResource.Result[0].PrimaryKey;
                if (!string.IsNullOrEmpty(assetGUID))
                {
                    outAssetKey?.Invoke(assetGUID);

                    if (!addressableManagingDatas.TryGetValue(assetGUID, out var data))
                    {
                        data = new AddressableManagingData();
                        addressableManagingDatas.Add(assetGUID, data);
                    }
                    data.loadCount++;
                    object accessKey = key is IKeyEvaluator keyEvaluator ? keyEvaluator.RuntimeKey : key;
                    data.accessKeys.Add(accessKey);
                    data.name = handle.DebugName;
                    if (loadType == LoadType.SafeLoad)
                        data.seceneName = SceneManager.GetActiveScene().name;
                    else
                        data.seceneName = string.Empty;
                    if (stackTrace != null)
                    {
                        var traces = data.GetAddressableManagingDataStackTrace(loadType);

                        int index = loadType == LoadType.UnsafeLoad ? UNSAFE_LOAD_STACK_COUNT : SAFE_LOAD_STACK_COUNT;
                        System.Diagnostics.StackFrame frame = stackTrace.GetFrame(index - 1);
                        string info = $"{frame.GetMethod().DeclaringType}.{frame.GetMethod().Name} ({frame.GetFileName()}:{frame.GetFileLineNumber()})";
                        if (!traces.TryGetValue(info, out var trace))
                        {
                            trace = new AddressableManagingData.AddressableManagingDataStackTrace();
                            traces.Add(info, trace);
                        }
                        trace.SetStackTrace(info);
                        trace.count++;
                        OnUpdated?.Invoke();
                    }

                    AddressableManager.AddressableLog($"Addressable 에셋의 GUID: {assetGUID}", Color.yellow);
                }
                else
                {
                    AddressableManager.AddressableLog(" 이 Addressable 에셋은 프로젝트 내부에 없음.", Color.red);
                }
            };
        }


        public static void TrackRelease<T>(AsyncOperationHandle<T> handle, string assetKey)
        {
            if (!string.IsNullOrEmpty(assetKey))
            {
                if (!addressableManagingDatas.TryGetValue(assetKey, out var data))
                {
                    data = new AddressableManagingData();
                    addressableManagingDatas.Add(assetKey, data);
                }
                data.loadCount--;
                OnUpdated?.Invoke();

                AddressableManager.AddressableLog($"Addressable 에셋의 GUID: {assetKey}", Color.yellow);
            }
        }

        public static void ClearData()
        {
            addressableManagingDatas.Clear();
        }
    }

    public class AddressableManagingData
    {
        public class AddressableManagingDataStackTrace : System.IEquatable<AddressableManagingDataStackTrace>
        {            
            private const string STACKTRACE_PATTERN = @"^([\w\+<>]+)\.?([\w<>\.]+)?\s*\(([^()]+\.cs):(\d+)\)$";

            private string stackTrace = string.Empty;
            private string functionName = string.Empty;
            private string fullPath = string.Empty;
            private string path = string.Empty;
            private int lineNumber = 0;
            public int count = 0;

            public string FunctionName => functionName;
            public string FullPath => fullPath;
            public string Path => path;
            public int LineNumber => lineNumber;

            public void SetStackTrace(string trace)
            {
                stackTrace = trace;

                Match match = Regex.Match(stackTrace, STACKTRACE_PATTERN);

                if (match.Success)
                {
                    functionName = $"{match.Groups[1].Value}.{match.Groups[2].Value}";
                    fullPath = match.Groups[3].Value;
                    lineNumber = int.Parse(match.Groups[4].Value);

                    int index = fullPath.IndexOf("Assets\\");
                    path = index == -1 ? fullPath : fullPath.Substring(index);
                }

            }


            public bool Equals(AddressableManagingDataStackTrace other)
            {
                return stackTrace == other.stackTrace;
            }

            public override int GetHashCode()
            {
                return stackTrace.GetHashCode();
            }

        }

        public HashSet<object> accessKeys = new HashSet<object>();
        public Dictionary<LoadType, Dictionary<string, AddressableManagingDataStackTrace>> stackTraces = new Dictionary<LoadType, Dictionary<string, AddressableManagingDataStackTrace>>((int)LoadType.Max);
        public string name;
        public int loadCount = 0;
        /// <summary>
        /// 에디터용
        /// </summary>
        public bool foldout = false;
        public List<bool> foldouts = new List<bool>(2) { false, false };
        public string seceneName = string.Empty;

        public Dictionary<string, AddressableManagingDataStackTrace> GetAddressableManagingDataStackTrace(LoadType loadType)
        {
            switch (loadType)
            {
                case LoadType.UnsafeLoad:
                case LoadType.SafeLoad:
                    if (!stackTraces.TryGetValue(loadType, out var trces))
                    {
                        trces = new Dictionary<string, AddressableManagingDataStackTrace>();
                        stackTraces.Add(loadType, trces);
                    }
                    return trces;
                case LoadType.Max:
                default:
                    return null;
            }
        }

        public int AccessKeysCount()
        {
            return accessKeys.Count;
        }

        public AddressableManagingDataStackTrace[] GetAddressableManagingDataStackTraces(Dictionary<string, AddressableManagingDataStackTrace> trces)
        {
            if (trces == null)
                return null;

            AddressableManagingDataStackTrace[] addressableManagingDataStackTraces = null;

            int count = trces.Count;
            addressableManagingDataStackTraces = ArrayPool<AddressableManagingDataStackTrace>.Shared.Rent(count);


            int i = 0;
            foreach (var tr in trces)
            {
                addressableManagingDataStackTraces[i] = tr.Value;
                i++;
            }
            return addressableManagingDataStackTraces;
        }


        public void ReturnAddressableManagingDataStackTraces(AddressableManagingDataStackTrace[] addressableManagingDataStackTraces)
        {
            if (addressableManagingDataStackTraces == null)
                return;
            ArrayPool<AddressableManagingDataStackTrace>.Shared.Return(addressableManagingDataStackTraces, clearArray: true);
        }
    }
}

#endif