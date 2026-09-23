using System;
using System.Collections.Generic;
using System.Linq;

using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityFramework.BattleSystem.Editor
{
    public sealed class BuffMonitorWindow : EditorWindow
    {
        private const string UXML_PATH =
            "Assets/Plugins/Framework/Editor/BattleSystem/BuffMonitor/BuffMonitorWindow.uxml";
        private const double RUNTIME_REFRESH_INTERVAL = 0.2d;

        private readonly List<BattleComponentListItem> battleComponents = new();
        private readonly List<BuffMonitorSnapshot> snapshots = new();
        private readonly List<BuffEntryView> buffEntryViews = new();

        private ListView battleComponentList;
        private ScrollView attributeScroll;
        private ScrollView buffScroll;
        private Label playModeStatus;
        private Label selectionLabel;
        private Label buffStatus;

        private BattleComponentListItem selectedBattleComponentItem;
        private BattleComponent selectedBattleComponent;
        private SerializedObject selectedBattleComponentObject;
        private double nextRuntimeRefreshTime;
        private bool componentScanRequested = true;

        [MenuItem("UnityFramework/Battle System/Buff Monitor", false, 0)]
        private static void Open()
        {
            BuffMonitorWindow window = GetWindow<BuffMonitorWindow>("Buff Monitor");
            window.minSize = new Vector2(760.0f, 440.0f);
            window.Show();
            window.Focus();
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            Undo.undoRedoPerformed -= OnUndoRedo;

            ClearBuffEntryViews();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UXML_PATH);
            if (visualTree == null)
            {
                rootVisualElement.Add(new HelpBox(
                    $"Buff Monitor layout was not found at {UXML_PATH}.",
                    HelpBoxMessageType.Error));
                return;
            }

            visualTree.CloneTree(rootVisualElement);
            BuildLayout(rootVisualElement.Q<VisualElement>("windowRoot"));
            SyncBattleComponents();
            RefreshSelection();
        }

        private void BuildLayout(VisualElement root)
        {
            VisualElement header = new VisualElement();
            header.AddToClassList("buff-monitor-header");

            playModeStatus = new Label();
            playModeStatus.AddToClassList("buff-monitor-play-status");
            header.Add(playModeStatus);

            Button refreshButton = new Button(RefreshBattleComponents)
            {
                text = "Refresh",
                tooltip = "Rescan the loaded scenes for BattleComponents.",
            };
            refreshButton.AddToClassList("buff-monitor-refresh-button");
            header.Add(refreshButton);
            root.Add(header);

            TwoPaneSplitView outerSplit = new TwoPaneSplitView(
                0,
                230.0f,
                TwoPaneSplitViewOrientation.Horizontal);
            outerSplit.AddToClassList("buff-monitor-split");

            VisualElement componentPane = CreatePane("Battle Components");
            battleComponentList = new ListView
            {
                fixedItemHeight = 22.0f,
                selectionType = SelectionType.Single,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                makeItem = () => new Label(),
                bindItem = BindBattleComponentItem,
                itemsSource = battleComponents,
            };
            battleComponentList.AddToClassList("buff-monitor-component-list");
            battleComponentList.selectionChanged += OnBattleComponentSelectionChanged;
            componentPane.Add(battleComponentList);
            outerSplit.Add(componentPane);

            TwoPaneSplitView detailSplit = new TwoPaneSplitView(
                0,
                220.0f,
                TwoPaneSplitViewOrientation.Vertical);
            detailSplit.AddToClassList("buff-monitor-split");

            VisualElement attributePane = CreatePane("Battle Attribute Set");
            selectionLabel = new Label("No BattleComponent selected.");
            selectionLabel.AddToClassList("buff-monitor-selection");
            attributePane.Add(selectionLabel);
            attributeScroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal)
            {
                horizontalScrollerVisibility = ScrollerVisibility.Auto,
                verticalScrollerVisibility = ScrollerVisibility.Auto,
            };
            attributeScroll.AddToClassList("buff-monitor-scroll");
            attributePane.Add(attributeScroll);
            detailSplit.Add(attributePane);

            VisualElement buffPane = CreatePane("Buffs");
            buffStatus = new Label();
            buffStatus.AddToClassList("buff-monitor-status");
            buffPane.Add(buffStatus);
            buffScroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal)
            {
                horizontalScrollerVisibility = ScrollerVisibility.Auto,
                verticalScrollerVisibility = ScrollerVisibility.Auto,
            };
            buffScroll.AddToClassList("buff-monitor-scroll");
            buffPane.Add(buffScroll);
            detailSplit.Add(buffPane);

            outerSplit.Add(detailSplit);
            root.Add(outerSplit);
        }

        private static VisualElement CreatePane(string title)
        {
            VisualElement pane = new VisualElement();
            pane.AddToClassList("buff-monitor-pane");

            Label titleLabel = new Label(title);
            titleLabel.AddToClassList("buff-monitor-title");
            pane.Add(titleLabel);
            return pane;
        }

        private void OnEditorUpdate()
        {
            double now = EditorApplication.timeSinceStartup;
            if (componentScanRequested)
            {
                componentScanRequested = false;
                SyncBattleComponents();
            }

            if (now >= nextRuntimeRefreshTime)
            {
                nextRuntimeRefreshTime = now + RUNTIME_REFRESH_INTERVAL;
                RefreshRuntimeValues();
            }
        }

        private void OnHierarchyChanged()
        {
            componentScanRequested = true;
        }

        private void RefreshBattleComponents()
        {
            componentScanRequested = false;
            SyncBattleComponents();
            RefreshRuntimeValues();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            componentScanRequested = true;
            nextRuntimeRefreshTime = 0.0d;

            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                List<BattleComponent> liveComponents = battleComponents
                    .Select(item => item.Component)
                    .Where(component => component != null)
                    .ToList();
                BuffMonitorSessionCache.instance.Capture(liveComponents);
                return;
            }

            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                BuffMonitorSessionCache.instance.ClearCache();
                SelectBattleComponentItem(null);
                battleComponents.Clear();
                battleComponentList?.Rebuild();
                return;
            }

            if (state == PlayModeStateChange.EnteredEditMode)
                LoadCachedComponents();
        }

        private void OnUndoRedo()
        {
            RefreshSelection();
        }

        private void SyncBattleComponents()
        {
            if (battleComponentList == null)
                return;

            playModeStatus.text = EditorApplication.isPlaying
                ? "PLAY MODE - LIVE"
                : BuffMonitorSessionCache.instance.HasData
                    ? "LAST PLAY MODE SNAPSHOT"
                    : "Enter Play Mode to inspect BattleComponents.";

            if (!EditorApplication.isPlaying)
            {
                LoadCachedComponents();
                return;
            }

            BattleComponent[] found = Resources.FindObjectsOfTypeAll<BattleComponent>()
                .Where(component =>
                    component != null &&
                    !EditorUtility.IsPersistent(component) &&
                    component.gameObject.scene.IsValid() &&
                    component.gameObject.scene.isLoaded)
                .OrderBy(component => component.gameObject.name, StringComparer.Ordinal)
                .ThenBy(component => component.GetInstanceID())
                .ToArray();

            if (HasSameComponents(found))
                return;

            int selectedInstanceId = selectedBattleComponent == null
                ? 0
                : selectedBattleComponent.GetInstanceID();
            battleComponents.Clear();
            for (int i = 0; i < found.Length; i++)
            {
                battleComponents.Add(new BattleComponentListItem(
                    found[i],
                    found[i].gameObject.name,
                    GetHierarchyPath(found[i].transform)));
            }
            battleComponentList.Rebuild();

            int selectedIndex = battleComponents.FindIndex(item =>
                item.Component != null && item.Component.GetInstanceID() == selectedInstanceId);
            if (selectedIndex >= 0)
                battleComponentList.SetSelection(selectedIndex);
            else
                SelectBattleComponentItem(null);
        }

        private bool HasSameComponents(IReadOnlyList<BattleComponent> found)
        {
            if (found.Count != battleComponents.Count)
                return false;

            for (int i = 0; i < found.Count; i++)
            {
                if (found[i] != battleComponents[i].Component)
                    return false;
            }

            return true;
        }

        private void LoadCachedComponents()
        {
            if (battleComponentList == null)
                return;

            IReadOnlyList<BuffMonitorCachedComponent> cachedComponents =
                BuffMonitorSessionCache.instance.Components;
            playModeStatus.text = cachedComponents.Count > 0
                ? "LAST PLAY MODE SNAPSHOT"
                : "Enter Play Mode to inspect BattleComponents.";
            string selectedPath = selectedBattleComponentItem?.HierarchyPath;

            battleComponents.Clear();
            for (int i = 0; i < cachedComponents.Count; i++)
            {
                BuffMonitorCachedComponent cached = cachedComponents[i];
                battleComponents.Add(new BattleComponentListItem(
                    cached,
                    cached.Name,
                    cached.HierarchyPath));
            }

            battleComponentList.Rebuild();
            int selectedIndex = string.IsNullOrEmpty(selectedPath)
                ? -1
                : battleComponents.FindIndex(item => item.HierarchyPath == selectedPath);
            if (selectedIndex >= 0)
                battleComponentList.SetSelection(selectedIndex);
            else if (battleComponents.Count > 0)
                battleComponentList.SetSelection(0);
            else
                SelectBattleComponentItem(null);
        }

        private void BindBattleComponentItem(VisualElement element, int index)
        {
            Label label = (Label)element;
            BattleComponentListItem item = battleComponents[index];
            label.text = item.Name;
            label.tooltip = item.HierarchyPath;
            label.AddToClassList("buff-monitor-component-item");
        }

        private static string GetHierarchyPath(Transform transform)
        {
            string path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = $"{transform.name}/{path}";
            }

            return path;
        }

        private void OnBattleComponentSelectionChanged(IEnumerable<object> selection)
        {
            SelectBattleComponentItem(selection.OfType<BattleComponentListItem>().FirstOrDefault());
        }

        private void SelectBattleComponentItem(BattleComponentListItem item)
        {
            if (ReferenceEquals(selectedBattleComponentItem, item))
                return;

            selectedBattleComponentItem = item;
            selectedBattleComponent = item?.Component;
            selectedBattleComponentObject = selectedBattleComponent == null
                ? null
                : new SerializedObject(selectedBattleComponent);
            RefreshSelection();
        }

        private void RefreshSelection()
        {
            if (attributeScroll == null || buffScroll == null)
                return;

            attributeScroll.Clear();
            if (selectedBattleComponentItem == null)
            {
                selectionLabel.text = "No BattleComponent selected.";
                ClearBuffEntryViews();
                buffStatus.text = "Select a BattleComponent.";
                return;
            }

            selectionLabel.text = selectedBattleComponentItem.HierarchyPath;
            if (selectedBattleComponentItem.CachedComponent != null)
            {
                DrawCachedFields(
                    attributeScroll,
                    selectedBattleComponentItem.CachedComponent.AttributeFields);
                DrawCachedBuffs(selectedBattleComponentItem.CachedComponent.Buffs);
                return;
            }

            selectedBattleComponentObject ??= new SerializedObject(selectedBattleComponent);
            selectedBattleComponentObject.UpdateIfRequiredOrScript();
            SerializedProperty attributeProperty =
                selectedBattleComponentObject.FindProperty("battleAttributeSet");
            if (attributeProperty == null)
            {
                attributeScroll.Add(new HelpBox(
                    "The serialized BattleAttributeSet field was not found.",
                    HelpBoxMessageType.Warning));
            }
            else
            {
                PropertyField attributeField = new PropertyField(
                    attributeProperty,
                    selectedBattleComponent.BattleAttributeSet?.GetType().Name ??
                    "Battle Attribute Set");
                attributeField.Bind(selectedBattleComponentObject);
                attributeScroll.Add(attributeField);
            }

            RefreshBuffList(true);
        }

        private void RefreshRuntimeValues()
        {
            if (selectedBattleComponentObject != null && selectedBattleComponent != null)
                selectedBattleComponentObject.UpdateIfRequiredOrScript();

            if (EditorApplication.isPlaying)
                RefreshBuffList(false);
        }

        private void RefreshBuffList(bool forceRebuild)
        {
            if (buffScroll == null)
                return;

            if (selectedBattleComponent == null)
            {
                if (buffEntryViews.Count > 0)
                    ClearBuffEntryViews();
                return;
            }

            if (!BuffMonitorReflection.TryCapture(
                selectedBattleComponent,
                snapshots,
                out string error))
            {
                ClearBuffEntryViews();
                buffStatus.text = $"Unable to read buffs: {error}";
                return;
            }

            bool structureChanged = forceRebuild || buffEntryViews.Count != snapshots.Count;
            if (!structureChanged)
            {
                for (int i = 0; i < snapshots.Count; i++)
                {
                    if (buffEntryViews[i].BuffId != snapshots[i].BuffId)
                    {
                        structureChanged = true;
                        break;
                    }
                }
            }

            if (structureChanged)
            {
                ClearBuffEntryViews();
                for (int i = 0; i < snapshots.Count; i++)
                {
                    BuffEntryView view = new BuffEntryView(snapshots[i]);
                    buffEntryViews.Add(view);
                    buffScroll.Add(view.Root);
                }
            }
            else
            {
                for (int i = 0; i < snapshots.Count; i++)
                    buffEntryViews[i].Update(snapshots[i]);
            }

            buffStatus.text = snapshots.Count == 0
                ? "No active buffs."
                : $"Active Buffs: {snapshots.Count}";
        }

        private void DrawCachedBuffs(IReadOnlyList<BuffMonitorCachedBuff> cachedBuffs)
        {
            ClearBuffEntryViews();
            for (int i = 0; i < cachedBuffs.Count; i++)
            {
                BuffMonitorCachedBuff cachedBuff = cachedBuffs[i];
                Foldout root = new Foldout
                {
                    text = $"Buff ID: {cachedBuff.BuffId}",
                };
                root.AddToClassList("buff-monitor-entry");

                Label stackLabel = new Label(
                    $"Stack: {cachedBuff.Stack} / {cachedBuff.MaxStack}");
                stackLabel.AddToClassList("buff-monitor-stack");
                root.Add(stackLabel);

                Foldout dataFoldout = new Foldout
                {
                    text = $"Buff Data ({cachedBuff.DataTypeName})",
                    value = true,
                };
                VisualElement dataContainer = new VisualElement();
                dataContainer.AddToClassList("buff-monitor-data-container");
                DrawCachedFields(dataContainer, cachedBuff.DataFields);
                dataFoldout.Add(dataContainer);
                root.Add(dataFoldout);

                Foldout modifiersFoldout = new Foldout
                {
                    text = $"Buff Modifiers ({cachedBuff.ModifierNames.Count})",
                };
                VisualElement modifierContainer = new VisualElement();
                modifierContainer.AddToClassList("buff-monitor-modifier-container");
                if (cachedBuff.ModifierNames.Count == 0)
                {
                    modifierContainer.Add(new Label("None"));
                }
                else
                {
                    for (int j = 0; j < cachedBuff.ModifierNames.Count; j++)
                        modifierContainer.Add(new Label(cachedBuff.ModifierNames[j]));
                }
                modifiersFoldout.Add(modifierContainer);
                root.Add(modifiersFoldout);
                buffScroll.Add(root);
            }

            buffStatus.text = cachedBuffs.Count == 0
                ? "No active buffs in the last snapshot."
                : $"Cached Buffs: {cachedBuffs.Count}";
        }

        private static void DrawCachedFields(
            VisualElement container,
            IReadOnlyList<BuffMonitorCachedField> fields)
        {
            if (fields.Count == 0)
            {
                container.Add(new Label("No serialized values."));
                return;
            }

            for (int i = 0; i < fields.Count; i++)
            {
                BuffMonitorCachedField field = fields[i];
                string value = string.IsNullOrEmpty(field.Value)
                    ? field.Name
                    : $"{field.Name}: {field.Value}";
                Label label = new Label(value);
                label.style.marginLeft = field.Depth * 14.0f;
                label.AddToClassList("buff-monitor-cached-field");
                container.Add(label);
            }
        }

        private void ClearBuffEntryViews()
        {
            for (int i = 0; i < buffEntryViews.Count; i++)
                buffEntryViews[i].Dispose();

            buffEntryViews.Clear();
            buffScroll?.Clear();
        }

        private sealed class BattleComponentListItem
        {
            internal BattleComponent Component { get; }
            internal BuffMonitorCachedComponent CachedComponent { get; }
            internal string Name { get; }
            internal string HierarchyPath { get; }

            internal BattleComponentListItem(
                BattleComponent component,
                string name,
                string hierarchyPath)
            {
                Component = component;
                Name = name;
                HierarchyPath = hierarchyPath;
            }

            internal BattleComponentListItem(
                BuffMonitorCachedComponent cachedComponent,
                string name,
                string hierarchyPath)
            {
                CachedComponent = cachedComponent;
                Name = name;
                HierarchyPath = hierarchyPath;
            }
        }

        private sealed class BuffEntryView : IDisposable
        {
            internal int BuffId { get; private set; }
            internal Foldout Root { get; }

            private readonly Label stackLabel;
            private readonly Foldout dataFoldout;
            private readonly Foldout modifiersFoldout;
            private readonly VisualElement dataContainer;
            private readonly VisualElement modifierContainer;

            private BuffDataViewProxy dataProxy;
            private SerializedObject dataObject;
            private BuffData displayedData;
            private string modifierSignature;

            internal BuffEntryView(BuffMonitorSnapshot snapshot)
            {
                Root = new Foldout();
                Root.AddToClassList("buff-monitor-entry");

                stackLabel = new Label();
                stackLabel.AddToClassList("buff-monitor-stack");
                Root.Add(stackLabel);

                dataFoldout = new Foldout { text = "Buff Data", value = true };
                dataContainer = new VisualElement();
                dataContainer.AddToClassList("buff-monitor-data-container");
                dataFoldout.Add(dataContainer);
                Root.Add(dataFoldout);

                modifiersFoldout = new Foldout { text = "Buff Modifiers", value = false };
                modifierContainer = new VisualElement();
                modifierContainer.AddToClassList("buff-monitor-modifier-container");
                modifiersFoldout.Add(modifierContainer);
                Root.Add(modifiersFoldout);

                Update(snapshot);
            }

            internal void Update(BuffMonitorSnapshot snapshot)
            {
                BuffId = snapshot.BuffId;
                Root.text = $"Buff ID: {snapshot.BuffId}";
                stackLabel.text = $"Stack: {snapshot.Stack} / {snapshot.MaxStack}";

                if (!ReferenceEquals(displayedData, snapshot.BuffData))
                    BuildDataView(snapshot.BuffData);
                else
                    dataObject?.UpdateIfRequiredOrScript();

                string signature = string.Join("\n", snapshot.ModifierNames);
                if (!string.Equals(signature, modifierSignature, StringComparison.Ordinal))
                {
                    modifierSignature = signature;
                    modifierContainer.Clear();
                    if (snapshot.ModifierNames.Count == 0)
                    {
                        modifierContainer.Add(new Label("None"));
                    }
                    else
                    {
                        for (int i = 0; i < snapshot.ModifierNames.Count; i++)
                            modifierContainer.Add(new Label(snapshot.ModifierNames[i]));
                    }
                }

                modifiersFoldout.text = $"Buff Modifiers ({snapshot.ModifierNames.Count})";
            }

            private void BuildDataView(BuffData buffData)
            {
                DestroyDataProxy();
                displayedData = buffData;
                dataContainer.Clear();

                if (buffData == null)
                {
                    dataContainer.Add(new Label("Null"));
                    return;
                }

                dataProxy = CreateInstance<BuffDataViewProxy>();
                dataProxy.hideFlags = HideFlags.HideAndDontSave;
                dataProxy.SetData(buffData);
                dataObject = new SerializedObject(dataProxy);
                SerializedProperty dataProperty = dataObject.FindProperty("data");
                PropertyField dataField = new PropertyField(dataProperty, buffData.GetType().Name);
                dataField.SetEnabled(false);
                dataField.Bind(dataObject);
                dataContainer.Add(dataField);
            }

            public void Dispose()
            {
                DestroyDataProxy();
            }

            private void DestroyDataProxy()
            {
                dataObject = null;
                if (dataProxy != null)
                    DestroyImmediate(dataProxy);
                dataProxy = null;
                displayedData = null;
            }
        }

        private sealed class BuffDataViewProxy : ScriptableObject
        {
            [SerializeReference] private BuffData data;

            internal void SetData(BuffData value)
            {
                data = value;
            }
        }
    }
}
