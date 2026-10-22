using UnityEngine;

namespace CuandoNadieMira.Menu
{
    /// <summary>Desplaza lentamente la textura de un plano (niebla baja animada).</summary>
    [RequireComponent(typeof(Renderer))]
    public class UVScroller : MonoBehaviour
    {
        static readonly int StId = Shader.PropertyToID("_BaseMap_ST");

        [SerializeField] Vector2 speed = new(0.01f, 0.004f);
        [SerializeField] Vector2 tiling = new(2f, 2f);

        Renderer rend;
        MaterialPropertyBlock block;

        void Awake()
        {
            rend = GetComponent<Renderer>();
            block = new MaterialPropertyBlock();
        }

        void Update()
        {
            Vector2 o = speed * Time.time;
            block ??= new MaterialPropertyBlock();
            rend.GetPropertyBlock(block);
            block.SetVector(StId, new Vector4(tiling.x, tiling.y, o.x % 1f, o.y % 1f));
            rend.SetPropertyBlock(block);
        }
    }
}
