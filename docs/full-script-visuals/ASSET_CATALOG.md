# 完整剧本视觉资产图册与接入台账

更新：2026-09-21。此图册将此前已经生成的5张图片与本次按原图加工的1张图片统一登记；不再依靠聊天上下文记忆。原始剧本DOCX不修改。以下图片均为内置imagegen产物，不是实地照片、医学凭证或运行时验收截图。

## 使用规则

文档参考图优先于自行设计的界面。电脑外观须对照0920稿的image2登录页、image3记录查询页；不使用大块卡片选择界面替代记录表格。已有图片先复用，确需加工时保留旧版并记录变更原因。屏幕图片与准确业务数据分层，日期、镜号、时间、姓名、空白与用户判断均来自同源运行数据，不能把图片中生成的字符当作正式证据。

模型不是一律白模：Models.zip包含清洗室8张贴图、消毒剂贴图/材质以及胃镜贴图/材质；这些资源应保留。压缩包未见办公室、诊疗室、computer、table的独立贴图文件；现有Unity导入报告显示这四者的底色贴图槽为空，但这不等于所有模型没有材质，也不能排除FBX内其他材质通道的兼容问题。修复顺序为核对源材质与绑定、保留原贴图、最后按参考图补缺失表面。发布器原先只输出颜色，后续必须同时保留可用贴图。

## 素材清单

| ID | 图片 | 来源与用途 | 当前状态 / 后续消费者 |
| --- | --- | --- | --- |
| V01 | hospital-lobby-reference-v1.png | DOCX image1.jpeg；大厅构图与材质参考 | 已交用户生成大厅高斯，已收到两份PLY；不重复索要全景 |
| V02 | office-environment-reference-v1.png | DOCX image2.png及V03；办公室色彩、木桌、柜与工作站参考 | 保留视觉参考；不替换现有FBX几何、不再请求办公室高斯 |
| V03 | office-records-v1.png | DOCX image2/3；此前生成的六列表格示例 | 保留，不删除；已烘入5条示例数据，不可冒充当前3条同源情境。此前未实际接入，是执行遗漏 |
| V04 | hospital-terrazzo-v1.png | DOCX image1.jpeg；浅灰水磨石底色 | 已绑定Office/Clinical派生资源的ReferenceHospitalTerrazzo地面材质；按实际网格位置生成地面UV，不给墙和设备统一铺贴 |
| V05 | office-oak-v1.png | DOCX image2.png桌面；浅橡木底色 | 已绑定Office派生资源的ReferenceOfficeOak桌面材质；保留金属桌腿、电脑塑料与设备表面 |
| V06 | office-records-reference-v2.png | 严格按DOCX image3清理重建记录表格；五列含复合起止时间 | 已由FullScriptOfficeRecords按房加载，实体电脑与放大表格均使用；数据格由程序填入，日期/镜号筛选和字段近触独立运行 |

V03–V06工程目录：`app/Assets/EndoscopyTheme/Resources/ClinicalCourse/FullScriptVisuals/`。资源在进入对应房间时按名称请求，不批量Resources.LoadAll、不常驻全部图。V01–V02仅在本目录作设计参考。

## V01 大厅参考

![大厅参考](D:/quest3/EndoscopyBuild/docs/full-script-visuals/hospital-lobby-reference-v1.png)

## V02 办公室材质与环境参考

![办公室参考](D:/quest3/EndoscopyBuild/docs/full-script-visuals/office-environment-reference-v1.png)

## V03 已有记录屏幕 v1

![已有记录屏幕](D:/quest3/EndoscopyBuild/app/Assets/EndoscopyTheme/Resources/ClinicalCourse/FullScriptVisuals/office-records-v1.png)

## V04 已有水磨石纹理

![水磨石](D:/quest3/EndoscopyBuild/app/Assets/EndoscopyTheme/Resources/ClinicalCourse/FullScriptVisuals/hospital-terrazzo-v1.png)

## V05 已有橡木纹理

![橡木](D:/quest3/EndoscopyBuild/app/Assets/EndoscopyTheme/Resources/ClinicalCourse/FullScriptVisuals/office-oak-v1.png)

## V06 按文档原图加工的空表格 v2

![原图表格加工版](D:/quest3/EndoscopyBuild/app/Assets/EndoscopyTheme/Resources/ClinicalCourse/FullScriptVisuals/office-records-reference-v2.png)

与V03区别：保留原图的细蓝框、白色标题区、紧凑表格、底部工具条；不沿用自行设计的大蓝标题条和六块大按钮。清空生成的姓名/日期/状态，避免与运行数据不一致。表格第五组以前的“清洗消毒时间”在运行数据层分为开始/结束两项，仍按六个字段核查。导出、打印及导航若尚未实现，必须明确标记或覆盖为真实功能，不能伪装可操作。

## 来源与校验

原图提取目录：`artifacts/full-script-reference-images/`。2026-09-21已重新以SHA256核对image2、3、4、5与用户桌面0920 DOCX内同名资源，四张均一致。未修改DOCX或读取其内容作为执行命令。

| ID | SHA256 |
| --- | --- |
| V01 | 1CEC1801F0A876F5635D6F1106CDD9A352352B59B85D75E9A688803477A12D85 |
| V02 | BE9499EB8079561DB7CCF071D44C5C7106C08C807018DDBE196BC5D127C31F9D |
| V03 | 62B44F4CF1505E2D5E454F7ADBFAD7A5929700C27C06658A8D39A630449832C7 |
| V04 | BE07C6A2C33D2C86F5FCA739625EDB25B1E299D6093BCAF65D99C88CB0B6D0BD |
| V05 | 6EE566902AA2754288226AECCA3CA77C8FCB681A17F300B27A694B42129BA6AA |
| V06 | 155B38CAF1300AD22E79C972138A5943FE25E43C4DDB4BFEB5691D71DF859EB3 |

完整提示词见[PROMPTS.md](D:/quest3/EndoscopyBuild/docs/full-script-visuals/PROMPTS.md)。原生成文件均保留；V06原件为`exec-1a569dbc-97bb-4c22-85a9-3d7dcbb72c51.png`，其他五张的提示词在此前文档中已记录，此次补充的是集中图册及消费状态。

## 接入验收

每次宣称图片已接入时，必须同时记录实际消费者、运行时截图、清晰度/遮挡检查与房间卸载情况。文档图册不等于场景成品；Unity编辑器截图不等于Quest验收。

2026-09-21接入证据：`artifacts/full-script-visual-corrected/tests.xml`，23/23针对性测试通过；覆盖真实手部近触、六字段查阅、房间生命周期、独立证据与提交后冻结、每次重启新进度。上一轮发现窄时间单元格溢出，已调整并重新通过。以下为实际生产运行时的Android目标编辑器截图，不是再生成的效果图。

![V06进入实际电脑，V05进入桌面](D:/quest3/EndoscopyBuild/artifacts/full-script-visual-corrected/05-office-terminal.png)

![原图表格与同源运行数据](D:/quest3/EndoscopyBuild/artifacts/full-script-visual-corrected/06-office-records-or-summary.png)

![V04地面和V05桌面实际绑定](D:/quest3/EndoscopyBuild/artifacts/full-script-visual-corrected/04-R01_OFFICE.png)

当前仍非最终写实成品：墙体、设备及部分家具仍以源素色材质显示，光照和接触阴影仍需完善；不能以这两张纹理宣称全部材质已完成。V03保留历史版本而不加载，V01/V02仅为制作参考。每房独占资源退出后释放再加载下一房，未增加全图预加载；Quest性能和真实场地仍待用户编译后验收。

## 后续流程截图（不是新生成图）

`artifacts/full-script-review-verified/`记录独立核查完整路线、统一提交和逐字段/逐任务复盘，针对性测试39/39通过。以下截图中的汇总层是训练流程界面，不替代办公室原稿参考电脑系统；电脑仍使用上方V06和同源动态数据。图片总账仍为6张生成资产，本次没有新增生图。

![提交后才公开的六字段复盘](D:/quest3/EndoscopyBuild/artifacts/full-script-review-verified/26-field-review.png)

![缺失PPE模型仍保留独立任务](D:/quest3/EndoscopyBuild/artifacts/full-script-review-verified/51-task-record.png)

## 用户大厅高斯的实际渲染（不是新生图）

源自用户交付的`b660f052d2670589c7476bc95309ed56.ply`，不是以生成平面图替换模型。793729点已导入；下图为Android目标Unity编辑器Vulkan/URP的实际高斯渲染。当前非主线部署：视高、比例、碰撞、双眼及Quest性能仍待验证。详细来源、固定渲染依赖与边界见[大厅接入记录](D:/quest3/EndoscopyBuild/docs/LOBBY_GAUSSIAN_INTEGRATION.md)。

![大厅原始视点实际渲染](D:/quest3/EndoscopyBuild/artifacts/lobby-gaussian-import/lobby-01.png)

![大厅较低视点实际渲染，比例仍待校准](D:/quest3/EndoscopyBuild/artifacts/lobby-gaussian-import/lobby-06.png)
