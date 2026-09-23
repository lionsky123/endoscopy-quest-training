# 主页大厅360°全景替换

用户最新决定：废除主页大厅高斯模型，以提供的全景图作为正式环境；其他房间不改变。

源文件：`C:/Users/i1204/Downloads/mudj41fg.png`。
项目副本：`app/Assets/EndoscopyTheme/Panoramas/Lobby360.png`。
SHA-256：`154DDF087AE829D68044F960853721D987C2BFBB39596419513FDEEC6B60FA6F`。
来源为用户提供的图像，未新增生图或修改图像像素；不认定为医院实拍或医疗证据。

正式消费者：FullScriptRoomCatalog → VirtualRoomEnvironment.LoadLobby → FullScriptRooms/Stationary/LobbyPanorama.prefab → Lobby360.mat → 源图。
复用现有支持XR实例化的等距柱状全景着色器；方向由同一初始观察位配置驱动，无每帧回中、无移动要求。InspectionWorkspace使用同一正式房间创建路径。

高斯预制体移至非Resources目录Assets/EndoscopyTheme/ArchivedLobbyGaussian；原始高斯资产/源码/许可保留历史用途，正式URP Renderer已移除GaussianSplatURPFeature。大厅不再克隆管线、不再覆盖MSAA、不再分配高斯GPU缓冲区。旧专项测试保存在docs/history/tests-before-lobby-panorama，由全景接入与切房释放断言替代。

原图7680×3840，像素完整保留；纹理Android最大4096、ASTC 6×6，U重复/V夹紧，mipmap开启，不保留CPU可读副本；随当前房间加载与释放。全景显示球壳无碰撞器，明确不作为实物障碍传入向导路径检查。

第一轮回归发现球壳被障碍边界检查视为实体墙，初始化失败；已修正背景障碍定义。lobby-panorama-migration-2：StationaryScriptTests全部35/35通过，覆盖大厅资源、缺件重试、完整原地流程与Unity预览隔离/配置同步。

真实Play：artifacts/production-play/lobby-panorama.txt PASS，Android目标、Vulkan；activePanorama=True、briefReady=True、legacyOpening=False。对应PNG已查看：原图中央入口为初始正向，欢迎页单一开始学习按钮，全景非倒置、非旧高斯。未完成Quest双眼、接缝近看、实际手部及性能验证。

验证与未完成验收统一勾选在[合并计划](STARTUP_FLOW_AUDIT_PLAN_20260923.md)。未构建或部署APK，Quest结果不能由桌面Play替代。
