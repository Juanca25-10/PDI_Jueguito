using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Guarda en primera persona con el Input System NUEVO (sin Input Actions: lee teclado y mouse directo).
/// WASD = caminar, mouse = mirar. Va en el objeto "Jugador" (con CharacterController); la cámara es su hija.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class ControlJugador : MonoBehaviour
{
    public Transform camara;
    public float velocidad = 3.2f;
    public float velocidadCorrer = 5.5f;     // Shift izquierdo
    public float alturaSalto = 0.9f;         // Espacio
    public float sensibilidad = 0.1f;        // el mouse nuevo da píxeles por fotograma
    public float gravedad = -15f;
    public float limiteVertical = 80f;
    public bool puedeCaminar = true;         // false = se queda en su sitio pero puede girar la cabeza
    public bool congelado;                   // true = no camina ni mira (derrota)

    [Header("Sensación de cámara")]
    public bool movimientoCamara = true;
    public float alturaBob = 0.045f;         // sube y baja al caminar (metros)
    public float lateralBob = 0.025f;        // balanceo de lado a lado (metros)
    public float rolLateral = 1.8f;          // grados que se inclina al moverse de lado
    public float respiracion = 0.004f;       // vaivén lento cuando está quieto (metros)
    public float fovCorrer = 6f;             // grados extra de campo de visión al correr
    [HideInInspector] public Vector3 sacudida;   // lo usa PulsoPantalla para el temblor

    [Header("Sonidos (todos opcionales)")]
    public AudioSource fuente;               // 2D; si lo dejas vacío se crea uno solo
    public AudioClip[] pasos;                // se elige uno al azar en cada paso
    public float metrosPorPaso = 1.7f;
    public AudioClip salto;
    public AudioClip aterrizaje;
    public AudioClip parpadeo;               // el mini sonido al pestañear
    [Range(0f, 1f)] public float volumenPasos = 0.5f;
    [Range(0f, 1f)] public float volumenSalto = 0.6f;
    [Range(0f, 1f)] public float volumenParpadeo = 0.25f;

    CharacterController cc;
    Camera camaraComp;
    Vector3 posCamaraBase;
    float fovBase, amplitudBob, cicloBob, roll, hundimiento, entradaLateral;
    OjosReceiver ojos;
    float distanciaPaso, caidaMaxima;
    bool enSueloAntes = true;
    int ultimoPaso = -1;
    float cabeceo, velocidadY;
    bool encarando;
    Vector3 puntoEncarado;
    Vector3 posInicial;
    Quaternion rotInicial;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        posInicial = transform.position;
        rotInicial = transform.rotation;
        if (camara == null && Camera.main != null) camara = Camera.main.transform;
        posCamaraBase = camara.localPosition;
        camaraComp = camara.GetComponent<Camera>();
        if (camaraComp != null) fovBase = camaraComp.fieldOfView;
        if (fuente == null)
        {
            fuente = gameObject.AddComponent<AudioSource>();
            fuente.playOnAwake = false;
            fuente.spatialBlend = 0f;
        }
        BloquearCursor();
    }

    void OnEnable()
    {
        ojos = FindFirstObjectByType<OjosReceiver>();
        if (ojos != null) ojos.OnParpadeo.AddListener(SonarParpadeo);
    }

    void OnDisable()
    {
        if (ojos != null) ojos.OnParpadeo.RemoveListener(SonarParpadeo);
    }

    void Update()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) BloquearCursor();

        if (encarando) GirarHaciaPunto();
        else if (!congelado) Mirar();

        Mover();
        ActualizarCamara();
    }

    void Mirar()
    {
        if (Mouse.current == null) return;
        Vector2 delta = Mouse.current.delta.ReadValue() * sensibilidad;
        transform.Rotate(0f, delta.x, 0f);
        cabeceo = Mathf.Clamp(cabeceo - delta.y, -limiteVertical, limiteVertical);
        AplicarRotacion();
    }

    void Mover()
    {
        Vector3 horizontal = Vector3.zero;
        entradaLateral = 0f;
        var teclado = Keyboard.current;
        if (puedeCaminar && !congelado && !encarando && teclado != null)
        {
            float x = (teclado.dKey.isPressed ? 1f : 0f) - (teclado.aKey.isPressed ? 1f : 0f);
            float z = (teclado.wKey.isPressed ? 1f : 0f) - (teclado.sKey.isPressed ? 1f : 0f);
            entradaLateral = x;
            float rapidez = teclado.leftShiftKey.isPressed ? velocidadCorrer : velocidad;
            horizontal = Vector3.ClampMagnitude(transform.right * x + transform.forward * z, 1f) * rapidez;
        }

        if (cc.isGrounded && velocidadY < 0f) velocidadY = -2f;
        if (cc.isGrounded && puedeCaminar && !congelado && !encarando && teclado != null && teclado.spaceKey.wasPressedThisFrame)
        {
            velocidadY = Mathf.Sqrt(alturaSalto * -2f * gravedad);
            Sonar(salto, volumenSalto);
        }
        velocidadY += gravedad * Time.deltaTime;
        cc.Move((horizontal + Vector3.up * velocidadY) * Time.deltaTime);
        ActualizarSonidos();
    }

    void ActualizarSonidos()
    {
        bool enSuelo = cc.isGrounded;

        if (!enSuelo) caidaMaxima = Mathf.Min(caidaMaxima, velocidadY);
        if (enSuelo && !enSueloAntes)
        {
            if (caidaMaxima < -3f)                                     // solo si venía cayendo de verdad
            {
                Sonar(aterrizaje, volumenSalto);
                hundimiento = Mathf.Clamp(-caidaMaxima * 0.012f, 0f, 0.12f);   // la cámara se hunde un poco
            }
            caidaMaxima = 0f;
            distanciaPaso = 0f;
        }
        enSueloAntes = enSuelo;

        if (!enSuelo || pasos == null || pasos.Length == 0) return;
        Vector3 v = cc.velocity; v.y = 0f;                              // movimiento real (si choca con una pared, no suena)
        distanciaPaso += v.magnitude * Time.deltaTime;
        if (distanciaPaso >= metrosPorPaso)
        {
            distanciaPaso = 0f;
            int i = Random.Range(0, pasos.Length);
            if (pasos.Length > 1 && i == ultimoPaso) i = (i + 1) % pasos.Length;
            ultimoPaso = i;
            Sonar(pasos[i], volumenPasos, 0.06f);
        }
    }

    void SonarParpadeo() { Sonar(parpadeo, volumenParpadeo, 0.1f); }

    void Sonar(AudioClip clip, float volumen, float variacionTono = 0.04f)
    {
        if (clip == null || fuente == null) return;
        fuente.pitch = 1f + Random.Range(-variacionTono, variacionTono);
        fuente.PlayOneShot(clip, volumen);
    }

    void AplicarRotacion()
    {
        camara.localRotation = Quaternion.Euler(cabeceo + hundimiento * 20f, 0f, roll);
    }

    void ActualizarCamara()
    {
        float dt = Time.deltaTime;
        Vector3 v = cc.velocity; v.y = 0f;
        float rapidez = v.magnitude;
        bool corriendo = rapidez > velocidad * 1.2f;

        float objetivoAmp = (movimientoCamara && cc.isGrounded)
            ? Mathf.Clamp01(rapidez / velocidad) * (corriendo ? 1.5f : 1f) : 0f;
        amplitudBob = Mathf.MoveTowards(amplitudBob, objetivoAmp, 6f * dt);
        cicloBob += rapidez * dt / (metrosPorPaso * 2f);          // un vaivén por paso, igual que el sonido
        float quieto = 1f - Mathf.Clamp01(amplitudBob);

        float y = Mathf.Sin(cicloBob * 4f * Mathf.PI) * alturaBob * amplitudBob
                + Mathf.Sin(Time.time * 1.4f) * respiracion * quieto * (movimientoCamara ? 1f : 0f)
                - hundimiento;
        float x = Mathf.Sin(cicloBob * 2f * Mathf.PI) * lateralBob * amplitudBob;
        camara.localPosition = posCamaraBase + new Vector3(x, y, 0f) + sacudida;

        float rolObjetivo = movimientoCamara
            ? -entradaLateral * rolLateral * (cc.isGrounded ? 1f : 0.5f)
              + Mathf.Sin(cicloBob * 2f * Mathf.PI) * 0.4f * amplitudBob
            : 0f;
        roll = Mathf.Lerp(roll, rolObjetivo, 1f - Mathf.Exp(-8f * dt));
        hundimiento = Mathf.Lerp(hundimiento, 0f, 1f - Mathf.Exp(-9f * dt));
        AplicarRotacion();

        if (camaraComp != null)
        {
            float fovObjetivo = fovBase + ((movimientoCamara && corriendo && amplitudBob > 0.5f) ? fovCorrer : 0f);
            camaraComp.fieldOfView = Mathf.Lerp(camaraComp.fieldOfView, fovObjetivo, 1f - Mathf.Exp(-6f * dt));
        }
    }

    void GirarHaciaPunto()
    {
        Vector3 dir = puntoEncarado - camara.position;
        Vector3 plano = new Vector3(dir.x, 0f, dir.z);
        if (plano.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(plano), 6f * Time.deltaTime);
        float objetivo = -Mathf.Atan2(dir.y, plano.magnitude) * Mathf.Rad2Deg;
        cabeceo = Mathf.Lerp(cabeceo, objetivo, 6f * Time.deltaTime);
        AplicarRotacion();
    }

    static void BloquearCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    /// <summary>Gira la cámara hacia un punto (la derrota encara a la estatua).</summary>
    public void MirarHacia(Vector3 punto) { puntoEncarado = punto; encarando = true; }

    /// <summary>Lo coloca en un punto y deja de poder caminar (el puesto del guarda al empezar el turno).</summary>
    public void FijarEn(Transform punto)
    {
        Teletransportar(punto.position, punto.rotation);
        puedeCaminar = false;
    }

    /// <summary>Vuelve al punto de partida y devuelve el control.</summary>
    public void Reiniciar()
    {
        encarando = false;
        congelado = false;
        puedeCaminar = true;
        Teletransportar(posInicial, rotInicial);
    }

    void Teletransportar(Vector3 posicion, Quaternion rotacion)
    {
        cc.enabled = false;                  // necesario para teletransportar un CharacterController
        transform.SetPositionAndRotation(posicion, rotacion);
        cc.enabled = true;
        velocidadY = 0f;
        cabeceo = 0f;
        roll = 0f;
        AplicarRotacion();
    }
}