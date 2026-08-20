using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace UnityFramework.FSM.Editor
{
    /// <summary>
    /// 컴파일된 Type 정보가 없어도 C# 소스에서 FSM Parameter 생성 코드를 복구한다.
    /// </summary>
    internal static class FSMParameterSourceGenerator
    {
        private const string GeneratedSuffix = ".FSMParameters.g.cs";

        private static bool isGenerating;
        private static bool refreshScheduled;

        private static readonly Regex namespaceRegex = new Regex(
            @"\bnamespace\s+(?<name>[A-Za-z_][A-Za-z0-9_\.]*)",
            RegexOptions.CultureInvariant);
        private static readonly Regex fieldRegex = new Regex(
            @"(?<attributes>(?:\s*\[[^\]]+\]\s*)+)" +
            @"(?<modifiers>(?:(?:public|private|protected|internal|static|readonly|const|volatile|new)\s+)*)" +
            @"(?<type>(?:global::)?(?:bool|int|float|System\.Boolean|System\.Int32|System\.Single))\s+" +
            @"(?<name>@?[A-Za-z_][A-Za-z0-9_]*)\s*(?:=[^;]*)?;",
            RegexOptions.CultureInvariant);
        private static readonly Regex parameterAttributeRegex = new Regex(
            @"\bFSMParameter(?:Attribute)?\b",
            RegexOptions.CultureInvariant);
        private static readonly Regex triggerAttributeRegex = new Regex(
            @"\bFSMTrigger(?:Attribute)?\b",
            RegexOptions.CultureInvariant);

        public static void GenerateAll()
        {
            if (!TryBeginGeneration())
                return;

            try
            {
                string[] assetPaths = AssetDatabase.GetAllAssetPaths();
                for (int i = 0; i < assetPaths.Length; i++)
                {
                    string assetPath = assetPaths[i];
                    if (IsSourceScript(assetPath))
                        Generate(assetPath, false);
                }
            }
            finally
            {
                EndGeneration();
            }
        }

        public static void GenerateChanged(IEnumerable<string> assetPaths)
        {
            if (assetPaths == null || !TryBeginGeneration())
                return;

            try
            {
                foreach (string assetPath in assetPaths)
                {
                    if (IsSourceScript(assetPath))
                        Generate(assetPath, true);
                }
            }
            finally
            {
                EndGeneration();
            }
        }

        /// <summary>
        /// 생성 파일 Import가 다시 Asset 콜백을 호출해도 중첩 생성을 시작하지 않는다.
        /// </summary>
        private static bool TryBeginGeneration()
        {
            if (isGenerating)
                return false;

            isGenerating = true;
            return true;
        }

        private static void EndGeneration() => isGenerating = false;

        private static bool IsSourceScript(string assetPath)
        {
            return !string.IsNullOrEmpty(assetPath) &&
                assetPath.StartsWith("Assets/", StringComparison.Ordinal) &&
                assetPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) &&
                !assetPath.EndsWith(GeneratedSuffix, StringComparison.OrdinalIgnoreCase);
        }

        private static void Generate(string sourcePath, bool logErrors)
        {
            if (!File.Exists(sourcePath))
                return;

            string source = File.ReadAllText(sourcePath);
            string generatedPath = GetGeneratedPath(sourcePath);
            bool mayContainBinding = source.IndexOf("FSMParameter", StringComparison.Ordinal) >= 0 ||
                source.IndexOf("FSMTrigger", StringComparison.Ordinal) >= 0;

            try
            {
                // 대부분의 스크립트는 바인딩 대상이 아니므로 정규식 파싱 전에 제외한다.
                if (!mayContainBinding)
                {
                    DeleteGeneratedFile(generatedPath);
                    return;
                }

                if (!TryParseSource(sourcePath, source, out FSMParameterSourceDefinition definition))
                {
                    return;
                }

                string generatedSource = BuildSource(definition);
                if (File.Exists(generatedPath) &&
                    string.Equals(File.ReadAllText(generatedPath), generatedSource, StringComparison.Ordinal))
                    return;

                File.WriteAllText(generatedPath, generatedSource, new UTF8Encoding(false));
                ScheduleAssetRefresh();
            }
            catch (Exception exception)
            {
                if (logErrors || mayContainBinding)
                    Debug.LogError($"FSM Parameter source generation failed for '{sourcePath}'.\n{exception}");
            }
        }

        /// <summary>
        /// 메뉴와 Asset 콜백 실행이 끝난 다음 생성 파일을 한 번에 Import한다.
        /// </summary>
        internal static void ScheduleAssetRefresh()
        {
            if (refreshScheduled)
                return;

            refreshScheduled = true;
            EditorApplication.delayCall += RefreshGeneratedAssets;
        }

        private static void RefreshGeneratedAssets()
        {
            EditorApplication.delayCall -= RefreshGeneratedAssets;
            refreshScheduled = false;
            AssetDatabase.Refresh();
        }

        private static void DeleteGeneratedFile(string generatedPath)
        {
            if (!File.Exists(generatedPath))
                return;

            File.Delete(generatedPath);
            string metaPath = generatedPath + ".meta";
            if (File.Exists(metaPath))
                File.Delete(metaPath);
            ScheduleAssetRefresh();
        }

        private static bool TryParseSource(
            string sourcePath,
            string source,
            out FSMParameterSourceDefinition definition)
        {
            definition = null;
            string sanitizedSource = SanitizeSource(source);
            string className = Path.GetFileNameWithoutExtension(sourcePath);
            Match classMatch = FindClass(sanitizedSource, className);
            if (!classMatch.Success)
                return false;

            int classBodyStart = sanitizedSource.IndexOf('{', classMatch.Index + classMatch.Length);
            if (classBodyStart < 0)
                throw new InvalidOperationException($"Class '{className}' has no body.");
            int classBodyEnd = FindMatchingBrace(sanitizedSource, classBodyStart);
            if (classBodyEnd < 0)
                throw new InvalidOperationException($"Class '{className}' has an incomplete body.");

            string modifiers = classMatch.Groups["modifiers"].Value;
            string declarationTail = sanitizedSource.Substring(
                classMatch.Index + classMatch.Length,
                classBodyStart - classMatch.Index - classMatch.Length);
            if (declarationTail.IndexOf('<') >= 0)
                throw new InvalidOperationException(
                    $"FSM Parameter binding does not support generic type '{className}'.");

            string classBody = sanitizedSource.Substring(
                classBodyStart + 1,
                classBodyEnd - classBodyStart - 1);
            var fields = new List<FSMParameterSourceField>();
            foreach (Match fieldMatch in fieldRegex.Matches(classBody))
            {
                if (GetBraceDepth(classBody, fieldMatch.Index) != 0)
                    continue;

                string attributes = fieldMatch.Groups["attributes"].Value;
                bool isParameter = parameterAttributeRegex.IsMatch(attributes);
                bool isTrigger = triggerAttributeRegex.IsMatch(attributes);
                if (!isParameter && !isTrigger)
                    continue;
                if (isParameter && isTrigger)
                    throw new InvalidOperationException(
                        $"Field '{className}.{fieldMatch.Groups["name"].Value}' cannot use both " +
                        "FSMParameterAttribute and FSMTriggerAttribute.");

                string fieldModifiers = fieldMatch.Groups["modifiers"].Value;
                if (ContainsModifier(fieldModifiers, "static") ||
                    ContainsModifier(fieldModifiers, "readonly") ||
                    ContainsModifier(fieldModifiers, "const"))
                    throw new InvalidOperationException(
                        $"FSM Parameter field '{className}.{fieldMatch.Groups["name"].Value}' " +
                        "must be a writable instance field.");

                string fieldName = fieldMatch.Groups["name"].Value.TrimStart('@');
                FSMParameterType parameterType = GetParameterType(
                    fieldMatch.Groups["type"].Value,
                    isTrigger,
                    className,
                    fieldName);
                fields.Add(new FSMParameterSourceField(fieldName, parameterType));
            }

            if (fields.Count == 0)
                return false;
            if (!ContainsModifier(modifiers, "partial"))
                throw new InvalidOperationException(
                    $"FSM Parameter source class '{className}' must be declared partial.");

            string namespaceName = FindNamespace(sanitizedSource, classMatch.Index);
            string assemblyName = CompilationPipeline.GetAssemblyNameFromScriptPath(sourcePath);
            if (string.IsNullOrEmpty(assemblyName))
                assemblyName = "Assembly-CSharp";
            else if (assemblyName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                assemblyName = Path.GetFileNameWithoutExtension(assemblyName);
            string fullTypeName = string.IsNullOrEmpty(namespaceName)
                ? className
                : $"{namespaceName}.{className}";
            string sourceTypeID = $"{assemblyName}:{fullTypeName}";

            for (int i = 0; i < fields.Count; i++)
                fields[i].SetID(FSMParameterKey.GetHash($"{sourceTypeID}:{fields[i].GetName()}"));

            definition = new FSMParameterSourceDefinition(
                namespaceName,
                className,
                ContainsModifier(modifiers, "public"),
                fields);
            return true;
        }

        private static Match FindClass(string source, string className)
        {
            var classRegex = new Regex(
                @"(?<modifiers>(?:(?:public|internal|abstract|sealed|static|partial|new)\s+)*)" +
                @"\bclass\s+@?" + Regex.Escape(className) + @"\b",
                RegexOptions.CultureInvariant);
            return classRegex.Match(source);
        }

        private static string FindNamespace(string source, int classIndex)
        {
            string namespaceName = string.Empty;
            foreach (Match match in namespaceRegex.Matches(source))
            {
                if (match.Index >= classIndex)
                    break;
                namespaceName = match.Groups["name"].Value;
            }
            return namespaceName;
        }

        private static FSMParameterType GetParameterType(
            string typeName,
            bool isTrigger,
            string className,
            string fieldName)
        {
            string normalizedType = typeName.Replace("global::", string.Empty);
            if (isTrigger)
            {
                if (normalizedType == "bool" || normalizedType == "System.Boolean")
                    return FSMParameterType.Trigger;
                throw new InvalidOperationException(
                    $"FSM Trigger field '{className}.{fieldName}' must be bool.");
            }

            if (normalizedType == "bool" || normalizedType == "System.Boolean")
                return FSMParameterType.Bool;
            if (normalizedType == "int" || normalizedType == "System.Int32")
                return FSMParameterType.Int;
            if (normalizedType == "float" || normalizedType == "System.Single")
                return FSMParameterType.Float;

            throw new InvalidOperationException(
                $"FSM Parameter field '{className}.{fieldName}' has unsupported type '{typeName}'.");
        }

        private static string BuildSource(FSMParameterSourceDefinition definition)
        {
            var builder = new StringBuilder(1024);
            builder.AppendLine("// <auto-generated />");
            builder.AppendLine();
            string namespaceName = definition.GetNamespace();
            if (!string.IsNullOrEmpty(namespaceName))
            {
                builder.Append("namespace ").Append(namespaceName).AppendLine();
                builder.AppendLine("{");
            }

            string indent = string.IsNullOrEmpty(namespaceName) ? string.Empty : "    ";
            builder.Append(indent)
                .Append(definition.GetIsPublic() ? "public " : "internal ")
                .Append("partial class @")
                .Append(definition.GetClassName())
                .AppendLine(" : UnityFramework.FSM.IFSMParameterBinder");
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).AppendLine("    void UnityFramework.FSM.IFSMParameterBinder.SyncFSMParameters(");
            builder.Append(indent).AppendLine("        UnityFramework.FSM.IStateMachine stateMachine)");
            builder.Append(indent).AppendLine("    {");

            IReadOnlyList<FSMParameterSourceField> fields = definition.GetFields();
            for (int i = 0; i < fields.Count; i++)
                AppendFieldBinding(builder, indent, fields[i]);

            builder.Append(indent).AppendLine("    }");
            builder.Append(indent).AppendLine("}");
            if (!string.IsNullOrEmpty(namespaceName))
                builder.AppendLine("}");
            return builder.ToString();
        }

        private static void AppendFieldBinding(
            StringBuilder builder,
            string indent,
            FSMParameterSourceField field)
        {
            if (field.GetParameterType() == FSMParameterType.Trigger)
            {
                builder.Append(indent).Append("        if (this.@").Append(field.GetName()).AppendLine(")");
                builder.Append(indent).AppendLine("        {");
                builder.Append(indent).Append("            stateMachine.SetTrigger(").Append(field.GetID()).AppendLine(");");
                builder.Append(indent).Append("            this.@").Append(field.GetName()).AppendLine(" = false;");
                builder.Append(indent).AppendLine("        }");
                return;
            }

            string setterName;
            switch (field.GetParameterType())
            {
                case FSMParameterType.Bool: setterName = "SetBool"; break;
                case FSMParameterType.Int: setterName = "SetInt"; break;
                case FSMParameterType.Float: setterName = "SetFloat"; break;
                default: throw new InvalidOperationException("Unsupported FSM Parameter type.");
            }

            builder.Append(indent)
                .Append("        stateMachine.")
                .Append(setterName)
                .Append('(')
                .Append(field.GetID())
                .Append(", this.@")
                .Append(field.GetName())
                .AppendLine(");");
        }

        private static string SanitizeSource(string source)
        {
            var result = new StringBuilder(source.Length);
            bool isLineComment = false;
            bool isBlockComment = false;
            bool isString = false;
            bool isVerbatimString = false;
            bool isCharacter = false;

            for (int i = 0; i < source.Length; i++)
            {
                char current = source[i];
                char next = i + 1 < source.Length ? source[i + 1] : '\0';

                if (isLineComment)
                {
                    if (current == '\n')
                    {
                        isLineComment = false;
                        result.Append('\n');
                    }
                    else
                        result.Append(' ');
                    continue;
                }
                if (isBlockComment)
                {
                    if (current == '*' && next == '/')
                    {
                        result.Append("  ");
                        i++;
                        isBlockComment = false;
                    }
                    else
                        result.Append(current == '\n' ? '\n' : ' ');
                    continue;
                }
                if (isString || isCharacter)
                {
                    if (!isVerbatimString && current == '\\')
                    {
                        result.Append("  ");
                        i++;
                        continue;
                    }
                    if (isVerbatimString && current == '"' && next == '"')
                    {
                        result.Append("  ");
                        i++;
                        continue;
                    }
                    if ((isString && current == '"') || (isCharacter && current == '\''))
                    {
                        isString = false;
                        isCharacter = false;
                        isVerbatimString = false;
                    }
                    result.Append(current == '\n' ? '\n' : ' ');
                    continue;
                }

                if (current == '/' && next == '/')
                {
                    result.Append("  ");
                    i++;
                    isLineComment = true;
                }
                else if (current == '/' && next == '*')
                {
                    result.Append("  ");
                    i++;
                    isBlockComment = true;
                }
                else if (current == '@' && next == '"')
                {
                    result.Append("  ");
                    i++;
                    isString = true;
                    isVerbatimString = true;
                }
                else if (current == '"')
                {
                    result.Append(' ');
                    isString = true;
                }
                else if (current == '\'')
                {
                    result.Append(' ');
                    isCharacter = true;
                }
                else
                    result.Append(current);
            }
            return result.ToString();
        }

        private static int FindMatchingBrace(string source, int openingBraceIndex)
        {
            int depth = 0;
            for (int i = openingBraceIndex; i < source.Length; i++)
            {
                if (source[i] == '{')
                    depth++;
                else if (source[i] == '}' && --depth == 0)
                    return i;
            }
            return -1;
        }

        private static int GetBraceDepth(string source, int endIndex)
        {
            int depth = 0;
            for (int i = 0; i < endIndex; i++)
            {
                if (source[i] == '{')
                    depth++;
                else if (source[i] == '}')
                    depth--;
            }
            return depth;
        }

        private static bool ContainsModifier(string modifiers, string modifier)
        {
            return Regex.IsMatch(
                modifiers,
                @"(?:^|\s)" + Regex.Escape(modifier) + @"(?:\s|$)",
                RegexOptions.CultureInvariant);
        }

        private static string GetGeneratedPath(string sourcePath)
        {
            return sourcePath.Substring(0, sourcePath.Length - ".cs".Length) + GeneratedSuffix;
        }
    }

    internal sealed class FSMParameterBindingSaveProcessor : AssetModificationProcessor
    {
        private static string[] OnWillSaveAssets(string[] paths)
        {
            FSMParameterSourceGenerator.GenerateChanged(paths);
            return paths;
        }
    }

    internal sealed class FSMParameterBindingAssetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            FSMParameterSourceGenerator.GenerateChanged(importedAssets);
            FSMParameterSourceGenerator.GenerateChanged(movedAssets);
        }
    }

    internal sealed class FSMParameterSourceDefinition
    {
        private readonly string namespaceName;
        private readonly string className;
        private readonly bool isPublic;
        private readonly List<FSMParameterSourceField> fields;

        public FSMParameterSourceDefinition(
            string namespaceName,
            string className,
            bool isPublic,
            List<FSMParameterSourceField> fields)
        {
            this.namespaceName = namespaceName;
            this.className = className;
            this.isPublic = isPublic;
            this.fields = fields;
        }

        public string GetNamespace() => this.namespaceName;
        public string GetClassName() => this.className;
        public bool GetIsPublic() => this.isPublic;
        public IReadOnlyList<FSMParameterSourceField> GetFields() => this.fields;
    }

    internal sealed class FSMParameterSourceField
    {
        private readonly string name;
        private readonly FSMParameterType parameterType;
        private int id;

        public FSMParameterSourceField(string name, FSMParameterType parameterType)
        {
            this.name = name;
            this.parameterType = parameterType;
        }

        public string GetName() => this.name;
        public FSMParameterType GetParameterType() => this.parameterType;
        public int GetID() => this.id;
        public void SetID(int id) => this.id = id;
    }
}
