using System.Collections.Generic;
using UnityEngine;

namespace CuandoNadieMira.Menu
{
    /// <summary>Se apaga con "Calidad baja" (niebla, polvo, haces de luz, sonda de reflejos).</summary>
    public class LowQualityObject : MonoBehaviour
    {
        static readonly List<LowQualityObject> all = new();

        void Awake()
        {
            all.Add(this);
            if (all.Count == 1) SettingsStore.Changed += ApplyAll;
            gameObject.SetActive(!SettingsStore.LowQuality);
        }

        void OnDestroy()
        {
            all.Remove(this);
            if (all.Count == 0) SettingsStore.Changed -= ApplyAll;
        }

        static void ApplyAll()
        {
            foreach (var o in all) if (o != null) o.gameObject.SetActive(!SettingsStore.LowQuality);
        }
    }
}
