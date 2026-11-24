using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

namespace CuandoNadieMira.Juego
{
    /// <summary>
    /// Subtítulos solo para la lectura de las placas. Parte el texto en frases y reparte la duración
    /// del audio según la longitud de cada una. Se dibuja dentro del canvas del HUD del museo.
    /// </summary>
    public class SubtitulosPlaca : MonoBehaviour
    {
        static readonly Dictionary<string, string> Textos = new()
        {
            ["placa_llorona"] = "La Llorona. Dicen que vaga por los ríos de noche, buscando a los hijos que ella misma perdió en el agua. " +
                                "Su lamento se oye lejos cuando está cerca... y cerca cuando ya es tarde. " +
                                "Los pescadores del Magdalena no miran atrás cuando la escuchan.",
            ["placa_mohan"] = "El Mohán. Un anciano de barba larga y ojos de fuego que vive en las cuevas bajo el río Magdalena. " +
                              "Protege los peces, enreda las redes... y se lleva a quien se acerca demasiado a la orilla. " +
                              "Dicen que todavía fuma tabaco en la oscuridad.",
        };

        static SubtitulosPlaca inst;
        TMP_Text texto;
        CanvasGroup grupo;
        Coroutine rutina;

        public static void Mostrar(string clip, float duracion)
        {
            if (duracion <= 0f || !Textos.TryGetValue(clip, out var t)) return;
            var s = Asegurar();
            if (s == null) return;
            if (s.rutina != null) s.StopCoroutine(s.rutina);
            s.rutina = s.StartCoroutine(s.Reproducir(Frases(t), duracion));
        }

        public static void Ocultar()
        {
            if (inst == null) return;
            if (inst.rutina != null) inst.StopCoroutine(inst.rutina);
            inst.rutina = null;
            inst.grupo.alpha = 0f;
            inst.texto.text = "";
        }

        static SubtitulosPlaca Asegurar()
        {
            if (inst != null) return inst;
            var hud = HudMuseo.Instance;
            var canvas = hud != null ? hud.GetComponentInParent<Canvas>() : null;
            if (canvas == null) canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null) return null;

            var go = new GameObject("SubtitulosPlaca", typeof(RectTransform), typeof(CanvasGroup));
            var rt = (RectTransform)go.transform;
            rt.SetParent(canvas.transform, false);
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 190f);
            rt.sizeDelta = new Vector2(1100f, 120f);
            rt.SetAsLastSibling();

            var fondo = new GameObject("Fondo", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            var frt = (RectTransform)fondo.transform;
            frt.SetParent(rt, false);
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
            frt.offsetMin = new Vector2(-24f, -10f); frt.offsetMax = new Vector2(24f, 10f);
            var img = fondo.GetComponent<UnityEngine.UI.Image>();
            img.color = new Color(0f, 0f, 0f, 0.55f);
            img.raycastTarget = false;

            var tgo = new GameObject("Texto", typeof(RectTransform), typeof(TextMeshProUGUI));
            var trt = (RectTransform)tgo.transform;
            trt.SetParent(rt, false);
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            var tmp = tgo.GetComponent<TextMeshProUGUI>();
            if (hud != null)
            {
                var otro = hud.GetComponentInChildren<TMP_Text>(true);
                if (otro != null) tmp.font = otro.font;
            }
            tmp.fontSize = 34f;
            tmp.color = new Color(0.93f, 0.9f, 0.84f);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.raycastTarget = false;

            inst = go.AddComponent<SubtitulosPlaca>();
            inst.texto = tmp;
            inst.grupo = go.GetComponent<CanvasGroup>();
            inst.grupo.alpha = 0f;
            inst.grupo.blocksRaycasts = false;
            return inst;
        }

        void OnDestroy() { if (inst == this) inst = null; }

        // Frases cortas: corta en punto y en "..." y, si una queda muy larga, en la coma más cercana a la mitad.
        static List<string> Frases(string t)
        {
            var res = new List<string>();
            foreach (Match m in Regex.Matches(t, @"[^.]+(\.\.\.|\.)?"))
            {
                string f = m.Value.Trim();
                if (f.Length == 0) continue;
                if (f.Length > 70)
                {
                    int mitad = f.Length / 2, mejor = -1;
                    for (int i = 0; i < f.Length; i++)
                        if (f[i] == ',' && (mejor < 0 || Mathf.Abs(i - mitad) < Mathf.Abs(mejor - mitad))) mejor = i;
                    if (mejor > 0) { res.Add(f.Substring(0, mejor + 1).Trim()); res.Add(f.Substring(mejor + 1).Trim()); continue; }
                }
                res.Add(f);
            }
            return res;
        }

        IEnumerator Reproducir(List<string> frases, float duracion)
        {
            int total = 0;
            foreach (var f in frases) total += f.Length;
            foreach (var f in frases)
            {
                texto.text = f;
                grupo.alpha = 1f;
                yield return new WaitForSeconds(duracion * f.Length / Mathf.Max(1, total));
            }
            grupo.alpha = 0f;
            texto.text = "";
            rutina = null;
        }
    }
}
