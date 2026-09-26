using System.Numerics;
using Raylib_cs;

namespace FishLegs;

// "Low Tide Stadium": a deliberately cramped pitch so positioning beats chasing.
static class Arena
{
    public static float HalfL { get; private set; } = 15f;   // goal line at x = +/-HalfL
    public static float HalfW { get; private set; } = 9f;    // side walls at z = +/-HalfW
    public const float WallH = 4.5f;    // visual glass height (ball collision extends to the ceiling)
    public const float Ceiling = 13f;
    public static float BaseGoalHalfW { get; private set; } = 3.2f;
    public static bool Big { get; private set; }
    public const float GoalH = 3.0f;
    public const float GoalDepth = 2.2f;
    public const float PostR = 0.13f;

    // Index 0 = left goal (x = -HalfL, defended by player 0), 1 = right goal.
    public static readonly float[] GoalHalfW = { BaseGoalHalfW, BaseGoalHalfW };

    public static float GoalX(int side) => side == 0 ? -HalfL : HalfL;

    static readonly Color GrassA = U.Col(86, 170, 78);
    static readonly Color GrassB = U.Col(74, 154, 68);
    static readonly Color Line = U.Col(245, 245, 235);
    static readonly Color Sand = U.Col(232, 214, 160);

    struct Spectator
    {
        public Vector3 Pos;
        public Color Col;
        public float Phase, Size;
        public bool FacesLeft;
    }

    static readonly List<Spectator> crowd = new();
    static float hype;          // 0..1 crowd excitement
    static float time;

    public static void Init()
    {
        crowd.Clear();
        Color[] palette =
        {
            U.Col(250, 130, 40), U.Col(60, 170, 170), U.Col(240, 200, 60), U.Col(200, 90, 160),
            U.Col(120, 140, 230), U.Col(230, 80, 80), U.Col(160, 200, 90), U.Col(255, 170, 190),
        };
        // Far stand (behind the -z wall) and both end stands.
        for (int row = 0; row < 4; row++)
        {
            float z = -HalfW - 2.2f - row * 1.4f;
            float y = 1.2f + row * 1.0f;
            for (float x = -HalfL - 1; x <= HalfL + 1; x += 1.05f)
                if (U.Rand(0, 1) < 0.85f)
                    crowd.Add(new Spectator
                    {
                        Pos = new Vector3(x + U.Rand(-0.2f, 0.2f), y, z),
                        Col = U.Pick(palette), Phase = U.Rand(0, 10), Size = U.Rand(0.38f, 0.5f),
                        FacesLeft = U.Rand(0, 1) < 0.5f,
                    });
        }
        for (int side = -1; side <= 1; side += 2)
            for (int row = 0; row < 3; row++)
            {
                float x = side * (HalfL + GoalDepth + 2.4f + row * 1.4f);
                float y = 1.2f + row * 1.0f;
                for (float z = -HalfW; z <= HalfW - 2; z += 1.1f)
                    if (U.Rand(0, 1) < 0.8f)
                        crowd.Add(new Spectator
                        {
                            Pos = new Vector3(x, y, z + U.Rand(-0.2f, 0.2f)),
                            Col = U.Pick(palette), Phase = U.Rand(0, 10), Size = U.Rand(0.38f, 0.5f),
                        });
            }
    }

    // 2v2 gets a slightly bigger pitch (about 20% longer and wider) with a wider goal mouth.
    public static void SetSize(bool big)
    {
        if (big == Big && crowd.Count > 0) return;
        Big = big;
        HalfL = big ? 18f : 15f;
        HalfW = big ? 11f : 9f;
        BaseGoalHalfW = big ? 3.7f : 3.2f;
        ResetGoals();
        Init();
    }

    public static void Hype(float amount) => hype = MathF.Min(1, hype + amount);

    public static void Update(float dt)
    {
        time += dt;
        hype = MathF.Max(0, hype - dt * 0.25f);
    }

    public static void ResetGoals()
    {
        GoalHalfW[0] = BaseGoalHalfW;
        GoalHalfW[1] = BaseGoalHalfW;
    }

    // ---------------------------------------------------------------- collision

    public static bool InGoalMouth(int side, Vector3 p, float r) =>
        MathF.Abs(p.Z) < GoalHalfW[side] - r * 0.2f && p.Y < GoalH - r * 0.2f;

    // Returns true if the ball hit a post/crossbar (for the satisfying DOINK).
    public static bool CollidePosts(ref Vector3 pos, ref Vector3 vel, float r)
    {
        bool hit = false;
        for (int side = 0; side < 2; side++)
        {
            float gx = GoalX(side);
            float gw = GoalHalfW[side];
            hit |= CollideSegment(ref pos, ref vel, r, new Vector3(gx, 0, -gw), new Vector3(gx, GoalH, -gw));
            hit |= CollideSegment(ref pos, ref vel, r, new Vector3(gx, 0, gw), new Vector3(gx, GoalH, gw));
            hit |= CollideSegment(ref pos, ref vel, r, new Vector3(gx, GoalH, -gw), new Vector3(gx, GoalH, gw));
        }
        return hit;
    }

    static bool CollideSegment(ref Vector3 pos, ref Vector3 vel, float r, Vector3 a, Vector3 b)
    {
        var c = U.ClosestOnSegment(pos, a, b);
        var d = pos - c;
        float dist = d.Length();
        float min = r + PostR;
        if (dist >= min || dist < 1e-5f) return false;
        var n = d / dist;
        pos = c + n * min;
        float vn = Vector3.Dot(vel, n);
        if (vn < 0) vel -= 1.75f * vn * n;
        return vn < -3f;
    }

    // Keeps the ball inside the goal box once it has crossed the line (for celebratory net-bulging).
    public static void ContainInGoal(int side, ref Vector3 pos, ref Vector3 vel, float r)
    {
        float sgn = side == 0 ? -1 : 1;
        float back = sgn * (HalfL + GoalDepth - r);
        if (sgn * pos.X > sgn * back) { pos.X = back; vel.X *= -0.3f; vel *= 0.6f; }
        float gw = GoalHalfW[side] - r;
        if (MathF.Abs(pos.Z) > gw) { pos.Z = MathF.Sign(pos.Z) * gw; vel.Z *= -0.3f; }
        if (pos.Y > GoalH - r) { pos.Y = GoalH - r; vel.Y *= -0.3f; }
    }

    // ---------------------------------------------------------------- drawing

    public static void DrawWorld(float celebrate)
    {
        // Beach around the stadium, and the sea the players really should be in.
        Raylib.DrawPlane(new Vector3(0, -0.05f, 0), new Vector2(200, 120), Sand);
        Raylib.DrawPlane(new Vector3(0, -0.02f + MathF.Sin(time * 0.8f) * 0.05f, -75), new Vector2(260, 90), U.Col(50, 130, 200));
        Raylib.DrawPlane(new Vector3(0, -0.01f, -31 + MathF.Sin(time * 0.8f) * 0.6f), new Vector2(260, 2), U.Col(235, 245, 255));
        DrawClouds();
        // Stadium floor apron.
        Draw.Box(new Vector3(0, -0.03f, 0), new Vector3(HalfL * 2 + 9, 0.04f, HalfW * 2 + 4), 0, U.Col(70, 130, 70));

        // Mowed stripes.
        int stripes = 10;
        float sw = HalfL * 2 / stripes;
        for (int i = 0; i < stripes; i++)
        {
            float x = -HalfL + sw * (i + 0.5f);
            Draw.Box(new Vector3(x, 0, 0), new Vector3(sw, 0.02f, HalfW * 2), 0, i % 2 == 0 ? GrassA : GrassB);
        }

        // Pitch markings.
        float lw = 0.12f, ly = 0.025f;
        Draw.Box(new Vector3(0, ly, 0), new Vector3(lw, 0.01f, HalfW * 2 - 0.4f), 0, Line);
        Draw.Box(new Vector3(0, ly, -HalfW + 0.2f), new Vector3(HalfL * 2 - 0.4f, 0.01f, lw), 0, Line);
        Draw.Box(new Vector3(0, ly, HalfW - 0.2f), new Vector3(HalfL * 2 - 0.4f, 0.01f, lw), 0, Line);
        for (int s = -1; s <= 1; s += 2)
        {
            Draw.Box(new Vector3(s * (HalfL - 0.2f), ly, 0), new Vector3(lw, 0.01f, HalfW * 2 - 0.4f), 0, Line);
            // Penalty box
            float bx = s * (HalfL - 3.5f);
            Draw.Box(new Vector3(bx, ly, 0), new Vector3(lw, 0.01f, 11f), 0, Line);
            Draw.Box(new Vector3(s * (HalfL - 1.85f), ly, -5.5f), new Vector3(3.3f, 0.01f, lw), 0, Line);
            Draw.Box(new Vector3(s * (HalfL - 1.85f), ly, 5.5f), new Vector3(3.3f, 0.01f, lw), 0, Line);
            Draw.Disc(new Vector3(s * (HalfL - 5.5f), ly + 0.005f, 0), 0.15f, Line, 10);
        }
        Draw.Ring(new Vector3(0, ly + 0.005f, 0), 2.9f, 3.02f, Line, 48);
        Draw.Disc(new Vector3(0, ly + 0.006f, 0), 0.18f, Line, 12);

        DrawStands();
        DrawCrowd(celebrate);

        for (int side = 0; side < 2; side++) DrawGoal(side);

        // Solid dasher boards (low wall). The near wall is left out so the camera can see.
        Color board = U.Col(245, 245, 250);
        Color trim = U.Col(40, 70, 140);
        Draw.Box(new Vector3(0, 0.45f, -HalfW - 0.15f), new Vector3(HalfL * 2 + 0.6f, 0.9f, 0.3f), 0, board);
        Draw.Box(new Vector3(0, 0.95f, -HalfW - 0.15f), new Vector3(HalfL * 2 + 0.6f, 0.12f, 0.36f), 0, trim);
        Draw.Box(new Vector3(0, 0.25f, HalfW + 0.15f), new Vector3(HalfL * 2 + 0.6f, 0.5f, 0.3f), 0, board);
        for (int s = -1; s <= 1; s += 2)
        {
            float x = s * (HalfL + 0.15f);
            float gw = GoalHalfW[s < 0 ? 0 : 1];
            float segLen = HalfW - gw;
            float segC = gw + segLen / 2;
            Draw.Box(new Vector3(x, 0.45f, -segC), new Vector3(0.3f, 0.9f, segLen), 0, board);
            Draw.Box(new Vector3(x, 0.45f, segC), new Vector3(0.3f, 0.9f, segLen), 0, board);
            Draw.Box(new Vector3(x, 0.95f, -segC), new Vector3(0.36f, 0.12f, segLen), 0, trim);
            Draw.Box(new Vector3(x, 0.95f, segC), new Vector3(0.36f, 0.12f, segLen), 0, trim);
            // Silly sponsor blocks on the boards.
            for (int k = 0; k < 3; k++)
            {
                Color ad = k switch { 0 => U.Col(250, 90, 60), 1 => U.Col(255, 205, 50), _ => U.Col(80, 190, 220) };
                Draw.Box(new Vector3(s * 7.5f + (k - 1) * 3.8f, 0.5f, -HalfW + 0.02f), new Vector3(3.2f, 0.55f, 0.02f), 0, ad);
            }
        }

        // Lighthouse floodlights in the corners, because this is a *coastal* sporting institution.
        for (int sx = -1; sx <= 1; sx += 2)
        {
            var b = new Vector3(sx * (HalfL + 5.5f), 0, -HalfW - 7f);
            Draw.Limb(b, b + new Vector3(0, 11, 0), 0.8f, 0.55f, U.Col(245, 245, 245), 10);
            for (int k = 0; k < 3; k++)
                Draw.Limb(b + new Vector3(0, 2 + k * 3.4f, 0), b + new Vector3(0, 3.2f + k * 3.4f, 0), 0.76f - k * 0.08f, 0.7f - k * 0.08f, U.Col(220, 50, 50), 10);
            Draw.Limb(b + new Vector3(0, 11, 0), b + new Vector3(0, 12.2f, 0), 0.7f, 0.7f, U.Col(255, 240, 150), 10);
            Draw.Limb(b + new Vector3(0, 12.2f, 0), b + new Vector3(0, 13.2f, 0), 0.85f, 0.05f, U.Col(40, 40, 50), 10);
        }
    }

    // ---------------------------------------------------------------- training room

    const float RoomH = 6f;
    static float RoomEnd => HalfL + GoalDepth + 1.2f;
    static readonly Color RoomFloor = U.Col(34, 56, 92);
    static readonly Color RoomWall = U.Col(196, 215, 232);
    static readonly Color GridMinor = U.Col(95, 135, 185);
    static readonly Color GridMajor = U.Col(175, 210, 245);
    static readonly Color WallGrid = U.Col(170, 192, 214);

    // A closed grid room with the same footprint as the pitch: no crowd, no sea, no distractions.
    public static void DrawTrainingRoom()
    {
        float end = RoomEnd;
        Draw.Box(new Vector3(0, -0.03f, 0), new Vector3(end * 2, 0.04f, HalfW * 2), 0, RoomFloor);

        // Floor grid: 1 m minor lines, 5 m major lines (centred on the halfway line).
        for (int i = -(int)end; i <= (int)end; i++)
        {
            bool major = i % 5 == 0;
            Draw.Box(new Vector3(i, 0.002f, 0), new Vector3(major ? 0.07f : 0.035f, 0.01f, HalfW * 2), 0, major ? GridMajor : GridMinor);
        }
        for (int j = -(int)HalfW; j <= (int)HalfW; j++)
        {
            bool major = j % 5 == 0;
            Draw.Box(new Vector3(0, 0.003f, j), new Vector3(end * 2, 0.01f, major ? 0.07f : 0.035f), 0, major ? GridMajor : GridMinor);
        }
        // Goal lines, so you can tell where "in" starts.
        for (int s = -1; s <= 1; s += 2)
            Draw.Box(new Vector3(s * HalfL, 0.006f, 0), new Vector3(0.1f, 0.01f, GoalHalfW[s < 0 ? 0 : 1] * 2), 0, U.Col(255, 215, 80));

        // Far wall and the walls behind the goals, with a faint grid.
        WallZ(-HalfW - 0.15f, -end, end, 0, RoomH);
        for (int s = -1; s <= 1; s += 2)
        {
            float x = s * (HalfL + 0.15f);
            float gw = GoalHalfW[s < 0 ? 0 : 1];
            WallX(x, -HalfW, -gw, 0, RoomH);
            WallX(x, gw, HalfW, 0, RoomH);
            WallX(x, -gw, gw, GoalH, RoomH);
            WallX(s * end, -HalfW, HalfW, 0, RoomH);
        }

        for (int side = 0; side < 2; side++) DrawGoal(side);
    }

    static void WallZ(float z, float x0, float x1, float y0, float y1)
    {
        float cx = (x0 + x1) / 2, w = x1 - x0, h = y1 - y0;
        Draw.Box(new Vector3(cx, y0 + h / 2, z), new Vector3(w, h, 0.3f), 0, RoomWall);
        float face = z + (z < 0 ? 0.16f : -0.16f);
        for (float x = MathF.Ceiling(x0); x <= x1; x += 1)
            Draw.Box(new Vector3(x, y0 + h / 2, face), new Vector3(0.03f, h, 0.01f), 0, WallGrid);
        for (float y = MathF.Ceiling(y0 + 0.01f); y < y1; y += 1)
            Draw.Box(new Vector3(cx, y, face), new Vector3(w, 0.03f, 0.01f), 0, WallGrid);
    }

    static void WallX(float x, float z0, float z1, float y0, float y1)
    {
        float cz = (z0 + z1) / 2, d = z1 - z0, h = y1 - y0;
        if (d <= 0.01f) return;
        Draw.Box(new Vector3(x, y0 + h / 2, cz), new Vector3(0.3f, h, d), 0, RoomWall);
        float face = x + (x < 0 ? 0.16f : -0.16f);
        for (float z = MathF.Ceiling(z0); z <= z1; z += 1)
            Draw.Box(new Vector3(face, y0 + h / 2, z), new Vector3(0.01f, h, 0.03f), 0, WallGrid);
        for (float y = MathF.Ceiling(y0 + 0.01f); y < y1; y += 1)
            Draw.Box(new Vector3(face, y, cz), new Vector3(0.01f, 0.03f, d), 0, WallGrid);
    }

    // The near wall is see-through so the camera can look into the room.
    public static void DrawTrainingGlass()
    {
        float end = RoomEnd;
        Rlgl.DisableDepthMask();
        Draw.Box(new Vector3(0, RoomH / 2, HalfW + 0.15f), new Vector3(end * 2, RoomH, 0.06f), 0, new Color(200, 225, 250, 28));
        Draw.Box(new Vector3(0, RoomH, HalfW + 0.15f), new Vector3(end * 2, 0.08f, 0.1f), 0, new Color(220, 235, 250, 140));
        Draw.Box(new Vector3(0, 0.02f, HalfW + 0.15f), new Vector3(end * 2, 0.08f, 0.1f), 0, new Color(220, 235, 250, 140));
        Rlgl.EnableDepthMask();
    }

    // Translucent glass drawn last so it doesn't punch holes in things behind it.
    public static void DrawGlass()
    {
        Color glass = new(180, 225, 255, 40);
        Rlgl.DisableDepthMask();
        Draw.Box(new Vector3(0, 0.9f + (WallH - 0.9f) / 2, -HalfW - 0.15f), new Vector3(HalfL * 2 + 0.6f, WallH - 0.9f, 0.06f), 0, glass);
        for (int s = -1; s <= 1; s += 2)
        {
            float x = s * (HalfL + 0.15f);
            float gw = GoalHalfW[s < 0 ? 0 : 1];
            float segLen = HalfW - gw;
            float segC = gw + segLen / 2;
            float h = WallH - 0.9f;
            Draw.Box(new Vector3(x, 0.9f + h / 2, -segC), new Vector3(0.06f, h, segLen), 0, glass);
            Draw.Box(new Vector3(x, 0.9f + h / 2, segC), new Vector3(0.06f, h, segLen), 0, glass);
            Draw.Box(new Vector3(x, GoalH + (WallH - GoalH) / 2, 0), new Vector3(0.06f, WallH - GoalH, gw * 2), 0, glass);
        }
        Rlgl.EnableDepthMask();
    }

    static void DrawClouds()
    {
        for (int i = 0; i < 7; i++)
        {
            float x = ((i * 37 + time * 0.6f) % 180) - 90;
            var c = new Vector3(x, 26 + (i % 3) * 4, -70 - (i % 2) * 15);
            for (int k = 0; k < 4; k++)
                Draw.Ellipsoid(c + new Vector3(k * 3.2f - 5, MathF.Abs(k - 1.5f) * -0.8f, 0), new Vector3(3.5f, 2.2f, 2.5f), U.Col(250, 250, 255), 6, 8);
        }
    }

    static void DrawStands()
    {
        Color concrete = U.Col(180, 190, 205);
        for (int row = 0; row < 4; row++)
        {
            float z = -HalfW - 2.2f - row * 1.4f;
            float y = 0.5f + row * 1.0f;
            Draw.Box(new Vector3(0, y / 2, z), new Vector3(HalfL * 2 + 4, y, 1.4f), 0, U.Shade(concrete, 1 - row * 0.05f));
        }
        for (int s = -1; s <= 1; s += 2)
            for (int row = 0; row < 3; row++)
            {
                float x = s * (HalfL + GoalDepth + 2.4f + row * 1.4f);
                float y = 0.5f + row * 1.0f;
                Draw.Box(new Vector3(x, y / 2, -1f), new Vector3(1.4f, y, HalfW * 2 + 2), 0, U.Shade(concrete, 1 - row * 0.05f));
            }
    }

    static void DrawCrowd(float celebrate)
    {
        float excite = MathF.Max(hype, celebrate);
        foreach (var s in crowd)
        {
            float bob = MathF.Abs(MathF.Sin(time * (3 + excite * 9) + s.Phase)) * (0.05f + excite * 0.55f);
            var p = s.Pos + new Vector3(0, bob, 0);
            Draw.Ellipsoid(p, new Vector3(s.Size * 1.1f, s.Size, s.Size * 0.8f), s.Col, 5, 7);
            // Eyes facing the pitch (toward +z or toward centre for end stands)
            Vector3 toPitch = U.SafeNormalize(new Vector3(-p.X * 0.3f, 0, -p.Z), Vector3.UnitZ);
            if (MathF.Abs(p.X) > HalfL + 2) toPitch = new Vector3(-MathF.Sign(p.X), 0, 0);
            var side = Vector3.Cross(Vector3.UnitY, toPitch);
            var eyeC = p + toPitch * s.Size * 0.7f + new Vector3(0, s.Size * 0.3f, 0);
            Raylib.DrawCube(eyeC + side * s.Size * 0.35f, 0.14f, 0.14f, 0.14f, Color.White);
            Raylib.DrawCube(eyeC - side * s.Size * 0.35f, 0.14f, 0.14f, 0.14f, Color.White);
            Raylib.DrawCube(eyeC + side * s.Size * 0.35f + toPitch * 0.07f, 0.07f, 0.07f, 0.07f, Color.Black);
            Raylib.DrawCube(eyeC - side * s.Size * 0.35f + toPitch * 0.07f, 0.07f, 0.07f, 0.07f, Color.Black);
            // Little fin "arms" raised when excited
            if (excite > 0.3f)
            {
                float wave = MathF.Sin(time * 14 + s.Phase) * 0.2f;
                Draw.Limb(p + side * s.Size, p + side * (s.Size + 0.25f) + new Vector3(0, 0.45f + wave, 0), 0.08f, 0.03f, U.Shade(s.Col, 0.8f), 4);
                Draw.Limb(p - side * s.Size, p - side * (s.Size + 0.25f) + new Vector3(0, 0.45f - wave, 0), 0.08f, 0.03f, U.Shade(s.Col, 0.8f), 4);
            }
        }
    }

    static void DrawGoal(int side)
    {
        float gx = GoalX(side);
        float sgn = side == 0 ? -1 : 1;
        float gw = GoalHalfW[side];
        float back = gx + sgn * GoalDepth;
        Color post = Color.White;
        Draw.Limb(new Vector3(gx, 0, -gw), new Vector3(gx, GoalH, -gw), PostR, PostR, post, 8);
        Draw.Limb(new Vector3(gx, 0, gw), new Vector3(gx, GoalH, gw), PostR, PostR, post, 8);
        Draw.Limb(new Vector3(gx, GoalH, -gw), new Vector3(gx, GoalH, gw), PostR, PostR, post, 8);
        Color frame = U.Col(200, 200, 205);
        Draw.Limb(new Vector3(back, 0, -gw), new Vector3(back, GoalH * 0.8f, -gw), 0.05f, 0.05f, frame, 5);
        Draw.Limb(new Vector3(back, 0, gw), new Vector3(back, GoalH * 0.8f, gw), 0.05f, 0.05f, frame, 5);
        Draw.Limb(new Vector3(gx, GoalH, -gw), new Vector3(back, GoalH * 0.8f, -gw), 0.05f, 0.05f, frame, 5);
        Draw.Limb(new Vector3(gx, GoalH, gw), new Vector3(back, GoalH * 0.8f, gw), 0.05f, 0.05f, frame, 5);
        Draw.Limb(new Vector3(back, GoalH * 0.8f, -gw), new Vector3(back, GoalH * 0.8f, gw), 0.05f, 0.05f, frame, 5);

        // Net: a fishing net, naturally. Deeply upsetting for the players.
        Color net = new(255, 255, 255, 150);
        float step = 0.4f;
        for (float z = -gw; z <= gw + 0.01f; z += step)
        {
            Raylib.DrawLine3D(new Vector3(back, 0, z), new Vector3(back, GoalH * 0.8f, z), net);
            Raylib.DrawLine3D(new Vector3(back, GoalH * 0.8f, z), new Vector3(gx, GoalH, z), net);
        }
        for (float y = 0; y <= GoalH * 0.8f + 0.01f; y += step)
            Raylib.DrawLine3D(new Vector3(back, y, -gw), new Vector3(back, y, gw), net);
        for (float t = 0; t <= 1.001f; t += 0.2f)
        {
            float x = U.Lerp(gx, back, t);
            float yTop = U.Lerp(GoalH, GoalH * 0.8f, t);
            Raylib.DrawLine3D(new Vector3(x, yTop, -gw), new Vector3(x, yTop, gw), net);
            Raylib.DrawLine3D(new Vector3(x, 0, -gw), new Vector3(x, yTop, -gw), net);
            Raylib.DrawLine3D(new Vector3(x, 0, gw), new Vector3(x, yTop, gw), net);
        }
        for (float y = 0; y <= GoalH; y += step)
        {
            float yb = MathF.Min(y, GoalH * 0.8f);
            Raylib.DrawLine3D(new Vector3(gx, y, -gw), new Vector3(back, yb, -gw), net);
            Raylib.DrawLine3D(new Vector3(gx, y, gw), new Vector3(back, yb, gw), net);
        }
    }
}
