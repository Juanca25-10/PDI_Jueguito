using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Recibe por UDP la probabilidad P(Cerrados) que envía ojos_server.py y la convierte
/// en eventos de juego. Solo hay que poner este script en un GameObject de la escena.
///
/// Mensaje (Tabla 8): {"seq","timestamp_ms","valid","state","p","face_score","motivo"}.
/// Se procesan todos los paquetes en orden de llegada y se descarta el que traiga un seq menor o igual
/// al último aceptado. Si el servidor se reinicia (seq que cae mucho o silencio > timeoutSinDatos) se acepta.
/// </summary>
public class OjosReceiver : MonoBehaviour
{
    [Serializable]
    class Paquete
    {
        public int seq;
        public long timestamp_ms;
        public bool valid;           // cara válida y banda de ojos recortable
        public string state;         // sin_camara | sin_rostro | abiertos | cerrados
        public float p;              // P(cerrados)
        public float face_score;     // puntuación de la U-Net en la cara
        public string motivo;
        public bool cara;            // campo del servidor anterior (respaldo)
    }

    [Header("Red (debe coincidir con --puerto de Python)")]
    public int puerto = 5005;

    [Header("Detección (mismos valores que el cuaderno)")]
    [Range(0f, 1f)] public float umbralCierre = 0.6f;    // se consideran cerrados al subir de aquí
    [Range(0f, 1f)] public float umbralApertura = 0.4f;  // se consideran abiertos al bajar de aquí
    [Range(0.05f, 1f)] public float suavizado = 0.5f;    // 1 = sin suavizar
    public float segCierreLargo = 1.0f;                  // cierre "castigable" (parpadeo < esto)
    public float timeoutSinDatos = 1.0f;                 // sin paquetes -> Python caído
    public int saltoReinicio = 1000;                     // si seq baja más que esto se asume que Python se reinició

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
    public string Motivo { get; private set; } = "";   // lo que reporta la red: ok, sin_rostro, rostro_pequeno...
    public float SegSinDatos => Time.unscaledTime - ultimoPaquete;   // muy grande = aún no llega ningún paquete

    // Datos del último paquete aceptado (Tabla 8)
    public string Estado { get; private set; } = "sin_rostro";   // sin_camara | sin_rostro | abiertos | cerrados
    public float FaceScore { get; private set; }
    public int UltimoSeq { get; private set; } = -1;
    public float LatenciaMs { get; private set; }     // reloj de Unity - timestamp_ms (mismo equipo)
    public int Aceptados { get; private set; }
    public int Descartados { get; private set; }      // fuera de orden, repetidos o inválidos

    UdpClient cliente;
    Thread hilo;
    volatile bool corriendo;
    readonly object candado = new object();
    readonly Queue<string> cola = new Queue<string>();
    const int MaxCola = 64;                        // si Unity se atrasa se descartan los más viejos
    float ultimoPaquete = -999f, tCierre;
    bool cierreLargoDisparado, primero = true;

    [Header("Calibración del menú")]
    [Tooltip("Usa los umbrales personales que guarda la pantalla de Calibración (si existen).")]
    public bool usarCalibracion = true;

    void Awake()
    {
        // Mismas claves que CuandoNadieMira.Menu.CalibrationStore
        if (usarCalibracion && PlayerPrefs.GetInt("cnm.calib.ok", 0) == 1)
        {
            umbralCierre = PlayerPrefs.GetFloat("cnm.calib.umbralCierre", umbralCierre);
            umbralApertura = Mathf.Min(PlayerPrefs.GetFloat("cnm.calib.umbralApertura", umbralApertura), umbralCierre - 0.02f);
            Debug.Log($"[OjosReceiver] Calibración aplicada: cierre {umbralCierre:F2}, apertura {umbralApertura:F2}");
        }
    }

    void OnEnable()
    {
        UltimoSeq = -1; Aceptados = Descartados = 0; primero = true;
        lock (candado) cola.Clear();
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
                string texto = Encoding.UTF8.GetString(bytes);
                lock (candado)
                {
                    cola.Enqueue(texto);
                    while (cola.Count > MaxCola) cola.Dequeue();
                }
            }
            catch (SocketException) { }       // al cerrar el socket
            catch (ObjectDisposedException) { }
        }
    }

    void Update()
    {
        float ahora = Time.unscaledTime;       // unscaled: sigue funcionando con timeScale = 0

        string[] mensajes;
        lock (candado) { mensajes = cola.ToArray(); cola.Clear(); }
        foreach (var texto in mensajes) Recibir(texto, ahora);

        if (mensajes.Length == 0 && HayRostro && ahora - ultimoPaquete > timeoutSinDatos)
        {
            Estado = "sin_camara";
            EstablecerRostro(false);           // Python se cayó o se cerró
        }

        // Cierre largo: se avisa una sola vez mientras siga cerrado
        if (Cerrados && !cierreLargoDisparado && ahora - tCierre >= segCierreLargo)
        {
            cierreLargoDisparado = true;
            OnCierreLargo.Invoke();
        }
    }

    // Valida el paquete, descarta los fuera de orden y aplica el resultado
    void Recibir(string texto, float ahora)
    {
        Paquete d = null;
        try { d = JsonUtility.FromJson<Paquete>(texto); } catch (ArgumentException) { }
        if (d == null) { Descartados++; return; }                  // no es JSON: se ignora

        bool silencio = ultimoPaquete > 0f && ahora - ultimoPaquete > timeoutSinDatos;
        bool reinicio = UltimoSeq - d.seq > saltoReinicio || silencio;
        if (d.seq <= UltimoSeq && !reinicio) { Descartados++; return; }   // llegó tarde o repetido

        UltimoSeq = d.seq;
        Aceptados++;
        ultimoPaquete = ahora;
        Motivo = d.motivo ?? "";
        FaceScore = d.face_score;
        if (d.timestamp_ms > 0)
            LatenciaMs = Mathf.Max(0f, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - d.timestamp_ms);

        bool hayCara = d.valid || d.cara;                          // "cara" = servidor anterior
        Estado = !string.IsNullOrEmpty(d.state) ? d.state : (hayCara ? "abiertos" : "sin_rostro");
        if (hayCara) { EstablecerRostro(true); Procesar(d.p, ahora); }
        else EstablecerRostro(false);
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