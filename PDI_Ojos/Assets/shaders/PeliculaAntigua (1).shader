// Cuando nadie mira - Shader "Pelicula antigua" (URP, Fullscreen Pass Renderer Feature)
// Fotografia de cine clasico (tipo Casablanca): plata suave, negros levantados, luces cremosas con difusion,
// grano fino de emulsion (esponjoso, no ruido de pixel), temblor de proyector, rayones y polvo discretos.
// Todo se anima a "fotogramas por segundo" (como un rollo), no suave.
// Costo: 11 lecturas de textura por pixel. Solo ASCII (el parser de Unity no acepta tildes ni enie).
Shader "Cuando Nadie Mira/Pelicula Antigua"
{
    Properties
    {
        [Header(Rollo)]
        _FPS("Fotogramas por segundo del rollo", Range(8, 30)) = 20
        _Temblor("Temblor del proyector (px)", Range(0, 6)) = 0.6
        _Parpadeo("Parpadeo de exposicion", Range(0, 0.3)) = 0.04

        [Header(Imagen)]
        _Contraste("Contraste de pelicula", Range(0, 1)) = 0.3
        _Gamma("Oscurecer medios tonos", Range(0.7, 1.6)) = 1.0
        _Hombro("Suavizar luces (rolloff)", Range(0, 1)) = 0.5
        _ColorSombras("Color de sombras (negros levantados)", Color) = (0.05, 0.05, 0.055, 1)
        _ColorLuces("Color de luces (virado)", Color) = (0.97, 0.96, 0.93, 1)
        _ColorResidual("Color original que se conserva", Range(0, 1)) = 0.0

        [Header(Lente)]
        _Difusion("Enfoque suave (difusion)", Range(0, 1)) = 0.3
        _Glow("Resplandor en las luces", Range(0, 1)) = 0.35
        _UmbralGlow("Desde que brillo", Range(0.3, 0.95)) = 0.55
        _RadioGlow("Radio (px)", Range(1, 14)) = 5
        _Aberracion("Aberracion en los bordes", Range(0, 2)) = 0.25

        [Header(Grano y desgaste)]
        _Grano("Grano de emulsion", Range(0, 0.3)) = 0.06
        _TamanoGrano("Tamano del grano (px)", Range(1, 4)) = 1.6
        _Rayones("Rayones verticales", Range(0, 1)) = 0.12
        _Polvo("Polvo y manchas", Range(0, 1)) = 0.25
        _EscalaPolvo("Densidad del polvo", Range(10, 80)) = 40

        [Header(Marco)]
        _Vineta("Vineta", Range(0, 1)) = 0.35
        _Barras("Barras de cine (0 = sin barras)", Range(0, 0.2)) = 0.0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        Pass
        {
            Name "Pelicula"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _FPS, _Temblor, _Parpadeo;
                float _Contraste, _Gamma, _Hombro, _ColorResidual;
                float4 _ColorSombras, _ColorLuces;
                float _Difusion, _Glow, _UmbralGlow, _RadioGlow, _Aberracion;
                float _Grano, _TamanoGrano, _Rayones, _Polvo, _EscalaPolvo;
                float _Vineta, _Barras;
            CBUFFER_END

            // Hash sin senos y sin patrones visibles (el anterior dejaba lineas horizontales en el grano)
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

                // Fotograma actual del rollo: todo el desgaste cambia solo cuando cambia el fotograma
                float t = floor(_Time.y * _FPS);
                float r1 = Hash(float2(t, 1.3));
                float r2 = Hash(float2(t, 7.7));
                float r3 = Hash(float2(t, 4.1));

                // 1) Temblor del proyector
                float2 uvT = uv + (float2(r1, r2) - 0.5) * _Temblor * texel * 2.0;

                // 2) Aberracion cromatica de lente vieja (crece hacia los bordes)
                float2 off = (uv - 0.5) * _Aberracion * 0.004;
                float3 col = float3(Muestra(uvT + off).r, Muestra(uvT).g, Muestra(uvT - off).b);
                float lum = Luma(col);

                // 3) Difusion y halacion: 8 puntos (cruz + diagonales). Suaviza el enfoque y hace sangrar las luces
                float2 rg = texel * _RadioGlow;
                float2 rd = rg * 0.7;
                float3 gl = Muestra(uvT + float2(rg.x, 0)) + Muestra(uvT - float2(rg.x, 0))
                          + Muestra(uvT + float2(0, rg.y)) + Muestra(uvT - float2(0, rg.y))
                          + Muestra(uvT + rd) + Muestra(uvT - rd)
                          + Muestra(uvT + float2(rd.x, -rd.y)) + Muestra(uvT + float2(-rd.x, rd.y));
                float lumDif = Luma(gl * 0.125);
                float brilloGl = saturate((lumDif - _UmbralGlow) * 3.0);
                float v = lerp(lum, lumDif, _Difusion) + brilloGl * _Glow;

                // 4) Exposicion parpadeante, curva de contraste y hombro suave en las luces (como la emulsion real)
                v *= 1.0 + (r3 - 0.5) * _Parpadeo + sin(_Time.y * 3.1) * 0.008;
                v = saturate(v);
                v = lerp(v, v * v * (3.0 - 2.0 * v), _Contraste);
                float hombro = (1.0 - exp(-v * 2.0)) / (1.0 - exp(-2.0));
                v = lerp(v, hombro, _Hombro);
                v = pow(saturate(v), _Gamma);

                // 5) Mapa de color: negro-sepia en sombras, papel calido en luces (+ algo del color original si se quiere)
                float3 mono = lerp(_ColorSombras.rgb, _ColorLuces.rgb, v);
                float3 croma = clamp(col / max(lum, 0.001), 0.0, 3.0);
                col = lerp(mono, mono * croma, _ColorResidual);

                // 6) Rayones verticales (2 posibles por fotograma, con tramos interrumpidos)
                float rayon = 0.0;
                [unroll]
                for (int k = 0; k < 2; k++)
                {
                    float x = Hash(float2(t, k * 3.1 + 0.5));
                    float activo = step(Hash(float2(t, k * 5.7 + 2.0)), _Rayones * 0.5);
                    float ancho = 0.0005 + 0.0007 * Hash(float2(t, k + 9.0));
                    float linea = smoothstep(ancho, 0.0, abs(uv.x - x));
                    float tramo = step(0.35, Ruido(float2(uv.y * 6.0 + k * 10.0, t * 0.7)));
                    rayon += linea * activo * tramo;
                }
                col += rayon * _ColorLuces.rgb * 0.6;

                // 7) Polvo y manchas: motas oscuras o claras que cambian de sitio en cada fotograma
                float2 g = uv * float2(aspecto, 1.0) * _EscalaPolvo;
                float2 id = floor(g);
                float2 fr = frac(g);
                float hay = step(1.0 - _Polvo * 0.03, Hash(id + t * 0.131));
                float2 centro = float2(Hash(id + 7.3), Hash(id + 3.9)) * 0.6 + 0.2;
                float radio = 0.05 + 0.12 * Hash(id + 1.1);
                float mota = hay * smoothstep(radio, radio * 0.4, length(fr - centro));
                float oscura = step(0.35, Hash(id + 11.0));
                col = lerp(col, col * 0.15, mota * oscura) + mota * (1.0 - oscura) * 0.35 * _ColorLuces.rgb;

                // 8) Grano de emulsion: ruido suave y "esponjoso" (dos escalas), distinto en cada fotograma,
                //    que se concentra en los medios tonos y casi desaparece en negros y blancos puros
                float2 gp = uv * _ScreenParams.xy / _TamanoGrano;
                float2 desp = float2(Hash(float2(t, 21.7)), Hash(float2(t, 33.1))) * 500.0;
                float n = Ruido(gp + desp) * 0.6 + Ruido(gp * 2.3 + desp * 1.7) * 0.4;
                float grano = (n - 0.5) * 3.0;
                float medios = 0.35 + 0.65 * 4.0 * v * (1.0 - v);
                col += grano * _Grano * medios;

                // 9) Vineta de lente (parpadea un poco) y barras de cine opcionales
                float2 d = uv - 0.5;
                d.x *= aspecto;
                float vig = smoothstep(0.35, 1.0, length(d) * 1.25);
                col *= 1.0 - vig * _Vineta * (1.0 + (r1 - 0.5) * 0.2);

                float borde = 0.5 - abs(uv.y - 0.5);                       // 0 en el borde, 0.5 en el centro
                col *= smoothstep(_Barras - 0.004, _Barras, borde);        // oscurece las franjas de arriba y abajo

                return float4(saturate(col), 1.0);
            }
            ENDHLSL
        }
    }
}
