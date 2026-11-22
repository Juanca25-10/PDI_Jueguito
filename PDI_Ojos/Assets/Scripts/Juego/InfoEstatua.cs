using TMPro;
using UnityEngine;

/// <summary>
/// Muestra un panel en el mundo (World Space Canvas) con la info de la estatua cuando el jugador se acerca,
/// SOLO antes de empezar el turno. Va en la raíz del Prefab, junto a "Estatua".
/// El Canvas lo creas tú como hijo de la raíz (no del Modelo) y lo arrastras a "Canvas Info".
/// </summary>
[RequireComponent(typeof(Estatua))]
public class InfoEstatua : MonoBehaviour
{
    [Tooltip("El Canvas en World Space con el panel. Se enciende al acercarte y se apaga al alejarte o al empezar el turno.")]
    public GameObject canvasInfo;
    public float radio = 3f;
    public bool girarHaciaElJugador = true;

    [Header("Opcional: rellenar con los datos de la Estatua")]
    public TMP_Text textoNombre;          // recibe Estatua.nombre
    public TMP_Text textoDescripcion;     // recibe Estatua.descripcion

    Estatua estatua;
    Transform jugador;

    void Awake() { estatua = GetComponent<Estatua>(); }

    void Start()
    {
        var control = FindFirstObjectByType<ControlJugador>();
        if (control != null) jugador = control.camara != null ? control.camara : control.transform;
        if (textoNombre != null) textoNombre.text = estatua.nombre;
        if (textoDescripcion != null) textoDescripcion.text = estatua.descripcion;
        Mostrar(false);
    }

    void Update()
    {
        if (canvasInfo == null || jugador == null) return;

        bool explorando = GameManager.Instancia != null && GameManager.Instancia.fase == GameManager.Fase.Explorando;
        bool cerca = Vector3.Distance(jugador.position, estatua.Posicion) <= radio;
        Mostrar(explorando && cerca);

        if (girarHaciaElJugador && canvasInfo.activeSelf)
        {
            Vector3 dir = canvasInfo.transform.position - jugador.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f) canvasInfo.transform.rotation = Quaternion.LookRotation(dir);
        }
    }

    void Mostrar(bool visible)
    {
        if (canvasInfo != null && canvasInfo.activeSelf != visible) canvasInfo.SetActive(visible);
    }
}
