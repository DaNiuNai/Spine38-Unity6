#if ENABLE_INPUT_SYSTEM
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace Spine.Unity.Examples {
    public sealed class ExampleInputSystem : IExampleInput, IDisposable {
        readonly InputActionMap actions = new InputActionMap("Spine Examples");
        readonly Dictionary<string, float> smoothed = new Dictionary<string, float>();
        readonly Dictionary<string, int> smoothFrames = new Dictionary<string, int>();
        static ExampleInputSystem instance;

        public ExampleInputSystem() {
            AddAxis("Horizontal", "a", "d", "leftArrow", "rightArrow", "x");
            AddAxis("Vertical", "s", "w", "downArrow", "upArrow", "y");
            AddButton("Jump", "<Keyboard>/space", "<Gamepad>/buttonSouth");
            AddButton("Fire1", "<Mouse>/leftButton", "<Keyboard>/leftCtrl", "<Gamepad>/buttonWest");
            AddButton("Fire2", "<Mouse>/rightButton", "<Keyboard>/leftAlt", "<Gamepad>/buttonEast");
            AddButton("Fire3", "<Mouse>/middleButton", "<Keyboard>/leftShift", "<Gamepad>/buttonNorth");
            AddButton("Submit", "<Keyboard>/enter", "<Gamepad>/buttonSouth");
            AddButton("Cancel", "<Keyboard>/escape", "<Gamepad>/buttonEast");
            actions.Enable();
        }

        void AddAxis(string name, string negative, string positive, string negativeArrow, string positiveArrow, string axis) {
            var action = actions.AddAction(name, InputActionType.Value, expectedControlLayout: "Axis");
            action.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/" + negative).With("Positive", "<Keyboard>/" + positive);
            action.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/" + negativeArrow).With("Positive", "<Keyboard>/" + positiveArrow);
            action.AddBinding("<Gamepad>/leftStick/" + axis);
            action.AddBinding("<Gamepad>/dpad/" + axis);
        }
        void AddButton(string name, params string[] bindings) {
            var action = actions.AddAction(name, InputActionType.Button);
            foreach (var binding in bindings) action.AddBinding(binding);
        }

        public Vector3 MousePosition {
            get {
                if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
                    return Touchscreen.current.primaryTouch.position.ReadValue();
                return Mouse.current == null ? Vector3.zero : (Vector3)Mouse.current.position.ReadValue();
            }
        }

        public float GetAxis(string name, bool raw) {
            var value = actions.FindAction(name, true).ReadValue<float>();
            if (raw) return value;
            int frame;
            float previous;
            smoothed.TryGetValue(name, out previous);
            if (smoothFrames.TryGetValue(name, out frame) && frame == Time.frameCount) return previous;
            if (value * previous < 0) previous = 0;
            smoothed[name] = Mathf.MoveTowards(previous, value, 3f * Time.unscaledDeltaTime);
            smoothFrames[name] = Time.frameCount;
            return smoothed[name];
        }
        public bool GetButton(string name, int phase) {
            var action = actions.FindAction(name, true);
            return phase == 0 ? action.IsPressed() : phase == 1 ? action.WasPressedThisFrame() : action.WasReleasedThisFrame();
        }

        public bool GetKey(KeyCode code, int phase) {
            ButtonControl control = null;
            if (code >= KeyCode.Mouse0 && code <= KeyCode.Mouse4 && Mouse.current != null) {
                switch (code) {
                    case KeyCode.Mouse0: control = Mouse.current.leftButton; break;
                    case KeyCode.Mouse1: control = Mouse.current.rightButton; break;
                    case KeyCode.Mouse2: control = Mouse.current.middleButton; break;
                    case KeyCode.Mouse3: control = Mouse.current.backButton; break;
                    case KeyCode.Mouse4: control = Mouse.current.forwardButton; break;
                }
            } else if (Keyboard.current != null && code != KeyCode.None) {
                var name = code.ToString();
                if (name.StartsWith("Alpha", StringComparison.Ordinal)) name = "Digit" + name.Substring(5);
                else if (name.StartsWith("Keypad", StringComparison.Ordinal)) name = "Numpad" + name.Substring(6);
                else if (code == KeyCode.Return) name = "Enter";
                else if (code == KeyCode.LeftControl) name = "LeftCtrl";
                else if (code == KeyCode.RightControl) name = "RightCtrl";
                Key key;
                if (Enum.TryParse(name, true, out key) && key != Key.None) control = Keyboard.current[key];
            }
            return control != null && (phase == 0 ? control.isPressed : phase == 1 ? control.wasPressedThisFrame : control.wasReleasedThisFrame);
        }

        public void Dispose() { actions.Dispose(); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install() {
#if !ENABLE_LEGACY_INPUT_MANAGER
            if (instance != null) instance.Dispose();
            instance = new ExampleInputSystem();
            ExampleInput.Provider = instance;
            SceneManager.sceneLoaded -= PrepareExampleUI;
            SceneManager.sceneLoaded += PrepareExampleUI;
#endif
        }

        static void PrepareExampleUI(Scene scene, LoadSceneMode mode) {
            // Only SDK sample scenes are adapted. A game's own EventSystems are untouched.
            if (!scene.path.StartsWith("Assets/Spine Examples/", StringComparison.Ordinal)
                && !scene.path.StartsWith("Packages/com.esotericsoftware.spine.urp-shaders/Examples/", StringComparison.Ordinal)) return;
            if (UnityEngine.EventSystems.EventSystem.current == null) {
                var events = new GameObject("Spine Example EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(InputSystemUIInputModule));
                SceneManager.MoveGameObjectToScene(events, scene);
            }
            foreach (var root in scene.GetRootGameObjects()) {
                foreach (var camera in root.GetComponentsInChildren<Camera>(true)) {
                    if (camera.GetComponent<UnityEngine.EventSystems.PhysicsRaycaster>() == null)
                        camera.gameObject.AddComponent<UnityEngine.EventSystems.PhysicsRaycaster>();
                    if (camera.GetComponent<UnityEngine.EventSystems.Physics2DRaycaster>() == null)
                        camera.gameObject.AddComponent<UnityEngine.EventSystems.Physics2DRaycaster>();
                }
                foreach (var legacy in root.GetComponentsInChildren<UnityEngine.EventSystems.BaseInputModule>(true)) {
                    // Old sample scenes also contain the obsolete TouchInputModule.
#pragma warning disable 618
                    if (!(legacy is UnityEngine.EventSystems.StandaloneInputModule)
                        && !(legacy is UnityEngine.EventSystems.TouchInputModule)) continue;
#pragma warning restore 618
                    legacy.enabled = false;
                    var module = legacy.GetComponent<InputSystemUIInputModule>();
                    if (module == null) module = legacy.gameObject.AddComponent<InputSystemUIInputModule>();
                    if (module.actionsAsset == null) module.AssignDefaultActions();
                }
            }
        }
    }
}
#endif
