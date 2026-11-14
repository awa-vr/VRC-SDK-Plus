using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.ScriptableObjects;
using Object = UnityEngine.Object;

namespace AwAVR.VRCSDKPlus {
    /// <summary>
    /// Browser-style back/forward history of visited expression menus. Each inspector window gets its own history
    /// (see <see cref="ForWindow"/>), so two inspectors no longer fight over one shared list.
    /// </summary>
    internal sealed class MenuHistory {
        internal const int MaxEntries = 64;

        private static readonly Dictionary<int, MenuHistory> Histories = new Dictionary<int, MenuHistory>();

        private readonly List<VRCExpressionsMenu> _entries = new List<VRCExpressionsMenu>();
        private int _index = -1;

        internal IReadOnlyList<VRCExpressionsMenu> Entries {
            get {
                Prune();
                return _entries;
            }
        }

        internal int Index {
            get {
                Prune();
                return _index;
            }
        }

        internal VRCExpressionsMenu Current {
            get {
                Prune();
                return _index >= 0 ? _entries[_index] : null;
            }
        }

        internal bool CanGoBack => Index > 0;
        internal bool CanGoForward => Index >= 0 && _index < _entries.Count - 1;

        /// <summary>
        /// Records that <paramref name="menu"/> is now shown. Re-visiting the current entry does nothing, visiting the
        /// entry right before or after the current one moves there (so selecting it by other means acts like back/forward),
        /// anything else drops the forward entries and is appended, like a browser.
        /// </summary>
        internal void Visit(VRCExpressionsMenu menu) {
            if (!menu) return;
            Prune();

            if (_index >= 0 && _entries[_index] == menu) return;
            if (_index + 1 < _entries.Count && _entries[_index + 1] == menu) {
                _index++;
                return;
            }

            if (_index - 1 >= 0 && _entries[_index - 1] == menu) {
                _index--;
                return;
            }

            if (_index + 1 < _entries.Count)
                _entries.RemoveRange(_index + 1, _entries.Count - _index - 1);

            _entries.Add(menu);
            _index = _entries.Count - 1;

            if (_entries.Count > MaxEntries) {
                int overflow = _entries.Count - MaxEntries;
                _entries.RemoveRange(0, overflow);
                _index -= overflow;
            }
        }

        internal VRCExpressionsMenu Back() => GoTo(Index - 1);
        internal VRCExpressionsMenu Forward() => GoTo(Index + 1);
        internal VRCExpressionsMenu First() => GoTo(0);
        internal VRCExpressionsMenu Last() => GoTo(Entries.Count - 1);

        /// <summary>Moves to an entry and returns the menu to show, or null if the index is out of range.</summary>
        internal VRCExpressionsMenu GoTo(int index) {
            Prune();
            if (index < 0 || index >= _entries.Count) return null;
            _index = index;
            return _entries[_index];
        }

        /// <summary>Removes deleted menus and keeps the current position pointing at the same menu.</summary>
        private void Prune() {
            for (int i = _entries.Count - 1; i >= 0; i--) {
                if (_entries[i]) continue;
                _entries.RemoveAt(i);
                if (i <= _index) _index--;
            }

            // Collapse neighbours that became identical after a removal (A, deleted, A -> A).
            for (int i = _entries.Count - 1; i > 0; i--) {
                if (_entries[i] != _entries[i - 1]) continue;
                _entries.RemoveAt(i);
                if (i <= _index) _index--;
            }

            if (_entries.Count == 0) _index = -1;
            else _index = Mathf.Clamp(_index, 0, _entries.Count - 1);
        }

        internal static MenuHistory ForWindow(EditorWindow window) {
            int key = window ? window.GetInstanceID() : 0;
            if (Histories.TryGetValue(key, out var history)) return history;

            // Forget windows that were closed.
            foreach (var closed in Histories.Keys.Where(k => k != 0 && !EditorUtility.InstanceIDToObject(k)).ToList())
                Histories.Remove(closed);

            history = new MenuHistory();
            Histories[key] = history;
            return history;
        }

        /// <summary>The editor window (normally an Inspector) that contains <paramref name="element"/>.</summary>
        internal static EditorWindow FindHostWindow(VisualElement element) {
            if (element?.panel == null) return null;
            foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>()) {
                VisualElement root;
                try {
                    root = window.rootVisualElement;
                }
                catch {
                    continue;
                }

                if (root != null && root.panel == element.panel && root.Contains(element)) return window;
            }

            // Fall back to any window sharing the panel (e.g. tabs docked together).
            return Resources.FindObjectsOfTypeAll<EditorWindow>()
                .FirstOrDefault(w => {
                    try {
                        return w.rootVisualElement?.panel == element.panel;
                    }
                    catch {
                        return false;
                    }
                });
        }

        #region Navigation

        private static readonly Type InspectorWindowType =
            Type.GetType("UnityEditor.InspectorWindow, UnityEditor");

        /// <summary>
        /// Shows <paramref name="menu"/> in <paramref name="window"/>. A locked inspector stays locked and switches to the
        /// menu itself, an unlocked one (or any other window) changes the selection.
        /// </summary>
        internal static void Open(EditorWindow window, VRCExpressionsMenu menu) {
            if (!menu) return;
            if (window && IsLockedInspector(window) && TrySetLockedObject(window, menu)) return;
            Selection.activeObject = menu;
        }

        private static bool IsLockedInspector(EditorWindow window) {
            if (InspectorWindowType == null || !InspectorWindowType.IsInstanceOfType(window)) return false;
            var property = FindMember<PropertyInfo>(window.GetType(), "isLocked");
            try {
                return property != null && (bool)property.GetValue(window);
            }
            catch {
                return false;
            }
        }

        private static bool TrySetLockedObject(EditorWindow window, Object obj) {
            var method = FindMember<MethodInfo>(window.GetType(), "SetObjectsLocked");
            if (method == null) return false;
            try {
                method.Invoke(window, new object[] { new List<Object> { obj } });
                // The locked objects only show up once the window's editor tracker rebuilds.
                if (FindMember<PropertyInfo>(window.GetType(), "tracker")?.GetValue(window) is ActiveEditorTracker tracker)
                    tracker.ForceRebuild();
                window.Repaint();
                return true;
            }
            catch (Exception e) {
                Debug.LogWarning($"[VRCSDK+] Couldn't change the locked inspector, changing the selection instead.\n{e}");
                return false;
            }
        }

        private static T FindMember<T>(Type type, string name) where T : MemberInfo {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            for (var t = type; t != null; t = t.BaseType) {
                var member = t.GetMember(name, flags).OfType<T>().FirstOrDefault();
                if (member != null) return member;
            }

            return null;
        }

        #endregion
    }
}
