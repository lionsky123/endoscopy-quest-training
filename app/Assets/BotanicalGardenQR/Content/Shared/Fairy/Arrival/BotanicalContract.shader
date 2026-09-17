Shader "BotanicalGardenQR/BotanicalContract"
{
 Properties {
  _MainTex("Contract artwork", 2D) = "black" {}
  _Color("Tint", Color) = (1,1,1,1)
  _Reveal("Angular reveal", Range(0,1)) = 1
  _InnerRadius("Inner band", Float) = 0
  _OuterRadius("Outer band", Float) = 1
  _Phase("Authored time", Float) = 0
 }
 SubShader {
  Tags { "Queue"="Transparent+150" "RenderType"="Transparent" }
  Cull Off ZWrite Off Lighting Off
  Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_instancing
   #include "UnityCG.cginc"
   sampler2D _MainTex; float4 _Color; float _Reveal, _InnerRadius, _OuterRadius, _Phase;
   struct appdata {float4 vertex:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct v2f {float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO};
   v2f vert(appdata v) { v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; return o; }
   half4 frag(v2f i):SV_Target {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float2 p=i.uv-.5; float r=length(p)*2;
    float angle=frac(atan2(p.y,p.x)/6.2831853+.5);
    half3 art=tex2D(_MainTex,i.uv).rgb;
    float ink=max(art.r,max(art.g,art.b));
    float band=smoothstep(_InnerRadius-.015,_InnerRadius+.005,r)*(1-smoothstep(_OuterRadius-.01,_OuterRadius+.01,r));
    float reveal=1-smoothstep(_Reveal-.025,_Reveal,angle);
    reveal *= step(.001,_Reveal);
    float pulse=.84+.16*sin(angle*25.13274-_Phase*2+r*9);
    return half4(art*_Color.rgb*1.2, saturate(ink*2)*band*reveal*_Color.a*pulse);
   }
   ENDCG
  }
 }
}
