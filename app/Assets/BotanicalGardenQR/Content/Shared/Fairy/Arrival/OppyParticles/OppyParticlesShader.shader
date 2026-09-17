// Copyright (c) Meta Platforms, Inc. and affiliates.

Shader "TheWorldBeyond/OppyParticlesShader"
{
    Properties
    {
        _MainTex("Base (RGB) Trans (A)", 2D) = "white" {}
        [HideInInspector] _PortalStencilRef("Portal Stencil Ref", Float) = 1
        [HideInInspector] _PortalStencilReadMask("Portal Stencil Read Mask", Float) = 255
        [HideInInspector] [Enum(UnityEngine.Rendering.CompareFunction)] _PortalStencilComp("Portal Stencil Comp", Float) = 8
        _Cutoff("Alpha cutoff", Range(0,1)) = 0.5
        _Color("Color", Color) = (0,0,0,0)
        [HideInInspector] _LuminanceAsAlpha("Use artwork luminance as alpha", Float) = 0
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent" "Queue" = "Transparent"
        }

        LOD 100
        ZWrite Off
        Lighting Off

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            Stencil { Ref [_PortalStencilRef] ReadMask [_PortalStencilReadMask] Comp [_PortalStencilComp] Pass Keep }
            CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma target 2.0

#include "UnityCG.cginc"

            struct appdata_t {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f {
                float4 vertex : SV_POSITION;
                float2 texcoord : TEXCOORD0;
                float4 vertexColor : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed _Cutoff;
            uniform half4 _Color;
            uniform half _LuminanceAsAlpha;

            v2f vert(appdata_t v) {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.vertexColor = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target {
                half4 artwork = tex2D(_MainTex, i.texcoord);
                artwork = lerp(artwork, half4(1, 1, 1, artwork.r), _LuminanceAsAlpha);
                half4 Color = artwork * i.vertexColor * _Color;
                //	half4 finalCol = (i.vertexColor.rgb, particleTexture.a * i.vertexColor.a);
                //	clip(col.a - _Cutoff);
                return Color;
            }
            ENDCG
        }
    }
}
