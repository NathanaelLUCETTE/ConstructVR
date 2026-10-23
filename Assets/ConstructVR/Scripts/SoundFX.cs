using UnityEngine;

namespace ConstructVR
{
    /// <summary>Sons du jeu, générés par le code (aucun fichier audio nécessaire).</summary>
    public static class SoundFX
    {
        const int Rate = 44100;

        static AudioClip s_Click, s_Error, s_Success, s_Trash, s_Pop, s_Blip, s_Talk, s_Hover, s_Star;

        /// <summary>Volume général des effets (réglable dans les options).</summary>
        public static float Volume = 0.8f;

        public static AudioClip Hover => s_Hover ??= Make("Survol", 0.04f, (t, i) =>
            Mathf.Sin(t * 2f * Mathf.PI * 1900f) * Fade(t, 0.04f) * 0.18f);

        public static AudioClip Star => s_Star ??= Make("Etoile", 0.45f, (t, i) =>
            (Mathf.Sin(t * 2f * Mathf.PI * 1568f) + 0.4f * Mathf.Sin(t * 2f * Mathf.PI * 3136f)) * Mathf.Exp(-t * 7f) * Mathf.Min(1f, t * 300f) * 0.35f);

        public static AudioClip Click => s_Click ??= Make("Clic", 0.06f, (t, i) =>
            Noise(i) * Mathf.Exp(-t * 90f) * 0.8f + Mathf.Sin(t * 2f * Mathf.PI * 2400f) * Mathf.Exp(-t * 70f) * 0.4f);

        public static AudioClip Error => s_Error ??= Make("Erreur", 0.38f, (t, i) =>
        {
            float f = t < 0.18f ? 220f : 165f;
            float env = t < 0.18f ? Fade(t, 0.18f) : Fade(t - 0.19f, 0.19f);
            return Square(t, f) * env * 0.35f;
        });

        public static AudioClip Success => s_Success ??= Make("Bravo", 0.75f, (t, i) =>
        {
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
            int n = Mathf.Min((int)(t / 0.15f), 3);
            float local = t - n * 0.15f;
            float len = n == 3 ? 0.3f : 0.15f;
            return Mathf.Sin(t * 2f * Mathf.PI * notes[n]) * Fade(local, len) * 0.45f;
        });

        public static AudioClip Trash => s_Trash ??= Make("Poubelle", 0.35f, (t, i) =>
            Noise(i) * Mathf.Sin(Mathf.PI * t / 0.35f) * 0.35f + Mathf.Sin(t * 2f * Mathf.PI * (300f - t * 500f)) * Fade(t, 0.35f) * 0.3f);

        public static AudioClip Pop => s_Pop ??= Make("Pop", 0.12f, (t, i) =>
            Mathf.Sin(t * 2f * Mathf.PI * (500f + t * 4000f)) * Fade(t, 0.12f) * 0.5f);

        public static AudioClip Blip => s_Blip ??= Make("Blip", 0.07f, (t, i) =>
            Mathf.Sin(t * 2f * Mathf.PI * 1320f) * Fade(t, 0.07f) * 0.35f);

        public static AudioClip Talk => s_Talk ??= Make("Parole", 0.5f, (t, i) =>
        {
            float syll = Mathf.Abs(Mathf.Sin(t * Mathf.PI * 10f));
            float f = 520f + 90f * Mathf.Sin(t * 23f);
            return (Mathf.Sin(t * 2f * Mathf.PI * f) * 0.6f + Mathf.Sin(t * 2f * Mathf.PI * f * 2f) * 0.2f) * syll * Fade(t, 0.5f) * 0.3f;
        });

        // ---------------------------------------------------------------- sons des animations de fin

        static AudioClip s_Engine, s_Horn, s_Fanfare, s_Boom, s_DingDong, s_Beeps, s_Chime, s_Rumble, s_Rocket;

        public static AudioClip Engine => s_Engine ??= Make("Moteur", 2.2f, (t, i) =>
        {
            float f = 55f + 35f * Mathf.Clamp01(t / 0.8f) - 20f * Mathf.Clamp01((t - 1.6f) / 0.6f);
            float env = Mathf.Min(1f, t * 8f) * Mathf.Clamp01((2.2f - t) * 3f);
            return (Square(t, f) * 0.5f + Mathf.Sin(t * 2f * Mathf.PI * f * 2f) * 0.3f + Noise(i) * 0.15f) * env * 0.35f;
        });

        public static AudioClip Horn => s_Horn ??= Make("Klaxon", 0.6f, (t, i) =>
        {
            bool on = t < 0.22f || (t > 0.3f && t < 0.52f);
            float env = on ? 1f : 0f;
            return (Square(t, 392f) * 0.5f + Square(t, 494f) * 0.5f) * env * 0.25f;
        });

        public static AudioClip Fanfare => s_Fanfare ??= Make("Fanfare", 1.6f, (t, i) =>
        {
            float[] notes = { 392f, 523.25f, 659.25f, 783.99f, 659.25f, 783.99f };
            float[] starts = { 0f, 0.18f, 0.36f, 0.54f, 0.9f, 1.05f };
            float[] lengths = { 0.16f, 0.16f, 0.16f, 0.34f, 0.13f, 0.55f };
            float v = 0f;
            for (int n = 0; n < notes.Length; n++)
            {
                float local = t - starts[n];
                if (local < 0f || local > lengths[n]) continue;
                float ph = t * notes[n];
                float saw = 2f * (ph - Mathf.Floor(ph + 0.5f));
                v += (saw * 0.5f + Mathf.Sin(ph * 2f * Mathf.PI) * 0.5f) * Fade(local, lengths[n]);
            }
            return v * 0.32f;
        });

        public static AudioClip Boom => s_Boom ??= Make("Boum", 0.9f, (t, i) =>
            (Noise(i) * 0.7f + Mathf.Sin(t * 2f * Mathf.PI * (90f - t * 60f)) * 0.6f) * Mathf.Exp(-t * 5f) * Mathf.Min(1f, t * 400f) * 0.5f);

        public static AudioClip DingDong => s_DingDong ??= Make("Sonnette", 1.3f, (t, i) =>
        {
            float a = Mathf.Sin(t * 2f * Mathf.PI * 659.25f) * Mathf.Exp(-t * 3f);
            float u = t - 0.5f;
            float b = u < 0f ? 0f : Mathf.Sin(u * 2f * Mathf.PI * 523.25f) * Mathf.Exp(-u * 3f);
            return (a + b) * Mathf.Min(1f, t * 300f) * 0.35f;
        });

        public static AudioClip Beeps => s_Beeps ??= Make("Bips", 1.2f, (t, i) =>
        {
            float[] notes = { 880f, 660f, 990f, 520f, 1180f, 740f };
            int n = Mathf.Min((int)(t / 0.2f), notes.Length - 1);
            float local = t - n * 0.2f;
            return Square(t, notes[n]) * Fade(local, 0.15f) * 0.22f;
        });

        public static AudioClip Chime => s_Chime ??= Make("Carillon", 1.4f, (t, i) =>
        {
            float v = 0f;
            float[] notes = { 1046.5f, 1318.5f, 1568f, 2093f };
            for (int n = 0; n < notes.Length; n++)
            {
                float u = t - n * 0.12f;
                if (u > 0f) v += Mathf.Sin(u * 2f * Mathf.PI * notes[n]) * Mathf.Exp(-u * 4f);
            }
            return v * Mathf.Min(1f, t * 300f) * 0.2f;
        });

        public static AudioClip Rumble => s_Rumble ??= Make("Grondement", 1.8f, (t, i) =>
            (Noise(i) * 0.5f + Mathf.Sin(t * 2f * Mathf.PI * 45f) * 0.5f) * Mathf.Sin(Mathf.PI * t / 1.8f) * 0.4f);

        public static AudioClip Rocket => s_Rocket ??= Make("Fusée", 2.5f, (t, i) =>
            (Noise(i) * 0.8f + Mathf.Sin(t * 2f * Mathf.PI * (80f + t * 120f)) * 0.3f) * Mathf.Min(1f, t * 2f) * Mathf.Clamp01((2.5f - t) * 2f) * 0.35f);

        public static void Play(AudioClip clip, Vector3 position, float volume = 1f)
        {
            if (clip == null || Volume <= 0f || !Application.isPlaying) return;
            var go = new GameObject("Son " + clip.name);
            go.transform.position = position;
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.volume = volume * Volume;
            src.spatialBlend = 0.6f;
            src.minDistance = 0.5f;
            src.Play();
            Object.Destroy(go, clip.length + 0.1f);
        }

        static AudioClip Make(string name, float duration, System.Func<float, int, float> sample)
        {
            int count = Mathf.CeilToInt(duration * Rate);
            var data = new float[count];
            for (int i = 0; i < count; i++) data[i] = Mathf.Clamp(sample(i / (float)Rate, i), -1f, 1f);
            var clip = AudioClip.Create(name, count, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float Fade(float t, float len) => t < 0f || t > len ? 0f : Mathf.Min(1f, t * 200f) * (1f - t / len);
        static float Square(float t, float f) => Mathf.Sign(Mathf.Sin(t * 2f * Mathf.PI * f)) * 0.6f + Mathf.Sin(t * 2f * Mathf.PI * f) * 0.4f;

        static float Noise(int i)
        {
            uint x = (uint)i * 747796405u + 2891336453u;
            x = ((x >> (int)((x >> 28) + 4u)) ^ x) * 277803737u;
            return ((x >> 22) ^ x) / (float)uint.MaxValue * 2f - 1f;
        }
    }
}
