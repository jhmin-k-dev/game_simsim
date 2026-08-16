// 02_기술사양 §3-1: 2단 램프 + 웜톤 그림자 + 아웃라인.
// Toony Colors Pro 2($40) 도입 전까지 쓰는 무료 자체 구현 (UTS3 대안).
Shader "Nurungi/Toon"
{
    Properties
    {
        _BaseColor      ("Base Color", Color) = (1,1,1,1)
        _BaseMap        ("Base Map", 2D) = "white" {}
        _ShadowColor    ("Shadow Tint (웜톤)", Color) = (0.86,0.78,0.66,1)
        _ShadowStep     ("Shadow Step", Range(0,1)) = 0.45
        _StepSmooth     ("Step Smoothness", Range(0.001,0.2)) = 0.02
        _OutlineColor   ("Outline Color", Color) = (0.43,0.37,0.29,1)
        _OutlineWidth   ("Outline Width (m)", Range(0,0.05)) = 0.012
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        // ---- Pass 1: 아웃라인 (inverted hull) ----
        Pass
        {
            Name "Outline"
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor, _ShadowColor, _OutlineColor;
                float4 _BaseMap_ST;
                float _ShadowStep, _StepSmooth, _OutlineWidth;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings   { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                // 카메라 거리에 비례해 굵기 보정 → 멀어져도 선이 사라지지 않음 (품질기준 §2-1 1번)
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 nWS = TransformObjectToWorldNormal(IN.normalOS);
                float dist = distance(posWS, GetCameraPositionWS());
                posWS += nWS * _OutlineWidth * max(dist, 1.0);
                OUT.positionCS = TransformWorldToHClip(posWS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target { return _OutlineColor; }
            ENDHLSL
        }

        // ---- Pass 2: 툰 라이팅 (2단 램프) ----
        Pass
        {
            Name "ToonLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor, _ShadowColor, _OutlineColor;
                float4 _BaseMap_ST;
                float _ShadowStep, _StepSmooth, _OutlineWidth;
            CBUFFER_END

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            struct Attributes {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };
            struct Varyings {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS = normalize(TransformObjectToWorldNormal(IN.normalOS));
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;

                Light light = GetMainLight();
                float ndl = dot(normalize(IN.normalWS), light.direction) * 0.5 + 0.5;
                // 2단 램프: 부드럽지 않게 딱 끊는다 (품질기준 §2-1 2번)
                float ramp = smoothstep(_ShadowStep - _StepSmooth, _ShadowStep + _StepSmooth, ndl);

                // 그림자는 검게 죽이지 않고 웜톤으로 물들인다
                half3 shadowed = albedo.rgb * _ShadowColor.rgb;
                half3 col = lerp(shadowed, albedo.rgb, ramp);
                col *= lerp(0.94, 1.0, ramp);
                return half4(col, albedo.a);
            }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Lit"
}
