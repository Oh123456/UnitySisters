using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityFramework.Addressable.Managing;
using UnityEngine;



#if UNITY_EDITOR
using UnityEditor;
#endif


#if USE_ADDRESSABLE_TASK
using System.Threading.Tasks;
#else
using Cysharp.Threading.Tasks;
#endif

namespace UnityFramework.Addressable
{
    public partial class AddressableManager
    {
        public event System.Action<float> OnLoadScenePercent;
        public event System.Action<SceneInstance> OnSceneLoadCompleted;

        System.Lazy<AddressableDataManager> addressableDataManager = new System.Lazy<AddressableDataManager>(() => new AddressableDataManager());

        /// <summary>
        /// If the loaded asset is not properly managed, it will not be released from memory, which may result in memory leaks.
        /// </summary>
        /// <param name="key">AddressableKey</param>
        /// <returns> UnsafeHandler</returns>
        public static AddressableResourceHandle<T> UnsafeLoadAsset<T>(object key)
        {
            var handle = Addressables.LoadAssetAsync<T>(key);
            AddressableResourceHandle<T> addressableResourceHandle = new AddressableResourceHandle<T>(handle);
#if UNITY_EDITOR
            Editor.AddressableManagingDataManager.TrackEditorLoad(handle, Editor.AddressableManagingDataManager.LoadType.UnsafeLoad, key, (assetKey) =>
            {
                addressableResourceHandle.editor_AssetKey = assetKey;
            });
#endif
            return addressableResourceHandle;
        }

        /// <summary>
        /// If the loaded asset is not properly managed, it will not be released from memory, potentially leading to memory leaks. However, by returning the handler using the out keyword, unnecessary copying is reduced, improving performance optimization.
        /// </summary>
        /// <param name="key">AddressableKey</param>
        /// <param name="addressableResourceHandle">UnsafeHandler</param>
        public static void UnsafeLoadAsset<T>(object key, out AddressableResourceHandle<T> addressableResourceHandle)
        {
            var handle = Addressables.LoadAssetAsync<T>(key);
            addressableResourceHandle = new AddressableResourceHandle<T>(handle);
#if UNITY_EDITOR
            System.Action<string> action = addressableResourceHandle.SetEditorAssetKey;
            Editor.AddressableManagingDataManager.TrackEditorLoad(handle, Editor.AddressableManagingDataManager.LoadType.UnsafeLoad, key, (assetKey) =>
            {
                action(assetKey);
            });
#endif
            return;
        }

        /// <summary>
        /// This is an Addressables asset managed on a per-scene basis, and it is automatically released when the scene transitions. As a result, memory is efficiently managed without requiring manual deallocation.
        /// </summary>
        /// <param name="key">AddressableKey</param>
        /// <returns>AddressableHandler </returns>
        public AddressableResource<T> LoadAsset<T>(object key)
        {
            return this.addressableDataManager.Value.LoadResource<T>(key);
        }

        /// <summary>
        /// The asset is first loaded into memory, and then a new instance is created based on it, allowing it to be used within the game.
        /// </summary>
        /// <typeparam name="T">Object Type</typeparam>
        /// <param name="key">key</param>
        /// <param name="addressableResourceHandle"> Unsafe Handler </param>
        /// <param name="OnCompleted"> Create CallBack </param>
        public static void UnsafeInstantiateAsset<T>(object key, out AddressableResourceHandle<T> addressableResourceHandle, System.Action<GameObject> OnCompleted) where T : Object
        {
            UnsafeLoadAsset(key, out addressableResourceHandle);
            WaitInstantiateAsset(addressableResourceHandle,OnCompleted);
        }


        /// <summary>
        /// The asset is first loaded into memory, and then a new instance is created based on it, allowing it to be used within the game.
        /// </summary>
        /// <typeparam name="T">Object Type</typeparam>
        /// <param name="key">key</param>
        /// <param name="addressableResource"> Addressable Handler </param>
        /// <param name="OnCompleted"> Create CallBack </param>
        public AddressableResource<T> InstantiateAsset<T>(object key, System.Action<GameObject> OnCompleted) where T : Object
        {
            AddressableResource<T> addressableResource = LoadAsset<T>(key);
            WaitInstantiateAsset(addressableResource, OnCompleted);
            return addressableResource;
        }


        private async void WaitInstantiateAsset<T>(AddressableResource<T> addressableResource, System.Action<GameObject> OnCompleted) where T : Object
        {
            await addressableResource.Task;
            GameObject gameObject = (GameObject.Instantiate(addressableResource.GetResource())) as GameObject;
            OnCompleted?.Invoke(gameObject);
        }

        private static async void WaitInstantiateAsset<T>(AddressableResourceHandle<T> addressableResourceHandle, System.Action<GameObject> OnCompleted) where T : Object
        {
            await addressableResourceHandle.Task;
            GameObject gameObject = (GameObject.Instantiate(addressableResourceHandle.GetResource())) as GameObject;
            OnCompleted?.Invoke(gameObject);
        }


        /// <summary>
        /// As confirmed so far, scene loading using Addressables does not function properly on mobile devices.
        /// </summary>
        public async void LoadScene(object sceneKey, UnityEngine.SceneManagement.LoadSceneMode loadSceneMode)
        {
            AsyncOperationHandle<SceneInstance> asyncOperationHandle = Addressables.LoadSceneAsync(sceneKey, loadSceneMode);

            while (!asyncOperationHandle.IsDone)
            {
                this.OnLoadScenePercent?.Invoke(asyncOperationHandle.PercentComplete);

#if USE_ADDRESSABLE_TASK
                await Task.Yield();
#else
                await UniTask.Yield();
#endif
            }

#if USE_ADDRESSABLE_TASK
            await Addressables.UnloadSceneAsync(asyncOperationHandle).Task;
#else
            await Addressables.UnloadSceneAsync(asyncOperationHandle).ToUniTask();
#endif

            this.OnSceneLoadCompleted?.Invoke(asyncOperationHandle.Result);
            this.OnSceneLoadCompleted = null;
            this.OnLoadScenePercent = null;
        }


        public AddressableDataManager GetAddressableDataManager()
        {
            return this.addressableDataManager.Value;
        }


    }
}
