# C09 房间过亮与反面漏面修复

2026-09-18 用户报告房间过亮、存在穿模，进一步明确：墙或地面从某些角度看是透明的。本轮处理表面渲染和近距离裁剪，不改变固定房间坐标、六站路线或真实步行方式。

## 复现

执行 `tools/test_vr_interaction.ps1 -Run room-surface-red -TestFilter ClinicalRoomSurfaceTests`，三项检查均失败（1.86 秒测试执行时间，不含 Unity 启动）。生产房间加载路径存在以下状态：

- 带贴图的材质原始色系数约 0.588，加载贴图后被覆盖为 1.0；原始导出色值未得到保留。
- 材质 `_Cull = 2`，剔除背面；`_Surface = 0`、`_ZWrite = 1`，不是材质透明度被调低。
- XR 摄像机的较大近裁剪距离未被房间初始化修正，复现输入 0.3 m 后仍为 0.3 m。

执行 `tools/render_clinical_evidence.ps1 -Run room-surface-before -Room`，使用发布的 Visitor 场景灯光和生产房间加载器；从原楼板网格提取单个原始三角面，保留其材质、UV 和顶点法线。正面画面覆盖率 0.1134，反面 0.0000，图像检查失败并以退出码 2 返回。洋红色是这项检查人为设置的背景，代表表面未遮挡背景，不是材质着色器丢失。

首次完整房间的第一张即时渲染出现贴图尚未稳定的现象，后续截图恢复正常。预览工具增加一次丢弃的渲染读回以等待 GPU 贴图上传；第一张旧预览不作为贴图绑定错误或精确亮度对照证据。

## 已落实的修复与边界

- 保留原导出材质色值与贴图的相乘关系，不修改原贴图像素，不整体降低教程/手部/UI亮度。
- 房间专用材质改为双面、不透明、写入深度，避免原墙/楼板从反面观察时消失。无需复制或重新摆放房间几何。
- 房间初始化时将 XR 摄像机近裁剪距离限制为最多 3 cm；已经更小的值不增大。避免继承的 10–30 cm 裁剪距离挖掉近处墙面。
- 房间仍固定，碰撞与实际步行逻辑不变；双面显示不阻止真实头部越过虚拟墙，也不修改实体设备几何。
- 全程静音，只进行 Android 目标编辑器测试与渲染，不构建、安装或启动 APK。

## 最终验证

- `tools/render_clinical_evidence.ps1 -Run room-surface-verified -Room -TestFilter 'ClinicalRoomSurfaceTests;ClinicalWorldPlacementTests;SilentVisitorAudioTests;ClinicalSixStopCourseAdvancesWithoutAtlasOrCollection'` 正常退出。Android 目标 EditMode **11/11 通过，8.59 秒**。包含材质色值、双面不透明深度、三种初始近裁剪距离、房间与六点固定、全景返回、面板朝向、静音和六站完成衔接。
- 原楼板三角面的正反面覆盖率均为 **0.1134**；修复前反面为 **0.0000**。图片对照直接复现并验证了反面漏面。
- 同一轮输出六站各一张实际修复状态与一张“仅将材质恢复成旧纯白系数”的对照，共 12 张房间图；另有两张原楼板正反面图。已查看修复后的 P01、P05 与楼板反面。
- `tools/check_room_render.py artifacts/room-surface-verified` 读取实际 PNG 像素后通过：六个站位的平均显示亮度均下降，接近纯白的像素比例没有增加，正反面覆盖面积一致。当前平均显示亮度范围约 0.422–0.488，旧系数对照约 0.688–0.804。这是编辑器截图数据，不是头显亮度计测量。
- `tools/check_vr_static.ps1`：18 个 C# 单元及固定六站路线通过；只有已有的 TMP 换行接口弃用警告。`tools/check_vr_assets.py`：VR 相机、静音、Android 设置、房间几何摘要、资源与 GUID 检查通过。
- 记录目录：`artifacts/room-surface-verified/`。测试报告为 `tests.xml`，渲染参数为 `render-check.txt`，图像数值为 `pixel-check.json`。
- 本轮未构建、安装或启动 APK，尚无本次 Quest 佩戴验证。既有 53 项回归是上一轮记录，本轮针对性验证为上述 11 项。

代码修改位于 `VirtualRoomEnvironment.cs`（保留贴图色系数、近裁剪上限）及房间独立 `RoomSurface.mat`（双面）。没有改动灯光亮度、原贴图、原网格、房间位置或导航路径。
