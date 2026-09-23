# Endoscopy stationary lobby integration

Status: superseded by the user's 2026-09-23 panorama decision. This package is retained for archived source/assets only. Production lobby uses LobbyPanorama.prefab; the production renderer no longer contains GaussianSplatURPFeature, and VirtualRoomEnvironment no longer overrides MSAA. Notes below describe historical work, not the active product.

2026-09-21. Base commit remains 2c6fed37da67a217367261fcfcd3316d34c73e76; MIT headers/license retained.

Local changes to Runtime/GaussianSplatRenderer.cs, Runtime/GaussianSplatURPFeature.cs,
Shaders/SplatUtilities.compute and Shaders/GaussianComposite.shader:

- Explicit per-view camera/projection/viewport inputs for compute (instead of centre-eye camera globals).
- URP render graph suspends XR single-pass state, renders each eye into its own array slice with matching depth, composites that slice, then restores XR state.
- Array-aware composite variant and zero-alpha divide guard.
- Mono path remains supported and was rendered in the production lobby editor preview.

XR device rendering, transparent ordering and GPU performance still require Quest validation. Lobby MSAA changes are scoped to a cloned runtime pipeline and restored when the room is disposed. This is not an upstream compatibility or performance guarantee.

2026-09-23: code92 device report required renewed validation. The current source before this fix had only view0 rendering and no explicit array-slice targets (the earlier description above was not valid evidence for it). A real lobby array-target test found slice1 empty. RenderEye now targets/composites the requested slice; the URP pass stops/starts XR single-pass around per-view draws. Android one-time logs report resources, shader support, GPU setup and render-target/XR state. Desktop Vulkan array regression passed; Quest repair is still awaiting a user-built update.

2026-09-23, code93 follow-up: Quest reaches the stereo pass but still shows no lobby and reports MSAA=4. The earlier scope claim was only present in documentation/preview, not the production room. VirtualRoomEnvironment now clones the active URP asset, sets MSAA=1 for the lobby, and restores the prior pipeline references on disposal. A production room transition regression covers the scope. Development Android players additionally sample each eye's central 64x64 intermediate splat pixels once, on render 30, using asynchronous readback; logs are persisted by VisitorInstaller. This evidence does not establish final headset output. Quest verification remains pending a user build.
