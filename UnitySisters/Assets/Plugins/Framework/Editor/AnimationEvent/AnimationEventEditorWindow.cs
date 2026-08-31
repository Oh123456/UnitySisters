#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using UnityEditor;
using UnityEditor.Animations;

using UnityEngine;

using UnityFramework.Animation;

public sealed class AnimationEventEditorWindow : EditorWindow
{
    private const string EventsPropertyName = "events";
    private const string EventTypePropertyName = "evetType";
    private const string StartTimePropertyName = "startTime";
    private const string EndTimePropertyName = "endTime";
    private const string EditorEventNamePropertyName = "editorEventName";
    private const int MaxEventCount = 32;
    private const float InspectorWidth = 300.0f;
    private const float ToolbarHeight = 24.0f;
    private const float MinPreviewHeight = 160.0f;
    private const float SplitterHeight = 5.0f;
    private const float ScrubberHeight = 54.0f;
    private const float HeaderWidth = 92.0f;
    private const float MarkerSize = 12.0f;
    private const float HandleWidth = 6.0f;
    private const float MinContinuousPixelWidth = 18.0f;
    private const float BaseTrackRowHeight = 30.0f;
    private const float TrackPadding = 8.0f;
    private const float MinZoomRange = 0.05f;
    private const string PreviewPrefabPrefsKeyPrefix = "UnityFramework.AnimationEventEditor.PreviewPrefab";
    private const string BottomPanelHeightPrefsKeyPrefix = "UnityFramework.AnimationEventEditor.BottomPanelHeight";

    private static readonly Color PreviewBackground = new Color(0.14f, 0.14f, 0.14f);
    private static readonly Color TrackBackground = new Color(0.11f, 0.11f, 0.11f);
    private static readonly Color TrackGrid = new Color(0.28f, 0.28f, 0.28f);
    private static readonly Color TriggerColor = new Color(0.42f, 0.72f, 1.0f);
    private static readonly Color ContinuousColor = new Color(0.34f, 0.74f, 0.48f);
    private static readonly Color SelectedColor = new Color(1.0f, 0.68f, 0.18f);
    private static readonly Color WarningColor = new Color(1.0f, 0.36f, 0.24f);

    private AnimationEventData eventData;
    private SerializedObject serializedEventData;
    private SerializedProperty eventsProperty;
    private AnimationClip previewClip;
    private GameObject previewPrefab;
    private GameObject previewInstance;
    private PreviewRenderUtility previewUtility;
    private Bounds previewFramingBounds = new Bounds(Vector3.zero, Vector3.one);
    private bool hasPreviewFramingBounds;
    private Vector3 previewRootPosition;
    private Quaternion previewRootRotation = Quaternion.identity;
    private Vector3 previewRootScale = Vector3.one;
    private Vector2 inspectorScroll;
    private int selectedEventIndex = -1;
    private int draggingEventIndex = -1;
    private DragMode dragMode = DragMode.None;
    private bool isPanning;
    private bool isResizingBottomPanel;
    private float panMouseStartX;
    private float panStartVisibleStart;
    private float panStartVisibleEnd;
    private float resizeStartMouseY;
    private float resizeStartBottomPanelHeight;
    private float dragStartMouseTime;
    private float dragOriginalStartTime;
    private float dragOriginalEndTime;
    private float currentTime;
    private float visibleStart;
    private float visibleEnd = 1.0f;
    private float bottomPanelHeight = 180.0f;
    private float previewYaw = 180.0f;
    private float previewPitch = 12.0f;
    private float previewDistanceMultiplier = 2.25f;
    private Vector3 previewPivotOffset;
    private bool isPlaying;
    private bool snapEnabled;
    private SnapMode snapMode = SnapMode.Normalized01;
    private double lastUpdateTime;

    private enum DragMode
    {
        None,
        Trigger,
        ContinuousBody,
        ContinuousStart,
        ContinuousEnd
    }

    private enum SnapMode
    {
        Normalized01,
        Frame
    }

    private readonly struct EventLayout
    {
        public readonly int Index;
        public readonly int Row;

        public EventLayout(int index, int row)
        {
            Index = index;
            Row = row;
        }
    }

    public static void Open(AnimationEventData target)
    {
        Open(target, null);
    }

    public static void Open(AnimationEventData target, AnimationClip clip)
    {
        AnimationEventEditorWindow window = GetWindow<AnimationEventEditorWindow>("Animation Event Editor");
        window.SetTarget(target, clip);
        window.Show();
        window.Focus();
    }

    [MenuItem("UnityFramework/Animation Event/Editor", false, 0)]
    public static void OpenFromTools()
    {
        AnimationEventData selectedData = Selection.activeObject as AnimationEventData;
        AnimationClip selectedClip = Selection.activeObject as AnimationClip;

        if (selectedData == null && Selection.activeObject is AnimationEventBehaviour behaviour)
        {
            selectedData = GetEventData(behaviour);
            selectedClip = FindOwnerStateClip(behaviour);
        }

        Open(selectedData, selectedClip);
    }

    private void SetTarget(AnimationEventData target, AnimationClip clip)
    {
        eventData = target;
        serializedEventData = target == null ? null : new SerializedObject(target);
        eventsProperty = serializedEventData?.FindProperty(EventsPropertyName);
        if (clip != null)
            previewClip = clip;
        LoadPreviewDefaults();
        LoadLayoutDefaults();
        selectedEventIndex = -1;
        draggingEventIndex = -1;
        dragMode = DragMode.None;
        RebuildPreviewInstance();
        SamplePreviewPose();
        Repaint();
    }

    private void OnEnable()
    {
        EditorApplication.update += EditorUpdate;
        EnsurePreviewUtility();
    }

    private void OnDisable()
    {
        EditorApplication.update -= EditorUpdate;
        SortAndSave();
        SaveLayoutDefaults();
        CleanupPreview();
    }

    private void EditorUpdate()
    {
        if (!isPlaying)
            return;

        double now = EditorApplication.timeSinceStartup;
        float deltaTime = Mathf.Max(0.0f, (float)(now - lastUpdateTime));
        lastUpdateTime = now;

        float normalizedDelta = previewClip == null || previewClip.length <= 0.0f ? deltaTime : deltaTime / previewClip.length;
        currentTime += normalizedDelta;
        if (currentTime > 1.0f)
            currentTime -= Mathf.Floor(currentTime);

        SamplePreviewPose();
        Repaint();
    }

    private void OnGUI()
    {
        if (eventData == null)
        {
            DrawNoTargetGUI();
            return;
        }

        serializedEventData.Update();

        Rect toolbarRect = new Rect(0.0f, 0.0f, position.width, ToolbarHeight);
        Rect leftRect = new Rect(0.0f, ToolbarHeight, Mathf.Max(0.0f, position.width - InspectorWidth), position.height - ToolbarHeight);
        Rect inspectorRect = new Rect(leftRect.xMax, ToolbarHeight, InspectorWidth, position.height - ToolbarHeight);

        DrawToolbar(toolbarRect);
        DrawEditor(leftRect);
        DrawInspector(inspectorRect);
        HandleKeyboard();

        if (serializedEventData.ApplyModifiedProperties())
        {
            ValidateAllEvents();
            SortAndSave();
            SamplePreviewPose();
        }
    }

    private void DrawNoTargetGUI()
    {
        GUILayout.Space(8.0f);
        GUILayout.BeginHorizontal();
        GUILayout.Space(8.0f);
        GUILayout.BeginVertical();

        EditorGUILayout.HelpBox("No AnimationEventData selected.", MessageType.Info);

        EditorGUI.BeginChangeCheck();
        AnimationEventData newEventData = (AnimationEventData)EditorGUILayout.ObjectField("Data", null, typeof(AnimationEventData), false);
        if (EditorGUI.EndChangeCheck() && newEventData != null)
            SetTarget(newEventData, previewClip);

        EditorGUILayout.Space(4.0f);
        using (new EditorGUI.DisabledScope(Selection.activeObject == null))
        {
            if (GUILayout.Button("Use Current Selection"))
                OpenFromTools();
        }

        GUILayout.EndVertical();
        GUILayout.Space(8.0f);
        GUILayout.EndHorizontal();
    }

    private void DrawToolbar(Rect rect)
    {
        GUILayout.BeginArea(rect, EditorStyles.toolbar);
        GUILayout.BeginHorizontal();

        if (GUILayout.Button(isPlaying ? "Pause" : "Play", EditorStyles.toolbarButton, GUILayout.Width(52.0f)))
        {
            isPlaying = !isPlaying;
            lastUpdateTime = EditorApplication.timeSinceStartup;
        }

        if (GUILayout.Button("Stop", EditorStyles.toolbarButton, GUILayout.Width(45.0f)))
        {
            isPlaying = false;
            currentTime = 0.0f;
            SamplePreviewPose();
        }

        GUILayout.Label($"Current : {currentTime:0.000} / 1.0", GUILayout.Width(140.0f));
        GUILayout.Label($"Time : {GetCurrentSeconds():0.000}s", GUILayout.Width(88.0f));

        snapEnabled = GUILayout.Toggle(snapEnabled, "Snap", EditorStyles.toolbarButton, GUILayout.Width(52.0f));
        using (new EditorGUI.DisabledScope(!snapEnabled))
            snapMode = (SnapMode)EditorGUILayout.EnumPopup(snapMode, EditorStyles.toolbarPopup, GUILayout.Width(96.0f));

        if (GUILayout.Button("Frame All", EditorStyles.toolbarButton, GUILayout.Width(72.0f)))
        {
            visibleStart = 0.0f;
            visibleEnd = 1.0f;
        }

        GUILayout.FlexibleSpace();

        DrawValidationSummary();

        if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(45.0f)))
        {
            ValidateAllEvents();
            SortAndSave();
        }

        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    private void DrawEditor(Rect rect)
    {
        int triggerRows = Mathf.Max(1, CalculateRows(AnimationEventCommandType.Trigger));
        int continuousRows = Mathf.Max(1, CalculateRows(AnimationEventCommandType.Continuous));
        float triggerHeight = TrackPadding * 2.0f + BaseTrackRowHeight * triggerRows;
        float continuousHeight = TrackPadding * 2.0f + BaseTrackRowHeight * continuousRows;
        float minimumBottomHeight = ScrubberHeight + triggerHeight + continuousHeight;
        float maxBottomHeight = Mathf.Max(minimumBottomHeight, rect.height - MinPreviewHeight - SplitterHeight);
        bottomPanelHeight = Mathf.Clamp(bottomPanelHeight, minimumBottomHeight, maxBottomHeight);
        float previewHeight = Mathf.Max(MinPreviewHeight, rect.height - bottomPanelHeight - SplitterHeight);

        Rect previewRect = new Rect(rect.x, rect.y, rect.width, previewHeight);
        Rect splitterRect = new Rect(rect.x, previewRect.yMax, rect.width, SplitterHeight);
        Rect scrubberRect = new Rect(rect.x, splitterRect.yMax, rect.width, ScrubberHeight);
        Rect triggerRect = new Rect(rect.x, scrubberRect.yMax, rect.width, triggerHeight);
        Rect continuousRect = new Rect(rect.x, triggerRect.yMax, rect.width, continuousHeight);

        DrawPreview(previewRect);
        DrawBottomSplitter(splitterRect, rect, minimumBottomHeight);
        DrawScrubber(scrubberRect);
        DrawTrack(triggerRect, AnimationEventCommandType.Trigger);
        DrawTrack(continuousRect, AnimationEventCommandType.Continuous);
    }

    private void DrawBottomSplitter(Rect splitterRect, Rect editorRect, float minimumBottomHeight)
    {
        EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeVertical);
        EditorGUI.DrawRect(splitterRect, new Color(0.08f, 0.08f, 0.08f));

        Event current = Event.current;
        if (current.type == EventType.MouseDown && current.button == 0 && splitterRect.Contains(current.mousePosition))
        {
            isResizingBottomPanel = true;
            resizeStartMouseY = current.mousePosition.y;
            resizeStartBottomPanelHeight = bottomPanelHeight;
            current.Use();
        }

        if (current.type == EventType.MouseDrag && isResizingBottomPanel)
        {
            float delta = resizeStartMouseY - current.mousePosition.y;
            float maxBottomHeight = Mathf.Max(minimumBottomHeight, editorRect.height - MinPreviewHeight - SplitterHeight);
            bottomPanelHeight = Mathf.Clamp(resizeStartBottomPanelHeight + delta, minimumBottomHeight, maxBottomHeight);
            current.Use();
            Repaint();
        }

        if (current.type == EventType.MouseUp && isResizingBottomPanel)
        {
            isResizingBottomPanel = false;
            SaveLayoutDefaults();
            current.Use();
        }
    }

    private void DrawPreview(Rect rect)
    {
        EditorGUI.DrawRect(rect, PreviewBackground);

        Rect controlsRect = new Rect(rect.x + 8.0f, rect.y + 8.0f, rect.width - 16.0f, 42.0f);
        GUILayout.BeginArea(controlsRect);
        EditorGUI.BeginChangeCheck();
        previewClip = (AnimationClip)EditorGUILayout.ObjectField("Clip", previewClip, typeof(AnimationClip), false);
        previewPrefab = (GameObject)EditorGUILayout.ObjectField("Preview Prefab", previewPrefab, typeof(GameObject), false);
        if (EditorGUI.EndChangeCheck())
        {
            SavePreviewPrefabDefault();
            RebuildPreviewInstance();
            currentTime = Mathf.Clamp01(currentTime);
            SamplePreviewPose();
        }
        GUILayout.EndArea();

        Rect renderRect = new Rect(rect.x + 8.0f, controlsRect.yMax + 8.0f, rect.width - 16.0f, Mathf.Max(1.0f, rect.height - 64.0f));
        GUI.Box(renderRect, GUIContent.none);
        HandlePreviewCameraInput(renderRect);

        if (previewClip == null || previewPrefab == null)
        {
            GUI.Label(renderRect, "Select an AnimationClip and Preview Prefab.", CenteredMiniLabel());
            return;
        }

        EnsurePreviewUtility();
        EnsurePreviewInstance();
        SamplePreviewPose();

        if (previewUtility == null || previewInstance == null)
        {
            GUI.Label(renderRect, "Preview is not available.", CenteredMiniLabel());
            return;
        }

        Bounds bounds = GetPreviewFramingBounds();
        float size = Mathf.Max(0.1f, bounds.extents.magnitude);
        Quaternion rotation = Quaternion.Euler(previewPitch, previewYaw, 0.0f);
        float distance = size * previewDistanceMultiplier;
        Vector3 pivot = bounds.center + previewPivotOffset;
        Camera camera = previewUtility.camera;
        camera.transform.position = pivot + rotation * new Vector3(0.0f, 0.0f, -distance);
        camera.transform.LookAt(pivot + Vector3.up * size * 0.15f);
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = size * 8.0f;
        camera.fieldOfView = 30.0f;

        previewUtility.lights[0].intensity = 1.2f;
        previewUtility.lights[0].transform.rotation = Quaternion.Euler(35.0f, 35.0f, 0.0f);
        previewUtility.lights[1].intensity = 0.7f;

        previewUtility.BeginPreview(renderRect, GUIStyle.none);
        previewUtility.Render(true);
        Texture texture = previewUtility.EndPreview();
        GUI.DrawTexture(renderRect, texture, ScaleMode.StretchToFill, false);
    }

    private void HandlePreviewCameraInput(Rect renderRect)
    {
        EditorGUIUtility.AddCursorRect(renderRect, MouseCursor.Orbit);

        Event current = Event.current;
        if (current.type == EventType.MouseDown && current.button == 1 && renderRect.Contains(current.mousePosition))
            current.Use();

        if (current.type == EventType.MouseDrag && current.button == 1 && !current.shift && renderRect.Contains(current.mousePosition))
        {
            previewYaw += current.delta.x * 0.45f;
            previewPitch = Mathf.Clamp(previewPitch - current.delta.y * 0.45f, -80.0f, 80.0f);
            current.Use();
            Repaint();
        }

        bool wantsPivotPan = current.button == 2 || (current.button == 0 && current.shift) || (current.button == 1 && current.shift);
        if (current.type == EventType.MouseDrag && wantsPivotPan && renderRect.Contains(current.mousePosition))
        {
            Bounds bounds = GetPreviewFramingBounds();
            float size = Mathf.Max(0.1f, bounds.extents.magnitude);
            float distance = size * previewDistanceMultiplier;
            Quaternion rotation = Quaternion.Euler(previewPitch, previewYaw, 0.0f);
            float panScale = distance * 0.0025f;
            Vector3 right = rotation * Vector3.right;
            Vector3 up = rotation * Vector3.up;
            previewPivotOffset -= right * current.delta.x * panScale;
            previewPivotOffset += up * current.delta.y * panScale;
            current.Use();
            Repaint();
        }

        if (current.type == EventType.ScrollWheel && renderRect.Contains(current.mousePosition))
        {
            previewDistanceMultiplier = Mathf.Clamp(previewDistanceMultiplier + current.delta.y * 0.08f, 0.7f, 8.0f);
            current.Use();
            Repaint();
        }
    }

    private void DrawScrubber(Rect rect)
    {
        Rect timelineRect = GetTimelineRect(rect);
        GUI.Box(rect, GUIContent.none);
        DrawTimelineGrid(timelineRect, rect);

        float currentX = TimeToX(timelineRect, currentTime);
        Handles.color = Color.cyan;
        Handles.DrawLine(new Vector2(currentX, timelineRect.y), new Vector2(currentX, timelineRect.yMax));
        GUI.Label(new Rect(currentX - 30.0f, rect.yMax - 18.0f, 60.0f, 16.0f), $"{currentTime:0.000}", EditorStyles.centeredGreyMiniLabel);

        Event current = Event.current;
        HandleTimelineZoomAndPan(timelineRect, current);

        if ((current.type == EventType.MouseDown || current.type == EventType.MouseDrag) && current.button == 0 && timelineRect.Contains(current.mousePosition) && !current.shift)
        {
            currentTime = SnapTime(XToTime(timelineRect, current.mousePosition.x));
            SamplePreviewPose();
            current.Use();
            Repaint();
        }
    }

    private void DrawTrack(Rect rect, AnimationEventCommandType eventType)
    {
        Rect labelRect = new Rect(rect.x, rect.y, HeaderWidth, rect.height);
        Rect timelineRect = GetTimelineRect(rect);
        string title = eventType == AnimationEventCommandType.Trigger ? "Trigger" : "Continuous";
        Dictionary<int, int> rows = BuildRows(eventType);

        GUI.Box(rect, GUIContent.none);
        GUI.Label(new Rect(labelRect.x + 6.0f, labelRect.y + 6.0f, labelRect.width - 12.0f, 18.0f), title, EditorStyles.boldLabel);
        GUI.Label(new Rect(labelRect.x + 6.0f, labelRect.y + 24.0f, labelRect.width - 12.0f, 16.0f), $"{CountEvents(eventType)} events", EditorStyles.miniLabel);
        EditorGUI.DrawRect(timelineRect, TrackBackground);
        DrawTimelineGrid(timelineRect, rect);

        Event current = Event.current;
        HandleTimelineZoomAndPan(timelineRect, current);

        for (int i = 0; i < eventsProperty.arraySize; i++)
        {
            SerializedProperty element = eventsProperty.GetArrayElementAtIndex(i);
            if (element.managedReferenceValue == null)
                continue;

            if (GetEventType(element) != eventType)
                continue;

            int row = rows.TryGetValue(i, out int rowValue) ? rowValue : 0;
            if (eventType == AnimationEventCommandType.Trigger)
                DrawTriggerEvent(timelineRect, element, i, row);
            else
                DrawContinuousEvent(timelineRect, element, i, row);
        }

        if (current.type == EventType.ContextClick && timelineRect.Contains(current.mousePosition))
        {
            ShowAddMenu(eventType, SnapTime(XToTime(timelineRect, current.mousePosition.x)));
            current.Use();
        }

        if (current.type == EventType.MouseDown && current.button == 0 && timelineRect.Contains(current.mousePosition) && !current.shift)
        {
            selectedEventIndex = -1;
            Repaint();
        }
    }

    private void DrawTimelineGrid(Rect timelineRect, Rect fullRect)
    {
        int tickCount = Mathf.Clamp(Mathf.CeilToInt((visibleEnd - visibleStart) / 0.05f), 4, 20);
        for (int i = 0; i <= tickCount; i++)
        {
            float time = Mathf.Lerp(visibleStart, visibleEnd, i / (float)tickCount);
            float x = TimeToX(timelineRect, time);
            Handles.color = TrackGrid;
            Handles.DrawLine(new Vector2(x, timelineRect.y), new Vector2(x, timelineRect.yMax));
            GUI.Label(new Rect(x - 24.0f, fullRect.y + 4.0f, 48.0f, 18.0f), time.ToString("0.##"), EditorStyles.centeredGreyMiniLabel);
        }
    }

    private void DrawTriggerEvent(Rect timelineRect, SerializedProperty element, int index, int row)
    {
        float startTime = GetStartTime(element);
        float x = TimeToX(timelineRect, startTime);
        float y = timelineRect.y + TrackPadding + row * BaseTrackRowHeight + (BaseTrackRowHeight - MarkerSize) * 0.5f;
        Rect markerRect = new Rect(x - MarkerSize * 0.5f, y, MarkerSize, MarkerSize);
        Event current = Event.current;

        Color color = GetEventWarning(element) == null ? (selectedEventIndex == index ? SelectedColor : TriggerColor) : WarningColor;
        EditorGUI.DrawRect(markerRect, color);
        GUI.Label(new Rect(markerRect.xMax + 3.0f, markerRect.y - 2.0f, 120.0f, 18.0f), GetEventDisplayName(element), EditorStyles.miniLabel);

        if (current.type == EventType.MouseDown && current.button == 0 && markerRect.Contains(current.mousePosition))
        {
            selectedEventIndex = index;
            draggingEventIndex = index;
            dragMode = DragMode.Trigger;
            dragStartMouseTime = XToTime(timelineRect, current.mousePosition.x);
            dragOriginalStartTime = startTime;
            current.Use();
        }

        if (current.type == EventType.ContextClick && markerRect.Contains(current.mousePosition))
        {
            selectedEventIndex = index;
            ShowEventMenu(index);
            current.Use();
        }

        HandleDrag(timelineRect, element, index);
    }

    private void DrawContinuousEvent(Rect timelineRect, SerializedProperty element, int index, int row)
    {
        float startTime = GetStartTime(element);
        float endTime = GetEndTime(element);
        float startX = TimeToX(timelineRect, startTime);
        float endX = TimeToX(timelineRect, endTime);
        float y = timelineRect.y + TrackPadding + row * BaseTrackRowHeight + 5.0f;
        Rect barRect = new Rect(startX, y, Mathf.Max(MinContinuousPixelWidth, endX - startX), 20.0f);
        Rect startHandleRect = new Rect(barRect.x, barRect.y, HandleWidth, barRect.height);
        Rect endHandleRect = new Rect(barRect.xMax - HandleWidth, barRect.y, HandleWidth, barRect.height);
        Event current = Event.current;

        Color color = GetEventWarning(element) == null ? (selectedEventIndex == index ? SelectedColor : ContinuousColor) : WarningColor;
        EditorGUI.DrawRect(barRect, color);
        EditorGUI.DrawRect(startHandleRect, Color.white);
        EditorGUI.DrawRect(endHandleRect, Color.white);
        GUI.Label(new Rect(barRect.x + 8.0f, barRect.y + 1.0f, Mathf.Max(50.0f, barRect.width - 16.0f), 18.0f), GetEventDisplayName(element), EditorStyles.miniLabel);

        if (current.type == EventType.MouseDown && current.button == 0 && barRect.Contains(current.mousePosition))
        {
            selectedEventIndex = index;
            draggingEventIndex = index;
            dragStartMouseTime = XToTime(timelineRect, current.mousePosition.x);
            dragOriginalStartTime = startTime;
            dragOriginalEndTime = endTime;
            dragMode = startHandleRect.Contains(current.mousePosition)
                ? DragMode.ContinuousStart
                : endHandleRect.Contains(current.mousePosition)
                    ? DragMode.ContinuousEnd
                    : DragMode.ContinuousBody;
            current.Use();
        }

        if (current.type == EventType.ContextClick && barRect.Contains(current.mousePosition))
        {
            selectedEventIndex = index;
            ShowEventMenu(index);
            current.Use();
        }

        HandleDrag(timelineRect, element, index);
    }

    private void HandleDrag(Rect timelineRect, SerializedProperty element, int index)
    {
        if (draggingEventIndex != index || dragMode == DragMode.None)
            return;

        Event current = Event.current;
        if (current.type == EventType.MouseDrag && current.button == 0)
        {
            Undo.RecordObject(eventData, "Move Animation Event");
            float mouseTime = XToTime(timelineRect, current.mousePosition.x);
            float deltaTime = mouseTime - dragStartMouseTime;

            if (dragMode == DragMode.Trigger)
            {
                SetStartTime(element, SnapTime(dragOriginalStartTime + deltaTime));
                SetEndTime(element, GetStartTime(element));
            }
            else if (dragMode == DragMode.ContinuousStart)
            {
                SetStartTime(element, SnapTime(Mathf.Clamp(dragOriginalStartTime + deltaTime, 0.0f, GetEndTime(element))));
            }
            else if (dragMode == DragMode.ContinuousEnd)
            {
                SetEndTime(element, SnapTime(Mathf.Clamp(dragOriginalEndTime + deltaTime, GetStartTime(element), 1.0f)));
            }
            else if (dragMode == DragMode.ContinuousBody)
            {
                float duration = Mathf.Max(0.0f, dragOriginalEndTime - dragOriginalStartTime);
                float start = Mathf.Clamp(dragOriginalStartTime + deltaTime, 0.0f, 1.0f - duration);
                start = SnapTime(start);
                SetStartTime(element, start);
                SetEndTime(element, Mathf.Clamp01(start + duration));
            }

            ValidateEvent(element);
            serializedEventData.ApplyModifiedProperties();
            EditorUtility.SetDirty(eventData);
            current.Use();
            Repaint();
        }

        if (current.type == EventType.MouseUp && current.button == 0)
        {
            draggingEventIndex = -1;
            dragMode = DragMode.None;
            ValidateAllEvents();
            SortAndSave();
            current.Use();
            Repaint();
        }
    }

    private void DrawInspector(Rect rect)
    {
        GUI.Box(rect, GUIContent.none);
        GUILayout.BeginArea(new Rect(rect.x + 8.0f, rect.y + 8.0f, rect.width - 16.0f, rect.height - 16.0f));
        inspectorScroll = EditorGUILayout.BeginScrollView(inspectorScroll);

        EditorGUI.BeginChangeCheck();
        AnimationEventData newEventData = (AnimationEventData)EditorGUILayout.ObjectField("Data", eventData, typeof(AnimationEventData), false);
        if (EditorGUI.EndChangeCheck())
        {
            SortAndSave();
            SetTarget(newEventData, previewClip);
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
            return;
        }

        EditorGUILayout.LabelField("Events", $"{eventsProperty.arraySize} / {MaxEventCount}");
        EditorGUILayout.Space(8.0f);

        if (selectedEventIndex < 0 || selectedEventIndex >= eventsProperty.arraySize)
        {
            EditorGUILayout.LabelField("No Event Selected", EditorStyles.boldLabel);
            DrawValidationDetails();
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
            return;
        }

        SerializedProperty element = eventsProperty.GetArrayElementAtIndex(selectedEventIndex);
        if (element.managedReferenceValue == null)
        {
            EditorGUILayout.LabelField("Null Event", EditorStyles.boldLabel);
            DrawValidationDetails();
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
            return;
        }

        EditorGUILayout.LabelField(GetEventDisplayName(element), EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.TextField("Class", element.managedReferenceValue.GetType().FullName);

        EditorGUI.BeginChangeCheck();
        SerializedProperty editorEventNameProperty = element.FindPropertyRelative(EditorEventNamePropertyName);
        if (editorEventNameProperty != null)
            EditorGUILayout.PropertyField(editorEventNameProperty, new GUIContent("Event Name"));

        EditorGUILayout.PropertyField(element.FindPropertyRelative(EventTypePropertyName), new GUIContent("Type"));
        EditorGUILayout.PropertyField(element.FindPropertyRelative(StartTimePropertyName), new GUIContent("Start Time"));
        if (GetEventType(element) == AnimationEventCommandType.Continuous)
            EditorGUILayout.PropertyField(element.FindPropertyRelative(EndTimePropertyName), new GUIContent("End Time"));
        else
            SetEndTime(element, GetStartTime(element));

        if (EditorGUI.EndChangeCheck())
        {
            ValidateEvent(element);
            SamplePreviewPose();
        }

        string warning = GetEventWarning(element);
        if (!string.IsNullOrEmpty(warning))
            EditorGUILayout.HelpBox(warning, MessageType.Warning);

        DrawCommandSpecificFields(element);

        EditorGUILayout.Space(8.0f);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Duplicate"))
            DuplicateEvent(selectedEventIndex);
        if (GUILayout.Button("Delete"))
            DeleteEvent(selectedEventIndex);
        GUILayout.EndHorizontal();

        DrawValidationDetails();

        EditorGUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void DrawCommandSpecificFields(SerializedProperty element)
    {
        SerializedProperty iterator = element.Copy();
        SerializedProperty end = iterator.GetEndProperty();
        bool enterChildren = true;

        EditorGUILayout.Space(6.0f);
        EditorGUILayout.LabelField("Command Fields", EditorStyles.boldLabel);

        bool drewAny = false;
        while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
        {
            enterChildren = false;
            if (iterator.name == EditorEventNamePropertyName || iterator.name == EventTypePropertyName || iterator.name == StartTimePropertyName || iterator.name == EndTimePropertyName)
                continue;

            drewAny = true;
            EditorGUILayout.PropertyField(iterator, true);
        }

        if (!drewAny)
            EditorGUILayout.LabelField("No command specific fields.", EditorStyles.miniLabel);
    }

    private void ShowAddMenu(AnimationEventCommandType eventType, float startTime)
    {
        GenericMenu menu = new GenericMenu();
        if (eventsProperty.arraySize >= MaxEventCount)
        {
            menu.AddDisabledItem(new GUIContent($"Event limit reached ({MaxEventCount})"));
            menu.ShowAsContext();
            return;
        }

        List<Type> types = GetCreatableCommandTypes(eventType);
        if (types.Count == 0)
        {
            menu.AddDisabledItem(new GUIContent("No creatable command types"));
        }
        else
        {
            foreach (Type type in types)
            {
                Type selectedType = type;
                menu.AddItem(new GUIContent(GetMenuPath(type)), false, () => AddEvent(selectedType, eventType, startTime));
            }
        }

        menu.ShowAsContext();
    }

    private void ShowEventMenu(int index)
    {
        GenericMenu menu = new GenericMenu();
        menu.AddItem(new GUIContent("Edit"), false, () =>
        {
            selectedEventIndex = index;
            Repaint();
        });
        menu.AddItem(new GUIContent("Duplicate"), false, () => DuplicateEvent(index));
        menu.AddItem(new GUIContent("Delete"), false, () => DeleteEvent(index));
        menu.ShowAsContext();
    }

    private void AddEvent(Type type, AnimationEventCommandType eventType, float startTime)
    {
        if (eventsProperty.arraySize >= MaxEventCount)
            return;

        Undo.RecordObject(eventData, "Add Animation Event");
        int index = eventsProperty.arraySize;
        eventsProperty.arraySize++;

        SerializedProperty element = eventsProperty.GetArrayElementAtIndex(index);
        element.managedReferenceValue = Activator.CreateInstance(type, true);
        serializedEventData.ApplyModifiedProperties();
        serializedEventData.Update();

        element = eventsProperty.GetArrayElementAtIndex(index);
        SetEventType(element, eventType);
        SetStartTime(element, startTime);
        SetEndTime(element, eventType == AnimationEventCommandType.Continuous ? Mathf.Clamp01(startTime + 0.1f) : startTime);
        selectedEventIndex = index;
        ValidateEvent(element);
        serializedEventData.ApplyModifiedProperties();
        SortAndSave();
    }

    private void DeleteEvent(int index)
    {
        if (index < 0 || index >= eventsProperty.arraySize)
            return;

        Undo.RecordObject(eventData, "Delete Animation Event");
        eventsProperty.DeleteArrayElementAtIndex(index);
        selectedEventIndex = -1;
        serializedEventData.ApplyModifiedProperties();
        SortAndSave();
    }

    private void DuplicateEvent(int index)
    {
        if (index < 0 || index >= eventsProperty.arraySize || eventsProperty.arraySize >= MaxEventCount)
            return;

        Undo.RecordObject(eventData, "Duplicate Animation Event");
        eventsProperty.InsertArrayElementAtIndex(index);
        selectedEventIndex = index + 1;
        serializedEventData.ApplyModifiedProperties();
        ValidateAllEvents();
        SortAndSave();
    }

    private void HandleKeyboard()
    {
        Event current = Event.current;
        if (current.type != EventType.KeyDown)
            return;

        if (current.keyCode == KeyCode.Delete || current.keyCode == KeyCode.Backspace)
        {
            DeleteEvent(selectedEventIndex);
            current.Use();
        }
        else if (current.keyCode == KeyCode.Space)
        {
            isPlaying = !isPlaying;
            lastUpdateTime = EditorApplication.timeSinceStartup;
            current.Use();
        }
        else if (current.control && current.keyCode == KeyCode.D)
        {
            DuplicateEvent(selectedEventIndex);
            current.Use();
        }
    }

    private void HandleTimelineZoomAndPan(Rect timelineRect, Event current)
    {
        if (current.type == EventType.ScrollWheel && timelineRect.Contains(current.mousePosition))
        {
            float mouseTime = XToTime(timelineRect, current.mousePosition.x);
            float currentRange = visibleEnd - visibleStart;
            float zoomFactor = current.delta.y > 0.0f ? 1.15f : 0.85f;
            float newRange = Mathf.Clamp(currentRange * zoomFactor, MinZoomRange, 1.0f);
            float pivot = Mathf.InverseLerp(visibleStart, visibleEnd, mouseTime);
            visibleStart = Mathf.Clamp01(mouseTime - newRange * pivot);
            visibleEnd = visibleStart + newRange;
            if (visibleEnd > 1.0f)
            {
                visibleEnd = 1.0f;
                visibleStart = visibleEnd - newRange;
            }

            current.Use();
            Repaint();
        }

        bool wantsPan = current.button == 2 || (current.button == 0 && current.shift);
        if (current.type == EventType.MouseDown && wantsPan && timelineRect.Contains(current.mousePosition))
        {
            isPanning = true;
            panMouseStartX = current.mousePosition.x;
            panStartVisibleStart = visibleStart;
            panStartVisibleEnd = visibleEnd;
            current.Use();
        }

        if (current.type == EventType.MouseDrag && isPanning)
        {
            float range = panStartVisibleEnd - panStartVisibleStart;
            float delta = (current.mousePosition.x - panMouseStartX) / timelineRect.width * range;
            visibleStart = Mathf.Clamp(panStartVisibleStart - delta, 0.0f, 1.0f - range);
            visibleEnd = visibleStart + range;
            current.Use();
            Repaint();
        }

        if (current.type == EventType.MouseUp && isPanning)
        {
            isPanning = false;
            current.Use();
        }
    }

    private void SortAndSave()
    {
        if (eventData == null)
            return;

        FieldInfo fieldInfo = typeof(AnimationEventData).GetField(EventsPropertyName, BindingFlags.Instance | BindingFlags.NonPublic);
        List<AnimationEventCommand> events = fieldInfo?.GetValue(eventData) as List<AnimationEventCommand>;
        if (events == null)
            return;

        AnimationEventCommand selectedCommand = null;
        if (selectedEventIndex >= 0 && selectedEventIndex < events.Count)
            selectedCommand = events[selectedEventIndex];

        events.Sort((left, right) =>
        {
            float leftTime = left == null ? float.MaxValue : left.StartTime;
            float rightTime = right == null ? float.MaxValue : right.StartTime;
            return leftTime.CompareTo(rightTime);
        });

        selectedEventIndex = selectedCommand == null ? -1 : events.IndexOf(selectedCommand);
        EditorUtility.SetDirty(eventData);
        AssetDatabase.SaveAssetIfDirty(eventData);
        serializedEventData?.Update();
    }

    private void ValidateAllEvents()
    {
        for (int i = 0; i < eventsProperty.arraySize; i++)
            ValidateEvent(eventsProperty.GetArrayElementAtIndex(i));
    }

    private void ValidateEvent(SerializedProperty element)
    {
        if (element == null || element.managedReferenceValue == null)
            return;

        AnimationEventCommandType eventType = GetEventType(element);
        float start = Mathf.Clamp01(GetStartTime(element));
        float end = Mathf.Clamp01(GetEndTime(element));
        if (eventType == AnimationEventCommandType.Trigger)
            end = start;
        else if (end < start)
            end = start;

        SetStartTime(element, start);
        SetEndTime(element, end);
    }

    private void DrawValidationSummary()
    {
        int warningCount = CountWarnings();
        if (warningCount > 0)
            GUILayout.Label($"{warningCount} warning(s)", EditorStyles.toolbarButton, GUILayout.Width(96.0f));
    }

    private void DrawValidationDetails()
    {
        if (eventsProperty.arraySize > MaxEventCount)
            EditorGUILayout.HelpBox($"Event count exceeds {MaxEventCount}.", MessageType.Error);

        for (int i = 0; i < eventsProperty.arraySize; i++)
        {
            SerializedProperty element = eventsProperty.GetArrayElementAtIndex(i);
            string warning = GetEventWarning(element);
            if (!string.IsNullOrEmpty(warning))
                EditorGUILayout.HelpBox($"Element {i}: {warning}", MessageType.Warning);
        }
    }

    private int CountWarnings()
    {
        int warnings = eventsProperty.arraySize > MaxEventCount ? 1 : 0;
        for (int i = 0; i < eventsProperty.arraySize; i++)
        {
            if (!string.IsNullOrEmpty(GetEventWarning(eventsProperty.GetArrayElementAtIndex(i))))
                warnings++;
        }

        return warnings;
    }

    private string GetEventWarning(SerializedProperty element)
    {
        if (element == null || element.managedReferenceValue == null)
            return "Null Command";

        float start = GetStartTime(element);
        float end = GetEndTime(element);
        if (start < 0.0f || start > 1.0f)
            return "StartTime must be 0 ~ 1.";
        if (GetEventType(element) == AnimationEventCommandType.Continuous && (end < 0.0f || end > 1.0f))
            return "EndTime must be 0 ~ 1.";
        if (GetEventType(element) == AnimationEventCommandType.Continuous && end < start)
            return "EndTime must be greater than or equal to StartTime.";

        return null;
    }

    private Dictionary<int, int> BuildRows(AnimationEventCommandType eventType)
    {
        List<EventLayout> layouts = new List<EventLayout>();
        List<float> rowEnds = new List<float>();
        List<int> sortedIndices = Enumerable.Range(0, eventsProperty.arraySize)
            .Where(index =>
            {
                SerializedProperty element = eventsProperty.GetArrayElementAtIndex(index);
                return element.managedReferenceValue != null && GetEventType(element) == eventType;
            })
            .OrderBy(index => GetStartTime(eventsProperty.GetArrayElementAtIndex(index)))
            .ToList();

        foreach (int index in sortedIndices)
        {
            SerializedProperty element = eventsProperty.GetArrayElementAtIndex(index);
            float start = GetStartTime(element);
            float end = eventType == AnimationEventCommandType.Trigger ? start + 0.035f : Mathf.Max(start + 0.035f, GetEndTime(element));
            int row = 0;

            while (row < rowEnds.Count && start < rowEnds[row])
                row++;

            if (row == rowEnds.Count)
                rowEnds.Add(end);
            else
                rowEnds[row] = end;

            layouts.Add(new EventLayout(index, row));
        }

        return layouts.ToDictionary(layout => layout.Index, layout => layout.Row);
    }

    private int CalculateRows(AnimationEventCommandType eventType)
    {
        Dictionary<int, int> rows = BuildRows(eventType);
        return rows.Count == 0 ? 1 : rows.Values.Max() + 1;
    }

    private int CountEvents(AnimationEventCommandType eventType)
    {
        int count = 0;
        for (int i = 0; i < eventsProperty.arraySize; i++)
        {
            SerializedProperty element = eventsProperty.GetArrayElementAtIndex(i);
            if (element.managedReferenceValue != null && GetEventType(element) == eventType)
                count++;
        }

        return count;
    }

    private List<Type> GetCreatableCommandTypes(AnimationEventCommandType eventType)
    {
        return TypeCache.GetTypesDerivedFrom<AnimationEventCommand>()
            .Where(IsCreatableCommandType)
            .Where(type => IsAllowedOnTrack(type, eventType))
            .OrderBy(type => type.Namespace)
            .ThenBy(type => type.Name)
            .ToList();
    }

    private bool IsCreatableCommandType(Type type)
    {
        if (type == null)
            return false;
        if (!type.IsClass || type.IsAbstract || type.IsGenericType)
            return false;
        if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            return false;
        if (!Attribute.IsDefined(type, typeof(SerializableAttribute), false))
            return false;

        ConstructorInfo constructor = type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
        return constructor != null;
    }

    private bool IsAllowedOnTrack(Type type, AnimationEventCommandType eventType)
    {
        AnimationEventCommandTypeAttribute attribute = type.GetCustomAttribute<AnimationEventCommandTypeAttribute>(false);
        return attribute == null || attribute.EventType == eventType;
    }

    private static AnimationEventData GetEventData(AnimationEventBehaviour behaviour)
    {
        if (behaviour == null)
            return null;

        SerializedObject serializedBehaviour = new SerializedObject(behaviour);
        SerializedProperty eventDataProperty = serializedBehaviour.FindProperty("eventData");
        return eventDataProperty == null ? null : eventDataProperty.objectReferenceValue as AnimationEventData;
    }

    private static AnimationClip FindOwnerStateClip(AnimationEventBehaviour behaviour)
    {
        if (behaviour == null)
            return null;

        string assetPath = AssetDatabase.GetAssetPath(behaviour);
        if (string.IsNullOrEmpty(assetPath))
            return null;

        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        foreach (UnityEngine.Object asset in assets)
        {
            if (asset is AnimatorState state && state.behaviours != null && state.behaviours.Contains(behaviour))
                return FindClip(state.motion);
        }

        return null;
    }

    private static AnimationClip FindClip(Motion motion)
    {
        if (motion is AnimationClip clip)
            return clip;

        if (motion is BlendTree blendTree)
        {
            foreach (ChildMotion childMotion in blendTree.children)
            {
                AnimationClip childClip = FindClip(childMotion.motion);
                if (childClip != null)
                    return childClip;
            }
        }

        return null;
    }

    private void LoadPreviewDefaults()
    {
        if (previewPrefab != null)
            return;

        string guid = EditorPrefs.GetString(GetPreviewPrefabPrefsKey(), string.Empty);
        if (string.IsNullOrEmpty(guid))
            return;

        string path = AssetDatabase.GUIDToAssetPath(guid);
        if (!string.IsNullOrEmpty(path))
            previewPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    private void SavePreviewPrefabDefault()
    {
        string key = GetPreviewPrefabPrefsKey();
        if (previewPrefab == null)
        {
            EditorPrefs.DeleteKey(key);
            return;
        }

        string path = AssetDatabase.GetAssetPath(previewPrefab);
        string guid = AssetDatabase.AssetPathToGUID(path);
        if (!string.IsNullOrEmpty(guid))
            EditorPrefs.SetString(key, guid);
    }

    private string GetPreviewPrefabPrefsKey()
    {
        return $"{PreviewPrefabPrefsKeyPrefix}.{Application.dataPath.GetHashCode()}";
    }

    private void LoadLayoutDefaults()
    {
        bottomPanelHeight = EditorPrefs.GetFloat(GetBottomPanelHeightPrefsKey(), bottomPanelHeight);
    }

    private void SaveLayoutDefaults()
    {
        EditorPrefs.SetFloat(GetBottomPanelHeightPrefsKey(), bottomPanelHeight);
    }

    private string GetBottomPanelHeightPrefsKey()
    {
        return $"{BottomPanelHeightPrefsKeyPrefix}.{Application.dataPath.GetHashCode()}";
    }

    private void EnsurePreviewUtility()
    {
        if (previewUtility != null)
            return;

        previewUtility = new PreviewRenderUtility();
        previewUtility.camera.clearFlags = CameraClearFlags.Color;
        previewUtility.camera.backgroundColor = PreviewBackground;
    }

    private void EnsurePreviewInstance()
    {
        if (previewInstance != null || previewPrefab == null)
            return;

        RebuildPreviewInstance();
    }

    private void RebuildPreviewInstance()
    {
        if (previewInstance != null)
        {
            DestroyImmediate(previewInstance);
            previewInstance = null;
        }

        hasPreviewFramingBounds = false;
        previewPivotOffset = Vector3.zero;

        if (previewPrefab == null)
            return;

        EnsurePreviewUtility();
        previewInstance = (GameObject)PrefabUtility.InstantiatePrefab(previewPrefab);
        if (previewInstance == null)
            previewInstance = Instantiate(previewPrefab);

        previewInstance.hideFlags = HideFlags.HideAndDontSave;
        previewUtility.AddSingleGO(previewInstance);
        CachePreviewRootTransform();
        SamplePreviewPose();
        CachePreviewFramingBounds();
    }

    private void CleanupPreview()
    {
        if (previewInstance != null)
        {
            DestroyImmediate(previewInstance);
            previewInstance = null;
        }

        previewUtility?.Cleanup();
        previewUtility = null;

        if (AnimationMode.InAnimationMode())
            AnimationMode.StopAnimationMode();
    }

    private void SamplePreviewPose()
    {
        if (previewClip == null || previewInstance == null)
            return;

        float time = Mathf.Clamp01(currentTime) * Mathf.Max(0.0f, previewClip.length);
        if (!AnimationMode.InAnimationMode())
            AnimationMode.StartAnimationMode();

        AnimationMode.BeginSampling();
        AnimationMode.SampleAnimationClip(previewInstance, previewClip, time);
        AnimationMode.EndSampling();
        RestorePreviewRootTransform();
    }

    private void CachePreviewRootTransform()
    {
        if (previewInstance == null)
            return;

        Transform transform = previewInstance.transform;
        previewRootPosition = transform.position;
        previewRootRotation = transform.rotation;
        previewRootScale = transform.localScale;
    }

    private void RestorePreviewRootTransform()
    {
        if (previewInstance == null)
            return;

        Transform transform = previewInstance.transform;
        transform.position = previewRootPosition;
        transform.rotation = previewRootRotation;
        transform.localScale = previewRootScale;
    }

    private void CachePreviewFramingBounds()
    {
        if (previewInstance == null)
        {
            hasPreviewFramingBounds = false;
            return;
        }

        RestorePreviewRootTransform();
        previewFramingBounds = CalculateBounds(previewInstance);
        hasPreviewFramingBounds = true;
    }

    private Bounds GetPreviewFramingBounds()
    {
        if (!hasPreviewFramingBounds && previewInstance != null)
            CachePreviewFramingBounds();

        return hasPreviewFramingBounds ? previewFramingBounds : new Bounds(Vector3.zero, Vector3.one);
    }

    private Bounds CalculateBounds(GameObject gameObject)
    {
        Renderer[] renderers = gameObject.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(Vector3.zero, Vector3.one);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        return bounds;
    }

    private Rect GetTimelineRect(Rect rect)
    {
        return new Rect(rect.x + HeaderWidth, rect.y + 8.0f, Mathf.Max(1.0f, rect.width - HeaderWidth - 10.0f), rect.height - 16.0f);
    }

    private float TimeToX(Rect timelineRect, float time)
    {
        float range = Mathf.Max(0.0001f, visibleEnd - visibleStart);
        return Mathf.LerpUnclamped(timelineRect.x, timelineRect.xMax, (Mathf.Clamp01(time) - visibleStart) / range);
    }

    private float XToTime(Rect timelineRect, float x)
    {
        return Mathf.Clamp(Mathf.Lerp(visibleStart, visibleEnd, (x - timelineRect.x) / timelineRect.width), 0.0f, 1.0f);
    }

    private float SnapTime(float time)
    {
        time = Mathf.Clamp01(time);
        if (!snapEnabled)
            return time;

        if (snapMode == SnapMode.Frame && previewClip != null && previewClip.frameRate > 0.0f && previewClip.length > 0.0f)
        {
            float frameCount = Mathf.Max(1.0f, previewClip.frameRate * previewClip.length);
            return Mathf.Clamp01(Mathf.Round(time * frameCount) / frameCount);
        }

        return Mathf.Clamp01(Mathf.Round(time * 100.0f) / 100.0f);
    }

    private float GetCurrentSeconds()
    {
        return previewClip == null ? 0.0f : currentTime * previewClip.length;
    }

    private GUIStyle CenteredMiniLabel()
    {
        GUIStyle style = new GUIStyle(EditorStyles.centeredGreyMiniLabel);
        style.alignment = TextAnchor.MiddleCenter;
        return style;
    }

    private string GetEventDisplayName(SerializedProperty element)
    {
        object value = element.managedReferenceValue;
        if (value == null)
            return "null";

        SerializedProperty editorEventNameProperty = element.FindPropertyRelative(EditorEventNamePropertyName);
        if (editorEventNameProperty != null && !string.IsNullOrWhiteSpace(editorEventNameProperty.stringValue))
            return editorEventNameProperty.stringValue;

        return value.GetType().Name;
    }

    private AnimationEventCommandType GetEventType(SerializedProperty element)
    {
        return (AnimationEventCommandType)element.FindPropertyRelative(EventTypePropertyName).enumValueIndex;
    }

    private void SetEventType(SerializedProperty element, AnimationEventCommandType eventType)
    {
        element.FindPropertyRelative(EventTypePropertyName).enumValueIndex = (int)eventType;
    }

    private float GetStartTime(SerializedProperty element)
    {
        return element.FindPropertyRelative(StartTimePropertyName).floatValue;
    }

    private void SetStartTime(SerializedProperty element, float value)
    {
        element.FindPropertyRelative(StartTimePropertyName).floatValue = Mathf.Clamp01(value);
    }

    private float GetEndTime(SerializedProperty element)
    {
        return element.FindPropertyRelative(EndTimePropertyName).floatValue;
    }

    private void SetEndTime(SerializedProperty element, float value)
    {
        element.FindPropertyRelative(EndTimePropertyName).floatValue = Mathf.Clamp01(value);
    }

    private string GetMenuPath(Type type)
    {
        if (string.IsNullOrEmpty(type.Namespace))
            return type.Name;

        return $"{type.Namespace.Replace('.', '/')}/{type.Name}";
    }
}
#endif
