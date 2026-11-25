using CuandoNadieMira.Menu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CuandoNadieMira.Juego
{
    /// <summary>
    /// Victoria y derrota con la estética del menú. Aparecen cuando el GameManager deja de esperar
    /// (segHastaPantallaFinal) y muestran lo que realmente pasó en el turno: quién te alcanzó, cuánto duró,
    /// cuánto tiempo estuviste con los ojos cerrados y cuántas veces parpadeaste.
    /// En la derrota, el retrato de quien te alcanzó (capturado del juego, ver HerramientasEntrega) se funde con la
    /// oscuridad: sin caja ni marco, más grande, y con un acercamiento lento mientras la pantalla está abierta.
    /// </summary>
    public class HudFinal : MonoBehaviour
    {
        [Header("Victoria")]
        [SerializeField] CanvasGroup victory;
        [SerializeField] TMP_Text vDuration, vClosed, vBlinks;
        [SerializeField] MenuButton vAgain, vMenu;

        [Header("Derrota")]
        [SerializeField] CanvasGroup defeat;
        [SerializeField] TMP_Text dKicker, dTitle, dSub, dDuration, dClosed, dBlinks;
        [SerializeField] Image dImage;
        [SerializeField] GameObject dFrame;
        [SerializeField] RectTransform dText;
        [SerializeField] Sprite timeOutImage;
        [SerializeField] MenuButton dAgain, dMenu;

        [Header("Textos")]
        [SerializeField] string culpritTitle = "{0} te alcanzó";
        [SerializeField, TextArea] string culpritSub = "Mientras tenías los ojos cerrados, se acercó hasta tu puesto.";
        [SerializeField] string timeTitle = "La noche se hizo eterna";
        [SerializeField, TextArea] string timeSub = "Te quedaste mirando y el amanecer nunca llegó.";

        [Header("Retrato de la derrota")]
        [SerializeField] Vector2 portraitSize = new Vector2(560f, 780f);
        [SerializeField] float portraitLeft = -900f;
        [SerializeField] float portraitZoom = 0.06f;      // cuánto se acerca el retrato (6 %) mientras se muestra
        [SerializeField] float portraitZoomSeconds = 14f;

        public bool Visible { get; private set; }
        float shownAt;

        void Start()
        {
            HudUtil.Set(victory, 0f);
            HudUtil.Set(defeat, 0f);
            IntegratePortrait();
            vAgain?.onActivate.AddListener(Again);
            dAgain?.onActivate.AddListener(Again);
            vMenu?.onActivate.AddListener(ToMenu);
            dMenu?.onActivate.AddListener(ToMenu);
        }

        void Update()
        {
            var hud = HudMuseo.Instance;
            var gm = hud != null ? hud.Game : null;
            if (gm == null) return;

            bool won = hud.Phase == GameManager.Fase.Victoria, lost = hud.Phase == GameManager.Fase.Derrota;
            if (!Visible && (won || lost) && hud.PhaseTime >= gm.segHastaPantallaFinal) Show(hud, won);
            else if (Visible && !won && !lost) Hide(hud);

            HudUtil.Fade(victory, Visible && won ? 1f : 0f, Visible ? 1.2f : 0.3f);
            HudUtil.Fade(defeat, Visible && lost ? 1f : 0f, Visible ? 0.9f : 0.3f);
            if (Visible) MenuNav.KeepSelection(won ? vAgain : dAgain);

            // El retrato se acerca despacio y "respira" (como la luz de una vela), sin salirse de su sitio
            if (Visible && lost && dImage != null)
            {
                float t = Mathf.Clamp01((Time.unscaledTime - shownAt) / portraitZoomSeconds);
                float ease = 1f - (1f - t) * (1f - t);
                dImage.rectTransform.localScale = Vector3.one * (1f + portraitZoom * ease);
                float flicker = 0.93f + 0.07f * Mathf.PerlinNoise(Time.unscaledTime * 1.7f, 0.3f);
                dImage.color = new Color(flicker, flicker, flicker, 1f);
            }
        }

        // Sin caja ni borde: las imágenes de derrota ya traen sus bordes fundidos a negro, así que se mezclan con el fondo
        void IntegratePortrait()
        {
            if (dFrame == null) return;
            foreach (var n in new[] { "Fondo", "Borde" })
            {
                var t = dFrame.transform.Find(n);
                if (t != null) t.gameObject.SetActive(false);
            }
            var frt = dFrame.transform as RectTransform;
            if (frt != null)
            {
                frt.sizeDelta = portraitSize;
                frt.anchoredPosition = new Vector2(portraitLeft, frt.anchoredPosition.y);
            }
            if (dImage != null)
            {
                var irt = dImage.rectTransform;
                irt.pivot = new Vector2(0.5f, 0.62f);      // se acerca hacia la cara, no hacia el centro
                irt.offsetMin = irt.offsetMax = Vector2.zero;
            }
        }

        void Show(HudMuseo hud, bool won)
        {
            Visible = true;
            shownAt = Time.unscaledTime;
            string duration = HudUtil.Duration(hud.TurnSeconds);
            string closed = HudUtil.Duration(hud.ClosedTotal);
            string blinks = hud.KeyboardMode ? "--" : hud.TurnBlinks.ToString();

            if (won)
            {
                Set(vDuration, duration); Set(vClosed, closed); Set(vBlinks, blinks);
            }
            else
            {
                var c = hud.Culprit;
                Set(dKicker, $"FIN DEL TURNO  ·  {HudUtil.Clock(hud.Game.progreso)}");
                Set(dTitle, c != null ? string.Format(culpritTitle, c.nombre) : timeTitle);
                Set(dSub, c != null ? culpritSub : timeSub);
                Set(dDuration, duration); Set(dClosed, closed); Set(dBlinks, blinks);

                Sprite img = c != null ? c.ImagenDerrotaAleatoria() : null;
                if (img == null) img = timeOutImage;
                if (dImage != null) dImage.sprite = img;
                if (dFrame != null) dFrame.SetActive(img != null);
                if (dText != null) dText.anchoredPosition = new Vector2(img != null ? 0f : -260f, dText.anchoredPosition.y);
            }

            // La cámara ya terminó de encarar a la estatua: se suelta el ratón para los botones
            if (hud.Player != null) hud.Player.enabled = false;
            HudUtil.FreeCursor();
            var first = won ? vAgain : dAgain;
            MenuNav.Wire(won ? new Selectable[] { vAgain, vMenu } : new Selectable[] { dAgain, dMenu }, horizontal: true);
            MenuNav.Focus(first);
            MenuAudio.Instance?.Play(won ? MenuSound.Bell : MenuSound.Heartbeat, 0.8f);
        }

        void Hide(HudMuseo hud)
        {
            Visible = false;
            if (hud.Player != null) hud.Player.enabled = true;
            HudUtil.LockCursor();
            if (UnityEngine.EventSystems.EventSystem.current != null) UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        }

        static void Set(TMP_Text t, string s) { if (t != null) t.text = s; }

        void Again()
        {
            if (Visible && GameManager.Instancia != null) GameManager.Instancia.ReiniciarTodo();
        }

        void ToMenu()
        {
            if (!Visible) return;
            MenuFlow.Intent = FlowIntent.Play;
            HudMuseo.Instance.GoTo(MenuFlow.MainMenuScene);
        }
    }
}
