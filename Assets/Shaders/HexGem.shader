Shader "UI/HexGem"
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

        // Gem effect properties
        _HighlightPos ("Highlight Position", Vector) = (0.35, 0.65, 0, 0)
        _HighlightSize ("Highlight Size", Range(0.05, 0.4)) = 0.10
        _HighlightIntensity ("Highlight Intensity", Range(0, 2)) = 0.35
        _SecondaryHighlightPos ("Secondary Highlight Pos", Vector) = (0.6, 0.3, 0, 0)
        _SecondaryHighlightSize ("Secondary Highlight Size", Range(0.02, 0.2)) = 0.05
        _EdgeDarken ("Edge Darkening", Range(0, 1)) = 0.25
        _InnerGlow ("Inner Glow Intensity", Range(0, 1)) = 0.08
        _DepthGradient ("Depth Gradient Strength", Range(0, 1)) = 0.2
        _FacetCount ("Facet Count", Float) = 6
        _FacetIntensity ("Facet Intensity", Range(0, 0.3)) = 0.03
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

            float4 _HighlightPos;
            float _HighlightSize;
            float _HighlightIntensity;
            float4 _SecondaryHighlightPos;
            float _SecondaryHighlightSize;
            float _EdgeDarken;
            float _InnerGlow;
            float _DepthGradient;
            float _FacetCount;
            float _FacetIntensity;

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
                float2 centered = uv - 0.5;
                float dist = length(centered);

                // Normalized distance (hex inscribed radius ~0.43 in UV space)
                float normalizedDist = saturate(dist / 0.43);

                // 1. Radial depth gradient - center bright, edges dark
                float depthFactor = 1.0 - normalizedDist * normalizedDist * _DepthGradient;

                // 2. Edge darkening (vignette)
                float edgeDark = 1.0 - smoothstep(0.25, 0.43, dist) * _EdgeDarken;

                // 3. Faceted gem pattern - angular brightness variation
                float facetBright = 1.0;
                if (_FacetIntensity > 0.001)
                {
                    float angle = atan2(centered.y, centered.x);
                    float facetAngle = frac(angle / (2.0 * 3.14159) * _FacetCount);
                    facetBright = 1.0 + (sin(facetAngle * 2.0 * 3.14159) * 0.5 + 0.5) * _FacetIntensity;
                }

                // 4. Primary specular highlight (upper-left)
                float highlightDist = length(uv - _HighlightPos.xy);
                float highlight = smoothstep(_HighlightSize, 0.0, highlightDist) * _HighlightIntensity;

                // 5. Secondary specular highlight (lower-right)
                float secDist = length(uv - _SecondaryHighlightPos.xy);
                float secHighlight = smoothstep(_SecondaryHighlightSize, 0.0, secDist) * _HighlightIntensity * 0.4;

                // 6. Inner glow - additive center brightness
                float innerGlow = (1.0 - normalizedDist) * (1.0 - normalizedDist) * _InnerGlow;

                // Composite color (texture RGB * vertex color: supports both
                // white procedural sprites with colored tint AND pre-colored textures)
                float3 baseColor = texColor.rgb * i.color.rgb;
                float3 gemColor = baseColor * depthFactor * edgeDark * facetBright;
                gemColor += baseColor * innerGlow;
                gemColor += float3(1, 1, 1) * highlight;
                gemColor += float3(1, 1, 1) * secHighlight;

                float alpha = texColor.a * i.color.a;

                #ifdef UNITY_UI_CLIP_RECT
                alpha *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(alpha - 0.001);
                #endif

                return fixed4(gemColor, alpha);
            }
            ENDCG
        }
    }
}
