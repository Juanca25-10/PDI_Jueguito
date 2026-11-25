using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

namespace CuandoNadieMira.Juego
{
    /// <summary>
    /// Subtítulos solo para la lectura de las placas. Parte el texto en frases y reparte la duración
    /// del audio según la longitud de cada una. Se dibuja dentro del HUD del museo (en su zona segura, así que
    /// respeta las franjas de cine y se atenúa con el HUD en pausa), centrado justo encima del aviso "E · Escuchar la placa",
    /// lejos de la lectura de ojos (abajo a la izquierda) y de los controles (abajo a la derecha).
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
            ["placa_madremonte"] = "La Madremonte. Una mujer alta, vestida de hojas y musgo, con una corona de ramas sobre la cabeza. " +
                                   "Cuida los montes y los nacimientos de agua, y castiga a quien tala o ensucia los ríos. " +
                                   "Cuando se enoja llegan las tormentas... y quien se adentra en el monte ya no encuentra el camino de vuelta.",
        };

        const float CaracteresPorSegundo = 13.5f;     // ritmo de lectura cuando la placa todavía no tiene audio

        public static bool TieneTexto(string clip) => clip != null && Textos.ContainsKey(clip);

        /// <summary>Lo que tarda en leerse el texto de una placa sin audio (0 si no hay texto).</summary>
        public static float DuracionLectura(string clip)
        {
            if (!TieneTexto(clip)) return 0f;
            var frases = Frases(Textos[clip]);
            int total = 0;
            foreach (var f in frases) total += f.Length;
            return total / CaracteresPorSegundo + frases.Count * 0.35f;
        }

        static SubtitulosPlaca inst;
        TMP_Text texto;
        CanvasGroup grupo;
        Coroutine rutina;
        float alfaObjetivo;

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
            inst.alfaObjetivo = 0f;
        }

        static SubtitulosPlaca Asegurar()
        {
            if (inst != null) return inst;
            var hud = HudMuseo.Instance;
            // Dentro de "Contenido" (la zona segura del HUD); si no existe, en el canvas
            Transform padre = hud != null ? hud.transform.Find("Contenido") : null;
            if (padre == null)
            {
                var canvas = hud != null ? hud.GetComponentInParent<Canvas>() : FindAnyObjectByType<Canvas>();
                if (canvas == null) return null;
                padre = canvas.transform;
            }

            var go = new GameObject("SubtitulosPlaca", typeof(RectTransform), typeof(CanvasGroup));
            go.layer = padre.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(padre, false);
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            // El aviso de la placa está centrado en y = 250 (caja de 96 de alto): los subtítulos van encima, sin tocarlo.
            // 880 de ancho: no llega a la lectura de ojos (termina en x = 622) ni a los controles (empiezan en x = 1464).
            rt.anchoredPosition = new Vector2(0f, 322f);
            rt.sizeDelta = new Vector2(880f, 92f);
            rt.SetAsLastSibling();

            var fondo = new GameObject("Fondo", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            var frt = (RectTransform)fondo.transform;
            frt.SetParent(rt, false);
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
            frt.offsetMin = new Vector2(-28f, -10f); frt.offsetMax = new Vector2(28f, 10f);
            var img = fondo.GetComponent<UnityEngine.UI.Image>();
            img.color = new Color(0f, 0f, 0f, 0.62f);
            img.raycastTarget = false;
            // Mismo fondo y marco fino que el aviso de la placa (si se encuentran para copiar sus sprites)
            var cajaAviso = hud != null ? hud.transform.Find("Contenido/AvisoInicio/Caja") : null;
            var bordeAviso = cajaAviso != null ? cajaAviso.Find("Borde")?.GetComponent<UnityEngine.UI.Image>() : null;
            var fondoAviso = cajaAviso != null ? cajaAviso.Find("Fondo")?.GetComponent<UnityEngine.UI.Image>() : null;
            if (fondoAviso != null) { img.sprite = fondoAviso.sprite; img.type = UnityEngine.UI.Image.Type.Sliced; }
            if (bordeAviso != null)
            {
                var b = new GameObject("Borde", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                var brt = (RectTransform)b.transform;
                brt.SetParent(rt, false);
                brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
                brt.offsetMin = frt.offsetMin; brt.offsetMax = frt.offsetMax;
                var bi = b.GetComponent<UnityEngine.UI.Image>();
                bi.sprite = bordeAviso.sprite;
                bi.type = UnityEngine.UI.Image.Type.Sliced;
                bi.color = new Color(1f, 1f, 1f, 0.28f);
                bi.raycastTarget = false;
            }

            var tgo = new GameObject("Texto", typeof(RectTransform), typeof(TextMeshProUGUI));
            var trt = (RectTransform)tgo.transform;
            trt.SetParent(rt, false);
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            var tmp = tgo.GetComponent<TextMeshProUGUI>();
            if (hud != null)
            {
                // La letra del texto del objetivo (la del cuerpo del menú); si no, cualquiera del HUD
                var otro = hud.transform.Find("Contenido/Objetivo/Texto")?.GetComponent<TMP_Text>();
                if (otro == null) otro = hud.GetComponentInChildren<TMP_Text>(true);
                if (otro != null) tmp.font = otro.font;
            }
            tmp.fontSize = 31f;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 24f;
            tmp.fontSizeMax = 31f;
            tmp.lineSpacing = -6f;
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

        void Update()
        {
            grupo.alpha = Mathf.MoveTowards(grupo.alpha, alfaObjetivo, Time.unscaledDeltaTime * 5f);
            if (grupo.alpha <= 0f && alfaObjetivo <= 0f && texto.text.Length > 0) texto.text = "";
        }

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
                alfaObjetivo = 1f;
                yield return new WaitForSeconds(duracion * f.Length / Mathf.Max(1, total));
            }
            alfaObjetivo = 0f;
            rutina = null;
        }
    }
}
