using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace CuandoNadieMira.Menu
{
    /// <summary>Aplica "Tamaño de texto" (100 % / 125 %) a todos los textos bajo este objeto.</summary>
    public class TextScaleApplier : MonoBehaviour
    {
        readonly Dictionary<TMP_Text, float> baseSizes = new();

        void Awake()
        {
            foreach (var t in GetComponentsInChildren<TMP_Text>(true)) baseSizes[t] = t.fontSize;
            SettingsStore.Changed += Apply;
            Apply();
        }

        void OnDestroy() => SettingsStore.Changed -= Apply;

        void Apply()
        {
            foreach (var kv in baseSizes)
                if (kv.Key != null) kv.Key.fontSize = kv.Value * SettingsStore.TextScale;
        }
    }
}
