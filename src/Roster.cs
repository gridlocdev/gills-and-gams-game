namespace FishLegs;

public enum DeviceKind { KeyboardA, KeyboardB, Pad }

// One physical input: a keyboard layout (WASD or arrows) or a controller slot.
public readonly record struct InputDevice(DeviceKind Kind, int Pad)
{
    public static readonly InputDevice KeyboardA = new(DeviceKind.KeyboardA, -1);
    public static readonly InputDevice KeyboardB = new(DeviceKind.KeyboardB, -1);
    public static InputDevice Gamepad(int pad) => new(DeviceKind.Pad, pad);

    public bool IsPad => Kind == DeviceKind.Pad;

    public string Label => Kind switch
    {
        DeviceKind.KeyboardA => "Keyboard (WASD)",
        DeviceKind.KeyboardB => "Keyboard (arrows)",
        _ => Pads.Name(Pad),
    };
}

// Which device belongs to which player slot (P1-P4), console style.
// Defaults: with no controllers, WASD -> P1. With any controller connected, controllers only:
// the first is P1, the next P2, and so on; keyboards start unassigned (add them in CONTROLLERS).
class Roster
{
    public const int Players = 4;
    public const int None = -1;

    readonly Dictionary<InputDevice, int> assigned = new();
    bool customized;   // once the player moves something, stop re-applying defaults on hot-plug

    public List<InputDevice> Devices()
    {
        var list = new List<InputDevice> { InputDevice.KeyboardA, InputDevice.KeyboardB };
        list.AddRange(Pads.Connected().Select(InputDevice.Gamepad));
        return list;
    }

    /// <summary>Tracks plugged/unplugged devices. Defaults are recomputed until the player customises.</summary>
    public void Sync()
    {
        var current = Devices();
        bool changed = current.Count != assigned.Count || current.Any(d => !assigned.ContainsKey(d));
        if (!changed) return;
        if (!customized)
        {
            ApplyDefaults(current);
            return;
        }
        foreach (var gone in assigned.Keys.Where(d => !current.Contains(d)).ToList()) assigned.Remove(gone);
        foreach (var d in current.Where(d => !assigned.ContainsKey(d)))
            assigned[d] = d.IsPad ? FirstPlayerWithoutPad() : None;
    }

    void ApplyDefaults(List<InputDevice> current)
    {
        assigned.Clear();
        var pads = current.Where(d => d.IsPad).ToList();
        foreach (var d in current)
            assigned[d] = d.IsPad
                ? (pads.IndexOf(d) < Players ? pads.IndexOf(d) : None)
                : d.Kind == DeviceKind.KeyboardA && pads.Count == 0 ? 0 : None;
    }

    int FirstPlayerWithoutPad()
    {
        for (int p = 0; p < Players; p++)
            if (!assigned.Any(kv => kv.Key.IsPad && kv.Value == p)) return p;
        return None;
    }

    public void ResetDefaults()
    {
        customized = false;
        ApplyDefaults(Devices());
    }

    public int PlayerOf(InputDevice d) => assigned.GetValueOrDefault(d, None);

    /// <summary>Moves a device one slot left/right through: not playing, P1, P2, P3, P4.</summary>
    public void Move(InputDevice d, int dir)
    {
        int slot = PlayerOf(d) + 1;                     // 0 = not playing
        slot = (slot + dir + Players + 1) % (Players + 1);
        assigned[d] = slot - 1;
        customized = true;
    }

    public List<InputDevice> DevicesFor(int player) => assigned.Where(kv => kv.Value == player).Select(kv => kv.Key)
        .OrderBy(d => d.Kind).ThenBy(d => d.Pad).ToList();

    public bool HasDevices(int player) => assigned.Any(kv => kv.Value == player);

    /// <summary>Builds the input reader for a player from their assigned devices.</summary>
    public static HumanInput InputFor(IReadOnlyCollection<InputDevice> devices)
    {
        bool a = devices.Contains(InputDevice.KeyboardA), b = devices.Contains(InputDevice.KeyboardB);
        var keys = a && b ? HumanInput.Merge(HumanInput.P1Keys, HumanInput.P2Keys) : a ? HumanInput.P1Keys : b ? HumanInput.P2Keys : new KeyMap();
        int keySet = a ? Device.KeysP1 : b ? Device.KeysP2 : Device.KeysMenu;
        return new HumanInput(keys, keySet) { AssignedPads = devices.Where(d => d.IsPad).Select(d => d.Pad).ToArray() };
    }
}
