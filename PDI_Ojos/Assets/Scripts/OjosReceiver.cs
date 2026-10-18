using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Recibe por UDP la probabilidad P(Cerrados) que envía ojos_server.py y la convierte
/// en eventos de juego. Solo hay que poner este script en un GameObject de la escena.
/// </summary>
public class OjosReceiver : MonoBehaviour
{
    [Serializable] class Paquete { public bool cara; public float p; public string motivo; }

    [Header("Red (debe coincidir con --puerto de Python)")]
    public int puerto = 5005;

    [Header("Detección (mismos valores que el cuaderno)")]
    [Range(0f, 1f)] public float umbralCierre = 0.6f;    // se consideran cerrados al subir de aquí
    [Range(0f, 1f)] public float umbralApertura = 0.4f;  // se consideran abiertos al bajar de aquí
    [Range(0.05f, 1f)] public float suavizado = 0.5f;    // 1 = sin suavizar
    public float segCierreLargo = 1.0f;                  // cierre "castigable" (parpadeo < esto)
    public float timeoutSinDatos = 1.0f;                 // sin paquetes -> Python caído

    [Header("Eventos (conéctalos en el Inspector o con AddListener)")]
    public UnityEvent OnCerrados;        // los ojos pasaron a cerrados
    public UnityEvent OnAbiertos;        // los ojos pasaron a abiertos
    public UnityEvent OnParpadeo;        // cierre corto: abrió antes de segCierreLargo
    public UnityEvent OnCierreLargo;     // sigue cerrado pasados segCierreLargo (se dispara 1 vez)
    public UnityEvent OnRostroPerdido;   // no hay rostro válido o Python dejó de enviar
    public UnityEvent OnRostroRecuperado;

    // Estado de solo lectura para usar desde otros scripts
    public float PCerrados { get; private set; }      // probabilidad suavizada, 0..1
    public bool Cerrados { get; private set; }
    public bool HayRostro { get; private set; }
    public float SegCerrados => Cerrados ? Time.unscaledTime - tCierre : 0f;
    public int Parpadeos { get; private set; }

    UdpClient cliente;
    Thread hilo;
    volatile bool corriendo;
    readonly object candado = new object();
    string pendiente;
    float ultimoPaquete = -999f, tCierre;
    bool cierreLargoDisparado, primero = true;

    void OnEnable()
    {
        cliente = new UdpClient(new IPEndPoint(IPAddress.Loopback, puerto));
        corriendo = true;
        hilo = new Thread(Escuchar) { IsBackground = true };
        hilo.Start();
    }

    void OnDisable()
    {
        corriendo = false;
        cliente?.Close();                 // desbloquea Receive()
        hilo?.Join(200);
    }

    void Escuchar()
    {
        var origen = new IPEndPoint(IPAddress.Any, 0);
        while (corriendo)
        {
            try
            {
                var bytes = cliente.Receive(ref origen);
                lock (candado) pendiente = Encoding.UTF8.GetString(bytes);   // solo importa el último
            }
            catch (SocketException) { }       // al cerrar el socket
            catch (ObjectDisposedException) { }
        }
    }

    void Update()
    {
        string msg;
        lock (candado) { msg = pendiente; pendiente = null; }

        float ahora = Time.unscaledTime;       // unscaled: sigue funcionando con timeScale = 0
        if (msg != null)
        {
            ultimoPaquete = ahora;
            var d = JsonUtility.FromJson<Paquete>(msg);
            if (d.cara) { EstablecerRostro(true); Procesar(d.p, ahora); }
            else EstablecerRostro(false);
        }
        else if (HayRostro && ahora - ultimoPaquete > timeoutSinDatos)
            EstablecerRostro(false);           // Python se cayó o se cerró

        // Cierre largo: se avisa una sola vez mientras siga cerrado
        if (Cerrados && !cierreLargoDisparado && ahora - tCierre >= segCierreLargo)
        {
            cierreLargoDisparado = true;
            OnCierreLargo.Invoke();
        }
    }

    void EstablecerRostro(bool hay)
    {
        if (hay == HayRostro) return;
        HayRostro = hay;
        (hay ? OnRostroRecuperado : OnRostroPerdido).Invoke();
    }

    void Procesar(float p, float ahora)
    {
        PCerrados = primero ? p : suavizado * p + (1f - suavizado) * PCerrados;   // media móvil exponencial
        primero = false;

        if (!Cerrados && PCerrados >= umbralCierre)             // histéresis: evita parpadeo del estado
        {
            Cerrados = true; tCierre = ahora; cierreLargoDisparado = false;
            OnCerrados.Invoke();
        }
        else if (Cerrados && PCerrados <= umbralApertura)
        {
            bool fueLargo = cierreLargoDisparado;
            Cerrados = false;
            if (!fueLargo) { Parpadeos++; OnParpadeo.Invoke(); }
            OnAbiertos.Invoke();
        }
    }
}
