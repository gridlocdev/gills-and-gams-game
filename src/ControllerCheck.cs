using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Raylib_cs;

namespace FishLegs;

// `dotnet run -- --controllers` : shows what the game (raylib) and macOS each see, live.
// `dotnet run -- --controllers --report` : prints the same thing to stdout and exits.
static class ControllerCheck
{
    record HidDevice(string Product, int Vendor, int ProductId, int Usage, string Transport);
    record UsbDevice(string Product, int Vendor, int ProductId);

    static readonly Dictionary<int, string> KnownVendors = new()
    {
        [0x045e] = "Microsoft", [0x057e] = "Nintendo", [0x28de] = "Valve", [0x054c] = "Sony",
    };

    static List<HidDevice> hid = new();
    static List<UsbDevice> usb = new();
    static readonly object scanLock = new();
    static bool scanned;

    public static void Run(bool report)
    {
        var scanner = new Thread(() =>
        {
            while (true)
            {
                var h = ScanHid();
                var u = ScanUsb();
                lock (scanLock) { hid = h; usb = u; scanned = true; }
                Thread.Sleep(1500);
            }
        }) { IsBackground = true };
        scanner.Start();

        int frames = 0;
        while (!Raylib.WindowShouldClose())
        {
            frames++;
            if (report && frames > 90 && scanned)
            {
                Console.WriteLine(BuildReport());
                return;
            }
            Pads.Update();
            if (Raylib.IsKeyPressed(KeyboardKey.Escape)) return;
            if (Raylib.IsKeyPressed(KeyboardKey.O)) MacPermissions.OpenSettings();
            for (int g = 0; g < Pads.Max; g++)
                if (Pads.Pressed(g, GamepadButton.RightFaceUp))
                    Pads.Rumble(g, 0.8f, 0.4f);
            Render();
        }
    }

    // ------------------------------------------------------------------ scanning

    static string RunTool(string file, string args)
    {
        try
        {
            var psi = new ProcessStartInfo(file, args) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            using var p = Process.Start(psi)!;
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(3000);
            return output;
        }
        catch { return ""; }
    }

    static List<HidDevice> ScanHid()
    {
        var list = new List<HidDevice>();
        foreach (var line in RunTool("/usr/bin/hidutil", "list --ndjson").Split('\n'))
        {
            if (!line.StartsWith('{')) continue;
            try
            {
                using var doc = JsonDocument.Parse(line);
                var r = doc.RootElement;
                if (Str(r, "type") != "device") continue;
                int page = Int(r, "PrimaryUsagePage"), usage = Int(r, "PrimaryUsage"), vendor = Int(r, "VendorID");
                bool padLike = page == 1 && (usage == 4 || usage == 5 || usage == 8);
                if (!padLike && !KnownVendors.ContainsKey(vendor)) continue;
                list.Add(new HidDevice(Str(r, "Product") ?? "(unnamed)", vendor, Int(r, "ProductID"), page == 1 ? usage : -page, Str(r, "Transport") ?? "?"));
            }
            catch (JsonException) { }
        }
        return list.DistinctBy(d => (d.Vendor, d.ProductId, d.Usage)).ToList();
    }

    static List<UsbDevice> ScanUsb()
    {
        var list = new List<UsbDevice>();
        string text = RunTool("/usr/sbin/ioreg", "-p IOUSB -l -w0 -r -c IOUSBHostDevice");
        foreach (var block in text.Split("+-o "))
        {
            var name = Regex.Match(block, "\"USB Product Name\" = \"([^\"]*)\"");
            var vid = Regex.Match(block, "\"idVendor\" = (\\d+)");
            var pid = Regex.Match(block, "\"idProduct\" = (\\d+)");
            if (!vid.Success) continue;
            int v = int.Parse(vid.Groups[1].Value);
            if (!KnownVendors.ContainsKey(v)) continue;
            list.Add(new UsbDevice(name.Success ? name.Groups[1].Value : "(unnamed)", v, pid.Success ? int.Parse(pid.Groups[1].Value) : 0));
        }
        return list;
    }

    static string Str(JsonElement e, string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    static int Int(JsonElement e, string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;

    // ------------------------------------------------------------------ verdicts

    static string SourceName(int g) => Pads.Source(g) == PadSource.GameController ? "Apple GameController" : "raylib (IOKit HID)";

    static string UsageName(int usage) => usage switch
    {
        4 => "joystick", 5 => "gamepad", 8 => "multi-axis controller", 6 => "keyboard", 2 => "mouse",
        < 0 => $"vendor/other (page {-usage})", _ => $"usage {usage}",
    };

    static List<string> Slots()
    {
        var s = new List<string>();
        for (int g = 0; g < Pads.Max; g++) if (Pads.Available(g)) s.Add(Pads.Name(g));
        return s;
    }

    static bool NameMatch(string a, string b)
    {
        a = a.ToLowerInvariant();
        b = b.ToLowerInvariant();
        return a.Contains(b) || b.Contains(a) || a.Split(' ').Intersect(b.Split(' ')).Count(w => w.Length > 3 && w != "controller") > 0;
    }

    static List<(string title, string verdict, bool ok)> Verdicts()
    {
        List<HidDevice> h;
        List<UsbDevice> u;
        lock (scanLock) { h = hid; u = usb; }
        var slots = Slots();
        var result = new List<(string, string, bool)>();

        foreach (var group in h.GroupBy(d => (d.Vendor, d.ProductId)))
        {
            var d = group.First();
            var usages = group.Select(x => x.Usage).Distinct().ToList();
            bool padLike = usages.Any(x => x is 4 or 5 or 8);
            string vendor = KnownVendors.GetValueOrDefault(d.Vendor, $"vendor 0x{d.Vendor:x4}");
            string title = $"{d.Product}  ({vendor}, {d.Transport}, 0x{d.Vendor:x4}:0x{d.ProductId:x4})";
            if (padLike && Pads.PermissionProblem)
                result.Add((title, $"Connected, but {MacPermissions.Why} (see the red box above).", false));
            else if (padLike && slots.Any(s => NameMatch(s, d.Product)))
                result.Add((title, "WORKS - the game sees it as a gamepad.", true));
            else if (padLike && slots.Count > 0 && h.Count(x => x.Usage is 4 or 5 or 8) == 1)
                result.Add((title, $"WORKS - the game sees it as \"{slots[0]}\".", true));
            else if (padLike)
                result.Add((title, "macOS sees a controller, but raylib doesn't recognise its button layout (needs a mapping).", false));
            else if (d.Vendor == 0x28de)
                result.Add((title, $"Acting as a {string.Join(" + ", usages.Select(UsageName))} (Steam Controller 'lizard mode'), not a gamepad.", false));
            else
                result.Add((title, $"Visible to macOS only as {string.Join(" + ", usages.Select(UsageName))}, not as a gamepad.", false));
        }

        foreach (var d in u)
        {
            if (h.Any(x => x.Vendor == d.Vendor && x.ProductId == d.ProductId)) continue;
            string vendor = KnownVendors[d.Vendor];
            result.Add(($"{d.Product}  ({vendor}, USB, 0x{d.Vendor:x4}:0x{d.ProductId:x4})",
                "Plugged in over USB, but it doesn't present itself as a standard controller, so it won't work wired.", false));
        }

        foreach (var s in slots.Where(s => !h.Any(x => NameMatch(s, x.Product))))
            if (!result.Any(r => r.Item2.Contains(s)))
                result.Add(($"{s}  (raylib)", "WORKS - the game sees it as a gamepad.", true));
        return result;
    }

    static string BuildReport()
    {
        var lines = new List<string>
        {
            $"== macOS Input Monitoring: {MacPermissions.InputMonitoring()} (host app: {MacPermissions.HostApp()}; only needed for raylib-read pads) ==",
            $"== Apple GameController framework: {(MacGameController.Available ? $"{MacGameController.Controllers.Count} controller(s)" : "unavailable")} ==",
            "== Game gamepad slots ==",
        };
        for (int g = 0; g < Pads.Max; g++)
            lines.Add(Pads.Available(g)
                ? $"  slot {g}: {Pads.Name(g)} [{Pads.Category(g)}]  | via {SourceName(g)} | prompts: {Prompts.DetectFamily(g)}"
                : $"  slot {g}: (empty)");
        lines.Add("== macOS HID controllers / devices from controller vendors ==");
        lock (scanLock)
        {
            if (hid.Count == 0) lines.Add("  (none)");
            foreach (var d in hid) lines.Add($"  {d.Product}  0x{d.Vendor:x4}:0x{d.ProductId:x4}  {UsageName(d.Usage)}  via {d.Transport}");
            lines.Add("== USB devices from controller vendors ==");
            if (usb.Count == 0) lines.Add("  (none)");
            foreach (var d in usb) lines.Add($"  {d.Product}  0x{d.Vendor:x4}:0x{d.ProductId:x4}");
        }
        lines.Add("== Verdicts ==");
        foreach (var (title, verdict, ok) in Verdicts()) lines.Add($"  [{(ok ? "OK" : "--")}] {title}\n        {verdict}");
        return string.Join('\n', lines);
    }

    // ------------------------------------------------------------------ rendering

    static readonly GamepadButton[] FaceOrder = { GamepadButton.RightFaceDown, GamepadButton.RightFaceRight, GamepadButton.RightFaceLeft, GamepadButton.RightFaceUp };

    static void Render()
    {
        int W = Raylib.GetScreenWidth();
        Raylib.BeginDrawing();
        Raylib.ClearBackground(U.Col(14, 22, 40));
        U.TextShadow("CONTROLLER CHECK", 40, 24, 40, U.Col(255, 215, 80));
        Raylib.DrawText("Plug in / pair controllers - this updates live.  Press Y / Triangle / X(Switch) to test rumble.  Esc to quit.", 40, 72, 18, U.Col(190, 205, 230));

        int y = 110;
        if (Pads.PermissionProblem)
        {
            Raylib.DrawRectangleRounded(new Rectangle(40, y - 6, W - 80, 84), 0.15f, 6, U.Col(120, 30, 40));
            Raylib.DrawText($"{MacPermissions.Why}: Input Monitoring is {MacPermissions.InputMonitoring().ToString().ToLowerInvariant()}.", 58, y + 4, 20, Color.White);
            Raylib.DrawText(MacPermissions.Fix, 58, y + 30, 16, U.Col(255, 210, 200));
            Raylib.DrawText("Press O to open that settings page.", 58, y + 52, 16, U.Col(255, 230, 150));
            y += 96;
        }
        U.TextShadow("WHAT THE GAME SEES", 40, y, 22, U.Col(150, 200, 255), 2);
        y += 32;
        int cardW = (W - 100) / 2;
        for (int g = 0; g < Pads.Max; g++)
        {
            int x = 40 + (g % 2) * (cardW + 20);
            int cy = y + (g / 2) * 150;
            Raylib.DrawRectangleRounded(new Rectangle(x, cy, cardW, 138), 0.1f, 6, new Color(30, 45, 75, 255));
            if (!Pads.Available(g))
            {
                Raylib.DrawText($"Slot {g}: empty", x + 16, cy + 14, 20, U.Col(120, 130, 150));
                continue;
            }
            var fam = Prompts.DetectFamily(g);
            Raylib.DrawText($"Slot {g}: {Pads.Name(g)}", x + 16, cy + 12, 20, U.Col(140, 255, 180));
            Raylib.DrawText($"via {SourceName(g)}  -  {fam} prompts", x + 16, cy + 36, 16, U.Col(180, 190, 210));

            // Stick
            var raw = Pads.RawStick(g);
            var stick = new Vector2(raw.X, -raw.Y);
            var c = new Vector2(x + 60, cy + 94);
            Raylib.DrawCircleLinesV(c, 30, U.Col(120, 140, 170));
            Raylib.DrawCircleV(c + stick * 30, 9, U.Col(255, 215, 80));

            // Face buttons, lit when held
            var faces = Prompts.FaceButtons(fam);
            for (int i = 0; i < 4; i++)
                Prompts.DrawIcon(faces[i], x + 120 + i * 50, cy + 74, 40, Pads.Down(g, FaceOrder[i]) ? 1f : 0.25f);
            // Triggers
            float lt = Pads.LeftTriggerValue(g);
            float rt = Pads.RightTriggerValue(g);
            Raylib.DrawText("LT", x + 340, cy + 72, 14, Color.White);
            Raylib.DrawRectangle(x + 366, cy + 74, 80, 10, new Color(0, 0, 0, 120));
            Raylib.DrawRectangle(x + 366, cy + 74, (int)(80 * U.Clamp01(lt)), 10, U.Col(255, 150, 80));
            Raylib.DrawText("RT", x + 340, cy + 96, 14, Color.White);
            Raylib.DrawRectangle(x + 366, cy + 98, 80, 10, new Color(0, 0, 0, 120));
            Raylib.DrawRectangle(x + 366, cy + 98, (int)(80 * U.Clamp01(rt)), 10, U.Col(255, 150, 80));
        }

        y += 310;
        U.TextShadow("VERDICT", 40, y, 22, U.Col(150, 200, 255), 2);
        y += 32;
        if (!scanned) Raylib.DrawText("Asking macOS...", 40, y, 18, Color.White);
        foreach (var (title, verdict, ok) in Verdicts())
        {
            Raylib.DrawCircle(52, y + 10, 7, ok ? U.Col(100, 230, 140) : U.Col(240, 110, 100));
            Raylib.DrawText(title, 68, y, 18, Color.White);
            Raylib.DrawText(verdict, 68, y + 22, 16, ok ? U.Col(150, 240, 180) : U.Col(255, 180, 160));
            y += 50;
        }
        if (scanned && Verdicts().Count == 0)
            Raylib.DrawText("No controllers from Microsoft, Nintendo, Valve or Sony detected by macOS.", 40, y, 18, U.Col(255, 180, 160));
        Raylib.EndDrawing();
    }
}
