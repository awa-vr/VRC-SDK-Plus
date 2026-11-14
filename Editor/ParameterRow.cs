using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;
using AnimatorControllerParameter = UnityEngine.AnimatorControllerParameter;
using AnimatorControllerParameterType = UnityEngine.AnimatorControllerParameterType;
using ValueType = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.ValueType;

namespace AwAVR.VRCSDKPlus {
    /// <summary>One expression parameter in the parameters inspector's list, with its optional advanced options row.</summary>
    internal sealed class ParameterRow : VisualElement {
        private const string LerpDefault = "Default";
        private const string LerpCustom = "Custom";

        private readonly VRCParamsPlus _owner;
        private int _index = -1;

        private readonly VisualElement _main;
        private readonly TextField _nameField;
        private readonly Label _typeHint;
        private readonly Button _dropdownButton;
        private readonly Button _addButton;
        private readonly VisualElement _statusContainer;
        private readonly VisualElement _warnIcon;
        private readonly Button _renameButton;
        private readonly EnumField _typeField;
        private readonly VisualElement _defaultBoolContainer;
        private readonly Toggle _defaultBoolField;
        private readonly IntegerField _defaultIntField;
        private readonly FloatField _defaultFloatField;
        private readonly Toggle _savedField;
        private readonly Toggle _syncedField;

        private readonly VisualElement _advanced;
        private readonly DropdownField _lerpTypeField;
        private readonly FloatField _lerpDurationField;
        private readonly IntegerField _bitWidthField;
        private readonly EnumField _representationField;
        private readonly Label _infoLabel;

        // Columns the parameters inspector lines its header up with.
        internal VisualElement NameColumn => _nameField;
        internal VisualElement TypeColumn => _typeField;
        internal VisualElement DefaultColumn => _defaultBoolContainer.resolvedStyle.display != DisplayStyle.None ? _defaultBoolContainer
            : _defaultIntField.resolvedStyle.display != DisplayStyle.None ? (VisualElement)_defaultIntField : _defaultFloatField;
        internal VisualElement SavedColumn => _savedField.parent;
        internal VisualElement SyncedColumn => _syncedField?.parent;

        internal ParameterRow(VRCParamsPlus owner) {
            _owner = owner;
            AddToClassList("sdkp-param-item");

            _main = new VisualElement();
            _main.AddToClassList("sdkp-param-main");
            Add(_main);

            _nameField = new TextField { isDelayed = false };
            _nameField.AddToClassList("sdkp-col-name");
            _typeHint = Toolbox.AddHint(_nameField, string.Empty, true);
            _main.Add(_nameField);

            _dropdownButton = new Button(ShowAnimatorParameterDropdown) {
                text = "▾",
                tooltip = "Pick a parameter from the avatar's animators"
            };
            _dropdownButton.AddToClassList("sdkp-dropdown-button");
            _main.Add(_dropdownButton);

            _addButton = new Button(ShowAddToPlayableMenu) {
                text = "Add",
                tooltip = "Add this parameter to one of the avatar's playable layers"
            };
            _addButton.AddToClassList("sdkp-small-button");
            _main.Add(_addButton);

            _statusContainer = new VisualElement();
            _statusContainer.AddToClassList("sdkp-col-status");
            _warnIcon = Toolbox.Icon(Toolbox.Strings.IconWarn);
            _statusContainer.Add(_warnIcon);
            _renameButton = Toolbox.IconButton(Toolbox.Strings.IconEdit, "Rename this parameter everywhere (Parameter Renamer)",
                OpenRenamer);
            _statusContainer.Add(_renameButton);
            _main.Add(_statusContainer);

            _typeField = new EnumField(ValueType.Int);
            _typeField.AddToClassList("sdkp-col-type");
            _typeField.RegisterValueChangedCallback(evt => {
                evt.StopPropagation();
                if (!(evt.newValue is ValueType newType) || !TryGetProperty(out var prop)) return;
                prop.FindPropertyRelative(ParameterUtility.ValueTypeField).intValue = (int)newType;
                ParameterUtility.ApplyTypeDefaults(prop);
                Apply(prop);
            });
            _main.Add(_typeField);

            _defaultBoolContainer = new VisualElement();
            _defaultBoolContainer.AddToClassList("sdkp-col-default");
            _defaultBoolContainer.style.alignItems = Align.Center;
            _defaultBoolField = new Toggle();
            _defaultBoolField.RegisterValueChangedCallback(evt => {
                evt.StopPropagation();
                SetDefaultValue(evt.newValue ? 1 : 0);
            });
            _defaultBoolContainer.Add(_defaultBoolField);
            _main.Add(_defaultBoolContainer);

            _defaultIntField = new IntegerField();
            _defaultIntField.AddToClassList("sdkp-col-default");
            _defaultIntField.RegisterValueChangedCallback(evt => {
                evt.StopPropagation();
                SetDefaultValue(evt.newValue);
            });
            _main.Add(_defaultIntField);

            _defaultFloatField = new FloatField();
            _defaultFloatField.AddToClassList("sdkp-col-default");
            _defaultFloatField.RegisterValueChangedCallback(evt => {
                evt.StopPropagation();
                SetDefaultValue(evt.newValue);
            });
            _main.Add(_defaultFloatField);

            _savedField = CreateToggleColumn(_main);
            _syncedField = ParameterUtility.HasSyncingOption ? CreateToggleColumn(_main) : null;
            // These are bound directly, refresh warnings/memory once the binding has written the new value.
            _nameField.RegisterValueChangedCallback(_ => _owner.NotifyChanged());
            _syncedField?.RegisterValueChangedCallback(_ => _owner.NotifyChanged());

            var remove = Toolbox.IconButton(Toolbox.Strings.IconRemove, "Remove parameter from list",
                () => _owner.DeleteParameter(_index));
            remove.AddToClassList("sdkp-col-remove");
            _main.Add(remove);

            #region Advanced options (VRCSDK 3.10.6+)

            // Two fixed lines that never wrap, so every row has the same height (see VRCParamsPlus.RowHeight).
            _advanced = new VisualElement();
            _advanced.AddToClassList("sdkp-param-advanced");
            Add(_advanced);
            var firstLine = AdvancedLine();
            var secondLine = AdvancedLine();

            if (ParameterUtility.HasLerpOptions) {
                firstLine.Add(AdvancedLabel("Lerp Duration",
                    "How long remote users take to smoothly move to a new value of this parameter. Bools never lerp.", true));
                _lerpTypeField = new DropdownField(new List<string> { LerpDefault, LerpCustom }, 0);
                _lerpTypeField.AddToClassList("sdkp-adv-field");
                _lerpTypeField.style.width = 76;
                _lerpTypeField.RegisterValueChangedCallback(evt => {
                    evt.StopPropagation();
                    if (!TryGetProperty(out var prop)) return;
                    prop.FindPropertyRelative(ParameterUtility.UseCustomLerpDurationField).boolValue =
                        evt.newValue == LerpCustom;
                    Apply(prop);
                });
                firstLine.Add(_lerpTypeField);

                _lerpDurationField = new FloatField { isDelayed = true, tooltip = "Seconds" };
                _lerpDurationField.AddToClassList("sdkp-adv-field");
                _lerpDurationField.style.width = 48;
                _lerpDurationField.RegisterValueChangedCallback(evt => {
                    evt.StopPropagation();
                    if (!TryGetProperty(out var prop)) return;
                    var value = Mathf.Clamp(evt.newValue, ParameterUtility.MinLerpDuration,
                        ParameterUtility.MaxLerpDuration);
                    prop.FindPropertyRelative(ParameterUtility.CustomLerpDurationField).floatValue = value;
                    Apply(prop);
                    _lerpDurationField.SetValueWithoutNotify(value);
                });
                firstLine.Add(_lerpDurationField);
            }

            var bitsLabel = AdvancedLabel("Bits",
                $"How many bits this parameter uses when synced ({ParameterUtility.MinBitWidth}-{ParameterUtility.MaxBitWidth}). Fewer bits use less memory but lower the range or precision.",
                false);
            secondLine.Add(bitsLabel);
            _bitWidthField = new IntegerField { isDelayed = true };
            _bitWidthField.AddToClassList("sdkp-adv-field");
            _bitWidthField.style.width = 36;
            _bitWidthField.RegisterValueChangedCallback(evt => {
                evt.StopPropagation();
                if (!TryGetProperty(out var prop)) return;
                var bits = Mathf.Clamp(evt.newValue, ParameterUtility.MinBitWidth, ParameterUtility.MaxBitWidth);
                prop.FindPropertyRelative(ParameterUtility.BitWidthField).intValue = bits;
                ClampDefault(prop);
                Apply(prop);
                _bitWidthField.SetValueWithoutNotify(bits);
            });
            secondLine.Add(_bitWidthField);

            var representationLabel = AdvancedLabel("Representation",
                "Signed values can be negative. Floats are signed by default and ints unsigned.", false);
            representationLabel.style.marginLeft = 10;
            secondLine.Add(representationLabel);
            _representationField = new EnumField(ParameterUtility.Representation.Unsigned);
            _representationField.AddToClassList("sdkp-adv-field");
            _representationField.style.width = 76;
            _representationField.RegisterValueChangedCallback(evt => {
                evt.StopPropagation();
                if (!(evt.newValue is ParameterUtility.Representation representation) ||
                    !TryGetProperty(out var prop)) return;
                var flipped = prop.FindPropertyRelative(ParameterUtility.FlippedRepresentationField);
                if (flipped == null) return;
                flipped.boolValue = ParameterUtility.IsFlipped(ParameterUtility.GetValueType(prop), representation);
                ClampDefault(prop);
                Apply(prop);
            });
            secondLine.Add(_representationField);

            _infoLabel = new Label();
            _infoLabel.AddToClassList("sdkp-adv-info");
            secondLine.Add(_infoLabel);

            #endregion

            this.AddManipulator(new ContextualMenuManipulator(evt => {
                evt.menu.AppendAction("Duplicate", _ => _owner.DuplicateParameter(_index));
                evt.menu.AppendSeparator();
                evt.menu.AppendAction("Delete", _ => _owner.DeleteParameter(_index));
            }));

            // The "(Float)" hint would cover the name in a narrow inspector.
            _nameField.RegisterCallback<GeometryChangedEvent>(_ =>
                _typeHint.style.visibility = _nameField.layout.width < 130 ? Visibility.Hidden : Visibility.Visible);
        }

        private VisualElement AdvancedLine() {
            var line = new VisualElement();
            line.AddToClassList("sdkp-adv-line");
            _advanced.Add(line);
            return line;
        }

        private static Label AdvancedLabel(string text, string tooltip, bool fixedWidth) {
            var label = new Label(text) { tooltip = tooltip };
            label.AddToClassList("sdkp-adv-label");
            if (fixedWidth) label.AddToClassList("sdkp-adv-label--fixed");
            return label;
        }

        private static Toggle CreateToggleColumn(VisualElement parent) {
            var container = new VisualElement();
            container.AddToClassList("sdkp-col-toggle");
            var toggle = new Toggle();
            toggle.style.marginLeft = 0;
            toggle.style.marginRight = 0;
            container.Add(toggle);
            parent.Add(container);
            return toggle;
        }

        internal void Bind(int index) {
            _index = index;
            if (!TryGetProperty(out var prop)) {
                Unbind();
                return;
            }

            _nameField.BindProperty(prop.FindPropertyRelative(ParameterUtility.NameField));
            _savedField.BindProperty(prop.FindPropertyRelative(ParameterUtility.SavedField));
            _syncedField?.BindProperty(prop.FindPropertyRelative(ParameterUtility.SyncedField));
            Refresh();
        }

        internal void Unbind() {
            _nameField.Unbind();
            _savedField.Unbind();
            _syncedField?.Unbind();
            _index = -1;
        }

        /// <summary>The parameter this row shows, fetched fresh so it is never a stale element after a reorder or delete.</summary>
        private bool TryGetProperty(out SerializedProperty property) {
            property = null;
            var list = _owner.ParameterList;
            if (list == null || _index < 0) return false;
            try {
                if (_index >= list.arraySize) return false;
                property = list.GetArrayElementAtIndex(_index);
                return property != null;
            }
            catch (Exception) {
                // The SerializedObject was disposed (the inspector is closing).
                return false;
            }
        }

        /// <summary>Updates everything that isn't bound directly: default value, type, warnings, advanced options.</summary>
        internal void Refresh() {
            if (!TryGetProperty(out var prop)) return;

            var status = _owner.GetStatus(_index);
            var type = ParameterUtility.GetValueType(prop);
            var name = prop.FindPropertyRelative(ParameterUtility.NameField).stringValue;
            var defaultValue = prop.FindPropertyRelative(ParameterUtility.DefaultValueField).floatValue;

            _typeField.SetValueWithoutNotify(type);
            _typeHint.text = status.MatchedParameter != null ? $"({status.MatchedParameter.type})" : "(?)";

            _defaultBoolContainer.SetDisplayed(type == ValueType.Bool);
            _defaultIntField.SetDisplayed(type == ValueType.Int);
            _defaultFloatField.SetDisplayed(type == ValueType.Float);
            _defaultBoolField.SetValueWithoutNotify(defaultValue != 0);
            if (_defaultIntField.focusController?.focusedElement != _defaultIntField)
                _defaultIntField.SetValueWithoutNotify(Mathf.RoundToInt(defaultValue));
            if (_defaultFloatField.focusController?.focusedElement != _defaultFloatField)
                _defaultFloatField.SetValueWithoutNotify(defaultValue);

            _dropdownButton.SetEnabled(VRCSDKPlus.GetValidParameters().Length > 0);
            _addButton.SetDisplayed(status.ParameterAddable && VRCSDKPlus.GetValidPlayables().Length > 0);
            _warnIcon.SetDisplayed(status.HasWarning);
            _warnIcon.tooltip = status.WarningMessage;
#if PARAMETER_RENAMER_INSTALLED
            _renameButton.SetDisplayed(!status.HasWarning && VRCSDKPlus.GetAvatar());
#else
            _renameButton.SetDisplayed(false);
#endif

            EnableInClassList("sdkp-dimmed", !Toolbox.SearchMatches(name, _owner.SearchValue));

            RefreshAdvanced(prop, type);
        }

        private void RefreshAdvanced(SerializedProperty prop, ValueType type) {
            _advanced.SetDisplayed(_owner.ShowAdvanced);
            if (!_owner.ShowAdvanced) return;

            bool isBool = type == ValueType.Bool;

            if (_lerpTypeField != null) {
                var useCustom = prop.FindPropertyRelative(ParameterUtility.UseCustomLerpDurationField).boolValue;
                _lerpTypeField.SetEnabled(!isBool);
                _lerpTypeField.SetValueWithoutNotify(useCustom && !isBool ? LerpCustom : LerpDefault);
                _lerpDurationField.style.visibility = useCustom && !isBool ? Visibility.Visible : Visibility.Hidden;
                _lerpDurationField.SetValueWithoutNotify(prop
                    .FindPropertyRelative(ParameterUtility.CustomLerpDurationField).floatValue);
            }

            _bitWidthField.SetEnabled(!isBool);
            _bitWidthField.SetValueWithoutNotify(ParameterUtility.GetBitWidth(prop));
            _representationField.SetEnabled(!isBool);
            _representationField.SetValueWithoutNotify(ParameterUtility.GetRepresentation(prop));
            _infoLabel.text = ParameterUtility.GetInfo(prop);
            _infoLabel.tooltip = _infoLabel.text;
        }

        private void SetDefaultValue(float value) {
            if (!TryGetProperty(out var prop)) return;
            var clamped = ParameterUtility.ClampDefault(prop, value);
            prop.FindPropertyRelative(ParameterUtility.DefaultValueField).floatValue = clamped;
            Apply(prop);
            if (!Mathf.Approximately(clamped, value)) Refresh();
        }

        private void Apply(SerializedProperty prop) {
            prop.serializedObject.ApplyModifiedProperties();
            _owner.NotifyChanged();
        }

        private static void ClampDefault(SerializedProperty prop) {
            var defaultValue = prop.FindPropertyRelative(ParameterUtility.DefaultValueField);
            defaultValue.floatValue = ParameterUtility.ClampDefault(prop, defaultValue.floatValue);
        }

        private void ShowAnimatorParameterDropdown() {
            if (!TryGetProperty(out var prop)) return;
            var list = _owner.ParameterList;
            var used = new HashSet<string>();
            for (int i = 0; i < list.arraySize; i++)
                used.Add(list.GetArrayElementAtIndex(i).FindPropertyRelative(ParameterUtility.NameField).stringValue);

            var available = VRCSDKPlus.GetValidParameters().Where(p => !used.Contains(p.name)).ToArray();
            if (available.Length == 0) {
                Helpers.GreenLog("All of the avatar's animator parameters are already in this list");
                return;
            }

            var index = _index;
            new SearchableDropdown<AnimatorControllerParameter>("Animator Parameters",
                available.Select(p => ($"{p.name}  ({p.type})", p)), parameter => {
                    var list2 = _owner.ParameterList;
                    if (index < 0 || index >= list2.arraySize) return;
                    var target = list2.GetArrayElementAtIndex(index);
                    target.FindPropertyRelative(ParameterUtility.NameField).stringValue = parameter.name;
                    target.serializedObject.ApplyModifiedProperties();
                    _owner.NotifyChanged();
                }).ShowUnder(_dropdownButton);
        }

        private void ShowAddToPlayableMenu() {
            if (!TryGetProperty(out var prop)) return;
            var avatar = VRCSDKPlus.GetAvatar();
            if (!avatar) return;

            var name = prop.FindPropertyRelative(ParameterUtility.NameField).stringValue;
            var type = ParameterUtility.GetValueType(prop);
            var defaultValue = prop.FindPropertyRelative(ParameterUtility.DefaultValueField).floatValue;

            var menu = new GenericMenu();
            var playables = VRCSDKPlus.GetValidPlayables();
            var indexes = VRCSDKPlus.GetValidPlayableIndexes();
            for (int i = 0; i < playables.Length; i++) {
                var playable = (VRCAvatarDescriptor.AnimLayerType)indexes[i];
                menu.AddItem(new GUIContent(playables[i]), false,
                    () => AddToPlayable(avatar, playable, name, type, defaultValue));
            }

            menu.DropDown(_addButton.worldBound);
        }

        private static void AddToPlayable(VRCAvatarDescriptor avatar, VRCAvatarDescriptor.AnimLayerType playable,
            string name, ValueType type, float defaultValue) {
            if (!avatar.GetPlayableLayer(playable, out var controller)) return;
            if (controller.parameters.Any(p => p.name == name)) {
                Helpers.GreenLog($"{name} already exists in the {playable} Playable Controller");
            }
            else {
                AnimatorControllerParameterType paramType;
                switch (type) {
                    case ValueType.Int:
                        paramType = AnimatorControllerParameterType.Int;
                        break;
                    case ValueType.Float:
                        paramType = AnimatorControllerParameterType.Float;
                        break;
                    default:
                        paramType = AnimatorControllerParameterType.Bool;
                        break;
                }

                Undo.RecordObject(controller, "Add parameter to controller");
                controller.AddParameter(new AnimatorControllerParameter {
                    name = name,
                    type = paramType,
                    defaultFloat = defaultValue,
                    defaultInt = (int)defaultValue,
                    defaultBool = defaultValue > 0
                });
                EditorUtility.SetDirty(controller);
                Helpers.GreenLog($"Added {paramType} {name} to {playable} Playable Controller");
            }

            VRCSDKPlus.RefreshAvatarInfo();
        }

        private void OpenRenamer() {
#if PARAMETER_RENAMER_INSTALLED
            if (!TryGetProperty(out var prop)) return;
            var name = prop.FindPropertyRelative(ParameterUtility.NameField).stringValue;
#if PARAMETER_RENAMER_1_4_0
            ParameterRenamer.Show(name, VRCSDKPlus.GetAvatar(), runAfterRenameFunction: VRCSDKPlus.RefreshAvatarInfo);
#else
            ParameterRenamer.Show(name, VRCSDKPlus.GetAvatar());
#endif
#endif
        }
    }
}
