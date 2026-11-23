using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CuandoNadieMira.Menu
{
    /// <summary>
    /// "Preparando la experiencia", en dos fases con datos reales:
    ///   1. Comprobación: cámara conectada, calibración guardada y seguimiento listo (rostro estable).
    ///      Si algo falta lo dice y ofrece opciones; si el rostro se pierde, muestra "Se perdió el seguimiento".
    ///   2. Carga: apaga el detector del menú (el juego abre el suyo con LanzadorPython), carga la sala de forma
    ///      asíncrona con su progreso real y entra con un fundido.
    /// Las opciones de volver solo existen antes de la carga: una carga en espera bloquearía cualquier otra escena.
    /// </summary>
    public class LoadingController : MonoBehaviour
    {
        enum Phase { Checking, Loading, Entering, Leaving }

        [Header("Destino")]
        [SerializeField] string gameScene = MenuFlow.DefaultGameScene;

        [Header("Sistemas")]
        [SerializeField] EyeTrackingClient client;
        [SerializeField] SceneTransition transition;
        [SerializeField] TrackingLostPanel lostPanel;

        [Header("Progreso")]
        [SerializeField] TMP_Text progressLabel, progressValue;
        [SerializeField] Image progressFill;

        [Header("Comprobaciones")]
        [SerializeField] StatusChip cameraCheck, calibrationCheck, trackingCheck;
        [SerializeField] TMP_Text status, tip;

        [Header("Botones")]
        [SerializeField] MenuButton btnEnterAnyway, btnCalibrate, btnBack;

        [Header("Tiempos")]
        [SerializeField] float minimumShowTime = 4f;     // para que se alcancen a leer las reglas
        [SerializeField] float readyHold = 1.2f;         // todo listo durante este tiempo -> empieza la carga
        [SerializeField] float offerChoicesAfter = 8f;   // si algo falta tanto tiempo, ofrece opciones
        [SerializeField] float lostAfter = 3f;           // rostro perdido (habiéndolo visto) -> excepción

        static readonly string[] Tips =
        {
            "Consejo: vuelve a abrir los ojos rápidamente si sientes que algo se acerca.",
            "Consejo: busca una luz frontal suave; las sombras fuertes confunden al detector.",
            "Consejo: si sales del encuadre, el museo espera. Nada avanza sin que lo sepas.",
            "Consejo: parpadear es normal. Solo cuenta cuando cierras los ojos a propósito.",
            "Consejo: siéntate a un brazo de distancia de la cámara, con la cara centrada.",
        };

        Phase phase = Phase.Checking;
        AsyncOperation op;
        float shownProgress, readySince = -1f, waitingSince, noFaceSince = -1f, startTime;
        bool choicesShown, sawFace;
        int tipIndex;
        MenuButton[] visible = new MenuButton[0];

        void Awake()
        {
            btnEnterAnyway.onActivate.AddListener(() => BeginLoading());
            btnCalibrate.onActivate.AddListener(() => GoTo(MenuFlow.CalibrationScene));
            btnBack.onActivate.AddListener(() => GoTo(MenuFlow.MainMenuScene));
            lostPanel.RetryButton.onActivate.AddListener(() => GoTo(MenuFlow.CalibrationScene));
            lostPanel.MenuButton.onActivate.AddListener(() => GoTo(MenuFlow.MainMenuScene));
            lostPanel.Resumed += () => { noFaceSince = -1f; waitingSince = Time.unscaledTime; };
            ShowChoices(false);
        }

        IEnumerator Start()
        {
            startTime = waitingSince = Time.unscaledTime;
            tipIndex = Random.Range(0, Tips.Length);
            tip.text = Tips[tipIndex];
            StartCoroutine(RotateTips());
            progressFill.fillAmount = 0f;
            progressValue.text = "";
            if (transition != null) yield return transition.FadeIn(1.2f);
        }

        void Update()
        {
            switch (phase)
            {
                case Phase.Checking: UpdateChecking(); break;
                case Phase.Loading: UpdateLoading(); break;
            }
        }

        // ==========================================================================================
        // Fase 1: comprobaciones
        // ==========================================================================================

        void UpdateChecking()
        {
            var s = EyeTrackingService.Instance;
            bool cam = s != null && s.CameraConnected;
            bool calib = CalibrationStore.IsCalibrated;
            bool tracking = cam && s.HasFace && s.IsStable(1f);
            bool starting = s == null || s.State is TrackingState.Off or TrackingState.Starting;
            if (tracking) sawFace = true;

            cameraCheck.Set(cam ? ChipState.Ok : starting ? ChipState.Pending : ChipState.Warning,
                cam ? "Cámara conectada" : starting ? "Buscando cámara" : "Cámara no detectada");
            calibrationCheck.Set(calib ? ChipState.Ok : ChipState.Warning, calib ? "Calibración completada" : "Sin calibrar");
            trackingCheck.Set(tracking ? ChipState.Ok : cam ? ChipState.Pending : ChipState.Off,
                tracking ? "Seguimiento listo" : cam ? "Buscando tu rostro" : "Seguimiento en espera");

            progressLabel.text = "Comprobando cámara y calibración...";
            bool allReady = cam && calib && tracking;

            if (allReady)
            {
                if (readySince < 0f) readySince = Time.unscaledTime;
                status.text = "Todo listo. Abriendo la sala...";
                if (choicesShown) ShowChoices(false);
                waitingSince = Time.unscaledTime;
                if (Time.unscaledTime - readySince >= readyHold) BeginLoading();
                return;
            }

            readySince = -1f;
            status.text = !cam ? (starting ? "Encendiendo la cámara..." : TrackingText.Problem(s) + " " + TrackingText.Advice(s))
                : !calib ? "Falta calibrar tus ojos antes de entrar."
                : TrackingText.Problem(s) + " " + TrackingText.Advice(s);

            // Rostro perdido después de haberlo visto: pantalla de excepción
            if (cam && calib && sawFace && !lostPanel.IsOpen)
            {
                if (s.HasFace) noFaceSince = -1f;
                else if (noFaceSince < 0f) noFaceSince = Time.unscaledTime;
                else if (Time.unscaledTime - noFaceSince > lostAfter) { ShowChoices(false); lostPanel.Show(); }
            }

            bool offer = !calib || (!starting && Time.unscaledTime - waitingSince > offerChoicesAfter);
            if (offer && !choicesShown && !lostPanel.IsOpen) ShowChoices(true);

            if (!lostPanel.IsOpen)
            {
                if (choicesShown) MenuNav.KeepSelection(visible.FirstOrDefault());
                if (MenuNav.BackPressed) GoTo(MenuFlow.MainMenuScene);
            }
        }

        // ==========================================================================================
        // Fase 2: carga real de la sala
        // ==========================================================================================

        void BeginLoading()
        {
            if (phase != Phase.Checking) return;
            phase = Phase.Loading;
            if (lostPanel.IsOpen) lostPanel.Hide();
            ShowChoices(false);
            // El juego abre su propio detector: el del menú suelta la cámara y los puertos antes de cargarlo
            EyeTrackingService.Shutdown();
            op = SceneTransition.LoadAsync(gameScene);
            if (op == null)
            {
                phase = Phase.Checking;
                status.text = $"La sala «{gameScene}» no está en Build Settings.";
                return;
            }
            op.allowSceneActivation = false;
            MenuAudio.Instance?.Play(MenuSound.Select, 0.6f, 0f);
        }

        void UpdateLoading()
        {
            float target = Mathf.Clamp01(op.progress / 0.9f);
            // La barra nunca se adelanta a la carga real; solo suaviza el salto
            shownProgress = Mathf.MoveTowards(shownProgress, target, Time.unscaledDeltaTime * 0.9f);
            progressFill.fillAmount = shownProgress;
            progressValue.text = Mathf.FloorToInt(shownProgress * 100f) + "%";
            progressLabel.text = shownProgress >= 0.999f ? "Sala lista" : "Cargando sala...";
            status.text = shownProgress >= 0.999f ? "Entrando al museo..." : "Abriendo la sala. No cierres los ojos todavía.";

            if (shownProgress >= 0.999f && Time.unscaledTime - startTime >= minimumShowTime)
                StartCoroutine(Enter());
        }

        IEnumerator Enter()
        {
            if (phase != Phase.Loading) yield break;
            phase = Phase.Entering;
            MenuMusic.Instance?.FadeTo(0f, 1.2f);
            MenuAudio.Instance?.Play(MenuSound.Heartbeat, 1.2f, 0f);
            yield return transition.FadeOut(1.2f);
            op.allowSceneActivation = true;
        }

        // ==========================================================================================
        // Opciones y salida
        // ==========================================================================================

        void GoTo(string scene)
        {
            if (phase != Phase.Checking) return;
            phase = Phase.Leaving;
            if (lostPanel.IsOpen) lostPanel.Hide();
            ShowChoices(false);
            if (scene == MenuFlow.CalibrationScene) MenuFlow.Intent = FlowIntent.Play;
            StartCoroutine(transition.Play(scene, true, missing =>
            {
                phase = Phase.Checking;
                status.text = $"La sala «{missing}» aún no está abierta.";
            }));
        }

        void ShowChoices(bool on)
        {
            choicesShown = on;
            var all = new[] { btnEnterAnyway, btnCalibrate, btnBack };
            foreach (var b in all) b.gameObject.SetActive(on);
            visible = on ? all : new MenuButton[0];
            if (!on) return;

            float total = all.Sum(b => ((RectTransform)b.transform).sizeDelta.x) + 20f * (all.Length - 1);
            float x = -total * 0.5f;
            foreach (var b in all)
            {
                var rt = (RectTransform)b.transform;
                rt.anchoredPosition = new Vector2(x, 0f);
                x += rt.sizeDelta.x + 20f;
            }
            MenuNav.Wire(all, horizontal: true);
            MenuNav.Focus(CalibrationStore.IsCalibrated ? btnEnterAnyway : btnCalibrate);
        }

        IEnumerator RotateTips()
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(7f);
                for (float t = 0f; t < 0.5f; t += Time.unscaledDeltaTime) { tip.alpha = 1f - t / 0.5f; yield return null; }
                tipIndex = (tipIndex + 1) % Tips.Length;
                tip.text = Tips[tipIndex];
                for (float t = 0f; t < 0.5f; t += Time.unscaledDeltaTime) { tip.alpha = t / 0.5f; yield return null; }
                tip.alpha = 1f;
            }
        }
    }
}
