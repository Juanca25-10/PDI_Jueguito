using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Muestra tu cámara dentro del juego (en un RawImage de tu Canvas), en vez de en una ventana aparte.
/// ojos_server.exe manda la imagen por UDP al puerto 5006 (LanzadorPython lo activa con "Vista En Juego").
/// Ponlo en el RawImage (o en cualquier objeto y arrastra el RawImage). Si no llegan imágenes, el RawImage se oculta.
/// </summary>
public class VistaCamara : MonoBehaviour
{
    public RawImage destino;
    public int puerto = 5006;                 // debe coincidir con "Puerto Vista" de LanzadorPython
    public float segSinImagen = 1.5f;         // sin imágenes durante este tiempo, se oculta

    UdpClient cliente;
    Thread hilo;
    volatile bool corriendo;
    readonly object candado = new object();
    byte[] pendiente;
    Texture2D textura;
    float tUltima = -999f;

    void Reset() { destino = GetComponent<RawImage>(); }

    void OnEnable()
    {
        cliente = new UdpClient(new IPEndPoint(IPAddress.Loopback, puerto));
        cliente.Client.ReceiveBufferSize = 1 << 20;      // las imágenes son datagramas grandes
        textura = new Texture2D(2, 2, TextureFormat.RGB24, false);
        if (destino != null) destino.texture = textura;
        corriendo = true;
        hilo = new Thread(Escuchar) { IsBackground = true };
        hilo.Start();
    }

    void OnDisable()
    {
        corriendo = false;
        cliente?.Close();
        hilo?.Join(200);
        if (textura != null) Destroy(textura);
    }

    void Escuchar()
    {
        var origen = new IPEndPoint(IPAddress.Any, 0);
        while (corriendo)
        {
            try
            {
                byte[] bytes = cliente.Receive(ref origen);
                lock (candado) pendiente = bytes;          // solo importa la última imagen
            }
            catch (SocketException) { }
            catch (System.ObjectDisposedException) { }
        }
    }

    void Update()
    {
        byte[] bytes;
        lock (candado) { bytes = pendiente; pendiente = null; }
        if (bytes != null && textura.LoadImage(bytes)) tUltima = Time.unscaledTime;

        if (destino != null) destino.enabled = Time.unscaledTime - tUltima < segSinImagen;
    }
}
