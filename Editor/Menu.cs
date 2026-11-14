using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using ControlType = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control.ControlType;

namespace AwAVR.VRCSDKPlus {
    internal sealed class VRCMenuPlus : Editor, IHasCustomMenu {
        private static bool _editorActive = true;

        private VRCExpressionsMenu _menu;
        private SerializedProperty _controls;

        private VisualElement _root;
        private EditorWindow _window;
        private MenuHistory _history;

        private Button _firstButton, _backButton, _forwardButton, _lastButton, _historyTitle;
        private Core.AvatarSelector _avatarSelector;

        private Label _countLabel;
        private Button _addButton, _copyButton, _pasteButton, _duplicateButton, _moveButton;
        private ToolbarToggle _compactToggle;

        private ListView _listView;
        private readonly List<int> _items = new List<int>();

        private VisualElement _controlContainer;
        private ControlEditor _controlEditor;
        private int _controlEditorIndex = -1;
        private int _controlEditorArraySize = -1;
        private bool _controlEditorCompact;

        private string SelectionKey => $"VSP_MenuSelection_{(_menu ? _menu.GetInstanceID() : 0)}";
        private int SelectedIndex => _listView?.selectedIndex ?? -1;
        private static bool ShiftHeld => Event.current != null && Event.current.shift;

        #region Initialization

        private void OnEnable() {
            _menu = target as VRCExpressionsMenu;
            if (!_menu) return;
            MenuOperations.GetControls(_menu);
            _controls = serializedObject.FindProperty("controls");
            VRCSDKPlus.AvatarInfoChanged -= OnAvatarInfoChanged;
            VRCSDKPlus.AvatarInfoChanged += OnAvatarInfoChanged;
            VRCSDKPlus.RefreshAvatar(a => a.expressionsMenu && IsSubmenuRecursive(_menu, a.expressionsMenu));
        }

        private void OnDisable() {
            VRCSDKPlus.AvatarInfoChanged -= OnAvatarInfoChanged;
            _controls = null;
        }

        /// <summary>False once the inspector dropped this editor (its SerializedObject may already be disposed).</summary>
        private bool IsAlive() {
            if (!_menu || _controls == null) return false;
            try {
                return _controls.arraySize >= 0;
            }
            catch (Exception) {
                return false;
            }
        }

        private static bool IsSubmenuRecursive(VRCExpressionsMenu needle, VRCExpressionsMenu haystack,
            HashSet<VRCExpressionsMenu> visited = null) {
            if (!haystack) return false;
            if (haystack == needle) return true;
            visited = visited ?? new HashSet<VRCExpressionsMenu>();
            if (!visited.Add(haystack) || haystack.controls == null) return false;
            return haystack.controls.Any(c => c.subMenu && IsSubmenuRecursive(needle, c.subMenu, visited));
        }

        public override VisualElement CreateInspectorGUI() {
            if (!_menu) return base.CreateInspectorGUI();

            serializedObject.Update();
            _controls = serializedObject.FindProperty("controls");

            _root = new VisualElement();
            Toolbox.AddStyleSheet(_root);
            _root.Add(CreateHistoryBar());
            _root.Add(CreateAvatarSection());
            _root.Add(CreateControlsHeader());
            _root.Add(CreateControlsList());
            _controlContainer = new VisualElement();
            _root.Add(_controlContainer);

            _root.Add(Toolbox.CreateChangeTracker(serializedObject, OnDataChanged));

            _root.RegisterCallback<AttachToPanelEvent>(OnAttach);
            _root.RegisterCallback<KeyDownEvent>(evt => {
                if (evt.keyCode != KeyCode.Escape || !MenuOperations.IsMoving) return;
                MenuOperations.CancelMove();
                RefreshHeader();
                _listView.RefreshItems();
                evt.StopPropagation();
            });

            // Clipboard and cut state can change in other inspectors, keep the buttons up to date.
            _root.schedule.Execute(RefreshHeader).Every(300);

            RebuildItems();
            int savedSelection = SessionState.GetInt(SelectionKey, 0);
            if (_items.Count > 0) _listView.SetSelectionWithoutNotify(new[] { Mathf.Clamp(savedSelection, 0, _items.Count - 1) });
            RebuildControlEditor();
            RefreshHeader();
            return _root;
        }

        private void OnAttach(AttachToPanelEvent _) {
            _window = MenuHistory.FindHostWindow(_root);
            _history = MenuHistory.ForWindow(_window);
            _history.Visit(_menu);
            RefreshHistoryBar();
        }

        #endregion

        #region Layout

        private VisualElement CreateHistoryBar() {
            var bar = new VisualElement();
            bar.AddToClassList("sdkp-history");
            bar.AddToClassList("sdkp-section");

            _firstButton = new Button(() => Navigate(_history?.First())) { text = "«", tooltip = "First menu in history" };
            _backButton = new Button(() => Navigate(_history?.Back())) { text = "‹", tooltip = "Previous menu" };
            _historyTitle = new Button(() => EditorGUIUtility.PingObject(_menu)) {
                text = _menu.name,
                tooltip = "Click to ping this menu, right-click for the history list"
            };
            _historyTitle.AddToClassList("sdkp-history-title");
            _historyTitle.AddManipulator(new ContextualMenuManipulator(PopulateHistoryMenu));

            var historyList = new Button(ShowHistoryList) { text = "▾", tooltip = "History" };
            historyList.AddToClassList("sdkp-dropdown-button");

            _forwardButton = new Button(() => Navigate(_history?.Forward())) { text = "›", tooltip = "Next menu" };
            _lastButton = new Button(() => Navigate(_history?.Last())) { text = "»", tooltip = "Last menu in history" };

            bar.Add(_firstButton);
            bar.Add(_backButton);
            bar.Add(_historyTitle);
            bar.Add(historyList);
            bar.Add(_forwardButton);
            bar.Add(_lastButton);
            return bar;
        }

        private VisualElement CreateAvatarSection() {
            var box = new VisualElement();
            box.AddToClassList("sdkp-section");
            _avatarSelector = new Core.AvatarSelector {
                FavoredAvatar = a => a.expressionsMenu && IsSubmenuRecursive(_menu, a.expressionsMenu),
                AutoSelectFirst = true
            };
            _avatarSelector.SetValueWithoutNotify(VRCSDKPlus.GetAvatar());
            _avatarSelector.RegisterValueChangedCallback(evt => VRCSDKPlus.SetAvatar(evt.newValue));
            _avatarSelector.Refresh();
            _avatarSelector.Q<ObjectField>()?.Aligned();
            box.Add(_avatarSelector);
            return box;
        }

        private VisualElement CreateControlsHeader() {
            var header = new VisualElement();
            header.AddToClassList("sdkp-controls-header");
            header.style.marginTop = 4;

            _countLabel = new Label();
            _countLabel.AddToClassList("sdkp-grow");
            header.Add(_countLabel);

            _addButton = Toolbox.IconButton(Toolbox.Strings.IconAdd, "Add a control",
                () => Run(() => MenuOperations.Add(_menu, MenuOperations.InsertIndexAfter(_menu, SelectedIndex), out var i) ? i : -1));
            _copyButton = Toolbox.IconButton(Toolbox.Strings.IconCopy, "Copy", () => MenuOperations.Copy(_menu, SelectedIndex));
            _pasteButton = Toolbox.IconButton(Toolbox.Strings.IconPaste, "Paste", ShowPasteMenu);
            _duplicateButton = Toolbox.IconButton(Toolbox.Strings.IconDuplicate, "Duplicate (hold Shift to keep the same parameter)",
                () => DuplicateSelected(!ShiftHeld));
            _moveButton = Toolbox.IconButton(Toolbox.Strings.IconMove, "Move", () => {
                if (MenuOperations.IsMoving) PlaceAfterSelection();
                else CutSelected();
            });

            _compactToggle = new ToolbarToggle {
                text = "Compact",
                value = Toolbox.Preferences.CompactMode,
                tooltip = "Compact layout for the control editor"
            };
            _compactToggle.style.marginLeft = 6;
            _compactToggle.RegisterValueChangedCallback(evt => {
                Toolbox.Preferences.CompactMode = evt.newValue;
                RebuildControlEditor();
            });

            header.Add(_addButton);
            header.Add(_copyButton);
            header.Add(_pasteButton);
            header.Add(_duplicateButton);
            header.Add(_moveButton);
            header.Add(_compactToggle);
            return header;
        }

        private VisualElement CreateControlsList() {
            _listView = new ListView(_items, 20, () => new ControlItem(this), (element, index) => ((ControlItem)element).Bind(index)) {
                reorderable = true,
                reorderMode = ListViewReorderMode.Animated,
                selectionType = SelectionType.Single,
                showBorder = true,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight
            };
            _listView.style.marginBottom = 4;

            _listView.itemIndexChanged += (from, to) => {
                serializedObject.Update();
                if (from < 0 || to < 0 || from >= _controls.arraySize || to >= _controls.arraySize) {
                    RebuildItems();
                    return;
                }

                _controls.MoveArrayElement(from, to);
                serializedObject.ApplyModifiedProperties();
                RebuildItems();
                Select(to);
            };
            _listView.selectionChanged += _ => {
                SessionState.SetInt(SelectionKey, SelectedIndex);
                RebuildControlEditor();
                RefreshHeader();
            };
            _listView.itemsChosen += _ => OpenSubMenu(SelectedIndex);

            Toolbox.RegisterCommands(_listView, CanExecute, Execute);
            return _listView;
        }

        #endregion

        #region Refresh

        private void OnAvatarInfoChanged() {
            if (!IsAlive()) return;
            if (_avatarSelector != null && _avatarSelector.value != VRCSDKPlus.GetAvatar())
                _avatarSelector.SetValueWithoutNotify(VRCSDKPlus.GetAvatar());
            _controlEditor?.Refresh();
        }

        private void OnDataChanged() {
            if (!IsAlive()) return;
            if (_controls.arraySize != _items.Count) RebuildItems();
            else _listView.RefreshItems();

            if (_items.Count > 0 && (SelectedIndex < 0 || SelectedIndex >= _items.Count))
                _listView.SetSelectionWithoutNotify(new[] { Mathf.Clamp(SelectedIndex, 0, _items.Count - 1) });

            RefreshControlEditor();
            RefreshHeader();
        }

        private void RebuildItems() {
            _items.Clear();
            for (int i = 0; i < _controls.arraySize; i++) _items.Add(i);
            _listView.Rebuild();
        }

        private void Select(int index) {
            if (index < 0 || index >= _items.Count) {
                _listView.ClearSelection();
                return;
            }

            _listView.SetSelection(index);
            _listView.ScrollToItem(index);
        }

        private void RefreshControlEditor() {
            int index = SelectedIndex;
            bool valid = index >= 0 && index < _controls.arraySize;
            bool needsRebuild = _controlEditor == null
                ? valid
                : !valid || index != _controlEditorIndex || _controls.arraySize != _controlEditorArraySize ||
                  _controlEditorCompact != Toolbox.Preferences.CompactMode ||
                  _controls.GetArrayElementAtIndex(index).FindPropertyRelative("type").ToControlType() !=
                  _controlEditor.BuiltType;

            if (needsRebuild) RebuildControlEditor();
            else _controlEditor?.Refresh();
        }

        private void RebuildControlEditor() {
            if (_controlContainer == null) return;
            _controlContainer.Clear();
            _controlEditor = null;
            _controlEditorIndex = -1;

            int index = SelectedIndex;
            if (index < 0 || index >= _controls.arraySize) {
                _controlContainer.Add(new HelpBox(_controls.arraySize == 0
                    ? "This menu has no controls yet. Press + to add one."
                    : "Select a control to edit it.", HelpBoxMessageType.None));
                return;
            }

            _controlEditorCompact = Toolbox.Preferences.CompactMode;
            _controlEditorIndex = index;
            _controlEditorArraySize = _controls.arraySize;
            _controlEditor = new ControlEditor(_controls.GetArrayElementAtIndex(index), _menu, GetExpressionParameters,
                _controlEditorCompact);
            _controlContainer.Add(_controlEditor);
        }

        private void RefreshHeader() {
            if (_countLabel == null || !IsAlive()) return;
            int count = MenuOperations.GetControls(_menu).Count;
            bool full = count >= MenuOperations.MaxControls;
            bool hasSelection = SelectedIndex >= 0 && SelectedIndex < count;
            bool moving = MenuOperations.IsMoving;
            bool movingHere = moving && MenuOperations.MoveSource == _menu;

            _countLabel.text = $"Controls ({count} / {MenuOperations.MaxControls})";
            _countLabel.style.color = count > MenuOperations.MaxControls ? new Color(1, 0.4f, 0.4f) : StyleKeyword.Null;

            _addButton.SetEnabled(!full);
            _addButton.tooltip = full ? Toolbox.Strings.MenuFullTooltip : "Add a control";
            _copyButton.SetEnabled(hasSelection);
            _pasteButton.SetEnabled(MenuOperations.CanPaste());
            _duplicateButton.SetEnabled(hasSelection && !full);
            _duplicateButton.tooltip = full ? Toolbox.Strings.MenuFullTooltip : "Duplicate (hold Shift to keep the same parameter)";

            Toolbox.SetIcon(_moveButton, moving ? Toolbox.Strings.IconPlace : Toolbox.Strings.IconMove);
            _moveButton.SetEnabled(moving ? movingHere || !full : hasSelection);
            _moveButton.tooltip = !moving ? "Move (cut) the selected control, then place it in this or another menu"
                : !movingHere && full ? Toolbox.Strings.MenuFullTooltip
                : $"Place the cut control after the selection (from {MenuOperations.MoveSource.name}). Esc to cancel, hold Shift to go back to the original menu";

            _compactToggle.SetValueWithoutNotify(Toolbox.Preferences.CompactMode);
        }

        private void RefreshHistoryBar() {
            if (_history == null || _historyTitle == null) return;
            _firstButton.SetEnabled(_history.CanGoBack);
            _backButton.SetEnabled(_history.CanGoBack);
            _forwardButton.SetEnabled(_history.CanGoForward);
            _lastButton.SetEnabled(_history.CanGoForward);
            _historyTitle.text = _menu.name;
        }

        #endregion

        #region History

        private void Navigate(VRCExpressionsMenu menu) {
            if (!menu) {
                RefreshHistoryBar();
                return;
            }

            MenuHistory.Open(_window, menu);
            RefreshHistoryBar();
        }

        private void ShowHistoryList() {
            var menu = new GenericMenu();
            PopulateHistory((label, isCurrent, action) => menu.AddItem(new GUIContent(label), isCurrent, () => action()));
            menu.DropDown(_historyTitle.worldBound);
        }

        private void PopulateHistoryMenu(ContextualMenuPopulateEvent evt) =>
            PopulateHistory((label, isCurrent, action) => evt.menu.AppendAction(label, _ => action(),
                isCurrent ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal));

        private void PopulateHistory(Action<string, bool, Action> addItem) {
            if (_history == null) return;
            var entries = _history.Entries;
            for (int i = entries.Count - 1; i >= 0; i--) {
                int index = i;
                // Prefix with the position so menus with the same name stay separate entries.
                addItem($"{i + 1}. {entries[i].name}", i == _history.Index, () => Navigate(_history.GoTo(index)));
            }
        }

        private void OpenSubMenu(int index) {
            if (index < 0 || index >= _controls.arraySize) return;
            var control = _controls.GetArrayElementAtIndex(index);
            if (control.FindPropertyRelative("type").ToControlType() != ControlType.SubMenu) return;
            if (control.FindPropertyRelative("subMenu").objectReferenceValue is VRCExpressionsMenu subMenu)
                MenuHistory.Open(_window, subMenu);
        }

        #endregion

        #region Control actions

        /// <summary>Runs a direct edit on the menu asset and refreshes the UI, selecting the returned index.</summary>
        private void Run(Func<int> operation) {
            serializedObject.ApplyModifiedProperties();
            int select = operation();
            serializedObject.Update();
            RebuildItems();
            if (select >= 0) Select(select);
            else if (_items.Count > 0 && (SelectedIndex < 0 || SelectedIndex >= _items.Count))
                Select(_items.Count - 1);
            RebuildControlEditor();
            RefreshHeader();
        }

        internal void CopyControl(int index) {
            serializedObject.ApplyModifiedProperties();
            MenuOperations.Copy(_menu, index);
            RefreshHeader();
        }

        internal void DeleteControl(int index) => Run(() => {
            int selected = SelectedIndex;
            if (!MenuOperations.Delete(_menu, index)) return -1;
            int count = MenuOperations.GetControls(_menu).Count;
            if (count == 0) return -1;
            // Keep the same control selected, or the one now in the deleted control's place.
            return selected > index ? selected - 1 : Mathf.Min(selected < 0 ? index : selected, count - 1);
        });

        internal void DuplicateControl(int index, bool smart) => Run(() =>
            MenuOperations.Duplicate(_menu, index, GetExpressionParameters(), smart, out var newIndex) ? newIndex : -1);

        private void DuplicateSelected(bool smart) => DuplicateControl(SelectedIndex, smart);

        internal void PasteControl(int index, bool asNew) => Run(() => {
            if (asNew)
                return MenuOperations.PasteAsNew(_menu, MenuOperations.InsertIndexAfter(_menu, index), out var pasted) ? pasted : -1;
            return MenuOperations.PasteValues(_menu, index) ? index : -1;
        });

        internal void CutControl(int index) {
            serializedObject.ApplyModifiedProperties();
            MenuOperations.BeginMove(_menu, index);
            _listView.RefreshItems();
            RefreshHeader();
        }

        private void CutSelected() => CutControl(SelectedIndex);

        private void PlaceAfterSelection() {
            var source = MenuOperations.MoveSource;
            bool goBack = ShiftHeld;
            Run(() => MenuOperations.Place(_menu, MenuOperations.InsertIndexAfter(_menu, SelectedIndex), out var placed) ? placed : -1);
            if (goBack && source && source != _menu) MenuHistory.Open(_window, source);
        }

        private void ShowPasteMenu() {
            var menu = new GenericMenu();
            bool hasSelection = SelectedIndex >= 0 && SelectedIndex < _items.Count;
            bool full = MenuOperations.IsFull(_menu);
            if (hasSelection) menu.AddItem(new GUIContent("Paste values"), false, () => PasteControl(SelectedIndex, false));
            else menu.AddDisabledItem(new GUIContent("Paste values"));
            if (!full) menu.AddItem(new GUIContent("Insert as new"), false, () => PasteControl(SelectedIndex, true));
            else menu.AddDisabledItem(new GUIContent("Insert as new"));
            menu.DropDown(_pasteButton.worldBound);
        }

        internal void PopulateControlContextMenu(ContextualMenuPopulateEvent evt, int index) {
            bool full = MenuOperations.IsFull(_menu);
            bool canPaste = MenuOperations.CanPaste();
            evt.menu.AppendAction("Cut", _ => CutControl(index));
            evt.menu.AppendAction("Copy", _ => CopyControl(index));
            evt.menu.AppendAction("Paste/Values", _ => PasteControl(index, false),
                canPaste ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            evt.menu.AppendAction("Paste/As New", _ => PasteControl(index, true),
                canPaste && !full ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            if (MenuOperations.IsMoving)
                evt.menu.AppendAction("Place Cut Control Here", _ => {
                    Select(index);
                    PlaceAfterSelection();
                }, MenuOperations.MoveSource == _menu || !full ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            evt.menu.AppendSeparator();
            evt.menu.AppendAction("Duplicate", _ => DuplicateControl(index, true),
                full ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
            evt.menu.AppendAction("Delete", _ => DeleteControl(index));
        }

        private bool CanExecute(Toolbox.EventCommands command) {
            bool hasSelection = SelectedIndex >= 0 && SelectedIndex < _items.Count;
            switch (command) {
                case Toolbox.EventCommands.Copy:
                case Toolbox.EventCommands.Cut:
                case Toolbox.EventCommands.Delete:
                case Toolbox.EventCommands.SoftDelete:
                    return hasSelection;
                case Toolbox.EventCommands.Duplicate:
                    return hasSelection;
                case Toolbox.EventCommands.Paste:
                    return MenuOperations.IsMoving || MenuOperations.CanPaste();
                default:
                    return false;
            }
        }

        private void Execute(Toolbox.EventCommands command) {
            switch (command) {
                case Toolbox.EventCommands.Copy:
                    CopyControl(SelectedIndex);
                    break;
                case Toolbox.EventCommands.Cut:
                    CutSelected();
                    break;
                case Toolbox.EventCommands.Paste:
                    if (MenuOperations.IsMoving) PlaceAfterSelection();
                    else PasteControl(SelectedIndex, true);
                    break;
                case Toolbox.EventCommands.Duplicate:
                    DuplicateSelected(!ShiftHeld);
                    break;
                case Toolbox.EventCommands.Delete:
                case Toolbox.EventCommands.SoftDelete:
                    DeleteControl(SelectedIndex);
                    break;
            }
        }

        #endregion

        internal SerializedProperty Controls => _controls;
        internal VRCExpressionsMenu MenuAsset => _menu;

        private VRCExpressionParameters GetExpressionParameters() {
            var avatar = VRCSDKPlus.GetAvatar();
            if (avatar && avatar.expressionParameters) return avatar.expressionParameters;
            try {
                // Newer SDKs let a menu reference its parameters directly.
                return serializedObject.FindProperty("Parameters")?.objectReferenceValue as VRCExpressionParameters;
            }
            catch (Exception) {
                return null;
            }
        }

        public void AddItemsToMenu(GenericMenu menu) => menu.AddItem(new GUIContent("Compact Mode"),
            Toolbox.Preferences.CompactMode, () => {
                Toolbox.Preferences.CompactMode = !Toolbox.Preferences.CompactMode;
                RebuildControlEditor();
                RefreshHeader();
            });

        [MenuItem("CONTEXT/VRCExpressionsMenu/[SDK+] Toggle Editor", false, 899)]
        private static void ToggleEditor() {
            _editorActive = !_editorActive;
            var targetType = Helpers.ExtendedGetType("VRCExpressionsMenu");
            if (targetType == null) {
                Debug.LogError("[VRCSDK+] VRCExpressionsMenu was not found! Could not apply custom editor.");
                return;
            }

            if (_editorActive) AutomatedMethods.OverrideEditor(targetType, typeof(VRCMenuPlus));
            else {
                var menuEditor = Helpers.ExtendedGetType("VRCExpressionsMenuEditor");
                if (menuEditor == null) {
                    Debug.LogWarning(
                        "[VRCSDK+] VRCExpressionsMenuEditor was not found! Could not apply custom editor.");
                    return;
                }

                AutomatedMethods.OverrideEditor(targetType, menuEditor);
            }
        }

        /// <summary>One row in the controls list: name, type and a remove button.</summary>
        private sealed class ControlItem : VisualElement {
            private readonly VRCMenuPlus _owner;
            private readonly Label _name;
            private readonly Label _type;
            private int _index = -1;

            internal ControlItem(VRCMenuPlus owner) {
                _owner = owner;
                AddToClassList("sdkp-control-item");

                _name = new Label { enableRichText = true };
                _name.AddToClassList("sdkp-control-name");
                Add(_name);

                _type = new Label();
                _type.AddToClassList("sdkp-control-type");
                Add(_type);

                Add(Toolbox.IconButton(Toolbox.Strings.IconRemove, "Remove control", () => _owner.DeleteControl(_index)));

                this.AddManipulator(new ContextualMenuManipulator(evt => _owner.PopulateControlContextMenu(evt, _index)));
            }

            internal void Bind(int index) {
                _index = index;
                var controls = _owner.Controls;
                if (controls == null || index < 0 || index >= controls.arraySize) {
                    _name.text = string.Empty;
                    _type.text = string.Empty;
                    return;
                }

                var control = controls.GetArrayElementAtIndex(index);
                var name = control.FindPropertyRelative("name").stringValue;
                bool empty = string.IsNullOrEmpty(name);
                _name.text = empty ? "[Unnamed]" : name;
                _name.EnableInClassList("sdkp-control-name--empty", empty);
                var type = control.FindPropertyRelative("type").ToControlType();
                _type.text = type.ToString();
                tooltip = type == ControlType.SubMenu ? "Double-click to open the sub menu" : null;
                EnableInClassList("sdkp-moving", MenuOperations.IsMovingControl(_owner.MenuAsset, index));
            }
        }
    }
}
