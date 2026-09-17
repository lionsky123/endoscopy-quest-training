// Unity SmokePortal's authored simulation frames, adapted to the shared window/body clipping path.
// Copyright (c) 2018 Unity Technologies ApS. Unity Companion License; see SmokePortal/LICENSE-Unity.md.
sampler2D _BoundaryAtlas;
sampler2D _FlowAtlas;

float2 ArrivalLocalUV(float2 p, float opening)
{
    return p / float2(lerp(.018, 1.22, opening), lerp(.12, 1.22, saturate(opening * 2))) * .5 + .5;
}

float2 ArrivalFrameUV(float2 uv, float frame)
{
    // Frames in the author-provided 8x8 atlas proceed left-to-right, top-to-bottom.
    float2 cell = float2(fmod(frame, 8), 7 - floor(frame / 8));
    return (cell + clamp(uv, .002, .998)) / 8;
}

float ArrivalBoundary(float2 p, float opening, float seconds)
{
    float2 uv = ArrivalLocalUV(p, opening);
    float frame = fmod(max(0, seconds) * 12, 64);
    float a = tex2Dlod(_BoundaryAtlas, float4(ArrivalFrameUV(uv, floor(frame)), 0, 0)).r;
    float b = tex2Dlod(_BoundaryAtlas, float4(ArrivalFrameUV(uv, fmod(floor(frame) + 1, 64)), 0, 0)).r;
    float bounds = min(min(uv.x, uv.y), min(1-uv.x, 1-uv.y));
    return min(lerp(a, b, frac(frame)) - .5, bounds);
}

float4 ArrivalFlow(float2 p, float opening, float seconds)
{
    float2 uv = ArrivalLocalUV(p, opening);
    float frame = fmod(max(0, seconds) * 12, 64);
    return lerp(tex2D(_FlowAtlas, ArrivalFrameUV(uv, floor(frame))),
        tex2D(_FlowAtlas, ArrivalFrameUV(uv, fmod(floor(frame)+1,64))), frac(frame));
}
