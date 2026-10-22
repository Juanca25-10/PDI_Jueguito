using System;
using System.Collections;
using UnityEngine;

namespace CuandoNadieMira.Menu
{
    /// <summary>
    /// Música ambiental generada en tiempo real (sin archivos): un bordón grave, acordes lentos en re menor
    /// que nunca terminan de resolver, notas sueltas de caja de música con eco y un viento muy tenue.
    /// La síntesis corre en el hilo de audio, así que no se repite ni ocupa memoria.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class MenuMusic : MonoBehaviour
    {
        public static MenuMusic Instance { get; private set; }

        [SerializeField, Range(0f, 1f)] float baseVolume = 0.55f;
        [SerializeField] float fadeInTime = 4f;

        AudioSource source;
        Synth synth;
        float fade;
        Coroutine fadeRoutine;

        void Awake()
        {
            Instance = this;
            int rate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
            synth = new Synth(rate, Environment.TickCount);
            source = GetComponent<AudioSource>();
            source.clip = AudioClip.Create("cnm_music", rate * 600, 2, rate, true, synth.Read);
            source.loop = true;
            source.spatialBlend = 0f;
            source.playOnAwake = false;
            source.volume = 0f;
            source.Play();
            FadeTo(1f, fadeInTime);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update() => source.volume = baseVolume * SettingsStore.MusicVolume * fade;

        /// <summary>Lleva la música a <paramref name="target"/> (0..1) en <paramref name="time"/> segundos.</summary>
        public void FadeTo(float target, float time)
        {
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(Fade(target, time));
        }

        IEnumerator Fade(float target, float time)
        {
            float start = fade;
            for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
            {
                fade = Mathf.Lerp(start, target, t / time);
                yield return null;
            }
            fade = target;
        }

        /// <summary>Sintetizador estéreo. Solo usa sus propios campos: es seguro en el hilo de audio.</summary>
        sealed class Synth
        {
            const double TwoPi = Math.PI * 2.0;

            // Progresión en re menor: Dm, Sib, Solm, La (la dominante deja todo en suspenso)
            static readonly double[][] Chords =
            {
                new[] { 146.83, 174.61, 220.00 },
                new[] { 116.54, 146.83, 174.61 },
                new[] { 98.00, 116.54, 146.83 },
                new[] { 110.00, 138.59, 164.81 },
            };

            // Notas de la caja de música (re menor, registro agudo)
            static readonly double[] Bells = { 587.33, 698.46, 783.99, 880.00, 1046.50, 1174.66, 466.16, 659.25 };

            const double ChordSeconds = 12.0;
            const double CrossfadeSeconds = 3.5;

            readonly int rate;
            readonly System.Random rng;
            long n;

            // Campanas activas
            readonly double[] bellFreq = new double[4];
            readonly long[] bellStart = new long[4];
            readonly float[] bellPan = new float[4];
            int bellNext;
            long nextBellAt;

            // Eco estéreo con filtro en la realimentación
            readonly float[] delayL, delayR;
            int delayPos;
            float fbL, fbR;

            // Filtros de un polo
            float padLp, windLp, windLp2;

            public Synth(int sampleRate, int seed)
            {
                rate = sampleRate;
                rng = new System.Random(seed);
                delayL = new float[(int)(rate * 0.53)];
                delayR = new float[(int)(rate * 0.71)];
                for (int i = 0; i < bellStart.Length; i++) bellStart[i] = -rate * 60L;
                nextBellAt = rate * 3L;
            }

            public void Read(float[] data)
            {
                for (int i = 0; i + 1 < data.Length; i += 2)
                {
                    double t = (double)n / rate;
                    float mono = Drone(t) + Pad(t) + Wind();
                    float bl = 0f, br = 0f;
                    Bell(ref bl, ref br);

                    // Eco: las campanas se repiten a izquierda y derecha, cada vez más apagadas
                    float dl = delayL[delayPos % delayL.Length];
                    float dr = delayR[delayPos % delayR.Length];
                    fbL += (dl - fbL) * 0.35f;
                    fbR += (dr - fbR) * 0.35f;
                    delayL[delayPos % delayL.Length] = bl + fbR * 0.42f;
                    delayR[delayPos % delayR.Length] = br + fbL * 0.42f;
                    delayPos++;

                    float l = mono + bl + dl * 0.45f;
                    float r = mono + br + dr * 0.45f;
                    data[i] = (float)Math.Tanh(l * 0.9f) * 0.6f;
                    data[i + 1] = (float)Math.Tanh(r * 0.9f) * 0.6f;
                    n++;
                }
            }

            float Drone(double t)
            {
                double trem = 0.75 + 0.25 * Math.Sin(TwoPi * 0.07 * t);
                double v = Math.Sin(TwoPi * 73.42 * t) * 0.16 + Math.Sin(TwoPi * 36.71 * t) * 0.1
                           + Math.Sin(TwoPi * 110.0 * t + Math.Sin(TwoPi * 0.05 * t)) * 0.04;
                return (float)(v * trem);
            }

            float Pad(double t)
            {
                int idx = (int)(t / ChordSeconds);
                double inChord = t - idx * ChordSeconds;
                double w = Math.Min(1.0, inChord / CrossfadeSeconds);
                w = w * w * (3 - 2 * w);
                double cur = ChordTone(Chords[idx % Chords.Length], t);
                double prev = idx > 0 ? ChordTone(Chords[(idx - 1) % Chords.Length], t) : 0.0;
                double breath = 0.7 + 0.3 * Math.Sin(TwoPi * 0.11 * t + 1.3);
                float raw = (float)((cur * w + prev * (1 - w)) * breath * 0.07);
                padLp += (raw - padLp) * 0.08f;   // suaviza el timbre
                return padLp;
            }

            static double ChordTone(double[] chord, double t)
            {
                double v = 0;
                foreach (double f in chord)
                {
                    // Dos osciladores levemente desafinados por nota: un coro lento
                    v += Math.Sin(TwoPi * f * t) + Math.Sin(TwoPi * f * 1.004 * t + 0.7);
                    v += 0.25 * Math.Sin(TwoPi * f * 2.0 * t + 0.3);
                }
                return v;
            }

            float Wind()
            {
                float noise = (float)(rng.NextDouble() * 2 - 1);
                windLp += (noise - windLp) * 0.012f;
                windLp2 += (windLp - windLp2) * 0.02f;
                double swell = 0.5 + 0.5 * Math.Sin(TwoPi * 0.031 * n / rate);
                return windLp2 * (float)(0.5 + swell) * 0.9f;
            }

            void Bell(ref float l, ref float r)
            {
                if (n >= nextBellAt)
                {
                    int k = bellNext++ % bellFreq.Length;
                    bellFreq[k] = Bells[rng.Next(Bells.Length)] * (rng.NextDouble() < 0.2 ? 0.5 : 1.0);
                    bellStart[k] = n;
                    bellPan[k] = (float)(rng.NextDouble() * 0.8 - 0.4);
                    // A veces dos notas seguidas, a veces silencios largos
                    double gap = rng.NextDouble() < 0.3 ? 0.35 + rng.NextDouble() * 0.4 : 2.2 + rng.NextDouble() * 4.5;
                    nextBellAt = n + (long)(gap * rate);
                }

                for (int k = 0; k < bellFreq.Length; k++)
                {
                    double dt = (double)(n - bellStart[k]) / rate;
                    if (dt < 0 || dt > 5) continue;
                    double f = bellFreq[k];
                    double env = Math.Min(1.0, dt * 300) * Math.Exp(-dt * 1.6);
                    double v = (Math.Sin(TwoPi * f * dt) + 0.3 * Math.Sin(TwoPi * f * 2.76 * dt) * Math.Exp(-dt * 3)
                                + 0.12 * Math.Sin(TwoPi * f * 5.4 * dt) * Math.Exp(-dt * 6)) * env * 0.075;
                    l += (float)(v * (0.5 - bellPan[k]));
                    r += (float)(v * (0.5 + bellPan[k]));
                }
            }
        }
    }
}
