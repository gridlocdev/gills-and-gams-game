using System.Numerics;
using Raylib_cs;

namespace FishLegs;

public enum Mode { VsCpu, Couch, TwoVTwo, Training }

public class Game
{
    enum State { Title, HowTo, Controllers, Kickoff, Play, Goal, Draft, Victory, Training }

    public const int WinScore = 5;
    const float Step = 1f / 120f;
    public static readonly string[] TeamNames = { "TEAM BATTERED", "TEAM DEEP-FRIED" };
    public static readonly Color[] TeamCols = { U.Col(220, 40, 60), U.Col(40, 110, 230) };

    public Fish[] Fish = [];
    public readonly Ball Ball = new();
    public readonly Fx Fx = new();

    State state = State.Title;
    Mode mode = Mode.VsCpu;
    float stateTime, gameTime, realTime;
    bool paused, quit;
    int round, titleSel;
    int lastScoringTeam = -1;
    Fish lastScorer;
    readonly int[] score = new int[2];

    readonly Roster roster = new();
    readonly ControllersScreen controllersScreen = new();
    HumanInput[] humans = [];         // null entry = CPU
    AiInput[] ais = [];
    PlayerInput[] pending = [];
    readonly MenuInput menu = new();
    readonly Training training = new();
    PlayerInput trainingHeld;
    List<int> connectedPads = new();
    string toast = "";
    float toastAge = 99;

    Camera3D cam;
    Vector3 camPos = new(0, 20, 30), camTarget;
    float shake, hitstop, simAccum, timeScale = 1;

    string comment = "";
    float commentAge = 99;

    string[] lastTech = [];
    float[] lastTechAt = [];
    Dictionary<string, int>[] techCount = [];

    List<Upgrade>[] draftOptions = [];
    int[] draftCursor = [];
    bool[] draftReady = [];
    float[] cpuPickAt = [];

    public bool Quit => quit;
    public bool DebugAiP1;   // let ROBO-TROUT drive player 1 too (used by the screenshot harness)

    public Game()
    {
        cam = new Camera3D { Position = camPos, Target = Vector3.Zero, Up = Vector3.UnitY, FovY = 45, Projection = CameraProjection.Perspective };
        roster.Sync();
        SetupMode(Mode.VsCpu);
        GoToTitle();
    }

    // ------------------------------------------------------------------ teams & players

    public bool TwoVTwo => mode == Mode.TwoVTwo;
    public IEnumerable<Fish> Opponents(Fish f) => Fish.Where(x => x.Team != f.Team);
    public IEnumerable<Fish> Teammates(Fish f) => Fish.Where(x => x.Team == f.Team && x != f);
    public IEnumerable<Fish> TeamOf(int team) => Fish.Where(x => x.Team == team);

    static readonly (string Name, Color Body, Color Belly, Color Fin, Color Skin, Color Lip)[] Looks =
    {
        ("THE CODFATHER", U.Col(245, 140, 50), U.Col(255, 215, 160), U.Col(230, 80, 50), U.Col(240, 190, 160), U.Col(255, 120, 140)),
        ("SOLE SURVIVOR", U.Col(190, 120, 200), U.Col(240, 215, 240), U.Col(150, 70, 170), U.Col(225, 175, 140), U.Col(255, 130, 170)),
        ("BASS ACKWARDS", U.Col(70, 170, 170), U.Col(200, 235, 220), U.Col(40, 110, 150), U.Col(170, 120, 90), U.Col(230, 110, 150)),
        ("GUPPY GILMORE", U.Col(250, 210, 70), U.Col(255, 245, 200), U.Col(240, 140, 40), U.Col(120, 85, 65), U.Col(240, 100, 120)),
    };

    Fish MakeFish(int id, int team, int look)
    {
        var l = Looks[look];
        var f = new Fish(id, team)
        {
            Name = l.Name, BodyCol = l.Body, BellyCol = l.Belly, FinCol = l.Fin, SkinCol = l.Skin, LipCol = l.Lip,
            TeamCol = TeamCols[team],
            ShoeCol = team == 0 ? U.Col(230, 40, 50) : U.Col(40, 60, 200),
        };
        return f;
    }

    // 1v1: P1 red vs P2 blue. 2v2: P1 + P2 red vs P3 + P4 blue.
    void SetupMode(Mode m)
    {
        mode = m;
        Arena.SetSize(m == Mode.TwoVTwo);
        Fish = m == Mode.TwoVTwo
            ? [MakeFish(0, 0, 0), MakeFish(1, 0, 1), MakeFish(2, 1, 2), MakeFish(3, 1, 3)]
            : [MakeFish(0, 0, 0), MakeFish(1, 1, 2)];
        int n = Fish.Length;
        ais = Fish.Select(f => new AiInput(f) { Skill = f.Team == 0 ? 0.7f : 0.75f }).ToArray();
        pending = new PlayerInput[n];
        lastTech = Enumerable.Repeat("", n).ToArray();
        lastTechAt = Enumerable.Repeat(-99f, n).ToArray();
        techCount = Enumerable.Range(0, n).Select(_ => new Dictionary<string, int>()).ToArray();
        draftOptions = Enumerable.Range(0, n).Select(_ => new List<Upgrade>()).ToArray();
        draftCursor = new int[n];
        draftReady = new bool[n];
        cpuPickAt = new float[n];
        ConfigureInputs();
    }

    void ConfigureInputs()
    {
        roster.Sync();
        humans = new HumanInput[Fish.Length];
        switch (mode)
        {
            case Mode.VsCpu or Mode.Training:
            {
                // Solo: with no controllers, both keyboard layouts drive P1 (as always).
                // With a controller connected, P1 uses only what's assigned to P1 (the controller by default).
                var pads = roster.Devices().Where(d => d.IsPad).ToList();
                humans[0] = Roster.InputFor(pads.Count == 0 ? [InputDevice.KeyboardA, InputDevice.KeyboardB]
                    : roster.HasDevices(0) ? roster.DevicesFor(0) : pads);
                break;
            }
            case Mode.Couch:
                humans[0] = Roster.InputFor(roster.HasDevices(0) ? roster.DevicesFor(0) : [InputDevice.KeyboardA]);
                humans[1] = Roster.InputFor(roster.HasDevices(1) ? roster.DevicesFor(1) : [InputDevice.KeyboardB]);
                break;
            case Mode.TwoVTwo:
                // Players with nothing assigned are played by ROBO-TROUT.
                for (int i = 0; i < Fish.Length; i++)
                    if (roster.HasDevices(i)) humans[i] = Roster.InputFor(roster.DevicesFor(i));
                break;
        }
    }

    bool IsHuman(int id) => state is not (State.Title or State.HowTo or State.Controllers)
        && id < humans.Length && humans[id] != null && !(DebugAiP1 && id == 0);

    public string Tag(int id) => IsHuman(id) ? $"P{id + 1}" : "CPU";

    void WatchPads()
    {
        var now = Pads.Connected();
        if (now.SequenceEqual(connectedPads)) return;
        var added = now.Except(connectedPads).ToList();
        var removed = connectedPads.Except(now).ToList();
        connectedPads = now;
        roster.Sync();
        ConfigureInputs();
        if (added.Count > 0 && Pads.PermissionProblem)
            toast = $"Controller found, but {MacPermissions.Why}. See HOW TO PLAY for the fix.";
        else if (added.Count > 0)
        {
            int pad = added[0];
            int p = roster.PlayerOf(InputDevice.Gamepad(pad));
            toast = $"Controller connected: {Pads.Name(pad)}  ->  {(p >= 0 ? $"P{p + 1}" : "not playing")}  (change it in CONTROLLERS)";
            Pads.Rumble(pad, 0.4f, 0.25f);
            Audio.Play(Sfx.Select, 1.3f, 0.6f);
        }
        else if (removed.Count > 0)
        {
            toast = "Controller disconnected. The fish is confused (more than usual).";
            if (state is State.Play or State.Kickoff) paused = true;
        }
        toastAge = 0;
    }

    public void Rumble(int fishId, float strength, float seconds)
    {
        if (IsHuman(fishId)) humans[fishId].Rumble(strength, seconds);
    }

    Device PlayerDevice(int id) => id < humans.Length && humans[id] != null ? humans[id].Device : menu.Device;

    // ------------------------------------------------------------------ event hooks used by entities

    public void Popup(Vector3 p, string text, Color c) => Fx.AddPopup(p, text, c);
    public void Shake(float amount) => shake = MathF.Min(1.2f, shake + amount);
    public void Hitstop(float t) => hitstop = MathF.Max(hitstop, t);
    public void Hype(float a) => Arena.Hype(a);

    public void Comment(string s)
    {
        if (state is State.Title or State.Controllers) return;
        // Don't stomp a fresh goal line with small stuff.
        if (commentAge < 2.5f && state == State.Goal) return;
        comment = s;
        commentAge = 0;
    }

    public void TechUsed(Fish f, string tech)
    {
        lastTech[f.Id] = tech;
        lastTechAt[f.Id] = gameTime;
        if (state == State.Training && f.Id == 0) training.OnTech(tech);
        if (state == State.Play)
            techCount[f.Id][tech] = techCount[f.Id].GetValueOrDefault(tech) + 1;
        Arena.Hype(0.15f);
    }

    public void DashTiming(Fish f, float sinceLand, float landImpact, bool wave)
    {
        if (state == State.Training && f.Id == 0) training.OnDashTiming(sinceLand, landImpact, wave);
    }

    // ------------------------------------------------------------------ flow

    void GoToTitle()
    {
        paused = false;
        if (mode != Mode.VsCpu) SetupMode(Mode.VsCpu);
        state = State.Title;
        stateTime = 0;
        ResetMatch();
        ResetPositions();
    }

    void ResetMatch()
    {
        score[0] = score[1] = 0;
        round = 0;
        lastScorer = null;
        lastScoringTeam = -1;
        Arena.ResetGoals();
        foreach (var f in Fish)
        {
            f.S = new Stats();
            f.Owned.Clear();
        }
        foreach (var t in techCount) t.Clear();
    }

    void ResetPositions()
    {
        Ball.Reset();
        if (Fish.Length == 4)
        {
            // One forward, one back per team, staggered so nobody starts nose-to-nose.
            Fish[0].ResetForKickoff(new Vector3(-6, 0, -3.5f));
            Fish[1].ResetForKickoff(new Vector3(-11, 0, 3.5f));
            Fish[2].ResetForKickoff(new Vector3(6, 0, 3.5f));
            Fish[3].ResetForKickoff(new Vector3(11, 0, -3.5f));
        }
        else
        {
            Fish[0].ResetForKickoff(new Vector3(-6, 0, 0));
            Fish[1].ResetForKickoff(new Vector3(6, 0, 0));
        }
    }

    void StartMatch(Mode m)
    {
        SetupMode(m);
        ResetMatch();
        StartKickoff();
    }

    void StartKickoff()
    {
        ResetPositions();
        Fx.Clear();
        paused = false;
        state = State.Kickoff;
        stateTime = 0;
        Comment(TwoVTwo ? Commentary.KickoffTeams(TeamNames[0], TeamNames[1], round) : Commentary.Kickoff(Fish[0], Fish[1], round));
    }

    void StartTraining()
    {
        SetupMode(Mode.Training);
        ResetMatch();
        ResetPositions();
        Fx.Clear();
        paused = false;
        state = State.Training;
        stateTime = 0;
        training.MenuSel = 0;
        training.BallResetAt = -1;
        ApplyDummyMode();
    }

    void ApplyDummyMode()
    {
        // "Off" parks the dummy far below the floor, out of every collision check.
        Fish[1].ResetForKickoff(training.Dummy == DummyMode.Off ? new Vector3(0, -50, 0) : Training.DummyHome);
    }

    void ResetTrainingBall()
    {
        var p = Fish[0];
        var at = p.Pos + p.Facing * 2.4f + Vector3.UnitY * 1.2f;
        at.X = U.Clamp(at.X, -Arena.HalfL + 1, Arena.HalfL - 1);
        at.Z = U.Clamp(at.Z, -Arena.HalfW + 1, Arena.HalfW - 1);
        Ball.Pos = at;
        Ball.Vel = Vector3.Zero;
        Ball.Spin = Vector3.Zero;
        training.BallResetAt = -1;
        Fx.Puff(at, 8);
        Audio.Play(Sfx.Blip, 1.3f);
    }

    void UpdateTrainingMenu()
    {
        if (!paused)
        {
            if (menu.Pause) { paused = true; training.MenuSel = 0; Audio.Play(Sfx.Blip); }
            else if (Raylib.IsKeyPressed(KeyboardKey.R) || Pads.Connected().Any(p => Pads.Pressed(p, GamepadButton.MiddleLeft)))
                ResetTrainingBall();
            return;
        }
        int n = Training.MenuItems.Length;
        if (menu.Vertical != 0) { training.MenuSel = (training.MenuSel + menu.Vertical + n) % n; Audio.Play(Sfx.Blip); }
        if (menu.Back || menu.Pause) { paused = false; Audio.Play(Sfx.Blip); return; }
        if (!menu.Confirm) return;
        Audio.Play(Sfx.Select);
        switch (training.MenuSel)
        {
            case 0: paused = false; break;
            case 1:
                training.Dummy = (DummyMode)(((int)training.Dummy + 1) % 3);
                ApplyDummyMode();
                break;
            case 2: training.NoCooldowns = !training.NoCooldowns; break;
            case 3: training.ShowInputs = !training.ShowInputs; break;
            case 4: ResetTrainingBall(); paused = false; break;
            case 5: training.ResetChecklist(); break;
            case 6: GoToTitle(); break;
        }
    }

    void OnGoal(int goalSide)
    {
        if (state == State.Training)
        {
            training.OnTech("goal");
            Popup(new Vector3(Arena.GoalX(goalSide), 3.5f, 0), "GOAL!", U.Col(255, 215, 80));
            Fx.Confetti(new Vector3(Arena.GoalX(goalSide), 1.5f, 0), 50);
            Audio.Play(Sfx.Horn, 1.3f, 0.5f);
            Rumble(0, 0.5f, 0.3f);
            training.BallResetAt = gameTime + 1.2f;
            return;
        }
        if (state is State.Title or State.HowTo or State.Controllers)
        {
            // Attract-mode goal: just reset quietly.
            ResetPositions();
            return;
        }

        int scoringTeam = 1 - goalSide;
        var toucher = Ball.LastTouch >= 0 && Ball.LastTouch < Fish.Length ? Fish[Ball.LastTouch] : null;
        bool own = toucher != null && toucher.Team == goalSide;
        var scorer = toucher != null && toucher.Team == scoringTeam ? toucher : TeamOf(scoringTeam).First();
        var victim = own ? toucher : TeamOf(goalSide).OrderBy(f => MathF.Abs(f.Pos.X - Arena.GoalX(goalSide))).First();
        string tech = !own && gameTime - lastTechAt[scorer.Id] < 2.5f ? lastTech[scorer.Id] : "";

        score[scoringTeam]++;
        lastScoringTeam = scoringTeam;
        lastScorer = scorer;
        state = State.Goal;
        stateTime = 0;
        foreach (var f in Fish)
        {
            if (f.Team == scoringTeam) { f.Celebrate = 2.8f; Rumble(f.Id, 0.5f, 0.5f); }
            else { f.Sulk = 2.8f; Rumble(f.Id, 1f, 0.7f); }
        }
        Audio.Play(Sfx.Horn);
        Audio.Play(Sfx.Cheer);
        Audio.Play(Sfx.Whistle, 0.9f, 0.6f);
        Fx.Confetti(new Vector3(Arena.GoalX(goalSide), 1.5f, 0), 120);
        Fx.Bubbles(Ball.Pos, 30);
        Arena.Hype(1);
        Shake(0.8f);
        commentAge = 99;
        Comment(Commentary.Goal(scorer, victim, own, tech));
        if (own) techCount[toucher.Id]["owngoal"] = techCount[toucher.Id].GetValueOrDefault("owngoal") + 1;
    }

    public void DebugCommand(string cmd)
    {
        switch (cmd)
        {
            case "play": DebugAiP1 = true; StartMatch(Mode.VsCpu); break;
            case "play2v2": DebugAiP1 = true; StartMatch(Mode.TwoVTwo); break;
            case "goal": state = State.Play; Ball.LastTouch = 0; OnGoal(1); break;
            case "draft": lastScoringTeam = 0; StartDraft(); break;
            case "victory": score[0] = WinScore; lastScoringTeam = 0; lastScorer = Fish[0]; state = State.Goal; stateTime = 3.2f; break;
            case "howto": state = State.HowTo; break;
            case "controllers": state = State.Controllers; stateTime = 0; break;
            case "title": GoToTitle(); break;
            case "pause": paused = !paused; break;
            case "training": StartTraining(); DebugAiP1 = true; break;
            case "trainmenu": paused = true; break;
        }
    }

    void StartDraft()
    {
        state = State.Draft;
        stateTime = 0;
        for (int i = 0; i < Fish.Length; i++)
        {
            bool victim = Fish[i].Team != lastScoringTeam;
            draftOptions[i] = Upgrades.Roll(Fish[i], victim ? 4 : 3);
            draftCursor[i] = 0;
            draftReady[i] = false;
            cpuPickAt[i] = U.Rand(1.2f, 2.4f);
        }
        Audio.Play(Sfx.Select, 0.8f);
    }

    void FinishDraft()
    {
        for (int i = 0; i < Fish.Length; i++)
        {
            var u = draftOptions[i][draftCursor[i]];
            u.Apply(Fish[i]);
            Fish[i].Owned.Add(u);
        }
        round++;
        StartKickoff();
        // The kickoff line is fine, but a pick comment is funnier.
        var who = U.Pick(Fish);
        Comment(Commentary.Upgrade(who, who.Owned[^1]));
    }

    // ------------------------------------------------------------------ update

    public void Update(float frameDt)
    {
        frameDt = MathF.Min(frameDt, 0.05f);
        realTime += frameDt;
        stateTime += frameDt;
        commentAge += frameDt;
        toastAge += frameDt;
        training.Update(frameDt);
        Pads.Update();
        menu.Update();
        WatchPads();

        // Gather input (edges are OR'd until a simulation step consumes them).
        bool attract = state is State.Title or State.HowTo or State.Controllers;
        for (int i = 0; i < Fish.Length; i++)
        {
            PlayerInput inp;
            if (attract) inp = ais[i].Read(this, frameDt);
            else if (state == State.Training && i == 1)
                inp = training.Dummy == DummyMode.Cpu ? ais[1].Read(this, frameDt) : default;
            else if (!IsHuman(i)) inp = ais[i].Read(this, frameDt);
            else inp = humans[i].Read();
            if (state == State.Training && i == 0)
            {
                trainingHeld = inp;
                if (!paused) training.RecordInput(inp, Fish[0], gameTime);
            }
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
                if (menu.Confirm || menu.Back) { state = State.Title; Audio.Play(Sfx.Blip); }
                break;
            case State.Controllers:
                if (controllersScreen.Update(roster, menu, frameDt)) { state = State.Title; ConfigureInputs(); Audio.Play(Sfx.Blip); }
                break;
            case State.Draft: UpdateDraft(); break;
            case State.Training: UpdateTrainingMenu(); break;
            case State.Victory:
                if (stateTime > 1.5f && (menu.Confirm || menu.Back)) { Audio.Play(Sfx.Select); GoToTitle(); }
                break;
        }

        if (state is State.Kickoff or State.Play or State.Goal)
        {
            if (paused)
            {
                if (menu.Quit) { GoToTitle(); return; }
                if (menu.Pause || menu.Back) { paused = false; Audio.Play(Sfx.Blip); }
            }
            else if (menu.Pause)
            {
                paused = true;
                Audio.Play(Sfx.Blip);
            }
        }
        if (paused)
        {
            // Don't let menu presses leak into gameplay (e.g. A to resume also jumping).
            for (int i = 0; i < pending.Length; i++) pending[i].ClearEdges();
            return;
        }

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
                if (score[lastScoringTeam] >= WinScore)
                {
                    state = State.Victory;
                    stateTime = 0;
                    foreach (var f in Fish)
                        if (f.Team == lastScoringTeam) f.Celebrate = 9999; else f.Sulk = 9999;
                    Comment(TwoVTwo
                        ? Commentary.TeamWin(TeamNames[lastScoringTeam], TeamNames[1 - lastScoringTeam])
                        : Commentary.Win(TeamOf(lastScoringTeam).First(), TeamOf(1 - lastScoringTeam).First()));
                    Audio.Play(Sfx.Cheer);
                    Audio.Play(Sfx.Horn, 1.2f);
                }
                else StartDraft();
            }
        }
        else timeScale = 1;

        // Fixed-step simulation with hitstop.
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
                for (int i = 0; i < pending.Length; i++) pending[i].ClearEdges();
            }
        }

        if (state == State.Victory && U.Rand(0, 1) < frameDt * 2)
            Fx.Confetti(new Vector3(U.Rand(-10, 10), 6, U.Rand(-6, 6)), 20);

        UpdateCamera(frameDt);
    }

    static readonly string[] TitleItems =
    {
        "VS ROBO-TROUT  (1 player)", "COUCH HOOLIGANS  (2 players)", "2v2 SCHOOL RUMBLE  (1-4 players)",
        "TRAINING ROOM", "CONTROLLERS", "HOW TO PLAY", "QUIT",
    };

    void UpdateTitle()
    {
        int d = menu.Vertical;
        if (d != 0)
        {
            titleSel = (titleSel + d + TitleItems.Length) % TitleItems.Length;
            Audio.Play(Sfx.Blip);
        }
        if (Raylib.IsKeyPressed(KeyboardKey.Escape)) quit = true;
        if (!menu.Confirm) return;
        Audio.Play(Sfx.Select);
        switch (titleSel)
        {
            case 0: StartMatch(Mode.VsCpu); break;
            case 1: StartMatch(Mode.Couch); break;
            case 2: StartMatch(Mode.TwoVTwo); break;
            case 3: StartTraining(); break;
            case 4: roster.Sync(); controllersScreen.Open(); state = State.Controllers; stateTime = 0; break;
            case 5: state = State.HowTo; break;
            case 6: quit = true; break;
        }
    }

    void UpdateDraft()
    {
        for (int i = 0; i < Fish.Length; i++)
        {
            if (draftReady[i]) continue;
            int n = draftOptions[i].Count;
            if (!IsHuman(i))
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
                Audio.Play(Sfx.Select, 1f + i * 0.1f);
            }
        }
        if (draftReady.All(r => r) && stateTime > 0.6f) FinishDraft();
    }

    void SimStep(float dt)
    {
        gameTime += dt;
        Arena.Update(dt);
        bool control = state is State.Play or State.Title or State.HowTo or State.Controllers or State.Training;
        bool dummyOff = state == State.Training && training.Dummy == DummyMode.Off;
        for (int i = 0; i < Fish.Length; i++)
            if (!(i == 1 && dummyOff)) Fish[i].Update(dt, control ? pending[i] : default, this);
        if (state == State.Training)
        {
            if (training.NoCooldowns) Fish[0].DashCd = Fish[0].SlapCd = 0;
            training.UpdateDummy(Fish[1], this, dt);
            if (training.BallResetAt >= 0 && gameTime >= training.BallResetAt) ResetTrainingBall();
        }

        for (int i = 0; i < Fish.Length; i++)
            for (int j = i + 1; j < Fish.Length; j++)
                CollideFishes(Fish[i], Fish[j]);

        bool allowGoal = state is State.Play or State.Title or State.HowTo or State.Controllers || (state == State.Training && training.BallResetAt < 0);
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

    void CollideFishes(Fish a, Fish b)
    {
        if (MathF.Abs(a.Pos.Y - b.Pos.Y) > 1.6f * MathF.Max(a.Sc, b.Sc)) return;
        var d = U.XZ(b.Pos - a.Pos);
        float dist = d.Length();
        float min = (a.Radius + b.Radius) * 0.85f;
        if (dist >= min) return;
        var n = dist > 1e-4f ? d / dist : new Vector2(1, 0);

        // Flop tackles: belly-first at speed knocks an opponent over. Teammates just bump.
        for (int k = 0; k < 2 && a.Team != b.Team; k++)
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
                Rumble(x.Id, 0.5f, 0.15f);
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
        float scale = Arena.HalfL / 15f;   // frame the bigger 2v2 pitch from further back
        switch (state)
        {
            case State.Title:
            case State.HowTo:
            case State.Controllers:
            {
                float a = realTime * 0.08f;
                wantTarget = new Vector3(0, 1, 0);
                wantPos = new Vector3(MathF.Sin(a) * 26, 11 + MathF.Sin(realTime * 0.3f) * 2, MathF.Cos(a) * 26) * scale;
                break;
            }
            case State.Goal:
            case State.Victory:
            {
                var who = lastScorer ?? Fish[0];
                wantTarget = state == State.Goal ? Vector3.Lerp(who.Pos, Ball.Pos, 0.4f) + Vector3.UnitY : who.Pos + Vector3.UnitY * 1.5f;
                wantTarget.X = U.Clamp(wantTarget.X, -Arena.HalfL + 3, Arena.HalfL - 3);
                float orbit = state == State.Victory ? realTime * 0.3f : 0;
                wantPos = wantTarget + new Vector3(MathF.Sin(orbit) * 9, 5.5f, MathF.Cos(orbit) * 9);
                break;
            }
            case State.Draft:
            {
                float a = realTime * 0.15f;
                wantTarget = new Vector3(0, 1.2f, 0);
                wantPos = new Vector3(MathF.Sin(a) * 14, 5, 13 + MathF.Cos(a) * 3) * scale;
                break;
            }
            default:
            {
                var fishAvg = Fish.Aggregate(Vector3.Zero, (acc, f) => acc + f.Pos) / Fish.Length;
                var focus = Ball.Pos * 0.5f + fishAvg * 0.5f;
                wantTarget = new Vector3(U.Clamp(focus.X * 0.45f, -4.5f * scale, 4.5f * scale), 0.5f, U.Clamp(focus.Z * 0.3f, -3, 2) - 0.5f);
                float minX = Fish.Min(f => f.Pos.X), maxX = Fish.Max(f => f.Pos.X);
                float spread = (maxX - minX) + MathF.Abs(Ball.Pos.X - wantTarget.X);
                float zoom = U.Clamp(1.0f + spread / (70f * scale), 1.0f, 1.15f);
                wantPos = wantTarget + new Vector3(0, 18, 16) * zoom * scale;
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

    // Scoreboard labels: fish names in 1v1, team names (with who's playing) in 2v2.
    (string name, string sub)[] TeamLabels() => TwoVTwo
        ? [.. Enumerable.Range(0, 2).Select(t => (TeamNames[t], string.Join(" + ", TeamOf(t).Select(f => Tag(f.Id)))))]
        : [(Fish[0].Name, Tag(0)), (Fish[1].Name, mode == Mode.VsCpu ? "ROBO-TROUT (CPU)" : Tag(1))];

    public void Render()
    {
        int W = Raylib.GetScreenWidth(), H = Raylib.GetScreenHeight();
        Raylib.BeginDrawing();
        Raylib.ClearBackground(U.Col(150, 205, 245));
        bool room = state == State.Training;
        if (room) Raylib.DrawRectangleGradientV(0, 0, W, H, U.Col(170, 200, 228), U.Col(210, 228, 242));
        else Raylib.DrawRectangleGradientV(0, 0, W, H, U.Col(80, 150, 230), U.Col(190, 225, 250));

        Raylib.BeginMode3D(cam);
        Raylib.BeginShaderMode(Draw.Lit);
        Draw.SetView(cam.Position);

        float celebrate = state is State.Goal or State.Victory ? 1 : 0;
        if (room) Arena.DrawTrainingRoom(); else Arena.DrawWorld(celebrate);
        Ball.Render();
        foreach (var f in Fish)
            if (!(room && f.Id == 1 && training.Dummy == DummyMode.Off)) f.Render(realTime);
        Fx.Render3D(realTime);

        // Transparent stuff last.
        foreach (var f in Fish)
        {
            f.DrawTransparent();
            if (f.Charging) DrawAimArrow(f);
        }
        if (room) Arena.DrawTrainingGlass(); else Arena.DrawGlass();

        Raylib.EndShaderMode();
        Raylib.EndMode3D();

        if (state is not (State.Controllers or State.HowTo)) Fx.Render2D(cam);

        var tags = Fish.Select(f => Tag(f.Id)).ToArray();
        switch (state)
        {
            case State.Title:
                Hud.Title(W, H, realTime, TitleItems, titleSel, menu.Device);
                if (Pads.PermissionProblem) Hud.PermissionWarning(W, 250);
                break;
            case State.HowTo:
                Hud.HowTo(W, H, menu.Device, connectedPads);
                if (Pads.PermissionProblem) Hud.PermissionWarning(W, H - 150);
                break;
            case State.Controllers:
                controllersScreen.Render(W, H, realTime, roster, menu.Device);
                break;
            case State.Draft:
                Hud.Draft(W, H, realTime, Fish, tags, draftOptions, draftCursor, draftReady, lastScoringTeam,
                    Fish.Select(f => IsHuman(f.Id)).ToArray(), Fish.Select(f => PlayerDevice(f.Id)).ToArray());
                break;
            case State.Training:
                training.Render(W, H, realTime, humans[0].Device, trainingHeld, Fish[0], Fish[1], cam);
                if (paused) training.RenderMenu(W, H, realTime, menu.Device);
                break;
            case State.Victory:
                Hud.Victory(W, H, realTime, Fish, lastScoringTeam, TwoVTwo ? TeamNames[lastScoringTeam] : TeamOf(lastScoringTeam).First().Name,
                    score, techCount, stateTime, menu.Device);
                Hud.Comment(W, H, comment, commentAge);
                break;
            default:
                Hud.Scoreboard(W, TeamLabels(), TeamCols, score);
                Hud.Upgrades(W, Fish, TwoVTwo);
                Hud.OverHead(cam, Fish, tags);
                Hud.Comment(W, H, comment, commentAge);
                if (state == State.Kickoff)
                    Hud.Countdown(W, H, stateTime, round == 0,
                        Fish.Where(f => IsHuman(f.Id)).Select(f => ($"P{f.Id + 1}", PlayerDevice(f.Id))).ToArray());
                if (state == State.Goal)
                    Hud.GoalBanner(W, H, stateTime, lastScorer, TwoVTwo ? TeamNames[lastScoringTeam] : lastScorer.Name, TeamLabels(), score);
                if (paused) Hud.Paused(W, H, menu.Device);
                break;
        }

        Hud.Toast(W, H, toast, toastAge);
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
