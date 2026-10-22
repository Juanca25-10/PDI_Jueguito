using System.Collections.Generic;
using UnityEngine;

namespace CuandoNadieMira.Menu
{
    /// <summary>
    /// Pool de sistemas de partículas (Shuriken) para los destellos de disolución.
    /// Las chispas nacen sobre la superficie de la malla, suben lento con turbulencia y parpadean.
    /// </summary>
    public class SparkleEmitter : MonoBehaviour
    {
        [SerializeField] Material sparkleMaterial;
        [SerializeField, Min(1)] int poolSize = 4;
        [SerializeField] Vector2 particlesPerObject = new(40, 120);

        readonly Queue<ParticleSystem> free = new();
        readonly List<ParticleSystem> busy = new();

        void Awake()
        {
            for (int i = 0; i < poolSize; i++) free.Enqueue(CreateSystem(i));
        }

        void Update()
        {
            // Devuelve al pool los sistemas que ya terminaron
            for (int i = busy.Count - 1; i >= 0; i--)
            {
                if (busy[i].IsAlive(true)) continue;
                busy[i].gameObject.SetActive(false);
                free.Enqueue(busy[i]);
                busy.RemoveAt(i);
            }
        }

        /// <summary>Emite destellos desde la superficie del renderer durante <paramref name="duration"/> segundos.</summary>
        public ParticleSystem Emit(Renderer source, float duration)
        {
            if (free.Count == 0 || source == null) return null;
            var ps = free.Dequeue();
            busy.Add(ps);
            ps.gameObject.SetActive(true);

            int count = Mathf.RoundToInt(Random.Range(particlesPerObject.x, particlesPerObject.y));
            if (SettingsStore.LowQuality) count /= 2;

            var shape = ps.shape;
            if (source is SkinnedMeshRenderer smr)
            {
                shape.shapeType = ParticleSystemShapeType.SkinnedMeshRenderer;
                shape.skinnedMeshRenderer = smr;
            }
            else
            {
                shape.shapeType = ParticleSystemShapeType.MeshRenderer;
                shape.meshRenderer = source as MeshRenderer;
            }

            var main = ps.main;
            main.duration = Mathf.Max(0.1f, duration);
            // Tamaño 4-10 mm a 3 m; más lejos crece para no volverse invisible (sigue siendo sutil en pantalla)
            var cam = Camera.main;
            float dist = cam != null ? Vector3.Distance(cam.transform.position, source.bounds.center) : 3f;
            float distanceScale = Mathf.Max(1f, Mathf.Sqrt(dist / 3f));
            // startSizeMultiplier reemplaza la constante en modo TwoConstants: se asigna la curva completa
            main.startSize = new ParticleSystem.MinMaxCurve(0.004f * distanceScale, 0.01f * distanceScale);
            var noise = ps.noise;
            noise.strengthMultiplier = 0.06f * distanceScale;
            var emission = ps.emission;
            emission.rateOverTime = count / main.duration;

            ps.Clear(true);
            ps.Play(true);
            return ps;
        }

        /// <summary>Corta la emisión (las chispas ya nacidas terminan su vida).</summary>
        public void StopEmitting(ParticleSystem ps)
        {
            if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        ParticleSystem CreateSystem(int index)
        {
            var go = new GameObject("Sparkles_" + index);
            go.transform.SetParent(transform, false);
            go.SetActive(false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.004f, 0.01f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.01f, 0.04f);
            main.gravityModifier = -0.015f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            // La escala del renderer de origen (FBX en cm, escala ~100) solo afecta a la forma, no al tamaño
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.maxParticles = 150;
            main.startColor = new Color(0.92f, 0.95f, 1f, 1f);

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.01f, 0.01f);
            vel.y = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.01f, 0.01f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.06f;
            noise.frequency = 1.6f;
            noise.scrollSpeed = 0.4f;
            noise.quality = ParticleSystemNoiseQuality.Low;

            // Parpadeo de opacidad: sube, tiembla y se apaga
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0.25f, 0.3f),
                    new GradientAlphaKey(0.9f, 0.48f), new GradientAlphaKey(0.35f, 0.7f), new GradientAlphaKey(0f, 1f)
                });
            col.color = g;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f;

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = sparkleMaterial;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }
    }
}
