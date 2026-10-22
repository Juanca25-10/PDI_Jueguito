// Variante transparente (vidrio de vitrina): reflejo por Fresnel, franja de reflejo que barre y disolución
Shader "CuandoNadieMira/GlassDissolve"
{
    Properties
    {
        _BaseColor ("Tinte", Color) = (0.8,0.85,0.9,1)
        _Alpha ("Opacidad base", Range(0,1)) = 0.06
        [HDR] _RimColor ("Color Fresnel", Color) = (0.7,0.75,0.8,1)
        _RimPower ("Potencia Fresnel", Range(0.5,8)) = 3
        _Sweep ("Franja de reflejo (Y local)", Float) = -1
        _Smoothness ("Ancho de la franja", Range(0.01,1)) = 0.12

        _Dissolve ("Disolución", Range(0,1)) = 0
        _EdgeWidth ("Ancho de borde", Range(0,0.2)) = 0.02
        [HDR] _EdgeColor ("Color del borde", Color) = (2.5,2.6,2.8,1)
        _NoiseScale ("Escala del ruido", Float) = 6
        _NoiseSeed ("Semilla", Float) = 0
        [Toggle] _DitherFade ("Desvanecido simple", Float) = 0

        [HideInInspector] _BaseMap ("Albedo", 2D) = "white" {}
        [HideInInspector] _BumpMap ("Normal", 2D) = "bump" {}
        [HideInInspector] _BumpScale ("BumpScale", Float) = 1
        [HideInInspector] _Metallic ("Metallic", Float) = 0
        [HideInInspector] _Silhouette ("Silhouette", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "ForwardGlass"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "CNM_SilhouetteCommon.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                half   fogFactor  : TEXCOORD3;
            };

            Varyings Vert(Attributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.positionOS = v.positionOS.xyz;
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half edge;
                clip(CNM_DissolveMask(i.positionOS, i.positionCS.xy, edge));
                float3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half fres = pow(1.0h - saturate(abs(dot(normalize(i.normalWS), v))), _RimPower);
                // Franja diagonal que recorre el vidrio de arriba abajo
                half band = 1.0h - smoothstep(0.0h, _Smoothness, abs(i.positionOS.y + i.positionOS.x * 0.3 - _Sweep));
                half3 col = _BaseColor.rgb * _Alpha + _RimColor.rgb * fres + band * 1.4h + _EdgeColor.rgb * edge;
                half a = saturate(_Alpha + fres * 0.5h + band * 0.35h + edge);
                col = MixFog(col, i.fogFactor);
                return half4(col, a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
