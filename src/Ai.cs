using System.Numerics;

namespace FishLegs;

// ROBO-TROUT: a CPU opponent with the tactical depth of a goldfish, which is roughly on brand.
class AiInput
{
    readonly Fish me;
    float chargeTarget = -1;
    float thinkTimer, jitterTimer;
    Vector2 jitter;
    bool wantWavedash;
    bool chaser = true;
    public float Skill = 0.75f;   // 0..1

    public AiInput(Fish me) { this.me = me; }

    public PlayerInput Read(Game g, float dt)
    {
        var inp = new PlayerInput();
        var ball = g.Ball;
        // Mark whichever opponent is closest to the ball.
        var opp = g.Opponents(me).OrderBy(o => Vector3.Distance(o.Pos, ball.Pos)).First();
        float a = me.AttackDir;
        var oppGoal = new Vector2(a * Arena.HalfL, 0);
        var ownGoal = new Vector2(-a * Arena.HalfL, 0);
        var mePos = U.XZ(me.Pos);

        float lead = U.Clamp(Vector2.Distance(mePos, U.XZ(ball.Pos)) / 16f, 0.1f, 0.5f);
        var bp = U.XZ(ball.Pos + ball.Vel * lead);
        bp = Vector2.Clamp(bp, new Vector2(-Arena.HalfL + 0.6f, -Arena.HalfW + 0.6f), new Vector2(Arena.HalfL - 0.6f, Arena.HalfW - 0.6f));

        var toGoal = Vector2.Normalize(oppGoal - bp);
        float myDist = Vector2.Distance(mePos, bp);
        float oppDist = Vector2.Distance(U.XZ(opp.Pos), bp);
        bool ballInMyHalf = bp.X * a < 0;
        bool goalSide = Vector2.Dot(mePos - bp, toGoal) > 0.3f;   // I'm between ball and their goal (wrong side)

        Vector2 target;
        if (oppDist + 2.5f < myDist && ballInMyHalf && opp.CanAct)
        {
            // Defend: stand between ball and own goal.
            var toBall = bp - ownGoal;
            target = ownGoal + Vector2.Normalize(toBall) * MathF.Min(4.5f, toBall.Length() * 0.5f);
        }
        else if (goalSide)
        {
            // Loop around the ball rather than pushing it toward our own goal.
            var perp = new Vector2(-toGoal.Y, toGoal.X);
            float sideSign = MathF.Sign(Vector2.Dot(mePos - bp, perp));
            if (sideSign == 0) sideSign = 1;
            target = bp - toGoal * 1.6f + perp * sideSign * 1.8f;
        }
        else target = bp - toGoal * (me.Radius + ball.R + 0.3f);

        // Human-ish sloppiness.
        jitterTimer -= dt;
        if (jitterTimer <= 0)
        {
            jitterTimer = U.Rand(0.2f, 0.6f);
            jitter = new Vector2(U.Rand(-1, 1), U.Rand(-1, 1)) * (1 - Skill) * 1.5f;
        }
        // 2v2: the teammate nearer the ball chases it (with a little hysteresis); the other supports.
        var mates = g.Teammates(me).ToList();
        if (mates.Count > 0)
        {
            float mine = myDist - (chaser ? 1.5f : 0);
            chaser = mates.All(m => mine <= Vector2.Distance(U.XZ(m.Pos), bp) || !m.CanAct);
            if (!chaser)
            {
                if (ballInMyHalf)
                {
                    // Sit between the ball and our goal, ready to clear.
                    var toBall = bp - ownGoal;
                    target = ownGoal + Vector2.Normalize(toBall) * MathF.Min(5.5f, toBall.Length() * 0.45f);
                }
                else
                {
                    // Trail the play on the opposite flank, ready for a rebound or a pass.
                    float flank = bp.Y > 0 ? -1 : 1;
                    target = bp - toGoal * 5.5f + new Vector2(0, flank * 3.5f);
                }
                target = Vector2.Clamp(target, new Vector2(-Arena.HalfL + 1.5f, -Arena.HalfW + 1.5f), new Vector2(Arena.HalfL - 1.5f, Arena.HalfW - 1.5f));
            }
        }
        target += jitter;

        var to = target - mePos;
        float dist = to.Length();
        Vector2 move = dist > 0.25f ? to / dist : Vector2.Zero;
        if (dist < 1.2f && !goalSide) move = Vector2.Lerp(move, toGoal, 0.6f);
        if (move.Length() > 1) move = Vector2.Normalize(move);
        inp.Move = new Vector2(move.X, -move.Y);   // world z -> screen up

        thinkTimer -= dt;
        bool think = thinkTimer <= 0;
        if (think) thinkTimer = U.Lerp(0.35f, 0.12f, Skill);

        float ballDist3 = Vector3.Distance(ball.Pos, me.Pos + me.Facing * 0.9f + Vector3.UnitY * 0.5f);
        float facingGoal = Vector2.Dot(U.XZ(me.Facing), Vector2.Normalize(oppGoal - mePos));

        // Kicking: hold for a charge proportional to distance from goal.
        if (me.Charging)
        {
            inp.KickDown = me.KickCharge < chargeTarget && ballDist3 < 2.6f;
        }
        else if (think && ballDist3 < 2.1f && facingGoal > 0.55f && !goalSide && me.KickAnim <= 0)
        {
            float d = Vector2.Distance(mePos, oppGoal);
            chargeTarget = U.Clamp(d / 22f, 0.15f, 1f) * U.Rand(0.8f, 1.1f);
            inp.KickDown = true;
            inp.KickPressed = true;
        }
        // Clearances when the ball is near our own goal.
        else if (think && ballDist3 < 2f && Vector2.Distance(bp, ownGoal) < 7 && facingGoal > -0.2f)
        {
            chargeTarget = 0.4f;
            inp.KickDown = true;
        }

        // Air kicks at bouncing balls.
        if (!me.Grounded && ballDist3 < 1.9f && facingGoal > 0.3f && think)
        {
            chargeTarget = 0;
            inp.KickDown = true;
        }

        // Jump for high balls nearby.
        if (think && ball.Pos.Y > 1.8f && Vector2.Distance(U.XZ(ball.Pos), mePos) < 3f && me.Grounded && U.Rand(0, 1) < Skill)
            inp.JumpPressed = true;

        // Dash to close distance; sometimes show off with a wavedash.
        if (think && me.DashCd <= 0 && dist > 5.5f && Vector2.Dot(U.XZ(me.Facing), move) > 0.8f)
        {
            if (me.Grounded && U.Rand(0, 1) < 0.35f * Skill) { inp.JumpPressed = true; wantWavedash = true; }
            else if (me.Grounded) inp.DashPressed = true;
        }
        if (wantWavedash && me.Grounded && me.SinceLand < 0.08f)
        {
            inp.DashPressed = true;
            wantWavedash = false;
        }
        if (me.Grounded && me.SinceLand > 1f) wantWavedash = false;

        // Slap the opponent if they're hogging the ball.
        float oppToMe = Vector3.Distance(opp.BodyCenter, me.BodyCenter);
        if (think && me.SlapCd <= 0 && opp.CanAct && oppToMe < me.S.SlapRadius * me.Sc &&
            (oppDist < 2f || Vector3.Distance(ball.Pos, me.BodyCenter) < 2f) && U.Rand(0, 1) < 0.5f * Skill)
            inp.SlapPressed = true;

        // Dive tackles.
        if (think && !me.Grounded && me.DashCd <= 0 && opp.CanAct && oppToMe < 5 && oppToMe > 2 && oppDist < 1.8f && U.Rand(0, 1) < 0.4f)
        {
            var dir = Vector2.Normalize(U.XZ(opp.Pos) - mePos);
            inp.Move = new Vector2(dir.X, -dir.Y);
            inp.DashPressed = true;
        }
        if (think && me.Grounded && me.DashCd <= 0 && opp.CanAct && oppToMe < 5 && oppToMe > 3 && oppDist < 1.5f && U.Rand(0, 1) < 0.25f * Skill)
            inp.JumpPressed = true;

        // Flop hop out of slides to keep momentum.
        if (me.SlideTimer > 0 && me.SlideTimer < 0.3f && U.Rand(0, 1) < 0.1f) inp.JumpPressed = true;

        return inp;
    }
}
