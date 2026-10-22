using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CuandoNadieMira.Menu
{
    /// <summary>
    /// Coordina las piezas desvanecibles: nunca más de 2 a la vez, la primera ocurre pronto
    /// y deja huellas de polvo cuando una pieza reaparece en otro lugar.
    /// </summary>
    public class FadeDirector : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] FadeableProp[] props;
        [SerializeField, Min(1)] int maxSimultaneous = 2;
        [SerializeField, Min(1)] int maxAway = 3;          // piezas fuera de escena (disolviéndose u ocultas) a la vez
        [SerializeField] Vector2 firstFadeDelay = new(2.5f, 6f);
        [SerializeField] float minGapBetweenStarts = 1.5f;

        [Header("Huellas en el mármol")]
        [SerializeField] Material footprintMaterial;
        [SerializeField] float floorHeight = 0f;
        [SerializeField] float footprintLifetime = 5f;

        readonly HashSet<FadeableProp> dissolving = new();
        readonly Queue<Renderer> footprintPool = new();
        MaterialPropertyBlock block;
        float lastStart = -99f;

        public void Configure(FadeableProp[] list, Material footprints, float floorY)
        {
            props = list;
            footprintMaterial = footprints;
            floorHeight = floorY;
        }

        void Start()
        {
            block = new MaterialPropertyBlock();
            if (props == null || props.Length == 0) props = FindObjectsByType<FadeableProp>(FindObjectsSortMode.None);
            if (props.Length > 0)
                props[Random.Range(0, props.Length)].ScheduleNext(Random.Range(firstFadeDelay.x, firstFadeDelay.y));
            for (int i = 0; i < 12; i++) footprintPool.Enqueue(CreateFootprint(i));
        }

        void Update()
        {
            if (Time.time - lastStart < minGapBetweenStarts) return;
            int away = 0;
            foreach (var p in props) if (p != null && p.IsBusy) away++;
            foreach (var p in props)
            {
                if (p == null || p.IsBusy || Time.time < p.NextFadeTime) continue;
                if (dissolving.Count >= maxSimultaneous || away >= maxAway)
                {
                    p.ScheduleNext(Random.Range(1f, 2.5f));   // sin cupo: lo intenta más tarde
                    continue;
                }
                dissolving.Add(p);
                lastStart = Time.time;
                p.BeginCycle(this);
                break;
            }
        }

        public void NotifyDissolveFinished(FadeableProp p) => dissolving.Remove(p);

        public int DissolvingCount => dissolving.Count;

        /// <summary>Deja 3 o 4 marcas de polvo entre la posición vieja y la nueva.</summary>
        public void SpawnFootprints(Vector3 from, Vector3 to)
        {
            if (footprintMaterial == null) return;
            int count = Random.Range(3, 5);
            Vector3 dir = to - from;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) return;
            Vector3 side = Vector3.Cross(Vector3.up, dir.normalized) * 0.09f;
            Quaternion rot = Quaternion.LookRotation(dir.normalized, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);
            for (int i = 0; i < count; i++)
            {
                if (footprintPool.Count == 0) break;
                float t = (i + 1f) / (count + 1f);
                Vector3 pos = Vector3.Lerp(from, to, t) + (i % 2 == 0 ? side : -side);
                pos.y = floorHeight + 0.003f;
                StartCoroutine(FadeFootprint(footprintPool.Dequeue(), pos, rot, i * 0.25f));
            }
        }

        IEnumerator FadeFootprint(Renderer r, Vector3 pos, Quaternion rot, float delay)
        {
            yield return new WaitForSeconds(delay);
            r.transform.SetPositionAndRotation(pos, rot);
            r.gameObject.SetActive(true);
            for (float t = 0f; t < footprintLifetime; t += Time.deltaTime)
            {
                block.SetColor(BaseColorId, new Color(0.75f, 0.75f, 0.75f, 0.55f * (1f - t / footprintLifetime)));
                r.SetPropertyBlock(block);
                yield return null;
            }
            r.gameObject.SetActive(false);
            footprintPool.Enqueue(r);
        }

        Renderer CreateFootprint(int i)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Footprint_" + i;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localScale = new Vector3(0.11f, 0.26f, 1f);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = footprintMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.SetActive(false);
            return r;
        }
    }
}
