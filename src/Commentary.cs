namespace FishLegs;

// Our commentator is Barry Barracuda. He has been doing this for 31 years and has not once been paid.
static class Commentary
{
    static string P(params string[] lines) => U.Pick(lines);

    public static string Kickoff(Fish a, Fish b, int round) => round == 0
        ? P($"Welcome to Low Tide Stadium! {a.Name} versus {b.Name}. Both teams: one fish.",
            "Remember folks: legs are a privilege, not a right.",
            "The pitch has been freshly hosed. The players have also been freshly hosed.",
            $"{b.Name} skipped leg day once. Once. Look at those calves now.")
        : P("And we're back after that brief locker-room mutation session.",
            "Both players have been given upgrades by our sponsor, Dr. Gerald's Discount Genetics.",
            "The referee reminds both players that biting is technically allowed.",
            "Somebody in the crowd has brought a tartar sauce sign. Tasteless.",
            "Hydration break over. Well. 'Hydration.' They just lay in a bucket.");

    public static string Goal(Fish scorer, Fish victim, bool ownGoal, string tech) => ownGoal
        ? P($"OWN GOAL! {victim.Name} has scored for the other fish. Humiliating. Hilarious.",
            $"{victim.Name} has put it in their own net. It IS a fishing net, to be fair. Instincts.",
            $"Oh no. Oh no no no. {victim.Name}, you absolute sardine.")
        : tech switch
        {
            "flopshot" => P($"A BELLY-FIRST GOAL! {scorer.Name} used their whole torso! Their WHOLE TORSO!",
                            "Scored with the belly. The belly! Physios everywhere are weeping."),
            "airkick" => P($"SCISSOR SHINS! {scorer.Name} goes airborne and the keeper just... gasps. Literally, no gills out here.",
                           "An overhead kick from a creature with no neck. Unbelievable scenes."),
            "wavedash" => P($"Did you see that wavedash?! {scorer.Name} has been practising in the parking lot."),
            "flophop" => P($"FLOP HOP into a GOAL! {scorer.Name} is slippery and proud of it!"),
            _ => P($"GOAAAL! {scorer.Name} finds the net! Their family would be so proud, if they hadn't been eaten.",
                   $"{scorer.Name} scores! {victim.Name} stares into the middle distance. They always do. No eyelids.",
                   $"What a strike! Those hairs on {scorer.Name}'s shins were standing straight up!",
                   $"GOAL! {victim.Name} looks like they've been left on the dock too long.",
                   $"{scorer.Name} SCORES! Calves of steel. Brain of a fish. Perfect combination."),
        };

    public static string Shinned(Fish a, Fish b) =>
        P($"{a.Name} kicks {b.Name} right in the shins! That's not football, that's just assault!",
          $"Ooh, straight to the shin. {b.Name} didn't even know they HAD shins until today.",
          "A shin-kick! The referee is looking the other way. The referee is a clam.");

    public static string Slapped(Fish a, Fish b) =>
        P($"TAIL SLAP! {b.Name} has been absolutely filleted!",
          $"{a.Name} spins and SLAPS! You could hear that in the car park!",
          $"The slap heard 'round the reef! {b.Name} is seeing little starfish.",
          "Tail to the face. A classic. A timeless art form.");

    public static string Tackle(Fish a, Fish b) =>
        P($"FLOP TACKLE! {a.Name} slides in belly-first and {b.Name} goes flying!",
          $"That's a sliding tackle in the most literal, most slimy sense.",
          $"{b.Name} just got bowled over by a wet torpedo with calves.");

    public static string Post() =>
        P("OFF THE POST! DOINK! The loveliest sound in football.",
          "The woodwork says no! Well, the aluminiumwork.",
          "Crossbar! Inches away! Well, fish-inches. Very small inches.");

    public static string Upgrade(Fish f, Upgrade u) =>
        P($"{f.Name} has chosen {u.Name}. {u.Flavor}",
          $"{f.Name} goes for {u.Name}. Bold. Stupid? Also possible.",
          $"Interesting pick from {f.Name}: {u.Name}. The medical staff have left the building.");

    public static string Win(Fish w, Fish l) =>
        P($"{w.Name} WINS! {l.Name} will be served with a light lemon butter.",
          $"It's over! {w.Name} is champion of the tide! {l.Name} returns to the sea in disgrace. And by 'sea' I mean the gift shop aquarium.",
          $"{w.Name} takes the trophy! It's a golden sneaker. They have two feet. The trophy committee did not think this through.");
}
