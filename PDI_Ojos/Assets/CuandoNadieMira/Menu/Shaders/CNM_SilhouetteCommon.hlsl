#ifndef CNM_SILHOUETTE_COMMON_INCLUDED
#define CNM_SILHOUETTE_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

TEXTURE2D(_BaseMap);    SAMPLER(sampler_BaseMap);
TEXTURE2D(_BumpMap);    SAMPLER(sampler_BumpMap);

// Todas las pasadas comparten el mismo CBUFFER para que el SRP Batcher funcione
CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4  _BaseColor;
    half   _BumpScale;
    half   _Smoothness;
    half   _Metallic;
    half   _Silhouette;      // 0 = material original, 1 = silueta casi negra
    half4  _RimColor;        // borde de luz fría (HDR)
    half   _RimPower;
    half   _Dissolve;        // 0 = visible, 1 = disuelto
    half   _EdgeWidth;
    half4  _EdgeColor;       // brillo del borde de la disolución (HDR)
    half   _NoiseScale;
    half   _NoiseSeed;
    half   _DitherFade;      // 1 = desvanecido simple sin destellos (fotosensibilidad)
    half   _Sweep;           // posición de la franja de reflejo (vitrina)
    half   _Alpha;           // solo variante transparente
CBUFFER_END

// Hash y Voronoi 3D en espacio de objeto
float3 CNM_Hash33(float3 p)
{
    p = frac(p * float3(0.1031, 0.1030, 0.0973));
    p += dot(p, p.yxz + 33.33);
    return frac((p.xxy + p.yxx) * p.zyx);
}

float CNM_Voronoi(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    float d = 1.0;
    [unroll] for (int z = -1; z <= 1; z++)
    [unroll] for (int y = -1; y <= 1; y++)
    [unroll] for (int x = -1; x <= 1; x++)
    {
        float3 g = float3(x, y, z);
        float3 r = g + CNM_Hash33(i + g) - f;
        d = min(d, dot(r, r));
    }
    return sqrt(d);
}

static const float CNM_BayerMatrix[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

float CNM_Bayer4(float2 pixel)
{
    uint2 p = uint2(pixel) % 4;
    return (CNM_BayerMatrix[p.y * 4 + p.x] + 0.5) / 16.0;
}

// Devuelve la distancia al umbral de disolución (<0 = descartar) y escribe el factor de borde
float CNM_DissolveMask(float3 positionOS, float2 pixel, out half edge)
{
    float3 q = positionOS * _NoiseScale + _NoiseSeed * 17.31;
    float n = saturate(CNM_Voronoi(q) * 0.7 + CNM_Voronoi(q * 2.3) * 0.3);
    n = lerp(n, CNM_Bayer4(pixel), _DitherFade);
    float threshold = _Dissolve * 1.05 - 0.001;
    float diff = n - threshold;
    edge = (_Dissolve > 0.001) ? (1.0 - smoothstep(0.0, max(_EdgeWidth, 1e-4), diff)) * (1.0 - _DitherFade) : 0.0;
    return diff;
}

#endif
