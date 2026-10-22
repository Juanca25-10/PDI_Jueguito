using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CuandoNadieMira.Menu
{
    public enum MenuState { Intro, Idle, Panel, Transition }

    /// <summary>
    /// Máquina de estados del menú principal: foco y navegación con wrap, piezas que despiertan,
    /// paneles por el reverso de la placa, opciones de accesibilidad y transiciones de escena.
    /// </summary>
    public class MenuController : MonoBehaviour
    {
        [Header("Escenas de destino")]
        [SerializeField] string startScene = "Calibracion";
        [SerializeField] string calibrationScene = "Calibracion";

        [Header("Botones principales (orden de foco)")]
        [SerializeField] MenuButton[] mainButtons;   // Iniciar, Calibración, Controles, Créditos, Salir

        [Header("Sistemas")]
        [SerializeField] MenuCameraRig rig;
        [SerializeField] PanelFlipper flipper;
        [SerializeField] SceneTransition transition;
        [SerializeField] TMP_Text plateMessage;
        [SerializeField] GameObject[] lowQualityDisabled;   // niebla, polvo y efectos caros

        [Header("Paneles del reverso")]
        [SerializeField] GameObject controlsPanel;
        [SerializeField] GameObject accessibilityPanel;
        [SerializeField] GameObject creditsPanel;
        [SerializeField] GameObject exitPanel;
        [SerializeField] RectTransform creditsContent;
        // Cada lista es el orden de foco del panel e incluye su botón de Inicio
        [SerializeField] MenuButton[] controlsButtons;       // Accesibilidad, Inicio
        [SerializeField] MenuButton[] accessibilityButtons;  // opciones, Volver a Controles, Inicio
        [SerializeField] MenuButton[] creditsButtons;        // Inicio
        [SerializeField] MenuButton[] exitButtons;           // Quedarme, Salir, Inicio
        [SerializeField] MenuButton[] homeButtons;           // un botón de Inicio por panel
        [SerializeField] MenuButton accessibilityBack;
        [SerializeField] TMP_Text optionHelp;                // ayuda de la opción enfocada

        public MenuState State { get; private set; } = MenuState.Intro;

        MuseumExhibit[] exhibits;
        MenuButton lastFocused, panelOpener;
        Coroutine messageRoutine;
        readonly Dictionary<TMP_Text, float> baseFontSizes = new();
        float creditsStartY;

        public void Configure(MenuButton[] main, MenuCameraRig cameraRig, PanelFlipper panelFlipper, SceneTransition sceneTransition,
            TMP_Text message, GameObject[] lowQualityObjects)
        {
            mainButtons = main; rig = cameraRig; flipper = panelFlipper; transition = sceneTransition;
            plateMessage = message; lowQualityDisabled = lowQualityObjects;
        }

        public void ConfigurePanels(GameObject controls, GameObject accessibility, GameObject credits, GameObject exit,
            RectTransform creditsScroll, MenuButton[] controlsBtns, MenuButton[] accessibilityBtns, MenuButton[] creditsBtns, MenuButton[] exitBtns,
            MenuButton[] homes, MenuButton accBack, TMP_Text help)
        {
            controlsPanel = controls; accessibilityPanel = accessibility; creditsPanel = credits; exitPanel = exit;
            creditsContent = creditsScroll; controlsButtons = controlsBtns; accessibilityButtons = accessibilityBtns;
            creditsButtons = creditsBtns; exitButtons = exitBtns; homeButtons = homes; accessibilityBack = accBack; optionHelp = help;
        }

        void Awake()
        {
            exhibits = FindObjectsByType<MuseumExhibit>(FindObjectsSortMode.None);
            foreach (var t in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.GetComponentInParent<Canvas>() != null) baseFontSizes[t] = t.fontSize;
            if (plateMessage != null) plateMessage.alpha = 0f;
            if (creditsContent != null) creditsStartY = creditsContent.anchoredPosition.y;

            WireNavigation(mainButtons);
            WireNavigation(controlsButtons);
            WireNavigation(accessibilityButtons);
            WireNavigation(creditsButtons);
            WireNavigation(exitButtons, horizontal: true);
            WireActions();

            SettingsStore.Changed += ApplySettings;
            ApplySettings();
        }

        void OnEnable()
        {
            MenuButton.Focused += OnButtonFocused;
        }

        void OnDisable()
        {
            MenuButton.Focused -= OnButtonFocused;
        }

        void OnDestroy() => SettingsStore.Changed -= ApplySettings;

        IEnumerator Start()
        {
            State = MenuState.Intro;
            if (transition != null) yield return transition.FadeIn(1.6f);
            State = MenuState.Idle;
            Focus(mainButtons[0]);
        }

        void Update()
        {
            HandleBack();
            KeepSelection();
            ScrollCredits();
        }

        // --- Navegación ------------------------------------------------------------

        static void WireNavigation(MenuButton[] buttons, bool horizontal = false)
        {
            if (buttons == null) return;
            for (int i = 0; i < buttons.Length; i++)
            {
                var prev = buttons[(i - 1 + buttons.Length) % buttons.Length];
                var next = buttons[(i + 1) % buttons.Length];
                var nav = new Navigation { mode = Navigation.Mode.Explicit };
                if (horizontal) { nav.selectOnLeft = prev; nav.selectOnRight = next; nav.selectOnUp = prev; nav.selectOnDown = next; }
                else { nav.selectOnUp = prev; nav.selectOnDown = next; }
                buttons[i].navigation = nav;
            }
        }

        void Focus(MenuButton b)
        {
            if (b == null || EventSystem.current == null) return;
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(b.gameObject);
        }

        // Si se pierde la selección (clic en vacío), cualquier navegación la recupera
        void KeepSelection()
        {
            if (EventSystem.current == null || EventSystem.current.currentSelectedGameObject != null) return;
            if (State != MenuState.Idle && State != MenuState.Panel) return;
            bool nav = (Keyboard.current != null && (Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.downArrowKey.wasPressedThisFrame
                        || Keyboard.current.wKey.wasPressedThisFrame || Keyboard.current.sKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame))
                       || (Gamepad.current != null && (Gamepad.current.dpad.ReadValue().sqrMagnitude > 0.1f || Gamepad.current.leftStick.ReadValue().sqrMagnitude > 0.3f));
            if (!nav) return;
            Focus(lastFocused != null && lastFocused.isActiveAndEnabled ? lastFocused : CurrentFirst());
        }

        MenuButton CurrentFirst()
        {
            if (State == MenuState.Panel && flipper.CurrentPanel != null)
            {
                var b = flipper.CurrentPanel.GetComponentInChildren<MenuButton>();
                if (b != null) return b;
            }
            return mainButtons[0];
        }

        void HandleBack()
        {
            bool back = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                        || (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);
            if (!back) return;
            if (State == MenuState.Panel) StartCoroutine(ClosePanel());
            else if (State == MenuState.Idle) Focus(mainButtons[0]);
        }

        void OnButtonFocused(MenuButton b)
        {
            lastFocused = b;
            UpdateHelp(b);
            if (State != MenuState.Idle || b.Exhibit == null) return;
            // La pieza asociada despierta y las demás bajan al 60 %
            foreach (var e in exhibits)
            {
                if (e == b.Exhibit) e.Awaken();
                else e.Sleep(0.6f);
            }
            rig.SetFocus(b.Exhibit.FocusPoint);
        }

        // --- Acciones --------------------------------------------------------------

        void WireActions()
        {
            mainButtons[0].onActivate.AddListener(() => StartCoroutine(GoToScene(startScene, false)));
            mainButtons[1].onActivate.AddListener(() => StartCoroutine(GoToScene(calibrationScene, true)));
            mainButtons[2].onActivate.AddListener(() => StartCoroutine(OpenPanel(controlsPanel, mainButtons[2])));
            mainButtons[3].onActivate.AddListener(() => StartCoroutine(OpenPanel(creditsPanel, mainButtons[3])));
            mainButtons[4].onActivate.AddListener(() => StartCoroutine(OpenPanel(exitPanel, mainButtons[4])));

            if (controlsButtons is { Length: >= 1 })
                controlsButtons[0].onActivate.AddListener(() => SwapPanel(accessibilityPanel));
            if (exitButtons is { Length: >= 2 })
            {
                exitButtons[0].onActivate.AddListener(() => StartCoroutine(ClosePanel()));
                exitButtons[1].onActivate.AddListener(Quit);
            }
            // Todas las pantallas tienen su botón de Inicio
            if (homeButtons != null)
                foreach (var h in homeButtons) h.onActivate.AddListener(() => StartCoroutine(ClosePanel()));
            if (accessibilityBack != null) accessibilityBack.onActivate.AddListener(() => SwapPanel(controlsPanel));
            WireAccessibility();
        }

        void WireAccessibility()
        {
            if (accessibilityButtons == null) return;
            foreach (var b in accessibilityButtons)
            {
                if (b is not OptionRow row) continue;
                switch (row.Key)
                {
                    case "reduceMotion": row.Bind(() => B(SettingsStore.ReduceMotion), v => SettingsStore.ReduceMotion = v > 0.5f); break;
                    case "disableSparkles": row.Bind(() => B(SettingsStore.DisableSparkles), v => SettingsStore.DisableSparkles = v > 0.5f); break;
                    case "master": row.Bind(() => SettingsStore.MasterVolume, v => SettingsStore.MasterVolume = v); break;
                    case "music": row.Bind(() => SettingsStore.MusicVolume, v => SettingsStore.MusicVolume = v); break;
                    case "sfx": row.Bind(() => SettingsStore.SfxVolume, v => SettingsStore.SfxVolume = v); break;
                    case "subtitles": row.Bind(() => B(SettingsStore.Subtitles), v => SettingsStore.Subtitles = v > 0.5f); break;
                    case "textSize": row.Bind(() => B(SettingsStore.LargeText), v => SettingsStore.LargeText = v > 0.5f); break;
                    case "lowQuality": row.Bind(() => B(SettingsStore.LowQuality), v => SettingsStore.LowQuality = v > 0.5f); break;
                }
            }
        }

        static float B(bool v) => v ? 1f : 0f;

        void UpdateHelp(MenuButton b)
        {
            if (optionHelp == null) return;
            if (b is OptionRow row) optionHelp.text = row.Description;
            else if (b == accessibilityBack) optionHelp.text = "Regresa a la tabla de controles.";
            else if (homeButtons != null && System.Array.IndexOf(homeButtons, b) >= 0) optionHelp.text = "Regresa a la pantalla de inicio. Atajo: Esc.";
        }

        void ApplySettings()
        {
            if (accessibilityButtons != null)
                foreach (var b in accessibilityButtons) if (b is OptionRow row) row.Refresh(false);
            foreach (var kv in baseFontSizes)
                if (kv.Key != null) kv.Key.fontSize = kv.Value * SettingsStore.TextScale;
            if (lowQualityDisabled != null)
                foreach (var go in lowQualityDisabled) if (go != null) go.SetActive(!SettingsStore.LowQuality);
        }

        IEnumerator OpenPanel(GameObject panel, MenuButton opener)
        {
            if (State != MenuState.Idle || panel == null) yield break;
            State = MenuState.Panel;
            panelOpener = opener;
            foreach (var e in exhibits) e.SetLevel(1f);
            if (panel == creditsPanel && creditsContent != null)
                creditsContent.anchoredPosition = new Vector2(creditsContent.anchoredPosition.x, creditsStartY);
            yield return flipper.ShowPanel(panel);
            Focus(panel.GetComponentInChildren<MenuButton>());
        }

        void SwapPanel(GameObject panel)
        {
            flipper.SwapPanel(panel);
            Focus(panel.GetComponentInChildren<MenuButton>());
        }

        IEnumerator ClosePanel()
        {
            if (State != MenuState.Panel || flipper.IsAnimating) yield break;
            yield return flipper.ShowFront();
            State = MenuState.Idle;
            Focus(panelOpener != null ? panelOpener : mainButtons[0]);
        }

        IEnumerator GoToScene(string scene, bool shortVersion)
        {
            if (State != MenuState.Idle) yield break;
            State = MenuState.Transition;
            EventSystem.current.SetSelectedGameObject(null);
            yield return transition.Play(scene, shortVersion, ShowMissingScene);
            // Si la escena no existe, la transición devuelve el control al menú
            State = MenuState.Idle;
            Focus(mainButtons[0]);
        }

        void ShowMissingScene(string scene)
        {
            ShowMessage($"La sala «{scene}» aún no está abierta.\nAgrega la escena a Build Settings.");
        }

        public void ShowMessage(string text)
        {
            if (plateMessage == null) return;
            if (messageRoutine != null) StopCoroutine(messageRoutine);
            messageRoutine = StartCoroutine(MessageRoutine(text));
        }

        IEnumerator MessageRoutine(string text)
        {
            plateMessage.text = text;
            for (float t = 0; t < 0.4f; t += Time.unscaledDeltaTime) { plateMessage.alpha = t / 0.4f; yield return null; }
            plateMessage.alpha = 1f;
            yield return new WaitForSecondsRealtime(3.5f);
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime) { plateMessage.alpha = 1f - t; yield return null; }
            plateMessage.alpha = 0f;
        }

        void ScrollCredits()
        {
            if (State != MenuState.Panel || creditsContent == null || !creditsPanel.activeInHierarchy) return;
            var p = creditsContent.anchoredPosition;
            p.y += 18f * Time.unscaledDeltaTime;
            if (p.y > creditsStartY + creditsContent.rect.height) p.y = creditsStartY - 260f;
            creditsContent.anchoredPosition = p;
        }

        void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
