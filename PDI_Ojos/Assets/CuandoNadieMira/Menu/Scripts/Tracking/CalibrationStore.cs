using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace CuandoNadieMira.Menu
{
    public enum CalibrationQuality { Insuficiente, Aceptable, Buena, Excelente }

    /// <summary>
    /// Resultado de la calibración guardado en PlayerPrefs. <c>OjosReceiver</c> (el detector del juego) lee
    /// las mismas claves al iniciar, así los umbrales personales también se usan durante la partida.
    /// </summary>
    public static class CalibrationStore
    {
        // Claves compartidas con OjosReceiver.cs y LanzadorPython.cs: si cambias una, cámbiala allí también
        public const string KeyDone = "cnm.calib.ok";
        public const string KeyCloseThreshold = "cnm.calib.umbralCierre";
        public const string KeyOpenThreshold = "cnm.calib.umbralApertura";
        public const string KeyCamera = "cnm.camara";
        const string KeyMeanOpen = "cnm.calib.pAbiertos";
        const string KeyMeanClosed = "cnm.calib.pCerrados";
        const string KeyQuality = "cnm.calib.calidad";
        const string KeyDate = "cnm.calib.fecha";
        const string KeyBlinks = "cnm.calib.parpadeos";

        public struct Result
        {
            public bool valid;
            public float meanOpen, meanClosed;          // P(cerrados) media con ojos abiertos y con ojos cerrados
            public float openHigh, closedLow;           // percentiles usados para los umbrales
            public float closeThreshold, openThreshold; // histéresis para OjosReceiver
            public CalibrationQuality quality;
            public int openSamples, closedSamples;
            public string problem;                      // por qué no sirve, si no sirve
        }

        public static bool IsCalibrated => PlayerPrefs.GetInt(KeyDone, 0) == 1;
        public static float CloseThreshold => PlayerPrefs.GetFloat(KeyCloseThreshold, 0.6f);
        public static float OpenThreshold => PlayerPrefs.GetFloat(KeyOpenThreshold, 0.4f);
        public static float MeanOpen => PlayerPrefs.GetFloat(KeyMeanOpen, 0f);
        public static float MeanClosed => PlayerPrefs.GetFloat(KeyMeanClosed, 0f);
        public static CalibrationQuality Quality => (CalibrationQuality)PlayerPrefs.GetInt(KeyQuality, 0);
        public static int TestBlinks => PlayerPrefs.GetInt(KeyBlinks, 0);

        public static DateTime? Date
        {
            get
            {
                string s = PlayerPrefs.GetString(KeyDate, "");
                return DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var d) ? d : null;
            }
        }

        public static bool HasCameraChoice => PlayerPrefs.HasKey(KeyCamera);
        public static int CameraIndex
        {
            get => PlayerPrefs.GetInt(KeyCamera, 0);
            set { PlayerPrefs.SetInt(KeyCamera, Mathf.Max(0, value)); PlayerPrefs.Save(); }
        }

        public static void Save(Result r, int blinks)
        {
            PlayerPrefs.SetInt(KeyDone, 1);
            PlayerPrefs.SetFloat(KeyCloseThreshold, r.closeThreshold);
            PlayerPrefs.SetFloat(KeyOpenThreshold, r.openThreshold);
            PlayerPrefs.SetFloat(KeyMeanOpen, r.meanOpen);
            PlayerPrefs.SetFloat(KeyMeanClosed, r.meanClosed);
            PlayerPrefs.SetInt(KeyQuality, (int)r.quality);
            PlayerPrefs.SetInt(KeyBlinks, blinks);
            PlayerPrefs.SetString(KeyDate, DateTime.Now.ToString("o", CultureInfo.InvariantCulture));
            PlayerPrefs.Save();
        }

        public static void Clear()
        {
            foreach (var k in new[] { KeyDone, KeyCloseThreshold, KeyOpenThreshold, KeyMeanOpen, KeyMeanClosed, KeyQuality, KeyDate, KeyBlinks })
                PlayerPrefs.DeleteKey(k);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Calcula los umbrales personales a partir de P(cerrados) con los ojos abiertos y con los ojos cerrados.
        /// Usa percentiles (no medias) para que un parpadeo suelto durante la toma no mueva el resultado.
        /// </summary>
        public static Result Compute(IReadOnlyList<float> open, IReadOnlyList<float> closed)
        {
            var r = new Result { openSamples = open.Count, closedSamples = closed.Count };
            if (open.Count < 15) { r.problem = "Hubo muy pocas lecturas con los ojos abiertos."; return r; }
            if (closed.Count < 10) { r.problem = "Hubo muy pocas lecturas con los ojos cerrados."; return r; }

            r.meanOpen = open.Average();
            r.meanClosed = closed.Average();
            r.openHigh = Percentile(open, 0.85f);      // lo más alto que llega el ruido con ojos abiertos
            r.closedLow = Percentile(closed, 0.2f);    // lo más bajo que llega con ojos cerrados
            float gap = r.closedLow - r.openHigh;

            if (r.meanClosed - r.meanOpen < 0.15f || gap < 0.08f)
            {
                r.problem = r.meanClosed <= r.meanOpen + 0.05f
                    ? "No se notó diferencia entre ojos abiertos y cerrados. ¿Los cerraste al oír la campana?"
                    : "La diferencia entre ojos abiertos y cerrados es muy pequeña. Mejora la luz sobre tu cara y repite.";
                r.quality = CalibrationQuality.Insuficiente;
                return r;
            }

            // Histéresis dentro del hueco: cerrar exige más que abrir (evita que el estado tiemble)
            r.closeThreshold = Mathf.Clamp(Mathf.Lerp(r.openHigh, r.closedLow, 0.55f), 0.2f, 0.92f);
            r.openThreshold = Mathf.Clamp(Mathf.Lerp(r.openHigh, r.closedLow, 0.3f), 0.08f, r.closeThreshold - 0.06f);
            r.quality = gap >= 0.5f ? CalibrationQuality.Excelente : gap >= 0.3f ? CalibrationQuality.Buena : CalibrationQuality.Aceptable;
            r.valid = true;
            return r;
        }

        static float Percentile(IReadOnlyList<float> values, float q)
        {
            var sorted = values.OrderBy(v => v).ToArray();
            float idx = Mathf.Clamp01(q) * (sorted.Length - 1);
            int lo = Mathf.FloorToInt(idx), hi = Mathf.CeilToInt(idx);
            return Mathf.Lerp(sorted[lo], sorted[hi], idx - lo);
        }

        public static string QualityLabel(CalibrationQuality q) => q switch
        {
            CalibrationQuality.Excelente => "Excelente",
            CalibrationQuality.Buena => "Buena",
            CalibrationQuality.Aceptable => "Aceptable",
            _ => "Insuficiente",
        };
    }
}
