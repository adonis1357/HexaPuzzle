Shader "UI/HexSpecialGem"
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

        // Base gem properties (same as HexGem)
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

        // Special block animation properties
        _ShimmerSpeed ("Shimmer Speed", Range(0, 5)) = 1.5
        _ShimmerIntensity ("Shimmer Intensity", Range(0, 0.5)) = 0.08
        _EnergyPulse ("Energy Pulse Amount", Range(0, 1)) = 0.15
        _RainbowStrength ("Rainbow Shift Strength", Range(0, 1)) = 0
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

            // Base gem
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

            // Special animation
            float _ShimmerSpeed;
            float _ShimmerIntensity;
            float _EnergyPulse;
            float _RainbowStrength;

            // Simple RGB to HSL conversion
            float3 RGBtoHSL(float3 rgb)
            {
                float maxC = max(rgb.r, max(rgb.g, rgb.b));
                float minC = min(rgb.r, min(rgb.g, rgb.b));
                float l = (maxC + minC) * 0.5;
                float s = 0;
                float h = 0;

                if (maxC != minC)
                {
                    float d = maxC - minC;
                    s = l > 0.5 ? d / (2.0 - maxC - minC) : d / (maxC + minC);

                    if (maxC == rgb.r)
                        h = (rgb.g - rgb.b) / d + (rgb.g < rgb.b ? 6.0 : 0.0);
                    else if (maxC == rgb.g)
                        h = (rgb.b - rgb.r) / d + 2.0;
                    else
                        h = (rgb.r - rgb.g) / d + 4.0;

                    h /= 6.0;
                }

                return float3(h, s, l);
            }

            float HueToRGB(float p, float q, float t)
            {
                if (t < 0.0) t += 1.0;
                if (t > 1.0) t -= 1.0;
                if (t < 1.0 / 6.0) return p + (q - p) * 6.0 * t;
                if (t < 0.5) return q;
                if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6.0;
                return p;
            }

            float3 HSLtoRGB(float3 hsl)
            {
                float h = hsl.x;
                float s = hsl.y;
                float l = hsl.z;

                if (s < 0.001)
                    return float3(l, l, l);

                float q = l < 0.5 ? l * (1.0 + s) : l + s - l * s;
                float p = 2.0 * l - q;

                float r = HueToRGB(p, q, h + 1.0 / 3.0);
                float g = HueToRGB(p, q, h);
                float b = HueToRGB(p, q, h - 1.0 / 3.0);

                return float3(r, g, b);
            }

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
                float normalizedDist = saturate(dist / 0.43);

                // === Base gem effects (same as HexGem) ===

                // Radial depth gradient
                float depthFactor = 1.0 - normalizedDist * normalizedDist * _DepthGradient;

                // Edge darkening
                float edgeDark = 1.0 - smoothstep(0.25, 0.43, dist) * _EdgeDarken;

                // Facet pattern
                float facetBright = 1.0;
                if (_FacetIntensity > 0.001)
                {
                    float angle = atan2(centered.y, centered.x);
                    float facetAngle = frac(angle / (2.0 * 3.14159) * _FacetCount);
                    facetBright = 1.0 + (sin(facetAngle * 2.0 * 3.14159) * 0.5 + 0.5) * _FacetIntensity;
                }

                // Primary specular highlight
                float highlightDist = length(uv - _HighlightPos.xy);
                float highlight = smoothstep(_HighlightSize, 0.0, highlightDist) * _HighlightIntensity;

                // Secondary specular highlight
                float secDist = length(uv - _SecondaryHighlightPos.xy);
                float secHighlight = smoothstep(_SecondaryHighlightSize, 0.0, secDist) * _HighlightIntensity * 0.4;

                // Inner glow
                float innerGlow = (1.0 - normalizedDist) * (1.0 - normalizedDist) * _InnerGlow;

                // === Special block animation effects ===

                // Shimmer: diagonal band sweeping across the gem
                float shimmerPhase = frac(_Time.y * _ShimmerSpeed);
                float shimmerPos = shimmerPhase * 1.4 - 0.2;
                float shimmer = smoothstep(0.08, 0.0, abs(dot(centered, float2(0.707, 0.707)) - shimmerPos));
                shimmer *= _ShimmerIntensity;

                // Energy pulse: subtle brightness oscillation
                float energy = sin(_Time.y * 3.0) * 0.5 + 0.5;
                float energyMul = 1.0 + energy * _EnergyPulse;

                // Composite color (texture RGB * vertex color)
                float3 baseColor = texColor.rgb * i.color.rgb;

                // Apply rainbow shift if enabled
                if (_RainbowStrength > 0.01)
                {
                    float3 hsl = RGBtoHSL(baseColor);
                    hsl.x = frac(hsl.x + _Time.y * 0.3 * _RainbowStrength);
                    baseColor = HSLtoRGB(hsl);
                }

                float3 gemColor = baseColor * depthFactor * edgeDark * facetBright * energyMul;
                gemColor += baseColor * innerGlow;
                gemColor += float3(1, 1, 1) * highlight;
                gemColor += float3(1, 1, 1) * secHighlight;
                gemColor += float3(1, 1, 1) * shimmer;

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
