using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityFramework.BehaviorTree
{
    internal enum UtilityInput { Constant, Float, Int, Bool }
    internal enum UtilityCombination { WeightedAverage, Product }
    internal enum UtilityReason { NotEvaluated, ConditionsRejected, InvalidInput, ZeroScore, LowerScore, Selected, MinimumHold, SwitchMargin, Failed }

    [Serializable]
    internal sealed class UtilityConsideration
    {
        [SerializeField] private UtilityInput input;
        [SerializeField] private float constant = 1;
        [SerializeField] private BlackboardKeyReference<float> floatKey = new();
        [SerializeField] private BlackboardKeyReference<int> intKey = new();
        [SerializeField] private BlackboardKeyReference<bool> boolKey = new();
        [SerializeField] private float minimum;
        [SerializeField] private float maximum = 1;
        [SerializeField, Min(0)] private float weight = 1;
        [SerializeField] private AnimationCurve curve = AnimationCurve.Linear(0, 0, 1, 1);
        internal UtilityInput Input { get => input; set => input = value; }
        internal float Constant { get => constant; set => constant = value; }
        internal BlackboardKeyReference<float> FloatKey { get => floatKey; set => floatKey = value; }
        internal BlackboardKeyReference<int> IntKey { get => intKey; set => intKey = value; }
        internal BlackboardKeyReference<bool> BoolKey { get => boolKey; set => boolKey = value; }
        internal float Minimum { get => minimum; set => minimum = value; }
        internal float Maximum { get => maximum; set => maximum = value; }
        internal float Weight { get => weight; set => weight = value; }
        internal AnimationCurve Curve { get => curve; set => curve = value; }
        internal void Validate(BlackboardData schema, List<string> errors)
        {
            if (input < UtilityInput.Constant || input > UtilityInput.Bool || !UtilityEvaluator.Finite(constant) ||
                !UtilityEvaluator.Finite(weight) || weight < 0 || !UtilityEvaluator.Finite(minimum) || !UtilityEvaluator.Finite(maximum) ||
                (input != UtilityInput.Bool && (maximum <= minimum || !UtilityEvaluator.Finite(maximum - minimum))) || curve == null || curve.length == 0)
                errors.Add("Invalid utility input, range, weight or curve.");
            if ((input == UtilityInput.Float && !schema.ContainsKey(floatKey)) ||
                (input == UtilityInput.Int && !schema.ContainsKey(intKey)) ||
                (input == UtilityInput.Bool && !schema.ContainsKey(boolKey))) errors.Add("Utility input key is missing or has an incompatible type.");
            if (curve != null)
                foreach (Keyframe key in curve.keys)
                    if (!UtilityEvaluator.Finite(key.time) || !UtilityEvaluator.Finite(key.value) ||
                        float.IsNaN(key.inTangent) || float.IsNaN(key.outTangent)) errors.Add("Utility curve contains invalid keys.");
        }
    }

    [Serializable]
    internal sealed class UtilityScoreSettings
    {
        [SerializeField, Range(0, 1)] private float constantScore = 0.1f;
        [SerializeField] private UtilityCombination combination;
        [SerializeField] private List<UtilityConsideration> considerations = new();
        internal float ConstantScore { get => constantScore; set => constantScore = value; }
        internal UtilityCombination Combination { get => combination; set => combination = value; }
        internal List<UtilityConsideration> Considerations { get => considerations; }
        internal void Validate(BlackboardData schema, List<string> errors)
        {
            if (!UtilityEvaluator.Finite(constantScore) || constantScore < 0 || constantScore > 1 ||
                combination < UtilityCombination.WeightedAverage || combination > UtilityCombination.Product || considerations == null)
            { errors.Add("Invalid utility score settings."); return; }
            float totalWeight = 0;
            foreach (var item in considerations)
            {
                if (item == null) errors.Add("Missing utility consideration.");
                else { item.Validate(schema, errors); totalWeight += item.Weight; }
            }
            if (considerations.Count > 0 && (!UtilityEvaluator.Finite(totalWeight) || totalWeight <= 0)) errors.Add("Utility considerations need a finite positive total weight.");
        }
    }

    [Serializable]
    internal sealed class UtilitySelectorNode : BehaviorNode
    {
        [SerializeField, Min(0.01f)] private float reevaluationInterval = 0.2f;
        [SerializeField, Min(0)] private float minimumHoldTime = 0.3f;
        [SerializeField, Range(0, 1)] private float switchMargin = 0.05f;
        internal float Interval { get => reevaluationInterval; set => reevaluationInterval = value; }
        internal float MinimumHoldTime { get => minimumHoldTime; set => minimumHoldTime = value; }
        internal float SwitchMargin { get => switchMargin; set => switchMargin = value; }
        internal void Validate(List<string> errors)
        {
            if (!UtilityEvaluator.Finite(reevaluationInterval) || reevaluationInterval <= 0 ||
                !UtilityEvaluator.Finite(minimumHoldTime) || minimumHoldTime < 0 ||
                !UtilityEvaluator.Finite(switchMargin) || switchMargin < 0 || switchMargin > 1) errors.Add("Invalid UtilitySelector timing or switch margin.");
        }
    }

    internal struct UtilitySample
    {
        internal float raw;
        internal float normalized;
        internal float score;
    }

    internal static class UtilityEvaluator
    {
        internal static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        internal static bool Evaluate(UtilityScoreSettings settings, IReadOnlyBlackboard board, UtilitySample[] samples, out float score)
        {
            score = settings.ConstantScore;
            if (settings.Considerations.Count == 0) return Finite(score);
            float sum = 0, totalWeight = 0, product = 1;
            for (int i = 0; i < settings.Considerations.Count; i++)
            {
                var item = settings.Considerations[i];
                float raw = item.Constant;
                if (item.Weight == 0) { samples[i] = default; continue; }
                switch (item.Input)
                {
                    case UtilityInput.Float: if (!board.TryGetValue(item.FloatKey, out raw)) return false; break;
                    case UtilityInput.Int: if (!board.TryGetValue(item.IntKey, out int number)) return false; raw = number; break;
                    case UtilityInput.Bool: if (!board.TryGetValue(item.BoolKey, out bool boolean)) return false; raw = boolean ? 1 : 0; break;
                }
                if (!Sample(item, raw, out samples[i])) return false;
                sum += samples[i].score * item.Weight; totalWeight += item.Weight;
                if (settings.Combination == UtilityCombination.Product) product *= Mathf.Pow(samples[i].score, item.Weight);
            }
            score = settings.Combination == UtilityCombination.WeightedAverage ? sum / totalWeight : product;
            return Finite(score);
        }
        internal static bool Sample(UtilityConsideration item, float raw, out UtilitySample sample)
        {
            sample = default;
            if (!Finite(raw) || item.Curve == null || item.Curve.length == 0 ||
                (item.Input != UtilityInput.Bool && (!Finite(item.Minimum) || !Finite(item.Maximum) ||
                    item.Maximum <= item.Minimum || !Finite(item.Maximum - item.Minimum)))) return false;
            float normalized = item.Input == UtilityInput.Bool ? Mathf.Clamp01(raw) : Mathf.Clamp01((raw - item.Minimum) / (item.Maximum - item.Minimum));
            float value = item.Curve.Evaluate(normalized);
            if (!Finite(value)) return false;
            sample = new UtilitySample { raw = raw, normalized = normalized, score = Mathf.Clamp01(value) };
            return true;
        }
    }

    internal sealed class UtilityRuntime
    {
        internal readonly float[] scores;
        internal readonly bool[] failed;
        internal readonly UtilityReason[] reasons;
        internal readonly UtilitySample[][] samples;
        internal int selected = -1;
        internal float elapsed;
        internal float held;
        internal bool force = true;
        internal UtilityRuntime(BehaviorNodeData definition, BehaviorTreeData tree)
        {
            int count = definition.Children.Count;
            scores = new float[count]; failed = new bool[count]; reasons = new UtilityReason[count]; samples = new UtilitySample[count][];
            for (int i = 0; i < count; i++) samples[i] = new UtilitySample[tree.FindNode(definition.Children[i]).UtilityScore.Considerations.Count];
        }
        internal void Reset()
        {
            selected = -1; elapsed = 0; held = 0; force = true;
            Array.Clear(scores, 0, scores.Length); Array.Clear(failed, 0, failed.Length); Array.Clear(reasons, 0, reasons.Length);
            for (int i = 0; i < samples.Length; i++) Array.Clear(samples[i], 0, samples[i].Length);
        }
    }
}
