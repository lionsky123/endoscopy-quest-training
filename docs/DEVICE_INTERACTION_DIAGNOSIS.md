# 设备双应用与模型交互排查（2026-09-16）

## 后续处理：用户授权删除与 M04 调整

用户明确要求“调整内容，避免出现类似情况；去掉多余的 app”。已执行 `adb uninstall com.DefaultCompany.app`，返回 `Success`；随即查询该包无输出，`pm path com.endoscopy.inspection` 仍返回中文包路径。只卸载这个已确认的默认命名包，没有删除中文应用、安装新版或启动应用。

内容已调整为 M04：合规全景后只纠正一个已有的未关门状态；设备/流向回到原场景的观察、按需实物说明及单向演示。已删除示教盒与待处理台脚本，不再用搬运代替设备分设理解。操作完成和观察记录分别计。

源码 0.8.1 / M04 仍由用户自行编译。构建入口固定校验包名/中文名；每次显式 Android 构建时按当前设置和上一成功回执的最大 code 递增，生成唯一 buildId，写入应用。成功后在 APK 旁写 `.receipt.json` 并更新 `artifacts/latest-quest-build.json`，记录实际 APK 路径和 SHA-256。这些构建钩子没有在本轮被调用；不以编辑器单元检查声称已经产出新包。

核验工具已改为默认查询当前设备包路径和哈希，只有副本与实时哈希一致才复用缓存；缺少设备时明确失败。候选包来自最新成功回执，或用户显式 `-CandidateApk` 指定，不再默认把 `1.apk` 当最新。判断安装一致性使用完整 SHA-256。显式 `-InstalledApk` 属于离线文件比对，报告中 `liveDeviceHashVerified=false`。

本轮后段设备未连接，实时核验按预期失败，没有假称中文应用已更新。卸载结论依据卸载后立即完成的核验。保存的旧包副本仅作历史证据。

定向验证记录：`artifacts/layout-m04-tests.xml`；静态检查通过。测试覆盖关门纠正、无示教盒、观察不计模型操作、导航接续、错误身份拒绝及版本 code 回退后的递增；真实 Quest 抓握仍待正确包安装后验证。

### 版本反复回退的代码根因已修复

验证中发现 `QuestProjectSettings.Apply()` 将版本硬编码为 0.8.0 / code 9；`RenderPipelineConfiguration.Apply()`、准备工程和原生构建入口都会调用它。此前手动修改 ProjectSettings 会再次被覆盖。

新增 `GraphicsRepairDoesNotDowngradeReleaseIdentity` 先复现失败：设置 0.8.2 / code 27 后运行真实渲染修复调用，实际变成 0.8.0。红色结果保存在 `artifacts/version-rollback-red.xml`。修正后配置仅补齐最低版本 0.8.1 / code 10，保留较新版本；显式构建再从当前 code 与上次成功回执中递增。

该回归与固定身份、code 递增、正式场景体验、静音设置一起定向验证。没有为了验证调用任何 Android 构建或打包入口。
以下为处理前的原始排查记录，不代表当前还存在两个应用。

## 已确认的事实

通过 ADB 只读查询安装记录，并读取两个实际安装的 APK。未构建、安装、卸载或启动应用。

| 设备显示名 | 包名 | 设备版本 |
|---|---|---|
| 内镜中心监督检查 | com.endoscopy.inspection | 0.8.0 / code 9 |
| app | com.DefaultCompany.app | 1.0 / code 1 |

这两个包名不同，因此在设备里作为两个独立应用并存。`app` 使用默认命名；本轮未确认它最初由哪次构建产生，不将推测写成结论。用户确认本次体验的是中文应用。

中文已安装 APK 的元数据包含 `BeginQuestion` 与“看一次现场变化”，不包含 `LayoutModelTask`、`SampleDoorHand`、`DoorClosedByHand`。本地 `app/1.apk` 包含这些模型交互模块，不包含 `BeginQuestion`。两者均标 0.8.0 / code 9，但内容不同。这说明设备仍运行面板判断版，新增的模型抓取逻辑没有安装进去。不能用本地编辑器测试通过来宣称设备已可操作。

| 文件 | SHA-256 |
|---|---|
| 设备中文 APK | C9882831AE3525853E2B48BCB6912A7786440D04D126B68BFE6329BC34132FB9 |
| 本地 app/1.apk | 7E0F80D9F7A607D20B8EE0B35BB7C6C5CF18D9D46EDDFA26034646436A779BE6 |

设备系统日期与工作站日期不同，因此本结论使用包内容、符号和哈希比对，不用安装时间与文件时间直接判断先后。

## 可重复检查

`tools/inspect-installed-lesson.ps1` 对下载留档的设备 APK 和本地 APK 检查交互模块并输出哈希，未包含所需模型交互时返回非零。已运行：

```powershell
./tools/inspect-installed-lesson.ps1 -OutputPath artifacts/device-diagnosis/lesson-fingerprints.json
```

结果：`installedContainsRequestedModelInteraction=false`，`candidateContainsRequestedModelInteraction=true`，`identical=false`。这是安装版本差异的确定性反馈，不是对当前头显手部追踪的验证。设备上的旧版本尚未被替换，因此该项目前仍为不通过。

## 本轮源码防混淆修正

源码版本改为 0.8.1 / code 10，入口显示“模型交互原型 M03”与运行时版本号。当前已存在的 APK 未被改写，设备版本也未改变。后续用户自行编译并安装后，才能看到此标识。

## 设计退回重审

用户反馈“关卡设计很奇怪”。当前方案存在两处明确偏差：让使用者先破坏合规关门状态再恢复；用示教盒归位作为设备分设的主要练习，尚不足以体现检查现场关系的教学目标。现有源码保留为可回溯原型，不再称为已获认可的完整关卡，不继续以此扩展其他站。

后续门段应围绕一次真实的未关门情境，操作与反馈对应隔断要求；设备分设和流向必须在原房间实际设备关系上重新设计，不能用搬盒子、依次点击或面板问答来凑完成条件。重设计不改变本轮发现的安装版本差异，也不能替代真实头显抓握验证。


