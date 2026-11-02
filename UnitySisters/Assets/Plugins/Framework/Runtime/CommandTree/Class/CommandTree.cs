using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityFramework.CommandTree
{
    public interface ICommandTreeNode
    {
        public ICommandBehavior CommandBehavior { get; }
    }

    [Serializable]
    public sealed class CommandTree<TKey> : IReadOnlyCommandTree<TKey>
    {
        [Serializable]
        private sealed class Node : ICommandTreeNode
        {
            [SerializeField] public TKey key;
            [SerializeReference] public ICommandBehavior value;
            [SerializeField] public List<Node> children;

            public ICommandBehavior CommandBehavior => value;

            public Node()
            {
            }

            public Node(TKey key)
            {
                this.key = key;
            }

        }

        private static readonly EqualityComparer<TKey> comparer = EqualityComparer<TKey>.Default;

        [SerializeField] private Node root = new Node();
        [NonSerialized] private bool isSetupComplete;
        [NonSerialized] private int count;

        public bool IsSetupComplete => isSetupComplete;
        public int Count => count;

        public ICommandTreeNode Root => root;

        public void Add(IReadOnlyList<TKey> keys, ICommandBehavior behavior)
        {
            if (keys == null)
                throw new ArgumentNullException(nameof(keys));

            Add(keys, 0, keys.Count, behavior);
        }

        public void Add(IReadOnlyList<TKey> keys, int startIndex, int keyCount, ICommandBehavior behavior)
        {
            if (isSetupComplete)
                throw new InvalidOperationException("Command tree setup is already complete.");
            if (keys == null)
                throw new ArgumentNullException(nameof(keys));
            if (behavior == null)
                throw new ArgumentNullException(nameof(behavior));

            ValidateRange(keys.Count, startIndex, keyCount);
            if (keyCount == 0)
                throw new ArgumentException("Command key sequence cannot be empty.", nameof(keyCount));

            Node currentNode = root;
            int endIndex = startIndex + keyCount;
            for (int keyIndex = startIndex; keyIndex < endIndex; keyIndex++)
            {
                TKey key = keys[keyIndex];
                if (key is null)
                    throw new ArgumentException("Command key cannot be null.", nameof(keys));

                Node childNode = FindChild(currentNode, key);
                if (childNode == null)
                {
                    childNode = new Node(key);
                    if (currentNode.children == null)
                        currentNode.children = new List<Node>();
                    currentNode.children.Add(childNode);
                }

                currentNode = childNode;
            }

            if (currentNode.value != null)
                throw new InvalidOperationException("The same command key sequence is already registered.");

            currentNode.value = behavior;
            count++;
        }

        public void CompleteSetup()
        {
            isSetupComplete = false;
            if (root == null)
                root = new Node();

            count = ValidateAndCount(root, true);
            isSetupComplete = true;
        }

        public bool TryFindLongest(IReadOnlyList<TKey> keys, out ICommandBehavior behavior, out int matchedCount)
        {
            if (keys == null)
                throw new ArgumentNullException(nameof(keys));

            return TryFindLongest(keys, 0, keys.Count, out behavior, out matchedCount);
        }

        public bool TryFindLongest(IReadOnlyList<TKey> keys, int startIndex, int keyCount, out ICommandBehavior behavior, out int matchedCount)
        {
            if (!isSetupComplete)
                throw new InvalidOperationException("Command tree setup is not complete.");
            if (keys == null)
                throw new ArgumentNullException(nameof(keys));

            ValidateRange(keys.Count, startIndex, keyCount);

            behavior = null;
            matchedCount = 0;

            Node currentNode = root;
            int endIndex = startIndex + keyCount;
            for (int keyIndex = startIndex; keyIndex < endIndex; keyIndex++)
            {
                currentNode = FindChild(currentNode, keys[keyIndex]);
                if (currentNode == null)
                    break;

                if (currentNode.value == null)
                    continue;

                behavior = currentNode.value;
                matchedCount = keyIndex - startIndex + 1;
            }

            return behavior != null;
        }

        public ICommandTreeNode FindChild(ICommandTreeNode parentNode, TKey key)
        {
            if (parentNode is Node node)
                return FindChild(node, key);
            else
                return null;
        }

        private Node FindChild(Node parentNode, TKey key)
        {
            List<Node> children = parentNode.children;
            if (children == null)
                return null;

            for (int childIndex = 0; childIndex < children.Count; childIndex++)
            {
                Node childNode = children[childIndex];
                if (comparer.Equals(childNode.key, key))
                    return childNode;
            }

            return null;
        }

        private static int ValidateAndCount(Node parentNode, bool isRoot)
        {
            if (parentNode == null)
                throw new InvalidOperationException("Command tree contains a null node.");
            if (!isRoot && parentNode.key is null)
                throw new InvalidOperationException("Command tree contains a null key.");

            int behaviorCount = parentNode.value == null ? 0 : 1;
            List<Node> children = parentNode.children;
            if (children == null)
                return behaviorCount;

            for (int childIndex = 0; childIndex < children.Count; childIndex++)
            {
                Node childNode = children[childIndex];
                if (childNode == null)
                    throw new InvalidOperationException("Command tree contains a null child node.");

                for (int duplicateIndex = 0; duplicateIndex < childIndex; duplicateIndex++)
                {
                    Node duplicateNode = children[duplicateIndex];
                    if (duplicateNode != null && comparer.Equals(duplicateNode.key, childNode.key))
                        throw new InvalidOperationException($"Command tree contains a duplicate key under the same parent: {childNode.key}.");
                }

                behaviorCount += ValidateAndCount(childNode, false);
            }

            return behaviorCount;
        }

        private static void ValidateRange(int collectionCount, int startIndex, int count)
        {
            if ((uint)startIndex > (uint)collectionCount)
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            if ((uint)count > (uint)(collectionCount - startIndex))
                throw new ArgumentOutOfRangeException(nameof(count));
        }
    }
}
