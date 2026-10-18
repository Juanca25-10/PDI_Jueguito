using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simula párpados en pantalla según P(Cerrados): dos imágenes (arriba y abajo) se acercan al
/// centro y un panel negro sube su alpha. Poner este script en el objeto Canvas.
/// </summary>
public class ParpadeoPantalla : MonoBehaviour
{
    [Header("Referencias")]
    public OjosReceiver ojos;
    public RectTransform parpadoSuperior;     // imagen con degradado (opaco arriba, transparente hacia el centro)
    public RectTransform parpadoInferior;     // imagen con degradado (opaco abajo, transparente hacia el centro)
    public Image panelNegro;                  // negro sólido, empieza con alpha 0

    [Header("Ajuste del efecto")]
    [Range(0.5f, 0.7f)] public float coberturaPorParpado = 0.55f; // fracción de pantalla que tapa cada párpado (>0.5 para que se solapen)
    [Range(0f, 0.4f)] public float zonaMuertaAbierto = 0.15f;     // P por debajo de esto = totalmente abierto
    [Range(0.6f, 1f)] public float zonaMuertaCerrado = 0.85f;     // P por encima de esto = totalmente cerrado
    [Range(0f, 1f)] public float alphaMaximoPanel = 1f;
    public float suavizadoSeg = 0.06f;                            // más alto = movimiento más lento y fluido
    public bool abrirSiNoHayRostro = true;                        // sin rostro: no oscurecer la pantalla

    [Header("Prueba sin cámara (para diseñar tus sprites)")]
    public bool usarPruebaManual = false;
    [Range(0f, 1f)] public float pruebaManual = 0f;

    RectTransform canvasRT;
    float actual, velocidad;                  // 0 = abierto, 1 = cerrado (ya suavizado)

    void Awake()
    {
        canvasRT = GetComponent<RectTransform>();

        // Anclas y pivotes por código: así no tienes que configurarlos a mano
        parpadoSuperior.anchorMin = new Vector2(0, 1); parpadoSuperior.anchorMax = new Vector2(1, 1);
        parpadoSuperior.pivot = new Vector2(0.5f, 1);
        parpadoInferior.anchorMin = new Vector2(0, 0); parpadoInferior.anchorMax = new Vector2(1, 0);
        parpadoInferior.pivot = new Vector2(0.5f, 0);
        panelNegro.raycastTarget = false;
    }

    void Update()
    {
        float p = usarPruebaManual ? pruebaManual
                : (ojos != null && (ojos.HayRostro || !abrirSiNoHayRostro)) ? ojos.PCerrados : 0f;

        // Remapeo: evita que el ruido cerca de 0 o de 1 deje los párpados temblando o sin cerrar del todo
        float objetivo = Mathf.InverseLerp(zonaMuertaAbierto, zonaMuertaCerrado, p);
        actual = Mathf.SmoothDamp(actual, objetivo, ref velocidad, suavizadoSeg, Mathf.Infinity, Time.unscaledDeltaTime);

        float t = Mathf.SmoothStep(0f, 1f, actual);                 // arranque y llegada suaves
        float alto = canvasRT.rect.height * coberturaPorParpado;

        parpadoSuperior.sizeDelta = new Vector2(0, alto);
        parpadoInferior.sizeDelta = new Vector2(0, alto);
        // Abierto: fuera de pantalla (desplazado ±alto). Cerrado: pegado al borde, cubriendo hasta el centro.
        parpadoSuperior.anchoredPosition = new Vector2(0, Mathf.Lerp(alto, 0f, t));
        parpadoInferior.anchoredPosition = new Vector2(0, Mathf.Lerp(-alto, 0f, t));

        Color c = panelNegro.color;
        c.a = t * alphaMaximoPanel;
        panelNegro.color = c;
    }
}
