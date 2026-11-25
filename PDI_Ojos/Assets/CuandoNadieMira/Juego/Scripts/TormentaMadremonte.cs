using System.Collections;
using CuandoNadieMira.Menu;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CuandoNadieMira.Juego
{
    /// <summary>
    /// La mecánica propia de la Madremonte: la dueña del monte trae la tormenta consigo.
    /// Cada pose que avanza (con tus ojos cerrados) la tormenta crece, y cuando la haces retroceder se calma:
    ///   pose 0  en su sitio, serena            → sin tormenta
    ///   pose 1  gira la cabeza: te notó        → empieza a llover cerca, la bruma se espesa
    ///   pose 2  se agacha y avanza             → lluvia y viento fuertes, truenos, la noche avanza más lento
    ///   final   te alcanza                     → derrota
    /// Efecto en el juego: mientras la tormenta está alta, el amanecer avanza más lento (RitmoAmanecer),
    /// así que conviene hacerla retroceder aunque esté lejos. El HUD lo dice ("La tormenta retrasa el amanecer").
    /// Se instala sola en la escena del juego si hay una estatua cuyo nombre contiene "madre".
    /// Todos los sonidos salen de Audio/Resources/Efectos (amb_tormenta_monte, mm_trueno), hechos con audios del proyecto.
    /// </summary>
    public class TormentaMadremonte : MonoBehaviour
    {
        /// <summary>0 = sin tormenta, 1 = la Madremonte a un paso de alcanzarte.</summary>
        public static float Nivel { get; private set; }
        /// <summary>Multiplica lo que avanza la noche con los ojos cerrados (1 = normal).</summary>
        public static float RitmoAmanecer => 1f - frenoMaximo * Nivel;

        const float frenoMaximo = 0.35f;              // con la tormenta al máximo, la noche avanza al 65 %
        const float volumenMaximo = 0.6f;
        const float brumaExtra = 1.4f;                // la densidad de la niebla llega a (1 + brumaExtra) veces la normal

        Estatua madre;
        AudioSource lluvia;
        float brumaBase = -1f, sigTrueno;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Instalar()
        {
            SceneManager.sceneLoaded += (s, m) => Poner(s);
            Poner(SceneManager.GetActiveScene());
        }

        static void Poner(Scene s)
        {
            Nivel = 0f;
            if (s.name != MenuFlow.DefaultGameScene || FindAnyObjectByType<TormentaMadremonte>() != null) return;
            var go = new GameObject("TormentaMadremonte");
            SceneManager.MoveGameObjectToScene(go, s);
            go.AddComponent<TormentaMadremonte>();
        }

        public static bool EsMadremonte(Estatua e) => e != null && (e.nombre ?? "").ToLowerInvariant().Replace(" ", "").Contains("madremonte");

        void OnEnable() { Estatua.Avanzo += AlAvanzar; }
        void OnDisable()
        {
            Estatua.Avanzo -= AlAvanzar;
            Nivel = 0f;
            if (brumaBase > 0f) RenderSettings.fogDensity = brumaBase;
        }

        void Start()
        {
            var clip = Voz.Clip("amb_tormenta_monte");
            if (clip == null) return;
            lluvia = gameObject.AddComponent<AudioSource>();
            lluvia.clip = clip;
            lluvia.loop = true;
            lluvia.spatialBlend = 0f;
            lluvia.volume = 0f;
            lluvia.playOnAwake = false;
        }

        void Update()
        {
            if (madre == null)
            {
                foreach (var e in Estatua.Todas) if (EsMadremonte(e)) { madre = e; break; }
                if (madre == null) return;
            }
            var gm = GameManager.Instancia;
            bool jugando = gm != null && gm.fase == GameManager.Fase.Jugando && !IntroCinematica.Activa;
            bool fin = gm != null && gm.fase == GameManager.Fase.Derrota;

            // Objetivo: la fracción del camino que lleva (pose / última pose). Sube despacio y se calma un poco más rápido.
            float objetivo = jugando || fin ? (float)madre.PoseActual / Mathf.Max(1, madre.PoseFinal) : 0f;
            float vel = objetivo > Nivel ? 0.22f : 0.35f;
            Nivel = Mathf.MoveTowards(Nivel, objetivo, vel * Time.deltaTime);

            // Lluvia y viento
            if (lluvia != null)
            {
                float v = Nivel * Nivel * volumenMaximo * Voz.Volumen * (HudPausa.Paused ? 0.35f : 1f);
                lluvia.volume = Mathf.MoveTowards(lluvia.volume, v, Time.unscaledDeltaTime * 0.5f);
                if (lluvia.volume > 0.001f && !lluvia.isPlaying) { lluvia.time = Random.Range(0f, lluvia.clip.length * 0.9f); lluvia.Play(); }
                else if (lluvia.volume <= 0.001f && lluvia.isPlaying) lluvia.Stop();
            }

            // Bruma: se lee la densidad normal la primera vez (la pone NieblaJuego al empezar)
            if (brumaBase < 0f && RenderSettings.fog) brumaBase = RenderSettings.fogDensity;
            if (brumaBase > 0f) RenderSettings.fogDensity = brumaBase * (1f + brumaExtra * Nivel);

            // Truenos lejanos de vez en cuando con la tormenta alta
            if (jugando && Nivel > 0.55f && Time.time >= sigTrueno)
            {
                sigTrueno = Time.time + Random.Range(14f, 26f);
                Trueno(0.35f + 0.3f * Nivel);
            }
        }

        void AlAvanzar(Estatua e)
        {
            if (e != madre || e == null || e.PoseActual >= e.PoseFinal) return;
            if (e.PoseActual >= 2) { sigTrueno = Time.time + Random.Range(12f, 20f); StartCoroutine(Despues(Random.Range(0.8f, 1.6f), 0.75f)); }
        }

        IEnumerator Despues(float seg, float vol)
        {
            yield return new WaitForSeconds(seg);
            Trueno(vol);
        }

        void Trueno(float vol)
        {
            // Desde lejos, del lado de la Madremonte, para que se sepa de dónde viene la tormenta
            Vector3? pos = null;
            var cam = Camera.main;
            if (madre != null && cam != null)
            {
                Vector3 dir = madre.Posicion - cam.transform.position;
                dir.y = 0f;
                pos = cam.transform.position + dir.normalized * 18f + Vector3.up * 6f;
            }
            var s = Voz.Efecto("mm_trueno", pos, vol, 80f);
            if (s != null) { s.spatialBlend = 0.55f; s.minDistance = 20f; }
        }
    }
}
