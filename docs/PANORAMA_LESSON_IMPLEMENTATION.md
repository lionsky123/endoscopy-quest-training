# 全景图片核查与离站修复实现记录

2026-09-17，用户确认推荐方案并回复“开始执行”。当前工程 `D:/quest3/EndoscopyBuild/app` 已落地本次改版；源码仍使用现有 C09/Quest Android 配置。没有构建、安装或启动 APK，设备上旧版本不代表本次源码。

## 已实现的体验

第一站继续使用用户的 7680×3840 全景。门、设备分设、工位流程三个主题各有一个与全景坐标匹配的观察入口；用户实际转头寻找，凝视确认后展开方法特写。特写通过透视方向采样原全景，未使用拉伸 uvRect 充当局部照片。方法顺序直接显示在图内。

每个主题随后切换到新的无答案情境图，按“选图中依据 → 选判断 → 显式确认”作答。设备和流程题要求两个依据同时选对，少选、多选、判断错误都不能完成。提示逐级增加，可重看方法；提示或尝试次数不会自动计分。正确后逐项显示依据轮廓及解析；末题解析内包含三项完成总结。

主图按后续手部操作反馈调整为约 0.702×0.395 m，初始阅读距离 0.62 m，约占 59° 水平视角。0.38 秒从对应观察方向展开，展开后固定在世界空间；前倾、选中、提示、答错与重看方法不重算位置。确认解析后收回对应全景方向，再进入下一观察主题。阅读时轻微降低背景亮度；不移动或旋转相机和 XR Origin。

观察任务、方法、题面、提示、解析共享一个主界面。退出按钮并入观察卡和阅读标题栏。继续使用参考工程的头向准星与 0.65 秒停留，确认后需移开再对准；按用户最新反馈，图片教学阅读面板同时接入真实 Meta 手部近触，手指靠近可交互表面时暂停凝视，撤离后重新开始凝视。没有新增鼠标、键盘或手柄提交入口。暂停或隐藏清除旧凝视进度，重新进入从第一题开始。

后续“第二题答错变灰”修复保留了错误提交时的依据与判断，用户可以只修改错误项后重新提交；区域使用常显轮廓及“未选／已选”标记，判断、主操作和禁用状态具有独立视觉反馈。修复证据及最新验证见[答错重试与手部交互修复](PICTURE_RETRY_HAND_INPUT_C09.md)。下列 78/78、33/33 和九张预览为该修复之前的教学改版验证记录。

三题完成后，用户确认“完成本站学习”，关闭全景恢复房间，通过现有内容完成事实进入离站提示；之后主动确认出发，小精灵按既有路线带往第二站。第一站不再打开原三道文字题。中途退出只允许重新学习，旧跳过/文字答题入口不能提前计作完成。后五站教学内容与题数维持原状（3/0/3/0/4），六站总计仍为 13 个核查任务。

## 两个公共故障的证据与修复

**完成后不能出发：** 真实 Startup 关闭决策在旧答题面板退出后重新变为可见，导致 Guidance 继续暂停。原先 ClearJourneyPrompt 在没有 journey prompt 时直接返回，没有消费关闭决策。现在完成成功显式消费本次关闭决策；下一次内容打开才重置，普通状态刷新不再让旧面板复活。原有导航仍要求主动确认出发，不因计分直接换站。

**教程堆叠：** 旧 Modal 策略没有在学习内容打开时抑制独立 Coach 对话，允许全景主界面与退出教程并存。现在打开学习内容时抑制独立 Coach，第一站提示和退出入口由当前教学界面负责。

先补真实 UI 失败复现再修复：`artifacts/vr-interaction/lesson-red.xml` 的 3 项全部失败，覆盖六站完成/跳过分支及打开内容时的 Coach 抑制。修复后公共链路 `lesson-flow-fixed.xml` 为 25/25 通过。

第一站完成接入 `GlobalFrontendShell → JourneyCloseDecisionBinding`，携带原内容会话；只接受刚关闭、当前站的第一站会话。过期、未关闭、错误站点和重复通知不推进。成功关闭前不发完成事件；关闭失败会返回学习入口供重试。旧第一站文字题与正误图数据保留供历史追溯，生产新流程不引用它们作答。

## 本轮验证

- Android 目标隔离 Unity EditMode：`artifacts/vr-interaction/evidence-final.xml`，**78/78 通过**，13.63 秒。包括第一站全流程真实共享凝视、原始全景投影、完成/失败/重复会话、真实 Startup 与六站 Guidance、面板生命周期、凝视控制器、全部地图测试和静音。
- 最终标签选择范围和内嵌退出入口调整后：`artifacts/vr-interaction/evidence-final-ui.xml`，**33/33 通过**，7.64 秒。包含三题在 0°/90° 两种进入朝向下的真实凝视操作、答错/重看/暂停/退出重进、中文文字高度、图片与按钮状态、六站出发以及 Startup/Modal 回归。
- `tools/check_vr_static.ps1`：15 组 C# 源码静态编译及真实地图控制器路线检查通过，不启动 Unity 构建。
- `tools/check_vr_assets.py`：不透明 VR 相机、关闭 MR 服务与权限、Android profile、唯一 Visitor 场景、静音、房间资产哈希及 GUID 检查通过。
- 实际 Unity 编辑器渲染：`tools/render_clinical_evidence.ps1` 输出 `artifacts/clinical-evidence` 下九张方法/题面/解析图。检查中文、图片比例、全景特写方位与控件排版。截图由编辑器产生，不能称为 Quest 实机截图。

测试过程中还修正了 Unity Rect 的 JSON 字段差异，改用明确的归一化坐标字段；共享凝视控制器会缓存按钮，因此图片依据区域采用预先注册的固定池，换题更新位置与状态，避免动态创建的新目标无法被凝视识别。

## 实现入口与素材

- `app/Assets/BotanicalGardenQR/Modules/Panorama/Contracts/ClinicalEvidenceLesson.cs`：题目结构与学习阶段、选择/判分/提示/完成语义。
- `app/Assets/BotanicalGardenQR/Modules/Panorama/Frontend/ClinicalEvidenceControls.cs`：全景定位、稳定大图、三题交互、展开收回和分层标注。
- `app/Assets/BotanicalGardenQR/Modules/Panorama/Frontend/Resources/ClinicalEvidencePerspective.shader`：从全景方向采样透视特写。
- `app/Assets/EndoscopyTheme/Resources/ClinicalEvidence/lesson.json`：三个观察点、依据区域、判断、提示与解释；同目录 door/equipment/flow.png 为三张新图。
- [素材与提示词](CLINICAL_EVIDENCE_ASSETS.md)；[有效 spec](PANORAMA_LESSON_REDESIGN.md)；[访谈决策](TEACHING_REDESIGN_REVIEW.md)。

## 仍需头显验收的部分

本轮代码、资产和编辑器级验证已完成；实际 Quest 的文字清晰度、近距离视觉舒适度、转头寻找、展开收回动画的体感及精灵带往第二站，需用户编译新版后验证。未检查本次 APK 哈希或设备安装结果。编辑器预览背景和镜头不等同于双眼头显视野；不将静态截图当作动画或真实手部验收。
