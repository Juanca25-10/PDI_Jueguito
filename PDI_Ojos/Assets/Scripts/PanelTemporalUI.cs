using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class PanelTemporalUI : MonoBehaviour
{
    [Header("Configuración de UI")]
    [SerializeField] private float tiempoVisible = 3.0f; // Cuánto dura el panel en pantalla

    private CanvasGroup canvasGroup;
    private Coroutine rutinaActiva;

    void Awake()
    {
        // Obtenemos el componente CanvasGroup del mismo objeto automáticamente
        canvasGroup = GetComponent<CanvasGroup>();

        // Al empezar el juego, nos aseguramos de que el panel arranque oculto
        OcultarPanel();
    }

    // --- MÉTODO PÚBLICO: Muestra el panel por unos segundos ---
    public void ActivarPanelPorTiempo()
    {
        if (rutinaActiva != null)
        {
            StopCoroutine(rutinaActiva);
        }
        rutinaActiva = StartCoroutine(RutinaPanel());
    }

    private IEnumerator RutinaPanel()
    {
        // 1. MOSTRAR PANEL
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;  // Permite interactuar con los botones del panel si tiene
        canvasGroup.interactable = true;

        // 2. ESPERAR
        yield return new WaitForSeconds(tiempoVisible);

        // 3. OCULTAR PANEL AUTOMÁTICAMENTE
        OcultarPanel();

        rutinaActiva = null;
    }

    private void OcultarPanel()
    {
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false; // Deja pasar los clics a lo que esté detrás del panel
        canvasGroup.interactable = false;
    }
}
