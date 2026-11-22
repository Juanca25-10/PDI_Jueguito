using System.Collections;
using UnityEngine;

/// <summary>
/// Efectos de luz del museo: parpadea cuando CUALQUIER estatua lanza su evento Retrocedio,
/// y amanece cuando ganas. Ponlo en un objeto vacío "Efectos". No necesita saber cuántas estatuas hay.
/// </summary>
public class EfectoVela : MonoBehaviour
{
    [Header("Luces que parpadean (SOLO sirven las Realtime o Mixed; las Baked no cambian en el juego)")]
    public Light[] luces;

    [Header("Parpadeo al retroceder una estatua")]
    public float duracionParpadeo = 0.3f;        // segundos en oscuridad (la estatua cambia de pose en medio)
    [Range(0f, 1f)] public float intensidadOscura = 0f;   // 0 = apagadas del todo
    public int titileos = 4;                     // cuántas veces titila al volver la luz

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
        Escalar(intensidadOscura);
        yield return new WaitForSeconds(duracionParpadeo);      // la estatua cambia de pose en medio (ver Estatua.retrasoCambio)
        for (int i = 0; i < titileos; i++)                      // la luz vuelve titilando, como una vela que se apaga
        {
            Escalar(Random.Range(0.1f, 1f));
            yield return new WaitForSeconds(Random.Range(0.04f, 0.12f));
        }
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