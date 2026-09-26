using System.Numerics;
using Raylib_cs;

namespace FishLegs;

public struct PlayerInput
{
    public Vector2 Move;        // x = screen right, y = screen up
    public bool JumpPressed, DashPressed, SlapPressed;
    public bool KickDown, KickPressed, KickReleased;
    public bool JumpDown, DashDown, SlapDown;   // held state, for the training input display
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

public enum PadSource { None, GameController, Raylib }

// One snapshot per frame of every connected controller, from Apple's GameController framework on
// macOS and raylib otherwise (or for controllers GameController doesn't support).
static class Pads
{
    public const int Max = 4;
    const float DeadZone = 0.25f;
    const int ButtonCount = (int)GamepadButton.RightThumb + 1;

    class Slot
    {
        public PadSource Source;
        public int RaylibIndex = -1;
        public string Name = "", Category = "";
        public Vector2 Stick, Dpad;
        public float LeftTrigger, RightTrigger;
        public bool[] Down = new bool[ButtonCount], Prev = new bool[ButtonCount];
    }

    static readonly Slot[] slots = Enumerable.Range(0, Max).Select(_ => new Slot()).ToArray();

    public static void Update()
    {
        foreach (var s in slots)
        {
            (s.Prev, s.Down) = (s.Down, s.Prev);
            Array.Clear(s.Down);
            s.Source = PadSource.None;
            s.RaylibIndex = -1;
            s.Stick = s.Dpad = Vector2.Zero;
            s.LeftTrigger = s.RightTrigger = 0;
        }

        int n = 0;
        MacGameController.Poll();
        foreach (var c in MacGameController.Controllers)
        {
            if (n >= Max) break;
            var s = slots[n++];
            s.Source = PadSource.GameController;
            s.Name = c.Name;
            s.Category = c.Category;
            s.Stick = c.LeftStick;
            s.LeftTrigger = c.LeftTrigger;
            s.RightTrigger = c.RightTrigger;
            Set(s, GamepadButton.RightFaceDown, c.A);
            Set(s, GamepadButton.RightFaceRight, c.B);
            Set(s, GamepadButton.RightFaceLeft, c.X);
            Set(s, GamepadButton.RightFaceUp, c.Y);
            Set(s, GamepadButton.LeftTrigger1, c.LeftShoulder);
            Set(s, GamepadButton.RightTrigger1, c.RightShoulder);
            Set(s, GamepadButton.LeftTrigger2, c.LeftTrigger > 0.5f);
            Set(s, GamepadButton.RightTrigger2, c.RightTrigger > 0.5f);
            Set(s, GamepadButton.MiddleRight, c.Menu);
            Set(s, GamepadButton.MiddleLeft, c.Options);
            Set(s, GamepadButton.LeftThumb, c.LeftThumb);
            Set(s, GamepadButton.RightThumb, c.RightThumb);
            Set(s, GamepadButton.LeftFaceUp, c.Dpad.Y > 0.5f);
            Set(s, GamepadButton.LeftFaceDown, c.Dpad.Y < -0.5f);
            Set(s, GamepadButton.LeftFaceLeft, c.Dpad.X < -0.5f);
            Set(s, GamepadButton.LeftFaceRight, c.Dpad.X > 0.5f);
        }

        // raylib pads that GameController isn't already providing (e.g. generic USB pads, non-macOS).
        for (int r = 0; r < Max && n < Max; r++)
        {
            if (!Raylib.IsGamepadAvailable(r)) continue;
            string name = (Raylib.GetGamepadName_(r) ?? "").Trim();
            if (MacGameController.Controllers.Any(c => SameDevice(c.Name, name))) continue;
            var s = slots[n++];
            s.Source = PadSource.Raylib;
            s.RaylibIndex = r;
            s.Name = string.IsNullOrWhiteSpace(name) ? $"Controller {r + 1}" : name;
            s.Category = "";
            for (int b = 1; b < ButtonCount; b++) s.Down[b] = Raylib.IsGamepadButtonDown(r, (GamepadButton)b);
            s.Stick = new Vector2(Raylib.GetGamepadAxisMovement(r, GamepadAxis.LeftX), -Raylib.GetGamepadAxisMovement(r, GamepadAxis.LeftY));
            // Triggers rest at -1 on most mappings.
            if (Raylib.GetGamepadAxisCount(r) > 4)
            {
                s.LeftTrigger = U.Clamp01((Raylib.GetGamepadAxisMovement(r, GamepadAxis.LeftTrigger) + 1) / 2);
                s.RightTrigger = U.Clamp01((Raylib.GetGamepadAxisMovement(r, GamepadAxis.RightTrigger) + 1) / 2);
            }
        }

        foreach (var s in slots)
        {
            if (s.Source == PadSource.None) continue;
            s.Dpad = new Vector2(
                (s.Down[(int)GamepadButton.LeftFaceRight] ? 1 : 0) - (s.Down[(int)GamepadButton.LeftFaceLeft] ? 1 : 0),
                (s.Down[(int)GamepadButton.LeftFaceUp] ? 1 : 0) - (s.Down[(int)GamepadButton.LeftFaceDown] ? 1 : 0));
        }
    }

    static void Set(Slot s, GamepadButton b, bool down) => s.Down[(int)b] = down;

    static bool SameDevice(string a, string b) =>
        a.Length > 0 && b.Length > 0 && (a.Contains(b, StringComparison.OrdinalIgnoreCase) || b.Contains(a, StringComparison.OrdinalIgnoreCase));

    static bool Valid(int pad) => pad >= 0 && pad < Max && slots[pad].Source != PadSource.None;

    public static bool Available(int pad) => Valid(pad);
    public static PadSource Source(int pad) => Valid(pad) ? slots[pad].Source : PadSource.None;
    public static string Name(int pad) => Valid(pad) ? slots[pad].Name : "";
    public static string Category(int pad) => Valid(pad) ? slots[pad].Category : "";

    public static List<int> Connected()
    {
        var list = new List<int>();
        for (int i = 0; i < Max; i++) if (Valid(i)) list.Add(i);
        return list;
    }

    public static Vector2 RawStick(int pad) => Valid(pad) ? slots[pad].Stick : Vector2.Zero;

    // Radial deadzone with rescale, so small stick drift doesn't make fish wander off.
    public static Vector2 Stick(int pad)
    {
        var v = RawStick(pad);
        float len = v.Length();
        if (len < DeadZone) return Vector2.Zero;
        float scaled = MathF.Min(1, (len - DeadZone) / (1 - DeadZone));
        return v / len * scaled;
    }

    public static Vector2 Dpad(int pad) => Valid(pad) ? slots[pad].Dpad : Vector2.Zero;
    public static float LeftTriggerValue(int pad) => Valid(pad) ? slots[pad].LeftTrigger : 0;
    public static float RightTriggerValue(int pad) => Valid(pad) ? slots[pad].RightTrigger : 0;

    public static bool Down(int pad, GamepadButton b) => Valid(pad) && slots[pad].Down[(int)b];
    public static bool Pressed(int pad, GamepadButton b) => Valid(pad) && slots[pad].Down[(int)b] && !slots[pad].Prev[(int)b];

    public static bool RightTrigger(int pad) => RightTriggerValue(pad) > 0.3f || Down(pad, GamepadButton.RightTrigger2);

    public static bool AnyButtonPressed(int pad)
    {
        for (int b = 1; b < ButtonCount; b++)
            if (Pressed(pad, (GamepadButton)b)) return true;
        return false;
    }

    // Input Monitoring only matters for controllers read through raylib (IOKit HID).
    public static bool PermissionProblem =>
        MacPermissions.Blocked && Connected().Any(p => Source(p) == PadSource.Raylib);

    public static bool Active(int pad) =>
        AnyButtonPressed(pad) || Stick(pad) != Vector2.Zero || RightTrigger(pad);

    // Rumble only goes through raylib; GameController haptics need CoreHaptics and aren't wired up.
    public static void Rumble(int pad, float strength, float seconds)
    {
        if (!Valid(pad) || slots[pad].RaylibIndex < 0) return;
        strength = U.Clamp01(strength);
        Raylib.SetGamepadVibration(slots[pad].RaylibIndex, strength, strength * 0.7f, seconds);
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

    public Device Device => UsingPad && LastPad >= 0 && Pads.Available(LastPad)
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
        inp.JumpDown = Down(keys.Jump);
        inp.DashDown = Down(keys.Dash);
        inp.SlapDown = Down(keys.Slap);
        inp.JumpPressed = Pressed(keys.Jump);
        inp.DashPressed = Pressed(keys.Dash);
        inp.SlapPressed = Pressed(keys.Slap);
        if (keys.All.Any(k => Raylib.IsKeyPressed(k))) UsingPad = false;

        foreach (int g in AssignedPads)
        {
            if (!Pads.Available(g)) continue;
            if (Pads.Active(g)) { UsingPad = true; LastPad = g; }
            m += Pads.Stick(g) + Pads.Dpad(g);
            kick |= Pads.Down(g, GamepadButton.RightFaceLeft) || Pads.RightTrigger(g);
            inp.JumpDown |= Pads.Down(g, GamepadButton.RightFaceDown);
            inp.DashDown |= Pads.Down(g, GamepadButton.RightFaceRight) || Pads.Down(g, GamepadButton.RightTrigger1);
            inp.SlapDown |= Pads.Down(g, GamepadButton.RightFaceUp) || Pads.Down(g, GamepadButton.LeftTrigger1);
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

    public Device Device => UsingPad && LastPad >= 0 && Pads.Available(LastPad)
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
            if (!Pads.Available(g)) { prevY[g] = 0; continue; }
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
