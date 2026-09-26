using System.Numerics;
using Raylib_cs;

namespace FishLegs;

static class U
{
    public static readonly Random Rng = new();

    public static float Rand(float a, float b) => a + (float)Rng.NextDouble() * (b - a);
    public static int RandInt(int a, int bExclusive) => Rng.Next(a, bExclusive);
    public static T Pick<T>(IReadOnlyList<T> list) => list[Rng.Next(list.Count)];

    public static float Clamp(float v, float a, float b) => v < a ? a : v > b ? b : v;
    public static float Clamp01(float v) => Clamp(v, 0, 1);
    public static float Lerp(float a, float b, float t) => a + (b - a) * t;
    public static float Damp(float a, float b, float lambda, float dt) => Lerp(a, b, 1 - MathF.Exp(-lambda * dt));
    public static Vector3 Damp(Vector3 a, Vector3 b, float lambda, float dt) => Vector3.Lerp(a, b, 1 - MathF.Exp(-lambda * dt));

    public static float MoveTowards(float cur, float target, float maxDelta)
    {
        if (MathF.Abs(target - cur) <= maxDelta) return target;
        return cur + MathF.Sign(target - cur) * maxDelta;
    }

    public static Vector2 MoveTowards(Vector2 cur, Vector2 target, float maxDelta)
    {
        var d = target - cur;
        float len = d.Length();
        if (len <= maxDelta || len < 1e-6f) return target;
        return cur + d / len * maxDelta;
    }

    public static float WrapAngle(float a)
    {
        while (a > MathF.PI) a -= MathF.Tau;
        while (a < -MathF.PI) a += MathF.Tau;
        return a;
    }

    public static float TurnTowards(float cur, float target, float maxDelta)
    {
        float d = WrapAngle(target - cur);
        if (MathF.Abs(d) <= maxDelta) return target;
        return cur + MathF.Sign(d) * maxDelta;
    }

    public static Vector3 Flat(Vector3 v) => new(v.X, 0, v.Z);
    public static Vector2 XZ(Vector3 v) => new(v.X, v.Z);
    public static Vector3 FromXZ(Vector2 v, float y = 0) => new(v.X, y, v.Y);

    public static Vector3 SafeNormalize(Vector3 v, Vector3 fallback)
    {
        float l = v.Length();
        return l > 1e-5f ? v / l : fallback;
    }

    public static Vector3 ClosestOnSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        var ab = b - a;
        float t = Vector3.Dot(p - a, ab) / MathF.Max(Vector3.Dot(ab, ab), 1e-6f);
        return a + ab * Clamp01(t);
    }

    public static Color Col(int r, int g, int b, int a = 255) => new((byte)r, (byte)g, (byte)b, (byte)a);

    public static Color Shade(Color c, float f) =>
        new((byte)Clamp(c.R * f, 0, 255), (byte)Clamp(c.G * f, 0, 255), (byte)Clamp(c.B * f, 0, 255), c.A);

    public static Color WithAlpha(Color c, float a) => new(c.R, c.G, c.B, (byte)Clamp(a * 255, 0, 255));

    public static Color LerpColor(Color a, Color b, float t) => new(
        (byte)Lerp(a.R, b.R, t), (byte)Lerp(a.G, b.G, t), (byte)Lerp(a.B, b.B, t), (byte)Lerp(a.A, b.A, t));

    // Text helpers that draw with a chunky drop shadow, because everything is funnier in bold.
    public static void TextShadow(string s, int x, int y, int size, Color c, int shadow = 3)
    {
        Raylib.DrawText(s, x + shadow, y + shadow, size, new Color(0, 0, 0, (int)(c.A * 0.7f)));
        Raylib.DrawText(s, x, y, size, c);
    }

    public static void TextCentered(string s, int cx, int y, int size, Color c, int shadow = 3)
    {
        int w = Raylib.MeasureText(s, size);
        TextShadow(s, cx - w / 2, y, size, c, shadow);
    }

    // Deterministic hash noise so leg hairs don't re-randomise every frame.
    public static float Hash(int n)
    {
        n = (n << 13) ^ n;
        return 1f - ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / 1073741824f;
    }
}
