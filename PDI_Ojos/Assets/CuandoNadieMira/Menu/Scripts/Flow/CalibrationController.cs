using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CuandoNadieMira.Menu
{
    /// <summary>
    /// Pantalla de Calibración. Ajusta el detector a los ojos de quien juega, con datos reales del detector:
    ///   1. Posición: el rostro tiene que verse bien y de forma estable durante 2 s.
    ///   2. Ojos abiertos: se mide P(cerrados) mirando al centro del marco.
    ///   3. Ojos cerrados: entre dos campanas se mide P(cerrados) con los ojos cerrados.
    ///   Luego calcula los umbrales personales, pide dos parpadeos de prueba y guarda el resultado
    ///   (<see cref="CalibrationStore"/>), que también usa el juego a través de OjosReceiver.
    /// Si el rostro se pierde a mitad de una toma, aparece "Se perdió el seguimiento".
    /// </summary>
    public class CalibrationController : MonoBehaviour
    {
        enum Phase { Idle, Position, OpenEyes, ClosedEyes, Verify, Done, Failed }

        [Header("Sistemas")]
        [SerializeField] EyeTrackingClient client;
        [SerializeField] SceneTransition transition;
        [SerializeField] TrackingLostPanel lostPanel;

        [Header("Cámara")]
        [SerializeField] CameraFeedView feed;
        [SerializeField] StatusChip faceChip, stableChip;
        [SerializeField] TMP_Text countdown;

        [Header("Pasos")]
        [SerializeField] TMP_Text stepLabel;
        [SerializeField] Image[] stepSegments;          // 3 barras de progreso de los pasos
        [SerializeField] CanvasGroup[] stepRows;        // 3 filas: número, icono y texto
        [SerializeField] TMP_Text[] stepNumbers;
        [SerializeField] Image[] stepChecks;            // marca de paso terminado
        [SerializeField] CanvasGroup stepsGroup;
        [SerializeField] TMP_Text instruction, hint, savedInfo;
        [SerializeField] Image phaseBar;
        [SerializeField] EyeMeter eyeMeter;

        [Header("Resultados")]
        [SerializeField] CanvasGroup resultsGroup;
        [SerializeField] TMP_Text resultsHeader, resOpen, resClosed, resQuality, resTest, resLive;

        [Header("Botones")]
        [SerializeField] RectTransform buttonsArea;
        [SerializeField] MenuButton btnStart, btnUseSaved, btnEnter, btnRepeat, btnBack, btnCancel, btnRetryCamera, btnSkipTest, btnCamera;

        [Header("Tiempos (segundos)")]
        [SerializeField] float positionHold = 2f;
        [SerializeField] float openCapture = 3f;
        [SerializeField] float closedCapture = 3f;
        [SerializeField] float settleTime = 0.6f;        // se descarta al inicio de cada toma (el ojo aún se está cerrando)
        [SerializeField] float lostAfter = 2.5f;         // sin rostro este tiempo durante una toma = seguimiento perdido
        [SerializeField] float verifyTimeout = 12f;
        [SerializeField] int verifyBlinks = 2;

        Phase phase = Phase.Idle;
        bool leaving;
        Coroutine routine;
        readonly List<float> openSamples = new(), closedSamples = new();
        readonly List<TrackingSample> fresh = new();
        long cursor;
        float noFaceSince = -1f;
        CalibrationStore.Result result;
        int blinksDone;
        float liveP = -1f;
        bool liveClosed;
        MenuButton[] allButtons;
        MenuButton[] visibleButtons = new MenuButton[0];

        EyeTrackingService Service => client != null ? client.Service : EyeTrackingService.Instance;

        // ==========================================================================================
        // Ciclo de vida
        // ==========================================================================================

        void Awake()
        {
            allButtons = new[] { btnStart, btnUseSaved, btnEnter, btnRepeat, btnBack, btnCancel, btnRetryCamera, btnSkipTest, btnCamera };
            btnStart.onActivate.AddListener(() => Begin(Phase.Position));
            btnUseSaved.onActivate.AddListener(() => Leave(MenuFlow.LoadingScene));
            btnEnter.onActivate.AddListener(() => Leave(MenuFlow.LoadingScene));
            btnRepeat.onActivate.AddListener(() => Begin(Phase.Position));
            btnBack.onActivate.AddListener(() => Leave(MenuFlow.MainMenuScene));
            btnCancel.onActivate.AddListener(Cancel);
            btnRetryCamera.onActivate.AddListener(() => { client.Restart(); ShowIdle(); });
            btnSkipTest.onActivate.AddListener(() => Finish());
            btnCamera.onActivate.AddListener(NextCamera);

            lostPanel.RetryButton.onActivate.AddListener(() => { lostPanel.Hide(); Begin(Phase.Position); });
            lostPanel.MenuButton.onActivate.AddListener(() => Leave(MenuFlow.MainMenuScene));
            lostPanel.Resumed += () => Begin(phase);   // repite la toma que se interrumpió

            if (countdown != null) countdown.alpha = 0f;
            if (resultsGroup != null) resultsGroup.alpha = 0f;
        }

        IEnumerator Start()
        {
            ShowIdle();
            if (transition != null) yield return transition.FadeIn(1.2f);
            Voz.Decir("cal_01_bienvenida");
        }

        int idleVoice;   // 1 = encendiendo, 2 = listo (para no repetir la frase)
        void IdleVoice(int v, string clip) { if (idleVoice == v) return; idleVoice = v; Voz.Encolar(clip); }

        void Update()
        {
            var s = Service;
            if (s != null)
            {
                fresh.Clear();
                s.ReadNew(ref cursor, fresh);
                foreach (var smp in fresh) if (smp.face) liveP = smp.p;
                if (!s.HasFace) liveP = -1f;
            }

            UpdateCameraPanel(s);
            if (phase == Phase.Idle) UpdateIdle(s);
            else if (phase is Phase.Position or Phase.OpenEyes or Phase.ClosedEyes or Phase.Verify) WatchTracking(s);
            if (phase == Phase.Done) UpdateLive();

            if (!lostPanel.IsOpen && !leaving)
            {
                MenuNav.KeepSelection(visibleButtons.FirstOrDefault());
                if (MenuNav.BackPressed) OnBack();
            }
        }

        void OnBack()
        {
            switch (phase)
            {
                case Phase.Idle:
                case Phase.Done: Leave(MenuFlow.MainMenuScene); break;
                default: Cancel(); break;
            }
        }

        // ==========================================================================================
        // Panel de cámara (siempre visible)
        // ==========================================================================================

        void UpdateCameraPanel(EyeTrackingService s)
        {
            bool face = s != null && s.HasFace;
            bool stable = s != null && s.IsStable();
            bool running = s != null && s.State == TrackingState.Running;

            faceChip.Set(face ? ChipState.Ok : running ? ChipState.Pending : ChipState.Off,
                face ? "Rostro detectado" : "Rostro no detectado");
            stableChip.Set(stable ? ChipState.Ok : s != null && s.CameraMissing ? ChipState.Warning : running ? ChipState.Pending : ChipState.Off,
                stable ? "Cámara estable" : s != null && s.CameraMissing ? "Sin cámara" : running ? "Señal inestable" : "Cámara apagada");

            feed.GuideTarget = face && s.Last.reason == "ok" ? (stable ? 1f : 0.7f) : 0f;
            feed.SetMessage(s == null ? "Encendiendo la cámara..." :
                s.CameraMissing || s.State != TrackingState.Running ? TrackingText.Problem(s) + "\n<size=70%>" + TrackingText.Advice(s) + "</size>" :
                "Esperando imagen de la cámara...");
        }

        // ==========================================================================================
        // Reposo: antes de empezar o con el detector en error
        // ==========================================================================================

        void ShowIdle()
        {
            StopRoutine();
            phase = Phase.Idle;
            MenuMusic.Instance?.FadeTo(1f, 1f);
            SetStepVisuals(-1, 0f);
            ShowResults(false);
            SetCountdown("");
            savedInfo.text = SavedSummary();
            RefreshIdleButtons(force: true);
        }

        bool idleError, idleCameras;

        void UpdateIdle(EyeTrackingService s)
        {
            bool error = s == null || s.State is TrackingState.MissingExe or TrackingState.PortBusy or TrackingState.ProcessExited or TrackingState.NoSignal || s.CameraMissing;
            if (error)
            {
                instruction.text = TrackingText.Problem(s);
                hint.text = TrackingText.Advice(s);
            }
            else if (s.State != TrackingState.Running)
            {
                instruction.text = "Encendiendo la cámara...";
                hint.text = "Esto tarda unos segundos la primera vez.";
                IdleVoice(1, "cal_02_encendiendo");
            }
            else
            {
                instruction.text = "Cuando estés listo, pulsa <b>Iniciar calibración</b>.";
                IdleVoice(2, "cal_03_listo");
                hint.text = s.HasFace ? "Ya te vemos. Ponte cómodo: la calibración dura menos de un minuto." : TrackingText.Advice(s);
            }
            phaseBar.fillAmount = 0f;
            eyeMeter.Set(liveP, "Lectura de tus ojos", CalibrationStore.IsCalibrated ? CalibrationStore.CloseThreshold : -1f,
                CalibrationStore.IsCalibrated ? CalibrationStore.OpenThreshold : -1f);

            // La lista de dispositivos se consulta cada 2 s, no en cada cuadro (es una llamada al sistema)
            if (Time.unscaledTime >= nextDevicePoll)
            {
                nextDevicePoll = Time.unscaledTime + 2f;
                deviceCount = WebCamTexture.devices.Length;
            }
            bool cameras = deviceCount > 1;
            if (error != idleError || cameras != idleCameras) RefreshIdleButtons();
        }

        float nextDevicePoll;
        int deviceCount;

        void RefreshIdleButtons(bool force = false)
        {
            var s = Service;
            idleError = s == null || s.State is TrackingState.MissingExe or TrackingState.PortBusy or TrackingState.ProcessExited or TrackingState.NoSignal || (s != null && s.CameraMissing);
            deviceCount = WebCamTexture.devices.Length;
            nextDevicePoll = Time.unscaledTime + 2f;
            idleCameras = deviceCount > 1;
            var list = new List<MenuButton>();
            if (idleError) list.Add(btnRetryCamera);
            list.Add(btnStart);
            if (CalibrationStore.IsCalibrated && MenuFlow.Intent == FlowIntent.Play) list.Add(btnUseSaved);
            if (idleCameras) { list.Add(btnCamera); UpdateCameraLabel(); }
            list.Add(btnBack);
            Layout(list.ToArray());
        }

        static string SavedSummary()
        {
            if (!CalibrationStore.IsCalibrated) return "Todavía no hay una calibración guardada en este equipo.";
            var d = CalibrationStore.Date;
            string when = d.HasValue ? d.Value.ToString("dd/MM/yyyy HH:mm") : "antes";
            return $"Última calibración: {when}  ·  calidad {CalibrationStore.QualityLabel(CalibrationStore.Quality).ToLower()}";
        }

        void NextCamera()
        {
            var devices = WebCamTexture.devices;
            if (devices.Length == 0) return;
            CalibrationStore.CameraIndex = (CalibrationStore.CameraIndex + 1) % devices.Length;
            UpdateCameraLabel();
            client.Restart();
        }

        void UpdateCameraLabel()
        {
            var devices = WebCamTexture.devices;
            int i = Mathf.Clamp(CalibrationStore.CameraIndex, 0, Mathf.Max(0, devices.Length - 1));
            string name = devices.Length > 0 ? devices[i].name : "-";
            if (name.Length > 26) name = name.Substring(0, 25) + "...";
            btnCamera.SetLabel("Cámara: " + name);
        }

        // ==========================================================================================
        // Pasos
        // ==========================================================================================

        void Begin(Phase p)
        {
            if (leaving) return;
            StopRoutine();
            if (lostPanel.IsOpen) lostPanel.Hide();
            noFaceSince = -1f;
            ShowResults(false);
            SetCountdown("");
            savedInfo.text = "";
            phase = p;
            routine = p switch
            {
                Phase.Position => StartCoroutine(PositionStep()),
                Phase.OpenEyes => StartCoroutine(OpenEyesStep()),
                Phase.ClosedEyes => StartCoroutine(ClosedEyesStep()),
                Phase.Verify => StartCoroutine(VerifyStep()),
                _ => null,
            };
        }

        void Cancel()
        {
            MenuAudio.Instance?.Play(MenuSound.Swish, 0.6f);
            ShowIdle();
        }

        void StopRoutine()
        {
            if (routine != null) StopCoroutine(routine);
            routine = null;
        }

        // Paso 1: rostro bien ubicado y estable durante positionHold segundos
        IEnumerator PositionStep()
        {
            SetStepVisuals(0, 0f);
            Layout(btnCancel);
            instruction.text = "Siéntate frente a la cámara y centra tu cara en la guía.";
            Voz.Decir("cal_04_centrar");
            bool saidGood = false;
            float held = 0f;
            while (held < positionHold)
            {
                var s = Service;
                bool good = s != null && s.HasFace && s.Last.reason == "ok" && s.IsStable(1f);
                held = good ? held + Time.unscaledDeltaTime : Mathf.Max(0f, held - Time.unscaledDeltaTime * 2f);
                if (good && !saidGood && held > 0.4f) { saidGood = true; Voz.Encolar("cal_05_bien"); }
                hint.text = good ? "Muy bien. Quédate así un momento..." : TrackingText.Problem(s) + " " + TrackingText.Advice(s);
                phaseBar.fillAmount = held / positionHold;
                SetStepVisuals(0, held / positionHold);
                eyeMeter.Set(liveP, "Lectura de tus ojos");
                yield return null;
            }
            Chime();
            Begin(Phase.OpenEyes);
        }

        // Paso 2: P(cerrados) con los ojos abiertos
        IEnumerator OpenEyesStep()
        {
            SetStepVisuals(1, 0f);
            Layout(btnCancel);
            openSamples.Clear();
            instruction.text = "Mira al centro del marco con los ojos <b>abiertos</b>.";
            hint.text = "Parpadea con normalidad. Solo mide unos segundos.";
            yield return Voz.Esperar(3f);
            Voz.Decir("cal_06_abiertos");
            yield return Voz.Esperar(6f);
            yield return Countdown(2, "Mira al centro");
            yield return Capture(openSamples, openCapture, 1, "Ojos abiertos");
            Chime();
            Begin(Phase.ClosedEyes);
        }

        // Paso 3: P(cerrados) con los ojos cerrados, entre dos campanas
        IEnumerator ClosedEyesStep()
        {
            SetStepVisuals(2, 0f);
            Layout(btnCancel);
            closedSamples.Clear();
            instruction.text = "Al sonar la campana, <b>cierra los ojos</b>. Ábrelos con la segunda campana.";
            hint.text = "Mantén la cabeza recta y quieta mientras tanto.";
            MenuMusic.Instance?.FadeTo(0.25f, 0.6f);
            Voz.Decir("cal_07_aviso");
            yield return Voz.Esperar(9f);
            yield return Countdown(3, "Prepárate");
            MenuAudio.Instance?.Play(MenuSound.Bell, 1.4f, 0f);
            Voz.Decir("cal_08_cierra");
            SetCountdown("Ojos cerrados");
            yield return Capture(closedSamples, closedCapture, 2, "Ojos cerrados");
            MenuAudio.Instance?.Play(MenuSound.Bell, 1.4f, 0f);
            Voz.Decir("cal_09_abre");
            SetCountdown("Abre los ojos");
            MenuMusic.Instance?.FadeTo(1f, 1.5f);
            yield return new WaitForSecondsRealtime(0.8f);
            SetCountdown("");

            result = CalibrationStore.Compute(openSamples, closedSamples);
            if (!result.valid) { ShowFailed(); yield break; }
            Begin(Phase.Verify);
        }

        /// <summary>Junta lecturas con rostro durante <paramref name="seconds"/> s efectivos (no cuenta el tiempo sin rostro).</summary>
        IEnumerator Capture(List<float> into, float seconds, int step, string meterLabel)
        {
            var s = Service;
            long c = s != null ? s.TotalSamples : 0;
            var buffer = new List<TrackingSample>();
            float effective = 0f;
            while (effective < seconds + settleTime)
            {
                s = Service;
                if (s == null) yield break;
                buffer.Clear();
                s.ReadNew(ref c, buffer);
                bool face = s.HasFace;
                if (face)
                {
                    effective += Time.unscaledDeltaTime;
                    if (effective > settleTime) into.AddRange(buffer.Where(b => b.face).Select(b => b.p));
                }
                float k = Mathf.Clamp01((effective - settleTime) / seconds);
                phaseBar.fillAmount = k;
                SetStepVisuals(step, k);
                eyeMeter.Set(liveP, meterLabel);
                yield return null;
            }
        }

        IEnumerator Countdown(int from, string caption)
        {
            for (int i = from; i > 0; i--)
            {
                SetCountdown(i.ToString(), caption);
                MenuAudio.Instance?.Play(MenuSound.Tick, 1.2f, 0f);
                for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime)
                {
                    eyeMeter.Set(liveP, "Lectura de tus ojos");
                    yield return null;
                }
            }
            SetCountdown("");
        }

        // Prueba: con los umbrales nuevos, el detector tiene que ver dos parpadeos
        IEnumerator VerifyStep()
        {
            SetStepVisuals(3, 0f);
            Layout(btnSkipTest, btnCancel);
            blinksDone = 0;
            instruction.text = $"Prueba final: <b>parpadea {verifyBlinks} veces</b>, despacio.";
            Voz.Encolar("cal_10_parpadea");
            float smoothed = -1f;
            bool closed = false;
            float start = Time.unscaledTime;
            var s = Service;
            long c = s != null ? s.TotalSamples : 0;
            var buffer = new List<TrackingSample>();
            while (blinksDone < verifyBlinks && Time.unscaledTime - start < verifyTimeout)
            {
                s = Service;
                if (s == null) yield break;
                buffer.Clear();
                s.ReadNew(ref c, buffer);
                foreach (var b in buffer)
                {
                    if (!b.face) continue;
                    smoothed = smoothed < 0f ? b.p : 0.5f * b.p + 0.5f * smoothed;      // igual que OjosReceiver
                    if (!closed && smoothed >= result.closeThreshold) closed = true;
                    else if (closed && smoothed <= result.openThreshold)
                    {
                        closed = false;
                        blinksDone++;
                        MenuAudio.Instance?.Play(MenuSound.Hover, 1.3f, 0f);
                    }
                }
                float left = verifyTimeout - (Time.unscaledTime - start);
                hint.text = $"Parpadeos detectados: {blinksDone} de {verifyBlinks}   ·   {Mathf.CeilToInt(left)} s";
                phaseBar.fillAmount = blinksDone / (float)verifyBlinks;
                SetStepVisuals(3, blinksDone / (float)verifyBlinks);
                eyeMeter.Set(liveP, closed ? "Ojos: cerrados" : "Ojos: abiertos", result.closeThreshold, result.openThreshold);
                yield return null;
            }
            yield return new WaitForSecondsRealtime(0.4f);
            Finish();
        }

        void Finish()
        {
            StopRoutine();
            CalibrationStore.Save(result, blinksDone);
            phase = Phase.Done;
            Chime();
            SetStepVisuals(4, 1f);
            ShowResults(true);
            resultsHeader.text = "CALIBRACIÓN COMPLETADA";
            resOpen.text = Pct(result.meanOpen);
            resClosed.text = Pct(result.meanClosed);
            resQuality.text = CalibrationStore.QualityLabel(result.quality);
            resTest.text = blinksDone >= verifyBlinks ? $"{blinksDone} de {verifyBlinks}  ·  superada"
                : blinksDone > 0 ? $"{blinksDone} de {verifyBlinks}  ·  repite si el juego no te responde bien" : "omitida";
            instruction.text = "Listo. El museo ya conoce tu mirada.";
            Voz.Decir("cal_11_completado");
            if (result.quality == CalibrationQuality.Aceptable) Voz.Encolar("cal_12_poca_luz");
            hint.text = result.quality == CalibrationQuality.Aceptable
                ? "La calidad es justa: con más luz sobre tu cara el juego te leerá mejor."
                : "Los umbrales se guardaron y el juego los usará.";

            Layout(MenuFlow.Intent == FlowIntent.CalibrateOnly
                ? new[] { btnBack, btnEnter, btnRepeat }
                : new[] { btnEnter, btnRepeat, btnBack });
        }

        void ShowFailed()
        {
            StopRoutine();
            phase = Phase.Failed;
            MenuAudio.Instance?.Play(MenuSound.Snuff, 1f);
            SetStepVisuals(2, 0f);
            instruction.text = "No pudimos calibrar tus ojos.";
            Voz.Decir("cal_13_fallo");
            hint.text = result.problem;
            phaseBar.fillAmount = 0f;
            Layout(btnRepeat, btnBack);
        }

        void UpdateLive()
        {
            // Estado en vivo con los umbrales recién guardados
            if (liveP >= 0f)
            {
                if (!liveClosed && liveP >= result.closeThreshold) liveClosed = true;
                else if (liveClosed && liveP <= result.openThreshold) liveClosed = false;
            }
            string state = liveP < 0f ? "sin rostro" : liveClosed ? "cerrados" : "abiertos";
            resLive.text = "Ahora mismo: ojos " + state;
            eyeMeter.Set(liveP, "Lectura de tus ojos", result.closeThreshold, result.openThreshold);
        }

        // Seguimiento perdido a mitad de una toma: pausa y muestra la pantalla de excepción
        void WatchTracking(EyeTrackingService s)
        {
            if (lostPanel.IsOpen || phase == Phase.Position) return;
            bool face = s != null && s.HasFace;
            bool broken = s == null || s.State is TrackingState.ProcessExited or TrackingState.NoSignal || s.CameraMissing;
            if (face && !broken) { noFaceSince = -1f; return; }
            if (noFaceSince < 0f) noFaceSince = Time.unscaledTime;
            if (broken || Time.unscaledTime - noFaceSince >= lostAfter)
            {
                StopRoutine();
                SetCountdown("");
                MenuMusic.Instance?.FadeTo(1f, 1f);
                lostPanel.Show();
                Voz.Decir("cal_14_perdido");
            }
        }

        // ==========================================================================================
        // Salida
        // ==========================================================================================

        void Leave(string scene)
        {
            if (leaving) return;
            leaving = true;
            StopRoutine();
            if (lostPanel.IsOpen) lostPanel.Hide();
            Layout();
            StartCoroutine(LeaveRoutine(scene));
        }

        IEnumerator LeaveRoutine(string scene)
        {
            yield return transition.Play(scene, true, missing =>
            {
                instruction.text = $"La sala «{missing}» aún no está abierta.";
                hint.text = "Agrega la escena a Build Settings.";
            });
            // Solo se llega aquí si la escena no existe
            leaving = false;
            ShowIdle();
        }

        // ==========================================================================================
        // Visuales
        // ==========================================================================================

        /// <param name="active">Paso activo (0..2), 3 = prueba final, 4 = terminado, -1 = ninguno.</param>
        void SetStepVisuals(int active, float progress)
        {
            int shownStep = Mathf.Clamp(active, 0, 2);
            stepLabel.text = active < 0 ? "Paso 1 de 3" : active == 3 ? "Paso 3 de 3  ·  prueba" : active >= 4 ? "Completado" : $"Paso {shownStep + 1} de 3";
            for (int i = 0; i < 3; i++)
            {
                bool done = active > i;
                bool current = active == i;
                stepSegments[i].fillAmount = done ? 1f : current ? Mathf.Clamp01(progress) : 0f;
                stepRows[i].alpha = active < 0 ? 0.85f : current ? 1f : done ? 0.75f : 0.4f;
                stepChecks[i].enabled = done;
                stepNumbers[i].enabled = !done;
            }
        }

        void ShowResults(bool on)
        {
            stepsGroup.alpha = on ? 0f : 1f;
            resultsGroup.alpha = on ? 1f : 0f;
            liveClosed = false;
        }

        void SetCountdown(string big, string caption = null)
        {
            if (countdown == null) return;
            countdown.text = string.IsNullOrEmpty(big) ? "" : caption != null ? $"{big}\n<size=30%>{caption}</size>" : big;
            countdown.alpha = string.IsNullOrEmpty(big) ? 0f : 1f;
        }

        void Chime() => MenuAudio.Instance?.Play(MenuSound.Select, 0.7f, 0f);

        static string Pct(float p) => Mathf.RoundToInt(p * 100f) + " %";

        /// <summary>Muestra solo estos botones, apilados y centrados, y enlaza su navegación.</summary>
        void Layout(params MenuButton[] visible)
        {
            foreach (var b in allButtons) if (b != null) b.gameObject.SetActive(false);
            // La acción principal va sola y centrada; las demás, en una fila debajo (así nunca bajan hasta las franjas)
            const float gap = 14f, rowGap = 20f;
            float rowWidth = -rowGap, rowHeight = 0f;
            for (int i = 1; i < visible.Length; i++)
            {
                var size = ((RectTransform)visible[i].transform).sizeDelta;
                rowWidth += size.x + rowGap;
                rowHeight = Mathf.Max(rowHeight, size.y);
            }
            float x = -rowWidth * 0.5f;
            for (int i = 0; i < visible.Length; i++)
            {
                var b = visible[i];
                b.gameObject.SetActive(true);
                var rt = (RectTransform)b.transform;
                if (i == 0)
                {
                    rt.anchoredPosition = new Vector2(-rt.sizeDelta.x * 0.5f, 0f);
                    continue;
                }
                float y = -(((RectTransform)visible[0].transform).sizeDelta.y * 0.5f + gap + rowHeight * 0.5f);
                rt.anchoredPosition = new Vector2(x, y);
                x += rt.sizeDelta.x + rowGap;
            }
            visibleButtons = visible;
            MenuNav.Wire(visible);
            if (visible.Length > 0 && !visible.Any(b => b.gameObject == MenuNav.Selected)) MenuNav.Focus(visible[0]);
        }
    }
}
