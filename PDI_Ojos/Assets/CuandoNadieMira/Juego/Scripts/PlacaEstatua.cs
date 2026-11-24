using CuandoNadieMira.Menu;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CuandoNadieMira.Juego
{
    /// <summary>
    /// Antes del turno, cerca de una estatua (radio de InfoEstatua), la tecla de interactuar hace que tu personaje
    /// lea la placa en voz alta (Voces/placa_llorona, placa_mohan...). Otra vez la tecla, alejarse o empezar el turno la cortan.
    /// Se añade sola a cada estatua con InfoEstatua; si no existe el audio de la placa, no hace nada.
    /// </summary>
    public class PlacaEstatua : MonoBehaviour
    {
        public static PlacaEstatua Cercana { get; private set; }
        public static PlacaEstatua Leyendo { get; private set; }

        Estatua estatua;
        InfoEstatua info;
        Transform jugador;
        string clip;

        public string Nombre => estatua != null ? estatua.nombre : "";
        public bool TieneAudio => clip != null;

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
            string n = Nombre.ToLowerInvariant();
            string nombreClip = n.Contains("llorona") ? "placa_llorona" : n.Contains("moh") ? "placa_mohan" : null;
            clip = Voz.Existe(nombreClip) ? nombreClip : null;
        }

        void OnDisable()
        {
            if (Cercana == this) Cercana = null;
            if (Leyendo == this) { Leyendo = null; Voz.Callar(); SubtitulosPlaca.Ocultar(); }
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
            if (Leyendo == this && (!Voz.Hablando || !explorando || d > info.radio * 1.6f))
            {
                if (Voz.Hablando) Voz.Callar();
                SubtitulosPlaca.Ocultar();
                Leyendo = null;
            }

            if (Cercana == this && HudUtil.Down(gm.teclaInteractuar))
            {
                if (Leyendo == this) { Voz.Callar(); SubtitulosPlaca.Ocultar(); Leyendo = null; }
                else
                {
                    Voz.Efecto("sfx_pagina_placa", estatua.Posicion, 0.8f, 8f);
                    SubtitulosPlaca.Mostrar(clip, Voz.Decir(clip));
                    Leyendo = this;
                }
            }
        }

        float Distancia(Transform j) => Vector3.Distance(j.position, estatua.Posicion);
    }
}
