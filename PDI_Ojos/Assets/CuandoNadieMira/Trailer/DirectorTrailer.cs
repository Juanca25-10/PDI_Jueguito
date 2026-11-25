#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CuandoNadieMira.Trailer
{
    /// <summary>
    /// Director de tomas para el trailer (solo en el editor; no entra en el build).
    /// Se activa únicamente si Unity se abre con la variable de entorno CNM_TRAILER.
    ///
    /// La imagen de "la cámara" es el video de un jugador real, servido en vivo por un alimentador externo
    /// (CNM_VIDEO_PRUEBA=tcp://127.0.0.1:23000). El detector real (U-Net + CNN) lo lee y el juego reacciona a lo que
    /// detecta. El director solo hace de jugador: le dice al alimentador qué gesto del video mostrar (ojos abiertos,
    /// parpadeo, cerrar, abrir) según lo que pide la calibración o la partida, camina con teclado y ratón simulados
    /// (Input System), lee la placa de la Llorona y se sienta en el puesto del guarda.
    ///
    /// También graba el audio del juego a WAV y anota los momentos clave (Trailer/tomas, junto al proyecto).
    /// </summary>
    public class DirectorTrailer : MonoBehaviour
    {
        static string Carpeta => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Trailer", "tomas"));
        static bool Activo => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CNM_TRAILER"));

        UdpClient udp;
        string ultimaOrden = "";

        // ------------------------------------------------------------------ instalación
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Instalar()
        {
            if (!Activo) return;
            Directory.CreateDirectory(Carpeta);
            var go = new GameObject("DirectorTrailer");
            DontDestroyOnLoad(go);
            go.AddComponent<DirectorTrailer>();
            SceneManager.sceneLoaded += (s, m) => PrepararEscena(s);
            PrepararEscena(SceneManager.GetActiveScene());
        }

        // La intro queda como cine puro: sin los textos de cada plano
        static void PrepararEscena(Scene s)
        {
            foreach (var intro in FindObjectsByType<IntroCinematica>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (intro.planos == null) continue;
                // En la escena la intro está apagada; en las tomas del trailer se enciende (solo en memoria, no se guarda)
                if (!intro.gameObject.activeSelf) intro.gameObject.SetActive(true);
                foreach (var p in intro.planos)
                {
                    if (p.mostrar != null) p.mostrar.SetActive(false);
                    p.mostrar = null;
                }
                intro.permitirSaltar = false;
            }
            Evento("escena", s.name);
        }

        void Awake() { udp = new UdpClient(); }
        void OnDestroy() { udp?.Close(); GrabadorAudio.Cerrar(); }

        void Start()
        {
            SceneManager.sceneLoaded += (s, m) => { StopAllCoroutines(); StartCoroutine(Escena(s.name)); };
            StartCoroutine(Escena(SceneManager.GetActiveScene().name));
        }

        void Update()
        {
            GrabadorAudio.Asegurar();
            GrabadorAudio.Vaciar();
        }

        IEnumerator Escena(string nombre)
        {
            Orden("abiertos");
            if (nombre == "Calibracion") return Calibracion();
            if (nombre == CuandoNadieMira.Menu.MenuFlow.DefaultGameScene) return Juego();
            return Nada();
        }

        IEnumerator Nada() { yield break; }

        // ------------------------------------------------------------------ calibración
        // El jugador sigue las instrucciones de la pantalla: cierra los ojos con la campana, los abre con la segunda
        // y parpadea dos veces en la prueba final.
        IEnumerator Calibracion()
        {
            bool cerro = false, abrio = false, parpadeo = false;
            while (true)
            {
                var textos = FindObjectsByType<TMP_Text>(FindObjectsSortMode.None).Where(t => t.isActiveAndEnabled).Select(t => t.text).ToList();
                bool preparate = textos.Any(t => t.Contains("Prepárate"));
                if (!cerro && preparate && textos.Contains("1"))
                {
                    cerro = true;
                    yield return new WaitForSecondsRealtime(0.55f);         // cierra justo cuando suena la campana
                    Orden("cerrados");
                    Evento("calibracion", "cierra los ojos");
                }
                if (cerro && !abrio && textos.Any(t => t.Contains("Abre los ojos")))
                {
                    abrio = true;
                    yield return new WaitForSecondsRealtime(0.25f);
                    Orden("abrir");
                    Evento("calibracion", "abre los ojos");
                }
                if (abrio && !parpadeo && textos.Any(t => t.Contains("Prueba final")))
                {
                    parpadeo = true;
                    yield return new WaitForSecondsRealtime(2.2f);
                    Orden("parpadeos");
                    Evento("calibracion", "parpadea dos veces");
                }
                if (parpadeo && textos.Any(t => t.Contains("El museo ya conoce tu mirada"))) { Evento("calibracion", "completada"); yield break; }
                if (textos.Any(t => t.Contains("Repetir") && t.Contains("calibr")) && cerro && !abrio) { cerro = false; }
                yield return null;
            }
        }

        // ------------------------------------------------------------------ partida
        ControlJugador jugador;
        Keyboard teclado;
        Mouse raton;
        readonly HashSet<Key> pulsadas = new();
        Estatua ultimaQueAvanzo;

        IEnumerator Juego()
        {
            yield return null;
            jugador = FindAnyObjectByType<ControlJugador>();
            teclado = Keyboard.current;
            raton = Mouse.current;
            if (jugador == null || teclado == null || raton == null) { Debug.LogWarning("[Trailer] falta jugador o dispositivos"); yield break; }
            Estatua.Avanzo += e => { ultimaQueAvanzo = e; Evento("avanza", e.nombre + " pose " + e.PoseActual); };
            Estatua.Retrocedio += e => Evento("retrocede", e.nombre);
            Estatua.LlegoAlFinal += e => Evento("alcanza", e.nombre);
            Evento("juego", "intro activa al empezar: " + IntroCinematica.Activa);
            GuardarMapa();
            Evento("juego", "mapa guardado; intro activa: " + IntroCinematica.Activa);
            var ruta0 = Ruta.Leer(Path.Combine(Carpeta, "..", "ruta.json"));
            if (ruta0.llorona.Count == 0) { Evento("juego", "sin ruta.json: solo reconocimiento"); yield break; }

            while (IntroCinematica.Activa) yield return null;
            Evento("juego", "fin de la intro");
            yield return new WaitForSeconds(1.6f);

            var ruta = Ruta.Leer(Path.Combine(Carpeta, "..", "ruta.json"));
            var llorona = Estatua.Todas.FirstOrDefault(e => (e.nombre ?? "").ToLowerInvariant().Contains("llorona"));
            var madre = Estatua.Todas.FirstOrDefault(e => (e.nombre ?? "").ToLowerInvariant().Replace(" ", "").Contains("madremonte"));
            var gm = GameManager.Instancia;

            // 1) Una mirada al museo
            yield return MirarA(jugador.transform.position + Quaternion.Euler(0, -35, 0) * jugador.transform.forward * 6f + Vector3.up * 1.4f, 2.2f, 40f);
            yield return MirarA(jugador.transform.position + Quaternion.Euler(0, 50, 0) * jugador.transform.forward * 6f + Vector3.up * 1.6f, 2.6f, 40f);

            // 2) Camina hasta la Llorona y escucha su placa
            if (llorona != null)
            {
                foreach (var p in ruta.llorona) yield return Caminar(p, 0.55f);
                var info = llorona.GetComponentInChildren<InfoEstatua>() ?? llorona.GetComponentInParent<InfoEstatua>();
                float radio = info != null ? info.radio : 3f;
                Vector3 objetivo = llorona.Posicion;
                if (Plano(jugador.transform.position - objetivo).magnitude > radio * 0.8f)
                    yield return Caminar(objetivo + Plano(jugador.transform.position - objetivo).normalized * radio * 0.6f, 0.3f);
                yield return MirarA(Cabeza(llorona), 1.4f, 50f);
                yield return new WaitForSeconds(0.8f);
                yield return Pulsar(Key.E);
                Evento("placa", "la Llorona: empieza");
                float t0 = Time.time;
                while (Time.time - t0 < 1.5f || CuandoNadieMira.Juego.PlacaEstatua.Leyendo != null)
                {
                    // Mientras escucha, la mirada recorre la estatua despacio
                    float k = Mathf.Sin((Time.time - t0) * 0.25f);
                    yield return MirarPaso(Cabeza(llorona) + Vector3.up * (0.25f * k) + jugador.transform.right * (0.35f * k), 8f);
                    if (Time.time - t0 > 70f) break;
                }
                Evento("placa", "la Llorona: termina");
                yield return new WaitForSeconds(1.2f);
            }

            // 3) Al puesto del guarda y empieza el turno
            foreach (var p in ruta.guardia) yield return Caminar(p, 0.55f);
            if (gm != null && gm.puntoInicio != null) yield return Caminar(gm.puntoInicio.position, 1.2f);
            yield return new WaitForSeconds(0.8f);
            yield return Pulsar(Key.E);
            Evento("turno", "empieza");
            yield return new WaitForSeconds(1.5f);

            // 4) El turno: abrir, cerrar, mirar quién se movió
            var orden = Estatua.Todas.OrderBy(e => Vector3.SignedAngle(jugador.transform.forward, Plano(e.Posicion - jugador.transform.position), Vector3.up)).ToList();
            foreach (var e in orden) yield return MirarA(Cabeza(e), 1.3f, 55f);
            Orden("parpadeo");
            yield return new WaitForSeconds(1.8f);

            Estatua[] miradas = { llorona, madre, orden.FirstOrDefault(e => e != llorona && e != madre) };
            for (int ciclo = 0; ciclo < 3 && gm.fase == GameManager.Fase.Jugando; ciclo++)
            {
                var foco = miradas[ciclo % miradas.Length] ?? orden[0];
                yield return MirarA(Cabeza(foco), 1.4f, 50f);
                yield return new WaitForSeconds(0.8f);
                Orden("cerrados");
                Evento("ojos", "cierra");
                float tc = Time.time;
                ultimaQueAvanzo = null;
                while (gm.fase == GameManager.Fase.Jugando && Time.time - tc < 11f && !Peligro()) yield return null;
                if (gm.fase != GameManager.Fase.Jugando) break;
                Orden("abrir");
                Evento("ojos", "abre");
                var cerca = Estatua.Todas.OrderByDescending(e => e.PoseActual / (float)Mathf.Max(1, e.PoseFinal)).First();
                yield return new WaitForSeconds(0.5f);
                yield return MirarA(Cabeza(cerca), 1.1f, 80f);
                float ta = Time.time;
                while (Time.time - ta < 10f && (Time.time - ta < 3.2f || Estatua.Todas.Any(e => e.PoseActual > 0 && e != madre)))
                    yield return MirarPaso(Cabeza(cerca), 4f);
                if (ciclo == 1) { Orden("parpadeo"); yield return new WaitForSeconds(1.5f); }
            }

            // 5) El final: con la tormenta encima, cierra los ojos hasta que lo alcanzan
            if (gm.fase == GameManager.Fase.Jugando)
            {
                yield return MirarA(Cabeza(madre ?? orden[0]), 1.6f, 45f);
                yield return new WaitForSeconds(1f);
                Orden("cerrados");
                Evento("ojos", "cierra (final)");
                float tf = Time.time;
                while (gm.fase == GameManager.Fase.Jugando && Time.time - tf < 60f) yield return null;
            }
            yield return new WaitForSeconds(0.4f);
            Orden("abrir");
            Evento("final", gm.fase.ToString());
            yield return new WaitForSeconds(14f);
            Evento("fin", "toma completa");
        }

        static bool Peligro() => Estatua.Todas.Any(e => e.PoseFinal > 0 && e.PoseActual >= e.PoseFinal - 1);

        // ------------------------------------------------------------------ entrada simulada
        IEnumerator Caminar(Vector3 destino, float llegada)
        {
            float t0 = Time.time;
            while (Plano(destino - jugador.transform.position).magnitude > llegada && Time.time - t0 < 25f)
            {
                Vector3 dir = Plano(destino - jugador.transform.position);
                float error = Vector3.SignedAngle(jugador.transform.forward, dir, Vector3.up);
                bool avanzar = Mathf.Abs(error) < 28f;
                Teclas(avanzar ? Key.W : Key.None);
                float pitchObj = 2f;                                       // mirada casi horizontal al caminar
                Raton(Mathf.Clamp(error * 3f, -75f, 75f) * Time.deltaTime, (Pitch() - pitchObj) * 2.5f * Time.deltaTime);
                yield return null;
            }
            Teclas(Key.None);
        }

        IEnumerator MirarA(Vector3 punto, float seg, float maxGradosSeg)
        {
            float t0 = Time.time;
            while (Time.time - t0 < seg) yield return MirarPaso(punto, maxGradosSeg / 20f);
        }

        // Un fotograma de giro suave hacia un punto
        IEnumerator MirarPaso(Vector3 punto, float suavidad)
        {
            Vector3 dir = punto - jugador.camara.position;
            float yawErr = Vector3.SignedAngle(jugador.transform.forward, Plano(dir), Vector3.up);
            float pitchObj = -Mathf.Atan2(dir.y, Plano(dir).magnitude) * Mathf.Rad2Deg;
            float k = 1f - Mathf.Exp(-Mathf.Max(0.5f, suavidad) * Time.deltaTime);
            Raton(yawErr * k, (Pitch() - pitchObj) * k);
            yield return null;
        }

        float Pitch()
        {
            float x = jugador.camara.localEulerAngles.x;
            return x > 180f ? x - 360f : x;
        }

        // Grados de giro -> movimiento del ratón (ControlJugador multiplica el delta por su sensibilidad)
        void Raton(float grados, float gradosArriba)
        {
            float s = Mathf.Max(0.001f, jugador.sensibilidad);
            InputSystem.QueueDeltaStateEvent(raton.delta, new Vector2(grados / s, gradosArriba / s));
        }

        void Teclas(params Key[] teclas)
        {
            var nuevas = new HashSet<Key>(teclas.Where(k => k != Key.None));
            if (nuevas.SetEquals(pulsadas)) return;
            pulsadas.Clear();
            foreach (var k in nuevas) pulsadas.Add(k);
            InputSystem.QueueStateEvent(teclado, new KeyboardState(pulsadas.ToArray()));
        }

        IEnumerator Pulsar(Key k)
        {
            Teclas(k);
            yield return null;
            yield return null;
            Teclas(Key.None);
            yield return null;
        }

        static Vector3 Plano(Vector3 v) { v.y = 0f; return v; }

        static Vector3 Cabeza(Estatua e)
        {
            if (e == null) return Vector3.zero;
            var rs = e.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy && !(r is ParticleSystemRenderer)).ToArray();
            if (rs.Length == 0) return e.Posicion + Vector3.up * 1.6f;
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return new Vector3(b.center.x, b.min.y + b.size.y * 0.82f, b.center.z);
        }

        // ------------------------------------------------------------------ alimentador y registro
        void Orden(string o)
        {
            if (o == ultimaOrden && o == "abiertos") return;
            ultimaOrden = o;
            var b = Encoding.ASCII.GetBytes(o);
            try { udp.Send(b, b.Length, "127.0.0.1", 23100); } catch { }
            Evento("orden", o);
        }

        static void Evento(string tipo, string detalle)
        {
            if (!Activo) return;
            double t = (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            File.AppendAllText(Path.Combine(Carpeta, "eventos.jsonl"),
                $"{{\"t\":{t.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)},\"tipo\":\"{tipo}\",\"detalle\":\"{detalle.Replace("\"", "'")}\"}}\n");
        }

        // Plano cenital del museo con la posición de todo, para trazar la ruta (Trailer/tomas/mapa.png + mapa.json)
        void GuardarMapa()
        {
            var gm = GameManager.Instancia;
            var b = new Bounds(jugador.transform.position, Vector3.one);
            foreach (var e in Estatua.Todas) b.Encapsulate(e.Posicion);
            if (gm != null && gm.puntoInicio != null) b.Encapsulate(gm.puntoInicio.position);
            b.Expand(new Vector3(12f, 0f, 12f));
            float lado = Mathf.Max(b.size.x, b.size.z);

            // Rejilla de ocupación a la altura del pecho: blanco = libre, gris = obstáculo (una celda = 0.2 m)
            const float celda = 0.2f;
            int n = Mathf.CeilToInt(lado / celda);
            var tex = new Texture2D(n, n, TextureFormat.RGB24, false);
            float y = jugador.transform.position.y + 0.9f;
            Vector3 esquina = b.center - new Vector3(lado / 2f, 0f, lado / 2f);
            var cc = jugador.GetComponent<CharacterController>();
            cc.enabled = false;
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    var c = new Vector3(esquina.x + (i + 0.5f) * celda, y, esquina.z + (j + 0.5f) * celda);
                    bool ocupado = Physics.CheckBox(c, new Vector3(celda / 2f, 0.6f, celda / 2f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                    bool suelo = Physics.Raycast(c, Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore);
                    tex.SetPixel(i, j, ocupado ? new Color(0.35f, 0.35f, 0.35f) : suelo ? Color.white : Color.black);
                }
            cc.enabled = true;
            tex.Apply();
            File.WriteAllBytes(Path.Combine(Carpeta, "mapa.png"), tex.EncodeToPNG());
            Destroy(tex);

            var sb = new StringBuilder("{");
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            string P(Vector3 v) => string.Format(ci, "[{0:F2},{1:F2},{2:F2}]", v.x, v.y, v.z);
            sb.Append(string.Format(ci, "\"centro\":{0},\"lado\":{1:F2},\"celda\":0.2,", P(b.center), lado));
            sb.Append("\"jugador\":" + P(jugador.transform.position) + ",\"adelante\":" + P(jugador.transform.forward) + ",");
            if (gm.puntoInicio != null) sb.Append("\"puntoInicio\":" + P(gm.puntoInicio.position) + ",");
            if (gm.puestoGuarda != null) sb.Append("\"puestoGuarda\":" + P(gm.puestoGuarda.position) + ",");
            sb.Append(string.Format(ci, "\"radioInicio\":{0:F2},\"estatuas\":[", gm.radioInicio));
            sb.Append(string.Join(",", Estatua.Todas.Select(e => $"{{\"nombre\":\"{e.nombre}\",\"pos\":{P(e.Posicion)},\"poses\":{e.PoseFinal + 1}}}")));
            sb.Append("]}");
            File.WriteAllText(Path.Combine(Carpeta, "mapa.json"), sb.ToString());
        }

        [Serializable]
        class Ruta
        {
            public List<Vector3> llorona = new(), guardia = new();
            [Serializable] class Crudo { public float[] llorona = new float[0], guardia = new float[0]; }
            public static Ruta Leer(string ruta)
            {
                var r = new Ruta();
                if (!File.Exists(ruta)) return r;
                var c = JsonUtility.FromJson<Crudo>(File.ReadAllText(ruta));
                for (int i = 0; i + 1 < c.llorona.Length; i += 2) r.llorona.Add(new Vector3(c.llorona[i], 0f, c.llorona[i + 1]));
                for (int i = 0; i + 1 < c.guardia.Length; i += 2) r.guardia.Add(new Vector3(c.guardia[i], 0f, c.guardia[i + 1]));
                return r;
            }
        }
    }

    /// <summary>Graba lo que suena por el AudioListener activo a un WAV (16 bits) por escena, con la hora de inicio.</summary>
    public class GrabadorAudio : MonoBehaviour
    {
        static readonly object cerrojo = new();
        static readonly List<float> pendiente = new();
        static FileStream archivo;
        static int canales, muestras, frecuencia = 48000;
        static string escena;

        public static void Asegurar()
        {
            frecuencia = AudioSettings.outputSampleRate;
            var oido = FindObjectsByType<AudioListener>(FindObjectsSortMode.None).FirstOrDefault(l => l.isActiveAndEnabled);
            if (oido != null && oido.GetComponent<GrabadorAudio>() == null) oido.gameObject.AddComponent<GrabadorAudio>();
            string s = SceneManager.GetActiveScene().name;
            if (escena != s) { Cerrar(); escena = s; }
        }

        void OnAudioFilterRead(float[] data, int ch)
        {
            lock (cerrojo)
            {
                if (archivo == null && escena != null)
                {
                    canales = ch;
                    double t = (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
                    string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Trailer", "tomas"));
                    string nombre = $"audio_{escena}_{DateTime.Now:HHmmss}";
                    archivo = new FileStream(Path.Combine(dir, nombre + ".wav"), FileMode.Create);
                    archivo.Write(new byte[44], 0, 44);
                    muestras = 0;
                    File.WriteAllText(Path.Combine(dir, nombre + ".json"),
                        "{\"inicio\":" + t.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + ",\"frecuencia\":" + frecuencia + ",\"canales\":" + ch + "}");
                }
                if (archivo != null && ch == canales) pendiente.AddRange(data);
            }
        }

        public static void Vaciar()
        {
            lock (cerrojo)
            {
                if (archivo == null || pendiente.Count == 0) return;
                var buf = new byte[pendiente.Count * 2];
                for (int i = 0; i < pendiente.Count; i++)
                {
                    short v = (short)Mathf.Clamp(pendiente[i] * 32767f, -32768f, 32767f);
                    buf[i * 2] = (byte)v; buf[i * 2 + 1] = (byte)(v >> 8);
                }
                archivo.Write(buf, 0, buf.Length);
                muestras += pendiente.Count;
                pendiente.Clear();
                Cabecera();
            }
        }

        static void Cabecera()
        {
            long pos = archivo.Position;
            int fr = frecuencia, datos = muestras * 2;
            var h = new BinaryWriter(archivo, Encoding.ASCII, true);
            archivo.Seek(0, SeekOrigin.Begin);
            h.Write(Encoding.ASCII.GetBytes("RIFF")); h.Write(36 + datos); h.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            h.Write(16); h.Write((short)1); h.Write((short)canales); h.Write(fr); h.Write(fr * canales * 2); h.Write((short)(canales * 2)); h.Write((short)16);
            h.Write(Encoding.ASCII.GetBytes("data")); h.Write(datos);
            h.Flush();
            archivo.Seek(pos, SeekOrigin.Begin);
        }

        public static void Cerrar()
        {
            lock (cerrojo)
            {
                if (archivo == null) return;
                Cabecera();
                archivo.Dispose();
                archivo = null;
                pendiente.Clear();
            }
        }
    }
}
#endif
