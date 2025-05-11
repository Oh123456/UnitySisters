using System.Collections.Generic;

using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;


namespace UnityFramework.Addressable.Managing
{
    public class AddressableDataManager
    {
        private  Dictionary<object, IAddressableResource> loadedResource = new Dictionary<object, IAddressableResource>();

        public event System.Func<bool> OnRelease;

        public AddressableDataManager()
        {
#if !CUSTOM_ADDRESSABLE_RELEASE
            SceneManager.sceneUnloaded += UnloadScene;
#endif
        }

        public AddressableResource<T> LoadResource<T>(object key)
        {
            object assetKey = null;

            if (key is IKeyEvaluator keyEvaluator)
                assetKey = keyEvaluator.RuntimeKey;
            else
                assetKey = key;

            IAddressableResource addressableResource = null;

            if (!loadedResource.TryGetValue(assetKey, out addressableResource))
            {
                AddressableManager.AddressableLog($"Loaded Addressable resource KeyCode : {assetKey}", Color.yellow);
                var handle = Addressables.LoadAssetAsync<T>(key);
                AddressableResourceHandle<T> addressableResourceHandle = new AddressableResourceHandle<T>(handle);
                addressableResource = new AddressableResource<T>(addressableResourceHandle);
#if UNITY_EDITOR
                AddressableResource<T> resource = addressableResource as AddressableResource<T>;    
                Editor.AddressableManagingDataManager.TrackEditorLoad(handle, Editor.AddressableManagingDataManager.LoadType.SafeLoad, key, (assetKey) =>
                {
                    resource.SetEdtior_AssetKey(assetKey);
                });
#endif
                loadedResource.Add(assetKey, addressableResource);
            }

            return addressableResource as AddressableResource<T>;
        }

        public void Release()
        {
            foreach (var pair in loadedResource)
            {
                IAddressableResource addressableResource = pair.Value;

                bool isReleaseComplete = false;
                while (!isReleaseComplete) 
                {
                   bool? releaseComplete = OnRelease?.Invoke();
                   isReleaseComplete = releaseComplete == null ? true : releaseComplete.Value;
                }
                
            }
            AddressableManager.AddressableLog($"LoadedResource Release!!", Color.blue);
            OnRelease = null;
            loadedResource.Clear();
        }

        private void UnloadScene(UnityEngine.SceneManagement.Scene scene)
        {
            Release();
            Resources.UnloadUnusedAssets();
        }
    }
}