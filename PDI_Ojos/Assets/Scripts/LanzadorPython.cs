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

    System.Diagnostics.Process proceso;

    void Awake()
    {
        if (!lanzar) return;

        string dir = Path.Combine(Application.streamingAssetsPath, "ojos_server");
        string exe = Path.Combine(dir, "ojos_server.exe");
        if (!File.Exists(exe))
        {
            Debug.LogWarning("No se encontró " + exe + ". Si ejecutas Python a mano, ignora este aviso.");
            return;
        }

        string args = $"--puerto {puerto}" +
                      (verVistaPrevia ? "" : " --sin-vista") +
                      (vistaEnJuego ? $" --vista-puerto {puertoVista}" : "") +
                      // Cámara elegida en la pantalla de Calibración (si hay más de una)
                      (PlayerPrefs.HasKey("cnm.camara") ? $" --camara {PlayerPrefs.GetInt("cnm.camara")}" : "");

        proceso = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = exe,
            Arguments = args,
            WorkingDirectory = dir,
            CreateNoWindow = true,
            UseShellExecute = false
        });
    }

    void Cerrar()
    {
        if (proceso != null && !proceso.HasExited) proceso.Kill();
        proceso = null;
    }

    void OnApplicationQuit() { Cerrar(); }
    void OnDestroy() { Cerrar(); }
}