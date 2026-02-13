Shader "UI/HexBackground"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15

        // Background effect properties
        _ShadowIntensity ("Inner Shadow Intensity", Range(0, 1)) = 0.35
        _ShadowOffset ("Shadow Offset", Vector) = (0.03, -0.05, 0, 0)
        _VignetteStrength ("Vignette Strength", Range(0, 1)) = 0.4
        _RimLightIntensity ("Rim Light Intensity", Range(0, 0.3)) = 0.12
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

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
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _ClipRect;

            float _ShadowIntensity;
            float4 _ShadowOffset;
            float _VignetteStrength;
            float _RimLightIntensity;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(o.worldPosition);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 texColor = tex2D(_MainTex, i.texcoord);
                float2 uv = i.texcoord;

                // Inner shadow with directional offset
                float2 shadowCenter = uv - 0.5 + _ShadowOffset.xy;
                float shadowDist = length(shadowCenter);
                float vignette = 1.0 - smoothstep(0.15, 0.45, shadowDist) * _VignetteStrength;

                // Subtle rim light on upper-left edge
                float2 centered = uv - 0.5;
                float edgeDist = length(centered);
                float2 rimDir = normalize(float2(-1, 1)); // top-left light direction
                float rimDot = dot(normalize(centered + 0.001), rimDir);
                float rim = saturate(rimDot) * _RimLightIntensity * smoothstep(0.3, 0.45, edgeDist);

                float3 bgColor = i.color.rgb * vignette + float3(rim, rim, rim);

                float alpha = texColor.a * i.color.a;

                #ifdef UNITY_UI_CLIP_RECT
                alpha *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(alpha - 0.001);
                #endif

                return fixed4(bgColor, alpha);
            }
            ENDCG
        }
    }
}
