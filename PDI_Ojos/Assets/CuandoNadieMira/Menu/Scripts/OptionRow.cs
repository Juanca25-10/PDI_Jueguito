using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CuandoNadieMira.Menu
{
    public enum OptionKind { Toggle, Slider, Choice }

    /// <summary>
    /// Fila de opción con su control visible a la derecha: interruptor, barra de 10 segmentos o selector.
    /// ←/→ ajusta, Enter o clic cambia, y el clic sobre la barra fija el valor en ese punto.
    /// </summary>
    public class OptionRow : MenuButton
    {
        static readonly Color On = new(0.92f, 0.92f, 0.92f, 1f);
        static readonly Color Off = new(1f, 1f, 1f, 0.18f);

        [SerializeField] OptionKind kind;
        [SerializeField] string key;
        [SerializeField, TextArea] string description;
        [SerializeField] TMP_Text valueLabel;

        [Header("Interruptor")]
        [SerializeField] Image track;
        [SerializeField] RectTransform knob;

        [Header("Barra")]
        [SerializeField] Image[] segments;
        [SerializeField] RectTransform segmentArea;

        [Header("Selector")]
        [SerializeField] TMP_Text[] choices;
        [SerializeField] Image[] choiceMarks;

        Func<float> getter;
        Action<float> setter;
        float knobX, knobVel;

        public string Key => key;
        public string Description => description;

        public void ConfigureOption(OptionKind k, string optionKey, string help, TMP_Text value, Image switchTrack, RectTransform switchKnob,
            Image[] bar, RectTransform barArea, TMP_Text[] choiceLabels, Image[] marks)
        {
            kind = k; key = optionKey; description = help; valueLabel = value; track = switchTrack; knob = switchKnob;
            segments = bar; segmentArea = barArea; choices = choiceLabels; choiceMarks = marks;
        }

        public void Bind(Func<float> get, Action<float> set)
        {
            getter = get;
            setter = set;
            Refresh(true);
        }

        public override void OnMove(AxisEventData eventData)
        {
            if (eventData.moveDir == MoveDirection.Left) { Adjust(-1); eventData.Use(); return; }
            if (eventData.moveDir == MoveDirection.Right) { Adjust(1); eventData.Use(); return; }
            base.OnMove(eventData);
        }

        public override void OnSubmit(BaseEventData eventData)
        {
            switch (kind)
            {
                case OptionKind.Toggle: Set(getter() > 0.5f ? 0f : 1f); break;
                case OptionKind.Choice: Set((Mathf.RoundToInt(getter()) + 1) % choices.Length); break;
                default:
                    float v = getter();
                    Set(v >= 0.99f ? 0f : Mathf.Min(1f, Mathf.Round(v * 10f + 1f) / 10f));
                    break;
            }
        }

        public override void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (kind == OptionKind.Slider && segmentArea != null &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(segmentArea, eventData.position, eventData.pressEventCamera, out var p))
            {
                // El clic sobre la barra fija el valor directamente
                float t = Mathf.InverseLerp(segmentArea.rect.xMin, segmentArea.rect.xMax, p.x);
                if (t > 0f && t < 1f) { Set(Mathf.Ceil(t * 10f) / 10f); return; }
            }
            OnSubmit(eventData);
        }

        void Adjust(int dir)
        {
            switch (kind)
            {
                case OptionKind.Toggle: Set(dir > 0 ? 1f : 0f); break;
                case OptionKind.Choice: Set(Mathf.Clamp(Mathf.RoundToInt(getter()) + dir, 0, choices.Length - 1)); break;
                default: Set(Mathf.Clamp01(Mathf.Round(getter() * 10f + dir) / 10f)); break;
            }
        }

        void Set(float v)
        {
            if (getter == null || Mathf.Approximately(getter(), v)) return;
            setter(v);
            MenuAudio.Instance?.Play(MenuSound.Tick, 1f, 0.06f);
            Refresh(false);
        }

        /// <summary>Actualiza el control con el valor guardado.</summary>
        public void Refresh(bool instant)
        {
            if (getter == null) return;
            float v = getter();
            switch (kind)
            {
                case OptionKind.Toggle:
                    bool on = v > 0.5f;
                    if (valueLabel != null) valueLabel.text = on ? "Sí" : "No";
                    if (track != null) track.color = on ? new Color(1f, 1f, 1f, 0.55f) : new Color(1f, 1f, 1f, 0.12f);
                    if (instant && knob != null) { knobX = on ? 1f : 0f; PlaceKnob(); }
                    break;
                case OptionKind.Slider:
                    int filled = Mathf.RoundToInt(v * 10f);
                    for (int i = 0; i < segments.Length; i++) segments[i].color = i < filled ? On : Off;
                    if (valueLabel != null) valueLabel.text = $"{Mathf.RoundToInt(v * 100f)} %";
                    break;
                case OptionKind.Choice:
                    int idx = Mathf.RoundToInt(v);
                    for (int i = 0; i < choices.Length; i++)
                    {
                        choices[i].color = i == idx ? On : new Color(1f, 1f, 1f, 0.4f);
                        if (choiceMarks != null && i < choiceMarks.Length) choiceMarks[i].enabled = i == idx;
                    }
                    break;
            }
        }

        void Update()
        {
            if (kind != OptionKind.Toggle || knob == null || getter == null) return;
            knobX = Mathf.SmoothDamp(knobX, getter() > 0.5f ? 1f : 0f, ref knobVel, 0.08f, Mathf.Infinity, Time.unscaledDeltaTime);
            PlaceKnob();
        }

        void PlaceKnob()
        {
            var parent = (RectTransform)knob.parent;
            float travel = parent.rect.width - knob.rect.width - 8f;
            knob.anchoredPosition = new Vector2(4f + knobX * travel, 0f);
        }
    }
}
