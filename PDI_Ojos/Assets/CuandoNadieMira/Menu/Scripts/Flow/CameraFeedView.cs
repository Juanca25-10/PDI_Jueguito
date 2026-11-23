using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CuandoNadieMira.Menu
{
    /// <summary>
    /// Marco de la cámara en Calibración: muestra la imagen real que manda el detector (en escala de grises y en
    /// espejo, como un selfie), la guía de cabeza y hombros, y un mensaje cuando no hay imagen.
    /// </summary>
    public class CameraFeedView : MonoBehaviour
    {
        [SerializeField] RawImage feed;
        [SerializeField] Image headGuide;
        [SerializeField] TMP_Text placeholder;
        [SerializeField] RectTransform scanLine;    // línea tenue que recorre el marco mientras se busca la cámara

        /// <summary>0 = guía tenue (sin alinear), 1 = guía brillante (rostro bien ubicado).</summary>
        public float GuideTarget { get; set; }
        public bool GuidePulse { get; set; } = true;

        float guide, feedAlpha;

        public void Configure(RawImage feedImage, Image guideImage, TMP_Text message, RectTransform scan)
        {
            feed = feedImage; headGuide = guideImage; placeholder = message; scanLine = scan;
        }

        public void SetMessage(string text)
        {
            if (placeholder != null && placeholder.text != text) placeholder.text = text;
        }

        void Update()
        {
            var s = EyeTrackingService.Instance;
            bool hasFrame = s != null && s.HasFrame;
            float dt = Time.unscaledDeltaTime;

            feedAlpha = Mathf.MoveTowards(feedAlpha, hasFrame ? 1f : 0f, dt * 2.5f);
            if (feed != null)
            {
                if (hasFrame && feed.texture != s.Frame) feed.texture = s.Frame;
                feed.enabled = feedAlpha > 0.01f && feed.texture != null;
                feed.color = new Color(1f, 1f, 1f, 0.82f * feedAlpha);
                if (feed.texture != null) Cover(feed);
            }

            if (placeholder != null) placeholder.alpha = 1f - feedAlpha;
            if (scanLine != null)
            {
                scanLine.gameObject.SetActive(!hasFrame);
                var parent = (RectTransform)scanLine.parent;
                float h = parent.rect.height;
                scanLine.anchoredPosition = new Vector2(scanLine.anchoredPosition.x, Mathf.Lerp(-h * 0.45f, h * 0.45f, Mathf.PingPong(Time.unscaledTime * 0.35f, 1f)));
            }

            guide = Mathf.MoveTowards(guide, GuideTarget, dt * 2f);
            if (headGuide != null)
            {
                float pulse = GuidePulse && guide < 0.5f ? 0.12f * Mathf.Sin(Time.unscaledTime * 2.6f) : 0f;
                headGuide.color = new Color(1f, 1f, 1f, Mathf.Clamp01(Mathf.Lerp(0.38f, 0.95f, guide) + pulse));
            }
        }

        // Ajusta las UV para que la imagen llene el marco sin deformarse (recorta lo que sobra)
        static void Cover(RawImage img)
        {
            var tex = img.texture;
            var rect = img.rectTransform.rect;
            if (tex.height == 0 || rect.height <= 0f) return;
            float texAspect = tex.width / (float)tex.height;
            float boxAspect = rect.width / rect.height;
            img.uvRect = texAspect > boxAspect
                ? new Rect((1f - boxAspect / texAspect) * 0.5f, 0f, boxAspect / texAspect, 1f)
                : new Rect(0f, (1f - texAspect / boxAspect) * 0.5f, 1f, texAspect / boxAspect);
        }
    }
}
