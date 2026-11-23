using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CuandoNadieMira.Menu
{
    /// <summary>
    /// Barra de lectura de ojos: muestra P(cerrados) en vivo (0 % = muy abiertos, 100 % = cerrados) y, cuando ya
    /// existen, las marcas de los umbrales de cierre y apertura. Sirve para que se vea qué está midiendo la red.
    /// </summary>
    public class EyeMeter : MonoBehaviour
    {
        [SerializeField] Image fill;
        [SerializeField] RectTransform closeMark, openMark;
        [SerializeField] TMP_Text label, value;

        float shown, vel;

        public void Configure(Image fillImage, RectTransform close, RectTransform open, TMP_Text text, TMP_Text valueText)
        {
            fill = fillImage; closeMark = close; openMark = open; label = text; value = valueText;
        }

        /// <param name="p">P(cerrados) actual; negativo si no hay lectura.</param>
        public void Set(float p, string text, float closeThreshold = -1f, float openThreshold = -1f)
        {
            bool has = p >= 0f;
            shown = Mathf.SmoothDamp(shown, has ? p : 0f, ref vel, 0.08f, Mathf.Infinity, Time.unscaledDeltaTime);
            if (fill != null)
            {
                fill.fillAmount = shown;
                fill.color = new Color(1f, 1f, 1f, has ? 0.85f : 0.2f);
            }
            if (value != null) value.text = has ? Mathf.RoundToInt(p * 100f) + " %" : "--";
            if (label != null && text != null) label.text = text;
            Mark(closeMark, closeThreshold);
            Mark(openMark, openThreshold);
        }

        void Mark(RectTransform mark, float t)
        {
            if (mark == null) return;
            mark.gameObject.SetActive(t >= 0f);
            if (t < 0f) return;
            var bar = (RectTransform)mark.parent;
            mark.anchoredPosition = new Vector2(bar.rect.width * Mathf.Clamp01(t), mark.anchoredPosition.y);
        }
    }
}
