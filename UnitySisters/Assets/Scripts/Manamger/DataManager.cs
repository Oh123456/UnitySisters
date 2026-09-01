using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityFramework.Singleton;

namespace UnitySisters.Manager
{

    public class DataManager : LazySingleton<DataManager>
    {
        private Dictionary<string, AttackDataScriptableObject> attackDatas;

        public void LoadDatas()
        {
            LoadAttackDatas();
        }

        private void LoadAttackDatas()
        {
            //TODO:: 임시니까 난중에 바꿀것
            Addressables.LoadAssetsAsync<AttackDataScriptableObject>("AttackData").Completed += (handle) =>
            {
                if (!handle.IsValid())
                {
                    Debug.LogError("z");
                    return;
                }

                var list = handle.Result;
                int count = list.Count;
                attackDatas = new Dictionary<string, AttackDataScriptableObject>(count);
                for (int i = 0; i < count; i++)
                {
                    var data = list[i];
                    attackDatas.Add(data.Key, data);
                }
            };
        }


        public bool TryGetAttackData(string key, out AttackDataScriptableObject attackData)
        {
            return attackDatas.TryGetValue(key, out attackData);
        }
    }
}