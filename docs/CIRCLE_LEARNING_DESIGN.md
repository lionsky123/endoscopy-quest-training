> 布局已被 [C06 六关路线](LEVEL_ROUTE_C06.md) 更新：模型/底座隐藏，点位分散；本文件的题库、媒体和 MR 技术说明继续适用，桌面展台与环形点位为历史。

# C05 · MR 展台与光圈问答

2026-09-16 用户新设计覆盖 M04 的动作主线：允许光圈、答题和对话面板；明确要求小精灵带玩家实际走动；选择桌面尺度房间模型。仅清洗消毒室，全程静音，面板仍仅接收真实手部近距离食指触碰。

## 主线

真实环境透视 → 预览展台和六圈路线 → 确认空地 → 跟随 Oppy 实际走到金色光圈 → 正确示范或修复问答 → 跟随到下一圈。

- 原房间模型缩小为最大水平尺寸 0.7 m，放在 0.83 m 高度；隐藏宽大片顶面便于俯视，保留内部设备。模型是空间参照，不用搬模型过关。
- 六个光圈在展台周围半径 0.85 m 的圆上，光圈半径 0.28 m。预览阶段可在玩家当前朝向前重新摆放。需要可容纳约 2.3 m 路线外廓及身体余量的空地；不是已扫描避障的导航系统。
- 小精灵以 0.3 m/s 沿相邻圈之间的路线移动；玩家落后超过 1.25 m 时等候。当前圈亮金色，其余圈显示编号。
- 抵达要求：头显追踪有效、MR 相机子系统运行、精灵已抵达、头部水平位置进入圈中心 0.28 m 内持续 0.6 s。按钮不能代替步行，主线不调用 StationLocomotion.Place。
- 到圈后只显示本站内容。答题时离圈超过 0.5 m 暂藏面板，返回恢复；追踪丢失暂停。无摇杆、WASD、射线、手柄答题入口。
- 换圈按钮只让小精灵开始带路，不移动 XR Origin 或玩家视角。

## 六圈内容

| 圈 | 主题 | 内容 | 修复题数 |
| --- | --- | --- | --- |
| 1 | 正确布局与流程 | 展台、用户全景、原创布局图谱 | 0 |
| 2 | 错乱布局与流程 | 敞门、共用设备、缺位与回流示意 | 3 |
| 3 | 正确工具与水路 | 五类工具、毛刷完整性、供水与滤膜；局部照片 | 0 |
| 4 | 错乱工具与水路 | 缺工具、破损毛刷、水路与维护证据缺失 | 3 |
| 5 | 正确产品与记录 | 产品标签/说明书、附件灭菌证据、测漏照片 | 0 |
| 6 | 错乱产品与记录 | 标签身份、适配关系、灭菌证据、逐次记录 | 4 |

问题一次一道，选择后立即解释。答错只重做当前题；答对显示恢复后的方案或待核实事项，再继续。独立记录“首次答对”和“最终选对”，重复触碰不加分。末页显示 10 道修复题的完成数与首次答对数，不表述为临床能力认证。

当前恢复结果用文字呈现，第一组附正确/错乱图谱；没有宣称已在模型中逐项动画修复。后两组的错误情境明确标为教学案例。

## 全景、图谱与视频

- 全景：用户提供的 PNG，原图完整复制，SHA-256 `D2BB04244B549E480762DB3B4761DB38DC79F25A144E2402D761D6EDF2760C98`。2:1 图像按等距柱状投影显示，尚需头显验证左右接缝和极区观感。
- 全景内原地转头观察，隐藏展台和路线。面前保留近触“返回光圈”，水平移动超过 0.25 m 自动返回 MR。观看期间不能推进路线或得分。
- `layout_correct.png` / `layout_wrong.png` 为按用户镜头一脚本制作的原创教学图谱，源脚本在 `tools/create-circle-atlas.ps1`；不是现场照片或生成的实拍证据。
- 既有工具与测漏照片只描述可见局部；产品图谱只作阅读练习，不推断所有内镜都适用。
- 支持可选图片、全景、静音本地 VideoClip 播放；无素材时不显示视频选项。当前没有装入教学视频，未把网页地址当成本地视频。
- 题库入口 `Resources/circle-course.json`。每项媒体包含 title / kind / path / caption，kind 为 image、panorama 或 video，path 是 Unity Resources 路径。视频必须是审核过的本地素材，并按具体设备型号核对；缺失素材可返回学习。

## MR 技术接入与验证边界

使用 Unity OpenXR Meta 2.5.1、AR Foundation 相机/会话、透明相机背景，启用 Android ARCameraFeature、ARSessionFeature 与 OpenXRCompositionLayersFeature，开启 URP alpha 输出。只需透视，不请求原始摄像头图像。编辑器初始化只修复配置和导入素材，不构建 APK。

针对性检查涵盖题库引用、3/4 题结构、错题重试、重复计分保护、步行抵达门控、当前入口、全景退出与静音设置。编辑器模拟不等于真实 Quest 验收；MR 透视、双手触碰、地面高度、现实路线及全景投影观感须在用户自行构建后现场确认。

## 外部资源候选（未打包）

- [Olympus Cleaning Inservice Videos](https://www.olympusprofed.com/reprocess/reprocessing-gastroenterology/38068/)：官方按型号组织的清洗教学视频索引，可作为后续选片来源，尚未获得适用离线资源并导入。
- [Olympus Training Materials](https://www.infectionprevention.olympus.com/en-us/scientific-evidence/trainings)：官方培训图谱/视频入口。具体型号资料不能直接替代本课的所有设备。
- [Unity Meta Passthrough 文档](https://docs.unity3d.com/Packages/com.unity.xr.meta-openxr@2.5/manual/features/camera.html)：MR 相机接入依据，同时核对了本机已安装的 2.5.1 包源码与文档。

发布状态：源码 C05；未构建 APK、未安装、未启动头显应用。此前已移除多余默认包；设备中的中文应用不因此自动更新。

## Composition Layers 构建校验修复

2026-09-16：原 C05 漏启用 Composition Layers Support，且原生 Build And Run 的配置准备未调用 MR 修复。已在 CircleProjectSetup 补齐该开关，并让 RenderPipelineConfiguration.Apply 在 Unity 构建校验前调用 MR 配置修复。测试直接调用 Unity OpenXR 校验器，先关闭开关复现同一错误，再修复验证；未执行 APK 构建。修复前 artifacts/composition-red.xml 1 项失败；修复后 artifacts/composition-fixed.xml 5 项通过，包含 MR 配置和构建身份回归。Android 配置文件中 m_enabled 已保存为 1。


