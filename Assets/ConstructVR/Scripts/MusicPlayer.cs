using UnityEngine;

namespace ConstructVR
{
    /// <summary>Petite musique d'ambiance douce, composée et générée par le code, jouée en boucle.</summary>
    [RequireComponent(typeof(AudioSource))]
    public class MusicPlayer : MonoBehaviour
    {
        const int Rate = 32000;
        const float Bpm = 92f;

        AudioSource m_Source;

        // accords (notes MIDI) : Do - La mineur - Fa - Sol, puis Do - Mi mineur - Fa - Sol
        static readonly int[][] Chords =
        {
            new[] { 48, 55, 60, 64 }, new[] { 45, 52, 57, 60 }, new[] { 41, 48, 53, 57 }, new[] { 43, 50, 55, 59 },
            new[] { 48, 55, 60, 64 }, new[] { 40, 47, 55, 59 }, new[] { 41, 48, 53, 57 }, new[] { 43, 50, 55, 62 },
        };

        // mélodie : une note par croche, -1 = silence
        static readonly int[] Melody =
        {
            72, -1, 76, 79, 76, -1, 74, 72,   69, -1, 72, 76, 74, -1, 72, -1,
            69, -1, 72, 77, 76, -1, 74, -1,   71, -1, 74, 79, 77, 76, 74, -1,
            72, -1, 76, 79, 81, -1, 79, 76,   71, -1, 74, 76, 79, -1, 76, -1,
            77, -1, 76, 74, 72, -1, 69, 72,   74, -1, 71, 67, 72, -1, -1, -1,
        };

        public float Volume
        {
            get => m_Source.volume;
            set => m_Source.volume = value;
        }

        void Awake()
        {
            m_Source = GetComponent<AudioSource>();
            m_Source.clip = Compose();
            m_Source.loop = true;
            m_Source.spatialBlend = 0f;
            m_Source.playOnAwake = false;
        }

        void Start()
        {
            m_Source.volume = SaveSystem.Data.MusicVolume;
            m_Source.Play();
        }

        static float Freq(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);

        static AudioClip Compose()
        {
            float beat = 60f / Bpm;
            float eighth = beat * 0.5f;
            float barLen = beat * 4f;
            int bars = Chords.Length;
            int total = Mathf.CeilToInt(bars * barLen * Rate);
            var data = new float[total];

            for (int bar = 0; bar < bars; bar++)
            {
                var chord = Chords[bar];
                // nappe douce
                foreach (var note in chord) AddTone(data, bar * barLen, barLen, Freq(note), 0.045f, 0.35f, 0.6f, true);
                // basse
                AddTone(data, bar * barLen, beat * 1.8f, Freq(chord[0] - 12), 0.12f, 0.01f, 0.4f, false);
                AddTone(data, bar * barLen + beat * 2f, beat * 1.8f, Freq(chord[0] - 12), 0.1f, 0.01f, 0.4f, false);
                // arpège léger
                for (int i = 0; i < 8; i++)
                    AddPluck(data, bar * barLen + i * eighth, eighth * 1.6f, Freq(chord[(i % 3) + 1] + 12), 0.025f);
            }
            for (int i = 0; i < Melody.Length; i++)
                if (Melody[i] >= 0) AddPluck(data, i * eighth, eighth * 2.2f, Freq(Melody[i]), 0.075f);

            for (int i = 0; i < total; i++) data[i] = (float)System.Math.Tanh(data[i] * 1.2f);
            var clip = AudioClip.Create("Musique", total, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static void AddTone(float[] data, float start, float length, float f, float amp, float attack, float release, bool soft)
        {
            int s = (int)(start * Rate), n = (int)(length * Rate);
            for (int i = 0; i < n && s + i < data.Length; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Min(1f, t / Mathf.Max(attack, 0.001f)) * Mathf.Clamp01((length - t) / release);
                float ph = 2f * Mathf.PI * f * t;
                float w = soft ? Mathf.Sin(ph) + 0.15f * Mathf.Sin(2f * ph) : Mathf.Sin(ph) + 0.3f * Mathf.Sin(2f * ph);
                data[s + i] += w * env * amp;
            }
        }

        static void AddPluck(float[] data, float start, float length, float f, float amp)
        {
            int s = (int)(start * Rate), n = (int)(length * Rate);
            for (int i = 0; i < n && s + i < data.Length; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Min(1f, t * 400f) * Mathf.Exp(-t * 5.5f) * Mathf.Clamp01((length - t) * 30f);
                float ph = 2f * Mathf.PI * f * t;
                data[s + i] += (Mathf.Sin(ph) + 0.25f * Mathf.Sin(3f * ph) * Mathf.Exp(-t * 12f)) * env * amp;
            }
        }
    }
}
