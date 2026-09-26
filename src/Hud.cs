using System.Numerics;
using Raylib_cs;

namespace FishLegs;

static class Hud
{
    static readonly Color Panel = new(15, 25, 45, 200);
    static readonly Color Gold = U.Col(255, 215, 80);

    static void Box(float x, float y, float w, float h, Color c, float round = 0.2f) =>
        Raylib.DrawRectangleRounded(new Rectangle(x, y, w, h), round, 8, c);

    static List<string> Wrap(string text, int size, int maxW)
    {
        var lines = new List<string>();
        var cur = "";
        foreach (var word in text.Split(' '))
        {
            var test = cur.Length == 0 ? word : cur + " " + word;
            if (Raylib.MeasureText(test, size) > maxW && cur.Length > 0) { lines.Add(cur); cur = word; }
            else cur = test;
        }
        if (cur.Length > 0) lines.Add(cur);
        return lines;
    }

    // Wobbly title text, one letter at a time.
    static void WobblyText(string s, int cx, int y, int size, Color c, float t, float amp = 6)
    {
        int total = Raylib.MeasureText(s, size) + s.Length * 2;
        int x = cx - total / 2;
        for (int i = 0; i < s.Length; i++)
        {
            string ch = s[i].ToString();
            int dy = (int)(MathF.Sin(t * 5 + i * 0.6f) * amp);
            U.TextShadow(ch, x, y + dy, size, c, Math.Max(3, size / 14));
            x += Raylib.MeasureText(ch, size) + 2;
        }
    }

    public static void Title(int W, int H, float t, string[] items, int sel)
    {
        Raylib.DrawRectangle(0, 0, W, H, new Color(0, 20, 50, 70));
        WobblyText("GILLS & GAMS", W / 2, 70, 96, Gold, t);
        U.TextCentered("Competitive Fish-Leg Football", W / 2, 180, 30, Color.White);
        U.TextCentered("\"It's like football, but worse, and wetter.\"  - Barry Barracuda", W / 2, 218, 20, U.Col(200, 230, 255));

        int y0 = H / 2 + 10;
        Box(W / 2 - 260, y0 - 24, 520, items.Length * 58 + 30, Panel);
        for (int i = 0; i < items.Length; i++)
        {
            bool on = i == sel;
            int y = y0 + i * 58;
            if (on)
            {
                float pulse = 0.5f + 0.5f * MathF.Sin(t * 6);
                Box(W / 2 - 240, y - 8, 480, 48, U.WithAlpha(Gold, 0.25f + pulse * 0.15f));
                U.TextShadow(">", W / 2 - 230 + (int)(pulse * 6), y, 32, Gold);
            }
            U.TextCentered(items[i], W / 2, y, 30, on ? Gold : U.Col(220, 230, 240));
        }
        U.TextCentered("W/S or arrows to choose  -  Enter / Space to confirm  -  Gamepads supported", W / 2, H - 40, 18, U.Col(230, 240, 255));
    }

    public static void HowTo(int W, int H)
    {
        Raylib.DrawRectangle(0, 0, W, H, new Color(5, 15, 35, 225));
        U.TextCentered("HOW TO FOOTBALL (WITH LEGS)", W / 2, 30, 44, Gold);

        string[,] rows =
        {
            { "ACTION", "PLAYER 1", "PLAYER 2", "GAMEPAD" },
            { "Move", "W A S D", "Arrow keys", "Left stick" },
            { "Kick (hold to charge)", "B  or  E", "L", "X / RT" },
            { "Jump (+ air jump)", "V  or  Space", ";", "A" },
            { "Dash  /  Dive (in air)", "N  or  L-Shift", "K", "B / RB" },
            { "Tail Slap (spin)", "C  or  Q", "J", "Y / LB" },
        };
        int[] cols = { 90, 440, 680, 900 };
        int y = 100;
        for (int r = 0; r < rows.GetLength(0); r++)
        {
            for (int c = 0; c < 4; c++)
                U.TextShadow(rows[r, c], cols[c], y, 22, r == 0 ? U.Col(150, 200, 255) : Color.White, 2);
            y += r == 0 ? 34 : 30;
        }
        U.TextShadow("(In 1-player mode you can use either keyboard layout.)", cols[0], y + 2, 18, U.Col(180, 190, 210), 2);

        y += 44;
        U.TextShadow("MOVEMENT TECH", 90, y, 28, Gold);
        y += 38;
        string[] tech =
        {
            "WAVEDASH  -  Dash right as you land from a jump: faster dash, cheaper cooldown.",
            "DIVE  -  Dash in mid-air to belly flop forward. You land in a BELLY SLIDE.",
            "FLOP HOP  -  Jump out of a belly slide to launch with bonus speed. Chain it: jump > dive > hop > dive...",
            "WALL KICK  -  Jump while touching a wall in mid-air. Refreshes your air jumps.",
            "FLOP SHOT / FLOP TACKLE  -  Slam belly-first into the ball or the other fish.",
            "SCISSOR SHINS  -  Kick in mid-air for an overhead volley.  Kick their SHINS to stun them.",
            "TAIL SLAP  -  Spin attack. Knocks away the ball and flattens anyone nearby.",
        };
        foreach (var line in tech)
        {
            U.TextShadow(line, 100, y, 20, Color.White, 2);
            y += 30;
        }
        y += 14;
        U.TextShadow("First to 5 goals. After every goal both fish pick a MUTATION. The scored-on fish gets a bonus pity option.", 90, y, 20, U.Col(255, 200, 150), 2);
        U.TextCentered("Press Enter / Space to go back", W / 2, H - 44, 22, Gold);
    }

    public static void Scoreboard(int W, Fish[] fish, int[] score, bool vsCpu)
    {
        int w = 620, h = 64;
        int x = W / 2 - w / 2, y = 12;
        Box(x, y, w, h, Panel, 0.4f);
        Box(x + 6, y + 6, 240, h - 12, U.WithAlpha(fish[0].TeamCol, 0.85f), 0.4f);
        Box(x + w - 246, y + 6, 240, h - 12, U.WithAlpha(fish[1].TeamCol, 0.85f), 0.4f);
        U.TextCentered(fish[0].Name, x + 126, y + 14, 20, Color.White, 2);
        U.TextCentered(fish[1].Name, x + w - 126, y + 14, 20, Color.White, 2);
        U.TextCentered("P1", x + 126, y + 38, 14, U.Col(255, 255, 255, 200), 1);
        U.TextCentered(vsCpu ? "ROBO-TROUT (CPU)" : "P2", x + w - 126, y + 38, 14, U.Col(255, 255, 255, 200), 1);
        U.TextCentered($"{score[0]} - {score[1]}", W / 2, y + 12, 42, Color.White);
        U.TextCentered($"first to {Game.WinScore}", W / 2, y + h + 4, 14, U.Col(255, 255, 255, 190), 1);
    }

    public static void Upgrades(int W, Fish[] fish)
    {
        for (int i = 0; i < 2; i++)
        {
            var owned = fish[i].Owned;
            if (owned.Count == 0) continue;
            int y = 90;
            foreach (var u in owned)
            {
                int tw = Raylib.MeasureText(u.Name, 14) + 22;
                int x = i == 0 ? 14 : W - 14 - tw;
                Box(x, y, tw, 22, new Color(10, 20, 40, 170), 0.5f);
                Raylib.DrawCircle(x + 10, y + 11, 5, u.Color);
                Raylib.DrawText(u.Name, x + 18, y + 4, 14, Color.White);
                y += 26;
            }
        }
    }

    public static void OverHead(Camera3D cam, Fish[] fish, bool vsCpu)
    {
        foreach (var f in fish)
        {
            var sp = Raylib.GetWorldToScreen(f.BodyCenter + new Vector3(0, 1.5f * f.Sc, 0), cam);
            string tag = f.Id == 0 ? "P1" : vsCpu ? "CPU" : "P2";
            int tw = Raylib.MeasureText(tag, 16);
            Box(sp.X - tw / 2f - 6, sp.Y - 26, tw + 12, 20, U.WithAlpha(f.TeamCol, 0.9f), 0.5f);
            Raylib.DrawText(tag, (int)(sp.X - tw / 2f), (int)sp.Y - 24, 16, Color.White);
            Raylib.DrawTriangle(new Vector2(sp.X - 5, sp.Y - 6), new Vector2(sp.X, sp.Y), new Vector2(sp.X + 5, sp.Y - 6), U.WithAlpha(f.TeamCol, 0.9f));

            // Dash cooldown pip bar + air jumps
            float dashFrac = 1 - f.DashCd / f.S.DashCooldown;
            Raylib.DrawRectangle((int)sp.X - 22, (int)sp.Y + 4, 44, 5, new Color(0, 0, 0, 140));
            Raylib.DrawRectangle((int)sp.X - 22, (int)sp.Y + 4, (int)(44 * dashFrac), 5, dashFrac >= 1 ? U.Col(120, 255, 200) : U.Col(90, 140, 160));
            if (f.SlapCd <= 0) Raylib.DrawCircle((int)sp.X + 28, (int)sp.Y + 6, 4, U.Col(255, 150, 220));

            if (f.Charging)
            {
                float c = f.KickCharge;
                var col = c >= 0.99f ? U.LerpColor(Gold, Color.White, 0.5f + 0.5f * MathF.Sin((float)Raylib.GetTime() * 30)) : U.LerpColor(U.Col(255, 255, 255), U.Col(255, 90, 40), c);
                Raylib.DrawRectangle((int)sp.X - 32, (int)sp.Y - 44, 64, 10, new Color(0, 0, 0, 160));
                Raylib.DrawRectangle((int)sp.X - 31, (int)sp.Y - 43, (int)(62 * c), 8, col);
            }
            if (f.StunTimer > 0)
                U.TextCentered("@_@", (int)sp.X, (int)sp.Y - 50, 22, Gold, 2);
        }
    }

    public static void Comment(int W, int H, string text, float age)
    {
        if (string.IsNullOrEmpty(text) || age > 6) return;
        float a = MathF.Min(1, MathF.Min(age * 5, (6 - age) * 2));
        var lines = Wrap(text, 20, W - 300);
        int h = 36 + lines.Count * 24;
        int y = H - h - 14;
        Box(90, y, W - 180, h, U.WithAlpha(Panel, 0.8f * a), 0.3f);
        Raylib.DrawText("BARRY BARRACUDA, COMMENTATOR:", 110, y + 8, 14, U.WithAlpha(Gold, a));
        for (int i = 0; i < lines.Count; i++)
            Raylib.DrawText(lines[i], 110, y + 26 + i * 24, 20, U.WithAlpha(Color.White, a));
    }

    public static void Countdown(int W, int H, float t, bool showControls)
    {
        string s = t < 0.5f ? "3" : t < 1.0f ? "2" : t < 1.5f ? "1" : "FLOP!";
        float local = t < 1.5f ? (t % 0.5f) / 0.5f : (t - 1.5f) / 0.5f;
        int size = (int)(120 * (1.3f - local * 0.3f));
        U.TextCentered(s, W / 2, H / 2 - size / 2 - 40, size, U.WithAlpha(Gold, 1 - local * 0.5f), 6);

        if (showControls)
        {
            Box(14, H - 250, 330, 118, Panel, 0.2f);
            string[] lines =
            {
                "P1: WASD  kick B/E  jump V/Space",
                "    dash N/Shift  slap C/Q",
                "P2: Arrows  kick L  jump ;",
                "    dash K  slap J",
                "Hold kick to charge. Try the tech!",
            };
            for (int i = 0; i < lines.Length; i++) Raylib.DrawText(lines[i], 26, H - 240 + i * 21, 16, Color.White);
        }
    }

    public static void GoalBanner(int W, int H, float t, Fish scorer, Fish[] fish, int[] score)
    {
        float grow = U.Clamp01(t * 4);
        int size = (int)(130 * (0.5f + 0.5f * grow));
        int y = 110;
        WobblyText("GOOOOAL!", W / 2, y, size, scorer.TeamCol, t, 10 * grow);
        U.TextCentered($"{scorer.Name} SCORES", W / 2, y + size + 10, 36, Color.White);
        U.TextCentered($"{fish[0].Name}  {score[0]} - {score[1]}  {fish[1].Name}", W / 2, y + size + 54, 24, U.Col(220, 235, 255));
    }

    public static void Paused(int W, int H)
    {
        Raylib.DrawRectangle(0, 0, W, H, new Color(0, 10, 30, 170));
        U.TextCentered("PAUSED", W / 2, H / 2 - 80, 80, Gold);
        U.TextCentered("The fish are breathing heavily. Through what, nobody knows.", W / 2, H / 2 + 10, 22, Color.White);
        U.TextCentered("Esc / P: resume     Enter: quit to title", W / 2, H / 2 + 60, 22, U.Col(200, 220, 255));
    }

    public static void Draft(int W, int H, float t, Fish[] fish, List<Upgrade>[] options, int[] cursor, bool[] ready, int lastScorer, bool vsCpu)
    {
        Raylib.DrawRectangle(0, 0, W, H, new Color(5, 10, 30, 170));
        WobblyText("LOCKER ROOM MUTATIONS", W / 2, 96, 46, Gold, t, 4);
        U.TextCentered("Pick one. Side effects may include: more legs.", W / 2, 148, 20, U.Col(210, 225, 255));

        int colW = Math.Min(540, W / 2 - 60);
        for (int i = 0; i < 2; i++)
        {
            var f = fish[i];
            int x = i == 0 ? W / 2 - colW - 20 : W / 2 + 20;
            int y = 190;
            U.TextShadow(f.Name, x, y, 28, f.TeamCol);
            if (i != lastScorer)
                U.TextShadow("+1 pity option (you got scored on)", x, y + 32, 16, U.Col(255, 190, 150), 2);
            y += 60;

            var opts = options[i];
            for (int k = 0; k < opts.Count; k++)
            {
                var u = opts[k];
                bool on = cursor[i] == k;
                int cx = x + (on ? 14 : 0);
                int ch = 88;
                Box(cx, y, colW - 14, ch, on ? new Color(40, 60, 100, 240) : new Color(20, 30, 55, 210), 0.18f);
                if (on) Raylib.DrawRectangleRoundedLinesEx(new Rectangle(cx, y, colW - 14, ch), 0.18f, 8, 3, ready[i] ? Gold : f.TeamCol);
                Raylib.DrawRectangleRounded(new Rectangle(cx + 10, y + 12, 12, ch - 24), 0.5f, 4, u.Color);
                Raylib.DrawText(u.Name, cx + 34, y + 10, 24, on ? Color.White : U.Col(210, 215, 225));
                if (u.Unique) Raylib.DrawText("UNIQUE", cx + colW - 90, y + 14, 14, U.Col(255, 200, 120));
                Raylib.DrawText(u.Desc, cx + 34, y + 40, 18, U.Col(170, 230, 190));
                Raylib.DrawText(u.Flavor, cx + 34, y + 64, 16, U.Col(160, 170, 190));
                y += ch + 10;
            }

            if (ready[i])
            {
                float p = 0.5f + 0.5f * MathF.Sin(t * 8);
                U.TextShadow("READY!", x + colW - 140, 196, 30, U.LerpColor(Gold, Color.White, p));
            }
            else
            {
                string hint = i == 0 ? "W/S + B/E/V to pick" : vsCpu ? "CPU is thinking (sort of)..." : "Up/Down + L/; to pick";
                Raylib.DrawText(hint, x, y + 4, 18, U.Col(200, 210, 230));
            }
        }
    }

    public static void Victory(int W, int H, float t, Fish winner, Fish loser, int[] score, Dictionary<string, int>[] tech, float st)
    {
        Raylib.DrawRectangle(0, 0, W, H, new Color(0, 10, 30, 110));
        WobblyText($"{winner.Name} WINS!", W / 2, 60, 72, winner.TeamCol, t, 8);
        U.TextCentered($"{score[0]} - {score[1]}", W / 2, 150, 48, Color.White);

        int y = 230;
        int bw = 460;
        for (int i = 0; i < 2; i++)
        {
            var f = i == 0 ? winner : loser;
            var d = tech[f.Id];
            int x = i == 0 ? W / 2 - bw - 20 : W / 2 + 20;
            Box(x, y, bw, 250, Panel, 0.1f);
            U.TextShadow(i == 0 ? "CHAMPION" : "FUTURE SUSHI", x + 20, y + 14, 22, i == 0 ? Gold : U.Col(200, 170, 170), 2);
            U.TextShadow(f.Name, x + 20, y + 42, 26, f.TeamCol, 2);
            int hairs = (int)(20 * f.S.HairDensity) * 4;
            string[] stats =
            {
                $"Wavedashes: {d.GetValueOrDefault("wavedash")}",
                $"Flop Hops: {d.GetValueOrDefault("flophop")}    Wall Kicks: {d.GetValueOrDefault("wallkick")}",
                $"Flop Shots: {d.GetValueOrDefault("flopshot")}    Flop Tackles: {d.GetValueOrDefault("tackle")}",
                $"Air kicks: {d.GetValueOrDefault("airkick")}    Own goals: {d.GetValueOrDefault("owngoal")}",
                $"Mutations: {f.Owned.Count}    Visible leg hairs: {hairs}",
            };
            for (int k = 0; k < stats.Length; k++)
                Raylib.DrawText(stats[k], x + 20, y + 84 + k * 30, 18, Color.White);
        }
        if (st > 1.5f)
            U.TextCentered("Press Enter / Space to return to the title screen", W / 2, H - 150, 22, U.WithAlpha(Gold, 0.6f + 0.4f * MathF.Sin(t * 5)));
    }
}
