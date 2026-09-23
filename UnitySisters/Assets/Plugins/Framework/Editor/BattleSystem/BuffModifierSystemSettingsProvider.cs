using System;
using System.Collections.Generic;
using System.Linq;

using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine.UIElements;

namespace UnityFramework.BattleSystem.Editor
{
    internal static class BuffModifierSystemSettingsProvider
    {
        private const string SettingsPath = "Project/UnityFramework/Battle System";
        private const string BattleAssemblyName = "Framework.BattleSysttem";

        [SettingsProvider]
        private static SettingsProvider CreateProvider()
        {
            return new SettingsProvider(SettingsPath, SettingsScope.Project)
            {
                label = "Battle System",
                keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "UnityFramework",
                    "Battle",
                    "Buff",
                    "Modifier",
                    "Assembly",
                },
                activateHandler = (_, root) => BuildSettingsUI(root),
            };
        }

        private static void BuildSettingsUI(VisualElement root)
        {
            root.Clear();
            root.Add(new Label("Buff Modifier System"));

            List<string> assemblyNames = GetCandidateAssemblyNames();
            string selectedAssembly = BuffModifierSystemSettings.instance.SearchAssemblyName;
            if (!assemblyNames.Contains(selectedAssembly))
                assemblyNames.Insert(0, selectedAssembly);

            DropdownField assemblyField = new DropdownField(
                "Search Assembly",
                assemblyNames,
                Math.Max(0, assemblyNames.IndexOf(selectedAssembly)));
            root.Add(assemblyField);

            Label statusLabel = new Label();
            root.Add(statusLabel);

            Button generateButton = new Button(() =>
            {
                BuffModifierSystemSettingsGenerator.Generate();
                UpdateStatus(statusLabel, assemblyField.value, assemblyNames);
            })
            {
                text = "Regenerate Runtime Settings",
            };
            root.Add(generateButton);

            assemblyField.RegisterValueChangedCallback(changeEvent =>
            {
                BuffModifierSystemSettings.instance.SetSearchAssemblyName(changeEvent.newValue);
                BuffModifierSystemSettingsGenerator.Generate();
                UpdateStatus(statusLabel, changeEvent.newValue, assemblyNames);
            });

            UpdateStatus(statusLabel, selectedAssembly, assemblyNames);
        }

        private static void UpdateStatus(
            Label statusLabel,
            string assemblyName,
            IReadOnlyCollection<string> candidateAssemblyNames)
        {
            if (!candidateAssemblyNames.Contains(assemblyName))
            {
                statusLabel.text = $"Assembly not found: {assemblyName}";
                return;
            }

            IReadOnlyList<Type> containerTypes =
                BuffModifierSystemSettingsGenerator.GetContainerTypes(assemblyName);
            if (containerTypes.Count == 0)
            {
                statusLabel.text =
                    "No concrete [BuffModifierContainerType] class was found in this assembly.";
                return;
            }

            if (containerTypes.Count > 1)
            {
                statusLabel.text =
                    $"Multiple [BuffModifierContainerType] classes were found ({containerTypes.Count}): " +
                    string.Join(", ", containerTypes.Select(type => type.FullName));
                return;
            }

            statusLabel.text = $"Detected Container: {containerTypes[0].FullName}";
        }

        private static List<string> GetCandidateAssemblyNames()
        {
            UnityEditor.Compilation.Assembly[] assemblies =
                CompilationPipeline.GetAssemblies(AssembliesType.Player);
            Dictionary<string, UnityEditor.Compilation.Assembly> assembliesByName =
                assemblies.ToDictionary(assembly => assembly.name, StringComparer.Ordinal);

            List<string> names = new List<string>();
            for (int i = 0; i < assemblies.Length; i++)
            {
                UnityEditor.Compilation.Assembly assembly = assemblies[i];
                if (assembly.name == BattleAssemblyName)
                    continue;

                if (ReferencesAssembly(
                    assembly,
                    BattleAssemblyName,
                    assembliesByName,
                    new HashSet<string>(StringComparer.Ordinal)))
                {
                    names.Add(assembly.name);
                }
            }

            if (!names.Contains(BuffModifierSystemSettings.DefaultAssemblyName))
                names.Add(BuffModifierSystemSettings.DefaultAssemblyName);

            names.Sort(StringComparer.Ordinal);
            return names;
        }

        private static bool ReferencesAssembly(
            UnityEditor.Compilation.Assembly assembly,
            string targetAssemblyName,
            IReadOnlyDictionary<string, UnityEditor.Compilation.Assembly> assembliesByName,
            ISet<string> visited)
        {
            if (!visited.Add(assembly.name))
                return false;

            UnityEditor.Compilation.Assembly[] references = assembly.assemblyReferences;
            for (int i = 0; i < references.Length; i++)
            {
                UnityEditor.Compilation.Assembly reference = references[i];
                if (reference.name == targetAssemblyName)
                    return true;

                if (assembliesByName.TryGetValue(reference.name, out UnityEditor.Compilation.Assembly resolved) &&
                    ReferencesAssembly(
                        resolved,
                        targetAssemblyName,
                        assembliesByName,
                        visited))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
