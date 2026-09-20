# C09 Quest 边界重新定位与固定房间

2026-09-20 最新启动覆盖：首次可靠头部采样将头手共同 trackingSpace 的水平位置和偏航对齐到固定入口、面朝第一站；仅一次，保留真实高度/俯仰/侧倾，不移动房间。启动校准纳入下方原点补偿基准，后续转头、走动和返回房间不再次回中。见 [路线与开场修订](VR_ROOM_PATH_BINDING_C09.md)。

## 2026-09-18 原点时序修订（当前实现）

用户明确现象为整个房间、模型及界面一起偏移或转向。本轮连接设备只读核验：`com.endoscopy.inspection` 为 0.9.1 / code 70，设备 APK SHA-256 为 `e9f52ab1f970aafbd35535895116b54a50a0365b2e601c58756489fca24799e7`，与本地 `app/1.apk` 相同。读取该 APK 的 IL2CPP 元数据，存在 `ClinicalWorldSurface`，不存在 `VirtualRoomTrackingOrigin`；设备尚未包含下方旧补偿或本次修订。现存日志未提供本次漂移时刻的原点事件，不能将源码模拟复现称为设备根因复现。

旧补偿在收到 pending 事件时立即移动 `trackingSpace`。新增回归 `PendingNotificationCannotMoveTheViewBeforeNewSamplesArrive` 先只发通知、不改变头部采样，旧代码导致视角偏移 0.85316 m。Android 目标 EditMode 红色报告为 `artifacts/vr-interaction/origin-timing-red.xml`。

当前修订：

- 事件只登记转换关系及 `ChangeTime`，不立即修改跟踪空间。订阅 `OVRCameraRig.UpdatedAnchors`，读取 Meta 渲染位姿采样时间，按该时间所属参考系重算补偿；覆盖普通 Update 与渲染前更新跨越边界的情况。不用墙钟、Unity 时间或猜测延迟帧数。
- 同一事件去重；重复渲染采样从初始位姿重算，不重复累乘。多个原点变化按生效时间组合。真实走动、转头仍由头部局部采样控制，房间、六站及既有面板不重定位。
- Meta Core 205 的公开事件没有时间参数。窄适配器仅在同步事件回调期间读取 `OVRManager.eventDataBuffer` 中保留的时间；核对 SDK 结构字段偏移，并用本地 `link.xml` 保留 IL2CPP 所需成员。不修改 PackageCache，也不自行消费 SDK 原生事件。更换 SDK 后必须重跑布局契约测试；适配失败进入恢复提示，不能回退到即时补偿。
- 第一次有效头部采样之前不开始开场流程；首次参考系没有旧位姿属于初始化。建立对齐后，如果事件缺少有效关系、时间或存在冲突，则停止手部交互及带路，显示“请先停止走动，退出并重新打开应用”。不从用户头部移动猜测原物理位置，不自动回中、不传送或重置课程。
- 普通追踪暂时丢失显示等待提示，恢复有效采样后继续。原点通知等待生效期间暂停输入，避免使用中间坐标放置新面板。恢复提示是仅在定位异常时出现的头部相对提示，正常教学界面继续固定世界姿态。
- MRUK、透视、扫描和空间权限保持关闭。当前不提供物理锚点持续纠偏，不能承诺修复没有有效原点事件关系的所有 SLAM 漂移。

验证覆盖：通知先于采样、边界前后采样、六次连续变化、重复事件、头手一致、无效关系/时间、追踪丢失及恢复、首次初始化、手部暂停和恢复提示、房间及六点固定、导航和组装生命周期。最终报告为 `artifacts/vr-interaction/origin-final.xml`，52/52 通过，19.61 秒；最初新增的失败用例在本组中已通过。设备基线记录为 `artifacts/vr-interaction/origin-fix-device-baseline.json`。只运行 Android 目标 EditMode；没有构建、安装或启动设备应用，实际 Quest 边界切换时序仍需用户编译后验证。

## 下方为此前即时补偿记录

用户确认：触发的是 Quest 系统边界/返回应用/重新定位提示，点击后房间相对现实的朝向发生变化。场景已经使用 Stage 且关闭 AllowRecenter；这些设置不能单独处理系统重新定义 Stage 原点。

## 定位与修复

原有测试只检查房间 Unity Transform 不动。系统更换跟踪参考系后，即使房间 Transform 没动，新头部/手部局部坐标仍会改变，造成房间在用户看来转向或偏移。

新增 `VirtualRoomTrackingOrigin` 由房间生命周期创建和释放。监听本地 Meta SDK 的 `OVRManager.TrackingOriginChangePending`；只处理当前跟踪类型、有限且有效的“新原点在旧空间中的位姿”。将该变换累乘到头部和双手共用的 `OVRCameraRig.trackingSpace`，补偿随后采样坐标的改变。房间、六站、路线和已展开面板不改坐标。不根据焦点返回重新摆放房间，不冻结头部，不添加传送或移动输入。

忽略其他参考系通知、空位姿及非法数值，Dispose 后退订。系统没有给出有效旧新空间关系时，不能凭头部姿态猜出原物理位置；该情况不保证恢复。SDK 公共通知为 pending 且未暴露原生 ChangeTime，本地事件回放验证变换结果，实际边界切换时序仍需设备验证。

## 证据

修复前：`tools/test_vr_interaction.ps1 -Run tracking-origin-red -TestFilter ClinicalTrackingOriginTests` 两项均失败；模拟静止用户遇到 Stage 重定义，世界头部位置偏移约 0.85 m。红色报告 `artifacts/vr-interaction/tracking-origin-red.xml`。

修复后的针对性用例覆盖两个转向、连续六次原点变化、真实后续走动/转头、头手同坐标、房间/站点不动、无效/其他参考系通知和退订。仅 Android 目标编辑器验证；未构建或部署 APK，不称为头显验收通过。

绿色报告：`artifacts/first-observation-and-origin/tests.xml` 中 4 项 ClinicalTrackingOriginTests 全部通过（所在针对性组 37/37 通过）。
