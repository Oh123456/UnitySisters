using PureComponents;
using PureComponents.Interfaces;
using System.Collections.Generic;
using UnityEngine;

public class CustomMonoBehaviour : MonoBehaviour
{
    protected Dictionary<System.Type, PureComponent> pureComponents = new Dictionary<System.Type, PureComponent>();
    private Queue<PureComponent> destroyComponentQueue = null;

    public T AddComponent<T>() where T : PureComponent , new()
    {
        System.Type type = typeof(T);
        if (pureComponents.TryGetValue(type, out PureComponent component))
        {
            Debug.LogWarning($"{name} 에 {type.Name}은 이미 있습니다.");
            return component as T;
        }

        T TComponent = new T()
        {
            customMonoBehaviour = this,
        };

        pureComponents.Add(typeof(T), TComponent);
        return TComponent;
    }

    public bool RemoveComponent<T>() where T : PureComponent, new()
    {
        System.Type type = typeof(T);
        if (!pureComponents.TryGetValue(type, out PureComponent component))
            return false;
        component.Destroy();

        return true;
    }


    private void DestroyComponent()
    {
        if (destroyComponentQueue == null || destroyComponentQueue.Count < 1)
            return;
    }

    protected virtual void LateUpdate()
    {
        DestroyComponent();
    }
}
