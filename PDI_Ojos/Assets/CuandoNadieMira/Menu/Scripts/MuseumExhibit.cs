using System.Collections;
using TMPro;
using UnityEngine;

namespace CuandoNadieMira.Menu
{
    public enum ExhibitAnimation { VitrineSweep, MirrorGhost, PlaqueReveal, GuestbookPage, DoorClose }

    /// <summary>
    /// Pieza del museo asociada a un botón. Al "despertar", su luz sube en 0.4 s
    /// y ejecuta una microanimación propia.
    /// </summary>
    public class MuseumExhibit : MonoBehaviour
    {
        static readonly int SweepId = Shader.PropertyToID("_Sweep");

        [SerializeField] ExhibitAnimation animationKind;
        [SerializeField] Light[] lights;
        [SerializeField] Transform focusPoint;
        [SerializeField] float awakeMultiplier = 2.2f;

        [Header("Referencias de la animación")]
        [SerializeField] Renderer glass;            // vitrina
        [SerializeField] GameObject ghost;          // espejo
        [SerializeField] TMP_Text plaqueText;       // placa
        [SerializeField] Transform page;            // libro
        [SerializeField] Transform doorSlit;        // puerta (rendija de luz)
        [SerializeField] Light doorLight;

        float[] baseIntensity;
        float level = 1f, levelTarget = 1f, levelVel;
        Coroutine anim;
        MaterialPropertyBlock block;
        Vector3 slitScale;
        float doorLightAngle;

        public Transform FocusPoint => focusPoint != null ? focusPoint : transform;

        public void Configure(ExhibitAnimation kind, Light[] exhibitLights, Transform focus)
        {
            animationKind = kind;
            lights = exhibitLights;
            focusPoint = focus;
        }

        public void SetAnimationRefs(Renderer glassRenderer = null, GameObject ghostObject = null, TMP_Text plaque = null,
            Transform pageTransform = null, Transform slit = null, Light slitLight = null)
        {
            glass = glassRenderer; ghost = ghostObject; plaqueText = plaque; page = pageTransform;
            doorSlit = slit; doorLight = slitLight;
        }

        void Awake()
        {
            block = new MaterialPropertyBlock();
            baseIntensity = new float[lights?.Length ?? 0];
            for (int i = 0; i < baseIntensity.Length; i++) baseIntensity[i] = lights[i] != null ? lights[i].intensity : 0f;
            if (ghost != null) ghost.SetActive(false);
            if (plaqueText != null) plaqueText.maxVisibleCharacters = 0;
            if (doorSlit != null) slitScale = doorSlit.localScale;
            if (doorLight != null) doorLightAngle = doorLight.spotAngle;
            SetSweep(-2f);
        }

        /// <summary>1 = normal, &gt;1 despierta, 0.6 = atenuada mientras otra pieza está activa.</summary>
        public void SetLevel(float target) => levelTarget = target;

        public void Awaken()
        {
            levelTarget = awakeMultiplier;
            if (anim != null) StopCoroutine(anim);
            anim = StartCoroutine(PlayAnimation());
        }

        public void Sleep(float dimLevel)
        {
            levelTarget = dimLevel;
            if (animationKind == ExhibitAnimation.PlaqueReveal && plaqueText != null)
            {
                if (anim != null) StopCoroutine(anim);
                anim = StartCoroutine(HidePlaque());
            }
        }

        void Update()
        {
            level = Mathf.SmoothDamp(level, levelTarget, ref levelVel, 0.4f * 0.35f);
            for (int i = 0; i < baseIntensity.Length; i++)
                if (lights[i] != null) lights[i].intensity = baseIntensity[i] * level;
        }

        IEnumerator PlayAnimation()
        {
            switch (animationKind)
            {
                case ExhibitAnimation.VitrineSweep:
                    // Reflejo que barre el vidrio de arriba abajo
                    for (float t = 0; t < 1.1f; t += Time.deltaTime)
                    {
                        SetSweep(Mathf.Lerp(0.9f, -0.9f, t / 1.1f));
                        yield return null;
                    }
                    SetSweep(-2f);
                    break;

                case ExhibitAnimation.MirrorGhost:
                    // El reflejo muestra por un instante una silueta que no está en la sala
                    yield return new WaitForSeconds(0.25f);
                    if (ghost != null)
                    {
                        ghost.SetActive(true);
                        yield return new WaitForSeconds(0.18f);
                        ghost.SetActive(false);
                        yield return new WaitForSeconds(0.08f);
                        ghost.SetActive(true);
                        yield return new WaitForSeconds(0.1f);
                        ghost.SetActive(false);
                    }
                    break;

                case ExhibitAnimation.PlaqueReveal:
                    // El texto grabado se ilumina letra por letra
                    if (plaqueText != null)
                    {
                        plaqueText.ForceMeshUpdate();
                        int total = plaqueText.textInfo.characterCount;
                        for (int i = plaqueText.maxVisibleCharacters; i <= total; i++)
                        {
                            plaqueText.maxVisibleCharacters = i;
                            yield return new WaitForSeconds(0.035f);
                        }
                    }
                    break;

                case ExhibitAnimation.GuestbookPage:
                    // Una página pasa sola
                    if (page != null)
                    {
                        MenuAudio.Instance?.Play(MenuSound.PageTurn, 0.8f);
                        for (float t = 0; t < 0.9f; t += Time.deltaTime)
                        {
                            float k = Mathf.SmoothStep(0f, 1f, t / 0.9f);
                            page.localRotation = Quaternion.Euler(0f, 0f, k * 178f);
                            yield return null;
                        }
                        yield return new WaitForSeconds(0.6f);
                        page.localRotation = Quaternion.identity;
                    }
                    break;

                case ExhibitAnimation.DoorClose:
                    // La rendija de luz se estrecha, como si alguien cerrara la puerta
                    if (doorSlit != null)
                    {
                        for (float t = 0; t < 1.2f; t += Time.deltaTime)
                        {
                            float k = Mathf.SmoothStep(0f, 1f, t / 1.2f);
                            doorSlit.localScale = new Vector3(slitScale.x * Mathf.Lerp(1f, 0.15f, k), slitScale.y, slitScale.z);
                            if (doorLight != null) doorLight.spotAngle = Mathf.Lerp(doorLightAngle, doorLightAngle * 0.3f, k);
                            yield return null;
                        }
                        yield return new WaitForSeconds(1.5f);
                        for (float t = 0; t < 0.8f; t += Time.deltaTime)
                        {
                            float k = t / 0.8f;
                            doorSlit.localScale = new Vector3(slitScale.x * Mathf.Lerp(0.15f, 1f, k), slitScale.y, slitScale.z);
                            if (doorLight != null) doorLight.spotAngle = Mathf.Lerp(doorLightAngle * 0.3f, doorLightAngle, k);
                            yield return null;
                        }
                    }
                    break;
            }
            anim = null;
        }

        IEnumerator HidePlaque()
        {
            while (plaqueText.maxVisibleCharacters > 0)
            {
                plaqueText.maxVisibleCharacters -= 3;
                yield return null;
            }
            plaqueText.maxVisibleCharacters = 0;
            anim = null;
        }

        void SetSweep(float v)
        {
            if (glass == null) return;
            glass.GetPropertyBlock(block);
            block.SetFloat(SweepId, v);
            glass.SetPropertyBlock(block);
        }
    }
}
