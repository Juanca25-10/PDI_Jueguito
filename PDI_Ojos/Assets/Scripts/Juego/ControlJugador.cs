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

    CharacterController cc;
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
        BloquearCursor();
    }

    void Update()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) BloquearCursor();

        if (encarando) GirarHaciaPunto();
        else if (!congelado) Mirar();

        Mover();
    }

    void Mirar()
    {
        if (Mouse.current == null) return;
        Vector2 delta = Mouse.current.delta.ReadValue() * sensibilidad;
        transform.Rotate(0f, delta.x, 0f);
        cabeceo = Mathf.Clamp(cabeceo - delta.y, -limiteVertical, limiteVertical);
        camara.localRotation = Quaternion.Euler(cabeceo, 0f, 0f);
    }

    void Mover()
    {
        Vector3 horizontal = Vector3.zero;
        var teclado = Keyboard.current;
        if (puedeCaminar && !congelado && !encarando && teclado != null)
        {
            float x = (teclado.dKey.isPressed ? 1f : 0f) - (teclado.aKey.isPressed ? 1f : 0f);
            float z = (teclado.wKey.isPressed ? 1f : 0f) - (teclado.sKey.isPressed ? 1f : 0f);
            float rapidez = teclado.leftShiftKey.isPressed ? velocidadCorrer : velocidad;
            horizontal = Vector3.ClampMagnitude(transform.right * x + transform.forward * z, 1f) * rapidez;
        }

        if (cc.isGrounded && velocidadY < 0f) velocidadY = -2f;
        if (cc.isGrounded && puedeCaminar && !congelado && !encarando && teclado != null && teclado.spaceKey.wasPressedThisFrame)
            velocidadY = Mathf.Sqrt(alturaSalto * -2f * gravedad);
        velocidadY += gravedad * Time.deltaTime;
        cc.Move((horizontal + Vector3.up * velocidadY) * Time.deltaTime);
    }

    void GirarHaciaPunto()
    {
        Vector3 dir = puntoEncarado - camara.position;
        Vector3 plano = new Vector3(dir.x, 0f, dir.z);
        if (plano.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(plano), 6f * Time.deltaTime);
        float objetivo = -Mathf.Atan2(dir.y, plano.magnitude) * Mathf.Rad2Deg;
        cabeceo = Mathf.Lerp(cabeceo, objetivo, 6f * Time.deltaTime);
        camara.localRotation = Quaternion.Euler(cabeceo, 0f, 0f);
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
        camara.localRotation = Quaternion.identity;
    }
}