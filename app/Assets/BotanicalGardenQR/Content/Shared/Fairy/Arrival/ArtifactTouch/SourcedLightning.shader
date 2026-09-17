//
// Spektr/Lightning - lightning bolt effect
//
// Copyright (C) 2015 Keijiro Takahashi
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//
Shader "BotanicalGardenQR/Fairy/SourcedLightning"
{
 Properties { _Color("Color",Color)=(1,1,1,1) }
 SubShader {
 Tags { "Queue"="Transparent+90" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
 Pass { Blend SrcAlpha One ZWrite Off Cull Off
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #include "UnityCG.cginc"
 #include "SimplexNoise2D.cginc"
 float _ArrivalTime;
        float3 _Point0;
        float3 _Point1;
        float _Distance;

        float3 _Axis0;
        float3 _Axis1;
        float3 _Axis2;

        float _Throttle;
        float2 _Interval;   // min, max
        float2 _Length;     // min, max

        float2 _NoiseAmplitude;
        float2 _NoiseFrequency;
        float2 _NoiseMotion;

        fixed4 _Color;
        float _Seed;

        // pseudo random number generator
        float nrand01(float seed, float salt)
        {
            float2 uv = float2(seed, salt);
            return frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453);
        }

        float nrand11(float seed, float salt)
        {
            return nrand01(seed, salt) * 2 - 1;
        }

        // displacement function
        float displace(float p, float t, float offs)
        {
            float2 np1 = float2(_NoiseFrequency.x * p + offs, t * _NoiseMotion.x);
            float2 np2 = float2(_NoiseFrequency.y * p + offs, t * _NoiseMotion.y);
            return snoise(np1) * _NoiseAmplitude.x + snoise(np2) * _NoiseAmplitude.y;
        }

        // vertex intensity function
        float intensity(float seed)
        {
            return (nrand01(seed, 4) < _Throttle) * nrand01(seed, 5) - 0.01;
        }

        void Displace(inout appdata_full v)
        {
            float pp01 = v.vertex.x; // position on the line segment [0-1]
            float seed = (v.vertex.y + _Seed) * 131.1; // random seed

            // interval (length of cycle)
            float interval = lerp(_Interval.x, _Interval.y, nrand01(seed, 0));

            float t = _ArrivalTime;          // absolute time
            float tpi = t / interval;
            float tp01 = frac(tpi);     // time parameter [0-1]
            float cycle = floor(tpi);   // cycle count

            // modify the random seed with the cycle count
            seed += fmod(cycle, 9973) * 3.174;

            // modify pp01 with the bolt length parameter
            float bolt_len = lerp(_Length.x, _Length.y, nrand01(seed, 1));
            pp01 = lerp(tp01, pp01, bolt_len);

            // start point, end point
            float3 p0 = _Point0 + _Axis0 * nrand11(seed, 2) * _NoiseAmplitude.x;
            float3 p1 = _Point1 + _Axis0 * nrand11(seed, 3) * _NoiseAmplitude.x;

            // get displacement on the current vertex
            float d0 = displace(pp01 * _Distance, t, seed *  13.45);
            float d1 = displace(pp01 * _Distance, t, seed * -21.73);

            // vertex position
            v.vertex.xyz = lerp(p0, p1, pp01) + _Axis1 * d0 + _Axis2 * d1;

            v.vertex.xyz += _Axis1 * v.texcoord.y * .012;
            // vertex color (indensity)
            v.color = _Color * intensity(seed);
        }


 struct vary { float4 pos:SV_POSITION; float4 color:COLOR; UNITY_VERTEX_OUTPUT_STEREO };
 vary vert(appdata_full v) { vary o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_OUTPUT(vary,o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); Displace(v); o.pos=UnityObjectToClipPos(v.vertex); o.color=v.color; return o; }
 fixed4 frag(vary i):SV_Target { UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i); clip(i.color.r); return fixed4(i.color.rgb,_Color.a); }
 ENDCG
 } }
}
