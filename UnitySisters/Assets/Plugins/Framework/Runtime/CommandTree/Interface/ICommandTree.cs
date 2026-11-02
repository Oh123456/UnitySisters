using System.Collections.Generic;
using UnityEngine;

namespace UnityFramework.CommandTree
{
    public interface IReadOnlyCommandTree<TKey>
    {
        bool TryFindLongest(IReadOnlyList<TKey> keys, out ICommandBehavior behavior, out int matchedCount);
        bool TryFindLongest(IReadOnlyList<TKey> keys, int startIndex, int keyCount, out ICommandBehavior behavior, out int matchedCount);
        ICommandTreeNode FindChild(ICommandTreeNode parentNode, TKey key);

        ICommandTreeNode Root { get; }
    }

}