using System;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CuandoNadieMira.Menu
{
    /// <summary>
    /// Modo sin cámara: ocupa el lugar de ojos_server y manda por UDP los mismos paquetes que él
    /// (puerto 5005) y una imagen sencilla de un ojo (puerto 5006). Así calibración, HUD y juego funcionan igual.
    ///   C (mantener) = ojos cerrados      F = alternar "sin rostro"
    /// Se activa con el menú Cuando Nadie Mira → Modo sin cámara (teclado), con la variable de entorno
    /// CNM_TECLADO=1 o lanzando el juego con -cnmTeclado.
    /// </summary>
    public class SimuladorOjos : MonoBehaviour
    {
        public const string PrefKey = "cnm.simTeclado";
        const int DataPort = 5005, FramePort = 5006;

        static SimuladorOjos inst;
        UdpClient udp;
        long seq;
        float p = 0.03f, sigPaquete, sigImagen;
        bool sinRostro;
        Texture2D img;

        public static bool Activo
        {
            get
            {
                if (Environment.GetEnvironmentVariable("CNM_TECLADO") == "1") return true;
                foreach (var a in Environment.GetCommandLineArgs()) if (a == "-cnmTeclado") return true;
#if UNITY_EDITOR
                if (UnityEditor.EditorPrefs.GetBool(PrefKey, false)) return true;
#endif
                return false;
            }
        }

        public static bool Cerrados => inst != null && inst.p > 0.5f;
        public static bool SinRostro => inst != null && inst.sinRostro;

        /// <summary>Crea el simulador si no existe (lo llaman quienes normalmente abrirían ojos_server).</summary>
        public static void Asegurar()
        {
            if (inst != null) return;
            var go = new GameObject("[SimuladorOjos]");
            DontDestroyOnLoad(go);
            inst = go.AddComponent<SimuladorOjos>();
            Debug.Log("[Cuando Nadie Mira] Modo sin cámara: mantén C para cerrar los ojos, F para simular que no hay rostro.");
        }

        void Awake()
        {
            udp = new UdpClient();
            img = new Texture2D(320, 180, TextureFormat.RGB24, false);
        }

        void OnDestroy()
        {
            udp?.Close();
            if (img != null) Destroy(img);
            if (inst == this) inst = null;
        }

        void Update()
        {
            var kb = Keyboard.current;
            bool cerrar = kb != null && kb.cKey.isPressed;
            if (kb != null && kb.fKey.wasPressedThisFrame) sinRostro = !sinRostro;

            // Transición rápida pero no instantánea, como la red real
            float objetivo = cerrar ? 0.96f : 0.04f;
            p = Mathf.MoveTowards(p, objetivo, Time.unscaledDeltaTime * 9f);

            if (Time.unscaledTime >= sigPaquete)
            {
                sigPaquete = Time.unscaledTime + 1f / 30f;
                float ruido = UnityEngine.Random.Range(-0.02f, 0.02f);
                string json = sinRostro
                    ? $"{{\"seq\":{seq++},\"cara\":false,\"p\":0,\"motivo\":\"sin_rostro\",\"score\":0}}"
                    : $"{{\"seq\":{seq++},\"cara\":true,\"p\":{Mathf.Clamp01(p + ruido).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)},\"motivo\":\"ok\",\"score\":0.99}}";
                Enviar(System.Text.Encoding.UTF8.GetBytes(json), DataPort);
            }

            if (Time.unscaledTime >= sigImagen)
            {
                sigImagen = Time.unscaledTime + 1f / 12f;
                Dibujar();
                Enviar(img.EncodeToJPG(70), FramePort);
            }
        }

        void Enviar(byte[] b, int puerto)
        {
            try { udp.Send(b, b.Length, "127.0.0.1", puerto); } catch (Exception) { }
        }

        // Un ojo simple: abierto (iris) o cerrado (línea); vacío si no hay rostro
        void Dibujar()
        {
            int w = img.width, h = img.height;
            var px = new Color32[w * h];
            float apertura = 1f - Mathf.InverseLerp(0.04f, 0.96f, p);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float g = 0.10f + 0.06f * y / h;
                    Color c = new Color(g, g, g);
                    if (!sinRostro)
                    {
                        float dx = (x - w * 0.5f) / (w * 0.32f), dy = (y - h * 0.5f) / (h * 0.36f);
                        float lente = dx * dx + dy * dy / Mathf.Max(0.02f, apertura * apertura);
                        if (lente < 1f && Mathf.Abs(dy) < apertura + 0.02f)
                        {
                            c = new Color(0.82f, 0.82f, 0.8f);
                            float r = Mathf.Sqrt(dx * dx * 2.2f + dy * dy * 0.9f);
                            if (r < 0.42f) c = new Color(0.25f, 0.22f, 0.2f);
                            if (r < 0.18f) c = Color.black;
                        }
                        if (apertura < 0.12f && Mathf.Abs(dy * (h * 0.36f)) < 2.5f && Mathf.Abs(dx) < 1f) c = new Color(0.7f, 0.7f, 0.7f);
                    }
                    px[y * w + x] = c;
                }
            img.SetPixels32(px);
            img.Apply(false);
        }
    }
}
