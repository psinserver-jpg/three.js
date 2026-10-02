using System.Collections.Generic;
using UnityEngine;

namespace NexusStrike
{
    /// <summary>
    /// Procedurally synthesised sound effects (no audio assets needed) played through a small pool
    /// of 3D sources plus one 2D source for UI / first-person sounds.
    /// </summary>
    public static class Sfx
    {
        const int Rate = 22050;
        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        static AudioSource[] pool;
        static int poolIndex;
        static AudioSource[] ui;
        static int uiIndex;
        public static float masterVolume = 0.7f;

        public static void Init(Transform owner)
        {
            if (pool != null) return;
            var host = new GameObject("SfxPool");
            host.transform.SetParent(owner, false);
            pool = new AudioSource[32];
            for (int i = 0; i < pool.Length; i++)
            {
                var go = new GameObject("Src" + i);
                go.transform.SetParent(host.transform, false);
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 1f;
                s.minDistance = 4f;
                s.maxDistance = 70f;
                s.rolloffMode = AudioRolloffMode.Linear;
                s.dopplerLevel = 0f;
                pool[i] = s;
            }
            ui = new AudioSource[8];
            for (int i = 0; i < ui.Length; i++)
            {
                ui[i] = host.AddComponent<AudioSource>();
                ui[i].playOnAwake = false;
                ui[i].spatialBlend = 0f;
            }
            Build();
        }

        public static void Play(string id, Vector3 pos, float volume = 1f, float pitch = 1f)
        {
            AudioClip c;
            if (pool == null || !clips.TryGetValue(id, out c)) return;
            var s = pool[poolIndex];
            poolIndex = (poolIndex + 1) % pool.Length;
            s.transform.position = pos;
            s.pitch = pitch * Random.Range(0.95f, 1.05f);
            s.volume = volume * masterVolume;
            s.clip = c;
            s.Play();
        }

        public static void Play2D(string id, float volume = 1f, float pitch = 1f)
        {
            AudioClip c;
            if (ui == null || !clips.TryGetValue(id, out c)) return;
            var s = ui[uiIndex];
            uiIndex = (uiIndex + 1) % ui.Length;
            s.pitch = pitch;
            s.volume = volume * masterVolume;
            s.clip = c;
            s.Play();
        }

        /// <summary>Plays 2D for the local player, 3D for everybody else.</summary>
        public static void PlayFor(Combatant c, string id, float volume = 1f, float pitch = 1f)
        {
            if (c != null && c.isPlayer) Play2D(id, volume * 0.6f, pitch);
            else if (c != null) Play(id, c.ChestPos, volume, pitch);
        }

        // ----------------------------------------------------------------- synthesis

        delegate float Gen(float t, float u, ref float state);

        static void Make(string id, float seconds, Gen g)
        {
            int n = Mathf.Max(1, (int)(seconds * Rate));
            var data = new float[n];
            float state = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                data[i] = Mathf.Clamp(g(t, t / seconds, ref state), -1f, 1f);
            }
            var clip = AudioClip.Create(id, n, 1, Rate, false);
            clip.SetData(data, 0);
            clips[id] = clip;
        }

        static float Noise() { return Random.value * 2f - 1f; }
        static float Sin(float hz, float t) { return Mathf.Sin(2f * Mathf.PI * hz * t); }

        static float LowNoise(ref float s, float k)
        {
            s += (Noise() - s) * k;
            return s;
        }

        static void Build()
        {
            Make("rifle", 0.14f, (float t, float u, ref float s) =>
                (Noise() * 0.6f + Sin(140f, t) * 0.6f) * Mathf.Exp(-t * 38f));
            Make("cannon", 0.12f, (float t, float u, ref float s) =>
                (LowNoise(ref s, 0.5f) * 0.9f + Sin(90f, t) * 0.7f) * Mathf.Exp(-t * 30f));
            Make("dart", 0.12f, (float t, float u, ref float s) =>
                (Noise() * 0.35f + Sin(900f - 500f * u, t) * 0.4f) * Mathf.Exp(-t * 35f));
            Make("bolt", 0.16f, (float t, float u, ref float s) =>
                Sin(1400f - 1000f * u, t) * 0.5f * Mathf.Exp(-t * 18f));
            Make("rocket", 0.4f, (float t, float u, ref float s) =>
                (LowNoise(ref s, 0.25f) * 1.4f + Sin(70f, t) * 0.4f * Mathf.Exp(-t * 20f)) * Mathf.Exp(-t * 7f));
            Make("explosion", 1.0f, (float t, float u, ref float s) =>
                (LowNoise(ref s, 0.12f) * 2.2f + Sin(48f, t) * Mathf.Exp(-t * 6f) * 0.9f) * Mathf.Exp(-t * 4.5f));
            Make("heal", 0.3f, (float t, float u, ref float s) =>
                Sin(600f + 500f * u, t) * 0.35f * Mathf.Sin(Mathf.PI * u));
            Make("hit", 0.06f, (float t, float u, ref float s) =>
                Sin(1900f, t) * 0.55f * Mathf.Exp(-t * 60f));
            Make("headshot", 0.12f, (float t, float u, ref float s) =>
                (Sin(2400f, t) + Sin(1500f, t)) * 0.35f * Mathf.Exp(-t * 30f));
            Make("kill", 0.32f, (float t, float u, ref float s) =>
                Sin(u < 0.4f ? 880f : 1320f, t) * 0.45f * Mathf.Exp(-t * 6f));
            Make("ability", 0.35f, (float t, float u, ref float s) =>
                LowNoise(ref s, 0.05f + 0.4f * u) * 1.6f * Mathf.Sin(Mathf.PI * u));
            Make("ult", 1.0f, (float t, float u, ref float s) =>
                (Sin(180f + 700f * u, t) * 0.4f + Sin(360f + 1400f * u, t) * 0.15f) * Mathf.Sin(Mathf.PI * u) * (0.75f + 0.25f * Sin(12f, t)));
            Make("reload", 0.3f, (float t, float u, ref float s) =>
                (u < 0.15f || (u > 0.6f && u < 0.72f)) ? Noise() * 0.5f : 0f);
            Make("barrier", 0.5f, (float t, float u, ref float s) =>
                (Sin(220f, t) + Sin(331f, t)) * 0.25f * Mathf.Sin(Mathf.PI * u));
            Make("stun", 0.7f, (float t, float u, ref float s) =>
                (Sin(520f, t) + Sin(780f, t) * 0.6f) * 0.3f * Mathf.Exp(-t * 4f));
            Make("announce", 0.6f, (float t, float u, ref float s) =>
                (Sin(u < 0.5f ? 660f : 990f, t) + Sin(u < 0.5f ? 330f : 495f, t) * 0.5f) * 0.3f * Mathf.Exp(-(u % 0.5f) * 5f));
            Make("pickup", 0.25f, (float t, float u, ref float s) =>
                Sin(700f + 900f * u, t) * 0.4f * (1f - u));
            Make("death", 0.5f, (float t, float u, ref float s) =>
                Sin(400f - 250f * u, t) * 0.4f * (1f - u));
            Make("whoosh", 0.3f, (float t, float u, ref float s) =>
                LowNoise(ref s, 0.15f) * 1.5f * Mathf.Sin(Mathf.PI * u));
            Make("slam", 0.8f, (float t, float u, ref float s) =>
                (LowNoise(ref s, 0.08f) * 2.4f + Sin(38f, t)) * Mathf.Exp(-t * 4f));
        }
    }
}
