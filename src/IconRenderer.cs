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
        ball.Pos = new Vector3(1.25f, ball.R, 0.9f);
        ball.Rot = Quaternion.CreateFromYawPitchRoll(0.6f, 0.4f, 0.2f);

        var cam = new Camera3D
        {
            Position = new Vector3(1.3f, 2.0f, 7.4f),
            Target = new Vector3(0.2f, 1.25f, 0),
            Up = Vector3.UnitY,
            FovY = 33,
            Projection = CameraProjection.Perspective,
        };

        Raylib.BeginTextureMode(rt);
        Raylib.ClearBackground(new Color(0, 0, 0, 0));

        // Full-bleed, opaque square: macOS applies its own rounded mask. Any transparent margin makes
        // macOS 26+ shrink the artwork onto a grey tile instead.
        float s = Supersample;
        Raylib.DrawRectangleGradientV(0, 0, n, n, U.Col(80, 178, 88), U.Col(52, 140, 64));
        Raylib.DrawRing(new Vector2(512 * s, 760 * s), 250 * s, 264 * s, 0, 360, 96, U.Col(235, 245, 235, 130));

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
        Raylib.ImageFormat(ref img, PixelFormat.UncompressedR8G8B8);   // no alpha channel at all
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        Raylib.ExportImage(img, path);
        Raylib.UnloadImage(img);
        Raylib.UnloadRenderTexture(rt);
        Console.WriteLine($"Icon written to {path}");
    }
}
