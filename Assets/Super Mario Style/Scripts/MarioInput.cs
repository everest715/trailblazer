using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public static class MarioInput
{
    public static bool AnyKeyDown
    {
        get
        {
            return AnyButtonDown(Keyboard.current)
                || AnyButtonDown(Mouse.current)
                || AnyButtonDown(Gamepad.current);
        }
    }

    public static bool GetKey(KeyCode key)
    {
        var button = GetButton(key);
        return button != null && button.isPressed;
    }

    public static bool GetKeyDown(KeyCode key)
    {
        var button = GetButton(key);
        return button != null && button.wasPressedThisFrame;
    }

    public static float GetAxis(string axisName)
    {
        var gamepad = Gamepad.current;
        if (gamepad == null) return 0f;

        if (axisName == "Horizontal") return gamepad.leftStick.x.ReadValue();
        if (axisName == "Vertical") return gamepad.leftStick.y.ReadValue();
        return 0f;
    }

    private static ButtonControl GetButton(KeyCode key)
    {
        if (TryGetKeyboardKey(key, out var keyboardKey))
        {
            return Keyboard.current?[keyboardKey];
        }

        var mouse = Mouse.current;
        if (mouse != null)
        {
            switch (key)
            {
                case KeyCode.Mouse0: return mouse.leftButton;
                case KeyCode.Mouse1: return mouse.rightButton;
                case KeyCode.Mouse2: return mouse.middleButton;
                case KeyCode.Mouse3: return mouse.forwardButton;
                case KeyCode.Mouse4: return mouse.backButton;
            }
        }

        var gamepad = Gamepad.current;
        if (gamepad == null) return null;

        switch (key)
        {
            case KeyCode.JoystickButton0: return gamepad.buttonSouth;
            case KeyCode.JoystickButton1: return gamepad.buttonEast;
            case KeyCode.JoystickButton2: return gamepad.buttonWest;
            case KeyCode.JoystickButton3: return gamepad.buttonNorth;
            case KeyCode.JoystickButton4: return gamepad.leftShoulder;
            case KeyCode.JoystickButton5: return gamepad.rightShoulder;
            case KeyCode.JoystickButton6: return gamepad.selectButton;
            case KeyCode.JoystickButton7: return gamepad.startButton;
            case KeyCode.JoystickButton8: return gamepad.leftStickButton;
            case KeyCode.JoystickButton9: return gamepad.rightStickButton;
            default: return null;
        }
    }

    private static bool TryGetKeyboardKey(KeyCode key, out Key keyboardKey)
    {
        var name = key.ToString();
        if (name.StartsWith("Alpha", StringComparison.Ordinal))
        {
            name = "Digit" + name.Substring(5);
        }
        else if (name.StartsWith("Keypad", StringComparison.Ordinal))
        {
            name = "Numpad" + name.Substring(6);
        }
        else
        {
            switch (name)
            {
                case "Return": name = "Enter"; break;
                case "LeftCommand":
                case "LeftWindows": name = "LeftMeta"; break;
                case "RightCommand":
                case "RightWindows": name = "RightMeta"; break;
                case "Print": name = "PrintScreen"; break;
            }
        }

        return Enum.TryParse(name, out keyboardKey) && keyboardKey != Key.None;
    }

    private static bool AnyButtonDown(InputDevice device)
    {
        if (device == null) return false;

        foreach (var control in device.allControls)
        {
            if (control is ButtonControl button && button.wasPressedThisFrame)
            {
                return true;
            }
        }
        return false;
    }
}
