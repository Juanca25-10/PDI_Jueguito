using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CuandoNadieMira.Menu
{
    public enum ChipState { Pending, Ok, Warning, Off }

    /// <summary>
    /// Indicador de estado con punto (o icono) y texto: "Rostro detectado", "Cámara conectada"...
    /// Pendiente = punto gris que late; Ok = blanco fijo; Aviso = blanco que parpadea; Apagado = gris tenue.
    /// </summary>
    public class StatusChip : MonoBehaviour
    {
        [SerializeField] Image dot;          // punto de estado
        [SerializeField] Image icon;         // opcional: se cambia por el de "ok" o el de "falla"
        [SerializeField] TMP_Text label;
        [SerializeField] Sprite okSprite, pendingSprite, failSprite;

        public ChipState State { get; private set; } = ChipState.Pending;

        public void Configure(Image statusDot, Image statusIcon, TMP_Text text, Sprite ok, Sprite pending, Sprite fail)
        {
            dot = statusDot; icon = statusIcon; label = text; okSprite = ok; pendingSprite = pending; failSprite = fail;
        }

        public void Set(ChipState state, string text = null)
        {
            State = state;
            if (text != null && label != null) label.text = text;
            if (icon != null)
            {
                var s = state == ChipState.Ok ? okSprite : state == ChipState.Warning ? failSprite : pendingSprite;
                if (s != null) icon.sprite = s;
            }
        }

        void Update()
        {
            float t = Time.unscaledTime;
            float a = State switch
            {
                ChipState.Ok => 1f,
                ChipState.Warning => 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(t * 3.2f)),
                ChipState.Pending => 0.25f + 0.25f * (0.5f + 0.5f * Mathf.Sin(t * 2.4f)),
                _ => 0.2f,
            };
            if (dot != null) dot.color = new Color(1f, 1f, 1f, a);
            if (icon != null) icon.color = new Color(1f, 1f, 1f, State == ChipState.Ok ? 0.95f : Mathf.Max(0.35f, a));
            if (label != null) label.color = new Color(0.93f, 0.93f, 0.93f, State == ChipState.Ok ? 1f : State == ChipState.Warning ? 0.95f : 0.6f);
        }
    }
}
