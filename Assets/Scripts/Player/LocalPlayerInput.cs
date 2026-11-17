using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Four local keyboard layouts plus one Unity Input System gamepad per player.</summary>
public static class LocalPlayerInput
{
    private static readonly Key[,] Keys =
    {
        { Key.LeftArrow, Key.RightArrow, Key.Delete, Key.RightShift },
        { Key.A, Key.D, Key.LeftCtrl, Key.LeftShift },
        { Key.I, Key.P, Key.Digit8, Key.Digit9 },
        { Key.R, Key.Y, Key.Digit4, Key.Digit5 }
    };

    private static Gamepad Pad(int player) => player >= 0 && player < Gamepad.all.Count ? Gamepad.all[player] : null;

    private static bool Held(int player, int action)
    {
        var keyboard = Keyboard.current;
        return player >= 0 && player < 4 && keyboard != null && keyboard[Keys[player, action]].isPressed;
    }

    public static float Horizontal(int player)
    {
        float keyboard = (Held(player, 1) ? 1f : 0f) - (Held(player, 0) ? 1f : 0f);
        var pad = Pad(player);
        float gamepad = pad == null ? 0f : pad.leftStick.x.ReadValue();
        if (pad != null && Mathf.Abs(pad.dpad.x.ReadValue()) > Mathf.Abs(gamepad))
            gamepad = pad.dpad.x.ReadValue();
        return Mathf.Abs(keyboard) > Mathf.Abs(gamepad) ? keyboard : gamepad;
    }

    public static bool JumpPressed(int player)
    {
        var keyboard = Keyboard.current;
        bool key = player >= 0 && player < 4 && keyboard != null && keyboard[Keys[player, 2]].wasPressedThisFrame;
        return key || (Pad(player)?.buttonSouth.wasPressedThisFrame ?? false);
    }

    public static bool RunHeld(int player) => Held(player, 3) || (Pad(player)?.buttonEast.isPressed ?? false);
}
