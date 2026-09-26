using Raylib_cs;

namespace FishLegs;

public class Stats
{
    public float MoveSpeed = 8.5f;
    public float JumpVel = 10.5f;
    public int AirJumps = 1;
    public float DashSpeed = 17f;
    public float DashCooldown = 1.1f;
    public float KickPower = 1f;
    public float ChargeTime = 0.6f;
    public float AirKickMult = 1f;
    public float SlapRadius = 2.3f;
    public float SlapStun = 0.8f;
    public float SlapCooldown = 1.6f;
    public float Scale = 1f;
    public float Mass = 1f;
    public float SlideMult = 1f;
    public float DiveHitMult = 1f;
    public float HairDensity = 1f;
    public float LegLength = 1f;
    public float Thighs = 1f;
    public bool Curve, StickyToes, Stinky, WallRunner, HomeWaters;
}

public class Upgrade
{
    public string Name = "";
    public string Desc = "";
    public string Flavor = "";
    public Color Color;
    public bool Unique;
    public Action<Fish> Apply = _ => { };
}

static class Upgrades
{
    public static readonly List<Upgrade> All = new()
    {
        new() { Name = "Calf Implants", Desc = "+12% run speed", Flavor = "Surgically enhanced. Legally grey.",
            Color = U.Col(250, 150, 90), Apply = f => f.S.MoveSpeed *= 1.12f },
        new() { Name = "Extra Leg Hair", Desc = "+8% run speed, much hairier legs", Flavor = "Aerodynamic. Allegedly.",
            Color = U.Col(150, 100, 60), Apply = f => { f.S.MoveSpeed *= 1.08f; f.S.HairDensity += 1.2f; } },
        new() { Name = "Thunder Thighs", Desc = "+22% kick power, visibly thicc", Flavor = "The thighs have it.",
            Color = U.Col(230, 80, 80), Apply = f => { f.S.KickPower *= 1.22f; f.S.Thighs += 0.25f; } },
        new() { Name = "Spring-Loaded Shins", Desc = "+18% jump height", Flavor = "Boing is a lifestyle.",
            Color = U.Col(120, 220, 120), Apply = f => f.S.JumpVel *= 1.09f },
        new() { Name = "Bonus Knee", Desc = "+1 mid-air jump", Flavor = "Nobody asked where it goes.",
            Color = U.Col(200, 160, 255), Apply = f => f.S.AirJumps += 1 },
        new() { Name = "Espresso Gills", Desc = "Dash cooldown -30%", Flavor = "Caffeine, absorbed directly. Vibrating.",
            Color = U.Col(140, 90, 50), Apply = f => f.S.DashCooldown *= 0.7f },
        new() { Name = "Mucus Coat", Desc = "Belly slides go further, Flop Hops launch harder", Flavor = "Slimy is a compliment.",
            Color = U.Col(170, 230, 120), Apply = f => f.S.SlideMult += 0.45f },
        new() { Name = "Big Boned", Desc = "+15% size, harder to knock around", Flavor = "It's the bones. Definitely the bones.",
            Color = U.Col(240, 200, 150), Apply = f => { f.S.Scale *= 1.15f; f.S.Mass *= 1.4f; } },
        new() { Name = "Sticky Toes", Desc = "Ball clings to your feet while dribbling", Flavor = "Toes of a gecko. Face of a cod.",
            Color = U.Col(255, 220, 80), Unique = true, Apply = f => f.S.StickyToes = true },
        new() { Name = "Banana Fins", Desc = "Kicks curve toward your held sideways input", Flavor = "Bend it like a goldfish.",
            Color = U.Col(255, 240, 90), Unique = true, Apply = f => f.S.Curve = true },
        new() { Name = "Slap Deluxe", Desc = "Tail Slap: +30% range, +50% stun, faster cooldown", Flavor = "Premium tail. Leather interior.",
            Color = U.Col(255, 120, 200), Apply = f => { f.S.SlapRadius *= 1.3f; f.S.SlapStun *= 1.5f; f.S.SlapCooldown *= 0.8f; } },
        new() { Name = "Parkour Training", Desc = "Wall Kicks go higher and refresh your dash", Flavor = "Will not stop talking about it.",
            Color = U.Col(90, 200, 255), Unique = true, Apply = f => f.S.WallRunner = true },
        new() { Name = "Fish Oil", Desc = "Charge kicks 40% faster", Flavor = "Rich in omega-3 and violence.",
            Color = U.Col(255, 190, 60), Apply = f => f.S.ChargeTime *= 0.6f },
        new() { Name = "Scissor Shins", Desc = "Mid-air kicks +45% power", Flavor = "Bicycle kick? Tricycle kick.",
            Color = U.Col(100, 240, 220), Apply = f => f.S.AirKickMult *= 1.45f },
        new() { Name = "Unwashed Socks", Desc = "Opponents near you move 15% slower", Flavor = "The smell has a hitbox.",
            Color = U.Col(150, 190, 60), Unique = true, Apply = f => f.S.Stinky = true },
        new() { Name = "Seaweed Curtain", Desc = "Your goal shrinks 12%", Flavor = "Totally regulation. Don't check.",
            Color = U.Col(60, 160, 90), Apply = f => Arena.GoalHalfW[f.Id] *= 0.88f },
        new() { Name = "Cannonball Belly", Desc = "Dives & slides hit the ball and fish 40% harder", Flavor = "Maximum splash damage.",
            Color = U.Col(90, 120, 230), Apply = f => f.S.DiveHitMult *= 1.4f },
        new() { Name = "Stilts (Organic)", Desc = "Legs 18% longer: taller, bigger strides", Flavor = "Now 60% leg by volume.",
            Color = U.Col(255, 170, 150), Apply = f => { f.S.LegLength *= 1.18f; f.S.MoveSpeed *= 1.05f; f.S.JumpVel *= 1.04f; } },
        new() { Name = "Home Waters", Desc = "+25% speed in your own half", Flavor = "Territorial, like a pufferfish in a bathtub.",
            Color = U.Col(80, 170, 240), Unique = true, Apply = f => f.S.HomeWaters = true },
    };

    public static List<Upgrade> Roll(Fish f, int count)
    {
        var pool = All.Where(u => !(u.Unique && f.Owned.Contains(u))).OrderBy(_ => U.Rng.Next()).ToList();
        return pool.Take(count).ToList();
    }
}
