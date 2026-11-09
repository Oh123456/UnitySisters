using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityFramework.BehaviorTree.Editor
{
    internal sealed partial class BehaviorTreeEditorWindow
    {
        private readonly Dictionary<string, float> utilityPreviewInputs = new();

        private void DrawUtility(SerializedObject serialized, SerializedProperty behavior, BehaviorNodeData definition)
        {
            EditorGUILayout.PropertyField(behavior, new GUIContent("Utility selection policy"), true);
            EditorGUILayout.HelpBox("Child score settings follow node IDs, not display order. Conditions reject candidates before scoring. Play observation shows the last decision snapshot.", MessageType.Info);
            UtilityRuntime runtime = null;
            bool active = false;
            if (!CanEdit && observedController != null)
            {
                var nodes = observedController.Model.nodes;
                for (int i = 0; i < nodes.Length; i++) if (nodes[i].definition.ID == definition.ID) { runtime = nodes[i].utility; active = nodes[i].active; break; }
            }
            for (int childIndex = 0; childIndex < definition.Children.Count; childIndex++)
            {
                string id = definition.Children[childIndex];
                var child = data.FindNode(id);
                if (child == null) continue;
                int nodeIndex = 0; while (data.Nodes[nodeIndex].ID != id) nodeIndex++;
                var settings = serialized.FindProperty("nodes").GetArrayElementAtIndex(nodeIndex).FindPropertyRelative("utilityScore");
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(child.Name, EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Node ID", id);
                var terms = settings.FindPropertyRelative("considerations");
                if (terms.arraySize == 0) EditorGUILayout.PropertyField(settings.FindPropertyRelative("constantScore"), new GUIContent("Fixed score"));
                else EditorGUILayout.PropertyField(settings.FindPropertyRelative("combination"));
                float sum = 0, weightSum = 0, product = 1;
                bool previewValid = true;
                for (int i = 0; i < terms.arraySize; i++)
                {
                    var term = terms.GetArrayElementAtIndex(i);
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.PropertyField(term.FindPropertyRelative("input"));
                    var input = (UtilityInput)term.FindPropertyRelative("input").enumValueIndex;
                    string keyField = input == UtilityInput.Float ? "floatKey" : input == UtilityInput.Int ? "intKey" : "boolKey";
                    if (input == UtilityInput.Constant) EditorGUILayout.PropertyField(term.FindPropertyRelative("constant"));
                    else EditorGUILayout.PropertyField(term.FindPropertyRelative(keyField), new GUIContent("Blackboard input"), true);
                    if (input != UtilityInput.Bool)
                    {
                        EditorGUILayout.PropertyField(term.FindPropertyRelative("minimum"));
                        EditorGUILayout.PropertyField(term.FindPropertyRelative("maximum"));
                    }
                    EditorGUILayout.PropertyField(term.FindPropertyRelative("weight"));
                    EditorGUILayout.PropertyField(term.FindPropertyRelative("curve"), new GUIContent("Response curve"));
                    float minimum = input == UtilityInput.Bool ? 0 : term.FindPropertyRelative("minimum").floatValue;
                    float maximum = input == UtilityInput.Bool ? 1 : term.FindPropertyRelative("maximum").floatValue;
                    string previewID = data.GetInstanceID() + ":" + id + ":" + i;
                    if (!utilityPreviewInputs.TryGetValue(previewID, out float raw)) raw = minimum;
                    if (input == UtilityInput.Constant) raw = term.FindPropertyRelative("constant").floatValue;
                    else
                    {
                        if (UtilityEvaluator.Finite(minimum) && UtilityEvaluator.Finite(maximum) && maximum > minimum)
                        {
                            using (new EditorGUI.DisabledScope(!CanEdit))
                                raw = EditorGUILayout.Slider("Preview input", raw, minimum, maximum);
                        }
                        else EditorGUILayout.HelpBox("Enter a finite range with maximum greater than minimum.", MessageType.Warning);
                        if (input == UtilityInput.Int) raw = Mathf.Round(raw);
                        if (input == UtilityInput.Bool) raw = raw >= 0.5f ? 1 : 0;
                        if (CanEdit) utilityPreviewInputs[previewID] = raw;
                    }
                    var preview = new UtilityConsideration { Input = input, Minimum = minimum, Maximum = maximum,
                        Curve = term.FindPropertyRelative("curve").animationCurveValue };
                    if (maximum <= minimum || !UtilityEvaluator.Sample(preview, raw, out UtilitySample sample)) previewValid = false;
                    else
                    {
                        EditorGUILayout.LabelField($"Normalized {sample.normalized:0.###}  → curve {sample.score:0.###}");
                        float weight = term.FindPropertyRelative("weight").floatValue;
                        if (weight > 0) { sum += sample.score * weight; weightSum += weight; product *= Mathf.Pow(sample.score, weight); }
                    }
                    if (runtime != null && childIndex < runtime.samples.Length && i < runtime.samples[childIndex].Length)
                    {
                        var actual = runtime.samples[childIndex][i];
                        EditorGUILayout.LabelField($"Last evaluation: input {actual.raw:0.###} / normalized {actual.normalized:0.###} / curve {actual.score:0.###}");
                    }
                    bool remove = GUILayout.Button("Remove consideration");
                    EditorGUILayout.EndVertical();
                    if (remove && CanEdit) { terms.DeleteArrayElementAtIndex(i); break; }
                }
                if (GUILayout.Button("Add consideration") && CanEdit)
                {
                    int index = terms.arraySize; terms.arraySize++;
                    var term = terms.GetArrayElementAtIndex(index);
                    term.FindPropertyRelative("input").enumValueIndex = 0; term.FindPropertyRelative("constant").floatValue = 1;
                    term.FindPropertyRelative("minimum").floatValue = 0; term.FindPropertyRelative("maximum").floatValue = 1;
                    term.FindPropertyRelative("weight").floatValue = 1;
                    term.FindPropertyRelative("curve").animationCurveValue = AnimationCurve.Linear(0, 0, 1, 1);
                }
                float total = terms.arraySize == 0 ? settings.FindPropertyRelative("constantScore").floatValue :
                    (UtilityCombination)settings.FindPropertyRelative("combination").enumValueIndex == UtilityCombination.Product ? product : sum / weightSum;
                EditorGUILayout.LabelField(previewValid && (terms.arraySize == 0 || weightSum > 0) && UtilityEvaluator.Finite(total) ? $"Preview score: {total:0.###}" : "Preview: invalid range/input/weights");
                if (runtime != null && childIndex < runtime.scores.Length)
                    EditorGUILayout.LabelField($"Runtime score {runtime.scores[childIndex]:0.###} | {runtime.reasons[childIndex]}" + (runtime.selected == childIndex ? (active ? " | EXECUTING" : " | LAST SELECTED") : ""));
                EditorGUILayout.EndVertical();
            }
            if (runtime != null) EditorGUILayout.LabelField($"Current hold: {runtime.held:0.###}s | evaluation timer: {runtime.elapsed:0.###}s");
        }
    }
}
