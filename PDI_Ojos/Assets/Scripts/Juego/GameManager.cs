using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Reglas del juego "Cuando nadie mira". No dibuja nada: la interfaz es HudJuego.
///
///  EXPLORANDO : recorres el museo; parpadear solo muestra los párpados en pantalla, no afecta nada.
///  JUGANDO    : ojos CERRADOS -> sube la barra "hasta el amanecer" y las estatuas avanzan una pose cada cierto tiempo.
///               ojos ABIERTOS -> (tras 1.5 s) la barra baja despacio y las estatuas retroceden una pose cada cierto tiempo.
///  VICTORIA   : barra al 100 %.
///  DERROTA    : una estatua llega a su última pose, o la barra llega a 0.
/// Sin rostro válido la partida se PAUSA (nunca se interpreta como ojos cerrados).
/// Las estatuas se detectan solas (Estatua.Todas): agregar o quitar una no requiere tocar este script.
/// </summary>
public class GameManager : MonoBehaviour
{
    public enum Fase { Explorando, Jugando, Victoria, Derrota }

    [Header("Referencias")]
    public OjosReceiver ojos;
    public ControlJugador jugador;
    public HudJuego hud;
    public EfectoVela efectos;
    public ParpadeoPantalla parpadeo;
    [Tooltip("Objeto vacío junto al escritorio: ahí se pulsa E para empezar el turno.")]
    public Transform puntoInicio;
    [Tooltip("Objeto vacío donde queda el jugador al empezar el turno (su posición y hacia dónde mira). Ya no podrá caminar, solo girar.")]
    public Transform puestoGuarda;

    [Header("Prueba sin cámara")]
    [Tooltip("Mantén pulsada la tecla = ojos cerrados.")]
    public bool modoTeclado = false;
    public Key teclaCerrarOjos = Key.C;

    [Header("Barra 'hasta el amanecer' (0 a 100)")]
    [Range(0f, 100f)] public float progresoInicial = 15f;
    [Tooltip("Segundos con los ojos CERRADOS para llenar la barra de 0 a 100. Para ganar hacen falta (100 - progresoInicial)% de esto.")]
    public float segCerradosParaLlenar = 80f;
    [Tooltip("Segundos con los ojos ABIERTOS (ya sostenidos) para vaciar la barra de 100 a 0.")]
    public float segAbiertosParaVaciar = 200f;

    [Header("Lectura de los ojos")]
    public float segCierreMinimo = 0.35f;       // un parpadeo más corto que esto no cuenta como "cerrado"
    public float segAbiertoSostenido = 1.5f;    // ojos abiertos este tiempo -> baja la barra y empiezan a retroceder las estatuas

    [Header("Inicio del turno")]
    public Key teclaInteractuar = Key.E;
    public float radioInicio = 2.5f;

    [Header("Final")]
    public float segHastaPantallaFinal = 1.5f;  // espera antes de mostrar el panel de victoria o derrota
    public Key teclaReiniciar = Key.R;

    [Header("Alarmas falsas (suenan con los ojos cerrados y no avanza nadie)")]
    public AudioClip[] sonidosAlarmaFalsa;
    public Vector2 intervaloAlarmaFalsa = new Vector2(7f, 15f);
    public Vector2 distanciaAlarmaFalsa = new Vector2(7f, 14f);
    [Range(0f, 1f)] public float volumenAlarmaFalsa = 0.6f;

    [Header("Otros sonidos (opcionales)")]
    public AudioClip sonidoDerrota;
    public AudioClip sonidoVictoria;

    [Header("Depuración")]
    public bool mostrarDepuracion = true;

    public static GameManager Instancia { get; private set; }
    public Fase fase { get; private set; } = Fase.Explorando;
    public float progreso { get; private set; }

    float tAbierto, tAlarma, tTeclaCerrada, tFin;
    bool pausadoSinRostro, finalMostrado;
    Estatua culpable;

    // ---------------------------------------------------------------- entrada y ojos (cámara o teclado)
    bool Pulsada(Key k) => Keyboard.current != null && Keyboard.current[k].isPressed;
    bool SePulso(Key k) => Keyboard.current != null && Keyboard.current[k].wasPressedThisFrame;

    bool HayRostro => modoTeclado || (ojos != null && ojos.HayRostro);
    bool CerradosCrudo => modoTeclado ? Pulsada(teclaCerrarOjos) : (ojos != null && ojos.Cerrados);
    float SegCerrados => modoTeclado ? tTeclaCerrada : (ojos != null ? ojos.SegCerrados : 0f);

    // ---------------------------------------------------------------- ciclo de vida
    void Awake() { Instancia = this; }
    void OnEnable() { Estatua.LlegoAlFinal += AlLlegarAlFinal; }
    void OnDisable() { Estatua.LlegoAlFinal -= AlLlegarAlFinal; }

    void AlLlegarAlFinal(Estatua e) { if (fase == Fase.Jugando) Perder(e); }

    void Start()
    {
        if (hud == null) hud = gameObject.AddComponent<HudJuego>();      // HUD vacío: todo son no-ops
        ReiniciarTodo();
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (modoTeclado) tTeclaCerrada = Pulsada(teclaCerrarOjos) ? tTeclaCerrada + dt : 0f;

        ActualizarParpadeoVisual();

        // Los avisos de rostro funcionan al explorar y durante el turno
        if (fase == Fase.Explorando || fase == Fase.Jugando) hud.AvisoSinRostro(!HayRostro);

        switch (fase)
        {
            case Fase.Explorando:
                bool cerca = CercaDelInicio();
                hud.AvisoInicio(cerca);
                if (cerca && SePulso(teclaInteractuar)) IniciarTurno();
                break;
            case Fase.Jugando:
                Jugar(dt);
                break;
            case Fase.Victoria:
            case Fase.Derrota:
                ActualizarFinal();
                break;
        }

        if (mostrarDepuracion) hud.Depuracion(TextoDepuracion());
    }

    // Los párpados en pantalla funcionan al explorar y durante el turno; en victoria o derrota quedan abiertos.
    void ActualizarParpadeoVisual()
    {
        if (parpadeo == null) return;
        bool abrirForzado = fase == Fase.Victoria || fase == Fase.Derrota;
        parpadeo.usarPruebaManual = modoTeclado || abrirForzado;
        parpadeo.pruebaManual = (modoTeclado && !abrirForzado && CerradosCrudo) ? 1f : 0f;
    }

    bool CercaDelInicio()
    {
        if (puntoInicio == null || jugador == null) return true;       // sin punto: vale desde cualquier sitio
        return Vector3.Distance(jugador.transform.position, puntoInicio.position) <= radioInicio;
    }

    // ---------------------------------------------------------------- partida
    public void IniciarTurno()
    {
        fase = Fase.Jugando;
        progreso = progresoInicial;
        tAbierto = 0f;
        culpable = null;
        finalMostrado = false;
        foreach (var e in Estatua.Todas) e.Reiniciar();
        if (efectos != null) efectos.Reiniciar();
        if (jugador != null)
        {
            if (puestoGuarda != null) jugador.FijarEn(puestoGuarda);
            else jugador.puedeCaminar = false;
        }
        tAlarma = Random.Range(intervaloAlarmaFalsa.x, intervaloAlarmaFalsa.y);
        hud.MostrarFase(fase);
        hud.Barra(progreso);
    }

    void Jugar(float dt)
    {
        pausadoSinRostro = !HayRostro;
        if (pausadoSinRostro) return;                                   // sin rostro: todo se detiene

        bool cerrado = CerradosCrudo && SegCerrados >= segCierreMinimo;

        if (cerrado)
        {
            tAbierto = 0f;
            progreso += 100f / segCerradosParaLlenar * dt;

            foreach (var e in Estatua.Todas.ToArray())       // copia: si una estatua llega al final, Perder() cambia la fase
            {
                e.CancelarRetroceso();
                if (progreso >= e.activaDesdeProgreso) e.Avanzar(dt);
                if (fase != Fase.Jugando) return;
            }

            tAlarma -= dt;
            if (tAlarma <= 0f)
            {
                AlarmaFalsa();
                tAlarma = Random.Range(intervaloAlarmaFalsa.x, intervaloAlarmaFalsa.y);
            }
        }
        else if (!CerradosCrudo)                                        // ojos realmente abiertos (no a mitad de un parpadeo)
        {
            tAbierto += dt;
            if (tAbierto >= segAbiertoSostenido)
            {
                progreso -= 100f / segAbiertosParaVaciar * dt;
                foreach (var e in Estatua.Todas) e.RetrocederPorTiempo(dt);
            }
        }

        progreso = Mathf.Clamp(progreso, 0f, 100f);
        hud.Barra(progreso);
        if (progreso >= 100f) Ganar();
        else if (progreso <= 0f) Perder(null);
    }

    void AlarmaFalsa()
    {
        if (sonidosAlarmaFalsa == null || sonidosAlarmaFalsa.Length == 0 || jugador == null) return;
        var clip = sonidosAlarmaFalsa[Random.Range(0, sonidosAlarmaFalsa.Length)];
        float dist = Random.Range(distanciaAlarmaFalsa.x, distanciaAlarmaFalsa.y);
        Vector3 pos = jugador.transform.position + Quaternion.Euler(0f, Random.value * 360f, 0f) * Vector3.forward * dist;
        Sonido3D.Reproducir(clip, pos, volumenAlarmaFalsa);
    }

    void Ganar()
    {
        fase = Fase.Victoria;
        tFin = Time.time;
        hud.MostrarFase(fase);
        if (efectos != null) efectos.Amanecer();
        Sonar(sonidoVictoria);
    }

    void Perder(Estatua fig)
    {
        fase = Fase.Derrota;
        tFin = Time.time;
        culpable = fig;
        hud.MostrarFase(fase);
        if (jugador != null)
        {
            jugador.congelado = true;
            if (fig != null) jugador.MirarHacia(fig.Posicion + Vector3.up * 1.4f);
        }
        Sonar(sonidoDerrota);
    }

    void ActualizarFinal()
    {
        if (!finalMostrado && Time.time - tFin >= segHastaPantallaFinal)
        {
            finalMostrado = true;
            if (fase == Fase.Victoria) hud.MostrarVictoria(); else hud.MostrarDerrota(culpable);
        }
        if (finalMostrado && SePulso(teclaReiniciar)) ReiniciarTodo();
    }

    void Sonar(AudioClip clip)
    {
        if (clip == null) return;
        Vector3 pos = jugador != null ? jugador.transform.position : transform.position;
        Sonido3D.Reproducir(clip, pos, 1f, 50f);
    }

    public void ReiniciarTodo()
    {
        fase = Fase.Explorando;
        progreso = 0f;
        tAbierto = 0f;
        culpable = null;
        finalMostrado = false;
        pausadoSinRostro = false;
        foreach (var e in Estatua.Todas) e.Reiniciar();
        if (efectos != null) efectos.Reiniciar();
        if (jugador != null) jugador.Reiniciar();
        hud.MostrarFase(fase);
        hud.Barra(0f);
    }

    string TextoDepuracion()
    {
        string s = "fase: " + fase + (modoTeclado ? "  [MODO TECLADO: mantén " + teclaCerrarOjos + "]" : "") +
                   "\nrostro: " + HayRostro + "  cerrados: " + CerradosCrudo + "  P=" + (ojos != null ? ojos.PCerrados.ToString("F2") : "-") +
                   "\nprogreso: " + progreso.ToString("F1") + "   abierto hace: " + tAbierto.ToString("F1") + " s";
        foreach (var e in Estatua.Todas) s += "\n" + e.nombre + ": pose " + e.PoseActual + "/" + e.PoseFinal;
        return s;
    }
}