using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

using UnityEditor;
using UnityEngine;

namespace UnityFramework.Utility.Editor
{
    internal static class DeepCopyGenerator
    {
        private const string MenuPath = "Assets/UnityFramework/Deep Copy/Generate";
        private const string GeneratedSuffix = ".generated.cs";

        [MenuItem(MenuPath, false, 2000)]
        private static void GenerateSelected()
        {
            MonoScript selectedScript = Selection.activeObject as MonoScript;
            string selectedPath = AssetDatabase.GetAssetPath(selectedScript);
            if (!IsSourceScript(selectedPath))
                return;

            Type selectedType = selectedScript.GetClass();
            if (selectedType == null)
            {
                Debug.LogError(
                    $"Deep copy generation failed. '{selectedPath}' does not expose a compiled class. " +
                    "Check compile errors and ensure the file name matches the class name.",
                    selectedScript);
                return;
            }

            if (!HasDeepCopyAttribute(selectedType))
            {
                Debug.LogError(
                    $"Deep copy generation stopped. '{selectedType.FullName}' does not have [DeepCopy].",
                    selectedScript);
                return;
            }

            try
            {
                Dictionary<Type, string> sourcePaths = BuildSourcePathMap(selectedType, selectedPath);
                Dictionary<Type, GenerationTarget> targets = new Dictionary<Type, GenerationTarget>();
                CollectTarget(selectedType, sourcePaths, targets, new HashSet<Type>());

                Dictionary<string, string> outputs = new Dictionary<string, string>();
                foreach (GenerationTarget target in targets.Values)
                    outputs.Add(target.GeneratedPath, BuildGeneratedSource(target));

                int changedCount = WriteOutputs(outputs);
                Debug.Log(
                    $"Deep copy generation completed for '{selectedType.FullName}'. " +
                    $"Generated types: {targets.Count}, changed files: {changedCount}.",
                    selectedScript);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"Deep copy generation failed for '{selectedType.FullName}'.\n{exception.Message}",
                    selectedScript);
            }
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateGenerateSelected()
        {
            if (Selection.objects.Length != 1 || !(Selection.activeObject is MonoScript script))
                return false;

            return IsSourceScript(AssetDatabase.GetAssetPath(script));
        }

        private static Dictionary<Type, string> BuildSourcePathMap(Type selectedType, string selectedPath)
        {
            Dictionary<Type, string> sourcePaths = new Dictionary<Type, string>
            {
                [selectedType] = selectedPath
            };

            string[] scriptGuids = AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets" });
            for (int i = 0; i < scriptGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(scriptGuids[i]);
                if (!IsSourceScript(path))
                    continue;

                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                Type type = script != null ? script.GetClass() : null;
                if (type != null && !sourcePaths.ContainsKey(type))
                    sourcePaths.Add(type, path);
            }
            return sourcePaths;
        }

        private static void CollectTarget(
            Type type,
            IReadOnlyDictionary<Type, string> sourcePaths,
            IDictionary<Type, GenerationTarget> targets,
            ISet<Type> visiting)
        {
            if (targets.ContainsKey(type))
                return;
            if (!visiting.Add(type))
                return;

            ValidateTargetType(type, sourcePaths, out string sourcePath);

            Type deepCopyParent = HasDeepCopyAttribute(type.BaseType) ? type.BaseType : null;
            if (deepCopyParent != null)
                CollectTarget(deepCopyParent, sourcePaths, targets, visiting);

            FieldInfo[] fields = type.GetFields(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.DeclaredOnly);

            List<FieldCopyDefinition> copiedFields = new List<FieldCopyDefinition>();
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                DeepCopyWrapperFieldAttribute wrapperAttribute =
                    field.GetCustomAttribute<DeepCopyWrapperFieldAttribute>(false);

                if (field.IsStatic || field.IsLiteral)
                {
                    if (wrapperAttribute != null)
                    {
                        throw new InvalidOperationException(
                            $"Wrapper field '{type.FullName}.{field.Name}' must be an instance field.");
                    }
                    continue;
                }

                if (wrapperAttribute != null)
                {
                    WrapperCopyDefinition wrapper = CreateWrapperDefinition(
                        type,
                        field,
                        wrapperAttribute);
                    copiedFields.Add(new FieldCopyDefinition(field, wrapper));

                    HashSet<Type> wrapperDependencies = new HashSet<Type>();
                    CollectAttributedTypes(wrapper.ValueType, wrapperDependencies);
                    foreach (Type dependency in wrapperDependencies)
                        CollectTarget(dependency, sourcePaths, targets, visiting);
                    continue;
                }

                if (!NeedsDeepCopy(field.FieldType))
                    continue;

                ValidateField(type, field);
                copiedFields.Add(new FieldCopyDefinition(field, null));

                HashSet<Type> dependencies = new HashSet<Type>();
                CollectAttributedTypes(field.FieldType, dependencies);
                foreach (Type dependency in dependencies)
                    CollectTarget(dependency, sourcePaths, targets, visiting);
            }

            string generatedPath = sourcePath.Substring(0, sourcePath.Length - ".cs".Length) +
                GeneratedSuffix;
            targets.Add(
                type,
                new GenerationTarget(type, sourcePath, generatedPath, deepCopyParent, copiedFields));
            visiting.Remove(type);
        }

        private static void ValidateTargetType(
            Type type,
            IReadOnlyDictionary<Type, string> sourcePaths,
            out string sourcePath)
        {
            if (!type.IsClass || type.IsNested || type.IsGenericType || type.ContainsGenericParameters)
            {
                throw new InvalidOperationException(
                    $"'{type.FullName}' must be a non-generic top-level class.");
            }
            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                throw new InvalidOperationException(
                    $"'{type.FullName}' derives from UnityEngine.Object and cannot use generated deep copy.");
            }
            if (!sourcePaths.TryGetValue(type, out sourcePath))
            {
                throw new InvalidOperationException(
                    $"The source script for '{type.FullName}' could not be found under Assets.");
            }
            if (!HasPartialClassDeclaration(sourcePath, type.Name))
            {
                throw new InvalidOperationException(
                    $"'{type.FullName}' must be declared as a partial class.");
            }
        }

        private static void ValidateField(Type declaringType, FieldInfo field)
        {
            Type fieldType = field.FieldType;
            string fieldName = $"{declaringType.FullName}.{field.Name}";

            if (field.IsDefined(typeof(CompilerGeneratedAttribute), false))
            {
                throw new InvalidOperationException(
                    $"Auto-property backing field '{fieldName}' cannot be generated. Use a field instead.");
            }
            if (field.IsInitOnly)
            {
                throw new InvalidOperationException(
                    $"Readonly reference field '{fieldName}' cannot be deep copied.");
            }
            if (typeof(Delegate).IsAssignableFrom(fieldType))
            {
                throw new InvalidOperationException(
                    $"Delegate field '{fieldName}' cannot be deep copied.");
            }

            ValidateReferenceType(fieldType, fieldName, false);
        }

        private static WrapperCopyDefinition CreateWrapperDefinition(
            Type declaringType,
            FieldInfo field,
            DeepCopyWrapperFieldAttribute attribute)
        {
            string fieldName = $"{declaringType.FullName}.{field.Name}";
            Type wrapperType = field.FieldType;

            if (field.IsDefined(typeof(CompilerGeneratedAttribute), false))
            {
                throw new InvalidOperationException(
                    $"Wrapper field '{fieldName}' cannot be an auto-property backing field.");
            }
            if (field.IsInitOnly)
            {
                throw new InvalidOperationException(
                    $"Wrapper field '{fieldName}' cannot be readonly.");
            }
            if (!wrapperType.IsClass || wrapperType.IsAbstract || wrapperType.IsNested ||
                typeof(UnityEngine.Object).IsAssignableFrom(wrapperType))
            {
                throw new InvalidOperationException(
                    $"Wrapper field '{fieldName}' must use a concrete top-level managed class.");
            }
            if (!wrapperType.IsPublic && wrapperType.Assembly != declaringType.Assembly)
            {
                throw new InvalidOperationException(
                    $"Wrapper type '{wrapperType.FullName}' is not accessible from '{declaringType.FullName}'.");
            }
            if (string.IsNullOrWhiteSpace(attribute.SetterName))
            {
                throw new InvalidOperationException(
                    $"Wrapper field '{fieldName}' must specify a setter member name.");
            }

            ConstructorInfo constructor = wrapperType.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null);
            if (constructor == null || !IsAccessible(constructor, declaringType))
            {
                throw new InvalidOperationException(
                    $"Wrapper type '{wrapperType.FullName}' needs an accessible parameterless constructor.");
            }

            MemberAccessDefinition getter;
            MemberAccessDefinition setter;
            if (!string.IsNullOrWhiteSpace(attribute.GetterName))
            {
                getter = FindSingleGetter(
                    wrapperType,
                    attribute.GetterName,
                    declaringType,
                    fieldName);
                setter = FindSingleSetter(
                    wrapperType,
                    attribute.SetterName,
                    getter.ValueType,
                    declaringType,
                    fieldName);
            }
            else
            {
                List<MemberAccessDefinition> setters = FindSetters(
                    wrapperType,
                    attribute.SetterName,
                    null,
                    declaringType);
                List<MemberAccessDefinition> validSetters = new List<MemberAccessDefinition>();
                for (int i = 0; i < setters.Count; i++)
                {
                    if (HasConversionOperator(wrapperType, setters[i].ValueType))
                        validSetters.Add(setters[i]);
                }

                if (validSetters.Count != 1)
                {
                    throw new InvalidOperationException(
                        $"Wrapper field '{fieldName}' must resolve exactly one setter paired with an " +
                        "implicit or explicit conversion from the wrapper to the setter value type.");
                }

                setter = validSetters[0];
                getter = new MemberAccessDefinition(
                    string.Empty,
                    WrapperMemberKind.Conversion,
                    setter.ValueType);
            }

            ValidateReferenceType(getter.ValueType, fieldName + " wrapped value", false);
            return new WrapperCopyDefinition(wrapperType, getter.ValueType, getter, setter);
        }

        private static MemberAccessDefinition FindSingleGetter(
            Type wrapperType,
            string memberName,
            Type declaringType,
            string fieldName)
        {
            List<MemberAccessDefinition> getters = new List<MemberAccessDefinition>();
            PropertyInfo property = wrapperType.GetProperty(
                memberName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.GetIndexParameters().Length == 0)
            {
                MethodInfo getMethod = property.GetGetMethod(true);
                if (getMethod != null && IsAccessible(getMethod, declaringType))
                {
                    getters.Add(new MemberAccessDefinition(
                        property.Name,
                        WrapperMemberKind.Property,
                        property.PropertyType));
                }
            }

            MethodInfo[] methods = wrapperType.GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (method.Name != memberName || method.IsSpecialName ||
                    method.ReturnType == typeof(void) || method.GetParameters().Length != 0 ||
                    !IsAccessible(method, declaringType))
                    continue;

                getters.Add(new MemberAccessDefinition(
                    method.Name,
                    WrapperMemberKind.Method,
                    method.ReturnType));
            }

            if (getters.Count != 1)
            {
                throw new InvalidOperationException(
                    $"Wrapper field '{fieldName}' getter '{memberName}' must resolve to exactly one " +
                    "readable property or parameterless instance method.");
            }
            return getters[0];
        }

        private static MemberAccessDefinition FindSingleSetter(
            Type wrapperType,
            string memberName,
            Type valueType,
            Type declaringType,
            string fieldName)
        {
            List<MemberAccessDefinition> setters = FindSetters(
                wrapperType,
                memberName,
                valueType,
                declaringType);
            if (setters.Count != 1)
            {
                throw new InvalidOperationException(
                    $"Wrapper field '{fieldName}' setter '{memberName}' must resolve to exactly one " +
                    $"writable property or void instance method accepting '{valueType.FullName}'.");
            }
            return setters[0];
        }

        private static List<MemberAccessDefinition> FindSetters(
            Type wrapperType,
            string memberName,
            Type requiredValueType,
            Type declaringType)
        {
            List<MemberAccessDefinition> setters = new List<MemberAccessDefinition>();
            PropertyInfo property = wrapperType.GetProperty(
                memberName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.GetIndexParameters().Length == 0 &&
                (requiredValueType == null || property.PropertyType == requiredValueType))
            {
                MethodInfo setMethod = property.GetSetMethod(true);
                if (setMethod != null && IsAccessible(setMethod, declaringType))
                {
                    setters.Add(new MemberAccessDefinition(
                        property.Name,
                        WrapperMemberKind.Property,
                        property.PropertyType));
                }
            }

            MethodInfo[] methods = wrapperType.GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                ParameterInfo[] parameters = method.GetParameters();
                if (method.Name != memberName || method.IsSpecialName ||
                    method.ReturnType != typeof(void) || parameters.Length != 1 ||
                    (requiredValueType != null && parameters[0].ParameterType != requiredValueType) ||
                    !IsAccessible(method, declaringType))
                    continue;

                setters.Add(new MemberAccessDefinition(
                    method.Name,
                    WrapperMemberKind.Method,
                    parameters[0].ParameterType));
            }
            return setters;
        }

        private static bool HasConversionOperator(Type wrapperType, Type valueType)
        {
            MethodInfo[] methods = wrapperType.GetMethods(
                BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (!method.IsSpecialName ||
                    (method.Name != "op_Implicit" && method.Name != "op_Explicit") ||
                    method.ReturnType != valueType)
                    continue;

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 1 && parameters[0].ParameterType == wrapperType)
                    return true;
            }
            return false;
        }

        private static bool IsAccessible(MethodBase method, Type accessingType)
        {
            if (method.IsPublic)
                return true;
            if (method.DeclaringType == accessingType)
                return true;

            bool sameAssembly = method.DeclaringType != null &&
                method.DeclaringType.Assembly == accessingType.Assembly;
            return sameAssembly && (method.IsAssembly || method.IsFamilyOrAssembly);
        }

        private static void ValidateReferenceType(Type type, string fieldName, bool insideContainer)
        {
            if (!NeedsDeepCopy(type))
                return;

            if (type.IsArray)
            {
                if (type.GetArrayRank() != 1)
                {
                    throw new InvalidOperationException(
                        $"Field '{fieldName}' uses a multidimensional array, which is not supported.");
                }
                if (insideContainer)
                    ThrowNestedContainer(fieldName);
                ValidateReferenceType(type.GetElementType(), fieldName, true);
                return;
            }

            if (type.IsGenericType && IsSupportedCollection(type.GetGenericTypeDefinition()))
            {
                if (insideContainer)
                    ThrowNestedContainer(fieldName);
                Type[] arguments = type.GetGenericArguments();
                for (int i = 0; i < arguments.Length; i++)
                    ValidateReferenceType(arguments[i], fieldName, true);
                return;
            }

            if (type == typeof(object) || type.IsInterface || type.IsAbstract || HasDeepCopyAttribute(type))
                return;

            throw new InvalidOperationException(
                $"Reference field '{fieldName}' uses '{type.FullName}', which does not have [DeepCopy].");
        }

        private static void ThrowNestedContainer(string fieldName)
        {
            throw new InvalidOperationException(
                $"Field '{fieldName}' uses nested collections. Wrap the nested collection in a [DeepCopy] class.");
        }

        private static void CollectAttributedTypes(Type type, ISet<Type> dependencies)
        {
            if (!NeedsDeepCopy(type))
                return;

            if (HasDeepCopyAttribute(type))
            {
                dependencies.Add(type);
                return;
            }

            if (type.IsArray)
            {
                CollectAttributedTypes(type.GetElementType(), dependencies);
                return;
            }

            if (!type.IsGenericType || !IsSupportedCollection(type.GetGenericTypeDefinition()))
                return;

            Type[] arguments = type.GetGenericArguments();
            for (int i = 0; i < arguments.Length; i++)
                CollectAttributedTypes(arguments[i], dependencies);
        }

        private static bool NeedsDeepCopy(Type type)
        {
            return type != null &&
                !type.IsValueType &&
                type != typeof(string) &&
                type != typeof(Type) &&
                !typeof(UnityEngine.Object).IsAssignableFrom(type);
        }

        private static bool IsSupportedCollection(Type genericTypeDefinition)
        {
            return genericTypeDefinition == typeof(List<>) ||
                genericTypeDefinition == typeof(Dictionary<,>) ||
                genericTypeDefinition == typeof(HashSet<>);
        }

        private static bool HasDeepCopyAttribute(Type type)
        {
            return type != null && type.IsDefined(typeof(DeepCopyAttribute), false);
        }

        private static string BuildGeneratedSource(GenerationTarget target)
        {
            Type type = target.Type;
            string className = EscapeIdentifier(type.Name);
            string helperName = GetHelperName(type);
            string namespaceName = type.Namespace;
            string indent = string.IsNullOrEmpty(namespaceName) ? string.Empty : "    ";
            StringBuilder builder = new StringBuilder(2048);

            builder.AppendLine("// <auto-generated />");
            builder.AppendLine();
            if (!string.IsNullOrEmpty(namespaceName))
            {
                builder.Append("namespace ").Append(EscapeNamespace(namespaceName)).AppendLine();
                builder.AppendLine("{");
            }

            builder.Append(indent)
                .Append(type.IsPublic ? "public " : "internal ")
                .Append("partial class ")
                .Append(className)
                .AppendLine(" : global::UnityFramework.Utility.IDeepCopyGenerated");
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).AppendLine(
                "    object global::UnityFramework.Utility.IDeepCopyGenerated.DeepCopyGenerated(");
            builder.Append(indent).AppendLine(
                "        global::UnityFramework.Utility.DeepCopyContext context)");
            builder.Append(indent).AppendLine("    {");
            builder.Append(indent).AppendLine("        if (context == null)");
            builder.Append(indent).AppendLine(
                "            throw new global::System.ArgumentNullException(nameof(context));");
            builder.AppendLine();
            builder.Append(indent).AppendLine(
                "        if (context.TryGetCopy(this, out object existingCopy))");
            builder.Append(indent).AppendLine("            return existingCopy;");
            builder.AppendLine();
            builder.Append(indent).Append("        ").Append(className)
                .Append(" copy = (").Append(className).AppendLine(")MemberwiseClone();");
            builder.Append(indent).AppendLine("        context.RegisterCopy(this, copy);");
            builder.Append(indent).Append("        ").Append(helperName)
                .AppendLine("(copy, context);");
            builder.Append(indent).AppendLine("        return copy;");
            builder.Append(indent).AppendLine("    }");
            builder.AppendLine();

            builder.Append(indent)
                .Append(type.IsSealed ? "    private void " : "    protected void ")
                .Append(helperName)
                .Append('(')
                .Append(className)
                .AppendLine(" target,");
            builder.Append(indent).AppendLine(
                "        global::UnityFramework.Utility.DeepCopyContext context)");
            builder.Append(indent).AppendLine("    {");

            if (target.DeepCopyParent != null)
            {
                builder.Append(indent).Append("        base.")
                    .Append(GetHelperName(target.DeepCopyParent))
                    .AppendLine("(target, context);");
            }

            for (int i = 0; i < target.Fields.Count; i++)
            {
                FieldCopyDefinition field = target.Fields[i];
                if (field.Wrapper == null)
                    AppendStandardFieldCopy(builder, indent, field.Field);
                else
                    AppendWrapperFieldCopy(builder, indent, field);
            }

            builder.Append(indent).AppendLine("    }");
            builder.Append(indent).AppendLine("}");
            if (!string.IsNullOrEmpty(namespaceName))
                builder.AppendLine("}");
            return builder.ToString();
        }

        private static void AppendStandardFieldCopy(
            StringBuilder builder,
            string indent,
            FieldInfo field)
        {
            string fieldName = EscapeIdentifier(field.Name);
            builder.Append(indent).Append("        target.").Append(fieldName)
                .Append(" = global::UnityFramework.Utility.DeepCopyUtility.CopyReference(this.")
                .Append(fieldName)
                .AppendLine(", context);");
        }

        private static void AppendWrapperFieldCopy(
            StringBuilder builder,
            string indent,
            FieldCopyDefinition field)
        {
            string fieldName = EscapeIdentifier(field.Field.Name);
            string variableSuffix = SanitizeIdentifierPart(field.Field.Name);
            string wrapperTypeName = GetTypeName(field.Wrapper.WrapperType);
            string valueTypeName = GetTypeName(field.Wrapper.ValueType);
            string wrapperVariable = "__" + variableSuffix + "WrapperCopy";
            string valueVariable = "__" + variableSuffix + "WrappedValue";
            string copiedValueVariable = "__" + variableSuffix + "CopiedValue";
            string existingVariable = "__" + variableSuffix + "ExistingWrapperCopy";

            builder.Append(indent).Append("        if (this.").Append(fieldName).AppendLine(" == null)");
            builder.Append(indent).Append("            target.").Append(fieldName).AppendLine(" = null;");
            builder.Append(indent).Append("        else if (context.TryGetCopy(this.")
                .Append(fieldName).Append(", out object ").Append(existingVariable).AppendLine("))");
            builder.Append(indent).Append("            target.").Append(fieldName)
                .Append(" = (").Append(wrapperTypeName).Append(')').Append(existingVariable).AppendLine(";");
            builder.Append(indent).AppendLine("        else");
            builder.Append(indent).AppendLine("        {");
            builder.Append(indent).Append("            ").Append(wrapperTypeName).Append(' ')
                .Append(wrapperVariable).Append(" = new ").Append(wrapperTypeName).AppendLine("();");
            builder.Append(indent).Append("            context.RegisterCopy(this.").Append(fieldName)
                .Append(", ").Append(wrapperVariable).AppendLine(");");
            builder.Append(indent).Append("            ").Append(valueTypeName).Append(' ')
                .Append(valueVariable).Append(" = ")
                .Append(BuildGetterExpression("this." + fieldName, field.Wrapper.Getter))
                .AppendLine(";");
            builder.Append(indent).Append("            ").Append(valueTypeName).Append(' ')
                .Append(copiedValueVariable)
                .Append(" = global::UnityFramework.Utility.DeepCopyUtility.CopyReference(")
                .Append(valueVariable).AppendLine(", context);");
            AppendSetterStatement(
                builder,
                indent + "            ",
                wrapperVariable,
                copiedValueVariable,
                field.Wrapper.Setter);
            builder.Append(indent).Append("            target.").Append(fieldName).Append(" = ")
                .Append(wrapperVariable).AppendLine(";");
            builder.Append(indent).AppendLine("        }");
        }

        private static string BuildGetterExpression(
            string wrapperExpression,
            MemberAccessDefinition getter)
        {
            switch (getter.Kind)
            {
                case WrapperMemberKind.Conversion:
                    return "(" + GetTypeName(getter.ValueType) + ")" + wrapperExpression;
                case WrapperMemberKind.Method:
                    return wrapperExpression + "." + EscapeIdentifier(getter.Name) + "()";
                case WrapperMemberKind.Property:
                    return wrapperExpression + "." + EscapeIdentifier(getter.Name);
                default:
                    throw new InvalidOperationException("Unsupported wrapper getter kind.");
            }
        }

        private static void AppendSetterStatement(
            StringBuilder builder,
            string indent,
            string wrapperExpression,
            string valueExpression,
            MemberAccessDefinition setter)
        {
            builder.Append(indent).Append(wrapperExpression).Append('.')
                .Append(EscapeIdentifier(setter.Name));
            if (setter.Kind == WrapperMemberKind.Method)
                builder.Append('(').Append(valueExpression).AppendLine(");");
            else if (setter.Kind == WrapperMemberKind.Property)
                builder.Append(" = ").Append(valueExpression).AppendLine(";");
            else
                throw new InvalidOperationException("Unsupported wrapper setter kind.");
        }

        private static int WriteOutputs(IReadOnlyDictionary<string, string> outputs)
        {
            int changedCount = 0;
            foreach (KeyValuePair<string, string> output in outputs)
            {
                if (File.Exists(output.Key) &&
                    string.Equals(File.ReadAllText(output.Key), output.Value, StringComparison.Ordinal))
                    continue;

                File.WriteAllText(output.Key, output.Value, new UTF8Encoding(false));
                changedCount++;
            }

            if (changedCount > 0)
                AssetDatabase.Refresh();
            return changedCount;
        }

        private static bool HasPartialClassDeclaration(string sourcePath, string className)
        {
            string source = SanitizeSource(File.ReadAllText(sourcePath));
            Regex classRegex = new Regex(
                @"(?<modifiers>(?:(?:public|internal|protected|private|abstract|sealed|static|partial|new|unsafe)\s+)*)" +
                @"\bclass\s+@?" + Regex.Escape(className) + @"\b",
                RegexOptions.CultureInvariant);
            Match match = classRegex.Match(source);
            return match.Success && Regex.IsMatch(
                match.Groups["modifiers"].Value,
                @"(?:^|\s)partial(?:\s|$)",
                RegexOptions.CultureInvariant);
        }

        private static string SanitizeSource(string source)
        {
            StringBuilder result = new StringBuilder(source.Length);
            bool lineComment = false;
            bool blockComment = false;
            bool text = false;
            bool verbatimText = false;
            bool character = false;

            for (int i = 0; i < source.Length; i++)
            {
                char current = source[i];
                char next = i + 1 < source.Length ? source[i + 1] : '\0';

                if (lineComment)
                {
                    if (current == '\n')
                    {
                        lineComment = false;
                        result.Append('\n');
                    }
                    else
                        result.Append(' ');
                    continue;
                }
                if (blockComment)
                {
                    if (current == '*' && next == '/')
                    {
                        result.Append("  ");
                        i++;
                        blockComment = false;
                    }
                    else
                        result.Append(current == '\n' ? '\n' : ' ');
                    continue;
                }
                if (text || character)
                {
                    if (!verbatimText && current == '\\')
                    {
                        result.Append("  ");
                        i++;
                        continue;
                    }
                    if (verbatimText && current == '"' && next == '"')
                    {
                        result.Append("  ");
                        i++;
                        continue;
                    }
                    if ((text && current == '"') || (character && current == '\''))
                    {
                        text = false;
                        character = false;
                        verbatimText = false;
                    }
                    result.Append(current == '\n' ? '\n' : ' ');
                    continue;
                }

                if (current == '/' && next == '/')
                {
                    result.Append("  ");
                    i++;
                    lineComment = true;
                }
                else if (current == '/' && next == '*')
                {
                    result.Append("  ");
                    i++;
                    blockComment = true;
                }
                else if (current == '@' && next == '"')
                {
                    result.Append("  ");
                    i++;
                    text = true;
                    verbatimText = true;
                }
                else if (current == '"')
                {
                    result.Append(' ');
                    text = true;
                }
                else if (current == '\'')
                {
                    result.Append(' ');
                    character = true;
                }
                else
                    result.Append(current);
            }
            return result.ToString();
        }

        private static string GetHelperName(Type type)
        {
            StringBuilder builder = new StringBuilder("__DeepCopyFieldsTo_");
            string fullName = type.FullName ?? type.Name;
            for (int i = 0; i < fullName.Length; i++)
            {
                char character = fullName[i];
                builder.Append(char.IsLetterOrDigit(character) || character == '_' ? character : '_');
            }
            return builder.ToString();
        }

        private static string GetTypeName(Type type)
        {
            if (type.IsGenericParameter)
                return EscapeIdentifier(type.Name);
            if (type.IsArray)
            {
                string commas = new string(',', type.GetArrayRank() - 1);
                return GetTypeName(type.GetElementType()) + "[" + commas + "]";
            }
            if (type.IsGenericType)
            {
                Type genericDefinition = type.GetGenericTypeDefinition();
                string definitionName = genericDefinition.FullName ?? genericDefinition.Name;
                int arityIndex = definitionName.IndexOf('`');
                if (arityIndex >= 0)
                    definitionName = definitionName.Substring(0, arityIndex);

                Type[] arguments = type.GetGenericArguments();
                StringBuilder builder = new StringBuilder();
                builder.Append("global::").Append(EscapeQualifiedName(definitionName)).Append('<');
                for (int i = 0; i < arguments.Length; i++)
                {
                    if (i > 0)
                        builder.Append(", ");
                    builder.Append(GetTypeName(arguments[i]));
                }
                return builder.Append('>').ToString();
            }

            string fullName = type.FullName ?? type.Name;
            return "global::" + EscapeQualifiedName(fullName.Replace('+', '.'));
        }

        private static string EscapeQualifiedName(string qualifiedName)
        {
            string[] parts = qualifiedName.Split('.');
            for (int i = 0; i < parts.Length; i++)
                parts[i] = EscapeIdentifier(parts[i]);
            return string.Join(".", parts);
        }

        private static string SanitizeIdentifierPart(string value)
        {
            StringBuilder builder = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                builder.Append(char.IsLetterOrDigit(character) || character == '_' ? character : '_');
            }
            return builder.ToString();
        }

        private static string EscapeNamespace(string namespaceName)
        {
            string[] parts = namespaceName.Split('.');
            for (int i = 0; i < parts.Length; i++)
                parts[i] = EscapeIdentifier(parts[i]);
            return string.Join(".", parts);
        }

        private static string EscapeIdentifier(string identifier)
        {
            return "@" + identifier;
        }

        private static bool IsSourceScript(string assetPath)
        {
            return !string.IsNullOrEmpty(assetPath) &&
                assetPath.StartsWith("Assets/", StringComparison.Ordinal) &&
                assetPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) &&
                !assetPath.EndsWith(GeneratedSuffix, StringComparison.OrdinalIgnoreCase);
        }

        private sealed class GenerationTarget
        {
            public Type Type { get; }
            public string SourcePath { get; }
            public string GeneratedPath { get; }
            public Type DeepCopyParent { get; }
            public IReadOnlyList<FieldCopyDefinition> Fields { get; }

            public GenerationTarget(
                Type type,
                string sourcePath,
                string generatedPath,
                Type deepCopyParent,
                IReadOnlyList<FieldCopyDefinition> fields)
            {
                Type = type;
                SourcePath = sourcePath;
                GeneratedPath = generatedPath;
                DeepCopyParent = deepCopyParent;
                Fields = fields;
            }
        }

        private sealed class FieldCopyDefinition
        {
            public FieldInfo Field { get; }
            public WrapperCopyDefinition Wrapper { get; }

            public FieldCopyDefinition(FieldInfo field, WrapperCopyDefinition wrapper)
            {
                Field = field;
                Wrapper = wrapper;
            }
        }

        private sealed class WrapperCopyDefinition
        {
            public Type WrapperType { get; }
            public Type ValueType { get; }
            public MemberAccessDefinition Getter { get; }
            public MemberAccessDefinition Setter { get; }

            public WrapperCopyDefinition(
                Type wrapperType,
                Type valueType,
                MemberAccessDefinition getter,
                MemberAccessDefinition setter)
            {
                WrapperType = wrapperType;
                ValueType = valueType;
                Getter = getter;
                Setter = setter;
            }
        }

        private sealed class MemberAccessDefinition
        {
            public string Name { get; }
            public WrapperMemberKind Kind { get; }
            public Type ValueType { get; }

            public MemberAccessDefinition(string name, WrapperMemberKind kind, Type valueType)
            {
                Name = name;
                Kind = kind;
                ValueType = valueType;
            }
        }

        private enum WrapperMemberKind
        {
            Conversion,
            Method,
            Property
        }
    }
}
