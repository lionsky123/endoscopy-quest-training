# Unity SmokePortal adaptation

Source: https://github.com/Unity-Technologies/VisualEffectGraph-Samples/tree/bcd800405c1b019654a0af3b4b8bd68fec590b4b/Assets/Samples/SmokePortal

Copyright (c) 2018 Unity Technologies ApS. Licensed under the Unity Companion License for Unity-dependent projects. Original notice: LICENSE-Unity.md. Full terms: https://unity.com/legal/licenses/unity-companion-license

Source asset: `6way_Textures/Portal_B3-64_A.tga`, 2048x2048, 8x8 frames. `PortalFlow.png` preserves the original RGBA pixels in PNG storage; RGB contains directional smoke lighting data, not painted rainbow albedo. Runtime shades these channels with a restrained green/blue palette.

`PortalBoundary.png` is derived from the original alpha: threshold, close small holes, retain the connected silhouette, fill its enclosed interior, bake signed distance. It supplies identical clipping to the window and skinned body. No silhouette was painted or replaced with synthetic sine-wave geometry. Both derivatives remain under the same Unity Companion terms.

Conversion entry: `Tools/Import-OpeningPortalAtlas.py --reference <external-reference-clone>`. Runtime does not depend on the reference folder. The HDRP graphs, environment and GPU particle simulation are not imported. This is a mobile-oriented texture adaptation, not a claim that the full HDRP six-way-lit sample runs on Quest.
