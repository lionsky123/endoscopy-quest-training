// Copyright (c) Meta Platforms, Inc. and affiliates.
// Adapted from Unity-TheWorldBeyond/ToonFoggyOppy.shader for URP 17.

Shader "TheWorldBeyond/ToonFoggyOppy"
{
    Properties
    {
        _SaturationAmount("Saturation Amount", Range(0, 1)) = 1
        _LightingRamp("Lighting Ramp", 2D) = "white" {}
        _MainTex("Main Texture", 2D) = "white" {}
        [HideInInspector] _BoundaryAtlas("Unity SmokePortal boundary atlas", 2D) = "black" {}
        [HideInInspector] _FlowAtlas("Unity SmokePortal flow atlas", 2D) = "black" {}
        _Color("Color", Color) = (1, 1, 1, 1)
        _GlowColor("Glow Color", Color) = (0, 0.82, 1, 1)
        _GlowStrength("Glow Strength", Range(0, 1)) = 0
        [HideInInspector] _ArrivalCrossing("Continuous crossing", Float) = 0
        [HideInInspector] _ArrivalCenter("Boundary center", Vector) = (0,0,0,0)
        [HideInInspector] _ArrivalNormal("Boundary normal", Vector) = (0,0,1,0)
        [HideInInspector] _ArrivalRight("Boundary right", Vector) = (1,0,0,0)
        [HideInInspector] _ArrivalSize("Boundary size", Vector) = (2.2,2,0,0)
        [HideInInspector] _ArrivalOpening("Boundary opening", Float) = 1
        [HideInInspector] _ArrivalTime("Boundary time", Float) = 0
        [HideInInspector] _PortalStencilRef("Portal Stencil Ref", Float) = 1
        [HideInInspector] _PortalStencilReadMask("Portal Stencil Read Mask", Float) = 255
        [HideInInspector] [Enum(UnityEngine.Rendering.CompareFunction)] _PortalStencilComp("Portal Stencil Comp", Float) = 8
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

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
            #include "../../../Arrival/ArrivalBoundary.hlsl"

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
                float2 uv : TEXCOORD2;
                half2 throbber : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_LightingRamp);
            SAMPLER(sampler_LightingRamp);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half4 _GlowColor;
                half _GlowStrength;
                half _SaturationAmount;
                float _ArrivalCrossing, _ArrivalOpening, _ArrivalTime;
                float4 _ArrivalCenter, _ArrivalNormal, _ArrivalRight, _ArrivalSize;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.throbber.x = sin((_Time.y * 2.9h) + (input.positionOS.x * 7.0h));
                output.throbber.y = sin((_Time.y * 3.1h) - (input.positionOS.z * 6.5h));
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                if (_ArrivalCrossing > .5 && dot(input.positionWS - _ArrivalCenter.xyz, _ArrivalNormal.xyz) > 0)
                {
                    // Only the part still in the garden is clipped. Intersect this eye's ray with the door.
                    float3 ray = input.positionWS - GetCameraPositionWS();
                    float denominator = dot(ray, _ArrivalNormal.xyz);
                    float distance = dot(_ArrivalCenter.xyz - GetCameraPositionWS(), _ArrivalNormal.xyz);
                    float3 hit = GetCameraPositionWS() + ray * (distance / (abs(denominator) < .00001 ? .00001 : denominator));
                    float3 local = hit - _ArrivalCenter.xyz;
                    float2 p = float2(dot(local, _ArrivalRight.xyz), local.y) * 2 / _ArrivalSize.xy;
                    clip(ArrivalBoundary(p, _ArrivalOpening, _ArrivalTime));
                }

                half4 mainTexture = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight();
                half halfLambert = dot(mainLight.direction, normalWS) * 0.5h + 0.5h;
                half3 ramp = SAMPLE_TEXTURE2D(
                    _LightingRamp,
                    sampler_LightingRamp,
                    half2(halfLambert, 0.5h)).rgb;
                half3 ambient = SampleSH(normalWS);
                half3 lighting = ambient + mainLight.color * ramp;

                half glowThrob = (_GlowStrength * 2.0h) *
                    (((input.throbber.x + input.throbber.y) * 0.25h + 0.5h) + 0.5h);
                half3 glowingAreas = (1.0h - mainTexture.a) * _GlowColor.rgb * glowThrob;
                half3 litTexture = lighting * mainTexture.rgb * _Color.rgb * 1.15h + glowingAreas;
                half luminance = dot(litTexture, half3(0.299h, 0.587h, 0.114h));
                half3 finalColor = lerp(luminance.xxx, litTexture, _SaturationAmount);
                return half4(finalColor, 1.0h);
            }
            ENDHLSL
        }
    }
}
