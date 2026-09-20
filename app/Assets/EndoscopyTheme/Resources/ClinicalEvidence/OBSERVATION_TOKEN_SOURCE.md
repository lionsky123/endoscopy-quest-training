# P01 inspection magnifier (replaces the rejected watch reuse)

Source: Magnifying Glass 01 by Nazar Borodavka, Poly Haven.
https://polyhaven.com/a/magnifying_glass_01
License: CC0, https://polyhaven.com/license
Downloaded 2026-09-18 using the public asset API.

InspectionMagnifier.fbx is the original 1K FBX, unchanged (MD5 90231ab2f1462e063b4e994d4b150690).
InspectionMagnifierDiffuse.jpg is the original 1K diffuse texture (MD5 7d1288b4ddb69d748011798c77115dd8).
Download URLs, expected checksums and originals: artifacts/observation-magnifier-source/.

Integration: normalize height to 22 cm (increased from 18 cm on 2026-09-20), center at grip placement, use a proportionally enlarged handle collider,
apply URP frame and transparent lens materials (this FBX imports lens slot 0, frame slot 1).
The diffuse texture has an explicit Texture2D importer and resource binding. No geometry hand drawn or generated.
As of 2026-09-20 the original glass submesh displays live 2x panorama magnification, driven by the actual held mesh and each viewing eye. Observation opens the explanation; releasing the tool no longer advances the lesson. This is angular image magnification of the existing panorama, without a physical refraction simulation.
The opening PocketWatch and its original licence notice are unchanged. P01 no longer references them.
