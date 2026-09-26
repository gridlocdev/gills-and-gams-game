using System.Numerics;
using Raylib_cs;

namespace FishLegs;

// `dotnet run -- --render-icon <path.png>` renders the 1024x1024 app icon from the real in-game fish.
static class IconRenderer
{
    const int Size = 1024;
    const int Supersample = 2;

    public static void Render(string path)
    {
        int n = Size * Supersample;
        var rt = Raylib.LoadRenderTexture(n, n);
        var game = new Game();
        var fish = game.Fish[0];
        fish.ResetForKickoff(new Vector3(0, 0.35f, 0));
        fish.Yaw = 0.35f;              // turned a touch toward the camera
        fish.Grounded = false;          // mid-hop, legs pedalling
        fish.Celebrate = 1;             // mouth open, fins up, smug eyebrows
        fish.S.HairDensity = 1.6f;      // it's an icon; the legs deserve to be seen

        var ball = game.Ball;
        ball.Pos = new Vector3(1.35f, ball.R, 0.75f);
        ball.Rot = Quaternion.CreateFromYawPitchRoll(0.6f, 0.4f, 0.2f);

        var cam = new Camera3D
        {
            Position = new Vector3(1.5f, 2.3f, 9.6f),
            Target = new Vector3(0.1f, 1.3f, 0),
            Up = Vector3.UnitY,
            FovY = 33,
            Projection = CameraProjection.Perspective,
        };

        Raylib.BeginTextureMode(rt);
        Raylib.ClearBackground(new Color(0, 0, 0, 0));

        // macOS icon grid: an 824px rounded square centred in the 1024px canvas.
        float s = Supersample;
        var body = new Rectangle(100 * s, 100 * s, 824 * s, 824 * s);
        Raylib.DrawRectangleRounded(body, 0.45f, 32, U.Col(58, 150, 70));
        Raylib.DrawRing(new Vector2(512 * s, 690 * s), 190 * s, 200 * s, 0, 360, 96, U.Col(235, 245, 235, 140));

        Raylib.BeginMode3D(cam);
        Raylib.BeginShaderMode(Draw.Lit);
        Draw.SetView(cam.Position);
        ball.Render();
        fish.Render(0.9f);
        fish.DrawTransparent();
        Raylib.EndShaderMode();
        Raylib.EndMode3D();
        Raylib.EndTextureMode();

        var img = Raylib.LoadImageFromTexture(rt.Texture);
        Raylib.ImageFlipVertical(ref img);
        Raylib.ImageResize(ref img, Size, Size);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        Raylib.ExportImage(img, path);
        Raylib.UnloadImage(img);
        Raylib.UnloadRenderTexture(rt);
        Console.WriteLine($"Icon written to {path}");
    }
}
