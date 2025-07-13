using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using AnimatorController = UnityEditor.Animations.AnimatorController;
using Object = UnityEngine.Object;

namespace AwAVR.VRCSDKPlus
{
    internal static class Toolbox
    {
        #region Ready Paths

        internal enum PathOption
        {
            Normal,
            ForceFolder,
            ForceFile
        }

        internal static string ReadyAssetPath(string path, bool makeUnique = false,
            PathOption pathOption = PathOption.Normal)
        {
            bool forceFolder = pathOption == PathOption.ForceFolder;
            bool forceFile = pathOption == PathOption.ForceFile;

            path = forceFile ? LegalizeName(path) : forceFolder ? LegalizePath(path) : LegalizeFullPath(path);
            bool isFolder = forceFolder || (!forceFile && string.IsNullOrEmpty(Path.GetExtension(path)));

            if (isFolder)
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                    AssetDatabase.ImportAsset(path);
                }
                else if (makeUnique)
                {
                    path = AssetDatabase.GenerateUniqueAssetPath(path);
                    Directory.CreateDirectory(path);
                    AssetDatabase.ImportAsset(path);
                }
            }
            else
            {
                const string basePath = "Assets";
                string folderPath = Path.GetDirectoryName(path);
                string fileName = Path.GetFileName(path);

                if (string.IsNullOrEmpty(folderPath))
                    folderPath = basePath;
                else if (!folderPath.StartsWith(Application.dataPath) && !folderPath.StartsWith(basePath))
                    folderPath = $"{basePath}/{folderPath}";

                if (folderPath != basePath && !Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                    AssetDatabase.ImportAsset(folderPath);
                }

                path = $"{folderPath}/{fileName}";
                if (makeUnique)
                    path = AssetDatabase.GenerateUniqueAssetPath(path);
            }

            return path;
        }

        internal static string ReadyAssetPath(string folderPath, string fullNameOrExtension, bool makeUnique = false)
        {
            if (string.IsNullOrEmpty(fullNameOrExtension))
                return ReadyAssetPath(LegalizePath(folderPath), makeUnique, PathOption.ForceFolder);
            if (string.IsNullOrEmpty(folderPath))
                return ReadyAssetPath(LegalizeName(fullNameOrExtension), makeUnique, PathOption.ForceFile);

            return ReadyAssetPath($"{LegalizePath(folderPath)}/{LegalizeName(fullNameOrExtension)}", makeUnique);
        }

        internal static string ReadyAssetPath(Object buddyAsset, string fullNameOrExtension = "",
            bool makeUnique = true)
        {
            var buddyPath = AssetDatabase.GetAssetPath(buddyAsset);
            string folderPath = Path.GetDirectoryName(buddyPath);
            if (string.IsNullOrEmpty(fullNameOrExtension))
                fullNameOrExtension = Path.GetFileName(buddyPath);
            if (fullNameOrExtension.StartsWith("."))
            {
                string assetName = string.IsNullOrWhiteSpace(buddyAsset.name) ? "SomeAsset" : buddyAsset.name;
                fullNameOrExtension = $"{assetName}{fullNameOrExtension}";
            }

            return ReadyAssetPath(folderPath, fullNameOrExtension, makeUnique);
        }

        internal static string LegalizeFullPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
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

        internal static string LegalizePath(string path)
        {
            string regexFolderReplace = Regex.Escape(new string(Path.GetInvalidPathChars()));

            path = path.Replace('\\', '/');
            if (path.IndexOf('/') > 0)
                path = string.Join("/", path.Split('/').Select(s => Regex.Replace(s, $@"[{regexFolderReplace}]", "-")));

            return path;
        }

        internal static string LegalizeName(string name)
        {
            string regexFileReplace = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
            return string.IsNullOrEmpty(name) ? "Unnamed" : Regex.Replace(name, $@"[{regexFileReplace}]", "-");
        }

        #endregion

        internal static bool TryGetActiveIndex(this ReorderableList orderList, out int index)
        {
            index = orderList.index;
            if (index < orderList.count && index >= 0) return true;
            index = -1;
            return false;
        }

        public static string GenerateUniqueString(string s, Func<string, bool> PassCondition,
            bool addNumberIfMissing = true)
        {
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

        public static class Container
        {
            public class Vertical : IDisposable
            {
                public Vertical(params GUILayoutOption[] options)
                    => EditorGUILayout.BeginVertical(GUI.skin.GetStyle("helpbox"), options);

                public Vertical(string title, params GUILayoutOption[] options)
                {
                    EditorGUILayout.BeginVertical(GUI.skin.GetStyle("helpbox"), options);

                    EditorGUILayout.LabelField(title, Toolbox.Styles.Label.Centered);
                }

                public void Dispose() => EditorGUILayout.EndVertical();
            }

            public class Horizontal : IDisposable
            {
                public Horizontal(params GUILayoutOption[] options)
                    => EditorGUILayout.BeginHorizontal(GUI.skin.GetStyle("helpbox"), options);

                public Horizontal(string title, params GUILayoutOption[] options)
                {
                    EditorGUILayout.BeginHorizontal(GUI.skin.GetStyle("helpbox"), options);

                    EditorGUILayout.LabelField(title, Toolbox.Styles.Label.Centered);
                }

                public void Dispose() => EditorGUILayout.EndHorizontal();
            }

            public static void BeginLayout(params GUILayoutOption[] options)
                => EditorGUILayout.BeginVertical(GUI.skin.GetStyle("helpbox"), options);

            public static void BeginLayout(string title, params GUILayoutOption[] options)
            {
                EditorGUILayout.BeginVertical(GUI.skin.GetStyle("helpbox"), options);

                EditorGUILayout.LabelField(title, Toolbox.Styles.Label.Centered);
            }

            public static void EndLayout() => EditorGUILayout.EndVertical();

            public static Rect GUIBox(float height)
            {
                var rect = EditorGUILayout.GetControlRect(false, height);
                return GUIBox(ref rect);
            }

            public static Rect GUIBox(ref Rect rect)
            {
                GUI.Box(rect, "", GUI.skin.GetStyle("helpbox"));

                rect.x += 4;
                rect.width -= 8;
                rect.y += 3;
                rect.height -= 6;

                return rect;
            }
        }

        public static class Placeholder
        {
            public static void GUILayout(float height) =>
                GUI(EditorGUILayout.GetControlRect(false, height));

            public static void GUI(Rect rect) => GUI(rect, EditorGUIUtility.isProSkin ? 53 : 182);

            private static void GUI(Rect rect, float color)
            {
                EditorGUI.DrawTextureTransparent(rect, GetColorTexture(color));
            }
        }

        public static class Styles
        {
            public const float Padding = 3;

            public static class Label
            {
                internal static readonly UnityEngine.GUIStyle Centered
                    = new UnityEngine.GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };

                internal static readonly UnityEngine.GUIStyle RichText
                    = new UnityEngine.GUIStyle(GUI.skin.label) { richText = true };


                internal static readonly UnityEngine.GUIStyle Type
                    = new UnityEngine.GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleRight,
                        normal =
                        {
                            textColor = EditorGUIUtility.isProSkin ? Color.gray : BrightnessToColor(91),
                        },
                        fontStyle = FontStyle.Italic,
                    };

                internal static readonly UnityEngine.GUIStyle PlaceHolder
                    = new UnityEngine.GUIStyle(Type)
                    {
                        fontSize = 11,
                        alignment = TextAnchor.MiddleLeft,
                        contentOffset = new Vector2(2.5f, 0)
                    };

                internal static readonly GUIStyle faintLinkLabel = new GUIStyle(PlaceHolder)
                    { name = "Toggle", hover = { textColor = new Color(0.3f, 0.7f, 1) } };

                internal static readonly UnityEngine.GUIStyle TypeFocused
                    = new UnityEngine.GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleRight,
                        normal =
                        {
                            textColor = EditorGUIUtility.isProSkin ? Color.white : Color.black,
                        },
                        fontStyle = FontStyle.Italic,
                    };

                internal static readonly GUIStyle TypeLabel = new GUIStyle(PlaceHolder)
                    { contentOffset = new Vector2(-2.5f, 0) };

                internal static readonly GUIStyle RightPlaceHolder = new GUIStyle(TypeLabel)
                    { alignment = TextAnchor.MiddleRight };

                internal static readonly UnityEngine.GUIStyle Watermark
                    = new UnityEngine.GUIStyle(PlaceHolder)
                    {
                        alignment = TextAnchor.MiddleRight,
                        fontSize = 10,
                    };

                internal static readonly UnityEngine.GUIStyle LabelDropdown
                    = new UnityEngine.GUIStyle(GUI.skin.GetStyle("DropDownButton"))
                    {
                        alignment = TextAnchor.MiddleLeft,
                        contentOffset = new Vector2(2.5f, 0)
                    };

                internal static readonly UnityEngine.GUIStyle RemoveIcon
                    = new UnityEngine.GUIStyle(GUI.skin.GetStyle("RL FooterButton"));
            }

            internal static readonly GUIStyle icon = new GUIStyle(GUI.skin.label) { fixedWidth = 18, fixedHeight = 18 };

            internal static readonly UnityEngine.GUIStyle letterButton =
                new UnityEngine.GUIStyle(GUI.skin.button)
                    { padding = new RectOffset(), margin = new RectOffset(1, 1, 1, 1), richText = true };
        }

        public static class Strings
        {
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
            public const string IconSearch = "Search Icon";

            public const string ClipboardPrefixControl = "[TAG=VSP_CONTROL]";

            public const string SettingsCompact = "VSP_Compact";
        }

        public static class GUIContent
        {
            public const string MissingParametersTooltip =
                "No Expression Parameters targeted. Auto-fill and warnings are disabled.";

            public const string MenuFullTooltip = "Menu's controls are already maxed out. (8/8)";

            public static readonly UnityEngine.GUIContent Copy
                = new UnityEngine.GUIContent(EditorGUIUtility.IconContent(Toolbox.Strings.IconCopy))
                {
                    tooltip = "Copy"
                };

            public static readonly UnityEngine.GUIContent Paste
                = new UnityEngine.GUIContent(EditorGUIUtility.IconContent(Toolbox.Strings.IconPaste))
                {
                    tooltip = "Paste"
                };

            public static readonly UnityEngine.GUIContent Move
                = new UnityEngine.GUIContent(EditorGUIUtility.IconContent(Toolbox.Strings.IconMove))
                {
                    tooltip = "Move"
                };

            public static readonly UnityEngine.GUIContent Place
                = new UnityEngine.GUIContent(EditorGUIUtility.IconContent(Toolbox.Strings.IconPlace))
                {
                    tooltip = "Place"
                };

            public static readonly UnityEngine.GUIContent Duplicate
                = new UnityEngine.GUIContent(EditorGUIUtility.IconContent(Toolbox.Strings.IconDuplicate))
                {
                    tooltip = "Duplicate"
                };

            public static readonly UnityEngine.GUIContent Help
                = new UnityEngine.GUIContent(EditorGUIUtility.IconContent(Toolbox.Strings.IconHelp));

            public static readonly UnityEngine.GUIContent Warn
                = new UnityEngine.GUIContent(EditorGUIUtility.IconContent(Toolbox.Strings.IconWarn));

            public static readonly UnityEngine.GUIContent Error
                = new UnityEngine.GUIContent(EditorGUIUtility.IconContent(Toolbox.Strings.IconError));

            public static readonly UnityEngine.GUIContent Clear
                = new UnityEngine.GUIContent(EditorGUIUtility.IconContent(Toolbox.Strings.IconClear))
                {
                    tooltip = "Clear"
                };

            public static readonly UnityEngine.GUIContent Folder
                = new UnityEngine.GUIContent(EditorGUIUtility.IconContent(Toolbox.Strings.IconFolder))
                {
                    tooltip = "Open"
                };

            public static readonly UnityEngine.GUIContent Remove
                = new UnityEngine.GUIContent(EditorGUIUtility.IconContent(Toolbox.Strings.IconRemove))
                    { tooltip = "Remove parameter from list" };

            public static readonly UnityEngine.GUIContent Search
                = new UnityEngine.GUIContent(EditorGUIUtility.IconContent(Toolbox.Strings.IconSearch))
                    { tooltip = "Search" };
        }

        public static class Preferences
        {
            public static bool CompactMode
            {
                get => EditorPrefs.GetBool(Toolbox.Strings.SettingsCompact, false);
                set => EditorPrefs.SetBool(Toolbox.Strings.SettingsCompact, value);
            }
        }

        public static Color BrightnessToColor(float brightness)
        {
            if (brightness > 1) brightness /= 255;
            return new Color(brightness, brightness, brightness, 1);
        }

        private static readonly Texture2D tempTexture = new Texture2D(1, 1)
            { anisoLevel = 0, filterMode = FilterMode.Point };

        internal static Texture2D GetColorTexture(float rgb, float a = 1)
            => GetColorTexture(rgb, rgb, rgb, a);

        internal static Texture2D GetColorTexture(float r, float g, float b, float a = 1)
        {
            if (r > 1) r /= 255;
            if (g > 1) g /= 255;
            if (b > 1) b /= 255;
            if (a > 1) a /= 255;

            return GetColorTexture(new Color(r, g, b, a));
        }

        internal static Texture2D GetColorTexture(Color color)
        {
            tempTexture.SetPixel(0, 0, color);
            tempTexture.Apply();
            return tempTexture;
        }

        // ReSharper disable once InconsistentNaming
        public static VRCExpressionsMenu.Control.ControlType ToControlType(this SerializedProperty property)
        {
            var value = property.enumValueIndex;
            switch (value)
            {
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

        public static int GetEnumValueIndex(this VRCExpressionsMenu.Control.ControlType type)
        {
            switch (type)
            {
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

        public static int FindIndex(this IEnumerable array, object target)
        {
            var enumerator = array.GetEnumerator();
            var index = 0;
            while (enumerator.MoveNext())
            {
                if (enumerator.Current != null && enumerator.Current.Equals(target))
                    return index;
                index++;
            }

            return -1;
        }

        internal static bool GetPlayableLayer(this VRCAvatarDescriptor avi, VRCAvatarDescriptor.AnimLayerType type,
            out AnimatorController controller)
        {
            controller =
                (from l in avi.baseAnimationLayers.Concat(avi.specialAnimationLayers)
                    where l.type == type
                    select l.animatorController).FirstOrDefault() as AnimatorController;
            return controller != null;
        }

        internal static bool IterateArray(this SerializedProperty property, Func<int, SerializedProperty, bool> func,
            params int[] skipIndex)
        {
            for (int i = property.arraySize - 1; i >= 0; i--)
            {
                if (skipIndex.Contains(i)) continue;
                if (i >= property.arraySize) continue;
                if (func(i, property.GetArrayElementAtIndex(i)))
                    return true;
            }

            return false;
        }

        #region Keyboard Commands

        internal enum EventCommands
        {
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

        internal static bool HasReceivedCommand(EventCommands command, string matchFocusControl = "",
            bool useEvent = true)
        {
            if (!string.IsNullOrEmpty(matchFocusControl) && GUI.GetNameOfFocusedControl() != matchFocusControl)
                return false;
            Event e = Event.current;
            if (e.type != EventType.ValidateCommand) return false;
            bool received = command.ToString() == e.commandName;
            if (received && useEvent) e.Use();
            return received;
        }

        internal static bool HasReceivedKey(KeyCode key, string matchFocusControl = "", bool useEvent = true)
        {
            if (!string.IsNullOrEmpty(matchFocusControl) && GUI.GetNameOfFocusedControl() != matchFocusControl)
                return false;
            Event e = Event.current;
            bool received = e.type == EventType.KeyDown && e.keyCode == key;
            if (received && useEvent) e.Use();
            return received;
        }

        internal static bool HasReceivedEnter(string matchFocusControl = "", bool useEvent = true) =>
            HasReceivedKey(KeyCode.Return, matchFocusControl, useEvent) ||
            HasReceivedKey(KeyCode.KeypadEnter, matchFocusControl, useEvent);

        internal static bool HasReceivedCancel(string matchFocusControl = "", bool useEvent = true) =>
            HasReceivedKey(KeyCode.Escape, matchFocusControl, useEvent);

        internal static bool HasReceivedAnyDelete(string matchFocusControl = "", bool useEvent = true) =>
            HasReceivedCommand(EventCommands.SoftDelete, matchFocusControl, useEvent) ||
            HasReceivedCommand(EventCommands.Delete, matchFocusControl, useEvent) ||
            HasReceivedKey(KeyCode.Delete, matchFocusControl, useEvent);

        internal static bool HandleConfirmEvents(string matchFocusControl = "", Action onConfirm = null,
            Action onCancel = null)
        {
            if (HasReceivedEnter(matchFocusControl))
            {
                onConfirm?.Invoke();
                return true;
            }

            if (HasReceivedCancel(matchFocusControl))
            {
                onCancel?.Invoke();
                return true;
            }

            return false;
        }

        internal static bool HandleTextFocusConfirmCommands(string matchFocusControl, Action onConfirm = null,
            Action onCancel = null)
        {
            if (!HandleConfirmEvents(matchFocusControl, onConfirm, onCancel)) return false;
            GUI.FocusControl(null);
            return true;
        }

        #endregion

        internal abstract class CustomDropdownBase : PopupWindowContent
        {
            internal static readonly GUIStyle backgroundStyle = new GUIStyle()
            {
                hover = { background = Toolbox.GetColorTexture(new Color(0.3020f, 0.3020f, 0.3020f)) },
                active = { background = Toolbox.GetColorTexture(new Color(0.1725f, 0.3647f, 0.5294f)) }
            };

            internal static readonly GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
        }

        internal class CustomDropdown<T> : CustomDropdownBase
        {
            private readonly string title;
            private string search;
            internal DropDownItem[] items;
            private readonly Action<DropDownItem> itemGUI;
            private readonly Action<int, T> onSelected;
            private Func<T, string, bool> onSearchChanged;

            private bool hasSearch;
            private float width;
            private bool firstPass = true;
            private Vector2 scroll;
            private readonly Rect[] selectionRects;

            public CustomDropdown(string title, IEnumerable<T> itemArray, Action<DropDownItem> itemGUI,
                Action<int, T> onSelected)
            {
                this.title = title;
                this.onSelected = onSelected;
                this.itemGUI = itemGUI;
                items = itemArray.Select((item, i) => new DropDownItem(item, i)).ToArray();
                selectionRects = new Rect[items.Length];
            }

            public void EnableSearch(Func<T, string, bool> onSearchChanged)
            {
                hasSearch = true;
                this.onSearchChanged = onSearchChanged;
            }

            public void OrderBy(Func<T, object> orderFunc)
            {
                items = orderFunc != null ? items.OrderBy(item => orderFunc(item.value)).ToArray() : items;
            }

            public void SetExtraOptions(Func<T, object[]> argReturn)
            {
                foreach (var i in items)
                    i.args = argReturn(i.value);
            }

            public override void OnGUI(Rect rect)
            {
                using (new GUILayout.AreaScope(rect))
                {
                    var e = Event.current;
                    scroll = GUILayout.BeginScrollView(scroll);
                    if (!string.IsNullOrEmpty(title))
                    {
                        GUILayout.Label(title, titleStyle);
                        DrawSeparator();
                    }

                    if (hasSearch)
                    {
                        EditorGUI.BeginChangeCheck();
                        if (firstPass) GUI.SetNextControlName($"{title}SearchBar");
                        search = EditorGUILayout.TextField(search, GUI.skin.GetStyle("SearchTextField"));
                        if (EditorGUI.EndChangeCheck())
                        {
                            foreach (var i in items)
                                i.displayed = onSearchChanged(i.value, search);
                        }
                    }

                    var t = e.type;
                    for (int i = 0; i < items.Length; i++)
                    {
                        var item = items[i];
                        if (!item.displayed) continue;
                        if (!firstPass)
                        {
                            if (GUI.Button(selectionRects[i], string.Empty, backgroundStyle))
                            {
                                onSelected(item.itemIndex, item.value);
                                editorWindow.Close();
                            }
                        }

                        using (new GUILayout.VerticalScope()) itemGUI(item);

                        if (t == EventType.Repaint)
                        {
                            selectionRects[i] = GUILayoutUtility.GetLastRect();

                            if (firstPass && selectionRects[i].width > width)
                                width = selectionRects[i].width;
                        }
                    }

                    if (t == EventType.Repaint && firstPass)
                    {
                        firstPass = false;
                        GUI.FocusControl($"{title}SearchBar");
                    }

                    GUILayout.EndScrollView();
                    if (rect.Contains(e.mousePosition))
                        editorWindow.Repaint();
                }
            }

            public override Vector2 GetWindowSize()
            {
                Vector2 ogSize = base.GetWindowSize();
                if (!firstPass) ogSize.x = width + 21;
                return ogSize;
            }

            public void Show(Rect position) => PopupWindow.Show(position, this);

            internal class DropDownItem
            {
                internal readonly int itemIndex;
                internal readonly T value;

                internal object[] args;
                internal bool displayed = true;

                internal object extra
                {
                    get => args[0];
                    set => args[0] = value;
                }

                internal DropDownItem(T value, int itemIndex)
                {
                    this.value = value;
                    this.itemIndex = itemIndex;
                }

                public static implicit operator T(DropDownItem i) => i.value;
            }

            private static void DrawSeparator(int thickness = 2, int padding = 10)
            {
                Rect r = EditorGUILayout.GetControlRect(GUILayout.Height(thickness + padding));
                r.height = thickness;
                r.y += padding / 2f;
                r.x -= 2;
                r.width += 6;
                ColorUtility.TryParseHtmlString(EditorGUIUtility.isProSkin ? "#595959" : "#858585",
                    out Color lineColor);
                EditorGUI.DrawRect(r, lineColor);
            }
        }
    }
}