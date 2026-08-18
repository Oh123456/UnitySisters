using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace UnityFramework.FSM.Editor
{
    internal readonly struct FSMParameterFieldBinding
    {
        private readonly FieldInfo field;
        private readonly string key;
        private readonly int id;
        private readonly FSMParameterType parameterType;

        public FSMParameterFieldBinding(
            FieldInfo field,
            string key,
            int id,
            FSMParameterType parameterType)
        {
            this.field = field;
            this.key = key;
            this.id = id;
            this.parameterType = parameterType;
        }

        public FieldInfo GetField() => this.field;
        public string GetKey() => this.key;
        public int GetID() => this.id;
        public FSMParameterType GetParameterType() => this.parameterType;
    }

    [InitializeOnLoad]
    internal static class FSMParameterBindingGenerator
    {
        private const string GeneratedSuffix = ".FSMParameters.g.cs";

        static FSMParameterBindingGenerator()
        {
            CompilationPipeline.compilationFinished -= OnCompilationFinished;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
        }

        [MenuItem("Tools/FSM/Generate Parameter Bindings")]
        private static void GenerateAll()
        {
            GenerateAllAndCleanup();
        }

        private static void OnCompilationFinished(object context)
        {
            EditorApplication.delayCall -= GenerateAllAndCleanup;
            EditorApplication.delayCall += GenerateAllAndCleanup;
        }

        private static void GenerateAllAndCleanup()
        {
            var sourceTypes = new List<Type>();
            GetSourceTypes(sourceTypes);
            for (int i = 0; i < sourceTypes.Count; i++)
            {
                try
                {
                    Generate(sourceTypes[i]);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            CleanupGeneratedFiles();
        }

        public static string GetTypeID(Type sourceType)
        {
            if (sourceType == null)
                return string.Empty;

            return FSMParameterKey.GetSourceTypeID(sourceType);
        }

        public static void GetSourceTypes(List<Type> results)
        {
            if (results == null)
                throw new ArgumentNullException(nameof(results));

            results.Clear();
            var uniqueTypes = new HashSet<Type>();
            foreach (FieldInfo field in TypeCache.GetFieldsWithAttribute<FSMParameterAttribute>())
            {
                Type sourceType = field.DeclaringType;
                if (sourceType != null && uniqueTypes.Add(sourceType))
                    results.Add(sourceType);
            }
            foreach (FieldInfo field in TypeCache.GetFieldsWithAttribute<FSMTriggerAttribute>())
            {
                Type sourceType = field.DeclaringType;
                if (sourceType != null && uniqueTypes.Add(sourceType))
                    results.Add(sourceType);
            }

            results.Sort((left, right) =>
                string.Compare(left.FullName, right.FullName, StringComparison.Ordinal));
        }

        public static Type FindSourceType(string typeID, List<Type> sourceTypes)
        {
            if (string.IsNullOrEmpty(typeID) || sourceTypes == null)
                return null;

            for (int i = 0; i < sourceTypes.Count; i++)
            {
                if (GetTypeID(sourceTypes[i]) == typeID)
                    return sourceTypes[i];
            }
            return null;
        }

        /// <summary>
        /// Attribute 필드를 FSMData Parameter로 동기화하고 수동 Parameter는 보존한다.
        /// </summary>
        public static void SyncData(FSMData fsmData, Type sourceType)
        {
            if (fsmData == null)
                throw new ArgumentNullException(nameof(fsmData));

            var bindings = new List<FSMParameterFieldBinding>();
            GetBindings(sourceType, bindings);
            var activeKeys = new HashSet<string>();

            // 에셋을 변경하기 전에 Hash 충돌을 모두 검사해 부분 동기화를 방지한다.
            for (int i = 0; i < bindings.Count; i++)
            {
                FSMParameterFieldBinding binding = bindings[i];
                for (int duplicateIndex = 0; duplicateIndex < i; duplicateIndex++)
                {
                    if (bindings[duplicateIndex].GetID() == binding.GetID())
                        throw new InvalidOperationException(
                            $"FSM Parameter hash collision between " +
                            $"'{bindings[duplicateIndex].GetField().Name}' and " +
                            $"'{binding.GetField().Name}'.");
                }

                FSMParameterData idOwner = fsmData.FindParameter(binding.GetID());
                if (idOwner != null && idOwner.GetBindingKey() != binding.GetKey())
                    throw new InvalidOperationException(
                        $"Parameter hash collision: '{binding.GetField().Name}' and " +
                        $"'{idOwner.GetName()}' use ID {binding.GetID()}.");
            }

            for (int i = 0; i < bindings.Count; i++)
            {
                FSMParameterFieldBinding binding = bindings[i];
                activeKeys.Add(binding.GetKey());
                FSMParameterData parameter = fsmData.FindBoundParameter(binding.GetKey());
                if (parameter == null)
                {
                    parameter = fsmData.AddParameter(
                        binding.GetID(),
                        binding.GetField().Name,
                        binding.GetParameterType());
                    parameter.SetFieldBinding(binding.GetKey());
                }
                else
                {
                    parameter.SetName(binding.GetField().Name);
                    parameter.SetParameterType(binding.GetParameterType());
                }
            }

            IReadOnlyList<FSMParameterData> parameters = fsmData.GetParameters();
            for (int i = parameters.Count - 1; i >= 0; i--)
            {
                FSMParameterData parameter = parameters[i];
                if (parameter != null && parameter.GetIsFieldBound() &&
                    !activeKeys.Contains(parameter.GetBindingKey()))
                    fsmData.RemoveParameter(parameter);
            }

            fsmData.SetParameterSourceTypeID(GetTypeID(sourceType));
            EditorUtility.SetDirty(fsmData);
        }

        public static void Generate(Type sourceType)
        {
            var bindings = new List<FSMParameterFieldBinding>();
            GetBindings(sourceType, bindings);
            if (bindings.Count == 0)
                return;
            if (sourceType.IsNested || sourceType.IsGenericType)
                throw new InvalidOperationException(
                    $"FSM Parameter binding does not support nested or generic type '{sourceType.FullName}'.");

            string sourcePath = FindMonoScriptPath(sourceType);
            if (string.IsNullOrEmpty(sourcePath))
                throw new InvalidOperationException(
                    $"Could not find MonoScript for FSM Parameter source '{sourceType.FullName}'.");

            string directory = Path.GetDirectoryName(sourcePath)?.Replace('\\', '/');
            string fileName = Path.GetFileNameWithoutExtension(sourcePath) + GeneratedSuffix;
            string generatedPath = string.IsNullOrEmpty(directory)
                ? fileName
                : $"{directory}/{fileName}";
            string generatedSource = BuildSource(sourceType, bindings);

            if (File.Exists(generatedPath) && File.ReadAllText(generatedPath) == generatedSource)
                return;

            File.WriteAllText(generatedPath, generatedSource, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(generatedPath, ImportAssetOptions.ForceUpdate);
        }

        public static void GetBindings(Type sourceType, List<FSMParameterFieldBinding> results)
        {
            if (results == null)
                throw new ArgumentNullException(nameof(results));

            results.Clear();
            if (sourceType == null)
                return;

            FieldInfo[] fields = sourceType.GetFields(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.DeclaredOnly);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                bool isParameter = field.IsDefined(typeof(FSMParameterAttribute), false);
                bool isTrigger = field.IsDefined(typeof(FSMTriggerAttribute), false);
                if (!isParameter && !isTrigger)
                    continue;
                if (isParameter && isTrigger)
                    throw new InvalidOperationException(
                        $"Field '{sourceType.FullName}.{field.Name}' cannot use both " +
                        "FSMParameterAttribute and FSMTriggerAttribute.");
                if (field.IsInitOnly || field.IsLiteral || field.IsStatic)
                    throw new InvalidOperationException(
                        $"FSM Parameter field '{sourceType.FullName}.{field.Name}' must be a writable instance field.");

                FSMParameterType parameterType = isTrigger
                    ? GetTriggerType(field)
                    : GetParameterType(field);
                string key = FSMParameterKey.GetFieldKey(sourceType, field.Name);
                results.Add(new FSMParameterFieldBinding(
                    field,
                    key,
                    FSMParameterKey.GetHash(key),
                    parameterType));
            }

            results.Sort((left, right) =>
                left.GetField().MetadataToken.CompareTo(right.GetField().MetadataToken));
        }

        private static FSMParameterType GetParameterType(FieldInfo field)
        {
            if (field.FieldType == typeof(bool))
                return FSMParameterType.Bool;
            if (field.FieldType == typeof(int))
                return FSMParameterType.Int;
            if (field.FieldType == typeof(float))
                return FSMParameterType.Float;

            throw new InvalidOperationException(
                $"FSM Parameter field '{field.DeclaringType?.FullName}.{field.Name}' has unsupported type " +
                $"'{field.FieldType.FullName}'. Only bool, int and float are supported.");
        }

        private static string FindMonoScriptPath(Type sourceType)
        {
            string[] scriptGUIDs = AssetDatabase.FindAssets("t:MonoScript");
            for (int i = 0; i < scriptGUIDs.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(scriptGUIDs[i]);
                if (path.EndsWith(GeneratedSuffix, StringComparison.Ordinal))
                    continue;
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script != null && script.GetClass() == sourceType)
                    return path;
            }
            return null;
        }

        private static void CleanupGeneratedFiles()
        {
            string[] assetPaths = AssetDatabase.GetAllAssetPaths();
            var bindings = new List<FSMParameterFieldBinding>();
            for (int i = 0; i < assetPaths.Length; i++)
            {
                string generatedPath = assetPaths[i];
                if (!generatedPath.EndsWith(GeneratedSuffix, StringComparison.Ordinal))
                    continue;

                string sourcePath = generatedPath.Substring(
                    0,
                    generatedPath.Length - GeneratedSuffix.Length) + ".cs";
                MonoScript sourceScript = AssetDatabase.LoadAssetAtPath<MonoScript>(sourcePath);
                Type sourceType = sourceScript?.GetClass();
                if (sourceType != null)
                    GetBindings(sourceType, bindings);
                else
                    bindings.Clear();

                if (bindings.Count == 0)
                    AssetDatabase.DeleteAsset(generatedPath);
            }
        }

        private static string BuildSource(
            Type sourceType,
            List<FSMParameterFieldBinding> bindings)
        {
            var builder = new StringBuilder(1024);
            builder.AppendLine("// <auto-generated />");
            builder.AppendLine();
            if (!string.IsNullOrEmpty(sourceType.Namespace))
            {
                builder.Append("namespace ").Append(sourceType.Namespace).AppendLine();
                builder.AppendLine("{");
            }

            string indent = string.IsNullOrEmpty(sourceType.Namespace) ? string.Empty : "    ";
            builder.Append(indent)
                .Append(sourceType.IsPublic ? "public " : "internal ")
                .Append("partial class @")
                .Append(sourceType.Name)
                .AppendLine(" : UnityFramework.FSM.IFSMParameterBinder");
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).AppendLine("    void UnityFramework.FSM.IFSMParameterBinder.SyncFSMParameters(");
            builder.Append(indent).AppendLine("        UnityFramework.FSM.IStateMachine stateMachine)");
            builder.Append(indent).AppendLine("    {");

            for (int i = 0; i < bindings.Count; i++)
            {
                FSMParameterFieldBinding binding = bindings[i];
                if (binding.GetParameterType() == FSMParameterType.Trigger)
                {
                    builder.Append(indent)
                        .Append("        if (this.@")
                        .Append(binding.GetField().Name)
                        .AppendLine(")");
                    builder.Append(indent).AppendLine("        {");
                    builder.Append(indent)
                        .Append("            stateMachine.SetTrigger(")
                        .Append(binding.GetID())
                        .AppendLine(");");
                    builder.Append(indent)
                        .Append("            this.@")
                        .Append(binding.GetField().Name)
                        .AppendLine(" = false;");
                    builder.Append(indent).AppendLine("        }");
                    continue;
                }

                string methodName = GetSetterName(binding.GetParameterType());
                builder.Append(indent)
                    .Append("        stateMachine.")
                    .Append(methodName)
                    .Append('(')
                    .Append(binding.GetID())
                    .Append(", this.@")
                    .Append(binding.GetField().Name)
                    .AppendLine(");");
            }

            builder.Append(indent).AppendLine("    }");
            builder.Append(indent).AppendLine("}");
            if (!string.IsNullOrEmpty(sourceType.Namespace))
                builder.AppendLine("}");
            return builder.ToString();
        }

        private static string GetSetterName(FSMParameterType parameterType)
        {
            switch (parameterType)
            {
                case FSMParameterType.Bool: return "SetBool";
                case FSMParameterType.Int: return "SetInt";
                case FSMParameterType.Float: return "SetFloat";
                default:
                    throw new InvalidOperationException(
                        $"Unsupported bound FSM Parameter type '{parameterType}'.");
            }
        }

        private static FSMParameterType GetTriggerType(FieldInfo field)
        {
            if (field.FieldType != typeof(bool))
                throw new InvalidOperationException(
                    $"FSM Trigger field '{field.DeclaringType?.FullName}.{field.Name}' must be bool.");
            return FSMParameterType.Trigger;
        }
    }
}
