# 2026-09-23启动修复实施记录

完整范围仍为[合并计划](STARTUP_FLOW_AUDIT_PLAN_20260923.md)。本记录不是完整项目交付声明。

## 当前规范

根目录AGENTS.md与app/AGENTS.md已改为当前有效规则，旧全文归档并核对哈希。体验者文案与开发信息分离，包含生成图内嵌文字；欢迎页不再显示资源接入进度和重启策略。保留模拟资料身份及真实不可用提示。

图像规则已明确：生图面向项目实际展示的界面、记录或教学素材并接入运行消费者；不生成场景渲染图冒充实现画面。按审计移除5张无消费者旧图（大厅/办公室场景参考、过期电脑记录屏、章节布局概念图、PPE拒绝稿），保留来源哈希与替代关系见[图像清理记录](full-script-visuals/experience-20260923/IMAGE_CLEANUP_20260923.md)。用户剧本原图、全景、在用屏幕和运行截图均未删。

## 已取得的启动证据

- `tools/test_production_play.ps1 -Run baseline-real-play`：从正式场景进入真实Play，原生退出码-1073740791（0xc0000409）。
- 同脚本`-Run baseline-empty-play -EmptyScene`：空场景同样崩溃。故障不依赖大厅、模型或起点。
- 两次日志末尾均为MRUK原生context创建失败后继续调用tracking-space setter。将MRUK嵌入包的BeforeSplashScreen自动初始化限定为显式历史MR编译符号，当前VR主线不加载此服务；Meta核心/真实手部保留。
- `mruk-isolated-empty`：空场景真实Play进入/退出，code 0。
- `mruk-isolated-production`：正式场景进程不再崩溃，但D3D11报告高斯wave功能不可用；早期探针的PASS仅证明进程与对象，不能当作视觉通过。随后已将日志图形错误加入失败条件。
- `mruk-isolated-vulkan`：真实Play进出code 0，无上述着色器错误。
- `direct-brief-vulkan`：正式入口通过，briefReady=True、legacyOpening=False；已查看实际运行截图，真实大厅高斯与模式选择可见。该图拍摄时仍带旧开发文案，已按用户后续纠偏修改，不作为最终文案截图。
- `current-rules-default-api`：未传强制图形API参数，配置后普通启动实际使用Vulkan；真实Play进入/退出code 0，briefReady=True、legacyOpening=False。已查看同名PNG，欢迎页仅保留欢迎、模式区别和轻触操作提示。此结果仍为编辑器证据，不是Quest验收。

## 已实施但仍需扩大验收

- 新原地装配不再创建隐藏魔法书/旧序章Presenter，也不模拟开书、手部就绪与对话完成；旧状态机只在显式历史模式创建。
- 新回归`ProductionEntryDoesNotInstantiateOrCompleteTheArchivedPrologue`修前0/1，实际发现旧状态机；修后与全原地路线一起2/2通过（`independent-startup-green`）。
- 首次定位后再对齐环境，真实追踪等待不再超时揭幕。延迟追踪/坐站姿等需继续补充分支验证。
- 编辑器预览平台独立于Quest真实追踪，使用同一业务runtime，不靠旧序章进入新流程；仅编辑器编译。
- 新`InspectionWorkspace`使用生产房间加载器，当前房间/观察点选择、Scene位置标记和调试流程可视；小型`inspection-views`配置供编辑/运行共同读取，不引用所有模型。
- `workspace-compile-and-entry`编译与两项入口/全路线测试2/2通过。工作区菜单、自动刷新、拖动编辑和多房实际画面仍需实际验收，不能由这两项测试推断完成。
- `InspectionWorkspace.Prepare`执行code 0，创建共享观察配置并将宿主编辑器图形API设置为Vulkan；活动目标仍为Android，没有Player构建。配置发布后仍需新的Play检查。

## 尚未完成

新旧序列化资源依赖的最终清理、独立手部恢复提示、共享配置变更回归、Unity工作区完整可视验证、首房故障恢复、连续Play三次、所有页面/对话/图片的开发文案审查，以及C01–C11完整内容与Quest实测均不能签收。

OF-02旧报告的19/20结果保留为旧独立模式的历史证据；其复盘断言已改为读取实际条数，不再重复修改固定旧常数。13项实际内容与C10新稿仍需另行完整核对。未构建、安装或启动设备应用。

当前重新核对的归档独立模式复盘专项`of02-review-current-1`为1/1通过：测试从实际会话读取复盘条目数，完成提交后循环全部复盘页，检查未答事实、边界回绕、原面板位置及任务明细无溢出。该结果只解除归档模式的复盘数量回归疑点，不覆盖正式原地流程，不等于C10完整教学、Quest画面或正式依据已完成；旧`19/20`报告保留为历史证据，C10继续未勾选。

## 全程引导与原地体验修订
2026-09-23：正式入口移除独立核查，保留一个开始学习按钮；运行时拒绝原地模式切换到独立核查。根/app AGENTS、规格第7节/D05与合并计划同步。候诊引导明确原地转头、按钮切换观察侧。历史独立会话代码保留为兼容，不作为正式入口。
验证：guided-only-stationary 编辑器回归2/2通过（单一引导入口、头手不移动的完整房间路线）；同名真实Play通过，Vulkan、高斯激活、简报可交互且无旧开场，PNG已查看。未构建或部署APK，非Quest验收；其余内容包仍在合并计划中待完成。

## 连续启动与追踪恢复续作
startup-three-1/2/3：同一源码连续三次正式Play进出，全部exit 0，Vulkan、高斯激活、入口可交互、无旧序章；第三次PNG已查看。M1其余失败恢复要求仍未签收。
检查点追踪丢失：tracking-focus-red 4项中3项失败，均为追踪未恢复30秒后错误调用完成回调；首次大厅等待用例通过。移除FocusOut/FocusMove/FocusIn的超时冒完成，FocusIn追踪丢失恢复全黑。tracking-focus-green 7/7通过，覆盖三阶段、恢复仅一次、不移动头手，以及原地全路线/房间提交/资源释放相邻回归。仅编辑器可控追踪验证，不代表Quest验收。

首房重试：暂时移动真实大厅prefab并在finally恢复，原实现initial-room-recovery-red 0/1，异常为Published supplied lobby Gaussian is missing。修复后保留启动遮黑与重试入口；重试先完成资源释放再重新加载，不创建虚假切房事务或清空会话。initial-room-recovery-green 6/6，包含真实资源恢复后的手部近触重试、释放屏障和后续切房回退。测试后prefab路径已恢复；失败画面的实际渲染仍待补。

原地整组回归：stationary-current-suite 32/40，8项失败均为已取消的独立入口点击。按用户单一引导要求归档旧快照到docs/history/tests-before-guided-only-20260923，移除8个独立入口参数用例，未恢复独立模式，其他引导情境断言保持。stationary-guided-current 32/32通过（50.93秒），包括储存纸表、测漏关联/学习、模型抓取、毛刷图、全路线、遮黑追踪和首房重试。此处是规格变更后的套件收敛，不将被移除用例计为修复通过。旧洗消快照打开菜单已移至历史工具；工作区实际可视验收仍未签收。

## Unity工作区隔离与同源配置

- workspace-isolation-red 首轮测试清理调用未显示窗口Close导致夹具错误，不作为行为红灯；修正清理后 workspace-isolation-red-valid 0/1，确认预览实例混入当前场景。
- 改用独立PreviewScene与专用Scene视图，保留用户当前场景；刷新释放旧房和预览场景，停止预览不留重复模型。workspace-isolation-vulkan 1/1。首次无图形模式无法初始化Scene视图的失败另保留，不作为图形通过；工具现支持无图形的模型检查及Vulkan的真实Scene视图检查。
- workspace-config-shared 首轮1/2，夹具直接修改资产但未标记未保存编辑，资源释放后被重载；改为按实际编辑流程SetDirty并在finally恢复原值/脏状态。workspace-config-authoring 2/2：改初始位与检查点后，预览和运行时读取相同位置/朝向，运行时保持头部位置不变，刷新释放与场景隔离也通过。这是编辑器配置回归，尚未替代保存后真实Play/导入自动刷新和所有房间视觉验收。
工作区实际取景：tools/capture_inspection_workspace.ps1 -Run isolated-shared-views，Vulkan/Android目标，无Player构建。实际工作区相机输出大厅/办公室坐站4张图，全部已查看。大厅高斯正常；办公室原模型、木纹与水磨石存在，但光照平、坐姿椅背遮挡显著，不能签收C01视觉或最终初始取景。目录artifacts/inspection-workspace/isolated-shared-views。此输出是房间预览，不冒充完整游戏UI或Quest画面。
recovery-final-play：恢复逻辑修复后真实Play再次PASS，正常大厅可交互，无旧序章，高斯激活，exit 0。

## M5.E01 章节目录与会话接入

- 新增`ClinicalActSelection`，从现有旅程房间、允许交接和稳定任务ID生成可用选择。入口大厅不作为章节；返回大厅是独立导航动作；未访问的未来房间不出现；已访问房间标为回查。现有`clinical-journey.json`已经含有路线、房间任务ID与交接关系，本切片复用它而不改数据。
- `ClinicalJourneySession`仅在房间交接提交后增加访问次数；回查与回大厅不推进主线游标。主线完成需到达最后一个有序位置（最终办公室），不能因所有不同房间都已访问而提前成立。首次办公室、回查办公室与最终汇总可区分。
- `FullScriptRoomVisit`按“前往”“回查”“返回大厅”显示操作，继续主线的入口保持主要反馈；保留`Travel_{roomId}`近触对象名，兼容已有交互测试。
- 测试执行入口按UTF-8读取Unity XML结果，修正其遇到中文程序集名时的PowerShell解析失败。

**验证（均显式`-buildTarget Android`；EditMode/nographics，无APK构建）：**

- `clinical-act-selection-red-2`：预期红测0/1，复现最终办公室尚未访问但主线已被判完成。
- `clinical-act-selection-green-3`：5/5通过，覆盖稳定任务ID、大厅与章节分离、回查游标、失败加载访问计数、最终办公室及提交后回查。
- `clinical-act-session-regression-1`：13/13通过。
- `clinical-act-stationary-regression-1`：首次整组回归13/35，22项因本切片曾更改内部触碰对象名而无法找到控件；恢复原对象名并保留主线高亮后，`clinical-act-stationary-regression-2`为35/35通过。又补齐回大厅自动展示章节目录的接续行为，含近触集成用例的最终`clinical-act-stationary-regression-3`为36/36通过。

报告：[`clinical-act-selection-green-3.xml`](../artifacts/inspection-editor/clinical-act-selection-green-3.xml)、[`clinical-act-session-regression-1.xml`](../artifacts/inspection-editor/clinical-act-session-regression-1.xml)、[`clinical-act-stationary-regression-3.xml`](../artifacts/inspection-editor/clinical-act-stationary-regression-3.xml)。未运行真实Play、实际画面或Quest验收；它们仍属于样板关卡/用户验收门槛。

## M5.E02 正式入口与安小卫引导衔接

- 新增`FullScriptGuideBinding`，以现有`VisitorCoachPresenter`和`VisitorDialogueFairyBinding`呈现“安小卫”单页短引导。首个大厅动作是“开始学习”，近触后完成N00并显示目的地面板；当前唯一主线目的地是“前往办公室”，不代表大厅作为章节可选。其他房间近触后直接打开本室检查内容，最终办公室引导先提示回看未完成/待补项。回到大厅不重复欢迎引导，直接显示当前可用的继续/回查目的地。
- 引导状态固定为单页`Guidance`，一个`ChoosePrimary`动作、禁止重播/延后；回调校验对话上下文、revision、动作类型及HandPoke来源，只执行一次。按房间组合生命周期解绑并隐藏对话；组合Tick驱动Presenter淡入/淡出。未接入旧Prologue或旧课程序列。
- 更新正式Stationary组合及编辑器预览路径。预览先捕获开场对话，再由预览夹具关闭引导并显示当前任务；预览操作不作为真实近触或画面验收证据。
- 新增`FullScriptGuideBindingTests` 4项：首大厅短文案、房间本地文案、最终办公室回看文案及单页/单动作合同。原地集成测试另验证大厅入口和Office本地引导均由`ClinicalHandFixture`真实近触触发，触发前没有第二个主面板；动作后才进入章节/房间内容。
- 原地整组测试夹具按实际顺序分别近触入口和房间引导，再操作目录/检查点；回查和返回大厅测试继续保留。

**Android目标EditMode证据（`tools/test_inspection_editor.ps1`，nographics，无APK）：**

- 首轮`fullscript-guide-binding-redgreen-1`在编译期失败，日志定位到测试缺少Bootstrap命名空间及编辑器预览越过Presenter公开接口。修正测试引用并让预览直接调用房间动作后，`fullscript-guide-binding-green-1`为4/4；Android目标Unity编译成功。
- 入口近触集成`fullscript-entry-guide-hand-poke-1`为1/1；原地整组最终`fullscript-stationary-guide-regression-3`为38/38。`fullscript-stationary-guide-regression-1`的16项失败来自旧测试在新房引导出现后仍直接打开房间选择/任务控件；按实际触碰顺序调整测试夹具后最终套件全绿。第二轮剩余3项同样为到房后先读面板的夹具顺序问题，随同修正。
- M5.E02计划项暂不勾选：实际对话画面、坐姿/站姿布局和近触可达性留待用户按计划验收。此次未启动真实Play、截图/渲染或Quest，未构建APK。

### 用户Quest反馈及目的地面板续修

- 用户自行烧录后确认：大厅可近触“开始学习”，随后出现旧样式的“前往办公室”界面。按当前路线，办公室确为首个且唯一主线目的地；旧样式由`FullScriptRoomVisit.Show(true)`生成，与安小卫新版入口面板视觉不一致。
- 已将目的地面板改为复用安小卫主题的浅色表面、深色文字和青绿色强调；标题/说明改为“下一步”及对应引导，单个主线目的地加大为640×82像素，并明确提示近触目的地。多项回查时仍保留双列可选项；没有改用凝视、控制器射线或键鼠输入。
- `c01-entry-navigation-light-1`通过：Android目标Unity编译及`FormalEntryUsesFairyDialogueAndHandPokeToStartTheGuidedMainline` 1/1通过，覆盖近触入口后出现办公室目的地、主题颜色、按钮尺寸及无模式选择。结果见`artifacts/inspection-editor/c01-entry-navigation-light-1.xml`。
- 随后的`c01-start-to-office-2`共2/2通过，覆盖大厅入口引导后显示办公室目的地，以及进入办公室后通过本室近触打开任务；Android目标Unity编译通过。该组合是EditMode场景行为回归，不是连续真实Play、运行截图或Quest画面证据。
- 此修订尚未进入APK；未启动真实Play渲染或Quest验证。之前展示的章节布局图是概念参考，旧大厅截图不是本次Quest画面，均不作为实现/验收证据。相关Quest画面及坐站可达性仍待用户下一次构建后的实机验收，M5.E02与C01.E01保持未勾选。

## M5.E03 房间切换与检查点切换互斥

- 核对并复用现有两种状态机：`RequestRoom`接收章节/房间目录选择后进入FadeOut，完成旧房解绑销毁、独占资源释放，再加载目标并等待可靠追踪；`RequestInspectionPoint`进入FocusOut/FocusMove/FocusIn，只在追踪可用时对齐并回调。两种路径进入非Active状态后`InputAllowed`关闭，重复或交叉请求会拒绝；追踪未恢复不会靠超时完成。
- 目标房加载失败沿用现有恢复逻辑：清理失败目标，重新经过资源释放屏障后恢复原房；恢复房后保留会话并显示重试/重新选择提示。加载提交才推进主线，失败和回查不产生重复访问记录。
- 新增切房近触互斥用例：用户在大厅目录对办公室实际近触一次后，加载期再次切房或切检查点均被拒绝；最终仅增加一次办公室访问与主线步数。原`InspectionPointWaitsForTrackingWithoutFalseCompletion`增补检查点重复/切房交叉拒绝；房间引导集成用例同时验证离房释放旧Presenter与绑定。
- `FullScriptRuntimeTests`既有恢复套件复核12/12；原地流程含切房、回查、检查点追踪丢失和释放屏障的`fullscript-stationary-guide-transition-regression-4`为39/39；新增切房重复提交专项`fullscript-room-transition-guard-1`为1/1。所有调用通过`tools/test_inspection_editor.ps1`显式Android目标、EditMode/nographics；编译通过，无APK。
- M5.E03计划项暂不勾选：转场幕/角色与房间画面、头手追踪及Quest稳定性仍需用户实机验收。此切片未启动真实Play、截图/渲染或设备。

## C01.E01 对话布局与办公室资料文案

- 对话正文按当前中文换行结果扩展高度，不缩小字号；单页隐藏分页计数，主按钮排在完整正文之后。头像资源为空时取消头像栏，正文和主按钮改用整幅宽度；继续按钮可见矩形与近触碰撞体/裁剪范围同步。
- 对话面板使用低眩光浅底、深色正文和克制的青绿色强调。办公室资料移除了参与者可见的版本/静态标签、实现说明、重复固定页脚和分页措辞；保留模拟训练身份、未核实/待补状态及“不证明检测效果”等证据边界。三条`SIM-L00x → SIM-R00x`引用映射未变，资料JSON解析为3条使用记录及6类文档。
- `c01-dialogue-layout-red-1`复现原固定170像素正文区导致短文留白；修复后定向`c01-dialogue-layout-green-3` 1/1通过，覆盖短文、较长中文、单页/多页计数、无头像铺满与收拢面板、正文和动作间距及近触命中范围。
- 办公任务页起初有两个并列主按钮；`c01-office-primary-red-1` 0/1确认双主按钮问题，按当前可用动作只突出一个后`c01-office-primary-green-1` 1/1通过。细项末项不回跳和开发模式标签隐藏由`c01-detail-navigation-green-2` 1/1覆盖。
- 同一Android目标下`c01-visitor-coach-regression-1`为9/9；C05交互修订后的最终`c01-c05-stationary-final-1`为41/41。Unity Android目标编译通过，无APK构建。
- C01.E01保持未勾选：坐姿/站姿中的文字可读性、面板遮挡与手部可达性，以及真实Quest近触仍待用户验收。本轮未启动真实Play、截图/渲染或设备测试。

## C05.E01 办公与储存资料查阅往返

- OF-01字段、OF-02使用记录和ST-02周记录选择已作为各自的有效查看动作；这三类任务页移除了重复的手动“记录已查阅”按钮，其他检查点继续保留原操作。上一条/下一条细项在两侧显示，末项不会回跳到第一项。当前有可用资料时突出“打开资料”，否则突出“下一检查点”，各页只保留一个清晰主按钮。
- OF-01专项回归近触字段后检查字段/记录引用，仅在需要时切换日期和镜号，打开清洗消毒资料再返回；筛选、当前引用和面板位姿保持，只有所选字段/记录加入已查看事实，没有自动完成方法或生成判断。ST-02选周的既有自动留痕与返回行为继续通过回归。
- 清理参与者可见的内部模式标签和“课程仍保留在工程中”等实施说明；暂不可用视频反馈压缩为可继续的简明状态。
- `c05-office-context-red-1`以界面仍有重复按钮复现问题；`c05-office-context-green-1`专项1/1通过。主按钮红测`c01-office-primary-red-1`确认原有两项并列，修正后`c01-office-primary-green-1` 1/1通过。首轮完整组40/41是新增分页断言读取了旧面板对象；修正断言后，`c01-c05-stationary-final-1`最终41/41。`c05-document-routing-regression-1`资料身份路由1/1通过。均为显式Android目标EditMode，编译通过，无APK。
- C05.E01保持未勾选：资料纸表结构与实际办公室/储存面板的坐姿、站姿、遮挡和Quest手部近触仍待用户核对。本轮未启动真实Play、截图/渲染或设备测试。

## C02 悬挂镜体资源候选审计

- 检查了现有源FBX与挂姿候选；源文件没有骨骼/变形器和可信语义分段依据，现有资料也无法确认柜内挂点。
- 当前挂姿草稿连续性检查有141条边拉伸超过2倍、86条边压缩至半长以下，最大拉伸653.82倍；未能满足真实器械形态与连续性门槛，因此没有制作或接入该候选。
- C02保持未完成。继续需要可信的部位分区和悬挂姿态参照，或可识别的分段/骨架来源；候选之后还需检查连续性、UV、标签贴附、穿插和柜体挂点匹配。

## C03.E01 储存主题选择入口

- 首次进入储存库后显示浅弧排列的三项检查内容：中央推荐“检查柜体”，两侧可直接选“查看登记表”或“观察胃镜副本”。选择后用现有检查点转场收起入口并显示对应内容，房间实例保持不变；任务面板底部可重新打开选择入口。未增加新场景或跳过完整房间。
- 复用现有柜门与柜侧登记/纸表。胃镜选项明确标示为平放副本、悬挂检查暂不可用；观察、选项切换及回到柜体均不会自动完成对应任务。编辑器旅程预览先选择推荐柜体，再继续已有储存演示步骤。
- `c03-storage-selector-stationary-1` 在Unity编译阶段首次失败：目录类没有 `Storage` 房间常量。日志定位后，四处判断统一改用当前工程的 `R02_STORAGE` 标识；保留首次失败日志。
- 修正后的 `c03-storage-selector-stationary-2` 共42项，41项通过；唯一失败是旧柜侧登记测试仍在新选择入口上触碰 `NextTask`。更新夹具为直接选择登记表后，`c03-storage-register-reentry-1` 专项1/1通过。浅弧选择、三项直达/回选、完整房间保持与悬挂缺件不签收由`c03-storage-selector-arc-1`专项1/1再次通过；整组其他41项均已通过。
- 上述运行均显式使用Android目标、EditMode与nographics；编译通过，没有构建APK。结果见[`c03-storage-selector-stationary-2.xml`](../artifacts/inspection-editor/c03-storage-selector-stationary-2.xml)、[`c03-storage-register-reentry-1.xml`](../artifacts/inspection-editor/c03-storage-register-reentry-1.xml)和[`c03-storage-selector-arc-1.xml`](../artifacts/inspection-editor/c03-storage-selector-arc-1.xml)。
- C03.E01仍保持未勾选：入口文字布局、坐姿/站姿可读性及真实Quest近触未做画面验收。C02可信悬挂资源缺口仍未解除；本轮没有制作或接入挂姿模型，也没有启动真实Play、截图/渲染或设备测试。


## C07/C08 媒体接入与视频原声例外

- MV01已复制到`app/Assets/StreamingAssets/ClinicalCourse/sink-trigger-04m46s-05m06s-with-audio.mp4`；RE-03第三细项的近触按钮打开固定播放器，支持暂停/继续、重播、失败重新加载及返回。复用Video模块，观察订阅先释放，播放器先Close，再销毁面板；离房/旅程结束复用同一清理路径。播放完成只增加`RE-03:video-watched`行为事实，不完成水路任务。
- `VideoRuntimeOptions.IgnoreListenerSilence`默认关闭，仅该片段明确开启。其AudioSource忽略全局listener暂停/音量，旅程全局静音保持原状；未开放其他声音。应用暂停时暂停播放，加载中断可重新加载；播放失败会停止并释放资源，关闭同步停止音源。
- V09/V10原样复制到`Resources/ClinicalCourse/FullScriptVisuals`，RE-02/RE-06实际加载。两者保留近触“对照原图”切换；V10六细项的提示区域按新图重新定位，毛刷继续使用已有资源。三份运行媒体SHA-256与归档来源完全一致；图册及各manifest已补实际消费者。
- 清除当前任务页直接显示`task.unavailableReason`的开发说明，换为简短真实缺件状态；视频不再显示“片段暂不可用”，设备/人员文案说明教学图身份与原厂资料边界。未修改模型、材质或FBX导入。
- 红测`sink-entry-red`：1项失败，确认原流程没有视频近触入口。首轮`sink-media-green`：7/7通过。`sink-audio-final`：7/9，新增两例断言暴露headless EditMode的AudioListener音量setter读回为1；改为比较配置前后实际全局状态，仍断言两种局部音源开关和其他音源默认值。失败日志保留。
- 最终[`sink-audio-final-2.xml`](../artifacts/inspection-editor/sink-audio-final-2.xml)：9/9通过，显式Android目标实际导入/编译通过，日志无C#编译错误。局部EditMode覆盖入口、失败重建、暂停/重播意图、完成不代签、返回/旅程销毁时取消订阅与释放surface、教学/原图切换、六部位提示，以及音源例外默认关闭且不修改全局状态。视频生命周期集成使用注入播放器，不能代替真实解码和声音验收；headless日志包含XR音频空间化插件未初始化提示。
- 静态：本轮代码`git diff --check`通过；媒体哈希3/3一致。真实Play、运行截图/渲染、Quest播放与原声实听均未执行；没有构建APK或设备操作。
- C07.E01/E02、C08.E01保持未勾选：现阶段入口位于任务面板，原水槽空间位置的近触触点仍待接入；设备逐项热点/学习记录、六部位实际交互与Quest可读可达性未完成，图片不解除实物缺件。完整计划仍有C02及各房资源/判断/汇总工作，不能以本次9项通过签收整体。


## C04/C05/C06/C07/C09 其余房间主题入口

- 办公室首次以办公现场、电子记录、纸质资料三组进入全部OF任务；两间诊疗室以用途设备、报告历史、台面废物进入各自任务，保留GI/RESP标识与独立状态。洗消室六类主题分两组，每屏至多三项；产品入口仍打开既有瓶体操作。候诊区直接提供两侧观察入口，不增加主题确认层。
- 入口没有整块背景大面板；保留完整房间，使用固定世界位置的近触按钮。选中后销毁选择入口，同检查点直接展开，改变检查点复用遮黑对齐；任务页可随时返回主题。尚未提供的报告、附件、人体和柜内情境没有因菜单直达而完成。
- `room-themes-red` 0/1复现办公室仍直接进入线性任务页；`room-themes-green` Android实际导入/编译及局部EditMode 4/4通过，覆盖全部新增房间入口、跨房标识、任务不代签、选择时保持房间实例、入室对话互斥，以及视频/教学图片相邻回归。旧用例的近触夹具在Travel后显式选择推荐主题；新增主题用例保留原始菜单逐项验证。
- 真实Play、渲染、Quest与坐站可达性未运行，未构建APK。相关C项保持未勾选，任务入口落地不代表其物件、证据、判断或体验验收全部完成。


## C10/C11 判断、提示与汇总；本批收尾后按用户要求暂停

- 新增会话内学习记录：打开、有效观察、操作、方法示范、提示、完整讲解分开记录；相同事实去重。电子字段、周登记选中、图像主动展开、真实柜门/门体状态、抓取事件、视频完成已接入不同事实，通用“记录已查阅”改为主动请求安小卫提示。打开页面不等于观察，更不等于判断正确。
- 只对已有完整模拟数据的电子记录六字段、储存柜四周登记、逐次测漏三条使用关系开放练习。判断要同时核对引用；错误先给观察线索，可回资料重看、修订或主动请求完整讲解。记录首次及后续错误次数、修改次数、提示和讲解使用，改对后不抹掉此前错误。缺失实物、报告或适用依据的任务没有生成正确/错误结果。
- 移除仅靠点击阅读/确认完成上述任务的路径，改为全部业务子项作答与引用核对后完成。柜体登记引导练习使用与发布数据一致的第三周缺项情境；“每周”仍只属模拟情境。办公室/洗消的逐次对账共用同一记录与结果，洗消实物附件任务不会因此完成。
- 安小卫专属Presenter实际接收首次方法、错误反馈、提示及完整讲解；对话与资料/练习面板交接，关闭时解绑事件。返回资料保留筛选、所选行和展开位姿；跨房回查保存任务/细项位置。任务事实/结果提交后拒绝修改，新会话重新创建空记录。
- 最终汇总增加有效查阅、操作、已判断、曾需重试、提示、讲解及修改信息；移除本次修改涉及的汇总内部编号/缺件开发原因。引导模式提交后可以查看已有模拟记录逐字段复盘，不生成医疗合规评分。
- 先前执行的证据：`learning-judgement-red` 0/1确认旧方法确认能直接完成任务；领域层`learning-progress-green-3` 1/1通过（错误保留、修订、重复提交去重、不可评不作答、方法去重、提交锁定与新会话清空）。该结果发生在最终UI接入之前，不作为最终批次测试通过。
- 中间编译失败已修复并保留日志：首次使用Unity当前API未提供的FirstOrDefault重载，改为Where/DefaultIfEmpty/First；并行新增编辑器发布脚本两处命名空间错误，最小修正为UnityEditor.SceneManagement.OpenSceneMode及MapNavigation.Contracts引用。本对话没有改模型、材质、贴图或调用模型发布。
- 用户随后要求合并几项再验证，最终要求“完成这个任务后暂时暂停，不用测试”。因此最终批次**未运行EditMode、真实Play、渲染、Quest或任何测试套件**。只遵守工程编译门槛执行`-batchmode -quit -nographics -buildTarget Android`，未传runTests或构建入口。
- 最终[`learning-batch-compile-only.log`](../artifacts/inspection-editor/learning-batch-compile-only.log)明确Targeting platform: Android，发生实际脚本编译，进程退出0，无C#编译错误。未构建APK、未操作设备。最终修改的交互、布局、生命周期及回归仍需后续测试/用户验收，不以较早的局部通过代签。
- 当前暂停，不启动下一批。原计划C02及各房实物/内容缺口、热点与整体验收继续保留；C10/C11已实现部分不等于全部验收完成。
