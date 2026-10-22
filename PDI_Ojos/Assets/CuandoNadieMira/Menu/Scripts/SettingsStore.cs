using System;
using UnityEngine;

namespace CuandoNadieMira.Menu
{
    /// <summary>Opciones de accesibilidad y audio guardadas con PlayerPrefs.</summary>
    public static class SettingsStore
    {
        const string KeyReduceMotion = "cnm.reduceMotion";
        const string KeyDisableSparkles = "cnm.disableSparkles";
        const string KeyMasterVolume = "cnm.masterVolume";
        const string KeySfxVolume = "cnm.sfxVolume";
        const string KeyMusicVolume = "cnm.musicVolume";
        const string KeyLargeText = "cnm.largeText";
        const string KeySubtitles = "cnm.subtitles";
        const string KeyLowQuality = "cnm.lowQuality";

        public static event Action Changed;

        public static bool ReduceMotion { get => GetBool(KeyReduceMotion); set => SetBool(KeyReduceMotion, value); }
        public static bool DisableSparkles { get => GetBool(KeyDisableSparkles); set => SetBool(KeyDisableSparkles, value); }
        public static bool LargeText { get => GetBool(KeyLargeText); set => SetBool(KeyLargeText, value); }
        public static bool Subtitles { get => GetBool(KeySubtitles); set => SetBool(KeySubtitles, value); }
        public static bool LowQuality { get => GetBool(KeyLowQuality); set => SetBool(KeyLowQuality, value); }

        public static float MasterVolume { get => PlayerPrefs.GetFloat(KeyMasterVolume, 0.8f); set => SetFloat(KeyMasterVolume, value); }
        public static float SfxVolume { get => PlayerPrefs.GetFloat(KeySfxVolume, 0.6f); set => SetFloat(KeySfxVolume, value); }
        public static float MusicVolume { get => PlayerPrefs.GetFloat(KeyMusicVolume, 0.7f); set => SetFloat(KeyMusicVolume, value); }

        /// <summary>Factor de movimiento de cámara: 20 % con "Reducir movimiento".</summary>
        public static float MotionScale => ReduceMotion ? 0.2f : 1f;

        /// <summary>Escala del texto: 100 % o 125 %.</summary>
        public static float TextScale => LargeText ? 1.25f : 1f;

        static bool GetBool(string key) => PlayerPrefs.GetInt(key, 0) == 1;

        static void SetBool(string key, bool value)
        {
            PlayerPrefs.SetInt(key, value ? 1 : 0);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        static void SetFloat(string key, float value)
        {
            PlayerPrefs.SetFloat(key, Mathf.Clamp01(value));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
