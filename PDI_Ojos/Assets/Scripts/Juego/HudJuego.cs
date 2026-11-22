using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Muestra y oculta los paneles y textos que TÚ creas en un Canvas (TextMeshPro, Slider, Panel...).
/// No dibuja nada por código. Todo es opcional: lo que dejes vacío simplemente no se usa.
/// Ponlo en el Canvas del HUD. Ese Canvas debe tener un "Sort Order" MAYOR que el de CanvasParpados
/// para que la barra se siga viendo con los párpados cerrados.
/// </summary>
public class HudJuego : MonoBehaviour
{
    [Header("Paneles (GameObjects que se encienden y apagan)")]
    public GameObject panelExplorando;     // instrucciones mientras exploras
    public GameObject avisoInicio;         // "Pulsa E para empezar" (solo aparece cerca del escritorio)
    public GameObject panelJugando;        // contiene la barra y lo que quieras mostrar durante el turno
    public GameObject avisoSinRostro;      // "Sin rostro: partida en pausa"
    public GameObject panelVictoria;
    public GameObject panelDerrota;

    [Header("Contenido dinámico (opcional)")]
    public Slider barra;                   // la barra "hasta el amanecer" (Whole Numbers desmarcado; cualquier rango sirve)
    public Image barraRelleno;             // alternativa: una Image con Image Type = Filled (se llena por porcentaje)
    public TMP_Text textoDerrota;          // si lo dejas vacío, el panel de derrota muestra su texto fijo
    [TextArea] public string mensajeDerrotaEstatua = "{0} te alcanzó.";      // {0} = nombre de la estatua
    [TextArea] public string mensajeDerrotaBarra = "Te quedaste mirando y la noche se hizo eterna.";
    public TMP_Text textoDepuracion;       // opcional: datos para ajustar el juego

    public void MostrarFase(GameManager.Fase fase)
    {
        Activar(panelExplorando, fase == GameManager.Fase.Explorando);
        Activar(panelJugando, fase == GameManager.Fase.Jugando);
        Activar(avisoInicio, false);
        Activar(avisoSinRostro, false);
        Activar(panelVictoria, false);
        Activar(panelDerrota, false);
    }

    public void AvisoInicio(bool visible) { Activar(avisoInicio, visible); }
    public void AvisoSinRostro(bool visible) { Activar(avisoSinRostro, visible); }

    /// <summary>progreso de 0 a 100.</summary>
    public void Barra(float progreso)
    {
        if (barra != null) barra.normalizedValue = progreso / 100f;
        if (barraRelleno != null) barraRelleno.fillAmount = progreso / 100f;
    }

    public void MostrarVictoria() { Activar(panelVictoria, true); }

    public void MostrarDerrota(Estatua culpable)
    {
        if (textoDerrota != null)
            textoDerrota.text = culpable != null ? string.Format(mensajeDerrotaEstatua, culpable.nombre) : mensajeDerrotaBarra;
        Activar(panelDerrota, true);
    }

    public void Depuracion(string texto)
    {
        if (textoDepuracion != null) textoDepuracion.text = texto;
    }

    static void Activar(GameObject g, bool visible)
    {
        if (g != null && g.activeSelf != visible) g.SetActive(visible);
    }
}