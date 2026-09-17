# physical_point_001 资产来源

- 当前测试表演直接引用巨人柱内容现有的 `giant_saguaro_bat.prefab` 与 `giant_saguaro_bat.controller`，以独立实例在 Point Pose
  播放；不会复用面板模型实例或 Model Controller。2026-08-30 按用户批准以本机输入 `bat_cactus.blend` 原位替换该模型，
  输入 SHA-256 为 `8FED13F24B352B73EAD2E02F402FC3C686DBF3C123B97FACC52660A1A3653279`，首版生产 FBX SHA-256 为
  `2E1DDA34AD7F46097BBA2067EF6E7A6E889416672EC979883F574CF39502948F`。2026-09-01 用户进一步批准以本机输入
  `bat_cactus(1).fbx` 原位替换；输入与当前生产 FBX SHA-256 均为
  `FD9B6D60FC618F3DBC64EE2773DBF14ABA9BF242640D4B037E22E8E30C9FF265`，Unity 导入为 `40,403` 顶点、`58,826`
  三角面，并继续保留 365 帧完整动作。这是用户确认的临时复用关系；准备好正式现场模型后，只替换 Point 的表演 Prefab，
  不改变运行时 Interface。用户仍需确认输入模型用于商业展示、修改和分发的权利范围。
- 花朵、Cue、花粉和仙人掌粗轮廓 Proxy 由项目 Editor Publisher 使用 Unity 基础网格生成，为本项目自有内容。
- 开花音效复制自项目已登记的 Spatial Lingo MIT 子集
  `Experience/Bootstrap/Art/GamePresentation/ReferenceAssets/SpatialLingo/Audio/SFX_TreeGrowing_Stage01.wav`；MIT 许可文本随本闭包复制。
- 虚拟可见材质使用已安装 Meta XR Core SDK 201 的 `EnvironmentDepth/OcclusionLit` 或
  `EnvironmentDepth/URP/OcclusionUnlit` Shader；SDK 文件不复制进本目录，适用 Oculus SDK License。

当前 Animator 表演使用本目录的 Environment Depth 材质覆盖，并以米制世界空间适配比例 `0.08` 播放；动画首帧实际 Renderer
包围盒约为 `0.89 m × 1.08 m × 1.02 m`，且落在 Anchor 原点附近。面板模型配置 `17.5` 位于约 `0.001` 的 Canvas 世界缩放
链下，不能按数值比例直接复用于米制锚点实例。
2026-08-30 对源动画正面/侧面按 15 帧粗采样、再对 176–296 帧按 5 帧细采样，确认源动作包含飞行接近、花冠采蜜与后续飞行动作。
用户最终要求 Model 与现实位置都保留完整动作，因此 Unity 的非循环 `Scene` Clip 导入 1–365 帧、24 fps、约 `15.17s`，
现实表演保守 fallback 为 `15.5s`。两条路径共享 Clip 但各自实例化、播放和释放，并都可从第 1 帧重播。
2026-09-01 起，闭包内既有 `PhysicalPoint001Bloom.wav` 通过 Performance root 的通用 OwnedMedia 组合接入现实表演；源文件为
`44.1 kHz / 24-bit / stereo / 3s`，Unity 导入时转为 mono，并由启用 spatializer 的 3D AudioSource 在锚点位置播放。重播、完成、
Lost、Stop 与实例释放共用 Performance Lease 生命周期，不建立 PointId 或植物名称特化代码。
该测试资产不代表已通过 Quest 多视角遮挡、比例、30 mm/5° 对齐或 72 FPS 验收。
