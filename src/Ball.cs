using System.Numerics;
using Raylib_cs;

namespace FishLegs;

public class Ball
{
    public readonly float R = 0.55f;
    public Vector3 Pos, Vel;
    public Vector3 Spin;             // lateral acceleration from Banana Fins
    public Quaternion Rot = Quaternion.Identity;
    public int LastTouch = -1;
    public float SinceTouch;
    float bounceCd;

    static readonly Vector3[] Patches = MakePatches();

    public void Reset()
    {
        Pos = new Vector3(0, R + 1.5f, 0);
        Vel = Vector3.Zero;
        Spin = Vector3.Zero;
        LastTouch = -1;
    }

    public void Touch(int id)
    {
        LastTouch = id;
        SinceTouch = 0;
    }

    /// <summary>Returns the side (0/1) whose goal the ball just fully crossed into, or -1.</summary>
    public int Update(float dt, Game g, bool allowGoal)
    {
        SinceTouch += dt;
        bounceCd = MathF.Max(0, bounceCd - dt);
        Vel.Y -= 20f * dt;
        Vel += Spin * dt;
        Spin *= MathF.Exp(-1.4f * dt);
        Vel *= MathF.Exp(-0.15f * dt);
        Pos += Vel * dt;

        // Ground
        if (Pos.Y < R)
        {
            Pos.Y = R;
            if (Vel.Y < -1.5f)
            {
                if (Vel.Y < -4 && bounceCd <= 0) { Audio.Play(Sfx.Bounce, 1f + U.Rand(-0.1f, 0.1f), U.Clamp01(-Vel.Y / 15)); bounceCd = 0.08f; }
                Vel.Y = -Vel.Y * 0.62f;
            }
            else Vel.Y = 0;
            float friction = MathF.Exp(-1.1f * dt);
            Vel.X *= friction;
            Vel.Z *= friction;
            Spin *= MathF.Exp(-3f * dt);
        }
        if (Pos.Y > Arena.Ceiling - R) { Pos.Y = Arena.Ceiling - R; Vel.Y = -MathF.Abs(Vel.Y) * 0.5f; }

        // Side walls
        float lz = Arena.HalfW - R;
        if (MathF.Abs(Pos.Z) > lz)
        {
            Pos.Z = MathF.Sign(Pos.Z) * lz;
            if (Vel.Z * MathF.Sign(Pos.Z) > 0) { WallThud(MathF.Abs(Vel.Z)); Vel.Z = -Vel.Z * 0.78f; Spin.Z = -Spin.Z; }
        }

        // End walls, except where there's a goal mouth
        for (int side = 0; side < 2; side++)
        {
            float sgn = side == 0 ? -1 : 1;
            float x = Pos.X * sgn;
            if (x <= Arena.HalfL - R) continue;
            bool wasInside = (Pos.X - Vel.X * dt) * sgn <= Arena.HalfL;
            if (x > Arena.HalfL)
            {
                Arena.ContainInGoal(side, ref Pos, ref Vel, R);
            }
            else if (!Arena.InGoalMouth(side, Pos, R) && wasInside)
            {
                Pos.X = sgn * (Arena.HalfL - R);
                if (Vel.X * sgn > 0) { WallThud(MathF.Abs(Vel.X)); Vel.X = -Vel.X * 0.78f; Spin.X = -Spin.X; }
            }
        }

        if (Arena.CollidePosts(ref Pos, ref Vel, R))
        {
            Audio.Play(Sfx.Post, U.Rand(0.95f, 1.05f));
            g.Popup(Pos + Vector3.UnitY, "DOINK!", U.Col(255, 255, 255));
            g.Comment(Commentary.Post());
            g.Shake(0.2f);
            g.Hype(0.4f);
        }

        // Rolling rotation
        float spd = new Vector2(Vel.X, Vel.Z).Length();
        if (spd > 0.01f)
        {
            var axis = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, new Vector3(Vel.X, 0, Vel.Z)));
            Rot = Quaternion.Normalize(Quaternion.CreateFromAxisAngle(axis, spd / R * dt) * Rot);
        }

        if (spd > 19 && U.Rand(0, 1) < 0.6f) g.Fx.Trail(Pos, LastTouch >= 0 ? g.Fish[LastTouch].TeamCol : Color.White);

        if (!allowGoal) return -1;
        if (Pos.X < -Arena.HalfL - R) return 0;
        if (Pos.X > Arena.HalfL + R) return 1;
        return -1;
    }

    void WallThud(float speed)
    {
        if (speed > 4 && bounceCd <= 0)
        {
            Audio.Play(Sfx.Bounce, 0.8f, U.Clamp01(speed / 18));
            bounceCd = 0.08f;
        }
    }

    // Ball vs. a fish's capsule colliders.
    public void CollideFish(Fish f, Game g, float dt)
    {
        var up = Vector3.UnitY;
        var c = f.BodyCenter;
        var fwd = f.Facing;
        float sc = f.Sc;
        bool dashing = f.Diving || f.SlideTimer > 0;

        Collide(f, c - fwd * 0.55f * sc, c + fwd * 0.55f * sc, 0.6f * sc, dashing, g);
        if (!f.Lying)
            Collide(f, f.Pos + up * 0.2f, f.Pos + up * f.HipY, 0.33f * sc, false, g);

        // Sticky Toes: gently herd the ball toward the fish's front foot while dribbling.
        if (f.S.StickyToes && f.Grounded && f.CanAct && !f.Lying && Pos.Y < 1.3f)
        {
            var target = f.Pos + fwd * (1.1f * sc + R);
            var to = new Vector2(target.X - Pos.X, target.Z - Pos.Z);
            var rel = new Vector2(Vel.X - f.Vel.X, Vel.Z - f.Vel.Z);
            if (to.Length() < 1.3f && rel.Length() < 10)
            {
                var a = to * 30 - rel * 6;
                Vel.X += a.X * dt;
                Vel.Z += a.Y * dt;
            }
        }
    }

    void Collide(Fish f, Vector3 a, Vector3 b, float radius, bool slam, Game g)
    {
        var cp = U.ClosestOnSegment(Pos, a, b);
        var d = Pos - cp;
        float dist = d.Length();
        float min = radius + R;
        if (dist >= min) return;
        var n = dist > 1e-4f ? d / dist : U.SafeNormalize(U.Flat(f.Facing), Vector3.UnitX);
        Pos = cp + n * min;
        var rel = Vel - f.Vel;
        float vn = Vector3.Dot(rel, n);
        if (vn >= 0) return;

        float e = slam ? 0.9f : 0.45f;
        Vel -= (1 + e) * vn * n;
        if (slam)
        {
            // FLOP SHOT: belly-first into the ball.
            float fs = new Vector2(f.Vel.X, f.Vel.Z).Length();
            if (fs > 8)
            {
                Vel += U.Flat(f.Vel) * 0.45f * f.S.DiveHitMult + Vector3.UnitY * 4;
                g.Popup(Pos + Vector3.UnitY, "FLOP SHOT!", U.Col(120, 220, 255));
                g.TechUsed(f, "flopshot");
                Audio.PlayVaried(Sfx.Splat, 0.1f);
                Audio.Play(Sfx.Kick, 0.8f);
                g.Hitstop(0.05f);
            }
        }
        else if (-vn > 5) Audio.Play(Sfx.Bounce, 1.3f, U.Clamp01(-vn / 15));
        Touch(f.Id);
    }

    public void Render()
    {
        Draw.Shadow(Pos, R * 1.1f, U.Clamp01(1 - (Pos.Y - R) / 10));
        Rlgl.PushMatrix();
        Rlgl.Translatef(Pos.X, Pos.Y, Pos.Z);
        float angle = 2 * MathF.Acos(U.Clamp(Rot.W, -1, 1));
        float s = MathF.Sqrt(MathF.Max(0, 1 - Rot.W * Rot.W));
        if (s > 1e-4f) Rlgl.Rotatef(angle * 180 / MathF.PI, Rot.X / s, Rot.Y / s, Rot.Z / s);
        Raylib.DrawSphereEx(Vector3.Zero, R, 12, 16, U.Col(250, 250, 245));
        foreach (var p in Patches)
        {
            Rlgl.PushMatrix();
            var at = p * R * 0.72f;
            Rlgl.Translatef(at.X, at.Y, at.Z);
            Raylib.DrawSphereEx(Vector3.Zero, R * 0.32f, 5, 6, U.Col(25, 25, 30));
            Rlgl.PopMatrix();
        }
        Rlgl.PopMatrix();
    }

    static Vector3[] MakePatches()
    {
        // The 12 vertices of an icosahedron ~ the black pentagons of a real football.
        float t = (1 + MathF.Sqrt(5)) / 2;
        var v = new List<Vector3>();
        foreach (var a in new[] { -1f, 1f })
            foreach (var b in new[] { -t, t })
            {
                v.Add(Vector3.Normalize(new Vector3(0, a, b)));
                v.Add(Vector3.Normalize(new Vector3(a, b, 0)));
                v.Add(Vector3.Normalize(new Vector3(b, 0, a)));
            }
        return v.ToArray();
    }
}
