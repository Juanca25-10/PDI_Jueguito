using System.Collections;
using UnityEngine;

/// <summary>
/// Efectos de luz del museo: parpadea cuando CUALQUIER estatua lanza su evento Retrocedio,
/// y amanece cuando ganas. Ponlo en un objeto vacío "Efectos". No necesita saber cuántas estatuas hay.
/// </summary>
public class EfectoVela : MonoBehaviour
{
    [Header("Luces que parpadean (las luces de la sala)")]
    public Light[] luces;
    public float duracionParpadeo = 0.22f;       // segundos que la sala queda casi a oscuras

    [Header("Amanecer (opcional)")]
    public Light luzAmanecer;                    // p. ej. una luz fuera de la ventana; empieza apagada
    public float intensidadAmanecer = 2.5f;
    public float segAmanecer = 6f;

    float[] basales;
    Coroutine rutina, parpadeando;

    void Awake()
    {
        basales = new float[luces != null ? luces.Length : 0];
        for (int i = 0; i < basales.Length; i++)
            if (luces[i] != null) basales[i] = luces[i].intensity;
        if (luzAmanecer != null) luzAmanecer.intensity = 0f;
    }

    void Escalar(float f)
    {
        for (int i = 0; i < basales.Length; i++)
            if (luces[i] != null) luces[i].intensity = basales[i] * f;
    }

    void OnEnable() { Estatua.Retrocedio += AlRetroceder; }
    void OnDisable() { Estatua.Retrocedio -= AlRetroceder; }

    void AlRetroceder(Estatua e)
    {
        if (parpadeando != null) StopCoroutine(parpadeando);
        parpadeando = StartCoroutine(RutinaParpadeo());
    }

    IEnumerator RutinaParpadeo()
    {
        Escalar(0.05f);
        yield return new WaitForSeconds(duracionParpadeo);      // la estatua cambia de pose a la mitad (ver Estatua.retrasoCambio)
        Escalar(0.6f); yield return new WaitForSeconds(0.05f);
        Escalar(0.15f); yield return new WaitForSeconds(0.05f);
        Escalar(1f);
    }

    public void Amanecer()
    {
        if (rutina != null) StopCoroutine(rutina);
        rutina = StartCoroutine(RutinaAmanecer());
    }

    IEnumerator RutinaAmanecer()
    {
        if (luzAmanecer == null) yield break;
        float t = 0f;
        while (t < segAmanecer)
        {
            t += Time.deltaTime;
            luzAmanecer.intensity = Mathf.Lerp(0f, intensidadAmanecer, t / segAmanecer);
            yield return null;
        }
    }

    /// <summary>Deja las luces como al empezar.</summary>
    public void Reiniciar()
    {
        StopAllCoroutines();
        rutina = parpadeando = null;
        Escalar(1f);
        if (luzAmanecer != null) luzAmanecer.intensity = 0f;
    }
}
