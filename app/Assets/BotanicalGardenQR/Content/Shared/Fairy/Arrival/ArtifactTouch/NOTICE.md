# Artifact touch sources

- PocketWatch.fbx and Watch*.png: Poly Haven Vintage Pocket Watch by Tal Swicegood, https://polyhaven.com/a/vintage_pocket_watch , CC0 https://polyhaven.com/license . Original 2K model/textures unchanged; material channel packing and Unity import settings adapted.
- SourcedLightning.shader / SimplexNoise2D.cginc: https://github.com/keijiro/SpektrLightning , MIT, copyright Keijiro Takahashi 2015 (full notice preserved in shader). Original noise/displacement calculation retained; surface shader changed to stereo-aware URP transparent pass; line topology widened to ribbons for readability without bloom. Not importing its editor/runtime scripts or image effects.
- SourcedSpeedLines.shader: https://github.com/MirzaBeig/Anime-Speed-Lines , Unlicense. Original polar noise/mask calculation retained; scene-color compositing replaced by transparent stereo overlay, explicit arrival clock, no camera renderer feature required.

Downloaded 2026-09-11. No AI-generated/hand-painted model or texture. Integration and temporal composition are project code. Original repositories/downloads remain in the external research directory. This effect does not sample or fracture actual Quest passthrough pixels.
