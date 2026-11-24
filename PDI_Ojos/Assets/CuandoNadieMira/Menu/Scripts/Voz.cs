using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CuandoNadieMira.Menu
{
    /// <summary>
    /// Voces y efectos generados (ElevenLabs). Los clips se buscan por nombre en
    /// Audio/Resources/Voces y Audio/Resources/Efectos. Si un archivo todavía no existe, no suena nada
    /// y el juego sigue igual. Una sola voz a la vez (2D), con cola para encadenar frases.
    /// </summary>
    public class Voz : MonoBehaviour
    {
        static Voz inst;
        static readonly Dictionary<string, AudioClip> cache = new();
        static readonly HashSet<string> faltan = new();

        AudioSource src;
        readonly Queue<string> cola = new();

        public static bool Hablando => inst != null && inst.src.isPlaying;
        public static float Volumen => Mathf.Clamp01(SettingsStore.SfxVolume * 1.4f);

        /// <summary>Clip de Voces/ o Efectos/ (null si no existe).</summary>
        public static AudioClip Clip(string nombre)
        {
            if (string.IsNullOrEmpty(nombre)) return null;
            if (cache.TryGetValue(nombre, out var c)) return c;
            if (faltan.Contains(nombre)) return null;
            c = Resources.Load<AudioClip>("Voces/" + nombre) ?? Resources.Load<AudioClip>("Efectos/" + nombre);
            if (c != null) cache[nombre] = c; else faltan.Add(nombre);
            return c;
        }

        public static bool Existe(string nombre) => Clip(nombre) != null;

        static Voz Get()
        {
            if (inst != null) return inst;
            var go = new GameObject("[Voz]");
            inst = go.AddComponent<Voz>();
            inst.src = go.AddComponent<AudioSource>();
            inst.src.playOnAwake = false;
            inst.src.spatialBlend = 0f;
            inst.src.priority = 0;
            return inst;
        }

        /// <summary>Dice una frase ahora (corta la anterior). Devuelve su duración (0 si no existe).</summary>
        public static float Decir(string nombre)
        {
            var c = Clip(nombre);
            if (c == null) return 0f;
            var v = Get();
            v.cola.Clear();
            v.src.Stop();
            v.src.clip = c;
            v.src.volume = Volumen;
            v.src.Play();
            return c.length;
        }

        /// <summary>Dice la frase cuando termine la actual.</summary>
        public static void Encolar(string nombre)
        {
            if (Clip(nombre) == null) return;
            var v = Get();
            if (!v.src.isPlaying && v.cola.Count == 0) Decir(nombre);
            else v.cola.Enqueue(nombre);
        }

        public static void Callar()
        {
            if (inst == null) return;
            inst.cola.Clear();
            inst.src.Stop();
        }

        /// <summary>Espera (tiempo real) a que termine de hablar, con un máximo.</summary>
        public static IEnumerator Esperar(float maximo = 8f)
        {
            float t = 0f;
            while ((Hablando || (inst != null && inst.cola.Count > 0)) && t < maximo)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        /// <summary>Efecto puntual en 3D (o 2D si pos es null). Devuelve la fuente para poder detenerla.</summary>
        public static AudioSource Efecto(string nombre, Vector3? pos, float volumen = 1f, float maxDist = 25f)
        {
            var c = Clip(nombre);
            if (c == null) return null;
            var go = new GameObject("[Sfx] " + nombre);
            if (pos.HasValue) go.transform.position = pos.Value;
            var s = go.AddComponent<AudioSource>();
            s.clip = c;
            s.volume = volumen * Volumen;
            s.spatialBlend = pos.HasValue ? 1f : 0f;
            s.rolloffMode = AudioRolloffMode.Logarithmic;
            s.minDistance = 2f;
            s.maxDistance = maxDist;
            s.dopplerLevel = 0f;
            s.Play();
            Destroy(go, c.length + 0.2f);
            return s;
        }

        void Update()
        {
            if (!src.isPlaying && cola.Count > 0)
            {
                var c = Clip(cola.Dequeue());
                if (c != null) { src.clip = c; src.volume = Volumen; src.Play(); }
            }
        }

        void OnDestroy() { if (inst == this) inst = null; }
    }
}
