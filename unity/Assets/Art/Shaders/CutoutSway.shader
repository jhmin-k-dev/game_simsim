// 02 §3-3 / 04 §2-4 3번: 컷아웃(나무·덤불)이 바람에 미세하게 흔들린다.
// 정지 프레임을 없애는 것이 목적 — 과하면 판이 들통나므로 아주 은은하게.
Shader "Nurungi/CutoutSway"
{
    Properties
    {
        _BaseMap   ("Texture", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _Cutoff    ("Alpha Cutoff", Range(0,1)) = 0.5
        _SwayAmp   ("Sway Amplitude (m)", Range(0, 0.2)) = 0.035
        _SwaySpeed ("Sway Speed", Range(0, 5)) = 1.1
        _SwayVar   ("Per-object Phase Variance", Range(0, 10)) = 3.7
    }

    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" }

        Pass
        {
            Name "Forward"
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                float _Cutoff, _SwayAmp, _SwaySpeed, _SwayVar;
            CBUFFER_END

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);

                // 오브젝트 위치로 위상을 흩어 나무마다 다르게 흔들리게
                float phase = dot(GetObjectToWorldMatrix()._m03_m13_m23, float3(1, 1, 1)) * _SwayVar;
                // 위쪽(uv.y↑)일수록 크게 — 뿌리는 고정
                float weight = IN.uv.y * IN.uv.y;
                float t = _Time.y * _SwaySpeed + phase;
                posWS.x += (sin(t) + 0.4 * sin(t * 2.7)) * _SwayAmp * weight;
                posWS.y += 0.25 * sin(t * 1.3 + 1.0) * _SwayAmp * weight;

                OUT.positionCS = TransformWorldToHClip(posWS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;
                clip(c.a - _Cutoff);
                return c;
            }
            ENDHLSL
        }
    }
}
