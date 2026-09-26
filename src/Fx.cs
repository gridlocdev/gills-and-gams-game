using System.Numerics;
using Raylib_cs;

namespace FishLegs;

public class Fx
{
    struct Particle
    {
        public Vector3 Pos, Vel;
        public float Life, MaxLife, Size, Gravity, Drag;
        public Color Col;
        public bool Spin;
    }

    struct Popup
    {
        public Vector3 Pos;
        public string Text;
        public Color Col;
        public float Life;
    }

    readonly List<Particle> parts = new();
    readonly List<Popup> popups = new();

    void Add(Vector3 p, Vector3 v, float life, float size, Color c, float gravity = 12, float drag = 1, bool spin = false)
    {
        if (parts.Count > 1500) return;
        parts.Add(new Particle { Pos = p, Vel = v, Life = life, MaxLife = life, Size = size, Col = c, Gravity = gravity, Drag = drag, Spin = spin });
    }

    static Vector3 RandDir() => U.SafeNormalize(new Vector3(U.Rand(-1, 1), U.Rand(-1, 1), U.Rand(-1, 1)), Vector3.UnitY);

    public void Dust(Vector3 p, int n)
    {
        for (int i = 0; i < n; i++)
            Add(p + new Vector3(U.Rand(-0.3f, 0.3f), 0.1f, U.Rand(-0.3f, 0.3f)),
                new Vector3(U.Rand(-3, 3), U.Rand(1, 3), U.Rand(-3, 3)), U.Rand(0.3f, 0.6f), U.Rand(0.12f, 0.22f),
                U.Col(215, 200, 160, 220), 2, 3);
    }

    public void Puff(Vector3 p, int n)
    {
        for (int i = 0; i < n; i++)
            Add(p, RandDir() * U.Rand(1, 3), U.Rand(0.25f, 0.45f), U.Rand(0.12f, 0.2f), U.Col(240, 240, 255, 200), 0, 4);
    }

    public void Spray(Vector3 p, Vector3 baseVel, int n)
    {
        for (int i = 0; i < n; i++)
            Add(p, baseVel + RandDir() * U.Rand(1, 4), U.Rand(0.4f, 0.8f), U.Rand(0.06f, 0.12f), U.Col(150, 210, 255, 230), 14, 0.5f);
    }

    public void Grass(Vector3 p, int n)
    {
        for (int i = 0; i < n; i++)
            Add(p + new Vector3(0, 0.1f, 0), new Vector3(U.Rand(-3, 3), U.Rand(2, 5), U.Rand(-3, 3)), U.Rand(0.4f, 0.8f), 0.08f, U.Col(70, 160, 60), 14, 1, true);
    }

    public void Burst(Vector3 p, Color c, int n)
    {
        for (int i = 0; i < n; i++)
            Add(p, RandDir() * U.Rand(3, 8), U.Rand(0.2f, 0.45f), U.Rand(0.07f, 0.14f), c, 4, 3);
    }

    public void Stars(Vector3 p, int n)
    {
        for (int i = 0; i < n; i++)
            Add(p + Vector3.UnitY * 0.6f, RandDir() * U.Rand(1, 3) + Vector3.UnitY * 2, U.Rand(0.5f, 0.9f), 0.14f, U.Col(255, 240, 90), 3, 2, true);
    }

    public void Trail(Vector3 p, Color c) =>
        Add(p + RandDir() * 0.2f, Vector3.Zero, 0.35f, 0.22f, U.WithAlpha(c, 0.6f), 0, 0);

    public void Confetti(Vector3 p, int n)
    {
        Color[] cols = { U.Col(255, 80, 80), U.Col(255, 220, 60), U.Col(80, 200, 255), U.Col(120, 255, 140), U.Col(255, 120, 230), Color.White };
        for (int i = 0; i < n; i++)
            Add(p, new Vector3(U.Rand(-6, 6), U.Rand(6, 16), U.Rand(-6, 6)), U.Rand(1.5f, 3f), U.Rand(0.1f, 0.18f), U.Pick(cols), 7, 1.2f, true);
    }

    public void Bubbles(Vector3 p, int n)
    {
        for (int i = 0; i < n; i++)
            Add(p + RandDir() * 0.5f, new Vector3(U.Rand(-1, 1), U.Rand(1.5f, 4), U.Rand(-1, 1)), U.Rand(0.8f, 1.6f), U.Rand(0.08f, 0.2f), U.Col(200, 240, 255, 170), -1, 0.5f);
    }

    public void AddPopup(Vector3 p, string text, Color c)
    {
        // Nudge up if another popup is fresh in the same spot so they don't overlap.
        foreach (var o in popups)
            if (o.Life > 0.8f && Vector3.Distance(o.Pos, p) < 1.2f) p.Y += 0.7f;
        popups.Add(new Popup { Pos = p, Text = text, Col = c, Life = 1.1f });
    }

    public void Update(float dt)
    {
        for (int i = parts.Count - 1; i >= 0; i--)
        {
            var q = parts[i];
            q.Life -= dt;
            if (q.Life <= 0) { parts.RemoveAt(i); continue; }
            q.Vel.Y -= q.Gravity * dt;
            q.Vel *= MathF.Exp(-q.Drag * dt);
            q.Pos += q.Vel * dt;
            if (q.Pos.Y < 0.02f) { q.Pos.Y = 0.02f; q.Vel.Y = MathF.Abs(q.Vel.Y) * 0.2f; q.Vel.X *= 0.6f; q.Vel.Z *= 0.6f; }
            parts[i] = q;
        }
        for (int i = popups.Count - 1; i >= 0; i--)
        {
            var p = popups[i];
            p.Life -= dt;
            p.Pos.Y += dt * 1.2f;
            if (p.Life <= 0) popups.RemoveAt(i); else popups[i] = p;
        }
    }

    public void Clear()
    {
        parts.Clear();
        popups.Clear();
    }

    public void Render3D(float time)
    {
        foreach (var q in parts)
        {
            float t = q.Life / q.MaxLife;
            float s = q.Size * (0.4f + 0.6f * t);
            var c = U.WithAlpha(q.Col, q.Col.A / 255f * MathF.Min(1, t * 2));
            if (q.Spin)
            {
                Rlgl.PushMatrix();
                Rlgl.Translatef(q.Pos.X, q.Pos.Y, q.Pos.Z);
                Rlgl.Rotatef((time * 400 + q.MaxLife * 1000) % 360, 0.3f, 1, 0.5f);
                Raylib.DrawCube(Vector3.Zero, s * 1.6f, s * 0.3f, s, c);
                Rlgl.PopMatrix();
            }
            else Raylib.DrawCube(q.Pos, s, s, s, c);
        }
    }

    public void Render2D(Camera3D cam)
    {
        foreach (var p in popups)
        {
            var sp = Raylib.GetWorldToScreen(p.Pos, cam);
            float age = 1.1f - p.Life;
            float pop = age < 0.12f ? U.Lerp(0.4f, 1.25f, age / 0.12f) : age < 0.22f ? U.Lerp(1.25f, 1f, (age - 0.12f) / 0.1f) : 1f;
            int size = (int)(30 * pop);
            float alpha = MathF.Min(1, p.Life * 3);
            int w = Raylib.MeasureText(p.Text, size);
            float wobble = MathF.Sin(age * 30) * 2 * (1 - U.Clamp01(age * 3));
            Raylib.DrawText(p.Text, (int)(sp.X - w / 2f + 3 + wobble), (int)sp.Y + 3, size, new Color(0, 0, 0, (int)(180 * alpha)));
            Raylib.DrawText(p.Text, (int)(sp.X - w / 2f + wobble), (int)sp.Y, size, U.WithAlpha(p.Col, alpha));
        }
    }
}
