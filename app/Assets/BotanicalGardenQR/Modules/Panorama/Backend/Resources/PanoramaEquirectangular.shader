Shader "BotanicalGardenQR/Panorama Equirectangular"
{
    Properties { _MainTex ("Full 360 x 180 panorama", 2D) = "white" {} }
    SubShader
    {
        // After the camera skybox, before the teaching UI. Near hands still win the depth test.
        Tags { "Queue"="Transparent-100" "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 ray = normalize(mul((float3x3)unity_WorldToObject,
                    input.positionWS - GetCameraPositionWS()));
                float2 uv = float2(atan2(ray.x, ray.z) / TWO_PI + .5,
                    asin(clamp(ray.y, -1.0, 1.0)) / PI + .5);
                return half4(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).rgb, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
