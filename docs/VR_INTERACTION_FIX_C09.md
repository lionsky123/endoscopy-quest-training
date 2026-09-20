# C09 开场触碰与精灵归线诊断

后续交互已按用户最新要求改为参考工程的“普通面板凝视、开场与空间对象手部”分工；最新面板后移复现、修复和验证见 [参考交互安排](REFERENCE_INTERACTION_C09.md)。下文手触流程及 35/35 记录保留为此前诊断历史，不作为新交互的设备验收。

2026-09-17。用户报告开场对话结束后靠近精灵不带路；开场面板第一次可以触碰，退出重进后手模型正常但触碰不响应。

## 复现与结论

1. 运行 `tools/test_vr_interaction.ps1 -Run baseline -TestFilter AuthoredFirstLegMovesActualFairyToFirstPoint`：真实精灵驱动两项失败，30 秒模拟步行后仍 `Paused`、进度 0。进一步使用 VR 固定 frame，在正向、90°、180°开场及无对话情况测试，侧向/反向开场不能归线。头部样本有效、对话暂停已解除，故该失败不是 MR 扫描或“识别人”分支。
2. 仅扩大原入口距离后，四种场景都通过第一段；之后将精灵归线距离独立为 `fairyJoinRadius=1.5`，保留 `departureRadius=0.55` 前向窗口与原到站条件。修复在生产 `FairyMapMotionSink` 及 `MapNavigationController` 生效。精灵以 0.55 m/s 转身步行归线，玩家实际跟随，不传送。
3. 运行 `tools/test_vr_interaction.ps1 -Run touch-baseline -TestFilter VisitorDialoguePointableLifecycleTests`：停用后漏收 Cancel，第二次新触碰应累计 2 次，实际仍为 1 次。`VisitorDialoguePointableTarget` 的按压锁跨生命周期残留；清除停用/启用/失焦/暂停边界状态后通过各 20 次循环，持续 Select 不重复提交。该测试确认代码中同类失效路径，不能单凭它断言用户在头显上完整杀进程重启的全部原因已被复现。
4. 对已在运行的 Quest 进程只读日志：`MetaXRAudioRoomAcousticProperties.Update` 持续抛出 `DllNotFoundException: MetaXRAudioUnity`。安装 SDK 的 `CheckSceneHasRoom` 在场景没有声学组件时创建 Temporary Room，立即调用 Update；异常阻断清理并留下每帧更新组件。Visitor 现在包含一个 active GameObject 上的 disabled 声学组件，让 SDK 扫描命中但不执行声学更新。没有恢复任何音频；没有把该异常直接认定为触碰失效的原因。

## 验证与范围

- `tools/test_vr_interaction.ps1 -Run verified`：35/35，测试主体 5.34 秒；使用隔离 Unity 缓存，启动参数明确为 `-buildTarget Android -runTests -testPlatform EditMode`。首次缓存导入耗时另计。
- 包含真实精灵 prefab/driver/生产 adapter 的六站完整路线、不同开场朝向、对话返回、限速、跟踪暂停；面板 disable/focus/pause 20 次循环；SDK PokeInteractor 手指扫过与鼠标/提交拒绝；SDK 原生音频初始化不被调用。
- `tools/check_vr_static.ps1`、`tools/check_vr_assets.py` 均通过；房间 692 个路线采样和最小 0.52 m 模型包围盒间距检查保持通过。
- `artifacts/vr-interaction` 下 baseline、fixed-frame-baseline、touch-baseline、entry-radius-check、join-and-touch、verified 的 XML/日志保留本机诊断过程。没有遗留临时调试日志或交互替代入口。
- 编辑器验证不等于 Quest 操作验证。设备只读版本为 0.9.1 / code 23，源码修订与 APK 哈希未核对，本次没有构建、安装或启动应用。

用户自行编译后需在头显确认：开场点按 → 退出/重新进入再次点按 → 完成对话后靠近精灵 → 超出距离等候并靠近继续 → 学完一站主动触碰出发。保持全程静音、真实手部近触、实际步行。

## 后续面板距离调整

用户反馈触发后的面板太远。开场/到站/出发、邀请、答题从 0.55 m 改为 0.45 m，图片继续面板从 0.60 m 改为 0.45 m；学习入口沿用的 ViewerFront 展示配置由 1.65 m 改为 0.45 m。同步相关默认值，保留现有字号、尺寸和垂直偏移；放置机制仍为触发时定位，随后固定，不改变玩家位置。

本次 12 组源码静态类型检查、六段导航控制器检查、五处序列化距离核对和 VR 资产检查通过。`panel-distance` EditMode 尝试在执行测试前因隔离缓存缺少 Bee 编译响应文件（CS2011）退出，没有产生本次测试通过记录。上方 35/35 是距离调整之前的结果，不作为本次编辑器验证。没有构建或部署 APK。
