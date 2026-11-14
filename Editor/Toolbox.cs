using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using AnimatorController = UnityEditor.Animations.AnimatorController;
using Object = UnityEngine.Object;

namespace AwAVR.VRCSDKPlus {
    internal static class Toolbox {
        #region Ready Paths

        internal enum PathOption {
            Normal,
            ForceFolder,
            ForceFile
        }

        internal static string ReadyAssetPath(string path, bool makeUnique = false,
            PathOption pathOption = PathOption.Normal) {
            bool forceFolder = pathOption == PathOption.ForceFolder;
            bool forceFile = pathOption == PathOption.ForceFile;

            path = forceFile ? LegalizeName(path) : forceFolder ? LegalizePath(path) : LegalizeFullPath(path);
            bool isFolder = forceFolder || (!forceFile && string.IsNullOrEmpty(Path.GetExtension(path)));

            if (isFolder) {
                if (!Directory.Exists(path)) {
                    Directory.CreateDirectory(path);
                    AssetDatabase.ImportAsset(path);
                }
                else if (makeUnique) {
                    path = AssetDatabase.GenerateUniqueAssetPath(path);
                    Directory.CreateDirectory(path);
                    AssetDatabase.ImportAsset(path);
                }
            }
            else {
                const string basePath = "Assets";
                string folderPath = Path.GetDirectoryName(path);
                string fileName = Path.GetFileName(path);

                if (string.IsNullOrEmpty(folderPath))
                    folderPath = basePath;
                else if (!folderPath.StartsWith(Application.dataPath) && !folderPath.StartsWith(basePath))
                    folderPath = $"{basePath}/{folderPath}";

                if (folderPath != basePath && !Directory.Exists(folderPath)) {
                    Directory.CreateDirectory(folderPath);
                    AssetDatabase.ImportAsset(folderPath);
                }

                path = $"{folderPath}/{fileName}";
                if (makeUnique)
                    path = AssetDatabase.GenerateUniqueAssetPath(path);
            }

            return path;
        }

        internal static string ReadyAssetPath(string folderPath, string fullNameOrExtension, bool makeUnique = false) {
            if (string.IsNullOrEmpty(fullNameOrExtension))
                return ReadyAssetPath(LegalizePath(folderPath), makeUnique, PathOption.ForceFolder);
            if (string.IsNullOrEmpty(folderPath))
                return ReadyAssetPath(LegalizeName(fullNameOrExtension), makeUnique, PathOption.ForceFile);

            return ReadyAssetPath($"{LegalizePath(folderPath)}/{LegalizeName(fullNameOrExtension)}", makeUnique);
        }

        internal static string ReadyAssetPath(Object buddyAsset, string fullNameOrExtension = "",
            bool makeUnique = true) {
            var buddyPath = AssetDatabase.GetAssetPath(buddyAsset);
            string folderPath = Path.GetDirectoryName(buddyPath);
            if (string.IsNullOrEmpty(fullNameOrExtension))
                fullNameOrExtension = Path.GetFileName(buddyPath);
            if (fullNameOrExtension.StartsWith(".")) {
                string assetName = string.IsNullOrWhiteSpace(buddyAsset.name) ? "SomeAsset" : buddyAsset.name;
                fullNameOrExtension = $"{assetName}{fullNameOrExtension}";
            }

            return ReadyAssetPath(folderPath, fullNameOrExtension, makeUnique);
        }

        internal static string LegalizeFullPath(string path) {
            if (string.IsNullOrEmpty(path)) {
                Debug.LogWarning("Legalizing empty path! Returned path as 'EmptyPath'");
                return "EmptyPath";
            }

            var ext = Path.GetExtension(path);
            bool isFolder = string.IsNullOrEmpty(ext);
            if (isFolder) return LegalizePath(path);

            string folderPath = Path.GetDirectoryName(path);
            var fileName = LegalizeName(Path.GetFileNameWithoutExtension(path));

            if (string.IsNullOrEmpty(folderPath)) return $"{fileName}{ext}";
            folderPath = LegalizePath(folderPath);

            return $"{folderPath}/{fileName}{ext}";
        }

        internal static string LegalizePath(string path) {
            string regexFolderReplace = Regex.Escape(new string(Path.GetInvalidPathChars()));

            path = path.Replace('\\', '/');
            if (path.IndexOf('/') > 0)
                path = string.Join("/", path.Split('/').Select(s => Regex.Replace(s, $@"[{regexFolderReplace}]", "-")));

            return path;
        }

        internal static string LegalizeName(string name) {
            string regexFileReplace = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
            return string.IsNullOrEmpty(name) ? "Unnamed" : Regex.Replace(name, $@"[{regexFileReplace}]", "-");
        }

        #endregion

        public static string GenerateUniqueString(string s, Func<string, bool> PassCondition,
            bool addNumberIfMissing = true) {
            if (PassCondition(s)) return s;
            var match = Regex.Match(s, @"(?=.*)(\d+)$");
            if (!match.Success && !addNumberIfMissing) return s;
            var numberString = match.Success ? match.Groups[1].Value : "1";
            if (!match.Success && !s.EndsWith(" ")) s += " ";
            var newString = Regex.Replace(s, @"(?=.*?)\d+$", string.Empty);
            while (!PassCondition($"{newString}{numberString}"))
                numberString = (int.Parse(numberString) + 1).ToString(new string('0', numberString.Length));

            return $"{newString}{numberString}";
        }

        // ReSharper disable once InconsistentNaming
        public static VRCExpressionsMenu.Control.ControlType ToControlType(this SerializedProperty property) {
            var value = property.enumValueIndex;
            switch (value) {
                case 0:
                    return VRCExpressionsMenu.Control.ControlType.Button;
                case 1:
                    return VRCExpressionsMenu.Control.ControlType.Toggle;
                case 2:
                    return VRCExpressionsMenu.Control.ControlType.SubMenu;
                case 3:
                    return VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet;
                case 4:
                    return VRCExpressionsMenu.Control.ControlType.FourAxisPuppet;
                case 5:
                    return VRCExpressionsMenu.Control.ControlType.RadialPuppet;
            }

            return VRCExpressionsMenu.Control.ControlType.Button;
        }

        public static int GetEnumValueIndex(this VRCExpressionsMenu.Control.ControlType type) {
            switch (type) {
                case VRCExpressionsMenu.Control.ControlType.Button:
                    return 0;
                case VRCExpressionsMenu.Control.ControlType.Toggle:
                    return 1;
                case VRCExpressionsMenu.Control.ControlType.SubMenu:
                    return 2;
                case VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet:
                    return 3;
                case VRCExpressionsMenu.Control.ControlType.FourAxisPuppet:
                    return 4;
                case VRCExpressionsMenu.Control.ControlType.RadialPuppet:
                    return 5;
                default:
                    return -1;
            }
        }

        public static int FindIndex(this IEnumerable array, object target) {
            var enumerator = array.GetEnumerator();
            var index = 0;
            while (enumerator.MoveNext()) {
                if (enumerator.Current != null && enumerator.Current.Equals(target))
                    return index;
                index++;
            }

            return -1;
        }

        internal static bool GetPlayableLayer(this VRCAvatarDescriptor avi, VRCAvatarDescriptor.AnimLayerType type,
            out AnimatorController controller) {
            controller =
                (from l in avi.baseAnimationLayers.Concat(avi.specialAnimationLayers)
                    where l.type == type
                    select l.animatorController).FirstOrDefault() as AnimatorController;
            return controller != null;
        }

        internal static bool IterateArray(this SerializedProperty property, Func<int, SerializedProperty, bool> func,
            params int[] skipIndex) {
            for (int i = property.arraySize - 1; i >= 0; i--) {
                if (skipIndex.Contains(i)) continue;
                if (i >= property.arraySize) continue;
                if (func(i, property.GetArrayElementAtIndex(i)))
                    return true;
            }

            return false;
        }

        public static class Strings {
            public const string IconCopy = "SaveActive";
            public const string IconPaste = "Clipboard";
            public const string IconMove = "MoveTool";
            public const string IconPlace = "DefaultSorting";
            public const string IconDuplicate = "TreeEditor.Duplicate";
            public const string IconHelp = "_Help";
            public const string IconWarn = "console.warnicon.sml";
            public const string IconError = "console.erroricon.sml";
            public const string IconClear = "winbtn_win_close";
            public const string IconFolder = "FolderOpened Icon";
            public const string IconRemove = "Toolbar Minus";
            public const string IconAdd = "Toolbar Plus";
            public const string IconSearch = "Search Icon";
            public const string IconEdit = "d_editicon.sml";
            public const string IconRefresh = "Refresh";

            public const string ClipboardPrefixControl = "[TAG=VSP_CONTROL]";

            public const string SettingsCompact = "VSP_Compact";

            // Shared with the VRCSDK's own editor so the option stays in sync when switching editors.
            public const string SettingsShowAdvancedParameters = "VRC.ExpressionParameters.ShowAdvancedOptions";

            public const string MissingParametersTooltip =
                "No Expression Parameters targeted. Auto-fill and warnings are disabled.";

            public const string MenuFullTooltip = "Menu's controls are already maxed out. (8/8)";
        }

        public static class Preferences {
            public static bool CompactMode {
                get => EditorPrefs.GetBool(Strings.SettingsCompact, false);
                set => EditorPrefs.SetBool(Strings.SettingsCompact, value);
            }

            public static bool ShowAdvancedParameters {
                get => EditorPrefs.GetBool(Strings.SettingsShowAdvancedParameters, false);
                set => EditorPrefs.SetBool(Strings.SettingsShowAdvancedParameters, value);
            }
        }

        #region UI Toolkit

        private static StyleSheet _styleSheet;

        /// <summary>Adds the VRCSDK+ stylesheet to an inspector root.</summary>
        internal static void AddStyleSheet(VisualElement root) {
            if (!_styleSheet) _styleSheet = Resources.Load<StyleSheet>("AwAVRSDKPlusStyles");
            if (_styleSheet) root.styleSheets.Add(_styleSheet);
            root.AddToClassList("sdkp-root");
        }

        private static readonly Dictionary<string, Texture2D> IconCache = new Dictionary<string, Texture2D>();

        internal static Texture2D GetIcon(string iconName) {
            if (IconCache.TryGetValue(iconName, out var icon) && icon) return icon;
            icon = EditorGUIUtility.IconContent(iconName)?.image as Texture2D;
            IconCache[iconName] = icon;
            return icon;
        }

        /// <summary>A small, frameless button showing a built-in editor icon.</summary>
        internal static Button IconButton(string iconName, string tooltip, Action onClick) {
            var button = new Button(onClick) { tooltip = tooltip };
            button.AddToClassList("sdkp-icon-button");
            SetIcon(button, iconName);
            return button;
        }

        internal static void SetIcon(VisualElement element, string iconName) {
            var icon = GetIcon(iconName);
            element.style.backgroundImage = icon ? new StyleBackground(icon) : new StyleBackground(StyleKeyword.None);
        }

        /// <summary>A non-interactive icon, used for warnings with a tooltip.</summary>
        internal static VisualElement Icon(string iconName, string tooltip = null) {
            var element = new VisualElement { tooltip = tooltip };
            element.AddToClassList("sdkp-icon");
            SetIcon(element, iconName);
            return element;
        }

        /// <summary>
        /// Calls <paramref name="onChanged"/> when <paramref name="serializedObject"/>'s target changes, whether from this
        /// inspector, another one, undo/redo or a script. Uses binding tracking plus a light dirty-count poll as backup.
        /// </summary>
        internal static VisualElement CreateChangeTracker(SerializedObject serializedObject, Action onChanged) {
            var tracker = new VisualElement { name = "sdkp-change-tracker", pickingMode = PickingMode.Ignore };
            tracker.style.position = Position.Absolute;
            tracker.style.width = 0;
            tracker.style.height = 0;
            tracker.TrackSerializedObjectValue(serializedObject, _ => onChanged());

            int lastDirtyCount = -1;
            tracker.schedule.Execute(() => {
                Object target;
                try {
                    target = serializedObject.targetObject;
                }
                catch (Exception) {
                    return; // disposed
                }

                if (!target) return;
                int dirtyCount = EditorUtility.GetDirtyCount(target);
                bool updated = serializedObject.UpdateIfRequiredOrScript();
                if (dirtyCount == lastDirtyCount && !updated) return;
                bool first = lastDirtyCount == -1;
                lastDirtyCount = dirtyCount;
                if (!first || updated) onChanged();
            }).Every(150);
            return tracker;
        }

        internal static void SetDisplayed(this VisualElement element, bool displayed) =>
            element.style.display = displayed ? DisplayStyle.Flex : DisplayStyle.None;

        /// <summary>Overlays a grey hint (e.g. a placeholder or the matched parameter type) inside a text field.</summary>
        internal static Label AddHint(TextField field, string text, bool rightAligned) {
            var hint = new Label(text) { pickingMode = PickingMode.Ignore };
            hint.AddToClassList("sdkp-hint");
            hint.AddToClassList(rightAligned ? "sdkp-hint--right" : "sdkp-hint--left");
            // Inside the input box, so a field label in front of it isn't covered.
            (field.Q(className: TextField.inputUssClassName) ?? field).Add(hint);
            return hint;
        }

        /// <summary>Makes a field with a label line its input up with the other fields in the inspector.</summary>
        internal static T Aligned<T>(this T field) where T : VisualElement {
            field.AddToClassList(BaseField<int>.alignedFieldUssClassName);
            return field;
        }

        /// <summary>
        /// A texture field drawn as a thumbnail: the picked texture fills it, drop a texture on it or use its picker button.
        /// Call the returned refresh action when the property may have changed.
        /// </summary>
        internal static ObjectField ThumbnailField(SerializedProperty property, float width, float height, string placeholder,
            out Action refresh) {
            var field = new ObjectField { objectType = typeof(Texture2D), allowSceneObjects = false };
            field.AddToClassList("sdkp-thumbnail");
            field.style.width = width;
            field.style.height = height;
            field.BindProperty(property);

            var hint = new Label(placeholder) { pickingMode = PickingMode.Ignore };
            hint.AddToClassList("sdkp-thumbnail-hint");
            field.Add(hint);

            refresh = () => {
                Texture2D texture;
                try {
                    texture = property.objectReferenceValue as Texture2D;
                }
                catch (Exception) {
                    return; // property disposed
                }

                var display = field.Q(className: ObjectField.objectUssClassName);
                if (display != null)
                    display.style.backgroundImage = texture ? new StyleBackground(texture) : new StyleBackground(StyleKeyword.None);
                hint.SetDisplayed(!texture);
                field.tooltip = texture ? texture.name : $"{placeholder} (drop a texture here)";
            };
            var refreshAction = refresh;
            field.RegisterValueChangedCallback(_ => refreshAction());
            field.RegisterCallback<AttachToPanelEvent>(_ => field.schedule.Execute(refreshAction));
            return field;
        }

        /// <summary>
        /// True while the user is typing in a text or number field, so list shortcuts (copy, paste, delete)
        /// should be left to that field.
        /// </summary>
        internal static bool IsEditingText(VisualElement element) {
            var focused = element?.panel?.focusController?.focusedElement as VisualElement;
            for (var e = focused; e != null; e = e.parent) {
                for (var t = e.GetType(); t != null && t != typeof(VisualElement); t = t.BaseType) {
                    if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(TextInputBaseField<>))
                        return true;
                }
            }

            return false;
        }

        private static double _lastDeleteTime;

        /// <summary>
        /// Routes editor commands (Copy, Paste, Duplicate, SoftDelete, ...) and the Delete key to <paramref name="execute"/>
        /// while <paramref name="element"/> or one of its children has focus. Typing in text fields is left alone.
        /// </summary>
        internal static void RegisterCommands(VisualElement element, Func<EventCommands, bool> canExecute,
            Action<EventCommands> execute) {
            bool TryParse(string commandName, out EventCommands command) =>
                Enum.TryParse(commandName, out command) && !IsEditingText(element);

            element.RegisterCallback<ValidateCommandEvent>(evt => {
                if (TryParse(evt.commandName, out var command) && canExecute(command))
                    evt.StopPropagation();
            });

            element.RegisterCallback<ExecuteCommandEvent>(evt => {
                if (!TryParse(evt.commandName, out var command) || !canExecute(command)) return;
                evt.StopPropagation();
                if (command == EventCommands.Delete || command == EventCommands.SoftDelete) {
                    // The Delete key can arrive both as a key press and as a command, only act once.
                    if (EditorApplication.timeSinceStartup - _lastDeleteTime < 0.1) return;
                    _lastDeleteTime = EditorApplication.timeSinceStartup;
                }

                execute(command);
            });

            element.RegisterCallback<KeyDownEvent>(evt => {
                if (evt.keyCode != KeyCode.Delete || IsEditingText(element)) return;
                if (!canExecute(EventCommands.SoftDelete)) return;
                evt.StopPropagation();
                if (EditorApplication.timeSinceStartup - _lastDeleteTime < 0.1) return;
                _lastDeleteTime = EditorApplication.timeSinceStartup;
                execute(EventCommands.SoftDelete);
            });
        }

        internal enum EventCommands {
            Copy,
            Cut,
            Paste,
            Duplicate,
            Delete,
            SoftDelete,
            SelectAll,
            Find,
            FrameSelected,
            FrameSelectedWithLock,
            FocusProjectWindow
        }

        #endregion

        /// <summary>
        /// Regex match that treats an invalid pattern (e.g. a lone "[" while typing) as a plain, case-insensitive substring.
        /// </summary>
        internal static bool SearchMatches(string value, string search) {
            if (string.IsNullOrEmpty(search)) return true;
            value = value ?? string.Empty;
            try {
                return Regex.IsMatch(value, search, RegexOptions.IgnoreCase);
            }
            catch (ArgumentException) {
                return value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }
    }
}
