using System.Collections;
using UnityEngine;

namespace CuandoNadieMira.Menu
{
    /// <summary>
    /// Gira la placa 180° sobre su eje vertical en 0.5 s para mostrar un panel por el reverso,
    /// como la otra cara de una placa del museo.
    /// </summary>
    public class PanelFlipper : MonoBehaviour
    {
        [SerializeField] MenuCameraRig rig;
        [SerializeField] CanvasGroup front;
        [SerializeField] CanvasGroup back;
        [SerializeField] GameObject[] backPanels;
        [SerializeField] float flipTime = 0.5f;

        public bool IsFlipped { get; private set; }
        public bool IsAnimating { get; private set; }
        public GameObject CurrentPanel { get; private set; }

        public void Configure(MenuCameraRig cameraRig, CanvasGroup frontGroup, CanvasGroup backGroup, GameObject[] panels)
        {
            rig = cameraRig; front = frontGroup; back = backGroup; backPanels = panels;
        }

        void Awake()
        {
            SetInteractable(front, true);
            SetInteractable(back, false);
            SetSide(0f);
        }

        // Los gráficos de UI son de doble cara: se oculta la cara que queda de espaldas.
        // Las ramas (LineRenderer) y las brasas no obedecen al CanvasGroup, así que se apagan aparte.
        void SetSide(float angle)
        {
            bool showFront = angle < 90f;
            if (front != null) front.alpha = showFront ? 1f : 0f;
            if (back != null) back.alpha = showFront ? 0f : 1f;
            frontRenderers ??= front != null ? front.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
            backRenderers ??= back != null ? back.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
            foreach (var r in frontRenderers) if (r != null) r.enabled = showFront;
            foreach (var r in backRenderers) if (r != null) r.enabled = !showFront;
        }

        Renderer[] frontRenderers, backRenderers;

        public IEnumerator ShowPanel(GameObject panel)
        {
            if (IsAnimating) yield break;
            foreach (var p in backPanels) p.SetActive(p == panel);
            CurrentPanel = panel;
            yield return Flip(true);
        }

        public IEnumerator ShowFront()
        {
            if (IsAnimating || !IsFlipped) yield break;
            yield return Flip(false);
            CurrentPanel = null;
        }

        /// <summary>Cambia de panel trasero sin girar (por ejemplo, de Controles a Accesibilidad).</summary>
        public void SwapPanel(GameObject panel)
        {
            foreach (var p in backPanels) p.SetActive(p == panel);
            CurrentPanel = panel;
        }

        IEnumerator Flip(bool toBack)
        {
            IsAnimating = true;
            SetInteractable(front, false);
            SetInteractable(back, false);
            MenuAudio.Instance?.Play(MenuSound.Swish, 0.6f, 0.05f);

            float from = toBack ? 0f : 180f, to = toBack ? 180f : 0f;
            for (float t = 0f; t < flipTime; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / flipTime);
                float angle = Mathf.Lerp(from, to, k);
                rig.uiPlateLocalRotation = Quaternion.Euler(0f, angle, 0f);
                SetSide(angle);
                yield return null;
            }
            rig.uiPlateLocalRotation = Quaternion.Euler(0f, to, 0f);
            SetSide(to);
            IsFlipped = toBack;
            SetInteractable(toBack ? back : front, true);
            IsAnimating = false;
        }

        static void SetInteractable(CanvasGroup g, bool on)
        {
            if (g == null) return;
            g.interactable = on;
            g.blocksRaycasts = on;
        }
    }
}
