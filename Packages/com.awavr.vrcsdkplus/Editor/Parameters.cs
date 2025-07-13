using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using AnimatorController = UnityEditor.Animations.AnimatorController;
using AnimatorControllerParameter = UnityEngine.AnimatorControllerParameter;
using AnimatorControllerParameterType = UnityEngine.AnimatorControllerParameterType;

namespace AwAVR.VRCSDKPlus
{
    internal sealed class VRCParamsPlus : Editor
    {
        private static int _maxMemoryCost;

        private static int MaxMemoryCost
        {
            get
            {
                if (_maxMemoryCost == 0)
                {
                    try
                    {
                        _maxMemoryCost = (int)typeof(VRCExpressionParameters)
                            .GetField("MAX_PARAMETER_COST", BindingFlags.Static | BindingFlags.Public)
                            .GetValue(null);
                    }
                    catch
                    {
                        Debug.LogError("Failed to dynamically get MAX_PARAMETER_COST. Falling back to 256");
                        _maxMemoryCost = 256;
                    }
                }

                return _maxMemoryCost;
            }
        }

        private static readonly bool HasSyncingOption =
            typeof(VRCExpressionParameters.Parameter).GetField("networkSynced") != null;

        private static bool _editorActive = true;
        private static bool _canCleanup;
        private int _currentCost;
        private string _searchValue;

        private SerializedProperty _parameterList;
        private ReorderableList _parametersOrderList;

        private ParameterStatus[] _parameterStatus;

        private static VRCExpressionParameters _mergeParams;

        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            using (new GUILayout.VerticalScope("helpbox"))
                Core.GetAvatar(ref VRCSDKPlus.GetAvatarRef(), ref VRCSDKPlus.GetValidAvatarsRef());

            CalculateTotalCost();
            ShowTotalMemory();

            _canCleanup = false;
            serializedObject.Update();
            HandleParameterEvents();
            _parametersOrderList.DoLayoutList();
            serializedObject.ApplyModifiedProperties();

            if (_canCleanup)
            {
                using (new GUILayout.HorizontalScope("helpbox"))
                {
                    GUILayout.Label("Cleanup Invalid, Blank, and Duplicate Parameters");
                    if (Helpers.ClickableButton("Cleanup"))
                    {
                        VRCSDKPlus.RefreshValidParameters();
                        _parameterList.IterateArray((i, p) =>
                        {
                            var name = p.FindPropertyRelative("name").stringValue;
                            if (string.IsNullOrEmpty(name))
                            {
                                Helpers.GreenLog($"Deleted blank parameter at index {i}");
                                _parameterList.DeleteArrayElementAtIndex(i);
                                return false;
                            }

                            if (VRCSDKPlus.GetAvatar() && VRCSDKPlus.GetValidParameters().All(p2 => p2.name != name))
                            {
                                Helpers.GreenLog($"Deleted invalid parameter {name}");
                                _parameterList.DeleteArrayElementAtIndex(i);
                                return false;
                            }

                            _parameterList.IterateArray((j, p2) =>
                            {
                                if (name == p2.FindPropertyRelative("name").stringValue)
                                {
                                    Helpers.GreenLog($"Deleted duplicate parameter {name}");
                                    _parameterList.DeleteArrayElementAtIndex(j);
                                }

                                return false;
                            }, i);


                            return false;
                        });
                        serializedObject.ApplyModifiedProperties();
                        VRCSDKPlus.RefreshValidParameters();
                        Helpers.GreenLog("Finished Cleanup!");
                    }
                }
            }

            EditorGUI.BeginChangeCheck();
            using (new GUILayout.HorizontalScope("helpbox"))
                _mergeParams = (VRCExpressionParameters)EditorGUILayout.ObjectField("Merge Parameters", null,
                    typeof(VRCExpressionParameters), true);
            if (EditorGUI.EndChangeCheck())
            {
                if (_mergeParams)
                {
                    if (_mergeParams.parameters != null)
                    {
                        VRCExpressionParameters myParams = (VRCExpressionParameters)target;
                        Undo.RecordObject(myParams, "Merge Parameters");
                        myParams.parameters = myParams.parameters.Concat(_mergeParams.parameters.Select(p =>
                            new VRCExpressionParameters.Parameter()
                            {
                                defaultValue = p.defaultValue,
                                name = p.name,
                                networkSynced = p.networkSynced,
                                valueType = p.valueType
                            })).ToArray();
                        EditorUtility.SetDirty(myParams);
                    }

                    _mergeParams = null;
                }
            }

            ShowTotalMemory();

            if (EditorGUI.EndChangeCheck()) RefreshAllParameterStatus();
        }

        private void ShowTotalMemory()
        {
            try
            {
                using (new EditorGUILayout.HorizontalScope("helpbox"))
                {
                    GUILayout.FlexibleSpace();
                    using (new GUILayout.VerticalScope())
                    {
                        using (new GUILayout.HorizontalScope())
                        {
                            GUILayout.FlexibleSpace();
                            GUILayout.Label("Total Memory");
                            GUIContent help = new GUIContent
                            {
                                image = EditorGUIUtility.IconContent("d_Help").image,
                                tooltip =
                                    $"VRChat only allows {MaxMemoryCost} synced parameter bits on an avatar. Only synced parameters in the VRC Expression Parameters are counted towards this." +
                                    "\n\n" +
                                    "Parameters in the animator can be any type that you want, they don't need to be the same as in the VRC Expression Parameters list. VRChat will automatically convert them, see more here: https://creators.vrchat.com/avatars/animator-parameters/#mismatched-parameter-type-conversion" +
                                    "\n\n" +
                                    "- bool = 1 bit\n" +
                                    "- int = 8 bits\n" +
                                    "- float = 8 bits"
                            };
                            GUILayout.Label(help);
                            GUILayout.FlexibleSpace();
                        }

                        using (new GUILayout.HorizontalScope())
                        {
                            GUILayout.FlexibleSpace();

                            // progress bar
                            Rect progressRect =
                                EditorGUILayout.GetControlRect(true, 20, GUILayout.ExpandWidth(true));
                            float cost = Mathf.Clamp01((float)_currentCost / MaxMemoryCost);
                            EditorGUI.ProgressBar(progressRect, cost, $"{_currentCost} / {MaxMemoryCost}");

                            // warning icon
                            if (_currentCost > MaxMemoryCost)
                                GUILayout.Label(VRCSDKPlus.GetRedWarnIcon(), GUILayout.Width(20));

                            GUILayout.FlexibleSpace();
                        }
                    }

                    GUILayout.FlexibleSpace();
                }
            }
            catch
            {
                // ignored
            }
        }

        private void OnEnable()
        {
            VRCSDKPlus.InitConstants();
            VRCSDKPlus.RefreshAvatar(a => a.expressionParameters == target);

            _parameterList = serializedObject.FindProperty("parameters");
            RefreshParametersOrderList();
            RefreshAllParameterStatus();
        }

        private void DrawElement(Rect rect, int index, bool active, bool focused)
        {
            if (!(index < _parameterList.arraySize && index >= 0)) return;

            var screenRect = GUIUtility.GUIToScreenRect(rect);
            if (screenRect.y > Screen.currentResolution.height || screenRect.y + screenRect.height < 0) return;

            SerializedProperty parameter = _parameterList.GetArrayElementAtIndex(index);
            SerializedProperty name = parameter.FindPropertyRelative("name");
            SerializedProperty valueType = parameter.FindPropertyRelative("valueType");
            SerializedProperty defaultValue = parameter.FindPropertyRelative("defaultValue");
            SerializedProperty saved = parameter.FindPropertyRelative("saved");
            SerializedProperty synced = HasSyncingOption ? parameter.FindPropertyRelative("networkSynced") : null;

            var status = _parameterStatus[index];
            bool parameterEmpty = status.ParameterEmpty;
            bool parameterAddable = status.ParameterAddable;
            bool parameterIsDuplicate = status.ParameterIsDuplicate;
            bool hasWarning = status.HasWarning;
            string warnMsg = parameterEmpty ? "Blank Parameter" :
                parameterIsDuplicate ? "Duplicate Parameter! May cause issues!" :
                "Parameter not found in any playable controller of Active Avatar";
            AnimatorControllerParameter matchedParameter = status.MatchedParameter;

            _canCleanup |= hasWarning;

            #region Rects

            rect.y += 1;
            rect.height = 18;


            Rect UseNext(float width, bool fixedWidth = false, float position = -1, bool fixedPosition = false)
            {
                Rect currentRect = rect;
                currentRect.width = fixedWidth ? width : width * rect.width / 100;
                currentRect.height = rect.height;
                currentRect.x = position == -1 ? rect.x :
                    fixedPosition ? position : rect.x + position * rect.width / 100;
                currentRect.y = rect.y;
                rect.x += currentRect.width;
                return currentRect;
            }

            Rect UseEnd(ref Rect r, float width, bool fixedWidth = false, float positionOffset = -1,
                bool fixedPosition = false)
            {
                Rect returnRect = r;
                returnRect.width = fixedWidth ? width : width * r.width / 100;
                float positionAdjust = positionOffset == -1 ? 0 :
                    fixedPosition ? positionOffset : positionOffset * r.width / 100;
                returnRect.x = r.x + r.width - returnRect.width - positionAdjust;
                r.width -= returnRect.width + positionAdjust;
                return returnRect;
            }

            Rect contextRect = rect;
            contextRect.x -= 20;
            contextRect.width = 20;

            Rect removeRect = UseEnd(ref rect, 32, true, 4, true);
            Rect syncedRect = HasSyncingOption ? UseEnd(ref rect, 18, true, 16f, true) : Rect.zero;
            Rect savedRect = UseEnd(ref rect, 18, true, HasSyncingOption ? 34f : 16, true);
            Rect defaultRect = UseEnd(ref rect, 85, true, 32, true);
            Rect typeRect = UseEnd(ref rect, 85, true, 12, true);
            Rect warnRect = UseEnd(ref rect, 18, true, 4, true);
            Rect addRect = hasWarning && parameterAddable ? UseEnd(ref rect, 55, true, 4, true) : Rect.zero;
            Rect dropdownRect = UseEnd(ref rect, 21, true, 1, true);
            dropdownRect.x -= 3;
            Rect nameRect = UseNext(100);

            //Rect removeRect = new Rect(rect.x + rect.width - 36, rect.y, 32, 18);
            //Rect syncedRect = new Rect(rect.x + rect.width - 60, rect.y, 14, 18);

            #endregion

            using (new EditorGUI.DisabledScope(!string.IsNullOrEmpty(_searchValue) &&
                                               !Regex.IsMatch(name.stringValue, $@"(?i){_searchValue}")))
            {
                //Hacky way to avoid proper UI Layout
                string parameterFieldName = $"namefield{index}";

                using (new EditorGUI.DisabledScope(VRCSDKPlus.GetValidParameters().Length == 0))
                    if (GUI.Button(dropdownRect, GUIContent.none, EditorStyles.popup))
                    {
                        var filteredParameters = VRCSDKPlus.GetValidParameters().Where(conParam =>
                            !_parameterList.IterateArray((_, prop) =>
                                prop.FindPropertyRelative("name").stringValue == conParam.name)).ToArray();
                        if (filteredParameters.Any())
                        {
                            Toolbox.CustomDropdown<AnimatorControllerParameter> textDropdown =
                                new Toolbox.CustomDropdown<AnimatorControllerParameter>(null,
                                    filteredParameters, item =>
                                    {
                                        using (new GUILayout.HorizontalScope())
                                        {
                                            GUILayout.Label(item.value.name);
                                            GUILayout.Label(item.value.type.ToString(),
                                                Toolbox.Styles.Label.TypeLabel,
                                                GUILayout.ExpandWidth(false));
                                        }
                                    }, (_, conParam) =>
                                    {
                                        name.stringValue = conParam.name;
                                        name.serializedObject.ApplyModifiedProperties();
                                        RefreshAllParameterStatus();
                                    });
                            textDropdown.EnableSearch((conParameter, search) =>
                                Regex.IsMatch(conParameter.name, $@"(?i){search}"));
                            textDropdown.Show(nameRect);
                        }
                    }

                GUI.SetNextControlName(parameterFieldName);
                EditorGUI.PropertyField(nameRect, name, GUIContent.none);
                EditorGUI.PropertyField(typeRect, valueType, GUIContent.none);
                EditorGUI.PropertyField(savedRect, saved, GUIContent.none);

                GUI.Label(nameRect, matchedParameter != null ? $"({matchedParameter.type})" : "(?)",
                    Toolbox.Styles.Label.RightPlaceHolder);

                if (HasSyncingOption) EditorGUI.PropertyField(syncedRect, synced, GUIContent.none);

                if (parameterAddable)
                {
                    using (var change = new EditorGUI.ChangeCheckScope())
                    {
                        Helpers.w_MakeRectLinkCursor(addRect);
                        int dummy = EditorGUI.IntPopup(addRect, -1, VRCSDKPlus.GetValidPlayables(),
                            VRCSDKPlus.GetValidPlayableIndexes());
                        if (change.changed)
                        {
                            var playable = (VRCAvatarDescriptor.AnimLayerType)dummy;
                            if (VRCSDKPlus.GetAvatar().GetPlayableLayer(playable, out AnimatorController c))
                            {
                                if (c.parameters.All(p => p.name != name.stringValue))
                                {
                                    AnimatorControllerParameterType paramType;
                                    switch (valueType.enumValueIndex)
                                    {
                                        case 0:
                                            paramType = AnimatorControllerParameterType.Int;
                                            break;
                                        case 1:
                                            paramType = AnimatorControllerParameterType.Float;
                                            break;
                                        default:
                                            paramType = AnimatorControllerParameterType.Bool;
                                            break;
                                    }

                                    c.AddParameter(new AnimatorControllerParameter()
                                    {
                                        name = name.stringValue,
                                        type = paramType,
                                        defaultFloat = defaultValue.floatValue,
                                        defaultInt = (int)defaultValue.floatValue,
                                        defaultBool = defaultValue.floatValue > 0
                                    });

                                    Helpers.GreenLog(
                                        $"Added {paramType} {name.stringValue} to {playable} Playable Controller");
                                }

                                VRCSDKPlus.RefreshValidParameters();
                            }
                        }
                    }

                    addRect.x += 3;
                    GUI.Label(addRect, "Add");
                }

                if (hasWarning)
                    GUI.Label(warnRect, new GUIContent(VRCSDKPlus.GetYellowWarnIcon()) { tooltip = warnMsg });

#if PARAMETER_RENAMER_INSTALLED
                if (!hasWarning)
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

                switch (valueType.enumValueIndex)
                {
                    case 2:
                        EditorGUI.BeginChangeCheck();
                        int dummy = EditorGUI.Popup(defaultRect, defaultValue.floatValue == 0 ? 0 : 1,
                            new[] { "False", "True" });
                        if (EditorGUI.EndChangeCheck())
                            defaultValue.floatValue = dummy;
                        break;
                    default:
                        EditorGUI.PropertyField(defaultRect, defaultValue, GUIContent.none);
                        break;
                }

                Helpers.w_MakeRectLinkCursor(removeRect);
                if (GUI.Button(removeRect, Toolbox.GUIContent.Remove,
                        Toolbox.Styles.Label.RemoveIcon))
                    DeleteParameter(index);
            }

            var e = Event.current;
            if (e.type == EventType.ContextClick && contextRect.Contains(e.mousePosition))
            {
                e.Use();
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Duplicate"), false, () => DuplicateParameter(index));
                menu.AddSeparator(string.Empty);
                menu.AddItem(new GUIContent("Delete"), false, () => DeleteParameter(index));
                menu.ShowAsContext();
            }
        }


        private void DrawHeader(Rect rect)
        {
            #region Rects

            /*rect.y += 1;
            rect.height = 18;

            Rect baseRect = rect;

            Rect UseNext(float width, bool fixedWidth = false, float position = -1, bool fixedPosition = false)
            {
                Rect currentRect = baseRect;
                currentRect.width = fixedWidth ? width : width * baseRect.width / 100;
                currentRect.height = baseRect.height;
                currentRect.x = position == -1 ? baseRect.x : fixedPosition ? position : rect.x + position * baseRect.width / 100; ;
                currentRect.y = baseRect.y;
                baseRect.x += currentRect.width;
                return currentRect;
            }

            Rect UseEnd(ref Rect r, float width, bool fixedWidth = false, float positionOffset = -1, bool fixedPosition = false)
            {
                Rect returnRect = r;
                returnRect.width = fixedWidth ? width : width * r.width / 100;
                float positionAdjust = positionOffset == -1 ? 0 : fixedPosition ? positionOffset : positionOffset * r.width / 100;
                returnRect.x = r.x + r.width - returnRect.width - positionAdjust;
                r.width -= returnRect.width + positionAdjust;
                return returnRect;
            }

            UseEnd(ref rect, 32, true, 4, true);
            Rect syncedRect = UseEnd(ref rect, 55, true);
            Rect savedRect = UseEnd(ref rect, 55, true);
            Rect defaultRect = UseEnd(ref rect, 60, true, 30, true);
            Rect typeRect = UseNext(16.66f);
            Rect nameRect = UseNext(rect.width * 0.4f, true);
            Rect searchIconRect = nameRect;
            searchIconRect.x += searchIconRect.width / 2 - 40;
            searchIconRect.width = 18;
            Rect searchRect = Rect.zero;
            Rect searchClearRect = Rect.zero;

            UseNext(canCleanup ? 12 : 26, true);
            UseNext(12, true);*/

            rect.y += 1;
            rect.height = 18;


            Rect UseNext(float width, bool fixedWidth = false, float position = -1, bool fixedPosition = false)
            {
                Rect currentRect = rect;
                currentRect.width = fixedWidth ? width : width * rect.width / 100;
                currentRect.height = rect.height;
                currentRect.x = position == -1 ? rect.x :
                    fixedPosition ? position : rect.x + position * rect.width / 100;
                currentRect.y = rect.y;
                rect.x += currentRect.width;
                return currentRect;
            }

            Rect UseEnd(ref Rect r, float width, bool fixedWidth = false, float positionOffset = -1,
                bool fixedPosition = false)
            {
                Rect returnRect = r;
                returnRect.width = fixedWidth ? width : width * r.width / 100;
                float positionAdjust = positionOffset == -1 ? 0 :
                    fixedPosition ? positionOffset : positionOffset * r.width / 100;
                returnRect.x = r.x + r.width - returnRect.width - positionAdjust;
                r.width -= returnRect.width + positionAdjust;
                return returnRect;
            }

            UseEnd(ref rect, 32, true, 4, true);
            Rect syncedRect = HasSyncingOption ? UseEnd(ref rect, 54, true) : Rect.zero;
            Rect savedRect = UseEnd(ref rect, 54, true);
            Rect defaultRect = UseEnd(ref rect, 117, true);
            Rect typeRect = UseEnd(ref rect, 75, true);
            UseEnd(ref rect, 48, true);
            Rect nameRect = UseNext(100);

            //guitest = EditorGUILayout.FloatField(guitest);

            Rect searchIconRect = nameRect;
            searchIconRect.x += searchIconRect.width / 2 - 40;
            searchIconRect.width = 18;
            Rect searchRect = Rect.zero;
            Rect searchClearRect = Rect.zero;

            #endregion

            const string controlName = "VRCSDKParameterSearch";
            if (Toolbox.HasReceivedCommand(Toolbox.EventCommands.Find))
                GUI.FocusControl(controlName);
            Toolbox.HandleTextFocusConfirmCommands(controlName,
                onCancel: () => _searchValue = string.Empty);
            bool isFocused = GUI.GetNameOfFocusedControl() == controlName;
            bool isSearching = isFocused || !string.IsNullOrEmpty(_searchValue);
            if (isSearching)
            {
                searchRect = nameRect;
                searchRect.x += 14;
                searchRect.width -= 14;
                searchClearRect = searchRect;
                searchClearRect.x += searchRect.width - 18;
                searchClearRect.y -= 1;
                searchClearRect.width = 16;
            }

            Helpers.w_MakeRectLinkCursor(searchIconRect);
            if (GUI.Button(searchIconRect, Toolbox.GUIContent.Search, VRCSDKPlus.CenteredLabel))
                EditorGUI.FocusTextInControl(controlName);

            GUI.Label(nameRect,
                new GUIContent("Name",
                    "Name of the Parameter. This must match the name of the parameter that it is controlling in the playable layers. Case sensitive."),
                VRCSDKPlus.CenteredLabel);


            Helpers.w_MakeRectLinkCursor(searchClearRect);
            if (GUI.Button(searchClearRect, string.Empty, GUIStyle.none))
            {
                _searchValue = string.Empty;
                if (isFocused) GUI.FocusControl(string.Empty);
            }

            GUI.SetNextControlName(controlName);
            _searchValue = GUI.TextField(searchRect, _searchValue, "SearchTextField");
            GUI.Button(searchClearRect, Toolbox.GUIContent.Clear, VRCSDKPlus.CenteredLabel);
            GUI.Label(typeRect, new GUIContent("Type", "Type of the Parameter."), VRCSDKPlus.CenteredLabel);
            GUI.Label(defaultRect, new GUIContent("Default", "The default/start value of this parameter."),
                VRCSDKPlus.CenteredLabel);
            GUI.Label(savedRect, new GUIContent("Saved", "Value will stay when loading avatar or changing worlds"),
                VRCSDKPlus.CenteredLabel);

            if (HasSyncingOption)
                GUI.Label(syncedRect,
                    new GUIContent("Synced",
                        "Value will be sent over the network to remote users. This is needed if this value should be the same locally and remotely. Synced parameters count towards the total memory usage."),
                    VRCSDKPlus.CenteredLabel);
        }

        private void HandleParameterEvents()
        {
            if (!_parametersOrderList.HasKeyboardControl()) return;
            if (!_parametersOrderList.TryGetActiveIndex(out int index)) return;
            if (Toolbox.HasReceivedCommand(Toolbox.EventCommands.Duplicate))
                DuplicateParameter(index);

            if (Toolbox.HasReceivedAnyDelete())
                DeleteParameter(index);
        }


        #region Automated Methods

        [MenuItem("CONTEXT/VRCExpressionParameters/[SDK+] Toggle Editor", false, 899)]
        private static void ToggleEditor()
        {
            _editorActive = !_editorActive;

            var targetType = Helpers.ExtendedGetType("VRCExpressionParameters");
            if (targetType == null)
            {
                Debug.LogError("[VRCSDK+] VRCExpressionParameters was not found! Could not apply custom editor.");
                return;
            }

            if (_editorActive) AutomatedMethods.OverrideEditor(targetType, typeof(VRCParamsPlus));
            else
            {
                var expressionsEditor = Helpers.ExtendedGetType("VRCExpressionParametersEditor");
                if (expressionsEditor == null)
                {
                    Debug.LogWarning(
                        "[VRCSDK+] VRCExpressionParametersEditor was not found! Could not apply custom editor");
                    return;
                }

                AutomatedMethods.OverrideEditor(targetType, expressionsEditor);
            }
        }

        private void RefreshAllParameterStatus()
        {
            var expressionParameters = (VRCExpressionParameters)target;
            if (expressionParameters.parameters == null)
            {
                expressionParameters.parameters = Array.Empty<VRCExpressionParameters.Parameter>();
                EditorUtility.SetDirty(expressionParameters);
            }

            var parameters = expressionParameters.parameters;
            _parameterStatus = new ParameterStatus[parameters.Length];

            for (int index = 0; index < parameters.Length; index++)
            {
                var exParameter = expressionParameters.parameters[index];
                AnimatorControllerParameter matchedParameter =
                    VRCSDKPlus.GetValidParameters().FirstOrDefault(conParam => conParam.name == exParameter.name);
                bool parameterEmpty = string.IsNullOrEmpty(exParameter.name);
                bool parameterIsValid = matchedParameter != null;
                bool parameterAddable = VRCSDKPlus.GetAvatarRef() && !parameterIsValid && !parameterEmpty;
                bool parameterIsDuplicate = !parameterEmpty && expressionParameters.parameters
                    .Where((p2, i) => index != i && exParameter.name == p2.name).Any();
                ;
                bool hasWarning = (VRCSDKPlus.GetAvatarRef() && !parameterIsValid) || parameterEmpty ||
                                  parameterIsDuplicate;
                _parameterStatus[index] = new ParameterStatus()
                {
                    ParameterEmpty = parameterEmpty,
                    ParameterAddable = parameterAddable,
                    ParameterIsDuplicate = parameterIsDuplicate,
                    HasWarning = hasWarning,
                    MatchedParameter = matchedParameter
                };
            }
        }

        private void CalculateTotalCost()
        {
            _currentCost = 0;
            for (int i = 0; i < _parameterList.arraySize; i++)
            {
                SerializedProperty p = _parameterList.GetArrayElementAtIndex(i);
                SerializedProperty synced = p.FindPropertyRelative("networkSynced");
                if (synced != null && !synced.boolValue) continue;
                _currentCost += p.FindPropertyRelative("valueType").enumValueIndex == 2 ? 1 : 8;
            }
        }

        private void RefreshParametersOrderList()
        {
            _parametersOrderList = new ReorderableList(serializedObject, _parameterList, true, true, true, false)
            {
                drawElementCallback = DrawElement,
                drawHeaderCallback = DrawHeader
            };
            _parametersOrderList.onReorderCallback += _ => RefreshAllParameterStatus();
            _parametersOrderList.onAddCallback = _ =>
            {
                _parameterList.InsertArrayElementAtIndex(_parameterList.arraySize);
                MakeParameterUnique(_parameterList.arraySize - 1);
            };
        }

        private void DuplicateParameter(int index)
        {
            _parameterList.InsertArrayElementAtIndex(index);
            MakeParameterUnique(index + 1);
            _parameterList.serializedObject.ApplyModifiedProperties();
            RefreshAllParameterStatus();
        }

        private void DeleteParameter(int index)
        {
            _parameterList.DeleteArrayElementAtIndex(index);
            _parameterList.serializedObject.ApplyModifiedProperties();
            RefreshAllParameterStatus();
        }

        private void MakeParameterUnique(int index)
        {
            var newElement = _parameterList.GetArrayElementAtIndex(index);
            var nameProp = newElement.FindPropertyRelative("name");
            nameProp.stringValue = Toolbox.GenerateUniqueString(nameProp.stringValue, newName =>
            {
                for (int i = 0; i < _parameterList.arraySize; i++)
                {
                    if (i == index) continue;
                    var p = _parameterList.GetArrayElementAtIndex(i);
                    if (p.FindPropertyRelative("name").stringValue == newName) return false;
                }

                return true;
            });
        }

        #endregion

        private struct ParameterStatus
        {
            internal bool ParameterEmpty;
            internal bool ParameterAddable;
            internal bool ParameterIsDuplicate;
            internal bool HasWarning;
            internal AnimatorControllerParameter MatchedParameter;
        }
    }
}