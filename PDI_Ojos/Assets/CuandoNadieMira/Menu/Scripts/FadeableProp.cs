using System.Collections;
using UnityEngine;

namespace CuandoNadieMira.Menu
{
    /// <summary>
    /// Pieza del fondo que, cada cierto tiempo, se disuelve en destellos sutiles,
    /// queda oculta unos segundos y reaparece (a veces más cerca de la cámara).
    /// El ritmo global lo controla <see cref="FadeDirector"/>.
    /// </summary>
    public class FadeableProp : MonoBehaviour
    {
        static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
        static readonly int SeedId = Shader.PropertyToID("_NoiseSeed");
        static readonly int DitherId = Shader.PropertyToID("_DitherFade");

        [SerializeField] Renderer[] renderers;
        [SerializeField] Transform[] alternativeAnchors; // posiciones más cercanas
        [SerializeField, Range(0, 1)] float relocateChance = 0.25f;
        [SerializeField] Vector2 visibleDelay = new(8f, 16f);
        [SerializeField] Vector2 hiddenDuration = new(3f, 8f);
        [SerializeField] float dissolveTime = 1.2f;
        [SerializeField] SparkleEmitter sparkles;   // pool de Shuriken

        public bool IsDissolving { get; private set; }
        public bool IsBusy { get; private set; }
        public float NextFadeTime { get; private set; }

        MaterialPropertyBlock block;
        Collider[] colliders;
        Vector3 homePosition;
        Quaternion homeRotation;
        float seed;

        public void Configure(Renderer[] rends, Transform[] anchors, SparkleEmitter emitter)
        {
            renderers = rends;
            alternativeAnchors = anchors;
            sparkles = emitter;
        }

        void Awake()
        {
            if (renderers == null || renderers.Length == 0) renderers = GetComponentsInChildren<Renderer>();
            colliders = GetComponentsInChildren<Collider>();
            block = new MaterialPropertyBlock();
            homePosition = transform.position;
            homeRotation = transform.rotation;
            seed = Random.Range(0f, 100f);
            SetDissolve(0f);
            // Arranque escalonado para que las piezas no desaparezcan todas en la misma ventana
            ScheduleNext(Random.Range(visibleDelay.x, visibleDelay.y * 3f));
        }

        public void ScheduleNext(float delay) => NextFadeTime = Time.time + delay;

        /// <summary>Lo llama el director cuando hay cupo para disolver.</summary>
        public void BeginCycle(FadeDirector director)
        {
            owner = director;
            StartCoroutine(Cycle(director));
        }

        FadeDirector owner;

        /// <summary>Revierte la disolución en curso en 0.3 s.</summary>
        public void ForceRevert()
        {
            if (!IsDissolving) return;
            StopAllCoroutines();
            StartCoroutine(Revert());
        }

        IEnumerator Cycle(FadeDirector director)
        {
            IsBusy = true;
            IsDissolving = true;
            bool simple = SettingsStore.DisableSparkles;
            var emitted = simple ? null : EmitFromLargestRenderer(dissolveTime);

            yield return Animate(0f, 1f, dissolveTime, simple);
            sparkles?.StopEmitting(emitted);
            IsDissolving = false;
            director.NotifyDissolveFinished(this);

            SetVisible(false);
            yield return new WaitForSeconds(Random.Range(hiddenDuration.x, hiddenDuration.y));

            // Reaparece en su sitio o, a veces, más cerca de la cámara
            Vector3 from = transform.position;
            bool relocate = alternativeAnchors != null && alternativeAnchors.Length > 0 && Random.value < relocateChance;
            if (relocate)
            {
                var a = alternativeAnchors[Random.Range(0, alternativeAnchors.Length)];
                transform.SetPositionAndRotation(a.position, a.rotation);
                director.SpawnFootprints(from, a.position);
            }
            else
            {
                transform.SetPositionAndRotation(homePosition, homeRotation);
                if ((from - homePosition).sqrMagnitude > 0.01f) director.SpawnFootprints(from, homePosition);
            }

            seed = Random.Range(0f, 100f);
            SetVisible(true);
            simple = SettingsStore.DisableSparkles;
            emitted = simple ? null : EmitFromLargestRenderer(dissolveTime);
            yield return Animate(1f, 0f, dissolveTime, simple);
            sparkles?.StopEmitting(emitted);

            ScheduleNext(Random.Range(visibleDelay.x, visibleDelay.y));
            IsBusy = false;
        }

        IEnumerator Revert()
        {
            float start = CurrentDissolve;
            yield return Animate(start, 0f, 0.3f, SettingsStore.DisableSparkles);
            IsDissolving = false;
            IsBusy = false;
            if (owner != null) owner.NotifyDissolveFinished(this);
            ScheduleNext(Random.Range(visibleDelay.x, visibleDelay.y));
        }

        float CurrentDissolve { get; set; }

        IEnumerator Animate(float from, float to, float time, bool simple)
        {
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                SetDissolve(Mathf.Lerp(from, to, t / time), simple);
                yield return null;
            }
            SetDissolve(to, simple);
        }

        ParticleSystem EmitFromLargestRenderer(float time)
        {
            if (sparkles == null) return null;
            Renderer best = null;
            float size = -1f;
            foreach (var r in renderers)
            {
                if (r == null || r is ParticleSystemRenderer || r.name == "Pedestal") continue;   // los destellos salen de la pieza
                float s = r.bounds.size.sqrMagnitude;
                if (s > size) { size = s; best = r; }
            }
            return sparkles.Emit(best, time);
        }

        void SetDissolve(float value, bool simple = false)
        {
            CurrentDissolve = value;
            foreach (var r in renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetFloat(DissolveId, value);
                block.SetFloat(SeedId, seed);
                block.SetFloat(DitherId, simple ? 1f : 0f);
                r.SetPropertyBlock(block);
            }
        }

        void SetVisible(bool visible)
        {
            foreach (var r in renderers) if (r != null) r.enabled = visible;
            foreach (var c in colliders) if (c != null) c.enabled = visible;
        }
    }
}
