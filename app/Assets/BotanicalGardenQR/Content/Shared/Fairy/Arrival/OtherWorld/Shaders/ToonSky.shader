// Copyright (c) Meta Platforms, Inc. and affiliates.
// The World Beyond source sky layering formula, ported to URP and stencil-clipped.

Shader "TheWorldBeyond/ToonSky"
{
    Properties
    {
        _SaturationDistance("Saturation Distance", Range(0, 1)) = 1
        _FogCubemap("Fog Cubemap", CUBE) = "white" {}
        _FogStrength("Fog Strength", Range(0, 1)) = 1
        _FogStartDistance("Fog Start Distance", Range(0, 100)) = 1
        _FogEndDistance("Fog End Distance", Range(0, 2000)) = 100
        _FogExponent("Fog Exponent", Range(0, 1)) = 1
        _MainTex("MainTex", 2D) = "white" {}
        _CloudColor("Cloud Color", Color) = (0, 0, 0, 0)
        _CloudMixStrength("Cloud Mix Strength", Range(0, 1)) = 1
        _MountainColor("Mountains Color", Color) = (0, 0, 0, 0)
        _MountainMixStrength("Mountain Mix Strength", Range(0, 1)) = 1
        [HideInInspector] _PortalStencilRef("Portal Stencil Ref", Float) = 1
        [HideInInspector] _PortalStencilReadMask("Portal Stencil Read Mask", Float) = 255
        [HideInInspector] [Enum(UnityEngine.Rendering.CompareFunction)] _PortalStencilComp("Portal Stencil Comp", Float) = 3
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On
            ZTest LEqual
            Stencil
            {
                Ref [_PortalStencilRef]
                ReadMask [_PortalStencilReadMask]
                Comp [_PortalStencilComp]
                Pass Keep
            }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                half3 worldViewDirection : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURECUBE(_FogCubemap); SAMPLER(sampler_FogCubemap);

            CBUFFER_START(UnityPerMaterial)
                half _SaturationDistance;
                half _FogStrength;
                half _FogStartDistance;
                half _FogEndDistance;
                half _FogExponent;
                half4 _CloudColor;
                half _CloudMixStrength;
                half4 _MountainColor;
                half _MountainMixStrength;
                half _PortalStencilRef;
                half _PortalStencilReadMask;
                half _PortalStencilComp;
            CBUFFER_END

            half FastPow(half value, half exponent)
            {
                return value / max(0.0001h, (1.0h - exponent) * value + exponent);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.uv = input.uv * float2(2.0, 1.0);
                output.worldViewDirection = normalize(GetWorldSpaceViewDir(position.positionWS));
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 mainTexture = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half3 foggingColor = SAMPLE_TEXTURECUBE(
                    _FogCubemap,
                    sampler_FogCubemap,
                    input.worldViewDirection).rgb;
                half3 clouds = lerp(foggingColor, _CloudColor.rgb, mainTexture.r * _CloudMixStrength);
                half3 mountains = lerp(foggingColor, _MountainColor.rgb, mainTexture.g * _MountainMixStrength);
                half3 mountainsOverClouds = lerp(clouds, mountains, mainTexture.b);

                half foggingRange = saturate(
                    (distance(_WorldSpaceCameraPos, input.positionWS) - _FogStartDistance) /
                    max(0.001h, _FogEndDistance - _FogStartDistance));
                foggingRange = FastPow(foggingRange, _FogExponent);
                half desaturatedColor = dot(
                    mountainsOverClouds,
                    1.2h * half3(0.299h, 0.587h, 0.114h));
                half satDistance = saturate((_SaturationDistance * 11.0h) - (foggingRange * 10.0h));
                half3 finalColor = lerp(mountainsOverClouds, desaturatedColor.xxx, satDistance);
                finalColor = finalColor / max(
                    half3(0.0001h, 0.0001h, 0.0001h),
                    0.545h * finalColor + 0.455h);
                return half4(finalColor, 1.0h);
            }
            ENDHLSL
        }
    }
}
