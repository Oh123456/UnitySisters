using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace UnityFramework.BattleSystem.Editor
{
    internal sealed class BuffMonitorSnapshot
    {
        internal BuffData BuffData { get; }
        internal int BuffId { get; }
        internal int Stack { get; }
        internal int MaxStack { get; }
        internal IReadOnlyList<string> ModifierNames { get; }

        internal BuffMonitorSnapshot(
            BuffData buffData,
            int stack,
            IReadOnlyList<string> modifierNames)
        {
            BuffData = buffData;
            BuffId = buffData == null ? 0 : buffData.BuffID;
            Stack = stack;
            MaxStack = buffData == null ? 0 : buffData.MaxStack;
            ModifierNames = modifierNames;
        }
    }

    internal static class BuffMonitorReflection
    {
        private const BindingFlags INSTANCE_FLAGS =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly FieldInfo BuffListField =
            typeof(BuffContainer).GetField("buffList", INSTANCE_FLAGS);

        private static readonly Dictionary<Type, PropertyInfo> StackProperties = new();
        private static readonly Dictionary<Type, PropertyInfo> BuffStateProperties = new();
        private static readonly Dictionary<Type, FieldInfo> ModifierEntryFields = new();
        private static readonly Dictionary<Type, FieldInfo> ModifierListFields = new();

        internal static bool TryCapture(
            BattleComponent battleComponent,
            List<BuffMonitorSnapshot> snapshots,
            out string error)
        {
            snapshots.Clear();
            error = string.Empty;

            if (battleComponent == null || battleComponent.BuffContainer == null)
                return true;

            try
            {
                if (BuffListField?.GetValue(battleComponent.BuffContainer) is not IList buffList)
                    return true;

                for (int i = 0; i < buffList.Count; i++)
                {
                    object instance = buffList[i];
                    if (instance == null)
                        continue;

                    Type instanceType = instance.GetType();
                    int stack = GetCachedProperty(StackProperties, instanceType, "Stack")
                        ?.GetValue(instance) as int? ?? 0;
                    BuffState buffState = GetCachedProperty(
                        BuffStateProperties,
                        instanceType,
                        "BuffState")?.GetValue(instance) as BuffState;

                    BuffData buffData = buffState?.BuffData;
                    snapshots.Add(new BuffMonitorSnapshot(
                        buffData,
                        stack,
                        GetModifierNames(instance, instanceType)));
                }

                return true;
            }
            catch (Exception exception)
            {
                error = exception.GetBaseException().Message;
                snapshots.Clear();
                return false;
            }
        }

        private static IReadOnlyList<string> GetModifierNames(object instance, Type instanceType)
        {
            List<string> names = new List<string>();
            FieldInfo entriesField = GetCachedField(
                ModifierEntryFields,
                instanceType,
                "buffModifierEntries");
            if (entriesField?.GetValue(instance) is not IDictionary entries)
                return names;

            foreach (DictionaryEntry pair in entries)
            {
                object entry = pair.Value;
                if (entry == null)
                    continue;

                FieldInfo listField = GetCachedField(
                    ModifierListFields,
                    entry.GetType(),
                    "buffModifiers");
                if (listField?.GetValue(entry) is not IEnumerable modifiers)
                    continue;

                foreach (object modifier in modifiers)
                {
                    if (modifier != null)
                        names.Add(modifier.GetType().Name);
                }
            }

            names.Sort(StringComparer.Ordinal);
            return names;
        }

        private static PropertyInfo GetCachedProperty(
            IDictionary<Type, PropertyInfo> cache,
            Type type,
            string propertyName)
        {
            if (!cache.TryGetValue(type, out PropertyInfo property))
            {
                property = type.GetProperty(propertyName, INSTANCE_FLAGS);
                cache.Add(type, property);
            }

            return property;
        }

        private static FieldInfo GetCachedField(
            IDictionary<Type, FieldInfo> cache,
            Type type,
            string fieldName)
        {
            if (!cache.TryGetValue(type, out FieldInfo field))
            {
                field = type.GetField(fieldName, INSTANCE_FLAGS);
                cache.Add(type, field);
            }

            return field;
        }
    }
}
