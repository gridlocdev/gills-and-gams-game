using System.Numerics;
using Raylib_cs;

namespace FishLegs;

public enum DummyMode { Statue, Cpu, Off }

// Training Room: practise inputs and movement tech with an input display, history and a tech checklist.
class Training
{
    public DummyMode Dummy = DummyMode.Statue;
    public bool NoCooldowns;
    public bool ShowInputs = true;
    public int MenuSel;
    public float BallResetAt = -1;

    public static readonly Vector3 DummyHome = new(6, 0, 0);

    record struct HistoryEntry(Act Act, string Label, float Time);

    readonly List<HistoryEntry> history = new();
    readonly Dictionary<string, int> counts = new();
    string feedback = "";
    Color feedbackCol = Color.White;
    float feedbackAge = 99;
    float dummyAwayFor;

    public static readonly (string Key, string Name, string Hint)[] Techs =
    {
        ("airjump", "Double Jump", "Jump again in mid-air"),
        ("wavedash", "Wavedash", "Dash within 150 ms of landing"),
        ("dive", "Dive", "Dash while airborne"),
        ("slide", "Belly Slide", "Land a dive"),
        ("flophop", "Flop Hop", "Jump during a belly slide"),
        ("wallkick", "Wall Kick", "Jump next to a wall in mid-air"),
        ("fullkick", "Full Charge Kick", "Hold kick until the arrow glows"),
        ("airkick", "Scissor Shins", "Kick the ball in mid-air"),
        ("flopshot", "Flop Shot", "Dive or slide into the ball"),
        ("slap", "Tail Slap Hit", "Slap the dummy"),
        ("shin", "Shin Kick", "Kick the dummy with no ball nearby"),
        ("tackle", "Flop Tackle", "Slide into the dummy"),
        ("goal", "Goal", "Put it in the net"),
    };

    public static readonly string[] MenuItems = { "Resume", "Dummy", "No cooldowns", "Input display", "Reset ball", "Reset checklist", "Quit to title" };

    public void ResetChecklist()
    {
        counts.Clear();
        history.Clear();
        Feedback("Checklist cleared. Fresh legs.", U.Col(200, 220, 255));
    }

    void Feedback(string text, Color c)
    {
        feedback = text;
        feedbackCol = c;
        feedbackAge = 0;
    }

    public void Update(float dt) => feedbackAge += dt;

    // ------------------------------------------------------------------ events from the game

    public void OnTech(string key)
    {
        if (!Techs.Any(t => t.Key == key)) return;
        int n = counts.GetValueOrDefault(key) + 1;
        counts[key] = n;
        if (n == 1)
        {
            var name = Techs.First(t => t.Key == key).Name;
            Feedback($"NEW: {name}!", U.Col(140, 255, 170));
            Audio.Play(Sfx.Select, 1.4f, 0.6f);
        }
    }

    public void OnDashTiming(float sinceLand, float landImpact, bool wave)
    {
        if (landImpact <= 5 || sinceLand > 0.6f) return;   // not a wavedash attempt
        int ms = (int)(sinceLand * 1000);
        if (wave) Feedback($"WAVEDASH - dashed {ms} ms after landing (window 150 ms)", U.Col(255, 230, 90));
        else Feedback($"Too late for a wavedash - {ms} ms after landing (window 150 ms)", U.Col(255, 160, 110));
    }

    // Called with this frame's fresh input (before the simulation consumes it).
    public void RecordInput(PlayerInput inp, Fish f, float time)
    {
        if (inp.JumpPressed)
            Add(Act.Jump, f.SlideTimer > 0 && f.Grounded ? "Jump  (flop hop)" : f.Grounded ? "Jump" : "Jump  (air)", time);
        if (inp.DashPressed)
            Add(Act.Dash, f.DashCd > 0 ? "Dash  (on cooldown)" : f.Grounded ? "Dash" : "Dash  (dive)", time);
        if (inp.SlapPressed)
            Add(Act.Slap, f.SlapCd > 0 ? "Slap  (on cooldown)" : "Tail Slap", time);
        if (inp.KickReleased && f.Charging)
            Add(Act.Kick, $"Kick  {(int)(f.KickCharge * 100)}%{(f.Grounded ? "" : "  (air)")}", time);
    }

    void Add(Act act, string label, float time)
    {
        history.Insert(0, new HistoryEntry(act, label, time));
        if (history.Count > 8) history.RemoveAt(history.Count - 1);
    }

    // Statue dummy wanders home after being knocked about.
    public void UpdateDummy(Fish dummy, Game g, float dt)
    {
        if (Dummy != DummyMode.Statue) { dummyAwayFor = 0; return; }
        bool away = Vector3.Distance(dummy.Pos, DummyHome) > 1f;
        dummyAwayFor = away && dummy.CanAct && dummy.Grounded ? dummyAwayFor + dt : 0;
        if (dummyAwayFor > 1.5f)
        {
            g.Fx.Puff(dummy.BodyCenter, 10);
            dummy.ResetForKickoff(DummyHome);
            g.Fx.Puff(dummy.BodyCenter, 10);
            dummyAwayFor = 0;
        }
    }

    // ------------------------------------------------------------------ HUD

    public void Render(int W, int H, float t, Device dev, PlayerInput held, Fish p1, Fish dummy, Camera3D cam)
    {
        // Title, in the spirit of every fighting game's practice mode.
        Raylib.DrawText("TRAINING ROOM", 34, 22, 56, U.Col(10, 40, 80));
        Raylib.DrawText("TRAINING ROOM", 30, 18, 56, U.Col(20, 125, 215));
        int hx = 32;
        hx += Prompts.Hint(dev, Act.Pause, "Training menu", hx, 82, 30, 18, U.Col(30, 50, 80)) + 28;
        hx += Prompts.Hint(dev, Act.Reset, "Reset ball", hx, 82, 30, 18, U.Col(30, 50, 80)) + 28;
        string dummyText = Dummy switch { DummyMode.Statue => "Dummy: statue", DummyMode.Cpu => "Dummy: CPU", _ => "Dummy: off" };
        Raylib.DrawText(dummyText + (NoCooldowns ? "   |   No cooldowns" : ""), hx, 88, 18, U.Col(30, 50, 80));

        if (Dummy != DummyMode.Off)
        {
            var sp = Raylib.GetWorldToScreen(dummy.BodyCenter + new Vector3(0, 1.5f * dummy.Sc, 0), cam);
            U.TextCentered(Dummy == DummyMode.Cpu ? "CPU" : "DUMMY", (int)sp.X, (int)sp.Y - 20, 16, U.Col(255, 255, 255), 2);
        }

        if (ShowInputs) DrawInputs(H, dev, held, p1, t);
        DrawChecklist(W);

        if (feedbackAge < 3.5f && feedback.Length > 0)
        {
            float a = MathF.Min(1, (3.5f - feedbackAge) * 2);
            int fw = Raylib.MeasureText(feedback, 22) + 40;
            Raylib.DrawRectangleRounded(new Rectangle(W / 2 - fw / 2, H - 70, fw, 40), 0.5f, 8, new Color(10, 25, 50, (int)(210 * a)));
            U.TextCentered(feedback, W / 2, H - 61, 22, U.WithAlpha(feedbackCol, a), 2);
        }
    }

    void DrawInputs(int H, Device dev, PlayerInput held, Fish p1, float t)
    {
        int x = 20, w = 300;
        int h = 150 + history.Count * 26;
        int y = H - h - 20;
        Raylib.DrawRectangleRounded(new Rectangle(x, y, w, h), 0.06f, 8, new Color(10, 25, 50, 200));
        Raylib.DrawText("INPUTS", x + 14, y + 10, 16, U.Col(150, 200, 255));

        // Stick
        var c = new Vector2(x + 58, y + 82);
        Raylib.DrawCircleLinesV(c, 34, U.Col(120, 150, 190));
        Raylib.DrawLineV(c - new Vector2(34, 0), c + new Vector2(34, 0), U.Col(60, 80, 110));
        Raylib.DrawLineV(c - new Vector2(0, 34), c + new Vector2(0, 34), U.Col(60, 80, 110));
        var m = held.Move;
        Raylib.DrawCircleV(c + new Vector2(m.X, -m.Y) * 34, 10, m.LengthSquared() > 0.01f ? U.Col(255, 215, 80) : U.Col(140, 150, 170));

        // Action buttons, lit while held
        (Act act, bool down)[] buttons =
        {
            (Act.Jump, held.JumpDown), (Act.Kick, held.KickDown), (Act.Dash, held.DashDown), (Act.Slap, held.SlapDown),
        };
        for (int i = 0; i < buttons.Length; i++)
        {
            int bx = x + 114 + (i % 2) * 90, by = y + 40 + (i / 2) * 50;
            var icons = Prompts.Binding(dev, buttons[i].act);
            if (icons.Length > 0) Prompts.DrawIcon(icons[0][0], bx, by, 38, buttons[i].down ? 1f : 0.3f);
            Raylib.DrawText(buttons[i].act.ToString(), bx + 40, by + 12, 14, buttons[i].down ? Color.White : U.Col(130, 145, 170));
        }
        if (p1.Charging)
        {
            Raylib.DrawRectangle(x + 114, y + 140, 170, 6, new Color(0, 0, 0, 150));
            Raylib.DrawRectangle(x + 114, y + 140, (int)(170 * p1.KickCharge), 6, p1.KickCharge >= 0.99f ? U.Col(255, 240, 120) : U.Col(255, 130, 60));
        }

        // History, newest first, with time since the previous input.
        int hy = y + 150;
        for (int i = 0; i < history.Count; i++)
        {
            var e = history[i];
            float alpha = 1 - i * 0.06f;
            var icons = Prompts.Binding(dev, e.Act);
            if (icons.Length > 0) Prompts.DrawIcon(icons[0][0], x + 12, hy - 4, 26, alpha);
            Raylib.DrawText(e.Label, x + 44, hy + 2, 16, U.WithAlpha(Color.White, alpha));
            if (i + 1 < history.Count)
            {
                int gap = (int)((e.Time - history[i + 1].Time) * 1000);
                string gs = gap < 1000 ? $"+{gap} ms" : $"+{gap / 1000f:0.0} s";
                Raylib.DrawText(gs, x + w - 14 - Raylib.MeasureText(gs, 14), hy + 3, 14, U.WithAlpha(U.Col(150, 170, 200), alpha));
            }
            hy += 26;
        }
    }

    void DrawChecklist(int W)
    {
        const int row = 24;
        int w = 250, x = W - w - 20, y = 20;
        var next = Techs.FirstOrDefault(tk => !counts.ContainsKey(tk.Key));
        int h = 42 + Techs.Length * row + (next.Key != null ? 44 : 8);
        Raylib.DrawRectangleRounded(new Rectangle(x, y, w, h), 0.05f, 8, new Color(10, 25, 50, 190));
        int done = Techs.Count(tk => counts.ContainsKey(tk.Key));
        Raylib.DrawText($"MOVEMENT TECH  {done}/{Techs.Length}", x + 12, y + 12, 16, U.Col(150, 200, 255));
        int ry = y + 38;
        foreach (var (key, name, _) in Techs)
        {
            int n = counts.GetValueOrDefault(key);
            bool ok = n > 0;
            Raylib.DrawRectangleRounded(new Rectangle(x + 12, ry + 2, 15, 15), 0.3f, 4, ok ? U.Col(90, 210, 120) : U.Col(40, 55, 80));
            if (ok)
            {
                Raylib.DrawLineEx(new Vector2(x + 15, ry + 9), new Vector2(x + 19, ry + 13), 2.5f, Color.White);
                Raylib.DrawLineEx(new Vector2(x + 19, ry + 13), new Vector2(x + 25, ry + 5), 2.5f, Color.White);
            }
            bool isNext = key == next.Key;
            Raylib.DrawText(name, x + 36, ry + 1, 16, ok ? Color.White : isNext ? U.Col(255, 215, 80) : U.Col(170, 185, 210));
            if (n > 0)
            {
                string cs = $"x{n}";
                Raylib.DrawText(cs, x + w - 12 - Raylib.MeasureText(cs, 14), ry + 2, 14, U.Col(255, 215, 80));
            }
            ry += row;
        }
        if (next.Key != null)
        {
            Raylib.DrawText("NEXT:", x + 12, ry + 6, 14, U.Col(255, 215, 80));
            Raylib.DrawText(next.Hint, x + 12, ry + 24, 14, U.Col(200, 215, 235));
        }
    }

    public void RenderMenu(int W, int H, float t, Device dev)
    {
        Raylib.DrawRectangle(0, 0, W, H, new Color(5, 15, 35, 160));
        int w = 440, h = 60 + MenuItems.Length * 50 + 50;
        int x = W / 2 - w / 2, y = H / 2 - h / 2;
        Raylib.DrawRectangleRounded(new Rectangle(x, y, w, h), 0.08f, 8, new Color(12, 30, 60, 240));
        U.TextCentered("TRAINING MENU", W / 2, y + 16, 30, U.Col(90, 170, 255));
        for (int i = 0; i < MenuItems.Length; i++)
        {
            int iy = y + 64 + i * 50;
            bool on = i == MenuSel;
            if (on) Raylib.DrawRectangleRounded(new Rectangle(x + 20, iy - 6, w - 40, 42), 0.4f, 8, U.WithAlpha(U.Col(90, 170, 255), 0.25f + 0.1f * MathF.Sin(t * 6)));
            string value = i switch
            {
                1 => Dummy switch { DummyMode.Statue => "Statue", DummyMode.Cpu => "CPU", _ => "Off" },
                2 => NoCooldowns ? "On" : "Off",
                3 => ShowInputs ? "On" : "Off",
                _ => "",
            };
            Raylib.DrawText(MenuItems[i], x + 40, iy + 2, 24, on ? Color.White : U.Col(190, 205, 225));
            if (value.Length > 0)
                Raylib.DrawText(value, x + w - 40 - Raylib.MeasureText(value, 24), iy + 2, 24, U.Col(255, 215, 80));
        }
        Prompts.HintRow(W / 2, y + h - 44, 30, 18, U.Col(200, 215, 240), 30,
            (dev, Act.Navigate, "Choose"), (dev, Act.Confirm, "Select / change"), (dev, Act.Pause, "Resume"));
    }
}
