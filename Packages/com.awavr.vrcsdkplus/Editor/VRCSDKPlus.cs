using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using AnimatorController = UnityEditor.Animations.AnimatorController;
using AnimatorControllerParameter = UnityEngine.AnimatorControllerParameter;

namespace AwAVR.VRCSDKPlus
{
    internal sealed class VRCSDKPlus
    {
        private static bool _initialized;
        private static GUIContent _redWarnIcon;
        private static GUIContent _yellowWarnIcon;
        internal static GUIStyle CenteredLabel => new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };

        private static readonly string[] AllPlayables =
        {
            "Base",
            "Additive",
            "Gesture",
            "Action",
            "FX",
            "Sitting",
            "TPose",
            "IKPose"
        };

        private static VRCAvatarDescriptor _avatar;
        private static List<VRCAvatarDescriptor> _validAvatars;
        private static AnimatorControllerParameter[] _validParameters;

        private static string[] _validPlayables;
        private static int[] _validPlayableIndexes;

        internal static void InitConstants()
        {
            if (_initialized) return;
            _redWarnIcon = new GUIContent(EditorGUIUtility.IconContent("CollabError"));
            //advancedPopupMethod = typeof(EditorGUI).GetMethod("AdvancedPopup", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(Rect), typeof(int), typeof(string[]) }, null);
            _yellowWarnIcon = new GUIContent(EditorGUIUtility.IconContent("d_console.warnicon.sml"));
            _initialized = true;
        }

        internal static void RefreshAvatar(System.Func<VRCAvatarDescriptor, bool> favoredAvatar = null)
        {
            Helpers.RefreshAvatar(ref _avatar, ref _validAvatars, null, favoredAvatar);
            RefreshAvatarInfo();
        }

        private static void RefreshAvatarInfo()
        {
            RefreshValidParameters();
            RefreshValidPlayables();
        }

        internal static void RefreshValidParameters()
        {
            if (!_avatar)
            {
                _validParameters = Array.Empty<AnimatorControllerParameter>();
                return;
            }

            List<AnimatorControllerParameter> validParams = new List<AnimatorControllerParameter>();
            foreach (var r in _avatar.baseAnimationLayers.Concat(_avatar.specialAnimationLayers)
                         .Select(p => p.animatorController).Concat(_avatar.GetComponentsInChildren<Animator>(true)
                             .Select(a => a.runtimeAnimatorController)).Distinct())
            {
                if (!r) continue;

                AnimatorController c = AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GetAssetPath(r));
                if (c) validParams.AddRange(c.parameters);
            }

            _validParameters = validParams.Distinct().OrderBy(p => p.name).ToArray();
        }

        internal static void RefreshValidPlayables()
        {
            if (!_avatar)
            {
                _validPlayables = Array.Empty<string>();
                _validPlayableIndexes = Array.Empty<int>();
                return;
            }

            List<(string, int)> myPlayables = new List<(string, int)>();
            for (int i = 0; i < AllPlayables.Length; i++)
            {
                int index = i == 0 ? i : i + 1;
                if (_avatar.GetPlayableLayer((VRCAvatarDescriptor.AnimLayerType)index, out AnimatorController c))
                {
                    myPlayables.Add((AllPlayables[i], index));
                }
            }

            _validPlayables = new string[myPlayables.Count];
            _validPlayableIndexes = new int[myPlayables.Count];
            for (int i = 0; i < myPlayables.Count; i++)
            {
                _validPlayables[i] = myPlayables[i].Item1;
                _validPlayableIndexes[i] = myPlayables[i].Item2;
            }
        }

        // TODO: Fix avatar list not getting updated when avatars get enabled or disabled in the scene.
        internal static VRCAvatarDescriptor GetAvatar() => _avatar;
        internal static ref VRCAvatarDescriptor GetAvatarRef() => ref _avatar;
        internal static List<VRCAvatarDescriptor> GetValidAvatars() => _validAvatars;
        internal static ref List<VRCAvatarDescriptor> GetValidAvatarsRef() => ref _validAvatars;
        internal static AnimatorControllerParameter[] GetValidParameters() => _validParameters;
        internal static string[] GetValidPlayables() => _validPlayables;
        internal static int[] GetValidPlayableIndexes() => _validPlayableIndexes;
        internal static GUIContent GetRedWarnIcon() => _redWarnIcon;
        internal static GUIContent GetYellowWarnIcon() => _yellowWarnIcon;
    }
}