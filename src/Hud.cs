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

    public static void Title(int W, int H, float t, string[] items, int sel, Device dev)
    {
        Raylib.DrawRectangle(0, 0, W, H, new Color(0, 20, 50, 70));
        WobblyText("GILLS & GAMS", W / 2, 70, 96, Gold, t);
        U.TextCentered("Competitive Fish-Leg Football", W / 2, 180, 30, Color.White);
        U.TextCentered("\"It's like football, but worse, and wetter.\"  - Barry Barracuda", W / 2, 218, 20, U.Col(200, 230, 255));

        const int step = 46;
        int y0 = H - 92 - items.Length * step;
        Box(W / 2 - 290, y0 - 20, 580, items.Length * step + 24, Panel);
        for (int i = 0; i < items.Length; i++)
        {
            bool on = i == sel;
            int y = y0 + i * step;
            if (on)
            {
                float pulse = 0.5f + 0.5f * MathF.Sin(t * 6);
                Box(W / 2 - 270, y - 7, 540, 40, U.WithAlpha(Gold, 0.25f + pulse * 0.15f));
                U.TextShadow(">", W / 2 - 262 + (int)(pulse * 6), y, 28, Gold);
            }
            U.TextCentered(items[i], W / 2, y, 26, on ? Gold : U.Col(220, 230, 240));
        }
        Prompts.HintRow(W / 2, H - 56, 36, 20, U.Col(230, 240, 255), 36,
            (dev, Act.Navigate, "Choose"), (dev, Act.Confirm, "Confirm"));
    }

    public static void HowTo(int W, int H, Device menuDev, List<int> pads)
    {
        Raylib.DrawRectangle(0, 0, W, H, new Color(5, 15, 35, 225));
        U.TextCentered("HOW TO FOOTBALL (WITH LEGS)", W / 2, 24, 44, Gold);

        var family = menuDev.Pad ? menuDev.Family : pads.Count > 0 ? Prompts.DetectFamily(pads[0]) : PadFamily.Xbox;
        Device[] devs = { Device.Keyboard(Device.KeysP1), Device.Keyboard(Device.KeysP2), Device.Gamepad(family) };
        (string label, Act act)[] rows =
        {
            ("Move", Act.Move), ("Kick (hold to charge)", Act.Kick), ("Jump (+ air jump)", Act.Jump),
            ("Dash  /  Dive (in air)", Act.Dash), ("Tail Slap (spin)", Act.Slap),
        };
        int[] cols = { 90, 400, 640, 870 };
        int y = 86;
        string[] heads = { "ACTION", "PLAYER 1", "PLAYER 2", "GAMEPAD" };
        for (int c = 0; c < 4; c++) U.TextShadow(heads[c], cols[c], y, 20, U.Col(150, 200, 255), 2);
        y += 28;
        const int icon = 34;
        foreach (var (label, act) in rows)
        {
            U.TextShadow(label, cols[0], y + icon / 2 - 11, 22, Color.White, 2);
            for (int c = 0; c < 3; c++) Prompts.Draw(devs[c], act, cols[c + 1], y, icon);
            y += icon + 4;
        }
        U.TextShadow("Gamepads: in 1P any controller works. For couch and 2v2 games, pick who uses which controller in CONTROLLERS on the title screen.",
            cols[0], y + 4, 16, U.Col(180, 190, 210), 2);
        string padList = pads.Count == 0 ? "No controllers connected." : "Connected: " + string.Join(", ", pads.Select(p => Pads.Name(p)));
        U.TextShadow(padList, cols[0], y + 26, 16, U.Col(150, 230, 180), 2);

        y += 58;
        U.TextShadow("MOVEMENT TECH", 90, y, 26, Gold);
        y += 34;
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
            U.TextShadow(line, 100, y, 19, Color.White, 2);
            y += 27;
        }
        y += 8;
        U.TextShadow("First to 5 goals. After every goal both fish pick a MUTATION. The scored-on fish gets a bonus pity option.", 90, y, 19, U.Col(255, 200, 150), 2);
        Prompts.HintRow(W / 2, H - 52, 34, 20, Gold, 30, (menuDev, Act.Back, "Back"));
    }

    public static void Scoreboard(int W, (string name, string sub)[] teams, Color[] teamCols, int[] score)
    {
        int w = 620, h = 64;
        int x = W / 2 - w / 2, y = 12;
        Box(x, y, w, h, Panel, 0.4f);
        Box(x + 6, y + 6, 240, h - 12, U.WithAlpha(teamCols[0], 0.85f), 0.4f);
        Box(x + w - 246, y + 6, 240, h - 12, U.WithAlpha(teamCols[1], 0.85f), 0.4f);
        U.TextCentered(teams[0].name, x + 126, y + 14, 20, Color.White, 2);
        U.TextCentered(teams[1].name, x + w - 126, y + 14, 20, Color.White, 2);
        U.TextCentered(teams[0].sub, x + 126, y + 38, 14, U.Col(255, 255, 255, 200), 1);
        U.TextCentered(teams[1].sub, x + w - 126, y + 38, 14, U.Col(255, 255, 255, 200), 1);
        U.TextCentered($"{score[0]} - {score[1]}", W / 2, y + 12, 42, Color.White);
        U.TextCentered($"first to {Game.WinScore}", W / 2, y + h + 4, 14, U.Col(255, 255, 255, 190), 1);
    }

    public static void Upgrades(int W, Fish[] fish, bool showNames)
    {
        int[] y = { 90, 90 };
        foreach (var f in fish)
        {
            if (f.Owned.Count == 0) continue;
            int side = f.Team;
            if (showNames)
            {
                int nw = Raylib.MeasureText(f.Name, 13);
                Raylib.DrawText(f.Name, side == 0 ? 16 : W - 16 - nw, y[side], 13, U.WithAlpha(f.BodyCol, 0.95f));
                y[side] += 17;
            }
            foreach (var u in f.Owned)
            {
                int tw = Raylib.MeasureText(u.Name, 14) + 22;
                int x = side == 0 ? 14 : W - 14 - tw;
                Box(x, y[side], tw, 22, new Color(10, 20, 40, 170), 0.5f);
                Raylib.DrawCircle(x + 10, y[side] + 11, 5, u.Color);
                Raylib.DrawText(u.Name, x + 18, y[side] + 4, 14, Color.White);
                y[side] += 26;
            }
            y[side] += 6;
        }
    }

    public static void OverHead(Camera3D cam, Fish[] fish, string[] tags)
    {
        foreach (var f in fish)
        {
            var sp = Raylib.GetWorldToScreen(f.BodyCenter + new Vector3(0, 1.5f * f.Sc, 0), cam);
            string tag = tags[f.Id];
            int tw = Raylib.MeasureText(tag, 16);
            Box(sp.X - tw / 2f - 6, sp.Y - 26, tw + 12, 20, U.WithAlpha(f.TeamCol, 0.9f), 0.5f);
            Raylib.DrawText(tag, (int)(sp.X - tw / 2f), (int)sp.Y - 24, 16, Color.White);
            Raylib.DrawTriangle(new Vector2(sp.X - 5, sp.Y - 6), new Vector2(sp.X, sp.Y), new Vector2(sp.X + 5, sp.Y - 6), U.WithAlpha(f.TeamCol, 0.9f));

            // Dash cooldown pip bar + slap ready dot
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

    public static void Countdown(int W, int H, float t, bool showControls, (string label, Device dev)[] players)
    {
        string s = t < 0.5f ? "3" : t < 1.0f ? "2" : t < 1.5f ? "1" : "FLOP!";
        float local = t < 1.5f ? (t % 0.5f) / 0.5f : (t - 1.5f) / 0.5f;
        int size = (int)(120 * (1.3f - local * 0.3f));
        U.TextCentered(s, W / 2, H / 2 - size / 2 - 40, size, U.WithAlpha(Gold, 1 - local * 0.5f), 6);

        if (!showControls) return;
        const int icon = 28;
        // Up to four panels: bottom-left, bottom-right, then above them.
        for (int i = 0; i < players.Length && i < 4; i++)
        {
            var d = players[i].dev;
            int w = 300, h = 5 * (icon + 2) + 40;
            int x = i % 2 == 0 ? 14 : W - 14 - w;
            int y = H - 150 - h - (i / 2) * (h + 10);
            Box(x, y, w, h, Panel, 0.12f);
            Raylib.DrawText($"{players[i].label} CONTROLS", x + 12, y + 8, 16, Gold);
            int yy = y + 30;
            foreach (var (label, act) in new[] { ("Move", Act.Move), ("Kick (hold)", Act.Kick), ("Jump", Act.Jump), ("Dash / Dive", Act.Dash), ("Tail Slap", Act.Slap) })
            {
                Raylib.DrawText(label, x + 12, yy + icon / 2 - 8, 16, Color.White);
                Prompts.Draw(d, act, x + 130, yy, icon);
                yy += icon + 2;
            }
        }
    }

    public static void GoalBanner(int W, int H, float t, Fish scorer, string headline, (string name, string sub)[] teams, int[] score)
    {
        float grow = U.Clamp01(t * 4);
        int size = (int)(130 * (0.5f + 0.5f * grow));
        int y = 110;
        WobblyText("GOOOOAL!", W / 2, y, size, scorer.TeamCol, t, 10 * grow);
        U.TextCentered($"{headline} SCORE{(headline.StartsWith("TEAM") ? "" : "S")}", W / 2, y + size + 10, 36, Color.White);
        U.TextCentered($"{teams[0].name}  {score[0]} - {score[1]}  {teams[1].name}", W / 2, y + size + 54, 24, U.Col(220, 235, 255));
    }

    public static void Paused(int W, int H, Device dev)
    {
        Raylib.DrawRectangle(0, 0, W, H, new Color(0, 10, 30, 170));
        U.TextCentered("PAUSED", W / 2, H / 2 - 80, 80, Gold);
        U.TextCentered("The fish are breathing heavily. Through what, nobody knows.", W / 2, H / 2 + 10, 22, Color.White);
        Prompts.HintRow(W / 2, H / 2 + 56, 36, 22, U.Col(200, 220, 255), 44,
            (dev, Act.Pause, "Resume"), (dev, Act.Quit, "Quit to title"));
    }

    public static void Draft(int W, int H, float t, Fish[] fish, string[] tags, List<Upgrade>[] options, int[] cursor, bool[] ready,
        int scoringTeam, bool[] human, Device[] devs)
    {
        Raylib.DrawRectangle(0, 0, W, H, new Color(5, 10, 30, 170));
        WobblyText("LOCKER ROOM MUTATIONS", W / 2, 96, 46, Gold, t, 4);
        U.TextCentered("Pick one. Side effects may include: more legs.", W / 2, 148, 20, U.Col(210, 225, 255));

        int n = fish.Length;
        bool narrow = n > 2;
        const int gap = 20;
        int colW = Math.Min(540, (W - 40 - gap * (n - 1)) / n);
        int x0 = W / 2 - (colW * n + gap * (n - 1)) / 2;
        for (int i = 0; i < n; i++)
        {
            var f = fish[i];
            int x = x0 + i * (colW + gap);
            int y = 190;
            U.TextShadow(narrow ? $"{tags[i]}  {f.Name}" : f.Name, x, y, narrow ? 20 : 28, f.TeamCol);
            if (f.Team != scoringTeam)
                U.TextShadow("+1 pity option (you got scored on)", x, y + (narrow ? 26 : 32), narrow ? 14 : 16, U.Col(255, 190, 150), 2);
            y += 60;

            var opts = options[i];
            for (int k = 0; k < opts.Count; k++)
            {
                var u = opts[k];
                bool on = cursor[i] == k;
                int cx = x + (on ? 10 : 0);
                int ch = 88;
                int cw = colW - 10;
                Box(cx, y, cw, ch, on ? new Color(40, 60, 100, 240) : new Color(20, 30, 55, 210), 0.18f);
                if (on) Raylib.DrawRectangleRoundedLinesEx(new Rectangle(cx, y, cw, ch), 0.18f, 8, 3, ready[i] ? Gold : f.TeamCol);
                Raylib.DrawRectangleRounded(new Rectangle(cx + 10, y + 12, 12, ch - 24), 0.5f, 4, u.Color);
                Raylib.DrawText(u.Name, cx + 32, y + 10, narrow ? 20 : 24, on ? Color.White : U.Col(210, 215, 225));
                if (u.Unique && !narrow) Raylib.DrawText("UNIQUE", cx + cw - 76, y + 14, 14, U.Col(255, 200, 120));
                FitText(u.Desc, cx + 32, y + 40, narrow ? 14 : 18, cw - 42, U.Col(170, 230, 190));
                FitText(u.Flavor, cx + 32, y + 64, narrow ? 13 : 16, cw - 42, U.Col(160, 170, 190));
                y += ch + 10;
            }

            if (ready[i])
            {
                float p = 0.5f + 0.5f * MathF.Sin(t * 8);
                U.TextShadow("READY!", x + colW - (narrow ? 90 : 140), 196 + (narrow ? 24 : 0), narrow ? 22 : 30, U.LerpColor(Gold, Color.White, p));
            }
            else if (!human[i])
                Raylib.DrawText("CPU is thinking (sort of)...", x, y + 4, narrow ? 15 : 18, U.Col(200, 210, 230));
            else
            {
                int size = narrow ? 24 : 30, font = narrow ? 15 : 18;
                int hx = x + Prompts.Hint(devs[i], Act.Navigate, "choose", x, y, size, font, U.Col(200, 210, 230)) + 16;
                Prompts.Hint(devs[i], Act.Confirm, "pick", hx, y, size, font, U.Col(200, 210, 230));
            }
        }
    }

    // Draws text, trimming it with an ellipsis if it would overflow maxW.
    static void FitText(string text, int x, int y, int size, int maxW, Color c)
    {
        if (Raylib.MeasureText(text, size) > maxW)
        {
            while (text.Length > 3 && Raylib.MeasureText(text + "...", size) > maxW) text = text[..^1];
            text = text.TrimEnd() + "...";
        }
        Raylib.DrawText(text, x, y, size, c);
    }

    public static void Victory(int W, int H, float t, Fish[] fish, int winningTeam, string winnerName, int[] score,
        Dictionary<string, int>[] tech, float st, Device dev)
    {
        Raylib.DrawRectangle(0, 0, W, H, new Color(0, 10, 30, 110));
        WobblyText($"{winnerName} WIN{(winnerName.StartsWith("TEAM") ? "" : "S")}!", W / 2, 60, 72, Game.TeamCols[winningTeam], t, 8);
        U.TextCentered($"{score[0]} - {score[1]}", W / 2, 150, 48, Color.White);

        // Winners first.
        var order = fish.OrderBy(f => f.Team == winningTeam ? 0 : 1).ThenBy(f => f.Id).ToArray();
        int n = order.Length;
        bool narrow = n > 2;
        const int gap = 20;
        int bw = Math.Min(460, (W - 40 - gap * (n - 1)) / n);
        int x0 = W / 2 - (bw * n + gap * (n - 1)) / 2;
        int y = 230;
        int fs = narrow ? 15 : 18;
        for (int i = 0; i < n; i++)
        {
            var f = order[i];
            var d = tech[f.Id];
            bool won = f.Team == winningTeam;
            int x = x0 + i * (bw + gap);
            Box(x, y, bw, 250, Panel, 0.1f);
            U.TextShadow(won ? "CHAMPION" : "FUTURE SUSHI", x + 16, y + 14, 20, won ? Gold : U.Col(200, 170, 170), 2);
            U.TextShadow(f.Name, x + 16, y + 42, narrow ? 20 : 26, f.TeamCol, 2);
            int hairs = (int)(20 * f.S.HairDensity) * 4;
            string[] stats = narrow
                ?
                [
                    $"Wavedashes: {d.GetValueOrDefault("wavedash")}",
                    $"Flop Hops: {d.GetValueOrDefault("flophop")}",
                    $"Wall Kicks: {d.GetValueOrDefault("wallkick")}",
                    $"Flop Shots / Tackles: {d.GetValueOrDefault("flopshot")} / {d.GetValueOrDefault("tackle")}",
                    $"Air kicks: {d.GetValueOrDefault("airkick")}   Own goals: {d.GetValueOrDefault("owngoal")}",
                    $"Mutations: {f.Owned.Count}   Leg hairs: {hairs}",
                ]
                :
                [
                    $"Wavedashes: {d.GetValueOrDefault("wavedash")}",
                    $"Flop Hops: {d.GetValueOrDefault("flophop")}    Wall Kicks: {d.GetValueOrDefault("wallkick")}",
                    $"Flop Shots: {d.GetValueOrDefault("flopshot")}    Flop Tackles: {d.GetValueOrDefault("tackle")}",
                    $"Air kicks: {d.GetValueOrDefault("airkick")}    Own goals: {d.GetValueOrDefault("owngoal")}",
                    $"Mutations: {f.Owned.Count}    Visible leg hairs: {hairs}",
                ];
            for (int k = 0; k < stats.Length; k++)
                Raylib.DrawText(stats[k], x + 16, y + 84 + k * (narrow ? 26 : 30), fs, Color.White);
        }
        if (st > 1.5f)
            Prompts.HintRow(W / 2, H - 156, 36, 22, U.WithAlpha(Gold, 0.6f + 0.4f * MathF.Sin(t * 5)), 30, (dev, Act.Confirm, "Back to the title screen"));
    }

    public static void Toast(int W, int H, string text, float age)
    {
        if (string.IsNullOrEmpty(text) || age > 3.5f) return;
        float a = MathF.Min(1, MathF.Min(age * 6, (3.5f - age) * 2));
        int w = Raylib.MeasureText(text, 18) + 36;
        int y = (int)U.Lerp(-40, 96, MathF.Min(1, age * 5));
        Box(W / 2 - w / 2, y, w, 34, U.WithAlpha(U.Col(20, 60, 50), 0.92f * a), 0.5f);
        Raylib.DrawText(text, W / 2 - w / 2 + 18, y + 8, 18, U.WithAlpha(U.Col(170, 255, 200), a));
    }

    public static void PermissionWarning(int W, int y)
    {
        string fix = MacPermissions.Fix;
        int w = Math.Max(Raylib.MeasureText(fix, 16), Raylib.MeasureText(MacPermissions.Why, 20)) + 40;
        Box(W / 2 - w / 2, y, w, 58, U.Col(120, 30, 40, 235), 0.25f);
        U.TextCentered($"{MacPermissions.Why}!", W / 2, y + 8, 20, Color.White, 2);
        U.TextCentered(fix, W / 2, y + 34, 16, U.Col(255, 210, 200), 1);
    }
}
