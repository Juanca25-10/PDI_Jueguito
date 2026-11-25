using CuandoNadieMira.Menu;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CuandoNadieMira.Juego
{
    /// <summary>
    /// Antes del turno, cerca de una estatua (radio de InfoEstatua), la tecla de interactuar hace que tu personaje
    /// lea la placa en voz alta (Voces/placa_llorona, placa_mohan, placa_madremonte). Otra vez la tecla, alejarse o empezar el turno la cortan.
    /// Se añade sola a cada estatua con InfoEstatua. Si todavía no existe el audio de una placa pero sí su texto,
    /// la placa se lee solo con subtítulos (a ritmo de lectura); en cuanto se agregue el audio, se usa.
    /// </summary>
    public class PlacaEstatua : MonoBehaviour
    {
        public static PlacaEstatua Cercana { get; private set; }
        public static PlacaEstatua Leyendo { get; private set; }

        Estatua estatua;
        InfoEstatua info;
        Transform jugador;
        string clip;
        bool conAudio;
        float finLectura;

        public string Nombre => estatua != null ? estatua.nombre : "";
        public bool TieneAudio => conAudio;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Instalar()
        {
            SceneManager.sceneLoaded += (s, m) => Poner(s);
            Poner(SceneManager.GetActiveScene());
        }

        static void Poner(Scene s)
        {
            if (s.name != MenuFlow.DefaultGameScene) return;
            foreach (var i in FindObjectsByType<InfoEstatua>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (i.GetComponent<PlacaEstatua>() == null) i.gameObject.AddComponent<PlacaEstatua>();
        }

        void Start()
        {
            estatua = GetComponent<Estatua>();
            info = GetComponent<InfoEstatua>();
            // Sin textos flotantes: la historia se escucha en la placa (InfoEstatua solo aporta su radio)
            if (info != null)
            {
                if (info.canvasInfo != null) info.canvasInfo.SetActive(false);
                info.enabled = false;
            }
            clip = ClipDe(Nombre);
            conAudio = Voz.Existe(clip);
            if (!conAudio && !SubtitulosPlaca.TieneTexto(clip)) clip = null;    // sin audio ni texto: no hay placa
        }

        static string ClipDe(string nombre)
        {
            string n = (nombre ?? "").ToLowerInvariant().Replace(" ", "");
            if (n.Contains("llorona")) return "placa_llorona";
            if (n.Contains("moh")) return "placa_mohan";
            if (n.Contains("madremonte")) return "placa_madremonte";
            return null;
        }

        void OnDisable()
        {
            if (Cercana == this) Cercana = null;
            if (Leyendo == this) Parar();
        }

        bool Sonando => conAudio ? Voz.Hablando : Time.time < finLectura;

        void Parar()
        {
            if (conAudio && Voz.Hablando) Voz.Callar();
            SubtitulosPlaca.Ocultar();
            Leyendo = null;
        }

        void Update()
        {
            var gm = GameManager.Instancia;
            if (estatua == null || info == null || gm == null) return;
            if (jugador == null)
            {
                var c = FindAnyObjectByType<ControlJugador>();
                if (c == null) return;
                jugador = c.transform;
            }

            bool explorando = gm.fase == GameManager.Fase.Explorando && !IntroCinematica.Activa && !HudPausa.Paused;
            float d = Vector3.Distance(jugador.position, estatua.Posicion);
            bool cerca = explorando && clip != null && d <= info.radio;

            if (cerca) { if (Cercana == null || Cercana == this || d < Cercana.Distancia(jugador)) Cercana = this; }
            else if (Cercana == this) Cercana = null;

            // Termina sola, o se corta al alejarse o al empezar el turno
            if (Leyendo == this && (!Sonando || !explorando || d > info.radio * 1.6f)) Parar();

            if (Cercana == this && HudUtil.Down(gm.teclaInteractuar))
            {
                if (Leyendo == this) Parar();
                else
                {
                    if (Leyendo != null) Leyendo.Parar();
                    Voz.Efecto("sfx_pagina_placa", estatua.Posicion, 0.8f, 8f);
                    float dur = conAudio ? Voz.Decir(clip) : SubtitulosPlaca.DuracionLectura(clip);
                    finLectura = Time.time + dur + (conAudio ? 0f : 0.6f);
                    SubtitulosPlaca.Mostrar(clip, dur);
                    Leyendo = this;
                }
            }
        }

        float Distancia(Transform j) => Vector3.Distance(j.position, estatua.Posicion);
    }
}
