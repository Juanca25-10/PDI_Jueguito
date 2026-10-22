using System.Collections;
using TMPro;
using UnityEngine;

namespace CuandoNadieMira.Menu
{
    /// <summary>
    /// Título vivo: las ramas de la O crecen si el jugador se queda sobre "Iniciar experiencia",
    /// y cada 12 s un brillo recorre las letras sincronizado con un relámpago tras el ventanal y un trueno.
    /// </summary>
    public class TitleAnimator : MonoBehaviour
    {
        [SerializeField] TMP_Text[] lines;
        [SerializeField] Transform branches;
        [SerializeField] MenuButton startButton;
        [SerializeField] Light windowLight;
        [SerializeField] Renderer windowGlass;

        [Header("Ramas")]
        [SerializeField] float hoverToGrow = 3f;
        [SerializeField] float grownScale = 1.15f;

        [Header("Relámpago")]
        [SerializeField] float lightningPeriod = 12f;
        [SerializeField] float sweepTime = 0.9f;
        [SerializeField, Range(0f, 1f)] float baseBrightness = 0.78f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        Vector3 branchBase;
        float branchScale = 1f, branchVel;
        float nextLightning;
        float windowBase;
        Color windowGlassBase;
        MaterialPropertyBlock block;
        float sweep = -10f;   // posición del brillo en coordenadas locales del texto

        public void Configure(TMP_Text[] titleLines, Transform branchRoot, MenuButton start, Light window, Renderer glass)
        {
            lines = titleLines; branches = branchRoot; startButton = start; windowLight = window; windowGlass = glass;
        }

        void Start()
        {
            block = new MaterialPropertyBlock();
            if (branches != null) branchBase = branches.localScale;
            if (windowLight != null) windowBase = windowLight.intensity;
            if (windowGlass != null) windowGlassBase = windowGlass.sharedMaterial.GetColor(BaseColorId);
            nextLightning = Time.time + 5f;
        }

        void Update()
        {
            // Ramas: crecen 1-2 mm tras 3 s sobre "Iniciar experiencia"
            bool grow = startButton != null && startButton.IsFocused && startButton.FocusedTime > hoverToGrow;
            branchScale = Mathf.SmoothDamp(branchScale, grow ? grownScale : 1f, ref branchVel, grow ? 1.2f : 0.4f);
            if (branches != null) branches.localScale = branchBase * branchScale;

            if (Time.time >= nextLightning)
            {
                nextLightning = Time.time + lightningPeriod;
                StartCoroutine(Lightning());
            }
            ApplySweep();
        }

        IEnumerator Lightning()
        {
            bool gentle = SettingsStore.DisableSparkles;   // fotosensibilidad: sin destello brusco
            StartCoroutine(WindowFlash(gentle));
            for (float t = 0f; t < sweepTime; t += Time.deltaTime)
            {
                sweep = Mathf.Lerp(-0.1f, 1.1f, t / sweepTime);
                yield return null;
            }
            sweep = -10f;
            yield return new WaitForSeconds(0.6f);
            MenuAudio.Instance?.Play(MenuSound.Thunder, 0.55f, 0.05f);
        }

        IEnumerator WindowFlash(bool gentle)
        {
            if (windowLight == null) yield break;
            float peak = gentle ? 1.6f : 5f;
            float[] pattern = gentle ? new[] { 0.6f } : new[] { 0.08f, 0.06f, 0.18f };
            foreach (float d in pattern)
            {
                for (float t = 0f; t < d; t += Time.deltaTime)
                {
                    float k = gentle ? Mathf.Sin(t / d * Mathf.PI) : 1f - t / d;
                    SetWindow(Mathf.Lerp(1f, peak, k));
                    yield return null;
                }
                SetWindow(1f);
                yield return new WaitForSeconds(0.05f);
            }
        }

        void SetWindow(float k)
        {
            windowLight.intensity = windowBase * k;
            if (windowGlass == null) return;
            windowGlass.GetPropertyBlock(block);
            block.SetColor(BaseColorId, windowGlassBase * k);
            windowGlass.SetPropertyBlock(block);
        }

        // Brillo que recorre las letras de izquierda a derecha (colores de vértice)
        void ApplySweep()
        {
            if (lines == null) return;
            foreach (var text in lines)
            {
                if (text == null) continue;
                var info = text.textInfo;
                if (info == null || info.characterCount == 0) { text.ForceMeshUpdate(); info = text.textInfo; }
                Bounds b = text.textBounds;
                for (int i = 0; i < info.characterCount; i++)
                {
                    var ch = info.characterInfo[i];
                    if (!ch.isVisible) continue;
                    float x = Mathf.InverseLerp(b.min.x, b.max.x, (ch.bottomLeft.x + ch.topRight.x) * 0.5f);
                    float glow = Mathf.Exp(-Mathf.Pow((x - sweep) / 0.08f, 2f));
                    byte v = (byte)(Mathf.Lerp(baseBrightness, 1f, glow) * 255f);
                    var colors = info.meshInfo[ch.materialReferenceIndex].colors32;
                    for (int j = 0; j < 4; j++) colors[ch.vertexIndex + j] = new Color32(v, v, v, 255);
                }
                text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
            }
        }
    }
}
