// Cuando nadie mira - Shader "Distorsion Magica" (URP) para particulas.
// Deforma lo que hay detras (como el aire caliente) con un remolino de ruido, separa los canales de color
// (aberracion cromatica) y anade un brillo de fantasia. Pensado para un Particle System con billboards grandes.
// REQUIERE "Opaque Texture" activado en el URP Asset (Rendering).
// Solo ASCII (el parser de Unity no acepta tildes ni enie).
Shader "Cuando Nadie Mira/Distorsion Magica"
{
    Properties
    {
        _Intensidad("Fuerza de la distorsion", Range(0, 0.2)) = 0.04
        _Cromatica("Aberracion de color", Range(0, 2)) = 0.8
        _Escala("Escala del remolino", Range(0.5, 8)) = 2.5
        _Velocidad("Velocidad del remolino", Range(0, 5)) = 1.5
        _Borde("Suavidad del borde", Range(0.2, 4)) = 1.5
        [HDR] _ColorBrillo("Color del brillo", Color) = (0.3, 0.8, 1.0, 1)
        _Brillo("Fuerza del brillo", Range(0, 3)) = 0.8
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Distorsion"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Intensidad, _Cromatica, _Escala, _Velocidad, _Borde, _Brillo;
                float4 _ColorBrillo;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
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
                o.screenPos = vp.positionNDC;
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // Mascara redonda y suave (el billboard del particle no necesita textura)
                float2 c = i.uv - 0.5;
                float mask = pow(saturate(1.0 - length(c) * 2.0), _Borde);

                // Remolino de ruido que se mueve; cada particula tiene un ruido distinto segun donde esta
                float t = _Time.y * _Velocidad;
                float2 p = (i.uv + i.positionWS.xz * 0.37 + i.positionWS.y * 0.21) * _Escala;
                float n1 = Ruido(p + t);
                float n2 = Ruido(p + 17.3 - t * 0.8);
                float2 giro = float2(-c.y, c.x);                                   // empuja en espiral
                float2 d = ((float2(n1, n2) - 0.5) * 2.0 + giro * 1.5) * _Intensidad * mask * i.color.a;

                // Lee lo que hay detras con un desplazamiento distinto por canal (croma)
                float2 suv = i.screenPos.xy / i.screenPos.w;
                float r = SampleSceneColor(suv + d * (1.0 + _Cromatica)).r;
                float g = SampleSceneColor(suv + d).g;
                float b = SampleSceneColor(suv + d * (1.0 - _Cromatica)).b;
                float3 col = float3(r, g, b);

                // Brillo de fantasia en las crestas del remolino, tenido por el color de la particula
                float cresta = smoothstep(0.45, 0.9, n1 * 0.6 + n2 * 0.4);
                float3 brillo = _ColorBrillo.rgb * i.color.rgb * _Brillo * mask * cresta * i.color.a;

                return half4(col + brillo, saturate(mask * 2.0 * i.color.a));
            }
            ENDHLSL
        }
    }
}
