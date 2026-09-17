// Copyright (c) Meta Platforms, Inc. and affiliates.
// Compatibility boundary for the automatic, non-handheld third-discovery window.

Shader "BotanicalGardenQR/Fairy/OtherWorldPortalStencil"
{
    Properties
    {
        _MainTex("Official Flashlight Mask", 2D) = "white" {}
        _BoundaryAtlas("Unity SmokePortal boundary atlas", 2D) = "black" {}
        _FlowAtlas("Unity SmokePortal flow atlas", 2D) = "black" {}
        _Intensity("Intensity", Range(0, 1)) = 1
        _RiftAmount("Local rupture", Range(0,1)) = 0
        _RitualTime("Authored time", Float) = 0
        [HideInInspector] _Rim("Rim pass", Float) = 0
        [HideInInspector] _ColorMask("Color mask", Float) = 0
        [HideInInspector] _StencilWriteMask("Stencil write mask", Float) = 255
        [HideInInspector] _ZTest("Depth test", Float) = 8
        [IntRange] _StencilRef("Stencil Ref", Range(0, 255)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry-100"
        }

        Pass
        {
            Name "OtherWorldPortalStencil"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Off
            ZWrite Off
            ZTest [_ZTest]
            ColorMask [_ColorMask]
            Blend SrcAlpha OneMinusSrcAlpha
            Stencil
            {
                Ref [_StencilRef]
                WriteMask [_StencilWriteMask]
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "ArrivalBoundary.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half alpha : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half _Intensity;
                half _StencilRef;
                half _RiftAmount;
                float _RitualTime;
                float _Rim;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.alpha = input.color.a;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                clip(_Intensity - .001);
                float boundary = ArrivalBoundary(input.uv * 2 - 1, _RiftAmount * _Intensity, _RitualTime);
                if (_Rim > .5)
                {
                    clip(.16 - abs(boundary));
                    float4 smoke = ArrivalFlow(input.uv * 2 - 1, _RiftAmount * _Intensity, _RitualTime);
                    float edge = 1 - smoothstep(.035, .16, abs(boundary));
                    float light = dot(smoke.rgb, float3(.25,.55,.2));
                    return half4(lerp(half3(.06,.18,.2), half3(.58,.88,.72), light), smoke.a * edge * _Intensity);
                }
                clip(boundary);
                return 0;
            }
            ENDHLSL
        }
    }
}
