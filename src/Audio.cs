using System.Runtime.InteropServices;
using Raylib_cs;

namespace FishLegs;

public enum Sfx
{
    Kick, BigKick, Boing, Whoosh, Splat, Slap, Bounce, Post, Whistle, Horn, Cheer, Blip, Select, Squeak, Blub, Wavedash
}

// Every sound in the game is synthesised at startup. No assets, no licensing, no dignity.
static class Audio
{
    const int Rate = 44100;
    const int Voices = 4;
    static readonly Dictionary<Sfx, Sound[]> sounds = new();
    static readonly Dictionary<Sfx, int> next = new();
    static bool ready;

    public static void Init()
    {
        Raylib.InitAudioDevice();
        ready = Raylib.IsAudioDeviceReady();
        if (!ready) return;

        Add(Sfx.Kick, Synth(0.16f, (t, n) =>
        {
            return (MathF.Sin(Phase(t, 160, 18, 45)) * 0.9f + n * 0.4f * MathF.Exp(-t * 60)) * Env(t, 0.16f, 0.002f);
        }));
        Add(Sfx.BigKick, Synth(0.35f, (t, n) =>
        {
            float s = MathF.Sin(Phase(t, 220, 12, 40));
            s = MathF.Tanh(s * 3f) * 0.8f + n * 0.5f * MathF.Exp(-t * 30);
            return s * Env(t, 0.35f, 0.002f);
        }));
        Add(Sfx.Boing, Synth(0.28f, (t, n) =>
        {
            float f = 260 + 520 * (1 - MathF.Exp(-t * 14)) + 40 * MathF.Sin(t * 70);
            return MathF.Sin(MathF.Tau * f * t) * 0.5f * Env(t, 0.28f, 0.005f);
        }));
        float lp = 0;
        Add(Sfx.Whoosh, Synth(0.3f, (t, n) =>
        {
            float cut = 0.05f + 0.35f * MathF.Sin(MathF.PI * t / 0.3f);
            lp += (n - lp) * cut;
            return lp * 0.9f * MathF.Sin(MathF.PI * t / 0.3f);
        }));
        float lp2 = 0;
        Add(Sfx.Splat, Synth(0.4f, (t, n) =>
        {
            lp2 += (n - lp2) * 0.18f;
            float thump = MathF.Sin(Phase(t, 120, 25, 40)) * MathF.Exp(-t * 18);
            float wet = lp2 * MathF.Exp(-t * 9) * (0.6f + 0.4f * MathF.Sin(t * 190));
            return (thump * 0.8f + wet * 1.1f) * Env(t, 0.4f, 0.001f);
        }));
        Add(Sfx.Slap, Synth(0.14f, (t, n) =>
        {
            return (n * MathF.Exp(-t * 45) + MathF.Sin(MathF.Tau * 1800 * t) * 0.25f * MathF.Exp(-t * 80)) * 0.9f;
        }));
        Add(Sfx.Bounce, Synth(0.1f, (t, n) => MathF.Sin(Phase(t, 180, 30, 70)) * MathF.Exp(-t * 35) * 0.6f));
        Add(Sfx.Post, Synth(0.7f, (t, n) =>
        {
            float s = MathF.Sin(MathF.Tau * 880 * t) + 0.6f * MathF.Sin(MathF.Tau * 1397 * t) + 0.3f * MathF.Sin(MathF.Tau * 2211 * t);
            return s * 0.3f * MathF.Exp(-t * 6) * Env(t, 0.7f, 0.001f);
        }));
        Add(Sfx.Whistle, Synth(0.65f, (t, n) =>
        {
            float trill = 0.6f + 0.4f * MathF.Sin(MathF.Tau * 32 * t);
            float f = 2900 + 60 * MathF.Sin(MathF.Tau * 32 * t);
            return (MathF.Sin(MathF.Tau * f * t) * trill + n * 0.05f) * 0.35f * Env(t, 0.65f, 0.02f);
        }));
        Add(Sfx.Horn, Synth(1.3f, (t, n) =>
        {
            float s = Saw(t * 174.6f) + Saw(t * 220f) * 0.8f + Saw(t * 261.6f) * 0.6f + Saw(t * 87.3f) * 0.8f;
            return MathF.Tanh(s * 0.8f) * 0.35f * Env(t, 1.3f, 0.05f);
        }));
        float cl = 0, cl2 = 0;
        Add(Sfx.Cheer, Synth(2.4f, (t, n) =>
        {
            cl += (n - cl) * 0.25f;
            cl2 += (cl - cl2) * 0.25f;
            float band = cl - cl2;
            float swell = MathF.Min(1, t * 3) * MathF.Min(1, (2.4f - t) * 1.2f);
            float chatter = 0.7f + 0.3f * MathF.Sin(t * 23 + MathF.Sin(t * 7) * 3);
            return band * 3.2f * swell * chatter;
        }));
        Add(Sfx.Blip, Synth(0.06f, (t, n) => Square(t * 880) * 0.18f * Env(t, 0.06f, 0.002f)));
        Add(Sfx.Select, Synth(0.25f, (t, n) =>
        {
            float f = t < 0.08f ? 660 : t < 0.16f ? 880 : 1320;
            return Square(t * f) * 0.16f * Env(t, 0.25f, 0.002f);
        }));
        Add(Sfx.Squeak, Synth(0.13f, (t, n) =>
        {
            float f = 1500 + 900 * t / 0.13f + 120 * MathF.Sin(t * 400);
            return MathF.Sin(MathF.Tau * f * t) * 0.25f * Env(t, 0.13f, 0.005f);
        }));
        Add(Sfx.Blub, Synth(0.35f, (t, n) =>
        {
            float bubble = t % 0.09f;
            float f = 500 + 1400 * bubble / 0.09f;
            return MathF.Sin(MathF.Tau * f * bubble) * MathF.Exp(-bubble * 30) * 0.45f * Env(t, 0.35f, 0.002f);
        }));
        Add(Sfx.Wavedash, Synth(0.25f, (t, n) =>
        {
            float f = 700 + 1600 * t / 0.25f;
            return (Square(t * f) * 0.1f + n * 0.25f * MathF.Exp(-t * 12)) * Env(t, 0.25f, 0.003f);
        }));
    }

    public static void Play(Sfx s, float pitch = 1f, float volume = 1f)
    {
        if (!ready || !sounds.TryGetValue(s, out var voices)) return;
        int i = next[s];
        next[s] = (i + 1) % voices.Length;
        Raylib.SetSoundPitch(voices[i], pitch);
        Raylib.SetSoundVolume(voices[i], volume);
        Raylib.PlaySound(voices[i]);
    }

    public static void PlayVaried(Sfx s, float spread = 0.12f, float volume = 1f) =>
        Play(s, 1f + U.Rand(-spread, spread), volume);

    public static void Shutdown()
    {
        if (!ready) return;
        foreach (var v in sounds.Values)
        {
            for (int i = 1; i < v.Length; i++) Raylib.UnloadSoundAlias(v[i]);
            Raylib.UnloadSound(v[0]);
        }
        Raylib.CloseAudioDevice();
    }

    // Phase of a pitch sweep f(t) = floor + sweep*exp(-decay*t), integrated so it doesn't click.
    static float Phase(float t, float sweep, float decay, float floor) =>
        MathF.Tau * (floor * t + sweep * (1 - MathF.Exp(-decay * t)) / decay);

    static float Saw(float p) => 2 * (p - MathF.Floor(p + 0.5f));
    static float Square(float p) => (p - MathF.Floor(p)) < 0.5f ? 1 : -1;

    static float Env(float t, float len, float attack)
    {
        float a = MathF.Min(1, t / attack);
        float r = MathF.Min(1, (len - t) / (len * 0.25f));
        return a * MathF.Max(0, r);
    }

    static short[] Synth(float seconds, Func<float, float, float> fn)
    {
        int n = (int)(seconds * Rate);
        var data = new short[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float noise = (float)(U.Rng.NextDouble() * 2 - 1);
            float v = U.Clamp(fn(t, noise), -1, 1);
            data[i] = (short)(v * 32000);
        }
        return data;
    }

    static unsafe void Add(Sfx id, short[] samples)
    {
        IntPtr mem = Marshal.AllocHGlobal(samples.Length * sizeof(short));
        Marshal.Copy(samples, 0, mem, samples.Length);
        var wave = new Wave
        {
            FrameCount = (uint)samples.Length,
            SampleRate = Rate,
            SampleSize = 16,
            Channels = 1,
            Data = (void*)mem,
        };
        var baseSound = Raylib.LoadSoundFromWave(wave);
        Marshal.FreeHGlobal(mem);

        var voices = new Sound[Voices];
        voices[0] = baseSound;
        for (int i = 1; i < Voices; i++) voices[i] = Raylib.LoadSoundAlias(baseSound);
        sounds[id] = voices;
        next[id] = 0;
    }
}
