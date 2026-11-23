using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace CuandoNadieMira.Menu
{
    /// <summary>
    /// Pantalla de excepción "Se perdió el seguimiento". Muestra el estado real de la cámara, del rostro y de la
    /// confianza, y ofrece reintentar la calibración, continuar cuando el rostro vuelva o volver al menú.
    /// </summary>
    public class TrackingLostPanel : MonoBehaviour
    {
        [SerializeField] CanvasGroup group;
        [SerializeField] TMP_Text subtitle;
        [SerializeField] StatusChip cameraChip, faceChip, confidenceChip;
        [SerializeField] MenuButton retryButton, resumeButton, menuButton;

        public bool IsOpen { get; private set; }
        public MenuButton RetryButton => retryButton;
        public MenuButton ResumeButton => resumeButton;
        public MenuButton MenuButton => menuButton;

        /// <summary>Se llama al pulsar "Continuar" (con el rostro ya de vuelta).</summary>
        public event Action Resumed;

        float faceBackSince = -1f;
        Coroutine fade;

        public void Configure(CanvasGroup g, TMP_Text sub, StatusChip cam, StatusChip face, StatusChip conf, MenuButton retry, MenuButton resume, MenuButton menu)
        {
            group = g; subtitle = sub; cameraChip = cam; faceChip = face; confidenceChip = conf;
            retryButton = retry; resumeButton = resume; menuButton = menu;
        }

        void Awake()
        {
            SetVisible(0f);
            gameObject.SetActive(false);
            resumeButton.onActivate.AddListener(() =>
            {
                if (!CanResume) return;
                Hide();
                Resumed?.Invoke();
            });
        }

        bool CanResume => faceBackSince >= 0f && Time.unscaledTime - faceBackSince >= 0.8f;

        public void Show()
        {
            if (IsOpen) return;
            IsOpen = true;
            faceBackSince = -1f;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            MenuNav.Wire(new UnityEngine.UI.Selectable[] { retryButton, resumeButton, menuButton });
            Refresh();
            MenuNav.Focus(retryButton);
            MenuAudio.Instance?.Play(MenuSound.Heartbeat, 0.7f, 0f);
            Restart(FadeTo(1f, 0.35f));
        }

        public void Hide()
        {
            if (!IsOpen) return;
            IsOpen = false;
            Restart(FadeTo(0f, 0.3f, deactivate: true));
        }

        void Update()
        {
            if (!IsOpen) return;
            Refresh();
            MenuNav.KeepSelection(CanResume ? resumeButton : retryButton);
            if (MenuNav.BackPressed && CanResume) resumeButton.Press();
        }

        void Refresh()
        {
            var s = EyeTrackingService.Instance;
            bool cam = s != null && s.CameraConnected;
            bool face = s != null && s.HasFace;
            float conf = s != null ? s.Confidence : 0f;

            if (face && s.IsStable(0.8f)) { if (faceBackSince < 0f) faceBackSince = Time.unscaledTime; }
            else faceBackSince = -1f;

            cameraChip.Set(cam ? ChipState.Ok : ChipState.Warning, cam ? "Cámara: conectada" : "Cámara: no detectada");
            faceChip.Set(face ? ChipState.Ok : ChipState.Warning, face ? "Rostro: detectado" : "Rostro: no detectado");
            confidenceChip.Set(conf >= 0.85f ? ChipState.Ok : conf >= 0.65f ? ChipState.Pending : ChipState.Warning,
                "Confianza: " + TrackingText.ConfidenceLabel(conf));

            if (subtitle != null)
                subtitle.text = CanResume ? "Te volvimos a ver. Puedes continuar." : TrackingText.Problem(s) + " " + TrackingText.Advice(s);

            resumeButton.SetLabel(CanResume ? "Continuar" : "Continuar cuando esté listo");
            resumeButton.interactable = CanResume;
            var cg = resumeButton.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = CanResume ? 1f : 0.45f;
        }

        void Restart(IEnumerator routine)
        {
            if (fade != null) StopCoroutine(fade);
            fade = StartCoroutine(routine);
        }

        IEnumerator FadeTo(float target, float time, bool deactivate = false)
        {
            float from = group.alpha;
            for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
            {
                SetVisible(Mathf.Lerp(from, target, t / time));
                yield return null;
            }
            SetVisible(target);
            if (deactivate) gameObject.SetActive(false);
        }

        void SetVisible(float a)
        {
            group.alpha = a;
            group.interactable = a > 0.5f;
            group.blocksRaycasts = a > 0.01f;
        }
    }
}
