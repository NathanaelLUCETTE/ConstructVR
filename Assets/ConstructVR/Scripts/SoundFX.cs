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
