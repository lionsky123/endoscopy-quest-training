# 完整剧本视觉资产图册与接入台账

## 房间 UI 专题概念参考（2026-09-24，非运行资源）

| ID | 源/加工与文件 | SHA-256 | 消费者与验证边界 |
| --- | --- | --- | --- |
| UI-CONCEPT-01 | Codex 内置 ImageGen 对当前正式 Play 办公室旧版截图进行两轮编辑，用户选择烟黑悬浮形态并确认浅紫灰选项/钴紫选中态；[最终概念预览](concepts/room-panel-smoke-lavender-concept-20260924.png)。原截图 `artifacts/production-play/ui-final-source-20260924-1-04-office-topic-selector.png` SHA-256 `57A39CDF6F19FAF2A9EE40D8797D3B85575BA70358F8C0A51804938C234434FE`；中间稿与完整提示词见[概念来源记录](concepts/README.md)。 | `938370FE07C500CBB57D68835235DA0C0052FFA21A5258DE3E55C959D2E7090C` | 仅由[专题规格](../ROOM_UI_REFRESH_SPEC_20260924.md)消费作视觉方向参考。未放入 Unity Resources、Prefab 或正式界面；未经真实 Play/Quest 实现与验证，不作为产品画面或医学证据。 |

## 分幕体验资源（2026-09-23）

设计与实际任务见[原合并计划](../STARTUP_FLOW_AUDIT_PLAN_20260923.md#分幕体验设计与实施顺序2026-09-23)，资源验证与限制见[本轮报告](experience-20260923/README.md)。生成不等于接入；V09/V10已由FullScriptStationaryContent消费，MV01已由FullScriptSinkVideo消费；实际验证见启动修复进展，不覆盖仍在使用的V04–V08/B01或原参考图片。旧图已从活动图册移除引用并标记退役；文件仍留在工作区，删除操作被执行策略拦截，详见[图像清理记录](experience-20260923/IMAGE_CLEANUP_20260923.md)。

| ID | 源/加工与文件 | SHA-256 | 拟消费者与验证边界 |
| --- | --- | --- | --- |
| V09 | 原图image4内置生图去大红字/箭头；[设备教学底图](experience-20260923/equipment-hotspot-teaching-v1.png)，1086×1448 | `fbd5c733644ebd15ca058e92559f42c4d25201bc952a95f53afa2e7b1ad76e10` | C07.E02：Resources/ClinicalCourse/FullScriptVisuals/equipment-hotspot-teaching-v1；RE-02默认教学图，可近触切换原图。设备逐项热点仍待接入；生成细字/数值不作证据，不替代实物设备 |
| V10 | 原图image5人物示意重制；[防护底图v2](experience-20260923/ppe-teaching-plate-v2.png)，1024×1536 | `ddb14c0e675db14b13a26849603d35ffeba8f7070e48de4437ca21caa81cdb7d` | C08.E01：Resources/ClinicalCourse/FullScriptVisuals/ppe-teaching-plate-v2；RE-06六细项提示区域与原图切换。图像形态非医学验收，三维部位交互仍待接入 |
| MV01 | 用户原片4:46–5:06；[20秒有声片段](experience-20260923/media/sink-trigger-04m46s-05m06s-with-audio.mp4) | `bcfba19d07b78ffa5184ceb66572d13dedd62125a6c22de47fb9c7eb22278bbe` | C07.E01：StreamingAssets/ClinicalCourse/sink-trigger-04m46s-05m06s-with-audio.mp4；RE-03第三细项近触播放器，暂停/重播/失败重载/返回，单音源原声例外。20.00秒/720p/30fps/H.264+AAC；水槽实物触点及Quest播放待验收 |
| M-PPE01 | 用户桌面医务人员穿戴.FBX；[源副本](experience-20260923/models/medical-worker-ppe-source.FBX) | `c08849f9fe06120b3e3c6f5e43a7284f37a2f68c8bf9cc0081fe855b519cd77b` | C08.E02优先复用；FBX7400，1网格/1材质/1UV/内嵌贴图，17514顶点/17512面；未导入/渲染/部位验收 |

输入图哈希、提示词、拟发布路径及已清理图像哈希均在本轮报告或清理记录。MV01按用户最新“不要静音”保留原声，是本片局部例外；其他媒体及向导/环境/操作不自动解禁。原视频字幕内容不自动转成判分依据。M-PPE01优先于新增人物制作，V10不作为三维缺件替代。

## 主页大厅360°全景（2026-09-23，替代高斯）

用户提供mudj41fg.png，项目资源Assets/EndoscopyTheme/Panoramas/Lobby360.png；SHA-256 `154DDF087AE829D68044F960853721D987C2BFBB39596419513FDEEC6B60FA6F`。未新增生图，未修改像素。正式大厅与Unity预览共用LobbyPanorama.prefab，不作为医院实拍/医疗证据。接入与验证见[迁移记录](../LOBBY_PANORAMA_MIGRATION_20260923.md)。

V08测漏纸表交互补充：原三条数据行增加透明近触区域与返回后的轻选中提示，按UseId跳转同源使用记录；复用原表线/文字布局，未修改图像或新增图。29/29验证通过，办公室/洗消初始页截图更新并核对；选中状态与正确关联由真实近触测试覆盖，不据此宣称完成正式测漏检查。

## V08复用为模拟测漏纸表（无新增图像）

`FullScriptOfficeRecords.BuildLeakPaper` 在办公室OF-02及洗消室RE-05第二细项共同消费已有空白五列纸面，不修改贴图、不重新绘制表线或新增卡片。列为登记号、使用号/内镜编号、测漏时间、操作人员、登记结果；3条同源程序数据，第四行明确未使用。镜号从关联电子记录取得，不另复制一份。数据升级SIM-20260921-2，既有三条登记值不变，改为结构化条目并校验关联。

V08 SHA256仍为 `437BDAD2B02C2F884259512B5861C56C03792FDA26330468F29A3B7FE12D3C56`。此为复用生成纸面制作的模拟表，不是医院原表、实拍或检测凭证。无新增生图，图册编号不增加。

`leak-paper-reuse.xml`25/25通过，20.210秒；`stationary-final/office-leak-register-paper.png`、`washing-linked-leak-ledger.png`已查看，15个单元格/长镜号/范围声明/空行和按钮可见，未见越格。纸面只读、不自动计分；正式资料、测漏设备及独立情境仍缺，编辑器截图不是Quest验收。

## V06洗消同源只读消费者（无新增图像）

已有 `office-records-reference-v2.png` 增加RE-05测漏细项消费者，复用 `FullScriptOfficeRecords` 的sourceOnly模式：使用记录表仍为同一原稿加工屏、同一程序数据；与既有SIM-LEAK正文相互对照，不生成第二份表或新文字纹理。仅主动打开时加载UI，无办公室模型/终端。24/24回归通过，`stationary-final/washing-linked-use-records.png`和`washing-linked-leak-ledger.png`已查看。后者为既有模拟文字资料页，不声称完成医院原始登记表视觉还原；生产图像文件与哈希不变。

## V08柜侧载体复用（2026-09-21，无新增生图）

同一`storage-cleaning-register-v1.png`新增`FullScriptStorageRegisterMount`实际消费者：固定在训练柜左外壁，通过ST-02主动遮黑观察、近触纸面放大。挂载和放大共用`BuildStoragePaper`/`StorageCleaningRecords`，不生成第二份内容，不在图像中硬编码答案；仅柜侧观察时开启纸面输入。随房间卸载，不引用其他房间模型。初版右侧被原门体挡住，切左后操作条仍落在柜板后；现观察距纸60cm/工具条52cm，保留全部几何；最终`storage-register-final.xml`63/63通过，两张`stationary-final/storage-cabinet-side-register.png`/`storage-cabinet-register-expanded.png`已核对并通过截图门槛，纸面及完整操作条可见。共享圆角图备用单位错误已修复，V08文件及哈希未改。下方“实体入口待做”为本轮前历史状态；单页载体不是多页装订交互。

## V08：储存柜模拟连续登记（2026-09-21）

新增并实际接入：`app/Assets/EndoscopyTheme/Resources/ClinicalCourse/FullScriptVisuals/storage-cleaning-register-v1.png`，1536×1024，GUID `5f00147f0c274b27b3ce971bb980c4ed`，SHA256 `437BDAD2B02C2F884259512B5861C56C03792FDA26330468F29A3B7FE12D3C56`。完整提示词见PROMPTS.md V08。V01–V08系列共8张，另有旧B01，不把参考原图计作生图。

来源边界：旧稿O-S06只有柜侧每周清洁登记文字，本次核对未找到对应医院原表图片。因此生成的是标注来源的模拟纸质表样，不冒称原图复刻、真实病历或临床标准。图中仅空白纸面/表线；正式训练数据由`StorageCleaningRecords`单源供给，禁止依赖生图文字作证据。

实际消费者：`FullScriptStorageRecords`，正式原位ST-02近触打开完整1页/4周，按实测表线排入日期、对象、内容、操作人。带教完整样例与独立缺项情境分离；逐周选中、判断、引用、明确学完/跳过、提交后只读与办公室复盘已接。只打开不完成。原始柜侧挂本实体入口仍待做，当前为原位近距查看；本图不解除ST-01空柜/悬挂镜体缺项。

验证状态：20单元静态通过，最终Android目标`storage-cleaning-records-verified.xml`55/55通过，74.709秒。`artifacts/stationary-final/storage-cleaning-register.png`与`storage-cleaning-register-selected.png`已核对：表线/文字对齐、选周高亮、完整操作区均可见。固定1.2m观察者、原位摄像机参数未为表格调位；编辑器渲染不是Quest真机验收。前两轮各54/55，分别发现旧6项计数断言和新增复盘溢出，均修复后再验证；没有APK构建。

## 储存/候诊照明消费调整（2026-09-21，非新增生图）

原模型派生建筑、门/灯原材质、已有水磨石及柜体表面均保留。两个房间现在由VirtualRoomEnvironment增加房间独占的4个无阴影顶棚Spot和中性三色环境光；不是改用统一白材质或生成背景图。房间根销毁时释放光源并恢复环境设置，其余房间不改。训练灯位是近似，不宣称实地照度或Quest性能验收。

最终`derived-room-lighting-balanced.xml`50/50通过，72.053秒；`stationary-final`中的储存整体/开门、候诊/诊疗通道侧4张实际截图已核对。前版与首版光照分别保存在`artifacts/room-lighting-before/`、`room-lighting-first/`。第二版改善首版过暗天花与强光斑对比，保留原表面及局部明暗；接触阴影、反射、真实表面细节和缺失物件仍未验收。本轮无新图片资产，无APK构建。

## 已有模型观察消费者调整（2026-09-21，非新增生图）

`FullScriptRooms/Stationary/Gastroscope`及`ClinicalCourse/Disinfectant/disinfectant-labeled`现分别由ST-03/RE-04的“检查实物”主动加载，而非放在说明面板前的小模型。最长边38cm仅为展示副本，保留源几何/贴图/材质，胃镜初始旋转使盘绕表面可见；不把盘绕状态改称悬挂。低位工具条支持主动取回和返回，提交后只读；不修改V01–V07/B01图像。

`source-object-clearance-verified.xml`43/43通过，61.001秒。`artifacts/stationary-final/gastroscope-object-inspection.png`和`disinfectant-object-inspection.png`已核对，正面标签、原胃镜表面及完整操作条可见，模型不遮挡说明。初次裁切和初始背面朝向已修正，记录见原位重构文档；不把截图当作医学依据或Quest验收。

## 旧素材实际复用：毛刷细节（2026-09-21，非新增生图）

补登记B01：`app/Assets/EndoscopyTheme/Resources/ClinicalCourse/brush-inspection.png`，既有C09生成教学图，1672×941，GUID `10ba6ba1d2f34602be3a0644a0bc3f1b`，SHA256 `ECB860E1527563D8B4DB4101E471EC547C62F01FDABFF89EA8F660FC88FE6DC5`。来源见ClinicalCourse/SOURCES.md；不是本次新增图片，未编辑原图。V01–V07的7张仅是该编号系列，不包含这张此前未集中登记的旧课素材。

新消费者：`FullScriptStationaryContent`正式RE-02第三细项，带教模式显示；前两细项仍用0920设备原图。近触“查看刷毛/查看芯线/返回全图”仅改变RawImage取样区域，并按区域宽高比显示，可放大；原图不重绘、不复制、不预加载。独立核查不显示示范，查阅只记RE-02:2，不将RE-02签为完成，不恢复旧course.json或旧课程控制器。当前仍缺现场工具模型及独立情境。

运行证据：`artifacts/vr-interaction/latest-brush-consumption.xml`41/41通过，56.932秒；新增带教/独立两用例覆盖真实SDK近触、局部比例、固定面板、原图切回复位、未冒签、旧课程模块未恢复及离房无旧RawImage。`artifacts/stationary-final/washing-brush-overview.png`、`washing-brush-bristles.png`、`washing-brush-wire.png`已人工核对：实际显示对应图像区域，来源提示可见，图片不遮挡底部按钮。截图为Android目标Vulkan编辑器，不是Quest验收或资源内存实测；本轮未构建APK。

## 储存柜结构派生（2026-09-21，非新增生图）

V07新增三维结构参考用途：`StorageRoomPublish`据其通用双门柜构成补建结构训练模型，并复用原OfficeRoom已发布建筑网格。`FullScriptRooms/Storage/StorageRoom`进入默认R02，`FullScriptStorageCabinet`实际消费左右门/把手/铰链与可见内壁；柜体单独Prefab是出版源副本，其网格/材质也由房间Prefab消费，不在运行中额外加载第二个柜体。生成图仍为7张，没有新增贴图或医疗表格。

柜体尺寸为训练布局参数，不是厂家测量；无竖直镜体，空安装横梁明确待补。玻璃、密封边和通风开孔是可见结构，不证明气密性、通风性能或管腔干燥。已发布1828三角形柜体及原建筑派生房间，来源在Storage/provenance.txt。门/灯原FBX内嵌材质已复制为独立.mat并保留设置，Storage/Waiting递归依赖中的FBX均为0（各目录dependencies.txt）。没有将所有原模型材质替换成统一白色。

`storage-cabinet-two-distance.xml`最终39/39通过；Vulkan实际整体/闭门/开门截图已核对，确认几何与把手、柜内空间存在，非仅生成参考图。`artifacts/stationary-final/storage-cabinet-overview.png`可见柜体上部全貌、双门、通风带及原建筑，底部仍有操作条遮挡；`storage-cabinet-open.png`可见展开门与空安装横梁。整体/近处观察切换已接入，整体位禁用抓取。当前表面光照仍偏平，写实表面、悬挂镜体和完整核查仍未完成，不作完整视觉验收。

## V07新增：储存柜带教图（2026-09-21）

生成图总数现为7张；下文V01–V06的六张统计为早期记录。新增`app/Assets/EndoscopyTheme/Resources/ClinicalCourse/FullScriptVisuals/storage-cabinet-teaching-v1.png`，1536×1024，SHA256 `DFE659EB9991A0A9514D5734C67474C704FCB89608075D7D0BC9E83C2ECB82C7`，GUID `d1de248798044e60b2c7e31b68af91ca`。

来源：内置imagegen按旧稿场景2镜头一补全储存库教学；无原照片作为输入，非实地拍摄、非特定产品/医院。图中闭合玻璃柜、内壁、开孔及三支分别悬挂的镜体可见。通风开孔外观不证明通风性能，外观干燥不证明管腔干燥；不生成医学合规分数，不代替三维开柜或真实柜内检查。

实际消费者：`FullScriptStationaryContent.ImageFor(ST-01)`，仅GuidedLearning创建RawImage，支持原位放大/返回，默认正文与放大页均标明生成教学图。独立核查不显示示范图或“三支”情境提示。只查阅不将ST-01从缺项状态改为完成。原图保留于Codex generated_images目录，工程保存独立副本；完整提示词见[PROMPTS.md](PROMPTS.md)。没有改写任何医疗记录表。

运行证据：Android EditMode `storage-teaching-waiting-framing.xml`38/38通过，包含真实近触带教消费、放大图不覆盖操作区、独立隐藏与任务未冒签。静态20单元通过；纹理导入GUID与原图/工程副本哈希已核对一致。

运行截图：`artifacts/stationary-final/storage-teaching-expanded.png`已人工核对，生成图/来源提示实际显示、底部操作未被遮挡。不是把生成原图当作运行证据。候诊两侧同目录截图已重拍，斜向视角新增座椅与地面可见区域；仍未达到完整环境视觉验收。此轮只增加V07，不改已有表格或原稿图。

## 2026-09-21候诊派生资产接入增量（非新增生图）

双侧观察续作：`WaitingObservation`/`ClinicalCorridorObservation`已由真实近触遮黑切换消费，现有资源不重复生成。`waiting-two-sided-verified.xml`36/36通过；重拍`waiting-observation.png`与`waiting-corridor-observation.png`显示两侧入口与深色按钮，旧截图按钮过浅已修正。人工检查仍发现坐姿首选朝向正对隔断、椅子落于侧方，完整空间关系展示仍需改善；外观和独立核查未验收。

`app/Assets/EndoscopyTheme/Resources/FullScriptRooms/Waiting/WaitingRoom.prefab`已由默认R03_WAITING实际按房加载；源为已提供OfficeRoom墙/地/顶/灯/门及既有独立Chair，来源映射随资源保存为`source-meshes.txt`。使用原灯/门材质，地面复用V04水磨石，墙/玻璃为派生表面；原FBX保留。未新增图片、未用生成图替代医疗记录。运行消费者为FullScriptRoomCatalog→VirtualRoomEnvironment；WT-01可近触收起面板观察。

证据：`artifacts/stationary-final/waiting-observation.png`、`05-R03_WAITING.png`；Android EditMode `waiting-source-room.xml`36/36通过。当前三椅为办公室转椅，非医院候诊排椅；派生训练布局非实地扫描，两通道观察/独立判断/设备性能与完整视觉验收仍待完成。截图中返回按钮对比度低，已在截图后改用标准深色样式，待重拍。

更新：2026-09-21。此图册将此前已经生成的5张图片与本次按原图加工的1张图片统一登记；不再依靠聊天上下文记忆。原始剧本DOCX不修改。以下图片均为内置imagegen产物，不是实地照片、医学凭证或运行时验收截图。

## 使用规则

文档参考图优先于自行设计的界面。电脑外观须对照0920稿的image2登录页、image3记录查询页；不使用大块卡片选择界面替代记录表格。已有图片先复用，确需加工时保留旧版并记录变更原因。屏幕图片与准确业务数据分层，日期、镜号、时间、姓名、空白与用户判断均来自同源运行数据，不能把图片中生成的字符当作正式证据。

模型来源以用户逐次明确指定的路径为准。本轮在既有`C:\Users\i1204\Desktop\Models`导入外，用户另行指定`C:\Users\i1204\Documents\xwechat_files\wxid_beoh8049sja622_cb77\msg\file\2026-09\清洗室模型.FBX`替换清洗室；这项授权仅针对该源文件。旧记录中对Models.zip的只读盘点仍只作历史来源信息。处理FBX前核对实际材质、内嵌/外部贴图和绑定；不能笼统称为白模，也不能把贴图槽为空推断为所有材质缺失。

## 素材清单

| ID | 图片 | 来源与用途 | 当前状态 / 后续消费者 |
| --- | --- | --- | --- |
| V04 | hospital-terrazzo-v1.png | DOCX image1.jpeg；浅灰水磨石底色 | 已绑定Office/Clinical派生资源的ReferenceHospitalTerrazzo地面材质；按实际网格位置生成地面UV，不给墙和设备统一铺贴 |
| V05 | office-oak-v1.png | DOCX image2.png桌面；浅橡木底色 | 已绑定Office派生资源的ReferenceOfficeOak桌面材质；保留金属桌腿、电脑塑料与设备表面 |
| V06 | office-records-reference-v2.png | 严格按DOCX image3清理重建记录表格；五列含复合起止时间 | 已由FullScriptOfficeRecords按房加载，实体电脑与放大表格均使用；数据格由程序填入，日期/镜号筛选和字段近触独立运行 |

V04–V06工程目录：`app/Assets/EndoscopyTheme/Resources/ClinicalCourse/FullScriptVisuals/`。资源在进入对应房间时按名称请求，不批量Resources.LoadAll、不常驻全部图。

## V04 已有水磨石纹理

![水磨石](D:/quest3/EndoscopyBuild/app/Assets/EndoscopyTheme/Resources/ClinicalCourse/FullScriptVisuals/hospital-terrazzo-v1.png)

## V05 已有橡木纹理

![橡木](D:/quest3/EndoscopyBuild/app/Assets/EndoscopyTheme/Resources/ClinicalCourse/FullScriptVisuals/office-oak-v1.png)

## V06 按文档原图加工的空表格 v2

![原图表格加工版](D:/quest3/EndoscopyBuild/app/Assets/EndoscopyTheme/Resources/ClinicalCourse/FullScriptVisuals/office-records-reference-v2.png)

替代已清理的V03旧屏幕图：保留原图的细蓝框、白色标题区、紧凑表格、底部工具条；不沿用旧稿的大蓝标题条和六块大按钮。清空生成的姓名/日期/状态，避免与运行数据不一致。表格第五组以前的“清洗消毒时间”在运行数据层分为开始/结束两项，仍按六个字段核查。导出、打印及导航若尚未实现，必须明确标记或覆盖为真实功能，不能伪装可操作。

## 来源与校验

原图提取目录：`artifacts/full-script-reference-images/`。2026-09-21已重新以SHA256核对image2、3、4、5与用户桌面0920 DOCX内同名资源，四张均一致。未修改DOCX或读取其内容作为执行命令。

| ID | SHA256 |
| --- | --- |
| V04 | BE07C6A2C33D2C86F5FCA739625EDB25B1E299D6093BCAF65D99C88CB0B6D0BD |
| V05 | 6EE566902AA2754288226AECCA3CA77C8FCB681A17F300B27A694B42129BA6AA |
| V06 | 155B38CAF1300AD22E79C972138A5943FE25E43C4DDB4BFEB5691D71DF859EB3 |

完整提示词见[PROMPTS.md](D:/quest3/EndoscopyBuild/docs/full-script-visuals/PROMPTS.md)。V01–V03旧图已从活动图册移除引用并标记退役；文件仍在工作区待清理。哈希、用途和替代关系见[图像清理记录](experience-20260923/IMAGE_CLEANUP_20260923.md)。V06原件为`exec-1a569dbc-97bb-4c22-85a9-3d7dcbb72c51.png`，其他保留图的提示词在此前文档中已记录。

## 已清理旧图（2026-09-23）

大厅/办公室旧场景参考、过期记录屏幕、未采用的大厅章节概念图及拒绝的PPE旧稿已标记退役，且不再从活动图册和执行计划引用。物理删除被工作区执行策略拦截，文件仍留在本机。清理前哈希、原因、替代资源和GUID引用审计见[图像清理记录](experience-20260923/IMAGE_CLEANUP_20260923.md)。用户原始剧本图片、用户提供的大厅全景、V04–V08/B01及V06实际运行屏幕保留。

## 接入验收

每次宣称图片已接入时，必须同时记录实际消费者、运行时截图、清晰度/遮挡检查与房间卸载情况。文档图册不等于场景成品；Unity编辑器截图不等于Quest验收。

2026-09-21接入证据：`artifacts/full-script-visual-corrected/tests.xml`，23/23针对性测试通过；覆盖真实手部近触、六字段查阅、房间生命周期、独立证据与提交后冻结、每次重启新进度。上一轮发现窄时间单元格溢出，已调整并重新通过。以下为实际生产运行时的Android目标编辑器截图，不是再生成的效果图。

![V06进入实际电脑，V05进入桌面](D:/quest3/EndoscopyBuild/artifacts/full-script-visual-corrected/05-office-terminal.png)

![原图表格与同源运行数据](D:/quest3/EndoscopyBuild/artifacts/full-script-visual-corrected/06-office-records-or-summary.png)

![V04地面和V05桌面实际绑定](D:/quest3/EndoscopyBuild/artifacts/full-script-visual-corrected/04-R01_OFFICE.png)

当前仍非最终写实成品：墙体、设备及部分家具仍以源素色材质显示，光照和接触阴影仍需完善；不能以这两张纹理宣称全部材质已完成。V01–V03旧图已退役但文件待清理；V04/V05继续只用于对应材质、V06继续用于运行屏幕。每房独占资源退出后释放再加载下一房，未增加全图预加载；Quest性能和真实场地仍待用户编译后验收。

## 后续流程截图（不是新生成图）

`artifacts/full-script-review-verified/`记录独立核查完整路线、统一提交和逐字段/逐任务复盘，针对性测试39/39通过。以下截图中的汇总层是训练流程界面，不替代办公室原稿参考电脑系统；电脑仍使用上方V06和同源动态数据。图片总账仍为6张生成资产，本次没有新增生图。

![提交后才公开的六字段复盘](D:/quest3/EndoscopyBuild/artifacts/full-script-review-verified/26-field-review.png)

![缺失PPE模型仍保留独立任务](D:/quest3/EndoscopyBuild/artifacts/full-script-review-verified/51-task-record.png)

## 用户大厅高斯的实际渲染（不是新生图）

源自用户交付的`b660f052d2670589c7476bc95309ed56.ply`，不是以生成平面图替换模型。793729点已导入；下图为Android目标Unity编辑器Vulkan/URP的实际高斯渲染。当前非主线部署：视高、比例、碰撞、双眼及Quest性能仍待验证。详细来源、固定渲染依赖与边界见[大厅接入记录](D:/quest3/EndoscopyBuild/docs/LOBBY_GAUSSIAN_INTEGRATION.md)。

![大厅原始视点实际渲染](D:/quest3/EndoscopyBuild/artifacts/lobby-gaussian-import/lobby-01.png)

![大厅较低视点实际渲染，比例仍待校准](D:/quest3/EndoscopyBuild/artifacts/lobby-gaussian-import/lobby-06.png)

## 原地版实际接入（2026-09-21晚间）

| ID | 实际文件/来源 | 消费者和状态 |
| --- | --- | --- |
| R04 | ClinicalCourse/FullScriptVisuals/script-equipment.png；0920 DOCX image4原图 | FullScriptStationaryContent的RE-02，原位图解/放大。原图仅清楚标注部分设备，未擅自补画成完整七类证据 |
| R05 | ClinicalCourse/FullScriptVisuals/script-ppe.png；0920 DOCX image5原图 | RE-06六页对应衣、口罩、眼面部、帽、手套、鞋；六部位框示与放大。仅参考图，不冒充三维人员 |
| G01 | Stationary/LobbyGaussian.prefab引用已导入793729点资产 | 默认大厅按房加载，生产URP feature；来源两PLY均保留。编辑器实际渲染已见，XR/Quest另待验 |

R04 SHA256：`F5C8B7D112CD9A9B240FB3E4E162180B64ED7AE8D7C8AC9238B5F7185ABE72A1`。
R05 SHA256：`BB68098551011A765514044A6168C94552F167B7B23E7886AD1023058547A57E`。

交付复核：`artifacts/stationary-final/washing-RE-02.png`与`washing-RE-06.png`确认两原图实际显示；PPE meta GUID修复后，资源加载断言与其余回归97/97通过。该目录`01-live-gaussian-lobby.png`为最新默认入口的实际大厅截图。
两原图从已核对的artifacts/full-script-reference-images复制，未编辑或生图。新消费者没有改变已退役V03与当前V06表格之间的数据关系，办公室运行查询使用V06。

![默认运行入口使用用户高斯](D:/quest3/EndoscopyBuild/artifacts/stationary/01-live-gaussian-lobby.png)

这是FullScriptJourneyRuntime的实际Vulkan编辑器截图，1.2m模拟坐姿视点，非独立高斯预览脚本、非新效果图、非Quest验收。图中文字瑕疵来自原高斯；米制尺度暂定3倍，仍待核准。
2026-09-21 晚间状态覆盖：大厅已绑定默认原地版入口，实际运行截图见本页末尾。原稿设备/PPE原图新增实际消费者，未新增生成图（生成图仍V01–V06共6张）。旧段落“大厅尚未绑定”是此前状态。详见[原地重构](../STATIONARY_REBUILD_20260921.md)。

## 2026-09-23 用户 Models 模型贴图接入

来源为 `C:/Users/i1204/Desktop/Models/`。完整文件清单、SHA-256、项目目标路径、材质贴图消费者及房间发布结果见[模型导入清单](../../artifacts/model-texture-import-20260923/models-import-manifest.json)。

| 资源组 | 处理与实际消费者 | 当前状态 |
| --- | --- | --- |
| 办公室 `办公室.FBX` | 逐材质恢复FBX内嵌的27张底色图；`FullScriptModelImport`生成的Office材质引用`Resources/FullScriptRooms/Office/SourceTextures`，`FullScriptRoomPublish`写入办公室房间清单供`VirtualRoomEnvironment`按房读取 | 27/64个发布材质组有源贴图；其余源材质保留原颜色。源FBX UV保留，未再用V04/V05替换办公室地面或桌面 |
| 诊疗室 `诊疗室模型.FBX` | 恢复FBX内嵌的5张底色图到Clinical房间资源，并写入诊疗室房间清单 | 5/61个发布材质组有源贴图；其余源材质保留原颜色。源UV保留 |
| 清洗室 `清洗室模型.FBX` | 用户指定的Documents源文件已复制到`ImportedModels/Source/CleaningRoom_20260923`；SHA-256 `1f597a7d3fe779123328d65ad36e2b7758ffac50a238be424e457bbb5070be38`。从FBX恢复11张嵌入图并连接12/16个材质，写入`Resources/EndoscopyRoom/SourceTextures`；34个静态网格批次写入`Resources/EndoscopyRoom/geometry.bytes`，由`VirtualRoomEnvironment`按房加载。发布报告：[washing-room-model-publication.json](../../artifacts/model-texture-import-20260923/washing-room-model-publication.json)；材质到源图映射及哈希：[washing-room-texture-map.json](../../app/Assets/EndoscopyTheme/ImportedModels/Source/CleaningRoom_20260923/washing-room-texture-map.json) | Android目标Unity导入/编译和正式房间发布通过；模型含2,115,428个三角面。路线已按门洞和柜体净空调整。清洗室运行时及编辑预览主方向光强度为0.77，离房时恢复原值。未运行Play、渲染或Quest验收 |
| 清洗室旧源 `CleaningRoom_Repaired` | 原FBX及8张相对路径贴图连同原目录结构和`.meta`继续保留在`ImportedModels/Source` | 历史备份，不再由EndoscopyRoom运行时消费 |
| 消毒剂、胃镜、电脑、桌子、门交互件 | 复用项目内已有同源文件；消毒剂继续使用`Resources/ClinicalCourse/Disinfectant`现有模型/材质/贴图，不复制重复GUID资源 | 消毒剂FBX与贴图和用户目录文件相同；电脑与桌子FBX无图像贴图引用 |

办公室与诊疗室清单共125个材质组，实际引用32份房间贴图文件（Office 27、Clinical 5；4张相同源图分别存放在两房资源目录，底层不同图像共28张），所有贴图路径均存在。用户目录共83个文件记录：78个目标文件与源文件内容一致，另外5个为已有项目`.meta`，GUID一致而FBX导入设置不同；保留了项目原`.meta`。V04水磨石和V05木纹仍保留为参考资产，但不再由Office/Clinical房间发布器覆盖模型本身材质；表格早期“已绑定到派生房间”的状态由本节更新。验证使用Unity `6000.3.23f1`、Android目标：资源导入/脚本编译及两房发布成功，日志无C#编译错误。未构建APK，未运行Play、渲染截图或Quest；实际画面和设备表现仍待验收。

贴图通道边界：本轮将FBX的`DiffuseColor`图像接到运行材质`BaseMap`，没有宣称还原全部原始材质参数。Office有3个源材质把同一图片同时连接到`TransparentColor`；当前`RoomSurface`为不透明表面。Clinical另有Bump、SpecularFactor、ReflectionColor及3ds Max程序贴图连接，没有对应嵌入图像的连接未生成补图。各房间无底色图路径的发布材质组分别为Office 37组、Clinical 56组；这些继续保留源材质颜色。若需复现透明/粗糙度/金属度等整体观感，还需另做材质通道适配并检查实际画面。

## 2026-09-23 办公室模拟资料图片

以下两张图片由`docs/full-script-visuals/create-office-training-doc-images.ps1`读取`app/Assets/EndoscopyTheme/Resources/ClinicalCourse/office-training-records.json`排版生成，没有使用AI生成或复制医院原表。清洗消毒图逐格读取六个字段与三条模拟记录，保留SIM-R002操作人员空白；人员培训图按同一JSON中的已审核模拟正文拆成字段。源JSON变化时脚本会重新生成；人员培训正文结构不再匹配时脚本会失败，防止悄悄排错字段。两图均明确标注为模拟训练资料。

| 文件 | 来源与校验 | 正式消费者 / 运行任务 | SHA-256 |
| --- | --- | --- | --- |
| `office-disinfection-training-record-v1.png` | 从现有结构化模拟资料排版；无医院来源或临床凭证含义 | `FullScriptOfficeRecords.DocumentImageResource`；办公室`OF-00`及资料画廊 | `8019BAC0017BBCF80BD4D2D080E7A408A46F5340B3281B1FC7DEAEFF25FAA8C7` |
| `office-staff-training-record-v1.png` | 从现有结构化模拟资料正文排版；无真实签名、资质或考核凭证含义 | `FullScriptOfficeRecords.DocumentImageResource`；办公室`OF-05`及资料画廊 | `25FB71D332D1B81DB258195B893A1617FC21E7445D19851A4E121284899451A2` |

Android目标EditMode`office-gallery-zoom-green-2`共18/18通过，覆盖图片资源、目录选取、图外放大、返回与任务状态边界；两张生成图已人工查看。当前真实Play/Quest的清晰度与双眼画面尚未验收。生物学监测、消毒剂监测及产品索证仍缺经审核的完整依据和记录，本次只显示其缺件状态，没有制作“完整资料”。

## 2026-09-23 房间选择画廊示意图

| 文件 | 来源与校验 | 正式消费者 | SHA-256 |
| --- | --- | --- | --- |
| `FullScriptRooms/RoomGallery/room-preview-atlas-v1.png` | Codex内置ImageGen；单张1254×1254、3×3网格图集。提示词见[PROMPTS.md](PROMPTS.md#房间选择画廊示意图2026-09-23)。插画仅表达大厅、办公室、储存库、候诊区、消化诊疗室、呼吸诊疗室、洗消室等房间类别，不是实际房间照片或模型，也不证明房内实际布置。 | `FullScriptRoomVisit.ShowRoomGallery`通过Resources纹理与七个UV裁切显示为导航缩略图；不加载对应房间模型 | `3EBA773D5362721D4529FA60B3553CF6B75759EA08BA4D28CADE172443B1EDB9` |

Android目标`room-gallery-routes-green-2`5/5、`room-gallery-session-green-2`14/14、`room-gallery-drag-green-1`2/2、`room-gallery-ui-green-2`3/3、`room-gallery-stationary-green-2`47/47通过。验证包含安卓目标导入/编译、七种房间各自UV、画廊固定于展开时世界姿态、手势状态机的松手/追踪丢失立即停止，以及越序选房不推进主线/不完成任务。失败红测和一次EditMode HandRef未初始化故障已保留于测试报告与修复进展。尚未运行真实Play、截图/渲染或Quest选房/拨动；实际房间一一对应、坐站可达与预览可读性继续保留在M5.E04/C01视觉验收中。

## 2026-09-23 画廊带练手势图集

| 文件 | 来源与校验 | 正式消费者 | SHA-256 |
| --- | --- | --- | --- |
| `FullScriptRooms/RoomGallery/gallery-gesture-tutorial-v2.png` | Codex内置ImageGen；1536×1024、透明底、3×2图集，五个独立动作格和一个空格；生成原文件与完整提示词见[PROMPTS.md](PROMPTS.md#c01e08-画廊带练手势图集2026-09-23) | `FullScriptRoomGallery`短把手旁的`RawImage`按当前教程状态裁切显示；仅为教学示意，真实动作仍由跟踪手输入，不作为输入或医学证据 | `A2E3E7BFCD809B4E4AEDE1428FD42927A96604B5E80C74C304A566CE06B647C1` |

Android纹理导入禁止NPOT缩放并保留透明度，以维持3×2格UV关系。`gallery-gesture-atlas-green-3.xml`验证图集载入、尺寸、透明与无缩放设置、首次/跳过/重看呈现1/1；`gallery-gesture-map-green-2.xml`验证五种动作与独立UV格映射4/4；`gallery-gesture-stationary-final-1.xml`包含图集的50/50合并回归。图像已查看；Quest下的小尺寸辨识度、坐站可见范围和左右手操作仍待最终验收。

## 2026-09-23 交互音频与背景音乐

| 素材/来源 | 来源与许可 | 正式消费者 | SHA-256 / 验证 |
| --- | --- | --- | --- |
| 运行时合成的16秒单声道环境音乐及确认/翻页/切房短音 | `FullScriptAudioRuntime.cs`内用`AudioClip.Create`和样本合成器生成；无外部音乐或第三方录音、无单独音频素材文件。样本峰值受限于[-1,1]。 | `FullScriptJourneyRuntime.Audio`单例跨房使用；音乐只在体验开始时启播，按钮音只在通过真实Poke提交后触发。声音设置可分开调音量/静音。 | 合成器源代码SHA-256：`4139AC61AFB39BCE385AD4E452306C274A5E832CCA1F23B34DF31488CCB3A77B`；Android目标EditMode音频逻辑7/7、旅程15/15、原地流程48/48通过。程序声音实际输出未在EditMode验证，保留真实Play/Quest验收。 |
| `UI_Panel_Wrist_Action_CompleteCheckMark.wav` | 现有Meta First Hand参考音效；源路径为`app/Assets/BotanicalGardenQR/Experience/Bootstrap/Art/GamePresentation/ReferenceAssets/FirstHand/Audio/UI_Panel_Wrist_Action_CompleteCheckMark.wav`。上游来源和MIT许可见[NOTICE](../../app/Assets/BotanicalGardenQR/Experience/Bootstrap/Art/GamePresentation/ReferenceAssets/NOTICE.md)及同目录`FirstHand/LICENSE.txt`。 | `VisitorCoachThemeAsset.ConfirmSound`用于安小卫接受动作；由`VisitorCoachPresenter.ConfirmationSoundRequested`送入旅程音效源，纳入音量与静音控制。 | `CD0E62A82C643C580B48F6F40A3F4BDE31FD84D2DD4BC975976EFFF7A66D3706`；资源本体未改。 |
| 水槽MV01 `sink-trigger-04m46s-05m06s-with-audio.mp4` | 指定的20秒片段保留原声；来源、SHA与内容边界记录在[C07媒体接入](../STARTUP_REPAIR_PROGRESS_20260923.md#c07c08-媒体接入与视频原声例外)。 | 仅`FullScriptSinkVideo`启用全局静音例外；VideoPhase为Playing时BGM压至已选音量的12%，其他状态恢复。 | 已有视频生命周期EditMode覆盖7/7；真实解码、播放暂停和原声听感尚未验收。 |

运行时保留现有`AudioListener.volume=0`和`AudioListener.pause=true`来隔离未授权的向导旁白与其他媒体；仅上述音效/音乐音源和MV01配置忽略该监听器静音。Unity无图形EditMode不初始化音频输出，因此测试只覆盖样本合成数值、音源属性、状态机和UI近触，不把这些结果记成可听验收。
