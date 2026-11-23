using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Intro con varias cámaras: cada plano usa su propia cámara, que se desliza despacio hacia donde tú digas,
/// y entre planos hay un corte por negro. Al final pasa a la cámara del jugador.
/// El negro y los textos los creas tú en el Canvas; este script solo los enciende y apaga.
/// </summary>
public class IntroCinematica : MonoBehaviour
{
    [Serializable]
    public class Plano
    {
        public string nombre = "Plano";
        public Camera camara;                                        // la cámara de este plano (colócala donde quieras empezar a encuadrar)
        public float duracion = 6f;                                  // incluye el fundido de entrada y el de salida
        [Tooltip("Hacia dónde se desliza, en metros y en ejes de la cámara: X = derecha/izquierda, Y = arriba/abajo, Z = adelante/atrás. Ej: (1.5, 0, 0) = a la derecha; (-1.5, 0, 0) = a la izquierda.")]
        public Vector3 desplazamiento = new Vector3(1.5f, 0f, 0f);
        [Tooltip("Giro opcional en grados (positivo = hacia la derecha).")]
        public float giro = 0f;
        [Tooltip("Cambio de campo de visión: negativo = acerca (zoom in), positivo = aleja.")]
        public float zoom = 0f;
        [Tooltip("Marcado: la cámara pasa por su posición en el medio del plano. Desmarcado: empieza ahí y avanza.")]
        public bool centrar = true;
        public GameObject mostrar;                                   // texto o panel tuyo que se ve solo durante este plano (opcional)
    }

    public static bool Activa { get; private set; }                  // GameManager e InfoEstatua esperan mientras sea true

    [Header("Cámaras")]
    public Camera camaraJugador;                                     // la cámara hija del Player
    public ControlJugador jugador;

    [Header("Planos (en orden)")]
    public Plano[] planos;
    public AnimationCurve curva = AnimationCurve.Linear(0f, 0f, 1f, 1f);   // Linear = velocidad constante; EaseInOut = suave al inicio y al final

    [Header("Transición negra")]
    public CanvasGroup negro;                                        // Image negra a pantalla completa en un Canvas Overlay con Sort Order alto
    public float segFundido = 0.7f;                                  // al principio y al final de cada plano
    public float segNegro = 0f;                                      // pausa en negro entre planos (0 = el movimiento nunca se detiene)
    public float segFundidoInicial = 2f;                             // fundido de negro a imagen al abrir el juego
    public float segNegroInicial = 0.5f;                             // se queda en negro este tiempo antes de empezar

    [Header("Opciones")]
    public bool reproducirAlIniciar = true;
    public bool permitirSaltar = true;
    public Key teclaSaltar = Key.Space;
    [Tooltip("Canvas o paneles del HUD que se ocultan mientras dura la intro.")]
    public GameObject[] ocultarDurante;

    Vector3[] pos0;
    Quaternion[] rot0;
    float[] fov0;
    bool saltar;

    void Awake()
    {
        Activa = reproducirAlIniciar && planos != null && planos.Length > 0;
        if (negro != null) negro.alpha = 1f;                         // negro desde el primer fotograma, sin parpadeo
    }

    void Start()
    {
        int n = planos != null ? planos.Length : 0;
        pos0 = new Vector3[n];
        rot0 = new Quaternion[n];
        fov0 = new float[n];
        for (int i = 0; i < n; i++)
        {
            Camera c = planos[i].camara;
            if (c == null) continue;
            pos0[i] = c.transform.position;
            rot0[i] = c.transform.rotation;
            fov0[i] = c.fieldOfView;
            var oido = c.GetComponent<AudioListener>();
            if (oido != null) oido.enabled = false;                  // solo escucha el del jugador
        }

        if (!Activa)
        {
            ApagarCamaras();
            StartCoroutine(FundidoInicialSolo());                    // sin intro: igual entra con fundido desde negro
            return;
        }
        StartCoroutine(Reproducir());
    }

    void Update()
    {
        if (Activa && permitirSaltar && Keyboard.current != null && Keyboard.current[teclaSaltar].wasPressedThisFrame)
            saltar = true;
    }

    IEnumerator Reproducir()
    {
        if (negro != null) negro.alpha = 1f;
        Estado(true);                                                // jugador quieto, HUD oculto, cámara del jugador apagada
        yield return new WaitForSecondsRealtime(segNegroInicial);

        for (int i = 0; i < planos.Length && !saltar; i++)
        {
            Plano p = planos[i];
            if (p.camara == null) continue;

            ApagarCamaras();
            p.camara.enabled = true;
            if (p.mostrar != null) p.mostrar.SetActive(true);

            float entraSeg = i == 0 ? segFundidoInicial : segFundido;   // el primer plano entra más despacio
            float dur = Mathf.Max(p.duracion, entraSeg + segFundido + 0.1f);

            // Desde el segundo plano la cámara ya va "en marcha" al aparecer: arranca como si se hubiera
            // movido durante el fundido de salida de la anterior, así el movimiento no se corta en el cambio.
            float t0 = i == 0 ? 0f : segFundido;
            for (float t = t0; t < dur && !saltar; t += Time.unscaledDeltaTime)
            {
                float k = curva.Evaluate(t / dur);
                float m = p.centrar ? k - 0.5f : k;                  // -0.5..0.5 si está centrado, 0..1 si no
                Transform tr = p.camara.transform;
                Quaternion giroY = Quaternion.Euler(0f, p.giro * m, 0f);
                tr.rotation = rot0[i] * giroY;
                tr.position = pos0[i] + rot0[i] * (p.desplazamiento * m);
                p.camara.fieldOfView = fov0[i] + p.zoom * m;

                // negro -> imagen al empezar, imagen -> negro al terminar
                float entra = 1f - (t - t0) / entraSeg;
                float sale = (t - (dur - segFundido)) / segFundido;
                if (negro != null) negro.alpha = Mathf.Clamp01(Mathf.Max(entra, sale));
                yield return null;
            }

            if (p.mostrar != null) p.mostrar.SetActive(false);
            if (negro != null) negro.alpha = 1f;
            if (!saltar && segNegro > 0f) yield return new WaitForSecondsRealtime(segNegro);
        }

        if (saltar) yield return Fundir(1f, 0.4f);                   // si la saltaron, oscurece antes de cambiar

        ApagarCamaras();
        Estado(false);                                               // pasa a la cámara del jugador
        Activa = false;
        yield return Fundir(0f, segFundido);                         // del negro al juego
    }

    IEnumerator FundidoInicialSolo()
    {
        yield return new WaitForSecondsRealtime(segNegroInicial);
        yield return Fundir(0f, segFundidoInicial);
    }

    void ApagarCamaras()
    {
        if (planos == null) return;
        foreach (var p in planos) if (p.camara != null) p.camara.enabled = false;
    }

    /// <summary>true = intro (jugador quieto, HUD oculto, su cámara apagada); false = juego normal.</summary>
    void Estado(bool intro)
    {
        if (camaraJugador != null) camaraJugador.enabled = !intro;
        if (jugador != null) jugador.enabled = !intro;
        foreach (var g in ocultarDurante) if (g != null) g.SetActive(!intro);
    }

    IEnumerator Fundir(float objetivo, float seg)
    {
        if (negro == null) yield break;
        while (!Mathf.Approximately(negro.alpha, objetivo))
        {
            negro.alpha = Mathf.MoveTowards(negro.alpha, objetivo, Time.unscaledDeltaTime / Mathf.Max(0.01f, seg));
            yield return null;
        }
    }
}