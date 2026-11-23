// Cuando nadie mira - Shader "Rayo de Luz" (URP) para falsear luz volumetrica.
// Se aplica a un cilindro de Unity (alto 2, radio 0.5) estirado desde la ventana hacia el piso.
// Aditivo, bordes suaves (fresnel), caida a lo largo del rayo, polvo en movimiento,
// se desvanece al tocar superficies (necesita Depth Texture activado en el URP Asset) y al acercarse la camara.
// Solo ASCII (el parser de Unity no acepta tildes ni enie).
Shader "Cuando Nadie Mira/Rayo de Luz"
{
    Properties
    {
        [Header(Luz)]
        _Color("Color de la luz", Color) = (1.0, 0.95, 0.82, 1)
        _Intensidad("Intensidad", Range(0, 5)) = 1.0

        [Header(Forma)]
        _SuavizadoBordes("Suavidad de los bordes", Range(0.2, 6)) = 2.0
        _Caida("Caida a lo largo del rayo", Range(0.2, 4)) = 1.2
        _Entrada("Entrada suave en la ventana", Range(0.01, 0.5)) = 0.12
        [Toggle] _Invertir("Invertir (la ventana esta en el otro extremo)", Float) = 0

        [Header(Polvo)]
        _Polvo("Polvo en movimiento", Range(0, 1)) = 0.5
        _EscalaPolvo("Escala del polvo", Range(0.1, 6)) = 1.2
        _VelPolvo("Velocidad del polvo", Range(0, 0.5)) = 0.06

        [Header(Suavidad con la escena)]
        _Profundidad("Desvanecer al tocar superficies (m)", Range(0.05, 3)) = 0.8
        _CercaInicio("Desvanecer cerca de la camara: desde (m)", Range(0, 5)) = 0.5
        _CercaFin("hasta (m)", Range(0.1, 10)) = 3
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Rayo"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Intensidad, _SuavizadoBordes, _Caida, _Entrada, _Invertir;
                float _Polvo, _EscalaPolvo, _VelPolvo;
                float _Profundidad, _CercaInicio, _CercaFin;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float t : TEXCOORD2;
                float4 screenPos : TEXCOORD3;
            };

            float Hash(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float Ruido(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x),
                            lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y);
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs vp = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = vp.positionCS;
                o.positionWS = vp.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.t = saturate(v.positionOS.y * 0.5 + 0.5);      // 0..1 a lo largo del cilindro de Unity (alto 2)
                o.screenPos = vp.positionNDC;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // Bordes suaves: el rayo es mas intenso donde la superficie mira a la camara
                float3 V = normalize(GetCameraPositionWS() - i.positionWS);
                float3 N = normalize(i.normalWS);
                float borde = pow(saturate(abs(dot(N, V))), _SuavizadoBordes);

                // A lo largo del rayo: t = 0 en la ventana, 1 en el extremo opuesto
                float t = _Invertir > 0.5 ? 1.0 - i.t : i.t;
                float largo = pow(saturate(1.0 - t), _Caida) * smoothstep(0.0, _Entrada, t);

                // Polvo: variacion lenta que sube y se mueve
                float3 p = i.positionWS * _EscalaPolvo + float3(0.3, -1.0, 0.2) * _Time.y * _VelPolvo * 4.0;
                float n = 0.5 * (Ruido(p.xy) + Ruido(p.zy * 1.3 + 17.0));
                float polvo = lerp(1.0, smoothstep(0.2, 0.85, n) * 1.7, _Polvo);

                // Se desvanece donde toca paredes y piso
                float2 suv = i.screenPos.xy / i.screenPos.w;
                float prof = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
                float suave = saturate((prof - i.screenPos.w) / _Profundidad);

                // Se desvanece cuando la camara esta muy cerca (evita el corte brusco al atravesarlo)
                float d = distance(GetCameraPositionWS(), i.positionWS);
                float cerca = saturate((d - _CercaInicio) / max(_CercaFin - _CercaInicio, 0.01));

                float respiro = 0.92 + 0.08 * sin(_Time.y * 1.7);
                float a = _Intensidad * borde * largo * polvo * suave * cerca * respiro;
                return half4(_Color.rgb, saturate(a));
            }
            ENDHLSL
        }
    }
}
