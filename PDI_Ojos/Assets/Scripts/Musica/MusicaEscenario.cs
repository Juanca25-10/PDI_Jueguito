using UnityEngine;

/// <summary>
/// Una sola playlist que suena a la vez por varios parlantes (AudioSources) repartidos por el escenario.
/// Todos reproducen la MISMA canción sincronizados, así que no suena "cuatro veces": suena una,
/// pero al acercarte a un parlante la escuchas como si estuviera pegado a tu oído.
/// El volumen baja solo durante el turno (cuando empieza el evento del escritorio) y se recupera al terminar.
/// Ponlo en un objeto vacío y mete los parlantes como hijos suyos (cada uno con un AudioSource).
/// </summary>
public class MusicaEscenario : MonoBehaviour
{
    [Header("Canciones")]
    public AudioClip[] canciones;
    public bool aleatorio = false;
    public float pausaEntreCanciones = 0.5f;

    [Header("Parlantes")]
    [Tooltip("Vacío = usa todos los AudioSource hijos de este objeto.")]
    public AudioSource[] parlantes;

    [Header("Espacialidad (se aplica a todos los parlantes)")]
    public bool aplicarEspacialidad = true;
    public float distanciaMinima = 2f;                      // más cerca que esto suena a todo volumen
    public float distanciaMaxima = 20f;                     // más lejos que esto no se oye
    public AudioRolloffMode atenuacion = AudioRolloffMode.Logarithmic;

    [Header("Volumen")]
    [Range(0f, 1f)] public float volumen = 0.8f;
    [Range(0f, 1f)] public float volumenEnTurno = 0.25f;   // durante el evento de la silla
    public float segCambioVolumen = 1.5f;                   // qué tan suave baja y sube
    public float segFundidoInicial = 3f;                    // entrada suave al abrir el juego

    int indice = -1;
    double finCancion;
    float factor = 1f, fundidoInicial;
    bool reproduciendo;

    void Start()
    {
        if (parlantes == null || parlantes.Length == 0) parlantes = GetComponentsInChildren<AudioSource>();
        foreach (var p in parlantes)
        {
            p.playOnAwake = false;
            p.loop = false;                                 // la playlist la maneja este script
            if (!aplicarEspacialidad) continue;
            p.spatialBlend = 1f;                            // 3D total
            p.dopplerLevel = 0f;                            // sin cambios de tono al caminar
            p.rolloffMode = atenuacion;
            p.minDistance = distanciaMinima;
            p.maxDistance = distanciaMaxima;
        }

        if (canciones == null || canciones.Length == 0 || parlantes.Length == 0) return;
        Siguiente(AudioSettings.dspTime + 0.3);
        reproduciendo = true;
    }

    void Update()
    {
        if (!reproduciendo) return;
        float dt = Time.unscaledDeltaTime;

        // Cuando termina la canción, todos los parlantes arrancan la siguiente en el mismo instante
        if (AudioSettings.dspTime >= finCancion)
            Siguiente(AudioSettings.dspTime + Mathf.Max(0.1f, pausaEntreCanciones));

        bool enTurno = GameManager.Instancia != null && GameManager.Instancia.fase == GameManager.Fase.Jugando;
        factor = Mathf.MoveTowards(factor, enTurno ? volumenEnTurno / Mathf.Max(0.0001f, volumen) : 1f,
                                   dt / Mathf.Max(0.01f, segCambioVolumen));
        fundidoInicial = Mathf.MoveTowards(fundidoInicial, 1f, dt / Mathf.Max(0.01f, segFundidoInicial));

        float v = volumen * factor * fundidoInicial;
        foreach (var p in parlantes) p.volume = v;
    }

    void Siguiente(double cuando)
    {
        if (aleatorio && canciones.Length > 1)
        {
            int nuevo;
            do { nuevo = Random.Range(0, canciones.Length); } while (nuevo == indice);
            indice = nuevo;
        }
        else indice = (indice + 1) % canciones.Length;

        AudioClip clip = canciones[indice];
        foreach (var p in parlantes)
        {
            p.Stop();
            p.clip = clip;
            p.PlayScheduled(cuando);                        // mismo instante en todos = sincronizados al sample
        }
        finCancion = cuando + (double)clip.samples / clip.frequency;
    }
}
