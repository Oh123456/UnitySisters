using System;
using System.Reflection;

using UnityEngine;

namespace UnityFramework.BattleSystem
{
    public static partial class BuffModifierSystem
    {
        private const string DEFAULT_SEARCH_ASSEMBLY_NAME = "Assembly-CSharp";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            buffModifierContainer = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            string searchAssemblyName = DEFAULT_SEARCH_ASSEMBLY_NAME;
            ConfigureSearchAssembly(ref searchAssemblyName);

            Assembly targetAssembly = FindLoadedAssembly(searchAssemblyName);
            if (targetAssembly == null)
            {
                Debug.LogError(
                    $"BuffModifierSystem initialization failed. Assembly '{searchAssemblyName}' is not loaded.");
                return;
            }

            Type containerType = FindContainerType(targetAssembly);
            if (containerType == null)
                return;

            try
            {
                BuffModifierContainer container =
                    (BuffModifierContainer)Activator.CreateInstance(containerType, true);
                container.Initialize();
                buffModifierContainer = container;
            }
            catch (Exception exception)
            {
                buffModifierContainer = null;
                Debug.LogError(
                    $"BuffModifierSystem failed to initialize '{containerType.FullName}'.\n{exception}");
            }
        }

        private static Assembly FindLoadedAssembly(string assemblyName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                if (string.Equals(
                    assemblies[i].GetName().Name,
                    assemblyName,
                    StringComparison.Ordinal))
                {
                    return assemblies[i];
                }
            }

            return null;
        }

        private static Type FindContainerType(Assembly assembly)
        {
            Type selectedType = null;
            Type[] types = GetLoadableTypes(assembly);
            for (int i = 0; i < types.Length; i++)
            {
                Type type = types[i];
                if (type == null || type.IsAbstract || type.IsGenericTypeDefinition ||
                    !typeof(BuffModifierContainer).IsAssignableFrom(type) ||
                    !type.IsDefined(typeof(BuffModifierContainerTypeAttribute), false))
                {
                    continue;
                }

                if (selectedType != null)
                {
                    Debug.LogError(
                        $"BuffModifierSystem initialization failed. Assembly '{assembly.GetName().Name}' " +
                        "contains multiple [BuffModifierContainerType] classes: " +
                        $"'{selectedType.FullName}' and '{type.FullName}'.");
                    return null;
                }

                selectedType = type;
            }

            if (selectedType == null)
            {
                Debug.LogError(
                    $"BuffModifierSystem initialization failed. Assembly '{assembly.GetName().Name}' " +
                    "does not contain a concrete [BuffModifierContainerType] class.");
            }

            return selectedType;
        }

        private static Type[] GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                Debug.LogWarning(
                    $"BuffModifierSystem could not inspect every type in '{assembly.GetName().Name}'.\n" +
                    exception);
                return exception.Types;
            }
        }

        static partial void ConfigureSearchAssembly(ref string assemblyName);
    }
}
