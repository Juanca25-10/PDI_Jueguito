using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CuandoNadieMira.Menu
{
    /// <summary>
    /// Botón del menú: hover y foco comparten estado (ratón, teclado y mando), despierta su pieza
    /// del museo, hace brotar brasas y suena. Al presionarlo, la placa se hunde 2 mm.
    /// </summary>
    public class MenuButton : Selectable, IPointerClickHandler, ISubmitHandler
    {
        static readonly Color BorderIdle = new(1f, 1f, 1f, 0.4f);
        static readonly Color BorderHover = new(1f, 1f, 1f, 0.85f);

        [Header("Partes")]
        [SerializeField] TMP_Text label;
        [SerializeField] RectTransform content;     // icono + texto (se desplaza 6 px)
        [SerializeField] RectTransform icon;
        [SerializeField] Image border;
        [SerializeField] Image ornament;            // línea fina que se dibuja bajo el texto
        [SerializeField] ParticleSystem embers;
        [SerializeField] bool iconBlinks;           // el ojo de "Iniciar experiencia" parpadea de verdad

        [Header("Comportamiento")]
        [SerializeField] MuseumExhibit exhibit;
        public UnityEvent onActivate = new();

        public MuseumExhibit Exhibit => exhibit;
        public bool IsFocused { get; private set; }
        public float FocusedTime => IsFocused ? Time.unscaledTime - focusStart : 0f;

        public static event Action<MenuButton> Focused;
        public static event Action<MenuButton> Unfocused;

        Coroutine hoverRoutine;
        Vector2 contentBase;
        float focusStart;
        bool pressing;

        public void Configure(TMP_Text text, RectTransform contentRoot, RectTransform iconRect, Image borderImage,
            Image ornamentImage, ParticleSystem emberSystem, bool blink, MuseumExhibit linkedExhibit)
        {
            label = text; content = contentRoot; icon = iconRect; border = borderImage;
            ornament = ornamentImage; embers = emberSystem; iconBlinks = blink; exhibit = linkedExhibit;
        }

        protected override void Awake()
        {
            base.Awake();
            transition = Transition.None;
            if (content != null) contentBase = content.anchoredPosition;
            if (border != null) border.color = BorderIdle;
            if (ornament != null) ornament.fillAmount = 0f;
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
            CursorEye.OverButton = true;
            if (IsInteractable()) Select();          // hover = foco
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);
            CursorEye.OverButton = false;
        }

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);
            if (IsFocused) return;
            IsFocused = true;
            focusStart = Time.unscaledTime;
            Restart(HoverIn());
            Focused?.Invoke(this);
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            base.OnDeselect(eventData);
            if (!IsFocused) return;
            IsFocused = false;
            Restart(HoverOut());
            Unfocused?.Invoke(this);
        }

        public virtual void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) Press();
        }

        public virtual void OnSubmit(BaseEventData eventData) => Press();

        public void Press()
        {
            if (!IsInteractable() || pressing) return;
            StartCoroutine(PressRoutine());
        }

        void Restart(IEnumerator routine)
        {
            if (hoverRoutine != null) StopCoroutine(hoverRoutine);
            hoverRoutine = StartCoroutine(routine);
        }

        IEnumerator HoverIn()
        {
            MenuAudio.Instance?.Play(MenuSound.Hover, 0.8f);
            if (embers != null && !SettingsStore.DisableSparkles) embers.Emit(UnityEngine.Random.Range(4, 9));
            if (iconBlinks && icon != null) StartCoroutine(Blink());

            Color from = border != null ? border.color : BorderIdle;
            float fill = ornament != null ? ornament.fillAmount : 0f;
            Vector2 start = content != null ? content.anchoredPosition : Vector2.zero;
            for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime)
            {
                if (border != null) border.color = Color.Lerp(from, BorderHover, Mathf.Clamp01(t / 0.15f));
                if (ornament != null) ornament.fillAmount = Mathf.Lerp(fill, 1f, t / 0.25f);
                if (content != null) content.anchoredPosition = Vector2.Lerp(start, contentBase + new Vector2(6f, 0f), EaseOut(t / 0.25f));
                yield return null;
            }
            if (border != null) border.color = BorderHover;
            if (ornament != null) ornament.fillAmount = 1f;
            if (content != null) content.anchoredPosition = contentBase + new Vector2(6f, 0f);
        }

        IEnumerator HoverOut()
        {
            Color from = border != null ? border.color : BorderHover;
            float fill = ornament != null ? ornament.fillAmount : 1f;
            Vector2 start = content != null ? content.anchoredPosition : contentBase;
            for (float t = 0f; t < 0.2f; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.2f;
                if (border != null) border.color = Color.Lerp(from, BorderIdle, k);
                if (ornament != null) ornament.fillAmount = Mathf.Lerp(fill, 0f, k);
                if (content != null) content.anchoredPosition = Vector2.Lerp(start, contentBase, EaseOut(k));
                yield return null;
            }
            if (border != null) border.color = BorderIdle;
            if (ornament != null) ornament.fillAmount = 0f;
            if (content != null) content.anchoredPosition = contentBase;
        }

        IEnumerator Blink()
        {
            Vector3 s = Vector3.one;
            for (float t = 0f; t < 0.18f; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Sin(t / 0.18f * Mathf.PI);   // cierra y abre
                icon.localScale = new Vector3(s.x, Mathf.Lerp(1f, 0.08f, k), s.z);
                yield return null;
            }
            icon.localScale = s;
        }

        IEnumerator PressRoutine()
        {
            pressing = true;
            MenuAudio.Instance?.Play(MenuSound.Select, 0.9f, 0.01f);
            Vector3 basePos = transform.localPosition;
            float sink = 0.002f / Mathf.Max(1e-5f, transform.lossyScale.z);   // 2 mm en Z
            if (border != null) border.color = Color.white;
            for (float t = 0f; t < 0.08f; t += Time.unscaledDeltaTime)
            {
                transform.localPosition = basePos + new Vector3(0f, 0f, sink * (t / 0.08f));
                yield return null;
            }
            yield return new WaitForSecondsRealtime(0.06f);
            for (float t = 0f; t < 0.12f; t += Time.unscaledDeltaTime)
            {
                transform.localPosition = basePos + new Vector3(0f, 0f, sink * (1f - t / 0.12f));
                if (border != null) border.color = Color.Lerp(Color.white, IsFocused ? BorderHover : BorderIdle, t / 0.12f);
                yield return null;
            }
            transform.localPosition = basePos;
            pressing = false;
            onActivate.Invoke();
        }

        static float EaseOut(float x) => 1f - Mathf.Pow(1f - Mathf.Clamp01(x), 3f);

        public void SetLabel(string text)
        {
            if (label != null) label.text = text;
        }
    }
}
