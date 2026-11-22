// Cuando nadie mira - Shader "Pintura al oleo" (URP, Fullscreen Pass Renderer Feature)
// Idea: la imagen se mantiene NITIDA. El look de oleo viene de:
//  - relieve de pintura gruesa (empaste): alturas procedurales iluminadas con una luz direccional
//  - suavizado que respeta los bordes (no es blur)
//  - color vibrante con luces doradas y sombras frias, resplandor calido en las luces
// Solo ASCII en este archivo (el parser de Unity no acepta tildes ni enie).
Shader "Cuando Nadie Mira/Pintura"
{
    Properties
    {
        [Header(Relieve de pintura)]
        _Relieve("Fuerza del relieve", Range(0, 1)) = 0.35
        _EscalaPincel("Tamano de las pinceladas", Range(4, 60)) = 22
        _Craquelado("Textura fina (grietas de oleo)", Range(0, 1)) = 0.35
        _EscalaFina("Escala de la textura fina", Range(100, 900)) = 420
        _LuzAngulo("Direccion de la luz (grados)", Range(0, 360)) = 135

        [Header(Suavizado que respeta bordes)]
        _Suavizado("Fuerza del suavizado", Range(0, 1)) = 0.5
        _RadioSuavizado("Radio (px)", Range(0.5, 3)) = 1.5
        _Nitidez("Proteccion de bordes", Range(5, 120)) = 40

        [Header(Color)]
        _Saturacion("Vibrancia", Range(0, 1)) = 0.35
        _Contraste("Contraste", Range(0.7, 1.5)) = 1.06
        _ColorSombras("Tinte de sombras", Color) = (0.46, 0.48, 0.58, 1)
        _ColorLuces("Tinte de luces", Color) = (0.60, 0.50, 0.38, 1)
        _FuerzaTinte("Fuerza del tinte", Range(0, 1)) = 0.3
        _Resplandor("Resplandor calido en luces", Range(0, 0.5)) = 0.12
        _ColorResplandor("Color del resplandor", Color) = (1.0, 0.72, 0.35, 1)

        [Header(Acabado)]
        _Grano("Grano", Range(0, 0.15)) = 0.025
        _ColorVineta("Color de la vineta", Color) = (0.10, 0.05, 0.05, 1)
        _Vineta("Fuerza de la vineta", Range(0, 1)) = 0.25
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        Pass
        {
            Name "Pintura"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Relieve, _EscalaPincel, _Craquelado, _EscalaFina, _LuzAngulo;
                float _Suavizado, _RadioSuavizado, _Nitidez;
                float _Saturacion, _Contraste;
                float4 _ColorSombras, _ColorLuces;
                float _FuerzaTinte, _Resplandor;
                float4 _ColorResplandor;
                float _Grano;
                float4 _ColorVineta;
                float _Vineta;
            CBUFFER_END

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Ruido(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash(i);
                float b = Hash(i + float2(1, 0));
                float c = Hash(i + float2(0, 1));
                float d = Hash(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float3 Muestra(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
            }

            float Luma(float3 c)
            {
                return dot(c, float3(0.299, 0.587, 0.114));
            }

            // Altura de la pintura: parches (pinceladas) con vetas en su direccion + textura fina
            float Altura(float2 uvA)
            {
                float2 g = uvA * _EscalaPincel;
                g += (float2(Ruido(g * 0.4), Ruido(g * 0.4 + 9.2)) - 0.5) * 1.6;   // parches irregulares
                float2 id = floor(g);
                float2 f = frac(g) - 0.5;
                float ang = Hash(id) * 3.1416;
                float2 dir = float2(cos(ang), sin(ang));
                float2 r = float2(dot(f, dir), dot(f, float2(-dir.y, dir.x)));
                float veta = Ruido(float2(r.x * 1.5 + Hash(id + 3.7) * 20.0, r.y * 16.0));   // vetas a lo largo del trazo
                float fino = Ruido(uvA * _EscalaFina);
                return lerp(veta, fino, _Craquelado * 0.6);
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                float2 texel = _BlitTexture_TexelSize.xy;
                float aspecto = _ScreenParams.x / _ScreenParams.y;
                float2 uvA = uv * float2(aspecto, 1.0);

                // 1) Suavizado que respeta bordes (4 puntos): pule la imagen sin borrar contornos
                float3 c0 = Muestra(uv);
                float3 acc = c0;
                float wsum = 1.0;
                const float2 offs[4] = { float2(1, 1), float2(-1, 1), float2(1, -1), float2(-1, -1) };
                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    float3 c = Muestra(uv + offs[i] * texel * _RadioSuavizado);
                    float3 dc = c - c0;
                    float w = exp(-dot(dc, dc) * _Nitidez);
                    acc += c * w;
                    wsum += w;
                }
                float3 col = lerp(c0, acc / wsum, _Suavizado);
                float lum = Luma(col);

                // 2) Relieve de empaste: normal a partir de la altura, iluminada por una luz direccional
                float eps = texel.y * 1.5;
                float h0 = Altura(uvA);
                float hx = Altura(uvA + float2(eps, 0));
                float hy = Altura(uvA + float2(0, eps));
                float2 grad = clamp(float2(hx - h0, hy - h0) / eps * 0.012, -1.0, 1.0);
                float3 N = normalize(float3(-grad, 1.0));
                float rad = radians(_LuzAngulo);
                float3 L = normalize(float3(cos(rad), sin(rad), 0.8));
                float sombra = dot(N, L) - L.z;                                   // 0 = plano
                col *= 1.0 + sombra * _Relieve * (0.6 + lum);
                col += saturate(sombra) * lum * _Relieve * 0.35 * _ColorResplandor.rgb;   // brillo en las crestas

                // 3) Color: sombras frias, luces doradas, vibrancia (mas en lo apagado)
                float3 tinte = lerp(_ColorSombras.rgb, _ColorLuces.rgb, smoothstep(0.1, 0.9, lum));
                col = lerp(col, col * tinte * 2.0, _FuerzaTinte);

                float mx = max(col.r, max(col.g, col.b));
                float mn = min(col.r, min(col.g, col.b));
                float sat = mx - mn;
                col = lerp(Luma(col).xxx, col, 1.0 + _Saturacion * (1.0 - sat));
                col = (col - 0.5) * _Contraste + 0.5;

                // 4) Resplandor calido en las luces (luz dorada de las pinturas)
                float brillo = saturate((lum - 0.55) * 2.5);
                col += brillo * brillo * _ColorResplandor.rgb * _Resplandor;

                // 5) Grano y vineta
                float tiempo = floor(_Time.y * 12.0);
                col += (Hash(uv * _ScreenParams.xy + tiempo) - 0.5) * _Grano;

                float2 d = uv - 0.5;
                float v = smoothstep(0.25, 0.85, dot(d, d) * 2.0);
                col = lerp(col, col * _ColorVineta.rgb * 4.0, v * _Vineta);

                return float4(saturate(col), 1.0);
            }
            ENDHLSL
        }
    }
}