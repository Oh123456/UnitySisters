using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.UIElements;

using UnityEngine;
using UnityEngine.UIElements;

namespace UnityFramework.CommandTree.Editor
{
    public sealed class CommandDataTypeGeneratorWindow : EditorWindow
    {
        private static readonly HashSet<string> csharpKeywords = new HashSet<string>
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class",
            "const", "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event",
            "explicit", "extern", "false", "finally", "fixed", "float", "for", "foreach", "goto", "if",
            "implicit", "in", "int", "interface", "internal", "is", "lock", "long", "namespace", "new",
            "null", "object", "operator", "out", "override", "params", "private", "protected", "public",
            "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static",
            "string", "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong",
            "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile", "while"
        };

        private readonly List<Type> keyTypes = new List<Type>();
        private readonly List<Type> filteredKeyTypes = new List<Type>();
        private readonly List<string> filteredKeyTypeNames = new List<string>();

        private TextField classNameField;
        private TextField namespaceField;
        private TextField keyTypeSearchField;
        private DropdownField keyTypeField;
        private ObjectField folderField;
        private TextField assetMenuField;
        private HelpBox statusBox;
        private Button generateButton;

        private Type selectedKeyType;

        public static void OpenWindow()
        {
            CommandDataTypeGeneratorWindow window = CreateInstance<CommandDataTypeGeneratorWindow>();
            window.titleContent = new GUIContent("Create Command Data Type");
            window.minSize = new Vector2(440.0f, 330.0f);
            window.maxSize = new Vector2(680.0f, 520.0f);
            window.ShowUtility();
            window.Focus();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 10.0f;
            rootVisualElement.style.paddingRight = 10.0f;
            rootVisualElement.style.paddingTop = 8.0f;
            rootVisualElement.style.paddingBottom = 8.0f;

            classNameField = new TextField("Class Name")
            {
                value = "InputCommandData"
            };
            classNameField.RegisterValueChangedCallback(changeEvent => UpdateGenerateButton());
            rootVisualElement.Add(classNameField);

            namespaceField = new TextField("Namespace");
            namespaceField.RegisterValueChangedCallback(changeEvent => UpdateGenerateButton());
            rootVisualElement.Add(namespaceField);

            keyTypeSearchField = new TextField("Key Type Search");
            keyTypeSearchField.RegisterValueChangedCallback(changeEvent => RefreshKeyTypeChoices(changeEvent.newValue));
            rootVisualElement.Add(keyTypeSearchField);

            keyTypeField = new DropdownField("Key Type");
            keyTypeField.RegisterValueChangedCallback(OnKeyTypeChanged);
            rootVisualElement.Add(keyTypeField);

            folderField = new ObjectField("Script Folder")
            {
                objectType = typeof(DefaultAsset),
                allowSceneObjects = false
            };
            folderField.RegisterValueChangedCallback(changeEvent => UpdateGenerateButton());
            rootVisualElement.Add(folderField);

            assetMenuField = new TextField("Asset Menu")
            {
                value = "UnityFramework/Command Data/Input Command Data"
            };
            rootVisualElement.Add(assetMenuField);

            statusBox = new HelpBox("Select a Key type and output folder.", HelpBoxMessageType.Info);
            statusBox.style.marginTop = 8.0f;
            rootVisualElement.Add(statusBox);

            VisualElement buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.justifyContent = Justify.FlexEnd;
            buttonRow.style.marginTop = 8.0f;

            Button cancelButton = new Button(Close)
            {
                text = "Cancel"
            };
            buttonRow.Add(cancelButton);

            generateButton = new Button(GenerateScript)
            {
                text = "Generate"
            };
            generateButton.style.marginLeft = 4.0f;
            buttonRow.Add(generateButton);
            rootVisualElement.Add(buttonRow);

            LoadKeyTypes();
            folderField.SetValueWithoutNotify(GetDefaultFolder());
            RefreshKeyTypeChoices(string.Empty);
            UpdateGenerateButton();
        }

        private void LoadKeyTypes()
        {
            keyTypes.Clear();
            AddKeyType(typeof(bool));
            AddKeyType(typeof(byte));
            AddKeyType(typeof(sbyte));
            AddKeyType(typeof(short));
            AddKeyType(typeof(ushort));
            AddKeyType(typeof(int));
            AddKeyType(typeof(uint));
            AddKeyType(typeof(long));
            AddKeyType(typeof(ulong));
            AddKeyType(typeof(char));
            AddKeyType(typeof(float));
            AddKeyType(typeof(double));
            AddKeyType(typeof(string));

            HashSet<string> playerAssemblyNames = new HashSet<string>(
                CompilationPipeline.GetAssemblies(AssembliesType.Player).Select(assembly => assembly.name));

            System.Reflection.Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int assemblyIndex = 0; assemblyIndex < assemblies.Length; assemblyIndex++)
            {
                System.Reflection.Assembly assembly = assemblies[assemblyIndex];
                if (assembly.IsDynamic || !playerAssemblyNames.Contains(assembly.GetName().Name))
                    continue;

                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException exception)
                {
                    types = exception.Types;
                }

                for (int typeIndex = 0; typeIndex < types.Length; typeIndex++)
                {
                    Type type = types[typeIndex];
                    if (!IsSupportedEnum(type))
                        continue;

                    AddKeyType(type);
                }
            }

            keyTypes.Sort((left, right) => string.Compare(GetKeyTypeDisplayName(left), GetKeyTypeDisplayName(right), StringComparison.OrdinalIgnoreCase));
        }

        private void AddKeyType(Type type)
        {
            if (type != null && !keyTypes.Contains(type))
                keyTypes.Add(type);
        }

        private bool IsSupportedEnum(Type type)
        {
            if (type == null || !type.IsEnum || (!type.IsPublic && !type.IsNestedPublic))
                return false;

            Type underlyingType = Enum.GetUnderlyingType(type);
            return underlyingType != typeof(long) && underlyingType != typeof(ulong);
        }

        private void RefreshKeyTypeChoices(string searchText)
        {
            filteredKeyTypes.Clear();
            filteredKeyTypeNames.Clear();

            for (int typeIndex = 0; typeIndex < keyTypes.Count; typeIndex++)
            {
                Type type = keyTypes[typeIndex];
                string displayName = GetKeyTypeDisplayName(type);
                if (!string.IsNullOrWhiteSpace(searchText) && displayName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                filteredKeyTypes.Add(type);
                filteredKeyTypeNames.Add(displayName);
            }

            keyTypeField.choices = new List<string>(filteredKeyTypeNames);

            int selectedIndex = selectedKeyType == null ? -1 : filteredKeyTypes.IndexOf(selectedKeyType);
            if (selectedIndex < 0 && filteredKeyTypes.Count > 0)
            {
                selectedIndex = 0;
                selectedKeyType = filteredKeyTypes[0];
            }
            else if (filteredKeyTypes.Count == 0)
            {
                selectedKeyType = null;
            }

            keyTypeField.SetValueWithoutNotify(selectedIndex < 0 ? string.Empty : filteredKeyTypeNames[selectedIndex]);
            UpdateGenerateButton();
        }

        private void OnKeyTypeChanged(ChangeEvent<string> changeEvent)
        {
            int selectedIndex = filteredKeyTypeNames.IndexOf(changeEvent.newValue);
            selectedKeyType = selectedIndex < 0 ? null : filteredKeyTypes[selectedIndex];
            UpdateGenerateButton();
        }

        private void GenerateScript()
        {
            if (!ValidateInput(out string errorMessage))
            {
                SetStatus(errorMessage, HelpBoxMessageType.Error);
                return;
            }

            string folderPath = AssetDatabase.GetAssetPath(folderField.value);
            string scriptPath = $"{folderPath}/{classNameField.value}.cs";
            if (File.Exists(scriptPath))
            {
                SetStatus($"A script already exists at '{scriptPath}'.", HelpBoxMessageType.Error);
                return;
            }

            string source = CreateSource();
            File.WriteAllText(Path.GetFullPath(scriptPath), source, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(scriptPath, ImportAssetOptions.ForceUpdate);

            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
            if (script != null)
            {
                Selection.activeObject = script;
                EditorGUIUtility.PingObject(script);
            }

            SetStatus($"Created '{scriptPath}'.", HelpBoxMessageType.Info);
        }

        private string CreateSource()
        {
            string newline = Environment.NewLine;
            string className = classNameField.value.Trim();
            string namespaceName = namespaceField.value.Trim();
            string keyTypeName = GetSourceTypeName(selectedKeyType);
            string menuName = EscapeString(assetMenuField.value.Trim());
            string indent = string.IsNullOrEmpty(namespaceName) ? string.Empty : "    ";

            StringBuilder source = new StringBuilder();
            source.Append("using UnityEngine;").Append(newline);
            source.Append("using UnityFramework.CommandTree;").Append(newline).Append(newline);

            if (!string.IsNullOrEmpty(namespaceName))
            {
                source.Append("namespace ").Append(namespaceName).Append(newline);
                source.Append('{').Append(newline);
            }

            source.Append(indent).Append("[CreateAssetMenu(fileName = \"").Append(className).Append("\", menuName = \"").Append(menuName).Append("\")]").Append(newline);
            source.Append(indent).Append("public sealed class ").Append(className).Append(" : CommandData<").Append(keyTypeName).Append('>').Append(newline);
            source.Append(indent).Append('{').Append(newline);
            source.Append(indent).Append('}').Append(newline);

            if (!string.IsNullOrEmpty(namespaceName))
                source.Append('}').Append(newline);

            return source.ToString();
        }

        private bool ValidateInput(out string errorMessage)
        {
            string className = classNameField.value.Trim();
            if (!IsValidIdentifier(className))
            {
                errorMessage = "Class Name must be a valid C# identifier.";
                return false;
            }

            string namespaceName = namespaceField.value.Trim();
            if (!string.IsNullOrEmpty(namespaceName))
            {
                string[] namespaceParts = namespaceName.Split('.');
                for (int partIndex = 0; partIndex < namespaceParts.Length; partIndex++)
                {
                    if (!IsValidIdentifier(namespaceParts[partIndex]))
                    {
                        errorMessage = "Namespace must contain valid C# identifiers.";
                        return false;
                    }
                }
            }

            if (selectedKeyType == null)
            {
                errorMessage = "Select a Key type.";
                return false;
            }

            string folderPath = AssetDatabase.GetAssetPath(folderField.value);
            if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath) || !folderPath.StartsWith("Assets", StringComparison.Ordinal))
            {
                errorMessage = "Script Folder must be a folder under Assets.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        private bool IsValidIdentifier(string value)
        {
            if (string.IsNullOrEmpty(value) || csharpKeywords.Contains(value))
                return false;
            if (!IsIdentifierStart(value[0]))
                return false;

            for (int characterIndex = 1; characterIndex < value.Length; characterIndex++)
            {
                if (!IsIdentifierPart(value[characterIndex]))
                    return false;
            }

            return true;
        }

        private bool IsIdentifierStart(char character)
        {
            return character == '_' || char.IsLetter(character);
        }

        private bool IsIdentifierPart(char character)
        {
            return character == '_' || char.IsLetterOrDigit(character);
        }

        private string GetKeyTypeDisplayName(Type type)
        {
            if (type == null)
                return string.Empty;

            string alias = GetBuiltInAlias(type);
            if (!string.IsNullOrEmpty(alias))
                return alias;

            return $"{type.FullName?.Replace('+', '.')} [{type.Assembly.GetName().Name}]";
        }

        private string GetSourceTypeName(Type type)
        {
            string alias = GetBuiltInAlias(type);
            if (!string.IsNullOrEmpty(alias))
                return alias;

            return $"global::{type.FullName?.Replace('+', '.')}";
        }

        private string GetBuiltInAlias(Type type)
        {
            if (type == typeof(bool)) return "bool";
            if (type == typeof(byte)) return "byte";
            if (type == typeof(sbyte)) return "sbyte";
            if (type == typeof(short)) return "short";
            if (type == typeof(ushort)) return "ushort";
            if (type == typeof(int)) return "int";
            if (type == typeof(uint)) return "uint";
            if (type == typeof(long)) return "long";
            if (type == typeof(ulong)) return "ulong";
            if (type == typeof(char)) return "char";
            if (type == typeof(float)) return "float";
            if (type == typeof(double)) return "double";
            if (type == typeof(string)) return "string";
            return null;
        }

        private string EscapeString(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private DefaultAsset GetDefaultFolder()
        {
            string selectedPath = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (!string.IsNullOrEmpty(selectedPath) && !AssetDatabase.IsValidFolder(selectedPath))
                selectedPath = Path.GetDirectoryName(selectedPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(selectedPath) || !AssetDatabase.IsValidFolder(selectedPath))
                selectedPath = "Assets";

            return AssetDatabase.LoadAssetAtPath<DefaultAsset>(selectedPath);
        }

        private void UpdateGenerateButton()
        {
            if (generateButton == null)
                return;

            generateButton.SetEnabled(ValidateInput(out _));
        }

        private void SetStatus(string message, HelpBoxMessageType messageType)
        {
            statusBox.text = message;
            statusBox.messageType = messageType;
        }
    }
}
