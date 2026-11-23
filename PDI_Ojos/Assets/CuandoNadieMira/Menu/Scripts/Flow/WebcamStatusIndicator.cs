using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CuandoNadieMira.Menu
{
    /// <summary>
    /// Indicador "Cámara: conectada / no detectada" (esquina superior derecha). Nunca inventa el estado:
    /// en el menú principal consulta <c>WebCamTexture.devices</c> al iniciar y cada 2 s; en Calibración y
    /// Preparando la experiencia usa lo que reporta el detector (que de verdad recibe imágenes de la cámara).
    /// </summary>
    public class WebcamStatusIndicator : MonoBehaviour
    {
        public enum Source { SystemDevices, Detector }

        [SerializeField] Source source = Source.SystemDevices;
        [SerializeField] TMP_Text label;
        [SerializeField] Image dot;
        [SerializeField] float pollSeconds = 2f;

        /// <summary>Hay al menos una cámara (según la fuente).</summary>
        public static bool AnyCameraConnected { get; private set; }
        public bool Connected { get; private set; }
        public string DeviceName { get; private set; } = "";

        enum Shown { Unknown, Connected, Missing, Starting }
        Shown shown = Shown.Unknown;

        public void Configure(Source src, TMP_Text text, Image statusDot)
        {
            source = src; label = text; dot = statusDot;
        }

        IEnumerator Start()
        {
            while (true)
            {
                if (source == Source.SystemDevices) PollDevices();
                yield return new WaitForSecondsRealtime(source == Source.SystemDevices ? pollSeconds : 0.25f);
            }
        }

        void PollDevices()
        {
            var devices = WebCamTexture.devices;
            Connected = devices != null && devices.Length > 0;
            AnyCameraConnected = Connected;
            DeviceName = Connected ? devices[0].name : "";
            Show(Connected ? Shown.Connected : Shown.Missing);
        }

        void Update()
        {
            if (source == Source.Detector)
            {
                var s = EyeTrackingService.Instance;
                if (s == null || s.State is TrackingState.Off or TrackingState.Starting) Show(Shown.Starting);
                else
                {
                    Connected = s.CameraConnected;
                    AnyCameraConnected = Connected;
                    if (testVideo != s.UsingTestVideo) { testVideo = s.UsingTestVideo; shown = Shown.Starting; }
                    Show(Connected ? Shown.Connected : Shown.Missing);
                }
            }

            if (dot == null) return;
            // Conectada: punto blanco fijo. No detectada: gris que parpadea. Buscando: late suave.
            float t = Time.unscaledTime;
            dot.color = shown switch
            {
                Shown.Connected => new Color(1f, 1f, 1f, 0.95f),
                Shown.Missing => new Color(0.6f, 0.6f, 0.6f, Mathf.Round(Mathf.Repeat(t * 1.6f, 1f)) * 0.85f + 0.1f),
                _ => new Color(0.8f, 0.8f, 0.8f, 0.3f + 0.3f * Mathf.Sin(t * 3f)),
            };
        }

        bool testVideo;

        void Show(Shown s)
        {
            if (s == shown || label == null) { shown = s; return; }
            shown = s;
            label.text = s switch
            {
                Shown.Connected => testVideo ? "Cámara: video de prueba" : "Cámara: conectada",
                Shown.Missing => "Cámara: no detectada",
                _ => "Cámara: buscando...",
            };
        }
    }
}
