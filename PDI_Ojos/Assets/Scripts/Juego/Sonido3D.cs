using UnityEngine;

/// <summary>Reproduce un sonido 3D en un punto del mundo (se destruye solo al terminar).</summary>
public static class Sonido3D
{
    public static void Reproducir(AudioClip clip, Vector3 posicion, float volumen = 1f, float distanciaMax = 25f)
    {
        if (clip == null) return;
        var go = new GameObject("Sonido3D (temporal)");
        go.transform.position = posicion;
        var a = go.AddComponent<AudioSource>();
        a.clip = clip;
        a.volume = volumen;
        a.spatialBlend = 1f;                         // 1 = totalmente 3D
        a.rolloffMode = AudioRolloffMode.Linear;
        a.minDistance = 2f;
        a.maxDistance = distanciaMax;
        a.Play();
        UnityEngine.Object.Destroy(go, clip.length + 0.3f);
    }
}
