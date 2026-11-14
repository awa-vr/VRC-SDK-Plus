using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.ScriptableObjects;
using AnimatorControllerParameter = UnityEngine.AnimatorControllerParameter;

namespace AwAVR.VRCSDKPlus {
    internal sealed class VRCParamsPlus : Editor {
        private static bool _editorActive = true;

        private SerializedProperty _parameterList;
        private ParameterStatus[] _parameterStatus = Array.Empty<ParameterStatus>();

        private VisualElement _root;
        private Core.AvatarSelector _avatarSelector;
        private ProgressBar _memoryBar;
        private ToolbarSearchField _searchField;
        private ListView _listView;
        private VisualElement _cleanupBox;
        private Label _cleanupLabel;

        internal SerializedProperty ParameterList => _parameterList;
        internal string SearchValue { get; private set; }
        internal bool ShowAdvanced => ParameterUtility.HasAdvancedOptions && Toolbox.Preferences.ShowAdvancedParameters;

        // Must match the heights in AwAVRSDKPlusStyles.uss (.sdkp-param-item / .sdkp-param-advanced).
        private const float RowHeight = 24;
        private const float AdvancedRowHeight = 72;
        private float CurrentRowHeight => ShowAdvanced ? AdvancedRowHeight : RowHeight;

        private VisualElement _header;
        private Label _headerName, _headerType, _headerDefault, _headerSaved, _headerSynced;

        private void OnEnable() {
            if (!target) return;
            var expressionParameters = (VRCExpressionParameters)target;
            if (expressionParameters.parameters == null) {
                expressionParameters.parameters = Array.Empty<VRCExpressionParameters.Parameter>();
                EditorUtility.SetDirty(expressionParameters);
            }

            _parameterList = serializedObject.FindProperty("parameters");
            VRCSDKPlus.AvatarInfoChanged -= OnAvatarInfoChanged;
            VRCSDKPlus.AvatarInfoChanged += OnAvatarInfoChanged;
            VRCSDKPlus.RefreshAvatar(a => a.expressionParameters == target);
            RefreshAllParameterStatus();
        }

        private void OnDisable() {
            VRCSDKPlus.AvatarInfoChanged -= OnAvatarInfoChanged;
            _parameterList = null;
        }

        /// <summary>
        /// False once the inspector dropped this editor. Its SerializedObject can be disposed before OnDisable runs,
        /// e.g. while the next editor's OnEnable refreshes the shared avatar info.
        /// </summary>
        private bool IsAlive() {
            if (_parameterList == null) return false;
            try {
                return _parameterList.arraySize >= 0 && serializedObject.targetObject;
            }
            catch (Exception) {
                return false;
            }
        }

        public override VisualElement CreateInspectorGUI() {
            serializedObject.Update();
            _root = new VisualElement();
            Toolbox.AddStyleSheet(_root);

            _root.Add(CreateAvatarSection());
            _root.Add(CreateMemoryToolbar());
            _root.Add(CreateOptionsRow());
            _root.Add(CreateListHeader());
            _root.Add(CreateParameterList());
            _root.Add(CreateCleanupBox());
            _root.Add(CreateMergeField());

            // Refresh warnings, memory and rows whenever the asset changes (edits, undo, other inspectors).
            _root.Add(Toolbox.CreateChangeTracker(serializedObject, OnDataChanged));

            Toolbox.RegisterCommands(_root, command => command == Toolbox.EventCommands.Find, _ => _searchField.Focus());

            OnDataChanged();
            return _root;
        }

        #region Layout

        private VisualElement CreateAvatarSection() {
            var box = new VisualElement();
            box.AddToClassList("sdkp-section");
            _avatarSelector = new Core.AvatarSelector {
                FavoredAvatar = a => a.expressionParameters == target,
                AutoSelectFirst = true
            };
            _avatarSelector.SetValueWithoutNotify(VRCSDKPlus.GetAvatar());
            _avatarSelector.RegisterValueChangedCallback(evt => VRCSDKPlus.SetAvatar(evt.newValue));
            _avatarSelector.Refresh();
            _avatarSelector.Q<ObjectField>()?.Aligned();
            box.Add(_avatarSelector);
            return box;
        }

        private VisualElement CreateMemoryToolbar() {
            var toolbar = new Toolbar();
            toolbar.style.marginTop = 2;
            toolbar.style.marginBottom = 2;

            _memoryBar = new ProgressBar { lowValue = 0, highValue = ParameterUtility.MaxMemoryCost };
            _memoryBar.AddToClassList("sdkp-memory-bar");
            toolbar.Add(_memoryBar);

            var help = Toolbox.Icon(Toolbox.Strings.IconHelp,
                $"VRChat only allows {ParameterUtility.MaxMemoryCost} synced parameter bits on an avatar. Only synced parameters in the VRC Expression Parameters are counted towards this." +
                "\n\n" +
                "Parameters in the animator can be any type that you want, they don't need to be the same as in the VRC Expression Parameters list. VRChat will automatically convert them, see more here: https://creators.vrchat.com/avatars/animator-parameters/#mismatched-parameter-type-conversion" +
                "\n\n" +
                "- bool = 1 bit\n" +
                "- int = 8 bits by default\n" +
                "- float = 8 bits by default" +
                (ParameterUtility.HasAdvancedOptions
                    ? "\n\nThe bits used by an int or float can be changed with \"Show Advanced Options\"."
                    : string.Empty));
            help.style.alignSelf = Align.Center;
            toolbar.Add(help);

            toolbar.Add(new ToolbarButton(() => {
                VRCSDKPlus.RefreshAvatarInfo();
                Helpers.GreenLog("Refreshed parameters from the avatar's animators");
            }) { text = "Refresh Parameters", tooltip = "Re-scan the avatar's animators for parameters" });

            return toolbar;
        }

        private VisualElement CreateOptionsRow() {
            var row = new VisualElement();
            row.AddToClassList("sdkp-row");
            row.style.marginTop = 2;
            row.style.marginBottom = 4;

            if (ParameterUtility.HasAdvancedOptions) {
                var advancedToggle = new Toggle("Show Advanced Options") {
                    value = Toolbox.Preferences.ShowAdvancedParameters,
                    tooltip = "Show lerp duration, bit width and representation for each parameter (VRCSDK 3.10.6+)"
                };
                advancedToggle.labelElement.style.minWidth = 0;
                advancedToggle.labelElement.style.marginRight = 6;
                advancedToggle.RegisterValueChangedCallback(evt => {
                    Toolbox.Preferences.ShowAdvancedParameters = evt.newValue;
                    _listView.fixedItemHeight = CurrentRowHeight;
                    _listView.Rebuild();
                });
                row.Add(advancedToggle);
            }

            _searchField = new ToolbarSearchField { tooltip = "Search parameters by name (regex). Ctrl+F" };
            _searchField.AddToClassList("sdkp-grow");
            _searchField.style.width = StyleKeyword.Auto;
            _searchField.RegisterValueChangedCallback(evt => {
                SearchValue = evt.newValue;
                RefreshRows();
            });
            _searchField.RegisterCallback<KeyDownEvent>(evt => {
                if (evt.keyCode != KeyCode.Escape) return;
                _searchField.value = string.Empty;
                evt.StopPropagation();
            });
            row.Add(_searchField);
            return row;
        }

        private VisualElement CreateListHeader() {
            _header = new VisualElement();
            _header.AddToClassList("sdkp-param-header");

            Label Column(string text, string tooltip) {
                var label = new Label(text) { tooltip = tooltip, pickingMode = PickingMode.Position };
                label.AddToClassList("sdkp-header-label");
                _header.Add(label);
                return label;
            }

            _headerName = Column("Name",
                "Name of the Parameter. This must match the name of the parameter that it is controlling in the playable layers. Case sensitive.");
            _headerType = Column("Type", "Type of the Parameter.");
            _headerDefault = Column("Default", "The default/start value of this parameter.");
            _headerSaved = Column("Saved", "Value will stay when loading avatar or changing worlds");
            if (ParameterUtility.HasSyncingOption)
                _headerSynced = Column("Synced",
                    "Value will be sent over the network to remote users. This is needed if this value should be the same locally and remotely. Synced parameters count towards the total memory usage.");

            _header.RegisterCallback<GeometryChangedEvent>(_ => AlignHeader());
            return _header;
        }

        /// <summary>Puts each header label exactly over its column in the first row, whatever the inspector width.</summary>
        private void AlignHeader() {
            var row = _listView?.Q<ParameterRow>();
            if (_header == null || row == null || _header.panel == null || float.IsNaN(_header.worldBound.x) ||
                float.IsNaN(_header.worldBound.width)) return;

            bool Valid(float f) => !float.IsNaN(f) && !float.IsInfinity(f);

            void Place(Label label, VisualElement column) {
                if (label == null) return;
                var bounds = column?.worldBound ?? Rect.zero;
                // Rows that aren't laid out yet report NaN, which must never reach a style value.
                if (column == null || column.resolvedStyle.display == DisplayStyle.None || !Valid(bounds.x) ||
                    !Valid(bounds.width) || bounds.width <= 0) {
                    label.style.visibility = Visibility.Hidden;
                    return;
                }

                // At least as wide as the text, centered on the column.
                float textWidth = label.MeasureTextSize(label.text, 0, VisualElement.MeasureMode.Undefined, 0,
                    VisualElement.MeasureMode.Undefined).x + 4;
                float width = Valid(textWidth) ? Mathf.Max(bounds.width, textWidth) : bounds.width;
                float left = bounds.center.x - _header.worldBound.x - width / 2;
                if (!Valid(left)) return;
                label.style.visibility = Visibility.Visible;
                label.style.left = left;
                label.style.width = width;
            }

            Place(_headerName, row.NameColumn);
            Place(_headerType, row.TypeColumn);
            Place(_headerDefault, row.DefaultColumn);
            Place(_headerSaved, row.SavedColumn);
            Place(_headerSynced, row.SyncedColumn);
        }

        private VisualElement CreateParameterList() {
            _listView = new ListView {
                reorderable = true,
                reorderMode = ListViewReorderMode.Animated,
                // Fixed heights keep the list exactly as tall as its rows, so it never shows a second scrollbar
                // inside the inspector (dynamic heights are estimated and the advanced rows are taller).
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                fixedItemHeight = CurrentRowHeight,
                showAddRemoveFooter = true,
                showBorder = true,
                showBoundCollectionSize = false,
                showFoldoutHeader = false,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly,
                selectionType = SelectionType.Single,
                makeItem = () => new ParameterRow(this),
                bindItem = (element, index) => ((ParameterRow)element).Bind(index),
                unbindItem = (element, _) => ((ParameterRow)element).Unbind()
            };
            // Unity's default stylesheet caps lists at 4000px, after which the list scrolls inside the inspector's own
            // scroll view (two scrollbars, also in the VRCSDK editor). Let it grow with its rows instead.
            _listView.style.maxHeight = StyleKeyword.None;
            _listView.RegisterCallback<GeometryChangedEvent>(_ => AlignHeader());

            _listView.itemsAdded += indices => {
                serializedObject.Update();
                foreach (var index in indices) {
                    if (index < 0 || index >= _parameterList.arraySize) continue;
                    MakeParameterUnique(index);
                }

                serializedObject.ApplyModifiedProperties();
            };

            Toolbox.RegisterCommands(_listView,
                command => _listView.selectedIndex >= 0 && _listView.selectedIndex < _parameterList.arraySize &&
                           (command == Toolbox.EventCommands.Duplicate ||
                            command == Toolbox.EventCommands.Delete ||
                            command == Toolbox.EventCommands.SoftDelete),
                command => {
                    if (command == Toolbox.EventCommands.Duplicate) DuplicateParameter(_listView.selectedIndex);
                    else DeleteParameter(_listView.selectedIndex);
                });

            _listView.BindProperty(_parameterList);
            return _listView;
        }

        private VisualElement CreateCleanupBox() {
            _cleanupBox = new VisualElement();
            _cleanupBox.AddToClassList("sdkp-section");
            _cleanupBox.AddToClassList("sdkp-row");
            _cleanupLabel = new Label("Cleanup Invalid, Blank, and Duplicate Parameters");
            _cleanupLabel.AddToClassList("sdkp-grow");
            _cleanupBox.Add(_cleanupLabel);
            _cleanupBox.Add(new Button(CleanupParameters) { text = "Cleanup" });
            return _cleanupBox;
        }

        private VisualElement CreateMergeField() {
            var box = new VisualElement();
            box.AddToClassList("sdkp-section");
            var mergeField = new ObjectField("Merge Parameters") {
                objectType = typeof(VRCExpressionParameters),
                allowSceneObjects = false,
                tooltip = "Drop another Expression Parameters asset here to add its parameters to this one"
            }.Aligned();
            mergeField.RegisterValueChangedCallback(evt => {
                if (evt.newValue is VRCExpressionParameters other) MergeParameters(other);
                mergeField.SetValueWithoutNotify(null);
            });
            box.Add(mergeField);
            return box;
        }

        #endregion

        #region Refresh

        private void OnAvatarInfoChanged() {
            if (!IsAlive()) return;
            if (_avatarSelector != null && _avatarSelector.value != VRCSDKPlus.GetAvatar())
                _avatarSelector.SetValueWithoutNotify(VRCSDKPlus.GetAvatar());
            OnDataChanged();
        }

        private void OnDataChanged() {
            if (!IsAlive()) return;
            if (ParameterUtility.NormalizeBitWidths(_parameterList))
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            RefreshAllParameterStatus();
            RefreshMemory();
            RefreshRows();
            _cleanupBox?.SetDisplayed(_parameterStatus.Any(s => s.HasWarning));
        }

        private void RefreshRows() {
            _listView?.Query<ParameterRow>().ForEach(row => row.Refresh());
            _header?.schedule.Execute(AlignHeader);
        }

        private void RefreshMemory() {
            if (_memoryBar == null) return;
            int max = ParameterUtility.MaxMemoryCost;
            int cost = ParameterUtility.GetTotalCost(_parameterList);
            _memoryBar.value = Mathf.Min(cost, max);
            _memoryBar.title = $"{cost} / {max} Synced Bits";
            _memoryBar.EnableInClassList("sdkp-memory-bar--over", cost > max);
            _memoryBar.EnableInClassList("sdkp-memory-bar--low", cost <= max && cost > max / 2);
            _memoryBar.EnableInClassList("sdkp-memory-bar--ok", cost <= max / 2);
        }

        /// <summary>Called by rows after they edit a parameter so warnings, memory and the other rows update right away.</summary>
        internal void NotifyChanged() {
            _refreshScheduled?.Pause();
            _refreshScheduled = _root?.schedule.Execute(OnDataChanged);
        }

        private IVisualElementScheduledItem _refreshScheduled;

        internal ParameterStatus GetStatus(int index) =>
            index >= 0 && index < _parameterStatus.Length ? _parameterStatus[index] : default;

        private void RefreshAllParameterStatus() {
            if (_parameterList == null) return;
            bool hasAvatar = VRCSDKPlus.GetAvatar();
            var validParameters = VRCSDKPlus.GetValidParameters();
            var names = new string[_parameterList.arraySize];
            for (int i = 0; i < names.Length; i++)
                names[i] = _parameterList.GetArrayElementAtIndex(i).FindPropertyRelative(ParameterUtility.NameField)
                    .stringValue;

            var nameCounts = names.Where(n => !string.IsNullOrEmpty(n)).GroupBy(n => n)
                .ToDictionary(g => g.Key, g => g.Count());

            _parameterStatus = new ParameterStatus[names.Length];
            for (int index = 0; index < names.Length; index++) {
                var name = names[index];
                var matchedParameter = validParameters.FirstOrDefault(p => p.name == name);
                bool parameterEmpty = string.IsNullOrEmpty(name);
                bool parameterIsValid = matchedParameter != null;
                bool parameterIsDuplicate = !parameterEmpty && nameCounts[name] > 1;
                _parameterStatus[index] = new ParameterStatus {
                    ParameterEmpty = parameterEmpty,
                    ParameterAddable = hasAvatar && !parameterIsValid && !parameterEmpty,
                    ParameterIsDuplicate = parameterIsDuplicate,
                    HasWarning = (hasAvatar && !parameterIsValid) || parameterEmpty || parameterIsDuplicate,
                    MatchedParameter = matchedParameter
                };
            }
        }

        #endregion

        #region Parameter actions

        internal void DuplicateParameter(int index) {
            serializedObject.Update();
            if (index < 0 || index >= _parameterList.arraySize) return;
            _parameterList.InsertArrayElementAtIndex(index);
            MakeParameterUnique(index + 1);
            serializedObject.ApplyModifiedProperties();
            _listView?.SetSelection(index + 1);
        }

        internal void DeleteParameter(int index) {
            serializedObject.Update();
            if (index < 0 || index >= _parameterList.arraySize) return;
            _parameterList.DeleteArrayElementAtIndex(index);
            serializedObject.ApplyModifiedProperties();
            if (_listView != null) {
                if (_parameterList.arraySize == 0) _listView.ClearSelection();
                else _listView.SetSelection(Mathf.Min(index, _parameterList.arraySize - 1));
            }
        }

        private void MakeParameterUnique(int index) {
            var newElement = _parameterList.GetArrayElementAtIndex(index);
            var nameProp = newElement.FindPropertyRelative(ParameterUtility.NameField);
            nameProp.stringValue = Toolbox.GenerateUniqueString(nameProp.stringValue, newName => {
                for (int i = 0; i < _parameterList.arraySize; i++) {
                    if (i == index) continue;
                    var p = _parameterList.GetArrayElementAtIndex(i);
                    if (p.FindPropertyRelative(ParameterUtility.NameField).stringValue == newName) return false;
                }

                return true;
            });
        }

        private void CleanupParameters() {
            VRCSDKPlus.RefreshAvatarInfo();
            serializedObject.Update();

            bool hasAvatar = VRCSDKPlus.GetAvatar();
            var validNames = new HashSet<string>(VRCSDKPlus.GetValidParameters().Select(p => p.name));
            var seen = new HashSet<string>();
            var toDelete = new List<(int index, string reason)>();

            for (int i = 0; i < _parameterList.arraySize; i++) {
                var name = _parameterList.GetArrayElementAtIndex(i).FindPropertyRelative(ParameterUtility.NameField)
                    .stringValue;
                if (string.IsNullOrEmpty(name)) toDelete.Add((i, $"blank parameter at index {i}"));
                else if (!seen.Add(name)) toDelete.Add((i, $"duplicate parameter {name}"));
                else if (hasAvatar && !validNames.Contains(name)) toDelete.Add((i, $"invalid parameter {name}"));
            }

            if (toDelete.Count == 0) {
                Helpers.GreenLog("Nothing to clean up!");
                return;
            }

            var summary = string.Join("\n", toDelete.Take(15).Select(d => "- " + d.reason));
            if (toDelete.Count > 15) summary += $"\n... and {toDelete.Count - 15} more";
            if (!EditorUtility.DisplayDialog("VRCSDK+ Cleanup",
                    $"Delete {toDelete.Count} parameter(s)?\n\n{summary}", "Delete", "Cancel"))
                return;

            foreach (var (index, reason) in toDelete.OrderByDescending(d => d.index)) {
                _parameterList.DeleteArrayElementAtIndex(index);
                Helpers.GreenLog($"Deleted {reason}");
            }

            serializedObject.ApplyModifiedProperties();
            Helpers.GreenLog("Finished Cleanup!");
        }

        private void MergeParameters(VRCExpressionParameters other) {
            var myParams = (VRCExpressionParameters)target;
            if (other == myParams || other.parameters == null) return;

            serializedObject.ApplyModifiedProperties();
            var existing = new HashSet<string>(myParams.parameters.Select(p => p.name));
            var added = other.parameters.Where(p => !existing.Contains(p.name)).Select(ParameterUtility.Clone)
                .ToArray();
            int skipped = other.parameters.Length - added.Length;

            Undo.RecordObject(myParams, "Merge Parameters");
            myParams.parameters = myParams.parameters.Concat(added).ToArray();
            EditorUtility.SetDirty(myParams);
            serializedObject.Update();

            Helpers.GreenLog($"Merged {added.Length} parameter(s) from {other.name}" +
                             (skipped > 0 ? $", skipped {skipped} that already exist" : string.Empty));
        }

        #endregion

        #region Automated Methods

        [MenuItem("CONTEXT/VRCExpressionParameters/[SDK+] Toggle Editor", false, 899)]
        private static void ToggleEditor() {
            _editorActive = !_editorActive;

            var targetType = Helpers.ExtendedGetType("VRCExpressionParameters");
            if (targetType == null) {
                Debug.LogError("[VRCSDK+] VRCExpressionParameters was not found! Could not apply custom editor.");
                return;
            }

            if (_editorActive) AutomatedMethods.OverrideEditor(targetType, typeof(VRCParamsPlus));
            else {
                var expressionsEditor = Helpers.ExtendedGetType("VRCExpressionParametersEditor");
                if (expressionsEditor == null) {
                    Debug.LogWarning(
                        "[VRCSDK+] VRCExpressionParametersEditor was not found! Could not apply custom editor");
                    return;
                }

                AutomatedMethods.OverrideEditor(targetType, expressionsEditor);
            }
        }

        #endregion

        internal struct ParameterStatus {
            internal bool ParameterEmpty;
            internal bool ParameterAddable;
            internal bool ParameterIsDuplicate;
            internal bool HasWarning;
            internal AnimatorControllerParameter MatchedParameter;

            internal string WarningMessage => ParameterEmpty ? "Blank Parameter" :
                ParameterIsDuplicate ? "Duplicate Parameter! May cause issues!" :
                "Parameter not found in any playable controller of Active Avatar";
        }
    }
}
