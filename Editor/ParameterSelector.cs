using System;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.ScriptableObjects;
using ValueType = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.ValueType;

namespace AwAVR.VRCSDKPlus {
    /// <summary>
    /// A menu control's parameter: name field with a dropdown of the avatar's expression parameters, a warning when it
    /// is missing, a button to add it to the expression parameters and, for the main parameter, its value.
    /// </summary>
    internal sealed class ParameterSelector : VisualElement {
        private readonly SerializedProperty _parameter;
        private readonly SerializedProperty _value;
        private readonly Func<VRCExpressionParameters> _getParameters;
        private readonly ValueType? _filter;
        private readonly bool _required;

        private readonly TextField _nameField;
        private readonly Label _placeholder;
        private readonly Button _dropdownButton;
        private readonly Button _addButton;
        private readonly VisualElement _warnIcon;
        private readonly Button _renameButton;
        private readonly IntegerField _intValueField;
        private readonly FloatField _floatValueField;
        private readonly TextField _boolValueField;

        /// <param name="label">Shown in front of the field, empty for compact layouts.</param>
        /// <param name="parameter">A <c>VRCExpressionsMenu.Control.Parameter</c> property.</param>
        /// <param name="filter">Only list (and add) parameters of this type.</param>
        /// <param name="required">Warn when the parameter is left blank.</param>
        /// <param name="value">The control's value, shown next to the parameter when given.</param>
        internal ParameterSelector(string label, SerializedProperty parameter, Func<VRCExpressionParameters> getParameters,
            ValueType? filter, bool required, SerializedProperty value = null) {
            _parameter = parameter;
            _value = value;
            _getParameters = getParameters;
            _filter = filter;
            _required = required;

            AddToClassList("sdkp-row");
            AddToClassList("sdkp-parameter-selector");

            _nameField = new TextField(string.IsNullOrEmpty(label) ? null : label);
            if (!string.IsNullOrEmpty(label)) _nameField.Aligned();
            _nameField.AddToClassList("sdkp-grow");
            _nameField.BindProperty(parameter.FindPropertyRelative("name"));
            _placeholder = Toolbox.AddHint(_nameField, "Parameter", false);
            _nameField.RegisterValueChangedCallback(_ => schedule.Execute(Refresh));
            Add(_nameField);

            _dropdownButton = new Button(ShowDropdown) { text = "▾", tooltip = "Pick an expression parameter" };
            _dropdownButton.AddToClassList("sdkp-dropdown-button");
            Add(_dropdownButton);

            _addButton = new Button(AddToExpressionParameters) {
                text = "Add",
                tooltip = "Add this parameter to the avatar's Expression Parameters"
            };
            _addButton.AddToClassList("sdkp-small-button");
            Add(_addButton);

            var status = new VisualElement();
            status.AddToClassList("sdkp-col-status");
            _warnIcon = Toolbox.Icon(Toolbox.Strings.IconWarn);
            status.Add(_warnIcon);
            _renameButton = Toolbox.IconButton(Toolbox.Strings.IconEdit,
                "Rename this parameter everywhere (Parameter Renamer)", OpenRenamer);
            status.Add(_renameButton);
            Add(status);

            if (value != null) {
                _intValueField = new IntegerField { tooltip = "Value the parameter is set to (0-255)" };
                _floatValueField = new FloatField { tooltip = "Value the parameter is set to" };
                _boolValueField = new TextField { tooltip = "Bools are set to true", value = "True" };
                _boolValueField.SetEnabled(false);
                foreach (var field in new VisualElement[] { _intValueField, _floatValueField, _boolValueField }) {
                    field.style.width = 50;
                    field.style.flexShrink = 0;
                    Add(field);
                }

                _intValueField.RegisterValueChangedCallback(evt => SetValue(Mathf.Clamp(evt.newValue, 0, 255)));
                _floatValueField.RegisterValueChangedCallback(evt => {
                    var param = FindParameter(CurrentName, out _);
                    SetValue(param != null ? Mathf.Clamp(evt.newValue, -1, 1) : Mathf.Clamp(evt.newValue, -1, 255));
                });
            }

            Refresh();
        }

        private string CurrentName => _parameter.FindPropertyRelative("name").stringValue;

        private VRCExpressionParameters.Parameter[] GetCandidates(VRCExpressionParameters parameters) {
            if (!parameters || parameters.parameters == null) return Array.Empty<VRCExpressionParameters.Parameter>();
            return parameters.parameters.Where(p => !string.IsNullOrEmpty(p.name) &&
                                                    (_filter == null || p.valueType == _filter.Value)).ToArray();
        }

        private VRCExpressionParameters.Parameter FindParameter(string name, out VRCExpressionParameters parameters) {
            parameters = _getParameters();
            if (string.IsNullOrEmpty(name)) return null;
            return GetCandidates(parameters).FirstOrDefault(p => p.name == name);
        }

        internal void Refresh() {
            string name;
            try {
                name = CurrentName;
            }
            catch (Exception) {
                return; // property disposed, the control editor is being rebuilt
            }

            var matched = FindParameter(name, out var parameters);
            bool hasParameters = parameters;
            bool isEmpty = string.IsNullOrEmpty(name);
            bool isMissing = !hasParameters || (!isEmpty && matched == null);
            bool warn = (!isEmpty && isMissing) || (_required && isEmpty) || !hasParameters;

            _placeholder.SetDisplayed(isEmpty);
            _dropdownButton.SetEnabled(hasParameters && GetCandidates(parameters).Length > 0);
            _addButton.SetDisplayed(hasParameters && !isEmpty && matched == null);
            _warnIcon.SetDisplayed(warn);
            _warnIcon.tooltip = !hasParameters ? Toolbox.Strings.MissingParametersTooltip :
                isEmpty ? "Parameter is blank. Control may be dysfunctional." :
                _filter != null ? $"Parameter ({name}) not found or not a {_filter.Value}" :
                $"Parameter ({name}) not found on the active avatar descriptor";
#if PARAMETER_RENAMER_INSTALLED
            _renameButton.SetDisplayed(!warn && !isEmpty && VRCSDKPlus.GetAvatar());
#else
            _renameButton.SetDisplayed(false);
#endif

            if (_value == null) return;
            var type = matched?.valueType;
            _intValueField.SetDisplayed(type == ValueType.Int);
            _floatValueField.SetDisplayed(type == ValueType.Float || type == null);
            _boolValueField.SetDisplayed(type == ValueType.Bool);

            if (type == ValueType.Bool && !Mathf.Approximately(_value.floatValue, 1)) {
                // Bool parameters are always set to true by a control.
                _value.floatValue = 1;
                _value.serializedObject.ApplyModifiedProperties();
            }

            _intValueField.SetValueWithoutNotify(Mathf.RoundToInt(_value.floatValue));
            _floatValueField.SetValueWithoutNotify(_value.floatValue);
        }

        private void SetValue(float value) {
            _value.floatValue = value;
            _value.serializedObject.ApplyModifiedProperties();
            Refresh();
        }

        private void ShowDropdown() {
            var parameters = _getParameters();
            var candidates = GetCandidates(parameters);
            var nameProperty = _parameter.FindPropertyRelative("name");
            var items = new[] { ("[None]", string.Empty) }
                .Concat(candidates.Select(p => ($"{p.name}  [{p.valueType.ToString().ToLower()}]", p.name)));
            new SearchableDropdown<string>("Expression Parameters", items, selected => {
                nameProperty.stringValue = selected;
                nameProperty.serializedObject.ApplyModifiedProperties();
                Refresh();
            }).ShowUnder(_dropdownButton);
        }

        private void AddToExpressionParameters() {
            if (_filter != null) {
                AddParameter(_filter.Value);
                return;
            }

            var menu = new GenericMenu();
            foreach (ValueType type in Enum.GetValues(typeof(ValueType)))
                menu.AddItem(new GUIContent(type.ToString()), false, () => AddParameter(type));
            menu.DropDown(_addButton.worldBound);
        }

        private void AddParameter(ValueType type) {
            var parameters = _getParameters();
            var name = CurrentName;
            if (!parameters || string.IsNullOrEmpty(name)) return;

            var so = new SerializedObject(parameters);
            var list = so.FindProperty("parameters");
            list.arraySize++;
            ParameterUtility.InitializeNewParameter(list.GetArrayElementAtIndex(list.arraySize - 1), name, type);
            so.ApplyModifiedProperties();
            Helpers.GreenLog($"Added {type} {name} to {parameters.name}");
            Refresh();
        }

        private void OpenRenamer() {
#if PARAMETER_RENAMER_INSTALLED
#if PARAMETER_RENAMER_1_4_0
            ParameterRenamer.Show(CurrentName, VRCSDKPlus.GetAvatar(), runAfterRenameFunction: VRCSDKPlus.RefreshAvatarInfo);
#else
            ParameterRenamer.Show(CurrentName, VRCSDKPlus.GetAvatar());
#endif
#endif
        }
    }
}
