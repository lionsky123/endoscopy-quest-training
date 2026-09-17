# C07 · 复用 BotanicalGardenQR 的地图、引导和触碰

用户于 2026-09-16 明确允许借鉴自己开发的 `D:/BotanicalGardenQR-Base`，包括地图点位、流程、移动与引导，只调整为清洗消毒教学主题。本方案覆盖 C06 临时两排布局。参考工程只读，改动全部在本项目内。

## 实际复用范围

| 内容 | 来源 | 本项目用法 |
| --- | --- | --- |
| 六点地图数据 | Content/Published/VisitorMapDefinition.json | 原样复制到 Resources/course-map.json |
| 地图模型 | Assets/StreamingAssets/VisitorAtlasHub/basement_map_8x8_6points.glb | 原样复制到 StreamingAssets/CourseMap/course-map.glb；近触打开路线图 |
| 导航规则与曲线几何 | Modules/MapNavigation 的 Contracts/Runtime 四个文件 | 原样复制到 Runtime/ReferenceMap，通过 CourseMapJourney 适配精灵与关卡 |
| 精灵模型、动画、肖像 | 原有 Oppy 复用资产 | 继续使用，保持静音 |
| 按钮交互 | VisitorDialoguePointableTarget 和 VisitorDialoguePressCommitGate 的行为 | 改用同版 Meta Interaction SDK 205.0.0 的 PokeInteractor、PokeInteractable 与表面组件；在 Select 事件提交一次，释放/取消后重新允许提交 |
| 地面引导 | MapRoutePresentation 的表达方式 | 当前曲线路径、目标光圈、目标光柱和关卡标题 |

未复制其他主题题库、语音、管理后台、账户信息、Library 或设备绑定。临床房间模型保持隐藏。原 SDK 中的鼠标、手柄、射线和凝视替代输入不接入本课。

## 地图与学习流程

沿用原地图 `scale=2`、`speed=0.7 m/s`、`waitDistance=2.5 m`、`resumeDistance=2.0 m` 及六条已设计曲线。以启动时站稳的位置和朝向对齐地图。原地图点位横向跨度为 10.6 m、纵向跨度约 10.1 m（不含行走余量），不再采用 C06 的 5×3 m 范围；需要与原路线相容的实际场地。

确认路线 → 跟随精灵沿曲线 → 玩家进入目标光圈并站稳 → 正确示范或修复问答 → 下一关。参考导航模块负责移动、距离等候、追踪暂停与恢复；本课负责实际到圈判定与答题进度。位置突变触发参考模块的暂停状态，不暗中传送玩家。

P01–P06 顺序对应：正确布局、修复布局、正确工具水路、修复工具水路、正确产品记录、修复产品记录。错误关保持 3 / 3 / 4 题，正确关提供图片、图谱、全景等可选资料。地图可在入口或本站内容页打开、收起，查看不会改变题目成绩。

## 面板触碰修复

旧路径使用每帧指尖深度区间判断。当指尖从按钮前 5 cm 到按钮后 5 cm 时，中间没有一帧落入窄窗口，真实尺寸按钮测试得到 0 次提交（期望 1 次），记录为 artifacts/native-poke-red.xml。

当前按钮改为 Meta SDK 的有界表面扫描和 IPointable 事件路径。Unity XR Hands 继续作为唯一真实指尖数据源，通过适配层提供世界坐标；指尖无效时停用交互器，不保留陈旧位置。手部可见模型保留原 XR Hands 资产。对话距离由 0.64 m 调整至 0.52 m，靠近、按下、取消分别显示反馈，页面切换保留短暂防误触。题目提交不再额外重复查询头显状态后静默丢弃事件；页面可用性由学习状态和追踪状态控制。

这证明并修复了可复现的漏触发路径；不能仅凭编辑器测试推断所有 Quest 现场触碰问题已排除。本轮只读取设备状态，未启动、安装或构建应用。

## 来源与许可

读取参考工作区时 Git HEAD 为 `11e8245edf7b7b6fa5d9c57c399c822b79d35fe8`。按用户授权复用工作区版本，四个导航源码文件保留原命名空间且未改写；其 MIT 许可保存在 Runtime/ReferenceMap/LICENSE.txt。

- 地图 JSON SHA-256：`D42E37A004F269F814348807F33D1E4175A85C7E3A0A70CC61B2646DE2C590CA`。
- 地图 GLB SHA-256：`EDF4E998D2F33C6C00A5807321EEBA42128FAC8767CBFB3E8863DDA801C83D01`。
- Meta Interaction SDK 通过包管理器引用原项目同版本，依其 SDK License；glTFast 使用同版 6.14.1，通过其包许可证引用。没有从参考工程复制整个 Packages 目录。

本课输入适配代码为本项目实现，保留了参考项目的近触表面、按下提交、去重与状态反馈机制，未声称移植全部 BotanicalGardenQR 应用。

## 验证范围

针对性验证使用实际 SDK PokeInteractor 驱动真实按钮表面，而非直接调用按钮回调；检查跨帧触碰、持续按住、取消、地图 GLB 加载、曲线路径跟随、到圈推进、原模型隐藏和原有题库成绩规则。只运行编辑器测试，不运行 APK 构建；真机透视、地图场地和实际双手触碰仍需用户自行构建后确认。

验证结果：artifacts/reference-c07-tests.xml，8 项通过、0 失败，2026-09-16 09:32:06 UTC。包括此前 Composition Layers 原始校验错误的复现与修复验证，全程没有触发 APK 构建。实际地图与入口截图已检查；地图面板位置调整后收起按钮可见。地图用的 glTF 着色器通过 Resources 材质明确保留，避免仅放在 StreamingAssets 时无构建引用。
