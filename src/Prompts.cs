using System.Numerics;
using Raylib_cs;

namespace FishLegs;

public enum PadFamily { Xbox, PlayStation, Switch }

public enum Act { Move, Kick, Jump, Dash, Slap, Navigate, Confirm, Back, Pause, Quit }

// Which glyphs to show for a player: a keyboard layout or a particular brand of gamepad.
public readonly record struct Device(bool Pad, PadFamily Family, int KeySet)
{
    public const int KeysP1 = 0, KeysP2 = 1, KeysMenu = 2;
    public static Device Keyboard(int keySet) => new(false, PadFamily.Xbox, keySet);
    public static Device Gamepad(PadFamily f) => new(true, f, 0);
}

// Kenney input prompt icons (CC0, www.kenney.nl), copied into assets/input-prompts.
static class Prompts
{
    static readonly Dictionary<string, Texture2D> icons = new();

    public static void Load()
    {
        string root = Path.Combine(AppContext.BaseDirectory, "assets", "input-prompts");
        if (!Directory.Exists(root)) return;
        foreach (var file in Directory.EnumerateFiles(root, "*.png", SearchOption.AllDirectories))
        {
            var tex = Raylib.LoadTexture(file);
            Raylib.GenTextureMipmaps(ref tex);
            Raylib.SetTextureFilter(tex, TextureFilter.Trilinear);
            icons[Path.GetFileNameWithoutExtension(file)] = tex;
        }
    }

    public static void Unload()
    {
        foreach (var t in icons.Values) Raylib.UnloadTexture(t);
        icons.Clear();
    }

    public static PadFamily DetectFamily(int pad)
    {
        string name = Pads.Name(pad).ToLowerInvariant();
        if (name.Contains("playstation") || name.Contains("dualsense") || name.Contains("dualshock") ||
            name.Contains("ps3") || name.Contains("ps4") || name.Contains("ps5") || name.Contains("sony"))
            return PadFamily.PlayStation;
        if (name.Contains("nintendo") || name.Contains("switch") || name.Contains("joy-con") || name.Contains("pro controller"))
            return PadFamily.Switch;
        return PadFamily.Xbox;
    }

    // Each binding is a list of alternatives; each alternative is a sequence of icons drawn side by side.
    public static string[][] Binding(Device d, Act a)
    {
        if (d.Pad)
        {
            // Face buttons are positional: bottom / right / left / top.
            var (bottom, right, left, top, lb, rb, rt, stick, dpad, start, select) = d.Family switch
            {
                PadFamily.PlayStation => ("playstation_button_color_cross", "playstation_button_color_circle", "playstation_button_color_square",
                    "playstation_button_color_triangle", "playstation_trigger_l1", "playstation_trigger_r1", "playstation_trigger_r2",
                    "playstation_stick_l", "playstation_dpad", "playstation5_button_options", "playstation5_button_create"),
                PadFamily.Switch => ("switch_button_b", "switch_button_a", "switch_button_y", "switch_button_x", "switch_button_l",
                    "switch_button_r", "switch_button_zr", "switch_stick_l", "switch_dpad", "switch_button_plus", "switch_button_minus"),
                _ => ("xbox_button_color_a", "xbox_button_color_b", "xbox_button_color_x", "xbox_button_color_y", "xbox_lb", "xbox_rb",
                    "xbox_rt", "xbox_stick_l", "xbox_dpad", "xbox_button_menu", "xbox_button_view"),
            };
            return a switch
            {
                Act.Move => [[stick]],
                Act.Kick => [[left], [rt]],
                Act.Jump => [[bottom]],
                Act.Dash => [[right], [rb]],
                Act.Slap => [[top], [lb]],
                Act.Navigate => [[stick], [dpad]],
                Act.Confirm => [[bottom]],
                Act.Back => [[right]],
                Act.Pause => [[start]],
                Act.Quit => [[select]],
                _ => [],
            };
        }

        return (d.KeySet, a) switch
        {
            (Device.KeysP2, Act.Move) => [["keyboard_arrow_up", "keyboard_arrow_left", "keyboard_arrow_down", "keyboard_arrow_right"]],
            (Device.KeysP2, Act.Kick) => [["keyboard_l"]],
            (Device.KeysP2, Act.Jump) => [["keyboard_semicolon"]],
            (Device.KeysP2, Act.Dash) => [["keyboard_k"]],
            (Device.KeysP2, Act.Slap) => [["keyboard_j"]],
            (Device.KeysP2, Act.Navigate) => [["keyboard_arrow_up", "keyboard_arrow_down"]],
            (Device.KeysP2, Act.Confirm) => [["keyboard_l"], ["keyboard_semicolon"]],
            (Device.KeysP1, Act.Move) => [["keyboard_w", "keyboard_a", "keyboard_s", "keyboard_d"]],
            (Device.KeysP1, Act.Kick) => [["keyboard_b"], ["keyboard_e"]],
            (Device.KeysP1, Act.Jump) => [["keyboard_v"], ["keyboard_space"]],
            (Device.KeysP1, Act.Dash) => [["keyboard_n"], ["keyboard_shift"]],
            (Device.KeysP1, Act.Slap) => [["keyboard_c"], ["keyboard_q"]],
            (Device.KeysP1, Act.Navigate) => [["keyboard_w", "keyboard_s"]],
            (Device.KeysP1, Act.Confirm) => [["keyboard_b"], ["keyboard_v"]],
            (_, Act.Navigate) => [["keyboard_arrow_up", "keyboard_arrow_down"], ["keyboard_w", "keyboard_s"]],
            (_, Act.Confirm) => [["keyboard_enter"], ["keyboard_space"]],
            (_, Act.Back) => [["keyboard_escape"]],
            (_, Act.Pause) => [["keyboard_escape"], ["keyboard_p"]],
            (_, Act.Quit) => [["keyboard_enter"]],
            _ => [],
        };
    }

    public static bool Has(string icon) => icons.ContainsKey(icon);

    // Face buttons in positional order: bottom, right, left, top.
    public static string[] FaceButtons(PadFamily f) => f switch
    {
        PadFamily.PlayStation => ["playstation_button_color_cross", "playstation_button_color_circle", "playstation_button_color_square", "playstation_button_color_triangle"],
        PadFamily.Switch => ["switch_button_b", "switch_button_a", "switch_button_y", "switch_button_x"],
        _ => ["xbox_button_color_a", "xbox_button_color_b", "xbox_button_color_x", "xbox_button_color_y"],
    };

    public static void DrawIcon(string name, int x, int y, int size, float alpha = 1)
    {
        if (icons.TryGetValue(name, out var tex))
            Raylib.DrawTexturePro(tex, new Rectangle(0, 0, tex.Width, tex.Height), new Rectangle(x, y, size, size), Vector2.Zero, 0, U.WithAlpha(Color.White, alpha));
    }

    /// <summary>Draws a binding at (x, y) with icons of the given height. Returns the width used.</summary>
    public static int Draw(Device d, Act a, int x, int y, int size, float alpha = 1)
    {
        var alts = Binding(d, a);
        int cx = x;
        var tint = U.WithAlpha(Color.White, alpha);
        for (int i = 0; i < alts.Length; i++)
        {
            if (i > 0)
            {
                int fs = Math.Max(12, size / 2);
                Raylib.DrawText("/", cx + 2, y + size / 2 - fs / 2, fs, U.WithAlpha(U.Col(200, 210, 230), alpha));
                cx += Raylib.MeasureText("/", fs) + 4;
            }
            foreach (var name in alts[i])
            {
                if (icons.TryGetValue(name, out var tex))
                    Raylib.DrawTexturePro(tex, new Rectangle(0, 0, tex.Width, tex.Height), new Rectangle(cx, y, size, size), Vector2.Zero, 0, tint);
                else
                    Raylib.DrawText("?", cx + size / 3, y, size, tint);
                cx += size - size / 8;   // Kenney glyphs have built-in padding, so overlap slightly
            }
        }
        return cx - x;
    }

    /// <summary>Draws "[icons] label" and returns the width used.</summary>
    public static int Hint(Device d, Act a, string label, int x, int y, int size, int fontSize, Color col)
    {
        int w = Draw(d, a, x, y, size, col.A / 255f);
        Raylib.DrawText(label, x + w + 4, y + size / 2 - fontSize / 2, fontSize, col);
        return w + 4 + Raylib.MeasureText(label, fontSize);
    }

    /// <summary>Measures a row of hints so it can be centred.</summary>
    public static int MeasureHint(Device d, Act a, string label, int size, int fontSize)
    {
        var alts = Binding(d, a);
        int w = 0;
        for (int i = 0; i < alts.Length; i++)
        {
            if (i > 0) w += Raylib.MeasureText("/", Math.Max(12, size / 2)) + 4;
            w += alts[i].Length * (size - size / 8);
        }
        return w + 4 + Raylib.MeasureText(label, fontSize);
    }

    public static void HintRow(int cx, int y, int size, int fontSize, Color col, int gap, params (Device d, Act a, string label)[] hints)
    {
        int total = hints.Sum(h => MeasureHint(h.d, h.a, h.label, size, fontSize)) + gap * (hints.Length - 1);
        int x = cx - total / 2;
        foreach (var h in hints) x += Hint(h.d, h.a, h.label, x, y, size, fontSize, col) + gap;
    }
}
