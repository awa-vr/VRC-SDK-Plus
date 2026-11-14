using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using ValueType = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.ValueType;

namespace AwAVR.VRCSDKPlus {
    /// <summary>
    /// UI-free helpers for expression parameters, including the advanced options added in VRCSDK 3.10.6
    /// (custom lerp duration, bit width and signed/unsigned representation).
    /// Fields are looked up by name, so on older SDKs the advanced options are simply reported as unavailable.
    /// </summary>
    internal static class ParameterUtility {
        internal const string NameField = "name";
        internal const string ValueTypeField = "valueType";
        internal const string DefaultValueField = "defaultValue";
        internal const string SavedField = "saved";
        internal const string SyncedField = "networkSynced";
        internal const string BitWidthField = "bitWidth";
        internal const string FlippedRepresentationField = "flippedRepresentation";
        internal const string UseCustomLerpDurationField = "useCustomLerpDuration";
        internal const string CustomLerpDurationField = "customLerpDuration";

        internal const int DefaultBitWidth = 8;

        internal enum Representation {
            Unsigned,
            Signed
        }

        internal static readonly bool HasSyncingOption =
            typeof(VRCExpressionParameters.Parameter).GetField(SyncedField) != null;

        /// <summary>True when the installed VRCSDK supports bit width, representation and lerp duration (3.10.6+).</summary>
        internal static readonly bool HasAdvancedOptions =
            typeof(VRCExpressionParameters.Parameter).GetField(BitWidthField) != null &&
            typeof(VRCExpressionParameters.Parameter).GetField(FlippedRepresentationField) != null;

        internal static readonly bool HasLerpOptions =
            typeof(VRCExpressionParameters.Parameter).GetField(UseCustomLerpDurationField) != null &&
            typeof(VRCExpressionParameters.Parameter).GetField(CustomLerpDurationField) != null;

        internal static int MinBitWidth => GetSdkConstant("MinBitWidth", 1);
        internal static int MaxBitWidth => GetSdkConstant("MaxBitWidth", 16);
        internal static float MinLerpDuration => GetSdkConstant("MinLerpDuration", 0f);
        internal static float MaxLerpDuration => GetSdkConstant("MaxLerpDuration", 10f);

        private static int _maxMemoryCost;

        internal static int MaxMemoryCost {
            get {
                if (_maxMemoryCost != 0) return _maxMemoryCost;
                try {
                    _maxMemoryCost = (int)typeof(VRCExpressionParameters)
                        .GetField("MAX_PARAMETER_COST", BindingFlags.Static | BindingFlags.Public)
                        .GetValue(null);
                }
                catch {
                    Debug.LogError("[VRCSDK+] Failed to dynamically get MAX_PARAMETER_COST. Falling back to 256");
                    _maxMemoryCost = 256;
                }

                return _maxMemoryCost;
            }
        }

        private static T GetSdkConstant<T>(string fieldName, T fallback) {
            var field = typeof(VRCExpressionParameters.Parameter).GetField(fieldName,
                BindingFlags.Static | BindingFlags.Public);
            if (field == null) return fallback;
            try {
                return (T)Convert.ChangeType(field.GetValue(null), typeof(T));
            }
            catch {
                return fallback;
            }
        }

        internal static ValueType GetValueType(SerializedProperty parameter) =>
            (ValueType)parameter.FindPropertyRelative(ValueTypeField).intValue;

        internal static bool IsSynced(SerializedProperty parameter) {
            var synced = parameter.FindPropertyRelative(SyncedField);
            return synced == null || synced.boolValue;
        }

        /// <summary>
        /// Bits this parameter uses when synced, matching the VRCSDK's own cost calculation: bools always use 1, ints
        /// and floats are clamped to the allowed range. Older SDKs without bit widths always use 8.
        /// The inspector rewrites out of range widths to 8 first (see <see cref="NormalizeBitWidths"/>).
        /// </summary>
        internal static int GetBitWidth(SerializedProperty parameter) {
            if (GetValueType(parameter) == ValueType.Bool) return 1;
            var bitWidth = parameter.FindPropertyRelative(BitWidthField);
            if (bitWidth == null) return DefaultBitWidth;
            return Mathf.Clamp(bitWidth.intValue, MinBitWidth, MaxBitWidth);
        }

        internal static bool IsValidBitWidth(int bits) => bits >= MinBitWidth && bits <= MaxBitWidth;

        /// <summary>
        /// Gives bools 1 bit and ints/floats with an out of range width (e.g. 0 on an element added to an empty list)
        /// the default 8 bits, like the VRCSDK editor does when it shows a parameter. Returns true when something changed.
        /// </summary>
        internal static bool NormalizeBitWidths(SerializedProperty parameters) {
            if (!HasAdvancedOptions) return false;
            bool changed = false;
            for (int i = 0; i < parameters.arraySize; i++) {
                var parameter = parameters.GetArrayElementAtIndex(i);
                var bitWidth = parameter.FindPropertyRelative(BitWidthField);
                int expected = GetValueType(parameter) == ValueType.Bool ? 1
                    : IsValidBitWidth(bitWidth.intValue) ? bitWidth.intValue : DefaultBitWidth;
                if (bitWidth.intValue == expected) continue;
                bitWidth.intValue = expected;
                changed = true;
            }

            return changed;
        }

        /// <summary>Synced memory used by one parameter, 0 when it isn't synced.</summary>
        internal static int GetCost(SerializedProperty parameter) =>
            IsSynced(parameter) ? GetBitWidth(parameter) : 0;

        internal static int GetTotalCost(SerializedProperty parameters) {
            int cost = 0;
            for (int i = 0; i < parameters.arraySize; i++)
                cost += GetCost(parameters.GetArrayElementAtIndex(i));
            return cost;
        }

        /// <summary>Floats are signed by default and ints unsigned, "flippedRepresentation" inverts that.</summary>
        internal static Representation GetRepresentation(ValueType type, bool flipped) {
            switch (type) {
                case ValueType.Float: return flipped ? Representation.Unsigned : Representation.Signed;
                case ValueType.Int: return flipped ? Representation.Signed : Representation.Unsigned;
                default: return Representation.Unsigned;
            }
        }

        internal static bool IsFlipped(ValueType type, Representation representation) {
            switch (type) {
                case ValueType.Float: return representation == Representation.Unsigned;
                case ValueType.Int: return representation == Representation.Signed;
                default: return false;
            }
        }

        internal static Representation GetRepresentation(SerializedProperty parameter) {
            var flipped = parameter.FindPropertyRelative(FlippedRepresentationField);
            return GetRepresentation(GetValueType(parameter), flipped != null && flipped.boolValue);
        }

        internal static void GetIntRange(int bits, Representation representation, out long min, out long max) {
            bits = Mathf.Clamp(bits, 1, 32);
            if (representation == Representation.Signed) {
                min = -(1L << (bits - 1));
                max = (1L << (bits - 1)) - 1;
            }
            else {
                min = 0;
                max = (1L << bits) - 1;
            }
        }

        internal static void GetFloatRange(Representation representation, out float min, out float max) {
            min = representation == Representation.Signed ? -1f : 0f;
            max = 1f;
        }

        /// <summary>The range (and precision for floats) shown next to the advanced options.</summary>
        internal static string GetInfo(SerializedProperty parameter) {
            var type = GetValueType(parameter);
            var bits = GetBitWidth(parameter);
            var representation = GetRepresentation(parameter);
            switch (type) {
                case ValueType.Bool:
                    return "True or False";
                case ValueType.Float: {
                    GetFloatRange(representation, out var min, out var max);
                    long intervals = Math.Max(1, (1L << bits) - 2);
                    return $"Range: [{min},{max}]  Precision: {(max - min) / intervals:G4}";
                }
                default: {
                    GetIntRange(bits, representation, out var min, out var max);
                    return $"Range: [{min},{max}]";
                }
            }
        }

        /// <summary>Keeps the default value inside the range allowed by the type, bit width and representation.</summary>
        internal static float ClampDefault(SerializedProperty parameter, float value) {
            var representation = GetRepresentation(parameter);
            switch (GetValueType(parameter)) {
                case ValueType.Bool:
                    return value != 0 ? 1 : 0;
                case ValueType.Float: {
                    GetFloatRange(representation, out var min, out var max);
                    return Mathf.Clamp(value, min, max);
                }
                default: {
                    GetIntRange(GetBitWidth(parameter), representation, out var min, out var max);
                    return Mathf.Clamp(Mathf.Round(value), min, max);
                }
            }
        }

        /// <summary>
        /// What the VRCSDK editor does when a parameter's type changes: 1 bit for bools, 8 for ints and floats,
        /// the type's default representation, and no custom lerp for bools (they never lerp).
        /// </summary>
        internal static void ApplyTypeDefaults(SerializedProperty parameter) {
            var type = GetValueType(parameter);
            var bitWidth = parameter.FindPropertyRelative(BitWidthField);
            if (bitWidth != null) bitWidth.intValue = type == ValueType.Bool ? 1 : DefaultBitWidth;

            var flipped = parameter.FindPropertyRelative(FlippedRepresentationField);
            if (flipped != null) flipped.boolValue = false;

            if (type == ValueType.Bool) {
                var useLerp = parameter.FindPropertyRelative(UseCustomLerpDurationField);
                if (useLerp != null) useLerp.boolValue = false;
            }

            var defaultValue = parameter.FindPropertyRelative(DefaultValueField);
            defaultValue.floatValue = ClampDefault(parameter, defaultValue.floatValue);
        }

        /// <summary>Sets up a parameter added from VRCSDK+ (menu "Add" button, add-from-animator) with sane defaults.</summary>
        internal static void InitializeNewParameter(SerializedProperty parameter, string name, ValueType type) {
            parameter.FindPropertyRelative(NameField).stringValue = name;
            parameter.FindPropertyRelative(ValueTypeField).intValue = (int)type;
            parameter.FindPropertyRelative(DefaultValueField).floatValue = 0;
            parameter.FindPropertyRelative(SavedField).boolValue = true;
            var synced = parameter.FindPropertyRelative(SyncedField);
            if (synced != null) synced.boolValue = true;
            var useLerp = parameter.FindPropertyRelative(UseCustomLerpDurationField);
            if (useLerp != null) useLerp.boolValue = false;
            ApplyTypeDefaults(parameter);
        }

        /// <summary>A full copy of a parameter, including fields this version of VRCSDK+ doesn't know about.</summary>
        internal static VRCExpressionParameters.Parameter Clone(VRCExpressionParameters.Parameter parameter) =>
            JsonUtility.FromJson<VRCExpressionParameters.Parameter>(JsonUtility.ToJson(parameter));
    }
}
