// Imagen de UI en escala de grises con un poco más de contraste: la cámara del jugador dentro de Calibración
// queda monocroma como el resto del museo (la cámara de UI no tiene postproceso).
Shader "CuandoNadieMira/UI Grayscale"
{
    Properties
    {
        [PerRendererData] _MainTex ("Textura", 2D) = "white" {}
        _Color ("Tinte", Color) = (1,1,1,1)
        _Contrast ("Contraste", Range(0.5, 2)) = 1.1
        _Gamma ("Gamma (menos = medios más claros)", Range(0.2, 1.5)) = 0.6
        _Gain ("Brillo", Range(0.5, 3)) = 1.3
        _Vignette ("Viñeta", Range(0, 1)) = 0.35

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; float4 local : TEXCOORD1; float2 rawUv : TEXCOORD2; };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _Contrast, _Vignette, _Gamma, _Gain;
            float4 _ClipRect;

            v2f vert (appdata v)
            {
                v2f o;
                o.local = v.vertex;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.rawUv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                float l = dot(c.rgb, float3(0.299, 0.587, 0.114));
                // El postproceso del museo (tonemapping + contraste) oscurece mucho los medios tonos de la cámara:
                // se aclaran aquí para que el rostro se vea
                l = saturate(pow(saturate(l), _Gamma) * _Gain);
                l = saturate((l - 0.5) * _Contrast + 0.5);
                float2 d = i.rawUv - 0.5;
                l *= 1.0 - _Vignette * saturate(dot(d, d) * 2.2);
                fixed4 r = fixed4(l, l, l, c.a) * i.color;
                #ifdef UNITY_UI_CLIP_RECT
                r.a *= UnityGet2DClipping(i.local.xy, _ClipRect);
                #endif
                return r;
            }
            ENDCG
        }
    }
}
