using System.IO;
using UnityEngine;

/// <summary>Abre ojos_server.exe al iniciar el juego y lo cierra al salir (solo Windows).</summary>
public class LanzadorPython : MonoBehaviour
{
    public bool lanzar = true;               // desmárcalo si prefieres correr Python a mano mientras pruebas
    public bool verVistaPrevia = false;      // true = muestra la ventana de la cámara (útil para depurar)
    public int puerto = 5005;                // debe coincidir con OjosReceiver
    public bool vistaEnJuego = true;         // manda la imagen de la cámara a Unity (para el RawImage con VistaCamara)
    public int puertoVista = 5006;           // debe coincidir con VistaCamara

    /// <summary>True si la imagen viene de un video de prueba (variable CNM_VIDEO_PRUEBA o -cnmVideo) y no de una cámara.</summary>
    public static bool UsandoVideoPrueba { get; private set; }

    System.Diagnostics.Process proceso;
    string exe, dir, args;

    void Awake()
    {
        UsandoVideoPrueba = false;
        if (!lanzar) return;
        if (CuandoNadieMira.Menu.SimuladorOjos.Activo) { CuandoNadieMira.Menu.SimuladorOjos.Asegurar(); return; }   // modo sin cámara

        dir = Path.Combine(Application.streamingAssetsPath, "ojos_server");
        exe = Path.Combine(dir, "ojos_server.exe");
        if (!File.Exists(exe))
        {
            Debug.LogWarning("No se encontró " + exe + ". Si ejecutas Python a mano, ignora este aviso.");
            return;
        }

        // Fuente de imagen: un video de prueba (para probar sin cámara) o la cámara elegida en Calibración
        string video = VideoPrueba();
        UsandoVideoPrueba = video != null && File.Exists(video);      // un flujo en vivo (tcp://, udp://...) cuenta como cámara
        string fuente = video != null ? $" --camara \"{video}\""
                      : PlayerPrefs.HasKey("cnm.camara") ? $" --camara {PlayerPrefs.GetInt("cnm.camara")}" : "";

        args = $"--puerto {puerto}" +
               (verVistaPrevia ? "" : " --sin-vista") +
               (vistaEnJuego ? $" --vista-puerto {puertoVista}" : "") +
               fuente;
        Abrir();
    }

    void Abrir()
    {
        proceso = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = exe,
            Arguments = args,
            WorkingDirectory = dir,
            CreateNoWindow = true,
            UseShellExecute = false
        });
    }

    // El video de prueba se acaba y el detector se cierra: se vuelve a abrir para que el video quede en bucle
    void Update()
    {
        if (VideoPrueba() != null && proceso != null && proceso.HasExited && proceso.ExitCode == 0)
        {
            proceso.Dispose();
            Abrir();
        }
    }

    static string VideoPrueba()
    {
        string env = System.Environment.GetEnvironmentVariable("CNM_VIDEO_PRUEBA");
        if (Valida(env)) return env;
        var a = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < a.Length - 1; i++)
            if (a[i] == "-cnmVideo" && Valida(a[i + 1])) return a[i + 1];
        return null;
    }

    // Un archivo de video que exista, o la dirección de un flujo en vivo (por ejemplo tcp://127.0.0.1:23000)
    static bool Valida(string fuente) => !string.IsNullOrEmpty(fuente) && (fuente.Contains("://") || File.Exists(fuente));

    void Cerrar()
    {
        if (proceso != null && !proceso.HasExited) proceso.Kill();
        proceso = null;
    }

    void OnApplicationQuit() { Cerrar(); }
    void OnDestroy() { Cerrar(); }
}
