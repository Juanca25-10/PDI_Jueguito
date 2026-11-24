using System;
using CuandoNadieMira.Menu;
using TMPro;
using UnityEngine;

namespace CuandoNadieMira.Juego
{
    /// <summary>
    /// HUD del juego con la estética del menú. Lee el estado real del juego sin modificarlo:
    /// GameManager (fase, barra, teclas), OjosReceiver (lo que dice la red neuronal) y Estatua.Todas.
    /// Esta pieza coordina: objetivo, aviso para empezar el turno, carteles de fase, pérdida del rostro
    /// y las estadísticas del turno. Las demás piezas (HudOjos, HudAmanecer, HudControles, HudPausa, HudFinal)
    /// leen de aquí. Nada de lo que se muestra es inventado: si no hay dato, se dice.
    /// </summary>
    public class HudMuseo : MonoBehaviour
    {
        [Header("Partes")]
        [SerializeField] CanvasGroup root;
        [SerializeField] CanvasGroup objective;
        [SerializeField] TMP_Text objectiveText;
        [SerializeField] CanvasGroup prompt;
        [SerializeField] RectTransform promptBox;
        [SerializeField] TMP_Text promptKey;
        [SerializeField] CanvasGroup banner;
        [SerializeField] TMP_Text bannerTitle, bannerSub;
        [SerializeField] CanvasGroup faceVeil, faceLost, faceBack;
        [SerializeField] TMP_Text faceReason, faceAdvice;
        [SerializeField] CanvasGroup black;

        [Header("Franjas de cine")]
        [Tooltip("Material del efecto de película (propiedad _Barras = alto de cada franja negra, 0-1). El HUD se queda dentro de la imagen.")]
        [SerializeField] Material film;
        [SerializeField] RectTransform safeArea;
        [SerializeField] float safeMargin = 0f;

        [Header("Textos")]
        [SerializeField, TextArea] string objectiveExplore = "Recorre el museo. Cuando estés listo, ve al escritorio del guarda y empieza tu turno.";
        [SerializeField, TextArea] string objectivePlay = "Llega a las 06:00. Con los ojos cerrados la noche avanza, y ellas también. Ábrelos para que retrocedan.";
        [SerializeField] float objectiveFullSeconds = 12f;

        public static HudMuseo Instance { get; private set; }

        public GameManager Game => GameManager.Instancia;
        public OjosReceiver Eyes { get; private set; }
        public ControlJugador Player { get; private set; }
        public GameManager.Fase Phase { get; private set; }
        /// <summary>Segundos de juego (sin contar pausas) desde que empezó la fase actual.</summary>
        public float PhaseTime => Time.time - phaseStart;
        public bool Ready => Game != null && !IntroCinematica.Activa;
        public CanvasGroup Black => black;

        public bool KeyboardMode => Game != null && Game.modoTeclado;
        public bool HasFace => KeyboardMode || (Eyes != null && Eyes.HayRostro);
        /// <summary>Ojos cerrados según la red (o la tecla en modo teclado), sin el mínimo de duración.</summary>
        public bool ClosedRaw => KeyboardMode ? HudUtil.Pressed(Game.teclaCerrarOjos) : Eyes != null && Eyes.Cerrados;
        /// <summary>Segundos que llevan cerrados (red o tecla).</summary>
        public float ClosedSeconds => KeyboardMode ? keyClosed : Eyes != null ? Eyes.SegCerrados : 0f;
        /// <summary>Lo mismo que cuenta el GameManager como "cerrados": un parpadeo corto no cuenta.</summary>
        public bool ClosedCounted => ClosedRaw && ClosedSeconds >= Game.segCierreMinimo;

        // Estadísticas del turno (todas medidas, ninguna estimada)
        public float TurnSeconds { get; private set; }
        public float ClosedTotal { get; private set; }
        public int TurnBlinks => Eyes != null ? Eyes.Parpadeos - blinksAtStart : 0;
        public int Advances { get; private set; }
        public Estatua Culprit { get; private set; }

        public event Action<GameManager.Fase> PhaseChanged;

        float phaseStart, keyClosed, bannerTime = -99f, faceBackTimer;
        int blinksAtStart;
        bool started, lostDuringPlay, introSeen;

        void Awake()
        {
            Instance = this;
            HudUtil.Set(root, 0f);
            foreach (var g in new[] { objective, prompt, banner, faceVeil, faceLost, faceBack }) HudUtil.Set(g, 0f);
            HudUtil.Set(black, 0f);
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void OnEnable()
        {
            Estatua.LlegoAlFinal += OnReachedEnd;
            Estatua.Avanzo += OnAdvanced;
        }

        void OnDisable()
        {
            Estatua.LlegoAlFinal -= OnReachedEnd;
            Estatua.Avanzo -= OnAdvanced;
        }

        void Start()
        {
            Eyes = FindAnyObjectByType<OjosReceiver>();
            Player = FindAnyObjectByType<ControlJugador>();
            if (Game != null) Phase = Game.fase;
            phaseStart = Time.time;
        }

        void OnReachedEnd(Estatua e) { if (Phase == GameManager.Fase.Jugando) Culprit = e; }
        void OnAdvanced(Estatua e) { if (Phase == GameManager.Fase.Jugando) Advances++; }

        void Update()
        {
            var gm = Game;
            FitToFilm();
            if (gm == null) { HudUtil.Fade(root, 0f, 0.3f); return; }

            // Durante la intro el HUD no existe; después entra despacio
            // En pausa casi desaparece para que el menú de pausa se lea limpio
            float target = IntroCinematica.Activa ? 0f : HudPausa.Paused ? 0.12f : 1f;
            HudUtil.Fade(root, target, IntroCinematica.Activa || HudPausa.Paused ? 0.2f : 0.9f);

            keyClosed = KeyboardMode && HudUtil.Pressed(gm.teclaCerrarOjos) ? keyClosed + Time.deltaTime : 0f;

            if (!started || gm.fase != Phase) EnterPhase(gm.fase);
            if (!IntroCinematica.Activa && !introSeen) { introSeen = true; ShowBanner("El museo cierra", "Tu turno empieza en el escritorio del guarda."); }

            if (Phase == GameManager.Fase.Jugando)
            {
                TurnSeconds = PhaseTime;
                if (HasFace && ClosedCounted) ClosedTotal += Time.deltaTime;
            }

            bool playing = Phase == GameManager.Fase.Jugando, exploring = Phase == GameManager.Fase.Explorando;

            // Objetivo: completo al empezar la fase, luego discreto
            if (objectiveText != null) objectiveText.text = playing ? objectivePlay : objectiveExplore;
            HudUtil.Fade(objective, (playing || exploring) ? (PhaseTime < objectiveFullSeconds ? 1f : 0.5f) : 0f, 0.8f);

            // Aviso "E: comenzar el turno" o "E: escuchar la placa" junto a una estatua
            bool nearStart = exploring && Ready && NearStart();
            var placa = exploring && Ready && !nearStart ? PlacaEstatua.Cercana : null;
            bool near = nearStart || placa != null;
            SetPromptText(nearStart ? "Comenzar el turno" : placa == null ? null
                    : PlacaEstatua.Leyendo == placa ? "Dejar de escuchar" : "Escuchar la placa",
                nearStart ? "Tu puesto: el escritorio del guarda" : placa != null ? placa.Nombre : null);
            if (promptKey != null) promptKey.text = HudUtil.KeyName(gm.teclaInteractuar);
            HudUtil.Fade(prompt, near ? 1f : 0f, near ? 0.25f : 0.35f);
            if (promptBox != null)
                promptBox.anchoredPosition = new Vector2(0f, (1f - prompt.alpha) * -18f + Mathf.Sin(Time.unscaledTime * 2.2f) * 3f);

            // Cartel de fase
            float b = Time.unscaledTime - bannerTime;
            HudUtil.Fade(banner, Ready && b < 3.2f ? 1f : 0f, b < 3.2f ? 0.5f : 1f);

            UpdateFace(playing);
        }

        float lastBars = -1f, lastHeight;

        // Las franjas negras las pinta el renderer en todas las cámaras; el HUD (Overlay) iría encima, así que se encoge para no taparlas
        void FitToFilm()
        {
            if (safeArea == null) return;
            float bars = film != null && film.HasProperty("_Barras") ? Mathf.Clamp(film.GetFloat("_Barras"), 0f, 0.3f) : 0f;
            float h = ((RectTransform)transform).rect.height;
            if (Mathf.Approximately(bars, lastBars) && Mathf.Approximately(h, lastHeight)) return;
            lastBars = bars;
            lastHeight = h;
            float inset = bars * h + (bars > 0f ? safeMargin : 0f);
            safeArea.offsetMin = new Vector2(0f, inset);
            safeArea.offsetMax = new Vector2(0f, -inset);
        }

        void EnterPhase(GameManager.Fase f)
        {
            var previous = Phase;
            Phase = f;
            phaseStart = Time.time;
            if (f == GameManager.Fase.Jugando)
            {
                TurnSeconds = ClosedTotal = 0f;
                Advances = 0;
                Culprit = null;
                blinksAtStart = Eyes != null ? Eyes.Parpadeos : 0;
                ShowBanner("Comienza el turno", "Cierra los ojos. Que pase la noche.");
            }
            else if (f == GameManager.Fase.Explorando && started && previous != GameManager.Fase.Explorando)
                ShowBanner("Otra noche", "Vuelve al escritorio del guarda cuando estés listo.");
            started = true;
            PhaseChanged?.Invoke(f);
        }

        void ShowBanner(string title, string sub)
        {
            if (bannerTitle != null) bannerTitle.text = title;
            if (bannerSub != null) bannerSub.text = sub;
            bannerTime = Time.unscaledTime;
        }

        TMP_Text promptTitle, promptSub;

        void SetPromptText(string title, string sub)
        {
            if (title == null || promptBox == null) return;
            if (promptTitle == null) promptTitle = promptBox.Find("Texto")?.GetComponent<TMP_Text>();
            if (promptSub == null) promptSub = promptBox.Find("Sub")?.GetComponent<TMP_Text>();
            if (promptTitle != null && promptTitle.text != title) promptTitle.text = title;
            if (promptSub != null && promptSub.text != sub) promptSub.text = sub;
        }

        bool NearStart()
        {
            var gm = Game;
            if (gm.puntoInicio == null || Player == null) return true;
            return Vector3.Distance(Player.transform.position, gm.puntoInicio.position) <= gm.radioInicio;
        }

        // Sin rostro durante el turno la partida se pausa sola (lo hace el GameManager): aquí se explica por qué.
        void UpdateFace(bool playing)
        {
            bool lost = playing && Ready && !HasFace;
            if (lost)
            {
                lostDuringPlay = true;
                faceBackTimer = 0f;
                if (faceReason != null) faceReason.text = Reason();
                if (faceAdvice != null) faceAdvice.text = Advice();
            }
            else if (lostDuringPlay && playing)
            {
                lostDuringPlay = false;
                faceBackTimer = 1.8f;
            }
            if (!playing) lostDuringPlay = false;
            faceBackTimer -= Time.unscaledDeltaTime;

            HudUtil.Fade(faceVeil, lost ? 1f : 0f, 0.4f);
            HudUtil.Fade(faceLost, lost ? 1f : 0f, lost ? 0.3f : 0.25f);
            HudUtil.Fade(faceBack, !lost && faceBackTimer > 0f && (faceLost == null || faceLost.alpha < 0.05f) ? 1f : 0f, 0.3f);
        }

        /// <summary>Por qué no hay lectura, en palabras del jugador.</summary>
        public string Reason()
        {
            if (Eyes == null) return "No hay receptor de ojos en la escena.";
            if (Eyes.SegSinDatos > 600f) return "Encendiendo la cámara...";
            if (Eyes.SegSinDatos > Eyes.timeoutSinDatos) return "El detector de ojos no responde.";
            return TrackingText.ProblemFor(Eyes.Motivo);
        }

        public string Advice()
        {
            if (Eyes == null) return "";
            if (Eyes.SegSinDatos > 600f) return "Esto tarda unos segundos la primera vez.";
            if (Eyes.SegSinDatos > Eyes.timeoutSinDatos) return "Revisa que la cámara esté conectada y que ninguna otra aplicación la use.";
            return TrackingText.AdviceFor(Eyes.Motivo);
        }

        public void GoTo(string scene)
        {
            StartCoroutine(HudUtil.LoadScene(scene, black));
        }
    }
}
