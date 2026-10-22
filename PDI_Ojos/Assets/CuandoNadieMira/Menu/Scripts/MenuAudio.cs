using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace CuandoNadieMira.Menu
{
    public enum MenuSound { Hover, Select, Tick, Swish, Heartbeat, Thunder, Drip, Hum, Breath, PageTurn, Snuff }

    /// <summary>
    /// Efectos del menú generados por código (sin archivos externos) y subtítulos de sonidos.
    /// Son deliberadamente suaves: golpes de campana tenues, roces filtrados y ambiente casi inaudible.
    /// </summary>
    public class MenuAudio : MonoBehaviour
    {
        public static MenuAudio Instance { get; private set; }

        [SerializeField] TMP_Text subtitleLabel;
        [SerializeField, Range(0f, 1f)] float uiVolume = 0.22f;

        readonly Dictionary<MenuSound, AudioClip> clips = new();
        AudioSource uiSource;
        Coroutine subtitleRoutine;

        static readonly Dictionary<MenuSound, string> Captions = new()
        {
            { MenuSound.Heartbeat, "[latido]" },
            { MenuSound.Thunder, "[trueno lejano]" },
            { MenuSound.PageTurn, "[una página pasa sola]" },
            { MenuSound.Snuff, "[una vela se apaga]" },
        };

        void Awake()
        {
            Instance = this;
            foreach (MenuSound s in System.Enum.GetValues(typeof(MenuSound)))
                clips[s] = Synth.Create(s);

            uiSource = gameObject.AddComponent<AudioSource>();
            uiSource.playOnAwake = false;
            uiSource.spatialBlend = 0f;
            var reverb = gameObject.AddComponent<AudioReverbFilter>();
            reverb.reverbPreset = AudioReverbPreset.Room;

            SettingsStore.Changed += ApplyVolume;
            ApplyVolume();
            if (subtitleLabel != null) subtitleLabel.alpha = 0f;
        }

        void OnDestroy()
        {
            SettingsStore.Changed -= ApplyVolume;
            if (Instance == this) Instance = null;
        }

        void ApplyVolume() => AudioListener.volume = SettingsStore.MasterVolume;

        public AudioClip Get(MenuSound s) => clips[s];

        /// <summary>Reproduce un efecto de interfaz con una variación de tono mínima.</summary>
        public void Play(MenuSound s, float volume = 1f, float pitchJitter = 0.02f)
        {
            uiSource.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            uiSource.PlayOneShot(clips[s], volume * uiVolume * SettingsStore.SfxVolume);
            ShowCaption(s);
        }

        public void ShowCaption(MenuSound s)
        {
            if (!SettingsStore.Subtitles || subtitleLabel == null || !Captions.TryGetValue(s, out var text)) return;
            if (subtitleRoutine != null) StopCoroutine(subtitleRoutine);
            subtitleRoutine = StartCoroutine(CaptionRoutine(text));
        }

        IEnumerator CaptionRoutine(string text)
        {
            subtitleLabel.text = text;
            subtitleLabel.alpha = 1f;
            yield return new WaitForSecondsRealtime(1.8f);
            for (float t = 0; t < 0.6f; t += Time.unscaledDeltaTime)
            {
                subtitleLabel.alpha = 1f - t / 0.6f;
                yield return null;
            }
            subtitleLabel.alpha = 0f;
        }

        /// <summary>Síntesis de los efectos. Todo pasa por envolventes suaves y filtros paso bajo.</summary>
        static class Synth
        {
            const int Rate = 44100;

            public static AudioClip Create(MenuSound s)
            {
                switch (s)
                {
                    // Campanita de vidrio muy tenue al enfocar un botón
                    case MenuSound.Hover: return Make("hover", 0.5f, (t, n) => SoftBell(t, 783.99f, 9f) * 0.14f);
                    // Dos notas graves en quinta al elegir
                    case MenuSound.Select: return Make("select", 1.6f, (t, n) => (SoftBell(t, 293.66f, 3f) + SoftBell(t - 0.09f, 440f, 3.5f) * 0.6f) * 0.2f);
                    // Clic de madera para cambiar una opción
                    case MenuSound.Tick: return Make("tick", 0.08f, (t, n) => Mathf.Sin(2 * Mathf.PI * 520f * t) * Mathf.Exp(-t * 70f) * 0.12f);
                    // Roce de tela al girar la placa
                    case MenuSound.Swish: return Make("swish", 0.55f, (t, n) => Low(n, 0.06f) * Mathf.Sin(Mathf.PI * t / 0.55f) * 0.5f);
                    case MenuSound.Heartbeat: return Make("heartbeat", 1.2f, (t, n) => (Thump(t) + Thump(t - 0.3f) * 0.6f) * 0.55f);
                    case MenuSound.Thunder: return Make("thunder", 4.5f, (t, n) => Brown(n) * Env(t, 4.5f, 0.8f, 3f) * 0.45f);
                    case MenuSound.Drip: return Make("drip", 0.15f, (t, n) => Mathf.Sin(2 * Mathf.PI * Mathf.Lerp(1100f, 600f, t / 0.15f) * t) * Mathf.Exp(-t * 35f) * 0.2f);
                    case MenuSound.Hum: return Make("hum", 3f, (t, n) => (Mathf.Sin(2 * Mathf.PI * 55f * t) + 0.4f * Mathf.Sin(2 * Mathf.PI * 82.4f * t)) * Mathf.Sin(Mathf.PI * t / 3f) * 0.15f);
                    case MenuSound.Breath: return Make("breath", 2.4f, (t, n) => Low(n, 0.05f) * Mathf.Sin(Mathf.PI * t / 2.4f) * 0.35f);
                    case MenuSound.PageTurn: return Make("page", 0.5f, (t, n) => Low(n, 0.15f) * Env(t, 0.5f, 0.06f, 0.35f) * 0.18f);
                    default: return Make("snuff", 0.3f, (t, n) => Low(n, 0.1f) * Env(t, 0.3f, 0.01f, 0.25f) * 0.2f);
                }
            }

            delegate float SampleFn(float t, float noise);

            static float lp, brown;

            static AudioClip Make(string name, float seconds, SampleFn fn)
            {
                int count = Mathf.CeilToInt(seconds * Rate);
                var data = new float[count];
                var rng = new System.Random(name.GetHashCode());
                lp = brown = 0f;
                for (int i = 0; i < count; i++)
                {
                    float noise = (float)rng.NextDouble() * 2f - 1f;
                    float fadeOut = Mathf.Clamp01((count - i) / (Rate * 0.02f));   // sin clics al final
                    data[i] = Mathf.Clamp(fn(i / (float)Rate, noise) * fadeOut, -1f, 1f);
                }
                var clip = AudioClip.Create("cnm_" + name, count, 1, Rate, false);
                clip.SetData(data, 0);
                return clip;
            }

            static float SoftBell(float t, float f, float decay)
            {
                if (t < 0f) return 0f;
                float env = Mathf.Min(1f, t * 250f) * Mathf.Exp(-t * decay);
                return (Mathf.Sin(2 * Mathf.PI * f * t) + 0.25f * Mathf.Sin(2 * Mathf.PI * f * 2.01f * t) * Mathf.Exp(-t * decay * 2f)) * env;
            }

            static float Env(float t, float len, float attack, float release)
            {
                if (t < attack) return t / attack;
                if (t > len - release) return Mathf.Max(0f, (len - t) / release);
                return 1f;
            }

            static float Low(float n, float k)
            {
                lp += (n - lp) * k;
                return lp;
            }

            static float Brown(float n)
            {
                brown = Mathf.Clamp(brown + n * 0.012f, -1f, 1f) * 0.999f;
                return brown;
            }

            static float Thump(float t)
            {
                if (t < 0f) return 0f;
                return Mathf.Sin(2 * Mathf.PI * 42f * t) * Mathf.Min(1f, t * 200f) * Mathf.Exp(-t * 16f);
            }
        }
    }
}
