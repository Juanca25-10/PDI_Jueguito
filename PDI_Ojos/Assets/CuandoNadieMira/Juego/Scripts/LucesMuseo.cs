using System.Collections;
using CuandoNadieMira.Menu;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CuandoNadieMira.Juego
{
    /// <summary>
    /// Parpadeos aleatorios de las luces del museo, con zumbido eléctrico. Solo mientras exploras:
    /// durante el turno el parpadeo es una señal del juego (una estatua retrocede, ver EfectoVela),
    /// así que aquí no se toca nada para no confundirla. Siempre devuelve cada luz a su intensidad exacta.
    /// </summary>
    public class LucesMuseo : MonoBehaviour
    {
        Light[] luces;
        float siguiente;
        Light actual;
        float intensidadOriginal;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Instalar()
        {
            SceneManager.sceneLoaded += (s, m) => Poner(s);
            Poner(SceneManager.GetActiveScene());
        }

        static void Poner(Scene s)
        {
            if (s.name != MenuFlow.DefaultGameScene || FindAnyObjectByType<LucesMuseo>() != null) return;
            var go = new GameObject("LucesMuseo");
            SceneManager.MoveGameObjectToScene(go, s);
            go.AddComponent<LucesMuseo>();
        }

        void Start()
        {
            var vela = FindAnyObjectByType<EfectoVela>();
            var todas = FindObjectsByType<Light>(FindObjectsSortMode.None);
            var lista = new System.Collections.Generic.List<Light>();
            foreach (var l in todas)
                if (l.type is LightType.Point or LightType.Spot && l.intensity > 0f && (vela == null || l != vela.luzAmanecer))
                    lista.Add(l);
            luces = lista.ToArray();
            siguiente = Time.time + Random.Range(12f, 22f);
        }

        bool Permitido()
        {
            var gm = GameManager.Instancia;
            return gm != null && gm.fase == GameManager.Fase.Explorando && !IntroCinematica.Activa && !HudPausa.Paused;
        }

        void Update()
        {
            if (actual != null && !Permitido()) Restaurar();
            if (actual != null || luces == null || luces.Length == 0 || !Permitido() || Time.time < siguiente) return;
            siguiente = Time.time + Random.Range(14f, 34f);
            var l = luces[Random.Range(0, luces.Length)];
            if (l != null && l.isActiveAndEnabled) StartCoroutine(Parpadeo(l));
        }

        IEnumerator Parpadeo(Light l)
        {
            actual = l;
            intensidadOriginal = l.intensity;
            Voz.Efecto("sfx_luz_parpadeo", l.transform.position, 0.6f, 14f);
            float fin = Time.time + Random.Range(0.5f, 1.4f);
            while (Time.time < fin && actual == l)
            {
                l.intensity = intensidadOriginal * (Random.value < 0.35f ? Random.Range(0f, 0.15f) : Random.Range(0.55f, 1f));
                yield return new WaitForSeconds(Random.Range(0.03f, 0.11f));
            }
            if (actual == l) Restaurar();
        }

        void Restaurar()
        {
            if (actual != null) actual.intensity = intensidadOriginal;
            actual = null;
        }

        void OnDisable() => Restaurar();
    }
}
