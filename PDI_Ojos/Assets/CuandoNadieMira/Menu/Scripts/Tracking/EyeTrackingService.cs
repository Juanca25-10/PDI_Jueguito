using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace CuandoNadieMira.Menu
{
    public enum TrackingState
    {
        Off,            // todavía no se ha iniciado
        Starting,       // el detector arrancó y aún no manda datos
        Running,        // llegan datos
        NoSignal,       // el detector sigue abierto, pero dejó de mandar datos
        MissingExe,     // no está ojos_server.exe en StreamingAssets
        ProcessExited,  // el detector se cerró solo
        PortBusy        // otro programa ya usa el puerto UDP
    }

    /// <summary>Una lectura del detector (un paquete UDP).</summary>
    public struct TrackingSample
    {
        public float time;      // Time.unscaledTime al recibirla
        public bool face;       // hay un rostro válido
        public float p;         // P(ojos cerrados) sin suavizar
        public float score;     // confianza de la U-Net en la cara (0 a 1)
        public string reason;   // ok | sin_rostro | rostro_pequeno | varios_rostros | rostro_cortado | banda_fuera | camara_...
    }

    /// <summary>
    /// Servicio del detector para las pantallas del menú (Calibración y Preparando la experiencia).
    /// Abre ojos_server.exe, recibe sus lecturas por UDP (puerto 5005) y la imagen de la cámara (puerto 5006).
    /// Sobrevive entre escenas del menú y se apaga solo al entrar a una escena sin <see cref="EyeTrackingClient"/>,
    /// para que el juego pueda abrir su propio detector (LanzadorPython + OjosReceiver) sin chocar por la cámara o el puerto.
    /// Todo lo que muestra sale de datos reales: si no llega nada, lo dice.
    /// </summary>
    public class EyeTrackingService : MonoBehaviour
    {
        public const int DataPort = 5005;    // igual que OjosReceiver
        public const int FramePort = 5006;   // igual que VistaCamara
        const float NoSignalAfter = 2.5f;
        const int HistorySize = 900;         // ~30 s a 30 lecturas por segundo

        public static EyeTrackingService Instance { get; private set; }

        [Serializable] class Packet { public int seq; public bool cara; public float p; public string motivo; public float score; }

        // --- Estado público (solo lectura) ---------------------------------------------------
        public TrackingState State { get; private set; } = TrackingState.Off;
        public TrackingSample Last { get; private set; }
        public bool HasData => Last.time > 0f;
        public float SecondsSinceData => HasData ? Time.unscaledTime - Last.time : float.PositiveInfinity;
        public bool HasFace => State == TrackingState.Running && Last.face;
        public string Reason => State == TrackingState.Running ? Last.reason : "";
        public Texture2D Frame { get; private set; }
        public bool HasFrame => Frame != null && Time.unscaledTime - lastFrameTime < 1.5f;
        public float StartedAt { get; private set; }
        public string LastError { get; private set; } = "";
        public long TotalSamples => totalSamples;

        /// <summary>La cámara entrega imágenes (no basta con que el detector esté abierto).</summary>
        public bool CameraConnected => State == TrackingState.Running && Last.reason != "camara_no_disponible" && Last.reason != "camara_sin_imagen";

        /// <summary>No hay cámara o el detector no puede abrirla.</summary>
        public bool CameraMissing => State == TrackingState.Running && (Last.reason == "camara_no_disponible" || Last.reason == "camara_sin_imagen");

        // --- Interno ----------------------------------------------------------------------------
        System.Diagnostics.Process process;
        UdpClient dataClient, frameClient;
        Thread dataThread, frameThread;
        volatile bool listening;
        readonly ConcurrentQueue<string> incoming = new();
        readonly object frameLock = new();
        byte[] pendingFrame;
        float lastFrameTime = -999f;
        readonly TrackingSample[] history = new TrackingSample[HistorySize];
        long totalSamples;
        readonly Queue<string> stderrTail = new();
        string launchArgs = "";

        // ==========================================================================================
        // API
        // ==========================================================================================

        /// <summary>Devuelve el servicio, creándolo y arrancando el detector si hace falta.</summary>
        public static EyeTrackingService Ensure(string testVideo = null, bool drawBoxes = true)
        {
            if (Instance != null)
            {
                if (Instance.State is TrackingState.Off or TrackingState.ProcessExited or TrackingState.MissingExe or TrackingState.PortBusy)
                    Instance.Launch(testVideo, drawBoxes);
                return Instance;
            }
            var go = new GameObject("EyeTrackingService");
            DontDestroyOnLoad(go);
            var s = go.AddComponent<EyeTrackingService>();
            s.Launch(testVideo, drawBoxes);
            return s;
        }

        /// <summary>Cierra el detector y libera la cámara y los puertos (lo llama el menú antes de cargar el juego).</summary>
        public static void Shutdown()
        {
            if (Instance == null) return;
            Instance.StopAll();
            Destroy(Instance.gameObject);
            Instance = null;
        }

        /// <summary>Reinicia el detector (botón "Reintentar").</summary>
        public void Restart(string testVideo = null, bool drawBoxes = true)
        {
            StopAll();
            Launch(testVideo, drawBoxes);
        }

        /// <summary>Copia las lecturas nuevas desde <paramref name="cursor"/> (contador absoluto) y lo avanza.</summary>
        public void ReadNew(ref long cursor, List<TrackingSample> into)
        {
            if (cursor < totalSamples - HistorySize) cursor = totalSamples - HistorySize;
            for (; cursor < totalSamples; cursor++) into.Add(history[cursor % HistorySize]);
        }

        /// <summary>Cuenta las lecturas de los últimos <paramref name="seconds"/> segundos.</summary>
        public void Window(float seconds, out int count, out int withFace, out float meanScore)
        {
            count = withFace = 0;
            meanScore = 0f;
            float from = Time.unscaledTime - seconds;
            for (long i = totalSamples - 1; i >= 0 && i >= totalSamples - HistorySize; i--)
            {
                var s = history[i % HistorySize];
                if (s.time < from) break;
                count++;
                if (s.face) { withFace++; meanScore += s.score; }
            }
            if (withFace > 0) meanScore /= withFace;
        }

        /// <summary>
        /// Seguimiento estable: llegan lecturas con frecuencia, casi todas con rostro válido y con buena confianza.
        /// </summary>
        public bool IsStable(float seconds = 1.5f)
        {
            if (State != TrackingState.Running || SecondsSinceData > 0.5f) return false;
            Window(seconds, out int n, out int face, out float score);
            return n >= seconds * 6f && face >= n * 0.9f && score >= 0.75f;
        }

        /// <summary>Lecturas por segundo en el último segundo.</summary>
        public int Rate
        {
            get { Window(1f, out int n, out _, out _); return n; }
        }

        /// <summary>Confianza media del rostro en los últimos 1.5 s (0 si no hubo rostro).</summary>
        public float Confidence
        {
            get { Window(1.5f, out _, out _, out float s); return s; }
        }

        /// <summary>Últimas líneas que el detector escribió en su salida de errores (diagnóstico).</summary>
        public string StderrTail
        {
            get { lock (stderrTail) return string.Join("\n", stderrTail); }
        }

        // ==========================================================================================
        // Ciclo de vida
        // ==========================================================================================

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            StopAll();
            if (Frame != null) Destroy(Frame);
            if (Instance == this) Instance = null;
        }

        void OnApplicationQuit() => StopAll();

        // Al entrar a una escena que no usa el detector del menú (el juego, el menú principal), se apaga solo
        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            if (FindAnyObjectByType<EyeTrackingClient>() == null) Shutdown();
        }

        // ==========================================================================================
        // Proceso y sockets
        // ==========================================================================================

        bool sourceIsVideo;

        /// <summary>True si la imagen viene de un video de prueba y no de una cámara.</summary>
        public bool UsingTestVideo => sourceIsVideo;

        void Launch(string testVideo, bool drawBoxes)
        {
            StopAll();
            LastError = "";
            Last = default;
            lastFrameTime = -999f;
            StartedAt = Time.unscaledTime;
            lock (stderrTail) stderrTail.Clear();

            string dir = Path.Combine(Application.streamingAssetsPath, "ojos_server");
            string exe = Path.Combine(dir, "ojos_server.exe");
            if (!File.Exists(exe))
            {
                State = TrackingState.MissingExe;
                LastError = "No se encontró " + exe;
                Debug.LogWarning("[Cuando Nadie Mira] " + LastError + ". Descomprime ojos_server.zip en Assets/StreamingAssets/.");
                return;
            }

            // Primero los sockets: si el puerto está ocupado no tiene sentido abrir la cámara
            try
            {
                dataClient = new UdpClient(new IPEndPoint(IPAddress.Loopback, DataPort));
                frameClient = new UdpClient(new IPEndPoint(IPAddress.Loopback, FramePort));
                frameClient.Client.ReceiveBufferSize = 1 << 20;   // las imágenes son datagramas grandes
            }
            catch (SocketException e)
            {
                CloseSockets();
                State = TrackingState.PortBusy;
                LastError = "El puerto " + DataPort + " o " + FramePort + " ya está en uso (" + e.SocketErrorCode + ").";
                Debug.LogWarning("[Cuando Nadie Mira] " + LastError);
                return;
            }

            listening = true;
            dataThread = new Thread(() => Listen(dataClient, false)) { IsBackground = true, Name = "CNM data" };
            frameThread = new Thread(() => Listen(frameClient, true)) { IsBackground = true, Name = "CNM frames" };
            dataThread.Start();
            frameThread.Start();

            string source = ResolveSource(testVideo);
            sourceIsVideo = source != null && File.Exists(source);
            launchArgs = $"--carpeta \"{dir}\" --puerto {DataPort} --sin-vista --vista-puerto {FramePort} --vista-ancho 480 --vista-fps 15"
                         + (drawBoxes ? " --vista-cajas" : "")
                         + (source != null ? $" --camara \"{source}\"" : "");
            if (SimuladorOjos.Activo) { SimuladorOjos.Asegurar(); return; }   // modo sin cámara: Unity hace de detector
            StartProcess(exe, dir);
        }

        string exePath, exeDir;

        void StartProcess(string exe, string dir)
        {
            exePath = exe;
            exeDir = dir;
            try
            {
                process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = exe,
                        Arguments = launchArgs,
                        WorkingDirectory = dir,
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true,
                    },
                    EnableRaisingEvents = true,
                };
                process.ErrorDataReceived += (_, e) => Remember(e.Data);
                process.OutputDataReceived += (_, e) => Remember(e.Data);
                process.Start();
                process.BeginErrorReadLine();
                process.BeginOutputReadLine();
                if (State != TrackingState.Running) State = TrackingState.Starting;
            }
            catch (Exception e)
            {
                State = TrackingState.ProcessExited;
                LastError = "No se pudo abrir el detector: " + e.Message;
                Debug.LogWarning("[Cuando Nadie Mira] " + LastError);
            }
        }

        /// <summary>
        /// Fuente de imagen: un video de prueba (argumento, variable de entorno CNM_VIDEO_PRUEBA o -cnmVideo en la línea
        /// de comandos) o el índice de cámara elegido en Calibración.
        /// </summary>
        static string ResolveSource(string testVideo)
        {
            if (Valid(testVideo)) return testVideo;
            string env = Environment.GetEnvironmentVariable("CNM_VIDEO_PRUEBA");
            if (Valid(env)) return env;
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-cnmVideo" && Valid(args[i + 1])) return args[i + 1];
            return CalibrationStore.HasCameraChoice ? CalibrationStore.CameraIndex.ToString() : null;
        }

        // Un archivo de video que exista, o la dirección de un flujo en vivo (por ejemplo tcp://127.0.0.1:23000)
        static bool Valid(string source) => !string.IsNullOrEmpty(source) && (source.Contains("://") || File.Exists(source));

        void Remember(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            lock (stderrTail)
            {
                stderrTail.Enqueue(line);
                while (stderrTail.Count > 12) stderrTail.Dequeue();
            }
        }

        void Listen(UdpClient client, bool frames)
        {
            var from = new IPEndPoint(IPAddress.Any, 0);
            while (listening)
            {
                try
                {
                    byte[] bytes = client.Receive(ref from);
                    if (frames) { lock (frameLock) pendingFrame = bytes; }          // solo importa la última imagen
                    else incoming.Enqueue(Encoding.UTF8.GetString(bytes));
                }
                catch (SocketException) { }
                catch (ObjectDisposedException) { break; }
            }
        }

        void StopAll()
        {
            listening = false;
            CloseSockets();
            dataThread?.Join(200);
            frameThread?.Join(200);
            dataThread = frameThread = null;
            while (incoming.TryDequeue(out _)) { }

            if (process != null)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                        process.WaitForExit(2000);   // libera la cámara antes de que el juego abra la suya
                    }
                }
                catch (Exception) { /* ya se cerró */ }
                process.Dispose();
                process = null;
            }
            if (State != TrackingState.MissingExe && State != TrackingState.PortBusy) State = TrackingState.Off;
        }

        void CloseSockets()
        {
            dataClient?.Close();
            frameClient?.Close();
            dataClient = frameClient = null;
        }

        // ==========================================================================================
        // Lectura en el hilo principal
        // ==========================================================================================

        void Update()
        {
            float now = Time.unscaledTime;

            while (incoming.TryDequeue(out string json))
            {
                Packet pk;
                try { pk = JsonUtility.FromJson<Packet>(json); }
                catch (Exception) { continue; }
                if (pk == null) continue;
                var s = new TrackingSample { time = now, face = pk.cara, p = Mathf.Clamp01(pk.p), score = pk.score, reason = pk.motivo ?? "" };
                history[totalSamples % HistorySize] = s;
                totalSamples++;
                Last = s;
                State = TrackingState.Running;
            }

            byte[] bytes;
            lock (frameLock) { bytes = pendingFrame; pendingFrame = null; }
            if (bytes != null)
            {
                if (Frame == null) Frame = new Texture2D(2, 2, TextureFormat.RGB24, false) { name = "CNM_CameraFrame", wrapMode = TextureWrapMode.Clamp };
                if (Frame.LoadImage(bytes, false)) lastFrameTime = now;
            }

            if (process != null)
            {
                bool exited;
                try { exited = process.HasExited; } catch (Exception) { exited = true; }
                if (exited && State is TrackingState.Starting or TrackingState.Running or TrackingState.NoSignal)
                {
                    int code = -1;
                    try { code = process.ExitCode; } catch (Exception) { }
                    if (code == 0 && sourceIsVideo)
                    {
                        // Modo prueba: el video terminó y se vuelve a empezar (así se puede calibrar sin cámara).
                        // Solo se reinicia el proceso: los sockets siguen abiertos y no se pierde el puerto.
                        process.Dispose();
                        process = null;
                        StartProcess(exePath, exeDir);
                        return;
                    }
                    State = TrackingState.ProcessExited;
                    LastError = "El detector se cerró (código " + code + ").";
                    Debug.LogWarning("[Cuando Nadie Mira] " + LastError + "\n" + StderrTail);
                }
            }

            if (State == TrackingState.Running && now - Last.time > NoSignalAfter) State = TrackingState.NoSignal;
        }
    }
}
