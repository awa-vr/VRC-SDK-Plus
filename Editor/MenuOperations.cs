using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using Control = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control;
using ControlType = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control.ControlType;

namespace AwAVR.VRCSDKPlus {
    /// <summary>
    /// UI-free control operations for expression menus: copy/paste, duplicate, delete and cut/place (move).
    /// They edit the menu assets directly with Undo, so callers should apply pending SerializedObject changes first
    /// and call <c>serializedObject.Update()</c> afterwards.
    /// </summary>
    internal static class MenuOperations {
        private static int _maxControls;

        internal static int MaxControls {
            get {
                if (_maxControls != 0) return _maxControls;
                var field = typeof(VRCExpressionsMenu).GetField("MAX_CONTROLS", BindingFlags.Static | BindingFlags.Public);
                _maxControls = field != null ? (int)field.GetValue(null) : 8;
                return _maxControls;
            }
        }

        internal static List<Control> GetControls(VRCExpressionsMenu menu) {
            if (menu.controls == null) menu.controls = new List<Control>();
            return menu.controls;
        }

        internal static bool IsFull(VRCExpressionsMenu menu) => GetControls(menu).Count >= MaxControls;

        internal static Control Clone(Control control) =>
            JsonUtility.FromJson<Control>(JsonUtility.ToJson(control));

        internal static Control CreateDefaultControl() => new Control {
            name = "New Control",
            type = ControlType.Toggle,
            value = 1,
            parameter = new Control.Parameter { name = string.Empty },
            subParameters = new Control.Parameter[0],
            labels = new Control.Label[0]
        };

        /// <summary>Where a new control goes: after the selected one, or at the end when nothing is selected.</summary>
        internal static int InsertIndexAfter(VRCExpressionsMenu menu, int selectedIndex) {
            var count = GetControls(menu).Count;
            return selectedIndex < 0 || selectedIndex >= count ? count : selectedIndex + 1;
        }

        #region Add / Delete / Duplicate

        internal static bool Add(VRCExpressionsMenu menu, int insertIndex, out int addedIndex) {
            addedIndex = -1;
            if (IsFull(menu)) return Warn(Toolbox.Strings.MenuFullTooltip);
            Undo.RecordObject(menu, "Add control");
            var controls = GetControls(menu);
            addedIndex = Mathf.Clamp(insertIndex, 0, controls.Count);
            controls.Insert(addedIndex, CreateDefaultControl());
            EditorUtility.SetDirty(menu);
            return true;
        }

        internal static bool Delete(VRCExpressionsMenu menu, int index) {
            var controls = GetControls(menu);
            if (index < 0 || index >= controls.Count) return false;
            Undo.RecordObject(menu, "Delete control");
            bool wasCut = IsMovingControl(menu, index);
            controls.RemoveAt(index);
            EditorUtility.SetDirty(menu);
            if (wasCut) CancelMove();
            else if (menu == _moveSource && index < _moveIndex) {
                _moveIndex--;
                _moveCount--;
            }

            return true;
        }

        /// <summary>
        /// Duplicates the control after itself. With <paramref name="smart"/> (the default, hold Shift to skip it),
        /// a toggle/button driving a bool gets the next numbered parameter, or the next value for an int.
        /// </summary>
        internal static bool Duplicate(VRCExpressionsMenu menu, int index, VRCExpressionParameters parameters,
            bool smart, out int newIndex) {
            newIndex = -1;
            var controls = GetControls(menu);
            if (index < 0 || index >= controls.Count) return false;
            if (IsFull(menu)) return Warn(Toolbox.Strings.MenuFullTooltip);

            Undo.RecordObject(menu, "Duplicate control");
            var copy = Clone(controls[index]);
            var lastName = copy.name ?? string.Empty;
            copy.name = Toolbox.GenerateUniqueString(lastName, newName => newName != lastName, false);

            if (smart && copy.parameter != null && !string.IsNullOrEmpty(copy.parameter.name) && parameters &&
                (copy.type == ControlType.Button || copy.type == ControlType.Toggle)) {
                var parName = copy.parameter.name;
                var matched = parameters.parameters == null ? null : System.Array.Find(parameters.parameters, p => p.name == parName);
                if (matched != null) {
                    if (matched.valueType == VRCExpressionParameters.ValueType.Bool)
                        copy.parameter.name = Toolbox.GenerateUniqueString(parName, s => s != parName, false);
                    else if (Mathf.Approximately(Mathf.Round(copy.value), copy.value))
                        copy.value++;
                }
            }

            newIndex = index + 1;
            controls.Insert(newIndex, copy);
            EditorUtility.SetDirty(menu);
            return true;
        }

        #endregion

        #region Clipboard

        internal static void Copy(VRCExpressionsMenu menu, int index) {
            var controls = GetControls(menu);
            if (index < 0 || index >= controls.Count) return;
            EditorGUIUtility.systemCopyBuffer = Toolbox.Strings.ClipboardPrefixControl + JsonUtility.ToJson(controls[index]);
        }

        internal static bool CanPaste() {
            var buffer = EditorGUIUtility.systemCopyBuffer;
            return !string.IsNullOrEmpty(buffer) && buffer.StartsWith(Toolbox.Strings.ClipboardPrefixControl);
        }

        private static Control ReadClipboard() {
            if (!CanPaste()) return null;
            try {
                return JsonUtility.FromJson<Control>(
                    EditorGUIUtility.systemCopyBuffer.Substring(Toolbox.Strings.ClipboardPrefixControl.Length));
            }
            catch (System.Exception e) {
                Debug.LogWarning($"[VRCSDK+] Couldn't read the copied control: {e.Message}");
                return null;
            }
        }

        /// <summary>Replaces the control at <paramref name="index"/> with the copied one.</summary>
        internal static bool PasteValues(VRCExpressionsMenu menu, int index) {
            var controls = GetControls(menu);
            if (index < 0 || index >= controls.Count) return false;
            var control = ReadClipboard();
            if (control == null) return false;
            Undo.RecordObject(menu, "Paste control values");
            controls[index] = control;
            EditorUtility.SetDirty(menu);
            return true;
        }

        internal static bool PasteAsNew(VRCExpressionsMenu menu, int insertIndex, out int pastedIndex) {
            pastedIndex = -1;
            if (IsFull(menu)) return Warn(Toolbox.Strings.MenuFullTooltip);
            var control = ReadClipboard();
            if (control == null) return false;
            Undo.RecordObject(menu, "Insert control as new");
            var controls = GetControls(menu);
            pastedIndex = Mathf.Clamp(insertIndex, 0, controls.Count);
            controls.Insert(pastedIndex, control);
            EditorUtility.SetDirty(menu);
            return true;
        }

        #endregion

        #region Move (cut / place)

        // The cut control is remembered by reference, by a JSON snapshot and by index, because the reference alone
        // goes stale when the menu is re-deserialized (undo/redo, domain reload) while a control is cut.
        private static VRCExpressionsMenu _moveSource;
        private static Control _moveControl;
        private static string _moveJson;
        private static int _moveIndex = -1;
        private static int _moveCount;

        /// <summary>True while a control has been cut and not placed yet, and it can still be found in its menu.</summary>
        internal static bool IsMoving => FindMoveIndex() >= 0;

        internal static VRCExpressionsMenu MoveSource => IsMoving ? _moveSource : null;

        internal static bool IsMovingControl(VRCExpressionsMenu menu, int index) =>
            menu && menu == _moveSource && index >= 0 && FindMoveIndex() == index;

        private static int FindMoveIndex() {
            if (!_moveSource || _moveJson == null) {
                CancelMove();
                return -1;
            }

            var controls = GetControls(_moveSource);
            bool Matches(Control c) => ReferenceEquals(c, _moveControl) || JsonUtility.ToJson(c) == _moveJson;

            if (_moveIndex >= 0 && _moveIndex < controls.Count && Matches(controls[_moveIndex])) return _moveIndex;
            for (int i = 0; i < controls.Count; i++) {
                if (!Matches(controls[i])) continue;
                _moveIndex = i;
                return i;
            }

            // The cut control was edited afterwards: still the same slot as long as nothing was added or removed.
            if (controls.Count == _moveCount && _moveIndex >= 0 && _moveIndex < controls.Count) return _moveIndex;

            CancelMove();
            return -1;
        }

        internal static void BeginMove(VRCExpressionsMenu menu, int index) {
            var controls = GetControls(menu);
            if (index < 0 || index >= controls.Count) return;
            _moveSource = menu;
            _moveControl = controls[index];
            _moveJson = JsonUtility.ToJson(controls[index]);
            _moveIndex = index;
            _moveCount = controls.Count;
        }

        internal static void CancelMove() {
            _moveSource = null;
            _moveControl = null;
            _moveJson = null;
            _moveIndex = -1;
            _moveCount = 0;
        }

        /// <summary>
        /// Moves the cut control to <paramref name="insertIndex"/> in <paramref name="target"/> and always removes it from
        /// where it came from, also when the target is empty or the same menu.
        /// </summary>
        internal static bool Place(VRCExpressionsMenu target, int insertIndex, out int placedIndex) {
            placedIndex = -1;
            int sourceIndex = FindMoveIndex();
            if (!target || sourceIndex < 0) return false;

            var source = _moveSource;
            var sourceControls = GetControls(source);
            var targetControls = GetControls(target);
            var control = sourceControls[sourceIndex];

            if (source == target) {
                Undo.RecordObject(target, "Move control");
                sourceControls.RemoveAt(sourceIndex);
                // Removing the control shifts everything after it one slot up.
                if (insertIndex > sourceIndex) insertIndex--;
                placedIndex = Mathf.Clamp(insertIndex, 0, sourceControls.Count);
                sourceControls.Insert(placedIndex, control);
                EditorUtility.SetDirty(target);
            }
            else {
                if (IsFull(target)) return Warn(Toolbox.Strings.MenuFullTooltip);
                Undo.RecordObjects(new Object[] { target, source }, "Move control");
                sourceControls.RemoveAt(sourceIndex);
                placedIndex = Mathf.Clamp(insertIndex, 0, targetControls.Count);
                targetControls.Insert(placedIndex, control);
                EditorUtility.SetDirty(source);
                EditorUtility.SetDirty(target);
            }

            CancelMove();
            return true;
        }

        #endregion

        private static bool Warn(string message) {
            Debug.LogWarning($"[VRCSDK+] {message}");
            return false;
        }
    }
}
