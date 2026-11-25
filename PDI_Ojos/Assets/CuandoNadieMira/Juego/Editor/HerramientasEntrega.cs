using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CuandoNadieMira.Juego.Entrega
{
    /// <summary>
    /// Herramientas para dejar el proyecto listo para la entrega (menú "Cuando Nadie Mira/Entrega").
    ///   - Madremonte: orden de sus poses (P0 serena → P2 gira la cabeza → P1 se agacha y avanza → P2 extiende la mano)
    ///     y un sonido propio al llegar a cada pose (Audio/Resources/Efectos/mm_pose*).
    ///   - Imágenes de derrota: se capturan del propio juego, con los modelos actuales en su última pose,
    ///     vistas desde el puesto del guarda (lo que ves cuando te alcanzan). Se guardan en Imagenes/imagenes derrota/.
    ///   - Escenas del build en orden: MainMenu, Calibracion, Carga, Juego_Estatuas.
    /// Todo con la escena del juego abierta; al terminar la guarda.
    /// </summary>
    public static class HerramientasEntrega
    {
        const string EscenaJuego = "Assets/Scenes/Juego_Estatuas.unity";
        const string CarpetaDerrota = "Assets/Imagenes/imagenes derrota";
        const string Efectos = "Assets/CuandoNadieMira/Audio/Resources/Efectos/";
        const int Ancho = 1080, Alto = 1350;

        static readonly string[] Build =
        {
            "Assets/CuandoNadieMira/Menu/Scenes/MainMenu.unity",
            "Assets/CuandoNadieMira/Menu/Scenes/Calibracion.unity",
            "Assets/CuandoNadieMira/Menu/Scenes/Carga.unity",
            EscenaJuego,
        };

        [MenuItem("Cuando Nadie Mira/Entrega/Preparar todo (Madremonte, derrotas y build)")]
        static void PrepararTodo()
        {
            if (!AbrirJuego()) return;
            AjustarMadremonte();
            OrientarMohan();
            AsignarDerrotas();
            OrdenarBuild();
            var gmDep = Object.FindAnyObjectByType<GameManager>();
            if (gmDep != null && gmDep.mostrarDepuracion) { Undo.RecordObject(gmDep, "Sin depuración"); gmDep.mostrarDepuracion = false; }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[Entrega] Listo: Madremonte, imágenes de derrota y orden del build.");
        }

        [MenuItem("Cuando Nadie Mira/Entrega/Capturar imágenes de derrota (encuadre automático)")]
        static void SoloDerrotas()
        {
            if (!AbrirJuego()) return;
            CapturarDerrotas();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
        }

        static bool AbrirJuego()
        {
            if (Application.isPlaying) { Debug.LogError("[Entrega] Sal del modo Play primero."); return false; }
            var activa = EditorSceneManager.GetActiveScene();
            if (activa.path == EscenaJuego) return true;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            EditorSceneManager.OpenScene(EscenaJuego);
            return true;
        }

        // ------------------------------------------------------------------ Madremonte
        static void AjustarMadremonte()
        {
            var e = Object.FindObjectsByType<Estatua>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                          .FirstOrDefault(TormentaMadremonte.EsMadremonte);
            if (e == null) { Debug.LogWarning("[Entrega] No hay Madremonte en la escena."); return; }

            // Las variantes que ya existen, identificadas por el modelo del que vienen (MadreMonte_P0/P1/P2)
            var porModelo = new Dictionary<string, GameObject>();
            foreach (var v in e.variantesPorPose)
            {
                if (v == null) continue;
                var src = PrefabUtility.GetCorrespondingObjectFromOriginalSource(v);
                string ruta = src != null ? AssetDatabase.GetAssetPath(src) : v.name;
                foreach (var k in new[] { "P0", "P1", "P2" })
                    if (Path.GetFileNameWithoutExtension(ruta).EndsWith(k)) porModelo[k] = v;
            }
            var so = new SerializedObject(e);
            if (porModelo.Count == 3 && e.contenedorPoses != null && e.contenedorPoses.childCount == 4)
            {
                var orden = new[] { porModelo["P0"], porModelo["P2"], porModelo["P1"], porModelo["P2"] };
                var p = so.FindProperty("variantesPorPose");
                p.arraySize = orden.Length;
                for (int i = 0; i < orden.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = orden[i];
            }
            else Debug.LogWarning($"[Entrega] Madremonte: se esperaban 3 modelos y 4 poses (hay {porModelo.Count} y {e.contenedorPoses?.childCount}); no se cambió el orden.");

            var sp = so.FindProperty("sonidoPorPose");
            var clips = new[] { null, "mm_pose1", "mm_pose2", "mm_pose3" };
            sp.arraySize = clips.Length;
            for (int i = 0; i < clips.Length; i++)
                sp.GetArrayElementAtIndex(i).objectReferenceValue = clips[i] == null ? null : AssetDatabase.LoadAssetAtPath<AudioClip>(Efectos + clips[i] + ".ogg");
            so.ApplyModifiedProperties();

            // Su fuente de audio con la reverberación del monte (la sala del museo ya no basta)
            if (e.fuente != null && e.fuente.GetComponent<AudioReverbFilter>() == null)
            {
                var r = Undo.AddComponent<AudioReverbFilter>(e.fuente.gameObject);
                r.reverbPreset = AudioReverbPreset.Forest;
            }
            Debug.Log("[Entrega] Madremonte: poses P0 → P2 → P1 → P2 y sonidos mm_pose1-3.");
        }

        // ------------------------------------------------------------------ Mohán
        // En su última pose (la que te alcanza) el modelo quedaba de espaldas al escritorio: la cámara de la derrota
        // le veía la nuca. Su cara está ~59° a la derecha del eje "forward" del modelo, así que se gira ese punto
        // para que la cara mire al puesto del guarda. Es idempotente (se calcula a partir del puesto, no se suma).
        const float CaraMohan = 59f;

        static void OrientarMohan()
        {
            var gm = Object.FindAnyObjectByType<GameManager>();
            Transform puesto = gm != null ? (gm.puestoGuarda != null ? gm.puestoGuarda : gm.puntoInicio) : null;
            var e = Object.FindObjectsByType<Estatua>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                          .FirstOrDefault(x => (x.nombre ?? "").ToLowerInvariant().Contains("moh"));
            if (e == null || puesto == null || e.contenedorPoses == null || e.contenedorPoses.childCount < 2) return;
            var punto = e.contenedorPoses.GetChild(e.contenedorPoses.childCount - 1);
            Vector3 hacia = puesto.position - punto.position; hacia.y = 0f;
            if (hacia.sqrMagnitude < 0.01f) return;
            float yaw = Mathf.Atan2(hacia.x, hacia.z) * Mathf.Rad2Deg - CaraMohan;
            Undo.RecordObject(punto, "Orientar Mohán");
            punto.rotation = Quaternion.Euler(0f, yaw, 0f);
            Debug.Log($"[Entrega] Mohán: su última pose ahora mira al puesto del guarda (yaw {yaw:0}°).");
        }

        /// <summary>Asigna las imágenes de derrota que ya existen ({estatua}_derrota_1/2.png y noche_eterna.png).</summary>
        static void AsignarDerrotas()
        {
            foreach (var e in Object.FindObjectsByType<Estatua>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var rutas = new[] { 1, 2 }.Select(i => $"{CarpetaDerrota}/{Slug(e.nombre)}_derrota_{i}.png").Where(File.Exists).ToList();
                if (rutas.Count == 0) continue;
                var so = new SerializedObject(e);
                var p = so.FindProperty("imagenesDerrota");
                p.arraySize = rutas.Count;
                for (int i = 0; i < rutas.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = ComoSprite(rutas[i]);
                so.ApplyModifiedProperties();
            }
            string eterna = $"{CarpetaDerrota}/noche_eterna.png";
            var fin = Object.FindAnyObjectByType<HudFinal>(FindObjectsInactive.Include);
            if (fin != null && File.Exists(eterna))
            {
                var so = new SerializedObject(fin);
                so.FindProperty("timeOutImage").objectReferenceValue = ComoSprite(eterna);
                so.ApplyModifiedProperties();
            }
            Debug.Log("[Entrega] Imágenes de derrota asignadas.");
        }

        // ------------------------------------------------------------------ build
        static void OrdenarBuild()
        {
            EditorBuildSettings.scenes = Build.Where(File.Exists).Select(s => new EditorBuildSettingsScene(s, true)).ToArray();
            Debug.Log("[Entrega] Escenas del build: " + string.Join(", ", EditorBuildSettings.scenes.Select(s => Path.GetFileNameWithoutExtension(s.path))));
        }

        // ------------------------------------------------------------------ imágenes de derrota
        struct Estado { public GameObject go; public bool activo; public Vector3 pos; public Quaternion rot; public Vector3 esc; }

        static void CapturarDerrotas()
        {
            var gm = Object.FindAnyObjectByType<GameManager>();
            Transform puesto = gm != null ? (gm.puestoGuarda != null ? gm.puestoGuarda : gm.puntoInicio) : null;
            if (puesto == null) { Debug.LogError("[Entrega] El GameManager no tiene puestoGuarda: no sé desde dónde mirar."); return; }
            Vector3 ojo = puesto.position + Vector3.up * 1.55f;
            Directory.CreateDirectory(CarpetaDerrota);

            var estatuas = Object.FindObjectsByType<Estatua>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            // Los paneles de texto en el mundo (InfoEstatua) no se ven en el juego: tampoco en las capturas
            var paneles = Object.FindObjectsByType<InfoEstatua>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                                .Where(i => i.canvasInfo != null && i.canvasInfo.activeSelf).Select(i => i.canvasInfo).ToList();
            foreach (var pnl in paneles) pnl.SetActive(false);
            try { CapturarEstatuas(estatuas, ojo, puesto, gm); }
            finally { foreach (var pnl in paneles) pnl.SetActive(true); }
        }

        static void CapturarEstatuas(Estatua[] estatuas, Vector3 ojo, Transform puesto, GameManager gm)
        {
            var nuevas = new Dictionary<Estatua, List<string>>();
            foreach (var e in estatuas)
            {
                if (e.contenedorPoses == null || e.contenedorPoses.childCount < 2) continue;
                var estados = Guardar(e);
                try
                {
                    int final = e.contenedorPoses.childCount - 1;
                    var vis = Colocar(e, final);
                    if (!Cuerpo(vis, out Bounds b)) continue;
                    string slug = Slug(e.nombre);
                    float h = b.size.y;
                    Vector3 cabeza = new Vector3(b.center.x, b.min.y + h * 0.78f, b.center.z);
                    Vector3 hacia = ojo - cabeza; hacia.y = 0f;            // la estatua mira hacia tu puesto
                    hacia = hacia.sqrMagnitude > 0.01f ? hacia.normalized : vis.forward;

                    // 1) De frente, un poco por debajo de la cabeza: del pecho hacia arriba
                    float d1 = Mathf.Clamp(h * 1.0f, 1.3f, 2.8f);
                    Vector3 p1 = Libre(cabeza, cabeza + hacia * d1 + Vector3.down * h * 0.08f, e.transform);
                    var a = $"{CarpetaDerrota}/{slug}_derrota_1.png";
                    Capturar(a, p1, new Vector3(cabeza.x, b.min.y + h * 0.7f, cabeza.z), h * 0.75f, cabeza, h);
                    // 2) Más cerca, desde abajo y de lado: la estatua encima de ti
                    Vector3 lado = Quaternion.Euler(0f, 28f, 0f) * hacia;
                    float d2 = Mathf.Clamp(h * 0.8f, 1.1f, 2.4f);
                    Vector3 objetivo2 = new Vector3(cabeza.x, b.min.y + h * 0.42f, cabeza.z);
                    Vector3 p2 = Libre(cabeza, objetivo2 + lado * d2, e.transform);
                    var c = $"{CarpetaDerrota}/{slug}_derrota_2.png";
                    Capturar(c, p2, new Vector3(cabeza.x, b.min.y + h * 0.72f, cabeza.z), h * 0.8f, cabeza, h);
                    nuevas[e] = new List<string> { a, c };
                }
                finally { Restaurar(estados); }
            }

            // La noche eterna: el museo desde el puesto del guarda, hacia donde miras al empezar el turno
            string eterna = $"{CarpetaDerrota}/noche_eterna.png";
            Vector3 frente = puesto.forward; frente.y = 0f;
            if (frente.sqrMagnitude < 0.01f) frente = Vector3.forward;
            Capturar(eterna, ojo, ojo + frente.normalized * 10f + Vector3.down * 0.4f, 0f, ojo + frente.normalized * 4f + Vector3.up * 2f, 3f, fovFijo: 58f);

            AssetDatabase.Refresh();
            foreach (var kv in nuevas)
            {
                var so = new SerializedObject(kv.Key);
                var p = so.FindProperty("imagenesDerrota");
                p.arraySize = kv.Value.Count;
                for (int i = 0; i < kv.Value.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = ComoSprite(kv.Value[i]);
                so.ApplyModifiedProperties();
            }
            var fin = Object.FindAnyObjectByType<HudFinal>(FindObjectsInactive.Include);
            if (fin != null)
            {
                var so = new SerializedObject(fin);
                so.FindProperty("timeOutImage").objectReferenceValue = ComoSprite(eterna);
                so.ApplyModifiedProperties();
                foreach (var img in fin.GetComponentsInChildren<UnityEngine.UI.Image>(true))
                    if (img.name == "Imagen") { Undo.RecordObject(img, "Imagen derrota"); img.sprite = ComoSprite(eterna); }
            }
            Debug.Log($"[Entrega] Imágenes de derrota capturadas: {nuevas.Count} estatuas + noche eterna.");
        }

        /// <summary>
        /// Vistas de prueba: cada estatua en su última pose desde 8 ángulos (cada 45°, 0° = hacia el puesto del guarda),
        /// a la altura del pecho y desde abajo. Van a Temp/CapturasPrueba (fuera de Assets) para elegir el mejor encuadre.
        /// </summary>
        [MenuItem("Cuando Nadie Mira/Entrega/Vistas de prueba (8 ángulos)")]
        static void VistasDePrueba()
        {
            if (!AbrirJuego()) return;
            OrientarMohan();
            var gm = Object.FindAnyObjectByType<GameManager>();
            Transform puesto = gm != null ? (gm.puestoGuarda != null ? gm.puestoGuarda : gm.puntoInicio) : null;
            if (puesto == null) return;
            Vector3 ojo = puesto.position + Vector3.up * 1.55f;
            string dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp", "CapturasPrueba");
            Directory.CreateDirectory(dir);
            var paneles = Object.FindObjectsByType<InfoEstatua>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                                .Where(i => i.canvasInfo != null && i.canvasInfo.activeSelf).Select(i => i.canvasInfo).ToList();
            foreach (var pnl in paneles) pnl.SetActive(false);
            try
            {
                foreach (var e in Object.FindObjectsByType<Estatua>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (e.contenedorPoses == null || e.contenedorPoses.childCount < 2) continue;
                    var estados = Guardar(e);
                    try
                    {
                        var vis = Colocar(e, e.contenedorPoses.childCount - 1);
                        if (!Cuerpo(vis, out Bounds b)) continue;
                        float h = b.size.y;
                        Vector3 cabeza = new Vector3(b.center.x, b.min.y + h * 0.78f, b.center.z);
                        Vector3 hacia = ojo - cabeza; hacia.y = 0f; hacia.Normalize();
                        var info = $"{Slug(e.nombre)}: vis.forward={vis.forward} haciaPuesto={hacia} punto.forward={e.contenedorPoses.GetChild(e.contenedorPoses.childCount - 1).forward}";
                        Debug.Log("[Entrega] " + info);
                        File.AppendAllText(Path.Combine(dir, "info.txt"), info + System.Environment.NewLine);
                        for (int ang = 0; ang < 360; ang += 45)
                        {
                            Vector3 d = Quaternion.Euler(0f, ang, 0f) * hacia;
                            float d1 = Mathf.Clamp(h * 1.0f, 1.3f, 2.8f);
                            Capturar(Path.Combine(dir, $"{Slug(e.nombre)}_a{ang:000}_pecho.png"), Libre(cabeza, cabeza + d * d1 + Vector3.down * h * 0.08f, e.transform),
                                     new Vector3(cabeza.x, b.min.y + h * 0.7f, cabeza.z), h * 0.75f, cabeza, h);
                            float d2 = Mathf.Clamp(h * 0.8f, 1.1f, 2.4f);
                            Vector3 o2 = new Vector3(cabeza.x, b.min.y + h * 0.42f, cabeza.z);
                            Capturar(Path.Combine(dir, $"{Slug(e.nombre)}_a{ang:000}_bajo.png"), Libre(cabeza, o2 + d * d2, e.transform),
                                     new Vector3(cabeza.x, b.min.y + h * 0.72f, cabeza.z), h * 0.8f, cabeza, h);
                        }
                    }
                    finally { Restaurar(estados); }
                }
            }
            finally { foreach (var pnl in paneles) pnl.SetActive(true); }
            Debug.Log("[Entrega] Vistas de prueba en " + dir);
        }

        /// <summary>Si algo (un mueble, una pared) tapa la vista entre la cabeza y la cámara, acerca la cámara.</summary>
        static Vector3 Libre(Vector3 desde, Vector3 camara, Transform ignorar)
        {
            Vector3 dir = camara - desde;
            float dist = dir.magnitude;
            dir /= Mathf.Max(dist, 1e-4f);
            float mejor = dist;
            foreach (var hit in Physics.RaycastAll(desde, dir, dist, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(ignorar) && hit.distance < mejor) mejor = hit.distance;
            if (mejor < dist) mejor = Mathf.Max(0.5f, mejor - 0.2f);
            return desde + dir * mejor;
        }

        static Sprite ComoSprite(string ruta)
        {
            AssetDatabase.ImportAsset(ruta, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(ruta);
            if (imp != null && (imp.textureType != TextureImporterType.Sprite || imp.mipmapEnabled))
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.mipmapEnabled = false;
                imp.maxTextureSize = 2048;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(ruta);
        }

        static string Slug(string n)
        {
            n = (n ?? "estatua").ToLowerInvariant().Replace("á", "a").Replace("é", "e").Replace("í", "i").Replace("ó", "o").Replace("ú", "u");
            return new string(n.Where(char.IsLetterOrDigit).ToArray());
        }

        static List<Estado> Guardar(Estatua e)
        {
            var lista = new List<Estado>();
            var objs = new HashSet<GameObject>();
            if (e.modelo != null) objs.Add(e.modelo.gameObject);
            if (e.variantesPorPose != null) foreach (var v in e.variantesPorPose) if (v != null) objs.Add(v);
            foreach (var g in objs)
                lista.Add(new Estado { go = g, activo = g.activeSelf, pos = g.transform.position, rot = g.transform.rotation, esc = g.transform.localScale });
            return lista;
        }

        static void Restaurar(List<Estado> l)
        {
            foreach (var s in l)
            {
                s.go.SetActive(s.activo);
                s.go.transform.SetPositionAndRotation(s.pos, s.rot);
                s.go.transform.localScale = s.esc;
            }
        }

        // Igual que Estatua.PonerPose: solo la variante de esa pose, en su punto, centrada y escalada
        static Transform Colocar(Estatua e, int pose)
        {
            Transform vis = e.modelo;
            if (e.variantesPorPose != null && e.variantesPorPose.Length > 0)
            {
                foreach (var v in e.variantesPorPose) if (v != null) v.SetActive(false);
                if (pose < e.variantesPorPose.Length && e.variantesPorPose[pose] != null) vis = e.variantesPorPose[pose].transform;
            }
            if (e.modelo != null && vis != e.modelo) e.modelo.gameObject.SetActive(false);
            vis.gameObject.SetActive(true);
            if (!Mathf.Approximately(e.escalaModelo, 1f)) vis.localScale *= e.escalaModelo;
            var punto = e.contenedorPoses.GetChild(pose);
            vis.SetPositionAndRotation(punto.position, punto.rotation);
            if (e.centrarEnPose && Cuerpo(vis, out Bounds b))
            {
                float baseEsc = b.min.y - vis.position.y;
                float baseOrig = e.escalaModelo > 0.01f ? baseEsc / e.escalaModelo : baseEsc;
                vis.position += new Vector3(punto.position.x - b.center.x, baseOrig - baseEsc, punto.position.z - b.center.z);
            }
            return vis;
        }

        static bool Cuerpo(Transform v, out Bounds b)
        {
            b = default;
            bool hay = false;
            foreach (var r in v.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                if (!hay) { b = r.bounds; hay = true; } else b.Encapsulate(r.bounds);
            }
            return hay;
        }

        /// <summary>Renderiza la escena con una luz de acento (de arriba y de frente) y un contraluz, y la guarda como PNG.</summary>
        static void Capturar(string ruta, Vector3 desde, Vector3 hacia, float alturaEncuadre, Vector3 iluminar, float alto, float fovFijo = 0f)
        {
            var temporales = new List<GameObject>();
            try
            {
                var camGo = new GameObject("[CapturaDerrota]") { hideFlags = HideFlags.HideAndDontSave };
                temporales.Add(camGo);
                camGo.transform.position = desde;
                camGo.transform.LookAt(hacia);
                var cam = camGo.AddComponent<Camera>();
                cam.nearClipPlane = 0.05f;
                cam.farClipPlane = 200f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                float d = Vector3.Distance(desde, hacia);
                cam.fieldOfView = fovFijo > 0f ? fovFijo : Mathf.Clamp(2f * Mathf.Atan(alturaEncuadre * 0.5f / Mathf.Max(0.3f, d)) * Mathf.Rad2Deg, 14f, 70f);
                var data = camGo.AddComponent<UniversalAdditionalCameraData>();
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

                if (alto > 0f && fovFijo <= 0f)
                {
                    // Acento frío desde arriba y de frente (como una linterna), y un contraluz que separa la silueta del fondo
                    Vector3 frente = (desde - iluminar); frente.y = 0f; frente.Normalize();
                    Luz(temporales, LightType.Spot, iluminar + frente * alto * 0.9f + Vector3.up * alto * 0.7f, iluminar, new Color(0.78f, 0.84f, 1f), 9f * alto, alto * 3f, 42f);
                    Luz(temporales, LightType.Point, iluminar - frente * alto * 0.6f + Vector3.up * alto * 0.25f, iluminar, new Color(0.9f, 0.95f, 1f), 4f * alto, alto * 1.6f, 0f);
                }

                var rt = new RenderTexture(Ancho, Alto, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                cam.targetTexture = rt;
                var pedido = new RenderPipeline.StandardRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(cam, pedido)) RenderPipeline.SubmitRenderRequest(cam, pedido);
                else cam.Render();

                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(Ancho, Alto, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, Ancho, Alto), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                File.WriteAllBytes(ruta, tex.EncodeToPNG());
                cam.targetTexture = null;
                Object.DestroyImmediate(tex);
                rt.Release();
                Object.DestroyImmediate(rt);
            }
            finally { foreach (var g in temporales) Object.DestroyImmediate(g); }
        }

        static void Luz(List<GameObject> temporales, LightType tipo, Vector3 pos, Vector3 mira, Color color, float intensidad, float rango, float angulo)
        {
            var go = new GameObject("[LuzCaptura]") { hideFlags = HideFlags.HideAndDontSave };
            temporales.Add(go);
            go.transform.position = pos;
            go.transform.LookAt(mira);
            var l = go.AddComponent<Light>();
            l.type = tipo;
            l.color = color;
            l.intensity = intensidad;
            l.range = rango;
            if (tipo == LightType.Spot) { l.spotAngle = angulo; l.innerSpotAngle = angulo * 0.5f; }
            l.shadows = LightShadows.Soft;
        }
    }
}
