using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CuandoNadieMira.Menu
{
    /// <summary>
    /// Transición de salida: las velas se apagan de los bordes hacia el centro, la cámara hace un dolly
    /// hacia la vitrina, la pantalla se funde a negro (con un latido) y se carga la escena de forma asíncrona.
    /// </summary>
    public class SceneTransition : MonoBehaviour
    {
        [SerializeField] MenuCameraRig rig;
        [SerializeField] Camera cam;
        [SerializeField] Image blackout;
        [SerializeField] MenuCandle[] candles;
        [SerializeField] Transform dollyTarget;
        [SerializeField] float candleInterval = 0.15f;

        public bool Running { get; private set; }

        public void Configure(MenuCameraRig cameraRig, Camera camera, Image black, MenuCandle[] allCandles, Transform target)
        {
            rig = cameraRig; cam = camera; blackout = black; candles = allCandles; dollyTarget = target;
        }

        /// <summary>Fundido desde negro al iniciar la escena.</summary>
        public IEnumerator FadeIn(float time)
        {
            yield return Fade(1f, 0f, time);
        }

        /// <summary>Ejecuta la transición completa (<paramref name="shortVersion"/> para Calibración).</summary>
        public IEnumerator Play(string sceneName, bool shortVersion, System.Action<string> onMissingScene)
        {
            if (Running) yield break;
            Running = true;

            // Velas: de los bordes de la pantalla hacia el centro
            var ordered = candles.Where(c => c != null)
                .OrderByDescending(c => Mathf.Abs(cam.WorldToViewportPoint(c.transform.position).x - 0.5f))
                .ToArray();
            float interval = shortVersion ? candleInterval * 0.5f : candleInterval;
            foreach (var c in ordered)
            {
                StartCoroutine(c.Extinguish());
                yield return new WaitForSecondsRealtime(interval);
            }

            // Dolly hacia la vitrina y fundido a negro
            float dollyTime = shortVersion ? 0.8f : 1.6f;
            Vector3 dir = dollyTarget != null
                ? Quaternion.Inverse(rig.transform.rotation) * (dollyTarget.position - rig.transform.position)
                : Vector3.forward;
            Vector3 offset = Vector3.ClampMagnitude(dir, shortVersion ? 0.6f : 1.5f);
            StartCoroutine(Fade(0f, 1f, dollyTime));
            MenuMusic.Instance?.FadeTo(0f, dollyTime + 0.8f);
            for (float t = 0f; t < dollyTime; t += Time.unscaledDeltaTime)
            {
                rig.DollyOffset = offset * Mathf.SmoothStep(0f, 1f, t / dollyTime) * Mathf.Max(0.3f, SettingsStore.MotionScale);
                yield return null;
            }

            if (!shortVersion) MenuAudio.Instance?.Play(MenuSound.Heartbeat, 1.2f, 0f);
            yield return new WaitForSecondsRealtime(shortVersion ? 0.3f : 1.2f);

            AsyncOperation op = LoadAsync(sceneName);
            if (op == null)
            {
                // La escena aún no existe: avisa y devuelve el menú a su estado
                onMissingScene?.Invoke(sceneName);
                rig.DollyOffset = Vector3.zero;
                foreach (var c in candles) if (c != null) StartCoroutine(c.Relight());
                MenuMusic.Instance?.FadeTo(1f, 2.5f);
                yield return Fade(1f, 0f, 0.8f);
                Running = false;
                yield break;
            }
            while (!op.isDone) yield return null;
        }

        static AsyncOperation LoadAsync(string sceneName)
        {
            if (Application.CanStreamedLevelBeLoaded(sceneName)) return SceneManager.LoadSceneAsync(sceneName);
#if UNITY_EDITOR
            // En el editor permite probar aunque la escena no esté en Build Settings
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets(sceneName + " t:Scene"))
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == sceneName)
                    return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(path,
                        new LoadSceneParameters(LoadSceneMode.Single));
            }
#endif
            return null;
        }

        IEnumerator Fade(float from, float to, float time)
        {
            if (blackout == null) yield break;
            blackout.raycastTarget = to > 0.5f;
            for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
            {
                SetAlpha(Mathf.Lerp(from, to, t / time));
                yield return null;
            }
            SetAlpha(to);
        }

        void SetAlpha(float a)
        {
            var c = blackout.color;
            c.a = a;
            blackout.color = c;
        }
    }
}
