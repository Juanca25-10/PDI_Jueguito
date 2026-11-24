using CuandoNadieMira.Menu;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CuandoNadieMira.Juego
{
    /// <summary>
    /// Pausa con Esc (o Start del mando): congela el tiempo y el sonido, suelta el ratón y ofrece
    /// continuar, ver los controles, recalibrar los ojos o volver al menú. Los ojos se siguen leyendo en vivo.
    /// </summary>
    public class HudPausa : MonoBehaviour
    {
        [SerializeField] CanvasGroup group, buttons, controls;
        [SerializeField] MenuButton resume, showControls, recalibrate, mainMenu, back;
        [SerializeField] HudFinal final;

        public static bool Paused { get; private set; }

        float savedScale = 1f;
        bool controlsOpen, leaving;

        void Start()
        {
            HudUtil.Set(group, 0f);
            HudUtil.Set(controls, 0f);
            resume?.onActivate.AddListener(Resume);
            showControls?.onActivate.AddListener(() => SetControls(true));
            back?.onActivate.AddListener(() => SetControls(false));
            recalibrate?.onActivate.AddListener(() => Leave(MenuFlow.CalibrationScene, FlowIntent.Play));
            mainMenu?.onActivate.AddListener(() => Leave(MenuFlow.MainMenuScene, FlowIntent.Play));

            // Los sonidos de los botones deben oírse aunque el resto del audio esté en pausa
            if (MenuAudio.Instance != null)
                foreach (var src in MenuAudio.Instance.GetComponents<AudioSource>()) src.ignoreListenerPause = true;
        }

        void OnDestroy()
        {
            if (Paused) { Time.timeScale = savedScale; AudioListener.pause = false; }
            Paused = false;
        }

        bool PausePressed =>
            (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);

        void Update()
        {
            var hud = HudMuseo.Instance;
            if (hud == null || hud.Game == null || leaving) return;

            if (!Paused)
            {
                bool canPause = hud.Ready && (hud.Phase == GameManager.Fase.Explorando || hud.Phase == GameManager.Fase.Jugando)
                                && (final == null || !final.Visible);
                if (canPause && PausePressed) Pause();
            }
            else
            {
                if (MenuNav.BackPressed || (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame))
                {
                    if (controlsOpen) SetControls(false); else Resume();
                }
                else MenuNav.KeepSelection(controlsOpen ? back : resume);
            }

            HudUtil.Fade(group, Paused ? 1f : 0f, 0.2f);
            HudUtil.Fade(buttons, Paused && !controlsOpen ? 1f : 0f, 0.15f);
            HudUtil.Fade(controls, Paused && controlsOpen ? 1f : 0f, 0.15f);
        }

        void Pause()
        {
            var hud = HudMuseo.Instance;
            Paused = true;
            controlsOpen = false;
            savedScale = Time.timeScale;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            if (hud.Player != null) hud.Player.enabled = false;   // su vista con el ratón ignora timeScale
            hud.Game.enabled = false;                               // que E o R no hagan nada detrás de la pausa
            HudUtil.FreeCursor();
            MenuNav.Wire(new Selectable[] { resume, showControls, recalibrate, mainMenu });
            MenuNav.Focus(resume);
            MenuAudio.Instance?.Play(MenuSound.Select, 0.6f);
        }

        public void Resume()
        {
            if (!Paused) return;
            var hud = HudMuseo.Instance;
            Paused = false;
            Time.timeScale = savedScale;
            AudioListener.pause = false;
            if (hud != null)
            {
                if (hud.Player != null) hud.Player.enabled = true;
                if (hud.Game != null) hud.Game.enabled = true;
            }
            HudUtil.LockCursor();
            if (UnityEngine.EventSystems.EventSystem.current != null) UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        }

        void SetControls(bool open)
        {
            controlsOpen = open;
            MenuNav.Focus(open ? back : showControls);
        }

        void Leave(string scene, FlowIntent intent)
        {
            leaving = true;
            MenuFlow.Intent = intent;
            Paused = false;
            HudMuseo.Instance.GoTo(scene);
        }
    }
}
