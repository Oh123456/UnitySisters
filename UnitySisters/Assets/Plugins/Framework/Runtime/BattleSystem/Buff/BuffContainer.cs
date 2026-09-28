using System.Collections.Generic;
using UnityEngine;

namespace UnityFramework.BattleSystem
{
    using Pool;

    public class BuffContainer : IBuffContainer, System.IDisposable
    {
        private Dictionary<int, int> buffIndexs = new Dictionary<int, int>();
        private List<BuffInstance> buffList = new List<BuffInstance>();
        private BattleAttributeSet controlBattleAttributeSet;

        public void AddBuff(BuffState buffState)
        {
            BuffInstance buffInstance = null;
            if (!buffIndexs.TryGetValue(buffState.BuffData.BuffID, out int index))
            {
                buffInstance = PoolManager.GetClassObject<BuffInstance>();
                buffInstance.SetBuffData(buffState, controlBattleAttributeSet);
                buffIndexs.Add(buffState.BuffData.BuffID, buffList.Count);
                buffList.Add(buffInstance);
            }
            else
            {
                buffInstance = buffList[index];
            }

            int currentStack = buffInstance.Stack;

            // 스택이 작으면~
            if (currentStack < buffState.BuffData.MaxStack)
            {
                buffInstance.Stack = currentStack + 1;
                buffInstance.StackChanged();
            }
        }

        public bool RemoveBuff(int buffId)
        {
            if (!buffIndexs.TryGetValue(buffId, out int index))
                return false;
            
            var removeInstance = buffList[index];

            int removeStack = removeInstance.Stack - 1;
            if (removeStack > 0)
            {
                removeInstance.Stack = removeStack;
                removeInstance.StackChanged();
                return false;
            }
            
            int lastIndex = buffList.Count - 1;
            if (index != lastIndex)
            {
                var swapInstance = buffList[lastIndex];
                buffList[index] = swapInstance;
                buffIndexs[swapInstance.BuffState.BuffData.BuffID] = index;
            }

            buffIndexs.Remove(buffId);
            buffList.RemoveAt(lastIndex);

            removeInstance.Reslase();
            PoolManager.SetClassObject(removeInstance);
            return true;
        }

        public void SetBattleAttributeSet(BattleAttributeSet battleAttributeSet)
        {
            controlBattleAttributeSet = battleAttributeSet;
        }

        public void Update()
        {
            float deltaTime = Time.deltaTime;
            int count = buffList.Count;
            for (int i = 0; i < count;)
            {
                var buff = buffList[i];
                buff.Update(deltaTime);
                if (buff.IsExpired() &&
                    RemoveBuff(buff.BuffID))
                {
                    --count;
                    continue;
                }
                i++;
            }
        }

        public void Dispose()
        {
            controlBattleAttributeSet = null;
            int count = buffList.Count;
            for (int i = 0; i < count; i++)
            { 
                var item = buffList[i];
                if (item == null)
                    continue;
                PoolManager.SetClassObject(item);
            }
            buffIndexs.Clear();
        }
    }

}