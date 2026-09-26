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

    public IEnumerable<KeyboardKey> All => Up.Concat(Down).Concat(Left).Concat(Right).Concat(Jump).Concat(Dash).Concat(Kick).Concat(Slap);
}

static class Pads
{
    public const int Max = 4;
    const float DeadZone = 0.25f;

    public static List<int> Connected()
    {
        var list = new List<int>();
        for (int i = 0; i < Max; i++) if (Raylib.IsGamepadAvailable(i)) list.Add(i);
        return list;
    }

    // Radial deadzone with rescale, so small stick drift doesn't make fish wander off.
    public static Vector2 Stick(int pad)
    {
        var v = new Vector2(Raylib.GetGamepadAxisMovement(pad, GamepadAxis.LeftX), -Raylib.GetGamepadAxisMovement(pad, GamepadAxis.LeftY));
        float len = v.Length();
        if (len < DeadZone) return Vector2.Zero;
        float scaled = MathF.Min(1, (len - DeadZone) / (1 - DeadZone));
        return v / len * scaled;
    }

    public static Vector2 Dpad(int pad)
    {
        var m = Vector2.Zero;
        if (Raylib.IsGamepadButtonDown(pad, GamepadButton.LeftFaceLeft)) m.X -= 1;
        if (Raylib.IsGamepadButtonDown(pad, GamepadButton.LeftFaceRight)) m.X += 1;
        if (Raylib.IsGamepadButtonDown(pad, GamepadButton.LeftFaceUp)) m.Y += 1;
        if (Raylib.IsGamepadButtonDown(pad, GamepadButton.LeftFaceDown)) m.Y -= 1;
        return m;
    }

    // Triggers report -1 at rest on most mappings; some pads expose them as buttons instead.
    public static bool RightTrigger(int pad) =>
        Raylib.GetGamepadAxisMovement(pad, GamepadAxis.RightTrigger) > 0.3f || Raylib.IsGamepadButtonDown(pad, GamepadButton.RightTrigger2);

    public static bool AnyButtonPressed(int pad)
    {
        for (int b = (int)GamepadButton.LeftFaceUp; b <= (int)GamepadButton.RightThumb; b++)
            if (Raylib.IsGamepadButtonPressed(pad, (GamepadButton)b)) return true;
        return false;
    }

    public static bool Active(int pad) =>
        AnyButtonPressed(pad) || Stick(pad) != Vector2.Zero || RightTrigger(pad);

    public static string Name(int pad)
    {
        string n = Raylib.GetGamepadName_(pad);
        return string.IsNullOrWhiteSpace(n) ? $"Controller {pad + 1}" : n.Trim();
    }

    public static bool Pressed(int pad, GamepadButton b) => Raylib.IsGamepadButtonPressed(pad, b);
    public static bool AnyPressed(GamepadButton b) => Connected().Any(p => Raylib.IsGamepadButtonPressed(p, b));

    public static void Rumble(int pad, float strength, float seconds)
    {
        if (pad < 0 || !Raylib.IsGamepadAvailable(pad)) return;
        strength = U.Clamp01(strength);
        Raylib.SetGamepadVibration(pad, strength, strength * 0.7f, seconds);
    }
}

// Reads one player's keyboard layout plus any gamepads assigned to them, with edge detection.
class HumanInput
{
    readonly KeyMap keys;
    readonly int keySet;
    public int[] AssignedPads = [];
    public bool UsingPad;          // last device this player touched (drives which prompts we show)
    public int LastPad = -1;
    bool prevKick;
    Vector2 prevMove;

    public HumanInput(KeyMap keys, int keySet)
    {
        this.keys = keys;
        this.keySet = keySet;
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

    public Device Device => UsingPad && LastPad >= 0 && Raylib.IsGamepadAvailable(LastPad)
        ? Device.Gamepad(Prompts.DetectFamily(LastPad))
        : Device.Keyboard(keySet);

    public void Rumble(float strength, float seconds)
    {
        if (UsingPad) Pads.Rumble(LastPad, strength, seconds);
    }

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
        if (keys.All.Any(k => Raylib.IsKeyPressed(k))) UsingPad = false;

        foreach (int g in AssignedPads)
        {
            if (!Raylib.IsGamepadAvailable(g)) continue;
            if (Pads.Active(g)) { UsingPad = true; LastPad = g; }
            m += Pads.Stick(g) + Pads.Dpad(g);
            kick |= Raylib.IsGamepadButtonDown(g, GamepadButton.RightFaceLeft) || Pads.RightTrigger(g);
            inp.JumpPressed |= Pads.Pressed(g, GamepadButton.RightFaceDown);
            inp.DashPressed |= Pads.Pressed(g, GamepadButton.RightFaceRight) || Pads.Pressed(g, GamepadButton.RightTrigger1);
            inp.SlapPressed |= Pads.Pressed(g, GamepadButton.RightFaceUp) || Pads.Pressed(g, GamepadButton.LeftTrigger1);
        }
        if (LastPad < 0 && AssignedPads.Length > 0) LastPad = AssignedPads[0];

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

// Menu navigation shared by everyone: keyboard plus the stick/d-pad of every connected pad.
class MenuInput
{
    readonly float[] prevY = new float[Pads.Max];
    public bool UsingPad;
    public int LastPad = -1;

    public int Vertical;
    public bool Confirm, Back, Pause, Quit;

    public Device Device => UsingPad && LastPad >= 0 && Raylib.IsGamepadAvailable(LastPad)
        ? Device.Gamepad(Prompts.DetectFamily(LastPad))
        : Device.Keyboard(Device.KeysMenu);

    public void Update()
    {
        Vertical = 0;
        if (Raylib.IsKeyPressed(KeyboardKey.Up) || Raylib.IsKeyPressed(KeyboardKey.W)) Vertical--;
        if (Raylib.IsKeyPressed(KeyboardKey.Down) || Raylib.IsKeyPressed(KeyboardKey.S)) Vertical++;
        Confirm = Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.Space);
        Back = Raylib.IsKeyPressed(KeyboardKey.Escape);
        Pause = Raylib.IsKeyPressed(KeyboardKey.Escape) || Raylib.IsKeyPressed(KeyboardKey.P);
        Quit = Raylib.IsKeyPressed(KeyboardKey.Enter);
        if (Raylib.GetKeyPressed() != 0) UsingPad = false;

        for (int g = 0; g < Pads.Max; g++)
        {
            if (!Raylib.IsGamepadAvailable(g)) { prevY[g] = 0; continue; }
            if (Pads.Active(g)) { UsingPad = true; LastPad = g; }
            float y = Pads.Stick(g).Y + Pads.Dpad(g).Y;
            if (y > 0.5f && prevY[g] <= 0.5f) Vertical--;
            if (y < -0.5f && prevY[g] >= -0.5f) Vertical++;
            prevY[g] = y;
            Confirm |= Pads.Pressed(g, GamepadButton.RightFaceDown) || Pads.Pressed(g, GamepadButton.MiddleRight);
            Back |= Pads.Pressed(g, GamepadButton.RightFaceRight);
            Pause |= Pads.Pressed(g, GamepadButton.MiddleRight);
            Quit |= Pads.Pressed(g, GamepadButton.MiddleLeft);
        }
        Vertical = Math.Sign(Vertical);
    }
}
