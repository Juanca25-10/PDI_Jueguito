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
    public GameObject avisoSinRostro;      // "Sin rostro: partida en pausa" (si tiene CanvasGroup, entra y sale con fundido)
    public CanvasGroup avisoRostroEncontrado;   // "Rostro encontrado": aparece unos segundos tras recuperar la cara y se desvanece
    public GameObject panelVictoria;
    public GameObject panelDerrota;

    [Header("Contenido dinámico (opcional)")]
    public Slider barra;                   // la barra "hasta el amanecer" (Whole Numbers desmarcado; cualquier rango sirve)
    public Image barraRelleno;             // alternativa: una Image con Image Type = Filled (se llena por porcentaje)
    public Image imagenDerrota;            // la Image del panel de derrota; el sprite que ya tenga es el de "morir por tiempo"
    public TMP_Text textoDerrota;          // si lo dejas vacío, el panel de derrota muestra su texto fijo
    [TextArea] public string mensajeDerrotaEstatua = "{0} te alcanzó.";      // {0} = nombre de la estatua
    [TextArea] public string mensajeDerrotaBarra = "Te quedaste mirando y la noche se hizo eterna.";
    public TMP_Text textoDepuracion;       // opcional: datos para ajustar el juego

    [Header("Fundidos de los avisos de rostro")]
    public float segAparecer = 0.35f;
    public float segDesvanecer = 0.6f;
    public float segEncontradoVisible = 1f;

    Sprite spriteDerrotaBase;
    CanvasGroup grupoSinRostro;
    bool sinRostro, esperandoEncontrado, huboPerdida;
    float objetivoSinRostro, objetivoEncontrado, timerEncontrado;

    void Awake()
    {
        if (imagenDerrota != null) spriteDerrotaBase = imagenDerrota.sprite;
        if (avisoSinRostro != null) grupoSinRostro = avisoSinRostro.GetComponent<CanvasGroup>();
        ReiniciarAvisosRostro();
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        // "Encontrado" solo entra cuando "sin rostro" ya casi no se ve: nunca se solapan
        if (esperandoEncontrado && Alfa(grupoSinRostro) < 0.02f)
        {
            objetivoEncontrado = 1f;
            if (Alfa(avisoRostroEncontrado) > 0.98f)
            {
                timerEncontrado -= dt;
                if (timerEncontrado <= 0f) { objetivoEncontrado = 0f; esperandoEncontrado = false; }
            }
        }
        Fundir(grupoSinRostro, objetivoSinRostro, dt);
        Fundir(avisoRostroEncontrado, objetivoEncontrado, dt);
    }

    public void MostrarFase(GameManager.Fase fase)
    {
        Activar(panelExplorando, fase == GameManager.Fase.Explorando);
        Activar(panelJugando, fase == GameManager.Fase.Jugando);
        Activar(avisoInicio, false);
        ReiniciarAvisosRostro();
        Activar(panelVictoria, false);
        Activar(panelDerrota, false);
    }

    public void AvisoInicio(bool visible) { Activar(avisoInicio, visible); }
    /// <summary>Se puede llamar cada frame: solo reacciona cuando el valor cambia.</summary>
    public void AvisoSinRostro(bool visible)
    {
        if (visible == sinRostro) return;
        sinRostro = visible;
        if (grupoSinRostro == null) { Activar(avisoSinRostro, visible); return; }   // sin CanvasGroup: encendido/apagado simple

        if (visible)
        {
            huboPerdida = true;
            esperandoEncontrado = false;
            objetivoEncontrado = 0f;
            objetivoSinRostro = 1f;
        }
        else
        {
            objetivoSinRostro = 0f;
            if (huboPerdida) { esperandoEncontrado = true; timerEncontrado = segEncontradoVisible; }
        }
    }

    void ReiniciarAvisosRostro()
    {
        sinRostro = huboPerdida = esperandoEncontrado = false;
        objetivoSinRostro = objetivoEncontrado = 0f;
        if (grupoSinRostro != null) { Preparar(grupoSinRostro); avisoSinRostro.SetActive(true); }
        else Activar(avisoSinRostro, false);
        Preparar(avisoRostroEncontrado);
    }

    static void Preparar(CanvasGroup g)
    {
        if (g == null) return;
        g.alpha = 0f;
        g.interactable = false;
        g.blocksRaycasts = false;
    }

    static float Alfa(CanvasGroup g) { return g != null ? g.alpha : 0f; }

    void Fundir(CanvasGroup g, float objetivo, float dt)
    {
        if (g == null) return;
        float seg = objetivo > g.alpha ? segAparecer : segDesvanecer;
        g.alpha = Mathf.MoveTowards(g.alpha, objetivo, dt / Mathf.Max(0.01f, seg));
    }

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
        if (imagenDerrota != null)
        {
            Sprite elegido = culpable != null ? culpable.ImagenDerrotaAleatoria() : null;
            imagenDerrota.sprite = elegido != null ? elegido : spriteDerrotaBase;   // sin lista o por tiempo: la base
        }
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