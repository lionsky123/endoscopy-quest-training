// SPDX-License-Identifier: MIT
Shader "Hidden/Gaussian Splatting/Composite"
{
    SubShader
    {
        Pass
        {
            ZWrite Off
            ZTest Always
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile _ GS_TEXTURE_ARRAY
#pragma require compute
#pragma use_dxc
#include "UnityCG.cginc"

struct v2f
{
    float4 vertex : SV_POSITION;
};

v2f vert (uint vtxID : SV_VertexID)
{
    v2f o;
    float2 quadPos = float2(vtxID&1, (vtxID>>1)&1) * 4.0 - 1.0;
	o.vertex = float4(quadPos, 1, 1);
    return o;
}

#if GS_TEXTURE_ARRAY
Texture2DArray _GaussianSplatRT;
int _GaussianEye;
#else
Texture2D _GaussianSplatRT;
#endif

half4 frag (v2f i) : SV_Target
{
    #if GS_TEXTURE_ARRAY
    half4 col = _GaussianSplatRT.Load(int4(i.vertex.xy, _GaussianEye, 0));
    #else
    half4 col = _GaussianSplatRT.Load(int3(i.vertex.xy, 0));
    #endif
    return float4(GammaToLinearSpace(col.rgb/max(col.a, 0.00001)),col.a);
}
ENDCG
        }
    }
}
