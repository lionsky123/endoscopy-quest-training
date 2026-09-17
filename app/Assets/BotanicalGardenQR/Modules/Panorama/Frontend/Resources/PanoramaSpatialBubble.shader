Shader "BotanicalGardenQR/Panorama Spatial Bubble"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.72, 0.92, 1.0, 0.28)
        _RimColor ("Rim Color", Color) = (0.45, 0.88, 1.0, 0.9)
        _HighlightColor ("Highlight Color", Color) = (1.0, 1.0, 1.0, 0.95)
        _Focus ("Focus", Range(0, 1)) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _CullMode ("Cull", Float) = 2
        _LayerOpacity ("Layer Opacity", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _RimColor;
            half4 _HighlightColor;
            half _Focus;
            half _LayerOpacity;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionHCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            half3 normalOS : TEXCOORD2;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings BubbleVert(Attributes input)
        {
            Varyings output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
            output.positionHCS = TransformWorldToHClip(output.positionWS);
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.normalOS = normalize(input.normalOS);
            return output;
        }

        half4 ShadeBubble(Varyings input)
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            half3 normalWS = normalize(input.normalWS);
            half3 viewDirection = SafeNormalize(GetCameraPositionWS() - input.positionWS);
            half facing = abs(dot(normalWS, viewDirection));
            half rim = pow(saturate(1.0h - facing), 2.15h);
            half3 highlightDirection = normalize(half3(-0.5h, 0.72h, -0.48h));
            half highlight = pow(saturate(dot(normalize(input.normalOS), highlightDirection)), 24.0h);
            half focusRim = rim * _Focus;

            half3 color = lerp(_BaseColor.rgb, _RimColor.rgb, saturate(rim * 0.92h));
            color += _HighlightColor.rgb * highlight * 0.7h;
            color += _RimColor.rgb * focusRim * 0.2h;

            half alpha = _BaseColor.a * (0.1h + rim * 0.82h);
            alpha += _HighlightColor.a * highlight * 0.42h;
            alpha += focusRim * 0.2h;
            return half4(saturate(color), saturate(alpha * _LayerOpacity));
        }
        ENDHLSL

        Pass
        {
            Name "SpatialBubble"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull [_CullMode]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex BubbleVert
            #pragma fragment BubbleFrag
            #pragma multi_compile_instancing

            half4 BubbleFrag(Varyings input) : SV_Target
            {
                return ShadeBubble(input);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
