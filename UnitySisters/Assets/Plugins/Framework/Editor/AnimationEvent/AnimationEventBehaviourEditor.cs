#if UNITY_EDITOR
using System.Linq;

using UnityEditor;
using UnityEditor.Animations;

using UnityEngine;

using UnityFramework.Animation;

[CustomEditor(typeof(AnimationEventBehaviour))]
public sealed class AnimationEventBehaviourEditor : Editor
{
    private SerializedProperty eventDataProperty;

    private void OnEnable()
    {
        eventDataProperty = serializedObject.FindProperty("eventData");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(eventDataProperty);

        using (new EditorGUI.DisabledScope(eventDataProperty.objectReferenceValue == null))
        {
            if (GUILayout.Button("Edit Animation Events"))
                AnimationEventEditorWindow.Open((AnimationEventData)eventDataProperty.objectReferenceValue, FindOwnerStateClip());
        }

        serializedObject.ApplyModifiedProperties();
    }

    private AnimationClip FindOwnerStateClip()
    {
        string assetPath = AssetDatabase.GetAssetPath(target);
        if (string.IsNullOrEmpty(assetPath))
            return null;

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        AnimationEventBehaviour behaviour = (AnimationEventBehaviour)target;
        foreach (Object asset in assets)
        {
            if (asset is AnimatorState state && state.behaviours != null && state.behaviours.Contains(behaviour))
                return FindClip(state.motion);
        }

        return null;
    }

    private AnimationClip FindClip(Motion motion)
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
}
#endif
