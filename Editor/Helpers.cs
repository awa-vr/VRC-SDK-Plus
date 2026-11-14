using System.Linq;
using UnityEngine;

namespace AwAVR.VRCSDKPlus {
    internal static class Helpers {
        internal static System.Type ExtendedGetType(string typeName) {
            var myType = System.Type.GetType(typeName);
            if (myType != null)
                return myType;
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies()) {
                System.Type[] types;
                try {
                    types = assembly.GetTypes();
                }
                catch (System.Reflection.ReflectionTypeLoadException e) {
                    types = e.Types.Where(t => t != null).ToArray();
                }

                myType = types.FirstOrDefault(t => t.FullName == typeName);
                if (myType != null)
                    return myType;
                myType = types.FirstOrDefault(t => t.Name == typeName);
                if (myType != null)
                    return myType;
            }

            return null;
        }

        internal static void GreenLog(string msg) => Debug.Log($"<color=green>[VRCSDK+] </color>{msg}");
    }
}
