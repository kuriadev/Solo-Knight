using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoloKnight
{
    public enum Sfx { Swing, Hit, Crit, PlayerHurt, Dash, Whirlwind, Pickup, Coin, LevelUp, Chest, BossRoar, Slam, Death, Portal, UiClick, Potion, Mission }

    /// <summary>
    /// Plays procedurally synthesized sound effects so the prototype has audio feedback
    /// without any imported audio assets. Swap clips in later by assigning overrides.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Range(0f, 1f)] public float masterVolume = 0.55f;
        [Tooltip("Optional hand-made clips that replace the synthesized ones (index = Sfx enum value).")]
        public AudioClip[] overrideClips;

        const int SampleRate = 44100;
        const int Voices = 6;
        readonly Dictionary<Sfx, AudioClip> clips = new Dictionary<Sfx, AudioClip>();
        AudioSource[] sources;
        int nextVoice;
        static System.Random rng = new System.Random(1234);

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            sources = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                sources[i] = gameObject.AddComponent<AudioSource>();
                sources[i].playOnAwake = false;
                sources[i].spatialBlend = 0f;
            }
            BuildClips();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static void Play(Sfx sfx, float volume = 1f, float pitchVariance = 0.06f)
        {
            if (Instance == null) return;
            Instance.PlayInternal(sfx, volume, pitchVariance);
        }

        void PlayInternal(Sfx sfx, float volume, float pitchVariance)
        {
            AudioClip clip = null;
            int idx = (int)sfx;
            if (overrideClips != null && idx < overrideClips.Length && overrideClips[idx] != null) clip = overrideClips[idx];
            if (clip == null) clips.TryGetValue(sfx, out clip);
            if (clip == null) return;
            var src = sources[nextVoice];
            nextVoice = (nextVoice + 1) % sources.Length;
            src.pitch = 1f + UnityEngine.Random.Range(-pitchVariance, pitchVariance);
            src.PlayOneShot(clip, volume * masterVolume);
        }

        // ---------------- Synthesis ----------------

        static float Noise() => (float)(rng.NextDouble() * 2.0 - 1.0);
        static float Env(float t, float dur, float attack = 0.005f) => t < attack ? t / attack : Mathf.Clamp01(1f - (t - attack) / Mathf.Max(0.0001f, dur - attack));
        static float Square(float phase) => Mathf.Repeat(phase, 1f) < 0.5f ? 1f : -1f;
        static float Saw(float phase) => Mathf.Repeat(phase, 1f) * 2f - 1f;

        static AudioClip Make(string name, float duration, Func<float, float, float> sampler)
        {
            int n = Mathf.CeilToInt(duration * SampleRate);
            var data = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                // sampler(t, phase) returns sample; phase accumulates at 1Hz so callers multiply by freq
                data[i] = Mathf.Clamp(sampler(t, phase), -1f, 1f);
                phase += 1f / SampleRate;
            }
            var clip = AudioClip.Create(name, n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // Frequency sweep helper: integrates the phase so sweeps sound smooth.
        static AudioClip Sweep(string name, float dur, float f0, float f1, Func<float, float> wave, float noiseMix, float gain)
        {
            int n = Mathf.CeilToInt(dur * SampleRate);
            var data = new float[n];
            float ph = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float f = Mathf.Lerp(f0, f1, t / dur);
                ph += f / SampleRate;
                float s = wave(ph) * (1f - noiseMix) + Noise() * noiseMix;
                data[i] = Mathf.Clamp(s * Env(t, dur) * gain, -1f, 1f);
            }
            var clip = AudioClip.Create(name, n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float Sine(float ph) => Mathf.Sin(ph * Mathf.PI * 2f);

        void BuildClips()
        {
            clips[Sfx.Swing] = Sweep("swing", 0.14f, 900f, 300f, Sine, 0.85f, 0.35f);
            clips[Sfx.Hit] = Sweep("hit", 0.12f, 220f, 70f, Square, 0.45f, 0.45f);
            clips[Sfx.Crit] = Sweep("crit", 0.2f, 1200f, 500f, Square, 0.3f, 0.4f);
            clips[Sfx.PlayerHurt] = Sweep("hurt", 0.22f, 260f, 90f, Saw, 0.25f, 0.5f);
            clips[Sfx.Dash] = Sweep("dash", 0.2f, 400f, 1400f, Sine, 0.8f, 0.35f);
            clips[Sfx.Whirlwind] = Make("whirl", 0.45f, (t, p) => Noise() * (0.5f + 0.5f * Mathf.Sin(t * 60f)) * Env(t, 0.45f, 0.05f) * 0.4f);
            clips[Sfx.Pickup] = Sweep("pickup", 0.12f, 660f, 1100f, Sine, 0f, 0.35f);
            clips[Sfx.Coin] = Make("coin", 0.18f, (t, p) => Sine(p * (t < 0.06f ? 988f : 1319f)) * Env(t, 0.18f) * 0.3f);
            clips[Sfx.LevelUp] = Make("levelup", 0.6f, (t, p) =>
            {
                float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
                int k = Mathf.Min(3, (int)(t / 0.12f));
                return (Sine(p * notes[k]) * 0.7f + Square(p * notes[k]) * 0.1f) * Env(t, 0.6f) * 0.4f;
            });
            clips[Sfx.Chest] = Sweep("chest", 0.35f, 110f, 440f, Square, 0.15f, 0.3f);
            clips[Sfx.BossRoar] = Make("roar", 0.9f, (t, p) => (Saw(p * (70f - t * 30f)) * 0.6f + Noise() * 0.4f) * Env(t, 0.9f, 0.08f) * 0.55f);
            clips[Sfx.Slam] = Make("slam", 0.45f, (t, p) => (Sine(p * (60f - t * 40f)) * 0.7f + Noise() * 0.3f * (1f - t / 0.45f)) * Env(t, 0.45f) * 0.7f);
            clips[Sfx.Death] = Sweep("death", 0.3f, 420f, 90f, Sine, 0.2f, 0.4f);
            clips[Sfx.Portal] = Make("portal", 0.7f, (t, p) => Sine(p * (300f + Mathf.Sin(t * 25f) * 80f + t * 500f)) * Env(t, 0.7f, 0.1f) * 0.35f);
            clips[Sfx.UiClick] = Sweep("click", 0.04f, 900f, 700f, Sine, 0f, 0.25f);
            clips[Sfx.Potion] = Make("potion", 0.3f, (t, p) => Sine(p * (500f + Mathf.Sin(t * 70f) * 120f)) * Env(t, 0.3f) * 0.3f);
            clips[Sfx.Mission] = Make("mission", 0.5f, (t, p) => Sine(p * (t < 0.15f ? 659.25f : t < 0.3f ? 880f : 1318.5f)) * Env(t, 0.5f) * 0.35f);
        }
    }
}
