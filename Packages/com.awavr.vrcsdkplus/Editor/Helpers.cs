using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using Object = UnityEngine.Object;

namespace AwAVR.VRCSDKPlus
{
    internal sealed class Helpers
    {
        #region Clickables

        internal static bool ClickableButton(string label, GUIStyle style = null, params GUILayoutOption[] options) =>
            ClickableButton(new GUIContent(label), style, options);

        internal static bool ClickableButton(string label, params GUILayoutOption[] options) =>
            ClickableButton(new GUIContent(label), null, options);

        internal static bool ClickableButton(GUIContent label, params GUILayoutOption[] options) =>
            ClickableButton(label, null, options);

        internal static bool ClickableButton(GUIContent label, GUIStyle style = null, params GUILayoutOption[] options)
        {
            if (style == null)
                style = GUI.skin.button;
            bool clicked = GUILayout.Button(label, style, options);
            if (GUI.enabled) w_MakeRectLinkCursor();
            return clicked;
        }

        internal static void w_MakeRectLinkCursor(Rect rect = default)
        {
            if (!GUI.enabled) return;
            if (Event.current.type == EventType.Repaint)
            {
                if (rect == default) rect = GUILayoutUtility.GetLastRect();
                EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
            }
        }

        internal static bool w_MakeRectClickable(Rect rect = default)
        {
            if (rect == default) rect = GUILayoutUtility.GetLastRect();
            w_MakeRectLinkCursor(rect);
            var e = Event.current;
            return e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition);
        }

        #endregion

        private static void Link(string label, string url)
        {
            var bgcolor = GUI.backgroundColor;
            GUI.backgroundColor = Color.clear;

            if (GUILayout.Button(new GUIContent(label, url), Toolbox.Styles.Label.faintLinkLabel))
                Application.OpenURL(url);
            w_UnderlineLastRectOnHover();

            GUI.backgroundColor = bgcolor;
        }

        internal static void w_UnderlineLastRectOnHover(Color? color = null)
        {
            if (color == null) color = new Color(0.3f, 0.7f, 1);
            if (Event.current.type == EventType.Repaint)
            {
                var rect = GUILayoutUtility.GetLastRect();
                var mp = Event.current.mousePosition;
                if (rect.Contains(mp)) EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1, rect.width, 1), color.Value);
                EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
            }
        }

        internal static System.Type ExtendedGetType(string typeName)
        {
            var myType = System.Type.GetType(typeName);
            if (myType != null)
                return myType;
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var types = assembly.GetTypes();
                myType = types.FirstOrDefault(t => t.FullName == typeName);
                if (myType != null)
                    return myType;
                myType = types.FirstOrDefault(t => t.Name == typeName);
                if (myType != null)
                    return myType;
            }

            return null;
        }

        internal static void RefreshAvatar(ref VRCAvatarDescriptor avatar, ref List<VRCAvatarDescriptor> validAvatars,
            System.Action OnAvatarChanged = null, System.Func<VRCAvatarDescriptor, bool> favoredAvatar = null)
        {
            validAvatars = Core.GetAvatarsInScene();
            if (avatar) return;

            if (validAvatars.Count > 0)
            {
                if (favoredAvatar != null)
                    avatar = validAvatars.FirstOrDefault(favoredAvatar) ?? validAvatars[0];
                else avatar = validAvatars[0];
            }

            OnAvatarChanged?.Invoke();
        }

        internal static bool DrawAdvancedAvatarFull(ref VRCAvatarDescriptor avatar, VRCAvatarDescriptor[] validAvatars,
            System.Action OnAvatarChanged = null, bool warnNonHumanoid = true, bool warnPrefab = true,
            bool warnDoubleFX = true, string label = "Avatar", string tooltip = "The Targeted VRCAvatar",
            System.Action ExtraGUI = null)
            => DrawAdvancedAvatarField(ref avatar, validAvatars, OnAvatarChanged, label, tooltip, ExtraGUI) &&
               DrawAdvancedAvatarWarning(avatar, warnNonHumanoid, warnPrefab, warnDoubleFX);

        private static VRCAvatarDescriptor DrawAdvancedAvatarField(ref VRCAvatarDescriptor avatar,
            VRCAvatarDescriptor[] validAvatars, System.Action OnAvatarChanged = null, string label = "Avatar",
            string tooltip = "The Targeted VRCAvatar", System.Action ExtraGUI = null)
        {
            using (new GUILayout.HorizontalScope())
            {
                var avatarContent = new GUIContent(label, tooltip);
                if (validAvatars == null || validAvatars.Length <= 0)
                    EditorGUILayout.LabelField(avatarContent, new GUIContent("No Avatar Descriptors Found"));
                else
                {
                    using (var change = new EditorGUI.ChangeCheckScope())
                    {
                        int dummy = EditorGUILayout.Popup(avatarContent,
                            avatar ? Array.IndexOf(validAvatars, avatar) : -1,
                            validAvatars.Where(a => a).Select(x => x.name).ToArray());
                        if (change.changed)
                        {
                            avatar = validAvatars[dummy];
                            EditorGUIUtility.PingObject(avatar);
                            OnAvatarChanged?.Invoke();
                        }
                    }
                }

                ExtraGUI?.Invoke();
            }

            return avatar;
        }

        private static bool DrawAdvancedAvatarWarning(VRCAvatarDescriptor avatar, bool warnNonHumanoid = true,
            bool warnPrefab = true, bool warnDoubleFX = true)
        {
            return (!warnPrefab || !DrawPrefabWarning(avatar)) &&
                   (!warnDoubleFX || !DrawDoubleFXWarning(avatar, warnNonHumanoid));
        }

        private static bool DrawPrefabWarning(VRCAvatarDescriptor avatar)
        {
            if (!avatar) return false;
            bool isPrefab = PrefabUtility.IsPartOfAnyPrefab(avatar.gameObject);
            if (isPrefab)
            {
                EditorGUILayout.HelpBox("Target Avatar is a part of a prefab. Prefab unpacking is required.",
                    MessageType.Error);
                if (GUILayout.Button("Unpack"))
                    PrefabUtility.UnpackPrefabInstance(avatar.gameObject, PrefabUnpackMode.Completely,
                        InteractionMode.AutomatedAction);
            }

            return isPrefab;
        }

        private static bool DrawDoubleFXWarning(VRCAvatarDescriptor avatar, bool warnNonHumanoid = true)
        {
            if (!avatar) return false;
            var layers = avatar.baseAnimationLayers;

            if (layers.Length > 3)
            {
                var isDoubled = layers[3].type == layers[4].type;
                if (isDoubled)
                {
                    EditorGUILayout.HelpBox(
                        "Your Avatar's Action playable layer is set as FX. This is an uncommon bug.",
                        MessageType.Error);
                    if (GUILayout.Button("Fix"))
                    {
                        avatar.baseAnimationLayers[3].type = VRCAvatarDescriptor.AnimLayerType.Action;
                        EditorUtility.SetDirty(avatar);
                    }
                }

                return isDoubled;
            }

            if (warnNonHumanoid)
                EditorGUILayout.HelpBox(
                    "Your Avatar's descriptor is set as Non-Humanoid! Please make sure that your Avatar's rig is Humanoid.",
                    MessageType.Error);
            return warnNonHumanoid;
        }

        internal static void GreenLog(string msg) => Debug.Log($"<color=green>[VRCSDK+] </color>{msg}");
    }
}