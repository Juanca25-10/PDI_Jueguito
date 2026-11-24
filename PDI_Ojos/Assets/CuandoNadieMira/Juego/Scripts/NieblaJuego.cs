using CuandoNadieMira.Menu;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CuandoNadieMira.Juego
{
    /// <summary>
    /// Niebla sutil en el juego, como la del menú: una bruma oscura de distancia y capas de niebla baja
    /// que se arrastran por el suelo alrededor del jugador (la textura queda fija al mundo, no al jugador).
    /// Se instala sola en la escena del juego; respeta la opción "Calidad baja" del menú.
    /// </summary>
    public class NieblaJuego : MonoBehaviour
    {
        const int Capas = 5;
        const float Tam = 44f;                                   // lado de cada capa (m)
        static readonly Color ColorBruma = new Color(0.02f, 0.02f, 0.024f);
        static readonly float[] AlturaCapa = { 0.06f, 0.2f, 0.36f, 0.6f, 0.95f };
        static readonly float[] AlfaCapa = { 0.11f, 0.09f, 0.07f, 0.045f, 0.025f };
        static readonly Color ColorCapa = new Color(0.52f, 0.55f, 0.6f, 1f);
        static readonly int StId = Shader.PropertyToID("_BaseMap_ST");
        static readonly int ColorId = Shader.PropertyToID("_BaseColor");

        Transform jugador;
        readonly Transform[] capas = new Transform[Capas];
        readonly Renderer[] rends = new Renderer[Capas];
        MaterialPropertyBlock block;
        float suelo, sigRayo;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Instalar()
        {
            SceneManager.sceneLoaded += (s, m) => Poner(s);
            Poner(SceneManager.GetActiveScene());
        }

        static void Poner(Scene s)
        {
            if (s.name != MenuFlow.DefaultGameScene || FindAnyObjectByType<NieblaJuego>() != null) return;
            var go = new GameObject("NieblaJuego");
            SceneManager.MoveGameObjectToScene(go, s);
            go.AddComponent<NieblaJuego>();
        }

        void Start()
        {
            // Bruma de distancia muy suave
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.03f;
            RenderSettings.fogColor = ColorBruma;

            if (SettingsStore.LowQuality) return;
            var mat = Resources.Load<Material>("NieblaJuego");
            if (mat == null) return;

            block = new MaterialPropertyBlock();
            for (int i = 0; i < Capas; i++)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(q.GetComponent<Collider>());
                q.name = "Capa_" + i;
                q.transform.SetParent(transform, false);
                q.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                q.transform.localScale = new Vector3(Tam, Tam, 1f);
                var r = q.GetComponent<Renderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                capas[i] = q.transform;
                rends[i] = r;
            }
        }

        float densidad = 0.03f;

        void LateUpdate()
        {
            // La bruma respira despacio y se espesa un poco durante el turno
            var gm = GameManager.Instancia;
            float objetivo = gm != null && gm.fase == GameManager.Fase.Jugando ? 0.038f : 0.028f;
            densidad = Mathf.MoveTowards(densidad, objetivo, Time.deltaTime * 0.002f);
            RenderSettings.fogDensity = densidad * (1f + 0.12f * Mathf.Sin(Time.time * 0.21f) + 0.06f * Mathf.Sin(Time.time * 0.53f + 1f));
            if (block == null) return;
            if (jugador == null)
            {
                var c = FindAnyObjectByType<ControlJugador>();
                if (c == null) return;
                jugador = c.transform;
                suelo = jugador.position.y - 1f;
            }

            // El suelo bajo el jugador (cada medio segundo)
            Vector3 p = jugador.position;
            if (Time.time >= sigRayo)
            {
                sigRayo = Time.time + 0.5f;
                if (Physics.Raycast(p + Vector3.up * 0.2f, Vector3.down, out var hit, 6f, ~0, QueryTriggerInteraction.Ignore))
                    suelo = hit.point.y;
            }

            for (int i = 0; i < Capas; i++)
            {
                capas[i].position = new Vector3(p.x, suelo + AlturaCapa[i], p.z);
                float tiling = 2f + i * 0.7f;
                Vector2 vel = new Vector2(0.005f * (i + 1), 0.003f * (i % 2 == 0 ? 1 : -1));
                // Desplazamiento fijo al mundo: al caminar la niebla no te sigue, la atraviesas
                Vector2 o = vel * Time.time + new Vector2(p.x, p.z) / Tam * tiling;
                rends[i].GetPropertyBlock(block);
                block.SetVector(StId, new Vector4(tiling, tiling, o.x % 1f, o.y % 1f));
                float vaiven = 0.75f + 0.25f * Mathf.Sin(Time.time * (0.17f + i * 0.05f) + i * 1.7f);
                block.SetColor(ColorId, new Color(ColorCapa.r, ColorCapa.g, ColorCapa.b, AlfaCapa[i] * vaiven));
                rends[i].SetPropertyBlock(block);
            }
        }
    }
}
