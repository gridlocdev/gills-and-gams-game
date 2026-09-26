using System.Numerics;
using Raylib_cs;

namespace FishLegs;

// Console-style controller assignment: shows every keyboard layout and controller, which player
// it belongs to, and a live readout of what's being pressed or held on it.
class ControllersScreen
{
    const float HoldThreshold = 0.35f;
    const float FlashTime = 0.3f;
    const float ResetHold = 1.2f;

    // Per device+control: how long it's been held, and a flash timer started on the press.
    readonly Dictionary<string, (float hold, float flash)> state = new();
    readonly Dictionary<int, float> prevStickX = new();
    float selectHeld;

    static readonly Color[] SlotCols = { Game.TeamCols[0], Game.TeamCols[0], Game.TeamCols[1], Game.TeamCols[1] };

    public void Open()
    {
        state.Clear();
        prevStickX.Clear();
        selectHeld = 0;
    }

    /// <summary>Returns true when the player backs out to the title screen.</summary>
    public bool Update(Roster roster, MenuInput menu, float dt)
    {
        roster.Sync();
        foreach (var d in roster.Devices())
            foreach (var (control, down) in Controls(d))
            {
                string key = $"{d}:{control}";
                var (hold, flash) = state.GetValueOrDefault(key);
                if (down && hold <= 0) flash = FlashTime;
                state[key] = (down ? hold + dt : 0, MathF.Max(0, flash - dt));
            }

        // Move devices between players with that device's own left/right.
        if (Raylib.IsKeyPressed(KeyboardKey.A)) Move(roster, InputDevice.KeyboardA, -1);
        if (Raylib.IsKeyPressed(KeyboardKey.D)) Move(roster, InputDevice.KeyboardA, 1);
        if (Raylib.IsKeyPressed(KeyboardKey.Left)) Move(roster, InputDevice.KeyboardB, -1);
        if (Raylib.IsKeyPressed(KeyboardKey.Right)) Move(roster, InputDevice.KeyboardB, 1);
        foreach (int p in Pads.Connected())
        {
            float x = Pads.Stick(p).X + Pads.Dpad(p).X;
            float prev = prevStickX.GetValueOrDefault(p);
            if (x > 0.5f && prev <= 0.5f) Move(roster, InputDevice.Gamepad(p), 1);
            if (x < -0.5f && prev >= -0.5f) Move(roster, InputDevice.Gamepad(p), -1);
            prevStickX[p] = x;
        }

        // Reset to defaults: R on the keyboard, or hold Select/View on any controller.
        bool selectDown = Pads.Connected().Any(p => Pads.Down(p, GamepadButton.MiddleLeft));
        selectHeld = selectDown ? selectHeld + dt : 0;
        if (Raylib.IsKeyPressed(KeyboardKey.R) || (selectHeld >= ResetHold && selectHeld - dt < ResetHold))
        {
            roster.ResetDefaults();
            Audio.Play(Sfx.Select, 0.9f);
        }

        // Leave with Esc / Enter or Start. (B is left alone so it can be tested.)
        return Raylib.IsKeyPressed(KeyboardKey.Escape) || Raylib.IsKeyPressed(KeyboardKey.Enter) ||
               Pads.Connected().Any(p => Pads.Pressed(p, GamepadButton.MiddleRight));
    }

    static void Move(Roster roster, InputDevice d, int dir)
    {
        roster.Move(d, dir);
        Audio.Play(Sfx.Blip, dir > 0 ? 1.2f : 1f);
        if (d.IsPad) Pads.Rumble(d.Pad, 0.25f, 0.1f);
    }

    static IEnumerable<(string control, bool down)> Controls(InputDevice d)
    {
        if (d.IsPad)
        {
            int p = d.Pad;
            yield return ("A", Pads.Down(p, GamepadButton.RightFaceDown));
            yield return ("B", Pads.Down(p, GamepadButton.RightFaceRight));
            yield return ("X", Pads.Down(p, GamepadButton.RightFaceLeft));
            yield return ("Y", Pads.Down(p, GamepadButton.RightFaceUp));
            yield return ("LB", Pads.Down(p, GamepadButton.LeftTrigger1));
            yield return ("RB", Pads.Down(p, GamepadButton.RightTrigger1));
            yield return ("LT", Pads.LeftTriggerValue(p) > 0.3f || Pads.Down(p, GamepadButton.LeftTrigger2));
            yield return ("RT", Pads.RightTriggerValue(p) > 0.3f || Pads.Down(p, GamepadButton.RightTrigger2));
            yield return ("Start", Pads.Down(p, GamepadButton.MiddleRight));
            yield return ("Select", Pads.Down(p, GamepadButton.MiddleLeft));
            yield return ("LS", Pads.Down(p, GamepadButton.LeftThumb));
            yield return ("RS", Pads.Down(p, GamepadButton.RightThumb));
            yield return ("Up", Pads.Down(p, GamepadButton.LeftFaceUp));
            yield return ("Down", Pads.Down(p, GamepadButton.LeftFaceDown));
            yield return ("Left", Pads.Down(p, GamepadButton.LeftFaceLeft));
            yield return ("Right", Pads.Down(p, GamepadButton.LeftFaceRight));
            yield return ("Stick", Pads.Stick(p) != Vector2.Zero);
            yield break;
        }
        var k = d.Kind == DeviceKind.KeyboardA ? HumanInput.P1Keys : HumanInput.P2Keys;
        yield return ("Up", k.Up.Any(x => Raylib.IsKeyDown(x)));
        yield return ("Down", k.Down.Any(x => Raylib.IsKeyDown(x)));
        yield return ("Left", k.Left.Any(x => Raylib.IsKeyDown(x)));
        yield return ("Right", k.Right.Any(x => Raylib.IsKeyDown(x)));
        yield return ("Jump", k.Jump.Any(x => Raylib.IsKeyDown(x)));
        yield return ("Kick", k.Kick.Any(x => Raylib.IsKeyDown(x)));
        yield return ("Dash", k.Dash.Any(x => Raylib.IsKeyDown(x)));
        yield return ("Slap", k.Slap.Any(x => Raylib.IsKeyDown(x)));
    }

    (float hold, float flash) S(InputDevice d, string control) => state.GetValueOrDefault($"{d}:{control}");

    // ------------------------------------------------------------------ rendering

    public void Render(int W, int H, float t, Roster roster, Device menuDev)
    {
        Raylib.DrawRectangle(0, 0, W, H, new Color(5, 15, 35, 225));
        U.TextShadow("CONTROLLERS", 30, 20, 44, U.Col(255, 215, 80));
        Raylib.DrawText("Push left / right on a device to move it between players.  In 2v2, P1 + P2 are red, P3 + P4 are blue,", 32, 70, 16, U.Col(200, 215, 240));
        Raylib.DrawText("and players with nothing assigned are played by the CPU.  With a controller connected, keyboards only play if you add them here.", 32, 90, 16, U.Col(200, 215, 240));

        const int gap = 14, cardH = 150;
        int colW = (W - 60 - gap * 3) / 4;
        for (int p = 0; p < Roster.Players; p++)
        {
            int x = 30 + p * (colW + gap), y = 120;
            var devices = roster.DevicesFor(p);
            Raylib.DrawRectangleRounded(new Rectangle(x, y, colW, 54), 0.25f, 8, U.WithAlpha(SlotCols[p], 0.9f));
            U.TextShadow($"P{p + 1}", x + 12, y + 8, 36, Color.White, 2);
            Raylib.DrawText(p < 2 ? "Red team (2v2)" : "Blue team (2v2)", x + 70, y + 10, 15, U.Col(255, 255, 255, 220));
            Raylib.DrawText(devices.Count > 0 ? "HUMAN" : "CPU", x + 70, y + 30, 16, devices.Count > 0 ? Color.White : U.Col(255, 255, 255, 160));

            int cy = y + 62;
            if (devices.Count == 0)
            {
                Raylib.DrawRectangleRoundedLinesEx(new Rectangle(x, cy, colW, cardH), 0.08f, 8, 2, U.Col(70, 90, 120));
                U.TextCentered("ROBO-TROUT", x + colW / 2, cy + cardH / 2 - 22, 20, U.Col(120, 140, 170), 1);
                U.TextCentered("(no controller)", x + colW / 2, cy + cardH / 2 + 4, 15, U.Col(100, 120, 150), 1);
            }
            foreach (var d in devices.Take(2))
            {
                DrawCard(d, x, cy, colW, cardH, t);
                cy += cardH + 8;
            }
            if (devices.Count > 2) Raylib.DrawText($"+{devices.Count - 2} more", x + 8, cy, 15, U.Col(200, 210, 230));
        }

        int ty = H - 220;
        Raylib.DrawText("NOT PLAYING", 32, ty, 18, U.Col(150, 200, 255));
        var spare = roster.Devices().Where(d => roster.PlayerOf(d) == Roster.None).ToList();
        if (spare.Count == 0) Raylib.DrawText("(everything is assigned)", 160, ty + 2, 16, U.Col(120, 140, 170));
        for (int i = 0; i < spare.Count && i < 4; i++)
            DrawCard(spare[i], 30 + i * (colW + gap), ty + 26, colW, cardH, t);

        var kb = Device.Keyboard(Device.KeysMenu);
        int hx = 32, hy = H - 40;
        if (menuDev.Pad)
        {
            hx += Prompts.Hint(menuDev, Act.Pause, "Done", hx, hy, 30, 18, U.Col(210, 220, 240)) + 28;
            Raylib.DrawText("Hold", hx, hy + 7, 18, U.Col(210, 220, 240));
            hx += Raylib.MeasureText("Hold", 18) + 6;
            hx += Prompts.Hint(menuDev, Act.Quit, "Reset to defaults", hx, hy, 30, 18, U.Col(210, 220, 240)) + 28;
        }
        else
        {
            hx += Prompts.Hint(kb, Act.Back, "Done", hx, hy, 30, 18, U.Col(210, 220, 240)) + 28;
            hx += Prompts.Hint(kb, Act.Reset, "Reset to defaults", hx, hy, 30, 18, U.Col(210, 220, 240)) + 28;
        }
        if (selectHeld > 0)
        {
            Raylib.DrawRectangle(hx, hy + 12, 120, 8, new Color(0, 0, 0, 150));
            Raylib.DrawRectangle(hx, hy + 12, (int)(120 * U.Clamp01(selectHeld / ResetHold)), 8, U.Col(255, 215, 80));
        }
    }

    void DrawCard(InputDevice d, int x, int y, int w, int h, float t)
    {
        Raylib.DrawRectangleRounded(new Rectangle(x, y, w, h), 0.08f, 8, new Color(20, 34, 62, 245));
        string name = d.IsPad ? $"{d.Label}" : d.Label;
        Raylib.DrawText(Fit(name, 16, w - 20), x + 10, y + 8, 16, Color.White);
        if (d.IsPad) DrawPad(d, x, y, w, t);
        else DrawKeyboard(d, x, y, w, t);

        // Bottom line: what's being held right now, and for how long.
        var held = Controls(d).Where(c => c.down).Select(c => (c.control, S(d, c.control).hold)).ToList();
        string status = held.Count == 0 ? "Press something!"
            : string.Join("  ", held.Select(hc => hc.hold >= HoldThreshold ? $"HOLD {Label(d, hc.control)} {hc.hold:0.0}s" : $"{Label(d, hc.control)}"));
        Raylib.DrawText(Fit(status, 14, w - 20), x + 10, y + h - 22, 14, held.Count == 0 ? U.Col(110, 130, 160) : U.Col(255, 215, 80));
    }

    static string Label(InputDevice d, string control)
    {
        if (!d.IsPad) return control;
        var fam = Prompts.DetectFamily(d.Pad);
        return (fam, control) switch
        {
            (PadFamily.PlayStation, "A") => "Cross",
            (PadFamily.PlayStation, "B") => "Circle",
            (PadFamily.PlayStation, "X") => "Square",
            (PadFamily.PlayStation, "Y") => "Triangle",
            (PadFamily.PlayStation, "LB") => "L1",
            (PadFamily.PlayStation, "RB") => "R1",
            (PadFamily.PlayStation, "LT") => "L2",
            (PadFamily.PlayStation, "RT") => "R2",
            (PadFamily.Switch, "A") => "B",
            (PadFamily.Switch, "B") => "A",
            (PadFamily.Switch, "X") => "Y",
            (PadFamily.Switch, "Y") => "X",
            (PadFamily.Switch, "LB") => "L",
            (PadFamily.Switch, "RB") => "R",
            (PadFamily.Switch, "LT") => "ZL",
            (PadFamily.Switch, "RT") => "ZR",
            _ => control,
        };
    }

    // Glow + press flash + hold ring around a control centred at c.
    void Feedback(InputDevice d, string control, Vector2 c, float radius)
    {
        var (hold, flash) = S(d, control);
        if (flash > 0)
        {
            float k = 1 - flash / FlashTime;
            Raylib.DrawRing(c, radius + 2 + k * 10, radius + 4 + k * 10, 0, 360, 32, U.WithAlpha(Color.White, 1 - k));
        }
        if (hold >= HoldThreshold)
        {
            float k = U.Clamp01((hold - HoldThreshold) / 1f);
            Raylib.DrawRing(c, radius + 3, radius + 6, -90, -90 + 360 * k, 32, U.Col(255, 215, 80));
        }
    }

    void DrawPad(InputDevice d, int x, int y, int w, float t)
    {
        int p = d.Pad;
        var fam = Prompts.DetectFamily(p);

        // Shoulders and triggers across the top.
        (string c, int bx, float fill)[] tops =
        {
            ("LT", x + 10, Pads.LeftTriggerValue(p)), ("LB", x + 58, -1),
            ("RB", x + w - 100, -1), ("RT", x + w - 52, Pads.RightTriggerValue(p)),
        };
        foreach (var (c, bx, fill) in tops)
        {
            bool down = Controls(d).First(k => k.control == c).down;
            var r = new Rectangle(bx, y + 32, 42, 20);
            Raylib.DrawRectangleRounded(r, 0.4f, 6, down ? U.Col(255, 215, 80) : U.Col(45, 60, 90));
            if (fill > 0.02f)
                Raylib.DrawRectangleRounded(new Rectangle(bx, y + 32, 42 * U.Clamp01(fill), 20), 0.4f, 6, U.WithAlpha(U.Col(255, 150, 80), 0.8f));
            U.TextCentered(Label(d, c), bx + 21, y + 36, 13, down ? U.Col(20, 30, 50) : Color.White, 0);
            Feedback(d, c, new Vector2(bx + 21, y + 42), 16);
        }

        // Left stick.
        var sc = new Vector2(x + 40, y + 92);
        var raw = Pads.RawStick(p);
        Raylib.DrawCircleLinesV(sc, 24, U.Col(120, 150, 190));
        bool ls = Controls(d).First(k => k.control == "LS").down;
        Raylib.DrawCircleV(sc + new Vector2(raw.X, -raw.Y) * 24, 9, ls ? Color.White : raw.Length() > 0.25f ? U.Col(255, 215, 80) : U.Col(140, 150, 170));
        Feedback(d, "Stick", sc, 24);

        // D-pad.
        var dc = new Vector2(x + 98, y + 96);
        foreach (var (c, off) in new[] { ("Up", new Vector2(0, -12)), ("Down", new Vector2(0, 12)), ("Left", new Vector2(-12, 0)), ("Right", new Vector2(12, 0)) })
        {
            bool down = Controls(d).First(k => k.control == c).down;
            Raylib.DrawRectangleRounded(new Rectangle(dc.X + off.X - 6, dc.Y + off.Y - 6, 12, 12), 0.3f, 4, down ? U.Col(255, 215, 80) : U.Col(70, 90, 125));
            Feedback(d, c, dc + off, 7);
        }

        // Select / Start.
        var sel = new Vector2(x + w / 2 - 14, y + 70);
        var sta = new Vector2(x + w / 2 + 14, y + 70);
        foreach (var (c, pos) in new[] { ("Select", sel), ("Start", sta) })
        {
            bool down = Controls(d).First(k => k.control == c).down;
            Raylib.DrawCircleV(pos, 7, down ? U.Col(255, 215, 80) : U.Col(70, 90, 125));
            Feedback(d, c, pos, 7);
        }

        // Face buttons as a diamond, in this controller's own style.
        var faces = Prompts.FaceButtons(fam);
        var fc = new Vector2(x + w - 56, y + 94);
        (string c, Vector2 off, string icon)[] diamond =
        {
            ("A", new Vector2(0, 22), faces[0]), ("B", new Vector2(22, 0), faces[1]),
            ("X", new Vector2(-22, 0), faces[2]), ("Y", new Vector2(0, -22), faces[3]),
        };
        foreach (var (c, off, icon) in diamond)
        {
            bool down = Controls(d).First(k => k.control == c).down;
            var pos = fc + off;
            Prompts.DrawIcon(icon, (int)pos.X - 16, (int)pos.Y - 16, 32, down ? 1f : 0.3f);
            Feedback(d, c, pos, 13);
        }
    }

    void DrawKeyboard(InputDevice d, int x, int y, int w, float t)
    {
        var dev = Device.Keyboard(d.Kind == DeviceKind.KeyboardA ? Device.KeysP1 : Device.KeysP2);
        var move = Prompts.Binding(dev, Act.Move)[0];   // up, left, down, right
        var mc = new Vector2(x + 58, y + 88);
        (string c, Vector2 off)[] keys = { ("Up", new Vector2(0, -18)), ("Left", new Vector2(-32, 16)), ("Down", new Vector2(0, 16)), ("Right", new Vector2(32, 16)) };
        for (int i = 0; i < 4; i++)
        {
            bool down = Controls(d).First(k => k.control == keys[i].c).down;
            var pos = mc + keys[i].off;
            Prompts.DrawIcon(move[i], (int)pos.X - 15, (int)pos.Y - 15, 30, down ? 1f : 0.3f);
            Feedback(d, keys[i].c, pos, 12);
        }

        (string c, Act act)[] actions = { ("Jump", Act.Jump), ("Kick", Act.Kick), ("Dash", Act.Dash), ("Slap", Act.Slap) };
        for (int i = 0; i < actions.Length; i++)
        {
            int ax = x + w / 2 + 4 + (i % 2) * ((w / 2 - 10) / 2), ay = y + 50 + (i / 2) * 40;
            bool down = Controls(d).First(k => k.control == actions[i].c).down;
            var icon = Prompts.Binding(dev, actions[i].act)[0][0];
            Prompts.DrawIcon(icon, ax, ay, 30, down ? 1f : 0.3f);
            Raylib.DrawText(actions[i].c, ax + 30, ay + 9, 13, down ? Color.White : U.Col(130, 145, 170));
            Feedback(d, actions[i].c, new Vector2(ax + 15, ay + 15), 12);
        }
    }

    static string Fit(string text, int size, int maxW)
    {
        if (Raylib.MeasureText(text, size) <= maxW) return text;
        while (text.Length > 3 && Raylib.MeasureText(text + "...", size) > maxW) text = text[..^1];
        return text.TrimEnd() + "...";
    }
}
