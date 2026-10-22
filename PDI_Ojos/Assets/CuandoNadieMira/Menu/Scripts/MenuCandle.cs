using System.Collections;
using UnityEngine;

namespace CuandoNadieMira.Menu
{
    /// <summary>Vela con parpadeo por ruido. Se puede apagar y volver a encender.</summary>
    public class MenuCandle : MonoBehaviour
    {
        [SerializeField] Light pointLight;
        [SerializeField] Transform flame;
        [SerializeField] float flicker = 0.25f;

        float baseIntensity;
        Vector3 flameScale;
        float seed;
        float lit = 1f;

        public bool IsLit => lit > 0.5f;

        public void Configure(Light light, Transform flameTransform)
        {
            pointLight = light;
            flame = flameTransform;
        }

        void Awake()
        {
            baseIntensity = pointLight != null ? pointLight.intensity : 0f;
            flameScale = flame != null ? flame.localScale : Vector3.one;
            seed = Random.Range(0f, 50f);
        }

        void Update()
        {
            float n = Mathf.PerlinNoise(Time.time * 4f + seed, seed) * 0.7f + Mathf.PerlinNoise(Time.time * 13f, seed * 2f) * 0.3f;
            float k = (1f - flicker + n * flicker * 2f) * lit;
            if (pointLight != null) pointLight.intensity = baseIntensity * k;
            if (flame != null) flame.localScale = new Vector3(flameScale.x, flameScale.y * (0.85f + n * 0.3f), flameScale.z) * lit;
        }

        public IEnumerator Extinguish(float time = 0.2f)
        {
            MenuAudio.Instance?.Play(MenuSound.Snuff, 0.5f, 0.1f);
            for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
            {
                lit = 1f - t / time;
                yield return null;
            }
            lit = 0f;
        }

        public IEnumerator Relight(float time = 0.6f)
        {
            for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
            {
                lit = t / time;
                yield return null;
            }
            lit = 1f;
        }
    }
}
