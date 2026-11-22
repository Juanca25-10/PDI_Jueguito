// Cuando nadie mira - Shader de pantalla "Pintura" (URP, Fullscreen Pass Renderer Feature)
// Mezcla: pinceladas direccionales + paleta c?lida/turquesa + posterizado suave
//         + l?nea de tinta (linograbado) + trama de lienzo + grano + vi?eta c?lida.
// Costo: ~8 lecturas de textura por p?xel. No toca materiales de la escena.
Shader "Cuando Nadie Mira/Pintura"
{
    Properties
    {
        [Header(Pincel)]
        _TamanoPincel("Largo de la pincelada (px)", Range(0, 6)) = 2.5
        _EscalaPincel("Tamano de las pinceladas", Range(1, 40)) = 14
        _Distorsion("Ondulacion tipo acuarela (px)", Range(0, 6)) = 1.5

        [Header(Color)]
        _Niveles("Niveles de color", Range(3, 24)) = 9
        _FuerzaPosterizado("Fuerza del posterizado", Range(0, 1)) = 0.45
        _Saturacion("Saturacion", Range(0, 2)) = 1.25
        _Contraste("Contraste", Range(0.5, 1.8)) = 1.1
        _ColorSombras("Tinte de sombras", Color) = (0.36, 0.50, 0.62, 1)
        _ColorLuces("Tinte de luces", Color) = (0.66, 0.50, 0.34, 1)
        _FuerzaTinte("Fuerza del tinte", Range(0, 1)) = 0.6

        [Header(Tinta)]
        _ColorTinta("Color de la linea", Color) = (0.12, 0.07, 0.10, 1)
        _FuerzaLinea("Fuerza de la linea", Range(0, 1)) = 0.5

        [Header(Lienzo)]
        _Lienzo("Trama de lienzo", Range(0, 1)) = 0.25
        _EscalaLienzo("Escala de la trama", Range(400, 2400)) = 1500
        _Grano("Grano", Range(0, 0.3)) = 0.06

        [Header(Vineta)]
        _ColorVineta("Color de la vineta", Color) = (0.08, 0.03, 0.04, 1)
        _Vineta("Fuerza de la vineta", Range(0, 1)) = 0.5
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
                float _TamanoPincel, _EscalaPincel, _Distorsion;
                float _Niveles, _FuerzaPosterizado, _Saturacion, _Contraste;
                float4 _ColorSombras, _ColorLuces;
                float _FuerzaTinte;
                float4 _ColorTinta;
                float _FuerzaLinea;
                float _Lienzo, _EscalaLienzo, _Grano;
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

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                float2 texel = _BlitTexture_TexelSize.xy;
                float aspecto = _ScreenParams.x / _ScreenParams.y;
                float2 uvA = uv * float2(aspecto, 1.0);

                // 1) Ondulacion suave (acuarela): mueve el UV con ruido de baja frecuencia
                float2 onda = float2(Ruido(uvA * 9.0), Ruido(uvA * 9.0 + 17.3)) - 0.5;
                float2 uvD = uv + onda * _Distorsion * texel * 2.0;

                // 2) Pinceladas: desenfoque de 5 puntos en una direccion que cambia con el ruido
                float2 p = uvA * _EscalaPincel;
                float angulo = (Ruido(p) + 0.5 * Ruido(p * 2.3 + 7.1)) * 6.2832;
                float2 dir = float2(cos(angulo), sin(angulo)) * texel * _TamanoPincel;

                float3 centro = Muestra(uvD);
                float3 col = centro * 0.33
                    + (Muestra(uvD + dir) + Muestra(uvD - dir)) * 0.22
                    + (Muestra(uvD + dir * 2.0) + Muestra(uvD - dir * 2.0)) * 0.115;

                // 3) Paleta: sombras frias (turquesa/azul), luces calidas (ocre/naranja)
                float lum = Luma(col);
                float3 tinte = lerp(_ColorSombras.rgb, _ColorLuces.rgb, saturate(lum * 1.5));
                col = lerp(col, col * tinte * 2.0, _FuerzaTinte);

                float lum2 = Luma(col);
                col = lerp(lum2.xxx, col, _Saturacion);
                col = (col - 0.5) * _Contraste + 0.5;

                // 4) Posterizado suave con ruido para que las bandas parezcan manchas de pintura
                float rPoster = Ruido(uvA * 60.0);
                float3 q = floor(col * _Niveles + rPoster) / _Niveles;
                col = lerp(col, q, _FuerzaPosterizado);

                // 5) Linea de tinta (linograbado): bordes por diferencia de luminosidad
                float lR = Luma(Muestra(uvD + float2(texel.x * 1.5, 0)));
                float lU = Luma(Muestra(uvD + float2(0, texel.y * 1.5)));
                float borde = abs(lum - lR) + abs(lum - lU);
                borde = smoothstep(0.05, 0.25, borde) * _FuerzaLinea;
                col = lerp(col, _ColorTinta.rgb, borde);

                // 6) Lienzo: trama fina + grano (el grano cambia ~12 veces por segundo)
                float2 q2 = uvA * _EscalaLienzo;
                float trama = (sin(q2.x) + sin(q2.y)) * 0.25;
                col *= 1.0 + trama * _Lienzo * 0.25;
                float tiempo = floor(_Time.y * 12.0);
                float grano = Hash(uv * _ScreenParams.xy + tiempo) - 0.5;
                col += grano * _Grano;

                // 7) Vi?eta calida oscura
                float2 d = uv - 0.5;
                float v = smoothstep(0.25, 0.85, dot(d, d) * 2.0);
                col = lerp(col, col * _ColorVineta.rgb * 4.0, v * _Vineta);

                return float4(saturate(col), 1.0);
            }
            ENDHLSL
        }
    }
}
