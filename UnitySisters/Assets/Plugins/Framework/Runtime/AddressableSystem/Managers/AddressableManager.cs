using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityFramework.Addressable.Managing;



#if USE_ADDRESSABLE_TASK
using System.Threading.Tasks;
using TaksLables = System.Threading.Tasks.Task<long>;
#else
using Cysharp.Threading.Tasks;
using TaksLables = Cysharp.Threading.Tasks.UniTask<long>;
#endif

namespace UnityFramework.Addressable
{
    public enum ByteUnit : long
    {
        B  = 1,
        KB = 1024,
        MB = 1024 * 1024,
        GB = 1024 * 1024 * 1024,
    }

    public struct AddressableDownLoadData
    {
        public AsyncOperationHandle handle;
        public string label;
    }

    public struct AddressableDownLoadUtility
    {       
        public static ByteUnit GetByteUnit(long downLoadSize)
        {
            if (downLoadSize < (long)ByteUnit.KB)
                return ByteUnit.B;
            if (downLoadSize < (long)ByteUnit.MB)
                return ByteUnit.KB;
            if (downLoadSize < (long)ByteUnit.GB)
                return ByteUnit.MB;

            return ByteUnit.GB;
        }

        public static float ToUnit(long downLoadSize, ByteUnit byteUnit)
        {
            return (float)downLoadSize / (float)byteUnit;
        }
    }

    public partial class AddressableManager : Singleton.LazySingleton<AddressableManager>
    {
        public const string BUILD_LABELS_PATH = "Assets/Resources";

        public event Action OnAllCompletedLoad;
        public event Action<AddressableDownLoadData> OnDownloadDependencies;
        public event Action<AddressableDownLoadData> OnDownload;

        private event Action OnCompletedLoad;

        AddressableBuildLabels addressableBuildLabels;

#if UNITY_EDITOR
        public AddressableManager()
        {
            UnityEditor.EditorApplication.playModeStateChanged += (playModeStateChange) =>
            {
                switch (playModeStateChange)
                {
                    case UnityEditor.PlayModeStateChange.EnteredEditMode:
                        break;
                    case UnityEditor.PlayModeStateChange.ExitingEditMode:
                        break;
                    case UnityEditor.PlayModeStateChange.EnteredPlayMode:
                        break;
                    case UnityEditor.PlayModeStateChange.ExitingPlayMode:
                        if (this.addressableDataManager.IsValueCreated)
                            this.addressableDataManager.Value.Release();
                        break;
                }
                if (playModeStateChange == UnityEditor.PlayModeStateChange.EnteredPlayMode)
                {

                }
                Debug.Log($"PlayModeStateChange : {playModeStateChange}");
            };
        }

#endif

        public async TaksLables CheckDownLoadBundle(List<string> customLabels = null)
        {
            AddressableLog("DownLoadCheck");
            List<string> labels = CheckLabels(customLabels);
            if (labels == null)
                return 0;

            AddressableLog($"SizeCheckLabel : {labels}");
            var handle = Addressables.GetDownloadSizeAsync(labels);
#if USE_ADDRESSABLE_TASK
            await handle.Task; 
#else
            await handle.ToUniTask();
#endif
            long size = handle.Result;
            Addressables.Release(handle);
            return size;

        }

        public async void DownLoadBundle(List<string> downLoadLabels = null)
        {
            AddressableLog("Addressables Start");

            List<string> labels = CheckLabels(downLoadLabels);
            if (labels == null)
                return ;

            var handler = Addressables.DownloadDependenciesAsync(labels, Addressables.MergeMode.Union);
            this.OnDownload?.Invoke(new AddressableDownLoadData()
            {
                handle = handler,
                label = labels[0]
            });

#if USE_ADDRESSABLE_TASK
            await handler.Task;
#else
            await handler.ToUniTask();
#endif

            DownloadAddressable(handler);
            Addressables.Release(handler);
            CompleteAll();
        }

        /// <summary>
        /// A function in Addressables that downloads remote asset bundles, storing them in the local cache via the network, allowing them to be loaded later. 
        /// </summary>
        /// <param name="customLabels">If the list of labels to download is set to null, all currently used labels are automatically detected, and the corresponding assets are downloaded. This allows necessary resources to be loaded without explicitly specifying labels.</param>
        [System.Obsolete("Use DownLoadBundle")]
        public async void DownLoad(List<string> customLabels = null)
        {
            List<string> labelNames;
            AddressableLog("Addressables Start");
            if (customLabels == null)
            {
                AddressableBuildLabels labels = Resources.Load<AddressableBuildLabels>(AddressableBuildLabels.NAME);

                if (labels == null)
                {
                    AddressableLog($"AddressableBuildLabels Not Found");
                    return;
                }

                labelNames = labels.Labels;
            }
            else
            {
                labelNames = customLabels;
            }

            int count = labelNames.Count;
            int downLoadCount = 0;

            System.Action downloadCallback = () =>
            {
                ++downLoadCount;
                if (downLoadCount >= count)
                {
                    CompleteAll();
                }
            };
            for (int i = 0; i < count; i++)
            {
                string label = labelNames[i];
                AddressableLog($"DownLabel : {label}");
                var handle = Addressables.GetDownloadSizeAsync(label);
                this.OnDownloadDependencies?.Invoke(new AddressableDownLoadData()
                {
                    handle = handle,
                    label = label
                });

#if USE_ADDRESSABLE_TASK
                await handle.Task; 
#else
                await handle.ToUniTask();
#endif

                OnCompletedLoad += downloadCallback;

                DownLoadAddressables(handle, label);

            }

        }

        private List<string> CheckLabels(List<string> customLabels)
        {
            List<string> labelNames = null;
            if (customLabels == null)
            {
                if (addressableBuildLabels == null)
                    addressableBuildLabels = Resources.Load<AddressableBuildLabels>(AddressableBuildLabels.NAME);

                if (addressableBuildLabels == null)
                {
                    AddressableLog($"AddressableBuildLabels Not Found");
                    return null;
                }

                labelNames = addressableBuildLabels.Labels;
            }
            else
            {
                labelNames = customLabels;
            }

            return labelNames;
        }



        async void DownLoadAddressables(AsyncOperationHandle<long> completedHandler, string label)
        {
            if (completedHandler.Status != AsyncOperationStatus.Succeeded)
            {
                AddressableLog("Failed", Color.red);
                return;
            }

            if (completedHandler.Result < 1)
            {
                AddressableLog("handle.Result < 1", Color.green);
                CompletedLoad();
#if UNITY_EDITOR
                Editor.AddressableManagingDataManager.ClearData();
#endif
                Addressables.Release(completedHandler);
                return;
            }

            Addressables.Release(completedHandler);


            var handler = Addressables.DownloadDependenciesAsync(label);
            this.OnDownload?.Invoke(new AddressableDownLoadData()
            {
                handle = handler,
                label = label
            });

#if USE_ADDRESSABLE_TASK
            await handler.Task;
#else
            await handler.ToUniTask();
#endif
            DownloadAddressable(handler);
            Addressables.Release(handler);
            return;
        }


        void DownloadAddressable(AsyncOperationHandle handle)
        {
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                AddressableLog("Download Completed", Color.green);
                CompletedLoad();
                return;
            }
            AddressableLog("Download Failed", Color.red);
            return;
        }

        void CompletedLoad()
        {
            this.OnCompletedLoad?.Invoke();
            this.OnCompletedLoad = null;
        }

        private void CompleteAll()
        {
            OnAllCompletedLoad?.Invoke();
            OnAllCompletedLoad = null;
            OnDownload = null;
            OnDownloadDependencies = null;
            OnCompletedLoad = null;
        }
    }

}