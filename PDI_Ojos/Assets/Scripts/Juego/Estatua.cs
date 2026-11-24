using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Una estatua con N poses (las que quieras: cada estatua define las suyas).
/// Estructura recomendada (conviértela en Prefab):
///
///   Estatua_Madremonte      <- este script (esta raíz NO se mueve)
///   ├─ Modelo               <- el modelo 3D (con un Collider); es lo que se coloca en cada pose
///   └─ Poses                <- sus hijos, EN ORDEN, son los puntos: el primero = inicio, el último = el que te hace perder
///        ├─ P0  ├─ P1  ├─ P2 ...
///
/// Para agregar una estatua a la partida: arrastra su Prefab a la escena. Para quitarla: bórrala o desactívala.
/// El GameManager las detecta solo, y las luces reaccionan a los eventos estáticos de abajo.
/// Con los ojos abiertos, cada estatua retrocede una pose cuando se cumple su tiempo (no hace falta mirarla).
/// </summary>
public class Estatua : MonoBehaviour
{
    // ---- Lista y eventos globales (cualquier script puede suscribirse sin conocer a la estatua) ----
    public static readonly List<Estatua> Todas = new List<Estatua>();
    public static event Action<Estatua> Avanzo;          // pasó a una pose más cercana (con tus ojos cerrados)
    public static event Action<Estatua> Retrocedio;      // empieza a retroceder (aquí parpadean las luces)
    public static event Action<Estatua> LlegoAlFinal;    // llegó a su última pose: derrota

    [Header("Identidad")]
    public string nombre = "Estatua";
    [TextArea(3, 8)] public string descripcion;      // la usa InfoEstatua (panel en el mundo antes de empezar)
    [Tooltip("Imágenes para la pantalla de derrota cuando esta estatua te alcanza. Se elige una al azar.")]
    public Sprite[] imagenesDerrota;

    [Header("Estructura")]
    public Transform modelo;                // lo que se mueve entre poses
    public Transform contenedorPoses;       // sus hijos, en orden, son las poses
    [Tooltip("Opcional: un modelo distinto por pose (uno por cada pose, en orden; el modelo base puede ser el primero). El de la pose actual se activa y se coloca en su punto. Si lo dejas vacío se mueve 'Modelo'.")]
    public GameObject[] variantesPorPose;
    [Tooltip("Multiplica el tamaño de los modelos (base y variantes) al empezar.")]
    public float escalaModelo = 1.25f;
    [Tooltip("Centra el modelo sobre el punto de la pose y lo apoya a esa altura, aunque el pivote del modelo esté corrido.")]
    public bool centrarEnPose = true;

    [Header("Dificultad")]
    [Tooltip("Cuando la barra de amanecer (0-100) pasa de este valor, la estatua empieza a moverse.")]
    public float activaDesdeProgreso = 0f;
    [Tooltip("Segundos CON LOS OJOS CERRADOS que tarda en avanzar una pose.")]
    public float segPorPose = 14f;
    [Tooltip("Multiplica segPorPose al final de la noche: 0.55 = casi el doble de rápido al amanecer")]
    [Range(0.3f, 1f)] public float ritmoAlAmanecer = 0.55f;
    [Tooltip("Segundos con los ojos abiertos (ya sostenidos) para que retroceda una pose.")]
    [FormerlySerializedAs("segMiradaParaRetroceder")]
    public float segAbiertoParaRetroceder = 2f;
    [Tooltip("Pausa entre avisar el retroceso y cambiar la pose: la tapa el parpadeo de las luces.")]
    public float retrasoCambio = 0.11f;

    [Header("Sonido (opcional)")]
    [Tooltip("AudioSource propio de esta estatua, en un hijo suyo (por ejemplo 'Audio'). Pon Spatial Blend = 1. Se coloca donde está la estatua cada vez que suena. Si lo dejas vacío se usa un sonido 3D temporal.")]
    public AudioSource fuente;
    [Tooltip("Sonido al LLEGAR a cada pose (índice = pose; el 0 no se usa).")]
    public AudioClip[] sonidoPorPose;
    [Range(0f, 1f)] public float volumen = 1f;

    public int PoseActual { get; private set; }
    public int PoseFinal => poses.Count - 1;
    public Vector3 Posicion => Visible.position;          // dónde está ahora (para encararla o sonar desde ahí)

    readonly List<Transform> poses = new List<Transform>();
    float tAvance, tAbierto;
    bool cambiando;

    // Al añadir el componente, busca solos los hijos llamados "Modelo" y "Poses".
    void Reset()
    {
        modelo = transform.Find("Modelo");
        contenedorPoses = transform.Find("Poses");
        var audio = transform.Find("Audio");
        if (audio != null) fuente = audio.GetComponent<AudioSource>();
    }

    void Awake()
    {
        if (modelo == null || contenedorPoses == null || contenedorPoses.childCount < 2)
        {
            Debug.LogError("Estatua '" + name + "': asigna 'Modelo' y un contenedor 'Poses' con al menos 2 hijos.", this);
            enabled = false;
            return;
        }
        foreach (Transform p in contenedorPoses) poses.Add(p);
        if (!Mathf.Approximately(escalaModelo, 1f))
        {
            var escalados = new HashSet<Transform> { modelo };
            if (variantesPorPose != null) foreach (var v in variantesPorPose) if (v != null) escalados.Add(v.transform);
            foreach (var t in escalados) t.localScale *= escalaModelo;
        }
        if (fuente != null)
        {
            // Si el AudioSource está en la raíz (o en un padre de Modelo/Poses), moverlo arrastraría los puntos de pose.
            if (fuente.transform == transform || contenedorPoses.IsChildOf(fuente.transform) || modelo.IsChildOf(fuente.transform))
            {
                Debug.LogError("Estatua '" + name + "': el AudioSource debe estar en un HIJO aparte (p. ej. 'Audio'), no en la raíz. Se usará un sonido 3D temporal.", this);
                fuente = null;
            }
            else fuente.playOnAwake = false;
        }
    }

    void OnEnable() { Todas.Add(this); }
    void OnDisable() { Todas.Remove(this); }
    void Start() { Reiniciar(); }

    public void Reiniciar()
    {
        StopAllCoroutines();
        cambiando = false;
        tAvance = tAbierto = 0f;
        PonerPose(0);
    }

    // Lo que se coloca en la pose: la variante de esa pose si hay, y si no, el modelo base.
    Transform Visible
    {
        get
        {
            if (variantesPorPose != null && PoseActual < variantesPorPose.Length && variantesPorPose[PoseActual] != null)
                return variantesPorPose[PoseActual].transform;
            return modelo;
        }
    }

    void PonerPose(int i)
    {
        PoseActual = Mathf.Clamp(i, 0, PoseFinal);
        if (variantesPorPose != null)
        {
            // Primero se apagan todas y después se enciende la actual: así funciona aunque un mismo objeto se repita en la lista.
            foreach (var v in variantesPorPose) if (v != null) v.SetActive(false);
            if (PoseActual < variantesPorPose.Length && variantesPorPose[PoseActual] != null)
                variantesPorPose[PoseActual].SetActive(true);
        }
        var punto = poses[PoseActual];
        var vis = Visible;
        vis.SetPositionAndRotation(punto.position, punto.rotation);
        if (centrarEnPose) Centrar(vis, punto.position, escalaModelo);
    }

    // Lleva el centro del modelo (en planta) al punto de la pose; en altura deja la base donde quedaba antes de escalar.
    static void Centrar(Transform v, Vector3 punto, float escala)
    {
        var rs = v.GetComponentsInChildren<Renderer>();
        bool hay = false;
        Bounds b = default;
        foreach (var r in rs)
        {
            if (!r.enabled || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            if (!hay) { b = r.bounds; hay = true; } else b.Encapsulate(r.bounds);
        }
        if (!hay) return;
        float baseEscalada = b.min.y - v.position.y;
        float baseOriginal = escala > 0.01f ? baseEscalada / escala : baseEscalada;
        v.position += new Vector3(punto.x - b.center.x, baseOriginal - baseEscalada, punto.z - b.center.z);
    }

    // ------------------------------------------------------------ ojos cerrados: avanza
    public void Avanzar(float dt)
    {
        if (cambiando || PoseActual >= PoseFinal) return;
        tAvance += dt;
        // Cuanto más avanzada la noche, más rápido se mueven (al amanecer, segPorPose * ritmoAlAmanecer)
        float noche = GameManager.Instancia != null ? GameManager.Instancia.progreso / 100f : 0f;
        if (tAvance < segPorPose * Mathf.Lerp(1f, ritmoAlAmanecer, noche)) return;
        tAvance = 0f;
        PonerPose(PoseActual + 1);
        Sonar(PoseActual);
        Avanzo?.Invoke(this);
        if (PoseActual >= PoseFinal) LlegoAlFinal?.Invoke(this);
    }

    void Sonar(int pose)
    {
        if (sonidoPorPose == null || pose >= sonidoPorPose.Length || sonidoPorPose[pose] == null) return;
        if (fuente == null)
        {
            Sonido3D.Reproducir(sonidoPorPose[pose], Posicion, volumen);
            return;
        }
        fuente.transform.position = Posicion;                // el AudioSource sigue a la estatua (debe ser un hijo aparte, no la raíz)
        fuente.PlayOneShot(sonidoPorPose[pose], volumen);
    }

    // ------------------------------------------------------------ ojos abiertos: retrocede con el tiempo
    public void CancelarRetroceso() { tAbierto = 0f; }

    public void RetrocederPorTiempo(float dt)
    {
        if (PoseActual == 0 || cambiando) { tAbierto = 0f; return; }
        tAbierto += dt;
        if (tAbierto >= segAbiertoParaRetroceder) StartCoroutine(Retroceder());
    }

    IEnumerator Retroceder()
    {
        cambiando = true;
        tAbierto = 0f;
        Retrocedio?.Invoke(this);                         // las luces parpadean
        yield return new WaitForSeconds(retrasoCambio);   // y justo entonces cambia la pose
        PonerPose(PoseActual - 1);
        cambiando = false;
    }

    // Dibuja la ruta de poses en la ventana Scene para colocarlas fácil.
    void OnDrawGizmos()
    {
        if (contenedorPoses == null) return;
        Gizmos.color = new Color(1f, 0.8f, 0.2f);
        Transform anterior = null;
        for (int i = 0; i < contenedorPoses.childCount; i++)
        {
            Transform p = contenedorPoses.GetChild(i);
            Gizmos.color = i == contenedorPoses.childCount - 1 ? Color.red : new Color(1f, 0.8f, 0.2f);
            Gizmos.DrawSphere(p.position, 0.12f);
            Gizmos.DrawRay(p.position, p.forward * 0.4f);
            if (anterior != null) Gizmos.DrawLine(anterior.position, p.position);
            anterior = p;
        }
    }

    /// <summary>Una imagen al azar de la lista, o null si no hay ninguna.</summary>
    public Sprite ImagenDerrotaAleatoria()
    {
        if (imagenesDerrota == null || imagenesDerrota.Length == 0) return null;
        return imagenesDerrota[UnityEngine.Random.Range(0, imagenesDerrota.Length)];
    }
}