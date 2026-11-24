using CuandoNadieMira.Menu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CuandoNadieMira.Juego
{
    /// <summary>
    /// Lectura de ojos en vivo: tu cámara, un ojo que se cierra con la probabilidad que da la red, el estado
    /// (abiertos, parpadeo, cerrados, sin rostro, sin señal), la barra P(cerrados) con tus umbrales y si estás calibrado.
    /// Todo sale de OjosReceiver (o de la tecla en modo teclado); si no hay lectura se muestra "--".
    /// </summary>
    public class HudOjos : MonoBehaviour
    {
        [SerializeField] CanvasGroup group;
        [SerializeField] RectTransform eye;
        [SerializeField] Image eyeOutline, iris;
        [SerializeField] TMP_Text state, detail, thresholds, calibration, blinks, header;
        [SerializeField] Image calibrationIcon, liveDot;
        [SerializeField] Sprite iconOk, iconWarning;
        [SerializeField] EyeMeter meter;
        [SerializeField] CanvasGroup portrait, keyboardCard;
        [SerializeField] TMP_Text keyboardKey;
        [SerializeField] Image stateGlow;

        float shownP, vel, openSince, punch;
        bool lastClosed;
        string lastState;

        void Update()
        {
            var hud = HudMuseo.Instance;
            var gm = hud != null ? hud.Game : null;
            if (gm == null) { HudUtil.Set(group, 0f); return; }

            var f = hud.Phase;
            bool visible = f == GameManager.Fase.Explorando || f == GameManager.Fase.Jugando;
            HudUtil.Fade(group, visible ? 1f : 0f, 0.5f);
            if (group != null && group.alpha <= 0f) return;

            bool kb = hud.KeyboardMode;
            var o = hud.Eyes;
            bool face = hud.HasFace;
            bool closed = face && hud.ClosedRaw;
            bool counted = face && hud.ClosedCounted;
            float p = kb ? (closed ? 1f : 0f) : (o != null && face ? o.PCerrados : -1f);

            if (closed != lastClosed) { punch = 1f; if (!closed) openSince = Time.unscaledTime; lastClosed = closed; }

            // Ojo: se cierra siguiendo la probabilidad suavizada de la red
            shownP = Mathf.SmoothDamp(shownP, p >= 0f ? p : 0.55f, ref vel, 0.07f, Mathf.Infinity, Time.unscaledDeltaTime);
            float open = 1f - shownP;
            if (eye != null) eye.localScale = new Vector3(1f + punch * 0.06f, Mathf.Lerp(0.1f, 1f, open), 1f);
            if (iris != null) iris.color = new Color(1f, 1f, 1f, face ? Mathf.Lerp(0f, 0.95f, open * open) : 0.15f);
            if (eyeOutline != null) eyeOutline.color = new Color(1f, 1f, 1f, face ? 0.95f : 0.35f);
            punch = Mathf.MoveTowards(punch, 0f, Time.unscaledDeltaTime * 4f);

            // Estado, con el mismo criterio que el GameManager (un parpadeo corto no cuenta como cerrados)
            string s, d;
            if (!face)
            {
                bool noSignal = o == null || o.SegSinDatos > o.timeoutSinDatos;
                s = noSignal ? "Sin señal" : "Sin rostro";
                d = hud.Reason();
            }
            else if (counted) { s = "Cerrados"; d = $"hace {hud.ClosedSeconds:0.0} s"; }
            else if (closed) { s = "Parpadeo"; d = "aún no cuenta como cerrados"; }
            else { s = "Abiertos"; d = $"hace {Mathf.Max(0f, Time.unscaledTime - openSince):0.0} s"; }
            if (s != lastState) { punch = Mathf.Max(punch, 0.6f); lastState = s; }
            if (state != null)
            {
                state.text = s;
                state.rectTransform.localScale = Vector3.one * (1f + punch * 0.08f);
                state.alpha = face ? 1f : 0.75f;
            }
            if (detail != null) detail.text = d;
            if (stateGlow != null) stateGlow.color = new Color(1f, 1f, 1f, (counted ? 0.10f : 0.02f) + punch * 0.12f);

            // Barra P(cerrados) con los umbrales que usa el receptor
            if (meter != null)
            {
                if (kb) meter.Set(p, null);
                else meter.Set(p, null, o != null ? o.umbralCierre : -1f, o != null ? o.umbralApertura : -1f);
            }
            if (thresholds != null)
                thresholds.text = kb ? $"modo teclado: mantén {HudUtil.KeyName(gm.teclaCerrarOjos)}"
                    : o != null ? $"cierra en {o.umbralCierre * 100f:0} %  ·  abre en {o.umbralApertura * 100f:0} %" : "";

            // Calibración personal (la guarda la pantalla de Calibración del menú)
            bool calibrated = !kb && o != null && o.usarCalibracion && PlayerPrefs.GetInt("cnm.calib.ok", 0) == 1;
            if (calibration != null)
                calibration.text = kb ? "Sin cámara" : calibrated ? "Calibración personal" : "Umbrales por defecto";
            if (calibrationIcon != null) calibrationIcon.sprite = calibrated ? iconOk : iconWarning;
            if (blinks != null) blinks.text = kb || o == null ? "" : $"parpadeos  {o.Parpadeos}";

            // Punto "en vivo": se enciende con cada paquete que llega de la red
            if (liveDot != null)
            {
                float since = kb ? 0f : o != null ? o.SegSinDatos : 999f;
                float a = since < 0.25f ? 0.95f : since < 1f ? 0.45f : 0.12f;
                liveDot.color = new Color(1f, 1f, 1f, a);
            }
            if (header != null)
                header.text = kb ? "LECTURA · TECLADO" : SimuladorOjos.Activo ? "LECTURA · SIN CÁMARA (C)" : LanzadorPython.UsandoVideoPrueba ? "LECTURA · VIDEO DE PRUEBA" : "LECTURA DE OJOS";

            HudUtil.Fade(portrait, kb ? 0f : 1f, 0.3f);
            HudUtil.Fade(keyboardCard, kb ? 1f : 0f, 0.3f);
            if (keyboardKey != null)
            {
                keyboardKey.text = HudUtil.KeyName(gm.teclaCerrarOjos);
                keyboardKey.alpha = closed ? 1f : 0.7f;
            }
        }
    }
}
