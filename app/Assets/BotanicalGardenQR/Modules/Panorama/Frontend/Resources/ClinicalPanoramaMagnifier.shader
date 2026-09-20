Shader "BotanicalGardenQR/Clinical Panorama Magnifier"
{
    Properties
    {
        _MainTex ("Panorama", 2D) = "white" {}
        _Magnification ("Magnification", Float) = 4
        _YawRadians ("Panorama yaw", Float) = 0
        _LensCenterOS ("Lens centre in mesh coordinates", Vector) = (0,0,0,0)
        _PanoramaOrigin ("Fixed panorama centre", Vector) = (0,0,0,0)
        _PanoramaRadius ("Panorama radius", Float) = 2.8
        _Active ("Held in viewing position", Float) = 0
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
            CBUFFER_START(UnityPerMaterial)
                float4 _LensCenterOS, _PanoramaOrigin;
                float _YawRadians, _Magnification, _Active, _PanoramaRadius;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 lensCenterWS:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.lensCenterWS = TransformObjectToWorld(_LensCenterOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                if (_Active < .5) return half4(.65, .90, 1, .12);
                // Sample through the real mesh from each XR eye. The centre follows
                // the held lens; contracting angular offsets provides 4x magnification.
                float3 eye = GetCameraPositionWS();
                float3 centre = normalize(input.lensCenterWS - eye);
                float3 through = normalize(input.positionWS - eye);
                float3 ray = normalize(lerp(centre, through, rcp(max(1, _Magnification))));
                // Intersect the same fixed sphere as the background, so leaning
                // does not make the lens sample a different part of the room.
                float3 offset = eye - _PanoramaOrigin.xyz;
                float b = dot(offset, ray);
                float distance = -b + sqrt(max(0, b * b - dot(offset, offset) + _PanoramaRadius * _PanoramaRadius));
                ray = normalize(offset + ray * distance);
                float s, c; sincos(_YawRadians, s, c);
                ray = float3(c * ray.x - s * ray.z, ray.y, s * ray.x + c * ray.z);
                float2 uv = float2(atan2(ray.x, ray.z) / TWO_PI + .5, asin(clamp(ray.y, -1, 1)) / PI + .5);
                return half4(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).rgb, 1);
            }
            ENDHLSL
        }
    }
}
