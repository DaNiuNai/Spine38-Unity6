using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Spine.Unity.Examples;

public static class SpineInputChecks {
    public static string Run() {
        if (!Application.isPlaying) throw new InvalidOperationException("Input regression requires Play Mode.");
        var previousEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        var previousBackgroundBehavior = InputSystem.settings.backgroundBehavior;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        var previousKeyboard = Keyboard.current;
        var keyboard = InputSystem.AddDevice<Keyboard>();
        var previousGamepad = Gamepad.current;
        var gamepad = InputSystem.AddDevice<Gamepad>();
        using (var input = new ExampleInputSystem()) {
            try {
                keyboard.MakeCurrent();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D, Key.Space));
                InputSystem.Update();
                Require(input.GetAxis("Horizontal", true) == 1, "D did not move right: value=" + input.GetAxis("Horizontal", true)
                    + ", key=" + keyboard.dKey.isPressed + ", enabled=" + keyboard.enabled + ", focused=" + Application.isFocused);
                Require(input.GetButton("Jump", 1), "Jump press edge missing");
                Require(input.GetKey(KeyCode.Space, 0), "KeyCode.Space mapping failed");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                Require(input.GetButton("Jump", 2), "Jump release edge missing");
                Require(!input.GetKey(KeyCode.Space, 0), "Space remained held after release");
                InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = new Vector2(-1, 0) });
                InputSystem.Update();
                Require(input.GetAxis("Horizontal", true) < -.9f, "Gamepad axis mapping failed");
                Directory.CreateDirectory("Artifacts/SpineCompatibility");
                var result = "Keyboard axis, key mapping, button press/release edges and gamepad axis passed.";
                File.WriteAllText("Artifacts/SpineCompatibility/input.txt", result);
                return result;
            } finally {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.RemoveDevice(gamepad);
                if (previousKeyboard != null) previousKeyboard.MakeCurrent();
                if (previousGamepad != null) previousGamepad.MakeCurrent();
                InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorBehavior;
                InputSystem.settings.backgroundBehavior = previousBackgroundBehavior;
            }
        }
    }
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
