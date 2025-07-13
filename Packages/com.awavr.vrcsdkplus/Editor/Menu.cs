using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace AwAVR.VRCSDKPlus
{
    internal sealed class VRCMenuPlus : Editor, IHasCustomMenu
    {
        private static bool _editorActive = true;
        private static VRCAvatarDescriptor _avatar;
        private VRCAvatarDescriptor[] _validAvatars;
        private ReorderableList _controlsList;

        private static readonly LinkedList<VRCExpressionsMenu> MenuHistory = new LinkedList<VRCExpressionsMenu>();
        private static LinkedListNode<VRCExpressionsMenu> _currentNode;
        private static VRCExpressionsMenu _lastMenu;

        private static VRCExpressionsMenu _moveSourceMenu;
        private static VRCExpressionsMenu.Control _moveTargetControl;
        private static bool _isMoving;

        internal static LinkedListNode<VRCExpressionsMenu> GetCurrentNode() => _currentNode;

        #region Initialization

        private void ReInitializeAll()
        {
            CheckAvatar();
            CheckMenu();
            InitializeList();
        }

        private void CheckAvatar()
        {
            _validAvatars = FindObjectsOfType<VRCAvatarDescriptor>();
            if (_validAvatars.Length == 0) _avatar = null;
            else if (!_avatar) _avatar = _validAvatars[0];
        }

        private void CheckMenu()
        {
            var currentMenu = target as VRCExpressionsMenu;
            if (!currentMenu || currentMenu == _lastMenu) return;

            if (_currentNode != null && MenuHistory.Last != _currentNode)
            {
                var node = _currentNode.Next;
                while (node != null)
                {
                    var nextNode = node.Next;
                    MenuHistory.Remove(node);
                    node = nextNode;
                }
            }

            _lastMenu = currentMenu;
            _currentNode = MenuHistory.AddLast(currentMenu);
        }

        private void InitializeList()
        {
            var l = serializedObject.FindProperty("controls");
            _controlsList = new ReorderableList(serializedObject, l, true, true, true, false);
            _controlsList.onCanAddCallback += reorderableList => reorderableList.count < 8;
            _controlsList.onAddCallback = _ =>
            {
                var controlsProp = _controlsList.serializedProperty;
                var index = controlsProp.arraySize++;
                _controlsList.index = index;

                var c = controlsProp.GetArrayElementAtIndex(index);
                c.FindPropertyRelative("name").stringValue = "New Control";
                c.FindPropertyRelative("icon").objectReferenceValue = null;
                c.FindPropertyRelative("parameter").FindPropertyRelative("name").stringValue = "";
                c.FindPropertyRelative("type").enumValueIndex = 1;
                c.FindPropertyRelative("subMenu").objectReferenceValue = null;
                c.FindPropertyRelative("labels").ClearArray();
                c.FindPropertyRelative("subParameters").ClearArray();
                c.FindPropertyRelative("value").floatValue = 1;
            };
            _controlsList.drawHeaderCallback = rect =>
            {
                if (_isMoving && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
                {
                    _isMoving = false;
                    Repaint();
                }

                EditorGUI.LabelField(rect, $"Controls ({_controlsList.count} / 8)");

                // Draw copy, paste, duplicate, and move buttons

                #region Rects

                var copyRect = new Rect(
                    rect.x + rect.width - rect.height - ((rect.height + Toolbox.Styles.Padding) * 3),
                    rect.y,
                    rect.height,
                    rect.height);

                var pasteRect = new Rect(
                    copyRect.x + copyRect.width + Toolbox.Styles.Padding,
                    copyRect.y,
                    copyRect.height,
                    copyRect.height);

                var duplicateRect = new Rect(
                    pasteRect.x + pasteRect.width + Toolbox.Styles.Padding,
                    pasteRect.y,
                    pasteRect.height,
                    pasteRect.height);

                var moveRect = new Rect(
                    duplicateRect.x + duplicateRect.width + Toolbox.Styles.Padding,
                    duplicateRect.y,
                    duplicateRect.height,
                    duplicateRect.height);

                #endregion

                bool isFull = _controlsList.count >= 8;
                bool isEmpty = _controlsList.count == 0;
                bool hasIndex = _controlsList.TryGetActiveIndex(out int index);
                bool hasFocus = _controlsList.HasKeyboardControl();
                if (!hasIndex) index = _controlsList.count;
                using (new EditorGUI.DisabledScope(isEmpty || !hasFocus || !hasIndex))
                {
                    #region Copy

                    Helpers.w_MakeRectLinkCursor(copyRect);
                    if (GUI.Button(copyRect, Toolbox.GUIContent.Copy, GUI.skin.label))
                        CopyControl(index);

                    #endregion

                    // This section was also created entirely by GitHub Copilot :3

                    #region Duplicate

                    using (new EditorGUI.DisabledScope(isFull))
                    {
                        Helpers.w_MakeRectLinkCursor(duplicateRect);
                        if (GUI.Button(duplicateRect,
                                isFull
                                    ? new GUIContent(Toolbox.GUIContent.Duplicate)
                                        { tooltip = Toolbox.GUIContent.MenuFullTooltip }
                                    : Toolbox.GUIContent.Duplicate, GUI.skin.label))
                            DuplicateControl(index);
                    }

                    #endregion
                }

                #region Paste

                using (new EditorGUI.DisabledScope(!CanPasteControl()))
                {
                    Helpers.w_MakeRectLinkCursor(pasteRect);
                    if (GUI.Button(pasteRect, Toolbox.GUIContent.Paste, GUI.skin.label))
                    {
                        var menu = new GenericMenu();
                        menu.AddItem(new GUIContent("Paste values"), false,
                            isEmpty || !hasFocus
                                ? (GenericMenu.MenuFunction)null
                                : () => PasteControl(index, false));
                        menu.AddItem(
                            new GUIContent("Insert as new"),
                            false,
                            isFull ? (GenericMenu.MenuFunction)null : () => PasteControl(index, true)
                        );
                        menu.ShowAsContext();
                    }
                }

                #endregion


                #region Move

                using (new EditorGUI.DisabledScope((_isMoving && isFull) || (!_isMoving && (!hasFocus || isEmpty))))
                {
                    Helpers.w_MakeRectLinkCursor(moveRect);
                    if (GUI.Button(moveRect,
                            _isMoving
                                ? isFull
                                    ? new GUIContent(Toolbox.GUIContent.Place)
                                        { tooltip = Toolbox.GUIContent.MenuFullTooltip }
                                    : Toolbox.GUIContent.Place
                                : Toolbox.GUIContent.Move, GUI.skin.label))
                    {
                        if (!_isMoving) MoveControl(index);
                        else PlaceControl(index);
                    }
                }

                #endregion
            };
            _controlsList.drawElementCallback = (rect2, index, _, focused) =>
            {
                if (!(index < l.arraySize && index >= 0)) return;
                var controlProp = l.GetArrayElementAtIndex(index);
                var controlType = controlProp.FindPropertyRelative("type").ToControlType();
                Rect removeRect = new Rect(rect2.width + 3, rect2.y + 1, 32, 18);
                rect2.width -= 48;
                // Draw control type
                EditorGUI.LabelField(rect2, controlType.ToString(), focused
                    ? Toolbox.Styles.Label.TypeFocused
                    : Toolbox.Styles.Label.Type);

                // Draw control name
                var nameGuiContent = new GUIContent(controlProp.FindPropertyRelative("name").stringValue);
                bool emptyName = string.IsNullOrEmpty(nameGuiContent.text);
                if (emptyName) nameGuiContent.text = "[Unnamed]";

                var nameRect = new Rect(rect2.x, rect2.y,
                    Toolbox.Styles.Label.RichText.CalcSize(nameGuiContent).x, rect2.height);

                EditorGUI.LabelField(nameRect,
                    new GUIContent(nameGuiContent),
                    emptyName
                        ? Toolbox.Styles.Label.PlaceHolder
                        : Toolbox.Styles.Label.RichText);

                Helpers.w_MakeRectLinkCursor(removeRect);
                if (GUI.Button(removeRect, Toolbox.GUIContent.Remove,
                        Toolbox.Styles.Label.RemoveIcon))
                    DeleteControl(index);

                var e = Event.current;

                if (controlType == VRCExpressionsMenu.Control.ControlType.SubMenu)
                {
                    if (e.clickCount == 2 && e.type == EventType.MouseDown && rect2.Contains(e.mousePosition))
                    {
                        var sm = controlProp.FindPropertyRelative("subMenu").objectReferenceValue;
                        if (sm) Selection.activeObject = sm;
                        e.Use();
                    }
                }

                if (e.type == EventType.ContextClick && rect2.Contains(e.mousePosition))
                {
                    e.Use();
                    var menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Cut"), false, () => MoveControl(index));
                    menu.AddItem(new GUIContent("Copy"), false, () => CopyControl(index));
                    if (!CanPasteControl()) menu.AddDisabledItem(new GUIContent("Paste"));
                    else
                    {
                        menu.AddItem(new GUIContent("Paste/Values"), false, () => PasteControl(index, false));
                        menu.AddItem(new GUIContent("Paste/As New"), false, () => PasteControl(index, true));
                    }

                    menu.AddSeparator(string.Empty);
                    menu.AddItem(new GUIContent("Duplicate"), false, () => DuplicateControl(index));
                    menu.AddItem(new GUIContent("Delete"), false, () => DeleteControl(index));
                    menu.ShowAsContext();
                }
            };
        }

        private VRCExpressionParameters.Parameter FetchParameter(string name)
        {
            if (!_avatar || !_avatar.expressionParameters) return null;
            var par = _avatar.expressionParameters;
            return par.parameters?.FirstOrDefault(p => p.name == name);
        }

        #endregion

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            HandleControlEvents();
            DrawHistory();
            DrawHead();
            DrawBody();
            serializedObject.ApplyModifiedProperties();
        }

        private void OnEnable()
        {
            ReInitializeAll();
        }

        private void DrawHistory()
        {
            using (new GUILayout.HorizontalScope("helpbox"))
            {
                void CheckHistory()
                {
                    for (LinkedListNode<VRCExpressionsMenu> node = MenuHistory.First; node != null;)
                    {
                        LinkedListNode<VRCExpressionsMenu> next = node.Next;
                        if (node.Value == null) MenuHistory.Remove(node);
                        node = next;
                    }
                }

                void SetCurrentNode(LinkedListNode<VRCExpressionsMenu> node)
                {
                    if (node.Value == null) return;
                    _currentNode = node;
                    Selection.activeObject = _lastMenu = _currentNode.Value;
                }

                using (new EditorGUI.DisabledScope(_currentNode.Previous == null))
                {
                    using (new EditorGUI.DisabledScope(_currentNode.Previous == null))
                    {
                        if (Helpers.ClickableButton("<<", GUILayout.ExpandWidth(false)))
                        {
                            CheckHistory();
                            SetCurrentNode(MenuHistory.First);
                        }

                        if (Helpers.ClickableButton("<", GUILayout.ExpandWidth(false)))
                        {
                            CheckHistory();
                            SetCurrentNode(_currentNode.Previous);
                        }
                    }
                }

                if (Helpers.ClickableButton(_lastMenu.name, Toolbox.Styles.Label.Centered,
                        GUILayout.ExpandWidth(true)))
                    EditorGUIUtility.PingObject(_lastMenu);

                using (new EditorGUI.DisabledScope(_currentNode.Next == null))
                {
                    if (Helpers.ClickableButton(">", GUILayout.ExpandWidth(false)))
                    {
                        CheckHistory();
                        SetCurrentNode(_currentNode.Next);
                    }

                    if (Helpers.ClickableButton(">>", GUILayout.ExpandWidth(false)))
                    {
                        CheckHistory();
                        SetCurrentNode(MenuHistory.Last);
                    }
                }
            }
        }

        private void DrawHead()
        {
            #region Avatar Selector

            // Generate name string array
            var targetsAsString = _validAvatars.Select(t => t.gameObject.name).ToArray();

            // Draw Avatar Selection
            using (new GUILayout.VerticalScope("helpbox"))
                Core.GetAvatar(ref VRCSDKPlus.GetAvatarRef(), ref VRCSDKPlus.GetValidAvatarsRef());

            #endregion
        }

        void DrawBody()
        {
            if (_controlsList == null)
                InitializeList();

            if (_controlsList.index == -1 && _controlsList.count != 0)
                _controlsList.index = 0;

            _controlsList.DoLayoutList();
            if (_controlsList.count == 0)
                _controlsList.index = -1;

            // EditorGUILayout.Separator();

            var control = _controlsList.index < 0 || _controlsList.index >= _controlsList.count
                ? null
                : _controlsList.serializedProperty.GetArrayElementAtIndex(_controlsList.index);
            var expressionParameters = _avatar == null ? null : _avatar.expressionParameters;

            if (Toolbox.Preferences.CompactMode)
                ControlRenderer.DrawControlCompact(control, expressionParameters);
            else
                ControlRenderer.DrawControl(control, expressionParameters);
        }

        private void HandleControlEvents()
        {
            if (!_controlsList.HasKeyboardControl()) return;
            if (!_controlsList.TryGetActiveIndex(out int index)) return;
            bool fullMenu = _controlsList.count >= 8;

            bool WarnIfFull()
            {
                if (fullMenu)
                {
                    Debug.LogWarning(Toolbox.GUIContent.MenuFullTooltip);
                    return true;
                }

                return false;
            }

            if (Toolbox.HasReceivedAnyDelete())
                DeleteControl(index);

            if (Toolbox.HasReceivedCommand(Toolbox.EventCommands.Duplicate))
                if (!WarnIfFull())
                    DuplicateControl(index);

            if (Toolbox.HasReceivedCommand(Toolbox.EventCommands.Copy))
                CopyControl(index);

            if (Toolbox.HasReceivedCommand(Toolbox.EventCommands.Cut))
                MoveControl(index);

            if (Toolbox.HasReceivedCommand(Toolbox.EventCommands.Paste))
                if (_isMoving && !WarnIfFull()) PlaceControl(index);
                else if (CanPasteControl() && !WarnIfFull()) PasteControl(index, true);
        }

        #region Control Methods

        private void CopyControl(int index)
        {
            EditorGUIUtility.systemCopyBuffer =
                Toolbox.Strings.ClipboardPrefixControl +
                JsonUtility.ToJson(((VRCExpressionsMenu)target).controls[index]);
        }

        private static bool CanPasteControl() =>
            EditorGUIUtility.systemCopyBuffer.StartsWith(Toolbox.Strings.ClipboardPrefixControl);

        private void PasteControl(int index, bool asNew)
        {
            if (!CanPasteControl()) return;
            if (!asNew)
            {
                var control = JsonUtility.FromJson<VRCExpressionsMenu.Control>(
                    EditorGUIUtility.systemCopyBuffer.Substring(Toolbox.Strings.ClipboardPrefixControl
                        .Length));

                Undo.RecordObject(target, "Paste control values");
                _lastMenu.controls[index] = control;
                EditorUtility.SetDirty(_lastMenu);
            }
            else
            {
                var newControl = JsonUtility.FromJson<VRCExpressionsMenu.Control>(
                    EditorGUIUtility.systemCopyBuffer.Substring(Toolbox.Strings.ClipboardPrefixControl
                        .Length));

                Undo.RecordObject(target, "Insert control as new");
                if (_lastMenu.controls.Count <= 0)
                {
                    _lastMenu.controls.Add(newControl);
                    _controlsList.index = 0;
                }
                else
                {
                    var insertIndex = index + 1;
                    if (insertIndex < 0) insertIndex = 0;
                    _lastMenu.controls.Insert(insertIndex, newControl);
                    _controlsList.index = insertIndex;
                }

                EditorUtility.SetDirty(_lastMenu);
            }
        }

        private void DuplicateControl(int index)
        {
            var controlsProp = _controlsList.serializedProperty;
            controlsProp.InsertArrayElementAtIndex(index);
            _controlsList.index = index + 1;

            var newElement = controlsProp.GetArrayElementAtIndex(index + 1);
            var lastName = newElement.FindPropertyRelative("name").stringValue;
            newElement.FindPropertyRelative("name").stringValue =
                Toolbox.GenerateUniqueString(lastName, newName => newName != lastName, false);

            if (Event.current.shift) return;
            var menuParameter = newElement.FindPropertyRelative("parameter");
            if (menuParameter == null) return;
            var parName = menuParameter.FindPropertyRelative("name").stringValue;
            if (string.IsNullOrEmpty(parName)) return;
            var matchedParameter = FetchParameter(parName);
            if (matchedParameter == null) return;
            var controlType = newElement.FindPropertyRelative("type").ToControlType();
            if (controlType != VRCExpressionsMenu.Control.ControlType.Button &&
                controlType != VRCExpressionsMenu.Control.ControlType.Toggle) return;

            if (matchedParameter.valueType == VRCExpressionParameters.ValueType.Bool)
            {
                menuParameter.FindPropertyRelative("name").stringValue =
                    Toolbox.GenerateUniqueString(parName, s => s != parName, false);
            }
            else
            {
                var controlValueProp = newElement.FindPropertyRelative("value");
                if (Mathf.RoundToInt(controlValueProp.floatValue) == controlValueProp.floatValue)
                    controlValueProp.floatValue++;
            }
        }

        private void DeleteControl(int index)
        {
            if (_controlsList.index == index) _controlsList.index--;
            _controlsList.serializedProperty.DeleteArrayElementAtIndex(index);
        }

        private void MoveControl(int index)
        {
            _isMoving = true;
            _moveSourceMenu = _lastMenu;
            _moveTargetControl = _lastMenu.controls[index];
        }

        private void PlaceControl(int index)
        {
            _isMoving = false;
            if (_moveSourceMenu && _moveTargetControl != null)
            {
                Undo.RecordObject(target, "Move control");
                Undo.RecordObject(_moveSourceMenu, "Move control");

                if (_lastMenu.controls.Count <= 0)
                    _lastMenu.controls.Add(_moveTargetControl);
                else
                {
                    var insertIndex = index + 1;
                    if (insertIndex < 0) insertIndex = 0;
                    _lastMenu.controls.Insert(insertIndex, _moveTargetControl);
                    _moveSourceMenu.controls.Remove(_moveTargetControl);
                }

                EditorUtility.SetDirty(_moveSourceMenu);
                EditorUtility.SetDirty(target);

                if (Event.current.shift) Selection.activeObject = _moveSourceMenu;
            }
        }

        #endregion

        public void AddItemsToMenu(GenericMenu menu) => menu.AddItem(new GUIContent("Compact Mode"),
            Toolbox.Preferences.CompactMode, ToggleCompactMode);

        private static void ToggleCompactMode() => Toolbox.Preferences.CompactMode =
            !Toolbox.Preferences.CompactMode;

        [MenuItem("CONTEXT/VRCExpressionsMenu/[SDK+] Toggle Editor", false, 899)]
        private static void ToggleEditor()
        {
            _editorActive = !_editorActive;
            var targetType = Helpers.ExtendedGetType("VRCExpressionsMenu");
            if (targetType == null)
            {
                Debug.LogError("[VRCSDK+] VRCExpressionsMenu was not found! Could not apply custom editor.");
                return;
            }

            if (_editorActive) AutomatedMethods.OverrideEditor(targetType, typeof(VRCMenuPlus));
            else
            {
                var menuEditor = Helpers.ExtendedGetType("VRCExpressionsMenuEditor");
                if (menuEditor == null)
                {
                    Debug.LogWarning(
                        "[VRCSDK+] VRCExpressionsMenuEditor was not found! Could not apply custom editor.");
                    return;
                }

                AutomatedMethods.OverrideEditor(targetType, menuEditor);
            }
            //else OverrideEditor(typeof(VRCExpressionsMenu), Type.GetType("VRCExpressionsMenuEditor, Assembly-CSharp-Editor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null"));
        }
}
}