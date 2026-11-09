using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityFramework.BehaviorTree
{
    [Serializable]
    internal sealed class BehaviorNodeData
    {
        [SerializeField] private string id;
        [SerializeField] private string name;
        [SerializeReference] private BehaviorNode behavior;
        [SerializeField] private List<string> children = new List<string>();
        [SerializeReference] private List<Decorator> decorators = new List<Decorator>();
        [SerializeReference] private List<Service> services = new List<Service>();
        [SerializeField] private Vector2 position;
        [SerializeField] private UtilityScoreSettings utilityScore = new();
        internal UtilityScoreSettings UtilityScore { get => utilityScore; }
        internal string ID { get => id; }
        internal string Name { get => name; set => name = value; }
        internal BehaviorNode Behavior { get => behavior; }
        internal List<string> Children { get => children; }
        internal IReadOnlyList<Decorator> Decorators { get => decorators; }
        internal IReadOnlyList<Service> Services { get => services; }
        internal Vector2 Position { get => position; set => position = value; }
        internal BehaviorNodeData(BehaviorNode behavior, Vector2 position)
        { id = Guid.NewGuid().ToString("N"); name = behavior.GetType().Name; this.behavior = behavior; this.position = position; }
    }

    [CreateAssetMenu(menuName = "UnityFramework/Behavior Tree/Tree")]
    public sealed class BehaviorTreeData : ScriptableObject
    {
        [SerializeField] private BlackboardData blackboard;
        [SerializeField] private string rootId;
        [SerializeField] private List<BehaviorNodeData> nodes = new List<BehaviorNodeData>();
        public BlackboardData BlackboardData { get => blackboard; }
        internal string RootID { get => rootId; set => rootId = value; }
        internal IReadOnlyList<BehaviorNodeData> Nodes { get => nodes; }

        public BehaviorTreeController CreateController(IBehaviorTreeOwner owner) { return new BehaviorTreeController(this, owner); }

        internal BehaviorNodeData FindNode(string id)
        {
            foreach (BehaviorNodeData node in nodes) if (node.ID == id) return node;
            return null;
        }

        internal BehaviorNodeData AddNode(BehaviorNode behavior, Vector2 position)
        {
            BehaviorNodeData node = new BehaviorNodeData(behavior, position);
            nodes.Add(node);
            return node;
        }

        internal void RemoveNode(string id)
        {
            foreach (BehaviorNodeData node in nodes) node.Children.Remove(id);
            nodes.RemoveAll(node => node.ID == id);
            if (rootId == id) rootId = null;
        }

        internal void Connect(string parentID, string childID)
        {
            if (FindNode(childID) == null) throw new ArgumentException("Child node does not exist.");
            if (string.IsNullOrEmpty(parentID))
            {
                foreach (BehaviorNodeData node in nodes) if (node.Children.Contains(childID)) throw new InvalidOperationException("The root node already has a parent.");
                rootId = childID;
                return;
            }
            BehaviorNodeData parent = FindNode(parentID);
            if (parent == null || parent.Behavior is TaskNodeData) throw new InvalidOperationException("Only composite nodes can have children.");
            if (childID == rootId) throw new InvalidOperationException("The root cannot have a parent.");
            foreach (BehaviorNodeData node in nodes) if (node.Children.Contains(childID)) throw new InvalidOperationException("A node can have only one parent.");
            if (Reaches(childID, parentID, new HashSet<string>())) throw new InvalidOperationException("A behavior tree cannot contain a cycle.");
            parent.Children.Add(childID);
        }

        private bool Reaches(string from, string target, HashSet<string> visited)
        {
            if (from == target) return true;
            if (!visited.Add(from)) return false;
            BehaviorNodeData node = FindNode(from);
            if (node == null) return false;
            foreach (string child in node.Children) if (Reaches(child, target, visited)) return true;
            return false;
        }

        internal List<string> Validate()
        {
            List<string> errors = new List<string>();
            if (blackboard == null) { errors.Add("Assign a Blackboard asset."); return errors; }
            blackboard.Validate(errors);
            Dictionary<string, BehaviorNodeData> lookup = new Dictionary<string, BehaviorNodeData>();
            foreach (BehaviorNodeData node in nodes)
            {
                if (node == null || string.IsNullOrEmpty(node.ID) || lookup.ContainsKey(node.ID)) { errors.Add("Node IDs must be present and unique."); continue; }
                lookup.Add(node.ID, node);
                if (node.Behavior == null) errors.Add($"{node.Name}: missing behavior.");
                else if (node.Behavior is TaskNodeData task)
                {
                    if (node.Children.Count != 0) errors.Add($"{node.Name}: Tasks cannot have children.");
                    if (node.Services.Count != 0) errors.Add($"{node.Name}: attach Services to a composite node.");
                    task.ValidateDefinition(blackboard, errors);
                }
                else if (node.Behavior is SelectorNode || node.Behavior is SequenceNode || node.Behavior is UtilitySelectorNode)
                {
                    if (node.Children.Count == 0) errors.Add($"{node.Name}: composites need children.");
                    if (node.Behavior is UtilitySelectorNode utility)
                    {
                        utility.Validate(errors);
                        foreach (string childID in node.Children)
                        {
                            var child = FindNode(childID);
                            if (child == null) continue;
                            if (child.UtilityScore == null) errors.Add("Utility child has missing score settings.");
                            else child.UtilityScore.Validate(blackboard, errors);
                        }
                    }
                }
                else errors.Add($"{node.Name}: unsupported node type.");
                foreach (Decorator decorator in node.Decorators)
                {
                    if (decorator == null) errors.Add($"{node.Name}: missing decorator.");
                    else
                    {
                        if (!Enum.IsDefined(typeof(BehaviorAbortMode), decorator.AbortMode)) errors.Add("Invalid observer abort mode.");
                        decorator.ValidateDefinition(blackboard, errors);
                    }
                }
                foreach (Service service in node.Services)
                { if (service == null) errors.Add($"{node.Name}: missing service."); else service.ValidateDefinition(blackboard, errors); }
            }
            if (string.IsNullOrEmpty(rootId) || !lookup.ContainsKey(rootId)) errors.Add("Connect exactly one root node.");
            HashSet<string> parented = new HashSet<string>();
            foreach (BehaviorNodeData node in lookup.Values)
                foreach (string child in node.Children)
                {
                    if (string.IsNullOrEmpty(child) || !lookup.ContainsKey(child)) errors.Add($"{node.Name}: missing child node.");
                    else if (!parented.Add(child) || child == rootId) errors.Add("A node cannot have multiple parents or parent the root.");
                }
            Dictionary<string, int> colors = new Dictionary<string, int>();
            foreach (string id in lookup.Keys) Visit(id, lookup, colors, errors);
            HashSet<string> reachable = new HashSet<string>();
            CollectReachable(rootId, lookup, reachable);
            if (reachable.Count != lookup.Count) errors.Add("Every node must be connected to the root.");
            return errors;
        }

        private static void Visit(string id, Dictionary<string, BehaviorNodeData> lookup, Dictionary<string, int> colors, List<string> errors)
        {
            if (colors.TryGetValue(id, out int color)) { if (color == 1) errors.Add("Tree contains a cycle."); return; }
            colors[id] = 1;
            foreach (string child in lookup[id].Children) if (!string.IsNullOrEmpty(child) && lookup.ContainsKey(child)) Visit(child, lookup, colors, errors);
            colors[id] = 2;
        }

        private static void CollectReachable(string id, Dictionary<string, BehaviorNodeData> lookup, HashSet<string> visited)
        {
            if (string.IsNullOrEmpty(id) || !lookup.TryGetValue(id, out BehaviorNodeData node) || !visited.Add(id)) return;
            foreach (string child in node.Children) CollectReachable(child, lookup, visited);
        }
    }
}
