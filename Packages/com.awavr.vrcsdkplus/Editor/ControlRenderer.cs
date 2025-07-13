using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace AwAVR.VRCSDKPlus
{
    static class ControlRenderer
    {
        private const float IconSize = 96;
        private const float IconSpace = IconSize + 3;

        private const float CompactIconSize = 60;
        private const float CompactIconSpace = CompactIconSize + 3;

        public static void DrawControl(SerializedProperty property, VRCExpressionParameters parameters)
        {
            MainContainer(property);
            EditorGUILayout.Separator();
            ParameterContainer(property, parameters);

            if (property != null)
            {
                EditorGUILayout.Separator();

                switch ((VRCExpressionsMenu.Control.ControlType)property.FindPropertyRelative("type").intValue)
                {
                    case VRCExpressionsMenu.Control.ControlType.RadialPuppet:
                        RadialContainer(property, parameters);
                        break;
                    case VRCExpressionsMenu.Control.ControlType.SubMenu:
                        SubMenuContainer(property);
                        break;
                    case VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet:
                        TwoAxisParametersContainer(property, parameters);
                        EditorGUILayout.Separator();
                        AxisCustomisationContainer(property);
                        break;
                    case VRCExpressionsMenu.Control.ControlType.FourAxisPuppet:
                        FourAxisParametersContainer(property, parameters);
                        EditorGUILayout.Separator();
                        AxisCustomisationContainer(property);
                        break;
                }
            }
        }

        public static void DrawControlCompact(SerializedProperty property, VRCExpressionParameters parameters)
        {
            CompactMainContainer(property, parameters);

            if (property != null)
            {
                switch ((VRCExpressionsMenu.Control.ControlType)property.FindPropertyRelative("type").intValue)
                {
                    case VRCExpressionsMenu.Control.ControlType.RadialPuppet:
                        RadialContainer(property, parameters);
                        break;
                    case VRCExpressionsMenu.Control.ControlType.SubMenu:
                        SubMenuContainer(property);
                        break;
                    case VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet:
                        // CompactTwoAxisParametersContainer(property, parameters);
                        TwoAxisParametersContainer(property, parameters);
                        EditorGUILayout.Separator();
                        AxisCustomisationContainer(property);
                        break;
                    case VRCExpressionsMenu.Control.ControlType.FourAxisPuppet:
                        // CompactFourAxisParametersContainer(property, parameters);
                        FourAxisParametersContainer(property, parameters);
                        EditorGUILayout.Separator();
                        AxisCustomisationContainer(property);
                        break;
                }
            }
        }

        #region Main container

        static void MainContainer(SerializedProperty property)
        {
            var rect = EditorGUILayout
                .GetControlRect(false, 147);
            Toolbox.Container.GUIBox(ref rect);

            var nameRect = new Rect(rect.x, rect.y, rect.width - IconSpace, 21);
            var typeRect = new Rect(rect.x, rect.y + 24, rect.width - IconSpace, 21);
            var baseStyleRect = new Rect(rect.x, rect.y + 48, rect.width - IconSpace, 21);
            var iconRect = new Rect(rect.x + rect.width - IconSize, rect.y, IconSize, IconSize);
            var helpRect = new Rect(rect.x, rect.y + IconSpace, rect.width, 42);

            DrawName(nameRect, property, true);
            DrawType(typeRect, property, true);
            DrawStyle(baseStyleRect, property, true);
            DrawIcon(iconRect, property);
            DrawHelp(helpRect, property);
        }

        static void CompactMainContainer(SerializedProperty property, VRCExpressionParameters parameters)
        {
            var rect = EditorGUILayout.GetControlRect(false, 66);
            Toolbox.Container.GUIBox(ref rect);

            var halfWidth = (rect.width - CompactIconSpace) / 2;
            var nameRect = new Rect(rect.x, rect.y, halfWidth - 3, 18);
            var typeRect = new Rect(rect.x + halfWidth, rect.y, halfWidth - 19, 18);
            var helpRect = new Rect(typeRect.x + typeRect.width + 1, rect.y, 18, 18);
            var parameterRect = new Rect(rect.x, rect.y + 21, rect.width - CompactIconSpace, 18);
            var styleRect = new Rect(rect.x, rect.y + 42, rect.width - CompactIconSize, 18);
            var iconRect = new Rect(rect.x + rect.width - CompactIconSize, rect.y, CompactIconSize,
                CompactIconSize);

            DrawName(nameRect, property, false);
            DrawType(typeRect, property, false);
            DrawStyle(styleRect, property, false);

            if (property != null)
                GUI.Label(helpRect,
                    new GUIContent(Toolbox.GUIContent.Help) { tooltip = GetHelpMessage(property) },
                    GUIStyle.none);

            ParameterContainer(property, parameters, parameterRect);

            DrawIcon(iconRect, property);

            // ToDo Draw error help if Parameter not found
        }

        static void DrawName(Rect rect, SerializedProperty property, bool drawLabel)
        {
            if (property == null)
            {
                Toolbox.Placeholder.GUI(rect);
                return;
            }

            var name = property.FindPropertyRelative("name");

            if (drawLabel)
            {
                var label = new Rect(rect.x, rect.y, 100, rect.height);
                rect = new Rect(rect.x + 103, rect.y, rect.width - 103, rect.height);

                GUI.Label(label, "Name");
            }

            name.stringValue = EditorGUI.TextField(rect, name.stringValue);
            if (string.IsNullOrEmpty(name.stringValue))
                GUI.Label(rect, "Name", Toolbox.Styles.Label.PlaceHolder);
        }

        static void DrawType(Rect rect, SerializedProperty property, bool drawLabel)
        {
            if (property == null)
            {
                Toolbox.Placeholder.GUI(rect);
                return;
            }

            if (drawLabel)
            {
                var label = new Rect(rect.x, rect.y, 100, rect.height);
                rect = new Rect(rect.x + 103, rect.y, rect.width - 103, rect.height);

                GUI.Label(label, "Type");
            }

            var controlType = property.FindPropertyRelative("type").ToControlType();
            var newType = (VRCExpressionsMenu.Control.ControlType)EditorGUI.EnumPopup(rect, controlType);

            if (newType != controlType)
                ConversionEntry(property, controlType, newType);
        }

        static void DrawStyle(Rect rect, SerializedProperty property, bool drawLabel)
        {
            const float toggleSize = 21;

            if (property == null)
            {
                Toolbox.Placeholder.GUI(rect);
                return;
            }

            if (drawLabel)
            {
                Rect labelRect = new Rect(rect.x, rect.y, 100, rect.height);
                rect = new Rect(rect.x + 103, rect.y, rect.width - 103, rect.height);
                GUI.Label(labelRect, "Style");
            }

            Rect colorRect = new Rect(rect.x, rect.y, rect.width - (toggleSize + 3) * 2, rect.height);
            Rect boldRect = new Rect(colorRect.x + colorRect.width, rect.y, toggleSize, rect.height);
            Rect italicRect = new Rect(boldRect);
            italicRect.x += italicRect.width + 3;
            boldRect.width = toggleSize;
            string rawName = property.FindPropertyRelative("name").stringValue;
            Color textColor = Color.white;

            var isBold = rawName.Contains("<b>") && rawName.Contains("</b>");
            var isItalic = rawName.Contains("<i>") && rawName.Contains("</i>");
            var m = Regex.Match(rawName, @"<color=(#[0-9|A-F]{6,8})>");
            if (m.Success)
            {
                if (rawName.Contains("</color>"))
                {
                    if (ColorUtility.TryParseHtmlString(m.Groups[1].Value, out Color newColor))
                        textColor = newColor;
                }
            }


            EditorGUI.BeginChangeCheck();
            textColor = EditorGUI.ColorField(colorRect, textColor);
            if (EditorGUI.EndChangeCheck())
            {
                rawName = Regex.Replace(rawName, @"</?color=?.*?>", string.Empty);
                rawName = $"<color=#{ColorUtility.ToHtmlStringRGB(textColor)}>{rawName}</color>";
            }

            void SetCharTag(char c, bool state)
            {
                rawName = !state ? Regex.Replace(rawName, $@"</?{c}>", string.Empty) : $"<{c}>{rawName}</{c}>";
            }

            Helpers.w_MakeRectLinkCursor(boldRect);
            EditorGUI.BeginChangeCheck();
            isBold = GUI.Toggle(boldRect, isBold, new GUIContent("<b>b</b>", "Bold"),
                Toolbox.Styles.letterButton);
            if (EditorGUI.EndChangeCheck()) SetCharTag('b', isBold);

            Helpers.w_MakeRectLinkCursor(italicRect);
            EditorGUI.BeginChangeCheck();
            isItalic = GUI.Toggle(italicRect, isItalic, new GUIContent("<i>i</i>", "Italic"),
                Toolbox.Styles.letterButton);
            if (EditorGUI.EndChangeCheck()) SetCharTag('i', isItalic);


            property.FindPropertyRelative("name").stringValue = rawName;
        }

        static void DrawIcon(Rect rect, SerializedProperty property)
        {
            if (property == null)
                Toolbox.Placeholder.GUI(rect);
            else
            {
                var value = property.FindPropertyRelative("icon");

                value.objectReferenceValue = EditorGUI.ObjectField(
                    rect,
                    string.Empty,
                    value.objectReferenceValue,
                    typeof(Texture2D),
                    false
                );
            }
        }

        static void DrawHelp(Rect rect, SerializedProperty property)
        {
            if (property == null)
            {
                Toolbox.Placeholder.GUI(rect);
                return;
            }

            string message = GetHelpMessage(property);
            EditorGUI.HelpBox(rect, message, MessageType.Info);
        }

        static string GetHelpMessage(SerializedProperty property)
        {
            switch (property.FindPropertyRelative("type").ToControlType())
            {
                case VRCExpressionsMenu.Control.ControlType.Button:
                    return
                        "Click or hold to activate. The button remains active for a minimum 0.2s.\nWhile active the (Parameter) is set to (Value).\nWhen inactive the (Parameter) is reset to zero.";
                case VRCExpressionsMenu.Control.ControlType.Toggle:
                    return
                        "Click to toggle on or off.\nWhen turned on the (Parameter) is set to (Value).\nWhen turned off the (Parameter) is reset to zero.";
                case VRCExpressionsMenu.Control.ControlType.SubMenu:
                    return
                        "Opens another expression menu.\nWhen opened the (Parameter) is set to (Value).\nWhen closed (Parameter) is reset to zero.";
                case VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet:
                    return
                        "Puppet menu that maps the joystick to two parameters (-1 to +1).\nWhen opened the (Parameter) is set to (Value).\nWhen closed (Parameter) is reset to zero.";
                case VRCExpressionsMenu.Control.ControlType.FourAxisPuppet:
                    return
                        "Puppet menu that maps the joystick to four parameters (0 to 1).\nWhen opened the (Parameter) is set to (Value).\nWhen closed (Parameter) is reset to zero.";
                case VRCExpressionsMenu.Control.ControlType.RadialPuppet:
                    return
                        "Puppet menu that sets a value based on joystick rotation. (0 to 1)\nWhen opened the (Parameter) is set to (Value).\nWhen closed (Parameter) is reset to zero.";
                default:
                    return "ERROR: Unable to load message - Invalid control type";
            }
        }

        #endregion

        #region Type Conversion

        private static void ConversionEntry(SerializedProperty property,
            VRCExpressionsMenu.Control.ControlType tOld, VRCExpressionsMenu.Control.ControlType tNew)
        {
            // Is old one button / toggle, and new one not?
            if (
                    (tOld == VRCExpressionsMenu.Control.ControlType.Button ||
                     tOld == VRCExpressionsMenu.Control.ControlType.Toggle) &&
                    (tNew != VRCExpressionsMenu.Control.ControlType.Button &&
                     tNew != VRCExpressionsMenu.Control.ControlType.Toggle)
                )
                // Reset parameter
                property.FindPropertyRelative("parameter").FindPropertyRelative("name").stringValue = "";
            else if (
                (tOld != VRCExpressionsMenu.Control.ControlType.Button &&
                 tOld != VRCExpressionsMenu.Control.ControlType.Toggle) &&
                (tNew == VRCExpressionsMenu.Control.ControlType.Button ||
                 tNew == VRCExpressionsMenu.Control.ControlType.Toggle)
            )
                SetupSubParameters(property, tNew);

            // Is either a submenu
            if (tOld == VRCExpressionsMenu.Control.ControlType.SubMenu ||
                tNew == VRCExpressionsMenu.Control.ControlType.SubMenu)
                SetupSubParameters(property, tNew);

            // Is either Puppet)
            if (IsPuppetConversion(tOld, tNew))
                DoPuppetConversion(property, tNew);
            else if (
                tNew == VRCExpressionsMenu.Control.ControlType.RadialPuppet ||
                tNew == VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet ||
                tNew == VRCExpressionsMenu.Control.ControlType.FourAxisPuppet
            )
                SetupSubParameters(property, tNew);

            property.FindPropertyRelative("type").enumValueIndex = tNew.GetEnumValueIndex();
        }

        private static bool IsPuppetConversion(VRCExpressionsMenu.Control.ControlType tOld,
            VRCExpressionsMenu.Control.ControlType tNew)
        {
            return (
                       tOld == VRCExpressionsMenu.Control.ControlType.RadialPuppet ||
                       tOld == VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet ||
                       tOld == VRCExpressionsMenu.Control.ControlType.FourAxisPuppet
                   ) &&
                   (
                       tNew == VRCExpressionsMenu.Control.ControlType.RadialPuppet ||
                       tNew == VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet ||
                       tNew == VRCExpressionsMenu.Control.ControlType.FourAxisPuppet
                   );
        }

        private static void DoPuppetConversion(SerializedProperty property,
            VRCExpressionsMenu.Control.ControlType tNew)
        {
            var subParameters = property.FindPropertyRelative("subParameters");
            var sub0 = subParameters.GetArrayElementAtIndex(0).FindPropertyRelative("name").stringValue;
            var sub1 = subParameters.arraySize > 1
                ? subParameters.GetArrayElementAtIndex(1).FindPropertyRelative("name").stringValue
                : string.Empty;

            subParameters.ClearArray();
            subParameters.InsertArrayElementAtIndex(0);
            subParameters.GetArrayElementAtIndex(0).FindPropertyRelative("name").stringValue = sub0;

            // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
            switch (tNew)
            {
                case VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet:
                    subParameters.InsertArrayElementAtIndex(1);
                    subParameters.GetArrayElementAtIndex(1).FindPropertyRelative("name").stringValue = sub1;
                    break;

                case VRCExpressionsMenu.Control.ControlType.FourAxisPuppet:
                    subParameters.InsertArrayElementAtIndex(1);
                    subParameters.GetArrayElementAtIndex(1).FindPropertyRelative("name").stringValue = sub1;
                    subParameters.InsertArrayElementAtIndex(2);
                    subParameters.GetArrayElementAtIndex(2).FindPropertyRelative("name").stringValue = "";
                    subParameters.InsertArrayElementAtIndex(3);
                    subParameters.GetArrayElementAtIndex(3).FindPropertyRelative("name").stringValue = "";
                    break;
            }
        }

        private static void SetupSubParameters(SerializedProperty property,
            VRCExpressionsMenu.Control.ControlType type)
        {
            var subParameters = property.FindPropertyRelative("subParameters");
            subParameters.ClearArray();

            switch (type)
            {
                case VRCExpressionsMenu.Control.ControlType.RadialPuppet:
                case VRCExpressionsMenu.Control.ControlType.SubMenu:
                    subParameters.InsertArrayElementAtIndex(0);
                    break;
                case VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet:
                    subParameters.InsertArrayElementAtIndex(0);
                    subParameters.InsertArrayElementAtIndex(1);
                    break;
                case VRCExpressionsMenu.Control.ControlType.FourAxisPuppet:
                    subParameters.InsertArrayElementAtIndex(0);
                    subParameters.InsertArrayElementAtIndex(1);
                    subParameters.InsertArrayElementAtIndex(2);
                    subParameters.InsertArrayElementAtIndex(3);
                    break;
            }
        }

        #endregion

        /*static void DrawParameterNotFound(string parameter)
        {
            EditorGUILayout.HelpBox(
                $"Parameter not found on the active avatar descriptor ({parameter})",
                MessageType.Warning
            );
        }*/


        #region BuildParameterArray

        static void BuildParameterArray(
            string name,
            VRCExpressionParameters parameters,
            out int index,
            out string[] parametersAsString
        )
        {
            index = -2;
            if (!parameters)
            {
                parametersAsString = Array.Empty<string>();
                return;
            }

            if (!string.IsNullOrWhiteSpace(name))
            {
                for (var i = 0; i < parameters.parameters.Length; i++)
                {
                    if (parameters.parameters[i].name != name) continue;

                    index = i + 1;
                    break;
                }
            }
            else
                index = -1;

            parametersAsString = new string[parameters.parameters.Length + 1];
            parametersAsString[0] = "[None]";
            for (var i = 0; i < parameters.parameters.Length; i++)
            {
                switch (parameters.parameters[i].valueType)
                {
                    case VRCExpressionParameters.ValueType.Int:
                        parametersAsString[i + 1] = $"{parameters.parameters[i].name} [int]";
                        break;
                    case VRCExpressionParameters.ValueType.Float:
                        parametersAsString[i + 1] = $"{parameters.parameters[i].name} [float]";
                        break;
                    case VRCExpressionParameters.ValueType.Bool:
                        parametersAsString[i + 1] = $"{parameters.parameters[i].name} [bool]";
                        break;
                }
            }
        }

        static void BuildParameterArray(
            string name,
            VRCExpressionParameters parameters,
            out int index,
            out VRCExpressionParameters.Parameter[] filteredParameters,
            out string[] filteredParametersAsString,
            VRCExpressionParameters.ValueType filter
        )
        {
            index = -2;
            if (!parameters)
            {
                filteredParameters = Array.Empty<VRCExpressionParameters.Parameter>();
                filteredParametersAsString = Array.Empty<string>();
                return;
            }

            filteredParameters = parameters.parameters.Where(p => p.valueType == filter).ToArray();

            if (!string.IsNullOrWhiteSpace(name))
            {
                for (var i = 0; i < filteredParameters.Length; i++)
                {
                    if (filteredParameters[i].name != name) continue;

                    index = i + 1;
                    break;
                }
            }
            else
                index = -1;

            filteredParametersAsString = new string[filteredParameters.Length + 1];
            filteredParametersAsString[0] = "[None]";
            for (var i = 0; i < filteredParameters.Length; i++)
            {
                switch (filteredParameters[i].valueType)
                {
                    case VRCExpressionParameters.ValueType.Int:
                        filteredParametersAsString[i + 1] = $"{filteredParameters[i].name} [int]";
                        break;
                    case VRCExpressionParameters.ValueType.Float:
                        filteredParametersAsString[i + 1] = $"{filteredParameters[i].name} [float]";
                        break;
                    case VRCExpressionParameters.ValueType.Bool:
                        filteredParametersAsString[i + 1] = $"{filteredParameters[i].name} [bool]";
                        break;
                }
            }
        }

        #endregion

        #region DrawParameterSelector

        struct ParameterSelectorOptions
        {
            public Action ExtraGUI;
            public Rect Rect;
            public bool Required;

            public ParameterSelectorOptions(Rect rect, bool required, Action extraGUI = null)
            {
                this.Required = required;
                this.Rect = rect;
                this.ExtraGUI = extraGUI;
            }

            public ParameterSelectorOptions(Rect rect, Action extraGUI = null)
            {
                this.Required = false;
                this.Rect = rect;
                this.ExtraGUI = extraGUI;
            }

            public ParameterSelectorOptions(bool required, Action extraGUI = null)
            {
                this.Required = required;
                this.Rect = default;
                this.ExtraGUI = extraGUI;
            }
        }

        private static bool DrawParameterSelector(
            string label,
            SerializedProperty property,
            VRCExpressionParameters parameters,
            ParameterSelectorOptions options = default
        )
        {
            BuildParameterArray(
                property.FindPropertyRelative("name").stringValue,
                parameters,
                out var index,
                out var parametersAsString
            );
            return DrawParameterSelection__BASE(
                label,
                property,
                index,
                parameters,
                parameters?.parameters,
                parametersAsString,
                false,
                options
            );
        }

        private static bool DrawParameterSelector(
            string label,
            SerializedProperty property,
            VRCExpressionParameters parameters,
            VRCExpressionParameters.ValueType filter,
            ParameterSelectorOptions options = default
        )
        {
            BuildParameterArray(
                property.FindPropertyRelative("name").stringValue,
                parameters,
                out var index,
                out var filteredParameters,
                out var parametersAsString,
                filter
            );
            return DrawParameterSelection__BASE(
                label,
                property,
                index,
                parameters,
                filteredParameters,
                parametersAsString,
                true,
                options
            );
        }

        private static bool DrawParameterSelection__BASE(
            string label,
            SerializedProperty property,
            int index,
            VRCExpressionParameters targetParameters,
            VRCExpressionParameters.Parameter[] parameters,
            string[] parametersAsString,
            bool isFiltered,
            ParameterSelectorOptions options
        )
        {
            var isEmpty = index == -1;
            var isMissing = index == -2;
            bool willWarn = isMissing || options.Required && isEmpty;
            string parameterName = property.FindPropertyRelative("name").stringValue;
            string warnMsg = targetParameters
                ? isMissing
                    ? isFiltered
                        ? $"Parameter ({parameterName}) not found or invalid"
                        : $"Parameter ({parameterName}) not found on the active avatar descriptor"
                    : "Parameter is blank. Control may be dysfunctional."
                : Toolbox.GUIContent.MissingParametersTooltip;

            var rectNotProvided = options.Rect == default;
            using (new GUILayout.HorizontalScope())
            {
                const float contentAddWidth = 50;
                const float contentWarnWidth = 18;
                const float contentDropdownWidth = 20;
                //const float CONTENT_TEXT_FIELD_PORTION = 0.25f;
                float missingFullWidth = contentAddWidth + contentWarnWidth + 2;

                bool hasLabel = !string.IsNullOrEmpty(label);

                if (rectNotProvided) options.Rect = EditorGUILayout.GetControlRect(false, 18);

                var name = property.FindPropertyRelative("name");

                Rect labelRect = new Rect(options.Rect) { width = hasLabel ? 120 : 0 };
                Rect textfieldRect = new Rect(labelRect)
                {
                    x = labelRect.x + labelRect.width,
                    width = options.Rect.width - labelRect.width - contentDropdownWidth - 2
                };
                Rect dropdownRect = new Rect(textfieldRect)
                    { x = textfieldRect.x + textfieldRect.width, width = contentDropdownWidth };
                Rect addRect = Rect.zero;
                Rect warnRect = Rect.zero;

                if (targetParameters && isMissing)
                {
                    textfieldRect.width -= missingFullWidth;
                    dropdownRect.x -= missingFullWidth;
                    addRect = new Rect(options.Rect)
                    {
                        x = textfieldRect.x + textfieldRect.width + contentDropdownWidth + 2,
                        width = contentAddWidth
                    };
                    warnRect = new Rect(addRect) { x = addRect.x + addRect.width, width = contentWarnWidth };
                }
                else if (!targetParameters || options.Required && isEmpty || true)
                {
                    textfieldRect.width -= contentWarnWidth;
                    dropdownRect.x -= contentWarnWidth;
                    warnRect = new Rect(dropdownRect)
                        { x = dropdownRect.x + dropdownRect.width, width = contentWarnWidth };
                }

                if (hasLabel) GUI.Label(labelRect, label);
                using (new EditorGUI.DisabledScope(!targetParameters || parametersAsString.Length <= 1))
                {
                    var newIndex = EditorGUI.Popup(dropdownRect, string.Empty, index, parametersAsString);
                    if (index != newIndex)
                        name.stringValue = newIndex == 0 ? string.Empty : parameters[newIndex - 1].name;
                }

                name.stringValue = EditorGUI.TextField(textfieldRect, name.stringValue);
                if (string.IsNullOrEmpty(name.stringValue))
                    GUI.Label(textfieldRect, "Parameter", Toolbox.Styles.Label.PlaceHolder);
                if (willWarn)
                    GUI.Label(warnRect,
                        new GUIContent(Toolbox.GUIContent.Warn) { tooltip = warnMsg });

#if PARAMETER_RENAMER_INSTALLED
                if (!willWarn)
                {
                    var editIcon = new GUIContent(EditorGUIUtility.IconContent("d_editicon.sml"));
                    if (GUI.Button(warnRect, new GUIContent(editIcon), EditorStyles.label))
                    {
                        try
                        {
                            ParameterRenamer.Show(name.stringValue, VRCSDKPlus.GetAvatar());
                        }
                        catch (Exception exception)
                        {
                            Console.WriteLine(exception);
                            throw;
                        }
                    }
                }
#endif

                if (isMissing)
                {
                    int dummy;

                    if (!isFiltered)
                    {
                        dummy = EditorGUI.Popup(addRect, -1,
                            Enum.GetNames(typeof(VRCExpressionParameters.ValueType)));

                        addRect.x += 3;
                        GUI.Label(addRect, "Add");
                    }
                    else dummy = GUI.Button(addRect, "Add") ? 1 : -1;

                    if (dummy != -1)
                    {
                        SerializedObject so = new SerializedObject(targetParameters);
                        var param = so.FindProperty("parameters");
                        var prop = param.GetArrayElementAtIndex(param.arraySize++);
                        prop.FindPropertyRelative("valueType").enumValueIndex = dummy;
                        prop.FindPropertyRelative("name").stringValue = name.stringValue;
                        prop.FindPropertyRelative("saved").boolValue = true;
                        try
                        {
                            prop.FindPropertyRelative("networkSynced").boolValue = true;
                        }
                        catch
                        {
                        }

                        so.ApplyModifiedProperties();
                    }
                }

                options.ExtraGUI?.Invoke();
            }

            return isMissing;
        }

        #endregion

        #region Parameter conainer

        static void ParameterContainer(
            SerializedProperty property,
            VRCExpressionParameters parameters,
            Rect rect = default
        )
        {
            var rectProvided = rect != default;

            if (property?.FindPropertyRelative("parameter") == null)
            {
                if (rectProvided)
                    Toolbox.Placeholder.GUI(rect);
                else
                {
                    Toolbox.Container.BeginLayout();
                    Toolbox.Placeholder.GUILayout(18);
                    Toolbox.Container.EndLayout();
                }
            }
            else
            {
                if (!rectProvided) Toolbox.Container.BeginLayout();

                float contentValueSelectorWidth = 50;
                Rect selectorRect = default;
                Rect valueRect = default;

                if (rectProvided)
                {
                    selectorRect = new Rect(rect.x, rect.y, rect.width - contentValueSelectorWidth - 3,
                        rect.height);
                    valueRect = new Rect(selectorRect.x + selectorRect.width + 3, rect.y,
                        contentValueSelectorWidth, rect.height);
                }

                var parameter = property.FindPropertyRelative("parameter");

                var t = (VRCExpressionsMenu.Control.ControlType)property.FindPropertyRelative("type").intValue;
                bool isRequired = t == VRCExpressionsMenu.Control.ControlType.Button ||
                                  t == VRCExpressionsMenu.Control.ControlType.Toggle;
                DrawParameterSelector(rectProvided ? string.Empty : "Parameter", parameter, parameters,
                    new ParameterSelectorOptions()
                    {
                        Rect = selectorRect,
                        Required = isRequired,
                        ExtraGUI = () =>
                        {
                            #region Value selector

                            var parameterName = parameter.FindPropertyRelative("name");
                            var param = parameters?.parameters.FirstOrDefault(p =>
                                p.name == parameterName.stringValue);

                            // Check what type the parameter is
                            var value = property.FindPropertyRelative("value");
                            switch (param?.valueType)
                            {
                                case VRCExpressionParameters.ValueType.Int:
                                    value.floatValue = Mathf.Clamp(
                                        rectProvided
                                            ? EditorGUI.IntField(valueRect, (int)value.floatValue)
                                            : EditorGUILayout.IntField((int)value.floatValue,
                                                GUILayout.Width(contentValueSelectorWidth)), 0f, 255f);
                                    break;

                                case VRCExpressionParameters.ValueType.Float:
                                    value.floatValue =
                                        Mathf.Clamp(
                                            rectProvided
                                                ? EditorGUI.FloatField(valueRect, value.floatValue)
                                                : EditorGUILayout.FloatField(value.floatValue,
                                                    GUILayout.Width(contentValueSelectorWidth)), -1, 1);
                                    break;

                                case VRCExpressionParameters.ValueType.Bool:
                                    using (new EditorGUI.DisabledScope(true))
                                    {
                                        if (rectProvided) EditorGUI.TextField(valueRect, string.Empty);
                                        else
                                            EditorGUILayout.TextField(string.Empty,
                                                GUILayout.Width(contentValueSelectorWidth));
                                    }

                                    value.floatValue = 1f;
                                    break;

                                default:
                                    value.floatValue = Mathf.Clamp(
                                        rectProvided
                                            ? EditorGUI.FloatField(valueRect, value.floatValue)
                                            : EditorGUILayout.FloatField(value.floatValue,
                                                GUILayout.Width(contentValueSelectorWidth)), -1, 255);
                                    break;
                            }

                            #endregion
                        }
                    });

                if (!rectProvided)
                    Toolbox.Container.EndLayout();
            }
        }

        #endregion

        #region Miscellaneous containers

        static void RadialContainer(SerializedProperty property, VRCExpressionParameters parameters)
        {
            using (new Toolbox.Container.Vertical())
                DrawParameterSelector(
                    "Rotation",
                    property.FindPropertyRelative("subParameters").GetArrayElementAtIndex(0),
                    parameters,
                    VRCExpressionParameters.ValueType.Float,
                    new ParameterSelectorOptions(true)
                );
        }

        static void SubMenuContainer(SerializedProperty property)
        {
            using (new Toolbox.Container.Vertical())
            {
                var subMenu = property.FindPropertyRelative("subMenu");
                var nameProperty = property.FindPropertyRelative("name");
                bool emptySubmenu = subMenu.objectReferenceValue == null;

                using (new GUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(subMenu);
                    if (emptySubmenu)
                    {
                        using (new EditorGUI.DisabledScope(VRCMenuPlus.GetCurrentNode()?.Value == null))
                            if (GUILayout.Button("New", GUILayout.Width(40)))
                            {
                                var m = VRCMenuPlus.GetCurrentNode().Value;
                                var path = AssetDatabase.GetAssetPath(m);
                                if (string.IsNullOrEmpty(path))
                                    path = $"Assets/{m.name}.asset";
                                var parentPath = Path.GetDirectoryName(path);
                                var assetName = string.IsNullOrEmpty(nameProperty?.stringValue)
                                    ? $"{m.name} SubMenu.asset"
                                    : $"{nameProperty.stringValue} Menu.asset";
                                var newMenuPath = Toolbox.ReadyAssetPath(parentPath, assetName, true);

                                var newMenu = VRCMenuPlus.CreateInstance<VRCExpressionsMenu>();
                                if (newMenu.controls == null)
                                    newMenu.controls = new List<VRCExpressionsMenu.Control>();

                                AssetDatabase.CreateAsset(newMenu, newMenuPath);
                                subMenu.objectReferenceValue = newMenu;
                            }

                        GUILayout.Label(
                            new GUIContent(Toolbox.GUIContent.Warn)
                                { tooltip = "Submenu is empty. This control has no use." },
                            Toolbox.Styles.icon);
                    }

                    using (new EditorGUI.DisabledScope(emptySubmenu))
                    {
                        if (Helpers.ClickableButton(Toolbox.GUIContent.Folder,
                                Toolbox.Styles.icon))
                            Selection.activeObject = subMenu.objectReferenceValue;
                        if (Helpers.ClickableButton(Toolbox.GUIContent.Clear,
                                Toolbox.Styles.icon))
                            subMenu.objectReferenceValue = null;
                    }
                }
            }
        }

        static void CompactTwoAxisParametersContainer(SerializedProperty property,
            VRCExpressionParameters parameters)
        {
            using (new Toolbox.Container.Vertical())
            {
                using (new GUILayout.HorizontalScope())
                {
                    using (new GUILayout.HorizontalScope())
                        GUILayout.Label("Axis Parameters", Toolbox.Styles.Label.Centered);


                    using (new GUILayout.HorizontalScope())
                    {
                        GUILayout.Label("Name -", Toolbox.Styles.Label.Centered);
                        GUILayout.Label("Name +", Toolbox.Styles.Label.Centered);
                    }
                }

                var subs = property.FindPropertyRelative("subParameters");
                var sub0 = subs.GetArrayElementAtIndex(0);
                var sub1 = subs.GetArrayElementAtIndex(1);

                var labels = SafeGetLabels(property);

                using (new GUILayout.HorizontalScope())
                {
                    var rect = EditorGUILayout.GetControlRect();
                    using (new GUILayout.HorizontalScope())
                    {
                        DrawParameterSelector(
                            "Horizontal",
                            sub0,
                            parameters,
                            VRCExpressionParameters.ValueType.Float,
                            new ParameterSelectorOptions(rect, true)
                        );
                    }

                    using (new GUILayout.HorizontalScope())
                    {
                        DrawLabel(labels.GetArrayElementAtIndex(0), "Left");
                        DrawLabel(labels.GetArrayElementAtIndex(1), "Right");
                    }
                }

                using (new GUILayout.HorizontalScope())
                {
                    var rect = EditorGUILayout.GetControlRect();
                    using (new GUILayout.HorizontalScope())
                    {
                        DrawParameterSelector(
                            "Vertical",
                            sub1,
                            parameters,
                            VRCExpressionParameters.ValueType.Float,
                            new ParameterSelectorOptions(rect, true)
                        );
                    }

                    using (new GUILayout.HorizontalScope())
                    {
                        DrawLabel(labels.GetArrayElementAtIndex(2), "Down");
                        DrawLabel(labels.GetArrayElementAtIndex(3), "Up");
                    }
                }
            }
        }

        static void CompactFourAxisParametersContainer(SerializedProperty property,
            VRCExpressionParameters parameters)
        {
            using (new Toolbox.Container.Vertical())
            {
                using (new GUILayout.HorizontalScope())
                {
                    var headerRect = EditorGUILayout.GetControlRect();
                    var r1 = new Rect(headerRect) { width = headerRect.width / 2 };
                    var r2 = new Rect(r1) { x = r1.x + r1.width };
                    GUI.Label(r1, "Axis Parameters", Toolbox.Styles.Label.Centered);
                    GUI.Label(r2, "Name", Toolbox.Styles.Label.Centered);
                }

                var subs = property.FindPropertyRelative("subParameters");
                var sub0 = subs.GetArrayElementAtIndex(0);
                var sub1 = subs.GetArrayElementAtIndex(1);
                var sub2 = subs.GetArrayElementAtIndex(2);
                var sub3 = subs.GetArrayElementAtIndex(3);

                var labels = SafeGetLabels(property);

                using (new GUILayout.HorizontalScope())
                {
                    var r = EditorGUILayout.GetControlRect();
                    using (new GUILayout.HorizontalScope())
                    {
                        DrawParameterSelector(
                            "Up",
                            sub0,
                            parameters,
                            VRCExpressionParameters.ValueType.Float,
                            new ParameterSelectorOptions(r, true)
                        );
                    }

                    using (new GUILayout.HorizontalScope())
                        DrawLabel(labels.GetArrayElementAtIndex(0), "Name");
                }

                using (new GUILayout.HorizontalScope())
                {
                    var r = EditorGUILayout.GetControlRect();
                    using (new GUILayout.HorizontalScope())
                    {
                        DrawParameterSelector(
                            "Right",
                            sub1,
                            parameters,
                            VRCExpressionParameters.ValueType.Float,
                            new ParameterSelectorOptions(r, true)
                        );
                    }

                    using (new GUILayout.HorizontalScope())
                        DrawLabel(labels.GetArrayElementAtIndex(1), "Name");
                }

                using (new GUILayout.HorizontalScope())
                {
                    var r = EditorGUILayout.GetControlRect();
                    using (new GUILayout.HorizontalScope())
                    {
                        DrawParameterSelector(
                            "Down",
                            sub2,
                            parameters,
                            VRCExpressionParameters.ValueType.Float,
                            new ParameterSelectorOptions(r, true)
                        );
                    }

                    using (new GUILayout.HorizontalScope())
                        DrawLabel(labels.GetArrayElementAtIndex(2), "Name");
                }

                using (new GUILayout.HorizontalScope())
                {
                    var r = EditorGUILayout.GetControlRect();
                    using (new GUILayout.HorizontalScope())
                    {
                        DrawParameterSelector(
                            "Left",
                            sub3,
                            parameters,
                            VRCExpressionParameters.ValueType.Float,
                            new ParameterSelectorOptions(r, true)
                        );
                    }

                    using (new GUILayout.HorizontalScope())
                        DrawLabel(labels.GetArrayElementAtIndex(3), "Name");
                }
            }
        }

        static void TwoAxisParametersContainer(SerializedProperty property, VRCExpressionParameters parameters)
        {
            Toolbox.Container.BeginLayout();

            GUILayout.Label("Axis Parameters", Toolbox.Styles.Label.Centered);

            var subs = property.FindPropertyRelative("subParameters");
            var sub0 = subs.GetArrayElementAtIndex(0);
            var sub1 = subs.GetArrayElementAtIndex(1);

            DrawParameterSelector(
                "Horizontal",
                sub0,
                parameters,
                VRCExpressionParameters.ValueType.Float,
                new ParameterSelectorOptions(true)
            );

            DrawParameterSelector(
                "Vertical",
                sub1,
                parameters,
                VRCExpressionParameters.ValueType.Float,
                new ParameterSelectorOptions(true)
            );

            Toolbox.Container.EndLayout();
        }

        static void FourAxisParametersContainer(SerializedProperty property, VRCExpressionParameters parameters)
        {
            Toolbox.Container.BeginLayout("Axis Parameters");

            var subs = property.FindPropertyRelative("subParameters");
            var sub0 = subs.GetArrayElementAtIndex(0);
            var sub1 = subs.GetArrayElementAtIndex(1);
            var sub2 = subs.GetArrayElementAtIndex(2);
            var sub3 = subs.GetArrayElementAtIndex(3);

            DrawParameterSelector(
                "Up",
                sub0,
                parameters,
                VRCExpressionParameters.ValueType.Float,
                new ParameterSelectorOptions(true)
            );

            DrawParameterSelector(
                "Right",
                sub1,
                parameters,
                VRCExpressionParameters.ValueType.Float,
                new ParameterSelectorOptions(true)
            );

            DrawParameterSelector(
                "Down",
                sub2,
                parameters,
                VRCExpressionParameters.ValueType.Float,
                new ParameterSelectorOptions(true)
            );

            DrawParameterSelector(
                "Left",
                sub3,
                parameters,
                VRCExpressionParameters.ValueType.Float,
                new ParameterSelectorOptions(true)
            );

            Toolbox.Container.EndLayout();
        }

        static void AxisCustomisationContainer(SerializedProperty property)
        {
            var labels = SafeGetLabels(property);

            using (new Toolbox.Container.Vertical("Customization"))
            {
                DrawLabel(labels.GetArrayElementAtIndex(0), "Up");
                DrawLabel(labels.GetArrayElementAtIndex(1), "Right");
                DrawLabel(labels.GetArrayElementAtIndex(2), "Down");
                DrawLabel(labels.GetArrayElementAtIndex(3), "Left");
            }
        }

        static SerializedProperty SafeGetLabels(SerializedProperty property)
        {
            var labels = property.FindPropertyRelative("labels");

            labels.arraySize = 4;
            var l0 = labels.GetArrayElementAtIndex(0);
            if (l0 == null)
            {
                var menu = (VRCExpressionsMenu)labels.serializedObject.targetObject;
                var index = menu.controls.FindIndex(property.objectReferenceValue);
                menu.controls[index].labels = new[]
                {
                    new VRCExpressionsMenu.Control.Label(),
                    new VRCExpressionsMenu.Control.Label(),
                    new VRCExpressionsMenu.Control.Label(),
                    new VRCExpressionsMenu.Control.Label()
                };
            }

            if (labels.GetArrayElementAtIndex(0) == null)
                Debug.Log("ITEM IS NULL");

            return labels;
        }

        static void DrawLabel(SerializedProperty property, string type)
        {
            bool compact = Toolbox.Preferences.CompactMode;
            float imgWidth = compact ? 28 : 58;
            float imgHeight = compact ? EditorGUIUtility.singleLineHeight : 58;

            var imgProperty = property.FindPropertyRelative("icon");
            var nameProperty = property.FindPropertyRelative("name");
            if (!compact) EditorGUILayout.BeginVertical("helpbox");

            using (new GUILayout.HorizontalScope())
            {
                using (new GUILayout.VerticalScope())
                {
                    if (!compact)
                        using (new EditorGUI.DisabledScope(true))
                            EditorGUILayout.LabelField("Axis", type,
                                Toolbox.Styles.Label.LabelDropdown);

                    EditorGUILayout.PropertyField(nameProperty,
                        compact ? GUIContent.none : new GUIContent("Name"));
                    var nameRect = GUILayoutUtility.GetLastRect();
                    if (compact && string.IsNullOrEmpty(nameProperty.stringValue))
                        GUI.Label(nameRect, $"{type}", Toolbox.Styles.Label.PlaceHolder);
                }

                imgProperty.objectReferenceValue = EditorGUILayout.ObjectField(imgProperty.objectReferenceValue,
                    typeof(Texture2D), false, GUILayout.Width(imgWidth), GUILayout.Height(imgHeight));
            }

            if (!compact) EditorGUILayout.EndHorizontal();
        }

        #endregion
    }
}