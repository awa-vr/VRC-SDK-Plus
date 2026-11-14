using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using AnimatorController = UnityEditor.Animations.AnimatorController;
using AnimatorControllerParameter = UnityEngine.AnimatorControllerParameter;

namespace AwAVR.VRCSDKPlus {
    /// <summary>
    /// State shared by the parameters and menu inspectors: the targeted avatar and what its animators contain.
    /// </summary>
    internal static class VRCSDKPlus {
        private static readonly string[] AllPlayables = {
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
        private static AnimatorControllerParameter[] _validParameters = Array.Empty<AnimatorControllerParameter>();
        private static string[] _validPlayables = Array.Empty<string>();
        private static int[] _validPlayableIndexes = Array.Empty<int>();

        /// <summary>Raised when the targeted avatar changes or its animators were re-scanned.</summary>
        internal static event Action AvatarInfoChanged;

        internal static VRCAvatarDescriptor GetAvatar() => _avatar ? _avatar : null;

        internal static void SetAvatar(VRCAvatarDescriptor avatar) {
            if (_avatar == avatar) return;
            _avatar = avatar;
            RefreshAvatarInfo();
        }

        /// <summary>
        /// Keeps the current avatar if it still exists, otherwise picks <paramref name="favoredAvatar"/> or the first one in the scene.
        /// </summary>
        internal static void RefreshAvatar(Func<VRCAvatarDescriptor, bool> favoredAvatar = null) {
            if (!_avatar) {
                var avatars = Core.GetAvatarsInScene() ?? new List<VRCAvatarDescriptor>();
                _avatar = (favoredAvatar != null ? avatars.FirstOrDefault(favoredAvatar) : null) ??
                          avatars.FirstOrDefault();
            }

            RefreshAvatarInfo();
        }

        public static void RefreshAvatarInfo() {
            RefreshValidParameters();
            RefreshValidPlayables();
            AvatarInfoChanged?.Invoke();
        }

        private static void RefreshValidParameters() {
            if (!_avatar) {
                _validParameters = Array.Empty<AnimatorControllerParameter>();
                return;
            }

            var validParams = new List<AnimatorControllerParameter>();
            foreach (var r in _avatar.baseAnimationLayers.Concat(_avatar.specialAnimationLayers)
                         .Select(p => p.animatorController).Concat(_avatar.GetComponentsInChildren<Animator>(true)
                             .Select(a => a.runtimeAnimatorController)).Distinct()) {
                if (!r) continue;

                var c = AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GetAssetPath(r));
                if (c) validParams.AddRange(c.parameters);
            }

            // Several animators often share a parameter, only list each name once.
            _validParameters = validParams.GroupBy(p => p.name).Select(g => g.First()).OrderBy(p => p.name).ToArray();
        }

        private static void RefreshValidPlayables() {
            if (!_avatar) {
                _validPlayables = Array.Empty<string>();
                _validPlayableIndexes = Array.Empty<int>();
                return;
            }

            var myPlayables = new List<(string, int)>();
            for (int i = 0; i < AllPlayables.Length; i++) {
                int index = i == 0 ? i : i + 1;
                if (_avatar.GetPlayableLayer((VRCAvatarDescriptor.AnimLayerType)index, out _)) {
                    myPlayables.Add((AllPlayables[i], index));
                }
            }

            _validPlayables = myPlayables.Select(p => p.Item1).ToArray();
            _validPlayableIndexes = myPlayables.Select(p => p.Item2).ToArray();
        }

        internal static AnimatorControllerParameter[] GetValidParameters() => _validParameters;
        internal static string[] GetValidPlayables() => _validPlayables;
        internal static int[] GetValidPlayableIndexes() => _validPlayableIndexes;
    }
}
