using System.Numerics;
using Raylib_cs;

namespace FishLegs;

public class Game
{
    enum State { Title, HowTo, Kickoff, Play, Goal, Draft, Victory }

    public const int WinScore = 5;
    const float Step = 1f / 120f;

    public readonly Fish[] Fish = { new(0), new(1) };
    public readonly Ball Ball = new();
    public readonly Fx Fx = new();

    State state = State.Title;
    float stateTime, gameTime, realTime;
    bool paused, vsCpu = true, quit;
    int round, titleSel, lastScorer = -1;
    readonly int[] score = new int[2];

    readonly HumanInput[] humans = new HumanInput[2];
    readonly AiInput[] ais = new AiInput[2];
    readonly PlayerInput[] pending = new PlayerInput[2];

    Camera3D cam;
    Vector3 camPos = new(0, 20, 30), camTarget;
    float shake, hitstop, simAccum, timeScale = 1;

    string comment = "";
    float commentAge = 99;

    readonly string[] lastTech = { "", "" };
    readonly float[] lastTechAt = { -99, -99 };
    readonly Dictionary<string, int>[] techCount = { new(), new() };

    List<Upgrade>[] draftOptions = { new(), new() };
    readonly int[] draftCursor = new int[2];
    readonly bool[] draftReady = new bool[2];
    readonly float[] cpuPickAt = new float[2];

    public bool Quit => quit;
    public bool DebugAiP1;   // let ROBO-TROUT drive player 1 too (used by the screenshot harness)

    public Game()
    {
        cam = new Camera3D { Position = camPos, Target = Vector3.Zero, Up = Vector3.UnitY, FovY = 45, Projection = CameraProjection.Perspective };
        ais[0] = new AiInput(Fish[0]) { Skill = 0.7f };
        ais[1] = new AiInput(Fish[1]) { Skill = 0.75f };
        Arena.Init();
        ConfigureInputs();
        SetupFish();
        GoToTitle();
    }

    void SetupFish()
    {
        var a = Fish[0];
        a.Name = "THE CODFATHER";
        a.BodyCol = U.Col(245, 140, 50);
        a.BellyCol = U.Col(255, 215, 160);
        a.FinCol = U.Col(230, 80, 50);
        a.TeamCol = U.Col(220, 40, 60);
        a.ShoeCol = U.Col(230, 40, 50);
        a.SkinCol = U.Col(240, 190, 160);
        a.LipCol = U.Col(255, 120, 140);

        var b = Fish[1];
        b.Name = "BASS ACKWARDS";
        b.BodyCol = U.Col(70, 170, 170);
        b.BellyCol = U.Col(200, 235, 220);
        b.FinCol = U.Col(40, 110, 150);
        b.TeamCol = U.Col(40, 110, 230);
        b.ShoeCol = U.Col(40, 60, 200);
        b.SkinCol = U.Col(170, 120, 90);
        b.LipCol = U.Col(230, 110, 150);
    }

    void ConfigureInputs()
    {
        humans[0] = new HumanInput(vsCpu ? HumanInput.Merge(HumanInput.P1Keys, HumanInput.P2Keys) : HumanInput.P1Keys, 0);
        humans[1] = new HumanInput(HumanInput.P2Keys, 1);
    }

    // ------------------------------------------------------------------ event hooks used by entities

    public void Popup(Vector3 p, string text, Color c) => Fx.AddPopup(p, text, c);
    public void Shake(float amount) => shake = MathF.Min(1.2f, shake + amount);
    public void Hitstop(float t) => hitstop = MathF.Max(hitstop, t);
    public void Hype(float a) => Arena.Hype(a);

    public void Comment(string s)
    {
        if (state == State.Title) return;
        // Don't stomp a fresh goal line with small stuff.
        if (commentAge < 2.5f && state == State.Goal) return;
        comment = s;
        commentAge = 0;
    }

    public void TechUsed(Fish f, string tech)
    {
        lastTech[f.Id] = tech;
        lastTechAt[f.Id] = gameTime;
        if (state == State.Play)
            techCount[f.Id][tech] = techCount[f.Id].GetValueOrDefault(tech) + 1;
        Arena.Hype(0.15f);
    }

    // ------------------------------------------------------------------ flow

    void GoToTitle()
    {
        state = State.Title;
        stateTime = 0;
        ResetMatch();
        ResetPositions();
    }

    void ResetMatch()
    {
        score[0] = score[1] = 0;
        round = 0;
        Arena.ResetGoals();
        foreach (var f in Fish)
        {
            f.S = new Stats();
            f.Owned.Clear();
        }
        techCount[0].Clear();
        techCount[1].Clear();
    }

    void ResetPositions()
    {
        Ball.Reset();
        Fish[0].ResetForKickoff(new Vector3(-6, 0, 0));
        Fish[1].ResetForKickoff(new Vector3(6, 0, 0));
    }

    void StartMatch(bool cpu)
    {
        vsCpu = cpu;
        ConfigureInputs();
        ResetMatch();
        StartKickoff();
    }

    void StartKickoff()
    {
        ResetPositions();
        Fx.Clear();
        state = State.Kickoff;
        stateTime = 0;
        Comment(Commentary.Kickoff(Fish[0], Fish[1], round));
    }

    void OnGoal(int goalSide)
    {
        int scorer = 1 - goalSide;
        var s = Fish[scorer];
        var v = Fish[goalSide];
        bool own = Ball.LastTouch == goalSide;
        string tech = gameTime - lastTechAt[scorer] < 2.5f && !own ? lastTech[scorer] : "";

        if (state is State.Title or State.HowTo)
        {
            // Attract-mode goal: just reset quietly.
            StartAttractKickoff();
            return;
        }

        score[scorer]++;
        lastScorer = scorer;
        state = State.Goal;
        stateTime = 0;
        s.Celebrate = 2.8f;
        v.Sulk = 2.8f;
        Audio.Play(Sfx.Horn);
        Audio.Play(Sfx.Cheer);
        Audio.Play(Sfx.Whistle, 0.9f, 0.6f);
        var gp = new Vector3(Arena.GoalX(goalSide), 1.5f, 0);
        Fx.Confetti(gp, 120);
        Fx.Bubbles(Ball.Pos, 30);
        Arena.Hype(1);
        Shake(0.8f);
        commentAge = 99;
        Comment(Commentary.Goal(s, v, own, tech));
        if (own) techCount[goalSide]["owngoal"] = techCount[goalSide].GetValueOrDefault("owngoal") + 1;
    }

    public void DebugCommand(string cmd)
    {
        switch (cmd)
        {
            case "play": DebugAiP1 = true; StartMatch(true); break;
            case "goal": state = State.Play; OnGoal(1); break;
            case "draft": lastScorer = 0; StartDraft(); break;
            case "victory": score[0] = WinScore; lastScorer = 0; state = State.Goal; stateTime = 3.2f; break;
            case "howto": state = State.HowTo; break;
        }
    }

    void StartAttractKickoff()
    {
        ResetPositions();
    }

    void StartDraft()
    {
        state = State.Draft;
        stateTime = 0;
        for (int i = 0; i < 2; i++)
        {
            bool victim = i != lastScorer;
            draftOptions[i] = Upgrades.Roll(Fish[i], victim ? 4 : 3);
            draftCursor[i] = 0;
            draftReady[i] = false;
            cpuPickAt[i] = U.Rand(1.2f, 2.2f);
        }
        Audio.Play(Sfx.Select, 0.8f);
    }

    void FinishDraft()
    {
        for (int i = 0; i < 2; i++)
        {
            var u = draftOptions[i][draftCursor[i]];
            u.Apply(Fish[i]);
            Fish[i].Owned.Add(u);
        }
        round++;
        StartKickoff();
        // The kickoff line is fine, but a pick comment is funnier.
        int who = U.RandInt(0, 2);
        Comment(Commentary.Upgrade(Fish[who], Fish[who].Owned[^1]));
    }

    // ------------------------------------------------------------------ update

    public void Update(float frameDt)
    {
        frameDt = MathF.Min(frameDt, 0.05f);
        realTime += frameDt;
        stateTime += frameDt;
        commentAge += frameDt;

        // Gather input (edges are OR'd until a simulation step consumes them).
        for (int i = 0; i < 2; i++)
        {
            PlayerInput inp;
            if (state == State.Title || state == State.HowTo) inp = ais[i].Read(this, frameDt);
            else if ((vsCpu && i == 1) || (DebugAiP1 && i == 0)) inp = ais[i].Read(this, frameDt);
            else inp = humans[i].Read();
            var p = pending[i];
            inp.JumpPressed |= p.JumpPressed;
            inp.DashPressed |= p.DashPressed;
            inp.SlapPressed |= p.SlapPressed;
            inp.KickPressed |= p.KickPressed;
            inp.KickReleased |= p.KickReleased;
            pending[i] = inp;
        }

        switch (state)
        {
            case State.Title: UpdateTitle(); break;
            case State.HowTo:
                if (MenuConfirm() || Raylib.IsKeyPressed(KeyboardKey.Escape)) { state = State.Title; Audio.Play(Sfx.Blip); }
                break;
            case State.Draft: UpdateDraft(frameDt); break;
            case State.Victory:
                if (stateTime > 1.5f && (MenuConfirm() || Raylib.IsKeyPressed(KeyboardKey.Escape))) { Audio.Play(Sfx.Select); GoToTitle(); }
                break;
        }

        if (state is State.Kickoff or State.Play or State.Goal)
        {
            if (Raylib.IsKeyPressed(KeyboardKey.Escape) || Raylib.IsKeyPressed(KeyboardKey.P) ||
                (Raylib.IsGamepadAvailable(0) && Raylib.IsGamepadButtonPressed(0, GamepadButton.MiddleRight)))
            {
                paused = !paused;
                Audio.Play(Sfx.Blip);
            }
            if (paused && Raylib.IsKeyPressed(KeyboardKey.Enter)) { paused = false; GoToTitle(); return; }
        }
        if (paused) return;

        if (state == State.Kickoff && stateTime >= 2.0f)
        {
            state = State.Play;
            stateTime = 0;
            Audio.Play(Sfx.Whistle);
        }
        if (state == State.Goal)
        {
            timeScale = stateTime < 0.9f ? 0.3f : 1f;
            if (stateTime > 3.3f)
            {
                timeScale = 1;
                if (score[lastScorer] >= WinScore)
                {
                    state = State.Victory;
                    stateTime = 0;
                    Fish[lastScorer].Celebrate = 9999;
                    Fish[1 - lastScorer].Sulk = 9999;
                    Comment(Commentary.Win(Fish[lastScorer], Fish[1 - lastScorer]));
                    Audio.Play(Sfx.Cheer);
                    Audio.Play(Sfx.Horn, 1.2f);
                }
                else StartDraft();
            }
        }
        else timeScale = 1;

        // Fixed-step simulation with hitstop.
        bool simulate = state is State.Title or State.HowTo or State.Kickoff or State.Play or State.Goal or State.Victory or State.Draft;
        if (simulate)
        {
            if (hitstop > 0) hitstop -= frameDt;
            else
            {
                simAccum += frameDt * timeScale;
                int steps = 0;
                while (simAccum >= Step && steps < 10)
                {
                    SimStep(Step);
                    simAccum -= Step;
                    steps++;
                    pending[0].ClearEdges();
                    pending[1].ClearEdges();
                }
            }
        }

        if (state == State.Victory && !paused && U.Rand(0, 1) < frameDt * 2)
            Fx.Confetti(new Vector3(U.Rand(-10, 10), 6, U.Rand(-6, 6)), 20);

        UpdateCamera(frameDt);
    }

    bool MenuConfirm()
    {
        bool any = Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.Space);
        for (int g = 0; g < 4; g++)
            if (Raylib.IsGamepadAvailable(g) && (Raylib.IsGamepadButtonPressed(g, GamepadButton.RightFaceDown) || Raylib.IsGamepadButtonPressed(g, GamepadButton.MiddleRight)))
                any = true;
        return any;
    }

    int MenuVertical()
    {
        int d = 0;
        if (Raylib.IsKeyPressed(KeyboardKey.Up) || Raylib.IsKeyPressed(KeyboardKey.W)) d--;
        if (Raylib.IsKeyPressed(KeyboardKey.Down) || Raylib.IsKeyPressed(KeyboardKey.S)) d++;
        for (int g = 0; g < 4; g++)
            if (Raylib.IsGamepadAvailable(g))
            {
                if (Raylib.IsGamepadButtonPressed(g, GamepadButton.LeftFaceUp)) d--;
                if (Raylib.IsGamepadButtonPressed(g, GamepadButton.LeftFaceDown)) d++;
            }
        return d;
    }

    static readonly string[] TitleItems = { "VS ROBO-TROUT  (1 player)", "COUCH HOOLIGANS  (2 players)", "HOW TO PLAY", "QUIT" };

    void UpdateTitle()
    {
        int d = MenuVertical();
        if (d != 0)
        {
            titleSel = (titleSel + d + TitleItems.Length) % TitleItems.Length;
            Audio.Play(Sfx.Blip);
        }
        if (Raylib.IsKeyPressed(KeyboardKey.Escape)) quit = true;
        if (!MenuConfirm()) return;
        Audio.Play(Sfx.Select);
        switch (titleSel)
        {
            case 0: StartMatch(true); break;
            case 1: StartMatch(false); break;
            case 2: state = State.HowTo; break;
            case 3: quit = true; break;
        }
    }

    void UpdateDraft(float dt)
    {
        for (int i = 0; i < 2; i++)
        {
            if (draftReady[i]) continue;
            int n = draftOptions[i].Count;
            if (vsCpu && i == 1)
            {
                if (stateTime > cpuPickAt[i])
                {
                    draftCursor[i] = U.RandInt(0, n);
                    draftReady[i] = true;
                    Audio.Play(Sfx.Select, 1.2f);
                }
                continue;
            }
            var inp = pending[i];
            if (inp.MenuUp) { draftCursor[i] = (draftCursor[i] + n - 1) % n; Audio.Play(Sfx.Blip); }
            if (inp.MenuDown) { draftCursor[i] = (draftCursor[i] + 1) % n; Audio.Play(Sfx.Blip); }
            if (stateTime > 0.4f && inp.Confirm)
            {
                draftReady[i] = true;
                Audio.Play(Sfx.Select, i == 0 ? 1f : 1.2f);
            }
        }
        if (draftReady[0] && draftReady[1] && stateTime > 0.6f) FinishDraft();
    }

    void SimStep(float dt)
    {
        gameTime += dt;
        Arena.Update(dt);
        bool control = state is State.Play or State.Title or State.HowTo;
        for (int i = 0; i < 2; i++)
            Fish[i].Update(dt, control ? pending[i] : default, this);

        CollideFishes();

        bool allowGoal = state is State.Play or State.Title or State.HowTo;
        int goal = Ball.Update(dt, this, allowGoal);
        foreach (var f in Fish) Ball.CollideFish(f, this, dt);
        if (state == State.Kickoff && Ball.Pos.Y < Ball.R + 0.01f)
        {
            // Hold the ball on the centre spot until the whistle.
            Ball.Vel = new Vector3(0, Ball.Vel.Y, 0);
            Ball.Pos = new Vector3(0, Ball.Pos.Y, 0);
        }
        if (goal >= 0) OnGoal(goal);
        Fx.Update(dt);
    }

    void CollideFishes()
    {
        var a = Fish[0];
        var b = Fish[1];
        if (MathF.Abs(a.Pos.Y - b.Pos.Y) > 1.6f * MathF.Max(a.Sc, b.Sc)) return;
        var d = U.XZ(b.Pos - a.Pos);
        float dist = d.Length();
        float min = (a.Radius + b.Radius) * 0.85f;
        if (dist >= min) return;
        var n = dist > 1e-4f ? d / dist : new Vector2(1, 0);

        // Flop tackles: belly-first at speed knocks the other fish over.
        for (int k = 0; k < 2; k++)
        {
            var x = k == 0 ? a : b;
            var y = k == 0 ? b : a;
            var dir = k == 0 ? n : -n;
            bool slam = x.Diving || x.SlideTimer > 0;
            float closing = Vector2.Dot(U.XZ(x.Vel) - U.XZ(y.Vel), dir);
            if (slam && closing > 7 && y.CanAct && y.StunImmune <= 0)
            {
                var knock = U.FromXZ(dir) * 9 * x.S.DiveHitMult / y.S.Mass + Vector3.UnitY * 5;
                y.Stun(0.75f * x.S.DiveHitMult, knock, this);
                x.Vel *= 0.7f;
                Popup(y.BodyCenter + Vector3.UnitY, "FLOP TACKLE!", U.Col(255, 200, 80));
                Comment(Commentary.Tackle(x, y));
                TechUsed(x, "tackle");
                Audio.Play(Sfx.Splat, 0.8f);
                Audio.Play(Sfx.Slap, 0.6f);
                Hitstop(0.08f);
                Shake(0.35f);
                return;
            }
        }

        float wa = b.S.Mass / (a.S.Mass + b.S.Mass);
        float overlap = min - dist;
        a.Pos -= U.FromXZ(n) * overlap * wa;
        b.Pos += U.FromXZ(n) * overlap * (1 - wa);
        float vrel = Vector2.Dot(U.XZ(b.Vel) - U.XZ(a.Vel), n);
        if (vrel < 0)
        {
            a.Vel += U.FromXZ(n) * vrel * wa * 1.2f;
            b.Vel -= U.FromXZ(n) * vrel * (1 - wa) * 1.2f;
            if (vrel < -8) Audio.Play(Sfx.Bounce, 0.7f, 0.6f);
        }
    }

    void UpdateCamera(float dt)
    {
        Vector3 wantTarget, wantPos;
        switch (state)
        {
            case State.Title:
            case State.HowTo:
            {
                float a = realTime * 0.08f;
                wantTarget = new Vector3(0, 1, 0);
                wantPos = new Vector3(MathF.Sin(a) * 26, 11 + MathF.Sin(realTime * 0.3f) * 2, MathF.Cos(a) * 26);
                break;
            }
            case State.Goal:
            case State.Victory:
            {
                var who = Fish[lastScorer < 0 ? 0 : lastScorer];
                wantTarget = state == State.Goal ? Vector3.Lerp(who.Pos, Ball.Pos, 0.4f) + Vector3.UnitY : who.Pos + Vector3.UnitY * 1.5f;
                wantTarget.X = U.Clamp(wantTarget.X, -12, 12);
                float orbit = state == State.Victory ? realTime * 0.3f : 0;
                wantPos = wantTarget + new Vector3(MathF.Sin(orbit) * 9, 5.5f, MathF.Cos(orbit) * 9);
                break;
            }
            case State.Draft:
            {
                float a = realTime * 0.15f;
                wantTarget = new Vector3(0, 1.2f, 0);
                wantPos = new Vector3(MathF.Sin(a) * 14, 5, 13 + MathF.Cos(a) * 3);
                break;
            }
            default:
            {
                var focus = Ball.Pos * 0.5f + (Fish[0].Pos + Fish[1].Pos) * 0.25f;
                wantTarget = new Vector3(U.Clamp(focus.X * 0.45f, -4.5f, 4.5f), 0.5f, U.Clamp(focus.Z * 0.3f, -3, 2) - 0.5f);
                float spread = MathF.Abs(Fish[0].Pos.X - Fish[1].Pos.X) + MathF.Abs(Ball.Pos.X - wantTarget.X);
                float zoom = U.Clamp(1.0f + spread / 70f, 1.0f, 1.15f);
                wantPos = wantTarget + new Vector3(0, 18, 16) * zoom;
                break;
            }
        }
        float lambda = state is State.Play or State.Kickoff ? 4 : 2.5f;
        camTarget = U.Damp(camTarget, wantTarget, lambda, dt);
        camPos = U.Damp(camPos, wantPos, lambda, dt);
        shake = MathF.Max(0, shake - dt * 2.2f);
        var jolt = new Vector3(U.Rand(-1, 1), U.Rand(-1, 1), U.Rand(-1, 1)) * shake * shake * 0.6f;
        cam.Position = camPos + jolt;
        cam.Target = camTarget + jolt * 0.5f;
    }

    // ------------------------------------------------------------------ rendering

    public void Render()
    {
        int W = Raylib.GetScreenWidth(), H = Raylib.GetScreenHeight();
        Raylib.BeginDrawing();
        Raylib.ClearBackground(U.Col(150, 205, 245));
        Raylib.DrawRectangleGradientV(0, 0, W, H, U.Col(80, 150, 230), U.Col(190, 225, 250));

        Raylib.BeginMode3D(cam);
        Raylib.BeginShaderMode(Draw.Lit);
        Draw.SetView(cam.Position);

        float celebrate = state is State.Goal or State.Victory ? 1 : 0;
        Arena.DrawWorld(celebrate);
        Ball.Render();
        foreach (var f in Fish) f.Render(realTime);
        Fx.Render3D(realTime);

        // Transparent stuff last.
        foreach (var f in Fish)
        {
            f.DrawTransparent();
            if (f.Charging) DrawAimArrow(f);
        }
        Arena.DrawGlass();

        Raylib.EndShaderMode();
        Raylib.EndMode3D();

        Fx.Render2D(cam);

        switch (state)
        {
            case State.Title: Hud.Title(W, H, realTime, TitleItems, titleSel); break;
            case State.HowTo: Hud.HowTo(W, H); break;
            case State.Draft:
                Hud.Draft(W, H, realTime, Fish, draftOptions, draftCursor, draftReady, lastScorer, vsCpu);
                break;
            case State.Victory:
                Hud.Victory(W, H, realTime, Fish[lastScorer], Fish[1 - lastScorer], score, techCount, stateTime);
                Hud.Comment(W, H, comment, commentAge);
                break;
            default:
                Hud.Scoreboard(W, Fish, score, vsCpu);
                Hud.Upgrades(W, Fish);
                Hud.OverHead(cam, Fish, vsCpu);
                Hud.Comment(W, H, comment, commentAge);
                if (state == State.Kickoff) Hud.Countdown(W, H, stateTime, round == 0);
                if (state == State.Goal) Hud.GoalBanner(W, H, stateTime, Fish[lastScorer], Fish, score);
                if (paused) Hud.Paused(W, H);
                break;
        }

        Raylib.EndDrawing();
    }

    void DrawAimArrow(Fish f)
    {
        var fw = f.Facing;
        var side = new Vector3(-fw.Z, 0, fw.X);
        var p0 = f.Pos + fw * 1.1f * f.Sc + Vector3.UnitY * 0.06f;
        float len = 0.8f + 2.8f * f.KickCharge;
        var tip = p0 + fw * len;
        var c = U.WithAlpha(f.KickCharge >= 0.99f ? U.Col(255, 240, 120) : f.TeamCol, 0.55f);
        Draw.Tri2(p0 + side * 0.18f, p0 - side * 0.18f, tip - fw * 0.5f - side * 0.18f, c);
        Draw.Tri2(p0 + side * 0.18f, tip - fw * 0.5f - side * 0.18f, tip - fw * 0.5f + side * 0.18f, c);
        Draw.Tri2(tip - fw * 0.5f + side * 0.45f, tip - fw * 0.5f - side * 0.45f, tip, c);
    }
}
