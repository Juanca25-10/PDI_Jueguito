using System.Collections;
using System.Collections.Generic;
using CuandoNadieMira.Menu;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CuandoNadieMira.Juego
{
    /// <summary>
    /// Sonido envolvente del museo (se instala solo en la escena del juego).
    /// Ambientes en bucle, ruidos lejanos alrededor del jugador y la voz de la Curadora en cada fase.
    /// Los susurros no son al azar: cada uno responde a lo que está pasando.
    ///   - Una estatua despierta            → se presenta ("¡Ay, mis hijos!", "El río me trajo hasta aquí").
    ///   - Avanza con tus ojos cerrados       → a veces te tienta a no abrirlos ("¿Los has visto?").
    ///   - Queda a un paso de alcanzarte      → te susurra de cerca ("No abras los ojos...", "Un poquito más cerca")
    ///                                          y, si sigues con los ojos cerrados, la Curadora avisa: "Detrás de ti".
    ///   - Retrocede porque la miras          → se lamenta o se burla ("¿Dónde están...?", "Te estoy viendo").
    ///   - Curadora: "Ellas no duermen" al inicio del turno, "Ya casi amanece... o no" cerca del final.
    ///   - La Madremonte no tiene voz: en su lugar suena el monte (mm_despierta, mm_tienta, mm_acecho, mm_retrocede),
    ///     y su tormenta la maneja TormentaMadremonte.
    /// Todo sale de Audio/Resources (ver Voz); si un archivo falta, simplemente no suena.
    /// </summary>
    public class AudioMuseo : MonoBehaviour
    {
        static readonly string[] Lejanos = { "sfx_madera_cruje", "sfx_puerta_lejana", "sfx_pasos_lejanos", "sfx_piedra_crujido" };

        class Lineas { public string despierta, tienta, acecho, retrocede; }

        readonly Dictionary<Estatua, bool> despierta = new();
        float sigLejano, sigRespiracion, ultimoSusurro = -99f, ultimoTienta = -99f, ultimoRetrocede = -99f, ultimoAviso = -99f, ultimoSinRostro = -99f;
        string ultimoLejano = "";
        GameManager.Fase fase;
        bool iniciado, introAvisada, sinRostroAntes, dijoNoDuermen, dijoAmanece;
        float inicioTurno;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Instalar()
        {
            SceneManager.sceneLoaded += (s, m) => Poner(s);
            Poner(SceneManager.GetActiveScene());
        }

        static void Poner(Scene s)
        {
            if (s.name != MenuFlow.DefaultGameScene || FindAnyObjectByType<AudioMuseo>() != null) return;
            var go = new GameObject("AudioMuseo");
            SceneManager.MoveGameObjectToScene(go, s);
            go.AddComponent<AudioMuseo>();
        }

        void OnEnable()
        {
            Estatua.Avanzo += AlAvanzar;
            Estatua.Retrocedio += AlRetroceder;
            Estatua.LlegoAlFinal += AlLlegar;
        }

        void OnDisable()
        {
            Estatua.Avanzo -= AlAvanzar;
            Estatua.Retrocedio -= AlRetroceder;
            Estatua.LlegoAlFinal -= AlLlegar;
        }

        void Start()
        {
            Ambiente("amb_museo_noche", 0.65f);
            Ambiente("amb_lluvia_lejana", 0.32f);
            Ambiente("sfx_agua_goteo", 0.22f);
            sigLejano = Time.time + Random.Range(10f, 20f);
            sigRespiracion = Time.time + 60f;
            Debug.Log("[Cuando Nadie Mira] Audio del museo: " + (Voz.Existe("sus_llorona_01") ? "voces y susurros cargados." : "no se encontraron las voces en Audio/Resources/Voces."));
        }

        void Ambiente(string clip, float vol)
        {
            var c = Voz.Clip(clip);
            if (c == null) return;
            var s = gameObject.AddComponent<AudioSource>();
            s.clip = c;
            s.loop = true;
            s.spatialBlend = 0f;
            s.volume = 0f;
            s.time = Random.Range(0f, c.length * 0.9f);
            s.Play();
            StartCoroutine(Subir(s, vol, 4f));
        }

        IEnumerator Subir(AudioSource s, float vol, float seg)
        {
            for (float t = 0f; t < seg && s != null; t += Time.deltaTime)
            {
                s.volume = Mathf.Lerp(0f, vol * Voz.Volumen, t / seg);
                yield return null;
            }
        }

        void Update()
        {
            var gm = GameManager.Instancia;
            if (gm == null) return;
            var hud = HudMuseo.Instance;

            if (!IntroCinematica.Activa && !introAvisada) { introAvisada = true; Voz.Decir("juego_01_cierre"); }
            if (!iniciado || gm.fase != fase)
            {
                fase = gm.fase;
                if (iniciado) CambioDeFase(fase);
                iniciado = true;
            }

            bool jugando = fase == GameManager.Fase.Jugando && !IntroCinematica.Activa;
            bool explorando = fase == GameManager.Fase.Explorando && !IntroCinematica.Activa;
            bool cerrados = Cerrados(hud);

            // Sin rostro durante el turno
            bool sinRostro = jugando && hud != null && !hud.HasFace;
            if (sinRostro && !sinRostroAntes && Time.time - ultimoSinRostro > 25f) { ultimoSinRostro = Time.time; Voz.Decir("juego_03_sin_rostro"); }
            sinRostroAntes = sinRostro;

            if (jugando)
            {
                // Una estatua despierta: se presenta desde su sitio
                foreach (var e in Estatua.Todas)
                {
                    bool activa = gm.progreso >= e.activaDesdeProgreso;
                    despierta.TryGetValue(e, out bool antes);
                    if (activa && !antes) StartCoroutine(Susurro(e, LineasDe(e)?.despierta, Mathf.Max(Random.Range(0.8f, 2f), inicioTurno + 6.5f - Time.time), forzar: true));   // después del aviso de la Curadora
                    despierta[e] = activa;
                }

                // La Curadora: al rato de empezar y cerca del amanecer
                if (!dijoNoDuermen && cerrados && Time.time - inicioTurno > 14f) { dijoNoDuermen = true; SusurroMuseo("sus_museo_01", hud); }
                if (!dijoAmanece && gm.progreso >= 85f && cerrados) { dijoAmanece = true; SusurroMuseo("sus_museo_03", hud); }

                // Respiración al oído, muy de vez en cuando, con los ojos cerrados
                if (cerrados && Time.time >= sigRespiracion && Random.value < 0.004f)
                {
                    sigRespiracion = Time.time + Random.Range(60f, 110f);
                    Voz.Efecto("sfx_respiracion_cerca", null, 0.55f);
                }
            }

            // Ruidos lejanos alrededor del jugador (más seguidos durante el turno)
            if ((explorando || jugando) && Time.time >= sigLejano)
            {
                sigLejano = Time.time + (jugando ? Random.Range(9f, 22f) : Random.Range(16f, 38f));
                string c = Elegir(Lejanos, ref ultimoLejano);
                if (c != null) Voz.Efecto(c, AlrededorDelJugador(hud, 6f, 14f), 0.7f, 30f);
            }
        }

        static bool Cerrados(HudMuseo hud) => hud != null && hud.HasFace && hud.ClosedRaw;

        void CambioDeFase(GameManager.Fase ahora)
        {
            switch (ahora)
            {
                case GameManager.Fase.Jugando:
                    Voz.Decir("juego_02_turno");
                    inicioTurno = Time.time;
                    dijoNoDuermen = dijoAmanece = false;
                    despierta.Clear();
                    break;
                case GameManager.Fase.Victoria:
                    Voz.Efecto("sfx_campana_amanecer", null, 0.9f);
                    StartCoroutine(Despues(2f, "juego_04_victoria"));
                    break;
                case GameManager.Fase.Derrota:
                    StartCoroutine(Despues(1.4f, "juego_05_derrota"));
                    break;
            }
        }

        IEnumerator Despues(float seg, string clip)
        {
            yield return new WaitForSeconds(seg);
            Voz.Decir(clip);
        }

        void AlAvanzar(Estatua e)
        {
            Voz.Efecto("sfx_piedra_moverse", e.Posicion, 0.9f, 30f);
            var l = LineasDe(e);
            if (l == null || e.PoseActual >= e.PoseFinal) return;
            var hud = HudMuseo.Instance;

            if (e.PoseActual == e.PoseFinal - 1)
            {
                // A un paso: te habla de cerca, y si sigues sin mirar, la Curadora te avisa
                StartCoroutine(Susurro(e, l.acecho, Random.Range(0.4f, 0.9f), forzar: true));
                if (Time.time - ultimoAviso > 25f) { ultimoAviso = Time.time; StartCoroutine(AvisoDetras(hud)); }
            }
            else if (Time.time - ultimoTienta > 14f && Random.value < 0.5f)
            {
                ultimoTienta = Time.time;
                StartCoroutine(Susurro(e, l.tienta, Random.Range(0.6f, 1.5f)));
            }
        }

        IEnumerator AvisoDetras(HudMuseo hud)
        {
            yield return new WaitForSeconds(3.2f);
            if (fase == GameManager.Fase.Jugando && Cerrados(hud)) SusurroMuseo("sus_museo_02", hud);
        }

        void AlRetroceder(Estatua e)
        {
            if (fase != GameManager.Fase.Jugando || Time.time - ultimoRetrocede < 10f || Random.value > 0.55f) return;
            ultimoRetrocede = Time.time;
            StartCoroutine(Susurro(e, LineasDe(e)?.retrocede, Random.Range(0.3f, 0.8f)));
        }

        void AlLlegar(Estatua e) => Voz.Efecto("sfx_golpe_terror", null, 1f);

        // Susurro desde la estatua: casi siempre audible (mezcla 3D/2D), con un mínimo de separación entre susurros
        IEnumerator Susurro(Estatua e, string clip, float espera, bool forzar = false)
        {
            if (string.IsNullOrEmpty(clip)) yield break;
            yield return new WaitForSeconds(espera);
            if (e == null || fase != GameManager.Fase.Jugando) yield break;
            if (!forzar && Time.time - ultimoSusurro < 4f) yield break;
            ultimoSusurro = Time.time;
            var s = Voz.Efecto(clip, e.Posicion + Vector3.up * 1.5f, 1f, 60f);
            if (s != null) { s.spatialBlend = 0.75f; s.minDistance = 8f; }
        }

        void SusurroMuseo(string clip, HudMuseo hud)
        {
            ultimoSusurro = Time.time;
            var s = Voz.Efecto(clip, AlrededorDelJugador(hud, 0.8f, 1.6f), 1f, 10f);
            if (s != null) { s.spatialBlend = 0.6f; s.minDistance = 3f; }
        }

        static Lineas LineasDe(Estatua e)
        {
            string n = (e.nombre ?? "").ToLowerInvariant();
            if (n.Contains("llorona"))
                return new Lineas { despierta = "sus_llorona_01", tienta = "sus_llorona_02", acecho = "sus_llorona_03", retrocede = "sus_llorona_04" };
            if (n.Contains("moh"))
                return new Lineas { despierta = "sus_mohan_01", tienta = null, acecho = "sus_mohan_03", retrocede = "sus_mohan_02" };
            // La Madremonte no habla: es el monte el que suena (crujidos, hojas, una respiración enorme), con reverberación
            if (TormentaMadremonte.EsMadremonte(e))
                return new Lineas { despierta = "mm_despierta", tienta = "mm_tienta", acecho = "mm_acecho", retrocede = "mm_retrocede" };
            return null;
        }

        static string Elegir(string[] opciones, ref string ultimo)
        {
            var validos = new List<string>();
            foreach (var o in opciones) if (Voz.Existe(o) && o != ultimo) validos.Add(o);
            if (validos.Count == 0) return null;
            ultimo = validos[Random.Range(0, validos.Count)];
            return ultimo;
        }

        static Vector3? AlrededorDelJugador(HudMuseo hud, float min, float max)
        {
            var j = hud != null && hud.Player != null ? hud.Player.transform : Camera.main != null ? Camera.main.transform : null;
            if (j == null) return null;
            Vector2 d = Random.insideUnitCircle.normalized * Random.Range(min, max);
            return j.position + new Vector3(d.x, Random.Range(0.2f, 2f), d.y);
        }
    }
}
