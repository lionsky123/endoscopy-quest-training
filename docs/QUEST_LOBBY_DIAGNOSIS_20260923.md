# Quest大厅黑底/闪烁诊断（2026-09-23）

> 此处保留旧高斯故障及修复历史。用户随后明确废除高斯并提供主页360°全景，未验收的高斯修复不再作为执行路线；后续采用[全景替换方案](LOBBY_PANORAMA_MIGRATION_20260923.md)。本记录不代表旧实机问题已通过。

## 状态与验收

- [x] 用户报告：Quest仅面板可见，背景黑，开场闪烁，大厅未显示。
- [x] 读取设备安装包身份与SHA-256：code92 / 0.9.1，设备包与app/1.apk一致，SHA-256 E24B35A1BE336E3DD95D49165714F137F531D4B642735222FFCDAF16627A65C2。不是装错本地APK。
- [x] 已保存当前进程日志、应用startup日志和包核对报告，目录artifacts/quest-lobby-20260923。
- [x] 定位MRUK原生初始化被隔离后，BeforeSceneLoad仍创建MRUKGlobalContext；Quest OpenXR存在时UpdateGlobalContext调用未初始化SetBaseSpace导致持续NullReferenceException。
- [x] quest-mruk-global-red：自动回调测试失败；修复为同一历史MR编译符号控制两个回调，并为全局context建立/释放添加ready状态，未建立时不更新；context创建失败不再继续初始化。
- [x] eye-array-red：使用真实大厅和当前渲染命令向双层纹理绘制，左层有内容，右层有效像素0。桌面单眼截图此前未覆盖该缺陷。
- [x] 修复高斯URP逐眼路径：暂停XR单通道，按每眼view/projection与数组slice绘制、同slice合成，再恢复XR状态；eye-array-green 2/2（像素回归+MRUK回调）。
- [x] eye-array-final 3/3通过：不同视点双眼有像素且图像不同、MRUK回调隔离、单一引导入口；两张256×256 GPU输出PNG已查看，均呈现大厅。
- [ ] 用户自行重新编译后，复验左右眼大厅、闪烁、头部运动和性能。code93用户报告仍无大厅；下面新增修复尚未进入设备，不能宣称已修好头显。

## code93第二轮实机证据

- [x] 设备code93 / 0.9.1，与app/1.apk SHA-256一致：9B2134D27DC05B0289040994F2CB3EA2F107400149F37FB9DA9928006E3466C7。证据目录artifacts/quest-lobby-round2。
- [x] 用户自行重开后PID18578，捕获高斯实际RenderGraph通道：xr=True、views=2、Tex2DArray、2688×2816、msaa=4。说明已通过有效资源/缓冲区筛选并进入绘制调度，不等于GPU已有正确像素。新日志没有之前的MRUK.UpdateGlobalContext空引用。
- [x] 实机报告GPU内存约1784MiB及APP_CMD_LOW_MEMORY；记录线索，不把低内存通知直接当作黑屏根因。
- [x] 找出兼容配置遗漏：早期预览工具会克隆管线并设置MSAA=1，正式VirtualRoomEnvironment.LoadLobby此前没有此逻辑，仍为4。上游[渲染集成文档](https://github.com/aras-p/UnityGaussianSplatting/blob/main/docs/render-pipeline-integration.md)标明MSAA不受支持。
- [x] lobby-msaa-red-compiled失败复现正式大厅仍引用原始管线；修复为大厅拥有MSAA=1的管线实例，切房/失败释放时恢复原引用，不修改共享asset。lobby-msaa-green 2/2：正式切房恢复+双眼数组像素。最初lobby-msaa-red为新增测试房间常量写错导致的编译失败，不能当作产品缺陷复现。
- [x] 将[ LobbyGaussian ]诊断前缀（实际无空格）纳入startup.log；开发版第30次绘制仅对每眼中心64×64高斯目标做一次异步读回，记录透明/有色像素与view矩阵。无持续读回，无体验界面调试文字。
- [x] 最终诊断代码编译及真实Play复验：lobby-msaa-diagnostics 3/3通过；quest-msaa-scope真实Play启动/退出PASS（Android目标、Vulkan），大厅与单一开始学习画面已查看。两张双眼数组PNG已查看。这些是宿主验证，不是Quest验收。
- [ ] 用户构建新包后核验msaa=1、持久化GPU初始化/双眼中心像素日志和实际大厅。中心小块读回只定位高斯中间目标，不能替代头显最终合成或完整画面验收。

## 证据边界

设备读取未安装、启动、重启或卸载应用。常规screencap返回0字节，没有有效设备截图，不使用它作为画面证据。设备日志日期与宿主日期不同，按实际PID和APK哈希关联，不用日期推断源码版本。

日志还出现EnvironmentDepth未支持、QR权限未获准和APP_CMD_LOW_MEMORY；GPU相关内存约1.8GB。这些作为后续风险记录，不凭一次低内存事件断定模型资源丢失或把所有闪烁归因于MRUK。

本轮尚未验证Adreno设备着色器执行、XR驱动以及实际双眼结果。双层纹理回归是可在宿主复现的图形缺陷验证，不能替代Quest实机。
