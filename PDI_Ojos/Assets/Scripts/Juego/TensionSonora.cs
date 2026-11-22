using UnityEngine;

/// <summary>
/// Sube un latido y un zumbido grave a medida que una estatua se acerca a su última pose
/// (o la barra se acerca a 0). Es la forma de "sentir" el peligro con los ojos cerrados.
/// Crea tú dos AudioSource (2D, Loop, Play On Awake apagado) con un clip de latido y otro de zumbido,
/// y arrástralos aquí. Ponlo en un objeto vacío (por ejemplo "Tension").
/// </summary>
public class TensionSonora : MonoBehaviour
{
    [Header("Fuentes (2D, Loop)")]
    public AudioSource latido;
    public AudioSource zumbido;

    [Header("Latido")]
    [Range(0f, 1f)] public float volumenLatidoMax = 0.8f;
    public Vector2 tonoLatido = new Vector2(0.9f, 1.4f);      // velocidad: de tranquilo a acelerado

    [Header("Zumbido")]
    [Range(0f, 1f)] public float volumenZumbidoMax = 0.6f;

    [Header("Respuesta")]
    [Range(0f, 0.5f)] public float tensionBase = 0.1f;       // lo mínimo que se oye durante el turno
    public float suavizado = 1.5f;                           // más alto = reacciona más rápido
    [Tooltip("Por debajo de este % de barra también sube la tensión (se acerca la derrota por ojos abiertos).")]
    public float barraPeligro = 20f;

    float tension;

    void Start()
    {
        Preparar(latido);
        Preparar(zumbido);
    }

    static void Preparar(AudioSource a)
    {
        if (a == null) return;
        a.loop = true;
        a.playOnAwake = false;
        a.volume = 0f;
        a.Play();
    }

    void Update()
    {
        float objetivo = CalcularTension();
        tension = Mathf.MoveTowards(tension, objetivo, suavizado * Time.deltaTime);

        if (latido != null)
        {
            latido.volume = tension * volumenLatidoMax;
            latido.pitch = Mathf.Lerp(tonoLatido.x, tonoLatido.y, tension);
        }
        if (zumbido != null) zumbido.volume = tension * volumenZumbidoMax;
    }

    float CalcularTension()
    {
        var gm = GameManager.Instancia;
        if (gm == null || gm.fase != GameManager.Fase.Jugando) return 0f;      // explorando, victoria o derrota: silencio

        float t = tensionBase;
        foreach (var e in Estatua.Todas)
            if (e.PoseFinal > 0) t = Mathf.Max(t, (float)e.PoseActual / e.PoseFinal);

        if (gm.progreso < barraPeligro) t = Mathf.Max(t, 1f - gm.progreso / barraPeligro);
        return Mathf.Clamp01(t);
    }
}
