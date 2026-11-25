using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CuandoNadieMira.Juego
{
    /// <summary>
    /// Arriba al centro: la barra "hasta el amanecer" del GameManager como un reloj del museo (00:00 a 06:00),
    /// con hacia dónde va la noche. Arriba a la derecha: las presencias, una fila por estatua con su pose real.
    /// </summary>
    public class HudAmanecer : MonoBehaviour
    {
        [Header("Amanecer")]
        [SerializeField] CanvasGroup dawn;
        [SerializeField] Image fill, head, moon, sun;
        [SerializeField] RectTransform bar;
        [SerializeField] TMP_Text clock, percent, trend;
        [SerializeField] Image tickTemplate;

        [Header("Presencias")]
        [SerializeField] bool showPresences = true;
        [SerializeField] CanvasGroup presences;
        [SerializeField] RectTransform presencesBox;
        [SerializeField] RectTransform rowTemplate;
        [SerializeField] Sprite pipFull, pipEmpty;
        [SerializeField] float rowHeight = 46f, rowsTop = 74f;

        class Row
        {
            public Estatua statue;
            public RectTransform rt;
            public TMP_Text name, state;
            public readonly List<Image> pips = new();
            public float flash, advancedAt = -99f, retreatedAt = -99f;
        }

        readonly List<Row> rows = new();
        readonly List<Image> ticks = new();
        float shown, rate, lastProgress;

        void OnEnable()
        {
            Estatua.Avanzo += OnAdvanced;
            Estatua.Retrocedio += OnRetreated;
        }

        void OnDisable()
        {
            Estatua.Avanzo -= OnAdvanced;
            Estatua.Retrocedio -= OnRetreated;
        }

        void Start()
        {
            if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
            if (tickTemplate != null) tickTemplate.gameObject.SetActive(false);
            HudUtil.Set(dawn, 0f);
            HudUtil.Set(presences, 0f);
            if (HudMuseo.Instance != null) HudMuseo.Instance.PhaseChanged += OnPhase;
        }

        void OnDestroy()
        {
            if (HudMuseo.Instance != null) HudMuseo.Instance.PhaseChanged -= OnPhase;
        }

        void OnPhase(GameManager.Fase f)
        {
            if (f != GameManager.Fase.Jugando) return;
            var gm = GameManager.Instancia;
            shown = lastProgress = gm != null ? gm.progreso : 0f;
            rate = 0f;
            BuildRows();
            BuildTicks();
        }

        Row Find(Estatua e) => rows.Find(r => r.statue == e);
        void OnAdvanced(Estatua e) { var r = Find(e); if (r != null) { r.flash = 1f; r.advancedAt = Time.unscaledTime; } }
        void OnRetreated(Estatua e) { var r = Find(e); if (r != null) r.retreatedAt = Time.unscaledTime; }

        void Update()
        {
            var hud = HudMuseo.Instance;
            var gm = GameManager.Instancia;
            if (hud == null || gm == null) return;

            bool playing = hud.Phase == GameManager.Fase.Jugando;
            bool ending = hud.Phase == GameManager.Fase.Victoria || hud.Phase == GameManager.Fase.Derrota;
            HudUtil.Fade(dawn, playing || (ending && hud.PhaseTime < 1.2f) ? 1f : 0f, 0.6f);
            HudUtil.Fade(presences, showPresences && playing && rows.Count > 0 ? 1f : 0f, 0.6f);
            if (dawn != null && dawn.alpha <= 0f) return;

            // Hacia dónde va la noche: se mide con el cambio real de la barra
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            float instant = (gm.progreso - lastProgress) / dt;
            lastProgress = gm.progreso;
            if (Time.deltaTime > 0f) rate = Mathf.Lerp(rate, instant, 1f - Mathf.Exp(-6f * Time.deltaTime));

            shown = Mathf.Lerp(shown, gm.progreso, 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));
            float t = Mathf.Clamp01(shown / 100f);
            if (fill != null) fill.fillAmount = t;
            if (bar != null && head != null)
            {
                head.rectTransform.anchoredPosition = new Vector2(bar.rect.width * t - head.rectTransform.rect.width * 0.5f, 0f);
                float glow = rate > 0.01f ? 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f) : 0.55f;
                head.color = new Color(1f, 1f, 1f, glow);
            }
            if (clock != null) clock.text = HudUtil.Clock(gm.progreso);
            if (percent != null) percent.text = $"{gm.progreso:0} %";
            if (moon != null) moon.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.95f, 0.35f, t));
            if (sun != null) sun.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.35f, 1f, t));

            if (trend != null)
            {
                if (hud.Phase == GameManager.Fase.Victoria) trend.text = "Amanece";
                else if (hud.Phase == GameManager.Fase.Derrota) trend.text = "El turno terminó";
                else if (!hud.HasFace) trend.text = "En pausa: no te vemos";
                else if (rate > 0.01f) trend.text = TormentaMadremonte.Nivel > 0.3f ? "La tormenta retrasa el amanecer" : "La noche avanza";
                else if (rate < -0.01f) trend.text = "El amanecer se aleja";
                else if (hud.ClosedRaw) trend.text = "Mantén los ojos cerrados...";
                else trend.text = "La noche espera";
            }

            UpdateRows(gm);
        }

        void BuildTicks()
        {
            foreach (var i in ticks) if (i != null) Destroy(i.gameObject);
            ticks.Clear();
            if (tickTemplate == null || bar == null) return;
            foreach (var e in Estatua.Todas)
            {
                if (e.activaDesdeProgreso <= 0f || e.activaDesdeProgreso >= 100f) continue;
                var tick = Instantiate(tickTemplate, tickTemplate.transform.parent);
                tick.gameObject.SetActive(true);
                tick.rectTransform.anchoredPosition = new Vector2(bar.rect.width * e.activaDesdeProgreso / 100f, 0f);
                ticks.Add(tick);
            }
        }

        void BuildRows()
        {
            foreach (var r in rows) if (r.rt != null) Destroy(r.rt.gameObject);
            rows.Clear();
            if (rowTemplate == null) return;

            int i = 0;
            foreach (var e in Estatua.Todas)
            {
                var rt = Instantiate(rowTemplate, rowTemplate.parent);
                rt.gameObject.SetActive(true);
                rt.name = "Fila_" + e.nombre;
                rt.anchoredPosition = new Vector2(0f, -rowsTop - i * rowHeight);
                var row = new Row
                {
                    statue = e,
                    rt = rt,
                    name = rt.Find("Nombre").GetComponent<TMP_Text>(),
                    state = rt.Find("Estado").GetComponent<TMP_Text>(),
                };
                row.name.text = e.nombre;

                // Una marca por paso hasta tu puesto (pose 1 a la última)
                var pip = rt.Find("Marca").GetComponent<Image>();
                pip.gameObject.SetActive(false);
                int n = Mathf.Max(1, e.PoseFinal);
                float span = 96f, step = n > 1 ? Mathf.Min(22f, span / (n - 1)) : 0f;
                for (int k = 0; k < n; k++)
                {
                    var p = Instantiate(pip, pip.transform.parent);
                    p.gameObject.SetActive(true);
                    var prt = p.rectTransform;
                    prt.anchoredPosition = pip.rectTransform.anchoredPosition + new Vector2(k * step, 0f);
                    if (k == n - 1) prt.sizeDelta *= 1.3f;       // la última: te alcanza
                    row.pips.Add(p);
                }
                rows.Add(row);
                i++;
            }
            if (presencesBox != null) presencesBox.sizeDelta = new Vector2(presencesBox.sizeDelta.x, rowsTop + rows.Count * rowHeight + 6f);
        }

        void UpdateRows(GameManager gm)
        {
            float now = Time.unscaledTime;
            foreach (var r in rows)
            {
                if (r.statue == null) continue;
                bool asleep = gm.progreso < r.statue.activaDesdeProgreso;
                int pose = r.statue.PoseActual, last = r.statue.PoseFinal;
                bool close = pose >= last - 1 && pose > 0;

                for (int k = 0; k < r.pips.Count; k++)
                {
                    bool on = k < pose;
                    r.pips[k].sprite = on ? pipFull : pipEmpty;
                    r.pips[k].color = new Color(1f, 1f, 1f, asleep ? 0.2f : on ? 0.95f : 0.4f);
                }

                string s = asleep ? "dormida" : now - r.advancedAt < 2.5f ? "se acercó" : now - r.retreatedAt < 2.5f ? "retrocede"
                         : close ? "muy cerca" : pose > 0 ? "acechando" : "en su sitio";
                r.state.text = s;

                r.flash = Mathf.MoveTowards(r.flash, 0f, Time.unscaledDeltaTime * 1.2f);
                float pulse = close ? 0.15f * (0.5f + 0.5f * Mathf.Sin(now * 5f)) : 0f;
                r.name.alpha = asleep ? 0.4f : 0.85f + r.flash * 0.15f;
                r.state.alpha = asleep ? 0.35f : 0.6f + r.flash * 0.4f + pulse;
                r.rt.localScale = Vector3.one * (1f + r.flash * 0.06f);
            }
        }
    }
}
