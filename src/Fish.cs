using System.Numerics;
using Raylib_cs;

namespace FishLegs;

public class Fish
{
    public readonly int Id;
    public string Name;
    public Color BodyCol, BellyCol, FinCol, TeamCol, ShoeCol, SkinCol, LipCol;
    public Stats S = new();
    public readonly List<Upgrade> Owned = new();

    public Vector3 Pos, Vel;
    public float Yaw;
    public bool Grounded = true;
    public int AirJumps;
    public float JumpBuffer, DashCd, DashTimer, SinceLand = 1, LastLandImpact, SlideTimer;
    public float StunTimer, StunImmune;
    public bool Diving, Charging;
    public float KickCharge, KickAnim, SlapAnim, SlapCd, FlailTimer;
    public float Celebrate;          // >0: doing a victory hop
    public float Sulk;               // >0: lying face-down in shame
    float walkPhase, squash, time, turnSqueakCd;
    Vector2 eyeJig, eyeJigVel;
    int kickLeg;
    bool airKick;
    float blink = 3;

    public const float Gravity = 26f;
    public const float KickTime = 0.26f;
    public const float SlapTime = 0.35f;

    public Fish(int id) { Id = id; }

    public float AttackDir => Id == 0 ? 1 : -1;
    public float Sc => S.Scale;
    public float LegLen => 1.4f * S.LegLength * Sc;
    public float HipY => LegLen + 0.05f * Sc;
    public float Radius => 0.72f * Sc;
    public Vector3 Facing => new(MathF.Cos(Yaw), 0, MathF.Sin(Yaw));
    public bool Lying => Diving || SlideTimer > 0 || StunTimer > 0 || Sulk > 0;
    public bool CanAct => StunTimer <= 0 && Sulk <= 0;

    public Vector3 BodyCenter => Lying
        ? Pos + new Vector3(0, 0.5f * Sc, 0)
        : Pos + new Vector3(0, HipY + 0.4f * Sc, 0);

    public void ResetForKickoff(Vector3 pos)
    {
        Pos = pos;
        Vel = Vector3.Zero;
        Yaw = Id == 0 ? 0 : MathF.PI;
        Grounded = true;
        Diving = Charging = false;
        DashCd = DashTimer = SlideTimer = StunTimer = StunImmune = KickCharge = KickAnim = SlapAnim = SlapCd = 0;
        Celebrate = Sulk = 0;
        AirJumps = S.AirJumps;
    }

    // ------------------------------------------------------------------ simulation

    public void Update(float dt, PlayerInput inp, Game g)
    {
        time += dt;
        DashCd = MathF.Max(0, DashCd - dt);
        DashTimer = MathF.Max(0, DashTimer - dt);
        SlapCd = MathF.Max(0, SlapCd - dt);
        SlapAnim = MathF.Max(0, SlapAnim - dt);
        KickAnim = MathF.Max(0, KickAnim - dt);
        FlailTimer = MathF.Max(0, FlailTimer - dt);
        StunImmune = MathF.Max(0, StunImmune - dt);
        turnSqueakCd = MathF.Max(0, turnSqueakCd - dt);
        SinceLand += dt;
        squash = U.Damp(squash, 0, 10, dt);
        UpdateEyes(dt);

        if (StunTimer > 0)
        {
            StunTimer -= dt;
            if (StunTimer <= 0) StunImmune = 0.7f;
            inp = default;
        }
        if (Sulk > 0) { Sulk -= dt; inp = default; }
        if (Celebrate > 0)
        {
            Celebrate -= dt;
            inp = default;
            if (Grounded) { Vel.Y = S.JumpVel * 0.8f; Grounded = false; Yaw += 0.8f; }
        }

        var other = g.Fish[1 - Id];
        Vector2 wish = new(inp.Move.X, -inp.Move.Y);   // screen up = -z
        float speed = S.MoveSpeed;
        if (other.S.Stinky && Vector3.Distance(Pos, other.Pos) < 3.5f) speed *= 0.85f;
        if (S.HomeWaters && Pos.X * AttackDir < 0) speed *= 1.25f;
        if (Charging) speed *= 0.72f;

        // --- kick
        if (CanAct)
        {
            if (inp.KickDown && KickAnim <= 0 && !Diving && SlideTimer <= 0)
            {
                if (!Charging) { Charging = true; KickCharge = 0; }
                KickCharge = MathF.Min(1, KickCharge + dt / S.ChargeTime);
            }
            if (Charging && !inp.KickDown)
            {
                Charging = false;
                DoKick(g, wish);
                KickCharge = 0;
            }
            if (inp.SlapPressed && SlapCd <= 0 && !Diving && SlideTimer <= 0) DoSlap(g);
        }
        else Charging = false;

        // --- jump (buffered so a slightly-early press on landing still counts)
        JumpBuffer = inp.JumpPressed ? 0.12f : JumpBuffer - dt;
        if (JumpBuffer > 0 && CanAct && TryJump(g)) JumpBuffer = 0;

        // --- dash / dive
        if (inp.DashPressed && CanAct && DashCd <= 0) DoDash(g, wish);

        // --- horizontal movement
        Vector2 hv = new(Vel.X, Vel.Z);
        if (Grounded)
        {
            if (StunTimer > 0 || Sulk > 0)
                hv = U.MoveTowards(hv, Vector2.Zero, 18 * dt);
            else if (SlideTimer > 0)
            {
                SlideTimer -= dt;
                hv *= MathF.Exp(-1.1f * dt / S.SlideMult);
                hv += wish * 7 * dt;
                if (U.Rand(0, 1) < 0.5f) g.Fx.Spray(Pos + new Vector3(0, 0.1f, 0), U.FromXZ(-hv * 0.1f, 1.5f), 1);
                if (SlideTimer <= 0) Yaw = MathF.Atan2(hv.Y, hv.X);
            }
            else if (DashTimer > 0) { }
            else
            {
                Vector2 target = wish * speed;
                float cur = hv.Length();
                float accel = wish.LengthSquared() > 0.01f ? 75 : 55;
                if (cur > speed * 1.05f) accel = 22;   // let wavedash momentum bleed off gracefully
                hv = U.MoveTowards(hv, target, accel * dt);
            }
        }
        else
        {
            float before = hv.Length();
            hv += wish * (Diving ? 5 : 28) * dt;
            float cap = MathF.Max(before, speed);
            if (hv.Length() > cap) hv = Vector2.Normalize(hv) * cap;
        }
        Vel.X = hv.X;
        Vel.Z = hv.Y;

        // --- facing
        if (Diving || SlideTimer > 0)
        {
            if (hv.LengthSquared() > 1) Yaw = U.TurnTowards(Yaw, MathF.Atan2(hv.Y, hv.X), 6 * dt);
        }
        else if (CanAct && wish.LengthSquared() > 0.04f)
        {
            float target = MathF.Atan2(wish.Y, wish.X);
            float diff = MathF.Abs(U.WrapAngle(target - Yaw));
            if (Grounded && diff > 2.2f && hv.Length() > 6 && turnSqueakCd <= 0)
            {
                Audio.PlayVaried(Sfx.Squeak, 0.15f, 0.6f);
                g.Fx.Dust(Pos, 5);
                turnSqueakCd = 0.3f;
                eyeJigVel += new Vector2(U.Rand(-4, 4), U.Rand(-3, 3));
            }
            Yaw = U.TurnTowards(Yaw, target, (Charging ? 9 : 16) * dt);
        }

        // --- vertical
        if (!Grounded || Vel.Y > 0)
        {
            Vel.Y -= Gravity * (Diving ? 1.15f : 1f) * dt;
            Grounded = false;
        }
        Pos += Vel * dt;

        if (Pos.Y <= 0)
        {
            Pos.Y = 0;
            if (!Grounded) Land(-Vel.Y, g);
            Vel.Y = 0;
            Grounded = true;
        }

        CollideWalls(g);

        if (Grounded && !Lying) walkPhase += hv.Length() * dt * 2.6f / MathF.Max(0.6f, LegLen);
        blink -= dt;
        if (blink < -0.12f) blink = U.Rand(1.5f, 4.5f);
    }

    bool TryJump(Game g)
    {
        if (Grounded && SlideTimer > 0)
        {
            // FLOP HOP: jump out of a belly slide, keeping (and boosting) all that slippery momentum.
            SlideTimer = 0;
            var hv = new Vector2(Vel.X, Vel.Z);
            float boost = 1.18f + 0.18f * (S.SlideMult - 1);
            hv *= boost;
            if (hv.Length() > 27) hv = Vector2.Normalize(hv) * 27;
            Vel = new Vector3(hv.X, S.JumpVel * 0.95f, hv.Y);
            Grounded = false;
            Yaw = MathF.Atan2(hv.Y, hv.X);
            g.Popup(BodyCenter, "FLOP HOP!", U.Col(120, 255, 200));
            g.TechUsed(this, "flophop");
            Audio.Play(Sfx.Boing, 0.8f);
            g.Fx.Spray(Pos, new Vector3(0, 4, 0), 10);
            return true;
        }
        if (Grounded)
        {
            Vel.Y = S.JumpVel;
            Grounded = false;
            squash = -0.15f;
            Audio.PlayVaried(Sfx.Boing, 0.08f, 0.55f);
            return true;
        }
        // Wall kick
        float r = Radius + 0.45f;
        float dx = Arena.HalfL - MathF.Abs(Pos.X), dz = Arena.HalfW - MathF.Abs(Pos.Z);
        if (MathF.Min(dx, dz) < r && Pos.Y > 0.25f)
        {
            Vector3 n = dx < dz ? new Vector3(-MathF.Sign(Pos.X), 0, 0) : new Vector3(0, 0, -MathF.Sign(Pos.Z));
            Vector3 tangential = U.Flat(Vel) - n * Vector3.Dot(U.Flat(Vel), n);
            Vel = tangential * 0.85f + n * 10.5f;
            Vel.Y = S.JumpVel * (S.WallRunner ? 1.15f : 0.95f);
            AirJumps = S.AirJumps;
            Diving = false;
            if (S.WallRunner) DashCd = 0;
            Yaw = MathF.Atan2(Vel.Z, Vel.X);
            g.Popup(BodyCenter, "WALL KICK!", U.Col(120, 200, 255));
            g.Rumble(Id, 0.25f, 0.08f);
            g.TechUsed(this, "wallkick");
            Audio.Play(Sfx.Kick, 1.4f, 0.8f);
            Audio.Play(Sfx.Boing, 1.2f, 0.5f);
            g.Fx.Dust(Pos + new Vector3(0, 0.8f, 0) - n * 0.5f, 8);
            return true;
        }
        if (AirJumps > 0)
        {
            AirJumps--;
            g.TechUsed(this, "airjump");
            Vel.Y = S.JumpVel * 0.88f;
            Diving = false;
            FlailTimer = 0.45f;
            Audio.Play(Sfx.Boing, 1.3f + U.Rand(0, 0.1f), 0.5f);
            g.Fx.Puff(Pos + new Vector3(0, 0.3f, 0), 6);
            return true;
        }
        return false;
    }

    void DoDash(Game g, Vector2 wish)
    {
        Vector2 dir = wish.LengthSquared() > 0.04f ? Vector2.Normalize(wish) : new Vector2(MathF.Cos(Yaw), MathF.Sin(Yaw));
        Vector2 hv = new(Vel.X, Vel.Z);
        if (Grounded)
        {
            // WAVEDASH: dash within a hair's breadth of landing for bonus speed and a cheaper cooldown.
            bool wave = SinceLand < 0.15f && LastLandImpact > 5;
            float spd = S.DashSpeed * (wave ? 1.32f : 1f);
            if (SlideTimer > 0) { SlideTimer = 0; spd *= 1.08f; }
            hv = dir * MathF.Max(spd, Vector2.Dot(hv, dir));
            DashTimer = 0.17f;
            DashCd = S.DashCooldown * (wave ? 0.35f : 1f);
            g.DashTiming(this, SinceLand, LastLandImpact, wave);
            Yaw = MathF.Atan2(dir.Y, dir.X);
            if (wave)
            {
                g.Popup(BodyCenter, "WAVEDASH!", U.Col(255, 230, 90));
                g.TechUsed(this, "wavedash");
                Audio.Play(Sfx.Wavedash);
                Audio.Play(Sfx.Squeak, 1.3f, 0.7f);
            }
            else Audio.PlayVaried(Sfx.Whoosh, 0.1f, 0.8f);
            g.Fx.Dust(Pos, wave ? 14 : 7);
        }
        else
        {
            // DIVE: a committed, horizontal belly flop. Lands into a slide.
            Diving = true;
            Charging = false;
            g.TechUsed(this, "dive");
            hv = dir * MathF.Max(S.DashSpeed * 0.95f, Vector2.Dot(hv, dir));
            Vel.Y = MathF.Max(Vel.Y * 0.3f, 0) + 3.5f;
            DashCd = S.DashCooldown;
            Yaw = MathF.Atan2(dir.Y, dir.X);
            Audio.Play(Sfx.Whoosh, 0.75f);
            g.Fx.Puff(BodyCenter, 5);
        }
        Vel.X = hv.X;
        Vel.Z = hv.Y;
        eyeJigVel += new Vector2(-6, U.Rand(-2, 2));
    }

    void Land(float impact, Game g)
    {
        SinceLand = 0;
        LastLandImpact = impact;
        AirJumps = S.AirJumps;
        eyeJigVel += new Vector2(0, -impact * 0.4f);
        if (Diving)
        {
            Diving = false;
            SlideTimer = 0.6f * S.SlideMult;
            g.TechUsed(this, "slide");
            Vel.X *= 1.05f;
            Vel.Z *= 1.05f;
            Audio.PlayVaried(Sfx.Splat, 0.1f);
            g.Fx.Spray(Pos + new Vector3(0, 0.3f, 0), new Vector3(0, 4, 0), 16);
            g.Shake(0.15f);
            g.Rumble(Id, 0.35f, 0.15f);
        }
        else if (StunTimer <= 0)
        {
            squash = MathF.Min(0.3f, impact * 0.018f);
            if (impact > 9)
            {
                Audio.Play(Sfx.Bounce, 0.6f, 0.6f);
                g.Fx.Dust(Pos, 6);
            }
        }
        else if (impact > 4) Audio.PlayVaried(Sfx.Splat, 0.2f, 0.7f);
    }

    void DoKick(Game g, Vector2 wish)
    {
        float charge = KickCharge;
        float power01 = U.Lerp(0.3f, 1f, MathF.Pow(charge, 0.8f));
        KickAnim = KickTime;
        kickLeg = 1 - kickLeg;
        airKick = !Grounded;
        var f = Facing;
        var up = Vector3.UnitY;

        Vector3 contact = airKick
            ? BodyCenter + f * 0.55f * Sc + up * 0.1f * Sc
            : Pos + f * 0.95f * Sc + up * 0.5f * Sc;
        float reach = (airKick ? 1.5f : 1.3f) * Sc + g.Ball.R;
        var ball = g.Ball;

        if (Vector3.Distance(ball.Pos, contact) < reach)
        {
            float lift = airKick ? 0.06f : 0.1f + 0.4f * charge;
            // Kicking a ball that's above you pops it up a bit more (headers-by-foot).
            if (!airKick && ball.Pos.Y > 1.4f * Sc) lift += 0.2f;
            var dir = Vector3.Normalize(f + up * lift);
            float spd = (11 + 19 * power01) * S.KickPower * (airKick ? S.AirKickMult * 1.1f : 1f);
            ball.Vel = dir * spd + U.Flat(Vel) * 0.35f;
            ball.Spin = Vector3.Zero;
            if (S.Curve)
            {
                var perp = new Vector2(-f.Z, f.X);
                float lateral = Vector2.Dot(wish, perp);
                if (MathF.Abs(lateral) > 0.2f) ball.Spin = U.FromXZ(perp * lateral * (10 + 10 * power01));
            }
            ball.Touch(Id);
            g.Rumble(Id, 0.2f + 0.6f * power01, 0.08f + 0.12f * charge);

            if (power01 > 0.75f)
            {
                g.Hitstop(0.06f + 0.04f * charge);
                g.Shake(0.25f + 0.2f * charge);
                Audio.PlayVaried(Sfx.BigKick, 0.08f);
                g.Fx.Burst(ball.Pos, TeamCol, 18);
            }
            else
            {
                Audio.PlayVaried(Sfx.Kick, 0.1f);
                g.Fx.Burst(ball.Pos, Color.White, 6);
            }
            if (airKick)
            {
                g.Popup(BodyCenter + up * 0.6f, U.Pick(new[] { "SCISSOR SHINS!", "BICYCLE KICK!", "AIR LEG!" }), U.Col(255, 150, 255));
                g.TechUsed(this, "airkick");
            }
            if (charge >= 0.99f) g.TechUsed(this, "fullkick");
            if (!airKick && charge >= 0.99f) g.Popup(BodyCenter + up * 0.6f, U.Pick(new[] { "LEG DAY!", "WHAMMY!", "SHIN-SANITY!" }), U.Col(255, 120, 80));
            eyeJigVel += new Vector2(U.Rand(-5, 5), 5);
            g.Fx.Grass(Pos + f * 0.8f, 5);
            return;
        }

        var o = g.Fish[1 - Id];
        if (o.StunTimer <= 0 && o.StunImmune <= 0 && Vector3.Distance(o.Pos + up * 0.6f * o.Sc, contact) < 1.3f * Sc + o.Radius * 0.5f)
        {
            o.Stun(0.5f + 0.3f * charge, f * (6 + 5 * charge) / o.S.Mass + up * 4, g);
            g.Popup(o.BodyCenter + up * 0.6f, "SHINNED!", U.Col(255, 90, 90));
            g.TechUsed(this, "shin");
            g.Comment(Commentary.Shinned(this, o));
            Audio.Play(Sfx.Slap, 0.8f);
            g.Hitstop(0.05f);
            return;
        }

        Audio.PlayVaried(Sfx.Whoosh, 0.1f, 0.5f);
        if (charge > 0.9f && U.Rand(0, 1) < 0.5f) g.Popup(BodyCenter + up * 0.6f, "whiff", U.Col(200, 200, 200));
    }

    void DoSlap(Game g)
    {
        SlapAnim = SlapTime;
        SlapCd = S.SlapCooldown;
        Charging = false;
        Audio.PlayVaried(Sfx.Whoosh, 0.1f, 0.7f);
        var c = BodyCenter;
        float rad = S.SlapRadius * Sc;
        var ball = g.Ball;
        var up = Vector3.UnitY;
        bool hitSomething = false;

        if (Vector3.Distance(ball.Pos, c) < rad + ball.R)
        {
            var dir = U.SafeNormalize(U.Flat(ball.Pos - Pos), Facing);
            ball.Vel = dir * 15 + up * 5;
            ball.Touch(Id);
            Audio.PlayVaried(Sfx.Slap, 0.1f);
            g.Popup(ball.Pos + up, "TAIL SLAP!", U.Col(255, 170, 230));
            g.Fx.Burst(ball.Pos, Color.White, 8);
            hitSomething = true;
        }

        var o = g.Fish[1 - Id];
        if (o.StunTimer <= 0 && o.StunImmune <= 0 && Vector3.Distance(o.BodyCenter, c) < rad + o.Radius * 0.6f)
        {
            var dir = U.SafeNormalize(U.Flat(o.Pos - Pos), Facing);
            o.Stun(S.SlapStun / MathF.Sqrt(o.S.Mass), (dir * 11 + up * 6) / o.S.Mass, g);
            Audio.Play(Sfx.Slap, 0.7f, 1f);
            g.Popup(o.BodyCenter + up * 0.8f, "SLAPPED!", U.Col(255, 120, 200));
            g.TechUsed(this, "slap");
            g.Comment(Commentary.Slapped(this, o));
            g.Shake(0.3f);
            g.Hype(0.3f);
            hitSomething = true;
        }
        if (hitSomething)
        {
            g.Hitstop(0.06f);
            g.Rumble(Id, 0.45f, 0.12f);
        }
    }

    public void Stun(float t, Vector3 knock, Game g)
    {
        StunTimer = t;
        Vel = knock;
        if (knock.Y > 0) Grounded = false;
        Charging = false;
        KickCharge = 0;
        Diving = false;
        SlideTimer = 0;
        eyeJigVel += new Vector2(U.Rand(-12, 12), U.Rand(-12, 12));
        Audio.Play(Sfx.Blub, U.Rand(0.9f, 1.2f));
        g.Fx.Stars(BodyCenter, 6);
        g.Rumble(Id, 0.9f, 0.35f);
    }

    void CollideWalls(Game g)
    {
        float r = Radius;
        float lx = Arena.HalfL - r, lz = Arena.HalfW - r;
        float impact = 0;
        if (MathF.Abs(Pos.X) > lx) { impact = MathF.Max(impact, MathF.Abs(Vel.X)); Pos.X = MathF.Sign(Pos.X) * lx; Vel.X *= Lying ? -0.4f : 0; }
        if (MathF.Abs(Pos.Z) > lz) { impact = MathF.Max(impact, MathF.Abs(Vel.Z)); Pos.Z = MathF.Sign(Pos.Z) * lz; Vel.Z *= Lying ? -0.4f : 0; }
        if (impact > 14 && (Diving || SlideTimer > 0))
        {
            g.Popup(BodyCenter, "BONK", U.Col(230, 230, 230));
            Audio.Play(Sfx.Bounce, 0.5f);
            g.Shake(0.2f);
        }
    }

    void UpdateEyes(float dt)
    {
        // Damped spring: the pupils are googly and they are *not* attached very well.
        eyeJigVel += (-eyeJig * 90f - eyeJigVel * 5f) * dt;
        eyeJig += eyeJigVel * dt;
        if (!Grounded) eyeJigVel.Y += Vel.Y * 0.4f * dt;
        eyeJig = Vector2.Clamp(eyeJig, new Vector2(-1.2f), new Vector2(1.2f));
    }

    // ------------------------------------------------------------------ rendering

    Matrix4x4 BodyMatrix(out float pitchDeg, out float rollDeg, out float yaw)
    {
        pitchDeg = 0;
        rollDeg = 0;
        var hv = new Vector2(Vel.X, Vel.Z);
        float speed01 = U.Clamp01(hv.Length() / 12f);
        if (Diving) pitchDeg = U.Clamp(Vel.Y * 3, -30, 15);
        else if (SlideTimer > 0) pitchDeg = -4;
        else if (StunTimer > 0 || Sulk > 0) rollDeg = 90;
        else
        {
            pitchDeg = -speed01 * 12;
            if (Charging) pitchDeg += 10 * KickCharge;
            if (KickAnim > 0) pitchDeg += (airKick ? 60 : 12) * MathF.Sin(MathF.PI * (1 - KickAnim / KickTime));
            if (!Grounded && FlailTimer > 0) rollDeg = MathF.Sin(FlailTimer * 30) * 10;
        }
        yaw = Yaw;
        if (SlapAnim > 0) yaw += MathF.Tau * (1 - SlapAnim / SlapTime);
        if (StunTimer > 0 && !Grounded) yaw += time * 12;

        var center = BodyCenter;
        if (!Lying)
        {
            float bob = MathF.Abs(MathF.Sin(walkPhase)) * 0.08f * Sc * speed01;
            center.Y += bob - squash * 0.4f;
        }
        else if (StunTimer > 0 || Sulk > 0) center.Y = Pos.Y + 0.45f * Sc;

        return Matrix4x4.CreateRotationZ(pitchDeg * MathF.PI / 180)
             * Matrix4x4.CreateRotationX(rollDeg * MathF.PI / 180)
             * Matrix4x4.CreateRotationY(-yaw)
             * Matrix4x4.CreateTranslation(center);
    }

    public void Render(float t)
    {
        var M = BodyMatrix(out float pitchDeg, out float rollDeg, out float yaw);
        float sc = Sc;
        Vector3 L(float x, float y, float z) => Vector3.Transform(new Vector3(x, y, z) * sc, M);
        Vector3 Dir(float x, float y, float z) => Vector3.Normalize(Vector3.TransformNormal(new Vector3(x, y, z), M));

        Draw.Shadow(Pos, 0.95f * sc, U.Clamp01(1 - Pos.Y / 8));

        // ------------ legs first (so the body sits on top of the hips)
        var f = Dir(1, 0, 0);
        var belly = Dir(0, -1, 0);
        var flatF = Facing;
        var up = Vector3.UnitY;
        float legLen = LegLen;
        float thigh = legLen * 0.5f, shin = legLen * 0.5f;
        var hv = new Vector2(Vel.X, Vel.Z);
        float speed01 = U.Clamp01(hv.Length() / 12f);

        for (int leg = 0; leg < 2; leg++)
        {
            float sideSign = leg == 0 ? 1 : -1;
            Vector3 hip = L(0.05f, -0.4f, 0.24f * sideSign);
            Vector3 foot;
            Vector3 bendHint;

            if (StunTimer > 0 || Sulk > 0)
            {
                // Legs in the air, kicking like an upturned beetle.
                float fl = Sulk > 0 ? 0.3f : 1f;
                var wiggle = flatF * MathF.Sin(t * 22 + leg * 2) * 0.35f * fl + up * MathF.Cos(t * 19 + leg) * 0.2f * fl;
                foot = hip + Vector3.Normalize(belly + up * 0.9f) * legLen * 0.85f + wiggle;
                bendHint = flatF;
            }
            else if (Diving || SlideTimer > 0)
            {
                // Trailing legs doing a frantic freestyle flutter-kick.
                float kickAmp = SlideTimer > 0 ? 0.25f : 0.4f;
                foot = hip - flatF * legLen * 0.9f + up * (0.25f + MathF.Sin(t * 28 + leg * MathF.PI) * kickAmp);
                bendHint = -up;
            }
            else if (!Grounded)
            {
                if (KickAnim > 0 && airKick && leg == kickLeg)
                {
                    float p = 1 - KickAnim / KickTime;
                    float a = U.Lerp(-0.6f, 2.6f, p);
                    foot = hip + (flatF * MathF.Sin(a) - up * MathF.Cos(a)) * legLen * 0.95f;
                }
                else if (FlailTimer > 0 || Celebrate > 0)
                {
                    float a = t * 26 + leg * MathF.PI;
                    foot = hip - up * legLen * 0.65f + (flatF * MathF.Cos(a) + up * MathF.Sin(a)) * legLen * 0.3f;
                }
                else
                {
                    float dangle = MathF.Sin(t * 9 + leg * 1.7f) * 0.15f;
                    foot = hip - up * legLen * 0.9f + flatF * (dangle - 0.1f + Vel.Y * -0.015f);
                }
                bendHint = flatF;
            }
            else if (KickAnim > 0 && leg == kickLeg)
            {
                float p = 1 - KickAnim / KickTime;
                float a = U.Lerp(-1.1f, 1.5f, MathF.Sin(p * MathF.PI * 0.5f));
                foot = hip + (flatF * MathF.Sin(a) - up * MathF.Cos(a)) * legLen * 0.97f;
                bendHint = flatF;
            }
            else if (Charging && leg == kickLeg)
            {
                float a = -0.5f - 0.8f * KickCharge + MathF.Sin(t * 40) * 0.04f * KickCharge;
                foot = hip + (flatF * MathF.Sin(a) - up * MathF.Cos(a)) * legLen * 0.9f;
                bendHint = flatF;
            }
            else
            {
                var ground = new Vector3(hip.X, Pos.Y, hip.Z);
                if (speed01 > 0.05f)
                {
                    float ph = walkPhase + leg * MathF.PI;
                    float stride = MathF.Min(1.3f, speed01 * 1.2f) * 0.55f * legLen;
                    var moveDir = new Vector3(hv.X, 0, hv.Y) / MathF.Max(hv.Length(), 0.001f);
                    foot = ground + moveDir * MathF.Sin(ph) * stride + up * (0.07f + MathF.Max(0, MathF.Cos(ph)) * stride * 0.6f);
                }
                else
                {
                    // Idle: occasional impatient foot tap.
                    float tap = leg == 0 ? MathF.Max(0, MathF.Sin(t * 7)) * 0.12f * (MathF.Sin(t * 0.7f) > 0.3f ? 1 : 0) : 0;
                    foot = ground + flatF * 0.08f + up * (0.07f + tap);
                }
                bendHint = flatF;
            }

            DrawLeg(hip, foot, thigh, shin, bendHint, flatF, leg);
        }

        // ------------ body
        Rlgl.PushMatrix();
        var center = Vector3.Transform(Vector3.Zero, M);
        Rlgl.Translatef(center.X, center.Y, center.Z);
        Rlgl.Rotatef(-yaw * 180 / MathF.PI, 0, 1, 0);
        Rlgl.Rotatef(rollDeg, 1, 0, 0);
        Rlgl.Rotatef(pitchDeg, 0, 0, 1);
        Rlgl.Scalef(sc, sc, sc);

        float stretch = 1 + squash * 0.6f;
        Draw.Ellipsoid(new Vector3(1.05f * stretch, 0.62f / stretch, 0.42f), BodyCol, 12, 16);
        Draw.Ellipsoid(new Vector3(0.05f, -0.2f, 0), new Vector3(0.88f, 0.44f, 0.38f), BellyCol, 10, 14);
        // Team band ("the jersey")
        Draw.Ellipsoid(new Vector3(-0.2f, 0, 0), new Vector3(0.22f, 0.635f / stretch, 0.43f), TeamCol, 8, 14);

        // Tail with a wag that gets frantic when running.
        float wag = MathF.Sin(t * (6 + speed01 * 14)) * (0.25f + speed01 * 0.3f);
        var tb = new Vector3(-0.95f, 0, 0);
        var tTop = new Vector3(-1.7f, 0.6f, wag);
        var tMid = new Vector3(-1.45f, 0, wag * 0.7f);
        var tBot = new Vector3(-1.7f, -0.6f, wag);
        Draw.Tri2(tb, tTop, tMid, FinCol);
        Draw.Tri2(tb, tMid, tBot, FinCol);
        // Dorsal fin
        Draw.Tri2(new Vector3(0.35f, 0.5f, 0), new Vector3(-0.55f, 0.48f, 0), new Vector3(-0.3f, 1.0f, 0), FinCol);
        Draw.Tri2(new Vector3(0.1f, 0.55f, 0), new Vector3(-0.1f, 0.9f, 0), new Vector3(-0.3f, 1.0f, 0), U.Shade(FinCol, 0.85f));

        // Pectoral fins, used as arms. Pumping when running, raised when charging, flapping when stunned.
        for (int s = -1; s <= 1; s += 2)
        {
            float flap = Charging ? 0.9f
                : StunTimer > 0 ? MathF.Sin(t * 30) * 0.8f
                : Celebrate > 0 ? 1.2f + MathF.Sin(t * 20) * 0.3f
                : MathF.Sin(walkPhase + (s > 0 ? 0 : MathF.PI)) * 0.6f * speed01 + 0.15f;
            var root = new Vector3(0.3f, -0.12f, 0.36f * s);
            var tip = root + new Vector3(-0.45f, 0.2f + flap * 0.35f, (0.35f + MathF.Abs(flap) * 0.1f) * s);
            var tip2 = root + new Vector3(-0.15f, -0.2f + flap * 0.35f, 0.3f * s);
            Draw.Tri2(root, tip, tip2, FinCol);
        }

        // Lips. Mouth hangs open when charging, stunned, or celebrating.
        float open = Charging ? 0.08f + KickCharge * 0.1f : StunTimer > 0 || Celebrate > 0 ? 0.16f : 0.02f + MathF.Abs(MathF.Sin(t * 2)) * 0.02f;
        Draw.Ellipsoid(new Vector3(1.0f, -0.04f, 0), new Vector3(0.12f, 0.06f, 0.17f), LipCol, 6, 10);
        Draw.Ellipsoid(new Vector3(0.98f, -0.15f - open, 0), new Vector3(0.11f, 0.06f, 0.16f), LipCol, 6, 10);
        if (open > 0.05f)
            Draw.Ellipsoid(new Vector3(0.96f, -0.1f - open * 0.5f, 0), new Vector3(0.05f, 0.03f + open * 0.4f, 0.12f), U.Col(60, 20, 30), 6, 8);

        // Googly eyes.
        for (int s = -1; s <= 1; s += 2)
        {
            var eye = new Vector3(0.6f, 0.2f, 0.3f * s);
            float eyeR = 0.21f;
            bool closed = blink < 0 || Sulk > 0;
            Draw.Ellipsoid(eye, new Vector3(eyeR, closed ? 0.04f : eyeR, eyeR), Color.White, 8, 10);
            if (!closed)
            {
                var look = Vector3.Normalize(new Vector3(0.55f + eyeJig.X * 0.3f, eyeJig.Y * 0.5f, 1f * s));
                if (StunTimer > 0) look = Vector3.Normalize(new Vector3(MathF.Cos(t * 15 * s), MathF.Sin(t * 15 * s), 1.2f * s));
                Draw.Ellipsoid(eye + look * eyeR * 0.78f, new Vector3(0.09f), Color.Black, 6, 8);
            }
            // Eyebrows: angry when charging, worried when stunned, smug when celebrating.
            float brow = Charging ? -30 : StunTimer > 0 ? 25 : Celebrate > 0 ? 15 : 0;
            Rlgl.PushMatrix();
            Rlgl.Translatef(0.62f, 0.45f, 0.32f * s);
            Rlgl.Rotatef(brow, 0, 0, 1);
            Raylib.DrawCube(Vector3.Zero, 0.28f, 0.05f, 0.08f, U.Shade(BodyCol, 0.4f));
            Rlgl.PopMatrix();
        }
        Rlgl.PopMatrix();

        // Unwashed socks: a lingering green haze.
        if (S.Stinky)
        {
            for (int i = 0; i < 4; i++)
            {
                float a = t * 1.3f + i * MathF.Tau / 4;
                var p = Pos + new Vector3(MathF.Cos(a) * 1.4f, 0.4f + MathF.Sin(t * 2 + i) * 0.2f, MathF.Sin(a) * 1.4f);
                stinkPuffs.Add(p);
            }
        }
    }

    // Collected during the opaque pass, drawn in the transparent pass.
    readonly List<Vector3> stinkPuffs = new();

    void DrawLeg(Vector3 hip, Vector3 foot, float l1, float l2, Vector3 hint, Vector3 f, int leg)
    {
        var d = foot - hip;
        float dist = d.Length();
        float maxLen = (l1 + l2) * 0.999f;
        if (dist > maxLen) { foot = hip + d / dist * maxLen; dist = maxLen; }
        dist = MathF.Max(dist, 0.05f);
        var dir = (foot - hip) / dist;
        float a = (l1 * l1 - l2 * l2 + dist * dist) / (2 * dist);
        float h = MathF.Sqrt(MathF.Max(0, l1 * l1 - a * a));
        var bend = U.SafeNormalize(hint - dir * Vector3.Dot(hint, dir), Vector3.UnitX);
        var knee = hip + dir * a + bend * h;

        float th = S.Thighs * Sc * 1.2f;
        Draw.Limb(hip, knee, 0.17f * th, 0.13f * th, SkinCol, 9);
        Draw.Ellipsoid(knee, new Vector3(0.135f * th), SkinCol, 6, 8);
        Draw.Limb(knee, foot, 0.13f * th, 0.085f * th, SkinCol, 9);
        // Sock with stripes
        var sockTop = foot + (knee - foot) * 0.38f;
        Draw.Limb(foot, sockTop, 0.1f * th, 0.11f * th, Color.White, 8);
        var s1 = foot + (knee - foot) * 0.28f;
        Draw.Limb(s1, s1 + (knee - foot) * 0.06f, 0.113f * th, 0.113f * th, TeamCol, 8);
        // Sneaker
        Rlgl.PushMatrix();
        var shoe = foot + f * 0.1f * Sc - Vector3.UnitY * 0.02f;
        Rlgl.Translatef(shoe.X, shoe.Y, shoe.Z);
        Rlgl.Rotatef(-Yaw * 180 / MathF.PI, 0, 1, 0);
        Draw.Ellipsoid(new Vector3(0.24f * Sc, 0.1f * Sc, 0.12f * Sc), ShoeCol, 6, 10);
        Raylib.DrawCube(new Vector3(0, -0.07f * Sc, 0), 0.46f * Sc, 0.05f * Sc, 0.22f * Sc, Color.White);
        Rlgl.PopMatrix();

        DrawHairs(hip, knee, 0.15f * th, leg * 1000);
        DrawHairs(knee, sockTop, 0.11f * th, leg * 1000 + 500);
    }

    void DrawHairs(Vector3 a, Vector3 b, float radius, int seed)
    {
        var axis = b - a;
        float len = axis.Length();
        if (len < 0.01f) return;
        axis /= len;
        var p1 = U.SafeNormalize(Vector3.Cross(axis, Vector3.UnitY), Vector3.UnitX);
        var p2 = Vector3.Cross(axis, p1);
        int n = (int)(20 * S.HairDensity);
        var hair = U.Col(45, 30, 22);
        for (int i = 0; i < n; i++)
        {
            int k = seed + i * 7 + Id * 31;
            float t = 0.1f + 0.85f * (U.Hash(k) * 0.5f + 0.5f);
            float ang = U.Hash(k + 1) * MathF.PI;
            var outward = p1 * MathF.Cos(ang) + p2 * MathF.Sin(ang);
            var root = a + axis * (t * len) + outward * radius;
            // Hairs point outward and slightly down the leg, and quiver when running.
            float quiver = MathF.Sin(time * 30 + i) * 0.15f * U.Clamp01(new Vector2(Vel.X, Vel.Z).Length() / 8);
            var hdir = Vector3.Normalize(outward + axis * (0.6f + U.Hash(k + 2) * 0.3f) + p2 * quiver);
            float hl = (0.09f + 0.06f * (U.Hash(k + 3) * 0.5f + 0.5f)) * Sc * (0.8f + 0.2f * S.HairDensity);
            Draw.Limb(root, root + hdir * hl, 0.016f * Sc, 0.005f * Sc, hair, 3);
        }
    }

    public void DrawTransparent()
    {
        foreach (var p in stinkPuffs)
            Draw.Ellipsoid(p, new Vector3(0.45f), new Color(140, 190, 60, 50), 6, 8);
        stinkPuffs.Clear();
    }
}
