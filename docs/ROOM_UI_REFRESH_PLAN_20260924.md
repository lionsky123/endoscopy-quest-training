# 房间界面与加载过渡独立实施计划

状态：2026-09-24 本地代码、Android 目标编译/局部回归和正式 Editor Play 已推进；Quest 黑帧与双眼/手部效果仍待实机核验。本计划由用户明确要求独立编写，专题任务以本页复选框跟踪；[原合并计划](STARTUP_FLOW_AUDIT_PLAN_20260923.md)的 C01.E06/E07、M5.E05/E06 和相关内容缺口仍保持其原状态，不因本页创建而自动勾选。依据：[专题规格](ROOM_UI_REFRESH_SPEC_20260924.md)、[完整规格](FULL_SCRIPT_SPEC_20260920.md)、[内容矩阵](FULL_SCRIPT_CONTENT_MATRIX.md)、[项目工作流](WORKFLOW.md)。

## 交付顺序与依赖

| 切片 | 可观察交付 | 前置 |
| --- | --- | --- |
| UI-01 | 页面/点击路径基线与 Quest 黑屏复现信息 | 无 |
| UI-02 | 统一视觉角色在欢迎及房间入口样板中可见 | UI-01 的本地页面基线 |
| UI-03 | 过渡画面在真实加载阶段持续存在 | UI-01 的本地转场审计、UI-02 的视觉角色；Quest 根因/最终画面另验 |
| UI-04 | 办公室入口、电脑和纸质资料直达闭环 | UI-02 |
| UI-05 | 储存库、候诊区任务入口及观察面板 | UI-04 的共用规则 |
| UI-06 | 消化、呼吸、洗消室任务入口与专属媒体/对象面板 | UI-05 |
| UI-07 | 欢迎、画廊、提示、判断、最终汇总一致性收尾 | UI-04–UI-06 |
| UI-08 | Android 局部回归、当前真实 Play 画面和 Quest 交接 | UI-03、UI-07 |

每项只覆盖一个可观察行为，修改约 1–5 个主要文件；若某房间内容需要更大改动，在该项下先拆成“入口→内容→返回”小步并分别验证。Quest 诊断结果未取得时，不阻塞 UI-02 及其他可在本机完成的界面工作；UI-03 可先实现可验证的本机改造，但不能勾选为实机无黑帧。每完成 2–3 项检查一次真实消费者、输入状态和 Android 编译，不重复运行同一源码状态已通过的检查。

## UI-01 基线与问题定位

- [x] 清点欢迎、大厅画廊、办公室（现场/电脑/纸面/汇总）、储存、候诊、消化、呼吸、洗消的当前可操作目标。分别记录房间入口、资料行/字段、判断选项和导航按钮，写下代表任务从房间入口到内容再返回所需的实际近触次数。
  - 依据：专题规格第 3–4 节，现有 [宿主 Play 画面](../artifacts/production-play/ui-final-source-20260924-1.txt) 与当前运行对象；宿主图只作旧版基线。
  - 主要文件：`FullScriptThemeSelection.cs`、`FullScriptStationaryContent.cs`、`FullScriptOfficeRecords.cs`；结果写入本计划 UI-01 证据段或 `artifacts/inspection-editor/` 报告。
  - 验收：形成逐页旧控件数、重叠面板、常见路径及最显著冗余清单；标明旧画面日期和源码状态。
  - 验证：只读控件审计及必要的现有正式 Play 路线；不把概念图计作证据。

### UI-01 源码基线（改动前，2026-09-24）

下表计的是源码定义的**最少近触次数**与同时可见的导航目标，不代表 Quest 实测手部成功率。旧 Play 证据见上方引用；本次未以概念图代替运行画面。

| 页面/任务 | 入口至内容 | 内容页导航目标 | 主要冗余 |
| --- | --- | --- | --- |
| 欢迎、大厅画廊 | 欢迎 1 次开始；画廊 1 次触图选房，另有抓握拖动 | 画廊有 7 张房图、拖动把手和带练开关；随访问状态禁用部分房图 | 深绿标题、绿把手和白卡片材质割裂；触图与拖动的意图需要更清楚 |
| 办公室现场/电脑/纸面 | 主题页选电脑或纸面各 1 次直达；现场 1 次 | 资料页可同时出现 6 个资料标签、放大、返回记录、返回房间，共 8–9 个；电脑主页另有 2 个底部离开按钮 | 一张资料页有两个不同返回层级；白底超大页与房间入口不统一 |
| 储存库 | 选择柜体、柜侧登记或胃镜各 1 次 | 任务页可同时出现细项翻页、提示、资料入口、选择内容、下一检查点及离房动作 | 登记表已有独立入口，任务页又重复“打开登记表” |
| 候诊区 | 选座椅侧/通道侧各 1 次 | 观察页有切侧、返回检查；任务页仍有选择内容、下一点、切房等 | 从观察回到说明再选方向会重复进入入口 |
| 消化/呼吸诊疗室 | 主题 → 具体检查点通常 2 次 | 任务页可出现细项前后、安小卫提示、选择内容、下一点、切房 | 主题与任务二层选择；长说明板遮住对象 |
| 清洗消毒室 | 主题 → 具体检查点通常 2 次 | 任务页再叠加视频、放大、对照图、对象等按任务出现的动作 | 分类入口与当前对象动作分离，说明板按钮密度偏高 |
| 办公室最终汇总 | 到最终办公室自动展示 | 逐项、逐字段、提交、回查房间、回看电脑共 5 个 | 复盘与提交同屏竞争注意力 |

源码消费者：`FullScriptRoomVisit.Show`、`FullScriptRoomGallery.ShowRoomGallery`、`FullScriptThemeSelection.ShowRoomThemeSelector`、`FullScriptStationaryContent.ShowScriptTask`、`FullScriptOfficeRecords.BuildPage`；同一时刻只保留一块主业务面板，但大面板会遮挡房间对象。Quest 黑屏是用户报告：所有房间加载期短暂出现并自行消失；未取得当时 APK 身份、逐帧录像或帧提交日志，尚不能将平台层与同步创建定为同一个根因。
- [ ] 定位 Quest 短黑屏发生在用户选房后的哪个加载阶段，并区分 Unity 画布遮幕、资源释放/同步创建造成的停帧与平台级加载画面。
  - 主要文件：`FullScriptJourneyRuntime.cs`、`ClinicalRoomAssetRelease.cs`、`VirtualRoomEnvironment.cs`、`EndoscopyThemeSetup.cs`；如需临时诊断，信息只入日志或编辑器工具。
  - 验收：记录当前安装包身份、黑屏起止与目标房间、是否每房一致，以及实际帧提交/加载状态；无 Quest 证据时明确“用户报告，根因未定位”，仍可推进不依赖实机的代码审计。
  - 验证：用户构建的当前 APK 和 Quest 画面/日志由用户提供或在另行明确授权后检查；不擅自构建、安装、启动应用。

## UI-02 共用视觉角色与首个真实样板

- [ ] 将已确认的烟黑承托、浅紫灰可触面、钴紫选中态、珊瑚确认细节和实底阅读面实现为可复用的颜色与状态角色，并在欢迎/房间入口的真实 UI 中形成样板。
  - 依赖：UI-01 的旧版页面清点。
  - 主要文件：`ClinicalPanelStyle.cs`、`VisitorCoachThemeAsset.cs`、`VisitorCoachTheme.asset`、`DialogueContractGraphic.cs`、`FullScriptVisualStyleTests.cs`；优先复用现有圆角/边缘图形，不默认引入新包或昂贵全屏模糊。
  - 验收：默认、接近、按下、已接受、选中、禁用状态视觉一致；主动作和次要动作有明确层次；欢迎页仍仅一个“开始学习”。长短中文不被裁切，概念图不是渲染验收。
  - 验证：当前 Android 目标导入/编译与视觉角色局部 EditMode；正式 Play 截图检查欢迎与房间入口，Quest 双眼对比/眩光留最终验收。

## UI-03 Quest 非黑色加载与失败恢复

- [ ] 根据 UI-01 证据修复切房加载期间的黑帧，确保轻量目标预览、状态和非黑背景在旧房释放、资源等待、目标创建与追踪等待期间持续提交画面；同房检查点使用较轻过渡。
  - 依赖：UI-01 根因记录；视觉色彩取 UI-02。确认为同步阻塞时，先调整加载调度/帧间工作，不将颜色替换当作完成。
  - 主要文件：`FullScriptJourneyRuntime.cs`、`ClinicalRoomAssetRelease.cs`、`VirtualRoomEnvironment.cs` 与相应 `FullScriptRuntimeTests.cs`；具体拆分以根因为准。
  - 验收：真实加载期没有应用可控制的纯黑帧、黑底进度条或假百分比；失败显示可执行重试/恢复；目标未就绪或追踪未恢复时不揭幕、不记访问成功。旧房停止媒体/输入、解绑销毁、资源释放完成后才创建目标房间。
  - 验证：针对性慢加载、释放、追踪等待、失败与重复切房 EditMode；当前 Android 编译；正式 Play 观察遮幕；Quest 双眼录像逐房确认是否仍黑，未实测保持未勾选。

## UI-04 办公室直达样板

- [ ] 把办公室首页、电脑记录和纸质资料做成同一完整路径：一触业务入口直接进入对应载体，阅读中只显示当前资料所需控件，返回保留选择与查阅状态。
  - 依赖：UI-02 的共用视觉与输入状态。
  - 主要文件：`FullScriptThemeSelection.cs`、`FullScriptStationaryContent.cs`、`FullScriptOfficeRecords.cs`、`StationaryScriptTests.cs`；文案及资料数据只在必要时改动。
  - 验收：电脑仍以电脑资料界面、纸张仍以原表/图片结构呈现；打开资料后无同义“再打开”动作。单页无翻页键，返回只保留一个明确路径；六字段与资料来源、缺项、判断状态不丢失。办公室入口的主动作与背景材质符合概念方向。
  - 验证：比对 UI-01 的按压次数和同时可见控件；办公室局部 EditMode、Android 编译、当前正式 Play 入口/电脑/文件截图；Quest 可读性最后核验。

## UI-05 储存库与候诊区

- [ ] 储存库把柜体、柜侧登记与镜体入口组织成可直接进入的少量选择；候诊区把两侧观察与座椅/隔断提示压缩为当前观察所需操作。
  - 依赖：UI-04 的入口/返回规则；不能把已建内容改为更多点击的分类树。
  - 主要文件：`FullScriptStationaryContent.cs`、`FullScriptStorageRecords.cs`、`FullScriptStorageRegisterMount.cs`、`StationaryScriptTests.cs`。
  - 验收：柜侧纸表仍挂在现有对象并可读/放大，原表数据与任务状态不变；候诊区能直达两侧观察，面板不遮隔断或路线；返回只回到有意义的上一步，不强制重复引导。
  - 验证：当前 Android 编译和两房局部测试；必要正式 Play 画面核对坐/站视野、遮挡及近触距离；Quest 实测留最终验收。

## UI-06 诊疗室与洗消室

- [ ] 消化、呼吸两房分别保留独立证据，把用途设备、报告历史、台面废物按当前对象呈现；洗消室把空间、水路、视频、防护、产品与附件按载体直达，隐藏当前阶段无关的重复按钮。
  - 依赖：UI-05 的共用房间任务布局。
  - 主要文件：`FullScriptThemeSelection.cs`、`FullScriptStationaryContent.cs`、`FullScriptSinkVideo.cs`、`StationaryScriptTests.cs`；若需更多文件，按诊疗室/洗消室各自闭环再拆。
  - 验收：GI/RESP 不能共用完成状态；洗消六任务及 RE-03 视频的播放、暂停、重看、返回和声音生命周期仍可用；缺件仍明示，不以隐藏入口冒充完成。典型任务比基线更少重复按压，检查对象保持可见。
  - 验证：独立房间的路由/资料/视频局部 EditMode、当前 Android 编译、对应正式 Play 画面；头显音画与手触由 Quest 验收。

## UI-07 全局收尾

- [ ] 复核欢迎、房间画廊、安小卫提示、判断反馈、声音设置和最终办公室汇总，使颜色角色、动作反馈、返回位置与空/失败状态一致；不把各载体统一成一种卡片。
  - 依赖：UI-04–UI-06 的房间路径。
  - 主要文件：`FullScriptRoomGallery.cs`、`VisitorCoachPresenter.cs`、`FullScriptLearningPanel.cs`、`FullScriptOfficeRecords.cs` 及必要局部测试；分页面小步实施。
  - 验收：欢迎仍单一开始入口；画廊仍整图近触、捏住短把手拖动后需新戳按；判断/提示不抢资料面板；汇总保留跳过、未答、未可用和统一提交后只读。二级操作按需出现，所有错误说明给出下一步。
  - 验证：更新后控件清点、代表路径近触次数与 Android 局部回归；当前真实 Play 画面逐类检查，不以这次概念预览充数。

## UI-08 集成验证与交接

- [x] 对当前源码完成 Android 目标实际导入/编译、相关 EditMode、必要静态检查，以及因用户报告黑屏而针对切房路径执行的正式 Play；分别记录编译、静态、EditMode、真实 Play、渲染与 Quest 的不同结论。结果见下方本地实施证据；Quest 未运行。
- [x] 提供每类真实页面和逐房切换的改前/当前控件/近触路径表，并记录主要视觉资源来源→正式消费者→交互/证据→画面。概念预览只列设计参考；实际房间缩略图替换及模型外观仍留在原合并计划，不将局部 UI 当作完整业务交付。
- [ ] 由用户自行构建 APK 并在 Quest 验收坐姿/站姿、左右手近触、双眼文字、透明层眩光、各房间加载无黑帧及实际性能；未拿到实机结果不得勾选本项或宣称整体验收。

适用的验证命令从仓库根目录运行，先确认没有其他 Unity 实例占用工程；每次使用新的运行名，已有同源码/配置结果不重复跑。此处列命令供**实施阶段**使用，本次文档交付不启动 Unity、不构建 APK：

```powershell
$taskRun = 'room-ui-style-' + (Get-Date -Format 'yyyyMMddHHmmss')
powershell -NoProfile -File tools/test_inspection_editor.ps1 -Run $taskRun -TestFilter 'BotanicalGardenQR.Tests.EditMode.FullScriptVisualStyleTests'

$taskRun = 'room-ui-route-' + (Get-Date -Format 'yyyyMMddHHmmss')
powershell -NoProfile -File tools/test_inspection_editor.ps1 -Run $taskRun -TestFilter 'BotanicalGardenQR.Tests.EditMode.StationaryScriptTests'

$taskRun = 'room-ui-transition-' + (Get-Date -Format 'yyyyMMddHHmmss')
powershell -NoProfile -File tools/test_inspection_editor.ps1 -Run $taskRun -TestFilter 'BotanicalGardenQR.Tests.EditMode.FullScriptRuntimeTests'

$taskRun = 'room-ui-play-' + (Get-Date -Format 'yyyyMMddHHmmss')
powershell -NoProfile -File tools/test_production_play.ps1 -Run $taskRun -Vulkan -RouteScreens

git diff --check
```

以上 Unity 脚本均显式使用 `-buildTarget Android`，其 EditMode 导入/编译不生成 APK。局部过滤器在实施时对当前测试命名再核对；不存在的测试或旧断言须按实际行为修正，不能把历史绿灯当新版视觉通过。

## 证据与完成规则

每个切片完成时在对应复选框下记录：源码状态、测试/画面路径、通过与失败数量、Quest 是否实际验证、剩余问题。部分完成、未验证或受阻的切片保持未勾选。UI-08 全部子项和专题规格第 6 节条件满足前，不将本计划标记完成；完整业务内容仍由原内容矩阵与合并计划验收。

## 2026-09-24 本地实施与剩余验收

- [x] 共用视觉代码：烟黑控制承托层、浅紫灰选择面、钴紫当前态、珊瑚确认线和不透明浅色资料面已用于欢迎主题、画廊、房间选择/任务、电脑资料、判断与汇总。`FullScriptVisualStyleTests` [3/3](../artifacts/inspection-editor/room-ui-style-20260924-1920.xml)；最终正式 Play 的欢迎、画廊、办公室及逐房入口见 [运行记录](../artifacts/production-play/room-ui-play-20260924-2330.txt)。概念图仍只作设计参考。
- [x] 本地操作路径：办公室现场/电脑/纸面各从房间入口直接到对应内容；普通资料页只留一个上下文返回，专注阅读隐藏选择侧栏；储存库柜侧登记直达实体纸表；消化、呼吸和洗消入口直接打开各类别首项；画廊增加近触“上一张/下一张”、整图选房，抓握滑动改为可选，演示按需打开。画廊只显示当前房和紧邻房卡，远处卡不接收近触；正式场景左右手组件各有不同真实手源 [1/1](../artifacts/inspection-editor/room-ui-hand-source-20260924-2345.xml)。当前完整房间路由 [54/54](../artifacts/inspection-editor/room-ui-route-20260924-2315.xml)；中间一次失败源于旧测试直接戳不可见远处房卡，现已让测试先用近触切图再选房，针对路径 [3/3](../artifacts/inspection-editor/room-ui-hidden-card-20260924-2300.xml)。
- [x] 本地加载改造：大房间资源改为 `Resources.LoadAsync` 请求并按帧分段校验和创建网格/材质，保持旧房停止、解绑销毁、资源释放完成后才创建目标；非黑色遮幕及失败恢复沿用同一状态机。分帧完成与校验失败清理 [2/2](../artifacts/inspection-editor/room-ui-stream-20260924-1737.xml)，现有房间材质/几何 [7/7](../artifacts/inspection-editor/room-ui-surface-20260924-1744.xml)。这是降低同步停帧的源码措施，尚未证明 Quest 黑帧根因或最终画面。
- [x] Android 目标与本机画面：相关 EditMode 均以 `-buildTarget Android` 运行；静态 `tools/check_vr_static.ps1` 通过 20 个源码程序集及路线检查；最终 [正式 Play 路线](../artifacts/production-play/room-ui-play-20260924-2330.txt) PASS，捕获 26 张当前场景运行图，包括办公室任务/电脑/普通资料/专注阅读、各房入口和最终汇总。环境为 Unity Editor Play、Android 目标、Vulkan、XR display inactive；它核对实际运行对象和画面，不代签 Quest 光学、手追踪或加载帧提交。本次未构建 APK。
- [ ] Quest 双眼与左右手近触/抓握验收，逐房录像确认是否仍有黑帧，并区分平台层与应用停帧；记录当前安装包身份、黑帧时间、房间和追踪状态。用户自行构建 APK 后执行；没有该证据时 UI-01 的黑帧定位、UI-03 和 UI-08 仍不勾选。
- [ ] 所有资料和房间任务的坐/站可读性、触达及晚段任务最短路径复核。当前正式 Play 可确认页面存在与相对布局，不能证明 Quest 双眼字级和真实手触；洗消类别后两项仍应重点核对是否因逐项前进增加操作。

### 改前/当前操作对照（源码路径，非 Quest 触摸成功率）

| 代表路径/页面 | 改前 | 当前 | 结论与界限 |
| --- | --- | --- | --- |
| 欢迎进入 | 1 个“开始学习” | 1 个“开始学习” | 入口数量不变，主题和反馈换为共用视觉角色 |
| 大厅推荐房间 | 触图 1 次；浏览依赖抓握短把手 | 触图 1 次；可触左右箭头再触图，抓握保留可选 | 浏览增加两个显式箭头目标，换取无需学会抓握也能浏览；Quest 实际可达待验 |
| 办公室入口到电脑/纸面 | 电子记录常见路径 3 次选择；纸面有重复入口 | 各 1 次进入对应载体 | 资料仍在原电脑数据和纸表/文件结构中 |
| 办公室资料页 | 6 个资料直达、放大、两个返回，最多 9 个按钮 | 普通页 6 个资料直达、放大、一个上下文返回，共 8 个；专注阅读仅 1 个返回 | 阅读时资料与控件分区，焦点页无侧栏；当前 Play 已核截图 |
| 储存柜侧登记 | 入口后还经过任务说明/打开动作 | 入口后直接近触实体柜侧纸表 | 少一个无业务意义的中转，登记字段和任务状态保留 |
| 候诊区两侧观察 | 任一方向 1 次进入；返回说明后再选方向 | 任一方向 1 次进入；当前页直接切侧，提示按需打开 | 当前入口 2 个观察方向、提示、选房共 4 个目标；未减少首次按压，避免增加层级 |
| 消化诊疗室类别首项 | 主题→具体任务通常 2 次 | 类别→首项 1 次 | 用途、历史、台面三类在本房独立，后续任务可在内容页前进 |
| 呼吸诊疗室类别首项 | 主题→具体任务通常 2 次 | 类别→首项 1 次 | 与消化室相同的入口简化，但任务记录分别归属各房 |
| 清洗消毒室类别首项 | 主题→具体任务通常 2 次 | 类别→首项 1 次 | 空间/设备/水路与防护/产品/附件各直达首项；RE-03 视频仍按任务出现 |
| 最终办公室汇总 | 提交前 4 个、提交后最多 5 个导航/提交目标；正文含大量操作计数 | 提交前 4 个、提交后 4 个；保留完成/未答/跳过/缺件/提示/讲解/待修正 | 两次提交确认保留，提交后原按钮位置换成只读复盘 |

### 主要视觉资源与正式消费者

| 来源 | 当前消费者与操作 | 当前真实 Play 画面 / 未完界限 |
| --- | --- | --- |
| 用户确认的[概念方向](full-script-visuals/ASSET_CATALOG.md)，落地为`ClinicalPanelStyle`和`VisitorCoachTheme.asset`的颜色/表面角色 | 欢迎、房间选择、任务、资料、判断和汇总的近触按钮；无概念图纹理进入正式界面 | [欢迎](../artifacts/production-play/room-ui-play-20260924-2330-01-lobby-welcome.png)、[办公室任务](../artifacts/production-play/room-ui-play-20260924-2330-04a-office-task-panel.png)；人物肖像仍受原洋红源图影响 |
| 图册登记的`room-preview-atlas-v1.png`及可选`gallery-gesture-tutorial-v2.png` | `FullScriptRoomGallery`按UV显示房间图卡；触图选房，上一张/下一张近触切图，手势图只在演示中出现 | [大厅画廊](../artifacts/production-play/room-ui-play-20260924-2330-02-lobby-gallery.png)；缩略图仍是类别插画，非实际房间运行截图，原计划M5.E04保留校准任务 |
| 图册登记的办公室模拟记录图，源为`office-training-records.json`的程序排版；现有原表图片仍按原字段关系使用 | `FullScriptOfficeRecords`在电脑/纸面资料页消费；字段/行选择与任务状态由同源数据驱动 | [办公室资料](../artifacts/production-play/room-ui-play-20260924-2330-06-office-document-image.png)、[专注阅读](../artifacts/production-play/room-ui-play-20260924-2330-06a-office-document-reading.png)；Quest双眼字级待验 |
| 现有各房Prefab/清单，模型源和贴图修正归原计划与另一对话 | `FullScriptRoomCatalog`→`VirtualRoomEnvironment`仅加载目标房；当前房任务入口面板就地近触 | [储存](../artifacts/production-play/room-ui-play-20260924-2330-09-r02-storage-first-choice.png)、[候诊](../artifacts/production-play/room-ui-play-20260924-2330-12-r03-waiting-first-choice.png)、[消化](../artifacts/production-play/room-ui-play-20260924-2330-15-r04-gi-first-choice.png)、[呼吸](../artifacts/production-play/room-ui-play-20260924-2330-18-r04-resp-first-choice.png)、[洗消](../artifacts/production-play/room-ui-play-20260924-2330-21-r05-reprocessing-first-choice.png)；本专题不签收模型最终材质 |

安小卫欢迎肖像当前仍引用原有洋红剪影源图；按[原合并计划](STARTUP_FLOW_AUDIT_PLAN_20260923.md)已交另一模型/贴图对话处理，本专题未修改模型、材质、绑定或肖像资源。正式 Play 截图中的这一现象不能解释为本次颜色角色的最终人物效果。
