using System;
using UnityEngine;

namespace Spine.Unity.Examples {

    // Optional input backends belong to the examples, not the Spine runtime.
    public interface IExampleInput {
        Vector3 MousePosition { get; }
        float GetAxis(string name, bool raw);
        bool GetButton(string name, int phase);
        bool GetKey(KeyCode key, int phase);
    }

    public static class ExampleInput {
        public static IExampleInput Provider { get; set; }

        static IExampleInput Backend {
            get {
                if (Provider == null) throw new InvalidOperationException(
                    "Spine examples need the optional Input System adapter when legacy input is disabled.");
                return Provider;
            }
        }

        public static Vector3 mousePosition {
            get {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
                return Backend.MousePosition;
#else
                return UnityEngine.Input.mousePosition;
#endif
            }
        }
        public static float GetAxis(string name) { return ReadAxis(name, false); }
        public static float GetAxisRaw(string name) { return ReadAxis(name, true); }
        static float ReadAxis(string name, bool raw) {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            return Backend.GetAxis(name, raw);
#else
            return raw ? UnityEngine.Input.GetAxisRaw(name) : UnityEngine.Input.GetAxis(name);
#endif
        }
        public static bool GetButton(string name) { return ReadButton(name, 0); }
        public static bool GetButtonDown(string name) { return ReadButton(name, 1); }
        public static bool GetButtonUp(string name) { return ReadButton(name, 2); }
        static bool ReadButton(string name, int phase) {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            return Backend.GetButton(name, phase);
#else
            return phase == 0 ? UnityEngine.Input.GetButton(name)
                : phase == 1 ? UnityEngine.Input.GetButtonDown(name) : UnityEngine.Input.GetButtonUp(name);
#endif
        }
        public static bool GetKey(KeyCode key) { return ReadKey(key, 0); }
        public static bool GetKeyDown(KeyCode key) { return ReadKey(key, 1); }
        public static bool GetKeyUp(KeyCode key) { return ReadKey(key, 2); }
        static bool ReadKey(KeyCode key, int phase) {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            return Backend.GetKey(key, phase);
#else
            return phase == 0 ? UnityEngine.Input.GetKey(key)
                : phase == 1 ? UnityEngine.Input.GetKeyDown(key) : UnityEngine.Input.GetKeyUp(key);
#endif
        }
    }
}
