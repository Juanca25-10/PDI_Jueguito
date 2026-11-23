using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cuando una estatua retrocede: una viñeta oscura palpita en los bordes de la pantalla y la cámara tiembla un instante.
/// No usa paneles negros (no se confunde con los párpados) ni depende del pipeline (URP, HDRP o clásico).
/// Ponlo en el Canvas del HUD. La Image de la viñeta la creas tú (sprite "Vineta.png", Raycast Target apagado).
/// </summary>
public class PulsoPantalla : MonoBehaviour
{
    [Header("Viñeta")]
    public Graphic vineta;                              // una Image a pantalla completa con el sprite de viñeta
    public float duracion = 0.5f;
    public AnimationCurve forma = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.2f, 1f), new Keyframe(1f, 0f));
    [Range(0f, 1f)] public float intensidad = 0.9f;

    [Header("Temblor de cámara")]
    public Transform camara;                            // la cámara del jugador
    public float fuerzaTemblor = 0.04f;                 // metros

    float tiempo = -1f;                                 // -1 = en reposo
    Vector3 posicionBase;
    ControlJugador jugador;                             // si existe, el temblor pasa por él (convive con el bamboleo)

    void Awake()
    {
        if (camara == null && Camera.main != null) camara = Camera.main.transform;
        if (camara != null) { posicionBase = camara.localPosition; jugador = camara.GetComponentInParent<ControlJugador>(); }
        Aplicar(0f);
    }

    void OnEnable() { Estatua.Retrocedio += AlRetroceder; }
    void OnDisable() { Estatua.Retrocedio -= AlRetroceder; }

    void AlRetroceder(Estatua e) { tiempo = 0f; }

    void Update()
    {
        if (tiempo < 0f) return;
        tiempo += Time.deltaTime;
        float t = tiempo / duracion;
        if (t >= 1f)
        {
            tiempo = -1f;
            Aplicar(0f);
            return;
        }
        Aplicar(forma.Evaluate(t));
    }

    void Aplicar(float valor)
    {
        if (vineta != null) vineta.canvasRenderer.SetAlpha(valor * intensidad);
        Vector3 temblor = Random.insideUnitSphere * (fuerzaTemblor * valor);
        if (jugador != null) jugador.sacudida = temblor;
        else if (camara != null) camara.localPosition = posicionBase + temblor;
    }
}