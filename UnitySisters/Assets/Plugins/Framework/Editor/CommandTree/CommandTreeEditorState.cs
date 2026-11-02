using System;
using System.Collections.Generic;

using UnityEditor;

using UnityEngine;

namespace UnityFramework.CommandTree.Editor
{
    [FilePath("Library/CommandTreeEditorState.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class CommandTreeEditorState : ScriptableSingleton<CommandTreeEditorState>
    {
        [Serializable]
        private sealed class NodePosition
        {
            [SerializeField] public string contextKey;
            [SerializeField] public string nodeIdentity;
            [SerializeField] public Vector2 position;
        }

        [SerializeField] private List<NodePosition> nodePositions = new List<NodePosition>();

        public Dictionary<string, Vector2> GetPositions(string contextKey)
        {
            Dictionary<string, Vector2> positions = new Dictionary<string, Vector2>();
            for (int index = 0; index < nodePositions.Count; index++)
            {
                NodePosition nodePosition = nodePositions[index];
                if (nodePosition.contextKey == contextKey)
                    positions[nodePosition.nodeIdentity] = nodePosition.position;
            }

            return positions;
        }

        public void SetPositions(string contextKey, IReadOnlyDictionary<string, Vector2> positions)
        {
            for (int index = nodePositions.Count - 1; index >= 0; index--)
            {
                if (nodePositions[index].contextKey == contextKey)
                    nodePositions.RemoveAt(index);
            }

            foreach (KeyValuePair<string, Vector2> pair in positions)
            {
                nodePositions.Add(new NodePosition
                {
                    contextKey = contextKey,
                    nodeIdentity = pair.Key,
                    position = pair.Value
                });
            }

            Save(true);
        }
    }
}
