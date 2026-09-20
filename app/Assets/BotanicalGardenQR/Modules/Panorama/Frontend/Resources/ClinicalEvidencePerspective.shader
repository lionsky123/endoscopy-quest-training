Shader "BotanicalGardenQR/Clinical Evidence Perspective"
{
    Properties
    {
        _MainTex ("Panorama", 2D) = "white" {}
        _ViewCenter ("Panorama UV", Vector) = (.5,.5,0,0)
        _Aspect ("Window aspect", Float) = 1.77778
        _TanHalfFov ("Vertical field", Float) = .3443
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float4 _ViewCenter; float _Aspect; float _TanHalfFov;
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
            {
                Varyings output; UNITY_SETUP_INSTANCE_ID(input); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz); output.uv=input.uv; output.color=input.color; return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float lon=(_ViewCenter.x-.5)*TWO_PI, lat=(_ViewCenter.y-.5)*PI;
                float3 forward=float3(cos(lat)*sin(lon),sin(lat),cos(lat)*cos(lon));
                float3 right=float3(cos(lon),0,-sin(lon));
                float3 up=cross(forward,right);
                float2 xy=(input.uv-.5)*2*_TanHalfFov;
                float3 ray=normalize(forward+right*xy.x*_Aspect+up*xy.y);
                float2 uv=float2(atan2(ray.x,ray.z)/TWO_PI+.5,asin(clamp(ray.y,-1,1))/PI+.5);
                return SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv)*input.color;
            }
            ENDHLSL
        }
    }
}
