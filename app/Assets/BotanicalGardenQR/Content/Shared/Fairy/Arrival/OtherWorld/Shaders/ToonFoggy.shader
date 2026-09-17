// Copyright (c) Meta Platforms, Inc. and affiliates.
// The World Beyond source lighting/fog formula, ported to URP and stencil-clipped.

Shader "TheWorldBeyond/ToonFoggy"
{
    Properties
    {
        _SaturationDistance("Saturation Distance", Range(0, 1)) = 1
        _FogCubemap("Fog Cubemap", CUBE) = "white" {}
        _FogStrength("Fog Strength", Range(0, 1)) = 1
        _FogStartDistance("Fog Start Distance", Range(0, 100)) = 1
        _FogEndDistance("Fog End Distance", Range(0, 2000)) = 100
        _FogExponent("Fog Exponent", Range(0, 1)) = 1
        _LightingRamp("Lighting Ramp", 2D) = "white" {}
        _MainTex("MainTex", 2D) = "white" {}
        _DetailTex("Detail Texture", 2D) = "white" {}
        _DetailTexStrength("Detail Strength", Range(0, 1)) = 0
        _Color("Color", Color) = (0, 0, 0, 0)
        _Overbrightening("Overbrightening", Range(0, 2)) = 1
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
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                half3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uvMain : TEXCOORD2;
                float2 uvDetail : TEXCOORD3;
                half3 worldViewDirection : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_LightingRamp); SAMPLER(sampler_LightingRamp);
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_DetailTex); SAMPLER(sampler_DetailTex);
            TEXTURECUBE(_FogCubemap); SAMPLER(sampler_FogCubemap);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _DetailTex_ST;
                half4 _Color;
                half _DetailTexStrength;
                half _FogStartDistance;
                half _FogEndDistance;
                half _FogExponent;
                half _SaturationDistance;
                half _FogStrength;
                half _Overbrightening;
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
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uvMain = TRANSFORM_TEX(input.uv, _MainTex);
                output.uvDetail = TRANSFORM_TEX(input.uv, _DetailTex);
                output.worldViewDirection = normalize(GetWorldSpaceViewDir(position.positionWS));
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 mainTexture = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uvMain);
                half3 detail = SAMPLE_TEXTURE2D(_DetailTex, sampler_DetailTex, input.uvDetail).rgb;
                mainTexture.rgb *= lerp(1.0h.xxx, detail, _DetailTexStrength);

                half3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight();
                half halfLambert = dot(mainLight.direction, normalWS) * 0.5h + 0.5h;
                half3 lightingRamp = SAMPLE_TEXTURE2D(_LightingRamp, sampler_LightingRamp, halfLambert.xx).rgb;
                half3 finalLighting = mainLight.color * lightingRamp + SampleSH(normalWS);
                half3 litTexture = finalLighting * mainTexture.rgb * _Color.rgb * _Overbrightening;

                half foggingRange = saturate(
                    (distance(_WorldSpaceCameraPos, input.positionWS) - _FogStartDistance) /
                    max(0.001h, _FogEndDistance - _FogStartDistance));
                foggingRange = FastPow(foggingRange, _FogExponent);
                half3 foggingColor = SAMPLE_TEXTURECUBE(
                    _FogCubemap,
                    sampler_FogCubemap,
                    input.worldViewDirection).rgb;
                half3 foggedColor = lerp(litTexture, foggingColor, foggingRange * _FogStrength);
                half desaturatedColor = dot(foggedColor, 1.2h * half3(0.299h, 0.587h, 0.114h));
                half satDistance = saturate((_SaturationDistance * 11.0h) - (foggingRange * 10.0h));
                half3 finalColor = lerp(foggedColor, desaturatedColor.xxx, satDistance);
                finalColor = finalColor / max(
                    half3(0.0001h, 0.0001h, 0.0001h),
                    0.545h * finalColor + 0.455h);
                return half4(finalColor, 1.0h);
            }
            ENDHLSL
        }
    }
}
