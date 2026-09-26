using Raylib_cs;

namespace FishLegs;

static class Program
{
    static void Main()
    {
        Raylib.SetConfigFlags(ConfigFlags.Msaa4xHint | ConfigFlags.VSyncHint | ConfigFlags.ResizableWindow | ConfigFlags.HighDpiWindow);
        Raylib.InitWindow(1280, 720, "Gills & Gams - Competitive Fish-Leg Football");
        Raylib.SetWindowMinSize(960, 600);
        Raylib.SetExitKey(KeyboardKey.Null);
        Raylib.SetTargetFPS(144);

        Audio.Init();
        Draw.Init();
        var game = new Game();

        // FISHLEGS_SHOTS="dir" runs a scripted tour and saves screenshots (dev aid).
        string shots = Environment.GetEnvironmentVariable("FISHLEGS_SHOTS");
        var script = new Dictionary<int, string>
        {
            [90] = "shot:title", [100] = "howto", [130] = "shot:howto", [140] = "play", [420] = "shot:play1",
            [700] = "shot:play2", [900] = "shot:play3", [910] = "goal", [960] = "shot:goal", [1300] = "draft",
            [1400] = "shot:draft", [1410] = "victory", [1600] = "shot:victory", [1610] = "quit",
        };
        int frame = 0;

        while (!Raylib.WindowShouldClose() && !game.Quit)
        {
            game.Update(shots != null ? 1f / 60f : Raylib.GetFrameTime());
            game.Render();
            if (shots != null && script.TryGetValue(frame, out var cmd))
            {
                if (cmd == "quit") break;
                if (cmd.StartsWith("shot:"))
                {
                    var img = Raylib.LoadImageFromScreen();
                    Raylib.ExportImage(img, Path.Combine(shots, cmd[5..] + ".png"));
                    Raylib.UnloadImage(img);
                }
                else game.DebugCommand(cmd);
            }
            frame++;
        }

        Raylib.UnloadShader(Draw.Lit);
        Audio.Shutdown();
        Raylib.CloseWindow();
    }
}
