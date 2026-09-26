using System.Numerics;
using Raylib_cs;

namespace FishLegs;

public struct PlayerInput
{
    public Vector2 Move;        // x = screen right, y = screen up
    public bool JumpPressed, DashPressed, SlapPressed;
    public bool KickDown, KickPressed, KickReleased;
    public bool MenuUp, MenuDown, MenuLeft, MenuRight;

    public bool Confirm => KickPressed || JumpPressed;

    public void ClearEdges()
    {
        JumpPressed = DashPressed = SlapPressed = KickPressed = KickReleased = false;
        MenuUp = MenuDown = MenuLeft = MenuRight = false;
    }
}

class KeyMap
{
    public KeyboardKey[] Up = [], Down = [], Left = [], Right = [];
    public KeyboardKey[] Jump = [], Dash = [], Kick = [], Slap = [];
}

// Reads one player's keyboard layout plus (optionally) a gamepad, with edge detection.
class HumanInput
{
    readonly KeyMap keys;
    public int Gamepad = -1;
    bool prevKick;
    Vector2 prevMove;

    public HumanInput(KeyMap keys, int gamepad)
    {
        this.keys = keys;
        Gamepad = gamepad;
    }

    public static readonly KeyMap P1Keys = new()
    {
        Up = [KeyboardKey.W], Down = [KeyboardKey.S], Left = [KeyboardKey.A], Right = [KeyboardKey.D],
        Jump = [KeyboardKey.V, KeyboardKey.Space],
        Kick = [KeyboardKey.B, KeyboardKey.E],
        Dash = [KeyboardKey.N, KeyboardKey.LeftShift],
        Slap = [KeyboardKey.C, KeyboardKey.Q],
    };

    public static readonly KeyMap P2Keys = new()
    {
        Up = [KeyboardKey.Up], Down = [KeyboardKey.Down], Left = [KeyboardKey.Left], Right = [KeyboardKey.Right],
        Jump = [KeyboardKey.Semicolon],
        Kick = [KeyboardKey.L],
        Dash = [KeyboardKey.K],
        Slap = [KeyboardKey.J],
    };

    // In single-player, P1 may also use the arrow keys / P2 buttons, whichever feels right.
    public static KeyMap Merge(KeyMap a, KeyMap b) => new()
    {
        Up = [.. a.Up, .. b.Up], Down = [.. a.Down, .. b.Down], Left = [.. a.Left, .. b.Left], Right = [.. a.Right, .. b.Right],
        Jump = [.. a.Jump, .. b.Jump], Kick = [.. a.Kick, .. b.Kick], Dash = [.. a.Dash, .. b.Dash], Slap = [.. a.Slap, .. b.Slap],
    };

    static bool Down(KeyboardKey[] ks) => ks.Any(k => Raylib.IsKeyDown(k));
    static bool Pressed(KeyboardKey[] ks) => ks.Any(k => Raylib.IsKeyPressed(k));

    public PlayerInput Read()
    {
        var inp = new PlayerInput();
        Vector2 m = Vector2.Zero;
        if (Down(keys.Left)) m.X -= 1;
        if (Down(keys.Right)) m.X += 1;
        if (Down(keys.Up)) m.Y += 1;
        if (Down(keys.Down)) m.Y -= 1;

        bool kick = Down(keys.Kick);
        inp.JumpPressed = Pressed(keys.Jump);
        inp.DashPressed = Pressed(keys.Dash);
        inp.SlapPressed = Pressed(keys.Slap);

        if (Gamepad >= 0 && Raylib.IsGamepadAvailable(Gamepad))
        {
            int g = Gamepad;
            var stick = new Vector2(Raylib.GetGamepadAxisMovement(g, GamepadAxis.LeftX), -Raylib.GetGamepadAxisMovement(g, GamepadAxis.LeftY));
            if (stick.Length() > 0.2f) m += stick;
            if (Raylib.IsGamepadButtonDown(g, GamepadButton.LeftFaceLeft)) m.X -= 1;
            if (Raylib.IsGamepadButtonDown(g, GamepadButton.LeftFaceRight)) m.X += 1;
            if (Raylib.IsGamepadButtonDown(g, GamepadButton.LeftFaceUp)) m.Y += 1;
            if (Raylib.IsGamepadButtonDown(g, GamepadButton.LeftFaceDown)) m.Y -= 1;
            kick |= Raylib.IsGamepadButtonDown(g, GamepadButton.RightFaceLeft) || Raylib.GetGamepadAxisMovement(g, GamepadAxis.RightTrigger) > 0.3f;
            inp.JumpPressed |= Raylib.IsGamepadButtonPressed(g, GamepadButton.RightFaceDown);
            inp.DashPressed |= Raylib.IsGamepadButtonPressed(g, GamepadButton.RightFaceRight) || Raylib.IsGamepadButtonPressed(g, GamepadButton.RightTrigger1);
            inp.SlapPressed |= Raylib.IsGamepadButtonPressed(g, GamepadButton.RightFaceUp) || Raylib.IsGamepadButtonPressed(g, GamepadButton.LeftTrigger1);
        }

        if (m.Length() > 1) m = Vector2.Normalize(m);
        inp.Move = m;
        inp.KickDown = kick;
        inp.KickPressed = kick && !prevKick;
        inp.KickReleased = !kick && prevKick;
        prevKick = kick;

        inp.MenuUp = m.Y > 0.5f && prevMove.Y <= 0.5f;
        inp.MenuDown = m.Y < -0.5f && prevMove.Y >= -0.5f;
        inp.MenuRight = m.X > 0.5f && prevMove.X <= 0.5f;
        inp.MenuLeft = m.X < -0.5f && prevMove.X >= -0.5f;
        prevMove = m;
        return inp;
    }
}
