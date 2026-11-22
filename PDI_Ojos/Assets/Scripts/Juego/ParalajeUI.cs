using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Mueve un panel de UI ligeramente al mover la vista, para dar sensación de profundidad.
/// Ponlo en un panel HIJO del Canvas (no en el Canvas mismo). Con "profundidad" distinta
/// en cada panel se logra el efecto de capas.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class ParalajeUI : MonoBehaviour
{
    public float intensidad = 1.5f;       // píxeles de desplazamiento por píxel de movimiento del mouse
    public float maximo = 25f;            // desplazamiento máximo en píxeles
    public float velocidad = 6f;          // qué tan rápido sigue y vuelve al centro
    public float profundidad = 1f;        // 1 = normal, 2 = se mueve el doble, negativo = al revés
    public float inclinacion = 0f;        // grados de giro opcionales (0 = apagado)

    RectTransform rt;
    Vector2 basePos, actual;
    float baseRot;

    void Awake()
    {
        rt = (RectTransform)transform;
        basePos = rt.anchoredPosition;
        baseRot = rt.localEulerAngles.z;
    }

    void OnDisable()
    {
        if (rt != null) { rt.anchoredPosition = basePos; actual = Vector2.zero; }
    }

    void Update()
    {
        Vector2 delta = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
        Vector2 objetivo = Vector2.ClampMagnitude(-delta * intensidad * profundidad, maximo);

        actual = Vector2.Lerp(actual, objetivo, 1f - Mathf.Exp(-velocidad * Time.unscaledDeltaTime));
        rt.anchoredPosition = basePos + actual;

        if (inclinacion != 0f)
            rt.localRotation = Quaternion.Euler(0, 0, baseRot + Mathf.Clamp(actual.x / maximo, -1f, 1f) * -inclinacion);
    }
}
