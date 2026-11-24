using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CuandoNadieMira.Menu
{
    /// <summary>
    /// Adapta el menú (MainMenu, Calibración y Carga) a cualquier proporción de pantalla, sin tocar la del juego:
    /// - Los canvas usan "Expand": el diseño de 1920x1080 cabe completo y el espacio que sobra se añade a los lados
    ///   (pantalla ancha) o arriba y abajo (pantalla estrecha).
    /// - Las franjas negras del efecto de película (_Barras, un % del alto de la pantalla) tapaban lo que va pegado
    ///   al borde de arriba o de abajo. Aquí se empuja hacia dentro solo lo que caería dentro de una franja.
    /// Se instala solo al arrancar; no hace falta ponerlo en ninguna escena.
    /// </summary>
    public class MenuScreenFit : MonoBehaviour
    {
        const float Margin = 14f;   // espacio entre la franja y el contenido (unidades del canvas)

        struct Pushed { public RectTransform rt; public Vector2 original; }

        readonly List<Pushed> pushed = new List<Pushed>();
        Material film;
        int lastW, lastH;
        float lastBars = -1f;
        Scene fittedScene;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            var go = new GameObject("[MenuScreenFit]");
            go.hideFlags = HideFlags.HideInHierarchy;
            DontDestroyOnLoad(go);
            go.AddComponent<MenuScreenFit>();
        }

        static bool IsMenuScene(Scene s) =>
            s.name == MenuFlow.MainMenuScene || s.name == MenuFlow.CalibrationScene || s.name == MenuFlow.LoadingScene;

        void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        void OnSceneLoaded(Scene s, LoadSceneMode mode)
        {
            if (!IsMenuScene(s)) return;
            foreach (var root in s.GetRootGameObjects())
                foreach (var scaler in root.GetComponentsInChildren<CanvasScaler>(true))
                    if (scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
                        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            lastBars = -1f;   // recalcular en el próximo LateUpdate
        }

        void LateUpdate()
        {
            var scene = SceneManager.GetActiveScene();
            if (!IsMenuScene(scene)) return;

            float bars = Bars();
            if (scene == fittedScene && Screen.width == lastW && Screen.height == lastH && Mathf.Approximately(bars, lastBars)) return;
            fittedScene = scene;
            lastW = Screen.width;
            lastH = Screen.height;
            lastBars = bars;
            Fit(scene, bars);
        }

        float Bars()
        {
            if (film == null)
                foreach (var m in Resources.FindObjectsOfTypeAll<Material>())
                    if (m != null && m.HasProperty("_Barras") && m.name.Contains("Pelicula Antigua 1")) { film = m; break; }
            return film != null ? Mathf.Clamp(film.GetFloat("_Barras"), 0f, 0.3f) : 0f;
        }

        void Fit(Scene scene, float bars)
        {
            // Devolver lo que se movió antes a su sitio original
            foreach (var p in pushed) if (p.rt != null) p.rt.anchoredPosition = p.original;
            pushed.Clear();
            if (bars <= 0f) return;

            foreach (var root in scene.GetRootGameObjects())
                foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                {
                    if (!canvas.isRootCanvas) continue;
                    var crt = (RectTransform)canvas.transform;
                    Canvas.ForceUpdateCanvases();
                    float h = crt.rect.height;
                    if (h <= 0f) continue;
                    float inset = bars * h + Margin;
                    Walk(crt, inset);
                }
        }

        // Recorre los contenedores que ocupan todo el canvas; sus hijos pegados arriba o abajo se ajustan
        void Walk(RectTransform parent, float inset)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                if (!(parent.GetChild(i) is RectTransform rt)) continue;

                bool fullStretch = rt.anchorMin == Vector2.zero && rt.anchorMax == Vector2.one
                                   && rt.offsetMin == Vector2.zero && rt.offsetMax == Vector2.zero;
                if (fullStretch) { Walk(rt, inset); continue; }
                if (!Mathf.Approximately(rt.anchorMin.y, rt.anchorMax.y)) continue;   // estirado en vertical: fondos, etc.

                float anchorY = rt.anchorMin.y;
                float height = rt.rect.height;
                float bottom = rt.anchoredPosition.y - rt.pivot.y * height;   // borde inferior respecto al ancla
                float top = bottom + height;
                Vector2 pos = rt.anchoredPosition;

                if (Mathf.Approximately(anchorY, 0f) && bottom < inset)
                    pos.y += inset - bottom;
                else if (Mathf.Approximately(anchorY, 1f) && top > -inset)
                    pos.y -= top + inset;
                else continue;

                pushed.Add(new Pushed { rt = rt, original = rt.anchoredPosition });
                rt.anchoredPosition = pos;
            }
        }
    }
}
