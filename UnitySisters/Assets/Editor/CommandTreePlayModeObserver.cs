using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityFramework.CommandTree;
using UnityFramework.CommandTree.Editor;

[InitializeOnLoad]
internal static class CommandTreePlayModeObserver
{
    private const BindingFlags privateFields = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo characterField = typeof(PlayerController).GetField("currentControlCharacter", privateFields);
    private static readonly FieldInfo observerField = typeof(PlayerController).GetField("commandTraceObserver", privateFields);
    private static readonly Action<ICommandTreeNode, ICommandBehavior, bool> observer = OnTrace;
    private static readonly Dictionary<ICommandTreeNode, string> nodePaths = new Dictionary<ICommandTreeNode, string>();
    private static readonly List<string> visitedPaths = new List<string>();
    private static PlayerController controller;
    private static Character character;
    private static CommandData data;
    private static CommandTreeEditorWindow window;
    private static ICommandTreeNode root;
    private static string commandPath;
    private static double nextRefreshTime;
    private static bool mappingDirty;

    static CommandTreePlayModeObserver()
    {
        Selection.selectionChanged += OnSelectionChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.update += Update;
        EditorApplication.projectChanged += InvalidateMapping;
        Undo.undoRedoPerformed += InvalidateMapping;
        AssemblyReloadEvents.beforeAssemblyReload += Disconnect;
        EditorApplication.delayCall += OnSelectionChanged;
    }

    private static PlayerController GetSelectedController()
    {
        GameObject selected = Selection.activeGameObject;
        return selected == null || EditorUtility.IsPersistent(selected) ? null : selected.GetComponent<PlayerController>();
    }

    private static void OnSelectionChanged()
    {
        Disconnect();
        if (!EditorApplication.isPlaying)
            return;
        PlayerController selected = GetSelectedController();
        if (selected != null)
            Connect(selected, true);
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
            OnSelectionChanged();
        else if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
            Disconnect();
    }

    private static void InvalidateMapping()
    {
        mappingDirty = true;
    }

    private static void Update()
    {
        if (!EditorApplication.isPlaying || ReferenceEquals(controller, null))
            return;
        if (controller == null || window == null || GetSelectedController() != controller)
        {
            Disconnect();
            return;
        }
        if (EditorApplication.timeSinceStartup < nextRefreshTime)
            return;
        nextRefreshTime = EditorApplication.timeSinceStartup + 0.2;
        Connect(controller, false);
    }

    private static void Connect(PlayerController selected, bool openWindow)
    {
        if (characterField == null || observerField == null)
            return;
        Character selectedCharacter = characterField.GetValue(selected) as Character;
        CommandData selectedData = null;
        if (selectedCharacter != null)
        {
            using (SerializedObject serializedCharacter = new SerializedObject(selectedCharacter))
                selectedData = serializedCharacter.FindProperty("commandData")?.objectReferenceValue as CommandData;
        }
        if (!openWindow && character == selectedCharacter && data == selectedData && !mappingDirty)
            return;

        Disconnect();
        controller = selected;
        character = selectedCharacter;
        data = selectedData;
        window = openWindow ? EditorWindow.GetWindow<CommandTreeEditorWindow>("Command Tree Editor", false) : window;
        if (window == null)
            return;
        window.minSize = new Vector2(760, 420);
        window.ShowObservedData(data, selected.name);
        mappingDirty = false;
        if (character == null || data == null)
            return;

        root = character.CommandTree.Root;
        MapNodes(root, "commandTree.root");
        observerField.SetValue(controller, observer);
    }

    private static void MapNodes(ICommandTreeNode node, string path)
    {
        nodePaths.Add(node, path);
        IEnumerable children = node.GetType().GetField("children", BindingFlags.Instance | BindingFlags.Public)?.GetValue(node) as IEnumerable;
        if (children == null)
            return;
        int index = 0;
        foreach (ICommandTreeNode child in children)
            MapNodes(child, $"{path}.children.Array.data[{index++}]");
    }

    private static void OnTrace(ICommandTreeNode node, ICommandBehavior behavior, bool completed)
    {
        if (window == null || controller == null)
            return;
        if (!completed && node != null && !nodePaths.ContainsKey(node))
        {
            mappingDirty = true;
            Connect(controller, false);
        }
        if (!completed && ReferenceEquals(node, root))
        {
            visitedPaths.Clear();
            commandPath = null;
        }
        else if (!completed && node != null && nodePaths.TryGetValue(node, out string path))
        {
            visitedPaths.Add(path);
            if (node.CommandBehavior != null)
                commandPath = path;
        }
        if (!completed)
            return;
        string result = behavior == null ? "No matching command" : $"Matched: {behavior.GetType().Name} (execution permission is decided by the character/FSM)";
        window.ShowRuntimeTrace(data, visitedPaths, commandPath, $"Observing: {controller.name} | {result}");
    }

    private static void Disconnect()
    {
        if (controller != null && observerField != null && Equals(observerField.GetValue(controller), observer))
            observerField.SetValue(controller, null);
        if (window != null)
            window.ClearRuntimeTrace();
        controller = null;
        character = null;
        data = null;
        root = null;
        commandPath = null;
        visitedPaths.Clear();
        nodePaths.Clear();
    }
}
